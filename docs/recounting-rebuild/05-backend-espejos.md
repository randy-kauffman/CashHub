# 05 — Backend de CH-App-Recounting v2: los CUATRO espejos (E-1..E-4) **y el GUARD**

Implementación de `01-app.md` §2.2 y `00-analisis-y-plan.md` §2.2 en **CashHubCore**.
Todo está en el repo **sin build de solución, sin import y sin commit**: este documento
fija qué se creó, qué ya corrió contra DEV y qué queda para la fase de deploy.

> **Dos mitades, un mismo assembly.** Los **espejos** (§1–§5) hacen que las consultas de
> la v2 **deleguen**; el **guard** (§6) hace que la v2 **no pueda mentir**. Los espejos son
> performance y verdad de lectura; el guard es seguridad y verdad de escritura. Sin el
> guard, la app v2 canvas sigue siendo la que decide aprobaciones, totales y asientos
> contables desde el dispositivo del cajero — que es exactamente el problema.

> **La app NUNCA escribe estas columnas: solo las lee.** Las cuatro llevan la
> descripción `Mantenido por plugin. No editar a mano.` — mismo contrato que los espejos
> de Reception y Supervisor.

---

## 1. Mapping columna → plugin → paso

| ID | Columna (display) | Tabla | Tipo | Origen | Plugin | Pasos |
|---|---|---|---|---|---|---|
| **E-1** | `RecountingSessionMirror`<br>`sanic_recountingsessionmirror` | `sanic_chtbldepositsrecounting` | Lookup → `sanic_chtblrecountingsession` | `BagID.RecountingSession` | `DepositSessionMirrorPlugin` | Pre-op 20 **Create** de `sanic_chtbldepositsrecounting`<br>Pre-op 20 **Update** de `sanic_chtbldepositsrecounting` filtrado `sanic_bagid`<br>Post-op 40 **Update** de `sanic_chtblbags` filtrado `sanic_recountingsession` + **PreImage** |
| **E-2** | `Bag`<br>`sanic_bag` | `sanic_chtblchecks` | Lookup → `sanic_chtblbags` | `RelatedReCounterDepositID.BagID` | `CheckMirrorStampPlugin` (**ya existía**) | Pre-op 20 **Create** de `sanic_chtblchecks`<br>Pre-op 20 **Update** filtrado `sanic_relatedrecounterdepositid,sanic_relateddeclaredcheck`<br>_(+ Post-op 40 **Create** `(post-op B3)` para el espejo B3 del declarado — ver §7.11)_ |
| **E-3** | `DeclaredBagMirror`<br>`sanic_declaredbagmirror` | `sanic_chtbldeclaredchecks` | Lookup → `sanic_chtbldeclaredbags` | `DeclaredDepositID.DeclaredBag` | `DeclaredCheckBagMirrorPlugin` | Pre-op 20 **Create** de `sanic_chtbldeclaredchecks`<br>Pre-op 20 **Update** filtrado `sanic_declareddepositid` |
| **E-4a** | `RecountingSessionDetailMirror`<br>`sanic_recountingsessiondetailmirror` | `sanic_chtblxmlreadingdetail` | Lookup → `sanic_chtblrecountingsessiondetail` | `XMLReading.RecountingSessionDetailId` | `XmlReadingDetailMirrorPlugin` | Pre-op 20 **Create** de `sanic_chtblxmlreadingdetail`<br>Pre-op 20 **Update** filtrado `sanic_xmlreading`<br>Post-op 40 **Update** de `sanic_chtblxmlreading` filtrado `sanic_recountingsessiondetailid,sanic_isactive` + **PreImage** |
| **E-4b** | `XMLReadingActiva`<br>`sanic_xmlreadingactiva` | `sanic_chtblxmlreadingdetail` | Bit (default `No`) | `XMLReading.IsActive` | `XmlReadingDetailMirrorPlugin` | los MISMOS 3 pasos que E-4a (las dos columnas se escriben **siempre juntas**) |

### Por qué cada cascada existe (o no)

- **E-1 — cascada SÍ.** `03` §6.3 patchea `Bags.RecountingSession` al **abrir** la tula,
  antes de que `scrConteo` cree ningún depósito: el camino feliz lo cubre el Create. Pero
  **re-abrir** una tula en otra sesión (`btnReabrirTul`) vuelve a patchear la misma
  columna con los depósitos YA creados. Sin cascada quedan colgando de la sesión vieja y
  el rail de KPIs suma plata de otra sesión. Con la cascada, el conjunto *"depósitos con
  `RecountingSessionMirror` = S"* es **exactamente** el que devuelve el fallback sin
  backend de `01` §2.2 (depósitos de las tulas que hoy están en S).
- **E-3 — cascada NO, deliberado.** `DeclarationMaterializer` materializa
  `Declaration → DeclaredBags → DeclaredDeposits → DeclaredChecks` **en ese orden** y
  escribe `sanic_declaredbag` en el **Create** del depósito declarado: cuando nace el
  cheque declarado, su depósito ya trae la bolsa. Nadie re-parenta después (la
  declaración es inmutable; re-procesar el correo materializa una declaración nueva). La
  cobertura, si algún día pasa, es el backfill `--only e3`. Está documentado como
  **LIMITACIÓN CONOCIDA** en el header del plugin, igual que en `ClientBranchNameMirrorPlugin`.
- **E-4 — cascada OBLIGATORIA.** Es el caso crítico del contrato: cuando llega una
  lectura nueva, la vieja se **apaga** (`IsActive → false`) *después* de que sus detalles
  ya existen. Sin propagar, **Q17** sigue sumando la lectura muerta y la columna `Auto`
  del cajero miente. Las dos columnas se propagan juntas: el valor efectivo de la que no
  vino en el Target sale de la **PreImage**.

---

## 2. Archivos

### Nuevos

| Ruta | Qué es |
|---|---|
| `src/backend/CashHub.Validation/Security/RecountingMirrors.cs` | **Dominio puro** (sin SDK): `ShouldStamp`, `ResolveLookupCascade`, `ResolveReadingCascade` |
| `src/backend/CashHub.Validation.Tests/RecountingMirrorsTests.cs` | 18 tests xUnit del dominio |
| `src/backend/CashHub.Plugins/DepositSessionMirrorPlugin.cs` | E-1 (estampa + cascada desde la tula) |
| `src/backend/CashHub.Plugins/DeclaredCheckBagMirrorPlugin.cs` | E-3 (estampa) |
| `src/backend/CashHub.Plugins/XmlReadingDetailMirrorPlugin.cs` | E-4 (estampa de las 2 columnas + cascada desde la lectura) |
| `scripts/create-recounting-mirror-columns.sh` | Crea E-1, E-3, E-4a, E-4b por Web API (idempotente) |
| `scripts/register-recounting-mirrors.sh` | Sube el assembly y registra los 8 pasos (idempotente) |
| `scripts/backfill-recounting-mirrors.py` | Backfill idempotente de E-1/E-2/E-3/E-4 con `--dry-run` y `--only` |

### Modificados

| Ruta | Qué cambió |
|---|---|
| `src/backend/CashHub.Plugins/Schema.cs` | `RecountingSessionDetail`, `XmlReading`, `XmlReadingDetail` (nuevos) + `DepositRecounting.BagLookup/SessionMirror`, `DeclaredCheck.DeclaredDepositLookup/DeclaredBagMirror`, `DeclaredDeposit.DeclaredBagLookup` |

**Nada de Reception ni de otras apps fue tocado.**

---

## 3. Lo que YA corrió contra DEV

`./scripts/create-recounting-mirror-columns.sh` → las 4 columnas creadas y publicadas en
`CashHubCore`. Verificado con `GET` de metadata:

| Columna | `SchemaName` | `AttributeType` | `IsValidForCreate` |
|---|---|---|---|
| `sanic_chtbldepositsrecounting.sanic_recountingsessionmirror` | `sanic_RecountingSessionMirror` | `Lookup` | `True` |
| `sanic_chtbldeclaredchecks.sanic_declaredbagmirror` | `sanic_DeclaredBagMirror` | `Lookup` | `True` |
| `sanic_chtblxmlreadingdetail.sanic_recountingsessiondetailmirror` | `sanic_RecountingSessionDetailMirror` | `Lookup` | `True` |
| `sanic_chtblxmlreadingdetail.sanic_xmlreadingactiva` | `sanic_XMLReadingActiva` | `Boolean` | `True` |

