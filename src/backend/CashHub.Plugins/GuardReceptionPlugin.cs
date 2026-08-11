using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Guard del dominio de recepcion: lotes, planillas, tulas y solicitudes de aprobacion.
    ///
    /// Toda la maquina de estados de la recepcion vive en Power Fx (Reception, Recounting,
    /// Supervisor), asi que cualquier usuario con el rol puede, por Web API o Excel, mandar
    /// un lote directo a boveda, editar montos de una planilla archivada, borrar tulas de un
    /// lote cerrado o auto-aprobarse una solicitud. Un unico plugin type registrado en pasos
    /// sincronos Pre-validation de Create/Update/Delete de las cuatro tablas despacha por
    /// entidad y mensaje; las reglas viven en el dominio puro (ReceptionGuard) y aca solo se
    /// arma el contexto (pre-images con fallback de lectura como sistema) y se propaga el
    /// bloqueo. Las lecturas como sistema son chequeos de seguridad de solo lectura,
    /// independientes de los privilegios del actor.
    /// </summary>
    public sealed class GuardReceptionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var entity = context.PrimaryEntityName;
            var message = context.MessageName;
            tracing.Trace("GuardReception: {0} de {1}, actor={2}, depth={3}.",
                message, entity, context.InitiatingUserId, context.Depth);

            ReceptionGuardResult result;
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
                tracing.Trace("GuardReception: error inesperado evaluando {0} de {1}: {2}", message, entity, ex);
                throw new InvalidPluginExecutionException(
                    $"GuardReception no pudo evaluar {message} de {entity}: {ex.Message}", ex);
            }

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardReception: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardReception: operacion permitida.");
        }

        private static ReceptionGuardResult Evaluate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string entity,
            string message)
        {
            switch (entity)
            {
                case Schema.ReceptionBatch.LogicalName:
                    return EvaluateBatch(context, serviceFactory, tracing, message);
                case Schema.Reception.LogicalName:
                    return EvaluateReception(context, serviceFactory, tracing, message);
                case Schema.Bag.LogicalName:
                    return EvaluateBag(context, serviceFactory, tracing, message);
                case Schema.ApprovalRequest.LogicalName:
                    return EvaluateApproval(context, serviceFactory, tracing, message);
                default:
                    tracing.Trace("GuardReception: entidad {0} fuera de alcance, se permite.", entity);
                    return ReceptionGuardResult.Allow();
            }
        }

        // ---- Lote ----

        private static ReceptionGuardResult EvaluateBatch(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message == "Create")
            {
                var target = GetTargetEntity(context);
                return ReceptionGuard.EvaluateBatchCreate(ReadStatus(target, Schema.ReceptionBatch.ReceptionStatus));
            }

            if (message == "Delete")
            {
                var previous = ResolveBatchPreState(context, serviceFactory, tracing, GetTargetReference(context).Id);
                return ReceptionGuard.EvaluateBatchDelete(previous);
            }

            var update = GetTargetEntity(context);
            var statusInTarget = update.Contains(Schema.ReceptionBatch.ReceptionStatus);
            var newStatus = statusInTarget ? ReadStatus(update, Schema.ReceptionBatch.ReceptionStatus) : null;
            var previousStatus = ResolveBatchPreState(context, serviceFactory, tracing, update.Id);

            tracing.Trace("GuardReception: lote {0} prev={1} nuevo={2}.",
                update.Id, Fmt(previousStatus), statusInTarget ? Fmt(newStatus) : "(sin cambio)");

            return ReceptionGuard.EvaluateBatchUpdate(previousStatus, statusInTarget, newStatus);
        }

        // ---- Planilla ----

        private static ReceptionGuardResult EvaluateReception(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message == "Create")
            {
                var target = GetTargetEntity(context);
                var batchRef = target.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
                if (batchRef == null)
                {
                    return ReceptionGuard.EvaluateReceptionCreate(false, null);
                }

                var batchStatus = RetrieveBatchStatus(serviceFactory, tracing, batchRef.Id);
                return ReceptionGuard.EvaluateReceptionCreate(true, batchStatus);
            }

            if (message == "Delete")
            {
                var receptionId = GetTargetReference(context).Id;
                var batchStatus = ResolveReceptionBatchStatus(context, serviceFactory, tracing, receptionId);
                return ReceptionGuard.EvaluateReceptionDelete(batchStatus);
            }

            var update = GetTargetEntity(context);

            // El espejo sanic_batchstatus lo escribe ReceptionBatchStatusMirrorPlugin
            // (metadata derivada, no datos de negocio): si el request no toca nada mas,
            // pasa aunque el lote este congelado — es justamente la propagacion del cierre.
            // La plataforma agrega el primary key al Target de un Update: se ignora.
            var touchesOnlyMirror = true;
            foreach (var key in update.Attributes.Keys)
            {
                if (key != Schema.Reception.BatchStatusMirror && key != Schema.Reception.Id)
                {
                    touchesOnlyMirror = false;
                    break;
                }
            }

            if (touchesOnlyMirror)
            {
                tracing.Trace("GuardReception: planilla {0}, escritura solo del espejo de estado, se permite.", update.Id);
                return ReceptionGuardResult.Allow();
            }

            var currentBatchStatus = ResolveReceptionBatchStatus(context, serviceFactory, tracing, update.Id);

            var isReparent = update.Contains(Schema.Reception.BatchLookup);
            int? newBatchStatus = null;
            if (isReparent)
            {
                var newBatchRef = update.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
                newBatchStatus = newBatchRef == null
                    ? null
                    : RetrieveBatchStatus(serviceFactory, tracing, newBatchRef.Id);
            }

            tracing.Trace("GuardReception: planilla {0} lote-actual={1} reparent={2}.",
                update.Id, Fmt(currentBatchStatus), isReparent);

            return ReceptionGuard.EvaluateReceptionUpdate(currentBatchStatus, isReparent, newBatchStatus);
        }

        // ---- Tula ----

        private static ReceptionGuardResult EvaluateBag(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message == "Create")
            {
                var target = GetTargetEntity(context);
                return ReceptionGuard.EvaluateBagCreate(ReadStatus(target, Schema.Bag.BagStatus));
            }

            if (message == "Delete")
            {
                var pre = GetPreImage(context, Schema.Bag.PreImageName);
                var bagStatus = pre != null ? ReadStatus(pre, Schema.Bag.BagStatus) : null;

                // Con la tula pendiente de supervisor el borrado es parte del circuito de
                // aprobacion y no hace falta resolver el lote (dos lecturas menos).
                if (bagStatus == ReceptionGuard.BagPendienteSupervisorEsclusa)
                {
                    return ReceptionGuard.EvaluateBagDelete(bagStatus, null);
                }

                var receptionRef = pre?.GetAttributeValue<EntityReference>(Schema.Bag.ReceptionLookup);
                var batchStatus = receptionRef == null
                    ? null
                    : ResolveReceptionBatchStatus(context, serviceFactory, tracing, receptionRef.Id, usePreImage: false);
                return ReceptionGuard.EvaluateBagDelete(bagStatus, batchStatus);
            }

            var update = GetTargetEntity(context);
            var preImage = GetPreImage(context, Schema.Bag.PreImageName);
            var previousStatus = preImage != null ? ReadStatus(preImage, Schema.Bag.BagStatus) : null;

            if (update.Contains(Schema.Bag.BagStatus))
            {
                var newStatusValue = ReadStatus(update, Schema.Bag.BagStatus);
                if (newStatusValue.HasValue)
                {
                    var transition = ReceptionGuard.EvaluateBagStatusTransition(previousStatus, newStatusValue.Value);
                    if (!transition.IsAllowed)
                    {
                        return transition;
                    }
                }
            }

            var editsIdentity = update.Contains(Schema.Bag.BagId)
                                || update.Contains(Schema.Bag.Name)
                                || update.Contains(Schema.Bag.ReceptionLookup);
            if (!editsIdentity)
            {
                return ReceptionGuardResult.Allow();
            }

            // La edicion de identidad sobre una tula pendiente es el circuito de aprobacion;
            // el lote se resuelve solo cuando de verdad decide (evita 2 lecturas por tula).
            if (previousStatus == ReceptionGuard.BagPendienteSupervisorEsclusa)
            {
                return ReceptionGuard.EvaluateBagIdentityEdit(previousStatus, null);
            }

            var currentReceptionRef = preImage?.GetAttributeValue<EntityReference>(Schema.Bag.ReceptionLookup);
            var currentBatchStatus = currentReceptionRef == null
                ? null
                : ResolveReceptionBatchStatus(context, serviceFactory, tracing, currentReceptionRef.Id, usePreImage: false);

            tracing.Trace("GuardReception: tula {0} identidad, estado-tula={1} lote={2}.",
                update.Id, Fmt(previousStatus), Fmt(currentBatchStatus));

            return ReceptionGuard.EvaluateBagIdentityEdit(previousStatus, currentBatchStatus);
        }

        // ---- Solicitud ----

        private static ReceptionGuardResult EvaluateApproval(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string message)
        {
            if (message == "Create")
            {
                var target = GetTargetEntity(context);
                return ReceptionGuard.EvaluateApprovalCreate(
                    ReadStatus(target, Schema.ApprovalRequest.ApprovalStatus),
                    target.GetAttributeValue<string>(Schema.ApprovalRequest.RequestType));
            }

            var update = GetTargetEntity(context);
            var pre = ResolveApprovalPreState(context, serviceFactory, tracing, update.Id);
            var decisionInTarget = update.Contains(Schema.ApprovalRequest.ApprovalStatus);

            tracing.Trace("GuardReception: solicitud {0} prev={1} decision={2} creador={3} actor={4}.",
                update.Id, Fmt(pre.Status), decisionInTarget, pre.CreatorId, context.InitiatingUserId);

            // Quien puede decidir lo determina el modelo de acceso: solo el rol de
            // supervisor tiene Write sobre la tabla. Aca solo se impone la separacion
            // creador != resolutor y el congelamiento post-decision.
            return ReceptionGuard.EvaluateApprovalUpdate(
                pre.Status, decisionInTarget, pre.CreatorId, context.InitiatingUserId);
        }

        // ---- Resolucion de pre-states (pre-image con fallback de lectura como sistema) ----

        private static int? ResolveBatchPreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid batchId)
        {
            var pre = GetPreImage(context, Schema.ReceptionBatch.PreImageName);
            if (pre != null && pre.Contains(Schema.ReceptionBatch.ReceptionStatus))
            {
                return ReadStatus(pre, Schema.ReceptionBatch.ReceptionStatus);
            }

            return RetrieveBatchStatus(serviceFactory, tracing, batchId);
        }

        private static int? RetrieveBatchStatus(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid batchId)
        {
            var service = serviceFactory.CreateOrganizationService(null);
            var batch = service.Retrieve(
                Schema.ReceptionBatch.LogicalName, batchId,
                new ColumnSet(Schema.ReceptionBatch.ReceptionStatus));
            return ReadStatus(batch, Schema.ReceptionBatch.ReceptionStatus);
        }

        /// <summary>Estado del lote de una planilla: pre-image de la planilla (o lectura) + lectura del lote.</summary>
        private static int? ResolveReceptionBatchStatus(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid receptionId,
            bool usePreImage = true)
        {
            EntityReference? batchRef = null;

            if (usePreImage)
            {
                var pre = GetPreImage(context, Schema.Reception.PreImageName);
                if (pre != null && pre.Contains(Schema.Reception.BatchLookup))
                {
                    batchRef = pre.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
                }
            }

            if (batchRef == null)
            {
                var service = serviceFactory.CreateOrganizationService(null);
                var reception = service.Retrieve(
                    Schema.Reception.LogicalName, receptionId,
                    new ColumnSet(Schema.Reception.BatchLookup));
                batchRef = reception.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
            }

            if (batchRef == null)
            {
                tracing.Trace("GuardReception: planilla {0} sin lote (huerfana).", receptionId);
                return null;
            }

            return RetrieveBatchStatus(serviceFactory, tracing, batchRef.Id);
        }

        private static ApprovalPreState ResolveApprovalPreState(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid requestId)
        {
            var pre = GetPreImage(context, Schema.ApprovalRequest.PreImageName);
            if (pre != null)
            {
                return new ApprovalPreState(
                    ReadStatus(pre, Schema.ApprovalRequest.ApprovalStatus),
                    pre.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id ?? Guid.Empty);
            }

            tracing.Trace("GuardReception: sin pre-image utilizable, se lee la solicitud como sistema.");
            var service = serviceFactory.CreateOrganizationService(null);
            var current = service.Retrieve(
                Schema.ApprovalRequest.LogicalName, requestId,
                new ColumnSet(Schema.ApprovalRequest.ApprovalStatus, Schema.ApprovalRequest.CreatedBy));

            return new ApprovalPreState(
                ReadStatus(current, Schema.ApprovalRequest.ApprovalStatus),
                current.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id ?? Guid.Empty);
        }

        // ---- Helpers ----

        private static Entity GetTargetEntity(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                throw new InvalidPluginExecutionException(
                    $"GuardReception: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo Entity.");
            }

            return target;
        }

        private static EntityReference GetTargetReference(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is EntityReference reference))
            {
                throw new InvalidPluginExecutionException(
                    $"GuardReception: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo EntityReference.");
            }

            return reference;
        }

        private static Entity? GetPreImage(IPluginExecutionContext context, string name)
        {
            return context.PreEntityImages.TryGetValue(name, out var image) ? image : null;
        }

        private static int? ReadStatus(Entity entity, string attribute)
        {
            return entity.GetAttributeValue<OptionSetValue>(attribute)?.Value;
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";

        private struct ApprovalPreState
        {
            public ApprovalPreState(int? status, Guid creatorId)
            {
                Status = status;
                CreatorId = creatorId;
            }

            public int? Status { get; }
            public Guid CreatorId { get; }
        }
    }
}
