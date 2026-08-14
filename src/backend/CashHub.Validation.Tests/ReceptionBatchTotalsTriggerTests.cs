using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Decide QUE lotes de recepcion recomputar cuando una planilla cambia.
    /// El calculo en si es un aggregate server-side (ReceptionTotalsCalculator);
    /// aca solo se prueba la decision, que es donde viven los casos borde.
    /// </summary>
    public class ReceptionBatchTotalsTriggerTests
    {
        private static readonly Guid LoteA = Guid.NewGuid();
        private static readonly Guid LoteB = Guid.NewGuid();

        [Fact]
        public void Create_ConLote_RecomputaEseLote()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Create", amountsTouched: true, batchTouched: true,
                targetBatch: LoteA, preImageBatch: null);

            Assert.Equal(new[] { LoteA }, targets);
        }

        [Fact]
        public void Create_SinLote_NoRecomputaNada()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Create", amountsTouched: true, batchTouched: false,
                targetBatch: null, preImageBatch: null);

            Assert.Empty(targets);
        }

        [Fact]
        public void Delete_RecomputaElLoteDeLaPreImagen()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Delete", amountsTouched: false, batchTouched: false,
                targetBatch: null, preImageBatch: LoteA);

            Assert.Equal(new[] { LoteA }, targets);
        }

        [Fact]
        public void Delete_PlanillaHuerfana_NoRecomputaNada()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Delete", amountsTouched: false, batchTouched: false,
                targetBatch: null, preImageBatch: null);

            Assert.Empty(targets);
        }

        [Fact]
        public void Update_CambioDeLote_RecomputaViejoYNuevo()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: false, batchTouched: true,
                targetBatch: LoteB, preImageBatch: LoteA);

            Assert.Equal(new[] { LoteA, LoteB }, targets);
        }

        [Fact]
        public void Update_AsignacionAUnLote_RecomputaSoloElNuevo()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: false, batchTouched: true,
                targetBatch: LoteB, preImageBatch: null);

            Assert.Equal(new[] { LoteB }, targets);
        }

        [Fact]
        public void Update_Desasignacion_RecomputaSoloElViejo()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: false, batchTouched: true,
                targetBatch: null, preImageBatch: LoteA);

            Assert.Equal(new[] { LoteA }, targets);
        }

        [Fact]
        public void Update_MismoLoteEnTargetYPreImagen_RecomputaUnaSolaVez()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: true, batchTouched: true,
                targetBatch: LoteA, preImageBatch: LoteA);

            Assert.Equal(new[] { LoteA }, targets);
        }

        [Fact]
        public void Update_SoloMontos_RecomputaElLoteActualDeLaPreImagen()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: true, batchTouched: false,
                targetBatch: null, preImageBatch: LoteA);

            Assert.Equal(new[] { LoteA }, targets);
        }

        [Fact]
        public void Update_SinMontosNiLote_NoRecomputaNada()
        {
            var targets = ReceptionBatchTotalsTrigger.ResolveRecomputeTargets(
                "Update", amountsTouched: false, batchTouched: false,
                targetBatch: null, preImageBatch: LoteA);

            Assert.Empty(targets);
        }
    }
}
