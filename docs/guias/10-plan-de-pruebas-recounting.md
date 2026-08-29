# Plan de pruebas de 0 a 100 — CH-App-Recounting

Guía exhaustiva para certificar la app de recuento: **todas** sus funcionalidades y,
sobre todo, su comportamiento con volumen real de datos (delegación).

**Cómo se relaciona con lo que ya existe:**

| Documento | Qué aporta | Relación con esta guía |
|---|---|---|
| [03-manual-cajero-recuento.md](03-manual-cajero-recuento.md) | Cómo se usa la app | El "qué debería pasar" desde el rol |
| [09-plan-de-pruebas-integral.md](09-plan-de-pruebas-integral.md) §4 | RE-01…RE-08, humo del módulo | Esta guía **expande** esos 8 casos |
| [06-escenarios-qa.md](../recounting-rebuild/06-escenarios-qa.md) | Datos sembrados (`ST990001`, `ST990002`, `ST990003`, `RCS-1026`) | Esta guía **los consume**, no los reemplaza |

Lo que **no** está en ningún otro lado y sí acá: el §1 de delegación.

---

## §0 — Pre-vuelo

Nada de lo de abajo tiene sentido si esto no se cumple primero.

| # | Verificación | Cómo | 🔴 bloqueante |
|---|---|---|---|
| 0.1 | Tulas **En bóveda** disponibles | Reception dejó tulas en `Almacenada en boveda` | 🔴 |
| 0.2 | El cajero tiene **mesa asignada** | `CH-Tbl-CountingDesks` con `Enable = true` (43 filas hoy) | 🔴 |
| 0.3 | Denominaciones sembradas | `CH-Tbl-MoneyDenominations` con `Enabled = true` (39 filas) | 🔴 |
| 0.4 | `CH-Flow-Process XML Files` | ON si vas a probar la contadora (RE-06) | ⚠️ |
| 0.5 | Escenarios QA sembrados | Guía 06, sección 8 (inventario) | 🔴 |
| 0.6 | La app **publicada**, no solo guardada | `pac solution export` trae la versión **publicada**: un save sin publish no viaja | 🔴 |

> **0.6 no es teórico.** Ya pasó en esta app: una corrección de fórmula guardada en
> Studio no apareció en el export porque faltaba publicar.

---

## §1 — Delegación (el capítulo que más importa)

### 1.1 Por qué importa acá

El límite de filas de la app es **500** (`DefaultConnectedDataSourceMaxGetRowsCount`).
Cuando una consulta **no se delega**, Power Apps trae solo las primeras 500 filas y
filtra en memoria: si el registro que buscás quedó fuera de esa ventana,
**no existe para la app**. No hay error, no hay aviso al usuario. Simplemente no aparece.

Las tablas de esta app están muy por encima de ese límite:

| Tabla | Filas | Sobre el límite |
|---|---:|---|
| `CH-Tbl-Bags` | 5000 | ✖ 10× |
| `CH-Tbl-DeclaredDeposits` | 5000 | ✖ 10× |
| `CH-Tbl-DeclaredChecks` | 5000 | ✖ 10× |
| `CH-Tbl-DepositsRecountings` | 5000 | ✖ 10× |
| `CH-Tbl-Checks` | 5000 | ✖ 10× |
| `CH-Tbl-CountedMoneyDetails` | 5000 | ✖ 10× |
| `CH-Tbl-XMLReadingDetails` | 5000 | ✖ 10× |
| `CH-Tbl-DeclaredBags` | 4348 | ✖ 8.7× |
| `CH-Tbl-ApprovalRequests` | 2220 | ✖ 4.4× |
| `CH-Tbl-RecountingSessionDetails` | 1501 | ✖ 3× |
| `CH-Tbl-RecountingSessions` | 627 | ✖ |
| `CH-Tbl-CountingDesks` | 43 | ✔ |
| `CH-Tbl-MoneyDenominations` | 39 | ✔ |

**Trece tablas, once por encima del límite.** Por eso una prueba funcional "con dos
tulas de juguete" no dice nada: hay que probar contra volumen.

### 1.2 El patrón que SÍ delega

Casi todas las consultas de la app usan esta forma, y está bien:

```powerfx
Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')
```

Aunque parezca que "sale" del lookup, comparar un lookup contra la **clave primaria**
de la tabla relacionada es el idioma estándar de Dataverse y se traduce a
`_sanic_bagid_value eq <guid>`. Delega.

Lo mismo aplica a `'Created By'.User = gvarUser.User` y `Cashier.User = gvarUser.User`:
verificado que en el datasource `Users` la columna `systemuserid` se llama `'User'`, o
sea que también es lookup → PK.

### 1.3 El patrón que NO delega

