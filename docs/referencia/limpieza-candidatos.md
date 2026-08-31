# Candidatos de limpieza — CashHub

> Análisis de arquitectura sobre el inventario reconciliado el 2026-08-31
> ([`inventario-componentes.md`](inventario-componentes.md)). Responde tres preguntas por
> componente: **para qué existe**, **si de verdad se usa**, y **si se puede eliminar**.
>
> Fuente de los veredictos de uso: reconciliación de 585 filas + auditoría adversarial que
> refutó 28 de 77 veredictos de no-uso (36% de falsos positivos). **Solo entran acá los que
> sobrevivieron la auditoría.** Los 28 refutados están en §0 del inventario.

## Estado de ejecución

**Categoría A ejecutada el 2026-08-31** contra DEV (`org36e60d9d`) con
[`scripts/cleanup-categoria-a.py`](../../scripts/cleanup-categoria-a.py) — dry-run primero,
`--apply` después. **Categoría A: 11 de 11 ✅ COMPLETA.** `AppCatalog.sanic_tags` se cerró el 2026-08-31 con el
paso manual en el maker portal (ver B.4).

**Categoría B: ✅ COMPLETA.**
- Variables de entorno: 21 de 23 eliminadas; sobreviven `CHEVSharedMailboxAddress` y
  `CHEVOpsTeamsGroupId`. Detalle en [`limpieza-env-vars.md`](limpieza-env-vars.md).
- `sanic_CHCanResolveApproval`: eliminada tras reverificar contra un export fresco (ver B.2).
- `CH-CL-SharedComponents`: eliminada por decisión del usuario (ver B.3).

El dry-run desmintió tres de mis propias conclusiones, y vale documentarlo porque explica por qué
el script verifica en vez de confiar en el análisis estático:

| Componente | Lo que decía el análisis | Lo que encontró el ambiente |
|---|---|---|
| `CH-Tbl-AppCatalog.sanic_tags` | Borrar directo | Está en **5 vistas y 1 formulario**. Se despejaron 4 vistas y el formulario; la **Quick Find no se puede editar por API**. Requiere un paso manual — ver B.4. |
| `CH-Tbl-ClientAccount.sanic_chchrecordstatus` | Borrar directo | **2240 filas con dato, las 2240 con el MISMO valor.** Eliminada el 2026-08-31 tras aprobación explícita — ver B.4. |
| `CH-Tbl-Client.sanic_iseditable` | Borrar directo | **6519 filas con dato.** Resultó ser una **columna fórmula** derivada de `sanic_recordstatus` — ver C.4. Eliminada el 2026-08-31 tras aprobación explícita. |

> **La lección**: "cero referencias en código" y "cero datos" son cosas distintas, y el análisis
> estático solo puede probar la primera. Ninguna búsqueda sobre el repo iba a decirme que
> `sanic_iseditable` tiene 32 clientes marcados a mano.

## Cómo leer este documento

| Categoría | Significado |
|---|---|
| **A — Borrar directo** | Cero referencias en código Y cero datos en el ambiente. Verificado en vivo. |
| **B — Borrar con ajuste** | Muerto, pero borrarlo requiere un cambio previo (terminar una migración, desregistrar un step, etc.). |
| **C — No tocar sin decisión** | Técnicamente sin uso, pero borrarlo es una decisión de producto, no de limpieza. |

**Regla que aplico en todo el documento**: la asimetría de costos. Un componente que se deja
puesto de más cuesta ruido. Un componente vivo que se borra cuesta producción. Ante duda,
categoría C.

---

## A — Borrar directo

Todos verificados con doble condición: **0 referencias en código** (los tres árboles de canvas,
flows, `src/backend/`, `scripts/`) **y 0 datos en el ambiente DEV** (consultado por FetchXML).

### A.1 — Tabla completa: `CH-Tbl-ClientDepositHistory` ✅ ELIMINADA

| Aspecto | Dato |
|---|---|
| **Propósito** | Historial de depósitos por cliente. Función cubierta hoy por `CH-Tbl-DeclaredDeposits` y `CH-Tbl-Transactions`. |
| **Uso en código** | Ninguno. No aparece siquiera declarada como datasource en ninguna app (`pkgs/TableDefinitions/` no la tiene). |
| **Datos en DEV** | **0 filas** (verificado por FetchXML aggregate). |
| **Convergencia** | Marcada de forma independiente por dos agentes distintos y confirmada por la auditoría con búsqueda exhaustiva en los tres árboles de fuente. |

