using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion de la politica FAIL-CLOSED de la autoridad de aprobacion (H-01).
    ///
    /// El agujero que cierra: hasta ahora "quien puede aprobar" lo decidia el modelo de
    /// acceso — y NINGUN team asigna el rol de supervisor, asi que cualquiera con Write
    /// sobre la tabla y distinto del creador podia firmar. Ahora la autoridad es la
    /// pertenencia a un grupo AAD cuyo id vive en CH-Tbl-Parameter.
    ///
    /// La regla no negociable: sin configuracion valida NO se aprueba. Un guard que se
    /// abre cuando falta un parametro no es un guard, es una decoracion.
    /// </summary>
    public class ApproverGroupConfigurationTests
    {
        private static readonly Guid Grupo = Guid.Parse("17b5e5c7-e7a0-4b61-98cd-ceff67211797");

        [Fact]
        public void Clave_EsLaConvencionDeLaTablaDeParametros()
        {
            // Dominio.Sub.Nombre, misma familia que Security.ApproverGroup.Banca/.Boveda.
            Assert.Equal("Security.ApproverGroup.Supervisor", ApproverGroupConfiguration.ParameterKey);
        }

        [Fact]
        public void ConGuidValido_QuedaConfigurado()
        {
            var resolution = ApproverGroupConfiguration.Resolve(Grupo.ToString());

            Assert.True(resolution.IsConfigured);
            Assert.Equal(Grupo, resolution.GroupId);
            Assert.Null(resolution.Reason);
        }

        [Theory]
        [InlineData("  17b5e5c7-e7a0-4b61-98cd-ceff67211797  ")]
        [InlineData("{17b5e5c7-e7a0-4b61-98cd-ceff67211797}")]
        [InlineData("17B5E5C7-E7A0-4B61-98CD-CEFF67211797")]
        public void ConFormatosQueUnHumanoPegaAMano_QuedaConfigurado(string rawValue)
        {
            // El valor se carga a mano en la tabla: tolerar llaves, espacios y mayusculas
            // no debilita nada (sigue siendo el mismo GUID) y evita un bloqueo tonto.
            var resolution = ApproverGroupConfiguration.Resolve(rawValue);

            Assert.True(resolution.IsConfigured);
            Assert.Equal(Grupo, resolution.GroupId);
        }

        [Theory]
        [InlineData(null)]   // la fila no existe o esta inactiva
        [InlineData("")]
        [InlineData("   ")]
        public void SinParametro_NoSeAprueba(string? rawValue)
        {
            var resolution = ApproverGroupConfiguration.Resolve(rawValue);

            Assert.False(resolution.IsConfigured);
            Assert.Equal(Guid.Empty, resolution.GroupId);
            Assert.Contains("No se pudo verificar la pertenencia al grupo aprobador", resolution.Reason);
            // El mensaje tiene que decirle al admin QUE sembrar, no "error inesperado".
            Assert.Contains(ApproverGroupConfiguration.ParameterKey, resolution.Reason);
            Assert.Contains("CH-Tbl-Parameter", resolution.Reason);
        }

        [Theory]
        [InlineData("no-soy-un-guid")]
        [InlineData("CashHub - Banca de Empresas -Approver")]  // el NOMBRE del grupo, no su id
        [InlineData("00000000-0000-0000-0000-000000000000")]   // el GUID cero no es un grupo
        public void ConValorQueNoEsUnGrupo_NoSeAprueba(string rawValue)
        {
            var resolution = ApproverGroupConfiguration.Resolve(rawValue);

            Assert.False(resolution.IsConfigured);
            Assert.Contains("No se pudo verificar la pertenencia al grupo aprobador", resolution.Reason);
        }

        [Fact]
        public void MensajeDeMembresiaCaida_EsDeNegocioYNoTecnico()
        {
            // Lo ve el supervisor en pantalla: tiene que decirle que hacer,
            // no filtrar el detalle de la consulta que fallo.
            var message = ApproverGroupConfiguration.MembershipUnavailableMessage;

            Assert.Contains("No se pudo verificar la pertenencia al grupo aprobador", message);
            Assert.Contains("no se resuelve por seguridad", message);
        }
    }
}
