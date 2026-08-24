# CH-App-Digitization v2 — Análisis y plan de mejora

> Estado: **PROPUESTA** (pendiente de aprobación). App v1 analizada
> (`sanic_chappdigitization_278aa`, msapp 278aa) con 4 agentes Opus: funcional/bugs,
> performance/delegación, seguridad y UX/inventario de controles. Fuente desempaquetada
> (`pac canvas unpack`, formato 0.30): **una sola** `ScreenDigitalizacion` de **3.787
> líneas**; `DataRowLimit = 500`; `enablerowscopeonetonexpand = false`.
>
> Objetivo: reconstruir la app al nivel "super elegante" del resto del suite
> (**Reception v2**, **Recounting v2**), eliminando todo control no nativo, con UX/UI y
> performance como prioridad, y cerrando los bugs y huecos de seguridad vigentes.

---

## 0. Qué es esta app (y qué no es)

**No es una app de "digitación" en el sentido literal.** Es la consola de **corte,
aplicación y control de lotes de transacciones** hacia el core bancario (IBS Branch),
más el **handshake de recepción y digitación de cheques** que le manda el Supervisor.
El nombre engaña: la digitación real de cheques (`Digitado`/`Rebotado`) recién se
implementa en esta v2 (ver §4).

El operador es el **Digitizer** (group team `CashHub - Digitizer`, rol
`CH-SR-Digitizer`). Su trabajo diario:

1. **Cortar** las transacciones de efectivo de un cajero en un lote (`scrCorte`).
2. **Aplicar** manualmente las transacciones que no van por corte automático —las que
   traen cheques o no tienen número de cuenta— (`scrManual`).
3. **Gestionar** el ciclo de vida del lote: Generado → Ingresado → Aplicado, descargar
   el CSV para el core, y sacar del lote las transacciones que el core rechaza
   (`scrLotes`).
4. **Recibir y digitar** los cheques que el Supervisor envía (`scrCheques`).

---

## 1. Veredicto del análisis

### 1.1 Lo que YA está bien (crédito al trabajo previo)

El **backend está maduro**. De los 7 CRITICAL del análisis del 11/8, **6 ya se
resolvieron server-side** y bien resueltos:

| Antes (v1 rota) | Ahora (corregido) |
|---|---|
| Doble aplicación de depósitos | Grids filtran `TransactionStatus = Blank()`; corte server-side filtra `TransactionStatus Null`; `TransactionGuard` bloquea asignar a lote una tx con status |
| Lotes nacían con `BatchStatus` NULL | El lote lo crea `CreateTransactionCutPlugin` con `Generado` explícito; `TransactionGuard.EvaluateBatchCreate` rechaza otro estado de nacimiento |
| Export CSV truncado a 2000 con `ForAll`+`Collect` | `CH-Flow-DownloadData` con `paginationPolicy.minimumItemCount: 100000` |
| `varNow` seteada después del submit | Sellos temporales server-side (`BatchTransitionStampPlugin`, `CheckStatusStampPlugin`) |
| Sin roles versionados sobre lotes | `CH-SR-Digitizer.xml` versiona Append/Create/Read/Write sobre `TransactionBatches`; `CH-SR-Counter` acotado a Basic/Local |
| Auditoría deshabilitada | `IsAuditEnabled=1` en las 4 tablas de dinero |
| Identidades hardcodeadas en el export | Parámetro `Export.Core.Identidad` en `sanic_chtblparameters` |
| Sin guard server-side | `TransactionGuard` + `CheckGuard` como pasos **Pre-validation sincrónicos** (stage 10) — límite de seguridad real, sin bypass por identidad |

**El problema hoy no es el backend: es la capa canvas.** El backend ya conoce el patrón
espejo+guard (`sanic_batchstatus` funciona perfecto). La app se quedó atrás.

### 1.2 Delegación — 6 fórmulas truncan dinero EN SILENCIO

El límite real es **500** (`MaxGetRowsCount: 500` en los 4 datasources +
`DefaultConnectedDataSourceMaxGetRowsCount: 500`), y `enablerowscopeonetonexpand: false`
significa que **todo `Lookup.Columna` dentro de un `Filter` materializa 500 filas y
filtra en el cliente**, sin warning.

