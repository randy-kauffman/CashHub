using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using static CashHub.Validation.Rules.TemplateContract;

namespace CashHub.Validation.Rules
{
    /// <summary>VR-24: el CIF del cliente viene declarado en la celda del parámetro.</summary>
    public sealed class Vr24CifDeclared : IValidationRule
    {
        public string Key => "VR-24";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var cellRef = definition.Value ?? "Depositos!J2";
            var cif = context.Workbook!.ReadCell(cellRef);
            if (string.IsNullOrWhiteSpace(cif))
            {
                var parts = cellRef.Split('!');
                return new[] { new Finding(Key, sheet: parts[0],
                    cellRef: parts.Length > 1 ? parts[1] : null, expectedValue: cellRef) };
            }
            return Array.Empty<Finding>();
        }
    }

    /// <summary>VR-25: el CIF corresponde a un cliente registrado. Resuelve la ATRIBUCIÓN.</summary>
    public sealed class Vr25ClientRegistered : IValidationRule
    {
        public string Key => "VR-25";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var cif = context.Workbook!.ReadCell("Depositos!J2")!.Trim();
            context.DeclaredCif = cif;

            var lookup = context.DataAccess.FindClientByCif(cif);
            context.ClientVigency = lookup.Vigency;

            // ATRIBUYE aunque el cliente esté dado de baja: sin sanic_client el correo
            // queda huérfano y no hay forma de rastrearlo desde la ficha del cliente.
            // No habilita nada: la regla igual falla, así que nunca se materializa.
            if (lookup.Id != null) context.ResolvedClientId = lookup.Id;

            // Un solo mensaje para las dos causas de fallo — "no corresponde a ningún
            // cliente ACTIVO" es verdad tanto si el CIF no existe como si el cliente existe
            // dado de baja. Además borra un oráculo: con dos textos distintos, quien mande
            // CIFs al voleo podría distinguir "no existe" de "existe pero está inactivo".
            // Con uno solo no aprende nada, y negocio conserva el control del texto.
            if (lookup.Vigency == Vigency.Vigente) return Array.Empty<Finding>();

            return new[] { new Finding(Key, sheet: "Depositos", cellRef: "J2", actualValue: cif) };
        }
    }

    /// <summary>
    /// VR-26: el vínculo entre el remitente y el cliente resuelto está VIGENTE. El correo
    /// AUTORIZA.
    ///
    /// Es la puerta de CONTENIDO: solo un vínculo vigente habilita contarle detalles del
    /// cliente. Un vínculo revocado o pendiente falla acá aunque el remitente tenga otros
    /// vínculos vivos — la revocación es por PAR (dirección × cliente), no por dirección.
    /// </summary>
    public sealed class Vr26SenderAuthorizedForClient : IValidationRule
    {
        public string Key => "VR-26";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var sender = context.Envelope.SenderEmail!;
            var clientId = context.ResolvedClientId!.Value;

            foreach (var link in context.EnsureSenderLinks())
            {
                if (link.ClientId != clientId) continue;
                return link.State == Vigency.Vigente
                    ? Array.Empty<Finding>()
                    : new[] { new Finding(Key, actualValue: sender) };
            }
            return new[] { new Finding(Key, actualValue: sender) };
        }
    }

    /// <summary>VR-23: cada cuenta declarada pertenece al cliente resuelto.</summary>
    public sealed class Vr23AccountsBelongToClient : IValidationRule
    {
        public string Key => "VR-23";

        public IReadOnlyList<Finding> Evaluate(RuleDefinition definition, ValidationContext context)
        {
            var table = context.Workbook!.Table(DepositsTable)!;
            var clientId = context.ResolvedClientId!.Value;
            var verified = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var findings = new List<Finding>();

            foreach (var row in table.Rows)
            {
                var account = row[Deposits.AccountNo];
                if (account?.IsEmpty != false) continue;

                var number = account!.Raw!.Trim();
                if (!verified.TryGetValue(number, out var belongs))
                {
                    belongs = context.DataAccess.AccountBelongsToClient(number, clientId);
                    verified[number] = belongs;
                }
                if (!belongs)
                {
                    findings.Add(new Finding(Key, sheet: table.Sheet, cellRef: account.CellRef,
                        rowNumber: row.RowNumber, actualValue: number));
                }
            }
            return findings;
        }
    }

    /// <summary>Catálogo completo de estrategias del motor (sobre + estructura + contenido + negocio).</summary>
    public static class FullRuleSet
    {
        public static IReadOnlyList<IValidationRule> All =>
            EnvelopeRuleSet.All.Concat(new IValidationRule[]
            {
                new Vr07WorkbookHasTables(),
                new Vr08TablesAreNamed(),
                new Vr19DepositColumns(),
                new Vr16CheckColumns(),
                new Vr27OfficialTemplate(),
                new Vr09AtLeastOneDeposit(),
                new Vr10DepositHasBag(),
                new Vr11DepositHasAccount(),
                new Vr17NumericColumnsAreNumeric(),
                new Vr20NoNegatives(),
                new Vr21DepositTotalPositive(),
                new Vr18SupportedCurrencies(),
                new Vr13AccountSingleCurrency(),
                new Vr15CheckFieldsComplete(),
                new Vr14NoDuplicateChecks(),
                new Vr22NoOrphanChecks(),
                new Vr12ChecksMatchDeclared(),
                new Vr28CheckBreakdownMatches(),
                new Vr24CifDeclared(),
                new Vr25ClientRegistered(),
                new Vr26SenderAuthorizedForClient(),
                new Vr23AccountsBelongToClient()
            }).ToList();
    }
}
