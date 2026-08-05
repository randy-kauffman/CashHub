using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Models;

namespace CashHub.Validation.Tests
{
    internal sealed class FakeDataAccess : IValidationDataAccess
    {
        public HashSet<string> RegisteredSenders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Guid> ClientsByCif { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<(string sender, Guid client)> Authorizations { get; } = new();
        public HashSet<(string account, Guid client)> Accounts { get; } = new();

        public bool IsSenderRegistered(string senderEmail) => RegisteredSenders.Contains(senderEmail);

        public Guid? FindClientIdByCif(string cif) =>
            ClientsByCif.TryGetValue(cif, out var id) ? id : (Guid?)null;

        public bool IsSenderAuthorizedForClient(string senderEmail, Guid clientId) =>
            Authorizations.Contains((senderEmail.ToLowerInvariant(), clientId));

        public bool AccountBelongsToClient(string accountNumber, Guid clientId) =>
            Accounts.Contains((accountNumber, clientId));
    }

    /// <summary>Construye WorkbookModel en memoria para testear reglas sin tocar xlsx.</summary>
    internal sealed class WorkbookBuilder
    {
        private readonly List<TableModel> _tables = new();
        private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _cells = new(StringComparer.OrdinalIgnoreCase);

        public WorkbookBuilder Metadata(string key, string value)
        {
            _metadata[key] = value;
            return this;
        }

        public WorkbookBuilder Cell(string sheetBangCell, string value)
        {
            _cells[sheetBangCell] = value;
            return this;
        }

        public WorkbookBuilder Table(string name, string sheet, string[] columns, params string?[][] rows)
        {
            var tableRows = new List<TableRow>();
            for (var i = 0; i < rows.Length; i++)
            {
                var rowNumber = 6 + i; // como la plantilla real: datos desde la fila 6
                var cells = new Dictionary<string, CellValue>();
                for (var c = 0; c < columns.Length; c++)
                {
                    var cellRef = (char)('A' + c) + rowNumber.ToString();
                    cells[columns[c]] = new CellValue(
                        c < rows[i].Length ? rows[i][c] : null, cellRef, rowNumber, sheet);
                }
                tableRows.Add(new TableRow(rowNumber, cells));
            }
            _tables.Add(new TableModel(name, sheet, columns.ToList(), tableRows));
            return this;
        }

        public WorkbookModel Build() =>
            new(_tables, _metadata,
                (sheet, cell) => _cells.TryGetValue($"{sheet}!{cell}", out var v) ? v : null);
    }

    internal static class Plantilla
    {
        public static readonly string[] DepositColumns =
        {
            "No. deposito", "No. bolsa", "No. Cuenta", "Moneda",
            "Cheques Locales BAC", "Cheques Locales Otros", "Efectivo", "Total", "Referencia"
        };

        public static readonly string[] CheckColumns =
            { "No. Deposito", "No. Cheque", "Tipo", "Moneda", "Monto" };

        /// <summary>Un libro completamente válido según el contrato v1.1.0.</summary>
        public static WorkbookBuilder Valida() => new WorkbookBuilder()
            .Metadata("TemplateId", "CH-TPL-Depositos")
            .Metadata("TemplateVersion", "1.1.0")
            .Cell("Depositos!J2", "CIF-001")
            .Table("Depositos", "Depositos", DepositColumns,
                new[] { "1", "B-100", "100200300", "USD", "150", "0", "50", "200", "Planilla" },
                new[] { "2", "B-100", "100200301", "NIO", "0", "0", "500", "500", "Efectivo" })
            .Table("Cheques", "Cheques", CheckColumns,
                new[] { "1", "CH-9001", "Cheque Local BAC", "USD", "150" });
    }
}
