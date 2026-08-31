using System;
using System.Collections.Generic;
using System.Text;
using CashHub.Validation.Export;
using CashHub.Validation.Security;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Generacion y archivado del CSV del lote hacia el core.
    ///
    /// POR QUE ESTA ACA Y NO EN CH-FLOW-DOWNLOADDATA. El flujo armaba el CSV con la
    /// accion "Create CSV table", que no escapa nada. Dos cosas lo hicieron insostenible:
    ///
    /// 1. UNA LISTA BLANCA DE CARACTERES NO SE PUEDE ESCRIBIR EN UN FLUJO. Power Automate
    ///    no tiene regex replace: seria un replace() anidado por caracter, sin tests. Y la
    ///    MISMA lista la va a usar la validacion de entrada para rebotar en origen — la
    ///    misma logica en dos lenguajes diverge el primer dia.
    ///
    /// 2. EL FORMATO DE DOS DECIMALES SE ANULABA SOLO. El mapeo era
    ///    float(formatNumber(x,'0.00')), y ese float() deshace lo que formatNumber acaba
    ///    de hacer: salian 5100 y 3427.5 donde el core EXIGE 5100.00 y 3427.50.
    ///
    /// Y no se arma en el navegador: el contenido de un archivo que entra al core no
    /// puede depender de codigo que cualquiera edita en las devtools. El componente de la
    /// app solo lo guarda como archivo.
    /// </summary>
    internal static class CoreCsvExport
    {
        /// <summary>
        /// Bloque de subida. Dataverse admite hasta 4 MB por UploadBlock; con el tope del
        /// corte (500 transacciones) un CSV real ronda los 50 KB y entra en uno solo.
        /// </summary>
        private const int BlockSize = 4 * 1024 * 1024;

        private const int PageSize = 500;

        /// <summary>
        /// Arma el CSV del lote leyendo sus transacciones, lo ARCHIVA en la columna File y
        /// devuelve el texto.
        ///
        /// Archivar y devolver el MISMO texto no es redundancia: es la garantia de que el
        /// archivo que baja el digitador y el que queda para auditoria son el mismo. Si
        /// uno se generara y el otro se leyera por caminos distintos, divergen.
        /// </summary>
        public static string BuildArchiveAndReturn(
            IOrganizationService service, ITracingService tracing, Guid batchId, string fileName)
        {
            var identity = ReadIdentity(service, tracing);
            var allowed = ReadParameter(service, Schema.Parameter.AllowedCharactersKey);

            if (string.IsNullOrEmpty(allowed))
            {
                // DEFAULT-DENY con aviso, no silencioso: sin lista, el saneo dejaria todos
                // los campos de texto en blanco. Es preferible frenar y decirlo a entregar
                // un archivo con los depositantes vacios.
                throw new InvalidPluginExecutionException(
                    $"El parametro {Schema.Parameter.AllowedCharactersKey} no esta configurado o esta " +
                    "vacio. Sin la lista de caracteres permitidos no se puede generar el CSV del core.");
            }

            var transactions = ReadTransactions(service, tracing, batchId);

            // EL FRENO QUE EVITA EL PEOR ARCHIVO POSIBLE.
            //
            // sanic_accountnumber tiene Field Level Security. Si quien llama no la puede
            // leer, Dataverse NO falla: la enmascara y devuelve null. Sin esta guarda, el
            // CSV salia con CUENTA 000000000 en TODAS las filas y el core habria recibido
            // el lote entero contra la cuenta cero — un archivo perfectamente formado y
            // completamente equivocado.
            //
            // Una transaccion de lote siempre tiene cuenta: las que no la tienen se rutean
            // a Manual y nunca entran a un corte. Asi que una cuenta vacia aca es una de
            // dos cosas, y las dos ameritan frenar: falta el perfil FLS, o el dato esta
            // mal. Frenar y decirlo es infinitamente mejor que exportar ceros.
            var sinCuenta = transactions.FindAll(t => !t.AccountNumber.HasValue).Count;
            if (sinCuenta > 0)
            {
                throw new InvalidPluginExecutionException(
                    $"{sinCuenta} de {transactions.Count} transacciones del lote no tienen numero de " +
                    "cuenta legible, y el CSV saldria con la cuenta en ceros. Suele ser falta de permiso " +
                    "de lectura sobre el campo protegido (perfil 'CashHub | FLS | Digitador Lectura " +
                    "Cuenta'). No se genero ningun archivo.");
            }

            var csv = CoreCsvBuilder.Build(transactions, identity, allowed);
            var bytes = new UTF8Encoding(false).GetBytes(csv);

            tracing.Trace("CoreCsvExport: {0} transacciones -> {1} bytes.", transactions.Count, bytes.Length);

            Archive(service, tracing, batchId, fileName, bytes);
            return csv;
        }

        /// <summary>
        /// Las transacciones del lote, tal como las ve el CSV. Es internal a proposito:
        /// el resumen por cuenta se arma de ESTAS filas, no de una consulta paralela. Si
        /// cada uno consultara por su lado, el resumen podria cuadrar contra el IBS
        /// mientras el archivo enviado dice otra cosa.
        /// </summary>
        internal static List<CoreExportTransaction> ReadTransactions(
            IOrganizationService service, ITracingService tracing, Guid batchId)
        {
            var query = new QueryExpression(Schema.Transaction.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.Transaction.AccountNumber,
                    Schema.Transaction.Total,
                    Schema.Transaction.TransactionIdText,
                    Schema.Transaction.TransactionReference,
                    Schema.Transaction.Currency,
                    Schema.Transaction.CashAmount),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize },
            };
            query.Criteria.AddCondition(
                Schema.Transaction.TransactionBatch, ConditionOperator.Equal, batchId);
            // Orden estable: sin ORDER BY, dos generaciones del MISMO lote pueden salir con
            // las filas en distinto orden y dejar de ser comparables byte a byte.
            query.AddOrder(Schema.Transaction.TransactionIdText, OrderType.Ascending);

            var rows = new List<CoreExportTransaction>();
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                foreach (var e in page.Entities)
                {
                    rows.Add(new CoreExportTransaction
                    {
                        AccountNumber = e.GetAttributeValue<decimal?>(Schema.Transaction.AccountNumber),
                        Total = e.GetAttributeValue<decimal>(Schema.Transaction.Total),
                        TransactionId = e.GetAttributeValue<string>(Schema.Transaction.TransactionIdText),
                        Reference = e.GetAttributeValue<string>(Schema.Transaction.TransactionReference),
                        Currency = e.GetAttributeValue<OptionSetValue>(Schema.Transaction.Currency)?.Value ?? 0,
                        CashAmount = e.GetAttributeValue<decimal>(Schema.Transaction.CashAmount),
                    });
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace("CoreCsvExport: lote {0} -> {1} transacciones leidas.", batchId, rows.Count);
            return rows;
        }

        private static void Archive(
            IOrganizationService service, ITracingService tracing,
            Guid batchId, string fileName, byte[] bytes)
        {
            var init = (InitializeFileBlocksUploadResponse)service.Execute(
                new InitializeFileBlocksUploadRequest
                {
                    Target = new EntityReference(Schema.TransactionBatch.LogicalName, batchId),
                    FileAttributeName = Schema.TransactionBatch.CsvCore,
                    // El nombre SOLO se fija al subir: patchear <columna>_name despues
                    // devuelve 204 y Dataverse lo ignora (es de solo lectura).
                    FileName = fileName,
                });

            var blockIds = new List<string>();
            for (var offset = 0; offset < bytes.Length; offset += BlockSize)
            {
                var length = Math.Min(BlockSize, bytes.Length - offset);
                var block = new byte[length];
                Array.Copy(bytes, offset, block, 0, length);

                // El id del bloque tiene que ser base64 y del mismo largo en todos los
                // bloques, o el commit los ordena mal.
                var blockId = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes((offset / BlockSize).ToString("D6")));
                blockIds.Add(blockId);

                service.Execute(new UploadBlockRequest
                {
                    FileContinuationToken = init.FileContinuationToken,
                    BlockId = blockId,
                    BlockData = block,
                });
            }

            service.Execute(new CommitFileBlocksUploadRequest
            {
                FileContinuationToken = init.FileContinuationToken,
                BlockList = blockIds.ToArray(),
                FileName = fileName,
                MimeType = "text/csv",
            });

            tracing.Trace("CoreCsvExport: archivado '{0}' ({1} bytes, {2} bloque(s)) en {3}.",
                fileName, bytes.Length, blockIds.Count, Schema.TransactionBatch.CsvCore);
        }

        private static CoreExportIdentity ReadIdentity(IOrganizationService service, ITracingService tracing)
        {
            var json = ReadParameter(service, Schema.Parameter.CoreIdentityKey);
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidPluginExecutionException(
                    $"El parametro {Schema.Parameter.CoreIdentityKey} no esta configurado. " +
                    "Sin la identidad bancaria no se puede generar el CSV del core.");
            }

            tracing.Trace("CoreCsvExport: identidad leida de {0}.", Schema.Parameter.CoreIdentityKey);

            return new CoreExportIdentity
            {
                TrnCode = FlatJson.Value(json, "TRNCODE"),
                Manifiesto = FlatJson.Value(json, "MANIFIESTO"),
                DisponibilidadInmediata = FlatJson.Value(json, "DISPONIBILIDADINMEDIATA"),
                TipoId = FlatJson.Value(json, "TIPOID"),
                Nacionalidad = FlatJson.Value(json, "NACIONALIDAD"),
                NumeroId1 = FlatJson.Value(json, "NUMEROID1"),
                NumeroId2 = FlatJson.Value(json, "NUMEROID2"),
            };
        }

        private static string ReadParameter(IOrganizationService service, string key)
        {
            var q = new QueryExpression(Schema.Parameter.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Parameter.Value),
                TopCount = 1,
            };
            q.Criteria.AddCondition(Schema.Parameter.Key, ConditionOperator.Equal, key);
            q.Criteria.AddCondition(Schema.Parameter.IsActive, ConditionOperator.Equal, true);

            var rows = service.RetrieveMultiple(q).Entities;
            return rows.Count > 0 ? rows[0].GetAttributeValue<string>(Schema.Parameter.Value) : null;
        }
    }
}
