using System;

namespace CashHub.Validation.Security
{
    /// <summary>Resultado de evaluar una escritura sobre una linea de RFC.</summary>
    public sealed class ChangeRequestLineGuardResult
    {
        private ChangeRequestLineGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si la escritura es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        public static ChangeRequestLineGuardResult Allow() => new ChangeRequestLineGuardResult(true, null);

        public static ChangeRequestLineGuardResult Block(string reason) => new ChangeRequestLineGuardResult(false, reason);
    }

    /// <summary>
    /// Guard de sanic_chtblclientdatachangerequestline (cierra S3 del analisis:
    /// docs/clientadmin-rebuild/00-analisis-y-plan.md — "no hay guard sobre
    /// ClientDataChangeRequestLines: crear/borrar lineas de un RFC ajeno en Draft vía Web
    /// API es posible para un Approver con Write global"). Corre en Pre-op sync de
    /// Create/Update/Delete de la linea.
    ///
    /// Reglas de negocio: el RFC padre debe estar en Draft (mientras el RFC este vivo, sus
    /// lineas son editables; una vez enviado, ya no), sin lineas duplicadas por (RFC,
    /// FieldDefinition), y solo el creador del RFC o un aprobador de la tabla objetivo
    /// puede tocar sus lineas — misma separacion de funciones que
    /// <see cref="ChangeRequestApprovalGuard"/> aplica a la cabecera.
    ///
    /// 🧨 Auto-bloqueo evitado: cuando sanic_CHResolveChangeRequest aplica o rechaza un
    /// RFC, marca cada linea Applied/Denied con la cabecera YA en Submitted (no Draft) —
    /// exactamente la transicion que este guard bloquearia si se evaluara igual que una
    /// escritura humana. La exencion es por PROFUNDIDAD de pipeline (igual que
    /// <see cref="RecountingGuard.IsWriteExempt"/>): la escritura del propio
    /// ResolveChangeRequestPlugin llega vía IOrganizationService DESDE OTRO plugin, asi
    /// que corre a depth > 1; el actor humano y las apps canvas siempre entran a depth 1.
    /// </summary>
    public static class ChangeRequestLineGuard
    {
        /// <summary>
        /// true cuando la escritura viene de otro plugin en el mismo pipeline (depth > 1) —
        /// tipicamente ResolveChangeRequestPlugin marcando el desenlace de sus propias
        /// lineas. El actor humano y las apps canvas SIEMPRE entran a depth 1.
        /// </summary>
        public static bool IsWriteExempt(int depth) => depth > 1;

        /// <summary>
        /// Evalua si Create/Update/Delete de una linea de RFC es legitimo.
        /// </summary>
        /// <param name="isExempt">Resultado de <see cref="IsWriteExempt"/>.</param>
        /// <param name="parentResolved">true si se pudo llegar al RFC padre (lookup resoluble).</param>
        /// <param name="parentStatus">sanic_changestatus del RFC padre.</param>
        /// <param name="parentCreatorId">createdby del RFC padre.</param>
        /// <param name="actorId">Usuario real que dispara la operacion (InitiatingUserId).</param>
        /// <param name="isAuthorizedApprover">
        /// true si <paramref name="actorId"/> pertenece al grupo Approver de la tabla
        /// objetivo del RFC padre (misma resolucion de membresia que
        /// GuardChangeRequestApprovalPlugin ya hace para la cabecera).
        /// </param>
        /// <param name="isDuplicateFieldDefinition">
        /// true si ya existe otra linea del mismo RFC para el mismo sanic_fielddefinition.
        /// Solo aplica a Create (o Update que cambia el FieldDefinition); Delete siempre
        /// pasa false aca.
        /// </param>
        public static ChangeRequestLineGuardResult Evaluate(
            bool isExempt,
            bool parentResolved,
            int? parentStatus,
            Guid parentCreatorId,
            Guid actorId,
            bool isAuthorizedApprover,
            bool isDuplicateFieldDefinition)
        {
            if (isExempt)
            {
                return ChangeRequestLineGuardResult.Allow();
            }

            if (!parentResolved)
            {
                return ChangeRequestLineGuardResult.Block(
                    "No se pudo resolver el RFC padre de la linea; se bloquea la escritura por seguridad.");
            }

            if (parentStatus != ChangeRequestApprovalGuard.Draft)
            {
                return ChangeRequestLineGuardResult.Block(
                    "Solo se pueden crear, editar o borrar lineas de un RFC que este en estado Draft.");
            }

            if (parentCreatorId == Guid.Empty)
            {
                return ChangeRequestLineGuardResult.Block(
                    "No se pudo determinar el creador del RFC padre; se bloquea la escritura por seguridad.");
            }

            if (actorId != parentCreatorId && !isAuthorizedApprover)
            {
                return ChangeRequestLineGuardResult.Block(
                    "Solo el creador del RFC o un aprobador de la tabla objetivo puede modificar sus lineas.");
            }

            if (isDuplicateFieldDefinition)
            {
                return ChangeRequestLineGuardResult.Block(
                    "Ya existe una linea de cambio para ese campo (sanic_fielddefinition) en este RFC.");
            }

            return ChangeRequestLineGuardResult.Allow();
        }
    }
}
