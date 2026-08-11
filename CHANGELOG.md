# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto adhiere a [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

### Added

- **`CH-Tbl-Parameter` (`sanic_chtblparameter`): tabla de configuración key/value para CashHub.** Consolida parámetros (grupos AAD, umbrales, listas, flags) en una tabla editable por negocio en vez de multiplicar environment variables. Organization-owned, en `CashHubCore`. Columnas: `sanic_name` (Key, primary, convención `Dominio.Sub.Nombre`), `sanic_value` (Memo 4000), `sanic_category` (choice local: Security/Email/Validation/Workflow/Integration/General), `sanic_datatype` (choice local discriminador de parseo: String/Number/Boolean/Guid/GuidList/Json/Email/Url), `sanic_description`, `sanic_isactive`. Alternate key `sanic_ParameterKey` sobre `sanic_name` para unicidad y retrieve-by-key. Provisión idempotente vía `scripts/provision-parameters-table.py` (no borra nada, no toca env vars). Seed inicial: los grupos AAD approver de Banca y Boveda. Decisiones: sin columna Environment (un row ya es env-específico por vivir en ese Dataverse); los secretos NO van acá (siguen en env var tipo Secret + Key Vault). Migración aditiva: cada consumer se recablea incremental, no hay rip-and-replace de los 30 env vars actuales.
- **Ícono de marca para CH-App-ClientAdministration** (`docs/marca/icono-client-administration.svg` + PNG en 512/192/96/48/32). Una persona con insignia de verificación sobre el rojo de marca (`#C8102E`, `rx=112`, trazo blanco): la persona es el cliente (mismo lenguaje que el ícono *People* del menú), la insignia con check es la aprobación del RFC — el ícono dice lo que la app hace. Mismo patrón que IngestionConsole: SVG fuente + `docs/marca/generar-icono-client-administration.py` (Pillow, dibuja a 4x y reduce con LANCZOS; fuente y PNG comparten coordenadas). Listo para cablear al catálogo del Hub (`sanic_icon` / `sanic_icon_url`) cuando se quiera.
- **Ícono de marca para CH-App-Reception** (`docs/marca/icono-reception.svg` + PNG en 512/192/96/48/32). Una bolsa de efectivo con signo `$` y una flecha de entrada sobre el rojo de marca (`#C8102E`, `rx=112`, trazo blanco): la bolsa es la tula, la flecha es la recepción — el oficial de esclusa RECIBE tulas. Mismo lenguaje visual que el resto del suite. El SVG es la fuente de verdad; `docs/marca/generar-icono-reception.py` lo renderiza con cairosvg a 4x y reduce con LANCZOS (a diferencia de los íconos previos, que redibujaban con primitivas Pillow: el `joint="curve"` de Pillow produce púas en los trazos bezier gruesos, así que se renderiza el SVG directo). Listo para cablear al catálogo del Hub (`sanic_icon` / `sanic_icon_url`) cuando se quiera.

### Fixed

- **CH-App-Hub: el filtrado de acceso era código muerto y el redirect de app única no existía.** `Gallery1.Items` usaba `'CH-Tbl-AppCatalogs'` crudo en vez de la colección filtrada por membresía que armaba `OnStart`, así que todos los usuarios veían todas las apps (incluso `Enabled = false`) y el trabajo de `ListGroupMembers` se descartaba. Reescrito `App.OnStart`: una sola llamada a Graph `checkMemberGroups` (vía `Office365Groups.HttpRequest`, membresía transitiva, hasta 20 grupos) reemplaza las N llamadas paginadas a `ListGroupMembers` (que además daba falsos negativos con grupos de >100 miembros y no resolvía grupos anidados). `sanic_aadgroupid` ahora admite varios GUIDs separados por `;` (una app puede estar habilitada para N grupos — caso ClientAdministration con 4 grupos). La galería usa `Sort(colAppData, 'Order')` (antes ignoraba `sanic_order`). Si el usuario tiene acceso a una sola app, `OnStart` lo redirige directo con `Launch(..., LaunchTarget.Replace)`. Si Graph falla, el hub degrada fail-open con banner de aviso (la autorización real la imponen los roles de Dataverse de cada app destino).
- **CH-App-Hub: roles sin lectura del catálogo veían el hub vacío.** `CH-SR-ClientAdminApprover`, `CH-SR-ClientAdminContributor`, `CH-SR-OpsAdmin` y `CH-SR-AnalyticsRead` no tenían `prvRead` sobre `sanic_CHTblAppCatalog`; agregado con nivel Global (paridad con `CH-Role`/`Counter`/`Receptionist`/`CountingSupervisor`).
- **CH-App-Hub UX**: eliminado `HtmlText1` (render duplicado del banner SVG animado, quemaba CPU); estado vacío ("No tiene acceso a ninguna aplicación") y banner de error de verificación de accesos; toda la card lanza la app (antes solo el ícono y el chevron); tooltip del chevron corregido ("View item details" → "Abrir <app>") y `AccessibleLabel` en el ícono.

