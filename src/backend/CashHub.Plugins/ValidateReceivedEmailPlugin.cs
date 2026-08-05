using System;
using Microsoft.Xrm.Sdk;

namespace CashHub.Plugins
{
    /// <summary>
    /// Un solo motor, dos puertas:
    /// 1) Custom API sanic_CHValidateReceivedEmail (Console "Reprocesar", barrido, pruebas).
    /// 2) Paso asíncrono Post-operation en Create de sanic_chtblreceivedemails (automático).
    /// </summary>
    public sealed class ValidateReceivedEmailPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // El motor escribe runs/results/estado: corre como sistema, no como el llamante.
            var service = serviceFactory.CreateOrganizationService(null);

            if (context.Depth > 2)
            {
                tracing.Trace("ValidateReceivedEmail: depth {0}, se omite para evitar bucles.", context.Depth);
                return;
            }

            var emailId = ResolveEmailId(context);
            tracing.Trace("ValidateReceivedEmail: message={0} email={1}", context.MessageName, emailId);

            try
            {
                var validator = new ReceivedEmailValidationService(service, tracing);
                var (runId, passed, failedFindings) = validator.Validate(emailId);

                if (string.Equals(context.MessageName, Schema.CustomApi.MessageName, StringComparison.OrdinalIgnoreCase))
                {
                    context.OutputParameters[Schema.CustomApi.OutRunId] = runId;
                    context.OutputParameters[Schema.CustomApi.OutResult] = passed ? "Pass" : "Fail";
                    context.OutputParameters[Schema.CustomApi.OutFailedFindings] = failedFindings;
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Nunca un correo en el limbo: se marca Error y LUEGO se propaga para que
                // el job del sistema quede en Failed (visible para operaciones).
                tracing.Trace("ValidateReceivedEmail: excepcion no controlada: {0}", ex);
                TryFlagSystemError(service, tracing, emailId, ex.Message);
                throw new InvalidPluginExecutionException(
                    $"El motor de validacion fallo para el correo {emailId:D}: {ex.Message}", ex);
            }
        }

        private static Guid ResolveEmailId(IPluginExecutionContext context)
        {
            if (context.InputParameters.TryGetValue(Schema.CustomApi.InReceivedEmailId, out var raw))
            {
                if (raw is Guid guid) return guid;
                if (raw is string text && Guid.TryParse(text, out var parsed)) return parsed;
            }

            if (context.InputParameters.TryGetValue("Target", out var target) && target is Entity entity)
            {
                return entity.Id;
            }

            throw new InvalidPluginExecutionException(
                "ValidateReceivedEmail: no se pudo resolver el correo a validar (ni ReceivedEmailId ni Target).");
        }

        private static void TryFlagSystemError(IOrganizationService service, ITracingService tracing,
            Guid emailId, string reason)
        {
            try
            {
                service.Update(new Entity(Schema.ReceivedEmail.LogicalName, emailId)
                {
                    [Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(Schema.ReceivedEmail.ReviewError),
                    [Schema.ReceivedEmail.ReviewMethod] = new OptionSetValue(Schema.ReceivedEmail.MethodAuto),
                    [Schema.ReceivedEmail.ErrorText] = $"Error de sistema del motor de validacion: {reason}"
                });
            }
            catch (Exception updateEx)
            {
                tracing.Trace("ValidateReceivedEmail: no se pudo marcar Error en el correo: {0}", updateEx.Message);
            }
        }
    }
}