| # | Sev | Fórmula (archivo:línea) | Efecto con volumen real |
|---|---|---|---|
| D-1 | 🔴 | `Filter('CH-Tbl-Transactions','Transaction Batch'.TransactionBatches = varLoteSelected…)` (`:3396`) | **El grid del lote se ve vacío** mientras los totales al lado (correctos, del espejo) dicen millones. El peor. |
| D-2 | 🔴 | `CountRows(Filter(… 'Created By'.User = cmbCajero.Selected.User))` (`:3751`) | **El conteo del diálogo de corte es falso**: dice "137", corta 2.840. El número sobre el que un humano da consentimiento. |
| D-3 | 🟠 | `Filter(… 'Created By'.User = cmbCajero.Selected.User)` (`:171`) | El cajero fuera de las primeras 500 ve su grid **vacío** ("sin pendientes"). |
| D-4 | 🟠 | Ídem "Tx Manuales" (`:453`) | El digitador cree que no hay nada, mientras se acumulan cientos. |
| D-5 | 🟠 | `Filter('CH-Tbl-Checks', RelatedReCounterDepositID.'…' = …)` (`:1940`) | La tabla de mayor volumen del dominio: cheques de la tx no aparecen. |
| D-6 | 🟡 | `Filter(…, Cajero.User = cmbCajeroLotes…)` + `SortByColumns` sobre set truncado (`:2100`) | Bomba de tiempo: en ~6 meses el filtro por cajero devuelve lotes viejos o vacío. |
| D-7 | 🟡 | `Choices([@'CH-Tbl-TransactionBatches'].createdby/…)` ×3 (`:2538,:2621,:2848`) | Materializa la lookup view de `systemuser` para render read-only; con >500 users muestra vacío donde debería ir un nombre. |

**Cinco de las seis se arreglan cambiando `Lookup.Columna` por `Lookup`** (igualdad de
registro → delega a `_sanic_…_value eq <guid>`). La lección ya está escrita en el propio
repo: el comentario de `TransactionBatchStatusMirrorPlugin.cs` lo documenta textual.

> 🧨 **Activar el App Checker en el pipeline.** El `.sarif` actual tiene 11 findings, todos
> de accesibilidad, **cero de delegación** sobre un archivo con 6 expresiones que truncan
> datos bancarios en silencio. Es un detector de humo sin batería.

### 1.3 Bugs funcionales vigentes

| # | Sev | Bug | Dónde |
|---|---|---|---|
| C-1 | 🔴 | `varTxLoteSelected` no se limpia al cambiar de lote → "Sacar del lote" puede desasignar una tx de **otro** lote; el toast miente | `:2113` / `:2417-2432` |
| C-2 | 🔴 | `Form2` (aplicar manual) sin `OnFailure` ni label `Form2.Error` → **los rechazos del guard son mudos**; el digitador cree que aplicó y no aplicó | `:666-691` |
| C-3 | 🔴 | `btnCorteConfirmar` nunca cierra el overlay (falta `varConfirmarCorte:false`) ni tiene anti-doble-click → modal trabado, flow re-ejecutable | `:3725-3738` |
| C-4 | 🔴 | La digitación real de cheques no existe: se escribe `Recibido`/`No Recibido`, no `Digitado`/`Rebotado` (ver §4) | `:3554,:3574` |
| W-1 | 🟠 | `SetFocus(ButtonCanvas2)` sobre un control recién oculto en `Form2.OnSuccess` → toast de error **en cada aplicación exitosa** | `:677-690` |
| W-2 | 🟠 | `tblChequesDeTx` lista cheques huérfanos si `RelatedDepositRecounting` es Blank | `:1940` |
| W-9 | 🟠 | `Form2` 100% read-only → no se puede capturar el `AccountNumber` que faltaba (la causa misma de que la tx sea manual) | `:759` |

### 1.4 Performance

