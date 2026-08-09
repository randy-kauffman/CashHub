using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Separacion de funciones para los RFC de administracion de clientes.
    ///
    /// La app canvas oculta los botones de aprobar/rechazar, pero eso es cosmetico: un
    /// Contributor con prvWrite/prvCreate puede pegarle el estado por Web API o Excel y
    /// auto-aprobarse. Este paso corre en Pre-operation de Create y Update de
    /// sanic_chtblclientdatachangerequest y bloquea la transicion ilegitima antes de que
    /// los flujos de aplicacion la tomen. La decision (allow/block) vive en el dominio puro
    /// (ChangeRequestApprovalGuard); aca se arma el contexto, se resuelve la membresia del
    /// grupo aprobador (Fase 2) y se propaga el bloqueo.
    /// </summary>
    public sealed class GuardChangeRequestApprovalPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("GuardChangeRequestApproval: sin Target de tipo Entity, se omite.");
                return;
            }

            var isCreate = string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase);
            var eventType = isCreate ? ChangeRequestEvent.Create : ChangeRequestEvent.Update;

            // Sin cambio de estado en este request no hay nada que evaluar.
            if (!target.Contains(Schema.ChangeRequest.ChangeStatus)
                || !(target[Schema.ChangeRequest.ChangeStatus] is OptionSetValue newStatusOption))
            {
                tracing.Trace("GuardChangeRequestApproval: {0} sin sanic_changestatus, se omite.", context.MessageName);
                return;
            }

            var newStatus = newStatusOption.Value;
            var initiatingUserId = context.InitiatingUserId;

            int? previousStatus = null;
            var creatorId = Guid.Empty;
            string? targetTable = null;

            if (isCreate)
            {
                // El registro aun no existe: el creador sera el usuario que lo dispara.
                creatorId = initiatingUserId;
                targetTable = target.GetAttributeValue<string>(Schema.ChangeRequest.TargetTable);
            }
            else
            {
                var snapshot = ResolvePreState(context, serviceFactory, tracing, target.Id);
                previousStatus = snapshot.Status;
                creatorId = snapshot.CreatorId;
                targetTable = snapshot.TargetTable;
            }

            var isDecision = newStatus == ChangeRequestApprovalGuard.Approved
                             || newStatus == ChangeRequestApprovalGuard.Rejected;

            // La membresia del grupo aprobador (Fase 2) solo se consulta cuando hace falta:
            // en la decision humana (aprobar/rechazar) de un Update.
            var isAuthorizedApprover = false;
            if (!isCreate && isDecision)
            {
                isAuthorizedApprover = IsUserApproverOfTable(
                    serviceFactory, tracing, targetTable, initiatingUserId);
            }

            tracing.Trace(
                "GuardChangeRequestApproval: msg={0} rfc={1} prev={2} new={3} tabla={4} creador={5} actor={6} aprobador={7}",
                context.MessageName, target.Id,
                previousStatus.HasValue ? previousStatus.Value.ToString() : "n/a",
                newStatus, targetTable ?? "n/a", creatorId, initiatingUserId, isAuthorizedApprover);

            var result = ChangeRequestApprovalGuard.Evaluate(
                eventType, previousStatus, newStatus, creatorId, initiatingUserId, isAuthorizedApprover);

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardChangeRequestApproval: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardChangeRequestApproval: transicion permitida.");
        }

        /// <summary>
        /// Resuelve si <paramref name="userId"/> pertenece al grupo Approver configurado para
        /// <paramref name="targetTable"/>. Fail-closed: si no hay grupo configurado o no se puede
        /// resolver, se lanza excepcion (no se aprueba). Se lee como sistema (chequeo de seguridad).
        /// </summary>
        private static bool IsUserApproverOfTable(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string? targetTable,
            Guid userId)
        {
            if (string.IsNullOrWhiteSpace(targetTable))
            {
                throw new InvalidPluginExecutionException(
                    "No se pudo determinar la tabla objetivo del RFC; no se puede validar la aprobacion.");
            }

            var service = serviceFactory.CreateOrganizationService(null);

            var permissionQuery = new QueryExpression(Schema.ChangePermission.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.ChangePermission.AadGroupId),
                TopCount = 1
            };
            permissionQuery.Criteria.AddCondition(
                Schema.ChangePermission.TargetTable, ConditionOperator.Equal, targetTable);
            permissionQuery.Criteria.AddCondition(
                Schema.ChangePermission.Type, ConditionOperator.Equal, Schema.ChangePermission.TypeApprover);

            var permissionRows = service.RetrieveMultiple(permissionQuery).Entities;
            var permission = permissionRows.Count > 0 ? permissionRows[0] : null;

            var aadGroupRaw = permission?.GetAttributeValue<string>(Schema.ChangePermission.AadGroupId);
            if (permission == null || string.IsNullOrWhiteSpace(aadGroupRaw)
                || !Guid.TryParse(aadGroupRaw, out var aadGroupId))
            {
                throw new InvalidPluginExecutionException(
                    $"No hay un grupo de aprobadores valido configurado para la tabla '{targetTable}'; " +
                    "no se puede validar la aprobacion.");
            }

            // AAD group team cuya azureactivedirectoryobjectid == grupo, con el usuario como miembro.
            var teamQuery = new QueryExpression(Schema.Team.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Team.Id),
                TopCount = 1
            };
            teamQuery.Criteria.AddCondition(Schema.Team.AadObjectId, ConditionOperator.Equal, aadGroupId);

            var membershipLink = teamQuery.AddLink(
                Schema.Team.MembershipRelationship, Schema.Team.Id, Schema.Team.Id);
            var userLink = membershipLink.AddLink(
                Schema.SystemUser.LogicalName, Schema.SystemUser.Id, Schema.SystemUser.Id);
            userLink.LinkCriteria.AddCondition(Schema.SystemUser.Id, ConditionOperator.Equal, userId);

            var isMember = service.RetrieveMultiple(teamQuery).Entities.Count > 0;
            tracing.Trace(
                "GuardChangeRequestApproval: membresia aprobador tabla={0} grupo={1} usuario={2} => {3}",
                targetTable, aadGroupId, userId, isMember);

            return isMember;
        }

        private static PreState ResolvePreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid recordId)
        {
            // Preferimos la pre-image registrada; si falta, leemos el registro como sistema
            // (chequeo de seguridad de solo lectura, independiente de los privilegios del actor).
            if (context.PreEntityImages.TryGetValue(Schema.ChangeRequest.PreImageName, out var preImage)
                && preImage != null
                && preImage.Contains(Schema.ChangeRequest.ChangeStatus))
            {
                return new PreState(
                    ReadStatus(preImage),
                    ReadCreator(preImage),
                    preImage.GetAttributeValue<string>(Schema.ChangeRequest.TargetTable));
            }

            tracing.Trace("GuardChangeRequestApproval: sin pre-image utilizable, se lee el RFC como sistema.");
            var service = serviceFactory.CreateOrganizationService(null);
            var current = service.Retrieve(
                Schema.ChangeRequest.LogicalName,
                recordId,
                new ColumnSet(
                    Schema.ChangeRequest.ChangeStatus,
                    Schema.ChangeRequest.CreatedBy,
                    Schema.ChangeRequest.TargetTable));

            return new PreState(
                ReadStatus(current),
                ReadCreator(current),
                current.GetAttributeValue<string>(Schema.ChangeRequest.TargetTable));
        }

        private static int? ReadStatus(Entity entity)
        {
            return entity.GetAttributeValue<OptionSetValue>(Schema.ChangeRequest.ChangeStatus)?.Value;
        }

        private static Guid ReadCreator(Entity entity)
        {
            return entity.GetAttributeValue<EntityReference>(Schema.ChangeRequest.CreatedBy)?.Id ?? Guid.Empty;
        }

        private struct PreState
        {
            public PreState(int? status, Guid creatorId, string? targetTable)
            {
                Status = status;
                CreatorId = creatorId;
                TargetTable = targetTable;
            }

            public int? Status { get; }
            public Guid CreatorId { get; }
            public string? TargetTable { get; }
        }
    }
}
