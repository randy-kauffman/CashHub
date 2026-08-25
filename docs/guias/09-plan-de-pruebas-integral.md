# Plan de pruebas integral — CashHub de 0 a 100 (certificación pre-producción)

Recorrido completo del sistema en el orden real del negocio: **nace un cliente →
declara por correo → el banco recibe → recuenta → supervisa → digita → acredita
→ se traza**. Cada módulo tiene su batería propia; al final, el **hilo de oro**
(un depósito nuevo atravesando TODO el sistema) y el **checklist Go/No-Go**.

Esta guía **orquesta** las baterías que ya existen y están verificadas — no las
duplica:

| Batería existente | Cubre | Se usa en |
|---|---|---|
| [08-plan-de-pruebas-validacion.md](08-plan-de-pruebas-validacion.md) | Motor de 28 reglas, correos de respuesta, contingencia | Módulo B |
| [../recounting-rebuild/06-escenarios-qa.md](../recounting-rebuild/06-escenarios-qa.md) | Escenarios sembrados de Recounting v2 (homónimas, sin declaración, multi-moneda) | Módulo D |
| [../supervisor-rebuild/09-entrega-fase2-qa.md](../supervisor-rebuild/09-entrega-fase2-qa.md) | Checklist por pantalla de Supervisor v2 | Módulo E |
| [../tracker-rebuild/07-guia-de-pruebas.md](../tracker-rebuild/07-guia-de-pruebas.md) | Delegación, funcionalidad y performance del Tracker | Módulo G |

**Convenciones**: cada caso se registra `[ok]` o `[FALLA + captura]`. Un caso
marcado 🔴 es **bloqueante** para salir a producción; ⚠️ es "documentar y
decidir". Los identificadores de caso son estables: citalos así en los reportes.

> **Nota sobre los manuales 01–07**: describen la semántica del proceso, que
> sigue vigente, pero varias apps fueron reconstruidas (Reception v2, Recounting
> v2, Supervisor v2, Digitization v2, Hub v2, Tracker). Donde el manual y la app
> difieran en pantallas/nombres, manda la app v2.

---

## §0 — Pre-vuelo del entorno (antes de probar NADA)

Si algo de esta sección falla, no tiene sentido correr el resto: vas a cazar
fantasmas de configuración, no bugs.

### 0.1 Flujos Power Automate 🔴

En make.powerautomate.com → Soluciones → CashHubFlows, verificar **Activado**:

| Flujo | Debe estar | Por qué |
|---|---|---|
| CH-Flow-IngestEmails | ON | Sin él no entra ningún correo (Módulo B) |
| CH-Flow-ProcessEmail | ON | Hijo de la ingesta |
| CH-Flow-MailboxSweep | ON | Red de seguridad + heartbeat |
| CH-Flow-Reply to client email | ON | Sin él el cliente nunca recibe respuesta |
| CH-Flow-Create client data | ON | Alta de cliente/sucursal/correo por RFC |
| CH-Flow-Apply client data changes | ON | RFC de tipo Update (fue reparado y reactivado — verificar que siga activo) |
| CH-Flow-Activate row | **verificar** | RFC de tipo Activate. Si está OFF, el caso CA-08 va a FALLAR por diseño |
| CH-Flow-Deactivate row | **verificar** | Ídem para Deactivate (caso CA-07) |
| CH-Flow-Notify reviewers | **verificar** | Notificación Teams a aprobadores (CA-10) |
| CH-Flow-Notify when RFC implementation finish | ON | Notificación de cierre de RFC |
| CH-Flow-RFCWatchdog | ON | Cierre de RFCs atascados (CA-12) |
| CH-Flow-DownloadData | ON | Descarga del CSV del lote (Módulo F) |
| CH-Flow-Process XML Files | **verificar** | Lectura de máquina contadora (RE-06). Si está OFF, ese botón de Recounting falla |

