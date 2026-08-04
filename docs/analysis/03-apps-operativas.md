# Análisis de Apps Operativas — CashHubApps

Fuente: Canvas Apps desempaquetadas en `solutions/unpacked/apps/CanvasApps/src/` (citado en este documento con la ruta original `src/apps/CanvasApps/src/`, previa a la reestructuración del repo). Todo lo documentado sale de los archivos `.fx.yaml`, `CanvasManifest.json`, `ComponentReferences.json` y `DataSources/*.json` de cada app. No se infiere nada que no esté en el código.

> **⚠ Corrección posterior (verificada contra `Entity.xml`)**: el hallazgo "esclusa/exclusa" de este documento tiene la **dirección invertida**. El esquema vivo (`solutions/unpacked/core/Entities/sanic_CHTblBags/Entity.xml`) usa la grafía correcta `'Recibida en esclusa'` y **Recounting coincide con él**; las desincronizadas son las copias locales cacheadas (`pkgs/TableDefinitions/CH-Tbl-Bags.json`) de Reception, Supervisor, Deposits Tracker y Email Validation Console, que conservan `'exclusa'`. Este documento comparó contra esa copia cacheada, por eso concluyó lo opuesto. Detalle en [`referencia/typos-conocidos.md` §6](../referencia/typos-conocidos.md). Las menciones a `'Recibida en exclusa'` y `'Pendiente de revision por supervisor - exclusa'` más abajo deben leerse con la grafía "esclusa" del esquema vivo.

Apps analizadas:

- `sanic_chappreception_9ff29` — **Reception** (esclusa)
- `sanic_chapprecounting_54962` — **Recounting** (recuento / cajeros)
- `sanic_chappsupervisor_75678` — **Supervisor**

---

## 1. App Reception (`sanic_chappreception_9ff29`)

### 1.1 Pantallas

| Pantalla (archivo) | Propósito | Proceso físico |
|---|---|---|
| `Screen CreacionLotesRecepcion.fx.yaml` (1.68 MB) | Crear un **lote de recepción** (`CH-Tbl-ReceptionBatches`), cargar transportadora, y capturar N **planillas** (`CH-Tbl-Receptions`) con sus **tulas/bolsas** (`CH-Tbl-Bags`) declaradas por el cliente. | El oficial de bóveda recibe físicamente las bolsas de una transportadora y las registra una por una contra la planilla de conducción. |
| `Screen AdministracionDeLotes.fx.yaml` (1.63 MB) | Listar lotes/planillas ya creados, mover el lote de "esclusa" a "bóveda", y editar/agregar/eliminar/remapear tulas de un lote existente. | Gestión posterior a la recepción: mover custodia física de esclusa a bóveda temporal, y corregir errores de captura. |

`App.fx.yaml` no define `OnStart` con lógica de rol — solo inicializa (y luego limpia) las colecciones `colPlanillas`/`colTulas` para forzar su tipo de columna (patrón típico de Power Apps para tipar una colección vacía).

### 1.2 OnStart / variables globales / seguridad

- **No hay `Set(gvarUser, ...)` en `App.fx.yaml`** de esta app (a diferencia de Recounting y Supervisor). No hay detección de rol ni de pertenencia a grupos AAD.
- **No se encontró ninguna referencia a `sanic_CHEVAADGroup_*`** en ningún archivo de esta app (ni de las otras dos). La seguridad de "quién puede usar esta app" no está implementada en Power Fx — se asume que se controla fuera de la app (roles de seguridad Dataverse / a quién se comparte la app).
- El menú de navegación (`Table({Icon:..., Label:..., Screen:...})`) está **hardcodeado dentro de cada pantalla** (duplicado literalmente en ambas), no viene de `App.Formulas` ni de una variable global — inconsistencia frente a la app Supervisor, que sí centraliza el menú en `gvarNavItems` (ver más abajo).
- Variables de contexto clave: `varSelectedReceptionBacth` (nombre con typo "Bacth"; guarda el registro de `CH-Tbl-ReceptionBatches` activo), `varCurrentRow`/`varRowIndex` (fila de planilla en edición), `varSoloGuardar` (flag para reconstruir colecciones tras guardar sin cerrar el lote).