`python3 scripts/backfill-recounting-mirrors.py --dry-run` (nada escrito):

```
E-1  depósitos leídos: 5262 · ya al día: 11   · a estampar: 5251 · sin tula: 0
E-2  cheques   leídos: 2104 · ya al día: 2104 · a estampar: 0    · sin depósito: 0
E-3  declarados leídos: 1261 · ya al día: 0   · a estampar: 1261 · sin depósito declarado: 0
E-4  detalles  leídos: 0    · ya al día: 0    · a estampar: 0    · sin lectura: 0
```

> 🔎 **E-2 está 100 % al día en DEV** (2104/2104): la columna, el plugin y sus dos pasos
> ya existen y están **activos** (`statecode 0`). Verificado por API:
> `CashHub.CheckMirrorStamp: Create de sanic_chtblchecks` (stage 20) y
> `... Update de sanic_chtblchecks` (stage 20, filtrado
> `sanic_relatedrecounterdepositid,sanic_relateddeclaredcheck`). **No hay nada que crear
> ni registrar para E-2.** _(Después de esa foto se agregó un tercer paso,_
> `... Create de sanic_chtblchecks (post-op B3)` _en stage 40, que NO toca E-2: estampa el
> espejo B3 del cheque declarado — §7.11.)_

> 🔎 **E-4 no tiene datos en DEV**: `sanic_chtblxmlreadings`, `sanic_chtblxmlreadingdetails`
> y `sanic_chtblrecountingsessiondetails` tienen **0 filas**. La consulta del backfill
> quedó validada contra la metadata real (devuelve 200), pero el backfill de E-4 es un
> **no-op estructural** hasta que haya lecturas de contadora. Ojo al probar `Leer
> contadora`: primero hay que sembrar lecturas.

---

## 4. Pendiente para la fase de DEPLOY (en este orden)

1. **Build del assembly**
   `dotnet build -c Release src/backend/CashHub.Plugins/CashHub.Plugins.csproj`
   → `src/backend/CashHub.Plugins/bin/Release/net462/CashHub.Plugins.dll`
   *(ya compila limpio: 0 errores, 0 warnings.)*
2. **Registro de los 8 pasos**
   `./scripts/register-recounting-mirrors.sh`
   Sube el assembly (upsert por `name eq 'CashHub.Plugins'`, mismo DLL de todo el suite)
   y crea/actualiza los 3 `plugintypes` + 8 `sdkmessageprocessingsteps` + 2 PreImages.
   Aborta si falta alguna columna espejo.
   > ⚠️ **Re-sube el DLL entero**: arrastra al ambiente cualquier otro cambio pendiente
   > del assembly. Coordinar con el resto del backend antes de correrlo.
3. **Backfill del histórico**
   `python3 scripts/backfill-recounting-mirrors.py --dry-run` (releer el diff) y después
   `python3 scripts/backfill-recounting-mirrors.py`.
   Re-ejecutable e idempotente: solo PATCHea lo que **difiere**.
4. **Pack + import de la solución** (si se versiona por solución en vez de por API):
   bump de `solutions/unpacked/core/Other/Solution.xml` → `pac solution pack` →
   `pac solution import --publish-changes`, mismo procedimiento que los espejos previos.
5. **Verificación funcional en la app** (checklist de `01` §9):
   - `Filter('CH-Tbl-DepositsRecountings', RecountingSessionMirror.'CH-Tbl-RecountingSession' = …)`
     con IntelliSense y sin aviso de delegación.
   - `Filter('CH-Tbl-DeclaredChecks', DeclaredBagMirror.'CH-Tbl-DeclaredBags' = …)`.
   - Q17 con las **dos** condiciones (`RecountingSessionDetailMirror` **y**
     `XMLReadingActiva = true`).
   > Si Studio no autocompleta un espejo recién creado: **quitar la tabla del panel Datos
   > y volver a agregarla** (el datasource generado por script no siempre trae la metadata
   > de relación del lookup — remedio ya documentado en `01` §2.2).

### Verificación post-registro sugerida

- **E-1 estampa**: crear un depósito en una tula con sesión → `RecountingSessionMirror`
  poblado sin tocar nada más.
- **E-1 cascada**: re-abrir una tula ya recontada en otra sesión → todos sus depósitos
  cambian de sesión espejo en la misma operación.
- **E-4 cascada**: apagar `IsActive` de una lectura → sus detalles pasan a
  `XMLReadingActiva = No` y desaparecen de Q17.
- **Idempotencia**: re-guardar tula/lectura **sin cambiar nada** → el trace dice
  `sin cambio real ... no se toca ningún <hijo>` y **cero** escrituras.

---

## 5. Desvíos respecto de `01` §2.2 (y su motivo)

| # | Desvío | Motivo |
|---|---|---|
| **1** | **Los tests son de dominio puro (xUnit), NO FakeXrmEasy.** | El repo **no tiene** FakeXrmEasy y no puede tenerlo tal como está: `CashHub.Validation.Tests` es `net8.0` y `CashHub.Plugins` es `net462` — un proyecto net8 **no puede referenciar** un assembly net462. El patrón de la casa (probado en `ClientBranchNameMirrors`, `CheckMirrors`, `BagReceptionMirrors`) es exactamente éste: toda la DECISIÓN vive en `CashHub.Validation/Security` y se testea sin SDK; el plugin queda como cableado fino sobre Dataverse. Se respetó el patrón de la casa por sobre la letra del pedido. Cambiarlo implicaría multi-targetear `CashHub.Plugins` — refactor del suite entero, fuera de alcance. |
| **2** | **E-3 sin cascada** desde `DeclaredDeposits.DeclaredBag`. | Evidencia en `DeclarationMaterializer`: la bolsa se escribe en el **Create** del depósito declarado y nadie re-parenta después. Un paso Post-op extra sobre una tabla de alto volumen a cambio de un evento que no ocurre no se paga. Documentado como limitación conocida + cubierto por el backfill (mismo criterio que `ClientBranchNameMirrorPlugin` con el rename de cliente). |
| **3** | **La cascada de E-4 usa `RetrieveMultiple` paginado + `Update` por fila**, no `ExecuteMultiple`/`UpdateMultiple`. | Los detalles de UNA lectura son las **denominaciones contadas** (decenas), no miles. `ExecuteMultiple` además cambia el manejo de errores parciales dentro de la transacción sincrónica. Se mantuvo el patrón de paginado de a 500 de `CheckMirrorStampPlugin`/`BagReceptionMirrorPlugin` — una sola forma de cascada en todo el backend. |
| **4** | **E-2 no se tocó en absoluto.** | Ya existe **y funciona**: columna + plugin + 2 pasos activos + 2104/2104 filas al día en DEV. La fase `e2` del backfill nuevo queda como red de seguridad (a diferencia de la de `backfill-tracker.py`, que solo estampaba cuando el espejo estaba **vacío**, no cuando **difiere**). |
| **5** | La cascada de E-1 **convive** con `CashHub.CheckMirrorStamp: Update de sanic_chtblbags` (misma entidad, mismo stage 40, mismo filtro `sanic_recountingsession`). | Son `plugintypes` distintos que escriben **tablas disjuntas** (depósitos vs. cheques) y tienen nombres de step distintos: no se pisan. Rank **30** (vs. 20 del de cheques) para que el orden sea explícito y no un empate resuelto por el motor. Regla de la casa respetada: *el plugin que ESCRIBE una columna espejo es el único que la mantiene*. |

---

## 6. El GUARD server-side (`00` §2.2, puntos 1–7)

La app canvas v2 **ya no escribe** aprobaciones, ni totales, ni transacciones. Esta sección
es la contraparte: lo que la app dejó de hacer, lo hace el servidor —y lo que la app
**podría** hacer por Web API, el servidor lo rechaza.

### 6.1 Mapping plugin → paso → qué mata

