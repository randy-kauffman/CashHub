# Inventario de componentes — CashHub

> Inventario exhaustivo de TODO componente de las soluciones CashHub (tablas, campos, relaciones,
> option sets, variables de entorno, roles, plugin assembly, plugin classes, Custom APIs, Canvas
> Apps y Cloud Flows). Vive en `docs/referencia/` porque es transversal — no pertenece a un único
> proceso. Nació como base para la revisión guiada de `docs/procesos/` (ver bitácora de decisiones
> en [`docs/procesos/decisiones-revision.md`](../procesos/decisiones-revision.md)), pero es de
> utilidad general para cualquier trabajo sobre el modelo de datos y los componentes del sistema.
>
> Cada fila tiene un ID estable (nombre lógico/schema name real) y ocho columnas fijas:
> **Objetivo** (para qué existe este componente), **Dónde se usa** (apps/flows/plugins que lo
> consumen), **Documentos de referencia** (otros .md del repo que lo mencionan), **Estado**
> (revisión: Sin revisar / Confirmado / Modificar / Eliminar) y **Notas**. Las tres primeras
> arrancan vacías a propósito — se completan a medida que se confirma, modifica o elimina cada
> elemento durante la revisión.
> Estado inicial de TODAS las filas: `Sin revisar`. NO inventes estados — este documento es la fotografía inicial.
> Fuente de verdad: solutions/unpacked/ y src/backend/ (código vivo del repo, ver docs/procesos/CONTRATO.md regla 1).
> Generado el 2026-08-24. Soluciones inventariadas: `CashHubCore` v1.14.0.0 (tablas, campos, relaciones, option sets globales, variables de entorno, roles), `CashHubApps` v1.31.0.4 (canvas apps), `CashHubFlows` v1.12.0.0 (cloud flows), y `src/backend/CashHub.Plugins` (fuera de solución, se registra por script — ver docs/soluciones/01-core.md §7).

## 1. Tablas (Entities)
| Logical name | Display name | Solución | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|
| `Account` | Account | CashHubCore | | | | Sin revisar | Tabla nativa de Dataverse extendida por la solución; 0 campo(s) custom en este export. |
| `SystemUser` | User | CashHubCore | | | | Sin revisar | Tabla nativa de Dataverse extendida por la solución; 0 campo(s) custom en este export. |
| `sanic_CHTblAppCatalog` | CH-Tbl-AppCatalog | CashHubCore | | | | Sin revisar | 10 campos custom. |
| `sanic_CHTblAppCatalogAccess` | CH-Tbl-AppCatalogAccess | CashHubCore | | | | Sin revisar | 3 campos custom. |
| `sanic_CHTblApprovalRequest` | CH-Tbl-ApprovalRequest | CashHubCore | | | | Sin revisar | 9 campos custom. |
| `sanic_CHTblBags` | CH-Tbl-Bags | CashHubCore | | | | Sin revisar | 15 campos custom. |
| `sanic_CHTblChecks` | CH-Tbl-Checks | CashHubCore | | | | Sin revisar | 15 campos custom. |
| `sanic_CHTblClient` | CH-Tbl-Client | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientAccount` | CH-Tbl-ClientAccount | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientBranch` | CH-Tbl-ClientBranch | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientDataChangePermission` | CH-Tbl-ClientDataChangePermission | CashHubCore | | | | Sin revisar | 4 campos custom. |
| `sanic_CHTblClientDataChangeRequest` | CH-Tbl-ClientDataChangeRequest | CashHubCore | | | | Sin revisar | 15 campos custom. |
| `sanic_CHTblClientDataChangeRequestLine` | CH-Tbl-ClientDataChangeRequestLine | CashHubCore | | | | Sin revisar | 9 campos custom. |
| `sanic_CHTblClientDataChangesAuditTrail` | CH-Tbl-ClientDataChangesAuditTrail | CashHubCore | | | | Sin revisar | 7 campos custom. |
| `sanic_CHTblClientDepositHistory` | CH-Tbl-ClientDepositHistory | CashHubCore | | | | Sin revisar | 3 campos custom. |
| `sanic_CHTblClientEmail` | CH-Tbl-ClientEmail | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientTableLogicalFieldName` | CH-Tbl-ClientTableLogicalFieldName | CashHubCore | | | | Sin revisar | 9 campos custom. |
| `sanic_CHTblCountedMoneyDetails` | CH-Tbl-CountedMoneyDetails | CashHubCore | | | | Sin revisar | 6 campos custom. |
| `sanic_CHTblCountingDesk` | CH-Tbl-CountingDesk | CashHubCore | | | | Sin revisar | 4 campos custom. |
| `sanic_CHTblDeclaration` | CH-Tbl-Declaration | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblDeclaredBags` | CH-Tbl-DeclaredBags | CashHubCore | | | | Sin revisar | 9 campos custom. |
| `sanic_CHTblDeclaredChecks` | CH-Tbl-DeclaredChecks | CashHubCore | | | | Sin revisar | 9 campos custom. |
| `sanic_CHTblDeclaredDeposits` | CH-Tbl-DeclaredDeposits | CashHubCore | | | | Sin revisar | 12 campos custom. |
| `sanic_CHTblDepositsRecounting` | CH-Tbl-DepositsRecounting | CashHubCore | | | | Sin revisar | 11 campos custom. |
| `sanic_CHTblEmailTemplate` | CH-Tbl-EmailTemplate | CashHubCore | | | | Sin revisar | 4 campos custom. |
| `sanic_CHTblEmailsAttachments` | CH-Tbl-EmailsAttachments | CashHubCore | | | | Sin revisar | 8 campos custom. |
| `sanic_CHTblLocations` | CH-Tbl-Locations | CashHubCore | | | | Sin revisar | 1 campos custom. |
| `sanic_CHTblMoneyDenomination` | CH-Tbl-MoneyDenomination | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblParameter` | CH-Tbl-Parameter | CashHubCore | | | | Sin revisar | 6 campos custom. |
| `sanic_CHTblPlaybook` | CH-Tbl-Playbook | CashHubCore | | | | Sin revisar | 6 campos custom. |
| `sanic_CHTblReceivedEmails` | CH-Tbl-Received Emails | CashHubCore | | | | Sin revisar | 29 campos custom. |
| `sanic_CHTblReceptionBatch` | CH-Tbl-ReceptionBatch | CashHubCore | | | | Sin revisar | 10 campos custom. |
| `sanic_CHTblReceptions` | CH-Tbl-Receptions | CashHubCore | | | | Sin revisar | 12 campos custom. |
| `sanic_CHTblRecountingSession` | CH-Tbl-RecountingSession | CashHubCore | | | | Sin revisar | 8 campos custom. |
| `sanic_CHTblRecountingSessionDetail` | CH-Tbl-RecountingSessionDetail | CashHubCore | | | | Sin revisar | 5 campos custom. |
| `sanic_CHTblSystemHeartbeat` | CH-Tbl-SystemHeartbeat | CashHubCore | | | | Sin revisar | 3 campos custom. |
| `sanic_CHTblTransactions` | CH-Tbl-Transactions | CashHubCore | | | | Sin revisar | 18 campos custom. |
| `sanic_CHTblTransportCompany` | CH-Tbl-TransportCompany | CashHubCore | | | | Sin revisar | 1 campos custom. |
| `sanic_CHTblValidationResults` | CH-Tbl-ValidationResults | CashHubCore | | | | Sin revisar | 10 campos custom. |
| `sanic_CHTblValidationRuns` | CH-Tbl-ValidationRuns | CashHubCore | | | | Sin revisar | 8 campos custom. |
| `sanic_CHTblXMLReading` | CH-Tbl-XMLReading | CashHubCore | | | | Sin revisar | 4 campos custom. |
| `sanic_CHTblXMLReadingDetail` | CH-Tbl-XMLReadingDetail | CashHubCore | | | | Sin revisar | 8 campos custom. |
| `sanic_TransactionBatches` | CH-Tbl-TransactionBatches | CashHubCore | | | | Sin revisar | 13 campos custom. |
| `sanic_ValidationRules` | CH-Tbl-ValidationRules | CashHubCore | | | | Sin revisar | 11 campos custom. |

## 2. Campos por tabla

> Solo campos custom (`IsCustomField=1` en el `Entity.xml`, prefijo `sanic_`). Para `Account` y `SystemUser` (extensiones de tablas nativas) se listan únicamente los campos custom agregados por esta solución — no hay campos nativos de Dataverse en esta sección. En este export ninguna de las dos tablas nativas tiene campos custom propios (ver nota en cada subsección).

### Account (`Account`)

_No hay campos custom (`sanic_*`) agregados por esta solución en este export._

### User (`SystemUser`)

_No hay campos custom (`sanic_*`) agregados por esta solución en este export._

### CH-Tbl-AppCatalog (`sanic_CHTblAppCatalog`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AAD Group Id | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_appkey` | App Key | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_appurl` | AppURL | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_description` | Description | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_displayname` | Display Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_enabled` | Enabled | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_icon` | Icon | Imagen | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_order` | Order | Número entero | required | | | | Sin revisar | |
| `sanic_tags` | Tags | Texto (nvarchar) | none | | | | Sin revisar | |

### CH-Tbl-AppCatalogAccess (`sanic_CHTblAppCatalogAccess`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AAD Group Id | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_appcatalog` | App Catalog | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | | | Sin revisar | |

### CH-Tbl-ApprovalRequest (`sanic_CHTblApprovalRequest`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_approvalstatus` | ApprovalStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_bagnumber` | BagNumber | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_requestdetails` | RequestDetails | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_requestresources` | RequestResources | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_requesttype` | RequestType | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_resolutiondatetime` | ResolutionDatetime | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_statusreason` | StatusReason | Texto (nvarchar) | recommended | | | | Sin revisar | |