### 1.3 Tablas Dataverse — lectura/escritura

| Tabla | Operaciones |
|---|---|
| `CH-Tbl-ReceptionBatches` | `Patch` (crear lote vía `Form1`; actualizar `ReceptionStatus` a `Recibido`/`En boveda`) |
| `CH-Tbl-Receptions` (planillas) | `Patch` (crear/editar cada planilla vía `'form Receptions'`, asociarla a `ReceptionBatch`); `Patch` para `Modificar numero de planilla` (solo vía approval) |
| `CH-Tbl-Bags` (tulas) | `Patch` (crear/editar tula, cambio de `BagStatus`); `RemoveIf`/`Remove` (borrar tula al eliminar planilla o directamente); `Patch` para mover a `'Almacenada en boveda'` |
| `CH-Tbl-ApprovalRequests` | `Patch` — crea solicitudes de aprobación (`RequestType`: `"Agregar tula"`, `"Eliminar tula"`, `"Remapear tula"`, `"Modificar numero de planilla"`, `"Editar tula"`) cuando el lote ya no está en estado editable libremente |
| `CH-Tbl-Bags` / `CH-Tbl-ReceptionBatches` (lectura) | `Filter`/`LookUp` para poblar colecciones locales `colPlanillas`/`colTulas`/`colPlanillasABoveda`/`colTulasABoveda` |
| `CH-Tbl-ClientBranches`, `CH-Tbl-Clients`, `CH-Tbl-TransportCompanies` | Solo lectura — combos de cliente/sucursal/transportadora |
| `Accounts` | Datasource presente pero sin referencias de uso detectadas en las 2 pantallas |

No se detectó ningún `.Run(` (Power Automate) en esta app.

### 1.4 Flujos Power Automate

Ninguno invocado desde esta app.

### 1.5 Navegación / user journey (Oficial de bóveda)

1. Abre la app → pantalla `AdministracionDeLotes` es implícitamente la vista de gestión (aunque no hay `StartScreen` explícito verificable en `App.fx.yaml`, el menú lateral referencia ambas pantallas).
2. **Crear lote**: navega a `CreacionLotesRecepcion` → completa transportadora (`Form1` sobre `CH-Tbl-ReceptionBatches`) → por cada planilla declarada, llena datos de cliente/sucursal/montos declarados y sus tulas → al confirmar, un `timer Submit` va guardando cada planilla vía `SubmitForm` en cascada (una fila por tick) y al terminar marca el lote `ReceptionStatus = Recibido`.
3. **Gestión de lotes**: en `AdministracionDeLotes`, filtra por lote o por planilla (`TabList1`), y desde el `CommandBar` puede: `Actualizar` (Refresh), `MoverABoveda` (cambia `BagStatus` de todas las tulas del lote/planilla de `'Recibida en exclusa'` a `'Almacenada en boveda'`, y el lote a `'En boveda'`), `Editar` (recarga el lote en colecciones y navega de vuelta a `CreacionLotesRecepcion` para reeditar), `Agregar`/`Eliminar`/`Remapear`/`Editar` tula (a nivel de tula individual).
4. Si el lote ya pasó de estado editable (`En progreso`/`Recibido`), cualquier alta/baja/remapeo de tula **no se aplica directo**: se crea un `CH-Tbl-ApprovalRequests` y la tula queda en `BagStatus = 'Pendiente de revision por supervisor - exclusa'`, a la espera de que el Supervisor apruebe o rechace (ver app Supervisor, pantalla Approvals).

### 1.6 Componentes compartidos

- **No usa `sanic_chclsharedcomponents`** (no hay referencias en `ComponentReferences.json` ni en el código).
- Usa `cat_powercatcomponentlibrary` (PowerCAT): `Fluent Details List`, `Fluent Command Bar`, `Combobox.pcfdataset`.
- Tiene sus **propios componentes locales** en `pkgs/Components/`: `ExpandMenu`, `Header`, `Panel`, `Dialog` — duplicados de los que probablemente existen en la librería compartida, en vez de reutilizarla.

