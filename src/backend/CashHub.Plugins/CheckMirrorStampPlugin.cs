using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejos de cheques para el tracker, en un solo paso del pipeline:
    ///
    ///   B6 — sanic_bag en el cheque fisico: derivada de
    ///        sanic_relatedrecounterdepositid.sanic_bagid. La pantalla Tula trae
    ///        todos los cheques en UNA query delegable (Filter por columna de
    ///        entidad relacionada no delega; ForAll por deposito es N+1).
    ///   B3 — sanic_matchedcheck en el cheque DECLARADO: cuando el fisico setea o
    ///        limpia sanic_relateddeclaredcheck, el declarado recibe/pierde el
    ///        espejo inverso (la pantalla Correo navega declarado → tula).
    ///
    /// Pre-operation (20) SINCRONO en Create y Update de sanic_chtblchecks (Update
    /// filtrado en sanic_relatedrecounterdepositid + sanic_relateddeclaredcheck),
    /// con PreImage (sanic_relateddeclaredcheck). La estampa B6 escribe EN el
    /// Target; las escrituras B3 sobre el declarado van como sistema. La decision
    /// vive en el dominio puro (CheckMirrors).
    /// </summary>
    public sealed class CheckMirrorStampPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("CheckMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                var system = serviceFactory.CreateOrganizationService(null);

                StampBag(system, tracing, context.MessageName, target);
                StampMatchedCheck(system, tracing, context, target);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("CheckMirror: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudieron estampar los espejos del cheque: {ex.Message}", ex);
            }
        }

        /// <summary>B6: deriva la tula del deposito de recuento y la escribe en el Target.</summary>
        private static void StampBag(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var depositTouched = target.Contains(Schema.Check.RelatedDepositLookup);
            if (!CheckMirrors.ShouldRestampBag(message, depositTouched))
            {
                return;
            }

            var depositRef = target.GetAttributeValue<EntityReference>(Schema.Check.RelatedDepositLookup);
            if (depositRef == null)
            {
                target[Schema.Check.BagLookup] = null;
                tracing.Trace("CheckMirror: cheque sin deposito de recuento, tula en blanco.");
                return;
            }

            var deposit = system.Retrieve(
                Schema.DepositRecounting.LogicalName, depositRef.Id,
                new ColumnSet("sanic_bagid"));
            var bagRef = deposit.GetAttributeValue<EntityReference>("sanic_bagid");

            target[Schema.Check.BagLookup] = bagRef;
            tracing.Trace("CheckMirror: cheque {0} ({1}) -> tula {2}.",
                target.Id, message, bagRef != null ? bagRef.Id.ToString() : "null");
        }

        /// <summary>B3: estampa/limpia el espejo inverso en el (los) cheque(s) declarado(s).</summary>
        private static void StampMatchedCheck(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            Guid? preDeclared = null;
            if (context.PreEntityImages.TryGetValue(Schema.Check.PreImageName, out var pre))
            {
                preDeclared = pre.GetAttributeValue<EntityReference>(Schema.Check.RelatedDeclaredCheck)?.Id;
            }

            var updates = CheckMirrors.ResolveMatchUpdates(
                context.MessageName,
                checkId: target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId,
                declaredTouched: target.Contains(Schema.Check.RelatedDeclaredCheck),
                targetDeclared: target.GetAttributeValue<EntityReference>(Schema.Check.RelatedDeclaredCheck)?.Id,
                preDeclared: preDeclared);

            foreach (var update in updates)
            {
                system.Update(new Entity(Schema.DeclaredCheck.LogicalName, update.DeclaredCheckId)
                {
                    [Schema.DeclaredCheck.MatchedCheck] = update.MatchedCheckId.HasValue
                        ? new EntityReference(Schema.Check.LogicalName, update.MatchedCheckId.Value)
                        : null
                });
                tracing.Trace("CheckMirror: declarado {0} -> match {1}.",
                    update.DeclaredCheckId,
                    update.MatchedCheckId.HasValue ? update.MatchedCheckId.Value.ToString() : "limpio");
            }
        }
    }
}