### CH-Tbl-Bags (`sanic_CHTblBags`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagdeclarationid` | BagDeclarationID | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_bagid` | BagID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_bagstatus` | BagStatus | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_decltotaleur` | DeclTotalEUR | Decimal | none | | | | Sin revisar | |
| `sanic_decltotalnio` | DeclTotalNIO | Decimal | none | | | | Sin revisar | |
| `sanic_decltotalusd` | DeclTotalUSD | Decimal | none | | | | Sin revisar | |
| `sanic_hasdifference` | HasDifference | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_receiptidmirror` | ReceiptIdMirror | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_receptionbatchid` | ReceptionBatchId | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_receptionid` | ReceptionID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_recountingsession` | RecountingSession | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | recommended | | | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | recommended | | | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | recommended | | | | Sin revisar | |

### CH-Tbl-Checks (`sanic_CHTblChecks`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bag` | Bag | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_checkamount` | CheckAmount | Decimal | required | | | | Sin revisar | |
| `sanic_checkcurrency` | CheckCurrency | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_checknumber` | CheckNumber | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_checkstatus` | CheckStatus | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_checktypes` | CheckTypes | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_notreceivedon` | No recibido el | Fecha y hora | none | | | | Sin revisar | |
| `sanic_receivedby` | ReceivedBy | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_receivedon` | Recibido el | Fecha y hora | none | | | | Sin revisar | |
| `sanic_recountingsession` | RecountingSession | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_relateddeclaredcheck` | RelatedDeclaredCheck | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_relatedrecounterdepositid` | RelatedReCounterDepositID | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_senttodigitizationby` | SentToDigitizationBy | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_senttodigitizationon` | Enviado el | Fecha y hora | none | | | | Sin revisar | |

### CH-Tbl-Client (`sanic_CHTblClient`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cifbco` | CIFBCO | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_cifcom` | CIFCOM | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_iseditable` | IsEditable | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | required | | | | Sin revisar | |

### CH-Tbl-ClientAccount (`sanic_CHTblClientAccount`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_chchrecordstatus` | CH-Ch-RecordStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_client` | Client | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_currency` | Moneda | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_name` | Numero de cuenta | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | | | Sin revisar | |

### CH-Tbl-ClientBranch (`sanic_CHTblClientBranch`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_client` | Client | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_clientnamemirror` | ClientNameMirror | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_clientstatus` | ClientStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | | | Sin revisar | |

### CH-Tbl-ClientDataChangePermission (`sanic_CHTblClientDataChangePermission`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AADGroupID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_teamsgroupid` | TeamsGroupID | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | required | | | | Sin revisar | |

### CH-Tbl-ClientDataChangeRequest (`sanic_CHTblClientDataChangeRequest`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `cr140_changestatus` | ChangeStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_applynotes` | ApplyNotes | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_applyon` | ApplyOn | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_changestatus` | Change Status | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_reason` | Reason | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_reasonfordecision` | ReasonForDecision | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_relatedclient` | RelatedClient | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_solvedon` | SolvedOn | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_targetrecordid` | TargetRecordID | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_targettable` | TargetTable | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_targettabledisplayname` | TargetTableDisplayName | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_targettablepluralname` | TargetTablePluralName | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | none | | | | Sin revisar | |

### CH-Tbl-ClientDataChangeRequestLine (`sanic_CHTblClientDataChangeRequestLine`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_changerequestid` | ChangeRequestID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_changestatus` | ChangeStatus | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_fielddefinition` | Field Definition | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_newvalue` | NewValue | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_oldvalue` | OldValue | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_reasonfordecision` | ReasonForDecision | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_solvedon` | SolvedOn | Fecha y hora | recommended | | | | Sin revisar | |

### CH-Tbl-ClientDataChangesAuditTrail (`sanic_CHTblClientDataChangesAuditTrail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_message` | Message | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_opeationtype` | OpeationType | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_relatedclient` | RelatedClient | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_relatedcrl` | RelatedCRL | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_relatedrfc` | RelatedRFC | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_result` | Result | Dos opciones (bit) | required | | | | Sin revisar | |

### CH-Tbl-ClientDepositHistory (`sanic_CHTblClientDepositHistory`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_chchrecordstatus` | CH-Ch-RecordStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_client` | Client | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |

### CH-Tbl-ClientEmail (`sanic_CHTblClientEmail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_alwaysnotify` | AlwaysNotify | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_client` | Client | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_clientstatus` | ClientStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | | | Sin revisar | |

### CH-Tbl-ClientTableLogicalFieldName (`sanic_CHTblClientTableLogicalFieldName`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_datatype` | DataType | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_isallowed` | IsAllowed | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_logicalname` | LogicalName | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_lookuptargettable` | LookupTargetTable | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_optionsetmap` | OptionSetMap | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_table` | Table | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_tabledisplayname` | Table DisplayName | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_tablepluralname` | TablePluralName | Texto (nvarchar) | none | | | | Sin revisar | |

### CH-Tbl-CountedMoneyDetails (`sanic_CHTblCountedMoneyDetails`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_denomination` | Denomination | Decimal | required | | | | Sin revisar | |
| `sanic_denominationtype` | DenominationType | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_quantity` | Quantity | Número entero | required | | | | Sin revisar | |
| `sanic_relateddepositrecountingid` | RelatedDepositRecountingID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_totalamount` | TotalAmount | Decimal | none | | | | Sin revisar | |

### CH-Tbl-CountingDesk (`sanic_CHTblCountingDesk`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cajero` | Cajero | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_enable` | Enable | Dos opciones (bit) | recommended | | | | Sin revisar | |
| `sanic_machineserialnumber` | MachineSerialNumber | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |

### CH-Tbl-Declaration (`sanic_CHTblDeclaration`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_client` | Cliente atribuido | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_receivedemail` | ReceivedEmail | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_sourceentity` | SourceEntity | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_sourcerecordid` | SourceRecordID | Texto (nvarchar) | required | | | | Sin revisar | |

### CH-Tbl-DeclaredBags (`sanic_CHTblDeclaredBags`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagid` | BagID | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_bagnumber` | BagNumber | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_declarationid` | DeclarationID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_isrelatedtobag` | IsRelatedToBag | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_totaldeposits` | TotalDeposits | Número entero | none | | | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | recommended | | | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | recommended | | | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | recommended | | | | Sin revisar | |

### CH-Tbl-DeclaredChecks (`sanic_CHTblDeclaredChecks`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_checkamount` | CheckAmount | Decimal | required | | | | Sin revisar | |
| `sanic_checkcurrency` | CheckCurrency | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_checknumber` | CheckNumber | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_checktype` | CheckType | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_declaration` | Declaration | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_declaredbagmirror` | DeclaredBagMirror | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_declareddepositid` | DeclaredDepositID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_matchedcheck` | MatchedCheck | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |

### CH-Tbl-DeclaredDeposits (`sanic_CHTblDeclaredDeposits`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Número entero | required | | | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_cashamount` | CashAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_declaration` | Declaration | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_declaredbag` | DeclaredBag | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_depositreference` | DepositReference | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_otherlocalcheckamount` | OtherLocalCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_relatedrecounteddeposit` | RelatedRecountedDeposit | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_total` | Total | Decimal | none | | | | Sin revisar | |
| `sanic_transactionno` | TransactionNo | Número entero | recommended | | | | Sin revisar | |

### CH-Tbl-DepositsRecounting (`sanic_CHTblDepositsRecounting`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Número entero | recommended | | | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_bagid` | BagID | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_cashamount` | CashAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_otherlocacheckamount` | OtherLocaCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_recountingsessionmirror` | RecountingSessionMirror | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_relateddeclareddeposit` | RelatedDeclaredDeposit | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_total` | Total | Decimal | none | | | | Sin revisar | |
| `sanic_transactionnumber` | TransactionNumber | Número entero | recommended | | | | Sin revisar | |

### CH-Tbl-EmailTemplate (`sanic_CHTblEmailTemplate`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bodyhtml` | BodyHtml | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_isactive` | IsActive | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_subject` | Subject | Texto (nvarchar) | recommended | | | | Sin revisar | |

### CH-Tbl-EmailsAttachments (`sanic_CHTblEmailsAttachments`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_email` | Email | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_emailattachmentid` | EmailAttachmentID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_filecontent` | FileContent | Archivo (file) | recommended | | | | Sin revisar | |
| `sanic_fileextention` | FileExtention | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_filename` | Filename | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_jsondata` | JSONData | Archivo (file) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_tamanoenbytes` | TamanoEnbytes | Número entero | none | | | | Sin revisar | |

### CH-Tbl-Locations (`sanic_CHTblLocations`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |

### CH-Tbl-MoneyDenomination (`sanic_CHTblMoneyDenomination`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_currency` | Currency | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_enabled` | Enabled | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_value` | Value | Decimal | required | | | | Sin revisar | |

### CH-Tbl-Parameter (`sanic_CHTblParameter`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_category` | Category | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_datatype` | Data Type | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_description` | Description | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_isactive` | Is Active | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | Key | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_value` | Value | Texto multilínea (ntext) | required | | | | Sin revisar | |

