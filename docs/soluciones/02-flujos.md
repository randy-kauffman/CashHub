# CashHubFlows — los 16 flujos Power Automate

> Fuente: `solutions/unpacked/flow/` (versión **1.6.5.0**, unmanaged). Estado
> ON/OFF leído de `<StateCode>/<StatusCode>` en cada `.json.data.xml`. Todo lo
> descrito sale del JSON de cada flujo.

La solución contiene 16 workflows, 13 connection references y una réplica de
los 3 option sets globales de Core. Nada más (sin tablas, roles ni web
resources).

## Resumen

| Flujo | Estado | Trigger | Propósito |
|---|---|---|---|
| CH-Flow-IngestEmails | ON | Outlook `SharedMailboxOnNewEmailV2` (1 min) | Detecta correo nuevo en el buzón compartido y delega el MessageId a ProcessEmail. |
| CH-Flow-ProcessEmail | ON | `Request` (hijo, param MessageId) | Ingesta punta a punta de un correo: dedup, registro + adjuntos, Custom API, mover a Procesados/Cuarentena. |
| CH-Flow-MailboxSweep | ON | Recurrence 10 min | Red de seguridad: revalida pendientes, vigila la ingesta, rescata huérfanos del buzón, alerta Teams, heartbeat. |
| CH-Flow-Reply to client email | ON | Webhook Update en ReceivedEmails | Envía la respuesta compuesta por el motor respondiendo el hilo original. |
| CH-Flow-Notify reviewers | **OFF** | Webhook Create RFC (Submitted) | Adaptive Card con el diff del RFC al chat Teams de los aprobadores. |
| CH-Flow-Apply client data changes | **OFF** | Webhook Create RFC (Approved+Update) | Aplica campo a campo las líneas del RFC con conversión por tipo y audit trail. |
| CH-Flow-Create client data | ON | Webhook Create RFC (Approved+Create) | Construye el JSON del registro nuevo desde las líneas y lo crea en la tabla destino. |
| CH-Flow-Activate row | **OFF** | Webhook Update RFC (Approved+Activate) | Pone `recordstatus = Active` en la tabla destino del RFC. |
| CH-Flow-Deactivate row | **OFF** | Webhook Update RFC (Approved+Deactivate) | Ídem con `Inactive`. |
| CH-Flow-Notify when RFC implementation finish | ON | Webhook Update RFC (Rejected/Applied/Failed) | Tarjeta HTML por Teams al creador y al aprobador con deep-link a la app. |
| CH-Flow-RFCWatchdog | ON | Recurrence 15 min | Marca Failed los RFC atascados en Approved más allá del timeout configurado. |
| CH-Flow-Create a Teams chat group… | ON | `Request` (hijo) | Crea un group chat de Teams con los miembros de un grupo M365 y devuelve el chatid. |
| CH-Flow-ExtractedTablesFromExcel | ON | `PowerAppV2` | (Legado) Extrae tablas del xlsx vía OneDrive + Excel Online y guarda JSON en el adjunto. |
| CH-Flow-Get JSON data from attachment | ON | `PowerAppV2` | (Legado) Devuelve el `sanic_jsondata` del adjunto decodificado. |
| CH-Flow-DownloadData | ON | `PowerAppV2` | Convierte transacciones JSON a CSV en OneDrive `/tesoreria/` y devuelve link de descarga. |
| CH-Flow-Process XML Files | **OFF** | `PowerAppV2` | Lee XMLs de la máquina contadora desde OneDrive y crea XMLReading(+Detail). |

Grafo padre→hijo (únicas referencias `workflowReferenceName` del paquete):

```
IngestEmails ──► ProcessEmail ◄── MailboxSweep (acción "Rescatar_correo")
Notify reviewers ──► Create a Teams chat group…
```

Invocables desde canvas apps: ExtractedTablesFromExcel, GetJSONdatafromattachment,
DownloadData (la usa **Digitization**), ProcessXMLFiles (la usa **Recounting**).

