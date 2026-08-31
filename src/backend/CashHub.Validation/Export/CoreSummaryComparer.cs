using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CashHub.Validation.Export
{
    public enum CoreSummaryStatus
    {
        Match,
        AmountDiffers,
        CountDiffers,
        AmountAndCountDiffer,

        /// <summary>La cuenta esta en el lote de CashHub y NO aparece en el resumen del IBS.</summary>
        MissingInCore,

        /// <summary>La cuenta aparece en el resumen del IBS y NO esta en el lote.</summary>
        MissingInBatch,
    }

    public sealed class CoreSummaryComparisonRow
    {
        public string Account { get; set; }
        public string Currency { get; set; }
        public CoreSummaryStatus Status { get; set; }

        public decimal BatchAmount { get; set; }
        public decimal CoreAmount { get; set; }
        public int BatchCount { get; set; }
        public int CoreCount { get; set; }

        /// <summary>Lote menos IBS. Positivo = al IBS le falta plata.</summary>
        public decimal AmountDifference => BatchAmount - CoreAmount;

        public int CountDifference => BatchCount - CoreCount;
    }

    public sealed class CoreSummaryComparison
    {
        public List<CoreSummaryComparisonRow> Rows { get; set; } = new List<CoreSummaryComparisonRow>();

        public bool IsMatch { get; set; }
        public int TotalAccounts => Rows.Count;
        public int MatchingAccounts => Rows.Count(r => r.Status == CoreSummaryStatus.Match);
        public int DifferingAccounts => Rows.Count(r => r.Status != CoreSummaryStatus.Match);
    }

    /// <summary>
    /// Compara el resumen por cuenta del lote de CashHub contra el que el aprobador copio
    /// del IBS Branch.
    ///
    /// EL VEREDICTO ES POR CUENTA, NO GLOBAL. Un "no cuadra" a secas deja al aprobador
    /// buscando la aguja a mano, que es justo el trabajo que esto viene a evitar: hay que
    /// decirle QUE cuenta, en QUE moneda y POR CUANTO.
    ///
    /// SIN TOLERANCIAS. Un centavo de diferencia en un archivo contable es una diferencia.
    /// Perdonarlo seria decidir por el banco que ese centavo no importa.
    /// </summary>
    public static class CoreSummaryComparer
    {
        public static CoreSummaryComparison Compare(
            IEnumerable<CoreSummaryRow> batch, IEnumerable<CoreSummaryRow> core)
        {
            var delLote = Indexar(batch);
            var delCore = Indexar(core);

            var claves = new List<string>();
            claves.AddRange(delLote.Keys);
            claves.AddRange(delCore.Keys.Where(k => !delLote.ContainsKey(k)));

            var filas = new List<CoreSummaryComparisonRow>();
            foreach (var clave in claves)
            {
                delLote.TryGetValue(clave, out var l);
                delCore.TryGetValue(clave, out var c);

                var fila = new CoreSummaryComparisonRow
                {
                    Account = (l ?? c).Account,
                    Currency = (l ?? c).Currency,
                    BatchAmount = l?.Amount ?? 0m,
                    CoreAmount = c?.Amount ?? 0m,
                    BatchCount = l?.Count ?? 0,
                    CoreCount = c?.Count ?? 0,
                };

                fila.Status = Evaluar(l, c);
                filas.Add(fila);
            }

            // Las diferencias PRIMERO: el operador no tiene que ir a buscarlas entre
            // cientos de lineas que estan bien.
            var ordenadas = filas
                .OrderBy(f => f.Status == CoreSummaryStatus.Match ? 1 : 0)
                .ThenBy(f => f.Account, StringComparer.Ordinal)
                .ThenBy(f => f.Currency, StringComparer.Ordinal)
                .ToList();

            return new CoreSummaryComparison
            {
                Rows = ordenadas,
                // Comparar nada contra nada NO es un match: no prueba absolutamente nada,
                // y decir que cuadra seria falso.
                IsMatch = ordenadas.Count > 0 && ordenadas.All(f => f.Status == CoreSummaryStatus.Match),
            };
        }

        private static CoreSummaryStatus Evaluar(CoreSummaryRow lote, CoreSummaryRow core)
        {
            if (core == null)
            {
                return CoreSummaryStatus.MissingInCore;
            }

            if (lote == null)
            {
                return CoreSummaryStatus.MissingInBatch;
            }

            var montoDistinto = lote.Amount != core.Amount;
            var cantidadDistinta = lote.Count != core.Count;

            if (montoDistinto && cantidadDistinta)
            {
                return CoreSummaryStatus.AmountAndCountDiffer;
            }
            if (montoDistinto)
            {
                return CoreSummaryStatus.AmountDiffers;
            }
            if (cantidadDistinta)
            {
                return CoreSummaryStatus.CountDiffers;
            }

            return CoreSummaryStatus.Match;
        }

        private static Dictionary<string, CoreSummaryRow> Indexar(IEnumerable<CoreSummaryRow> filas)
        {
            var d = new Dictionary<string, CoreSummaryRow>(StringComparer.Ordinal);
            foreach (var f in filas ?? Enumerable.Empty<CoreSummaryRow>())
            {
                // La clave es cuenta + MONEDA. Sin la moneda, una cuenta con cordobas y
                // dolares se sumaria en una sola linea y el descuadre quedaria escondido.
                var clave = f.Account + "|" + f.Currency;
                if (d.TryGetValue(clave, out var previa))
                {
                    previa.Amount += f.Amount;
                    previa.Count += f.Count;
                }
                else
                {
                    d[clave] = new CoreSummaryRow
                    {
                        Account = f.Account,
                        Currency = f.Currency,
                        Amount = f.Amount,
                        Count = f.Count,
                    };
                }
            }

            return d;
        }
    }

    /// <summary>
    /// Arma el resumen por cuenta a partir de las MISMAS transacciones con las que se
    /// genera el CSV que va al core.
    ///
    /// Que salgan de la misma fuente no es un detalle de implementacion: es la propiedad
    /// que hace confiable la comparacion. Si el resumen se calculara por otro camino,
    /// podria cuadrar contra el IBS mientras el archivo enviado dice otra cosa.
    /// </summary>
    public static class CoreSummaryBuilder
    {
        public static List<CoreSummaryRow> FromTransactions(IEnumerable<CoreExportTransaction> transactions)
        {
            var acumulado = new Dictionary<string, CoreSummaryRow>(StringComparer.Ordinal);
            var orden = new List<string>();

            foreach (var t in transactions ?? Enumerable.Empty<CoreExportTransaction>())
            {
                // MONTOTOTAL, no EFECTIVO: es lo que el IBS suma en su Resumen por Cuenta.
                // En los lotes con cheques las dos columnas difieren —la diferencia es
                // exactamente el monto de los cheques— asi que elegir mal haria que el
                // comparador marcara diferencias falsas justo ahi.
                var cuenta = CoreSummaryParser.NormalizarCuenta(
                    ((long)(t.AccountNumber ?? 0m)).ToString(CultureInfo.InvariantCulture));
                var moneda = Moneda(t.Currency);
                var clave = cuenta + "|" + moneda;

                if (acumulado.TryGetValue(clave, out var fila))
                {
                    fila.Amount += t.Total;
                    fila.Count += 1;
                }
                else
                {
                    acumulado[clave] = new CoreSummaryRow
                    {
                        Account = cuenta,
                        Currency = moneda,
                        Amount = t.Total,
                        Count = 1,
                    };
                    orden.Add(clave);
                }
            }

            return orden.Select(k => acumulado[k]).ToList();
        }

        private static string Moneda(int code)
        {
            switch (code)
            {
                case CoreExportTransaction.CurrencyNio: return "COR";
                case CoreExportTransaction.CurrencyUsd: return "USD";
                case CoreExportTransaction.CurrencyEur: return "EUR";
                default: return string.Empty;
            }
        }
    }
}
