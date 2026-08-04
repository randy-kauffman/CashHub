# Patrones técnicos de las canvas apps — CashHub

> Fuente: [`docs/analysis/03-apps-operativas.md`](../analysis/03-apps-operativas.md), [`04-apps-administrativas.md`](../analysis/04-apps-administrativas.md), y lectura directa de `solutions/unpacked/apps/CanvasApps/src/*/Src/*.fx.yaml`.

Objetivo de este documento: antes de tocar cualquiera de las 8 canvas apps, entender qué patrones se repiten entre ellas y cuáles no — para no asumir consistencia que no existe.

## 1. Detección de usuario / rol

**Patrón dominante**: resolver el usuario actual contra `systemuser` por email, sin verificar rol ni grupo.

```powerfx
Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email))
```

Presente en `App.OnStart` de **Reception** (no, ver excepción abajo), **Recounting** y **Supervisor**. `Users` es el datasource nativo `systemuser` de Dataverse.

| App | ¿Resuelve `gvarUser`? | ¿Verifica grupo AAD? |
|---|---|---|
| Reception | No hay `Set(gvarUser, ...)` en `App.fx.yaml` — ninguna detección de identidad | No |
| Recounting | Sí (`Screen1.OnVisible` lo usa para buscar sesión abierta del cajero) | No |
| Supervisor | Sí, en `App.OnStart` | No |
| Digitization | No — `App.fx.yaml` de 11 líneas, sin `OnStart` | No |
| Deposits Tracker | No — sin `OnStart`, sin `App.Formulas` | No |
| Email Validation Console | No (usa `Office365Users.MyProfileV2()` para otro propósito, no como gate de acceso) | No |
| Client Administration (canvas) | Sí (`gvarUser`) | **Sí** — vía `Office365Groups.ListGroupMembers` sobre `sanic_CHTblClientDataChangePermission.AADGroupID` (por GUID, no por nombre de variable de entorno) |
| Hub | No usa `gvarUser` para esto | **Sí** — filtra `sanic_CHTblAppCatalog` por `AAD Group Id` vía `Office365Groups.ListGroupMembers` |

**Hallazgo transversal**: ninguna de las 8 apps referencia las variables de entorno `sanic_CHEVAADGroup_*` de Core dentro de su código Power Fx. Donde SÍ hay chequeo de grupo (Hub, Client Administration), es contra un **GUID de grupo almacenado en una tabla de datos** (`sanic_CHTblAppCatalog`, `sanic_CHTblClientDataChangePermission`), no contra las variables de entorno dedicadas a ese propósito. Las 6 apps restantes no tienen ningún control de acceso en Power Fx: la seguridad de "quién puede usar esta app" se apoya enteramente en a quién se comparte la app y en los roles de seguridad Dataverse subyacentes — ver el gap de roles vacíos documentado en [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md).

## 2. Navegación / menú

Dos patrones distintos, sin unificar:

- **Hardcodeado por pantalla** (Reception): la tabla `Table({Icon:..., Label:..., Screen:...})` del menú lateral está **duplicada literalmente en cada pantalla** (`CreacionLotesRecepcion` y `AdministracionDeLotes`), no centralizada.
- **Centralizado en `App.Formulas`** (Supervisor): única app de las analizadas que define `Set(gvarNavItems, Table(...))` en `App.OnStart`/`App.Formulas` y todas las pantallas leen de ahí. Ejemplo real:
  ```powerfx
  Set(gvarNavItems, Table(
    {Icon:"ProductRelease", Label:"Aprobaciones", Screen:'Screen Approvals', ...},
    {Icon:"ProductVariant", Label:"Localizacion de tulas", Screen:'Screen BagLocation', ...},
    {Icon:"ExploreData", Label:"Resumen de operaciones", Screen:'Screen OperationSummary', ...},
    {Icon:"StackIndicator", Label:"Administracion de cheques", Screen:'Screen ChecksSummary', ...}
  ));
  ```
- **Sin menú / navegación por pestañas o visibilidad condicional** (Recounting: pantalla única real; Digitization: `TabList1` con 3 pestañas dentro de `Screen1`, cero `Navigate()`; Deposits Tracker: visibilidad condicional según `gvarSelectedDetail`, cero `Navigate()`).

Si se decide adoptar un patrón único, `gvarNavItems` de Supervisor es el candidato ya probado — replicarlo evitaría la duplicación de Reception.

## 3. Componentes compartidos

Existe una biblioteca dedicada: `sanic_chclsharedcomponents_85fca` (`solutions/unpacked/apps/CanvasApps/src/sanic_chclsharedcomponents_85fca/`), con 2 componentes:

