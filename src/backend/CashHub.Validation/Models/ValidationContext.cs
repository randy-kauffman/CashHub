namespace CashHub.Validation.Models
{
    /// <summary>Datos del "sobre" del correo: lo que se valida sin abrir el archivo.</summary>
    public sealed class EmailEnvelope
    {
        public string? SenderEmail { get; set; }
        public string? Subject { get; set; }
        public int AttachmentCount { get; set; }
        public string? AttachmentFileName { get; set; }
        public long AttachmentSizeKb { get; set; }
    }

    /// <summary>
    /// Acceso a datos que las reglas necesitan de Dataverse. Se implementa con
    /// IOrganizationService en el plugin y con fakes en los tests.
    /// </summary>
    public interface IValidationDataAccess
    {
        /// <summary>VR-03: ¿el remitente existe (activo) en sanic_CHTblClientEmail?</summary>
        bool IsSenderRegistered(string senderEmail);
    }

    /// <summary>Todo lo que una regla puede mirar durante una corrida.</summary>
    public sealed class ValidationContext
    {
        public ValidationContext(EmailEnvelope envelope, IValidationDataAccess dataAccess)
        {
            Envelope = envelope;
            DataAccess = dataAccess;
        }

        public EmailEnvelope Envelope { get; }
        public IValidationDataAccess DataAccess { get; }
    }
}
