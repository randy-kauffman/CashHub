# Supervisión y aprobaciones

## 1. Qué es este proceso

Supervisión es el **control** de todo el circuito de tulas: cada vez
que Recepción o Recuento tocan algo que ya salió de la edición libre (una tula que
no cuadra, una sesión abierta en mesa ajena, un cambio a un lote cerrado), el
sistema genera una **solicitud de aprobación** y la manda a esta bandeja. El
supervisor es además el **único actor que postea faltantes y sobrantes**: ni
Recepción ni Recuento contabilizan la diferencia por sí solos — la transacción nace
cuando el supervisor **aprueba** la solicitud correspondiente. El proceso también
administra la **localización de tulas** (consulta), el **resumen de operaciones**
(consulta) y el **envío de cheques a digitación** (la acción que dispara el
handoff hacia 06).

Los 10 tipos de solicitud que puede recibir esta bandeja:

| Tipo (texto libre exacto) | Lo genera |
|---|---|
| Agregar tula | Recepción |
| Eliminar tula | Recepción |
| Remapear tula | Recepción |
| Editar tula | Recepción |
| Modificar numero de planilla | Recepción |
| Crear sesion de recuento | Recuento |
| Primera tula en planilla | Recuento |
| Tula sin declaracion | Recuento |
| Tula con diferencia | Recuento |
| Tula recontada sin declaracion | Recuento |

```mermaid
flowchart LR
    subgraph P03["Recepción — 03"]
        R1["Cambio a lote/planilla/tula\nya fuera de edición libre"]
    end
    subgraph P05["Recuento — 05"]
        R2["Sesión en mesa ajena,\ntula sin declaración,\ntula con diferencia"]
    end
    R1 --> AR["CH-Tbl-ApprovalRequest\nnace Pendiente"]
    R2 --> AR
    AR --> BAND["scrAprobaciones\nbandeja del supervisor"]
    BAND -->|Aprobar| EFE["Efecto real:\nalta/baja/remapeo de tula,\nconfirmación de sesión,\nasientos Depósito+Sobrante/Faltante"]
    BAND -->|Rechazar| REV["Reversión al estado previo\n(donde aplica)"]
    EFE --> TULAS["scrTulas\nlocalización — solo lectura"]
    EFE --> RES["scrResumen\noperación por cajero/sesión"]
    EFE --> CHQ["scrCheques\nRecontado habilitado"]
    CHQ -->|Enviar a digitación| P06["Digitación — 06"]
```

**Handoffs**:
- **Recibe de** [03 — Recepción de tulas en esclusa](03-recepcion-tulas-esclusa.md)
  y [05 — Recuento de tulas](05-recuento-tulas.md): ambos procesos crean filas en
  `CH-Tbl-ApprovalRequest` (`sanic_chtblapprovalrequest`) cuando su propia app
  detecta una excepción; ninguno resuelve su propia solicitud.
- **Entrega a** [06 — Digitación en core bancario](06-digitacion-core-bancario.md):
  el botón **Enviar** de `scrCheques` es la única puerta de entrada — un cheque
  nunca llega al digitador sin pasar por acá.

## 2. Estados y transiciones

### 2.1 Solicitud de aprobación (`CH-Tbl-ApprovalRequest` / `sanic_chtblapprovalrequest`)

`sanic_approvalstatus` (picklist) — **`Blank()` = Pendiente**, no es un valor del
option set:

| Etiqueta | Valor | Quién transiciona | Guard |
|---|---|---|---|
| *(pendiente — sin valor)* | `null` | Recepción/Recuento al crear | `GuardReceptionPlugin` bloquea que nazca ya resuelta (excepción abajo) |
| Aprobado | `159460000` | Supervisor, botón **Aprobar** | `ReceptionGuard.EvaluateApprovalUpdate` |
| Rechazado | `159460001` | Supervisor, botón **Rechazar** | ídem |

**Única excepción de nacimiento resuelto**: `Crear sesion de recuento` puede nacer
ya `Aprobado` cuando el cajero elige **su propia mesa** — es un atajo de negocio
deliberado, verificado server-side (la sesión referenciada en el JSON tiene que
existir y pertenecer al propio solicitante, resuelto por el plugin **como
sistema**, no por el texto del JSON). Cualquier otro `RequestType` que intente
nacer resuelto se bloquea; si la excepción de sesión propia no se puede probar
con certeza (JSON ilegible, sesión de otro, sesión inexistente) la solicitud **no
se bloquea**: se **degrada a pendiente** — el cajero no se queda sin poder pedir
su sesión por un JSON mal armado.

