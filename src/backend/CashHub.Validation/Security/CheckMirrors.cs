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
    /// </summary>
    public static class CheckMirrors
    {
        /// <summary>Instruccion de estampa sobre UN cheque declarado. MatchedCheckId null = limpiar.</summary>
        public sealed class MatchUpdate
        {
            public Guid DeclaredCheckId { get; set; }
            public Guid? MatchedCheckId { get; set; }
        }

        /// <summary>
        /// true si hay que (re)derivar la tula del cheque: siempre en Create (aunque
        /// el deposito venga vacio: espejo en blanco explicito), y en Update solo si
        /// el lookup al deposito de recuento vino en el Target.
        /// </summary>
        public static bool ShouldRestampBag(string message, bool depositTouched)
        {
            return string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || depositTouched;
        }

        /// <summary>
        /// Instrucciones de MatchedCheck cuando el cheque fisico (des)referencia un
        /// declarado: limpiar el viejo si cambio, estampar el nuevo si hay. Sin
        /// cambio real, cero escrituras (no ensuciar auditoria del declarado).
        /// </summary>
        public static IReadOnlyList<MatchUpdate> ResolveMatchUpdates(
            string message, Guid checkId, bool declaredTouched,
            Guid? targetDeclared, Guid? preDeclared)
        {
            var updates = new List<MatchUpdate>(2);

            if (string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase))
            {
                if (targetDeclared.HasValue)
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

            if (targetDeclared.HasValue)
            {
                updates.Add(new MatchUpdate
                {
                    DeclaredCheckId = targetDeclared.Value,
                    MatchedCheckId = checkId
                });
            }

            return updates;
        }
    }
}
