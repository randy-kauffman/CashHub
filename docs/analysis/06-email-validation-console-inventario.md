# Inventario: Canvas App "Email Validation Console" (sanic_chappemailvalidationconsole_f3b93)

Fuente: `/home/gmaker/projects/CashHub/solutions/unpacked/apps/CanvasApps/src/sanic_chappemailvalidationconsole_f3b93/`

Metodología: lectura por rangos (offset/limit) y `rg`/`python3` sobre los `.fx.yaml`, nunca lectura completa de archivos >3000 líneas. Cada afirmación cita archivo y línea aproximada.

---

## 1. Inventario de pantallas (Src/)

| Archivo | Líneas | Propósito de negocio |
|---|---|---|
| `Src/App.fx.yaml` | 156 | App-level: define el UDF `EvaluateEV` (líneas 4-133), `App.OnStart` (134-147), tema. |
| `Src/screen CorreosElectronicos.fx.yaml` | 13.821 | Bandeja de entrada manual: lee el buzón compartido directamente vía Graph API (Office365Outlook), permite filtrar/buscar correos, ver adjuntos, y "dar de alta" un correo en Dataverse (`CH-Tbl-Received Emails`) + archivarlo en Outlook. Es el punto de **intake manual** de correos, no consume el pipeline automático. |
| `Src/screen ColaDeProcesamiento.fx.yaml` | 17.960 | Pantalla central: cola de correos pendientes de revisión/respuesta (`CH-Tbl-Received Emails`), ejecución manual de las 23 reglas de validación (motor `EvaluateEV`), materialización de la declaración (Declarations/DeclaredBags/DeclaredChecks/DeclaredDeposits) a partir del Excel adjunto, y envío de la respuesta al cliente. Es la pantalla que concentra casi toda la lógica de negocio de la app (23.777 de las ~32.326 líneas totales de pantallas están acá). |
| `Src/screen TestUDF.fx.yaml` | 389 | Arnés de pruebas unitarias standalone para el UDF `EvaluateEV` (colección local `colTests`, sin datasource real). Pantalla de desarrollo/QA, no de negocio. |

Total líneas de pantallas: **32.326** (App.fx.yaml + 3 screens).

Componente compartido embebido: `pkgs/Components/ExpandMenu.fx.yaml` (11.464 líneas) — ver punto 7.

---

## 2. El motor `EvaluateEV`

- **Definición**: `Src/App.fx.yaml`, como `App.Formulas`, línea **4** (`=EvaluateEV(_op:Text, _iv:Text, _bv:Text):Boolean = IfError(...)`) hasta línea **133** (`);`). El bloque completo de `Formulas:` del archivo ocupa prácticamente todo `App.fx.yaml` (líneas 3-133) — **≈130 líneas**.
- **Qué hace**: un intérprete genérico de operadores de comparación (`equal`, `<`, `<=`, `>`, `>=`, `contains`, `begins with`, `ends with`, `empty`, alias y typos como `greather than`, `empoty`) que recibe `(_op, _iv, _bv)` como texto y soporta comparación de arrays JSON (orden-insensible). Es el "runtime" genérico que interpreta el campo `Validation Operation` de cada regla contra el `ValidationValue` configurado.
- **Cómo se invoca**: llamado por nombre `EvaluateEV(...)` — no hay una función wrapper intermedia.
  - `Src/screen ColaDeProcesamiento.fx.yaml`: **23 invocaciones**, todas dentro del botón oculto `btnValidationRules` (línea 16835), en un `Switch(varValidationRule.Name, "VR-01", ..., "VR-23", ...)` que va de línea **16835 a ~17954** (**≈1.120 líneas**, el `OnSelect` completo de ese botón). Líneas de las llamadas: 16864, 16889, 16901, 16916, 17228, 17240, 17260, 17277, 17310, 17335, 17446, 17484, 17515, 17540, 17552, 17643, 17684, 17696, 17752, 17806, 17830, 17854 (más una en 16916 dentro del caso VR-01... nota: no todas las 23 reglas usan `EvaluateEV` directamente — VR-02, por ejemplo, línea 16880, resuelve con `LookUp` directo sin pasar por `EvaluateEV`, y VR-06 invoca un flujo de Power Automate en su lugar, línea 16931).
  - `Src/screen TestUDF.fx.yaml`: 2 invocaciones — línea **318** (filtro de la galería de tests) y línea **334** (columna "Actual" de cada caso de prueba). Esta pantalla entera depende de `EvaluateEV` y no tiene otro propósito.