### CH-Tbl-Playbook (`sanic_CHTblPlaybook`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_isactive` | IsActive | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_nota` | Nota | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_orden` | Orden | Número entero | none | | | | Sin revisar | |
| `sanic_pasos` | Pasos | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_titulo` | Titulo | Texto (nvarchar) | none | | | | Sin revisar | |

### CH-Tbl-Received Emails (`sanic_CHTblReceivedEmails`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_attachment` | Attachment | Archivo (file) | recommended | | | | Sin revisar | |
| `sanic_bancanotifiedon` | BancaNotifiedOn | Fecha y hora | none | | | | Sin revisar | |
| `sanic_client` | Cliente atribuido | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_declaredcif` | DeclaredCif | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_emailreplystatus` | EmailReplyStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_emailreviewmethod` | EmailReviewMethod | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_emailreviewstatus` | EmailReviewStatus | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_errorcode` | ErrorCode | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_errortext` | ErrorText | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_overrideby` | OverrideBy | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_overrideon` | OverrideOn | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_overridereason` | OverrideReason | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_receivedemailinternetmessageid` | ReceivedEmailInternetMessageId | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_receivedemailoutlookuniqueid` | ReceivedEmailOutlookUniqueID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_receptiondatetime` | ReceptionDateTime | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_repliedby` | RepliedBy | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_replyattempts` | ReplyAttempts | Número entero | none | | | | Sin revisar | |
| `sanic_replybodyhtml` | ReplyBodyHtml | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_replyclosenote` | ReplyCloseNote | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_replydatetime` | ReplyDateTime | Fecha y hora | recommended | | | | Sin revisar | |
| `sanic_replyemailinternetmessageid` | ReplyEmailInternetMessageId | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_replyemailoutlookuniqueid` | ReplyEmailOutlookUniqueID | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_replysubject` | ReplySubject | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_senderclassification` | SenderClassification | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_sourceemailaddress` | SourceEmailAddress | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_subject` | Subject | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_triagenotes` | TriageNotes | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_validationstatus` | ValidationStatus | Option set (picklist) | recommended | | | | Sin revisar | |

### CH-Tbl-ReceptionBatch (`sanic_CHTblReceptionBatch`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_decniocash` | DecNIOCash | Decimal | none | | | | Sin revisar | |
| `sanic_decniocheck` | DecNIOCheck | Decimal | none | | | | Sin revisar | |
| `sanic_decniototal` | DecNIOTotal | Decimal | none | | | | Sin revisar | |
| `sanic_decusdcash` | DecUSDCash | Decimal | none | | | | Sin revisar | |
| `sanic_decusdcheck` | DecUSDCheck | Decimal | none | | | | Sin revisar | |
| `sanic_decusdtotal` | DecUSDTotal | Decimal | none | | | | Sin revisar | |
| `sanic_location` | Location | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_receptionstatus` | ReceptionStatus | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_transportadora` | Transportadora | Texto (nvarchar) | required | | | | Sin revisar | |

### CH-Tbl-Receptions (`sanic_CHTblReceptions`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_batchstatus` | BatchStatus | Número entero | none | | | | Sin revisar | |
| `sanic_branchname` | BranchName | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_comments` | Comments | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_niocash` | NIOCash | Decimal | recommended | | | | Sin revisar | |
| `sanic_niocheck` | NIOCheck | Decimal | recommended | | | | Sin revisar | |
| `sanic_receiptid` | ReceiptID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_receptionbatch` | ReceptionBatch | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_remitentclient` | RemitentClient | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_remitentname` | RemitentName | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_usdcash` | USDCash | Decimal | recommended | | | | Sin revisar | |
| `sanic_usdcheck` | USDCheck | Decimal | recommended | | | | Sin revisar | |

### CH-Tbl-RecountingSession (`sanic_CHTblRecountingSession`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cashier` | Cashier | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_confirmed` | Confirmed | Dos opciones (bit) | recommended | | | | Sin revisar | |
| `sanic_confirmedby` | ConfirmedBy | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_countingdesk` | CountingDesk | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_forcedclosureby` | ForcedClosureBy | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_reasonforforcedflosure` | ReasonForForcedFlosure | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_sessionforciblyclosed` | SessionForciblyClosed | Dos opciones (bit) | recommended | | | | Sin revisar | |

### CH-Tbl-RecountingSessionDetail (`sanic_CHTblRecountingSessionDetail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagid` | BagId | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_name` | RecountingSessionDetailId | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_recountingsessiondetailstatus` | RecountingSessionDetailStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_recountingsessionid` | RecountingSessionId | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_supervisorid` | SupervisorId | Búsqueda (lookup) | none | | | | Sin revisar | |

### CH-Tbl-SystemHeartbeat (`sanic_CHTblSystemHeartbeat`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_details` | Details | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_lastrunon` | LastRunOn | Fecha y hora | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |

### CH-Tbl-Transactions (`sanic_CHTblTransactions`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Decimal | recommended | | | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_batchstatus` | BatchStatus | Número entero | none | | | | Sin revisar | |
| `sanic_cashamount` | CashAmount | Decimal | none | | | | Sin revisar | |
| `sanic_comentarios` | Comentarios | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_motivorechazocore` | MotivoRechazoCore | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_otherlocalcheckamount` | OtherLocalCheckAmount | Decimal | recommended | | | | Sin revisar | |
| `sanic_relateddepositrecounting` | RelatedDepositRecounting | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_total` | Total | Decimal | none | | | | Sin revisar | |
| `sanic_transactionbatch` | Transaction Batch | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_transactionid` | TransactionID | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_transactionnumber` | TransactionNumber | Número entero | recommended | | | | Sin revisar | |
| `sanic_transactionprocessingtype` | TransactionProcessingType | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_transactionreference` | TransactionReference | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_transactionstatus` | TransactionStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_transactiontype` | TransactionType | Option set (picklist) | recommended | | | | Sin revisar | |

### CH-Tbl-TransportCompany (`sanic_CHTblTransportCompany`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | required | | | | Sin revisar | |

### CH-Tbl-ValidationResults (`sanic_CHTblValidationResults`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cellref` | Celda | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_detail` | Detalle | Texto multilínea (ntext) | none | | | | Sin revisar | |
| `sanic_durationms` | Durationms | Número entero | recommended | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_rownumber` | Fila | Número entero | none | | | | Sin revisar | |
| `sanic_sheet` | Hoja | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_validationresult` | ValidationResult | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_validationrule` | ValidationRule | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_validationrun` | ValidationRun | Búsqueda (lookup) | required | | | | Sin revisar | |

### CH-Tbl-ValidationRuns (`sanic_CHTblValidationRuns`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_receivedemail` | ReceivedEmail | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_runend` | RunEnd | Fecha y hora | none | | | | Sin revisar | |
| `sanic_runresult` | RunResult | Option set (picklist) | recommended | | | | Sin revisar | |
| `sanic_runstart` | RunStart | Fecha y hora | required | | | | Sin revisar | |
| `sanic_sourceentity` | SourceEntity | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_sourcerecordid` | SourceRecordID | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | required | | | | Sin revisar | |

### CH-Tbl-XMLReading (`sanic_CHTblXMLReading`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_filename` | FileName | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_isactive` | IsActive | Dos opciones (bit) | none | | | | Sin revisar | |
| `sanic_name` | XMLReadingId | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_recountingsessiondetailid` | RecountingSessionDetailId | Búsqueda (lookup) | required | | | | Sin revisar | |

### CH-Tbl-XMLReadingDetail (`sanic_CHTblXMLReadingDetail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_currency` | Currency | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_denomination` | Denomination | Decimal | none | | | | Sin revisar | |
| `sanic_name` | XMLReadingDetailId | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_quantity` | Quantity | Número entero | none | | | | Sin revisar | |
| `sanic_recountingsessiondetailmirror` | RecountingSessionDetailMirror | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_subtotal` | Subtotal | Decimal | none | | | | Sin revisar | |
| `sanic_xmlreading` | XMLReading | Búsqueda (lookup) | required | | | | Sin revisar | |
| `sanic_xmlreadingactiva` | XMLReadingActiva | Dos opciones (bit) | none | | | | Sin revisar | |

### CH-Tbl-TransactionBatches (`sanic_TransactionBatches`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aplicadoon` | Aplicado el | Fecha y hora | none | | | | Sin revisar | |
| `sanic_aplicadopor` | Aplicado por | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_batchstatus` | BatchStatus | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_cajero` | Cajero | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_comentarios` | Comentarios | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_csvcore` | CSV para el core | Archivo (file) | none | | | | Sin revisar | |
| `sanic_generadopor` | Generado por | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_ingresadoon` | Ingresado el | Fecha y hora | none | | | | Sin revisar | |
| `sanic_ingresadopor` | Ingresado por | Búsqueda (lookup) | none | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | none | | | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | none | | | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | none | | | | Sin revisar | |

### CH-Tbl-ValidationRules (`sanic_ValidationRules`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_isactive` | IsActive | Dos opciones (bit) | required | | | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | | | Sin revisar | |
| `sanic_notes` | Notes | Texto multilínea (ntext) | recommended | | | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_validationdatatype` | ValidationDataType | Option set (picklist) | required | | | | Sin revisar | |
| `sanic_validationdependency` | ValidationDependency | Búsqueda (lookup) | recommended | | | | Sin revisar | |
| `sanic_validationfailmessage` | ValidationFailMessage | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_validationname` | Validation Name | Texto (nvarchar) | required | | | | Sin revisar | |
| `sanic_validationoperation` | Validation Operation | Option set (picklist) | none | | | | Sin revisar | |
| `sanic_validationorder` | ValidationOrder | Número entero | recommended | | | | Sin revisar | |
| `sanic_validationvalue` | ValidationValue | Texto (nvarchar) | required | | | | Sin revisar | |

## 3. Relaciones (1:N y N:N)

