using System;
using System.Collections.Generic;
using System.Linq;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Contrato de datos de <c>sanic_requestresources</c> de CH-Tbl-ApprovalRequests,
    /// verificado contra los productores canvas y documentado en
    /// docs/supervisor-rebuild/03-pantalla-aprobaciones.md §0.
    ///
    /// <c>sanic_requesttype</c> es TEXTO LIBRE (no un option set) y el JSON lo arman las
    /// apps por concatenacion de strings: las claves NO son homogeneas entre tipos y
    /// tampoco entre productores del mismo tipo. Por eso este chequeo, hoy, SOLO MIDE:
    /// el guard traza el desvio y deja pasar la creacion. Bloquear con los productores
    /// viejos vivos seria romper la operacion; primero se mide, despues se endurece.
    ///
    /// La lectura de valores la hace <see cref="FlatJson"/> (lector posicional, no
    /// lanza): aca solo vive el contrato y el diagnostico.
    /// </summary>
    public static class ApprovalRequestResources
    {
        /// <summary>
        /// Los 10 tipos conocidos con las claves que su productor declara. Comparacion
        /// ORDINAL exacta: el literal viaja como texto libre y una variante de tipeo es
        /// justamente lo que queremos ver en la traza, no absorber en silencio.
        /// </summary>
        private static readonly Dictionary<string, string[]> ContractByType =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Remapear tula"] = new[] { "GUID", "ReceiptID", "ReceiptGUID", "BagPreviousState" },
                ["Agregar tula"] = new[] { "ReceiptGUID", "BagID", "BagGUID", "ReceptionBatchGUID" },
                ["Eliminar tula"] = new[] { "GUID", "BagPreviousState" },
                ["Modificar numero de planilla"] = new[] { "ReceiptGUID", "NewReceiptID" },
                ["Editar tula"] = new[] { "ReceiptGUID", "BagID", "ReceptionBatchGUID", "BagGUID" },
                ["Crear sesion de recuento"] = new[] { "RecountingSessionGUID" },
                ["Primera tula en planilla"] = new[] { "BagGUID", "RecountingSessionGUID", "DeclarationBagGUID" },
                ["Tula sin declaracion"] = new[] { "BagGUID", "RecountingSessionGUID" },
                ["Tula con diferencia"] = new[] { "BagGUID" },
                ["Tula recontada sin declaracion"] = new[] { "BagGUID" },
            };

        /// <summary>Clave del GUID de sesion de recuento (unica clave del tipo sesion).</summary>
        public const string RecountingSessionGuidKey = "RecountingSessionGUID";

        /// <summary>Tipos de solicitud con contrato declarado.</summary>
        public static IEnumerable<string> KnownTypes => ContractByType.Keys;

        /// <summary>Claves declaradas por el productor de <paramref name="requestType"/>; vacio si el tipo no es conocido.</summary>
        public static IReadOnlyList<string> ContractKeys(string? requestType)
        {
            if (requestType != null && ContractByType.TryGetValue(requestType, out var keys))
            {
                return keys;
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Compara el JSON contra el contrato del tipo. No decide nada: describe.
        /// Nunca lanza — un blob ilegible es un dato del diagnostico, no una excepcion.
        /// </summary>
        public static RequestResourcesInspection Inspect(string? requestType, string? json)
        {
            var isKnownType = requestType != null && ContractByType.ContainsKey(requestType);

            var missing = new List<string>();
            if (isKnownType)
            {
                foreach (var key in ContractByType[requestType!])
                {
                    if (string.IsNullOrWhiteSpace(FlatJson.Value(json, key)))
                    {
                        missing.Add(key);
                    }
                }
            }

            return new RequestResourcesInspection(requestType, json, isKnownType, missing);
        }

        /// <summary>
        /// Forma minima de "objeto JSON": abre y cierra llave y las comillas cierran.
        /// No valida la gramatica entera — separa el blob truncado/HTML/vacio del que
        /// al menos tiene forma, que es la unica distincion que la traza necesita.
        /// </summary>
        internal static bool LooksLikeObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var text = json!.Trim();
            if (text.Length < 2 || text[0] != '{' || text[text.Length - 1] != '}')
            {
                return false;
            }

            var insideString = false;
            for (var i = 1; i < text.Length - 1; i++)
            {
                var c = text[i];
                if (insideString && c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    insideString = !insideString;
                }
            }

            return !insideString;
        }
    }

    /// <summary>Resultado de inspeccionar el JSON de RequestResources contra el contrato.</summary>
    public sealed class RequestResourcesInspection
    {
        private readonly string? _json;

        internal RequestResourcesInspection(
            string? requestType,
            string? json,
            bool isKnownType,
            IReadOnlyList<string> missingKeys)
        {
            RequestType = requestType;
            _json = json;
            IsKnownType = isKnownType;
            MissingKeys = missingKeys;
        }

        public string? RequestType { get; }

        /// <summary>true si el tipo esta en la tabla de contrato (comparacion ordinal exacta).</summary>
        public bool IsKnownType { get; }

        /// <summary>true si sanic_requestresources trae algo.</summary>
        public bool HasResources => !string.IsNullOrWhiteSpace(_json);

        /// <summary>true si el texto tiene forma de objeto JSON (ver <see cref="ApprovalRequestResources.LooksLikeObject"/>).</summary>
        public bool IsObjectShaped => ApprovalRequestResources.LooksLikeObject(_json);

        /// <summary>Claves del contrato ausentes o vacias. Vacio si el tipo no es conocido.</summary>
        public IReadOnlyList<string> MissingKeys { get; }

        /// <summary>true si no hay nada que reportar: tipo desconocido, o tipo conocido con el JSON completo.</summary>
        public bool IsCompliant => !IsKnownType || (IsObjectShaped && MissingKeys.Count == 0);

        /// <summary>Valor escalar de una clave, o null si falta o el blob no es legible.</summary>
        public string? Value(string key) => FlatJson.Value(_json, key);

        /// <summary>
        /// Linea unica y estable para la traza. NO vuelca el JSON: puede traer datos de
        /// negocio y las trazas son semi-publicas (las lee cualquier admin).
        /// </summary>
        public string Summary()
        {
            var type = string.IsNullOrWhiteSpace(RequestType) ? "(sin tipo)" : RequestType;

            if (!IsKnownType)
            {
                return $"tipo='{type}' fuera de los 10 del contrato; forma-objeto={IsObjectShaped}";
            }

            if (!HasResources)
            {
                return $"tipo='{type}' SIN RequestResources";
            }

            if (!IsObjectShaped)
            {
                return $"tipo='{type}' RequestResources no tiene forma de objeto JSON";
            }

            return MissingKeys.Count == 0
                ? $"tipo='{type}' contrato completo"
                : $"tipo='{type}' faltan claves del contrato: {string.Join(",", MissingKeys.ToArray())}";
        }
    }
}