- **P-1 (🔴):** `Refresh()` de tabla completa por cada registro digitado (`:678,:3559,:3579`)
  → 3 re-queries (dos no delegables de 500) por transacción. 300 tx/día = 900 re-queries
  que nadie mira.
- **P-2 (🟠):** `SubmitForm` escribe **8 columnas** para cambiar 1 (`:659` + 8 cards en
  `DisplayMode.View`) → reescribe valores idénticos, infla payload, auditoría y trabajo de
  3 plugins.
- **P-3 (🟠):** auto-avance roto en 3 dimensiones (SetFocus a oculto + ignora filtro de
  cajero + carrera con `Refresh` asíncrono + desincroniza el grid).
- **P-4 (🟡):** 3 cargas independientes de `systemuser` sin filtrar (`Users.json` 1.3 MB +
  `TableDefinitions/Users.json` 6.3 MB al abrir).

### 1.5 Seguridad — 3 CRITICAL nuevos, 5 de 8 vectores NO requieren abrir la app

| # | Sev | Hallazgo | Fix (incluido en este trabajo) |
|---|---|---|---|
| V-1 | 🔴 | Custom API `sanic_CHCreateTransactionCut` registrada `isprivate:False` **sin `executeprivilegename`** → cualquier usuario del entorno la invoca por Web API con `CajeroId` arbitrario; el plugin solo valida que el GUID no esté vacío | `executeprivilegename` + autorización `CajeroId == InitiatingUserId` (o rol supervisión) dentro del plugin, con `ITracingService` en cada rama; `cmbCajero.Items` filtrado por team |
| V-2 | 🔴 | La app entrega a CreateCut una conexión Dataverse de **Service Principal (System Admin)** → el digitador ejecuta con privilegios de SP; `createdby` = application user (rompe la segregación de funciones que el guard sí implementa) | Cambiar el slot Dataverse de CreateCut a **conexión de usuario** |
| V-3 | 🔴 | `CH-Flow-DownloadData` interpola el `LoteId` crudo en un `$filter` OData corriendo `embedded` → inyección `… or sanic_total gt 0` devuelve **todas las transacciones de la organización**; y el CSV queda con link `scope: Organization` | Validar `text` contra regex de GUID (Terminate si falla) + `runtimeSource` `embedded → invoker` + `scope: Organization → restringido` |
| V-5 | 🟠 | Nombre de archivo controlado por el usuario en la ruta de OneDrive (`text_1` con `../`) | Sanitizar o derivar el nombre server-side del `sanic_name` del lote |
| V-6 | 🟠 | `CH-SR-Digitizer` con Write **GLOBAL** sobre las 3 tablas de dinero → puede reescribir montos/cuenta de cualquier tx pendiente de cualquier cajero por Web API | Bajar Write a **Local** en las 3 tablas |
| V-7 | 🟡 | `prvReadTraceLog` Global + privilegios de `Workflow` en el rol | Quitar del rol |

---

## 2. Arquitectura v2 — adiós al monolito

Cuatro pantallas reales, navegación por **segmentado en el header** (patrón Reception v2
`scrCaptura`/`scrLotes`). Cada `Navigate()` acota el grafo de dependencias: un `Refresh`
en una pantalla ya no recalcula las grillas de las otras tres.

