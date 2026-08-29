using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Parsing;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Verifica que la bateria de archivos de prueba (docs/plantillas/pruebas) siga
    /// disparando exactamente las reglas que documenta el plan de pruebas. Si alguien
    /// cambia una regla y la bateria deja de probar lo que dice, esto lo delata.
    /// </summary>
    public class TestBatteryTests
    {
        private static readonly Guid Cliente = Guid.NewGuid();

        public static IEnumerable<object[]> Casos => new[]
        {
            new object[] { "01-OK-completo", Array.Empty<string>() },
            new object[] { "02-OK-minimo", Array.Empty<string>() },
            new object[] { "14-OK-volumen", Array.Empty<string>() },
            new object[] { "03-ERR-cuenta-ajena", new[] { "VR-23" } },
            new object[] { "04-ERR-sin-cif", new[] { "VR-24" } },
            new object[] { "05-ERR-cif-desconocido", new[] { "VR-25" } },
            new object[] { "06-ERR-sin-bolsa-ni-cuenta", new[] { "VR-10", "VR-11" } },
            new object[] { "07-ERR-montos-invalidos", new[] { "VR-20" } },
            new object[] { "08-ERR-tipos-y-moneda", new[] { "VR-17", "VR-18" } },
            new object[] { "09-ERR-cuenta-dos-monedas", new[] { "VR-13" } },
            new object[] { "10-ERR-cheques", new[] { "VR-14" } },
            new object[] { "11-ERR-cheque-incompleto", new[] { "VR-15" } },
            new object[] { "12-ERR-sin-depositos", new[] { "VR-09" } },
            new object[] { "13-ERR-multiple", new[] { "VR-10", "VR-13", "VR-20", "VR-22", "VR-23" } },
            new object[] { "15-ERR-desglose-cheques", new[] { "VR-28" } },
        };

        [Theory]
        [MemberData(nameof(Casos))]
        public void El_archivo_dispara_exactamente_las_reglas_documentadas(string archivo, string[] esperadas)
        {
            var path = Path.Combine(RepoRoot(), "docs", "plantillas", "pruebas", archivo + ".xlsx");
            Assert.True(File.Exists(path), $"falta el archivo de prueba {archivo}.xlsx (regenerar con scripts/build-test-workbooks.py)");

            var context = new ValidationContext(new EmailEnvelope
            {
                SenderEmail = "tesoreria@elporvenir.com.ni",
                Subject = "Pre-registro prueba",
                AttachmentCount = 1,
                AttachmentFileName = archivo + ".xlsx",
                AttachmentSizeKb = new FileInfo(path).Length / 1024
            }, new BatteryData(Cliente));

            try { context.Workbook = XlsxParser.Parse(File.ReadAllBytes(path)); }
            catch (XlsxParseException ex) { context.WorkbookError = ex.Message; }

            var result = new ValidationEngine(FullRuleSet.All).Run(Catalogo(), context);
            var fallidas = result.Executions
                .Where(e => e.Outcome == RuleOutcome.Fail)
                .Select(e => e.Definition.Key)
                .OrderBy(k => k)
                .ToArray();

            Assert.Equal(esperadas.OrderBy(k => k).ToArray(), fallidas);
            Assert.Equal(esperadas.Length == 0, result.Passed);
        }

        [Fact]
        public void Ningun_archivo_de_la_bateria_deja_reglas_sin_estrategia()
        {
            var path = Path.Combine(RepoRoot(), "docs", "plantillas", "pruebas", "01-OK-completo.xlsx");
            var context = new ValidationContext(new EmailEnvelope
            {
                SenderEmail = "tesoreria@elporvenir.com.ni", Subject = "Pre-registro",
                AttachmentCount = 1, AttachmentFileName = "d.xlsx", AttachmentSizeKb = 20
            }, new BatteryData(Cliente))
            { Workbook = XlsxParser.Parse(File.ReadAllBytes(path)) };

            var result = new ValidationEngine(FullRuleSet.All).Run(Catalogo(), context);
            var huerfanas = result.Executions
                .Where(e => e.Outcome == RuleOutcome.NotImplemented)
                .Select(e => e.Definition.Key);
            Assert.Empty(huerfanas);
        }

        /// <summary>Replica de data/seed/50-validationrules.json (orden, parametros y dependencias).</summary>
        private static List<RuleDefinition> Catalogo() => new()
        {
            new("VR-01", "Remitente presente", 10, true, null, "Sin remitente valido.", null),
            new("VR-02", "Cantidad de adjuntos", 20, true, "1", "Debe traer {esperado} adjunto y trae {valor}.", null),
            new("VR-03", "Remitente registrado", 30, true, null, "El correo {valor} no esta registrado.", "VR-01"),
            new("VR-04", "Prefijo del asunto", 40, true, "Pre-registro", "El asunto debe comenzar con '{esperado}'.", null),
            new("VR-05", "Adjunto xlsx", 50, true, ".xlsx", "El adjunto debe ser {esperado}.", "VR-02"),
            new("VR-06", "Tamano maximo", 60, true, "5120", "Pesa {valor} KB; maximo {esperado}.", "VR-05"),
            new("VR-27", "Plantilla oficial", 65, true,
                "{\"templateId\":\"CH-TPL-Depositos\",\"minVersion\":\"1.1.0\"}", "Plantilla no oficial.", "VR-05"),
            new("VR-07", "Dos tablas", 70, true, "2", "Debe contener {esperado} tablas; hay {valor}.", "VR-05"),
            new("VR-08", "Tablas nombradas", 80, true, "[\"Depositos\",\"Cheques\"]", "Falta la tabla '{valor}'.", "VR-07"),
            new("VR-19", "Columnas Depositos", 82, true,
                "[\"No. deposito\",\"No. bolsa\",\"No. Cuenta\",\"Moneda\",\"Cheques Locales BAC\",\"Cheques Locales Otros\",\"Efectivo\",\"Total\",\"Referencia\"]",
                "Falta la columna '{valor}'.", "VR-08"),
            new("VR-16", "Columnas Cheques", 84, true,
                "[\"No. Deposito\",\"No. Cheque\",\"Tipo\",\"Moneda\",\"Monto\"]", "Falta la columna '{valor}'.", "VR-08"),
            new("VR-09", "Al menos un deposito", 90, true, "1", "No hay depositos declarados.", "VR-19"),
            new("VR-10", "Bolsa obligatoria", 100, true, null, "Fila {fila} ({celda}) sin bolsa.", "VR-09"),
            new("VR-11", "Cuenta obligatoria", 110, true, null, "Fila {fila} ({celda}) sin cuenta.", "VR-09"),
            new("VR-17", "Numericos validos", 120, true, null, "La celda {celda} debe ser numerica: '{valor}'.", "VR-19"),
            new("VR-20", "Sin negativos", 130, true, null, "La celda {celda} es negativa ({valor}).", "VR-17"),
            new("VR-21", "Total positivo", 140, true, null, "Fila {fila} declara {valor}.", "VR-20"),
            new("VR-18", "Monedas soportadas", 150, true, "[\"NIO\",\"USD\",\"EUR\"]",
                "La celda {celda} declara '{valor}'; soportadas {esperado}.", "VR-19"),
            new("VR-13", "Cuenta con moneda unica", 160, true, null, "La cuenta {valor} tiene dos monedas (fila {fila}).", "VR-18"),
            new("VR-15", "Cheques completos", 170, true, null, "El cheque de la fila {fila}: {celda} vacia.", "VR-16"),
            new("VR-14", "Sin repetidos", 180, true, null, "El cheque {valor} esta repetido (fila {fila}).", "VR-15"),
            new("VR-22", "Sin huerfanos", 190, true, null, "Fila {fila}: el deposito {valor} no existe.", "VR-14"),
            new("VR-12", "Cheques cuadran", 200, true, null, "El deposito {valor} (fila {fila}) no cuadra: {esperado}.", "VR-22"),
            new("VR-28", "Desglose por tipo", 205, true, null, "El deposito {valor} (fila {fila}) cruzado: {esperado}.", "VR-12"),
            new("VR-24", "CIF declarado", 210, true, "Depositos!J2", "La celda {esperado} debe tener el CIF.", "VR-08"),
            new("VR-25", "Cliente registrado", 220, true, null, "El CIF '{valor}' no existe.", "VR-24"),
            new("VR-26", "Correo autorizado", 230, true, null, "{valor} no esta autorizado.", "VR-25"),
            new("VR-23", "Cuentas del cliente", 240, true, null, "La cuenta {valor} (fila {fila}) no es del cliente.", "VR-25"),
        };

        /// <summary>Datos del cliente sintetico Distribuidora El Porvenir.</summary>
        private sealed class BatteryData : IValidationDataAccess
        {
            private readonly Guid _cliente;
            public BatteryData(Guid cliente) => _cliente = cliente;
            public IReadOnlyList<SenderLink> GetSenderLinks(string senderEmail) =>
                new[] { new SenderLink(_cliente, Vigency.Vigente) };
            public ClientLookup FindClientByCif(string cif) =>
                cif == "0012024001" ? new ClientLookup(_cliente, Vigency.Vigente) : ClientLookup.NotFound;
            public bool AccountBelongsToClient(string accountNumber, Guid clientId) =>
                accountNumber == "100200111" || accountNumber == "100200112";
        }

        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "docs", "plantillas", "pruebas")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            return dir ?? throw new InvalidOperationException(
                "No se encontro docs/plantillas/pruebas subiendo desde " + AppContext.BaseDirectory);
        }
    }
}
