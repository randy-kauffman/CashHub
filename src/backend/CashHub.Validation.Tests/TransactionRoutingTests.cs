using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.TransactionRouting;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del ruteo de transacciones (CH-Tbl-Transactions) a proceso
    /// Batch o Manual. El agujero que cierra: la regla vive duplicada en 8 sitios
    /// Power Fx (con derivas entre si) y NINGUNO aplica "sin cuenta -> Manual";
    /// ademas el corte a lotes es un UpdateIf client-side (cap 2000, no atomico)
    /// que no excluye transacciones ya Aplicadas: doble aplicacion.
    /// </summary>
    public class TransactionRoutingTests
    {
        // ---- HasChecks: cualquier monto de cheque > 0 (propio o del deposito) ----

        [Theory]
        [InlineData(100, null, null, null)] // cheque BAC propio
        [InlineData(null, 100, null, null)] // cheque otros bancos propio
        [InlineData(null, null, 100, null)] // cheque BAC del deposito relacionado
        [InlineData(null, null, null, 100)] // cheque otros bancos del deposito
        public void HasChecks_UnSoloMontoPositivo_EsTrue(int? txBac, int? txOther, int? depBac, int? depOther)
        {
            Assert.True(HasChecks(txBac, txOther, depBac, depOther));
        }

        [Theory]
        [InlineData(null, null, null, null)]
        [InlineData(0, 0, 0, 0)]
        public void HasChecks_SinMontos_EsFalse(int? txBac, int? txOther, int? depBac, int? depOther)
        {
            Assert.False(HasChecks(txBac, txOther, depBac, depOther));
        }

        // ---- ResolveProcessingType: Manual explicito se respeta; Batch solo si las reglas lo permiten ----

        [Theory]
        [InlineData(ProcessingBatch)] // pidieron Batch pero hay cheques: Manual igual
        [InlineData(ProcessingManual)]
        [InlineData(null)]
        public void Resolve_ConCheques_EsManual(int? requestedType)
        {
            Assert.Equal(ProcessingManual, ResolveProcessingType(requestedType, hasChecks: true, hasAccountNumber: true));
        }

        [Theory]
        [InlineData(ProcessingBatch)] // la regla que hoy NO existe en Power Fx: sin cuenta no hay Batch
        [InlineData(ProcessingManual)]
        [InlineData(null)]
        public void Resolve_SinCuenta_EsManual(int? requestedType)
        {
            Assert.Equal(ProcessingManual, ResolveProcessingType(requestedType, hasChecks: false, hasAccountNumber: false));
        }

        [Fact]
        public void Resolve_ManualExplicito_SeRespeta()
        {
            // El Supervisor fuerza Manual a proposito en aprobaciones: con cuenta y
            // sin cheques, Manual pedido sigue siendo Manual.
            Assert.Equal(ProcessingManual, ResolveProcessingType(ProcessingManual, hasChecks: false, hasAccountNumber: true));
        }

        [Theory]
        [InlineData(ProcessingBatch)]
        [InlineData(null)] // sin pedido explicito, efectivo con cuenta va a Batch
        public void Resolve_EfectivoConCuenta_EsBatch(int? requestedType)
        {
            Assert.Equal(ProcessingBatch, ResolveProcessingType(requestedType, hasChecks: false, hasAccountNumber: true));
        }

        // ---- IsEligibleForCut: solo Batch, sin lote, pendiente (Aplicada NUNCA entra) ----

        [Fact]
        public void Cut_BatchSinLotePendiente_EsElegible()
        {
            Assert.True(IsEligibleForCut(ProcessingBatch, hasBatch: false, transactionStatus: null));
        }

        [Fact]
        public void Cut_TransaccionAplicada_NoEsElegible()
        {
            // El fix del bug de doble aplicacion: una transaccion ya Aplicada no
            // vuelve a entrar en un corte aunque no tenga lote asignado.
            Assert.False(IsEligibleForCut(ProcessingBatch, hasBatch: false, transactionStatus: StatusAplicada));
        }

        [Fact]
        public void Cut_ConLoteAsignado_NoEsElegible()
        {
            Assert.False(IsEligibleForCut(ProcessingBatch, hasBatch: true, transactionStatus: null));
        }

        [Fact]
        public void Cut_ProcesoManual_NoEsElegible()
        {
            Assert.False(IsEligibleForCut(ProcessingManual, hasBatch: false, transactionStatus: null));
        }

        [Fact]
        public void Cut_SinTipoDeProceso_NoEsElegible()
        {
            // Transaccion legada sin tipo resuelto: no se asume Batch.
            Assert.False(IsEligibleForCut(null, hasBatch: false, transactionStatus: null));
        }
    }
}
