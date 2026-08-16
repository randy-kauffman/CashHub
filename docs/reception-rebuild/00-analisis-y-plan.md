# CH-App-Reception v2 — Análisis y plan de reconstrucción

Fecha: 2026-08-15 · Fuente analizado: `solutions/unpacked/apps/CanvasApps/src/sanic_chappreception_9ff29` (sync 10-ago, commit `f5aa927`)
Auditorías completas en Engram: `reception/analisis-2026-08` (funcional), `reception/perf-delegacion-2026-08` (performance/delegación), `security/reception-app-posture` (seguridad), `reception/inventario-v1-2026-08` (inventario).

## 1. Por qué se reconstruye

La v1 funciona pero está construida sobre cuatro cimientos podridos:

1. **Commit parcial silencioso.** El 94% de los `Patch` corre sin `IfError`; `App.OnError` solo notifica, no aborta. Tres CRITICAL activos: `varSoloGuardar` pegado tras un fallo convierte "Cerrar lote" en no-op (N1); el trash de planilla lee `ThisItem.GUID` después de mutar la colección y deja huérfanos en Dataverse (N2); `OnSuccess` marca planillas guardadas aunque el guard rechace las bolsas (N3). El GuardReceptionPlugin server-side hace estos escenarios MÁS probables, no menos.
2. **Delegación rota con `DataRowLimit=500`.** Los dup-checks de tula/planilla, el `RemoveIf` de bolsas y MoverABoveda truncan en silencio a partir de 500 filas: duplicados que entran, borrados parciales, lotes "En bóveda" con tulas en esclusa. La adopción del espejo delegable `sanic_batchstatus` fue de UNA expresión; los espejos B-2/B-3 de Bags ni figuran en la metadata de la app.
3. **Controles de código.** 4× FluentDetailsList, 2× CommandBar, SearchBox, KeyboardShortcuts (PCF Creator Kit) + ExpandMenu/Dialog/Panel (librería PowerCAT). El override `GetMenuIconPath` del ExpandMenu mete 11.280 líneas de SVG por pantalla: el 79% del código fuente es una tabla de íconos.
4. **HTML de approval sin diseño e inyectable.** Las 5 variantes son un `<p>` con `<br>` que concatena TextInput crudo (inyección HTML confirmada); `RequestResources` es JSON armado a mano con `""""` — y el guard lo parsea para decidir ownership. El htmlViewer del supervisor tiene un parser de `<style>` que Reception nunca aprovechó.

## 2. Diseño v2 — decisiones

### 2.1 Pantallas (2)

| Pantalla | Reemplaza a | Rol |
|---|---|---|
| `scrCaptura` | CreacionLotesRecepcion | Crear/retomar lote, grilla de planillas, panel de tulas, validación, Guardar / Cerrar lote. |
| `scrLotes` | AdministracionDeLotes | Administración master-detail (tabs Lotes/Planillas), filtros, Mover a bóveda, 4 operaciones de tula + editar planilla con doble camino directo/aprobación. |

**Acceso** (decisión 15-ago): sin pantalla de guard ni validación de grupo en la app. El control de acceso es el sharing de la canvas app + el rol `CH-SR-Receptionist` de Dataverse — quien no tenga la app compartida no la abre, y quien no tenga el rol no ve datos (lo bloquea Dataverse). La seguridad de operaciones sigue siendo server-side (GuardReceptionPlugin).

**Navegación**: conmutador segmentado `[Captura | Lotes]` en el header (2 Botones clásicos sobre superficie redondeada, receta del Hub) — reemplaza al ExpandMenu. Navegación contextual: "Editar lote" en scrLotes carga el lote y navega a scrCaptura. Guarda de salida: si scrCaptura tiene cambios sin guardar, el conmutador pide confirmación (Descartar / Guardar y salir). `App.BackEnabled = false`.

### 2.2 Controles: 100% nativos

- **Fuera**: FluentDetailsList, CommandBar, SearchBox, KeyboardShortcuts, ExpandMenu, Dialog, Panel (PowerCAT/PCF). Cero code components.
- **Base clásica** (robustez): `gallery` vertical + labels de encabezado (las grillas), `label`, `icon`, `rectangle` (sin Radius — para superficies redondeadas se usa **Botón clásico** con `DisplayMode.Disabled`+`DisabledFill`, receta del Hub), **Botón clásico** para acciones (tiene Radius), `dropdown` clásico para estado, **Combo box clásico** para empresa/transportadora (búsqueda incluida).
- **Modernos puntuales** (permitidos, plataforma nativa): `TabList` (conmutador Lotes/Planillas), `DatePicker` ×2 (rango de fechas), `Spinner` (overlay de progreso). **Esos TRES y ninguno más** — el catálogo cerrado de `01` §0.3 es el contrato: `TextInput`/`NumberInput` modernos quedan FUERA (se usan los clásicos).
- Selección en grillas: `varLoteSel` / `varPlanillaSel` / `varTulaSel` (registro completo), fill condicional en la fila. Orden por columna: ícono toggle + `SortByColumns` con variable.