Arrastra consigo sus 3 campos custom (`sanic_name`, `sanic_client`, `sanic_chchrecordstatus`) y
su relación con `CH-Tbl-Client`. **Es el borrado más limpio y de mayor rendimiento del lote:
elimina una tabla, tres columnas, una relación y sus formularios/vistas de una sola pasada.**

### A.2 — Columnas muertas de `CH-Tbl-Received Emails` ✅ ELIMINADAS (6)

De 268 filas en DEV, **las seis tienen 0 valores no nulos**:

| Campo | Propósito aparente | Veredicto |
|---|---|---|
| `sanic_attachment` | Adjunto del correo. Sustituido por la tabla `CH-Tbl-EmailsAttachments`. | Muerto |
| `sanic_overrideby` | Quién forzó manualmente la validación. | Muerto |
| `sanic_overrideon` | Cuándo se forzó. | Muerto |
| `sanic_overridereason` | Por qué se forzó. | Muerto |
| `sanic_replyemailinternetmessageid` | `Internet-Message-Id` para encadenar la respuesta. | Muerto |
| `sanic_replyemailoutlookuniqueid` | Id único de Outlook para lo mismo. | Muerto |

Los tres `override*` tienen **evidencia positiva**, no solo ausencia:
`scripts/grant-console-email-fields.sh:30` dice explícitamente que *la app no las lee*.

Esto no es basura aleatoria — son **dos funcionalidades diseñadas y nunca construidas**: un
override manual de la validación con su pista de auditoría, y un encadenado de respuestas por
id de mensaje. Ninguna se implementó, ninguna tiene un solo dato.

### A.3 — Columna muerta suelta ✅ ELIMINADA

| Campo | Propósito | Datos en DEV |
|---|---|---|
| `CH-Tbl-Receptions.sanic_comments` | Comentario libre de recepción. | **0 no nulos**, 0 dependencias |

> Los otros tres candidatos que este documento clasificaba como A (`AppCatalog.sanic_tags`,
> `ClientAccount.sanic_chchrecordstatus`, `Client.sanic_iseditable`) fueron **reclasificados**
> tras la verificación en vivo. Ver "Estado de ejecución" arriba y B.4 / C.4 abajo.

---

## B — Borrar con ajuste previo

### B.1 — 20 de 23 variables de entorno sin un solo consumidor

**Este es el hallazgo de mayor volumen.** Solo 3 de 23 env vars se consumen hoy
(`OpsTeamsGroupId`, `RfcApplyTimeoutMinutes`, `SharedMailboxAddress`).

Un dato estructural de la auditoría que vale más que la lista: **ningún plugin C# lee
`environmentvariablevalue` — cero coincidencias en todo `src/backend/`.** O sea que en esta
solución las env vars solo pueden consumirse desde cloud flows. Eso reduce muchísimo la
superficie donde puede esconderse un uso.

**Familia 1 — migración a `CH-Tbl-Parameter` a medio hacer (11 variables)**

6 `CHEVAADGroup_*` (Admin, ClientAdmin, Counting, EmailValidation, Reception, Supervisor) y
5 `CHEVAppUrl_*` (ClientAdmin, Counting, EmailValidation, Reception, Supervisor).

Dos de ellas ya están **probadamente** reemplazadas:
- `CHEVBancaMailbox` → `LookUp('CH-Tbl-Parameters', Key = "Email.Banca.Mailbox" && 'Is Active' = true, Value)` en `screen ColaDeProcesamiento.fx.yaml:710`
- `CHEVAppUrl_ClientAdmin` → `Key = "Apps.ClientAdministration.Url"`, misma pantalla línea 711/712, más 2 flows que filtran por OData contra la tabla de parámetros

**El ajuste**: terminar la migración para las que faltan, y recién ahí borrar las 11 de una.
Es exactamente la dirección que ya venís empujando — la tabla de parámetros existe para esto.

**Familia 2 — sin patrón común (9 variables)**

`CHEVCsvExportLibraryUrl`, `CHEVEmailSubjectToAccept`, `CHEVEnvironmentURL`, `CHEVFileSizeMaxMB`,
`CHEVIbsBranchCodeList`, `CHEVProcessingLibraryUrl`, `CHEVSharedMailboxArchiveFolderID`,
`CHEVTimezone`.

Estas hay que mirarlas una por una: `CHEVFileSizeMaxMB` huele a que quedó obsoleta cuando el tope
de descompresión pasó a `CH-Tbl-Parameter`, pero eso hay que confirmarlo antes de tocarla.

> **Cuidado de ALM antes de borrar cualquier env var**: la definición vive en `CashHubCore`. Si la
> borrás en DEV y después importás la solución a TEST/PROD, desaparece también allá **junto con
> su valor**. Confirmá que ningún ambiente downstream la esté usando, no solo DEV.

