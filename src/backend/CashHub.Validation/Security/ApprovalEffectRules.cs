using System;
using System.Collections.Generic;
using System.Globalization;

namespace CashHub.Validation.Security
{
    /// <summary>Que le pasa al mundo cuando el supervisor resuelve una solicitud.</summary>
    public enum ApprovalEffect
    {
        /// <summary>La decision no tiene efecto material (nada que revertir).</summary>
        Ninguno,

        /// <summary>Mover la tula a la planilla destino y restituir su estado previo.</summary>
        RemapearTula,

        /// <summary>Materializar la tula pendiente en boveda (con dup-check de numero).</summary>
        AltaTulaEnBoveda,

        /// <summary>Borrar la tula.</summary>
        EliminarTula,

        /// <summary>Renombrar la planilla (con dup-check dentro del lote).</summary>
        RenombrarPlanilla,

        /// <summary>Renombrar la tula y devolverla a boveda (con dup-check dentro del lote).</summary>
        RenombrarTula,

        /// <summary>Confirmar la sesion de recuento.</summary>
        ConfirmarSesion,

        /// <summary>Eliminar la sesion de recuento.</summary>
        EliminarSesion,

        /// <summary>Asignar la tula al cajero y enlazarla con su bolsa declarada.</summary>
        AsignarTulaConDeclaracion,

        /// <summary>Asignar la tula al cajero y abrir su detalle de sesion.</summary>
        AsignarTulaSinDeclaracion,

        /// <summary>Devolver la tula a boveda y desenlazarla de la sesion.</summary>
        LiberarTulaABoveda,

        /// <summary>Restituir el estado previo declarado en el contrato.</summary>
        RestituirTula,

        /// <summary>
        /// Restituir el estado previo si el contrato lo trae; si no, boveda con aviso.
        /// Solo lo usa "Editar tula", cuyo contrato no declara BagPreviousState.
        /// </summary>
        RestituirTulaOBoveda,

        /// <summary>Cerrar el recuento generando los asientos (Fase 2).</summary>
        CerrarRecuentoConAsientos,

        /// <summary>Devolver la tula al cajero para que rehaga el recuento.</summary>
        DevolverTulaAlCajero,
    }

    /// <summary>
    /// Reglas puras de la materializacion de solicitudes de aprobacion. Salen de
    /// scrAprobaciones de CH-App-Supervisor, donde vivian como Power Fx sin test.
    ///
    /// No tocan Dataverse: reciben lo ya leido y devuelven una decision. El plugin
    /// hace la E/S; aca vive lo que se puede razonar y fijar con un test.
    /// </summary>
    public static class ApprovalEffectRules
    {
        /// <summary>
        /// Etiqueta del option set <c>sanic_bagstatus</c> (lang 1033) -> codigo.
        ///
        /// Escritas tal cual estan en la metadata del entorno, SIN acentos
        /// ("boveda", no "bóveda"). Si alguien renombra una opcion en Dataverse,
        /// esta tabla deja de reconocerla y el rechazo cae en la guarda: la tula
        /// no se toca y se avisa. Ruidoso, pero nunca incorrecto.
        /// </summary>
        private static readonly Dictionary<string, int> BagStatusByLabel =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Recibida en esclusa"] = ReceptionGuard.BagRecibidaEnEsclusa,
                ["Almacenada en boveda"] = ReceptionGuard.BagAlmacenadaEnBoveda,
                ["Asignada a cajero"] = ReceptionGuard.BagAsignadaACajero,
                ["Recontada"] = ReceptionGuard.BagRecontada,
                ["Pendiente de revision por supervisor - recuento"] = ReceptionGuard.BagPendienteSupervisorRecuento,
                ["Pendiente de revision por supervisor - esclusa"] = ReceptionGuard.BagPendienteSupervisorEsclusa,
            };

