using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El estado 'Devuelto': el aprobador devolvio la carga en el IBS Branch.
    ///
    /// POR QUE IMPORTA. Hasta ahora el ciclo del lote era ESTRICTAMENTE hacia adelante
    /// (Generado -> Ingresado -> Aplicado) y cualquier retroceso estaba bloqueado. Pero
    /// la realidad del IBS tiene un camino que la aplicacion no reflejaba: el aprobador
    /// revisa la carga y, si algo esta mal, la DEVUELVE. Ese lote quedaba atrapado en
    /// 'Ingresado' —un estado que dice "ya entro al Core"— cuando en realidad no entro
    /// nada y hay que corregirlo. La app mentia sobre donde estaba la plata.
    ///
    /// 'Devuelto' es el PRIMER retroceso legal del ciclo, y por eso se define con
    /// precision: de donde se entra, a donde se sale, quien puede hacerlo y que se
    /// exige para hacerlo.
    /// </summary>
    public class BatchReturnedGuardTests
    {
        private static readonly Guid Digitador = Guid.NewGuid();
        private static readonly Guid Aprobador = Guid.NewGuid();

        private const string Motivo = "El IBS rechazo la cuenta 992983992.";

        private static TransactionGuardResult Transicion(
            int? desde, int? hasta, Guid actor, Guid? ingresadoPor = null, string motivo = Motivo) =>
            TransactionGuard.EvaluateBatchUpdate(
                previousStatus: desde,
                statusInTarget: true,
                newStatus: hasta,
                touchesOnlyTotals: false,
                initiatingUserId: actor,
                ingresadoPor: ingresadoPor,
                returnReason: motivo);

        // ---- Entrada a Devuelto ----

        [Fact]
        public void IngresadoADevuelto_LoPuedeHacerAlguienDistintoDeQuienIngreso()
        {
            var r = Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchDevuelto,
                actor: Aprobador, ingresadoPor: Digitador);

            Assert.True(r.IsAllowed);
        }

        [Fact]
        public void IngresadoADevuelto_QuienIngresoNoPuedeDevolverSuPropioLote()
        {
            // SEGREGACION DE FUNCIONES, igual que en Aplicado. Aplicar y devolver son las
            // DOS caras de la misma decision del aprobador: si quien cargo el lote pudiera
            // registrar el veredicto, se estaria revisando a si mismo.
            var r = Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchDevuelto,
                actor: Digitador, ingresadoPor: Digitador);

            Assert.False(r.IsAllowed);
            Assert.Contains("Segregacion", r.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void IngresadoADevuelto_SinEstampaDeQuienIngreso_NoSeBloquea()
        {
            // Los lotes viejos no tienen sanic_ingresadopor. No se los deja varados por
            // una estampa que no existia cuando nacieron — mismo criterio que Aplicado.
            var r = Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchDevuelto,
                actor: Digitador, ingresadoPor: null);

            Assert.True(r.IsAllowed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IngresadoADevuelto_SinMotivo_SeBloquea(string motivo)
        {
            // El motivo es OBLIGATORIO. Quien tiene que corregir el lote necesita saber
            // QUE corregir: una devolucion sin motivo le dice "esta mal" y nada mas, y lo
            // obliga a ir a preguntar al IBS.
            var r = Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchDevuelto,
                actor: Aprobador, ingresadoPor: Digitador, motivo: motivo);

            Assert.False(r.IsAllowed);
            Assert.Contains("motivo", r.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GeneradoADevuelto_SeBloquea()
        {
            // Un lote que nunca se cargo al IBS no puede volver de el.
            var r = Transicion(TransactionGuard.BatchGenerado, TransactionGuard.BatchDevuelto, Aprobador);

            Assert.False(r.IsAllowed);
        }

        [Fact]
        public void AplicadoADevuelto_SeBloquea()
        {
            // Aplicado sigue siendo terminal e inmutable: lo que ya entro al Core no se
            // deshace desde la aplicacion.
            var r = Transicion(TransactionGuard.BatchAplicado, TransactionGuard.BatchDevuelto, Aprobador);

            Assert.False(r.IsAllowed);
        }

        // ---- Salida de Devuelto ----

        [Fact]
        public void DevueltoAIngresado_SePermite_EsLaRecarga()
        {
            // Se corrigio y se volvio a cargar al IBS. El motivo no se exige al volver:
            // solo la devolucion lo necesita.
            var r = Transicion(TransactionGuard.BatchDevuelto, TransactionGuard.BatchIngresado,
                actor: Digitador, ingresadoPor: Digitador, motivo: null);

            Assert.True(r.IsAllowed);
        }

        [Fact]
        public void DevueltoAAplicado_SeBloquea()
        {
            // NO hay atajo. Un lote devuelto tiene que volver a pasar por el IBS: si
            // pudiera saltar a Aplicado, se estaria marcando como cargado algo que el
            // Core rechazo y nadie volvio a presentar.
            var r = Transicion(TransactionGuard.BatchDevuelto, TransactionGuard.BatchAplicado,
                actor: Aprobador, ingresadoPor: Digitador);

            Assert.False(r.IsAllowed);
        }

        [Fact]
        public void DevueltoAGenerado_SeBloquea()
        {
            var r = Transicion(TransactionGuard.BatchDevuelto, TransactionGuard.BatchGenerado, Aprobador);

            Assert.False(r.IsAllowed);
        }

        [Fact]
        public void DevueltoADevuelto_SeBloquea()
        {
            // El no-op tambien se rechaza, como en el resto de la maquina.
            var r = Transicion(TransactionGuard.BatchDevuelto, TransactionGuard.BatchDevuelto, Aprobador);

            Assert.False(r.IsAllowed);
        }

        // ---- Lo que NO cambia ----

        [Fact]
        public void ElCaminoFeliz_SigueIntacto()
        {
            Assert.True(Transicion(TransactionGuard.BatchGenerado, TransactionGuard.BatchIngresado, Digitador).IsAllowed);
            Assert.True(Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchAplicado,
                actor: Aprobador, ingresadoPor: Digitador).IsAllowed);
            Assert.False(Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchAplicado,
                actor: Digitador, ingresadoPor: Digitador).IsAllowed);
        }

        [Fact]
        public void Aprobado_SigueProhibidoEnCualquierDireccion()
        {
            Assert.False(Transicion(TransactionGuard.BatchIngresado, TransactionGuard.BatchAprobado, Aprobador).IsAllowed);
            Assert.False(Transicion(TransactionGuard.BatchAprobado, TransactionGuard.BatchIngresado, Aprobador).IsAllowed);
            Assert.False(Transicion(TransactionGuard.BatchDevuelto, TransactionGuard.BatchAprobado, Aprobador).IsAllowed);
        }

        // ---- Sacar transacciones del lote ----

        [Fact]
        public void SacarDelLote_SePermiteEnGeneradoYDevuelto()
        {
            Assert.True(TransactionGuard.CanRemoveFromCut(TransactionGuard.BatchGenerado));
            Assert.True(TransactionGuard.CanRemoveFromCut(TransactionGuard.BatchDevuelto));
        }

        [Fact]
        public void SacarDelLote_NoSePermiteMientrasEstaEnElIBS()
        {
            // Un lote Ingresado esta EN MANOS DEL APROBADOR. Sacarle una transaccion por
            // atras dejaria el archivo que el tiene en pantalla diciendo una cosa y el
            // lote de CashHub diciendo otra. Si hay algo que corregir, el aprobador lo
            // DEVUELVE — y recien ahi el lote vuelve a ser editable.
            Assert.False(TransactionGuard.CanRemoveFromCut(TransactionGuard.BatchIngresado));
        }

        [Fact]
        public void SacarDelLote_NoSePermiteEnAplicadoNiAprobadoNiSinLote()
        {
            Assert.False(TransactionGuard.CanRemoveFromCut(TransactionGuard.BatchAplicado));
            Assert.False(TransactionGuard.CanRemoveFromCut(TransactionGuard.BatchAprobado));
            Assert.False(TransactionGuard.CanRemoveFromCut(null));
        }

        [Fact]
        public void DesasignarDeUnLoteDevuelto_SePermite()
        {
            // Es el punto de todo: el lote volvio del IBS y hay que sacarle la transaccion
            // que lo hizo rebotar.
            var r = TransactionGuard.EvaluateTransactionUpdate(
                touchesOnlyMirror: false,
                statusInTarget: false,
                newStatus: null,
                previousStatus: null,
                batchInTarget: true,
                newBatchIsNull: true,
                currentBatchStatus: TransactionGuard.BatchDevuelto,
                targetBatchStatus: null,
                hadBatch: true);

            Assert.True(r.IsAllowed);
        }

        [Fact]
        public void EditarMontosEnUnLoteDevuelto_SeBloquea()
        {
            // El lote YA entro al Core una vez. La correccion es SACAR la transaccion del
            // lote, no reescribirle el monto: si se pudiera editar en el lugar, el archivo
            // que se mando al Core y lo que quedo en la base dejarian de coincidir.
            var r = TransactionGuard.EvaluateTransactionUpdate(
                touchesOnlyMirror: false,
                statusInTarget: false,
                newStatus: null,
                previousStatus: null,
                batchInTarget: false,
                newBatchIsNull: false,
                currentBatchStatus: TransactionGuard.BatchDevuelto,
                targetBatchStatus: null,
                hadBatch: true,
                touchesAmounts: true);

            Assert.False(r.IsAllowed);
        }

        [Fact]
        public void AgregarTransaccionesAUnLoteDevuelto_SeBloquea()
        {
            // Solo se agrega a un lote Generado. Un lote devuelto se corrige sacando lo
            // que sobra, no metiendole cosas nuevas: lo que se recargue al IBS tiene que
            // ser el mismo lote menos los rechazos, no un lote distinto.
            var r = TransactionGuard.EvaluateTransactionUpdate(
                touchesOnlyMirror: false,
                statusInTarget: false,
                newStatus: null,
                previousStatus: null,
                batchInTarget: true,
                newBatchIsNull: false,
                currentBatchStatus: null,
                targetBatchStatus: TransactionGuard.BatchDevuelto,
                hadBatch: false);

            Assert.False(r.IsAllowed);
        }
    }
}
