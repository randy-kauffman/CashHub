using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing de la Custom API <c>sanic_CHCanResolveApproval</c>: responde si el usuario
    /// que llama puede resolver (aprobar o rechazar) una solicitud de aprobacion, y por que
    /// no si no puede.
    ///
    /// NO resuelve nada ni escribe: es un pre-vuelo de solo lectura. La autoridad sigue
    /// siendo <see cref="GuardReceptionPlugin"/> en el Update — este plugin existe porque
    /// Power Fx descarta el mensaje custom de los faults de plugin (ver
    /// <see cref="Schema.CanResolveApprovalApi"/>) y la app necesita el motivo real ANTES
    /// de intentar el Patch.
    ///
    /// CONTRATO: nunca lanza por una decision de negocio. Un veredicto negativo viaja como
    /// <c>Allowed=false</c> + <c>Message</c> con HTTP 200, para que el canal de error de
    /// Power Fx no participe. Solo lanza si el contrato de entrada esta mal (id ausente o
    /// ilegible), que es un error de programacion de la app, no un caso de negocio.
    ///
    /// La regla vive en <see cref="ReceptionGuard.EvaluateApprovalUpdate"/> — la MISMA que
    /// aplica el guard. No se reimplementa: si divergieran, la app mostraria un mensaje que
    /// el servidor no respeta.
    /// </summary>
    public sealed class CanResolveApprovalPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var actor = context.InitiatingUserId;
            var requestId = ReadRequestId(context);
            tracing.Trace("CanResolveApproval: solicitud {0}, actor {1}.", requestId, actor);

            // Lectura COMO SISTEMA: el pre-vuelo tiene que poder informar "no sos aprobador"
            // aunque el usuario no alcance a leer la solicitud con sus propios privilegios.
            var system = serviceFactory.CreateOrganizationService(null);

            Entity request;
            try
            {
                request = system.Retrieve(
                    Schema.ApprovalRequest.LogicalName, requestId,
                    new ColumnSet(Schema.ApprovalRequest.ApprovalStatus, Schema.ApprovalRequest.CreatedBy));
            }
            catch (Exception ex)
            {
                tracing.Trace("CanResolveApproval: no se pudo leer la solicitud {0}: {1}", requestId, ex);
                Respond(context, tracing, false,
                    "La solicitud ya no existe o no se pudo leer. Actualice la bandeja e intente de nuevo.");
                return;
            }

            var previousStatus = request.GetAttributeValue<OptionSetValue>(Schema.ApprovalRequest.ApprovalStatus)?.Value;
            var creatorId = request.GetAttributeValue<EntityReference>(Schema.ApprovalRequest.CreatedBy)?.Id ?? Guid.Empty;

            // La membresia solo se consulta cuando de verdad decide: si ya esta resuelta, si
            // no hay creador o si el actor ES el creador, el dominio bloquea igual. Mismo
            // criterio que GuardReceptionPlugin, para no pagar la consulta ni disparar su
            // fail-closed de configuracion en un caso ya perdido.
            var isAuthorizedApprover = false;
            if (previousStatus == null && creatorId != Guid.Empty && creatorId != actor)
            {
                if (!TryResolveApprover(system, tracing, actor, out isAuthorizedApprover, out var membershipProblem))
                {
                    Respond(context, tracing, false, membershipProblem);
                    return;
                }
            }

            var verdict = ReceptionGuard.EvaluateApprovalUpdate(
                previousStatus, decisionInTarget: true, creatorId, actor, isAuthorizedApprover);

            tracing.Trace(
                "CanResolveApproval: prev={0} creador={1} actor={2} aprobador={3} => {4}.",
                previousStatus?.ToString() ?? "null", creatorId, actor, isAuthorizedApprover, verdict.IsAllowed);

            Respond(context, tracing, verdict.IsAllowed, verdict.IsAllowed ? string.Empty : verdict.Reason);
        }

        private static Guid ReadRequestId(IPluginExecutionContext context)
        {
            if (!context.InputParameters.TryGetValue(Schema.CanResolveApprovalApi.InApprovalRequestId, out var raw))
            {
                throw new InvalidPluginExecutionException(
                    $"CanResolveApproval: falta el parametro de entrada '{Schema.CanResolveApprovalApi.InApprovalRequestId}'.");
            }

            switch (raw)
            {
                case Guid g when g != Guid.Empty:
                    return g;
                case string s when Guid.TryParse(s, out var parsed) && parsed != Guid.Empty:
                    return parsed;
                default:
                    throw new InvalidPluginExecutionException(
                        $"CanResolveApproval: '{Schema.CanResolveApprovalApi.InApprovalRequestId}' no es un identificador valido.");
            }
        }

        /// <summary>
        /// Delega en <see cref="ApproverTeamLookup"/> pero TRADUCE su politica fail-closed:
        /// alli un problema de configuracion LANZA (bloquear es lo correcto en un guard),
        /// aca tiene que volver como dato. Todo el sentido de este pre-vuelo es que el
        /// motivo llegue a la app, y Power Fx descarta el mensaje de los faults de plugin.
        /// </summary>
        private static bool TryResolveApprover(
            IOrganizationService system, ITracingService tracing, Guid actor,
            out bool isApprover, out string problem)
        {
            isApprover = false;
            problem = string.Empty;

            try
            {
                isApprover = ApproverTeamLookup.IsApprover(system, tracing, actor);
                return true;
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace("CanResolveApproval: membresia no verificable: {0}", ex.Message);
                problem = ex.Message;
                return false;
            }
        }

        private static void Respond(
            IPluginExecutionContext context, ITracingService tracing, bool allowed, string message)
        {
            var outcome = allowed
                ? Schema.CanResolveApprovalApi.OutcomeAllowed
                : Schema.CanResolveApprovalApi.OutcomeBlocked;

            context.OutputParameters[Schema.CanResolveApprovalApi.OutOutcome] = outcome;
            context.OutputParameters[Schema.CanResolveApprovalApi.OutMessage] = message ?? string.Empty;
            tracing.Trace("CanResolveApproval: Outcome={0} Message='{1}'.", outcome, message);
        }
    }
}
