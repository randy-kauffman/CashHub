# CashHubCore — componente por componente

> Fuente: `solutions/unpacked/core/` (versión **1.6.0.0**, unmanaged). Todo lo
> de este documento está leído de los XML desempaquetados; el "dónde se ocupa"
> sale de cruzar cada nombre lógico contra `flow/Workflows/`, las fuentes de las
> canvas apps (`DataSources/` declarados + referencias reales en `Src/`) y
> `src/backend/`.

## Qué es esta solución

El modelo de datos y la seguridad de CashHub: 40 tablas custom, 8 roles, 3
option sets globales, 21 variables de entorno y 4 connection references. **No
contiene código**: ni plugin assemblies, ni Custom APIs, ni web resources — el
motor de validación se registra por fuera (ver [§7](#7-custom-api-y-plugin-fuera-de-la-solución)).

RootComponents declarados: 42 entidades (las 40 `sanic_*` + `account` y
`systemuser` como shells de referencia), 8 roles, 3 option sets globales y la
imagen de `sanic_CHTblAppCatalog`. **Las variables de entorno y las connection
references existen como componentes pero NO figuran en `<RootComponents>`** —
rareza de empaquetado a tener en cuenta en ALM.

---

## 1. Tablas por dominio funcional

Convención: todas usan `sanic_name` como columna primaria y son user/team
owned. Solo 3 tienen descripción a nivel entidad (AppCatalog, Playbook,
RecountingSessionDetail).

### 1.1 Catálogo / plataforma

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblAppCatalog` (CH-Tbl-AppCatalog) | Catálogo data-driven de las apps que muestra el Hub: título, descripción, ícono (única tabla con columna imagen), URL, grupo AAD que la habilita, orden. Clave alterna `sanic_appkey`. | App **Hub** (galería de lanzamiento). Rol `CH-SR-Hub` da el Read. |
| `sanic_CHTblPlaybook` (CH-Tbl-Playbook) | Instructivo "Qué hacer" por categoría de la bandeja de contingencia (`cliente-sin-registrar`, `sospechoso`, `error-sistema`, `respuesta-sin-enviar`). Editable por negocio sin desplegar la app. | App **Email Validation Console** (bloque "Que hacer"). |
| `sanic_CHTblSystemHeartbeat` (CH-Tbl-SystemHeartbeat) | Latido de procesos automáticos: `sanic_lastrunon` + `sanic_details`. Hoy una sola fila lógica: `ingesta-correos`. | Escribe **CH-Flow-MailboxSweep**; lee la **Console** (semáforo de salud del pipeline). |
| `sanic_CHTblEmailTemplate` (CH-Tbl-EmailTemplate) | Plantillas editables de la respuesta al cliente (`reply-accepted`, `reply-rejected`): asunto + HTML con tokens `{{...}}`. | Backend **ClientReplyComposer** (con fallback embebido si no hay plantilla activa). Declarada como datasource en la Console pero sin uso en pantalla. |
| `sanic_CHTblApprovalRequest` (CH-Tbl-ApprovalRequest) | Cola genérica de aprobaciones del supervisor: tipo, detalle, recursos, estado (Aprobado/Rechazado), resuelto por/cuándo. | Crean solicitudes **Reception** y **Recounting**; las resuelve **Supervisor**. |

### 1.2 Cliente y datos maestros

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblClient` (CH-Tbl-Client) | Hub del dominio cliente. Claves alternas por `sanic_cifbco` y `sanic_cifcom`; `sanic_recordstatus` (global RecordStatus, default PendingApproval). Referenciada por 8 tablas. | Flujos RFC (Activate/Apply/Create/Deactivate); app **Client Administration**; backend (VR-25 atribuye por CIF, materializador liga la Declaration). |
| `sanic_CHTblClientAccount` (CH-Tbl-ClientAccount) | Cuentas bancarias del cliente. AK sobre `sanic_name` (número de cuenta); moneda global. | Flujos Activate/Deactivate row; **Client Administration**; backend (VR-23: la cuenta pertenece al cliente). |
| `sanic_CHTblClientBranch` (CH-Tbl-ClientBranch) | Sucursales del cliente. | Flujos Activate/Deactivate row; **Client Administration**; **Reception** (`sanic_BranchName` de la recepción). |
| `sanic_CHTblClientEmail` (CH-Tbl-ClientEmail) | Remitentes autorizados por cliente. AK compuesta `sanic_client`+`sanic_name`: el mismo correo puede servir a varios clientes, pero no duplicarse dentro de uno. | Flujos Activate/Deactivate row; **Client Administration**; backend (VR-03 remitente registrado, VR-26 correo autorizado). La Console la registra vía RFC, no directo. |
| `sanic_CHTblClientDepositHistory` (CH-Tbl-ClientDepositHistory) | Historial de depósitos por cliente. | **Huérfana funcional**: solo está declarada como datasource en Client Administration, sin referencias en pantalla, flujos ni backend. |

### 1.3 RFC — solicitudes de cambio sobre datos de cliente

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblClientDataChangeRequest` (CH-Tbl-ClientDataChangeRequest) | Cabecera del RFC: tipo (Update/Deactivate/Create/Activate), estado (Draft…Canceled), tabla y registro destino, resolución y notas de aplicación. Conserva el atributo legado `cr140_changestatus` de publisher ajeno (única columna no-`sanic_` de la solución). | Todos los flujos del dominio RFC + RFCWatchdog; apps **Client Administration** y **Console** (alta de remitentes). |
| `sanic_CHTblClientDataChangeRequestLine` (CH-Tbl-ClientDataChangeRequestLine) | Línea del RFC: campo (lookup al metamodelo), valor viejo/nuevo, estado propio. | Flujos Apply/Create/Notify reviewers; **Client Administration**; **Console**. |
| `sanic_CHTblClientTableLogicalFieldName` (CH-Tbl-ClientTableLogicalFieldName) | **Metamodelo**: qué campos de qué tablas son editables vía RFC, con tipo de dato (Text…TwoOptions), mapa de option sets y tabla destino de lookups. AK compuesta `sanic_table`+`sanic_logicalname`. | Flujos Apply/Create (conversión por tipo); **Client Administration** y **Console** (formularios dinámicos). |
| `sanic_CHTblClientDataChangePermission` (CH-Tbl-ClientDataChangePermission) | Mapa grupo AAD → rol funcional (Contributor/Approver) por tabla, más el `teamsgroupid` del chat de aprobadores. | Flujo Notify reviewers; **Client Administration** y **Console** (visibilidad de botones). |
| `sanic_CHTblClientDataChangesAuditTrail` (CH-Tbl-ClientDataChangesAuditTrail) | Bitácora del proceso RFC: operación (typo grabado: `sanic_opeationtype`), resultado, mensaje; apunta a cliente, RFC y línea. | Flujos Activate/Apply/Create/Deactivate/RFCWatchdog; **Client Administration** (pestaña Bitácora). |

### 1.4 Ingesta y validación de correos

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblReceivedEmails` (CH-Tbl-Received Emails) | Cabecera del correo recibido y **bandeja de trabajo de la Console**. AK `sanic_KeyInternetMessageId` (dedup de ingesta). Estados: `validationstatus`, `emailreviewstatus`, `emailreviewmethod`, `emailreplystatus`, `senderclassification`; más triage (`triagenotes`), override auditado (`overrideby/on/reason`) y la respuesta compuesta (`replysubject`, `replybodyhtml`, ids de Outlook). | Flujos MailboxSweep/ProcessEmail/Reply to client email; apps **Console** y **Recounting**; backend (el plugin la lee y le escribe el veredicto completo). |
| `sanic_CHTblEmailsAttachments` (CH-Tbl-EmailsAttachments) | Adjuntos del correo con **dos columnas file**: `sanic_filecontent` (el xlsx) y `sanic_jsondata` (legado de la extracción vía Excel Online). | Flujos ProcessEmail (sube el archivo), ExtractedTablesFromExcel y GetJSONdatafromattachment (legado); **Console**; backend (descarga por bloques y parsea). |
| `sanic_ValidationRules` (CH-Tbl-ValidationRules — sin `CHTbl` en el nombre lógico) | Catálogo paramétrico de las 28 reglas VR-01..VR-28: orden de ejecución, valor parámetro, mensaje de fallo, dependencia (lookup auto-referencial) y activo sí/no. Seed: `data/seed/50-validationrules.json`. | Backend (el motor las carga todas en cada corrida; si está vacía, error explícito); **Console** (lista de reglas activas y "no intentadas"). |
| `sanic_CHTblValidationRuns` (CH-Tbl-ValidationRuns) | Una corrida del motor por validación: inicio/fin, resultado Pass/Fail, origen polimórfico textual (`sourceentity`+`sourcerecordid`). | Backend (crea una por corrida); MailboxSweep (¿el correo pendiente ya tiene run?); **Console** (última corrida del correo). |
| `sanic_CHTblValidationResults` (CH-Tbl-ValidationResults) | Resultado por regla dentro de una corrida: Pass/Fail, detalle, ubicación (`sheet`/`cellref`/`rownumber`), duración. Las reglas Skipped no generan fila. | Backend (las crea); **Console** (bloque "Reglas que no pasaron"). |

### 1.5 Declaración (lo que el cliente dice que envió)

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblDeclaration` (CH-Tbl-Declaration) | Cabecera de la declaración digital materializada de un correo VALIDO; origen polimórfico textual. | Backend **DeclarationMaterializer** (la crea). Datasource declarada en Console y Recounting sin referencia en pantalla. |
| `sanic_CHTblDeclaredBags` (CH-Tbl-DeclaredBags) | Tula declarada; **puente declarado↔físico** vía `sanic_BagID` → Bags. Totales por moneda; `sanic_totaldeposits` es rollup (las escrituras se ignoran). | Backend (la crea; también para el resumen de la respuesta al cliente); apps **Recounting** y **Supervisor** (conciliación). |
| `sanic_CHTblDeclaredDeposits` (CH-Tbl-DeclaredDeposits) | Depósito declarado; `sanic_relatedrecounteddeposit` → DepositsRecounting para conciliar 1:1 con lo recontado. `sanic_accountnumber` es **int** (deuda de esquema conocida). | Backend (la crea); **Recounting** y **Supervisor**. |
| `sanic_CHTblDeclaredChecks` (CH-Tbl-DeclaredChecks) | Cheque declarado (número, monto, moneda, tipo BAC Local/Otros Local). | Backend (la crea); **Recounting** (conciliación de cheques). |

### 1.6 Recepción física

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblReceptionBatch` (CH-Tbl-ReceptionBatch) | Lote de recepción de la transportadora; estado En progreso/Recibido/Anulado/En bóveda. `sanic_transportadora` es texto libre (no lookup, pese a existir TransportCompany). | App **Reception** (crea/cierra); **Deposits Tracker** y **Supervisor**. |
| `sanic_CHTblReceptions` (CH-Tbl-Receptions) | Planilla individual dentro del lote: cliente remitente, sucursal, totales efectivo/cheque por moneda. | **Reception** (captura); **Recounting**, **Supervisor**, **Deposits Tracker**. |
| `sanic_CHTblBags` (CH-Tbl-Bags) | **Entidad central del flujo físico**: la tula. Estado de custodia (esclusa → bóveda → cajero → recontada → revisiones), lookups a Receptions, DeclaredBags y RecountingSession, totales por moneda. | **Reception** (alta), **Recounting**, **Supervisor** (localización), **Deposits Tracker**. |
| `sanic_CHTblLocations` (CH-Tbl-Locations) | Catálogo de ubicaciones (solo `sanic_name`). | **HUÉRFANA**: ningún flujo, app ni backend la consume (solo permisos en 3 roles y el lookup desde ReceptionBatch, sin uso). |
| `sanic_CHTblTransportCompany` (CH-Tbl-TransportCompany) | Catálogo de transportadoras (solo `sanic_name`). Sin ninguna relación entrante. | App **Reception** (selector). |

### 1.7 Recuento

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblCountingDesk` (CH-Tbl-CountingDesk) | Mesa de conteo: cajero asignado, serie de la máquina contadora (BPSC1), habilitada sí/no. | **Recounting** (selección de mesa); **Supervisor** (datasource sin uso en pantalla). |
| `sanic_CHTblRecountingSession` (CH-Tbl-RecountingSession) | Sesión de recuento del cajero: mesa, cajero, confirmación y cierre forzado (3 lookups a SystemUser). | **Recounting** (sesión del día); **Supervisor**; puente desde Bags. |
| `sanic_CHTblRecountingSessionDetail` (CH-Tbl-RecountingSessionDetail) | Línea sesión × tula, con supervisor y estado (global RecountingDetailsStatus). | **Recounting** y **Supervisor**; flujo ProcessXMLFiles (bind de lecturas XML). |
| `sanic_CHTblDepositsRecounting` (CH-Tbl-DepositsRecounting) | **Espejo recontado de DeclaredDeposits**: lo que realmente había en la tula. La tabla más referenciada del dominio (la apuntan Checks, CountedMoneyDetails, DeclaredDeposits y Transactions). Typo grabado: `sanic_otherlocacheckamount`. | **Recounting** (captura), **Supervisor**, **Deposits Tracker**; Digitization la declara sin uso. |
| `sanic_CHTblCountedMoneyDetails` (CH-Tbl-CountedMoneyDetails) | Desglose por denominación de lo recontado (denominación, tipo, cantidad, total). | **Recounting**. |
| `sanic_CHTblChecks` (CH-Tbl-Checks) | Cheque físico recontado, conciliado contra el declarado (`sanic_RelatedDeclaredCheck`); estado Recontado → Enviado a digitación → Digitado/Rebotado. | **Recounting** (captura), **Supervisor** (envío a digitación), **Digitization**, **Deposits Tracker**. |
| `sanic_CHTblMoneyDenomination` (CH-Tbl-MoneyDenomination) | Catálogo de denominaciones por moneda (billete/moneda, habilitada). | **Recounting** (colección de denominaciones). |

### 1.8 Lectura XML de máquinas contadoras

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_CHTblXMLReading` (CH-Tbl-XMLReading) | Un archivo XML de la contadora ligado al detalle de sesión; `sanic_isactive` marca la lectura vigente. | Flujo **ProcessXMLFiles** (hoy apagado); **Recounting**. |
| `sanic_CHTblXMLReadingDetail` (CH-Tbl-XMLReadingDetail) | Detalle por denominación del XML. `sanic_currency` es **nvarchar** (inconsistente con el global Currency del resto del modelo). | Flujo **ProcessXMLFiles**; **Recounting**. |

### 1.9 Transacciones / salida al core bancario

| Tabla | Objetivo | Dónde se ocupa |
|---|---|---|
| `sanic_TransactionBatches` (CH-Tbl-TransactionBatches — sin `CHTbl` en el nombre lógico) | Lote de transacciones para IBS Branch: Generado → Ingresado → Aprobado → Aplicado. | **Digitization** (corte, marcado, descarga). |
| `sanic_CHTblTransactions` (CH-Tbl-Transactions) | Transacción individual: tipo (Depósito/Faltante/Sobrante/Sin declaración), montos por instrumento, moneda. `sanic_accountnumber` acá es **decimal** (en Declared/Recounting es int); `sanic_transactionstatus` tiene un único valor (Aplicada). | **Recounting** y **Supervisor** (posteo), **Digitization** (aplicación). |

### 1.10 Claves alternas (todas)

| Tabla | Clave | Atributos |
|---|---|---|
| AppCatalog | `sanic_AppKey_Unique` | `sanic_appkey` |
| Client | `sanic_AlternetKey_CIFBCO` / `sanic_AlternetKey_CIFCOM` | `sanic_cifbco` / `sanic_cifcom` |
| ClientAccount | `sanic_KeyNumeroDeCuenta` | `sanic_name` |
| ClientEmail | `sanic_KeyCorreoPorCliente` | `sanic_client` + `sanic_name` |
| ClientTableLogicalFieldName | `sanic_AK_TableLogicalName_FieldLogicalName` | `sanic_logicalname` + `sanic_table` |
| ReceivedEmails | `sanic_KeyInternetMessageId` | `sanic_receivedemailinternetmessageid` |

---

## 2. Roles de seguridad (`core/Roles/`)

8 roles. Resumen de cobertura sobre tablas CH:

| Rol | Para qué sirve | Cobertura |
|---|---|---|
| `CH-SR-Hub` | Rol mínimo "poder abrir el Hub": baseline de plataforma + Read de AppCatalog y AppCatalogAccess. Renombrado desde `CH-Role` el 2026-08-31 (mismo GUID). | 2 tablas |
| `CH-SR-Receptionist` | Recepción de valores. | AppCatalog, ApprovalRequest, Bags, DeclaredBags, Locations, ReceptionBatch, Receptions, TransportCompany |
| `CH-SR-Counter` | Cajero de recuento. | 18 tablas: todo recepción + recuento + transacciones, incl. CountedMoneyDetails |
| `CH-SR-CountingSupervisor` | Supervisor de recuento. | 17 tablas (las de Counter menos CountedMoneyDetails, más ApprovalRequest con Append/Assign) |
| `CH-SR-ClientAdminApprover` / `CH-SR-ClientAdminContributor` | Administración de datos de cliente. | Las 9 tablas del dominio cliente+RFC. **Idénticos entre sí**: la diferencia Approver/Contributor NO vive en el rol sino en `ClientDataChangePermission.sanic_type` + grupos AAD. |
| `CH-SR-OpsAdmin` / `CH-SR-AnalyticsRead` | **Incompletos**: byte a byte idénticos, solo privilegios de plataforma, cero tablas de negocio. | 0 tablas |

No existe un rol para la Email Validation Console ni para Digitization: hoy ese
acceso depende de roles fuera de esta lista o de asignación directa.

---

## 3. Option sets globales (`core/OptionSets/`)

| Option set | Valores | Usado por |
|---|---|---|
| `sanic_chchcurrency` (CH-Ch-Currency) | 159460000 NIO · …001 USD · …002 EUR | Transactions, DeclaredDeposits, DeclaredChecks, MoneyDenomination, Checks, ClientAccount, DepositsRecounting |
| `sanic_chchrecordstatus` (CH-Ch-RecordStatus) | Active · Inactive · PendingApproval · PendingDeactivation | Client (default PendingApproval), ClientAccount, ClientBranch, ClientEmail, ClientDepositHistory — ojo: ClientAccount, ClientBranch y ClientEmail tienen **dos columnas** apuntando al mismo global (duplicación grabada) |
| `sanic_chrecountingdetailsstatus` (ch-recountingDetailsStatus — rompe la convención CH-Ch-) | En progreso · Cerrado · Cerrado con diferencias | RecountingSessionDetail |

Los ~35 option sets locales relevantes (estados de correo, RFC, tulas, cheques,
transacciones) están descritos en cada tabla de la sección 1. La solución Flow
replica estos 3 globales en su propio paquete.

---

## 4. Variables de entorno (21, `core/environmentvariabledefinitions/`)

Ninguna es secreta ni requerida; ninguna trae `environmentvariablevalue` (solo
definición y, en 4 casos, default).

| Grupo | Variables | Quién las consume hoy |
|---|---|---|
| Grupos AAD | `sanic_CHEVAADGroup_Admin`, `_ClientAdmin`, `_Counting`, `_EmailValidation`, `_Reception`, `_Supervisor` | Nadie en el código actual (las apps resuelven grupos vía `CH-Tbl-AppCatalog` y `ClientDataChangePermission`). |
| URLs de apps | `sanic_CHEVAppUrl_ClientAdmin`, `_Counting`, `_EmailValidation`, `_Reception`, `_Supervisor` | `_ClientAdmin` la leen los flujos Notify reviewers / Notify when RFC finish (deep-link `&RFC=`). Las demás, sin consumo detectado. |
| Buzón compartido | `sanic_CHEVSharedMailboxAddress` (default `no-reply-powerplatform-dev@baccredomatic.ni`), `sanic_CHEVSharedMailboxArchiveFolderID` (default: ID de carpeta Outlook concreto) | IngestEmails, MailboxSweep, Reply to client email; Console (lectura Graph y archivado). **Defaults con valores de DEV embebidos — riesgo de ALM.** |
| Operación | `sanic_CHEVOpsTeamsGroupId` (alertas del barrido; vacío = solo heartbeat), `sanic_CHEVRfcApplyTimeoutMinutes` (Number, default 30) | MailboxSweep; RFCWatchdog. |
| Sin consumo detectado | `sanic_CHEVCsvExportLibraryUrl`, `sanic_CHEVEmailSubjectToAccept`, `sanic_CHEVFileSizeMaxMB` (Number, default 5), `sanic_CHEVIbsBranchCodeList`, `sanic_CHEVProcessingLibraryUrl`, `sanic_CHEVTimezone` (default `America/Managua`) | Nadie: el asunto y el tamaño máximo hoy los validan VR-04 y VR-06 con parámetros de `sanic_ValidationRules`, no estas EV. |

---

## 5. Connection references (4, en `Other/Customizations.xml`)

| Logical name | Conector |
|---|---|
| `sanic_CHCRDataverse` | Dataverse |
| `sanic_CHCROneDrive` | OneDrive for Business |
| `sanic_CHCROutlook` | Office 365 Outlook |
| `sanic_CHCRSharepoint` | SharePoint Online |

La solución Flow define además las suyas propias (13); ver
[02-flujos.md](02-flujos.md#connection-references).

---

## 6. Metadata for archival

`metadataforarchivals/` trae configuración de Long Term Retention para
`sanic_chtblreceivedemails`, `sanic_chtblvalidationruns` y
`sanic_chtblvalidationresults` — las tres con `isavailableforarchival = 0`, es
decir, presente pero **desactivada**.

---

## 7. Custom API y plugin (fuera de la solución)

El motor de validación NO viaja en CashHubCore. Se registra imperativamente con
`scripts/register-validation-plugin.sh` (Web API v9.2, idempotente):

- **Plugin assembly** `CashHub.Plugins` (sandbox, database).
- **Plugin type** `CashHub.Plugins.ValidateReceivedEmailPlugin`.
- **Custom API `sanic_CHValidateReceivedEmail`** — global/unbound, Action.
  Request: `ReceivedEmailId`. Response: `RunId`, `Result` ("Pass"/"Fail"),
  `FailedFindings`.
- **Sin SdkMessageProcessingStep**: el paso asíncrono en Create fue retirado
  (fase D); la ingesta invoca el Custom API explícitamente tras subir los
  adjuntos y el barrido revalida pendientes. El script elimina el step viejo si
  sobrevive.

Detalle completo del motor en [04-backend.md](04-backend.md).

---

## 8. Deuda técnica e inconsistencias grabadas (verificadas)

1. Nombres lógicos fuera de convención: `sanic_TransactionBatches` y
   `sanic_ValidationRules` (sin `CHTbl`); option
   set `ch-recountingDetailsStatus` (sin `CH-Ch-`).
2. Typos grabados en LogicalName (no corregibles sin recrear el campo):
   `sanic_opeationtype`, `sanic_otherlocacheckamount`, `sanic_fileextention`,
   `sanic_reasonforforcedflosure`.
3. Columnas duplicadas contra el mismo global RecordStatus en ClientAccount,
   ClientBranch y ClientEmail.
4. Atributo de publisher ajeno vivo: `cr140_changestatus` en
   ClientDataChangeRequest.
5. Etiquetas divergentes cabecera/línea del RFC: Rejected vs Denied.
6. `XMLReadingDetail.sanic_currency` nvarchar; `Transactions.sanic_accountnumber`
   decimal vs int en las tablas espejo; `ReceptionBatch.sanic_transportadora`
   texto libre con TransportCompany sin relación.
7. `Transactions.sanic_transactionstatus` con un único valor (Aplicada).
8. Roles OpsAdmin/AnalyticsRead idénticos y vacíos de negocio.
9. Env vars y connection references fuera de `<RootComponents>`; defaults de EV
   con valores de DEV embebidos.
10. Tablas sin uso: `sanic_CHTblLocations` (huérfana total),
    `sanic_CHTblClientDepositHistory` (solo datasource declarada).
