namespace CashHub.Validation.Security
{
    /// <summary>Resultado de evaluar una operacion del dominio de digitalizacion.</summary>
    public sealed class TransactionGuardResult
    {
        private TransactionGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si la operacion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        public static TransactionGuardResult Allow() => new TransactionGuardResult(true, null);

        public static TransactionGuardResult Block(string reason) => new TransactionGuardResult(false, reason);
    }

    /// <summary>
    /// Maquina de estados del dominio de digitalizacion: lotes de transacciones
    /// (CH-Tbl-TransactionBatches) y transacciones (CH-Tbl-Transactions).
    ///
    /// El ciclo del lote es estrictamente hacia adelante: Generado (el corte) ->
    /// Ingresado (cargado en el Core) -> Aplicado (confirmado; inmutable). Aprobado
    /// existe en el option set pero es un dead-end prohibido por el negocio: no se
    /// entra ni se sale de el. Las apps condicionan botones en Power Fx, pero eso es
    /// cosmetico: cualquier usuario con el rol puede, por Web API o Excel, retroceder
    /// un lote Aplicado, meter transacciones en un lote cerrado, re-aplicar una
    /// transaccion Aplicada o vaciar un lote ya aplicado. Este dominio codifica las
    /// reglas y el plugin (GuardTransactionPlugin) las impone en el pipeline.
    /// </summary>
    public static class TransactionGuard
    {
        // Option set sanic_batchstatus de CH-Tbl-TransactionBatches. Generado es la
        // misma constante que usa el corte (TransactionRouting): unica fuente.
        public const int BatchGenerado = TransactionRouting.BatchGenerado;
        public const int BatchIngresado = 159460001;
        public const int BatchAprobado = 159460002;
        public const int BatchAplicado = 159460003;

        // Option set sanic_transactionstatus. Unico valor definido; pendiente = null.
        public const int StatusAplicada = TransactionRouting.StatusAplicada;

        // ---- Lote (CH-Tbl-TransactionBatches) ----

        /// <summary>
        /// Un lote solo puede nacer Generado, y EXPLICITO: los defaults de columna de
        /// Dataverse no aplican a estos Create, y un lote con estado null queda fuera
        /// de la maquina de estados para siempre.
        /// </summary>
        public static TransactionGuardResult EvaluateBatchCreate(int? bornStatus)
        {
            if (bornStatus == BatchGenerado)
            {
                return TransactionGuardResult.Allow();
            }

            return TransactionGuardResult.Block(
                "Un lote de transacciones solo puede crearse en estado 'Generado' (explicito).");
        }

        /// <summary>
        /// Transiciones del estado del lote, SOLO hacia adelante: Generado -&gt; Ingresado
        /// -&gt; Aplicado. Todo lo demas se bloquea: saltos, retrocesos, el no-op, cualquier
        /// transicion hacia o desde Aprobado (dead-end prohibido) y cualquier escritura
        /// de estado sobre un lote ya Aplicado (inmutable).
        /// </summary>
        /// <param name="previousStatus">Estado previo del lote (null si no tiene).</param>
        /// <param name="statusInTarget">true si el request escribe sanic_batchstatus.</param>
        /// <param name="newStatus">Estado destino cuando <paramref name="statusInTarget"/>.</param>
        /// <param name="touchesOnlyTotals">true si el request solo escribe los totales materializados.</param>
        public static TransactionGuardResult EvaluateBatchUpdate(
            int? previousStatus,
            bool statusInTarget,
            int? newStatus,
            bool touchesOnlyTotals)
        {
            if (touchesOnlyTotals)
            {
                // Recompute de totales materializados (infraestructura, no negocio):
                // pasa siempre, incluso sobre un lote Aplicado.
                return TransactionGuardResult.Allow();
            }

            if (!statusInTarget)
            {
                // Campos comunes sin tocar el estado: no es asunto de esta maquina.
                return TransactionGuardResult.Allow();
            }

            if (previousStatus == BatchAplicado)
            {
                return TransactionGuardResult.Block(
                    "El lote ya esta Aplicado en el Core y es inmutable; su estado no admite cambios.");
            }

            var isLegal =
                (previousStatus == BatchGenerado && newStatus == BatchIngresado)
                || (previousStatus == BatchIngresado && newStatus == BatchAplicado);

            if (isLegal)
            {
                return TransactionGuardResult.Allow();
            }

            return TransactionGuardResult.Block(
                "Transicion de estado de lote no permitida. El ciclo es solo hacia adelante: " +
                "Generado -> Ingresado -> Aplicado (Aprobado no se usa).");
        }