### B.2 — Custom API `sanic_CHCanResolveApproval` ✅ ELIMINADA

**Reverificada el 2026-08-31 contra un export FRESCO de `CashHubApps`**, no contra la foto del
repo: se reexportó la solución, se desempaquetaron los 9 `.msapp` a YAML y se buscó en los tres
árboles de fuente más los flujos.

**Cero invocaciones en Power Fx ejecutable.** Los únicos hits están en `DataSources/Users.json` y
`pkgs/Wadl/Environment.xml`, que son catálogos autogenerados del conector y listan *todas* las
acciones del ambiente — no son invocaciones.

La desambiguación importa, porque hay tres APIs con nombres parecidos:

| API | Quién la invoca |
|---|---|
| `sanic_CHCanResolveApproval` | **nadie** ← eliminada |
| `sanic_CHResolveApprovalRequest` | `CH-App-Supervisor` |
| `sanic_CHResolveChangeRequest` | `CH-App-ClientAdministration` — el botón de resolver que funciona hoy |

Se eliminaron el parámetro de entrada (`ApprovalRequestId`), las dos propiedades de salida
(`Message`, `Outcome`) y la definición. Verificado después: las otras dos APIs siguen intactas.

> **Pendiente de código**: la clase C# `CanResolveApprovalPlugin` sigue en el assembly. Sacarla
> exige recompilar y desplegar, que es una operación aparte. Borrar el `plugintype` por API no
> serviría: se re-sincroniza desde el DLL mientras la clase siga compilada.

### B.3 — Librería `CH-CL-SharedComponents` ✅ ELIMINADA

Los 9 `ComponentReferences.json` (8 apps + la propia librería) estaban **vacíos** — reconfirmado
contra el export fresco del 2026-08-31. Ninguna app la importaba.

**Decisión del usuario**: borrarla y convivir con la duplicación. Adoptarla habría implicado un
refactor grande de las apps que hoy no se va a hacer.

**Corrección a un análisis previo**: este documento afirmaba que las 8 apps tenían "copias locales
de los mismos componentes". Al abrir la librería resultó tener **solo 2**: `cmpAppHeader`, que
`CH-App-ClientAdministration` sí tiene duplicado en local, y `cmpTheme` — 28 líneas de design
tokens (paleta, spacing, radios, tipografía, z-index) que **no existían en ninguna otra app**. La
duplicación real era mucho menor de lo que se dijo.

> **El fuente no se pierde**: la librería está commiteada en
> `solutions/unpacked/apps/CanvasApps/src/sanic_chclsharedcomponents_85fca/` y sigue recuperable
> desde git. Si algún día se quiere un design system compartido, `cmpTheme` es el punto de partida.

---

## C — No tocar sin tu decisión

### C.0 — Un flujo puede llamar a otro: el cuarto canal de invocación

**Hallazgo del 2026-08-31, a partir de una pregunta del usuario.** El método de verificación que
se venía usando —buscar invocadores en los tres árboles de canvas, el backend y los scripts—
**tenía un hueco: no contemplaba que un flujo invoque a otro.**

`CH-Flow-Create a Teams chat group based on Office 365 Group Members` parecía basura de manual:
encendido, con trigger de botón y sin ninguna app que lo llamara. **Está vivo**:

```json
"Run_a_Child_Flow": {
  "type": "Workflow",
  "inputs": { "host": { "workflowReferenceName": "f386eeb1-f2aa-f011-bbd2-000d3a52eba2" } }
}
```

Lo invoca `CH-Flow-Notify reviewers` (ON) dentro de un `For_each`, pasándole `sanic_aadgroupid`.
**No es candidato a limpieza.**

#### Cómo buscar este canal

```bash
rg -o '"workflowReferenceName":\s*"[0-9a-fA-F-]{36}"' solutions/unpacked/flow/Workflows/*.json
```

Mapa completo de child flows en el sistema (2026-08-31) — solo 3 flujos invocan a otros:

| Flujo padre | Child flow que invoca |
|---|---|
| `CH-Flow-IngestEmails` | `CH-Flow-ProcessEmail` |
| `CH-Flow-MailboxSweep` | `CH-Flow-ProcessEmail` |
| `CH-Flow-Notify reviewers` | `CH-Flow-Create a Teams chat group…` |

> **Verificación posterior al borrado de los 7**: se buscaron los GUID de los 7 flujos eliminados
> en los flujos supervivientes, en `apps/` y en `patch/`. **Cero referencias** — estaban realmente
> huérfanos. Y los dos destinos de child flow resuelven contra el ambiente y están ON. Nada se
> rompió, pero el hueco existía y podría haber costado caro con otro conjunto de candidatos.

