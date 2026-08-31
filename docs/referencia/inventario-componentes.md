# Inventario de componentes — CashHub

> Inventario exhaustivo de TODO componente de las soluciones CashHub (tablas, campos, relaciones,
> option sets, variables de entorno, roles, plugin assembly, plugin classes, Custom APIs, Canvas
> Apps, Cloud Flows, apps model-driven, controles PCF y web resources). Vive en `docs/referencia/`
> porque es transversal — no pertenece a un único proceso.
>
> Cada fila tiene un ID estable (nombre lógico/schema name real) y las columnas fijas:
> **Objetivo** (para qué existe este componente), **Dónde se usa** (apps/flows/plugins que lo
> consumen), **Documentos de referencia**, **Estado** (revisión: Sin revisar / Confirmado /
> Modificar / Eliminar) y **Notas**.
>
> **Reconciliado el 2026-08-31** contra `solutions/unpacked/` en el commit `00f6f1c` (sync con el
> ambiente DEV `org36e60d9d`). Versiones inventariadas: `CashHubCore` v1.14.0.3,
> `CashHubFlows` v1.12.0.2, `CashHubApps` v1.33.2.0, más `src/backend/CashHub.Plugins` (fuera de
> solución, se registra por script) y `src/pcf/` (controles PCF).
> La foto anterior era del 2026-08-24 (Core v1.14.0.0 / Apps v1.31.0.4 / Flows v1.12.0.0).
>
> **Cómo se llenó `Dónde se usa`**: búsqueda con `rg` del logical name Y del display name contra
> los TRES árboles de fuente de canvas apps (ver §0), los flows, `src/backend/` y `scripts/`.
> Cuando la única evidencia es cache de datasource o `FormXml`/`SavedQueries`, se indica.
> Cuando no hubo coincidencias, se escribe explícitamente — un vacío no distingue entre
> "no se buscó" y "se buscó y no está".
>
> **Las columnas `Objetivo` y `Estado` no fueron completadas por el proceso de reconciliación.**
> Se completan en la revisión de arquitectura; ver `limpieza-candidatos.md` para los componentes
> ya analizados.

## 0. Correcciones de auditoría adversarial (MANDAN sobre las filas)

> Los veredictos de uso de este inventario fueron sometidos a una auditoría adversarial cuyo
> objetivo explícito era REFUTARLOS. De **77 veredictos de no-uso**, **28 resultaron falsos**
> (36%). Las correcciones de abajo tienen precedencia sobre lo que diga la celda `Dónde se usa`
> de la fila correspondiente, que puede seguir reflejando el veredicto original erróneo.

### Hallazgo metodológico: hay TRES árboles de fuente de canvas apps, no uno

Cualquier búsqueda de uso en este repo debe cubrir los tres o produce falsos negativos:

1. `solutions/unpacked/apps/CanvasApps/src/**/Src/*.fx.yaml`
2. `solutions/unpacked/patch/CanvasApps/src/**/Src/*.fx.yaml` — a veces idéntico al primero, a veces no
3. `**/Other/Src/*.pa.yaml` — en AMBOS árboles; **formato alternativo con lógica real que no está replicada en los otros dos**

El caso que lo destapó: `sanic_replyclosenote` parecía muerto en los dos primeros árboles y está vivo en
`patch/.../Other/Src/screen ColaDeProcesamiento.pa.yaml:874`, en el botón `btnCierreConfirmar`.

### Los 28 veredictos refutados — SÍ se usan

| Componente | Evidencia que se había perdido |
|---|---|
| `sanic_chtblreceptionbatch_Location_sanic_chtbllocations` (relación) | dot-walk: `scrLote.fx.yaml:209` |
| `sanic_chtblreceptions_BranchName_sanic_chtblclientbranch` (relación) | dot-walk: `scrCaptura.fx.yaml:46,2671` |
| `CH-Tbl-AppCatalog.sanic_description` | `scrHub.fx.yaml:572` |
| `CH-Tbl-AppCatalog.sanic_displayname` | `scrHub.fx.yaml:555` |
| `CH-Tbl-AppCatalogAccess.sanic_name` | `scripts/provision-appcatalog-access.sh:186,194` |
| `CH-Tbl-ApprovalRequest.sanic_name` | `scripts/seed-supervisor-aprobaciones.py`, `scripts/qa-approve-pending.py` |
| `CH-Tbl-Bags.sanic_name` | `filteringattributes` de `GuardReceptionPlugin` — `scripts/register-reception-guard.sh:183` |
| `CH-Tbl-Checks.sanic_name` | `scripts/seed-digitization-stress.py` |
| `CH-Tbl-ClientDataChangeRequest.sanic_name` | `scrSolicitud.fx.yaml:238,1557-1581` |
| `CH-Tbl-ClientEmail.sanic_name` | `scripts/seed-clientadmin-stress.py`, `scripts/seed-sender-matrix.py` |
| `CH-Tbl-ClientTableLogicalFieldName.sanic_name` | dot-walk `'Field Definition'.Name` — `scrSolicitud.fx.yaml:838,1561` + `$expand` en flows |
| `CH-Tbl-CountedMoneyDetails.sanic_name` | `scripts/seed-recounting-stress.py` |
| `CH-Tbl-CountingDesk.sanic_name` | **solo visible en `patch/`** — `scrResumen.fx.yaml:282` |
| `CH-Tbl-Declaration.sanic_name` | `scripts/seed-qa-escenarios-tulas.py`, `scripts/seed-tracker-stress.py` |
| `CH-Tbl-DeclaredBags.sanic_name` | `scripts/seed-recounting-stress.py` |
| `CH-Tbl-DeclaredChecks.sanic_name` | `scripts/seed-qa-escenarios-tulas.py`, `scripts/seed-recounting-stress.py` |
| `CH-Tbl-DepositsRecounting.sanic_name` | `scripts/seed-recounting-stress.py` |
| **`CH-Tbl-EmailTemplate.sanic_name`** | **ALTO IMPACTO** — clave de búsqueda activa en `ClientReplyComposer.cs:150,154` (`QueryExpression` + `AddCondition("sanic_name", Equal, key)`). Borrarlo rompe el compositor de respuestas de correo en producción. |
| `CH-Tbl-EmailsAttachments.sanic_name` | `CH-Flow-ProcessEmail-...json:227,232` |
| `CH-Tbl-Playbook.sanic_isactive` | formulario ya aplicado — `FormXml:51` |
| `CH-Tbl-Playbook.sanic_orden` | formulario y vista default ya aplicados — `FormXml:43` + `SavedQueries:13,26,31` |
| **`CH-Tbl-Received Emails.sanic_replyclosenote`** | **tercera fuente** — `patch/.../Other/Src/screen ColaDeProcesamiento.pa.yaml:874` |
| `CH-Tbl-TransportCompany.sanic_name` | coacción de lookup a texto — `scrLotes.fx.yaml:144` (`ThisItem.Transportadora`) |
| `CH-Tbl-ValidationRules.sanic_notes` | formulario ya aplicado — `FormXml:128` |
| `CH-Tbl-ValidationRules.sanic_sourcetype` | formulario ya aplicado — `FormXml:66` |
| `CH-Tbl-ValidationRules.sanic_validationdatatype` | formulario ya aplicado — `FormXml:74` |
| `CH-Tbl-ValidationRules.sanic_validationoperation` | vista pública por defecto (`isdefault=1`) — `SavedQueries/{75b083f6...}.xml` |
| `sanic_CHAppConfiguration` (app model-driven) | verificación en vivo en `docs/desarrollo/09-app-configuracion.md`; SiteMap con 5 grupos / 12 subáreas, rol con 98 privilegios. El `statecode=0` del export es artefacto de exportación, no evidencia de desuso. |

### Patrones que causaron los falsos negativos

- **17 de 28** son el campo primario `sanic_name`, casi siempre vivo en `scripts/seed-*.py` / `qa-*.py` vía OData `$select` / `$filter=startswith(sanic_name,...)`. El barrido de `scripts/` fue incompleto.
- **7** se descartaron asumiendo que `scripts/provision-config-app.py` "no estaba aplicado", premisa apoyada en una lectura incorrecta de `git status` (el archivo está commiteado en `f4ce2d2`). Los 7 resultaron falsos.
- **3** son dot-walk indirecto por display name (`Location.Name`, `BranchName`, `Transportadora`).
- **1** destapó el tercer árbol de fuente.

> **Nota sobre el campo primario**: en las 42 tablas custom de CashHub, `sanic_name` es el
> *primary name attribute* (marcado `PrimaryName` en su `<DisplayMask>`). **Dataverse no permite
> eliminarlo**, y lo usa para renderizar todo lookup, link de registro y vista. Un `sanic_name`
> sin referencias en código NO es candidato a borrar.

## 1. Tablas (Entities)

> 44 tablas en el export (2 nativas: `Account`, `SystemUser` + 42 `sanic_*`), igual cantidad que el inventario 2026-08-24 — **ninguna tabla nueva ni ausente**. Cambiaron los conteos de campos custom en 4 tablas: `sanic_CHTblClient` (+1, nuevo `sanic_TaxId`), `sanic_CHTblClientDataChangeRequest` (-1), `sanic_CHTblTransactions` (+2) y `sanic_TransactionBatches` (+3). Verificado contra `solutions/unpacked/core/Entities/*/Entity.xml` (conteo de `<attribute PhysicalName="sanic_*">` menos el atributo `primarykey` de cada tabla).

| Logical name | Display name | Solución | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|
| `Account` | Account | CashHubCore | | INFRA — tabla nativa de Dataverse sin campos custom; se usa solo como target de lookups (owner/systemuser) en plugins y flows, no como objeto de negocio propio. | | Sin revisar | Tabla nativa de Dataverse extendida por la solución; 0 campo(s) custom en este export. |
| `SystemUser` | User | CashHubCore | | INFRA — tabla nativa de Dataverse sin campos custom; se usa solo como target de lookups (owner/systemuser) en plugins y flows, no como objeto de negocio propio. | | Sin revisar | Tabla nativa de Dataverse extendida por la solución; 0 campo(s) custom en este export. |
| `sanic_CHTblAppCatalog` | CH-Tbl-AppCatalog | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Hub, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: GuardAppCatalogPlugin.cs, Schema.cs. Scripts: provision-appcatalog-access.sh, provision-config-app.py, provision-tracker-role.sh, register-appcatalog-guard.sh. | | Sin revisar | 10 campos custom. |
| `sanic_CHTblAppCatalogAccess` | CH-Tbl-AppCatalogAccess | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Hub, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: Schema.cs. Scripts: provision-appcatalog-access.sh, provision-config-app.py, provision-tracker-role.sh, register-appcatalog-guard.sh. | | Sin revisar | 3 campos custom. |
| `sanic_CHTblApprovalRequest` | CH-Tbl-ApprovalRequest | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: ApprovalRequestBagNumberPlugin.cs, GuardRecountingPlugin.cs, RecountingSessionAutoApprovePlugin.cs, Schema.cs. Scripts: apply-supervisor-role-remediation.sh, backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-supervisor-mirror-columns.sh (+8 más). | | Sin revisar | 9 campos custom. |
| `sanic_CHTblBags` | CH-Tbl-Bags | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagReceptionMirrorPlugin.cs, BagReconciliationMirrorPlugin.cs, BagRecountClosurePlugin.cs, CheckMirrorStampPlugin.cs, DepositSessionMirrorPlugin.cs (+2 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, backfill-tracker.py, create-supervisor-mirror-columns.sh (+15 más). | | Sin revisar | 15 campos custom. |
| `sanic_CHTblChecks` | CH-Tbl-Checks | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckMirrorStampPlugin.cs, GuardRecountingPlugin.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, backfill-tracker.py, create-digitization-columns.sh (+16 más). | | Sin revisar | 15 campos custom. |
| `sanic_CHTblClient` | CH-Tbl-Client | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-Notifyreviewers, CH-Flow-NotifywhenRFCimplementationfinish, CH-Flow-RFCWatchdog, CH-Flow-UnblockOnClient, CH-Flow-UnblockOnClientEmail. Plugins: ChangeRequestApproverResolver.cs, ChangeRequestLineGuard.cs, ChangeRequestLineGuardTests.cs, ChangeRequestResolution.cs, ChangeRequestResolutionTests.cs (+11 más). Scripts: assign-clientadmin-team-roles.sh, backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-client-taxid-column.sh (+18 más). | | Sin revisar | 6 campos custom (antes 5; incluye el nuevo `sanic_TaxId` agregado en esta sync — TaxId obligatorio). |
| `sanic_CHTblClientAccount` | CH-Tbl-ClientAccount | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Deactivaterow. Plugins: ReceivedEmailValidationService.cs. Scripts: create-alternate-keys.sh, fix-clientaccount-alternate-key.sh, seed-clientadmin-stress.py, seed-sender-matrix.py. | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientBranch` | CH-Tbl-ClientBranch | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Deactivaterow. Plugins: ChangeRequestResolutionTests.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, FieldValueConverterTests.cs. Scripts: backfill-clientbranch-mirror.py, create-clientbranch-mirror-column.sh, register-clientbranch-mirror.sh, seed-clientadmin-stress.py (+1 más). | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientDataChangePermission` | CH-Tbl-ClientDataChangePermission | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-Notifyreviewers. Plugins: ChangeRequestApproverResolver.cs, Schema.cs. Scripts: assign-clientadmin-team-roles.sh, provision-approver-teams.sh, provision-config-app.py. | | Sin revisar | 4 campos custom. |
| `sanic_CHTblClientDataChangeRequest` | CH-Tbl-ClientDataChangeRequest | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-Notifyreviewers, CH-Flow-NotifywhenRFCimplementationfinish, CH-Flow-RFCWatchdog. Plugins: ChangeRequestLineGuard.cs, ChangeRequestLineGuardTests.cs, ChangeRequestResolution.cs, GuardChangeRequestApprovalPlugin.cs, GuardChangeRequestLinePlugin.cs (+1 más). Scripts: migrate-cr140-changestatus.sh, register-changerequestline-guard.sh, register-clientadmin-guard.sh, register-resolve-changerequest.sh (+1 más). | | Sin revisar | 14 campos custom (antes 15; recalculado desde Entity.xml, ya no da 15). |
| `sanic_CHTblClientDataChangeRequestLine` | CH-Tbl-ClientDataChangeRequestLine | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Notifyreviewers. Plugins: ChangeRequestLineGuard.cs, ChangeRequestLineGuardTests.cs, ChangeRequestResolution.cs, GuardChangeRequestLinePlugin.cs, Schema.cs. Scripts: register-changerequestline-guard.sh, seed-clientadmin-stress.py. | | Sin revisar | 9 campos custom. |
| `sanic_CHTblClientDataChangesAuditTrail` | CH-Tbl-ClientDataChangesAuditTrail | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-RFCWatchdog. Plugins: Schema.cs. Scripts: seed-clientadmin-stress.py. | | Sin revisar | 7 campos custom. |
| `sanic_CHTblClientDepositHistory` | CH-Tbl-ClientDepositHistory | CashHubCore | | Sin evidencia de uso en código ejecutable (Fx.yaml, plugins, flows o scripts). Solo aparece en el cache de esquema (DataSources/TableDefinitions) de 7 canvas apps y en sus propios FormXml/SavedQueries. | | **ELIMINADO 2026-08-31** | 3 campos custom. |
| `sanic_CHTblClientEmail` | CH-Tbl-ClientEmail | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-Activaterow, CH-Flow-Deactivaterow, CH-Flow-UnblockOnClientEmail. Plugins: Schema.cs, ValidationContext.cs. Scripts: create-alternate-keys.sh, provision-email-ingestion-roles.sh, seed-clientadmin-stress.py, seed-sender-matrix.py. | | Sin revisar | 5 campos custom. |
| `sanic_CHTblClientTableLogicalFieldName` | CH-Tbl-ClientTableLogicalFieldName | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: FieldValueConverter.cs, FieldValueConverterTests.cs, Schema.cs. Scripts: provision-config-app.py, seed-clientadmin-stress.py. | | Sin revisar | 9 campos custom. |
| `sanic_CHTblCountedMoneyDetails` | CH-Tbl-CountedMoneyDetails | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: GuardRecountingPlugin.cs, Schema.cs. Scripts: register-recounting-guard.sh, seed-recounting-stress.py. | | Sin revisar | 6 campos custom. |
| `sanic_CHTblCountingDesk` | CH-Tbl-CountingDesk | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: provision-config-app.py, seed-recounting-stress.py, seed-supervisor-aprobaciones.py, seed-tracker-stress.py. | | Sin revisar | 4 campos custom. |
| `sanic_CHTblDeclaration` | CH-Tbl-Declaration | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: DeclarationMaterializer.cs. Scripts: backfill-tracker.py, create-tracker-columns.sh, provision-email-ingestion-roles.sh, provision-tracker-role.sh (+4 más). | | Sin revisar | 5 campos custom. |
| `sanic_CHTblDeclaredBags` | CH-Tbl-DeclaredBags | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, DeclaredCheckBagMirrorPlugin.cs, ReceivedEmailValidationService.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, build-test-workbooks.py, create-recounting-mirror-columns.sh, provision-email-ingestion-roles.sh (+5 más). | | Sin revisar | 9 campos custom. |
| `sanic_CHTblDeclaredChecks` | CH-Tbl-DeclaredChecks | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, DeclaredCheckBagMirrorPlugin.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, backfill-tracker.py, build-test-workbooks.py, create-recounting-mirror-columns.sh (+10 más). | | Sin revisar | 9 campos custom. |
| `sanic_CHTblDeclaredDeposits` | CH-Tbl-DeclaredDeposits | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, DeclaredCheckBagMirrorPlugin.cs, Schema.cs. Scripts: backfill-tracker.py, build-test-workbooks.py, create-tracker-columns.sh, fix-declared-check-summary.py (+8 más). | | Sin revisar | 12 campos custom. |
| `sanic_CHTblDepositsRecounting` | CH-Tbl-DepositsRecounting | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: DepositSessionMirrorPlugin.cs, GuardRecountingPlugin.cs, Schema.cs. Scripts: backfill-cajero-en-transacciones.py, backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, grant-cashier-account-read.sh (+9 más). | | Sin revisar | 11 campos custom. |
| `sanic_CHTblEmailTemplate` | CH-Tbl-EmailTemplate | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: ClientReplyComposer.cs. Scripts: provision-config-app.py, provision-email-ingestion-roles.sh. | | Sin revisar | 4 campos custom. |
| `sanic_CHTblEmailsAttachments` | CH-Tbl-EmailsAttachments | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-ExtractedTablesFromExcel, CH-Flow-GetJSONdatafromattachment, CH-Flow-ProcessEmail. Plugins: Schema.cs. Scripts: probe-parser-limits.py, provision-email-ingestion-roles.sh, provision-tracker-role.sh. | | Sin revisar | 8 campos custom. |
| `sanic_CHTblLocations` | CH-Tbl-Locations | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Scripts: provision-config-app.py, provision-tracker-role.sh. | | Sin revisar | 1 campos custom. |
| `sanic_CHTblMoneyDenomination` | CH-Tbl-MoneyDenomination | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Scripts: provision-config-app.py, seed-recounting-stress.py. | | Sin revisar | 5 campos custom. |
| `sanic_CHTblParameter` | CH-Tbl-Parameter | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-DownloadData, CH-Flow-Notifyreviewers, CH-Flow-NotifywhenRFCimplementationfinish. Plugins: ApproverGroupConfiguration.cs, GuardReceptionPlugin.cs, Schema.cs. Scripts: provision-config-app.py, provision-parameters-table.py, provision-tracker-role.sh, register-reception-guard.sh. | | Sin revisar | 6 campos custom. |
| `sanic_CHTblPlaybook` | CH-Tbl-Playbook | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Scripts: provision-config-app.py, provision-email-ingestion-roles.sh. | | Sin revisar | 6 campos custom. |
| `sanic_CHTblReceivedEmails` | CH-Tbl-Received Emails | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-MailboxSweep, CH-Flow-ProcessEmail, CH-Flow-Replytoclientemail, CH-Flow-UnblockOnClient, CH-Flow-UnblockOnClientEmail. Plugins: Schema.cs, ValidateReceivedEmailPlugin.cs. Scripts: backfill-tracker.py, build-test-workbooks.py, create-alternate-keys.sh, create-senderclassification-options.sh (+11 más). | | Sin revisar | 29 campos custom. |
| `sanic_CHTblReceptionBatch` | CH-Tbl-ReceptionBatch | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: backfill-supervisor-mirrors.py, backfill-tracker.py, create-supervisor-mirror-columns.sh, create-tracker-columns.sh (+7 más). | | Sin revisar | 10 campos custom. |
| `sanic_CHTblReceptions` | CH-Tbl-Receptions | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: BagReceptionMirrorPlugin.cs, ReceptionBatchTotalsPlugin.cs, Schema.cs. Scripts: backfill-tracker.py, provision-tracker-role.sh, register-reception-guard.sh, register-reception-mirror.sh (+6 más). | | Sin revisar | 12 campos custom. |
| `sanic_CHTblRecountingSession` | CH-Tbl-RecountingSession | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: DepositSessionMirrorPlugin.cs, GuardReceptionPlugin.cs, GuardRecountingPlugin.cs, Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-cajero-en-transacciones.py, backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh (+8 más). | | Sin revisar | 8 campos custom. |
| `sanic_CHTblRecountingSessionDetail` | CH-Tbl-RecountingSessionDetail | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, provision-tracker-role.sh, reasignar-sesion-recuento.py (+1 más). | | Sin revisar | 5 campos custom. |
| `sanic_CHTblSystemHeartbeat` | CH-Tbl-SystemHeartbeat | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-MailboxSweep. Scripts: provision-email-ingestion-roles.sh. | | Sin revisar | 3 campos custom. |
| `sanic_CHTblTransactions` | CH-Tbl-Transactions | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-DownloadData. Plugins: AccountingGuard.cs, CHRemoveFromCutPlugin.cs, GuardRecountingPlugin.cs, GuardSupervisorAccountingPlugin.cs, RecountingGuardTests.cs (+2 más). Scripts: apply-supervisor-role-remediation.sh, backfill-cajero-en-transacciones.py, create-alternate-keys.sh, create-digitization-columns.sh (+12 más). | | Sin revisar | 20 campos custom (antes 18; se agregaron `sanic_MotivoRechazoCore` y `sanic_SupervisorAutoriza`). |
| `sanic_CHTblTransportCompany` | CH-Tbl-TransportCompany | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Scripts: provision-config-app.py. | | Sin revisar | 1 campos custom. |
| `sanic_CHTblValidationResults` | CH-Tbl-ValidationResults | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: Finding.cs, Schema.cs. Scripts: create-tracker-columns.sh, provision-email-ingestion-roles.sh, provision-tracker-role.sh, seed-bandeja-demo.py (+1 más). | | Sin revisar | 10 campos custom. |
| `sanic_CHTblValidationRuns` | CH-Tbl-ValidationRuns | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Flows: CH-Flow-MailboxSweep. Plugins: Schema.cs. Scripts: provision-email-ingestion-roles.sh, provision-tracker-role.sh, seed-bandeja-demo.py, seed-tracker-stress.py. | | Sin revisar | 8 campos custom. |
| `sanic_CHTblXMLReading` | CH-Tbl-XMLReading | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh, seed-recounting-stress.py. | | Sin revisar | 4 campos custom. |
| `sanic_CHTblXMLReadingDetail` | CH-Tbl-XMLReadingDetail | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh, seed-recounting-stress.py. | | Sin revisar | 8 campos custom. |
| `sanic_TransactionBatches` | CH-Tbl-TransactionBatches | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-DownloadData. Plugins: BatchTransitionStampPlugin.cs, Schema.cs. Scripts: provision-lote-devuelto.sh, provision-tracker-role.sh, register-batch-stamp.sh, register-transaction-guard.sh (+4 más). | | Sin revisar | 16 campos custom (antes 13; se agregaron `sanic_DevueltoOn`, `sanic_DevueltoPor` y `sanic_MotivoDevolucion` — flujo de devolución de lote). |
| `sanic_ValidationRules` | CH-Tbl-ValidationRules | CashHubCore | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: RuleDefinition.cs, Schema.cs. Scripts: provision-config-app.py, provision-email-ingestion-roles.sh, provision-tracker-role.sh, seed-bandeja-demo.py (+1 más). | | Sin revisar | 11 campos custom. |

## 2. Campos por tabla

> Solo campos custom (`IsCustomField=1` en el `Entity.xml`, prefijo `sanic_`). Para `Account` y `SystemUser` (extensiones de tablas nativas) se listan únicamente los campos custom agregados por esta solución — no hay campos nativos de Dataverse en esta sección. En este export ninguna de las dos tablas nativas tiene campos custom propios (ver nota en cada subsección).

### Account (`Account`)

_No hay campos custom (`sanic_*`) agregados por esta solución en este export._

### User (`SystemUser`)

_No hay campos custom (`sanic_*`) agregados por esta solución en este export._

### CH-Tbl-AppCatalog (`sanic_CHTblAppCatalog`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AAD Group Id | Texto (nvarchar) | none | | Canvas `CH-App-Hub` (`App.fx.yaml:52`); plugin `GuardAppCatalogPlugin.cs`; `scripts/provision-appcatalog-access.sh` | | Sin revisar | |
| `sanic_appkey` | App Key | Texto (nvarchar) | required | | `scripts/provision-appcatalog-access.sh` (lee/compara `sanic_appkey`) | | Sin revisar | |
| `sanic_appurl` | AppURL | Texto (nvarchar) | recommended | | Canvas `CH-App-Hub` (`App.fx.yaml:99`, `scrHub.fx.yaml:449`); plugin `GuardAppCatalogPlugin.cs` | | Sin revisar | |
| `sanic_description` | Description | Texto (nvarchar) | none | | Solo `scripts/provision-config-app.py` (arma metadata de formulario) | | Sin revisar | |
| `sanic_displayname` | Display Name | Texto (nvarchar) | required | | Solo `scripts/provision-config-app.py` (spec de vista/formulario) | | Sin revisar | |
| `sanic_enabled` | Enabled | Dos opciones (bit) | required | | Canvas `CH-App-Hub` (`App.fx.yaml:41`, `Filter(..., Enabled)`) | | Sin revisar | |
| `sanic_icon` | Icon | Imagen | none | | Canvas `CH-App-Hub` (`scrHub.fx.yaml:510,512,537`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas y `scripts/provision-config-app.py` (spec) | | Sin revisar | |
| `sanic_order` | Order | Número entero | required | | Canvas `CH-App-Hub` (`scrHub.fx.yaml:446`, `Sort(colAppData, 'Order')`) | | Sin revisar | |
| `sanic_tags` | Tags | Texto (nvarchar) | none | | Solo `scripts/provision-config-app.py` | | Sin revisar | |

### CH-Tbl-AppCatalogAccess (`sanic_CHTblAppCatalogAccess`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AAD Group Id | Texto (nvarchar) | required | | Plugin `GuardAppCatalogAccessPlugin.cs` | | Sin revisar | |
| `sanic_appcatalog` | App Catalog | Búsqueda (lookup) | required | | Plugin `GuardAppCatalogAccessPlugin.cs` (`Schema.AppCatalogAccess.AppCatalogLookup`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | Solo formularios/vistas | | Sin revisar | |

### CH-Tbl-ApprovalRequest (`sanic_CHTblApprovalRequest`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_approvalstatus` | ApprovalStatus | Option set (picklist) | none | | Canvas `CH-App-Recounting`/`CH-App-Supervisor` (`scrConteo.fx.yaml`, `scrSesion.fx.yaml`, `scrAprobaciones.fx.yaml`) | | Sin revisar | |
| `sanic_bagnumber` | BagNumber | Texto (nvarchar) | none | | Canvas `CH-App-Supervisor` (`scrResumen.fx.yaml:1458`, `scrAprobaciones.fx.yaml:398,411`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_requestdetails` | RequestDetails | Texto multilínea (ntext) | none | | Canvas `CH-App-Reception`/`CH-App-Recounting`/`CH-App-Supervisor` (varias pantallas) | | Sin revisar | |
| `sanic_requestresources` | RequestResources | Texto (nvarchar) | recommended | | Canvas `CH-App-Reception`/`CH-App-Recounting`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_requesttype` | RequestType | Texto (nvarchar) | required | | Canvas `CH-App-Reception`/`CH-App-Recounting`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_resolutiondatetime` | ResolutionDatetime | Fecha y hora | recommended | | Canvas `CH-App-Recounting`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | Canvas `CH-App-Recounting`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_statusreason` | StatusReason | Texto (nvarchar) | recommended | | Canvas `CH-App-Recounting`/`CH-App-Supervisor` | | Sin revisar | |

### CH-Tbl-Bags (`sanic_CHTblBags`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagdeclarationid` | BagDeclarationID | Búsqueda (lookup) | recommended | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:60-64`) | | Sin revisar | |
| `sanic_bagid` | BagID | Texto (nvarchar) | required | | Canvas `CH-App-DepositsTracker`/`CH-App-Recounting` (varias pantallas) | | Sin revisar | |
| `sanic_bagstatus` | BagStatus | Option set (picklist) | recommended | | Canvas `CH-App-DepositsTracker` (`scrLote.fx.yaml:1035,1036`) | | Sin revisar | |
| `sanic_decltotaleur` | DeclTotalEUR | Decimal | none | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |
| `sanic_decltotalnio` | DeclTotalNIO | Decimal | none | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |
| `sanic_decltotalusd` | DeclTotalUSD | Decimal | none | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |
| `sanic_hasdifference` | HasDifference | Dos opciones (bit) | none | | Canvas `CH-App-DepositsTracker` (`scrLote.fx.yaml:1079,1084`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas | | Sin revisar | |
| `sanic_receiptidmirror` | ReceiptIdMirror | Texto (nvarchar) | none | | Canvas `CH-App-Supervisor` (`scrTulas.fx.yaml:346,350`) | | Sin revisar | |
| `sanic_receptionbatchid` | ReceptionBatchId | Búsqueda (lookup) | none | | Canvas `CH-App-Recounting`/`CH-App-Reception` | | Sin revisar | |
| `sanic_receptionid` | ReceptionID | Búsqueda (lookup) | required | | Canvas `CH-App-DepositsTracker` (`scrLote.fx.yaml:961`, `scrDeposito.fx.yaml:543`) | | Sin revisar | |
| `sanic_recountingsession` | RecountingSession | Búsqueda (lookup) | recommended | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:71,75`) | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | recommended | | Canvas `CH-App-DepositsTracker`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | recommended | | Canvas `CH-App-DepositsTracker`/`CH-App-Supervisor` | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | recommended | | Canvas `CH-App-Supervisor` (`scrResumen.fx.yaml:474,936`) | | Sin revisar | |

### CH-Tbl-Checks (`sanic_CHTblChecks`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bag` | Bag | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:17`, `scrCorreo.fx.yaml:1628`) | | Sin revisar | |
| `sanic_checkamount` | CheckAmount | Decimal | required | | Canvas `CH-App-DepositsTracker`/`CH-App-Recounting` | | Sin revisar | |
| `sanic_checkcurrency` | CheckCurrency | Option set (picklist) | required | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:1163`, `scrTula.fx.yaml:1657`) | | Sin revisar | |
| `sanic_checknumber` | CheckNumber | Texto (nvarchar) | required | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:1200`, `scrTula.fx.yaml:1694`) | | Sin revisar | |
| `sanic_checkstatus` | CheckStatus | Option set (picklist) | recommended | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:1087,1088`) | | Sin revisar | |
| `sanic_checktypes` | CheckTypes | Option set (picklist) | required | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:1181`, `scrTula.fx.yaml:1675`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas | | Sin revisar | |
| `sanic_notreceivedon` | No recibido el | Fecha y hora | none | | Canvas `CH-App-Supervisor`/`CH-App-Digitization` (`scrCheques.fx.yaml`) | | Sin revisar | |
| `sanic_receivedby` | ReceivedBy | Búsqueda (lookup) | none | | Canvas `CH-App-Digitization` (`scrCheques.fx.yaml:1100`) | | Sin revisar | |
| `sanic_receivedon` | Recibido el | Fecha y hora | none | | Canvas `CH-App-Digitization` (`scrCheques.fx.yaml:1139`) | | Sin revisar | |
| `sanic_recountingsession` | RecountingSession | Búsqueda (lookup) | none | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:461`) | | Sin revisar | |
| `sanic_relateddeclaredcheck` | RelatedDeclaredCheck | Búsqueda (lookup) | recommended | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:608,627`) | | Sin revisar | |
| `sanic_relatedrecounterdepositid` | RelatedReCounterDepositID | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker`/`CH-App-Digitization` | | Sin revisar | |
| `sanic_senttodigitizationby` | SentToDigitizationBy | Búsqueda (lookup) | none | | Canvas `CH-App-Digitization` (`scrCheques.fx.yaml:1023`) | | Sin revisar | |
| `sanic_senttodigitizationon` | Enviado el | Fecha y hora | none | | Canvas `CH-App-Supervisor` (`scrCheques.fx.yaml:670,1100`) | | Sin revisar | |

### CH-Tbl-Client (`sanic_CHTblClient`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cifbco` | CIFBCO | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:223`, `scrClientes.fx.yaml:34,99,139`) | | Sin revisar | |
| `sanic_cifcom` | CIFCOM | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:34,99,139`) | | Sin revisar | |
| `sanic_iseditable` | IsEditable | Dos opciones (bit) | none | | Solo vista `SavedQueries/{7a07d9eb-...}.xml` | | **ELIMINADO 2026-08-31** | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:34,99,139`, `Search(...)`) | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml`, extensivo); flows Activaterow/Deactivaterow | | Sin revisar | |
| `sanic_taxid` | TaxId | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:353,2229,2339`, `scrClientes.fx.yaml:34,99,139`) | | Sin revisar | **NUEVO desde el export del 2026-08-24** |