### 2.3 Datos: una consulta delegable por vista

Reglas duras (sin excepción):

- Prohibido `a.b = c` navegando a una **columna de datos** del registro relacionado en `Filter`/`LookUp`/`RemoveIf` sobre datasource. 🧨 **Corregido por el QA 16-ago**: la igualdad de **registro** (`ReceptionBatch = varLoteSel`) **NO COMPILA** — se compara **PK contra PK**: `Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = varLoteSel.'CH-Tbl-ReceptionBatch')`, que sigue siendo delegable (`01` §2.3, D-1 en detalle).
- 🧨 **Una vista NO puede ser el primer argumento de `Filter`** (`01` §2.5): va como **segundo** (`Filter(tabla, vista)`, sin predicado) o se elimina. `galLotesLot.Items` va contra la tabla pelada + `SortByColumns` explícito.
- 🧨 **Catálogo en colección SOLO si la tabla es estructuralmente acotada (< 500 filas)** — regla **D-10**, nacida de **L-23**. Si no, `Items` server-side con `IsSearchable`.
- Prohibido `CountRows(Filter(ds, …))` — usar espejo materializado.
- Prohibido `RemoveIf(ds, predicado)` — siempre `Remove(ds, registro)` con GUID explícito.
- Prohibido `Filter/LookUp/Search` en `Items` de controles dentro de galerías.
- Tulas de un lote: `Filter('CH-Tbl-Bags', ReceptionBatchID = varLoteSel)` vía espejo **B-2 `sanic_receptionbatchid`** (ya existe, mantenido por `BagReceptionMirrorPlugin`). **La app nueva debe regenerar la metadata de datasources para verlo.**
- Estado del lote en planillas: espejo `sanic_batchstatus` (ya existe).
- Cero `Refresh(ds)` **como paso automático tras un `Patch`**: se actualiza la colección local con el registro devuelto. Única excepción (regla D-5 de `01` §2.3, divergencia DL-10 aceptada): el botón ⟳ explícito de `scrLotes`, cuyas galerías maestras van atadas al datasource para no clavarse en `DataRowLimit`.
- Orden: un `Filter` sobre un datasource **no hereda** el `ORDER BY` de la vista ⇒ `SortByColumns` explícito en todo `Items` de galería maestra (regla D-9).
- `Concurrent()` en cargas independientes. `DataRowLimit` a 2000 como red de seguridad, no como solución.

### 2.4 Guardado sin timer-máquina-de-estados

Muere `'timer Submit'` + form oculto + `varCurrentRow/varFirstRow/varRowIndex/colPlanillasGuardadas`.

> 🧨 **Matizado por el QA 16-ago (L-25).** Lo que muere es el **timer como bucle**:
> una máquina de estados que iteraba fila por fila con `varRowIndex`. Lo que sí
> existe en la v2 es **un `Timer` de UN frame** (`tmrGuardarCap`, `Duration 50`,
> `Repeat false`), cuyo único trabajo es dejar que canvas **pinte el diálogo
> "Guardando…"** antes de bloquearse — y desde el cual navega *Guardar y salir*,
> porque los timers no corren en pantallas invisibles. Corre **una vez por click**,
> sin estado de iteración. Regla de olfato: **un `Repeat true` o un contador de fila
> en un timer de esta app = la v1 volviendo.** Receta `01` §7.11.C.

- **Guardar** (incremental): un `Patch('CH-Tbl-Receptions', ForAll(colPlanillas As P, {...}))` con tabla (una llamada), envuelto en `IfError` con decisión explícita; después un `Patch` con tabla para las tulas nuevas. Colecciones locales con **GUID real como clave** (muere la renumeración manual O(n³) y la paginación de 9: scroll nativo de galería).
- **Cerrar lote**: `Environment.sanic_CloseReceptionBatch(lote)` — **Custom API** server-side (Fase 0) que valida duplicados contra la tabla completa (mata los dup-checks truncados C1/C2) y transiciona el lote a `Recibido` en una transacción. Atomicidad real: o cierra todo o no cierra nada.
- Validación (6 reglas de la v1, se preservan): **una sola vez** en `App.Formulas` (named formulas `fxPlanillasInvalidas`, etc.), consumida por Validar/Guardar/Cerrar. Feedback: resaltado ámbar + panel de validación fijo en la pantalla (no solo Notify).
- Todo borrado destructivo con **diálogo de confirmación** (la v1 no tenía ninguno).

