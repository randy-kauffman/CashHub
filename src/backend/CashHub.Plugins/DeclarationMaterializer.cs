using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Models;
using CashHub.Validation.Rules;
using Microsoft.Xrm.Sdk;
using static CashHub.Validation.Rules.TemplateContract;

namespace CashHub.Plugins
{
    /// <summary>
    /// Con la validación en verde, convierte el libro parseado en la declaración digital:
    /// Declaration (atribuida al cliente) → DeclaredBags (con totales) → DeclaredDeposits
    /// → DeclaredChecks. Traza cada creación con su id.
    /// </summary>
    public sealed class DeclarationMaterializer
    {
        private static class Decl
        {
            public const string Entity = "sanic_chtbldeclaration";
            public const string Name = "sanic_name";
            public const string Client = "sanic_client";
            public const string SourceEntity = "sanic_sourceentity";
            public const string SourceRecordId = "sanic_sourcerecordid";

            /// <summary>
            /// Vinculo real al correo (B1). SourceRecordId se conserva por la tabla
            /// generica, pero un GUID en texto no delega: el tracker resuelve
            /// tracking → declaracion con este lookup (mismo racional que
            /// Run.ReceivedEmailLookup, documentado en Schema.cs).
            /// </summary>
            public const string ReceivedEmail = "sanic_receivedemail";
        }

        private static class Bag
        {
            public const string Entity = "sanic_chtbldeclaredbags";
            public const string Name = "sanic_name";
            public const string Number = "sanic_bagnumber";
            public const string Declaration = "sanic_declarationid";
            public const string TotalDeposits = "sanic_totaldeposits";
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";
        }

        private static class Deposit
        {
            public const string Entity = "sanic_chtbldeclareddeposits";
            public const string Name = "sanic_name";
            public const string TransactionNo = "sanic_transactionno";
            public const string AccountNumber = "sanic_accountnumber";
            public const string Currency = "sanic_currency";
            public const string BacChecks = "sanic_baclocalcheckamount";
            public const string OtherChecks = "sanic_otherlocalcheckamount";
            public const string Cash = "sanic_cashamount";
            public const string Total = "sanic_total";
            public const string Reference = "sanic_depositreference";
            public const string DeclaredBag = "sanic_declaredbag";

            /// <summary>Denormalizacion a la declaracion raiz (B7): la pantalla Correo del
            /// tracker llena sus grillas con UNA query delegable por tabla.</summary>
            public const string Declaration = "sanic_declaration";
        }

        private static class Check
        {
            public const string Entity = "sanic_chtbldeclaredchecks";
            public const string Name = "sanic_name";
            public const string Number = "sanic_checknumber";
            public const string Amount = "sanic_checkamount";
            public const string Currency = "sanic_checkcurrency";
            public const string Type = "sanic_checktype";
            public const string DeclaredDeposit = "sanic_declareddepositid";

            /// <summary>Denormalizacion a la declaracion raiz (B7).</summary>
            public const string Declaration = "sanic_declaration";
        }

        private static readonly Dictionary<string, int> CurrencyMap =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["NIO"] = 159460000, ["USD"] = 159460001, ["EUR"] = 159460002 };

        private const int CheckTypeBacLocal = 159460000;
        private const int CheckTypeOtrosLocal = 159460001;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        public DeclarationMaterializer(IOrganizationService service, ITracingService tracing)
        {
            _service = service;
            _tracing = tracing;
        }

