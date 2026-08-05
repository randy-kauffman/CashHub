using System.Collections.Generic;
using CashHub.Validation.Models;

namespace CashHub.Validation.Engine
{
    /// <summary>
    /// Estrategia de una regla. Devuelve hallazgos; lista vacía = la regla pasa.
    /// La regla NO renderiza mensajes ni decide dependencias — eso es del motor.
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>Clave estable que mapea a sanic_ValidationName (ej. "VR-01").</summary>
        string Key { get; }

        IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context);
    }
}
