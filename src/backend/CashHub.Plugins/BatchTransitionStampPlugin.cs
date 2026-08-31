using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;

namespace CashHub.Plugins
{
    /// <summary>
    /// Sella server-side quien y cuando ejecuta cada transicion del lote de
    /// transacciones (CH-Tbl-TransactionBatches).
    ///
    /// Registrado sincrono Pre-operation (stage 20) en Update de
    /// sanic_transactionbatches filtrado por sanic_batchstatus, DESPUES del guard
    /// (Pre-validation, stage 10): cuando corre, la transicion ya es legal. Lee el
    /// estado que escribe el request y estampa EN EL TARGET (sin update extra,
    /// dentro de la misma operacion): Ingresado -> sanic_ingresadopor +
    /// sanic_ingresadoon; Aplicado -> sanic_aplicadopor + sanic_aplicadoon. El
    /// actor es context.InitiatingUserId y la hora la del servidor, asi el sello
    /// no depende del reloj ni de la honestidad del cliente; Devuelto ->
    /// sanic_devueltopor + sanic_devueltoon. Otros estados no estampan nada.
    ///
    /// La estampa de Ingresado es el insumo de la regla de segregacion de
    /// funciones del guard: quien marco Ingresado no puede marcar Aplicado.
    /// </summary>
    public sealed class BatchTransitionStampPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("BatchStamp: {0} de {1}, actor={2}, depth={3}.",
                context.MessageName, context.PrimaryEntityName, context.InitiatingUserId, context.Depth);

            try
            {
                if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
                {
                    throw new InvalidPluginExecutionException(
                        $"BatchStamp: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo Entity.");
                }

                var status = target.GetAttributeValue<OptionSetValue>(Schema.TransactionBatch.BatchStatus)?.Value;

                string userAttribute;
                string dateAttribute;
                switch (status)
                {
                    case TransactionGuard.BatchIngresado:
                        userAttribute = Schema.TransactionBatch.IngresadoPor;
                        dateAttribute = Schema.TransactionBatch.IngresadoOn;
                        break;
                    case TransactionGuard.BatchAplicado:
                        userAttribute = Schema.TransactionBatch.AplicadoPor;
                        dateAttribute = Schema.TransactionBatch.AplicadoOn;
                        break;
                    case TransactionGuard.BatchDevuelto:
                        userAttribute = Schema.TransactionBatch.DevueltoPor;
                        dateAttribute = Schema.TransactionBatch.DevueltoOn;
                        break;
                    default:
                        tracing.Trace("BatchStamp: lote {0}, estado {1} no estampa transicion.",
                            target.Id, status.HasValue ? status.Value.ToString() : "null");
                        return;
                }

                target[userAttribute] = new EntityReference(Schema.SystemUser.LogicalName, context.InitiatingUserId);
                target[dateAttribute] = DateTime.UtcNow;
                tracing.Trace("BatchStamp: lote {0}, estado {1} estampa {2}={3} y {4} en el Target.",
                    target.Id, status, userAttribute, context.InitiatingUserId, dateAttribute);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BatchStamp: error inesperado estampando la transicion del lote: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"BatchStamp no pudo estampar la transicion del lote: {ex.Message}", ex);
            }
        }
    }
}