| Pantalla | Rol | Qué muestra |
|---|---|---|
| **`scrCorte`** | Corte de transacciones a lote | **Rail de cajeros con corte pendiente** (badge de conteo) + KPIs (pendientes, total NIO/USD/EUR) + lista de transacciones `Batch` pendientes. Botón **Realizar corte** + diálogo de confirmación con el **conteo real (server-side)**. |
| **`scrManual`** | Aplicación manual | **Rail de cajeros con manuales pendientes** + segmentado **Pendientes / Aplicadas** + lista **ordenable por motivo** con badge (**Sin cuenta / Con cheques / Rechazo core**). Panel de detalle **keyboard-first** (solo lectura, sin captura de cuenta) + **cheques de la transacción** (para cotejar contra los físicos en mano) + botón **Aplicar** (con feedback de rechazo). |
| **`scrLotes`** | Ciclo de vida del lote | **Rail de cajeros con lotes por aplicar** + lista de lotes con **filtro por estado (Por aplicar / Aplicados / Todos)**, buscador por número y filtro de fecha para llegar a **lotes antiguos**. Panel de detalle: **KPI cards de totales**, grid **delegable** de tx del lote, botones **Ingresado** / **Aplicado** (segregación de funciones en UI), **Descargar CSV** y **Sacar del lote** (**en Generado o Ingresado** — los rechazos del Core ocurren con el lote ya Ingresado —, con confirmación + captura de motivo). **Historial** de la transacción con el motivo visible después. |
| **`scrCheques`** | Recepción de cheques | Segmentado por estado (**Enviados / Recibidos / No recibidos**), lista con **cliente/depósito + quién envió**, panel de detalle con **trazabilidad (quién envió / quién recibió)**. Acciones: **Recibido / No recibido**. Sin digitación (ver §4). |

**Estado (`App.Formulas` + variables por pantalla):** con 4 pantallas, las variables de
selección dejan de ser globales sin criterio. Cada pantalla gobierna su `varXSelected`
local y la limpia en `OnVisible` (mata C-1 y W-8 de raíz).

### 2.1 Diseño responsive horizontal

**Nada de 1366×768 fijo.** El layout se adapta a cualquier ancho de pantalla (el sentido
vertical queda fuera de alcance). En canvas esto exige:

- **App con Escalar-para-ajustar OFF** y bloqueo de relación de aspecto OFF (`App.Width`/
  `App.Height` siguen el tamaño real del dispositivo, no un lienzo escalado uniformemente).
- **Auto-layout containers** (`groupContainer` horizontal/vertical) con `FillPortions` en
  vez de anchos fijos; el rail usa un ancho acotado (equivalente a `clamp`), el resto fluye.
- **Tamaños relativos a `Parent.Width`/`Parent.Height`**, cero coordenadas absolutas.
- Grillas (galerías) con columnas por fracción, no por px.

> El resto del suite usa "Escalar para ajustar ON", que escala uniforme pero **no adapta**.
> Esta app es la primera del suite en responsive real — se documenta como patrón nuevo en
> `01-app.md`.

### 2.2 Rail de cajeros — el problema de los 40 cajeros

En Corte, Manual y Lotes, el digitador no puede recorrer 40 cajeros para ver quién tiene
trabajo. El **rail izquierdo** muestra **solo** los cajeros con pendientes, con badge de
conteo, ordenables por cantidad o nombre. Reemplaza al dropdown de filtro. Requiere un
**agregado por cajero server-side** (§3.2, `sanic_CHWorkloadByCashier`): el `GroupBy` de
canvas no delega.

### 2.3 scrManual — flujo de teclado para grabación rápida

Los grabadores manuales son **rapidísimos**; la UX no debe romperles el ritmo. Flujo:

1. Al seleccionar (o auto-seleccionar) un registro, el foco cae en **Número de cuenta**.
2. El operador copia el valor, lo pega en el Core, vuelve, **Tab** al siguiente campo
   (Moneda → Monto → … → Referencia), copiando cada uno.
3. Desde Referencia, **Tab** lleva al botón **Marcar como aplicada**; **Enter** lo dispara.
4. Al aplicar, auto-avanza al **siguiente registro** con el foco de nuevo en Cuenta.

**Nativo y garantizado:** `SetFocus` + `TabIndex` secuencial + `OnSelect` del botón activado
por Enter + auto-avance en el `OnSuccess`/tras el `Patch`. **No nativo:** Power Fx **no tiene
función de portapapeles** (`Copy`/`SetClipboard` no existen); el `Ctrl+C` es del navegador.
El **auto-seleccionar el texto al enfocar** (para copiar sin `Ctrl+A`) se valida en
construcción — patrón candidato: `Text input` clásico read-only (con `OnChange: Reset(Self)`
para no persistir cambios, respetando que los datos del cliente no se modifican). Si no sale
nativo, el costo es una tecla extra, no rompe el flujo.

