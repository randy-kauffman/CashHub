using System;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del contrato de sanic_RequestResources (H-08.3).
    ///
    /// Hoy esto MIDE, no bloquea: los productores canvas son heterogeneos y armar el JSON
    /// por concatenacion de strings garantiza desvios. La inspeccion existe para que el
    /// guard trace una linea estable por creacion y se pueda decidir con datos cuando
    /// endurecer. Lo que si es de seguridad es lo que sale de aca: el GUID de la sesion
    /// de recuento que el guard verifica contra el cajero (H-08).
    /// </summary>
    public class ApprovalRequestResourcesTests
    {
        private const string SesionGuid = "6f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f607";

        /// <summary>JSON tal cual lo arma Recounting Screen1 (verificado contra el productor).</summary>
        private const string JsonSesionReal = "{\"RecountingSessionGUID\":\"" + SesionGuid + "\"}";

        // ---- El caso que sostiene el control de H-08 ----

        [Fact]
        public void Sesion_ConElJsonRealDelProductor_EntregaElGuid()
        {
            var inspection = ApprovalRequestResources.Inspect("Crear sesion de recuento", JsonSesionReal);

            Assert.True(inspection.IsCompliant);
            Assert.Equal(SesionGuid, inspection.Value(ApprovalRequestResources.RecountingSessionGuidKey));
        }

        [Theory]
        [InlineData("{\"RecountingSessionGUID\":\"" + SesionGuid)]        // truncado
        [InlineData("{RecountingSessionGUID:" + SesionGuid + "}")]        // sin comillas
        [InlineData("<p>Solicitada por Fulano</p>")]                      // HTML en la columna equivocada
        [InlineData("")]
        [InlineData(null)]
        public void Sesion_ConJsonIlegible_NoEntregaGuidNiRompe(string? json)
        {
            // Sin GUID legible el guard no puede verificar de quien es la sesion:
            // la solicitud tiene que nacer PENDIENTE (nunca aprobada por defecto).
            var inspection = ApprovalRequestResources.Inspect("Crear sesion de recuento", json);

            Assert.Null(inspection.Value(ApprovalRequestResources.RecountingSessionGuidKey));
            Assert.False(inspection.IsCompliant);
        }

        [Fact]
        public void Sesion_ConGuidVacio_NoEntregaGuid()
        {
            var inspection = ApprovalRequestResources.Inspect(
                "Crear sesion de recuento", "{\"RecountingSessionGUID\":\"\"}");

            Assert.Null(inspection.Value(ApprovalRequestResources.RecountingSessionGuidKey));
            Assert.Contains("RecountingSessionGUID", inspection.MissingKeys);
        }

        // ---- Medicion del contrato de los 10 tipos ----

        [Fact]
        public void TipoConocido_ConTodasLasClaves_EsCompliant()
        {
            var json = "{\"BagGUID\":\"" + SesionGuid + "\",\"RecountingSessionGUID\":\"" + SesionGuid + "\"}";
            var inspection = ApprovalRequestResources.Inspect("Tula sin declaracion", json);

            Assert.True(inspection.IsCompliant);
            Assert.Empty(inspection.MissingKeys);
        }

        [Fact]
        public void TipoConocido_ConClavesFaltantes_ReportaCualesFaltan()
        {
            var inspection = ApprovalRequestResources.Inspect(
                "Primera tula en planilla", "{\"BagGUID\":\"" + SesionGuid + "\"}");

            Assert.False(inspection.IsCompliant);
            Assert.Contains("RecountingSessionGUID", inspection.MissingKeys);
            Assert.Contains("DeclarationBagGUID", inspection.MissingKeys);
            Assert.DoesNotContain("BagGUID", inspection.MissingKeys);
        }

        [Fact]
        public void ClaveGUID_NoSeSatisfaceConReceiptGUID()
        {
            // "Eliminar tula" pide la clave GUID (el GUID de la tula). Un IndexOf pelado
            // daria por presente "GUID" al ver "ReceiptGUID" — y el circuito de rechazo
            // terminaria revirtiendo el registro equivocado.
            var inspection = ApprovalRequestResources.Inspect(
                "Eliminar tula", "{\"ReceiptGUID\":\"" + SesionGuid + "\"}");

            Assert.Contains("GUID", inspection.MissingKeys);
        }

        [Fact]
        public void TipoDesconocido_NoSeMide()
        {
            // No medimos contra un contrato que no existe: seria ruido en la traza.
            var inspection = ApprovalRequestResources.Inspect("Tipo inventado", "{\"algo\":\"1\"}");

            Assert.False(inspection.IsKnownType);
            Assert.True(inspection.IsCompliant);
            Assert.Empty(inspection.MissingKeys);
        }

        [Fact]
        public void TipoConocido_EsOrdinalExacto()
        {
            // sanic_requesttype es texto libre: una variante de tipeo NO es el tipo.
            // Que aparezca como "fuera de contrato" en la traza es justamente el punto.
            Assert.False(ApprovalRequestResources.Inspect("eliminar tula", "{}").IsKnownType);
            Assert.True(ApprovalRequestResources.Inspect("Eliminar tula", "{}").IsKnownType);
        }

        [Fact]
        public void Contrato_CubreLosDiezTiposDocumentados()
        {
            // docs/supervisor-rebuild/03-pantalla-aprobaciones.md §0.
            Assert.Equal(10, ApprovalRequestResources.KnownTypes.Count());
            Assert.Contains("Crear sesion de recuento", ApprovalRequestResources.KnownTypes);
            Assert.Equal(
                new[] { "RecountingSessionGUID" },
                ApprovalRequestResources.ContractKeys("Crear sesion de recuento").ToArray());
        }

        // ---- La traza no es un volcado de datos ----

        [Fact]
        public void Summary_NoVuelcaElContenidoDelJson()
        {
            // Las trazas las lee cualquier admin: se reporta QUE falta, no los valores.
            var summary = ApprovalRequestResources.Inspect("Crear sesion de recuento", JsonSesionReal).Summary();

            Assert.DoesNotContain(SesionGuid, summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Crear sesion de recuento", summary);
        }

        [Fact]
        public void Summary_DistingueSinRecursosDeFormatoInvalido()
        {
            Assert.Contains("SIN RequestResources",
                ApprovalRequestResources.Inspect("Eliminar tula", null).Summary());
            Assert.Contains("no tiene forma de objeto",
                ApprovalRequestResources.Inspect("Eliminar tula", "no soy json").Summary());
        }
    }
}
