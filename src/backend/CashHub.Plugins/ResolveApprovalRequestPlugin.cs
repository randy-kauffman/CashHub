using System;
using System.Collections.Generic;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing de <c>sanic_CHResolveApprovalRequest</c>: resuelve una solicitud de
    /// aprobacion y aplica su efecto COMO UNIDAD TRANSACCIONAL.
    ///
    /// ORDEN, y es lo importante:
    ///   1. autorizar (misma regla de dominio que GuardReceptionPlugin)
    ///   2. si bloquea -> devolver el motivo como DATO, CERO escrituras
    ///   3. si autoriza -> aplicar el efecto y marcar la solicitud, en la misma
    ///      ejecucion; si algo falla, la plataforma revierte TODO
    ///
    /// La autorizacion va primero a proposito: aplicar el efecto antes dejaria que un
    /// no-aprobador destruyera datos y recien despues chocara contra el bloqueo.
    ///
    /// Las decisiones viven en <see cref="ApprovalEffectRules"/> (bajo test). Aca solo
    /// hay lecturas y escrituras.
    ///
    /// FASE 1: los 8 tipos de efecto acotado. Los dos que generan asientos contables
    /// (<see cref="ApprovalEffect.CerrarRecuentoConAsientos"/> y
    /// <see cref="ApprovalEffect.DevolverTulaAlCajero"/>) todavia los resuelve la app;
    /// aca se rechazan explicitamente para que nadie crea que ya estan cubiertos.
    /// </summary>
    public sealed class ResolveApprovalRequestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var requestId = ReadGuid(context, Schema.ResolveApprovalApi.InApprovalRequestId);
            var approve = ReadDecision(context);
            var reason = ReadOptionalString(context, Schema.ResolveApprovalApi.InReason);
            var actor = context.InitiatingUserId;

            tracing.Trace("ResolveApproval: solicitud {0} decision={1} actor={2}.",
                requestId, approve ? "Approve" : "Reject", actor);

            // DOS servicios, y la diferencia es deliberada:
            //
            //   system  — lee la solicitud y aplica el EFECTO sobre tulas, planillas y
            //             sesiones. Va como sistema porque el supervisor no
            //             necesariamente tiene privilegios de escritura en esas tablas,
            //             y no se los vamos a dar solo para esto.
            //
            //   asActor — marca la SOLICITUD, y va como el usuario real. Ese Update
            //             vuelve a disparar GuardReceptionPlugin, que es la autoridad
            //             de esta regla. Si lo hicieramos como sistema, el guard veria
            //             al usuario SYSTEM — que no esta en el grupo aprobador — y
            //             bloquearia la operacion entera. Pasó exactamente eso: efecto
            //             aplicado, marcado bloqueado, transaccion revertida.
            //
            // Mandar al actor real ahi no debilita nada: el guard revalida la MISMA
            // regla con el MISMO actor que este plugin ya evaluo. Es defensa en
            // profundidad de verdad, no una firma de goma.
            var system = factory.CreateOrganizationService(null);
            var asActor = factory.CreateOrganizationService(actor);

            // ---- 1. La solicitud, COMO SISTEMA ----
            Entity request;
            try
            {
                request = system.Retrieve(
                    Schema.ApprovalRequest.LogicalName, requestId,
                    new ColumnSet(
                        Schema.ApprovalRequest.ApprovalStatus,
                        Schema.ApprovalRequest.CreatedBy,
                        Schema.ApprovalRequest.RequestType,
                        SupervisorMirrors.ApprovalRequest.RequestResources));
            }
            catch (Exception ex)
            {
                tracing.Trace("ResolveApproval: no se pudo leer la solicitud {0}: {1}", requestId, ex);
                Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked,
                    "La solicitud ya no existe o no se pudo leer. Actualice la bandeja e intente de nuevo.");
                return;
            }

            var previousStatus = request.GetAttributeValue<OptionSetValue>(Schema.ApprovalRequest.ApprovalStatus)?.Value;
            var creatorId = request.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id ?? Guid.Empty;
            var requestType = request.GetAttributeValue<string>(Schema.ApprovalRequest.RequestType);
            var resourcesJson = request.GetAttributeValue<string>(SupervisorMirrors.ApprovalRequest.RequestResources);

            // ---- 2. Autorizacion, ANTES de tocar nada ----
            var isApprover = false;
            if (previousStatus == null && creatorId != Guid.Empty && creatorId != actor)
            {
                try
                {
                    isApprover = ApproverTeamLookup.IsApprover(system, tracing, actor);
                }
                catch (InvalidPluginExecutionException ex)
                {
                    Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked, ex.Message);
                    return;
                }
            }

            var verdict = ReceptionGuard.EvaluateApprovalUpdate(
                previousStatus, decisionInTarget: true, creatorId, actor, isApprover);

            tracing.Trace("ResolveApproval: prev={0} creador={1} aprobador={2} => permitido={3}.",
                previousStatus?.ToString() ?? "null", creatorId, isApprover, verdict.IsAllowed);

            if (!verdict.IsAllowed)
            {
                Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked, verdict.Reason!);
                return;
            }

            if (!approve && string.IsNullOrWhiteSpace(reason))
            {
                Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked,
                    "Un rechazo exige motivo. Escriba por que se rechaza y vuelva a enviarlo.");
                return;
            }

            // ---- 3. Que efecto corresponde ----
            if (!ApprovalEffectRules.TryGetEffect(requestType, approve, out var effect))
            {
                Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked,
                    $"Tipo de solicitud desconocido: '{requestType}'. No se ejecuto nada.");
                return;
            }

            var resources = new RequestResources(resourcesJson);

            // ---- 4. Efecto + marcado, en la MISMA transaccion ----
            //      Si el efecto lanza, el marcado no ocurre. Si el marcado lanza, la
            //      plataforma revierte el efecto. No hay medio estado posible.
            string note;
            try
            {
                note = ApplyEffect(system, asActor, tracing, effect, resources, actor,
                    sinDeclaracion: string.Equals(
                        requestType, ApprovalEffectRules.TipoTulaRecontadaSinDeclaracion,
                        StringComparison.Ordinal));
            }
            catch (EffectRefusedException ex)
            {
                // Rechazo de NEGOCIO. Por el invariante de arriba llegamos aca con cero
                // escrituras, asi que devolverlo como dato es seguro: no hay nada que
                // revertir y el motivo le llega intacto al supervisor.
                tracing.Trace("ResolveApproval: efecto rechazado — {0}", ex.Message);
                Respond(context, tracing, Schema.ResolveApprovalApi.OutcomeBlocked, ex.Message);
                return;
            }

            asActor.Update(new Entity(Schema.ApprovalRequest.LogicalName, requestId)
            {
                [Schema.ApprovalRequest.ApprovalStatus] = new OptionSetValue(
                    approve ? ApprovalStatusAprobado : ApprovalStatusRechazado),
                [Schema.ApprovalRequest.SolvedBy] = new EntityReference(Schema.SystemUser.LogicalName, actor),
                [Schema.ApprovalRequest.ResolutionDatetime] = DateTime.UtcNow,
                [Schema.ApprovalRequest.StatusReason] = reason ?? string.Empty,
            });

            var outcome = approve
                ? Schema.ResolveApprovalApi.OutcomeApproved
                : Schema.ResolveApprovalApi.OutcomeRejected;

            Respond(context, tracing, outcome, note);
        }

        // sanic_approvalstatus: 159460000 Aprobado, 159460001 Rechazado (verificado
        // contra el option set de la tabla; solo existen esos dos valores).
        private const int ApprovalStatusAprobado = 159460000;
        private const int ApprovalStatusRechazado = 159460001;

        // =====================================================================
        // Efectos
        // =====================================================================

        /// <summary>
        /// Aplica el efecto. Recibe DOS servicios porque no todas las escrituras van
        /// con la misma identidad, y la diferencia no es un detalle:
        ///
        ///   system  — tulas, planillas y bolsas declaradas. Sus guards son maquinas de
        ///             estado (¿la transicion es legal?, ¿el lote esta abierto?), no
        ///             preguntan QUIEN escribe. Va como sistema para no exigirle al
        ///             supervisor privilegios de escritura en media base.
        ///
        ///   asActor — la SESION de recuento. GuardRecountingPlugin si pregunta quien
        ///             actua: solo el cajero dueno, o un aprobador. Como sistema, el
        ///             guard ve al usuario SYSTEM — ni dueno ni aprobador — y bloquea
        ///             con "Solo el cajero dueno de la sesion puede darla de baja".
        ///             Verificado contra el entorno: eso es exactamente lo que pasaba.
        ///
        /// REGLA: donde un guard evalua QUIEN actua, la escritura tiene que llevar al
        /// actor real. Mandar sistema ahi no es "mas permisivo", es DENEGAR de mas.
        /// </summary>
        private static string ApplyEffect(
            IOrganizationService system, IOrganizationService asActor, ITracingService tracing,
            ApprovalEffect effect, RequestResources res, Guid actor, bool sinDeclaracion)
        {
            switch (effect)
            {
                case ApprovalEffect.Ninguno:
                    return "Resuelta. No habia efecto que aplicar.";

                case ApprovalEffect.RemapearTula:
                    return RemapearTula(system, tracing, res);

                case ApprovalEffect.AltaTulaEnBoveda:
                    return AltaTulaEnBoveda(system, res);

                case ApprovalEffect.EliminarTula:
                    return EliminarTula(system, res);

                case ApprovalEffect.RenombrarPlanilla:
                    return RenombrarPlanilla(system, res);

                case ApprovalEffect.RenombrarTula:
                    return RenombrarTula(system, res);

                case ApprovalEffect.ConfirmarSesion:
                    return ConfirmarSesion(system, asActor, res, actor);

                case ApprovalEffect.EliminarSesion:
                    return EliminarSesion(system, asActor, res);

                case ApprovalEffect.AsignarTulaConDeclaracion:
                    return AsignarTulaConDeclaracion(system, res);

                case ApprovalEffect.AsignarTulaSinDeclaracion:
                    return AsignarTulaSinDeclaracion(system, res, actor);

                case ApprovalEffect.LiberarTulaABoveda:
                    return LiberarTulaABoveda(system, res);

                case ApprovalEffect.RestituirTula:
                    return RestituirTula(system, res, fallbackABoveda: false);

                case ApprovalEffect.RestituirTulaOBoveda:
                    return RestituirTula(system, res, fallbackABoveda: true);

                case ApprovalEffect.CerrarRecuentoConAsientos:
                    return CerrarRecuentoConAsientos(system, tracing, res, sinDeclaracion, actor);

                case ApprovalEffect.DevolverTulaAlCajero:
                    return DevolverTulaAlCajero(system, res);

                default:
                    throw new InvalidPluginExecutionException(
                        $"ResolveApproval: efecto '{effect}' sin implementacion.");
            }
        }

        private static string RemapearTula(IOrganizationService system, ITracingService tracing, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("GUID"), "la tula");
            var receptionId = res.Guid("ReceiptGUID");
            if (receptionId == Guid.Empty || !Exists(system, Schema.Reception.LogicalName, receptionId))
            {
                throw new EffectRefusedException("No se encontro la planilla destino del remapeo.");
            }

            var update = new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.ReceptionLookup] = new EntityReference(Schema.Reception.LogicalName, receptionId)
            };

            // Guarda portada de la app: si el estado previo no resuelve, NO se escribe
            // BagStatus. Estampar un estado inventado sobre una tula es peor que dejarla
            // como esta y avisar.
            if (ApprovalEffectRules.TryResolveBagPreviousState(res.Text("BagPreviousState"), out var previo))
            {
                update[Schema.Bag.BagStatus] = new OptionSetValue(previo);
                system.Update(update);
                return "Tula remapeada.";
            }

            tracing.Trace("ResolveApproval: BagPreviousState ilegible, no se escribe BagStatus.");
            system.Update(update);
            return "Tula remapeada. El estado previo no pudo resolverse — revisalo a mano.";
        }

        private static string AltaTulaEnBoveda(IOrganizationService system, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula pendiente");
            var nuevo = res.Text("BagID");

            EnsureNoBagNumberCollision(system, res.Guid("ReceptionBatchGUID"), nuevo, bag);

            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAlmacenadaEnBoveda)
            });
            return "Tula dada de alta en boveda.";
        }

        private static string EliminarTula(IOrganizationService system, RequestResources res)
        {
            // "Eliminar tula" trae GUID; "Agregar tula" (rechazo) trae BagGUID.
            var bagId = res.Guid("GUID");
            if (bagId == Guid.Empty) bagId = res.Guid("BagGUID");

            var bag = RequireBag(system, bagId, "la tula");
            system.Delete(Schema.Bag.LogicalName, bag);
            return "Tula eliminada.";
        }

        private static string RenombrarPlanilla(IOrganizationService system, RequestResources res)
        {
            var receptionId = res.Guid("ReceiptGUID");
            var reception = RequireRecord(
                system, Schema.Reception.LogicalName, receptionId, "la planilla",
                new ColumnSet(Schema.Reception.BatchLookup));

            var nuevo = res.Text("NewReceiptID");
            if (string.IsNullOrWhiteSpace(nuevo))
            {
                throw new EffectRefusedException(
                    "La solicitud no trae el nuevo numero de planilla (NewReceiptID).");
            }

            // ARREGLO respecto de la version Power Fx: alli se comparaba el LOTE de cada
            // planilla contra el ID de LA planilla — identificadores de tablas distintas,
            // jamas coincidian, y el dup-check no disparaba nunca. Ademas no se excluia
            // a si misma. Ahora el alcance es el lote REAL de la planilla.
            var batch = reception.GetAttributeValue<EntityReference>(Schema.Reception.BatchLookup);
            if (batch != null)
            {
                var scope = new List<(Guid, string?)>();
                var query = new QueryExpression(Schema.Reception.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.Reception.ReceiptId)
                };
                query.Criteria.AddCondition(Schema.Reception.BatchLookup, ConditionOperator.Equal, batch.Id);
                foreach (var row in system.RetrieveMultiple(query).Entities)
                {
                    scope.Add((row.Id, row.GetAttributeValue<string>(Schema.Reception.ReceiptId)));
                }

                if (ApprovalEffectRules.HasNumberCollision(scope, nuevo, receptionId))
                {
                    throw new EffectRefusedException(
                        $"El numero de planilla '{nuevo}' ya existe en el mismo lote de recepcion.");
                }
            }

            system.Update(new Entity(Schema.Reception.LogicalName, receptionId)
            {
                [Schema.Reception.ReceiptId] = nuevo,
                [Schema.Reception.Name] = nuevo,
            });
            return "Numero de planilla actualizado.";
        }

        private static string RenombrarTula(IOrganizationService system, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula");
            var nuevo = res.Text("BagID");

            EnsureNoBagNumberCollision(system, res.Guid("ReceptionBatchGUID"), nuevo, bag);

            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.Name] = nuevo,
                [Schema.Bag.BagId] = nuevo,
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAlmacenadaEnBoveda),
            });
            return "Numero de tula actualizado.";
        }

        private static string ConfirmarSesion(
            IOrganizationService system, IOrganizationService asActor, RequestResources res, Guid actor)
        {
            // Se LEE como sistema (el aprobador puede no alcanzar a ver la sesion) y se
            // ESCRIBE como el actor, porque el guard de la sesion evalua quien actua.
            var sessionId = RequireRecord(
                system, Schema.RecountingSession.LogicalName, res.Guid("RecountingSessionGUID"),
                "la sesion de recuento", new ColumnSet(Schema.RecountingSession.Id)).Id;

            asActor.Update(new Entity(Schema.RecountingSession.LogicalName, sessionId)
            {
                [Schema.RecountingSession.Confirmed] = true,
                [Schema.RecountingSession.ConfirmedBy] = new EntityReference(Schema.SystemUser.LogicalName, actor),
            });
            return "Sesion de recuento confirmada.";
        }

        private static string EliminarSesion(
            IOrganizationService system, IOrganizationService asActor, RequestResources res)
        {
            var sessionId = RequireRecord(
                system, Schema.RecountingSession.LogicalName, res.Guid("RecountingSessionGUID"),
                "la sesion de recuento", new ColumnSet(Schema.RecountingSession.Id)).Id;

            // La baja va COMO EL ACTOR. GuardRecountingPlugin.EvaluateSessionDelete
            // pregunta si quien borra es el cajero dueno o un aprobador (P0.1); con el
            // servicio de sistema veria al usuario SYSTEM y bloquearia una baja legitima.
            asActor.Delete(Schema.RecountingSession.LogicalName, sessionId);
            return "Sesion de recuento rechazada y eliminada.";
        }

        private static string AsignarTulaConDeclaracion(IOrganizationService system, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula");
            var sessionId = RequireRecord(
                system, Schema.RecountingSession.LogicalName, res.Guid("RecountingSessionGUID"),
                "la sesion de recuento", new ColumnSet(Schema.RecountingSession.Id)).Id;

            var update = new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAsignadaACajero),
                [Schema.Bag.RecountingSessionLookup] =
                    new EntityReference(Schema.RecountingSession.LogicalName, sessionId),
            };

            var declaredId = res.Guid("DeclarationBagGUID");
            if (declaredId == Guid.Empty || !Exists(system, Schema.DeclaredBag.LogicalName, declaredId))
            {
                // Guarda portada: no se escribe el lookup en blanco.
                system.Update(update);
                return "Tula asignada al cajero. No se pudo enlazar la bolsa declarada — revisalo a mano.";
            }

            update[Schema.Bag.DeclaredBagLookup] =
                new EntityReference(Schema.DeclaredBag.LogicalName, declaredId);
            system.Update(update);

            system.Update(new Entity(Schema.DeclaredBag.LogicalName, declaredId)
            {
                [Schema.DeclaredBag.BagLookup] = new EntityReference(Schema.Bag.LogicalName, bag)
            });
            return "Tula asignada al cajero y enlazada a su declaracion.";
        }

        private static string AsignarTulaSinDeclaracion(
            IOrganizationService system, RequestResources res, Guid actor)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula");
            var sessionId = RequireRecord(
                system, Schema.RecountingSession.LogicalName, res.Guid("RecountingSessionGUID"),
                "la sesion de recuento", new ColumnSet(Schema.RecountingSession.Id)).Id;

            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAsignadaACajero),
                [Schema.Bag.RecountingSessionLookup] =
                    new EntityReference(Schema.RecountingSession.LogicalName, sessionId),
            });

            system.Create(new Entity(Schema.RecountingSessionDetail.LogicalName)
            {
                [Schema.RecountingSessionDetail.SessionLookup] =
                    new EntityReference(Schema.RecountingSession.LogicalName, sessionId),
                [Schema.RecountingSessionDetail.BagLookup] =
                    new EntityReference(Schema.Bag.LogicalName, bag),
                [Schema.RecountingSessionDetail.SupervisorLookup] =
                    new EntityReference(Schema.SystemUser.LogicalName, actor),
            });
            return "Tula sin declaracion asignada al cajero.";
        }

        private static string LiberarTulaABoveda(IOrganizationService system, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula");

            // Devolver a boveda Y DESENLAZAR. Sin el desenlace la tula queda apuntando
            // a una sesion que ya no la cuenta: invisible para su cajero y no escaneable
            // por otro (la rama de escaneo exige "Almacenada en boveda").
            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAlmacenadaEnBoveda),
                [Schema.Bag.RecountingSessionLookup] = null,
            });
            return "Tula devuelta a boveda y liberada de la sesion.";
        }

        private static string RestituirTula(
            IOrganizationService system, RequestResources res, bool fallbackABoveda)
        {
            var bagId = res.Guid("GUID");
            if (bagId == Guid.Empty) bagId = res.Guid("BagGUID");
            var bag = RequireBag(system, bagId, "la tula");

            if (ApprovalEffectRules.TryResolveBagPreviousState(res.Text("BagPreviousState"), out var previo))
            {
                system.Update(new Entity(Schema.Bag.LogicalName, bag)
                {
                    [Schema.Bag.BagStatus] = new OptionSetValue(previo)
                });
                return "Rechazada. Tula restituida a su estado previo.";
            }

            if (!fallbackABoveda)
            {
                // Sin estado previo legible NO se escribe: la tula queda como esta y se
                // avisa. Es la guarda de la app, portada tal cual.
                return "Rechazada, pero el estado previo no pudo resolverse: la tula quedo como estaba. Corregilo a mano.";
            }

            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAlmacenadaEnBoveda)
            });
            return "Rechazada. El contrato de este tipo no trae estado previo: la tula se devolvio a boveda. Verifica que sea el estado correcto.";
        }

        // =====================================================================
        // Fase 2 — cierres que generan asientos contables
        // =====================================================================

        /// <summary>
        /// Cierre del recuento: genera los asientos, marca los cheques, deja la tula
        /// Recontada y cierra el detalle de sesion.
        ///
        /// QUE asientos van lo decide <see cref="RecountClosureAccounting"/>, que esta
        /// bajo test — incluida la regla que mas importa: la diferencia se calcula POR
        /// COMPONENTE, nunca por el neto. Aca solo hay lecturas y escrituras.
        ///
        /// Todo esto corre dentro de la transaccion de la CAPI: si el ultimo Update
        /// falla, no queda ni un asiento suelto. La version Power Fx no podia prometer
        /// eso — su propio codigo tenia una rama "la solicitud quedo APROBADA pero los
        /// efectos fallaron: revisa la tula antes de reintentar".
        /// </summary>
        private static string CerrarRecuentoConAsientos(
            IOrganizationService system, ITracingService tracing,
            RequestResources res, bool sinDeclaracion, Guid actor)
        {
            var bagRef = RequireRecord(
                system, Schema.Bag.LogicalName, res.Guid("BagGUID"), "la tula",
                new ColumnSet(Schema.Bag.BagId, Schema.Bag.DeclaredBagLookup,
                              Schema.Bag.RecountingSessionLookup));

            var bagId = bagRef.Id;
            var bagNumber = bagRef.GetAttributeValue<string>(Schema.Bag.BagId);

            var recounted = ReadRecountedDeposits(system, bagId);

            IReadOnlyList<LedgerEntry> entries;
            int detailStatus;
            string note;

            if (sinDeclaracion)
            {
                entries = RecountClosureAccounting.ForRecountWithoutDeclaration(bagNumber, recounted);
                detailStatus = Schema.RecountingSessionDetail.StatusCerrado;
                note = entries.Count > 0
                    ? $"Recuento sin declaracion cerrado: {entries.Count} asientos generados."
                    : "Recuento sin declaracion cerrado. La tula no tenia depositos recontados: cero asientos.";
            }
            else
            {
                var declaredBag = bagRef.GetAttributeValue<EntityReference>(Schema.Bag.DeclaredBagLookup);
                var declared = ReadDeclaredDeposits(system, declaredBag?.Id ?? Guid.Empty);
                entries = RecountClosureAccounting.ForBagWithDifferences(bagNumber, recounted, declared);
                detailStatus = Schema.RecountingSessionDetail.StatusCerradoConDiferencias;
                note = $"Recuento cerrado con diferencias: {entries.Count} asientos generados.";
            }

            tracing.Trace("ResolveApproval: tula {0} ({1}) => {2} asientos.",
                bagId, bagNumber ?? "(sin numero)", entries.Count);

            // VALIDACION ANTES DE ESCRIBIR NADA. La referencia viaja al core, que no
            // acepta mas de 30 caracteres. No se trunca: recortar el numero de tula
            // mandaria una referencia equivocada, y dos tulas distintas podrian
            // colapsar en la misma. Mejor bloquear el cierre con un motivo claro —
            // un cierre bloqueado se reintenta, un asiento mal referenciado ya viajo.
            foreach (var entry in entries)
            {
                if (RecountClosureAccounting.ExceedsCoreLimit(entry.Reference))
                {
                    throw new EffectRefusedException(
                        $"La referencia '{entry.Reference}' tiene {entry.Reference.Length} caracteres y el core " +
                        $"acepta {RecountClosureAccounting.CoreReferenceMaxLength}. " +
                        $"El numero de tula '{bagNumber}' es demasiado largo para este tipo de asiento. " +
                        "No se genero ningun asiento.");
                }
            }

            // El cajero sale de la SESION de la tula; el supervisor, de quien esta
            // resolviendo la solicitud AHORA. Son dos personas distintas y el asiento
            // tiene que poder nombrarlas a las dos.
            var cajeroDeLaTula = ReadBagCashier(system, tracing, bagId);
            foreach (var entry in entries)
            {
                system.Create(ToTransaction(entry, cajeroDeLaTula, actor));
            }

            MarcarChequesRecontados(system, tracing, bagId);

            system.Update(new Entity(Schema.Bag.LogicalName, bagId)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagRecontada)
            });

            var session = bagRef.GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup);
            return note + CerrarDetalleDeSesion(system, tracing, bagId, session?.Id ?? Guid.Empty, detailStatus);
        }

        /// <summary>
        /// Rechazo del cierre: la tula vuelve al cajero para que rehaga el recuento.
        /// NO se genera ningun asiento — es el punto entero del rechazo.
        /// </summary>
        private static string DevolverTulaAlCajero(IOrganizationService system, RequestResources res)
        {
            var bag = RequireBag(system, res.Guid("BagGUID"), "la tula");

            system.Update(new Entity(Schema.Bag.LogicalName, bag)
            {
                [Schema.Bag.BagStatus] = new OptionSetValue(ReceptionGuard.BagAsignadaACajero)
            });

            return "Recuento rechazado: la tula vuelve al cajero. No se genero ningun asiento.";
        }

        private static List<RecountedDeposit> ReadRecountedDeposits(
            IOrganizationService system, Guid bagId)
        {
            var query = new QueryExpression(Schema.DepositRecounting.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.DepositRecounting.AccountNumber, Schema.DepositRecounting.Currency,
                    Schema.DepositRecounting.CashAmount, Schema.DepositRecounting.BacCheckAmount,
                    Schema.DepositRecounting.OtherCheckAmount, Schema.DepositRecounting.TransactionNumber)
            };
            query.Criteria.AddCondition(
                Schema.DepositRecounting.BagLookup, ConditionOperator.Equal, bagId);

            var rows = new List<RecountedDeposit>();
            foreach (var e in system.RetrieveMultiple(query).Entities)
            {
                rows.Add(new RecountedDeposit(
                    e.Id,
                    e.GetAttributeValue<int?>(Schema.DepositRecounting.AccountNumber),
                    e.GetAttributeValue<OptionSetValue>(Schema.DepositRecounting.Currency)?.Value,
                    Amount(e, Schema.DepositRecounting.CashAmount),
                    Amount(e, Schema.DepositRecounting.BacCheckAmount),
                    Amount(e, Schema.DepositRecounting.OtherCheckAmount),
                    e.GetAttributeValue<int?>(Schema.DepositRecounting.TransactionNumber)));
            }

            return rows;
        }

        private static List<DeclaredDeposit> ReadDeclaredDeposits(
            IOrganizationService system, Guid declaredBagId)
        {
            var rows = new List<DeclaredDeposit>();
            if (declaredBagId == Guid.Empty)
            {
                // Sin bolsa declarada no hay contra que comparar; todo lo contado sale
                // como sobrante. Es lo mismo que hacia la app (Clear(colDecApr)).
                return rows;
            }

            var query = new QueryExpression(Schema.DeclaredDeposit.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.DeclaredDeposit.AccountNumber, Schema.DeclaredDeposit.Currency,
                    Schema.DeclaredDeposit.CashAmount, Schema.DeclaredDeposit.BacCheckAmount,
                    Schema.DeclaredDeposit.OtherCheckAmount, Schema.DeclaredDeposit.TransactionNumber,
                    Schema.DeclaredDeposit.DepositReference, Schema.DeclaredDeposit.RelatedRecountedDeposit)
            };
            query.Criteria.AddCondition(
                Schema.DeclaredDeposit.DeclaredBagLookup, ConditionOperator.Equal, declaredBagId);

            foreach (var e in system.RetrieveMultiple(query).Entities)
            {
                var link = e.GetAttributeValue<EntityReference>(
                    Schema.DeclaredDeposit.RelatedRecountedDeposit);

                rows.Add(new DeclaredDeposit(
                    e.Id,
                    e.GetAttributeValue<int?>(Schema.DeclaredDeposit.AccountNumber),
                    e.GetAttributeValue<OptionSetValue>(Schema.DeclaredDeposit.Currency)?.Value,
                    Amount(e, Schema.DeclaredDeposit.CashAmount),
                    Amount(e, Schema.DeclaredDeposit.BacCheckAmount),
                    Amount(e, Schema.DeclaredDeposit.OtherCheckAmount),
                    e.GetAttributeValue<int?>(Schema.DeclaredDeposit.TransactionNumber),
                    e.GetAttributeValue<string>(Schema.DeclaredDeposit.DepositReference),
                    link?.Id));
            }

            return rows;
        }

        /// <summary>
        /// Un asiento nacido de una APROBACION lleva las dos firmas: quien conto la
        /// plata y quien autorizo el asiento. Un sobrante, un faltante o una tula
        /// recontada sin declaracion existen porque un supervisor los firmo, y esa
        /// firma es parte del asiento, no de una bitacora aparte.
        /// </summary>
        private static Entity ToTransaction(LedgerEntry entry, Guid? cajero, Guid supervisor)
        {
            var tx = new Entity(Schema.Transaction.LogicalName)
            {
                // OJO: sanic_accountnumber es Integer en los depositos pero Decimal en
                // transacciones. La conversion es explicita a proposito; sin ella la
                // plataforma responde "Unable to cast Int32 to String" y no dice donde.
                [Schema.Transaction.CashAmount] = entry.CashAmount,
                [Schema.Transaction.BacCheckAmount] = entry.BacCheckAmount,
                [Schema.Transaction.OtherCheckAmount] = entry.OtherCheckAmount,
                [Schema.Transaction.TransactionReference] = entry.Reference,
                [Schema.Transaction.TransactionType] = new OptionSetValue((int)entry.Type),
                [Schema.Transaction.ProcessingType] = new OptionSetValue((int)entry.Processing),
            };

            if (cajero != null)
            {
                tx[Schema.Transaction.Cajero] =
                    new EntityReference(Schema.SystemUser.LogicalName, cajero.Value);
            }

            if (supervisor != Guid.Empty)
            {
                tx[Schema.Transaction.SupervisorAutoriza] =
                    new EntityReference(Schema.SystemUser.LogicalName, supervisor);
            }

            if (entry.AccountNumber.HasValue)
            {
                tx[Schema.Transaction.AccountNumber] = (decimal)entry.AccountNumber.Value;
            }

            if (entry.TransactionNumber.HasValue)
            {
                tx[Schema.Transaction.TransactionNumber] = entry.TransactionNumber.Value;
            }

            if (entry.Currency.HasValue)
            {
                tx[Schema.Transaction.Currency] = new OptionSetValue(entry.Currency.Value);
            }

            // Sin recuento de origen el lookup queda VACIO, nunca apuntando a cualquiera.
            if (entry.RelatedRecountedDepositId is Guid rec && rec != Guid.Empty)
            {
                tx[Schema.Transaction.RelatedDepositRecounting] =
                    new EntityReference(Schema.DepositRecounting.LogicalName, rec);
            }

            return tx;
        }

        /// <summary>
        /// Estampa Recontado en los cheques de la tula que TODAVIA NO arrancaron el
        /// handshake (estado vacio).
        ///
        /// El filtro por estado nulo no es una optimizacion: CheckStatusStampPlugin
        /// impone el ciclo Recontado -> 'Enviado a digitacion' -> Recibido/'No Recibido',
        /// asi que re-estampar un cheque que ya avanzo lo hace RETROCEDER y el guard
        /// aborta la operacion entera. La app lo hacia sin condicion — con una tula
        /// cuyos cheques ya estaban en digitacion, el cierre entero se caia.
        /// </summary>
        private static void MarcarChequesRecontados(
            IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            var query = new QueryExpression(Schema.Check.LogicalName)
            {
                ColumnSet = new ColumnSet(false)
            };
            query.Criteria.AddCondition(Schema.Check.BagLookup, ConditionOperator.Equal, bagId);
            query.Criteria.AddCondition(Schema.Check.CheckStatus, ConditionOperator.Null);

            var cheques = system.RetrieveMultiple(query).Entities;
            foreach (var c in cheques)
            {
                system.Update(new Entity(Schema.Check.LogicalName, c.Id)
                {
                    [Schema.Check.CheckStatus] = new OptionSetValue(Schema.Check.CheckStatusRecontado)
                });
            }

            tracing.Trace("ResolveApproval: {0} cheques sin estado marcados Recontado.", cheques.Count);
        }

        /// <summary>
        /// Cierra el detalle de sesion de la tula. Si no lo encuentra NO revienta: los
        /// asientos ya son correctos y volarlos por un detalle faltante seria peor. Se
        /// devuelve el aviso pegado a la nota para que el supervisor lo vea, igual que
        /// hacia la app.
        /// </summary>
        private static string CerrarDetalleDeSesion(
            IOrganizationService system, ITracingService tracing,
            Guid bagId, Guid sessionId, int status)
        {
            if (sessionId == Guid.Empty)
            {
                tracing.Trace("ResolveApproval: la tula {0} no tiene sesion de recuento.", bagId);
                return " La tula no tiene sesion de recuento: cerra el detalle a mano.";
            }

            var query = new QueryExpression(Schema.RecountingSessionDetail.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(
                Schema.RecountingSessionDetail.BagLookup, ConditionOperator.Equal, bagId);
            query.Criteria.AddCondition(
                Schema.RecountingSessionDetail.SessionLookup, ConditionOperator.Equal, sessionId);

            var detalle = system.RetrieveMultiple(query).Entities;
            if (detalle.Count == 0)
            {
                tracing.Trace("ResolveApproval: sin detalle de sesion para tula {0}/sesion {1}.",
                    bagId, sessionId);
                return " Asientos generados, pero la tula no tiene detalle de sesion: cerralo a mano.";
            }

            system.Update(new Entity(Schema.RecountingSessionDetail.LogicalName, detalle[0].Id)
            {
                [Schema.RecountingSessionDetail.Status] = new OptionSetValue(status)
            });

            return string.Empty;
        }

        /// <summary>
        /// Los montos de estas tablas son Decimal en Dataverse, NO Money — verificado
        /// contra la metadata. Leerlos como Money devuelve null en silencio y habria
        /// escrito CEROS en todos los asientos sin fallar ni una vez.
        /// </summary>
        private static decimal Amount(Entity e, string field) =>
            e.GetAttributeValue<decimal?>(field) ?? 0m;

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Rechazo de NEGOCIO de un efecto: la solicitud no se puede materializar
        /// (numero duplicado, registro que ya no existe, contrato incompleto).
        ///
        /// Es un tipo propio y no <see cref="InvalidPluginExecutionException"/> a
        /// proposito. Un rechazo de negocio se responde como DATO — si viajara como
        /// fault, Power Fx descartaria el mensaje y el supervisor veria el generico
        /// "Network error", que es justo el bug que esta CAPI vino a matar. En cambio
        /// un fault que sale de una ESCRITURA (un guard anidado que bloquea, un error
        /// de plataforma) tiene que seguir propagando para que la plataforma revierta:
        /// ahi ya hay escrituras en juego y tragarselo commitearia un estado parcial.
        ///
        /// INVARIANTE que hace segura esa distincion: los efectos validan TODO antes
        /// de escribir NADA. Al lanzar esto no hay ninguna escritura hecha. Si algun
        /// efecto futuro rompe ese orden, el catch de arriba pasa a commitear estado
        /// parcial — mantener el orden validar-y-despues-escribir NO es opcional.
        /// </summary>
        private sealed class EffectRefusedException : Exception
        {
            public EffectRefusedException(string message) : base(message) { }
        }

        private static void EnsureNoBagNumberCollision(
            IOrganizationService system, Guid batchId, string? nuevo, Guid selfBagId)
        {
            if (batchId == Guid.Empty || string.IsNullOrWhiteSpace(nuevo))
            {
                return;
            }

            var scope = new List<(Guid, string?)>();
            var query = new QueryExpression(Schema.Bag.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Bag.BagId)
            };
            query.Criteria.AddCondition(Schema.Bag.ReceptionBatchLookup, ConditionOperator.Equal, batchId);
            foreach (var row in system.RetrieveMultiple(query).Entities)
            {
                scope.Add((row.Id, row.GetAttributeValue<string>(Schema.Bag.BagId)));
            }

            if (ApprovalEffectRules.HasNumberCollision(scope, nuevo, selfBagId))
            {
                throw new EffectRefusedException(
                    $"El numero de tula '{nuevo}' ya existe en otra planilla del mismo lote.");
            }
        }

        private static Guid RequireBag(IOrganizationService system, Guid id, string label) =>
            RequireRecord(system, Schema.Bag.LogicalName, id, label, new ColumnSet(Schema.Bag.Id)).Id;

        private static Entity RequireRecord(
            IOrganizationService system, string logicalName, Guid id, string label, ColumnSet columns)
        {
            if (id == Guid.Empty)
            {
                throw new EffectRefusedException(
                    $"La solicitud no identifica {label}; no se puede aplicar el efecto.");
            }

            try
            {
                return system.Retrieve(logicalName, id, columns);
            }
            catch (Exception)
            {
                throw new EffectRefusedException($"Ya no existe {label} de la solicitud.");
            }
        }

        private static bool Exists(IOrganizationService system, string logicalName, Guid id)
        {
            if (id == Guid.Empty) return false;
            try
            {
                system.Retrieve(logicalName, id, new ColumnSet(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // =====================================================================
        // Entrada / salida
        // =====================================================================

        private static Guid ReadGuid(IPluginExecutionContext context, string key)
        {
            if (context.InputParameters.TryGetValue(key, out var raw))
            {
                if (raw is Guid g && g != Guid.Empty) return g;
                if (raw is string s && Guid.TryParse(s, out var p) && p != Guid.Empty) return p;
            }

            throw new InvalidPluginExecutionException(
                $"ResolveApproval: '{key}' ausente o no es un identificador valido.");
        }

        private static bool ReadDecision(IPluginExecutionContext context)
        {
            var raw = ReadOptionalString(context, Schema.ResolveApprovalApi.InDecision);

            if (string.Equals(raw, Schema.ResolveApprovalApi.DecisionApprove, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(raw, Schema.ResolveApprovalApi.DecisionReject, StringComparison.OrdinalIgnoreCase))
                return false;

            throw new InvalidPluginExecutionException(
                $"ResolveApproval: 'Decision' debe ser '{Schema.ResolveApprovalApi.DecisionApprove}' o " +
                $"'{Schema.ResolveApprovalApi.DecisionReject}'; llego '{raw}'.");
        }

        private static string? ReadOptionalString(IPluginExecutionContext context, string key) =>
            context.InputParameters.TryGetValue(key, out var raw) ? raw as string : null;

        private static void Respond(
            IPluginExecutionContext context, ITracingService tracing, string outcome, string message)
        {
            context.OutputParameters[Schema.ResolveApprovalApi.OutOutcome] = outcome;
            context.OutputParameters[Schema.ResolveApprovalApi.OutMessage] = message ?? string.Empty;
            tracing.Trace("ResolveApproval: Outcome={0} Message='{1}'.", outcome, message);
        }

        /// <summary>Lectura tolerante del RequestResources. Un blob ilegible es un dato, no una excepcion.</summary>
        private sealed class RequestResources
        {
            private readonly string? _json;

            public RequestResources(string? json) => _json = json;

            public string? Text(string key) => FlatJson.Value(_json, key);

            public Guid Guid(string key) =>
                System.Guid.TryParse(Text(key), out var g) ? g : System.Guid.Empty;
        }

        /// <summary>Cajero dueño de la sesion de la tula. null si no se puede resolver.</summary>
        private static Guid? ReadBagCashier(
            IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            try
            {
                var bag = system.Retrieve(Schema.Bag.LogicalName, bagId,
                    new ColumnSet(Schema.Bag.RecountingSessionLookup));
                var session = bag.GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup);
                if (session == null)
                {
                    return null;
                }

                var ses = system.Retrieve(Schema.RecountingSession.LogicalName, session.Id,
                    new ColumnSet(Schema.RecountingSession.Cashier));
                return ses.GetAttributeValue<EntityReference>(Schema.RecountingSession.Cashier)?.Id;
            }
            catch (Exception ex)
            {
                tracing.Trace("ResolveApproval: no se pudo resolver el cajero de la tula {0}: {1}",
                    bagId, ex.Message);
                return null;
            }
        }
    }
}
