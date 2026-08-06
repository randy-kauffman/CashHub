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
    }
}
