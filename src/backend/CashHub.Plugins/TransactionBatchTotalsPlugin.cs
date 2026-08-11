using System;
using Microsoft.Xrm.Sdk;

namespace CashHub.Plugins
{
    /// <summary>
    /// Mantiene los totales materializados del lote cuando una transaccion cambia de
    /// lote (asignacion o desasignacion individual).
    ///
    /// Post-operation (40) sincrono en Update de sanic_chtbltransactions filtrado en
    /// sanic_transactionbatch, con pre-image "PreImage" (sanic_transactionbatch).
    /// Recomputa via BatchTotalsCalculator el lote viejo (pre-image, si habia) y el
    /// nuevo (target, si hay). A depth &gt; 1 se corta: las asignaciones masivas del
    /// corte (CutPlugin, depth 2) ya recomputan UNA sola vez al final del corte —
    /// recomputar aca seria una query aggregate por transaccion asignada.
    /// </summary>
    public sealed class TransactionBatchTotalsPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.Depth > 1)
            {
                // Updates del corte (u otros plugins): el CutPlugin recomputa una sola vez.
                tracing.Trace("BatchTotals: depth {0}, recompute delegado al proceso padre.", context.Depth);
                return;
            }

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target)
                || !target.Contains(Schema.Transaction.TransactionBatch))
            {
                tracing.Trace("BatchTotals: sin cambio de lote en el Target, se omite.");
                return;
            }

            try
            {
                var newBatchRef = target.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch);

                EntityReference oldBatchRef = null;
                if (context.PreEntityImages.TryGetValue(Schema.Transaction.PreImageName, out var pre))
                {
                    oldBatchRef = pre.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch);
                }

                // Los totales son metadata derivada: se escriben como sistema.
                var service = serviceFactory.CreateOrganizationService(null);

                if (oldBatchRef != null && (newBatchRef == null || oldBatchRef.Id != newBatchRef.Id))
                {
                    BatchTotalsCalculator.Recompute(service, tracing, oldBatchRef.Id);
                }

                if (newBatchRef != null)
                {
                    BatchTotalsCalculator.Recompute(service, tracing, newBatchRef.Id);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BatchTotals: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudieron recalcular los totales del lote: {ex.Message}", ex);
            }
        }
    }
}
