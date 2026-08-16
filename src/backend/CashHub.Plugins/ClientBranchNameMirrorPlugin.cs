using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejo del nombre del cliente en la sucursal (CH-App-Reception v2):
    ///
    ///   sanic_clientnamemirror (texto 850) en sanic_chtblclientbranch, derivado de
    ///   sanic_client.sanic_name.
    ///
    /// Dos pasos SINCRONOS, los dos Pre-operation (20) sobre la sucursal:
    ///   - Create de sanic_chtblclientbranch: estampa el espejo.
    ///   - Update de sanic_chtblclientbranch (filtrado en sanic_client):
    ///     re-estampa al re-colgar la sucursal de otro cliente.
    ///
    /// Se escribe EN el Target: UNA sola lectura del cliente y CERO escrituras
    /// adicionales. La lectura va como sistema porque es una derivacion, no un
    /// permiso de negocio: el que da de alta la sucursal no necesariamente lee la
    /// ficha del cliente.
    ///
    /// Por que existe: el combo de sucursales de la v2 muestra y busca por nombre
    /// de CLIENTE. Por la relacion (sanic_Client.sanic_name) el filtro NO delega —
    /// Power Fx se trae el tope de filas, filtra en el cliente y deja sucursales
    /// afuera SIN error. Con ~50.000 sucursales eso es un combo que miente.
    /// La decision de cuando estampar vive en el dominio puro
    /// (ClientBranchNameMirrors).
    ///
    /// -------------------------------------------------------------------------
    /// LIMITACION CONOCIDA — NO HAY CASCADA POR RENAME DEL CLIENTE
    /// -------------------------------------------------------------------------
    /// Si a un cliente le cambian sanic_name, las sucursales YA existentes quedan
    /// con el nombre viejo en el espejo. Es deliberado:
    ///   - un cliente puede tener miles de sucursales; una cascada sincrona sobre
    ///     el Update del cliente le pega N escrituras a una operacion que el
    ///     usuario ve como "corregir una letra del nombre";
    ///   - el rename de cliente es un evento RARO y controlado (pasa por el flujo
    ///     de RFC de datos de cliente), no un evento de alta frecuencia.
    /// La cobertura es scripts/backfill-clientbranch-mirror.py: es re-ejecutable,
    /// idempotente y solo PATCHea lo que difiere. Correrlo despues de un rename
    /// masivo deja el espejo al dia.
    /// Si algun dia el rename se vuelve frecuente, el reemplazo natural es un paso
    /// Post-operation (40) de Update de sanic_chtblclient filtrado en sanic_name
    /// con PreImage, paginado — mismo patron que la cascada de
    /// BagReceptionMirrorPlugin.
    /// </summary>
    public sealed class ClientBranchNameMirrorPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("ClientBranchNameMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("ClientBranchNameMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            if (context.PrimaryEntityName != ClientBranchMirrors.ClientBranch.LogicalName)
            {
                tracing.Trace("ClientBranchNameMirror: entidad {0} fuera de alcance, se omite.",
                    context.PrimaryEntityName);
                return;
            }

            try
            {
                StampBranch(
                    serviceFactory.CreateOrganizationService(null),
                    tracing, context.MessageName, target);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("ClientBranchNameMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo mantener el espejo del nombre del cliente en la sucursal: "
                    + ex.Message, ex);
            }
        }

        /// <summary>
        /// Resuelve el cliente del Target y escribe sanic_clientnamemirror EN el
        /// propio Target (Pre-operation: cero escrituras adicionales).
        /// </summary>
        private static void StampBranch(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var clientTouched = target.Contains(ClientBranchMirrors.ClientBranch.ClientLookup);

            if (!ClientBranchNameMirrors.ShouldStamp(message, clientTouched))
            {
                tracing.Trace(
                    "ClientBranchNameMirror: Update de sucursal sin cambio de cliente, se omite.");
                return;
            }

            if (!clientTouched)
            {
                // Create sin cliente (el lookup es requerido, pero un Create por API
                // puede llegar sin el y rebotar despues): no hay de donde derivar.
                tracing.Trace(
                    "ClientBranchNameMirror: sucursal creada sin cliente, sin espejo que estampar.");
                return;
            }

            var clientRef = target.GetAttributeValue<EntityReference>(
                ClientBranchMirrors.ClientBranch.ClientLookup);

            if (clientRef == null)
            {
                target[ClientBranchMirrors.ClientBranch.ClientNameMirror] = null;
                tracing.Trace(
                    "ClientBranchNameMirror: sucursal desvinculada del cliente, espejo en blanco.");
                return;
            }

            var client = system.Retrieve(
                ClientBranchMirrors.Client.LogicalName, clientRef.Id,
                new ColumnSet(ClientBranchMirrors.Client.Name));

            var name = ClientBranchNameMirrors.CapName(
                client.GetAttributeValue<string>(ClientBranchMirrors.Client.Name));

            target[ClientBranchMirrors.ClientBranch.ClientNameMirror] = name;

            tracing.Trace(
                "ClientBranchNameMirror: sucursal {0} ({1}) -> cliente {2}, nombre espejo '{3}'.",
                target.Id, message, clientRef.Id, name ?? "null");
        }
    }
}
