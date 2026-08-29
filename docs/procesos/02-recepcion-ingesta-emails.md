# Recepción e ingesta de emails


## 1. Qué es este proceso

El cliente declara el contenido de sus tulas llenando la plantilla oficial
`CH-TPL-Depositos` (Excel) y la envía por correo, con asunto que empieza con
"Pre-registro", al buzón compartido del banco. Un flujo detecta el correo cada
minuto y lo entrega a un pipeline que: registra el correo y su adjunto,
invoca al motor de validación (28 reglas paramétricas corriendo dentro de un
plugin C# síncrono), y según el resultado **materializa la declaración digital**
(bolsas → depósitos → cheques) o la rechaza con el detalle exacto de qué
falló. Un segundo componente — el **triage de remitente** — decide, de forma
independiente de si el archivo pasó o no, si corresponde responderle a quien
escribió: solo una dirección verificada recibe respuesta; todo lo demás se
cuarentena o se escala a Banca de Empresas sin contestar, por seguridad (no
confirmarle a un tercero que un CIF es cliente del banco). Un barrido cada 10
minutos actúa de red de seguridad: re-valida pendientes, rescata correos que
quedaron huérfanos en el buzón, y deja un latido de salud que la Email
Validation Console usa como semáforo. Esa Console es la **bandeja de
contingencia**: en un día sano queda vacía, porque todo el camino normal es
automático.

```mermaid
flowchart TD
    Cliente["Cliente\n(Excel + asunto 'Pre-registro')"] -->|1| Buzon["Buzón compartido\n(Outlook)"]
    Buzon -->|IngestEmails, por trigger| Ingest["CH-Flow-IngestEmails"]
    Ingest -->|MessageId| Process["CH-Flow-ProcessEmail\n(dedup por InternetMessageId)"]
    Process -->|crea ReceivedEmail + Attachment,\nmueve a Procesados| API["Custom API\nsanic_CHValidateReceivedEmail"]
    API --> Motor["ReceivedEmailValidationService\n28 reglas (VR-01..VR-28)"]
    Motor -->|Passed + cliente resuelto| Materializer["DeclarationMaterializer\nDeclaration → DeclaredBags → Deposits → Checks"]
    Motor --> Triage["SenderTriage\n(VR-03 + VR-27 + VR-25)"]
    Triage -->|Registrado| Reply["ClientReplyComposer\n+ CH-Flow-Reply to client email"]
    Reply --> Cliente
    Triage -->|Cliente sin registrar| Manual["Revisión manual\n(Banca de Empresas, vía Console)"]
    Triage -->|Sospechoso / No corresponde| Cuarentena["Carpeta Cuarentena\n(Outlook) + alerta"]
    Sweep["CH-Flow-MailboxSweep\ncada 10 min"] -.->|sana pendientes,\nrescata huérfanos,\nheartbeat| Process
    Console["Email Validation Console\n(contingencia)"] -.->|reproceso,\ningesta manual| API
    Console -->|"Mover a cuarentena"\n(manual, Operaciones)| Cuarentena
```

**Handoffs**:
- **Recibe**: nada de otro proceso — es la puerta de entrada primaria del
  contenido declarado del cliente. Consume el maestro de remitentes
  autorizados (`CH-Tbl-ClientEmail`) que administra
  [01-administracion-datos-clientes](01-administracion-datos-clientes.md).
- **Entrega**: las `CH-Tbl-DeclaredBags`/`DeclaredDeposits`/`DeclaredChecks`
  materializadas son el contra-valor contra el que
  [03-recepcion-tulas-esclusa](03-recepcion-tulas-esclusa.md) y
  [05-recuento-tulas](05-recuento-tulas.md) concilian la bolsa física. Cuando
  la Console "Registra correo" de un remitente sin registrar, crea un RFC de
  alta que resuelve [01-administracion-datos-clientes](01-administracion-datos-clientes.md).

## 2. Estados y transiciones

Todo vive en `CH-Tbl-Received Emails` (`sanic_chtblreceivedemails`), una fila
por correo. Cinco atributos independientes. `sanic_validationstatus`,
`sanic_emailreviewstatus`, `sanic_senderclassification` y
`sanic_emailreviewmethod` los escribe siempre
`ReceivedEmailValidationService.UpdateEmail` — la app **no** tiene ninguna
facultad de override sobre ellos (se sacó el "Denegar/Aceptar manualmente",
ver §9). La Console sí puede escribir `sanic_emailreplystatus` (Resuelto por
fuera, Mover a cuarentena) y `sanic_receivedemailoutlookuniqueid` (Mover a
cuarentena, tras mover el mensaje de carpeta).

### `sanic_validationstatus` — veredicto del archivo

| Valor | Etiqueta | Quién transiciona | Guard |
|---|---|---|---|
| `159460000` | Pendiente | Valor inicial (`AppDefaultValue`) | — |
| `159460001` | Válido | Motor: `result.Passed = true` | Ninguna reescritura fuera del plugin |
| `159460002` | Inválido | Motor: `result.Passed = false` con hallazgos de negocio | ídem |

### `sanic_emailreviewstatus` — estado operativo del correo

| Valor | Etiqueta | Quién transiciona | Guard |
|---|---|---|---|
| `159460000` | Pendiente | Alta (ingesta) | — |
| `159460001` | Aceptado | Motor, si `Passed` | Requiere `Passed` Y `ResolvedClientId` Y `Workbook` no nulo para que exista `Declaration` (§10) |
| `159460002` | Omitido | Motor, si el triage es Sospechoso/No corresponde y no hay respuesta | — |
| `159460003` | Revisión manual | Motor, si el triage es Cliente sin registrar | Le toca a Banca de Empresas |
| `159460004` | Error | Motor, si `HasSystemIssues` sin hallazgos de negocio (falla de sistema, no del cliente) | `ErrorText` lleva la nota técnica cruda |
| `159460005` | Denegado | Motor, si `Passed = false` con hallazgos de negocio | `ErrorText` lleva los mensajes que el cliente ya vio en el correo de rechazo |

### `sanic_emailreplystatus` — si salió respuesta al cliente

| Valor | Etiqueta | Quién transiciona |
|---|---|---|
| `159460000` | Pendiente | El motor arma `reply` (asunto+cuerpo) y lo deja para el flujo de envío |
| `159460001` | Respondido | `CH-Flow-Reply to client email`, tras `ReplyToV3` exitoso |
| `159460002` | Omitido | El motor, cuando el triage decide `MayReplyToSender = false` (Cliente sin registrar / Sospechoso / No corresponde) |

Si el envío falla, el flujo **no** cambia el estado a Respondido: concatena en
`sanic_errortext` el aviso `"[SISTEMA] No se pudo enviar la respuesta…"` y el
correo queda con `ReplyStatus = Pendiente.

### `sanic_senderclassification` — triage del remitente

| Valor | Etiqueta | Regla que lo produce |
|---|---|---|
| `159460000` | Remitente registrado | VR-03 = Pass |
| `159460001` | Cliente sin registrar | VR-03 ≠ Pass, VR-27 = Pass, VR-25 = Pass |
| `159460002` | Sospechoso | VR-03 ≠ Pass, VR-27 = Pass, VR-25 ≠ Pass |
| `159460003` | No corresponde | VR-03 ≠ Pass, VR-27 ≠ Pass |

**una regla Omitida por dependencia nunca cuenta
como aprobada** — sin evidencia no se otorga confianza. `sanic_triagenotes`
siempre cita los VR concretos (regla de la casa: nunca un motivo genérico).

### `sanic_emailreviewmethod`

`159460000` Manual / `159460001` Auto (lo estampa siempre el motor). **Desde
que se eliminó el override "Denegar/Aceptar manualmente" (§9), nada en el
sistema vuelve a escribir `Manual`** — el valor queda huérfano en el option
set, solo por compatibilidad con filas históricas. No confundir con
`sanic_emailreplystatus = Omitido`, que sí lo sigue estampando la Console
("Resuelto por fuera" y "Mover a cuarentena").

## 3. Roles y acceso

Este proceso pasó de "sin ningún rol de seguridad Dataverse mapeado" a tener
2 roles nuevos, creados por API (sin import de solución, para no arriesgar
cambios en vivo que el repo local no tenía — `scripts/provision-email-ingestion-roles.sh`,
idempotente). Los dos están en la BU raíz, agregados a `CashHubCore`, pero
**todavía sin asignar a ningún usuario/team/Service Principal** — eso queda
pendiente de los GUID de grupo AAD correspondientes.

**`CH-SR-EmailIngestionFlow`** — la conexión Dataverse de los Cloud Flows de
ingesta (`IngestEmails`/`ProcessEmail`/`MailboxSweep`/`Reply to client email`).
El motor de validación corre como sistema (ver abajo), así que este rol NO lo
gobierna a él — solo lo que los flujos tocan directo:

| Tabla | Privilegios | Depth |
|---|---|---|
| `sanic_chtblreceivedemails` | Create, Read, Write, Append, AppendTo | Global |
| `sanic_chtblemailsattachments` | Create, Read, Append | Global |
| `sanic_chtblsystemheartbeat` | Create, Read, Write | Global |

**`CH-SR-EmailOperationsConsole`** — humanos de Operaciones en la Console
(Marcar para reproceso, Reenviar respuesta, Resuelto por fuera, Mover a
cuarentena). Solo lectura salvo Write sobre ReceivedEmails; `ValidationResults`
queda de solo lectura a propósito — lo mantiene el plugin, no se edita a mano:

| Tabla | Privilegios | Depth |
|---|---|---|
| `sanic_chtblreceivedemails` | Read, Write | Global |
| `sanic_chtblemailsattachments`, `sanic_chtblvalidationruns`, `sanic_chtblvalidationresults`, `sanic_validationrules`, `sanic_chtblclientemail`, `sanic_chtblemailtemplate`, `sanic_chtblplaybook`, `sanic_chtbldeclaration`, `sanic_chtbldeclaredbags`, `sanic_chtbldeclareddeposits`, `sanic_chtbldeclaredchecks` | Read | Global |

**Qué verifica la app vs. qué impone el servidor**:
- Solo oculta botones que no corresponden al caso. El control de acceso
  real, tal como está exportado, depende de a quién se comparte la app.
- El servidor **sí** impone dos cosas server-side: (1) `sanic_sourceemailaddress`
  tiene `IsSecured=1` (columna segura) — **pendiente crear el Field Security
  Profile** que la exponga; sin esto, `CH-SR-EmailOperationsConsole` va a
  poder leer la bandeja pero no el remitente, aunque tenga Read sobre la
  tabla; (2) el motor de validación corre como sistema, así que ningún
  privilegio de tabla del usuario condiciona si el correo se valida o no.

| Rol físico | App | Rol de seguridad Dataverse | Acceso real a este dominio |
|---|---|---|---|
| Cliente (externo) | — (envía correo) | — | Ninguno directo; su remitente debe estar en `CH-Tbl-ClientEmail` |
| Operaciones (§9 la llama "Operaciones"; §3 legado la llamaba "Analista de correos" — mismo perfil, nombre sin unificar en este doc) | Email Validation Console | `CH-SR-EmailOperationsConsole` (creado, sin asignar) | Ver grilla arriba |
| Banca de Empresas | Email Validation Console (vista "Esperando a Banca") — solo recibe el aviso de `btnAvisarBanca`; el alta real del correo la hace en `CH-App-ClientAdministration` | — (no necesita rol en *este* proceso; usa `CH-SR-ClientAdminContributor` para el alta en [01-administracion-datos-clientes](01-administracion-datos-clientes.md)) | Ninguno directo en este dominio |
| Sistema — conexión de los Cloud Flows | — | `CH-SR-EmailIngestionFlow` (creado, sin asignar a SP) | Ver grilla arriba |
| Sistema (plugin, `ValidateReceivedEmailPlugin`) | — | Corre como sistema (`CreateOrganizationService(null)`) | CRUD completo, sin restricción — ningún Security Role lo condiciona |

## 4. Aplicaciones

| App | Pantalla | Sirve para |
|---|---|---|
| **CH-App-EmailValidationConsole** (`sanic_chappemailvalidationconsole`) | `ColaDeProcesamiento` | Bandeja de contingencia: banda de salud (semáforo contra el heartbeat `ingesta-correos`), 4 contadores, vistas por caso ("Esperando a Banca", "Sospechosos", "Errores", "Sin responder"), panel de detalle con "Qué pasó" / "Qué hacer" (`CH-Tbl-Playbook`) / reglas fallidas, y las acciones del §9. |
| **CH-App-DepositsTracker** (`sanic_chappdepositstracker`) | `scrCorreo` (guía [`06-pantalla-correo`](../tracker-rebuild/06-pantalla-correo.md)) | Trazabilidad de solo lectura: la raíz donde aterriza cualquier búsqueda por número de gestión (`Email-N`), con las 4 pestañas Validaciones / Declaración / Cheques declarados / Respuesta al cliente. |


## 5. Flujos Power Automate


| Flujo | Trigger | Qué toca  |
|---|---|---|
| `CH-Flow-IngestEmails` | `SharedMailboxOnNewEmailV2`, buzón `sanic_CHEVSharedMailboxAddress` — **hardcodeado literal en el trigger**, no leído de la env var (§10) | Invoca `ProcessEmail` con el `MessageId`. Sin manejo de error propio a propósito: si el hijo falla, el correo queda en Inbox y lo recoge el barrido |
| `CH-Flow-ProcessEmail` | `Request` (hijo, param `MessageId`) | Dedup por `sanic_receivedemailinternetmessageid`; si es nuevo: crea `ReceivedEmail` + `EmailsAttachments` (sube `sanic_filecontent`), mueve a `Procesados`, **refresca `sanic_receivedemailoutlookuniqueid`** (Exchange cambia el id al mover), invoca la Custom API (retry ×4), y si el triage sale Sospechoso/No corresponde mueve el mensaje a `Cuarentena` |  
| `CH-Flow-MailboxSweep` | Recurrence 10 min | 5 etapas: sana pendientes (top 20 sin corrida o con corrida más vieja que `modifiedon`), vigila que `IngestEmails` esté activo, rescata del Inbox lo que lleve >5 min sin registro (invocando `ProcessEmail`), **reintenta respuestas encoladas** (top 20 con `ReplyStatus = Pendiente`, cuerpo ya armado y `sanic_replyattempts < 3`, quietas >5 min: incrementa `sanic_replyattempts` y lo deja en el log de notas — no vuelve a invocar `ReplyToV3` directo), y siempre actualiza/crea el heartbeat `sanic_name = 'ingesta-correos'` en `CH-Tbl-SystemHeartbeat` con `sanic_details = "sanados=… faltantes=… notas=…"` y `sanic_lastrunon` | 
| `CH-Flow-Reply to client email` | Webhook `Update` sobre `ReceivedEmails`, filtrado por `sanic_replybodyhtml,sanic_emailreplystatus`  | `ReplyToV3` sobre el hilo original desde el buzón compartido; marca Respondido + fecha + `repliedby = "CashHub (automatico)"`; si falla, concatena el aviso de sistema en `sanic_errortext` |

## 6. Tablas

| Tabla | Logical name | Columnas propias del proceso |
|---|---|---|
| **CH-Tbl-Received Emails** | `sanic_chtblreceivedemails` | `sanic_sourceemailaddress` (`IsSecured=1`), `sanic_subject`, `sanic_client` (lookup, "el contenido ATRIBUYE" aunque el correo se rechace), `sanic_declaredcif`, `sanic_name` (autonumber `Email-N`), + los 5 option sets del §2 |
| **CH-Tbl-EmailsAttachments** | `sanic_chtblemailsattachments` | `sanic_email` (lookup, req.), `sanic_filename`, `sanic_tamanoenbytes`, `sanic_filecontent` (file, `IsSecured=0`) |
| **CH-Tbl-ValidationRules** | `sanic_validationrules` | Catálogo paramétrico: `sanic_validationname` (clave VR-xx), `sanic_validationorder`, `sanic_isactive`, `sanic_validationvalue`, `sanic_validationfailmessage`, `sanic_validationdependency` (self-lookup) |
| **CH-Tbl-ValidationRuns** | `sanic_chtblvalidationruns` | `sanic_name` = `"Validacion de {Email-N}"`, `sanic_runstart`/`sanic_runend`, `sanic_runresult` (Pass/Fail), `sanic_receivedemail` — lookup real al correo (**Mantenido por plugin**; existe porque `sanic_sourcerecordid` es texto libre polimórfico y no delega — ver §10 sobre el bug de casing que tuvo este campo) |
| **CH-Tbl-ValidationResults** | `sanic_chtblvalidationresults` | Una fila por hallazgo (o por regla si Pass/Omitida): `sanic_validationrule`/`sanic_validationrun` (lookups), `sanic_validationresult` (Pass/Fail/Omitida), `sanic_durationms`, `sanic_sheet`/`sanic_cellref`/`sanic_rownumber` (español), `sanic_detail` — **Mantenido por plugin, no editar a mano.** |
| **CH-Tbl-ClientEmail** | `sanic_chtblclientemail` | `sanic_name` = dirección de correo, `sanic_client` (lookup). Único remitente que dispara VR-03=Pass |
| **CH-Tbl-Declarations** | `sanic_chtbldeclaration` | `sanic_name` = `"Declaracion {fecha} {Email-N}"`, `sanic_client`, `sanic_receivedemail` (lookup real al correo) — **Mantenido por plugin.** Solo existe si `Passed` |
| **CH-Tbl-DeclaredBags** | `sanic_chtbldeclaredbags` | `sanic_bagnumber`, `sanic_declarationid` (lookup), `sanic_totalnio/usd/eur`, **`sanic_totaldeposits`: entero simple escrito por `DeclarationMaterializer` con el conteo real de depósitos por bolsa — NO es rollup** (lo era hasta el fix reciente, §10) — **Mantenido por plugin, no editar a mano** |
| **CH-Tbl-DeclaredDeposits** | `sanic_chtbldeclareddeposits` | `sanic_transactionno`, `sanic_accountnumber`, `sanic_currency`, `sanic_cashamount`/`sanic_baclocalcheckamount`/`sanic_otherlocalcheckamount`, `sanic_declaredbag` (lookup), `sanic_declaration` (denormalizado a la raíz) — **Mantenido por plugin.** |
| **CH-Tbl-DeclaredChecks** | `sanic_chtbldeclaredchecks` | `sanic_checknumber`, `sanic_checkamount`, `sanic_checkcurrency`, `sanic_checktype` (BAC si el texto de tipo contiene "BAC", si no Otros), `sanic_declareddepositid` (lookup) — **Mantenido por plugin.** |
| **CH-Tbl-EmailTemplate** | `sanic_chtblemailtemplate` | Claves `reply-accepted`/`reply-rejected`, `IsActive` (`sanic_isactive`). Si falta, está inactiva o falla: cae al HTML embebido de respaldo — nunca se deja de responder por un problema de plantilla |
| **CH-Tbl-Playbook** | `sanic_chtblplaybook` | `sanic_titulo`, `sanic_pasos`, `sanic_orden`, `sanic_nota`, `sanic_isactive` — el "Qué hacer" que la Console arma por clave de caso |
| **CH-Tbl-SystemHeartbeat** | `sanic_chtblsystemheartbeat` | `sanic_name` (clave `'ingesta-correos'`), `sanic_lastrunon`, `sanic_details` — **Mantenido por el flujo de barrido, no la app** |

## 7. Custom APIs

### `sanic_CHValidateReceivedEmail`

| Campo | Detalle |
|---|---|
| Tipo | Global, síncrona (`allowedcustomprocessingsteptype: 0`, `bindingtype: 0`) |
| Entrada | `ReceivedEmailId` (Guid, requerido) |
| Salida | `RunId` (Guid), `Result` (`"Pass"`/`"Fail"`), `FailedFindings` (entero) |
| Plugin | `CashHub.Plugins.ValidateReceivedEmailPlugin` — asociado directo sobre el `customapi` |
| `executeprivilegename` | La API queda sin privilegio dedicado atado. Alcance real de quién puede invocarla |
| Quién la invoca | `CH-Flow-ProcessEmail` (tras subir adjuntos, orden garantizado, retry ×4), `CH-Flow-MailboxSweep` (pendientes sin corrida o con corrida vieja), Custom API directa desde la Console ("Marcar para reproceso", indirecto — lo levanta el barrido en ≤10 min) o invocación manual con Web API para pruebas (guía 08, caso D1) |

Corre como sistema (`serviceFactory.CreateOrganizationService(null)`) y tiene
guarda anti-bucle: si `context.Depth > 2` se omite sin error.

## 8. Steps de plugins

**No hay steps clásicos de plugin (Pre-validation/Pre-operation/Post-operation)
sobre `sanic_chtblreceivedemails` hoy.

## 9. Acciones

### Enviar la declaración

| Campo | Detalle |
|---|---|
| **Quién** | Cliente (externo al sistema) |
| **Cuándo** | Cuando tiene tulas para declarar; puede reenviar tras una corrección |
| **Por qué / para qué** | Dispara todo el pipeline; habilita que su declaración se concilie luego contra la tula física |
| **Cómo** | Correo al buzón compartido, asunto empezando con "Pre-registro", **un** adjunto `.xlsx` con la plantilla `CH-TPL-Depositos` |
| **Qué valida el servidor** | Nada todavía — recién entra al motor cuando `ProcessEmail` lo registra |
| **Qué pasa si falla / deshacer** | No aplica: es la entrada del proceso. Si el archivo está mal, el motor lo rechaza con detalle (ver siguiente acción) |

### Ingesta y validación automática

| Campo | Detalle |
|---|---|
| **Quién** | Sistema (`CH-Flow-IngestEmails` → `CH-Flow-ProcessEmail` → Custom API → plugin) |
| **Cuándo** | Cada 1 minuto hay correo nuevo en el buzón compartido |
| **Por qué / para qué** | Registrar el correo, correr las 28 reglas y, si corresponde, materializar la declaración y responder — sin intervención humana |
| **Cómo** | No aplica (automático) |
| **Qué valida el servidor** | Las 28 reglas VR-01..VR-28 (`ValidationEngine.Run`, sin cortocircuito global — el cliente recibe la lista completa de hallazgos en un solo correo); dedup por `sanic_receivedemailinternetmessageid` |
| **Qué pasa si falla / deshacer** | Si el motor tira excepción no controlada, el correo queda `Error` con `ErrorText` explícito y el system job queda Failed (visible para operaciones); no hay deshacer — se reprocesa con la Custom API |

### Registrar correo (alta de remitente)

| Campo | Detalle |
|---|---|
| **Quién** | Banca de Empresas |
| **Cuándo** | Correo en triage "Cliente sin registrar" (plantilla oficial + CIF válido, remitente no autorizado) |
| **Por qué / para qué** | Habilitar que ese remitente reciba respuesta en futuros envíos |
| **Cómo** | Console → `ColaDeProcesamiento` → correo → "Registrar correo" |
| **Qué valida el servidor** | Crea un RFC (`ClientDataChangeRequest`) de alta de correo autorizado. La aprobación real del RFC la defiende `GuardChangeRequestApprovalPlugin` de [01-administracion-datos-clientes](01-administracion-datos-clientes.md), no este proceso |
| **Qué pasa si falla / deshacer** | El correo original no se reprocesa solo: hay que "Marcar para reproceso" después de que el RFC se apruebe |

### Marcar para reproceso

| Campo | Detalle |
|---|---|
| **Quién** | Operaciones |
| **Cuándo** | Cualquier correo que necesite re-evaluarse (RFC de remitente ya aprobado) |
| **Por qué / para qué** | Forzar una nueva corrida del motor |
| **Cómo** | Console → correo → "Marcar para reproceso" |
| **Qué valida el servidor** | Es indirecto: el `MailboxSweep` lo levanta en su próxima corrida (≤10 min) y vuelve a invocar la Custom API — las mismas 28 reglas, sin atajos |
| **Qué pasa si falla / deshacer** | Sin deshacer |

### Reenviar respuesta

| Campo | Detalle |
|---|---|
| **Quién** | Operaciones |
| **Cuándo** | `EmailReplyStatus = Pendiente` con `sanic_errortext` (el flujo de respuesta falló) |
| **Por qué / para qué** | El cliente no quede sin la respuesta que ya se compuso |
| **Cómo** | Console → correo → "Reenviar respuesta" |
| **Qué valida el servidor** | El disparo real sigue siendo el webhook de `CH-Flow-Reply to client email` sobre `sanic_replybodyhtml`/`sanic_emailreplystatus` — la acción de la Console solo re-arma las condiciones para que el webhook dispare |
| **Qué pasa si falla / deshacer** | Puede fallar de nuevo por el mismo motivo de conectividad/Outlook; sin deshacer |

### Resuelto por fuera

| Campo | Detalle |
|---|---|
| **Quién** | Operaciones |
| **Cuándo** | El caso se resolvió por un canal fuera del sistema (llamada, correo manual) |
| **Por qué / para qué** | Sacar el correo de la bandeja accionable sin fingir que el motor lo aprobó |
| **Cómo** | Console → correo → "Resuelto por fuera" + nota |
| **Qué valida el servidor** | Cierra con `EmailReplyStatus = Omitido` — no hay guard server-side, es un cierre administrativo |
| **Qué pasa si falla / deshacer** | Sin deshacer registrado |

### Mover a cuarentena (manual)

> Reemplaza a "Denegar/Aceptar manualmente" (eliminado). Decisión del usuario:
> Operaciones no debería tener la facultad de aprobar/denegar el veredicto de
> negocio del motor — solo puede escalar un caso a cuarentena, igual que ya
> hace el motor automáticamente para Sospechoso/No corresponde (§1, Bloque E).

| Campo | Detalle |
|---|---|
| **Quién** | Operaciones |
| **Cuándo** | Cualquier correo que Operaciones decida escalar a cuarentena a criterio propio — típicamente un caso "Revisión manual" (Cliente sin registrar) que no conviene dejar accionable, o cualquier otro que amerite sacarlo de circulación sin fingir una aprobación de negocio |
| **Por qué / para qué** | Sacar el correo de la bandeja accionable sin que Operaciones tenga poder de aprobar/denegar contenido — mismo criterio de seguridad que ya aplica el triage automático (no se le confirma nada al remitente) |
| **Cómo** | Console → correo → "Mover a cuarentena". Conector Office 365 Outlook (`MoveEmailV2`) mueve el mensaje a la carpeta `Cuarentena` del buzón compartido (mismo buzón que lee `sanic_CHEVSharedMailboxAddress`); en el mismo paso, un `Patch` sobre el registro deja `EmailReplyStatus = Omitido` y refresca `sanic_receivedemailoutlookuniqueid` con el id que Exchange asigna al mover el mensaje |
| **Qué valida el servidor** | Nada server-side — mismo patrón sin guard que "Resuelto por fuera". **Ojo**: ambas acciones dejan `EmailReplyStatus = Omitido`; se distinguen solo por si `ReplyCloseNote`/`RepliedBy` quedaron poblados (los pone "Resuelto por fuera", no "Mover a cuarentena") |
| **Qué pasa si falla / deshacer** | Sin deshacer registrado. No toca `EmailReviewStatus` ni `EmailReviewMethod` — a diferencia del override eliminado, esta acción no se disfraza de veredicto del motor |


## 10. Supuestos y limitaciones

1. **Re-validar un correo ya Aceptado crea una SEGUNDA `Declaration`.** El
   `DeclarationMaterializer` no dedupea: cada corrida en verde con
   `ResolvedClientId` materializa bolsas/depósitos/cheques nuevos. "Marcar
   para reproceso" sobre un correo ya Aceptado duplica la declaración. 
2. **VR-25 tiene la celda `Depositos!J2` hardcodeada** en vez de tomarla de
   parámetro (VR-24 sí lo hace) — deuda documentada en el backend.
3. **El filtro del webhook de `CH-Flow-Reply to client email` no incluye
   `sanic_replysubject`**: cambiar solo el asunto de la respuesta no dispara
   el envío. Limitación conocida, no un bug nuevo.

## 11. Pruebas

### Antes de empezar

| Requisito | Cómo se verifica |
|---|---|
| Flujos activos: `CH-Flow-IngestEmails`, `CH-Flow-Reply to client email`, `CH-Flow-MailboxSweep` | make.powerautomate.com → Soluciones → CashHubFlows |
| Su remitente autorizado| Tabla `CH-Tbl-ClientEmail`, o agréguelo desde la app de administración de clientes |
| Plantillas de correo activas | Tabla `CH-Tbl-EmailTemplate`: `reply-accepted` y `reply-rejected` |


**Cómo se envía cada prueba**: correo al buzón de CashHub con asunto que empiece con
**"Pre-registro"** y el archivo del caso como **único adjunto**.


---

### Bloque A — Casos aceptados (notificación de éxito)

| # | Archivo | Qué prueba | Resultado esperado |
|---|---|---|---|
| A1 | `01-OK-completo.xlsx` | Camino feliz completo: 3 depósitos, 2 bolsas, dos monedas, efectivo + cheques | **Correo verde** "Declaración recibida y validada" con tabla de 2 bolsas ( USD 800 / NIO 15,000; USD 1,200). Declaración materializada con 3 depósitos y 2 cheques colgando del depósito 1. |
| A2 | `02-OK-minimo.xlsx` | Mínimo aceptable: un depósito, solo efectivo, sin cheques | Correo verde con una sola bolsa (NIO 5,000). |
| A3 | `14-OK-volumen.xlsx` | 25 depósitos en 25 bolsas alternando monedas | Correo verde con las 25 bolsas listadas.  |

---

### Bloque B — Errores de negocio del cliente (notificación de corrección)

| # | Archivo | Regla | Resultado esperado en el correo |
|---|---|---|---|
| B1 | `03-ERR-cuenta-ajena.xlsx` | VR-23 | 1 error: la cuenta `999999999` (fila 7) no pertenece al cliente. **Con celda `Depositos!K7`.** |
| B2 | `04-ERR-sin-cif.xlsx` | VR-24 | 1 error: la celda `Depositos!J2` debe contener el CIF. |
| B3 | `05-ERR-cif-desconocido.xlsx` | VR-25 | 1 error: el CIF `0099999999` no corresponde a ningún cliente. El correo **no queda atribuido** a ningún cliente. |
| B4 | `06-ERR-sin-bolsa-ni-cuenta.xlsx` | VR-10, VR-11 | 2 errores ubicados: `J6` sin bolsa, `K7` sin cuenta. |
| B5 | `07-ERR-montos-invalidos.xlsx` | VR-20 | Errores por valores negativos en `O6` y `P6`. **Nota**: VR-21 (total en cero) se omite porque depende de VR-20 — es la cadena anti-ruido funcionando. |
| B6 | `08-ERR-tipos-y-moneda.xlsx` | VR-17, VR-18 | 2 errores: `O6` contiene texto donde va número; `L6` declara moneda `CAD` no soportada. |
| B7 | `09-ERR-cuenta-dos-monedas.xlsx` | VR-13 | 1 error: la cuenta `100200112` aparece con dos monedas (fila 7). |
| B8 | `10-ERR-cheques.xlsx` | VR-14 | Cheque `CHQ-REP` repetido (fila 3). **Nota**: VR-22 (huérfano) y VR-12 (descuadre) se omiten por dependencia de VR-14. |
| B9 | `11-ERR-cheque-incompleto.xlsx` | VR-15 | 2 errores: celdas `C2` (tipo) y `D2` (moneda) vacías en el cheque. |
| B10 | `12-ERR-sin-depositos.xlsx` | VR-09 | 1 error: no hay depósitos declarados. |
| B11 | `13-ERR-multiple.xlsx` | VR-10, VR-20, VR-13, VR-22, VR-23 | **6 errores mezclados**: es la prueba del agrupado del correo — verificar que la tabla los liste ordenados por hoja y fila, y que se lea sin esfuerzo. |
| B12 | `15-ERR-desglose-cheques.xlsx` | VR-28 | El **total** de cheques cuadra (300 = 300) pero el desglose está cruzado: se declaran como cheques del BAC y el cheque listado es de otro banco. 2 errores, uno por cada columna del desglose. |

---

### Bloque C — Errores del sobre (sin abrir el archivo)

Estos no necesitan archivo especial: se prueban con cualquiera de los anteriores.

| # | Cómo provocarlo | Regla | Resultado esperado |
|---|---|---|---|
| C1 | Enviar con asunto **sin** "Pre-registro" (ej. "Depósitos de hoy") | VR-04 | Error de sobre: el asunto debe comenzar con 'Pre-registro'. |
| C2 | Enviar **sin adjunto** | VR-02 | Error: debe traer 1 adjunto y trae 0. Las reglas del archivo se omiten en cadena. |
| C3 | Enviar con **dos adjuntos** | VR-02 | Error: debe traer 1 adjunto y trae 2. |
| C4 | Enviar un **PDF o Word** en vez del Excel | VR-05 | Error: el adjunto debe ser .xlsx. |
| C5 | Enviar desde una dirección **no registrada** | VR-03 | Error: el correo no está registrado como remitente autorizado. |
| C6 | Enviar un Excel cualquiera (no la plantilla) | VR-27 / VR-07 | Error: el archivo no es la plantilla oficial o no tiene las 2 tablas. |
| C7 | Enviar un archivo `.xlsx` **corrupto** (renombrar un .txt) | VR-07 | Error explícito: no se pudo abrir como paquete ZIP. |

---

### Bloque D — Contingencia y operación

| # | Escenario | Cómo probarlo | Resultado esperado |
|---|---|---|---|
| D1 | **Correo huérfano** | Apagar `CH-Flow-IngestEmails`, enviar un correo, esperar 10 min | El barrido detecta el flujo apagado / el correo sin registrar y alerta por Teams (si está configurada la variable `sanic_CHEVOpsTeamsGroupId`); deja latido en `CH-Tbl-SystemHeartbeat`. |
| D2 | **Pendiente sin validar** | Crear a mano un `ReceivedEmail` en estado Pendiente | El barrido lo re-valida solo en su próxima corrida (≤10 min). |
| D3 | **Plantilla de correo editada** | Cambiar un texto o color en `CH-Tbl-EmailTemplate` (`reply-rejected`) y reenviar un caso del bloque B | El correo sale con el diseño nuevo **sin redesplegar nada**. |
| D4 | **Plantilla desactivada** | Poner `IsActive = No` en `reply-accepted` y enviar `02-OK-minimo.xlsx` | El correo llega igual, con el diseño embebido de respaldo (nunca queda sin respuesta). |


### Bloque E Triage de remitentes — cuarentena y no respondidos 🔴

El motor clasifica al remitente combinando VR-03 (remitente registrado), VR-27
(plantilla oficial) y VR-25 (CIF conocido). **Solo "Registrado" recibe
respuesta** — a los demás NO se les responde (responder confirmaría a un
tercero que ese CIF es cliente del banco).

| # | Caso | Cómo provocarlo | Esperado |
|---|---|---|---|
| E1 🔴 | Cliente sin registrar | Remitente NO autorizado + plantilla oficial + CIF válido | Clasificación "Cliente sin registrar". **SIN respuesta al remitente**. Queda para revisión de Banca de Empresas (visible en la Console). |
| E2 🔴 | Sospechoso | Remitente NO autorizado + plantilla oficial + CIF inexistente | Clasificación "Sospechoso": sin respuesta, correo movido a carpeta **Cuarentena** en Outlook |
| E3 🔴 | No corresponde | Remitente NO autorizado + adjunto que no es la plantilla (o sin adjunto) | Clasificación "No corresponde": sin respuesta, a **Cuarentena** |
| E4 | Cuarentena manual (Operaciones) | Console → cualquier correo → "Mover a cuarentena" | Mensaje movido a la carpeta **Cuarentena** del buzón compartido, `EmailReplyStatus = Omitido`, `sanic_receivedemailoutlookuniqueid` actualizado con el id post-movimiento. `EmailReviewStatus`/`EmailReviewMethod` **sin tocar** |

### Bloque D Email Validation Console (contingencia)

- D1: en un día sano, con el pipeline ON, la bandeja de la Console está
      **vacía** — todo entra y se responde solo.
- D2: heartbeat `ingesta-correos` en `CH-Tbl-SystemHeartbeat`
      actualizándose cada ~10 min (el semáforo de la Console).
