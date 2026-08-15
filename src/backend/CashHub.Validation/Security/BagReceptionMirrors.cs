using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Espejos de la tula hacia su planilla, para CH-App-Supervisor v2:
    ///   B-2 — sanic_receptionbatchid (lookup al lote) en CH-Tbl-Bags, derivado de
    ///         sanic_receptionid.sanic_receptionbatch.
    ///   B-3 — sanic_receiptidmirror (texto) en CH-Tbl-Bags, derivado de
    ///         sanic_receptionid.sanic_receiptid.
    ///
    /// Por que existen: el dup-check de tulas y la busqueda por recibo de scrTulas
    /// hoy hacen dot-walk de DOS saltos (Bag -> Reception -> Batch/ReceiptID). Un
    /// Filter sobre columna de entidad relacionada NO delega: Power Fx se trae el
    /// tope de filas (500/2000), filtra en el cliente y a partir de ahi deja
    /// registros afuera SIN error — el dup-check "no encuentra" duplicados que
    /// existen. Con la columna en la propia tula el filtro es delegable.
    ///
    /// Este tipo es la DECISION pura (sin SDK): cuando re-estampar y que propagar.
    /// El acceso a Dataverse vive en BagReceptionMirrorPlugin.
    /// </summary>
    public static class BagReceptionMirrors
    {
        /// <summary>Largo de sanic_receiptidmirror (nvarchar 100) creado por script.</summary>
        public const int MaxReceiptLength = 100;

        /// <summary>
        /// true si hay que (re)derivar los espejos de la tula: siempre en Create
        /// (aunque la planilla venga vacia, para dejar el espejo en blanco explicito)
        /// y en Update SOLO si el lookup a la planilla vino en el Target — re-colgar
        /// la tula de otra planilla es el unico evento que mueve los espejos desde
        /// el lado de la tula.
        /// </summary>
        public static bool ShouldStamp(string message, bool receptionTouched)
        {
            return string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || receptionTouched;
        }

        /// <summary>
        /// Que propagar a las tulas cuando la PLANILLA cambia de lote o de recibo.
        /// Solo se listan las columnas que CAMBIARON de valor: el paso esta filtrado,
        /// pero Dataverse dispara un paso filtrado aunque se reescriba el mismo valor,
        /// y una cascada de N tulas por un update sin cambio real es auditoria basura
        /// mas N escrituras al pedo.
        /// </summary>
        public sealed class CascadePlan
        {
            /// <summary>El lote cambio: hay que reescribir sanic_receptionbatchid en las tulas.</summary>
            public bool PropagateBatch { get; set; }

            /// <summary>Lote nuevo (null = la planilla quedo sin lote y el espejo se limpia).</summary>
            public Guid? BatchId { get; set; }

            /// <summary>El recibo cambio: hay que reescribir sanic_receiptidmirror.</summary>
            public bool PropagateReceipt { get; set; }

            /// <summary>Recibo nuevo, ya recortado al largo de la columna espejo.</summary>
            public string? ReceiptId { get; set; }

            /// <summary>false = no hay nada que propagar; no se consulta ni una tula.</summary>
            public bool Any => PropagateBatch || PropagateReceipt;
        }

        /// <summary>
        /// Compara lo que trae el Target contra la PreImage y arma el plan de cascada.
        /// Los flags *Touched dicen si la columna vino en el Target (si no vino, no
        /// cambio: el valor efectivo es el de la PreImage).
        /// </summary>
        public static CascadePlan ResolveCascade(
            bool batchTouched, Guid? newBatchId, Guid? previousBatchId,
            bool receiptTouched, string? newReceiptId, string? previousReceiptId)
        {
            var newReceipt = CapReceipt(newReceiptId);
            var oldReceipt = CapReceipt(previousReceiptId);

            return new CascadePlan
            {
                PropagateBatch = batchTouched && newBatchId != previousBatchId,
                BatchId = newBatchId,
                PropagateReceipt = receiptTouched
                    && !string.Equals(newReceipt, oldReceipt, StringComparison.Ordinal),
                ReceiptId = newReceipt
            };
        }

        /// <summary>
        /// Normaliza el recibo para el espejo: trim, vacio -> null y recorte al largo
        /// de la columna. Sin el recorte, una planilla con recibo largo voltearia la
        /// escritura del espejo y con ella la operacion del usuario.
        /// </summary>
        public static string? CapReceipt(string? receiptId)
        {
            if (receiptId == null)
            {
                return null;
            }

            var trimmed = receiptId.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            return trimmed.Length <= MaxReceiptLength
                ? trimmed
                : trimmed.Substring(0, MaxReceiptLength);
        }
    }
}