### CH-Tbl-ClientAccount (`sanic_CHTblClientAccount`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_chchrecordstatus` | CH-Ch-RecordStatus | Option set (picklist) | none | | Sin uso detectado (ver notas) | | **ELIMINADO 2026-08-31** | Sin coincidencias en fx.yaml/flows/backend/scripts ni en FormXml/SavedQueries propios; posible campo huérfano |
| `sanic_client` | Client | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:33,1366`) | | Sin revisar | |
| `sanic_currency` | Moneda | Option set (picklist) | none | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:2240,2307,2385`) | | Sin revisar | |
| `sanic_name` | Numero de cuenta | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:1454,2116,2189,2239,...`) | | Sin revisar | **Corrección: el Entity.xml actual marca este campo como `required`, el doc anterior decía `recommended`** |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | Flows `CH-Flow-Activaterow`, `CH-Flow-Deactivaterow` | | Sin revisar | |

### CH-Tbl-ClientBranch (`sanic_CHTblClientBranch`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_client` | Client | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:23,34`) | | Sin revisar | |
| `sanic_clientnamemirror` | ClientNameMirror | Texto (nvarchar) | none | | Canvas `CH-App-Reception` (`scrCaptura.fx.yaml:204,214,215`, logical name en `DisplayFields`/`SearchFields`) | | Sin revisar | |
| `sanic_clientstatus` | ClientStatus | Option set (picklist) | none | | Columna fórmula Dataverse (`Formulas/sanic_chtblclientbranch-sanic_clientstatus.xaml`) que lee `sanic_client.sanic_recordstatus` | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:2114`, `LookUp(...).Name`) | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:855,941,952,980`); flows Activaterow/Deactivaterow | | Sin revisar | |

### CH-Tbl-ClientDataChangePermission (`sanic_CHTblClientDataChangePermission`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aadgroupid` | AADGroupID | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`App.fx.yaml:67,75,81`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:399,449,714,732,854,888,947`) | | Sin revisar | |
| `sanic_teamsgroupid` | TeamsGroupID | Texto (nvarchar) | recommended | | Flow `CH-Flow-Notifyreviewers` | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:399,449,714,732,854,888,947`) | | Sin revisar | |

### CH-Tbl-ClientDataChangeRequest (`sanic_CHTblClientDataChangeRequest`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `cr140_changestatus` | ChangeStatus | Option set (picklist) | none | | `scripts/migrate-cr140-changestatus.sh` (migración a `sanic_changestatus`) | | Sin revisar | |
| `sanic_applynotes` | ApplyNotes | Texto multilínea (ntext) | none | | Flows Activaterow/Applyclientdatachanges/Createclientdata/NotifywhenRFCimplementationfinish/Deactivaterow/RFCWatchdog | | Sin revisar | |
| `sanic_applyon` | ApplyOn | Fecha y hora | recommended | | Flows Activaterow/Applyclientdatachanges/Createclientdata/Deactivaterow/RFCWatchdog | | Sin revisar | |
| `sanic_changestatus` | Change Status | Option set (picklist) | recommended | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:25-27`, `scrClientes.fx.yaml:515`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_reason` | Reason | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:1577`, `scrCliente.fx.yaml:2959,3043`) | | Sin revisar | |
| `sanic_reasonfordecision` | ReasonForDecision | Texto multilínea (ntext) | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:116,1520`) | | Sin revisar | |
| `sanic_relatedclient` | RelatedClient | Búsqueda (lookup) | recommended | | Canvas `CH-App-ClientAdministration` (`App.fx.yaml:113`, `scrCliente.fx.yaml:23,34`) | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:115,1511,1518`) | | Sin revisar | |
| `sanic_solvedon` | SolvedOn | Fecha y hora | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:115,118,435`) | | Sin revisar | |
| `sanic_targetrecordid` | TargetRecordID | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:38,42,51`) | | Sin revisar | |
| `sanic_targettable` | TargetTable | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:1578`, `scrCliente.fx.yaml:401,450,497`) | | Sin revisar | |
| `sanic_targettabledisplayname` | TargetTableDisplayName | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:1579`, `scrCliente.fx.yaml:1760,2955`) | | Sin revisar | |
| `sanic_targettablepluralname` | TargetTablePluralName | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:1580`, `scrCliente.fx.yaml:2956,3040`) | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | none | | Canvas `CH-App-ClientAdministration` (`scrSolicitudes.fx.yaml:1025-1068`, `scrSolicitud.fx.yaml:37,240-244`, etc.) | | Sin revisar | |

### CH-Tbl-ClientDataChangeRequestLine (`sanic_CHTblClientDataChangeRequestLine`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_changerequestid` | ChangeRequestID | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrClientes.fx.yaml:1590,1604,1618`) | | Sin revisar | |
| `sanic_changestatus` | ChangeStatus | Option set (picklist) | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:1511`) | | Sin revisar | |
| `sanic_fielddefinition` | Field Definition | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:2971`, `scrSolicitud.fx.yaml:139,838,1561`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_newvalue` | NewValue | Texto (nvarchar) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:2977`, `scrSolicitud.fx.yaml:868,1926`) | | Sin revisar | |
| `sanic_oldvalue` | OldValue | Texto (nvarchar) | recommended | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:2976`, `scrSolicitud.fx.yaml:854,1925`) | | Sin revisar | |
| `sanic_reasonfordecision` | ReasonForDecision | Texto (nvarchar) | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:116,1520`) | | Sin revisar | |
| `sanic_solvedby` | SolvedBy | Búsqueda (lookup) | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:1511,1518`) | | Sin revisar | |
| `sanic_solvedon` | SolvedOn | Fecha y hora | recommended | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:1511`) | | Sin revisar | |

### CH-Tbl-ClientDataChangesAuditTrail (`sanic_CHTblClientDataChangesAuditTrail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_message` | Message | Texto multilínea (ntext) | recommended | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:1987`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_opeationtype` | OpeationType | Option set (picklist) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:1955`) | | Sin revisar | |
| `sanic_relatedclient` | RelatedClient | Búsqueda (lookup) | recommended | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:35`) | | Sin revisar | |
| `sanic_relatedcrl` | RelatedCRL | Búsqueda (lookup) | recommended | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |
| `sanic_relatedrfc` | RelatedRFC | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:80,575`) | | Sin revisar | |
| `sanic_result` | Result | Dos opciones (bit) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:1962,1967,1975`) | | Sin revisar | |

### CH-Tbl-ClientDepositHistory (`sanic_CHTblClientDepositHistory`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_chchrecordstatus` | CH-Ch-RecordStatus | Option set (picklist) | none | | Sin uso detectado | | **ELIMINADO 2026-08-31** | Sin coincidencias ni en fx.yaml/flows/backend/scripts ni en FormXml/SavedQueries propios |
| `sanic_client` | Client | Búsqueda (lookup) | required | | Solo vistas (`SavedQueries/*.xml`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | La tabla completa (`sanic_chtblclientdeposithistory`) no aparece en ningún fx.yaml, flow, plugin C# ni script — parece no usada por las apps activas |

### CH-Tbl-ClientEmail (`sanic_CHTblClientEmail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_alwaysnotify` | AlwaysNotify | Dos opciones (bit) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:1201,2236,2306`) | | Sin revisar | |
| `sanic_client` | Client | Búsqueda (lookup) | required | | Canvas `CH-App-ClientAdministration` (`scrCliente.fx.yaml:32`) | | Sin revisar | |
| `sanic_clientstatus` | ClientStatus | Option set (picklist) | none | | Columna fórmula Dataverse (`Formulas/sanic_chtblclientemail-sanic_clientstatus.xaml`) que lee `sanic_client.sanic_recordstatus` | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_recordstatus` | RecordStatus | Option set (picklist) | none | | Flows Activaterow/Deactivaterow/UnblockOnClientEmail | | Sin revisar | |

### CH-Tbl-ClientTableLogicalFieldName (`sanic_CHTblClientTableLogicalFieldName`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_datatype` | DataType | Option set (picklist) | required | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |
| `sanic_isallowed` | IsAllowed | Dos opciones (bit) | required | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:30`) | | Sin revisar | |
| `sanic_logicalname` | LogicalName | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:853,1749,1791`) | | Sin revisar | |
| `sanic_lookuptargettable` | LookupTargetTable | Texto (nvarchar) | recommended | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas | | Sin revisar | |
| `sanic_optionsetmap` | OptionSetMap | Texto multilínea (ntext) | recommended | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |
| `sanic_table` | Table | Texto (nvarchar) | none | | Canvas `CH-App-ClientAdministration` (`scrSolicitud.fx.yaml:30`) | | Sin revisar | |
| `sanic_tabledisplayname` | Table DisplayName | Texto (nvarchar) | required | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |
| `sanic_tablepluralname` | TablePluralName | Texto (nvarchar) | none | | Flows Applyclientdatachanges/Createclientdata | | Sin revisar | |

### CH-Tbl-CountedMoneyDetails (`sanic_CHTblCountedMoneyDetails`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_denomination` | Denomination | Decimal | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:1257,2666,6120,6449`); columna fórmula `sanic_totalamount` | | Sin revisar | |
| `sanic_denominationtype` | DenominationType | Texto (nvarchar) | none | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:6120,6449`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_quantity` | Quantity | Número entero | required | | Columna fórmula `sanic_totalamount = sanic_denomination * sanic_quantity` (`Formulas/sanic_chtblcountedmoneydetails-FormulaDefinitions.yaml`) | | Sin revisar | |
| `sanic_relateddepositrecountingid` | RelatedDepositRecountingID | Búsqueda (lookup) | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:1249,6088`) | | Sin revisar | |
| `sanic_totalamount` | TotalAmount | Decimal | none | | Columna fórmula (`Formulas/sanic_chtblcountedmoneydetails-FormulaDefinitions.yaml`) | | Sin revisar | |

### CH-Tbl-CountingDesk (`sanic_CHTblCountingDesk`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cajero` | Cajero | Búsqueda (lookup) | required | | Canvas `CH-App-Digitization` (`scrManual.fx.yaml:23,79`) | | Sin revisar | |
| `sanic_enable` | Enable | Dos opciones (bit) | recommended | | Canvas `CH-App-Recounting` (`scrSesion.fx.yaml:49`) | | Sin revisar | |
| `sanic_machineserialnumber` | MachineSerialNumber | Texto (nvarchar) | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:2612,2625`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas | | Sin revisar | |

### CH-Tbl-Declaration (`sanic_CHTblDeclaration`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_client` | Cliente atribuido | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:472`, `scrTula.fx.yaml:66`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_receivedemail` | ReceivedEmail | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:64,65`) | | Sin revisar | |
| `sanic_sourceentity` | SourceEntity | Texto (nvarchar) | required | | Plugin `DeclarationMaterializer.cs` | | Sin revisar | |
| `sanic_sourcerecordid` | SourceRecordID | Texto (nvarchar) | required | | Plugin `DeclarationMaterializer.cs`; canvas `CH-App-EmailValidationConsole` (`screen ColaDeProcesamiento.fx.yaml:399,421`) | | Sin revisar | |

### CH-Tbl-DeclaredBags (`sanic_CHTblDeclaredBags`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagid` | BagID | Búsqueda (lookup) | recommended | | Columna fórmula `sanic_isrelatedtobag` (`Formulas/sanic_chtbldeclaredbags-FormulaDefinitions.yaml`) | | Sin revisar | |
| `sanic_bagnumber` | BagNumber | Texto (nvarchar) | required | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:41`) | | Sin revisar | |
| `sanic_declarationid` | DeclarationID | Búsqueda (lookup) | required | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:62,64`) | | Sin revisar | |
| `sanic_isrelatedtobag` | IsRelatedToBag | Dos opciones (bit) | none | | Columna fórmula (`Formulas/sanic_chtbldeclaredbags-FormulaDefinitions.yaml`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo formularios/vistas | | Sin revisar | |
| `sanic_totaldeposits` | TotalDeposits | Número entero | none | | Canvas `CH-App-Recounting` (`scrTulas.fx.yaml:2234`) | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | recommended | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | recommended | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | recommended | | Plugin `BagReconciliationMirrorPlugin.cs` | | Sin revisar | |

### CH-Tbl-DeclaredChecks (`sanic_CHTblDeclaredChecks`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_checkamount` | CheckAmount | Decimal | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:613`) | | Sin revisar | |
| `sanic_checkcurrency` | CheckCurrency | Option set (picklist) | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:613`) | | Sin revisar | |
| `sanic_checknumber` | CheckNumber | Texto (nvarchar) | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:613`) | | Sin revisar | |
| `sanic_checktype` | CheckType | Option set (picklist) | required | | Canvas `CH-App-Recounting`/`CH-App-DepositsTracker` (`scrConteo.fx.yaml:614`, `scrCorreo.fx.yaml:1579`) | | Sin revisar | |
| `sanic_declaration` | Declaration | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:43,44`) | | Sin revisar | |
| `sanic_declaredbagmirror` | DeclaredBagMirror | Búsqueda (lookup) | none | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:102`) | | Sin revisar | |
| `sanic_declareddepositid` | DeclaredDepositID | Búsqueda (lookup) | required | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:618`) | | Sin revisar | |
| `sanic_matchedcheck` | MatchedCheck | Búsqueda (lookup) | none | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:608,627,1109,1220`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |

### CH-Tbl-DeclaredDeposits (`sanic_CHTblDeclaredDeposits`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Número entero | required | | Canvas `CH-App-Recounting`/`CH-App-Supervisor`; plugin `ResolveApprovalRequestPlugin.cs` | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | Columna fórmula `sanic_total`; canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:12,13`) | | Sin revisar | |
| `sanic_cashamount` | CashAmount | Decimal | recommended | | Columna fórmula `sanic_total`; canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:11`) | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | required | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:309,310`) | | Sin revisar | |
| `sanic_declaration` | Declaration | Búsqueda (lookup) | none | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:36,40`) | | Sin revisar | |
| `sanic_declaredbag` | DeclaredBag | Búsqueda (lookup) | required | | Canvas `CH-App-Supervisor` (`scrAprobaciones.fx.yaml:1588`) | | Sin revisar | |
| `sanic_depositreference` | DepositReference | Texto (nvarchar) | recommended | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:14`, `scrCorreo.fx.yaml:750`) | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:750`, `Coalesce(ThisItem.DepositReference, ThisItem.Name)`) | | Sin revisar | |
| `sanic_otherlocalcheckamount` | OtherLocalCheckAmount | Decimal | recommended | | Columna fórmula `sanic_total`; canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:13`, `scrCorreo.fx.yaml:821`) | | Sin revisar | |
| `sanic_relatedrecounteddeposit` | RelatedRecountedDeposit | Búsqueda (lookup) | recommended | | Canvas `CH-App-DepositsTracker` (`scrCorreo.fx.yaml:848,853`) | | Sin revisar | |
| `sanic_total` | Total | Decimal | none | | Columna fórmula (`Formulas/sanic_chtbldeclareddeposits-FormulaDefinitions.yaml`) | | Sin revisar | |
| `sanic_transactionno` | TransactionNo | Número entero | recommended | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:962,6389`) | | Sin revisar | |

### CH-Tbl-DepositsRecounting (`sanic_CHTblDepositsRecounting`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Número entero | recommended | | Plugins `BagRecountClosurePlugin.cs`, `ResolveApprovalRequestPlugin.cs` | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | Columna fórmula `sanic_total`; plugins `BagRecountClosurePlugin.cs`, `TransactionRoutingPlugin.cs` | | Sin revisar | |
| `sanic_bagid` | BagID | Búsqueda (lookup) | required | | Plugins `DepositSessionMirrorPlugin.cs`, `GuardRecountingPlugin.cs` (`Schema.DepositRecounting.BagLookup`) | | Sin revisar | |
| `sanic_cashamount` | CashAmount | Decimal | recommended | | Columna fórmula `sanic_total`; canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:270,281`); plugin `BagRecountClosurePlugin.cs` | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | recommended | | Canvas `CH-App-DepositsTracker` (`scrDeposito.fx.yaml:270,281`); plugins `BagRecountClosurePlugin.cs`, `GuardSupervisorAccountingPlugin.cs` | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_otherlocacheckamount` | OtherLocaCheckAmount | Decimal | recommended | | Columna fórmula `sanic_total`; plugins `BagRecountClosurePlugin.cs`, `TransactionRoutingPlugin.cs` | | Sin revisar | |
| `sanic_recountingsessionmirror` | RecountingSessionMirror | Búsqueda (lookup) | none | | Canvas `CH-App-Recounting` (`scrTulas.fx.yaml:1156,1176`); plugin `DepositSessionMirrorPlugin.cs` | | Sin revisar | |
| `sanic_relateddeclareddeposit` | RelatedDeclaredDeposit | Búsqueda (lookup) | recommended | | Canvas `CH-App-DepositsTracker` (`scrTula.fx.yaml:11,12,13`) | | Sin revisar | |
| `sanic_total` | Total | Decimal | none | | Columna fórmula (`Formulas/sanic_chtbldepositsrecounting-FormulaDefinitions.yaml`) | | Sin revisar | |
| `sanic_transactionnumber` | TransactionNumber | Número entero | recommended | | Canvas `CH-App-Recounting` (`scrConteo.fx.yaml:962,1125`); plugin `ResolveApprovalRequestPlugin.cs` | | Sin revisar | |

### CH-Tbl-EmailTemplate (`sanic_CHTblEmailTemplate`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bodyhtml` | BodyHtml | Texto multilínea (ntext) | recommended | | Plugin `ClientReplyComposer.cs` | | Sin revisar | |
| `sanic_isactive` | IsActive | Dos opciones (bit) | required | | Plugin `ClientReplyComposer.cs` | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_subject` | Subject | Texto (nvarchar) | recommended | | Plugin `ClientReplyComposer.cs` | | Sin revisar | |

### CH-Tbl-EmailsAttachments (`sanic_CHTblEmailsAttachments`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_email` | Email | Búsqueda (lookup) | required | | Canvas `CH-App-EmailValidationConsole` (`Cuarentena.fx.yaml:491`); flow `CH-Flow-ProcessEmail` | | Sin revisar | |
| `sanic_emailattachmentid` | EmailAttachmentID | Texto (nvarchar) | required | | Flow `CH-Flow-ProcessEmail` | | Sin revisar | |
| `sanic_filecontent` | FileContent | Archivo (file) | recommended | | Canvas `CH-App-EmailValidationConsole` (`Cuarentena.fx.yaml:487`, `screen ColaDeProcesamiento.fx.yaml:887`) | | Sin revisar | |
| `sanic_fileextention` | FileExtention | Texto (nvarchar) | none | | Flow `CH-Flow-ProcessEmail` | | Sin revisar | |
| `sanic_filename` | Filename | Texto (nvarchar) | required | | Canvas `CH-App-DepositsTracker`/`CH-App-EmailValidationConsole` (`scrCorreo.fx.yaml:367`, `Cuarentena.fx.yaml:491`) | | Sin revisar | |
| `sanic_jsondata` | JSONData | Archivo (file) | recommended | | Flows `CH-Flow-ExtractedTablesFromExcel`, `CH-Flow-GetJSONdatafromattachment` | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Solo formularios/vistas | | Sin revisar | |
| `sanic_tamanoenbytes` | TamanoEnbytes | Número entero | none | | Flow `CH-Flow-ProcessEmail` | | Sin revisar | |

