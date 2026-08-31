using CashHub.Validation.Security;

namespace CashHub.Plugins
{
    /// <summary>Nombres lógicos y valores de option sets del esquema CashHub.</summary>
    internal static class Schema
    {
        internal static class ReceivedEmail
        {
            public const string LogicalName = "sanic_chtblreceivedemails";
            public const string Id = "sanic_chtblreceivedemailsid";
            public const string SenderAddress = "sanic_sourceemailaddress";
            public const string Subject = "sanic_subject";
            public const string ValidationStatus = "sanic_validationstatus";
            public const string ReviewStatus = "sanic_emailreviewstatus";
            public const string ReviewMethod = "sanic_emailreviewmethod";
            public const string ErrorText = "sanic_errortext";
            public const string ClientLookup = "sanic_client";
            public const string TrackingName = "sanic_name";
            public const string ReplyStatus = "sanic_emailreplystatus";
            public const string ReplySubject = "sanic_replysubject";
            public const string ReplyBodyHtml = "sanic_replybodyhtml";
            public const string SenderClassification = "sanic_senderclassification";
            public const string TriageNotes = "sanic_triagenotes";
            public const string DeclaredCif = "sanic_declaredcif";

            // Triage del remitente. Solo Registrado habilita CONTAR DETALLES del cliente;
            // la puerta de respuesta es aparte (ver TriageDecision.MayReplyToSender).
            public const int SenderRegistrado = 159460000;
            public const int SenderClienteSinRegistrar = 159460001;
            public const int SenderSospechoso = 159460002;
            public const int SenderNoCorresponde = 159460003;

            // Baldes nuevos del rediseño de vínculos:
            //   RevocadoParaCliente  — el vínculo con ESTE cliente no autoriza (revocado o
            //                          pendiente de aprobación: por decisión de negocio van
            //                          al mismo balde, ante la duda se trabaja seguro)
            //   ContactoDeOtroCliente— contacto vivo del banco que erró de CIF
            //   Revocado             — le cortamos en todos lados y sigue insistiendo
            public const int SenderRevocadoParaCliente = 159460004;
            public const int SenderContactoDeOtroCliente = 159460005;
            public const int SenderRevocado = 159460006;

            public const int ReplyPendiente = 159460000;
            public const int ReplyRespondido = 159460001;
            public const int ReplyOmitido = 159460002;

            public const int ValidationPendiente = 159460000;
            public const int ValidationValido = 159460001;
            public const int ValidationInvalido = 159460002;

            public const int ReviewPendiente = 159460000;
            public const int ReviewAceptado = 159460001;
            public const int ReviewOmitido = 159460002;
            public const int ReviewManual = 159460003;
            public const int ReviewError = 159460004;
            public const int ReviewDenegado = 159460005;

            public const int MethodAuto = 159460001;
        }

        internal static class Attachment
        {
            public const string LogicalName = "sanic_chtblemailsattachments";
            public const string EmailLookup = "sanic_email";
            public const string FileName = "sanic_filename";
            public const string SizeBytes = "sanic_tamanoenbytes";
            public const string FileContent = "sanic_filecontent";
        }

        internal static class Rule
        {
            public const string LogicalName = "sanic_validationrules";
            public const string Id = "sanic_validationrulesid";
            public const string Key = "sanic_validationname";
            public const string Name = "sanic_name";
            public const string Order = "sanic_validationorder";
            public const string IsActive = "sanic_isactive";
            public const string Value = "sanic_validationvalue";
            public const string FailMessage = "sanic_validationfailmessage";
            public const string Dependency = "sanic_validationdependency";
        }

        internal static class Run
        {
            public const string LogicalName = "sanic_chtblvalidationruns";
            public const string Name = "sanic_name";
            public const string Start = "sanic_runstart";
            public const string End = "sanic_runend";
            public const string Result = "sanic_runresult";
            public const string SourceEntity = "sanic_sourceentity";
            public const string SourceRecordId = "sanic_sourcerecordid";

            /// <summary>
            /// Vinculo real al correo. SourceRecordId se conserva porque la tabla es
            /// generica, pero un GUID en texto no se puede delegar: sin este lookup la
            /// app se trae 500 corridas y filtra en el cliente, y a partir de la 501
            /// deja de encontrar las nuevas SIN dar error.
            /// </summary>
            public const string ReceivedEmailLookup = "sanic_receivedemail";
            public const string SourceType = "sanic_sourcetype";

            public const int ResultPass = 159460000;
            public const int ResultFail = 159460001;
            public const int SourceSharedMailbox = 159460000;
        }

        internal static class Result
        {
            public const string LogicalName = "sanic_chtblvalidationresults";
            public const string Name = "sanic_name";
            public const string RuleLookup = "sanic_validationrule";
            public const string RunLookup = "sanic_validationrun";
            public const string Outcome = "sanic_validationresult";
            public const string DurationMs = "sanic_durationms";
            public const string Sheet = "sanic_sheet";
            public const string CellRef = "sanic_cellref";
            public const string Detail = "sanic_detail";

            public const int OutcomePass = 159460000;
            public const int OutcomeFail = 159460001;

            /// <summary>
            /// Regla omitida por dependencia (la madre fallo). Antes los Skipped no
            /// se persistian y el tracker no podia mostrar el cuadro completo de las
            /// 28 reglas; el valor se agrego al option set junto con este codigo.
            /// </summary>
            public const int OutcomeOmitida = 159460002;
        }

        internal static class ClientEmail
        {
            public const string LogicalName = "sanic_chtblclientemail";
            public const string Address = "sanic_name";
        }

        internal static class CustomApi
        {
            public const string MessageName = "sanic_CHValidateReceivedEmail";
            public const string InReceivedEmailId = "ReceivedEmailId";
            public const string OutRunId = "RunId";
            public const string OutResult = "Result";
            public const string OutFailedFindings = "FailedFindings";
        }

        /// <summary>Solicitud de cambio de datos de cliente (RFC).</summary>
        internal static class ChangeRequest
        {
            public const string LogicalName = "sanic_chtblclientdatachangerequest";
            public const string Id = "sanic_chtblclientdatachangerequestid";
            public const string ChangeStatus = "sanic_changestatus";
            public const string TargetTable = "sanic_targettable";
            public const string CreatedBy = "createdby";

            /// <summary>Option set sanic_type (verificado contra el Entity.xml): Update/Deactivate/Create/Activate.</summary>
            public const string Type = "sanic_type";

            /// <summary>GUID en TEXTO del registro maestro objetivo (Create lo llena al aplicarse).</summary>
            public const string TargetRecordId = "sanic_targetrecordid";

            /// <summary>Lookup a sanic_chtblclient. Denormalizado en TODO RFC (aunque la tabla
            /// objetivo sea Branch/Email/Account) para trazabilidad; en Create de cliente,
            /// ResolveChangeRequestPlugin lo estampa con el cliente recien creado.</summary>
            public const string RelatedClient = "sanic_relatedclient";

