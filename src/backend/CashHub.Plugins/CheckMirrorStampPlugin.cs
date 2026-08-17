using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejos de cheques para el tracker, en un solo tipo:
    ///
    ///   B6 — sanic_bag en el cheque fisico: derivada de
    ///        sanic_relatedrecounterdepositid.sanic_bagid. La pantalla Tula trae
    ///        todos los cheques en UNA query delegable (Filter por columna de
    ///        entidad relacionada no delega; ForAll por deposito es N+1).
    ///   B3 — sanic_matchedcheck en el cheque DECLARADO: cuando el fisico setea o
    ///        limpia sanic_relateddeclaredcheck, el declarado recibe/pierde el
    ///        espejo inverso (la pantalla Correo navega declarado → tula).
    ///   B-9 — sanic_recountingsession en el cheque fisico: derivada de la SESION de
    ///        la tula (sanic_bag.sanic_recountingsession). scrResumen cuenta cheques
    ///        con CountRows DELEGADO por sesion; sin el espejo la cadena es
    ///        Cheque → Tula → Sesion (dot-walk de dos saltos, NO delega) y
    ///        materializar filas topa en el limite del cliente (500/2000) SIN error:
    ///        el conteo miente por omision (QA lo probo con 2.074 cheques).
    ///
    /// Cuatro pasos SINCRONOS:
    ///   - Pre-operation  (20) Create de sanic_chtblchecks: estampa B6 + B-9 (EN el
    ///     Target). B3 NO: en pre-op la fila del cheque todavia no existe.
    ///   - Post-operation (40) Create de sanic_chtblchecks: estampa B3, ya con la
    ///     fila del cheque creada.
    ///   - Pre-operation  (20) Update de sanic_chtblchecks, filtrado en
    ///     sanic_relatedrecounterdepositid + sanic_relateddeclaredcheck, con PreImage
    ///     (sanic_relateddeclaredcheck): re-estampa al re-colgar el cheque (B6+B-9+B3;
    ///     en Update la fila ya existe, el write-back no necesita post-op).
    ///   - Post-operation (40) Update de sanic_chtblbags, filtrado en
    ///     sanic_recountingsession y con PreImage de esa columna: CASCADA de la
    ///     sesion a TODOS los cheques de la tula, paginada de a 500.
    ///
    /// El Create esta PARTIDO en dos pasos por integridad referencial: B3 escribe en
    /// el cheque DECLARADO un lookup que apunta al cheque fisico, y en Pre-operation
    /// esa fila no existe — Dataverse responde 0x80040265 y tumba el Create entero
    /// (bug reproducido con 783 cheques en el stress de recuento).
    ///
    /// Por que la cascada vive ACA y no en BagReceptionMirrorPlugin (que ya despacha
    /// por entidad): la regla es "el plugin que ESCRIBE una columna espejo es el
    /// unico que la mantiene". sanic_recountingsession del CHEQUE lo escribe este
    /// tipo en el alta; si la re-asignacion de sesion la propagara otro, el espejo
    /// tendria dos duenos y la proxima mudanza de columna rompe en dos lugares.
    /// BagReceptionMirrorPlugin mantiene los espejos de la TULA — otra tabla destino.
    ///
    /// Las estampas B6/B-9 escriben EN el Target (cero escrituras extra); las
    /// escrituras B3 y la cascada van como sistema: son metadata derivada, no datos
    /// de negocio. La decision vive en el dominio puro (CheckMirrors).
    /// </summary>
    public sealed class CheckMirrorStampPlugin : IPlugin
    {
        /// <summary>Tamano de pagina de la cascada (mismo que los demas espejos).</summary>
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("CheckMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("CheckMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                var system = serviceFactory.CreateOrganizationService(null);

                if (context.PrimaryEntityName == Schema.Bag.LogicalName)
                {
                    CascadeSessionToChecks(system, tracing, context, target);
                }
                else if (context.PrimaryEntityName == Schema.Check.LogicalName)
                {
                    // B6/B-9 solo tienen sentido en el paso Pre-operation (escriben EN
                    // el Target); B3 se auto-filtra por mensaje+stage en el dominio.
                    if (CheckMirrors.ShouldStampTargetMirrors(context.Stage))
                    {
                        StampBagAndSession(system, tracing, context.MessageName, target);
                    }

                    StampMatchedCheck(system, tracing, context, target);
                }
                else
                {
                    tracing.Trace("CheckMirror: entidad {0} fuera de alcance, se omite.",
                        context.PrimaryEntityName);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("CheckMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudieron estampar los espejos del cheque: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// B6 + B-9: deriva la tula del deposito de recuento y la SESION de esa tula,
        /// y escribe las dos EN el Target. Son la misma cadena
        /// (deposito → tula → sesion): se resuelven en la misma pasada, con dos
        /// Retrieve como sistema y cero escrituras adicionales.
        /// </summary>
        private static void StampBagAndSession(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var depositTouched = target.Contains(Schema.Check.RelatedDepositLookup);
            if (!CheckMirrors.ShouldRestampBag(message, depositTouched))
            {
                tracing.Trace("CheckMirror: Update de cheque sin cambio de deposito, sin re-estampa.");
                return;
            }

            var depositRef = target.GetAttributeValue<EntityReference>(Schema.Check.RelatedDepositLookup);
            if (depositRef == null)
            {
                target[Schema.Check.BagLookup] = null;
                target[Schema.Check.SessionLookup] = null;
                tracing.Trace("CheckMirror: cheque sin deposito de recuento, tula y sesion en blanco.");
                return;
            }

            // Lectura como sistema: quien mueve el cheque (recontador) no
            // necesariamente lee la tula ni la sesion. Es derivacion, no permiso.
            var deposit = system.Retrieve(
                Schema.DepositRecounting.LogicalName, depositRef.Id,
                new ColumnSet("sanic_bagid"));
            var bagRef = deposit.GetAttributeValue<EntityReference>("sanic_bagid");

            target[Schema.Check.BagLookup] = bagRef;

            // Segundo salto: la sesion vive en la TULA, no en el deposito. Sin tula no
            // hay sesion posible: espejo en blanco explicito (no se deja stale).
            EntityReference sessionRef = null;
            if (bagRef != null)
            {
                var bag = system.Retrieve(
                    Schema.Bag.LogicalName, bagRef.Id,
                    new ColumnSet(Schema.Bag.RecountingSessionLookup));
                sessionRef = bag.GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup);
            }

            // Tula SIN sesion tambien es un valor: null limpio, nunca el anterior.
            target[Schema.Check.SessionLookup] = sessionRef;

            tracing.Trace("CheckMirror: cheque {0} ({1}) -> tula {2}, sesion {3}.",
                target.Id, message,
                bagRef != null ? bagRef.Id.ToString() : "null",
                sessionRef != null ? sessionRef.Id.ToString() : "null");
        }

        /// <summary>
        /// B3: estampa/limpia el espejo inverso en el (los) cheque(s) declarado(s).
        ///
        /// Escribe en OTRA tabla un lookup que apunta AL cheque fisico, asi que exige
        /// que la fila del cheque exista: en el Create eso recien pasa en
        /// Post-operation (40) — en Pre-operation Dataverse rechaza el lookup con
        /// 0x80040265 y se cae el Create entero. En el Update la fila ya existe y el
        /// write-back sigue viajando en el mismo paso Pre-operation.
        /// </summary>
        private static void StampMatchedCheck(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            if (!CheckMirrors.CanWriteBackMatch(context.MessageName, context.Stage))
            {
                tracing.Trace(
                    "CheckMirror: {0} en stage {1} — la fila del cheque todavia no existe; "
                    + "el MatchedCheck del declarado lo estampa el paso Post-operation (40).",
                    context.MessageName, context.Stage);
                return;
            }

            Guid? preDeclared = null;
            if (context.PreEntityImages.TryGetValue(Schema.Check.PreImageName, out var pre))
            {
                preDeclared = pre.GetAttributeValue<EntityReference>(Schema.Check.RelatedDeclaredCheck)?.Id;
            }

            var declaredTouched = target.Contains(Schema.Check.RelatedDeclaredCheck);
            var targetDeclared = target
                .GetAttributeValue<EntityReference>(Schema.Check.RelatedDeclaredCheck)?.Id;

            // Idempotencia: si el declarado ya apunta a este cheque, reescribirlo
            // ensucia su auditoria y re-dispara sus propios espejos por nada. Una
            // lectura de UNA columna es mas barata que la escritura que evita.
            Guid? targetDeclaredMatch = null;
            if (declaredTouched && targetDeclared.HasValue)
            {
                var declared = system.Retrieve(
                    Schema.DeclaredCheck.LogicalName, targetDeclared.Value,
                    new ColumnSet(Schema.DeclaredCheck.MatchedCheck));
                targetDeclaredMatch = declared
                    .GetAttributeValue<EntityReference>(Schema.DeclaredCheck.MatchedCheck)?.Id;
            }

            var updates = CheckMirrors.ResolveMatchUpdates(
                context.MessageName,
                context.Stage,
                checkId: target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId,
                declaredTouched: declaredTouched,
                targetDeclared: targetDeclared,
                preDeclared: preDeclared,
                targetDeclaredMatch: targetDeclaredMatch);

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

        /// <summary>
        /// B-9, lado TULA: si la tula se re-asigna a otra sesion de recuento, TODOS
        /// sus cheques quedan con el espejo stale y los CountRows por sesion de
        /// scrResumen empiezan a mentir. Reescribe el espejo en los cheques de esa
        /// tula, paginado, y SOLO si la sesion cambio de verdad (para eso la PreImage).
        /// </summary>
        private static void CascadeSessionToChecks(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            Entity pre = null;
            context.PreEntityImages.TryGetValue(Schema.Check.SessionCascadePreImageName, out pre);
            if (pre == null)
            {
                tracing.Trace(
                    "CheckMirror: falta la PreImage '{0}' del paso de cascada de sesion — "
                    + "no se puede saber si la sesion cambio, se omite.",
                    Schema.Check.SessionCascadePreImageName);
                return;
            }

            var sessionTouched = target.Contains(Schema.Bag.RecountingSessionLookup);

            var plan = CheckMirrors.ResolveSessionCascade(
                sessionTouched,
                newSessionId: target
                    .GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup)?.Id,
                previousSessionId: pre
                    .GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup)?.Id);

            if (!plan.Propagate)
            {
                tracing.Trace(
                    "CheckMirror: Update de tula sin cambio real de sesion "
                    + "(sesion-tocada={0}), no se toca ningun cheque.",
                    sessionTouched);
                return;
            }

            var bagId = target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId;

            var sessionValue = plan.SessionId.HasValue
                ? new EntityReference(Schema.RecountingSession.LogicalName, plan.SessionId.Value)
                : null;

            var query = new QueryExpression(Schema.Check.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize }
            };
            // Se filtra por el espejo B6 y no por el deposito: los dos espejos se
            // escriben SIEMPRE juntos, asi que "cheques con sanic_bag = esta tula" es
            // exactamente el conjunto cuyo espejo de sesion salio de esta tula. Un
            // cheque sin sanic_bag es un problema de B6 y lo arregla su re-estampa
            // (que tambien reescribe la sesion), no esta cascada.
            query.Criteria.AddCondition(Schema.Check.BagLookup, ConditionOperator.Equal, bagId);

            var updated = 0;
            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    // SOLO el espejo: asi el guard de cheques lo deja pasar aun con el
                    // cheque Recibido (congelado) — ver el bypass touchesOnlyMirror de
                    // GuardTransactionPlugin/CheckGuard.
                    system.Update(new Entity(Schema.Check.LogicalName, row.Id)
                    {
                        [Schema.Check.SessionLookup] = sessionValue
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
                "CheckMirror: tula {0} -> sesion {1}; {2} cheques re-espejados.",
                bagId,
                plan.SessionId.HasValue ? plan.SessionId.Value.ToString() : "null",
                updated);
        }
    }
}