        /// <summary>
        /// Solo se elimina un lote Generado y sin transacciones: el rollback del corte
        /// vacio. Un lote con contenido, o que ya avanzo en el ciclo, no se borra.
        /// </summary>
        public static TransactionGuardResult EvaluateBatchDelete(int? status, bool hasTransactions)
        {
            if (status != BatchGenerado)
            {
                return TransactionGuardResult.Block(
                    "Solo puede eliminarse un lote en estado 'Generado'.");
            }

            if (hasTransactions)
            {
                return TransactionGuardResult.Block(
                    "El lote tiene transacciones asignadas; un lote con contenido no se elimina.");
            }

            return TransactionGuardResult.Allow();
        }

        // ---- Transaccion (CH-Tbl-Transactions) ----

        /// <summary>
        /// Update de transaccion. Reglas:
        /// - El espejo sanic_batchstatus (mirror/cascada) pasa siempre: es infraestructura.
        /// - Aplicada es TERMINAL: su estado no se toca mas. Marcar Aplicada (null ->
        ///   Aplicada) es solo del circuito manual: sin lote y sin asignar uno en el
        ///   mismo request (las transacciones de lote no llevan status propio).
        /// - Asignacion a lote: solo una transaccion pendiente y sin lote, hacia un lote
        ///   Generado (a un Ingresado/Aplicado no se agrega nada). Sin reasignacion directa.
        /// - Desasignacion: solo con el lote actual Generado o Ingresado (los rechazos del
        ///   Core ocurren durante la carga); un lote Aplicado es intocable.
        /// </summary>
        /// <param name="touchesOnlyMirror">true si el request solo escribe sanic_batchstatus (+PK).</param>
        /// <param name="statusInTarget">true si el request escribe sanic_transactionstatus.</param>
        /// <param name="newStatus">Estado destino cuando <paramref name="statusInTarget"/>.</param>
        /// <param name="previousStatus">Estado previo de la transaccion (null = pendiente).</param>
        /// <param name="batchInTarget">true si el request escribe sanic_transactionbatch.</param>
        /// <param name="newBatchIsNull">true si el lookup de lote se escribe en null (desasignacion).</param>
        /// <param name="currentBatchStatus">Estado del lote actual, si la transaccion tenia lote.</param>
        /// <param name="targetBatchStatus">Estado del lote destino, si el request asigna uno.</param>
        /// <param name="hadBatch">true si la transaccion ya tenia lote asignado.</param>
        public static TransactionGuardResult EvaluateTransactionUpdate(
            bool touchesOnlyMirror,
            bool statusInTarget,
            int? newStatus,
            int? previousStatus,
            bool batchInTarget,
            bool newBatchIsNull,
            int? currentBatchStatus,
            int? targetBatchStatus,
            bool hadBatch)
        {
            if (touchesOnlyMirror)
            {
                // Escrituras del mirror plugin / cascada de estado: metadata derivada.
                return TransactionGuardResult.Allow();
            }

            var assignsBatch = batchInTarget && !newBatchIsNull;

            if (statusInTarget)
            {
                if (previousStatus == StatusAplicada)
                {
                    return TransactionGuardResult.Block(
                        "La transaccion ya esta Aplicada; ese estado es terminal y no admite cambios.");
                }

                if (newStatus == StatusAplicada && (hadBatch || assignsBatch))
                {
                    return TransactionGuardResult.Block(
                        "Solo una transaccion del circuito manual (sin lote) se marca Aplicada; " +
                        "las transacciones de lote se aplican con el lote.");
                }
            }

            if (assignsBatch)
            {
                if (hadBatch)
                {
                    return TransactionGuardResult.Block(
                        "La transaccion ya pertenece a un lote; no se reasigna en forma directa " +
                        "(primero desasignar, despues cortar de nuevo).");
                }

                if (targetBatchStatus != BatchGenerado)
                {
                    return TransactionGuardResult.Block(
                        "Una transaccion solo puede asignarse a un lote en estado 'Generado'; " +
                        "a un lote ya ingresado o aplicado no se agrega nada.");
                }

                if (previousStatus != null)
                {
                    return TransactionGuardResult.Block(
                        "Una transaccion Aplicada no puede entrar a un lote (doble aplicacion).");
                }
            }
            else if (batchInTarget && newBatchIsNull && hadBatch)
            {
                var batchIsOpen = currentBatchStatus == BatchGenerado
                                  || currentBatchStatus == BatchIngresado;
                if (!batchIsOpen)
                {
                    return TransactionGuardResult.Block(
                        "La transaccion no puede desasignarse: su lote ya esta Aplicado (o fuera " +
                        "del ciclo) y es intocable. Los rechazos del Core ocurren durante la carga.");
                }
            }

            return TransactionGuardResult.Allow();
        }
    }
}