- **Líneas de RFC fallidas quedaban marcadas como Applied.** En `CH-Flow-Applyclientdatachanges` y `CH-Flow-Createclientdata`, la acción `Actualizar_status_CRL_a_FAILED` escribía `sanic_changestatus = 159460004` (Applied) — el mismo valor que la rama de éxito —, así que una línea que fallaba al aplicarse quedaba registrada como aplicada correctamente y la bitácora mentía. Corregido a `159460005` (Failed). Desplegado en DEV vía patch de `clientdata` de ambos flujos.
- **`CH-Flow-Applyclientdatachanges` estaba desactivado e inválido; los RFC tipo Update no se aplicaban.** El caso OptionSet hacía `AppendToArrayVariable` sobre `varItem`, una variable **nunca inicializada** en ese flujo (patrón copiado de `Createclientdata`, donde sí se inicializa), y sin `Update_a_row` que escribiera el valor. La definición no validaba (`InvalidVariableOperation`), el flujo quedaba en Draft y los cambios de campos tipo OptionSet nunca se escribían. Reemplazado por un `Update_a_row_optionset` que escribe el entero mapeado al registro (igual que los demás tipos). El flujo volvió a activarse. Verificado en vivo: sin referencias a `varItem`, `Update_a_row_optionset` presente, flujo activo.
- **CH-App-ClientAdministration UX — etiquetas de la pestaña Cuentas y typos.** En `formCuentas`, `DataCardKey2Cta` mostraba "Nombre de sucursal" (era el número de cuenta → corregido a "Numero de cuenta") y `DataCardKey9Cta` mostraba "Notificar siempre" (era la moneda → corregido a "Moneda") — copy-paste de sucursales/emails sin ajustar; los controles legítimos de Sucursales (`DataCardKey2`) y Emails (`DataCardKey9`) no se tocaron. Typo "Campos incompeltos" → "Campos incompletos" en el `OnButtonSelect` del sidePanel. Desplegado en DEV (CashHubApps 1.28.0.0).
- **CH-App-ClientAdministration: `App.OnStart` resolvía permisos con N llamadas seriales y perdía usuarios más allá de la página 1.** El bloque hacía `ForAll('CH-Tbl-ClientDataChangePermissions', ...LookUp(Office365Groups.ListGroupMembers(AADGroupID).value, ...))` — una llamada de membresía por permiso, serial, y sin paginación: un usuario en un grupo de >100 miembros que cayera fuera de la primera página perdía sus botones de crear/aprobar RFC en silencio. Reescrito al patrón `checkMemberGroups` (membresía transitiva de Graph vía `Office365Groups.HttpRequest`, en lotes de 20 con `Sequence`/`RoundUp`/`FirstN`/`LastN`), igual que el Hub: una llamada por lote resuelve todos los grupos distintos de la tabla de permisos y `colClientDataPermissions` se arma filtrando localmente. **Fail-CLOSED** (a diferencia del Hub, que es fail-open): si Graph falla no se otorga ningún permiso y se avisa con `Notify` — el gate real de aprobación sigue siendo el `GuardChangeRequestApprovalPlugin` server-side. Desplegado en DEV (CashHubApps 1.28.0.0). Verificación funcional final pendiente de prueba con usuario real (checkMemberGroups corre en contexto del usuario logueado; el SP no puede reproducirla).
- **CH-App-ClientAdministration: el campo "fecha de aplicación" (`sanic_applyon`) era editable y mentía.** El `ApplyOn_DataCard` aparecía como picker de fecha/hora editable en los formularios de RFC (pantallas `ClientInformation`, `Clients` y `ChangeRequestDetails`), sugiriendo que el operador podía **programar** cuándo se aplicaba el cambio — pero los flujos aplican de inmediato y pisan `applyon` con `utcNow()`, así que la fecha ingresada se descartaba. Por decisión de negocio se volvió **read-only**: `DisplayMode.View` en las tres tarjetas (cascadea a los pickers hijos) y etiqueta renombrada a "Fecha de aplicacion real". Ahora el campo muestra, en solo lectura, la fecha real en que el flujo aplicó el RFC, en vez de fingir programación. Desplegado en DEV (CashHubApps 1.29.0.0).
- **Al crear un cliente por RFC, el RFC no guardaba el id del cliente creado ni se enlazaba la bitácora.** En `CH-Flow-Createclientdata`, `Update_a_row_1` (backfill del RFC tras crear el cliente) y el `Update_a_row` del `Apply_to_each` (backfill de la bitácora) tenían el `item` **vacío** — eran PATCH no-op. Ahora `Update_a_row_1` escribe `sanic_targetrecordid` y enlaza `sanic_RelatedClient` al cliente recién creado, y el `Apply_to_each` enlaza `sanic_RelatedClient` en cada audit trail de creación de cliente (que se creaban sin cliente porque este aún no existía). Desplegado en DEV.