**Congelamiento**: una vez que `sanic_approvalstatus` tiene valor, el registro
queda **congelado** — ningún Update que toque la decisión pasa, salvo la
excepción de espejo (§6).


## 3. Roles y acceso

| Rol Dataverse | Qué habilita en este proceso |
|---|---|---|
| `CH-SR-CountingSupervisor`  | Acceso a `CH-App-Supervisor`: las 4 pantallas |


**Lo que verifica la app (cosmético) vs. lo que impone el servidor (real)**:

- La app **no verifica ningún rol dentro del código**. Cualquier usuario al que
  se le comparta `CH-App-Supervisor` ve las 4 pantallas; el control de acceso a
  la app depende exclusivamente de la compartición manual .
- Lo que **sí** es real y server-side es la resolución de una solicitud:
  `GuardReceptionPlugin`, step `CashHub.GuardReception: Update de
  sanic_chtblapprovalrequest` (síncrono, Pre-validation), exige — leyendo como
  sistema, no confiando en lo que mande el cliente:
  1. La solicitud sigue Pendiente (`sanic_approvalstatus` sin valor). Si ya
     tiene valor, el Update se **bloquea entero**: registro congelado.
  2. Quien resuelve (`InitiatingUserId`) **no es** quien creó (`createdby`) —
     separación de funciones. Comparación de GUID, nunca de nombre.
  3. Quien resuelve **pertenece al grupo AAD** configurado en `CH-Tbl-Parameter`
     (`Security.ApproverGroup.Supervisor`). Esta verificación es **FAIL-CLOSED**:
     si la fila del parámetro no existe, está inactiva, o su valor no es un GUID
     válido, **nadie puede aprobar ni rechazar** — no hay modo degradado que deje
     pasar por error de configuración..

## 4. Aplicaciones

App: `CH-App-Supervisor`. Cuatro pantallas, todas construidas con
controles clásicos:

| Pantall | Para qué sirve |
|---|---|---|
| `scrResumen` | Resumen de recuentos por cajero/sesión — solo lectura |
| `scrAprobaciones`  | Bandeja de solicitudes — pantalla principal de trabajo |
| `scrTulas`  | Localización de cada tula, cualquier estado — solo lectura |
| `scrCheques`  | Entrega física de cheques recontados a digitación |

## 5. Flujos Power Automate

**No aplica**: este proceso no tiene flujos propios. La resolución de
solicitudes, la generación de asientos y el envío de cheques corren **dentro de
la app canvas** (Power Fx) contra Dataverse directo, sin intermediario de
Power Automate. 

## 6. Tablas


**`CH-Tbl-ApprovalRequest`** (`sanic_chtblapprovalrequest`) — PK singular
`sanic_chtblapprovalrequestid`:

| Columna | Lógico | Notas |
|---|---|---|
| RequestType | `sanic_requesttype` | **Texto libre**, NO es picklist. Comparación server-side es **ordinal exacta** (el guard viejo comparaba ignorando mayúsculas — cerrado) |
| RequestResources | `sanic_requestresources` | JSON plano |
| ApprovalStatus | `sanic_approvalstatus` | Picklist, `Blank()` = Pendiente — ver §2.1 |
| SolvedBy | `sanic_solvedby` | Lookup a `systemuser` — quien resolvió (o el plugin de auto-aprobación) |
| ResolutionDatetime | `sanic_resolutiondatetime` | — |
| StatusReason | `sanic_statusreason` | Motivo del rechazo (obligatorio en la UI) o de la aprobación |
| BagNumber | `sanic_bagnumber` | **Mantenido por plugin. No editar a mano.** Número de tula extraído del JSON al crear, para buscar por tula en la bandeja con delegación |
| *(solicitante)* | `createdby` | `CH-Tbl-ApprovalRequest` **no tiene columna propia de solicitante**  |

**`CH-Tbl-Bags`** (`sanic_chtblbags`) — columnas propias de este proceso:

| Columna | Lógico | Notas |
|---|---|---|
| BagStatus | `sanic_bagstatus` | Máquina de estados de §2.2 |
| BagID | `sanic_bagid` | Número de tula (texto) — **nunca confundir con el GUID** |
| ReceptionBatchId | `sanic_receptionbatchid` | **Mantenido por plugin. No editar a mano.** Espejo del lote de la planilla, para dup-check delegable de "Agregar tula"/"Editar tula" |
| ReceiptIdMirror | `sanic_receiptidmirror` | **Mantenido por plugin. No editar a mano.** Espejo del `ReceiptID` de la planilla, para búsqueda por recibo delegable en `scrTulas` |
| RecountingSession | `sanic_recountingsession` | Columna de **negocio** (la escribe Recuento), no espejo — es el origen del espejo del cheque |