> Regla de despliegue: si un flujo se activó por API/como SP, el trigger puede
> no estar registrado — reactivar impersonando al owner (gotcha conocido del
> proyecto).

### 0.2 Backend y guards 🔴

- [ ] Todos los pasos `CashHub.*` en `sdkmessageprocessingsteps` con
      `statecode = 0` (Active), **cero Disabled**. En particular
      `CashHub.GuardReception: Update de sanic_chtblapprovalrequest` (el
      cuatro-ojos), que se apaga temporalmente para QA con
      `scripts/qa-approve-pending.py` — jamás debe llegar apagado a producción.
- [ ] Suite de tests del backend en verde: `dotnet test src/backend` (618+).
- [ ] Custom APIs registradas: `sanic_CHValidateReceivedEmail`,
      `sanic_CHWorkloadByCashier`, `sanic_CHPreviewTransactionCut`,
      `sanic_CHRemoveFromCut`.

### 0.3 Datos y configuración 🔴

- [ ] Las 28 reglas sembradas: `CH-Tbl-ValidationRules` con VR-01..VR-28 activas.
- [ ] Plantillas de correo activas: `CH-Tbl-EmailTemplate` → `reply-accepted` y
      `reply-rejected` con `IsActive = Sí`.
- [ ] `CH-Tbl-Parameter` con los grupos AAD approver sembrados.
- [ ] Variables de entorno pobladas: buzón compartido
      (`sanic_CHEVSharedMailboxAddress`), grupo Teams de operaciones
      (`sanic_CHEVOpsTeamsGroupId`), timeout RFC
      (`sanic_CHEVRfcApplyTimeoutMinutes`), URL de Client Admin.
- [ ] Cliente de referencia sembrado: **Distribuidora El Porvenir, S.A.** (CIF
      `0012024001`, cuentas `100200111` NIO / `100200112` USD) con un remitente
      autorizado que sea TU buzón de pruebas.

### 0.4 Seguridad y acceso 🔴

- [ ] Roles asignados por app: recepcionista, cajero, supervisor, digitador
      (`CH-SR-Digitizer`), tracker (`CH-SR-DepositsTracker`), admin de clientes
      (Contributor y Approver **en usuarios distintos** — lo vas a necesitar
      para el cuatro-ojos).
- [ ] Grupos AAD de aprobadores con miembros reales y sus AAD group teams
      provisionados (`scripts/provision-approver-teams.sh`).
- [ ] Perfiles FLS de `sanic_accountnumber` asignados (solo System
      Administrator y "CashHub | FLS | Supervisor Cuentas" escriben cuenta).
- [ ] Catálogo del Hub: una fila por app, URL `apps.powerapps.com`, filas de
      `CH-Tbl-AppCatalogAccess` por grupo.

### 0.5 Higiene de apps ⚠️

En Studio, por cada app: **App Checker → Delegación en CERO warnings** (salvo
los 2 declarados de Supervisor/Aprobaciones hasta el swap a espejos) y límite
de filas documentado (Supervisor viaja en 2000, el resto 500).

---

## §1 — Módulo A · Administración de clientes (el 0 absoluto)

