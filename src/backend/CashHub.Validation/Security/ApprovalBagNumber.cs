using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// B-6 — espejo sanic_bagnumber en CH-Tbl-ApprovalRequest.
    ///
    /// La bandeja de aprobaciones tiene que poder buscar por numero de TULA, pero
    /// el numero vive DENTRO del blob JSON de sanic_requestresources (ledger L-19
    /// de 00-analisis-y-plan.md): un StartsWith sobre un JSON no es una busqueda,
    /// y ninguna consulta delegable puede entrar ahi. El espejo saca el numero a
    /// una columna propia al crear la solicitud.
    ///
    /// El contrato de claves NO es homogeneo (03-pantalla-aprobaciones.md §0):
    ///   Agregar tula / Editar tula        -> BagID = NUMERO de tula (texto) + BagGUID
    ///   Remapear tula / Eliminar tula     -> GUID  = GUID de tula
    ///   Primera tula en planilla,
    ///   Tula sin declaracion,
    ///   Tula con diferencia,
    ///   Tula recontada sin declaracion    -> BagGUID
    ///   Crear sesion de recuento,
    ///   Modificar numero de planilla      -> sin tula
    ///
    /// Regla 1 del contrato: **BagID NUNCA es un GUID** — es el numero. Por eso el
    /// resolvedor devuelve DOS caminos: numero literal (estampa directa, cero
    /// lecturas) o GUID de tula (el plugin resuelve el numero con UN Retrieve).
    /// Si no hay ninguno, el espejo queda en blanco: es un dato derivado de
    /// conveniencia y jamas puede voltear el Create de una solicitud.
    /// </summary>
    public static class ApprovalBagNumber
    {
        /// <summary>Largo de sanic_bagnumber (nvarchar 100) creado por script.</summary>
        public const int MaxLength = 100;

        /// <summary>Clave que trae el NUMERO de tula ya listo.</summary>
        public const string KeyBagNumber = "BagID";

        /// <summary>Claves que traen el GUID de la tula, en orden de preferencia.</summary>
        public const string KeyBagGuid = "BagGUID";
        public const string KeyGuid = "GUID";

        /// <summary>De donde sale el numero de tula de una solicitud.</summary>
        public sealed class Source
        {
            /// <summary>Numero literal tomado del JSON, ya normalizado. null = no vino.</summary>
            public string? Number { get; set; }

            /// <summary>GUID de la tula, para que el plugin resuelva el numero.</summary>
            public Guid? BagId { get; set; }

            /// <summary>true = la solicitud no habla de ninguna tula; el espejo queda null.</summary>
            public bool IsEmpty => Number == null && !BagId.HasValue;
        }

        /// <summary>
        /// Lee sanic_requestresources y decide de donde sale el numero. NUNCA lanza:
        /// JSON vacio, roto, truncado o con otro esquema devuelve un Source vacio.
        /// </summary>
        public static Source Resolve(string? requestResources)
        {
            var source = new Source();
            if (string.IsNullOrWhiteSpace(requestResources))
            {
                return source;
            }

            var raw = FlatJson.Value(requestResources, KeyBagNumber);

            // Defensa contra productores futuros: si alguien mete un GUID en BagID
            // (el bug que la app vieja daba por hecho), se trata como GUID, no como
            // numero — un GUID en la columna de busqueda seria basura para el usuario.
            if (raw != null && Guid.TryParse(raw, out var bagIdAsGuid))
            {
                source.BagId = bagIdAsGuid;
            }
            else
            {
                source.Number = Cap(raw);
            }

            if (!source.BagId.HasValue)
            {
                source.BagId = ReadGuid(requestResources, KeyBagGuid)
                    ?? ReadGuid(requestResources, KeyGuid);
            }

            return source;
        }

        /// <summary>Normaliza el numero para la columna: trim, vacio -> null, recorte al largo.</summary>
        public static string? Cap(string? value)
        {
            if (value == null)
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            return trimmed.Length <= MaxLength ? trimmed : trimmed.Substring(0, MaxLength);
        }

        private static Guid? ReadGuid(string? json, string key)
        {
            var raw = FlatJson.Value(json, key);
            return raw != null && Guid.TryParse(raw, out var parsed) ? parsed : (Guid?)null;
        }
    }
}
