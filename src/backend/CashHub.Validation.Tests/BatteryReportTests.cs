using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Parsing;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Corre TODA la bateria y escribe un reporte con lo que dispara cada archivo.
    /// No es un test de aserciones: es la herramienta que produce la tabla del plan de
    /// pruebas, para que el documento no se escriba de memoria.
    ///
    /// Salida: CASHHUB_BATTERY_REPORT (o el temporal del sistema).
    /// </summary>
    public class BatteryReportTests
    {
        [Fact]
        public void Reporte_de_la_bateria()
        {
            var dir = Path.Combine(TestBatteryTests.RepoRootPublic(), "docs", "plantillas", "pruebas");
            var cliente = Guid.NewGuid();
            var sb = new StringBuilder();
            sb.AppendLine("archivo|passed|fallidas|omitidas|hallazgos");

            foreach (var path in Directory.GetFiles(dir, "*.xlsx").OrderBy(p => p))
            {
                var nombre = Path.GetFileNameWithoutExtension(path);
                var ctx = new ValidationContext(new EmailEnvelope
                {
                    SenderEmail = "tesoreria@elporvenir.com.ni",
                    Subject = "Pre-registro prueba",
                    AttachmentCount = 1,
                    AttachmentFileName = nombre + ".xlsx",
                    AttachmentSizeKb = new FileInfo(path).Length / 1024
                }, new ReportData(cliente));

                try { ctx.Workbook = XlsxParser.Parse(File.ReadAllBytes(path)); }
                catch (XlsxParseException ex) { ctx.WorkbookError = ex.Message; }

                RunResult r;
                try { r = new ValidationEngine(FullRuleSet.All).Run(TestBatteryTests.CatalogoPublic(), ctx); }
                catch (Exception ex) { sb.AppendLine($"{nombre}|EXCEPCION|{ex.GetType().Name}: {ex.Message}||"); continue; }

                var fallidas = r.Executions.Where(e => e.Outcome == RuleOutcome.Fail)
                    .Select(e => e.Definition.Key).OrderBy(k => k);
                var omitidas = r.Executions.Count(e => e.Outcome == RuleOutcome.Skipped);
                var primeros = r.FailFindings.Take(2)
                    .Select(f => (f.RenderedMessage ?? "").Replace("|", "/").Replace("\n", " "));

                sb.AppendLine($"{nombre}|{r.Passed}|{string.Join(",", fallidas)}|{omitidas}|{string.Join(" ;; ", primeros)}");
            }

            var outPath = Environment.GetEnvironmentVariable("CASHHUB_BATTERY_REPORT")
                          ?? Path.Combine(Path.GetTempPath(), "battery-report.txt");
            File.WriteAllText(outPath, sb.ToString());
            Assert.True(File.Exists(outPath));
        }

        private sealed class ReportData : IValidationDataAccess
        {
            private readonly Guid _cliente;
            public ReportData(Guid cliente) => _cliente = cliente;

            // El remitente se trata como vinculado y vigente: este reporte mira el
            // ARCHIVO, no la matriz de remitentes (esa se prueba enviando de verdad).
            public IReadOnlyList<SenderLink> GetSenderLinks(string senderEmail) =>
                new[] { new SenderLink(_cliente, Vigency.Vigente) };

            public ClientLookup FindClientByCif(string cif) =>
                cif == "00120240014" ? new ClientLookup(_cliente, Vigency.Vigente) : ClientLookup.NotFound;

            public bool AccountBelongsToClient(string accountNumber, Guid clientId) =>
                accountNumber == "100200111" || accountNumber == "100200112";
        }
    }
}
