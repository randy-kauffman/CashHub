using CashHub.Validation.Models;

namespace CashHub.Validation.Engine
{
    /// <summary>
    /// Rellena los placeholders del mensaje redactado por negocio:
    /// {hoja} {celda} {fila} {valor} {esperado}.
    /// </summary>
    public static class MessageRenderer
    {
        public static string Render(string? template, Finding finding)
        {
            var text = string.IsNullOrWhiteSpace(template)
                ? $"La validacion {finding.RuleKey} no paso."
                : template!;

            return text
                .Replace("{hoja}", finding.Sheet ?? "-")
                .Replace("{celda}", finding.CellRef ?? "-")
                .Replace("{fila}", finding.RowNumber?.ToString() ?? "-")
                .Replace("{valor}", finding.ActualValue ?? "-")
                .Replace("{esperado}", finding.ExpectedValue ?? "-");
        }
    }
}
