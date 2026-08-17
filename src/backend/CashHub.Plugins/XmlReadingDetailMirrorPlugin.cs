using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejo E-4 de CH-App-Recounting v2 (01 §2.2) — son DOS columnas, no una
    /// (divergencia L-3 del ledger):
    ///
    ///   sanic_recountingsessiondetailmirror (lookup ->
    ///       sanic_chtblrecountingsessiondetail)  y
    ///   sanic_xmlreadingactiva              (bit)
    ///   en sanic_chtblxmlreadingdetail, derivadas de
    ///   sanic_xmlreading.sanic_recountingsessiondetailid y .sanic_isactive.
    ///
    /// Tres pasos SINCRONOS:
    ///   - Pre-operation  (20) Create de sanic_chtblxmlreadingdetail: estampa las dos
    ///     EN el Target (cero escrituras adicionales).
    ///   - Pre-operation  (20) Update de sanic_chtblxmlreadingdetail, filtrado en
    ///     sanic_xmlreading: re-estampa al re-colgar el detalle de otra lectura.
    ///   - Post-operation (40) Update de sanic_chtblxmlreading, filtrado en
    ///     sanic_recountingsessiondetailid + sanic_isactive y con PreImage de las dos:
    ///     CASCADA a TODOS los detalles de la lectura, paginada de a 500.
    ///
    /// La cascada NO es un lujo — es el corazon del contrato: cuando llega una lectura
    /// nueva de la contadora, la vieja se APAGA (IsActive -> false) DESPUES de que sus
    /// detalles ya existen. Sin propagar, Q17 sigue sumando la lectura muerta y la
    /// columna Auto del cajero miente.
    ///
    /// Por que existen las columnas: Q17 ("Leer contadora", 04 §8.4) necesita DOS
    /// condiciones —la sesion-detalle Y que la lectura este activa— en UN salto. La v1
    /// escribia Filter(details, XMLReading.Id in Filter(readings, ...).Id): un "in" con
    /// subconsulta que JAMAS delega (regla D-6), y con >500 filas historicas la columna
    /// Auto mostraba 0 (bug D-1). Con un espejo solo del lookup habria que volver a
    /// CH-Tbl-XMLReadings a preguntar IsActive: vuelve el segundo salto. Por eso van
    /// las dos, y por eso las escribe el MISMO plugin (un solo dueno por par).
    ///
    /// Volumen: los detalles de UNA lectura son las denominaciones contadas (decenas),
    /// asi que la cascada paginada de a 500 alcanza de sobra — no hace falta
    /// ExecuteMultiple/UpdateMultiple, que ademas cambiaria el manejo de errores
    /// parciales dentro de la transaccion. La decision de que propagar vive en el
    /// dominio puro (RecountingMirrors).
    /// </summary>
    public sealed class XmlReadingDetailMirrorPlugin : IPlugin
    {
        /// <summary>Tamano de pagina de la cascada (mismo que los demas espejos).</summary>
        private const int PageSize = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("XmlReadingDetailMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("XmlReadingDetailMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            try
            {
                var system = serviceFactory.CreateOrganizationService(null);

                if (context.PrimaryEntityName == Schema.XmlReading.LogicalName)
                {
                    CascadeReadingToDetails(system, tracing, context, target);
                }
                else if (context.PrimaryEntityName == Schema.XmlReadingDetail.LogicalName)
                {
                    StampReading(system, tracing, context.MessageName, target);
                }
                else
                {
                    tracing.Trace("XmlReadingDetailMirror: entidad {0} fuera de alcance, se omite.",
                        context.PrimaryEntityName);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("XmlReadingDetailMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudieron mantener los espejos de la lectura de contadora en el detalle (E-4): "
                    + ex.Message, ex);
            }
        }

        /// <summary>
        /// Resuelve la lectura del Target y escribe las DOS columnas espejo EN el
        /// propio Target: UNA sola lectura y CERO escrituras adicionales.
        /// </summary>
        private static void StampReading(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var readingTouched = target.Contains(Schema.XmlReadingDetail.ReadingLookup);

            if (!RecountingMirrors.ShouldStamp(message, readingTouched))
            {
                tracing.Trace(
                    "XmlReadingDetailMirror: Update de detalle sin cambio de lectura, sin re-estampa.");
                return;
            }

            var readingRef = target.GetAttributeValue<EntityReference>(
                Schema.XmlReadingDetail.ReadingLookup);

            if (readingRef == null)
            {
                // Sin lectura no hay ni sesion-detalle ni vigencia: las DOS en blanco.
                // Nunca "activa" por defecto: un detalle huerfano no puede entrar a Q17.
                target[Schema.XmlReadingDetail.SessionDetailMirror] = null;
                target[Schema.XmlReadingDetail.ActiveMirror] = null;
                tracing.Trace("XmlReadingDetailMirror: detalle sin lectura, los dos espejos en blanco.");
                return;
            }

            // Lectura como sistema: es derivacion, no permiso de negocio.
            var reading = system.Retrieve(
                Schema.XmlReading.LogicalName, readingRef.Id,
                new ColumnSet(Schema.XmlReading.SessionDetailLookup, Schema.XmlReading.IsActive));

            var sessionDetailRef = reading.GetAttributeValue<EntityReference>(
                Schema.XmlReading.SessionDetailLookup);
            var isActive = reading.Contains(Schema.XmlReading.IsActive)
                ? (bool?)reading.GetAttributeValue<bool>(Schema.XmlReading.IsActive)
                : null;

            target[Schema.XmlReadingDetail.SessionDetailMirror] = sessionDetailRef;
            target[Schema.XmlReadingDetail.ActiveMirror] = isActive;

            tracing.Trace(
                "XmlReadingDetailMirror: detalle {0} ({1}) -> lectura {2}, sesion-detalle {3}, activa {4}.",
                target.Id, message, readingRef.Id,
                sessionDetailRef != null ? sessionDetailRef.Id.ToString() : "null",
                isActive.HasValue ? isActive.Value.ToString() : "null");
        }

        /// <summary>
        /// Cascada del lado LECTURA: reescribe las DOS columnas en todos los detalles,
        /// paginado, y SOLO si alguna cambio de verdad (para eso la PreImage —que
        /// ademas aporta el valor EFECTIVO de la columna que no vino en el Target).
        /// </summary>
        private static void CascadeReadingToDetails(
            IOrganizationService system, ITracingService tracing,
            IPluginExecutionContext context, Entity target)
        {
            Entity pre;
            if (!context.PreEntityImages.TryGetValue(Schema.XmlReading.PreImageName, out pre) || pre == null)
            {
                tracing.Trace(
                    "XmlReadingDetailMirror: falta la PreImage '{0}' del paso de cascada — "
                    + "no se puede saber si la lectura cambio, se omite.",
                    Schema.XmlReading.PreImageName);
                return;
            }

            var sessionDetailTouched = target.Contains(Schema.XmlReading.SessionDetailLookup);
            var activeTouched = target.Contains(Schema.XmlReading.IsActive);

            var plan = RecountingMirrors.ResolveReadingCascade(
                sessionDetailTouched,
                newSessionDetailId: target
                    .GetAttributeValue<EntityReference>(Schema.XmlReading.SessionDetailLookup)?.Id,
                previousSessionDetailId: pre
                    .GetAttributeValue<EntityReference>(Schema.XmlReading.SessionDetailLookup)?.Id,
                activeTouched: activeTouched,
                newActive: activeTouched
                    ? (bool?)target.GetAttributeValue<bool>(Schema.XmlReading.IsActive)
                    : null,
                previousActive: pre.Contains(Schema.XmlReading.IsActive)
                    ? (bool?)pre.GetAttributeValue<bool>(Schema.XmlReading.IsActive)
                    : null);

            if (!plan.Propagate)
            {
                tracing.Trace(
                    "XmlReadingDetailMirror: Update de lectura sin cambio real "
                    + "(detalle-tocado={0}, activa-tocada={1}), no se toca ningun detalle.",
                    sessionDetailTouched, activeTouched);
                return;
            }

            var readingId = target.Id != Guid.Empty ? target.Id : context.PrimaryEntityId;

            var sessionDetailValue = plan.SessionDetailId.HasValue
                ? new EntityReference(
                    Schema.RecountingSessionDetail.LogicalName, plan.SessionDetailId.Value)
                : null;

            var query = new QueryExpression(Schema.XmlReadingDetail.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = PageSize }
            };
            // Por el vinculo REAL (sanic_xmlreading), que es la fuente de verdad de
            // quien es hijo de esta lectura — no por el espejo, que es justamente lo
            // que puede estar stale y estamos arreglando.
            query.Criteria.AddCondition(
                Schema.XmlReadingDetail.ReadingLookup, ConditionOperator.Equal, readingId);

            var updated = 0;
            while (true)
            {
                var page = system.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    // Las DOS juntas SIEMPRE: son un par y escribir una sola dejaria al
                    // detalle con mitad de la verdad de su lectura.
                    system.Update(new Entity(Schema.XmlReadingDetail.LogicalName, row.Id)
                    {
                        [Schema.XmlReadingDetail.SessionDetailMirror] = sessionDetailValue,
                        [Schema.XmlReadingDetail.ActiveMirror] = plan.IsActive
                    });
                    updated++;
                }

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            tracing.Trace(
                "XmlReadingDetailMirror: lectura {0} -> sesion-detalle {1}, activa {2}; "
                + "{3} detalles re-espejados.",
                readingId,
                plan.SessionDetailId.HasValue ? plan.SessionDetailId.Value.ToString() : "null",
                plan.IsActive.HasValue ? plan.IsActive.Value.ToString() : "null",
                updated);
        }
    }
}