### Changed

- **El acceso por grupos del hub ahora es relacional: `CH-Tbl-AppCatalogAccess`.** El multi-valor de GUIDs separados por `;` en `sanic_aadgroupid` del catálogo se reemplazó por una tabla hija (una fila = una app + un grupo AAD, lookup `sanic_appcatalog` con delete cascade, en la solución Core). El hub lee esa tabla en `OnStart` (colección normalizada app/grupo) y resuelve membresía con la misma llamada única a `checkMemberGroups`; una app sin filas de acceso es pública. Los 8 roles CH recibieron `prvRead` Global de la tabla nueva. Migrados los 4 grupos de ClientAdministration a filas de acceso y limpiada la columna vieja, que quedó **deprecada y bloqueada por el guard** (cualquier escritura no vacía rebota señalando la tabla nueva). El datasource de la canvas app se generó desde la metadata viva del entorno (`scripts/generate-canvas-datasource.py` — el blob de `pkgs/TableDefinitions` es cache de respuestas del Web API; la `InstanceUrl` debe coincidir con la de los demás datasources del dataset o `pac canvas pack` falla con PA3005). Core 1.11.0.0 (export del entorno), Apps 1.26.0.0.
- **`GuardAppCatalogAccessPlugin`**: síncrono Pre-operation en `Create`/`Update` de la tabla de accesos — exige UN GUID por fila (sin `;`), app obligatoria, normaliza el GUID (trim + minúsculas) y bloquea duplicados (app, grupo) leyendo como sistema. Verificado e2e en DEV: duplicado, `;`, GUID inválido, fila sin app y escritura al campo deprecado del catálogo rebotan con 400; alta válida normaliza y pasa. Batería de tests: 131 en verde.
- **CH-App-Hub: `checkMemberGroups` en lotes y `App.OnError`.** La resolución de membresía ya no trunca en silencio a 20 grupos: el `OnStart` parte los grupos distintos del catálogo en lotes de 20 (`Sequence`/`RoundUp` + `FirstN`/`LastN`) y acumula las respuestas; si un lote falla, el hub degrada fail-open con el banner existente. Nuevo `App.OnError`: `Trace` con severidad Error (mensaje, `ErrorKind`, source — visible en Monitor/App Insights) + `Notify` al usuario, para que ningún error de fórmula muera en silencio. Apps 1.27.0.0.

