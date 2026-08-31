using System.Collections.Generic;
using CashHub.Validation.Export;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El armado del CSV que va al core (layout IBS, 14 columnas fijas).
    ///
    /// Reemplaza a la accion "Create CSV table" de CH-Flow-DownloadData, que no escapa
    /// nada y ademas anulaba el formato de dos decimales: el mapeo hacia
    /// float(formatNumber(x,'0.00')), y ese float() deshace justo lo que formatNumber
    /// acababa de hacer. Por eso en los archivos reales se ven 5100 y 3427.5 en vez de
    /// 5100.00 y 3427.50. Los dos decimales son EXIGIDOS por el core.
    /// </summary>
    public class CoreCsvBuilderTests
    {
        private static readonly CoreExportIdentity Identity = new CoreExportIdentity
        {
            TrnCode = "8004",
            Manifiesto = "1",
            DisponibilidadInmediata = "0",
            TipoId = "0000000000001",
            Nacionalidad = "0000000000558",
            NumeroId1 = "4411001991",
            NumeroId2 = "003A",
        };

        private const string Whitelist =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .-";

        private static CoreExportTransaction Tx(
            decimal? account = 992983992m,
            decimal total = 5100m,
            string transactionId = "16912",
            string reference = "SRC-80841988",
            int currency = CoreExportTransaction.CurrencyNio,
            decimal cash = 5100m) =>
            new CoreExportTransaction
            {
                AccountNumber = account,
                Total = total,
                TransactionId = transactionId,
                Reference = reference,
                Currency = currency,
                CashAmount = cash,
            };

        private static string[] Rows(string csv) =>
            csv.Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);

        [Fact]
        public void Cabecera_EsElLayoutIbsExacto()
        {
            var csv = CoreCsvBuilder.Build(new List<CoreExportTransaction>(), Identity, Whitelist);

            Assert.Equal(
                "TRNCODE,CUENTA,MONTOTOTAL,REFERENCIA,DEPOSITANTE,MONEDA,DEPOSITID," +
                "DISPONIBILIDADINMEDIATA,EFECTIVO,MANIFIESTO,TIPOID,NACIONALIDAD,NUMEROID1,NUMEROID2",
                Rows(csv)[0]);
        }

        [Fact]
        public void Fila_ReproduceElLayoutDelFlujo()
        {
            var csv = CoreCsvBuilder.Build(new[] { Tx() }, Identity, Whitelist);

            Assert.Equal(
                "8004,992983992,5100.00,16912,SRC-80841988,COR,16912,0,5100.00,1," +
                "0000000000001,0000000000558,4411001991,003A",
                Rows(csv)[1]);
        }

        [Fact]
        public void Terminador_EsCrLf_ComoElArchivoQueYaConsumeElCore()
        {
            var csv = CoreCsvBuilder.Build(new[] { Tx() }, Identity, Whitelist);

            Assert.EndsWith("\r\n", csv);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(csv, "\r\n").Count);
        }

        [Theory]
        [InlineData(5100, "5100.00")]
        [InlineData(3427.5, "3427.50")]
        [InlineData(0, "0.00")]
        [InlineData(1406.869, "1406.87")]
        [InlineData(1406.861, "1406.86")]
        public void Montos_SiempreConDosDecimales(decimal monto, string esperado)
        {
            // LO QUE EL FLUJO ROMPIA. float(formatNumber(x,'0.00')) devolvia 5100 y
            // 3427.5. El core los exige con dos decimales, siempre.
            var csv = CoreCsvBuilder.Build(new[] { Tx(total: monto, cash: monto) }, Identity, Whitelist);
            var campos = Rows(csv)[1].Split(',');

            Assert.Equal(esperado, campos[2]);  // MONTOTOTAL
            Assert.Equal(esperado, campos[8]);  // EFECTIVO
        }

        [Fact]
        public void Montos_UsanPuntoDecimalPaseLoQuePase()
        {
            // Con cultura es-NI el separador decimal es la COMA. Formatear con la
            // cultura del servidor meteria "5100,00" y partiria la fila en dos. Va
            // InvariantCulture SIEMPRE.
            var previa = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture =
                    new System.Globalization.CultureInfo("es-NI");

                var csv = CoreCsvBuilder.Build(new[] { Tx(total: 5100m, cash: 5100m) }, Identity, Whitelist);

                Assert.Equal(14, Rows(csv)[1].Split(',').Length);
                Assert.Contains("5100.00", csv);
                Assert.DoesNotContain("5100,00", csv);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previa;
            }
        }

        [Fact]
        public void Cuenta_SeRellenaANueveDigitos()
        {
            var csv = CoreCsvBuilder.Build(new[] { Tx(account: 12345m) }, Identity, Whitelist);

            Assert.Equal("000012345", Rows(csv)[1].Split(',')[1]);
        }

        [Fact]
        public void Cuenta_VaciaSaleEnCeros()
        {
            var csv = CoreCsvBuilder.Build(new[] { Tx(account: null) }, Identity, Whitelist);

            Assert.Equal("000000000", Rows(csv)[1].Split(',')[1]);
        }

        [Fact]
        public void Cuenta_NoArrastraDecimales()
        {
            // sanic_accountnumber es decimal en Dataverse: llega 100200111.0 y el core
            // espera 100200111. Un ".0" pegado a la cuenta seria una cuenta inexistente.
            var csv = CoreCsvBuilder.Build(new[] { Tx(account: 100200111.0m) }, Identity, Whitelist);

            Assert.Equal("100200111", Rows(csv)[1].Split(',')[1]);
        }

        [Theory]
        [InlineData(CoreExportTransaction.CurrencyNio, "COR")]
        [InlineData(CoreExportTransaction.CurrencyUsd, "USD")]
        [InlineData(CoreExportTransaction.CurrencyEur, "EUR")]
        [InlineData(0, "")]
        public void Moneda_SeMapeaComoEnElFlujo(int codigo, string esperado)
        {
            var csv = CoreCsvBuilder.Build(new[] { Tx(currency: codigo) }, Identity, Whitelist);

            Assert.Equal(esperado, Rows(csv)[1].Split(',')[5]);
        }

        [Fact]
        public void Depositante_ConComa_NoAgregaColumnas()
        {
            // LA RAZON DE SER DE TODO ESTO. Antes esta fila salia con 15 campos y el
            // core leia la moneda en la columna del deposito.
            var csv = CoreCsvBuilder.Build(
                new[] { Tx(reference: "PEREZ, JUAN S.A.") }, Identity, Whitelist);
            var campos = Rows(csv)[1].Split(',');

            Assert.Equal(14, campos.Length);
            Assert.Equal("PEREZ JUAN S.A.", campos[4]);
            Assert.Equal("COR", campos[5]);
        }

        [Fact]
        public void Depositante_ConSaltoDeLinea_NoAgregaFilas()
        {
            var csv = CoreCsvBuilder.Build(
                new[] { Tx(reference: "PEREZ\r\nJUAN") }, Identity, Whitelist);

            Assert.Equal(2, Rows(csv).Length);
            Assert.Equal("PEREZ JUAN", Rows(csv)[1].Split(',')[4]);
        }

        [Fact]
        public void Depositante_SinReferencia_CaeAlNumeroDeTransaccion()
        {
            // Mismo coalesce que hacia el flujo.
            var csv = CoreCsvBuilder.Build(
                new[] { Tx(reference: null, transactionId: "6729") }, Identity, Whitelist);

            Assert.Equal("6729", Rows(csv)[1].Split(',')[4]);
        }

        [Fact]
        public void Depositante_QueSeSaneaHastaQuedarVacio_CaeAlNumeroDeTransaccion()
        {
            // Sin esto el core recibiria un depositante en blanco, que es peor que uno
            // imperfecto: el numero de transaccion al menos identifica el deposito.
            var csv = CoreCsvBuilder.Build(
                new[] { Tx(reference: ",,,", transactionId: "6729") }, Identity, Whitelist);

            Assert.Equal("6729", Rows(csv)[1].Split(',')[4]);
        }

        [Fact]
        public void Identidad_TambienSeSanea()
        {
            // La identidad es configuracion nuestra, pero una coma mal tipeada en el
            // parametro rompe TODAS las filas del archivo. Se sanea igual: la defensa no
            // depende de que nadie se equivoque cargando un parametro.
            var sucia = new CoreExportIdentity
            {
                TrnCode = "80,04",
                Manifiesto = "1",
                DisponibilidadInmediata = "0",
                TipoId = "0000000000001",
                Nacionalidad = "0000000000558",
                NumeroId1 = "4411001991",
                NumeroId2 = "003A",
            };

            var csv = CoreCsvBuilder.Build(new[] { Tx() }, sucia, Whitelist);

            Assert.Equal(14, Rows(csv)[1].Split(',').Length);
            Assert.Equal("80 04", Rows(csv)[1].Split(',')[0]);
        }

        [Fact]
        public void SinTransacciones_DevuelveSoloLaCabecera()
        {
            var csv = CoreCsvBuilder.Build(new List<CoreExportTransaction>(), Identity, Whitelist);

            Assert.Single(Rows(csv));
        }

        [Fact]
        public void TodasLasFilas_TienenExactamenteCatorceColumnas()
        {
            var txs = new[]
            {
                Tx(reference: "PEREZ, JUAN"),
                Tx(reference: "A\"B"),
                Tx(reference: "C\r\nD"),
                Tx(reference: null),
                Tx(reference: ";;;"),
            };

            var csv = CoreCsvBuilder.Build(txs, Identity, Whitelist);

            foreach (var fila in Rows(csv))
            {
                Assert.Equal(14, fila.Split(',').Length);
            }
        }
    }
}
