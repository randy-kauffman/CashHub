using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>Naturaleza contable del asiento (sanic_transactiontype).</summary>
    public enum LedgerEntryType
    {
        Deposito = 159460000,
        Faltante = 159460001,
        Sobrante = 159460002,
        SinDeclaracion = 159460003,
    }

    /// <summary>Como se procesa el asiento (sanic_transactionprocessingtype).</summary>
    public enum LedgerProcessing
    {
        Manual = 159460000,
        Batch = 159460001,
    }

    /// <summary>Un deposito tal como lo conto el cajero (CH-Tbl-DepositsRecounting).</summary>
    public sealed class RecountedDeposit
    {
        public RecountedDeposit(
            Guid id, int? accountNumber, int? currency,
            decimal cashAmount, decimal bacCheckAmount, decimal otherCheckAmount,
            int? transactionNumber)
        {
            Id = id;
            AccountNumber = accountNumber;
            Currency = currency;
            CashAmount = cashAmount;
            BacCheckAmount = bacCheckAmount;
            OtherCheckAmount = otherCheckAmount;
            TransactionNumber = transactionNumber;
        }

        public Guid Id { get; }
        public int? AccountNumber { get; }
        public int? Currency { get; }
        public decimal CashAmount { get; }
        public decimal BacCheckAmount { get; }
        public decimal OtherCheckAmount { get; }
        public int? TransactionNumber { get; }

        /// <summary>Un deposito con cheques no puede liquidarse por lote.</summary>
        public bool HasChecks => BacCheckAmount > 0 || OtherCheckAmount > 0;
    }

    /// <summary>Un deposito tal como lo declaro el cliente (CH-Tbl-DeclaredDeposits).</summary>
    public sealed class DeclaredDeposit
    {
        public DeclaredDeposit(
            Guid id, int? accountNumber, int? currency,
            decimal cashAmount, decimal bacCheckAmount, decimal otherCheckAmount,
            int? transactionNumber, string? depositReference, Guid? relatedRecountedDepositId)
        {
            Id = id;
            AccountNumber = accountNumber;
            Currency = currency;
            CashAmount = cashAmount;
            BacCheckAmount = bacCheckAmount;
            OtherCheckAmount = otherCheckAmount;
            TransactionNumber = transactionNumber;
            DepositReference = depositReference;
            RelatedRecountedDepositId = relatedRecountedDepositId;
        }

        public Guid Id { get; }
        public int? AccountNumber { get; }
        public int? Currency { get; }
        public decimal CashAmount { get; }
        public decimal BacCheckAmount { get; }
        public decimal OtherCheckAmount { get; }
        public int? TransactionNumber { get; }
        public string? DepositReference { get; }
        public Guid? RelatedRecountedDepositId { get; }
    }

    /// <summary>Un asiento a crear en CH-Tbl-Transactions.</summary>
    public sealed class LedgerEntry
    {
        public LedgerEntry(
            int? accountNumber, int? currency,
            decimal cashAmount, decimal bacCheckAmount, decimal otherCheckAmount,
            string reference, LedgerEntryType type, int? transactionNumber,
            LedgerProcessing processing, Guid? relatedRecountedDepositId)
        {
            AccountNumber = accountNumber;
            Currency = currency;
            CashAmount = cashAmount;
            BacCheckAmount = bacCheckAmount;
            OtherCheckAmount = otherCheckAmount;
            Reference = reference;
            Type = type;
            TransactionNumber = transactionNumber;
            Processing = processing;
            RelatedRecountedDepositId = relatedRecountedDepositId;
        }

        public int? AccountNumber { get; }
        public int? Currency { get; }
        public decimal CashAmount { get; }
        public decimal BacCheckAmount { get; }
        public decimal OtherCheckAmount { get; }
        public string Reference { get; }
        public LedgerEntryType Type { get; }
        public int? TransactionNumber { get; }
        public LedgerProcessing Processing { get; }

        /// <summary>Recuento que origina el asiento; null cuando no hay ninguno.</summary>
        public Guid? RelatedRecountedDepositId { get; }
    }

    /// <summary>
    /// Los asientos que genera el cierre de un recuento. Salian de scrAprobaciones
    /// (CH-App-Supervisor) como Power Fx sin un solo test — generando plata contra
    /// cuentas de clientes.
    ///
    /// No toca Dataverse: recibe lo ya leido y devuelve que asientos hay que crear.
    /// El plugin hace la E/S; aca vive lo que se puede razonar y fijar con un test.
    ///
    /// LA REGLA QUE MAS IMPORTA: la diferencia se calcula POR COMPONENTE, nunca por el
    /// neto. Un deposito con 100 de mas en efectivo y 100 de menos en cheques no es un
    /// cierre limpio: es un sobrante de 100 Y un faltante de 100. Netearlos haria
    /// desaparecer las dos mitades de un descuadre real.
    /// </summary>
    public static class RecountClosureAccounting
    {
        /// <summary>
        /// Tope de la referencia que impone el CORE. NO es el de la columna
        /// (<c>sanic_transactionreference</c> admite 31): es el del sistema que
        /// consume el archivo. La referencia se descarga y se sube al core, y el
        /// core no acepta mas de 30.
        /// </summary>
        public const int CoreReferenceMaxLength = 30;

        /// <summary>
        /// La referencia se pasa del tope del core?
        ///
        /// Se pregunta en vez de truncar A PROPOSITO. El numero de tula es lo que
        /// identifica el asiento del otro lado: recortarlo manda al core una
        /// referencia equivocada, y dos tulas distintas podrian colapsar en la
        /// misma. Un cierre bloqueado se reintenta; un asiento mal referenciado ya
        /// viajo. El llamador la rechaza como dato ANTES de escribir nada.
        ///
        /// Presupuesto por prefijo (30 - largo del prefijo = tula maxima):
        ///   "S/"  (2) -> tula <= 28
        ///   "F/"  (2) -> tula <= 28
        ///   "SS/" (3) -> tula <= 27
        ///   "SD/" (3) -> tula <= 27
        /// Con las tulas de hoy (maximo 11) nunca dispara. Sigue existiendo porque
        /// sanic_bagid admite 50 y el negocio habla de tulas de hasta 30: de 28 en
        /// adelante ya no entra ni con estos codigos.
        /// </summary>
        public static bool ExceedsCoreLimit(string? reference) =>
            (reference?.Length ?? 0) > CoreReferenceMaxLength;

        // Codigos de la referencia que viaja al core. Son CORTOS a proposito: la
        // referencia tope 30 y el numero de tula puede llegar a 30, asi que cada
        // caracter de prefijo se le come presupuesto a lo unico que de verdad
        // identifica el asiento del otro lado. Estos codigos estan acordados con
        // quien lee el archivo en el core — cambiarlos rompe esa lectura.
        private const string RefSobrante = "S/";
        private const string RefFaltante = "F/";
        private const string RefSobranteSinDeclarar = "SS/";
        private const string RefSinDeclaracion = "SD/";

        /// <summary>
        /// Arma la referencia ENTERA, sin recortar. Ver <see cref="ExceedsCoreLimit"/>.
        /// </summary>
        private static string Reference(string prefix, string? suffix) => prefix + suffix;

        /// <summary>
        /// Cierre de "Tula con diferencia": recontados contra declarados.
        ///
        /// Tres ramas, y ninguna es opcional:
        ///   1. recontado sin declarado  -> Sobrante (alguien conto plata que nadie declaro)
        ///   2. recontado con declarado  -> diferencias por componente + el deposito real
        ///   3. declarado sin recontar   -> Faltante + su contrapartida de deposito
        /// </summary>
        public static IReadOnlyList<LedgerEntry> ForBagWithDifferences(
            string? bagNumber,
            IEnumerable<RecountedDeposit>? recounted,
            IEnumerable<DeclaredDeposit>? declared)
        {
            var entries = new List<LedgerEntry>();
            var recountedList = ToList(recounted);
            var declaredList = ToList(declared);

            // Indice del declarado por recuento enlazado. Un declarado que apunta a un
            // recuento que no vino NO cuenta como recontado: darlo por bueno seria dar
            // por contada plata que nadie conto.
            var declaredByRecounted = new Dictionary<Guid, DeclaredDeposit>();
            var matchedDeclared = new HashSet<Guid>();
            foreach (var d in declaredList)
            {
                if (d.RelatedRecountedDepositId is Guid link && link != Guid.Empty)
                {
                    declaredByRecounted[link] = d;
                }
            }

            foreach (var r in recountedList)
            {
                if (!declaredByRecounted.TryGetValue(r.Id, out var d))
                {
                    // (1) Contado y no declarado. La cuenta sale del RECUENTO: es el
                    //     unico lado que existe.
                    entries.Add(new LedgerEntry(
                        r.AccountNumber, r.Currency,
                        r.CashAmount, r.BacCheckAmount, r.OtherCheckAmount,
                        Reference(RefSobranteSinDeclarar, bagNumber),
                        LedgerEntryType.Sobrante, r.TransactionNumber,
                        LedgerProcessing.Manual, r.Id));
                    continue;
                }

                matchedDeclared.Add(d.Id);

                // (2) Diferencias POR COMPONENTE. La cuenta sale del DECLARADO: la plata
                //     se le acredita o debita a la cuenta que el cliente declaro.
                var dCash = r.CashAmount - d.CashAmount;
                var dBac = r.BacCheckAmount - d.BacCheckAmount;
                var dOther = r.OtherCheckAmount - d.OtherCheckAmount;

                var sobranteCash = Positive(dCash);
                var sobranteBac = Positive(dBac);
                var sobranteOther = Positive(dOther);
                if (sobranteCash + sobranteBac + sobranteOther > 0)
                {
                    entries.Add(new LedgerEntry(
                        d.AccountNumber, r.Currency,
                        sobranteCash, sobranteBac, sobranteOther,
                        Reference(RefSobrante, bagNumber),
                        LedgerEntryType.Sobrante, r.TransactionNumber,
                        LedgerProcessing.Manual, r.Id));
                }

                var faltanteCash = NegativeAsPositive(dCash);
                var faltanteBac = NegativeAsPositive(dBac);
                var faltanteOther = NegativeAsPositive(dOther);
                if (faltanteCash + faltanteBac + faltanteOther > 0)
                {
                    entries.Add(new LedgerEntry(
                        d.AccountNumber, r.Currency,
                        faltanteCash, faltanteBac, faltanteOther,
                        Reference(RefFaltante, bagNumber),
                        LedgerEntryType.Faltante, r.TransactionNumber,
                        LedgerProcessing.Manual, r.Id));
                }

                // El deposito en si, con los montos REALMENTE contados. Con cheques no
                // puede ir por lote: alguien los tiene que procesar a mano.
                entries.Add(new LedgerEntry(
                    d.AccountNumber, r.Currency,
                    r.CashAmount, r.BacCheckAmount, r.OtherCheckAmount,
                    Reference(string.Empty, d.DepositReference),
                    LedgerEntryType.Deposito, r.TransactionNumber,
                    r.HasChecks ? LedgerProcessing.Manual : LedgerProcessing.Batch,
                    r.Id));
            }

            // (3) Declarado que nadie reconto: falta la plata, y ademas hay que dejar
            //     asentado el deposito que el cliente creia haber hecho.
            foreach (var d in declaredList)
            {
                if (matchedDeclared.Contains(d.Id))
                {
                    continue;
                }

                entries.Add(new LedgerEntry(
                    d.AccountNumber, d.Currency,
                    d.CashAmount, d.BacCheckAmount, d.OtherCheckAmount,
                    Reference(RefFaltante, bagNumber),
                    LedgerEntryType.Faltante, d.TransactionNumber,
                    LedgerProcessing.Manual, null));

                entries.Add(new LedgerEntry(
                    d.AccountNumber, d.Currency,
                    d.CashAmount, d.BacCheckAmount, d.OtherCheckAmount,
                    Reference(string.Empty, d.DepositReference),
                    LedgerEntryType.Deposito, d.TransactionNumber,
                    LedgerProcessing.Manual, null));
            }

            return entries;
        }

        /// <summary>
        /// Cierre de "Tula recontada sin declaracion": no hay contra que comparar, asi
        /// que cada deposito contado va como un asiento 'Sin declaracion'. Sin
        /// declaracion no hay sobrante ni faltante posible — no existe el otro lado.
        /// </summary>
        public static IReadOnlyList<LedgerEntry> ForRecountWithoutDeclaration(
            string? bagNumber, IEnumerable<RecountedDeposit>? recounted)
        {
            var entries = new List<LedgerEntry>();

            foreach (var r in ToList(recounted))
            {
                entries.Add(new LedgerEntry(
                    r.AccountNumber, r.Currency,
                    r.CashAmount, r.BacCheckAmount, r.OtherCheckAmount,
                    Reference(RefSinDeclaracion, bagNumber),
                    LedgerEntryType.SinDeclaracion, r.TransactionNumber,
                    LedgerProcessing.Manual, r.Id));
            }

            return entries;
        }

        private static decimal Positive(decimal value) => value > 0 ? value : 0;

        private static decimal NegativeAsPositive(decimal value) => value < 0 ? -value : 0;

        private static List<T> ToList<T>(IEnumerable<T>? source)
        {
            var list = new List<T>();
            if (source == null)
            {
                return list;
            }

            foreach (var item in source)
            {
                list.Add(item);
            }

            return list;
        }
    }
}