### 1.7 Lógica de negocio clave — estados

- **`ReceptionStatus` (CH-Tbl-ReceptionBatches)**: `En progreso` → `Recibido` (al terminar de guardar todas las planillas) → `En boveda` (al mover custodia).
- **`BagStatus` (CH-Tbl-Bags)**: `Recibida en exclusa` → `Almacenada en boveda` → (más adelante, en Recounting) `Asignada a cajero` → `Recontada`. Además existe el estado transitorio `Pendiente de revision por supervisor - exclusa` cuando una edición de tula requiere aprobación.
- **Manejo de diferencias**: esta app **no calcula faltantes/sobrantes** (eso ocurre en Recounting); su única gestión de "excepción" es el circuito de aprobación para cambios estructurales (agregar/eliminar/remapear tula, corregir número de planilla) una vez que el lote salió del estado libremente editable.
- No existe una opción para eliminar un lote completo, solo tulas individuales.

---

## 2. App Recounting (`sanic_chapprecounting_54962`)

### 2.1 Pantallas

| Pantalla | Propósito |
|---|---|
| `Screen1.fx.yaml` (661 KB) | Pantalla única y real de trabajo del cajero: selección/creación de sesión de recuento, escaneo de tula, recuento de efectivo por denominación, gestión de cheques, cierre de tula (con o sin diferencias). |
| `Screen2.fx.yaml` (94 bytes) | Prácticamente vacía (solo `Fill`/`LoadingSpinnerColor`, sin controles). Pantalla sin uso aparente — posible remanente de plantilla. |

### 2.2 OnStart / variables globales / seguridad

- `App.fx.yaml`: `OnStart: =Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email))`. `Users` es el datasource nativo `systemuser` de Dataverse — o sea, el rol/identidad se resuelve contra el usuario de sistema, **no contra un grupo AAD ni variable de entorno** `sanic_CHEVAADGroup_*` (tampoco existen en esta app).
- `Screen1.OnVisible` (al entrar): `Refresh` de `CH-Tbl-Bags`, `CH-Tbl-ApprovalRequests`, `CH-Tbl-RecountingSessions`; busca si el cajero ya tiene una sesión de recuento abierta hoy (`varRecountingSession = LookUp('CH-Tbl-RecountingSessions', Cashier.User = gvarUser.User && 'Created On' es de hoy)`); carga denominaciones habilitadas (`colDefinedDenominations` desde `CH-Tbl-MoneyDenominations`); revisa si el cajero tiene una `CH-Tbl-ApprovalRequests` propia pendiente (`ApprovalStatus` en blanco) de los tipos `"Crear sesion de recuento"`, `"Tula sin declaracion"`, `"Tula con diferencia"`, `"Tula recontada sin declaracion"` — si la hay, bloquea la UI con `varScreenSpinnerMessage = "Esperando aprobacion del supervisor"`.
- **Hallazgo**: ese mismo bloque de "revisar aprobaciones pendientes" aparece **duplicado dos veces consecutivas** dentro de `OnVisible`, la segunda precedida por el comentario `// CODIGO NUEVO: 17/06/2026` — la lógica original no fue removida, quedó reescrita al lado (código muerto/redundante).
- Variables globales de negocio: `gvarSelectedTula` (tula activa en recuento), `gvarDatosConteo` (datos de sesión de mesa/máquina contadora), `gvarSelectedDeclaredDeposit`.

### 2.3 Tablas Dataverse — lectura/escritura

