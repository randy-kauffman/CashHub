using System.Linq;
using CashHub.Validation.Export;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// La comparacion del lote de CashHub contra el "Resumen por Cuenta" del IBS.
    ///
    /// El veredicto es POR CUENTA, no global. Un "no cuadra" a secas obliga al aprobador a
    /// buscar la aguja a mano, que es exactamente el trabajo que esto viene a evitar: tiene
    /// que decir QUE cuenta, en QUE moneda y POR CUANTO.
    /// </summary>
    public class CoreSummaryComparerTests
    {
        private static CoreSummaryRow F(string cuenta, string moneda, decimal monto, int cant) =>
            new CoreSummaryRow { Account = cuenta, Currency = moneda, Amount = monto, Count = cant };

        [Fact]
        public void TodoIgual_EsMatch()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 1000m, 5), F("80", "USD", 250.50m, 2) },
                new[] { F("80", "USD", 250.50m, 2), F("70", "COR", 1000m, 5) });

            Assert.True(r.IsMatch);
            Assert.All(r.Rows, x => Assert.Equal(CoreSummaryStatus.Match, x.Status));
        }

        [Fact]
        public void ElOrdenNoImporta()
        {
            // El copiado del IBS no tiene por que venir en el mismo orden que el lote.
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 100m, 1), F("80", "COR", 200m, 1) },
                new[] { F("80", "COR", 200m, 1), F("70", "COR", 100m, 1) });

            Assert.True(r.IsMatch);
        }

        [Fact]
        public void MontoDistinto_LoDiceConLaCuentaYLaDiferencia()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 1000m, 5) },
                new[] { F("70", "COR", 900m, 5) });

            Assert.False(r.IsMatch);
            var fila = Assert.Single(r.Rows);
            Assert.Equal(CoreSummaryStatus.AmountDiffers, fila.Status);
            Assert.Equal("70", fila.Account);
            Assert.Equal(1000m, fila.BatchAmount);
            Assert.Equal(900m, fila.CoreAmount);
            Assert.Equal(100m, fila.AmountDifference);
        }

        [Fact]
        public void CantidadDistinta_SeDetectaAunqueElMontoCuadre()
        {
            // El caso traicionero: el dinero da igual pero hay una transaccion de mas o de
            // menos. Si solo se comparara el monto, pasaria como correcto.
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 1000m, 5) },
                new[] { F("70", "COR", 1000m, 4) });

            Assert.False(r.IsMatch);
            var fila = Assert.Single(r.Rows);
            Assert.Equal(CoreSummaryStatus.CountDiffers, fila.Status);
            Assert.Equal(5, fila.BatchCount);
            Assert.Equal(4, fila.CoreCount);
        }

        [Fact]
        public void MontoYCantidadDistintos_SeReportanLosDos()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 1000m, 5) },
                new[] { F("70", "COR", 900m, 4) });

            Assert.Equal(CoreSummaryStatus.AmountAndCountDiffer, Assert.Single(r.Rows).Status);
        }

        [Fact]
        public void CuentaQueEstaEnElLoteYNoEnElIBS()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 100m, 1), F("80", "COR", 200m, 1) },
                new[] { F("70", "COR", 100m, 1) });

            Assert.False(r.IsMatch);
            var falta = r.Rows.Single(x => x.Account == "80");
            Assert.Equal(CoreSummaryStatus.MissingInCore, falta.Status);
            Assert.Equal(200m, falta.BatchAmount);
            Assert.Equal(0m, falta.CoreAmount);
        }

        [Fact]
        public void CuentaQueEstaEnElIBSYNoEnElLote()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 100m, 1) },
                new[] { F("70", "COR", 100m, 1), F("99", "COR", 500m, 3) });

            Assert.False(r.IsMatch);
            var sobra = r.Rows.Single(x => x.Account == "99");
            Assert.Equal(CoreSummaryStatus.MissingInBatch, sobra.Status);
            Assert.Equal(500m, sobra.CoreAmount);
        }

        [Fact]
        public void LaMismaCuentaEnDosMonedas_SonDosFilasIndependientes()
        {
            // La clave es cuenta + MONEDA. Sin la moneda, una cuenta con cordobas y dolares
            // se sumaria en una sola linea y el descuadre quedaria escondido.
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 100m, 1), F("70", "USD", 50m, 1) },
                new[] { F("70", "COR", 100m, 1), F("70", "USD", 999m, 1) });

            Assert.False(r.IsMatch);
            Assert.Equal(CoreSummaryStatus.Match,
                r.Rows.Single(x => x.Account == "70" && x.Currency == "COR").Status);
            Assert.Equal(CoreSummaryStatus.AmountDiffers,
                r.Rows.Single(x => x.Account == "70" && x.Currency == "USD").Status);
        }

        [Fact]
        public void ElResumenVacioDelIBS_NoSeReportaComoMatch()
        {
            // Pegar nada y que diga "coincide" seria la peor mentira posible.
            var r = CoreSummaryComparer.Compare(new[] { F("70", "COR", 100m, 1) }, new CoreSummaryRow[0]);

            Assert.False(r.IsMatch);
            Assert.Equal(CoreSummaryStatus.MissingInCore, Assert.Single(r.Rows).Status);
        }

        [Fact]
        public void LoteVacioYResumenVacio_NoEsMatch()
        {
            // Comparar nada contra nada no prueba nada; decir que cuadra seria falso.
            var r = CoreSummaryComparer.Compare(new CoreSummaryRow[0], new CoreSummaryRow[0]);

            Assert.False(r.IsMatch);
            Assert.Empty(r.Rows);
        }

        [Fact]
        public void LasDiferenciasVanPRIMERO_ElOperadorNoTieneQueBuscarlas()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("10", "COR", 100m, 1), F("20", "COR", 200m, 1), F("30", "COR", 300m, 1) },
                new[] { F("10", "COR", 100m, 1), F("20", "COR", 999m, 1), F("30", "COR", 300m, 1) });

            Assert.Equal("20", r.Rows[0].Account);
            Assert.NotEqual(CoreSummaryStatus.Match, r.Rows[0].Status);
        }

        [Fact]
        public void Totales_ResumenDeLoQuePasoParaNoTenerQueContarAMano()
        {
            var r = CoreSummaryComparer.Compare(
                new[] { F("10", "COR", 100m, 1), F("20", "COR", 200m, 1) },
                new[] { F("10", "COR", 100m, 1), F("20", "COR", 999m, 1) });

            Assert.Equal(2, r.TotalAccounts);
            Assert.Equal(1, r.MatchingAccounts);
            Assert.Equal(1, r.DifferingAccounts);
        }

        [Fact]
        public void DiferenciasDeCentavos_CUENTAN()
        {
            // Nada de tolerancias. Un centavo de diferencia en un archivo contable es una
            // diferencia: si se perdonara, se estaria decidiendo por el banco que ese
            // centavo no importa.
            var r = CoreSummaryComparer.Compare(
                new[] { F("70", "COR", 1000.01m, 1) },
                new[] { F("70", "COR", 1000.00m, 1) });

            Assert.False(r.IsMatch);
            Assert.Equal(0.01m, Assert.Single(r.Rows).AmountDifference);
        }
    }

    /// <summary>
    /// El resumen se arma de las MISMAS transacciones que generan el CSV. Si saliera por
    /// otro camino podria cuadrar contra el IBS mientras el archivo enviado dice otra cosa.
    /// </summary>
    public class CoreSummaryBuilderTests
    {
        private static CoreExportTransaction Tx(decimal cuenta, int moneda, decimal total, decimal efectivo) =>
            new CoreExportTransaction
            {
                AccountNumber = cuenta,
                Currency = moneda,
                Total = total,
                CashAmount = efectivo,
                TransactionId = "1",
            };

        [Fact]
        public void AgrupaPorCuentaYMoneda_SumandoYContando()
        {
            var r = CoreSummaryBuilder.FromTransactions(new[]
            {
                Tx(400004000m, CoreExportTransaction.CurrencyNio, 1330.40m, 1330.40m),
                Tx(400004000m, CoreExportTransaction.CurrencyNio, 669.60m, 669.60m),
                Tx(400004003m, CoreExportTransaction.CurrencyUsd, 1741.33m, 1741.33m),
            });

            Assert.Equal(2, r.Count);
            var cor = r.Single(x => x.Currency == "COR");
            Assert.Equal("400004000", cor.Account);
            Assert.Equal(2000.00m, cor.Amount);
            Assert.Equal(2, cor.Count);
        }

        [Fact]
        public void SumaELMONTOTOTAL_NoElEfectivo()
        {
            // CONFIRMADO CON EL USUARIO: el "Monto" del Resumen por Cuenta del IBS es el
            // monto total. En los lotes con cheques total y efectivo difieren —la
            // diferencia es exactamente el cheque— asi que elegir mal haria que el
            // comparador marcara diferencias falsas justo en esos lotes.
            var r = CoreSummaryBuilder.FromTransactions(new[]
            {
                Tx(70m, CoreExportTransaction.CurrencyNio, 48107.00m, 45038.64m),
            });

            Assert.Equal(48107.00m, Assert.Single(r).Amount);
        }

        [Fact]
        public void LaCuentaSeNormalizaIgualQueLaDelIBS()
        {
            // Del lado de CashHub la cuenta es numerica (70); del lado del IBS viene
            // "000000070". Las dos puntas pasan por la misma normalizacion.
            var r = CoreSummaryBuilder.FromTransactions(new[]
            {
                Tx(70m, CoreExportTransaction.CurrencyNio, 100m, 100m),
            });

            Assert.Equal("70", Assert.Single(r).Account);
            Assert.Equal(CoreSummaryParser.Parse("000000070 COR 100.00 1").Rows[0].Account,
                r[0].Account);
        }

        [Fact]
        public void LaMismaCuentaEnDosMonedas_SonDosFilas()
        {
            var r = CoreSummaryBuilder.FromTransactions(new[]
            {
                Tx(70m, CoreExportTransaction.CurrencyNio, 100m, 100m),
                Tx(70m, CoreExportTransaction.CurrencyUsd, 50m, 50m),
            });

            Assert.Equal(2, r.Count);
        }

        [Fact]
        public void SinTransacciones_ResumenVacio()
        {
            Assert.Empty(CoreSummaryBuilder.FromTransactions(new CoreExportTransaction[0]));
            Assert.Empty(CoreSummaryBuilder.FromTransactions(null));
        }
    }
}