            public const string SolvedBy = "sanic_solvedby";
            public const string SolvedOn = "sanic_solvedon";
            public const string ApplyNotes = "sanic_applynotes";
            public const string ApplyOn = "sanic_applyon";

            /// <summary>Pre-image registrada con sanic_changestatus, sanic_targettable y createdby (solo Update).</summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Linea de RFC: UN campo a cambiar (sanic_chtblclientdatachangerequestline).</summary>
        internal static class ChangeRequestLine
        {
            public const string LogicalName = "sanic_chtblclientdatachangerequestline";
            public const string Id = "sanic_chtblclientdatachangerequestlineid";
            public const string ChangeRequestLookup = "sanic_changerequestid";
            public const string FieldDefinitionLookup = "sanic_fielddefinition";
            public const string OldValue = "sanic_oldvalue";
            public const string NewValue = "sanic_newvalue";
            public const string LineStatus = "sanic_changestatus";
            public const string CreatedBy = "createdby";

            /// <summary>PreImage del guard (Update/Delete): sanic_changerequestid + sanic_fielddefinition.</summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Catalogo de campos editables por RFC (sanic_chtblclienttablelogicalfieldname).</summary>
        internal static class FieldDefinition
        {
            public const string LogicalName = "sanic_chtblclienttablelogicalfieldname";
            public const string Id = "sanic_chtblclienttablelogicalfieldnameid";
            public const string Table = "sanic_table";
            public const string LogicalNameField = "sanic_logicalname";
            public const string DataType = "sanic_datatype";
            public const string OptionSetMap = "sanic_optionsetmap";
            public const string LookupTargetTable = "sanic_lookuptargettable";
            public const string IsAllowed = "sanic_isallowed";
        }

        /// <summary>Bitacora de cambios de datos de cliente (sanic_chtblclientdatachangesaudittrail).</summary>
        internal static class ChangeRequestAuditTrail
        {
            public const string LogicalName = "sanic_chtblclientdatachangesaudittrail";
            public const string Message = "sanic_message";

            /// <summary>Typo REAL del esquema publicado (falta la 'r' de "Operation"). NO corregir.</summary>
            public const string OperationType = "sanic_opeationtype";

            public const string RelatedClient = "sanic_relatedclient";
            public const string RelatedCrl = "sanic_relatedcrl";
            public const string RelatedRfc = "sanic_relatedrfc";

            /// <summary>bit (Two Options): true=Success, false=Fail. NO es OptionSetValue.</summary>
            public const string Result = "sanic_result";

            // Option set sanic_opeationtype (verificado contra el Entity.xml): OJO, el
            // ORDEN NO coincide con ChangeRequest.Type (alli Update=0/Deactivate=1/
            // Create=2/Activate=3) salvo en Activate. ResolveChangeRequestPlugin mapea
            // explicitamente (MapOperationType) — castear el valor crudo del RFC
            // escribiria la operacion equivocada en la bitacora.
            public const int OperationCreate = 159460000;
            public const int OperationUpdate = 159460001;
            public const int OperationDeactivate = 159460002;
            public const int OperationActivate = 159460003;

            /// <summary>
            /// Traduce el sanic_type crudo del RFC (ChangeRequestResolution.Type*) al valor
            /// de sanic_opeationtype (Operation*) de la bitacora. Los ordenes NO coinciden
            /// (ver comentario arriba) — castear el valor crudo escribiria la operacion
            /// equivocada.
            /// </summary>
            public static int MapOperationType(int type)
            {
                switch (type)
                {
                    case ChangeRequestResolution.TypeCreate:
                        return OperationCreate;
                    case ChangeRequestResolution.TypeUpdate:
                        return OperationUpdate;
                    case ChangeRequestResolution.TypeDeactivate:
                        return OperationDeactivate;
                    case ChangeRequestResolution.TypeActivate:
                        return OperationActivate;
                    default:
                        throw new System.ArgumentOutOfRangeException(
                            nameof(type), type, "sanic_type desconocido; no se puede mapear a sanic_opeationtype.");
                }
            }
        }

        /// <summary>
        /// sanic_recordstatus: columna GEMELA (mismo nombre, mismo option set global
        /// sanic_chchrecordstatus) en las 4 tablas maestras de cliente (Client/
        /// ClientBranch/ClientEmail/ClientAccount). Vive aca — no en cada tabla — porque
        /// ResolveChangeRequestPlugin resuelve la tabla objetivo DINAMICAMENTE desde el
        /// RFC (sanic_targettable), sin switch de 4 tablas hardcodeado (mata L6).
        /// </summary>
        internal static class MasterRecordStatus
        {
            public const string FieldName = "sanic_recordstatus";

            public const int Active = 159460000;
            public const int Inactive = 159460001;
            public const int PendingApproval = 159460002;
            public const int PendingDeactivation = 159460003;
        }

        /// <summary>Nombre logico de la tabla Client — el UNICO caso donde ChangeRequest.RelatedClient se re-estampa (Create).</summary>
        internal static class Client
        {
            public const string LogicalName = "sanic_chtblclient";

            /// <summary>
            /// Identificacion tributaria: RUC / NIT / cedula juridica segun el pais.
            /// Se llama TaxId y no LegalIdentifier a proposito: "Legal Entity Identifier"
            /// (LEI) es un estandar ISO 17442 real y su largo es exactamente 20 caracteres
            /// — el mismo de esta columna —, asi que ese nombre se leeria como otra cosa.
            /// La crea scripts/create-client-taxid-column.sh (nvarchar 20 + alternate key).
            /// </summary>
            public const string TaxId = "sanic_taxid";

            /// <summary>
            /// Las TRES columnas por las que un cliente puede identificarse en la
            /// plantilla (Depositos!J2). La busqueda es un OR sobre las tres: el cliente
            /// declara con el identificador que tiene a mano y no tiene por que adivinar
            /// cual guardamos nosotros.
            /// </summary>
            public static readonly string[] IdentifierColumns =
                { "sanic_cifbco", "sanic_cifcom", TaxId };
        }

        /// <summary>Custom API sanic_CHResolveChangeRequest: reemplaza los 4 flujos materializadores de RFC.</summary>
        internal static class ResolveChangeRequestApi
        {
            public const string MessageName = "sanic_CHResolveChangeRequest";

            public const string InChangeRequestId = "ChangeRequestId";
            public const string InDecision = "Decision";
            public const string InComment = "Comment";

            public const string DecisionApprove = "Approve";
            public const string DecisionReject = "Reject";

