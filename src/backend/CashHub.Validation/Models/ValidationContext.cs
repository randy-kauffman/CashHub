using System;

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

        /// <summary>VR-25: resuelve el cliente por CIF (CIFBCO o CIFCOM). Null si no existe.</summary>
        Guid? FindClientIdByCif(string cif);

        /// <summary>VR-26: ¿existe el par (remitente, cliente) activo? El correo AUTORIZA.</summary>
        bool IsSenderAuthorizedForClient(string senderEmail, Guid clientId);

        /// <summary>VR-23: ¿la cuenta pertenece al cliente?</summary>
        bool AccountBelongsToClient(string accountNumber, Guid clientId);
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

        /// <summary>El xlsx parseado; null si no hubo adjunto legible.</summary>
        public WorkbookModel? Workbook { get; set; }

        /// <summary>Por qué no se pudo parsear (mensaje explícito del parser), si aplica.</summary>
        public string? WorkbookError { get; set; }

        /// <summary>Cliente resuelto por VR-25 vía CIF: la ATRIBUCIÓN de la declaración.</summary>
        public Guid? ResolvedClientId { get; set; }

        /// <summary>
        /// CIF que el cliente declaro en el Excel, exista o no en CashHub. Se guarda
        /// aparte de ResolvedClientId porque cuando el CIF NO resuelve es justamente
        /// cuando hace falta: permite que el barrido detecte despues que Banca cargo
        /// ese cliente y reprocese el correo solo.
        /// </summary>
        public string? DeclaredCif { get; set; }
    }
}