### Security

- **Validación del catálogo de apps del hub** (`GuardAppCatalogPlugin`). El hub lanza `sanic_appurl` tal cual (`Launch`), así que una URL arbitraria escrita por Web API/Excel convertía al hub en un lanzador de phishing con identidad corporativa; y un `sanic_aadgroupid` malformado rompía el gating por grupos en silencio. El guard corre **síncrono en Pre-validation** de `Create` y `Update` de `sanic_chtblappcatalog` (Update filtrado en los dos campos): la URL debe ser https absoluta con host exactamente `apps.powerapps.com` en puerto estándar (bloquea `http`, hosts ajenos, sufijos `apps.powerapps.com.evil.com`, trucos de userinfo `@`, puertos raros); los grupos deben ser GUIDs válidos separados por `;`, sin duplicados. Blanco es válido en ambos (fila en preparación / app pública). Lógica en el dominio puro (`AppCatalogGuard`, 17 casos de test, batería total 132), plugin como cáscara. Registro idempotente: `scripts/register-appcatalog-guard.sh`. Verificado end-to-end contra DEV: URL maliciosa y GUID inválido rebotan con 400 y mensaje claro; escritura legítima pasa. Nota: ningún rol CH tiene write sobre el catálogo (solo lectura) — la escritura queda reservada a System Administrator/System Customizer, y para ellos también rige el guard.
- **Separación de funciones en los RFC de administración de clientes** (`GuardChangeRequestApprovalPlugin`). Hasta ahora el control de aprobación vivía solo en la app canvas (visibilidad de botones): un Contributor con `prvWrite`/`prvCreate` podía pegarle `Approved` a su propio RFC por Web API o Excel y auto-aprobarse, y los flujos de aplicación lo tomaban. El guard corre **síncrono en Pre-operation** de `Create` y `Update` de `sanic_chtblclientdatachangerequest` y bloquea la transición ilegítima antes de que el pipeline la vea:
  - Aprobar/rechazar exige estado previo `Submitted` **y** que quien decide **no sea el creador** del RFC.
  - Impide **crear** un RFC ya resuelto (`Approved`/`Rejected`/`Applied`/`Failed`), cerrando el bypass por `Create`.
  - Las transiciones de sistema (`Approved→Applied/Failed` de los flujos, `→Canceled`, `Draft→Submitted`) pasan sin fricción.
  - **Fase 2**: además, aprobar/rechazar exige que el usuario **pertenezca al grupo Approver** de la tabla objetivo. Se resuelve server-side leyendo `CH-Tbl-ClientDataChangePermission` (tabla → grupo AAD) y la membresía del **Azure AD group team** correspondiente (`teammembership`). Fail-closed: sin grupo configurado o sin membresía, se bloquea.
  - Lógica de decisión en el dominio puro (`ChangeRequestApprovalGuard`, `netstandard2.0`, sin SDK) con 24 casos de test; el plugin es solo la cáscara que arma el contexto, resuelve la membresía y propaga el bloqueo.
  - Provisión idempotente de los AAD group teams de aprobadores: `scripts/provision-approver-teams.sh` (data-driven desde la tabla de permisos).
  - Registro idempotente del guard: `scripts/register-clientadmin-guard.sh` (requiere `dotnet build -c Release` previo). Dos pasos síncronos Pre-operation en Create y Update de `sanic_chtblclientdatachangerequest`.
  - Verificado end-to-end contra el entorno: se bloquean el create-en-resuelto, la transición inválida y la auto-aprobación del creador; la resolución del grupo Approver acierta los cuatro casos de prueba.

## [0.11.0] - 2026-08-05

Fase E (parte 1): respuesta automática al cliente con diseño parametrizable. Core 1.5.1.0, Flow 1.5.0.0.