---

## A. Pipeline de correo

### CH-Flow-IngestEmails
Trigger `SharedMailboxOnNewEmailV2` cada minuto sobre el buzón de la EV
`sanic_CHEVSharedMailboxAddress`, carpeta Inbox, `splitOn` por mensaje. Una sola
acción: invocar al hijo **ProcessEmail** con el `id` del mensaje. Sin manejo de
error propio, a propósito: si el hijo falla, el correo queda en Inbox y lo
recoge MailboxSweep.

### CH-Flow-ProcessEmail
Hijo por `Request`; entrada única `MessageId`. Camino:

1. `GetEmailV2` con adjuntos.
2. **Dedup**: busca en ReceivedEmails por `sanic_receivedemailinternetmessageid`
   (la clave alterna). Si ya existe, mueve a `Procesados` y termina sin
   reprocesar.
3. Si es nuevo (`scope Try_Ingesta`): crea el ReceivedEmail (estados iniciales
   Pendiente/Auto), crea cada adjunto y sube `sanic_filecontent`, mueve el
   mensaje a `Procesados` y **refresca `sanic_receivedemailoutlookuniqueid`
   con el id nuevo** (Exchange cambia el id al mover — este refresh es el fix
   del bug que dejaba respuestas en Pendiente para siempre), invoca la
   **Custom API `sanic_CHValidateReceivedEmail`** (retry exponencial ×4), lee
   la clasificación del remitente y, si es Sospechoso o No corresponde, mueve
   el mensaje a `Cuarentena` (refrescando el id otra vez).
4. `Catch_Proceso`: ante fallo responde HTTP 500 y `Terminate` Failed
   (`CH_PROCESS_EMAIL_FALLO`); el correo se deja en Inbox a propósito para el
   reintento del barrido.

Tablas: ReceivedEmails (lee/crea/actualiza), EmailsAttachments (crea + file).

### CH-Flow-MailboxSweep
Recurrence 10 min. Cuatro etapas tolerantes a fallo, acumulando notas:

1. **Sanar pendientes**: top 20 correos en Pendiente/Pendiente; si no tienen
   run de validación (o el run es más viejo que el `modifiedon`), re-invoca la
   Custom API.
2. **Vigilar la ingesta**: consulta la tabla `workflows`; si
   CH-Flow-IngestEmails no está activo, lo anota.
3. **Vigilar el buzón**: lee el Inbox (con fallback a "Bandeja de entrada" en
   español); todo correo con más de 5 minutos y sin registro en Dataverse se
   rescata invocando **ProcessEmail**.
4. **Alertar y latir**: si hubo faltantes y `sanic_CHEVOpsTeamsGroupId` no está
   vacío, postea Adaptive Card "⚠ Barrido de ingesta CashHub"; siempre
   actualiza/crea el heartbeat `ingesta-correos` en SystemHeartbeat con el
   resumen (`sanados=… faltantes=… notas=…`) que la Console usa de semáforo.

### CH-Flow-Reply to client email
Webhook Update sobre ReceivedEmails filtrado por
`sanic_replybodyhtml,sanic_emailreplystatus`. Si hay respuesta Pendiente con
cuerpo y `outlookuniqueid`: `ReplyToV3` sobre el hilo original desde el buzón
compartido y marca Respondido (+fecha, `repliedby = "CashHub (automatico)"`).
Si el envío falla, concatena en `sanic_errortext` el aviso "[SISTEMA] No se
pudo enviar la respuesta…" — la Console ofrece reenviar. Nota: el filtro no
incluye `sanic_replysubject`; un cambio solo de asunto no dispara el flujo.

---

## B. RFC de datos de cliente

Los seis flujos comparten el patrón: webhook sobre
`sanic_chtblclientdatachangerequest` filtrado por `sanic_changestatus` +
`sanic_type`, aplicación con audit trail y cierre Applied/Failed.

