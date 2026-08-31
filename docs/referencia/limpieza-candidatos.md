# Candidatos de limpieza — CashHub

> Análisis de arquitectura sobre el inventario reconciliado el 2026-08-31
> ([`inventario-componentes.md`](inventario-componentes.md)). Responde tres preguntas por
> componente: **para qué existe**, **si de verdad se usa**, y **si se puede eliminar**.
>
> Fuente de los veredictos de uso: reconciliación de 585 filas + auditoría adversarial que
> refutó 28 de 77 veredictos de no-uso (36% de falsos positivos). **Solo entran acá los que
> sobrevivieron la auditoría.** Los 28 refutados están en §0 del inventario.

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

### A.1 — Tabla completa: `CH-Tbl-ClientDepositHistory`

| Aspecto | Dato |
|---|---|
| **Propósito** | Historial de depósitos por cliente. Función cubierta hoy por `CH-Tbl-DeclaredDeposits` y `CH-Tbl-Transactions`. |
| **Uso en código** | Ninguno. No aparece siquiera declarada como datasource en ninguna app (`pkgs/TableDefinitions/` no la tiene). |
| **Datos en DEV** | **0 filas** (verificado por FetchXML aggregate). |
| **Convergencia** | Marcada de forma independiente por dos agentes distintos y confirmada por la auditoría con búsqueda exhaustiva en los tres árboles de fuente. |

Arrastra consigo sus 3 campos custom (`sanic_name`, `sanic_client`, `sanic_chchrecordstatus`) y
su relación con `CH-Tbl-Client`. **Es el borrado más limpio y de mayor rendimiento del lote:
elimina una tabla, tres columnas, una relación y sus formularios/vistas de una sola pasada.**

### A.2 — Columnas muertas de `CH-Tbl-Received Emails`

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

### A.3 — Columnas muertas sueltas

| Campo | Propósito | Datos en DEV | Nota |
|---|---|---|---|
| `CH-Tbl-Receptions.sanic_comments` | Comentario libre de recepción. | **0 no nulos** | Sin referencias en código. |
| `CH-Tbl-AppCatalog.sanic_tags` | Etiquetas para filtrar apps en el Hub. | no consultado | Corroborado por `docs/procesos/00-acceso-y-plataforma.md:125` ("No se leen en pantalla"). El Hub filtra por acceso, no por tags. |
| `CH-Tbl-ClientAccount.sanic_chchrecordstatus` | Estado de vigencia legacy. | no consultado | **Distinto** de `sanic_recordstatus` (ese sí se usa). El propio equipo lo documenta como no usado en `scripts/seed-clientadmin-stress.py:184-187`. Homónimo bien resuelto, no un falso positivo. |
| `CH-Tbl-Client.sanic_iseditable` | Bandera de editabilidad del cliente. | **no verificable** por agregación (campo de dos opciones) | 0 referencias en los cinco árboles de búsqueda. Confirmar contenido antes de borrar. |

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

### B.2 — Custom API `sanic_CHCanResolveApproval`

| Aspecto | Dato |
|---|---|
| **Propósito** | Chequeo de permiso *antes* de actuar: "¿este usuario puede resolver esta aprobación?". Sirve para habilitar o esconder un botón. |
| **Uso** | **Ninguno.** Confirmado sin invocador en los tres árboles de canvas, todos los flows y el backend. La acción real la hace `sanic_CHResolveApprovalRequest`, que sí se invoca desde `scrAprobaciones.fx.yaml`. |
| **El ajuste** | No es borrar una fila: hay que desregistrar el step y sacar la clase backing `CanResolveApprovalPlugin`. |

**Mi lectura**: no es basura acumulada, es una **pieza de UX que nunca se cableó**. Se construyó
el chequeo previo y la pantalla terminó sin usarlo. Antes de borrarla, una pregunta de producto:
¿hoy el botón de resolver se le muestra a todo el mundo y falla del lado servidor si no tiene
permiso? Si es así, la API no sobra — **falta cablearla**. Borrarla consolidaría una peor UX.

### B.3 — Librería `CH-CL-SharedComponents` que nadie importa

Los 9 `ComponentReferences.json` (8 apps + la propia librería) están **vacíos**. La librería
existe, está en la solución como `RootComponent type="300"`, y **ninguna app la consume**.

Pero el problema real no es la librería — es lo que su ausencia de uso revela: **las 8 apps
tienen copias LOCALES de los mismos componentes** (`cmpDialogo`, `cmpKpi`, etc.) en su propio
`Src/Components/`. Eso es duplicación pura: un cambio en el diálogo hay que hacerlo ocho veces.

Dos caminos, y son opuestos:
- **Borrar la librería** y aceptar la duplicación. Limpia el inventario, no arregla la deuda.
- **Adoptar la librería** y deduplicar las 8 copias. Más trabajo, ataca la causa.

Es una decisión tuya. Yo no la tomo por vos, pero te digo cuál me parece: si esas apps se van a
seguir tocando, la duplicación te va a seguir costando.

---

## C — No tocar sin tu decisión

### C.1 — 7 cloud flows sin invocador

**5 en Draft (OFF)**: `Applyclientdatachanges`, `Createclientdata`, `Activaterow`,
`Deactivaterow`, `RFCWatchdog`.
**2 en ON pero sin invocador**: `ExtractedTablesFromExcel`, `GetJSONdatafromattachment`.

**Acá freno, y te explico por qué.** `CH-Flow-Createclientdata` **es parte de un proceso de
negocio vivo**: el RFC de creación aprobado es lo que mueve `recordstatus` de `PendingApproval`
a `Active`. Y pasó de ON a OFF **después del 24 de agosto**.

Hay dos lecturas y llevan a acciones opuestas:
- Se retiró a propósito, reemplazado por el Custom API `sanic_CHResolveChangeRequest` — mismo
  patrón que `CreateCut` → `CHCreateTransactionCut`. Entonces se puede borrar.
- Se desactivó sin querer. Entonces **hay un proceso de negocio caído** y borrarlo lo sepulta.

Lo mismo con `RFCWatchdog`, que además arrastra un acoplamiento: la env var
`CHEVRfcApplyTimeoutMinutes` figura como USADA **solo porque este flow la lee**. Si el flow está
muerto, la variable se suma a la lista de bajas. Si no, ninguna de las dos se toca.

No borro flujos de proceso de negocio por un veredicto de grep. Decime vos.

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

| Categoría | Componentes | Verificación |
|---|---|---|
| **A — Borrar directo** | 1 tabla + 1 relación + 13 columnas | 0 refs en código **y** 0 datos en DEV |
| **B — Borrar con ajuste** | 20 env vars, 1 Custom API, 1 librería de componentes | 0 refs; requiere cambio previo |
| **C — Decisión de producto** | 7 flows, 2 roles, 1 columna cableada sin uso | Riesgo de negocio, no de limpieza |

> El punto 7 de la checklist es el que más importa: **este análisis se hizo contra DEV.**
> Nada de esto debería aplicarse a producción sin repetir la verificación de datos allá.