- **`cmpAppHeader`**: `CustomProperties: []` — no expone ninguna propiedad de entrada/salida. Un solo `Rectangle1` sin `Fill` posicionado fuera del área visible práctica. Es un placeholder sin terminar, no un header funcional.
- **`cmpTheme`**: expone una propiedad de salida `Theme` (Record, `Hidden:true`) con tokens de diseño fijos (paleta morada, spacing, radius, fuente). Sin inputs, sin soporte claro/oscuro dinámico.

**Ninguna de las 8 canvas apps de negocio usa esta biblioteca** (`ComponentReferences.json` vacío o sin referencia en las 8). En su lugar, cada app mantiene sus propios componentes locales duplicados en `pkgs/Components/` de su propia carpeta: `Dialog`, `ExpandMenu`/`Header`/`Panel` reaparecen copiados de forma independiente en Reception, Recounting y Supervisor. Todas sí usan `cat_powercatcomponentlibrary_0be3a` (PowerCAT, librería pública de la comunidad — no es código propio de CashHub): `Fluent Details List`, `Fluent Command Bar`, `Combobox.pcfdataset`, `ExpandMenu`.

**Implicación de desarrollo**: un cambio visual (por ejemplo, el color primario o el layout del header) hoy requiere tocar cada app por separado, porque ninguna consume `sanic_chclsharedcomponents`. Completar `cmpAppHeader` y darle uso real sería el primer paso para des-duplicar.

## 4. Invocación de flujos Power Automate (`.Run`)

No todas las apps invocan flujos. Inventario completo de llamadas `.Run(` encontradas:

| App | Flujo invocado | Disparador |
|---|---|---|
| Recounting | `'CH-Flow-ProcessXMLFiles'.Run(gvarDatosConteo.ID_Sesion, gvarDatosConteo.Serial_Maquina)` | Botón "Obtener recuento de la máquina contadora" |
| Digitization | `'CH-Flow-DownloadData'.Run(JSON(colTx, JSONFormat.IncludeBinaryData), varLoteSelected.Name).archivosalida` | Botón "Descargar lote" |
| Email Validation Console | `'CH-Flow-GetJSONdatafromattachment'.Run(AttachmentGUID)` | Al seleccionar un correo en la cola (preview) |
| Email Validation Console | `'CH-Flow-ExtractedTablesFromExcel'.Run(AttachmentGUID)` | Dentro de la regla VR-06 (evaluación real) |
| Email Validation Console | `PowerAppV2->Replytoemail(V3)` (`CH-Flow-Replytoclientemail`) | Botón "Responder correo" |

**Apps que no invocan ningún flujo** (`0 .Run(` confirmado): Reception, Supervisor, Deposits Tracker, Client Administration (canvas). Esto es consistente con el hallazgo de arquitectura: la orquestación de negocio pesada vive en Power Fx dentro de las apps (Supervisor calcula faltante/sobrante en Power Fx puro, Client Administration arma el RFC en Power Fx puro), y los flujos que sí se invocan son utilitarios puntuales (descarga de archivo, lectura de XML de máquina, extracción de Excel), no orquestadores de proceso.

**Rareza a tener en cuenta**: Email Validation Console invoca **dos flujos distintos con el mismo parámetro** (`AttachmentGUID`) para lo que parece la misma operación (`GetJSONdatafromattachment` para preview, `ExtractedTablesFromExcel` para evaluación real) — no se pudo confirmar la diferencia interna sin acceso a ambos flujos en profundidad; podría ser deuda técnica (dos implementaciones de lo mismo) o tener una diferencia funcional real no visible desde la app.

## 5. Colecciones y estado

Patrones observados, sin estandarizar entre apps:

- **Colecciones tipadas al vacío en `OnStart`**: Reception inicializa y limpia `colPlanillas`/`colTulas` en `App.OnStart` solo para fijar el tipo de columna de una colección vacía (truco típico de Power Apps cuando no hay filas de origen para inferir el esquema).
- **Variables de contexto con typos que sobreviven en producción**: `varSelectedReceptionBacth` (Reception, "Bacth" en vez de "Batch") — no se puede corregir sin tocar todas las referencias, análogo a los typos de `LogicalName` en Core que no se pueden renombrar sin recrear el campo.
- **Variables globales de negocio sin inicialización explícita**: en Digitization y Deposits Tracker (ambas sin `App.OnStart`), las variables se crean implícitamente al primer uso — sin snapshot centralizado de qué variables existen, hay que leer toda la pantalla para inventariarlas.
- **Doble ejecución de un mismo bloque de lógica**: en Recounting, `Screen1.OnVisible` tiene el bloque de "revisar aprobaciones pendientes del cajero" **duplicado dos veces consecutivas**, la segunda marcada con el comentario `// CODIGO NUEVO: 17/06/2026` — parche que no eliminó el código anterior.
- **JSON como mecanismo de estado entre apps**: el circuito `CH-Tbl-ApprovalRequests.RequestResources` (texto, contiene JSON) es el puente real entre Reception/Recounting (generan la solicitud con el estado previo embebido) y Supervisor (único punto que hace `ParseJSON(...)` para materializar el cambio o revertir). No es una colección en memoria sino persistencia intermedia en Dataverse — vale tenerlo presente porque cualquier cambio de forma en ese JSON rompe silenciosamente al otro lado si no se actualizan las tres apps a la vez.