| Schema name relación | Tabla 1 (referencing) | Tabla 2 (referenced) | Tipo | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_CHTblAppCatalog_CHTblAppCatalogAccess` | `sanic_CHTblAppCatalogAccess` | `sanic_CHTblAppCatalog` | 1:N (lookup `sanic_AppCatalog`) | | | | Sin revisar | |
| `sanic_chtblapprovalrequest_SolvedBy_systemuser` | `sanic_CHTblApprovalRequest` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | | | Sin revisar | |
| `sanic_chtblbags_BagDeclarationID_sanic_chtbldeclaredbags` | `sanic_CHTblBags` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_BagDeclarationID`) | | | | Sin revisar | |
| `sanic_chtblbags_ReceptionBatch_sanic_chtblreceptionbatch` | `sanic_CHTblBags` | `sanic_CHTblReceptionBatch` | 1:N (lookup `sanic_ReceptionBatchId`) | | | | Sin revisar | |
| `sanic_chtblbags_ReceptionID_sanic_chtblreceptions` | `sanic_CHTblBags` | `sanic_CHTblReceptions` | 1:N (lookup `sanic_ReceptionID`) | | | | Sin revisar | |
| `sanic_chtblbags_RecountingSession_sanic_chtblrecountingsession` | `sanic_CHTblBags` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSession`) | | | | Sin revisar | |
| `sanic_chtblchecks_Bag_sanic_chtblbags` | `sanic_CHTblChecks` | `sanic_CHTblBags` | 1:N (lookup `sanic_Bag`) | | | | Sin revisar | |
| `sanic_chtblchecks_ReceivedBy_systemuser` | `sanic_CHTblChecks` | `SystemUser` | 1:N (lookup `sanic_ReceivedBy`) | | | | Sin revisar | |
| `sanic_chtblchecks_RecountingSession_sanic_chtblrecountingsession` | `sanic_CHTblChecks` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSession`) | | | | Sin revisar | |
| `sanic_chtblchecks_RelatedDeclaredCheck_sanic_chtbldeclaredchecks` | `sanic_CHTblChecks` | `sanic_CHTblDeclaredChecks` | 1:N (lookup `sanic_RelatedDeclaredCheck`) | | | | Sin revisar | |
| `sanic_chtblchecks_RelatedReCounterDepositID_sanic_chtbldepositsrecounting` | `sanic_CHTblChecks` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedReCounterDepositID`) | | | | Sin revisar | |
| `sanic_chtblchecks_SentToDigitizationBy_systemuser` | `sanic_CHTblChecks` | `SystemUser` | 1:N (lookup `sanic_SentToDigitizationBy`) | | | | Sin revisar | |
| `sanic_chtblclientaccount_Client_sanic_chtblclient` | `sanic_CHTblClientAccount` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_chtblclientbranch_Client_sanic_chtblclient` | `sanic_CHTblClientBranch` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangerequest_RelatedClient_sanic_chtblclient` | `sanic_CHTblClientDataChangeRequest` | `sanic_CHTblClient` | 1:N (lookup `sanic_RelatedClient`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangerequest_SolvedBy_systemuser` | `sanic_CHTblClientDataChangeRequest` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_ChangeRequestID_sanic_chtblclientdatachangerequest` | `sanic_CHTblClientDataChangeRequestLine` | `sanic_CHTblClientDataChangeRequest` | 1:N (lookup `sanic_ChangeRequestID`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_FieldDefinition_sanic_chtblclienttablelogicalfieldname` | `sanic_CHTblClientDataChangeRequestLine` | `sanic_CHTblClientTableLogicalFieldName` | 1:N (lookup `sanic_FieldDefinition`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_SolvedBy_systemuser` | `sanic_CHTblClientDataChangeRequestLine` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedClient_sanic_chtblclient` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClient` | 1:N (lookup `sanic_RelatedClient`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedCRL_sanic_chtblclientdatachangerequestline` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClientDataChangeRequestLine` | 1:N (lookup `sanic_RelatedCRL`) | | | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedRFC_sanic_chtblclientdatachangerequest` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClientDataChangeRequest` | 1:N (lookup `sanic_RelatedRFC`) | | | | Sin revisar | |
| `sanic_chtblclientdeposithistory_Client_sanic_chtblclient` | `sanic_CHTblClientDepositHistory` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_chtblclientemail_Client_sanic_chtblclient` | `sanic_CHTblClientEmail` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_chtblcountedmoneydetails_RelatedDepositRecountingID_sanic_chtbldepositsrecounting` | `sanic_CHTblCountedMoneyDetails` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedDepositRecountingID`) | | | | Sin revisar | |
| `sanic_chtblcountingdesk_Cajero_systemuser` | `sanic_CHTblCountingDesk` | `SystemUser` | 1:N (lookup `sanic_Cajero`) | | | | Sin revisar | |
| `sanic_chtbldeclaration_Client_sanic_chtblclient` | `sanic_CHTblDeclaration` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_chtbldeclaredbags_BagID_sanic_chtblbags` | `sanic_CHTblDeclaredBags` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagID`) | | | | Sin revisar | |
| `sanic_chtbldeclaredbags_DeclarationID_sanic_chtbldeclaration` | `sanic_CHTblDeclaredBags` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_DeclarationID`) | | | | Sin revisar | |
| `sanic_chtbldeclaredchecks_Declaration_sanic_chtbldeclaration` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_Declaration`) | | | | Sin revisar | |
| `sanic_chtbldeclaredchecks_DeclaredBagMirror_sanic_chtbldeclaredbags` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_DeclaredBagMirror`) | | | | Sin revisar | |
| `sanic_chtbldeclaredchecks_DeclaredDepositID_sanic_chtbldeclareddeposits` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaredDeposits` | 1:N (lookup `sanic_DeclaredDepositID`) | | | | Sin revisar | |
| `sanic_chtbldeclaredchecks_MatchedCheck_sanic_chtblchecks` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblChecks` | 1:N (lookup `sanic_MatchedCheck`) | | | | Sin revisar | |
| `sanic_chtbldeclareddeposits_Declaration_sanic_chtbldeclaration` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_Declaration`) | | | | Sin revisar | |
| `sanic_chtbldeclareddeposits_DeclaredBag_sanic_chtbldeclaredbags` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_DeclaredBag`) | | | | Sin revisar | |
| `sanic_chtbldeclareddeposits_RelatedRecountedDeposit_sanic_chtbldepositsrecounting` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedRecountedDeposit`) | | | | Sin revisar | |
| `sanic_chtbldepositsrecounting_BagID_sanic_chtblbags` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagID`) | | | | Sin revisar | |
| `sanic_chtbldepositsrecounting_RecountingSessionMirror_sanic_chtblrecountingsession` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSessionMirror`) | | | | Sin revisar | |
| `sanic_chtbldepositsrecounting_RelatedDeclaredDeposit_sanic_chtbldeclareddeposits` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblDeclaredDeposits` | 1:N (lookup `sanic_RelatedDeclaredDeposit`) | | | | Sin revisar | |
| `sanic_chtblemailsattachments_Email_sanic_chtblreceivedemails` | `sanic_CHTblEmailsAttachments` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_Email`) | | | | Sin revisar | |
| `sanic_chtblreceivedemails_Client_sanic_chtblclient` | `sanic_CHTblReceivedEmails` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | | | Sin revisar | |
| `sanic_CHTblReceivedEmails_Declarations` | `sanic_CHTblDeclaration` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_ReceivedEmail`) | | | | Sin revisar | |
| `sanic_CHTblReceivedEmails_ValidationRuns` | `sanic_CHTblValidationRuns` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_ReceivedEmail`) | | | | Sin revisar | |
| `sanic_chtblreceptionbatch_Location_sanic_chtbllocations` | `sanic_CHTblReceptionBatch` | `sanic_CHTblLocations` | 1:N (lookup `sanic_Location`) | | | | Sin revisar | |
| `sanic_chtblreceptions_BranchName_sanic_chtblclientbranch` | `sanic_CHTblReceptions` | `sanic_CHTblClientBranch` | 1:N (lookup `sanic_BranchName`) | | | | Sin revisar | |
| `sanic_chtblreceptions_ReceptionBatch_sanic_chtblreceptionbatch` | `sanic_CHTblReceptions` | `sanic_CHTblReceptionBatch` | 1:N (lookup `sanic_ReceptionBatch`) | | | | Sin revisar | |
| `sanic_chtblreceptions_RemitentClient_sanic_chtblclient` | `sanic_CHTblReceptions` | `sanic_CHTblClient` | 1:N (lookup `sanic_RemitentClient`) | | | | Sin revisar | |
| `sanic_chtblrecountingsession_Cashier_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_Cashier`) | | | | Sin revisar | |
| `sanic_chtblrecountingsession_ConfirmedBy_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_ConfirmedBy`) | | | | Sin revisar | |
| `sanic_chtblrecountingsession_CountingDesk_sanic_chtblcountingdesk` | `sanic_CHTblRecountingSession` | `sanic_CHTblCountingDesk` | 1:N (lookup `sanic_CountingDesk`) | | | | Sin revisar | |
| `sanic_chtblrecountingsession_ForcedClosureBy_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_ForcedClosureBy`) | | | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_BagId_sanic_chtblbags` | `sanic_CHTblRecountingSessionDetail` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagId`) | | | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_RecountingSessionId_sanic_chtblrecountingsession` | `sanic_CHTblRecountingSessionDetail` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSessionId`) | | | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_SupervisorId_systemuser` | `sanic_CHTblRecountingSessionDetail` | `SystemUser` | 1:N (lookup `sanic_SupervisorId`) | | | | Sin revisar | |
| `sanic_chtbltransactions_RelatedDepositRecounting_sanic_chtbldepositsrecounting` | `sanic_CHTblTransactions` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedDepositRecounting`) | | | | Sin revisar | |
| `sanic_chtbltransactions_TransactionBatch_sanic_transactionbatches` | `sanic_CHTblTransactions` | `sanic_TransactionBatches` | 1:N (lookup `sanic_TransactionBatch`) | | | | Sin revisar | |
| `sanic_chtblvalidationresults_ValidationRule_sanic_validationrules` | `sanic_CHTblValidationResults` | `sanic_ValidationRules` | 1:N (lookup `sanic_ValidationRule`) | | | | Sin revisar | |
| `sanic_chtblvalidationresults_ValidationRun_sanic_chtblvalidationruns` | `sanic_CHTblValidationResults` | `sanic_CHTblValidationRuns` | 1:N (lookup `sanic_ValidationRun`) | | | | Sin revisar | |
| `sanic_chtblxmlreading_RecountingSessionDetailId_sanic_chtblrecountingsessiondetail` | `sanic_CHTblXMLReading` | `sanic_CHTblRecountingSessionDetail` | 1:N (lookup `sanic_RecountingSessionDetailId`) | | | | Sin revisar | |
| `sanic_chtblxmlreadingdetail_RecountingSessionDetailMirror_sanic_chtblrecountingsessiondetail` | `sanic_CHTblXMLReadingDetail` | `sanic_CHTblRecountingSessionDetail` | 1:N (lookup `sanic_RecountingSessionDetailMirror`) | | | | Sin revisar | |
| `sanic_chtblxmlreadingdetail_XMLReading_sanic_chtblxmlreading` | `sanic_CHTblXMLReadingDetail` | `sanic_CHTblXMLReading` | 1:N (lookup `sanic_XMLReading`) | | | | Sin revisar | |
| `sanic_transactionbatches_sanic_AplicadoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_AplicadoPor`) | | | | Sin revisar | |
| `sanic_transactionbatches_sanic_Cajero_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_Cajero`) | | | | Sin revisar | |
| `sanic_transactionbatches_sanic_GeneradoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_GeneradoPor`) | | | | Sin revisar | |
| `sanic_transactionbatches_sanic_IngresadoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_IngresadoPor`) | | | | Sin revisar | |
| `sanic_validationrules_ValidationDependency_sanic_validationrules` | `sanic_ValidationRules` | `sanic_ValidationRules` | 1:N (lookup `sanic_ValidationDependency`) | | | | Sin revisar | |