- **Tamaño de la cirugía**:
  1. Retirar la definición completa (`App.fx.yaml` líneas 4-133, ~130 líneas).
  2. Retirar/reemplazar el `Switch` de 23 casos en `btnValidationRules` (`screen ColaDeProcesamiento.fx.yaml`, ~1.120 líneas, líneas 16835-17954) por una llamada al Custom API `sanic_CHValidateReceivedEmail` (ya en producción). Esto también implica retirar la lógica de materialización de resultados (`Patch` a `CH-Tbl-ValidationResults`/`CH-Tbl-ValidationRuns`, líneas 17876-17954) si el plugin C# ya la hace server-side — **hay que confirmar contra el código del plugin qué mitad de esa lógica quedó duplicada**.
  3. Eliminar la pantalla completa `screen TestUDF.fx.yaml` (389 líneas) — es un arnés de test del motor que se retira, no tiene otra función.
  4. Nada en `screen CorreosElectronicos.fx.yaml` depende de `EvaluateEV` (0 ocurrencias confirmadas por grep).

En criollo: la cirugía no es "una función que se borra y ya" — es un botón de 1.120 líneas con un switch de 23 casos escrito a mano, más el archivo App.fx.yaml casi completo, más una pantalla entera de testing. Es plata en horas de QA de regresión si no se hace con cuidado.

---

## 3. Datasources (DataSources/)

Archivos de tabla presentes y su `LogicalName` real (confirmado vía JSON, no vía nombre de archivo):

| Archivo | LogicalName Dataverse |
|---|---|
| `CH-Tbl-Bags.json` | `sanic_chtblbags` |
| `CH-Tbl-ClientEmails.json` | `sanic_chtblclientemail` |
| `CH-Tbl-Clients.json` | `sanic_chtblclient` |
| `CH-Tbl-Declarations.json` | `sanic_chtbldeclaration` |
| `CH-Tbl-DeclaredBags.json` | `sanic_chtbldeclaredbags` |
| `CH-Tbl-DeclaredChecks.json` | `sanic_chtbldeclaredchecks` |
| `CH-Tbl-DeclaredDeposits.json` | `sanic_chtbldeclareddeposits` |
| `CH-Tbl-EmailsAttachments.json` | `sanic_chtblemailsattachments` |
| `CH-Tbl-Received Emails.json` | `sanic_chtblreceivedemails` |
| `CH-Tbl-ValidationResults.json` | `sanic_chtblvalidationresults` |
| `CH-Tbl-ValidationRules.json` | `sanic_validationrules` |
| `CH-Tbl-ValidationRuns.json` | `sanic_chtblvalidationruns` |

Más: `Environment Variable Definitions.json`, `Environment Variable Values.json`, `Office365Outlook.json` (conector), `Office365Users.json` (conector), `PowerAppV2->Replytoemail(V3)...json` (flujo), `CH-Flow-ExtractedTablesFromExcel.json` (flujo), `CH-Flow-GetJSONdatafromattachment.json` (flujo). También hay samples sin uso real (`ComboBoxSample.json`, `CustomGallerySample.json`, `DropDownSample.json`).

**Respuesta directa a lo pedido**:
- `sanic_chtblvalidationruns` → **SÍ**, presente.
- `sanic_chtblvalidationresults` → **SÍ**, presente.
- `sanic_validationrules` → **SÍ**, presente.
- `sanic_chtblreceivedemails` → **SÍ**, presente.
- `sanic_chtblemailsattachments` → **SÍ**, presente.
- `sanic_chtblsystemheartbeat` → **NO está**. Confirmado: cero archivos en `DataSources/`, cero menciones de "heartbeat" en todo el árbol de la app (grep case-insensitive sobre todo el directorio, sin resultados).
- `sanic_chtblemailtemplate` → **NO está**. Cero menciones de "emailtemplate" en todo el árbol de la app.

Hay que agregar ambos datasources antes de poder construir el tablero de salud y la administración de plantillas.

---

## 4. Controles clave por pantalla

