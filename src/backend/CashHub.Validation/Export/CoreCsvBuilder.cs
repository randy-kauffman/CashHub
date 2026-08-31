using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CashHub.Validation.Export
{
    /// <summary>Identidad bancaria fija del CSV al core (parametro Export.Core.Identidad).</summary>
    public sealed class CoreExportIdentity
    {
        public string TrnCode { get; set; }
        public string Manifiesto { get; set; }
        public string DisponibilidadInmediata { get; set; }
        public string TipoId { get; set; }
        public string Nacionalidad { get; set; }
        public string NumeroId1 { get; set; }
        public string NumeroId2 { get; set; }
    }

    /// <summary>Una transaccion del lote, con solo lo que el layout del core necesita.</summary>
    public sealed class CoreExportTransaction
    {
        public const int CurrencyNio = 159460000;
        public const int CurrencyUsd = 159460001;
        public const int CurrencyEur = 159460002;

        /// <summary>Numerica en Dataverse (sanic_accountnumber es decimal), no texto.</summary>
        public decimal? AccountNumber { get; set; }
        public decimal Total { get; set; }
        public string TransactionId { get; set; }
        public string Reference { get; set; }
        public int Currency { get; set; }
        public decimal CashAmount { get; set; }
    }

    /// <summary>
    /// Arma el CSV del lote con el layout IBS (14 columnas fijas).
    ///
    /// REEMPLAZA A "Create CSV table" DE CH-FLOW-DOWNLOADDATA, por dos motivos que no se
    /// podian resolver en el flujo:
    ///
    /// 1. NO ESCAPABA NADA. Power Automate no tiene regex replace, asi que una lista
    ///    blanca de caracteres ahi seria un replace() anidado por caracter, sin tests y
    ///    sin forma de compartir la regla con la validacion de entrada. La misma logica
    ///    escrita dos veces en dos lenguajes diverge el primer dia.
    ///
    /// 2. ANULABA EL FORMATO DE DOS DECIMALES. El mapeo era
    ///    float(formatNumber(x,'0.00')), y ese float() deshace justo lo que formatNumber
    ///    acababa de aplicar: en los archivos reales se ven 5100 y 3427.5 donde el core
    ///    EXIGE 5100.00 y 3427.50.
    ///
    /// Se arma en el SERVIDOR, no en el navegador: el contenido de un archivo que entra
    /// al core bancario no puede depender de codigo que cualquiera edita en las
    /// devtools. El componente de la app solo lo guarda como archivo.
    /// </summary>
    public static class CoreCsvBuilder
    {
        public const string Header =
            "TRNCODE,CUENTA,MONTOTOTAL,REFERENCIA,DEPOSITANTE,MONEDA,DEPOSITID," +
            "DISPONIBILIDADINMEDIATA,EFECTIVO,MANIFIESTO,TIPOID,NACIONALIDAD,NUMEROID1,NUMEROID2";

        /// <summary>CRLF, como el archivo que el core ya venia consumiendo.</summary>
        private const string RowTerminator = "\r\n";

        private const int AccountDigits = 9;

        public static string Build(
            IEnumerable<CoreExportTransaction> transactions,
            CoreExportIdentity identity,
            string allowedCharacters)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append(RowTerminator);

            if (transactions == null)
            {
                return sb.ToString();
            }

            // La identidad es configuracion nuestra, pero una coma mal tipeada en el
            // parametro romperia TODAS las filas del archivo. Se sanea igual: la defensa
            // no puede depender de que nadie se equivoque cargando un parametro.
            var trn = Clean(identity?.TrnCode, allowedCharacters);
            var manifiesto = Clean(identity?.Manifiesto, allowedCharacters);
            var disponibilidad = Clean(identity?.DisponibilidadInmediata, allowedCharacters);
            var tipoId = Clean(identity?.TipoId, allowedCharacters);
            var nacionalidad = Clean(identity?.Nacionalidad, allowedCharacters);
            var numeroId1 = Clean(identity?.NumeroId1, allowedCharacters);
            var numeroId2 = Clean(identity?.NumeroId2, allowedCharacters);

            foreach (var t in transactions)
            {
                var transactionId = Clean(t.TransactionId, allowedCharacters);

                // Mismo coalesce que hacia el flujo, pero DESPUES de sanear: si la
                // referencia se sanea hasta quedar vacia, cae al numero de transaccion.
                // Mandar el depositante en blanco seria peor que mandarlo imperfecto —
                // el numero al menos identifica el deposito.
                var depositante = Clean(t.Reference, allowedCharacters);
                if (depositante.Length == 0)
                {
                    depositante = transactionId;
                }

                sb.Append(trn).Append(',')
                  .Append(Account(t.AccountNumber)).Append(',')
                  .Append(Amount(t.Total)).Append(',')
                  .Append(transactionId).Append(',')
                  .Append(depositante).Append(',')
                  .Append(Currency(t.Currency)).Append(',')
                  .Append(transactionId).Append(',')
                  .Append(disponibilidad).Append(',')
                  .Append(Amount(t.CashAmount)).Append(',')
                  .Append(manifiesto).Append(',')
                  .Append(tipoId).Append(',')
                  .Append(nacionalidad).Append(',')
                  .Append(numeroId1).Append(',')
                  .Append(numeroId2)
                  .Append(RowTerminator);
            }

            return sb.ToString();
        }

        private static string Clean(string value, string allowed) =>
            CoreExportSanitizer.Sanitize(value, allowed);

        /// <summary>
        /// Dos decimales SIEMPRE, con punto decimal SIEMPRE.
        ///
        /// InvariantCulture no es un detalle: con cultura es-NI el separador decimal es
        /// la coma, y "5100,00" partiria la fila en dos igual que un depositante con
        /// coma. El formato del archivo no puede depender de la cultura del servidor que
        /// lo genera.
        /// </summary>
        private static string Amount(decimal value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>
        /// Nueve digitos con ceros a la izquierda, como hacia formatNumber(x,'000000000')
        /// en el flujo. No se sanea: la columna es numerica en Dataverse, no puede traer
        /// una coma. Y va sin decimales — 100200111.0 sale 100200111.
        /// </summary>
        private static string Account(decimal? account) =>
            ((long)(account ?? 0m)).ToString(new string('0', AccountDigits), CultureInfo.InvariantCulture);

        private static string Currency(int code)
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