| # (`00` §2.2) | Plugin | Paso registrado | Qué mata |
|---|---|---|---|
| **1** | `RecountingSessionAutoApprovePlugin` | Pre-op **20** `Create` de `sanic_chtblapprovalrequest` | **S-1** — la v1 escribía `ApprovalStatus: Aprobado` **desde el canvas** cuando `varIsOwnDesk`. Con `gvarUser` en blanco, el `LookUp` de mesas devolvía la **primera mesa sin cajero** y cualquiera entraba auto-aprobado. Ahora la mesa sale de la sesión referenciada (leída **como sistema**) y el usuario de `context.InitiatingUserId` |
| **1** | `RecountingSessionAutoApprovePlugin` | Post-op **40** `Create` de `sanic_chtblapprovalrequest` | **S-1** — `Confirmed`/`ConfirmedBy` de la sesión. Va **después** del Create: si el Create se cae, la sesión no queda confirmada colgando de una solicitud que no existe |
| **2** | `BagRecountClosurePlugin` | Pre-op **20** `Update` de `sanic_chtblbags`, filtrado `sanic_bagstatus` + **PreImage** | **S-2a** — `TotalNIO/USD/EUR` salían de `Sum(Filter(colRecountedDeposits, …))`, una **colección en memoria del cliente** que Power Fx trunca en 500/2000 **sin error**. Ahora se recalculan con una query server-side por `sanic_bagid` y se **PISA** lo que mande el cliente |
| **3** | `BagRecountClosurePlugin` | Post-op **40** `Update` de `sanic_chtblbags`, mismo filtro + **PreImage** | **S-2b** — los asientos de `CH-Tbl-Transactions` los armaba un `ForAll` sobre esa misma colección. Ahora los crea el plugin, **idempotentemente** |
| **4** | `GuardRecountingPlugin` | Pre-val **10** `Create`/`Update`/`Delete` de `sanic_chtbldepositsrecounting`, `sanic_chtblchecks`, `sanic_chtblcountedmoneydetails` | **S-5** — la v1 condicionaba botones en Power Fx (cosmético). Por Web API cualquiera con el rol cargaba depósitos sobre una tula en bóveda, o agregaba cheques a una tula ya `Recontada` — o sea, **después** de que sus asientos se generaron |
| **5** | `GuardRecountingPlugin` | Pre-val **10** `Delete` de `sanic_chtblapprovalrequest` y `sanic_chtblrecountingsession` | **D02-4 / B02-3** — habilita *Cancelar solicitud* / *Elegir otra mesa* de `02` §9 **sin** abrir el borrado a todo lo demás. Sin esto la mesa queda ocupada todo el día y la solicitud sigue en la cola del supervisor |
| **6** | *(ninguno)* | — | **Ya cubierto.** `ReceptionGuard.EvaluateBagStatusTransition` ya tiene `Almacenada en bóveda → Asignada a cajero` (y `Recibida en esclusa → Asignada a cajero`) en su matriz legal. **No se tocó `ReceptionGuard`**; se agregaron **tests de regresión** que fijan esas transiciones (el stress de Reception demostró que el guard está activo en DEV) |
| **7** | *(no es plugin)* | `scripts/backfill-recounting-mirrors.py` — paso final, **dry-run por defecto** | Solicitudes legadas `"Primera tula en planilla"` **pendientes**: al aprobarlas, el canvas del Supervisor re-patchea la bolsa a `Asignada a cajero` con la **sesión y la declaración viejas**, sobre una tula que la v2 quizá ya recontó |

### 6.2 Semántica replicada de la v1 (verificada línea por línea)

- **Auto-aprobación** — `Screen1.fx.yaml`, `Form1.OnSuccess`. La v1 hacía
  `Patch(sesión, {Confirmed: true})` **y** `Patch(solicitud, {…, ApprovalStatus: Aprobado})`.
  La v2 estampa además `SolvedBy` y `ResolutionDatetime` (la v1 los dejaba vacíos: una
  solicitud aprobada **sin firmante**).
- **Totales** — `dialog_ConfirmarTerminarTula.OnButtonSelect`. Misma suma:
  `CashAmount + BACLocalCheckAmount + OtherLocaCheckAmount` agrupada por moneda. Se
  recalculan en **las dos** ramas de cierre (limpio → `Recontada`; con discrepancias →
  `Pendiente de revisión … - recuento`), igual que la v1.
- **Transacciones** — mismo `ForAll`: `AccountNumber`, `Currency`, `CashAmount`,
  `BACLocalCheckAmount`, `OtherLocalCheckAmount` (⚠️ **con `l`** en `Transactions`, **sin
  `l`** en `DepositsRecountings`), `TransactionType = Deposito` (159460000),
  `RelatedDepositRecounting`, y `TransactionReference`/`TransactionNumber` copiados del
  `RelatedDeclaredDeposit` (`sanic_depositreference` / `sanic_transactionno`) cuando hay
  enlace. `TransactionProcessingType` = **Manual** (159460000) si hay cheques, **Batch**
  (159460001) si no. Solo en el cierre **limpio**: la tula que va a revisión no genera
  contabilidad, igual que la v1.
- 🧨 **Idempotencia (nueva, no estaba en la v1).** La tula puede volver a `Recontada` tras
  un rechazo y reconteo. El plugin consulta primero qué depósitos de la tula **ya tienen
  asiento de tipo `Deposito`** (`Transaction → RelatedDepositRecounting → Deposit → Bag`,
  **una** query con link-entity) y solo crea los que faltan. La clave es
  **(recuento, tipo)**, no (recuento): con la clave sola por depósito, un recuento que ya
  tiene un `Sobrante` o un `Faltante` del circuito del supervisor se leería como *"ya
  asentado"* y su asiento de `Deposito` **no se crearía jamás**.

### 6.2.b 🧨 Convivencia con `GuardSupervisorAccountingPlugin` (hallazgo)

Los `Create` que emite `BagRecountClosurePlugin` **pasan por**
`GuardSupervisorAccountingPlugin` (Pre-validation del `Create` de `sanic_chtbltransactions`,
rank 2). `AccountingGuard.IsAccountingType` incluye a **`Deposito`**, así que el asiento
recorre el circuito contable completo, y `AccountingGuard.EvaluateAccountingCreate` exige:

1. **moneda idéntica** a la del depósito recontado;
2. **montos idénticos por componente** (`expected = deposit.Amounts` para `Deposito`);
3. **ningún duplicado** de `(recuento, tipo)`.

El plugin copia los tres del depósito, así que cumple por construcción — y hay un test
(`BuildTransactions_ProduceExactamenteLoQueAccountingGuardEXIGE`) que **fija ese contrato**:
si alguien cambia el plan del asiento, revienta el test y no el cierre de tula en
producción.

> La consulta de idempotencia del plugin **no reemplaza** al chequeo `duplicateExists` de
> `AccountingGuard`: lo **anticipa**. Sin ella, el `Create` duplicado *lanza* y voltea la
> transición entera de la tula, en vez de saltear el asiento que ya existe.

### 6.3 Decisiones tomadas en los puntos ambiguos

