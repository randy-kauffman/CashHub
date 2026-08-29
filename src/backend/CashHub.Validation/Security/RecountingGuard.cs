using System;
using System.Collections.Generic;

namespace CashHub.Validation.Security
{
    /// <summary>Que hacer con una solicitud "Crear sesion de recuento" recien creada.</summary>
    public enum RecountingAutoApproval
    {
        /// <summary>La solicitud no es de sesion de recuento: el guard no opina.</summary>
        NotApplicable = 0,

        /// <summary>La mesa referenciada es la del propio solicitante: se aprueba en el acto.</summary>
        AutoApprove = 1,

        /// <summary>Mesa ajena, sin cajero o actor no confiable: la firma un supervisor.</summary>
        LeavePending = 2
    }

    /// <summary>Resultado de evaluar una operacion del dominio de recuento.</summary>
    public sealed class RecountingGuardResult
    {
        private RecountingGuardResult(bool isAllowed, string? reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>true si la operacion es legitima; false si debe bloquearse.</summary>
        public bool IsAllowed { get; }

        /// <summary>Motivo del bloqueo (apto para mostrar al cajero). null cuando permite.</summary>
        public string? Reason { get; }

        public static RecountingGuardResult Allow() => new RecountingGuardResult(true, null);

        public static RecountingGuardResult Block(string reason) => new RecountingGuardResult(false, reason);
    }

    /// <summary>Montos de UN deposito de recuento, para el recalculo de totales de la tula.</summary>
    public sealed class DepositAmounts
    {
        /// <summary>sanic_currency (option set global sanic_chchcurrency). null = sin moneda.</summary>
        public int? Currency { get; set; }

        public decimal CashAmount { get; set; }
        public decimal BacCheckAmount { get; set; }

        /// <summary>sanic_otherlocacheckamount — el logical name real NO lleva la 'l' de "local".</summary>
        public decimal OtherCheckAmount { get; set; }

        /// <summary>Lo que aporta el deposito a su moneda (misma suma que la v1).</summary>
        public decimal Total => CashAmount + BacCheckAmount + OtherCheckAmount;
    }

    /// <summary>Totales recontados de la tula, por moneda.</summary>
    public sealed class BagTotals
    {
        public decimal Nio { get; set; }
        public decimal Usd { get; set; }
        public decimal Eur { get; set; }
    }

    /// <summary>Deposito de recuento con todo lo que necesita su asiento contable.</summary>
    public sealed class DepositForTransaction
    {
        /// <summary>PK del deposito. Es la CLAVE de idempotencia (RelatedDepositRecounting).</summary>
        public Guid DepositId { get; set; }

        public int? Currency { get; set; }
        public decimal CashAmount { get; set; }
        public decimal BacCheckAmount { get; set; }
        public decimal OtherCheckAmount { get; set; }

        /// <summary>sanic_accountnumber. Decimal porque asi esta tipada en Transactions.</summary>
        public decimal? AccountNumber { get; set; }

        /// <summary>DepositReference del deposito DECLARADO enlazado (null si no hay enlace).</summary>
        public string? DeclaredReference { get; set; }

        /// <summary>TransactionNo del deposito DECLARADO enlazado (null si no hay enlace).</summary>
        public int? DeclaredTransactionNumber { get; set; }
    }

    /// <summary>Asiento contable a crear en CH-Tbl-Transactions.</summary>
    public sealed class TransactionPlan
    {
        public Guid DepositId { get; set; }
        public int? Currency { get; set; }
        public decimal CashAmount { get; set; }
        public decimal BacCheckAmount { get; set; }
        public decimal OtherCheckAmount { get; set; }
        public decimal? AccountNumber { get; set; }
        public string? TransactionReference { get; set; }
        public int? TransactionNumber { get; set; }
        public int TransactionType { get; set; }
        public int ProcessingType { get; set; }
    }

