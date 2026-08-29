using System.Linq;
using CashHub.Validation.Models;

namespace CashHub.Validation.Engine
{
    /// <summary>Grado de confianza que el banco tiene en el remitente de un correo.</summary>
    public enum SenderTrust
    {
        /// <summary>Vinculo VIGENTE con el cliente declarado. El unico que habilita detalle.</summary>
        Registered,

        /// <summary>
        /// El vinculo con ESTE cliente fue revocado. Es el unico escenario donde alguien
        /// usa un acceso que le quitamos a proposito: la senal mas fuerte de la matriz.
        /// </summary>
        RevokedForClient,

        /// <summary>
        /// Sin vinculo con este cliente, pero vigente con otro. Tipicamente el grupo
        /// empresarial que tipeo el CIF de la razon social hermana.
        /// </summary>
        OtherClientContact,

        /// <summary>Sin vinculo vigente en ningun lado y con revocados: le cortamos e insiste.</summary>
        Revoked,

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
        public TriageDecision(SenderTrust trust, bool mayReplyToSender, bool mayDiscloseClientDetail,
            string reason)
        {
            Trust = trust;
            MayReplyToSender = mayReplyToSender;
            MayDiscloseClientDetail = mayDiscloseClientDetail;
            Reason = reason;
        }

        public SenderTrust Trust { get; }

        /// <summary>
        /// Puerta de RESPUESTA: unico permiso para que salga un correo del banco. Se abre
        /// cuando la direccion tiene al menos un vinculo vigente — hay relacion comercial
        /// viva. Escribirle a un desconocido le confirmaria que ese CIF es cliente nuestro.
        /// </summary>
        public bool MayReplyToSender { get; }

        /// <summary>
        /// Puerta de CONTENIDO: solo el vinculo vigente con ESTE cliente habilita nombrarlo
        /// o listar los hallazgos. Sin esto se responde generico, sin confirmar nada del CIF.
        ///
        /// Separarla de MayReplyToSender es el punto del rediseno: antes un solo booleano
        /// contestaba "¿le escribimos?" y "¿que le decimos?" a la vez, y el mensaje de VR-26
        /// ("no esta autorizado para declarar depositos del cliente indicado") terminaba
        /// confirmandole a un tercero que ese CIF opera con el banco.
        /// </summary>
        public bool MayDiscloseClientDetail { get; }

        /// <summary>Motivo explicito, con las reglas que lo sustentan (regla de la casa).</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Clasifica al remitente cruzando los HECHOS de los vinculos (TriageFacts, que salen de
    /// datos) con los resultados de las reglas de plantilla y CIF.
    ///
    /// Los hechos van aparte de las reglas a proposito: VR-26 queda Omitida cuando VR-25
    /// falla (cliente dado de baja), y aun asi necesitamos saber si el remitente era su
    /// contacto autorizado para decidir si le respondemos.
    /// </summary>
    public static class SenderTriage
    {
        public const string SenderRegisteredRule = "VR-03";
        public const string OfficialTemplateRule = "VR-27";
        public const string ClientRegisteredRule = "VR-25";
        public const string SenderAuthorizedRule = "VR-26";

        public static TriageDecision Classify(RunResult result, TriageFacts facts)
        {
            switch (facts.LinkToClient)
            {
                case Vigency.Vigente:
                    return new TriageDecision(SenderTrust.Registered, true, true,
                        $"{SenderAuthorizedRule}: el vinculo entre el remitente y el cliente declarado " +
                        "esta vigente. Se responde con el detalle completo.");

                // Pendiente se trata IGUAL que Revocado por decision de negocio: mientras el
                // vinculo no este resuelto no autoriza, y ante la duda se trabaja del lado
                // seguro. La vigencia real igual queda en el tracing y en el propio
                // recordstatus del registro, asi que no se pierde el diagnostico.
                case Vigency.Revocado:
                case Vigency.Pendiente:
                    var sinResolver = facts.LinkToClient == Vigency.Pendiente;
                    return new TriageDecision(SenderTrust.RevokedForClient, facts.HasAnyActiveLink, false,
                        $"{SenderAuthorizedRule}: el vinculo con el cliente declarado " +
                        (sinResolver ? "esta PENDIENTE DE APROBACION" : "fue REVOCADO") + ", no autoriza. " +
                        (facts.HasAnyActiveLink
                            ? "El remitente sigue vigente con otro cliente, asi que se le responde en " +
                              "terminos genericos, sin confirmar nada del CIF."
                            : "No le queda ningun vinculo vigente: no se responde.") +
                        " Va a revision interna.");
            }

            if (facts.HasAnyActiveLink)
            {
                return new TriageDecision(SenderTrust.OtherClientContact, true, false,
                    $"{SenderAuthorizedRule}: el remitente no tiene vinculo con el cliente declarado, " +
                    "pero esta vigente con otro. Se le responde en terminos genericos para no dejarlo " +
                    "esperando, sin confirmar nada del CIF ajeno.");
            }

            if (facts.HasAnyRevokedLink)
            {
                return new TriageDecision(SenderTrust.Revoked, false, false,
                    $"{SenderAuthorizedRule}: todos los vinculos del remitente estan revocados. " +
                    "Escribirle contradiria el acto de revocar. Se cuarentena sin responder.");
            }

            // Desconocido total: no hay ninguna fila con esta direccion en ningun cliente.
            var sender = Outcome(result, SenderRegisteredRule);
            var senderState = sender == null
                ? $"{SenderRegisteredRule} no se evaluo (sin remitente valido)"
                : $"{SenderRegisteredRule} dio {sender}";

            var template = Outcome(result, OfficialTemplateRule);
            if (template == RuleOutcome.Pass)
            {
                // El cliente cuenta como resuelto aunque este dado de baja: un ex-cliente no
                // es un ladron de plantillas. Se mira tambien el outcome de VR-25 porque si
                // VR-03 revento nunca llegamos a cargar los hechos.
                var clientKnown = facts.Client != Vigency.Inexistente
                    || Outcome(result, ClientRegisteredRule) == RuleOutcome.Pass;

                if (clientKnown)
                {
                    return new TriageDecision(SenderTrust.UnregisteredClient, false, false,
                        $"{senderState} y el remitente no figura en ningun cliente, pero " +
                        $"{OfficialTemplateRule} confirma la plantilla oficial y {ClientRegisteredRule} " +
                        "ubico el CIF en un cliente real: se trata como cliente por registrar y pasa a " +
                        "revision interna; no se le escribe al remitente.");
                }

                return new TriageDecision(SenderTrust.Suspicious, false, false,
                    $"{senderState} y {OfficialTemplateRule} confirma la plantilla oficial, pero " +
                    $"{ClientRegisteredRule} no ubico el CIF en ningun cliente, ni activo ni dado de " +
                    "baja. Alguien tiene nuestra plantilla y no deberia. Se cuarentena y se alerta.");
            }

            return new TriageDecision(SenderTrust.Unrelated, false, false,
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