| Tema | Decisión | Motivo |
|---|---|---|
| **4 (a)** — pipeline de plugins | **`Depth > 1` exime.** | Los espejos E-1/E-2/E-4 y sus cascadas escriben depósitos y cheques con la tula ya cerrada. El actor humano y las apps canvas **siempre** entran con `Depth 1` |
| **4 (b·1)** — backfills de espejos | **Exento el `Update` que toca ÚNICAMENTE columnas espejo derivadas** (`sanic_recountingsessionmirror` en el depósito; `sanic_bag` y `sanic_recountingsession` en el cheque). | 🧨 **Hallazgo:** `backfill-recounting-mirrors.py` (fases `e1`/`e2`) y `backfill-tracker.py` estampan esas columnas **por Web API**, o sea con `Depth 1` y sobre el **histórico entero** — tulas en bóveda, recontadas, de todo. Sin esta excepción el gate **voltea los dos backfills completos** y el rebuild se queda sin espejos. Es metadata derivada, no negocio: mismo bypass y mismo nombre (`touchesOnlyMirror`) que ya tienen `CheckGuard` y `ReceptionGuard` |
| **4 (b·2)** — usuario SP / seeder | **NO hay excepción por identidad.** | Ningún guard del repo la tiene. El mecanismo de la casa es **el orden**: `seed-reception-stress.py` documenta que *"el seed corre con el guard 100 % activo; el orden lo hace legal"*, y para el `--clean` —que borra hacia atrás y no tiene orden legal posible— **desactiva temporalmente los 3 pasos `Delete` concretos**. Un seeder de reconteo hará lo mismo con los `Delete` de `GuardRecounting` (los tres nombres están en el header de `register-recounting-guard.sh`). Una excepción por identidad sería una **llave maestra permanente** para resolver un problema de scripts que ya tiene solución acotada y auditable |
| **4 (c)** — el marcado de cheques a `Recontado` | **Exento el `Update` de cheque que toca ÚNICAMENTE `sanic_checkstatus`.** | El orden real de `04` §11.3 es **bolsa primero, cheques después**, y es deliberado: *"si el `Patch` de la bolsa falla, los cheques NO se marcan"*. Reordenarlo marcaría cheques de una tula que nunca cerró. Con la bolsa ya en `Recontada`, el gate voltearía el cierre limpio. **La excepción no afloja nada**: la transición de estado del cheque la sigue gobernando `CheckGuard` (vía `GuardTransactionPlugin`), que ve ese mismo `Update`. Es el patrón `touchesOnly…` que ya usan `ReceptionGuard` (totales del lote, espejo `sanic_bagnumber`) y `CheckGuard` (espejos B6/B-9) |
| **4** — `CH-Tbl-Transactions` | **Fuera del gate.** | Los asientos los crea el propio guard con la tula **ya** en `Recontada`; y la tabla tiene su `GuardTransactionPlugin` |
| **4** — re-colgado | **Un `Update` que mueve el registro a otra tula exige que las DOS estén `Asignada a cajero`.** | Sin eso, un `Update` legítimo re-cuelga un depósito de una tula en bóveda y el dinero cambia de dueño |
| **5** — “propias” en `ApprovalRequests` | **`createdby == InitiatingUserId`.** | Es el único dato de autoría de la tabla (no hay lookup de cajero) y es **exactamente** el campo con el que `ReceptionGuard.EvaluateApprovalUpdate` ya impone la separación creador/resolutor |
| **5** — “propias” en `RecountingSessions` | **`sanic_cashier == InitiatingUserId`**, no `createdby`. | `00` §2.2 punto 5 lo nombra literalmente (*"`Cashier` = usuario"*), `01` §2.4 lo declara requerido y escrito explícitamente al crear la sesión, y es la **misma columna** que `GuardReceptionPlugin` ya lee para verificar la mesa propia. `createdby` puede ser un SP o una impersonación y no dice de quién es la mesa. Sin cajero → **bloquea** (fail-closed) |
| **5** — sesión con solicitud resuelta | **Bloquea.** La búsqueda es `sanic_requestresources LIKE '%<guid>%'` con `ApprovalStatus` no nulo. | El vínculo sesión↔solicitud **no es un lookup**: viaja como texto dentro del JSON (`01` §5.7). La query solo corre cuando la baja ya pasó pertenencia y `Confirmed`. Si la consulta falla, **fail-closed**: se asume que hay decisión colgando |
| **6** — transición de apertura | **No se tocó `ReceptionGuard`.** | Las dos transiciones **ya estaban** en la matriz legal. Se agregaron tests de regresión que las fijan, más uno que fija que `Recontada` sigue siendo **terminal** (si dejara de serlo, la idempotencia de los asientos pasaría de red de seguridad a único freno contra la plata duplicada) |
| **7** — GC | **Dentro de `backfill-recounting-mirrors.py`**, no en un script propio. | Es el mismo dominio, la misma auth y el mismo `$batch`; un script hermano sería 250 líneas de andamiaje duplicado. Corre **siempre al final** en modo lista y solo escribe con `--gc-apply`; `--only-gc` lo corre solo. **Cierra = `Rechazado` + `StatusReason`**, jamás `Delete`: la solicitud es evidencia de que alguien pidió algo |
| **7** — criterio de “peligrosa” | `BagStatus` posterior a `Almacenada en bóveda` **o** la tula cuelga de **otra** sesión que la del JSON. | Es literalmente lo que dice `00` §2.2 punto 7. Una solicitud cuya tula **no se puede resolver** se reporta aparte y **no se cierra**: cerrar a ciegas es el error del otro lado |

> ⚠️ **El GC con `--gc-apply` escribe `ApprovalStatus`, así que pasa por
> `GuardReceptionPlugin`**: quien lo corra debe pertenecer al grupo AAD aprobador
> (`Security.ApproverGroup.Supervisor`) y **no** ser el creador de las solicitudes. El
> script lo avisa antes de mandar los `PATCH`.

### 6.4 Archivos del guard

**Nuevos**

| Ruta | Qué es |
|---|---|
| `src/backend/CashHub.Validation/Security/RecountingGuard.cs` | **Dominio puro** (sin SDK): `ShouldAutoApprove`, `ShouldRecomputeTotals`, `ComputeTotals`, `ShouldBuildTransactions`, `BuildTransactions`, `IsWriteExempt`, `IsWriteAllowed`, `EvaluateApprovalDelete`, `EvaluateSessionDelete` |
| `src/backend/CashHub.Validation.Tests/RecountingGuardTests.cs` | **73 tests** xUnit del dominio (suite total: **611**, era 538) |
| `src/backend/CashHub.Plugins/RecountingSessionAutoApprovePlugin.cs` | Punto 1 (Pre-op estampa la resolución · Post-op confirma la sesión) |
| `src/backend/CashHub.Plugins/BagRecountClosurePlugin.cs` | Puntos 2 y 3 (Pre-op totales · Post-op asientos idempotentes) |
| `src/backend/CashHub.Plugins/GuardRecountingPlugin.cs` | Puntos 4 y 5 (gate de estado + bajas acotadas) |
| `scripts/register-recounting-guard.sh` | Sube el assembly y registra los **15 pasos** + 8 PreImages (idempotente) |

**Modificados**

| Ruta | Qué cambió |
|---|---|
| `src/backend/CashHub.Plugins/Schema.cs` | `CountingDesk` y `CountedMoneyDetail` (nuevos) · `RecountingSession` completa (`Cashier`, `CountingDesk`, `Confirmed`, `ConfirmedBy`, `SessionForciblyClosed`, `PreImageName`) · `ApprovalRequest.SolvedBy/ResolutionDatetime` · `Transaction.TransactionReference` · `DeclaredDeposit.DepositReference` · `DepositRecounting.PreImageName` |
| `scripts/backfill-recounting-mirrors.py` | Paso final de **GC** de solicitudes legadas + flags `--gc-apply` y `--only-gc` |

**Nada de `ReceptionGuard`, `CheckGuard`, `TransactionGuard` ni de sus plugins fue tocado.**

### 6.5 Pendiente para la fase de DEPLOY

1. `dotnet build -c Release src/backend/CashHub.Plugins/CashHub.Plugins.csproj` *(compila: 0 errores)*.
2. `./scripts/register-recounting-guard.sh` — **re-sube el DLL entero**, igual que los demás
   scripts de registro: coordinar con el resto del backend.
3. `python3 scripts/backfill-recounting-mirrors.py --only-gc` para **leer** el GC, y recién
   después decidir el `--gc-apply`.
4. **Sanity checks post-registro** (con la app v2 ya desplegada):
   - Pedir la **mesa propia** → la solicitud nace `Aprobado` con `SolvedBy` = el cajero y la
     sesión queda `Confirmed`. Pedir una **mesa ajena** → nace pendiente.
   - Cerrar una tula **sin** discrepancias → `TotalNIO/USD/EUR` poblados **y** un
     `CH-Tbl-Transactions` por depósito. Volver a patchear `Recontada` sobre la misma tula →
     **cero** asientos nuevos (trace: *"N ya asentados"*).
   - `Patch` de un depósito sobre una tula en **bóveda** por Web API → rechazado con el
     mensaje del guard.
   - *Cancelar solicitud* con la solicitud **pendiente** → borra. Con la solicitud
     **rechazada** → `02` §9.2 saltea el borrado y da de baja **solo** la sesión.

---

## 7. Stress plan — `scripts/seed-recounting-stress.py`

