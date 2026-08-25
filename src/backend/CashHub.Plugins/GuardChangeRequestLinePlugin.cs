using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Guard de sanic_chtblclientdatachangerequestline — cierra S3 del analisis
    /// (docs/clientadmin-rebuild/00-analisis-y-plan.md): "no hay guard sobre
    /// ClientDataChangeRequestLines; crear/borrar lineas de un RFC ajeno en Draft vía Web
    /// API es posible para un Approver con Write global". Paso SINCRONO Pre-operation en
    /// Create/Update/Delete de la linea; las reglas viven en el dominio puro
    /// (<see cref="ChangeRequestLineGuard"/>), aca solo se arma el contexto y se propaga
    /// el bloqueo — mismo esqueleto que <see cref="GuardChangeRequestApprovalPlugin"/>.
    ///
    /// Reglas: el RFC padre debe estar en Draft, sin lineas duplicadas por (RFC,
    /// FieldDefinition), y solo el creador del RFC o un aprobador de la tabla objetivo
    /// puede tocar sus lineas (la membresia se resuelve con
    /// <see cref="ChangeRequestApproverResolver"/>, la MISMA que usa el guard de cabecera).
    ///
    /// 🧨 Auto-bloqueo evitado: cuando sanic_CHResolveChangeRequest (ResolveChangeRequestPlugin)
    /// aplica o rechaza un RFC, marca cada linea Applied/Denied con la cabecera YA en
    /// Submitted — la transicion que este guard bloquearia si la evaluara como una
    /// escritura humana. La exencion es por PROFUNDIDAD (<see cref="ChangeRequestLineGuard.IsWriteExempt"/>,
    /// mismo patron que <see cref="RecountingGuard.IsWriteExempt"/>): esa escritura llega
    /// vía IOrganizationService DESDE OTRO plugin y corre a depth &gt; 1; el actor humano y
    /// las apps canvas SIEMPRE entran a depth 1.
    /// </summary>
    public sealed class GuardChangeRequestLinePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var message = context.MessageName;

            tracing.Trace("GuardChangeRequestLine: {0} de linea, actor={1}, depth={2}.",
                message, context.InitiatingUserId, context.Depth);

            ChangeRequestLineGuardResult result;
            try
            {
                result = Evaluate(context, serviceFactory, tracing, message);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardChangeRequestLine: error inesperado evaluando {0}: {1}", message, ex);
                throw new InvalidPluginExecutionException(
                    $"GuardChangeRequestLine no pudo evaluar {message} de la linea: {ex.Message}", ex);
            }

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardChangeRequestLine: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardChangeRequestLine: escritura permitida.");
        }

        private static ChangeRequestLineGuardResult Evaluate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message != "Create" && message != "Update" && message != "Delete")
            {
                tracing.Trace("GuardChangeRequestLine: mensaje {0} fuera de alcance, se permite.", message);
                return ChangeRequestLineGuardResult.Allow();
            }

            var isExempt = ChangeRequestLineGuard.IsWriteExempt(context.Depth);
            if (isExempt)
            {
                tracing.Trace(
                    "GuardChangeRequestLine: EXENTO (depth={0}) — escritura del propio pipeline " +
                    "(ResolveChangeRequestPlugin marcando el desenlace de sus lineas).", context.Depth);
                return ChangeRequestLineGuard.Evaluate(
                    isExempt: true, parentResolved: false, parentStatus: null,
                    parentCreatorId: Guid.Empty, actorId: Guid.Empty,
                    isAuthorizedApprover: false, isDuplicateFieldDefinition: false);
            }

            var actorId = context.InitiatingUserId;
            var systemService = serviceFactory.CreateOrganizationService(null);

            var changeRequestId = ResolveChangeRequestId(context, systemService, tracing, message);
            var fieldDefinitionId = ResolveFieldDefinitionId(context, message);

            if (changeRequestId == Guid.Empty)
            {
                return ChangeRequestLineGuard.Evaluate(
                    isExempt: false, parentResolved: false, parentStatus: null,
                    parentCreatorId: Guid.Empty, actorId: actorId,
                    isAuthorizedApprover: false, isDuplicateFieldDefinition: false);
            }

            var parent = ResolveParent(systemService, tracing, changeRequestId);

            var isAuthorizedApprover = false;
            if (parent.Resolved && actorId != parent.CreatorId)
            {
                isAuthorizedApprover = ChangeRequestApproverResolver.IsUserApproverOfTable(
                    serviceFactory, tracing, parent.TargetTable, actorId);
            }

            var isDuplicate = fieldDefinitionId.HasValue
                && IsDuplicateFieldDefinition(
                    systemService, changeRequestId, fieldDefinitionId.Value, ExcludingLineId(context, message));

            tracing.Trace(
                "GuardChangeRequestLine: {0} rfc={1} estado={2} creadorRfc={3} actor={4} aprobador={5} duplicado={6}.",
                message, changeRequestId, Fmt(parent.Status), parent.CreatorId, actorId, isAuthorizedApprover, isDuplicate);

            return ChangeRequestLineGuard.Evaluate(
                isExempt: false, parentResolved: parent.Resolved, parentStatus: parent.Status,
                parentCreatorId: parent.CreatorId, actorId: actorId,
                isAuthorizedApprover: isAuthorizedApprover, isDuplicateFieldDefinition: isDuplicate);
        }

        /// <summary>
        /// sanic_changerequestid de la linea. En Create/Update viene del Target; en
        /// Delete (y en un Update que no lo toca) se lee de la PreImage y, a falta de
        /// ella, del registro como sistema (chequeo de seguridad de solo lectura).
        /// </summary>
        private static Guid ResolveChangeRequestId(
            IPluginExecutionContext context, IOrganizationService system, ITracingService tracing, string message)
        {
            if (message != "Delete"
                && context.InputParameters.TryGetValue("Target", out var rawTarget) && rawTarget is Entity target
                && target.Contains(Schema.ChangeRequestLine.ChangeRequestLookup))
            {
                return target.GetAttributeValue<EntityReference>(Schema.ChangeRequestLine.ChangeRequestLookup)?.Id
                       ?? Guid.Empty;
            }

            if (context.PreEntityImages.TryGetValue(Schema.ChangeRequestLine.PreImageName, out var pre)
                && pre != null && pre.Contains(Schema.ChangeRequestLine.ChangeRequestLookup))
            {
                return pre.GetAttributeValue<EntityReference>(Schema.ChangeRequestLine.ChangeRequestLookup)?.Id
                       ?? Guid.Empty;
            }

            var lineId = ResolveLineId(context);
            if (lineId == Guid.Empty)
            {
                return Guid.Empty;
            }

            tracing.Trace("GuardChangeRequestLine: sin PreImage utilizable, se lee la linea {0} como sistema.", lineId);
            try
            {
                var current = system.Retrieve(
                    Schema.ChangeRequestLine.LogicalName, lineId,
                    new ColumnSet(Schema.ChangeRequestLine.ChangeRequestLookup));
                return current.GetAttributeValue<EntityReference>(Schema.ChangeRequestLine.ChangeRequestLookup)?.Id
                       ?? Guid.Empty;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardChangeRequestLine: no se pudo leer la linea {0}: {1}", lineId, ex.Message);
                return Guid.Empty;
            }
        }

        /// <summary>
        /// sanic_fielddefinition SOLO si el request lo trae explicitamente (Create
        /// siempre; Update solo si cambia de campo). null = no hay chequeo de duplicado
        /// que hacer (Delete, o un Update que no toca el campo).
        /// </summary>
        private static Guid? ResolveFieldDefinitionId(IPluginExecutionContext context, string message)
        {
            if (message == "Delete")
            {
                return null;
            }

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                return null;
            }

            if (!target.Contains(Schema.ChangeRequestLine.FieldDefinitionLookup))
            {
                if (message == "Create")
                {
                    throw new InvalidPluginExecutionException(
                        "La linea de RFC no trae sanic_fielddefinition; no se puede crear sin el campo objetivo.");
                }

                return null;
            }

            return target.GetAttributeValue<EntityReference>(Schema.ChangeRequestLine.FieldDefinitionLookup)?.Id;
        }

        private static ParentRfc ResolveParent(IOrganizationService system, ITracingService tracing, Guid changeRequestId)
        {
            try
            {
                var header = system.Retrieve(
                    Schema.ChangeRequest.LogicalName, changeRequestId,
                    new ColumnSet(Schema.ChangeRequest.ChangeStatus, Schema.ChangeRequest.CreatedBy, Schema.ChangeRequest.TargetTable));

                return new ParentRfc
                {
                    Resolved = true,
                    Status = header.GetAttributeValue<OptionSetValue>(Schema.ChangeRequest.ChangeStatus)?.Value,
                    CreatorId = header.GetAttributeValue<EntityReference>(Schema.ChangeRequest.CreatedBy)?.Id ?? Guid.Empty,
                    TargetTable = header.GetAttributeValue<string>(Schema.ChangeRequest.TargetTable),
                };
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardChangeRequestLine: no se pudo leer el RFC padre {0}: {1}", changeRequestId, ex.Message);
                return new ParentRfc { Resolved = false };
            }
        }

        private static bool IsDuplicateFieldDefinition(
            IOrganizationService system, Guid changeRequestId, Guid fieldDefinitionId, Guid excludeLineId)
        {
            var query = new QueryExpression(Schema.ChangeRequestLine.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
            };
            query.Criteria.AddCondition(
                Schema.ChangeRequestLine.ChangeRequestLookup, ConditionOperator.Equal, changeRequestId);
            query.Criteria.AddCondition(
                Schema.ChangeRequestLine.FieldDefinitionLookup, ConditionOperator.Equal, fieldDefinitionId);
            if (excludeLineId != Guid.Empty)
            {
                query.Criteria.AddCondition(Schema.ChangeRequestLine.Id, ConditionOperator.NotEqual, excludeLineId);
            }

            return system.RetrieveMultiple(query).Entities.Count > 0;
        }

        /// <summary>La propia linea, para excluirla del chequeo de duplicados en un Update. Vacio en Create.</summary>
        private static Guid ExcludingLineId(IPluginExecutionContext context, string message)
        {
            return message == "Update" ? ResolveLineId(context) : Guid.Empty;
        }

        private static Guid ResolveLineId(IPluginExecutionContext context)
        {
            if (context.InputParameters.TryGetValue("Target", out var raw))
            {
                if (raw is Entity target && target.Id != Guid.Empty)
                {
                    return target.Id;
                }

                if (raw is EntityReference reference)
                {
                    return reference.Id;
                }
            }

            return context.PrimaryEntityId;
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";

        private struct ParentRfc
        {
            public bool Resolved { get; set; }
            public int? Status { get; set; }
            public Guid CreatorId { get; set; }
            public string? TargetTable { get; set; }
        }
    }
}
