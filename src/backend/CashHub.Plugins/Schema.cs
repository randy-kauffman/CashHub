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

            // Triage del remitente. Solo Registrado habilita que salga correo del banco.
            public const int SenderRegistrado = 159460000;
            public const int SenderClienteSinRegistrar = 159460001;
            public const int SenderSospechoso = 159460002;
            public const int SenderNoCorresponde = 159460003;

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
            public const string ReceivedEmailLookup = "sanic_ReceivedEmail";
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
            public const string RowNumber = "sanic_rownumber";
            public const string Detail = "sanic_detail";
            public const string SourceType = "sanic_sourcetype";

            public const int OutcomePass = 159460000;
            public const int OutcomeFail = 159460001;
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

            /// <summary>Pre-image registrada con sanic_changestatus, sanic_targettable y createdby (solo Update).</summary>
            public const string PreImageName = "PreImage";
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
            public const string AadGroupId = "sanic_aadgroupid";
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

            public const string PreImageName = "PreImage";
        }

        /// <summary>Planilla de recepcion (CH-Tbl-Receptions).</summary>
        internal static class Reception
        {
            public const string LogicalName = "sanic_chtblreceptions";
            public const string Id = "sanic_chtblreceptionsid";
            public const string BatchLookup = "sanic_receptionbatch";

            /// <summary>
            /// Espejo entero del sanic_receptionstatus del lote, mantenido por
            /// ReceptionBatchStatusMirrorPlugin. Existe para que las apps filtren
            /// planillas por estado de lote con delegacion (un Filter sobre columna
            /// de la entidad relacionada no delega y miente a partir de 500 filas).
            /// </summary>
            public const string BatchStatusMirror = "sanic_batchstatus";

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

            public const string PreImageName = "PreImage";
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