El seeder de estrés del dominio de reconteo. Hermano de `seed-reception-stress.py`:
**misma arquitectura** (`$batch` con **un changeset por request**, marcadores rastreables,
fases `--only-*`, throughput reporting, `--clean` con apagado temporal y auditado de pasos,
auth por `pp_secrets.env`). Lo que cambia es el dominio — y una cosa más: acá el **guard es
parte de lo que se prueba**, no un obstáculo que esquivar.

> 🧨 **El seeder ABORTA si el guard no está desplegado.** No es una comodidad: los totales,
> los asientos y la auto-aprobación son la **salida** que el seed produce y `--verify` mide.
> Con los pasos apagados, el seed sembraría su propio JSON y no probaría nada.

### 7.1 Materia prima: el stress de Reception

**No se crean tulas.** Salen del universo que dejó `seed-reception-stress.py`
(`sanic_bagid` = `ST` + correlativo) y que está en **`Almacenada en bóveda`** (159460001) —
el único estado desde el que Recounting puede abrir una tula (`01` §5.1).

Medido en DEV el 2026-08-17 con `--dry-run`:

| Universo disponible | Filas |
|---|---|
| tulas `ST*` totales | **13.379** |
| … en `Almacenada en bóveda` (materia prima) | **6.616** |
| … en `Asignada a cajero` / `Recontada` / `Pendiente - recuento` | 0 / 0 / 0 |
| usuarios reales habilitados (`isdisabled eq false and applicationid eq null`) | **16** |
| denominaciones habilitadas (`CH-Tbl-MoneyDenominations`) | NIO **11** · USD **13** · EUR **15** |
| `overriddencreatedon` de la sesión, `IsValidForCreate` | **True** |

### 7.2 Marcador propio: `SRC-` en `sanic_name`, en las 13 tablas

Todas las tablas del dominio tienen `sanic_name` **AUTONUMBER** (`RCS-{SEQNUM:4}`,
`DR-{SEQNUM:8}`, `CMD-{SEQNUM:12}`, `CK-{SEQNUM:8}`, `DB-{SEQNUM:7}`, `DD-{SEQNUM:8}`,
`AR-{SEQNUM:8}`, `Decla-{SEQNUM:4}`…). Un autonumber de Dataverse **se puede pisar** mandando
el valor explícito en el `Create` — es exactamente lo que ya hace
`seed-supervisor-aprobaciones.py` con `sanic_name = 'AR-STRESS-####'`. Se reusa ese patrón:

`sanic_name = 'SRC-<TAG><correlativo>'` → `SRC-DECL` · `SRC-DB` · `SRC-DD` · `SRC-DC` ·
`SRC-MESA` · `SRC-S` · `SRC-SD` · `SRC-DR` · `SRC-CMD` · `SRC-CK` · `SRC-AR` · `SRC-XR` ·
`SRC-XD`.

Un único filtro **server-side y delegable** —`startswith(sanic_name,'SRC-')`— sirve para el
inventario y para el `--clean` de las 13 tablas. **No colisiona** con `SR*`/`ST*` de Reception
(sin guion) ni con `AR-STRESS-` del seed de Supervisor, y **no ensucia ninguna columna de
negocio**: `BagNumber`, `CheckNumber`, `Denomination`, `Currency` y los montos son **reales**,
que es lo que la app lee.

### 7.3 Números objetivo por fase — y qué límite ataca cada uno

| Fase | Qué siembra | Volumen | Límite de delegación que ataca |
|---|---|---|---|
| **F1** `--only-declarations` | `Declarations` | ~60 | contenedor (1 cada 50 bolsas) |
| | **`DeclaredBags`** | **3.000** | **>2000** — grilla `B1` de selección de declaración por `BagNumber` |
| | `DeclaredDeposits` (2–6 por bolsa) | ~12.000 | **>2000** — universo del matcher de depósitos |
| | `DeclaredChecks` | ~13.800 | **>2000** total |
| | **6 bolsas GORDAS** (60 dep × 10 cheq) | **600 cheques declarados POR BOLSA** | 🧨 **>500 en UNA bolsa** — es el caso que **E-3** existe para delegar: `Filter('CH-Tbl-DeclaredChecks', DeclaredBagMirror = …)`. Sin el espejo, el `Filter` navega el lookup y **trunca en 500 sin error** |
| **F2** `--only-sessions` | `CountingDesks` | 40 | catálogo de mesas de `02` |
| | `RecountingSessions` **Confirmed**, retro-fechadas hasta 90 días | **600** | **>500** — la consulta de sesión del cajero (`Cashier = gvarUser and SessionForciblyClosed = false` + rango de `Created On`) tiene que seguir delegando con el histórico encima |
| | `ApprovalRequests` de los **4 tipos** de `01` §5.6 | **800** (200 c/u) | **>500** — recuperación por PK y por `RequestType` del polling (`01` §5.8) |
| **F3** `--only-recount` | apertura `bóveda → Asignada a cajero` | **1.500** | universo vivo de `scrTulas` |
| | `RecountingSessionDetails` (1 por tula, **las 4 rutas**) | 1.500 | contrato de entrada de `04` |
| | `DepositsRecountings` (1–5 por tula) | ~4.500 | **>2000** — es lo que **E-1** existe para delegar (`RecountingSessionMirror`) |
| | `CountedMoneyDetails` (2–5 denominaciones reales por depósito) | ~15.750 | volumen de arqueo del rail de KPIs |
| | `Checks` (declarados enlazados por `MatchedCheck` + manuales) | ~3.100 | |
| | 🧨 **UNA tula GORDA** | **600 cheques en UN depósito** | **>500 en UNA tula** — la grilla de cheques de `04` y el `Sum` de `BACLocalCheckAmount`/`OtherLocaCheckAmount` |
| | cierres a **`Recontada`** | **1.000** | dispara `BagRecountClosurePlugin`: **~3.000 `CH-Tbl-Transactions` los crea el GUARD**, no el seeder |
| | cierres a **`Pendiente - recuento`** + `ApprovalRequest "Tula con diferencia"` | **200** | cola del Supervisor |
| | quedan **abiertas** en `Asignada a cajero` con depósitos a medias | **300** | prueba de la UI en vivo |
| **F4** `--only-xml` | `XMLReadings` (`IsActive` mixto, 1–3 por detalle, sólo la última activa) | **700** | **E-4b** |
| | **`XMLReadingDetails`** (30–60 por lectura) | **~31.500** | 🧨 **>20.000** — es el caso que **E-4** existe para delegar: **Q17** con las **dos** condiciones (`RecountingSessionDetailMirror` **y** `XMLReadingActiva = true`) |

**Total estimado: ~90.000 escrituras del seeder + ~3.000 asientos que crea el guard.**

### 7.4 Orden de fases (y por qué ese orden ES la legalidad)

```
F1 declaraciones ──► F2 mesas + sesiones ──► F3 reconteo ──► F4 lecturas
   (independiente)      (independiente)        │                │
                                               │                └─ necesita RecountingSessionDetails
                                               └─ necesita sesiones (F2) y, opcionalmente,
                                                  declaraciones (F1) para enlazar
```

Dentro de **F3** el orden es **obligatorio**, y es el único motivo por el que el seed corre
con el guard 100 % activo:

1. **`PATCH` de la tula** `Almacenada en bóveda → Asignada a cajero` (+ `RecountingSession`
   + `BagDeclarationID` en el **mismo** request, como hace la app en `01` §2.4).
   `ReceptionGuard.EvaluateBagStatusTransition` ya tiene esa transición en su matriz legal.
2. **recién ahí** `RecountingSessionDetail`, `DepositsRecountings`, `CountedMoneyDetails` y
   `Checks`: `GuardRecountingPlugin` rechaza **cualquier** hijo cuya tula ancestro no esté
   `Asignada a cajero`.
3. cheques **sin `CheckStatus`** (`CheckGuard` sólo admite `null` o `Recontado` en el `Create`).
4. **cierre al final**: `→ Recontada` (limpio) o `→ Pendiente - recuento` (con diferencia).
   El marcado de cheques a `Recontado` va **después** de la bolsa, en un `Update` que toca
   **únicamente** `sanic_checkstatus` — la exención `touchesOnlyCheckStatus` de
   `RecountingGuard.IsWriteExempt` (`04` §11.3).

