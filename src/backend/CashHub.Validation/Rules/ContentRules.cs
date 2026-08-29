using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using static CashHub.Validation.Rules.TemplateContract;

namespace CashHub.Validation.Rules
{
    /// <summary>VR-09: al menos un depósito declarado.</summary>
    public sealed class Vr09AtLeastOneDeposit : IValidationRule
    {
        public string Key => "VR-09";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(DepositsTable)!;
            var minimum = int.Parse(definition.Value ?? "1");
            if (table.Rows.Count < minimum)
            {
                return new[] { new Finding(Key, sheet: table.Sheet,
                    actualValue: table.Rows.Count.ToString(), expectedValue: minimum.ToString()) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>Base de VR-10/VR-11: una columna obligatoria en cada fila de Depositos.</summary>
    public abstract class RequiredDepositColumnRule : IValidationRule
    {
        public abstract string Key { get; }
        protected abstract string Column { get; }

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(DepositsTable)!;
            return table.Rows
                .Where(row => row[Column]?.IsEmpty != false)
                .Select(row => new Finding(Key, sheet: table.Sheet,
                    cellRef: row[Column]?.CellRef, rowNumber: row.RowNumber))
                .ToList();
        }
    }

    public sealed class Vr10DepositHasBag : RequiredDepositColumnRule
    {
        public override string Key => "VR-10";
        protected override string Column => Deposits.BagNo;
    }

    public sealed class Vr11DepositHasAccount : RequiredDepositColumnRule
    {
        public override string Key => "VR-11";
        protected override string Column => Deposits.AccountNo;
    }

    /// <summary>VR-17: las columnas numéricas de ambas tablas contienen números.</summary>
    public sealed class Vr17NumericColumnsAreNumeric : IValidationRule
    {
        public string Key => "VR-17";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var findings = new List<Finding>();
            Check(context.Workbook!.Table(DepositsTable)!, Deposits.NumericColumns, findings);
            Check(context.Workbook!.Table(ChecksTable)!, Checks.NumericColumns, findings);
            return findings;
        }

        private void Check(TableModel table, string[] columns, List<Finding> findings)
        {
            foreach (var row in table.Rows)
            {
                foreach (var column in columns)
                {
                    var cell = row[column];
                    if (cell != null && !cell.IsEmpty && !cell.TryDecimal(out _))
                    {
                        findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: cell.CellRef,
                            rowNumber: row.RowNumber, actualValue: cell.Raw));
                    }
                }
            }
        }
    }

    /// <summary>VR-20: los numéricos no son negativos.</summary>
    public sealed class Vr20NoNegatives : IValidationRule
    {
        public string Key => "VR-20";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var findings = new List<Finding>();
            Check(context.Workbook!.Table(DepositsTable)!, Deposits.NumericColumns, findings);
            Check(context.Workbook!.Table(ChecksTable)!, Checks.NumericColumns, findings);
            return findings;
        }

        private void Check(TableModel table, string[] columns, List<Finding> findings)
        {
            foreach (var row in table.Rows)
            {
                foreach (var column in columns)
                {
                    var cell = row[column];
                    if (cell != null && cell.TryDecimal(out var value) && value < 0)
                    {
                        findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: cell.CellRef,
                            rowNumber: row.RowNumber, actualValue: cell.Raw));
                    }
                }
            }
        }
    }

    /// <summary>VR-21: el Total de cada depósito es mayor a cero.</summary>
    public sealed class Vr21DepositTotalPositive : IValidationRule
    {
        public string Key => "VR-21";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(DepositsTable)!;
            return table.Rows
                .Where(row => row[Deposits.Total]?.TryDecimal(out var total) == true &&
                              ReadDecimal(row[Deposits.Total]) <= 0)
                .Select(row => new Finding(Key, sheet: table.Sheet,
                    cellRef: row[Deposits.Total]!.CellRef, rowNumber: row.RowNumber,
                    actualValue: row[Deposits.Total]!.Raw))
                .ToList();
        }

        private static decimal ReadDecimal(CellValue? cell)
        {
            cell!.TryDecimal(out var value);
            return value;
        }
    }

    /// <summary>VR-18: monedas dentro de la lista soportada (parámetro), en ambas tablas.</summary>
    public sealed class Vr18SupportedCurrencies : IValidationRule
    {
        public string Key => "VR-18";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var allowed = new HashSet<string>(RuleJson.StringList(definition.Value), StringComparer.OrdinalIgnoreCase);
            var findings = new List<Finding>();
            foreach (var (tableName, column) in new[]
                     { (DepositsTable, Deposits.Currency), (ChecksTable, Checks.Currency) })
            {
                var table = context.Workbook!.Table(tableName)!;
                foreach (var row in table.Rows)
                {
                    var cell = row[column];
                    if (cell != null && !cell.IsEmpty && !allowed.Contains(cell.Raw!.Trim()))
                    {
                        findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: cell.CellRef,
                            rowNumber: row.RowNumber, actualValue: cell.Raw,
                            expectedValue: string.Join(", ", allowed)));
                    }
                }
            }
            return findings;
        }
    }

    /// <summary>VR-13: una cuenta no puede aparecer con dos monedas distintas.</summary>
    public sealed class Vr13AccountSingleCurrency : IValidationRule
    {
        public string Key => "VR-13";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(DepositsTable)!;
            var firstCurrency = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var findings = new List<Finding>();

            foreach (var row in table.Rows)
            {
                var account = row[Deposits.AccountNo];
                var currency = row[Deposits.Currency];
                if (account?.IsEmpty != false || currency?.IsEmpty != false) continue;

                if (firstCurrency.TryGetValue(account!.Raw!, out var seen))
                {
                    if (!string.Equals(seen, currency!.Raw, StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new Finding(Key, sheet: table.Sheet,
                            cellRef: currency.CellRef, rowNumber: row.RowNumber,
                            actualValue: account.Raw, expectedValue: seen));
                    }
                }
                else
                {
                    firstCurrency[account.Raw!] = currency!.Raw!;
                }
            }
            return findings;
        }
    }

    /// <summary>VR-15: todos los campos de cada cheque están completos.</summary>
    public sealed class Vr15CheckFieldsComplete : IValidationRule
    {
        public string Key => "VR-15";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(ChecksTable)!;
            var findings = new List<Finding>();
            foreach (var row in table.Rows)
            {
                foreach (var column in table.Columns)
                {
                    var cell = row[column];
                    if (cell == null || cell.IsEmpty)
                    {
                        findings.Add(new Finding(Key, sheet: table.Sheet,
                            cellRef: cell?.CellRef, rowNumber: row.RowNumber, actualValue: column));
                    }
                }
            }
            return findings;
        }
    }

    /// <summary>
    /// VR-29: el número de cheque son SOLO DÍGITOS (depende de VR-15).
    ///
    /// El campo <em>serial number</em> de la línea MICR contiene únicamente dígitos — los
    /// símbolos impresos son delimitadores, no parte del número. Wells Fargo especifica de
    /// 4 a 10 dígitos; el estándar del Reserve Bank of India, 6. Ni letras ni guiones.
    ///
    /// No alcanzaba con agregar "No. Cheque" a <c>Checks.NumericColumns</c> (VR-17): esa
    /// regla usa TryDecimal, que aceptaría 1234.56, -5 y 1e5. Un serial no es una cantidad.
    ///
    /// Valida el valor NORMALIZADO — el mismo que persiste DeclarationMaterializer — para
    /// que no se valide una cosa y se guarde otra.
    /// </summary>
    public sealed class Vr29CheckNumbersAreDigits : IValidationRule
    {
        public string Key => "VR-29";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(ChecksTable)!;
            var findings = new List<Finding>();

            foreach (var row in table.Rows)
            {
                var cell = row[Checks.CheckNo];
                // Las celdas vacías son de VR-15, que además es la dependencia de esta
                // regla: reportar dos veces el mismo problema llena el correo de ruido.
                if (cell == null || cell.IsEmpty) continue;

                if (!DeclaredValueNormalizer.IsValidCheckNumber(cell.Raw))
                {
                    findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: cell.CellRef,
                        rowNumber: row.RowNumber, actualValue: cell.Raw));
                }
            }
            return findings;
        }
    }

    /// <summary>VR-14: sin números de cheque repetidos.</summary>
    public sealed class Vr14NoDuplicateChecks : IValidationRule
    {
        public string Key => "VR-14";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(ChecksTable)!;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var findings = new List<Finding>();
            foreach (var row in table.Rows)
            {
                var check = row[Checks.CheckNo];
                if (check?.IsEmpty != false) continue;
                if (!seen.Add(check!.Raw!))
                {
                    findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: check.CellRef,
                        rowNumber: row.RowNumber, actualValue: check.Raw));
                }
            }
            return findings;
        }
    }

    /// <summary>VR-22: todo cheque referencia un depósito que existe en la tabla Depositos.</summary>
    public sealed class Vr22NoOrphanChecks : IValidationRule
    {
        public string Key => "VR-22";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var deposits = context.Workbook!.Table(DepositsTable)!;
            var checks = context.Workbook!.Table(ChecksTable)!;
            var depositNumbers = new HashSet<string>(
                deposits.Rows.Select(r => r[Deposits.DepositNo]?.Raw?.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))!,
                StringComparer.OrdinalIgnoreCase);

            return checks.Rows
                .Where(row => row[Checks.DepositNo]?.IsEmpty == false &&
                              !depositNumbers.Contains(row[Checks.DepositNo]!.Raw!.Trim()))
                .Select(row => new Finding(Key, sheet: checks.Sheet,
                    cellRef: row[Checks.DepositNo]!.CellRef, rowNumber: row.RowNumber,
                    actualValue: row[Checks.DepositNo]!.Raw))
                .ToList();
        }
    }

    /// <summary>
    /// VR-28: el DESGLOSE por tipo coincide, no solo el total. Un cheque del propio banco
    /// y uno de otro banco tienen tiempos de compensacion distintos: si el cliente los
    /// declara cruzados, el total cuadra pero la expectativa de acreditacion es equivocada.
    /// Depende de VR-12 (si el total ya no cuadra, no se reporta tambien el desglose).
    /// </summary>
    public sealed class Vr28CheckBreakdownMatches : IValidationRule
    {
        public string Key => "VR-28";

        /// <summary>Un tipo cuenta como BAC si su etiqueta menciona al banco; el resto es "otros".</summary>
        private const string BacMarker = "BAC";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var deposits = context.Workbook!.Table(DepositsTable)!;
            var checks = context.Workbook!.Table(ChecksTable)!;

            var byDeposit = checks.Rows
                .Where(r => r[Checks.DepositNo]?.IsEmpty == false)
                .GroupBy(r => r[Checks.DepositNo]!.Raw!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (
                    Bac: SumWhere(g, isBac: true),
                    Otros: SumWhere(g, isBac: false)),
                    StringComparer.OrdinalIgnoreCase);

            var findings = new List<Finding>();
            foreach (var row in deposits.Rows)
            {
                var depositNo = row[Deposits.DepositNo]?.Raw?.Trim();
                if (string.IsNullOrEmpty(depositNo)) continue;

                byDeposit.TryGetValue(depositNo!, out var listed);
                var declaredBac = ReadAmount(row, Deposits.BacChecks);
                var declaredOtros = ReadAmount(row, Deposits.OtherChecks);

                if (declaredBac != listed.Bac)
                {
                    findings.Add(new Finding(Key, sheet: deposits.Sheet,
                        cellRef: row[Deposits.BacChecks]?.CellRef, rowNumber: row.RowNumber,
                        actualValue: depositNo,
                        expectedValue: $"cheques BAC: declarado {declaredBac}, listado {listed.Bac}"));
                }
                if (declaredOtros != listed.Otros)
                {
                    findings.Add(new Finding(Key, sheet: deposits.Sheet,
                        cellRef: row[Deposits.OtherChecks]?.CellRef, rowNumber: row.RowNumber,
                        actualValue: depositNo,
                        expectedValue: $"cheques de otros bancos: declarado {declaredOtros}, listado {listed.Otros}"));
                }
            }
            return findings;
        }

        private static decimal SumWhere(IEnumerable<TableRow> rows, bool isBac) =>
            rows.Where(r => IsBacType(r[Checks.Type]?.Raw) == isBac)
                .Sum(r => { r[Checks.Amount]!.TryDecimal(out var amount); return amount; });

        private static bool IsBacType(string? type) =>
            (type ?? string.Empty).IndexOf(BacMarker, StringComparison.OrdinalIgnoreCase) >= 0;

        private static decimal ReadAmount(TableRow row, string column)
        {
            decimal value = 0;
            row[column]?.TryDecimal(out value);
            return value;
        }
    }

    /// <summary>VR-12: por depósito, el monto declarado en cheques (BAC + Otros) coincide
    /// con la suma de sus cheques en la tabla Cheques.</summary>
    public sealed class Vr12ChecksMatchDeclared : IValidationRule
    {
        public string Key => "VR-12";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var deposits = context.Workbook!.Table(DepositsTable)!;
            var checks = context.Workbook!.Table(ChecksTable)!;

            var checksByDeposit = checks.Rows
                .Where(r => r[Checks.DepositNo]?.IsEmpty == false)
                .GroupBy(r => r[Checks.DepositNo]!.Raw!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                    g => g.Sum(r => { r[Checks.Amount]!.TryDecimal(out var amount); return amount; }),
                    StringComparer.OrdinalIgnoreCase);

            var findings = new List<Finding>();
            foreach (var row in deposits.Rows)
            {
                var depositNo = row[Deposits.DepositNo]?.Raw?.Trim();
                if (string.IsNullOrEmpty(depositNo)) continue;

                decimal declared = 0;
                if (row[Deposits.BacChecks]?.TryDecimal(out var bac) == true) declared += bac;
                if (row[Deposits.OtherChecks]?.TryDecimal(out var other) == true) declared += other;
                checksByDeposit.TryGetValue(depositNo!, out var listed);

                if (declared != listed)
                {
                    findings.Add(new Finding(Key, sheet: deposits.Sheet,
                        cellRef: row[Deposits.BacChecks]?.CellRef, rowNumber: row.RowNumber,
                        actualValue: depositNo,
                        expectedValue: $"declarado {declared} vs cheques {listed}"));
                }
            }
            return findings;
        }
    }
}