Tablas con más referencias en la pantalla: `CH-Tbl-Bags` (114), `CH-Tbl-Checks` (102), `CH-Tbl-DeclaredDeposits` (85), `CH-Tbl-DeclaredChecks` (81), `CH-Tbl-DepositsRecountings` (50/48), `CH-Tbl-RecountingSessions` (42/14), `CH-Tbl-CountedMoneyDetails` (28), `CH-Tbl-ApprovalRequests` (21), `CH-Tbl-DeclaredBags` (18), `CH-Tbl-MoneyDenominations` (12), `CH-Tbl-Receptions` (8), `CH-Tbl-Transactions` (5), `CH-Tbl-RecountingSessionDetails` (4), `CH-Tbl-CountingDesks` (3-4), `CH-Tbl-XMLReadings`/`CH-Tbl-XMLReadingDetails` (2).

Escrituras (`Patch`) confirmadas hacia: `CH-Tbl-RecountingSessions` (crear/confirmar sesión), `CH-Tbl-Bags` (cambios de `BagStatus`, totales `TotalNIO/TotalUSD/TotalEUR`), `CH-Tbl-DeclaredBags` (enlazar tula física con declaración), `CH-Tbl-DeclaredDeposits`/`CH-Tbl-DeclaredChecks`/`CH-Tbl-Checks`/`CH-Tbl-CountedMoneyDetails`/`CH-Tbl-DepositsRecountings` (detalle del recuento por depósito/moneda/cheque), `CH-Tbl-RecountingSessionDetails` (una fila por tula asignada a la sesión), `CH-Tbl-Transactions` (posteo final cuando la tula cierra **sin** diferencias), `CH-Tbl-ApprovalRequests` (solicitudes de aprobación).

### 2.4 Flujos Power Automate

- `'CH-Flow-ProcessXMLFiles'.Run(gvarDatosConteo.ID_Sesion, gvarDatosConteo.Serial_Maquina)` — botón "Obtener recuento de la máquina contadora" (icono reload). Dispara el flujo que procesa los reportes `.xml` generados automáticamente por la máquina contadora de billetes, ligados a `CH-Tbl-XMLReadings`/`CH-Tbl-XMLReadingDetails` y a la sesión/mesa de recuento (`CH-Tbl-RecountingSessionDetails`).

### 2.5 Navegación / user journey (Cajero)

Al ser una app de una sola pantalla real, el "journey" es todo dentro de `Screen1`:

1. **Sesión de recuento**: si no tiene sesión abierta hoy, crea una vía `Form1` sobre `CH-Tbl-RecountingSessions` (cajero + mesa de conteo). Al confirmar (`OnSuccess`):
   - Si la mesa elegida coincide con `varAssignedDesk` (`LookUp('CH-Tbl-CountingDesks', Cajero.User = gvarUser.User)`) → `varIsOwnDesk = true` → **auto-aprobación**: `Patch` de `Confirmed: true` directo sobre la sesión y creación de un `CH-Tbl-ApprovalRequests` con `ApprovalStatus` ya en `Aprobado` (comentario en código: `// ✅ ESTE ES EL FIX DEFINITIVO`).
   - Si es otra mesa → crea el `ApprovalRequest` sin `ApprovalStatus` (pendiente) y espera aprobación del supervisor.
2. **Escaneo de tula**: busca la tula por código entre `'Recibida en esclusa'` (sic, ver hallazgo abajo) / `'Almacenada en boveda'` / `'Asignada a cajero'`. Si la tula no tiene declaración asociada, se genera `ApprovalRequest` tipo `"Tula sin declaracion"`.
3. **Recuento**: por cada depósito dentro de la tula, captura efectivo por denominación (`colRecountedMoneyDetails`), cheques (`colChecks`) y compara contra lo declarado.
4. **Cierre de tula** (`dialog_ConfirmarTerminarTula`):
   - Calcula diferencias globales (NIO/USD/EUR) y por transacción (`colRecountedDepostiWithDifference`), arma un reporte HTML (`varRequestDetails`).
   - **Si hay diferencias** (o la tula no tenía declaración): `BagStatus → 'Pendiente de revision por supervisor - recuento'` y crea `ApprovalRequest` (`"Tula con diferencia"` o `"Tula recontada sin declaracion"`), quedando bloqueado hasta que el supervisor resuelva.
   - **Si no hay diferencias**: postea totales en la tula, marca cheques `Recontado`, `BagStatus → Recontada`, y **crea directamente** los registros en `CH-Tbl-Transactions` (sin pasar por aprobación).

