using System;
using System.Collections.Generic;
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
    ///
    /// Sobre las solicitudes de aprobacion este paso impone ademas (auditoria H-01/H-08):
    ///  - quien resuelve debe pertenecer al grupo AAD aprobador configurado en
    ///    CH-Tbl-Parameter (clave <see cref="ApproverGroupConfiguration.ParameterKey"/>),
    ///    FAIL-CLOSED;
    ///  - la excepcion "nacer aprobada" del tipo sesion de recuento solo vale si la sesion
    ///    referenciada existe y su cajero es el propio solicitante (leido como sistema).
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

            // Memo de la autoridad de aprobacion, con vida de UNA invocacion.
            // No es estatico ni vive en SharedVariables: la plataforma REUSA la instancia
            // del plugin entre ejecuciones y usuarios, y un veredicto de seguridad cacheado
            // fuera de la ejecucion es un bypass esperando a pasar.
            var authority = new ApproverAuthority(serviceFactory, tracing);

            ReceptionGuardResult result;
            try
            {
                result = Evaluate(context, serviceFactory, tracing, authority, entity, message);
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
            ApproverAuthority authority,
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
                    return EvaluateApproval(context, serviceFactory, tracing, authority, message);
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

            // Escritura SOLO de totales declarados materializados
            // (ReceptionBatchTotalsPlugin/backfill): infraestructura, no negocio.
            // La plataforma agrega el primary key al Target de un Update: se ignora.
            var touchesOnlyTotals = true;
            foreach (var key in update.Attributes.Keys)
            {
                if (key != Schema.ReceptionBatch.DecNioCash
                    && key != Schema.ReceptionBatch.DecNioCheck
                    && key != Schema.ReceptionBatch.DecNioTotal
                    && key != Schema.ReceptionBatch.DecUsdCash
                    && key != Schema.ReceptionBatch.DecUsdCheck
                    && key != Schema.ReceptionBatch.DecUsdTotal
                    && key != Schema.ReceptionBatch.Id)
                {
                    touchesOnlyTotals = false;
                    break;
                }
            }

            if (touchesOnlyTotals)
            {
                tracing.Trace("GuardReception: lote {0}, escritura solo de totales declarados, se permite.", update.Id);
                return ReceptionGuard.EvaluateBatchUpdate(null, false, null, touchesOnlyTotals: true);
            }

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

            // ---- NO SE PUEDE COLGAR UNA TULA DE UNA SESION MUERTA ----
            //   El otro agujero de "cerrar sin efecto": el cajero seguia abriendo tulas
            //   NUEVAS contra su sesion cerrada. El guard de recuento protege depositos,
            //   cheques y denominaciones, pero la ASIGNACION es un Update de la tula y
            //   pasaba por aca sin que nadie preguntara si la sesion seguia viva.
            //
            //   Solo se paga la lectura cuando el request SETEA el lookup de sesion, que
            //   es el unico momento en que la pregunta tiene sentido: abrir o reasignar.
            //   Un Update de estado sin tocar la sesion no cuesta nada.
            if (update.Contains(Schema.Bag.RecountingSessionLookup))
            {
                var target = update.GetAttributeValue<EntityReference>(
                    Schema.Bag.RecountingSessionLookup);

                if (target != null)
                {
                    var closed = IsSessionForciblyClosed(serviceFactory, tracing, target.Id);
                    var alive = RecountingGuard.IsWriteSessionOpen(closed.HasValue, closed ?? false);
                    tracing.Trace("GuardReception: colgar la tula de la sesion {0} => {1}.",
                        target.Id, alive.IsAllowed);

                    if (!alive.IsAllowed)
                    {
                        return ReceptionGuardResult.Block(alive.Reason);
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
            ApproverAuthority authority,
            string message)
        {
            return message == "Create"
                ? EvaluateApprovalCreate(context, serviceFactory, tracing)
                : EvaluateApprovalUpdate(context, serviceFactory, tracing, authority);
        }

        /// <summary>
        /// Create de solicitud. Dos cosas pasan aca:
        ///  1. H-08.3 — se mide el contrato de RequestResources y se traza el desvio
        ///     (los productores viejos son heterogeneos: se mide, no se bloquea todavia).
        ///  2. H-08 — la excepcion "nace aprobada" del tipo sesion de recuento se resuelve
        ///     server-side: la sesion referenciada tiene que ser del propio solicitante.
        ///     Si no lo es (o no se puede saber), la solicitud se DEGRADA a pendiente.
        /// </summary>
        private static ReceptionGuardResult EvaluateApprovalCreate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            var target = GetTargetEntity(context);
            var bornStatus = ReadStatus(target, Schema.ApprovalRequest.ApprovalStatus);
            var requestType = target.GetAttributeValue<string>(Schema.ApprovalRequest.RequestType);
            var resources = target.GetAttributeValue<string>(SupervisorMirrors.ApprovalRequest.RequestResources);

            // Telemetria del contrato (no bloquea): una linea estable por creacion para
            // poder medir cuantas solicitudes de cada tipo llegan fuera de contrato.
            var inspection = ApprovalRequestResources.Inspect(requestType, resources);
            tracing.Trace("GuardReception: contrato RequestResources {0} — {1}",
                inspection.IsCompliant ? "OK" : "DESVIO", inspection.Summary());

            // La sesion solo se resuelve cuando de verdad se invoca la excepcion:
            // una solicitud que nace pendiente no paga ninguna lectura extra.
            var ownership = RecountingSessionOwnership.NotApplicable;
            if (bornStatus.HasValue
                && string.Equals(requestType, ReceptionGuard.ApprovalTypeSesionRecuento, StringComparison.Ordinal))
            {
                ownership = ResolveSessionOwnership(
                    serviceFactory, tracing, inspection, context.InitiatingUserId);
            }

            var evaluation = ReceptionGuard.EvaluateApprovalCreate(bornStatus, requestType, ownership);

            tracing.Trace("GuardReception: solicitud nueva estado={0} tipo='{1}' sesion={2} => {3}.",
                Fmt(bornStatus), requestType ?? "(sin tipo)", ownership, evaluation.Decision);

            if (evaluation.Decision == ApprovalCreateDecision.Block)
            {
                return ReceptionGuardResult.Block(evaluation.Reason);
            }

            if (evaluation.Decision == ApprovalCreateDecision.DegradeToPending)
            {
                // Se saca la decision del Target (Pre-validation: lo que quede en el
                // Target es lo que se escribe) en vez de abortar: la solicitud es
                // legitima, lo ilegitimo era nacer resuelta. Los sellos de resolucion
                // se van con ella para que el registro no quede incoherente.
                target.Attributes.Remove(Schema.ApprovalRequest.ApprovalStatus);
                target.Attributes.Remove(ApprovalNames.SolvedBy);
                target.Attributes.Remove(ApprovalNames.ResolutionDatetime);

                tracing.Trace("GuardReception: solicitud DEGRADADA a pendiente — {0}", evaluation.Reason);
            }

            return ReceptionGuardResult.Allow();
        }

        /// <summary>
        /// Update de solicitud: congelamiento post-decision, separacion creador/resolutor
        /// y (H-01) pertenencia al grupo AAD aprobador.
        /// </summary>
        private static ReceptionGuardResult EvaluateApprovalUpdate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            ApproverAuthority authority)
        {
            var update = GetTargetEntity(context);

            // El espejo sanic_bagnumber (B-6) lo estampa/backfillea un plugin desde el
            // JSON de RequestResources: metadata derivada, no una decision de negocio. El
            // grueso del backfill cae sobre solicitudes YA RESUELTAS, y el chequeo de
            // congelamiento de mas abajo (previousStatus != null) las bloquearia. Si el
            // Update toca SOLO el espejo, pasa aunque la solicitud este resuelta — mismo
            // racional que touchesOnlyMirror de Receptions. La plataforma agrega el PK al
            // Target de un Update: se ignora.
            var touchesOnlyMirror = true;
            foreach (var key in update.Attributes.Keys)
            {
                if (key != SupervisorMirrors.ApprovalRequest.BagNumber
                    && key != Schema.ApprovalRequest.Id)
                {
                    touchesOnlyMirror = false;
                    break;
                }
            }

            if (touchesOnlyMirror)
            {
                tracing.Trace("GuardReception: solicitud {0}, escritura solo del espejo de tula (B-6), se permite.",
                    update.Id);
                // No hace falta leer el pre-state ni la membresia: la decision "espejo
                // solo => Allow" vive en el dominio (y esta bajo test). Se pasa
                // previousStatus=null porque no se leyo, y la guarda touchesOnlyMirror lo
                // resuelve sin mirarlo — igual que EvaluateBatchUpdate con touchesOnlyTotals.
                return ReceptionGuard.EvaluateApprovalUpdate(
                    null, false, Guid.Empty, context.InitiatingUserId, false, touchesOnlyMirror: true);
            }

            var pre = ResolveApprovalPreState(context, serviceFactory, tracing, update.Id);
            var decisionInTarget = update.Contains(Schema.ApprovalRequest.ApprovalStatus);
            var actor = context.InitiatingUserId;

            // La membresia se consulta solo cuando la decision esta realmente en juego:
            // si la solicitud ya esta resuelta, si el request no toca el estado o si el
            // actor es el creador, el dominio bloquea igual y la lectura no aporta.
            // Ademas evita que un caso ya perdido dispare el fail-closed de configuracion.
            var isAuthorizedApprover = false;
            if (decisionInTarget
                && pre.Status == null
                && pre.CreatorId != Guid.Empty
                && pre.CreatorId != actor)
            {
                isAuthorizedApprover = authority.IsApprover(actor);
            }

            tracing.Trace(
                "GuardReception: solicitud {0} prev={1} decision={2} creador={3} actor={4} aprobador={5}.",
                update.Id, Fmt(pre.Status), decisionInTarget, pre.CreatorId, actor, isAuthorizedApprover);

            return ReceptionGuard.EvaluateApprovalUpdate(
                pre.Status, decisionInTarget, pre.CreatorId, actor, isAuthorizedApprover);
        }

        /// <summary>
        /// Resuelve, COMO SISTEMA, si la sesion de recuento referenciada en el JSON es del
        /// solicitante. Nunca lanza: cualquier duda es <see cref="RecountingSessionOwnership.Unresolved"/>
        /// y el dominio degrada la solicitud a pendiente (no se le voltea la creacion al cajero).
        /// </summary>
        private static RecountingSessionOwnership ResolveSessionOwnership(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            RequestResourcesInspection inspection,
            Guid requesterId)
        {
            var raw = inspection.Value(ApprovalRequestResources.RecountingSessionGuidKey);
            if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var sessionId) || sessionId == Guid.Empty)
            {
                tracing.Trace("GuardReception: RequestResources sin {0} legible; sesion no verificable.",
                    ApprovalRequestResources.RecountingSessionGuidKey);
                return RecountingSessionOwnership.Unresolved;
            }

            Entity session;
            try
            {
                var service = serviceFactory.CreateOrganizationService(null);
                session = service.Retrieve(
                    ApprovalNames.RecountingSession.LogicalName, sessionId,
                    new ColumnSet(ApprovalNames.RecountingSession.Cashier));
            }
            catch (Exception ex)
            {
                // Sesion inexistente o ilegible: no es motivo para voltear la creacion.
                tracing.Trace("GuardReception: no se pudo leer la sesion {0}: {1}", sessionId, ex.Message);
                return RecountingSessionOwnership.Unresolved;
            }

            var cashierId = session.GetAttributeValue<EntityReference>(ApprovalNames.RecountingSession.Cashier)?.Id
                            ?? Guid.Empty;
            if (cashierId == Guid.Empty)
            {
                tracing.Trace("GuardReception: sesion {0} sin cajero; no verificable.", sessionId);
                return RecountingSessionOwnership.Unresolved;
            }

            var isOwn = cashierId == requesterId;
            tracing.Trace("GuardReception: sesion {0} cajero={1} solicitante={2} => {3}.",
                sessionId, cashierId, requesterId, isOwn ? "propia" : "ajena");

            return isOwn
                ? RecountingSessionOwnership.OwnedByRequester
                : RecountingSessionOwnership.OwnedByOtherUser;
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

        /// <summary>
        /// Autoridad de aprobacion: resuelve si un usuario pertenece al grupo AAD aprobador
        /// configurado en CH-Tbl-Parameter, con el MISMO patron que
        /// GuardChangeRequestApprovalPlugin (AAD group team por azureactivedirectoryobjectid
        /// + teammembership). Se lee como sistema: es un chequeo de seguridad de solo
        /// lectura que no puede depender de los privilegios del actor.
        ///
        /// FAIL-CLOSED en todo: sin parametro, con parametro que no es GUID, sin team del
        /// grupo o con la consulta caida, lanza y la decision no se escribe.
        ///
        /// La instancia vive UNA invocacion (la crea Execute). Memoriza el GUID del grupo y
        /// el veredicto por usuario dentro de esa invocacion; deliberadamente NO usa estado
        /// estatico ni SharedVariables: la plataforma reusa la instancia del plugin entre
        /// ejecuciones y usuarios, y cachear un veredicto de seguridad mas alla de la
        /// ejecucion es exactamente como se construye un bypass.
        /// </summary>
        private sealed class ApproverAuthority
        {
            private readonly IOrganizationServiceFactory _serviceFactory;
            private readonly ITracingService _tracing;
            private readonly Dictionary<Guid, bool> _verdicts = new Dictionary<Guid, bool>();
            private IOrganizationService _service;
            private Guid? _groupId;

            public ApproverAuthority(IOrganizationServiceFactory serviceFactory, ITracingService tracing)
            {
                _serviceFactory = serviceFactory;
                _tracing = tracing;
            }

            public bool IsApprover(Guid userId)
            {
                if (_verdicts.TryGetValue(userId, out var cached))
                {
                    _tracing.Trace("GuardReception: membresia de {0} ya resuelta en esta ejecucion => {1}.",
                        userId, cached);
                    return cached;
                }

                var groupId = ResolveGroupId();
                bool isMember;
                try
                {
                    var query = new QueryExpression(Schema.Team.LogicalName)
                    {
                        ColumnSet = new ColumnSet(Schema.Team.Id),
                        TopCount = 1
                    };
                    query.Criteria.AddCondition(Schema.Team.AadObjectId, ConditionOperator.Equal, groupId);

                    var membershipLink = query.AddLink(
                        Schema.Team.MembershipRelationship, Schema.Team.Id, Schema.Team.Id);
                    var userLink = membershipLink.AddLink(
                        Schema.SystemUser.LogicalName, Schema.SystemUser.Id, Schema.SystemUser.Id);
                    userLink.LinkCriteria.AddCondition(Schema.SystemUser.Id, ConditionOperator.Equal, userId);

                    isMember = Service().RetrieveMultiple(query).Entities.Count > 0;
                }
                catch (Exception ex)
                {
                    _tracing.Trace("GuardReception: fallo la consulta de membresia del grupo {0}: {1}",
                        groupId, ex);
                    throw new InvalidPluginExecutionException(
                        ApproverGroupConfiguration.MembershipUnavailableMessage);
                }

                _tracing.Trace("GuardReception: membresia grupo={0} usuario={1} => {2}.",
                    groupId, userId, isMember);

                _verdicts[userId] = isMember;
                return isMember;
            }

            private Guid ResolveGroupId()
            {
                if (_groupId.HasValue)
                {
                    return _groupId.Value;
                }

                string rawValue;
                try
                {
                    var query = new QueryExpression(ApprovalNames.Parameter.LogicalName)
                    {
                        ColumnSet = new ColumnSet(ApprovalNames.Parameter.Value),
                        TopCount = 1
                    };
                    query.Criteria.AddCondition(
                        ApprovalNames.Parameter.Key, ConditionOperator.Equal,
                        ApproverGroupConfiguration.ParameterKey);
                    query.Criteria.AddCondition(
                        ApprovalNames.Parameter.IsActive, ConditionOperator.Equal, true);

                    var rows = Service().RetrieveMultiple(query).Entities;
                    rawValue = rows.Count > 0
                        ? rows[0].GetAttributeValue<string>(ApprovalNames.Parameter.Value)
                        : null;
                }
                catch (Exception ex)
                {
                    _tracing.Trace("GuardReception: fallo la lectura de CH-Tbl-Parameter '{0}': {1}",
                        ApproverGroupConfiguration.ParameterKey, ex);
                    throw new InvalidPluginExecutionException(
                        ApproverGroupConfiguration.MembershipUnavailableMessage);
                }

                // La politica fail-closed vive en el dominio (y esta bajo test).
                var resolution = ApproverGroupConfiguration.Resolve(rawValue);
                if (!resolution.IsConfigured)
                {
                    _tracing.Trace("GuardReception: parametro '{0}' ausente, inactivo o invalido.",
                        ApproverGroupConfiguration.ParameterKey);
                    throw new InvalidPluginExecutionException(resolution.Reason);
                }

                _groupId = resolution.GroupId;
                return resolution.GroupId;
            }

            private IOrganizationService Service()
            {
                return _service ?? (_service = _serviceFactory.CreateOrganizationService(null));
            }
        }

        /// <summary>
        /// Nombres logicos que consume el circuito de aprobacion y que todavia no estan en
        /// Schema.cs. Viven aca para no tocar ese archivo mientras corren en paralelo otros
        /// paquetes del rebuild; se pliegan a Schema (ApprovalRequest / tabla nueva) cuando
        /// cierren, sin cambiar una sola cadena.
        /// </summary>
        private static class ApprovalNames
        {
            /// <summary>Supervisor que resolvio (lookup a systemuser). Lo estampa la app al firmar.</summary>
            public const string SolvedBy = "sanic_solvedby";

            /// <summary>Fecha/hora de la resolucion.</summary>
            public const string ResolutionDatetime = "sanic_resolutiondatetime";

            /// <summary>Sesion de recuento (CH-Tbl-RecountingSessions).</summary>
            internal static class RecountingSession
            {
                public const string LogicalName = "sanic_chtblrecountingsession";

                /// <summary>Cajero dueno de la sesion (lookup a systemuser). Display name: "n".</summary>
                public const string Cashier = "sanic_cashier";
            }

            /// <summary>
            /// Tabla de configuracion key/value de CashHub (CH-Tbl-Parameter). Es LA fuente
            /// de config del backend: no se multiplican environment variables.
            /// </summary>
            internal static class Parameter
            {
                public const string LogicalName = "sanic_chtblparameter";

                /// <summary>Columna primaria = clave (convencion Dominio.Sub.Nombre).</summary>
                public const string Key = "sanic_name";

                public const string Value = "sanic_value";
                public const string IsActive = "sanic_isactive";
            }
        }

        /// <summary>
        /// true/false si se pudo leer la sesion; null si NO se pudo (fail-closed: el
        /// llamador lo trata como "no resoluble" y bloquea).
        /// </summary>
        private static bool? IsSessionForciblyClosed(
            IOrganizationServiceFactory serviceFactory, ITracingService tracing, Guid sessionId)
        {
            try
            {
                var system = serviceFactory.CreateOrganizationService(null);
                var session = system.Retrieve(
                    Schema.RecountingSession.LogicalName, sessionId,
                    new ColumnSet(Schema.RecountingSession.SessionForciblyClosed));
                return session.GetAttributeValue<bool>(
                    Schema.RecountingSession.SessionForciblyClosed);
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardReception: no se pudo leer la sesion {0}: {1}", sessionId, ex.Message);
                return null;
            }
        }
    }
}