Apps: **Client Administration** (canvas + model-driven). Actores: un usuario
**Contributor** y un usuario **Approver** (distintos — el guard lo exige).

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| CA-01 🔴 | Alta de cliente desde 0 | Contributor: Clients → Nuevo → completar datos → guardar | NO se crea el cliente: se crea un RFC en **Submitted** con una línea por campo. El cliente (si se pre-crea) nace con `recordstatus = PendingApproval` |
| CA-02 🔴 | Aprobación del alta | Approver (OTRO usuario): RFC → ChangeRequestDetails → Aprobar + comentario | Comentario obligatorio; RFC pasa a Approved; `CH-Flow-Create client data` lo materializa: cliente creado con `recordstatus = Active`, RFC → **Applied**, `targetrecordid` y `RelatedClient` poblados en RFC y bitácora |
| CA-03 🔴 | Alta de sucursal | Contributor: ClientInformation del cliente nuevo → agregar sucursal → RFC → aprobar | Sucursal creada Active; espejo `sanic_clientnamemirror` estampado (verificar por Web API) |
| CA-04 🔴 | Alta de correo autorizado | Ídem con un correo (el que va a usar el Módulo B) | `CH-Tbl-ClientEmail` Active. **Este correo es prerrequisito del hilo de oro** |
| CA-05 🔴 | Alta de cuenta | Ídem con cuenta + moneda | Cuenta Active; etiquetas correctas ("Numero de cuenta", "Moneda") |
| CA-06 | Update de un campo | Contributor: editar teléfono del cliente → RFC → aprobar | `Apply client data changes` escribe el campo, línea Applied, RFC Applied |
| CA-07 🔴 | Desactivar cliente | Acción Desactivar → RFC tipo Deactivate → aprobar | `recordstatus = Inactive`. Un correo del cliente desactivado debe REBOTAR en el Módulo B (VR-03/25) |
| CA-08 🔴 | Reactivar cliente | Acción Activar → RFC tipo Activate → aprobar | `recordstatus = Active` de vuelta; el correo vuelve a pasar |
| CA-09 🔴 | Auto-aprobación bloqueada | El MISMO Contributor intenta aprobar su RFC (probalo también por Web API con `changestatus = Approved`) | Rebota con 400 del `GuardChangeRequestApprovalPlugin`: creador ≠ resolutor y membresía del grupo Approver se exigen server-side |
| CA-10 | Notificaciones Teams | Enviar un RFC / esperar su cierre | Card a los aprobadores al Submitted (si Notify reviewers está ON) y card de resultado al creador y aprobador al Applied/Failed/Rejected |
| CA-11 | RFC en conflicto | Con un RFC pendiente sobre un campo, intentar otro RFC sobre el MISMO campo del mismo registro | La app bloquea el envío |
| CA-12 | Watchdog | Dejar un RFC en Approved con su flujo aplicador apagado (solo en DEV) | A los ~30 min el RFCWatchdog lo marca **Failed** con nota — jamás queda Approved eterno |
| CA-13 | Rechazo | Approver rechaza con comentario | RFC Rejected, nada se aplica, notificación de cierre llega |
| CA-14 ⚠️ | Option set en RFC de Update | RFC que cambie un campo de lista de opciones → aprobar | Verificar EXPLÍCITAMENTE que el campo quedó escrito (defecto histórico reparado; confirmar el fix en vivo) |
| CA-15 | Línea fallida honesta | Forzar un fallo de aplicación (p. ej. lookup a registro borrado) | La línea queda **Failed** (159460005), NO Applied — la bitácora no miente |

---

## §2 — Módulo B · Pipeline de correo (declarar, rechazar, cuarentena, no respondidos)

Prerrequisito: CA-04 (tu buzón como remitente autorizado). La batería fina del
motor está en la **[guía 08](08-plan-de-pruebas-validacion.md)** — corrección:
corré sus 4 bloques completos. Acá se agrega lo que la 08 no cubre.

### 2.1 La guía 08, obligatoria 🔴

- [ ] **Bloque A** (A1–A3): aceptados + correo verde + declaración materializada.
- [ ] **Bloque B** (B1–B12): rechazos de **contenido** (el Excel) con hoja/celda.
- [ ] **Bloque C** (C1–C7): rechazos de **sobre** (asunto, adjuntos, formato,
      remitente, plantilla, corrupto).
- [ ] **Bloque D** (D1–D6): contingencia (reproceso, huérfanos, plantillas,
      duplicados).

### 2.2 Triage de remitentes — cuarentena y no respondidos 🔴

El motor clasifica al remitente combinando VR-03 (remitente registrado), VR-27
(plantilla oficial) y VR-25 (CIF conocido). **Solo "Registrado" recibe
respuesta** — a los demás NO se les responde (responder confirmaría a un
tercero que ese CIF es cliente del banco).