### `screen CorreosElectronicos.fx.yaml`
- **`Form1`** (línea 23, `As form`, `DataSource: 'CH-Tbl-Received Emails'`) — formulario **oculto** (`Visible: =false`, línea ~93) usado solo como mecanismo de `SubmitForm` para crear el registro en Dataverse; no se ve en pantalla.
- **`Table1`** (línea ~2071, `Table.pcfdataset`) — grilla que lista `colParsedEmails` (colección local poblada leyendo el buzón vía Graph API en `btn_CargarEmail`, no lee `CH-Tbl-Received Emails`). `OnSelect` (línea ~2093) carga adjuntos vía `Office365Outlook.HttpRequest` a `varEmailAttachments`.
- **`Gallery3`** (línea 1903) — galería de adjuntos del correo seleccionado (`Items: =varEmailAttachments`).
- **`btn_CargarEmail`** (~línea 2129) — dispara `Office365Outlook.HttpRequest` contra Graph API para traer correos del buzón y arma `colParsedEmails`.
- **`btn_ConstruirURI`** (~línea 2075) — arma la URL de Graph API con filtros de fecha/remitente.
- **`Icon3`** (línea 2185, ícono "Reload") — dispara la recarga (`Select(btn_CargarEmail)`).
- **`Toggle1`** (línea 2217) — alterna entre carpeta "Inbox" y "Archivo".
- **`CancelButton1`** ("Incluir + Validar Auto", línea ~1966) y **`SubmitButton1`** ("Incluir + Validar Manual", línea ~2005) — dan de alta el correo en `CH-Tbl-Received Emails` vía `SubmitForm(Form1)` con `EmailReviewMethod` en Auto/Manual respectivamente.
- **`ButtonCanvas1`** ("Archivar", ~línea 2170) — mueve el correo a la carpeta de archivado sin crear registro (`Office365Outlook.MoveV2`).
- **`ExpandMenu_1`** (línea 2471) — instancia del componente de menú lateral (ver punto 7).

### `screen ColaDeProcesamiento.fx.yaml`
- **`Table1_1`** (línea 186, `Table.pcfdataset`) — grilla principal de la cola: `Items` (línea ~218) es `Search(Filter('CH-Tbl-Received Emails', EmailReviewStatus = Pendiente || EmailReplyStatus = Pendiente), ...)`. Este es el corazón visual de la "consola de contingencia".
- **`Gallery1`** (línea 1199) — lista las reglas de `colValidationRules` pendientes de ejecutar para el `gvarSelectedVRUN` actual, con botón **`ButtonCanvas2`** ("Ejecutar", ~línea 1287) que dispara `Select(btnValidationRules)`.
- **`Gallery1_1`** (línea 1303) — lista los resultados ya guardados (`colValidatedResults`) para la corrida seleccionada.
- **`ButtonCanvas3`** ("Crear lote de validacion", línea ~793) — `NewForm(Form2); SubmitForm(Form2)` crea un registro en `CH-Tbl-ValidationRuns`.
- **`ButtonCanvas3_1`** ("Correr todas las validaciones", línea ~856) — dispara el flag `varRunAllValidations` para correr el switch completo.
- **`ButtonCanvas4`** ("Guardar resultados", línea ~886) — arma el HTML de respuesta (aprobado/rechazado según `RunResult`) y dispara `SubmitForm('form Declaration')`, es decir, **acá se materializa la Declaration** si la corrida fue Pass.
- **Botón "Responder correo"** (línea ~1173) — invoca el flujo `PowerAppV2->Replytoemail(V3)...` y luego hace `Patch` sobre `CH-Tbl-Received Emails` con `EmailReplyStatus: Respondido`.
- **`btnValidationRules`** (línea 16835) — botón **oculto** (`Visible: =false`, línea ~17953) que contiene el motor completo: switch de 23 reglas, llamadas a `EvaluateEV`, dos llamadas a flujos (VR-06 y GetJSON), y los `Patch` finales a `ValidationResults`/`ValidationRuns`/`Received Emails`.
- **Bloque `"'container Forms'"`** (línea 13581 a 16766, **≈3.185 líneas**) — cuatro formularios ocultos: `'form Declaration'` (`CH-Tbl-Declarations`, línea 13598), `Form2` (`CH-Tbl-ValidationRuns`, línea 1394), y dos más para `CH-Tbl-DeclaredBags` (línea 14366), `CH-Tbl-DeclaredDeposits` (línea 15172), `CH-Tbl-DeclaredChecks` (línea 15964) — parsean el JSON extraído del Excel adjunto y materializan los registros de la declaración. **Esta lógica de materialización NO es parte del motor de validación y debe preservarse** independientemente de qué se haga con `EvaluateEV`.
- **`ExpandMenu_2`** (línea 2236) — instancia del mismo componente de menú lateral.
- **No existen** botones de "Aprobar manualmente", "Denegar manualmente" ni "Reprocesar" — confirmado por grep case-insensitive sobre `aprob|denega|reproces|override|reintent|manual|forzar` en textos de botones: sin resultados. **Esto hay que construirlo desde cero** para el punto (1) del rediseño (override manual).

