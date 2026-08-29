using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// VR-29: el numero de cheque son SOLO DIGITOS.
    ///
    /// El campo serial number de la linea MICR contiene unicamente digitos — los simbolos
    /// que se ven impresos son delimitadores, no parte del numero. Wells Fargo especifica
    /// 4 a 10 digitos; el estandar del Reserve Bank of India, 6. Ni letras ni guiones.
    ///
    /// No alcanza con meter "No. Cheque" en Checks.NumericColumns (VR-17): esa regla usa
    /// TryDecimal, que aceptaria 1234.56, -5 y 1e5. "Solo digitos" es mas estricto y es
    /// lo correcto para un serial.
    /// </summary>
    public class Vr29CheckNumberTests
    {
        private static RuleDefinition Def(string? msg = null) =>
            new("VR-29", "Numero de cheque numerico", 175, true, null, msg, "VR-15");

        private static ValidationContext Ctx(params string[] numerosDeCheque)
        {
            var filas = numerosDeCheque
                .Select(n => new[] { "1", n, "BAC", "USD", "100" })
                .ToArray();

            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100200300", "USD", "100", "0", "0", "100", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns, filas)
                .Build();

            return new ValidationContext(new EmailEnvelope(), new FakeDataAccess()) { Workbook = wb };
        }

        [Theory]
        [InlineData("1234")]
        [InlineData("0001234")]      // ceros a la izquierda: valido y significativo
        [InlineData("000000")]
        [InlineData("1234567890")]   // 10 digitos, el tope de la especificacion de Wells Fargo
        public void Acepta_numeros_de_cheque_de_solo_digitos(string numero)
        {
            Assert.Empty(new Vr29CheckNumbersAreDigits().Evaluate(Def(), Ctx(numero)));
        }

        [Theory]
        [InlineData("ABC123")]
        [InlineData("123-456")]      // guion: no existe en el campo serial del MICR
        [InlineData("12 34")]        // espacio interno
        [InlineData("1234.56")]      // decimal: TryDecimal de VR-17 lo dejaria pasar
        [InlineData("-5")]           // negativo: idem
        [InlineData("1e5")]          // notacion cientifica: idem
        [InlineData("١٢٣")]          // digitos arabigo-indicos: char.IsDigit los aceptaria
        public void Rechaza_cualquier_cosa_que_no_sean_digitos_ascii(string numero)
        {
            var finding = new Vr29CheckNumbersAreDigits().Evaluate(Def(), Ctx(numero)).Single();

            Assert.Equal(numero, finding.ActualValue);
            Assert.Equal("Cheques", finding.Sheet);
        }

        [Fact]
        public void Tolera_espacios_alrededor_porque_es_lo_que_el_materializador_guarda()
        {
            // La regla valida el valor NORMALIZADO, el mismo que persiste
            // DeclarationMaterializer. Si validara el crudo, un espacio al final —
            // cosmetico y comunisimo en Excel — rechazaria una declaracion legitima.
            Assert.Empty(new Vr29CheckNumbersAreDigits().Evaluate(Def(), Ctx("  1234  ")));
        }

        [Fact]
        public void No_opina_sobre_celdas_vacias()
        {
            // De los cheques incompletos ya se ocupa VR-15, que ademas es su dependencia.
            // Reportar dos veces el mismo problema llena el correo de ruido.
            Assert.Empty(new Vr29CheckNumbersAreDigits().Evaluate(Def(), Ctx("")));
        }

        [Fact]
        public void Reporta_una_fila_por_cada_cheque_malo_con_su_ubicacion()
        {
            var findings = new Vr29CheckNumbersAreDigits().Evaluate(Def(), Ctx("1234", "ABC", "56X"));

            Assert.Equal(2, findings.Count);
            Assert.Equal(new[] { "ABC", "56X" }, findings.Select(f => f.ActualValue));
            Assert.All(findings, f => Assert.NotNull(f.CellRef));
            Assert.All(findings, f => Assert.NotNull(f.RowNumber));
        }

        [Fact]
        public void Esta_registrada_en_el_motor()
        {
            Assert.Contains(FullRuleSet.All, r => r.Key == "VR-29");
        }
    }
}