            /// <summary>
            /// "Failed" es un valor DOCUMENTADO del contrato, no una salida que este plugin
            /// pueda devolver: cualquier fallo de negocio o tecnico lanza
            /// InvalidPluginExecutionException y aborta TODA la transaccion (mata L4 — un
            /// "Outcome: Failed" con un Update parcial ya aplicado seria exactamente el
            /// mismo exito-disfrazado que el flujo viejo). Se deja documentado para que el
            /// consumidor (la app) sepa que un fault de la Custom API equivale a "Failed".
            /// </summary>
            public const string OutOutcome = "Outcome";
            public const string OutMessage = "Message";

            public const string OutcomeApplied = "Applied";
            public const string OutcomeRejected = "Rejected";
        }

        /// <summary>
        /// Contrato de sanic_CHResolveApprovalRequest: resuelve una solicitud de
        /// aprobacion (Approve/Reject) COMO UNIDAD TRANSACCIONAL.
        ///
        /// POR QUE EXISTE. La app hacia dos escrituras sueltas: marcar la solicitud y
        /// despues aplicar el efecto. Sin transaccion, si la segunda fallaba quedaba
        /// la solicitud resuelta y el efecto sin ejecutar — y peor, la app se plantaba
        /// en "esta solicitud ya fue resuelta" y el reintento era imposible. Invertir
        /// el orden no servia: la validacion de membresia al grupo aprobador vive en
        /// el Update de la solicitud, asi que aplicar el efecto primero dejaba que un
        /// no-aprobador destruyera datos antes de chocar contra el bloqueo.
        ///
        /// Aca se autoriza PRIMERO y se escribe despues, todo en la misma ejecucion
        /// del plugin: si algo falla, revierte entero. Un bloqueo viaja como DATO
        /// (Outcome/Message, HTTP 200) porque Power Fx descarta el mensaje de los
        /// faults de plugin.
        /// </summary>
        internal static class ResolveApprovalApi
        {
            public const string MessageName = "sanic_CHResolveApprovalRequest";

            public const string InApprovalRequestId = "ApprovalRequestId";
            public const string InDecision = "Decision";
            public const string InReason = "Reason";

            public const string DecisionApprove = "Approve";
            public const string DecisionReject = "Reject";

            public const string OutOutcome = "Outcome";
            public const string OutMessage = "Message";

            public const string OutcomeApproved = "Approved";
            public const string OutcomeRejected = "Rejected";
            public const string OutcomeBlocked = "Blocked";
        }

        /// <summary>
        /// <c>sanic_CHReturnBagForReassignment</c>: saca una tula de la mesa y la deja
        /// libre, borrando todo lo que el cajero alcanzo a cargar.
        ///
        /// Va como Custom API y no como una tanda de Patch desde Power Fx porque son
        /// CUATRO borrados y CUATRO desvinculaciones sobre cinco tablas: si la quinta
        /// falla, la tula queda a medio limpiar y NADIE se entera. Aca, o pasa todo o
        /// no pasa nada.
        ///
        /// Sin Boolean en la respuesta: el conector de Environment no expone Custom APIs
        /// con salidas booleanas. Se devuelve texto, igual que ResolveApprovalApi.
        /// </summary>
        internal static class ReturnBagApi
        {
            public const string MessageName = "sanic_CHReturnBagForReassignment";

            public const string InBagId = "BagId";
            public const string InReason = "Reason";

            public const string OutOutcome = "Outcome";
            public const string OutMessage = "Message";

            public const string OutcomeReturned = "Returned";
            public const string OutcomeBlocked = "Blocked";
        }

        /// <summary>
        /// <c>sanic_CHForceCloseRecountingSession</c>: el supervisor cierra a la fuerza
        /// una sesion de recuento y libera las tulas que el cajero tenia en la mano.
        ///
        /// Va como Custom API por lo mismo que la devolucion: cierra la sesion Y vacia
        /// N tulas. Media operacion dejaria la sesion cerrada con tulas enllavadas
        /// colgando de ella, que es peor que no haber hecho nada.
        /// </summary>
        internal static class ForceCloseSessionApi
        {
            public const string MessageName = "sanic_CHForceCloseRecountingSession";

            public const string InSessionId = "SessionId";
            public const string InReason = "Reason";

            public const string OutOutcome = "Outcome";
            public const string OutMessage = "Message";

            public const string OutcomeClosed = "Closed";
            public const string OutcomeBlocked = "Blocked";
        }

        /// <summary>Tabla de configuracion del backend (CH-Tbl-Parameter): clave -> valor de texto.</summary>
        internal static class Parameter
        {
            public const string LogicalName = "sanic_chtblparameter";
            public const string Key = "sanic_name";
            public const string Value = "sanic_value";
            public const string IsActive = "sanic_isactive";

            /// <summary>
            /// Tope de tamaño DESCOMPRIMIDO del adjunto, en MB. Solo puede BAJAR el limite:
            /// el techo absoluto lo pone XlsxParser.MaxUncompressedBytesCeiling (100 MB).
            /// Fila ausente/inactiva/no numerica => el parser usa su default.
            /// </summary>
            public const string MaxUncompressedMbKey = "Validation.MaxUncompressedMB";

            /// <summary>
            /// Tope de transacciones que un corte asigna DE UNA VEZ
            /// (Digitization.MaxTransactionsPerCut).
            ///
            /// Existe porque un corte grande revienta el limite de la base:
            /// "Too many concurrent requests ... The request limit for the database is
            /// 400 and has been reached". Con 2.851 pendientes el corte moria entero y
            /// no cortaba NADA: un corte parcial que funciona vale mas que uno completo
            /// que nunca termina.
            ///
            /// Vive en la tabla de parametros y no en el codigo para poder moverlo sin
            /// redeploy cuando cambie la capacidad del entorno.
            /// Fila ausente/inactiva/no numerica => el plugin usa su piso (500).
            /// </summary>
            public const string MaxTransactionsPerCutKey = "Digitization.MaxTransactionsPerCut";

            /// <summary>Identidad bancaria fija del CSV al core, en JSON plano.</summary>
            public const string CoreIdentityKey = "Export.Core.Identidad";

            /// <summary>
            /// Lista BLANCA de caracteres permitidos en los campos de texto libre que van
            /// al CSV del core (Export.Core.AllowedCharacters).
            ///
            /// El CSV va separado por comas y SIN entrecomillar nada. Un depositante
            /// "PEREZ, JUAN" parte la fila y corre TODAS las columnas siguientes: el core
            /// lee la moneda donde va el deposito y postea mal SIN fallar. Un archivo que
            /// falla se ve; uno que postea mal no se ve hasta que alguien cuadra.
            ///
            /// Una regla, dos defensas: aca se SANEA lo que ya esta guardado, y la
            /// validacion de entrada usara la misma lista para REBOTAR en origen.
            ///
            /// Ausente o vacia => no pasa nada (default-deny). Si fuera al reves, borrar
            /// el parametro por error apagaria la defensa justo cuando mas hace falta.
            /// </summary>
            public const string AllowedCharactersKey = "Export.Core.AllowedCharacters";
        }

