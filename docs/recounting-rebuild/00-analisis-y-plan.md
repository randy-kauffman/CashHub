# CH-App-Recounting v2 — Análisis y plan de mejora

> Estado: **PROPUESTA** (pendiente de aprobación). Analizada la v1 (`sanic_chapprecounting`,
> msapp 54962) con 4 agentes: bugs (26 hallazgos), performance/delegación (30),
> seguridad (13) y funcional. Fuente v1 desempaquetada con `pac canvas unpack` (formato 0.30):
> una sola `Screen1` de **11.239 líneas**; `DataRowLimit = 500`.

---

## 1. Veredicto del análisis

### 1.1 Los que rompen DINERO (arreglar por diseño, no por parche)

| # | Hallazgo | Efecto |
|---|---|---|
| D-1 | Lectura de contadora: `Filter(XMLReadingDetails, XMLReading.XMLReadingId in Filter(XMLReadings, …2 saltos…))` — `in` con subconsulta **jamás delega** | Con >500 filas históricas la columna **Auto** de billetes muestra 0: el cajero recuenta con totales de máquina equivocados |
| D-2 | 12 `Sum(Filter('CH-Tbl-DepositsRecountings', BagID.RecountingSession…))` — 2 saltos, no delegable | Los totales de caja del panel lateral muestran **cifras parciales de dinero sin aviso** |
| D-3 | `UpdateIf('CH-Tbl-Checks', RelatedReCounterDepositID.BagID… )` — 2 saltos | Al terminar la tula solo los primeros 500 cheques pasan a `Recontado`; el resto queda en estado viejo, sin error |
| D-4 | `colDenominationTotals` es por **sesión** y nunca se limpia; el `Reset` de la columna Auto está comentado | El conteo de la máquina se hereda en **cada** depósito de la tula → efectivo duplicado/triplicado en `CashAmount` y en los totales de la bolsa |
| D-5 | El efectivo de la contadora se suma a `CashAmount` pero **no se persiste** en `CH-Tbl-CountedMoneyDetails` (solo la parte manual) | El desglose de denominaciones nunca cuadra con el monto: arqueo irreconstruible |
| D-6 | `Patch('CH-Tbl-DeclaredChecks', …, {DeclaredDepositID: Blank()})` al destildar un cheque — limpia el **lookup padre** en vez de `MatchedCheck` | Cheques declarados huérfanos de su declaración para siempre; desaparecen de todas las validaciones |
| D-7 | `Patch` de enlace contra `gvarSelectedDeclaredDeposit` **Blank** (depósito sin declaración, caso legítimo) | Crea fila fantasma en `CH-Tbl-DeclaredDeposits` y pisa el `AccountNumber` recién escrito |
| D-8 | "Terminar" valida `colRecountedDeposits` **45 líneas antes** del `ClearCollect` que la puebla | El primer clic no reporta ninguna diferencia por transacción al supervisor |
| D-9 | `OrderID` generado con `Max()+1` dentro de `ForAll` → duplicados | Editar el monto de un cheque sobrescribe el de todos los que comparten OrderID; tildar uno tilda todos |

### 1.2 Seguridad (la app decide lo que debe decidir el servidor)

| # | Hallazgo |
|---|---|
| S-1 | **Auto-aprobación client-side**: si la mesa es la propia, el canvas escribe `Confirmed: true` y crea el ApprovalRequest ya `Aprobado`. Bypass: con `gvarUser` Blank, `LookUp(CountingDesks, Cajero.User = Blank())` devuelve la primera mesa sin cajero → cualquiera entra auto-aprobado |
| S-2 | Totales de la tula (`TotalNIO/USD/EUR`) y los asientos de `CH-Tbl-Transactions` se generan desde el dispositivo del cajero, sumando colecciones en memoria |
| S-3 | **Inyección HTML** en `RequestDetails`: `CheckNumber` (texto libre), `RemitentName`, `ReceiptID` concatenados sin escapar → lo que el supervisor ve al aprobar es falsificable |
| S-4 | Polling **falla abierto**: la condición es `ApprovalStatus = Blank()` — un **rechazo** libera al cajero igual que una aprobación (no existe "Rechazado" en toda la app) |
| S-5 | Autorización 100 % en `Visible`/`DisplayMode`; borrado en cascada de evidencia financiera (`RemoveIf` de Checks + CountedMoneyDetails + DepositsRecountings) sin gate server-side |
| S-6 | `Notify` de debug en producción con GUIDs y serial de la contadora |

### 1.3 Performance (por qué se siente lenta)

- **N+1 brutales**: hasta ~200 round-trips secuenciales al pulsar "Terminar" (un `LookUp` remoto por cheque declarado), ~82 al abrir una tula (un `Filter` remoto por depósito declarado).
- **2 timers cada 5 s** con `Refresh` que invalida la caché de Bags/Sessions → ~10 consultas en cascada por ciclo mientras hay spinner; uno tiene `Start: =true` literal y corre **siempre**.
- **`Gallery.AllItems` + 5 PCF por fila** en `gal Checks`: los handlers leen controles de fila desde fuera → se instancian todos los componentes React de todas las filas, visibles o no. La virtualización queda anulada.
- El mismo `OnSelect` de ~90-120 líneas **copiado en 6-7 controles por fila** de las dos galerías de depósitos (~1.400 líneas duplicadas que el motor indexa por fila).
- `OnVisible` con 3 `Refresh` + el mismo `LookUp` de approvals ejecutado 3 veces, sin `Concurrent`, y un bloque entero de código muerto.
- Forms invisibles de 1×1 px con `Choices()` sobre la tabla de tulas (13k+ filas) como mecanismo de guardado.

### 1.4 Controles no nativos (a eliminar)

- **PowerCAT `Dialog` ×8** (librería `cat_powercatcomponentlibrary`, copia local 8 meses vieja). Cada instancia contiene un `htmlViewer` a pantalla completa **siempre instanciado**. → Se reemplazan por el patrón de diálogo nativo de Reception v2 (overlay + card con controles clásicos).
- PCF modernos en uso: Button, Checkbox, Combobox, Dropdown, NumberInput, Spinner, TabList, TextInput, Text. → En v2 quedan **solo** los acordados: `TabList` (pestañas), `Spinner` (esperas). El resto pasa a controles clásicos, que además son los únicos con `Radius`.

---

## 2. Arquitectura v2

### 2.1 Tres pantallas (adiós al monolito)

| Pantalla | Rol | Qué muestra |
|---|---|---|
| `scrSesion` | Apertura de sesión | Tarjeta centrada: mesa de conteo (dropdown clásico), botón Solicitar, y el estado de espera de aprobación (spinner + polling). Si ya hay sesión de hoy, navega sola a `scrTulas` |
| `scrTulas` | Hub de la sesión | Rail izquierdo con KPIs de sesión por moneda (efectivo / cheques / total, solo tulas recontadas). Cuerpo: barra **scan-first** ("Escaneá o digitá el número de tula" + Enter) y lista de tulas aperturadas con estado. Diálogos: desambiguación de tula recibida, de tula declarada, y confirmación "tula sin declaración" |
| `scrConteo` | El corazón: recuento de una tula | Header con Tula/Planilla/Cliente + estado + Terminar/Cerrar. Franja de matrices Rec/Dec/Diff por moneda. Izquierda: depósitos (TabList Declarado/Recontado). Derecha: depósito en curso (TabList Billetes/Monedas/Cheques, totales en vivo, botonera). Diálogos nativos de confirmación |