**`CH-Tbl-Transactions`** (`sanic_chtbltransactions`) — los asientos que postea
esta pantalla:

| Columna | Lógico | Notas |
|---|---|---|
| TransactionType | `sanic_transactiontype` | `Deposito` 159460000 · `Faltante` 159460001 · `Sobrante` 159460002 · `Sin declaracion` 159460003 |
| CashAmount / BACLocalCheckAmount / OtherLocalCheckAmount | `sanic_cashamount` / `sanic_baclocalcheckamount` / `sanic_otherlocalcheckamount` | **Por componente** — el neto nunca se calcula ni se guarda |
| RelatedDepositRecounting | `sanic_relateddepositrecounting` | Ancla al depósito recontado. Vacío solo en el camino "declarado sin recontar" (Faltante + Depósito) |
| AccountNumber | `sanic_accountnumber` | `IsSecured=1` — ver FLS en §3 |
| Total | `sanic_total` | **Columna calculada**: Dataverse la rechaza en Create/Update API. **No se puede auditar** |

**`CH-Tbl-DepositsRecounting`** (`sanic_chtbldepositsrecounting`) — ojo con la
ortografía real, verificada contra `Entity.xml`:

| Columna | Lógico |
|---|---|
| CashAmount | `sanic_cashamount` |
| BACLocalCheckAmount | `sanic_baclocalcheckamount` |
| OtherLoca~~l~~CheckAmount | **`sanic_otherlocacheckamount`** — falta la segunda "l" (typo de origen, no corregir: así está publicado) |
| RelatedDeclaredDeposit | `sanic_relateddeclareddeposit` | lado recontado → declarado |

**`CH-Tbl-DeclaredDeposits`** (`sanic_chtbldeclareddeposits`) — la misma columna,
bien escrita acá: `OtherLocalCheckAmount` = `sanic_otherlocalcheckamount`.
`RelatedRecountedDeposit` = `sanic_relatedrecounteddeposit` (lado declarado →
recontado, vacío = nadie lo recontó).

**`CH-Tbl-Checks`** (`sanic_chtblchecks`) — columnas del handshake hacia
Digitación:

| Columna | Lógico | Notas |
|---|---|---|
| CheckStatus | `sanic_checkstatus` | Máquina de §2.3 |
| SentToDigitizationOn / By | `sanic_senttodigitizationon` / `sanic_senttodigitizationby` | **Mantenido por plugin** (`CheckStatusStampPlugin`). El canvas nunca escribe fechas: solo las lee |
| NotReceivedOn / ReceivedOn / ReceivedBy | `sanic_notreceivedon` / `sanic_receivedon` / `sanic_receivedby` | ídem — sellos del proceso 06 |
| Bag | `sanic_bag` | **Mantenido por plugin.** Tula del cheque, derivada del depósito de recuento |
| RecountingSession | `sanic_recountingsession` | **Mantenido por plugin.** Espejo de `sanic_bag.sanic_recountingsession`, para que `scrResumen` cuente cheques por sesión con `CountRows` delegado en vez de materializar filas |

**`CH-Tbl-Parameter`** (`sanic_chtblparameter`) — la fila que hace posible el
cuatro ojos:

| Campo | Valor |
|---|---|
| `sanic_name` | `Security.ApproverGroup.Supervisor` |
| `sanic_value` | GUID del team AAD aprobador |
| `sanic_category` | `159460000` (Security) |
| `sanic_isactive` | `true` — una fila inactiva **cuenta como ausente** |

## 7. Custom APIs

**No aplica hoy — resolución client-side.** No hay ningún Custom API desplegado
para este proceso;.


## 9. Acciones

