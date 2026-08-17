namespace CashHub.Plugins
{
    /// <summary>Nombres lógicos y valores de option sets del esquema CashHub.</summary>
    internal static class Schema
    {
        internal static class ReceivedEmail
        {
            public const string LogicalName = "sanic_chtblreceivedemails";
            public const string Id = "sanic_chtblreceivedemailsid";
            public const string SenderAddress = "sanic_sourceemailaddress";
            public const string Subject = "sanic_subject";
            public const string ValidationStatus = "sanic_validationstatus";
            public const string ReviewStatus = "sanic_emailreviewstatus";
            public const string ReviewMethod = "sanic_emailreviewmethod";
            public const string ErrorText = "sanic_errortext";
            public const string ClientLookup = "sanic_client";
            public const string TrackingName = "sanic_name";
            public const string ReplyStatus = "sanic_emailreplystatus";
            public const string ReplySubject = "sanic_replysubject";
            public const string ReplyBodyHtml = "sanic_replybodyhtml";
            public const string SenderClassification = "sanic_senderclassification";
            public const string TriageNotes = "sanic_triagenotes";
            public const string DeclaredCif = "sanic_declaredcif";

            // Triage del remitente. Solo Registrado habilita que salga correo del banco.
            public const int SenderRegistrado = 159460000;
            public const int SenderClienteSinRegistrar = 159460001;
            public const int SenderSospechoso = 159460002;
            public const int SenderNoCorresponde = 159460003;

            public const int ReplyPendiente = 159460000;
            public const int ReplyRespondido = 159460001;
            public const int ReplyOmitido = 159460002;

            public const int ValidationPendiente = 159460000;
            public const int ValidationValido = 159460001;
            public const int ValidationInvalido = 159460002;

            public const int ReviewPendiente = 159460000;
            public const int ReviewAceptado = 159460001;
            public const int ReviewOmitido = 159460002;
            public const int ReviewManual = 159460003;
            public const int ReviewError = 159460004;
            public const int ReviewDenegado = 159460005;

            public const int MethodAuto = 159460001;
        }

        internal static class Attachment
        {
            public const string LogicalName = "sanic_chtblemailsattachments";
            public const string EmailLookup = "sanic_email";
            public const string FileName = "sanic_filename";
            public const string SizeBytes = "sanic_tamanoenbytes";
            public const string FileContent = "sanic_filecontent";
        }

        internal static class Rule
        {
            public const string LogicalName = "sanic_validationrules";
            public const string Id = "sanic_validationrulesid";
            public const string Key = "sanic_validationname";
            public const string Name = "sanic_name";
            public const string Order = "sanic_validationorder";
            public const string IsActive = "sanic_isactive";
            public const string Value = "sanic_validationvalue";
            public const string FailMessage = "sanic_validationfailmessage";
            public const string Dependency = "sanic_validationdependency";
        }

        internal static class Run
        {
            public const string LogicalName = "sanic_chtblvalidationruns";
            public const string Name = "sanic_name";
            public const string Start = "sanic_runstart";
            public const string End = "sanic_runend";
            public const string Result = "sanic_runresult";
            public const string SourceEntity = "sanic_sourceentity";
            public const string SourceRecordId = "sanic_sourcerecordid";

            /// <summary>
            /// Vinculo real al correo. SourceRecordId se conserva porque la tabla es
            /// generica, pero un GUID en texto no se puede delegar: sin este lookup la
            /// app se trae 500 corridas y filtra en el cliente, y a partir de la 501
            /// deja de encontrar las nuevas SIN dar error.
            /// </summary>
            public const string ReceivedEmailLookup = "sanic_ReceivedEmail";
            public const string SourceType = "sanic_sourcetype";

            public const int ResultPass = 159460000;
            public const int ResultFail = 159460001;
            public const int SourceSharedMailbox = 159460000;
        }

        internal static class Result
        {
            public const string LogicalName = "sanic_chtblvalidationresults";
            public const string Name = "sanic_name";
            public const string RuleLookup = "sanic_validationrule";
            public const string RunLookup = "sanic_validationrun";
            public const string Outcome = "sanic_validationresult";
            public const string DurationMs = "sanic_durationms";
            public const string Sheet = "sanic_sheet";
            public const string CellRef = "sanic_cellref";
            public const string RowNumber = "sanic_rownumber";
            public const string Detail = "sanic_detail";
            public const string SourceType = "sanic_sourcetype";

            public const int OutcomePass = 159460000;
            public const int OutcomeFail = 159460001;

            /// <summary>
            /// Regla omitida por dependencia (la madre fallo). Antes los Skipped no
            /// se persistian y el tracker no podia mostrar el cuadro completo de las
            /// 28 reglas; el valor se agrego al option set junto con este codigo.
            /// </summary>
            public const int OutcomeOmitida = 159460002;
        }

        internal static class ClientEmail
        {
            public const string LogicalName = "sanic_chtblclientemail";
            public const string Address = "sanic_name";
        }

        internal static class CustomApi
        {
            public const string MessageName = "sanic_CHValidateReceivedEmail";
            public const string InReceivedEmailId = "ReceivedEmailId";
            public const string OutRunId = "RunId";
            public const string OutResult = "Result";
            public const string OutFailedFindings = "FailedFindings";
        }

        /// <summary>Solicitud de cambio de datos de cliente (RFC).</summary>
        internal static class ChangeRequest
        {
            public const string LogicalName = "sanic_chtblclientdatachangerequest";
            public const string Id = "sanic_chtblclientdatachangerequestid";
            public const string ChangeStatus = "sanic_changestatus";
            public const string TargetTable = "sanic_targettable";
            public const string CreatedBy = "createdby";

            /// <summary>Pre-image registrada con sanic_changestatus, sanic_targettable y createdby (solo Update).</summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Permiso de cambio de datos de cliente: mapea tabla objetivo -> grupo AAD por tipo.</summary>
        internal static class ChangePermission
        {
            public const string LogicalName = "sanic_chtblclientdatachangepermission";
            public const string TargetTable = "sanic_name";
            public const string Type = "sanic_type";
            public const string AadGroupId = "sanic_aadgroupid";

            public const int TypeContributor = 159460000;
            public const int TypeApprover = 159460001;
        }

        /// <summary>Catalogo de apps del hub (CH-App-Hub). El hub lanza AppUrl tal cual.</summary>
        internal static class AppCatalog
        {
            public const string LogicalName = "sanic_chtblappcatalog";
            public const string Id = "sanic_chtblappcatalogid";
            public const string AppUrl = "sanic_appurl";
            public const string AadGroupId = "sanic_aadgroupid";
        }

        /// <summary>Acceso por grupo AAD a una app del hub: una fila = (app, grupo).</summary>
        internal static class AppCatalogAccess
        {
            public const string LogicalName = "sanic_chtblappcatalogaccess";
            public const string Id = "sanic_chtblappcatalogaccessid";
            public const string AppCatalogLookup = "sanic_appcatalog";
            public const string AadGroupId = "sanic_aadgroupid";
        }

        /// <summary>Lote de recepcion (CH-Tbl-ReceptionBatches).</summary>
        internal static class ReceptionBatch
        {
            public const string LogicalName = "sanic_chtblreceptionbatch";
            public const string Id = "sanic_chtblreceptionbatchid";
            public const string ReceptionStatus = "sanic_receptionstatus";

            /// <summary>
            /// Totales DECLARADOS del lote, materializados por
            /// ReceptionBatchTotalsPlugin sumando las planillas (aggregate
            /// server-side). Existen para que el tracker muestre montos del lote
            /// sin sumar client-side (cap de filas). "No editar a mano."
            /// </summary>
            public const string DecNioCash = "sanic_decniocash";
            public const string DecNioCheck = "sanic_decniocheck";
            public const string DecNioTotal = "sanic_decniototal";
            public const string DecUsdCash = "sanic_decusdcash";
            public const string DecUsdCheck = "sanic_decusdcheck";
            public const string DecUsdTotal = "sanic_decusdtotal";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Planilla de recepcion (CH-Tbl-Receptions).</summary>
        internal static class Reception
        {
            public const string LogicalName = "sanic_chtblreceptions";
            public const string Id = "sanic_chtblreceptionsid";
            public const string BatchLookup = "sanic_receptionbatch";

            /// <summary>
            /// Espejo entero del sanic_receptionstatus del lote, mantenido por
            /// ReceptionBatchStatusMirrorPlugin. Existe para que las apps filtren
            /// planillas por estado de lote con delegacion (un Filter sobre columna
            /// de la entidad relacionada no delega y miente a partir de 500 filas).
            /// </summary>
            public const string BatchStatusMirror = "sanic_batchstatus";

            /// <summary>Montos declarados de la planilla (vienen del papel).</summary>
            public const string NioCash = "sanic_niocash";
            public const string NioCheck = "sanic_niocheck";
            public const string UsdCash = "sanic_usdcash";
            public const string UsdCheck = "sanic_usdcheck";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Tula (CH-Tbl-Bags).</summary>
        internal static class Bag
        {
            public const string LogicalName = "sanic_chtblbags";
            public const string Id = "sanic_chtblbagsid";
            public const string BagStatus = "sanic_bagstatus";
            public const string BagId = "sanic_bagid";
            public const string Name = "sanic_name";
            public const string ReceptionLookup = "sanic_receptionid";
            public const string DeclaredBagLookup = "sanic_bagdeclarationid";

            /// <summary>
            /// Sesion de recuento a la que pertenece la tula. Columna de NEGOCIO (la
            /// escribe la app Recounting), no un espejo: es el ORIGEN del espejo B-9
            /// sanic_recountingsession del cheque.
            /// </summary>
            public const string RecountingSessionLookup = "sanic_recountingsession";

            /// <summary>Totales recontados (los estampa la app Recounting).</summary>
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";

            /// <summary>
            /// Conciliacion materializada por BagReconciliationMirrorPlugin: copia de
            /// los totales del DeclaredBag + bit de diferencia delegable para la
            /// grilla del tracker. "No editar a mano."
            /// </summary>
            public const string DeclTotalNio = "sanic_decltotalnio";
            public const string DeclTotalUsd = "sanic_decltotalusd";
            public const string DeclTotalEur = "sanic_decltotaleur";
            public const string HasDifference = "sanic_hasdifference";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Solicitud de aprobacion de esclusa/recuento (CH-Tbl-ApprovalRequests).</summary>
        internal static class ApprovalRequest
        {
            public const string LogicalName = "sanic_chtblapprovalrequest";
            public const string Id = "sanic_chtblapprovalrequestid";
            public const string ApprovalStatus = "sanic_approvalstatus";
            public const string RequestType = "sanic_requesttype";
            public const string CreatedBy = "createdby";

            /// <summary>Supervisor (o el plugin de auto-aprobacion) que resolvio. Lookup a systemuser.</summary>
            public const string SolvedBy = "sanic_solvedby";

            /// <summary>Fecha/hora de la resolucion.</summary>
            public const string ResolutionDatetime = "sanic_resolutiondatetime";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Transaccion (CH-Tbl-Transactions).</summary>
        internal static class Transaction
        {
            public const string LogicalName = "sanic_chtbltransactions";
            public const string Id = "sanic_chtbltransactionsid";
            public const string TransactionBatch = "sanic_transactionbatch";
            public const string RelatedDepositRecounting = "sanic_relateddepositrecounting";
            public const string AccountNumber = "sanic_accountnumber";
            public const string TransactionStatus = "sanic_transactionstatus";
            public const string ProcessingType = "sanic_transactionprocessingtype";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";
            public const string OtherCheckAmount = "sanic_otherlocalcheckamount";
            public const string CreatedOn = "createdon";
            public const string CreatedBy = "createdby";
            public const string CashAmount = "sanic_cashamount";
            public const string Currency = "sanic_currency";
            public const string TransactionType = "sanic_transactiontype";
            public const string TransactionNumber = "sanic_transactionnumber";

            /// <summary>
            /// Referencia del deposito DECLARADO enlazado (DeclaredDeposit.DepositReference).
            /// La copia el asiento al crearse; en blanco en la ruta "tula sin declaracion".
            /// </summary>
            public const string TransactionReference = "sanic_transactionreference";

            /// <summary>
            /// Total CALCULADO (SourceType 3): ValidForCreateApi/UpdateApi = 0, la
            /// plataforma no lo deja escribir. Esta en la lista de columnas de dinero
            /// del guard igual, por si alguna vez se convierte en editable.
            /// </summary>
            public const string Total = "sanic_total";

            /// <summary>
            /// Columnas de DINERO de la transaccion. Ninguna se toca una vez que la
            /// transaccion esta Aplicada o su lote entro al Core (H-04).
            /// OJO: sanic_accountnumber tiene IsSecured=1 (field security).
            /// </summary>
            public static readonly string[] MoneyAttributes =
            {
                CashAmount,
                BacCheckAmount,
                OtherCheckAmount,
                AccountNumber,
                Total,
            };

            // Option set global sanic_chchcurrency (verificado contra OptionSets/sanic_chchcurrency.xml).
            public const int CurrencyNio = 159460000;
            public const int CurrencyUsd = 159460001;
            public const int CurrencyEur = 159460002;

            /// <summary>Espejo del estado del lote, mantenido por TransactionBatchStatusMirrorPlugin.</summary>
            public const string BatchStatusMirror = "sanic_batchstatus";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Cheque (CH-Tbl-Checks).</summary>
        internal static class Check
        {
            public const string LogicalName = "sanic_chtblchecks";
            public const string Id = "sanic_chtblchecksid";
            public const string CheckStatus = "sanic_checkstatus";

            /// <summary>
            /// Sellos server-side del handshake de recepcion, estampados por
            /// CheckStatusStampPlugin en Pre-operation al escribirse el estado.
            /// </summary>
            public const string SentToDigitizationOn = "sanic_senttodigitizationon";
            public const string ReceivedOn = "sanic_receivedon";
            public const string NotReceivedOn = "sanic_notreceivedon";

            public const string RelatedDepositLookup = "sanic_relatedrecounterdepositid";
            public const string RelatedDeclaredCheck = "sanic_relateddeclaredcheck";

            /// <summary>
            /// Tula del cheque, derivada del deposito de recuento por
            /// CheckMirrorStampPlugin. Existe para que el tracker traiga los cheques
            /// de una tula en UNA query delegable. "No editar a mano."
            /// </summary>
            public const string BagLookup = "sanic_bag";

            /// <summary>
            /// B-9 — sesion de recuento del cheque, derivada de
            /// sanic_bag.sanic_recountingsession por CheckMirrorStampPlugin (columna
            /// creada por create-supervisor-mirror-columns.sh). Existe para que los
            /// conteos de cheques de scrResumen sean CountRows DELEGADOS: la cadena
            /// Cheque -> Tula -> Sesion es dot-walk de dos saltos, NO delega, y
            /// materializar filas en coleccion topa en el limite del cliente
            /// (500/2000) sin error. "No editar a mano."
            /// </summary>
            public const string SessionLookup = "sanic_recountingsession";

            /// <summary>
            /// PreImage del paso de CASCADA (Update Post-op de la TULA) con
            /// sanic_recountingsession: sin ella no se sabe si la sesion CAMBIO y la
            /// cascada reescribiria N cheques por un update sin cambio real.
            /// </summary>
            public const string SessionCascadePreImageName = "PreImage";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Sesion de recuento (CH-Tbl-RecountingSessions).</summary>
        internal static class RecountingSession
        {
            public const string LogicalName = "sanic_chtblrecountingsession";
            public const string Id = "sanic_chtblrecountingsessionid";

            /// <summary>Cajero dueno de la sesion (lookup a systemuser, requerido).</summary>
            public const string Cashier = "sanic_cashier";

            /// <summary>Mesa de conteo elegida (lookup a CH-Tbl-CountingDesks, requerido).</summary>
            public const string CountingDesk = "sanic_countingdesk";

            /// <summary>
            /// Confirmacion de la sesion. 🚫 La app v2 NUNCA la escribe: la estampa
            /// RecountingSessionAutoApprovePlugin al auto-aprobar la mesa propia
            /// (00 §2.2 punto 1). Mata S-1.
            /// </summary>
            public const string Confirmed = "sanic_confirmed";

            /// <summary>Quien confirmo la sesion (lookup a systemuser).</summary>
            public const string ConfirmedBy = "sanic_confirmedby";

            public const string SessionForciblyClosed = "sanic_sessionforciblyclosed";

            /// <summary>PreImage del paso Delete del guard, con sanic_cashier + sanic_confirmed.</summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Mesa de conteo (CH-Tbl-CountingDesks) — solo lectura para las apps.</summary>
        internal static class CountingDesk
        {
            public const string LogicalName = "sanic_chtblcountingdesk";

            /// <summary>Cajero asignado a la mesa (lookup a systemuser). Es el insumo del auto-approve.</summary>
            public const string Cajero = "sanic_cajero";
        }

        /// <summary>Detalle de arqueo por denominacion (CH-Tbl-CountedMoneyDetails).</summary>
        internal static class CountedMoneyDetail
        {
            public const string LogicalName = "sanic_chtblcountedmoneydetails";
            public const string Id = "sanic_chtblcountedmoneydetailsid";

            /// <summary>Deposito de recuento padre (lookup, requerido). Es por donde el gate llega a la tula.</summary>
            public const string DepositLookup = "sanic_relateddepositrecountingid";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Detalle de sesion de recuento — una tula dentro de la sesion
        /// (CH-Tbl-RecountingSessionDetails).</summary>
        internal static class RecountingSessionDetail
        {
            public const string LogicalName = "sanic_chtblrecountingsessiondetail";
            public const string Id = "sanic_chtblrecountingsessiondetailid";
        }

        /// <summary>Lectura de la contadora (CH-Tbl-XMLReadings).</summary>
        internal static class XmlReading
        {
            public const string LogicalName = "sanic_chtblxmlreading";
            public const string Id = "sanic_chtblxmlreadingid";

            /// <summary>Sesion-detalle (tula) a la que pertenece la lectura. Origen de E-4.</summary>
            public const string SessionDetailLookup = "sanic_recountingsessiondetailid";

            /// <summary>
            /// Bit de lectura vigente. Origen de E-4: cuando llega una lectura nueva,
            /// la vieja se apaga DESPUES de que sus detalles ya existen — por eso el
            /// espejo necesita cascada y no alcanza con estampar en el Create del hijo.
            /// </summary>
            public const string IsActive = "sanic_isactive";

            /// <summary>
            /// PreImage del paso de CASCADA (Update Post-op de la LECTURA) con
            /// sanic_recountingsessiondetailid + sanic_isactive: sin ella no se sabe si
            /// los valores CAMBIARON y la cascada reescribiria N detalles por un update
            /// sin cambio real. Ademas aporta el valor EFECTIVO de la columna que no
            /// vino en el Target (las dos se propagan juntas).
            /// </summary>
            public const string PreImageName = "PreImage";
        }

        /// <summary>Detalle de la lectura de la contadora, por denominacion
        /// (CH-Tbl-XMLReadingDetails).</summary>
        internal static class XmlReadingDetail
        {
            public const string LogicalName = "sanic_chtblxmlreadingdetail";
            public const string Id = "sanic_chtblxmlreadingdetailid";
            public const string ReadingLookup = "sanic_xmlreading";

            /// <summary>
            /// E-4 (lookup): sesion-detalle de la lectura del detalle, derivada por
            /// XmlReadingDetailMirrorPlugin. Existe para que Q17 ("Leer contadora") sea
            /// un Filter DELEGABLE de UN salto: la v1 escribia
            /// Filter(details, XMLReading.Id in Filter(readings, ...).Id) — un "in" con
            /// subconsulta que JAMAS delega (D-1). "No editar a mano."
            /// </summary>
            public const string SessionDetailMirror = "sanic_recountingsessiondetailmirror";

            /// <summary>
            /// E-4 (bit): espejo de XMLReading.IsActive. Es la SEGUNDA condicion de Q17;
            /// sin ella habria que volver a CH-Tbl-XMLReadings a preguntar IsActive y
            /// vuelve el segundo salto que E-4 vino a matar (ledger L-3).
            /// "No editar a mano."
            /// </summary>
            public const string ActiveMirror = "sanic_xmlreadingactiva";
        }

        /// <summary>Bolsa declarada (CH-Tbl-DeclaredBags).</summary>
        internal static class DeclaredBag
        {
            public const string LogicalName = "sanic_chtbldeclaredbags";
            public const string Id = "sanic_chtbldeclaredbagsid";
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";
        }

        /// <summary>Cheque declarado (CH-Tbl-DeclaredChecks).</summary>
        internal static class DeclaredCheck
        {
            public const string LogicalName = "sanic_chtbldeclaredchecks";
            public const string Id = "sanic_chtbldeclaredchecksid";

            /// <summary>
            /// Espejo del cheque fisico que lo referencia (la relacion real vive en
            /// sanic_chtblchecks.sanic_relateddeclaredcheck), estampado por
            /// CheckMirrorStampPlugin para que el tracker navegue del declarado a su
            /// recuento. "No editar a mano."
            /// </summary>
            public const string MatchedCheck = "sanic_matchedcheck";

            /// <summary>Deposito declarado padre. Origen del espejo E-3.</summary>
            public const string DeclaredDepositLookup = "sanic_declareddepositid";

            /// <summary>
            /// E-3: bolsa declarada del cheque declarado, derivada de
            /// sanic_declareddepositid.sanic_declaredbag por
            /// DeclaredCheckBagMirrorPlugin. Existe para que la apertura de tula traiga
            /// TODOS los cheques declarados de la bolsa en UNA query delegable en vez
            /// de un Filter remoto por deposito declarado (~82 round-trips medidos).
            /// "No editar a mano."
            /// </summary>
            public const string DeclaredBagMirror = "sanic_declaredbagmirror";
        }

        /// <summary>Lote de transacciones (CH-Tbl-TransactionBatches).</summary>
        internal static class TransactionBatch
        {
            public const string LogicalName = "sanic_transactionbatches";
            public const string Id = "sanic_transactionbatchesid";
            public const string Name = "sanic_name";
            public const string BatchStatus = "sanic_batchstatus";
            public const string Comentarios = "sanic_comentarios";

            /// <summary>
            /// Totales materializados por moneda (decimal 2), recomputados por
            /// BatchTotalsCalculator desde las transacciones del lote. Existen para
            /// que las apps muestren totales sin sumar client-side (cap de filas).
            /// </summary>
            public const string TotalNio = "sanic_totalnio";
            public const string TotalUsd = "sanic_totalusd";
            public const string TotalEur = "sanic_totaleur";

            /// <summary>
            /// Cajero dueño de las transacciones del corte (lookup a systemuser),
            /// estampado por CreateTransactionCutPlugin junto con GeneradoPor.
            /// </summary>
            public const string Cajero = "sanic_cajero";
            public const string GeneradoPor = "sanic_generadopor";

            /// <summary>
            /// Sellos server-side de las transiciones del lote (quien y cuando),
            /// estampados por BatchTransitionStampPlugin en Pre-operation. La estampa
            /// IngresadoPor es ademas el insumo de la regla de segregacion de funciones
            /// (quien marca Aplicado no puede ser quien marco Ingresado).
            /// </summary>
            public const string IngresadoPor = "sanic_ingresadopor";
            public const string IngresadoOn = "sanic_ingresadoon";
            public const string AplicadoPor = "sanic_aplicadopor";
            public const string AplicadoOn = "sanic_aplicadoon";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Deposito de recuento (CH-Tbl-DepositsRecounting).</summary>
        internal static class DepositRecounting
        {
            public const string LogicalName = "sanic_chtbldepositsrecounting";
            public const string Id = "sanic_chtbldepositsrecountingid";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";

            /// <summary>
            /// OJO: el logical name real tiene un typo de origen ("loca" sin la 'l'
            /// de "local"). NO corregirlo: asi esta publicado en el ambiente.
            /// </summary>
            public const string OtherCheckAmount = "sanic_otherlocacheckamount";

            public const string CashAmount = "sanic_cashamount";
            public const string Currency = "sanic_currency";
            public const string AccountNumber = "sanic_accountnumber";
            public const string TransactionNumber = "sanic_transactionnumber";

            /// <summary>
            /// Lado recontado -> declarado de la relacion. El lado espejo vive en
            /// DeclaredDeposit.RelatedRecountedDeposit y las apps usan LOS DOS: el
            /// guard resuelve por este y, si viene vacio, por el otro.
            /// </summary>
            public const string RelatedDeclaredDeposit = "sanic_relateddeclareddeposit";

            /// <summary>Tula del deposito de recuento. Origen del espejo E-1.</summary>
            public const string BagLookup = "sanic_bagid";

            /// <summary>
            /// E-1: sesion de recuento del deposito, derivada de
            /// sanic_bagid.sanic_recountingsession por DepositSessionMirrorPlugin.
            /// Existe para que los 12 Sum de dinero de scrTulas sean DELEGADOS: la
            /// cadena Deposito -> Tula -> Sesion es dot-walk de DOS saltos, NO delega,
            /// y Power Fx trunca en el tope del cliente (500/2000) SIN error — el rail
            /// de KPIs mostraba cifras PARCIALES de plata (D-2). "No editar a mano."
            /// </summary>
            public const string SessionMirror = "sanic_recountingsessionmirror";

            public const string PreImageName = "PreImage";
        }

        /// <summary>Deposito declarado por el cliente (CH-Tbl-DeclaredDeposits).</summary>
        internal static class DeclaredDeposit
        {
            public const string LogicalName = "sanic_chtbldeclareddeposits";
            public const string Id = "sanic_chtbldeclareddepositsid";
            public const string CashAmount = "sanic_cashamount";
            public const string BacCheckAmount = "sanic_baclocalcheckamount";

            /// <summary>Aca SI lleva la 'l' — a diferencia de DepositsRecounting.</summary>
            public const string OtherCheckAmount = "sanic_otherlocalcheckamount";

            public const string Currency = "sanic_currency";
            public const string TransactionNumber = "sanic_transactionno";

            /// <summary>Referencia del deposito declarado (texto 31, field-level security).</summary>
            public const string DepositReference = "sanic_depositreference";

            /// <summary>Lado declarado -> recontado de la relacion (vacio = nadie lo reconto).</summary>
            public const string RelatedRecountedDeposit = "sanic_relatedrecounteddeposit";

            /// <summary>
            /// Bolsa declarada a la que pertenece el deposito. La escribe
            /// DeclarationMaterializer en el Create; es el ORIGEN del espejo E-3
            /// sanic_declaredbagmirror del cheque declarado.
            /// </summary>
            public const string DeclaredBagLookup = "sanic_declaredbag";
        }

        /// <summary>Custom API de corte de transacciones a lote (CH-App-Digitization).</summary>
        internal static class CutApi
        {
            public const string MessageName = "sanic_CHCreateTransactionCut";

            /// <summary>systemuserid del cajero cuyas transacciones toma el corte (REQUERIDO).</summary>
            public const string InCajeroId = "CajeroId";

            public const string OutBatchId = "BatchId";
            public const string OutBatchName = "BatchName";
            public const string OutAssignedCount = "AssignedCount";
        }

        /// <summary>Team de Dataverse. Los AAD group teams materializan su membresia en teammembership.</summary>
        internal static class Team
        {
            public const string LogicalName = "team";
            public const string Id = "teamid";
            public const string AadObjectId = "azureactivedirectoryobjectid";
            public const string MembershipRelationship = "teammembership";
        }

        internal static class SystemUser
        {
            public const string LogicalName = "systemuser";
            public const string Id = "systemuserid";
        }
    }
}
