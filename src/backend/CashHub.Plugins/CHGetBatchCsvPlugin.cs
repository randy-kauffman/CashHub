using System;
using System.IO;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
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
        /// <summary>
        /// Techo defensivo del archivo que se devuelve como texto. Con el tope del corte
        /// (Digitization.MaxTransactionsPerCut = 500) un CSV real ronda los 50 KB, asi que
        /// 5 MB es holgado por dos ordenes de magnitud. Existe para que un archivo absurdo
        /// falle con un mensaje claro y no reventando la app del otro lado.
        /// </summary>
        private const int MaxCsvBytes = 5 * 1024 * 1024;

        /// <summary>Bloque de descarga. Dataverse admite hasta 4 MB por DownloadBlock.</summary>
        private const int BlockSize = 4 * 1024 * 1024;

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
                    new ColumnSet(Schema.TransactionBatch.Name, Schema.TransactionBatch.CsvCore + "_name"));
                var batchName = batch.GetAttributeValue<string>(Schema.TransactionBatch.Name) ?? batchId.ToString();

                tracing.Trace("GetBatchCsv: lote {0} ({1}).", batchName, batchId);

                InitializeFileBlocksDownloadResponse init;
                try
                {
                    init = (InitializeFileBlocksDownloadResponse)service.Execute(
                        new InitializeFileBlocksDownloadRequest
                        {
                            Target = new EntityReference(Schema.TransactionBatch.LogicalName, batchId),
                            FileAttributeName = Schema.TransactionBatch.CsvCore,
                        });
                }
                catch (Exception ex)
                {
                    // El caso normal aca es "el lote todavia no tiene CSV". Se dice asi, en
                    // el idioma del usuario, en vez de escupir la excepcion de plataforma.
                    tracing.Trace("GetBatchCsv: no se pudo abrir el archivo del lote {0}: {1}", batchName, ex);
                    throw new InvalidPluginExecutionException(
                        $"El lote {batchName} todavia no tiene un CSV generado. " +
                        "Genera el archivo antes de descargarlo.", ex);
                }

                if (init.FileSizeInBytes <= 0)
                {
                    throw new InvalidPluginExecutionException(
                        $"El CSV del lote {batchName} esta vacio (0 bytes). " +
                        "Hay que volver a generarlo antes de descargarlo.");
                }

                if (init.FileSizeInBytes > MaxCsvBytes)
                {
                    throw new InvalidPluginExecutionException(
                        $"El CSV del lote {batchName} pesa {init.FileSizeInBytes / 1024} KB y supera el maximo " +
                        $"de {MaxCsvBytes / 1024} KB que se puede devolver a la aplicacion.");
                }

                var bytes = new byte[init.FileSizeInBytes];
                var offset = 0L;
                using (var buffer = new MemoryStream(bytes))
                {
                    while (offset < init.FileSizeInBytes)
                    {
                        var block = (DownloadBlockResponse)service.Execute(new DownloadBlockRequest
                        {
                            FileContinuationToken = init.FileContinuationToken,
                            Offset = offset,
                            BlockLength = Math.Min(BlockSize, init.FileSizeInBytes - offset),
                        });

                        if (block.Data == null || block.Data.Length == 0)
                        {
                            // Sin esta guarda un bloque vacio deja el while girando para
                            // siempre y el plugin muere por timeout, no por el error real.
                            throw new InvalidPluginExecutionException(
                                $"La descarga del CSV del lote {batchName} se corto en el byte {offset} " +
                                $"de {init.FileSizeInBytes}.");
                        }

                        buffer.Write(block.Data, 0, block.Data.Length);
                        offset += block.Data.Length;
                    }
                }

                // FIDELIDAD BYTE A BYTE: se devuelve el archivo TAL CUAL quedo archivado.
                // Nada de normalizar saltos de linea ni de sacar el BOM. Este CSV es el
                // artefacto que va al core y que queda para auditoria: lo que se baja tiene
                // que ser identico a lo que se archivo, o el archivo deja de probar nada.
                // El BOM, si viniera, viaja igual — dentro de un string de Power Fx es
                // invisible y la app nunca lo muestra.
                //
                // Se devuelve ademas SizeBytes para que el otro lado pueda VERIFICAR que
                // llego completo, en vez de confiar.
                var csv = new UTF8Encoding(false).GetString(bytes);

                var fileName = batch.GetAttributeValue<string>(Schema.TransactionBatch.CsvCore + "_name");
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = init.FileName;
                }
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = batchName + ".csv";
                }

                context.OutputParameters[Schema.BatchCsvApi.OutCsv] = csv;
                context.OutputParameters[Schema.BatchCsvApi.OutFileName] = fileName;
                context.OutputParameters[Schema.BatchCsvApi.OutSizeBytes] = (int)init.FileSizeInBytes;

                tracing.Trace("GetBatchCsv: lote {0} -> '{1}', {2} bytes, {3} caracteres.",
                    batchName, fileName, init.FileSizeInBytes, csv.Length);
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
