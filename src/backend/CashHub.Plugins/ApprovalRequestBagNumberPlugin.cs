using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// B-6 — estampa sanic_bagnumber en la solicitud de aprobacion al CREARLA,
    /// leyendo el numero de tula del JSON de sanic_requestresources.
    ///
    /// Por que: la bandeja de aprobaciones necesita buscar por numero de TULA, pero
    /// el numero vive dentro de un blob JSON (ledger L-19) y ninguna consulta
    /// delegable puede entrar ahi. Con el espejo, la busqueda es
    /// StartsWith(BagNumber, texto) — delegable.
    ///
    /// Pre-operation (20) SINCRONO en Create de sanic_chtblapprovalrequest: escribe
    /// EN el Target, sin update extra. El contrato de claves NO es homogeneo
    /// (03-pantalla-aprobaciones.md §0): unos tipos traen el NUMERO en "BagID",
    /// otros solo el GUID en "BagGUID"/"GUID" — en ese caso se resuelve el numero
    /// con UN Retrieve como sistema.
    ///
    /// CONTRATO DURO: este espejo es un dato de conveniencia y NUNCA puede voltear
    /// el Create de una solicitud. Un JSON roto, ausente, con otro esquema o una
    /// tula borrada dejan la columna en blanco y se trazan; no se lanza jamas.
    /// </summary>
    public sealed class ApprovalRequestBagNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("ApprovalBagNumber: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("ApprovalBagNumber: sin Target de tipo Entity, se omite.");
                return;
            }

            if (target.LogicalName != Schema.ApprovalRequest.LogicalName)
            {
                tracing.Trace("ApprovalBagNumber: entidad {0} fuera de alcance, se omite.",
                    target.LogicalName);
                return;
            }

            try
            {
                var resources = target.GetAttributeValue<string>(
                    SupervisorMirrors.ApprovalRequest.RequestResources);
                if (string.IsNullOrWhiteSpace(resources))
                {
                    tracing.Trace("ApprovalBagNumber: solicitud sin RequestResources, espejo en blanco.");
                    return;
                }

                var source = ApprovalBagNumber.Resolve(resources);
                if (source.IsEmpty)
                {
                    tracing.Trace(
                        "ApprovalBagNumber: RequestResources sin BagID/BagGUID/GUID utilizables "
                        + "(tipo '{0}', {1} caracteres) — espejo en blanco, la solicitud sigue.",
                        target.GetAttributeValue<string>(Schema.ApprovalRequest.RequestType) ?? "sin tipo",
                        resources.Length);
                    return;
                }

                var number = source.Number;
                if (number == null && source.BagId.HasValue)
                {
                    number = ResolveNumberFromBag(serviceFactory, tracing, source.BagId.Value);
                }

                if (number == null)
                {
                    tracing.Trace("ApprovalBagNumber: no se pudo resolver el numero de tula, espejo en blanco.");
                    return;
                }

                target[SupervisorMirrors.ApprovalRequest.BagNumber] = number;
                tracing.Trace("ApprovalBagNumber: solicitud -> tula '{0}' (origen: {1}).",
                    number, source.Number != null ? "numero en el JSON" : "GUID resuelto");
            }
            catch (Exception ex)
            {
                // A PROPOSITO se traga TODO, incluido InvalidPluginExecutionException:
                // el espejo es opcional y no puede impedir que se cree una solicitud
                // de aprobacion. El diagnostico queda en el trace log.
                tracing.Trace("ApprovalBagNumber: error resolviendo el espejo, se deja en blanco: {0}", ex);
            }
        }

        /// <summary>
        /// Resuelve el numero de la tula por su GUID. Lectura como sistema: quien
        /// crea la solicitud (cajero) puede no tener lectura sobre esa tula.
        /// Una tula inexistente o borrada devuelve null, no una excepcion.
        /// </summary>
        private static string ResolveNumberFromBag(
            IOrganizationServiceFactory serviceFactory, ITracingService tracing, Guid bagId)
        {
            try
            {
                var system = serviceFactory.CreateOrganizationService(null);
                var bag = system.Retrieve(
                    Schema.Bag.LogicalName, bagId, new ColumnSet(Schema.Bag.BagId));
                return ApprovalBagNumber.Cap(bag.GetAttributeValue<string>(Schema.Bag.BagId));
            }
            catch (Exception ex)
            {
                tracing.Trace("ApprovalBagNumber: no se pudo leer la tula {0}: {1}", bagId, ex.Message);
                return null;
            }
        }
    }
}
