using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// B-6 — numero de tula extraido del JSON de RequestResources.
    /// Los JSON de estos tests son los REALES de cada productor, tal cual la tabla
    /// de contrato de docs/supervisor-rebuild/03-pantalla-aprobaciones.md §0.
    ///
    /// La regla que se blinda aca: el espejo es opcional y NUNCA puede voltear el
    /// Create de una solicitud — todo lo que no parsea devuelve vacio, no excepcion.
    /// </summary>
    public class ApprovalBagNumberTests
    {
        private const string BagGuidText = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
        private const string ReceiptGuidText = "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d";

        // ---------- numero literal en el JSON ----------

        [Fact]
        public void AgregarTula_TomaElNumeroDeBagID()
        {
            var json = "{\"ReceiptGUID\":\"" + ReceiptGuidText + "\",\"BagID\":\"TULA-0042\","
                     + "\"BagGUID\":\"" + BagGuidText + "\",\"ReceptionBatchGUID\":\"" + ReceiptGuidText + "\"}";

            var source = ApprovalBagNumber.Resolve(json);

            Assert.Equal("TULA-0042", source.Number);
            Assert.False(source.IsEmpty);
        }

        [Fact]
        public void EditarTula_TomaElNumeroNUEVO_NoElGuid()
        {
            // El bug de la app vieja: GUID(ParseJSON(...).BagID). BagID es el NUMERO.
            var json = "{\"ReceiptGUID\":\"" + ReceiptGuidText + "\",\"BagID\":\"77\","
                     + "\"BagGUID\":\"" + BagGuidText + "\"}";

            var source = ApprovalBagNumber.Resolve(json);

            Assert.Equal("77", source.Number);
        }

        [Fact]
        public void NumeroNumericoSinComillas_TambienSeLee()
        {
            var source = ApprovalBagNumber.Resolve("{\"BagID\": 1234, \"BagGUID\": null}");

            Assert.Equal("1234", source.Number);
        }

        // ---------- solo GUID: lo resuelve el plugin con un Retrieve ----------

        [Fact]
        public void TulaConDiferencia_DevuelveElGuidParaResolver()
        {
            var source = ApprovalBagNumber.Resolve("{\"BagGUID\":\"" + BagGuidText + "\"}");

            Assert.Null(source.Number);
            Assert.Equal(Guid.Parse(BagGuidText), source.BagId);
            Assert.False(source.IsEmpty);
        }

        [Fact]
        public void RemapearTula_UsaLaClaveGUID_YNoConfundeConReceiptGUID()
        {
            // "ReceiptGUID" contiene la subcadena GUID: un IndexOf pelado la matchea.
            var json = "{\"GUID\":\"" + BagGuidText + "\",\"ReceiptID\":\"R-1\","
                     + "\"ReceiptGUID\":\"" + ReceiptGuidText + "\","
                     + "\"BagPreviousState\":\"Recibida en esclusa\"}";

            var source = ApprovalBagNumber.Resolve(json);

            Assert.Equal(Guid.Parse(BagGuidText), source.BagId);
        }

        [Fact]
        public void BagGuidTienePrioridadSobreGuid()
        {
            var json = "{\"GUID\":\"" + ReceiptGuidText + "\",\"BagGUID\":\"" + BagGuidText + "\"}";

            var source = ApprovalBagNumber.Resolve(json);

            Assert.Equal(Guid.Parse(BagGuidText), source.BagId);
        }

        [Fact]
        public void BagIdConUnGuidAdentro_SeTrataComoGuid_NoComoNumero()
        {
            // Defensa: un GUID en la columna de busqueda seria basura para el usuario.
            var source = ApprovalBagNumber.Resolve("{\"BagID\":\"" + BagGuidText + "\"}");

            Assert.Null(source.Number);
            Assert.Equal(Guid.Parse(BagGuidText), source.BagId);
        }

        // ---------- sin tula: espejo en blanco, sin drama ----------

        [Fact]
        public void CrearSesionDeRecuento_NoHablaDeNingunaTula()
        {
            var source = ApprovalBagNumber.Resolve(
                "{\"RecountingSessionGUID\":\"" + ReceiptGuidText + "\"}");

            Assert.True(source.IsEmpty);
        }

        [Fact]
        public void ModificarNumeroDePlanilla_NoHablaDeNingunaTula()
        {
            var source = ApprovalBagNumber.Resolve(
                "{\"ReceiptGUID\":\"" + ReceiptGuidText + "\",\"NewReceiptID\":\"R-9\"}");

            Assert.True(source.IsEmpty);
        }

        // ---------- JSON roto: NUNCA lanza ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no es json")]
        [InlineData("{")]
        [InlineData("{\"BagID\":")]
        [InlineData("{\"BagID\":\"sin cerrar")]
        [InlineData("<html>RequestDetails pegado en la columna equivocada</html>")]
        [InlineData("[\"un\",\"arreglo\"]")]
        [InlineData("{\"BagID\":{\"anidado\":\"objeto\"}}")]
        [InlineData("{\"BagID\":null}")]
        public void JsonRotoOAusente_DevuelveVacioSinLanzar(string? json)
        {
            var source = ApprovalBagNumber.Resolve(json);

            Assert.True(source.IsEmpty);
        }

        [Fact]
        public void JsonTruncadoDespuesDeUnaClaveValida_IgualRescataLoQueHay()
        {
            // El JSON esta roto, pero el numero ya se leyo entero: no se tira.
            var source = ApprovalBagNumber.Resolve("{\"BagID\":\"TULA-9\",\"BagGUID\":\"3f25");

            Assert.Equal("TULA-9", source.Number);
        }

        // ---------- normalizacion ----------

        [Fact]
        public void Numero_SeRecortaAlLargoDeLaColumna()
        {
            var largo = new string('9', ApprovalBagNumber.MaxLength + 40);

            var source = ApprovalBagNumber.Resolve("{\"BagID\":\"" + largo + "\"}");

            Assert.Equal(ApprovalBagNumber.MaxLength, source.Number!.Length);
        }

        [Fact]
        public void Numero_SeLimpiaDeEspacios()
        {
            var source = ApprovalBagNumber.Resolve("{\"BagID\":\"  TULA-1  \"}");

            Assert.Equal("TULA-1", source.Number);
        }

        [Fact]
        public void NumeroVacio_NoEstampaNada()
        {
            var source = ApprovalBagNumber.Resolve("{\"BagID\":\"   \"}");

            Assert.True(source.IsEmpty);
        }
    }
}