### 2.6 Componentes compartidos

- Tampoco referencia `sanic_chclsharedcomponents`. Usa PowerCAT (`Fluent Details List` u otros vía `cat_powercatcomponentlibrary`) y su propio `Dialog` local en `pkgs/Components/`.

### 2.7 Lógica de negocio clave — estados y diferencias

- **`BagStatus`** transitado en esta app: `Almacenada en boveda` → `Asignada a cajero` → `Recontada` (camino feliz) o → `Pendiente de revision por supervisor - recuento` (con diferencia, hasta resolución).
- **Faltante/Sobrante**: el cálculo de diferencia ocurre acá (comparación recontado vs. declarado por moneda y por depósito), pero el **posteo contable real** (tipo `Sobrante`/`Faltante`/`Deposito` en `CH-Tbl-Transactions`) se ejecuta recién cuando el **Supervisor aprueba** la solicitud `"Tula con diferencia"` (ver app Supervisor). Es decir, el cajero detecta y reporta la diferencia; el supervisor la audita y es quien efectivamente contabiliza el faltante/sobrante.

### 2.8 Hallazgos / rarezas puntuales

1. **Typo en option set hardcodeado**: línea ~8968/9918 usa el literal `'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'` (con "esclusa"), mientras que el valor real definido en la tabla (`pkgs/TableDefinitions/CH-Tbl-Bags.json`, y usado correctamente en Reception y Supervisor) es **`'Recibida en exclusa'`**. Esto afecta el `Filter` que decide si una tula escaneada puede considerarse "recibida" para el flujo de recuento — si el label no existe, la comparación no cumple nunca esa rama.
2. **Bloque `OnVisible` duplicado** (ver 2.2) con comentario de fecha `17/06/2026` — sugiere un parche reciente que no limpió el código anterior.
3. **Código comentado sin remover** alrededor de la línea 9200 (bloque `/* ... */` con lógica vieja de selección de tula).
4. **Auto-aprobación "fix definitivo"**: cuando el cajero recuenta en su propia mesa asignada, el sistema autoaprueba su propia sesión de recuento (crea el `ApprovalRequest` ya en estado `Aprobado`), sin intervención real de un supervisor — vale la pena confirmar si esto es una decisión de negocio consciente o un atajo que rompe la segregación de funciones esperada en un proceso de bóveda.
5. `Screen2` no tiene contenido — candidata a limpieza.

---

## 3. App Supervisor (`sanic_chappsupervisor_75678`)

### 3.1 Pantallas

| Pantalla | Propósito |
|---|---|
| `Screen Approvals.fx.yaml` (1.63 MB) | Bandeja de aprobación/rechazo de todas las `CH-Tbl-ApprovalRequests` generadas por Reception y Recounting. Es el corazón operativo del rol Supervisor. |
| `Screen BagLocation.fx.yaml` (1.58 MB) | Consulta de ubicación/estado de tulas (solo lectura — no se detectó ningún `Patch`). |
| `Screen OperationSummary.fx.yaml` (1.57 MB) | Resumen de operaciones por cajero/sesión de recuento (solo lectura). |
| `Screen ChecksSummary.fx.yaml` (1.56 MB) | Administración de cheques recontados; permite marcarlos como `'Enviado a digitacion'` (única escritura de esta pantalla) para pasarlos al proceso de digitización (app `sanic_chappdigitization`, fuera de este análisis). |

### 3.2 OnStart / App.Formulas / seguridad

```
Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email));
Set(gvarNavItems, Table(
  {Icon:"ProductRelease", Label:"Aprobaciones", Screen:'Screen Approvals', ...},
  {Icon:"ProductVariant", Label:"Localizacion de tulas", Screen:'Screen BagLocation', ...},
  {Icon:"ExploreData", Label:"Resumen de operaciones", Screen:'Screen OperationSummary', ...},
  {Icon:"StackIndicator", Label:"Administracion de cheques", Screen:'Screen ChecksSummary', ...}
));
```