| # | Caso | Cómo provocarlo | Esperado |
|---|---|---|---|
| CO-01 🔴 | Registrado | Remitente autorizado + cualquier caso A/B de la guía 08 | Clasificación "Registrado", respuesta enviada, correo movido a carpeta **Procesados** |
| CO-02 🔴 | Cliente sin registrar | Remitente NO autorizado + plantilla oficial + CIF válido | Clasificación "Cliente sin registrar". **SIN respuesta al remitente**. Queda para revisión de Banca de Empresas (visible en la Console). `sanic_triagenotes` cita los VR concretos |
| CO-03 🔴 | Sospechoso | Remitente NO autorizado + plantilla oficial + CIF inexistente | Clasificación "Sospechoso": sin respuesta, correo movido a carpeta **Cuarentena** en Outlook, alerta |
| CO-04 🔴 | No corresponde | Remitente NO autorizado + adjunto que no es la plantilla (o sin adjunto) | Clasificación "No corresponde": sin respuesta, a **Cuarentena** |
| CO-05 🔴 | Respuesta fallida (no respondido) | Romper temporalmente el envío (p. ej. desactivar Reply to client email) y mandar un caso A | El correo queda **Aceptado** pero `EmailReplyStatus = Pendiente`; al reactivar, `sanic_errortext` acumula "[SISTEMA] No se pudo enviar…" si falló; la **Console ofrece reenviar** y el reenvío llega |
| CO-06 | Pendientes se auto-sanan | Crear un ReceivedEmail Pendiente sin corrida (guía 08 D3) | El MailboxSweep lo re-valida en ≤10 min |
| CO-07 | Cambio solo de asunto no dispara respuesta | Editar únicamente `sanic_replysubject` de un correo | El flujo de respuesta NO se dispara (limitación conocida del filtro — documentar, no es bug nuevo) |

### 2.3 Email Validation Console (contingencia)

- [ ] CO-08: en un día sano, con el pipeline ON, la bandeja de la Console está
      **vacía** — todo entra y se responde solo.
- [ ] CO-09: los casos CO-02/03/04 aparecen en la Console con su triage y
      permiten resolver manualmente (override / reproceso vía Custom API).
- [ ] CO-10: heartbeat `ingesta-correos` en `CH-Tbl-SystemHeartbeat`
      actualizándose cada ~10 min (el semáforo de la Console).

---

## §3 — Módulo C · Reception (el lote llega a la esclusa)

App: **Reception v2** (`scrCaptura` + `scrLotes`). Actor: oficial de
esclusa/bóveda. Estados de lote: **En progreso → Recibido → En bóveda**; de
tula: **Recibida en esclusa → Almacenada en bóveda**.

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| RC-01 🔴 | Lote completo | scrCaptura: transportadora + 2 planillas (cliente/sucursal del Módulo A) + tulas escaneadas en cada una → guardar → validar | Lote **Recibido**; planillas y tulas persistidas; guardado planilla por planilla sin duplicar |
| RC-02 🔴 | Scan-first | Escanear 5 tulas seguidas en la caja de escaneo | El foco RETORNA a la caja tras cada escaneo; corrección = borrar + re-escanear |
| RC-03 🔴 | Dup-check de tula | Escanear un código repetido en la misma planilla y en otra planilla del lote; luego uno que exista en OTRO lote | Rebota con aviso en los tres casos (dup intra e inter-planilla, delegable — la encuentra aunque haya miles) |
| RC-04 | Guardado delta | Reabrir un lote En progreso, tocar una sola planilla, guardar | Solo viaja lo modificado; sin cambios = toast y cero llamadas |
| RC-05 🔴 | Combo de sucursales | Buscar sucursal por nombre de sucursal Y por nombre de cliente | Encuentra más allá de las 500 filas (espejo `ClientNameMirror` delegable) |
| RC-06 🔴 | Mover a bóveda | scrLotes: seleccionar el lote Recibido → Mover a bóveda | Todas las tulas pasan a **Almacenada en bóveda**, lote a **En bóveda**. Probar también el lote gordo del stress (600 tulas) |
| RC-07 🔴 | Edición post-cierre = aprobación | En un lote Recibido/En bóveda: agregar, eliminar, remapear y editar una tula; modificar número de planilla | NO se aplica directo: se crea la solicitud y la tula queda "Pendiente de revisión por supervisor - esclusa" hasta que Supervisor resuelva (Módulo E) |
| RC-08 🔴 | Última tula intocable | Intentar eliminar/remapear la ÚNICA tula de una planilla | Bloqueado — una planilla no puede quedar vacía |
| RC-09 🔴 | Guard server-side | Por Web API: crear una tula naciendo en "Asignada a cajero"; transición ilegal de estado de lote | Rebota con 400 del `GuardReceptionPlugin` — la app no es la única defensa |

