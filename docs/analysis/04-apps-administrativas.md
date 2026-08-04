# Apps administrativas y de soporte — CashHubApps

> **Nota**: este documento cita los fuentes con la ruta original `src/{core,flow,apps}/`, previa a la reestructuración del repo. La ruta actual es `solutions/unpacked/{core,flow,apps}/`.


Análisis de las Canvas Apps y la app de modelo (model-driven) relacionadas con:
consola de validación de correos, digitación/acreditación en IBS Branch, tracker de
depósitos, administración de clientes (con aprobaciones), hub de navegación y
biblioteca de componentes compartidos.

Fuente: YAML desempaquetado en
`/home/gmaker/projects/CashHub/src/apps/CanvasApps/src/`, más
`/home/gmaker/projects/CashHub/src/apps/AppModules/`,
`/home/gmaker/projects/CashHub/src/apps/AppModuleSiteMaps/` y
`/home/gmaker/projects/CashHub/src/apps/WebResources/`.

Convención de lectura: todo lo afirmado abajo está respaldado por código Power Fx,
XML o JSON leído directamente. Donde el código no permite confirmar algo (por
ejemplo, lógica que vive en un Cloud Flow o plugin fuera de este repo), se indica
explícitamente como "no verificable desde estos archivos" en vez de inventarse.

---

## 1. Consola de Validación de Correos — `sanic_chappemailvalidationconsole_f3b93`

Ruta: `src/sanic_chappemailvalidationconsole_f3b93/`

### 1.1 Pantallas

| Pantalla | Propósito |
|---|---|
| `screen CorreosElectronicos` | Bandeja de entrada: lee el buzón compartido de Outlook vía Microsoft Graph, permite ver adjuntos, dar de alta el correo en Dataverse y archivarlo en Outlook una vez tomado. |
| `screen ColaDeProcesamiento` | Núcleo de la app: lista correos pendientes, corre el motor de 23 reglas de validación (VR-01..VR-23) sobre el Excel adjunto, arma la jerarquía Declaración→Bolsas→Depósitos→Cheques y responde al cliente. |
| `screen TestUDF` | Pantalla de QA/desarrollo — corre ~30 casos de prueba unitarios contra la UDF `EvaluateEV`. No toca el flujo de negocio real. |

### 1.2 App.OnStart / App.Formulas

- **`App.Formulas`** define `EvaluateEV(_op, _iv, _bv)`: UDF genérica de comparación que sustenta **todo** el motor de reglas VR-01..VR-23. Soporta operadores `=`/`equal` (con comparación de arrays JSON por multiset, insensible a orden/mayúsculas), `<`, `<=`, `>`, `>=`, `contains`, `begins with`/`starts with`, `ends with`, `empty` (con tolerancia a typos del propio código: "greather than", "empoty").
- **`App.OnStart`**: `varBuzonCorreo` (dirección del buzón compartido, vía `Environment Variable Values`/`Definitions`), `varIDFolderArchivado` (**hardcodeado**, la env var real está deshabilitada según comentario propio del código), `Office365Users.MyProfileV2()`, `varHoy`, `ClearCollect(colExpandMenu, ...)` con el menú de las 2 pantallas de negocio, y `ClearCollect(colValidationRules, Filter('CH-Tbl-ValidationRules', IsActive=true))` — las reglas son config-driven, no hardcodeadas.
- **Detección de rol vía grupos AAD: no existe en esta app.** No hay ninguna referencia a `sanic_CHEVAADGroup_*` ni a `IsInGroup`/`Office365Groups` en todo el paquete.

### 1.3 Tablas Dataverse

| Tabla | Operación |
|---|---|
| `CH-Tbl-Received Emails` (`sanic_CHTblReceivedEmails`) | `Filter` (cola por `EmailReviewStatus`/`EmailReplyStatus = Pendiente`); `Patch` de alta; `Patch` de cierre (`EmailReviewStatus→Aceptado/Denegado`, `EmailReplyStatus→Respondido`) |
| `CH-Tbl-EmailsAttachments` | `Patch` (guardar adjunto descargado); `Filter`/`LookUp` (traer adjunto del correo seleccionado) |
| `CH-Tbl-ValidationRules` | `Filter(IsActive=true)` → `colValidationRules` |
| `CH-Tbl-ValidationRuns` | `Patch` (crear lote de validación); `Patch` (cerrar corrida `RunResult=Pass/Fail`) |
| `CH-Tbl-ValidationResults` | `Patch` (un registro por regla ejecutada, Pass/Fail + duración ms); `Filter` (traer resultados de la corrida) |
| `CH-Tbl-ClientEmails` | `LookUp` (VR-02: remitente debe ser correo de cliente activo) |
| `CH-Tbl-Declarations` | `Patch` (`SubmitForm('form Declaration')`) |
| `CH-Tbl-DeclaredBags` | `Patch` (una por bolsa detectada) |
| `CH-Tbl-DeclaredDeposits` | `Patch` (uno por depósito) |
| `CH-Tbl-DeclaredChecks` | `Patch` (uno por cheque) |
| `CH-Tbl-Clients`, `CH-Tbl-Bags` | Declaradas como datasource pero **sin uso detectado** en las 3 pantallas. |