    /// <summary>
    /// Guard server-side de CH-App-Recounting v2 — la contraparte de seguridad del
    /// rebuild (docs/recounting-rebuild/00-analisis-y-plan.md §2.2, puntos 1-6).
    ///
    /// La v1 decidia TODO desde el dispositivo del cajero, en Power Fx:
    ///
    ///   S-1  se auto-aprobaba la sesion escribiendo ApprovalStatus = Aprobado en el
    ///        propio Create (Form1.OnSuccess). Con gvarUser en blanco, el LookUp de
    ///        mesa devolvia la primera mesa SIN cajero y cualquiera entraba aprobado.
    ///   S-2a los TotalNIO/USD/EUR de la tula salian de sumar colecciones EN MEMORIA
    ///        (colRecountedDeposits) — es decir, de lo que el cliente hubiera llegado
    ///        a materializar, con el tope de filas de Power Fx truncando SIN error.
    ///   S-2b los asientos de CH-Tbl-Transactions los creaba un ForAll sobre esa misma
    ///        coleccion: la contabilidad del banco, armada client-side.
    ///   S-5  depositos, cheques y denominaciones se escribian sin que nadie verificara
    ///        que la tula estuviera efectivamente asignada a ese cajero.
    ///
    /// Todo eso es cosmetico mientras viva en el canvas: cualquier usuario con el rol
    /// lo reproduce por Web API o desde Excel. Este tipo es la DECISION pura (sin SDK);
    /// el acceso a Dataverse vive en RecountingSessionAutoApprovePlugin,
    /// BagRecountClosurePlugin y GuardRecountingPlugin.
    ///
    /// Lo que NO vive aca: la matriz de transiciones de BagStatus (es de
    /// <see cref="ReceptionGuard"/>, que ya la impone) y la maquina de estados del
    /// cheque (es de <see cref="CheckGuard"/>). Duplicarlas seria tener dos verdades.
    /// </summary>
    public static class RecountingGuard
    {
        /// <summary>
        /// Tipo de solicitud de apertura de sesion. TEXTO LIBRE y comparacion ORDINAL
        /// exacta (01 §5.6): una variante de tipeo NO invoca la excepcion.
        /// </summary>
        public const string RequestTypeSesionRecuento = ReceptionGuard.ApprovalTypeSesionRecuento;

        /// <summary>
        /// Tipo legado que la v2 ya no produce ni espera (ledger L-6). Vive aca porque
        /// el GC de solicitudes pendientes de ese tipo (00 §2.2 punto 7) lo necesita.
        /// </summary>
        public const string RequestTypePrimeraTula = "Primera tula en planilla";

        // Option set global sanic_chchcurrency (verificado contra OptionSets/).
        public const int CurrencyNio = 159460000;
        public const int CurrencyUsd = 159460001;
        public const int CurrencyEur = 159460002;

        // Option sets de CH-Tbl-Transactions (verificados contra su Entity.xml).
        public const int TransactionTypeDeposito = 159460000;
        public const int ProcessingTypeManual = 159460000;
        public const int ProcessingTypeBatch = 159460001;

        /// <summary>Estado con el que el cierre de tula marca los cheques (04 §11.3, paso 2).</summary>
        public const int CheckStatusRecontado = CheckGuard.Recontado;

        /// <summary>
        /// Mensaje del Create de solicitud de sesion cuyo RequestResources no deja leer
        /// la sesion. ESPECIFICO a proposito: el cajero tiene que poder decir QUE fallo.
        /// </summary>
        public const string UnreadableResourcesMessage =
            "La solicitud 'Crear sesion de recuento' no trae un RecountingSessionGUID legible " +
            "en RequestResources: sin la sesion no se puede verificar la mesa ni resolver la " +
            "solicitud. Vuelva a pedir la mesa desde la app de recuento.";

        /// <summary>Mensaje del Create cuya sesion referenciada no existe (o no se puede leer).</summary>
        public static string SessionNotFoundMessage(Guid sessionId) =>
            $"La sesion de recuento {sessionId} referenciada por la solicitud no existe o no se " +
            "pudo leer: la solicitud quedaria apuntando a la nada. Vuelva a pedir la mesa desde " +
            "la app de recuento.";

        // =================================================================
        // 1. Auto-aprobacion de sesion por mesa propia (mata S-1)
        // =================================================================

