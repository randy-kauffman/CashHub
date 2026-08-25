# Conexiones y flujos — CashHubFlow

Fuente: `solutions/unpacked/flow/Workflows/*.json` (+ `.json.data.xml` de metadata).
19 flujos (17 archivos `.json` con prefijo `CH-Flow-` + `Ch-Flow-VR02`, con casing
distinto — ver [convenciones-nomenclatura.md](convenciones-nomenclatura.md) — +
`ValidationRulesOrchestrator`). Todos son Cloud Flows (`Category=5`).

## 1. Hallazgo crítico: `IngestEmails` está desactivado

Confirmado en `CH-Flow-IngestEmails-4FDD7239-309E-F011-BBD2-7CED8D1ED971.json.data.xml`:

```xml
<StateCode>0</StateCode>
<StatusCode>1</StatusCode>
```

Es decir **Draft/Off**. Es el único de los 19 flujos con este estado — todos los
demás verificados tienen `StateCode=1` (Activado). El flujo que dispara toda la
cadena (recepción de correo del cliente) está apagado en este export. Si esto
refleja el ambiente real, el sistema no está procesando correos entrantes.

## 2. Inventario de los 19 flujos

| # | Flujo | Trigger | Estado | Conectores principales | Propósito |
|---|---|---|---|---|---|
| 1 | `CH-Flow-IngestEmails` | Automatizado — `SharedMailboxOnNewEmailV2`, polling 1 min | **Draft (StateCode=0)** | Office 365 Outlook, Dataverse | Detecta duplicados por `internetMessageId` y crea `ReceivedEmails` + `EmailsAttachments`. |
| 2 | `CH-Flow-ExtractedTablesFromExcel` | Instantáneo `PowerAppV2` (child flow) | Activado | Dataverse, OneDrive for Business, Excel Online Business | Descarga el Excel adjunto, lo sube a OneDrive `/tmp`, lee todas las tablas y arma un JSON consolidado en `sanic_jsondata`. |
| 3 | `CH-Flow-GetJSONdatafromattachment` | Instantáneo `PowerAppV2` | Activado | Dataverse | Descarga `sanic_jsondata` y lo devuelve decodificado de base64. |
| 4 | `CH-Flow-ValidationRulesOrchestrator` | Instantáneo `PowerAppV2` | Activado (pero **stub vacío**) | Ninguno | `Switch` sobre nombre de regla con `case "VR01": {actions:{}}` y `default: {actions:{}}` — literalmente vacío, no responde nada. |
| 5 | `CH-Flow-VR01` | Instantáneo `Button` (child flow) | Activado | Ninguno | `equals(number, int(text))` — valida cantidad de adjuntos. |
| 6 | `Ch-Flow-VR02` (casing distinto) | Instantáneo `Button` | Activado (pero **no implementa lógica**) | Ninguno | Único `Response` incondicional con `validationresult: "Pass"` — sin `If`, siempre Pass. |
| 7 | `CH-Flow-VR03` | Instantáneo `Button` | Activado | Ninguno | `equals(startsWith(toLower(text), toLower(text_1)), true)` — asunto del correo. |
| 8 | `CH-Flow-VR04` | Instantáneo `Button` | Activado (pero **bug**) | Ninguno | `Condition` con `equals(toLower(text_1), toLower(text))`; rama `then` y rama `else` responden **ambas** `"Pass"` — confirmado en el JSON, la validación de extensión nunca puede fallar. |
| 9 | `CH-Flow-VR05` | Instantáneo `Button` | Activado | Ninguno | `lessOrEquals(number, int(text))` — tamaño de adjunto. |
| 10 | `CH-Flow-Replytoclientemail` | Instantáneo `PowerAppV2` | Activado | Office 365 Outlook (`runtimeSource: invoker`) | `ReplyToV3` sobre el correo original. |
| 11 | `CH-Flow-ProcessXMLFiles` | Instantáneo `PowerAppV2` | Activado | OneDrive for Business, Dataverse | Procesa XML de máquina contadora, crea/actualiza `XMLReading`/`XMLReadingDetail`. |
| 12 | `CH-Flow-DownloadData` | Instantáneo `PowerAppV2` | Activado | OneDrive for Business | Genera CSV desde datos recibidos, sube a OneDrive, devuelve link. |
| 13 | `CH-Flow-Activaterow` | Webhook Dataverse (RFC `Approved` + tipo `Activate`) | **RETIRADO (2026-08-25)** | Dataverse | Setea `sanic_recordstatus = Active` en Client/ClientBranch/ClientEmail según tabla objetivo. Reemplazado por la Custom API `sanic_CHResolveChangeRequest`. |
| 14 | `CH-Flow-Deactivaterow` | Webhook Dataverse (RFC `Approved` + tipo `Deactivate`) | **RETIRADO (2026-08-25)** | Dataverse | Igual que `Activaterow` pero setea `Inactive`. Reemplazado por la misma Custom API. |
| 15 | `CH-Flow-Applyclientdatachanges` | Webhook Dataverse (RFC `Approved` + tipo `Update`) | **RETIRADO (2026-08-25)** | Dataverse | Aplica cada línea (`CRL`) campo por campo con `Switch` por tipo de dato. Reemplazado por la misma Custom API (un solo `Update` por RFC, no uno por campo). |
| 16 | `CH-Flow-Createclientdata` | Webhook Dataverse (RFC `Approved` + tipo `Create`) | **RETIRADO (2026-08-25)** | Dataverse | Crea un registro nuevo acumulando todas las propiedades de las líneas del RFC. Reemplazado por la misma Custom API. |
| 17 | `CH-Flow-Notifyreviewers` | Webhook Dataverse (RFC `Submitted`) | Activado — **vigente, sin cambios** | Dataverse (x2 conn. refs.), Teams | Adaptive Card a Teams para los aprobadores; crea el chat si no existe. Sigue escuchando exactamente la misma transición que produce la app v2. |
| 18 | `CH-Flow-NotifywhenRFCimplementationfinish` | Webhook Dataverse (RFC `Failed`/`Applied`/`Rejected`) | Activado — **vigente, sin cambios** | Dataverse, Teams | Mensaje directo por Teams al creador y al aprobador con el resultado final. Desde la migración, es la Custom API `sanic_CHResolveChangeRequest` la que escribe `Applied`/`Rejected` (síncrono); este flujo sigue disparando igual porque ve la misma transición de estado. |
| 19 | `CH-Flow-CreateaTeamschatgroupbasedonOffice365Group` | Instantáneo `Button` (child flow) | Activado — vigente | Office 365 Groups, Teams, Office 365 Users | Crea chat grupal de Teams a partir de un grupo AAD. |
| — | `CH-Flow-RFCWatchdog` (no listado arriba: no aparece en el export de `solutions/unpacked/flow/`, pero existió y se referencia en docs anteriores) | Recurrence 15 min | **RETIRADO (2026-08-25)** | Dataverse | Cerraba RFC huérfanos en `Approved`. Sin objeto desde que la resolución es síncrona: ya no existe la ventana `Approved` que vigilaba. |

