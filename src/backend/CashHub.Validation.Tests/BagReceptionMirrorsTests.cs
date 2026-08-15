using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Espejos de la tula hacia su planilla (CH-App-Supervisor v2):
    /// B-2 (lote) y B-3 (recibo). Cubre las tres decisiones que toma el plugin:
    /// cuando estampar desde el lado de la tula, que propagar cuando la PLANILLA
    /// cambia, y cuando NO tocar nada (el caso que evita la cascada de ruido).
    /// </summary>
    public class BagReceptionMirrorsTests
    {
        private static readonly Guid LoteViejo = Guid.NewGuid();
        private static readonly Guid LoteNuevo = Guid.NewGuid();

        // ---------- estampa desde la tula ----------

        [Fact]
        public void ShouldStamp_EnCreate_SiempreTrue()
        {
            // Una tula nace con su planilla: los dos espejos se derivan siempre.
            Assert.True(BagReceptionMirrors.ShouldStamp("Create", receptionTouched: false));
            Assert.True(BagReceptionMirrors.ShouldStamp("Create", receptionTouched: true));
        }

        [Fact]
        public void ShouldStamp_EnUpdate_SoloSiSeReColgoDeOtraPlanilla()
        {
            Assert.True(BagReceptionMirrors.ShouldStamp("Update", receptionTouched: true));
        }

        [Fact]
        public void ShouldStamp_EnUpdateQueNoTocaLaPlanilla_NoReEstampa()
        {
            // Sin esto, cualquier update de la tula (estado, totales) dispararia una
            // lectura de la planilla y reescribiria espejos identicos: ruido puro.
            Assert.False(BagReceptionMirrors.ShouldStamp("Update", receptionTouched: false));
        }

        // ---------- cascada desde la planilla ----------

        [Fact]
        public void Cascade_LoteCambio_PropagaSoloElLote()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: true, newBatchId: LoteNuevo, previousBatchId: LoteViejo,
                receiptTouched: false, newReceiptId: null, previousReceiptId: "R-001");

            Assert.True(plan.Any);
            Assert.True(plan.PropagateBatch);
            Assert.Equal(LoteNuevo, plan.BatchId);
            Assert.False(plan.PropagateReceipt);
        }

        [Fact]
        public void Cascade_ReciboCambio_PropagaSoloElRecibo()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: false, newBatchId: null, previousBatchId: LoteViejo,
                receiptTouched: true, newReceiptId: "R-002", previousReceiptId: "R-001");

            Assert.True(plan.Any);
            Assert.False(plan.PropagateBatch);
            Assert.True(plan.PropagateReceipt);
            Assert.Equal("R-002", plan.ReceiptId);
        }

        [Fact]
        public void Cascade_LoteYReciboCambian_PropagaLosDos()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: true, newBatchId: LoteNuevo, previousBatchId: LoteViejo,
                receiptTouched: true, newReceiptId: "R-002", previousReceiptId: "R-001");

            Assert.True(plan.PropagateBatch);
            Assert.True(plan.PropagateReceipt);
        }

        [Fact]
        public void Cascade_PlanillaDesvinculadaDelLote_PropagaLimpieza()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: true, newBatchId: null, previousBatchId: LoteViejo,
                receiptTouched: false, newReceiptId: null, previousReceiptId: "R-001");

            Assert.True(plan.PropagateBatch);
            Assert.Null(plan.BatchId);
        }

        [Fact]
        public void Cascade_SinCambioReal_NoTocaNingunaTula()
        {
            // Dataverse dispara un paso filtrado aunque se reescriba el MISMO valor.
            // Sin esta guarda, guardar la planilla sin cambiarla reescribe N tulas.
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: true, newBatchId: LoteViejo, previousBatchId: LoteViejo,
                receiptTouched: true, newReceiptId: "R-001", previousReceiptId: "R-001");

            Assert.False(plan.Any);
            Assert.False(plan.PropagateBatch);
            Assert.False(plan.PropagateReceipt);
        }

        [Fact]
        public void Cascade_ColumnasNoTocadas_NoPropagaAunqueDifieran()
        {
            // Si la columna no vino en el Target, no cambio: el valor efectivo es el
            // de la PreImage. Propagar aca seria inventar un cambio que no ocurrio.
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: false, newBatchId: null, previousBatchId: LoteViejo,
                receiptTouched: false, newReceiptId: null, previousReceiptId: "R-001");

            Assert.False(plan.Any);
        }

        [Fact]
        public void Cascade_ReciboSoloCambiaEnEspaciosEnBlanco_NoPropaga()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: false, newBatchId: null, previousBatchId: null,
                receiptTouched: true, newReceiptId: "  R-001  ", previousReceiptId: "R-001");

            Assert.False(plan.PropagateReceipt);
        }

        [Fact]
        public void Cascade_ReciboVaciadoAVacio_SeNormalizaANull()
        {
            var plan = BagReceptionMirrors.ResolveCascade(
                batchTouched: false, newBatchId: null, previousBatchId: null,
                receiptTouched: true, newReceiptId: "   ", previousReceiptId: "R-001");

            Assert.True(plan.PropagateReceipt);
            Assert.Null(plan.ReceiptId);
        }

        // ---------- normalizacion del recibo ----------

        [Fact]
        public void CapReceipt_RecortaAlLargoDeLaColumna()
        {
            var largo = new string('X', BagReceptionMirrors.MaxReceiptLength + 25);

            var capped = BagReceptionMirrors.CapReceipt(largo);

            // Sin recorte, un recibo largo volteaba la escritura del espejo y con
            // ella la operacion del usuario que solo queria mover una tula.
            Assert.Equal(BagReceptionMirrors.MaxReceiptLength, capped!.Length);
        }

        [Fact]
        public void CapReceipt_NullYVacioSonNull()
        {
            Assert.Null(BagReceptionMirrors.CapReceipt(null));
            Assert.Null(BagReceptionMirrors.CapReceipt(""));
            Assert.Null(BagReceptionMirrors.CapReceipt("   "));
        }
    }
}