- Al igual que Recounting, `gvarUser` se resuelve contra `Users` (systemuser de Dataverse), sin chequeo de grupo AAD ni variable de entorno `sanic_CHEVAADGroup_*` (no existen referencias en toda la app). Esta app es la **única de las tres que sí centraliza el menú en una variable global de `App.Formulas`** (`gvarNavItems`), a diferencia de Reception que lo hardcodea por pantalla.
- No hay ninguna comprobación de "¿este usuario es realmente supervisor?" dentro del código — el control de acceso depende exclusivamente de compartir/no compartir la app y de los roles de seguridad Dataverse subyacentes.

### 3.3 Tablas Dataverse — lectura/escritura

- **Approvals**: lee `CH-Tbl-ApprovalRequests` (bandeja `Table1`), y según `RequestType` hace `Patch`/`Remove` sobre: `CH-Tbl-Bags`, `CH-Tbl-Receptions`, `CH-Tbl-RecountingSessions`, `CH-Tbl-DeclaredBags`, `CH-Tbl-RecountingSessionDetails`, `CH-Tbl-Transactions`, `CH-Tbl-Checks`; y siempre cierra con `Patch('CH-Tbl-ApprovalRequests', ..., {ApprovalStatus, ResolutionDatetime: Now(), SolvedBy: gvarUser})`.
- **BagLocation**: solo lectura de `CH-Tbl-Bags`/`CH-Tbl-DeclaredBags`.
- **OperationSummary**: solo lectura de `CH-Tbl-Bags`, `CH-Tbl-RecountingSessions`, `CH-Tbl-DepositsRecountings`, `CH-Tbl-Checks`.
- **ChecksSummary**: lectura de `CH-Tbl-Checks`/`CH-Tbl-RecountingSessions`; único `Patch` observado: `Patch('CH-Tbl-Checks', ..., {CheckStatus: 'Enviado a digitacion'})`.

No se detectó ningún `.Run(` (Power Automate) en ninguna de las 4 pantallas de esta app.

### 3.4 Navegación / user journey (Supervisor)

1. Entra a la app → ve el menú lateral (`gvarNavItems`) con 4 opciones y aterriza en `Screen Approvals`.
2. **Aprobaciones**: en `Table1` ve las `CH-Tbl-ApprovalRequests` pendientes (`ApprovalStatus` en blanco). Por cada fila, dos botones ejecutan un `Switch(Table1.Selected.RequestType, ...)`:
   - **Aprobar** (`SubmitButton1`): aplica el efecto real de la solicitud, parseando `RequestResources` (JSON) con `ParseJSON(...)`. Casos manejados: `"Remapear tula"`, `"Agregar tula"`, `"Eliminar tula"`, `"Modificar numero de planilla"`, `"Crear sesion de recuento"`, `"Primera tula en planilla"`, `"Tula sin declaracion"`, `"Tula con diferencia"`, `"Tula recontada sin declaracion"`.
   - **Rechazar**: revierte la tula a su `BagPreviousState` (guardado dentro del propio `RequestResources` al momento de crear la solicitud) para los tipos que lo aplican; para `"Agregar tula"`, `"Modificar numero de planilla"` y `"Primera tula en planilla"` explícitamente **no hace nada** (comentario `//Do nothing`) más que marcar la solicitud como `Rechazado`.
3. **Caso `"Tula con diferencia"` (aprobado)**: recalcula, por cada depósito recontado enlazado a una declaración, la diferencia (`Diferencia = recontado - declarado`); si `Diferencia > 0` genera una transacción `TransactionType = Sobrante`, si `< 0` genera `Faltante`, y siempre agrega la transacción base `Deposito`. También cubre depósitos declarados sin contraparte recontada (se postean como `Faltante` + `Deposito`) y depósitos recontados sin declaración (se postean como `Deposito` con referencia "Sobrante"). Al final: `Patch` masivo a `CH-Tbl-Transactions`, cheques → `Recontado`, tula → `BagStatus = Recontada`, y el detalle de sesión (`CH-Tbl-RecountingSessionDetails`) → `'Cerrado con diferencias'`.
4. **Localización de tulas / Resumen de operaciones / Cheques**: pantallas de consulta y, en el caso de cheques, el paso operativo de mandarlos a digitalizar.

