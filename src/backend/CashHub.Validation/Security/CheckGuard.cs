namespace CashHub.Validation.Security
{
    /// <summary>
    /// Maquina de estados del handshake de recepcion de cheques (CH-Tbl-Checks).
    ///
    /// Los cheques viajan fisicamente del supervisor al digitador (esclusa):
    /// CH-App-Supervisor los marca 'Enviado a digitacion', el digitador confirma
    /// 'Recibido' o marca 'No Recibido', y el supervisor puede reenviar un
    /// 'No Recibido'. El ciclo completo:
    ///
    ///   (sin estado) -> Recontado -> 'Enviado a digitacion' -> Recibido (terminal)
    ///                                        ^      |
    ///                                        |      v
    ///                                     'No Recibido' (reenviable)
    ///
    /// Recibido CONGELA el registro: bloquea cualquier edicion, no solo el estado.
    /// 'Digitado' y 'Rebotado' existen en el option set pero quedan RESERVADOS para
    /// la etapa futura de digitacion al Core: hoy toda transicion hacia o desde
    /// ellos se bloquea. Las apps condicionan botones en Power Fx, pero eso es
    /// cosmetico: este dominio codifica las reglas y GuardTransactionPlugin las
    /// impone en el pipeline.
    /// </summary>
    public static class CheckGuard
    {
        // Option set sanic_checkstatus de CH-Tbl-Checks.
        public const int Recontado = 159460000;
        public const int Digitado = 159460001;           // RESERVADO: digitacion al Core (futuro)
        public const int Rebotado = 159460002;           // RESERVADO: digitacion al Core (futuro)
        public const int EnviadoADigitacion = 159460003;
        public const int Recibido = 159460004;
        public const int NoRecibido = 159460005;

        /// <summary>
        /// Un cheque nace sin estado (Recounting lo marca Recontado despues) o, a lo
        /// sumo, ya Recontado. Nacer en medio del handshake o en un estado reservado
        /// se bloquea.
        /// </summary>
        public static TransactionGuardResult EvaluateCheckCreate(int? bornStatus)
        {
            if (bornStatus == null || bornStatus == Recontado)
            {
                return TransactionGuardResult.Allow();
            }

            return TransactionGuardResult.Block(
                "Un cheque solo puede crearse sin estado o en 'Recontado'; el resto del " +
                "ciclo se recorre con el handshake de recepcion.");
        }

        /// <summary>
        /// Update de cheque. Recibido es TERMINAL y CONGELA el registro: cualquier
        /// edicion sobre un cheque Recibido se bloquea, toque o no el estado. Si el
        /// request escribe el estado, las unicas transiciones legales son las del
        /// handshake (mas el no-op idempotente del recuento); 'Digitado' y 'Rebotado'
        /// estan reservados para la etapa futura. Sin estado en el target, la edicion
        /// de datos pasa.
        /// </summary>
        /// <param name="previousStatus">Estado previo del cheque (null si no tiene).</param>
        /// <param name="statusInTarget">true si el request escribe sanic_checkstatus.</param>
        /// <param name="newStatus">Estado destino cuando <paramref name="statusInTarget"/>.</param>
        /// <param name="touchesOnlyMirror">
        /// true si el request escribe SOLO el espejo sanic_bag (CheckMirrorStampPlugin
        /// o backfill). Es metadata derivada, no una edicion de negocio: pasa aun con
        /// el cheque congelado — mismo racional que touchesOnlyTotals del lote TB.
        /// </param>
        public static TransactionGuardResult EvaluateCheckUpdate(
            int? previousStatus,
            bool statusInTarget,
            int? newStatus,
            bool touchesOnlyMirror = false)
        {
            if (touchesOnlyMirror)
            {
                return TransactionGuardResult.Allow();
            }

            if (previousStatus == Recibido)
            {
                return TransactionGuardResult.Block(
                    "El cheque ya esta Recibido por el digitador; ese estado es terminal y " +
                    "congela el registro: no admite mas ediciones.");
            }

            if (!statusInTarget)
            {
                // Edicion de datos sin tocar el estado: no es asunto del handshake.
                return TransactionGuardResult.Allow();
            }

            var isLegal =
                (previousStatus == null && newStatus == Recontado)
                || (previousStatus == Recontado && newStatus == Recontado)
                || (previousStatus == Recontado && newStatus == EnviadoADigitacion)
                || (previousStatus == EnviadoADigitacion && newStatus == Recibido)
                || (previousStatus == EnviadoADigitacion && newStatus == NoRecibido)
                || (previousStatus == NoRecibido && newStatus == EnviadoADigitacion);

            if (isLegal)
            {
                return TransactionGuardResult.Allow();
            }

            if (newStatus == Digitado || newStatus == Rebotado
                || previousStatus == Digitado || previousStatus == Rebotado)
            {
                return TransactionGuardResult.Block(
                    "'Digitado' y 'Rebotado' estan en estado reservado para la etapa futura de " +
                    "digitacion al Core; hoy ninguna transicion los usa.");
            }

            return TransactionGuardResult.Block(
                "Transicion de estado de cheque no permitida. El handshake es: Recontado -> " +
                "'Enviado a digitacion' -> Recibido o 'No Recibido' (y el reenvio " +
                "'No Recibido' -> 'Enviado a digitacion').");
        }
    }
}
