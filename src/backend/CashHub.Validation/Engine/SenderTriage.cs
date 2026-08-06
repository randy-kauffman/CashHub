using System.Linq;

namespace CashHub.Validation.Engine
{
    /// <summary>Grado de confianza que el banco tiene en el remitente de un correo.</summary>
    public enum SenderTrust
    {
        /// <summary>Direccion verificada: existe en CH-Tbl-ClientEmail (VR-03 paso).</summary>
        Registered,

        /// <summary>
        /// Direccion desconocida, pero el adjunto ES la plantilla oficial y declara el CIF de
        /// un cliente real. Casi seguro un cliente legitimo cuyo correo nadie registro todavia.
        /// </summary>
        UnregisteredClient,

        /// <summary>
        /// Direccion desconocida con la plantilla oficial pero un CIF que no existe.
        /// Alguien tiene nuestra plantilla y no deberia: esto se alerta.
        /// </summary>
        Suspicious,

        /// <summary>Direccion desconocida y sin plantilla oficial: ruido o spam.</summary>
        Unrelated
    }

    /// <summary>Que se puede hacer con el correo, y por que.</summary>
    public sealed class TriageDecision
    {
        public TriageDecision(SenderTrust trust, bool mayReplyToSender, string reason)
        {
            Trust = trust;
            MayReplyToSender = mayReplyToSender;
            Reason = reason;
        }

        public SenderTrust Trust { get; }

        /// <summary>
        /// Unico permiso para que salga un correo del banco. Solo es true con la direccion
        /// verificada: escribirle a un desconocido divulgaria que ese CIF es cliente nuestro.
        /// </summary>
        public bool MayReplyToSender { get; }

        /// <summary>Motivo explicito, con las reglas que lo sustentan (regla de la casa).</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Clasifica al remitente a partir de reglas que el motor YA evalua. Las cadenas de
    /// dependencia de VR-03 (remitente) y VR-25 (CIF) son independientes, asi que el CIF se
    /// resuelve aunque el remitente sea desconocido: por eso se puede distinguir un cliente
    /// sin registrar de un spam sin escribirle a nadie.
    /// </summary>
    public static class SenderTriage
    {
        public const string SenderRegisteredRule = "VR-03";
        public const string OfficialTemplateRule = "VR-27";
        public const string ClientRegisteredRule = "VR-25";

        public static TriageDecision Classify(RunResult result)
        {
            var sender = Outcome(result, SenderRegisteredRule);
            if (sender == RuleOutcome.Pass)
            {
                return new TriageDecision(SenderTrust.Registered, true,
                    $"{SenderRegisteredRule} paso: la direccion esta registrada en CashHub, se puede responder.");
            }

            var template = Outcome(result, OfficialTemplateRule);
            var client = Outcome(result, ClientRegisteredRule);

            // Sin remitente evaluable (VR-01 fallo y VR-03 ni corrio) no hay a quien escribirle.
            var senderState = sender == null
                ? $"{SenderRegisteredRule} no se evaluo (sin remitente valido)"
                : $"{SenderRegisteredRule} dio {sender}";

            if (template == RuleOutcome.Pass && client == RuleOutcome.Pass)
            {
                return new TriageDecision(SenderTrust.UnregisteredClient, false,
                    $"{senderState}, pero {OfficialTemplateRule} confirma la plantilla oficial y " +
                    $"{ClientRegisteredRule} resolvio el CIF a un cliente real: se trata como cliente " +
                    "por registrar y pasa a revision interna; no se le escribe al remitente.");
            }

            if (template == RuleOutcome.Pass)
            {
                return new TriageDecision(SenderTrust.Suspicious, false,
                    $"{senderState} y {OfficialTemplateRule} confirma la plantilla oficial, pero " +
                    $"{ClientRegisteredRule} dio {client?.ToString() ?? "no se evaluo"}: el CIF no " +
                    "corresponde a ningun cliente. Se cuarentena y se alerta.");
            }

            return new TriageDecision(SenderTrust.Unrelated, false,
                $"{senderState} y {OfficialTemplateRule} dio {template?.ToString() ?? "no se evaluo"}: " +
                "el adjunto no prueba ser la plantilla oficial. Se cuarentena sin responder.");
        }

        /// <summary>
        /// Resultado de una regla, o null si el motor no la ejecuto. Omitida por dependencia
        /// NO es aprobada: sin evidencia no se otorga confianza.
        /// </summary>
        private static RuleOutcome? Outcome(RunResult result, string key) =>
            result.Executions.FirstOrDefault(e => e.Definition.Key == key)?.Outcome;
    }
}