```powerfx
IsBlank(BagID.'CH-Tbl-Bags')     ← ✖ requiere join, no delega
IsBlank(BagID)                    ← ✔ se traduce a _sanic_bagid_value eq null
```

La diferencia es sutil: `= <guid>` sobre la travesía delega, pero `IsBlank()` sobre la
travesía no. Studio marca el `IsBlank` porque es lo que envuelve la expresión, pero el
culpable es el `.'CH-Tbl-Bags'` de adentro.

**Ocurrencia conocida:** `scrTulas.fx.yaml:574`, dentro de `btnAbrirTul.OnSelect`:

```powerfx
Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul && IsBlank(BagID.'CH-Tbl-Bags'))
```

Con 4348 declaraciones, una tula declarada "vieja" puede caer fuera de las primeras 500
y el cajero recibe *"no está disponible para reconteo"* con una tula perfectamente válida.

### 1.4 Estado del App Checker

El último análisis guardado en la app reporta:

| Regla | Cantidad | Qué es |
|---|---:|---|
| `app-SuggestRemoteExecutionHint` | **2** | Avisos de delegación |
| `app-ForAllWithMutation` | 9 | `ForAll` que muta estado (patrón frágil, no bloqueante) |
| `app-CollectingReadOnlyTable` | 1 | `Collect` sobre tabla de solo lectura |
| `app-ScreenHasManyControls` | 1 | `scrConteo` (247 controles) |
| Accesibilidad | 553 | TabIndex, foco visible, etiquetas |

> **Pendiente honesto:** por análisis del fuente encontré **una** sola expresión no
> delegable (la de §1.3). El App Checker reporta **dos**. El SARIF guardado no trae la
> ubicación del segundo. **Abrí App Checker en Studio y anotá el segundo antes de
> certificar** — no lo pude ubicar desde el código.

### 1.5 Casos de prueba de delegación

Estos casos **no se pueden probar con datos de juguete**. Requieren las tablas cargadas.

| # | Caso | Cómo probarlo | Esperado | 🔴 |
|---|---|---|---|---|
| DEL-01 | Declaración fuera de la ventana | Elegí una `CH-Tbl-DeclaredBags` **antigua** (ordená por `createdon` ascendente, tomá una de las primeras) cuyo `BagID` esté vacío. Escaneá ese `BagNumber` | La app la encuentra. Si dice "no está disponible", **está el bug de §1.3** | 🔴 |
| DEL-02 | Tula fuera de la ventana | Escaneá una tula de `CH-Tbl-Bags` creada hace mucho, en bóveda | Se abre normal | 🔴 |
| DEL-03 | Tula gorda (E1) | `ST000517`, 612 cheques | Los 612 llegan y pagina sin colgarse | 🔴 |
| DEL-04 | Depósitos de una tula con muchos | Tula con >100 depósitos recontados | Aparecen todos en la grilla | 🔴 |
| DEL-05 | Solicitud pendiente antigua | `CH-Tbl-ApprovalRequests` (2220 filas): generá una solicitud, esperá a que queden muchas por encima, reabrí la app | La app recupera **tu** solicitud pendiente | ⚠️ |
| DEL-06 | Sesión del día con historial | `CH-Tbl-RecountingSessions` (627 filas) | Encuentra tu sesión de hoy, no una vieja | 🔴 |
| DEL-07 | Lectura XML con historial | `CH-Tbl-XMLReadingDetails` (5000 filas) | Trae la lectura de **tu** sesión-detalle | ⚠️ |

**Cómo confirmar que una consulta se delegó de verdad:** Studio → **Monitor** (Ver →
Monitor) mientras usás la app. Cada operación muestra la URL OData enviada. Si ves
`$filter=` con tu condición, delegó. Si ves un `$select` sin filtro seguido de mucho
tráfico, no delegó.

---

## §2 — `scrSesion` (abrir la sesión del día)