---

## §4 — Módulo D · Recounting (el cajero cuenta)

App: **Recounting v2** (`scrSesion`, `scrTulas`, `scrConteo`). Prerrequisito:
tulas En bóveda (RC-06) y los escenarios sembrados de
**[06-escenarios-qa.md](../recounting-rebuild/06-escenarios-qa.md)**.

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| RE-01 🔴 | Sesión en mesa propia | Abrir sesión del día en TU mesa asignada | Auto-aprobada **por el servidor** en el mismo Create (no interviene supervisor) |
| RE-02 🔴 | Sesión en mesa ajena | Abrir sesión en otra mesa | Solicitud "Crear sesión de recuento"; pantalla bloqueada "Esperando aprobación" hasta que Supervisor resuelva. Probar la rama **Rechazado** también: la app lo muestra como estado de primera clase, no queda colgada |
| RE-03 🔴 | Homónimas (E2) | Escanear `ST990001` | Diálogo con 3 candidatas; SOLO la de bóveda es seleccionable |
| RE-04 🔴 | Tula sin declaración (E3) | Escanear `ST990002` | Solicitud "Tula sin declaración" → tras aprobación del supervisor se puede contar |
| RE-05 🔴 | Conteo limpio | Contar una tula declarada cuadrando exacto → cerrar | Totales posteados, cheques **Recontado**, tula **Recontada**, y la **transacción de Depósito se crea directa** sin supervisor (idempotente: no hay duplicados si se reintenta) |
| RE-06 ⚠️ | Máquina contadora | Botón de lectura XML con la serie de tu máquina | Denominaciones cargadas desde el XML. **Depende de CH-Flow-Process XML Files activo** (§0.1) — si está OFF, decidir: activarlo o sacar el botón antes de producción |
| RE-07 🔴 | Multi-moneda con diferencias y cheques (E4) | Guion §5.3 de la guía QA: NIO con faltante, cheque declarado sin marcar, cheque manual extra, USD y EUR con diferencia | Diálogo de cierre con hallazgos POR MONEDA; solicitud "Tula con diferencia" con el HTML de detalle en `RequestDetails`; tula "Pendiente de revisión por supervisor - recuento"; cajero bloqueado en esa tula |
| RE-08 🔴 | Tula gorda (E1) | Abrir `ST000517` (612 cheques) | Abre y pagina sin colgarse; los 612 llegan (delegación por espejo) |

---

## §5 — Módulo E · Supervisor (el punto de control)

