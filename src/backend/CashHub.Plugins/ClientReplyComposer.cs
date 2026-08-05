using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Reporting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Arma el correo de respuesta al cliente. Prioridad: plantilla editable en
    /// CH-Tbl-EmailTemplate; si no existe o está inactiva, cae al diseño embebido
    /// (el cliente NUNCA se queda sin respuesta por un error de configuración).
    /// </summary>
    public sealed class ClientReplyComposer
    {
        public const string TemplateAccepted = "reply-accepted";
        public const string TemplateRejected = "reply-rejected";

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        public ClientReplyComposer(IOrganizationService service, ITracingService tracing)
        {
            _service = service;
            _tracing = tracing;
        }

        public (string Subject, string BodyHtml) BuildAccepted(string clientName, string fileName,
            IReadOnlyList<ReplyBagSummary> bags, DateTime receivedOn, string trackingId, Guid runId)
        {
            var template = LoadTemplate(TemplateAccepted);
            if (template == null)
            {
                _tracing.Trace("Reply[{0}]: sin plantilla '{1}' activa — usando diseño embebido.",
                    trackingId, TemplateAccepted);
                return ($"Declaracion recibida — {trackingId}",
                    ClientReplyBuilder.BuildAccepted(clientName, fileName, bags, receivedOn, trackingId, runId.ToString("D")));
            }

            var data = BaseData(clientName, fileName, receivedOn, trackingId, runId)
                .Set("estado", "aceptado")
                .Set("total_bolsas", bags.Count.ToString())
                .Set("total_depositos", bags.Sum(b => b.Deposits).ToString())
                .Set("total_nio", Amount(bags.Sum(b => b.Nio)))
                .Set("total_usd", Amount(bags.Sum(b => b.Usd)))
                .Set("total_eur", Amount(bags.Sum(b => b.Eur)));
            data.SetList("bolsas", bags.Select(b => new Dictionary<string, string?>
            {
                ["bolsa"] = b.BagNumber,
                ["depositos"] = b.Deposits.ToString(),
                ["nio"] = Amount(b.Nio),
                ["usd"] = Amount(b.Usd),
                ["eur"] = Amount(b.Eur)
            }));

            _tracing.Trace("Reply[{0}]: usando plantilla '{1}' de Dataverse ({2} bolsas).",
                trackingId, TemplateAccepted, bags.Count);
            return (TemplateEngine.Render(template.Value.Subject, data),
                    TemplateEngine.Render(template.Value.Body, data));
        }

        public (string Subject, string BodyHtml) BuildRejected(string? clientName, string fileName,
            IReadOnlyList<ReplyIssue> issues, DateTime receivedOn, string trackingId, Guid runId)
        {
            var template = LoadTemplate(TemplateRejected);
            if (template == null)
            {
                _tracing.Trace("Reply[{0}]: sin plantilla '{1}' activa — usando diseño embebido.",
                    trackingId, TemplateRejected);
                return ($"Su declaracion necesita correcciones — {trackingId}",
                    ClientReplyBuilder.BuildRejected(clientName, fileName, issues, receivedOn, trackingId, runId.ToString("D")));
            }

            var envelope = issues.Where(i => !i.HasLocation).ToList();
            var located = issues.Where(i => i.HasLocation).OrderBy(i => i.Sheet).ThenBy(i => i.Row ?? 0).ToList();

            var data = BaseData(clientName, fileName, receivedOn, trackingId, runId)
                .Set("estado", "rechazado")
                .Set("total_errores", issues.Count.ToString());
            data.SetList("errores_correo", envelope.Select(i => new Dictionary<string, string?>
            {
                ["mensaje"] = i.Message
            }));
            data.SetList("errores_archivo", located.Select(i => new Dictionary<string, string?>
            {
                ["hoja"] = i.Sheet,
                ["celda"] = i.CellRef,
                ["fila"] = i.Row?.ToString(),
                ["ubicacion"] = i.CellRef != null ? $"{i.Sheet}!{i.CellRef}" : i.Sheet,
                ["mensaje"] = i.Message
            }));

            _tracing.Trace("Reply[{0}]: usando plantilla '{1}' de Dataverse ({2} de sobre, {3} ubicados).",
                trackingId, TemplateRejected, envelope.Count, located.Count);
            return (TemplateEngine.Render(template.Value.Subject, data),
                    TemplateEngine.Render(template.Value.Body, data));
        }

        private static TemplateEngine.TemplateData BaseData(string? clientName, string fileName,
            DateTime receivedOn, string trackingId, Guid runId) =>
            new TemplateEngine.TemplateData()
                .Set("cliente", clientName)
                .Set("archivo", fileName)
                .Set("recibido", receivedOn.ToString("dd/MM/yyyy HH:mm"))
                .Set("gestion", trackingId)
                .Set("validacion", runId.ToString("D"));

        private (string Subject, string Body)? LoadTemplate(string key)
        {
            try
            {
                var query = new QueryExpression("sanic_chtblemailtemplate")
                {
                    ColumnSet = new ColumnSet("sanic_subject", "sanic_bodyhtml"),
                    TopCount = 1
                };
                query.Criteria.AddCondition("sanic_name", ConditionOperator.Equal, key);
                query.Criteria.AddCondition("sanic_isactive", ConditionOperator.Equal, true);
                query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

                var row = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
                var body = row?.GetAttributeValue<string>("sanic_bodyhtml");
                if (row == null || string.IsNullOrWhiteSpace(body))
                {
                    return null;
                }
                return (row.GetAttributeValue<string>("sanic_subject") ?? "CashHub", body!);
            }
            catch (Exception ex)
            {
                // Una plantilla mal configurada NO puede dejar al cliente sin respuesta.
                _tracing.Trace("Reply: fallo leyendo la plantilla '{0}' de sanic_chtblemailtemplate ({1}: {2}); " +
                    "se usa el diseño embebido.", key, ex.GetType().Name, ex.Message);
                return null;
            }
        }

        private static string Amount(decimal value) =>
            value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
    }
}