        /// <summary>Permiso de cambio de datos de cliente: mapea tabla objetivo -> grupo AAD por tipo.</summary>
        internal static class ChangePermission
        {
            public const string LogicalName = "sanic_chtblclientdatachangepermission";
            public const string TargetTable = "sanic_name";
            public const string Type = "sanic_type";
            public const string AadGroupId = "sanic_aadgroupid";

            public const int TypeContributor = 159460000;
            public const int TypeApprover = 159460001;
        }

        /// <summary>Catalogo de apps del hub (CH-App-Hub). El hub lanza AppUrl tal cual.</summary>
        internal static class AppCatalog
        {
            public const string LogicalName = "sanic_chtblappcatalog";
            public const string Id = "sanic_chtblappcatalogid";
            public const string AppUrl = "sanic_appurl";
        }

        /// <summary>Acceso por grupo AAD a una app del hub: una fila = (app, grupo).</summary>
        internal static class AppCatalogAccess
        {
            public const string LogicalName = "sanic_chtblappcatalogaccess";
            public const string Id = "sanic_chtblappcatalogaccessid";
            public const string AppCatalogLookup = "sanic_appcatalog";
            public const string AadGroupId = "sanic_aadgroupid";
        }

        /// <summary>Lote de recepcion (CH-Tbl-ReceptionBatches).</summary>
        internal static class ReceptionBatch
        {
            public const string LogicalName = "sanic_chtblreceptionbatch";
            public const string Id = "sanic_chtblreceptionbatchid";
            public const string ReceptionStatus = "sanic_receptionstatus";

            /// <summary>
            /// Totales DECLARADOS del lote, materializados por
            /// ReceptionBatchTotalsPlugin sumando las planillas (aggregate
            /// server-side). Existen para que el tracker muestre montos del lote
            /// sin sumar client-side (cap de filas). "No editar a mano."
            /// </summary>
            public const string DecNioCash = "sanic_decniocash";
            public const string DecNioCheck = "sanic_decniocheck";
            public const string DecNioTotal = "sanic_decniototal";
            public const string DecUsdCash = "sanic_decusdcash";
            public const string DecUsdCheck = "sanic_decusdcheck";
            public const string DecUsdTotal = "sanic_decusdtotal";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Planilla de recepcion (CH-Tbl-Receptions).</summary>
        internal static class Reception
        {
            public const string LogicalName = "sanic_chtblreceptions";
            public const string Id = "sanic_chtblreceptionsid";
            public const string BatchLookup = "sanic_receptionbatch";
            public const string ReceiptId = "sanic_receiptid";
            public const string Name = "sanic_name";

            /// <summary>
            /// Espejo entero del sanic_receptionstatus del lote, mantenido por
            /// ReceptionBatchStatusMirrorPlugin. Existe para que las apps filtren
            /// planillas por estado de lote con delegacion (un Filter sobre columna
            /// de la entidad relacionada no delega y miente a partir de 500 filas).
            /// </summary>
            public const string BatchStatusMirror = "sanic_batchstatus";

            /// <summary>Montos declarados de la planilla (vienen del papel).</summary>
            public const string NioCash = "sanic_niocash";
            public const string NioCheck = "sanic_niocheck";
            public const string UsdCash = "sanic_usdcash";
            public const string UsdCheck = "sanic_usdcheck";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Tula (CH-Tbl-Bags).</summary>
        internal static class Bag
        {
            public const string LogicalName = "sanic_chtblbags";
            public const string Id = "sanic_chtblbagsid";
            public const string BagStatus = "sanic_bagstatus";
            public const string BagId = "sanic_bagid";
            public const string Name = "sanic_name";
            public const string ReceptionLookup = "sanic_receptionid";
            public const string DeclaredBagLookup = "sanic_bagdeclarationid";

            /// <summary>
            /// Sesion de recuento a la que pertenece la tula. Columna de NEGOCIO (la
            /// escribe la app Recounting), no un espejo: es el ORIGEN del espejo B-9
            /// sanic_recountingsession del cheque.
            /// </summary>
            public const string RecountingSessionLookup = "sanic_recountingsession";
            /// <summary>Lookup al lote de recepcion. Define el alcance del dup-check de numero de tula.</summary>
            public const string ReceptionBatchLookup = "sanic_receptionbatchid";

            /// <summary>Totales recontados (los estampa la app Recounting).</summary>
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";

            /// <summary>
            /// Conciliacion materializada por BagReconciliationMirrorPlugin: copia de
            /// los totales del DeclaredBag + bit de diferencia delegable para la
            /// grilla del tracker. "No editar a mano."
            /// </summary>
            public const string DeclTotalNio = "sanic_decltotalnio";
            public const string DeclTotalUsd = "sanic_decltotalusd";
            public const string DeclTotalEur = "sanic_decltotaleur";
            public const string HasDifference = "sanic_hasdifference";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Solicitud de aprobacion de esclusa/recuento (CH-Tbl-ApprovalRequests).</summary>
        internal static class ApprovalRequest
        {
            public const string LogicalName = "sanic_chtblapprovalrequest";
            public const string Id = "sanic_chtblapprovalrequestid";
            public const string ApprovalStatus = "sanic_approvalstatus";
            public const string RequestType = "sanic_requesttype";
            public const string CreatedBy = "createdby";

            /// <summary>Supervisor (o el plugin de auto-aprobacion) que resolvio. Lookup a systemuser.</summary>
            public const string SolvedBy = "sanic_solvedby";

            /// <summary>Fecha/hora de la resolucion.</summary>
            public const string ResolutionDatetime = "sanic_resolutiondatetime";
            /// <summary>Motivo escrito por el resolutor (nvarchar propio, NO el statuscode del sistema).</summary>
            public const string StatusReason = "sanic_statusreason";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Transaccion (CH-Tbl-Transactions).</summary>
        internal static class Transaction
        {
            public const string LogicalName = "sanic_chtbltransactions";
            public const string Id = "sanic_chtbltransactionsid";
            public const string TransactionBatch = "sanic_transactionbatch";
            public const string RelatedDepositRecounting = "sanic_relateddepositrecounting";

            /// <summary>
            /// Cajero que reconto la plata de este asiento. SE ESTAMPA AL CREARLO.
            ///
            /// No se deduce de createdby —esa es la identidad que ESCRIBE la fila, y la
            /// escribe un plugin del servidor: todos los asientos del recuento quedaban
            /// atribuidos a SYSTEM o al service principal—. Tampoco se deduce de la
            /// cadena deposito -> sesion -> cajero: el 42% de los asientos pendientes no
            /// tiene deposito asociado y la cadena se corta.
            ///
            /// Quien conto la plata es un HECHO del asiento, no algo que se reconstruye.
            /// </summary>
            public const string Cajero = "sanic_cajero";