**Los cuatro canales por los que se invoca algo en este sistema**: Power Fx de canvas app, acción
dentro de un cloud flow, código C# de plugin, y **`Run a Child Flow` de flujo a flujo**. Cualquier
verificación de "esto no se usa" que no cubra los cuatro es incompleta.

### C.1 — 7 cloud flows sin invocador ✅ ELIMINADOS

Confirmado por el usuario el 2026-08-31: `CH-Flow-Create client data` **se apagó a propósito**
porque la Custom API `sanic_CHResolveChangeRequest` lo reemplazó. No fue una desactivación
accidental — que era la duda que había frenado este borrado.

Eliminados por dos causas distintas:

**(a) Retirados y reemplazados por Custom API (5, en Draft)**
`CH-Flow-Create client data`, `CH-Flow-Apply client data changes`, `CH-Flow-Activate row`,
`CH-Flow-Deactivate row`, `CH-Flow-RFCWatchdog`.

**(b) Encendidos pero imposibles de disparar (2)**
`CH-Flow-ExtractedTablesFromExcel` y `CH-Flow-Get JSON data from attachment`. Los dos tienen
trigger **`PowerAppV2`**, o sea que *solo* pueden ejecutarse si una canvas app los invoca. Se
verificaron los tres árboles de fuente: **ninguna app los llama**. No es que "no se usen" — es
que no pueden correr.

> **Detalle de ejecución**: Dataverse rechaza el `DELETE` de un workflow activo. El script pone
> `statecode=0` primero y después borra. Los dos encendidos se desactivaron en ese paso.

**Resultado**: de 18 flujos se pasó a **11, todos ON**. No queda un solo Draft en el sistema.

### C.2 — 2 roles funcionalmente vacíos ✅ ELIMINADOS, con transferencia previa

`CH-SR-AnalyticsRead` y `CH-SR-OpsAdmin`: ningún privilegio sobre tablas de negocio —solo
`prvReadsanic_CHTblAppCatalog(Access)`, lo mínimo para abrir el Hub— y **0 usuarios y 0 equipos
asignados**, verificado en vivo antes de borrar.

**Pero el análisis inicial tenía un criterio incompleto**, y vale documentarlo: llamó a OpsAdmin
"funcionalmente vacío" mirando solo privilegios sobre tablas de negocio. El valor de OpsAdmin no
estaba ahí, estaba en un **privilegio de plataforma**:

> `CH-SR-OpsAdmin` era el **único de los 13 roles con `prvReadPluginTraceLog`**, y por diseño
> deliberado: `apply-supervisor-role-remediation.sh` se lo quitó a `CH-Role`, `AnalyticsRead`,
> `ClientAdminContributor` y `ClientAdminApprover` para que quedara solo en el rol de soporte.
> Motivo documentado: *"los trace logs son semi-públicos y arrastran payloads de negocio"*.

Borrarlo sin más habría dejado al sistema **sin nadie capaz de leer trace logs**.

**Qué se transfirió a `CH-SR-ConfigAdmin` y qué no.** El delta OpsAdmin-menos-ConfigAdmin eran 65
privilegios:

| | Cantidad | Decisión |
|---|---|---|
| Lectura (`prvRead*`) | **28** | **Transferidos.** Sostienen el diagnóstico, incluido `prvReadPluginTraceLog`. |
| Autoría (`prvCreate*`/`prvWrite*`/`prvDelete*`/`prvAssign*`/`prvShare*`) sobre Workflow, NewProcess, TranslationProcess, AsyncOperation | **37** | **NO transferidos.** Darle a un rol de configuración el poder de crear, modificar y borrar flujos y procesos es escalación de privilegios, no transferencia de capacidad de soporte. |

El script **transfiere primero y borra después**, y aborta el borrado si la transferencia falla.
Verificado post-ejecución: `ConfigAdmin` tiene hoy `prvReadPluginTraceLog`, `prvReadTraceLog`,
`prvReadWorkflow` y `prvReadAsyncOperation`.

**Scripts corregidos** — los tres nombraban roles que ya no existen:

| Script | Qué se arregló |
|---|---|
| `provision-appcatalog-access.sh` | Su lista `ROLES` nombraba `CH-SR-Counter`, `CH-SR-OpsAdmin` y `CH-SR-AnalyticsRead`. **`CH-SR-Counter` ya estaba roto desde el rename a `CH-SR-Cashier`, antes de esta limpieza.** Ahora lista los 9 roles que hoy tienen el privilegio. |
| `apply-supervisor-role-remediation.sh` | `ROLE_FILES` actualizado; el comentario de seguridad ahora apunta a `ConfigAdmin` como rol de soporte. |
| `provision-tracker-role.sh` | La nota "decisión del usuario 2026-08-12: AnalyticsRead no se toca" pasa a nota histórica. |

