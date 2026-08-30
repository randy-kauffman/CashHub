using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.RecountingGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del guard server-side de CH-App-Recounting v2
    /// (docs/recounting-rebuild/00-analisis-y-plan.md §2.2, puntos 1-6).
    ///
    /// El agujero que cierra: la v1 decidia TODO en Power Fx desde el dispositivo del
    /// cajero — se auto-aprobaba la sesion (S-1), sumaba los totales de la tula en
    /// memoria (S-2a), armaba los asientos contables client-side (S-2b) y escribia
    /// depositos/cheques/denominaciones sobre tulas que no estaban asignadas a nadie
    /// (S-5). Cualquiera con el rol podia reproducir todo eso por Web API o Excel.
    /// Aca vive la DECISION pura; los plugins solo arman el contexto.
    /// </summary>
    public class RecountingGuardTests
    {
        private static readonly Guid Cajero = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        private static readonly Guid OtroCajero = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        private static readonly Guid Dep1 = Guid.Parse("dddddddd-0001-0000-0000-000000000001");
        private static readonly Guid Dep2 = Guid.Parse("dddddddd-0002-0000-0000-000000000002");
        private static readonly Guid Dep3 = Guid.Parse("dddddddd-0003-0000-0000-000000000003");

        // =====================================================================
        // 1. Auto-aprobacion de sesion por mesa propia (mata S-1)
        // =====================================================================

        [Fact]
        public void AutoApprove_MesaPropia_AutoAprueba()
        {
            Assert.Equal(
                RecountingAutoApproval.AutoApprove,
                ShouldAutoApprove(RequestTypeSesionRecuento, Cajero, Cajero));
        }

        [Fact]
        public void AutoApprove_MesaAjena_QuedaPendiente()
        {
            // La sesion existe pero la mesa es de OTRO cajero: la firma un supervisor.
            Assert.Equal(
                RecountingAutoApproval.LeavePending,
                ShouldAutoApprove(RequestTypeSesionRecuento, OtroCajero, Cajero));
        }

        [Fact]
        public void AutoApprove_MesaSinCajero_QuedaPendiente()
        {
            // Mesa huerfana: sin cajero no hay "mesa propia" que verificar. Fail-closed.
            Assert.Equal(
                RecountingAutoApproval.LeavePending,
                ShouldAutoApprove(RequestTypeSesionRecuento, null, Cajero));
        }

        [Fact]
        public void AutoApprove_MesaConCajeroVacio_QuedaPendiente()
        {
            Assert.Equal(
                RecountingAutoApproval.LeavePending,
                ShouldAutoApprove(RequestTypeSesionRecuento, Guid.Empty, Cajero));
        }

        [Fact]
        public void AutoApprove_SinActor_QuedaPendiente()
        {
            // Sin InitiatingUserId confiable no se auto-aprueba NADA (era el bug S-1:
            // con gvarUser en blanco, la v1 tomaba la primera mesa sin cajero).
            Assert.Equal(
                RecountingAutoApproval.LeavePending,
                ShouldAutoApprove(RequestTypeSesionRecuento, Guid.Empty, Guid.Empty));
        }

        [Theory]
        [InlineData("Tula sin declaracion")]
        [InlineData("Tula con diferencia")]
        [InlineData("Tula recontada sin declaracion")]
        [InlineData("Primera tula en planilla")]
        [InlineData("crear sesion de recuento")] // ORDINAL: minuscula NO invoca la excepcion
        [InlineData("Crear sesion de recuento ")] // espacio de mas tampoco
        [InlineData("")]
        [InlineData(null)]
        public void AutoApprove_OtroTipo_NoAplica(string? requestType)
        {
            Assert.Equal(
                RecountingAutoApproval.NotApplicable,
                ShouldAutoApprove(requestType, Cajero, Cajero));
        }

        [Fact]
        public void RequestTypeSesionRecuento_EsElStringExactoDelContrato()
        {
            // 01 §5.6: texto libre, string EXACTO. Si cambia, el Supervisor deja de verlo.
            Assert.Equal("Crear sesion de recuento", RequestTypeSesionRecuento);
            Assert.Equal(ReceptionGuard.ApprovalTypeSesionRecuento, RequestTypeSesionRecuento);
        }

        [Fact]
        public void MensajesDeSesionIrresoluble_SonEspecificos()
        {
            // Nunca genericos: el cajero tiene que poder decirle a soporte QUE fallo.
            Assert.Contains("RecountingSessionGUID", UnreadableResourcesMessage, StringComparison.Ordinal);
            Assert.Contains("Crear sesion de recuento", UnreadableResourcesMessage, StringComparison.Ordinal);

            var id = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
            Assert.Contains(id.ToString(), SessionNotFoundMessage(id), StringComparison.Ordinal);
        }

        // =====================================================================
        // 2. Totales de la tula recalculados server-side (mata S-2a)
        // =====================================================================

        [Theory]
        [InlineData(ReceptionGuard.BagAsignadaACajero, ReceptionGuard.BagRecontada)]
        [InlineData(ReceptionGuard.BagAsignadaACajero, ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento, ReceptionGuard.BagRecontada)]
        public void ShouldRecomputeTotals_TransicionDeCierre_Recalcula(int previous, int next)
        {
            Assert.True(ShouldRecomputeTotals(previous, true, next));
        }

        [Fact]
        public void ShouldRecomputeTotals_SinEstadoPrevio_Recalcula()
        {
            // Registro legado sin estado que entra a Recontada: se recalcula igual.
            Assert.True(ShouldRecomputeTotals(null, true, ReceptionGuard.BagRecontada));
        }

        [Theory]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda, ReceptionGuard.BagAsignadaACajero)]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa, ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento, ReceptionGuard.BagAsignadaACajero)]
        public void ShouldRecomputeTotals_TransicionQueNoCierra_NoRecalcula(int previous, int next)
        {
            Assert.False(ShouldRecomputeTotals(previous, true, next));
        }

        [Fact]
        public void ShouldRecomputeTotals_SinEstadoEnElTarget_NoRecalcula()
        {
            Assert.False(ShouldRecomputeTotals(ReceptionGuard.BagAsignadaACajero, false, null));
        }

        [Fact]
        public void ShouldRecomputeTotals_NoOpDelMismoEstado_NoRecalcula()
        {
            // Dataverse dispara el paso filtrado aunque se reescriba el mismo valor:
            // sin esta guarda, guardar la tula sin cambiarla paga N lecturas al pedo.
            Assert.False(ShouldRecomputeTotals(
                ReceptionGuard.BagRecontada, true, ReceptionGuard.BagRecontada));
        }

        [Fact]
        public void ComputeTotals_DepositosMixtos_SumaPorMoneda()
        {
            // Misma semantica de la v1: CashAmount + BACLocalCheckAmount + OtherLocaCheckAmount,
            // agrupado por moneda (dialog_ConfirmarTerminarTula.OnButtonSelect).
            var totals = ComputeTotals(new[]
            {
                Deposito(CurrencyNio, cash: 1000m, bac: 250.50m, other: 49.50m),
                Deposito(CurrencyNio, cash: 500m,  bac: 0m,      other: 0m),
                Deposito(CurrencyUsd, cash: 120m,  bac: 80m,     other: 0m),
                Deposito(CurrencyEur, cash: 0m,    bac: 0m,      other: 33.33m),
            });

            Assert.Equal(1800m, totals.Nio);
            Assert.Equal(200m, totals.Usd);
            Assert.Equal(33.33m, totals.Eur);
        }

        [Fact]
        public void ComputeTotals_SinDepositos_TodoEnCero()
        {
            // Tula cerrada sin ningun deposito: los totales van a CERO EXPLICITO, no se
            // dejan como estaban. Un total viejo sobre una tula vaciada es plata fantasma.
            var totals = ComputeTotals(Array.Empty<DepositAmounts>());

            Assert.Equal(0m, totals.Nio);
            Assert.Equal(0m, totals.Usd);
            Assert.Equal(0m, totals.Eur);
        }

        [Fact]
        public void ComputeTotals_MonedaEnBlancoODesconocida_NoSumaEnNinguna()
        {
            var totals = ComputeTotals(new[]
            {
                Deposito(null, cash: 999m, bac: 999m, other: 999m),
                Deposito(159469999, cash: 777m, bac: 0m, other: 0m),
                Deposito(CurrencyNio, cash: 10m, bac: 0m, other: 0m),
            });

            Assert.Equal(10m, totals.Nio);
            Assert.Equal(0m, totals.Usd);
            Assert.Equal(0m, totals.Eur);
        }

        [Fact]
        public void ComputeTotals_ColeccionNula_TodoEnCero()
        {
            var totals = ComputeTotals(null);

            Assert.Equal(0m, totals.Nio);
            Assert.Equal(0m, totals.Usd);
            Assert.Equal(0m, totals.Eur);
        }

        [Fact]
        public void ComputeTotals_NoAdmiteAporteDelCliente()
        {
            // 🧨 El guard PISA: la unica fuente de los totales son los depositos
            // PERSISTIDOS. No hay parametro por donde entre un valor del cliente —
            // si algun dia aparece uno, este test deja de compilar y hay que discutirlo.
            var metodo = typeof(RecountingGuard).GetMethod(nameof(ComputeTotals));
            Assert.NotNull(metodo);
            Assert.Single(metodo!.GetParameters());
        }

        // =====================================================================
        // 3. Asientos contables server-side + idempotencia (mata S-2b)
        // =====================================================================

        [Fact]
        public void ShouldBuildTransactions_SoloAlEntrarARecontada()
        {
            Assert.True(ShouldBuildTransactions(
                ReceptionGuard.BagAsignadaACajero, true, ReceptionGuard.BagRecontada));
            Assert.True(ShouldBuildTransactions(
                ReceptionGuard.BagPendienteSupervisorRecuento, true, ReceptionGuard.BagRecontada));
        }

        [Fact]
        public void ShouldBuildTransactions_PendienteDeSupervisor_NoCreaAsientos()
        {
            // 🧨 La tula con discrepancias NO genera asientos: el supervisor todavia no
            // decidio. La v1 tampoco lo hacia (solo la rama del cierre limpio).
            Assert.False(ShouldBuildTransactions(
                ReceptionGuard.BagAsignadaACajero, true, ReceptionGuard.BagPendienteSupervisorRecuento));
        }

        [Fact]
        public void ShouldBuildTransactions_SinCambioDeEstado_NoCreaAsientos()
        {
            Assert.False(ShouldBuildTransactions(
                ReceptionGuard.BagRecontada, true, ReceptionGuard.BagRecontada));
            Assert.False(ShouldBuildTransactions(
                ReceptionGuard.BagAsignadaACajero, false, null));
        }

        [Fact]
        public void BuildTransactions_CopiaLaSemanticaDeLaV1()
        {
            var planes = BuildTransactions(
                new[]
                {
                    new DepositForTransaction
                    {
                        DepositId = Dep1,
                        Currency = CurrencyUsd,
                        CashAmount = 100m,
                        BacCheckAmount = 0m,
                        OtherCheckAmount = 0m,
                        AccountNumber = 123456789m,
                        DeclaredReference = "REF-001",
                        DeclaredTransactionNumber = 42,
                    },
                },
                alreadyPosted: null);

            var plan = Assert.Single(planes);
            Assert.Equal(Dep1, plan.DepositId);
            Assert.Equal(CurrencyUsd, plan.Currency);
            Assert.Equal(100m, plan.CashAmount);
            Assert.Equal(123456789m, plan.AccountNumber);
            Assert.Equal("REF-001", plan.TransactionReference);
            Assert.Equal(42, plan.TransactionNumber);
            Assert.Equal(TransactionTypeDeposito, plan.TransactionType);
        }

        [Fact]
        public void BuildTransactions_ConCheques_EsManual()
        {
            var conBac = BuildTransactions(
                new[] { Trx(Dep1, bac: 0.01m, other: 0m) }, null);
            var conOtros = BuildTransactions(
                new[] { Trx(Dep2, bac: 0m, other: 0.01m) }, null);

            Assert.Equal(ProcessingTypeManual, conBac[0].ProcessingType);
            Assert.Equal(ProcessingTypeManual, conOtros[0].ProcessingType);
        }

        [Fact]
        public void BuildTransactions_SinCheques_EsBatch()
        {
            var planes = BuildTransactions(new[] { Trx(Dep1, bac: 0m, other: 0m) }, null);

            Assert.Equal(ProcessingTypeBatch, planes[0].ProcessingType);
        }

        [Fact]
        public void BuildTransactions_DepositoSinDeclaracion_ViajaSinReferencia()
        {
            // La ruta "tula sin declaracion": no hay declarado del que copiar
            // TransactionReference/TransactionNumber, y eso NO es un error.
            var planes = BuildTransactions(
                new[]
                {
                    new DepositForTransaction
                    {
                        DepositId = Dep1,
                        Currency = CurrencyNio,
                        CashAmount = 10m,
                    },
                },
                null);

            Assert.Null(planes[0].TransactionReference);
            Assert.Null(planes[0].TransactionNumber);
        }

        [Fact]
        public void BuildTransactions_Idempotente_NoDuplicaAsientos()
        {
            // 🧨 La tula puede RE-transicionar a Recontada (rechazo del supervisor y
            // reconteo). Sin esta guarda, cada vuelta duplica la plata en el Core.
            var deposits = new[] { Trx(Dep1), Trx(Dep2), Trx(Dep3) };

            var planes = BuildTransactions(deposits, new HashSet<Guid> { Dep1, Dep3 });

            Assert.Equal(new[] { Dep2 }, planes.Select(p => p.DepositId).ToArray());
        }

        [Fact]
        public void BuildTransactions_TodoYaAsentado_NoCreaNada()
        {
            var deposits = new[] { Trx(Dep1), Trx(Dep2) };

            Assert.Empty(BuildTransactions(deposits, new HashSet<Guid> { Dep1, Dep2 }));
        }

        [Fact]
        public void BuildTransactions_DepositoSinPk_SeIgnora()
        {
            // Sin PK no hay RelatedDepositRecounting con que verificar idempotencia:
            // crear el asiento seria sembrar un duplicado que nadie puede detectar.
            var planes = BuildTransactions(new[] { Trx(Guid.Empty) }, null);

            Assert.Empty(planes);
        }

        [Fact]
        public void BuildTransactions_SinDepositos_NoCreaNada()
        {
            Assert.Empty(BuildTransactions(null, null));
            Assert.Empty(BuildTransactions(Array.Empty<DepositForTransaction>(), null));
        }

        [Fact]
        public void BuildTransactions_ProduceExactamenteLoQueAccountingGuardEXIGE()
        {
            // 🧨 Los asientos que crea este guard pasan por GuardSupervisorAccounting
            // (Pre-validation del Create de sanic_chtbltransactions). Para el tipo
            // Deposito, AccountingGuard exige moneda IGUAL a la del recuento y montos
            // IGUALES por componente. Si este plan dejara de copiarlos tal cual, cada
            // cierre limpio de tula reventaria en produccion — y no en un test.
            var deposit = new DepositForTransaction
            {
                DepositId = Dep1,
                Currency = CurrencyUsd,
                CashAmount = 500.25m,
                BacCheckAmount = 100m,
                OtherCheckAmount = 25.75m,
            };

            var plan = BuildTransactions(new[] { deposit }, null)[0];

            Assert.Equal(deposit.Currency, plan.Currency);
            Assert.Equal(deposit.CashAmount, plan.CashAmount);
            Assert.Equal(deposit.BacCheckAmount, plan.BacCheckAmount);
            Assert.Equal(deposit.OtherCheckAmount, plan.OtherCheckAmount);

            // Y el tipo tiene que ser uno de los cuatro que AccountingGuard reconoce.
            Assert.True(AccountingGuard.IsAccountingType(plan.TransactionType));
            Assert.Equal(AccountingGuard.TipoDeposito, plan.TransactionType);
        }

        [Fact]
        public void ValoresDeOptionSet_SonLosDelEntityXml()
        {
            // Verificados contra solutions/unpacked/core/Entities/sanic_CHTblTransactions/Entity.xml
            Assert.Equal(159460000, TransactionTypeDeposito);
            Assert.Equal(159460000, ProcessingTypeManual);
            Assert.Equal(159460001, ProcessingTypeBatch);
            Assert.Equal(159460000, CurrencyNio);
            Assert.Equal(159460001, CurrencyUsd);
            Assert.Equal(159460002, CurrencyEur);
            Assert.Equal(CheckGuard.Recontado, CheckStatusRecontado);
        }

        // =====================================================================
        // 4. Gate de escritura de hijos del recuento (mata S-5)
        // =====================================================================

        [Fact]
        public void ChildWrite_TulaAsignadaACajero_SePermite()
        {
            Assert.True(IsWriteAllowed(false, true, ReceptionGuard.BagAsignadaACajero).IsAllowed);
        }

        [Theory]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData(ReceptionGuard.BagRecontada)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorEsclusa)]
        public void ChildWrite_TulaFueraDeMesa_SeBloquea(int bagStatus)
        {
            var result = IsWriteAllowed(false, true, bagStatus);

            Assert.False(result.IsAllowed);
            Assert.Contains("Asignada a cajero", result.Reason!, StringComparison.Ordinal);
        }

        [Fact]
        public void ChildWrite_TulaSinEstado_SeBloquea()
        {
            // Fail-closed: sin estado no se puede afirmar que la tula este en la mesa.
            Assert.False(IsWriteAllowed(false, true, null).IsAllowed);
        }

        [Fact]
        public void ChildWrite_TulaIrresoluble_SeBloquea()
        {
            var result = IsWriteAllowed(false, false, null);

            Assert.False(result.IsAllowed);
            Assert.Contains("tula", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ChildWrite_Eximido_PasaSinMirarLaTula()
        {
            // La excepcion se resuelve ANTES de leer nada: es lo que hace que el
            // pipeline de plugins y el cierre de cheques no paguen una lectura de tula.
            Assert.True(IsWriteAllowed(true, false, null).IsAllowed);
            Assert.True(IsWriteAllowed(true, true, ReceptionGuard.BagRecontada).IsAllowed);
        }

        [Fact]
        public void IsWriteExempt_PipelineDePlugins_EsExento()
        {
            // Depth > 1 = lo escribe otro plugin (espejos E-1/E-2/E-4, cascadas,
            // el propio guard). El actor humano SIEMPRE entra con depth 1.
            Assert.True(IsWriteExempt("Update", 2, false, false));
            Assert.True(IsWriteExempt("Create", 5, false, false));
            Assert.True(IsWriteExempt("Delete", 3, false, false));
        }

        [Fact]
        public void IsWriteExempt_ActorHumano_NoEsExento()
        {
            Assert.False(IsWriteExempt("Create", 1, false, false));
            Assert.False(IsWriteExempt("Update", 1, false, false));
            Assert.False(IsWriteExempt("Delete", 1, false, false));
            Assert.False(IsWriteExempt("Update", 0, false, false));
        }

        [Fact]
        public void IsWriteExempt_MarcadoDeChequeARecontado_EsExento()
        {
            // 🧨 04 §11.3 marca los cheques a Recontado DESPUES de mover la tula a
            // Recontada (a proposito: si el Patch de la bolsa falla, los cheques NO se
            // tocan). Sin esta excepcion el gate voltearia el cierre limpio de la app.
            // Quien decide si la transicion de estado es legal sigue siendo CheckGuard.
            Assert.True(IsWriteExempt("Update", 1, touchesOnlyCheckStatus: true, touchesOnlyMirror: false));
        }

        [Fact]
        public void IsWriteExempt_EscrituraPuraDeEspejo_EsExento()
        {
            // 🧨 Los BACKFILLS estampan los espejos por Web API: depth 1 y sobre el
            // HISTORICO ENTERO (tulas en boveda, recontadas, de todo). Sin esta
            // excepcion el gate voltea backfill-recounting-mirrors.py completo.
            Assert.True(IsWriteExempt("Update", 1, touchesOnlyCheckStatus: false, touchesOnlyMirror: true));
        }

        [Fact]
        public void IsWriteExempt_LasExcepcionesDeUpdate_NoAplicanAlCreateNiAlDelete()
        {
            // "Solo el estado" / "solo el espejo" son formas de UPDATE. Un Create o un
            // Delete con esas banderas seria el llamador armando mal el contexto.
            Assert.False(IsWriteExempt("Create", 1, true, true));
            Assert.False(IsWriteExempt("Delete", 1, true, true));
        }

        // =====================================================================
        // 5. Bajas acotadas (D02-4)
        // =====================================================================

        [Fact]
        public void ApprovalDelete_PropiaYSinResolver_SePermite()
        {
            Assert.True(EvaluateApprovalDelete(Cajero, Cajero, null).IsAllowed);
        }

        [Fact]
        public void ApprovalDelete_Ajena_SeBloquea()
        {
            var result = EvaluateApprovalDelete(OtroCajero, Cajero, null);

            Assert.False(result.IsAllowed);
            Assert.Contains("cre", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(ReceptionGuard.ApprovalAprobado)]
        [InlineData(ReceptionGuard.ApprovalRechazado)]
        public void ApprovalDelete_YaResuelta_SeBloquea(int status)
        {
            // D02-5: la solicitud RECHAZADA es la evidencia de una decision del
            // supervisor y queda como registro. La aprobada, mas todavia.
            var result = EvaluateApprovalDelete(Cajero, Cajero, status);

            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void ApprovalDelete_SinCreadorConocido_SeBloquea()
        {
            Assert.False(EvaluateApprovalDelete(Guid.Empty, Cajero, null).IsAllowed);
        }

        [Fact]
        public void SessionDelete_PropiaSinConfirmarYSinResolucion_SePermite()
        {
            Assert.True(EvaluateSessionDelete(Cajero, Cajero, false, false).IsAllowed);
        }

        [Fact]
        public void SessionDelete_Ajena_SeBloquea()
        {
            var result = EvaluateSessionDelete(OtroCajero, Cajero, false, false);

            Assert.False(result.IsAllowed);
            Assert.Contains("cajero", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionDelete_Confirmada_SeBloquea()
        {
            // Sesion confirmada = mesa en uso con recuento real colgando.
            Assert.False(EvaluateSessionDelete(Cajero, Cajero, true, false).IsAllowed);
        }

        [Fact]
        public void SessionDelete_ConSolicitudResuelta_SeBloquea()
        {
            // Borrar la sesion referenciada por una solicitud YA resuelta deja al
            // Supervisor con una decision que apunta a la nada.
            Assert.False(EvaluateSessionDelete(Cajero, Cajero, false, true).IsAllowed);
        }

        [Fact]
        public void SessionDelete_SinCajeroConocido_SeBloquea()
        {
            Assert.False(EvaluateSessionDelete(Guid.Empty, Cajero, false, false).IsAllowed);
        }

        // ---------------------------------------------------------------------
        // Baja de sesion por un APROBADOR (supervisor)
        //
        // Regla de negocio: el supervisor puede dar de baja una sesion mientras NO
        // haya sido aprobada/activada (sanic_Confirmed). Una vez confirmada hay
        // recuento real colgando y la salida es el cierre forzado, no el borrado.
        //
        // Sin esto, rechazar una solicitud "Crear sesion de recuento" dejaba la
        // sesion huerfana: el supervisor no podia borrarla (no es el cajero) y el
        // cajero tampoco (la solicitud ya figuraba resuelta).
        // ---------------------------------------------------------------------

        private static readonly Guid Supervisor = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

        private const bool EsAprobador = true;
        private const bool NoEsAprobador = false;

        [Fact]
        public void SessionDelete_AjenaPeroActorEsAprobador_SinConfirmar_SePermite()
        {
            Assert.True(
                EvaluateSessionDelete(Cajero, Supervisor, false, false, EsAprobador).IsAllowed);
        }

        [Fact]
        public void SessionDelete_AprobadorConSolicitudResuelta_SePermite()
        {
            // Es EL caso del rechazo: la solicitud se marca resuelta y el borrado de
            // la sesion es su efecto. Bloquearlo aca es lo que producia el huerfano.
            Assert.True(
                EvaluateSessionDelete(Cajero, Supervisor, false, true, EsAprobador).IsAllowed);
        }

        [Fact]
        public void SessionDelete_Confirmada_NiSiquieraElAprobador()
        {
            // La confirmacion es la frontera dura: aplica a TODOS.
            var result = EvaluateSessionDelete(Cajero, Supervisor, true, false, EsAprobador);

            Assert.False(result.IsAllowed);
            Assert.Contains("confirmada", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionDelete_ConfirmadaYAjena_ElMotivoEsLaConfirmacion()
        {
            // La confirmacion se evalua ANTES que la propiedad: un cajero ajeno que
            // intenta borrar una sesion confirmada tiene que leer el motivo real,
            // no "no es tuya" (que ademas lo mandaria a pedirsela al dueno, y el
            // dueno tampoco puede).
            var result = EvaluateSessionDelete(Cajero, OtroCajero, true, false, NoEsAprobador);

            Assert.False(result.IsAllowed);
            Assert.Contains("confirmada", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionDelete_SinCajeroConocido_NiSiquieraElAprobador()
        {
            // No se sabe de quien es la mesa: se bloquea por seguridad, sin excepcion.
            Assert.False(
                EvaluateSessionDelete(Guid.Empty, Supervisor, false, false, EsAprobador).IsAllowed);
        }

        [Fact]
        public void SessionDelete_AjenaYNoAprobador_SigueBloqueada()
        {
            // Candado de regresion: la excepcion es SOLO para aprobadores.
            var result = EvaluateSessionDelete(Cajero, OtroCajero, false, false, NoEsAprobador);

            Assert.False(result.IsAllowed);
            Assert.Contains("cajero", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionDelete_PropiaConSolicitudResuelta_SinSerAprobador_SigueBloqueada()
        {
            // Candado de regresion del caso original.
            Assert.False(
                EvaluateSessionDelete(Cajero, Cajero, false, true, NoEsAprobador).IsAllowed);
        }

        // =====================================================================
        // 6. La transicion de apertura que escribe el cajero sigue siendo legal
        //     (candado de regresion sobre ReceptionGuard: el guard esta ACTIVO en DEV)
        // =====================================================================

        [Theory]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda, ReceptionGuard.BagAsignadaACajero)]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa, ReceptionGuard.BagAsignadaACajero)]
        public void AperturaDeTula_PatcheadaPorElCajero_SigueSiendoLegal(int previous, int next)
        {
            // 00 §2.2 punto 6: el cajero patchea la apertura (03 §6.3 y §9.1). Si esta
            // transicion deja de ser legal, la v2 no puede abrir NINGUNA tula.
            Assert.True(ReceptionGuard.EvaluateBagStatusTransition(previous, next).IsAllowed);
        }

        [Theory]
        [InlineData(ReceptionGuard.BagAsignadaACajero, ReceptionGuard.BagRecontada)]
        [InlineData(ReceptionGuard.BagAsignadaACajero, ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento, ReceptionGuard.BagAsignadaACajero)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento, ReceptionGuard.BagRecontada)]
        public void CierreDeTula_SigueSiendoLegal(int previous, int next)
        {
            Assert.True(ReceptionGuard.EvaluateBagStatusTransition(previous, next).IsAllowed);
        }

        [Fact]
        public void TulaVarada_NoPuedeVolverSolaABoveda()
        {
            // Plata contada a medias no vuelve sola al cofre. La salida de la tula
            // varada es "Devuelta para reasignacion", no boveda.
            Assert.False(ReceptionGuard.EvaluateBagStatusTransition(
                ReceptionGuard.BagAsignadaACajero, ReceptionGuard.BagAlmacenadaEnBoveda).IsAllowed);
        }

        // =====================================================================
        // 4-ter. Devolucion para reasignacion
        //
        //   Hoy una tula que entra en "Asignada a cajero" queda ENLLAVADA: sus
        //   unicas salidas son Recontada (terminal) y el estado de revision del
        //   supervisor, que significa otra cosa —una diferencia que hay que
        //   aprobar—. Si el cajero se va, si cae el cierre de jornada o si
        //   simplemente se equivoco de tula, no hay forma de sacarla.
        //
        //   El estado nuevo dice lo que de verdad paso: la tula se devolvio para
        //   que la tome otro. No se reutiliza el estado de supervisor justamente
        //   para no confundir "hay una diferencia que revisar" con "esta libre".
        //
        //   ENTRA SOLO DESDE "Asignada a cajero", y esa restriccion es la que
        //   mantiene la operacion segura: los asientos contables se arman al pasar
        //   a Recontada, asi que una tula devuelta NUNCA tiene contabilidad que
        //   desarmar. Devolver una tula ya recontada seria otro problema.
        //
        //   SALE SOLO hacia "Asignada a cajero": la retoma un cajero reescaneandola.
        // =====================================================================

        [Fact]
        public void Devolucion_EntraSoloDesdeAsignadaACajero()
        {
            Assert.True(ReceptionGuard.EvaluateBagStatusTransition(
                ReceptionGuard.BagAsignadaACajero,
                ReceptionGuard.BagDevueltaParaReasignacion).IsAllowed);
        }

        [Theory]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData(ReceptionGuard.BagRecontada)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorEsclusa)]
        public void Devolucion_DesdeCualquierOtroEstado_SeBloquea(int previous)
        {
            // BagRecontada es el que mas importa: si esta transicion se abriera,
            // habria que desarmar los asientos contables de la tula.
            Assert.False(ReceptionGuard.EvaluateBagStatusTransition(
                previous, ReceptionGuard.BagDevueltaParaReasignacion).IsAllowed);
        }

        [Fact]
        public void Devuelta_SoloSaleHaciaAsignadaACajero()
        {
            Assert.True(ReceptionGuard.EvaluateBagStatusTransition(
                ReceptionGuard.BagDevueltaParaReasignacion,
                ReceptionGuard.BagAsignadaACajero).IsAllowed);
        }

        [Theory]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData(ReceptionGuard.BagRecontada)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorEsclusa)]
        public void Devuelta_NoSaleHaciaNingunOtroEstado(int next)
        {
            // Decision explicita: NO vuelve a boveda. La tula devuelta se retoma
            // reescaneandola, y punto.
            Assert.False(ReceptionGuard.EvaluateBagStatusTransition(
                ReceptionGuard.BagDevueltaParaReasignacion, next).IsAllowed);
        }

        [Fact]
        public void Devolucion_TulaEnMesa_SePermite()
        {
            var result = EvaluateBagReturn(true, ReceptionGuard.BagAsignadaACajero);
            Assert.True(result.IsAllowed);
            Assert.Null(result.Reason);
        }

        [Fact]
        public void Devolucion_TulaYaRecontada_SeBloqueaConMotivoContable()
        {
            var result = EvaluateBagReturn(true, ReceptionGuard.BagRecontada);
            Assert.False(result.IsAllowed);
            Assert.Contains("Recontada", result.Reason);
        }

        [Theory]
        [InlineData(ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData(ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData(ReceptionGuard.BagPendienteSupervisorEsclusa)]
        [InlineData(ReceptionGuard.BagDevueltaParaReasignacion)]
        public void Devolucion_TulaFueraDeMesa_SeBloquea(int bagStatus)
        {
            Assert.False(EvaluateBagReturn(true, bagStatus).IsAllowed);
        }

        // =====================================================================
        // 4-quater. La tula tiene DUEÑO, no solo estado (el resto de S-5)
        //
        //   IsWriteAllowed mira UNICAMENTE el estado de la tula. Alcanzaba
        //   mientras "Asignada a cajero" fuera un punto sin retorno, pero con la
        //   devolucion dejo de serlo, y se abrio esta secuencia:
        //
        //     1. el cajero A esta recontando la tula X
        //     2. el supervisor la devuelve       -> X queda sin sesion
        //     3. el cajero B la escanea          -> X vuelve a "Asignada a cajero"
        //     4. el cajero A, que sigue en la pantalla, guarda
        //
        //   En el paso 4 el estado ES "Asignada a cajero", asi que el guard
        //   autorizaba: A escribia depositos sobre la tula de B, y podia cerrarla
        //   generando los asientos contables. COMPROBADO contra el entorno antes
        //   de escribir esto.
        //
        //   El estado responde "¿esta sobre una mesa?". Falta "¿sobre CUAL?".
        // =====================================================================

        [Fact]
        public void Duenio_ElCajeroDeLaSesion_Escribe()
        {
            var cajero = Guid.NewGuid();
            var result = IsWriteOwner(true, cajero, cajero);
            Assert.True(result.IsAllowed);
            Assert.Null(result.Reason);
        }

        [Fact]
        public void Duenio_OtroCajero_SeBloquea()
        {
            // El caso de la secuencia de arriba, que es el que motiva la regla.
            var result = IsWriteOwner(true, Guid.NewGuid(), Guid.NewGuid());
            Assert.False(result.IsAllowed);
            Assert.Contains("otro cajero", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Duenio_SesionNoResoluble_SeBloquea()
        {
            // Fail-closed, igual que en todo el resto del guard: si no se puede
            // saber de quien es la tula, no se escribe.
            Assert.False(IsWriteOwner(false, null, Guid.NewGuid()).IsAllowed);
        }

        [Fact]
        public void Duenio_SesionSinCajero_SeBloquea()
        {
            // Una sesion sin cajero no es de nadie, y "de nadie" no es "de todos".
            Assert.False(IsWriteOwner(true, null, Guid.NewGuid()).IsAllowed);
        }

        [Fact]
        public void Duenio_ActorVacio_SeBloquea()
        {
            var cajero = Guid.NewGuid();
            Assert.False(IsWriteOwner(true, cajero, Guid.Empty).IsAllowed);
        }

        [Fact]
        public void Duenio_ServicePrincipal_Exento()
        {
            // El SP siembra y despliega; no es un cajero. La regla existe para que un
            // cajero no pise la tula de otro, y quien tiene las credenciales del SP ya
            // puede todo por Web API: exigirle que "sea el cajero" no suma seguridad y
            // rompe la tooling de datos.
            Assert.True(IsWriteOwner(true, Guid.NewGuid(), Guid.NewGuid(), true).IsAllowed);
            Assert.True(IsWriteOwner(false, null, Guid.Empty, true).IsAllowed);
        }

        [Fact]
        public void Duenio_ServicePrincipal_NoLoEximeDelControlDeEstado()
        {
            // Ni el SP carga plata en una tula que no esta sobre una mesa.
            Assert.False(IsWriteAllowed(false, true, ReceptionGuard.BagAlmacenadaEnBoveda).IsAllowed);
            Assert.False(IsWriteAllowed(false, true, ReceptionGuard.BagDevueltaParaReasignacion).IsAllowed);
        }

        [Fact]
        public void Duenio_NoReemplazaAlControlDeEstado()
        {
            // Las dos reglas son independientes y se piden LAS DOS: ser el dueño no
            // habilita a escribir sobre una tula que ya no esta en la mesa.
            var cajero = Guid.NewGuid();
            Assert.True(IsWriteOwner(true, cajero, cajero).IsAllowed);
            Assert.False(IsWriteAllowed(false, true, ReceptionGuard.BagRecontada).IsAllowed);
        }

        [Fact]
        public void Devolucion_TulaNoResoluble_SeBloquea()
        {
            // Fail-closed: la operacion BORRA depositos, cheques y denominaciones.
            // Sin saber en que estado esta la tula no se borra nada.
            Assert.False(EvaluateBagReturn(false, null).IsAllowed);
            Assert.False(EvaluateBagReturn(true, null).IsAllowed);
        }

        // =====================================================================
        // 4-bis. Cierre de jornada: una sesion es de UN dia
        //
        //   gvarSesion se resuelve UNA vez, en scrSesion.OnVisible, y ninguna
        //   pantalla vuelve a mirarlo: scrTulas solo pregunta si esta en blanco.
        //   Una pestana abierta cruzando la medianoche sigue imputando el trabajo
        //   de hoy a la sesion de ayer. La consulta ya filtra por Today(), asi que
        //   toda la app YA asume "una sesion = un dia"; el unico lugar donde esa
        //   regla no se aplica es la variable del navegador. Aca se aplica del lado
        //   del servidor, que es el unico que no depende del reloj del cajero.
        //
        //   El corte es la MEDIANOCHE DE CALENDARIO en hora local (decision de
        //   negocio), para que coincida con el Today() de las consultas.
        // =====================================================================

        [Fact]
        public void Jornada_SesionDeHoyAbierta_Permite()
        {
            var ahora = new DateTime(2026, 8, 30, 14, 30, 0);
            var result = IsSessionWithinBusinessDay(true, new DateTime(2026, 8, 30, 7, 46, 0), false, ahora);
            Assert.True(result.IsAllowed);
            Assert.Null(result.Reason);
        }

        [Fact]
        public void Jornada_SesionDeAyer_SeBloquea()
        {
            // El caso real: el cajero dejo la app abierta y volvio al dia siguiente.
            var ahora = new DateTime(2026, 8, 30, 8, 0, 0);
            var result = IsSessionWithinBusinessDay(true, new DateTime(2026, 8, 29, 16, 0, 0), false, ahora);
            Assert.False(result.IsAllowed);
            Assert.Contains("29/08/2026", result.Reason);
        }

        [Fact]
        public void Jornada_UnSegundoDespuesDeMedianoche_SeBloquea()
        {
            // El borde exacto. 23:59:59 y 00:00:01 son dias distintos: no hay
            // tolerancia, porque cualquier gracia habria que justificarla y auditarla.
            var result = IsSessionWithinBusinessDay(
                true,
                new DateTime(2026, 8, 29, 23, 59, 59),
                false,
                new DateTime(2026, 8, 30, 0, 0, 1));
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Jornada_ElUltimoSegundoDelDiaPropio_Permite()
        {
            var result = IsSessionWithinBusinessDay(
                true,
                new DateTime(2026, 8, 30, 0, 0, 0),
                false,
                new DateTime(2026, 8, 30, 23, 59, 59));
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Jornada_CerradaPorJornada_SeBloqueaAunqueSeaDeHoy()
        {
            // La bandera manda sobre la fecha: si el job ya la cerro, cerrada esta.
            // Es columna PROPIA, no SessionForciblyClosed — esa es la decision del
            // supervisor y lleva autor y motivo; esta es automatica y no tiene autor.
            var ahora = new DateTime(2026, 8, 30, 10, 0, 0);
            var result = IsSessionWithinBusinessDay(true, new DateTime(2026, 8, 30, 7, 0, 0), true, ahora);
            Assert.False(result.IsAllowed);
            Assert.Contains("cerrada", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Jornada_SesionNoResoluble_SeBloquea()
        {
            // Fail-closed, igual que IsWriteAllowed sin tula: si no se sabe de que dia
            // es la sesion, no se autoriza. Nunca al reves.
            var ahora = new DateTime(2026, 8, 30, 10, 0, 0);
            Assert.False(IsSessionWithinBusinessDay(false, null, false, ahora).IsAllowed);
            Assert.False(IsSessionWithinBusinessDay(true, null, false, ahora).IsAllowed);
        }

        [Fact]
        public void Jornada_SesionConFechaFutura_SeBloquea()
        {
            // Reloj corrido o dato sembrado a mano: tampoco pasa. La regla es
            // "el MISMO dia", no "hasta hoy".
            var result = IsSessionWithinBusinessDay(
                true,
                new DateTime(2026, 8, 31, 9, 0, 0),
                false,
                new DateTime(2026, 8, 30, 10, 0, 0));
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Recontada_SigueSiendoTerminal()
        {
            // Si alguna vez deja de serlo, la idempotencia de los asientos pasa de ser
            // una red de seguridad a ser el UNICO freno contra la plata duplicada.
            Assert.False(ReceptionGuard.EvaluateBagStatusTransition(
                ReceptionGuard.BagRecontada, ReceptionGuard.BagAsignadaACajero).IsAllowed);
        }

        // ---- helpers ----

        private static DepositAmounts Deposito(int? currency, decimal cash, decimal bac, decimal other)
        {
            return new DepositAmounts
            {
                Currency = currency,
                CashAmount = cash,
                BacCheckAmount = bac,
                OtherCheckAmount = other,
            };
        }

        private static DepositForTransaction Trx(Guid id, decimal bac = 0m, decimal other = 0m)
        {
            return new DepositForTransaction
            {
                DepositId = id,
                Currency = CurrencyNio,
                CashAmount = 1m,
                BacCheckAmount = bac,
                OtherCheckAmount = other,
            };
        }
    }
}
