using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHRemoveFromCut: saca una transaccion de su lote y
    /// la devuelve al circuito Manual, persistiendo el motivo del rechazo del Core y
    /// recomputando los totales del lote — todo server-side y atomico.
    ///
    /// Reemplaza el Patch('Transaction Batch': Blank()) client-side de scrLotes seguido
    /// de dos Refresh y un LookUp (P-5, W-3): la desasignacion no garantizaba el
    /// recalculo de totales y el LookUp podia leerlos rancios. Aca la desasignacion, el
    /// motivo y el recompute pasan en una operacion, y el API DEVUELVE los totales
    /// nuevos para que la app actualice el espejo local sin releer (cero carrera).
    ///
    /// Se puede sacar del lote solo mientras el lote esta Generado (revisar/quitar antes
    /// de subir) o Ingresado (los rechazos del Core ocurren durante la carga): la regla
    /// vive en TransactionGuard.CanRemoveFromCut, la MISMA que el guard impone en la
    /// desasignacion. Al desasignarse, el mirror plugin devuelve la transaccion a Manual
    /// (regla F3): un rechazo del Core nunca vuelve solo a un corte Batch automatico.
    /// </summary>
    public sealed class CHRemoveFromCutPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.RemoveFromCutApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("RemoveFromCut: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            // Desasignacion + motivo + recompute: corre como sistema, no como el llamante.
            var service = serviceFactory.CreateOrganizationService(null);

            try
            {
                // 0. TransactionId REQUERIDO.
                if (!(context.InputParameters.TryGetValue(Schema.RemoveFromCutApi.InTransactionId, out var rawTx)
                      && rawTx is Guid transactionId
                      && transactionId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "Sacar del lote requiere la transaccion: el parametro 'TransactionId' " +
                        "(sanic_chtbltransactionsid) es obligatorio, de tipo Guid y no puede ser vacio.");
                }

                // Motivo opcional (texto del rechazo del Core).
                context.InputParameters.TryGetValue(Schema.RemoveFromCutApi.InMotivo, out var rawMotivo);
                var motivo = rawMotivo as string;

                // 1. Estado real de la transaccion y de su lote (no confiar en el cliente).
                var tx = service.Retrieve(
                    Schema.Transaction.LogicalName, transactionId,
                    new ColumnSet(Schema.Transaction.TransactionBatch));

                var batchRef = tx.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch);
                if (batchRef == null)
                {
                    throw new InvalidPluginExecutionException(
                        "La transaccion no pertenece a ningun lote: no hay nada que sacar.");
                }

                var batch = service.Retrieve(
                    Schema.TransactionBatch.LogicalName, batchRef.Id,
                    new ColumnSet(Schema.TransactionBatch.BatchStatus));
                var batchStatus = batch.GetAttributeValue<OptionSetValue>(Schema.TransactionBatch.BatchStatus)?.Value;

                // 2. Regla del dominio: solo Generado o Devuelto (la misma que el guard).
                //    El mensaje NOMBRA el estado que estorba: "no se puede" a secas obliga
                //    al operador a adivinar si el problema es el lote, la transaccion o un
                //    permiso.
                if (!TransactionGuard.CanRemoveFromCut(batchStatus))
                {
                    if (batchStatus == TransactionGuard.BatchIngresado)
                    {
                        throw new InvalidPluginExecutionException(
                            "La transaccion no puede sacarse del lote: el lote esta 'Ingresado', o sea " +
                            "en manos del aprobador en el IBS. Si hay algo que corregir, el aprobador " +
                            "tiene que devolverlo primero.");
                    }

                    if (batchStatus == TransactionGuard.BatchAplicado)
                    {
                        throw new InvalidPluginExecutionException(
                            "La transaccion no puede sacarse del lote: el lote ya esta Aplicado en el " +
                            "Core y es intocable. Una correccion contable se hace con un asiento nuevo.");
                    }

                    throw new InvalidPluginExecutionException(
                        "La transaccion no puede sacarse del lote: solo se saca de un lote en estado " +
                        "'Generado' (antes de subirlo al Core) o 'Devuelto' (el IBS lo rechazo y hay " +
                        "que corregirlo).");
                }

                tracing.Trace("RemoveFromCut: tx {0} del lote {1} (estado {2}), actor {3}.",
                    transactionId, batchRef.Id, batchStatus, context.InitiatingUserId);

                // 3. Desasignar + estampar el motivo en UNA operacion. El lookup a null
                //    dispara el mirror plugin (F3 -> Manual) y pasa por el guard, que
                //    valida la desasignacion con la misma CanRemoveFromCut.
                var update = new Entity(Schema.Transaction.LogicalName, transactionId)
                {
                    [Schema.Transaction.TransactionBatch] = null
                };
                if (!string.IsNullOrWhiteSpace(motivo))
                {
                    update[Schema.Transaction.MotivoRechazoCore] = motivo;
                }
                service.Update(update);
                tracing.Trace("RemoveFromCut: tx {0} desasignada del lote {1} y devuelta a Manual.",
                    transactionId, batchRef.Id);

                // 4. Totales materializados del lote: recompute explicito (el plugin de
                //    totales se corta a depth > 1, igual que en el corte). La escritura
                //    pasa el guard por la regla touchesOnlyTotals.
                BatchTotalsCalculator.Recompute(service, tracing, batchRef.Id);

                // 5. Devolver los totales nuevos para que la app actualice sin releer.
                var totals = service.Retrieve(
                    Schema.TransactionBatch.LogicalName, batchRef.Id,
                    new ColumnSet(
                        Schema.TransactionBatch.TotalNio,
                        Schema.TransactionBatch.TotalUsd,
                        Schema.TransactionBatch.TotalEur));

                context.OutputParameters[Schema.RemoveFromCutApi.OutBatchId] = batchRef.Id;
                context.OutputParameters[Schema.RemoveFromCutApi.OutTotalNio] =
                    totals.GetAttributeValue<decimal>(Schema.TransactionBatch.TotalNio);
                context.OutputParameters[Schema.RemoveFromCutApi.OutTotalUsd] =
                    totals.GetAttributeValue<decimal>(Schema.TransactionBatch.TotalUsd);
                context.OutputParameters[Schema.RemoveFromCutApi.OutTotalEur] =
                    totals.GetAttributeValue<decimal>(Schema.TransactionBatch.TotalEur);

                tracing.Trace("RemoveFromCut: completo — lote {0} recalculado.", batchRef.Id);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("RemoveFromCut: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo sacar la transaccion del lote: {ex.Message}", ex);
            }
        }
    }
}