### CH-Tbl-Locations (`sanic_CHTblLocations`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdepositstracker_2b2c3/Src/scrLote.fx.yaml:209` (`gvarLote.Location.Name`, dot-walk desde el lookup `sanic_location` de CH-Tbl-ReceptionBatch). Metadata: FormXml propio (main/quick/card). | | Sin revisar | |

### CH-Tbl-MoneyDenomination (`sanic_CHTblMoneyDenomination`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_currency` | Currency | Option set (picklist) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml:6113` (`Text(Currency) = ctxMonedaCon`). Script: `scripts/seed-recounting-stress.py`. | | Sin revisar | |
| `sanic_enabled` | Enabled | Dos opciones (bit) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml:47` (`Filter('CH-Tbl-MoneyDenominations', Enabled = true)`). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Script: `scripts/seed-recounting-stress.py:1500` (`d.get('sanic_name')`, lectura real del valor). | | Sin revisar | |
| `sanic_type` | Type | Option set (picklist) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml:6115` (`D.Type = 'Type (CH-Tbl-MoneyDenominations)'.Billete`). | | Sin revisar | |
| `sanic_value` | Value | Decimal | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml:48` (`SortByColumns(..., "sanic_value", ...)`) y `D.Value` en el mismo archivo. | | Sin revisar | |

### CH-Tbl-Parameter (`sanic_CHTblParameter`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_category` | Category | Option set (picklist) | none | | Script: `scripts/provision-parameters-table.py:149` (crea el atributo) y `:237` (escribe filas reales `"sanic_category": 159460000, ...`). | | Sin revisar | |
| `sanic_datatype` | Data Type | Option set (picklist) | required | | Script: `scripts/provision-parameters-table.py:165,237` (siembra filas reales de CH-Tbl-Parameter). OJO: `sanic_datatype` también existe en CH-Tbl-ClientTableLogicalFieldName (`Schema.FieldDefinition.DataType` en `src/backend/CashHub.Plugins/Schema.cs:200`) — es otra tabla, no confundir. | | Sin revisar | |
| `sanic_description` | Description | Texto multilínea (ntext) | none | | Script: `scripts/provision-parameters-table.py:183` (crea el atributo) y `:238,244,254,...` (valores reales de descripción por parámetro). | | Sin revisar | |
| `sanic_isactive` | Is Active | Dos opciones (bit) | none | | C#: `src/backend/CashHub.Plugins/Schema.cs:452` (`Parameter.IsActive`); usado por el motor de parámetros (`Validation.MaxUncompressedMB`, etc). Script: `scripts/register-reception-guard.sh:121`. | | Sin revisar | |
| `sanic_name` | Key | Texto (nvarchar) | required | | C#: `src/backend/CashHub.Plugins/Schema.cs:450` (`Parameter.Key = "sanic_name"`), leído en `scripts/register-reception-guard.sh:129` y otros scripts de provisioning. | | Sin revisar | |
| `sanic_value` | Value | Texto multilínea (ntext) | required | | C#: `src/backend/CashHub.Plugins/Schema.cs:451` (`Parameter.Value`); leído por `scripts/register-reception-guard.sh:121-129`. | | Sin revisar | |

### CH-Tbl-Playbook (`sanic_CHTblPlaybook`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_isactive` | IsActive | Dos opciones (bit) | none | | Solo en FormXml propio (`solutions/unpacked/core/Entities/sanic_CHTblPlaybook/FormXml/main/{710399ae...}.xml`). Ningún .fx.yaml/flow/C# lo lee. Script `scripts/provision-config-app.py:286` solo arma la vista/formulario (no aplicado aún, ver git status). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | Fx: `"solutions/unpacked/apps/CanvasApps/src/sanic_chappemailvalidationconsole_f3b93/Src/screen ColaDeProcesamiento.fx.yaml":654` (`LookUp('CH-Tbl-Playbook', Name = Switch(...))`). | | Sin revisar | |
| `sanic_nota` | Nota | Texto multilínea (ntext) | none | | Fx: `"...screen ColaDeProcesamiento.fx.yaml":672` (`g.Nota`). | | Sin revisar | |
| `sanic_orden` | Orden | Número entero | none | | Solo en FormXml propio. Ningún .fx.yaml/flow/C# lo lee; `scripts/provision-config-app.py:282` solo arma la vista (no aplicado aún). | | Sin revisar | |
| `sanic_pasos` | Pasos | Texto multilínea (ntext) | none | | Fx: `"...screen ColaDeProcesamiento.fx.yaml":659-660` (`g.Pasos`, se parte por línea para armar el instructivo). | | Sin revisar | |
| `sanic_titulo` | Titulo | Texto (nvarchar) | none | | Fx: `"...screen ColaDeProcesamiento.fx.yaml":656` (`g.Titulo`). | | Sin revisar | |

### CH-Tbl-Received Emails (`sanic_CHTblReceivedEmails`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_attachment` | Attachment | Archivo (file) | recommended | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_bancanotifiedon` | BancaNotifiedOn | Fecha y hora | none | | Fx: `"...screen ColaDeProcesamiento.fx.yaml":750-752` (`galBandeja.Selected.BancaNotifiedOn`). | | Sin revisar | |
| `sanic_client` | Cliente atribuido | Búsqueda (lookup) | none | | C#: `src/backend/CashHub.Plugins/ReceivedEmailValidationService.cs:617-688`; Fx: `Cuarentena.fx.yaml:508` (`galCuarentena.Selected.'Cliente atribuido'.Name`). | | Sin revisar | |
| `sanic_declaredcif` | DeclaredCif | Texto (nvarchar) | none | | Flow: `CH-Flow-UnblockOnClient-....json:52`; Fx: `Cuarentena.fx.yaml:509`. | | Sin revisar | |
| `sanic_emailreplystatus` | EmailReplyStatus | Option set (picklist) | none | | Flow: `CH-Flow-Replytoclientemail-....json:75,140`; `CH-Flow-ProcessEmail-....json:196`; `CH-Flow-MailboxSweep-....json:782`. | | Sin revisar | |
| `sanic_emailreviewmethod` | EmailReviewMethod | Option set (picklist) | recommended | | C#: `Schema.cs:16` (`ReceivedEmail.ReviewMethod`); Flow: `CH-Flow-ProcessEmail-....json:197`. | | Sin revisar | |
| `sanic_emailreviewstatus` | EmailReviewStatus | Option set (picklist) | recommended | | Flow: `CH-Flow-UnblockOnClient*.json`, `CH-Flow-ProcessEmail-....json:198`, `CH-Flow-MailboxSweep-....json:160`. Fx (por display name) en `"...ColaDeProcesamiento.fx.yaml":654`. | | Sin revisar | |
| `sanic_errorcode` | ErrorCode | Texto (nvarchar) | none | | Fx: `Cuarentena.fx.yaml:510` (`galCuarentena.Selected.ErrorCode`). | | Sin revisar | |
| `sanic_errortext` | ErrorText | Texto multilínea (ntext) | none | | C#: `Schema.cs:17`; Flow: `CH-Flow-Replytoclientemail-....json:173`; Fx: `Cuarentena.fx.yaml:404`. | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | C#: `Schema.cs:19` (`ReceivedEmail.TrackingName`); Flow: `CH-Flow-MailboxSweep-....json:781,847` (busca/loguea por `sanic_name`). | | Sin revisar | |
| `sanic_overrideby` | OverrideBy | Texto (nvarchar) | recommended | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_overrideon` | OverrideOn | Fecha y hora | recommended | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_overridereason` | OverrideReason | Texto multilínea (ntext) | none | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_receivedemailinternetmessageid` | ReceivedEmailInternetMessageId | Texto (nvarchar) | required | | Flow: `CH-Flow-MailboxSweep-....json:607`; `CH-Flow-ProcessEmail-....json:111,191`. Scripts: `scripts/create-alternate-keys.sh:83`, `scripts/seed-bandeja-demo.py:87`. | | Sin revisar | |
| `sanic_receivedemailoutlookuniqueid` | ReceivedEmailOutlookUniqueID | Texto (nvarchar) | required | | Flow: `CH-Flow-Replytoclientemail-....json:90,113`; `CH-Flow-ProcessEmail-....json:192,357,427`. | | Sin revisar | |
| `sanic_receptiondatetime` | ReceptionDateTime | Fecha y hora | recommended | | Flow: `CH-Flow-ProcessEmail-....json:195`; Fx: `Cuarentena.fx.yaml:319,387` (`ThisItem.ReceptionDateTime`). | | Sin revisar | |
| `sanic_repliedby` | RepliedBy | Texto (nvarchar) | recommended | | Flow: `CH-Flow-Replytoclientemail-....json:142` (`"CashHub (automatico)"`). | | Sin revisar | |
| `sanic_replyattempts` | ReplyAttempts | Número entero | none | | Flow: `CH-Flow-MailboxSweep-....json:782,823`. | | Sin revisar | |
| `sanic_replybodyhtml` | ReplyBodyHtml | Texto multilínea (ntext) | none | | Flow: `CH-Flow-Replytoclientemail-....json:54,82,114`; `CH-Flow-MailboxSweep-....json:782`. | | Sin revisar | |
| `sanic_replyclosenote` | ReplyCloseNote | Texto multilínea (ntext) | none | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | Sin revisar | |
| `sanic_replydatetime` | ReplyDateTime | Fecha y hora | recommended | | Flow: `CH-Flow-Replytoclientemail-....json:141`. | | Sin revisar | |
| `sanic_replyemailinternetmessageid` | ReplyEmailInternetMessageId | Texto (nvarchar) | recommended | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_replyemailoutlookuniqueid` | ReplyEmailOutlookUniqueID | Texto (nvarchar) | recommended | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_replysubject` | ReplySubject | Texto (nvarchar) | recommended | | Flow: `CH-Flow-Replytoclientemail-....json:115`. | | Sin revisar | |
| `sanic_senderclassification` | SenderClassification | Option set (picklist) | none | | C#: `ReceivedEmailValidationService.cs:565`; Flow: `CH-Flow-ProcessEmail-....json:307,387,393,454`. | | Sin revisar | |
| `sanic_sourceemailaddress` | SourceEmailAddress | Texto (nvarchar) | required | | C#: `Schema.cs:12`; Flow: `CH-Flow-ProcessEmail-....json:193`; Fx: `Cuarentena.fx.yaml:331,383`. | | Sin revisar | |
| `sanic_subject` | Subject | Texto (nvarchar) | recommended | | C#: `Schema.cs:13`; Flow: `CH-Flow-MailboxSweep-....json:159`; `CH-Flow-ProcessEmail-....json:194`; Fx: `Cuarentena.fx.yaml:306`. | | Sin revisar | |
| `sanic_triagenotes` | TriageNotes | Texto multilínea (ntext) | none | | C#: `Schema.cs:24`; Fx: `Cuarentena.fx.yaml:404`. | | Sin revisar | |
| `sanic_validationstatus` | ValidationStatus | Option set (picklist) | recommended | | C#: `Schema.cs:14`; Flow: `CH-Flow-UnblockOnClient*.json:83`, `CH-Flow-MailboxSweep-....json:160`; Fx: `Cuarentena.fx.yaml:512`. | | Sin revisar | |

### CH-Tbl-ReceptionBatch (`sanic_CHTblReceptionBatch`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_decniocash` | DecNIOCash | Decimal | none | | C#: `src/backend/CashHub.Plugins/ReceptionBatchTotalsPlugin.cs:122` (materializado sumando planillas del lote). | | Sin revisar | |
| `sanic_decniocheck` | DecNIOCheck | Decimal | none | | C#: `ReceptionBatchTotalsPlugin.cs:123`. | | Sin revisar | |
| `sanic_decniototal` | DecNIOTotal | Decimal | none | | C#: `ReceptionBatchTotalsPlugin.cs:124`. | | Sin revisar | |
| `sanic_decusdcash` | DecUSDCash | Decimal | none | | C#: `ReceptionBatchTotalsPlugin.cs:125`. | | Sin revisar | |
| `sanic_decusdcheck` | DecUSDCheck | Decimal | none | | C#: `ReceptionBatchTotalsPlugin.cs:126`. | | Sin revisar | |
| `sanic_decusdtotal` | DecUSDTotal | Decimal | none | | C#: `ReceptionBatchTotalsPlugin.cs:127`. | | Sin revisar | |
| `sanic_location` | Location | Búsqueda (lookup) | recommended | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdepositstracker_2b2c3/Src/scrLote.fx.yaml:209` (`gvarLote.Location.Name`, dot-walk). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappreception_9ff29/Src/scrLotes.fx.yaml:365,521` (`ThisItem.ReceptionBatch.Name`, `galLotesLot.Selected.Name`). | | Sin revisar | |
| `sanic_receptionstatus` | ReceptionStatus | Option set (picklist) | recommended | | C#: `src/backend/CashHub.Plugins/ReceptionBatchStatusMirrorPlugin.cs:65,135-136`; `GuardReceptionPlugin.cs:108`. | | Sin revisar | |
| `sanic_transportadora` | Transportadora | Texto (nvarchar) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappreception_9ff29/Src/scrLotes.fx.yaml:144,3610` (`ThisItem.Transportadora`, `galLotesLot.Selected.Transportadora`). | | Sin revisar | |

### CH-Tbl-Receptions (`sanic_CHTblReceptions`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_batchstatus` | BatchStatus | Número entero | none | | C#: `src/backend/CashHub.Plugins/Schema.cs:566` (`Reception.BatchStatusMirror`), mantenido por `ReceptionBatchStatusMirrorPlugin`. | | Sin revisar | |
| `sanic_branchname` | BranchName | Búsqueda (lookup) | recommended | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappreception_9ff29/Src/scrCaptura.fx.yaml:46,2671` (`D.BranchName.'CH-Tbl-ClientBranch'`). | | Sin revisar | |
| `sanic_comments` | Comments | Texto (nvarchar) | none | | Sin coincidencias en .fx.yaml/flow/C#/scripts/FormXml propio/SavedQueries propio — solo existe en su Entity.xml. | | **ELIMINADO 2026-08-31** | |
| `sanic_name` | Name | Texto (nvarchar) | required | | C#: `Schema.cs:558` (`Reception.Name`); Fx: `scrLotes.fx.yaml:521` (`galLotesLot.Selected.Name`, sobre `CH-Tbl-Receptions`). | | Sin revisar | |
| `sanic_niocash` | NIOCash | Decimal | recommended | | C#: `Schema.cs:569` (`Reception.NioCash`). | | Sin revisar | |
| `sanic_niocheck` | NIOCheck | Decimal | recommended | | C#: `Schema.cs:570` (`Reception.NioCheck`). | | Sin revisar | |
| `sanic_receiptid` | ReceiptID | Texto (nvarchar) | required | | C#: `Schema.cs:557`; Fx: `scrLotes.fx.yaml:1598,3707` (`galPlanillasLot.Selected.ReceiptID`). | | Sin revisar | |
| `sanic_receptionbatch` | ReceptionBatch | Búsqueda (lookup) | recommended | | C#: `Schema.cs:556` (`Reception.BatchLookup`); Fx: `scrCaptura.fx.yaml:26` (`Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = ...)`). | | Sin revisar | |
| `sanic_remitentclient` | RemitentClient | Búsqueda (lookup) | required | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrCheques.fx.yaml:982` (`ThisItem.Bag.ReceptionID.RemitentClient.Name`). | | Sin revisar | |
| `sanic_remitentname` | RemitentName | Texto (nvarchar) | none | | Fx: `scrLotes.fx.yaml:348,1344,1598` (`ThisItem.RemitentName`, `galPlanillasLot.Selected.RemitentName`). | | Sin revisar | |
| `sanic_usdcash` | USDCash | Decimal | recommended | | C#: `Schema.cs:571` (`Reception.UsdCash`). | | Sin revisar | |
| `sanic_usdcheck` | USDCheck | Decimal | recommended | | C#: `Schema.cs:572` (`Reception.UsdCheck`). | | Sin revisar | |

### CH-Tbl-RecountingSession (`sanic_CHTblRecountingSession`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cashier` | Cashier | Búsqueda (lookup) | required | | C#: `src/backend/CashHub.Plugins/Schema.cs:793` (`RecountingSession.Cashier`), usado por `RecountingSessionAutoApprovePlugin`. | | Sin revisar | |
| `sanic_confirmed` | Confirmed | Dos opciones (bit) | recommended | | C#: `Schema.cs:803` — "la app v2 NUNCA la escribe: la estampa `RecountingSessionAutoApprovePlugin`". | | Sin revisar | |
| `sanic_confirmedby` | ConfirmedBy | Búsqueda (lookup) | recommended | | C#: `Schema.cs:806`. | | Sin revisar | |
| `sanic_countingdesk` | CountingDesk | Búsqueda (lookup) | required | | C#: `Schema.cs:796`. | | Sin revisar | |
| `sanic_forcedclosureby` | ForcedClosureBy | Búsqueda (lookup) | recommended | | C#: `Schema.cs:811`. | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml:486` y `scrTulas.fx.yaml:257` (`gvarSesion.Name`). | | Sin revisar | |
| `sanic_reasonforforcedflosure` | ReasonForForcedFlosure | Texto (nvarchar) | recommended | | C#: `Schema.cs:819` — OJO: el logical name tiene la errata "flosure" (verificado, no corregir). | | Sin revisar | |
| `sanic_sessionforciblyclosed` | SessionForciblyClosed | Dos opciones (bit) | recommended | | C#: `Schema.cs:808`. | | Sin revisar | |

### CH-Tbl-RecountingSessionDetail (`sanic_CHTblRecountingSessionDetail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_bagid` | BagId | Búsqueda (lookup) | required | | C#: `src/backend/CashHub.Plugins/Schema.cs:856` (`RecountingSessionDetail.BagLookup`). | | Sin revisar | |
| `sanic_name` | RecountingSessionDetailId | Texto (nvarchar) | required | | Solo en FormXml propio (main/quick/card). Ningún .fx.yaml/flow/C#/script lee o escribe su propio `sanic_name` (no confundir con el lookup `sanic_recountingsessiondetailid` de otras tablas, que sí se usa mucho). | | Sin revisar | |
| `sanic_recountingsessiondetailstatus` | RecountingSessionDetailStatus | Option set (picklist) | none | | C#: `Schema.cs:860`; Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrResumen.fx.yaml:442` (`RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias'`). | | Sin revisar | |
| `sanic_recountingsessionid` | RecountingSessionId | Búsqueda (lookup) | required | | C#: `Schema.cs:855` (`RecountingSessionDetail.SessionLookup`). | | Sin revisar | |
| `sanic_supervisorid` | SupervisorId | Búsqueda (lookup) | none | | C#: `Schema.cs:857` (`RecountingSessionDetail.SupervisorLookup`). | | Sin revisar | |

### CH-Tbl-SystemHeartbeat (`sanic_CHTblSystemHeartbeat`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_details` | Details | Texto multilínea (ntext) | recommended | | Flow: `solutions/unpacked/flow/Workflows/CH-Flow-MailboxSweep-FB8E0A66-F4FE-4825-9D0C-78FB16271516.json:424,455` (`item/sanic_details`). | | Sin revisar | |
| `sanic_lastrunon` | LastRunOn | Fecha y hora | required | | Flow: `CH-Flow-MailboxSweep-....json:423,454` (`item/sanic_lastrunon`). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Flow: `CH-Flow-MailboxSweep-....json:397,456` (`$filter sanic_name eq 'ingesta-correos'` / `item/sanic_name`). | | Sin revisar | |

### CH-Tbl-Transactions (`sanic_CHTblTransactions`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_accountnumber` | AccountNumber | Decimal | recommended | | C#: `src/backend/CashHub.Plugins/Schema.cs:663` (`Transaction.AccountNumber`, en `MoneyAttributes`). | | Sin revisar | |
| `sanic_baclocalcheckamount` | BACLocalCheckAmount | Decimal | recommended | | C#: `Schema.cs:666`. | | Sin revisar | |
| `sanic_batchstatus` | BatchStatus | Número entero | none | | C#: `Schema.cs:715` (`Transaction.BatchStatusMirror`, mantenido por `TransactionBatchStatusMirrorPlugin`). | | Sin revisar | |
| `sanic_cajero` | Cajero | Búsqueda (lookup) | none | | C#: `Schema.cs:654` (`Transaction.Cajero` — "cajero que recontó la plata, se estampa al crearlo"). | | Sin revisar | **NUEVO desde el export del 2026-08-24** |
| `sanic_cashamount` | CashAmount | Decimal | none | | C#: `Schema.cs:670`, en `MoneyAttributes`. | | Sin revisar | |
| `sanic_comentarios` | Comentarios | Texto (nvarchar) | none | | Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrManual.fx.yaml:1527` (`gvarTxMan.Comentarios`). | | Sin revisar | |
| `sanic_currency` | Currency | Option set (picklist) | none | | C#: `Schema.cs:671`; Fx: `scrLotes.fx.yaml:1749` (`Text(gvarTxLot.Currency)`). | | Sin revisar | |
| `sanic_motivorechazocore` | MotivoRechazoCore | Texto multilínea (ntext) | none | | C#: `Schema.cs:722` (lo escribe el Custom API `sanic_CHRemoveFromCut`); Fx: `scrManual.fx.yaml:1527`. | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | Fx: `scrLotes.fx.yaml:1305,1737` y `scrManual.fx.yaml:1655` (`gvarTxLot.Name`, `gvarTxMan.Name`). | | Sin revisar | |
| `sanic_otherlocalcheckamount` | OtherLocalCheckAmount | Decimal | recommended | | C#: `Schema.cs:667`, en `MoneyAttributes`. | | Sin revisar | |
| `sanic_relateddepositrecounting` | RelatedDepositRecounting | Búsqueda (lookup) | recommended | | C#: `Schema.cs:641`; Fx: `scrManual.fx.yaml:1642` (`gvarTxMan.RelatedDepositRecounting.Name`). | | Sin revisar | |
| `sanic_supervisorautoriza` | Supervisor que autoriza | Búsqueda (lookup) | none | | C#: `Schema.cs:662` (`Transaction.SupervisorAutoriza` — "solo se llena cuando nace de una aprobación"). | | Sin revisar | **NUEVO desde el export del 2026-08-24** |
| `sanic_total` | Total | Decimal | none | | C#: `Schema.cs:693` (campo calculado, `ValidForCreateApi/UpdateApi = 0`); Fx: `scrLotes.fx.yaml:1749` (`Text(gvarTxLot.Total, FmtMoney)`). | | Sin revisar | |
| `sanic_transactionbatch` | Transaction Batch | Búsqueda (lookup) | none | | C#: `Schema.cs:640`; Fx: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrMovimientos.fx.yaml:485` (`ThisItem.'Transaction Batch'.Name`). | | Sin revisar | |
| `sanic_transactionid` | TransactionID | Texto (nvarchar) | recommended | | C#: `Schema.cs:686` (`Transaction.TransactionIdText`). | | Sin revisar | |
| `sanic_transactionnumber` | TransactionNumber | Número entero | recommended | | C#: `Schema.cs:673`. | | Sin revisar | |
| `sanic_transactionprocessingtype` | TransactionProcessingType | Option set (picklist) | recommended | | C#: `Schema.cs:665` (`Transaction.ProcessingType`). | | Sin revisar | |
| `sanic_transactionreference` | TransactionReference | Texto (nvarchar) | recommended | | C#: `Schema.cs:679`. | | Sin revisar | |
| `sanic_transactionstatus` | TransactionStatus | Option set (picklist) | none | | C#: `Schema.cs:664`. | | Sin revisar | |
| `sanic_transactiontype` | TransactionType | Option set (picklist) | recommended | | C#: `Schema.cs:672`. | | Sin revisar | |