No se encontraron relaciones N:N (ManyToMany) en `solutions/unpacked/core/Other/Relationships/`.

**Nota de alcance**: se listan solo las relaciones de NEGOCIO (lookup custom entre tablas o hacia SystemUser vía columna `sanic_*`). Se excluyen de esta tabla las relaciones de plomería estándar de Dataverse que existen automáticamente para cada tabla custom (no son decisiones de diseño a revisar):
- **41 relaciones `owner_*`** (OwnerId, propietario de la fila — Owner/Team/SystemUser polimórfico), una por cada una de las 41 tablas `sanic_*` (no aplica a Account/SystemUser, que no son propiedad de esta solución).
- **4 relaciones `FileAttachment_*`** (columnas de tipo *File* — almacenamiento binario nativo de Dataverse, no un lookup de negocio): FileAttachment_sanic_CHTblEmailsAttachments_sanic_FileContent, FileAttachment_sanic_CHTblEmailsAttachments_sanic_JSONData, FileAttachment_sanic_CHTblReceivedEmails_sanic_Attachment, FileAttachment_sanic_TransactionBatches_sanic_CsvCore.
- Las relaciones estándar de {BusinessUnit, Team, SystemUser (createdby/modifiedby)} por cada tabla (`business_unit_*`, `team_*`, `systemuser_*` con atributo `CreatedBy`/`ModifiedBy`/`OwningTeam`/`OwningUser`), presentes también en `solutions/unpacked/core/Other/Relationships/BusinessUnit.xml`, `Team.xml`, `SystemUser.xml`, `Owner.xml`.

Total de relaciones en el export: 318. De negocio (listadas arriba): 66. Boilerplate excluida: 252.

## 4. Option Sets / Choices globales

> Fuente: `solutions/unpacked/core/OptionSets/*.xml` (los 3 únicos option sets **globales** de la solución; prefijo de valor `159460xxx` = `CustomizationOptionValuePrefix=15946` de `Other/Solution.xml`). Hay además ~20 option sets **locales** de una sola tabla (ver `docs/referencia/catalogo-de-choices.md` §2-3) que no son componentes reutilizables independientes — su estado de revisión va atado al campo que los contiene, en la sección 2.

| Nombre | Valores (etiqueta=número) | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|
| `sanic_chchcurrency` (CH-Ch-Currency) | NIO=159460000, USD=159460001, EUR=159460002 | | | | Sin revisar | Usado en DeclaredChecks, DeclaredDeposits, Checks, MoneyDenomination, DepositsRecounting, Transactions (según catalogo-de-choices.md). |
| `sanic_chchrecordstatus` (CH-Ch-RecordStatus) | Active=159460000, Inactive=159460001, PendingApproval=159460002, PendingDeactivation=159460003 | | | | Sin revisar | Usado en Client, ClientBranch, ClientEmail, ClientDepositHistory (`sanic_recordstatus`). `Activaterow`/`Deactivaterow` (flows) lo manipulan. |
| `sanic_chrecountingdetailsstatus` (ch-recountingDetailsStatus) | En progreso=159460000, Cerrado=159460001, Cerrado con diferencias=159460002 | | | | Sin revisar | Usado solo en `RecountingSessionDetail.sanic_recountingsessiondetailstatus`. |

## 5. Variables de entorno

> Fuente: `solutions/unpacked/core/environmentvariabledefinitions/<nombre>/environmentvariabledefinition.xml`. **23 variables** en el repo actual, todas `iscustomizable=1`, `isrequired=0`, `secretstore=0` (ninguna en Azure Key Vault). Tipo `100000000` = String, `100000001` = Number.

| Schema name | Display name | Tipo | Valor default | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_CHEVAADGroup_Admin` | CH-EV-AADGroup_Admin | String | — (sin default) | | | | Sin revisar | Sin referencia textual confirmada de consumo (docs/referencia/variables-de-entorno.md). |
| `sanic_CHEVAADGroup_ClientAdmin` | CH-EV-AADGroup_ClientAdmin | String | — | | | | Sin revisar | Ídem — sin referencia textual confirmada. |
| `sanic_CHEVAADGroup_Counting` | CH-EV-AADGroup_Counting | String | — | | | | Sin revisar | Ídem. |
| `sanic_CHEVAADGroup_EmailValidation` | CH-EV-AADGroup_EmailValidation | String | — | | | | Sin revisar | Ídem. |
| `sanic_CHEVAADGroup_Reception` | CH-EV-AADGroup_Reception | String | — | | | | Sin revisar | Ídem. |
| `sanic_CHEVAADGroup_Supervisor` | CH-EV-AADGroup_Supervisor | String | — | | | | Sin revisar | Ídem. |
| `sanic_CHEVAppUrl_ClientAdmin` | CH-EV-AppUrl_ClientAdmin | String | — | | | | Sin revisar | **Sí se consume**: flows `Notifyreviewers` y `NotifywhenRFCimplementationfinish`, vía `ListRecords` crudo sobre `environmentvariablevalues` (no la acción nativa). |
| `sanic_CHEVAppUrl_Counting` | CH-EV-AppUrl_Counting | String | — | | | | Sin revisar | Sin consumo encontrado en los flujos analizados. |
| `sanic_CHEVAppUrl_EmailValidation` | CH-EV-AppUrl_EmailValidation | String | — | | | | Sin revisar | Sin consumo encontrado. |
| `sanic_CHEVAppUrl_Reception` | CH-EV-AppUrl_Reception | String | — | | | | Sin revisar | Sin consumo encontrado. |
| `sanic_CHEVAppUrl_Supervisor` | CH-EV-AppUrl_Supervisor | String | — | | | | Sin revisar | Sin consumo encontrado. |
| `sanic_CHEVBancaMailbox` | CH-EV-BancaMailbox | String | `cashhub@55xljh.onmicrosoft.com` | | | | Sin revisar | Buzón de Banca de Empresas para recordatorios de alta de remitente. **No documentada** en `docs/referencia/variables-de-entorno.md` (ver discrepancia en el reporte). |
| `sanic_CHEVCsvExportLibraryUrl` | CH-EV-CsvExportLibraryUrl | String | — | | | | Sin revisar | Sin consumo encontrado en flujos ni apps. |
| `sanic_CHEVEmailSubjectToAccept` | CH-EV-EmailSubjectToAccept | String | — | | | | Sin revisar | No consumida — VR03 recibe el asunto como parámetro de entrada del flujo, no lee esta variable. |
| `sanic_CHEVEnvironmentURL` | CH-EV-EnvironmentURL | String | — | | | | Sin revisar | Introducida en versión 1.14.0.0 — carpeta nueva, **aparece como `??` (untracked) en `git status`** al momento de este inventario. No documentada en `docs/referencia/variables-de-entorno.md` (ver discrepancia). |
| `sanic_CHEVFileSizeMaxMB` | CH-EV-FileSizeMaxMB | Number | `5` | | | | Sin revisar | VR05 recibe el máximo como parámetro de entrada; no se confirmó lectura directa de esta variable en `VR05.json`. |
| `sanic_CHEVIbsBranchCodeList` | CH-EV-IbsBranchCodeList | String | — | | | | Sin revisar | **No explotada**: Digitization usa `TRNCODE=8004` fijo y campos de identidad de agencia hardcodeados en vez de resolver contra esta lista. |
| `sanic_CHEVOpsTeamsGroupId` | CH-EV-OpsTeamsGroupId | String | — | | | | Sin revisar | "Group chat de Teams del equipo de operaciones para alertas del barrido de ingesta. Vacío = sin alertas de Teams." Introducida en 1.4.1.0. No documentada en `docs/referencia/variables-de-entorno.md` (ver discrepancia). |
| `sanic_CHEVProcessingLibraryUrl` | CH-EV-ProcessingLibraryUrl | String | — | | | | Sin revisar | Sin consumo encontrado en flujos ni apps. |
| `sanic_CHEVRfcApplyTimeoutMinutes` | CH-EV-RfcApplyTimeoutMinutes | Number | `30` | | | | Sin revisar | "Minutos máximos que un RFC puede permanecer en Approved sin pasar a Applied/Failed antes de que el watchdog lo marque Failed." Consumida por `CH-Flow-RFCWatchdog` (nombre lo indica; confirmar lectura exacta en vivo). No documentada en `docs/referencia/variables-de-entorno.md` (ver discrepancia). |
| `sanic_CHEVSharedMailboxAddress` | CH-EV-SharedMailboxAddress | String | `no-reply-powerplatform-dev@baccredomatic.ni` | | | | Sin revisar | Default de **ambiente dev** literal. **No consumida por `IngestEmails`**: el buzón está hardcodeado en el JSON del trigger. |
| `sanic_CHEVSharedMailboxArchiveFolderID` | CH-EV-SharedMailboxArchiveFolderID | String | `AAMkADgz...` (ID carpeta Outlook) | | | | Sin revisar | **No consumida donde se esperaría**: Email Validation Console tiene `varIDFolderArchivado` hardcodeado en Power Fx. |
| `sanic_CHEVTimezone` | CH-EV-Timezone | String | `America/Managua` | | | | Sin revisar | Sin lectura explícita confirmada vía "Get environment variable"; `ExtractedTablesFromExcel` usa el literal `Central America Standard Time` en su lugar. |

## 6. Roles de seguridad

> Fuente: `solutions/unpacked/core/Roles/*.xml` — 10 roles (9 `CH-SR-*` + `CH-Role`).

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `CH-Role` | | | | Sin revisar | Rol base/plantilla (sin sufijo `SR-`) — confirmar en vivo su propósito exacto respecto a los 9 `CH-SR-*`. |
| `CH-SR-AnalyticsRead` | | | | Sin revisar | |
| `CH-SR-ClientAdminApprover` | Aprobar/rechazar RFC de Client Administration — Write Global sobre cabecera y línea de RFC, Read Global sobre los 4 maestros (Client/ClientBranch/ClientEmail/ClientAccount). | Canvas app `CH-App-ClientAdministration v2`, validado server-side por `GuardChangeRequestApprovalPlugin`/`GuardChangeRequestLinePlugin` y por `sanic_CHResolveChangeRequest`. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §3 | Sin revisar | Read Global sobre `CH-Tbl-ClientAccount` agregado 2026-08-25 (GAP S2, commit `4d527f8`) — confirmado presente en el XML actual. |
| `CH-SR-ClientAdminContributor` | Crear/editar RFC propios de Client Administration — Create/Write Basic sobre cabecera y línea de RFC, Read Global sobre los 4 maestros. | Canvas app `CH-App-ClientAdministration v2`. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §3 | Sin revisar | Read Global sobre `CH-Tbl-ClientAccount` agregado 2026-08-25 (GAP S2, commit `4d527f8`) — confirmado presente en el XML actual. |
| `CH-SR-Counter` | | | | Sin revisar | Cajero de recuento (CH-App-Recounting). |
| `CH-SR-CountingSupervisor` | | | | Sin revisar | Supervisor de conteo (CH-App-Supervisor). |
| `CH-SR-DepositsTracker` | | | | Sin revisar | Rol del proceso de tracking de depósitos. |
| `CH-SR-Digitizer` | | | | Sin revisar | Digitador (CH-App-Digitization) — tiene el privilegio `prvCreatesanic_TransactionBatches` que gatea las Custom APIs de digitación (§9). |
| `CH-SR-OpsAdmin` | | | | Sin revisar | Rol de operaciones/soporte (proceso 08). |
| `CH-SR-Receptionist` | | | | Sin revisar | Oficial de bóveda/recepción (CH-App-Reception). |

## 7. Plugin assembly

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `CashHub.Plugins` | | | | Sin revisar | Única DLL registrable (net462, firmada con `CashHub.snk` — **no versionado en el repo**, prerrequisito de entorno). Compila dentro de sí el dominio puro `CashHub.Validation` (`<Compile Include="../CashHub.Validation/**/*.cs">`). Fuera de las 3 soluciones exportadas: se registra por script (`scripts/register-*.sh`), no por import de solución — ver `docs/soluciones/01-core.md` §7 y `docs/procesos/08-operacion-y-soporte.md` §7. |

