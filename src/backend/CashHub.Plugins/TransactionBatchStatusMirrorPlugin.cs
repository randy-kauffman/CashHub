using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Mantiene sanic_batchstatus de las transacciones como espejo del estado de su lote.
    ///
    /// La app de digitalizacion filtra transacciones "sin lote" y por estado de lote; un
    /// Filter con IsBlank(lookup) o sobre columna de la entidad relacionada
    /// (TransactionBatch.BatchStatus) no delega a Dataverse y a partir de 500/2000 filas
    /// deja registros afuera SIN error. Con el espejo int en la propia transaccion el
    /// filtro es delegable (BatchStatus = Blank() / por valor). Tres pasos:
    ///   - Post-operation sync en Update del lote (filtrado en sanic_batchstatus):
    ///     propaga el estado nuevo a todas sus transacciones.
    ///   - Pre-operation en Create de transaccion: estampa el estado del lote destino.
    ///   - Pre-operation en Update de transaccion (filtrado en sanic_transactionbatch):
    ///     re-estampa al asignarla a un lote (corte) o desasignarla (espejo en blanco).
    /// Las escrituras del espejo van como sistema (infraestructura, no datos de negocio).
    /// </summary>
    public sealed class TransactionBatchStatusMirrorPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("TransactionMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                if (context.PrimaryEntityName == Schema.TransactionBatch.LogicalName)
                {
                    CascadeBatchStatus(serviceFactory, tracing, target);
                }
                else if (context.PrimaryEntityName == Schema.Transaction.LogicalName)
                {
                    StampTransaction(serviceFactory, tracing, context.MessageName, target);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("TransactionMirror: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo propagar el estado del lote a sus transacciones: {ex.Message}", ex);
            }
        }

        /// <summary>Propaga el estado nuevo del lote a todas sus transacciones (paginado).</summary>
        private static void CascadeBatchStatus(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Entity batchTarget)
        {
            var newStatus = batchTarget.GetAttributeValue<OptionSetValue>(Schema.TransactionBatch.BatchStatus)?.Value;
            if (newStatus == null)
            {
                tracing.Trace("TransactionMirror: Update de lote sin estado en el Target, se omite.");
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);
            var query = new QueryExpression(Schema.Transaction.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = 500 }
            };
            query.Criteria.AddCondition(
                Schema.Transaction.TransactionBatch, ConditionOperator.Equal, batchTarget.Id);

            var updated = 0;
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    var mirror = new Entity(Schema.Transaction.LogicalName, row.Id);
                    mirror[Schema.Transaction.BatchStatusMirror] = newStatus.Value;
                    service.Update(mirror);
                    updated++;
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace("TransactionMirror: lote {0} -> estado {1} propagado a {2} transacciones.",
                batchTarget.Id, newStatus.Value, updated);
        }

        /// <summary>Estampa el espejo al crear la transaccion o al (des)asignarla de lote.</summary>
        private static void StampTransaction(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message,
            Entity target)
        {
            var isCreate = string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase);
            if (!target.Contains(Schema.Transaction.TransactionBatch))
            {
                if (isCreate)
                {
                    tracing.Trace("TransactionMirror: transaccion creada sin lote, sin espejo que estampar.");
                }

                return;
            }

            var batchRef = target.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch);
            if (batchRef == null)
            {
                target[Schema.Transaction.BatchStatusMirror] = null;
                tracing.Trace("TransactionMirror: transaccion desasignada de lote, espejo en blanco.");
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);
            var batch = service.Retrieve(
                Schema.TransactionBatch.LogicalName, batchRef.Id,
                new ColumnSet(Schema.TransactionBatch.BatchStatus));
            var status = batch.GetAttributeValue<OptionSetValue>(Schema.TransactionBatch.BatchStatus)?.Value;

            if (status.HasValue)
            {
                target[Schema.Transaction.BatchStatusMirror] = status.Value;
            }

            tracing.Trace("TransactionMirror: transaccion {0} ({1}) estampada con estado {2} del lote {3}.",
                target.Id, message, status.HasValue ? status.Value.ToString() : "null", batchRef.Id);
        }
    }
}
