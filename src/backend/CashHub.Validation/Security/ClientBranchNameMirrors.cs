using System;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Espejo de la sucursal hacia su cliente, para CH-App-Reception v2:
    ///   sanic_clientnamemirror (texto 850) en CH-Tbl-ClientBranch, derivado de
    ///   sanic_client.sanic_name.
    ///
    /// Por que existe: el combo de sucursales de la v2 tiene que MOSTRAR y BUSCAR
    /// por nombre de cliente. Hacerlo por la relacion (sanic_Client.sanic_name) es
    /// un dot-walk que NO delega: Power Fx se trae el tope de filas (500/2000),
    /// filtra en el cliente y a partir de ahi deja sucursales afuera SIN error —
    /// con ~50.000 sucursales el combo miente por omision. Con la columna en la
    /// propia sucursal, StartsWith es delegable.
    ///
    /// Este tipo es la DECISION pura (sin SDK): cuando re-estampar y como
    /// normalizar el nombre. El acceso a Dataverse vive en
    /// ClientBranchNameMirrorPlugin.
    /// </summary>
    public static class ClientBranchNameMirrors
    {
        /// <summary>
        /// Largo de sanic_clientnamemirror (nvarchar 850) creado por
        /// create-clientbranch-mirror-column.sh. Es el MISMO largo que
        /// sanic_chtblclient.sanic_name, asi que en la practica nunca recorta:
        /// el tope esta para que un cambio de esquema del origen no voltee la
        /// escritura del espejo (y con ella la operacion del usuario).
        /// </summary>
        public const int MaxNameLength = 850;

        /// <summary>
        /// true si hay que (re)derivar el espejo: siempre en Create (aunque el
        /// cliente venga vacio, para dejar el espejo en blanco explicito) y en
        /// Update SOLO si el lookup al cliente vino en el Target — re-colgar la
        /// sucursal de otro cliente es el unico evento que mueve el espejo desde
        /// el lado de la sucursal.
        ///
        /// El rename del CLIENTE no entra aca a proposito: no hay cascada (ver la
        /// limitacion conocida en el header de ClientBranchNameMirrorPlugin).
        /// </summary>
        public static bool ShouldStamp(string message, bool clientTouched)
        {
            return string.Equals(message, "Create", StringComparison.OrdinalIgnoreCase)
                || clientTouched;
        }

        /// <summary>
        /// Normaliza el nombre para el espejo: trim, vacio -> null y recorte al
        /// largo de la columna. Sin el recorte, un cliente con nombre mas largo que
        /// la columna espejo voltearia el alta de la sucursal entera.
        /// </summary>
        public static string? CapName(string? name)
        {
            if (name == null)
            {
                return null;
            }

            var trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            return trimmed.Length <= MaxNameLength
                ? trimmed
                : trimmed.Substring(0, MaxNameLength);
        }
    }
}