### CH-Flow-Notify reviewers *(OFF)*
Al Submitted: lee el RFC (+creador), sus líneas y arma una Adaptive Card 1.5
con el diff (campo, valor actual, nuevo) y deep-link `&RFC=<id>` usando la EV
`sanic_CHEVAppUrl_ClientAdmin`. Busca en ClientDataChangePermission el grupo
aprobador (`sanic_type = Approver`) de la tabla destino; si no tiene
`teamsgroupid`, invoca al hijo **Create a Teams chat group…** y lo persiste;
postea la card en el chat.

### CH-Flow-Apply client data changes *(OFF)*
Al Approved+Update: por cada línea, `Switch` sobre el `sanic_datatype` del
metamodelo (Text, Number, Date, Money, LookUp con `@odata.bind`, OptionSet vía
`optionsetmap`, TwoOptions) y `Update` sobre la tabla/registro destino; marca
la línea, crea audit trail y cierra el RFC Applied/Failed con
`sanic_applynotes`. Scope catch global que garantiza el cierre en Failed.

### CH-Flow-Create client data
Al Approved+Create: mismo `Switch` por tipo pero acumulando fragmentos
`"campo":valor` en un array; agrega `sanic_recordstatus = Active`, arma el JSON
y crea el registro en la tabla destino. Audit trail por línea (con o sin
cliente relacionado según sea alta de cliente) y cierre Applied/Failed. Es el
flujo que materializa el alta de remitentes que la Console dispara como RFC.

### CH-Flow-Activate row / CH-Flow-Deactivate row *(ambos OFF)*
Al Approved+Activate/Deactivate: `Switch` por tabla destino (Client,
ClientBranch, ClientEmail, ClientAccount) y `Update` de `sanic_recordstatus` a
Active/Inactive; audit trail y cierre Applied/Failed en ramas paralelas por
éxito/fallo.

### CH-Flow-Notify when RFC implementation finish
Al Rejected/Applied/Failed: lee el RFC expandido (resolutor, creador, cliente),
arma tarjeta HTML (tabla + pill ✅/❌ + link a la app) y la envía por el bot de
Teams al creador y al aprobador.

### CH-Flow-RFCWatchdog
Recurrence 15 min. Lee `sanic_CHEVRfcApplyTimeoutMinutes` (fallback 30), busca
RFCs en Approved con `modifiedon` más viejo que el timeout, los marca Failed
con nota explicativa y deja audit trail. Es el que hoy "cierra" los RFC de
Update/Activate/Deactivate, porque sus aplicadores están apagados.

---

## C. Utilidades para canvas apps

### CH-Flow-DownloadData
`PowerAppV2` con el JSON de transacciones y el nombre de archivo. Proyecta 14
columnas fijas del layout IBS (`TRNCODE, CUENTA, MONTOTOTAL, REFERENCIA,
DEPOSITANTE, MONEDA, DEPOSITID, DISPONIBILIDADINMEDIATA, EFECTIVO, MANIFIESTO,
TIPOID, NACIONALIDAD, NUMEROID1, NUMEROID2`), genera CSV en OneDrive
`/tesoreria/` y devuelve un share link con `?download=1`. No toca Dataverse.
Consumidor: **Digitization** ("Descargar lote").

### CH-Flow-Process XML Files *(OFF)*
`PowerAppV2` con SessionID + serie de máquina. Desactiva lecturas previas de la
sesión, lista OneDrive `/CashHub_XML_Anneke/Pendientes`, filtra archivos
`BPS_<serial>*`, crea XMLReading + un XMLReadingDetail por `<Counter>`
(moneda, denominación, cantidad) y archiva el archivo. Consumidor:
**Recounting** — que hoy llama a un flujo apagado.

### CH-Flow-ExtractedTablesFromExcel y CH-Flow-Get JSON data from attachment
Legado del pipeline pre-plugin: extraer tablas del xlsx vía OneDrive + Excel
Online guardando JSON en `sanic_jsondata`, y devolver ese JSON a la app.
**Ninguna app del repo los invoca hoy** — candidatos a borrar cuando se
confirme que nadie externo los usa.

