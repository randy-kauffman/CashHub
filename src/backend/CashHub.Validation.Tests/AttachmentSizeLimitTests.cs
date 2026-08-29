using CashHub.Validation.Parsing;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Resolucion del tope de tamaño descomprimido. El hardcodeado (100 MB) es a la vez el
    /// default cuando el parametro falta y el TECHO ABSOLUTO: el parametro de CH-Tbl-Parameter
    /// solo puede BAJAR el limite, nunca subirlo. Asi un "999999" en la tabla no reabre el
    /// agujero — se clampea al techo. Y una tabla sin la fila no puede desactivar el guardia.
    /// </summary>
    public class AttachmentSizeLimitTests
    {
        private const long Techo = 100L * 1024 * 1024;

        [Fact]
        public void Sin_parametro_usa_el_default_hardcodeado()
        {
            Assert.Equal(Techo, XlsxParser.ResolveMaxUncompressedBytes(null));
        }

        [Fact]
        public void Un_parametro_menor_al_techo_baja_el_limite()
        {
            var diezMb = 10L * 1024 * 1024;
            Assert.Equal(diezMb, XlsxParser.ResolveMaxUncompressedBytes(diezMb));
        }

        [Fact]
        public void Un_parametro_mayor_al_techo_se_clampea_al_techo()
        {
            // El caso que importa: alguien pone un numero enorme en la tabla para "permitir
            // archivos grandes". No puede pasar del techo hardcodeado.
            Assert.Equal(Techo, XlsxParser.ResolveMaxUncompressedBytes(999_999L * 1024 * 1024));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Un_parametro_cero_o_negativo_cae_al_default_no_desactiva_el_guardia(long bytes)
        {
            // Un 0 apagaria el guardia (todo archivo lo excede) o lo dejaria sin sentido.
            // Se trata como invalido: vuelve al default. Nunca fail-open.
            Assert.Equal(Techo, XlsxParser.ResolveMaxUncompressedBytes(bytes));
        }
    }
}
