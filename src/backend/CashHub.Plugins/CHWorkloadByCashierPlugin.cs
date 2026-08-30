using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHWorkloadByCashier: agregado por cajero que
    /// alimenta el rail de scrCorte / scrManual / scrLotes. Devuelve un JSON string
    /// (Workload) con una fila por cajero que tiene trabajo pendiente:
    /// { cajero, cajeroNombre, mesa, pendientesCorte, pendientesManual, lotesPorAplicar,
    ///   totalNio, totalUsd, totalEur }.
    ///
    /// Existe porque el conteo por cajero es un GroupBy que NO delega en canvas: la app
    /// tendria que traer todas las transacciones y agrupar client-side (cap de filas,
    /// miente en silencio). Aca es una query aggregate server-side. Cada pantalla filtra
    /// el rail por su campo (pendientesCorte / pendientesManual / lotesPorAplicar &gt; 0)
    /// y muestra ese conteo; los totales son los del CORTE (los KPIs de scrCorte).
    ///
    /// AGRUPA POR sanic_cajero, NO POR createdby. Parecia lo mismo y no lo es: el
    /// asiento lo escribe un plugin del servidor, asi que createdby era SYSTEM o el
    /// service principal. El tablero mostraba identidades de escritura y el trabajo de
    /// todos los cajeros se apilaba bajo SYSTEM — un cajero real no aparecia nunca.
    ///
    /// Nota de escala: el aggregate topa en el AggregateQueryRecordLimit (50000 filas
    /// escaneadas). El working set son las transacciones PENDIENTES (sin lote, sin
    /// status) — se cortan a diario, no se acumulan como el historico Aplicado —, asi
    /// que en la practica queda muy por debajo del limite.
    /// </summary>
    public sealed class CHWorkloadByCashierPlugin : IPlugin
    {
        private sealed class Row
        {
            public int PendientesCorte;
            public int PendientesManual;
            public int LotesPorAplicar;
            public decimal TotalNio;
            public decimal TotalUsd;
            public decimal TotalEur;
            public string Nombre = string.Empty;
            public string Mesa = string.Empty;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.WorkloadApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("Workload: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            var service = serviceFactory.CreateOrganizationService(null);

            try
            {
                var rows = new Dictionary<Guid, Row>();
                Row Get(Guid id)
                {
                    if (!rows.TryGetValue(id, out var r)) { r = new Row(); rows[id] = r; }
                    return r;
                }

                // 1. Transacciones pendientes: count + sum(cashamount) agrupado por
                //    cajero (createdby), tipo de proceso y moneda.
                var fetchTx = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Transaction.LogicalName}'>
    <attribute name='{Schema.Transaction.Id}' alias='cnt' aggregate='count' />
    <attribute name='{Schema.Transaction.CashAmount}' alias='total' aggregate='sum' />
    <attribute name='{Schema.Transaction.Cajero}' alias='cajero' groupby='true' />
    <attribute name='{Schema.Transaction.ProcessingType}' alias='ptype' groupby='true' />
    <attribute name='{Schema.Transaction.Currency}' alias='currency' groupby='true' />
    <filter type='and'>
      <condition attribute='{Schema.Transaction.TransactionBatch}' operator='null' />
      <condition attribute='{Schema.Transaction.TransactionStatus}' operator='null' />
    </filter>
  </entity>
</fetch>";

                foreach (var g in service.RetrieveMultiple(new FetchExpression(fetchTx)).Entities)
                {
                    var cajero = AsGuid(g, "cajero");
                    if (cajero == Guid.Empty) { continue; }

                    var cnt = g.GetAttributeValue<AliasedValue>("cnt")?.Value as int? ?? 0;
                    var total = g.GetAttributeValue<AliasedValue>("total")?.Value as decimal? ?? 0m;
                    var ptype = (g.GetAttributeValue<AliasedValue>("ptype")?.Value as OptionSetValue)?.Value;
                    var currency = (g.GetAttributeValue<AliasedValue>("currency")?.Value as OptionSetValue)?.Value;

                    var r = Get(cajero);
                    if (ptype == TransactionRouting.ProcessingBatch)
                    {
                        r.PendientesCorte += cnt;
                        switch (currency)
                        {
                            case Schema.Transaction.CurrencyNio: r.TotalNio += total; break;
                            case Schema.Transaction.CurrencyUsd: r.TotalUsd += total; break;
                            case Schema.Transaction.CurrencyEur: r.TotalEur += total; break;
                        }
                    }
                    else if (ptype == TransactionRouting.ProcessingManual)
                    {
                        r.PendientesManual += cnt;
                    }
                }

                // 2. Lotes por aplicar (Generado o Ingresado) agrupados por cajero.
                var fetchBatch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.TransactionBatch.LogicalName}'>
    <attribute name='{Schema.TransactionBatch.Id}' alias='cnt' aggregate='count' />
    <attribute name='{Schema.TransactionBatch.Cajero}' alias='cajero' groupby='true' />
    <filter type='and'>
      <condition attribute='{Schema.TransactionBatch.BatchStatus}' operator='in'>
        <value>{TransactionGuard.BatchGenerado}</value>
        <value>{TransactionGuard.BatchIngresado}</value>
      </condition>
    </filter>
  </entity>
</fetch>";

                foreach (var g in service.RetrieveMultiple(new FetchExpression(fetchBatch)).Entities)
                {
                    var cajero = AsGuid(g, "cajero");
                    if (cajero == Guid.Empty) { continue; }
                    Get(cajero).LotesPorAplicar += g.GetAttributeValue<AliasedValue>("cnt")?.Value as int? ?? 0;
                }

                if (rows.Count == 0)
                {
                    context.OutputParameters[Schema.WorkloadApi.OutWorkload] = "[]";
                    tracing.Trace("Workload: sin cajeros con trabajo pendiente.");
                    return;
                }

                var ids = new List<Guid>(rows.Keys);

                // 3. Nombres de los cajeros (una query).
                var users = new QueryExpression(Schema.SystemUser.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.SystemUser.Id, "fullname")
                };
                // ConditionOperator.In con params object[]: un Guid[] NO se expande solo
                // (C# lo pasa como un unico valor Guid[]). Cast a object[] para que cada
                // Guid entre como argumento individual.
                users.Criteria.AddCondition(Schema.SystemUser.Id, ConditionOperator.In, ids.Cast<object>().ToArray());
                foreach (var u in service.RetrieveMultiple(users).Entities)
                {
                    if (rows.TryGetValue(u.Id, out var r))
                    {
                        r.Nombre = u.GetAttributeValue<string>("fullname") ?? string.Empty;
                    }
                }

                // 4. Mesa de cada cajero (una query; si un cajero tiene varias, la ultima gana).
                var desks = new QueryExpression(Schema.CountingDesk.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.CountingDesk.Name, Schema.CountingDesk.Cajero)
                };
                desks.Criteria.AddCondition(Schema.CountingDesk.Cajero, ConditionOperator.In, ids.Cast<object>().ToArray());
                foreach (var d in service.RetrieveMultiple(desks).Entities)
                {
                    var cajero = d.GetAttributeValue<EntityReference>(Schema.CountingDesk.Cajero);
                    if (cajero != null && rows.TryGetValue(cajero.Id, out var r))
                    {
                        r.Mesa = d.GetAttributeValue<string>(Schema.CountingDesk.Name) ?? string.Empty;
                    }
                }

                context.OutputParameters[Schema.WorkloadApi.OutWorkload] = BuildJson(rows);
                tracing.Trace("Workload: {0} cajeros con trabajo pendiente.", rows.Count);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("Workload: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo calcular la carga por cajero: {ex.Message}", ex);
            }
        }

        /// <summary>Lee un groupby de lookup, que puede venir como EntityReference o Guid.</summary>
        private static Guid AsGuid(Entity row, string alias)
        {
            var v = row.GetAttributeValue<AliasedValue>(alias)?.Value;
            if (v is EntityReference er) { return er.Id; }
            if (v is Guid g) { return g; }
            return Guid.Empty;
        }

        private static string BuildJson(Dictionary<Guid, Row> rows)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("[");
            var first = true;
            foreach (var kv in rows)
            {
                if (!first) { sb.Append(','); }
                first = false;
                var r = kv.Value;
                sb.Append('{')
                  .Append("\"cajero\":").Append(JsonStr(kv.Key.ToString())).Append(',')
                  .Append("\"cajeroNombre\":").Append(JsonStr(r.Nombre)).Append(',')
                  .Append("\"mesa\":").Append(JsonStr(r.Mesa)).Append(',')
                  .Append("\"pendientesCorte\":").Append(r.PendientesCorte.ToString(inv)).Append(',')
                  .Append("\"pendientesManual\":").Append(r.PendientesManual.ToString(inv)).Append(',')
                  .Append("\"lotesPorAplicar\":").Append(r.LotesPorAplicar.ToString(inv)).Append(',')
                  .Append("\"totalNio\":").Append(r.TotalNio.ToString(inv)).Append(',')
                  .Append("\"totalUsd\":").Append(r.TotalUsd.ToString(inv)).Append(',')
                  .Append("\"totalEur\":").Append(r.TotalEur.ToString(inv))
                  .Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string JsonStr(string s)
        {
            if (s == null) { return "\"\""; }
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
