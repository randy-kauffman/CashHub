using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Espejos de cheques para el tracker:
    ///   B6 — sanic_bag en el cheque fisico (derivada del deposito de recuento):
    ///        la pantalla Tula trae todos los cheques en UNA query delegable en vez
    ///        de un filtro por entidad relacionada (que no delega) o un ForAll N+1.
    ///   B3 — sanic_matchedcheck en el cheque DECLARADO: la relacion real vive en el
    ///        cheque fisico (sanic_relateddeclaredcheck) y la pantalla Correo la
    ///        necesita en la otra direccion para navegar del declarado a su tula.
    ///   B-9 — sanic_recountingsession en el cheque fisico (derivada de la SESION de
    ///        su tula): los conteos de cheques de scrResumen tienen que ser CountRows
    ///        DELEGADOS al servidor. La cadena Cheque -> Tula -> Sesion es dot-walk de
    ///        DOS saltos y NO delega; materializar las filas en una coleccion topa en
    ///        el limite del cliente (500/2000) sin error, asi que el conteo MIENTE por
    ///        omision a partir de ese volumen (QA lo probo con 2.074 cheques).
    ///
    /// Este tipo es la DECISION pura (sin SDK): cuando re-estampar y que propagar.
    /// El acceso a Dataverse vive en CheckMirrorStampPlugin.
    /// </summary>
    public static class CheckMirrors
    {
        /// <summary>Stage Pre-operation del pipeline (sdkmessageprocessingstep.stage = 20).</summary>
        public const int PreOperationStage = 20;

        /// <summary>Stage Post-operation del pipeline (sdkmessageprocessingstep.stage = 40).</summary>
        public const int PostOperationStage = 40;

        /// <summary>Instruccion de estampa sobre UN cheque declarado. MatchedCheckId null = limpiar.</summary>
        public sealed class MatchUpdate
        {
            public Guid DeclaredCheckId { get; set; }
            public Guid? MatchedCheckId { get; set; }
        }

        /// <summary>
        /// B6/B-9 se escriben EN el Target: solo tienen efecto en Pre-operation, donde
        /// lo que se escribe todavia viaja con la operacion. En Post-operation la fila
        /// ya se guardo y tocar el Target no escribe nada.
        /// </summary>
        public static bool ShouldStampTargetMirrors(int stage)
        {
            return stage == PreOperationStage;
        }

        /// <summary>
        /// true si hay que (re)derivar la tula del cheque: siempre en Create (aunque
        /// el deposito venga vacio: espejo en blanco explicito), y en Update solo si
        /// el lookup al deposito de recuento vino en el Target.
        ///
        /// La MISMA condicion gobierna el espejo de sesion (B-9): los dos cuelgan del
        /// deposito de recuento (deposito -> tula -> sesion) y se resuelven en la
        /// misma pasada, asi que separarlos en dos decisiones seria mentir sobre el
        /// disparador.
        /// </summary>
        public static bool ShouldRestampBag(string message, bool depositTouched)
        {
            return string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || depositTouched;
        }

        /// <summary>
        /// true si en este mensaje+stage la fila del cheque fisico YA EXISTE y por lo
        /// tanto se le puede apuntar un lookup desde el declarado. En el Create eso
        /// recien pasa en Post-operation (40); en el Update pasa siempre.
        ///
        /// Existe aparte de <see cref="ResolveMatchUpdates"/> para que el plugin ni
        /// siquiera lea el declarado cuando el write-back no corresponde.
        /// </summary>
        public static bool CanWriteBackMatch(string message, int stage)
        {
            return !string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || stage >= PostOperationStage;
        }

        /// <summary>
        /// Instrucciones de MatchedCheck cuando el cheque fisico (des)referencia un
        /// declarado: limpiar el viejo si cambio, estampar el nuevo si hay. Sin
        /// cambio real, cero escrituras (no ensuciar auditoria del declarado).
        ///
        /// El STAGE es parte de la decision y NO es decorativo: el write-back escribe
        /// en OTRA tabla un lookup que apunta AL cheque fisico. En el Create, en
        /// Pre-operation (20) esa fila TODAVIA NO EXISTE y Dataverse rechaza el lookup
        /// con 0x80040265 ("Does Not Exist"), tumbando el Create entero. Por eso el
        /// Create solo estampa desde Post-operation (40). El Update no tiene ese
        /// problema — la fila ya existe en cualquier stage — y por eso sigue viviendo
        /// en el mismo paso Pre-operation de siempre.
        ///
        /// <paramref name="targetDeclaredMatch"/> es el MatchedCheck que YA tiene el
        /// declarado destino: si coincide, no se reescribe (idempotencia).
        /// </summary>
        public static IReadOnlyList<MatchUpdate> ResolveMatchUpdates(
            string message, int stage, Guid checkId, bool declaredTouched,
            Guid? targetDeclared, Guid? preDeclared, Guid? targetDeclaredMatch = null)
        {
            var updates = new List<MatchUpdate>(2);
            var stamp = targetDeclared.HasValue && targetDeclaredMatch != checkId;

            if (string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase))
            {
                if (CanWriteBackMatch(message, stage) && stamp)
                {
                    updates.Add(new MatchUpdate
                    {
                        DeclaredCheckId = targetDeclared.Value,
                        MatchedCheckId = checkId
                    });
                }
                return updates;
            }

            if (!declaredTouched || targetDeclared == preDeclared)
            {
                return updates;
            }

            if (preDeclared.HasValue)
            {
                updates.Add(new MatchUpdate
                {
                    DeclaredCheckId = preDeclared.Value,
                    MatchedCheckId = null
                });
            }

            if (stamp)
            {
                updates.Add(new MatchUpdate
                {
                    DeclaredCheckId = targetDeclared.Value,
                    MatchedCheckId = checkId
                });
            }

            return updates;
        }

        /// <summary>
        /// B-9, lado TULA: que propagar a los cheques cuando la tula cambia de sesion
        /// de recuento. Igual que la cascada de la planilla (BagReceptionMirrors):
        /// el paso esta filtrado, pero Dataverse dispara un paso filtrado AUNQUE se
        /// reescriba el mismo valor, y una cascada de N cheques por un update sin
        /// cambio real es auditoria basura mas N escrituras al pedo.
        /// </summary>
        public sealed class SessionCascadePlan
        {
            /// <summary>La sesion cambio: hay que reescribir el espejo en los cheques.</summary>
            public bool Propagate { get; set; }

            /// <summary>Sesion nueva (null = la tula quedo sin sesion y el espejo se limpia).</summary>
            public Guid? SessionId { get; set; }
        }

        /// <summary>
        /// Compara lo que trae el Target de la TULA contra su PreImage y arma el plan.
        /// <paramref name="sessionTouched"/> dice si la columna vino en el Target: si
        /// no vino, no cambio (el valor efectivo es el de la PreImage) y propagar
        /// seria inventar un cambio que no ocurrio.
        /// </summary>
        public static SessionCascadePlan ResolveSessionCascade(
            bool sessionTouched, Guid? newSessionId, Guid? previousSessionId)
        {
            return new SessionCascadePlan
            {
                Propagate = sessionTouched && newSessionId != previousSessionId,
                SessionId = newSessionId
            };
        }
    }
}
