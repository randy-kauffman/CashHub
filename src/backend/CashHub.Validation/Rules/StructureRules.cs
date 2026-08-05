using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;

namespace CashHub.Validation.Rules
{
    internal static class RuleJson
    {
        /// <summary>Extrae una lista JSON simple ["a","b"] sin dependencias externas.</summary>
        public static List<string> StringList(string? json) =>
            (json ?? string.Empty).Trim().TrimStart('[').TrimEnd(']')
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"'))
                .Where(s => s.Length > 0)
                .ToList();

        /// <summary>Extrae el valor string de una clave en un objeto JSON plano.</summary>
        public static string? Value(string? json, string key)
        {
            if (json == null) return null;
            var marker = $"\"{key}\"";
            var i = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            var colon = json.IndexOf(':', i + marker.Length);
            if (colon < 0) return null;
            var rest = json.Substring(colon + 1).TrimStart();
            if (rest.StartsWith("\""))
            {
                var end = rest.IndexOf('"', 1);
                return end > 0 ? rest.Substring(1, end - 1) : null;
            }
            return new string(rest.TakeWhile(c => c != ',' && c != '}').ToArray()).Trim();
        }
    }

    /// <summary>VR-07: el archivo es un Excel legible con la cantidad esperada de tablas.
    /// Es LA compuerta del contenido: si no abre, todo lo demás se omite en cadena.</summary>
    public sealed class Vr07WorkbookHasTables : IValidationRule
    {
        public string Key => "VR-07";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            if (context.Workbook == null)
            {
                return new[] { new Finding(Key,
                    actualValue: context.WorkbookError ?? "el archivo no se pudo leer") };
            }
            var expected = int.Parse(definition.Value ?? "2");
            var actual = context.Workbook.Tables.Count;
            if (actual != expected)
            {
                return new[] { new Finding(Key,
                    actualValue: actual.ToString(), expectedValue: expected.ToString()) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>VR-08: las tablas se llaman como exige la plantilla (parámetro JSON).</summary>
    public sealed class Vr08TablesAreNamed : IValidationRule
    {
        public string Key => "VR-08";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var expected = RuleJson.StringList(definition.Value);
            var findings = new List<Finding>();
            foreach (var name in expected)
            {
                if (context.Workbook!.Table(name) == null)
                {
                    findings.Add(new Finding(Key, actualValue: name,
                        expectedValue: string.Join(", ", expected)));
                }
            }
            return findings;
        }
    }

    /// <summary>Base común de VR-19 (Depositos) y VR-16 (Cheques): columnas esperadas.</summary>
    public abstract class TableColumnsRule : IValidationRule
    {
        public abstract string Key { get; }
        protected abstract string TableName { get; }

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(TableName)!;
            var expected = RuleJson.StringList(definition.Value);
            return expected
                .Where(col => !table.Columns.Contains(col, StringComparer.OrdinalIgnoreCase))
                .Select(col => new Finding(Key, sheet: table.Sheet, actualValue: col,
                    expectedValue: string.Join(", ", expected)))
                .ToList();
        }
    }

    public sealed class Vr19DepositColumns : TableColumnsRule
    {
        public override string Key => "VR-19";
        protected override string TableName => TemplateContract.DepositsTable;
    }

    public sealed class Vr16CheckColumns : TableColumnsRule
    {
        public override string Key => "VR-16";
        protected override string TableName => TemplateContract.ChecksTable;
    }

    /// <summary>VR-27: guardián de plantilla — TemplateId exacto y versión mínima.</summary>
    public sealed class Vr27OfficialTemplate : IValidationRule
    {
        public string Key => "VR-27";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var workbook = context.Workbook;
            if (workbook == null)
            {
                return new[] { new Finding(Key,
                    actualValue: context.WorkbookError ?? "el archivo no se pudo leer") };
            }

            var expectedId = RuleJson.Value(definition.Value, "templateId") ?? "CH-TPL-Depositos";
            var minVersion = RuleJson.Value(definition.Value, "minVersion") ?? "1.1.0";

            workbook.Metadata.TryGetValue("TemplateId", out var actualId);
            workbook.Metadata.TryGetValue("TemplateVersion", out var actualVersion);

            if (!string.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { new Finding(Key, sheet: "Metadata",
                    actualValue: actualId ?? "(sin TemplateId)", expectedValue: expectedId) };
            }
            if (!Version.TryParse(actualVersion, out var version) ||
                version < Version.Parse(minVersion))
            {
                return new[] { new Finding(Key, sheet: "Metadata",
                    actualValue: actualVersion ?? "(sin TemplateVersion)", expectedValue: $">= {minVersion}") };
            }
            return Array.Empty<Finding>();
        }
    }
}
