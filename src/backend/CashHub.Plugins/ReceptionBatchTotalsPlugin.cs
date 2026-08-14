using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Mantiene los totales DECLARADOS materializados del lote de recepcion
    /// (sanic_decniocash/check/total y sanic_decusdcash/check/total) sumando los
    /// montos declarados de sus planillas.
    ///
    /// El tracker muestra "montos del lote" en el header y en la lista de
    /// recientes; sumar planillas client-side topa con el cap de filas y miente en
    /// silencio (mismo racional que BatchTotalsCalculator para lotes TB).
    ///
    /// Post-operation (40) SINCRONO en:
    ///   - Create de sanic_chtblreceptions
    ///   - Update de sanic_chtblreceptions (filtrado en los 4 montos declarados +
    ///     sanic_receptionbatch), con PreImage (sanic_receptionbatch): mover una
    ///     planilla de lote recomputa AMBOS lotes.
    ///   - Delete de sanic_chtblreceptions, con PreImage.
    /// La decision de que lotes recomputar vive en el dominio puro
    /// (ReceptionBatchTotalsTrigger); las escrituras van como sistema.
    /// </summary>
    public sealed class ReceptionBatchTotalsPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                Entity target = null;
                if (context.InputParameters.TryGetValue("Target", out var raw))
                {
                    target = raw as Entity; // en Delete el Target es EntityReference
                }

                var amountsTouched = target != null &&
                    (target.Contains(Schema.Reception.NioCash)
                     || target.Contains(Schema.Reception.NioCheck)
                     || target.Contains(Schema.Reception.UsdCash)
                     || target.Contains(Schema.Reception.UsdCheck));
                var batchTouched = target != null && target.Contains(Schema.Reception.BatchLookup);
                var targetBatch = target?.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup)?.Id;

                Guid? preBatch = null;
                if (context.PreEntityImages.TryGetValue(Schema.Reception.PreImageName, out var pre))
                {
                    preBatch = pre.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup)?.Id;
                }

                var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                    context.MessageName, amountsTouched, batchTouched, targetBatch, preBatch);

                if (targets.Count == 0)
                {
                    tracing.Trace("ReceptionTotals: {0} sin lote afectado, se omite.", context.MessageName);
                    return;
                }

                // Totales = metadata derivada: se escriben como sistema.
                var service = serviceFactory.CreateOrganizationService(null);
                foreach (var batchId in targets)
                {
                    ReceptionTotalsCalculator.Recompute(service, tracing, batchId);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("ReceptionTotals: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudieron recalcular los totales declarados del lote de recepcion: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Recompute de los totales declarados de un lote de recepcion: UNA query
    /// FetchXML aggregate server-side (4 sums filtrados por lote, sin groupby: las
    /// planillas no tienen moneda como option set, traen columnas por moneda).
    /// Planillas sin filas -> 0, no null (un total en blanco parece dato faltante).
    /// </summary>
    internal static class ReceptionTotalsCalculator
    {
        internal static void Recompute(IOrganizationService service, ITracingService tracing, Guid batchId)
        {
            var fetch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Reception.LogicalName}'>
    <attribute name='{Schema.Reception.NioCash}' alias='niocash' aggregate='sum' />
    <attribute name='{Schema.Reception.NioCheck}' alias='niocheck' aggregate='sum' />
    <attribute name='{Schema.Reception.UsdCash}' alias='usdcash' aggregate='sum' />
    <attribute name='{Schema.Reception.UsdCheck}' alias='usdcheck' aggregate='sum' />
    <filter>
      <condition attribute='{Schema.Reception.BatchLookup}' operator='eq' value='{batchId}' />
    </filter>
  </entity>
</fetch>";

            var groups = service.RetrieveMultiple(new FetchExpression(fetch));

            decimal nioCash = 0m, nioCheck = 0m, usdCash = 0m, usdCheck = 0m;
            if (groups.Entities.Count > 0)
            {
                var row = groups.Entities[0];
                nioCash = Read(row, "niocash");
                nioCheck = Read(row, "niocheck");
                usdCash = Read(row, "usdcash");
                usdCheck = Read(row, "usdcheck");
            }

            service.Update(new Entity(Schema.ReceptionBatch.LogicalName, batchId)
            {
                [Schema.ReceptionBatch.DecNioCash] = nioCash,
                [Schema.ReceptionBatch.DecNioCheck] = nioCheck,
                [Schema.ReceptionBatch.DecNioTotal] = nioCash + nioCheck,
                [Schema.ReceptionBatch.DecUsdCash] = usdCash,
                [Schema.ReceptionBatch.DecUsdCheck] = usdCheck,
                [Schema.ReceptionBatch.DecUsdTotal] = usdCash + usdCheck
            });

            tracing.Trace("ReceptionTotals: lote {0} -> NIO {1}+{2} / USD {3}+{4}.",
                batchId, nioCash, nioCheck, usdCash, usdCheck);
        }

        private static decimal Read(Entity row, string alias)
        {
            return row.GetAttributeValue<AliasedValue>(alias)?.Value as decimal? ?? 0m;
        }
    }
}
