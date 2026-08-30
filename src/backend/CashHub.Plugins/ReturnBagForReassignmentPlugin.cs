using System;
using System.Collections.Generic;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing de <c>sanic_CHReturnBagForReassignment</c>: saca una tula de la mesa y
    /// la deja libre para que la tome otro cajero, borrando TODO lo que se alcanzo a
    /// cargar, COMO UNIDAD TRANSACCIONAL.
    ///
    /// EL PROBLEMA. Una tula que entra en "Asignada a cajero" quedaba enllavada: sus
    /// unicas salidas eran Recontada (terminal) y el estado de revision del supervisor,
    /// que significa otra cosa. Un cajero que se va, un cierre de jornada o un escaneo
    /// equivocado la dejaban trabada para siempre.
    ///
    /// ORDEN, y es lo importante:
    ///   1. autorizar (<see cref="RecountingGuard.EvaluateBagReturn"/>)
    ///   2. si bloquea -> devolver el motivo como DATO, CERO borrados
    ///   3. si autoriza -> borrar y desvincular en la misma ejecucion; si algo falla,
    ///      la plataforma revierte TODO
    ///
    /// La autorizacion va primero a proposito: borrar antes dejaria que una llamada
    /// invalida destruyera el recuento y recien despues chocara contra el bloqueo.
    ///
    /// EL ORDEN DE BORRADO NO ES ARBITRARIO. Se desvincula ANTES de borrar en las tres
    /// puntas declaradas (DeclaredChecks.MatchedCheck, DeclaredDeposits.
    /// RelatedRecountedDeposit, DeclaredBags.BagID): si se borrara primero el cheque
    /// fisico, el lookup del declarado quedaria apuntando a un registro muerto o la
    /// plataforma rechazaria el Delete. Y las denominaciones van antes que sus
    /// depositos, por la misma razon.
    ///
    /// LO QUE NO SE TOCA, y es deliberado:
    ///   - las SOLICITUDES DE APROBACION de la tula. Son la evidencia de que un
    ///     supervisor autorizo algo, y en un banco eso no se borra. Lo que si hay que
    ///     hacer —del lado de la app— es que el reescaneo deje de reutilizarlas.
    ///   - los ASIENTOS CONTABLES. No hace falta ni mirarlos: se arman al pasar a
    ///     Recontada, Recontada es terminal, y esta operacion solo entra desde
    ///     "Asignada a cajero". Una tula devuelta no puede tener contabilidad.
    ///   - los espejos de conciliacion de la tula (BagDeclarationID, DeclTotal*,
    ///     HasDifference): los recalcula BagReconciliationMirrorPlugin cuando cambia el
    ///     vinculo o el estado. Escribirlos aca seria pelearse con ese plugin.
    ///
    /// Las decisiones viven en <see cref="RecountingGuard"/> (bajo test). Aca solo hay
    /// lecturas y escrituras.
    /// </summary>
    public sealed class ReturnBagForReassignmentPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var bagId = ReadGuid(context, Schema.ReturnBagApi.InBagId);
            var reason = ReadOptionalString(context, Schema.ReturnBagApi.InReason);
            var actor = context.InitiatingUserId;

            tracing.Trace("ReturnBag: tula {0} actor={1} motivo={2}.",
                bagId, actor, string.IsNullOrWhiteSpace(reason) ? "(sin motivo)" : reason);

            // Como SISTEMA: el supervisor no tiene —ni le vamos a dar— Delete sobre
            // depositos, cheques y denominaciones del recuento solo para esto. La
            // autorizacion de QUIEN puede llamar vive en el privilegio de ejecucion de
            // la Custom API, no en los privilegios de tabla.
            var system = factory.CreateOrganizationService(null);

            // ---- 1. La tula ----
            Entity bag;
            try
            {
                bag = system.Retrieve(
                    Schema.Bag.LogicalName, bagId,
                    new ColumnSet(Schema.Bag.BagStatus, Schema.Bag.BagId));
            }
            catch (Exception ex)
            {
                tracing.Trace("ReturnBag: no se pudo leer la tula {0}: {1}", bagId, ex);
                Respond(context, Schema.ReturnBagApi.OutcomeBlocked,
                    "No se pudo leer la tula indicada.");
                return;
            }

            var status = bag.GetAttributeValue<OptionSetValue>(Schema.Bag.BagStatus);
            var numero = bag.GetAttributeValue<string>(Schema.Bag.BagId);

            // ---- 2. AUTORIZAR. Antes de tocar un solo registro. ----
            var verdict = RecountingGuard.EvaluateBagReturn(true, status?.Value);
            if (!verdict.IsAllowed)
            {
                tracing.Trace("ReturnBag: BLOQUEADA la tula {0} (estado {1}): {2}",
                    numero, status?.Value, verdict.Reason);
                Respond(context, Schema.ReturnBagApi.OutcomeBlocked, verdict.Reason);
                return;
            }

            // ---- 3. El efecto. De aca en adelante, todo o nada. ----
            try
            {
                var borrados = Wipe(system, tracing, bagId);

                // La tula, al final: su Update dispara los espejos de conciliacion, y
                // conviene que los vean con los hijos YA borrados.
                system.Update(new Entity(Schema.Bag.LogicalName, bagId)
                {
                    [Schema.Bag.BagStatus] =
                        new OptionSetValue(ReceptionGuard.BagDevueltaParaReasignacion),
                    [Schema.Bag.RecountingSessionLookup] = null,
                    [Schema.Bag.TotalNio] = 0m,
                    [Schema.Bag.TotalUsd] = 0m,
                    [Schema.Bag.TotalEur] = 0m,
                });

                var resumen =
                    "Tula " + numero + " devuelta para reasignacion. Se borraron " +
                    borrados.Denominations + " denominaciones, " +
                    borrados.Checks + " cheques, " +
                    borrados.Deposits + " depositos y " +
                    borrados.SessionDetails + " detalles de sesion; se desvincularon " +
                    borrados.DeclaredUnlinked + " registros declarados.";

                tracing.Trace("ReturnBag: OK. {0}", resumen);
                Respond(context, Schema.ReturnBagApi.OutcomeReturned, resumen);
            }
            catch (Exception ex)
            {
                // Se relanza a proposito: un fallo a mitad del barrido TIENE que
                // revertir la transaccion. Devolverlo como dato dejaria la tula a
                // medio limpiar y la llamada diciendo "todo bien".
                tracing.Trace("ReturnBag: fallo el barrido de la tula {0}: {1}", numero, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo devolver la tula " + numero + ": " + ex.Message +
                    ". No se borro nada.", ex);
            }
        }

        private sealed class WipeCounts
        {
            public int Denominations;
            public int Checks;
            public int Deposits;
            public int SessionDetails;
            public int DeclaredUnlinked;
        }

        /// <summary>
        /// Barre el recuento de la tula. El orden importa: primero se desvinculan las
        /// puntas declaradas que apuntan a lo que vamos a borrar, despues se borra de
        /// hijo a padre.
        /// </summary>
        private static WipeCounts Wipe(IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            var counts = new WipeCounts();

            var deposits = Ids(system, Schema.DepositRecounting.LogicalName,
                Schema.DepositRecounting.Id, Schema.DepositRecounting.BagLookup, bagId);
            var checks = Ids(system, Schema.Check.LogicalName,
                Schema.Check.Id, Schema.Check.BagLookup, bagId);
            tracing.Trace("ReturnBag: {0} depositos y {1} cheques cuelgan de la tula.",
                deposits.Count, checks.Count);

            // -- 3.1 Desvincular el DECLARADO de cada cheque, antes de borrar el cheque.
            foreach (var checkId in checks)
            {
                foreach (var declared in Ids(system, Schema.DeclaredCheck.LogicalName,
                             Schema.DeclaredCheck.Id, Schema.DeclaredCheck.MatchedCheck, checkId))
                {
                    system.Update(new Entity(Schema.DeclaredCheck.LogicalName, declared)
                    {
                        [Schema.DeclaredCheck.MatchedCheck] = null,
                    });
                    counts.DeclaredUnlinked++;
                }
            }

            // -- 3.2 Desvincular el DECLARADO de cada deposito, antes de borrar el deposito.
            foreach (var depositId in deposits)
            {
                foreach (var declared in Ids(system, Schema.DeclaredDeposit.LogicalName,
                             Schema.DeclaredDeposit.Id,
                             Schema.DeclaredDeposit.RelatedRecountedDeposit, depositId))
                {
                    system.Update(new Entity(Schema.DeclaredDeposit.LogicalName, declared)
                    {
                        [Schema.DeclaredDeposit.RelatedRecountedDeposit] = null,
                    });
                    counts.DeclaredUnlinked++;
                }
            }

            // -- 3.3 Desvincular la BOLSA declarada. Es el enlace que hace que el
            //    escaneo la considere "ya tomada": sin esto, la declaracion queda
            //    pegada a una tula que ya no la esta recontando y NADIE mas la puede
            //    usar. Es el mismo hueco que el escaneo mira con IsBlank(BagID).
            foreach (var declaredBag in Ids(system, Schema.DeclaredBag.LogicalName,
                         Schema.DeclaredBag.Id, Schema.DeclaredBag.BagLookup, bagId))
            {
                system.Update(new Entity(Schema.DeclaredBag.LogicalName, declaredBag)
                {
                    [Schema.DeclaredBag.BagLookup] = null,
                });
                counts.DeclaredUnlinked++;
            }

            // -- 3.4 Denominaciones, antes que sus depositos.
            foreach (var depositId in deposits)
            {
                foreach (var detail in Ids(system, Schema.CountedMoneyDetail.LogicalName,
                             Schema.CountedMoneyDetail.Id,
                             Schema.CountedMoneyDetail.DepositLookup, depositId))
                {
                    system.Delete(Schema.CountedMoneyDetail.LogicalName, detail);
                    counts.Denominations++;
                }
            }

            // -- 3.5 Cheques fisicos.
            foreach (var checkId in checks)
            {
                system.Delete(Schema.Check.LogicalName, checkId);
                counts.Checks++;
            }

            // -- 3.6 Depositos.
            foreach (var depositId in deposits)
            {
                system.Delete(Schema.DepositRecounting.LogicalName, depositId);
                counts.Deposits++;
            }

            // -- 3.7 El detalle de sesion: es lo que ata la tula a la sesion del cajero.
            foreach (var detail in Ids(system, Schema.RecountingSessionDetail.LogicalName,
                         Schema.RecountingSessionDetail.Id,
                         Schema.RecountingSessionDetail.BagLookup, bagId))
            {
                system.Delete(Schema.RecountingSessionDetail.LogicalName, detail);
                counts.SessionDetails++;
            }

            return counts;
        }

        /// <summary>PKs de las filas de <paramref name="entity"/> cuyo lookup apunta a <paramref name="value"/>.</summary>
        private static List<Guid> Ids(
            IOrganizationService system, string entity, string idField, string lookupField, Guid value)
        {
            var query = new QueryExpression(entity) { ColumnSet = new ColumnSet(false) };
            query.Criteria.AddCondition(lookupField, ConditionOperator.Equal, value);

            var ids = new List<Guid>();
            foreach (var row in system.RetrieveMultiple(query).Entities)
            {
                ids.Add(row.Id);
            }

            return ids;
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
            context.OutputParameters[Schema.ReturnBagApi.OutOutcome] = outcome;
            context.OutputParameters[Schema.ReturnBagApi.OutMessage] = message ?? string.Empty;
        }
    }
}
