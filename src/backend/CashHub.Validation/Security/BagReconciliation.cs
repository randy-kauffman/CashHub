namespace CashHub.Validation.Security
{
    /// <summary>
    /// Conciliacion materializada de la tula (CH-Tbl-Bags) para el tracker:
    /// copia de los totales declarados del DeclaredBag (DeclTotalNIO/USD/EUR) y bit
    /// HasDifference. Existen para que la grilla de tulas filtre y pinte
    /// "con/sin diferencia" con delegacion: el join Bag→DeclaredBag en Power Fx no
    /// delega, y comparar totales en el cliente miente a partir del cap de filas.
    /// </summary>
    public static class BagReconciliation
    {
        // Option set sanic_bagstatus de CH-Tbl-Bags.
        public const int BagStatusRecontada = 159460003;

        public sealed class Result
        {
            public decimal DeclNio { get; set; }
            public decimal DeclUsd { get; set; }
            public decimal DeclEur { get; set; }
            public bool HasDifference { get; set; }
        }

        /// <summary>
        /// true si el Update de la tula toca algo que puede mover la conciliacion:
        /// el vinculo a la declaracion, los totales recontados o el estado.
        /// </summary>
        public static bool ShouldRecompute(
            bool declarationTouched, bool totalsTouched, bool statusTouched)
        {
            return declarationTouched || totalsTouched || statusTouched;
        }

        /// <summary>
        /// Calcula los espejos. HasDifference solo puede ser true con la tula
        /// Recontada Y con declaracion relacionada: antes del recuento no hay nada
        /// que comparar, y sin declaracion la "diferencia" contra cero seria ruido
        /// (ese caso ya viaja como transaccion "Sin declaracion").
        /// </summary>
        public static Result Compute(
            int? bagStatus, bool hasDeclaration,
            decimal? declNio, decimal? declUsd, decimal? declEur,
            decimal? recNio, decimal? recUsd, decimal? recEur)
        {
            var dNio = hasDeclaration ? declNio ?? 0m : 0m;
            var dUsd = hasDeclaration ? declUsd ?? 0m : 0m;
            var dEur = hasDeclaration ? declEur ?? 0m : 0m;

            var difference =
                bagStatus == BagStatusRecontada
                && hasDeclaration
                && ((recNio ?? 0m) != dNio
                    || (recUsd ?? 0m) != dUsd
                    || (recEur ?? 0m) != dEur);

            return new Result
            {
                DeclNio = dNio,
                DeclUsd = dUsd,
                DeclEur = dEur,
                HasDifference = difference
            };
        }
    }
}