        /// <summary>
        /// Atajo de negocio deliberado: si la mesa que pide el cajero es LA SUYA, la
        /// solicitud se resuelve sola. La diferencia con la v1 no es el atajo — es
        /// QUIEN lo evalua: aca el cajero de la mesa se lee server-side (como sistema,
        /// navegando la sesion referenciada) y se compara contra el InitiatingUserId
        /// del contexto de ejecucion, que el cliente no puede falsear.
        ///
        /// Cualquier duda deja la solicitud PENDIENTE (nunca la bloquea): la solicitud
        /// en si es legitima, lo que no se pudo probar es el atajo.
        /// </summary>
        /// <param name="requestType">sanic_requesttype tal cual viene (comparacion ordinal).</param>
        /// <param name="deskCashierId">
        /// sanic_cajero de la mesa de la sesion referenciada, leido como sistema.
        /// null o <see cref="Guid.Empty"/> = mesa sin cajero (no verificable).
        /// </param>
        /// <param name="initiatingUserId">Usuario real que dispara la creacion.</param>
        public static RecountingAutoApproval ShouldAutoApprove(
            string? requestType, Guid? deskCashierId, Guid initiatingUserId)
        {
            if (!string.Equals(requestType, RequestTypeSesionRecuento, StringComparison.Ordinal))
            {
                return RecountingAutoApproval.NotApplicable;
            }

            if (initiatingUserId == Guid.Empty)
            {
                return RecountingAutoApproval.LeavePending;
            }

            if (!deskCashierId.HasValue || deskCashierId.Value == Guid.Empty)
            {
                return RecountingAutoApproval.LeavePending;
            }

            return deskCashierId.Value == initiatingUserId
                ? RecountingAutoApproval.AutoApprove
                : RecountingAutoApproval.LeavePending;
        }

        // =================================================================
        // 2. Totales de la tula recalculados server-side (mata S-2a)
        // =================================================================

        /// <summary>
        /// Los totales se recalculan cuando la tula ENTRA a un estado de cierre:
        /// Recontada (cierre limpio) o Pendiente de revision por supervisor - recuento
        /// (cierre con discrepancias). Son los dos unicos momentos en que la v1 los
        /// escribia, y siguen siendo los dos unicos en que el numero cambia de verdad.
        ///
        /// Se exige cambio REAL de estado: Dataverse dispara un paso filtrado aunque se
        /// reescriba el mismo valor, y recalcular por un update sin cambio es pagar N
        /// lecturas de deposito por nada.
        /// </summary>
        public static bool ShouldRecomputeTotals(int? previousStatus, bool statusInTarget, int? newStatus)
        {
            if (!statusInTarget || !newStatus.HasValue || newStatus == previousStatus)
            {
                return false;
            }

            return newStatus.Value == ReceptionGuard.BagRecontada
                   || newStatus.Value == ReceptionGuard.BagPendienteSupervisorRecuento;
        }

        /// <summary>
        /// Totales por moneda desde los depositos PERSISTIDOS de la tula.
        ///
        /// 🧨 Es la UNICA fuente: no hay parametro por donde entre un valor del cliente.
        /// El plugin pisa lo que venga en el Target con esto. La v1 sumaba
        /// colRecountedDeposits —una coleccion en memoria del dispositivo del cajero—
        /// y Power Fx la materializaba hasta el tope del cliente (500/2000) truncando
        /// SIN error: la tula podia cerrarse declarando menos plata de la que tenia.
        ///
        /// Una tula sin depositos da CERO EXPLICITO en las tres monedas, no "sin
        /// cambio": un total viejo sobre una tula vaciada es plata fantasma.
        /// </summary>
        public static BagTotals ComputeTotals(IEnumerable<DepositAmounts>? deposits)
        {
            var totals = new BagTotals();

            if (deposits == null)
            {
                return totals;
            }

            foreach (var deposit in deposits)
            {
                if (deposit == null || !deposit.Currency.HasValue)
                {
                    continue;
                }

                switch (deposit.Currency.Value)
                {
                    case CurrencyNio:
                        totals.Nio += deposit.Total;
                        break;
                    case CurrencyUsd:
                        totals.Usd += deposit.Total;
                        break;
                    case CurrencyEur:
                        totals.Eur += deposit.Total;
                        break;
                    default:
                        // Moneda fuera del option set: no se inventa a que columna va.
                        break;
                }
            }

            return totals;
        }

        // =================================================================
        // 3. Asientos contables server-side + idempotencia (mata S-2b)
        // =================================================================

        /// <summary>
        /// Los asientos se crean SOLO al entrar a Recontada — el cierre limpio. La tula
        /// que va a revision del supervisor NO genera contabilidad: la decision todavia
        /// no esta tomada (misma rama que la v1).
        /// </summary>
        public static bool ShouldBuildTransactions(int? previousStatus, bool statusInTarget, int? newStatus)
        {
            return statusInTarget
                   && newStatus.HasValue
                   && newStatus.Value == ReceptionGuard.BagRecontada
                   && newStatus != previousStatus;
        }