### CH-Tbl-TransportCompany (`sanic_CHTblTransportCompany`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | required | | Solo en FormXml propio (main/quick/card). `scripts/provision-config-app.py:345-351` solo arma la vista/formulario (script de la Config App, no aplicado aún). Ningún .fx.yaml/flow/C# lo lee. | | Sin revisar | |

### CH-Tbl-ValidationResults (`sanic_CHTblValidationResults`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_cellref` | Celda | Texto (nvarchar) | none | | C#: `src/backend/CashHub.Plugins/ReceivedEmailValidationService.cs:372` (`Schema.Result.CellRef`). | | Sin revisar | |
| `sanic_detail` | Detalle | Texto multilínea (ntext) | none | | C#: `ReceivedEmailValidationService.cs:370` (`Schema.Result.Detail`). | | Sin revisar | |
| `sanic_durationms` | Durationms | Número entero | recommended | | C#: `ReceivedEmailValidationService.cs:363` (`Schema.Result.DurationMs`). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | C#: `ReceivedEmailValidationService.cs:360` (`Schema.Result.Name`). | | Sin revisar | |
| `sanic_rownumber` | Fila | Número entero | none | | C#: `ReceivedEmailValidationService.cs:373` (`Schema.Result.RowNumber`). | | Sin revisar | |
| `sanic_sheet` | Hoja | Texto (nvarchar) | none | | C#: `ReceivedEmailValidationService.cs:371` (`Schema.Result.Sheet`). | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | recommended | | C#: `ReceivedEmailValidationService.cs:364` (`Schema.Result.SourceType`). | | Sin revisar | |
| `sanic_validationresult` | ValidationResult | Option set (picklist) | recommended | | C#: `ReceivedEmailValidationService.cs:362` (`Schema.Result.Outcome`). | | Sin revisar | |
| `sanic_validationrule` | ValidationRule | Búsqueda (lookup) | required | | C#: `ReceivedEmailValidationService.cs:368` (`Schema.Result.RuleLookup`). | | Sin revisar | |
| `sanic_validationrun` | ValidationRun | Búsqueda (lookup) | required | | C#: `ReceivedEmailValidationService.cs:361` (`Schema.Result.RunLookup`). | | Sin revisar | |

### CH-Tbl-ValidationRuns (`sanic_CHTblValidationRuns`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_name` | Name | Texto (nvarchar) | recommended | | C#: `src/backend/CashHub.Plugins/ReceivedEmailValidationService.cs:302` (`Schema.Run.Name`). | | Sin revisar | |
| `sanic_receivedemail` | ReceivedEmail | Búsqueda (lookup) | none | | C#: `ReceivedEmailValidationService.cs:308` (`Schema.Run.ReceivedEmailLookup`). | | Sin revisar | |
| `sanic_runend` | RunEnd | Fecha y hora | none | | C#: `ReceivedEmailValidationService.cs:304` (`Schema.Run.End`). | | Sin revisar | |
| `sanic_runresult` | RunResult | Option set (picklist) | recommended | | C#: `ReceivedEmailValidationService.cs:305` (`Schema.Run.Result`). | | Sin revisar | |
| `sanic_runstart` | RunStart | Fecha y hora | required | | C#: `ReceivedEmailValidationService.cs:303` (`Schema.Run.Start`). | | Sin revisar | |
| `sanic_sourceentity` | SourceEntity | Texto (nvarchar) | required | | C#: `ReceivedEmailValidationService.cs:306` (`Schema.Run.SourceEntity`). | | Sin revisar | |
| `sanic_sourcerecordid` | SourceRecordID | Texto (nvarchar) | required | | C#: `ReceivedEmailValidationService.cs:307` (`Schema.Run.SourceRecordId`). | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | required | | C#: `ReceivedEmailValidationService.cs:310` (`Schema.Run.SourceType`). | | Sin revisar | |

### CH-Tbl-XMLReading (`sanic_CHTblXMLReading`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_filename` | FileName | Texto (nvarchar) | required | | Flow: `solutions/unpacked/flow/Workflows/CH-Flow-ProcessXMLFiles-0F5B3097-0D44-F111-88B4-00224808C7C9.json:253` (`item/sanic_filename`). | | Sin revisar | |
| `sanic_isactive` | IsActive | Dos opciones (bit) | none | | C#: `src/backend/CashHub.Plugins/XmlReadingDetailMirrorPlugin.cs:130,134-181` (`Schema.XmlReading.IsActive`), origen del espejo E-4. | | Sin revisar | |
| `sanic_name` | XMLReadingId | Texto (nvarchar) | none | | Solo en FormXml propio (main/quick/card). Ningún .fx.yaml/flow/C#/script lo lee o escribe. | | Sin revisar | |
| `sanic_recountingsessiondetailid` | RecountingSessionDetailId | Búsqueda (lookup) | required | | C#: `XmlReadingDetailMirrorPlugin.cs:130,133,167,173,175` (`Schema.XmlReading.SessionDetailLookup`); Flow: `CH-Flow-ProcessXMLFiles-....json:161,183,252` (`entityName: sanic_chtblxmlreadings`). | | Sin revisar | |

### CH-Tbl-XMLReadingDetail (`sanic_CHTblXMLReadingDetail`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_currency` | Currency | Texto (nvarchar) | required | | Flow: `CH-Flow-ProcessXMLFiles-....json:277` (`item/sanic_currency`: `xpath(item(), 'string(/*/@Currency)')`). | | Sin revisar | |
| `sanic_denomination` | Denomination | Decimal | none | | Flow: `CH-Flow-ProcessXMLFiles-....json:279` (`item/sanic_denomination`). | | Sin revisar | |
| `sanic_name` | XMLReadingDetailId | Texto (nvarchar) | none | | Flow: `CH-Flow-ProcessXMLFiles-....json:281` (`item/sanic_name: " "`, se escribe un placeholder en blanco al crear el detalle). | | Sin revisar | |
| `sanic_quantity` | Quantity | Número entero | none | | Flow: `CH-Flow-ProcessXMLFiles-....json:280` (`item/sanic_quantity`). | | Sin revisar | |
| `sanic_recountingsessiondetailmirror` | RecountingSessionDetailMirror | Búsqueda (lookup) | none | | C#: `src/backend/CashHub.Plugins/Schema.cs:908` (`XmlReadingDetail.SessionDetailMirror`), mantenido por `XmlReadingDetailMirrorPlugin` para que el Filter de "Leer contadora" delegue en un salto. | | Sin revisar | |
| `sanic_subtotal` | Subtotal | Decimal | none | | Columna FÓRMULA de Dataverse: `solutions/unpacked/core/Entities/sanic_CHTblXMLReadingDetail/Formulas/sanic_chtblxmlreadingdetail-FormulaDefinitions.yaml:1` (`sanic_subtotal: =sanic_denomination * sanic_quantity`). No se escribe manualmente (confirmado en `scripts/seed-recounting-stress.py:2120`). | | Sin revisar | |
| `sanic_xmlreading` | XMLReading | Búsqueda (lookup) | required | | C#: `Schema.cs:899` (`XmlReadingDetail.ReadingLookup`). | | Sin revisar | |
| `sanic_xmlreadingactiva` | XMLReadingActiva | Dos opciones (bit) | none | | C#: `Schema.cs:916` (`XmlReadingDetail.ActiveMirror`), espejo de `XMLReading.IsActive`. | | Sin revisar | |

### CH-Tbl-TransactionBatches (`sanic_TransactionBatches`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_aplicadoon` | Aplicado el | Fecha y hora | none | | C#: `src/backend/CashHub.Plugins/GuardTransactionPlugin.cs` (`Schema.TransactionBatch.AplicadoOn`), estampado por `BatchTransitionStampPlugin`. | | Sin revisar | |
| `sanic_aplicadopor` | Aplicado por | Búsqueda (lookup) | none | | C#: `Schema.cs:1002` (`TransactionBatch.AplicadoPor`). | | Sin revisar | |
| `sanic_batchstatus` | BatchStatus | Option set (picklist) | none | | C#: `src/backend/CashHub.Plugins/GuardTransactionPlugin.cs:101,124-143` (`Schema.TransactionBatch.BatchStatus`). | | Sin revisar | |
| `sanic_cajero` | Cajero | Búsqueda (lookup) | none | | C#: `Schema.cs:981` (`TransactionBatch.Cajero`, estampado por `CreateTransactionCutPlugin`). | | Sin revisar | |
| `sanic_comentarios` | Comentarios | Texto (nvarchar) | none | | C#: `Schema.cs:966` (`TransactionBatch.Comentarios`). | | Sin revisar | |
| `sanic_csvcore` | CSV para el core | Archivo (file) | none | | C#: `Schema.cs:992` (`TransactionBatch.CsvCore`, subido por `CH-Flow-DownloadData`, leído por el Custom API `BatchCsvApi`). | | Sin revisar | |
| `sanic_devueltoon` | Devuelto el | Fecha y hora | none | | C#: `Schema.cs:1015` (`TransactionBatch.DevueltoOn`); `GuardTransactionPlugin.cs` valida el motivo de devolución. | | Sin revisar | **NUEVO desde el export del 2026-08-24** |
| `sanic_devueltopor` | Devuelto por | Búsqueda (lookup) | none | | C#: `Schema.cs:1014` (`TransactionBatch.DevueltoPor`). | | Sin revisar | **NUEVO desde el export del 2026-08-24** |
| `sanic_generadopor` | Generado por | Búsqueda (lookup) | none | | C#: `Schema.cs:982` (`TransactionBatch.GeneradoPor`, estampado por `CreateTransactionCutPlugin`). | | Sin revisar | |
| `sanic_ingresadoon` | Ingresado el | Fecha y hora | none | | C#: `Schema.cs:1001` (`TransactionBatch.IngresadoOn`). | | Sin revisar | |
| `sanic_ingresadopor` | Ingresado por | Búsqueda (lookup) | none | | C#: `src/backend/CashHub.Plugins/GuardTransactionPlugin.cs:391,397` (`Schema.TransactionBatch.IngresadoPor`), insumo de la regla de segregación de funciones. | | Sin revisar | |
| `sanic_motivodevolucion` | Motivo de la devolucion | Texto multilínea (ntext) | none | | C#: `src/backend/CashHub.Plugins/GuardTransactionPlugin.cs:160-161` (`Schema.TransactionBatch.MotivoDevolucion`), obligatorio al marcar el lote Devuelto. | | Sin revisar | **NUEVO desde el export del 2026-08-24** |
| `sanic_name` | Name | Texto (nvarchar) | recommended | | C#: `src/backend/CashHub.Plugins/CHBatchSummaryPlugin.cs:59-60` (`Schema.TransactionBatch.Name`). | | Sin revisar | |
| `sanic_totaleur` | TotalEUR | Decimal | none | | C#: `Schema.cs:975` (`TransactionBatch.TotalEur`, recomputado por `BatchTotalsCalculator`). | | Sin revisar | |
| `sanic_totalnio` | TotalNIO | Decimal | none | | C#: `Schema.cs:973` (`TransactionBatch.TotalNio`). | | Sin revisar | |
| `sanic_totalusd` | TotalUSD | Decimal | none | | C#: `Schema.cs:974` (`TransactionBatch.TotalUsd`). | | Sin revisar | |

### CH-Tbl-ValidationRules (`sanic_ValidationRules`)