### Resolver una solicitud — Aprobar

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor (usuario con `CH-App-Supervisor` compartida y miembro del grupo AAD `Security.ApproverGroup.Supervisor`) |
| **Cuándo** | Solicitud en la bandeja de **Pendientes**, seleccionada, con `cntDetStaleApr` apagado (el detalle cargado corresponde a la solicitud elegida) |
| **Por qué / para qué** | Aplica el efecto real de la solicitud: alta/baja/remapeo de tula, confirmación de sesión de recuento, o cierre del recuento con generación de asientos. Es el paso que hace que el trabajo de Recepción/Recuento se vuelva definitivo |
| **Cómo** | `scrAprobaciones` → seleccionar fila de `galBandejaApr` → revisar el detalle (incluida la caja de diferencias por componente si aplica) → botón **"✓ Aprobar y generar asientos"** → diálogo de confirmación → **"Sí, aprobar y contabilizar"** |
| **Qué valida el servidor** | `GuardReceptionPlugin` en el Update de `CH-Tbl-ApprovalRequest`: congelamiento, `createdby ≠ InitiatingUserId`, pertenencia al grupo aprobador. La app además revalida con un `LookUp` fresco justo antes de patchear, pero **la defensa real es el guard**, no ese `LookUp` |
| **Qué pasa si falla / deshacer** | Si el guard rechaza el Update, `Notify` con el mensaje del guard, nada cambia. Si la solicitud se marca Aprobada pero los efectos fallan a mitad de camino (sin Custom API atómico — §7), la app lo declara: *"La solicitud quedó APROBADA pero los efectos fallaron… revisá la tula antes de reintentar."* **"Tula con diferencia" y "Tula recontada sin declaracion" NO tienen deshacer**: los asientos ya están posteados. Los demás tipos sí revierten al rechazar, pero aprobar nunca se deshace por sí solo |

### Resolver una solicitud — Rechazar

| Campo | Contenido |
|---|---|
| **Quién** | Igual que Aprobar |
| **Cuándo** | Igual que Aprobar |
| **Por qué / para qué** | Revierte la tula/sesión al estado previo donde aplica, y dejar constancia del motivo en `StatusReason` |
| **Cómo** | `scrAprobaciones` → seleccionar fila → **"✕ Rechazar"** → diálogo con **motivo obligatorio** (`inpMotivoApr`) → **"Sí, rechazar"** (deshabilitado hasta que haya texto) |
| **Qué valida el servidor** | Mismo guard que Aprobar. El motivo obligatorio es **solo de la app** — el guard no exige `StatusReason` |
| **Qué pasa si falla / deshacer** | Ninguna reversión de Rechazar tiene "deshacer" — es en sí misma la reversión. Casos especiales: "Tula sin declaracion" **no revierte nada** (el productor nunca patcheó la tula, es no-op deliberado); "Editar tula" y "Primera tula en planilla" restituyen a *Almacenada en boveda* con aviso de advertencia si el contrato no trae `BagPreviousState` (los dos productores dejan la tula pre-patcheada antes de pedir la aprobación — fix documentado de las tulas que quedaban atrapadas en la app anterior) |