### Added

- **Correo de respuesta al cliente** con diseño propio (HTML de email: tablas de layout, estilos inline, sin imágenes externas, 640px, legible en móvil): acuse de recibo con totales por moneda y bolsa cuando la declaración pasa, o lista completa de errores **con hoja y celda de cada uno** cuando no. Incluye el **número de gestión** (`Email-{SEQNUM}`) y el id de la corrida de validación para consultas.
- **Plantillas editables sin código** (`CH-Tbl-EmailTemplate` + `data/seed/60-emailtemplates.json`): negocio cambia HTML, textos y estilos en Dataverse; el motor las renderiza con `TemplateEngine` (`{{token}}`, `{{{sin_escapar}}}`, `{{#lista}}`, `{{#si:token}}`). Si la plantilla no existe, está inactiva o falla su lectura, cae al diseño embebido — el cliente nunca se queda sin respuesta.
- `CH-Flow-Replytoclientemail` reescrito: webhook sobre la respuesta lista, responde el hilo original del cliente, marca `Respondido` con fecha; ante fallo de envío deja rastro y conserva la respuesta para reenviarla.
- Campos `sanic_ReplySubject`/`sanic_ReplyBodyHtml` en `ReceivedEmails`.
- 63 tests (motor de plantillas + diseño de correo, incluidas las reglas de HTML para email y el escapado de contenido del cliente).

### Fixed

- El autonumber `Email-{SEQNUM}` de los correos volvió a funcionar: la ingesta lo pisaba con el asunto.
- `register-validation-plugin.sh`: el body va por archivo temporal (el assembly en base64 supera el límite de argumentos del SO).

### Pendiente de activación manual

- `CH-Flow-Replytoclientemail` (conexión de Outlook del usuario).

## [0.10.0] - 2026-08-05

Fase D del motor de validación: ingesta confiable, barrido con auto-reparación y heartbeat. Core 1.4.1.0, Flow 1.4.0.0.

### Added

- `CH-Flow-IngestEmails` reescrito de cero (el original tenía 3 defectos de fábrica: dedup contra el campo de respuesta, trigger sin adjuntos y propiedad inexistente): dedup por `InternetMessageId`, buzón por variable de entorno nativa, orden garantizado correo→adjuntos→archivo→motor (invoca `sanic_CHValidateReceivedEmail` explícitamente), Try/Catch con estados y mensajes explícitos.
- `CH-Flow-MailboxSweep` (cada 10 min): re-valida pendientes sin run (auto-reparación), detecta correos del buzón sin registrar (>10 min) y alerta por Teams al grupo de operaciones (`sanic_CHEVOpsTeamsGroupId`, opcional), y escribe el heartbeat.
- Tabla `sanic_CHTblSystemHeartbeat` — el semáforo de salud que consumirá la Console en la fase E.

### Changed

- Retirado el paso asíncrono de Create sobre `ReceivedEmails`: una sola fuente de disparo con orden garantizado + el barrido como red de contención (el script de registro ahora elimina el paso si existe).

### Pendiente de activación manual

- `CH-Flow-IngestEmails` y `CH-Flow-MailboxSweep` importan en borrador: las conexiones de Outlook/Teams pertenecen al usuario (activar en make.powerautomate.com).

## [0.9.0] - 2026-08-05

Fase C del motor de validación: parser de la plantilla, las 27 reglas completas y materialización de la declaración — verificado E2E positivo y negativo en DEV.

### Added

