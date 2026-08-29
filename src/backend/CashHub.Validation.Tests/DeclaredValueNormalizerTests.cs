using CashHub.Validation.Models;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Normalizacion de los valores que se GUARDAN de la declaracion.
    ///
    /// Vive en el dominio y no en DeclarationMaterializer por la misma razon que
    /// RecordVigencyMap: CashHub.Plugins es net462 y CrmSdk.CoreAssemblies solo publica
    /// net462, asi que lo que quede alli no se puede testear. Y ademas VR-29 tiene que
    /// validar EXACTAMENTE lo mismo que el materializador guarda — si la regla acepta
    /// " 123 " y el materializador guarda otra cosa, valida una cosa y persiste otra.
    /// </summary>
    public class DeclaredValueNormalizerTests
    {
        // ---------------------------------------------------------------------
        // Numero de tula: trim + MAYUSCULAS, siempre.
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData("tula-01", "TULA-01")]
        [InlineData("  tula-01  ", "TULA-01")]
        [InlineData("TULA-01", "TULA-01")]
        [InlineData("TuLa-01", "TULA-01")]
        [InlineData("b-100", "B-100")]
        public void El_numero_de_tula_se_guarda_trimmeado_y_en_mayusculas(string raw, string esperado)
        {
            Assert.Equal(esperado, DeclaredValueNormalizer.BagNumber(raw));
        }

        [Fact]
        public void Dos_tulas_que_solo_difieren_en_capitalizacion_normalizan_igual()
        {
            // Antes se agrupaban con OrdinalIgnoreCase pero se GUARDABA la capitalizacion
            // de la primera fila del grupo: el mismo numero de tula quedaba escrito
            // distinto segun el orden de las filas del Excel.
            Assert.Equal(DeclaredValueNormalizer.BagNumber("tula-7"),
                         DeclaredValueNormalizer.BagNumber("  TULA-7 "));
        }

        [Fact]
        public void La_tula_usa_mayusculas_invariantes()
        {
            // ToUpper() con cultura turca convierte 'i' en 'İ' (I con punto) y cambia el
            // numero de tula segun donde corra el plugin. ToUpperInvariant no.
            Assert.Equal("BIN-1", DeclaredValueNormalizer.BagNumber("bin-1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Una_tula_vacia_normaliza_a_vacio_sin_reventar(string? raw)
        {
            Assert.Equal(string.Empty, DeclaredValueNormalizer.BagNumber(raw));
        }

        // ---------------------------------------------------------------------
        // Numero de cheque: trim, sin tocar el contenido.
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData("  001234  ", "001234")]
        [InlineData("1234", "1234")]
        public void El_numero_de_cheque_se_guarda_trimmeado(string raw, string esperado)
        {
            Assert.Equal(esperado, DeclaredValueNormalizer.CheckNumber(raw));
        }

        [Fact]
        public void El_numero_de_cheque_conserva_los_ceros_a_la_izquierda()
        {
            // Por esto se guarda como TEXTO: 0001234 y 1234 son cheques distintos, y en
            // un entero son el mismo numero.
            Assert.Equal("0001234", DeclaredValueNormalizer.CheckNumber("0001234"));
        }

        [Fact]
        public void El_numero_de_cheque_no_se_pasa_a_mayusculas()
        {
            // No hace falta: VR-29 garantiza que sean solo digitos. Si algun dia entrara
            // una letra, mayusculizarla la escondaria en vez de rechazarla.
            Assert.Equal("a1", DeclaredValueNormalizer.CheckNumber(" a1 "));
        }
    }
}
