using System;
using System.Globalization;
using System.Linq;
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

                    var tipo = Opt(g, "tipo");

                    sb.Append("{\"moneda\":").Append(Moneda(Opt(g, "moneda")))
                      .Append(",\"tipo\":").Append(Tipo(tipo))
                      .Append(",\"signo\":").Append(Signo(tipo))
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
                context.OutputParameters[Schema.CashierDayApi.OutDays] = DiasConMovimiento(service, tracing, cajeroId);

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

        /// <summary>
        /// Los dias en que ESTE cajero tuvo movimientos, del mas reciente al mas viejo.
        ///
        /// POR QUE HACE FALTA. La pantalla filtra por fecha de CREACION y arranca en hoy.
        /// Un cajero cuyas transacciones son del 17/08 se veia completamente vacio, sin una
        /// sola pista de donde estaba su trabajo: la pantalla decia la verdad y aun asi
        /// dejaba al operador en un callejon. Con esta lista puede saltar al dia que le
        /// importa en vez de ir probando fechas a mano.
        ///
        /// Se acota a los ultimos 180 dias y a 60 filas: es una ayuda de navegacion, no un
        /// historico.
        /// </summary>
        private static string DiasConMovimiento(
            IOrganizationService service, ITracingService tracing, Guid cajeroId)
        {
            var desde = DateTime.UtcNow.AddDays(-180).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            var fetch = $@"
<fetch aggregate='true'>
  <entity name='{Schema.Transaction.LogicalName}'>
    <attribute name='{Schema.Transaction.Id}' alias='cant' aggregate='count' />
    <attribute name='{Schema.Transaction.CreatedOn}' alias='dia' groupby='true' dategrouping='day' />
    <attribute name='{Schema.Transaction.CreatedOn}' alias='mes' groupby='true' dategrouping='month' />
    <attribute name='{Schema.Transaction.CreatedOn}' alias='anio' groupby='true' dategrouping='year' />
    <filter type='and'>
      <condition attribute='{Schema.Transaction.Cajero}' operator='eq' value='{cajeroId}' />
      <condition attribute='{Schema.Transaction.CreatedOn}' operator='on-or-after' value='{desde}' />
    </filter>
  </entity>
</fetch>";

            try
            {
                var filas = service.RetrieveMultiple(new FetchExpression(fetch)).Entities
                    .Select(e => new
                    {
                        Anio = Int(e, "anio"),
                        Mes = Int(e, "mes"),
                        Dia = Int(e, "dia"),
                        Cant = Int(e, "cant"),
                    })
                    .Where(x => x.Anio > 0)
                    .OrderByDescending(x => x.Anio).ThenByDescending(x => x.Mes).ThenByDescending(x => x.Dia)
                    .Take(60)
                    .ToList();

                var sb = new StringBuilder("[");
                var primera = true;
                foreach (var f in filas)
                {
                    if (!primera) { sb.Append(','); }
                    primera = false;
                    sb.Append("{\"fecha\":\"")
                      .Append(f.Anio.ToString("0000", CultureInfo.InvariantCulture)).Append('-')
                      .Append(f.Mes.ToString("00", CultureInfo.InvariantCulture)).Append('-')
                      .Append(f.Dia.ToString("00", CultureInfo.InvariantCulture))
                      .Append("\",\"cantidad\":").Append(f.Cant).Append('}');
                }

                tracing.Trace("CashierDay: {0} dia(s) con movimiento para el cajero {1}.", filas.Count, cajeroId);
                return sb.Append(']').ToString();
            }
            catch (Exception ex)
            {
                // Es una AYUDA de navegacion: si falla, la pantalla sigue sirviendo. No se
                // voltea la consulta principal por no poder ofrecer un atajo.
                tracing.Trace("CashierDay: no se pudieron listar los dias con movimiento ({0}).", ex.Message);
                return "[]";
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
                // Los asientos sembrados viejos no tienen tipo. Se etiquetan en vez de
                // dejarlos con la cadena vacia: una fila sin nombre en el resumen parece un
                // error de la pantalla, no un dato incompleto.
                default: return "\"Sin tipo\"";
            }
        }

        /// <summary>
        /// EL FALTANTE RESTA. Un faltante es plata que NO esta: se guarda en positivo —los
        /// 116 de la base lo estan— pero en cualquier total del dia va con signo negativo.
        /// Sumarlo seria inflar el cierre del cajero justo con lo que le falta.
        ///
        /// El signo viaja EN EL CUBO, calculado en el servidor, para que la app no tenga que
        /// conocer la regla contable: solo multiplica.
        /// </summary>
        private static int Signo(int tipoCode) => tipoCode == 159460001 ? -1 : 1;

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