            /// <summary>
            /// Supervisor que autorizo el asiento. Solo se llena cuando nace de una
            /// aprobacion —sobrante, faltante, tula recontada sin declaracion—: si el
            /// asiento existe porque alguien lo firmo, esa firma es parte del asiento.
            /// En un cierre normal queda vacio, y eso tambien es informacion.
            /// </summary>
            public const string SupervisorAutoriza = "sanic_supervisorautoriza";
            public const string AccountNumber = "sanic_accountnumber";
            public const string TransactionStatus = "sanic_transactionstatus";
            public const string ProcessingType = "sanic_transactionprocessingtype";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";
            public const string OtherCheckAmount = "sanic_otherlocalcheckamount";
            public const string CreatedOn = "createdon";
            public const string CreatedBy = "createdby";
            public const string CashAmount = "sanic_cashamount";
            public const string Currency = "sanic_currency";
            public const string TransactionType = "sanic_transactiontype";
            public const string TransactionNumber = "sanic_transactionnumber";

            /// <summary>
            /// Referencia del deposito DECLARADO enlazado (DeclaredDeposit.DepositReference).
            /// La copia el asiento al crearse; en blanco en la ruta "tula sin declaracion".
            /// </summary>
            public const string TransactionReference = "sanic_transactionreference";

            /// <summary>
            /// Numero de transaccion visible (texto, NO la clave primaria). En el CSV al
            /// core va en REFERENCIA y en DEPOSITID, y ademas es el respaldo de
            /// DEPOSITANTE cuando la referencia viene vacia o se sanea hasta quedar vacia.
            /// </summary>
            public const string TransactionIdText = "sanic_transactionid";

            /// <summary>
            /// Total CALCULADO (SourceType 3): ValidForCreateApi/UpdateApi = 0, la
            /// plataforma no lo deja escribir. Esta en la lista de columnas de dinero
            /// del guard igual, por si alguna vez se convierte en editable.
            /// </summary>
            public const string Total = "sanic_total";

            /// <summary>
            /// Columnas de DINERO de la transaccion. Ninguna se toca una vez que la
            /// transaccion esta Aplicada o su lote entro al Core (H-04).
            /// OJO: sanic_accountnumber tiene IsSecured=1 (field security).
            /// </summary>
            public static readonly string[] MoneyAttributes =
            {
                CashAmount,
                BacCheckAmount,
                OtherCheckAmount,
                AccountNumber,
                Total,
            };

            // Option set global sanic_chchcurrency (verificado contra OptionSets/sanic_chchcurrency.xml).
            public const int CurrencyNio = 159460000;
            public const int CurrencyUsd = 159460001;
            public const int CurrencyEur = 159460002;

            /// <summary>Espejo del estado del lote, mantenido por TransactionBatchStatusMirrorPlugin.</summary>
            public const string BatchStatusMirror = "sanic_batchstatus";

            /// <summary>
            /// Motivo por el que el Core rechazo la transaccion al sacarla del lote
            /// (memo). Lo escribe el Custom API sanic_CHRemoveFromCut; la app lo
            /// muestra en el historial. Creada por create-digitization-columns.sh.
            /// </summary>
            public const string MotivoRechazoCore = "sanic_motivorechazocore";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Cheque (CH-Tbl-Checks).</summary>
        internal static class Check
        {
            public const string LogicalName = "sanic_chtblchecks";
            public const string Id = "sanic_chtblchecksid";
            public const string CheckStatus = "sanic_checkstatus";

            /// <summary>Primer estado del ciclo del cheque, el que estampa el cierre del recuento.</summary>
            public const int CheckStatusRecontado = 159460000;

            /// <summary>
            /// Sellos server-side del handshake de recepcion, estampados por
            /// CheckStatusStampPlugin en Pre-operation al escribirse el estado.
            /// </summary>
            public const string SentToDigitizationOn = "sanic_senttodigitizationon";
            public const string ReceivedOn = "sanic_receivedon";
            public const string NotReceivedOn = "sanic_notreceivedon";

            /// <summary>
            /// Autores del handshake (lookup a systemuser), estampados por
            /// CheckStatusStampPlugin junto a los sellos de fecha: SentToDigitizationBy
            /// al enviar (el Supervisor), ReceivedBy al (no) recibir (el Digitador).
            /// scrCheques v2 los muestra como "Enviado por" / "Recibido por".
            /// Creadas por create-digitization-columns.sh. "No editar a mano."
            /// </summary>
            public const string SentToDigitizationBy = "sanic_senttodigitizationby";
            public const string ReceivedBy = "sanic_receivedby";

            public const string RelatedDepositLookup = "sanic_relatedrecounterdepositid";
            public const string RelatedDeclaredCheck = "sanic_relateddeclaredcheck";

            /// <summary>
            /// Tula del cheque, derivada del deposito de recuento por
            /// CheckMirrorStampPlugin. Existe para que el tracker traiga los cheques
            /// de una tula en UNA query delegable. "No editar a mano."
            /// </summary>
            public const string BagLookup = "sanic_bag";

            /// <summary>
            /// B-9 — sesion de recuento del cheque, derivada de
            /// sanic_bag.sanic_recountingsession por CheckMirrorStampPlugin (columna
            /// creada por create-supervisor-mirror-columns.sh). Existe para que los
            /// conteos de cheques de scrResumen sean CountRows DELEGADOS: la cadena
            /// Cheque -> Tula -> Sesion es dot-walk de dos saltos, NO delega, y
            /// materializar filas en coleccion topa en el limite del cliente
            /// (500/2000) sin error. "No editar a mano."
            /// </summary>
            public const string SessionLookup = "sanic_recountingsession";

            /// <summary>
            /// PreImage del paso de CASCADA (Update Post-op de la TULA) con
            /// sanic_recountingsession: sin ella no se sabe si la sesion CAMBIO y la
            /// cascada reescribiria N cheques por un update sin cambio real.
            /// </summary>
            public const string SessionCascadePreImageName = "PreImage";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Sesion de recuento (CH-Tbl-RecountingSessions).</summary>
        internal static class RecountingSession
        {
            public const string LogicalName = "sanic_chtblrecountingsession";
            public const string Id = "sanic_chtblrecountingsessionid";

            /// <summary>Cajero dueno de la sesion (lookup a systemuser, requerido).</summary>
            public const string Cashier = "sanic_cashier";

            /// <summary>Mesa de conteo elegida (lookup a CH-Tbl-CountingDesks, requerido).</summary>
            public const string CountingDesk = "sanic_countingdesk";

            /// <summary>
            /// Confirmacion de la sesion. 🚫 La app v2 NUNCA la escribe: la estampa
            /// RecountingSessionAutoApprovePlugin al auto-aprobar la mesa propia
            /// (00 §2.2 punto 1). Mata S-1.
            /// </summary>
            public const string Confirmed = "sanic_confirmed";