        public Guid Materialize(Guid emailId, string trackingId, WorkbookModel workbook, Guid clientId)
        {
            var deposits = workbook.Table(DepositsTable)
                ?? throw new InvalidPluginExecutionException(
                    $"Materializacion del correo {emailId:D}: el libro validado no contiene la tabla '{DepositsTable}' — esto no deberia pasar tras VR-08.");
            var checks = workbook.Table(ChecksTable)
                ?? throw new InvalidPluginExecutionException(
                    $"Materializacion del correo {emailId:D}: el libro validado no contiene la tabla '{ChecksTable}' — esto no deberia pasar tras VR-08.");

            _tracing.Trace("Materializer: correo={0} cliente={1} depositos={2} cheques={3}",
                emailId, clientId, deposits.Rows.Count, checks.Rows.Count);

            var declarationId = _service.Create(new Entity(Decl.Entity)
            {
                // Numero de gestion (Email-N) en el nombre, no el GUID: es lo que el
                // operador ve en apps y vistas.
                [Decl.Name] = $"Declaracion {DateTime.UtcNow:yyyy-MM-dd HH:mm} {trackingId}",
                [Decl.Client] = new EntityReference("sanic_chtblclient", clientId),
                [Decl.SourceEntity] = Schema.ReceivedEmail.LogicalName,
                [Decl.SourceRecordId] = emailId.ToString("D"),
                [Decl.ReceivedEmail] = new EntityReference(Schema.ReceivedEmail.LogicalName, emailId)
            });
            _tracing.Trace("Materializer: Declaration creada {0}", declarationId);

            var bagIds = CreateBags(declarationId, deposits);
            var depositIds = CreateDeposits(emailId, declarationId, deposits, bagIds);
            CreateChecks(emailId, declarationId, checks, depositIds);

            _tracing.Trace("Materializer: completo — {0} bolsas, {1} depositos, {2} cheques.",
                bagIds.Count, depositIds.Count, checks.Rows.Count);
            return declarationId;
        }

