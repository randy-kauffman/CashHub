using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Mantiene sanic_batchstatus de las planillas como espejo del estado de su lote.
    ///
    /// Las apps filtran planillas por estado de lote; un Filter sobre columna de la
    /// entidad relacionada (ReceptionBatch.ReceptionStatus) no delega a Dataverse y a
    /// partir de 500/2000 filas deja registros afuera SIN error. Con el espejo en la
    /// propia planilla el filtro es delegable. Tres pasos:
    ///   - Post-operation sync en Update del lote (filtrado en sanic_receptionstatus):
    ///     propaga el estado nuevo a todas sus planillas.
    ///   - Pre-operation en Create de planilla: estampa el estado del lote destino.
    ///   - Pre-operation en Update de planilla (filtrado en sanic_receptionbatch):
    ///     re-estampa al re-colgar de otro lote.
    /// Las escrituras del espejo van como sistema (infraestructura, no datos de negocio)
    /// y el GuardReceptionPlugin las deja pasar aunque el lote este congelado.
    /// </summary>
    public sealed class ReceptionBatchStatusMirrorPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("ReceptionMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                if (context.PrimaryEntityName == Schema.ReceptionBatch.LogicalName)
                {
                    CascadeBatchStatus(serviceFactory, tracing, target);
                }
                else if (context.PrimaryEntityName == Schema.Reception.LogicalName)
                {
                    StampReception(serviceFactory, tracing, context.MessageName, target);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("ReceptionMirror: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo propagar el estado del lote a sus planillas: {ex.Message}", ex);
            }
        }

        /// <summary>Propaga el estado nuevo del lote a todas sus planillas (paginado).</summary>
        private static void CascadeBatchStatus(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Entity batchTarget)
        {
            var newStatus = batchTarget.GetAttributeValue<OptionSetValue>(Schema.ReceptionBatch.ReceptionStatus)?.Value;
            if (newStatus == null)
            {
                tracing.Trace("ReceptionMirror: Update de lote sin estado en el Target, se omite.");
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);
            var query = new QueryExpression(Schema.Reception.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = 500 }
            };
            query.Criteria.AddCondition(
                Schema.Reception.BatchLookup, ConditionOperator.Equal, batchTarget.Id);

            var updated = 0;
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    var mirror = new Entity(Schema.Reception.LogicalName, row.Id);
                    mirror[Schema.Reception.BatchStatusMirror] = newStatus.Value;
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

            tracing.Trace("ReceptionMirror: lote {0} -> estado {1} propagado a {2} planillas.",
                batchTarget.Id, newStatus.Value, updated);
        }

        /// <summary>Estampa el espejo en la planilla al crearla o re-colgarla de otro lote.</summary>
        private static void StampReception(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message,
            Entity target)
        {
            var isCreate = string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase);
            if (!target.Contains(Schema.Reception.BatchLookup))
            {
                if (isCreate)
                {
                    tracing.Trace("ReceptionMirror: planilla creada sin lote, sin espejo que estampar.");
                }

                return;
            }

            var batchRef = target.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
            if (batchRef == null)
            {
                target[Schema.Reception.BatchStatusMirror] = null;
                tracing.Trace("ReceptionMirror: planilla desvinculada de lote, espejo en blanco.");
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);
            var batch = service.Retrieve(
                Schema.ReceptionBatch.LogicalName, batchRef.Id,
                new ColumnSet(Schema.ReceptionBatch.ReceptionStatus));
            var status = batch.GetAttributeValue<OptionSetValue>(Schema.ReceptionBatch.ReceptionStatus)?.Value;

            if (status.HasValue)
            {
                target[Schema.Reception.BatchStatusMirror] = status.Value;
            }

            tracing.Trace("ReceptionMirror: planilla {0} ({1}) estampada con estado {2} del lote {3}.",
                target.Id, message, status.HasValue ? status.Value.ToString() : "null", batchRef.Id);
        }
    }
}