## 8. Plugin classes (steps)

> Fuente: `src/backend/CashHub.Plugins/*.cs` — clases que implementan `IPlugin`. De los 36 archivos `.cs` del proyecto, **29 implementan `IPlugin`** (listadas abajo) y **7 son helpers/schemas sin `IPlugin`**, excluidos de esta tabla por instrucción explícita: `BatchTotalsCalculator.cs` (cálculo puro reutilizado por los mirrors de totales), `ClientBranchMirrorSchema.cs` y `SupervisorMirrorSchema.cs` (constantes de nombres de columna), `ClientReplyComposer.cs` (compone la respuesta HTML al cliente, invocado por el motor de validación), `DeclarationMaterializer.cs` (materializa Declaration→DeclaredBags→DeclaredDeposits→DeclaredChecks, invocado por el motor de validación), `ReceivedEmailValidationService.cs` (orquesta las 10 etapas del motor de validación, invocado por `ValidateReceivedEmailPlugin`), `Schema.cs` (constantes de nombres de columna del dominio de emails).
>
> Columna "Mensaje/Etapa": tomada literalmente del comentario XML-doc (`/// <summary>`) de cada clase — es código vivo del repo, no una suposición, pero **no es el mismo artefacto que el registro real en Dataverse** (`sdkmessageprocessingstep`, dado de alta por los scripts `scripts/register-*.sh`). Ante duda sobre si el step vive hoy exactamente así en el entorno, marcar "verificar en vivo" al revisar.

