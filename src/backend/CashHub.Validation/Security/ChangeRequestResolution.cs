using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>Decision humana que dispara la Custom API sanic_CHResolveChangeRequest.</summary>
    public enum ChangeRequestDecision
    {
        Approve,
        Reject
    }

    /// <summary>
    /// Resultado de resolver un RFC: si es legitimo, trae los estados FINALES de cabecera
    /// y linea a escribir en la misma transaccion (sin el estado intermedio Approved que
    /// usaban los 4 flujos — aca la materializacion es sincrona, no hay ventana).
    /// </summary>
    public sealed class ChangeRequestResolutionOutcome
    {
        private ChangeRequestResolutionOutcome(bool isAllowed, string? reason, int headerStatus, int lineStatus)
        {
            IsAllowed = isAllowed;
            Reason = reason;
            HeaderStatus = headerStatus;
            LineStatus = lineStatus;
        }

        /// <summary>true si la resolucion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al usuario). null cuando <see cref="IsAllowed"/> es true.</summary>
        public string? Reason { get; }

        /// <summary>Estado final de sanic_changestatus de la cabecera. Solo valido si <see cref="IsAllowed"/>.</summary>
        public int HeaderStatus { get; }

        /// <summary>Estado final de sanic_changestatus de cada linea. Solo valido si <see cref="IsAllowed"/>.</summary>
        public int LineStatus { get; }

        public static ChangeRequestResolutionOutcome Allow(int headerStatus, int lineStatus) =>
            new ChangeRequestResolutionOutcome(true, null, headerStatus, lineStatus);

        public static ChangeRequestResolutionOutcome Block(string reason) =>
            new ChangeRequestResolutionOutcome(false, reason, 0, 0);
    }

    /// <summary>
    /// Motor de decision de sanic_CHResolveChangeRequest: reemplaza los 4 flujos
    /// materializadores (Applyclientdatachanges/Createclientdata/Activaterow/
    /// Deactivaterow, docs/clientadmin-rebuild/00-analisis-y-plan.md §3.1). La
    /// separacion de funciones (creador != resolutor, membresia Approver de la Fase 2)
    /// sigue siendo de <see cref="ChangeRequestApprovalGuard"/> — aca solo se traduce la
    /// decision (Approve/Reject) al estado final de cabecera y linea, y se agrega la
    /// regla "sin lineas no hay materializacion" para Update/Create (mata L2/L4: un RFC
    /// vacio jamas fue un exito).
    /// </summary>
    public static class ChangeRequestResolution
    {
        // Option set sanic_changestatus de sanic_chtblclientdatachangerequestline
        // (verificado contra su Entity.xml). Los valores coinciden numericamente con los
        // de la cabecera salvo la etiqueta 159460003 (Rejected en cabecera, Denied en
        // linea): mismo option set "shape", dos catalogos distintos.
        public const int LineDraft = 159460000;
        public const int LineSubmitted = 159460001;
        public const int LineApproved = 159460002;
        public const int LineDenied = 159460003;
        public const int LineApplied = 159460004;
        public const int LineFailed = 159460005;
        public const int LineCanceled = 159460006;

        // Option set sanic_type de la cabecera (verificado contra su Entity.xml).
        public const int TypeUpdate = 159460000;
        public const int TypeDeactivate = 159460001;
        public const int TypeCreate = 159460002;
        public const int TypeActivate = 159460003;

        /// <summary>
        /// Evalua si la resolucion (Approve/Reject) de un RFC en estado
        /// <paramref name="currentHeaderStatus"/> es legitima, delegando la separacion de
        /// funciones en <see cref="ChangeRequestApprovalGuard"/>. Si es legitima, devuelve
        /// los estados finales a escribir: Approve -> Applied/LineApplied (sin pasar por
        /// Approved: la materializacion es atomica, no hay estado intermedio observable);
        /// Reject -> Rejected/LineDenied.
        /// </summary>
        public static ChangeRequestResolutionOutcome Evaluate(
            int currentHeaderStatus,
            ChangeRequestDecision decision,
            Guid creatorId,
            Guid resolverId,
            bool isAuthorizedApprover)
        {
            var decisionStatus = decision == ChangeRequestDecision.Approve
                ? ChangeRequestApprovalGuard.Approved
                : ChangeRequestApprovalGuard.Rejected;

            var guard = ChangeRequestApprovalGuard.Evaluate(
                ChangeRequestEvent.Update, currentHeaderStatus, decisionStatus, creatorId, resolverId, isAuthorizedApprover);

            if (!guard.IsAllowed)
            {
                return ChangeRequestResolutionOutcome.Block(guard.Reason!);
            }

            return decision == ChangeRequestDecision.Approve
                ? ChangeRequestResolutionOutcome.Allow(ChangeRequestApprovalGuard.Applied, LineApplied)
                : ChangeRequestResolutionOutcome.Allow(ChangeRequestApprovalGuard.Rejected, LineDenied);
        }

        /// <summary>
        /// Update y Create exigen al menos una linea de cambio: sin ella no hay campos que
        /// convertir y aprobar "en vacio" es un exito disfrazado (L2/L4 del analisis).
        /// Activate/Deactivate mueven sanic_recordstatus del maestro, sin campos — no
        /// dependen de lineas.
        /// </summary>
        public static bool RequiresLines(int type) => type == TypeUpdate || type == TypeCreate;

        /// <summary>
        /// Campos que NINGUN registro de la tabla puede tener vacios, ni al nacer ni despues.
        ///
        /// sanic_taxid es el TERCER identificador de cliente (junto a sanic_cifbco y
        /// sanic_cifcom) y es UNICO — tiene la alternate key sanic_KeyTaxId. En Dataverse
        /// quedo deliberadamente con RequiredLevel=None: ApplicationRequired no se enforcea
        /// a nivel SDK (o sea, no frenaria a este mismo plugin, que es por donde se crean y
        /// editan los clientes) y ademas volveria no-guardables a los ~4999 clientes
        /// historicos desde los formularios model-driven. La obligatoriedad vive aca, que
        /// es el unico punto por el que pasan TODAS las altas y ediciones.
        /// </summary>
        private static readonly Dictionary<string, string[]> RequiredFieldsByTable =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sanic_chtblclient"] = new[] { "sanic_taxid" },
            };

        /// <summary>
        /// Columnas obligatorias de <paramref name="targetTable"/>. El plugin las usa para
        /// saber que traer del maestro antes de evaluar un Update. Vacio si la tabla no
        /// tiene requisitos.
        /// </summary>
        public static IReadOnlyList<string> RequiredFieldsFor(string? targetTable)
        {
            if (string.IsNullOrWhiteSpace(targetTable)
                || !RequiredFieldsByTable.TryGetValue(targetTable!, out var required))
            {
                return Array.Empty<string>();
            }

            return required;
        }

        /// <summary>
        /// Devuelve los campos obligatorios que quedarian vacios DESPUES de aplicar el RFC.
        /// Vacio si el tipo no escribe campos, si la tabla no tiene requisitos, o si estan
        /// todos cubiertos.
        ///
        /// La regla se evalua sobre el ESTADO FINAL del maestro, no sobre el contenido del
        /// RFC:
        /// <code>
        /// satisfecho = si el RFC trae linea del campo -> el valor no es blanco
        ///              si no                          -> Update y el maestro YA lo tiene
        /// </code>
        /// Editar un cliente que ya tiene taxid no obliga a repetirlo (el panel de edicion
        /// filtra los valores que no cambiaron: esa linea jamas se crearia). Editar uno de
        /// los historicos sin taxid EXIGE completarlo en la misma solicitud — asi el dato
        /// converge sin dejar a nadie sin poder operar.
        ///
        /// Una linea explicita en blanco NUNCA satisface, tenga o no el maestro el valor:
        /// pisaria con vacio un dato bueno.
        ///
        /// Activate/Deactivate quedan fuera: solo mueven sanic_recordstatus, no escriben
        /// campos. Bloquearlos dejaria basura historica imposible de desactivar.
        /// </summary>
        /// <param name="type">sanic_type de la cabecera.</param>
        /// <param name="targetTable">sanic_targettable de la cabecera.</param>
        /// <param name="lineValues">Pares (logical name, sanic_newvalue) de las lineas del RFC.</param>
        /// <param name="currentValues">Pares (logical name, valor actual) del maestro. Solo se consulta en Update.</param>
        public static IReadOnlyList<string> MissingRequiredFields(
            int type,
            string? targetTable,
            IEnumerable<KeyValuePair<string, string?>>? lineValues,
            IEnumerable<KeyValuePair<string, string?>>? currentValues)
        {
            if (type != TypeCreate && type != TypeUpdate)
            {
                return Array.Empty<string>();
            }

            var required = RequiredFieldsFor(targetTable);
            if (required.Count == 0)
            {
                return Array.Empty<string>();
            }

            // Lineas del RFC: se distingue "no vino" de "vino en blanco".
            var linePresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lineFilled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (lineValues != null)
            {
                foreach (var line in lineValues)
                {
                    if (string.IsNullOrWhiteSpace(line.Key))
                    {
                        continue;
                    }

                    linePresent.Add(line.Key);
                    if (!string.IsNullOrWhiteSpace(line.Value))
                    {
                        lineFilled.Add(line.Key);
                    }
                }
            }

            // Valor ya presente en el maestro (solo cuenta en Update: en un alta no hay).
            var masterFilled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (type == TypeUpdate && currentValues != null)
            {
                foreach (var current in currentValues)
                {
                    if (!string.IsNullOrWhiteSpace(current.Key) && !string.IsNullOrWhiteSpace(current.Value))
                    {
                        masterFilled.Add(current.Key);
                    }
                }
            }

            var missing = new List<string>();
            foreach (var field in required)
            {
                var satisfied = linePresent.Contains(field)
                    ? lineFilled.Contains(field)
                    : masterFilled.Contains(field);

                if (!satisfied)
                {
                    missing.Add(field);
                }
            }

            return missing;
        }
    }
}
