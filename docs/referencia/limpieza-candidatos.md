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

### C.2 — 2 roles funcionalmente vacíos

`CH-SR-AnalyticsRead` y `CH-SR-OpsAdmin`: **cero privilegios sobre tablas de negocio**, solo
lectura de `CH-Tbl-AppCatalog(Access)` — lo mínimo para abrir el Hub. Son byte-idénticos entre sí.

Están provisionados y referenciados en 3 scripts, así que "sin uso" es impreciso: **son roles
reales pero vacíos**. Otra vez, dos lecturas opuestas: o son placeholders que nunca se llenaron
(y entonces falta darles privilegios), o son restos (y entonces sobran). El nombre sugiere lo
primero: alguien quería un rol de solo-lectura analítica y otro de operaciones.

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