| Clase (.cs) | Mensaje/Etapa | Tabla(s) que toca | Regla de negocio que defiende (resumen 1 línea) | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `ApprovalRequestBagNumberPlugin` | Pre-operation (20), Create | `sanic_chtblapprovalrequest` | Estampa `sanic_bagnumber` (B-6) leído del JSON de `sanic_requestresources` para que la bandeja de aprobaciones busque por número de tula (delegable); nunca bloquea el Create. | | | | Sin revisar | |
| `BagReceptionMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtblbags`; Post-op (40) Update `sanic_chtblreceptions` (cascada paginada de 500) | `sanic_chtblbags`, `sanic_chtblreceptions` | Espeja `sanic_receptionbatchid` (B-2) y `sanic_receiptidmirror` (B-3) de la planilla hacia la tula — el combo/filtro por lote no delega en dos saltos. | | | | Sin revisar | |
| `BagReconciliationMirrorPlugin` | Pre-op (20), Create y Update | `sanic_chtblbags` | Materializa `sanic_decltotalnio/usd/eur` + `sanic_hasdifference` — el join Bag→DeclaredBag no delega en Power Fx. | | | | Sin revisar | |
| `BagRecountClosurePlugin` | Pre-op (20) TOTALES + Post-op (40) ASIENTOS, Update `sanic_chtblbags` filtrado en `sanic_bagstatus` | `sanic_chtblbags`, `sanic_chtbltransactions` | Recalcula TotalNIO/USD/EUR server-side (el cliente puede truncar la colección en memoria y cerrar la tula con menos plata) y crea los asientos `CH-Tbl-Transactions` de forma idempotente al cerrar. | | | | Sin revisar | Puntos 2 y 3 del guard de Recounting v2. |
| `BatchTransitionStampPlugin` | Pre-op (20), Update, filtrado en `sanic_batchstatus`, después del guard | `sanic_transactionbatches` | Sella server-side `sanic_ingresadopor/on` y `sanic_aplicadopor/on` con `InitiatingUserId` y hora del servidor — insumo de la segregación de funciones (quien marcó Ingresado no puede marcar Aplicado). | | | | Sin revisar | |
| `CHPreviewTransactionCutPlugin` | Custom API `sanic_CHPreviewTransactionCut` (no es step sobre mensaje/etapa de tabla) | `sanic_chtbltransactions` (lectura) | Dry-run del corte: cuenta transacciones elegibles y suma totales por moneda con la MISMA query que el corte real, sin crear nada. | | | | Sin revisar | |
| `CHRemoveFromCutPlugin` | Custom API `sanic_CHRemoveFromCut` | `sanic_chtbltransactions`, `sanic_transactionbatches` | Saca una transacción de su lote, persiste el motivo del rechazo del Core y recalcula totales del lote — atómico. | | | | Sin revisar | |
| `CHWorkloadByCashierPlugin` | Custom API `sanic_CHWorkloadByCashier` | `sanic_chtbltransactions`, `sanic_transactionbatches` (aggregate) | Agregado server-side por cajero (pendientes de corte/manual/lotes por aplicar) — reemplaza un GroupBy que no delega en canvas. | | | | Sin revisar | |
| `CheckMirrorStampPlugin` | Pre-op (20) Create + Post-op (40) Create + Pre-op (20) Update, `sanic_chtblchecks`; Post-op (40) Update `sanic_chtblbags` (cascada paginada de 500) | `sanic_chtblchecks`, `sanic_chtbldeclaredchecks`, `sanic_chtblbags` | Espeja `sanic_bag` (B6), `sanic_matchedcheck` (B3) y `sanic_recountingsession` (B-9) del cheque físico — evita dot-walk de dos saltos que no delega y miente el conteo (QA lo probó con 2.074 cheques). | | | | Sin revisar | Create partido en dos pasos por integridad referencial (0x80040265). |
| `CheckStatusStampPlugin` | Pre-op (20), Update, filtrado en `sanic_checkstatus`, después del guard | `sanic_chtblchecks` | Sella fecha/hora y actor (`sanic_senttodigitizationby/on`, `sanic_receivedby/on`, `sanic_notreceivedon`) del handshake de recepción de cheques, server-side. | | | | Sin revisar | |
| `ClientBranchNameMirrorPlugin` | Pre-op (20), Create y Update (filtrado en `sanic_client`) | `sanic_chtblclientbranch` | Espeja `sanic_clientnamemirror` (texto 850) del cliente — el combo de sucursales por nombre de cliente no delega. | | | | Sin revisar | **Sin cascada por rename del cliente** (deliberado) — cobertura vía `scripts/backfill-clientbranch-mirror.py`. |
| `CreateTransactionCutPlugin` | Custom API `sanic_CHCreateTransactionCut` | `sanic_transactionbatches`, `sanic_chtbltransactions` | Corte atómico por cajero: crea el lote `Generado` explícito, asigna en páginas de 500 las transacciones elegibles, borra el lote si queda vacío (regla W6). | | | | Sin revisar | |
| `DeclaredCheckBagMirrorPlugin` | Pre-op (20), Create y Update (filtrado en `sanic_declareddepositid`) | `sanic_chtbldeclaredchecks` | Espejo E-3: `sanic_declaredbagmirror` derivado del depósito declarado — evita ~82 round-trips al abrir una tula. | | | | Sin revisar | Sin cascada desde el depósito declarado (deliberado, respaldado por el orden de `DeclarationMaterializer`). |
| `DepositSessionMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtbldepositsrecounting`; Post-op (40) Update `sanic_chtblbags` (cascada paginada de 500) | `sanic_chtbldepositsrecounting`, `sanic_chtblbags` | Espejo E-1: `sanic_recountingsessionmirror` — la cascada es obligatoria por el flujo de "reabrir tula" (re-cuelga depósitos ya creados de otra sesión). | | | | Sin revisar | |
| `GuardAppCatalogAccessPlugin` | Pre-op, Create y Update | `sanic_chtblappcatalogaccess` | Normaliza el GUID de grupo AAD y bloquea filas duplicadas (misma app + mismo grupo). | | | | Sin revisar | |
| `GuardAppCatalogPlugin` | Pre-validation, Create y Update | `sanic_chtblappcatalog` | Valida que `sanic_appurl` apunte al player de Power Apps y que `sanic_aadgroupid` sea una lista válida de GUIDs separados por `;`. | | | | Sin revisar | Decisión en dominio puro `AppCatalogGuard`. |
| `GuardChangeRequestApprovalPlugin` | Pre-op, Create y Update | `sanic_chtblclientdatachangerequest` | Segregación de funciones: bloquea que un Contributor se auto-apruebe un RFC por Web API/Excel aunque la app oculte los botones. | | | | Sin revisar | Decisión en dominio puro `ChangeRequestApprovalGuard`; resuelve membresía del grupo aprobador. |
| `GuardReceptionPlugin` | Pre-validation, Create/Update/Delete (4 tablas) | Lotes, planillas, tulas y solicitudes de aprobación (recepción) | Impone la máquina de estados de recepción server-side (la v1 solo la imponía en Power Fx) — un único plugin type despacha por entidad/mensaje. | | | | Sin revisar | Decisión en dominio puro `ReceptionGuard`; exige membresía del grupo aprobador (fail-closed) para resolver solicitudes. |
| `GuardRecountingPlugin` | Pre-validation (10), Create/Update/Delete | `sanic_chtbldepositsrecounting`, `sanic_chtblchecks`, `sanic_chtblcountedmoneydetails`, y bajas acotadas de `sanic_chtblapprovalrequest`/`sanic_chtblrecountingsession` | Puntos 4 y 5 del guard de Recounting v2: rechaza escrituras si la tula ancestro no está "Asignada a cajero"; permite bajas solo de lo propio. | | | | Sin revisar | Decisión en dominio puro `RecountingGuard`; excepciones por Depth>1 y por update de solo `sanic_checkstatus`. |
| `GuardSupervisorAccountingPlugin` | Pre-validation (10), Create | `sanic_chtbltransactions` | Recalcula la diferencia Sobrante/Faltante server-side (lee depósito recontado + declarado + asientos existentes) y bloquea montos que no coincidan — mata H-02. | | | | Sin revisar | Decisión en dominio puro `AccountingGuard`; separado de `GuardTransactionPlugin` (dominios distintos). |
| `GuardTransactionPlugin` | Pre-validation, Create/Update (2 tablas), Delete (lote) | `sanic_transactionbatches`, `sanic_chtbltransactions`, `sanic_chtblchecks` (handshake) | Impone la máquina de estados del lote (Generado→Ingresado→Aplicado) y el handshake de cheques server-side; sin excepción por depth (el CutPlugin también debe cumplirla). | | | | Sin revisar | Decisión en dominio puro `TransactionGuard`/`CheckGuard`. |
| `ReceptionBatchStatusMirrorPlugin` | Post-op, Update lote (filtrado en `sanic_receptionstatus`); Pre-op, Create/Update planilla | `sanic_chtblreceptionbatch`, `sanic_chtblreceptions` | Espeja `sanic_batchstatus` del lote en cada planilla — el filtro por estado de lote no delega en la entidad relacionada. | | | | Sin revisar | |
| `ReceptionBatchTotalsPlugin` | Post-op (40), Create/Update/Delete | `sanic_chtblreceptions`, `sanic_chtblreceptionbatch` | Materializa los totales declarados del lote (`sanic_decniocash/check/total`, `sanic_decusdcash/check/total`) sumando planillas. | | | | Sin revisar | Decisión en dominio puro `ReceptionBatchTotalsTrigger`. |
| `RecountingSessionAutoApprovePlugin` | Pre-op (20) + Post-op (40), Create | `sanic_chtblapprovalrequest`, `sanic_chtblrecountingsession` | Auto-aprobación server-side de la sesión de recuento por "mesa propia" — reemplaza el `varIsOwnDesk` client-side que cualquiera podía falsear. | | | | Sin revisar | Punto 1 del guard de Recounting v2; convive con `GuardReceptionPlugin` (Pre-validation). |
| `TransactionBatchStatusMirrorPlugin` | Post-op, Update lote (propaga); Pre-op, Create/Update transacción | `sanic_transactionbatches`, `sanic_chtbltransactions` | Espeja `sanic_batchstatus` en cada transacción — el filtro "sin lote"/"por estado de lote" no delega. | | | | Sin revisar | |
| `TransactionBatchTotalsPlugin` | Post-op (40), Update, filtrado en `sanic_transactionbatch`, con pre-image | `sanic_chtbltransactions`, `sanic_transactionbatches` | Recomputa totales del lote viejo y del nuevo al reasignar/desasignar una transacción individual. | | | | Sin revisar | Se corta a Depth>1 (el corte masivo ya recomputa una sola vez). |
| `TransactionRoutingPlugin` | Pre-op (20), Create y Update | `sanic_chtbltransactions` | Ruteo server-side a proceso Batch o Manual — la regla estaba duplicada en 8 sitios Power Fx y ninguno aplicaba "sin cuenta → Manual". | | | | Sin revisar | |
| `ValidateReceivedEmailPlugin` | Custom API `sanic_CHValidateReceivedEmail` (sync) + Post-operation async, Create | `sanic_chtblreceivedemails` | Motor de validación de correos: 28 reglas (VR-01..VR-28), triage de remitente, materialización de la declaración, composición de respuesta. | | | | Sin revisar | Guarda anti-bucle Depth>2; corre como sistema. El paso asíncrono en Create fue retirado según `docs/soluciones/04-backend.md` — verificar en vivo si el doc-comment de la clase todavía lo describe así. |
| `XmlReadingDetailMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtblxmlreadingdetail`; Post-op (40) Update `sanic_chtblxmlreading` (cascada paginada de 500) | `sanic_chtblxmlreadingdetail`, `sanic_chtblxmlreading` | Espejo E-4 (dos columnas): `sanic_recountingsessiondetailmirror` + `sanic_xmlreadingactiva` — sin la cascada, Q17 sigue sumando la lectura vieja de la contadora. | | | | Sin revisar | |

## 9. Custom APIs

> Fuente: `docs/procesos/06-digitacion-core-bancario.md` §7, `docs/procesos/08-operacion-y-soporte.md` §7, `docs/soluciones/01-core.md` §7, y los `scripts/register-*.sh`. Las Custom APIs **no se exportan como XML** en `solutions/unpacked/` (se registran fuera de solución por script) — su definición formal vive en el código de los scripts de registro, no en el export.

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_CHValidateReceivedEmail` | | | | Sin revisar | Backing: `CashHub.Plugins.ValidateReceivedEmailPlugin`. Script: `register-validation-plugin.sh`. `executeprivilegename`: **ninguno registrado** — abierto a cualquiera con acceso de escritura a la tabla. In: `ReceivedEmailId`; out: `RunId`, `Result`, `FailedFindings`. |
| `sanic_CHWorkloadByCashier` | | | | Sin revisar | Backing: `CashHub.Plugins.CHWorkloadByCashierPlugin`. Script: `register-workload.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. Out: `Workload` (JSON). |
| `sanic_CHPreviewTransactionCut` | | | | Sin revisar | Backing: `CashHub.Plugins.CHPreviewTransactionCutPlugin`. Script: `register-preview-cut.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `CajeroId`; out: `EligibleCount`, `TotalNIO`/`USD`/`EUR`. |
| `sanic_CHRemoveFromCut` | | | | Sin revisar | Backing: `CashHub.Plugins.CHRemoveFromCutPlugin`. Script: `register-remove-from-cut.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `TransactionId`, `Motivo` (opc.); out: `BatchId`, totales nuevos. |
| `sanic_CHCreateTransactionCut` | | | | Sin revisar | Backing: `CashHub.Plugins.CreateTransactionCutPlugin`. Script: `register-transaction-cut.sh` (confirmado por grep directo — `uniquename eq 'sanic_CHCreateTransactionCut'`). `executeprivilegename`: `prvCreatesanic_TransactionBatches` (según docs/procesos/06, que sí la documenta). In: `CajeroId`; out: `BatchId`, `BatchName`, `AssignedCount`. **Falta en la tabla de `docs/procesos/08-operacion-y-soporte.md` §7** (ver discrepancia en el reporte). |

