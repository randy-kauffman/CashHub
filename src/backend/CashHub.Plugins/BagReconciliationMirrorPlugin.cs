using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Mantiene la conciliacion materializada de la tula: sanic_decltotalnio/usd/eur
    /// (copia de los totales del DeclaredBag vinculado) + sanic_hasdifference.
    ///
    /// La grilla de tulas del tracker pinta "con/sin diferencia": el join
    /// Bag→DeclaredBag en Power Fx no delega, asi que la comparacion se materializa
    /// aca (mismo racional que los espejos sanic_batchstatus).
    ///
    /// Pre-operation (20) SINCRONO en Create y Update de sanic_chtblbags (Update
    /// filtrado en sanic_bagdeclarationid + sanic_totalnio/usd/eur + sanic_bagstatus),
    /// con PreImage de esas mismas columnas: escribe los espejos EN el Target, sin
    /// updates extra. La decision y el calculo viven en el dominio puro
    /// (BagReconciliation).
    /// </summary>
    public sealed class BagReconciliationMirrorPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("BagReconciliation: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                Entity pre = null;
                context.PreEntityImages.TryGetValue(Schema.Bag.PreImageName, out pre);

                var isCreate = string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase);
                if (!isCreate && !BagReconciliation.ShouldRecompute(
                        declarationTouched: target.Contains(Schema.Bag.DeclaredBagLookup),
                        totalsTouched: target.Contains(Schema.Bag.TotalNio)
                            || target.Contains(Schema.Bag.TotalUsd)
                            || target.Contains(Schema.Bag.TotalEur),
                        statusTouched: target.Contains(Schema.Bag.BagStatus)))
                {
                    tracing.Trace("BagReconciliation: Update sin disparadores, se omite.");
                    return;
                }

                // Valor efectivo: lo que viene en el Target manda; si no vino, la PreImage.
                T Effective<T>(string attribute) where T : class
                {
                    if (target.Contains(attribute)) return target.GetAttributeValue<T>(attribute);
                    return pre?.GetAttributeValue<T>(attribute);
                }
                decimal? EffectiveDec(string attribute)
                {
                    if (target.Contains(attribute)) return target.GetAttributeValue<decimal?>(attribute);
                    return pre?.GetAttributeValue<decimal?>(attribute);
                }

                var declaredBagRef = Effective<EntityReference>(Schema.Bag.DeclaredBagLookup);
                var bagStatus = Effective<OptionSetValue>(Schema.Bag.BagStatus)?.Value;

                decimal? declNio = null, declUsd = null, declEur = null;
                if (declaredBagRef != null)
                {
                    // Lectura como sistema: el que actualiza la tula (cajero) no
                    // necesariamente lee DeclaredBags.
                    var system = serviceFactory.CreateOrganizationService(null);
                    var declaredBag = system.Retrieve(
                        Schema.DeclaredBag.LogicalName, declaredBagRef.Id,
                        new ColumnSet(Schema.DeclaredBag.TotalNio, Schema.DeclaredBag.TotalUsd,
                            Schema.DeclaredBag.TotalEur));
                    declNio = declaredBag.GetAttributeValue<decimal?>(Schema.DeclaredBag.TotalNio);
                    declUsd = declaredBag.GetAttributeValue<decimal?>(Schema.DeclaredBag.TotalUsd);
                    declEur = declaredBag.GetAttributeValue<decimal?>(Schema.DeclaredBag.TotalEur);
                }

                var result = BagReconciliation.Compute(
                    bagStatus, hasDeclaration: declaredBagRef != null,
                    declNio: declNio, declUsd: declUsd, declEur: declEur,
                    recNio: EffectiveDec(Schema.Bag.TotalNio),
                    recUsd: EffectiveDec(Schema.Bag.TotalUsd),
                    recEur: EffectiveDec(Schema.Bag.TotalEur));

                target[Schema.Bag.DeclTotalNio] = result.DeclNio;
                target[Schema.Bag.DeclTotalUsd] = result.DeclUsd;
                target[Schema.Bag.DeclTotalEur] = result.DeclEur;
                target[Schema.Bag.HasDifference] = result.HasDifference;

                tracing.Trace("BagReconciliation: tula {0} ({1}) -> decl NIO={2} USD={3} EUR={4}, diferencia={5}.",
                    target.Id, context.MessageName, result.DeclNio, result.DeclUsd, result.DeclEur,
                    result.HasDifference);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BagReconciliation: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo materializar la conciliacion de la tula: {ex.Message}", ex);
            }
        }
    }
}
