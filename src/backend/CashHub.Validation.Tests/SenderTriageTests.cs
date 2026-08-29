using System.Collections.Generic;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El triage contesta DOS preguntas independientes, no una:
    ///   1) ¿sale un correo del banco?  -> hay al menos un vinculo VIGENTE con el banco.
    ///   2) ¿que podemos decir?         -> el vinculo con ESTE cliente esta vigente.
    ///
    /// La direccion no es global: CH-Tbl-ClientEmail tiene clave alternativa
    /// (sanic_client, sanic_name) — "Correo unico por cliente". Cada fila es un VINCULO
    /// (direccion x cliente) con su propio recordstatus. Desactivar un ClientEmail revoca
    /// el PAR, no la direccion. Por eso "¿el correo esta desactivado?" siempre es
    /// "desactivado ¿para quien?".
    /// </summary>
    public class SenderTriageTests
    {
        private static RuleExecution Exec(string key, RuleOutcome outcome) =>
            new(new RuleDefinition(key, key, 1, true, null, null, null), outcome,
                outcome == RuleOutcome.Fail ? new[] { new Finding("x") } : new Finding[0], 1);

        private static TriageDecision Clasificar(TriageFacts facts,
            params (string Key, RuleOutcome Outcome)[] ejecuciones)
        {
            var list = new List<RuleExecution>();
            foreach (var (key, outcome) in ejecuciones) list.Add(Exec(key, outcome));
            return SenderTriage.Classify(new RunResult(list), facts);
        }

        private static TriageFacts Facts(Vigency link, bool otroVigente = false,
            bool otroRevocado = false, Vigency cliente = Vigency.Vigente) =>
            new(link, otroVigente, otroRevocado, cliente);

        // ---------------------------------------------------------------------
        // Los 5 casos de vinculo acordados con negocio.
        // ---------------------------------------------------------------------

        /// <summary>Caso 5: vinculo vigente con el cliente que declara. Unico caso con detalle.</summary>
        [Fact]
        public void Vinculo_vigente_con_el_cliente_responde_con_detalle_completo()
        {
            var d = Clasificar(Facts(Vigency.Vigente), ("VR-03", RuleOutcome.Pass));

            Assert.Equal(SenderTrust.Registered, d.Trust);
            Assert.True(d.MayReplyToSender);
            Assert.True(d.MayDiscloseClientDetail);
        }

        /// <summary>
        /// Caso 2: sin vinculo con este cliente, pero vigente con otro. Es un contacto vivo
        /// del banco (tipico: grupo empresarial que tipeo el CIF de la razon social hermana).
        /// Se le responde para no dejarlo esperando, pero SIN confirmar nada del CIF ajeno.
        /// </summary>
        [Fact]
        public void Sin_vinculo_pero_vigente_con_otro_cliente_responde_generico()
        {
            var d = Clasificar(Facts(Vigency.Inexistente, otroVigente: true));

            Assert.Equal(SenderTrust.OtherClientContact, d.Trust);
            Assert.True(d.MayReplyToSender);
            Assert.False(d.MayDiscloseClientDetail);
        }

        /// <summary>
        /// Caso 4: vinculo con ESTE cliente revocado, pero vigente con otro. Puerta de
        /// respuesta identica al caso 2 (decision de negocio), clasificacion distinta:
        /// es el unico escenario donde alguien usa un acceso que le quitamos a proposito.
        /// El silencio no compra seguridad aca; el escalamiento si.
        /// </summary>
        [Fact]
        public void Vinculo_revocado_para_este_cliente_responde_generico_pero_se_marca_distinto()
        {
            var d = Clasificar(Facts(Vigency.Revocado, otroVigente: true));

            Assert.Equal(SenderTrust.RevokedForClient, d.Trust);
            Assert.True(d.MayReplyToSender);
            Assert.False(d.MayDiscloseClientDetail);
        }

        /// <summary>Caso 3: sin vinculo con este cliente y revocado en todos los demas.</summary>
        [Fact]
        public void Revocado_en_todos_lados_no_recibe_respuesta()
        {
            var d = Clasificar(Facts(Vigency.Inexistente, otroRevocado: true));

            Assert.Equal(SenderTrust.Revoked, d.Trust);
            Assert.False(d.MayReplyToSender);
            Assert.False(d.MayDiscloseClientDetail);
        }

        /// <summary>
        /// Caso 4 sin salvavidas: revocado para este cliente y sin ningun vinculo vigente.
        /// La puerta de respuesta lo corta aunque la clasificacion siga siendo la fuerte.
        /// </summary>
        [Fact]
        public void Revocado_para_este_cliente_y_sin_otros_vinculos_no_recibe_respuesta()
        {
            var d = Clasificar(Facts(Vigency.Revocado));

            Assert.Equal(SenderTrust.RevokedForClient, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        /// <summary>Caso 1: desconocido total + plantilla oficial + CIF real => Banca lo registra.</summary>
        [Fact]
        public void Desconocido_total_con_plantilla_y_cif_real_es_cliente_sin_registrar()
        {
            var d = Clasificar(Facts(Vigency.Inexistente),
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Pass));

            Assert.Equal(SenderTrust.UnregisteredClient, d.Trust);
            Assert.False(d.MayReplyToSender);
            Assert.Contains("registrar", d.Reason);
        }

        // ---------------------------------------------------------------------
        // Vinculo pendiente de aprobacion: NO es un revocado.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Decision de negocio: mientras el vinculo no este resuelto no autoriza, y va al
        /// MISMO balde que un revocado — ante la duda se trabaja del lado seguro. La
        /// vigencia real igual queda en el motivo, asi que no se pierde el diagnostico.
        /// </summary>
        [Fact]
        public void Vinculo_pendiente_de_aprobacion_se_trata_igual_que_un_revocado()
        {
            var pendiente = Clasificar(Facts(Vigency.Pendiente, otroVigente: true));
            var revocado = Clasificar(Facts(Vigency.Revocado, otroVigente: true));

            Assert.Equal(SenderTrust.RevokedForClient, pendiente.Trust);
            Assert.Equal(revocado.Trust, pendiente.Trust);
            Assert.Equal(revocado.MayReplyToSender, pendiente.MayReplyToSender);
            Assert.False(pendiente.MayDiscloseClientDetail);
        }

        [Fact]
        public void El_motivo_distingue_pendiente_de_revocado_aunque_el_balde_sea_el_mismo()
        {
            // Mismo trato, distinto diagnostico: seguridad si le cortamos, Banca si hay un
            // RFC de alta esperando firma.
            Assert.Contains("PENDIENTE DE APROBACION",
                Clasificar(Facts(Vigency.Pendiente, otroVigente: true)).Reason);
            Assert.Contains("REVOCADO",
                Clasificar(Facts(Vigency.Revocado, otroVigente: true)).Reason);
        }

        [Fact]
        public void Vinculo_pendiente_sin_otros_vinculos_no_recibe_respuesta()
        {
            Assert.False(Clasificar(Facts(Vigency.Pendiente)).MayReplyToSender);
        }

        // ---------------------------------------------------------------------
        // Cliente desactivado: eje ORTOGONAL al estado del vinculo.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Cliente dado de baja pero el contacto sigue vigente con el: es su contacto
        /// autorizado, asi que merece el rechazo con el motivo real. No hay fuga posible
        /// (le estamos hablando de SU propio cliente).
        /// </summary>
        [Fact]
        public void Cliente_desactivado_con_vinculo_vigente_recibe_rechazo_con_motivo()
        {
            var d = Clasificar(Facts(Vigency.Vigente, cliente: Vigency.Revocado),
                ("VR-03", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.Registered, d.Trust);
            Assert.True(d.MayReplyToSender);
            Assert.True(d.MayDiscloseClientDetail);
        }

        /// <summary>
        /// Cliente desactivado + vinculo revocado (el ex-cliente escribiendo desde su
        /// ex-direccion): es el escenario MAS inocente de la matriz. Antes caia en
        /// Sospechoso y disparaba alerta de seguridad. Nunca mas.
        /// </summary>
        [Fact]
        public void Ex_cliente_desde_su_ex_direccion_no_es_sospechoso()
        {
            var d = Clasificar(Facts(Vigency.Revocado, cliente: Vigency.Revocado),
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Fail));

            Assert.NotEqual(SenderTrust.Suspicious, d.Trust);
            Assert.Equal(SenderTrust.RevokedForClient, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        /// <summary>
        /// Desconocido + plantilla oficial + CIF de un cliente DADO DE BAJA: no es un
        /// ladron de plantillas, es un ex-cliente. Va a Banca, no a la cola de alertas.
        /// </summary>
        [Fact]
        public void Desconocido_con_cif_de_cliente_dado_de_baja_no_es_sospechoso()
        {
            var d = Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Revocado),
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.UnregisteredClient, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        // ---------------------------------------------------------------------
        // Lo que ya funcionaba y no se puede romper.
        // ---------------------------------------------------------------------

        [Fact]
        public void Plantilla_oficial_con_cif_inexistente_sigue_siendo_sospechoso()
        {
            var d = Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Inexistente),
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Pass),
                ("VR-25", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.Suspicious, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        [Fact]
        public void Sin_plantilla_oficial_es_ruido_y_no_se_responde()
        {
            var d = Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Inexistente),
                ("VR-03", RuleOutcome.Fail),
                ("VR-27", RuleOutcome.Fail));

            Assert.Equal(SenderTrust.Unrelated, d.Trust);
            Assert.False(d.MayReplyToSender);
        }

        [Fact]
        public void Regla_omitida_por_dependencia_no_cuenta_como_aprobada()
        {
            // VR-05 fallo (no es .xlsx) => VR-27 ni se ejecuta. No hay prueba de plantilla.
            var d = Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Inexistente),
                ("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Skipped));

            Assert.Equal(SenderTrust.Unrelated, d.Trust);
        }

        [Fact]
        public void Sin_remitente_valido_no_hay_a_quien_responder()
        {
            // VR-01 fallo => VR-03 se omite y los vinculos nunca se cargaron.
            var d = Clasificar(TriageFacts.Unknown, ("VR-01", RuleOutcome.Fail));

            Assert.False(d.MayReplyToSender);
            Assert.False(d.MayDiscloseClientDetail);
            Assert.Equal(SenderTrust.Unrelated, d.Trust);
        }

        [Fact]
        public void Si_la_regla_del_remitente_revento_no_se_responde_pero_no_se_pierde_el_deposito()
        {
            // Ante la duda: no escribimos, pero si el archivo prueba ser de un cliente real
            // lo mandamos a revision interna en vez de tirarlo a la basura.
            var d = Clasificar(TriageFacts.Unknown,
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
                Clasificar(Facts(Vigency.Vigente), ("VR-03", RuleOutcome.Pass)),
                Clasificar(Facts(Vigency.Revocado, otroVigente: true)),
                Clasificar(Facts(Vigency.Pendiente, otroVigente: true)),
                Clasificar(Facts(Vigency.Inexistente, otroVigente: true)),
                Clasificar(Facts(Vigency.Inexistente, otroRevocado: true)),
                Clasificar(Facts(Vigency.Inexistente), ("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Pass), ("VR-25", RuleOutcome.Pass)),
                Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Inexistente), ("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Pass), ("VR-25", RuleOutcome.Fail)),
                Clasificar(Facts(Vigency.Inexistente, cliente: Vigency.Inexistente), ("VR-03", RuleOutcome.Fail), ("VR-27", RuleOutcome.Fail)),
            })
            {
                Assert.False(string.IsNullOrWhiteSpace(d.Reason));
                Assert.Contains("VR-", d.Reason);
            }
        }

        /// <summary>
        /// Invariante dura: solo el vinculo VIGENTE con este cliente habilita contar
        /// detalles. Cualquier otro estado responde generico o no responde.
        /// </summary>
        [Fact]
        public void Solo_el_vinculo_vigente_habilita_contar_detalles()
        {
            foreach (var link in new[] { Vigency.Inexistente, Vigency.Revocado, Vigency.Pendiente })
            {
                var d = Clasificar(Facts(link, otroVigente: true));
                Assert.False(d.MayDiscloseClientDetail);
            }
        }
    }
}