| # | Caso | Pasos | Esperado | 🔴 |
|---|---|---|---|---|
| SES-01 | Mesa propia | Abrir sesión en tu mesa asignada | Auto-aprobada por el servidor en el mismo Create; no interviene supervisor | 🔴 |
| SES-02 | Mesa ajena | Abrir sesión en otra mesa | Solicitud "Crear sesion de recuento"; pantalla bloqueada "Esperando aprobación" | 🔴 |
| SES-03 | Rechazo | Que el supervisor **rechace** SES-02 | La app muestra el rechazo como estado de primera clase con su motivo; **no queda colgada** | 🔴 |
| SES-04 | Cancelar solicitud | Con SES-02 pendiente, cancelar | Vuelve a la elección de mesa; la solicitud queda cancelada | ⚠️ |
| SES-05 | Elegir otra mesa | Tras rechazo, "Elegir otra" | Permite reintentar sin recargar la app | ⚠️ |
| SES-06 | Sesión ya abierta hoy | Reabrir la app con sesión activa | La retoma, no pide mesa de nuevo | 🔴 |
| SES-07 | Abandonar sesión | Botón abandonar | Cierra la sesión; al reabrir pide mesa | 🔴 |
| SES-08 | Mesa deshabilitada | Poné `Enable = false` en tu mesa | No aparece en el desplegable | ⚠️ |
| SES-09 | Sin mesas | Todas deshabilitadas | Mensaje claro, no pantalla vacía | ⚠️ |
| SES-10 | Polling | Con SES-02 pendiente, aprobar desde Supervisor **sin tocar** la app del cajero | La app se desbloquea sola (timer) | 🔴 |

---

## §3 — `scrTulas` (escanear y elegir)

| # | Caso | Pasos | Esperado | 🔴 |
|---|---|---|---|---|
| TUL-01 | Escaneo feliz | Tula única en bóveda con declaración | Abre `scrConteo` | 🔴 |
| TUL-02 | Homónimas (E2) | `ST990001` | Diálogo con las candidatas; **solo la de bóveda** seleccionable | 🔴 |
| TUL-03 | Sin declaración (E3) | `ST990002` | Solicitud "Tula sin declaracion"; tras aprobación se puede contar | 🔴 |
| TUL-04 | Inexistente | Número inventado | "no existe, no fue recibida o ya fue recontada"; input reseteado y con foco | 🔴 |
| TUL-05 | No está en bóveda | Tula en esclusa o de otro cajero | "no está almacenada en bóveda…" | 🔴 |
| TUL-06 | Retomar la propia | Tula ya abierta en **esta** sesión | La retoma directo (reabrir), no la trata como nueva | 🔴 |
| TUL-07 | Enter en el input | Escribir y Enter | Dispara la apertura igual que el botón | ⚠️ |
| TUL-08 | Input vacío | Enter sin texto | No hace nada, sin error | ⚠️ |
| TUL-09 | Lista de la sesión | Ver la grilla | Muestra las tulas ligadas a **tu** sesión, ordenadas por modificación | 🔴 |
| TUL-10 | Actualizar | Botón actualizar | Refresca la lista sin perder el estado | ⚠️ |
| TUL-11 | KPIs por moneda | NIO / USD / EUR en el rail | Coinciden con la suma real de la sesión | 🔴 |
| TUL-12 | Rechazo de tula sin declaración | Supervisor rechaza TUL-03 | La app lo muestra y libera al cajero | 🔴 |
| TUL-13 | Ir a sesión | Botón de vuelta | Navega a `scrSesion` sin perder la sesión | ⚠️ |

---

## §4 — `scrConteo` (contar)

### 4.1 Efectivo

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| CON-01 | Contar billetes por denominación | Total por fila y total de billetes correctos | 🔴 |
| CON-02 | Contar monedas | Ídem monedas | 🔴 |
| CON-03 | Cantidad 0 / vacío | No rompe el total; no crea detalle basura | ⚠️ |
| CON-04 | Cantidad negativa o texto | Rechazado o ignorado, sin corromper el total | 🔴 |
| CON-05 | Cambio de moneda | Al cambiar NIO/USD/EUR la grilla y los totales cambian de contexto | 🔴 |
| CON-06 | Lectura de contadora (RE-06) | Denominaciones cargadas desde el XML de **tu** máquina | ⚠️ |
| CON-07 | Contadora con flow OFF | Mensaje claro, no cuelgue | 🔴 |

### 4.2 Cheques

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| CON-08 | Marcar cheque declarado | Queda contado | 🔴 |
| CON-09 | Cheque declarado NO marcado | Se reporta como faltante al cerrar | 🔴 |
| CON-10 | Agregar cheque manual | Se suma al total y se reporta como extra | 🔴 |
| CON-11 | Borrar cheque manual | Se descuenta del total | 🔴 |
| CON-12 | Cheque con monto inválido | Rechazado | ⚠️ |

### 4.3 Depósitos

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| CON-13 | Enlazar recontado ↔ declarado | Queda el vínculo; el pie refleja el cambio | 🔴 |
| CON-14 | Desenlazar | Se deshace sin borrar datos | 🔴 |
| CON-15 | Nuevo depósito | Se crea con moneda y cuenta | 🔴 |
| CON-16 | Eliminar depósito | Borra sus detalles y cheques asociados | 🔴 |
| CON-17 | Descartar cambios | Vuelve al último guardado | 🔴 |
| CON-18 | Guardar | Persiste; el chip "sin guardar" desaparece | 🔴 |
| CON-19 | Salir sin guardar | Diálogo de confirmación, no pierde silenciosamente | 🔴 |