Los 4 forms encadenados (Declaration→Bags→Deposits→Checks) implementan **rollback manual** en `OnFailure`: si falla un `Patch` de la cadena, se hace `Remove` de todo lo insertado en esa corrida — compensación porque Power Apps no tiene transacciones atómicas nativas entre Patches.

### 1.4 Flujos de Power Automate

1. **`CH-Flow-GetJSONdatafromattachment`** — disparado al seleccionar un correo en la cola; recibe el GUID del adjunto, devuelve `.jsonstring` usado para **previsualización**.
2. **`CH-Flow-ExtractedTablesFromExcel`** — disparado dentro de la regla VR-06, mismo parámetro, devuelve `.datos` usado para la **evaluación real** de reglas. (Función aparentemente redundante con el anterior; no se pudo confirmar la diferencia interna porque la lógica de ambos flows no está en este repo.)
3. **`PowerAppV2->Replytoemail(V3)`** — responde el correo al cliente con el resultado de la validación (HTML armado en la app), pasando el buzón y el ID del mensaje original.

Además hay llamadas directas a **Microsoft Graph** (`Office365Outlook.HttpRequest`, `MoveV2`) para listar correos/adjuntos y archivar, sin pasar por flow.

### 1.5 User journey

1. En "Emails": carga inbox (Graph), selecciona correo, ve adjuntos y cuerpo.
2. "Incluir + Validar" → alta en `CH-Tbl-Received Emails`, descarga adjuntos a `CH-Tbl-EmailsAttachments`, archiva el correo original en Outlook.
3. En "Cola de procesamiento": selecciona el correo pendiente → trae el JSON del Excel para preview.
4. "Crear lote de validación" → crea `CH-Tbl-ValidationRuns`.
5. "Correr todas las validaciones" → ejecuta VR-01..VR-23 en orden (respetando dependencias), persistiendo cada resultado.
6. Si falla alguna regla → `RunResult=Fail` y `EmailReviewStatus=Denegado` automático. Si todas pasan → `RunResult=Pass` y se habilita "Guardar resultados", que crea en cascada Declaration→Bags→Deposits→Checks y marca `EmailReviewStatus=Aceptado`.
7. "Responder correo" → arma HTML de aceptación/rechazo (con motivos de las reglas fallidas) y lo envía; marca `EmailReplyStatus=Respondido`.

### 1.6 Motor de reglas (VR-01 a VR-23)

Cubren: cantidad/extensión/tamaño de adjuntos (VR-01, VR-04, VR-05); remitente autorizado y activo (VR-02); patrón de asunto (VR-03); Excel parseable como JSON (VR-06); columnas esperadas en hojas de depósitos/cheques (VR-07, VR-16, VR-19); filas sin bolsa/cuenta (VR-08 a VR-11); **cuadre de montos cheques vs. depósitos por moneda** (VR-12); una cuenta no puede declarar 2 monedas (VR-13); cheques duplicados (VR-14); campos obligatorios vacíos (VR-15, VR-20); tipos numéricos inválidos (VR-17); moneda debe ser NIO/USD/EUR (VR-18); montos negativos o cero (VR-21, VR-22); cheques huérfanos sin depósito asociado (VR-23).

**Hallazgos/rarezas**:
- `varIDFolderArchivado` está hardcodeado pese a que el código indica que debería salir de una Environment Variable (deshabilitada).
- No hay control de acceso por rol/grupo AAD dentro de la app — a diferencia de otras apps del set.
- Dos flows (`GetJSONdatafromattachment` y `ExtractedTablesFromExcel`) que reciben el mismo parámetro y aparentan la misma función; podría ser deuda técnica o tener alguna diferencia interna no visible desde Canvas Apps.

---

## 2. Digitación — `sanic_chappdigitization_278aa`

Ruta: `src/sanic_chappdigitization_278aa/`

### 2.1 Pantallas/vistas

Una sola pantalla física (`Screen1`, sin ningún `Navigate()` en todo el código), navegación interna vía `TabList1` con 3 pestañas:

| Pestaña | Propósito |
|---|---|
| **Tx sin lote** | Transacciones de efectivo puro aún no agrupadas en lote — punto de partida del "corte" para carga masiva a IBS Branch. |
| **Tx Manuales** | Transacciones que traen cheques (BAC u otros bancos) y **no** pueden ir por carga masiva — se digitan una a una directo en IBS Branch. |
| **Lotes** | Ciclo de vida del lote (Generado→Ingresado→Aplicado) y descarga del archivo de carga masiva. |

Hay además un `Form1` oculto usado solo como mecanismo programático para crear el lote ("Realizar corte").

### 2.2 App.OnStart / App.Formulas

**No existen.** `App.fx.yaml` tiene 11 líneas, solo configuración de `appinfo` (BackEnabled, Theme, hostControl). No hay `OnStart`, `OnVisible`, `Office365Users`, `User()` ni ninguna referencia a `sanic_CHEVAADGroup_*` en toda la app. **No hay detección de rol ni precarga** — el control de acceso, de existir, se resuelve fuera de esta Canvas App (seguridad Dataverse / asignación de app en el entorno).

### 2.3 Tablas Dataverse

Ninguna escritura usa `Patch(` directo — todo pasa por `SubmitForm()`/`NewForm()`.

| Tabla | Operación |
|---|---|
| `CH-Tbl-Transactions` (`sanic_chtbltransactions`) | Lectura en grillas de las 3 pestañas; escritura individual vía `SubmitForm(Form2)` (`sanic_transactionstatus = Aplicada`); escritura masiva `UpdateIf(...)` asigna el lote nuevo a las transacciones pendientes al "hacer el corte". |
| `CH-Tbl-TransactionBatches` (`sanic_transactionbatches`) | Lectura (grilla de lotes); creación (`NewForm(Form1); SubmitForm(Form1)`, autonumerado `TB-{SEQNUM:4}`); cambio de estado vía `SubmitForm(Form2_1)` (`sanic_batchstatus`). |
| `CH-Tbl-Checks` (`sanic_chtblchecks`) | Solo lectura informativa (`EnableEditing:"no"`), filtrada por el depósito relacionado. |
| `CH-Tbl-DepositsRecountings` | No se accede directamente; se filtra vía relación `RelatedDepositRecounting` desde `Transactions`. |

### 2.4 Flujo de Power Automate

Uno solo: **`CH-Flow-DownloadData`**. Contrato: recibe `text` (JSON de las transacciones del lote) y `text_1` (nombre del archivo); devuelve `archivosalida` (archivo resultante). Se invoca desde el botón "Descargar lote":
```
'CH-Flow-DownloadData'.Run(JSON(colTx, JSONFormat.IncludeBinaryData), varLoteSelected.Name).archivosalida
```
Depende de una conexión OneDrive for Business. **La lógica interna del flow (armado real del CSV) no está en este repo** — solo se puede confirmar el contrato de entrada/salida desde Canvas Apps.

### 2.5 User journey

1. "Tx sin lote": el digitador ve las transacciones de efectivo puro pendientes de agrupar.
2. "Realizar corte" → crea un `TransactionBatches` nuevo y asigna automáticamente todas las transacciones sin lote y sin cheques (usando un timestamp capturado antes del submit para evitar condiciones de carrera).
3. "Tx Manuales": transacciones excluidas del corte por traer cheques BAC/otro banco — se digitan una por una en IBS Branch y se marcan "Aplicado" en la app (`TransactionStatus=Aplicada`), avanzando automáticamente a la siguiente pendiente.
4. "Lotes": selecciona un lote, ve totales por moneda (NIO/USD/EUR) y detalle.
5. "Descargar lote" → arma `colTx` con el layout IBS Branch, llama al flow, descarga el archivo.
6. Sube el archivo manualmente a IBS Branch (carga masiva, proceso externo a la app).
7. "Marcar como Ingresado" (visible solo si `BatchStatus=Generado`) tras subir el archivo.
8. "Marcar como Aplicado" (visible solo si `BatchStatus=Ingresado`) tras confirmación del core — cierra el ciclo.

### 2.6 Generación del archivo para carga masiva en IBS Branch

Botón "Descargar lote" arma la colección `colTx` con este layout (columnas fijas del contrato IBS):

```
TRNCODE: 8004                       (fijo, código de operación)
CUENTA: cuenta a 9 dígitos con ceros a la izquierda
MONTOTOTAL: total, 2 decimales
REFERENCIA / DEPOSITID: TransactionID (duplicado en ambos campos)
DEPOSITANTE: TransactionReference o, si vacío, TransactionID
MONEDA: NIO→"COR", USD→"USD", EUR→"EUR"
DISPONIBILIDADINMEDIATA: "0"        (fijo)
EFECTIVO: monto en efectivo, 2 decimales
MANIFIESTO: 1                       (fijo)
TIPOID: "0000000000001"             (fijo)
NACIONALIDAD: "0000000000558"       (fijo)
NUMEROID1: "4411001991"             (fijo)
NUMEROID2: "003A"                   (fijo)
```

