using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Pertenencia de la sesion de recuento referenciada por una solicitud
    /// "Crear sesion de recuento" respecto de quien la esta creando.
    /// La resuelve el plugin leyendo la sesion COMO SISTEMA (no por texto del JSON).
    /// </summary>
    public enum RecountingSessionOwnership
    {
        /// <summary>No hace falta resolverla: la solicitud no invoca la excepcion.</summary>
        NotApplicable = 0,

        /// <summary>La sesion existe y su cajero (sanic_cashier) ES el solicitante.</summary>
        OwnedByRequester = 1,

        /// <summary>La sesion existe y su cajero es OTRO usuario.</summary>
        OwnedByOtherUser = 2,

        /// <summary>
        /// No se pudo resolver: JSON ilegible, sin RecountingSessionGUID, GUID invalido,
        /// sesion inexistente o sesion sin cajero.
        /// </summary>
        Unresolved = 3
    }

    /// <summary>Que hacer con un Create de solicitud de aprobacion.</summary>
    public enum ApprovalCreateDecision
    {
        /// <summary>Se crea tal cual viene.</summary>
        Allow = 0,

        /// <summary>
        /// Se crea, pero PENDIENTE: el plugin borra la decision del Target.
        /// No se bloquea porque la solicitud en si es legitima; lo ilegitimo es que nazca resuelta.
        /// </summary>
        DegradeToPending = 1,

        /// <summary>Se rechaza la creacion.</summary>
        Block = 2
    }

    /// <summary>Resultado de evaluar el Create de una solicitud de aprobacion.</summary>
    public sealed class ApprovalCreateEvaluation
    {
        private ApprovalCreateEvaluation(ApprovalCreateDecision decision, string? reason)
        {
            Decision = decision;
            Reason = reason;
        }

        public ApprovalCreateDecision Decision { get; }

        /// <summary>Motivo (para el usuario si bloquea, para la traza si degrada). null cuando permite.</summary>
        public string? Reason { get; }

        public bool IsAllowed => Decision != ApprovalCreateDecision.Block;

        public static ApprovalCreateEvaluation Allow() =>
            new ApprovalCreateEvaluation(ApprovalCreateDecision.Allow, null);

        public static ApprovalCreateEvaluation DegradeToPending(string reason) =>
            new ApprovalCreateEvaluation(ApprovalCreateDecision.DegradeToPending, reason);

        public static ApprovalCreateEvaluation Block(string reason) =>
            new ApprovalCreateEvaluation(ApprovalCreateDecision.Block, reason);
    }

    /// <summary>Resultado de evaluar una operacion del dominio de recepcion.</summary>
    public sealed class ReceptionGuardResult
    {
        private ReceptionGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si la operacion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        public static ReceptionGuardResult Allow() => new ReceptionGuardResult(true, null);

        public static ReceptionGuardResult Block(string reason) => new ReceptionGuardResult(false, reason);
    }

    /// <summary>
    /// Maquina de estados del dominio de recepcion (lotes, planillas, tulas y solicitudes
    /// de aprobacion de esclusa/recuento).
    ///
    /// Las apps canvas condicionan botones y ramas, pero eso es cosmetico: cualquier usuario
    /// con el rol puede, por Web API o Excel, mandar un lote directo a boveda, editar montos
    /// de una planilla archivada, borrar tulas de un lote cerrado o auto-aprobarse una
    /// solicitud. Este dominio codifica las transiciones legales observadas en las tres apps
    /// (Reception, Recounting, Supervisor) y el plugin las impone en el pipeline.
    /// </summary>
    public static class ReceptionGuard
    {
        // Option set sanic_receptionstatus de CH-Tbl-ReceptionBatches (verificado contra el Entity.xml del core).
        public const int BatchEnProgreso = 159460000;
        public const int BatchRecibido = 159460001;
        public const int BatchAnulado = 159460002;
        public const int BatchEnBoveda = 159460003;

        // Option set sanic_bagstatus de CH-Tbl-Bags.
        public const int BagRecibidaEnEsclusa = 159460000;
        public const int BagAlmacenadaEnBoveda = 159460001;
        public const int BagAsignadaACajero = 159460002;
        public const int BagRecontada = 159460003;
        public const int BagPendienteSupervisorRecuento = 159460004;
        public const int BagPendienteSupervisorEsclusa = 159460005;

        /// <summary>
        /// La tula se saco de la mesa y quedo libre para que la tome otro cajero.
        ///
        /// Existe porque "Asignada a cajero" era una trampa: sus unicas salidas eran
        /// Recontada (terminal) y el estado de revision del supervisor, que significa
        /// otra cosa —hay una diferencia que aprobar—. Una tula abandonada quedaba
        /// enllavada para siempre. Reusar el estado de supervisor habria mezclado
        /// "esto hay que revisarlo" con "esto esta libre", que en la pantalla del
        /// supervisor son dos colas distintas.
        /// </summary>
        public const int BagDevueltaParaReasignacion = 159460006;

        // Option set sanic_approvalstatus de CH-Tbl-ApprovalRequests. Pendiente = null.
        public const int ApprovalAprobado = 159460000;
        public const int ApprovalRechazado = 159460001;

        /// <summary>
        /// Unico tipo de solicitud que puede nacer ya aprobada: la sesion de recuento de mesa
        /// propia (atajo de negocio deliberado de la app de recuento).
        /// </summary>
        public const string ApprovalTypeSesionRecuento = "Crear sesion de recuento";

        // ---- Lote (CH-Tbl-ReceptionBatches) ----

        /// <summary>Un lote solo puede nacer En progreso (o sin estado, que el default resuelve).</summary>
        public static ReceptionGuardResult EvaluateBatchCreate(int? bornStatus)
        {
            if (bornStatus == null || bornStatus == BatchEnProgreso)
            {
                return ReceptionGuardResult.Allow();
            }

            return ReceptionGuardResult.Block(
                "Un lote de recepcion solo puede crearse en estado 'En progreso'.");
        }

        /// <summary>
        /// Transiciones legales del lote: En progreso &lt;-&gt; Recibido (cerrar y reabrir) y
        /// Recibido -&gt; En boveda. Un lote en boveda queda congelado por completo.
        /// </summary>
        /// <param name="previousStatus">Estado previo (null si el registro legado no tiene).</param>
        /// <param name="statusInTarget">true si el request escribe sanic_receptionstatus.</param>
        /// <param name="newStatus">Estado destino cuando <paramref name="statusInTarget"/>.</param>
        public static ReceptionGuardResult EvaluateBatchUpdate(
            int? previousStatus, bool statusInTarget, int? newStatus,
            bool touchesOnlyTotals = false)
        {
            if (touchesOnlyTotals)
            {
                // Escritura SOLO de los totales declarados materializados
                // (ReceptionBatchTotalsPlugin/backfill): metadata derivada, no una
                // edicion de negocio — pasa aun con el lote en boveda. Mismo
                // racional que touchesOnlyTotals del lote TB.
                return ReceptionGuardResult.Allow();
            }

            if (previousStatus == null)
            {
                return ReceptionGuardResult.Allow();
            }

            if (previousStatus == BatchEnBoveda)
            {
                if (statusInTarget && newStatus == BatchEnBoveda)
                {
                    return ReceptionGuardResult.Allow();
                }

                return ReceptionGuardResult.Block(
                    "El lote ya esta en boveda y quedo congelado; no admite modificaciones.");
            }

            if (previousStatus == BatchAnulado)
            {
                return ReceptionGuardResult.Block("Un lote anulado no admite modificaciones.");
            }

            if (!statusInTarget || newStatus == previousStatus)
            {
                return ReceptionGuardResult.Allow();
            }

            var isLegal =
                (previousStatus == BatchEnProgreso && newStatus == BatchRecibido)
                || (previousStatus == BatchRecibido && newStatus == BatchEnProgreso)
                || (previousStatus == BatchRecibido && newStatus == BatchEnBoveda);

            if (isLegal)
            {
                return ReceptionGuardResult.Allow();
            }

            return ReceptionGuardResult.Block(
                "Transicion de estado de lote no permitida. El ciclo es: " +
                "En progreso -> Recibido -> En boveda (y Recibido puede reabrirse a En progreso).");
        }

        /// <summary>Solo se elimina un lote que sigue En progreso.</summary>
        public static ReceptionGuardResult EvaluateBatchDelete(int? status)
        {
            if (status == null || status == BatchEnProgreso)
            {
                return ReceptionGuardResult.Allow();
            }

            return ReceptionGuardResult.Block(
                "Solo puede eliminarse un lote que este 'En progreso'.");
        }

        // ---- Planilla (CH-Tbl-Receptions) ----

        /// <summary>Una planilla no puede crearse dentro de un lote cerrado.</summary>
        public static ReceptionGuardResult EvaluateReceptionCreate(bool hasBatch, int? batchStatus)
        {
            if (!hasBatch)
            {
                return ReceptionGuardResult.Allow();
            }

            return EvaluateBatchIsOpen(batchStatus, "crear una planilla en");
        }

        /// <summary>
        /// Una planilla de un lote en boveda esta congelada; y re-colgarla de otro lote
        /// exige que el lote destino tambien este abierto.
        /// </summary>
        public static ReceptionGuardResult EvaluateReceptionUpdate(int? currentBatchStatus, bool isReparent, int? newBatchStatus)
        {
            var current = EvaluateBatchIsOpen(currentBatchStatus, "modificar una planilla de");
            if (!current.IsAllowed)
            {
                return current;
            }

            if (isReparent)
            {
                return EvaluateBatchIsOpen(newBatchStatus, "mover una planilla hacia");
            }

            return ReceptionGuardResult.Allow();
        }

        /// <summary>Una planilla de un lote en boveda no puede eliminarse.</summary>
        public static ReceptionGuardResult EvaluateReceptionDelete(int? batchStatus)
        {
            return EvaluateBatchIsOpen(batchStatus, "eliminar una planilla de");
        }

        // ---- Tula (CH-Tbl-Bags) ----

        /// <summary>
        /// Estados de nacimiento legales: sin estado o Recibida en esclusa (captura),
        /// Pendiente de supervisor - esclusa (circuito de aprobacion) y Almacenada en
        /// boveda (materializacion de 'Agregar tula' por el supervisor).
        /// </summary>
        public static ReceptionGuardResult EvaluateBagCreate(int? bornStatus)
        {
            if (bornStatus == null
                || bornStatus == BagRecibidaEnEsclusa
                || bornStatus == BagAlmacenadaEnBoveda
                || bornStatus == BagPendienteSupervisorEsclusa)
            {
                return ReceptionGuardResult.Allow();
            }

            return ReceptionGuardResult.Block(
                "Una tula no puede nacer en un estado del ciclo de recuento; " +
                "debe ingresar por esclusa o por el circuito de aprobacion.");
        }

        /// <summary>
        /// Matriz de transiciones de BagStatus observada en las tres apps:
        /// esclusa -&gt; boveda/cajero/pendiente-esclusa; boveda -&gt; cajero/pendiente-esclusa;
        /// cajero -&gt; recontada/pendiente-recuento; pendiente-recuento -&gt; cajero/recontada;
        /// pendiente-esclusa -&gt; esclusa/boveda. Recontada es terminal.
        /// </summary>
        public static ReceptionGuardResult EvaluateBagStatusTransition(int? previousStatus, int newStatus)
        {
            if (previousStatus == null || previousStatus == newStatus)
            {
                return ReceptionGuardResult.Allow();
            }

            bool isLegal;
            switch (previousStatus.Value)
            {
                case BagRecibidaEnEsclusa:
                    isLegal = newStatus == BagAlmacenadaEnBoveda
                              || newStatus == BagAsignadaACajero
                              || newStatus == BagPendienteSupervisorEsclusa;
                    break;
                case BagAlmacenadaEnBoveda:
                    isLegal = newStatus == BagAsignadaACajero
                              || newStatus == BagPendienteSupervisorEsclusa;
                    break;
                case BagAsignadaACajero:
                    isLegal = newStatus == BagRecontada
                              || newStatus == BagPendienteSupervisorRecuento
                              // Devolucion: la unica forma de sacar una tula de la mesa
                              // sin recontarla. Ver BagDevueltaParaReasignacion.
                              || newStatus == BagDevueltaParaReasignacion;
                    break;
                case BagPendienteSupervisorRecuento:
                    isLegal = newStatus == BagAsignadaACajero
                              || newStatus == BagRecontada;
                    break;
                case BagPendienteSupervisorEsclusa:
                    isLegal = newStatus == BagRecibidaEnEsclusa
                              || newStatus == BagAlmacenadaEnBoveda;
                    break;
                case BagDevueltaParaReasignacion:
                    // NO vuelve a boveda: decision explicita. La tula devuelta se
                    // retoma reescaneandola, que es el mismo gesto con el que el
                    // cajero toma cualquier tula.
                    isLegal = newStatus == BagAsignadaACajero;
                    break;
                case BagRecontada:
                default:
                    isLegal = false;
                    break;
            }

            if (isLegal)
            {
                return ReceptionGuardResult.Allow();
            }

            return ReceptionGuardResult.Block(
                "Transicion de estado de tula no permitida por el ciclo de vida " +
                "esclusa -> boveda -> cajero -> recontada (con desvios de aprobacion).");
        }

        /// <summary>
        /// La identidad de una tula (numero, nombre, planilla) solo se edita con el lote
        /// abierto — o sobre una tula pendiente de supervisor, que es como el circuito de
        /// aprobacion materializa remapeos y ediciones con el lote ya en boveda.
        /// </summary>
        public static ReceptionGuardResult EvaluateBagIdentityEdit(int? bagPreviousStatus, int? batchStatus)
        {
            if (bagPreviousStatus == BagPendienteSupervisorEsclusa)
            {
                return ReceptionGuardResult.Allow();
            }

            return EvaluateBatchIsOpen(batchStatus, "editar la identidad de una tula de");
        }

        /// <summary>
        /// Se borra una tula con el lote abierto; con el lote en boveda, solo si esta
        /// pendiente de supervisor (denegacion de 'Agregar tula' o aprobacion de 'Eliminar tula').
        /// </summary>
        public static ReceptionGuardResult EvaluateBagDelete(int? bagStatus, int? batchStatus)
        {
            if (bagStatus == BagPendienteSupervisorEsclusa)
            {
                return ReceptionGuardResult.Allow();
            }

            return EvaluateBatchIsOpen(batchStatus, "eliminar una tula de");
        }

        // ---- Solicitud (CH-Tbl-ApprovalRequests) ----

        /// <summary>
        /// Una solicitud nace pendiente (sanic_approvalstatus en blanco). La unica excepcion
        /// es <see cref="ApprovalTypeSesionRecuento"/>, y HOY ESTA ACOTADA (H-08):
        /// no alcanza con que el texto libre del tipo coincida — la sesion de recuento
        /// referenciada en RequestResources tiene que existir y ser DEL SOLICITANTE,
        /// resuelto server-side leyendo la sesion como sistema.
        ///
        /// Cuando la excepcion no aplica pero la solicitud es legitima (sesion de otro,
        /// JSON ilegible, sesion inexistente) NO se bloquea la creacion: se DEGRADA a
        /// pendiente y la firma un supervisor. Bloquear ahi seria dejar al cajero sin
        /// poder pedir su sesion por un JSON mal armado; degradar mantiene el cuatro ojos.
        /// </summary>
        /// <param name="bornStatus">sanic_approvalstatus que trae el Create (null = pendiente).</param>
        /// <param name="requestType">sanic_requesttype (texto libre; comparacion ORDINAL exacta).</param>
        /// <param name="sessionOwnership">
        /// Pertenencia de la sesion referenciada, resuelta por el plugin como sistema.
        /// Sin dato confiable se pasa <see cref="RecountingSessionOwnership.Unresolved"/>.
        /// </param>
        public static ApprovalCreateEvaluation EvaluateApprovalCreate(
            int? bornStatus,
            string? requestType,
            RecountingSessionOwnership sessionOwnership)
        {
            if (bornStatus == null)
            {
                return ApprovalCreateEvaluation.Allow();
            }

            // Ordinal exacto: el tipo es texto libre y antes se comparaba ignorando
            // mayusculas — una variante de tipeo alcanzaba para pedir la excepcion.
            if (!string.Equals(requestType, ApprovalTypeSesionRecuento, StringComparison.Ordinal))
            {
                return ApprovalCreateEvaluation.Block(
                    "Una solicitud de aprobacion no puede crearse ya resuelta; " +
                    "nace pendiente y la resuelve un supervisor.");
            }

            if (bornStatus != ApprovalAprobado)
            {
                // El atajo de negocio autoriza a nacer APROBADA (mesa propia), nada mas.
                return ApprovalCreateEvaluation.Block(
                    "Una solicitud de sesion de recuento solo puede nacer aprobada por el atajo " +
                    "de mesa propia; cualquier otra resolucion la firma un supervisor.");
            }

            switch (sessionOwnership)
            {
                case RecountingSessionOwnership.OwnedByRequester:
                    return ApprovalCreateEvaluation.Allow();

                case RecountingSessionOwnership.OwnedByOtherUser:
                    return ApprovalCreateEvaluation.DegradeToPending(
                        "La sesion de recuento referenciada es de otro cajero: la solicitud nace " +
                        "PENDIENTE y la debe resolver un supervisor.");

                default:
                    return ApprovalCreateEvaluation.DegradeToPending(
                        "No se pudo verificar que la sesion de recuento referenciada sea del " +
                        "solicitante: la solicitud nace PENDIENTE y la debe resolver un supervisor.");
            }
        }

        /// <summary>
        /// Separacion de funciones de la decision: la solicitud debe estar pendiente,
        /// quien decide no puede ser quien la creo Y ademas tiene que pertenecer al grupo
        /// AAD de supervisores aprobadores (H-01). El modelo de acceso solo no alcanza:
        /// hoy ningun team asigna el rol de supervisor, asi que la autoridad de aprobacion
        /// se verifica por membresia de grupo, no por rol.
        /// </summary>
        /// <param name="previousStatus">Estado previo (null = pendiente).</param>
        /// <param name="decisionInTarget">true si el request escribe sanic_approvalstatus.</param>
        /// <param name="creatorId">createdby de la solicitud.</param>
        /// <param name="initiatingUserId">Usuario real que dispara la operacion.</param>
        /// <param name="isAuthorizedApprover">
        /// true si el actor pertenece al grupo AAD aprobador. Lo resuelve el plugin
        /// (parametro CH-Tbl-Parameter + AAD group team) y es FAIL-CLOSED: sin dato
        /// confiable el plugin ni siquiera llega aca. Sin valor por defecto a proposito:
        /// un llamador nuevo tiene que decidir explicitamente que pasa.
        /// </param>
        /// <param name="touchesOnlyMirror">
        /// true si el Update escribe SOLO el espejo derivado sanic_bagnumber (B-6): el
        /// plugin lo estampa/backfillea desde el JSON de RequestResources. Es metadata,
        /// no una decision de negocio, asi que pasa aunque la solicitud este congelada —
        /// mismo racional que <c>touchesOnlyTotals</c> del lote. El backfill de B-6 cae
        /// sobre el grueso del historico (solicitudes ya resueltas) y sin esto chocaria
        /// en masa contra el congelamiento. GUARDA: nunca junto con una decision, para
        /// que no se cuele un cambio de estado escondido detras del espejo.
        /// </param>
        public static ReceptionGuardResult EvaluateApprovalUpdate(
            int? previousStatus,
            bool decisionInTarget,
            Guid creatorId,
            Guid initiatingUserId,
            bool isAuthorizedApprover,
            bool touchesOnlyMirror = false)
        {
            if (touchesOnlyMirror && !decisionInTarget)
            {
                // Escritura pura del espejo derivado: se permite incluso sobre una
                // solicitud resuelta (habilita el backfill de B-6 sin abrir la puerta).
                return ReceptionGuardResult.Allow();
            }

            if (previousStatus != null)
            {
                return ReceptionGuardResult.Block(
                    "La solicitud ya fue resuelta y quedo congelada; no admite modificaciones.");
            }

            if (!decisionInTarget)
            {
                return ReceptionGuardResult.Allow();
            }

            if (creatorId == Guid.Empty)
            {
                return ReceptionGuardResult.Block(
                    "No se pudo determinar el creador de la solicitud; se bloquea la decision por seguridad.");
            }

            if (initiatingUserId == creatorId)
            {
                return ReceptionGuardResult.Block(
                    "Quien crea una solicitud no puede resolverla (separacion de funciones).");
            }

            if (!isAuthorizedApprover)
            {
                return ReceptionGuardResult.Block(
                    "No pertenece al grupo aprobador de supervisores; no puede resolver esta solicitud.");
            }

            return ReceptionGuardResult.Allow();
        }

        private static ReceptionGuardResult EvaluateBatchIsOpen(int? batchStatus, string action)
        {
            if (batchStatus == BatchEnBoveda)
            {
                return ReceptionGuardResult.Block(
                    $"No se puede {action} un lote que ya esta en boveda; use el circuito de aprobacion del supervisor.");
            }

            if (batchStatus == BatchAnulado)
            {
                return ReceptionGuardResult.Block($"No se puede {action} un lote anulado.");
            }

            return ReceptionGuardResult.Allow();
        }
    }
}
