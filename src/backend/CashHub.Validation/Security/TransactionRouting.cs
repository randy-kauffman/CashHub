namespace CashHub.Validation.Security
{
    /// <summary>
    /// Ruteo de transacciones (CH-Tbl-Transactions) a proceso Batch o Manual, y
    /// elegibilidad para el corte automatico a lotes.
    ///
    /// La regla vivia duplicada en 8 sitios Power Fx (con derivas entre si) y ninguno
    /// aplicaba "sin cuenta -> Manual": una transaccion sin cuenta destino entraba al
    /// corte Batch y quedaba inaplicable. Ademas el corte era un UpdateIf client-side
    /// (cap 2000, no atomico) que no excluia transacciones ya Aplicadas. Este dominio
    /// es la unica fuente de la regla; el plugin la impone en el pipeline y el Custom
    /// API de corte la usa server-side.
    /// </summary>
    public static class TransactionRouting
    {
        // Option set sanic_transactionprocessingtype de CH-Tbl-Transactions.
        public const int ProcessingManual = 159460000;
        public const int ProcessingBatch = 159460001;

        // Option set sanic_transactionstatus. Unico valor definido; pendiente = null.
        public const int StatusAplicada = 159460000;

        // Option set sanic_batchstatus de CH-Tbl-TransactionBatches.
        public const int BatchGenerado = 159460000;

        /// <summary>
        /// Resuelve el tipo de proceso efectivo. Manual explicito SIEMPRE se respeta
        /// (el Supervisor fuerza Manual a proposito en aprobaciones); Batch solo si
        /// las reglas lo permiten: con cheques o sin cuenta destino la transaccion es
        /// Manual aunque pidan Batch (o no pidan nada), y solo el efectivo con cuenta
        /// va a Batch.
        /// </summary>
        public static int ResolveProcessingType(int? requestedType, bool hasChecks, bool hasAccountNumber)
        {
            if (requestedType == ProcessingManual)
            {
                return ProcessingManual;
            }

            if (hasChecks || !hasAccountNumber)
            {
                return ProcessingManual;
            }

            return ProcessingBatch;
        }

        /// <summary>
        /// true si la transaccion trae cheques: cualquiera de los montos de cheque
        /// (propios o del deposito de recuento relacionado) es mayor a cero.
        /// </summary>
        public static bool HasChecks(decimal? txBac, decimal? txOther, decimal? depBac, decimal? depOther)
        {
            return (txBac ?? 0m) > 0m
                || (txOther ?? 0m) > 0m
                || (depBac ?? 0m) > 0m
                || (depOther ?? 0m) > 0m;
        }

        /// <summary>
        /// Tipo de proceso al desasignar una transaccion de su lote (regla F3): un
        /// rechazo del Core la devuelve al circuito Manual — nunca vuelve sola a un
        /// corte Batch automatico, porque el motivo del rechazo exige gestion humana.
        /// </summary>
        public static int ResolveOnUnassign()
        {
            return ProcessingManual;
        }

        /// <summary>
        /// true si la transaccion entra en un corte: proceso Batch, sin lote asignado
        /// y todavia pendiente. Una transaccion Aplicada NUNCA entra a un corte (el
        /// fix del bug de doble aplicacion del UpdateIf client-side).
        /// </summary>
        public static bool IsEligibleForCut(int? processingType, bool hasBatch, int? transactionStatus)
        {
            return processingType == ProcessingBatch
                && !hasBatch
                && transactionStatus == null;
        }
    }
}