La app **no genera el CSV en sí** — serializa `colTx` a JSON y delega la conversión al formato final al flow `CH-Flow-DownloadData`, que devuelve el archivo listo para descargar. Los campos `DISPONIBILIDADINMEDIATA`, `MANIFIESTO`, `TIPOID`, `NACIONALIDAD`, `NUMEROID1`, `NUMEROID2` están **hardcodeados con valores fijos**, lo que sugiere que el lote se carga bajo una identidad genérica/de agencia en IBS Branch, no bajo la identidad de cada depositante — hallazgo a confirmar con negocio, no inferible con certeza solo del código.

**Cómo se marca "acreditado"**: no hay confirmación automática desde IBS Branch — es 100% manual/declarativo. A nivel de lote: `sanic_batchstatus` avanza `Generado→Ingresado→Aplicado` por clicks del digitador. A nivel de transacción individual (pestaña Manuales): `sanic_transactionstatus=Aplicada`.

**Hallazgo colateral de esquema**: la fórmula usa `RelatedDepositRecounting.OtherLocaCheckAmount` (sin "l"); se confirmó que ese es el nombre real de la columna en Dataverse (`sanic_otherlocacheckamount`, display name con el typo) — el typo está en el esquema de `DepositsRecountings`, no en la fórmula de la app.

---

## 3. Tracker de Depósitos — `sanic_chappdepositstracker_2b2c3`

Ruta: `src/sanic_chappdepositstracker_2b2c3/`

### 3.1 Pantallas

Una sola pantalla (`screen TransactionTracker`, ~7460 líneas), sin `Navigate()`; la navegación es por visibilidad condicional según `gvarSelectedDetail`:

- **Search**: dropdown de tipo de identificador (`"Lote de recepcion"`, `"Planilla"`, `"Tula"`) + búsqueda.
- **Reception**: detalle del lote de recepción y tabla de tulas asociadas.
- **Bag**: detalle de una tula, con tabs "General" (datos de sesión de recuento, cajero, cliente, comparación Declarado vs. Recontado por moneda) y "Por depósito" (grid de depósitos con desglose Efectivo/Recontado).
- **Checks**: cheques asociados a la tula (monto, moneda, número, status, tipo).
- Botones **Declaration**, **Deposits**, **Timeline** existen en la UI pero están **deshabilitados** (`Visible:false`, `OnSelect:false`) — funcionalidad planeada, no implementada.

### 3.2 App.OnStart / App.Formulas

**No existen.** `App.fx.yaml` de 11 líneas, sin `OnStart` ni `App.Formulas`. Sin referencias a `sanic_CHEVAADGroup_*` (0 resultados). Las variables globales se crean implícitamente al usarse (no hay inicialización explícita ni precarga).

### 3.3 Tablas Dataverse (todas de solo lectura — **cero `Patch` en todo el código**)

