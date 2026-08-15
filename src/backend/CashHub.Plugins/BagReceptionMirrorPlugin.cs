using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejos de la tula hacia su planilla (CH-App-Supervisor v2), en UN solo tipo:
    ///
    ///   B-2 — sanic_receptionbatchid (lookup al lote) en sanic_chtblbags,
    ///         derivado de sanic_receptionid.sanic_receptionbatch.
    ///   B-3 — sanic_receiptidmirror (texto) en sanic_chtblbags,
    ///         derivado de sanic_receptionid.sanic_receiptid.
    ///
    /// Ambos salen de la MISMA planilla: se resuelven con UNA sola lectura y se
    /// escriben EN el Target, sin updates extra. Tres pasos sincronos:
    ///   - Pre-operation (20) Create de sanic_chtblbags: estampa los dos espejos.
    ///   - Pre-operation (20) Update de sanic_chtblbags (filtrado en
    ///     sanic_receptionid): re-estampa al re-colgar la tula de otra planilla.
    ///   - Post-operation (40) Update de sanic_chtblreceptions (filtrado en
    ///     sanic_receptionbatch + sanic_receiptid, con PreImage de esas dos):
    ///     CASCADA a todas las tulas de la planilla, paginada de a 500.
    ///
    /// Mismo racional que ReceptionBatchStatusMirrorPlugin: un Filter sobre columna
    /// de entidad relacionada NO delega y a partir del cap de filas deja registros
    /// afuera SIN error; aca son DOS saltos (tula -> planilla -> lote), peor todavia.
    /// La cascada va Post-operation para propagar solo valores ya comprometidos.
    /// Las escrituras del espejo van como sistema: son metadata derivada, no datos
    /// de negocio, y tocan SOLO columnas espejo (por eso ningun guard las frena).
    /// La decision de cuando y que propagar vive en el dominio puro
    /// (BagReceptionMirrors).
    /// </summary>
    public sealed class BagReceptionMirrorPlugin : IPlugin
    {
        /// <summary>Tamano de pagina de la cascada (mismo que el espejo de estado).</summary>
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("BagReceptionMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("BagReceptionMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                var system = serviceFactory.CreateOrganizationService(null);

                if (context.PrimaryEntityName == Schema.Bag.LogicalName)
                {
                    StampBag(system, tracing, context.MessageName, target);
                }
                else if (context.PrimaryEntityName == Schema.Reception.LogicalName)
                {
                    CascadeToBags(system, tracing, context, target);
                }
                else
                {
                    tracing.Trace("BagReceptionMirror: entidad {0} fuera de alcance, se omite.",
                        context.PrimaryEntityName);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BagReceptionMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudieron mantener los espejos de lote y recibo de la tula: "
                    + ex.Message, ex);
            }
        }

        /// <summary>
        /// B-2 + B-3 desde el lado de la tula: resuelve la planilla y escribe los dos
        /// espejos EN el Target (Pre-operation, cero escrituras adicionales).
        /// </summary>
        private static void StampBag(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var receptionTouched = target.Contains(Schema.Bag.ReceptionLookup);

            if (!BagReceptionMirrors.ShouldStamp(message, receptionTouched))
            {
                tracing.Trace("BagReceptionMirror: Update de tula sin cambio de planilla, se omite.");
                return;
            }

            if (!receptionTouched)
            {
                // Create sin planilla: no hay de donde derivar; el espejo nace vacio solo.
                tracing.Trace("BagReceptionMirror: tula creada sin planilla, sin espejos que estampar.");
                return;
            }

            var receptionRef = target.GetAttributeValue<EntityReference>(Schema.Bag.ReceptionLookup);
            if (receptionRef == null)
            {
                target[SupervisorMirrors.Bag.ReceptionBatchMirror] = null;
                target[SupervisorMirrors.Bag.ReceiptIdMirror] = null;
                tracing.Trace("BagReceptionMirror: tula desvinculada de planilla, espejos en blanco.");
                return;
            }

            // Lectura como sistema: el que mueve la tula (cajero) no necesariamente
            // lee la planilla ni el lote. Es una derivacion, no un permiso de negocio.
            var reception = system.Retrieve(
                Schema.Reception.LogicalName, receptionRef.Id,
                new ColumnSet(Schema.Reception.BatchLookup, SupervisorMirrors.Reception.ReceiptId));

            var batchRef = reception.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
            var receiptId = BagReceptionMirrors.CapReceipt(
                reception.GetAttributeValue<string>(SupervisorMirrors.Reception.ReceiptId));

            target[SupervisorMirrors.Bag.ReceptionBatchMirror] = batchRef;
            target[SupervisorMirrors.Bag.ReceiptIdMirror] = receiptId;

            tracing.Trace(
                "BagReceptionMirror: tula {0} ({1}) -> planilla {2}, lote {3}, recibo {4}.",
                target.Id, message, receptionRef.Id,
                batchRef != null ? batchRef.Id.ToString() : "null",
                receiptId ?? "null");
        }

        /// <summary>
        /// Cascada desde la planilla: si cambio el lote o el recibo, reescribe el
        /// espejo correspondiente en TODAS sus tulas, paginado. Solo se propagan las
        /// columnas que cambiaron de verdad (la PreImage es lo que lo permite).
        /// </summary>
        private static void CascadeToBags(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            Entity pre = null;
            context.PreEntityImages.TryGetValue(SupervisorMirrors.Reception.MirrorPreImageName, out pre);
            if (pre == null)
            {
                tracing.Trace(
                    "BagReceptionMirror: falta la PreImage '{0}' del paso de cascada — "
                    + "no se puede saber si el valor cambio, se omite.",
                    SupervisorMirrors.Reception.MirrorPreImageName);
                return;
            }

            var batchTouched = target.Contains(Schema.Reception.BatchLookup);
            var receiptTouched = target.Contains(SupervisorMirrors.Reception.ReceiptId);

            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched,
                newBatchId: target.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup)?.Id,
                previousBatchId: pre.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup)?.Id,
                receiptTouched,
                newReceiptId: target.GetAttributeValue<string>(SupervisorMirrors.Reception.ReceiptId),
                previousReceiptId: pre.GetAttributeValue<string>(SupervisorMirrors.Reception.ReceiptId));

            if (!plan.Any)
            {
                tracing.Trace(
                    "BagReceptionMirror: Update de planilla sin cambio real de lote ni recibo "
                    + "(lote-tocado={0}, recibo-tocado={1}), no se toca ninguna tula.",
                    batchTouched, receiptTouched);
                return;
            }

            var receptionId = target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId;

            var batchValue = plan.BatchId.HasValue
                ? new EntityReference(Schema.ReceptionBatch.LogicalName, plan.BatchId.Value)
                : null;

            var query = new QueryExpression(Schema.Bag.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize }
            };
            query.Criteria.AddCondition(
                Schema.Bag.ReceptionLookup, ConditionOperator.Equal, receptionId);

            var updated = 0;
            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    var mirror = new Entity(Schema.Bag.LogicalName, row.Id);
                    if (plan.PropagateBatch)
                    {
                        mirror[SupervisorMirrors.Bag.ReceptionBatchMirror] = batchValue;
                    }

                    if (plan.PropagateReceipt)
                    {
                        mirror[SupervisorMirrors.Bag.ReceiptIdMirror] = plan.ReceiptId;
                    }

                    system.Update(mirror);
                    updated++;
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace(
                "BagReceptionMirror: planilla {0} -> lote {1} (propaga={2}), recibo {3} (propaga={4}); "
                + "{5} tulas actualizadas.",
                receptionId,
                plan.BatchId.HasValue ? plan.BatchId.Value.ToString() : "null", plan.PropagateBatch,
                plan.ReceiptId ?? "null", plan.PropagateReceipt,
                updated);
        }
    }
}
