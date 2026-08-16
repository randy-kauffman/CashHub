using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Espejo del nombre del cliente en la sucursal (CH-App-Reception v2).
    /// Cubre las dos decisiones que toma ClientBranchNameMirrorPlugin: cuando
    /// (re)estampar y como normalizar el nombre antes de escribirlo.
    /// </summary>
    public class ClientBranchNameMirrorsTests
    {
        // ---------- cuando estampar ----------

        [Fact]
        public void ShouldStamp_EnCreate_SiempreTrue()
        {
            // Una sucursal nace colgada de su cliente: el espejo se deriva siempre.
            Assert.True(ClientBranchNameMirrors.ShouldStamp("Create", clientTouched: false));
            Assert.True(ClientBranchNameMirrors.ShouldStamp("Create", clientTouched: true));
        }

        [Fact]
        public void ShouldStamp_EnUpdate_SoloSiSeReColgoDeOtroCliente()
        {
            Assert.True(ClientBranchNameMirrors.ShouldStamp("Update", clientTouched: true));
        }

        [Fact]
        public void ShouldStamp_EnUpdateQueNoTocaElCliente_NoReEstampa()
        {
            // Sin esto, cualquier update de la sucursal (nombre, estado) dispararia
            // una lectura del cliente para reescribir el MISMO valor: ruido puro,
            // y con 50.000 sucursales es un Retrieve por cada guardado.
            Assert.False(ClientBranchNameMirrors.ShouldStamp("Update", clientTouched: false));
        }

        // ---------- normalizacion del nombre ----------

        [Fact]
        public void CapName_RecortaAlLargoDeLaColumna()
        {
            var largo = new string('X', ClientBranchNameMirrors.MaxNameLength + 25);

            var capped = ClientBranchNameMirrors.CapName(largo);

            // Sin recorte, un nombre mas largo que la columna espejo voltea el alta
            // de la sucursal entera: el usuario ve un error que no es suyo.
            Assert.Equal(ClientBranchNameMirrors.MaxNameLength, capped!.Length);
        }

        [Fact]
        public void CapName_NoTocaLoQueEntra()
        {
            Assert.Equal("Distribuidora El Progreso, S.A.",
                ClientBranchNameMirrors.CapName("Distribuidora El Progreso, S.A."));
        }

        [Fact]
        public void CapName_HaceTrim()
        {
            // El espejo alimenta un StartsWith delegado: un espacio de mas adelante
            // hace que la busqueda por "Distribuidora" no encuentre la sucursal.
            Assert.Equal("Ferreteria Central del Norte",
                ClientBranchNameMirrors.CapName("  Ferreteria Central del Norte  "));
        }

        [Fact]
        public void CapName_NullYVacioSonNull()
        {
            Assert.Null(ClientBranchNameMirrors.CapName(null));
            Assert.Null(ClientBranchNameMirrors.CapName(""));
            Assert.Null(ClientBranchNameMirrors.CapName("   "));
        }
    }
}