        /// <summary>
        /// Resuelve <c>BagPreviousState</c> del RequestResources.
        ///
        /// EL PRODUCTOR GUARDA LA ETIQUETA, NO EL CODIGO. Verificado contra el
        /// entorno: de 2000 solicitudes, las 107 que traen la clave guardan
        /// 'Recibida en esclusa' o 'Almacenada en boveda'. Cero codigos numericos.
        /// El codigo se acepta igual (no colisiona con ninguna etiqueta) para poder
        /// migrar el productor sin romper lo que ya esta escrito.
        ///
        /// Devuelve false ante cualquier duda. El llamador NO debe escribir BagStatus
        /// en ese caso: dejar la tula como esta y avisar es mejor que estamparle un
        /// estado inventado.
        /// </summary>
        public static bool TryResolveBagPreviousState(string? raw, out int status)
        {
            status = 0;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var trimmed = raw!.Trim();

            if (BagStatusByLabel.TryGetValue(trimmed, out var byLabel))
            {
                status = byLabel;
                return true;
            }

            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            foreach (var known in BagStatusByLabel.Values)
            {
                if (known == parsed)
                {
                    status = parsed;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Hay colision de numero dentro del alcance (el lote)?
        ///
        /// ACA ESTABA EL BUG que se corrige al portar: la version Power Fx de
        /// "Modificar numero de planilla" comparaba el LOTE de cada planilla contra
        /// el ID de LA planilla — identificadores de tablas distintas, jamas
        /// coincidian, y el dup-check no disparaba nunca. Ademas no se excluia a si
        /// misma, cosa que las variantes de tula si hacian.
        /// </summary>
        /// <param name="scope">Pares (id, numero) de los registros del mismo alcance.</param>
        /// <param name="newNumber">Numero propuesto.</param>
        /// <param name="selfId">Id del registro que se esta renombrando; se excluye.</param>
        public static bool HasNumberCollision(
            IEnumerable<(Guid Id, string? Number)>? scope, string? newNumber, Guid selfId)
        {
            if (scope == null || string.IsNullOrWhiteSpace(newNumber))
            {
                return false;
            }

            var target = newNumber!.Trim();

            foreach (var row in scope)
            {
                if (row.Id == selfId || string.IsNullOrWhiteSpace(row.Number))
                {
                    continue;
                }

                // Comparacion laxa a proposito: "R-002" y "r-002 " son el mismo numero
                // para quien trabaja en la esclusa, y dejarlos convivir es justo el
                // duplicado que se quiere evitar.
                if (string.Equals(row.Number!.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Cierre de recuento CON declaracion contra la cual comparar.</summary>
        public const string TipoTulaConDiferencia = "Tula con diferencia";

        /// <summary>Cierre de recuento SIN declaracion: no hay contra que comparar.</summary>
        public const string TipoTulaRecontadaSinDeclaracion = "Tula recontada sin declaracion";

        // (tipo, aprobar) -> efecto. Sin default: un tipo sin entrada NO se resuelve,
        // en vez del Notify de "tipo no soportado" que dejaba la solicitud marcada y
        // el efecto sin correr (exito disfrazado).
        private static readonly Dictionary<string, (ApprovalEffect Approve, ApprovalEffect Reject)> EffectByType =
            new Dictionary<string, (ApprovalEffect, ApprovalEffect)>(StringComparer.Ordinal)
            {
                ["Remapear tula"] = (ApprovalEffect.RemapearTula, ApprovalEffect.RestituirTula),
                ["Agregar tula"] = (ApprovalEffect.AltaTulaEnBoveda, ApprovalEffect.EliminarTula),
                ["Eliminar tula"] = (ApprovalEffect.EliminarTula, ApprovalEffect.RestituirTula),
                ["Modificar numero de planilla"] = (ApprovalEffect.RenombrarPlanilla, ApprovalEffect.Ninguno),
                ["Editar tula"] = (ApprovalEffect.RenombrarTula, ApprovalEffect.RestituirTulaOBoveda),
                ["Crear sesion de recuento"] = (ApprovalEffect.ConfirmarSesion, ApprovalEffect.EliminarSesion),
                ["Primera tula en planilla"] = (ApprovalEffect.AsignarTulaConDeclaracion, ApprovalEffect.LiberarTulaABoveda),
                // Rechazo no-op DELIBERADO: el productor no pre-patchea la tula al crear
                // esta solicitud, asi que no hay estado que restituir — y su contrato,
                // coherentemente, no declara BagPreviousState. La tula sigue disponible.
                ["Tula sin declaracion"] = (ApprovalEffect.AsignarTulaSinDeclaracion, ApprovalEffect.Ninguno),
                [TipoTulaConDiferencia] = (ApprovalEffect.CerrarRecuentoConAsientos, ApprovalEffect.DevolverTulaAlCajero),
                [TipoTulaRecontadaSinDeclaracion] = (ApprovalEffect.CerrarRecuentoConAsientos, ApprovalEffect.DevolverTulaAlCajero),
            };

        /// <summary>Efecto declarado para el tipo y la decision. false si el tipo no es conocido.</summary>
        public static bool TryGetEffect(string? requestType, bool approve, out ApprovalEffect effect)
        {
            effect = ApprovalEffect.Ninguno;

            if (requestType == null || !EffectByType.TryGetValue(requestType, out var pair))
            {
                return false;
            }

            effect = approve ? pair.Approve : pair.Reject;
            return true;
        }
    }
}
