using System;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Espejos server-side de CH-App-Recounting v2 (01 §2.2):
    ///   E-1  sanic_recountingsessionmirror en el deposito de recuento
    ///        (deposito -> tula -> sesion), con su cascada desde la TULA.
    ///   E-3  sanic_declaredbagmirror en el cheque declarado
    ///        (cheque declarado -> deposito declarado -> bolsa declarada).
    ///   E-4  sanic_recountingsessiondetailmirror + sanic_xmlreadingactiva en el
    ///        detalle de lectura, con su cascada desde la LECTURA (las DOS columnas).
    ///
    /// Todo lo que se prueba aca es DECISION pura: cuando re-estampar y que
    /// propagar. El acceso a Dataverse vive en los tres plugins.
    /// </summary>
    public class RecountingMirrorsTests
    {
        private static readonly Guid SesionVieja = Guid.NewGuid();
        private static readonly Guid SesionNueva = Guid.NewGuid();
        private static readonly Guid DetalleViejo = Guid.NewGuid();
        private static readonly Guid DetalleNuevo = Guid.NewGuid();

        // ---------- Disparador de estampa (lado HIJO): E-1, E-3 y E-4 ----------

        [Fact]
        public void ShouldStamp_EnCreate_SiempreTrue()
        {
            // Aunque el lookup origen venga vacio: el espejo se deja en blanco
            // EXPLICITO, nunca heredado de un default.
            Assert.True(RecountingMirrors.ShouldStamp("Create", sourceTouched: false));
            Assert.True(RecountingMirrors.ShouldStamp("Create", sourceTouched: true));
        }

        [Fact]
        public void ShouldStamp_EnCreate_NoDependeDeMayusculas()
        {
            Assert.True(RecountingMirrors.ShouldStamp("create", sourceTouched: false));
            Assert.True(RecountingMirrors.ShouldStamp("CREATE", sourceTouched: false));
        }

        [Fact]
        public void ShouldStamp_EnUpdate_SoloSiElOrigenVinoEnElTarget()
        {
            Assert.True(RecountingMirrors.ShouldStamp("Update", sourceTouched: true));
            Assert.False(RecountingMirrors.ShouldStamp("Update", sourceTouched: false));
        }

        [Fact]
        public void ShouldStamp_MensajeNulo_SoloSiElOrigenVinoEnElTarget()
        {
            Assert.False(RecountingMirrors.ShouldStamp(null, sourceTouched: false));
            Assert.True(RecountingMirrors.ShouldStamp(null, sourceTouched: true));
        }

        // ---------- E-1: cascada de la sesion desde la TULA a sus depositos ----------

        [Fact]
        public void CascadaLookup_LaTulaSeReAsignaDeSesion_PropagaLaNueva()
        {
            var plan = RecountingMirrors.ResolveLookupCascade(
                touched: true, newId: SesionNueva, previousId: SesionVieja);

            Assert.True(plan.Propagate);
            Assert.Equal(SesionNueva, plan.ValueId);
        }

        [Fact]
        public void CascadaLookup_TulaSinSesionPorPrimeraVez_Propaga()
        {
            var plan = RecountingMirrors.ResolveLookupCascade(
                touched: true, newId: SesionNueva, previousId: null);

            Assert.True(plan.Propagate);
            Assert.Equal(SesionNueva, plan.ValueId);
        }

        [Fact]
        public void CascadaLookup_TulaDesvinculada_PropagaLaLimpieza()
        {
            // Dejar el espejo viejo seria peor que no tenerlo: los Sum por sesion de
            // scrTulas sumarian dinero de depositos que ya no son de esa sesion.
            var plan = RecountingMirrors.ResolveLookupCascade(
                touched: true, newId: null, previousId: SesionVieja);

            Assert.True(plan.Propagate);
            Assert.Null(plan.ValueId);
        }

        [Fact]
        public void CascadaLookup_SinCambioReal_NoTocaNingunHijo()
        {
            // Dataverse dispara un paso FILTRADO aunque se reescriba el MISMO valor.
            var plan = RecountingMirrors.ResolveLookupCascade(
                touched: true, newId: SesionVieja, previousId: SesionVieja);

            Assert.False(plan.Propagate);
        }

        [Fact]
        public void CascadaLookup_ColumnaNoTocada_NoPropagaAunqueDifiera()
        {
            // Si no vino en el Target, no cambio: el valor efectivo es el de la
            // PreImage y propagar seria inventar un cambio que no ocurrio.
            var plan = RecountingMirrors.ResolveLookupCascade(
                touched: false, newId: null, previousId: SesionVieja);

            Assert.False(plan.Propagate);
        }

        // ---------- E-4: cascada de la LECTURA a sus detalles (dos columnas) ----------

        [Fact]
        public void CascadaLectura_CambiaLaSesionDetalle_PropagaAmbasColumnas()
        {
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: true, newSessionDetailId: DetalleNuevo,
                previousSessionDetailId: DetalleViejo,
                activeTouched: false, newActive: null, previousActive: true);

            Assert.True(plan.Propagate);
            Assert.Equal(DetalleNuevo, plan.SessionDetailId);
            // IsActive no vino en el Target: el valor efectivo es el de la PreImage.
            Assert.True(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_LaLecturaSeDesactiva_PropagaElBitYMantieneLaSesionDetalle()
        {
            // ESTE es el caso critico del contrato: una lectura vieja se apaga cuando
            // llega la nueva. Sin propagar, Q17 sigue sumando la lectura muerta.
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: false, newSessionDetailId: null,
                previousSessionDetailId: DetalleViejo,
                activeTouched: true, newActive: false, previousActive: true);

            Assert.True(plan.Propagate);
            Assert.Equal(DetalleViejo, plan.SessionDetailId);
            Assert.False(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_LaLecturaSeActiva_Propaga()
        {
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: false, newSessionDetailId: null,
                previousSessionDetailId: DetalleViejo,
                activeTouched: true, newActive: true, previousActive: false);

            Assert.True(plan.Propagate);
            Assert.Equal(DetalleViejo, plan.SessionDetailId);
            Assert.True(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_ActivaDesdeEnBlanco_Propaga()
        {
            // IsActive nulo (nunca escrito) -> true es un cambio real.
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: false, newSessionDetailId: null,
                previousSessionDetailId: DetalleViejo,
                activeTouched: true, newActive: true, previousActive: null);

            Assert.True(plan.Propagate);
            Assert.True(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_CambianLasDos_PropagaLasDos()
        {
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: true, newSessionDetailId: DetalleNuevo,
                previousSessionDetailId: DetalleViejo,
                activeTouched: true, newActive: false, previousActive: true);

            Assert.True(plan.Propagate);
            Assert.Equal(DetalleNuevo, plan.SessionDetailId);
            Assert.False(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_LecturaDesvinculadaDelDetalle_PropagaLaLimpieza()
        {
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: true, newSessionDetailId: null,
                previousSessionDetailId: DetalleViejo,
                activeTouched: false, newActive: null, previousActive: false);

            Assert.True(plan.Propagate);
            Assert.Null(plan.SessionDetailId);
            Assert.False(plan.IsActive);
        }

        [Fact]
        public void CascadaLectura_NingunaColumnaTocada_NoPropaga()
        {
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: false, newSessionDetailId: null,
                previousSessionDetailId: DetalleViejo,
                activeTouched: false, newActive: null, previousActive: true);

            Assert.False(plan.Propagate);
        }

        [Fact]
        public void CascadaLectura_TocadasPeroSinCambioReal_NoPropaga()
        {
            // Idempotencia: re-guardar la lectura con los MISMOS valores no puede
            // reescribir N detalles ni ensuciar su auditoria.
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: true, newSessionDetailId: DetalleViejo,
                previousSessionDetailId: DetalleViejo,
                activeTouched: true, newActive: true, previousActive: true);

            Assert.False(plan.Propagate);
        }

        [Fact]
        public void CascadaLectura_SinPreImage_NoPropagaPorqueNoSeSabeSiCambio()
        {
            // Modelado como "nada tocado": el plugin ya corta antes si falta la
            // PreImage; esta prueba fija la semantica de la decision pura.
            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched: false, newSessionDetailId: null,
                previousSessionDetailId: null,
                activeTouched: false, newActive: null, previousActive: null);

            Assert.False(plan.Propagate);
        }
    }
}
