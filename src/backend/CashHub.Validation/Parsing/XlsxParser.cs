using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using CashHub.Validation.Models;

namespace CashHub.Validation.Parsing
{
    /// <summary>El archivo no es un xlsx legible. El mensaje SIEMPRE dice qué parte falló.</summary>
    public sealed class XlsxParseException : Exception
    {
        public XlsxParseException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Lector mínimo de xlsx (OpenXML crudo vía ZipArchive) apto para el sandbox de
    /// Dataverse: sin dependencias externas, todo en memoria. Lee tablas nombradas
    /// (ListObjects), la hoja Metadata como pares clave→valor y celdas sueltas.
    /// </summary>
    public static class XlsxParser
    {
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// Tope del tamaño DESCOMPRIMIDO del paquete. Una plantilla de depositos llena, con
        /// 200 filas y 50 cheques, pesa unos cientos de KB descomprimida; 100 MB es varios
        /// ordenes de magnitud mas y aun asi frena una bomba de descompresion (un .xlsx de
        /// pocos KB que expande a gigabytes) ANTES de cargar nada en memoria.
        ///
        /// Este es el verdadero limite de tamaño del adjunto. VR-06 mide el tamaño
        /// COMPRIMIDO y corre en la etapa 5, despues del parseo: para una bomba llega tarde
        /// y con el numero equivocado. El corte tiene que estar aca, en la etapa 3.
        /// </summary>
        private const long MaxUncompressedBytes = 100L * 1024 * 1024;

        public static WorkbookModel Parse(byte[] content)
        {
            if (content == null || content.Length == 0)
            {
                throw new XlsxParseException("El adjunto esta vacio (0 bytes): no hay archivo que leer.");
            }

            try
            {
                using (var stream = new MemoryStream(content))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    GuardUncompressedSize(zip);
                    var shared = ReadSharedStrings(zip);
                    var sheets = ReadSheetCatalog(zip);          // nombre -> ruta del xml de la hoja
                    var cellCache = new Dictionary<string, Dictionary<string, (string val, int row, bool formula)>>(StringComparer.OrdinalIgnoreCase);

                    Dictionary<string, (string val, int row, bool formula)> SheetCells(string sheetName)
                    {
                        if (!cellCache.TryGetValue(sheetName, out var cells))
                        {
                            if (!sheets.TryGetValue(sheetName, out var path))
                            {
                                throw new XlsxParseException(
                                    $"El libro no contiene la hoja '{sheetName}'. Hojas presentes: {string.Join(", ", sheets.Keys)}.");
                            }
                            cells = ReadCells(zip, path, shared);
                            cellCache[sheetName] = cells;
                        }
                        return cells;
                    }

                    var tables = ReadTables(zip, sheets, SheetCells);
                    var metadata = ReadMetadata(sheets, SheetCells);

                    return new WorkbookModel(tables, metadata, (sheet, cell) =>
                        sheets.ContainsKey(sheet) && SheetCells(sheet).TryGetValue(cell, out var v) ? v.val : null);
                }
            }
            catch (XlsxParseException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                throw new XlsxParseException(
                    "El archivo no es un xlsx valido: no se pudo abrir como paquete ZIP (¿esta corrupto o es otro formato renombrado?).", ex);
            }
            catch (Exception ex)
            {
                throw new XlsxParseException(
                    $"Fallo inesperado leyendo el xlsx en la etapa de parseo: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            var doc = Load(entry, "xl/sharedStrings.xml");
            return doc.Root!.Elements(M + "si")
                .Select(si => string.Concat(si.Descendants(M + "t").Select(t => (string)t)))
                .ToList();
        }

        private static Dictionary<string, string> ReadSheetCatalog(ZipArchive zip)
        {
            var wbEntry = zip.GetEntry("xl/workbook.xml")
                ?? throw new XlsxParseException("El paquete no contiene xl/workbook.xml: no es un libro de Excel.");
            var wb = Load(wbEntry, "xl/workbook.xml");

            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels")
                ?? throw new XlsxParseException("El paquete no contiene las relaciones del libro (xl/_rels/workbook.xml.rels).");
            var rels = Load(relsEntry, "workbook.xml.rels").Root!
                .Elements(Rel + "Relationship")
                .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in wb.Root!.Element(M + "sheets")!.Elements(M + "sheet"))
            {
                var rid = (string)sheet.Attribute(R + "id")!;
                var target = rels[rid];
                result[(string)sheet.Attribute("name")!] =
                    target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
            }
            return result;
        }

        private static Dictionary<string, (string val, int row, bool formula)> ReadCells(
            ZipArchive zip, string sheetPath, List<string> shared)
        {
            var entry = zip.GetEntry(sheetPath)
                ?? throw new XlsxParseException($"La hoja declarada en el libro no existe en el paquete: {sheetPath}.");
            var doc = Load(entry, sheetPath);
            var cells = new Dictionary<string, (string, int, bool)>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in doc.Descendants(M + "c"))
            {
                var cellRef = (string?)c.Attribute("r");
                if (cellRef == null) continue;

                string? value = null;
                var type = (string?)c.Attribute("t");
                if (type == "inlineStr")
                {
                    value = string.Concat(c.Descendants(M + "t").Select(t => (string)t));
                }
                else
                {
                    var v = c.Element(M + "v");
                    if (v == null) continue;
                    value = type == "s" ? shared[int.Parse((string)v)] : (string)v;
                }

                if (value != null)
                {
                    var isFormula = c.Element(M + "f") != null;
                    cells[cellRef] = (value, RowOf(cellRef), isFormula);
                }
            }
            return cells;
        }