**Resultado**: de 13 roles a **11**.

### C.3 — `sanic_replyclosenote`: cableado pero nunca ejercitado

Caso raro que merece mención propia. La auditoría **refutó** su veredicto de muerto: sí está
referenciado, en el botón `btnCierreConfirmar` de
`patch/.../Other/Src/screen ColaDeProcesamiento.pa.yaml:874`.

Pero al consultar el ambiente: **0 valores no nulos en 268 filas.** O sea que el código existe y
la funcionalidad de cierre con nota **nunca se usó en la práctica**. No es candidato a borrar
—romperías la pantalla— pero sí vale preguntarse si esa función hace falta.

### C.4 — `CH-Tbl-Client.sanic_iseditable` ✅ ELIMINADA — era una columna fórmula

Este caso merece quedar documentado porque **el análisis se equivocó dos veces** antes de dar
con la respuesta, y las dos veces por no mirar el lugar correcto.

**Primera lectura (incorrecta)**: "6.487 en `Yes`, 32 en `No` — alguien marcó 32 clientes a mano,
hay una regla de negocio capturada en el dato y no implementada".

**Segunda lectura (también incorrecta)**: "los 32 son `STRESS-` y `MTX`, es data de prueba".

**La verdad**: `sanic_iseditable` era una **columna fórmula**. Su definición vivía en
`Entities/sanic_CHTblClient/Formulas/sanic_chtblclient-FormulaDefinitions.yaml`:

```
sanic_iseditable: =If(sanic_recordstatus = Active || sanic_recordstatus = Inactive, true, false)
```

Los 32 en `No` son **exactamente** los clientes en `PendingApproval` o `PendingDeactivation` —
comprobable en los datos que se extrajeron antes de borrar. Que fueran registros `STRESS-` fue
coincidencia: son clientes de prueba que estaban en estado pendiente.

**Por qué el borrado fue correcto, y más seguro de lo que parecía**: era dato *derivado*, no
autorado. El valor se reconstruye entero desde `sanic_recordstatus`. Pérdida de información al
borrarla: cero, por definición. Y nadie la leía.

> **La lección para el resto de esta limpieza**: antes de razonar sobre por qué una columna tiene
> los datos que tiene, revisar `Entities/<tabla>/Formulas/`. Una columna fórmula no la escribe
> nadie y sus valores no son decisiones de nadie — son el resultado de una expresión. Interpretarla
> como dato autorado lleva a conclusiones inventadas.
>
> Las 6 columnas fórmula que quedan en el modelo: `CountedMoneyDetails.sanic_totalamount`,
> `DeclaredBags.sanic_isrelatedtobag`, `DeclaredDeposits.sanic_total`,
> `DepositsRecounting.sanic_total`, `Transactions.sanic_total`, `XMLReadingDetail.sanic_subtotal`.
> Ninguna está en esta lista de candidatos.

---

## D — Hallazgos posteriores (análisis tabla por tabla)

### D.1 — `CH-Tbl-AppCatalog.sanic_aadgroupid` ✅ ELIMINADA — era una lápida

`nvarchar(255)`, opcional, sin descripción. **0 de 7 filas con valor.**

**Qué era**: los grupos AAD que habilitaban una app en el Hub, como string multivalor
separado por `;`.

**Qué lo reemplazó**: la tabla relacional `CH-Tbl-AppCatalogAccess` — una fila por par
(app, grupo), que sí tiene 4 filas con valor. El Hub lee de ahí:

```powerfx
ClearCollect(colAccess, ForAll('CH-Tbl-AppCatalogAccesses' As row, {
    AppId:   row.'App Catalog'.'CH-Tbl-AppCatalog',
    GroupId: Substitute(Trim(row.'AAD Group Id'), " ", "")
}));
```

**Por qué era una lápida y no un campo en uso**: el único código que lo tocaba era
`AppCatalogGuard.EvaluateDeprecatedCatalogGroups()`, invocado desde `GuardAppCatalogPlugin`,
y existía para **bloquear** cualquier escritura:

> *"El campo de grupos AAD del catálogo está deprecado. Declare los grupos en la tabla
> CH-Tbl-AppCatalogAccess (una fila por grupo)."*

Nadie lo leía. Se mantenía vivo solo para poder rechazar el modelo viejo — y si la columna no
existe, Dataverse rechaza la escritura por sí solo, sin necesidad de código defendiendo la puerta.

#### El campo se llama igual en TRES tablas

