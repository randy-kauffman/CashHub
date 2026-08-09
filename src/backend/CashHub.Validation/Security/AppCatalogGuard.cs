using System;
using System.Collections.Generic;

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
        /// Valida sanic_aadgroupid: lista de GUIDs de grupos AAD separados por ';'.
        /// Blanco es valido (app visible para todos). Con valor, cada token no vacio debe ser
        /// un GUID, debe quedar al menos un token, y no se admiten duplicados.
        /// </summary>
        public static AppCatalogGuardResult EvaluateAadGroupIds(string? aadGroupIds)
        {
            if (string.IsNullOrWhiteSpace(aadGroupIds))
            {
                return AppCatalogGuardResult.Allow();
            }

            var seen = new HashSet<Guid>();
            foreach (var rawToken in aadGroupIds!.Split(';'))
            {
                var token = rawToken.Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (!Guid.TryParse(token, out var groupId))
                {
                    return AppCatalogGuardResult.Block(
                        $"El grupo AAD '{token}' no es un GUID valido. " +
                        "El campo admite GUIDs separados por ';'.");
                }

                if (!seen.Add(groupId))
                {
                    return AppCatalogGuardResult.Block(
                        $"El grupo AAD '{token}' esta duplicado en la lista.");
                }
            }

            if (seen.Count == 0)
            {
                return AppCatalogGuardResult.Block(
                    "El campo de grupos AAD tiene separadores pero ningun GUID. " +
                    "Dejelo vacio (app publica) o declare al menos un grupo.");
            }

            return AppCatalogGuardResult.Allow();
        }
    }
}
