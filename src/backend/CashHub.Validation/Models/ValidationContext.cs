using System;
using System.Collections.Generic;

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
        /// <summary>
        /// TODOS los vinculos de la direccion en sanic_CHTblClientEmail, con su vigencia,
        /// sin filtrar por cliente. Una sola consulta alimenta VR-03 (¿hay alguno vigente?)
        /// y VR-26 (¿como esta el de ESTE cliente?).
        ///
        /// Reemplaza a IsSenderRegistered, que preguntaba "¿existe ALGUNA fila con esta
        /// direccion?" — un existencial sobre todos los clientes que gobernaba la puerta de
        /// seguridad. Con la clave (sanic_client, sanic_name) esa pregunta no tiene
        /// respuesta bien definida: siempre es "vigente ¿para quien?".
        /// </summary>
        IReadOnlyList<SenderLink> GetSenderLinks(string senderEmail);

        /// <summary>VR-25: resuelve el cliente por CIF (CIFBCO o CIFCOM) con su vigencia.</summary>
        ClientLookup FindClientByCif(string cif);

        /// <summary>VR-23: ¿la cuenta pertenece al cliente y esta vigente?</summary>
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

        /// <summary>Vigencia del cliente que resolvio VR-25. Ortogonal al estado del vinculo.</summary>
        public Vigency ClientVigency { get; set; } = Vigency.Inexistente;

        /// <summary>Cache de los vinculos del remitente; null hasta la primera consulta.</summary>
        public IReadOnlyList<SenderLink>? SenderLinks { get; private set; }

        /// <summary>
        /// Carga los vinculos del remitente una sola vez por corrida. Sin direccion
        /// evaluable devuelve vacio SIN consultar: no hay a quien buscar.
        /// </summary>
        public IReadOnlyList<SenderLink> EnsureSenderLinks()
        {
            if (SenderLinks != null) return SenderLinks;

            var sender = Envelope.SenderEmail;
            SenderLinks = string.IsNullOrWhiteSpace(sender)
                ? (IReadOnlyList<SenderLink>)Array.Empty<SenderLink>()
                : DataAccess.GetSenderLinks(sender!);
            return SenderLinks;
        }

        /// <summary>
        /// Destila los vinculos y la vigencia del cliente en los hechos que el triage
        /// necesita. Se arma del CONTEXTO, no de los resultados de reglas: VR-26 puede
        /// quedar Omitida (si VR-25 fallo por cliente dado de baja) y aun asi tenemos que
        /// saber si el remitente era su contacto autorizado.
        /// </summary>
        public TriageFacts BuildTriageFacts()
        {
            var links = EnsureSenderLinks();
            var linkToClient = Vigency.Inexistente;
            var anyActive = false;
            var anyRevoked = false;

            foreach (var link in links)
            {
                if (link.State == Vigency.Vigente) anyActive = true;
                if (link.State == Vigency.Revocado) anyRevoked = true;
                if (ResolvedClientId != null && link.ClientId == ResolvedClientId.Value)
                {
                    linkToClient = link.State;
                }
            }

            return new TriageFacts(linkToClient, anyActive, anyRevoked, ClientVigency);
        }
    }
}
