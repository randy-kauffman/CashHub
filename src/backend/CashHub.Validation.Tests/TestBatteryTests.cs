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

            // ---- VR-29: el numero de cheque son solo digitos ----
            new object[] { "16-OK-cheque-ceros-izquierda", Array.Empty<string>() },
            new object[] { "17-OK-cheque-espacios-alrededor", Array.Empty<string>() },
            new object[] { "18-ERR-cheque-alfanumerico", new[] { "VR-29" } },
            new object[] { "19-ERR-cheque-con-guion", new[] { "VR-29" } },
            new object[] { "20-ERR-cheque-no-entero", new[] { "VR-29" } },
            new object[] { "21-ERR-cheque-espacio-interno", new[] { "VR-29" } },
            new object[] { "22-ERR-cheque-digitos-no-ascii", new[] { "VR-29" } },

            // ---- Limites de columna. Los que pasan en verde y NO deberian estan
            // marcados HUECO: ninguna regla los cuida y revientan al persistir. ----
            new object[] { "23-LIM-tula-50-exacto", Array.Empty<string>() },
            new object[] { "24-LIM-tula-51-excede", Array.Empty<string>() },      // HUECO: String(50)
            new object[] { "25-LIM-cheque-30-exacto", Array.Empty<string>() },
            new object[] { "26-LIM-cheque-31-excede", Array.Empty<string>() },    // HUECO: String(30)
            new object[] { "27-LIM-cuenta-10-digitos", new[] { "VR-23" } },
            new object[] { "28-LIM-cuenta-cero", new[] { "VR-23" } },
            new object[] { "29-LIM-monto-tope-exacto", Array.Empty<string>() },
            new object[] { "30-LIM-monto-excede", Array.Empty<string>() },        // HUECO: Decimal max 1e11
            new object[] { "31-LIM-decimales-de-mas", Array.Empty<string>() },    // HUECO: precision 2
            new object[] { "32-LIM-suma-bolsa-excede", Array.Empty<string>() },   // HUECO: suma por bolsa
            new object[] { "33-LIM-cif-excede", new[] { "VR-25" } },

            // ---- Valores raros ----
            new object[] { "34-OK-tula-case-y-espacios", Array.Empty<string>() },
            new object[] { "35-RARO-unicode", Array.Empty<string>() },
            new object[] { "36-RARO-invisibles", Array.Empty<string>() },
            new object[] { "37-RARO-inyeccion", Array.Empty<string>() },
            new object[] { "38-RARO-solo-espacios", new[] { "VR-10", "VR-11" } },
            new object[] { "39-RARO-referencia-larga", Array.Empty<string>() },
            new object[] { "40-OK-volumen-tope", Array.Empty<string>() },

            // Los 5x-MTX-* NO se asertan aca: dependen del estado de los vinculos en el
            // ambiente (scripts/seed-sender-matrix.py) y se prueban ENVIANDO de verdad.
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

        /// <summary>
        /// El catalogo de esta bateria es una REPLICA A MANO del seed, y una replica a mano
        /// se desincroniza. Ya paso: VR-29 se sembro en el ambiente y se registro en el
        /// motor, pero nadie la agrego aca, asi que la bateria siguio en verde mientras el
        /// ambiente rechazaba archivos que aca pasaban.
        ///
        /// Este test compara clave por clave contra data/seed/50-validationrules.json.
        /// </summary>
        [Fact]
        public void El_catalogo_de_la_bateria_no_puede_divergir_del_seed()
        {
            var seedPath = Path.Combine(RepoRoot(), "data", "seed", "50-validationrules.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(seedPath));

            var enElSeed = doc.RootElement.GetProperty("rows").EnumerateArray()
                .Where(r => r.GetProperty("sanic_isactive").GetBoolean())
                .Select(r => r.GetProperty("sanic_validationname").GetString()!)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            var enLaBateria = Catalogo()
                .Where(r => r.IsActive)
                .Select(r => r.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(enElSeed, enLaBateria);
        }

        /// <summary>
        /// Toda regla activa del seed tiene que tener estrategia en el motor. Sin esto, el
        /// motor la marca NotImplemented -> HasSystemIssues -> TODOS los correos van a
        /// Revision con error. Es la trampa del orden de despliegue: sembrar una regla
        /// antes de subir el assembly rompe la ingesta entera.
        /// </summary>
        [Fact]
        public void Toda_regla_activa_del_seed_tiene_estrategia_en_el_motor()
        {
            var seedPath = Path.Combine(RepoRoot(), "data", "seed", "50-validationrules.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(seedPath));

            var conEstrategia = FullRuleSet.All.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
            var sinEstrategia = doc.RootElement.GetProperty("rows").EnumerateArray()
                .Where(r => r.GetProperty("sanic_isactive").GetBoolean())
                .Select(r => r.GetProperty("sanic_validationname").GetString()!)
                .Where(k => !conEstrategia.Contains(k))
                .ToArray();

            Assert.Empty(sinEstrategia);
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
            new("VR-29", "Numero de cheque numerico", 175, true, null, "El numero de cheque '{valor}' (fila {fila}) no es numerico.", "VR-15"),
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
                cif == "00120240014" ? new ClientLookup(_cliente, Vigency.Vigente) : ClientLookup.NotFound;
            public bool AccountBelongsToClient(string accountNumber, Guid clientId) =>
                accountNumber == "100200111" || accountNumber == "100200112";
        }

        internal static string RepoRootPublic() => RepoRoot();
        internal static List<RuleDefinition> CatalogoPublic() => Catalogo();

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