App: **Supervisor v2** (`scrResumen`, `scrAprobaciones`, `scrTulas`,
`scrCheques`). Complemento del checklist de
**[09-entrega-fase2-qa.md](../supervisor-rebuild/09-entrega-fase2-qa.md)**.

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| SU-01 🔴 | Bandeja completa | Generar y resolver **los 10 tipos** de solicitud: Agregar / Eliminar / Remapear / Editar tula, Modificar nº de planilla (Módulo C), Crear sesión, Primera tula en planilla, Tula sin declaración, Tula con diferencia, Tula recontada sin declaración (Módulo D) | Cada tipo aparece con su detalle; Aprobar aplica el efecto real; Rechazar revierte donde aplica (verificar el estado de la tula tras cada rechazo — incluida "Editar tula", que históricamente quedaba atrapada) |
| SU-02 🔴 | Cuatro ojos | Intentar resolver una solicitud creada por VOS mismo; y por un usuario fuera del grupo AAD aprobador | Rebota server-side (guard en `ApprovalRequest`), no solo botones ocultos |
| SU-03 🔴 | Tula con diferencia — el caso sagrado | Aprobar la solicitud de RE-07 revisando la caja por componente × moneda (Sobrante/Faltante SIN compensar) + motivo obligatorio | Transacciones posteadas: **Depósito + Sobrante/Faltante según corresponda por depósito**; tula Recontada; cheques Recontado; detalle "Cerrado con diferencias". **Sin deshacer** — verificar montos contra E4 antes de aprobar |
| SU-04 🔴 | Doble aprobación concurrente | Abrir la misma solicitud en 2 sesiones y aprobar en ambas | La segunda revalida el estado y NO postea duplicado |
| SU-05 | Resumen de operaciones | scrResumen con el datepicker (abre en HOY) | KPIs y conteos por cajero/sesión consistentes con lo hecho en el Módulo D |
| SU-06 🔴 | Localización de tulas | scrTulas: segmentado con los 6 estados + búsqueda por número | Los 6 estados aparecen (incluidos "Asignada a cajero" y "Pend. revisión recuento"); "hasta hoy" incluye hoy; solo lectura |
| SU-07 🔴 | Envío de cheques a digitación | scrCheques: multiseleccionar cheques Recontado → Enviar | Solo los Recontado son seleccionables; reporte "N enviados, M rechazados"; sellos de quién/cuándo; es el paso que habilita el Módulo F |
| SU-08 | Reenvío | Reenviar cheques ya enviados | Pide confirmación con conteo |

---

## §6 — Módulo F · Digitization (rumbo al core)

App: **Digitization v2** (`scrCorte`, `scrManual`, `scrLotes`, `scrCheques`).
Prerrequisito: transacciones creadas por los Módulos D/E y cheques enviados por
SU-07. Estados de lote: **Generado → Ingresado → Aplicado**.

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| DI-01 🔴 | Rail de cajeros | Abrir scrCorte con volumen real (hay 7.000+ tx del stress) | El conteo por cajero es server-side (`sanic_CHWorkloadByCashier`): números EXACTOS aunque superen 500/2000, en <1 s |
| DI-02 🔴 | Preview del corte | Seleccionar cajero → Realizar corte | El diálogo muestra conteo y totales por moneda del `PreviewTransactionCut` — deben coincidir EXACTO con lo que el corte efectivamente agrupa |
| DI-03 🔴 | Corte | Confirmar el corte | Lote **Generado** con todas las tx de efectivo puro del cajero; las tx con cheques NO entran (van a Manuales); totales del lote = suma server-side |
| DI-04 🔴 | Tx manuales | scrManual: aplicar una a una (flujo de teclado: Tab/Enter) | Orden por motivo; muestra los cheques de la tx (número, banco, monto); al marcar **Aplicado** avanza sola a la siguiente; "ver aplicadas" disponible |
| DI-05 🔴 | Sacar del lote | Sobre un lote **Generado** y sobre uno **Ingresado**: sacar una tx con motivo | `sanic_CHRemoveFromCut` la desasigna y **recalcula los totales del lote**; en lote Aplicado debe REBOTAR (guard server-side) |
| DI-06 🔴 | Descarga del CSV | scrLotes: Descargar lote | El CSV baja **con nombre y extensión correctos** desde la columna Archivo del lote (`sanic_csvcore/$value`); layout IBS de 14 columnas; el archivo queda archivado en el lote (trazabilidad) |
| DI-07 🔴 | Ciclo de vida del lote | Marcar Ingresado (solo visible en Generado) → marcar Aplicado (solo visible en Ingresado) | Transiciones válidas únicamente en ese orden; por Web API una transición ilegal rebota |
| DI-08 🔴 | Cheques | scrCheques: marcar Recibido / No recibido | Solo cheques "Enviado a digitación" aparecen; trazabilidad de quién envió y quién recibió |
| DI-09 | Filtros de lotes | Filtrar por estado y fecha | Correcto con los 18 lotes del stress (incluido el de 600 tx) |

