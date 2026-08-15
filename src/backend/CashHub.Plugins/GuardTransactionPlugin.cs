using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Guard del dominio de digitalizacion: lotes de transacciones, transacciones
    /// y el handshake de recepcion de cheques (CH-Tbl-Checks, reglas en CheckGuard:
    /// Recontado -> 'Enviado a digitacion' -> Recibido/'No Recibido', con Recibido
    /// congelando el registro y Digitado/Rebotado reservados).
    ///
    /// La maquina de estados del lote (Generado -> Ingresado -> Aplicado) vive en
    /// Power Fx, asi que cualquier usuario con el rol puede, por Web API o Excel,
    /// retroceder un lote Aplicado, mandarlo al dead-end Aprobado, meter o sacar
    /// transacciones de un lote cerrado o re-aplicar una transaccion Aplicada. Un
    /// unico plugin type registrado en pasos sincronos Pre-validation de
    /// Create/Update de ambas tablas y Delete del lote despacha por entidad y
    /// mensaje; las reglas viven en el dominio puro (TransactionGuard) y aca solo
    /// se juntan los insumos (pre-images con fallback de lectura como sistema) y se
    /// propaga el bloqueo. Las lecturas como sistema son chequeos de seguridad de
    /// solo lectura, independientes de los privilegios del actor.
    ///
    /// NOTA: el CutPlugin corre como sistema a depth 1 y sus escrituras disparan
    /// este guard a depth 2; sus operaciones CUMPLEN las reglas (asigna
    /// transacciones pendientes a un lote Generado), asi que NO hay excepcion por
    /// depth — el guard vale para todos, plugins incluidos.
    /// </summary>
    public sealed class GuardTransactionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var entity = context.PrimaryEntityName;
            var message = context.MessageName;
            tracing.Trace("GuardTransaction: {0} de {1}, actor={2}, depth={3}.",
                message, entity, context.InitiatingUserId, context.Depth);

            TransactionGuardResult result;
            try
            {
                result = Evaluate(context, serviceFactory, tracing, entity, message);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardTransaction: error inesperado evaluando {0} de {1}: {2}", message, entity, ex);
                throw new InvalidPluginExecutionException(
                    $"GuardTransaction no pudo evaluar {message} de {entity}: {ex.Message}", ex);
            }

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardTransaction: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardTransaction: operacion permitida.");
        }

        private static TransactionGuardResult Evaluate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string entity,
            string message)
        {
            switch (entity)
            {
                case Schema.TransactionBatch.LogicalName:
                    return EvaluateBatch(context, serviceFactory, tracing, message);
                case Schema.Transaction.LogicalName:
                    return EvaluateTransaction(context, serviceFactory, tracing, message);
                case Schema.Check.LogicalName:
                    return EvaluateCheck(context, serviceFactory, tracing, message);
                default:
                    tracing.Trace("GuardTransaction: entidad {0} fuera de alcance, se permite.", entity);
                    return TransactionGuardResult.Allow();
            }
        }

        // ---- Lote ----

        private static TransactionGuardResult EvaluateBatch(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message == "Create")
            {
                var target = GetTargetEntity(context);
                return TransactionGuard.EvaluateBatchCreate(
                    ReadStatus(target, Schema.TransactionBatch.BatchStatus));
            }

            if (message == "Delete")
            {
                var batchId = GetTargetReference(context).Id;
                var status = RetrieveBatchStatus(serviceFactory, batchId);
                var hasTransactions = BatchHasTransactions(serviceFactory, batchId);

                tracing.Trace("GuardTransaction: delete de lote {0} estado={1} conTransacciones={2}.",
                    batchId, Fmt(status), hasTransactions);

                return TransactionGuard.EvaluateBatchDelete(status, hasTransactions);
            }

            var update = GetTargetEntity(context);

            // Escrituras del recompute de totales materializados (BatchTotalsCalculator):
            // infraestructura, no negocio. La plataforma agrega el primary key al
            // Target de un Update: se ignora.
            var touchesOnlyTotals = true;
            foreach (var key in update.Attributes.Keys)
            {
                if (key != Schema.TransactionBatch.TotalNio
                    && key != Schema.TransactionBatch.TotalUsd
                    && key != Schema.TransactionBatch.TotalEur
                    && key != Schema.TransactionBatch.Id)
                {
                    touchesOnlyTotals = false;
                    break;
                }
            }

            if (touchesOnlyTotals)
            {
                tracing.Trace("GuardTransaction: lote {0}, escritura solo de totales, se permite.", update.Id);
                return TransactionGuard.EvaluateBatchUpdate(
                    null, false, null, touchesOnlyTotals: true,
                    initiatingUserId: context.InitiatingUserId, ingresadoPor: null);
            }

            var statusInTarget = update.Contains(Schema.TransactionBatch.BatchStatus);
            var newStatus = statusInTarget ? ReadStatus(update, Schema.TransactionBatch.BatchStatus) : null;

            // Este guard corre Pre-validation (10), ANTES del stamp (Pre-op 20): la
            // pre-image trae el sanic_ingresadopor de la transicion ANTERIOR, que es
            // exactamente el insumo de la regla de segregacion (quien marco Ingresado
            // no puede marcar Aplicado).
            var pre = ResolveBatchPreState(context, serviceFactory, update.Id);

            tracing.Trace("GuardTransaction: lote {0} prev={1} nuevo={2} ingresadopor={3} actor={4}.",
                update.Id, Fmt(pre.Status), statusInTarget ? Fmt(newStatus) : "(sin cambio)",
                pre.IngresadoPor.HasValue ? pre.IngresadoPor.Value.ToString() : "null",
                context.InitiatingUserId);

            return TransactionGuard.EvaluateBatchUpdate(
                pre.Status, statusInTarget, newStatus, touchesOnlyTotals: false,
                initiatingUserId: context.InitiatingUserId, ingresadoPor: pre.IngresadoPor);
        }

        // ---- Transaccion ----

        private static TransactionGuardResult EvaluateTransaction(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            var target = GetTargetEntity(context);

            if (message == "Create")
            {
                // Con lote preseteado la asignacion se valida igual que en Update:
                // lote destino Generado y sin nacer ya Aplicada.
                var batchInTarget = target.Contains(Schema.Transaction.TransactionBatch);
                var batchRef = batchInTarget
                    ? target.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch)
                    : null;
                var bornStatus = ReadStatus(target, Schema.Transaction.TransactionStatus);
                var targetBatchStatus = batchRef == null
                    ? null
                    : RetrieveBatchStatus(serviceFactory, batchRef.Id);

                tracing.Trace("GuardTransaction: create de transaccion lote={0} estado-lote={1} status={2}.",
                    batchRef != null ? batchRef.Id.ToString() : "null", Fmt(targetBatchStatus), Fmt(bornStatus));

                return TransactionGuard.EvaluateTransactionUpdate(
                    touchesOnlyMirror: false,
                    statusInTarget: target.Contains(Schema.Transaction.TransactionStatus),
                    newStatus: bornStatus,
                    previousStatus: null,
                    batchInTarget: batchInTarget,
                    newBatchIsNull: batchRef == null,
                    currentBatchStatus: null,
                    targetBatchStatus: targetBatchStatus,
                    hadBatch: false);
            }

            // El espejo sanic_batchstatus lo escriben el mirror plugin y su cascada
            // (metadata derivada, no datos de negocio): si el request no toca nada
            // mas, pasa sin resolver lotes. El primary key del Target se ignora.
            var touchesOnlyMirror = true;
            foreach (var key in target.Attributes.Keys)
            {
                if (key != Schema.Transaction.BatchStatusMirror && key != Schema.Transaction.Id)
                {
                    touchesOnlyMirror = false;
                    break;
                }
            }

            if (touchesOnlyMirror)
            {
                tracing.Trace("GuardTransaction: transaccion {0}, escritura solo del espejo de estado, se permite.", target.Id);
                return TransactionGuard.EvaluateTransactionUpdate(
                    true, false, null, null, false, false, null, null, false);
            }

            // H-04: el request toca dinero (montos, cuenta o el total). Se evalua ANTES
            // de resolver nada mas porque decide si hace falta el estado del lote actual.
            var touchesAmounts = false;
            foreach (var money in Schema.Transaction.MoneyAttributes)
            {
                if (target.Contains(money))
                {
                    touchesAmounts = true;
                    break;
                }
            }

            var pre = ResolveTransactionPreState(context, serviceFactory, tracing, target.Id);

            if (touchesAmounts)
            {
                // Evidencia del intento en la traza (solo administradores la ven): que
                // columna de dinero se toca y que decia antes. La pre-image incluye las
                // columnas de dinero justamente para esto.
                TraceMoneyDelta(context, tracing, target);
            }

            var statusInTarget = target.Contains(Schema.Transaction.TransactionStatus);
            var newStatus = statusInTarget ? ReadStatus(target, Schema.Transaction.TransactionStatus) : null;

            var batchInTargetUpd = target.Contains(Schema.Transaction.TransactionBatch);
            var newBatchRef = batchInTargetUpd
                ? target.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch)
                : null;
            var newBatchIsNull = batchInTargetUpd && newBatchRef == null;

            // Los estados de lote se leen como sistema SOLO cuando la regla los necesita.
            int? targetStatus = null;
            if (batchInTargetUpd && newBatchRef != null)
            {
                targetStatus = RetrieveBatchStatus(serviceFactory, newBatchRef.Id);
            }

            // El estado del lote actual lo piden dos reglas: la desasignacion y la de
            // dinero congelado (H-04). Una sola lectura, solo cuando alguna lo necesita.
            int? currentStatus = null;
            if ((newBatchIsNull || touchesAmounts) && pre.BatchRef != null)
            {
                currentStatus = RetrieveBatchStatus(serviceFactory, pre.BatchRef.Id);
            }

            tracing.Trace(
                "GuardTransaction: transaccion {0} status-prev={1} status-nuevo={2} lote-previo={3} lote-actual-estado={4} lote-destino-estado={5} toca-dinero={6}.",
                target.Id, Fmt(pre.Status), statusInTarget ? Fmt(newStatus) : "(sin cambio)",
                pre.BatchRef != null ? pre.BatchRef.Id.ToString() : "null",
                Fmt(currentStatus), Fmt(targetStatus), touchesAmounts);

            return TransactionGuard.EvaluateTransactionUpdate(
                touchesOnlyMirror: false,
                statusInTarget: statusInTarget,
                newStatus: newStatus,
                previousStatus: pre.Status,
                batchInTarget: batchInTargetUpd,
                newBatchIsNull: newBatchIsNull,
                currentBatchStatus: currentStatus,
                targetBatchStatus: targetStatus,
                hadBatch: pre.BatchRef != null,
                touchesAmounts: touchesAmounts);
        }

        // ---- Cheque (handshake de recepcion) ----

        private static TransactionGuardResult EvaluateCheck(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            var target = GetTargetEntity(context);

            if (message == "Create")
            {
                var bornStatus = ReadStatus(target, Schema.Check.CheckStatus);
                tracing.Trace("GuardTransaction: create de cheque status={0}.", Fmt(bornStatus));
                return CheckGuard.EvaluateCheckCreate(bornStatus);
            }

            // Escritura SOLO de columnas espejo del cheque — sanic_bag (B6) y
            // sanic_recountingsession (B-9) — puestas por CheckMirrorStampPlugin, por
            // su cascada desde la tula o por el backfill: infraestructura, no negocio.
            // Este paso NO tiene filteringattributes a proposito (Recibido congela el
            // registro entero), asi que ve TODAS las escrituras: sin este bypass, la
            // cascada de sesion y el backfill de B-9 se estrellan contra cada cheque
            // ya Recibido. La plataforma agrega el primary key al Target: se ignora.
            var touchesOnlyMirror = true;
            foreach (var key in target.Attributes.Keys)
            {
                if (key != Schema.Check.BagLookup
                    && key != Schema.Check.SessionLookup
                    && key != Schema.Check.Id)
                {
                    touchesOnlyMirror = false;
                    break;
                }
            }

            if (touchesOnlyMirror)
            {
                tracing.Trace(
                    "GuardTransaction: cheque {0}, escritura solo de espejos ({1}), se permite.",
                    target.Id, string.Join(",", target.Attributes.Keys));
                return CheckGuard.EvaluateCheckUpdate(null, false, null, touchesOnlyMirror: true);
            }

            var statusInTarget = target.Contains(Schema.Check.CheckStatus);
            var newStatus = statusInTarget ? ReadStatus(target, Schema.Check.CheckStatus) : null;
            var previousStatus = ResolveCheckPreState(context, serviceFactory, tracing, target.Id);

            tracing.Trace("GuardTransaction: cheque {0} prev={1} nuevo={2}.",
                target.Id, Fmt(previousStatus), statusInTarget ? Fmt(newStatus) : "(sin cambio)");

            return CheckGuard.EvaluateCheckUpdate(previousStatus, statusInTarget, newStatus);
        }

        /// <summary>
        /// Estado previo del cheque. Si hay pre-image se confia en ella (un atributo
        /// ausente en la imagen ES null en la base: los cheques nacen sin estado);
        /// sin pre-image se lee como sistema.
        /// </summary>
        private static int? ResolveCheckPreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid checkId)
        {
            var pre = GetPreImage(context, Schema.Check.PreImageName);
            if (pre != null)
            {
                return ReadStatus(pre, Schema.Check.CheckStatus);
            }

            tracing.Trace("GuardTransaction: sin pre-image utilizable, se lee el cheque como sistema.");
            var service = serviceFactory.CreateOrganizationService(null);
            var current = service.Retrieve(
                Schema.Check.LogicalName, checkId,
                new ColumnSet(Schema.Check.CheckStatus));

            return ReadStatus(current, Schema.Check.CheckStatus);
        }

        // ---- Resolucion de pre-states (pre-image con fallback de lectura como sistema) ----

        /// <summary>
        /// Estado previo y sanic_ingresadopor del lote. Si la pre-image trae el estado
        /// se confia en ella (un atributo ausente en la imagen ES null en la base: los
        /// lotes pre-estampas no tienen ingresadopor); sin pre-image utilizable se lee
        /// como sistema.
        /// </summary>
        private static BatchPreState ResolveBatchPreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            Guid batchId)
        {
            var pre = GetPreImage(context, Schema.TransactionBatch.PreImageName);
            if (pre != null && pre.Contains(Schema.TransactionBatch.BatchStatus))
            {
                return new BatchPreState(
                    ReadStatus(pre, Schema.TransactionBatch.BatchStatus),
                    pre.GetAttributeValue<EntityReference>(Schema.TransactionBatch.IngresadoPor)?.Id);
            }

            var service = serviceFactory.CreateOrganizationService(null);
            var batch = service.Retrieve(
                Schema.TransactionBatch.LogicalName, batchId,
                new ColumnSet(Schema.TransactionBatch.BatchStatus, Schema.TransactionBatch.IngresadoPor));

            return new BatchPreState(
                ReadStatus(batch, Schema.TransactionBatch.BatchStatus),
                batch.GetAttributeValue<EntityReference>(Schema.TransactionBatch.IngresadoPor)?.Id);
        }

        /// <summary>
        /// Estado y lote previos de la transaccion. Si hay pre-image se confia en ella
        /// (un atributo ausente en la imagen ES null en la base); sin pre-image se lee
        /// como sistema.
        /// </summary>
        private static TransactionPreState ResolveTransactionPreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid transactionId)
        {
            var pre = GetPreImage(context, Schema.Transaction.PreImageName);
            if (pre != null)
            {
                return new TransactionPreState(
                    ReadStatus(pre, Schema.Transaction.TransactionStatus),
                    pre.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch));
            }

            tracing.Trace("GuardTransaction: sin pre-image utilizable, se lee la transaccion como sistema.");
            var service = serviceFactory.CreateOrganizationService(null);
            var current = service.Retrieve(
                Schema.Transaction.LogicalName, transactionId,
                new ColumnSet(Schema.Transaction.TransactionStatus, Schema.Transaction.TransactionBatch));

            return new TransactionPreState(
                ReadStatus(current, Schema.Transaction.TransactionStatus),
                current.GetAttributeValue<EntityReference>(Schema.Transaction.TransactionBatch));
        }

        private static int? RetrieveBatchStatus(IOrganizationServiceFactory serviceFactory, Guid batchId)
        {
            var service = serviceFactory.CreateOrganizationService(null);
            var batch = service.Retrieve(
                Schema.TransactionBatch.LogicalName, batchId,
                new ColumnSet(Schema.TransactionBatch.BatchStatus));
            return ReadStatus(batch, Schema.TransactionBatch.BatchStatus);
        }

        /// <summary>true si el lote tiene al menos una transaccion (query top-1 sin columnas).</summary>
        private static bool BatchHasTransactions(IOrganizationServiceFactory serviceFactory, Guid batchId)
        {
            var service = serviceFactory.CreateOrganizationService(null);
            var query = new QueryExpression(Schema.Transaction.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(
                Schema.Transaction.TransactionBatch, ConditionOperator.Equal, batchId);

            return service.RetrieveMultiple(query).Entities.Count > 0;
        }

        // ---- Helpers ----

        private static Entity GetTargetEntity(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                throw new InvalidPluginExecutionException(
                    $"GuardTransaction: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo Entity.");
            }

            return target;
        }

        private static EntityReference GetTargetReference(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is EntityReference reference))
            {
                throw new InvalidPluginExecutionException(
                    $"GuardTransaction: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo EntityReference.");
            }

            return reference;
        }

        /// <summary>
        /// Deja en la traza que columna de dinero toca el request y que valor tenia en la
        /// pre-image. No va al mensaje de error: la traza la leen administradores, el
        /// mensaje lo lee cualquiera.
        /// </summary>
        private static void TraceMoneyDelta(
            IPluginExecutionContext context, ITracingService tracing, Entity target)
        {
            var pre = GetPreImage(context, Schema.Transaction.PreImageName);

            foreach (var money in Schema.Transaction.MoneyAttributes)
            {
                if (!target.Contains(money))
                {
                    continue;
                }

                var previous = pre != null && pre.Contains(money) ? pre[money] : null;
                tracing.Trace("GuardTransaction: dinero — {0} previo={1} nuevo={2}.",
                    money,
                    previous == null ? "(sin dato)" : previous.ToString(),
                    target[money] == null ? "null" : target[money].ToString());
            }
        }

        private static Entity GetPreImage(IPluginExecutionContext context, string name)
        {
            return context.PreEntityImages.TryGetValue(name, out var image) ? image : null;
        }

        private static int? ReadStatus(Entity entity, string attribute)
        {
            return entity.GetAttributeValue<OptionSetValue>(attribute)?.Value;
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";

        private struct BatchPreState
        {
            public BatchPreState(int? status, Guid? ingresadoPor)
            {
                Status = status;
                IngresadoPor = ingresadoPor;
            }

            public int? Status { get; }
            public Guid? IngresadoPor { get; }
        }

        private struct TransactionPreState
        {
            public TransactionPreState(int? status, EntityReference batchRef)
            {
                Status = status;
                BatchRef = batchRef;
            }

            public int? Status { get; }
            public EntityReference BatchRef { get; }
        }
    }
}