        /// <summary>
        /// Un asiento por deposito de la tula, con la MISMA semantica de la v1
        /// (dialog_ConfirmarTerminarTula.OnButtonSelect): cuenta, moneda y los tres
        /// montos se copian tal cual; la referencia y el numero de transaccion salen
        /// del deposito DECLARADO enlazado (en blanco si no hay enlace, que es la ruta
        /// "tula sin declaracion" y no es un error); el tipo es Deposito y el
        /// procesamiento es Manual cuando hay cheques y Batch cuando no.
        ///
        /// 🧨 IDEMPOTENCIA: la tula puede RE-transicionar a Recontada (el supervisor
        /// rechaza, el cajero recuenta y cierra otra vez). Sin
        /// <paramref name="alreadyPosted"/> cada vuelta duplicaria la plata en el Core.
        ///
        /// <paramref name="alreadyPosted"/> son los depositos que ya tienen un asiento
        /// DE TIPO Deposito — la MISMA clave (recuento, tipo) con la que
        /// <see cref="AccountingGuard.EvaluateAccountingCreate"/> rechaza duplicados.
        /// Con la clave solo por deposito, un recuento que ya tiene un Sobrante o un
        /// Faltante del circuito del supervisor se leeria como "ya asentado" y su
        /// asiento de Deposito no se crearia jamas.
        ///
        /// Un deposito sin PK se descarta: su asiento no seria verificable nunca mas.
        /// </summary>
        public static IReadOnlyList<TransactionPlan> BuildTransactions(
            IEnumerable<DepositForTransaction>? deposits,
            ISet<Guid>? alreadyPosted)
        {
            var plans = new List<TransactionPlan>();

            if (deposits == null)
            {
                return plans;
            }

            foreach (var deposit in deposits)
            {
                if (deposit == null || deposit.DepositId == Guid.Empty)
                {
                    continue;
                }

                if (alreadyPosted != null && alreadyPosted.Contains(deposit.DepositId))
                {
                    continue;
                }

                var hasChecks = deposit.BacCheckAmount > 0m || deposit.OtherCheckAmount > 0m;

                plans.Add(new TransactionPlan
                {
                    DepositId = deposit.DepositId,
                    Currency = deposit.Currency,
                    CashAmount = deposit.CashAmount,
                    BacCheckAmount = deposit.BacCheckAmount,
                    OtherCheckAmount = deposit.OtherCheckAmount,
                    AccountNumber = deposit.AccountNumber,
                    TransactionReference = deposit.DeclaredReference,
                    TransactionNumber = deposit.DeclaredTransactionNumber,
                    TransactionType = TransactionTypeDeposito,
                    ProcessingType = hasChecks ? ProcessingTypeManual : ProcessingTypeBatch,
                });
            }

            return plans;
        }

        // =================================================================
        // 4. Gate de escritura de los hijos del recuento (mata S-5)
        // =================================================================

