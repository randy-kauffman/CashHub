using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Parsing;
using CashHub.Validation.Reporting;
using CashHub.Validation.Rules;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Orquesta una corrida de validación sobre un correo recibido: sobre + parseo del
    /// xlsx + motor completo + persistencia + atribución + materialización de la
    /// declaración. Traza cada etapa (regla de la casa: nunca un error genérico).
    /// </summary>
    public sealed class ReceivedEmailValidationService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        public ReceivedEmailValidationService(IOrganizationService service, ITracingService tracing)
        {
            _service = service;
            _tracing = tracing;
        }

        public (Guid RunId, bool Passed, int FailedFindings) Validate(Guid receivedEmailId)
        {
            _tracing.Trace("Validate[{0}]: etapa 1 — cargando correo.", receivedEmailId);
            var email = _service.Retrieve(Schema.ReceivedEmail.LogicalName, receivedEmailId,
                new ColumnSet(Schema.ReceivedEmail.SenderAddress, Schema.ReceivedEmail.Subject,
                    Schema.ReceivedEmail.TrackingName, "createdon"));
            var trackingId = email.GetAttributeValue<string>(Schema.ReceivedEmail.TrackingName) ?? receivedEmailId.ToString("D");
            var receivedOn = email.GetAttributeValue<DateTime?>("createdon") ?? DateTime.UtcNow;

            _tracing.Trace("Validate[{0}]: etapa 2 — cargando adjuntos.", receivedEmailId);
            var attachments = LoadAttachments(receivedEmailId);
            var envelope = BuildEnvelope(email, attachments);
            _tracing.Trace("Validate[{0}]: sobre listo — remitente='{1}', adjuntos={2}, archivo='{3}', {4} KB.",
                receivedEmailId, envelope.SenderEmail, envelope.AttachmentCount,
                envelope.AttachmentFileName, envelope.AttachmentSizeKb);

            var context = new ValidationContext(envelope, new DataverseValidationDataAccess(_service, _tracing));
            ParseAttachment(receivedEmailId, attachments.FirstOrDefault(), context);

            _tracing.Trace("Validate[{0}]: etapa 4 — cargando reglas.", receivedEmailId);
            var rules = LoadRules(out var ruleIdsByKey);
            _tracing.Trace("Validate[{0}]: {1} reglas cargadas ({2} activas).",
                receivedEmailId, rules.Count, rules.Count(r => r.IsActive));

            _tracing.Trace("Validate[{0}]: etapa 5 — ejecutando motor.", receivedEmailId);
            var started = DateTime.UtcNow;
            var result = new ValidationEngine(FullRuleSet.All).Run(rules, context);
            foreach (var execution in result.Executions)
            {
                _tracing.Trace("Validate[{0}]:   {1} => {2}{3}", receivedEmailId,
                    execution.Definition.Key, execution.Outcome,
                    execution.Findings.Count > 0 ? $" ({execution.Findings.Count} hallazgos)" : string.Empty);
            }

            _tracing.Trace("Validate[{0}]: etapa 6 — persistiendo run y hallazgos.", receivedEmailId);
            var runId = PersistRun(receivedEmailId, trackingId, started, result, ruleIdsByKey);

            Guid? declarationId = null;
            if (result.Passed && context.ResolvedClientId != null && context.Workbook != null)
            {
                _tracing.Trace("Validate[{0}]: etapa 7 — validación en verde, materializando declaración.",
                    receivedEmailId);
                declarationId = new DeclarationMaterializer(_service, _tracing)
                    .Materialize(receivedEmailId, trackingId, context.Workbook, context.ResolvedClientId.Value);
            }

            _tracing.Trace("Validate[{0}]: etapa 8 — clasificando al remitente.", receivedEmailId);
            var facts = context.BuildTriageFacts();
            _tracing.Trace(
                "Validate[{0}]: vinculo con el cliente declarado={1}; algun vinculo vigente={2}; " +
                "algun vinculo revocado={3}; vigencia del cliente={4}.",
                receivedEmailId, facts.LinkToClient, facts.HasAnyActiveLink,
                facts.HasAnyRevokedLink, facts.Client);

            var triage = SenderTriage.Classify(result, facts);
            _tracing.Trace(
                "Validate[{0}]: remitente '{1}' clasificado como {2}; responder={3}; detalle={4}. Motivo: {5}",
                receivedEmailId, envelope.SenderEmail ?? "(sin direccion)", triage.Trust,
                triage.MayReplyToSender, triage.MayDiscloseClientDetail, triage.Reason);

            (string Subject, string Body)? reply = null;
            if (triage.MayReplyToSender)
            {
                _tracing.Trace("Validate[{0}]: etapa 9 — componiendo respuesta {1} (gestion {2}).",
                    receivedEmailId, triage.MayDiscloseClientDetail ? "con detalle" : "GENERICA", trackingId);
                reply = ComposeReply(receivedEmailId, result, context, envelope, trackingId, receivedOn,
                    runId, declarationId, triage.MayDiscloseClientDetail);
            }
            else
            {
                // Puerta de RESPUESTA: sin ningun vinculo vigente con el banco no hay a quien
                // escribirle. Hacerlo le confirmaria a un tercero que ese CIF es cliente
                // nuestro, o contradiria una revocacion que hicimos a proposito.
                _tracing.Trace(
                    "Validate[{0}]: etapa 9 — respuesta SUPRIMIDA hacia '{1}' por clasificacion {2}. {3}",
                    receivedEmailId, envelope.SenderEmail ?? "(sin direccion)", triage.Trust, triage.Reason);
            }

            _tracing.Trace("Validate[{0}]: etapa 10 — actualizando estado del correo.", receivedEmailId);
            UpdateEmail(receivedEmailId, result, context.ResolvedClientId, reply, triage, context.DeclaredCif);

            _tracing.Trace("Validate[{0}]: FIN — run={1}, passed={2}, hallazgos={3}, declaracion={4}.",
                receivedEmailId, runId, result.Passed, result.FailFindings.Count(),
                declarationId?.ToString("D") ?? "(no aplica)");
            return (runId, result.Passed, result.FailFindings.Count());
        }

        private List<Entity> LoadAttachments(Guid emailId)
        {
            var query = new QueryExpression(Schema.Attachment.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Attachment.FileName, Schema.Attachment.SizeBytes)
            };
            query.Criteria.AddCondition(Schema.Attachment.EmailLookup, ConditionOperator.Equal, emailId);
            return _service.RetrieveMultiple(query).Entities.ToList();
        }

        private static EmailEnvelope BuildEnvelope(Entity email, List<Entity> attachments)
        {
            var first = attachments.FirstOrDefault();
            return new EmailEnvelope
            {
                SenderEmail = email.GetAttributeValue<string>(Schema.ReceivedEmail.SenderAddress),
                Subject = email.GetAttributeValue<string>(Schema.ReceivedEmail.Subject),
                AttachmentCount = attachments.Count,
                AttachmentFileName = first?.GetAttributeValue<string>(Schema.Attachment.FileName),
                AttachmentSizeKb = (first?.GetAttributeValue<int?>(Schema.Attachment.SizeBytes) ?? 0) / 1024
            };
        }

        private void ParseAttachment(Guid emailId, Entity attachment, ValidationContext context)
        {
            if (attachment == null)
            {
                context.WorkbookError = "el correo no trae adjunto que parsear";
                _tracing.Trace("Validate[{0}]: etapa 3 — sin adjunto, no hay parseo.", emailId);
                return;
            }

            try
            {
                _tracing.Trace("Validate[{0}]: etapa 3 — descargando adjunto {1}.", emailId, attachment.Id);
                var bytes = DownloadFileColumn(attachment.Id);
                var maxBytes = ResolveMaxUncompressedBytes();
                _tracing.Trace("Validate[{0}]: adjunto descargado ({1} bytes); parseando xlsx (tope descomprimido {2} MB).",
                    emailId, bytes.Length, maxBytes / (1024 * 1024));
                context.Workbook = XlsxParser.Parse(bytes, maxBytes);
                _tracing.Trace("Validate[{0}]: xlsx parseado — {1} tablas, {2} claves de metadata.",
                    emailId, context.Workbook.Tables.Count, context.Workbook.Metadata.Count);
            }
            catch (XlsxParseException ex)
            {
                // Error de NEGOCIO (archivo malo del cliente): lo reporta VR-07 con el detalle.
                context.WorkbookError = ex.Message;
                _tracing.Trace("Validate[{0}]: el adjunto no parsea — {1}", emailId, ex.Message);
            }
        }

        /// <summary>
        /// Lee el tope de tamaño descomprimido de CH-Tbl-Parameter (en MB) y lo pasa a bytes.
        /// Se lee ANTES de parsear (etapa 3), asi que no puede salir del motor de reglas
        /// (etapa 5) — por eso vive en la tabla de parametros y no en el valor de una regla.
        ///
        /// Fail-safe en todos los caminos: fila ausente, inactiva, valor no numerico o
        /// consulta que revienta => se devuelve null y XlsxParser cae a su default
        /// hardcodeado. Un parametro roto NUNCA desactiva el guardia; el clamp al techo lo
        /// hace el propio parser.
        /// </summary>
        private long? ResolveMaxUncompressedBytes()
        {
            try
            {
                var query = new QueryExpression(Schema.Parameter.LogicalName)
                {
                    ColumnSet = new ColumnSet(Schema.Parameter.Value),
                    TopCount = 1
                };
                query.Criteria.AddCondition(Schema.Parameter.Key, ConditionOperator.Equal,
                    Schema.Parameter.MaxUncompressedMbKey);
                query.Criteria.AddCondition(Schema.Parameter.IsActive, ConditionOperator.Equal, true);

                var rows = _service.RetrieveMultiple(query).Entities;
                var raw = rows.Count > 0 ? rows[0].GetAttributeValue<string>(Schema.Parameter.Value) : null;

                if (long.TryParse(raw?.Trim(), out var mb) && mb > 0)
                {
                    _tracing.Trace("Validate: tope descomprimido tomado de CH-Tbl-Parameter '{0}' = {1} MB.",
                        Schema.Parameter.MaxUncompressedMbKey, mb);
                    return mb * 1024L * 1024L;
                }

                _tracing.Trace("Validate: parametro '{0}' ausente o no numerico ('{1}'); se usa el default del parser.",
                    Schema.Parameter.MaxUncompressedMbKey, raw ?? "(sin fila)");
                return null;
            }
            catch (Exception ex)
            {
                // Nunca dejar sin parsear un correo por una falla al leer un parametro.
                _tracing.Trace("Validate: fallo leyendo el parametro '{0}' ({1}); se usa el default del parser.",
                    Schema.Parameter.MaxUncompressedMbKey, ex.Message);
                return null;
            }
        }

        private byte[] DownloadFileColumn(Guid attachmentId)
        {
            var init = new OrganizationRequest("InitializeFileBlocksDownload")
            {
                ["Target"] = new EntityReference(Schema.Attachment.LogicalName, attachmentId),
                ["FileAttributeName"] = Schema.Attachment.FileContent
            };
            OrganizationResponse initResponse;
            try
            {
                initResponse = _service.Execute(init);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    $"Descarga del adjunto {attachmentId:D}: InitializeFileBlocksDownload fallo sobre la columna " +
                    $"'{Schema.Attachment.FileContent}' de {Schema.Attachment.LogicalName}. ¿El adjunto tiene archivo cargado? Detalle: {ex.Message}", ex);
            }

            var token = (string)initResponse["FileContinuationToken"];
            var size = (long)initResponse["FileSizeInBytes"];
            using (var buffer = new MemoryStream())
            {
                long offset = 0;
                const long blockSize = 4 * 1024 * 1024;
                while (offset < size)
                {
                    var block = _service.Execute(new OrganizationRequest("DownloadBlock")
                    {
                        ["FileContinuationToken"] = token,
                        ["Offset"] = offset,
                        ["BlockLength"] = Math.Min(blockSize, size - offset)
                    });
                    var data = (byte[])block["Data"];
                    buffer.Write(data, 0, data.Length);
                    offset += data.Length;
                }
                return buffer.ToArray();
            }
        }

        private List<RuleDefinition> LoadRules(out Dictionary<string, Guid> ruleIdsByKey)
        {
            var query = new QueryExpression(Schema.Rule.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Rule.Key, Schema.Rule.Name, Schema.Rule.Order,
                    Schema.Rule.IsActive, Schema.Rule.Value, Schema.Rule.FailMessage, Schema.Rule.Dependency)
            };
            var rows = _service.RetrieveMultiple(query).Entities;
            if (rows.Count == 0)
            {
                throw new InvalidPluginExecutionException(
                    $"Carga de reglas: la tabla {Schema.Rule.LogicalName} esta VACIA en este ambiente. " +
                    "Sembrar data/seed/50-validationrules.json (scripts/seed-data.sh) antes de validar correos.");
            }

            var keyById = rows.ToDictionary(r => r.Id, r => r.GetAttributeValue<string>(Schema.Rule.Key));
            ruleIdsByKey = rows
                .Where(r => keyById[r.Id] != null)
                .ToDictionary(r => keyById[r.Id], r => r.Id, StringComparer.OrdinalIgnoreCase);

            return rows
                .Where(r => r.GetAttributeValue<string>(Schema.Rule.Key) != null)
                .Select(r => new RuleDefinition(
                    r.GetAttributeValue<string>(Schema.Rule.Key),
                    r.GetAttributeValue<string>(Schema.Rule.Name) ?? r.GetAttributeValue<string>(Schema.Rule.Key),
                    r.GetAttributeValue<int?>(Schema.Rule.Order) ?? int.MaxValue,
                    r.GetAttributeValue<bool?>(Schema.Rule.IsActive) ?? false,
                    r.GetAttributeValue<string>(Schema.Rule.Value),
                    r.GetAttributeValue<string>(Schema.Rule.FailMessage),
                    DependencyKey(r, keyById)))
                .ToList();
        }

        private static string DependencyKey(Entity rule, Dictionary<Guid, string> keyById)
        {
            var dep = rule.GetAttributeValue<EntityReference>(Schema.Rule.Dependency);
            return dep != null && keyById.TryGetValue(dep.Id, out var key) ? key : null;
        }

        private Guid PersistRun(Guid emailId, string trackingId, DateTime started, RunResult result,
            Dictionary<string, Guid> ruleIdsByKey)
        {
            var run = new Entity(Schema.Run.LogicalName)
            {
                // El nombre usa el numero de gestion (Email-N), no el GUID: es lo que
                // el operador ve en apps y vistas.
                [Schema.Run.Name] = $"Validacion de {trackingId}",
                [Schema.Run.Start] = started,
                [Schema.Run.End] = DateTime.UtcNow,
                [Schema.Run.Result] = new OptionSetValue(result.Passed ? Schema.Run.ResultPass : Schema.Run.ResultFail),
                [Schema.Run.SourceEntity] = Schema.ReceivedEmail.LogicalName,
                [Schema.Run.SourceRecordId] = emailId.ToString("D"),
                [Schema.Run.ReceivedEmailLookup] =
                    new EntityReference(Schema.ReceivedEmail.LogicalName, emailId),
                [Schema.Run.SourceType] = new OptionSetValue(Schema.Run.SourceSharedMailbox)
            };
            var runId = _service.Create(run);
            _tracing.Trace("Validate[{0}]: ValidationRun {1} creado.", emailId, runId);

            foreach (var execution in result.Executions)
            {
                if (execution.Outcome == RuleOutcome.Skipped)
                {
                    // B2: las omitidas por dependencia SI se persisten (Omitida):
                    // el tracker muestra el cuadro completo de las 28 reglas y por
                    // que cada una no corrio (la madre que fallo va en la nota).
                    CreateResult(runId, ruleIdsByKey, execution, Schema.Result.OutcomeOmitida,
                        name: $"{execution.Definition.Key} Omitida",
                        detail: execution.EngineNote,
                        sheet: null, cell: null, row: null);
                    continue;
                }

                if (execution.Outcome == RuleOutcome.Fail)
                {
                    foreach (var finding in execution.Findings)
                    {
                        CreateResult(runId, ruleIdsByKey, execution, Schema.Result.OutcomeFail,
                            name: $"{execution.Definition.Key} FAIL",
                            detail: finding.RenderedMessage,
                            sheet: finding.Sheet, cell: finding.CellRef, row: finding.RowNumber);
                    }
                }
                else
                {
                    var outcome = execution.Outcome == RuleOutcome.Pass
                        ? Schema.Result.OutcomePass
                        : Schema.Result.OutcomeFail;
                    CreateResult(runId, ruleIdsByKey, execution, outcome,
                        name: $"{execution.Definition.Key} {execution.Outcome}",
                        detail: execution.EngineNote,
                        sheet: null, cell: null, row: null);
                }
            }

            return runId;
        }

        private void CreateResult(Guid runId, Dictionary<string, Guid> ruleIdsByKey,
            RuleExecution execution, int outcome, string name, string detail,
            string sheet, string cell, int? row)
        {
            var entity = new Entity(Schema.Result.LogicalName)
            {
                [Schema.Result.Name] = name,
                [Schema.Result.RunLookup] = new EntityReference(Schema.Run.LogicalName, runId),
                [Schema.Result.Outcome] = new OptionSetValue(outcome),
                [Schema.Result.DurationMs] = (int)execution.DurationMs,
                [Schema.Result.SourceType] = new OptionSetValue(Schema.Run.SourceSharedMailbox)
            };
            if (ruleIdsByKey.TryGetValue(execution.Definition.Key, out var ruleId))
            {
                entity[Schema.Result.RuleLookup] = new EntityReference(Schema.Rule.LogicalName, ruleId);
            }
            if (detail != null) entity[Schema.Result.Detail] = detail;
            if (sheet != null) entity[Schema.Result.Sheet] = sheet;
            if (cell != null) entity[Schema.Result.CellRef] = cell;
            if (row != null) entity[Schema.Result.RowNumber] = row.Value;

            _service.Create(entity);
        }

        /// <summary>
        /// Arma asunto y cuerpo HTML de la respuesta al cliente. Si algo falla acá, el correo
        /// igual queda validado: la respuesta se reintenta desde la Console o el barrido.
        /// </summary>
        private (string Subject, string Body)? ComposeReply(Guid emailId, RunResult result,
            ValidationContext context, EmailEnvelope envelope, string trackingId,
            DateTime receivedOn, Guid runId, Guid? declarationId, bool mayDiscloseClientDetail)
        {
            try
            {
                var composer = new ClientReplyComposer(_service, _tracing);
                var fileName = envelope.AttachmentFileName ?? "(sin adjunto)";

                if (!mayDiscloseClientDetail)
                {
                    // Puerta de CONTENIDO cerrada: es un contacto vigente del banco, pero no
                    // de ESTE cliente. Ni el nombre, ni el CIF, ni los hallazgos — listarlos
                    // le confirmaria que ese cliente opera con nosotros.
                    _tracing.Trace("Validate[{0}]: respuesta GENERICA (sin datos del cliente).", emailId);
                    return composer.BuildGeneric(fileName, receivedOn, trackingId, runId);
                }

                var clientName = ClientName(context.ResolvedClientId);

                if (result.Passed && declarationId != null)
                {
                    return composer.BuildAccepted(clientName ?? "Cliente", fileName,
                        BagSummaries(declarationId.Value), receivedOn, trackingId, runId);
                }

                var issues = result.FailFindings
                    .Select(f => new ReplyIssue(f.RenderedMessage ?? string.Empty, f.Sheet, f.CellRef, f.RowNumber))
                    .ToList();
                if (issues.Count == 0)
                {
                    // Falla de sistema (sin hallazgos de negocio): no se responde al cliente.
                    _tracing.Trace("Validate[{0}]: sin hallazgos de negocio; la respuesta al cliente se omite.", emailId);
                    return null;
                }
                return composer.BuildRejected(clientName, fileName, issues, receivedOn, trackingId, runId);
            }
            catch (Exception ex)
            {
                _tracing.Trace("Validate[{0}]: fallo componiendo la respuesta ({1}: {2}); el correo queda validado igual.",
                    emailId, ex.GetType().Name, ex.Message);
                return null;
            }
        }

        private string? ClientName(Guid? clientId)
        {
            if (clientId == null) return null;
            var client = _service.Retrieve("sanic_chtblclient", clientId.Value, new ColumnSet("sanic_name"));
            return client.GetAttributeValue<string>("sanic_name");
        }

        private List<ReplyBagSummary> BagSummaries(Guid declarationId)
        {
            var query = new QueryExpression("sanic_chtbldeclaredbags")
            {
                ColumnSet = new ColumnSet("sanic_bagnumber", "sanic_totalnio", "sanic_totalusd",
                    "sanic_totaleur", "sanic_totaldeposits")
            };
            query.Criteria.AddCondition("sanic_declarationid", ConditionOperator.Equal, declarationId);
            return _service.RetrieveMultiple(query).Entities
                .Select(b => new ReplyBagSummary(
                    b.GetAttributeValue<string>("sanic_bagnumber") ?? "-",
                    b.GetAttributeValue<int?>("sanic_totaldeposits") ?? 0,
                    b.GetAttributeValue<decimal?>("sanic_totalnio") ?? 0m,
                    b.GetAttributeValue<decimal?>("sanic_totalusd") ?? 0m,
                    b.GetAttributeValue<decimal?>("sanic_totaleur") ?? 0m))
                .ToList();
        }

        /// <summary>
        /// El resumen en el correo tiene tope de columna; el detalle completo SIEMPRE vive
        /// en los ValidationResults. Cortamos con aviso explícito de cuántos faltan.
        /// </summary>
        private static string CapForColumn(IEnumerable<string> lines, int maxLength = 90000)
        {
            var included = new List<string>();
            var total = 0;
            var length = 0;
            foreach (var line in lines)
            {
                total++;
                if (length + line.Length + 1 <= maxLength)
                {
                    included.Add(line);
                    length += line.Length + 1;
                }
            }
            if (included.Count < total)
            {
                included.Add($"... y {total - included.Count} errores mas (detalle completo en los resultados de validacion).");
            }
            return string.Join("\n", included);
        }

        private void UpdateEmail(Guid emailId, RunResult result, Guid? resolvedClientId,
            (string Subject, string Body)? reply = null, TriageDecision triage = null,
            string declaredCif = null)
        {
            var update = new Entity(Schema.ReceivedEmail.LogicalName, emailId)
            {
                [Schema.ReceivedEmail.ReviewMethod] = new OptionSetValue(Schema.ReceivedEmail.MethodAuto)
            };

            if (!string.IsNullOrWhiteSpace(declaredCif))
            {
                update[Schema.ReceivedEmail.DeclaredCif] = declaredCif;
            }

            if (triage != null)
            {
                update[Schema.ReceivedEmail.SenderClassification] = new OptionSetValue(ClassificationValue(triage.Trust));
                update[Schema.ReceivedEmail.TriageNotes] = triage.Reason;
            }

            if (resolvedClientId != null)
            {
                // El contenido ATRIBUYE: el correo queda vinculado al cliente aunque falle.
                update[Schema.ReceivedEmail.ClientLookup] =
                    new EntityReference("sanic_chtblclient", resolvedClientId.Value);
            }

            if (result.HasSystemIssues && !result.FailFindings.Any())
            {
                update[Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(Schema.ReceivedEmail.ReviewError);
                update[Schema.ReceivedEmail.ErrorText] =
                    CapForColumn(result.Executions.Where(e => e.EngineNote != null).Select(e => e.EngineNote));
            }
            else if (result.Passed)
            {
                update[Schema.ReceivedEmail.ValidationStatus] = new OptionSetValue(Schema.ReceivedEmail.ValidationValido);
                update[Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(Schema.ReceivedEmail.ReviewAceptado);
                update[Schema.ReceivedEmail.ErrorText] = null;
            }
            else
            {
                update[Schema.ReceivedEmail.ValidationStatus] = new OptionSetValue(Schema.ReceivedEmail.ValidationInvalido);
                update[Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(Schema.ReceivedEmail.ReviewDenegado);
                update[Schema.ReceivedEmail.ErrorText] =
                    CapForColumn(result.FailFindings.Select(f => f.RenderedMessage));
            }

            if (reply != null)
            {
                update[Schema.ReceivedEmail.ReplySubject] = reply.Value.Subject;
                update[Schema.ReceivedEmail.ReplyBodyHtml] = reply.Value.Body;
                update[Schema.ReceivedEmail.ReplyStatus] = new OptionSetValue(Schema.ReceivedEmail.ReplyPendiente);
            }
            else if (triage != null && !triage.MayReplyToSender)
            {
                // Omitido, no Pendiente: el flujo de respuesta jamas debe levantar este correo.
                update[Schema.ReceivedEmail.ReplyStatus] = new OptionSetValue(Schema.ReceivedEmail.ReplyOmitido);
            }

            // Ruteo de cola: INDEPENDIENTE de si salio respuesta. Un vinculo revocado exige
            // ojo humano lo mismo si le contestamos generico que si no le contestamos nada;
            // colgar esto del else de la respuesta dejaba el caso "revocado para este cliente
            // pero vigente con otro" —el unico intento de usar un acceso que quitamos— sin
            // escalar, que es exactamente el escenario que motivo el balde nuevo.
            // MayDiscloseClientDetail solo es true en Registered, asi que esta condicion
            // cubre los otros siete estados de confianza.
            if (triage != null && !triage.MayDiscloseClientDetail)
            {
                update[Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(ReviewFor(triage.Trust));
            }

            _service.Update(update);
        }

        private static int ClassificationValue(SenderTrust trust)
        {
            switch (trust)
            {
                case SenderTrust.Registered: return Schema.ReceivedEmail.SenderRegistrado;
                case SenderTrust.RevokedForClient: return Schema.ReceivedEmail.SenderRevocadoParaCliente;
                case SenderTrust.OtherClientContact: return Schema.ReceivedEmail.SenderContactoDeOtroCliente;
                case SenderTrust.Revoked: return Schema.ReceivedEmail.SenderRevocado;
                case SenderTrust.UnregisteredClient: return Schema.ReceivedEmail.SenderClienteSinRegistrar;
                case SenderTrust.Suspicious: return Schema.ReceivedEmail.SenderSospechoso;
                case SenderTrust.Unrelated: return Schema.ReceivedEmail.SenderNoCorresponde;
                default:
                    throw new InvalidPluginExecutionException(
                        $"Triage desconocido '{trust}' al clasificar el remitente: no hay valor de " +
                        "opcion equivalente en sanic_senderclassification. Revisar SenderTriage.");
            }
        }

        /// <summary>
        /// A que cola va el correo. Manual = necesita una persona: Banca de Empresas para
        /// registrar o firmar un alta, seguridad para un acceso revocado.
        /// </summary>
        private static int ReviewFor(SenderTrust trust)
        {
            switch (trust)
            {
                // El vinculo con este cliente no autoriza (revocado, o pedido y sin firmar).
                // Necesita ojo humano: seguridad si le quitamos el acceso, Banca si hay un
                // RFC de alta esperando firma. El recordstatus del vinculo dice cual es.
                case SenderTrust.RevokedForClient:
                // Contacto legitimo sin registrar todavia.
                case SenderTrust.UnregisteredClient:
                    return Schema.ReceivedEmail.ReviewManual;

                // Contacto vivo del banco que erro de CIF: ya se le respondio, la pelota
                // esta de su lado. No inflamos la cola de revision con esto.
                case SenderTrust.OtherClientContact:
                    return Schema.ReceivedEmail.ReviewDenegado;

                // Revocado en todos lados, plantilla ajena o ruido: cuarentena visible.
                default:
                    return Schema.ReceivedEmail.ReviewOmitido;
            }
        }
    }

    /// <summary>Acceso a datos de Dataverse para las reglas, con tracing de cada consulta.</summary>
    public sealed class DataverseValidationDataAccess : IValidationDataAccess
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        public DataverseValidationDataAccess(IOrganizationService service, ITracingService tracing)
        {
            _service = service;
            _tracing = tracing;
        }

        /// <summary>
        /// Trae TODOS los vinculos de la direccion, de todos los clientes, con su vigencia.
        /// Una sola consulta por corrida (el ValidationContext la cachea).
        /// </summary>
        public IReadOnlyList<SenderLink> GetSenderLinks(string senderEmail)
        {
            var query = new QueryExpression(Schema.ClientEmail.LogicalName)
            {
                ColumnSet = new ColumnSet("sanic_client", Schema.MasterRecordStatus.FieldName)
            };
            query.Criteria.AddCondition(Schema.ClientEmail.Address, ConditionOperator.Equal, senderEmail);

            var links = new List<SenderLink>();
            foreach (var row in _service.RetrieveMultiple(query).Entities)
            {
                var client = row.GetAttributeValue<EntityReference>("sanic_client");
                if (client == null) continue;   // sanic_client es required; defensivo igual
                var vigency = RecordVigencyMap.FromRecordStatus(row.GetAttributeValue<OptionSetValue>(
                    Schema.MasterRecordStatus.FieldName)?.Value);
                links.Add(new SenderLink(client.Id, vigency));
                _tracing.Trace("DataAccess.GetSenderLinks('{0}'): cliente {1} => {2}",
                    senderEmail, client.Id, vigency);
            }
            _tracing.Trace("DataAccess.GetSenderLinks('{0}') = {1} vinculo(s).", senderEmail, links.Count);
            return links;
        }

        /// <summary>
        /// Resuelve el cliente por cualquiera de sus TRES identificadores. La plantilla
        /// trae un solo dato en Depositos!J2 y el cliente puede declarar con el que tenga
        /// a mano; el motor no le exige adivinar cual guardamos nosotros.
        ///
        /// TopCount = 2, no 1: con tres columnas en OR la superficie de colision crece, y
        /// dos clientes distintos podrian responder al mismo texto (el CIFBCO de uno y el
        /// taxid de otro). Quedarse con rows[0] atribuiria la declaracion al cliente
        /// EQUIVOCADO en silencio. Se levanta la excepcion: el motor la aisla como Error,
        /// el correo va a Revision con error y NO se le responde al cliente — es un
        /// problema de datos nuestro, no suyo.
        /// </summary>
        public ClientLookup FindClientByCif(string cif)
        {
            var query = new QueryExpression("sanic_chtblclient")
            {
                ColumnSet = new ColumnSet(Schema.MasterRecordStatus.FieldName),
                TopCount = 2
            };
            var filter = new FilterExpression(LogicalOperator.Or);
            foreach (var column in Schema.Client.IdentifierColumns)
            {
                filter.AddCondition(column, ConditionOperator.Equal, cif);
            }
            query.Criteria.AddFilter(filter);

            var rows = _service.RetrieveMultiple(query).Entities;
            if (rows.Count == 0)
            {
                _tracing.Trace("DataAccess.FindClientByCif('{0}') = (no existe)", cif);
                return ClientLookup.NotFound;
            }

            if (rows.Count > 1)
            {
                throw new InvalidPluginExecutionException(
                    $"El identificador '{cif}' resuelve a MAS DE UN cliente en " +
                    $"{string.Join(", ", Schema.Client.IdentifierColumns)} (al menos " +
                    $"{rows[0].Id:D} y {rows[1].Id:D}). No se atribuye la declaracion a " +
                    "ninguno: corrija los identificadores duplicados en CH-Tbl-Client.");
            }

            var vigency = RecordVigencyMap.FromRecordStatus(rows[0].GetAttributeValue<OptionSetValue>(
                Schema.MasterRecordStatus.FieldName)?.Value);
            _tracing.Trace("DataAccess.FindClientByCif('{0}') = {1} ({2})", cif, rows[0].Id, vigency);
            return new ClientLookup(rows[0].Id, vigency);
        }

        public bool AccountBelongsToClient(string accountNumber, Guid clientId)
        {
            var query = VigentQuery("sanic_chtblclientaccount");
            query.Criteria.AddCondition("sanic_name", ConditionOperator.Equal, accountNumber);
            query.Criteria.AddCondition("sanic_client", ConditionOperator.Equal, clientId);
            var found = _service.RetrieveMultiple(query).Entities.Count > 0;
            _tracing.Trace("DataAccess.AccountBelongsToClient('{0}', {1}) = {2}",
                accountNumber, clientId, found);
            return found;
        }

        /// <summary>
        /// Consulta filtrada a los registros vigentes. Los valores salen de
        /// RecordVigencyMap.VigentValues — la MISMA fuente que traduce la fila leida, para
        /// que el filtro y la traduccion no puedan divergir (hay un test que lo verifica).
        ///
        /// Antes esto filtraba por statecode = 0, y CashHub nunca escribe statecode en las
        /// tablas maestras: la baja va por sanic_recordstatus (ver
        /// ResolveChangeRequestPlugin.ApplyRecordStatusChange). El filtro era un no-op —
        /// siempre daba verdadero — y con eso un cliente dado de baja o una direccion
        /// revocada pasaban las cuatro reglas de autorizacion.
        /// </summary>
        private static QueryExpression VigentQuery(string entity)
        {
            var query = new QueryExpression(entity) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            var vigente = new FilterExpression(LogicalOperator.Or);
            foreach (var value in RecordVigencyMap.VigentValues)
            {
                vigente.AddCondition(Schema.MasterRecordStatus.FieldName, ConditionOperator.Equal, value);
            }
            query.Criteria.AddFilter(vigente);
            return query;
        }
    }
}
