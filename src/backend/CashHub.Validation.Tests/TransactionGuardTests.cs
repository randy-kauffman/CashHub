using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.TransactionGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del guard del dominio de digitalizacion (lotes de transacciones
    /// y transacciones). El agujero que cierra: la maquina de estados del lote
    /// (Generado -> Ingresado -> Aplicado) vive en Power Fx, asi que cualquier
    /// usuario con el rol puede, por Web API o Excel, retroceder un lote Aplicado,
    /// mandar un lote al dead-end Aprobado, meter transacciones en un lote cerrado,
    /// re-aplicar una transaccion Aplicada o vaciar un lote ya aplicado en el Core.
    /// </summary>
    public class TransactionGuardTests
    {
        /// <summary>Atajo con defaults para no repetir 9 primitivas en cada caso.</summary>
        private static TransactionGuardResult EvalTx(
            bool touchesOnlyMirror = false,
            bool statusInTarget = false,
            int? newStatus = null,
            int? previousStatus = null,
            bool batchInTarget = false,
            bool newBatchIsNull = false,
            int? currentBatchStatus = null,
            int? targetBatchStatus = null,
            bool hadBatch = false)
        {
            return EvaluateTransactionUpdate(
                touchesOnlyMirror,
                statusInTarget, newStatus, previousStatus,
                batchInTarget, newBatchIsNull,
                currentBatchStatus, targetBatchStatus, hadBatch);
        }

        // ---- Lote (TransactionBatch): nace Generado EXPLICITO ----

        [Fact]
        public void BatchCreate_Generado_IsAllowed()
        {
            Assert.True(EvaluateBatchCreate(BatchGenerado).IsAllowed);
        }

        [Fact]
        public void BatchCreate_SinEstado_IsBlocked()
        {
            // Los defaults de columna NO aplican a estos Create: un lote con estado
            // null queda fuera de la maquina de estados para siempre.
            var result = EvaluateBatchCreate(null);
            Assert.False(result.IsAllowed);
            Assert.Contains("Generado", result.Reason);
        }

        [Theory]
        [InlineData(BatchIngresado)]
        [InlineData(BatchAprobado)]
        [InlineData(BatchAplicado)]
        public void BatchCreate_NacidoAvanzado_IsBlocked(int bornStatus)
        {
            Assert.False(EvaluateBatchCreate(bornStatus).IsAllowed);
        }

        // ---- Lote: transiciones SOLO hacia adelante ----

        [Theory]
        [InlineData(BatchGenerado, BatchIngresado)]
        [InlineData(BatchIngresado, BatchAplicado)]
        public void BatchUpdate_TransicionHaciaAdelante_IsAllowed(int previous, int next)
        {
            Assert.True(EvaluateBatchUpdate(previous, true, next, touchesOnlyTotals: false).IsAllowed);
        }

        [Theory]
        [InlineData(BatchGenerado, BatchAplicado)]   // salto: se salta la carga en el Core
        [InlineData(BatchIngresado, BatchGenerado)]  // retroceso
        [InlineData(BatchGenerado, BatchAprobado)]   // Aprobado es un dead-end prohibido
        [InlineData(BatchIngresado, BatchAprobado)]
        [InlineData(BatchAprobado, BatchAplicado)]   // ni entrar ni salir de Aprobado
        [InlineData(BatchAprobado, BatchGenerado)]
        [InlineData(BatchGenerado, BatchGenerado)]   // ni siquiera el no-op: solo hacia adelante
        public void BatchUpdate_TransicionIlegal_IsBlocked(int previous, int next)
        {
            Assert.False(EvaluateBatchUpdate(previous, true, next, touchesOnlyTotals: false).IsAllowed);
        }

        [Theory]
        [InlineData(BatchIngresado)]
        [InlineData(BatchGenerado)]
        [InlineData(BatchAplicado)] // un Aplicado es inmutable, ni el no-op pasa
        public void BatchUpdate_EstadoSobreLoteAplicado_IsBlocked(int next)
        {
            var result = EvaluateBatchUpdate(BatchAplicado, true, next, touchesOnlyTotals: false);
            Assert.False(result.IsAllowed);
            Assert.Contains("Aplicado", result.Reason);
        }

        [Fact]
        public void BatchUpdate_SinEstadoPrevio_IsBlocked()
        {
            // Un lote sin estado esta fuera de la maquina; no hay transicion legal
            // desde la nada (el Create ya exige Generado explicito).
            Assert.False(EvaluateBatchUpdate(null, true, BatchIngresado, touchesOnlyTotals: false).IsAllowed);
        }

        [Theory]
        [InlineData(BatchGenerado)]
        [InlineData(BatchIngresado)]
        [InlineData(BatchAplicado)] // el recompute de totales pasa incluso sobre un Aplicado
        public void BatchUpdate_SoloTotales_IsAllowed(int previous)
        {
            // Escrituras del recompute de totales materializados (infraestructura).
            Assert.True(EvaluateBatchUpdate(previous, false, null, touchesOnlyTotals: true).IsAllowed);
        }

        [Fact]
        public void BatchUpdate_SinCambioDeEstado_IsAllowed()
        {
            // Campos comunes (comentarios) sin tocar el estado: no es asunto del guard.
            Assert.True(EvaluateBatchUpdate(BatchIngresado, false, null, touchesOnlyTotals: false).IsAllowed);
        }

        // ---- Lote: borrado solo del rollback del corte vacio ----

        [Fact]
        public void BatchDelete_GeneradoVacio_IsAllowed()
        {
            Assert.True(EvaluateBatchDelete(BatchGenerado, hasTransactions: false).IsAllowed);
        }

        [Fact]
        public void BatchDelete_GeneradoConTransacciones_IsBlocked()
        {
            var result = EvaluateBatchDelete(BatchGenerado, hasTransactions: true);
            Assert.False(result.IsAllowed);
            Assert.Contains("transacciones", result.Reason);
        }

        [Theory]
        [InlineData(BatchIngresado)]
        [InlineData(BatchAprobado)]
        [InlineData(BatchAplicado)]
        [InlineData(null)]
        public void BatchDelete_NoGenerado_IsBlocked(int? status)
        {
            Assert.False(EvaluateBatchDelete(status, hasTransactions: false).IsAllowed);
        }

        // ---- Transaccion: escrituras de infraestructura del espejo ----

        [Fact]
        public void TxUpdate_SoloEspejoDeEstado_IsAllowed()
        {
            // La cascada del mirror plugin escribe sanic_batchstatus fila a fila.
            Assert.True(EvalTx(touchesOnlyMirror: true).IsAllowed);
        }

        // ---- Transaccion: Aplicada es TERMINAL ----

        [Fact]
        public void TxUpdate_TocarEstadoDeUnaAplicada_IsBlocked()
        {
            var result = EvalTx(statusInTarget: true, newStatus: null, previousStatus: StatusAplicada);
            Assert.False(result.IsAllowed);
            Assert.Contains("Aplicada", result.Reason);
        }

        [Fact]
        public void TxUpdate_ReAplicarUnaAplicada_IsBlocked()
        {
            // Ni siquiera el no-op Aplicada -> Aplicada: terminal es terminal.
            Assert.False(EvalTx(statusInTarget: true, newStatus: StatusAplicada, previousStatus: StatusAplicada).IsAllowed);
        }

        [Fact]
        public void TxUpdate_MarcarAplicadaSinLote_IsAllowed()
        {
            // El circuito manual: la transaccion sin lote se aplica una a una.
            Assert.True(EvalTx(statusInTarget: true, newStatus: StatusAplicada, hadBatch: false).IsAllowed);
        }

        [Fact]
        public void TxUpdate_MarcarAplicadaConLote_IsBlocked()
        {
            // Las transacciones de lote no llevan status propio: se aplican con el lote.
            Assert.False(EvalTx(statusInTarget: true, newStatus: StatusAplicada, hadBatch: true).IsAllowed);
        }

        [Fact]
        public void TxUpdate_MarcarAplicadaYAsignarLoteEnElMismoRequest_IsBlocked()
        {
            Assert.False(EvalTx(
                statusInTarget: true, newStatus: StatusAplicada,
                batchInTarget: true, newBatchIsNull: false,
                targetBatchStatus: BatchGenerado).IsAllowed);
        }

        // ---- Transaccion: asignacion a lote ----

        [Fact]
        public void TxUpdate_AsignarPendienteALoteGenerado_IsAllowed()
        {
            // El corte: transaccion pendiente y sin lote entra a un lote Generado.
            Assert.True(EvalTx(
                batchInTarget: true, newBatchIsNull: false,
                targetBatchStatus: BatchGenerado, hadBatch: false).IsAllowed);
        }

        [Theory]
        [InlineData(BatchIngresado)]
        [InlineData(BatchAprobado)]
        [InlineData(BatchAplicado)]
        [InlineData(null)] // lote sin estado: fuera de la maquina, nada entra ahi
        public void TxUpdate_AsignarALoteNoGenerado_IsBlocked(int? targetBatchStatus)
        {
            var result = EvalTx(
                batchInTarget: true, newBatchIsNull: false,
                targetBatchStatus: targetBatchStatus, hadBatch: false);
            Assert.False(result.IsAllowed);
            Assert.Contains("Generado", result.Reason);
        }

        [Fact]
        public void TxUpdate_ReasignarDeUnLoteAOtro_IsBlocked()
        {
            // Sin reasignacion directa: primero se desasigna, despues se corta de nuevo.
            Assert.False(EvalTx(
                batchInTarget: true, newBatchIsNull: false,
                targetBatchStatus: BatchGenerado, hadBatch: true,
                currentBatchStatus: BatchGenerado).IsAllowed);
        }

        [Fact]
        public void TxUpdate_AsignarUnaAplicada_IsBlocked()
        {
            // El bug historico de la doble aplicacion: una Aplicada no re-entra a lote.
            Assert.False(EvalTx(
                previousStatus: StatusAplicada,
                batchInTarget: true, newBatchIsNull: false,
                targetBatchStatus: BatchGenerado, hadBatch: false).IsAllowed);
        }

        // ---- Transaccion: desasignacion (rechazo del Core durante la carga) ----

        [Theory]
        [InlineData(BatchGenerado)]
        [InlineData(BatchIngresado)]
        public void TxUpdate_DesasignarDeLoteAbierto_IsAllowed(int currentBatchStatus)
        {
            Assert.True(EvalTx(
                batchInTarget: true, newBatchIsNull: true,
                hadBatch: true, currentBatchStatus: currentBatchStatus).IsAllowed);
        }

        [Theory]
        [InlineData(BatchAprobado)]
        [InlineData(null)]
        public void TxUpdate_DesasignarDeLoteFueraDeCiclo_IsBlocked(int? currentBatchStatus)
        {
            Assert.False(EvalTx(
                batchInTarget: true, newBatchIsNull: true,
                hadBatch: true, currentBatchStatus: currentBatchStatus).IsAllowed);
        }

        [Fact]
        public void TxUpdate_DesasignarDeLoteAplicado_IsBlocked()
        {
            // Regla del negocio: los rechazos del Core ocurren durante la carga,
            // ANTES de Aplicado; un lote Aplicado es intocable.
            var result = EvalTx(
                batchInTarget: true, newBatchIsNull: true,
                hadBatch: true, currentBatchStatus: BatchAplicado);
            Assert.False(result.IsAllowed);
            Assert.Contains("Aplicado", result.Reason);
        }

        [Fact]
        public void TxUpdate_DesasignarSinLotePrevio_IsAllowed()
        {
            // Escribir lote=null sobre una transaccion que no tenia lote: no-op.
            Assert.True(EvalTx(batchInTarget: true, newBatchIsNull: true, hadBatch: false).IsAllowed);
        }

        // ---- Transaccion: lo demas no es asunto del guard ----

        [Fact]
        public void TxUpdate_CamposComunesSinEstadoNiLote_IsAllowed()
        {
            Assert.True(EvalTx().IsAllowed);
        }
    }
}
