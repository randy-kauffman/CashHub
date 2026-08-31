using System;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHGetBatchCsv: devuelve el CSV de un lote como texto.
    ///
    /// EL PROBLEMA QUE RESUELVE. La descarga vieja era Download() sobre
    /// .../sanic_csvcore/$value. Download() es una NAVEGACION del navegador: Power Fx no
    /// tiene forma de ponerle un header Authorization, asi que lo unico que puede
    /// autenticarla es la cookie del org. Los usuarios de negocio no la tienen —viven en
    /// apps.powerapps.com, que nunca setea esa cookie— y les daba HTTP 401 con una pestaña
    /// en blanco. Peor: al no ser una operacion de la app, el fallo quedaba FUERA del
    /// alcance de cualquier IfError. La app no podia enterarse, y el usuario se quedaba
    /// mirando una pestaña rota sin un solo aviso.
    ///
    /// Este API viaja por el conector de Dataverse, que va autenticado como corresponde.
    /// El archivo lo arma el navegador (componente de codigo, Blob). Sin cookie, sin
    /// navegacion, sin depender del estado del browser.
    ///
    /// NO GENERA EL CSV: lo lee de la columna File que ya subio CH-Flow-DownloadData.
    /// Regenerarlo aca duplicaria la logica y abriria la puerta a que el archivo que se
    /// archiva y el que se baja dejen de ser el mismo — justo lo que un proceso bancario
    /// no puede permitirse.
    ///
    /// SEGURIDAD: corre en el contexto de quien llama, sin elevar. Si el usuario no puede
    /// leer el lote, Dataverse lo frena en el Retrieve; no hay que reimplementar permisos.
    /// </summary>
    public sealed class CHGetBatchCsvPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.BatchCsvApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("GetBatchCsv: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            // CONTEXTO DEL LLAMADOR, SIN ELEVAR.
            //
            // Hubo un momento en que esto corria elevado a SYSTEM, para esquivar la Field
            // Level Security de sanic_accountnumber —el digitador no podia leerla y las
            // cuentas salian en 000000000—. Se revirtio: el permiso se resolvio donde
            // correspondia, con un perfil FLS de SOLO LECTURA para el digitador
            // (scripts/grant-digitizer-account-read.sh).
            //
            // Asi el archivo contiene exactamente lo que el operador esta autorizado a
            // ver, y no hay una escalada de privilegios escondida adentro de una
            // exportacion bancaria. Ademas el Retrieve del lote en este mismo contexto ES
            // el control de acceso: si el usuario no puede leer el lote, Dataverse lo
            // frena ahi y no hay que reimplementar permisos.
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                if (!(context.InputParameters.TryGetValue(Schema.BatchCsvApi.InBatchId, out var rawBatch)
                      && rawBatch is Guid batchId
                      && batchId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "La descarga del CSV requiere el lote: 'BatchId' es obligatorio, " +
                        "de tipo Guid y no vacio.");
                }

                // El nombre del lote se relee para que los mensajes de error digan TB-####
                // y no un GUID: el que lee el error es el digitador, no el desarrollador.
                var batch = service.Retrieve(
                    Schema.TransactionBatch.LogicalName, batchId,
                    new ColumnSet(Schema.TransactionBatch.Name));
                var batchName = batch.GetAttributeValue<string>(Schema.TransactionBatch.Name) ?? batchId.ToString();

                tracing.Trace("GetBatchCsv: lote {0} ({1}).", batchName, batchId);

                // EL CSV SE GENERA ACA, no se lee de lo que dejo el flujo.
                //
                // Antes este API leia la columna File que subia CH-Flow-DownloadData. Ya
                // no alcanza: el flujo armaba el archivo con "Create CSV table", que NO
                // escapa nada. Un depositante "PEREZ, JUAN" parte la fila y corre todas
                // las columnas siguientes — el core lee la moneda donde va el deposito y
                // postea mal SIN fallar. Y ademas el mapeo hacia float(formatNumber(x,
                // '0.00')), que anula los dos decimales que el core exige.
                //
                // Ninguna de las dos cosas se podia arreglar en el flujo: Power Automate
                // no tiene regex replace, y la lista de caracteres permitidos tiene que
                // ser LA MISMA que va a usar la validacion de entrada para rebotar en
                // origen. Una regla, dos defensas.
                //
                // Se genera Y se archiva en la misma pasada, para que el archivo que baja
                // el digitador y el que queda para auditoria sean el mismo.
                var fileName = (batchName ?? "lote") + ".csv";
                var csv = CoreCsvExport.BuildArchiveAndReturn(service, tracing, batchId, fileName);
                var sizeBytes = new UTF8Encoding(false).GetByteCount(csv);

                context.OutputParameters[Schema.BatchCsvApi.OutCsv] = csv;
                context.OutputParameters[Schema.BatchCsvApi.OutFileName] = fileName;
                context.OutputParameters[Schema.BatchCsvApi.OutSizeBytes] = sizeBytes;

                tracing.Trace("GetBatchCsv: lote {0} -> '{1}', {2} bytes, {3} caracteres.",
                    batchName, fileName, sizeBytes, csv.Length);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("GetBatchCsv: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException(
                    $"No se pudo obtener el CSV del lote: {ex.Message}", ex);
            }
        }
    }
}
