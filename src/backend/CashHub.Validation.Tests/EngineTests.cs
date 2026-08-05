using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using Xunit;

namespace CashHub.Validation.Tests
{
    internal sealed class FakeDataAccess : IValidationDataAccess
    {
        public HashSet<string> RegisteredSenders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsSenderRegistered(string senderEmail) => RegisteredSenders.Contains(senderEmail);
    }

    internal sealed class AlwaysFailsRule : IValidationRule
    {
        public string Key => "VR-XX";
        public IReadOnlyList<Finding> Evaluate(RuleDefinition d, ValidationContext c)
            => new[] { new Finding(Key, actualValue: "malo") };
    }

    internal sealed class ThrowingRule : IValidationRule
    {
        public string Key => "VR-BOOM";
        public IReadOnlyList<Finding> Evaluate(RuleDefinition d, ValidationContext c)
            => throw new InvalidOperationException("explota");
    }

    public class EngineTests
    {
        private static ValidationContext Context() =>
            new(new EmailEnvelope(), new FakeDataAccess());

        private static RuleDefinition Rule(string key, int order, string? dependsOn = null,
            bool active = true, string? value = null, string? message = null) =>
            new(key, key, order, active, value, message, dependsOn);

        [Fact]
        public void Regla_dependiente_se_omite_cuando_la_madre_falla()
        {
            var engine = new ValidationEngine(new IValidationRule[] { new AlwaysFailsRule(), new Vr04Stub() });
            var result = engine.Run(new[]
            {
                Rule("VR-XX", 10),
                Rule("VR-STUB", 20, dependsOn: "VR-XX")
            }, Context());

            Assert.Equal(RuleOutcome.Fail, result.Executions[0].Outcome);
            Assert.Equal(RuleOutcome.Skipped, result.Executions[1].Outcome);
        }

        [Fact]
        public void Regla_dependiente_se_omite_cuando_la_madre_esta_inactiva()
        {
            var engine = new ValidationEngine(new IValidationRule[] { new AlwaysFailsRule(), new Vr04Stub() });
            var result = engine.Run(new[]
            {
                Rule("VR-XX", 10, active: false),
                Rule("VR-STUB", 20, dependsOn: "VR-XX")
            }, Context());

            Assert.Single(result.Executions);          // la inactiva ni aparece
            Assert.Equal(RuleOutcome.Skipped, result.Executions[0].Outcome);
        }

        [Fact]
        public void Regla_activa_sin_estrategia_se_reporta_como_NotImplemented()
        {
            var engine = new ValidationEngine(Array.Empty<IValidationRule>());
            var result = engine.Run(new[] { Rule("VR-99", 10) }, Context());

            Assert.Equal(RuleOutcome.NotImplemented, result.Executions[0].Outcome);
            Assert.True(result.HasSystemIssues);
            Assert.False(result.Passed);
        }

        [Fact]
        public void Excepcion_en_una_estrategia_se_aisla_y_el_resto_corre()
        {
            var engine = new ValidationEngine(new IValidationRule[] { new ThrowingRule(), new Vr04Stub() });
            var result = engine.Run(new[]
            {
                Rule("VR-BOOM", 10),
                Rule("VR-STUB", 20)
            }, Context());

            Assert.Equal(RuleOutcome.Error, result.Executions[0].Outcome);
            Assert.Equal(RuleOutcome.Pass, result.Executions[1].Outcome);
            Assert.False(result.Passed);
        }

        [Fact]
        public void No_se_detiene_en_el_primer_fallo_y_renderiza_los_mensajes()
        {
            var engine = new ValidationEngine(new IValidationRule[] { new AlwaysFailsRule(), new Vr04Stub() });
            var result = engine.Run(new[]
            {
                Rule("VR-XX", 10, message: "Fallo con valor {valor}"),
                Rule("VR-STUB", 20)
            }, Context());

            var finding = result.FailFindings.Single();
            Assert.Equal("Fallo con valor malo", finding.RenderedMessage);
            Assert.Equal(2, result.Executions.Count); // siguió tras el fallo
        }

        [Fact]
        public void Las_reglas_corren_en_orden_de_ValidationOrder()
        {
            var engine = new ValidationEngine(new IValidationRule[] { new AlwaysFailsRule(), new Vr04Stub() });
            var result = engine.Run(new[]
            {
                Rule("VR-STUB", 20),
                Rule("VR-XX", 10)
            }, Context());

            Assert.Equal("VR-XX", result.Executions[0].Definition.Key);
            Assert.Equal("VR-STUB", result.Executions[1].Definition.Key);
        }

        private sealed class Vr04Stub : IValidationRule
        {
            public string Key => "VR-STUB";
            public IReadOnlyList<Finding> Evaluate(RuleDefinition d, ValidationContext c)
                => Array.Empty<Finding>();
        }
    }
}
