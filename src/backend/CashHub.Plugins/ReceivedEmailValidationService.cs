using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Orquesta una corrida de validación sobre un correo recibido: carga sobre y
    /// reglas, ejecuta el motor y persiste run/hallazgos/estado del correo.
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
            var email = _service.Retrieve(Schema.ReceivedEmail.LogicalName, receivedEmailId,
                new ColumnSet(Schema.ReceivedEmail.SenderAddress, Schema.ReceivedEmail.Subject));

            var envelope = BuildEnvelope(email);
            var rules = LoadRules(out var ruleIdsByKey);
            var engine = new ValidationEngine(EnvelopeRuleSet.All);
            var context = new ValidationContext(envelope, new DataverseValidationDataAccess(_service));

            var started = DateTime.UtcNow;
            var result = engine.Run(rules, context);
            var runId = PersistRun(receivedEmailId, started, result, ruleIdsByKey);
            UpdateEmail(receivedEmailId, result);

            _tracing.Trace("Validate: run={0} passed={1} fails={2} systemIssues={3}",
                runId, result.Passed, result.FailFindings.Count(), result.HasSystemIssues);

            return (runId, result.Passed, result.FailFindings.Count());
        }

        private EmailEnvelope BuildEnvelope(Entity email)
        {
            var query = new QueryExpression(Schema.Attachment.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Attachment.FileName, Schema.Attachment.SizeBytes)
            };
            query.Criteria.AddCondition(Schema.Attachment.EmailLookup, ConditionOperator.Equal, email.Id);
            var attachments = _service.RetrieveMultiple(query).Entities;

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

        private List<RuleDefinition> LoadRules(out Dictionary<string, Guid> ruleIdsByKey)
        {
            // Se cargan TODAS (activas e inactivas) para resolver la clave de la dependencia;
            // el motor solo ejecuta las activas.
            var query = new QueryExpression(Schema.Rule.LogicalName)
            {
                ColumnSet = new ColumnSet(Schema.Rule.Key, Schema.Rule.Name, Schema.Rule.Order,
                    Schema.Rule.IsActive, Schema.Rule.Value, Schema.Rule.FailMessage, Schema.Rule.Dependency)
            };
            var rows = _service.RetrieveMultiple(query).Entities;

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

        private Guid PersistRun(Guid emailId, DateTime started, RunResult result,
            Dictionary<string, Guid> ruleIdsByKey)
        {
            var run = new Entity(Schema.Run.LogicalName)
            {
                [Schema.Run.Name] = $"Validacion de correo {emailId:D}",
                [Schema.Run.Start] = started,
                [Schema.Run.End] = DateTime.UtcNow,
                [Schema.Run.Result] = new OptionSetValue(result.Passed ? Schema.Run.ResultPass : Schema.Run.ResultFail),
                [Schema.Run.SourceEntity] = Schema.ReceivedEmail.LogicalName,
                [Schema.Run.SourceRecordId] = emailId.ToString("D"),
                [Schema.Run.SourceType] = new OptionSetValue(Schema.Run.SourceSharedMailbox)
            };
            var runId = _service.Create(run);

            foreach (var execution in result.Executions)
            {
                if (execution.Outcome == RuleOutcome.Skipped)
                {
                    continue; // las omitidas por dependencia no ensucian el reporte
                }

                if (execution.Outcome == RuleOutcome.Fail)
                {
                    foreach (var finding in execution.Findings)
                    {
                        CreateResult(runId, ruleIdsByKey, execution, fail: true,
                            name: $"{execution.Definition.Key} FAIL",
                            detail: finding.RenderedMessage,
                            sheet: finding.Sheet, cell: finding.CellRef, row: finding.RowNumber);
                    }
                }
                else
                {
                    var fail = execution.Outcome != RuleOutcome.Pass;
                    CreateResult(runId, ruleIdsByKey, execution, fail,
                        name: $"{execution.Definition.Key} {execution.Outcome}",
                        detail: execution.EngineNote,
                        sheet: null, cell: null, row: null);
                }
            }

            return runId;
        }

        private void CreateResult(Guid runId, Dictionary<string, Guid> ruleIdsByKey,
            RuleExecution execution, bool fail, string name, string detail,
            string sheet, string cell, int? row)
        {
            var entity = new Entity(Schema.Result.LogicalName)
            {
                [Schema.Result.Name] = name,
                [Schema.Result.RunLookup] = new EntityReference(Schema.Run.LogicalName, runId),
                [Schema.Result.Outcome] = new OptionSetValue(fail ? Schema.Result.OutcomeFail : Schema.Result.OutcomePass),
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

        private void UpdateEmail(Guid emailId, RunResult result)
        {
            var update = new Entity(Schema.ReceivedEmail.LogicalName, emailId)
            {
                [Schema.ReceivedEmail.ReviewMethod] = new OptionSetValue(Schema.ReceivedEmail.MethodAuto)
            };

            if (result.HasSystemIssues && !result.FailFindings.Any())
            {
                // Nada de negocio falló pero el motor tuvo incidentes: nunca dar Valido a medias.
                update[Schema.ReceivedEmail.ReviewStatus] = new OptionSetValue(Schema.ReceivedEmail.ReviewError);
                update[Schema.ReceivedEmail.ErrorText] =
                    string.Join("\n", result.Executions.Where(e => e.EngineNote != null).Select(e => e.EngineNote));
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
                    string.Join("\n", result.FailFindings.Select(f => f.RenderedMessage));
            }

            _service.Update(update);
        }
    }

    /// <summary>Acceso a datos de Dataverse para las reglas.</summary>
    public sealed class DataverseValidationDataAccess : IValidationDataAccess
    {
        private readonly IOrganizationService _service;

        public DataverseValidationDataAccess(IOrganizationService service) => _service = service;

        public bool IsSenderRegistered(string senderEmail)
        {
            var query = new QueryExpression(Schema.ClientEmail.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(Schema.ClientEmail.Address, ConditionOperator.Equal, senderEmail);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return _service.RetrieveMultiple(query).Entities.Count > 0;
        }
    }
}
