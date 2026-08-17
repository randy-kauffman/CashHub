using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Punto 1 del guard de CH-App-Recounting v2 (00 §2.2): la auto-aprobacion de la
    /// sesion de recuento por MESA PROPIA pasa a decidirse server-side. Mata S-1.
    ///
    /// Lo que hacia la v1 (Screen1.fx.yaml, Form1.OnSuccess):
    ///
    ///     If(varIsOwnDesk,
    ///        Patch('CH-Tbl-RecountingSessions', ..., {Confirmed: true});
    ///        Patch('CH-Tbl-ApprovalRequests', ..., {..., ApprovalStatus: Aprobado}),
    ///        ...)
    ///
    /// `varIsOwnDesk` se calculaba en el dispositivo del cajero comparando la mesa
    /// elegida contra un LookUp de mesas por `Cajero.User = gvarUser.User`. Con
    /// `gvarUser` en blanco ese LookUp devolvia la PRIMERA mesa sin cajero y cualquiera
    /// entraba auto-aprobado; y aun con la app sana, escribir `ApprovalStatus` desde el
    /// cliente significa que la Web API tambien puede. El atajo de negocio se conserva
    /// —es deliberado—, pero ahora lo evalua el servidor con datos que el cliente no
    /// controla: la mesa sale de la sesion referenciada (leida como SISTEMA) y el
    /// usuario sale de `context.InitiatingUserId`.
    ///
    /// DOS pasos SINCRONOS sobre el Create de sanic_chtblapprovalrequest:
    ///   - Pre-operation  (20): estampa la resolucion EN EL MISMO Create
    ///     (ApprovalStatus = Aprobado, SolvedBy, ResolutionDatetime). Cero escrituras
    ///     extra y cero ventana en la que la solicitud existe sin resolver.
    ///   - Post-operation (40): confirma la sesion (Confirmed + ConfirmedBy). Va
    ///     DESPUES a proposito: si el Create se cae, la sesion no queda confirmada
    ///     colgando de una solicitud que no existe.
    ///
    /// Convive con GuardReceptionPlugin (Pre-validation, stage 10), que corre ANTES y
    /// ya bloquea/degrada la solicitud que intenta NACER resuelta desde el cliente.
    /// Este plugin es el unico autor legitimo de esas tres columnas para este tipo.
    /// </summary>
    public sealed class RecountingSessionAutoApprovePlugin : IPlugin
    {
        /// <summary>
        /// Bandera Pre-op -> Post-op. Vive en SharedVariables (misma ejecucion, mismo
        /// pipeline) y NUNCA en un campo de instancia: la plataforma reusa la instancia
        /// del plugin entre ejecuciones y usuarios.
        /// </summary>
        private const string ConfirmSessionKey = "CashHub.Recounting.AutoApprovedSessionId";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("RecountingAutoApprove: {0} de {1}, stage={2}, actor={3}, depth={4}.",
                context.MessageName, context.PrimaryEntityName, context.Stage,
                context.InitiatingUserId, context.Depth);

            try
            {
                if (context.Stage == 40)
                {
                    ConfirmSession(context, serviceFactory, tracing);
                    return;
                }

                StampApproval(context, serviceFactory, tracing);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("RecountingAutoApprove: error inesperado en stage {0}: {1}", context.Stage, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo resolver la auto-aprobacion de la sesion de recuento: " + ex.Message, ex);
            }
        }

        // ---- Pre-operation (20): la resolucion viaja EN el Create ----

        private static void StampApproval(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("RecountingAutoApprove: sin Target de tipo Entity, se omite.");
                return;
            }

            var requestType = target.GetAttributeValue<string>(Schema.ApprovalRequest.RequestType);

            // Comparacion ORDINAL exacta (01 §5.6): el tipo es texto libre y una
            // variante de tipeo NO puede invocar el atajo.
            if (!string.Equals(requestType, RecountingGuard.RequestTypeSesionRecuento, StringComparison.Ordinal))
            {
                tracing.Trace("RecountingAutoApprove: tipo '{0}' fuera de alcance, se omite.",
                    requestType ?? "(sin tipo)");
                return;
            }

            // Este plugin es el UNICO autor de la resolucion para este tipo: lo que
            // venga del cliente se descarta antes de decidir nada. GuardReceptionPlugin
            // ya lo bloquea/degrada en stage 10; esto es la segunda cerradura.
            var clientTriedToResolve = target.Contains(Schema.ApprovalRequest.ApprovalStatus)
                                       || target.Contains(Schema.ApprovalRequest.SolvedBy)
                                       || target.Contains(Schema.ApprovalRequest.ResolutionDatetime);
            if (clientTriedToResolve)
            {
                tracing.Trace(
                    "RecountingAutoApprove: el request traia sellos de resolucion; se descartan " +
                    "(la resolucion la decide el servidor).");
                target.Attributes.Remove(Schema.ApprovalRequest.ApprovalStatus);
                target.Attributes.Remove(Schema.ApprovalRequest.SolvedBy);
                target.Attributes.Remove(Schema.ApprovalRequest.ResolutionDatetime);
            }

            var sessionId = ReadSessionId(
                target.GetAttributeValue<string>(SupervisorMirrors.ApprovalRequest.RequestResources), tracing);

            var system = serviceFactory.CreateOrganizationService(null);
            var deskCashierId = ResolveDeskCashier(system, tracing, sessionId);

            var decision = RecountingGuard.ShouldAutoApprove(
                requestType, deskCashierId, context.InitiatingUserId);

            tracing.Trace("RecountingAutoApprove: sesion={0} cajero-mesa={1} actor={2} => {3}.",
                sessionId,
                deskCashierId.HasValue ? deskCashierId.Value.ToString() : "null",
                context.InitiatingUserId,
                decision);

            if (decision != RecountingAutoApproval.AutoApprove)
            {
                // Mesa ajena o no verificable: la solicitud nace PENDIENTE y la firma un
                // supervisor. No se bloquea nada — pedir una mesa ajena es legitimo.
                return;
            }

            target[Schema.ApprovalRequest.ApprovalStatus] =
                new OptionSetValue(ReceptionGuard.ApprovalAprobado);
            target[Schema.ApprovalRequest.SolvedBy] =
                new EntityReference(Schema.SystemUser.LogicalName, context.InitiatingUserId);
            target[Schema.ApprovalRequest.ResolutionDatetime] = DateTime.UtcNow;

            context.SharedVariables[ConfirmSessionKey] = sessionId.ToString();

            tracing.Trace("RecountingAutoApprove: mesa propia — solicitud estampada APROBADA.");
        }

        // ---- Post-operation (40): la sesion queda confirmada ----

        private static void ConfirmSession(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            if (!context.SharedVariables.TryGetValue(ConfirmSessionKey, out var rawId)
                || !Guid.TryParse(Convert.ToString(rawId), out var sessionId)
                || sessionId == Guid.Empty)
            {
                tracing.Trace("RecountingAutoApprove: la solicitud no se auto-aprobo, la sesion queda sin confirmar.");
                return;
            }

            // Como sistema: confirmar la sesion es la consecuencia de una decision que
            // ya tomo el servidor, no un permiso de negocio del cajero (que ademas, por
            // 01 §2.4, NUNCA escribe Confirmed).
            var system = serviceFactory.CreateOrganizationService(null);
            system.Update(new Entity(Schema.RecountingSession.LogicalName, sessionId)
            {
                [Schema.RecountingSession.Confirmed] = true,
                [Schema.RecountingSession.ConfirmedBy] =
                    new EntityReference(Schema.SystemUser.LogicalName, context.InitiatingUserId),
            });

            tracing.Trace("RecountingAutoApprove: sesion {0} confirmada por {1}.",
                sessionId, context.InitiatingUserId);
        }

        // ---- Resolucion de la sesion y su mesa ----

        /// <summary>
        /// GUID de la sesion desde el JSON de RequestResources. A diferencia del Create
        /// generico de GuardReceptionPlugin —que DEGRADA a pendiente cuando el JSON no
        /// se puede leer—, aca se LANZA: en la v2 este JSON lo arma la propia app con
        /// `JSON({RecountingSessionGUID: ...})` inmediatamente despues de crear la
        /// sesion (01 §5.7). Un blob ilegible en este tipo no es un productor viejo:
        /// es una solicitud que nadie va a poder resolver, porque el Supervisor lee la
        /// sesion exactamente de la misma clave. Dejarla nacer seria dejar al cajero
        /// esperando para siempre a una cola que no lo puede atender.
        /// </summary>
        private static Guid ReadSessionId(string? requestResources, ITracingService tracing)
        {
            var raw = FlatJson.Value(requestResources, ApprovalRequestResources.RecountingSessionGuidKey);

            if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var sessionId) || sessionId == Guid.Empty)
            {
                // NO se vuelca el JSON en la traza: es semi-publica y puede traer datos
                // de negocio. Se dice QUE falta, no que decia.
                tracing.Trace("RecountingAutoApprove: RequestResources sin {0} legible.",
                    ApprovalRequestResources.RecountingSessionGuidKey);
                throw new InvalidPluginExecutionException(RecountingGuard.UnreadableResourcesMessage);
            }

            return sessionId;
        }

        /// <summary>
        /// Cajero de la mesa de la sesion, leido COMO SISTEMA en dos saltos
        /// (sesion -> mesa -> cajero). Es un chequeo de seguridad de solo lectura: no
        /// puede depender de si el cajero tiene privilegio de leer la ficha de la mesa.
        ///
        /// La sesion inexistente LANZA (la solicitud apuntaria a la nada). La mesa
        /// ausente o sin cajero NO lanza: devuelve null y el dominio deja la solicitud
        /// pendiente para el supervisor.
        /// </summary>
        private static Guid? ResolveDeskCashier(
            IOrganizationService system, ITracingService tracing, Guid sessionId)
        {
            Entity session;
            try
            {
                session = system.Retrieve(
                    Schema.RecountingSession.LogicalName, sessionId,
                    new ColumnSet(Schema.RecountingSession.CountingDesk));
            }
            catch (Exception ex)
            {
                tracing.Trace("RecountingAutoApprove: no se pudo leer la sesion {0}: {1}", sessionId, ex.Message);
                throw new InvalidPluginExecutionException(RecountingGuard.SessionNotFoundMessage(sessionId));
            }

            var deskRef = session.GetAttributeValue<EntityReference>(Schema.RecountingSession.CountingDesk);
            if (deskRef == null)
            {
                tracing.Trace("RecountingAutoApprove: sesion {0} sin mesa; no verificable.", sessionId);
                return null;
            }

            Entity desk;
            try
            {
                desk = system.Retrieve(
                    Schema.CountingDesk.LogicalName, deskRef.Id,
                    new ColumnSet(Schema.CountingDesk.Cajero));
            }
            catch (Exception ex)
            {
                tracing.Trace("RecountingAutoApprove: no se pudo leer la mesa {0}: {1}", deskRef.Id, ex.Message);
                return null;
            }

            return desk.GetAttributeValue<EntityReference>(Schema.CountingDesk.Cajero)?.Id;
        }
    }
}
