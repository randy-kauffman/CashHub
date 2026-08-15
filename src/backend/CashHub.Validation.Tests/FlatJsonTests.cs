using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Lector de JSON plano usado por los espejos. Lo que se blinda aca es lo que
    /// un IndexOf pelado hace mal: confundir una clave con una subcadena de otra
    /// clave, o con texto que vive DENTRO de un valor.
    /// </summary>
    public class FlatJsonTests
    {
        [Fact]
        public void LeeUnaClaveDeCadena()
        {
            Assert.Equal("R-1", FlatJson.Value("{\"ReceiptID\":\"R-1\"}", "ReceiptID"));
        }

        [Fact]
        public void LaClaveEsCaseInsensitive()
        {
            Assert.Equal("R-1", FlatJson.Value("{\"receiptid\":\"R-1\"}", "ReceiptID"));
        }

        [Fact]
        public void NoConfundeUnaClaveConElSufijoDeOtra()
        {
            // "ReceiptGUID" termina en "GUID": pedir "GUID" NO puede devolver su valor.
            var json = "{\"ReceiptGUID\":\"del-recibo\",\"GUID\":\"de-la-tula\"}";

            Assert.Equal("de-la-tula", FlatJson.Value(json, "GUID"));
        }

        [Fact]
        public void ClaveAusenteEsNull()
        {
            var json = "{\"ReceiptGUID\":\"del-recibo\"}";

            Assert.Null(FlatJson.Value(json, "GUID"));
        }

        [Fact]
        public void NoConfundeUnValorDeTextoConUnaClave()
        {
            // El texto "BagID" vive DENTRO de un valor: no es una clave.
            var json = "{\"Nota\":\"BagID\",\"BagID\":\"77\"}";

            Assert.Equal("77", FlatJson.Value(json, "BagID"));
        }

        [Fact]
        public void LeeLiteralesNumericosYBooleanos()
        {
            var json = "{\"n\": 42 , \"b\": true }";

            Assert.Equal("42", FlatJson.Value(json, "n"));
            Assert.Equal("true", FlatJson.Value(json, "b"));
        }

        [Fact]
        public void NullYEstructurasDevuelvenNull()
        {
            Assert.Null(FlatJson.Value("{\"a\":null}", "a"));
            Assert.Null(FlatJson.Value("{\"a\":{\"b\":1}}", "a"));
            Assert.Null(FlatJson.Value("{\"a\":[1,2]}", "a"));
        }

        [Fact]
        public void SaltaObjetosAnidadosParaLlegarALaClaveSiguiente()
        {
            var json = "{\"meta\":{\"BagID\":\"impostor\"},\"BagID\":\"real\"}";

            // El objeto anidado se saltea ENTERO: su "BagID" no es candidato, y la
            // clave que gana es la del nivel de arriba. Un IndexOf pelado devolveria
            // "impostor" porque aparece antes en el texto.
            Assert.Equal("real", FlatJson.Value(json, "BagID"));
        }

        [Fact]
        public void DesEscapaComillasYUnicode()
        {
            var json = "{\"a\":\"con \\\"comillas\\\" y \\u00f1\"}";

            Assert.Equal("con \"comillas\" y ñ", FlatJson.Value(json, "a"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{\"a\":")]
        [InlineData("{\"a\":\"sin cerrar")]
        [InlineData("basura sin comillas")]
        public void EntradaRotaDevuelveNullSinLanzar(string? json)
        {
            Assert.Null(FlatJson.Value(json, "a"));
        }
    }
}