### Aprobar "Tula con diferencia" — el caso que postea dinero

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor |
| **Cuándo** | Solicitud `Tula con diferencia`, con la caja de diferencias (`cntDifApr`) revisada por componente × moneda |
| **Por qué / para qué** | Cierra el recuento: postea el asiento **Depósito** de cada depósito recontado, y **Sobrante**/**Faltante** por separado donde corresponda — **sin compensar** un componente contra otro. Deja la tula en `Recontada`, los cheques asociados en `Recontado`, y el detalle de sesión en *"Cerrado con diferencias"* |
| **Cómo** | Igual que "Aprobar" genérico — la caja de diferencias se calcula **por depósito × componente**, nunca por el neto: dos depósitos opuestos de +200.000/−200.000 en el mismo componente muestran `+200,000.00 / −200,000.00`, no "Cuadra" |
| **Qué valida el servidor** | Si `GuardSupervisorAccounting` (B-5) está registrado (§8): recalcula el monto esperado por componente contra el recuento y el declarado, leídos como sistema, y bloquea cualquier monto que no coincida o cualquier duplicado. **Si B-5 no está registrado** (verificar en vivo), la única defensa es el orden de ejecución client-side de §7 — nada impide que un Web API directo fabrique un asiento arbitrario |
| **Qué pasa si falla / deshacer** | **No tiene deshacer.** Una vez aprobada, las transacciones ya quedaron posteadas — es la advertencia explícita en el diálogo de confirmación y en el manual de usuario |

### Consultar localización de tulas

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor |
| **Cuándo** | Cualquier momento — pantalla de solo lectura |
| **Por qué / para qué** | Saber dónde está una tula y en qué estado, incluidos los dos estados que la app anterior no mostraba nunca (*Asignada a cajero*, *Pendiente de revisión — recuento*) |
| **Cómo** | `scrTulas` → filtrar por fecha/búsqueda/creador → segmentado de 6 estados + "Todas" |
| **Qué valida el servidor** | No aplica — no hay escritura. La consulta es delegable con ventana temporal (30 días por defecto); con más de 2.000 filas (límite configurado) `lblTopeTula` avisa que los contadores son parciales |
| **Qué pasa si falla / deshacer** | No aplica (solo lectura) |

### Consultar resumen de operaciones

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor |
| **Cuándo** | Cualquier momento — pantalla de solo lectura |
| **Por qué / para qué** | Ver el resumen de recuentos por cajero y por sesión del día elegido |
| **Cómo** | `scrResumen` → selector de fecha (abre en Hoy) |
| **Qué valida el servidor** | No aplica — no hay escritura |
| **Qué pasa si falla / deshacer** | No aplica (solo lectura) |

### Enviar cheques a digitación

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor |
| **Cuándo** | Cheques en estado **Recontado** — es el único estado seleccionable en la grilla |
| **Por qué / para qué** | Es la entrega física del supervisor al digitador; habilita que el digitador los procese en `CH-App-Digitization` (proceso 06) |
| **Cómo** | `scrCheques` → multiseleccionar (icono `icoCheckChq`, o clic en la fila) solo cheques con la píldora "Recontado" → **"→ Enviar N a digitación"** |
| **Qué valida el servidor** | `CheckGuard` (proceso 06): solo `Recontado → Enviado a digitacion` es legal. La app filtra visualmente por las dudas, pero el guard es la defensa real — si otro supervisor envió el mismo cheque entre la carga y el clic, el `Patch` individual de ese cheque falla y se cuenta como rechazado, no se finge éxito |
| **Qué pasa si falla / deshacer** | Reporte real "N enviado(s) · M rechazado(s) por el servidor: `<mensaje>`" (cada `Patch` va envuelto en su propio `IfError`, no hay `ForAll` ciego). No hay "deshacer" del envío: para revertir hace falta que el digitador marque el cheque **No Recibido**, lo que vuelve a habilitar el **Reenviar** |

### Reenviar cheques no recibidos

| Campo | Contenido |
|---|---|
| **Quién** | Supervisor |
| **Cuándo** | Existen cheques en estado **No Recibido** dentro de la ventana cargada (el día elegido + arrastre de 30 días) |
| **Por qué / para qué** | Reintentar la entrega de cheques que el digitador marcó como no recibidos |
| **Cómo** | `scrCheques` → **"↩ Reenviar N no recibidos"** → diálogo modal de confirmación (nombra el alcance exacto: "los N cheques No recibido de la ventana cargada… estén o no visibles con el filtro actual") → **"Sí, reenviar"** |
| **Qué valida el servidor** | Mismo `CheckGuard`: solo `No Recibido → Enviado a digitacion` es legal |
| **Qué pasa si falla / deshacer** | Mismo patrón de reporte real que Enviar. **El alcance NO es la selección** — es todos los `No Recibido` de la ventana cargada, aunque el filtro de Estado los tenga ocultos en ese momento; por eso existe el diálogo de confirmación con el conteo explícito antes de escribir |

## 10. Supuestos y limitaciones

- **El contador `pilBandejaCountApr` de la bandeja puede mentir hacia abajo**
  con más de una página de pendientes (usa `AllItems`, que solo cuenta filas ya
  paginadas). El número que manda para "cuánto falta firmar" es el del
  segmento (`CountRows(Filter(...))`, delegado).

## 11. Pruebas

| ID | Qué cubre |
|---|---|
| `SU-01` 🔴 | Bandeja completa — los 10 tipos de solicitud, aprobar aplica el efecto real, rechazar revierte donde aplica |
| `SU-02` 🔴 | Resolver una solicitud propia, y resolver siendo ajeno al grupo AAD aprobador; el guard rebota server-side, no solo por botones ocultos |
| `SU-03` 🔴 | Tula con diferencia — el caso sagrado: caja por componente × moneda, Sobrante/Faltante sin compensar, sin deshacer |
| `SU-04` 🔴 | Doble aprobación concurrente — la segunda revalida y no duplica el asiento |
| `SU-05` | Resumen de operaciones — KPIs consistentes con lo hecho en Recuento |
| `SU-06` 🔴 | Localización de tulas — los 6 estados visibles, fecha-hasta inclusiva |
| `SU-07` 🔴 | Envío de cheques — solo Recontado seleccionable, reporte real, habilita Digitación |
| `SU-08` | Reenvío — confirmación con conteo antes de escribir |
