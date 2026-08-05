using System;
using System.Collections.Generic;
using System.Linq;

namespace CashHub.Validation.Models
{
    /// <summary>Una celda leída del xlsx, con su ubicación para reportar hallazgos.</summary>
    public sealed class CellValue
    {
        public CellValue(string? raw, string cellRef, int row, string sheet, bool isFormulaCached = false)
        {
            Raw = raw;
            CellRef = cellRef;
            Row = row;
            Sheet = sheet;
            IsFormulaCached = isFormulaCached;
        }

        /// <summary>El valor no fue tipeado: es el resultado cacheado de una fórmula de la plantilla.</summary>
        public bool IsFormulaCached { get; }

        public string? Raw { get; }
        public string CellRef { get; }
        public int Row { get; }
        public string Sheet { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Raw);

        public bool TryDecimal(out decimal value) =>
            decimal.TryParse(Raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Una fila de una tabla nombrada de Excel: columna → celda.</summary>
    public sealed class TableRow
    {
        public TableRow(int rowNumber, IReadOnlyDictionary<string, CellValue> cells)
        {
            RowNumber = rowNumber;
            Cells = cells;
        }

        public int RowNumber { get; }
        public IReadOnlyDictionary<string, CellValue> Cells { get; }

        public CellValue? this[string column] =>
            Cells.TryGetValue(column, out var cell) ? cell : null;

        /// <summary>Una fila "vacía" (relleno de la plantilla) no cuenta como dato.
        /// Los valores cacheados de fórmulas (los totales pre-armados del rango completo)
        /// no convierten una fila en dato: solo lo tipeado por el cliente cuenta.</summary>
        public bool IsBlank => Cells.Values.All(c => c.IsEmpty || c.IsFormulaCached);
    }

    /// <summary>Una tabla nombrada de Excel (ListObject) ya materializada.</summary>
    public sealed class TableModel
    {
        public TableModel(string name, string sheet, IReadOnlyList<string> columns, IReadOnlyList<TableRow> rows)
        {
            Name = name;
            Sheet = sheet;
            Columns = columns;
            Rows = rows;
        }

        public string Name { get; }
        public string Sheet { get; }
        public IReadOnlyList<string> Columns { get; }

        /// <summary>Solo filas con algún dato (excluye el relleno vacío de la plantilla).</summary>
        public IReadOnlyList<TableRow> Rows { get; }
    }

    /// <summary>El xlsx parseado: tablas nombradas + metadatos + celdas sueltas de interés.</summary>
    public sealed class WorkbookModel
    {
        public WorkbookModel(IReadOnlyList<TableModel> tables,
            IReadOnlyDictionary<string, string> metadata,
            Func<string, string, string?> cellReader)
        {
            Tables = tables;
            Metadata = metadata;
            _cellReader = cellReader;
        }

        private readonly Func<string, string, string?> _cellReader;

        public IReadOnlyList<TableModel> Tables { get; }

        /// <summary>Pares clave→valor de la hoja Metadata (TemplateId, TemplateVersion, ClientCIF).</summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        public TableModel? Table(string name) =>
            Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Lee una celda suelta por referencia "Hoja!Celda" (ej. el CIF en Depositos!J2).</summary>
        public string? ReadCell(string sheetBangCell)
        {
            var parts = sheetBangCell.Split('!');
            return parts.Length == 2 ? _cellReader(parts[0], parts[1]) : null;
        }
    }
}
