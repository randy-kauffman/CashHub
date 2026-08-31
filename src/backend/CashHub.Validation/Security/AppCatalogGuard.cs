using System;

namespace CashHub.Validation.Security
{
    /// <summary>Resultado de validar un campo del catalogo de apps.</summary>
    public sealed class AppCatalogGuardResult
    {
        private AppCatalogGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si el valor es valido; false si la escritura debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        public static AppCatalogGuardResult Allow() => new AppCatalogGuardResult(true, null);

        public static AppCatalogGuardResult Block(string reason) => new AppCatalogGuardResult(false, reason);
    }

    /// <summary>
    /// Validacion del catalogo de apps del hub (CH-Tbl-AppCatalogs).
    ///
    /// El hub lanza sanic_appurl tal cual (Launch con LaunchTarget.Replace): una URL arbitraria
    /// escrita por Web API o Excel convierte al hub en un lanzador de phishing con identidad
    /// corporativa. Y un sanic_aadgroupid malformado rompe el gating por grupos en silencio:
    /// el token nunca matchea contra checkMemberGroups y la app desaparece para todos.
    /// La UI no valida nada de esto; el guard vive en el pipeline para que la regla se
    /// aplique venga de donde venga la escritura.
    /// </summary>
    public static class AppCatalogGuard
    {
        /// <summary>Unico host permitido para las URLs del catalogo (player de Power Apps).</summary>
        public const string AllowedHost = "apps.powerapps.com";

        /// <summary>
        /// Valida sanic_appurl. Blanco es valido (fila en preparacion, sin nada que lanzar);
        /// con valor, debe ser una URL https absoluta cuyo host sea exactamente
        /// <see cref="AllowedHost"/> en el puerto por defecto.
        /// </summary>
        public static AppCatalogGuardResult EvaluateAppUrl(string? appUrl)
        {
            if (string.IsNullOrWhiteSpace(appUrl))
            {
                return AppCatalogGuardResult.Allow();
            }

            var candidate = appUrl!.Trim();

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return AppCatalogGuardResult.Block(
                    $"La URL de la app no es una URL absoluta valida: '{candidate}'.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return AppCatalogGuardResult.Block(
                    $"La URL de la app debe usar https (se recibio '{uri.Scheme}').");
            }

            if (!string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
            {
                return AppCatalogGuardResult.Block(
                    $"La URL de la app debe apuntar a {AllowedHost} (se recibio '{uri.Host}').");
            }

            if (!uri.IsDefaultPort)
            {
                return AppCatalogGuardResult.Block(
                    $"La URL de la app no puede usar un puerto no estandar ({uri.Port}).");
            }

            return AppCatalogGuardResult.Allow();
        }

        /// <summary>
        /// Valida el sanic_aadgroupid de una fila de CH-Tbl-AppCatalogAccess: exactamente UN
        /// GUID de grupo AAD (la fila existe solo para declarar ese grupo). Devuelve el GUID
        /// parseado para que el plugin lo normalice y chequee duplicados.
        /// </summary>
        public static AppCatalogGuardResult EvaluateAccessGroupId(string? aadGroupId, out Guid groupId)
        {
            groupId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(aadGroupId))
            {
                return AppCatalogGuardResult.Block(
                    "La fila de acceso debe declarar el GUID del grupo AAD.");
            }

            var candidate = aadGroupId!.Trim();

            if (candidate.Contains(";"))
            {
                return AppCatalogGuardResult.Block(
                    "Una fila de acceso admite un solo grupo AAD: es una fila por grupo, sin ';'.");
            }

            if (!Guid.TryParse(candidate, out groupId))
            {
                return AppCatalogGuardResult.Block(
                    $"El grupo AAD '{candidate}' no es un GUID valido.");
            }

            return AppCatalogGuardResult.Allow();
        }
    }
}