            /// <summary>Quien confirmo la sesion (lookup a systemuser).</summary>
            public const string ConfirmedBy = "sanic_confirmedby";

            public const string SessionForciblyClosed = "sanic_sessionforciblyclosed";

            /// <summary>Quien cerro la sesion a la fuerza (lookup a systemuser).</summary>
            public const string ForcedClosureBy = "sanic_forcedclosureby";

            /// <summary>
            /// Motivo del cierre forzado.
            /// 🧨 EL LOGICAL NAME TIENE UNA ERRATA EN EL ENTORNO: dice "flosure", no
            ///    "closure". Verificado contra la metadata. NO lo "corrijas" aca: el
            ///    codigo dejaria de encontrar la columna.
            /// </summary>
            public const string ReasonForForcedClosure = "sanic_reasonforforcedflosure";

            /// <summary>PreImage del paso Delete del guard, con sanic_cashier + sanic_confirmed.</summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Mesa de conteo (CH-Tbl-CountingDesks) — solo lectura para las apps.</summary>
        internal static class CountingDesk
        {
            public const string LogicalName = "sanic_chtblcountingdesk";

            /// <summary>Nombre de la mesa (CH-Tbl-CountingDesk). Da la mesa del cajero en el rail (B4).</summary>
            public const string Name = "sanic_name";

            /// <summary>Cajero asignado a la mesa (lookup a systemuser). Es el insumo del auto-approve.</summary>
            public const string Cajero = "sanic_cajero";
        }

        /// <summary>Detalle de arqueo por denominacion (CH-Tbl-CountedMoneyDetails).</summary>
        internal static class CountedMoneyDetail
        {
            public const string LogicalName = "sanic_chtblcountedmoneydetails";
            public const string Id = "sanic_chtblcountedmoneydetailsid";

            /// <summary>Deposito de recuento padre (lookup, requerido). Es por donde el gate llega a la tula.</summary>
            public const string DepositLookup = "sanic_relateddepositrecountingid";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Detalle de sesion de recuento — una tula dentro de la sesion
        /// (CH-Tbl-RecountingSessionDetails).</summary>
        internal static class RecountingSessionDetail
        {
            public const string LogicalName = "sanic_chtblrecountingsessiondetail";
            public const string Id = "sanic_chtblrecountingsessiondetailid";
            public const string SessionLookup = "sanic_recountingsessionid";
            public const string BagLookup = "sanic_bagid";
            public const string SupervisorLookup = "sanic_supervisorid";

            /// <summary>Estado del detalle: 159460000 En progreso, 159460001 Cerrado, 159460002 Cerrado con diferencias.</summary>
            public const string Status = "sanic_recountingsessiondetailstatus";

            public const int StatusEnProgreso = 159460000;
            public const int StatusCerrado = 159460001;
            public const int StatusCerradoConDiferencias = 159460002;
        }

        /// <summary>Lectura de la contadora (CH-Tbl-XMLReadings).</summary>
        internal static class XmlReading
        {
            public const string LogicalName = "sanic_chtblxmlreading";
            public const string Id = "sanic_chtblxmlreadingid";

            /// <summary>Sesion-detalle (tula) a la que pertenece la lectura. Origen de E-4.</summary>
            public const string SessionDetailLookup = "sanic_recountingsessiondetailid";

            /// <summary>
            /// Bit de lectura vigente. Origen de E-4: cuando llega una lectura nueva,
            /// la vieja se apaga DESPUES de que sus detalles ya existen — por eso el
            /// espejo necesita cascada y no alcanza con estampar en el Create del hijo.
            /// </summary>
            public const string IsActive = "sanic_isactive";

            /// <summary>
            /// PreImage del paso de CASCADA (Update Post-op de la LECTURA) con
            /// sanic_recountingsessiondetailid + sanic_isactive: sin ella no se sabe si
            /// los valores CAMBIARON y la cascada reescribiria N detalles por un update
            /// sin cambio real. Ademas aporta el valor EFECTIVO de la columna que no
            /// vino en el Target (las dos se propagan juntas).
            /// </summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Detalle de la lectura de la contadora, por denominacion
        /// (CH-Tbl-XMLReadingDetails).</summary>
        internal static class XmlReadingDetail
        {
            public const string LogicalName = "sanic_chtblxmlreadingdetail";
            public const string Id = "sanic_chtblxmlreadingdetailid";
            public const string ReadingLookup = "sanic_xmlreading";

            /// <summary>
            /// E-4 (lookup): sesion-detalle de la lectura del detalle, derivada por
            /// XmlReadingDetailMirrorPlugin. Existe para que Q17 ("Leer contadora") sea
            /// un Filter DELEGABLE de UN salto: la v1 escribia
            /// Filter(details, XMLReading.Id in Filter(readings, ...).Id) — un "in" con
            /// subconsulta que JAMAS delega (D-1). "No editar a mano."
            /// </summary>
            public const string SessionDetailMirror = "sanic_recountingsessiondetailmirror";

            /// <summary>
            /// E-4 (bit): espejo de XMLReading.IsActive. Es la SEGUNDA condicion de Q17;
            /// sin ella habria que volver a CH-Tbl-XMLReadings a preguntar IsActive y
            /// vuelve el segundo salto que E-4 vino a matar (ledger L-3).
            /// "No editar a mano."
            /// </summary>
            public const string ActiveMirror = "sanic_xmlreadingactiva";
        }

        /// <summary>Bolsa declarada (CH-Tbl-DeclaredBags).</summary>
        internal static class DeclaredBag
        {
            public const string LogicalName = "sanic_chtbldeclaredbags";
            public const string Id = "sanic_chtbldeclaredbagsid";
            /// <summary>Lookup a la tula FISICA. Ojo: en CH-Tbl-Bags "sanic_bagid" es texto, aca es lookup.</summary>
            public const string BagLookup = "sanic_bagid";
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";
        }

        /// <summary>Cheque declarado (CH-Tbl-DeclaredChecks).</summary>
        internal static class DeclaredCheck
        {
            public const string LogicalName = "sanic_chtbldeclaredchecks";
            public const string Id = "sanic_chtbldeclaredchecksid";

            /// <summary>
            /// Espejo del cheque fisico que lo referencia (la relacion real vive en
            /// sanic_chtblchecks.sanic_relateddeclaredcheck), estampado por
            /// CheckMirrorStampPlugin para que el tracker navegue del declarado a su
            /// recuento. "No editar a mano."
            /// </summary>
            public const string MatchedCheck = "sanic_matchedcheck";

            /// <summary>Deposito declarado padre. Origen del espejo E-3.</summary>
            public const string DeclaredDepositLookup = "sanic_declareddepositid";

