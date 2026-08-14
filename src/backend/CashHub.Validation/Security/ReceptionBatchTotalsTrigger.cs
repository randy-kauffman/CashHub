using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Decide QUE lotes de recepcion (CH-Tbl-ReceptionBatch) recomputar cuando una
    /// planilla (CH-Tbl-Receptions) se crea, cambia o se borra.
    ///
    /// Los totales declarados del lote (DecNIO*/DecUSD*) son materializados: el
    /// tracker los muestra sin sumar client-side (cap de filas, misma razon que los
    /// totales de TransactionBatch). El calculo es un aggregate server-side
    /// (ReceptionTotalsCalculator); este dominio solo resuelve los casos borde de
    /// "a quien le toca": mover una planilla de lote debe recomputar AMBOS lotes.
    /// </summary>
    public static class ReceptionBatchTotalsTrigger
    {
        /// <summary>
        /// Lotes a recomputar, sin duplicados, en orden viejo-primero. Vacio cuando
        /// el cambio no afecta totales de ningun lote.
        /// </summary>
        public static IReadOnlyList<Guid> ResolveRecomputeTargets(
            string message, bool amountsTouched, bool batchTouched,
            Guid? targetBatch, Guid? preImageBatch)
        {
            var targets = new List<Guid>(2);

            if (string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase))
            {
                if (targetBatch.HasValue)
                {
                    targets.Add(targetBatch.Value);
                }
                return targets;
            }

            if (string.Equals(message, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                if (preImageBatch.HasValue)
                {
                    targets.Add(preImageBatch.Value);
                }
                return targets;
            }

            // Update
            if (batchTouched)
            {
                if (preImageBatch.HasValue)
                {
                    targets.Add(preImageBatch.Value);
                }
                if (targetBatch.HasValue && targetBatch != preImageBatch)
                {
                    targets.Add(targetBatch.Value);
                }
                return targets;
            }

            if (amountsTouched && preImageBatch.HasValue)
            {
                targets.Add(preImageBatch.Value);
            }

            return targets;
        }
    }
}