**El seeder NO escribe**: `TotalNIO/USD/EUR`, `HasDifference`, `CH-Tbl-Transactions`, ni
**ninguno** de los cuatro espejos (E-1…E-4), ni `Total`/`TotalAmount`/`Subtotal`/`IsRelatedToBag`
(campos fórmula/rollup). Todo eso es **salida** que `--verify` muestrea.

### 7.5 Decisiones tomadas en los puntos duros

| Tema | Decisión | Motivo |
|---|---|---|
| **Identidades** | `Cashier`/`Cajero` = usuarios **reales** del entorno (16 en DEV); fallback al usuario de aplicación del SP si no hubiera ninguno | No hay excepción por identidad en ningún guard (§6.3, punto 4·b·2) y este seeder **no pide una**. Respeta el guard por **orden**, igual que el de Reception |
| **Fechas históricas** | `overriddencreatedon` en el `Create` (verificado: `IsValidForCreate = True`), sesiones repartidas entre ayer y hace 90 días. `--no-backdate` lo desactiva | Es la única forma soportada de fijar `createdon`; el `--dry-run` **verifica la metadata antes de prometerlo** |
| **Solicitudes históricas RESUELTAS** | Dos caminos, y **sólo uno es gratis**. **(a)** el tipo `"Crear sesion de recuento"` cuya sesión cuelga de una mesa cuyo `Cajero` es el propio actor **nace RESUELTA sola**: la auto-aprueba `RecountingSessionAutoApprovePlugin` en el `Create`. El seeder crea **una** mesa con `Cajero` = actor para habilitar exactamente eso. **(b)** los otros **tres** tipos **no pueden** nacer resueltos (`ReceptionGuard.EvaluateApprovalCreate` lo prohíbe explícitamente) **ni** resolverse después (`EvaluateApprovalUpdate` exige resolutor ≠ creador **y** pertenencia al grupo AAD aprobador; el seeder es **una** identidad y es la creadora de todo). Por defecto **nacen PENDIENTES**; `--resolve-approvals` es **opt-in** y apaga temporalmente el único paso `CashHub.GuardReception: Update de sanic_chtblapprovalrequest`, resuelve en lote y lo restaura (`try/finally` + verificación) | Pedir una excepción por identidad en el guard sería una **llave maestra permanente** para un problema de scripts que ya tiene solución acotada y auditable. Es el mismo criterio de §6.3, punto 4·b·2 |
| **`Confirmed` de la sesión** | **se escribe** en el `Create` de las sesiones históricas | `register-recounting-guard.sh` registra **sólo el `Delete`** sobre `sanic_chtblrecountingsession`: no hay gate de `Create`/`Update`. Una sesión histórica sin `Confirmed` es una sesión que nunca existió. La **app** sigue sin escribirla (`01` §2.4) |
| **`AccountNumber`** | `int` aleatorio de 9 dígitos | Es `int` 1..999999999, **no** texto con guiones (`01` §2.4, divergencia **L-4**) |
| **`Quantity` / `CheckAmount`** | el generador **jamás** produce ceros | `Quantity` tiene mínimo **1** y `CheckAmount` mínimo **1**: una fila en 0 no se guarda |
| **`OtherLocaCheckAmount` vs `OtherLocalCheckAmount`** | dos nombres distintos, verificados en `Entity.xml` | `sanic_otherlocacheckamount` en `DepositsRecountings` (**sin** la `l`), `sanic_otherlocalcheckamount` en `DeclaredDeposits` (**con** la `l`) |
| **`MatchedCheck`, jamás `DeclaredDepositID`** | el enlace del cheque declarado va por `MatchedCheck` | Es el bug **D-6** de la v1: limpiar `DeclaredDepositID` dejaba el cheque declarado huérfano **para siempre** |

### 7.6 `--clean`: los SEIS pasos que hay que apagar

El borrado **no tiene orden legal posible** (`Recontada` es **terminal** y una sesión
`Confirmed` no se borra ni siendo propia), así que se desactivan **sólo** estos seis y se
restauran **siempre** (`try/finally` + verificación + `--restore-steps` de rescate):

```
CashHub.GuardRecounting: Delete de sanic_chtbldepositsrecounting
CashHub.GuardRecounting: Delete de sanic_chtblchecks
CashHub.GuardRecounting: Delete de sanic_chtblcountedmoneydetails
CashHub.GuardRecounting: Delete de sanic_chtblapprovalrequest
CashHub.GuardRecounting: Delete de sanic_chtblrecountingsession
CashHub.GuardReception:  Update de sanic_chtblbags
```

Los cinco primeros salen **textuales** del header de `register-recounting-guard.sh`; el sexto
es el que congela `Recontada` y hay que apagarlo para **devolver las tulas a
`Almacenada en bóveda`**.

**Orden inverso legal del `--clean`:**

1. `CH-Tbl-Transactions` que creó el guard sobre nuestros depósitos
   *(no necesita apagar nada: `sanic_chtbltransactions` **no tiene** paso `Delete` registrado)*
2. desasociar los lookups cruzados (`DeclaredChecks.MatchedCheck`,
   `DeclaredDeposits.RelatedRecountedDeposit`) con `DELETE …/<NavProperty>/$ref`
3. **devolver las tulas** a `Almacenada en bóveda` y desenlazar `RecountingSession` /
   `BagDeclarationID`
4. borrado por marcador `SRC-*`: arqueos → cheques → depósitos → detalles de lectura →
   lecturas → detalles de sesión → solicitudes → sesiones → cheques declarados → depósitos
   declarados → bolsas declaradas → declaraciones → mesas

> **Los ESPEJOS y `BagRecountClosure` NUNCA se tocan.** Al volver a bóveda el cierre es un
> **no-op por contrato**: sólo actúa al **ENTRAR** a `Recontada` / `Pendiente - recuento`.

### 7.7 Fases del CLI

```bash
python3 scripts/seed-recounting-stress.py --dry-run          # plan + conteos reales, CERO escrituras
python3 scripts/seed-recounting-stress.py --all              # F1 + F2 + F3 + F4
python3 scripts/seed-recounting-stress.py --only-declarations
python3 scripts/seed-recounting-stress.py --only-sessions
python3 scripts/seed-recounting-stress.py --only-recount     # requiere F2 (y F1 para enlazar)
python3 scripts/seed-recounting-stress.py --only-xml         # requiere F3
python3 scripts/seed-recounting-stress.py --verify           # espejos + latencias + topes
python3 scripts/seed-recounting-stress.py --clean
python3 scripts/seed-recounting-stress.py --restore-steps    # rescate de pasos
```

`--dry-run` **ya funciona** (autentica, lee metadata y conteos reales de DEV, imprime el plan
y el estado de los 28 pasos que audita) y **no escribe una sola fila**. Todos los nombres de
columna, `EntitySetName` y **21 propiedades de navegación** de `@odata.bind` fueron validados
contra la metadata real de DEV: **cero columnas imaginadas**.

### 7.8 Qué depende del deploy del guard

El seeder **aborta** con mensaje explícito si falta cualquiera de los pasos que audita. Al
2026-08-17 en DEV faltan **23 de 28**: están activos `CheckMirrorStamp` (×2) y
`GuardReception` (`Update` de tulas y de solicitudes); **no están registrados** los 11 pasos
de `GuardRecounting`, los 2 de `BagRecountClosure`, los 2 de `RecountingAutoApprove`, los 3 de
`DepositSessionMirror`, los 2 de `DeclaredCheckBagMirror` y los 3 de `XmlReadingDetailMirror`.

**Orden de ejecución:**

1. `dotnet build -c Release src/backend/CashHub.Plugins/CashHub.Plugins.csproj`
2. `./scripts/register-recounting-mirrors.sh` (§4, paso 2)
3. `./scripts/register-recounting-guard.sh` (§6.5, paso 2)
4. `python3 scripts/seed-recounting-stress.py --dry-run` → confirmar **28/28 ACTIVOS**
5. `python3 scripts/seed-recounting-stress.py --all`
6. `python3 scripts/backfill-recounting-mirrors.py --dry-run` → debe dar **0 a estampar**
   sobre lo recién sembrado: si da distinto de 0, algún plugin de espejo **no corrió dentro
   de `$batch`** y eso es un hallazgo, no un detalle.

### 7.9 Resultado real de la siembra (DEV, 2026-08-17)