> 🧨 **Los datos del cliente NO se modifican** (máxima del proyecto). Una transacción sin
> número de cuenta se aplica **sin** cuenta; la app **no** ofrece capturarla. Los campos del
> panel son de solo lectura: se copian, no se editan.

---

## 3. Prerrequisitos server-side (el patrón espejo+guard del suite)

Igual que Reception v2 (`sanic_batchstatus`) y Recounting v2 (4 espejos), lo que hoy se
**infiere navegando relaciones** hay que **materializarlo en columnas delegables**.

### 3.1 Espejo de cajero en la transacción — **raíz de D-2, D-3, D-4**

Hoy el cajero de una transacción solo vive en `createdby`; llegar a él exige dot-walk no
delegable. Dos caminos:

- **(a) inmediato:** `'Created By' = cmbCajero.Selected` (igualdad de registro, delega).
  Cero schema, se aplica ya. Ata el concepto "cajero" al metadato `createdby`.
- **(b) canónico:** columna espejo `sanic_cajero` (EntityReference) en
  `CH-Tbl-Transactions`, estampada Pre-op Create = `createdby`, **alineada con el
  `sanic_cajero` que el lote ya tiene**. Requiere plugin + migración, pero desacopla
  negocio de plataforma y deja transacción y lote hablando el mismo idioma.

**Recomendación:** (a) ahora para parar la hemorragia; (b) en el mismo ciclo si se toca el
corte, porque `CreateTransactionCutPlugin` también filtra por `createdby` y debería mirar
`sanic_cajero`.

### 3.2 Operaciones a mover / endurecer server-side

| Op | Estado | Acción v2 |
|---|---|---|
| Corte | ✅ ya server-side (`CreateTransactionCutPlugin`) | Solo cerrar V-1/V-2 (privilegio + conexión de usuario) y el conteo de confirmación (Custom API dry-run `sanic_CHPreviewTransactionCut` que devuelva `EligibleCount`, o al menos delegar D-2) |
| Export CSV | ✅ pagina 100k | Cerrar V-3/V-5 (validar GUID, invoker, scope, nombre server-side); evaluar **patrón asíncrono** (flow escribe URL en `sanic_archivosalida`; la app muestra "Descargar" cuando deja de estar vacía) para lotes que exceden los ~2 min del trigger |
| Sacar del lote | ⚠ `Patch` client-side + `Refresh` + `LookUp` (P-5, W-3) | Custom API `sanic_CHRemoveFromCut(TransactionId, Motivo)`: valida contra `TransactionGuard` (**solo lotes en `Generado`**), desasigna, recalcula totales, **persiste el motivo del rechazo del core** (visible después en el historial de la tx), y **devuelve los totales nuevos** (cero `Refresh`, cero carrera) |
| Aplicación manual masiva | ⚠ de a una, con `Refresh` de tabla completa | Corto plazo: P-1 + P-2 (`Patch` de una columna, sin `Refresh`). Canónico: Custom API `sanic_CHApplyTransactions(TransactionIds)` paginado y atómico |
| Recepción de cheques | ⚠ de a uno | `Patch` de una columna sin `Refresh` de tabla completa; opcional multi-selección + Custom API `sanic_CHReceiveChecks` |

### 3.3 Prerrequisitos de datos nuevos

| Necesidad | Solución server-side |
|---|---|
| Rail de cajeros con carga pendiente (§2.2) | Custom API `sanic_CHWorkloadByCashier` → `[{cajero, mesa, pendientesCorte, pendientesManual, lotesPorAplicar, totales}]`. El `GroupBy`/agregado por cajero no delega en canvas |
| Motivo del rechazo del core visible en el historial | Columna `sanic_motivorechazocore` (texto) en `CH-Tbl-Transactions`, escrita por `sanic_CHRemoveFromCut` |
| Quién envió / recibió el cheque (§4) | Lookups `sanic_senttodigitizationby` y `sanic_receivedby` (a `systemuser`) en `CH-Tbl-Checks`, estampados server-side junto a las fechas ya existentes (`senttodigitizationon`/`receivedon`) por el mismo patrón de `CheckStatusStampPlugin` |

