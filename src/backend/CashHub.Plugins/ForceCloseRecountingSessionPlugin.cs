using System;
using System.Collections.Generic;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing de <c>sanic_CHForceCloseRecountingSession</c>: el supervisor cierra a la
    /// fuerza una sesion de recuento y libera las tulas que el cajero tenia en la mano,
    /// COMO UNIDAD TRANSACCIONAL.
    ///
    /// ORDEN, y es lo importante:
    ///   1. juntar el panorama (tulas de la sesion, solicitudes sin resolver)
    ///   2. autorizar con los frenos (<see cref="RecountingGuard.EvaluateForceClose"/>)
    ///   3. si bloquea -> devolver el motivo como DATO, con NOMBRE Y APELLIDO de lo que
    ///      traba, y CERO escrituras
    ///   4. si autoriza -> liberar cada tula y cerrar la sesion en la misma ejecucion
    ///
    /// EL GUARD VIVE ACA Y NO EN LA APP a proposito. Cerrar la sesion es un Update de
    /// dos columnas: cualquiera con el rol de supervisor puede hacerlo por Web API o
    /// desde Excel y saltearse los frenos, las tulas y el barrido. Poner los chequeos
    /// en Power Fx seria dejar la puerta abierta y un cartel de "no pasar".
    ///
    /// LIBERAR = LA MISMA DEVOLUCION DE UNA TULA. Se reusa <see cref="BagRecountWipe"/>
    /// y el mismo estado destino: el cierre forzado no inventa una tercera forma de
    /// sacar una tula de la mesa. Solo se liberan las que estan EN la mesa
    /// (<see cref="RecountingGuard.ShouldReleaseOnForceClose"/>); una Recontada ya tiene
    /// sus asientos y no se revierte por aca.
    ///
    /// Las decisiones viven en <see cref="RecountingGuard"/> (bajo test). Aca solo hay
    /// lecturas y escrituras.
    /// </summary>
    public sealed class ForceCloseRecountingSessionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var sessionId = ReadGuid(context, Schema.ForceCloseSessionApi.InSessionId);
            var reason = ReadOptionalString(context, Schema.ForceCloseSessionApi.InReason);
            var actor = context.InitiatingUserId;

            tracing.Trace("ForceClose: sesion {0} actor={1} motivo={2}.",
                sessionId, actor, string.IsNullOrWhiteSpace(reason) ? "(sin motivo)" : reason);

            // Como SISTEMA: el supervisor no tiene —ni le vamos a dar— Delete sobre los
            // hijos del recuento. La autorizacion de QUIEN puede llamar vive en el
            // privilegio de ejecucion de la Custom API.
            var system = factory.CreateOrganizationService(null);

            // ---- 1. La sesion ----
            Entity session;
            try
            {
                session = system.Retrieve(
                    Schema.RecountingSession.LogicalName, sessionId,
                    new ColumnSet(Schema.RecountingSession.SessionForciblyClosed));
            }
            catch (Exception ex)
            {
                tracing.Trace("ForceClose: no se pudo leer la sesion {0}: {1}", sessionId, ex);
                Respond(context, Schema.ForceCloseSessionApi.OutcomeBlocked,
                    "No se pudo leer la sesion de recuento indicada.");
                return;
            }

            var yaCerrada = session.GetAttributeValue<bool>(
                Schema.RecountingSession.SessionForciblyClosed);

            // ---- 2. El panorama ANTES de decidir ----
            var bags = ReadSessionBags(system, sessionId);
            var esperandoRevision = new List<string>();
            var aLiberar = new List<KeyValuePair<Guid, string>>();

            foreach (var bag in bags)
            {
                var estado = bag.GetAttributeValue<OptionSetValue>(Schema.Bag.BagStatus)?.Value;
                var numero = bag.GetAttributeValue<string>(Schema.Bag.BagId) ?? bag.Id.ToString();

                if (estado == ReceptionGuard.BagPendienteSupervisorRecuento)
                {
                    esperandoRevision.Add(numero);
                }
                else if (RecountingGuard.ShouldReleaseOnForceClose(estado))
                {
                    aLiberar.Add(new KeyValuePair<Guid, string>(bag.Id, numero));
                }
            }

            var pendientes = CountPendingApprovals(system, tracing, sessionId);
            tracing.Trace("ForceClose: {0} tulas en la sesion — {1} a liberar, {2} esperando " +
                          "revision; {3} solicitudes sin resolver.",
                bags.Count, aLiberar.Count, esperandoRevision.Count, pendientes);

            // ---- 3. AUTORIZAR. Antes de tocar un solo registro. ----
            var verdict = RecountingGuard.EvaluateForceClose(
                true, yaCerrada, pendientes, esperandoRevision);

            if (!verdict.IsAllowed)
            {
                tracing.Trace("ForceClose: BLOQUEADO — {0}", verdict.Reason);
                Respond(context, Schema.ForceCloseSessionApi.OutcomeBlocked, verdict.Reason);
                return;
            }

            // ---- 4. El efecto. De aca en adelante, todo o nada. ----
            try
            {
                var total = new BagRecountWipe.Counts();
                foreach (var bag in aLiberar)
                {
                    total.Add(BagRecountWipe.Run(system, tracing, bag.Key));

                    system.Update(new Entity(Schema.Bag.LogicalName, bag.Key)
                    {
                        [Schema.Bag.BagStatus] =
                            new OptionSetValue(ReceptionGuard.BagDevueltaParaReasignacion),
                        [Schema.Bag.RecountingSessionLookup] = null,
                        [Schema.Bag.TotalNio] = 0m,
                        [Schema.Bag.TotalUsd] = 0m,
                        [Schema.Bag.TotalEur] = 0m,
                    });
                }

                // La sesion, AL FINAL: si algo del barrido falla, la transaccion se
                // revierte y la sesion no queda cerrada con tulas colgando.
                var cierre = new Entity(Schema.RecountingSession.LogicalName, sessionId)
                {
                    [Schema.RecountingSession.SessionForciblyClosed] = true,
                    [Schema.RecountingSession.ForcedClosureBy] =
                        new EntityReference("systemuser", actor),
                };

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    cierre[Schema.RecountingSession.ReasonForForcedClosure] = reason;
                }

                system.Update(cierre);

                var resumen = aLiberar.Count == 0
                    ? "Sesion cerrada a la fuerza. No habia tulas en la mesa que liberar."
                    : "Sesion cerrada a la fuerza. Se liberaron " + aLiberar.Count +
                      (aLiberar.Count == 1 ? " tula (" : " tulas (") +
                      string.Join(", ", Names(aLiberar)) + "). " + total.Describe();

                tracing.Trace("ForceClose: OK. {0}", resumen);
                Respond(context, Schema.ForceCloseSessionApi.OutcomeClosed, resumen);
            }
            catch (Exception ex)
            {
                // Se relanza a proposito: media operacion dejaria la sesion cerrada con
                // tulas enllavadas colgando, que es peor que no haber hecho nada.
                tracing.Trace("ForceClose: fallo el cierre de la sesion {0}: {1}", sessionId, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo cerrar la sesion: " + ex.Message + ". No se cerro ni se libero nada.", ex);
            }
        }

        private static List<Entity> ReadSessionBags(IOrganizationService system, Guid sessionId)
        {
            var query = new QueryExpression(Schema.Bag.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Bag.BagStatus, Schema.Bag.BagId),
            };
            query.Criteria.AddCondition(
                Schema.Bag.RecountingSessionLookup, ConditionOperator.Equal, sessionId);

            return new List<Entity>(system.RetrieveMultiple(query).Entities);
        }

        /// <summary>
        /// Solicitudes de ESTA sesion todavia sin resolver.
        ///
        /// El vinculo con la sesion no es un lookup: el GUID viaja dentro del JSON de
        /// sanic_requestresources, que es como lo escribe y lo lee el resto del sistema.
        /// De ahi el Like sobre el texto. Es una consulta acotada —solicitudes sin
        /// resolver, que son pocas— y corre UNA vez por cierre.
        /// </summary>
        private static int CountPendingApprovals(
            IOrganizationService system, ITracingService tracing, Guid sessionId)
        {
            try
            {
                var query = new QueryExpression(Schema.ApprovalRequest.LogicalName)
                {
                    ColumnSet = new ColumnSet(false),
                };
                query.Criteria.AddCondition(
                    Schema.ApprovalRequest.ApprovalStatus, ConditionOperator.Null);
                query.Criteria.AddCondition(
                    SupervisorMirrors.ApprovalRequest.RequestResources,
                    ConditionOperator.Like, "%" + sessionId.ToString() + "%");

                return system.RetrieveMultiple(query).Entities.Count;
            }
            catch (Exception ex)
            {
                // Fail-closed: si no se puede saber si hay solicitudes abiertas, se
                // reporta una para que el cierre se frene. Cerrar a ciegas es lo que no
                // podemos hacer.
                tracing.Trace("ForceClose: no se pudieron contar las solicitudes: {0}", ex.Message);
                return 1;
            }
        }

        private static IEnumerable<string> Names(IEnumerable<KeyValuePair<Guid, string>> bags)
        {
            foreach (var bag in bags)
            {
                yield return bag.Value;
            }
        }

        private static Guid ReadGuid(IPluginExecutionContext context, string name)
        {
            if (!context.InputParameters.Contains(name))
            {
                throw new InvalidPluginExecutionException("Falta el parametro " + name + ".");
            }

            var raw = context.InputParameters[name];
            if (raw is Guid guid && guid != Guid.Empty)
            {
                return guid;
            }

            if (raw is string text && Guid.TryParse(text, out var parsed) && parsed != Guid.Empty)
            {
                return parsed;
            }

            throw new InvalidPluginExecutionException("El parametro " + name + " no es un id valido.");
        }

        private static string ReadOptionalString(IPluginExecutionContext context, string name)
        {
            return context.InputParameters.Contains(name)
                ? context.InputParameters[name] as string
                : null;
        }

        private static void Respond(IPluginExecutionContext context, string outcome, string message)
        {
            context.OutputParameters[Schema.ForceCloseSessionApi.OutOutcome] = outcome;
            context.OutputParameters[Schema.ForceCloseSessionApi.OutMessage] = message ?? string.Empty;
        }
    }
}
