using System;
using System.Collections.Generic;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Puntos 2 y 3 del guard de CH-App-Recounting v2 (00 §2.2): el CIERRE de la tula
    /// deja de calcularse en el dispositivo del cajero. Mata S-2a y S-2b.
    ///
    /// Lo que hacia la v1 (Screen1.fx.yaml, dialog_ConfirmarTerminarTula.OnButtonSelect):
    ///
    ///     Patch('CH-Tbl-Bags', gvarSelectedTula, {
    ///         TotalNIO: Sum(Filter(colRecountedDeposits, Currency = NIO),
    ///                       CashAmount + OtherLocaCheckAmount + BACLocalCheckAmount),
    ///         ... , BagStatus: Recontada});
    ///     ForAll(colRecountedDeposits As Recounted,
    ///         Patch('CH-Tbl-Transactions', Defaults(...), { ... }))
    ///
    /// `colRecountedDeposits` es una COLECCION EN MEMORIA del cliente. Power Fx la
    /// materializa hasta el tope de filas (500/2000) y trunca SIN error: la tula podia
    /// cerrarse declarando menos plata de la que tenia, y los asientos contables del
    /// banco se armaban con esa misma verdad parcial. Todo eso ademas es reproducible
    /// por Web API por cualquiera con el rol.
    ///
    /// DOS pasos SINCRONOS sobre el Update de sanic_chtblbags, filtrados en
    /// sanic_bagstatus y con PreImage de esa columna:
    ///
    ///   - Pre-operation  (20) TOTALES: al entrar a Recontada o a Pendiente de revision
    ///     por supervisor - recuento, recalcula TotalNIO/USD/EUR sumando los depositos
    ///     PERSISTIDOS de la tula (query server-side por sanic_bagid) y los PISA en el
    ///     Target. Lo que mande el cliente en esas tres columnas se ignora: el guard es
    ///     el unico autor.
    ///
    ///   - Post-operation (40) ASIENTOS: al entrar a Recontada (solo el cierre limpio;
    ///     la tula que va a revision todavia no tiene decision), crea un
    ///     CH-Tbl-Transactions por deposito con la MISMA semantica de la v1.
    ///     🧨 IDEMPOTENTE: la tula puede volver a Recontada tras un rechazo y reconteo,
    ///     y cada vuelta duplicaria la plata. Antes de crear se consulta que asientos ya
    ///     existen para los depositos de la tula (por RelatedDepositRecounting).
    ///
    /// Las reglas viven en el dominio puro (<see cref="RecountingGuard"/>); aca solo se
    /// arma el contexto. La PreImage es OBLIGATORIA: sin ella no se sabe si el estado
    /// CAMBIO, y Dataverse dispara el paso filtrado aunque se reescriba el mismo valor.
    /// </summary>
    public sealed class BagRecountClosurePlugin : IPlugin
    {
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("BagRecountClosure: {0} de {1}, stage={2}, actor={3}, depth={4}.",
                context.MessageName, context.PrimaryEntityName, context.Stage,
                context.InitiatingUserId, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("BagRecountClosure: sin Target de tipo Entity, se omite.");
                return;
            }

            var statusInTarget = target.Contains(Schema.Bag.BagStatus);
            var newStatus = statusInTarget
                ? target.GetAttributeValue<OptionSetValue>(Schema.Bag.BagStatus)?.Value
                : null;

            int? previousStatus = null;
            if (context.PreEntityImages.TryGetValue(Schema.Bag.PreImageName, out var pre) && pre != null)
            {
                previousStatus = pre.GetAttributeValue<OptionSetValue>(Schema.Bag.BagStatus)?.Value;
            }
            else
            {
                // Sin PreImage no se puede saber si el estado cambio. Se avisa fuerte y
                // se sigue con previousStatus=null: el dominio trata "sin estado previo"
                // como transicion real, que es el comportamiento seguro (recalcula de
                // mas, nunca de menos). El paso mal registrado se ve en la traza.
                tracing.Trace(
                    "BagRecountClosure: FALTA la PreImage '{0}' del paso — el estado previo se " +
                    "asume desconocido. Revisar el registro del paso.",
                    Schema.Bag.PreImageName);
            }

            var bagId = target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId;

            try
            {
                if (context.Stage == 40)
                {
                    if (!RecountingGuard.ShouldBuildTransactions(previousStatus, statusInTarget, newStatus))
                    {
                        tracing.Trace("BagRecountClosure: tula {0} prev={1} nuevo={2} — sin asientos que crear.",
                            bagId, Fmt(previousStatus), Fmt(newStatus));
                        return;
                    }

                    CreateTransactions(serviceFactory, tracing, bagId);
                    return;
                }

                if (!RecountingGuard.ShouldRecomputeTotals(previousStatus, statusInTarget, newStatus))
                {
                    tracing.Trace("BagRecountClosure: tula {0} prev={1} nuevo={2} — sin recalculo de totales.",
                        bagId, Fmt(previousStatus), Fmt(newStatus));
                    return;
                }

                StampTotals(serviceFactory, tracing, target, bagId);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BagRecountClosure: error inesperado en stage {0} de la tula {1}: {2}",
                    context.Stage, bagId, ex);
                throw new InvalidPluginExecutionException(
                    context.Stage == 40
                        ? "No se pudieron generar los asientos contables del recuento de la tula: " + ex.Message
                        : "No se pudieron recalcular los totales recontados de la tula: " + ex.Message,
                    ex);
            }
        }

        // ---- Pre-operation (20): totales ----

        private static void StampTotals(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Entity target,
            Guid bagId)
        {
            if (target.Contains(Schema.Bag.TotalNio)
                || target.Contains(Schema.Bag.TotalUsd)
                || target.Contains(Schema.Bag.TotalEur))
            {
                // 🧨 El guard PISA. La app v2 no escribe estas columnas (01 §2.4); si
                // alguien las manda, es la Web API o un cliente viejo: se descartan.
                tracing.Trace("BagRecountClosure: el request traia totales; se descartan y se recalculan.");
            }

            var system = serviceFactory.CreateOrganizationService(null);

            var deposits = new List<DepositAmounts>();
            var query = new QueryExpression(Schema.DepositRecounting.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.DepositRecounting.Currency,
                    Schema.DepositRecounting.CashAmount,
                    Schema.DepositRecounting.BacCheckAmount,
                    Schema.DepositRecounting.OtherCheckAmount),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize },
            };
            // Por la tula REAL del deposito (sanic_bagid), no por el espejo E-1: el
            // vinculo de negocio vive en la propia fila y es la fuente de verdad.
            query.Criteria.AddCondition(Schema.DepositRecounting.BagLookup, ConditionOperator.Equal, bagId);

            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    deposits.Add(new DepositAmounts
                    {
                        Currency = row.GetAttributeValue<OptionSetValue>(Schema.DepositRecounting.Currency)?.Value,
                        CashAmount = ReadDecimal(row, Schema.DepositRecounting.CashAmount),
                        BacCheckAmount = ReadDecimal(row, Schema.DepositRecounting.BacCheckAmount),
                        OtherCheckAmount = ReadDecimal(row, Schema.DepositRecounting.OtherCheckAmount),
                    });
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            var totals = RecountingGuard.ComputeTotals(deposits);

            target[Schema.Bag.TotalNio] = totals.Nio;
            target[Schema.Bag.TotalUsd] = totals.Usd;
            target[Schema.Bag.TotalEur] = totals.Eur;

            tracing.Trace("BagRecountClosure: tula {0} — {1} depositos, totales NIO/USD/EUR estampados.",
                bagId, deposits.Count);
        }

        // ---- Post-operation (40): asientos contables ----

        private static void CreateTransactions(
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing,
            Guid bagId)
        {
            var system = serviceFactory.CreateOrganizationService(null);

            var alreadyPosted = ReadPostedDeposits(system, tracing, bagId);
            var deposits = ReadDepositsForTransactions(system, bagId);

            var plans = RecountingGuard.BuildTransactions(deposits, alreadyPosted);

            // EL CAJERO SALE DE LA SESION DE LA TULA, no de context.InitiatingUserId.
            // Parece lo mismo —el cajero es quien cierra— pero no lo es: el mismo cierre
            // lo puede disparar el supervisor al resolver una revision, y ahi el actor es
            // el supervisor. La sesion sabe de quien era la tula; el ejecutor, no.
            var cajero = ReadBagCashier(system, tracing, bagId);

            tracing.Trace("BagRecountClosure: tula {0} — {1} depositos, {2} ya asentados, {3} asientos nuevos.",
                bagId, deposits.Count, alreadyPosted.Count, plans.Count);

            foreach (var plan in plans)
            {
                var transaction = new Entity(Schema.Transaction.LogicalName)
                {
                    [Schema.Transaction.CashAmount] = plan.CashAmount,
                    [Schema.Transaction.BacCheckAmount] = plan.BacCheckAmount,
                    [Schema.Transaction.OtherCheckAmount] = plan.OtherCheckAmount,
                    [Schema.Transaction.TransactionType] = new OptionSetValue(plan.TransactionType),
                    [Schema.Transaction.ProcessingType] = new OptionSetValue(plan.ProcessingType),
                    [Schema.Transaction.RelatedDepositRecounting] =
                        new EntityReference(Schema.DepositRecounting.LogicalName, plan.DepositId),
                };

                if (cajero != null)
                {
                    transaction[Schema.Transaction.Cajero] =
                        new EntityReference(Schema.SystemUser.LogicalName, cajero.Value);
                }

                if (plan.Currency.HasValue)
                {
                    transaction[Schema.Transaction.Currency] = new OptionSetValue(plan.Currency.Value);
                }

                if (plan.AccountNumber.HasValue)
                {
                    transaction[Schema.Transaction.AccountNumber] = plan.AccountNumber.Value;
                }

                if (!string.IsNullOrWhiteSpace(plan.TransactionReference))
                {
                    transaction[Schema.Transaction.TransactionReference] = plan.TransactionReference;
                }

                if (plan.TransactionNumber.HasValue)
                {
                    transaction[Schema.Transaction.TransactionNumber] = plan.TransactionNumber.Value;
                }

                system.Create(transaction);
            }

            tracing.Trace("BagRecountClosure: tula {0} — {1} asientos creados.", bagId, plans.Count);
        }

        /// <summary>
        /// Depositos de la tula que YA tienen asiento DE TIPO Deposito. Se resuelve en
        /// UNA query con link-entity (transaccion -> deposito -> tula) en vez de un `In`
        /// con la lista de depositos: una tula grande desbordaria el filtro.
        ///
        /// 🧨 La clave es (deposito, tipo), NO (deposito): es EXACTAMENTE la misma con
        /// la que AccountingGuard.EvaluateAccountingCreate rechaza duplicados
        /// (AccountingEntryExists(recuento, tipo)). Con la clave solo por deposito, un
        /// recuento que ya tiene un Sobrante o un Faltante del circuito del supervisor
        /// se leeria como "ya asentado" y su asiento de Deposito no se crearia nunca.
        ///
        /// Esta consulta NO reemplaza al chequeo de AccountingGuard: lo ANTICIPA. Sin
        /// ella, el Create duplicado lanzaria y voltearia la transicion entera de la
        /// tula en vez de saltear el asiento que ya existe.
        /// </summary>
        private static HashSet<Guid> ReadPostedDeposits(
            IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            var posted = new HashSet<Guid>();

            var query = new QueryExpression(Schema.Transaction.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Transaction.RelatedDepositRecounting),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize },
            };
            query.Criteria.AddCondition(
                Schema.Transaction.TransactionType, ConditionOperator.Equal,
                RecountingGuard.TransactionTypeDeposito);
            var link = query.AddLink(
                Schema.DepositRecounting.LogicalName,
                Schema.Transaction.RelatedDepositRecounting,
                Schema.DepositRecounting.Id);
            link.LinkCriteria.AddCondition(
                Schema.DepositRecounting.BagLookup, ConditionOperator.Equal, bagId);

            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    var depositRef = row.GetAttributeValue<EntityReference>(
                        Schema.Transaction.RelatedDepositRecounting);
                    if (depositRef != null)
                    {
                        posted.Add(depositRef.Id);
                    }
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace(
                "BagRecountClosure: idempotencia — {0} depositos de la tula ya tienen asiento de tipo Deposito.",
                posted.Count);

            return posted;
        }

        /// <summary>
        /// Depositos de la tula con los datos del declarado enlazado en la MISMA query
        /// (link-entity outer): la v1 hacia un dot-walk por fila
        /// (`Recounted.RelatedDeclaredDeposit.DepositReference`), que server-side seria
        /// un N+1. Se usa SOLO el lado recontado -> declarado, igual que la v1.
        /// </summary>
        private static List<DepositForTransaction> ReadDepositsForTransactions(
            IOrganizationService system, Guid bagId)
        {
            const string DeclaredAlias = "dec";

            var deposits = new List<DepositForTransaction>();

            var query = new QueryExpression(Schema.DepositRecounting.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.DepositRecounting.Currency,
                    Schema.DepositRecounting.CashAmount,
                    Schema.DepositRecounting.BacCheckAmount,
                    Schema.DepositRecounting.OtherCheckAmount,
                    Schema.DepositRecounting.AccountNumber),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize },
            };
            query.Criteria.AddCondition(Schema.DepositRecounting.BagLookup, ConditionOperator.Equal, bagId);

            var declared = query.AddLink(
                Schema.DeclaredDeposit.LogicalName,
                Schema.DepositRecounting.RelatedDeclaredDeposit,
                Schema.DeclaredDeposit.Id,
                JoinOperator.LeftOuter);
            declared.EntityAlias = DeclaredAlias;
            declared.Columns = new ColumnSet(
                Schema.DeclaredDeposit.DepositReference,
                Schema.DeclaredDeposit.TransactionNumber);

            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    deposits.Add(new DepositForTransaction
                    {
                        DepositId = row.Id,
                        Currency = row.GetAttributeValue<OptionSetValue>(Schema.DepositRecounting.Currency)?.Value,
                        CashAmount = ReadDecimal(row, Schema.DepositRecounting.CashAmount),
                        BacCheckAmount = ReadDecimal(row, Schema.DepositRecounting.BacCheckAmount),
                        OtherCheckAmount = ReadDecimal(row, Schema.DepositRecounting.OtherCheckAmount),
                        AccountNumber = ReadNullableDecimal(row, Schema.DepositRecounting.AccountNumber),
                        DeclaredReference = ReadAliased(row, DeclaredAlias, Schema.DeclaredDeposit.DepositReference)
                            as string,
                        DeclaredTransactionNumber =
                            ReadAliased(row, DeclaredAlias, Schema.DeclaredDeposit.TransactionNumber) as int?,
                    });
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            return deposits;
        }

        // ---- Helpers ----

        private static object? ReadAliased(Entity row, string alias, string attribute)
        {
            return row.GetAttributeValue<AliasedValue>(alias + "." + attribute)?.Value;
        }

        private static decimal ReadDecimal(Entity row, string attribute)
        {
            return ReadNullableDecimal(row, attribute) ?? 0m;
        }

        /// <summary>
        /// Lee un numerico sin presuponer el tipo CLR: sanic_accountnumber es `int` en
        /// DepositsRecounting y `decimal` en Transactions, y los montos podrian llegar
        /// como Money en algun entorno. Un cast rigido rompe en produccion y no en DEV.
        /// </summary>
        private static decimal? ReadNullableDecimal(Entity row, string attribute)
        {
            if (!row.Contains(attribute) || row[attribute] == null)
            {
                return null;
            }

            var value = row[attribute];
            if (value is Money money)
            {
                return money.Value;
            }

            return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";

        /// <summary>
        /// Cajero dueño de la sesion de la tula. null si no se puede resolver — el
        /// asiento se crea igual: perder la atribucion es malo, no crear la
        /// contabilidad es peor.
        /// </summary>
        private static Guid? ReadBagCashier(
            IOrganizationService system, ITracingService tracing, Guid bagId)
        {
            try
            {
                var bag = system.Retrieve(Schema.Bag.LogicalName, bagId,
                    new ColumnSet(Schema.Bag.RecountingSessionLookup));
                var session = bag.GetAttributeValue<EntityReference>(Schema.Bag.RecountingSessionLookup);
                if (session == null)
                {
                    return null;
                }

                var ses = system.Retrieve(Schema.RecountingSession.LogicalName, session.Id,
                    new ColumnSet(Schema.RecountingSession.Cashier));
                return ses.GetAttributeValue<EntityReference>(Schema.RecountingSession.Cashier)?.Id;
            }
            catch (Exception ex)
            {
                tracing.Trace("BagRecountClosure: no se pudo resolver el cajero de la tula {0}: {1}",
                    bagId, ex.Message);
                return null;
            }
        }
    }
}
