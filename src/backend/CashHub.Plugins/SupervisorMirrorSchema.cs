namespace CashHub.Plugins
{
    /// <summary>
    /// Nombres logicos de los espejos del paquete CH-App-Supervisor v2 (B-2, B-3, B-6).
    ///
    /// Viven en un archivo aparte de Schema.cs a proposito: las columnas se crean por
    /// script (create-supervisor-mirror-columns.sh) y este paquete se construyo en
    /// paralelo con otros que tocan Schema.cs. Se pueden plegar dentro de Schema
    /// (Bag / ApprovalRequest) cuando el paquete cierre, sin cambiar una sola cadena.
    ///
    /// Los tres son DERIVADOS: los mantiene un plugin y no se editan a mano.
    /// </summary>
    internal static class SupervisorMirrors
    {
        internal static class Bag
        {
            /// <summary>
            /// B-2 — lookup al lote de recepcion, derivado de
            /// sanic_receptionid.sanic_receptionbatch por BagReceptionMirrorPlugin.
            /// Existe porque Bag -> Reception -> Batch son DOS saltos y no delega.
            /// </summary>
            public const string ReceptionBatchMirror = "sanic_receptionbatchid";

            /// <summary>
            /// B-3 — espejo de texto del numero de recibo de la planilla, derivado de
            /// sanic_receptionid.sanic_receiptid. Habilita la busqueda por recibo
            /// delegable en scrTulas (ledger L-18).
            /// </summary>
            public const string ReceiptIdMirror = "sanic_receiptidmirror";
        }

        internal static class Reception
        {
            /// <summary>Numero de recibo de la planilla (origen de B-3).</summary>
            public const string ReceiptId = "sanic_receiptid";

            /// <summary>
            /// PreImage del paso de cascada (Update Post-op de la planilla) con
            /// sanic_receptionbatch + sanic_receiptid: sin ella no se puede saber si
            /// el valor CAMBIO y la cascada escribiria N tulas por nada.
            /// </summary>
            public const string MirrorPreImageName = "PreImage";
        }

        internal static class ApprovalRequest
        {
            /// <summary>JSON plano de recursos de la solicitud (origen de B-6).</summary>
            public const string RequestResources = "sanic_requestresources";

            /// <summary>
            /// B-6 — numero de tula extraido del JSON al crear la solicitud, para que
            /// la bandeja pueda buscar por tula con delegacion (ledger L-19).
            /// </summary>
            public const string BagNumber = "sanic_bagnumber";
        }
    }
}