| Logical name (schema) | Display name | Tipo | Requerido | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_isactive` | IsActive | Dos opciones (bit) | required | | C#: `src/backend/CashHub.Plugins/ReceivedEmailValidationService.cs:261,282` (`Schema.Rule.IsActive`). | | Sin revisar | |
| `sanic_name` | Name | Texto (nvarchar) | none | | C#: `ReceivedEmailValidationService.cs:260,280` (`Schema.Rule.Name`). | | Sin revisar | |
| `sanic_notes` | Notes | Texto multilínea (ntext) | recommended | | Solo en FormXml propio (`.../sanic_ValidationRules/FormXml/main/{1bb9c702...}.xml`). `scripts/provision-config-app.py:252` solo arma el formulario (no aplicado aún). Ningún .fx.yaml/flow/C# lo lee. | | Sin revisar | |
| `sanic_sourcetype` | SourceType | Option set (picklist) | required | | Solo en FormXml propio. `scripts/provision-config-app.py:249` solo arma el formulario (no aplicado aún). OJO: no confundir con `sanic_sourcetype` de CH-Tbl-ValidationResults/CH-Tbl-ValidationRuns, que sí se usa en C# (`Schema.Result.SourceType`, `Schema.Run.SourceType`) — son campos homónimos en tablas distintas. | | Sin revisar | |
| `sanic_validationdatatype` | ValidationDataType | Option set (picklist) | required | | Solo en FormXml propio. `scripts/provision-config-app.py:249` solo arma el formulario (no aplicado aún). | | Sin revisar | |
| `sanic_validationdependency` | ValidationDependency | Búsqueda (lookup) | recommended | | C#: `ReceivedEmailValidationService.cs:261,291` (`Schema.Rule.Dependency`). | | Sin revisar | |
| `sanic_validationfailmessage` | ValidationFailMessage | Texto (nvarchar) | required | | C#: `ReceivedEmailValidationService.cs:261,284` (`Schema.Rule.FailMessage`). | | Sin revisar | |
| `sanic_validationname` | Validation Name | Texto (nvarchar) | required | | C#: `ReceivedEmailValidationService.cs:260,271,277,279` (`Schema.Rule.Key`). | | Sin revisar | |
| `sanic_validationoperation` | Validation Operation | Option set (picklist) | none | | Solo en FormXml propio. `scripts/provision-config-app.py:244,249,257` solo arma vista/formulario (no aplicado aún). | | Sin revisar | |
| `sanic_validationorder` | ValidationOrder | Número entero | recommended | | C#: `ReceivedEmailValidationService.cs:261,281` (`Schema.Rule.Order`). | | Sin revisar | |
| `sanic_validationvalue` | ValidationValue | Texto (nvarchar) | required | | C#: `ReceivedEmailValidationService.cs:261,283` (`Schema.Rule.Value`). | | Sin revisar | |

## 3. Relaciones (1:N y N:N)

> 69 relaciones de negocio en el export (antes 66) — **las 66 originales siguen existiendo, sin ausentes**. Se agregaron 3 relaciones nuevas, las tres hacia `SystemUser` desde campos de auditoría/autorización agregados en esta sync: `sanic_systemuser_transactionbatches_devueltopor` (`sanic_DevueltoPor`), `sanic_transaction_cajero` (`sanic_Cajero` en Transactions) y `sanic_transaction_supervisorautoriza` (`sanic_SupervisorAutoriza`). Total de relaciones en el export: 321 (antes 318). Boilerplate excluida: sigue en 252 (41 `owner_*`, 41 `business_unit_*`, 41 `team_*`, 4 `FileAttachment_*`, resto `systemuser_*`/CreatedBy/ModifiedBy — igual que antes, no cambió).

| Schema name relación | Tabla 1 (referencing) | Tabla 2 (referenced) | Tipo | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_CHTblAppCatalog_CHTblAppCatalogAccess` | `sanic_CHTblAppCatalogAccess` | `sanic_CHTblAppCatalog` | 1:N (lookup `sanic_AppCatalog`) | | Apps: CH-App-Hub. Plugins: Schema.cs. Scripts: provision-appcatalog-access.sh, provision-config-app.py, register-appcatalog-guard.sh. | | Sin revisar | |
| `sanic_CHTblReceivedEmails_Declarations` | `sanic_CHTblDeclaration` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_ReceivedEmail`) | | Apps: CH-App-DepositsTracker, CH-App-IngestionConsole. Flows: CH-Flow-MailboxSweep, CH-Flow-ProcessEmail, CH-Flow-Replytoclientemail. Plugins: DeclarationMaterializer.cs, Schema.cs. Scripts: backfill-tracker.py, create-alternate-keys.sh, create-tracker-columns.sh, seed-bandeja-demo.py (+2 más). | | Sin revisar | |
| `sanic_CHTblReceivedEmails_ValidationRuns` | `sanic_CHTblValidationRuns` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_ReceivedEmail`) | | Apps: CH-App-DepositsTracker, CH-App-IngestionConsole. Flows: CH-Flow-MailboxSweep, CH-Flow-ProcessEmail, CH-Flow-Replytoclientemail. Plugins: DeclarationMaterializer.cs, Schema.cs. Scripts: backfill-tracker.py, create-alternate-keys.sh, create-tracker-columns.sh, seed-bandeja-demo.py (+2 más). | | Sin revisar | |
| `sanic_chtblapprovalrequest_SolvedBy_systemuser` | `sanic_CHTblApprovalRequest` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-NotifywhenRFCimplementationfinish. Plugins: GuardReceptionPlugin.cs, Schema.cs. Scripts: apply-supervisor-role-remediation.sh, register-reception-guard.sh, seed-clientadmin-stress.py, seed-recounting-stress.py (+1 más). | | Sin revisar | |
| `sanic_chtblbags_BagDeclarationID_sanic_chtbldeclaredbags` | `sanic_CHTblBags` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_BagDeclarationID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagReconciliationMirrorPlugin.cs, Schema.cs. Scripts: backfill-tracker.py, register-tracker-mirrors.sh, seed-qa-escenarios-tulas.py, seed-recounting-stress.py (+2 más). | | Sin revisar | |
| `sanic_chtblbags_ReceptionBatch_sanic_chtblreceptionbatch` | `sanic_CHTblBags` | `sanic_CHTblReceptionBatch` | 1:N (lookup `sanic_ReceptionBatchId`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagReceptionMirrorPlugin.cs, BagReceptionMirrors.cs, Schema.cs, SupervisorMirrorSchema.cs. Scripts: backfill-supervisor-mirrors.py, create-supervisor-mirror-columns.sh, register-supervisor-mirrors.sh, seed-qa-escenarios-tulas.py. | | Sin revisar | |
| `sanic_chtblbags_ReceptionID_sanic_chtblreceptions` | `sanic_CHTblBags` | `sanic_CHTblReceptions` | 1:N (lookup `sanic_ReceptionID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagReceptionMirrorPlugin.cs, BagReceptionMirrors.cs, Schema.cs, SupervisorMirrorSchema.cs. Scripts: backfill-supervisor-mirrors.py, register-reception-guard.sh, register-supervisor-mirrors.sh, seed-qa-escenarios-tulas.py (+2 más). | | Sin revisar | |
| `sanic_chtblbags_RecountingSession_sanic_chtblrecountingsession` | `sanic_CHTblBags` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSession`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: CheckGuard.cs, CheckGuardTests.cs, CheckMirrorStampPlugin.cs, CheckMirrors.cs, DepositSessionMirrorPlugin.cs (+7 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh, create-supervisor-mirror-columns.sh (+8 más). | | Sin revisar | |
| `sanic_chtblchecks_Bag_sanic_chtblbags` | `sanic_CHTblChecks` | `sanic_CHTblBags` | 1:N (lookup `sanic_Bag`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: ApprovalBagNumber.cs, ApprovalEffectRules.cs, ApprovalEffectRulesTests.cs, ApprovalRequestBagNumberPlugin.cs, BagReconciliation.cs (+19 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, backfill-tracker.py, build-test-workbooks.py (+17 más). | | Sin revisar | |
| `sanic_chtblchecks_ReceivedBy_systemuser` | `sanic_CHTblChecks` | `SystemUser` | 1:N (lookup `sanic_ReceivedBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckStatusStampPlugin.cs, Schema.cs. Scripts: create-digitization-columns.sh. | | Sin revisar | |
| `sanic_chtblchecks_RecountingSession_sanic_chtblrecountingsession` | `sanic_CHTblChecks` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSession`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: CheckGuard.cs, CheckGuardTests.cs, CheckMirrorStampPlugin.cs, CheckMirrors.cs, DepositSessionMirrorPlugin.cs (+7 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh, create-supervisor-mirror-columns.sh (+8 más). | | Sin revisar | |
| `sanic_chtblchecks_RelatedDeclaredCheck_sanic_chtbldeclaredchecks` | `sanic_CHTblChecks` | `sanic_CHTblDeclaredChecks` | 1:N (lookup `sanic_RelatedDeclaredCheck`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckMirrorStampPlugin.cs, CheckMirrors.cs, Schema.cs. Scripts: register-tracker-mirrors.sh, seed-recounting-stress.py, seed-tracker-stress-fix.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblchecks_RelatedReCounterDepositID_sanic_chtbldepositsrecounting` | `sanic_CHTblChecks` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedReCounterDepositID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckMirrorStampPlugin.cs, GuardRecountingPlugin.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, backfill-tracker.py, register-recounting-guard.sh (+5 más). | | Sin revisar | |
| `sanic_chtblchecks_SentToDigitizationBy_systemuser` | `sanic_CHTblChecks` | `SystemUser` | 1:N (lookup `sanic_SentToDigitizationBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckStatusStampPlugin.cs, Schema.cs. Scripts: create-digitization-columns.sh. | | Sin revisar | |
| `sanic_chtblclientaccount_Client_sanic_chtblclient` | `sanic_CHTblClientAccount` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtblclientbranch_Client_sanic_chtblclient` | `sanic_CHTblClientBranch` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtblclientdatachangerequest_RelatedClient_sanic_chtblclient` | `sanic_CHTblClientDataChangeRequest` | `sanic_CHTblClient` | 1:N (lookup `sanic_RelatedClient`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-NotifywhenRFCimplementationfinish. Plugins: Schema.cs. Scripts: seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdatachangerequest_SolvedBy_systemuser` | `sanic_CHTblClientDataChangeRequest` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-NotifywhenRFCimplementationfinish. Plugins: GuardReceptionPlugin.cs, Schema.cs. Scripts: apply-supervisor-role-remediation.sh, register-reception-guard.sh, seed-clientadmin-stress.py, seed-recounting-stress.py (+1 más). | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_ChangeRequestID_sanic_chtblclientdatachangerequest` | `sanic_CHTblClientDataChangeRequestLine` | `sanic_CHTblClientDataChangeRequest` | 1:N (lookup `sanic_ChangeRequestID`) | | Apps: CH-App-ClientAdministration. Plugins: GuardChangeRequestLinePlugin.cs, Schema.cs. Scripts: register-changerequestline-guard.sh, seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_FieldDefinition_sanic_chtblclienttablelogicalfieldname` | `sanic_CHTblClientDataChangeRequestLine` | `sanic_CHTblClientTableLogicalFieldName` | 1:N (lookup `sanic_FieldDefinition`) | | Apps: CH-App-ClientAdministration. Flows: CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata. Plugins: ChangeRequestLineGuard.cs, GuardChangeRequestLinePlugin.cs, ResolveChangeRequestPlugin.cs, Schema.cs. Scripts: register-changerequestline-guard.sh, seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdatachangerequestline_SolvedBy_systemuser` | `sanic_CHTblClientDataChangeRequestLine` | `SystemUser` | 1:N (lookup `sanic_SolvedBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-NotifywhenRFCimplementationfinish. Plugins: GuardReceptionPlugin.cs, Schema.cs. Scripts: apply-supervisor-role-remediation.sh, register-reception-guard.sh, seed-clientadmin-stress.py, seed-recounting-stress.py (+1 más). | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedCRL_sanic_chtblclientdatachangerequestline` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClientDataChangeRequestLine` | 1:N (lookup `sanic_RelatedCRL`) | | Apps: CH-App-ClientAdministration. Flows: CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata. Plugins: Schema.cs. Scripts: seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedClient_sanic_chtblclient` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClient` | 1:N (lookup `sanic_RelatedClient`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-NotifywhenRFCimplementationfinish. Plugins: Schema.cs. Scripts: seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdatachangesaudittrail_RelatedRFC_sanic_chtblclientdatachangerequest` | `sanic_CHTblClientDataChangesAuditTrail` | `sanic_CHTblClientDataChangeRequest` | 1:N (lookup `sanic_RelatedRFC`) | | Apps: CH-App-ClientAdministration. Flows: CH-Flow-Activaterow, CH-Flow-Applyclientdatachanges, CH-Flow-Createclientdata, CH-Flow-Deactivaterow, CH-Flow-RFCWatchdog. Plugins: Schema.cs. Scripts: seed-clientadmin-stress.py. | | Sin revisar | |
| `sanic_chtblclientdeposithistory_Client_sanic_chtblclient` | `sanic_CHTblClientDepositHistory` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtblclientemail_Client_sanic_chtblclient` | `sanic_CHTblClientEmail` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtblcountedmoneydetails_RelatedDepositRecountingID_sanic_chtbldepositsrecounting` | `sanic_CHTblCountedMoneyDetails` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedDepositRecountingID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: GuardRecountingPlugin.cs, Schema.cs. Scripts: register-recounting-guard.sh, seed-recounting-stress.py. | | Sin revisar | |
| `sanic_chtblcountingdesk_Cajero_systemuser` | `sanic_CHTblCountingDesk` | `SystemUser` | 1:N (lookup `sanic_Cajero`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CHWorkloadByCashierPlugin.cs, CreateTransactionCutPlugin.cs, RecountingGuard.cs, Schema.cs. Scripts: backfill-cajero-en-transacciones.py, provision-config-app.py, register-transaction-cut.sh, seed-digitization-stress.py (+1 más). | | Sin revisar | |
| `sanic_chtbldeclaration_Client_sanic_chtblclient` | `sanic_CHTblDeclaration` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtbldeclaredbags_BagID_sanic_chtblbags` | `sanic_CHTblDeclaredBags` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagRecountClosurePlugin.cs, CheckMirrorStampPlugin.cs, DepositSessionMirrorPlugin.cs, GuardRecountingPlugin.cs, RecountClosureAccounting.cs (+1 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh, register-reception-guard.sh (+8 más). | | Sin revisar | |
| `sanic_chtbldeclaredbags_DeclarationID_sanic_chtbldeclaration` | `sanic_CHTblDeclaredBags` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_DeclarationID`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, ReceivedEmailValidationService.cs. Scripts: seed-qa-escenarios-tulas.py, seed-recounting-stress.py, seed-tracker-stress-fix.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtbldeclaredchecks_Declaration_sanic_chtbldeclaration` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_Declaration`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, ReceivedEmailValidationService.cs. Scripts: backfill-tracker.py, create-tracker-columns.sh, seed-qa-escenarios-tulas.py, seed-recounting-stress.py (+2 más). | | Sin revisar | |
| `sanic_chtbldeclaredchecks_DeclaredBagMirror_sanic_chtbldeclaredbags` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_DeclaredBagMirror`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclaredCheckBagMirrorPlugin.cs, RecountingMirrors.cs, RecountingMirrorsTests.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh, seed-qa-escenarios-tulas.py. | | Sin revisar | |
| `sanic_chtbldeclaredchecks_DeclaredDepositID_sanic_chtbldeclareddeposits` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblDeclaredDeposits` | 1:N (lookup `sanic_DeclaredDepositID`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, DeclaredCheckBagMirrorPlugin.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, backfill-tracker.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh (+4 más). | | Sin revisar | |
| `sanic_chtbldeclaredchecks_MatchedCheck_sanic_chtblchecks` | `sanic_CHTblDeclaredChecks` | `sanic_CHTblChecks` | 1:N (lookup `sanic_MatchedCheck`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: CheckMirrorStampPlugin.cs, CheckMirrors.cs, Schema.cs. Scripts: backfill-tracker.py, create-tracker-columns.sh, seed-recounting-stress.py. | | Sin revisar | |
| `sanic_chtbldeclareddeposits_Declaration_sanic_chtbldeclaration` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDeclaration` | 1:N (lookup `sanic_Declaration`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, ReceivedEmailValidationService.cs. Scripts: backfill-tracker.py, create-tracker-columns.sh, seed-qa-escenarios-tulas.py, seed-recounting-stress.py (+2 más). | | Sin revisar | |
| `sanic_chtbldeclareddeposits_DeclaredBag_sanic_chtbldeclaredbags` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDeclaredBags` | 1:N (lookup `sanic_DeclaredBag`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: DeclarationMaterializer.cs, DeclaredCheckBagMirrorPlugin.cs, RecountingMirrors.cs, RecountingMirrorsTests.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, backfill-tracker.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh (+5 más). | | Sin revisar | |
| `sanic_chtbldeclareddeposits_RelatedRecountedDeposit_sanic_chtbldepositsrecounting` | `sanic_CHTblDeclaredDeposits` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedRecountedDeposit`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: GuardSupervisorAccountingPlugin.cs, Schema.cs. Scripts: seed-recounting-stress.py, seed-tracker-stress-fix.py. | | Sin revisar | |
| `sanic_chtbldepositsrecounting_BagID_sanic_chtblbags` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagID`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagRecountClosurePlugin.cs, CheckMirrorStampPlugin.cs, DepositSessionMirrorPlugin.cs, GuardRecountingPlugin.cs, RecountClosureAccounting.cs (+1 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh, register-reception-guard.sh (+8 más). | | Sin revisar | |
| `sanic_chtbldepositsrecounting_RecountingSessionMirror_sanic_chtblrecountingsession` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSessionMirror`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: DepositSessionMirrorPlugin.cs, RecountingGuard.cs, RecountingMirrors.cs, RecountingMirrorsTests.cs, Schema.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, reasignar-sesion-recuento.py, register-recounting-guard.sh (+1 más). | | Sin revisar | |
| `sanic_chtbldepositsrecounting_RelatedDeclaredDeposit_sanic_chtbldeclareddeposits` | `sanic_CHTblDepositsRecounting` | `sanic_CHTblDeclaredDeposits` | 1:N (lookup `sanic_RelatedDeclaredDeposit`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: create-alternate-keys.sh, seed-recounting-stress.py, seed-tracker-stress-fix.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblemailsattachments_Email_sanic_chtblreceivedemails` | `sanic_CHTblEmailsAttachments` | `sanic_CHTblReceivedEmails` | 1:N (lookup `sanic_Email`) | | Apps: CH-App-DepositsTracker, CH-App-IngestionConsole. Flows: CH-Flow-MailboxSweep, CH-Flow-ProcessEmail, CH-Flow-Replytoclientemail, CH-Flow-UnblockOnClient, CH-Flow-UnblockOnClientEmail. Plugins: Schema.cs. Scripts: probe-parser-limits.py, seed-bandeja-demo.py, seed-tracker-stress-fix.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblreceivedemails_Client_sanic_chtblclient` | `sanic_CHTblReceivedEmails` | `sanic_CHTblClient` | 1:N (lookup `sanic_Client`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Supervisor. Plugins: BusinessRules.cs, ClientBranchMirrorSchema.cs, ClientBranchNameMirrorPlugin.cs, ClientBranchNameMirrors.cs, DeclarationMaterializer.cs (+6 más). Scripts: backfill-clientbranch-mirror.py, create-alternate-keys.sh, create-clientbranch-mirror-column.sh, fix-clientaccount-alternate-key.sh (+8 más). | | Sin revisar | |
| `sanic_chtblreceptionbatch_Location_sanic_chtbllocations` | `sanic_CHTblReceptionBatch` | `sanic_CHTblLocations` | 1:N (lookup `sanic_Location`) | | SOLO-METADATA — la lookup `sanic_Location` solo aparece en el cache de esquema (DataSources/TableDefinitions) de los canvas apps; ningún plugin, flow o script la lee/escribe. | | Sin revisar | |
| `sanic_chtblreceptions_BranchName_sanic_chtblclientbranch` | `sanic_CHTblReceptions` | `sanic_CHTblClientBranch` | 1:N (lookup `sanic_BranchName`) | | SOLO-METADATA — la lookup `sanic_BranchName` solo aparece en el cache de esquema (DataSources/TableDefinitions) de los canvas apps; ningún plugin, flow o script la lee/escribe. | | Sin revisar | |
| `sanic_chtblreceptions_ReceptionBatch_sanic_chtblreceptionbatch` | `sanic_CHTblReceptions` | `sanic_CHTblReceptionBatch` | 1:N (lookup `sanic_ReceptionBatch`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagReceptionMirrorPlugin.cs, BagReceptionMirrors.cs, ReceptionBatchStatusMirrorPlugin.cs, ReceptionBatchTotalsPlugin.cs, Schema.cs (+1 más). Scripts: backfill-supervisor-mirrors.py, create-supervisor-mirror-columns.sh, register-reception-guard.sh, register-reception-mirror.sh (+5 más). | | Sin revisar | |
| `sanic_chtblreceptions_RemitentClient_sanic_chtblclient` | `sanic_CHTblReceptions` | `sanic_CHTblClient` | 1:N (lookup `sanic_RemitentClient`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Scripts: seed-reception-stress.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblrecountingsession_Cashier_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_Cashier`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: GuardReceptionPlugin.cs, ReceptionGuard.cs, RecountingGuard.cs, Schema.cs. Scripts: register-recounting-guard.sh, seed-digitization-stress.py, seed-recounting-stress.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblrecountingsession_ConfirmedBy_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_ConfirmedBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: seed-recounting-stress.py. | | Sin revisar | |
| `sanic_chtblrecountingsession_CountingDesk_sanic_chtblcountingdesk` | `sanic_CHTblRecountingSession` | `sanic_CHTblCountingDesk` | 1:N (lookup `sanic_CountingDesk`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: seed-recounting-stress.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblrecountingsession_ForcedClosureBy_systemuser` | `sanic_CHTblRecountingSession` | `SystemUser` | 1:N (lookup `sanic_ForcedClosureBy`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_BagId_sanic_chtblbags` | `sanic_CHTblRecountingSessionDetail` | `sanic_CHTblBags` | 1:N (lookup `sanic_BagId`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BagRecountClosurePlugin.cs, CheckMirrorStampPlugin.cs, DepositSessionMirrorPlugin.cs, GuardRecountingPlugin.cs, RecountClosureAccounting.cs (+1 más). Scripts: backfill-recounting-mirrors.py, backfill-supervisor-mirrors.py, create-recounting-mirror-columns.sh, register-reception-guard.sh (+8 más). | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_RecountingSessionId_sanic_chtblrecountingsession` | `sanic_CHTblRecountingSessionDetail` | `sanic_CHTblRecountingSession` | 1:N (lookup `sanic_RecountingSessionId`) | | Apps: CH-App-DepositsTracker, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. Scripts: reasignar-sesion-recuento.py, seed-recounting-stress.py. | | Sin revisar | |
| `sanic_chtblrecountingsessiondetail_SupervisorId_systemuser` | `sanic_CHTblRecountingSessionDetail` | `SystemUser` | 1:N (lookup `sanic_SupervisorId`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: Schema.cs. | | Sin revisar | |
| `sanic_chtbltransactions_RelatedDepositRecounting_sanic_chtbldepositsrecounting` | `sanic_CHTblTransactions` | `sanic_CHTblDepositsRecounting` | 1:N (lookup `sanic_RelatedDepositRecounting`) | | Apps: CH-App-DepositsTracker, CH-App-Digitization, CH-App-Recounting, CH-App-Supervisor. Plugins: AccountingGuard.cs, GuardRecountingPlugin.cs, Schema.cs. Scripts: create-alternate-keys.sh, register-recounting-guard.sh, register-transaction-routing.sh, seed-digitization-stress.py (+3 más). | | Sin revisar | |
| `sanic_chtbltransactions_TransactionBatch_sanic_transactionbatches` | `sanic_CHTblTransactions` | `sanic_TransactionBatches` | 1:N (lookup `sanic_TransactionBatch`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-DownloadData. Plugins: BatchTransitionStampPlugin.cs, Schema.cs, TransactionBatchStatusMirrorPlugin.cs, TransactionBatchTotalsPlugin.cs, TransactionGuard.cs. Scripts: provision-lote-devuelto.sh, provision-tracker-role.sh, register-batch-stamp.sh, register-transaction-guard.sh (+5 más). | | Sin revisar | |
| `sanic_chtblvalidationresults_ValidationRule_sanic_validationrules` | `sanic_CHTblValidationResults` | `sanic_ValidationRules` | 1:N (lookup `sanic_ValidationRule`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-IngestionConsole, CH-App-Reception, CH-App-Recounting, +1 más. Plugins: RuleDefinition.cs, Schema.cs. Scripts: provision-config-app.py, provision-email-ingestion-roles.sh, provision-tracker-role.sh, seed-bandeja-demo.py (+1 más). | | Sin revisar | |
| `sanic_chtblvalidationresults_ValidationRun_sanic_chtblvalidationruns` | `sanic_CHTblValidationResults` | `sanic_CHTblValidationRuns` | 1:N (lookup `sanic_ValidationRun`) | | Apps: CH-App-DepositsTracker, CH-App-IngestionConsole. Plugins: Schema.cs. Scripts: seed-bandeja-demo.py, seed-tracker-stress.py. | | Sin revisar | |
| `sanic_chtblxmlreading_RecountingSessionDetailId_sanic_chtblrecountingsessiondetail` | `sanic_CHTblXMLReading` | `sanic_CHTblRecountingSessionDetail` | 1:N (lookup `sanic_RecountingSessionDetailId`) | | Apps: CH-App-Recounting, CH-App-Supervisor. Flows: CH-Flow-ProcessXMLFiles. Plugins: Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: register-recounting-mirrors.sh, seed-recounting-stress.py. | | Sin revisar | |
| `sanic_chtblxmlreadingdetail_RecountingSessionDetailMirror_sanic_chtblrecountingsessiondetail` | `sanic_CHTblXMLReadingDetail` | `sanic_CHTblRecountingSessionDetail` | 1:N (lookup `sanic_RecountingSessionDetailMirror`) | | Apps: CH-App-Recounting, CH-App-Supervisor. Plugins: RecountingMirrors.cs, RecountingMirrorsTests.cs, Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh. | | Sin revisar | |
| `sanic_chtblxmlreadingdetail_XMLReading_sanic_chtblxmlreading` | `sanic_CHTblXMLReadingDetail` | `sanic_CHTblXMLReading` | 1:N (lookup `sanic_XMLReading`) | | Apps: CH-App-Recounting. Flows: CH-Flow-ProcessXMLFiles. Plugins: RecountingMirrors.cs, RecountingMirrorsTests.cs, Schema.cs, XmlReadingDetailMirrorPlugin.cs. Scripts: backfill-recounting-mirrors.py, create-recounting-mirror-columns.sh, register-recounting-mirrors.sh, seed-recounting-stress.py. | | Sin revisar | |
| `sanic_systemuser_transactionbatches_devueltopor` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_DevueltoPor`) | | Apps: CH-App-Digitization. Plugins: BatchTransitionStampPlugin.cs, Schema.cs. Scripts: provision-lote-devuelto.sh. | | Sin revisar | **NUEVA en este export** (no estaba en el inventario 2026-08-24). |
| `sanic_transaction_cajero` | `sanic_CHTblTransactions` | `SystemUser` | 1:N (lookup `sanic_Cajero`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CHWorkloadByCashierPlugin.cs, CreateTransactionCutPlugin.cs, RecountingGuard.cs, Schema.cs. Scripts: backfill-cajero-en-transacciones.py, provision-config-app.py, register-transaction-cut.sh, seed-digitization-stress.py (+1 más). | | Sin revisar | **NUEVA en este export** (no estaba en el inventario 2026-08-24). |
| `sanic_transaction_supervisorautoriza` | `sanic_CHTblTransactions` | `SystemUser` | 1:N (lookup `sanic_SupervisorAutoriza`) | | Apps: CH-App-Digitization. Plugins: Schema.cs. Scripts: backfill-cajero-en-transacciones.py. | | Sin revisar | **NUEVA en este export** (no estaba en el inventario 2026-08-24). |
| `sanic_transactionbatches_sanic_AplicadoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_AplicadoPor`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BatchTransitionStampPlugin.cs, Schema.cs. Scripts: register-batch-stamp.sh. | | Sin revisar | |
| `sanic_transactionbatches_sanic_Cajero_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_Cajero`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CHWorkloadByCashierPlugin.cs, CreateTransactionCutPlugin.cs, RecountingGuard.cs, Schema.cs. Scripts: backfill-cajero-en-transacciones.py, provision-config-app.py, register-transaction-cut.sh, seed-digitization-stress.py (+1 más). | | Sin revisar | |
| `sanic_transactionbatches_sanic_GeneradoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_GeneradoPor`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: CreateTransactionCutPlugin.cs, Schema.cs. Scripts: register-transaction-cut.sh, seed-digitization-stress.py. | | Sin revisar | |
| `sanic_transactionbatches_sanic_IngresadoPor_systemuser` | `sanic_TransactionBatches` | `SystemUser` | 1:N (lookup `sanic_IngresadoPor`) | | Apps: CH-App-ClientAdministration, CH-App-DepositsTracker, CH-App-Digitization, CH-App-Reception, CH-App-Recounting, CH-App-Supervisor. Plugins: BatchReturnedGuardTests.cs, BatchTransitionStampPlugin.cs, GuardTransactionPlugin.cs, Schema.cs, TransactionGuard.cs (+1 más). Scripts: register-batch-stamp.sh, register-transaction-guard.sh. | | Sin revisar | |
| `sanic_validationrules_ValidationDependency_sanic_validationrules` | `sanic_ValidationRules` | `sanic_ValidationRules` | 1:N (lookup `sanic_ValidationDependency`) | | Apps: CH-App-DepositsTracker, CH-App-IngestionConsole. Plugins: Schema.cs. Scripts: provision-config-app.py. | | Sin revisar | |

No se encontraron relaciones N:N (ManyToMany) en `solutions/unpacked/core/Other/Relationships/`.

**Nota de alcance**: se listan solo las relaciones de NEGOCIO (lookup custom entre tablas o hacia SystemUser vía columna `sanic_*`). Se excluyen de esta tabla las relaciones de plomería estándar de Dataverse que existen automáticamente para cada tabla custom (no son decisiones de diseño a revisar):
- **41 relaciones `owner_*`** (OwnerId, propietario de la fila — Owner/Team/SystemUser polimórfico), una por cada una de las 41 tablas `sanic_*` (no aplica a Account/SystemUser, que no son propiedad de esta solución).
- **4 relaciones `FileAttachment_*`** (columnas de tipo *File* — almacenamiento binario nativo de Dataverse, no un lookup de negocio): FileAttachment_sanic_CHTblEmailsAttachments_sanic_FileContent, FileAttachment_sanic_CHTblEmailsAttachments_sanic_JSONData, FileAttachment_sanic_CHTblReceivedEmails_sanic_Attachment, FileAttachment_sanic_TransactionBatches_sanic_CsvCore.
- Las relaciones estándar de {BusinessUnit, Team, SystemUser (createdby/modifiedby)} por cada tabla (`business_unit_*`, `team_*`, `systemuser_*` con atributo `CreatedBy`/`ModifiedBy`/`OwningTeam`/`OwningUser`), presentes también en `solutions/unpacked/core/Other/Relationships/BusinessUnit.xml`, `Team.xml`, `SystemUser.xml`, `Owner.xml`.

Total de relaciones en el export: 321 (antes 318). De negocio (listadas arriba): 69 (antes 66; +3 nuevas). Boilerplate excluida: 252 (sin cambios).

## 4. Option Sets / Choices globales

> Fuente: `solutions/unpacked/core/OptionSets/*.xml` (los 3 únicos option sets **globales** de la solución; prefijo de valor `159460xxx` = `CustomizationOptionValuePrefix=15946` de `Other/Solution.xml`, verificado sin cambios). Hay además ~20 option sets **locales** de una sola tabla (ver `docs/referencia/catalogo-de-choices.md` §2-3) que no son componentes reutilizables independientes — su estado de revisión va atado al campo que los contiene, en la sección 2. **Sin cambios respecto al inventario 2026-08-24**: mismos 3 option sets, mismos valores/etiquetas, mismo prefijo.

| Nombre | Valores (etiqueta=número) | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|
| `sanic_chchcurrency` (CH-Ch-Currency) | NIO=159460000, USD=159460001, EUR=159460002 | | Usado en los campos `sanic_currency`/`sanic_moneda`/`sanic_txcurrency` de `CH-Tbl-DeclaredChecks`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-Checks`, `CH-Tbl-MoneyDenomination`, `CH-Tbl-DepositsRecounting`, `CH-Tbl-Transactions` (Entity.xml). Plugins: `src/backend/CashHub.Plugins/Schema.cs` (constantes `CurrencyNio=159460000`, `CurrencyUsd=159460001`, `CurrencyEur=159460002`), `AccountingGuard.cs`, `RecountingGuard.cs`. App: canvas Fx.yaml de CH-App-Supervisor. Scripts: seed-digitization-stress.py, seed-recounting-stress.py. | | Sin revisar | Usado en DeclaredChecks, DeclaredDeposits, Checks, MoneyDenomination, DepositsRecounting, Transactions (según catalogo-de-choices.md) — confirmado contra Entity.xml y Schema.cs. |
| `sanic_chchrecordstatus` (CH-Ch-RecordStatus) | Active=159460000, Inactive=159460001, PendingApproval=159460002, PendingDeactivation=159460003 | | Usado en Client, ClientBranch, ClientEmail, ClientDepositHistory, ClientAccount (`sanic_recordstatus`, Entity.xml). Plugins: `src/backend/CashHub.Validation/Models/RecordVigencyMap.cs`, `Schema.cs`. Flows `Activaterow`/`Deactivaterow` lo manipulan (patch de clientdata + toggle statecode). Scripts: seed-clientadmin-stress.py, seed-reception-stress.py. | | Sin revisar | Usado en Client, ClientBranch, ClientEmail, ClientDepositHistory (`sanic_recordstatus`). `Activaterow`/`Deactivaterow` (flows) lo manipulan — confirmado. |
| `sanic_chrecountingdetailsstatus` (ch-recountingDetailsStatus) | En progreso=159460000, Cerrado=159460001, Cerrado con diferencias=159460002 | | Usado solo en `RecountingSessionDetail.sanic_recountingsessiondetailstatus` (Entity.xml). Plugin `src/backend/CashHub.Plugins/ResolveApprovalRequestPlugin.cs` setea `OptionSetValue(nCerrado)`/`OptionSetValue(nCerradoConDiferencias)`; constante en `Schema.cs`. Apps: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml`, `.../scrTulas.fx.yaml`, `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrResumen.fx.yaml`. | | Sin revisar | Usado solo en `RecountingSessionDetail.sanic_recountingsessiondetailstatus` — confirmado, además ahora con evidencia de Fx.yaml y plugin concretos. |

## 5. Variables de entorno

> Fuente: `solutions/unpacked/core/environmentvariabledefinitions/<nombre>/environmentvariabledefinition.xml`. **23 variables** en el repo actual (sin cambio de conteo respecto al 2026-08-24), todas `iscustomizable=1`, `isrequired=0`, `secretstore=0` (ninguna en Azure Key Vault). Tipo `100000000` = String, `100000001` = Number. **`sanic_CHEVEnvironmentURL` ya no aparece como `??` (untracked)** — quedó comprometida al repo en el commit `f542a6e`.
>
> **Migración en curso a `CH-Tbl-Parameter`** (ver `docs/desarrollo/09-app-configuracion.md` §9, doc nuevo del 2026-08-31): el 2026-08-31 se migraron los dos únicos consumidores reales que tenía esta sección (`sanic_CHEVBancaMailbox` y `sanic_CHEVAppUrl_ClientAdmin`, leídos por `CH-App-IngestionConsole` y por los flows `CH-Flow-Notifyreviewers`/`CH-Flow-NotifywhenRFCimplementationfinish`) hacia la tabla `CH-Tbl-Parameter` (`Email.Banca.Mailbox` y `Apps.ClientAdministration.Url`). Las dos env vars **siguen existiendo** en `CashHubCore` — no se borraron, solo quedaron sin consumidores — porque tocar `CashHubCore` para eliminarlas está fuera de alcance de ese cambio.

| Schema name | Display name | Tipo | Valor default | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `sanic_CHEVAADGroup_Admin` | CH-EV-AADGroup_Admin | String | — (sin default) | | | | Sin revisar | Sin referencia textual confirmada de consumo (docs/referencia/variables-de-entorno.md). Re-verificado 2026-08-31: sigue sin aparecer en ningún `.cs`, flow `.json`, canvas `.fx.yaml` ni script de `scripts/`. |
| `sanic_CHEVAADGroup_ClientAdmin` | CH-EV-AADGroup_ClientAdmin | String | — | | | | Sin revisar | Ídem — sin referencia textual confirmada. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAADGroup_Counting` | CH-EV-AADGroup_Counting | String | — | | | | Sin revisar | Ídem. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAADGroup_EmailValidation` | CH-EV-AADGroup_EmailValidation | String | — | | | | Sin revisar | Ídem. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAADGroup_Reception` | CH-EV-AADGroup_Reception | String | — | | | | Sin revisar | Ídem. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAADGroup_Supervisor` | CH-EV-AADGroup_Supervisor | String | — | | | | Sin revisar | Ídem. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAppUrl_ClientAdmin` | CH-EV-AppUrl_ClientAdmin | String | — | | Solo en un artefacto generado y no autoritativo: `solutions/unpacked/apps/CanvasApps/src/sanic_chappemailvalidationconsole_f3b93/Src/EditorState/screen ColaDeProcesamiento.editorstate.json` (caché de EditorState — no es la fórmula que corre). | | Sin revisar | **MIGRADA a `CH-Tbl-Parameter` el 2026-08-31** (`docs/desarrollo/09-app-configuracion.md` §9): el `.fx.yaml` real de `CH-App-IngestionConsole` (`Src/screen ColaDeProcesamiento.fx.yaml:711`, y su copia en `patch/`) ya usa `LookUp('CH-Tbl-Parameters', Key = "Apps.ClientAdministration.Url" ...)` en vez de `LookUp('Environment Variable Values', 'Schema Name' = "sanic_CHEVAppUrl_ClientAdmin", ...)`. El doc nuevo aclara que esta env var **nunca tuvo valor asignado en el ambiente**, así que el link de "Abrir Client Administration" en Teams/correo nunca se renderizó incluso antes de la migración. **No se eliminó** (vive en `CashHubCore`, fuera de alcance tocarla). |
| `sanic_CHEVAppUrl_Counting` | CH-EV-AppUrl_Counting | String | — | | | | Sin revisar | Sin consumo encontrado en los flujos analizados. Re-verificado 2026-08-31, sin coincidencias en `.cs`/`.json`/`.fx.yaml`/`scripts/`. |
| `sanic_CHEVAppUrl_EmailValidation` | CH-EV-AppUrl_EmailValidation | String | — | | | | Sin revisar | Sin consumo encontrado. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAppUrl_Reception` | CH-EV-AppUrl_Reception | String | — | | | | Sin revisar | Sin consumo encontrado. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVAppUrl_Supervisor` | CH-EV-AppUrl_Supervisor | String | — | | | | Sin revisar | Sin consumo encontrado. Re-verificado 2026-08-31, mismo resultado. |
| `sanic_CHEVBancaMailbox` | CH-EV-BancaMailbox | String | `cashhub@55xljh.onmicrosoft.com` | | Solo en un artefacto generado y no autoritativo: `solutions/unpacked/apps/CanvasApps/src/sanic_chappemailvalidationconsole_f3b93/Src/EditorState/screen ColaDeProcesamiento.editorstate.json` (caché de EditorState — no es la fórmula que corre). | | Sin revisar | **MIGRADA a `CH-Tbl-Parameter` el 2026-08-31** (`docs/desarrollo/09-app-configuracion.md` §9): el `.fx.yaml` real (`Src/screen ColaDeProcesamiento.fx.yaml:710`, y copia en `patch/`) usa `LookUp('CH-Tbl-Parameters', Key = "Email.Banca.Mailbox" ...)`. El seed de la tabla de parámetros vive en `scripts/provision-parameters-table.py:296` ("Migrado desde la environment variable sanic_CHEVBancaMailbox"). **No se eliminó** (vive en `CashHubCore`). |
| `sanic_CHEVCsvExportLibraryUrl` | CH-EV-CsvExportLibraryUrl | String | — | | | | Sin revisar | Sin consumo encontrado en flujos ni apps. Re-verificado 2026-08-31, sin coincidencias en `.cs`/`.json`/`.fx.yaml`. |
| `sanic_CHEVEmailSubjectToAccept` | CH-EV-EmailSubjectToAccept | String | — | | | | Sin revisar | No consumida — VR03 recibe el asunto como parámetro de entrada del flujo, no lee esta variable. Re-verificado 2026-08-31, sin coincidencias en `src/backend/CashHub.Plugins/*.cs`. |
| `sanic_CHEVEnvironmentURL` | CH-EV-EnvironmentURL | String | — | | | | Sin revisar | Introducida en versión 1.14.0.0. **Ya NO aparece como `??` (untracked)**: quedó comprometida al repo en el commit `f542a6e` ("fix(core): lookup de ValidationRun por logical name..."). Sigue sin ninguna referencia de consumo fuera de su propia definición (`rg` sobre todo el repo solo la encuentra en `environmentvariabledefinition.xml` y en este inventario). No documentada en `docs/referencia/variables-de-entorno.md` (discrepancia sin resolver). |
| `sanic_CHEVFileSizeMaxMB` | CH-EV-FileSizeMaxMB | Number | `5` | | | | Sin revisar | VR05 recibe el máximo como parámetro de entrada; no se confirmó lectura directa de esta variable en `VR05.json`. Re-verificado 2026-08-31: sin coincidencias en `src/backend/CashHub.Plugins/*.cs`. |
| `sanic_CHEVIbsBranchCodeList` | CH-EV-IbsBranchCodeList | String | — | | | | Sin revisar | **No explotada**: Digitization usa `TRNCODE=8004` fijo y campos de identidad de agencia hardcodeados en vez de resolver contra esta lista. Re-verificado 2026-08-31, sin coincidencias en `.cs`/`.json`/`.fx.yaml`. |
| `sanic_CHEVOpsTeamsGroupId` | CH-EV-OpsTeamsGroupId | String | — | | `solutions/unpacked/flow/Workflows/CH-Flow-MailboxSweep-FB8E0A66-F4FE-4825-9D0C-78FB16271516.json` (líneas 51, 332, 369 — `@parameters('CH-EV-OpsTeamsGroupId (sanic_CHEVOpsTeamsGroupId)')`, notificación de Teams al equipo de operaciones ante fallas del barrido). | | Sin revisar | "Group chat de Teams del equipo de operaciones para alertas del barrido de ingesta. Vacío = sin alertas de Teams." Introducida en 1.4.1.0. Consumo confirmado por evidencia directa en el JSON del flow (no solo por nombre) — actualiza la nota anterior que decía "no documentada / sin confirmar". |
| `sanic_CHEVProcessingLibraryUrl` | CH-EV-ProcessingLibraryUrl | String | — | | | | Sin revisar | Sin consumo encontrado en flujos ni apps. Re-verificado 2026-08-31, sin coincidencias. |
| `sanic_CHEVRfcApplyTimeoutMinutes` | CH-EV-RfcApplyTimeoutMinutes | Number | `30` | | `solutions/unpacked/flow/Workflows/CH-Flow-RFCWatchdog-DE989841-51A1-45C2-820D-7A215E005357.json:49` — `ListRecords` crudo sobre `environmentvariablevalues` con `$filter: "schemaname eq 'sanic_CHEVRfcApplyTimeoutMinutes'"`. | | Sin revisar | "Minutos máximos que un RFC puede permanecer en Approved sin pasar a Applied/Failed antes de que el watchdog lo marque Failed." **Confirmado 2026-08-31** por lectura directa del JSON (ya no es solo "el nombre lo indica"): el flow lo lee vía `ListRecords`/`environmentvariablevalues`, igual patrón que `sanic_CHEVAppUrl_ClientAdmin` antes de su migración. |
| `sanic_CHEVSharedMailboxAddress` | CH-EV-SharedMailboxAddress | String | `no-reply-powerplatform-dev@baccredomatic.ni` | | `solutions/unpacked/flow/Workflows/CH-Flow-MailboxSweep-*.json`, `CH-Flow-IngestEmails-*.json`, `CH-Flow-ProcessEmail-*.json`, `CH-Flow-Replytoclientemail-*.json` — las 4 usan `@parameters('...sanic_CHEVSharedMailboxAddress...')` para `mailboxAddress`/`toOrCc`. | | Sin revisar | Default de **ambiente dev** literal. La nota vieja decía "No consumida por IngestEmails: el buzón está hardcodeado" — **corregido 2026-08-31**: sí está referenciada como parámetro en los 4 flows de correo (`mailboxAddress`), incluido `CH-Flow-IngestEmails`. Revisar en vivo si el valor efectivo del parámetro coincide con el hardcode que motivó la nota original. |
| `sanic_CHEVSharedMailboxArchiveFolderID` | CH-EV-SharedMailboxArchiveFolderID | String | `AAMkADgz...` (ID carpeta Outlook) | | | | Sin revisar | **No consumida donde se esperaría**: Email Validation Console tiene `varIDFolderArchivado` hardcodeado en Power Fx. Re-verificado 2026-08-31, sin coincidencias en `.cs`/`.json`/`.fx.yaml`/`scripts/`. |
| `sanic_CHEVTimezone` | CH-EV-Timezone | String | `America/Managua` | | | | Sin revisar | Sin lectura explícita confirmada vía "Get environment variable"; `ExtractedTablesFromExcel` usa el literal `Central America Standard Time` en su lugar. Re-verificado 2026-08-31, sin coincidencias en `.cs`/`.json`/`.fx.yaml`/`scripts/`. |

## 6. Roles de seguridad

> Fuente: `solutions/unpacked/core/Roles/*.xml` — **13 roles** en el export actual (10 en el snapshot del 2026-08-24 + 3 nuevos: `CH-SR-ConfigAdmin`, `CH-SR-EmailIngestionFlow`, `CH-SR-EmailOperationsConsole`). El `name=` de cada XML es el nombre vigente en el ambiente.
>
> **Rename confirmado**: `CH-SR-Counter` → `CH-SR-Cashier`, mismo `roleid` `{f9bee50d-6a75-f011-b4cb-7c1e5248985f}` (verificado leyendo `<Role id="{f9bee50d-6a75-f011-b4cb-7c1e5248985f}" name="CH-SR-Cashier" ...>` en `solutions/unpacked/core/Roles/CH-SR-Cashier.xml`; el archivo `CH-SR-Counter.xml` ya no existe en el export). El nombre viejo sigue vivo por texto en varios lugares que **no fueron actualizados** tras el rename en el ambiente: `docs/referencia/roles-y-seguridad.md` (todo el documento, incluida la matriz Rol×Entidad), `docs/soluciones/01-core.md:139`, y el propio comentario de `scripts/grant-cashier-declared-write.sh` ("'CH-SR-Cashier' y en solutions/unpacked/core/Roles/ como 'CH-SR-Counter.xml'"). Los scripts operativos (`scripts/grant-cashier-account-read.sh`, `scripts/grant-cashier-declared-write.sh`) ya resuelven por **GUID**, no por nombre, así que el rename no les rompe nada — el propio comentario de `grant-cashier-account-read.sh` lo dice explícito: *"CH-SR-Cashier (por ID: el nombre difiere entre entorno y repo)"*.
>
> **3 roles nuevos, no solo 1**: el contexto de esta tarea adelantaba únicamente `CH-SR-ConfigAdmin` como novedad — verificado, existe, pero además aparecen `CH-SR-EmailIngestionFlow` y `CH-SR-EmailOperationsConsole` (`scripts/provision-email-ingestion-roles.sh` los crea a los dos juntos). Los 3 son roles genuinamente nuevos, sin equivalente previo (no son renames).

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `CH-Role` | | Único privilegio custom: `prvReadsanic_CHTblAppCatalog` (Global) — confirmado en `docs/soluciones/01-core.md:34,134`. Es el rol base que permite abrir el Hub (Read de `CH-Tbl-AppCatalog`). Referenciado en `scripts/apply-supervisor-role-remediation.sh` y `scripts/provision-appcatalog-access.sh`. | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1 | Sin revisar | Rol base/plantilla (sin sufijo `SR-`) — confirmar en vivo su propósito exacto respecto a los demás `CH-SR-*`. |
| `CH-SR-AnalyticsRead` | | **Cero privilegios sobre tablas custom** — confirmado por `docs/referencia/roles-y-seguridad.md:13` y `docs/soluciones/01-core.md:139` ("byte a byte idéntico a OpsAdmin, solo privilegios de plataforma"). Aparece en `scripts/apply-supervisor-role-remediation.sh` y `scripts/provision-tracker-role.sh`/`provision-appcatalog-access.sh`, pero solo en listas de roles a tocar, no otorga acceso real a ninguna tabla. | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1-2 | Sin revisar | Un rol llamado "lectura de analítica" que hoy, tal como está exportado, no puede leer ninguna tabla custom (0 de 40 tablas `sanic_*`) — no es funcional para su nombre. |
| `CH-SR-Cashier` | | Cajero de recuento (`CH-App-Recounting`). 65 privilegios (`docs/referencia/roles-y-seguridad.md:14`, con el nombre viejo `CH-SR-Counter`): CRUD completo sobre `Bags`/`Checks`/`CountedMoneyDetails`/`DepositsRecounting`/`RecountingSession`/`Transactions`/`ApprovalRequest`. Referenciado por GUID en `scripts/grant-cashier-account-read.sh` y `scripts/grant-cashier-declared-write.sh` (cierre de huecos de privilegios post-rename). | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1-2 | Sin revisar | **RENOMBRADO** desde `CH-SR-Counter` (mismo `roleid` `{f9bee50d-6a75-f011-b4cb-7c1e5248985f}`). El nombre viejo `CH-SR-Counter` está **AUSENTE EN EL EXPORT ACTUAL** — el archivo `CH-SR-Counter.xml` ya no existe; quedó reemplazado por `CH-SR-Cashier.xml` con idéntico GUID. Documentación desactualizada que todavía dice "Counter": `docs/referencia/roles-y-seguridad.md`, `docs/soluciones/01-core.md:139`. |
| `CH-SR-ClientAdminApprover` | Aprobar/rechazar RFC de Client Administration — Write Global sobre cabecera y línea de RFC, Read Global sobre los 4 maestros (Client/ClientBranch/ClientEmail/ClientAccount). | Canvas app `CH-App-ClientAdministration v2`, validado server-side por `GuardChangeRequestApprovalPlugin`/`GuardChangeRequestLinePlugin` (steps `{25b5a32c-...}`/`{28b5a32c-...}`/`{23b5a32c-...}`) y por `sanic_CHResolveChangeRequest` (`ResolveChangeRequestPlugin`, `scripts/register-resolve-changerequest.sh`). Asignado por `scripts/assign-clientadmin-team-roles.sh`. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §3 | Sin revisar | Read Global sobre `CH-Tbl-ClientAccount` agregado 2026-08-25 (GAP S2, commit `4d527f8`) — confirmado presente en el XML actual. |
| `CH-SR-ClientAdminContributor` | Crear/editar RFC propios de Client Administration — Create/Write Basic sobre cabecera y línea de RFC, Read Global sobre los 4 maestros. | Canvas app `CH-App-ClientAdministration v2`. Asignado por `scripts/assign-clientadmin-team-roles.sh`. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §3 | Sin revisar | Read Global sobre `CH-Tbl-ClientAccount` agregado 2026-08-25 (GAP S2, commit `4d527f8`) — confirmado presente en el XML actual. |
| `CH-SR-ConfigAdmin` | | App model-driven `CH-App-Configuration` (`sanic_CHAppConfiguration`, solución `CashHub_Patch_2`). Asociación confirmada en `solutions/unpacked/apps/AppModules/sanic_CHAppConfiguration/AppModule.xml` (`<Role id="{227a473d-0ba5-f111-aaac-000d3a3425f0}" />`) y en `solutions/unpacked/apps/Other/Solution.xml` (`<Required type="20" displayName="CH-SR-ConfigAdmin" ... id="{227a473d-...}" />`). Creado/gestionado por `scripts/provision-config-app.py`. CRUD Global sobre 11 tablas de configuración asegurables + privilegios de plataforma (98 privilegios totales, según `docs/desarrollo/09-app-configuracion.md` §5). | [`docs/desarrollo/09-app-configuracion.md`](../desarrollo/09-app-configuracion.md) §5 | Sin revisar | **NUEVO desde el export del 2026-08-24**. `roleid` `{227a473d-0ba5-f111-aaac-000d3a3425f0}`. Documentado en detalle en `docs/desarrollo/09-app-configuracion.md` (archivo nuevo, sin trackear en git al momento de este inventario). **No otorga nada sobre `CH-Tbl-Parameter`**: esa tabla nació `OwnershipType=None` y Dataverse no genera privilegios para ella (ver §8, nota de `CH-Tbl-Parameter`) — "Falta a mano": asignar el rol a usuarios/team todavía no se hizo. |
| `CH-SR-CountingSupervisor` | | Supervisor de conteo (`CH-App-Supervisor`). Lectura amplia (mismas 17 tablas que Cashier); Create limitado a `Bags`/`Checks`/`Transactions`; Assign sobre `ApprovalRequest` (`docs/referencia/roles-y-seguridad.md:15`). Referenciado en `scripts/register-reception-guard.sh`, `scripts/apply-supervisor-role-remediation.sh`. | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1-2 | Sin revisar | |
| `CH-SR-DepositsTracker` | | Rol del proceso de tracking de depósitos (`CH-App-DepositsTracker`). Provisto/gestionado por `scripts/provision-tracker-role.sh`. | [`docs/procesos/07-tracking-depositos.md`](../procesos/07-tracking-depositos.md) | Sin revisar | |
| `CH-SR-Digitizer` | | Digitador (`CH-App-Digitization`) — tiene el privilegio `prvCreatesanic_TransactionBatches` que gatea las Custom APIs de digitación (§9). Referenciado en 7 scripts: `scripts/deploy-digitizer-role.sh`, `scripts/grant-digitizer-account-read.sh`, `scripts/register-transaction-cut.sh`, `scripts/register-remove-from-cut.sh`, `scripts/register-get-batch-csv.sh`, `scripts/register-batch-summary.sh`, `scripts/register-cashier-day.sh` (las 3 últimas son de los plugins **nuevos** `CHGetBatchCsvPlugin`/`CHBatchSummaryPlugin`/`CHCashierDayPlugin`, gateadas por el mismo privilegio). | [`docs/procesos/06-digitacion-core-bancario.md`](../procesos/06-digitacion-core-bancario.md) §7 | Sin revisar | Es el rol con más superficie de crecimiento reciente: 3 de los 5 Custom APIs nuevos de digitación (§8/§9) lo usan como gate. |
| `CH-SR-EmailIngestionFlow` | | Conexión Dataverse de los Cloud Flows de ingesta de correo (no un rol de humano). Creado por `scripts/provision-email-ingestion-roles.sh` ("y nada mas: el latido lo escribe MailboxSweep con CH-SR-EmailIngestionFlow"). | [`docs/procesos/02-recepcion-ingesta-emails.md`](../procesos/02-recepcion-ingesta-emails.md) | Sin revisar | **NUEVO desde el export del 2026-08-24**. `roleid` `{d7d07f26-2ca1-f111-aaac-7ced8d3ca69e}`. Según `docs/procesos/02-recepcion-ingesta-emails.md`: "creado, sin asignar" al SP al momento de la última revisión de ese doc — confirmar en vivo si ya se asignó. |
| `CH-SR-EmailOperationsConsole` | | Humanos de Operaciones en `CH-App-IngestionConsole` (`sanic_chappemailvalidationconsole_f3b93`). Creado por `scripts/provision-email-ingestion-roles.sh`. Campos adicionales otorgados por `scripts/grant-console-email-fields.sh` (`ROLE = 'CH-SR-EmailOperationsConsole'`). Usado también como ejemplo de "usuario sin admin de ninguna clase" en la prueba de impersonación del agujero de `CH-Tbl-Parameter` (`docs/desarrollo/09-app-configuracion.md` §6). | [`docs/procesos/02-recepcion-ingesta-emails.md`](../procesos/02-recepcion-ingesta-emails.md) | Sin revisar | **NUEVO desde el export del 2026-08-24**. `roleid` `{10a38b2c-2ca1-f111-aaac-7ced8d3ca69e}`. Según `docs/procesos/02-recepcion-ingesta-emails.md`: "creado, sin asignar" al momento de la última revisión de ese doc — confirmar en vivo. |
| `CH-SR-OpsAdmin` | | Rol de operaciones/soporte (proceso 08). **Cero privilegios sobre tablas custom** (`docs/referencia/roles-y-seguridad.md:16`, "mismo resultado vacío que AnalyticsRead"; `docs/soluciones/01-core.md:139` lo describe byte-a-byte idéntico a `CH-SR-AnalyticsRead`). Referenciado en `scripts/apply-supervisor-role-remediation.sh`, `scripts/provision-appcatalog-access.sh`. | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1-2 | Sin revisar | Un rol de "administración de operaciones" sin ninguna app mapeada y sin privilegios de tabla — mismo hallazgo que `AnalyticsRead`. |
| `CH-SR-Receptionist` | | Oficial de bóveda/recepción (`CH-App-Reception`). CRUD sobre `Bags`/`ReceptionBatch`/`Receptions`/`ApprovalRequest`/`TransportCompany`; ningún privilegio sobre tablas de conteo (`docs/referencia/roles-y-seguridad.md:17`). Referenciado en `scripts/provision-appcatalog-access.sh`. | [`docs/referencia/roles-y-seguridad.md`](../referencia/roles-y-seguridad.md) §1-2 | Sin revisar | |

## 7. Plugin assembly

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `CashHub.Plugins` | | `solutions/unpacked/core/PluginAssemblies/CashHubPlugins-58F5DA7C-B190-F111-B8DA-002248097B5C/` (`PluginAssemblyId={58f5da7c-b190-f111-b8da-002248097b5c}`, `FullName="CashHub.Plugins, Version=1.0.0.0, ..., PublicKeyToken=d41f54dee76a38a6"`). Registra **38 `PluginType`** en `CashHubPlugins.dll.data.xml` (subió de 29 a 38: +9 clases nuevas desde el 2026-08-24, ver §8). 25 de esas 38 tienen step en la solución (`solutions/unpacked/core/SdkMessageProcessingSteps/`, 77 archivos de step en total); las otras 13 son backing de Custom APIs registradas fuera de solución por `scripts/register-*.sh`/`scripts/provision-*.sh`. | [`docs/soluciones/01-core.md`](../soluciones/01-core.md) §7, [`docs/procesos/08-operacion-y-soporte.md`](../procesos/08-operacion-y-soporte.md) §7 | Sin revisar | Única DLL registrable (net462, firmada con `CashHub.snk` — **no versionado en el repo**, prerrequisito de entorno). Compila dentro de sí el dominio puro `CashHub.Validation` (`<Compile Include="../CashHub.Validation/**/*.cs">`). El `AssemblyVersion` (`1.0.0.0`) no se movió pese a las 9 clases nuevas — no hay bump automático de versión de assembly ligado a los `register-*.sh`, a diferencia del versionado de solución (CashHubCore 1.14.0.3). |

## 8. Plugin classes (steps)

> Fuente: `src/backend/CashHub.Plugins/*.cs` — clases que implementan `IPlugin`. De los **49 archivos `.cs`** del proyecto (subió de 36 a 49: **+13 archivos nuevos** desde el 2026-08-24), **38 implementan `IPlugin`** (listadas abajo, subió de 29 a 38: **+9 clases nuevas**, todas con commit posterior al 2026-08-24 — verificado con `git log --diff-filter=A`) y **11 son helpers/schemas sin `IPlugin`** (subió de 7 a 11: **+4 helpers nuevos** — `ApproverTeamLookup.cs`, `BagRecountWipe.cs`, `ChangeRequestApproverResolver.cs`, `CoreCsvExport.cs`), excluidos de esta tabla por instrucción explícita: `BatchTotalsCalculator.cs`, `ClientBranchMirrorSchema.cs`, `SupervisorMirrorSchema.cs`, `ClientReplyComposer.cs`, `DeclarationMaterializer.cs`, `ReceivedEmailValidationService.cs`, `Schema.cs`, más los 4 nuevos recién listados.
>
> Columna "Mensaje/Etapa": tomada literalmente del comentario XML-doc (`/// <summary>`) de cada clase — es código vivo del repo, no una suposición, pero **no es el mismo artefacto que el registro real en Dataverse** (`sdkmessageprocessingstep`, dado de alta por los scripts `scripts/register-*.sh`/`scripts/provision-*.sh`). Ante duda sobre si el step vive hoy exactamente así en el entorno, marcar "verificar en vivo" al revisar.
>
> **Las 9 clases nuevas están todas activas** — ninguna es código muerto. `GuardChangeRequestLinePlugin` tiene step registrado en la solución (`solutions/unpacked/core/SdkMessageProcessingSteps/{25b5a32c-...}.xml`, `{28b5a32c-...}.xml`, `{23b5a32c-...}.xml`); las otras 8 son backing de Custom APIs registradas por script (`scripts/register-*.sh` o `scripts/provision-*.sh`), mismo patrón que `CHWorkloadByCashierPlugin`/`CreateTransactionCutPlugin`/etc. en la tabla vieja.

| Clase (.cs) | Mensaje/Etapa | Tabla(s) que toca | Regla de negocio que defiende (resumen 1 línea) | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|---|---|---|
| `ApprovalRequestBagNumberPlugin` | Pre-operation (20), Create | `sanic_chtblapprovalrequest` | Estampa `sanic_bagnumber` (B-6) leído del JSON de `sanic_requestresources` para que la bandeja de aprobaciones busque por número de tula (delegable); nunca bloquea el Create. | | Step registrado en solución (`PluginTypeId=2f84e0fa-f398-f111-b8dd-7ced8d7091fc`). | | Sin revisar | |
| `BagReceptionMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtblbags`; Post-op (40) Update `sanic_chtblreceptions` (cascada paginada de 500) | `sanic_chtblbags`, `sanic_chtblreceptions` | Espeja `sanic_receptionbatchid` (B-2) y `sanic_receiptidmirror` (B-3) de la planilla hacia la tula — el combo/filtro por lote no delega en dos saltos. | | Step registrado en solución. | | Sin revisar | |
| `BagReconciliationMirrorPlugin` | Pre-op (20), Create y Update | `sanic_chtblbags` | Materializa `sanic_decltotalnio/usd/eur` + `sanic_hasdifference` — el join Bag→DeclaredBag no delega en Power Fx. | | Step registrado en solución. | | Sin revisar | |
| `BagRecountClosurePlugin` | Pre-op (20) TOTALES + Post-op (40) ASIENTOS, Update `sanic_chtblbags` filtrado en `sanic_bagstatus` | `sanic_chtblbags`, `sanic_chtbltransactions` | Recalcula TotalNIO/USD/EUR server-side (el cliente puede truncar la colección en memoria y cerrar la tula con menos plata) y crea los asientos `CH-Tbl-Transactions` de forma idempotente al cerrar. | | Step registrado en solución. | | Sin revisar | Puntos 2 y 3 del guard de Recounting v2. |
| `BatchTransitionStampPlugin` | Pre-op (20), Update, filtrado en `sanic_batchstatus`, después del guard | `sanic_transactionbatches` | Sella server-side `sanic_ingresadopor/on` y `sanic_aplicadopor/on` con `InitiatingUserId` y hora del servidor — insumo de la segregación de funciones (quien marcó Ingresado no puede marcar Aplicado). | | Step registrado en solución. | | Sin revisar | |
| `CHPreviewTransactionCutPlugin` | Custom API `sanic_CHPreviewTransactionCut` (no es step sobre mensaje/etapa de tabla) | `sanic_chtbltransactions` (lectura) | Dry-run del corte: cuenta transacciones elegibles y suma totales por moneda con la MISMA query que el corte real, sin crear nada. | | Registrada por script (`scripts/register-preview-cut.sh`), fuera de solución. | | Sin revisar | |
| `CHRemoveFromCutPlugin` | Custom API `sanic_CHRemoveFromCut` | `sanic_chtbltransactions`, `sanic_transactionbatches` | Saca una transacción de su lote, persiste el motivo del rechazo del Core y recalcula totales del lote — atómico. | | Registrada por script (`scripts/register-remove-from-cut.sh`), fuera de solución. | | Sin revisar | |
| `CHWorkloadByCashierPlugin` | Custom API `sanic_CHWorkloadByCashier` | `sanic_chtbltransactions`, `sanic_transactionbatches` (aggregate) | Agregado server-side por cajero (pendientes de corte/manual/lotes por aplicar) — reemplaza un GroupBy que no delega en canvas. | | Registrada por script (`scripts/register-workload.sh`), fuera de solución. | | Sin revisar | |
| `CheckMirrorStampPlugin` | Pre-op (20) Create + Post-op (40) Create + Pre-op (20) Update, `sanic_chtblchecks`; Post-op (40) Update `sanic_chtblbags` (cascada paginada de 500) | `sanic_chtblchecks`, `sanic_chtbldeclaredchecks`, `sanic_chtblbags` | Espeja `sanic_bag` (B6), `sanic_matchedcheck` (B3) y `sanic_recountingsession` (B-9) del cheque físico — evita dot-walk de dos saltos que no delega y miente el conteo (QA lo probó con 2.074 cheques). | | Step registrado en solución. | | Sin revisar | Create partido en dos pasos por integridad referencial (0x80040265). |
| `CheckStatusStampPlugin` | Pre-op (20), Update, filtrado en `sanic_checkstatus`, después del guard | `sanic_chtblchecks` | Sella fecha/hora y actor (`sanic_senttodigitizationby/on`, `sanic_receivedby/on`, `sanic_notreceivedon`) del handshake de recepción de cheques, server-side. | | Step registrado en solución. | | Sin revisar | |
| `ClientBranchNameMirrorPlugin` | Pre-op (20), Create y Update (filtrado en `sanic_client`) | `sanic_chtblclientbranch` | Espeja `sanic_clientnamemirror` (texto 850) del cliente — el combo de sucursales por nombre de cliente no delega. | | Step registrado en solución. | | Sin revisar | **Sin cascada por rename del cliente** (deliberado) — cobertura vía `scripts/backfill-clientbranch-mirror.py`. |
| `CreateTransactionCutPlugin` | Custom API `sanic_CHCreateTransactionCut` | `sanic_transactionbatches`, `sanic_chtbltransactions` | Corte atómico por cajero: crea el lote `Generado` explícito, asigna en páginas de 500 las transacciones elegibles, borra el lote si queda vacío (regla W6). | | Registrada por script (`scripts/register-transaction-cut.sh`), fuera de solución. | | Sin revisar | |
| `DeclaredCheckBagMirrorPlugin` | Pre-op (20), Create y Update (filtrado en `sanic_declareddepositid`) | `sanic_chtbldeclaredchecks` | Espejo E-3: `sanic_declaredbagmirror` derivado del depósito declarado — evita ~82 round-trips al abrir una tula. | | Step registrado en solución. | | Sin revisar | Sin cascada desde el depósito declarado (deliberado, respaldado por el orden de `DeclarationMaterializer`). |
| `DepositSessionMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtbldepositsrecounting`; Post-op (40) Update `sanic_chtblbags` (cascada paginada de 500) | `sanic_chtbldepositsrecounting`, `sanic_chtblbags` | Espejo E-1: `sanic_recountingsessionmirror` — la cascada es obligatoria por el flujo de "reabrir tula" (re-cuelga depósitos ya creados de otra sesión). | | Step registrado en solución. | | Sin revisar | |
| `GuardAppCatalogAccessPlugin` | Pre-op, Create y Update | `sanic_chtblappcatalogaccess` | Normaliza el GUID de grupo AAD y bloquea filas duplicadas (misma app + mismo grupo). | | Step registrado en solución. | | Sin revisar | |
| `GuardAppCatalogPlugin` | Pre-validation, Create y Update | `sanic_chtblappcatalog` | Valida que `sanic_appurl` apunte al player de Power Apps y que `sanic_aadgroupid` sea una lista válida de GUIDs separados por `;`. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `AppCatalogGuard`. |
| `GuardChangeRequestApprovalPlugin` | Pre-op, Create y Update | `sanic_chtblclientdatachangerequest` | Segregación de funciones: bloquea que un Contributor se auto-apruebe un RFC por Web API/Excel aunque la app oculte los botones. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `ChangeRequestApprovalGuard`; resuelve membresía del grupo aprobador. |
| `GuardChangeRequestLinePlugin` | Pre-operation (20), Create/Update/Delete de la línea (`CashHub.GuardChangeRequestLine: Create/Update/Delete de linea`, steps `{23b5a32c-...}`/`{25b5a32c-...}`/`{28b5a32c-...}`) | `sanic_chtblclientdatachangerequestline` | Cierra el gap "no hay guard sobre ClientDataChangeRequestLines; crear/borrar líneas de un RFC ajeno en Draft vía Web API es posible para un Approver con Write global" — mismo esqueleto que `GuardChangeRequestApprovalPlugin`. | | Step registrado en solución (3 steps: Create/Update/Delete). | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `2bf96bb`, 2026-08-25). Decisión en dominio puro `ChangeRequestLineGuard`. Nota: esta clase **ya estaba citada** en la sección 6 del inventario viejo (fila `CH-SR-ClientAdminApprover`) pero faltaba en esta tabla — omisión del inventario anterior, no algo nuevo del sync de hoy. |
| `GuardReceptionPlugin` | Pre-validation, Create/Update/Delete (4 tablas) | Lotes, planillas, tulas y solicitudes de aprobación (recepción) | Impone la máquina de estados de recepción server-side (la v1 solo la imponía en Power Fx) — un único plugin type despacha por entidad/mensaje. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `ReceptionGuard`; exige membresía del grupo aprobador (fail-closed) para resolver solicitudes. |
| `GuardRecountingPlugin` | Pre-validation (10), Create/Update/Delete | `sanic_chtbldepositsrecounting`, `sanic_chtblchecks`, `sanic_chtblcountedmoneydetails`, y bajas acotadas de `sanic_chtblapprovalrequest`/`sanic_chtblrecountingsession` | Puntos 4 y 5 del guard de Recounting v2: rechaza escrituras si la tula ancestro no está "Asignada a cajero"; permite bajas solo de lo propio. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `RecountingGuard`; excepciones por Depth>1 y por update de solo `sanic_checkstatus`. |
| `GuardSupervisorAccountingPlugin` | Pre-validation (10), Create | `sanic_chtbltransactions` | Recalcula la diferencia Sobrante/Faltante server-side (lee depósito recontado + declarado + asientos existentes) y bloquea montos que no coincidan — mata H-02. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `AccountingGuard`; separado de `GuardTransactionPlugin` (dominios distintos). |
| `GuardTransactionPlugin` | Pre-validation, Create/Update (2 tablas), Delete (lote) | `sanic_transactionbatches`, `sanic_chtbltransactions`, `sanic_chtblchecks` (handshake) | Impone la máquina de estados del lote (Generado→Ingresado→Aplicado) y el handshake de cheques server-side; sin excepción por depth (el CutPlugin también debe cumplirla). | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `TransactionGuard`/`CheckGuard`. |
| `ReceptionBatchStatusMirrorPlugin` | Post-op, Update lote (filtrado en `sanic_receptionstatus`); Pre-op, Create/Update planilla | `sanic_chtblreceptionbatch`, `sanic_chtblreceptions` | Espeja `sanic_batchstatus` del lote en cada planilla — el filtro por estado de lote no delega en la entidad relacionada. | | Step registrado en solución. | | Sin revisar | |
| `ReceptionBatchTotalsPlugin` | Post-op (40), Create/Update/Delete | `sanic_chtblreceptions`, `sanic_chtblreceptionbatch` | Materializa los totales declarados del lote (`sanic_decniocash/check/total`, `sanic_decusdcash/check/total`) sumando planillas. | | Step registrado en solución. | | Sin revisar | Decisión en dominio puro `ReceptionBatchTotalsTrigger`. |
| `RecountingSessionAutoApprovePlugin` | Pre-op (20) + Post-op (40), Create | `sanic_chtblapprovalrequest`, `sanic_chtblrecountingsession` | Auto-aprobación server-side de la sesión de recuento por "mesa propia" — reemplaza el `varIsOwnDesk` client-side que cualquiera podía falsear. | | Step registrado en solución. | | Sin revisar | Punto 1 del guard de Recounting v2; convive con `GuardReceptionPlugin` (Pre-validation). |
| `TransactionBatchStatusMirrorPlugin` | Post-op, Update lote (propaga); Pre-op, Create/Update transacción | `sanic_transactionbatches`, `sanic_chtbltransactions` | Espeja `sanic_batchstatus` en cada transacción — el filtro "sin lote"/"por estado de lote" no delega. | | Step registrado en solución. | | Sin revisar | |
| `TransactionBatchTotalsPlugin` | Post-op (40), Update, filtrado en `sanic_transactionbatch`, con pre-image | `sanic_chtbltransactions`, `sanic_transactionbatches` | Recomputa totales del lote viejo y del nuevo al reasignar/desasignar una transacción individual. | | Step registrado en solución. | | Sin revisar | Se corta a Depth>1 (el corte masivo ya recomputa una sola vez). |
| `TransactionRoutingPlugin` | Pre-op (20), Create y Update | `sanic_chtbltransactions` | Ruteo server-side a proceso Batch o Manual — la regla estaba duplicada en 8 sitios Power Fx y ninguno aplicaba "sin cuenta → Manual". | | Step registrado en solución. | | Sin revisar | |
| `ValidateReceivedEmailPlugin` | Custom API `sanic_CHValidateReceivedEmail` (sync) + Post-operation async, Create | `sanic_chtblreceivedemails` | Motor de validación de correos: 28 reglas (VR-01..VR-28), triage de remitente, materialización de la declaración, composición de respuesta. | | Registrada por script (`scripts/register-validation-plugin.sh`), fuera de solución. | | Sin revisar | Guarda anti-bucle Depth>2; corre como sistema. El paso asíncrono en Create fue retirado según `docs/soluciones/04-backend.md` — verificar en vivo si el doc-comment de la clase todavía lo describe así. |
| `XmlReadingDetailMirrorPlugin` | Pre-op (20) Create/Update `sanic_chtblxmlreadingdetail`; Post-op (40) Update `sanic_chtblxmlreading` (cascada paginada de 500) | `sanic_chtblxmlreadingdetail`, `sanic_chtblxmlreading` | Espejo E-4 (dos columnas): `sanic_recountingsessiondetailmirror` + `sanic_xmlreadingactiva` — sin la cascada, Q17 sigue sumando la lectura vieja de la contadora. | | Step registrado en solución. | | Sin revisar | |
| `ResolveChangeRequestPlugin` | Custom API `sanic_CHResolveChangeRequest` (sync, transaccional) | `sanic_chtblclientdatachangerequest`, sus líneas, bitácora y cabecera | Reemplaza los 4 flows materializadores de RFC (`Applyclientdatachanges`/`Createclientdata`/`Activaterow`/`Deactivaterow`); resuelve Approve/Reject de un RFC de forma síncrona y atómica — si algo falla, revierte TODA la transacción. | | Registrada por script (`scripts/register-resolve-changerequest.sh`), fuera de solución. Referenciada también en `scripts/seed-clientadmin-stress.py`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `2bf96bb`, 2026-08-25). |
| `CHBatchSummaryPlugin` | Custom API `sanic_CHBatchSummary` | `sanic_chtbltransactions`/`sanic_transactionbatches` (agregado) | Resumen por cuenta del lote (Cuenta\|Moneda\|Monto\|Cant TRN) y comparación contra el IBS Branch — reemplaza el cotejo manual transacción por transacción (hasta 600 cuentas en el lote más grande del ambiente). | | Registrada por script (`scripts/register-batch-summary.sh`), fuera de solución. Gate: `CH-SR-Digitizer`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `762bf4e`, 2026-08-30). |
| `CHCashierDayPlugin` | Custom API `sanic_CHCashierDay` | `sanic_chtbltransactions` (agregado) | Devuelve el "cubo" de un cajero en un día (moneda × tipo × vía, máx. 24 filas) para que la app haga rollups sobre 24 filas en vez de re-consultar en cada interacción. | | Registrada por script (`scripts/register-cashier-day.sh`), fuera de solución. Gate: `CH-SR-Digitizer`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `742a614`, 2026-08-30). |
| `CHGetBatchCsvPlugin` | Custom API `sanic_CHGetBatchCsv` | `sanic_chtbltransactions`/`sanic_transactionbatches` (lectura de `sanic_csvcore`) | Devuelve el CSV de un lote como texto — reemplaza `Download()` sobre `.../sanic_csvcore/$value`, que fallaba con 401 para usuarios sin cookie de org (apps.powerapps.com). | | Registrada por script (`scripts/register-get-batch-csv.sh`), fuera de solución. Gate: `CH-SR-Digitizer`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `dcf5c14`, 2026-08-30). |
| `ReturnBagForReassignmentPlugin` | Custom API `sanic_CHReturnBagForReassignment` (sync, transaccional) | `sanic_chtblbags` y datos de la mesa de conteo cargados | Saca una tula de la mesa y la deja libre para que la tome otro cajero, borrando todo lo cargado, como unidad transaccional — antes "Asignada a cajero" solo tenía salida vía Recontada o revisión del supervisor. | | Registrada por script (`scripts/provision-devolucion-de-tulas.sh`), fuera de solución. `executeprivilegename: prvWritesanic_CHTblBags`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `3799e22`, 2026-08-30). |
| `ForceCloseRecountingSessionPlugin` | Custom API `sanic_CHForceCloseRecountingSession` (sync, transaccional) | `sanic_chtblrecountingsession`, tulas asociadas | El supervisor cierra a la fuerza una sesión de recuento y libera las tulas que el cajero tenía en la mano, como unidad transaccional, autorizado por `RecountingGuard.EvaluateForceClose`. | | Registrada por script (`scripts/provision-cierre-forzado-sesion.sh`), fuera de solución. `executeprivilegename: prvWritesanic_CHTblRecountingSession`. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `cff6b50`, 2026-08-30). |
| `CanResolveApprovalPlugin` | Custom API `sanic_CHCanResolveApproval` (sync, solo lectura) | `sanic_chtblapprovalrequest` (lectura) | Responde si el usuario que llama puede resolver una solicitud de aprobación y por qué no si no puede — "pre-vuelo" de solo lectura porque Power Fx descarta el mensaje custom de los faults de plugin; la autoridad real sigue siendo `GuardReceptionPlugin` en el Update. | | Registrada por script (`scripts/register-can-resolve-approval.sh`), fuera de solución. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `37f153c`, 2026-08-29). |
| `ResolveApprovalRequestPlugin` | Custom API `sanic_CHResolveApprovalRequest` (sync, transaccional) | `sanic_chtblapprovalrequest` y la entidad afectada por el efecto de la aprobación | Resuelve una solicitud de aprobación y aplica su efecto como unidad transaccional: autoriza con la misma regla que `GuardReceptionPlugin`; si bloquea, devuelve el motivo como dato sin escrituras; si autoriza, aplica el efecto y marca la solicitud en la misma transacción. | | Registrada por script (`scripts/register-resolve-approval-request.sh`), fuera de solución. | | Sin revisar | **NUEVO desde el export del 2026-08-24** (commit `37f153c`, 2026-08-29). |

## 9. Custom APIs

> Fuente: `docs/procesos/06-digitacion-core-bancario.md` §7, `docs/procesos/08-operacion-y-soporte.md` §7, `docs/soluciones/01-core.md` §7, y los `scripts/register-*.sh` / `scripts/provision-*.sh`. **RECONCILIADO 2026-08-31 contra el export sincronizado con DEV (commit `00f6f1c`, CashHubCore 1.14.0.3): la nota "las Custom APIs no se exportan como XML" quedó DESACTUALIZADA — ahora SÍ se exportan.** Verificado: `solutions/unpacked/core/customapis/` tiene 11 carpetas, cada una con `customapi.xml` (definición, `executeprivilegename`, `plugintypeid`) + subcarpetas por parámetro con `customapirequestparameter.xml` / `customapiresponseproperty.xml`. El backing class se resuelve cruzando `<plugintypeexportkey>` de cada `customapi.xml` contra `solutions/unpacked/core/PluginAssemblies/CashHubPlugins-58F5DA7C-B190-F111-B8DA-002248097B5C/CashHubPlugins.dll.data.xml` (mapa PluginTypeId → `AssemblyQualifiedName`). El doc viejo (2026-08-24) documentaba 5; el export actual tiene 11 — 5 nuevas confirmadas altas del commit `00f6f1c` y 1 (`sanic_CHResolveChangeRequest`) que ya existía desde el commit `7fcf12a` (2026-08-29, sync previo) pero nunca tuvo fila en esta tabla.

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_CHValidateReceivedEmail` | | Flow `CH-Flow-ProcessEmail` (`solutions/unpacked/flow/Workflows/CH-Flow-ProcessEmail-A1B2C3D4-5E6F-4A7B-8C9D-0E1F2A3B4C5D.json`, línea con `"actionName": "sanic_CHValidateReceivedEmail"`). | | Sin revisar | Backing: `CashHub.Plugins.ValidateReceivedEmailPlugin` (confirmado por `plugintypeexportkey` `5af5da7c-b190-f111-b8da-002248097b5c` → `CashHub.Plugins.ValidateReceivedEmailPlugin` en `CashHubPlugins.dll.data.xml`). Script: `scripts/register-validation-plugin.sh`. `executeprivilegename`: **ninguno** en `solutions/unpacked/core/customapis/sanic_CHValidateReceivedEmail/customapi.xml` — confirmado, sigue abierto a cualquiera con acceso de escritura a la tabla. In: `ReceivedEmailId` (Guid); out: `RunId` (Guid), `Result` (String), `FailedFindings` (Integer). |
| `sanic_CHWorkloadByCashier` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrManual.fx.yaml`, `scrCorte.fx.yaml`, `scrMovimientos.fx.yaml`, `scrLotes.fx.yaml` (Digitization). | | Sin revisar | Backing: `CashHub.Plugins.CHWorkloadByCashierPlugin`. Script: `scripts/register-workload.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. Sin parámetros de entrada; out: `Workload` (String, JSON). |
| `sanic_CHPreviewTransactionCut` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrCorte.fx.yaml` (Digitization). | | Sin revisar | Backing: `CashHub.Plugins.CHPreviewTransactionCutPlugin`. Script: `scripts/register-preview-cut.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `CajeroId` (Guid); out: `EligibleCount` (Integer), `TotalNIO`/`TotalUSD`/`TotalEUR` (Decimal). |
| `sanic_CHRemoveFromCut` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrLotes.fx.yaml` (Digitization). | | Sin revisar | Backing: `CashHub.Plugins.CHRemoveFromCutPlugin`. Script: `scripts/register-remove-from-cut.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `TransactionId` (Guid), `Motivo` (String, opc.); out: `BatchId` (Guid), `TotalNIO`/`TotalUSD`/`TotalEUR` (Decimal). |
| `sanic_CHCreateTransactionCut` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrCorte.fx.yaml` (Digitization). | | Sin revisar | Backing: `CashHub.Plugins.CreateTransactionCutPlugin`. Script: `scripts/register-transaction-cut.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `CajeroId` (Guid); out: `BatchId` (Guid), `BatchName` (String), `AssignedCount` (Integer). **Confirmado como reemplazo del flow `CH-Flow-CreateCut`, que fue eliminado del ambiente en el sync de hoy (commit `00f6f1c`)** — el GUID viejo del flow (`131b6816-d69e-4041-a344-df9fe3e49655`) y el string `CreateCut` ya no aparecen en ningún archivo de `solutions/unpacked/` (verificado con `rg`), solo en docs de referencia viejos. |
| `sanic_CHCanResolveApproval` | | **Sin coincidencias.** Verificado con `rg -i` sobre los 53 `.fx.yaml` de `solutions/unpacked/apps/CanvasApps/src/**/Src/**` y sobre `solutions/unpacked/flow/Workflows/*.json`: ninguna canvas app ni flow lo invoca. Solo aparece en el catálogo auto-generado del conector (`pkgs/Wadl/Environment.xml` de varias apps), que lista TODAS las acciones disponibles del ambiente, no evidencia de uso real. | | Sin revisar | **NUEVO desde el export del 2026-08-24.** Backing: `CashHub.Plugins.CanResolveApprovalPlugin` (`src/backend/CashHub.Plugins/CanResolveApprovalPlugin.cs`). Script: `scripts/register-can-resolve-approval.sh`. `executeprivilegename`: `prvWritesanic_CHTblApprovalRequest`. In: `ApprovalRequestId` (Guid); out: `Outcome` (String), `Message` (String). Descripción del propio `customapi.xml`: "Pre-vuelo de solo lectura: dice si quien llama puede resolver una solicitud de aprobacion... No escribe; la autoridad sigue siendo GuardReceptionPlugin." |
| `sanic_CHForceCloseRecountingSession` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrResumen.fx.yaml` (Supervisor), llamada `Environment.sanic_CHForceCloseRecountingSession({ SessionId: ..., Reason: ... })`. | | Sin revisar | **NUEVO desde el export del 2026-08-24.** Backing: `CashHub.Plugins.ForceCloseRecountingSessionPlugin` (`src/backend/CashHub.Plugins/ForceCloseRecountingSessionPlugin.cs`). Script: `scripts/provision-cierre-forzado-sesion.sh` (no sigue el patrón `register-*.sh`, es un script `provision-*` en Python que hace upsert vía Web API). `executeprivilegename`: `prvWritesanic_CHTblRecountingSession`. In: `SessionId` (Guid), `Reason` (String); out: `Outcome` (String), `Message` (String). |
| `sanic_CHGetBatchCsv` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrLotes.fx.yaml` (Digitization), llamada `Environment.sanic_CHGetBatchCsv({ BatchId: ... })`, resultado alimenta el control PCF `FileDownloader1` (`CashHub.FileDownloader`, ver sección 14). | | Sin revisar | **NUEVO desde el export del 2026-08-24.** Backing: `CashHub.Plugins.CHGetBatchCsvPlugin` (`src/backend/CashHub.Plugins/CHGetBatchCsvPlugin.cs`). Script: `scripts/register-get-batch-csv.sh`. `executeprivilegename`: `prvCreatesanic_TransactionBatches`. In: `BatchId` (Guid); out: `Csv` (String), `FileName` (String), `SizeBytes` (Integer). Nota: `scrLotes.fx.yaml` también llama `'CH-Flow-DownloadData'.Run(...)` en el mismo bloque — conviven dos mecanismos de descarga, verificar en vivo cuál es el vigente. |
| `sanic_CHResolveApprovalRequest` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrAprobaciones.fx.yaml` (Supervisor), llamada `Environment.sanic_CHResolveApprovalRequest({ ApprovalRequestId: ..., Decision: ..., Reason: ... })`. Comentario en el propio `.fx.yaml`: "UNA sola llamada. La CAPI... autoriza, aplica el efecto y marca la solicitud EN LA MISMA TRANSACCION." | | Sin revisar | **NUEVO desde el export del 2026-08-24.** Backing: `CashHub.Plugins.ResolveApprovalRequestPlugin` (`src/backend/CashHub.Plugins/ResolveApprovalRequestPlugin.cs`). Script: `scripts/register-resolve-approval-request.sh`. `executeprivilegename`: `prvWritesanic_CHTblApprovalRequest`. In: `ApprovalRequestId` (Guid), `Decision` (String), `Reason` (String); out: `Outcome` (String), `Message` (String). |
| `sanic_CHReturnBagForReassignment` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/scrTulas.fx.yaml` (Supervisor), llamada `Environment.sanic_CHReturnBagForReassignment({ BagId: ..., Reason: "Devuelta por el supervisor" })`. | | Sin revisar | **NUEVO desde el export del 2026-08-24.** Backing: `CashHub.Plugins.ReturnBagForReassignmentPlugin` (`src/backend/CashHub.Plugins/ReturnBagForReassignmentPlugin.cs`). Script: `scripts/provision-devolucion-de-tulas.sh` (no sigue el patrón `register-*.sh`). `executeprivilegename`: `prvWritesanic_CHTblBags`. In: `BagId` (Guid), `Reason` (String); out: `Outcome` (String), `Message` (String). |
| `sanic_CHResolveChangeRequest` | | `solutions/unpacked/apps/CanvasApps/src/sanic_chappclientadministration_fd97f/Src/scrSolicitud.fx.yaml` (ClientAdministration). | | Sin revisar | **NO ES NUEVO DE HOY, pero FALTABA EN ESTA TABLA** — existe en el export desde el sync anterior (commit `7fcf12a`, 2026-08-29), antes del `00f6f1c` de hoy; el inventario 2026-08-24 nunca la documentó pese a que la sección 11 (Cloud Flows) de este mismo doc ya la mencionaba como reemplazo de `CH-Flow-Applyclientdatachanges`/`CH-Flow-Createclientdata`/`CH-Flow-Activaterow`/`CH-Flow-Deactivaterow`/`CH-Flow-RFCWatchdog`. Backing: `CashHub.Plugins.ResolveChangeRequestPlugin` (`src/backend/CashHub.Plugins/ResolveChangeRequestPlugin.cs`). Script: `scripts/register-resolve-changerequest.sh`. `executeprivilegename`: `prvWritesanic_CHTblClientDataChangeRequest`. In: `ChangeRequestId` (Guid), `Decision` (String), `Comment` (String); out: `Outcome` (String), `Message` (String). |

**Nota — Custom API diseñada pero NO implementada**: `docs/procesos/03-recepcion-tulas-esclusa.md` §7 documenta `sanic_CloseReceptionBatch` como diseño planeado (ítem B0-3) para el cierre atómico de lote. **Verificado de nuevo en el export del 2026-08-31: sigue sin existir** — no hay carpeta `sanic_CloseReceptionBatch` en `solutions/unpacked/core/customapis/`, ni clase `.cs` que la respalde, ni script `register-*`/`provision-*` que la dé de alta. No se agrega como fila.

## 10. Canvas Apps

> Fuente: `solutions/unpacked/apps/CanvasApps/*.meta.xml` (solución `CashHubApps`). **RECONCILIADO 2026-08-31 contra el export sincronizado con DEV (commit `00f6f1c`): la versión real es CashHubApps 1.33.2.0** (el doc viejo del 2026-08-24 citaba 1.31.0.4). **9 carpetas `.meta.xml` en el export actual** (verificado con `fdfind`), las mismas 9 propias de CashHub del doc viejo — **`cat_powercatcomponentlibrary_0be3a` (Power CAT Component Library) YA NO ESTÁ**: no hay ninguna carpeta, `.msapp` ni referencia a "powercat" en todo `solutions/unpacked/` (verificado con `fdfind -i powercat` y `rg -il`, cero resultados). El doc viejo listaba 10; el conteo correcto hoy es 9.

| Nombre interno | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_chapphub_2038c` (CH-App-Hub) | | Lanza las demás apps dinámicamente vía `Launch(galHubApps.Selected.AppURL, ...)` leyendo la tabla `sanic_chtblappcatalog` (`solutions/unpacked/apps/CanvasApps/src/sanic_chapphub_2038c/Src/App.fx.yaml` línea 99 y `scrHub.fx.yaml` línea 449) — el catálogo de apps es dato de tabla, no metadata de solución; no se puede confirmar desde el export qué apps están realmente cargadas en `CH-Tbl-AppCatalog`. 6 DataSources propios. | | Sin revisar | |
| `sanic_chappreception_9ff29` (CH-App-Reception) | 11 DataSources en `solutions/unpacked/apps/CanvasApps/src/sanic_chappreception_9ff29/DataSources/`. | | | Sin revisar | Reconstruida v2 según CHANGELOG — mandan las guías de rebuild, no este doc. |
| `sanic_chapprecounting_54962` (CH-App-Recounting) | 19 DataSources. Consume el flow `CH-Flow-ProcessXMLFiles` vía `'CH-Flow-ProcessXMLFiles'.Run(...)` en `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml` línea 2636. | | | Sin revisar | Reconstruida v2. |
| `sanic_chappsupervisor_75678` (CH-App-Supervisor) | 18 DataSources. Consume 3 Custom APIs nuevas: `sanic_CHForceCloseRecountingSession` (`scrResumen.fx.yaml`), `sanic_CHResolveApprovalRequest` (`scrAprobaciones.fx.yaml`), `sanic_CHReturnBagForReassignment` (`scrTulas.fx.yaml`). | | | Sin revisar | Reconstruida v2. |
| `sanic_chappdigitization_278aa` (CH-App-Digitization) | 13 DataSources. Consume `sanic_CHWorkloadByCashier`, `sanic_CHPreviewTransactionCut`, `sanic_CHRemoveFromCut`, `sanic_CHCreateTransactionCut`, `sanic_CHGetBatchCsv` (nueva) y el control PCF `CashHub.FileDownloader` en `scrLotes.fx.yaml`. Es la única app con dependencia de flow declarada en su propio `.meta.xml` (`CH-Flow-DownloadData`, `workflowEntityId 64049da2-f2c6-f011-8543-7c1e5267db57`). | | | Sin revisar | Reconstruida v2 (Apps 1.32.0.5 según CHANGELOG reciente; el export actual ya está en Apps 1.33.2.0 con cambios adicionales en `scrManual.fx.yaml`: el commit `00f6f1c` borró los archivos locales `Src/Components/cmpDialogo.json`, `cmpEmpty.json`, `cmpHeader.json`, `cmpKpi.json`, `cmpPildora.json`, `cmpRailCajeros.json` — verificar en vivo si esos componentes pasaron a consumirse desde `CH-CL-SharedComponents` o si fue una limpieza de código muerto). |
| `sanic_chappdepositstracker_2b2c3` (CH-App-DepositsTracker) | 22 DataSources (la app con más datasources de las 8). | | | Sin revisar | |
| `sanic_chappclientadministration_fd97f` (CH-App-ClientAdministration) | 15 DataSources. Consume `sanic_CHResolveChangeRequest` en `scrSolicitud.fx.yaml`. | | | Sin revisar | |
| `sanic_chappemailvalidationconsole_f3b93` (CH-App-IngestionConsole) | 14 DataSources. | | | Sin revisar | Nombre interno confirmado `sanic_chappemailvalidationconsole_f3b93` (`<Name>` del `.meta.xml`), DisplayName confirmado `CH-App-IngestionConsole` (`<DisplayName>` del `.meta.xml`) — la discrepancia de nombres sigue vigente en el export de hoy, no se resolvió. |
| `sanic_chclsharedcomponents_85fca` (CH-CL-SharedComponents) | **Verificado: `ComponentReferences.json` está vacío (`[]`) en las 8 apps operables** (`sanic_chapphub_2038c`, `sanic_chappreception_9ff29`, `sanic_chapprecounting_54962`, `sanic_chappsupervisor_75678`, `sanic_chappdigitization_278aa`, `sanic_chappdepositstracker_2b2c3`, `sanic_chappclientadministration_fd97f`, `sanic_chappemailvalidationconsole_f3b93`) — ninguna la importa como librería de componentes por el mecanismo estándar. | | | Sin revisar | Librería de componentes compartidos (no es una app operable por sí misma). **Ver veredicto de uso — candidata a SOLO-METADATA/SIN-USO, contradice la premisa de "compartida".** |
| `cat_powercatcomponentlibrary_0be3a` (Power CAT Component Library) | | | | Sin revisar | **AUSENTE EN EL EXPORT ACTUAL.** Verificado con `fdfind -i powercat` sobre todo `solutions/unpacked/` y `rg -il "powercatcomponentlibrary\|cat_powercatcomponentlibrary"` sobre `solutions/unpacked/apps/`: cero resultados. La solución `CashHubApps` ya no la trae — no se puede confirmar desde el repo si fue removida de la solución en el ambiente o si el unpack de hoy simplemente no la trajo (es de Microsoft, no de CashHub); verificar en vivo si sigue instalada en el ambiente aunque no esté en `CashHubApps`. |

## 11. Cloud Flows

> Fuente: `solutions/unpacked/flow/Workflows/*.json.data.xml` (solución `CashHubFlows`). **RECONCILIADO 2026-08-31 contra el export sincronizado con DEV (commit `00f6f1c`): la versión real es CashHubFlows 1.12.0.2** (el doc viejo del 2026-08-24 citaba 1.12.0.0). **18 flujos en el repo actual** (antes 19) — `CH-Flow-CreateCut` (GUID `131b6816-d69e-4041-a344-df9fe3e49655`) fue **eliminado del ambiente**: no existe ningún `.json`/`.json.data.xml` con ese nombre ni GUID en `solutions/unpacked/flow/Workflows/`, y `rg -il` sobre todo el repo (excluyendo `.git`) para el GUID y para el string `CreateCut` solo devuelve coincidencias en docs de referencia vieja (`docs/digitization-rebuild/*.md`, este mismo inventario) — **ninguna canvas app ni flow vivo lo sigue referenciando**. El corte de transacciones ahora vive como los Custom API `sanic_CHCreateTransactionCut` / `sanic_CHPreviewTransactionCut` / `sanic_CHRemoveFromCut` (sección 9). Estado ON/OFF releído de `<StateCode>`/`<StatusCode>` de cada `.json.data.xml`: `StateCode=1`/`StatusCode=2` = Activado (ON); `StateCode=0`/`StatusCode=1` = Draft (OFF). **CAMBIOS DE ESTADO CONFIRMADOS respecto al doc del 2026-08-24** (releídos directamente de los XML de hoy, no asumidos): `CH-Flow-Notifyreviewers` pasó de OFF a **ON**; `CH-Flow-Createclientdata` pasó de ON a **OFF**; `CH-Flow-RFCWatchdog` pasó de ON a **OFF**; `CH-Flow-ProcessXMLFiles` pasó de OFF a **ON**. Las 4 discrepancias que el doc viejo marcaba como "CONFIRMADA 2026-08-25" quedan **RESUELTAS** en el export de hoy — el diseño y el ambiente vivo ahora coinciden en los 4 casos.

| Nombre | Estado (ON/OFF según export 2026-08-31) | Objetivo | Dónde se usa | Documentos de referencia | Estado revisión | Notas |
|---|---|---|---|---|---|---|
| `CH-Flow-IngestEmails` | ON (sin cambio) | | Dispara `CH-Flow-ProcessEmail` como hijo (confirmado: GUID de ProcessEmail `a1b2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d` aparece en `CH-Flow-IngestEmails-...json`). | | Sin revisar | Trigger Outlook `SharedMailboxOnNewEmailV2` (confirmado en el JSON). Buzón hardcodeado — según export, puede estar desactualizado, ver CHANGELOG.md. |
| `CH-Flow-ProcessEmail` | ON (sin cambio) | Llama al Custom API `sanic_CHValidateReceivedEmail` (confirmado, `"actionName": "sanic_CHValidateReceivedEmail"` en el JSON). | Hijo de `CH-Flow-IngestEmails` y `CH-Flow-MailboxSweep` (ambos GUIDs de ProcessEmail confirmados dentro de esos dos JSON). | | Sin revisar | |
| `CH-Flow-MailboxSweep` | ON (sin cambio) | Recurrence 10 min (confirmado: `"frequency": "Minute", "interval": 10`). Dispara `CH-Flow-ProcessEmail` como hijo. | | | Sin revisar | |
| `CH-Flow-Replytoclientemail` (Reply to client email) | ON (sin cambio) | Trigger propio `OpenApiConnectionWebhook` (confirmado) — no depende de otro flow. | | | Sin revisar | |
| `CH-Flow-Notifyreviewers` (Notify reviewers) | **ON — CAMBIÓ desde OFF** | Avisar por Teams a los Approvers de la tabla objetivo cuando un RFC pasa a Submitted. | RFC de Client Administration (`sanic_chtblclientdatachangerequest`). Dispara `CH-Flow-CreateaTeamschatgroupbasedonOffice365Group` como hijo (GUID confirmado dentro del JSON de Notifyreviewers). | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | **DISCREPANCIA RESUELTA**: el doc viejo (2026-08-24/25) confirmaba `StateCode=0` (OFF) contra un diseño que lo daba por ON. Releído hoy: `StateCode=1`/`StatusCode=2` (ON) — coincide con el diseño. Verificar en vivo que el cambio sea intencional y no un artefacto del sync. |
| `CH-Flow-Applyclientdatachanges` (Apply client data changes) | OFF (Draft) (sin cambio) | (Retirado por diseño) Materializar por campo un RFC de Update aprobado — reemplazado por `sanic_CHResolveChangeRequest`. | — (retirado; ver guía 01 §5 histórico) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado de nuevo — sigue coincidiendo con el diseño. |
| `CH-Flow-Createclientdata` (Create client data) | **OFF — CAMBIÓ desde ON** | (Retirado por diseño) Materializar un RFC de Create aprobado — reemplazado por `sanic_CHResolveChangeRequest`. | — (retirado) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5, §10 | Sin revisar | **DISCREPANCIA RESUELTA**: el doc viejo confirmaba `StateCode=1` (ON) contra un diseño que lo daba por retirado. Releído hoy: `StateCode=0`/`StatusCode=1` (OFF) — ya coincide con el diseño, el riesgo de materialización en paralelo quedó cerrado. |
| `CH-Flow-Activaterow` (Activate row) | OFF (Draft) (sin cambio) | (Retirado por diseño) Activar/desactivar registro vía RFC — reemplazado por `sanic_CHResolveChangeRequest`. | — (retirado) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado de nuevo. |
| `CH-Flow-Deactivaterow` (Deactivate row) | OFF (Draft) (sin cambio) | (Retirado por diseño) Ídem Activate row, para desactivación. | — (retirado) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado OFF confirmado de nuevo. |
| `CH-Flow-NotifywhenRFCimplementationfinish` (Notify when RFC implementation finish) | ON (sin cambio) | Avisar por Teams cuando un RFC llega a un estado final (Applied/Rejected/Failed). | RFC de Client Administration. | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5 | Sin revisar | Estado ON confirmado de nuevo — coincide con el diseño. |
| `CH-Flow-RFCWatchdog` | **OFF — CAMBIÓ desde ON** | (Retirado por diseño) Barrido cada 15 min de RFC en `Approved` vencidos, marcarlos `Failed`. | — (retirado) | [`docs/procesos/01-administracion-datos-clientes.md`](../procesos/01-administracion-datos-clientes.md) §5, §10 | Sin revisar | **DISCREPANCIA RESUELTA**: el doc viejo confirmaba `StateCode=1` (ON, recurrence 15 min intacto) contra un diseño que lo daba por retirado. Releído hoy: `StateCode=0`/`StatusCode=1` (OFF) — el recurrence de 15 min sigue definido en el JSON pero el flow está en Draft, ya no corre. Coincide con el diseño. |
| `CH-Flow-CreateaTeamschatgroupbasedonOffice365Group` (Create a Teams chat group based on Office 365 Group Members) | ON (sin cambio) | | Hijo de `Notifyreviewers` (GUID confirmado dentro del JSON de Notifyreviewers) — ahora que `Notifyreviewers` volvió a ON, este hijo vuelve a dispararse en la práctica. | | Sin revisar | |
| `CH-Flow-ExtractedTablesFromExcel` | ON (sin cambio) | Trigger `Request`/`kind: PowerApp` (confirmado en el JSON — diseñado para invocarse desde una canvas app). | **Sin coincidencias en `.fx.yaml` de ninguna de las 8 canvas apps ni en `ComponentReferences`/`DataSources`/`Connections.json`** (`rg -il "extractedtablesfromexcel"` sobre `solutions/unpacked/apps/CanvasApps/src` completo, cero resultados). | | Sin revisar | (Legado) el trigger sigue siendo invocable desde canvas apps por diseño, pero ninguna app viva lo llama hoy — verificar en vivo si quedó huérfano. |
| `CH-Flow-GetJSONdatafromattachment` (Get JSON data from attachment) | ON (sin cambio) | Trigger `Request`/`kind: PowerApp` (confirmado en el JSON). | **Sin coincidencias en `.fx.yaml` de ninguna de las 8 canvas apps** (`rg -il "getjsondatafromattachment"` sobre `solutions/unpacked/apps/CanvasApps/src` completo, cero resultados). | | Sin revisar | (Legado) mismo caso que `ExtractedTablesFromExcel` — invocable por diseño, sin invocador real detectado en el export actual. |
| `CH-Flow-DownloadData` | ON (sin cambio) | | Usada por CH-App-Digitization — confirmado: `workflowEntityId 64049da2-f2c6-f011-8543-7c1e5267db57` y `workflowDisplayName "CH-Flow-DownloadData"` en `sanic_chappdigitization_278aa.meta.xml`, más `'CH-Flow-DownloadData'.Run(gvarLote.TransactionBatches, gvarLote.Name)` en `scrLotes.fx.yaml` línea 789. | | Sin revisar | Convive con el nuevo Custom API `sanic_CHGetBatchCsv` en el mismo bloque de `scrLotes.fx.yaml` — verificar en vivo cuál de los dos mecanismos de descarga es el vigente. |
| `CH-Flow-ProcessXMLFiles` (Process XML Files) | **ON — CAMBIÓ desde OFF** | | Usada por CH-App-Recounting — confirmado: `'CH-Flow-ProcessXMLFiles'.Run(Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'), gvarMesa.MachineSerialNumber)` en `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/scrConteo.fx.yaml` línea 2636. | | Sin revisar | **DISCREPANCIA RESUELTA**: el doc viejo marcaba el riesgo "OFF pese a ser consumida por una app activa". Releído hoy: `StateCode=1`/`StatusCode=2` (ON) — ya coincide con el uso real desde Recounting. |
| `CH-Flow-CreateCut` | — | — | — | — | — | **AUSENTE EN EL EXPORT ACTUAL — eliminado del ambiente.** Confirmado por el mensaje del commit `00f6f1c` ("CH-Flow-CreateCut sale de CashHubFlows y de las dependencias de Digitization: quedó reemplazado por los Custom API CreateTransactionCut, PreviewTransactionCut y RemoveFromCut") y por búsqueda exhaustiva (`rg -il`) del GUID `131b6816-d69e-4041-a344-df9fe3e49655` y del string `CreateCut` en todo el repo: cero coincidencias fuera de docs de referencia vieja. No queda ninguna app ni flow huérfano apuntándolo. |
| `CH-Flow-UnblockOnClient` | ON (sin cambio) | | | | Sin revisar | |
| `CH-Flow-UnblockOnClientEmail` | ON (sin cambio) | | | | Sin revisar | |

## 12. App Model-Driven (NUEVA)

> Componente sin lugar en el inventario anterior. Fuente: `solutions/unpacked/apps/AppModules/sanic_CHAppConfiguration/AppModule.xml`, `solutions/unpacked/apps/Other/Solution.xml`, `solutions/unpacked/apps/WebResources/sanic_/icons/`. Alta del commit `00f6f1c` (sync 2026-08-31) — mencionada también en el commit `f4ce2d2` ("feat(config): app model-driven de configuracion con rol e icono").

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_CHAppConfiguration` (CH-App-Configuration) | | Roles con acceso (`AppModuleRoleMaps` en `AppModule.xml`): `CH-SR-ConfigAdmin` (GUID `227a473d-0ba5-f111-aaac-000d3a3425f0`, confirmado cruzando contra `solutions/unpacked/core/Roles/CH-SR-ConfigAdmin.xml`) + 2 roles más (`627090ff-40a3-4053-8790-584edc5be201`, `119f245c-3cc8-4b62-b31c-d1a046ced15d`) que **no matchean ningún rol custom `CH-SR-*`** en `solutions/unpacked/core/Roles/` — probablemente roles estándar de Dataverse (System Administrator/Customizer), no confirmado por GUID exacto en este repo. | | Sin revisar | **NUEVO desde el export del 2026-08-24 — sección sin lugar en el inventario anterior, propuesta como sección 14.** `statecode=0`/`statuscode=1` en `AppModule.xml` (verificar en vivo qué implica para publicación). Ícono propio: `sanic_/icons/cashhub-configuration.svg` (WebResourceId `a43a484b-0aa5-f111-aaac-000d3a3425f0`, confirmado exacto contra `AppModule.xml`). Descripción del propio XML: "Configuracion de CashHub: parametros del sistema, catalogo de apps del Hub, permisos del modulo RFC, motor de validacion, plantillas de correo y catalogos maestros. Unico punto de administracion de lo que no se toca desde las canvas apps." No hay ninguna canvas app que lo referencie (es un App Module aparte, se abre por URL propia, no embebido). |

## 13. Controles PCF (NUEVA)

> Componente sin lugar en el inventario anterior. Fuente: `solutions/unpacked/core/Controls/sanic_CashHub.FileDownloader/ControlManifest.xml` y `src/pcf/FileDownloader/`.

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_CashHub.FileDownloader` (CashHub File Downloader) | Descripción del propio manifest: "Baja un archivo de texto generado en el servidor, sin navegar ni depender de la cookie del entorno." | **USADO, confirmado**: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/scrLotes.fx.yaml` línea 46, control instanciado como `FileDownloader1 As 'CashHub File Downloader'`; sus propiedades `Status`/`Content` se leen en las líneas 2399-2405 del mismo archivo, alimentadas por el Custom API `sanic_CHGetBatchCsv` (sección 9). | | Sin revisar | **NUEVO desde el export del 2026-08-24 — sección sin lugar en el inventario anterior, propuesta como sección 15.** Namespace `CashHub`, constructor `FileDownloader`, `control-type="standard"`, `api-version="1.3.18"`, construido con `pac` v1.51.1 (`built-by` en el manifest). Propiedades in: `Content` (Multiple), `FileName`/`MimeType`/`Trigger` (SingleLine.Text), `ExpectedBytes` (Whole.None); out: `Status` (SingleLine.Text), `BytesDownloaded` (Whole.None). Fuente TypeScript en `src/pcf/FileDownloader/FileDownloader/` (no se abrió el código fuente en esta reconciliación, solo el manifest exportado y el `bundle.js` compilado). |

## 14. Web Resources (NUEVA)

> Componente sin lugar en el inventario anterior. Fuente: `solutions/unpacked/apps/WebResources/sanic_/`.

| Nombre | Objetivo | Dónde se usa | Documentos de referencia | Estado | Notas |
|---|---|---|---|---|---|
| `sanic_/icons/cashhub-configuration.svg` | Descripción del propio `.data.xml`: "Icono de CH-App-Configuration. Linea de marca CashHub: cuadro rojo #C8102E, glifo blanco de trazo." | Ícono del App Module `sanic_CHAppConfiguration` (sección 14) — `WebResourceId` `a43a484b-0aa5-f111-aaac-000d3a3425f0` confirmado idéntico en ambos archivos. | | Sin revisar | **NUEVO desde el export del 2026-08-24 — sección sin lugar en el inventario anterior, propuesta como sección 16.** `WebResourceType=11` (SVG), `DisplayName` "CashHub \| Icono de Configuracion". Es el único Web Resource en todo el export (`fdfind . solutions/unpacked/apps/WebResources -t f` devuelve un solo archivo de contenido + su `.data.xml`) — si aparecen más Web Resources en sync futuros, esta sección debería ampliarse, no reemplazarse fila por fila. |

## Resumen por categoría

| Categoría | Ítems | Δ vs 2026-08-24 |
|---|---|---|
| 1. Tablas (Entities) | 44 | = |
| 2. Campos por tabla (custom) | 351 | +6 |
| 3. Relaciones (1:N y N:N, de negocio) | 69 | +3 |
| 4. Option Sets / Choices globales | 3 | = |
| 5. Variables de entorno | 23 | = |
| 6. Roles de seguridad | 13 | +3 (1 rename) |
| 7. Plugin assembly | 1 | = |
| 8. Plugin classes (steps) | 38 | +9 |
| 9. Custom APIs | 11 | +6 |
| 10. Canvas Apps | 10 | = (9 vivas + 1 ausente) |
| 11. Cloud Flows | 19 | = (18 vivos + 1 ausente) |
| 12. App Model-Driven | 1 | +1 (nueva categoría) |
| 13. Controles PCF | 1 | +1 (nueva categoría) |
| 14. Web Resources | 1 | +1 (nueva categoría) |
| **Total** | **585** | **+30** |

> Nota de alcance: la sección 3 lista solo relaciones de negocio (69 de 321 totales en el export);
> las 252 restantes son plomería estándar de Dataverse (owner, business unit, team, file
> attachment, createdby/modifiedby), no ítems a revisar uno por uno.

### Componentes marcados AUSENTE EN EL EXPORT ACTUAL

| Componente | Qué pasó |
|---|---|
| `cat_powercatcomponentlibrary_0be3a` | Librería de Microsoft, desapareció por completo del export. Sin rastro en `solutions/unpacked/`. |
| `CH-Flow-CreateCut` (`{131b6816-d69e-4041-a344-df9fe3e49655}`) | Eliminado del ambiente. Reemplazado por los Custom API `sanic_CHCreateTransactionCut`, `sanic_CHPreviewTransactionCut` y `sanic_CHRemoveFromCut`. Migración limpia: no quedaron referencias huérfanas. |
| `CH-SR-Counter` | Renombrado a `CH-SR-Cashier` (mismo GUID `{f9bee50d-6a75-f011-b4cb-7c1e5248985f}`). No es una baja. |

### Cambios de estado ON/OFF en cloud flows desde el 2026-08-24

Leídos de `<StateCode>`/`<StatusCode>` en cada `.json.data.xml`:

| Flow | Antes | Ahora |
|---|---|---|
| `CH-Flow-Notifyreviewers` | OFF | **ON** |
| `CH-Flow-ProcessXMLFiles` | OFF | **ON** |
| `CH-Flow-Createclientdata` | ON | **OFF (Draft)** |
| `CH-Flow-RFCWatchdog` | ON | **OFF (Draft)** |

> Estos 4 cambios resuelven las 4 discrepancias que el documento arrastraba marcadas como
> "CONFIRMADA 2026-08-25".
