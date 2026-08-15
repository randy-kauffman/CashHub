using System;
using System.Text;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Lector minimo de objetos JSON PLANOS, sin dependencias externas.
    ///
    /// El assembly de plugins es net462 firmado y no lleva Newtonsoft ni
    /// System.Text.Json: para leer UNA clave escalar de un blob corto no paga
    /// arrastrar un paquete. A diferencia de un IndexOf pelado, este lector
    /// reconoce la posicion sintactica: solo acepta como clave una cadena
    /// seguida de ':', asi que "GUID" NO matchea dentro de "ReceiptGUID" ni
    /// dentro de un VALOR que contenga esa palabra.
    ///
    /// Contrato duro: NUNCA lanza. Un JSON roto, truncado o vacio devuelve null.
    /// Lo consumen estampas de espejo que jamas deben voltear la operacion del
    /// usuario (B-6: si no parsea, el espejo queda en blanco y se traza).
    /// </summary>
    public static class FlatJson
    {
        /// <summary>
        /// Valor escalar de <paramref name="key"/> (comparacion case-insensitive).
        /// Devuelve null si falta la clave, si el valor es null/objeto/arreglo o
        /// si el texto no es JSON. Las cadenas vuelven des-escapadas.
        /// </summary>
        public static string? Value(string? json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            try
            {
                return Scan(json!, key);
            }
            catch (Exception)
            {
                // Blindaje final: cualquier forma inesperada se trata como "sin dato".
                return null;
            }
        }

        private static string? Scan(string s, string key)
        {
            var i = 0;
            while (i < s.Length)
            {
                if (s[i] != '"')
                {
                    i++;
                    continue;
                }

                var end = FindStringEnd(s, i + 1);
                if (end < 0)
                {
                    return null;
                }

                var afterString = SkipWhitespace(s, end + 1);
                if (afterString >= s.Length || s[afterString] != ':')
                {
                    // Era un valor de cadena, no una clave.
                    i = end + 1;
                    continue;
                }

                var token = Unescape(s, i + 1, end);
                var valueStart = SkipWhitespace(s, afterString + 1);

                if (string.Equals(token, key, StringComparison.OrdinalIgnoreCase))
                {
                    return ReadValue(s, valueStart);
                }

                i = SkipValue(s, valueStart);
            }

            return null;
        }

        private static string? ReadValue(string s, int i)
        {
            if (i >= s.Length)
            {
                return null;
            }

            if (s[i] == '"')
            {
                var end = FindStringEnd(s, i + 1);
                if (end < 0)
                {
                    return null;
                }

                var text = Unescape(s, i + 1, end);
                return text.Length == 0 ? null : text;
            }

            var stop = SkipValue(s, i);
            var literal = s.Substring(i, stop - i).Trim();

            if (literal.Length == 0
                || literal[0] == '{' || literal[0] == '['
                || string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return literal;
        }

        /// <summary>Indice del primer caracter DESPUES del valor que arranca en i.</summary>
        private static int SkipValue(string s, int i)
        {
            if (i >= s.Length)
            {
                return s.Length;
            }

            if (s[i] == '"')
            {
                var end = FindStringEnd(s, i + 1);
                return end < 0 ? s.Length : end + 1;
            }

            var depth = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '"')
                {
                    var end = FindStringEnd(s, i + 1);
                    i = end < 0 ? s.Length : end + 1;
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    if (depth == 0)
                    {
                        return i;
                    }

                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return i;
                }

                i++;
            }

            return s.Length;
        }

        /// <summary>Indice de la comilla que cierra la cadena abierta en <paramref name="i"/>.</summary>
        private static int FindStringEnd(string s, int i)
        {
            while (i < s.Length)
            {
                if (s[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (s[i] == '"')
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }

            return i;
        }

        private static string Unescape(string s, int start, int end)
        {
            var builder = new StringBuilder(end - start);
            var i = start;
            while (i < end)
            {
                var c = s[i];
                if (c != '\\')
                {
                    builder.Append(c);
                    i++;
                    continue;
                }

                i++;
                if (i >= end)
                {
                    break;
                }

                switch (s[i])
                {
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'u':
                        if (i + 4 < end
                            && int.TryParse(s.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out var code))
                        {
                            builder.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: builder.Append(s[i]); break;
                }

                i++;
            }

            return builder.ToString();
        }
    }
}