### 2.5 HTML de approval — rediseño (los 5 tipos)

- La app manda **un solo template** con bloque `<style>` (el htmlViewer del supervisor ya lo extrae y aplica): card con tipografía Segoe UI, encabezado con tipo de solicitud + lote, tabla `Campo / Valor actual / Valor solicitado`, colores de la familia (#C8102E acentos, neutros cálidos). Mockup incluido.
- **Todo dato interpolado pasa por un escape HTML** (named formula que reemplaza `& < >` con `Substitute`) — cierra la inyección.
- `RequestResources` se construye con **`JSON({...})`**, nunca concatenación. **Claves EXACTAS de la v1 se mantienen** (contrato con supervisor): Agregar=`{ReceiptGUID,BagID,BagGUID,ReceptionBatchGUID}`, Eliminar=`{GUID,BagPreviousState}`, Remapear=`{GUID,ReceiptID,ReceiptGUID,BagPreviousState}`, Planilla=`{ReceiptGUID,NewReceiptID}`, Editar=`{ReceiptGUID,BagID,ReceptionBatchGUID,BagGUID}`. La unificación GUID/BagGUID (hallazgo N10) queda para una fase coordinada con Supervisor, NO en este rediseño.

### 2.6 Fixes de comportamiento heredados (van en las guías)

- Reset de todo diálogo al abrirlo (`Reset` de inputs + combos — mata N6, remapeo silencioso).
- La X de un panel/diálogo **cancela**, nunca commitea (mata N4).
- Gates de habilitación consistentes con el control que la acción usa (mata N7) y con el estado del lote (mata N9).
- Sin rama muerta de `'En boveda'` en alta/edición de tula (mata N8): el BagStatus destino se calcula una vez, explícito por camino.
- Atajos de teclado: no se portan (KeyboardShortcuts es PCF; 4 de 8 ni funcionaban).

## 3. Fase 0 — backend (previo a las guías de pantalla)

| # | Qué | Por qué |
|---|---|---|
| B0-1 | Espejo `sanic_createdbyname` (texto) en ReceptionBatch, mantenido por plugin en Create | `StartsWith` delegable por autor en los filtros (mata M1) |
| B0-2 | Espejo `sanic_bagcount` (entero) en Receptions, mantenido por `BagReceptionMirrorPlugin` | Mata todos los `CountRows(Filter(Bags))` (corazón del N+1 A1) |
| B0-3 | Custom API `sanic_CloseReceptionBatch` | Cierre atómico + dup-check server-side sin truncamiento |
| B0-4 | Seguridad A-1: montos (`sanic_totalnio/usd/eur`), `sanic_recountingsession`, `sanic_bagdeclarationid` en filteringattributes del paso Update de Bags + tratarlos como congelables en bóveda | Montos editables por API en lote congelado |
| B0-5 | Seguridad A-2: chequeo de procedencia (`Depth > 1`) en `touchesOnlyTotals`/`touchesOnlyMirror` | Bypass de infraestructura abierto a usuarios |
| B0-6 | Seguridad A-3: estampar `sanic_solvedby`/`sanic_resolutiondatetime` en el plugin desde `InitiatingUserId` | El sello de quién aprobó hoy es dato del cliente |
| B0-7 | Auditoría ON en `CH-Tbl-Parameter` + sembrar `Security.ApproverGroup.Supervisor` en `provision-parameters-table.py` | La config del grupo aprobador cambia sin rastro; gap de ALM |
| B0-8 | Rol: quitar `prvReadTraceLog` Global; revisar privilegios Workflow | Superficie innecesaria |
| B0-9 | ✅ **CERRADA (16-ago)** — Columna espejo `sanic_ClientNameMirror` (String 850) en `sanic_chtblclientbranch`, mantenida por **`ClientBranchNameMirrorPlugin`** (Pre-operation 20, pasos Create y Update filtrado en `sanic_client`; dominio puro `ClientBranchNameMirrors`; backfill idempotente). **Desplegada y verificada e2e en DEV** | Habilita el **combo de sucursales server-side** de `02` §5.4 (**L-23**): sin el espejo, buscar una sucursal por el nombre de su empresa exigiría `Client.Name` dentro de un `Filter` — dot-walk, no delegable (D-1). Con él, `SearchFields ["sanic_name","sanic_clientnamemirror"]` delega por los dos campos. ⚠️ Sin cascada por rename de cliente (limitación documentada; el backfill re-ejecutable la cubre). ⚠️ `sanic_clientname` (la columna vieja de esa tabla) es **huérfana** y NO se usa |

(B0-4…B0-8 no bloquean las guías de pantalla; pueden correr en paralelo al vestido.
**B0-9 sí bloqueaba `02` §5.4 y ya está cerrada.**)

## 4. Pipeline de construcción

1. **Mockup aprobado** por el humano (este entregable).
2. **Guías** `docs/reception-rebuild/` con el skill `powerapps-screen-guides`, pipeline autor → R1 fidelidad vs mockup → R2 antagonista (agentes Opus): `01-app.md` (App, formulas, tema), `02-pantalla-captura.md`, `03-pantalla-lotes.md`.
3. **Scaffolding** en Studio por el humano (controles con nombre, sin propiedades).
4. **Vestido**: export → `pac canvas unpack` → agente viste el YAML → `pac canvas pack --overwrite` → bump `Solution.xml` → `pac solution pack` → `import --publish-changes` (flujo probado, Hub 1.30.0.7). Ícono: `docs/marca/icono-reception-*.png` (ya existe).
5. **Stress**: seeder de datos sintéticos (>2000 lotes, >5000 planillas, >10000 tulas, marca STRESS-RCP) + verificación de delegación en vivo y limpieza.
6. Commit + cierre.

## 5. Esquema verificado (15-ago, Entity.xml + DEV vivo) y ledger de divergencias

Verificación completa en Engram `reception/esquema-guias-2026-08`. Lo que cambia fórmulas:

- **B-2/B-3 EXISTEN en DEV** (`ReceptionBatchId` lookup y `ReceiptIdMirror` texto en Bags) aunque el Entity.xml del repo (4-ago) no los tenga: el repo está desactualizado. Las guías los usan; al agregar el datasource en Studio la metadata fresca los trae. Pendiente re-export del repo.
- `BatchStatus` (Receptions) es **entero**, no option set → comparar contra 159460000–159460003 crudos.
- `RequestType` es **texto libre** → strings exactos de la v1, cualquier typo crea un tipo fantasma.
- `ApprovalStatus` no tiene "Pendiente": pendiente = campo en blanco.
- Autonumbers que NO se patchean: `ReceptionBatch.Name` (`RB-{SEQNUM:5}`, maxlen 20), `ApprovalRequest.Name` (`AR-{SEQNUM:8}`).
- `Transportadora` es texto (se llena con el `Name` elegido de TransportCompanies).
- Radius disponible: Botón clásico (10), Entrada de texto clásica (5), Imagen, Contenedor (4). Rectángulo NO, Etiqueta NO.
- **NO existen** (backend Fase 0 pendiente): `sanic_createdbyname` (B0-1), `sanic_bagcount` (B0-2), Custom API `sanic_CloseReceptionBatch` (B0-3).
- **SÍ existe desde el 16-ago**: `sanic_ClientNameMirror` en `sanic_chtblclientbranch` (**B0-9**, §3) — la habilita el combo server-side de `02` §5.4. La columna vieja `sanic_clientname` de esa misma tabla es **huérfana** (`IsValidForCreate/Update = false`, `IsFilterable = false`): **no es reusable y no se usa**.
- El option set de `RecordStatus` en ClientBranch es **GLOBAL**: el literal Power Fx es `'CH-Ch-RecordStatus'.Active`, **no** `'RecordStatus (CH-Tbl-ClientBranches)'` (que no compila).

**Ledger de divergencias declaradas vs mockup:**

| # | Divergencia | Motivo |
|---|---|---|
| L-1 | El cierre de lote del mockup invoca una Custom API atómica; la guía lo construye client-side (Patch + IfError + validación local) | B0-3 no existe aún; la guía deja el bloque marcado para reemplazo |
| L-2 | El buscador de lotes del mockup filtra también por autor | Sin `createdbyname` (B0-1) el filtro por autor no delega; se omite hasta tener el espejo — búsqueda por lote/transportadora/planilla solamente |
| L-3 | El contador de tulas por planilla del mockup (chip en listas de scrLotes) | Sin `bagcount` (B0-2) no hay conteo delegable por fila; el conteo exacto vive solo en el detalle de la planilla seleccionada (una query por selección, no por fila) |
| L-4 | Combos y dropdowns clásicos quedan con esquina viva (el mockup redondea todo) | Esos controles no exponen Radius; redondear obligaría a controles modernos o superposiciones frágiles |
| L-5 | El diálogo "Así la verá el supervisor" (preview del HTML de approval) no va en la app | Era didáctico del prototipo; en producción el toast confirma el envío y el HTML lo ve el supervisor en su visor |

**Nuevas de `02-pantalla-captura.md` §11** (IDs originales de esa guía, sin renumerar):

| # | Guía | Divergencia | Motivo |
|---|---|---|---|
| L-6 | 02 | El chip de tulas de la fila muestra solo el número, sin el glifo `⛁` | Glifos prohibidos en el `Text` (tofu en Open Sans) y el catálogo de iconos no tiene uno de tula |
| L-7 | 02 | El panel de validación rotula "Planilla ‹N°› — regla", no "Fila N — regla" | El índice de una colección editable cambia al agregar/borrar filas: "Fila 4" apuntaría a otra planilla |
| L-8 | 02 | *(condicional)* Si la proyección de índice de `galPlanillasCap.Items` provoca pérdida de foco al tipear, se cae a `Items = colPlanillas` y la columna `#` queda vacía | Estabilidad de entrada > columna cosmética; se decide en Studio con su ítem de checklist |
| L-9 | 02 | En el panel de tulas el hueco del mensaje de duplicado está siempre reservado (48 px) en vez de aparecer/desaparecer | El `TemplateSize` de una galería canvas es fijo: no puede variar por fila |
| L-10 | 02 | El overlay de guardado no anima el progreso fila por fila **ni muestra el estado "en vuelo"** (*Guardando…*) | El camino feliz es un solo `Patch` con tabla: no hay progreso por fila que reportar; animarlo exigiría un `Timer` que además mentiría. Y el estado "en vuelo" (*Guardando…*) **no llega a pintarse**: canvas no repinta a mitad de una fórmula, y todo el guardado corre dentro de un `OnSelect`; el overlay muestra directamente el resultado |
| L-11 | 02 | No hay `Spinner` en el overlay de guardado | El catálogo de `01` §0.3 ya lo permite; la barra en tono suave ya comunica el estado y un control más no aporta información. Decisión de diseño, no limitación |
| L-12 | 02 | El chip de usuario muestra `User().FullName` sin el cargo ("· Oficial de esclusa") | Cero conectores: `User()` no expone el puesto y Office365Users está prohibido |
| L-13 | 02 | "Reintentar fallidas" re-ejecuta el guardado completo, no solo las filas de `colFallidas` | Tras el re-keyado el guardado es idempotente; duplicar el bloque de la receta 7.5 es el código que se pudre |
| L-14 | 02 | El overlay de *Nuevo lote* arranca bajo el header (Y 57) y no tiene ✕ ni Cancelar | Es un gate de estado, no un diálogo cancelable: sin lote no hay a qué volver, y el header debe seguir usable |
| L-15 | 02 | Los combos `cmbEmpresaCap` / `cmbTransportadoraCap` quedan de esquina viva | Instancia de **L-4**: el Cuadro combinado clásico no expone `Radius` |
| L-16 | 02 | `lblMetaCap` no pone en negrita los números ("**7 planillas** · **21 tulas**") | Una Etiqueta clásica tiene un solo `FontWeight`; partirla en 5 etiquetas por un matiz tipográfico no paga |
| L-17 | 02 | Los diálogos de **tulas** y **guardado** miden 520 de ancho y el de **salida** 420, contra los 380 del mockup | En 380 la galería de estado del guardado corta los mensajes del guard y el footer de 3 botones de la guarda de salida no respira. El resto de los diálogos sí quedan en 380 |
| L-18 | 02 | La grilla de planillas tiene estado vacío ("Este lote todavía no tiene planillas…"); el mockup no lo tiene | Mejora deliberada: el mockup deja la card en blanco y el operador no sabe si la app cargó o si el lote está vacío |
| L-19 | 02 | El overlay de *Nuevo lote* agrega "El número (RB-…) lo asigna Dataverse." y la transportadora arranca **sin preselección** (*Crear lote* gris hasta elegir) | Mejora deliberada: la preselección del mockup deja crear el lote con la transportadora **residual** del anterior con un clic apurado. Sin preselección ese bug no existe |
| L-20 | 02 | El segmentado del header arranca en X 210; con el ancho de contenido del mockup daría ~172 | El título usa ancho fijo 120 **compartido con `scrLotes`**, donde "Administración de lotes" lo necesita: un ancho por pantalla desincronizaría los dos headers, que son el mismo bloque visual |
| L-21 | 02 + 03 | Los toasts del mockup (card oscura abajo a la derecha) se materializan como `Notify()` nativo: banner de plataforma arriba, ancho completo | Canvas no tiene equivalente nativo **posicionable**; replicarlo exigiría contenedor propio + `Timer` por toast con su cola de estado |
| L-22 | 02 | El panel de tulas **descarta en silencio** las filas con `Numero` en blanco al *Aplicar* (`Filter(colTulasEdit, !IsBlank(Trim(Numero)))`, `02` §7.2): no avisa ni deshabilita el botón | El mockup hace **exactamente lo mismo** (`applyTulas` filtra los vacíos). ⚠️ Con el panel **scan-first** (L-24/§7.2) el caso es casi imposible: el escáner ignora el Enter en vacío. El `Filter` queda de cinturón |

**Nuevas del QA humano (16-ago, sobre `CashHubApps 1.31.0.4`)** — la app se construyó,
se desplegó y pasó QA humano intensivo; estas cuatro filas salen de ahí:

| # | Guía | Divergencia / rediseño | Motivo |
|---|---|---|---|
| **L-23** | 01 + 02 | **El catálogo local de sucursales MUERE.** `colSucursales` / `colSucursalesRefCap` eliminadas; `cmbEmpresaCap.Items` pasa a ser **server-side** (`Filter('CH-Tbl-ClientBranches', RecordStatus = 'CH-Ch-RecordStatus'.Active)` con `IsSearchable`, `Template ListItemTemplate.Double`, `DisplayFields`/`SearchFields` `["sanic_name","sanic_clientnamemirror"]`) | El entorno tiene **~50 000 sucursales activas**: el `ClearCollect` truncaba en `DataRowLimit` (2000) **en silencio** y el operador no encontraba su sucursal — el bug que motivó todo el rediseño (§1.2) con ropa nueva. **Regla nueva D-10** (`01` §2.3): catálogo en colección **solo si la tabla es estructuralmente acotada (< 500)**. Dependencia backend: **B0-9** (§3), ya cerrada. Corolario: `CH-Tbl-Clients` **no se agrega** (la excepción condicional de `01` §2.1 queda sin efecto) y la proyección 7.10 rehidrata `Empresa` con `LookUp('CH-Tbl-ClientBranches', …)` — registro fresco, `.Client` navegable, `RemitentClient` **nunca** en blanco |
| **L-24** | 01 + 02 | **Los `OnChange` de los 6 controles de fila NO patchean**: hacen solo `Set(gvarCambiosSinGuardar, true)`. El valor se baja a `colPlanillas` con un **VOLCADO** (`01` receta **7.11.A**, `02` §5.4.a) que corre como primera línea del `OnSelect` de 6 botones | Un `Patch` a la colección en un `OnChange` **muta el `Items` de la galería**, la galería se re-renderiza y el control **pierde el foco** a mitad de la palabra. No lo arregla `DelayOutput`. De paso quedaron escritos los gotchas de foco de galería (`01` §7.11.B): **`TabIndex` positivo en un template = filas entrelazadas** (las réplicas comparten número y el orden global agrupa por número) ⇒ siempre `0` + orden geométrico; **Íconos/Imágenes/Formas nacen en `TabIndex -1`** ⇒ `0` explícito si son interactivos; **las acciones destructivas quedan FUERA del Tab** |
| **L-25** | 01 + 02 | **Guardado diferido + delta.** El `OnSelect` de *Guardar* solo vuelca, valida y calcula el delta (`colCambiosPlanCap` / `colTulasNuevasCap`); el guardado real corre en `tmrGuardarCap.OnTimerEnd` (`Duration 50`, `Repeat false`, `Start ctxGuardandoCap`, `Visible false`), junto con la continuación del cierre y la navegación de *Guardar y salir* | Canvas **no repinta a mitad de una fórmula** ⇒ el diálogo *"Guardando…"* nunca se veía (era la segunda mitad de **L-10**, hoy **superada**). El defer de **un frame** lo hace visible. Y el delta evita re-patchear N planillas idénticas por click. 🧨 **Los timers no corren en pantallas no visibles**: por eso el `Navigate` de *Guardar y salir* vive en el `OnTimerEnd`. 🧨 **NO resucita el `'timer Submit'` de la v1** (§2.4): aquél era una máquina de estados con `varCurrentRow`/`varRowIndex` que iteraba fila por fila usando el timer como bucle; éste corre **una sola vez**, sin `Repeat` y sin estado de iteración. La prohibición queda **matizada, no levantada**. Bug corregido de paso: el re-keyado re-hidrataba `colTulasDV` en vez de **`colPlanillasDV`**, y por eso el **segundo guardado duplicaba** las planillas nuevas |
| **DL-22** | 03 | **La última tula de una planilla es intocable desde Administración**: `btnEliminarTulaLot` y `btnRemapearTulaLot` suman `\|\| CountRows(galTulasLot.AllItems) <= 1` a su `DisplayMode` (gate `G-TULA-ULTIMA`, `03` §11.3), con `Tooltip` explicativo | Las dos operaciones dejaban la planilla con **cero tulas** — exactamente el registro que la regla *"Sin tulas asociadas"* de `fxProblemas` impide crear en Captura. Administración no puede fabricar por la puerta de atrás lo que Captura tiene prohibido guardar. Misma lógica que **DL-20**. *Editar tula* no lleva el término |

> 📌 **QA humano 16-ago — detalle completo en Engram**: buscar por `reception/*`
> (`reception/espejo-clientname-2026-08` para B0-9, y los registros del QA de la
> 1.31.0.4). Además del rediseño, el QA destapó **cuatro errores sistémicos de
> fórmula** que estaban en las tres guías y hoy están corregidos in-place:
> (1) la **igualdad de registro** `Filter(ds, Lookup = registro)` **no compila** —
> va **PK contra PK** (`01` §2.3, D-1 en detalle); (2) una **vista no puede ser el
> primer argumento de `Filter`** — va como segundo, o se elimina (`01` §2.5);
> (3) `IfError(Patch(…), Collect(col, {…}))` **no compila** — el brazo de fallo
> termina en `; Blank()` (`01` receta 7.5, regla 7); (4) leer `cmb….Items` desde
> otra fórmula **no está soportado** — se lee `IsBlank(cmb.Selected.'PK')`
> (`03` §12.3, ESCOTILLA 1, hoy **forma primaria**). También:
> `ClearCollect(col, Filter(col2, false))` pasa a `FirstN(col2, 0)` (warning de
> lint) y el literal del option set **global** de `ClientBranch` es
> `'CH-Ch-RecordStatus'.Active`, no `'RecordStatus (CH-Tbl-ClientBranches)'`.

**Nuevas de `03-pantalla-lotes.md` §15.4** (IDs originales de esa guía, sin renumerar):

| # | Guía | Divergencia | Motivo |
|---|---|---|---|
| DL-1 | 03 | Tab list dibujado como píldora con sombra; en la app es la **Lista de pestañas moderna** con chrome de plataforma | §2.2 la habilita y su gestión de foco/teclado es nativa; el costo es que su aspecto no se calca (catálogo de `01` §0.3 ampliado) |
| DL-2 | 03 | La fila de lote no muestra "3 planillas · 9 tulas", solo la píldora de estado | Extensión de **L-3**: sin `bagcount` (B0-2) ni contador de planillas, ambos números exigirían `CountRows(Filter(ds,…))` por fila = D-2 + N+1 |
| DL-3 | 03 | La mini-tabla del lote cambia la columna **Tulas** por **Total US$** | Ídem DL-2; el ancho liberado se usa para el total en dólares, que el mockup escondía |
| DL-4 | 03 | La meta de la fila de planilla omite "· 4 tulas" | Ídem DL-2 |
| DL-5 | 03 | El combo de remapeo usa `DisplayFields ["ReceiptID","RemitentName"]` (dos líneas) en vez de una línea, y **sin `AddColumns`** | El registro elegido se asigna directo al lookup `ReceptionID`: un registro ensanchado por `AddColumns` tiene otra forma y rompe la asignación |
| DL-6 | 03 | Remapear con lote de una sola planilla abre el diálogo con el combo vacío y el OK gris, en vez de tostear antes de abrir | Pre-validar exigiría `CountRows(Filter(ds,…))` (D-2); el costo es un clic, no un dato |
| DL-7 | 03 | El ⟳ no tostea "Listas actualizadas" | Cada operación ya emite su `Notify` y llama al ⟳: el toast se duplicaría en cada acción |
| DL-8 | 03 | Sin glifos dentro de los botones (`＋`, `⛁`, `🗑`, `🔒`): los que quedan son Iconos clásicos separados | Open Sans no tiene esos glifos ⇒ tofu; un icono no se puede meter dentro de un Botón clásico |
| DL-9 | 03 | Sin `box-shadow` en cards, diálogos y segmento activo (solo borde `ThemeLine`); sombra únicamente en la card del modal | El Botón clásico no expone sombra; el `DropShadow` solo lo tiene el Contenedor |
| DL-10 | 03 | ✅ **ACEPTADA → regla D-5.** El ⟳ ejecuta `Refresh(ds)` × 3 | `Refresh(ds)` queda **prohibido como paso automático post-operación** y **permitido como acción explícita del usuario**: atar las galerías maestras a colecciones las clavaría en `DataRowLimit = 2000` y el plan de estrés siembra >2000 lotes. Ya NO es divergencia: es la regla escrita en `01` §2.3 |
| DL-11 | 03 | La fecha de la tula es `Text(ThisItem.'Created On', FmtFechaCorta)` real, no el "15 ago 09:05" del mockup | El mockup es de utilería |
| DL-12 | 03 | El banner de bóveda usa `Icon.Lock` en vez del emoji `🔒` | Instancia de DL-8 |
| DL-13 | 03 | Las cabeceras de lista son Botón clásico con `RadiusTopLeft/TopRight 12`, no el `overflow:hidden` de la card | El Rectángulo no tiene `Radius` y sus esquinas vivas asomarían fuera de la card |
| DL-14 | 03 | El rango de fechas es **funcional** (el mockup lo marca decorativo) e inclusivo del día "hasta" completo | Un filtro de fechas que excluye el día de hoy es peor que no tenerlo |
| DL-15 | 03 | El chip de usuario muestra solo `UsuarioNombre` | Mismo motivo que **L-12**: app sin conectores, `User()` no expone el puesto |
| DL-16 | 03 | Sin el punto de 6 px dentro de la píldora (`.pill::before`) | Ya declarado en la receta `01` §7.4; se repite porque esta pantalla tiene 4 píldoras |
| DL-17 | 03 | **La búsqueda es por vista**: *Lotes* filtra por N° de lote y transportadora; *Planillas*, por N° de planilla y remitente (el `HintText` cambia con el tab). El mockup cruzaba las dos jerarquías: planilla/empresa desde *Lotes*, lote desde *Planillas* | Cruzarlo exige un `Filter` anidado por fila (**D-1 + D-4**) — "traeme los lotes que TENGAN una planilla que…" no delega. Mitigación: el tab *Planillas* cubre la búsqueda por planilla/empresa, a un clic |
| DL-18 | 03 | Estados vacíos reescritos: "Sin lote que mostrar…", "Sin planilla que mostrar…", "Esta planilla no tiene tulas.", "Este lote no tiene planillas." (nuevo) y "Revisá el detalle antes de confirmar." (nuevo, diálogo Eliminar), en vez de los "Seleccioná un lote / una planilla" del mockup | Esos textos **nunca serían verdad**: la galería clásica auto-selecciona la primera fila (`03` §0.2), así que "sin selección" solo ocurre con la lista **vacía** — y ahí el problema es el filtro, no la selección. Los dos textos nuevos cubren huecos que el mockup dejaba en blanco |
| DL-19 | 03 | El toast de *Mover a bóveda* **no** copia el "operación atómica" del mockup: dice cuántas tulas pasaron y en qué quedó el lote | Emparentado con **L-1**: client-side **no hay atomicidad**. Son dos fases con su propio `IfError` (tulas → lote); si la segunda falla, las tulas ya se movieron. Prometer atomicidad sin la Custom API (B0-3) es mentir en la operación que menos se puede deshacer |
| DL-20 | 03 | El mockup permitía **operar tulas de lotes `En progreso` desde Lotes** (camino directo). La v2 lo **bloquea**: `G-PLAN` y `G-TULA` suman `galPlanillasLot.Selected.BatchStatus <> 159460000` y los 5 comandos quedan grises con tooltip *"Los lotes En progreso se editan en Captura"* | Un lote `En progreso` es un lote **en captura**: sus planillas y tulas viven en `colPlanillas`/`colTulas` del otro lado. Escribir la misma tula desde las dos pantallas deja la sesión de Captura con datos viejos y el guardado la pisa sin avisar. El camino está definido: `En progreso` ⇒ Captura (contrato `01` §4.3 y §5.4); `Recibido`/`En boveda` ⇒ Lotes |
| DL-21 | 03 | Tras *Mover a bóveda* con el **filtro de estado en `Recibido`**, el lote sale de la lista y la selección **salta a otra fila**: el detalle muestra un lote que el usuario no pidió | Es el **comportamiento correcto del filtro** (el lote ya no es `Recibido`) más la auto-selección de la galería clásica (`03` §0.2). No hay nada que arreglar: se declara para que no se reporte como bug y para que el checklist lo espere |

## 6. Estados — contrato inter-app (sin cambios)

- ReceptionBatch: `En progreso` (159460000) → `Recibido` (…001) → `En boveda` (…003). `Anulado` (…002) sigue muerto — fuera del alcance.
- Bags: produce `Recibida en esclusa` (…000), `Almacenada en boveda` (…001), `Pendiente de revision por supervisor - esclusa` (…005). Consume Recounting: `Asignada a cajero`.
- ApprovalRequest: la app crea con `ApprovalStatus` blank; resuelve Supervisor.