### 4.4 Cierre

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| CON-20 | Cierre cuadrado (RE-05) | Totales posteados, cheques **Recontado**, tula **Recontada**, transacción de depósito creada directo. **Idempotente**: reintentar no duplica | 🔴 |
| CON-21 | Cierre con diferencia (RE-07, E4) | Diálogo con hallazgos **por moneda**; solicitud "Tula con diferencia" con el HTML en `RequestDetails`; tula "Pendiente de revisión…"; cajero bloqueado en esa tula | 🔴 |
| CON-22 | Multi-moneda | NIO faltante + USD y EUR con diferencia, en un mismo cierre | 🔴 |
| CON-23 | Aprobación de la diferencia | Supervisor aprueba CON-21 | La app del cajero se desbloquea sola | 🔴 |
| CON-24 | Rechazo de la diferencia | Supervisor rechaza | Se muestra y el cajero puede corregir | 🔴 |
| CON-25 | Cerrar tula sin contar nada | Comportamiento definido, no excepción | ⚠️ |

---

## §5 — Responsive (nuevo)

La app pasó a layout fluido: `DocumentLayoutScaleToFit` y `MaintainAspectRatio` en
**False**, anclas horizontales por fracción, alto atado a `Parent.Height`.

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| RSP-01 | **1366×768** | Todo idéntico al diseño original. Es el criterio de aceptación: si algo se movió a esta medida, hay una fórmula mal | 🔴 |
| RSP-02 | 1024×768 | Sin recortes ni scroll horizontal. En `scrConteo` la columna izquierda baja a ~521 y la derecha a ~453: mirá que las 7 columnas de depósitos no se pisen | 🔴 |
| RSP-03 | 1920×1080 | Columnas centradas y proporcionadas, sin estirar iconos ni botones | 🔴 |
| RSP-04 | `scrTulas` rail | El rail se mantiene en 250 px mientras el contenido crece | 🔴 |
| RSP-05 | `scrSesion` | Tarjeta centrada y chip de usuario pegado a la derecha en toda resolución | ⚠️ |
| RSP-06 | Redimensionar en vivo | Cambiar el tamaño de la ventana con la app abierta: relayout sin artefactos | ⚠️ |

---

## §6 — Concurrencia, errores y borde

| # | Caso | Esperado | 🔴 |
|---|---|---|---|
| BRD-01 | Dos cajeros, misma tula | El segundo no puede abrirla | 🔴 |
| BRD-02 | Supervisor cierra la sesión mientras contás | La app lo detecta por polling y avisa | 🔴 |
| BRD-03 | Pérdida de red al guardar | Error visible con mensaje real, no genérico; los datos no se pierden | 🔴 |
| BRD-04 | Sesión expirada | Reautenticación limpia | ⚠️ |
| BRD-05 | Doble clic en Guardar | No duplica | 🔴 |
| BRD-06 | Volver atrás del navegador | No rompe el estado | ⚠️ |
| BRD-07 | `app-ForAllWithMutation` (9) | Bajo carga, verificar que los `ForAll` que mutan no dejen estado a medias | ⚠️ |

---

## §7 — Go / No-Go

### Bloqueantes (cualquiera en FALLA = no sale)

- [ ] **DEL-01** — la declaración vieja se encuentra (esto es el bug de §1.3)
- [ ] **DEL-02, DEL-03, DEL-04, DEL-06** — volumen
- [ ] **§1.4** — el segundo aviso de delegación ubicado y resuelto o aceptado por escrito
- [ ] SES-01, SES-02, SES-03, SES-06, SES-07, SES-10
- [ ] TUL-01…TUL-06, TUL-09, TUL-11, TUL-12
- [ ] CON-01, CON-02, CON-04, CON-05, CON-08…CON-11, CON-13…CON-20, CON-21…CON-24
- [ ] RSP-01, RSP-02, RSP-03, RSP-04
- [ ] BRD-01, BRD-02, BRD-03, BRD-05

### Riesgos a aceptar por escrito si no se corrigen

- ⚠️ Límite de 500 filas con 11 tablas por encima: cualquier consulta que se
  descubra no delegada es una falla silenciosa, no un error visible.
- ⚠️ `app-ForAllWithMutation` × 9.
- ⚠️ 553 hallazgos de accesibilidad.
- ⚠️ `scrConteo` con 247 controles (`app-ScreenHasManyControls`): impacto en tiempo de carga.

### Registro

| Caso | Fecha | Quién | Resultado | Evidencia / ticket |
|---|---|---|---|---|
| | | | | |
