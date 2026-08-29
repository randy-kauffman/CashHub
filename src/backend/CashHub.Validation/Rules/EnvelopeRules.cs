using System;
using System.Collections.Generic;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;

namespace CashHub.Validation.Rules
{
    /// <summary>VR-01: el correo tiene un remitente válido.</summary>
    public sealed class Vr01SenderPresent : IValidationRule
    {
        public string Key => "VR-01";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var sender = context.Envelope.SenderEmail;
            if (string.IsNullOrWhiteSpace(sender) || !sender!.Contains("@"))
            {
                return new[] { new Finding(Key, actualValue: sender ?? "(vacio)") };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>VR-02: cantidad exacta de adjuntos (parámetro).</summary>
    public sealed class Vr02AttachmentCount : IValidationRule
    {
        public string Key => "VR-02";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var expected = int.Parse(definition.Value ?? "1");
            var actual = context.Envelope.AttachmentCount;
            if (actual != expected)
            {
                return new[] { new Finding(Key,
                    actualValue: actual.ToString(), expectedValue: expected.ToString()) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>
    /// VR-03: el remitente tiene al menos un vínculo VIGENTE con el banco (depende de VR-01).
    ///
    /// Es la puerta de RESPUESTA: establece que hay relación comercial viva y una dirección
    /// a la que estamos autorizados a escribir. NO dice nada sobre el cliente declarado —
    /// eso es VR-26, y corre 200 posiciones después, cuando el CIF ya se leyó.
    /// </summary>
    public sealed class Vr03SenderRegistered : IValidationRule
    {
        public string Key => "VR-03";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var sender = context.Envelope.SenderEmail!;
            foreach (var link in context.EnsureSenderLinks())
            {
                if (link.State == Vigency.Vigente) return Array.Empty<Finding>();
            }
            return new[] { new Finding(Key, actualValue: sender) };
        }
    }

    /// <summary>VR-04: el asunto comienza con el prefijo del parámetro (insensible a mayúsculas).</summary>
    public sealed class Vr04SubjectPrefix : IValidationRule
    {
        public string Key => "VR-04";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var prefix = definition.Value ?? string.Empty;
            var subject = (context.Envelope.Subject ?? string.Empty).TrimStart();
            if (!subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { new Finding(Key,
                    actualValue: context.Envelope.Subject ?? "(vacio)", expectedValue: prefix) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>VR-05: el adjunto es un archivo con la extensión del parámetro (depende de VR-02).</summary>
    public sealed class Vr05AttachmentIsXlsx : IValidationRule
    {
        public string Key => "VR-05";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var expectedExtension = definition.Value ?? ".xlsx";
            var fileName = context.Envelope.AttachmentFileName ?? string.Empty;
            if (!fileName.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { new Finding(Key,
                    actualValue: string.IsNullOrEmpty(fileName) ? "(sin nombre)" : fileName,
                    expectedValue: expectedExtension) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>VR-06: el adjunto no supera el tamaño máximo en KB (parámetro; depende de VR-05).</summary>
    public sealed class Vr06MaxAttachmentSize : IValidationRule
    {
        public string Key => "VR-06";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var maxKb = long.Parse(definition.Value ?? "5120");
            var actualKb = context.Envelope.AttachmentSizeKb;
            if (actualKb > maxKb)
            {
                return new[] { new Finding(Key,
                    actualValue: actualKb.ToString(), expectedValue: maxKb.ToString()) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>Catálogo de estrategias de sobre disponibles en esta versión del motor.</summary>
    public static class EnvelopeRuleSet
    {
        public static IReadOnlyList<IValidationRule> All => new IValidationRule[]
        {
            new Vr01SenderPresent(),
            new Vr02AttachmentCount(),
            new Vr03SenderRegistered(),
            new Vr04SubjectPrefix(),
            new Vr05AttachmentIsXlsx(),
            new Vr06MaxAttachmentSize()
        };
    }
}
