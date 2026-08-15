using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Politica FAIL-CLOSED de la autoridad de aprobacion del circuito de recepcion/recuento.
    ///
    /// El GUID del grupo AAD cuyos miembros pueden firmar solicitudes vive en la tabla de
    /// parametros CH-Tbl-Parameter (sanic_chtblparameter), NO en environment variables:
    /// es la fuente de config del backend de CashHub y se administra sin tocar solucion.
    ///
    /// Que esto sea un dominio puro y no tres if adentro del plugin no es ceremonia: la
    /// regla "sin config no se aprueba" es la que sostiene todo el control, y tiene que
    /// estar bajo test. El plugin solo hace la consulta y le pasa el texto crudo.
    /// </summary>
    public static class ApproverGroupConfiguration
    {
        /// <summary>
        /// Clave de CH-Tbl-Parameter con el object id del grupo AAD aprobador.
        /// Convencion de la tabla: Dominio.Sub.Nombre — misma familia que
        /// Security.ApproverGroup.Banca / .Boveda que ya usan los RFC de clientes.
        ///
        /// SIN ESTA FILA NADIE APRUEBA. Sembrarla ANTES de registrar el paso
        /// (ver scripts/register-reception-guard.sh).
        /// </summary>
        public const string ParameterKey = "Security.ApproverGroup.Supervisor";

        /// <summary>
        /// Interpreta el valor crudo del parametro (o null si la fila no existe o esta inactiva).
        /// </summary>
        public static ApproverGroupResolution Resolve(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return ApproverGroupResolution.NotConfigured(
                    "No se pudo verificar la pertenencia al grupo aprobador de supervisores: " +
                    $"falta el parametro '{ParameterKey}' en CH-Tbl-Parameter (o esta inactivo). " +
                    "Avise al administrador.");
            }

            // Tolera el formato con llaves de Azure ({...}) y espacios: el valor se carga a mano.
            var trimmed = rawValue!.Trim();
            if (!Guid.TryParse(trimmed, out var groupId) || groupId == Guid.Empty)
            {
                return ApproverGroupResolution.NotConfigured(
                    "No se pudo verificar la pertenencia al grupo aprobador de supervisores: " +
                    $"el parametro '{ParameterKey}' de CH-Tbl-Parameter no es un identificador " +
                    "de grupo valido. Avise al administrador.");
            }

            return ApproverGroupResolution.Configured(groupId);
        }

        /// <summary>Mensaje cuando la membresia no se puede resolver por una falla de consulta.</summary>
        public const string MembershipUnavailableMessage =
            "No se pudo verificar la pertenencia al grupo aprobador de supervisores; " +
            "la solicitud no se resuelve por seguridad. Reintente y, si persiste, avise al administrador.";
    }

    /// <summary>Resultado de interpretar el parametro del grupo aprobador.</summary>
    public sealed class ApproverGroupResolution
    {
        private ApproverGroupResolution(bool isConfigured, Guid groupId, string? reason)
        {
            IsConfigured = isConfigured;
            GroupId = groupId;
            Reason = reason;
        }

        /// <summary>true si hay un grupo AAD utilizable.</summary>
        public bool IsConfigured { get; }

        /// <summary>Object id del grupo AAD. <see cref="Guid.Empty"/> cuando no esta configurado.</summary>
        public Guid GroupId { get; }

        /// <summary>Motivo apto para el usuario cuando no esta configurado. null si lo esta.</summary>
        public string? Reason { get; }

        internal static ApproverGroupResolution Configured(Guid groupId) =>
            new ApproverGroupResolution(true, groupId, null);

        internal static ApproverGroupResolution NotConfigured(string reason) =>
            new ApproverGroupResolution(false, Guid.Empty, reason);
    }
}
