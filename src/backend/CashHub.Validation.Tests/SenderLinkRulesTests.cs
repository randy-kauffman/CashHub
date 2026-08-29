using System;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// VR-03 / VR-25 / VR-26 sobre el modelo de VINCULOS.
    ///
    /// CH-Tbl-ClientEmail tiene clave alternativa (sanic_client, sanic_name): la misma
    /// direccion existe N veces, una por cliente, cada una con su propio recordstatus.
    /// Antes el motor preguntaba "¿existe ALGUNA fila con esta direccion?" (existencial
    /// sobre todos los clientes) y con eso decidia si el banco escribia. Se acabo.
    /// </summary>
    public class SenderLinkRulesTests
    {
        private static readonly Guid ClienteA = Guid.NewGuid();
        private static readonly Guid ClienteB = Guid.NewGuid();
        private const string Sender = "contador@cliente.com";

        private static RuleDefinition Def(string key, string? msg = null) =>
            new(key, key, 1, true, null, msg, null);

        private static (ValidationContext Ctx, FakeDataAccess Data) Escenario(string? cif = "CIF-001")
        {
            var data = new FakeDataAccess();
            var ctx = new ValidationContext(new EmailEnvelope { SenderEmail = Sender }, data);
            if (cif != null)
            {
                ctx.Workbook = new WorkbookBuilder().Cell("Depositos!J2", cif).Build();
            }
            return (ctx, data);
        }

        // ---------------------------------------------------------------------
        // VR-03 — puerta de RESPUESTA: ¿hay al menos un vinculo vigente con el banco?
        // ---------------------------------------------------------------------

        [Fact]
        public void Vr03_pasa_si_hay_al_menos_un_vinculo_vigente_aunque_sea_con_otro_cliente()
        {
            var (ctx, data) = Escenario();
            data.Authorizations.Add((Sender, ClienteB));

            Assert.Empty(new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx));
        }

        [Fact]
        public void Vr03_falla_si_todos_los_vinculos_estan_revocados()
        {
            var (ctx, data) = Escenario();
            data.RevokedLinks.Add((Sender, ClienteA));
            data.RevokedLinks.Add((Sender, ClienteB));

            var findings = new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx);

            Assert.Single(findings);
            Assert.Equal(Sender, findings[0].ActualValue);
        }

        [Fact]
        public void Vr03_falla_si_el_unico_vinculo_esta_pendiente_de_aprobacion()
        {
            // Un alta sin firmar todavia no autoriza nada.
            var (ctx, data) = Escenario();
            data.PendingLinks.Add((Sender, ClienteA));

            Assert.Single(new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx));
        }

        [Fact]
        public void Vr03_falla_si_la_direccion_no_existe_en_ningun_cliente()
        {
            var (ctx, _) = Escenario();

            Assert.Single(new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx));
        }

        // ---------------------------------------------------------------------
        // VR-25 — cliente por CIF, distinguiendo "no existe" de "no esta vigente".
        // ---------------------------------------------------------------------

        [Fact]
        public void Vr25_falla_con_cliente_desactivado_pero_igual_ATRIBUYE_el_correo()
        {
            // Sin esto el correo queda huerfano: sin sanic_client no hay forma de
            // rastrearlo desde la ficha del cliente.
            var (ctx, data) = Escenario();
            data.InactiveClientsByCif["CIF-001"] = ClienteA;

            var findings = new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx);

            Assert.Single(findings);
            Assert.Equal(ClienteA, ctx.ResolvedClientId);
            Assert.Equal(Vigency.Revocado, ctx.ClientVigency);
        }

        [Fact]
        public void Vr25_con_cif_inexistente_no_atribuye()
        {
            var (ctx, _) = Escenario();

            Assert.Single(new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx));
            Assert.Null(ctx.ResolvedClientId);
            Assert.Equal(Vigency.Inexistente, ctx.ClientVigency);
        }

        /// <summary>
        /// Las dos causas de fallo de VR-25 —CIF inexistente y cliente dado de baja— dan
        /// EXACTAMENTE el mismo mensaje, y el mensaje sigue siendo el que redacta negocio
        /// en sanic_CHTblValidationRules.
        ///
        /// No es cosmetica: dos textos distintos serian un oraculo. Quien mande CIFs al
        /// voleo podria distinguir "no existe" de "existe pero esta inactivo" y enumerar
        /// clientes del banco leyendo los rechazos. Con un solo texto no aprende nada.
        /// Y "no corresponde a ningun cliente ACTIVO" es verdad en los dos casos: no hay
        /// que elegir entre no filtrar y no mentir.
        /// </summary>
        [Fact]
        public void Vr25_da_el_mismo_mensaje_exista_o_no_el_cliente()
        {
            const string mensajeDeNegocio = "El CIF '{valor}' no corresponde a ningun cliente activo en el servicio.";

            var (ctxInexistente, _) = Escenario();
            var (ctxDeBaja, dataDeBaja) = Escenario();
            dataDeBaja.InactiveClientsByCif["CIF-001"] = ClienteA;

            var deInexistente = new Vr25ClientRegistered().Evaluate(Def("VR-25", mensajeDeNegocio), ctxInexistente).Single();
            var deBaja = new Vr25ClientRegistered().Evaluate(Def("VR-25", mensajeDeNegocio), ctxDeBaja).Single();

            var textoInexistente = MessageRenderer.Render(mensajeDeNegocio, deInexistente);
            var textoDeBaja = MessageRenderer.Render(mensajeDeNegocio, deBaja);

            Assert.Equal(textoInexistente, textoDeBaja);
            Assert.Contains("no corresponde a ningun cliente activo", textoDeBaja);

            // El cliente de baja igual se ATRIBUYE: el mensaje es el mismo, la trazabilidad no.
            Assert.Equal(ClienteA, ctxDeBaja.ResolvedClientId);
            Assert.Null(ctxInexistente.ResolvedClientId);
        }

        [Fact]
        public void Vr25_pasa_con_cliente_vigente()
        {
            var (ctx, data) = Escenario();
            data.ClientsByCif["CIF-001"] = ClienteA;

            Assert.Empty(new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx));
            Assert.Equal(ClienteA, ctx.ResolvedClientId);
            Assert.Equal(Vigency.Vigente, ctx.ClientVigency);
        }

        // ---------------------------------------------------------------------
        // VR-26 — puerta de CONTENIDO: el vinculo con ESTE cliente.
        // ---------------------------------------------------------------------

        [Fact]
        public void Vr26_falla_si_el_vinculo_con_este_cliente_esta_revocado_aunque_haya_otro_vigente()
        {
            // El caso que mi primer fix dejaba pasar: revocado para A, vigente con B.
            var (ctx, data) = Escenario();
            ctx.ResolvedClientId = ClienteA;
            data.RevokedLinks.Add((Sender, ClienteA));
            data.Authorizations.Add((Sender, ClienteB));

            Assert.Single(new Vr26SenderAuthorizedForClient().Evaluate(Def("VR-26"), ctx));
        }

        [Fact]
        public void Vr26_pasa_solo_con_el_vinculo_vigente_del_cliente_declarado()
        {
            var (ctx, data) = Escenario();
            ctx.ResolvedClientId = ClienteA;
            data.Authorizations.Add((Sender, ClienteA));

            Assert.Empty(new Vr26SenderAuthorizedForClient().Evaluate(Def("VR-26"), ctx));
        }

        // ---------------------------------------------------------------------
        // Los vinculos se consultan UNA vez por corrida.
        // ---------------------------------------------------------------------

        [Fact]
        public void Los_vinculos_del_remitente_se_consultan_una_sola_vez()
        {
            var (ctx, data) = Escenario();
            ctx.ResolvedClientId = ClienteA;
            data.Authorizations.Add((Sender, ClienteA));

            new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx);
            new Vr26SenderAuthorizedForClient().Evaluate(Def("VR-26"), ctx);
            new Vr26SenderAuthorizedForClient().Evaluate(Def("VR-26"), ctx);

            Assert.Equal(1, data.SenderLinkQueries);
        }

        // ---------------------------------------------------------------------
        // De los vinculos a los hechos del triage.
        // ---------------------------------------------------------------------

        [Fact]
        public void Los_hechos_del_triage_salen_del_contexto_sin_consultar_de_nuevo()
        {
            var (ctx, data) = Escenario();
            data.RevokedLinks.Add((Sender, ClienteA));
            data.Authorizations.Add((Sender, ClienteB));
            data.InactiveClientsByCif["CIF-001"] = ClienteA;

            new Vr03SenderRegistered().Evaluate(Def("VR-03"), ctx);
            new Vr25ClientRegistered().Evaluate(Def("VR-25"), ctx);

            var facts = ctx.BuildTriageFacts();

            Assert.Equal(Vigency.Revocado, facts.LinkToClient);
            Assert.True(facts.HasAnyActiveLink);
            Assert.True(facts.HasAnyRevokedLink);
            Assert.Equal(Vigency.Revocado, facts.Client);
            Assert.Equal(1, data.SenderLinkQueries);
        }

        [Fact]
        public void Sin_remitente_los_hechos_quedan_en_desconocido()
        {
            var ctx = new ValidationContext(new EmailEnvelope { SenderEmail = null }, new FakeDataAccess());

            var facts = ctx.BuildTriageFacts();

            Assert.Equal(Vigency.Inexistente, facts.LinkToClient);
            Assert.False(facts.HasAnyActiveLink);
            Assert.False(facts.HasAnyRevokedLink);
        }
    }
}
