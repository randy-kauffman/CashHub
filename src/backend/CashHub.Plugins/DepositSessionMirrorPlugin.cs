using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejo E-1 de CH-App-Recounting v2 (01 §2.2):
    ///
    ///   sanic_recountingsessionmirror (lookup -> sanic_chtblrecountingsession) en
    ///   sanic_chtbldepositsrecounting, derivado de sanic_bagid.sanic_recountingsession.
    ///
    /// Tres pasos SINCRONOS:
    ///   - Pre-operation  (20) Create de sanic_chtbldepositsrecounting: estampa el
    ///     espejo EN el Target (cero escrituras adicionales).
    ///   - Pre-operation  (20) Update de sanic_chtbldepositsrecounting, filtrado en
    ///     sanic_bagid: re-estampa al re-colgar el deposito de otra tula.
    ///   - Post-operation (40) Update de sanic_chtblbags, filtrado en
    ///     sanic_recountingsession y con PreImage de esa columna: CASCADA de la sesion
    ///     a TODOS los depositos de la tula, paginada de a 500.
    ///
    /// Por que la cascada es OBLIGATORIA y no alcanza el Create: la app patchea
    /// Bags.RecountingSession al ABRIR la tula (03 §6.3), antes de que scrConteo cree
    /// ningun deposito — asi que el camino feliz lo cubre el Create. Pero RE-abrir una
    /// tula en otra sesion (btnReabrirTul) vuelve a patchear la misma columna con los
    /// depositos YA creados: sin cascada quedan colgando de la sesion vieja y el rail
    /// de KPIs de scrTulas suma plata de otra sesion. Con la cascada, el conjunto
    /// "depositos con RecountingSessionMirror = S" es EXACTAMENTE el que devuelve el
    /// fallback sin backend de 01 §2.2 (depositos de las tulas que hoy estan en S).
    ///
    /// Por que existe la columna: los 12 Sum(Filter(..., BagID.RecountingSession...))
    /// del rail de scrTulas son dot-walk de DOS saltos y NO delegan. Power Fx se trae
    /// el tope de filas del cliente (500/2000), suma lo que trajo y muestra una cifra
    /// PARCIAL de dinero SIN error (bug D-2). La decision de cuando estampar y que
    /// propagar vive en el dominio puro (RecountingMirrors).
    /// </summary>
    public sealed class DepositSessionMirrorPlugin : IPlugin
    {
        /// <summary>Tamano de pagina de la cascada (mismo que los demas espejos).</summary>
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("DepositSessionMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("DepositSessionMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                var system = serviceFactory.CreateOrganizationService(null);

                if (context.PrimaryEntityName == Schema.Bag.LogicalName)
                {
                    CascadeSessionToDeposits(system, tracing, context, target);
                }
                else if (context.PrimaryEntityName == Schema.DepositRecounting.LogicalName)
                {
                    StampSession(system, tracing, context.MessageName, target);
                }
                else
                {
                    tracing.Trace("DepositSessionMirror: entidad {0} fuera de alcance, se omite.",
                        context.PrimaryEntityName);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("DepositSessionMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo mantener el espejo de la sesion de recuento en el deposito (E-1): "
                    + ex.Message, ex);
            }
        }

        /// <summary>
        /// Resuelve la tula del Target, lee su sesion y escribe el espejo EN el propio
        /// Target: UNA sola lectura y CERO escrituras adicionales.
        /// </summary>
        private static void StampSession(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var bagTouched = target.Contains(Schema.DepositRecounting.BagLookup);

            if (!RecountingMirrors.ShouldStamp(message, bagTouched))
            {
                tracing.Trace(
                    "DepositSessionMirror: Update de deposito sin cambio de tula, sin re-estampa.");
                return;
            }

            var bagRef = target.GetAttributeValue<EntityReference>(Schema.DepositRecounting.BagLookup);

            if (bagRef == null)
            {
                // Sin tula no hay sesion posible: espejo en blanco EXPLICITO, nunca el
                // anterior (un espejo stale suma plata en la sesion equivocada).
                target[Schema.DepositRecounting.SessionMirror] = null;
                tracing.Trace("DepositSessionMirror: deposito sin tula, espejo de sesion en blanco.");
                return;
            }

            // Lectura como sistema: es una DERIVACION, no un permiso de negocio — el
            // cajero que crea el deposito no necesariamente lee la ficha de la tula.
            var bag = system.Retrieve(
                Schema.Bag.LogicalName, bagRef.Id,
                new ColumnSet(Schema.Bag.RecountingSessionLookup));

            var sessionRef = bag.GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup);

            target[Schema.DepositRecounting.SessionMirror] = sessionRef;

            tracing.Trace("DepositSessionMirror: deposito {0} ({1}) -> tula {2}, sesion {3}.",
                target.Id, message, bagRef.Id,
                sessionRef != null ? sessionRef.Id.ToString() : "null");
        }

        /// <summary>
        /// Cascada del lado TULA: reescribe el espejo en TODOS los depositos de la
        /// tula, paginado, y SOLO si la sesion cambio de verdad (para eso la PreImage:
        /// Dataverse dispara un paso filtrado aunque se reescriba el mismo valor).
        /// </summary>
        private static void CascadeSessionToDeposits(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            Entity pre;
            if (!context.PreEntityImages.TryGetValue(Schema.Bag.PreImageName, out pre) || pre == null)
            {
                tracing.Trace(
                    "DepositSessionMirror: falta la PreImage '{0}' del paso de cascada — "
                    + "no se puede saber si la sesion cambio, se omite.",
                    Schema.Bag.PreImageName);
                return;
            }

            var sessionTouched = target.Contains(Schema.Bag.RecountingSessionLookup);

            var plan = RecountingMirrors.ResolveLookupCascade(
                sessionTouched,
                newId: target
                    .GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup)?.Id,
                previousId: pre
                    .GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup)?.Id);

            if (!plan.Propagate)
            {
                tracing.Trace(
                    "DepositSessionMirror: Update de tula sin cambio real de sesion "
                    + "(sesion-tocada={0}), no se toca ningun deposito.",
                    sessionTouched);
                return;
            }

            var bagId = target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId;

            var sessionValue = plan.ValueId.HasValue
                ? new EntityReference(Schema.RecountingSession.LogicalName, plan.ValueId.Value)
                : null;

            var query = new QueryExpression(Schema.DepositRecounting.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize }
            };
            // Se filtra por la TULA REAL del deposito (sanic_bagid), no por el espejo:
            // a diferencia del cheque —cuya tula es ella misma un espejo—, aca el
            // vinculo de negocio esta en la propia fila y es la fuente de verdad.
            query.Criteria.AddCondition(
                Schema.DepositRecounting.BagLookup, ConditionOperator.Equal, bagId);

            var updated = 0;
            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    // SOLO el espejo: nada de tocar columnas de negocio del deposito.
                    system.Update(new Entity(Schema.DepositRecounting.LogicalName, row.Id)
                    {
                        [Schema.DepositRecounting.SessionMirror] = sessionValue
                    });
                    updated++;
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace(
                "DepositSessionMirror: tula {0} -> sesion {1}; {2} depositos re-espejados.",
                bagId,
                plan.ValueId.HasValue ? plan.ValueId.Value.ToString() : "null",
                updated);
        }
    }
}