- Parser de xlsx propio (`XlsxParser`, ZipArchive + XML crudo, apto para el sandbox de Dataverse): tablas nombradas con ubicación de cada celda, hoja Metadata, celdas sueltas (CIF), y errores SIEMPRE explícitos (`XlsxParseException` dice qué parte del paquete falló).
- Las 21 estrategias restantes (VR-07…VR-27): estructura (tablas/columnas/plantilla oficial), contenido (bolsa/cuenta obligatorias, numéricos, negativos, totales, monedas, cuenta↔moneda única, cheques completos/repetidos/huérfanos, cruce cheques vs declarado) y negocio (CIF declarado → cliente registrado → correo autorizado → cuentas del cliente), con hallazgo por celda.
- `DeclarationMaterializer`: con la validación en verde crea `Declaration` (atribuida al cliente) → `DeclaredBags` (totales por moneda; `TotalDeposits` es rollup y se deja a Dataverse) → `DeclaredDeposits` → `DeclaredChecks`.
- Servicio con tracing explícito etapa por etapa (regla de la casa) incluida la descarga del archivo por bloques (`InitializeFileBlocksDownload`).
- **44 tests verdes**, incluido el parser leyendo la plantilla REAL v1.1.0 del repo.

### Verificado E2E en DEV

- Positivo: plantilla llena (CIF, 2 depósitos, 1 cheque) → `Pass`, correo `Valido/Aceptado` y **atribuido**, árbol completo de declaración con totales por moneda y cheque vinculado.
- Negativo: efectivo negativo, cuenta ajena y cheque huérfano → 3 hallazgos **con pestaña, celda y fila** (`Depositos!O7`, `Cheques!A3`, `Depositos!K7`) y mensajes de negocio renderizados.

## [0.8.0] - 2026-08-05

Fase B del motor de validación: primer backend C# del proyecto, desplegado y verificado E2E en DEV.

### Added

- `src/backend/`: motor de validación en C# — `CashHub.Validation` (netstandard2.0, lógica pura testeable) con el engine (orden, dependencias anti-ruido, aislamiento de excepciones por estrategia, renderizado de placeholders, acumulación de TODOS los errores) y las 6 estrategias de sobre (VR-01…VR-06); `CashHub.Plugins` (net462 firmado, dominio compilado adentro para registro de una sola dll) con el Custom API `sanic_CHValidateReceivedEmail` y la persistencia de runs/hallazgos/estado del correo.
- **30 tests xunit verdes** (motor + matriz pass/fail de cada regla + integración del sobre completo) y workflow de CI (`.github/workflows/ci.yml`).
- `scripts/register-validation-plugin.sh`: registro idempotente por Web API de assembly, plugin type, Custom API (parámetro + 3 respuestas) y paso asíncrono en Create de `ReceivedEmails`.
- Smoke E2E en DEV: correo inválido → validado automáticamente en ~60s → `Invalido/Denegado/Auto`, `ErrorText` con los 3 errores renderizados, run + resultados por regla, y las reglas sin estrategia contenidas por la cadena de dependencias.

### Notas

- Hasta la fase C (parser + reglas de contenido), un correo con sobre VÁLIDO queda contenido por la cadena (VR-07+ dependen del parseo) — nunca un "Válido" a medias.
- Navigation properties de las entidades de Custom API son PascalCase (`PluginTypeId`, `CustomAPIId`); los entity sets de Dataverse pluralizan a ciegas (`...emailses`, `...runses`).

## [0.7.0] - 2026-08-05

Fase A del motor de validación de correos: diseño y parametrización. Core en 1.4.0.1.

### Added