## 6. Tabla app × tablas Dataverse × flujos invocados

| App | Tablas Dataverse principales (lectura y/o escritura) | Flujos invocados |
|---|---|---|
| **Reception** (`sanic_chappreception_9ff29`) | `CH-Tbl-ReceptionBatches`, `CH-Tbl-Receptions`, `CH-Tbl-Bags`, `CH-Tbl-ApprovalRequests`, `CH-Tbl-ClientBranches`, `CH-Tbl-Clients`, `CH-Tbl-TransportCompanies` | Ninguno |
| **Recounting** (`sanic_chapprecounting_54962`) | `CH-Tbl-Bags`, `CH-Tbl-Checks`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-DeclaredChecks`, `CH-Tbl-DepositsRecountings`, `CH-Tbl-RecountingSessions`, `CH-Tbl-CountedMoneyDetails`, `CH-Tbl-ApprovalRequests`, `CH-Tbl-DeclaredBags`, `CH-Tbl-MoneyDenominations`, `CH-Tbl-Receptions`, `CH-Tbl-Transactions`, `CH-Tbl-RecountingSessionDetails`, `CH-Tbl-CountingDesks`, `CH-Tbl-XMLReadings`/`Details` | `CH-Flow-ProcessXMLFiles` |
| **Supervisor** (`sanic_chappsupervisor_75678`) | `CH-Tbl-ApprovalRequests`, `CH-Tbl-Bags`, `CH-Tbl-Receptions`, `CH-Tbl-RecountingSessions`, `CH-Tbl-DeclaredBags`, `CH-Tbl-RecountingSessionDetails`, `CH-Tbl-Transactions`, `CH-Tbl-Checks`, `CH-Tbl-DepositsRecountings` | Ninguno |
| **Digitization** (`sanic_chappdigitization_278aa`) | `CH-Tbl-Transactions`, `CH-Tbl-TransactionBatches`, `CH-Tbl-Checks` (solo lectura), `CH-Tbl-DepositsRecountings` (indirecto) | `CH-Flow-DownloadData` |
| **Deposits Tracker** (`sanic_chappdepositstracker_2b2c3`) | `CH-Tbl-ReceptionBatches`, `CH-Tbl-Receptions`, `CH-Tbl-Bags`, `CH-Tbl-DepositsRecountings`, `CH-Tbl-Checks`, `CH-Tbl-DeclaredBags`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-Clients`, `CH-Tbl-RecountingSessions`, `Users` — **todas de solo lectura, cero `Patch`** | Ninguno |
| **Email Validation Console** (`sanic_chappemailvalidationconsole_f3b93`) | `CH-Tbl-ReceivedEmails`, `CH-Tbl-EmailsAttachments`, `CH-Tbl-ValidationRules`, `CH-Tbl-ValidationRuns`, `CH-Tbl-ValidationResults`, `CH-Tbl-ClientEmails`, `CH-Tbl-Declarations`, `CH-Tbl-DeclaredBags`, `CH-Tbl-DeclaredDeposits`, `CH-Tbl-DeclaredChecks` | `CH-Flow-GetJSONdatafromattachment`, `CH-Flow-ExtractedTablesFromExcel`, `CH-Flow-Replytoclientemail` |
| **Client Administration** (canvas, `sanic_chappclientadministration_fd97f`) | `CH-Tbl-Clients`, `CH-Tbl-ClientBranches`, `CH-Tbl-ClientEmails` (solo lectura); `CH-Tbl-ClientDataChangeRequests`, `CH-Tbl-ClientDataChangeRequestLines` (escritura); `CH-Tbl-ClientDataChangePermissions`, `CH-Tbl-ClientTableLogicalFieldNames` (solo lectura) | Ninguno (solo conector Office 365 Groups) |
| **Hub** (`sanic_chapphub_2038c`) | `CH-Tbl-AppCatalogs` (solo lectura) | Ninguno |

## 7. Ver también

- [01-arquitectura.md](01-arquitectura.md) §3 — por qué la validación de correos vive en Power Fx (Email Validation Console) y no en los flujos VR0x.
- [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md) — ítems de calidad derivados de los hallazgos de este documento (código debug, duplicación de componentes, typos).