El plan de §7.3 **corrió entero contra DEV** con los 28 pasos ACTIVOS. La corrida se cortó dos
veces y se completó con el flag nuevo **`--resume`** (§7.10), sin `--clean` y sin duplicar una
sola fila.

#### Volúmenes finales (`--dry-run` posterior a la siembra)

| Tabla (marcador) | Objetivo §7.3 | Real | |
|---|---:|---:|---|
| `Declarations` `SRC-DECL*` | ~60 | **60** | ✅ |
| `DeclaredBags` `SRC-DB*` | 3.000 | **3.000** | ✅ |
| `DeclaredDeposits` `SRC-DD*` | ~12.000 | **12.379** | ✅ |
| `DeclaredChecks` `SRC-DC*` | ~13.800 | **21.326** | ✅ (la fórmula del plan sub-estima: no suma los 3.600 de las 6 bolsas gordas) |
| `CountingDesks` `SRC-MESA*` | 40 | **40** | ✅ |
| `RecountingSessions` `SRC-S*` | 600 | **600** | ✅ |
| `ApprovalRequests` `SRC-AR*` | 800 + 200 | **1.000** | ✅ (800 históricas de F2 + 200 del cierre de F3) |
| `RecountingSessionDetails` `SRC-SD*` | 1.500 | **1.500** | ✅ |
| `DepositsRecountings` `SRC-DR*` | ~4.500 | **4.563** | ✅ |
| `CountedMoneyDetails` `SRC-CMD*` | ~15.750 | **15.938** | ✅ |
| `Checks` `SRC-CK*` | ~3.100 | **6.175** | ⚠️ ver nota |
| `XMLReadings` `SRC-XR*` | 700 | **700** | ✅ |
| `XMLReadingDetails` `SRC-XD*` | ~31.500 | **31.486** | ✅ |
| tulas `Recontada` | 1.000 | **1.000** | ✅ |
| tulas `Pendiente - recuento` | 200 | **200** | ✅ |
| tulas que quedan `Asignada a cajero` | 300 | **300** | ✅ |
| `CH-Tbl-Transactions` **creadas por el GUARD** | ~3.000 | **3.051** | ✅ |

> ⚠️ **Cheques: 6.175 y no ~3.100.** Dos cosas. (1) La fórmula del plan
> (`depósitos × 0,55 + gorda`) no refleja el generador real: los pesos
> `(0,1,2,3,6)/(50,22,14,9,5)` dan media **1,07 cheques por depósito** → ~5.500 en una corrida
> limpia. (2) Los ~700 restantes vienen de los dos cortes: el `--resume` **conserva** el sorteo
> de los depósitos que ya tenían cheques y sólo vuelve a tirar por los que tienen CERO, así que
> nunca duplica pero sí acumula. **Ninguna fila es duplicada**: cada cheque cuelga de un solo
> depósito y los montos del depósito se recalcularon contra lo realmente persistido.

#### Throughput real (`$batch` de 100, 6 workers)

| Etapa | Filas | Tiempo | op/s |
|---|---:|---:|---:|
| F3 · cheques (`Create`) | 4.357 | 152,3 s | **28,6** |
| F3 · enlace cheque → declarado (`PATCH`, dispara B3) | 783 | 30,7 s | 25,5 |
| F3 · montos del depósito | 3.457 | 75,3 s | 45,9 |
| F3 · enlace `DeclaredDeposits.RelatedRecountedDeposit` | 1.829 | 23,0 s | 79,7 |
| F3 · **cierre a `Recontada`** (guard: totales + asientos) | 1.000 | 195,7 s | **5,1** |
| F3 · cierre a `Pendiente - recuento` | 200 | 15,4 s | 13,0 |
| F3 · detalles de sesión al estado final | 1.200 | 12,8 s | 94,0 |
| F3 · cheques → `Recontado` | 4.357 | 114,8 s | 38,0 |
| F3 · solicitudes de diferencia | 200 | 11,3 s | 17,8 |
| F4 · `XMLReadings` | 700 | 7,1 s | 99,1 |
| F4 · `XMLReadingDetails` | 31.486 | 516,6 s | **60,9** |

**El cierre es el cuello de botella y tiene que serlo: 5,1 op/s** — cada `PATCH` de tula
dispara `BagRecountClosurePlugin`, que lee todos los depósitos persistidos, recalcula los tres
totales y crea un asiento por depósito. Es el precio de que la plata la calcule el servidor.

**Errores: 0. Reintentos de red consumidos: 11 (todos `batch/429`)**, absorbidos por el backoff
nuevo (§7.10). Ningún rechazo del guard: **`RECHAZOS DEL GUARD: 0`**.

#### Verificación post-siembra

**a) Espejos E-1..E-4 — `backfill-recounting-mirrors.py --dry-run`:**

| Espejo | Filas leídas | Ya al día | **A estampar** |
|---|---:|---:|---:|
| E-1 depósito → sesión | 9.825 | 9.825 | **0** |
| E-2 cheque → tula | 8.279 | 8.279 | **0** |
| E-3 cheque declarado → bolsa | 22.587 | 22.587 | **0** |
| E-4 detalle de lectura → sesión-detalle + vigencia | 31.486 | 31.486 | **0** |

Los cuatro plugins de espejo estamparon **el 100 %** de lo sembrado **dentro de `$batch`**.

**b) Totales que escribió el guard** (muestreo de 20 tulas `Recontada`, suma server-side de sus
depósitos, `Cash + BAC + Other` por moneda): **20/20 exactas** en NIO, USD y EUR.
El seeder **no escribió un solo total**.

**c) Asientos contables** (`CH-Tbl-Transactions` colgando de depósitos `SRC-DR*`):

- **3.051** asientos / **3.051** depósitos con asiento → **0 duplicados** (idempotencia del
  guard verificada sobre el universo entero, no sobre una muestra).
- `TransactionType`: **3.051/3.051 = Depósito** (159460000).
- `ProcessingType`: **1.719 Manual** (el depósito tiene cheques) / **1.332 Batch** (no tiene) —
  **0 desvíos** contra la regla `hasChecks ? Manual : Batch`.
- Montos, moneda y cuenta: **3.051/3.051 idénticos** al depósito de origen.

**d) Delegación viva** (queries reales de la app, paginadas siguiendo `@odata.nextLink`):

| Query | Filas | Páginas (100) | Latencia |
|---|---:|---:|---:|
| Cheques de la **tula gorda** vía `Bag` (E-2) | **612** | 7 | 2.036 ms |
| `DeclaredChecks` de la **bolsa gorda** vía `DeclaredBagMirror` (E-3) | **600** | 6 | 1.745 ms |
| Depósitos de una sesión vía `RecountingSessionMirror` (E-1) | 33 | 1 | 244 ms |
| **Q17** `XMLReadingDetails` de un detalle (E-4 + `XMLReadingActiva`) | 60 | 1 | 334 ms |

🧨 **E-1 y E-4 NO cruzan las 500 filas y no pueden cruzarlas con este plan**: 1.500 tulas
repartidas en 600 sesiones dan **máx. 33 depósitos por sesión**, y 700 lecturas sobre 1.500
detalles dan **máx. 60 detalles activos por sesión-detalle**. Lo que esas dos columnas prueban
acá **no es el tope de 500 sino la DELEGABILIDAD**: el `Filter` corre server-side sobre tablas
de **9.825** y **31.486** filas y devuelve el conjunto completo — el dot-walk de dos saltos que
reemplazan ni siquiera delega. Para forzar >500 en E-1/E-4 habría que concentrar el volumen
(`--sessions 20`, o más lecturas por detalle); **el tope de 500 ya está cubierto por E-2 y E-3**,
que son las dos grillas donde la v1 truncaba sin avisar.

**e) Solicitudes `"Tula con diferencia"`:** 400 en total con el marcador (200 históricas de F2 +
**200 del cierre de F3**). Las 200 del cierre llevan el **GUID real** de la tula en
`RequestResources` (las históricas llevan uno inventado, por eso el `--resume` no las confunde),
**200/200 tulas `Pendiente - recuento` cubiertas**, **0 JSON inválidos**, **200 PENDIENTES** de
resolución (nacer resuelta lo prohíbe `ReceptionGuard`). `RequestDetails` es un **fragmento**
HTML de 250 chars — **no** un documento: `<html>` = 0 y `</html>` = 0 **a propósito** (se
renderiza dentro de un `HtmlText`). Muestreo de 3: **17 tags abiertos / 17 cerrados**, **0 `&`
sin escapar**, y el fragmento **parsea con XML estricto**.

