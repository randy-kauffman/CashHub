using System.Linq;
using CashHub.Validation.Export;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El parseo del "Resumen por Cuenta" que el aprobador copia del IBS Branch.
    ///
    /// POR QUE EXISTE. El aprobador no ve el detalle transaccion por transaccion: ve un
    /// resumen por cuenta y moneda, y tiene que compararlo contra el lote. Cuando un lote
    /// trae cientos de cuentas —el mas grande del entorno tiene 600— comparar a ojo es
    /// lento y se cometen errores justo donde no se pueden cometer.
    ///
    /// El texto llega pegado desde una grilla web, asi que puede venir con tabulaciones o
    /// con corridas de espacios, con encabezado o sin el, y con la columna "Select" que no
    /// aporta nada. El parser se ancla en la MONEDA, que es el unico token inequivoco de
    /// la fila: la cuenta es el token anterior, el monto el siguiente y la cantidad el
    /// ultimo. Asi no depende de cuantas columnas de adorno traiga el copiado.
    /// </summary>
    public class CoreSummaryParserTests
    {
        [Fact]
        public void Parse_LoPegadoDelNotepad_TalCualSeVeEnLaCaptura()
        {
            var pegado =
                "Detalle Cuenta   Moneda  Monto      Cant TRN\r\n" +
                "Select  000000070        COR     1,000,000.00     5";

            var r = CoreSummaryParser.Parse(pegado);

            var fila = Assert.Single(r.Rows);
            Assert.Equal("70", fila.Account);
            Assert.Equal("COR", fila.Currency);
            Assert.Equal(1000000.00m, fila.Amount);
            Assert.Equal(5, fila.Count);
            Assert.Empty(r.IgnoredLines);
        }

        [Fact]
        public void Parse_ConTabulaciones_EsElCopiadoDeLaGrillaWeb()
        {
            var r = CoreSummaryParser.Parse("Select\t000000070\tCOR\t1,000,000.00\t5");

            var fila = Assert.Single(r.Rows);
            Assert.Equal("70", fila.Account);
            Assert.Equal(1000000.00m, fila.Amount);
        }

        [Fact]
        public void Parse_SinEncabezadoYSinColumnaSelect()
        {
            var r = CoreSummaryParser.Parse("000000070 COR 1,000,000.00 5");

            Assert.Single(r.Rows);
            Assert.Equal("70", r.Rows[0].Account);
        }

        [Fact]
        public void Parse_VariasFilas()
        {
            var pegado =
                "Detalle\tCuenta\tMoneda\tMonto\tCant TRN\n" +
                "Select\t400004000\tCOR\t1,330.40\t1\n" +
                "Select\t400004003\tUSD\t1,741.33\t1\n" +
                "Select\t400004004\tEUR\t1,878.64\t1\n";

            var r = CoreSummaryParser.Parse(pegado);

            Assert.Equal(3, r.Rows.Count);
            Assert.Equal(new[] { "COR", "USD", "EUR" }, r.Rows.Select(x => x.Currency));
            Assert.Equal(1878.64m, r.Rows[2].Amount);
        }

        [Fact]
        public void Parse_LaCuentaSeNormalizaSinCerosALaIzquierda()
        {
            // El IBS muestra la cuenta con ceros a la izquierda ("000000070") y CashHub la
            // guarda como numero (70). Si no se normalizaran las dos puntas, TODAS las
            // cuentas darian "falta en el lote" y el comparador seria inutil.
            var r = CoreSummaryParser.Parse("000000070 COR 100.00 1\n70 USD 200.00 1");

            Assert.Equal("70", r.Rows[0].Account);
            Assert.Equal("70", r.Rows[1].Account);
        }

        [Fact]
        public void Parse_CuentaEnCeros_NoSeVuelveVacia()
        {
            var r = CoreSummaryParser.Parse("000000000 COR 100.00 1");

            Assert.Equal("0", Assert.Single(r.Rows).Account);
        }

        [Theory]
        [InlineData("1,000,000.00", 1000000.00)]
        [InlineData("1000000.00", 1000000.00)]
        [InlineData("1,330.40", 1330.40)]
        [InlineData("0.00", 0)]
        [InlineData("48,107.00", 48107.00)]
        public void Parse_MontosConSeparadorDeMiles(string texto, decimal esperado)
        {
            var r = CoreSummaryParser.Parse($"70 COR {texto} 1");

            Assert.Equal(esperado, Assert.Single(r.Rows).Amount);
        }

        [Theory]
        [InlineData("1.000.000,00", 1000000.00)]
        [InlineData("1.330,40", 1330.40)]
        public void Parse_MontosEnFormatoEuropeo_TambienSeEntienden(string texto, decimal esperado)
        {
            // Defensivo: el separador decimal es el ULTIMO de . o , que aparezca seguido de
            // uno o dos digitos. Si el IBS algun dia se configura en otra localizacion, el
            // comparador no empieza a mentir en silencio.
            var r = CoreSummaryParser.Parse($"70 COR {texto} 1");

            Assert.Equal(esperado, Assert.Single(r.Rows).Amount);
        }

        [Fact]
        public void Parse_LineasQueNoSeEntienden_SeReportanEnVezDeIgnorarseCallado()
        {
            // Que una linea se caiga sin avisar seria lo peor: el aprobador creeria que
            // comparo todo cuando en realidad comparo la mitad.
            var r = CoreSummaryParser.Parse("70 COR 100.00 1\nesto no es una fila\n80 USD 50.00 2");

            Assert.Equal(2, r.Rows.Count);
            Assert.Equal("esto no es una fila", Assert.Single(r.IgnoredLines));
        }

        [Fact]
        public void Parse_LineasVaciasYEncabezados_NoCuentanComoIgnoradas()
        {
            var r = CoreSummaryParser.Parse(
                "Detalle Cuenta Moneda Monto Cant TRN\n\n   \nSelect 70 COR 100.00 1\n");

            Assert.Single(r.Rows);
            Assert.Empty(r.IgnoredLines);
        }

        [Fact]
        public void Parse_TextoVacio_NoDevuelveFilasNiRompe()
        {
            Assert.Empty(CoreSummaryParser.Parse(null).Rows);
            Assert.Empty(CoreSummaryParser.Parse("").Rows);
            Assert.Empty(CoreSummaryParser.Parse("   \n  ").Rows);
        }

        [Fact]
        public void Parse_MonedaEnMinusculas_SeAcepta()
        {
            var r = CoreSummaryParser.Parse("70 cor 100.00 1");

            Assert.Equal("COR", Assert.Single(r.Rows).Currency);
        }

        [Fact]
        public void Parse_NioSeTratComoCor()
        {
            // Adentro de CashHub la moneda se llama NIO; el CSV al core la manda como COR.
            // Si alguien pega de una fuente que dice NIO, no se lo trata como otra moneda.
            var r = CoreSummaryParser.Parse("70 NIO 100.00 1");

            Assert.Equal("COR", Assert.Single(r.Rows).Currency);
        }

        [Fact]
        public void Parse_FilasRepetidasDeLaMismaCuentaYMoneda_SeSuman()
        {
            // El IBS no deberia repetirlas, pero si el copiado trae la misma cuenta dos
            // veces sumarlas es mas fiel que quedarse con una y descartar la otra.
            var r = CoreSummaryParser.Parse("70 COR 100.00 1\n70 COR 50.00 2");

            var fila = Assert.Single(r.Rows);
            Assert.Equal(150.00m, fila.Amount);
            Assert.Equal(3, fila.Count);
        }
    }
}
