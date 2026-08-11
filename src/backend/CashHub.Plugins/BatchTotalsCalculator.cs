using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Recompute de los totales materializados por moneda de un lote de transacciones
    /// (sanic_totalnio / sanic_totalusd / sanic_totaleur).
    ///
    /// UNA sola query FetchXML aggregate server-side: sum(sanic_cashamount) agrupado
    /// por sanic_currency, filtrada por lote. Sumar client-side en la app (o iterar
    /// filas aca) topa con el cap de filas y miente en silencio. Las monedas sin
    /// filas quedan en 0, no en null: un total en blanco en la app parece un dato
    /// faltante, no un cero. Compartido por CreateTransactionCutPlugin (recompute al
    /// cerrar el corte) y TransactionBatchTotalsPlugin (recompute al (des)asignar).
    /// La escritura pasa el guard por la regla touchesOnlyTotals.
    /// </summary>
    internal static class BatchTotalsCalculator
    {
        /// <summary>Recalcula y escribe los totales del lote. <paramref name="service"/> debe correr como sistema.</summary>
        internal static void Recompute(IOrganizationService service, ITracingService tracing, Guid batchId)
        {
            // Aggregate con groupby por option set: alias obligatorio en ambos
            // atributos; dategrouping no aplica (eso es solo para datetime).
            var fetch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Transaction.LogicalName}'>
    <attribute name='{Schema.Transaction.CashAmount}' alias='total' aggregate='sum' />
    <attribute name='{Schema.Transaction.Currency}' alias='currency' groupby='true' />
    <filter>
      <condition attribute='{Schema.Transaction.TransactionBatch}' operator='eq' value='{batchId}' />
    </filter>
  </entity>
</fetch>";

            var groups = service.RetrieveMultiple(new FetchExpression(fetch));

            decimal nio = 0m, usd = 0m, eur = 0m;
            foreach (var row in groups.Entities)
            {
                var currency = (row.GetAttributeValue<AliasedValue>("currency")?.Value as OptionSetValue)?.Value;
                var total = row.GetAttributeValue<AliasedValue>("total")?.Value as decimal? ?? 0m;

                switch (currency)
                {
                    case Schema.Transaction.CurrencyNio:
                        nio = total;
                        break;
                    case Schema.Transaction.CurrencyUsd:
                        usd = total;
                        break;
                    case Schema.Transaction.CurrencyEur:
                        eur = total;
                        break;
                    default:
                        tracing.Trace("BatchTotals: lote {0}, grupo de moneda {1} fuera de catalogo, se ignora.",
                            batchId, currency.HasValue ? currency.Value.ToString() : "null");
                        break;
                }
            }

            service.Update(new Entity(Schema.TransactionBatch.LogicalName, batchId)
            {
                [Schema.TransactionBatch.TotalNio] = nio,
                [Schema.TransactionBatch.TotalUsd] = usd,
                [Schema.TransactionBatch.TotalEur] = eur
            });

            tracing.Trace("BatchTotals: lote {0} -> NIO={1} USD={2} EUR={3}.", batchId, nio, usd, eur);
        }
    }
}
