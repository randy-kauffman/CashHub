using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Ruteo server-side de transacciones (CH-Tbl-Transactions) a proceso Batch o Manual.
    ///
    /// La regla vivia duplicada en 8 sitios Power Fx y ninguno aplicaba "sin cuenta ->
    /// Manual". Corre sincrono en Pre-operation (20) de Create y Update: re-resuelve el
    /// tipo de proceso con la regla del dominio (TransactionRouting) y lo estampa en el
    /// Target antes de que toque la base — sin update extra ni condicion de carrera.
    /// En Update usa la pre-image "PreImage" como fallback de lo que no venga en el
    /// request. La decision vive en el dominio puro; aca solo se juntan los insumos.
    /// </summary>
    public sealed class TransactionRoutingPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("TransactionRouting: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                Entity preImage = null;
                if (string.Equals(context.MessageName, "Update", StringComparison.OrdinalIgnoreCase))
                {
                    context.PreEntityImages.TryGetValue(Schema.Transaction.PreImageName, out preImage);
                }

                // Insumos efectivos: lo que escribe este request, con la pre-image como
                // fallback (en Create no hay pre-image y todo sale del Target).
                var requestedType = ReadOptionSet(target, preImage, Schema.Transaction.ProcessingType);
                var accountNumber = ReadDecimal(target, preImage, Schema.Transaction.AccountNumber);
                var txBac = ReadDecimal(target, preImage, Schema.Transaction.BacCheckAmount);
                var txOther = ReadDecimal(target, preImage, Schema.Transaction.OtherCheckAmount);
                var depositRef = ReadReference(target, preImage, Schema.Transaction.RelatedDepositRecounting);

                decimal? depBac = null;
                decimal? depOther = null;
                if (depositRef != null)
                {
                    // Los montos del deposito son insumo de infraestructura del ruteo:
                    // se leen como sistema, no como el llamante.
                    var service = serviceFactory.CreateOrganizationService(null);
                    var deposit = service.Retrieve(
                        Schema.DepositRecounting.LogicalName, depositRef.Id,
                        new ColumnSet(Schema.DepositRecounting.BacCheckAmount, Schema.DepositRecounting.OtherCheckAmount));
                    depBac = deposit.GetAttributeValue<decimal?>(Schema.DepositRecounting.BacCheckAmount);
                    depOther = deposit.GetAttributeValue<decimal?>(Schema.DepositRecounting.OtherCheckAmount);
                }

                var hasChecks = TransactionRouting.HasChecks(txBac, txOther, depBac, depOther);
                var hasAccountNumber = accountNumber.HasValue;
                var resolved = TransactionRouting.ResolveProcessingType(requestedType, hasChecks, hasAccountNumber);

                tracing.Trace(
                    "TransactionRouting: {0} pedido={1} cheques={2} cuenta={3} -> resuelto={4}.",
                    context.MessageName,
                    requestedType.HasValue ? requestedType.Value.ToString() : "null",
                    hasChecks, hasAccountNumber, resolved);

                if (resolved != requestedType)
                {
                    // Se estampa SOLO cuando difiere: un Target sin cambios reales no
                    // dispara escrituras (ni re-entradas de pasos filtrados) de mas.
                    target[Schema.Transaction.ProcessingType] = new OptionSetValue(resolved);
                    tracing.Trace("TransactionRouting: tipo de proceso estampado en el Target.");
                }
                else
                {
                    tracing.Trace("TransactionRouting: el tipo pedido ya es el correcto, sin cambios.");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("TransactionRouting: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo resolver el tipo de proceso de la transaccion: {ex.Message}", ex);
            }
        }

        /// <summary>Valor efectivo de un option set: Target primero, pre-image como fallback.</summary>
        private static int? ReadOptionSet(Entity target, Entity preImage, string attribute)
        {
            if (target.Contains(attribute))
            {
                return target.GetAttributeValue<OptionSetValue>(attribute)?.Value;
            }

            return preImage != null && preImage.Contains(attribute)
                ? preImage.GetAttributeValue<OptionSetValue>(attribute)?.Value
                : null;
        }

        /// <summary>Valor efectivo de un decimal: Target primero, pre-image como fallback.</summary>
        private static decimal? ReadDecimal(Entity target, Entity preImage, string attribute)
        {
            if (target.Contains(attribute))
            {
                return target.GetAttributeValue<decimal?>(attribute);
            }

            return preImage != null && preImage.Contains(attribute)
                ? preImage.GetAttributeValue<decimal?>(attribute)
                : null;
        }

        /// <summary>Valor efectivo de un lookup: Target primero, pre-image como fallback.</summary>
        private static EntityReference ReadReference(Entity target, Entity preImage, string attribute)
        {
            if (target.Contains(attribute))
            {
                return target.GetAttributeValue<EntityReference>(attribute);
            }

            return preImage != null && preImage.Contains(attribute)
                ? preImage.GetAttributeValue<EntityReference>(attribute)
                : null;
        }
    }
}
