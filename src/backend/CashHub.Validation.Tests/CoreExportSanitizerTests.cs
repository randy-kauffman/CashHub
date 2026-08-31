using CashHub.Validation.Export;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El saneo de los campos de texto libre que van al CSV del core.
    ///
    /// POR QUE EXISTE. El CSV al core se genera separado por comas y SIN entrecomillar
    /// nada: cero comillas dobles en el archivo real. Un depositante llamado
    /// "PEREZ, JUAN" parte la fila en dos y corre TODAS las columnas siguientes una
    /// posicion: MONEDA se lleva el valor de DEPOSITID, EFECTIVO se lleva otro. La fila
    /// no revienta — sigue pareciendo valida y postea mal. Un archivo que falla se ve;
    /// uno que postea mal no se ve hasta que alguien cuadra.
    ///
    /// La lista de caracteres permitidos es un PARAMETRO, no una constante, porque la
    /// misma regla la va a usar despues la validacion de entrada para rebotar la
    /// transaccion en origen. Una sola regla, dos defensas.
    /// </summary>
    public class CoreExportSanitizerTests
    {
        private const string Whitelist =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .-";

        [Fact]
        public void Sanitize_SinCaracteresProhibidos_DevuelveElMismoTexto()
        {
            Assert.Equal("SRC-80841988", CoreExportSanitizer.Sanitize("SRC-80841988", Whitelist));
        }

        [Fact]
        public void Sanitize_ConComa_LaReemplazaPorEspacio()
        {
            // El caso que rompe el archivo.
            Assert.Equal("PEREZ JUAN", CoreExportSanitizer.Sanitize("PEREZ, JUAN", Whitelist));
        }

        [Fact]
        public void Sanitize_ConComillaDoble_LaSaca()
        {
            Assert.Equal("DISTRIBUIDORA X", CoreExportSanitizer.Sanitize("DISTRIBUIDORA \"X\"", Whitelist));
        }

        [Theory]
        [InlineData("LINEA1\nLINEA2")]
        [InlineData("LINEA1\r\nLINEA2")]
        [InlineData("LINEA1\rLINEA2")]
        public void Sanitize_ConSaltosDeLinea_NoDejaNingunoVivo(string entrada)
        {
            // Un salto de linea no corre columnas: parte la fila en DOS FILAS. Es peor
            // que la coma, porque el archivo cambia de cantidad de registros.
            var salida = CoreExportSanitizer.Sanitize(entrada, Whitelist);

            Assert.DoesNotContain("\n", salida);
            Assert.DoesNotContain("\r", salida);
            Assert.Equal("LINEA1 LINEA2", salida);
        }

        [Fact]
        public void Sanitize_ConPuntoYComa_LoSaca()
        {
            // Hoy el separador es la coma, pero si algun dia el core pide punto y coma,
            // la lista blanca ya lo cubre sin tocar codigo.
            Assert.Equal("A B", CoreExportSanitizer.Sanitize("A;B", Whitelist));
        }

        [Fact]
        public void Sanitize_ColapsaEspaciosYRecorta()
        {
            // "PEREZ, JUAN" -> reemplazar la coma deja DOS espacios seguidos. Se colapsan
            // para no mandar al core un nombre con separacion rara.
            Assert.Equal("PEREZ JUAN", CoreExportSanitizer.Sanitize("  PEREZ,  JUAN  ", Whitelist));
        }

        [Fact]
        public void Sanitize_TodoProhibido_DevuelveVacio()
        {
            // No inventa contenido: si no queda nada, no queda nada. Quien llama decide
            // el reemplazo (en el CSV, el numero de transaccion).
            Assert.Equal(string.Empty, CoreExportSanitizer.Sanitize(",,,", Whitelist));
        }

        [Fact]
        public void Sanitize_TextoVacioONulo_DevuelveVacio()
        {
            Assert.Equal(string.Empty, CoreExportSanitizer.Sanitize(null, Whitelist));
            Assert.Equal(string.Empty, CoreExportSanitizer.Sanitize("", Whitelist));
        }

        [Fact]
        public void Sanitize_ListaBlancaVacia_NoDejaPasarNADA()
        {
            // DEFAULT-DENY. Una lista blanca vacia significa "no permito nada", no
            // "permito todo". Si se interpretara al reves, un parametro borrado por
            // error desactivaria la defensa justo cuando mas hace falta.
            Assert.Equal(string.Empty, CoreExportSanitizer.Sanitize("PEREZ", ""));
            Assert.Equal(string.Empty, CoreExportSanitizer.Sanitize("PEREZ", null));
        }

        [Fact]
        public void Sanitize_RespetaAcentosYEnieSiEstanEnLaLista()
        {
            // Los nombres nicaraguenses llevan Ñ y tildes. Si la lista los incluye, NO
            // se tocan: mutilar "PEÑA" a "PE A" seria corromper el dato para protegerlo.
            var conEspanol = Whitelist + "ÑñÁÉÍÓÚáéíóú";

            Assert.Equal("PEÑA GARCÍA", CoreExportSanitizer.Sanitize("PEÑA GARCÍA", conEspanol));
        }

        [Fact]
        public void Sanitize_SinAcentosEnLaLista_LosReemplaza()
        {
            // Y si el core NO acepta esos caracteres, la lista los deja afuera y el
            // saneo actua. La decision es del parametro, no del codigo.
            Assert.Equal("PE A GARC A", CoreExportSanitizer.Sanitize("PEÑA GARCÍA", Whitelist));
        }

        [Fact]
        public void EsLimpio_DetectaSinModificar()
        {
            // Lo que la validacion de entrada va a usar para REBOTAR en origen, en vez
            // de sanear. Misma lista, misma regla, distinta reaccion.
            Assert.True(CoreExportSanitizer.IsClean("PEREZ JUAN", Whitelist));
            Assert.False(CoreExportSanitizer.IsClean("PEREZ, JUAN", Whitelist));
        }

        [Fact]
        public void CaracteresProhibidos_LosListaParaPoderNombrarlosEnElError()
        {
            // Un mensaje que dice "hay un caracter no permitido" no sirve: el operador
            // tiene que saber CUAL sacar.
            Assert.Equal(new[] { ',', '#' }, CoreExportSanitizer.ForbiddenCharacters("PEREZ, JUAN #1", Whitelist));
        }

        [Fact]
        public void CaracteresProhibidos_NoRepiteElMismoCaracter()
        {
            Assert.Equal(new[] { ',' }, CoreExportSanitizer.ForbiddenCharacters("A,B,C,D", Whitelist));
        }
    }
}
