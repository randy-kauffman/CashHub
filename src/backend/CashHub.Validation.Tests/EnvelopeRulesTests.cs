using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    public class EnvelopeRulesTests
    {
        private static readonly FakeDataAccess Data = new();

        static EnvelopeRulesTests()
        {
            Data.RegisteredSenders.Add("contador@cliente.com");
        }

        private static ValidationContext Ctx(EmailEnvelope envelope) => new(envelope, Data);

        private static RuleDefinition Def(string key, string? value = null) =>
            new(key, key, 1, true, value, null, null);

        // ---------- VR-01 remitente ----------
        [Theory]
        [InlineData("contador@cliente.com", 0)]
        [InlineData("", 1)]
        [InlineData(null, 1)]
        [InlineData("sin-arroba", 1)]
        public void Vr01_valida_remitente(string? sender, int hallazgosEsperados)
        {
            var findings = new Vr01SenderPresent().Evaluate(Def("VR-01"),
                Ctx(new EmailEnvelope { SenderEmail = sender }));
            Assert.Equal(hallazgosEsperados, findings.Count);
        }

        // ---------- VR-02 cantidad de adjuntos ----------
        [Theory]
        [InlineData(1, 0)]
        [InlineData(0, 1)]
        [InlineData(3, 1)]
        public void Vr02_exige_cantidad_exacta(int adjuntos, int hallazgosEsperados)
        {
            var findings = new Vr02AttachmentCount().Evaluate(Def("VR-02", "1"),
                Ctx(new EmailEnvelope { AttachmentCount = adjuntos }));
            Assert.Equal(hallazgosEsperados, findings.Count);
            if (hallazgosEsperados > 0)
            {
                Assert.Equal("1", findings[0].ExpectedValue);
            }
        }

        // ---------- VR-03 remitente registrado ----------
        [Theory]
        [InlineData("contador@cliente.com", 0)]
        [InlineData("CONTADOR@CLIENTE.COM", 0)]
        [InlineData("otro@nadie.com", 1)]
        public void Vr03_verifica_registro(string sender, int hallazgosEsperados)
        {
            var findings = new Vr03SenderRegistered().Evaluate(Def("VR-03"),
                Ctx(new EmailEnvelope { SenderEmail = sender }));
            Assert.Equal(hallazgosEsperados, findings.Count);
        }

        // ---------- VR-04 prefijo del asunto ----------
        [Theory]
        [InlineData("Pre-registro depositos semana 32", 0)]
        [InlineData("  pre-registro con espacios", 0)]
        [InlineData("Depositos de la semana", 1)]
        [InlineData(null, 1)]
        public void Vr04_exige_prefijo(string? asunto, int hallazgosEsperados)
        {
            var findings = new Vr04SubjectPrefix().Evaluate(Def("VR-04", "Pre-registro"),
                Ctx(new EmailEnvelope { Subject = asunto }));
            Assert.Equal(hallazgosEsperados, findings.Count);
        }

        // ---------- VR-05 extensión ----------
        [Theory]
        [InlineData("declaracion.xlsx", 0)]
        [InlineData("DECLARACION.XLSX", 0)]
        [InlineData("declaracion.xls", 1)]
        [InlineData("minuta.pdf", 1)]
        [InlineData(null, 1)]
        public void Vr05_exige_xlsx(string? archivo, int hallazgosEsperados)
        {
            var findings = new Vr05AttachmentIsXlsx().Evaluate(Def("VR-05", ".xlsx"),
                Ctx(new EmailEnvelope { AttachmentFileName = archivo }));
            Assert.Equal(hallazgosEsperados, findings.Count);
        }

        // ---------- VR-06 tamaño ----------
        [Theory]
        [InlineData(5120, 0)]
        [InlineData(100, 0)]
        [InlineData(5121, 1)]
        public void Vr06_limita_tamano(long kb, int hallazgosEsperados)
        {
            var findings = new Vr06MaxAttachmentSize().Evaluate(Def("VR-06", "5120"),
                Ctx(new EmailEnvelope { AttachmentSizeKb = kb }));
            Assert.Equal(hallazgosEsperados, findings.Count);
        }

        // ---------- Integración: sobre completo contra el motor ----------
        [Fact]
        public void Sobre_valido_pasa_todas_las_reglas()
        {
            var engine = new ValidationEngine(EnvelopeRuleSet.All);
            var result = engine.Run(SeedRules(), Ctx(new EmailEnvelope
            {
                SenderEmail = "contador@cliente.com",
                Subject = "Pre-registro depositos",
                AttachmentCount = 1,
                AttachmentFileName = "declaracion.xlsx",
                AttachmentSizeKb = 200
            }));

            Assert.True(result.Passed);
            Assert.All(result.Executions, e => Assert.Equal(RuleOutcome.Pass, e.Outcome));
        }

        [Fact]
        public void Sobre_sin_adjunto_omite_extension_y_tamano_pero_reporta_el_resto()
        {
            var engine = new ValidationEngine(EnvelopeRuleSet.All);
            var result = engine.Run(SeedRules(), Ctx(new EmailEnvelope
            {
                SenderEmail = "otro@nadie.com",
                Subject = "sin prefijo",
                AttachmentCount = 0
            }));

            var porRegla = result.Executions.ToDictionary(e => e.Definition.Key, e => e.Outcome);
            Assert.Equal(RuleOutcome.Fail, porRegla["VR-02"]);
            Assert.Equal(RuleOutcome.Fail, porRegla["VR-03"]);
            Assert.Equal(RuleOutcome.Fail, porRegla["VR-04"]);
            Assert.Equal(RuleOutcome.Skipped, porRegla["VR-05"]); // depende de VR-02
            Assert.Equal(RuleOutcome.Skipped, porRegla["VR-06"]); // depende de VR-05
            Assert.Equal(3, result.FailFindings.Count());          // TODOS los errores, no solo el primero
        }

        /// <summary>Réplica de data/seed/50-validationrules.json (familia sobre).</summary>
        private static IEnumerable<RuleDefinition> SeedRules() => new[]
        {
            new RuleDefinition("VR-01", "Remitente presente", 10, true, null, "El correo no tiene un remitente valido.", null),
            new RuleDefinition("VR-02", "Cantidad de adjuntos", 20, true, "1", "Debe traer {esperado} adjunto y trae {valor}.", null),
            new RuleDefinition("VR-03", "Remitente registrado", 30, true, null, "El correo {valor} no esta registrado.", "VR-01"),
            new RuleDefinition("VR-04", "Prefijo del asunto", 40, true, "Pre-registro", "El asunto debe comenzar con '{esperado}'.", null),
            new RuleDefinition("VR-05", "Adjunto .xlsx", 50, true, ".xlsx", "El adjunto debe ser {esperado}; llego '{valor}'.", "VR-02"),
            new RuleDefinition("VR-06", "Tamano maximo", 60, true, "5120", "Pesa {valor} KB; maximo {esperado} KB.", "VR-05")
        };
    }
}