**Migración 2026-08-25**: los 4 webhooks de materialización (`Activaterow`,
`Deactivaterow`, `Applyclientdatachanges`, `Createclientdata`) y el
`RFCWatchdog` fueron **retirados** — reemplazados por la Custom API
transaccional `sanic_CHResolveChangeRequest` (`ResolveChangeRequestPlugin`
en `CashHub.Plugins`), invocada directo por la app v2
`CH-App-ClientAdministration v2` al Aprobar/Rechazar. Quedan desactivados
pero no eliminados de la solución — ver
[docs/clientadmin-rebuild/08-integracion-y-deploy.md](../clientadmin-rebuild/08-integracion-y-deploy.md)
y [docs/procesos/01-administracion-datos-clientes.md](../procesos/01-administracion-datos-clientes.md)
§5/§7 para el detalle. Los 2 webhooks de notificación
(`Notifyreviewers`, `NotifywhenRFCimplementationfinish`) siguen **vigentes,
sin cambios de trigger** — ven las mismas transiciones de
`sanic_changestatus` de siempre, solo que ahora `Applied`/`Rejected` los
escribe la Custom API en vez de un flujo aplicador.

Los webhooks activos (`Notifyreviewers`, `NotifywhenRFCimplementationfinish`)
usan `SubscribeWebhookTrigger` sobre `sanic_chtblclientdatachangerequest`,
disparadores nativos de Dataverse (no polling) — igual que los 4 retirados
cuando estaban en uso.