---

## §7 — Módulo G · Deposits Tracker (la trazabilidad)

Corré la guía completa **[tracker-rebuild/07-guia-de-pruebas.md](../tracker-rebuild/07-guia-de-pruebas.md)** —
sus tres partes, en su orden:

- [ ] 🔴 **Parte 1 — Delegación**: los 3 chequeos estáticos + el protocolo del
      límite en 1 + los centinelas D2. *Cualquier D-falla es bloqueante: son
      datos escondidos en silencio.*
- [ ] 🔴 **Parte 2 — Funcionalidad** (F1–F12): búsqueda por los 4 tipos, drill
      completo con migas, estados de pill, correo con validaciones (Pasó /
      Falló / **Omitida**), respuesta al cliente renderizada.
- [ ] **Parte 3 — Performance**: presupuesto de queries + cronómetro.
- [ ] TR-01 🔴 (integración): buscar los registros creados por ESTE plan (el
      lote de RC-01, la tula de RE-07, el correo del hilo de oro) y verificar
      que la cronología cuente la historia completa hasta "acreditada en core
      (lote TB-…)".

---

## §8 — Módulo H · Hub (la puerta de entrada)

| # | Caso | Pasos | Esperado |
|---|---|---|---|
| HU-01 🔴 | Acceso por grupos | Entrar con usuarios de grupos distintos | Cada uno ve SOLO las apps de sus grupos (`checkMemberGroups` transitivo, en lotes); una app sin filas de acceso es pública |
| HU-02 | App única | Usuario con acceso a una sola app | Redirect directo con `Launch(..., Replace)` |
| HU-03 | Fail-open | Simular fallo de Graph (p. ej. sin consentimiento del conector) | Banner de aviso y el hub degrada mostrando — la autorización real la imponen los roles de Dataverse de cada app |
| HU-04 | Sin acceso | Usuario sin ningún grupo | Estado vacío "No tiene acceso a ninguna aplicación" |
| HU-05 🔴 | Guard del catálogo | Por Web API: escribir una URL http / host ajeno / `apps.powerapps.com.evil.com` en el catálogo; un GUID inválido o duplicado en accesos | Rebota con 400 (`GuardAppCatalogPlugin` / `GuardAppCatalogAccessPlugin`) |
| HU-06 | Orden y branding | Revisar tarjetas | Orden por `sanic_order`, glifos por dato (columna imagen), tooltips "Abrir <app>" |

---

## §9 — El hilo de oro: un depósito de 0 a 100 🔴

La prueba integral de verdad: **un solo depósito nuevo, sin datos pre-armados,
atravesando todo el sistema en un día**. Si este guion pasa limpio de punta a
punta, el sistema funciona como un todo — no solo como módulos.

1. **[Módulo A]** Alta del cliente "QA Producción, S.A." con CIF nuevo, 1
   sucursal, 1 cuenta NIO, 1 cuenta USD y TU buzón como correo autorizado
   (CA-01..05). Verificar Active.
2. **[Módulo B]** Enviar la plantilla oficial declarando 1 tula con 2 depósitos
   (NIO con 1 cheque BAC, USD solo efectivo), asunto "Pre-registro…". Esperar
   el **correo verde** con número de gestión y verificar la declaración
   materializada.