---

## 5. Flujos invocados (`.Run(`)

Los 3 únicos `.Run(` de toda la app, todos en `screen ColaDeProcesamiento.fx.yaml`:

1. **`'CH-Flow-GetJSONdatafromattachment'.Run(varAttachment).jsonstring`** — línea **273**, dentro del `OnSelect` de `Table1_1` (al seleccionar un correo de la cola). Extrae el JSON del adjunto para poblar `JSONData`.
2. **`'PowerAppV2->Replytoemail(V3),RespondtoaPowerApporflow'.Run(varOutgoingEmail, gvarSelectedEmail.ReceivedEmailOutlookUniqueID, varBuzonCorreo)`** — línea **1113**, dentro del botón "Responder correo" (~línea 1108-1173). Envía la respuesta HTML al cliente y, si `status = "Ok"`, hace `Patch` sobre `CH-Tbl-Received Emails` marcando `EmailReplyStatus: Respondido`.
3. **`'CH-Flow-ExtractedTablesFromExcel'.Run(...)`** — línea **16931**, dentro del caso `"VR-06"` del switch de `btnValidationRules`. Extrae las tablas del Excel para validar estructura/contenido.

`screen CorreosElectronicos.fx.yaml` y `screen TestUDF.fx.yaml`: **0 invocaciones** `.Run(` confirmadas por grep.

---

## 6. App.OnStart / App.Formulas

