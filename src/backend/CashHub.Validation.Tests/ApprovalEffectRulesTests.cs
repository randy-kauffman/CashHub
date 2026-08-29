using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ApprovalEffectRules;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Reglas puras que gobiernan la materializacion de una solicitud de aprobacion.
    /// Salen de la app del supervisor (scrAprobaciones), donde vivian como Power Fx
    /// sin test: es exactamente donde se escondia el dup-check roto de "Modificar
    /// numero de planilla" (comparaba el LOTE de cada planilla contra el ID de LA
    /// planilla — dos tablas distintas, jamas coincidian).
    /// </summary>
    public class ApprovalEffectRulesTests
    {
        // =====================================================================
        // 1. Estado previo de la tula (BagPreviousState del RequestResources)
        //
        //    EL PRODUCTOR GUARDA LA ETIQUETA, NO EL CODIGO. Verificado contra el
        //    entorno: de 2000 solicitudes, las 107 que traen BagPreviousState
        //    guardan 'Recibida en esclusa' (99) o 'Almacenada en boveda' (8).
        //    Cero codigos numericos. La primera version de esta regla aceptaba
        //    SOLO el codigo — con eso RestituirTula jamas habria restituido nada:
        //    todos los rechazos de "Eliminar tula" y "Remapear tula" habrian caido
        //    en la guarda y dejado la tula donde estaba.
        //
        //    El codigo se sigue aceptando a proposito: no es ambiguo contra
        //    ninguna etiqueta, y deja migrar el productor sin romper lo viejo.
        //
        //    Si no resuelve, el llamador NO escribe BagStatus y avisa — esa guarda
        //    es una regla, no un detalle: estamparle un estado inventado a una tula
        //    es peor que dejarla como esta y avisar.
        // =====================================================================

        [Theory]
        [InlineData("Recibida en esclusa", ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData("Almacenada en boveda", ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData("Asignada a cajero", ReceptionGuard.BagAsignadaACajero)]
        [InlineData("Recontada", ReceptionGuard.BagRecontada)]
        [InlineData("Pendiente de revision por supervisor - recuento", ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData("Pendiente de revision por supervisor - esclusa", ReceptionGuard.BagPendienteSupervisorEsclusa)]
        public void EstadoPrevio_EtiquetaValida_Resuelve(string raw, int esperado)
        {
            // Las 6 etiquetas salen del option set sanic_bagstatus (lang 1033),
            // leidas de la metadata del entorno. Sin acentos, tal cual estan.
            Assert.True(TryResolveBagPreviousState(raw, out var status));
            Assert.Equal(esperado, status);
        }

        [Theory]
        [InlineData("159460000", ReceptionGuard.BagRecibidaEnEsclusa)]
        [InlineData("159460001", ReceptionGuard.BagAlmacenadaEnBoveda)]
        [InlineData("159460002", ReceptionGuard.BagAsignadaACajero)]
        [InlineData("159460003", ReceptionGuard.BagRecontada)]
        [InlineData("159460004", ReceptionGuard.BagPendienteSupervisorRecuento)]
        [InlineData("159460005", ReceptionGuard.BagPendienteSupervisorEsclusa)]
        public void EstadoPrevio_CodigoValido_TambienResuelve(string raw, int esperado)
        {
            Assert.True(TryResolveBagPreviousState(raw, out var status));
            Assert.Equal(esperado, status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("159460099")]              // codigo inexistente
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("159460001.5")]
        [InlineData("Almacenada en bodega")]   // etiqueta parecida pero inexistente
        [InlineData("En boveda")]              // fragmento, no la etiqueta
        public void EstadoPrevio_Invalido_NoResuelve(string? raw)
        {
            Assert.False(TryResolveBagPreviousState(raw, out var status));
            Assert.Equal(0, status);
        }

        [Theory]
        [InlineData("  159460001  ")]
        [InlineData("  Almacenada en boveda  ")]
        [InlineData("almacenada en boveda")]
        [InlineData("ALMACENADA EN BOVEDA")]
        public void EstadoPrevio_ToleraEspaciosYMayusculas(string raw)
        {
            // El JSON lo escribe una canvas app; un espacio o una mayuscula no
            // pueden costar que la tula quede en un estado equivocado.
            Assert.True(TryResolveBagPreviousState(raw, out var status));
            Assert.Equal(ReceptionGuard.BagAlmacenadaEnBoveda, status);
        }

        // =====================================================================
        // 2. Dup-check de numero dentro de un alcance (lote)
        //
        //    ACA ESTABA EL BUG. La regla es: colisiona si OTRO registro del mismo
        //    alcance ya usa ese numero. "Otro" es la parte que se rompio: hay que
        //    excluirse a si mismo, si no renombrar A -> A da falso positivo.
        // =====================================================================

        private static readonly Guid Yo = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Otro = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Tercero = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static IEnumerable<(Guid, string)> Alcance(params (Guid, string)[] filas) => filas;

        [Fact]
        public void DupCheck_OtroYaUsaElNumero_Colisiona()
        {
            Assert.True(HasNumberCollision(
                Alcance((Otro, "R-002")), "R-002", Yo));
        }

        [Fact]
        public void DupCheck_NadieMasLoUsa_NoColisiona()
        {
            Assert.False(HasNumberCollision(
                Alcance((Otro, "R-001"), (Tercero, "R-003")), "R-002", Yo));
        }

        [Fact]
        public void DupCheck_SoloYoLoUso_NoColisiona()
        {
            // Renombrar un registro a su propio numero (o reenviar la solicitud) no
            // es un duplicado. Sin la exclusion de si mismo, esto daba falso positivo.
            Assert.False(HasNumberCollision(
                Alcance((Yo, "R-002")), "R-002", Yo));
        }

        [Fact]
        public void DupCheck_AlcanceVacio_NoColisiona()
        {
            Assert.False(HasNumberCollision(Alcance(), "R-002", Yo));
            Assert.False(HasNumberCollision(null, "R-002", Yo));
        }

        [Theory]
        [InlineData("r-002")]
        [InlineData("R-002 ")]
        [InlineData(" R-002")]
        public void DupCheck_IgnoraMayusculasYEspacios(string existente)
        {
            // Dos tulas "R-002" y "r-002" en el mismo lote son la misma para un
            // humano en la esclusa. Dejarlas pasar es el duplicado que se quiere evitar.
            Assert.True(HasNumberCollision(
                Alcance((Otro, existente)), "R-002", Yo));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DupCheck_NumeroNuevoVacio_NoColisiona(string? nuevo)
        {
            // Un numero vacio no es un duplicado: es otro problema, y lo ataja
            // la validacion del contrato, no esta regla.
            Assert.False(HasNumberCollision(Alcance((Otro, "R-002")), nuevo, Yo));
        }

        [Fact]
        public void DupCheck_FilasSinNumero_SeIgnoran()
        {
            Assert.False(HasNumberCollision(
                Alcance((Otro, null!), (Tercero, "  ")), "R-002", Yo));
        }

        // =====================================================================
        // 3. Cobertura: los 10 tipos conocidos tienen efecto declarado en AMBAS
        //    decisiones. En la app el default era un Notify de "tipo no soportado"
        //    que dejaba la solicitud resuelta y el efecto sin ejecutar — un exito
        //    disfrazado. Aca eso tiene que ser imposible por construccion.
        // =====================================================================

        [Fact]
        public void TodosLosTiposConocidos_TienenEfectoDeclarado_EnAmbasDecisiones()
        {
            var faltantes = new List<string>();

            foreach (var tipo in ApprovalRequestResources.KnownTypes)
            {
                if (!TryGetEffect(tipo, approve: true, out _)) faltantes.Add($"{tipo} (aprobar)");
                if (!TryGetEffect(tipo, approve: false, out _)) faltantes.Add($"{tipo} (rechazar)");
            }

            Assert.Empty(faltantes);
        }

        [Fact]
        public void TipoDesconocido_NoTieneEfecto()
        {
            Assert.False(TryGetEffect("Tipo inventado", approve: true, out _));
            Assert.False(TryGetEffect(null, approve: true, out _));
        }

        [Fact]
        public void ModificarNumeroDePlanilla_AlRechazar_NoTieneNadaQueRevertir()
        {
            Assert.True(TryGetEffect("Modificar numero de planilla", approve: false, out var efecto));
            Assert.Equal(ApprovalEffect.Ninguno, efecto);
        }

        [Fact]
        public void CrearSesionDeRecuento_MapeaConfirmarYEliminar()
        {
            Assert.True(TryGetEffect("Crear sesion de recuento", approve: true, out var ok));
            Assert.Equal(ApprovalEffect.ConfirmarSesion, ok);

            Assert.True(TryGetEffect("Crear sesion de recuento", approve: false, out var no));
            Assert.Equal(ApprovalEffect.EliminarSesion, no);
        }

        [Fact]
        public void EditarTula_AlRechazar_CaeABovedaPorqueSuContratoNoTraeEstadoPrevio()
        {
            // Es el unico tipo cuyo rechazo NO restituye el estado previo: su contrato
            // (ApprovalRequestResources) no declara BagPreviousState, asi que el
            // fallback seguro es bóveda. Que sea explicito y no un descuido.
            Assert.DoesNotContain("BagPreviousState", ApprovalRequestResources.ContractKeys("Editar tula"));

            Assert.True(TryGetEffect("Editar tula", approve: false, out var efecto));
            Assert.Equal(ApprovalEffect.RestituirTulaOBoveda, efecto);
        }

        [Fact]
        public void LosTiposQueRestituyenEstadoPrevio_LoDeclaranEnSuContrato()
        {
            // Candado de coherencia: si un efecto restituye BagPreviousState, el
            // contrato del tipo tiene que pedir esa clave. Si no, el efecto nunca
            // podria resolverla y siempre caeria en la guarda.
            foreach (var tipo in ApprovalRequestResources.KnownTypes)
            {
                if (TryGetEffect(tipo, approve: false, out var efecto)
                    && efecto == ApprovalEffect.RestituirTula)
                {
                    Assert.Contains("BagPreviousState", ApprovalRequestResources.ContractKeys(tipo));
                }
            }
        }
    }
}
