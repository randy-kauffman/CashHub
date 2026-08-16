namespace CashHub.Plugins
{
    /// <summary>
    /// Nombres logicos del espejo de nombre de cliente en la sucursal
    /// (CH-App-Reception v2).
    ///
    /// Vive en un archivo aparte de Schema.cs por el mismo motivo que
    /// SupervisorMirrorSchema: la columna se crea por script
    /// (create-clientbranch-mirror-column.sh) y este paquete se construyo en
    /// paralelo con otros que tocan Schema.cs. Se puede plegar dentro de Schema
    /// cuando el paquete cierre, sin cambiar una sola cadena.
    ///
    /// El espejo es DERIVADO: lo mantiene ClientBranchNameMirrorPlugin y no se
    /// edita a mano ni desde una app.
    /// </summary>
    internal static class ClientBranchMirrors
    {
        internal static class Client
        {
            public const string LogicalName = "sanic_chtblclient";

            /// <summary>Nombre del cliente: el ORIGEN del espejo.</summary>
            public const string Name = "sanic_name";
        }

        internal static class ClientBranch
        {
            public const string LogicalName = "sanic_chtblclientbranch";

            /// <summary>Lookup al cliente: de aca sale el espejo.</summary>
            public const string ClientLookup = "sanic_client";

            /// <summary>
            /// Espejo de texto del nombre del cliente, para que el combo de
            /// sucursales de CH-App-Reception v2 muestre y busque por cliente con
            /// StartsWith DELEGADO, sin navegar la relacion.
            /// </summary>
            public const string ClientNameMirror = "sanic_clientnamemirror";
        }
    }
}