`Src/App.fx.yaml`, bloque `OnStart` (líneas 134-147):
- `Set(varBuzonCorreo, ...)` — resuelve la dirección del buzón compartido desde Environment Variable Values, con fallback a Definitions (línea 137).
- `Set(varIDFolderArchivado, "AAMk...")` — **ID de carpeta de Outlook hardcodeado como string literal**, línea 139, con un comentario `//LookUp(...)` al lado indicando que en algún momento se pensó leerlo de una Environment Variable y se dejó hardcodeado. Nota: `screen CorreosElectronicos.fx.yaml` en su propio `OnVisible` (línea 6) SÍ lo resuelve correctamente vía `LookUp` sobre Environment Variables — hay **inconsistencia entre App.OnStart y el OnVisible de la pantalla** sobre cómo se obtiene este valor.
- `Set(UsuarioOffice, Office365Users.MyProfileV2())` (línea 140) y `Set(varHoy, Today())` (línea 141).
- `ClearCollect(colExpandMenu, Table(...))` (líneas 142-146) — arma las 2 entradas del menú lateral ("Emails" → `screen CorreosElectronicos`, "Cola de procesamiento" → `screen ColaDeProcesamiento").
- `ClearCollect(colValidationRules, Filter('CH-Tbl-ValidationRules', IsActive=true))` (línea 147).
- **No hay detección de rol ni de grupo de seguridad.** Grep de `UsuarioOffice|varRol|AAD|SecurityGroup|Office365Groups` en App.fx.yaml y en ambas pantallas de negocio: la variable `UsuarioOffice` se setea pero **nunca se vuelve a leer** en ninguna condición de negocio o `Visible`/`DisplayMode`. Las únicas coincidencias de "AADLogo"/"SecurityGroup" son nombres de íconos en un selector de íconos (no lógica de permisos). Esto confirma para esta app el mismo hallazgo ya documentado a nivel de síntesis (`docs/analysis/00-sintesis.md`, hallazgo #2): la app no verifica rol.

---

## 7. Componentes compartidos

- **No usa** `sanic_chclsharedcomponents` (la librería de componentes propia de CashHub) — cero menciones en todo el árbol de la app.
- **Sí usa** un componente de PowerCAT (community component library de Microsoft): `ExpandMenu`, referenciado en `ComponentReferences.json` (`LibraryLogicalName: cat_powercatcomponentlibrary_0be3a`) e instanciado como `ExpandMenu_1` (CorreosElectronicos, línea 2471) y `ExpandMenu_2` (ColaDeProcesamiento, línea 2236). El componente completo vive embebido localmente en `pkgs/Components/ExpandMenu.fx.yaml` (**11.464 líneas**) y `pkgs/Components/ExpandMenu.json`.
- No hay menú/header propio construido a mano: toda la navegación lateral depende de este componente externo + la colección `colExpandMenu` armada en `App.OnStart`.
- Implicación para el rediseño: si el resto del portafolio de apps de CashHub migró (o va a migrar) a `sanic_chclsharedcomponents`, esta app queda **desalineada** — usa una dependencia externa distinta (PowerCAT) en lugar del estándar interno.

---

## 8. Lógica de escritura (`Patch(`)

| Archivo | Línea | Tabla destino | Contexto |
|---|---|---|---|
| `CorreosElectronicos.fx.yaml` | 51 | `CH-Tbl-EmailsAttachments` | `Form1.OnSuccess` — por cada adjunto descargado del correo, crea un registro con el `FileContent` (blob), tamaño, extensión. |
| `ColaDeProcesamiento.fx.yaml` | 1155 | `CH-Tbl-Received Emails` | Tras `.Run()` exitoso del flujo de respuesta — marca `EmailReplyStatus: Respondido` + fechas/IDs del mensaje de respuesta. |
| `ColaDeProcesamiento.fx.yaml` | 16749 | `CH-Tbl-ValidationRuns` (a confirmar contexto exacto — dentro del bloque de creación de corrida) | — |
| `ColaDeProcesamiento.fx.yaml` | 17876 | `CH-Tbl-ValidationResults` | Dentro de `btnValidationRules` — guarda el resultado (`Pass`/`Fail`) de la regla evaluada, con duración en ms. |
| `ColaDeProcesamiento.fx.yaml` | 17890 | `colValidatedResults` (colección local, no Dataverse) | Espejo local del Patch anterior, para refrescar la UI sin esperar el `Refresh()`. |
| `ColaDeProcesamiento.fx.yaml` | 17908 | `CH-Tbl-ValidationRuns` | Si `EVResult = false` y la corrida no tenía resultado aún → `RunResult: Fail`, `RunEnd: Now()`. |
| `ColaDeProcesamiento.fx.yaml` | 17918 | `CH-Tbl-Received Emails` | En el mismo bloque anterior → `EmailReviewStatus: Denegado`. |
| `ColaDeProcesamiento.fx.yaml` | 17934 | `CH-Tbl-ValidationRuns` | Si todas las reglas pasaron → `RunResult: Pass`, `RunEnd: Now()`. |

Además, vía `SubmitForm` (no `Patch` directo, pero también escritura): `Form1` → `CH-Tbl-Received Emails` (alta manual de correo); `Form2` → `CH-Tbl-ValidationRuns` (alta de corrida); `'form Declaration'` → `CH-Tbl-Declarations`; formularios ocultos adicionales → `CH-Tbl-DeclaredBags`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-DeclaredChecks` (materialización de la declaración tras validación exitosa).

**Comportamiento a preservar**: el ciclo completo "correo pendiente → ejecutar reglas → guardar resultado por regla → si todas pasan, cerrar corrida en Pass y materializar Declaration+Bags+Checks+Deposits → responder al cliente; si falla una, cerrar en Fail y marcar el correo Denegado". El plugin C# en producción probablemente ya hace la evaluación de reglas y el guardado de resultados server-side — falta confirmar si también materializa la Declaration o si eso sigue siendo responsabilidad de esta app (los Patches de líneas 17876-17934 y los `SubmitForm` de los 4 formularios podrían quedar duplicados o podrían ser la única fuente de esa materialización; **no se pudo confirmar sin leer el código del plugin C#**, que está fuera del alcance de esta exploración).

---

## Notas de honestidad metodológica

- No se leyó línea por línea el bloque completo de "container Forms" (13581-16766, ~3.185 líneas) por su tamaño; se caracterizó por sus `DataSource:` y estructura de encabezado. Si el rediseño toca esa lógica de materialización, se recomienda una exploración dedicada a ese bloque.
- No se inspeccionó el código C# del Custom API `sanic_CHValidateReceivedEmail` — la comparación de qué reglas exactas ya cubre el plugin vs. qué queda huérfano en el switch de 23 casos requiere leer ese plugin, no estaba en el árbol de la canvas app.
- Las 23 líneas de `EvaluateEV(` en `screen ColaDeProcesamiento.fx.yaml` fueron listadas por grep; no se abrió cada uno de los 23 casos VR-01..VR-23 individualmente (se abrieron VR-01 a VR-06 como muestra representativa). Se recomienda no asumir que todos siguen el mismo patrón sin muestreo adicional si el reemplazo se hace caso por caso.
