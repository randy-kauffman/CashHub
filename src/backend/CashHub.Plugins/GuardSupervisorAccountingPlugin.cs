using System;
using System.Linq;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Guard de la CONTABILIDAD del circuito de aprobacion de recuentos (B-5 del plan,
    /// hallazgo H-02 de la auditoria).
    ///
    /// Los asientos Sobrante / Faltante / 'Sin declaracion' / Deposito los calcula hoy
    /// Power Fx dentro de las apps: el servidor acepta el monto que le manden. Con el
    /// rol de cajero y la Web API (o Excel) se fabrica un Sobrante de cualquier importe
    /// contra cualquier deposito, o se duplica un asiento ya generado, y nada lo nota.
    /// Este plugin junta los insumos leyendo como SISTEMA (el deposito recontado, su
    /// declarado y los asientos ya existentes) y delega la decision en el dominio puro
    /// (AccountingGuard), que RECALCULA la diferencia por componente y bloquea todo lo
    /// que no coincida. Las lecturas como sistema son chequeos de seguridad de solo
    /// lectura, independientes de los privilegios del actor.
    ///
    /// Va en un plugin type SEPARADO de GuardTransactionPlugin (que impone la maquina
    /// de estados lote/transaccion) porque son dos dominios distintos con insumos
    /// distintos: aquel no consulta depositos, este no consulta lotes. Ambos corren
    /// sincronos en Pre-validation (10) sobre el Create de sanic_chtbltransactions
    /// (rank 1 la maquina de estados, rank 2 la contabilidad).
    ///
    /// Fuera de alcance: una transaccion sin tipo contable y sin recuento asociado es
    /// del circuito de digitacion y pasa sin tocar Dataverse (salida temprana, cero
    /// queries).
    /// </summary>
    public sealed class GuardSupervisorAccountingPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("GuardSupervisorAccounting: {0} de {1}, actor={2}, depth={3}.",
                context.MessageName, context.PrimaryEntityName, context.InitiatingUserId, context.Depth);

            if (context.MessageName != "Create"
                || context.PrimaryEntityName != Schema.Transaction.LogicalName)
            {
                tracing.Trace("GuardSupervisorAccounting: mensaje/entidad fuera de alcance, se permite.");
                return;
            }

            TransactionGuardResult result;
            try
            {
                result = Evaluate(context, serviceFactory, tracing);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("GuardSupervisorAccounting: error inesperado evaluando el asiento: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo verificar la contabilidad del asiento contra el recuento aprobado: "
                    + ex.Message, ex);
            }

            if (!result.IsAllowed)
            {
                tracing.Trace("GuardSupervisorAccounting: BLOQUEADO — {0}", result.Reason);
                throw new InvalidPluginExecutionException(result.Reason);
            }

            tracing.Trace("GuardSupervisorAccounting: asiento consistente con el recuento, se permite.");
        }

        private static TransactionGuardResult Evaluate(
            IPluginExecutionContext context,
            IOrganizationServiceFactory serviceFactory,
            ITracingService tracing)
        {
            var target = GetTargetEntity(context);

            var type = ReadOptionSet(target, Schema.Transaction.TransactionType);
            var currency = ReadOptionSet(target, Schema.Transaction.Currency);
            var recountingRef = target.GetAttributeValue<EntityReference>(Schema.Transaction.RelatedDepositRecounting);
            var amounts = ReadAmounts(
                target,
                Schema.Transaction.CashAmount,
                Schema.Transaction.BacCheckAmount,
                Schema.Transaction.OtherCheckAmount);

            tracing.Trace("GuardSupervisorAccounting: tipo={0} moneda={1} recuento={2} montos-target [{3}].",
                Fmt(type), Fmt(currency),
                recountingRef != null ? recountingRef.Id.ToString() : "null", amounts);

            // Salida temprana SIN tocar Dataverse: circuito de digitacion.
            if (!AccountingGuard.IsAccountingType(type) && recountingRef == null)
            {
                tracing.Trace("GuardSupervisorAccounting: transaccion sin tipo contable ni recuento, fuera de alcance.");
                return AccountingGuard.EvaluateAccountingCreate(
                    type, currency, amounts, false, null, null, false, null);
            }

            var system = serviceFactory.CreateOrganizationService(null);

            if (recountingRef == null)
            {
                // Camino SIN ancla (guia 03 §11 9.b): declarado que nadie reconto.
                var candidate = FindUnrecountedDeclared(system, tracing, target, currency, amounts);
                return AccountingGuard.EvaluateAccountingCreate(
                    type, currency, amounts, false, null, null, false, candidate);
            }

            // UNA sola lectura del recontado: trae montos, moneda y el lookup al declarado.
            var recountedRow = RetrieveRecountedDeposit(system, tracing, recountingRef.Id);
            var recounted = ToRecountedFacts(recountedRow);
            var declared = ResolveDeclaredDeposit(system, tracing, recountedRow);
            var duplicateExists = AccountingEntryExists(system, recountingRef.Id, type);

            tracing.Trace("GuardSupervisorAccounting: recontado [{0}] moneda={1}; declarado {2}; duplicado={3}.",
                recounted.Amounts,
                Fmt(recounted.Currency),
                declared.HasValue ? "[" + declared.Value.Amounts + "]" : "ninguno",
                duplicateExists);

            return AccountingGuard.EvaluateAccountingCreate(
                type, currency, amounts, true, recounted, declared, duplicateExists, null);
        }

        // ---- Lecturas como sistema ----

        /// <summary>Deposito recontado del lookup. Si no existe, el Retrieve levanta y lo toma el catch.</summary>
        private static Entity RetrieveRecountedDeposit(
            IOrganizationService system, ITracingService tracing, Guid recountingId)
        {
            var deposit = system.Retrieve(
                Schema.DepositRecounting.LogicalName, recountingId,
                new ColumnSet(
                    Schema.DepositRecounting.CashAmount,
                    Schema.DepositRecounting.BacCheckAmount,
                    Schema.DepositRecounting.OtherCheckAmount,
                    Schema.DepositRecounting.Currency,
                    Schema.DepositRecounting.RelatedDeclaredDeposit));

            tracing.Trace("GuardSupervisorAccounting: deposito recontado {0} leido como sistema.", recountingId);
            return deposit;
        }

        private static DepositFacts ToRecountedFacts(Entity recounted)
        {
            return new DepositFacts(
                ReadAmounts(
                    recounted,
                    Schema.DepositRecounting.CashAmount,
                    Schema.DepositRecounting.BacCheckAmount,
                    Schema.DepositRecounting.OtherCheckAmount),
                ReadOptionSet(recounted, Schema.DepositRecounting.Currency));
        }

        /// <summary>
        /// Declarado enlazado al recontado. La relacion esta espejada y las apps usan
        /// LOS DOS lados: primero el lookup del recontado, y si viene vacio se busca el
        /// declarado que apunta a este recuento. Sin esta segunda consulta un declarado
        /// enlazado solo del lado declarado se leeria como "sin declaracion" y el guard
        /// exigiria el monto entero como Sobrante.
        /// </summary>
        private static DepositFacts? ResolveDeclaredDeposit(
            IOrganizationService system, ITracingService tracing, Entity recountedRow)
        {
            var declaredRef = recountedRow.GetAttributeValue<EntityReference>(
                Schema.DepositRecounting.RelatedDeclaredDeposit);

            if (declaredRef != null)
            {
                var declared = system.Retrieve(
                    Schema.DeclaredDeposit.LogicalName, declaredRef.Id, DeclaredColumns());
                tracing.Trace("GuardSupervisorAccounting: declarado {0} resuelto por el lado recontado.", declaredRef.Id);
                return ToDeclaredFacts(declared);
            }

            var query = new QueryExpression(Schema.DeclaredDeposit.LogicalName)
            {
                ColumnSet = DeclaredColumns(),
                TopCount = 1
            };
            query.Criteria.AddCondition(
                Schema.DeclaredDeposit.RelatedRecountedDeposit, ConditionOperator.Equal, recountedRow.Id);

            var byMirror = system.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (byMirror == null)
            {
                tracing.Trace("GuardSupervisorAccounting: el recuento no tiene deposito declarado por ningun lado.");
                return null;
            }

            tracing.Trace("GuardSupervisorAccounting: declarado {0} resuelto por el lado declarado (espejo).", byMirror.Id);
            return ToDeclaredFacts(byMirror);
        }

        /// <summary>
        /// IDEMPOTENCIA: ya existe un asiento con el mismo (recuento, tipo). Es la misma
        /// unicidad que garantiza la alternate key sanic_KeyAsientoPorRecuento; la key
        /// cubre la carrera entre dos supervisores simultaneos, esta consulta da el
        /// mensaje de negocio en vez del error crudo del indice.
        /// </summary>
        private static bool AccountingEntryExists(IOrganizationService system, Guid recountingId, int? type)
        {
            if (type == null)
            {
                return false;
            }

            var query = new QueryExpression(Schema.Transaction.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(
                Schema.Transaction.RelatedDepositRecounting, ConditionOperator.Equal, recountingId);
            query.Criteria.AddCondition(
                Schema.Transaction.TransactionType, ConditionOperator.Equal, type.Value);

            return system.RetrieveMultiple(query).Entities.Count > 0;
        }

        /// <summary>
        /// Camino SIN ancla: el asiento tiene que corresponder a un deposito DECLARADO
        /// que nadie reconto (sanic_relatedrecounteddeposit vacio) con el mismo numero
        /// de transaccion. Se elige el candidato que ya cuadra en moneda y montos; si
        /// ninguno cuadra se devuelve el primero de la misma moneda (o el primero a
        /// secas) para que el dominio arme el mensaje por componente.
        /// </summary>
        private static DepositFacts? FindUnrecountedDeclared(
            IOrganizationService system,
            ITracingService tracing,
            Entity target,
            int? currency,
            AccountingAmounts amounts)
        {
            var transactionNumber = target.GetAttributeValue<int?>(Schema.Transaction.TransactionNumber);
            if (transactionNumber == null)
            {
                tracing.Trace("GuardSupervisorAccounting: asiento sin recuento y sin numero de transaccion: sin respaldo posible.");
                return null;
            }

            var query = new QueryExpression(Schema.DeclaredDeposit.LogicalName)
            {
                ColumnSet = DeclaredColumns(),
                TopCount = 25
            };
            query.Criteria.AddCondition(
                Schema.DeclaredDeposit.TransactionNumber, ConditionOperator.Equal, transactionNumber.Value);
            query.Criteria.AddCondition(
                Schema.DeclaredDeposit.RelatedRecountedDeposit, ConditionOperator.Null);

            var candidates = system.RetrieveMultiple(query).Entities
                .Select(ToDeclaredFacts)
                .ToList();

            tracing.Trace("GuardSupervisorAccounting: {0} declarado(s) sin recontar con numero {1}.",
                candidates.Count, transactionNumber.Value);

            if (candidates.Count == 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Currency == currency && candidate.Amounts.Equals(amounts))
                {
                    return candidate;
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Currency == currency)
                {
                    return candidate;
                }
            }

            return candidates[0];
        }

        private static ColumnSet DeclaredColumns()
        {
            return new ColumnSet(
                Schema.DeclaredDeposit.CashAmount,
                Schema.DeclaredDeposit.BacCheckAmount,
                Schema.DeclaredDeposit.OtherCheckAmount,
                Schema.DeclaredDeposit.Currency);
        }

        private static DepositFacts ToDeclaredFacts(Entity declared)
        {
            return new DepositFacts(
                ReadAmounts(
                    declared,
                    Schema.DeclaredDeposit.CashAmount,
                    Schema.DeclaredDeposit.BacCheckAmount,
                    Schema.DeclaredDeposit.OtherCheckAmount),
                ReadOptionSet(declared, Schema.DeclaredDeposit.Currency));
        }

        // ---- Helpers ----

        private static Entity GetTargetEntity(IPluginExecutionContext context)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                throw new InvalidPluginExecutionException(
                    "GuardSupervisorAccounting: el Create de la transaccion no trae Target de tipo Entity.");
            }

            return target;
        }

        /// <summary>Montos por componente; un atributo ausente o nulo vale cero.</summary>
        private static AccountingAmounts ReadAmounts(Entity entity, string cash, string bac, string other)
        {
            return new AccountingAmounts(
                ReadDecimal(entity, cash),
                ReadDecimal(entity, bac),
                ReadDecimal(entity, other));
        }

        private static decimal ReadDecimal(Entity entity, string attribute)
        {
            if (!entity.Contains(attribute) || entity[attribute] == null)
            {
                return 0m;
            }

            // Las tres columnas son Decimal en las tres tablas (verificado en Entity.xml);
            // Money se contempla por si alguna se migra de tipo.
            var value = entity[attribute];
            if (value is Money money)
            {
                return money.Value;
            }

            return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int? ReadOptionSet(Entity entity, string attribute)
        {
            return entity.GetAttributeValue<OptionSetValue>(attribute)?.Value;
        }

        private static string Fmt(int? value) => value.HasValue ? value.Value.ToString() : "null";
    }
}