## 3. Cadena real de orquestación

### 3.1 Lo único que SÍ está conectado por `Run a Child Flow`

En todo el paquete de 19 flujos, **una sola** llamada explícita a otro flujo:

```
Notifyreviewers → CreateaTeamschatgroupbasedonOffice365Group
  (workflowReferenceName: "f386eeb1-f2aa-f011-bbd2-000d3a52eba2")
```

Ningún otro flujo invoca a otro flujo vía "Run a Child Flow", HTTP o `Workflow`. En
particular:

- **`IngestEmails` no llama a `ExtractedTablesFromExcel`** — solo crea las filas en
  `ReceivedEmails`/`EmailsAttachments` y termina ahí.
- **`ValidationRulesOrchestrator` no llama a ningún VR0x** — su único `Switch` tiene
  las acciones vacías (confirmado en el JSON, sección 1 más arriba).

### 3.2 Quién llama a los flujos "huérfanos"

`ExtractedTablesFromExcel`, `GetJSONdatafromattachment`, `Replytoclientemail`,
`ProcessXMLFiles`, `DownloadData`, `ValidationRulesOrchestrator` y `VR01/VR03/VR04/VR05`
tienen trigger `PowerAppV2`/`Button` — están diseñados como **child flows invocables
desde una Canvas App**, no como una cadena `runAfter` entre flujos. Confirmado contra
el código real de las apps:

- **Email Validation Console** (`sanic_chappemailvalidationconsole_f3b93`) invoca
  directamente `CH-Flow-GetJSONdatafromattachment` (preview del correo seleccionado) y
  `CH-Flow-ExtractedTablesFromExcel` (dentro de la regla VR-06, para la evaluación
  real) — **la orquestación real de "correo → extracción → validación → respuesta"
  vive en esta app, en Power Fx, con un motor propio de 23 reglas VR-01..VR-23**, no en
  `ValidationRulesOrchestrator` ni en los flujos VR0x de Power Automate.
- **Recounting** (`sanic_chapprecounting_54962`) invoca
  `'CH-Flow-ProcessXMLFiles'.Run(gvarDatosConteo.ID_Sesion, gvarDatosConteo.Serial_Maquina)`.
- **Digitization** (`sanic_chappdigitization_278aa`) invoca
  `'CH-Flow-DownloadData'.Run(JSON(colTx,...), varLoteSelected.Name)`.
- Los flujos `VR01/VR02/VR03/VR04/VR05` y `ValidationRulesOrchestrator` **no
  aparecen invocados desde ninguna app revisada** con `.Run(` — la app Email
  Validation Console implementa su propio motor de reglas en Power Fx
  (`EvaluateEV`), no delega en estos flujos. Son código muerto o un intento anterior
  de mover la validación al backend que quedó incompleto.
- `Replytoclientemail` sí lo invoca Email Validation Console para responder al
  cliente.

### 3.3 Diagrama de orquestación