| Tabla | Operación |
|---|---|
| `CH-Tbl-ReceptionBatches` | `LookUp` por Name (búsqueda "Lote de recepción") |
| `CH-Tbl-Receptions` | `LookUp` por ReceiptID (búsqueda "Planilla"); `Filter` por lote |
| `CH-Tbl-Bags` | `LookUp` por BagID (búsqueda "Tula"); `Filter` por recepción |
| `CH-Tbl-DepositsRecountings` | `Filter`+`SortByColumns` → `colDeposits` (única colección de toda la app) |
| `CH-Tbl-Checks` | `Filter` por tula relacionada |
| `CH-Tbl-DeclaredBags`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-Clients`, `CH-Tbl-RecountingSessions`, `Users` | Acceso indirecto vía lookups relacionados (comparación declarado vs. recontado, nombre de cliente, cajero) |

### 3.4 Flujos de Power Automate

**Ninguno.** `Connections.json` vacío, cero `.Run(` en el código. App puramente de consulta a Dataverse.

### 3.5 User journey

App de **solo consulta/auditoría** sin ninguna capacidad de escritura ni navegación a flows: buscar por Lote/Planilla/Tula → ver detalle de recepción → seleccionar tula → ver comparación declarado vs. recontado por depósito y moneda → ver cheques asociados.

### 3.6 Estados visibles

La app **no filtra por estado explícitamente** en Power Fx — solo muestra las columnas de status tal cual vienen del registro. Los valores de choice (definidos en el modelo Dataverse, no en esta app) son:

- **`sanic_BagStatus`**: Recibida en esclusa, Almacenada en boveda, Asignada a cajero, Recontada, Pendiente de revisión por supervisor (recuento), Pendiente de revisión por supervisor (esclusa).
- **`sanic_CheckStatus`**: Recontado, Digitado, Rebotado, Enviado a digitación.
- **`sanic_CheckTypes`**: BAC Local, Otros Local.
- **`CH-Ch-Currency`**: NIO, USD, EUR.

No se encontró ningún estado de "acreditado en IBS Branch" ni "rechazado" a nivel de depósito dentro de esta app — si existe, vive en otra tabla/app no cubierta acá.

---

## 4. Administración de Clientes — `sanic_chappclientadministration_fd97f`

Ruta: `src/sanic_chappclientadministration_fd97f/`

### 4.1 Pantallas

| Pantalla | Propósito |
|---|---|
| `screen Splash` | Arranque: si viene `Param("RFC")` en la URL, resuelve la solicitud de cambio y navega directo a `ChangeRequestDetails`; si no, navega a `Clients`. |
| `screen Clients` (~14.000 líneas) | Listado/CRUD de clientes (`CH-Tbl-Clients`). Crear/Editar/Desactivar/Activar generan una `CH-Tbl-ClientDataChangeRequest` — **nunca** escribe directo sobre `CH-Tbl-Clients`. |
| `screen ClientInformation` (~4.600 líneas) | Ficha de cliente: datos generales, sub-grillas de Sucursales (`CH-Tbl-ClientBranches`) y Correos autorizados (`CH-Tbl-ClientEmails`). Igual que en Clients, cada alta/edición arma una solicitud de cambio. |
| `screen RFC` (~11.700 líneas) | **"RFC" = "Request For Change"**, no identificación fiscal — listado de solicitudes de cambio (`CH-Tbl-ClientDataChangeRequests`) con toggle pendientes/todas. |
| `screen ChangeRequestDetails` | Pantalla central de aprobación: cabecera de la solicitud + líneas de cambio (NewValue/OldValue), botones Aprobar/Rechazar, diálogos de envío, resolución y cancelación. |

### 4.2 App.OnStart / App.Formulas

```
Set(gvarSideMenu, ...)
Clear(colClientDataPermissions);
ForAll('CH-Tbl-ClientDataChangePermissions' As Permission,
    If(!IsBlank(LookUp(Office365Groups.ListGroupMembers(Permission.AADGroupID).value, userPrincipalName = User().Email)),
       Collect(colClientDataPermissions, Permission)));
Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email));
If(!IsBlank(Param("RFC")), ... precarga gvarSelectedChangeRequest/gvarSelectedClient)
```

- **Detección de rol**: por cada fila de `CH-Tbl-ClientDataChangePermissions` (que tiene un `AADGroupID` GUID), verifica pertenencia vía `Office365Groups.ListGroupMembers`. **No se encontró la cadena `sanic_CHEVAADGroup_*`** en esta app — el vínculo es por GUID, no por nombre de grupo.
- **Rol solicitante vs. aprobador**: campo `sanic_type` de `ClientDataChangePermissions` con valores `Contributor` (159460000) / `Approver` (159460001), segmentado además por tabla objetivo (`sanic_chtblclient`, `sanic_chtblclientbranch`, `sanic_chtblclientemail`) — un usuario puede ser Approver de Cliente pero no de Sucursal.

### 4.3 Tablas Dataverse

| Tabla | Operación |
|---|---|
| `CH-Tbl-Clients` | Solo lectura (grid + `Refresh`) |
| `CH-Tbl-ClientBranches` | Solo lectura |
| `CH-Tbl-ClientEmails` | Solo lectura |
| **`CH-Tbl-ClientDataChangeRequests`** (`sanic_chtblclientdatachangerequest`) | `Patch` de creación (Draft/Submitted); `Patch` de actualización en Enviar/Aprobar/Rechazar/Cancelar (`ChangeStatus`, `SolvedBy`, `SolvedOn`, `ReasonForDecision`, `ApplyOn`, `ApplyNotes`) |
| **`CH-Tbl-ClientDataChangeRequestLines`** | `Patch` de creación (una línea por campo modificado); `Update` masivo de `ChangeStatus` de todas las líneas al resolver la solicitud |
| `CH-Tbl-ClientDataChangePermissions` | Solo lectura, leída íntegra en `OnStart` |
| `CH-Tbl-ClientTableLogicalFieldNames` | Solo lectura — catálogo de campos editables por tabla objetivo, con su `DataType` (Text/Number/Date/Money/OptionSet/TwoOptions) para renderizado dinámico del formulario de cambio |
| `CH-Tbl-ClientDataChangesAuditTrails`, `CH-Tbl-ClientDepositHistories` | Conectadas como datasource pero **sin uso detectado** en las 5 pantallas |

### 4.4 Flujos de Power Automate

**Ninguno** (`0 .Run(` en toda la app). Único conector externo: Office 365 Groups (para membresía AAD).

### 4.5 User journey

**Solicitante (Contributor)**: crea/edita cliente, sucursal o email desde un panel → se genera 1 `ClientDataChangeRequest` + N `ClientDataChangeRequestLines` (una por campo) en `Submitted` → puede revisar/enviar/cancelar en `ChangeRequestDetails`. Visibilidad de acciones condicionada a `colClientDataPermissions` con `Type=Contributor`.

**Aprobador (Approver)**: entra por "Solicitudes de cambio" (`screen RFC`), ve **todas** las solicitudes (no solo las propias), abre el detalle. Botones Aprobar/Rechazar visibles solo si `ChangeStatus=Submited` **y** el usuario tiene permiso `Approver` para esa tabla objetivo. Exige comentario obligatorio al resolver.

### 4.6 Lógica de aprobación (clave)

**Estados de la solicitud** (`ChangeStatus`, cabecera): `Draft` → `Submited` → `Approved` | `Rejected` → `Applied` | `Failed`; también `Canceled`.
**Estados de línea** (independiente): `Draft` → `Submitted` → `Approved` | `Denied` → `Applied` | `Failed` | `Canceled`.
**Tipo de solicitud**: `Update`, `Deactivate`, `Create`, `Activate`.

**Campos modificables vía solicitud**: Cliente (Nombre, CIFBCO, CIFCOM), Sucursal (Sucursal, Cliente), Correo autorizado (Correo, Cliente, "Notificar siempre"), más cualquier campo marcado `IsAllowed=true` en el catálogo de campos lógicos.

**Flujo de decisión**: exige comentario → actualiza el `ChangeStatus` de **todas** las líneas de la solicitud al mismo valor (aprobación a nivel de solicitud completa, no campo por campo, aunque el modelo de datos soporta estado por línea) → `Patch` de la cabecera con `SolvedBy`, `SolvedOn`, `ReasonForDecision`.

**Detección de conflictos**: antes de reenviar a `Submitted`, se busca si ya existe otra línea con el mismo `Field Definition` en `Submitted`/`Approved` para evitar ediciones concurrentes sobre el mismo campo; si hay conflicto, bloquea el envío.

**Hallazgo importante**: la app **nunca aplica el cambio real** — jamás hace `Patch` sobre `CH-Tbl-Clients`/`ClientBranches`/`ClientEmails` tras una aprobación, ni setea `ApplyOn`/`ApplyNotes`, ni mueve el estado a `Applied`. Deja la solicitud en `Approved` y termina ahí. La aplicación real del dato al registro maestro (y el paso a `Applied`/`Failed`) debe ocurrir por un mecanismo externo (probablemente un flow o plugin de Dataverse disparado por el cambio de estado), **no incluido en este paquete de Canvas App**.

---

## 5. Hub — `sanic_chapphub_2038c`

Ruta: `src/sanic_chapphub_2038c/` (nombre confirmado en `CanvasManifest.json`: "CH-App-Hub")

### 5.1 App.OnStart — detección de rol (data-driven)

```
ClearCollect(colAppData,
    Filter(Filter('CH-Tbl-AppCatalogs', !IsBlank('AAD Group Id') && Enabled),
        !IsBlank(LookUp(Office365Groups.ListGroupMembers(Trim('AAD Group Id')).value,
                         userPrincipalName = User().Email))));
Collect(colAppData, Filter('CH-Tbl-AppCatalogs', IsBlank('AAD Group Id') && Enabled));
```

El catálogo de apps vive en la tabla Dataverse `CH-Tbl-AppCatalogs` (`sanic_chtblappcatalog`), con columna `AAD Group Id`. Por cada fila habilitada con grupo asignado, se valida membresía vía `Office365Groups.ListGroupMembers`; las filas sin grupo asignado (pero habilitadas) se muestran siempre. Esta es la app donde probablemente residen (como dato, no como código) los GUIDs de los grupos `sanic_CHEVAADGroup_*` — pero el nombre del grupo no aparece en el código Power Fx.

**Hallazgo**: la galería principal (`Gallery1`) tiene `Items:='CH-Tbl-AppCatalogs'` — consume la **tabla completa sin el filtro de grupo**, no la colección `colAppData` que se arma en `OnStart`. `colAppData` se construye pero no se usa en ningún control detectado — podría ser un remanente/trabajo inconcluso.

### 5.2 Navegación

Una sola pantalla (`Screen1`). Cada tarjeta de `Gallery1` lanza `Launch(ThisItem.AppURL, {}, LaunchTarget.Replace)` — reemplaza la pestaña actual, comportamiento típico de launcher. No hay `Navigate()` en toda la app.

### 5.3 "Contadores" en pantalla

Los indicadores visuales ("Procesadas: 128", "En revisión: 7", luces de estado) están **hardcodeados dentro de un SVG estático** (con animaciones CSS decorativas) en `HtmlText1`/`Image2` — no están ligados a ninguna fuente de datos Power Fx real. Es puramente ilustrativo, no telemetría.

### 5.4 Uso de componentes compartidos

`ComponentReferences.json` del Hub está **vacío** — el Hub **no usa** `sanic_chclsharedcomponents_85fca` (ni `cmpAppHeader` ni `cmpTheme`).

---

## 6. Biblioteca de Componentes Compartidos — `sanic_chclsharedcomponents_85fca`

Ruta: `src/sanic_chclsharedcomponents_85fca/` (nombre confirmado: "CH-CL-SharedComponents", `DocumentType: ComponentLibrary`)

Expone 2 componentes:

### 6.1 `cmpAppHeader`

Contiene un único control (`Rectangle1`) con `Fill` sin valor y posicionado fuera del área visible práctica (`Y=40` en un contenedor de 64px de alto). `CustomProperties: []` en el manifiesto — **no expone ninguna propiedad de entrada ni de salida**. Sin logo, sin nombre de usuario, sin botón de logout, sin lógica de AAD. **Es un placeholder/esqueleto no terminado**, no un header funcional pese a su nombre.

### 6.2 `cmpTheme`

Expone **una única propiedad de salida `Theme`** (tipo `Record`, `Hidden:true`) con tokens de diseño fijos:
- Colores (paleta morada: Primary `#5B4BDF`, Danger, Warn, Success, Text, Bg, Surface, Border).
- Spacing (XS–XXL), Radius (S/M/L), Font (familia Segoe UI + tamaños H1/H2/Body/Small), Z-index (Overlay/Toast/Modal).

No tiene inputs, no soporta claro/oscuro dinámico (un único set de valores fijo) ni lógica condicional — es un proveedor estático de design tokens, no un selector de tema.

**Ninguno de los dos componentes contiene lógica de negocio** (sin AAD, sin fechas, sin moneda). No se pudo confirmar si otras apps del ecosistema (recepción, recuento, supervisor — fuera de este alcance) sí consumen esta librería.

---

## 7. Componentes mencionados sin profundizar

- **`sanic_chappclientadministrationdefaultcommand13815`**: Canvas App auxiliar tipo "Command Library" asociada a la app model-driven de Client Administration (referenciada en `AppModule.xml` como `sanic_chappclientadministrationdefaultcommandlibrary_1dd0b`). Su `App.fx.yaml` es un esqueleto mínimo (solo `appinfo`/`hostControl`), consistente con ser una librería de comandos del ribbon en vez de una app de negocio con pantallas propias.
- **`cat_powercatcomponentlibrary_0be3a`**: librería de componentes del PowerCAT Component Library estándar (paquete de componentes reutilizables de la comunidad Power Platform: `ExpandMenu`, `Header`, `Material`, `Dialog`, `Panel`), usada como dependencia de UI por otras apps del set (por ejemplo, el menú expandible `ExpandMenu` referenciado desde `colExpandMenu` en varias apps analizadas). No es código propio de negocio de CashHub.

---

## 8. App Model-Driven "Client Administration" (Dataverse) y WebResources

Ruta: `AppModules/sanic_ChAppClientAdministration/AppModule.xml`, `AppModuleSiteMaps/sanic_ChAppClientAdministration/AppModuleSiteMap.xml`, `WebResources/sanic_chwebrproposechange.js*`.

### 8.1 Entidades incluidas (`AppModule.xml`)

```
sanic_chtblclient                        (clientes)
sanic_chtblclientbranch                  (sucursales)
sanic_chtblclientdatachangerequest       (solicitud de cambio — cabecera)
sanic_chtblclientdatachangerequestline   (solicitud de cambio — líneas/bitácora)
sanic_chtblclientdeposithistory          (cuentas a depositar / historial)
sanic_chtblclientemail                   (correos autorizados)
```

Hay 5 componentes `type="26"` (forms/vistas) referenciados solo por GUID, sin `schemaName` — no se pudo determinar cuáles son sin el `Customizations.xml` de la solución. Dos `AppModuleRoleMaps` restringen la app a roles de seguridad específicos (solo por GUID).

### 8.2 Menú de navegación (`AppModuleSiteMap.xml`)

| Área | Grupo | SubÁrea | Entidad |
|---|---|---|---|
| Operations | Entities | Clientes | `sanic_chtblclient` |
| Operations | Entities | Sucursales | `sanic_chtblclientbranch` |
| Operations | Entities | Cuentas a depositar | `sanic_chtblclientdeposithistory` |
| Operations | Entities | Emails | `sanic_chtblclientemail` |
| Governance | Gebernanza (sic) | Solicitud de cambios | `sanic_chtblclientdatachangerequest` |
| Governance | Gebernanza (sic) | Bitácora de cambios | `sanic_chtblclientdatachangerequestline` |
| Reviews | Revision | Pending reviews | `sanic_chtblclientdatachangerequest` |
| Reviews | Revision | Applied changes | `sanic_chtblclientdatachangerequest` |
| Reports | Reports | (sin subáreas) | — |

Ambas subáreas de "Reviews" apuntan a la **misma entidad**; el Site Map no especifica `savedquery`, así que la diferenciación pendientes/aplicados dependería de una vista default o de personalización no capturada en este archivo.

### 8.3 Script `sanic_chwebrproposechange.js`

Función `ch_openProposeChange(primaryControl, entityLogicalName, typeValue, draftStatusValue)`, pensada para colgarse de un **botón de la Command Bar** (ribbon), no de un evento de formulario:

1. Obtiene el `recordId` desde el form context o desde la fila seleccionada de una grilla.
2. Si no hay registro seleccionado, corta con `openAlertDialog("No record selected.")` — es la única validación del script.
3. Abre un **Quick Create** de `sanic_chtblclientdatachangerequest` (`useQuickCreateForm:true`) prellenado con:
   - `sanic_targettable` = entidad origen del registro.
   - `sanic_targetrecordid` = GUID del registro a modificar.
   - `sanic_type` = tipo de cambio (parámetro recibido, p.ej. Update=100000000).
   - `sanic_changestatus` = estado inicial (parámetro recibido, p.ej. Draft=100000000).
4. `Xrm.Navigation.openForm(...)`. Manejo de error con `try/catch` + `openAlertDialog`.

**El script no crea el registro directamente** (no hay `Xrm.WebApi.createRecord` ni Custom API) — solo abre el Quick Create prellenado; el usuario completa y guarda. No contiene lógica de aprobación (eso vive en otra parte del sistema, no incluida en estos archivos).

### 8.4 Relación con "ClientDataChangeRequest"

Confirmado en los 3 artefactos: el nombre lógico exacto es **`sanic_chtblclientdatachangerequest`** (cabecera) y **`sanic_chtblclientdatachangerequestline`** (detalle) — coincide exactamente con las tablas usadas por la Canvas App de administración de clientes (sección 4), confirmando que ambas superficies (model-driven y canvas) operan sobre el mismo modelo de aprobación de cambios.

---

## 9. Síntesis de hallazgos y rarezas transversales

1. **Ninguna de las 4 Canvas Apps operativas** (Digitación, Tracker, Consola de Emails, Administración de Clientes) implementa detección de rol vía grupos AAD con el patrón `sanic_CHEVAADGroup_*` dentro de su propio código Power Fx — el único lugar donde aparece ese mecanismo (por GUID, vía `Office365Groups.ListGroupMembers`) es el **Hub** (catálogo de apps) y **Administración de Clientes** (permisos Contributor/Approver).
2. **La aprobación de cambios de cliente no se cierra dentro de la Canvas App ni del script del web resource**: ambas dejan la solicitud en `Approved`/`Submitted` pero la escritura real sobre `CH-Tbl-Clients`/`ClientBranches`/`ClientEmails` y el paso a `Applied` ocurre en un mecanismo no incluido en este repo (flow o plugin).
3. **La generación del archivo de carga masiva para IBS Branch delega en un Cloud Flow** (`CH-Flow-DownloadData`); la Canvas App de Digitación solo arma el JSON con el layout de columnas (varias hardcodeadas: `TRNCODE=8004`, identidad genérica de depositante) y descarga el resultado — el formato final (CSV, ancho fijo, etc.) no es verificable desde este repo.
4. El **Hub tiene contadores puramente decorativos** (SVG hardcodeado) sin conexión a datos reales, y **no usa** la librería de componentes compartidos.
5. La **librería de componentes compartidos está incompleta**: `cmpAppHeader` es un placeholder sin propiedades ni contenido funcional, y `cmpTheme` es un proveedor estático de un único tema (no dinámico claro/oscuro).
6. La pantalla "RFC" de Administración de Clientes es **"Request For Change"**, no un dato de identificación fiscal — nomenclatura que puede confundir sin este análisis.
7. Se detectó un **typo de esquema** (`OtherLocaCheckAmount` sin "l") en la tabla `CH-Tbl-DepositsRecountings`, correctamente respetado por las apps que la consumen.
8. La app de **Tracker de Depósitos es de solo lectura** (cero `Patch`, cero flows) — funciona puramente como consola de auditoría/trazabilidad.
