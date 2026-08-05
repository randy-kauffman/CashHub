namespace CashHub.Validation.Models
{
    /// <summary>
    /// Un hallazgo de validación: se persiste como una fila de sanic_CHTblValidationResults.
    /// Una regla puede producir muchos hallazgos (uno por celda con problema).
    /// </summary>
    public sealed class Finding
    {
        public Finding(string ruleKey, string? sheet = null, string? cellRef = null,
            int? rowNumber = null, string? actualValue = null, string? expectedValue = null)
        {
            RuleKey = ruleKey;
            Sheet = sheet;
            CellRef = cellRef;
            RowNumber = rowNumber;
            ActualValue = actualValue;
            ExpectedValue = expectedValue;
        }

        public string RuleKey { get; }
        public string? Sheet { get; }
        public string? CellRef { get; }
        public int? RowNumber { get; }

        /// <summary>Rellena {valor} en el mensaje.</summary>
        public string? ActualValue { get; }

        /// <summary>Rellena {esperado} en el mensaje.</summary>
        public string? ExpectedValue { get; }

        /// <summary>Mensaje final renderizado (lo escribe el motor, no la regla).</summary>
        public string? RenderedMessage { get; internal set; }
    }
}