```mermaid
flowchart TD
    subgraph Ingesta["Pipeline de correo"]
        MAIL["Correo del cliente\n(buzón compartido)"] -->|trigger 1 min\nDESACTIVADO| IE["IngestEmails"]
        IE -->|CreateRecord| REC["ReceivedEmails"]
        IE -->|CreateRecord + binario| ATT["EmailsAttachments"]

        APP["App: Email Validation Console\nmotor propio VR-01..VR-23 en Power Fx"] -->|Run| GJD["GetJSONdatafromattachment"]
        APP -->|Run, dentro de regla VR-06| ETE["ExtractedTablesFromExcel"]
        APP -->|Run| RCE["Replytoclientemail"]
        APP -.no invoca.-> VRO["ValidationRulesOrchestrator (stub vacío)"]
        APP -.no invoca.-> VR0X["VR01..VR05 (huérfanos)"]

        ETE -->|lee| ATT
        ETE -->|Excel Online| XLSX["Excel .xlsx temporal en OneDrive /tmp\n(nunca se borra)"]
        ETE -->|sube JSON a sanic_jsondata| ATT
        GJD -->|lee sanic_jsondata| ATT
        RCE -->|ReplyToV3| MAIL
    end

    subgraph Recuento["Recuento por máquina contadora"]
        APPR["App: Recounting"] -->|Run(SessionID, MachineSerial)| PXF["ProcessXMLFiles"]
        PXF --> XMLR["XMLReading / XMLReadingDetail"]
    end

    subgraph Digit["Digitación / carga masiva IBS Branch"]
        APPD["App: Digitization"] -->|Run(JSON transacciones)| DD["DownloadData"]
        DD --> CSV["Archivo de carga masiva\n(descarga manual, sube a IBS Branch fuera del sistema)"]
    end

    subgraph RFC["Subsistema RFC: cambios de datos de cliente (migrado 2026-08-25)"]
        SUBMIT["RFC Submitted"] -->|webhook, vigente| NR["Notifyreviewers"]
        NR -->|Run a Child Flow, si falta chat| TEAMS_GRP["CreateaTeamschatgroupbasedonOffice365Group"]
        NR -->|Adaptive Card| TEAMS["Microsoft Teams"]
        TEAMS -->|aprobador decide en CH-App-ClientAdministration v2| API["Custom API sanic_CHResolveChangeRequest\n(ResolveChangeRequestPlugin, SÍNCRONA)"]
        API --> DONE["RFC Applied/Rejected\n(directo desde Submitted)"]
        DONE -->|webhook, vigente| NF["NotifywhenRFCimplementationfinish"]
        NF -->|mensaje directo| TEAMS

        CCD["Createclientdata (RETIRADO)"]
        ACDC["Applyclientdatachanges (RETIRADO)"]
        ACT["Activaterow (RETIRADO)"]
        DEACT["Deactivaterow (RETIRADO)"]
        WD["RFCWatchdog (RETIRADO)"]
        API -.reemplaza.-> CCD
        API -.reemplaza.-> ACDC
        API -.reemplaza.-> ACT
        API -.reemplaza.-> DEACT
        API -.hace innecesario.-> WD
    end
```

## 4. Connection references

Confirmadas en los `.json` de flujo (`connectionReferences`):

| Connection reference | Conector | Usada en |
|---|---|---|
| `sanic_CEADCONNOutlook365` | `shared_office365` | `IngestEmails` |
| `sanic_CHCRDataverse` | `shared_commondataserviceforapps` | `IngestEmails`, `ExtractedTablesFromExcel`, `GetJSONdatafromattachment`, `Activaterow`, `Deactivaterow`, `Createclientdata`, `Applyclientdatachanges`, `Notifyreviewers` |
| `sanic_CHCROneDrive` | OneDrive for Business (`runtimeSource: invoker`) | `ExtractedTablesFromExcel` |
| `sanic_CEADCONNExcelOnlineBusiness` | Excel Online Business (`runtimeSource: invoker`) | `ExtractedTablesFromExcel` |
| `new_sharedonedriveforbusiness_a6c30` | OneDrive for Business | `ProcessXMLFiles` |
| `sanic_sharedcommondataserviceforapps_025cb` | Dataverse | `ProcessXMLFiles` |
| `sanic_AEDDConnOneDrive` | OneDrive for Business | `DownloadData` |
| `new_sharedteams_084e4` | Teams | `Notifyreviewers` |
| `sanic_AEDDConnDataverse` | Dataverse (segunda conexión, redundante con `sanic_CHCRDataverse` en el mismo flujo) | `Notifyreviewers` |