Cada `Navigate()` acota el grafo de dependencias: un `Refresh` en `scrTulas` ya no recalcula
las grillas de denominaciones.

### 2.2 Prerrequisitos server-side (el patrón espejo de Reception v2)

**Cuatro lookups espejo** poblados por plugin Pre-operation (mismo patrón que
`ClientBranchNameMirrorPlugin`), que vuelven delegable TODO lo crítico:

| Columna nueva | Tabla | Mata |
|---|---|---|
| `RecountingSessionMirror` (lookup) | `CH-Tbl-DepositsRecountings` | D-2 (12 sums de totales de caja) |
| `BagMirror` (lookup) | `CH-Tbl-Checks` | D-3, N+1 de "Terminar" |
| `DeclaredBagMirror` (lookup) | `CH-Tbl-DeclaredChecks` | N+1 de apertura de tula (82 round-trips → 1 query) |
| `RecountingSessionDetailMirror` (lookup) | `CH-Tbl-XMLReadingDetails` | D-1 (lectura de contadora delegable en 1 salto) |

**Guard server-side** (plugin, mismo espíritu que `GuardReceptionPlugin`):

1. **Aprobaciones**: la app deja de escribir `ApprovalStatus` y `Confirmed`. Un plugin en
   Create de `ApprovalRequests` tipo "Crear sesion de recuento" compara
   `CountingDesk.Cajero` vs `InitiatingUserId` y auto-aprueba **server-side** (mata S-1).
2. **Totales**: `TotalNIO/USD/EUR` de la bolsa los recalcula el plugin al transicionar
   `BagStatus`, sumando desde `CH-Tbl-DepositsRecountings` persistidos (mata S-2a, D-4/D-5 en la capa de datos).
3. **Transacciones**: `CH-Tbl-Transactions` las crea el plugin al pasar la bolsa a
   `Recontada` — la app no vuelve a tocar esa tabla (mata S-2b).
4. **Gates de estado**: rechazar escrituras de Checks/CountedMoneyDetails/DepositsRecountings
   si la Bag no está `Asignada a cajero` o la sesión no es del usuario (mata S-5).
5. **Bajas acotadas (`Delete`)**: el cajero necesita **retirar** lo que él mismo creó —es el
   *Cancelar solicitud* / *Elegir otra mesa* de `02` (`D02-4`, `B02-3`)—. El guard debe
   permitir `Delete` **solo** de: (a) `CH-Tbl-ApprovalRequests` **propias** (`Created By` =
   usuario que ejecuta) y **sin resolver** (`ApprovalStatus` en blanco); (b)
   `CH-Tbl-RecountingSessions` **propias** (`Cashier` = usuario) y **sin confirmar**
   (`Confirmed` distinto de `true`). Todo lo demás se rechaza con mensaje. Sin esto, la
   mesa queda ocupada todo el día y la solicitud sigue en la cola del supervisor.
6. **Transición de apertura escrita por el cajero — tolerada y validada.** Tras aprobarse una
   `"Tula sin declaracion"`, `03` hace el `Patch` de la bolsa a `Asignada a cajero`
   (`03` §9.1 → §6.3, con re-lectura por PK y verificación de estado). **Hoy no existe
   ningún plugin que haga esa transición** —la hace el canvas de `CH-App-Supervisor`, y el
   `Patch` de `03` es idempotente sobre él—, así que el guard **debe tolerarla**: aceptar
   `Almacenada en boveda → Asignada a cajero` cuando existe una `ApprovalRequest`
   `"Tula sin declaracion"` **aprobada** para esa bolsa y ese cajero, y **rechazarla** si no
   la hay. Cuando el plugin exista, la app deja de escribirla y la rama se simplifica a
   `Select(btnReabrirTul)`.
7. **GC de solicitudes legadas `"Primera tula en planilla"`.** Con `L-6` decidido, la v2 no
   crea **ni espera** ese tipo. Pero una solicitud v1 todavía pendiente sigue siendo
   peligrosa: al aprobarla, `CH-App-Supervisor` re-patchea la bolsa a `Asignada a cajero`
   con la **sesión y la declaración viejas** (verificado en `Screen Approvals.fx.yaml`) —
   sobre una tula que la v2 quizá ya recontó. El guard debe **rechazar o cerrar** las
   pendientes de ese tipo cuya bolsa ya tenga `RecountingSession` de otra sesión o un
   `BagStatus` posterior a `Almacenada en boveda`.

