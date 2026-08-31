using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;

namespace CashHub.Plugins
{
    /// <summary>
    /// Validacion del catalogo de apps del hub (CH-App-Hub).
    ///
    /// El hub lanza sanic_appurl tal cual (Launch), asi que la URL debe apuntar si o si al
    /// player de Power Apps. Corre sincrono en Pre-validation
    /// de Create y Update de sanic_chtblappcatalog: aborta antes de abrir la transaccion.
    /// La decision vive en el dominio puro (AppCatalogGuard); aca solo se lee el Target y
    /// se propaga el bloqueo.
    /// </summary>
    public sealed class GuardAppCatalogPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("GuardAppCatalog: sin Target de tipo Entity, se omite.");
                return;
            }

            // Solo se validan los campos presentes en este request: un Update que no toca
            // la URL ni los grupos pasa sin fricción.
            if (target.Contains(Schema.AppCatalog.AppUrl))
            {
                var appUrl = target.GetAttributeValue<string>(Schema.AppCatalog.AppUrl);
                tracing.Trace("GuardAppCatalog: {0} valida AppUrl='{1}'.", context.MessageName, appUrl ?? "(null)");

                var urlResult = AppCatalogGuard.EvaluateAppUrl(appUrl);
                if (!urlResult.IsAllowed)
                {
                    tracing.Trace("GuardAppCatalog: BLOQUEADO — {0}", urlResult.Reason);
                    throw new InvalidPluginExecutionException(urlResult.Reason);
                }
            }

            tracing.Trace("GuardAppCatalog: escritura valida.");
        }
    }
}
