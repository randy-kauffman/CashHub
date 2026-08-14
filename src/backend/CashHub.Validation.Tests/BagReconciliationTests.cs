using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Conciliacion materializada de la tula (B5): copia de los totales declarados
    /// del DeclaredBag y bit HasDifference delegable para la grilla del tracker.
    /// </summary>
    public class BagReconciliationTests
    {
        private const int Recontada = BagReconciliation.BagStatusRecontada;
        private const int AsignadaACajero = 159460002;

        [Fact]
        public void Compute_RecontadaSinDiferencias_HasDifferenceFalse()
        {
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: true,
                declNio: 1000m, declUsd: 50m, declEur: 0m,
                recNio: 1000m, recUsd: 50m, recEur: 0m);

            Assert.False(r.HasDifference);
            Assert.Equal(1000m, r.DeclNio);
            Assert.Equal(50m, r.DeclUsd);
            Assert.Equal(0m, r.DeclEur);
        }

        [Fact]
        public void Compute_RecontadaConFaltanteEnNio_HasDifferenceTrue()
        {
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: true,
                declNio: 30500m, declUsd: 0m, declEur: 0m,
                recNio: 30000m, recUsd: 0m, recEur: 0m);

            Assert.True(r.HasDifference);
        }

        [Fact]
        public void Compute_RecontadaConSobranteEnUsd_HasDifferenceTrue()
        {
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: true,
                declNio: 0m, declUsd: 100m, declEur: 0m,
                recNio: 0m, recUsd: 130m, recEur: 0m);

            Assert.True(r.HasDifference);
        }

        [Fact]
        public void Compute_TodaviaNoRecontada_HasDifferenceFalseAunqueLosTotalesDifieran()
        {
            var r = BagReconciliation.Compute(
                bagStatus: AsignadaACajero, hasDeclaration: true,
                declNio: 500m, declUsd: 0m, declEur: 0m,
                recNio: 0m, recUsd: 0m, recEur: 0m);

            Assert.False(r.HasDifference);
        }

        [Fact]
        public void Compute_SinDeclaracion_NoHayConciliacion_DeclaradosEnCeroYSinDiferencia()
        {
            // Tula sin declaracion relacionada: la transaccion nace "Sin declaracion";
            // marcar diferencia contra cero seria ruido, no conciliacion.
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: false,
                declNio: null, declUsd: null, declEur: null,
                recNio: 9800m, recUsd: 0m, recEur: 0m);

            Assert.False(r.HasDifference);
            Assert.Equal(0m, r.DeclNio);
            Assert.Equal(0m, r.DeclUsd);
            Assert.Equal(0m, r.DeclEur);
        }

        [Fact]
        public void Compute_NulosSeTratanComoCero()
        {
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: true,
                declNio: null, declUsd: null, declEur: null,
                recNio: null, recUsd: null, recEur: null);

            Assert.False(r.HasDifference);
            Assert.Equal(0m, r.DeclNio);
        }

        [Fact]
        public void Compute_DeclaradoNuloContraRecontadoConMonto_EsDiferencia()
        {
            var r = BagReconciliation.Compute(
                bagStatus: Recontada, hasDeclaration: true,
                declNio: null, declUsd: 0m, declEur: 0m,
                recNio: 250m, recUsd: 0m, recEur: 0m);

            Assert.True(r.HasDifference);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void ShouldRecompute_ConCualquierDisparador_True(
            bool declarationTouched, bool totalsTouched, bool statusTouched)
        {
            Assert.True(BagReconciliation.ShouldRecompute(
                declarationTouched, totalsTouched, statusTouched));
        }

        [Fact]
        public void ShouldRecompute_SinDisparadores_False()
        {
            Assert.False(BagReconciliation.ShouldRecompute(false, false, false));
        }
    }
}