        private static List<TableModel> ReadTables(ZipArchive zip, Dictionary<string, string> sheets,
            Func<string, Dictionary<string, (string val, int row, bool formula)>> sheetCells)
        {
            var tables = new List<TableModel>();
            var sheetByPath = sheets.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in zip.Entries.Where(e =>
                e.FullName.StartsWith("xl/tables/", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                var doc = Load(entry, entry.FullName);
                var root = doc.Root!;
                var name = (string?)root.Attribute("displayName") ?? (string?)root.Attribute("name")
                    ?? throw new XlsxParseException($"La tabla en {entry.FullName} no tiene nombre.");
                var reference = (string?)root.Attribute("ref")
                    ?? throw new XlsxParseException($"La tabla '{name}' no declara su rango (atributo ref).");

                var sheetName = FindSheetOfTable(zip, entry.FullName, sheetByPath)
                    ?? throw new XlsxParseException($"No se pudo resolver a que hoja pertenece la tabla '{name}'.");

                var columns = root.Element(M + "tableColumns")!
                    .Elements(M + "tableColumn")
                    .Select(c => (string)c.Attribute("name")!)
                    .ToList();

                tables.Add(Materialize(name, sheetName, reference, columns, sheetCells(sheetName)));
            }
            return tables;
        }

        private static string? FindSheetOfTable(ZipArchive zip, string tablePath,
            Dictionary<string, string> sheetByPath)
        {
            var tableFile = tablePath.Substring(tablePath.LastIndexOf('/') + 1);
            foreach (var rels in zip.Entries.Where(e =>
                e.FullName.StartsWith("xl/worksheets/_rels/", StringComparison.OrdinalIgnoreCase)))
            {
                var doc = Load(rels, rels.FullName);
                var found = doc.Root!.Elements(Rel + "Relationship")
                    .Any(x => ((string)x.Attribute("Target")!).EndsWith(tableFile, StringComparison.OrdinalIgnoreCase));
                if (found)
                {
                    // xl/worksheets/_rels/sheet1.xml.rels -> xl/worksheets/sheet1.xml
                    var sheetPath = rels.FullName.Replace("/_rels", string.Empty)
                        .Substring(0, rels.FullName.Replace("/_rels", string.Empty).Length - ".rels".Length);
                    return sheetByPath.TryGetValue(sheetPath, out var sheetName) ? sheetName : null;
                }
            }
            return null;
        }

        private static TableModel Materialize(string name, string sheet, string reference,
            List<string> columns, Dictionary<string, (string val, int row, bool formula)> cells)
        {
            var parts = reference.Split(':');
            var startCol = ColumnOf(parts[0]);
            var startRow = RowOf(parts[0]);
            var endRow = RowOf(parts[1]);

            var rows = new List<TableRow>();
            for (var row = startRow + 1; row <= endRow; row++) // +1: salta la fila de encabezados
            {
                var rowCells = new Dictionary<string, CellValue>();
                for (var i = 0; i < columns.Count; i++)
                {
                    var cellRef = ColumnName(startCol + i) + row;
                    cells.TryGetValue(cellRef, out var found);
                    rowCells[columns[i]] = new CellValue(found.val, cellRef, row, sheet, found.formula);
                }
                var tableRow = new TableRow(row, rowCells);
                if (!tableRow.IsBlank)
                {
                    rows.Add(tableRow);
                }
            }
            return new TableModel(name, sheet, columns, rows);
        }

        private static Dictionary<string, string> ReadMetadata(Dictionary<string, string> sheets,
            Func<string, Dictionary<string, (string val, int row, bool formula)>> sheetCells)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!sheets.ContainsKey("Metadata")) return metadata;

            var cells = sheetCells("Metadata");
            foreach (var kv in cells.Where(c => c.Key.StartsWith("A", StringComparison.OrdinalIgnoreCase)))
            {
                var row = kv.Value.row;
                if (cells.TryGetValue("B" + row, out var value))
                {
                    metadata[kv.Value.val] = value.val;
                }
            }
            return metadata;
        }

        /// <summary>
        /// Frena una bomba de descompresion mirando el tamaño DESCOMPRIMIDO declarado en el
        /// directorio central del ZIP (ZipArchiveEntry.Length): leerlo NO descomprime nada,
        /// asi que el corte ocurre antes de cargar un solo byte de contenido en memoria. Un
        /// .xlsx de 2 MB que expande a 1 GB muere aca, no en un OutOfMemory del worker.
        /// </summary>
        private static void GuardUncompressedSize(ZipArchive zip)
        {
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                total += entry.Length;
                if (entry.Length > MaxUncompressedBytes || total > MaxUncompressedBytes)
                {
                    throw new XlsxParseException(
                        $"El adjunto es demasiado grande al descomprimirse ({total / (1024 * 1024)} MB o mas, " +
                        $"tope {MaxUncompressedBytes / (1024 * 1024)} MB). Se rechaza sin procesarlo por seguridad; " +
                        "si es un archivo legitimo, divida la declaracion en varios envios.");
                }
            }
        }

        private static XDocument Load(ZipArchiveEntry entry, string label)
        {
            try
            {
                using (var s = entry.Open())
                {
                    return XDocument.Load(s);
                }
            }
            catch (Exception ex)
            {
                throw new XlsxParseException($"El XML interno '{label}' esta dañado y no se pudo leer.", ex);
            }
        }

        private static int RowOf(string cellRef) =>
            int.Parse(new string(cellRef.Where(char.IsDigit).ToArray()));

        private static int ColumnOf(string cellRef)
        {
            var letters = new string(cellRef.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            return letters.Aggregate(0, (acc, ch) => acc * 26 + (ch - 'A' + 1));
        }

        private static string ColumnName(int index)
        {
            var name = string.Empty;
            while (index > 0)
            {
                index--;
                name = (char)('A' + index % 26) + name;
                index /= 26;
            }
            return name;
        }
    }
}