3. **[Módulo C]** Registrar el lote de recepción con la planilla del cliente y
   la tula declarada (mismo código). Validar → Recibido → **Mover a bóveda**.
4. **[Módulo D]** Abrir sesión en mesa propia, escanear la tula, enlazar la
   declaración y contar con una **diferencia deliberada de +500 NIO** y el
   cheque marcado. Cerrar → pendiente de supervisor.
5. **[Módulo E]** Con OTRO usuario: revisar la caja de diferencias (debe decir
   Sobrante 500 NIO), aprobar con motivo. Verificar transacciones: Depósito +
   Sobrante. En scrCheques, enviar el cheque a digitación.
6. **[Módulo F]** Realizar corte del cajero (la tx USD de efectivo entra al
   lote; la NIO con cheque va a Manuales). Aplicar la manual. Descargar el CSV
   del lote y verificar sus 14 columnas contra los montos. Marcar Ingresado →
   Aplicado.
7. **[Módulo G]** En el Tracker: buscar el número de gestión del correo, el
   lote, la tula. La cronología debe contar TODO: correo validado → recibida →
   bóveda → recontada con diferencia → aprobada → acreditada en core, con el
   pill "Acreditada" y el lote TB correcto.
8. **[Módulo B otra vez]** Reenviar el MISMO correo del paso 2: no debe crear
   nada (dedup). Desactivar el cliente (CA-07) y reenviar: debe rebotar.

**Cronometrá el recorrido completo** y anotalo: es tu línea base de operación.

---

## §10 — Go/No-Go de producción

### Criterios bloqueantes (cualquiera en FALLA = NO se sale)

- [ ] §0 pre-vuelo completo en verde.
- [ ] Todos los casos 🔴 de los módulos A–H en `[ok]`.
- [ ] El hilo de oro (§9) completo, con evidencia (capturas + número de gestión
      + CSV descargado).
- [ ] Cero warnings de delegación no declarados; cero pasos de guard Disabled.
- [ ] Decisión tomada y ejecutada sobre los "verificar" del §0.1 (flujos de
      Activate/Deactivate/Notify reviewers/XML): **activados y probados, o la
      función correspondiente retirada/documentada**. Producción no sale con
      botones que llaman flujos apagados.

### Riesgos conocidos a aceptar explícitamente (⚠️ — documentados, no bugs nuevos)

| Riesgo | Dónde | Mitigación |
|---|---|---|
| Ingesta/respuesta dependen de conexiones de Outlook del owner | Flujos de correo | Runbook de reactivación impersonando al owner; monitorear heartbeat |
| Estados Ingresado/Aplicado del lote son marcas manuales (sin confirmación de IBS) | Digitization | Procedimiento operativo + conciliación diaria contra el core |
| Campos fijos del CSV (identidad genérica de agencia) | DownloadData | Confirmado con negocio antes de salir |
| Rename de cliente no cascadea al espejo de sucursales | ClientBranch mirror | `backfill-clientbranch-mirror.py` programado/documentado |
| Colecciones acotadas del Tracker (techo 500 por registro) | Tracker D3 | Techos documentados; hoy nadie los roza |

### Registro de resultados

| Módulo | Casos | [ok] | [FALLA] | Bloqueantes abiertos | Firma / fecha |
|---|---|---|---|---|---|
| §0 Pre-vuelo | — | | | | |
| A Client Admin | CA-01..15 | | | | |
| B Correo | 08:A–D + CO-01..10 | | | | |
| C Reception | RC-01..09 | | | | |
| D Recounting | RE-01..08 | | | | |
| E Supervisor | SU-01..08 | | | | |
| F Digitization | DI-01..09 | | | | |
| G Tracker | 07:P1–P3 + TR-01 | | | | |
| H Hub | HU-01..06 | | | | |
| §9 Hilo de oro | 8 pasos | | | | |

**Regla final**: se sale a producción con este documento COMPLETO, firmado, y
con los ⚠️ aceptados por escrito por negocio. No antes.
