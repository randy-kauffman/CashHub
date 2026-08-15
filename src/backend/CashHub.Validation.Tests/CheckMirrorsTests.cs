using System;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Espejos de cheques del tracker: B6 (tula estampada en el cheque fisico),
    /// B3 (MatchedCheck en el cheque declarado cuando el fisico lo referencia) y
    /// B-9 (sesion de recuento del cheque, con su cascada desde la tula).
    /// </summary>
    public class CheckMirrorsTests
    {
        private static readonly Guid Cheque = Guid.NewGuid();
        private static readonly Guid DeclaradoViejo = Guid.NewGuid();
        private static readonly Guid DeclaradoNuevo = Guid.NewGuid();
        private static readonly Guid SesionVieja = Guid.NewGuid();
        private static readonly Guid SesionNueva = Guid.NewGuid();

        // ---------- B6: re-estampa de la tula ----------

        [Fact]
        public void ShouldRestampBag_EnCreate_SiempreTrue()
        {
            Assert.True(CheckMirrors.ShouldRestampBag("Create", depositTouched: false));
        }

        [Fact]
        public void ShouldRestampBag_EnUpdate_SoloSiElDepositoCambio()
        {
            Assert.True(CheckMirrors.ShouldRestampBag("Update", depositTouched: true));
            Assert.False(CheckMirrors.ShouldRestampBag("Update", depositTouched: false));
        }

        // ---------- B3: instrucciones de MatchedCheck ----------

        [Fact]
        public void Create_ConDeclarado_EstampaElMatch()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Create", Cheque, declaredTouched: true,
                targetDeclared: DeclaradoNuevo, preDeclared: null);

            var u = Assert.Single(updates);
            Assert.Equal(DeclaradoNuevo, u.DeclaredCheckId);
            Assert.Equal(Cheque, u.MatchedCheckId);
        }

        [Fact]
        public void Create_SinDeclarado_NoHaceNada()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Create", Cheque, declaredTouched: false,
                targetDeclared: null, preDeclared: null);

            Assert.Empty(updates);
        }

        [Fact]
        public void Update_SinTocarElDeclarado_NoHaceNada()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Update", Cheque, declaredTouched: false,
                targetDeclared: null, preDeclared: DeclaradoViejo);

            Assert.Empty(updates);
        }

        [Fact]
        public void Update_VinculaPorPrimeraVez_EstampaElNuevo()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Update", Cheque, declaredTouched: true,
                targetDeclared: DeclaradoNuevo, preDeclared: null);

            var u = Assert.Single(updates);
            Assert.Equal(DeclaradoNuevo, u.DeclaredCheckId);
            Assert.Equal(Cheque, u.MatchedCheckId);
        }

        [Fact]
        public void Update_CambiaDeDeclarado_LimpiaElViejoYEstampaElNuevo()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Update", Cheque, declaredTouched: true,
                targetDeclared: DeclaradoNuevo, preDeclared: DeclaradoViejo);

            Assert.Equal(2, updates.Count);
            var clear = updates.Single(u => u.DeclaredCheckId == DeclaradoViejo);
            Assert.Null(clear.MatchedCheckId);
            var stamp = updates.Single(u => u.DeclaredCheckId == DeclaradoNuevo);
            Assert.Equal(Cheque, stamp.MatchedCheckId);
        }

        [Fact]
        public void Update_Desvincula_SoloLimpiaElViejo()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Update", Cheque, declaredTouched: true,
                targetDeclared: null, preDeclared: DeclaradoViejo);

            var u = Assert.Single(updates);
            Assert.Equal(DeclaradoViejo, u.DeclaredCheckId);
            Assert.Null(u.MatchedCheckId);
        }

        [Fact]
        public void Update_MismoDeclarado_NoHaceNada()
        {
            var updates = CheckMirrors.ResolveMatchUpdates(
                "Update", Cheque, declaredTouched: true,
                targetDeclared: DeclaradoViejo, preDeclared: DeclaradoViejo);

            Assert.Empty(updates);
        }

        // ---------- B-9: cascada de la sesion desde la TULA hacia sus cheques ----------

        [Fact]
        public void CascadaSesion_LaTulaSeReAsigna_PropagaLaSesionNueva()
        {
            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched: true, newSessionId: SesionNueva, previousSessionId: SesionVieja);

            Assert.True(plan.Propagate);
            Assert.Equal(SesionNueva, plan.SessionId);
        }

        [Fact]
        public void CascadaSesion_TulaSinSesionPorPrimeraVez_PropagaLaSesion()
        {
            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched: true, newSessionId: SesionNueva, previousSessionId: null);

            Assert.True(plan.Propagate);
            Assert.Equal(SesionNueva, plan.SessionId);
        }

        [Fact]
        public void CascadaSesion_TulaDesvinculadaDeLaSesion_PropagaLaLimpieza()
        {
            // Dejar el espejo viejo seria peor que no tenerlo: los CountRows por sesion
            // de scrResumen contarian cheques que ya no pertenecen a esa sesion.
            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched: true, newSessionId: null, previousSessionId: SesionVieja);

            Assert.True(plan.Propagate);
            Assert.Null(plan.SessionId);
        }

        [Fact]
        public void CascadaSesion_SinCambioReal_NoTocaNingunCheque()
        {
            // Dataverse dispara un paso filtrado aunque se reescriba el MISMO valor.
            // Sin esta guarda, guardar la tula sin cambiarla reescribe N cheques.
            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched: true, newSessionId: SesionVieja, previousSessionId: SesionVieja);

            Assert.False(plan.Propagate);
        }

        [Fact]
        public void CascadaSesion_ColumnaNoTocada_NoPropagaAunqueDifiera()
        {
            // Si la columna no vino en el Target, no cambio: el valor efectivo es el de
            // la PreImage. Propagar aca seria inventar un cambio que no ocurrio.
            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched: false, newSessionId: null, previousSessionId: SesionVieja);

            Assert.False(plan.Propagate);
        }
    }
}
