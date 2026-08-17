using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Espejos server-side de CH-App-Recounting v2 (docs/recounting-rebuild/01-app.md
    /// §2.2). Son columnas DERIVADAS que la app NUNCA escribe: solo las lee.
    ///
    ///   E-1  sanic_recountingsessionmirror (lookup -> CH-Tbl-RecountingSessions) en
    ///        CH-Tbl-DepositsRecountings, derivada de BagID.RecountingSession.
    ///        Mata D-2: los 12 Sum(Filter(..., BagID.RecountingSession...)) de DOS
    ///        saltos que mostraban cifras PARCIALES de dinero (Power Fx materializa
    ///        hasta el tope del cliente —500/2000— y trunca SIN error).
    ///
    ///   E-3  sanic_declaredbagmirror (lookup -> CH-Tbl-DeclaredBags) en
    ///        CH-Tbl-DeclaredChecks, derivada de DeclaredDepositID.DeclaredBag.
    ///        Mata los ~82 round-trips de la apertura de tula (un Filter remoto por
    ///        deposito declarado) reduciendolos a UNA query delegable.
    ///
    ///   E-4  sanic_recountingsessiondetailmirror (lookup ->
    ///        CH-Tbl-RecountingSessionDetails) + sanic_xmlreadingactiva (bit) en
    ///        CH-Tbl-XMLReadingDetails, derivadas de XMLReading.RecountingSessionDetailId
    ///        y XMLReading.IsActive. Mata D-1: el "in" con subconsulta que JAMAS delega
    ///        y dejaba la columna Auto en 0 con >500 filas historicas.
    ///        Son DOS columnas a proposito: con una sola habria que volver a
    ///        CH-Tbl-XMLReadings a preguntar IsActive y vuelve el segundo salto que
    ///        E-4 vino a matar (divergencia L-3 del ledger).
    ///
    /// E-2 (sanic_bag en CH-Tbl-Checks) NO vive aca: ya existe y lo mantiene
    /// CheckMirrorStampPlugin / CheckMirrors (espejo B6 del tracker).
    ///
    /// Este tipo es la DECISION pura (sin SDK): cuando re-estampar y que propagar.
    /// El acceso a Dataverse vive en DepositSessionMirrorPlugin,
    /// DeclaredCheckBagMirrorPlugin y XmlReadingDetailMirrorPlugin.
    /// </summary>
    public static class RecountingMirrors
    {
        /// <summary>
        /// Disparador de estampa del lado HIJO, compartido por E-1, E-3 y E-4: hay
        /// que (re)derivar el espejo SIEMPRE en Create —aunque el lookup origen venga
        /// vacio, para dejar el espejo en blanco EXPLICITO y no heredado— y en Update
        /// SOLO si el lookup origen vino en el Target (re-colgar el hijo de otro
        /// padre es el unico evento que mueve el espejo desde el lado del hijo).
        ///
        /// Los tres espejos comparten la MISMA forma de disparo (un lookup origen en
        /// el propio Target), asi que separarlos en tres predicados identicos seria
        /// mentir sobre que son tres reglas distintas.
        /// </summary>
        public static bool ShouldStamp(string? message, bool sourceTouched)
        {
            return string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || sourceTouched;
        }

        /// <summary>Plan de cascada de UN lookup del padre a sus hijos.</summary>
        public sealed class LookupCascadePlan
        {
            /// <summary>El valor cambio de verdad: hay que reescribir el espejo en los hijos.</summary>
            public bool Propagate { get; set; }

            /// <summary>Valor nuevo (null = el padre quedo sin referencia y el espejo se limpia).</summary>
            public Guid? ValueId { get; set; }
        }

        /// <summary>
        /// E-1, lado TULA: que propagar a los depositos de recuento cuando la tula
        /// cambia de sesion. La app patchea Bags.RecountingSession al abrir la tula
        /// (03 §6.3) —antes de que existan depositos, asi que el Create alcanza para
        /// el camino feliz—, pero tambien al RE-abrir una tula en otra sesion: ahi los
        /// depositos ya existen y sin cascada quedan colgando de la sesion vieja.
        /// Con la cascada, el conjunto "depositos con RecountingSessionMirror = S" es
        /// EXACTAMENTE el que devuelve el fallback sin backend de 01 §2.2 (los
        /// depositos de las tulas que hoy estan en S).
        ///
        /// <paramref name="touched"/> dice si la columna vino en el Target: si no
        /// vino, no cambio (el valor efectivo es el de la PreImage) y propagar seria
        /// inventar un cambio que no ocurrio. Dataverse dispara un paso FILTRADO
        /// aunque se reescriba el mismo valor: sin esta guarda, guardar la tula sin
        /// cambiarla reescribe N depositos y ensucia su auditoria.
        /// </summary>
        public static LookupCascadePlan ResolveLookupCascade(
            bool touched, Guid? newId, Guid? previousId)
        {
            return new LookupCascadePlan
            {
                Propagate = touched && newId != previousId,
                ValueId = newId
            };
        }

        /// <summary>Plan de cascada de la LECTURA a sus detalles: las DOS columnas de E-4.</summary>
        public sealed class ReadingCascadePlan
        {
            /// <summary>Alguna de las dos cambio de verdad.</summary>
            public bool Propagate { get; set; }

            /// <summary>Sesion-detalle EFECTIVA (null = la lectura quedo sin detalle).</summary>
            public Guid? SessionDetailId { get; set; }

            /// <summary>IsActive EFECTIVO (null = la lectura nunca lo escribio).</summary>
            public bool? IsActive { get; set; }
        }

        /// <summary>
        /// E-4, lado LECTURA: cuando CH-Tbl-XMLReadings cambia de sesion-detalle o se
        /// (des)activa, sus detalles quedan con el espejo stale y Q17 —el Filter de
        /// UN salto de "Leer contadora"— empieza a sumar la lectura muerta o a
        /// ignorar la viva. Es el caso CRITICO del contrato: la lectura vieja se apaga
        /// DESPUES de que sus detalles ya existen.
        ///
        /// Se propagan SIEMPRE las dos columnas juntas (el valor efectivo de la que no
        /// vino en el Target sale de la PreImage): son un par, y escribir una sola
        /// dejaria al detalle con mitad de la verdad de su lectura.
        /// </summary>
        public static ReadingCascadePlan ResolveReadingCascade(
            bool sessionDetailTouched, Guid? newSessionDetailId, Guid? previousSessionDetailId,
            bool activeTouched, bool? newActive, bool? previousActive)
        {
            var sessionDetailChanged =
                sessionDetailTouched && newSessionDetailId != previousSessionDetailId;
            var activeChanged = activeTouched && newActive != previousActive;

            return new ReadingCascadePlan
            {
                Propagate = sessionDetailChanged || activeChanged,
                SessionDetailId = sessionDetailTouched ? newSessionDetailId : previousSessionDetailId,
                IsActive = activeTouched ? newActive : previousActive
            };
        }
    }
}
