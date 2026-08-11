using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;

namespace CashHub.Plugins
{
    /// <summary>
    /// Sella server-side las transiciones del handshake de recepcion de cheques.
    ///
    /// Registrado sincrono Pre-operation (stage 20) en Update de CH-Tbl-Checks
    /// filtrado por sanic_checkstatus, DESPUES del guard (Pre-validation): cuando
    /// corre, la transicion ya es legal. Lee el estado que escribe el request y
    /// estampa la fecha/hora del servidor EN EL TARGET (sin update extra, dentro
    /// de la misma operacion): 'Enviado a digitacion' -> sanic_senttodigitizationon,
    /// Recibido -> sanic_receivedon, 'No Recibido' -> sanic_notreceivedon. Asi el
    /// sello no depende del reloj ni de la honestidad del cliente. Otros estados
    /// (Recontado y los reservados) no estampan nada.
    /// </summary>
    public sealed class CheckStatusStampPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("CheckStamp: {0} de {1}, actor={2}, depth={3}.",
                context.MessageName, context.PrimaryEntityName, context.InitiatingUserId, context.Depth);

            try
            {
                if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
                {
                    throw new InvalidPluginExecutionException(
                        $"CheckStamp: {context.MessageName} de {context.PrimaryEntityName} sin Target de tipo Entity.");
                }

                var status = target.GetAttributeValue<OptionSetValue>(Schema.Check.CheckStatus)?.Value;
                var stampAttribute = ResolveStampAttribute(status);

                if (stampAttribute == null)
                {
                    tracing.Trace("CheckStamp: cheque {0}, estado {1} no estampa fecha.",
                        target.Id, status.HasValue ? status.Value.ToString() : "null");
                    return;
                }

                target[stampAttribute] = DateTime.UtcNow;
                tracing.Trace("CheckStamp: cheque {0}, estado {1} estampa {2} en el Target.",
                    target.Id, status, stampAttribute);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("CheckStamp: error inesperado estampando el handshake: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"CheckStamp no pudo estampar la transicion del cheque: {ex.Message}", ex);
            }
        }

        /// <summary>Columna datetime que sella la transicion, o null si el estado no estampa.</summary>
        private static string ResolveStampAttribute(int? status)
        {
            switch (status)
            {
                case CheckGuard.EnviadoADigitacion:
                    return Schema.Check.SentToDigitizationOn;
                case CheckGuard.Recibido:
                    return Schema.Check.ReceivedOn;
                case CheckGuard.NoRecibido:
                    return Schema.Check.NotReceivedOn;
                default:
                    return null;
            }
        }
    }
}
