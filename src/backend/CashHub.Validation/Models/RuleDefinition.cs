namespace CashHub.Validation.Models
{
    /// <summary>
    /// Fila de sanic_ValidationRules proyectada al dominio. ValidationName es la clave
    /// que mapea a la estrategia C#; negocio controla el resto sin tocar código.
    /// </summary>
    public sealed class RuleDefinition
    {
        public RuleDefinition(string key, string name, int order, bool isActive,
            string? value, string? failMessage, string? dependsOnKey)
        {
            Key = key;
            Name = name;
            Order = order;
            IsActive = isActive;
            Value = value;
            FailMessage = failMessage;
            DependsOnKey = dependsOnKey;
        }

        public string Key { get; }
        public string Name { get; }
        public int Order { get; }
        public bool IsActive { get; }

        /// <summary>Parámetro de la regla (JSON cuando son varios).</summary>
        public string? Value { get; }

        /// <summary>Mensaje redactado por negocio, con placeholders {hoja} {celda} {fila} {valor} {esperado}.</summary>
        public string? FailMessage { get; }

        /// <summary>Si la regla madre no pasó, esta se omite (cadena anti-ruido).</summary>
        public string? DependsOnKey { get; }
    }
}
