using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHCreateTransactionCut: corte atomico de
    /// transacciones Batch a un lote nuevo, POR CAJERO.
    ///
    /// Reemplaza el UpdateIf client-side de CH-App-Digitization (cap 2000 filas, no
    /// atomico, y sin excluir transacciones ya Aplicadas: doble aplicacion). El corte
    /// corre server-side y sincrono: crea el lote con estado Generado EXPLICITO (los
    /// defaults de columna de Dataverse no aplican a este Create — el bug de lotes con
    /// estado null), asigna en lotes de 500 todas las transacciones elegibles creadas
    /// hasta el instante del corte, y si no habia ninguna borra el lote para no dejar
    /// lotes vacios (regla de negocio W6).
    ///
    /// El corte es por cajero: CajeroId (systemuserid) es REQUERIDO y solo entran
    /// transacciones creadas por ese cajero (createdby). El lote queda estampado con
    /// sanic_cajero = CajeroId y sanic_generadopor = quien disparo el corte
    /// (InitiatingUserId), base de la trazabilidad y la segregacion de funciones.
    /// </summary>
    public sealed class CreateTransactionCutPlugin : IPlugin
    {
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.CutApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("CreateTransactionCut: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            // El corte escribe lotes y asignaciones: corre como sistema, no como el llamante.
            var service = serviceFactory.CreateOrganizationService(null);

            try
            {
                // 0. CajeroId REQUERIDO: el corte es por cajero, no global.
                if (!(context.InputParameters.TryGetValue(Schema.CutApi.InCajeroId, out var rawCajero)
                      && rawCajero is Guid cajeroId
                      && cajeroId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "El corte requiere el cajero: el parametro 'CajeroId' (systemuserid) es " +
                        "obligatorio, de tipo Guid y no puede ser vacio.");
                }

                var cutTime = DateTime.UtcNow;
                tracing.Trace("CreateTransactionCut: corte del cajero {0} al instante {1:o}, generado por {2}.",
                    cajeroId, cutTime, context.InitiatingUserId);

                // 1. Lote nuevo con estado Generado explicito, estampado con el cajero
                //    del corte y con quien lo genero (InitiatingUserId, no el system
                //    user con el que corre el plugin). El Create corre COMO EL OPERADOR
                //    para que createdby tambien refleje a la persona real; el resto de
                //    las escrituras del corte sigue como sistema.
                var operatorService = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
                var batchId = operatorService.Create(new Entity(Schema.TransactionBatch.LogicalName)
                {
                    [Schema.TransactionBatch.BatchStatus] = new OptionSetValue(TransactionRouting.BatchGenerado),
                    [Schema.TransactionBatch.Cajero] =
                        new EntityReference(Schema.SystemUser.LogicalName, cajeroId),
                    [Schema.TransactionBatch.GeneradoPor] =
                        new EntityReference(Schema.SystemUser.LogicalName, context.InitiatingUserId)
                });
                tracing.Trace("CreateTransactionCut: lote {0} creado en estado Generado (cajero={1}, generadopor={2}).",
                    batchId, cajeroId, context.InitiatingUserId);

                // 1b. La PROPIEDAD del lote es del cajero: la visibilidad futura por
                //     profundidad User (cajeros ven solo lo suyo) filtra por owner.
                //     La reasignacion corre como sistema — el operador no tiene (ni
                //     necesita) privilegio Assign sobre lotes.
                service.Update(new Entity(Schema.TransactionBatch.LogicalName, batchId)
                {
                    ["ownerid"] = new EntityReference(Schema.SystemUser.LogicalName, cajeroId)
                });
                tracing.Trace("CreateTransactionCut: lote {0} asignado en propiedad al cajero {1}.", batchId, cajeroId);

                // 2. Elegibles segun el dominio (TransactionRouting.IsEligibleForCut),
                //    delegado al servidor: Batch, DEL CAJERO (createdby), sin lote,
                //    pendientes (una Aplicada NUNCA entra) y creadas hasta el instante
                //    del corte.
                var query = new QueryExpression(Schema.Transaction.LogicalName)
                {
                    ColumnSet = new ColumnSet(false),
                    PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize }
                };
                query.Criteria.AddCondition(Schema.Transaction.TransactionBatch, ConditionOperator.Null);
                query.Criteria.AddCondition(Schema.Transaction.TransactionStatus, ConditionOperator.Null);
                query.Criteria.AddCondition(
                    Schema.Transaction.ProcessingType, ConditionOperator.Equal, TransactionRouting.ProcessingBatch);
                query.Criteria.AddCondition(Schema.Transaction.CreatedOn, ConditionOperator.LessEqual, cutTime);
                query.Criteria.AddCondition(Schema.Transaction.CreatedBy, ConditionOperator.Equal, cajeroId);

                // 3. Asignacion paginada de a 500. Cada pagina procesada sale del filtro
                //    (el lookup deja de ser Null), asi que siempre se relee la pagina 1:
                //    avanzar PageNumber sobre un resultado que se achica saltearia filas.
                //    Update fila a fila como el mirror plugin: ExecuteMultiple dentro del
                //    pipeline del Custom API faultea los child requests EN SILENCIO (la
                //    asignacion no persistia); el Update directo participa de la
                //    transaccion y ante un fallo revierte el corte completo, lote incluido.
                var assigned = 0;
                while (true)
                {
                    var page = service.RetrieveMultiple(query);
                    if (page.Entities.Count == 0)
                    {
                        break;
                    }

                    foreach (var row in page.Entities)
                    {
                        service.Update(new Entity(Schema.Transaction.LogicalName, row.Id)
                        {
                            [Schema.Transaction.TransactionBatch] =
                                new EntityReference(Schema.TransactionBatch.LogicalName, batchId)
                        });
                    }

                    assigned += page.Entities.Count;
                    tracing.Trace("CreateTransactionCut: {0} transacciones asignadas (acumulado {1}).",
                        page.Entities.Count, assigned);

                    if (!page.MoreRecords)
                    {
                        break;
                    }
                }

                // 4. Sin elegibles no se deja un lote vacio (regla de negocio W6).
                if (assigned == 0)
                {
                    service.Delete(Schema.TransactionBatch.LogicalName, batchId);
                    tracing.Trace("CreateTransactionCut: sin transacciones elegibles del cajero {0}; lote {1} eliminado.",
                        cajeroId, batchId);

                    context.OutputParameters[Schema.CutApi.OutBatchId] = Guid.Empty;
                    context.OutputParameters[Schema.CutApi.OutBatchName] = string.Empty;
                    context.OutputParameters[Schema.CutApi.OutAssignedCount] = 0;
                    return;
                }

                // 5. Totales materializados del lote: UNA query aggregate al final del
                //    corte (TransactionBatchTotalsPlugin se corta a depth > 1 para no
                //    recomputar por cada transaccion asignada). La escritura pasa el
                //    guard por la regla touchesOnlyTotals.
                BatchTotalsCalculator.Recompute(service, tracing, batchId);

                // 6. El nombre (autonumber TB-{SEQNUM:4}) lo asigna Dataverse: se relee.
                var batch = service.Retrieve(
                    Schema.TransactionBatch.LogicalName, batchId,
                    new ColumnSet(Schema.TransactionBatch.Name));
                var batchName = batch.GetAttributeValue<string>(Schema.TransactionBatch.Name) ?? string.Empty;

                context.OutputParameters[Schema.CutApi.OutBatchId] = batchId;
                context.OutputParameters[Schema.CutApi.OutBatchName] = batchName;
                context.OutputParameters[Schema.CutApi.OutAssignedCount] = assigned;
                tracing.Trace("CreateTransactionCut: corte completo — lote {0} '{1}' con {2} transacciones del cajero {3}.",
                    batchId, batchName, assigned, cajeroId);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("CreateTransactionCut: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo ejecutar el corte de transacciones: {ex.Message}", ex);
            }
        }
    }
}
