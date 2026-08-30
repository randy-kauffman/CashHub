using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Puntos 4 y 5 del guard de CH-App-Recounting v2 (00 §2.2). Un unico plugin type
    /// registrado en pasos SINCRONOS Pre-validation (10) que despacha por entidad y
    /// mensaje; las reglas viven en el dominio puro (<see cref="RecountingGuard"/>) y
    /// aca solo se arma el contexto y se propaga el bloqueo. Mismo esqueleto que
    /// <see cref="GuardReceptionPlugin"/>.
    ///
    /// GATE DE ESTADO (punto 4, mata S-5) — Create/Update/Delete de
    /// sanic_chtbldepositsrecounting, sanic_chtblchecks y sanic_chtblcountedmoneydetails:
    /// se rechazan si la tula ANCESTRO no esta 'Asignada a cajero'. La v1 condicionaba
    /// botones en Power Fx, que es cosmetico: por Web API cualquiera con el rol podia
    /// cargar depositos sobre una tula en boveda o agregar cheques a una tula ya
    /// Recontada —o sea, DESPUES de que sus asientos contables se generaron—.
    ///
    /// EXCEPCIONES (ver <see cref="RecountingGuard.IsWriteExempt"/>):
    ///   (a) Depth &gt; 1 — el pipeline de plugins (espejos E-1/E-2/E-4 y sus cascadas).
    ///   (b) Update de cheque que toca UNICAMENTE sanic_checkstatus — el marcado a
    ///       Recontado del cierre de tula (04 §11.3 paso 2), que corre DESPUES de mover
    ///       la bolsa. CheckGuard sigue siendo quien decide si esa transicion es legal.
    ///   (c) NO hay excepcion por usuario (SP/seeder). El repo no la tiene en ningun
    ///       guard: el seeder respeta el guard por ORDEN y, para el --clean, desactiva
    ///       temporalmente los pasos Delete concretos (seed-reception-stress.py).
    ///
    /// Fuera del gate a proposito:
    ///   - sanic_chtblbags: la apertura ('Almacenada en boveda' -> 'Asignada a cajero')
    ///     la patchea el cajero (00 §2.2 punto 6) y ya esta en la matriz legal de
    ///     ReceptionGuard, que la impone. Gatearla aca la haria imposible.
    ///   - sanic_chtbltransactions: los asientos los crea BagRecountClosurePlugin con la
    ///     tula YA en Recontada. Ademas tiene su propio guard (GuardTransactionPlugin).
    ///
    /// BAJAS ACOTADAS (punto 5, D02-4) — Delete de sanic_chtblapprovalrequest y
    /// sanic_chtblrecountingsession: el cajero necesita retirar lo que el mismo creo
    /// (Cancelar solicitud / Elegir otra mesa, 02 §9), y sin eso la mesa queda ocupada
    /// todo el dia y la solicitud sigue en la cola del supervisor. Se permite SOLO lo
    /// propio y sin resolver; todo lo demas se rechaza con mensaje.
    /// </summary>
    public sealed class GuardRecountingPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var entity = context.PrimaryEntityName;
            var message = context.MessageName;

            tracing.Trace("GuardRecounting: {0} de {1}, actor={2}, depth={3}.",
                message, entity, context.InitiatingUserId, context.Depth);

            RecountingGuardResult result;
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
                tracing.Trace("GuardRecounting: error inesperado evaluando {0} de {1}: {2}", message, entity, ex);
                throw new InvalidPluginExecutionException(
                    $"GuardRecounting no pudo evaluar {message} de {entity}: {ex.Message}", ex);
            }

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardRecounting: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardRecounting: operacion permitida.");
        }

        private static RecountingGuardResult Evaluate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string entity,
            string message)
        {
            switch (entity)
            {
                case Schema.DepositRecounting.LogicalName:
                case Schema.Check.LogicalName:
                case Schema.CountedMoneyDetail.LogicalName:
                    return EvaluateChildWrite(context, serviceFactory, tracing, entity, message);

                case Schema.ApprovalRequest.LogicalName:
                    return EvaluateApprovalDelete(context, serviceFactory, tracing);

                case Schema.RecountingSession.LogicalName:
                    return EvaluateSessionDelete(context, serviceFactory, tracing);

                default:
                    tracing.Trace("GuardRecounting: entidad {0} fuera de alcance, se permite.", entity);
                    return RecountingGuardResult.Allow();
            }
        }

        // =================================================================
        // Punto 4 — gate de estado de los hijos del recuento
        // =================================================================

        private static RecountingGuardResult EvaluateChildWrite(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            string entity,
            string message)
        {
            var touchesOnlyCheckStatus = entity == Schema.Check.LogicalName
                                         && message == "Update"
                                         && TouchesOnly(context, entity, CheckStatusColumns);

            var touchesOnlyMirror = message == "Update"
                                    && TouchesOnly(context, entity, MirrorColumnsOf(entity));

            var isExempt = RecountingGuard.IsWriteExempt(
                message, context.Depth, touchesOnlyCheckStatus, touchesOnlyMirror);
            if (isExempt)
            {
                tracing.Trace(
                    "GuardRecounting: {0} de {1} EXENTO (depth={2}, solo-estado-de-cheque={3}, " +
                    "solo-espejo={4}); no se resuelve la tula.",
                    message, entity, context.Depth, touchesOnlyCheckStatus, touchesOnlyMirror);
                return RecountingGuard.IsWriteAllowed(true, false, null);
            }

            var system = serviceFactory.CreateOrganizationService(null);

            // Estado ACTUAL: la tula de la que el registro cuelga hoy.
            var currentBag = ResolveAncestorBag(context, system, tracing, entity, message, useTarget: false);
            var current = RecountingGuard.IsWriteAllowed(false, currentBag.Resolved, currentBag.Status);

            tracing.Trace("GuardRecounting: {0} de {1} — tula actual {2} estado={3} => {4}.",
                message, entity, Fmt(currentBag.Id), Fmt(currentBag.Status), current.IsAllowed);

            if (!current.IsAllowed)
            {
                return current;
            }

            // DE QUIEN ES LA TULA. El control de estado responde "¿esta sobre una
            // mesa?"; este responde "¿sobre CUAL?". Hacen falta los dos desde que
            // existe la devolucion: una tula devuelta y retomada por OTRO cajero
            // vuelve a estar "Asignada a cajero", y el control de estado la
            // aprobaba mientras el primer cajero seguia con la pantalla abierta.
            var cashier = ReadSessionCashier(system, tracing, currentBag.SessionId);
            var owner = RecountingGuard.IsWriteOwner(
                currentBag.SessionId != null, cashier, context.InitiatingUserId);

            tracing.Trace("GuardRecounting: dueño — sesion {0} cajero {1} actor {2} => {3}.",
                Fmt(currentBag.SessionId), Fmt(cashier), context.InitiatingUserId, owner.IsAllowed);

            if (!owner.IsAllowed)
            {
                // La lectura de "¿es service principal?" se paga UNICAMENTE aca. En el
                // camino normal —el cajero escribiendo SU tula— no cuesta nada.
                owner = RecountingGuard.IsWriteOwner(
                    currentBag.SessionId != null, cashier, context.InitiatingUserId,
                    IsApplicationUser(system, tracing, context.InitiatingUserId));

                if (!owner.IsAllowed)
                {
                    return owner;
                }

                tracing.Trace("GuardRecounting: actor {0} es usuario de aplicacion; " +
                              "exento del control de dueño (no del de estado).",
                              context.InitiatingUserId);
            }

            // RE-COLGADO: si el request mueve el registro a OTRA tula, la tula destino
            // tambien tiene que estar en la mesa. Sin esto, un Update de un deposito
            // legitimo lo re-cuelga de una tula en boveda y el dinero cambia de dueno.
            if (message != "Update")
            {
                return current;
            }

            var newBag = ResolveAncestorBag(context, system, tracing, entity, message, useTarget: true);
            if (!newBag.Reparented || newBag.Id == currentBag.Id)
            {
                return current;
            }

            var target = RecountingGuard.IsWriteAllowed(false, newBag.Resolved, newBag.Status);
            tracing.Trace("GuardRecounting: re-colgado hacia la tula {0} estado={1} => {2}.",
                Fmt(newBag.Id), Fmt(newBag.Status), target.IsAllowed);

            return target;
        }

        /// <summary>El estado del cheque, que gobierna <see cref="CheckGuard"/>, no este gate.</summary>
        private static readonly string[] CheckStatusColumns = { Schema.Check.CheckStatus };

        /// <summary>
        /// Columnas ESPEJO derivadas de cada tabla gateada — las que mantienen los
        /// plugins de espejo y estampan los backfills por Web API. Ninguna es dato de
        /// negocio: son "Mantenido por plugin. No editar a mano.".
        /// El detalle de arqueo no tiene espejos: array vacio = nunca exento por esta via.
        /// </summary>
        private static string[] MirrorColumnsOf(string entity)
        {
            switch (entity)
            {
                case Schema.DepositRecounting.LogicalName:
                    return new[] { Schema.DepositRecounting.SessionMirror };
                case Schema.Check.LogicalName:
                    return new[] { Schema.Check.BagLookup, Schema.Check.SessionLookup };
                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// El Target escribe UNICAMENTE columnas de <paramref name="allowed"/> (y al
        /// menos una). La plataforma agrega el primary key al Target de un Update: se
        /// ignora. Mismo patron que touchesOnlyMirror / touchesOnlyTotals de
        /// <see cref="ReceptionGuard"/> y <see cref="CheckGuard"/>.
        /// </summary>
        private static bool TouchesOnly(IPluginExecutionContext context, string entity, string[] allowed)
        {
            if (allowed.Length == 0)
            {
                return false;
            }

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                return false;
            }

            var primaryKey = PrimaryKeyOf(entity);
            var touched = false;

            foreach (var key in target.Attributes.Keys)
            {
                if (key == primaryKey)
                {
                    continue;
                }

                if (Array.IndexOf(allowed, key) < 0)
                {
                    return false;
                }

                touched = true;
            }

            return touched;
        }

        private static string PrimaryKeyOf(string entity)
        {
            switch (entity)
            {
                case Schema.DepositRecounting.LogicalName:
                    return Schema.DepositRecounting.Id;
                case Schema.Check.LogicalName:
                    return Schema.Check.Id;
                default:
                    return Schema.CountedMoneyDetail.Id;
            }
        }

        /// <summary>
        /// Tula ancestro del registro. La cadena depende de la tabla:
        ///   deposito            -> sanic_bagid
        ///   cheque              -> sanic_relatedrecounterdepositid -> deposito -> tula
        ///   detalle de arqueo   -> sanic_relateddepositrecountingid -> deposito -> tula
        ///
        /// El lookup se toma del Target cuando <paramref name="useTarget"/> (el
        /// re-colgado) y, si no, de la PreImage con fallback a lectura como SISTEMA:
        /// es un chequeo de seguridad de solo lectura, independiente de los privilegios
        /// del actor.
        /// </summary>
        private static AncestorBag ResolveAncestorBag(
            IPluginExecutionContext context,
            IOrganizationService system,
            ITracingService tracing,
            string entity,
            string message,
            bool useTarget)
        {
            string parentAttribute;
            string preImageName;
            switch (entity)
            {
                case Schema.DepositRecounting.LogicalName:
                    parentAttribute = Schema.DepositRecounting.BagLookup;
                    preImageName = Schema.DepositRecounting.PreImageName;
                    break;
                case Schema.Check.LogicalName:
                    parentAttribute = Schema.Check.RelatedDepositLookup;
                    preImageName = Schema.Check.PreImageName;
                    break;
                default:
                    parentAttribute = Schema.CountedMoneyDetail.DepositLookup;
                    preImageName = Schema.CountedMoneyDetail.PreImageName;
                    break;
            }

            var parentRef = ReadParentReference(
                context, system, tracing, entity, message, parentAttribute, preImageName,
                useTarget, out var reparented);

            if (parentRef == null)
            {
                tracing.Trace("GuardRecounting: {0} sin '{1}': no hay tula que resolver.", entity, parentAttribute);
                return new AncestorBag { Resolved = false, Reparented = reparented };
            }

            // El deposito ya ES el hijo directo de la tula; cheque y detalle dan un salto mas.
            var bagId = entity == Schema.DepositRecounting.LogicalName
                ? parentRef.Id
                : ReadDepositBagId(system, tracing, parentRef.Id);

            if (bagId == null || bagId.Value == Guid.Empty)
            {
                return new AncestorBag { Resolved = false, Reparented = reparented };
            }

            int? status;
            Guid? sessionId;
            try
            {
                // La sesion se pide en la MISMA lectura que ya se hacia por el estado:
                // el control de dueño no agrega ni un viaje.
                var bag = system.Retrieve(
                    Schema.Bag.LogicalName, bagId.Value,
                    new ColumnSet(Schema.Bag.BagStatus, Schema.Bag.RecountingSessionLookup));
                status = bag.GetAttributeValue<OptionSetValue>(Schema.Bag.BagStatus)?.Value;
                sessionId = bag.GetAttributeValue<EntityReference>(
                    Schema.Bag.RecountingSessionLookup)?.Id;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardRecounting: no se pudo leer la tula {0}: {1}", bagId, ex.Message);
                return new AncestorBag { Id = bagId, Resolved = false, Reparented = reparented };
            }

            return new AncestorBag
            {
                Id = bagId,
                Status = status,
                SessionId = sessionId,
                Resolved = true,
                Reparented = reparented,
            };
        }

        private static EntityReference? ReadParentReference(
            IPluginExecutionContext context,
            IOrganizationService system,
            ITracingService tracing,
            string entity,
            string message,
            string parentAttribute,
            string preImageName,
            bool useTarget,
            out bool reparented)
        {
            reparented = false;

            var hasTargetEntity = context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity;
            var target = hasTargetEntity ? (Entity)raw! : null;

            if (target != null && target.Contains(parentAttribute))
            {
                reparented = message == "Update";
                if (useTarget || message == "Create")
                {
                    return target.GetAttributeValue<EntityReference>(parentAttribute);
                }
            }
            else if (useTarget)
            {
                // No hay re-colgado: la tula "destino" es la misma que la actual.
                return null;
            }

            if (context.PreEntityImages.TryGetValue(preImageName, out var pre)
                && pre != null
                && pre.Contains(parentAttribute))
            {
                return pre.GetAttributeValue<EntityReference>(parentAttribute);
            }

            var recordId = ResolveRecordId(context, target);
            if (recordId == Guid.Empty)
            {
                return null;
            }

            tracing.Trace("GuardRecounting: sin PreImage utilizable de {0}, se lee el registro como sistema.", entity);
            try
            {
                var current = system.Retrieve(entity, recordId, new ColumnSet(parentAttribute));
                return current.GetAttributeValue<EntityReference>(parentAttribute);
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardRecounting: no se pudo leer {0} {1}: {2}", entity, recordId, ex.Message);
                return null;
            }
        }

        private static Guid? ReadDepositBagId(IOrganizationService system, ITracingService tracing, Guid depositId)
        {
            try
            {
                var deposit = system.Retrieve(
                    Schema.DepositRecounting.LogicalName, depositId,
                    new ColumnSet(Schema.DepositRecounting.BagLookup));
                return deposit.GetAttributeValue<EntityReference>(Schema.DepositRecounting.BagLookup)?.Id;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardRecounting: no se pudo leer el deposito {0}: {1}", depositId, ex.Message);
                return null;
            }
        }

        // =================================================================
        // Punto 5 — bajas acotadas
        // =================================================================

        private static RecountingGuardResult EvaluateApprovalDelete(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            if (context.MessageName != "Delete")
            {
                // Create y Update de solicitudes son de GuardReceptionPlugin: una sola
                // autoridad por regla, si no terminan siendo dos verdades.
                return RecountingGuardResult.Allow();
            }

            var requestId = GetTargetReference(context).Id;

            Guid creatorId;
            int? status;

            if (context.PreEntityImages.TryGetValue(Schema.ApprovalRequest.PreImageName, out var pre)
                && pre != null
                && pre.Contains(Schema.ApprovalRequest.CreatedBy))
            {
                creatorId = pre.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id ?? Guid.Empty;
                status = pre.GetAttributeValue<OptionSetValue>(Schema.ApprovalRequest.ApprovalStatus)?.Value;
            }
            else
            {
                tracing.Trace("GuardRecounting: sin PreImage de la solicitud, se lee como sistema.");
                var system = serviceFactory.CreateOrganizationService(null);
                var current = system.Retrieve(
                    Schema.ApprovalRequest.LogicalName, requestId,
                    new ColumnSet(Schema.ApprovalRequest.CreatedBy, Schema.ApprovalRequest.ApprovalStatus));
                creatorId = current.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id
                            ?? Guid.Empty;
                status = current.GetAttributeValue<OptionSetValue>(Schema.ApprovalRequest.ApprovalStatus)?.Value;
            }

            tracing.Trace("GuardRecounting: baja de solicitud {0} creador={1} actor={2} estado={3}.",
                requestId, creatorId, context.InitiatingUserId, Fmt(status));

            return RecountingGuard.EvaluateApprovalDelete(creatorId, context.InitiatingUserId, status);
        }

        private static RecountingGuardResult EvaluateSessionDelete(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            if (context.MessageName != "Delete")
            {
                return RecountingGuardResult.Allow();
            }

            var sessionId = GetTargetReference(context).Id;
            var system = serviceFactory.CreateOrganizationService(null);

            Guid cashierId;
            bool confirmed;

            if (context.PreEntityImages.TryGetValue(Schema.RecountingSession.PreImageName, out var pre)
                && pre != null
                && pre.Contains(Schema.RecountingSession.Cashier))
            {
                cashierId = pre.GetAttributeValue<EntityReference>(Schema.RecountingSession.Cashier)?.Id ?? Guid.Empty;
                confirmed = pre.GetAttributeValue<bool>(Schema.RecountingSession.Confirmed);
            }
            else
            {
                tracing.Trace("GuardRecounting: sin PreImage de la sesion, se lee como sistema.");
                var current = system.Retrieve(
                    Schema.RecountingSession.LogicalName, sessionId,
                    new ColumnSet(Schema.RecountingSession.Cashier, Schema.RecountingSession.Confirmed));
                cashierId = current.GetAttributeValue<EntityReference>(Schema.RecountingSession.Cashier)?.Id
                            ?? Guid.Empty;
                confirmed = current.GetAttributeValue<bool>(Schema.RecountingSession.Confirmed);
            }

            var actor = context.InitiatingUserId;

            // La solicitud resuelta solo se busca cuando puede cambiar el resultado: si
            // la sesion esta confirmada el dominio bloquea igual, y si es ajena el
            // congelamiento no es lo que decide.
            var hasResolvedApproval = false;
            if (cashierId != Guid.Empty && !confirmed && cashierId == actor)
            {
                hasResolvedApproval = HasResolvedApproval(system, tracing, sessionId);
            }

            // La membresia se consulta SOLO en los casos que sin ella quedarian
            // bloqueados: sesion ajena, o propia pero congelada por una solicitud ya
            // resuelta (el rechazo, que es justamente cuando el supervisor necesita
            // borrarla). En el camino feliz del cajero no se paga la query.
            var actorIsApprover = false;
            if (cashierId != Guid.Empty && !confirmed && (cashierId != actor || hasResolvedApproval))
            {
                actorIsApprover = ApproverTeamLookup.IsApprover(system, tracing, actor);
            }

            tracing.Trace(
                "GuardRecounting: baja de sesion {0} cajero={1} actor={2} confirmada={3} resuelta={4} aprobador={5}.",
                sessionId, cashierId, actor, confirmed, hasResolvedApproval, actorIsApprover);

            return RecountingGuard.EvaluateSessionDelete(
                cashierId, actor, confirmed, hasResolvedApproval, actorIsApprover);
        }

        /// <summary>
        /// ¿Hay alguna solicitud YA RESUELTA que referencie esta sesion? El vinculo no
        /// es un lookup: viaja como texto dentro del JSON de sanic_requestresources
        /// (clave RecountingSessionGUID, 01 §5.7), asi que se busca por `Like`. Es una
        /// consulta acotada —solo solicitudes con estado— y solo corre cuando la baja
        /// ya paso los chequeos de pertenencia.
        /// </summary>
        private static bool HasResolvedApproval(
            IOrganizationService system, ITracingService tracing, Guid sessionId)
        {
            try
            {
                var query = new QueryExpression(Schema.ApprovalRequest.LogicalName)
                {
                    ColumnSet = new ColumnSet(false),
                    TopCount = 1,
                };
                query.Criteria.AddCondition(
                    Schema.ApprovalRequest.ApprovalStatus, ConditionOperator.NotNull);
                query.Criteria.AddCondition(
                    SupervisorMirrors.ApprovalRequest.RequestResources,
                    ConditionOperator.Like,
                    "%" + sessionId.ToString("D") + "%");

                return system.RetrieveMultiple(query).Entities.Count > 0;
            }
            catch (Exception ex)
            {
                // FAIL-CLOSED: si no se puede verificar, se asume que hay una decision
                // del supervisor colgando y no se borra la sesion.
                tracing.Trace("GuardRecounting: no se pudo verificar solicitudes resueltas de la sesion {0}: {1}",
                    sessionId, ex.Message);
                return true;
            }
        }

        // ---- Helpers ----

        private static Guid ResolveRecordId(IPluginExecutionContext context, Entity? target)
        {
            if (target != null && target.Id != Guid.Empty)
            {
                return target.Id;
            }

            if (context.InputParameters.TryGetValue("Target", out var raw) && raw is EntityReference reference)
            {
                return reference.Id;
            }

            return context.PrimaryEntityId;
        }

        private static EntityReference GetTargetReference(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is EntityReference reference))
            {
                throw new InvalidPluginExecutionException(
                    $"GuardRecounting: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo EntityReference.");
            }

            return reference;
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";

        private static string Fmt(Guid? value) => value.HasValue ? value.Value.ToString() : "null";

        /// <summary>
        /// true si el actor es un usuario de APLICACION (service principal) y no una
        /// persona. Es la identidad con la que se despliega y se siembra el entorno.
        /// Fail-closed: si no se puede leer, se lo trata como persona.
        /// </summary>
        private static bool IsApplicationUser(
            IOrganizationService system, ITracingService tracing, Guid actorId)
        {
            if (actorId == Guid.Empty)
            {
                return false;
            }

            try
            {
                var user = system.Retrieve("systemuser", actorId, new ColumnSet("applicationid"));
                return user.GetAttributeValue<Guid?>("applicationid") != null;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardRecounting: no se pudo leer el actor {0}: {1}", actorId, ex.Message);
                return false;
            }
        }

        /// <summary>Cajero de la sesion. null si no hay sesion o no se pudo leer (fail-closed).</summary>
        private static Guid? ReadSessionCashier(
            IOrganizationService system, ITracingService tracing, Guid? sessionId)
        {
            if (sessionId == null || sessionId.Value == Guid.Empty)
            {
                return null;
            }

            try
            {
                var session = system.Retrieve(
                    Schema.RecountingSession.LogicalName, sessionId.Value,
                    new ColumnSet(Schema.RecountingSession.Cashier));
                return session.GetAttributeValue<EntityReference>(
                    Schema.RecountingSession.Cashier)?.Id;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardRecounting: no se pudo leer la sesion {0}: {1}", sessionId, ex.Message);
                return null;
            }
        }

        private struct AncestorBag
        {
            public Guid? Id { get; set; }
            public int? Status { get; set; }
            public bool Resolved { get; set; }

            /// <summary>sanic_recountingsession de la tula. Es lo que dice de QUIEN es.</summary>
            public Guid? SessionId { get; set; }

            /// <summary>true si el request trae el lookup del padre (posible re-colgado).</summary>
            public bool Reparented { get; set; }
        }
    }
}
