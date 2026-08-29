using System;

namespace CashHub.Validation.Models
{
    /// <summary>
    /// Vigencia de una fila maestra, leida de sanic_recordstatus (NO de statecode: CashHub
    /// nunca desactiva a nivel plataforma, asi que statecode siempre vale 0).
    ///
    /// PendingApproval NO es un revocado: es un alta esperando firma. Confundirlos
    /// dispararia alerta de seguridad sobre trabajo administrativo rutinario.
    /// </summary>
    public enum Vigency
    {
        /// <summary>No hay fila. Es el default a proposito: sin evidencia, no hay confianza.</summary>
        Inexistente = 0,

        /// <summary>Active o PendingDeactivation: opera hasta que el aprobador firme la baja.</summary>
        Vigente = 1,

        /// <summary>Inactive: se le quito la autorizacion deliberadamente.</summary>
        Revocado = 2,

        /// <summary>PendingApproval: alta pedida, todavia sin aprobar. No autoriza nada.</summary>
        Pendiente = 3
    }

    /// <summary>
    /// Una fila de CH-Tbl-ClientEmail: el VINCULO entre una direccion y UN cliente.
    ///
    /// La clave alternativa de la tabla es (sanic_client, sanic_name) — "Correo unico por
    /// cliente" — asi que la misma direccion existe N veces, una por cliente, cada una con
    /// su propia vigencia. Desactivar un ClientEmail revoca el PAR, no la direccion.
    /// </summary>
    public sealed class SenderLink
    {
        public SenderLink(Guid clientId, Vigency state)
        {
            ClientId = clientId;
            State = state;
        }

        public Guid ClientId { get; }
        public Vigency State { get; }
    }

    /// <summary>
    /// Resolucion de un CIF. Distingue "no existe" de "existe pero no esta vigente":
    /// colapsar ambos en null hacia que VR-25 le dijera al cliente "el CIF no corresponde
    /// a ningun cliente" cuando si corresponde, y dejaba el correo sin atribuir.
    /// </summary>
    public sealed class ClientLookup
    {
        public ClientLookup(Guid? id, Vigency vigency)
        {
            Id = id;
            Vigency = vigency;
        }

        public Guid? Id { get; }
        public Vigency Vigency { get; }

        public static ClientLookup NotFound { get; } = new(null, Vigency.Inexistente);
    }

    /// <summary>
    /// Lo que el triage necesita saber del remitente, ya resuelto. Son hechos de DATOS,
    /// separados de los resultados de reglas: una regla omitida por dependencia no puede
    /// borrar lo que sabemos de los vinculos.
    /// </summary>
    public sealed class TriageFacts
    {
        public TriageFacts(Vigency linkToClient, bool hasAnyActiveLink, bool hasAnyRevokedLink, Vigency client)
        {
            LinkToClient = linkToClient;
            HasAnyActiveLink = hasAnyActiveLink;
            HasAnyRevokedLink = hasAnyRevokedLink;
            Client = client;
        }

        /// <summary>Vigencia del vinculo con el cliente que el CIF resolvio. Puerta de CONTENIDO.</summary>
        public Vigency LinkToClient { get; }

        /// <summary>¿La direccion tiene al menos un vinculo vigente con el banco? Puerta de RESPUESTA.</summary>
        public bool HasAnyActiveLink { get; }

        /// <summary>¿Hay vinculos revocados? Distingue "nunca registrado" de "le cortamos".</summary>
        public bool HasAnyRevokedLink { get; }

        /// <summary>Vigencia del cliente declarado. Eje ORTOGONAL al del vinculo.</summary>
        public Vigency Client { get; }

        /// <summary>Sin remitente evaluable no sabemos nada: no se responde.</summary>
        public static TriageFacts Unknown { get; } =
            new(Vigency.Inexistente, false, false, Vigency.Inexistente);
    }
}