Esta es la parte que contamina cualquier búsqueda por nombre. De los 5 hits de `aadgroupid` en
Power Fx, flows y C#, **ninguno era de AppCatalog**:

| Dónde aparece | A qué tabla pertenece |
|---|---|
| App Hub (`App.fx.yaml`) | `CH-Tbl-AppCatalogAccess` |
| App ClientAdministration | `CH-Tbl-ClientDataChangePermission` |
| `CH-Flow-Notifyreviewers` | `CH-Tbl-ClientDataChangePermission` (`entityName` lo confirma) |
| `ChangeRequestApproverResolver.cs` | `CH-Tbl-ClientDataChangePermission` |
| `provision-approver-teams.sh`, `assign-clientadmin-team-roles.sh` | `CH-Tbl-ClientDataChangePermission` |

#### Una dependencia nueva: los filtros de un plugin step

Además de 3 vistas, la columna estaba en los **`filteringattributes` de un
`SdkMessageProcessingStep`** — componente tipo **92**, que no había aparecido antes. Dataverse no
deja borrar una columna que un step tenga en sus filtros.

Y acá el homónimo era peligroso de verdad: **dos steps distintos filtraban por `sanic_aadgroupid`**,
uno de cada tabla.

| Step | Filtros antes | Filtros después |
|---|---|---|
| `GuardAppCatalog: Update de app del catalogo` | `sanic_appurl,sanic_aadgroupid` | `sanic_appurl` |
| `GuardAppCatalogAccess: Update de acceso` | `sanic_aadgroupid,sanic_appcatalog` | **sin cambio** |

> El script identifica el step **por el id que reporta `RetrieveDependenciesForDelete`**, no
> buscando `contains(filteringattributes, columna)`. Buscar por texto habría agarrado también el
> step de `AppCatalogAccess` y le habría roto el filtro.

#### Limpieza de código asociada

Eliminados: `EvaluateDeprecatedCatalogGroups()` en `AppCatalogGuard.cs`, el bloque que la invocaba
en `GuardAppCatalogPlugin.cs`, la constante `Schema.AppCatalog.AadGroupId` (las de
`AppCatalogAccess` y `ChangePermission` siguen) y sus 2 tests. Build 0 errores; suite de
**1041 tests** (eran 1043 — bajan exactamente los 2 eliminados). Assembly redesplegado.

---

### D.2 — `CH-Tbl-ValidationResults`: 2 de 10 campos eliminados

Tabla de **8.388 filas**, muy viva: la escribe `ReceivedEmailValidationService.CreateResult()` y
la leen la consola de ingesta (`ColaDeProcesamiento`) y el tracker (`scrCorreo`). Se eliminaron 2
campos; los otros 8 quedan.

**`sanic_sourcetype` — cero información.** 8.388 de 8.388 filas con el MISMO valor
(*Shared Mailbox*), porque el C# lo escribía hardcodeado:

```csharp
[Schema.Result.SourceType] = new OptionSetValue(Schema.Run.SourceSharedMailbox)
```

Sin condición, siempre el mismo. Un picklist con un único valor en el 100% de los registros no
contiene información — mismo patrón que `ClientAccount.sanic_chchrecordstatus`.

**`sanic_rownumber` — redundante al 100%.** El número de fila ya está en `sanic_cellref`:

| `sanic_sheet` | `sanic_cellref` | `sanic_rownumber` |
|---|---|---|
| Depositos | **J33** | 33 |
| Depositos | **K7** | 7 |

La consola muestra *"Hoja y celda: Depositos!J33"* e ignora `Fila`, precisamente porque ya está ahí.

#### Lo que se dejó, y por qué

**`sanic_durationms` NO se borró** pese a no tener lectores. Tiene 8.388 valores reales y variados:
es el tiempo de ejecución de cada regla. Si una corrida se degrada, esa columna dice **qué regla**
la frena. Es capacidad no expuesta, no basura — mismo criterio que `AuditTrail.sanic_relatedcrl`.

#### El homónimo, otra vez

`sanic_sourcetype` existe **también en `CH-Tbl-ValidationRuns` y `CH-Tbl-ValidationRules`**. Solo se
eliminó el de `ValidationResults`; los otros dos siguen y son análisis aparte. El riesgo era real:
`seed-tracker-stress.py` y `seed-bandeja-demo.py` escriben **ambos** en el mismo archivo, y hubo que
atribuir línea por línea cuál POST iba a `/sanic_chtblvalidationresultses` y cuál a
`/sanic_chtblvalidationrunses`.

#### Limpieza de código

