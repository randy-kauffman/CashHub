using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CashHub.Validation.Export;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHBatchSummary: el resumen por cuenta del lote y, si se
    /// le pega el del IBS, la comparacion entre los dos.
    ///
    /// POR QUE EXISTE. El aprobador del IBS Branch no ve el detalle transaccion por
    /// transaccion: ve un "Resumen por Cuenta" (Cuenta | Moneda | Monto | Cant TRN) y tiene
    /// que decidir con eso. Comparar a mano contra el lote es lento y propenso a errores —
    /// el lote mas grande del entorno tiene 600 cuentas distintas—, y el error ahi se paga
    /// aprobando o devolviendo la carga equivocada.
    ///
    /// EL RESUMEN SALE DE LAS MISMAS FILAS QUE EL CSV (CoreCsvExport.ReadTransactions). No
    /// es un detalle de implementacion: es lo que hace confiable la comparacion. Si el
    /// resumen se calculara por otro camino, podria cuadrar contra el IBS mientras el
    /// archivo que se le mando dice otra cosa.
    ///
    /// SERVER-SIDE y no con un GroupBy en Power Fx: el cliente topa en 500 filas y un lote
    /// de 600 cuentas mostraria un resumen incompleto sin avisar.
    /// </summary>
    public sealed class CHBatchSummaryPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.BatchSummaryApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("BatchSummary: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                if (!(context.InputParameters.TryGetValue(Schema.BatchSummaryApi.InBatchId, out var rawBatch)
                      && rawBatch is Guid batchId
                      && batchId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "El resumen del lote requiere 'BatchId': es obligatorio, de tipo Guid y no vacio.");
                }

                // Contexto del llamador: si no puede leer el lote, Dataverse lo frena aca.
                var batch = service.Retrieve(
                    Schema.TransactionBatch.LogicalName, batchId,
                    new ColumnSet(Schema.TransactionBatch.Name));
                var batchName = batch.GetAttributeValue<string>(Schema.TransactionBatch.Name) ?? batchId.ToString();

                var transactions = CoreCsvExport.ReadTransactions(service, tracing, batchId);
                var resumen = CoreSummaryBuilder.FromTransactions(transactions);

                context.OutputParameters[Schema.BatchSummaryApi.OutSummary] = SerializarResumen(resumen);

                context.InputParameters.TryGetValue(Schema.BatchSummaryApi.InPastedSummary, out var rawPasted);
                var pegado = rawPasted as string;

                if (string.IsNullOrWhiteSpace(pegado))
                {
                    context.OutputParameters[Schema.BatchSummaryApi.OutComparison] = "[]";
                    context.OutputParameters[Schema.BatchSummaryApi.OutOutcome] = "sin-comparacion";
                    context.OutputParameters[Schema.BatchSummaryApi.OutMessage] =
                        $"{batchName}: {resumen.Count} cuenta(s) en el lote.";
                    tracing.Trace("BatchSummary: lote {0}, {1} cuentas, sin comparacion.", batchName, resumen.Count);
                    return;
                }

                var leido = CoreSummaryParser.Parse(pegado);
                var comparacion = CoreSummaryComparer.Compare(resumen, leido.Rows);

                context.OutputParameters[Schema.BatchSummaryApi.OutComparison] = SerializarComparacion(comparacion);
                context.OutputParameters[Schema.BatchSummaryApi.OutOutcome] = comparacion.IsMatch ? "coincide" : "difiere";
                context.OutputParameters[Schema.BatchSummaryApi.OutMessage] =
                    Mensaje(batchName, resumen, leido, comparacion);

                tracing.Trace("BatchSummary: lote {0} -> {1} cuentas, {2} leidas del IBS, {3} diferencias, {4} lineas ignoradas.",
                    batchName, resumen.Count, leido.Rows.Count, comparacion.DifferingAccounts, leido.IgnoredLines.Count);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("BatchSummary: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo armar el resumen del lote: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// El mensaje dice QUE paso y, si algo no se entendio, LO DICE. Una linea del
        /// pegado que se caiga sin aviso haria creer al aprobador que comparo todo cuando
        /// comparo una parte — y eso es peor que no comparar nada.
        /// </summary>
        private static string Mensaje(
            string batchName, List<CoreSummaryRow> resumen,
            CoreSummaryParseResult leido, CoreSummaryComparison comparacion)
        {
            var sb = new StringBuilder();

            if (leido.Rows.Count == 0)
            {
                sb.Append("No se reconocio ninguna fila en el texto pegado. ");
                sb.Append("Se espera el Resumen por Cuenta del IBS (Cuenta, Moneda, Monto, Cant TRN).");
                return sb.ToString();
            }

            sb.Append(comparacion.IsMatch
                ? $"{batchName} COINCIDE con el IBS: {comparacion.MatchingAccounts} cuenta(s), todo igual."
                : $"{batchName} NO coincide: {comparacion.DifferingAccounts} de {comparacion.TotalAccounts} cuenta(s) con diferencia.");

            if (leido.IgnoredLines.Count > 0)
            {
                sb.Append($" Ojo: {leido.IgnoredLines.Count} linea(s) del texto pegado no se entendieron y NO se compararon.");
            }

            return sb.ToString();
        }

        private static string SerializarResumen(IEnumerable<CoreSummaryRow> filas)
        {
            var sb = new StringBuilder("[");
            var primera = true;
            foreach (var f in filas)
            {
                if (!primera) { sb.Append(','); }
                primera = false;
                sb.Append("{\"cuenta\":").Append(Json(f.Account))
                  .Append(",\"moneda\":").Append(Json(f.Currency))
                  .Append(",\"monto\":").Append(Num(f.Amount))
                  .Append(",\"cantidad\":").Append(f.Count)
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }

        private static string SerializarComparacion(CoreSummaryComparison c)
        {
            var sb = new StringBuilder("[");
            var primera = true;
            foreach (var f in c.Rows)
            {
                if (!primera) { sb.Append(','); }
                primera = false;
                sb.Append("{\"cuenta\":").Append(Json(f.Account))
                  .Append(",\"moneda\":").Append(Json(f.Currency))
                  .Append(",\"estado\":").Append(Json(Estado(f.Status)))
                  .Append(",\"montoLote\":").Append(Num(f.BatchAmount))
                  .Append(",\"montoCore\":").Append(Num(f.CoreAmount))
                  .Append(",\"difMonto\":").Append(Num(f.AmountDifference))
                  .Append(",\"cantLote\":").Append(f.BatchCount)
                  .Append(",\"cantCore\":").Append(f.CoreCount)
                  .Append(",\"difCant\":").Append(f.CountDifference)
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// <summary>Etiquetas en el idioma del operador, no nombres de enum.</summary>
        private static string Estado(CoreSummaryStatus s)
        {
            switch (s)
            {
                case CoreSummaryStatus.Match: return "coincide";
                case CoreSummaryStatus.AmountDiffers: return "monto distinto";
                case CoreSummaryStatus.CountDiffers: return "cantidad distinta";
                case CoreSummaryStatus.AmountAndCountDiffer: return "monto y cantidad distintos";
                case CoreSummaryStatus.MissingInCore: return "no esta en el IBS";
                case CoreSummaryStatus.MissingInBatch: return "no esta en el lote";
                default: return s.ToString();
            }
        }

        private static string Num(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Json(string s)
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
                        if (c < ' ') { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
