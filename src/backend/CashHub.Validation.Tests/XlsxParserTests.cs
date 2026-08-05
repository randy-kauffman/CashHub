using System;
using System.IO;
using System.Linq;
using CashHub.Validation.Parsing;
using Xunit;

namespace CashHub.Validation.Tests
{
    public class XlsxParserTests
    {
        /// <summary>La prueba más honesta posible: parsear la plantilla REAL del repo.</summary>
        [Fact]
        public void Parsea_la_plantilla_oficial_v110()
        {
            var workbook = XlsxParser.Parse(File.ReadAllBytes(TemplatePath()));

            Assert.Equal(2, workbook.Tables.Count);
            var deposits = workbook.Table("Depositos")!;
            Assert.Equal("Depositos", deposits.Sheet);
            Assert.Contains("No. Cuenta", deposits.Columns);
            Assert.Contains("Referencia", deposits.Columns);

            var checks = workbook.Table("Cheques")!;
            Assert.Contains("No. Cheque", checks.Columns);

            Assert.Equal("CH-TPL-Depositos", workbook.Metadata["TemplateId"]);
            Assert.Equal("1.1.0", workbook.Metadata["TemplateVersion"]);

            // La plantilla trae filas de ejemplo: deben leerse con su ubicación real.
            var primera = deposits.Rows.First();
            Assert.Equal("Depositos", primera[TemplateColumns.AccountNo]!.Sheet);
            Assert.StartsWith("K", primera[TemplateColumns.AccountNo]!.CellRef);
        }

        [Fact]
        public void Celda_del_cif_se_lee_por_referencia()
        {
            var workbook = XlsxParser.Parse(File.ReadAllBytes(TemplatePath()));
            // En la plantilla distribuida J2 va vacía (la llena el cliente).
            Assert.Null(workbook.ReadCell("Depositos!J2"));
            Assert.Equal("CIF del Cliente:", workbook.ReadCell("Depositos!I2"));
        }

        [Fact]
        public void Archivo_corrupto_da_error_explicito()
        {
            var ex = Assert.Throws<XlsxParseException>(() =>
                XlsxParser.Parse(new byte[] { 1, 2, 3, 4, 5 }));
            Assert.Contains("no es un xlsx valido", ex.Message);
            Assert.Contains("ZIP", ex.Message);
        }

        [Fact]
        public void Adjunto_vacio_da_error_explicito()
        {
            var ex = Assert.Throws<XlsxParseException>(() => XlsxParser.Parse(Array.Empty<byte>()));
            Assert.Contains("vacio", ex.Message);
        }

        private static class TemplateColumns
        {
            public const string AccountNo = "No. Cuenta";
        }

        private static string TemplatePath()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "docs", "plantillas", "CH-TPL-Depositos-v1.1.0.xlsx")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            if (dir == null)
            {
                throw new InvalidOperationException(
                    "No se encontró docs/plantillas/CH-TPL-Depositos-v1.1.0.xlsx subiendo desde " + AppContext.BaseDirectory);
            }
            return Path.Combine(dir, "docs", "plantillas", "CH-TPL-Depositos-v1.1.0.xlsx");
        }
    }
}
