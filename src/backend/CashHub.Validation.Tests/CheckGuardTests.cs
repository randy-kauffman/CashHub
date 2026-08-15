using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.CheckGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del guard del handshake de recepcion de cheques
    /// (CH-Tbl-Checks). Los cheques viajan fisicamente del supervisor al
    /// digitador (esclusa): el supervisor los marca 'Enviado a digitacion', el
    /// digitador confirma 'Recibido' o marca 'No Recibido', y el supervisor puede
    /// reenviar un 'No Recibido'. Recibido CONGELA el registro (terminal).
    /// 'Digitado' y 'Rebotado' quedan reservados para la etapa futura de
    /// digitacion al Core: hoy toda transicion hacia o desde ellos se bloquea.
    /// </summary>
    public class CheckGuardTests
    {
        // ---- Create: el cheque nace sin estado (Recounting lo marca despues) ----

        [Fact]
        public void CheckCreate_SinEstado_IsAllowed()
        {
            // Los cheques NACEN sin estado; Recounting los marca Recontado despues.
            Assert.True(EvaluateCheckCreate(null).IsAllowed);
        }

        [Fact]
        public void CheckCreate_Recontado_IsAllowed()
        {
            Assert.True(EvaluateCheckCreate(Recontado).IsAllowed);
        }

        [Theory]
        [InlineData(Digitado)]           // reservado para la etapa futura
        [InlineData(Rebotado)]           // reservado para la etapa futura
        [InlineData(EnviadoADigitacion)] // no se nace en medio del handshake
        [InlineData(Recibido)]
        [InlineData(NoRecibido)]
        public void CheckCreate_NacidoAvanzado_IsBlocked(int bornStatus)
        {
            var result = EvaluateCheckCreate(bornStatus);
            Assert.False(result.IsAllowed);
            Assert.Contains("Recontado", result.Reason);
        }

        // ---- Update: Recibido es TERMINAL y CONGELA el registro ----

        [Fact]
        public void CheckUpdate_EditarDatosDeUnRecibido_IsBlocked()
        {
            // El congelamiento aplica a CUALQUIER edicion, incluso sin tocar el
            // estado: el cheque recibido no se toca mas.
            var result = EvaluateCheckUpdate(Recibido, statusInTarget: false, newStatus: null);
            Assert.False(result.IsAllowed);
            Assert.Contains("Recibido", result.Reason);
        }

        [Theory]
        [InlineData(Recibido)]           // ni siquiera el no-op
        [InlineData(EnviadoADigitacion)] // ni devolverlo a la esclusa
        [InlineData(Recontado)]          // ni retrocederlo al recuento
        [InlineData(Digitado)]           // ni adelantarlo a la etapa futura
        [InlineData(Rebotado)]
        [InlineData(null)]               // ni borrarle el estado
        public void CheckUpdate_TocarEstadoDeUnRecibido_IsBlocked(int? newStatus)
        {
            var result = EvaluateCheckUpdate(Recibido, statusInTarget: true, newStatus: newStatus);
            Assert.False(result.IsAllowed);
            Assert.Contains("Recibido", result.Reason);
        }

        // ---- Update: transiciones legales del handshake ----

        [Theory]
        [InlineData(null, Recontado)]                     // Recounting marca el recuento
        [InlineData(Recontado, Recontado)]                // no-op idempotente del recuento
        [InlineData(Recontado, EnviadoADigitacion)]       // supervisor manda a la esclusa
        [InlineData(EnviadoADigitacion, Recibido)]        // digitador confirma recepcion
        [InlineData(EnviadoADigitacion, NoRecibido)]      // digitador no lo encontro
        [InlineData(NoRecibido, EnviadoADigitacion)]      // reenvio del supervisor
        public void CheckUpdate_TransicionDelHandshake_IsAllowed(int? previous, int next)
        {
            Assert.True(EvaluateCheckUpdate(previous, statusInTarget: true, newStatus: next).IsAllowed);
        }

        // ---- Update: saltos, retrocesos, no-ops y null explicito ----

        [Theory]
        [InlineData(null, EnviadoADigitacion)]            // salto: nada entra a la esclusa sin recuento
        [InlineData(null, Recibido)]
        [InlineData(null, NoRecibido)]
        [InlineData(null, null)]                          // status null explicito sobre un sin-estado
        [InlineData(Recontado, Recibido)]                 // salto: se salta la esclusa
        [InlineData(Recontado, NoRecibido)]
        [InlineData(Recontado, null)]                     // borrar el estado
        [InlineData(EnviadoADigitacion, Recontado)]       // retroceso
        [InlineData(EnviadoADigitacion, EnviadoADigitacion)] // no-op fuera del recuento
        [InlineData(EnviadoADigitacion, null)]
        [InlineData(NoRecibido, Recontado)]               // retroceso
        [InlineData(NoRecibido, Recibido)]                // el digitador confirma sobre un reenvio, no directo
        [InlineData(NoRecibido, NoRecibido)]              // no-op
        [InlineData(NoRecibido, null)]
        public void CheckUpdate_TransicionIlegal_IsBlocked(int? previous, int? next)
        {
            var result = EvaluateCheckUpdate(previous, statusInTarget: true, newStatus: next);
            Assert.False(result.IsAllowed);
            Assert.Contains("handshake", result.Reason);
        }

        // ---- Update: Digitado y Rebotado estan RESERVADOS (etapa futura) ----

        [Theory]
        [InlineData(null, Digitado)]
        [InlineData(Recontado, Digitado)]
        [InlineData(EnviadoADigitacion, Digitado)]        // ni siquiera desde la esclusa
        [InlineData(NoRecibido, Digitado)]
        [InlineData(null, Rebotado)]
        [InlineData(Recontado, Rebotado)]
        [InlineData(EnviadoADigitacion, Rebotado)]
        [InlineData(NoRecibido, Rebotado)]
        public void CheckUpdate_HaciaEstadoReservado_IsBlocked(int? previous, int next)
        {
            var result = EvaluateCheckUpdate(previous, statusInTarget: true, newStatus: next);
            Assert.False(result.IsAllowed);
            Assert.Contains("reservado", result.Reason);
        }

        [Theory]
        [InlineData(Digitado, Recontado)]
        [InlineData(Digitado, EnviadoADigitacion)]
        [InlineData(Digitado, Digitado)]
        [InlineData(Rebotado, Recontado)]
        [InlineData(Rebotado, EnviadoADigitacion)]
        [InlineData(Rebotado, Rebotado)]
        public void CheckUpdate_DesdeEstadoReservado_IsBlocked(int previous, int next)
        {
            var result = EvaluateCheckUpdate(previous, statusInTarget: true, newStatus: next);
            Assert.False(result.IsAllowed);
            Assert.Contains("reservado", result.Reason);
        }

        // ---- Update: edicion de datos sin tocar el estado ----

        [Theory]
        [InlineData(null)]
        [InlineData(Recontado)]
        [InlineData(EnviadoADigitacion)]
        [InlineData(NoRecibido)]
        [InlineData(Digitado)] // reservado bloquea el ESTADO, no la edicion de datos
        [InlineData(Rebotado)]
        public void CheckUpdate_EditarDatosSinTocarEstado_IsAllowed(int? previous)
        {
            // Correcciones de datos del cheque: no es asunto del handshake
            // (salvo el congelamiento de Recibido, cubierto arriba).
            Assert.True(EvaluateCheckUpdate(previous, statusInTarget: false, newStatus: null).IsAllowed);
        }

        // ---- Update: escritura SOLO de espejos (sanic_bag + sanic_recountingsession) ----

        [Theory]
        [InlineData(null)]
        [InlineData(Recontado)]
        [InlineData(EnviadoADigitacion)]
        [InlineData(Recibido)]  // el espejo pasa AUN con el cheque congelado
        [InlineData(Digitado)]
        [InlineData(Rebotado)]
        public void CheckUpdate_SoloEspejo_IsAllowedInclusoCongelado(int? previous)
        {
            // Mismo racional que touchesOnlyTotals del lote TB: los espejos sanic_bag
            // (B6) y sanic_recountingsession (B-9) son metadata derivada
            // (CheckMirrorStampPlugin, su cascada desde la tula o el backfill), no una
            // edicion de negocio — el congelamiento no aplica.
            var result = EvaluateCheckUpdate(previous, statusInTarget: false,
                newStatus: null, touchesOnlyMirror: true);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void CheckUpdate_EspejoMasEstado_NoEsEspejo_IsBlockedSiEstaCongelado()
        {
            // "Solo espejo" NUNCA puede significar "espejo + estado": si el request
            // tambien escribe sanic_checkstatus, esto es negocio y el congelamiento de
            // Recibido manda, aunque el llamador arme mal el set de columnas.
            var result = EvaluateCheckUpdate(Recibido, statusInTarget: true,
                newStatus: EnviadoADigitacion, touchesOnlyMirror: true);

            Assert.False(result.IsAllowed);
            Assert.Contains("congela", result.Reason);
        }

        [Fact]
        public void CheckUpdate_EspejoMasEstado_TransicionIlegal_IsBlocked()
        {
            // Ni siquiera sobre un cheque no congelado el flag de espejo puede
            // habilitar una transicion que el handshake no admite.
            var result = EvaluateCheckUpdate(Recontado, statusInTarget: true,
                newStatus: Recibido, touchesOnlyMirror: true);

            Assert.False(result.IsAllowed);
        }
    }
}