> Alcance: son cambios de `CashHubCore` (C#, dominio puro + tests, como siempre).
> Sin ellos, la v2 canvas seguiría mintiendo dinero a escala. **No son opcionales.**

### 2.3 HTML de approval v2 (los "feitos")

- **Plantilla nueva y elegante**: tarjeta con header (tula/planilla/cliente/cajero/fecha),
  chips de estado, tablas limpias con zebra, misma paleta de la app (tokens del tema),
  tipografía del sistema del render de Outlook/Approvals. Diseño incluido en el mockup.
- **`HtmlEscape()`** como función de `App.Formulas` (cadena de `Substitute` sobre `& < > " '`)
  aplicada a TODO dato variable (mata S-3).
- Formato monetario único `"#,###.00"` (la v1 tenía `"#,####.00"` con grupos de 4 dígitos),
  un solo `</body></html>`, y las 6 secciones condicionales conservadas.
- `RequestResources` con `JSON()` en vez de concatenación de strings.

### 2.4 Controles: nativos primero

- Etiquetas, botones, inputs, galerías, iconos, rectángulos: **clásicos** (los robustos, y
  los únicos con `Radius` — recordatorio: el rectángulo NO tiene border radius, las
  superficies redondeadas se hacen con Botón clásico según la receta de Reception).
- Modernos permitidos: `TabList` (Declarado/Recontado y Billetes/Monedas/Cheques) y
  `Spinner` (esperas de aprobación). Nada más.
- **Cero PowerCAT**: los 8 diálogos pasan al patrón nativo (overlay `Fill` semitransparente +
  card de Botón clásico + título/cuerpo/acciones), igual que Reception v2.
- Sin forms ocultos: el guardado es `Patch` explícito contra Dataverse.

### 2.5 Componentes canvas (lo repetitivo, una sola vez)

| Componente | Reemplaza | Instancias |
|---|---|---|
| `cmpMatrizMoneda` | La matriz Rec/Dec/Diff × (Qty Chk / Chk / Cash) que la v1 dibuja con ~60 labels sueltos (`Label6_*`) | 3 en `scrConteo` (NIO/USD/EUR) + 1 para el depósito en curso |
| `cmpKpiSesion` | Tarjeta de total por moneda del rail | 3 en `scrTulas` |
| `cmpDialogo` | El chrome de los 8 diálogos PowerCAT: overlay, card, título, subtítulo, Cancelar/Continuar (propiedades de entrada: título, texto, visible; salida: OnConfirmar/OnCancelar) | ~8 usos entre pantallas |

Entradas/salidas con custom properties; los totales SIEMPRE calculados desde colecciones
(`colChecks`, `colDetalleConteo`), nunca desde `Gallery.AllItems` (mata la des-virtualización).

### 2.6 UX para cajeros (fluidez o muerte)

- **Scan-first**: el input de tula tiene foco al entrar a `scrTulas`, Enter dispara, el
  lector de código de barras funciona sin tocar el mouse.
- **Grilla de denominaciones teclado-primero**: una fila por denominación
  (`denominación × (auto + manual) = total`), TAB baja por la columna Cantidad, totales en
  vivo desde la colección (cero lag), tipografía numérica grande alineada a la derecha.
- **La columna Auto se resetea por depósito** (D-4 muerto por diseño): `colDenominationTotals`
  se limpia al crear/cambiar de depósito, y **la cantidad persistida es manual + auto**
  (D-5 muerto: el arqueo se reconstruye desde `CountedMoneyDetails`).
- **Claves locales `LocalId` (GUID)** para `colChecks` — nunca `Max()+1` (D-9 muerto).
- **Confirmar ANTES de ejecutar**: ningún diálogo corre la acción destructiva antes del
  "Continuar" (la v1 borraba el conteo y después preguntaba).
- **Estados visibles**: la espera de aprobación distingue Pendiente / **Rechazado** (S-4
  muerto: el rechazo bloquea con mensaje, no libera).
- Polling: **un** timer, 15 s, una sola query delegable, sin `Refresh` en cascada.
- Un solo handler por fila de galería (fondo transparente + `Select(Parent)`), no 6 copias.

---

## 3. Qué NO cambia (contratos con el resto de CashHub)

- Estados de `BagStatus` (159460000-5), `RecountingDetailStatus`, tipos de `RequestType`
  (strings exactos) y el contrato `RequestResources` con CH-App-Supervisor.
- El flujo de la máquina contadora (`CH-Flow-ProcessXMLFiles` + OneDrive + XMLReadings)
  se sigue invocando igual; solo cambia la query de lectura (delegable vía espejo).
- El modelo de sesión diaria por cajero/mesa y el ciclo esclusa → bóveda → cajero →
  recontada / revisión.
- La ruta "Tula sin declaración" y la desambiguación manual de tulas homónimas.

## 4. Mockup

`docs/recounting-rebuild/mockup-recounting.html` — todas las vistas de las 3 pantallas,
los diálogos, y la plantilla nueva del HTML de approval. Mismo lenguaje visual que
`docs/reception-rebuild/mockup-reception.html` (tokens, tipografía, densidad).

## 5. Pipeline (igual que Reception v2)

1. ✔ Análisis (este documento, §1)
2. ⏳ **Este plan + mockup → aprobación**
3. Guías MD por pantalla (`01-app.md`, `02-scrSesion… 04-scrConteo`) con la skill
   `powerapps-screen-guides` + review de fidelidad vs mockup + review antagonista
4. Plugins/espejos de §2.2 en CashHubCore (dominio puro + tests + backfill)
5. Scaffolding en Studio (humano) → verificación de scaffold → vestido por código
   (unpack → fx.yaml → pack → deploy)
6. Icono PNG
7. Stress test con data sintética masiva (seeder dedicado de reconteo)
8. Commit y cierre

---

## 6. Ledger de divergencias declaradas

Toda decisión que se aparta del **mockup**, del **plan** o de lo que hace **CH-App-Reception
v2** queda acá, con su motivo. **El modelo manda sobre el mockup**; ninguna divergencia es
silenciosa. Se llenó al escribir `01-app.md` y las guías de pantalla lo siguen alimentando.

| ID | Qué | Por qué | Dónde |
|---|---|---|---|
| **L-1** | Se activan **dos** banderas de *Características próximas*: **Funciones definidas por el usuario** y **Propiedades de componente mejoradas**. Reception v2 no activa ninguna | `HtmlEscape(t)` y las 14 `fx…` del HTML de approval llevan **parámetros**: sin UDFs, la misma expresión se copia inline en los 3 lugares que arman HTML y se desincronizan a la primera corrección. `cmpDialogo` expone `OnConfirmar`/`OnCancelar`, que son propiedades de **comportamiento**. Las dos tienen fallback documentado y probado | `01` §1.4, §3.1 nota **F**, §8.3 nota **B** |
| **L-2** | Formato monetario **`"#,##0.00"`**, no `"#,###.00"` como dicen el plan (§2.3) y el pie del mockup | `"#,###"` imprime **cadena vacía** para el entero 0 ⇒ `C$ .00`. Hallazgo del QA humano de Reception 1.31. Para todo valor ≥ 1 el resultado es idéntico; para 0 dice `0.00` en vez de `.00` | `01` §3.1 (`FmtMoney`), checklist §9 |
| **L-3** | El espejo **E-4** son **DOS** columnas en `CH-Tbl-XMLReadingDetails` (`RecountingSessionDetailMirror` **+** `XMLReadingActiva`), no una como pedía el plan §2.2 | La consulta de la contadora necesita **dos** condiciones: la sesión-detalle y que la lectura esté activa. Con un solo espejo habría que volver a `CH-Tbl-XMLReadings` a preguntar `IsActive`, y vuelve el segundo salto que E-4 venía a matar (bug **D-1**) | `01` §2.2 |
| **L-4** | El **número de cuenta** se captura y muestra como **entero de hasta 9 dígitos**, sin guiones. El mockup dibuja `100-501-2345` | `AccountNumber` es `int` (mín. 1, máx. `999999999`) en `CH-Tbl-DepositsRecountings` **y** en `CH-Tbl-DeclaredDeposits`. El modelo no admite guiones ni 10 dígitos. Entrada con `Format: TextFormat.Number`, `Patch` con `Value(…)`, display con `Text(…)` sin máscara | `01` §2.4, §8.3 (ejemplo de uso) |
| **L-5** | **Todos los atributos HTML de la plantilla de approval usan comillas SIMPLES**, y `HtmlEscape()` escapa **cinco** caracteres (`& < > " '`), no tres como Reception | En Power Fx una comilla doble dentro de un literal hay que duplicarla (`""`), y el template de 6 secciones se vuelve ilegible. Con comillas simples el HTML se lee tal cual; el precio es escapar también el apóstrofo, que el plan §2.3 ya pedía | `01` §3.1 (`HtmlEscape`, `HtmlApEstilo`, `HtmlApTh…`) |
| **L-6** | ✅ **DECIDIDO** — **la v2 NO crea el `RequestType` `"Primera tula en planilla"`.** Abrir una tula **con** declaración va **directo** a `scrConteo`, sin solicitud ni espera de supervisor | El mockup (`B1`/`B2`) manda al cajero directo al conteo al elegir tula y declaración, y el caller fijó los 4 tipos del contrato. **DECIDIDO y comunicado al dueño del proceso.** Consecuencia aceptada: la transición `Almacenada en boveda → Asignada a cajero` deja de pasar por aprobación en el camino feliz, y el Supervisor deja de ver ese tipo desde Recounting. El tipo sigue existiendo en el guard (`ApprovalRequestResources.cs`) y en `docs/referencia/catalogo-de-choices.md`. **La v2 tampoco lo ESPERA**: ninguna consulta de recuperación lo incluye (`01` §5.8, regla 5) — la ruta con declaración no espera nada. Las solicitudes legadas todavía pendientes las cierra el guard (§2.2, punto 7) | `01` §5.6, §5.8; `03` §11.4; `00` §2.2 punto 7 |
| **L-7** | En `cmpDialogo`, **`Visible` NO es una propiedad personalizada** (se usa la nativa de la instancia), y se agregan **dos entradas** que el plan no listaba: `TextoCancelar` y `AnchoCard` | Canvas rechaza una propiedad personalizada llamada `Visible`, `Width`, `Height`, `X`, `Y` o `Fill`: ya son nativas. `TextoCancelar` lo exige el diálogo `C6-3` (*"Seguir capturando"*); `AnchoCard` cubre los tres anchos del mockup (`400` / `560` / `330`) sin tres componentes distintos | `01` §8.3 |
| **L-8** | En `cmpMatrizMoneda` las **3 diferencias se CALCULAN dentro** del componente y salen como propiedades de **salida**. El plan hablaba de "los 9 valores" de entrada | Si la diferencia entrara como parámetro habría **dos verdades** para el mismo número: la que calcula la pantalla y la que muestra la matriz. Se desincronizan a la primera corrección. Como salida, `04` las reutiliza en las validaciones de *Terminar* sin recalcular | `01` §8.1 |
| **L-9** | El diálogo `B2` (*Seleccione la declaración*) **no muestra el origen del correo** ni el conteo de cheques declarados, y la fecha sale de `'Created On'` de la **DeclaredBag** | El remitente exige `CH-Tbl-Received Emails` (tabla grande) y un dot-walk de 2 saltos (`DeclarationID.ReceivedEmail.…`, regla D-1); el conteo de cheques exige cargar `DeclaredChecks` antes de que el cajero haya elegido declaración. La tarjeta conserva lo que **sí** decide: cliente, fecha, monto por moneda y `TotalDeposits` (rollup nativo). Consecuencia: se eliminan **dos** datasources de la v1 | `01` §2.1 (exclusiones), `03` |
| **L-10** | `cmpDialogo` se usa en **3** diálogos, no en los ~8 que estimaba el plan §2.5. Los otros 8 se arman con la **receta 7.2** | Un componente canvas **no puede recibir controles hijos**: `cmpDialogo` solo puede llevar título, subtítulo y acciones. Los diálogos con cuerpo rico (galerías de *pick cards*, banners apilados, tablas de resumen, spinner + kv) no caben. El chrome sigue siendo uno solo: la receta 7.2 es la **misma** estructura que el componente implementa por dentro | `01` §7.2, §8.3 nota **C** |
| **L-11** | Símbolo del dólar: **`"U$ "`**, no `"US$"` como en Reception v2 | Es lo que dibuja el mockup de reconteo en las 18 vistas y en el HTML de approval. Las dos apps quedan con símbolos distintos para la misma moneda — **declarado a propósito**, siguiendo el mockup aprobado | `01` §3.1 (`SimboloUSD`) |
| **L-12** | **No hay sombras** en cards, chips ni pills (`box-shadow` de `.card`, `.kpi`, `.mat`, `.pickCard`) | El Botón clásico —única superficie con `Radius`— **no expone `DropShadow`**. La separación la da el borde `ThemeLine`. La única sombra de la app es la de la card de diálogo, que es un **Contenedor** | `01` §7.1, §8.1, §8.2 |
| **L-13** | El **Menú desplegable clásico** queda de **esquina viva** (mockup: `border-radius: 7px`) y el `HintText` de las entradas de texto no toma color | Ninguno de los dos controles clásicos expone esas propiedades. El borde, el tamaño y el fondo sí se respetan. Misma divergencia ya aceptada en Reception v2 | `01` §6.1 |
| **L-14** | Los números **no** usan `font-variant-numeric: tabular-nums` | No existe en canvas. Se compensa alineando a la derecha toda columna numérica y fijando el ancho de columna | `01` §6.1, §8.1 |
| **L-15** | `DataRowLimit` se configura en **2000**, pero **todas** las reglas de delegación y todas las consultas se escriben asumiendo **500** | El 2000 es red de seguridad, no un supuesto: si una consulta solo funciona con 2000, es un bug de la guía. La v1 estaba en 500 y de ahí salían los truncamientos silenciosos | `01` §1.2, §2.3 |
| **L-16** | La **guarda dura de identidad** (`IsBlank(gvarUser)`) se implementa como un **overlay bloqueante en `scrSesion`**, no como una cuarta pantalla | El plan fija 3 pantallas. Un overlay al tope del z-order con `Visible: !gvarIdentidadOk`, sin botón de salida y sin `Navigate`, bloquea igual y no agrega superficie. Es el fix estructural de **S-1**: la v1 seguía andando con `gvarUser` en blanco y le entregaba la primera mesa sin cajero a cualquiera | `01` §3.2 |
| **L-17** | El **clic en el velo del diálogo NO cierra** el modal (`btnBloqueoDlg.OnSelect = false`) | El mockup no define el comportamiento del clic fuera. Todos los diálogos de esta app confirman acciones que pierden dinero contado o disparan solicitudes al supervisor: cerrarlos por un clic accidental fuera de la card es peor que obligar a apuntar a *Cancelar* | `01` §7.2, §8.3 |
| **L-18** | La app escribe **`BagNumber`** en `CH-Tbl-ApprovalRequests`, que la v1 dejaba vacío | Es una columna existente (`nvarchar(100)`) que permite al Supervisor filtrar y buscar por N° de tula **sin parsear el JSON de `RequestResources`**. No altera el contrato de claves (`ApprovalRequestResources.cs` no la inspecciona) | `01` §2.4, §5.7 |
| **L-19** | Se agrega el datasource **`Users`**, que Reception v2 no usa | `Cashier` de la sesión es un lookup a `SystemUser` y las comparaciones del polling son `'Created By'.User = gvarUser.User` (PK contra PK, delegable). `User()` sola no da un registro tipado. Uso acotado y declarado: **un** `LookUp` por email en `App.OnStart` y ninguna consulta más | `01` §2.1, §3.2 |
| **L-20** | Se eliminan **5 tablas** que la v1 traía (`CH-Tbl-XMLReadings`, `CH-Tbl-Transactions`, `CH-Tbl-Declarations`, `CH-Tbl-Received Emails`, `CH-Tbl-ReceptionBatches`) y los 3 samples de plantilla | Cada exclusión con su motivo en `01` §2.1. `CH-Tbl-Transactions` sale porque **la app deja de crear asientos** (los genera el plugin, §2.2 punto 3); `CH-Tbl-XMLReadings` sale por el espejo E-4; `CH-Tbl-ReceptionBatches` sale porque el N° de lote se lee **navegando** el registro ya cargado, que no exige el datasource del otro lado | `01` §2.1 |
| **L-21** | El espejo **`BagMirror`** del plan §2.2 **ya existe** en el modelo con el nombre **`Bag`** (`sanic_bag` en `CH-Tbl-Checks`, descripción *"Mantenido por plugin. No editar a mano."*) | No hay columna nueva que crear: lo que falta es **confirmar que el plugin la puebla** y hacer el **backfill** de lo histórico. Las guías la nombran `Bag`, no `BagMirror` — usar el nombre del plan no compilaría | `01` §2.2 (E-2), checklist §9 |

> 🚩 **`L-6` es la única divergencia que cambia el COMPORTAMIENTO DE NEGOCIO** y está
> **DECIDIDA**. Las demás son de implementación, de fidelidad visual o correcciones del
> modelo sobre el mockup.

### 6.1 Divergencias de pantalla consolidadas (`L-22` en adelante)

Nacieron en `02`, `03` y `04` y suben acá porque se apartan del **mockup**, del **plan** o
del comportamiento de la **v1**. Cada una conserva su `D0X-n` de origen, que es donde vive
la justificación completa. **No suben** las divergencias que la consolidación **resolvió**
(`D02-2`, `D02-3`, `D02-10`, `D03-1`, `D03-10`, `D04-1`, `D04-16`, `D04-18`, `D04-19`,
`D04-20`): quedaron marcadas como resueltas en su guía de origen, apuntando a `01`.

| ID | Qué | Por qué | Origen |
|---|---|---|---|
| **L-22** | La **espera** y el **rechazo** de `02` no son un overlay (receta 7.7, regla 5): son **modos de la misma tarjeta** | Es lo que dibuja el mockup (`A1`/`A2`/`A3` son el mismo lienzo). En `02` **no hay nada detrás** que preservar; en `03`/`04` sí, y ahí sí son overlays | `02` `D02-1` |
| **L-23** | *Cancelar solicitud* / *Elegir otra mesa* se implementan como **abandono con bajas**: `Remove` de la solicitud + `Remove` de la sesión, las dos o ninguna; y si falla el `Patch` de la solicitud, la sesión recién creada se **borra** (compensación) | El plan define cómo se **crea** y cómo se **resuelve** una solicitud, no cómo se **retira**. Sin las bajas, la mesa queda ocupada todo el día. Exige el privilegio de `Delete` acotado de §2.2 punto 5 | `02` `D02-4`, `D02-5`, `D02-7` |
| **L-24** | El desplegable y los chips de mesa muestran **`Name` a secas**: nada de *"· Bóveda A"* / *"· Bóveda B"* | `CH-Tbl-CountingDesks` no tiene ninguna columna de ubicación (`01` §2.4). **El modelo manda sobre el mockup** | `02` `D02-8`, `03` `D03-7`, `04` `D04-2` |
| **L-25** | El chip de usuario muestra solo el nombre, sin *"· Cajero de conteo"* | `User()` da nombre y correo, **no** el puesto, y los conectores de directorio están prohibidos (`01` §1.5). Misma decisión que Reception v2 | `02` `D02-12` |
| **L-26** | Las fechas usan los **tres formatos de `01` §3.1** (`16/08/2026 07:41`, `16/08/2026`, `07:41`), no el *"16 ago 2026 · 07:41"* del mockup | Inventar un `"dd mmm yyyy"` sería un cuarto formato fuera del inventario, y `hh` sería reloj de 12 h **sin AM/PM** | `02` `D02-13`, `03` `D03-13` |
| **L-27** | Todo texto que el mockup pinta con **dos pesos en una línea** (banner de rechazo, pie del rail) se parte en **dos etiquetas** | Una Etiqueta clásica tiene **un solo** `FontWeight`. Es la única forma nativa de conseguir el contraste | `02` `D02-14`, `03` `D03-13` |
| **L-28** | `02` agrega un **estado vacío** de mesas (*"No hay mesas disponibles…"* en ámbar) y deshabilita el desplegable; el mockup no lo contempla | **Mejora deliberada**: con todas las mesas ocupadas, el mockup deja un desplegable vacío y un botón activo, y el cajero no sabe si la app cargó o si no hay mesas | `02` `D02-15` |
| **L-29** | La solicitud de mesa **no escribe `BagNumber`**, contra `L-18` | `BagNumber` es el N° de tula y **esta solicitud no tiene tula**: escribir un valor inventado sería peor que dejarlo vacío. Los tres tipos de `03`/`04` sí la escriben | `02` `D02-17` |
| **L-30** | Una **sesión de hoy sin solicitud** (huérfana) se degrada al estado inicial **con aviso**; la app **no la borra sola** | Solo se alcanza si fallaron el `Patch` de la solicitud **y** su compensación. Borrar automáticamente una sesión cuyo origen la app no conoce es peor que avisar. **Candidato a GC server-side** en el guard de §2.2 | `02` `D02-18` |
| **L-31** | El orden de las mesas se pide con **`Sort(Filter(…), Name)`**, no con `SortByColumns(…, "…")` como pide la regla **D-9** | `SortByColumns` exige el nombre de columna **como texto** y en Dataverse manda el **lógico** (`sanic_name`): un display name ahí compila y falla en runtime. `Sort` con la columna tipada delega igual y no tiene esa ambigüedad | `02` `D02-11` |
| **L-32** | El **Spinner moderno** no reproduce la pista gris del mockup (`.spin{border:3px solid var(--line)}`) | Es uno de los **dos** modernos permitidos y no expone el color de la pista. No hay equivalente clásico animado | `02` `D02-19` |
| **L-33** | La columna **"Apertura"** del mockup se llama **"Actividad"** y muestra `Modified On` | **No existe un sello de apertura** en `CH-Tbl-Bags`. Aproximación **honesta**, no un dato real. Camino de mejora (columna `OpenedOn` por plugin) en `03` §11.4 | `03` `D03-2` |
| **L-34** | El estado vacío de la lista de tulas **no lleva el glifo `⛁`** del mockup | El catálogo de iconos de `01` §6.1 es **cerrado** y no tiene equivalente; como texto sale *tofu* en Open Sans | `03` `D03-3` |
| **L-35** | Los diálogos `B1` y `B2` **no pre-seleccionan** ninguna tarjeta; el mockup dibuja la primera con `pickSel` | Elegir la tula o la declaración equivocada arruina el arqueo entero: centinela `ctx…` en `Defaults(<tabla>)` y botón deshabilitado hasta el clic explícito | `03` `D03-4` |
| **L-36** | La tarjeta **no seleccionable** de `B1` no tiene borde punteado ni nombra al cajero/mesa que la tiene | El Botón clásico no expone `border-style: dashed`; el cajero y la mesa son dot-walks de **dos** saltos (**D-1**) y exigirían dos datasources más. El motivo se dice en palabras | `03` `D03-5` |
| **L-37** | `B2` **no muestra origen del correo ni conteo de cheques** (extiende `L-9`) y **agrega el total EUR** al renglón chico, que el mockup no dibuja | El remitente exige `CH-Tbl-Received Emails` + 2 saltos (`L-20`). El EUR se agrega porque una declaración en euros quedaría **invisible** justo donde se elige contra qué se compara la tula | `03` `D03-6` |
| **L-38** | La salida de la espera de `03` se llama **"Cancelar la espera"** y **no anula** la solicitud | La app **nunca escribe `ApprovalStatus`** (mata **S-1**): no tiene forma de anular una solicitud. Prometerlo sería mentir | `03` `D03-14` |
| **L-39** | Antes de crear una `"Tula sin declaracion"`, `03` **busca la previa** y la **re-adopta** si sigue pendiente o la **consume** si ya está aprobada | Cancelar la espera deja la solicitud viva. Sin esto, volver a escanear la misma tula le manda al supervisor una segunda solicitud idéntica | `03` `D03-15` |
| **L-40** | El `←` del header de `04` y el botón *Cerrar* hacen **lo mismo** (`btnVolverCon.OnSelect = Select(btnCerrarTulaCon)`) | El mockup dibuja los dos y no define qué los diferencia. Una sola definición de "salir de la tula" es lo único que no se puede desincronizar | `04` `D04-3` |
| **L-41** | Las **3 matrices de la franja** muestran lo **PERSISTIDO**; la **4ª** muestra el depósito **en curso** | Un arqueo de tula no puede moverse con cada tecla. El mockup no distingue los dos planos | `04` `D04-4` |
| **L-42** | 🧨 **El pie de la tabla de depósitos muestra totales POR MONEDA** —una línea por moneda con su símbolo, omitiendo las de importe cero—, **no** el único `C$` que suma las tres del mockup | El mockup muestra **solo el subtotal de la moneda mayoritaria y omite las demás**, con lo que un depósito `USD` **desaparece del pie**; la v2 emite **una línea por moneda presente**. **DECIDIDO en la consolidación.** Con una tula de una sola moneda el pie se ve idéntico al mockup | `04` `D04-5` |
| **L-43** | 🧨 **El subtotal de la sección 4 del HTML de approval sale POR MONEDA**: una fila `sub` por moneda presente, con su símbolo | `fxHtmlFilaTotalCheques` recibe **un** símbolo y la sección puede mezclar monedas. Se resuelve **llamando a la misma `fx…` una vez por moneda**, sin tocar `App.Formulas`. **DECIDIDO en la consolidación** | `04` `D04-13` |
| **L-44** | *Desenlazar* **no** borra cheques ni pone `AccountNumber` en blanco (la v1 hacía las dos cosas) | El N° de cuenta es un dato del recuento, no del enlace; y borrar cheques desde el desenlace era una escritura destructiva sin `IfError` ni confirmación | `04` `D04-6` |
| **L-45** | Los pies de grilla, la matriz del depósito y *Total del depósito* se refrescan con el **VOLCADO**, no con cada tecla. El total de **fila** sí es en vivo | Un `Patch` a la colección en un `OnChange` re-renderiza la galería y **mata el foco** (hallazgo del QA humano de Reception 1.31) | `04` `D04-8` |
| **L-46** | Los cheques **manuales** nacen `Seleccionado: true` y **no muestran casilla**; el mockup dibuja un `—` | Un cheque que el cajero agregó a mano **está** en el depósito por definición. Así todas las sumas usan un único predicado | `04` `D04-9` |
| **L-47** | Un cheque manual con **monto 0** bloquea el guardado con mensaje explícito | `CheckAmount` tiene **mínimo 1** en el modelo. La v1 **reseteaba el monto en silencio** | `04` `D04-10` |
| **L-48** | **Siete hallazgos, siete banners** en el diálogo `C5` | El mockup junta *"recontados sin enlazar"* y *"declarados sin enlazar"* en uno y no contempla *"tula sin declaración"*. Una fila por hallazgo hace la galería trivial y el orden explícito | `04` `D04-11` |
| **L-49** | El `kv` de la espera dice **Enviada** (fecha y hora), no **Supervisor** como el mockup | Mientras la solicitud está pendiente **nadie la resolvió**: `SolvedBy` está vacío por definición | `04` `D04-14` |
| **L-50** | 🚩 Al **rechazar** una revisión de tula, la app devuelve la bolsa a `Asignada a cajero` y el detalle a `En progreso` | El mockup `C5` dice *"queda en Pendiente de revisión"*: dejarla ahí deja la tula **muerta** (en `Pendiente` la pantalla es solo lectura y el cajero no puede corregir). **Hoy es CONTRATO en `01` §5.8**, y el canvas del Supervisor ya hace ese mismo `Patch` ⇒ el de `04` es idempotente | `04` `D04-15` |
| **L-51** | La grilla de cheques ofrece **todos los cheques declarados de la TULA** en la moneda del depósito (menos los que otro depósito ya tomó), no solo los del declarado enlazado | Es lo que dibuja el mockup `C3` y lo que permite marcar cheques en un depósito **sin** enlace. La v1 los ataba al enlace, y por eso el desenlace tenía que borrarlos | `04` `D04-17` |
| **L-52** | Seleccionar otro depósito recontado con cambios sin guardar **no abre un diálogo**: avisa y **no cambia** de depósito | Un diálogo con destino pendiente exige una variable más de estado (*"a cuál iba"*) que se puede desincronizar | `04` `D04-21` |
| **L-53** | **Dos patrones de "una sola definición de la acción", no uno.** Los **motores 1×1** —botones transparentes invocados con `Select()`, **nunca** `Visible: false`— se usan cuando la acción **no tiene un control de UI que la SEA**: los **cinco** de `03` (cadena de apertura), el `btnAbandonarSes` de `02` y los **cuatro** de `04` (Bloques V/C/N/M). El **discriminador de origen** (`ctxEliminandoCon`) se usa cuando **sí** lo tiene: el *Eliminar* de la botonera de `04` | Un motor 1×1 no lo ve nadie y puede ser un control aparte; donde el botón **es** de la botonera, un motor gemelo serían **dos** controles con la misma acción destructiva. Regla común, **sin excepción**: `Select()` jamás apunta a un control invisible, y **ningún botón que ABRE un diálogo contiene `Clear(`/`Remove(`/`Patch(`** | `02` §9, `03` `D03-9`, `04` §6.1 y §13.3, `04` `D04-27` |
| **L-54** | 🚩 **TODA la UI trata al cajero de USTED** (*"Escanee o digite un número de tula"*, *"Guarde o descarte el depósito"*, *"Avise al supervisor"*), no de vos. La **prosa de las guías** sigue en voseo: es la voz del autor hablándole al constructor, no la de la app | **El mockup manda**: es el contrato aprobado y escribe usted en las 18 vistas (*"Presione «Nuevo»"*, *"Elija la que tiene físicamente en la mesa"*, *"Si elige la incorrecta…"*). Las guías habían nacido mezclando los dos registros — mitad mockup, mitad `Notify` nuevos en voseo — y una app que tutea en un `Notify` y trata de usted en el diálogo de al lado se lee como dos productos. **DECIDIDO: registro único de usted en todo literal que el usuario lee.** Corolario del mismo criterio: los plurales van **naturales** (`If(n = 1, "1 cheque", "N cheques")`), nunca `"cheque(s)"`, que es exactamente lo que dibuja el mockup | `02` §5, §8, §9; `03` §6.1, §9.3; `04` §9.1, §10.1, §12.2, §13.4 |
| **L-55** | En las secciones **1 y 2** del HTML de approval, la diferencia **cero se pinta VERDE** (`fxClaseDif` ⇒ `n ok`) en las dos. El mockup pinta el cero verde en la sección 1 y con **tinta normal** en la 2 | **Se unifica hacia el verde, no hacia la tinta normal.** El cero es el resultado *bueno* —recontado = declarado— y el supervisor escanea la tabla buscando color: si la sección 2 lo deja en negro, un cero "correcto" se lee igual que una celda sin dato. Una sola función (`fxClaseDif`) decide el color de **toda** diferencia del documento; dos criterios serían dos verdades para el mismo número. **DECIDIDO en la revisión R1** | `01` §3.1 (`fxClaseDif`) |
| **L-56** | El **`kv`** del diálogo `C6-2` (*Efectivo* / *Cheques*) se degrada a **dos renglones de texto** dentro de `Subtitulo`, con un solo peso tipográfico | Consecuencia directa de **`L-10`**: un componente canvas **no puede recibir controles hijos**, y `cmpDialogo` no admite las 4 etiquetas de la receta `01` §7.2.b. Rearmar `C6-2` con la receta 7.2 suelta serían 11 controles más para dos renglones y **dos** definiciones del chrome | `04` `D04-23` |
| **L-57** | El `kv` **Hallazgos** del overlay de espera `C7` muestra **`—`**, no un número, cuando la app se reabrió sobre una tula que ya está esperando revisión | `colHallazgosCon` es **local** y el `OnVisible` la vacía: la **solicitud** se recupera por `BagNumber` (`01` §5.8) pero los **hallazgos no** —se calcularon en *Terminar tula* y **nunca se persistieron**—. `Text(CountRows(…))` sobre la colección vacía imprimiría un **`0` falso por construcción**: sin hallazgos la tula termina por `C4` y no hay nada que esperar. **Aproximación honesta**, mismo criterio que `L-33`. El detalle completo vive en el HTML de la solicitud. Camino de mejora: columna `FindingsCount` en `CH-Tbl-ApprovalRequests` | `04` `D04-26`, §12.1 |
| **L-58** | 🚫 **`colChecksBorradosCon` NO EXISTE.** La colección `{ LocalId, GUID }` de cheques manuales borrados con la papelera se **elimina por completo** de la app: no se siembra, no se llena y no se limpia | Era un **cuarto** lugar donde vivía la misma verdad, con **tres `Clear`** para desincronizar, y el trabajo ya lo hacía el **paso de bajas** del guardado: `Filter(colChecksDVCon As X, IsBlank(LookUp(colChecksCon, GUID = X.'CH-Tbl-Checks' && Seleccionado)))` cubre con **un** predicado los **tres** casos —declarado destildado, manual borrado, fila desaparecida por cambio de moneda—, porque los tres dejan una fila en el snapshot **sin** contraparte seleccionada. Es la regla 2 de `01` §7.5 | `04` §14.5, §8.3, §9.2 paso 4 |
| **L-59** | Los dos overlays de estado de `03` se renombran **`cntDlgEsperaTul` → `cntEsperaTul`** y **`cntDlgRechazoTul` → `cntRechazoTul`**, y `01` §7.7 pasa a definir **dos** contenedores (`cntEspera<Scr>` con `Visible ctxEsperando<Scr>` y `cntRechazo<Scr>` con `Visible ctxRechazada<Scr>`), no uno con el `OR` de las dos banderas | **No son diálogos de decisión** (no tienen `ctxDlg…`): son overlays de **estado del servidor**. Y no comparten **ni un** control: con un solo contenedor, cada uno de sus ~20 hijos tendría que repetir su propia condición de `Visible`. `04` ya los nombraba `cntEsperaCon` / `cntRechazoCon` ⇒ con el renombre **las tres pantallas usan el mismo nombre para la misma cosa**. `02` sigue siendo la excepción de `L-22`: ahí son **modos de la tarjeta** | `01` §7.7, `03` §9.2/§9.4/§10.1 |
| **L-60** | El primer parámetro del flujo de la contadora se manda con **`Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail')`** —GUID como **texto**—, y eso es un **supuesto heredado de la v1**, no un hecho verificado contra el flujo | `'CH-Tbl-RecountingSessionDetail'` es un GUID y `CH-Flow-ProcessXMLFiles` puede esperarlo crudo. **Al construir `04` §8.4 hay que verificar el tipo del parámetro contra el flujo real** (Power Automate → *Cuando Power Apps llama a un flujo*): **si espera GUID, se quita el `Text()`**. Un tipo equivocado **no falla en tiempo de diseño**: falla en runtime con un mensaje del conector, y el cajero cree que la contadora no responde | `01` §7.11 regla 7, `04` §8.4 |
| **L-61** | 🚩 🧨 **`IsBlank(<lookup>)` y `<lookup> = Blank()` PELADOS quedan PROHIBIDOS en todo `Filter`/`LookUp` sobre Dataverse: siempre contra la PK** (`IsBlank(BagID.'CH-Tbl-Bags')`). Nueva regla dura **`D-13`** de `01` §2.3. `03` §6.1 (`Q-4`) pasa a la forma con PK | **Bug de RUNTIME encontrado por el QA humano, que el App Checker NO detecta**: la fórmula compila en verde y ni siquiera marca delegación, pero el conector arma un `$expand` de la *navigation property* y mete su nombre en el `$select` interno ⇒ Dataverse contesta `Could not find a property named 'sanic_Xxx'` y el `ClearCollect` vuelve **VACÍO, sin error visible**. Consecuencia: **toda tula CON declaración caía por la rama "sin declaración"** (`B3`) y le pedía aprobación al supervisor. Con la PK explícita el predicado se pliega a un `eq null` sobre `_sanic_xxx_value`: un salto, delegable. Es el mismo criterio de **PK y nada más que PK** que ya rige `D-1` y la existencia de registros (§5.8) | `01` §2.3 (`D-13`), `03` §6.1, §11.1 `Q-4`, §11.3, §11.4-4 |
| **L-62** | 🧨 **Todo brazo de fallo de un `IfError` con MÁS DE UNA instrucción va envuelto en `If(true, … ; Blank())`.** `IfError(x, a; b)` deja de ser forma válida en las guías: pasa a `IfError(x, If(true, a; b; Blank()))` en las **32** apariciones de las cuatro guías (**6** en la receta de `01` §7.5, **4** en `02`, **6** en `03`, **16** en `04`) | **Dos problemas en el mismo lugar.** (1) **Parser**: `IfError(valor, brazo)` es una llamada de función y una cadena `a; b` **no es un argumento válido** — el `;` corta la lista de argumentos y Studio marca *"Se esperaba un operador"*. (2) **Tipado**: el brazo tiene que unificar con el tipo del primer argumento, y `Notify` (booleano), `Collect` (tabla) y `Patch` (registro) no unifican solos ⇒ la cadena **termina siempre en `Blank()`**. `If(true, …)` es un bloque de comportamiento legítimo y admite el `;`. Agravante: **`pac canvas pack` NO valida fórmulas** — la forma mala se empaqueta, se importa y se publica, y explota recién al abrir Studio | `01` §7.5 regla 1; `02` §8.1, §9.2; `03` §6.3, §6.4, §7, §10.4; `04` §5.4, §5.5, §8.4, §9.2, §11.3, §11.5 |
| **L-63** | 🧨 **`ForAll` sobre un DATASOURCE + `Remove`/`Patch` del MISMO datasource queda PROHIBIDO: patrón SNAPSHOT** (`ClearCollect(colTmp, Filter(ds, …))` → `ForAll(colTmp As X, …)` → `Clear(colTmp)`). Nueva regla dura **`D-14`** de `01` §2.3. Se aplica en `04` §5.5 paso 3 con `colBorrarTmpCon` | **Lo prohíbe el RUNTIME**: la fuente del `ForAll` es una consulta **viva** y borrar filas mientras se la recorre deja el lote **a medio ejecutar y sin error** — el desglose de denominaciones del depósito eliminado quedaba **huérfano en Dataverse**. El paso 2 (cheques) **no** necesita snapshot: itera `colChecksTulaCon`, que ya es una colección local. `colBorrarTmpCon` es **local del paso**: no entra al inventario de `01` §4.2 ni a la siembra de `App.OnStart` | `01` §2.3 (`D-14`), `04` §5.5 |
| **L-64** | 🧨 **`GroupBy` + `AddColumns` FUERA de *Leer contadora*: la agregación pasa a `Distinct` + `Sum` sobre un snapshot local (`colLecturasTmpCon`)**, con proyección explícita a `{ Denominacion, Cantidad }` — el mismo esquema en **español** que declara `01` §4.2 y siembra `App.OnStart` | **`GroupBy` es quisquilloso**: choque de nombres del grupo, fuentes que no acepta, y el `Sum(GruposXml, Quantity)` de `AddColumns` fallando en runtime sobre el resultado de un `Filter` de datasource ⇒ `colAutoContadoraCon` volvía **vacía sin error** y la columna **Auto** quedaba en cero — exactamente el bug **D-1** que esa sección existe para matar. `Distinct()` devuelve la columna **`Value`** (de ahí `D.Value`). `colLecturasTmpCon` es **local del paso** (mismo criterio que `L-63`): no entra al inventario ni a la siembra | `01` §7.11, `04` §8.4, §2 `Q17` |
| **L-65** | 🚩 🧨 **`Navigate()` queda PROHIBIDO en todo `OnVisible`: patrón MOTOR.** Las tres pantallas ganan un motor 1×1 de **salida** — **`btnEntrarTulasSes`** (`02` §1.1), **`btnIrSesionTul`** (`03` §1.1) y **`btnIrTulasCon`** (`04` §1.1) —; las ramas que navegaban pasan a `Select(<motor>)`. Conteos: `02` **43 → 44**, `03` **142 → 143**, `04` **245 → 246** | El App Checker marca *"would automatically always navigate away"*, y tiene razón: una pantalla que navega sola desde su `OnVisible` **no se puede abrir en Studio** para revisarla, y el `Navigate` queda enterrado en una rama de una máquina de estados. Es el mismo **patrón motor** de `L-53`: el `OnVisible` **invoca**, el motor **hace** — con las mismas propiedades (1 × 1, transparente, `Y 767`, `TabIndex -1`, `DisplayMode.Edit` sin condición) y la **misma regla sin excepción: `Visible true`, jamás `Visible false`**. 🚩 **La prohibición es EXCLUSIVA del `OnVisible`**: los `Navigate` de `OnSelect` y de `OnTimerEnd` —incluido el de la rama *Aprobado* de `tmrPollSes` (`02` §10) y los cinco de `04`— **siguen inline**, porque son manejadores de comportamiento y ahí `Navigate()` es legítimo | `01` §4.5; `02` §1, §1.1, §10; `03` §1, §1.1, §6, `D03-9`; `04` §1, §1.1, §6.1 |
| **L-66** | 🧨 **La Casilla clásica NO expone `OnChange`: `chkChequeCon` lleva `OnCheck` Y `OnUncheck`**, las dos con `Set(gvarDepositoSinGuardar, true)` | Tildar y destildar ensucian el depósito **por igual**. Escribir la marca de sucio en una sola de las dos deja que destildar un cheque **no** marque el depósito ⇒ el cajero sale sin que la guarda de salida (`04` §13.1) le avise, y pierde el cambio. Es el **único** control editable de fila que no usa `OnChange`: los otros seis (dos Entradas de cantidad, `inpNumChequeCon`, `ddTipoChequeCon`, `inpMontoChequeCon`, `inpCuentaCon`) sí | `04` §8.3 |
| **L-67** | 🧨 **Los TRES componentes canvas van con `AccessAppScope` (`AllowAccessToGlobals`) en `true`**, no en `false` como decía `01` §8 | **Sin la bandera, los tres NO COMPILAN**: leen los tokens **`Theme*`** de la paleta —los tres— y `cmpMatrizMoneda` / `cmpKpiSesion` leen además las **`fx…`** (`fxMonto`, `fxMontoDif`, `fxCantDif`, `fxColorDif`), y **`App.Formulas` ES ámbito de aplicación**. 🚩 **La bandera NO abre la puerta a los datos**: el contrato *cero datasources, cero `gvar…`, cero `col…` dentro de un componente* **sigue intacto** y verificado (`rg` sobre `Src/Components/*.fx.yaml` ⇒ cero). Lo único que concede —y lo único que se usa— es el **sistema de diseño** y **funciones puras**, que son constantes de la app, no estado | `01` §8, §9 (checklist); `04` §16 |
| **L-68** | ✅ **El paso `CashHub.GuardReception: Update de sanic_chtblapprovalrequest` fue REACTIVADO el `2026-08-17` al cierre de la QA** (`statecode = 0` verificado, `74 / 74` activos). `06` deja la advertencia como **registro histórico**, no como estado actual | El paso se había apagado **a pedido del dueño** y **solo** para esa ronda: sin él, cualquiera puede resolver su propia solicitud (no se exige resolutor ≠ creador ni grupo AAD aprobador). 🧨 **Corolario operativo**: `scripts/qa-approve-pending.py` aprueba con **la misma identidad que creó** la solicitud ⇒ con el paso activo **ya no funciona**. Volver a apagarlo es una operación **deliberada, acotada y con re-activación obligatoria en el mismo turno** (`06` §6 → §7) | `06` (encabezado, §6, §7) |