En `ReceivedEmailValidationService`: las 2 escrituras y el parámetro `row` de `CreateResult` con sus
3 llamadas. En `Schema.cs`: las constantes `Result.SourceType` y `Result.RowNumber` — las de
`Run` y `Rule` siguen. En los seeds: 4 escrituras, solo las de `ValidationResults`.

> **`Finding.RowNumber` NO se tocó.** Es concepto de dominio usado en toda `ContentRules.cs` para
> construir los hallazgos; lo que se eliminó es la plomería que lo persistía.

Build 0 errores, **1041 tests** sin cambio, assembly redesplegado (37 plugintype, 0 huérfanos).

---

### D.3 — `sanic_ValidationRules`: 3 de 11 campos eliminados

29 filas — el catálogo de reglas del motor de validación de correo. **Las 27 reglas son clases C#**
(`IValidationRule` en `ContentRules`, `EnvelopeRules`, `StructureRules`, `BusinessRules`); la tabla
solo les da configuración.

`ReceivedEmailValidationService` las carga con un `ColumnSet` explícito de **7 campos**:
`validationname` (la clave VR-nn), `name`, `validationorder`, `isactive`, `validationvalue`,
`validationfailmessage`, `validationdependency`. Los otros 4 quedaron afuera.

**`sanic_sourcetype` — cero información.** 29 de 29 filas con el mismo valor (*Shared Mailbox*), y
marcado `required`. Mismo caso que el homónimo de `ValidationResults`.

**`sanic_validationdatatype` y `sanic_validationoperation` — el motor declarativo que nunca se
construyó.** 0 de 29 filas con valor, vacíos al 100%. Juntos formaban el diseño *"regla como dato"*
(`dataType` + `operation` + `value`): declarar una validación sin escribir código. Solo `value`
sobrevivió — es el que hoy parametriza reglas como VR-06 (el tope de 5120 KB).

> `validationdatatype` estaba marcado **`required`** y sin embargo vacío en las 29 filas. Eso prueba
> que `RequiredLevel` en Dataverse es requisito de **formulario**, no de base: nunca se enforzó.

#### Efecto en la UI de la app de Configuración

Los tres estaban en el formulario `Information`, y `validationoperation` además en 2 vistas:
`Active ValidationRules` y **`CH-Config | Reglas inactivas`**. O sea que la app de Configuración
mostraba dos campos siempre vacíos y una columna en blanco en dos grillas.

Se corrigió `scripts/provision-config-app.py`, que los declaraba en su definición de formulario y
vistas — sin eso, la próxima corrida habría fallado al referenciar columnas inexistentes.

#### Lo que se dejó

**`sanic_notes` NO se borró** pese a tener solo 2 de 29 filas con valor y ningún lector en código.
El contenido es documentación real para quien configura:

> *VR-27: "Regla recomendada por arquitectura (lee TemplateId/TemplateVersion de la hoja Metadata).
> **Apagable** si negocio decide aceptar archivos libres."*

`provision-config-app.py` lo pone en una pestaña "Notas" del formulario. Lo lee un humano, no el
motor — mismo criterio que `relatedcrl` y `durationms`.

#### Sin recompilación

A diferencia de los campos de `ValidationResults`, estos tres **no estaban en `Schema.Rule`** ni en
ningún `.cs`. El único hit de `sanic_sourcetype` en `Schema.cs` es `Schema.Run.SourceType`, de
`ValidationRuns`, que no se tocó.

---

### D.4 — `CH-Tbl-ValidationRuns`: 2 de 8 campos, y un hallazgo que invirtió el análisis

292 filas. Se eliminaron `sanic_sourcetype` y `sanic_sourceentity`: **los dos con un único valor
en las 292 filas** (*Shared Mailbox* y `sanic_chtblreceivedemails`). La genericidad de la "tabla
genérica" nunca se usó — solo hubo una fuente.

`sourcetype` es el **tercero del mismo nombre** que cae, después de `ValidationResults` y
`ValidationRules`. Hoy ese nombre ya no existe en el modelo.

#### Las dos formas del vínculo están vivas, una por app

A diferencia de `Declaration`, acá **no se puede borrar el campo de texto**:

| Consumidor | Filtra por |
|---|---|
| Consola de ingesta | `SourceRecordID = Text(ThisItem.'CH-Tbl-Received Emails')` — **el texto** |
| `CH-Flow-MailboxSweep` | `$filter=sanic_sourcerecordid eq '...'` — **el texto** |
| Tracker (`scrCorreo`) | `ReceivedEmail.'CH-Tbl-Received Emails' = ...` — **el lookup** |

#### El hallazgo: 13 corridas huérfanas, y por qué el texto las conserva

