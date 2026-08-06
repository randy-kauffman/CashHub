using System.Collections.Generic;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El triage decide si el banco puede escribirle al remitente. La regla es absoluta:
    /// NUNCA sale correo hacia una direccion que no esta verificada. Responderle a un
    /// desconocido "no estas autorizado para Distribuidora El Porvenir" le confirmaria a
    /// un tercero que ese cliente opera con el banco.
    /// </summary>
    public class SenderTriageTests
    {
        private static RuleExecution Exec(string key, RuleOutcome outcome) =>
            new(new RuleDefinition(key, key, 1, true, null, null, null), outcome,
                outcome == RuleOutcome.Fail ? new[] { new Finding("x") } : new Finding[0], 1);

        private static TriageDecision Clasificar(params (string Key, RuleOutcome Outcome)[] ejecuciones)
        {
            var list = new List<RuleExecution>();
            foreach (var (key, outcome) in ejecuciones) list.Add(Exec(key, outcome));
            return SenderTriage.Classify(new RunResult(list));
        }

        [Fact]
        public void Remitente_registrado_puede_recibir_respuesta()
        {
            var d = Clasificar(("VR-03", RuleOutcome.Pass));
            Assert.Equal(SenderTrust.Registered, d.Trust);
            Assert.True(d.MayReplyToSender);
        }

        [Fact]
        public void Plantilla_oficial_con_cif_real_es_cliente_sin_registrar_y_no_se_responde()
        {
            var d = Clasificar(
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Pass));

            Assert.Equal(SenderTrust.UnregisteredClient, d.Trust);
            Assert.False(d.MayReplyToSender);
            Assert.Contains("registrar", d.Reason);
        }

        [Fact]
        public void Plantilla_oficial_con_cif_inexistente_es_sospechoso()
        {
            var d = Clasificar(
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.Suspicious, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        [Fact]
        public void Sin_plantilla_oficial_es_ruido_y_no_se_responde()
        {
            var d = Clasificar(
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.Unrelated, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        [Fact]
        public void Regla_omitida_por_dependencia_no_cuenta_como_aprobada()
        {
            // VR-05 fallo (no es .xlsx) => VR-27 ni se ejecuta. No hay prueba de plantilla.
            var d = Clasificar(("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Skipped));
            Assert.Equal(SenderTrust.Unrelated, d.Trust);
        }

        [Fact]
        public void Sin_remitente_valido_no_hay_a_quien_responder()
        {
            // VR-01 fallo => VR-03 se omite. No sabemos ni la direccion.
            var d = Clasificar(("VR-01", RuleOutcome.Fail));
            Assert.False(d.MayReplyToSender);
            Assert.Equal(SenderTrust.Unrelated, d.Trust);
        }

        [Fact]
        public void Si_la_regla_del_remitente_reventa_no_se_responde_pero_no_se_pierde_el_deposito()
        {
            // Ante la duda: no escribimos, pero si el archivo prueba ser de un cliente real
            // lo mandamos a revision interna en vez de tirarlo a la basura.
            var d = Clasificar(
                ("VR-03", RuleOutcome.Error),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Pass));

            Assert.False(d.MayReplyToSender);
            Assert.Equal(SenderTrust.UnregisteredClient, d.Trust);
        }

        [Fact]
        public void El_motivo_nunca_es_generico()
        {
            foreach (var d in new[]
            {
                Clasificar(("VR-03", RuleOutcome.Pass)),
                Clasificar(("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Pass), ("VR-25", RuleOutcome.Pass)),
                Clasificar(("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Pass), ("VR-25", RuleOutcome.Fail)),
                Clasificar(("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Fail)),
            })
            {
                Assert.False(string.IsNullOrWhiteSpace(d.Reason));
                Assert.Contains("VR-", d.Reason);
            }
        }
    }
}
