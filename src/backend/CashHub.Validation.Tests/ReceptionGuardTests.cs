using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ReceptionGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del guard del dominio de recepcion (lotes, planillas, tulas y
    /// solicitudes de aprobacion). El agujero que cierra: toda la maquina de estados
    /// vive en Power Fx, asi que cualquier usuario con el rol puede, por Web API o
    /// Excel, mandar un lote directo a boveda, editar planillas archivadas, borrar
    /// tulas de lotes cerrados o auto-aprobarse solicitudes.
    /// </summary>
    public class ReceptionGuardTests
    {
        private static readonly Guid Creator = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Supervisor = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // ---- Lote (ReceptionBatch): ciclo En progreso -> Recibido -> En boveda ----

        [Fact]
        public void BatchCreate_EnProgreso_IsAllowed()
        {
            Assert.True(EvaluateBatchCreate(BatchEnProgreso).IsAllowed);
        }

        [Fact]
        public void BatchCreate_SinEstado_IsAllowed()
        {
            Assert.True(EvaluateBatchCreate(null).IsAllowed);
        }

        [Theory]
        [InlineData(BatchRecibido)]
        [InlineData(BatchEnBoveda)]
        [InlineData(BatchAnulado)]
        public void BatchCreate_NacidoResuelto_IsBlocked(int bornStatus)
        {
            // Un lote no puede nacer ya recibido, en boveda ni anulado.
            Assert.False(EvaluateBatchCreate(bornStatus).IsAllowed);
        }

        [Theory]
        [InlineData(BatchEnProgreso, BatchRecibido)]
        [InlineData(BatchRecibido, BatchEnProgreso)] // reabrir un cierre por error
        [InlineData(BatchRecibido, BatchEnBoveda)]
        [InlineData(BatchEnProgreso, BatchEnProgreso)] // no-op idempotente
        public void BatchUpdate_TransicionLegal_IsAllowed(int previous, int next)
        {
            Assert.True(EvaluateBatchUpdate(previous, true, next).IsAllowed);
        }

        [Theory]
        [InlineData(BatchEnProgreso, BatchEnBoveda)] // saltarse la recepcion
        [InlineData(BatchEnBoveda, BatchRecibido)]   // nada sale de boveda
        [InlineData(BatchEnBoveda, BatchEnProgreso)]
        [InlineData(BatchEnProgreso, BatchAnulado)]  // anulacion sin circuito disenado
        [InlineData(BatchRecibido, BatchAnulado)]
        public void BatchUpdate_TransicionIlegal_IsBlocked(int previous, int next)
        {
            Assert.False(EvaluateBatchUpdate(previous, true, next).IsAllowed);
        }

        [Fact]
        public void BatchUpdate_CamposDeUnLoteEnBoveda_IsBlocked()
        {
            // Lote en boveda queda congelado: ni siquiera updates sin cambio de estado.
            var result = EvaluateBatchUpdate(BatchEnBoveda, false, null);
            Assert.False(result.IsAllowed);
            Assert.Contains("boveda", result.Reason);
        }

        [Fact]
        public void BatchUpdate_SoloTotalesDeclarados_IsAllowedInclusoEnBoveda()
        {
            // Los DecNIO*/DecUSD* son metadata derivada (ReceptionBatchTotalsPlugin
            // y backfill): mismo racional que touchesOnlyTotals del lote TB. El
            // congelamiento de boveda es para manos humanas, no para el espejo.
            var result = EvaluateBatchUpdate(BatchEnBoveda, false, null, touchesOnlyTotals: true);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void BatchUpdate_CamposDeUnLoteAbierto_IsAllowed()
        {
            Assert.True(EvaluateBatchUpdate(BatchEnProgreso, false, null).IsAllowed);
        }

        [Fact]
        public void BatchUpdate_SinEstadoPrevioConocido_IsAllowed()
        {
            // Registro legado sin estado: no hay base para bloquear.
            Assert.True(EvaluateBatchUpdate(null, true, BatchRecibido).IsAllowed);
        }

        [Theory]
        [InlineData(BatchEnProgreso, true)]
        [InlineData(null, true)]
        [InlineData(BatchRecibido, false)]
        [InlineData(BatchEnBoveda, false)]
        public void BatchDelete_SoloEnProgreso(int? status, bool expected)
        {
            Assert.Equal(expected, EvaluateBatchDelete(status).IsAllowed);
        }

        // ---- Planilla (Receptions): congelada cuando el lote esta en boveda ----

        [Theory]
        [InlineData(BatchEnProgreso, true)]
        [InlineData(BatchRecibido, true)]
        [InlineData(BatchEnBoveda, false)]
        [InlineData(BatchAnulado, false)]
        public void ReceptionCreate_SegunEstadoDelLote(int batchStatus, bool expected)
        {
            Assert.Equal(expected, EvaluateReceptionCreate(true, batchStatus).IsAllowed);
        }

        [Fact]
        public void ReceptionCreate_SinLote_IsAllowed()
        {
            // El esquema permite planillas sin lote (lookup recommended); no es asunto del guard.
            Assert.True(EvaluateReceptionCreate(false, null).IsAllowed);
        }

        [Theory]
        [InlineData(BatchEnProgreso, true)]
        [InlineData(BatchRecibido, true)]
        [InlineData(BatchEnBoveda, false)]
        public void ReceptionUpdate_SegunEstadoDelLote(int batchStatus, bool expected)
        {
            Assert.Equal(expected, EvaluateReceptionUpdate(batchStatus, false, null).IsAllowed);
        }

        [Fact]
        public void ReceptionUpdate_ReparentAUnLoteEnBoveda_IsBlocked()
        {
            // Mover una planilla hacia un lote cerrado tambien es adulterarlo.
            var result = EvaluateReceptionUpdate(BatchEnProgreso, true, BatchEnBoveda);
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void ReceptionUpdate_ReparentAUnLoteAbierto_IsAllowed()
        {
            Assert.True(EvaluateReceptionUpdate(BatchEnProgreso, true, BatchRecibido).IsAllowed);
        }

        [Theory]
        [InlineData(BatchEnProgreso, true)]
        [InlineData(BatchRecibido, true)]
        [InlineData(BatchEnBoveda, false)]
        public void ReceptionDelete_SegunEstadoDelLote(int batchStatus, bool expected)
        {
            Assert.Equal(expected, EvaluateReceptionDelete(batchStatus).IsAllowed);
        }

        // ---- Tula (Bags): matriz de transiciones del ciclo de vida ----

        [Theory]
        [InlineData(null)]
        [InlineData(BagRecibidaEnEsclusa)]
        [InlineData(BagAlmacenadaEnBoveda)]          // Supervisor aprueba "Agregar tula"
        [InlineData(BagPendienteSupervisorEsclusa)]  // Reception crea tula pendiente (fix #2)
        public void BagCreate_EstadosDeNacimientoLegales_IsAllowed(int? bornStatus)
        {
            Assert.True(EvaluateBagCreate(bornStatus).IsAllowed);
        }

        [Theory]
        [InlineData(BagAsignadaACajero)]
        [InlineData(BagRecontada)]
        [InlineData(BagPendienteSupervisorRecuento)]
        public void BagCreate_NacidaEnEstadoDeRecuento_IsBlocked(int bornStatus)
        {
            Assert.False(EvaluateBagCreate(bornStatus).IsAllowed);
        }

        [Theory]
        // Reception
        [InlineData(BagRecibidaEnEsclusa, BagAlmacenadaEnBoveda)]
        [InlineData(BagRecibidaEnEsclusa, BagPendienteSupervisorEsclusa)]
        [InlineData(BagAlmacenadaEnBoveda, BagPendienteSupervisorEsclusa)]
        // Recounting
        [InlineData(BagRecibidaEnEsclusa, BagAsignadaACajero)]
        [InlineData(BagAlmacenadaEnBoveda, BagAsignadaACajero)]
        [InlineData(BagAsignadaACajero, BagRecontada)]
        [InlineData(BagAsignadaACajero, BagPendienteSupervisorRecuento)]
        // Supervisor
        [InlineData(BagPendienteSupervisorEsclusa, BagRecibidaEnEsclusa)]
        [InlineData(BagPendienteSupervisorEsclusa, BagAlmacenadaEnBoveda)]
        [InlineData(BagPendienteSupervisorRecuento, BagAsignadaACajero)]
        [InlineData(BagPendienteSupervisorRecuento, BagRecontada)]
        // No-op
        [InlineData(BagAsignadaACajero, BagAsignadaACajero)]
        public void BagTransition_Legal_IsAllowed(int previous, int next)
        {
            Assert.True(EvaluateBagStatusTransition(previous, next).IsAllowed);
        }

        [Theory]
        [InlineData(BagRecontada, BagRecibidaEnEsclusa)]      // Recontada es terminal
        [InlineData(BagRecontada, BagAsignadaACajero)]
        [InlineData(BagAlmacenadaEnBoveda, BagRecibidaEnEsclusa)] // no se vuelve a esclusa
        [InlineData(BagAlmacenadaEnBoveda, BagRecontada)]     // recuento sin pasar por cajero
        [InlineData(BagRecibidaEnEsclusa, BagRecontada)]
        [InlineData(BagAsignadaACajero, BagRecibidaEnEsclusa)]
        [InlineData(BagAsignadaACajero, BagAlmacenadaEnBoveda)]
        [InlineData(BagPendienteSupervisorEsclusa, BagAsignadaACajero)]
        public void BagTransition_Ilegal_IsBlocked(int previous, int next)
        {
            Assert.False(EvaluateBagStatusTransition(previous, next).IsAllowed);
        }

        [Fact]
        public void BagTransition_SinEstadoPrevio_IsAllowed()
        {
            // Tulas legadas nacidas sin estado: la primera escritura las incorpora al ciclo.
            Assert.True(EvaluateBagStatusTransition(null, BagRecibidaEnEsclusa).IsAllowed);
        }

        [Theory]
        [InlineData(BagRecibidaEnEsclusa, BatchEnProgreso, true)]
        [InlineData(BagRecibidaEnEsclusa, BatchRecibido, true)]
        [InlineData(BagRecibidaEnEsclusa, BatchEnBoveda, false)]  // identidad congelada en boveda
        [InlineData(BagAlmacenadaEnBoveda, BatchEnBoveda, false)]
        public void BagIdentityEdit_SegunEstadoDelLote(int bagStatus, int batchStatus, bool expected)
        {
            Assert.Equal(expected, EvaluateBagIdentityEdit(bagStatus, batchStatus).IsAllowed);
        }

        [Fact]
        public void BagIdentityEdit_PendienteDeSupervisor_IsAllowed()
        {
            // El circuito de aprobacion (remapear/editar) materializa cambios de identidad
            // sobre tulas pendientes aunque el lote este en boveda.
            Assert.True(EvaluateBagIdentityEdit(BagPendienteSupervisorEsclusa, BatchEnBoveda).IsAllowed);
        }

        [Theory]
        [InlineData(BagRecibidaEnEsclusa, BatchEnProgreso, true)]
        [InlineData(BagRecibidaEnEsclusa, BatchRecibido, true)]
        [InlineData(BagRecibidaEnEsclusa, BatchEnBoveda, false)]
        [InlineData(BagPendienteSupervisorEsclusa, BatchEnBoveda, true)] // "Eliminar tula" aprobada
        [InlineData(BagAlmacenadaEnBoveda, BatchEnBoveda, false)]
        public void BagDelete_SegunEstadoDelLoteYDeLaTula(int bagStatus, int batchStatus, bool expected)
        {
            Assert.Equal(expected, EvaluateBagDelete(bagStatus, batchStatus).IsAllowed);
        }

        // ---- Solicitudes (ApprovalRequests): separacion de funciones ----

        [Fact]
        public void ApprovalCreate_Pendiente_IsAllowed()
        {
            Assert.True(EvaluateApprovalCreate(null, "Eliminar tula").IsAllowed);
        }

        [Fact]
        public void ApprovalCreate_SesionDeRecuentoMesaPropia_NaceAprobada_IsAllowed()
        {
            // Atajo de negocio deliberado de la app de recuento ("FIX DEFINITIVO"):
            // la sesion de mesa propia nace aprobada.
            Assert.True(EvaluateApprovalCreate(ApprovalAprobado, "Crear sesion de recuento").IsAllowed);
        }

        [Theory]
        [InlineData(ApprovalAprobado, "Eliminar tula")]
        [InlineData(ApprovalRechazado, "Remapear tula")]
        [InlineData(ApprovalAprobado, null)]
        public void ApprovalCreate_NacidaResuelta_IsBlocked(int bornStatus, string? requestType)
        {
            Assert.False(EvaluateApprovalCreate(bornStatus, requestType).IsAllowed);
        }

        [Fact]
        public void ApprovalUpdate_DecisionPorOtroUsuario_IsAllowed()
        {
            // Quien puede escribir la tabla lo determina el rol (solo supervisores tienen Write).
            var result = EvaluateApprovalUpdate(null, true, Creator, Supervisor);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void ApprovalUpdate_AutoAprobacion_IsBlocked()
        {
            // El ataque central: el mismo que pidio el cambio se lo aprueba por Web API.
            var result = EvaluateApprovalUpdate(null, true, Creator, Creator);
            Assert.False(result.IsAllowed);
            Assert.Contains("separacion de funciones", result.Reason);
        }

        [Fact]
        public void ApprovalUpdate_SinCreadorIdentificable_IsBlocked()
        {
            var result = EvaluateApprovalUpdate(null, true, Guid.Empty, Supervisor);
            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData(ApprovalAprobado)]
        [InlineData(ApprovalRechazado)]
        public void ApprovalUpdate_SolicitudYaResuelta_IsBlocked(int previousStatus)
        {
            // Una solicitud resuelta queda congelada: no se re-decide ni se edita.
            var result = EvaluateApprovalUpdate(previousStatus, true, Creator, Supervisor);
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void ApprovalUpdate_EditarDetallesMientrasPendiente_IsAllowed()
        {
            Assert.True(EvaluateApprovalUpdate(null, false, Creator, Creator).IsAllowed);
        }
    }
}
