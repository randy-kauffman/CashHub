using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Resuelve si un usuario pertenece al grupo aprobador de supervisores.
    ///
    /// OJO — es el EQUIPO DE DATAVERSE ligado al grupo AAD, no AAD. Dataverse
    /// materializa la membresia de un equipo de grupo AAD de forma perezosa, al
    /// acceder el usuario: alguien recien agregado al grupo sigue dando false hasta
    /// que entre al entorno. Consultar AAD por otra via (p.ej. Office365Groups desde
    /// una canvas app) daria un veredicto DISTINTO al que aplican los guards, y esa
    /// divergencia es peor que no tener el chequeo.
    ///
    /// El grupo sale de CH-Tbl-Parameter, clave
    /// <see cref="ApproverGroupConfiguration.ParameterKey"/>. Politica fail-closed:
    /// si el parametro falta, esta inactivo o es ilegible, se LANZA. Todos los
    /// llamadores usan esto para levantar una restriccion, asi que no poder
    /// verificarlo tiene que bloquear, nunca permitir.
    ///
    /// NOTA: <see cref="GuardReceptionPlugin"/> tiene su propia copia con memoizacion
    /// por invocacion (resuelve el mismo usuario varias veces en una ejecucion). No se
    /// unifico para no tocar codigo en produccion que funciona; si se toca, plegarlo aca.
    /// </summary>
    internal static class ApproverTeamLookup
    {
        public static bool IsApprover(
            IOrganizationService system, ITracingService tracing, Guid userId)
        {
            var groupId = ResolveGroupId(system, tracing);

            try
            {
                var query = new QueryExpression(Schema.Team.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.Team.Id),
                    TopCount = 1
                };
                query.Criteria.AddCondition(Schema.Team.AadObjectId, ConditionOperator.Equal, groupId);

                var membershipLink = query.AddLink(
                    Schema.Team.MembershipRelationship, Schema.Team.Id, Schema.Team.Id);
                var userLink = membershipLink.AddLink(
                    Schema.SystemUser.LogicalName, Schema.SystemUser.Id, Schema.SystemUser.Id);
                userLink.LinkCriteria.AddCondition(Schema.SystemUser.Id, ConditionOperator.Equal, userId);

                var isMember = system.RetrieveMultiple(query).Entities.Count > 0;
                tracing.Trace("ApproverTeamLookup: grupo={0} usuario={1} => {2}.", groupId, userId, isMember);
                return isMember;
            }
            catch (Exception ex)
            {
                tracing.Trace("ApproverTeamLookup: fallo la consulta de membresia del grupo {0}: {1}",
                    groupId, ex);
                throw new InvalidPluginExecutionException(
                    ApproverGroupConfiguration.MembershipUnavailableMessage);
            }
        }

        private static Guid ResolveGroupId(IOrganizationService system, ITracingService tracing)
        {
            string? rawValue;
            try
            {
                var rows = system.RetrieveMultiple(new QueryExpression(Schema.Parameter.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.Parameter.Value),
                    TopCount = 1,
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                Schema.Parameter.Key, ConditionOperator.Equal,
                                ApproverGroupConfiguration.ParameterKey)
                        }
                    }
                });

                rawValue = rows.Entities.Count > 0
                    ? rows.Entities[0].GetAttributeValue<string>(Schema.Parameter.Value)
                    : null;
            }
            catch (Exception ex)
            {
                tracing.Trace("ApproverTeamLookup: no se pudo leer CH-Tbl-Parameter: {0}", ex);
                throw new InvalidPluginExecutionException(
                    ApproverGroupConfiguration.MembershipUnavailableMessage);
            }

            // La politica fail-closed vive en el dominio (y esta bajo test).
            var resolution = ApproverGroupConfiguration.Resolve(rawValue);
            if (!resolution.IsConfigured)
            {
                tracing.Trace("ApproverTeamLookup: parametro '{0}' ausente, inactivo o invalido.",
                    ApproverGroupConfiguration.ParameterKey);
                throw new InvalidPluginExecutionException(resolution.Reason);
            }

            return resolution.GroupId;
        }
    }
}