---

## 4. scrCheques — solo recepción (alcance ajustado)

**Decisión del usuario (rev. 2):** la app **NO digita** cheques. `Digitado`/`Rebotado`
**no** se hacen en el área de digitación → se descartan de esta reconstrucción. La app se
limita al **acuse de recepción física**: `Recibido` / `No recibido`. Esto **simplifica** el
backend: no se toca `CheckGuard` para abrir transiciones, ni se agregan sellos de digitado.

### 4.1 Estados relevantes (`sanic_CHTblChecks/Entity.xml`, esquema vivo)

`sanic_checkstatus` tiene 6 valores; la app solo opera sobre estos:

| Valor | Label | Sello temporal | Quién |
|---|---|---|---|
| `159460003` | Enviado a digitacion | `sanic_senttodigitizationon` ✅ | Supervisor (entrada) |
| `159460004` | Recibido | `sanic_receivedon` ✅ | Digitizer |
| `159460005` | No Recibido | `sanic_notreceivedon` ✅ | Digitizer |

`Recontado`, `Digitado` y `Rebotado` quedan **fuera** del alcance de esta app.

### 4.2 Máquina de estados (acotada)

```
Enviado a digitacion ─(Digitizer)→ Recibido        (acuse OK)
                     └(Digitizer)→ No Recibido       (no llegó; el Supervisor puede reenviar)
```

### 4.3 Único cambio de backend para cheques

Agregar dos lookups de **autoría** (§3.3) para mostrar quién envió y quién recibió:
`sanic_senttodigitizationby` (estampado por el flujo/app del Supervisor al enviar) y
`sanic_receivedby` (estampado por `CheckStatusStampPlugin` con `InitiatingUserId` al marcar
`Recibido`). Alineados con los sellos de fecha que ya existen. Sin tocar transiciones ni
congelamientos del `CheckGuard`.

---

## 5. Sistema de diseño (fuente canónica: Reception v2)

Se copian **textual** los 24 tokens `Theme*` de
`sanic_chappreception_9ff29/Src/App.fx.yaml:6-30` a `App.Formulas` (no `Themes.json`, no
`OnStart`). Paleta BAC: acento `#C8102E`, fondo `#F6F5F4`, superficie `#FFFFFF`, tinta
`#201C1D`. Tipografía **Open Sans** explícita, `Size` en puntos = `Round(px × 0.75, 2)`.

**Regla de oro de las esquinas:** solo tienen `Radius` el **Botón clásico**, la **Entrada
de texto clásica** y el **Contenedor**. El **Rectángulo NO**, la **Etiqueta NO**. Toda
superficie redondeada es un **Botón clásico con `Text:""` `DisplayMode.View`** (receta
7.1). El rectángulo queda solo para líneas y barras de esquina viva.

**Catálogo de controles CERRADO.** Se eliminan los **10 PCF de primera parte**
(Table/OneGrid, Tab list, Text, Text input, Number input, Combobox, Dropdown, Button,
Date picker, Spinner PCF) y los **2 `Form` + 19 `typedDataCard`** (~130 controles de
andamiaje para escribir 1 campo). Quedan **solo** clásicos + `modernTabList` y
`modernDatePicker` donde haga falta. También se borra `DataSources/ComboBoxSample.json`
(sample muerto, cero referencias).

> La librería compartida `sanic_chclsharedcomponents_85fca` está **muerta** (cero apps la
> referencian, su `cmpTheme` tiene púrpura `#5B4BDF` equivocado, `AllowAccessToGlobals:false`).
> **No revivirla.** Componentes **locales a la app**, como el resto del suite.

### 5.1 Componentes canvas de la app (locales)

