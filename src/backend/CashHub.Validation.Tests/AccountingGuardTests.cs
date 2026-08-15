using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.AccountingGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion de la CONTABILIDAD del circuito de aprobacion de recuentos
    /// (B-5 del plan, hallazgo H-02). El agujero que cierra: los asientos Sobrante /
    /// Faltante / 'Sin declaracion' / Deposito los calcula Power Fx dentro de las
    /// apps y el servidor acepta el monto que le manden, asi que con el rol de
    /// cajero y la Web API (o Excel) se fabrica un Sobrante de cualquier importe
    /// contra cualquier deposito, o se duplica un asiento ya generado.
    ///
    /// El caso que manda: la COMPENSACION. Un deposito con +200.000 de efectivo y
    /// -100.000 de cheques BAC no es "un sobrante de 100.000": son DOS asientos,
    /// Sobrante(200.000 efectivo) y Faltante(100.000 BAC). Sumar el neto fue el bug
    /// historico y por eso todo se recalcula componente a componente.
    /// </summary>
    public class AccountingGuardTests
    {
        // Option set global sanic_chchcurrency.
        private const int Nio = 159460000;
        private const int Usd = 159460001;

        private static AccountingAmounts A(decimal cash, decimal bac, decimal other)
            => new AccountingAmounts(cash, bac, other);

        private static DepositFacts Dep(decimal cash, decimal bac, decimal other, int currency = Nio)
            => new DepositFacts(A(cash, bac, other), currency);

        /// <summary>Atajo del Create con defaults: moneda NIO y sin duplicado previo.</summary>
        private static TransactionGuardResult Eval(
            int? type,
            AccountingAmounts amounts,
            DepositFacts? recounted = null,
            DepositFacts? declared = null,
            int? currency = Nio,
            bool duplicate = false,
            DepositFacts? unrecountedDeclared = null,
            bool? hasRecountingRef = null)
        {
            return EvaluateAccountingCreate(
                type,
                currency,
                amounts,
                hasRecountingRef ?? recounted.HasValue,
                recounted,
                declared,
                duplicate,
                unrecountedDeclared);
        }

        // ---- Sobrante: solo los excedentes, componente a componente ----

        [Fact]
        public void Sobrante_ConMontosRecalculados_IsAllowed()
        {
            // Recontado 1.500 efectivo contra 1.000 declarados: sobran 500.
            var result = Eval(
                TipoSobrante, A(500, 0, 0),
                recounted: Dep(1500, 200, 0),
                declared: Dep(1000, 200, 0));

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Sobrante_ConMontosInflados_IsBlocked()
        {
            // El asiento dice 5.000 de efectivo; el servidor recalcula 500.
            var result = Eval(
                TipoSobrante, A(5000, 0, 0),
                recounted: Dep(1500, 200, 0),
                declared: Dep(1000, 200, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("efectivo", result.Reason);
        }

        [Fact]
        public void Sobrante_ConChequesInventados_IsBlocked()
        {
            // El efectivo cuadra pero se cuela un cheque BAC que nadie conto.
            var result = Eval(
                TipoSobrante, A(500, 750, 0),
                recounted: Dep(1500, 200, 0),
                declared: Dep(1000, 200, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("cheques BAC", result.Reason);
        }

        [Fact]
        public void Sobrante_SinDiferenciaPositiva_IsBlocked()
        {
            // El recuento no supera a lo declarado en ningun componente: no hay
            // sobrante que contabilizar y un asiento en cero no se crea.
            var result = Eval(
                TipoSobrante, A(0, 0, 0),
                recounted: Dep(1000, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("sobrante", result.Reason);
        }

        [Fact]
        public void Sobrante_DeUnRecontadoSinDeclarar_LlevaElMontoEntero()
        {
            // Deposito recontado que nadie declaro: sobra TODO (guia 03 §11 9.a).
            var result = Eval(
                TipoSobrante, A(3000, 0, 450),
                recounted: Dep(3000, 0, 450),
                declared: null);

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Sobrante_DeUnRecontadoSinDeclarar_ConOtroMonto_IsBlocked()
        {
            var result = Eval(
                TipoSobrante, A(9000, 0, 450),
                recounted: Dep(3000, 0, 450),
                declared: null);

            Assert.False(result.IsAllowed);
        }

        // ---- Faltante: solo los defectos, en valor absoluto ----

        [Fact]
        public void Faltante_ConMontosRecalculados_IsAllowed()
        {
            // Se declararon 1.000 de efectivo y se contaron 600: faltan 400.
            var result = Eval(
                TipoFaltante, A(400, 0, 0),
                recounted: Dep(600, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Faltante_ConSignoInvertido_IsBlocked()
        {
            // Un faltante se contabiliza en ABSOLUTO; mandar el monto del recuento
            // en vez de la diferencia no cuadra.
            var result = Eval(
                TipoFaltante, A(600, 0, 0),
                recounted: Dep(600, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Faltante_SinDeclarado_IsBlocked()
        {
            // Sin declaracion no hay contra que medir: un recontado sin declarar
            // genera Sobrante, nunca Faltante.
            var result = Eval(
                TipoFaltante, A(600, 0, 0),
                recounted: Dep(600, 0, 0),
                declared: null);

            Assert.False(result.IsAllowed);
            Assert.Contains("declarado", result.Reason);
        }

        [Fact]
        public void Faltante_SinDiferenciaNegativa_IsBlocked()
        {
            var result = Eval(
                TipoFaltante, A(0, 0, 0),
                recounted: Dep(1000, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("faltante", result.Reason);
        }

        // ---- COMPENSACION: dos asientos por el MISMO deposito ----

        [Fact]
        public void Compensacion_SobranteDeEfectivoYFaltanteDeCheques_AmbosAsientosPasan()
        {
            // +200.000 de efectivo y -100.000 de cheques BAC sobre el mismo deposito.
            // El neto (+100.000) NO existe en este dominio: salen DOS asientos y el
            // guard tiene que aceptar los dos.
            var recontado = Dep(1_200_000, 400_000, 0);
            var declarado = Dep(1_000_000, 500_000, 0);

            var sobrante = Eval(TipoSobrante, A(200_000, 0, 0), recontado, declarado);
            var faltante = Eval(TipoFaltante, A(0, 100_000, 0), recontado, declarado);

            Assert.True(sobrante.IsAllowed);
            Assert.True(faltante.IsAllowed);
        }

        [Fact]
        public void Compensacion_UnSoloAsientoPorElNeto_IsBlocked()
        {
            // Exactamente el bug que el circuito arrastraba: Abs(recontado - declarado)
            // sobre el neto hacia desaparecer una discrepancia.
            var result = Eval(
                TipoSobrante, A(100_000, 0, 0),
                recounted: Dep(1_200_000, 400_000, 0),
                declared: Dep(1_000_000, 500_000, 0));

            Assert.False(result.IsAllowed);
        }

        // ---- Deposito: refleja lo RECONTADO ----

        [Fact]
        public void Deposito_ConMontosDelRecontado_IsAllowed()
        {
            // Verificado en los dos productores vivos (CH-App-Recounting cuando la
            // tula cierra sin diferencia, y el supervisor al aprobar con diferencia).
            var result = Eval(
                TipoDeposito, A(1500, 200, 0),
                recounted: Dep(1500, 200, 0),
                declared: Dep(1000, 200, 0));

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Deposito_ConMontosDelDeclarado_IsBlocked()
        {
            var result = Eval(
                TipoDeposito, A(1000, 200, 0),
                recounted: Dep(1500, 200, 0),
                declared: Dep(1000, 200, 0));

            Assert.False(result.IsAllowed);
        }

        // ---- 'Sin declaracion': el recuento entero, y solo sin declaracion ----

        [Fact]
        public void SinDeclaracion_ConMontosDelRecontado_IsAllowed()
        {
            var result = Eval(
                TipoSinDeclaracion, A(800, 0, 120),
                recounted: Dep(800, 0, 120),
                declared: null);

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void SinDeclaracion_ConMontosPropios_IsBlocked()
        {
            var result = Eval(
                TipoSinDeclaracion, A(80_000, 0, 120),
                recounted: Dep(800, 0, 120),
                declared: null);

            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void SinDeclaracion_SobreUnRecontadoQueSiTieneDeclaracion_IsBlocked()
        {
            // Anti-evasion: etiquetar 'Sin declaracion' un deposito que SI tiene
            // declarado esquivaria la resta por componente y blanquearia la diferencia.
            var result = Eval(
                TipoSinDeclaracion, A(1500, 0, 0),
                recounted: Dep(1500, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("declaracion", result.Reason);
        }

        // ---- Idempotencia: un asiento por (recuento, tipo) ----

        [Fact]
        public void AsientoDuplicado_IsBlocked()
        {
            var result = Eval(
                TipoSobrante, A(500, 0, 0),
                recounted: Dep(1500, 0, 0),
                declared: Dep(1000, 0, 0),
                duplicate: true);

            Assert.False(result.IsAllowed);
            Assert.Contains("YA tiene un asiento", result.Reason);
        }

        [Fact]
        public void AsientoDuplicado_DeDeposito_IsBlocked()
        {
            // Los Deposito legitimos por deposito recontado son EXACTAMENTE 1; es la
            // misma unicidad que impone la alternate key sanic_KeyAsientoPorRecuento.
            var result = Eval(
                TipoDeposito, A(1500, 0, 0),
                recounted: Dep(1500, 0, 0),
                duplicate: true);

            Assert.False(result.IsAllowed);
        }

        // ---- Ancla: sin recuento asociado ----

        [Fact]
        public void Sobrante_SinRecuentoAsociado_IsBlocked()
        {
            var result = Eval(TipoSobrante, A(500, 0, 0), hasRecountingRef: false);

            Assert.False(result.IsAllowed);
            Assert.Contains("RelatedDepositRecounting", result.Reason);
        }

        [Fact]
        public void SinDeclaracion_SinRecuentoAsociado_IsBlocked()
        {
            var result = Eval(TipoSinDeclaracion, A(500, 0, 0), hasRecountingRef: false);

            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Faltante_SinRecuentoYSinDeclaradoQueLoRespalde_IsBlocked()
        {
            var result = Eval(TipoFaltante, A(500, 0, 0), hasRecountingRef: false);

            Assert.False(result.IsAllowed);
            Assert.Contains("declarado sin recontar", result.Reason);
        }

        [Fact]
        public void Faltante_DeUnDeclaradoQueNadieReconto_IsAllowed()
        {
            // Guia 03 §11 9.b (y su equivalente en la app vieja): el declarado que
            // nadie reconto genera el par Faltante + Deposito por el monto declarado.
            // No hay recuento al cual anclarlo porque el recuento NO EXISTE, asi que
            // el respaldo es el propio declarado sin recontar.
            var declaradoSinRecontar = Dep(2000, 300, 0);

            var faltante = Eval(
                TipoFaltante, A(2000, 300, 0),
                hasRecountingRef: false,
                unrecountedDeclared: declaradoSinRecontar);
            var deposito = Eval(
                TipoDeposito, A(2000, 300, 0),
                hasRecountingRef: false,
                unrecountedDeclared: declaradoSinRecontar);

            Assert.True(faltante.IsAllowed);
            Assert.True(deposito.IsAllowed);
        }

        [Fact]
        public void Faltante_DeUnDeclaradoSinRecontar_ConOtroMonto_IsBlocked()
        {
            var result = Eval(
                TipoFaltante, A(9000, 300, 0),
                hasRecountingRef: false,
                unrecountedDeclared: Dep(2000, 300, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("efectivo", result.Reason);
        }

        // ---- Moneda ----

        [Fact]
        public void MonedaDistintaDelDeposito_IsBlocked()
        {
            // Montos correctos pero moneda cambiada: el asiento diria dolares sobre un
            // recuento en cordobas.
            var result = Eval(
                TipoSobrante, A(500, 0, 0),
                recounted: Dep(1500, 0, 0, Nio),
                declared: Dep(1000, 0, 0, Nio),
                currency: Usd);

            Assert.False(result.IsAllowed);
            Assert.Contains("moneda", result.Reason);
        }

        [Fact]
        public void SinMoneda_IsBlocked()
        {
            var result = Eval(
                TipoSobrante, A(500, 0, 0),
                recounted: Dep(1500, 0, 0),
                declared: Dep(1000, 0, 0),
                currency: null);

            Assert.False(result.IsAllowed);
        }

        // ---- Alcance del guard ----

        [Fact]
        public void TransaccionDeDigitacion_SinTipoNiRecuento_IsAllowed()
        {
            // El circuito de digitacion crea transacciones sin tipo contable y sin
            // recuento: no es asunto de este guard (y no gasta una sola consulta).
            var result = Eval(null, A(1000, 0, 0), hasRecountingRef: false);

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void TransaccionConRecuentoPeroSinTipo_IsBlocked()
        {
            // Un asiento sin tipo es invisible para la idempotencia y para el Core.
            var result = Eval(null, A(1000, 0, 0), recounted: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
            Assert.Contains("tipo", result.Reason);
        }

        [Fact]
        public void RecuentoIlegible_IsBlocked()
        {
            var result = EvaluateAccountingCreate(
                TipoSobrante, Nio, A(500, 0, 0),
                hasRecountingRef: true,
                recounted: null, declared: null,
                duplicateExists: false, unrecountedDeclaredCandidate: null);

            Assert.False(result.IsAllowed);
        }

        // ---- Aritmetica por componente (el corazon del recalculo) ----

        [Fact]
        public void Surplus_TomaSoloLosPositivos()
        {
            var sobrante = AccountingAmounts.Surplus(A(1200, 400, 90), A(1000, 500, 90));

            Assert.Equal(200m, sobrante.Cash);
            Assert.Equal(0m, sobrante.BacCheck);
            Assert.Equal(0m, sobrante.OtherCheck);
        }

        [Fact]
        public void Shortfall_TomaSoloLosNegativosEnAbsoluto()
        {
            var faltante = AccountingAmounts.Shortfall(A(1200, 400, 90), A(1000, 500, 90));

            Assert.Equal(0m, faltante.Cash);
            Assert.Equal(100m, faltante.BacCheck);
            Assert.Equal(0m, faltante.OtherCheck);
        }

        [Fact]
        public void ToleranciaCero_UnCentavoDeMasYaNoCuadra()
        {
            var result = Eval(
                TipoSobrante, A(500.01m, 0, 0),
                recounted: Dep(1500, 0, 0),
                declared: Dep(1000, 0, 0));

            Assert.False(result.IsAllowed);
        }
    }
}
