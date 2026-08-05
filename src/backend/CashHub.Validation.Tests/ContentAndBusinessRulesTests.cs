using System;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    public class ContentAndBusinessRulesTests
    {
        private static readonly Guid Cliente1 = Guid.NewGuid();

        private static ValidationContext Ctx(WorkbookModel? workbook, string? workbookError = null)
        {
            var data = new FakeDataAccess();
            data.RegisteredSenders.Add("contador@cliente.com");
            data.ClientsByCif["CIF-001"] = Cliente1;
            data.Authorizations.Add(("contador@cliente.com", Cliente1));
            data.Accounts.Add(("100200300", Cliente1));
            data.Accounts.Add(("100200301", Cliente1));

            return new ValidationContext(new EmailEnvelope
            {
                SenderEmail = "contador@cliente.com",
                Subject = "Pre-registro",
                AttachmentCount = 1,
                AttachmentFileName = "d.xlsx",
                AttachmentSizeKb = 10
            }, data)
            { Workbook = workbook, WorkbookError = workbookError };
        }

        private static RuleDefinition Def(string key, string? value = null) =>
            new(key, key, 1, true, value, null, null);

        [Fact]
        public void Vr07_reporta_el_error_del_parser_cuando_no_abre()
        {
            var findings = new Vr07WorkbookHasTables().Evaluate(Def("VR-07", "2"),
                Ctx(null, "no es un zip"));
            Assert.Equal("no es un zip", findings.Single().ActualValue);
        }

        [Fact]
        public void Vr08_detecta_tabla_faltante()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns)
                .Build();
            var findings = new Vr08TablesAreNamed().Evaluate(
                Def("VR-08", "[\"Depositos\",\"Cheques\"]"), Ctx(wb));
            Assert.Equal("Cheques", findings.Single().ActualValue);
        }

        [Fact]
        public void Vr19_detecta_columna_faltante_en_depositos()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", new[] { "No. deposito", "No. bolsa" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns)
                .Build();
            var findings = new Vr19DepositColumns().Evaluate(
                Def("VR-19", "[\"No. deposito\",\"No. bolsa\",\"Moneda\"]"), Ctx(wb));
            Assert.Equal("Moneda", findings.Single().ActualValue);
        }

        [Fact]
        public void Vr27_rechaza_version_vieja_de_plantilla()
        {
            var wb = Plantilla.Valida().Metadata("TemplateVersion", "1.0.0").Build();
            var findings = new Vr27OfficialTemplate().Evaluate(
                Def("VR-27", "{\"templateId\":\"CH-TPL-Depositos\",\"minVersion\":\"1.1.0\"}"), Ctx(wb));
            Assert.Equal(">= 1.1.0", findings.Single().ExpectedValue);
        }

        [Fact]
        public void Vr10_ubica_la_fila_sin_bolsa()
        {
            var wb = Plantilla.Valida()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100200300", "USD", "0", "0", "10", "10", "r" },
                    new[] { "2", null, "100200300", "USD", "0", "0", "10", "10", "r" })
                .Build();
            // el builder agrega tablas; la última Depositos agregada es la del test
            var findings = new Vr10DepositHasBag().Evaluate(Def("VR-10"),
                Ctx(RebuildWithLast(wb, "Depositos")));
            var f = findings.Single();
            Assert.Equal(7, f.RowNumber);
            Assert.Equal("B7", f.CellRef);
            Assert.Equal("Depositos", f.Sheet);
        }

        [Fact]
        public void Vr17_y_Vr20_ubican_celdas_invalidas()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "abc", "0", "-5", "10", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns)
                .Build();
            var ctx = Ctx(wb);

            var noNumerico = new Vr17NumericColumnsAreNumeric().Evaluate(Def("VR-17"), ctx).Single();
            Assert.Equal("abc", noNumerico.ActualValue);
            Assert.Equal("E6", noNumerico.CellRef);

            var negativo = new Vr20NoNegatives().Evaluate(Def("VR-20"), ctx).Single();
            Assert.Equal("-5", negativo.ActualValue);
            Assert.Equal("G6", negativo.CellRef);
        }

        [Fact]
        public void Vr13_detecta_cuenta_con_dos_monedas()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100200300", "USD", "0", "0", "10", "10", "r" },
                    new[] { "2", "B-1", "100200300", "NIO", "0", "0", "10", "10", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns)
                .Build();
            var finding = new Vr13AccountSingleCurrency().Evaluate(Def("VR-13"), Ctx(wb)).Single();
            Assert.Equal("100200300", finding.ActualValue);
            Assert.Equal("USD", finding.ExpectedValue);
            Assert.Equal(7, finding.RowNumber);
        }

        [Fact]
        public void Vr14_Vr22_Vr12_cruzan_cheques_contra_depositos()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "100", "0", "0", "100", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns,
                    new[] { "1", "CH-1", "Cheque Local BAC", "USD", "60" },
                    new[] { "1", "CH-1", "Cheque Local BAC", "USD", "30" },   // repetido
                    new[] { "9", "CH-2", "Cheque Local BAC", "USD", "10" })   // huérfano
                .Build();
            var ctx = Ctx(wb);

            Assert.Equal("CH-1", new Vr14NoDuplicateChecks().Evaluate(Def("VR-14"), ctx).Single().ActualValue);
            Assert.Equal("9", new Vr22NoOrphanChecks().Evaluate(Def("VR-22"), ctx).Single().ActualValue);
            var vr12 = new Vr12ChecksMatchDeclared().Evaluate(Def("VR-12"), ctx).Single();
            Assert.Equal("1", vr12.ActualValue);
            Assert.Contains("declarado 100", vr12.ExpectedValue);
            Assert.Contains("cheques 90", vr12.ExpectedValue);
        }

        [Fact]
        public void Vr24_a_Vr23_resuelven_la_atribucion_completa()
        {
            var wb = Plantilla.Valida().Build();
            var ctx = Ctx(wb);

            Assert.Empty(new Vr24CifDeclared().Evaluate(Def("VR-24", "Depositos!J2"), ctx));
            Assert.Empty(new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx));
            Assert.Equal(Cliente1, ctx.ResolvedClientId);
            Assert.Empty(new Vr26SenderAuthorizedForClient().Evaluate(Def("VR-26"), ctx));
            Assert.Empty(new Vr23AccountsBelongToClient().Evaluate(Def("VR-23"), ctx));
        }

        [Fact]
        public void Vr25_rechaza_cif_desconocido_y_Vr23_cuenta_ajena()
        {
            var wb = Plantilla.Valida().Cell("Depositos!J2", "CIF-NADIE").Build();
            var ctx = Ctx(wb);
            Assert.Equal("CIF-NADIE",
                new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx).Single().ActualValue);

            var wb2 = Plantilla.Valida().Build();
            var ctx2 = Ctx(wb2);
            new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx2);
            ((FakeDataAccess)ctx2.DataAccess).Accounts.Remove(("100200301", Cliente1));
            var finding = new Vr23AccountsBelongToClient().Evaluate(Def("VR-23"), ctx2).Single();
            Assert.Equal("100200301", finding.ActualValue);
        }


        // ---------- VR-28: desglose de cheques por tipo ----------

        [Fact]
        public void Vr28_pasa_cuando_el_desglose_por_tipo_coincide()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "200", "100", "0", "300", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns,
                    new[] { "1", "CH-1", "Cheque Local BAC", "USD", "200" },
                    new[] { "1", "CH-2", "Otros Local", "USD", "100" })
                .Build();
            Assert.Empty(new Vr28CheckBreakdownMatches().Evaluate(Def("VR-28"), Ctx(wb)));
        }

        [Fact]
        public void Vr28_detecta_el_desglose_cruzado_aunque_el_total_cuadre()
        {
            // Declara todo como BAC, pero el cheque es de otro banco: el TOTAL cuadra (300=300).
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "300", "0", "0", "300", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns,
                    new[] { "1", "CH-1", "Otros Local", "USD", "300" })
                .Build();
            var ctx = Ctx(wb);

            Assert.Empty(new Vr12ChecksMatchDeclared().Evaluate(Def("VR-12"), ctx));  // el total cuadra
            var findings = new Vr28CheckBreakdownMatches().Evaluate(Def("VR-28"), ctx);
            Assert.Equal(2, findings.Count);   // sobra en BAC y falta en otros
            Assert.Contains(findings, f => f.ExpectedValue!.Contains("BAC: declarado 300, listado 0"));
            Assert.Contains(findings, f => f.ExpectedValue!.Contains("otros bancos: declarado 0, listado 300"));
        }

        [Fact]
        public void Vr28_ubica_cada_diferencia_en_su_columna()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "500", "0", "0", "500", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns,
                    new[] { "1", "CH-1", "Cheque Local BAC", "USD", "400" })
                .Build();
            var finding = new Vr28CheckBreakdownMatches().Evaluate(Def("VR-28"), Ctx(wb)).Single();
            Assert.Equal("E6", finding.CellRef);      // columna Cheques Locales BAC
            Assert.Equal("Depositos", finding.Sheet);
            Assert.Equal(6, finding.RowNumber);
        }

        [Fact]
        public void Vr28_reconoce_el_tipo_sin_importar_como_lo_escriban()
        {
            var wb = new WorkbookBuilder()
                .Table("Depositos", "Depositos", Plantilla.DepositColumns,
                    new[] { "1", "B-1", "100", "USD", "100", "0", "0", "100", "r" })
                .Table("Cheques", "Cheques", Plantilla.CheckColumns,
                    new[] { "1", "CH-1", "cheque local bac", "USD", "100" })  // minúsculas
                .Build();
            Assert.Empty(new Vr28CheckBreakdownMatches().Evaluate(Def("VR-28"), Ctx(wb)));
        }

        /// <summary>El builder puede acumular dos tablas homónimas; usa la última para el test.</summary>
        private static WorkbookModel RebuildWithLast(WorkbookModel wb, string tableName)
        {
            var tables = wb.Tables.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last()).ToList();
            return new WorkbookModel(tables, wb.Metadata, (s, c) => wb.ReadCell($"{s}!{c}"));
        }
    }
}
