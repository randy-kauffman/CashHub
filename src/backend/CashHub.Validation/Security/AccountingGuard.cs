using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>
    /// Montos de un asiento o de un deposito, SIEMPRE por componente. El neto no
    /// existe en este dominio: sumar efectivo con cheques fue el bug historico del
    /// circuito (+200.000 de efectivo y -100.000 de cheques se compensaban a
    /// +100.000 y una discrepancia desaparecia).
    /// </summary>
    public struct AccountingAmounts : IEquatable<AccountingAmounts>
    {
        public AccountingAmounts(decimal cash, decimal bacCheck, decimal otherCheck)
        {
            Cash = cash;
            BacCheck = bacCheck;
            OtherCheck = otherCheck;
        }

        /// <summary>sanic_cashamount.</summary>
        public decimal Cash { get; }

        /// <summary>sanic_baclocalcheckamount.</summary>
        public decimal BacCheck { get; }

        /// <summary>
        /// Cheques de otros bancos. OJO con la ortografia real: en Transactions y
        /// DeclaredDeposits la columna es sanic_otherlocalcheckamount, pero en
        /// DepositsRecounting es sanic_otherloca<b>c</b>heckamount (falta la 'l').
        /// </summary>
        public decimal OtherCheck { get; }

        public bool IsZero => Cash == 0m && BacCheck == 0m && OtherCheck == 0m;

        /// <summary>
        /// SOBRANTE = solo los excedentes, componente a componente: Max(recontado -
        /// declarado, 0). Un componente que falta NO se compensa contra otro que sobra.
        /// </summary>
        public static AccountingAmounts Surplus(AccountingAmounts recounted, AccountingAmounts declared)
        {
            return new AccountingAmounts(
                Positive(recounted.Cash - declared.Cash),
                Positive(recounted.BacCheck - declared.BacCheck),
                Positive(recounted.OtherCheck - declared.OtherCheck));
        }

        /// <summary>
        /// FALTANTE = solo los defectos y en valor ABSOLUTO, componente a componente:
        /// Abs(Min(recontado - declarado, 0)).
        /// </summary>
        public static AccountingAmounts Shortfall(AccountingAmounts recounted, AccountingAmounts declared)
        {
            return new AccountingAmounts(
                NegativeAbs(recounted.Cash - declared.Cash),
                NegativeAbs(recounted.BacCheck - declared.BacCheck),
                NegativeAbs(recounted.OtherCheck - declared.OtherCheck));
        }

        private static decimal Positive(decimal delta) => delta > 0m ? delta : 0m;

        private static decimal NegativeAbs(decimal delta) => delta < 0m ? -delta : 0m;

        /// <summary>Igualdad EXACTA por componente: la tolerancia contable es cero.</summary>
        public bool Equals(AccountingAmounts other)
        {
            return Cash == other.Cash && BacCheck == other.BacCheck && OtherCheck == other.OtherCheck;
        }

        public override bool Equals(object? obj) => obj is AccountingAmounts other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Cash.GetHashCode();
                hash = (hash * 397) ^ BacCheck.GetHashCode();
                return (hash * 397) ^ OtherCheck.GetHashCode();
            }
        }

        /// <summary>Solo para trazas del plugin (nunca para el mensaje al usuario).</summary>
        public override string ToString()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "efectivo={0} bac={1} otros={2}", Cash, BacCheck, OtherCheck);
        }

        /// <summary>Nombres de negocio de los componentes que NO coinciden.</summary>
        public IEnumerable<string> Mismatches(AccountingAmounts other)
        {
            if (Cash != other.Cash) yield return "efectivo";
            if (BacCheck != other.BacCheck) yield return "cheques BAC";
            if (OtherCheck != other.OtherCheck) yield return "cheques de otros bancos";
        }
    }

    /// <summary>Insumos de un deposito (recontado o declarado) para el recalculo.</summary>
    public struct DepositFacts
    {
        public DepositFacts(AccountingAmounts amounts, int? currency)
        {
            Amounts = amounts;
            Currency = currency;
        }

        public AccountingAmounts Amounts { get; }

        /// <summary>sanic_currency (option set global sanic_chchcurrency).</summary>
        public int? Currency { get; }
    }

    /// <summary>
    /// CONTABILIDAD del circuito de aprobacion de recuentos (hallazgo H-02).
    ///
    /// Los asientos Sobrante / Faltante / 'Sin declaracion' / Deposito los arma hoy
    /// Power Fx dentro de CH-App-Supervisor: el servidor acepta cualquier monto que
    /// le manden. Con el rol de cajero y la Web API (o Excel) se fabrica un Sobrante
    /// de cualquier importe contra cualquier deposito, o se duplica un asiento ya
    /// generado, sin que nada lo note. Este dominio RECALCULA server-side lo que el
    /// asiento tendria que decir y bloquea todo lo demas; el plugin
    /// (GuardSupervisorAccountingPlugin) junta los insumos leyendo como sistema.
    ///
    /// La unidad de calculo es DEPOSITO x COMPONENTE — la misma que usan las guias
    /// 03 §8/§11 y las apps productoras. Nunca el neto.
    ///
    /// Semantica implementada (verificada contra guia 03 §11 y contra los DOS
    /// productores vivos: CH-App-Recounting Screen1 y CH-App-Supervisor Approvals):
    ///
    ///   Deposito         montos = los del deposito RECONTADO (aunque tenga declarado).
    ///   Sobrante         con declarado: Max(rec - dec, 0) por componente.
    ///                    sin declarado (recontado que nadie declaro): los del recontado.
    ///   Faltante         con declarado: Abs(Min(rec - dec, 0)) por componente.
    ///                    sin declarado anclado: prohibido (no hay contra que medir).
    ///   Sin declaracion  montos = los del recontado, y el recontado NO puede tener
    ///                    declaracion (si la tiene, el asiento correcto es
    ///                    Sobrante/Faltante — la etiqueta no puede esquivar la resta).
    ///
    /// Ademas: un solo asiento por (deposito recontado, tipo) — idempotencia — y la
    /// moneda del asiento tiene que ser la del deposito.
    /// </summary>
    public static class AccountingGuard
    {
        // Option set sanic_transactiontype de CH-Tbl-Transactions (verificado contra
        // solutions/unpacked/core/Entities/sanic_CHTblTransactions/Entity.xml).
        public const int TipoDeposito = 159460000;
        public const int TipoFaltante = 159460001;
        public const int TipoSobrante = 159460002;
        public const int TipoSinDeclaracion = 159460003;

        /// <summary>true si el tipo participa del circuito contable de recuentos.</summary>
        public static bool IsAccountingType(int? transactionType)
        {
            return transactionType == TipoDeposito
                || transactionType == TipoFaltante
                || transactionType == TipoSobrante
                || transactionType == TipoSinDeclaracion;
        }

        /// <summary>
        /// Create de una transaccion del circuito contable.
        /// </summary>
        /// <param name="transactionType">sanic_transactiontype del Target.</param>
        /// <param name="currency">sanic_currency del Target.</param>
        /// <param name="amounts">Montos por componente del Target (ausente = 0).</param>
        /// <param name="hasRecountingRef">true si el Target trae sanic_relateddepositrecounting con valor.</param>
        /// <param name="recounted">
        /// Deposito recontado apuntado por el lookup, leido como sistema. null solo si
        /// <paramref name="hasRecountingRef"/> es false.
        /// </param>
        /// <param name="declared">
        /// Deposito declarado enlazado al recontado (por cualquiera de los dos lados de
        /// la relacion). null si el recontado no tiene declaracion.
        /// </param>
        /// <param name="duplicateExists">
        /// true si YA existe una transaccion con el mismo (recuento, tipo).
        /// </param>
        /// <param name="unrecountedDeclaredCandidate">
        /// Camino SIN ancla: mejor candidato de deposito DECLARADO que nadie reconto y
        /// que respalda el asiento (par Faltante + Deposito de la guia 03 §11 9.b).
        /// null si no hay ninguno.
        /// </param>
        public static TransactionGuardResult EvaluateAccountingCreate(
            int? transactionType,
            int? currency,
            AccountingAmounts amounts,
            bool hasRecountingRef,
            DepositFacts? recounted,
            DepositFacts? declared,
            bool duplicateExists,
            DepositFacts? unrecountedDeclaredCandidate)
        {
            var isAccounting = IsAccountingType(transactionType);

            if (!isAccounting && !hasRecountingRef)
            {
                // Transaccion del circuito de digitacion (sin tipo contable y sin
                // recuento asociado): no es asunto de este guard.
                return TransactionGuardResult.Allow();
            }

            if (!isAccounting)
            {
                // Enlazada a un recuento pero SIN tipo: un asiento sin tipo es invisible
                // para la idempotencia y para el Core. No se crea.
                return TransactionGuardResult.Block(
                    "Una transaccion enlazada a un deposito recontado tiene que declarar su tipo " +
                    "(Deposito, Sobrante, Faltante o 'Sin declaracion').");
            }

            if (!hasRecountingRef)
            {
                return EvaluateWithoutAnchor(transactionType, currency, amounts, unrecountedDeclaredCandidate);
            }

            if (recounted == null)
            {
                return TransactionGuardResult.Block(
                    "No se pudo leer el deposito recontado del asiento; no se genera contabilidad " +
                    "sobre un recuento inexistente.");
            }

            if (duplicateExists)
            {
                return TransactionGuardResult.Block(
                    "Ese deposito recontado YA tiene un asiento de este tipo. Los asientos del " +
                    "recuento se generan una sola vez: recarga la aprobacion antes de reintentar.");
            }

            var deposit = recounted.Value;

            if (currency == null || currency != deposit.Currency)
            {
                return TransactionGuardResult.Block(
                    "La moneda del asiento no coincide con la del deposito recontado.");
            }

            AccountingAmounts expected;
            switch (transactionType)
            {
                case TipoDeposito:
                    // El asiento del deposito refleja lo RECONTADO (es lo que entra al
                    // banco), tenga o no declaracion. Verificado en los dos productores.
                    expected = deposit.Amounts;
                    break;

                case TipoSinDeclaracion:
                    if (declared != null)
                    {
                        return TransactionGuardResult.Block(
                            "El deposito recontado SI tiene declaracion: su diferencia se contabiliza " +
                            "como Sobrante y/o Faltante, no como 'Sin declaracion'.");
                    }

                    expected = deposit.Amounts;
                    break;

                case TipoSobrante:
                    expected = declared == null
                        ? deposit.Amounts                                        // recontado que nadie declaro
                        : AccountingAmounts.Surplus(deposit.Amounts, declared.Value.Amounts);

                    if (expected.IsZero)
                    {
                        return TransactionGuardResult.Block(
                            "No hay sobrante que contabilizar: el recuento no supera a lo declarado " +
                            "en ningun componente.");
                    }

                    break;

                case TipoFaltante:
                    if (declared == null)
                    {
                        return TransactionGuardResult.Block(
                            "Un Faltante exige un deposito declarado contra el cual medirlo; el " +
                            "recuento asociado no tiene declaracion.");
                    }

                    expected = AccountingAmounts.Shortfall(deposit.Amounts, declared.Value.Amounts);

                    if (expected.IsZero)
                    {
                        return TransactionGuardResult.Block(
                            "No hay faltante que contabilizar: el recuento no queda por debajo de lo " +
                            "declarado en ningun componente.");
                    }

                    break;

                default:
                    // Inalcanzable: IsAccountingType ya filtro los cuatro valores.
                    return TransactionGuardResult.Block(
                        "Tipo de transaccion no soportado por el circuito contable.");
            }

            if (!amounts.Equals(expected))
            {
                return TransactionGuardResult.Block(BuildMismatchReason(transactionType, amounts, expected));
            }

            return TransactionGuardResult.Allow();
        }

        /// <summary>
        /// Asiento SIN sanic_relateddepositrecounting. Unico caso legitimo (guia 03 §11
        /// 9.b y su equivalente en la app vieja): un deposito DECLARADO que nadie
        /// reconto genera el par Faltante + Deposito por el monto declarado, y no hay
        /// recuento al cual anclarlo porque el recuento no existe. Se valida contra el
        /// declarado sin recontar; sin respaldo, no se crea nada.
        /// </summary>
        private static TransactionGuardResult EvaluateWithoutAnchor(
            int? transactionType,
            int? currency,
            AccountingAmounts amounts,
            DepositFacts? unrecountedDeclaredCandidate)
        {
            if (transactionType == TipoSobrante || transactionType == TipoSinDeclaracion)
            {
                return TransactionGuardResult.Block(
                    "Un asiento de Sobrante o 'Sin declaracion' tiene que apuntar al deposito " +
                    "recontado que lo origina (RelatedDepositRecounting).");
            }

            if (unrecountedDeclaredCandidate == null)
            {
                return TransactionGuardResult.Block(
                    "El asiento no apunta a ningun deposito recontado y tampoco hay un deposito " +
                    "declarado sin recontar que lo respalde.");
            }

            var candidate = unrecountedDeclaredCandidate.Value;

            if (currency == null || currency != candidate.Currency)
            {
                return TransactionGuardResult.Block(
                    "La moneda del asiento no coincide con la del deposito declarado que lo respalda.");
            }

            if (!amounts.Equals(candidate.Amounts))
            {
                return TransactionGuardResult.Block(BuildMismatchReason(transactionType, amounts, candidate.Amounts));
            }

            return TransactionGuardResult.Allow();
        }

        /// <summary>
        /// Mensaje de bloqueo por montos. Nombra los componentes que no cuadran pero NO
        /// los importes esperados: el recalculo se hace leyendo como SISTEMA y el
        /// mensaje llega al usuario, que puede no tener privilegio sobre esos depositos
        /// (los numeros van a la traza del plugin, visible solo para administradores).
        /// </summary>
        private static string BuildMismatchReason(
            int? transactionType, AccountingAmounts amounts, AccountingAmounts expected)
        {
            var componentes = string.Join(", ", amounts.Mismatches(expected));
            if (componentes.Length == 0)
            {
                componentes = "los montos";
            }

            return string.Format(
                "El asiento de {0} no coincide con el recalculo del servidor en: {1}. Los montos " +
                "salen del recuento aprobado, no se escriben a mano: recarga la aprobacion y " +
                "volve a intentar.",
                TypeName(transactionType), componentes);
        }

        private static string TypeName(int? transactionType)
        {
            switch (transactionType)
            {
                case TipoDeposito: return "Deposito";
                case TipoFaltante: return "Faltante";
                case TipoSobrante: return "Sobrante";
                case TipoSinDeclaracion: return "'Sin declaracion'";
                default: return "transaccion";
            }
        }
    }
}