### 7.10 `--resume`: retomar una corrida cortada sin duplicar

```bash
python3 scripts/seed-recounting-stress.py --resume --all
python3 scripts/seed-recounting-stress.py --resume --only-recount
python3 scripts/seed-recounting-stress.py --resume --all --dry-writes   # ENSAYO: lee real, simula escrituras
```

**La regla: ninguna etapa decide por un contador ciego.** Cada etapa consulta lo que ya existe
por marcador `SRC-<TAG>` y salta si está completa o completa **el delta**, siempre **keyed por
el dato**:

| Etapa | Cómo decide el delta | ¿Ambiguo? |
|---|---|---|
| F1 `DeclaredBags` / F2 mesas, sesiones, solicitudes / F4 lecturas | `objetivo − existentes` por marcador | no |
| F1 depósitos declarados | `DeclaredBag` **sin ningún** `DeclaredDeposit` (`dep_min ≥ 1`) | no |
| F3 apertura + detalle de sesión | `RecountingSessionDetail` existentes → rehidrata `bag`, `sesión`, `declaración` | no |
| F3 depósitos | tula **sin ningún** depósito | no |
| F3 arqueo | depósito **con 0** `CountedMoneyDetails` (`k ≥ 1` siempre) | no |
| F3 montos | monto guardado ≠ monto recalculado (arqueo real + cheques reales) | no |
| F3 enlaces (`MatchedCheck`, `RelatedRecountedDeposit`) | lookup en `null` | no |
| F3 cierre | **estado de la tula** (`Recontada` / `Pendiente` ya cuentan contra el objetivo) | no |
| F3 solicitudes de diferencia | `BagGUID` real ya presente en un `RequestResources` | no |
| F4 detalles de lectura | lectura **con 0** detalles (`xmd_min ≥ 1`) | no |
| **F3 cheques** | depósito **con 0** cheques | ⚠️ **sí** |

⚠️ **La única etapa que no es idempotente en volumen es la de cheques**: "cero cheques" también
es un resultado legítimo del generador (peso 50 %), así que un `--resume` sobre una etapa de
cheques **ya completa** le da una segunda tirada a esos depósitos e **infla el total** (nunca
duplica filas). El seeder lo grita por stdout antes de escribir. La tula gorda está protegida
aparte: si **alguna** tula ya tiene ≥ `--fat-bag-checks`, no se engorda ninguna otra.

### 7.11 Dos bugs que encontró la corrida

**1. ✅ CORREGIDO — BUG DE PLUGIN: `CheckMirrorStamp` B3 en Pre-operation del `Create`.**
**Todo** `Create` de `sanic_chtblchecks` que trajera `sanic_RelatedDeclaredCheck` fallaba siempre:

```
0x80040265  No se pudieron estampar los espejos del cheque:
            Entity 'sanic_CHTblChecks' With Id = <guid> Does Not Exist
```

`CashHub.CheckMirrorStamp: Create de sanic_chtblchecks` estaba registrado en **stage 20
(Pre-operation), síncrono**. En pre-op **la fila del cheque todavía no existe**, y
`CheckMirrors.ResolveMatchUpdates` con `message = "Create"` hacía que el plugin ejecutara
`system.Update(declarado, MatchedCheck = ref(cheque, target.Id))` — un lookup apuntando a una
fila inexistente. Integridad referencial lo rechazaba.

- **Alcance**: bloqueaba el camino "cheque físico que nace ya matcheado con su declarado", que es
  exactamente lo que hace el matcher de `04` (§9.2 paso 3).
- **Corrección aplicada — el `Create` va PARTIDO en dos pasos** (el `Update` **no se tocó**: ahí
  la fila ya existe y el write-back sigue viviendo en su mismo paso Pre-op):

  | Paso | Stage | Qué estampa | Por qué ahí |
  |---|---|---|---|
  | `CashHub.CheckMirrorStamp: Create de sanic_chtblchecks` | **20** Pre-op | B6 (`sanic_bag`) + B-9 (`sanic_recountingsession`) | se escriben **EN el `Target`**: cero escrituras extra, y en post-op esa escritura sería muerta |
  | `CashHub.CheckMirrorStamp: Create de sanic_chtblchecks (post-op B3)` | **40** Post-op | B3 (`sanic_matchedcheck` en el declarado) | escribe en OTRA tabla un lookup **al cheque**: recién en post-op esa fila existe |
  | `CashHub.CheckMirrorStamp: Update de sanic_chtblchecks` | **20** Pre-op | B6 + B-9 + B3 | sin cambios — en `Update` la fila ya existe en cualquier stage |

  El **stage es parte de la decisión y vive en el dominio puro**: `CheckMirrors.CanWriteBackMatch`
  (el write-back sólo si la fila del cheque ya existe: `Create` ⇒ stage ≥ 40, `Update` ⇒ siempre) y
  `CheckMirrors.ShouldStampTargetMirrors` (los espejos que se escriben en el `Target` sólo en
  pre-op). `ResolveMatchUpdates` recibe ahora `stage` y, además, el `MatchedCheck` que **ya** tiene
  el declarado destino: si coincide **no reescribe** (idempotencia — no ensuciar la auditoría del
  declarado ni re-disparar sus propios espejos por nada). El plugin lee esa columna sólo cuando hay
  chance real de estampar: una lectura de UNA columna es más barata que la escritura que evita.
- **Verificado en vivo en DEV** (Web API, contra el propio stress): `POST` de un cheque
  `PRUEBA-STAGE-*` con `sanic_RelatedDeclaredCheck` y `RelatedReCounterDepositID` en un depósito
  cuya tula está *Asignada a cajero* → **HTTP 201**, `MatchedCheck` del declarado = el cheque nuevo,
  `sanic_bag` (E-2) y `sanic_recountingsession` estampados en el mismo alta, y un `PATCH`
  re-vinculando al MISMO declarado no cambia nada. Registros de prueba eliminados.
- **Workaround del seeder** (etapa 5.b): sigue vigente pero **ya no es necesario** — el cheque
  puede nacer con el lookup. **No se apagó ningún paso.** En la corrida original la etapa 6 reportó
  cuántos `MatchedCheck` tuvo que rellenar el cliente: **0 de 783** → el camino `Update` ya
  estampaba todos, y ese cero fue la evidencia de que el defecto era de *stage*, no de lógica.
- Los tests de `CheckMirrorsTests` cubren la decisión pura, incluido el gate de stage
  (`Create_EnPreOperation_NoTocaAlDeclarado`, `Create_EnPostOperation_EstampaElMatch`,
  idempotencia). Lo que **ningún** test unitario puede ver es el **registro** (en qué stage está
  el paso): eso lo fija `scripts/register-tracker-mirrors.sh` y lo audita la Web API.

**2. BUG DEL SEEDER — `$batch` con 400 en el sobre se contaba como fallo total.**
Dataverse responde el **sobre** del `$batch` con `HTTP 400` cuando **una** de las partes falla,
pero el cuerpo es el multipart **completo** con el status de cada parte. `batch_exec` leía sólo
400 chars del cuerpo y marcaba **las 100 operaciones del chunk** como error: la corrida reportó
**4.937 errores con 424 filas realmente escritas**. Ahora, si el cuerpo es un `batchresponse`
parseable, se parsea parte por parte.

**3. BUG DEL SEEDER — el `TimeoutError` del socket mataba el proceso** (lo que cortó la corrida
original). `req()` y `batch_exec` cubrían `HTTPError` y `URLError`, pero el timeout salta
**crudo** desde `h.getresponse()` (`TimeoutError`, que es `OSError`) y **no** viaja envuelto en
`URLError` — y saltando desde un thread del pool se llevaba la fase entera puesta. Se agregó
`except (TimeoutError, OSError, http.client.HTTPException)` con backoff de 5 s en ambos, `retry`
de `$batch` subido de 3 a **6**, y un contador de reintentos por causa que se imprime al final.
