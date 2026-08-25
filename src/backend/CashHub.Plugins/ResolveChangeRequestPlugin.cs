using System;
using System.Collections.Generic;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Backing del Custom API sanic_CHResolveChangeRequest: reemplaza los 4 flujos
    /// materializadores de RFC (Applyclientdatachanges / Createclientdata / Activaterow /
    /// Deactivaterow, docs/clientadmin-rebuild/00-analisis-y-plan.md §3.1). Resuelve
    /// Approve/Reject de UN RFC de forma SINCRONA y ATOMICA: si algo falla, TODA la
    /// transaccion (maestro + lineas + bitacora + cabecera) se revierte y la excepcion
    /// sale con el motivo de negocio — nunca un "exito" con la mitad de los campos
    /// aplicados (mata L1) ni una ventana Approved-sin-aplicar (mata L3/L5/P4).
    ///
    /// Inputs: ChangeRequestId (Guid, requerido), Decision (String "Approve"|"Reject",
    /// requerido), Comment (String, opcional -> sanic_applynotes). Outputs: Outcome
    /// ("Applied"|"Rejected" — "Failed" es solo documentado: un fallo real SIEMPRE sale
    /// como InvalidPluginExecutionException, nunca como esta salida disfrazando un
    /// exito), Message.
    ///
    /// Identidad DOBLE (mismo patron que CreateTransactionCutPlugin/DeclarationMaterializer):
    ///   - <c>systemService</c> (CreateOrganizationService(null)): lee la cabecera/lineas/
    ///     catalogo (chequeo de seguridad de solo lectura) y escribe el MAESTRO (Client/
    ///     ClientBranch/ClientEmail/ClientAccount) y la bitacora — nadie humano tiene
    ///     privilegio de escritura ahi por diseno.
    ///   - <c>operatorService</c> (CreateOrganizationService(resolverId)): escribe la
    ///     cabecera y las lineas del RFC CON LA IDENTIDAD REAL del resolutor. Si esas
    ///     escrituras corrieran como sistema, GuardChangeRequestApprovalPlugin (Pre-op de
    ///     la cabecera) resolveria la membresia del grupo Approver contra el usuario
    ///     SYSTEM — nunca es miembro de nada — y bloquearia toda resolucion legitima.
    ///
    /// La tabla objetivo se resuelve DINAMICAMENTE desde sanic_targettable del RFC — sin
    /// el switch de 4 tablas hardcodeado que tenian Activaterow/Deactivaterow (mata L6).
    /// </summary>
    public sealed class ResolveChangeRequestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, Schema.ResolveChangeRequestApi.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                tracing.Trace("ResolveChangeRequest: mensaje {0} inesperado, se omite.", context.MessageName);
                return;
            }

            try
            {
                // 0. Inputs.
                if (!(context.InputParameters.TryGetValue(Schema.ResolveChangeRequestApi.InChangeRequestId, out var rawId)
                      && rawId is Guid changeRequestId && changeRequestId != Guid.Empty))
                {
                    throw new InvalidPluginExecutionException(
                        "El parametro 'ChangeRequestId' es obligatorio, de tipo Guid, y no puede ser vacio.");
                }

                if (!(context.InputParameters.TryGetValue(Schema.ResolveChangeRequestApi.InDecision, out var rawDecision)
                      && rawDecision is string decisionText && !string.IsNullOrWhiteSpace(decisionText)))
                {
                    throw new InvalidPluginExecutionException(
                        $"El parametro 'Decision' es obligatorio " +
                        $"('{Schema.ResolveChangeRequestApi.DecisionApprove}' o '{Schema.ResolveChangeRequestApi.DecisionReject}').");
                }

                var decision = ParseDecision(decisionText);

                var comment = context.InputParameters.TryGetValue(Schema.ResolveChangeRequestApi.InComment, out var rawComment)
                    ? rawComment as string
                    : null;

                var resolverId = context.InitiatingUserId;
                tracing.Trace("ResolveChangeRequest: 0. rfc={0} decision={1} resolutor={2}.",
                    changeRequestId, decision, resolverId);

                var systemService = serviceFactory.CreateOrganizationService(null);
                var operatorService = serviceFactory.CreateOrganizationService(resolverId);

                // 1. Cabecera COMO SISTEMA (chequeo de seguridad de solo lectura, independiente
                //    de los privilegios del resolutor).
                var header = systemService.Retrieve(
                    Schema.ChangeRequest.LogicalName, changeRequestId,
                    new ColumnSet(
                        Schema.ChangeRequest.ChangeStatus, Schema.ChangeRequest.TargetTable,
                        Schema.ChangeRequest.TargetRecordId, Schema.ChangeRequest.Type,
                        Schema.ChangeRequest.CreatedBy, Schema.ChangeRequest.RelatedClient));

                var currentStatus = header.GetAttributeValue<OptionSetValue>(Schema.ChangeRequest.ChangeStatus)?.Value
                    ?? throw new InvalidPluginExecutionException(
                        $"El RFC {changeRequestId:D} no tiene sanic_changestatus; no se puede resolver.");
                var targetTable = header.GetAttributeValue<string>(Schema.ChangeRequest.TargetTable);
                var targetRecordIdRaw = header.GetAttributeValue<string>(Schema.ChangeRequest.TargetRecordId);
                var type = header.GetAttributeValue<OptionSetValue>(Schema.ChangeRequest.Type)?.Value
                    ?? throw new InvalidPluginExecutionException(
                        $"El RFC {changeRequestId:D} no tiene sanic_type; no se puede resolver.");
                var creatorId = header.GetAttributeValue<EntityReference>(Schema.ChangeRequest.CreatedBy)?.Id ?? Guid.Empty;
                var relatedClient = header.GetAttributeValue<EntityReference>(Schema.ChangeRequest.RelatedClient);

                if (string.IsNullOrWhiteSpace(targetTable))
                {
                    throw new InvalidPluginExecutionException(
                        $"El RFC {changeRequestId:D} no tiene sanic_targettable; no se puede resolver.");
                }

                tracing.Trace(
                    "ResolveChangeRequest: 1. rfc={0} estado={1} tabla={2} tipo={3} creador={4}.",
                    changeRequestId, currentStatus, targetTable, type, creatorId);

                // 2. Membresia del grupo Approver (Fase 2) — MISMA resolucion que
                //    GuardChangeRequestApprovalPlugin (ChangeRequestApproverResolver).
                var isApprover = ChangeRequestApproverResolver.IsUserApproverOfTable(
                    serviceFactory, tracing, targetTable, resolverId);

                // 3. Decision de dominio: separacion de funciones (creador != resolutor,
                //    Submitted -> decision) + estado final de cabecera/linea.
                var outcome = ChangeRequestResolution.Evaluate(currentStatus, decision, creatorId, resolverId, isApprover);
                if (!outcome.IsAllowed)
                {
                    throw new InvalidPluginExecutionException(outcome.Reason);
                }

                tracing.Trace("ResolveChangeRequest: 3. resolucion permitida — headerStatus={0} lineStatus={1}.",
                    outcome.HeaderStatus, outcome.LineStatus);

                // 4. Lineas del RFC, COMO SISTEMA. Un RFC vacio en Update/Create no es un
                //    exito disfrazado (mata L2/L4).
                var lines = RetrieveLines(systemService, changeRequestId);
                tracing.Trace("ResolveChangeRequest: 4. rfc={0} con {1} lineas.", changeRequestId, lines.Count);

                if (decision == ChangeRequestDecision.Approve
                    && ChangeRequestResolution.RequiresLines(type) && lines.Count == 0)
                {
                    throw new InvalidPluginExecutionException(
                        $"El RFC {changeRequestId:D} no tiene lineas de cambio; no hay nada que aplicar.");
                }

                Guid? newMasterId = null;

                // 5. Materializacion sobre el MAESTRO, COMO SISTEMA — solo en Approve.
                if (decision == ChangeRequestDecision.Approve)
                {
                    switch (type)
                    {
                        case ChangeRequestResolution.TypeUpdate:
                            ApplyUpdate(systemService, tracing, changeRequestId, targetTable, targetRecordIdRaw, lines);
                            break;

                        case ChangeRequestResolution.TypeCreate:
                            newMasterId = ApplyCreate(systemService, tracing, targetTable, lines);
                            break;

                        case ChangeRequestResolution.TypeActivate:
                            ApplyRecordStatusChange(
                                systemService, tracing, changeRequestId, targetTable, targetRecordIdRaw,
                                Schema.MasterRecordStatus.Active);
                            break;

                        case ChangeRequestResolution.TypeDeactivate:
                            ApplyRecordStatusChange(
                                systemService, tracing, changeRequestId, targetTable, targetRecordIdRaw,
                                Schema.MasterRecordStatus.Inactive);
                            break;

                        default:
                            throw new InvalidPluginExecutionException(
                                $"El RFC {changeRequestId:D} tiene un sanic_type desconocido ({type}).");
                    }
                }

                // 6. Lineas al estado final. Identidad del RESOLUTOR: GuardChangeRequestLinePlugin
                //    no la bloquea porque esta escritura corre a depth > 1 (viene de otro plugin).
                foreach (var line in lines)
                {
                    operatorService.Update(new Entity(Schema.ChangeRequestLine.LogicalName, line.Id)
                    {
                        [Schema.ChangeRequestLine.LineStatus] = new OptionSetValue(outcome.LineStatus)
                    });
                }

                // 7. Bitacora, COMO SISTEMA — una fila por linea (o una fila de cabecera si el
                //    RFC no tiene lineas, caso Activate/Deactivate).
                var operationType = Schema.ChangeRequestAuditTrail.MapOperationType(type);
                var decisionLabel = decision == ChangeRequestDecision.Approve ? "Aplicado" : "Rechazado";
                WriteAuditTrail(systemService, changeRequestId, lines, relatedClient, operationType, decisionLabel);

                // 8. Cabecera al estado final, identidad del RESOLUTOR (para que
                //    GuardChangeRequestApprovalPlugin re-valide con la identidad real, no con
                //    el usuario SYSTEM).
                var headerUpdate = new Entity(Schema.ChangeRequest.LogicalName, changeRequestId)
                {
                    [Schema.ChangeRequest.ChangeStatus] = new OptionSetValue(outcome.HeaderStatus),
                    [Schema.ChangeRequest.SolvedBy] = new EntityReference(Schema.SystemUser.LogicalName, resolverId),
                    [Schema.ChangeRequest.SolvedOn] = DateTime.UtcNow,
                };

                if (!string.IsNullOrWhiteSpace(comment))
                {
                    headerUpdate[Schema.ChangeRequest.ApplyNotes] = comment;
                }

                if (decision == ChangeRequestDecision.Approve)
                {
                    headerUpdate[Schema.ChangeRequest.ApplyOn] = DateTime.UtcNow;

                    if (type == ChangeRequestResolution.TypeCreate && newMasterId.HasValue)
                    {
                        headerUpdate[Schema.ChangeRequest.TargetRecordId] = newMasterId.Value.ToString("D");

                        if (string.Equals(targetTable, Schema.Client.LogicalName, StringComparison.OrdinalIgnoreCase))
                        {
                            headerUpdate[Schema.ChangeRequest.RelatedClient] =
                                new EntityReference(Schema.Client.LogicalName, newMasterId.Value);
                        }
                    }
                }

                operatorService.Update(headerUpdate);

                var outcomeLabel = decision == ChangeRequestDecision.Approve
                    ? Schema.ResolveChangeRequestApi.OutcomeApplied
                    : Schema.ResolveChangeRequestApi.OutcomeRejected;

                context.OutputParameters[Schema.ResolveChangeRequestApi.OutOutcome] = outcomeLabel;
                context.OutputParameters[Schema.ResolveChangeRequestApi.OutMessage] =
                    $"RFC {changeRequestId:D} resuelto: {outcomeLabel} por {resolverId:D}.";

                tracing.Trace("ResolveChangeRequest: 8. rfc={0} resuelto — {1}.", changeRequestId, outcomeLabel);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("ResolveChangeRequest: error inesperado: {0}", ex);
                throw new InvalidPluginExecutionException($"No se pudo resolver el RFC: {ex.Message}", ex);
            }
        }

        private static ChangeRequestDecision ParseDecision(string decisionText)
        {
            if (string.Equals(decisionText, Schema.ResolveChangeRequestApi.DecisionApprove, StringComparison.OrdinalIgnoreCase))
            {
                return ChangeRequestDecision.Approve;
            }

            if (string.Equals(decisionText, Schema.ResolveChangeRequestApi.DecisionReject, StringComparison.OrdinalIgnoreCase))
            {
                return ChangeRequestDecision.Reject;
            }

            throw new InvalidPluginExecutionException(
                $"Decision '{decisionText}' invalida; use '{Schema.ResolveChangeRequestApi.DecisionApprove}' o " +
                $"'{Schema.ResolveChangeRequestApi.DecisionReject}'.");
        }

        private static IReadOnlyList<Entity> RetrieveLines(IOrganizationService system, Guid changeRequestId)
        {
            var query = new QueryExpression(Schema.ChangeRequestLine.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    Schema.ChangeRequestLine.FieldDefinitionLookup,
                    Schema.ChangeRequestLine.OldValue,
                    Schema.ChangeRequestLine.NewValue)
            };
            query.Criteria.AddCondition(Schema.ChangeRequestLine.ChangeRequestLookup, ConditionOperator.Equal, changeRequestId);

            return system.RetrieveMultiple(query).Entities;
        }

        // =================================================================
        // Materializacion por tipo (sin switch de 4 tablas: la tabla es sanic_targettable)
        // =================================================================

        private static void ApplyUpdate(
            IOrganizationService system, ITracingService tracing, Guid changeRequestId,
            string targetTable, string? targetRecordIdRaw, IReadOnlyList<Entity> lines)
        {
            if (!Guid.TryParse(targetRecordIdRaw, out var targetId) || targetId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    $"El RFC {changeRequestId:D} no tiene un sanic_targetrecordid valido; no se puede aplicar el Update.");
            }

            var entity = new Entity(targetTable, targetId);
            ApplyLineValues(system, tracing, targetTable, lines, entity);

            // UN SOLO Update con TODOS los campos convertidos: atomicidad real (mata L1 —
            // Applyclientdatachanges hacia un UpdateOnlyRecord POR CAMPO, sin rollback si
            // fallaba a mitad de camino).
            system.Update(entity);
            tracing.Trace("ResolveChangeRequest: 5. Update aplicado sobre {0} {1} ({2} campos).",
                targetTable, targetId, entity.Attributes.Count);
        }

        private static Guid ApplyCreate(
            IOrganizationService system, ITracingService tracing, string targetTable, IReadOnlyList<Entity> lines)
        {
            var entity = new Entity(targetTable);
            ApplyLineValues(system, tracing, targetTable, lines, entity);
            entity[Schema.MasterRecordStatus.FieldName] = new OptionSetValue(Schema.MasterRecordStatus.Active);

            var newId = system.Create(entity);
            tracing.Trace("ResolveChangeRequest: 5. Create aplicado sobre {0} -> {1} ({2} campos + recordstatus Active).",
                targetTable, newId, entity.Attributes.Count);
            return newId;
        }

        private static void ApplyRecordStatusChange(
            IOrganizationService system, ITracingService tracing, Guid changeRequestId,
            string targetTable, string? targetRecordIdRaw, int recordStatus)
        {
            if (!Guid.TryParse(targetRecordIdRaw, out var targetId) || targetId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    $"El RFC {changeRequestId:D} no tiene un sanic_targetrecordid valido; " +
                    $"no se puede cambiar sanic_recordstatus de {targetTable}.");
            }

            system.Update(new Entity(targetTable, targetId)
            {
                [Schema.MasterRecordStatus.FieldName] = new OptionSetValue(recordStatus)
            });
            tracing.Trace("ResolveChangeRequest: 5. sanic_recordstatus de {0} {1} -> {2}.",
                targetTable, targetId, recordStatus);
        }

        /// <summary>
        /// Convierte cada linea segun su FieldDefinition y las acumula en
        /// <paramref name="entity"/>. Fail-fast: la primera linea no convertible aborta
        /// TODO el RFC con el motivo concreto (nunca se aplica "lo que se pudo").
        /// </summary>
        private static void ApplyLineValues(
            IOrganizationService system, ITracingService tracing,
            string targetTable, IReadOnlyList<Entity> lines, Entity entity)
        {
            foreach (var line in lines)
            {
                var fieldDefRef = line.GetAttributeValue<EntityReference>(Schema.ChangeRequestLine.FieldDefinitionLookup);
                if (fieldDefRef == null)
                {
                    throw new InvalidPluginExecutionException(
                        $"La linea {line.Id:D} no tiene sanic_fielddefinition; no se puede aplicar.");
                }

                var fieldDef = system.Retrieve(
                    Schema.FieldDefinition.LogicalName, fieldDefRef.Id,
                    new ColumnSet(
                        Schema.FieldDefinition.Table, Schema.FieldDefinition.LogicalNameField,
                        Schema.FieldDefinition.DataType, Schema.FieldDefinition.OptionSetMap,
                        Schema.FieldDefinition.LookupTargetTable, Schema.FieldDefinition.IsAllowed));

                var fieldTable = fieldDef.GetAttributeValue<string>(Schema.FieldDefinition.Table);
                if (!string.Equals(fieldTable, targetTable, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException(
                        $"La linea {line.Id:D} referencia un campo de la tabla '{fieldTable}', pero el RFC apunta a '{targetTable}'.");
                }

                if (!fieldDef.GetAttributeValue<bool>(Schema.FieldDefinition.IsAllowed))
                {
                    throw new InvalidPluginExecutionException(
                        $"La linea {line.Id:D} referencia un campo no habilitado para edicion por RFC (sanic_isallowed=false).");
                }

                var logicalName = fieldDef.GetAttributeValue<string>(Schema.FieldDefinition.LogicalNameField);
                if (string.IsNullOrWhiteSpace(logicalName))
                {
                    throw new InvalidPluginExecutionException(
                        $"El campo del catalogo {fieldDefRef.Id:D} no tiene sanic_logicalname configurado.");
                }

                var dataType = fieldDef.GetAttributeValue<OptionSetValue>(Schema.FieldDefinition.DataType)?.Value
                    ?? throw new InvalidPluginExecutionException(
                        $"El campo del catalogo {fieldDefRef.Id:D} no tiene sanic_datatype configurado.");

                var optionSetMap = fieldDef.GetAttributeValue<string>(Schema.FieldDefinition.OptionSetMap);
                var lookupTargetTable = fieldDef.GetAttributeValue<string>(Schema.FieldDefinition.LookupTargetTable);
                var newValue = line.GetAttributeValue<string>(Schema.ChangeRequestLine.NewValue);

                var conversion = FieldValueConverter.Convert(dataType, newValue, optionSetMap, lookupTargetTable);
                if (!conversion.Success)
                {
                    throw new InvalidPluginExecutionException(
                        $"Linea {line.Id:D} (campo '{logicalName}'): {conversion.Error}");
                }

                entity[logicalName] = ToAttributeValue(conversion.Value!, dataType);
                tracing.Trace("ResolveChangeRequest: linea {0} campo '{1}' convertido ({2}).",
                    line.Id, logicalName, conversion.Value!.Kind);
            }
        }

        private static object ToAttributeValue(ConvertedValue value, int dataType)
        {
            switch (value.Kind)
            {
                case ConvertedValueKind.Text:
                    return value.Text ?? string.Empty;

                case ConvertedValueKind.Decimal:
                    // Money y Number comparten conversion decimal; solo Money se envuelve
                    // en el tipo SDK que Dataverse exige para columnas de dinero.
                    return dataType == FieldValueConverter.Money ? (object)new Money(value.Decimal) : value.Decimal;

                case ConvertedValueKind.Date:
                    return value.Date;

                case ConvertedValueKind.Lookup:
                    return new EntityReference(value.Lookup!.Value.LogicalName, value.Lookup.Value.Id);

                case ConvertedValueKind.OptionSet:
                    return new OptionSetValue(value.OptionSetCode);

                case ConvertedValueKind.Boolean:
                    return value.Boolean;

                default:
                    throw new InvalidPluginExecutionException($"Tipo de valor convertido desconocido: {value.Kind}.");
            }
        }

        // =================================================================
        // Bitacora
        // =================================================================

        private static void WriteAuditTrail(
            IOrganizationService system, Guid changeRequestId, IReadOnlyList<Entity> lines,
            EntityReference? relatedClient, int operationType, string decisionLabel)
        {
            if (lines.Count == 0)
            {
                // Activate/Deactivate: sin lineas, una fila de bitacora a nivel de cabecera.
                system.Create(BuildAuditEntity(
                    changeRequestId, null, relatedClient, operationType,
                    $"RFC {changeRequestId:D}: {decisionLabel}."));
                return;
            }

            foreach (var line in lines)
            {
                var oldValue = line.GetAttributeValue<string>(Schema.ChangeRequestLine.OldValue);
                var newValue = line.GetAttributeValue<string>(Schema.ChangeRequestLine.NewValue);
                var message = $"RFC {changeRequestId:D} linea {line.Id:D}: {decisionLabel} ('{oldValue}' -> '{newValue}').";
                system.Create(BuildAuditEntity(changeRequestId, line.Id, relatedClient, operationType, message));
            }
        }

        private static Entity BuildAuditEntity(
            Guid changeRequestId, Guid? lineId, EntityReference? relatedClient, int operationType, string message)
        {
            var audit = new Entity(Schema.ChangeRequestAuditTrail.LogicalName)
            {
                [Schema.ChangeRequestAuditTrail.Message] = message,
                [Schema.ChangeRequestAuditTrail.OperationType] = new OptionSetValue(operationType),
                [Schema.ChangeRequestAuditTrail.RelatedRfc] = new EntityReference(Schema.ChangeRequest.LogicalName, changeRequestId),
                // Esta fila SOLO se escribe cuando llegamos hasta aca sin excepcion: un
                // fallo aborta la transaccion entera y no queda ninguna fila "Fail" a
                // medio camino (a diferencia del viejo flujo, que podia morir sin dejar
                // rastro — S4 del analisis).
                [Schema.ChangeRequestAuditTrail.Result] = true,
            };

            if (lineId.HasValue)
            {
                audit[Schema.ChangeRequestAuditTrail.RelatedCrl] =
                    new EntityReference(Schema.ChangeRequestLine.LogicalName, lineId.Value);
            }

            if (relatedClient != null)
            {
                audit[Schema.ChangeRequestAuditTrail.RelatedClient] = relatedClient;
            }

            return audit;
        }
    }
}