            /// <summary>
            /// E-3: bolsa declarada del cheque declarado, derivada de
            /// sanic_declareddepositid.sanic_declaredbag por
            /// DeclaredCheckBagMirrorPlugin. Existe para que la apertura de tula traiga
            /// TODOS los cheques declarados de la bolsa en UNA query delegable en vez
            /// de un Filter remoto por deposito declarado (~82 round-trips medidos).
            /// "No editar a mano."
            /// </summary>
            public const string DeclaredBagMirror = "sanic_declaredbagmirror";
        }

        /// <summary>Lote de transacciones (CH-Tbl-TransactionBatches).</summary>
        internal static class TransactionBatch
        {
            public const string LogicalName = "sanic_transactionbatches";
            public const string Id = "sanic_transactionbatchesid";
            public const string Name = "sanic_name";
            public const string BatchStatus = "sanic_batchstatus";
            public const string Comentarios = "sanic_comentarios";

            /// <summary>
            /// Totales materializados por moneda (decimal 2), recomputados por
            /// BatchTotalsCalculator desde las transacciones del lote. Existen para
            /// que las apps muestren totales sin sumar client-side (cap de filas).
            /// </summary>
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";

            /// <summary>
            /// Cajero dueño de las transacciones del corte (lookup a systemuser),
            /// estampado por CreateTransactionCutPlugin junto con GeneradoPor.
            /// </summary>
            public const string Cajero = "sanic_cajero";
            public const string GeneradoPor = "sanic_generadopor";

            /// <summary>
            /// Columna de tipo Archivo (File) con el CSV que va al core, subido por
            /// CH-Flow-DownloadData. Es columna File y NO una nota a proposito:
            /// annotations/documentbody/$value devuelve el base64 CRUDO como text/plain
            /// (archivo inservible), mientras que la columna File devuelve los bytes
            /// decodificados. Ademas es el artefacto de auditoria: queda archivado
            /// exactamente lo que se mando al core.
            /// </summary>
            public const string CsvCore = "sanic_csvcore";

            /// <summary>
            /// Sellos server-side de las transiciones del lote (quien y cuando),
            /// estampados por BatchTransitionStampPlugin en Pre-operation. La estampa
            /// IngresadoPor es ademas el insumo de la regla de segregacion de funciones
            /// (quien marca Aplicado no puede ser quien marco Ingresado).
            /// </summary>
            public const string IngresadoPor = "sanic_ingresadopor";
            public const string IngresadoOn = "sanic_ingresadoon";
            public const string AplicadoPor = "sanic_aplicadopor";
            public const string AplicadoOn = "sanic_aplicadoon";

            /// <summary>
            /// La devolucion del IBS: por que volvio, quien la registro y cuando.
            ///
            /// El motivo es OBLIGATORIO al marcar el lote Devuelto (lo exige
            /// TransactionGuard.EvaluateBatchUpdate). Quien tenga que corregir el lote
            /// necesita saber QUE rechazo el Core: una devolucion sin motivo dice "esta
            /// mal" y nada mas, y manda a preguntar al IBS.
            /// </summary>
            public const string MotivoDevolucion = "sanic_motivodevolucion";
            public const string DevueltoPor = "sanic_devueltopor";
            public const string DevueltoOn = "sanic_devueltoon";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Deposito de recuento (CH-Tbl-DepositsRecounting).</summary>
        internal static class DepositRecounting
        {
            public const string LogicalName = "sanic_chtbldepositsrecounting";
            public const string Id = "sanic_chtbldepositsrecountingid";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";

            /// <summary>
            /// OJO: el logical name real tiene un typo de origen ("loca" sin la 'l'
            /// de "local"). NO corregirlo: asi esta publicado en el ambiente.
            /// </summary>
            public const string OtherCheckAmount = "sanic_otherlocacheckamount";

            public const string CashAmount = "sanic_cashamount";
            public const string Currency = "sanic_currency";
            public const string AccountNumber = "sanic_accountnumber";
            public const string TransactionNumber = "sanic_transactionnumber";

            /// <summary>
            /// Lado recontado -> declarado de la relacion. El lado espejo vive en
            /// DeclaredDeposit.RelatedRecountedDeposit y las apps usan LOS DOS: el
            /// guard resuelve por este y, si viene vacio, por el otro.
            /// </summary>
            public const string RelatedDeclaredDeposit = "sanic_relateddeclareddeposit";

            /// <summary>Tula del deposito de recuento. Origen del espejo E-1.</summary>
            public const string BagLookup = "sanic_bagid";

            /// <summary>
            /// E-1: sesion de recuento del deposito, derivada de
            /// sanic_bagid.sanic_recountingsession por DepositSessionMirrorPlugin.
            /// Existe para que los 12 Sum de dinero de scrTulas sean DELEGADOS: la
            /// cadena Deposito -> Tula -> Sesion es dot-walk de DOS saltos, NO delega,
            /// y Power Fx trunca en el tope del cliente (500/2000) SIN error — el rail
            /// de KPIs mostraba cifras PARCIALES de plata (D-2). "No editar a mano."
            /// </summary>
            public const string SessionMirror = "sanic_recountingsessionmirror";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Deposito declarado por el cliente (CH-Tbl-DeclaredDeposits).</summary>
        internal static class DeclaredDeposit
        {
            public const string LogicalName = "sanic_chtbldeclareddeposits";
            public const string Id = "sanic_chtbldeclareddepositsid";
            public const string AccountNumber = "sanic_accountnumber";
            public const string CashAmount = "sanic_cashamount";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";

            /// <summary>Aca SI lleva la 'l' — a diferencia de DepositsRecounting.</summary>
            public const string OtherCheckAmount = "sanic_otherlocalcheckamount";

            public const string Currency = "sanic_currency";
            public const string TransactionNumber = "sanic_transactionno";

            /// <summary>Referencia del deposito declarado (texto 31, field-level security).</summary>
            public const string DepositReference = "sanic_depositreference";

            /// <summary>Lado declarado -> recontado de la relacion (vacio = nadie lo reconto).</summary>
            public const string RelatedRecountedDeposit = "sanic_relatedrecounteddeposit";

            /// <summary>
            /// Bolsa declarada a la que pertenece el deposito. La escribe
            /// DeclarationMaterializer en el Create; es el ORIGEN del espejo E-3
            /// sanic_declaredbagmirror del cheque declarado.
            /// </summary>
            public const string DeclaredBagLookup = "sanic_declaredbag";
        }

