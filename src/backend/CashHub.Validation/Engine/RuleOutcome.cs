namespace CashHub.Validation.Engine
{
    public enum RuleOutcome
    {
        /// <summary>Ejecutada sin hallazgos.</summary>
        Pass,

        /// <summary>Ejecutada con uno o más hallazgos.</summary>
        Fail,

        /// <summary>No ejecutada porque su dependencia no pasó.</summary>
        Skipped,

        /// <summary>Activa en la tabla pero sin estrategia registrada en el motor.</summary>
        NotImplemented,

        /// <summary>La estrategia reventó; el motor la aísla y sigue con el resto.</summary>
        Error
    }
}