        private Dictionary<string, Guid> CreateBags(Guid declarationId, TableModel deposits)
        {
            // La clave del grupo YA viene normalizada (trim + MAYUSCULAS), asi que es
            // exactamente lo que se persiste: antes se agrupaba sin distinguir mayusculas
            // pero se guardaba la capitalizacion de la PRIMERA fila del grupo, y la misma
            // tula quedaba escrita distinto segun el orden de las filas del Excel.
            var bagIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in deposits.Rows.GroupBy(
                         r => DeclaredValueNormalizer.BagNumber(r[Deposits.BagNo]!.Raw),
                         StringComparer.OrdinalIgnoreCase))
            {
                decimal Total(string currency) => group
                    .Where(r => string.Equals(r[Deposits.Currency]?.Raw?.Trim(), currency,
                        StringComparison.OrdinalIgnoreCase))
                    .Sum(r => { r[Deposits.Total]!.TryDecimal(out var t); return t; });

                // sanic_totaldeposits era un rollup SIN definicion de agregacion
                // (FormulaDefinition null: nunca calculo, siempre 0). Se reemplazo por
                // una columna entera simple que escribe este materializador — mismo
                // patron que el resto de los totales materializados del proyecto.
                var bagId = _service.Create(new Entity(Bag.Entity)
                {
                    [Bag.Name] = group.Key,
                    [Bag.Number] = group.Key,
                    [Bag.Declaration] = new EntityReference(Decl.Entity, declarationId),
                    [Bag.TotalDeposits] = group.Count(),
                    [Bag.TotalNio] = Total("NIO"),
                    [Bag.TotalUsd] = Total("USD"),
                    [Bag.TotalEur] = Total("EUR")
                });
                bagIds[group.Key] = bagId;
                _tracing.Trace("Materializer: DeclaredBag '{0}' -> {1} ({2} depositos)",
                    group.Key, bagId, group.Count());
            }
            return bagIds;
        }

        private Dictionary<string, Guid> CreateDeposits(Guid emailId, Guid declarationId,
            TableModel deposits, Dictionary<string, Guid> bagIds)
        {
            var depositIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in deposits.Rows)
            {
                var depositNo = row[Deposits.DepositNo]!.Raw!.Trim();
                var accountRaw = row[Deposits.AccountNo]!.Raw!.Trim();
                if (!int.TryParse(accountRaw, out var accountNumber))
                {
                    // Deuda conocida del esquema: sanic_accountnumber es int.
                    throw new InvalidPluginExecutionException(
                        $"Materializacion del correo {emailId:D}: la cuenta '{accountRaw}' (fila {row.RowNumber} de {DepositsTable}) " +
                        "no cabe en el campo sanic_accountnumber (entero de 32 bits). " +
                        "Es la deuda de esquema documentada en el backlog (AccountNumber int); requiere migrar el campo a texto.");
                }

                var deposit = new Entity(Deposit.Entity)
                {
                    [Deposit.Name] = $"Deposito {depositNo}",
                    [Deposit.TransactionNo] = int.Parse(depositNo),
                    [Deposit.AccountNumber] = accountNumber,
                    [Deposit.Currency] = MapCurrency(emailId, row[Deposits.Currency]!.Raw!, row.RowNumber),
                    [Deposit.BacChecks] = ReadDecimal(row, Deposits.BacChecks),
                    [Deposit.OtherChecks] = ReadDecimal(row, Deposits.OtherChecks),
                    [Deposit.Cash] = ReadDecimal(row, Deposits.Cash),
                    [Deposit.Total] = ReadDecimal(row, Deposits.Total),
                    [Deposit.Reference] = row[Deposits.Reference]?.Raw,
                    [Deposit.DeclaredBag] = new EntityReference(Bag.Entity,
                        bagIds[DeclaredValueNormalizer.BagNumber(row[Deposits.BagNo]!.Raw)]),
                    [Deposit.Declaration] = new EntityReference(Decl.Entity, declarationId)
                };
                var id = _service.Create(deposit);
                depositIds[depositNo] = id;
                _tracing.Trace("Materializer: DeclaredDeposit {0} (cuenta {1}) -> {2}",
                    depositNo, accountRaw, id);
            }
            return depositIds;
        }

        private void CreateChecks(Guid emailId, Guid declarationId, TableModel checks,
            Dictionary<string, Guid> depositIds)
        {
            foreach (var row in checks.Rows)
            {
                var depositNo = row[Checks.DepositNo]!.Raw!.Trim();
                if (!depositIds.TryGetValue(depositNo, out var depositId))
                {
                    throw new InvalidPluginExecutionException(
                        $"Materializacion del correo {emailId:D}: el cheque de la fila {row.RowNumber} referencia el deposito '{depositNo}' " +
                        "que no fue materializado — esto no deberia pasar tras VR-22.");
                }

                var typeRaw = row[Checks.Type]!.Raw!.Trim();
                var type = typeRaw.IndexOf("BAC", StringComparison.OrdinalIgnoreCase) >= 0
                    ? CheckTypeBacLocal
                    : CheckTypeOtrosLocal;

                // Trimmeado, y como TEXTO: los ceros a la izquierda son significativos
                // (0001234 y 1234 son cheques distintos). VR-29 ya garantizo que sean
                // solo digitos, validando este MISMO valor normalizado.
                var checkNumber = DeclaredValueNormalizer.CheckNumber(row[Checks.CheckNo]!.Raw);

                var id = _service.Create(new Entity(Check.Entity)
                {
                    [Check.Name] = $"Cheque {checkNumber}",
                    [Check.Number] = checkNumber,
                    [Check.Amount] = ReadDecimal(row, Checks.Amount),
                    [Check.Currency] = MapCurrency(emailId, row[Checks.Currency]!.Raw!, row.RowNumber),
                    [Check.Type] = new OptionSetValue(type),
                    [Check.DeclaredDeposit] = new EntityReference(Deposit.Entity, depositId),
                    [Check.Declaration] = new EntityReference(Decl.Entity, declarationId)
                });
                _tracing.Trace("Materializer: DeclaredCheck {0} (deposito {1}) -> {2}",
                    checkNumber, depositNo, id);
            }
        }

        private static OptionSetValue MapCurrency(Guid emailId, string currency, int row)
        {
            if (!CurrencyMap.TryGetValue(currency.Trim(), out var value))
            {
                throw new InvalidPluginExecutionException(
                    $"Materializacion del correo {emailId:D}: la moneda '{currency}' (fila {row}) " +
                    "no mapea a NIO/USD/EUR — esto no deberia pasar tras VR-18.");
            }
            return new OptionSetValue(value);
        }

        private static decimal ReadDecimal(TableRow row, string column)
        {
            row[column]!.TryDecimal(out var value);
            return value; // los montos de Declared* son columnas decimal, no money
        }
    }
}
