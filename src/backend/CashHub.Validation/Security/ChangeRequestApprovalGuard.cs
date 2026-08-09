using System;

namespace CashHub.Validation.Security
{
    /// <summary>Evento del pipeline sobre el que se evalua la solicitud de cambio (RFC).</summary>
    public enum ChangeRequestEvent
    {
        Create,
        Update
    }

    /// <summary>Resultado de evaluar una transicion de estado de un RFC.</summary>
    public sealed class ApprovalGuardResult
    {
        private ApprovalGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si la transicion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        public static ApprovalGuardResult Allow() => new ApprovalGuardResult(true, null);

        public static ApprovalGuardResult Block(string reason) => new ApprovalGuardResult(false, reason);
    }

    /// <summary>
    /// Separacion de funciones para los RFC de administracion de clientes.
    ///
    /// La app canvas oculta los botones de aprobar/rechazar segun el grupo y el creador,
    /// pero eso es solo cosmetico: un Contributor con prvWrite/prvCreate puede pegarle el
    /// estado por Web API o Excel y saltarse el control. Este guard vive en el pipeline
    /// (Create y Update) para que la regla se aplique venga de donde venga el cambio.
    ///
    /// Fase 1 (aca): la decision (Approved/Rejected) exige estado previo Submitted y que
    /// quien decide NO sea el creador. Con eso queda cerrada la auto-aprobacion.
    /// Fase 2 (pendiente): validar que el usuario pertenece al grupo Approver de la tabla
    /// objetivo — requiere resolver membresia de grupo AAD (via Azure AD group teams), que
    /// no se puede hacer desde un plugin en sandbox sin verificar primero el setup del entorno.
    /// </summary>
    public static class ChangeRequestApprovalGuard
    {
        // Option set sanic_changestatus (verificado contra el Entity.xml del core).
        public const int Draft = 159460000;
        public const int Submitted = 159460001;
        public const int Approved = 159460002;
        public const int Rejected = 159460003;
        public const int Applied = 159460004;
        public const int Failed = 159460005;
        public const int Canceled = 159460006;

        /// <summary>
        /// Evalua si la transicion de estado de un RFC es legitima.
        /// </summary>
        /// <param name="eventType">Create o Update.</param>
        /// <param name="previousStatus">Estado anterior (null en Create).</param>
        /// <param name="newStatus">Estado al que se quiere mover el RFC.</param>
        /// <param name="creatorId">createdby del RFC. En Create es el usuario que lo esta creando.</param>
        /// <param name="initiatingUserId">Usuario real que dispara la operacion (InitiatingUserId).</param>
        /// <param name="isAuthorizedApprover">
        /// Fase 2: true si el usuario pertenece al grupo Approver de la tabla objetivo del RFC.
        /// Lo resuelve el plugin (membresia de AAD group team); solo se consulta en la decision.
        /// </param>
        public static ApprovalGuardResult Evaluate(
            ChangeRequestEvent eventType,
            int? previousStatus,
            int newStatus,
            Guid creatorId,
            Guid initiatingUserId,
            bool isAuthorizedApprover)
        {
            if (eventType == ChangeRequestEvent.Create)
            {
                // Un RFC no puede nacer ya resuelto: debe pasar por el flujo (Draft/Submitted).
                if (newStatus == Approved || newStatus == Rejected || newStatus == Applied || newStatus == Failed)
                {
                    return ApprovalGuardResult.Block(
                        "Un RFC no puede crearse directamente en un estado resuelto (Approved/Rejected/Applied/Failed). " +
                        "Debe crearse como Draft o Submitted y resolverse mediante el proceso de aprobacion.");
                }

                return ApprovalGuardResult.Allow();
            }

            // Update: solo nos interesa la decision humana (aprobar/rechazar).
            // Las transiciones de sistema (Approved->Applied/Failed, ->Canceled, Draft->Submitted) pasan.
            if (newStatus != Approved && newStatus != Rejected)
            {
                return ApprovalGuardResult.Allow();
            }

            var decision = newStatus == Approved ? "aprobar" : "rechazar";

            if (previousStatus != Submitted)
            {
                return ApprovalGuardResult.Block(
                    $"Solo se puede {decision} un RFC que este en estado Submitted.");
            }

            if (creatorId == Guid.Empty)
            {
                // Sin creador identificable no se puede garantizar la separacion de funciones.
                return ApprovalGuardResult.Block(
                    "No se pudo determinar el creador del RFC; se bloquea la resolucion por seguridad.");
            }

            if (initiatingUserId == creatorId)
            {
                return ApprovalGuardResult.Block(
                    $"El creador de un RFC no puede {decision} su propia solicitud (separacion de funciones).");
            }

            if (!isAuthorizedApprover)
            {
                return ApprovalGuardResult.Block(
                    $"No pertenece al grupo de aprobadores de la tabla objetivo; no puede {decision} este RFC.");
            }

            return ApprovalGuardResult.Allow();
        }
    }
}
