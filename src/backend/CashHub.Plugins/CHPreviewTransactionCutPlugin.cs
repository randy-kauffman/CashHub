using System;
using System.Globalization;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHPreviewTransactionCut: dry-run del corte. Cuenta
    /// las transacciones elegibles de un cajero y suma sus totales por moneda, SIN crear
    /// ni asignar nada, con la MISMA query que CreateTransactionCutPlugin (Batch, del
    /// cajero, sin lote, pendientes, hasta el instante). El diálogo de confirmación de
    /// scrCorte lo usa para que el número que el operador confirma y el que el corte
    /// ejecuta salgan de la misma fuente (mata D-2: el CountRows client-side con dot-walk
    /// mentía el conteo del corte).
    /// </summary>
    public sealed class CHPreviewTransactionCutPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.PreviewCutApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("PreviewCut: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);

            try
            {
                if (!(context.InputParameters.TryGetValue(Schema.PreviewCutApi.InCajeroId, out var rawCajero)
                      && rawCajero is Guid cajeroId
                      && cajeroId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "El conteo del corte requiere el cajero: 'CajeroId' (systemuserid) es obligatorio, " +
                        "de tipo Guid y no vacio.");
                }

                // Mismo instante-corte y mismo criterio que CreateTransactionCutPlugin.
                var cutTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

                // Un aggregate: count(*) + sum(cashamount) agrupado por moneda, con el
                // filtro de elegibilidad del corte. El count total es la suma de los
                // count por grupo de moneda.
                var fetch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Transaction.LogicalName}'>
    <attribute name='{Schema.Transaction.Id}' alias='cnt' aggregate='count' />
    <attribute name='{Schema.Transaction.CashAmount}' alias='total' aggregate='sum' />
    <attribute name='{Schema.Transaction.Currency}' alias='currency' groupby='true' />
    <filter type='and'>
      <condition attribute='{Schema.Transaction.TransactionBatch}' operator='null' />
      <condition attribute='{Schema.Transaction.TransactionStatus}' operator='null' />
      <condition attribute='{Schema.Transaction.ProcessingType}' operator='eq' value='{TransactionRouting.ProcessingBatch}' />
      <condition attribute='{Schema.Transaction.CreatedBy}' operator='eq' value='{cajeroId}' />
      <condition attribute='{Schema.Transaction.CreatedOn}' operator='on-or-before' value='{cutTime}' />
    </filter>
  </entity>
</fetch>";

                var groups = service.RetrieveMultiple(new FetchExpression(fetch));

                var eligible = 0;
                decimal nio = 0m, usd = 0m, eur = 0m;
                foreach (var row in groups.Entities)
                {
                    var count = row.GetAttributeValue<AliasedValue>("cnt")?.Value as int? ?? 0;
                    var total = row.GetAttributeValue<AliasedValue>("total")?.Value as decimal? ?? 0m;
                    var currency = (row.GetAttributeValue<AliasedValue>("currency")?.Value as OptionSetValue)?.Value;

                    eligible += count;
                    switch (currency)
                    {
                        case Schema.Transaction.CurrencyNio: nio = total; break;
                        case Schema.Transaction.CurrencyUsd: usd = total; break;
                        case Schema.Transaction.CurrencyEur: eur = total; break;
                    }
                }

                context.OutputParameters[Schema.PreviewCutApi.OutEligibleCount] = eligible;
                context.OutputParameters[Schema.PreviewCutApi.OutTotalNio] = nio;
                context.OutputParameters[Schema.PreviewCutApi.OutTotalUsd] = usd;
                context.OutputParameters[Schema.PreviewCutApi.OutTotalEur] = eur;

                tracing.Trace("PreviewCut: cajero {0} -> {1} elegibles (NIO={2} USD={3} EUR={4}).",
                    cajeroId, eligible, nio, usd, eur);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("PreviewCut: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo calcular el conteo del corte: {ex.Message}", ex);
            }
        }
    }
}
