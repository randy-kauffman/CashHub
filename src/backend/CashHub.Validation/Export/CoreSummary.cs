using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CashHub.Validation.Export
{
    /// <summary>Una linea del resumen por cuenta: cuenta + moneda, con su monto y su conteo.</summary>
    public sealed class CoreSummaryRow
    {
        public string Account { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public sealed class CoreSummaryParseResult
    {
        public List<CoreSummaryRow> Rows { get; set; } = new List<CoreSummaryRow>();

        /// <summary>
        /// Lineas que no se pudieron interpretar. Se REPORTAN en vez de descartarse en
        /// silencio: que una linea se caiga sin avisar haria creer al aprobador que
        /// comparo todo cuando comparo una parte.
        /// </summary>
        public List<string> IgnoredLines { get; set; } = new List<string>();
    }

    /// <summary>
    /// Lee el "Resumen por Cuenta" que el aprobador copia de la pantalla de revision del
    /// IBS Branch (Cuenta | Moneda | Monto | Cant TRN).
    ///
    /// SE ANCLA EN LA MONEDA. Es el unico token inequivoco de la fila: la cuenta es el
    /// token anterior, el monto el siguiente y la cantidad el ultimo. Asi da igual que el
    /// copiado traiga la columna "Select", que venga con tabulaciones o con corridas de
    /// espacios, y que incluya o no el encabezado.
    /// </summary>
    public static class CoreSummaryParser
    {
        private static readonly HashSet<string> Monedas =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "COR", "USD", "EUR", "NIO" };

        public static CoreSummaryParseResult Parse(string pasted)
        {
            var result = new CoreSummaryParseResult();
            if (string.IsNullOrWhiteSpace(pasted))
            {
                return result;
            }

            var acumulado = new Dictionary<string, CoreSummaryRow>(StringComparer.Ordinal);
            var orden = new List<string>();

            foreach (var raw in pasted.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                var linea = raw.Trim();
                if (linea.Length == 0 || EsEncabezado(linea))
                {
                    continue;
                }

                var fila = LeerFila(linea);
                if (fila == null)
                {
                    result.IgnoredLines.Add(linea);
                    continue;
                }

                // Misma cuenta y moneda repetidas: se suman. El IBS no deberia repetirlas,
                // pero sumar es mas fiel que quedarse con una y descartar la otra.
                var clave = fila.Account + "|" + fila.Currency;
                if (acumulado.TryGetValue(clave, out var previa))
                {
                    previa.Amount += fila.Amount;
                    previa.Count += fila.Count;
                }
                else
                {
                    acumulado[clave] = fila;
                    orden.Add(clave);
                }
            }

            result.Rows = orden.Select(k => acumulado[k]).ToList();
            return result;
        }

        private static bool EsEncabezado(string linea)
        {
            var l = linea.ToUpperInvariant();
            return l.Contains("CUENTA") && (l.Contains("MONEDA") || l.Contains("MONTO"));
        }

        private static CoreSummaryRow LeerFila(string linea)
        {
            var tokens = linea
                .Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            // La moneda es el ancla. Sin ella la linea no es una fila del resumen.
            var iMoneda = Array.FindIndex(tokens, t => Monedas.Contains(t));
            if (iMoneda <= 0 || iMoneda + 1 >= tokens.Length)
            {
                return null;
            }

            var cuenta = NormalizarCuenta(tokens[iMoneda - 1]);
            if (cuenta == null)
            {
                return null;
            }

            if (!TryLeerMonto(tokens[iMoneda + 1], out var monto))
            {
                return null;
            }

            // La cantidad es el ultimo token; si no viene, queda en 0 y la comparacion lo
            // marcara como diferencia de conteo en vez de inventar un numero.
            var cantidad = 0;
            if (tokens.Length > iMoneda + 2)
            {
                int.TryParse(tokens[tokens.Length - 1].Replace(",", string.Empty),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out cantidad);
            }

            return new CoreSummaryRow
            {
                Account = cuenta,
                Currency = NormalizarMoneda(tokens[iMoneda]),
                Amount = monto,
                Count = cantidad,
            };
        }

        /// <summary>
        /// El IBS muestra la cuenta con ceros a la izquierda ("000000070") y CashHub la
        /// guarda como numero (70). Se normalizan las DOS puntas o todas las cuentas
        /// darian "falta en el lote" y el comparador seria inutil.
        /// </summary>
        public static string NormalizarCuenta(string cuenta)
        {
            if (string.IsNullOrWhiteSpace(cuenta))
            {
                return null;
            }

            var limpia = cuenta.Trim();
            if (!limpia.All(char.IsDigit))
            {
                return null;
            }

            var sinCeros = limpia.TrimStart('0');
            return sinCeros.Length == 0 ? "0" : sinCeros;
        }

        private static string NormalizarMoneda(string moneda)
        {
            // Adentro de CashHub la moneda se llama NIO; el CSV al core la manda como COR.
            // Se unifican para no tratarlas como monedas distintas.
            var m = moneda.ToUpperInvariant();
            return m == "NIO" ? "COR" : m;
        }

        /// <summary>
        /// Monto con separador de miles. El separador DECIMAL es el ultimo '.' o ',' que
        /// aparezca seguido de uno o dos digitos; todo lo demas son miles. Asi entra tanto
        /// "1,000,000.00" como "1.000.000,00" sin que el comparador empiece a mentir si
        /// algun dia cambia la localizacion del IBS.
        /// </summary>
        private static bool TryLeerMonto(string texto, out decimal monto)
        {
            monto = 0m;
            if (string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            var t = texto.Trim();
            var ultimoPunto = t.LastIndexOf('.');
            var ultimaComa = t.LastIndexOf(',');
            var iDecimal = Math.Max(ultimoPunto, ultimaComa);

            var sb = new StringBuilder(t.Length);
            for (var i = 0; i < t.Length; i++)
            {
                var c = t[i];
                if (char.IsDigit(c) || c == '-')
                {
                    sb.Append(c);
                }
                else if ((c == '.' || c == ',') && i == iDecimal && t.Length - i - 1 <= 2)
                {
                    sb.Append('.');
                }
                else if (c != '.' && c != ',')
                {
                    // Cualquier otro caracter (simbolo de moneda, espacio duro) invalida la
                    // fila: es preferible reportarla como no entendida que adivinar.
                    return false;
                }
            }

            return decimal.TryParse(sb.ToString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out monto);
        }
    }
}
