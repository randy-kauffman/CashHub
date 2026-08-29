using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CashHub.Validation.Reporting
{
    /// <summary>
    /// Motor de plantillas mínimo para los correos al cliente. Deliberadamente simple:
    /// negocio edita HTML y textos SIN tocar código, pero la lógica sigue en C#.
    ///
    /// Sintaxis soportada:
    ///   {{token}}                      valor simple (escapado)
    ///   {{{token}}}                    valor sin escapar (para HTML ya armado)
    ///   {{#lista}} ... {{/lista}}      repite el bloque por cada fila de la lista
    ///   {{#si:token}} ... {{/si}}      muestra el bloque solo si el token tiene valor
    /// Dentro de un bloque de lista, {{campo}} refiere a la columna de esa fila.
    /// </summary>
    public static class TemplateEngine
    {
        private static readonly Regex ListBlock =
            new(@"\{\{#(?<name>[a-zA-Z0-9_]+)\}\}(?<body>.*?)\{\{/\k<name>\}\}", RegexOptions.Singleline);
        private static readonly Regex IfBlock =
            new(@"\{\{#si:(?<name>[a-zA-Z0-9_]+)\}\}(?<body>.*?)\{\{/si\}\}", RegexOptions.Singleline);
        // Existia un token triple {{{x}}} que emitia el valor SIN escapar. Se elimino: el
        // correo se arma con datos que pone el cliente (nombre, referencias, numeros), y
        // negocio edita las plantillas en Dataverse sin tocar codigo. Un {{{cliente}}} en
        // una plantilla era inyeccion de HTML directa. Ya no hay ningun camino sin escapar.
        // Se sigue reconociendo la sintaxis triple para no dejar {...} colgando si alguna
        // plantilla vieja la trae, pero ahora escapa igual que la doble.
        private static readonly Regex TripleToken = new(@"\{\{\{(?<name>[a-zA-Z0-9_]+)\}\}\}");
        private static readonly Regex Token = new(@"\{\{(?<name>[a-zA-Z0-9_]+)\}\}");

        public sealed class TemplateData
        {
            public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<Dictionary<string, string?>>> Lists { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public TemplateData Set(string key, string? value)
            {
                Values[key] = value;
                return this;
            }

            public TemplateData SetList(string key, IEnumerable<Dictionary<string, string?>> rows)
            {
                Lists[key] = rows.ToList();
                return this;
            }
        }

        public static string Render(string template, TemplateData data)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            // 1. Bloques de lista (antes que nada: su contenido puede traer tokens propios)
            var output = ListBlock.Replace(template, m =>
            {
                var name = m.Groups["name"].Value;
                if (!data.Lists.TryGetValue(name, out var rows))
                {
                    return string.Empty; // lista desconocida o vacía: el bloque desaparece
                }
                var sb = new StringBuilder();
                foreach (var row in rows)
                {
                    var rowData = new TemplateData();
                    foreach (var kv in data.Values) rowData.Values[kv.Key] = kv.Value;
                    foreach (var kv in row) rowData.Values[kv.Key] = kv.Value;
                    sb.Append(ReplaceTokens(m.Groups["body"].Value, rowData));
                }
                return sb.ToString();
            });

            // 2. Bloques condicionales
            output = IfBlock.Replace(output, m =>
            {
                var name = m.Groups["name"].Value;
                var hasValue = data.Values.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v);
                var hasRows = data.Lists.TryGetValue(name, out var rows) && rows.Count > 0;
                return hasValue || hasRows ? m.Groups["body"].Value : string.Empty;
            });

            // 3. Tokens sueltos
            return ReplaceTokens(output, data);
        }

        private static string ReplaceTokens(string text, TemplateData data)
        {
            // La triple se resuelve primero (para consumir {{{x}}} antes de que la doble
            // matchee {{x}} adentro y deje llaves sueltas), pero ESCAPA — no hay salida cruda.
            text = TripleToken.Replace(text, m =>
                data.Values.TryGetValue(m.Groups["name"].Value, out var v) ? Escape(v) : string.Empty);

            return Token.Replace(text, m =>
            {
                var name = m.Groups["name"].Value;
                if (name.StartsWith("#") || name.StartsWith("/")) return m.Value;
                return data.Values.TryGetValue(name, out var v) ? Escape(v) : string.Empty;
            });
        }

        public static string Escape(string? text) =>
            (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