- Diseño completo del motor (`docs/desarrollo/08-motor-validacion.md`): arquitectura de 3 piezas (transporte mínimo en Power Automate, motor como Custom API en C#, respuesta), estados del correo sobre los picklists existentes, contrato de reglas paramétricas con dependencias, detección de fallos por síntoma (barrido + heartbeat + semáforo), y nuevo oficio de la Email Validation Console (contingencia/salud/administración — las 23 reglas Power Fx se retiran).
- Seed de las 27 reglas de validación (`data/seed/50-validationrules.json`): las 26 de negocio + VR-27 (guardián de plantilla oficial, apagable), con orden de ejecución, parámetros (JSON donde aplica), mensajes con placeholders `{hoja} {celda} {fila} {valor} {esperado}` redactables por negocio, y cadena de dependencias anti-ruido — sembradas y verificadas idempotentes en DEV.
- `ValidationResults` extendida con `Hoja`, `Celda`, `Fila` y `Detalle` — un hallazgo por fila para que el correo de respuesta liste todos los errores con su ubicación exacta en el Excel.
- Resolver `@lookup:` en el script de seed (referencias entre filas sembradas, con comparación idempotente contra el `_value`).

## [0.6.0] - 2026-08-05

Tanda 4: UX de Client Administration y UI de Cuentas. Apps en 1.4.1.0, Core en 1.3.0.2.

### Added

- Pestaña **Cuentas** en ClientInformation: grilla de cuentas del cliente (número, moneda, estado, indicador de RFC pendiente), command bar con permisos de área y alta vía RFC (formCuentas con tarjetas de número y moneda). Ciclo completo verificado E2E: alta → aprobación → materialización con moneda por `sanic_optionsetmap` y cliente por Lookup.
- Badges de color por estado en el dashboard RFC (`ColCellType: tag` del Creator Kit).
- Indicador "RFC en curso" por fila en clientes, sucursales y correos (colección precargada, sin N+1).
- Campo `sanic_recordstatus` estándar en la tabla de Cuentas (el flujo genérico de creación lo asigna en todo alta; el `sanic_chchrecordstatus` heredado del clon queda deprecado).

### Fixed

- **Bug latente de materialización de Lookups**: el catálogo de campos guardaba `sanic_client` en minúsculas, pero los `@odata.bind` exigen el casing del navigation property (`sanic_Client`) — el alta de sucursales y correos vía app habría fallado siempre en la materialización. Corregido en seed para las 3 tablas.
- Pluralización real de Dataverse en el catálogo (`sanic_chtblclientbranchs`, sin "e").
- Templates de campo agregados a los PCF dataset para que las columnas calculadas viajen en el dataset.

## [0.5.0] - 2026-08-05

Tanda 3: esquema de cuentas y atribución de declaraciones. Core en 1.3.0.1, Flow en 1.3.0.0.

### Added

- Tabla `sanic_CHTblClientAccount`: número de cuenta (texto, alternate key única a nivel banco), cliente, moneda (option set global), estado — integrada al modelo RFC (catálogo de campos con `sanic_optionsetmap` para Moneda, permisos por área, casos en Activate/Deactivate; los flujos Create/Apply ya eran genéricos).
- Alternate key `(sanic_name, sanic_client)` en `ClientEmail` — un correo puede pertenecer a varios clientes, el par es único (decisión "el correo autoriza, el contenido atribuye"). Creadas vía `scripts/create-alternate-keys.sh` y verificadas funcionalmente.
- Lookup `sanic_Client` de atribución en `Declaration` y `ReceivedEmails`.
- Plantilla `CH-TPL-Depositos` v1.1.0 con `ClientCIF` en la hoja Metadata.
- Permisos RFC sembrados con los GUIDs reales de AAD/Teams de negocio (8 filas, Banca de Empresas y Bóveda).

### Fixed

- `ClientEmail.sanic_name` MaxLength 850→320 (bloqueaba la alternate key por el límite de índice de 1700 bytes).
- `EntitySetName` de la tabla nueva corregido (el clon heredó el plural de la entidad modelo y colisionaba con el endpoint de DepositHistory).

## [0.4.0] - 2026-08-05

### Added

- Mecanismo de seed data: datos de configuración versionados en `data/seed/` y siembra idempotente por ambiente con `scripts/seed-data.sh` (upsert por clave natural, resolución en vivo de URLs de canvas apps, omisión con advertencia de configuración pendiente).
- Datasets sembrados en DEV: catálogo de campos del RFC (6), catálogo de apps del Hub (7, con URLs reales del ambiente), denominaciones NIO/USD/EUR (39). Los permisos RFC por área (6) quedan definidos pero pendientes de los GUIDs de grupos AAD/Teams de negocio.
- Documentación en `docs/desarrollo/07-seed-data.md`.

## [0.3.0] - 2026-08-05

Tanda 2 de mejoras de Client Administration (robustez del motor RFC). Soluciones en 1.2.0.0.

### Added

- `CH-Flow-RFCWatchdog`: flujo programado (cada 15 min) que detecta RFCs en `Approved` más allá del timeout configurado y los marca `Failed` con auditoría; la notificación de Teams sale automáticamente vía `NotifywhenRFCimplementationfinish`.
- Variable de entorno `sanic_CHEVRfcApplyTimeoutMinutes` (default 30) para el timeout del watchdog.
- Caso `OptionSet` implementado en la conversión de tipos de `Createclientdata`/`Applyclientdatachanges` (contrato: `sanic_optionsetmap` = JSON etiqueta→valor).

### Changed

- `Createclientdata`/`Applyclientdatachanges` reestructurados con patrón Try/Catch: cualquier fallo — incluido el primer paso — marca el RFC `Failed` con auditoría (antes quedaba en `Approved` para siempre, sin notificación).
- Guard de líneas: un RFC aprobado sin líneas de cambio pasa a `Failed` sin crear el registro destino (verificado E2E: antes creaba un registro vacío y marcaba `Applied`).
- `retryPolicy` exponencial explícito en las 47 acciones Dataverse de los 4 flujos de materialización.
- Conflict-check de la canvas app corregido: exige mismo campo + mismo registro destino + RFC distinto (antes bloqueaba RFCs de clientes sin relación entre sí).

### Fixed

- Restaurado el RootComponent de la canvas app Client Administration (eliminado por error en la limpieza de la model-driven) y purgadas las asociaciones `appaction_appactionrule_classicrulesset` residuales de `Customizations.xml` que rompían el import.

## [0.2.0] - 2026-08-04

Tanda 1 de mejoras de Client Administration (seguridad). Soluciones en 1.1.0.0.

### Added

- Roles de seguridad `CH-SR-ClientAdminContributor` y `CH-SR-ClientAdminApprover` con privilegios explícitos sobre las 9 tablas del dominio cliente: solo lectura sobre los maestros (escribe únicamente la cuenta de servicio), creación/edición de solicitudes propias para Contributor y edición global de solicitudes para Approver.
- Campo `sanic_changestatus` en la cabecera del RFC (reemplaza a `cr140_changestatus`, de publisher ajeno), con valores alineados al esquema de la línea.
- Runbook de migración `docs/desarrollo/06-runbook-migracion-cr140.md` y script `scripts/migrate-cr140-changestatus.sh` con mapeo explícito de valores.

### Changed

- Los botones Aprobar/Rechazar de la canvas app Client Administration ahora exigen que el aprobador sea distinto de quien creó la solicitud (cierra la auto-aprobación de RFCs).
- Los 6 flujos del subsistema RFC pasan a operar sobre `sanic_changestatus` (cutover coordinado según runbook).

### Removed

- Model-driven app de Client Administration completa (decisión de negocio): AppModule, sitemap, command library y los 9 web resources de formulario (todos con validaciones desconectadas); limpiado el Quick Create de la línea de RFC que los referenciaba.

## [0.1.0] - 2026-08-04

### Added

- Fuente desempaquetado de las tres soluciones de Power Platform (`solutions/unpacked/`):
  - CashHubCore 1.0.0.5 — 38 tablas, 6 roles de seguridad, 3 option sets globales, 19 variables de entorno.
  - CashHubFlow 1.0.0.6 — 19 flujos de Power Automate.
  - CashHubApps 1.0.0.12 — 8 canvas apps por rol, 1 model-driven app y bibliotecas de componentes, con fuente YAML extraído de cada `.msapp`.
- Análisis técnico inicial del sistema as-is (`docs/analysis/`): síntesis de punta a punta, modelo de datos, flujos, apps operativas y administrativas, con hallazgos priorizados de seguridad, integridad de datos y calidad.
- Documentación por audiencia siguiendo el modelo del repo sigil: `docs/referencia/`, `docs/desarrollo/` y `docs/guias/`.
- Estructura base del repositorio: README, CHANGELOG y .gitignore.