`sanic_receivedemail` estaba en 279 de 292. Se escribió el paso **B1b** del backfill para estampar
las 13 faltantes, y **las 13 fallaron**:

```
'sanic_CHTblReceivedEmails' With Id = ... Does Not Exist
```

**Su correo de origen fue borrado.** No les falta el lookup: no puede existir. Y eso expone
exactamente el costo del diseño de texto:

> El lookup tiene **integridad referencial** — cuando se borra el correo, se limpia. El texto no:
> `sourcerecordid` conserva un **puntero colgado** a un registro que ya no está. Las 13 corridas
> sobreviven apuntando a la nada.

**Corrección de un análisis previo publicado en esta misma sesión**: se afirmó que *"la consola
muestra esas corridas y el tracker no"*. Es falso. La consola las filtra contra un correo de su
galería; si el correo no existe, no está en la galería y la corrida tampoco se muestra. **La
inconsistencia no es alcanzable.** El impacto real de las 13 es cero, y 12 de ellas son artefactos
`PROBE-*` de `probe-parser-limits.py`.

El paso B1b quedó en `backfill-tracker.py`, reescrito para **reportar los huérfanos en vez de
romper**, y sin el guard de `sourceentity` — que además ya no existe en esta tabla.

#### Lo que se dejó

**`sanic_runend`** (292/292, timestamps reales): con `runstart` da la duración de cada corrida.
Telemetría sin exponer, mismo criterio que `durationms`.

**`sanic_runresult`** (292/292, **99 Fail / 193 Pass**): variación real. Es el veredicto
desnormalizado de la corrida; la consola lo recalcula agregando `ValidationResults`. Un campo
contra agregar 8.388 filas de detalle es una desnormalización legítima.

#### Deuda que queda anotada

La consola filtra con `SourceRecordID = Text(...)`. **Comparar un GUID convertido a texto no delega
en Power Fx** — es precisamente lo que documenta el campo hermano de `Declaration`: *"este lookup
existe porque un GUID en texto no delega"*. Con 292 filas no se nota; con volumen, esa pantalla se
degrada. Cambiar ese filtro a `ReceivedEmail.'CH-Tbl-Received Emails' = ...`, como ya hace el
tracker, resolvería la delegación. **No se tocó**: es cambio de canvas app y esos van por el Studio.

---

## Antes de borrar cualquier cosa

1. **Columnas**: Dataverse no deja eliminar una columna que esté en algún formulario o vista.
   Hay que sacarla de `FormXml` y `SavedQueries` primero.
2. **El campo primario no se borra.** En las 42 tablas custom, `sanic_name` es el
   *primary name attribute* (marcado `PrimaryName` en su `<DisplayMask>`). Aunque no aparezca en
   el código, Dataverse lo usa para renderizar todo lookup y link de registro.
3. **Tablas**: eliminar primero las relaciones que la referencian.
4. **Env vars**: ver la advertencia de ALM en B.1 — se propagan a los ambientes downstream.
5. **Custom APIs**: desregistrar el step antes de borrar la definición, o queda un step apuntando
   a la nada.
6. **Verificar datos en vivo, no solo código.** Un grep limpio no prueba que la columna esté
   vacía. Todo lo que está en categoría A se verificó con FetchXML contra DEV.
7. **Verificar contra TEST/PROD, no solo DEV.** Todo este análisis se hizo contra
   `org36e60d9d` (DEV). Una columna vacía en DEV puede tener datos en producción.

## Resumen

| Categoría | Componentes | Estado |
|---|---|---|
| **A — Borrar directo** | 1 tabla + 1 relación + 3 columnas propias + 8 columnas | ✅ **COMPLETA** — 11 de 11 el 2026-08-31 |
| **B — Borrar con ajuste** | 21 env vars, 1 Custom API, 1 librería de componentes | ✅ **COMPLETA** el 2026-08-31 |
| **C — Decisión de producto** | 7 flows, 2 roles | Pendiente: riesgo de negocio, no de limpieza |

Lo eliminado en DEV: la tabla `CH-Tbl-ClientDepositHistory` (con sus 3 columnas, su relación con
`CH-Tbl-Client`, 3 formularios y 7 vistas), 6 columnas de `CH-Tbl-Received Emails` y
`CH-Tbl-Receptions.sanic_comments`. Efecto colateral limpio: los roles `CH-SR-ClientAdminApprover`
y `CH-SR-ClientAdminContributor` perdieron solos el privilegio `prvReadsanic_CHTblClientDepositHistory`.

> El punto 7 de la checklist es el que más importa: **este análisis se hizo contra DEV.**
> Nada de esto debería aplicarse a producción sin repetir la verificación de datos allá.