| Componente | Reemplaza | Instancias |
|---|---|---|
| `cmpHeader` | (no existe hoy) | 1 por pantalla — marca, título, segmentado de navegación, chip de usuario |
| `cmpPildoraEstado` | estados como texto crudo | ~8 — `BatchStatus`, `TransactionStatus`, `CheckStatus`, `ProcessingType` |
| `cmpKpiMoneda` | 6 labels sueltas a `Size 14` | 3 — NIO/USD/EUR (superficie `Radius 11` + label MAYÚSC + valor Bold) |
| `cmpDialogo` | 1 diálogo roto | ≥5 — confirmar corte, Ingresado, Aplicado, sacar del lote, digitar cheque |
| `cmpBarraFiltroCajero` | 3 copias con placeholders distintos | 1 — elimina los 3 estados de filtro independientes |

Recetas (no componentes, por límite de canvas): fila de galería clicable (7.3), estado
vacío con CTA, fila kv de detalle (~130 controles → ~36).

---

## 6. Divergencias declaradas contra el mockup (ledger `L-*`)

- **L-1** — Sin `DropShadow` en superficies de Botón clásico (el Botón no lo soporta); la
  separación la da el borde `ThemeLine`. Solo los `groupContainer` de diálogo llevan
  `DropShadow.Regular`.
- **L-2** — El `Dropdown` clásico no tiene `Radius`; se acepta o se reemplaza por
  `Combobox` clásico donde el radio importe visualmente.
- **L-3** — Sin spinner animado durante `Patch` (el hilo de UI está tomado y una animación
  congelada parece la app colgada). Se usa texto estático o barra de progreso real; el
  `Spinner` moderno solo para esperas con el hilo libre.

---

## 7. Plan de trabajo

| # | Fase | Entregable | Quién |
|---|---|---|---|
| 1 | Análisis | 4 informes Opus (✅ hecho) + esta propuesta | Claude |
| 2 | Diseño | `mockup-digitization.html` alta fidelidad (todas las pantallas + diálogos + estados vacíos) | Claude |
| 3 | Guías | Un MD por pantalla con `powerapps-screen-guides` → revisor de fidelidad vs mockup → antagonista de completitud/coherencia | Claude + agentes Opus |
| 4 | Vestido | El usuario hace el scaffolding en Studio (componentes con nombres); Claude descarga el `.msapp`, lo descomprime y viste las propiedades editando el código | Usuario + Claude |
| 5 | Marca | Icono PNG de la app | Claude |
| 6 | Prueba | Seeder de estrés con data sintética masiva; verificación de delegación (>500/2000) y performance | Claude |
| 7 | Cierre | Commits convencionales, changelog, memoria | Claude |

**Track backend paralelo (no bloquea la UI):** §3.2 (Custom APIs de sacar del lote con
motivo / aplicar masivo), §3.3 (agregado por cajero `sanic_CHWorkloadByCashier`, columna de
motivo, lookups de autoría de cheques), §1.5 (fixes de seguridad V-1/V-2/V-3/V-5/V-6). Se
implementa con Strict TDD sobre `CashHubCore`, como el resto del core. **No incluye** ciclo
de digitación de cheques (fuera de alcance, §4).

---

## 8. Estructura de la documentación (`docs/digitization-rebuild/`)

```
00-analisis-y-plan.md     (este documento)
01-app.md                 identidad, datasources + reglas de delegación (D-*), App.Formulas
                          (paleta copiada de Reception), OnStart, OnError, variables,
                          option sets, §6 estilos (alias ESTILO-*), §7 recetas
                          (7.1 superficie, 7.2 diálogo, 7.3 fila, 7.4 píldora),
                          §8 componentes (props, árbol), §9 checklist de salida
02-pantalla-corte.md      scrCorte (rail de cajeros + corte)
03-pantalla-manual.md     scrManual (rail + keyboard-first + Pendientes/Aplicadas)
04-pantalla-lotes.md      scrLotes (rail + ciclo + sacar del lote + historial)
05-pantalla-cheques.md    scrCheques (recepción + trazabilidad)
06-backend.md             espejo de cajero, Custom APIs (workload, remove-from-cut,
                          apply), lookups de autoría de cheques, fixes de seguridad
07-guia-de-pruebas.md     escenarios de estrés (delegación + performance)
mockup-digitization.html  la verdad visual contra la que se mide todo
```
