using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Validacion de CH-Tbl-AppCatalogAccess (una fila = un grupo AAD habilitado para una app).
    ///
    /// El hub resuelve visibilidad con estas filas: un GUID invalido rompe el gating en
    /// silencio y una fila duplicada (misma app, mismo grupo) ensucia la configuracion.
    /// Corre sincrono en Pre-operation de Create y Update: normaliza el GUID en el Target
    /// (trim + formato canonico) y bloquea duplicados leyendo como sistema.
    /// </summary>
    public sealed class GuardAppCatalogAccessPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("GuardAppCatalogAccess: sin Target de tipo Entity, se omite.");
                return;
            }

            var isCreate = string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase);

            // GUID del grupo: en Create es obligatorio (la plataforma no exige
            // ApplicationRequired via API); en Update solo se valida si viene en el request.
            Guid groupId;
            if (target.Contains(Schema.AppCatalogAccess.AadGroupId) || isCreate)
            {
                var rawGroup = target.GetAttributeValue<string>(Schema.AppCatalogAccess.AadGroupId);
                tracing.Trace("GuardAppCatalogAccess: {0} valida grupo='{1}'.", context.MessageName, rawGroup ?? "(null)");

                var result = AppCatalogGuard.EvaluateAccessGroupId(rawGroup, out groupId);
                if (!result.IsAllowed)
                {
                    tracing.Trace("GuardAppCatalogAccess: BLOQUEADO — {0}", result.Reason);
                    throw new InvalidPluginExecutionException(result.Reason);
                }

                // Normaliza el valor que se va a escribir (trim + formato canonico en minusculas).
                target[Schema.AppCatalogAccess.AadGroupId] = groupId.ToString("D");
            }
            else
            {
                groupId = ReadCurrentGroup(serviceFactory, target.Id);
                tracing.Trace("GuardAppCatalogAccess: Update sin grupo en el request; actual={0}.", groupId);
            }

            var appRef = target.GetAttributeValue<EntityReference>(Schema.AppCatalogAccess.AppCatalogLookup);
            var appId = appRef?.Id ?? Guid.Empty;
            if (appId == Guid.Empty)
            {
                if (isCreate)
                {
                    tracing.Trace("GuardAppCatalogAccess: BLOQUEADO — Create sin app.");
                    throw new InvalidPluginExecutionException(
                        "La fila de acceso debe apuntar a una app del catalogo (App Catalog).");
                }

                appId = ReadCurrentApp(serviceFactory, target.Id);
                tracing.Trace("GuardAppCatalogAccess: Update sin app en el request; actual={0}.", appId);
            }

            // Duplicados: misma app + mismo grupo en otra fila. Se lee como sistema (chequeo
            // de integridad independiente de los privilegios del actor).
            var service = serviceFactory.CreateOrganizationService(null);
            var dupQuery = new QueryExpression(Schema.AppCatalogAccess.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.AppCatalogAccess.Id),
                TopCount = 1
            };
            dupQuery.Criteria.AddCondition(
                Schema.AppCatalogAccess.AppCatalogLookup, ConditionOperator.Equal, appId);
            dupQuery.Criteria.AddCondition(
                Schema.AppCatalogAccess.AadGroupId, ConditionOperator.Equal, groupId.ToString("D"));
            if (!isCreate)
            {
                dupQuery.Criteria.AddCondition(
                    Schema.AppCatalogAccess.Id, ConditionOperator.NotEqual, target.Id);
            }

            if (service.RetrieveMultiple(dupQuery).Entities.Count > 0)
            {
                tracing.Trace("GuardAppCatalogAccess: BLOQUEADO — duplicado app={0} grupo={1}.", appId, groupId);
                throw new InvalidPluginExecutionException(
                    "Ya existe una fila de acceso para esa app con ese grupo AAD.");
            }

            tracing.Trace("GuardAppCatalogAccess: fila valida (app={0}, grupo={1}).", appId, groupId);
        }

        private static Guid ReadCurrentGroup(IOrganizationServiceFactory serviceFactory, Guid recordId)
        {
            var current = Retrieve(serviceFactory, recordId, Schema.AppCatalogAccess.AadGroupId);
            var value = current.GetAttributeValue<string>(Schema.AppCatalogAccess.AadGroupId);
            return Guid.TryParse(value?.Trim(), out var parsed) ? parsed : Guid.Empty;
        }

        private static Guid ReadCurrentApp(IOrganizationServiceFactory serviceFactory, Guid recordId)
        {
            var current = Retrieve(serviceFactory, recordId, Schema.AppCatalogAccess.AppCatalogLookup);
            return current.GetAttributeValue<EntityReference>(Schema.AppCatalogAccess.AppCatalogLookup)?.Id
                ?? Guid.Empty;
        }

        private static Entity Retrieve(IOrganizationServiceFactory serviceFactory, Guid recordId, string column)
        {
            var service = serviceFactory.CreateOrganizationService(null);
            return service.Retrieve(
                Schema.AppCatalogAccess.LogicalName, recordId, new ColumnSet(column));
        }
    }
}