        /// <summary>Custom API de corte de transacciones a lote (CH-App-Digitization).</summary>
        /// <summary>
        /// Custom API que devuelve el CSV del lote como texto, para que la app lo baje
        /// SIN navegar a Dataverse.
        ///
        /// POR QUE EXISTE. La descarga vieja era Download() sobre .../sanic_csvcore/$value:
        /// una navegacion del navegador, en la que Power Fx NO puede poner un header
        /// Authorization. Lo unico que la autenticaba era la cookie del org, que los
        /// usuarios de negocio no tienen: viven en apps.powerapps.com. Resultado: HTTP 401
        /// y una pestaña en blanco. Y MUDA — el Download() esta fuera del alcance de
        /// cualquier IfError, asi que la app no podia ni enterarse de que fallo.
        ///
        /// Este API viaja por el conector, que SI va autenticado, y el archivo lo arma el
        /// navegador con un Blob. Sin cookie, sin navegacion, sin estado de browser.
        ///
        /// NO genera el CSV: lo LEE de la columna File que ya subio CH-Flow-DownloadData.
        /// Generarlo de nuevo aca duplicaria la logica y abriria la puerta a que lo que se
        /// archiva y lo que se baja dejen de ser el mismo archivo.
        /// </summary>
        internal static class BatchCsvApi
        {
            public const string MessageName = "sanic_CHGetBatchCsv";

            /// <summary>Id del lote cuyo CSV se devuelve (REQUERIDO).</summary>
            public const string InBatchId = "BatchId";

            public const string OutCsv = "Csv";
            public const string OutFileName = "FileName";
            public const string OutSizeBytes = "SizeBytes";
        }

        /// <summary>
        /// Custom API del resumen por cuenta del lote, y su comparacion contra el que el
        /// aprobador copia del IBS Branch.
        ///
        /// POR QUE EXISTE. El aprobador no ve el detalle transaccion por transaccion: ve un
        /// "Resumen por Cuenta" (Cuenta | Moneda | Monto | Cant TRN) y tiene que compararlo
        /// contra el lote. El lote mas grande del entorno tiene 600 cuentas distintas:
        /// compararlas a ojo es lento y se cometen errores justo donde no se pueden cometer.
        ///
        /// Server-side y no con un GroupBy en Power Fx porque el cliente topa en 500 filas:
        /// un lote de 600 cuentas mostraria un resumen incompleto SIN avisar.
        /// </summary>
        /// <summary>
        /// Custom API del cubo de un cajero en un dia: una fila por moneda x tipo x via,
        /// con efectivo, cheque BAC, cheque otros, total y conteo.
        ///
        /// DEVUELVE UN CUBO, NO UN TOTAL. Son a lo sumo 3 x 4 x 2 = 24 filas, asi que la
        /// app arma con eso tanto el resumen grande —que sigue a los filtros— como los
        /// contadores de cada chip, sin volver a consultar cada vez que se toca uno.
        ///
        /// Agregado server-side porque un dia de un cajero real son 5.442 transacciones:
        /// sumarlas en el cliente toparia en 500 y daria un resumen incompleto SIN avisar.
        /// El DETALLE no pasa por aca — la galeria se ata al datasource con un Filter
        /// delegable y muestra todas.
        /// </summary>
        internal static class CashierDayApi
        {
            public const string MessageName = "sanic_CHCashierDay";

            public const string InCajeroId = "CajeroId";
            public const string InDesde = "Desde";
            public const string InHasta = "Hasta";

            public const string OutCube = "Cube";
            public const string OutTotalCount = "TotalCount";

            /// <summary>
            /// Dias en que el cajero tuvo movimientos, del mas reciente al mas viejo. La
            /// pantalla filtra por fecha de CREACION y arranca en hoy: sin esta lista, un
            /// cajero cuyas transacciones son de otro dia se ve vacio y sin una sola pista
            /// de donde esta su trabajo.
            /// </summary>
            public const string OutDays = "Days";
        }

        internal static class BatchSummaryApi
        {
            public const string MessageName = "sanic_CHBatchSummary";

            /// <summary>Lote del que se arma el resumen (REQUERIDO).</summary>
            public const string InBatchId = "BatchId";

            /// <summary>Texto pegado del Resumen por Cuenta del IBS. Opcional: sin el, solo se devuelve el resumen.</summary>
            public const string InPastedSummary = "PastedSummary";

            public const string OutSummary = "Summary";
            public const string OutComparison = "Comparison";
            public const string OutOutcome = "Outcome";
            public const string OutMessage = "Message";
        }

        internal static class CutApi
        {
            public const string MessageName = "sanic_CHCreateTransactionCut";

            /// <summary>systemuserid del cajero cuyas transacciones toma el corte (REQUERIDO).</summary>
            public const string InCajeroId = "CajeroId";

            public const string OutBatchId = "BatchId";
            public const string OutBatchName = "BatchName";
            public const string OutAssignedCount = "AssignedCount";
        }

        /// <summary>
        /// Custom API para sacar una transaccion de su lote y devolverla al circuito
        /// Manual (rechazos del Core / revision antes de subir). Reemplaza el
        /// Patch client-side + Refresh + LookUp de scrLotes.
        /// </summary>
        internal static class RemoveFromCutApi
        {
            public const string MessageName = "sanic_CHRemoveFromCut";

            /// <summary>sanic_chtbltransactionsid de la transaccion a desasignar (REQUERIDO).</summary>
            public const string InTransactionId = "TransactionId";

            /// <summary>Motivo del rechazo del Core (opcional); se persiste en sanic_motivorechazocore.</summary>
            public const string InMotivo = "Motivo";

            public const string OutBatchId = "BatchId";
            public const string OutTotalNio = "TotalNIO";
            public const string OutTotalUsd = "TotalUSD";
            public const string OutTotalEur = "TotalEUR";
        }

        /// <summary>
        /// Custom API de conteo del corte (dry-run): cuenta los elegibles del cajero con
        /// la MISMA semantica que CreateTransactionCutPlugin, sin crear nada. El dialogo
        /// de confirmacion de scrCorte lo usa para que el numero que confirma el operador
        /// y el que ejecuta el corte salgan de la misma fuente (mata D-2).
        /// </summary>
        internal static class PreviewCutApi
        {
            public const string MessageName = "sanic_CHPreviewTransactionCut";
            public const string InCajeroId = "CajeroId";
            public const string OutEligibleCount = "EligibleCount";
            public const string OutTotalNio = "TotalNIO";
            public const string OutTotalUsd = "TotalUSD";
            public const string OutTotalEur = "TotalEUR";
        }

        /// <summary>
        /// Custom API que alimenta el rail de cajeros de scrCorte/scrManual/scrLotes:
        /// agregado por cajero (el GroupBy no delega en canvas). Devuelve un JSON string
        /// (Workload) con una fila por cajero que tiene trabajo pendiente.
        /// </summary>
        internal static class WorkloadApi
        {
            public const string MessageName = "sanic_CHWorkloadByCashier";
            public const string OutWorkload = "Workload";
        }

        /// <summary>Team de Dataverse. Los AAD group teams materializan su membresia en teammembership.</summary>
        internal static class Team
        {
            public const string LogicalName = "team";
            public const string Id = "teamid";
            public const string AadObjectId = "azureactivedirectoryobjectid";
            public const string MembershipRelationship = "teammembership";
        }

        internal static class SystemUser
        {
            public const string LogicalName = "systemuser";
            public const string Id = "systemuserid";
        }
    }
}
