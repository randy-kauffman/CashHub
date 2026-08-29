using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ChangeRequestApprovalGuard;
using static CashHub.Validation.Security.ChangeRequestResolution;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Motor de decision de la Custom API sanic_CHResolveChangeRequest: reemplaza los 4
    /// flujos materializadores. Reutiliza <see cref="ChangeRequestApprovalGuard"/> para la
    /// separacion de funciones (creador != resolutor, membresia Approver) y agrega el
    /// mapeo a los estados FINALES de cabecera y linea (Applied/Rejected+Denied) que antes
    /// vivian repartidos entre 4 flujos de Power Automate.
    /// </summary>
    public class ChangeRequestResolutionTests
    {
        private static readonly Guid Creator = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Approver = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private const bool IsApprover = true;
        private const bool NotApprover = false;

        [Fact]
        public void Approve_Submitted_ByAuthorizedApprover_ReturnsAppliedHeaderAndLine()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Approver, IsApprover);

            Assert.True(result.IsAllowed);
            Assert.Equal(Applied, result.HeaderStatus);
            Assert.Equal(LineApplied, result.LineStatus);
        }

        [Fact]
        public void Reject_Submitted_ByAuthorizedApprover_ReturnsRejectedHeaderAndDeniedLine()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Reject, Creator, Approver, IsApprover);

            Assert.True(result.IsAllowed);
            Assert.Equal(Rejected, result.HeaderStatus);
            Assert.Equal(LineDenied, result.LineStatus);
        }

        [Fact]
        public void Approve_ByCreator_IsBlocked()
        {
            // Mismo agujero que ChangeRequestApprovalGuard: el creador no se auto-resuelve.
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Creator, IsApprover);

            Assert.False(result.IsAllowed);
            Assert.Contains("separacion de funciones", result.Reason);
        }

        [Fact]
        public void Approve_ByNonApprover_IsBlocked()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Approver, NotApprover);

            Assert.False(result.IsAllowed);
            Assert.Contains("grupo de aprobadores", result.Reason);
        }

        [Theory]
        [InlineData(Draft)]
        [InlineData(Approved)]
        [InlineData(Rejected)]
        [InlineData(Applied)]
        [InlineData(Failed)]
        [InlineData(Canceled)]
        public void Resolve_FromNonSubmittedState_IsBlocked(int currentStatus)
        {
            var result = Evaluate(currentStatus, ChangeRequestDecision.Approve, Creator, Approver, IsApprover);

            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData(TypeUpdate, true)]
        [InlineData(TypeCreate, true)]
        [InlineData(TypeActivate, false)]
        [InlineData(TypeDeactivate, false)]
        public void RequiresLines_UpdateAndCreateOnly(int type, bool expected)
        {
            Assert.Equal(expected, RequiresLines(type));
        }

        // --- Campos obligatorios del cliente (Create Y Update) ---
        //
        // Regla de negocio: NINGUN cliente puede quedar sin sanic_taxid. Se evalua sobre el
        // ESTADO FINAL del maestro, no sobre el contenido del RFC:
        //
        //   satisfecho = si el RFC trae linea del campo -> el valor no es blanco
        //                si no                          -> Update y el maestro YA lo tiene
        //
        // Asi, editar un cliente que ya tiene taxid no obliga a repetirlo (el panel de
        // edicion filtra los valores que no cambiaron: seria imposible de cumplir), pero
        // editar uno de los ~4999 historicos sin taxid EXIGE completarlo en la misma
        // solicitud. El backfill converge solo.
        //
        // En Dataverse el campo sigue con RequiredLevel=None: ApplicationRequired no se
        // enforcea a nivel SDK — o sea, no frenaria a este mismo plugin — y ademas volveria
        // no-guardables a los historicos desde los formularios model-driven.

        private const string ClientTable = "sanic_chtblclient";
        private const string BranchTable = "sanic_chtblclientbranch";

        private static KeyValuePair<string, string?> Val(string logicalName, string? value) =>
            new KeyValuePair<string, string?>(logicalName, value);

        /// <summary>Maestro que YA tiene taxid cargado.</summary>
        private static readonly KeyValuePair<string, string?>[] MaestroConTaxId =
            { Val("sanic_taxid", "J0310000012345") };

        /// <summary>Uno de los ~4999 historicos: existe, pero sin taxid.</summary>
        private static readonly KeyValuePair<string, string?>[] MaestroSinTaxId =
            { Val("sanic_taxid", null) };

        private static readonly KeyValuePair<string, string?>[] AltaCompleta =
        {
            Val("sanic_name", "Distribuidora El Porvenir, S.A."),
            Val("sanic_cifbco", "0012024099"),
            Val("sanic_cifcom", "C-00099"),
            Val("sanic_taxid", "J0310000012345"),
        };

        private static readonly KeyValuePair<string, string?>[] SoloCambiaNombre =
            { Val("sanic_name", "Razon social nueva, S.A.") };

        // --- Create ---

        [Fact]
        public void Create_ConTaxId_NoFaltaNada()
        {
            Assert.Empty(MissingRequiredFields(TypeCreate, ClientTable, AltaCompleta, null));
        }

        [Fact]
        public void Create_SinLineaTaxId_ReportaTaxId()
        {
            var sinTaxId = AltaCompleta.Where(l => l.Key != "sanic_taxid").ToArray();

            Assert.Equal(new[] { "sanic_taxid" }, MissingRequiredFields(TypeCreate, ClientTable, sinTaxId, null));
        }

        [Fact]
        public void Create_NoSeSatisfaceConElMaestro()
        {
            // En un alta no hay maestro previo; aunque llegara algo, la linea manda.
            var sinTaxId = AltaCompleta.Where(l => l.Key != "sanic_taxid").ToArray();

            Assert.Equal(
                new[] { "sanic_taxid" },
                MissingRequiredFields(TypeCreate, ClientTable, sinTaxId, MaestroConTaxId));
        }

        // --- Update: el corazon del cambio ---

        [Fact]
        public void Update_ClienteQueYaTieneTaxId_SoloCambiaNombre_Pasa()
        {
            // NO se le puede exigir repetir el taxid: el panel de edicion filtra los valores
            // que no cambiaron, asi que esa linea jamas se crearia.
            Assert.Empty(MissingRequiredFields(TypeUpdate, ClientTable, SoloCambiaNombre, MaestroConTaxId));
        }

        [Fact]
        public void Update_ClienteSinTaxId_SoloCambiaNombre_EsBloqueado()
        {
            // Uno de los ~4999 historicos: no se lo puede editar sin completarle el taxid.
            Assert.Equal(
                new[] { "sanic_taxid" },
                MissingRequiredFields(TypeUpdate, ClientTable, SoloCambiaNombre, MaestroSinTaxId));
        }

        [Fact]
        public void Update_ClienteSinTaxId_QueLoCompletaEnLaMismaSolicitud_Pasa()
        {
            // El camino de backfill: nombre + taxid en el mismo RFC.
            var conBackfill = SoloCambiaNombre.Append(Val("sanic_taxid", "J0310000099999")).ToArray();

            Assert.Empty(MissingRequiredFields(TypeUpdate, ClientTable, conBackfill, MaestroSinTaxId));
        }

        [Fact]
        public void Update_SinMaestro_SeComportaComoSiNoLoTuviera()
        {
            Assert.Equal(
                new[] { "sanic_taxid" },
                MissingRequiredFields(TypeUpdate, ClientTable, SoloCambiaNombre, null));
        }

        // --- Blanqueo explicito: nunca permitido, tenga o no el maestro ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void Update_LineaTaxIdEnBlanco_EsBloqueada_AunqueElMaestroLoTenga(string? blanco)
        {
            // Una linea explicita en blanco pisaria con vacio un taxid bueno.
            var blanqueo = SoloCambiaNombre.Append(Val("sanic_taxid", blanco)).ToArray();

            Assert.Equal(
                new[] { "sanic_taxid" },
                MissingRequiredFields(TypeUpdate, ClientTable, blanqueo, MaestroConTaxId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_LineaTaxIdEnBlanco_EsBloqueada(string? blanco)
        {
            var conBlanco = AltaCompleta
                .Where(l => l.Key != "sanic_taxid")
                .Append(Val("sanic_taxid", blanco))
                .ToArray();

            Assert.Equal(new[] { "sanic_taxid" }, MissingRequiredFields(TypeCreate, ClientTable, conBlanco, null));
        }

        [Theory]
        [InlineData("   ")]
        [InlineData("")]
        public void Update_MaestroConTaxIdEnBlanco_NoSatisface(string maestroBlanco)
        {
            var maestro = new[] { Val("sanic_taxid", maestroBlanco) };

            Assert.Equal(
                new[] { "sanic_taxid" },
                MissingRequiredFields(TypeUpdate, ClientTable, SoloCambiaNombre, maestro));
        }

        // --- Alcance: tipos y tablas ---

        [Theory]
        [InlineData(TypeActivate)]
        [InlineData(TypeDeactivate)]
        public void ActivateYDeactivate_NuncaExigenTaxId(int type)
        {
            // Solo mueven sanic_recordstatus; no escriben campos. Bloquear la baja de un
            // cliente historico por no tener taxid dejaria basura imposible de desactivar.
            Assert.Empty(MissingRequiredFields(type, ClientTable, null, MaestroSinTaxId));
        }

        [Theory]
        [InlineData(TypeCreate)]
        [InlineData(TypeUpdate)]
        public void OtrasTablas_NoTienenRequisito(int type)
        {
            Assert.Empty(MissingRequiredFields(type, BranchTable, new[] { Val("sanic_name", "Sucursal Centro") }, null));
        }

        [Fact]
        public void TablaNulaODesconocida_NoRevienta()
        {
            Assert.Empty(MissingRequiredFields(TypeCreate, null, AltaCompleta, null));
            Assert.Empty(MissingRequiredFields(TypeUpdate, "tabla_inexistente", SoloCambiaNombre, null));
        }

        [Fact]
        public void EsCaseInsensitive_EnTabla_LineasYMaestro()
        {
            Assert.Empty(MissingRequiredFields(
                TypeCreate, "SANIC_CHTBLCLIENT", new[] { Val("SANIC_TaxId", "J031") }, null));

            Assert.Empty(MissingRequiredFields(
                TypeUpdate, "Sanic_ChTblClient", SoloCambiaNombre, new[] { Val("SANIC_TAXID", "J031") }));
        }

        // --- Contrato que consume el plugin para saber que columnas traer del maestro ---

        [Fact]
        public void RequiredFieldsFor_DevuelveLasColumnasAConsultar()
        {
            Assert.Equal(new[] { "sanic_taxid" }, RequiredFieldsFor(ClientTable));
            Assert.Equal(new[] { "sanic_taxid" }, RequiredFieldsFor("SANIC_CHTBLCLIENT"));
            Assert.Empty(RequiredFieldsFor(BranchTable));
            Assert.Empty(RequiredFieldsFor(null));
        }
    }
}
