using System;

namespace CashHub.Validation.Security
{
    /// <summary>Decision humana que dispara la Custom API sanic_CHResolveChangeRequest.</summary>
    public enum ChangeRequestDecision
    {
        Approve,
        Reject
    }

    /// <summary>
    /// Resultado de resolver un RFC: si es legitimo, trae los estados FINALES de cabecera
    /// y linea a escribir en la misma transaccion (sin el estado intermedio Approved que
    /// usaban los 4 flujos — aca la materializacion es sincrona, no hay ventana).
    /// </summary>
    public sealed class ChangeRequestResolutionOutcome
    {
        private ChangeRequestResolutionOutcome(bool isAllowed, string? reason, int headerStatus, int lineStatus)
        {
            IsAllowed = isAllowed;
            Reason = reason;
            HeaderStatus = headerStatus;
            LineStatus = lineStatus;
        }

        /// <summary>true si la resolucion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        /// <summary>Estado final de sanic_changestatus de la cabecera. Solo valido si <see cref="IsAllowed"/>.</summary>
        public int HeaderStatus { get; }

        /// <summary>Estado final de sanic_changestatus de cada linea. Solo valido si <see cref="IsAllowed"/>.</summary>
        public int LineStatus { get; }

        public static ChangeRequestResolutionOutcome Allow(int headerStatus, int lineStatus) =>
            new ChangeRequestResolutionOutcome(true, null, headerStatus, lineStatus);

        public static ChangeRequestResolutionOutcome Block(string reason) =>
            new ChangeRequestResolutionOutcome(false, reason, 0, 0);
    }

    /// <summary>
    /// Motor de decision de sanic_CHResolveChangeRequest: reemplaza los 4 flujos
    /// materializadores (Applyclientdatachanges/Createclientdata/Activaterow/
    /// Deactivaterow, docs/clientadmin-rebuild/00-analisis-y-plan.md §3.1). La
    /// separacion de funciones (creador != resolutor, membresia Approver de la Fase 2)
    /// sigue siendo de <see cref="ChangeRequestApprovalGuard"/> — aca solo se traduce la
    /// decision (Approve/Reject) al estado final de cabecera y linea, y se agrega la
    /// regla "sin lineas no hay materializacion" para Update/Create (mata L2/L4: un RFC
    /// vacio jamas fue un exito).
    /// </summary>
    public static class ChangeRequestResolution
    {
        // Option set sanic_changestatus de sanic_chtblclientdatachangerequestline
        // (verificado contra su Entity.xml). Los valores coinciden numericamente con los
        // de la cabecera salvo la etiqueta 159460003 (Rejected en cabecera, Denied en
        // linea): mismo option set "shape", dos catalogos distintos.
        public const int LineDraft = 159460000;
        public const int LineSubmitted = 159460001;
        public const int LineApproved = 159460002;
        public const int LineDenied = 159460003;
        public const int LineApplied = 159460004;
        public const int LineFailed = 159460005;
        public const int LineCanceled = 159460006;

        // Option set sanic_type de la cabecera (verificado contra su Entity.xml).
        public const int TypeUpdate = 159460000;
        public const int TypeDeactivate = 159460001;
        public const int TypeCreate = 159460002;
        public const int TypeActivate = 159460003;

        /// <summary>
        /// Evalua si la resolucion (Approve/Reject) de un RFC en estado
        /// <paramref name="currentHeaderStatus"/> es legitima, delegando la separacion de
        /// funciones en <see cref="ChangeRequestApprovalGuard"/>. Si es legitima, devuelve
        /// los estados finales a escribir: Approve -> Applied/LineApplied (sin pasar por
        /// Approved: la materializacion es atomica, no hay estado intermedio observable);
        /// Reject -> Rejected/LineDenied.
        /// </summary>
        public static ChangeRequestResolutionOutcome Evaluate(
            int currentHeaderStatus,
            ChangeRequestDecision decision,
            Guid creatorId,
            Guid resolverId,
            bool isAuthorizedApprover)
        {
            var decisionStatus = decision == ChangeRequestDecision.Approve
                ? ChangeRequestApprovalGuard.Approved
                : ChangeRequestApprovalGuard.Rejected;

            var guard = ChangeRequestApprovalGuard.Evaluate(
                ChangeRequestEvent.Update, currentHeaderStatus, decisionStatus, creatorId, resolverId, isAuthorizedApprover);

            if (!guard.IsAllowed)
            {
                return ChangeRequestResolutionOutcome.Block(guard.Reason!);
            }

            return decision == ChangeRequestDecision.Approve
                ? ChangeRequestResolutionOutcome.Allow(ChangeRequestApprovalGuard.Applied, LineApplied)
                : ChangeRequestResolutionOutcome.Allow(ChangeRequestApprovalGuard.Rejected, LineDenied);
        }

        /// <summary>
        /// Update y Create exigen al menos una linea de cambio: sin ella no hay campos que
        /// convertir y aprobar "en vacio" es un exito disfrazado (L2/L4 del analisis).
        /// Activate/Deactivate mueven sanic_recordstatus del maestro, sin campos — no
        /// dependen de lineas.
        /// </summary>
        public static bool RequiresLines(int type) => type == TypeUpdate || type == TypeCreate;
    }
}