### 3.5 Componentes compartidos

- Tampoco referencia `sanic_chclsharedcomponents`. Usa PowerCAT y un `ExpandMenu` local propio en `pkgs/Components/` (idéntico en estructura al de Reception, pero copiado/mantenido por separado).

### 3.6 Lógica de negocio clave — estados y diferencias

- **`ApprovalStatus` (CH-Tbl-ApprovalRequests)**: blanco (pendiente) → `Aprobado` / `Rechazado`, con `ResolutionDatetime` y `SolvedBy = gvarUser`.
- **`TransactionType` (CH-Tbl-Transactions)**: `Deposito`, `Sobrante`, `Faltante` — generados exclusivamente en el momento de aprobar una `"Tula con diferencia"` (o directamente por Recounting cuando la tula cierra sin diferencia, con `TransactionType = Deposito` implícito en ese flujo).
- **`RecountingSessionDetailStatus` (`'ch-recountingDetailsStatus'`)**: se fija en `'Cerrado con diferencias'` al aprobar una tula con diferencia.
- El Supervisor es el **único actor que efectivamente contabiliza faltantes/sobrantes** en `CH-Tbl-Transactions`; Reception y Recounting solo detectan/reportan la excepción.

### 3.7 Hallazgos / rarezas puntuales

1. `Notify("Entra")` — mensaje de depuración dejado en el branch `"Tula recontada sin declaracion"` del botón Aprobar (línea ~1554).
2. Varios `RequestType` en el botón **Rechazar** tienen comentario `//Do nothing` pero en algunos casos (`"Eliminar tula"`) sí ejecutan un `Patch` real pese al comentario — el comentario está desactualizado respecto al código.
3. Ningún control de rol dentro de la app: cualquier usuario al que se le comparta esta app puede aprobar/rechazar y contabilizar faltantes/sobrantes sin una verificación adicional en Power Fx.

---

## 4. Hallazgos transversales (las 3 apps)

1. **No existe uso de variables de entorno `sanic_CHEVAADGroup_*` ni de grupos AAD** en ninguna de las tres apps operativas, pese a que el contexto de negocio las menciona. La única "identidad" resuelta en runtime es `gvarUser = LookUp(Users, 'Primary Email' = User().Email)` contra el `systemuser` de Dataverse — el control de quién ve qué pantalla/app se apoya enteramente en la seguridad de Dataverse y en compartir la app, no en lógica Power Fx.
2. **Ninguna de las tres apps usa el componente compartido `sanic_chclsharedcomponents`**; cada una mantiene copias locales de `Dialog`, `ExpandMenu`, `Header`, `Panel` en su propio `pkgs/Components/`, con el riesgo de divergencia que eso implica.
3. **El circuito de aprobación (`CH-Tbl-ApprovalRequests` + `RequestResources` como JSON string)** es el mecanismo central que conecta las tres apps: Reception y Recounting generan solicitudes con el estado previo embebido como JSON (para poder revertir en caso de rechazo), y Supervisor es el único punto donde ese JSON se parsea (`ParseJSON`) y se materializa el cambio.
4. **Cadena de estados de `BagStatus`** de punta a punta, reconstruida a partir de las tres apps: `Recibida en exclusa` (Reception) → `Almacenada en boveda` (Reception, tras "Mover a bóveda") → `Asignada a cajero` (Supervisor, al aprobar `"Primera tula en planilla"` / `"Tula sin declaracion"`) → `Recontada` (Recounting, cierre sin diferencia, o Supervisor al aprobar `"Tula con diferencia"`), con el desvío `Pendiente de revision por supervisor - exclusa` / `- recuento` cada vez que una acción requiere aprobación.