---

## D. Infraestructura

### CH-Flow-Create a Teams chat group based on Office 365 Group Members
Hijo por `Request` (AADGroup + GroupName): lista los miembros del grupo M365,
excluye al invocador, crea el group chat y devuelve `ChatID`. Lo usa Notify
reviewers para materializar el chat de aprobadores por tabla.

---

## Connection references

Trece en el paquete; las de uso real:

| Connection reference | Conector | Flujos |
|---|---|---|
| `sanic_CHCRDataverse` | Dataverse | casi todos (12) |
| `sanic_AEDDConnDataverse` | Dataverse | Notify reviewers (2 acciones — duplica configuración) |
| `sanic_sharedcommondataserviceforapps_025cb` | Dataverse | Process XML Files |
| `sanic_CEADCONNOutlook365` | Outlook | Reply to client email |
| `new_sharedteams_084e4` | Teams | Notify reviewers, Notify when RFC finish, MailboxSweep, Create Teams chat |
| `sanic_sharedoffice365groups_cff38` / `sanic_sharedoffice365users_dd86c` | O365 Groups / Users | Create Teams chat |
| `sanic_CEADCONNExcelOnlineBusiness`, `sanic_CHCROneDrive` | Excel / OneDrive | ExtractedTablesFromExcel |
| `sanic_AEDDConnOneDrive` | OneDrive | DownloadData |
| `new_sharedonedriveforbusiness_a6c30` | OneDrive | Process XML Files |

Huérfanas en el paquete: `sanic_sharedoffice365users_3b441`,
`sanic_sharedonedriveforbusiness_0d05f`. **Referenciadas pero NO incluidas**
(MissingDependencies): `sanic_conn_wfe_office365outlook` (IngestEmails,
ProcessEmail) y `sanic_CHCROutlook` (MailboxSweep).

## Flujos apagados

5 de 16: **Notify reviewers, Apply client data changes, Activate row,
Deactivate row, Process XML Files**. Consecuencias operativas:

- Un RFC de tipo Update/Activate/Deactivate aprobado **no se aplica nunca**; a
  los 30 minutos el RFCWatchdog lo marca Failed.
- Nadie recibe la notificación de RFC enviado (Notify reviewers OFF).
- El botón de lectura XML de Recounting invoca un flujo apagado.

## Defectos detectados en el JSON (verificados, pendientes de arreglo)

1. **Apply client data changes**: el caso OptionSet hace `Append` a `varItem`
   — variable que este flujo nunca inicializa — y no ejecuta ningún update: un
   cambio de option set aprobado no se aplicaría ni con el flujo encendido
   (código copiado de Create client data).
2. **Apply y Create**: `Actualizar_status_CRL_a_FAILED` escribe `159460004`,
   el mismo valor que APPLIED — las líneas fallidas quedan marcadas como
   aplicadas.
3. **Create client data**: `Update_a_row` y `Update_a_row_1` son updates sin
   ningún campo (`no-ops`); la intención era rellenar `sanic_RelatedClient` en
   RFC y audit trail con el cliente recién creado.
4. **Activate row**: las acciones se llaman `Actualizar_*_a_INACTIVE` pero
   escriben Active (cosmético, confunde).
5. **Notify reviewers**: variables `varNewValue`/`varOldValue` con nombres
   cruzados y un binding `"$data": "${changes}"` en la card que nunca se
   alimenta (las columnas reciben los arrays completos).
6. **ExtractedTablesFromExcel**: referencia `outputs('Get_File_Metadata')` con
   caja distinta a la acción `Get_file_metadata`, y deja los `.xlsx` temporales
   en OneDrive `/tmp` sin limpiar.
7. **Process XML Files**: rutas personales hardcodeadas
   (`/CashHub_XML_Anneke/…`) y devuelve a la app el **nombre lógico** de la
   tabla en lugar del GUID de la sesión (`…@lookuplogicalname`).