        /// <summary>
        /// Excepciones del gate, resueltas ANTES de leer nada (por eso son un predicado
        /// aparte: un exento no paga la resolucion de la tula ancestro).
        ///
        ///   (a) <paramref name="depth"/> &gt; 1 — lo escribe OTRO plugin: los espejos
        ///       E-1/E-2/E-4 y sus cascadas, y el propio guard. El actor humano y las
        ///       apps canvas SIEMPRE entran con depth 1.
        ///   (b) <paramref name="touchesOnlyCheckStatus"/> — el marcado de cheques a
        ///       Recontado del cierre de tula (04 §11.3, paso 2), que corre DESPUES de
        ///       mover la bolsa a Recontada. Ver la nota de arriba de este metodo.
        ///   (c) <paramref name="touchesOnlyMirror"/> — escritura PURA de columnas
        ///       espejo derivadas. Los BACKFILLS (backfill-recounting-mirrors.py,
        ///       backfill-tracker.py) las estampan por Web API, o sea con depth 1 y
        ///       sobre el HISTORICO ENTERO: tulas en boveda, recontadas, de todo. Sin
        ///       esta excepcion el gate voltea el backfill completo. Es metadata
        ///       derivada, no una edicion de negocio — mismo racional y mismo nombre
        ///       que touchesOnlyMirror de <see cref="CheckGuard"/> y
        ///       <see cref="ReceptionGuard"/>.
        ///
        /// NO hay excepcion por usuario (SP de despliegue, seeder ni rol): el repo no
        /// la tiene en ningun guard. El seeder de estres respeta el guard por ORDEN
        /// (escribe los hijos con la tula en 'Asignada a cajero' y recien despues la
        /// cierra) y, cuando eso es imposible —el --clean, que borra hacia atras—,
        /// desactiva TEMPORALMENTE los pasos Delete concretos, igual que hace
        /// seed-reception-stress.py con GuardReceptionPlugin. Una excepcion por
        /// identidad seria una llave maestra permanente para matar un problema de
        /// scripts que ya tiene solucion acotada y auditable.
        /// </summary>
        /// <param name="message">Create / Update / Delete.</param>
        /// <param name="depth">context.Depth.</param>
        /// <param name="touchesOnlyCheckStatus">
        /// true si el request es un Update de CH-Tbl-Checks que escribe UNICAMENTE
        /// sanic_checkstatus. 🧨 El orden de 04 §11.3 es deliberado: primero la bolsa
        /// (si ese Patch falla, los cheques NO se tocan), despues los cheques. Con la
        /// bolsa ya en Recontada, el gate voltearia el cierre limpio de la app. Quien
        /// decide si la transicion de estado es legal sigue siendo <see cref="CheckGuard"/>,
        /// que ve ese mismo Update: esta excepcion no afloja el handshake, solo dice
        /// que el gate de recuento no es quien tiene que opinar.
        /// </param>
        /// <param name="touchesOnlyMirror">
        /// true si el request escribe UNICAMENTE columnas espejo derivadas del registro
        /// (E-1 sanic_recountingsessionmirror en el deposito; E-2 sanic_bag y B-9
        /// sanic_recountingsession en el cheque). Nunca junto con datos de negocio.
        /// </param>
        public static bool IsWriteExempt(
            string? message, int depth, bool touchesOnlyCheckStatus, bool touchesOnlyMirror)
        {
            if (depth > 1)
            {
                return true;
            }

            // Las dos excepciones son formas de UPDATE. En Create o Delete la bandera
            // seria el llamador armando mal el contexto: no se le concede nada.
            if (!string.Equals(message, "Update", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return touchesOnlyCheckStatus || touchesOnlyMirror;
        }

        /// <summary>
        /// Los hijos del recuento —CH-Tbl-DepositsRecountings, CH-Tbl-Checks y
        /// CH-Tbl-CountedMoneyDetails— solo se escriben con la tula ancestro
        /// efectivamente EN LA MESA ('Asignada a cajero'). Todo lo demas se rechaza:
        /// una tula en boveda que recibe depositos es plata que aparece de la nada, y
        /// una tula ya Recontada que recibe cheques es plata que se agrega DESPUES de
        /// que la contabilidad se asento.
        ///
        /// Fail-closed en los dos bordes: sin tula resoluble y sin estado, se bloquea.
        /// </summary>
        /// <param name="isExempt">Resultado de <see cref="IsWriteExempt"/>.</param>
        /// <param name="bagResolved">true si se pudo llegar a la tula ancestro.</param>
        /// <param name="bagStatus">sanic_bagstatus de la tula ancestro.</param>
        public static RecountingGuardResult IsWriteAllowed(bool isExempt, bool bagResolved, int? bagStatus)
        {
            if (isExempt)
            {
                return RecountingGuardResult.Allow();
            }

            if (!bagResolved)
            {
                return RecountingGuardResult.Block(
                    "No se pudo resolver la tula del registro de recuento: sin saber en que estado " +
                    "esta la tula no se puede autorizar la escritura.");
            }

            if (bagStatus == ReceptionGuard.BagAsignadaACajero)
            {
                return RecountingGuardResult.Allow();
            }

            return RecountingGuardResult.Block(
                "Solo se puede registrar el recuento de una tula que este 'Asignada a cajero'. " +
                "Abra la tula desde la app de recuento antes de cargar depositos, cheques o " +
                "denominaciones.");
        }

        // =================================================================
        // 5. Bajas acotadas (D02-4 / B02-3)
        // =================================================================

        /// <summary>
        /// El cajero retira SU solicitud mientras sigue en la cola ("Cancelar solicitud",
        /// 02 §9.2). "Propia" es <c>createdby</c>: es el unico dato de autoria que la
        /// solicitud tiene (no hay lookup de cajero en la tabla), y es exactamente el
        /// mismo campo con el que <see cref="ReceptionGuard.EvaluateApprovalUpdate"/>
        /// impone la separacion creador/resolutor.
        ///
        /// Una solicitud YA RESUELTA no se borra ni siendo propia: es la evidencia de
        /// una decision del supervisor (D02-5 — desde A3, "Elegir otra mesa" saltea el
        /// borrado justamente por esto y da de baja solo la sesion).
        /// </summary>
        public static RecountingGuardResult EvaluateApprovalDelete(
            Guid creatorId, Guid actorId, int? approvalStatus)
        {
            if (creatorId == Guid.Empty)
            {
                return RecountingGuardResult.Block(
                    "No se pudo determinar el creador de la solicitud; se bloquea el borrado por seguridad.");
            }

            if (creatorId != actorId)
            {
                return RecountingGuardResult.Block(
                    "Solo quien creo la solicitud puede retirarla de la cola del supervisor.");
            }

            if (approvalStatus != null)
            {
                return RecountingGuardResult.Block(
                    "La solicitud ya fue resuelta por un supervisor: queda como registro de esa " +
                    "decision y no se elimina.");
            }

            return RecountingGuardResult.Allow();
        }

        /// <summary>
        /// El cajero libera SU mesa dando de baja la sesion sin confirmar ("Cancelar
        /// solicitud" / "Elegir otra mesa", 02 §9.2).
        ///
        /// "Propia" es <c>sanic_cashier</c>, NO createdby: 01 §2.4 lo declara requerido
        /// y escrito explicitamente con el usuario al crear la sesion, y 00 §2.2 punto 5
        /// lo nombra literalmente ("<c>Cashier</c> = usuario"). Es ademas el mismo campo
        /// que GuardReceptionPlugin ya lee para verificar la mesa propia, asi que las
        /// dos mitades del circuito miran la MISMA columna. createdby podria ser un SP
        /// o una impersonacion y no dice de quien es la mesa.
        ///
        /// Se bloquea la sesion confirmada (hay recuento real colgando) y la referenciada
        /// por una solicitud ya resuelta (dejaria la decision del supervisor apuntando
        /// a la nada).
        /// </summary>
        /// <param name="actorIsApprover">
        /// El actor pertenece al grupo aprobador de supervisores. Le levanta DOS
        /// restricciones que existen para proteger al cajero de si mismo, no de su
        /// supervisor: la propiedad de la mesa y el congelamiento por solicitud ya
        /// resuelta. Esta ultima es imprescindible: al rechazar una solicitud "Crear
        /// sesion de recuento", el borrado de la sesion ES el efecto de esa decision
        /// — bloquearlo por "hay una decision que apunta a esto" es circular, y era
        /// lo que dejaba la sesion huerfana e imborrable para todos.
        ///
        /// NO le levanta la confirmacion: esa frontera es dura para todos.
        /// </param>
        public static RecountingGuardResult EvaluateSessionDelete(
            Guid cashierId, Guid actorId, bool confirmed, bool hasResolvedApproval,
            bool actorIsApprover = false)
        {
            if (cashierId == Guid.Empty)
            {
                return RecountingGuardResult.Block(
                    "La sesion de recuento no tiene cajero: no se puede verificar de quien es la " +
                    "mesa y se bloquea el borrado por seguridad.");
            }

            // La confirmacion se evalua PRIMERO, antes que la propiedad, porque es la
            // unica restriccion que no admite excepcion: hay recuento real colgando.
            // Si fuera despues, un cajero ajeno leeria "no es tuya" y saldria a pedirle
            // el borrado al dueno — que tampoco puede. El motivo tiene que ser el real.
            if (confirmed)
            {
                return RecountingGuardResult.Block(
                    "La sesion ya esta confirmada y la mesa esta en uso: no se elimina. " +
                    "Pida al supervisor el cierre forzado de la sesion.");
            }

            if (cashierId != actorId && !actorIsApprover)
            {
                return RecountingGuardResult.Block(
                    "Solo el cajero dueno de la sesion puede darla de baja.");
            }

            if (hasResolvedApproval && !actorIsApprover)
            {
                return RecountingGuardResult.Block(
                    "La sesion esta referenciada por una solicitud que el supervisor ya resolvio: " +
                    "borrarla dejaria esa decision apuntando a la nada.");
            }

            return RecountingGuardResult.Allow();
        }
    }
}
