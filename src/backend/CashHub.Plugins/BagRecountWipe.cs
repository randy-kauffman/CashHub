using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Barrido del recuento de UNA tula: borra lo que el cajero cargo y desvincula lo
    /// declarado, dejando la tula como si nunca hubiera pasado por una mesa.
    ///
    /// VIVE APARTE PORQUE TIENE DOS LLAMADORES, y son dos formas de lo mismo:
    ///   ReturnBagForReassignmentPlugin        el supervisor devuelve UNA tula
    ///   ForceCloseRecountingSessionPlugin     el cierre forzado libera TODAS las de
    ///                                         la sesion
    /// Duplicarlo seria tener dos verdades sobre que significa "vaciar una tula", y la
    /// segunda se iba a quedar vieja el dia que la primera cambie.
    ///
    /// EL ORDEN NO ES ARBITRARIO. Se desvincula ANTES de borrar en las tres puntas
    /// declaradas: borrar primero el cheque fisico dejaria el lookup del declarado
    /// apuntando a un registro muerto. Y las denominaciones van antes que sus depositos.
    ///
    /// NO toca el estado de la tula ni su sesion: eso es decision de cada llamador.
    /// Tampoco toca las solicitudes de aprobacion —son evidencia de una decision de un
    /// supervisor— ni los asientos contables, que no pueden existir porque las dos
    /// operaciones solo entran desde "Asignada a cajero" y los asientos se arman al
    /// pasar a Recontada.
    /// </summary>
    internal static class BagRecountWipe
    {
        internal sealed class Counts
        {
            public int Denominations;
            public int Checks;
            public int Deposits;
            public int SessionDetails;
            public int DeclaredUnlinked;

            public string Describe()
            {
                return "Se borraron " + Denominations + " denominaciones, " +
                       Checks + " cheques, " + Deposits + " depositos y " +
                       SessionDetails + " detalles de sesion; se desvincularon " +
                       DeclaredUnlinked + " registros declarados.";
            }

            public void Add(Counts other)
            {
                Denominations += other.Denominations;
                Checks += other.Checks;
                Deposits += other.Deposits;
                SessionDetails += other.SessionDetails;
                DeclaredUnlinked += other.DeclaredUnlinked;
            }
        }

        internal static Counts Run(IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            var counts = new Counts();

            var deposits = Ids(system, Schema.DepositRecounting.LogicalName,
                Schema.DepositRecounting.BagLookup, bagId);
            var checks = Ids(system, Schema.Check.LogicalName, Schema.Check.BagLookup, bagId);
            tracing.Trace("BagRecountWipe: tula {0} — {1} depositos y {2} cheques.",
                bagId, deposits.Count, checks.Count);

            // -- 1. Desvincular el DECLARADO de cada cheque, antes de borrar el cheque.
            foreach (var checkId in checks)
            {
                foreach (var declared in Ids(system, Schema.DeclaredCheck.LogicalName,
                             Schema.DeclaredCheck.MatchedCheck, checkId))
                {
                    system.Update(new Entity(Schema.DeclaredCheck.LogicalName, declared)
                    {
                        [Schema.DeclaredCheck.MatchedCheck] = null,
                    });
                    counts.DeclaredUnlinked++;
                }
            }

            // -- 2. Desvincular el DECLARADO de cada deposito.
            foreach (var depositId in deposits)
            {
                foreach (var declared in Ids(system, Schema.DeclaredDeposit.LogicalName,
                             Schema.DeclaredDeposit.RelatedRecountedDeposit, depositId))
                {
                    system.Update(new Entity(Schema.DeclaredDeposit.LogicalName, declared)
                    {
                        [Schema.DeclaredDeposit.RelatedRecountedDeposit] = null,
                    });
                    counts.DeclaredUnlinked++;
                }
            }

            // -- 3. Desvincular la BOLSA declarada. Es el enlace que hace que el escaneo
            //    la considere "ya tomada": sin esto la declaracion queda pegada a una
            //    tula que ya no la esta recontando y NADIE mas la puede usar.
            foreach (var declaredBag in Ids(system, Schema.DeclaredBag.LogicalName,
                         Schema.DeclaredBag.BagLookup, bagId))
            {
                system.Update(new Entity(Schema.DeclaredBag.LogicalName, declaredBag)
                {
                    [Schema.DeclaredBag.BagLookup] = null,
                });
                counts.DeclaredUnlinked++;
            }

            // -- 4. Denominaciones, antes que sus depositos.
            foreach (var depositId in deposits)
            {
                foreach (var detail in Ids(system, Schema.CountedMoneyDetail.LogicalName,
                             Schema.CountedMoneyDetail.DepositLookup, depositId))
                {
                    system.Delete(Schema.CountedMoneyDetail.LogicalName, detail);
                    counts.Denominations++;
                }
            }

            // -- 5. Cheques fisicos.
            foreach (var checkId in checks)
            {
                system.Delete(Schema.Check.LogicalName, checkId);
                counts.Checks++;
            }

            // -- 6. Depositos.
            foreach (var depositId in deposits)
            {
                system.Delete(Schema.DepositRecounting.LogicalName, depositId);
                counts.Deposits++;
            }

            // -- 7. El detalle de sesion: es lo que ata la tula a la sesion del cajero.
            foreach (var detail in Ids(system, Schema.RecountingSessionDetail.LogicalName,
                         Schema.RecountingSessionDetail.BagLookup, bagId))
            {
                system.Delete(Schema.RecountingSessionDetail.LogicalName, detail);
                counts.SessionDetails++;
            }

            return counts;
        }

        /// <summary>PKs de las filas de <paramref name="entity"/> cuyo lookup apunta a <paramref name="value"/>.</summary>
        internal static List<Guid> Ids(
            IOrganizationService system, string entity, string lookupField, Guid value)
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
    }
}