**Nota — Custom API diseñada pero NO implementada**: `docs/procesos/03-recepcion-tulas-esclusa.md` §7 documenta `sanic_CloseReceptionBatch` como diseño planeado (ítem B0-3) para el cierre atómico de lote. **No existe** en el repo — no hay clase `.cs` que la respalde ni script `register-*.sh` que la dé de alta. No se agrega como fila porque este inventario es de componentes existentes; queda como referencia si el usuario quiere marcarlo como pendiente de construir.

## 10. Canvas Apps

> Fuente: `solutions/unpacked/apps/CanvasApps/*.meta.xml` (solución `CashHubApps` v1.31.0.4). 10 carpetas — 9 propias de CashHub + 1 librería de componentes de Microsoft.

| Nombre interno | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_chapphub_2038c` (CH-App-Hub) | | | | Sin revisar | |
| `sanic_chappreception_9ff29` (CH-App-Reception) | | | | Sin revisar | Reconstruida v2 según CHANGELOG — mandan las guías de rebuild, no este doc. |
| `sanic_chapprecounting_54962` (CH-App-Recounting) | | | | Sin revisar | Reconstruida v2. |
| `sanic_chappsupervisor_75678` (CH-App-Supervisor) | | | | Sin revisar | Reconstruida v2. |
| `sanic_chappdigitization_278aa` (CH-App-Digitization) | | | | Sin revisar | Reconstruida v2 (Apps 1.32.0.5 según CHANGELOG reciente). |
| `sanic_chappdepositstracker_2b2c3` (CH-App-DepositsTracker) | | | | Sin revisar | |
| `sanic_chappclientadministration_fd97f` (CH-App-ClientAdministration) | | | | Sin revisar | |
| `sanic_chappemailvalidationconsole_f3b93` (CH-App-IngestionConsole) | | | | Sin revisar | Nombre interno dice "EmailValidationConsole", DisplayName real es "CH-App-IngestionConsole" — confirmar cuál es el nombre vigente al revisar. |
| `sanic_chclsharedcomponents_85fca` (CH-CL-SharedComponents) | | | | Sin revisar | Librería de componentes compartidos (no es una app operable por sí misma). |
| `cat_powercatcomponentlibrary_0be3a` (Power CAT Component Library) | | | | Sin revisar | **Third-party, de Microsoft — no revisar** como componente propio de CashHub. |

## 11. Cloud Flows

> Fuente: `solutions/unpacked/flow/Workflows/*.json.data.xml` (solución `CashHubFlows` v1.12.0.0, export al 2026-08-24). **19 flujos** en el repo actual — más que los 16 documentados en `docs/soluciones/02-flujos.md` (fechado a la versión 1.6.5.0, desactualizado; ver discrepancia en el reporte). Estado ON/OFF leído de `<StateCode>`/`<StatusCode>` de cada `.json.data.xml`: `StateCode=1`/`StatusCode=2` = Activado (ON); `StateCode=0`/`StatusCode=1` = Draft (OFF). **CAVEAT del CONTRATO.md regla 2: esta es una foto del export, no del entorno vivo** — hubo reparaciones/reactivaciones posteriores por API (ver CHANGELOG.md, "Apply client data changes").

| Nombre | Estado (ON/OFF según export) | Objetivo | Dónde se usa | Documentos de referencia | Estado revisión | Notas |
|---|---|---|---|---|---|---|
| `CH-Flow-IngestEmails` | ON | | | | Sin revisar | Trigger Outlook `SharedMailboxOnNewEmailV2`. Buzón hardcodeado en el JSON (no lee `sanic_CHEVSharedMailboxAddress`) — según export, puede estar desactualizado, ver CHANGELOG.md. |
| `CH-Flow-ProcessEmail` | ON | | | | Sin revisar | Hijo de IngestEmails/MailboxSweep — según export, puede estar desactualizado, ver CHANGELOG.md. |
| `CH-Flow-MailboxSweep` | ON | | | | Sin revisar | Recurrence 10 min — según export, puede estar desactualizado, ver CHANGELOG.md. |
| `CH-Flow-Replytoclientemail` (Reply to client email) | ON | | | | Sin revisar | Según export, puede estar desactualizado, ver CHANGELOG.md. |
| `CH-Flow-Notifyreviewers` (Notify reviewers) | **OFF** (Draft) | Avisar por Teams a los Approvers de la tabla objetivo cuando un RFC pasa a Submitted. | RFC de Client Administration (`sanic_chtblclientdatachangerequest`). | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | **DISCREPANCIA CONFIRMADA 2026-08-25** (verificado `StateCode=0` en el XML, contrastado contra guía 01): el diseño lo da por ON/vigente pero el export lo tiene OFF — si es real, los aprobadores no reciben aviso. Verificar en vivo. |
| `CH-Flow-Applyclientdatachanges` (Apply client data changes) | **OFF** (Draft) | (Retirado por diseño) Materializar por campo un RFC de Update aprobado — reemplazado por `sanic_CHResolveChangeRequest`. | — (retirado; ver guía 01 §5 histórico) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado, coincide con el diseño (a diferencia de Notifyreviewers/Createclientdata/RFCWatchdog). |
| `CH-Flow-Createclientdata` (Create client data) | ON | (Retirado por diseño) Materializar un RFC de Create aprobado — reemplazado por `sanic_CHResolveChangeRequest`. | — (debería estar retirado; ver riesgo en Notas) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5, §10 | Sin revisar | **DISCREPANCIA CONFIRMADA 2026-08-25** (`StateCode=1`): el diseño lo da por retirado pero sigue ON — riesgo de materializar en paralelo con la Custom API si algún RFC vuelve a quedar en `Approved`. Verificar en vivo. |
| `CH-Flow-Activaterow` (Activate row) | **OFF** (Draft) | (Retirado por diseño) Activar/desactivar registro vía RFC — reemplazado por `sanic_CHResolveChangeRequest`. | — (retirado; ver guía 01 §5 histórico) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado, coincide con el diseño. |
| `CH-Flow-Deactivaterow` (Deactivate row) | **OFF** (Draft) | (Retirado por diseño) Ídem Activate row, para desactivación. | — (retirado; ver guía 01 §5 histórico) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado, coincide con el diseño. |
| `CH-Flow-NotifywhenRFCimplementationfinish` (Notify when RFC implementation finish) | ON | Avisar por Teams cuando un RFC llega a un estado final (Applied/Rejected/Failed). | RFC de Client Administration. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado ON confirmado, coincide con el diseño. |
| `CH-Flow-RFCWatchdog` | ON | (Retirado por diseño) Barrido cada 15 min de RFC en `Approved` vencidos, marcarlos `Failed` — red de contención que dejó de tener sentido con la resolución síncrona. | — (debería estar retirado; ver riesgo en Notas) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5, §10 | Sin revisar | **DISCREPANCIA CONFIRMADA 2026-08-25** (`StateCode=1`): el diseño lo da por retirado pero sigue ON, recurrence 15 min intacto — consumidor probable de `sanic_CHEVRfcApplyTimeoutMinutes`. Riesgo: podría marcar `Failed` un RFC que la Custom API ya resolvió por otra vía. Verificar en vivo. |
| `CH-Flow-CreateaTeamschatgroupbasedonOffice365Group` (Create a Teams chat group based on Office 365 Group Members) | ON | | | | Sin revisar | Hijo de `Notifyreviewers`. |
| `CH-Flow-ExtractedTablesFromExcel` | ON | | | | Sin revisar | (Legado) invocable desde canvas apps. |
| `CH-Flow-GetJSONdatafromattachment` (Get JSON data from attachment) | ON | | | | Sin revisar | (Legado) invocable desde canvas apps. |
| `CH-Flow-DownloadData` | ON | | | | Sin revisar | Usada por CH-App-Digitization. |
| `CH-Flow-ProcessXMLFiles` (Process XML Files) | **OFF** (Draft) | | | | Sin revisar | Usada por CH-App-Recounting — verificar en vivo si sigue OFF pese a ser consumida por una app activa. |
| `CH-Flow-CreateCut` | ON | | | | Sin revisar | **No documentado** en `docs/soluciones/02-flujos.md` §Resumen (solo lista 16 flujos) — flujo agregado después de esa versión (1.6.5.0). Nombre sugiere relación con el corte de transacciones (§9 Custom APIs) — confirmar en vivo su rol real, dado que el corte también existe como Custom API (`sanic_CHCreateTransactionCut`). |
| `CH-Flow-UnblockOnClient` | ON | | | | Sin revisar | **No documentado** en `docs/soluciones/02-flujos.md` §Resumen — flujo agregado después de esa versión. |
| `CH-Flow-UnblockOnClientEmail` | ON | | | | Sin revisar | **No documentado** en `docs/soluciones/02-flujos.md` §Resumen — flujo agregado después de esa versión. |

## Resumen por categoría

| Categoría | Cantidad de ítems | Sin revisar |
|---|---|---|
| 1. Tablas (Entities) | 44 | 44 |
| 2. Campos por tabla (custom) | 345 | 345 |
| 3. Relaciones (1:N y N:N, de negocio) | 66 | 66 |
| 4. Option Sets / Choices globales | 3 | 3 |
| 5. Variables de entorno | 23 | 23 |
| 6. Roles de seguridad | 10 | 10 |
| 7. Plugin assembly | 1 | 1 |
| 8. Plugin classes (steps) | 29 | 29 |
| 9. Custom APIs | 5 | 5 |
| 10. Canvas Apps | 10 | 10 |
| 11. Cloud Flows | 19 | 19 |
| **Total** | **555** | **555** |

> Nota de alcance: la sección 3 lista solo relaciones de negocio (66 de 318 relaciones totales en el export); las 252 restantes son plomería estándar de Dataverse (owner, business unit, team, file attachment, createdby/modifiedby) documentada en la nota al pie de la sección 3, no items a revisar uno por uno.
