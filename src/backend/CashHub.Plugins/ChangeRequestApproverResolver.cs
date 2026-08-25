using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Resolucion de membresia del grupo Approver (Fase 2) para la tabla objetivo de un
    /// RFC: sanic_chtblclientdatachangepermission (Type=Approver) -> AAD group team ->
    /// teammembership. Extraida de GuardChangeRequestApprovalPlugin para que
    /// ResolveChangeRequestPlugin (la Custom API que reemplaza los 4 flujos
    /// materializadores) valide la MISMA regla sin duplicarla — las dos mitades del
    /// circuito (guard de cabecera + resolucion) tienen que estar de acuerdo sobre quien
    /// es aprobador.
    /// </summary>
    internal static class ChangeRequestApproverResolver
    {
        /// <summary>
        /// Fail-closed: si no hay grupo configurado o no se puede resolver, se lanza
        /// excepcion (no se aprueba). Se lee como sistema (chequeo de seguridad).
        /// </summary>
        public static bool IsUserApproverOfTable(
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
                "ChangeRequestApproverResolver: membresia aprobador tabla={0} grupo={1} usuario={2} => {3}",
                targetTable, aadGroupId, userId, isMember);

            return isMember;
        }
    }
}
