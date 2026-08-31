using System;
using System.Globalization;
using System.Text;
using CashHub.Validation.Export;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHCashierDay: el cubo de un cajero en un dia.
    ///
    /// DEVUELVE UN CUBO, NO UN TOTAL. Una fila por combinacion de moneda x tipo x via, con
    /// efectivo, cheque BAC, cheque otros, total y conteo. Son a lo sumo 3 x 4 x 2 = 24
    /// filas, asi que la app hace los rollups —el resumen grande y los contadores de los
    /// filtros— sobre 24 filas y no vuelve a consultar cada vez que se toca un chip.
    ///
    /// POR QUE AGREGADO Y NO CONTANDO FILAS. Un dia de un cajero real son 5.442
    /// transacciones. Sumarlas del lado del cliente toparia en 500 y daria un resumen
    /// incompleto SIN avisar — el mismo modo de falla que ya nos mordio en el conteo del
    /// corte y en el resumen por cuenta. El agregado lo hace el servidor y es exacto sin
    /// importar el volumen.
    ///
    /// El DETALLE (la tabla) NO pasa por aca: la galeria se ata directo al datasource con
    /// un Filter delegable, asi que se ven TODAS las transacciones del dia y no una pagina.
    /// </summary>
    public sealed class CHCashierDayPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.CashierDayApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("CashierDay: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                if (!(context.InputParameters.TryGetValue(Schema.CashierDayApi.InCajeroId, out var rawCajero)
                      && rawCajero is Guid cajeroId && cajeroId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "Se requiere 'CajeroId': es obligatorio, de tipo Guid y no vacio.");
                }

                if (!(context.InputParameters.TryGetValue(Schema.CashierDayApi.InDesde, out var rawDesde)
                      && rawDesde is DateTime desde))
                {
                    throw new InvalidPluginExecutionException("Se requiere 'Desde' (fecha y hora de inicio del dia).");
                }

                if (!(context.InputParameters.TryGetValue(Schema.CashierDayApi.InHasta, out var rawHasta)
                      && rawHasta is DateTime hasta))
                {
                    throw new InvalidPluginExecutionException("Se requiere 'Hasta' (fecha y hora de fin del dia).");
                }

                if (hasta <= desde)
                {
                    throw new InvalidPluginExecutionException(
                        "El rango de fechas esta invertido: 'Hasta' tiene que ser posterior a 'Desde'.");
                }

                var d = desde.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                var h = hasta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

                // Un solo aggregate agrupado por moneda x tipo x via. De aca salen tanto el
                // resumen grande (filtrado) como los contadores de los chips (del dia
                // entero): la app elige que sumar sin volver a preguntar.
                var fetch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Transaction.LogicalName}'>
    <attribute name='{Schema.Transaction.Id}' alias='cant' aggregate='count' />
    <attribute name='{Schema.Transaction.CashAmount}' alias='efectivo' aggregate='sum' />
    <attribute name='{Schema.Transaction.BacCheckAmount}' alias='chqbac' aggregate='sum' />
    <attribute name='{Schema.Transaction.OtherCheckAmount}' alias='chqotros' aggregate='sum' />
    <attribute name='{Schema.Transaction.Total}' alias='total' aggregate='sum' />
    <attribute name='{Schema.Transaction.Currency}' alias='moneda' groupby='true' />
    <attribute name='{Schema.Transaction.TransactionType}' alias='tipo' groupby='true' />
    <attribute name='{Schema.Transaction.ProcessingType}' alias='via' groupby='true' />
    <filter type='and'>
      <condition attribute='{Schema.Transaction.Cajero}' operator='eq' value='{cajeroId}' />
      <condition attribute='{Schema.Transaction.CreatedOn}' operator='on-or-after' value='{d}' />
      <condition attribute='{Schema.Transaction.CreatedOn}' operator='lt' value='{h}' />
    </filter>
  </entity>
</fetch>";

                var grupos = service.RetrieveMultiple(new FetchExpression(fetch));

                var sb = new StringBuilder("[");
                var primera = true;
                var filas = 0;
                var totalTrn = 0;

                foreach (var g in grupos.Entities)
                {
                    if (!primera) { sb.Append(','); }
                    primera = false;
                    filas++;

                    var cant = Int(g, "cant");
                    totalTrn += cant;

                    sb.Append("{\"moneda\":").Append(Moneda(Opt(g, "moneda")))
                      .Append(",\"tipo\":").Append(Tipo(Opt(g, "tipo")))
                      .Append(",\"via\":").Append(Via(Opt(g, "via")))
                      .Append(",\"efectivo\":").Append(Num(Dec(g, "efectivo")))
                      .Append(",\"chequeBac\":").Append(Num(Dec(g, "chqbac")))
                      .Append(",\"chequeOtros\":").Append(Num(Dec(g, "chqotros")))
                      .Append(",\"total\":").Append(Num(Dec(g, "total")))
                      .Append(",\"cantidad\":").Append(cant)
                      .Append('}');
                }

                sb.Append(']');

                context.OutputParameters[Schema.CashierDayApi.OutCube] = sb.ToString();
                context.OutputParameters[Schema.CashierDayApi.OutTotalCount] = totalTrn;

                tracing.Trace("CashierDay: cajero {0} de {1} a {2} -> {3} grupo(s), {4} transaccion(es).",
                    cajeroId, d, h, filas, totalTrn);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("CashierDay: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo armar el resumen del cajero: {ex.Message}", ex);
            }
        }

        private static int Opt(Entity e, string alias) =>
            (e.GetAttributeValue<AliasedValue>(alias)?.Value as OptionSetValue)?.Value ?? -1;

        private static int Int(Entity e, string alias) =>
            e.GetAttributeValue<AliasedValue>(alias)?.Value as int? ?? 0;

        private static decimal Dec(Entity e, string alias) =>
            e.GetAttributeValue<AliasedValue>(alias)?.Value as decimal? ?? 0m;

        private static string Num(decimal v) =>
            v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Moneda(int code)
        {
            switch (code)
            {
                case CoreExportTransaction.CurrencyNio: return "\"COR\"";
                case CoreExportTransaction.CurrencyUsd: return "\"USD\"";
                case CoreExportTransaction.CurrencyEur: return "\"EUR\"";
                default: return "\"\"";
            }
        }

        /// <summary>Etiquetas del option set sanic_transactiontype, en el idioma del operador.</summary>
        private static string Tipo(int code)
        {
            switch (code)
            {
                case 159460000: return "\"Deposito\"";
                case 159460001: return "\"Faltante\"";
                case 159460002: return "\"Sobrante\"";
                case 159460003: return "\"Sin declaracion\"";
                default: return "\"\"";
            }
        }

        /// <summary>Manual / Lote (sanic_transactionprocessingtype).</summary>
        private static string Via(int code)
        {
            switch (code)
            {
                case 159460000: return "\"Manual\"";
                case 159460001: return "\"Lote\"";
                default: return "\"\"";
            }
        }
    }
}