**Rareza confirmada**: `Notifyreviewers` tiene dos connection references distintas
apuntando al mismo conector Dataverse dentro del mismo flujo (`sanic_CHCRDataverse` y
`sanic_AEDDConnDataverse`) — posible resto de una fusión de flujos sin reconciliar.

## 5. Flujos stub / bugueados (resumen ejecutivo)

| Flujo | Problema | Evidencia |
|---|---|---|
| `IngestEmails` | Desactivado (`StateCode=0`). Punto de entrada del sistema apagado. | `.json.data.xml` |
| `ValidationRulesOrchestrator` | `Switch` con `case "VR01": {actions:{}}` y `default: {actions:{}}` — no llama a nada, no responde nada. | `.json`, acción `Switch` |
| `Ch-Flow-VR02` | Un único `Response` con `"validationresult": "Pass"` hardcodeado, sin `If` — no valida el dominio/formato del email del remitente pese a ser su propósito declarado. | `.json`, acción `Respond_to_a_Power_App_or_flow` |
| `CH-Flow-VR04` | `Condition` correcto (`equals(toLower(text_1), toLower(text))`) pero **ambas ramas** (`then` y `else`) responden `"Pass"` — la validación de extensión de adjunto nunca puede fallar. | `.json`, acción `Condition` con `actions`/`else.actions` idénticas |
| `ExtractedTablesFromExcel` | El `.xlsx` temporal creado en OneDrive `/tmp` nunca se borra (no hay acción `DeleteFile`). | `.json` |
| `Createclientdata` / `Applyclientdatachanges` **(RETIRADOS 2026-08-25 — referencia histórica)** | El caso `OptionSet` (`159460005`) del `Switch` por tipo de dato tiene acciones vacías en ambos flujos — un RFC que cambie un campo Option Set no actualiza esa columna, sin generar error visible. Reemplazados por `sanic_CHResolveChangeRequest`, cuyo `FieldValueConverter` cubre las 7 conversiones de tipo con tests — el bug no tiene forma de repetirse en el camino vivo. | `.json`, `Switch` de conversión de tipo |
| `Activaterow` **(RETIRADO 2026-08-25 — referencia histórica)** | La acción que pone el registro en estado **Active** se llama literalmente `Actualizar_cliente_a_INACTIVE` (copiada de `Deactivaterow` sin renombrar). El valor seteado (`159460000`=Active) era correcto; el nombre de la acción era engañoso. | `.json`, nombre de acción |

**Manejo de errores**: salvo el patrón "`Response` con `runAfter` en
`Failed`/`Skipped`/`TimedOut`" de `VR01`/`VR03`/`VR04`/`VR05` y
`GetJSONdatafromattachment`, la mayoría de los flujos (`IngestEmails`,
`ExtractedTablesFromExcel`, `Replytoclientemail`, `ProcessXMLFiles`, `DownloadData`)
no tiene ramas de captura de error ni notificación ante fallo de conector.

Ver también: [variables-de-entorno.md](variables-de-entorno.md) para el detalle de
`sanic_CHEVSharedMailboxAddress` hardcodeada en `IngestEmails`;
[modelo-de-datos.md](modelo-de-datos.md) para las tablas que cada flujo lee/escribe.
