# Deep-dive: Administración de Clientes (Client Administration) — CashHub

> Nota de alcance: este documento profundiza específicamente el módulo de administración de
> clientes (altas de cliente, sucursales, correos autorizados, y el modelo de aprobación RFC)
> ya relevado en `04-apps-administrativas.md` (sección 4) y `02-flows.md` (secciones 3.13-3.19).
> Acá se cita evidencia adicional leída directamente de los fuentes que esos documentos no
> profundizaron: Roles de seguridad, Entity.xml de las 9 tablas del dominio, los 5 web
> resources del lado model-driven (2 no estaban documentados antes), y el contenido línea por
> línea de `screen ChangeRequestDetails.fx.yaml`, `screen RFC.fx.yaml`, `screen Clients.fx.yaml`
> y `screen ClientInformation.fx.yaml`.
>
> Convención: toda afirmación cita archivo + línea aproximada. Donde no se pudo confirmar desde
> el código exportado, se marca explícitamente **"a verificar en ambiente"**.

---

## 0. Inventario de tablas del dominio

| Tabla (nombre lógico) | Rol en el modelo | Ownership |
|---|---|---|
| `sanic_chtblclient` | Cliente (maestro) | UserOwned |
| `sanic_chtblclientbranch` | Sucursal del cliente | UserOwned |
| `sanic_chtblclientemail` | Correo autorizado del cliente | UserOwned |
| `sanic_chtblclientdatachangerequest` | RFC — cabecera | UserOwned |
| `sanic_chtblclientdatachangerequestline` | RFC — línea de cambio (1 por campo) | UserOwned |
| `sanic_chtblclientdatachangepermission` | Matriz Contributor/Approver por tabla + grupo AAD | UserOwned |
| `sanic_chtblclientdatachangesaudittrail` | Bitácora de aplicación de RFC | UserOwned |
| `sanic_chtblclienttablelogicalfieldname` | Catálogo de campos editables por tabla | UserOwned |
| `sanic_chtblclientdeposithistory` | Cuentas/historial de depósito del cliente | UserOwned |

Fuente: `solutions/unpacked/core/Entities/*/Entity.xml`, tag `<OwnershipTypeMask>` (confirmado
`UserOwned` en `sanic_CHTblClient/Entity.xml`). Ser `UserOwned` importa para la sección 2: implica
que el modelo de seguridad estándar de Dataverse (privilegios Create/Read/Write/Delete/
Append/AppendTo/Assign/Share por rol, en niveles Ninguno/Básico/Local/Profundo/Global) **aplica
de lleno** — no es una tabla de configuración de acceso libre.

---

## 1. Mapa exacto del funcionamiento actual

### 1.1 Pantallas de la Canvas App (`sanic_chappclientadministration_fd97f`)

| Pantalla | Archivo | Líneas | Función |
|---|---|---|---|
| `screen Splash` | `Src/screen Splash.fx.yaml` | 51 | Si viene `Param("RFC")` en la URL, resuelve el RFC y navega directo a `ChangeRequestDetails`; si no, navega a `Clients`. |
| `screen Clients` | `Src/screen Clients.fx.yaml` | 13.981 | Listado/CRUD de clientes. Crear/editar SIEMPRE genera un RFC (nunca `Patch` directo sobre `CH-Tbl-Clients`). |
| `screen ClientInformation` | `Src/screen ClientInformation.fx.yaml` | 4.604 | Ficha de cliente con sub-grillas de Sucursales y Correos. Altas/ediciones ahí generan RFC igual que en Clients, pero **por un form separado** (ver 1.2.1). |
| `screen RFC` | `Src/screen RFC.fx.yaml` | 11.726 | Listado de RFCs (toggle pendientes/todas). Es **solo un dashboard**: no tiene `Patch(`, ni botones Aprobar/Rechazar, ni lógica de creación — confirmado por `rg -n "Patch\("` sin resultados en todo el archivo. Al click en un registro navega a `ChangeRequestDetails` (línea ~11536). |
| `screen ChangeRequestDetails` | `Src/screen ChangeRequestDetails.fx.yaml` | 2.610 | Pantalla central: cabecera del RFC + grilla de líneas + botones Aprobar/Rechazar/Enviar/Cancelar. |

### 1.2 Ciclo de vida del RFC — estados confirmados por Entity.xml

Cabecera (`sanic_chtblclientdatachangerequest`, campo **`cr140_changestatus`** — ver 2.4 sobre el
publisher de este campo):

```
796490000 Draft → 796490001 Submited → 796490002 Approved → 796490004 Applied
                                      ↳ 796490003 Rejected  ↳ 796490005 Failed
                  (159460001 Canceled, disponible desde Draft)
```
Fuente: `solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequest/Entity.xml`,
bloque `<optionset Name="cr140_sanic_chtblclientdatachangerequest_cr140_changestatus">`.
Nótese que "Canceled" usa el valor `159460001` — del rango de opciones del publisher `sanic`
(prefijo `15946`), mientras el resto de la lista usa el rango `796490xxx`, ajeno a ambos
publishers de la solución (`sanic`/`cr140`) — tercera evidencia de que este picklist se armó
fuera del flujo normal de personalización.

Línea (`sanic_chtblclientdatachangerequestline`, campo `sanic_ChangeStatus`, propio de `sanic`):
`159460000 Draft, 159460001 Submitted, 159460002 Approved, 159460003 Denied, 159460004 Applied,
159460005 Failed, 159460006 Canceled` (mismo archivo Entity.xml de la línea).

Tipo de RFC (`sanic_Type`): `159460000 Update, 159460001 Deactivate, 159460002 Create,
159460003 Activate` — coincide con lo ya inferido en `02-flows.md` desde los filtros OData de
los webhooks, ahora confirmado directamente contra el optionset.

#### 1.2.1 Alta completa de un cliente = 3 RFCs independientes, no 1

Evidencia (agente de exploración sobre `screen Clients.fx.yaml` y `screen ClientInformation.fx.yaml`):
el alta de **Cliente** usa `formRFC_Cliente` (Clients.fx.yaml, líneas 7-93) que crea 1 cabecera +
3 líneas (Nombre/CIFBCO/CIFCOM) en el mismo `Patch`. Pero **Sucursal** y **Correo** se dan de alta
desde `formRFC_Cliente_1` en `ClientInformation.fx.yaml` (líneas 5-169), con un
`Switch(varTableDisplayName, "Sucursales", ..., "Emails", ...)` mutuamente excluyente (líneas
51-126) que dispara `NewForm(formRFC_Cliente_1); SubmitForm(formRFC_Cliente_1)` una vez por
submit (líneas ~4036-4042). Es decir: **dar de alta un cliente con 1 correo y 1 sucursal genera
3 cabeceras de RFC separadas**, cada una aprobable en un momento distinto por un Approver
potencialmente distinto (Banca de Empresas para Cliente/Correo, Bóveda para Sucursal) — no hay
forma de aprobar "el alta completa" como una unidad.

### 1.3 Detección de rol / grupos AAD

`App.OnStart` (`04-apps-administrativas.md`, líneas 234-238, ya documentado):
```
Clear(colClientDataPermissions);
ForAll('CH-Tbl-ClientDataChangePermissions' As Permission,
    If(!IsBlank(LookUp(Office365Groups.ListGroupMembers(Permission.AADGroupID).value, userPrincipalName = User().Email)),
       Collect(colClientDataPermissions, Permission)));
```
Confirmado en este deep-dive: `sanic_AADGroupID` (tabla `sanic_CHTblClientDataChangePermission`)
es un campo `nvarchar(100)` **de texto libre**, requerido, sin relación con Environment
Variables ni con ningún catálogo de grupos AAD (`solutions/unpacked/core/Entities/
sanic_CHTblClientDataChangePermission/Entity.xml`, atributo `sanic_AADGroupID`). No es
"hardcodeado en código" ni "variable de entorno": es **dato de configuración en una tabla
Dataverse**, editable solo por quien tenga privilegio de escritura sobre esa tabla — privilegio
que, como se ve en la sección 2.1, **ningún rol de seguridad exportado otorga**.

### 1.4 Los 6 flujos webhook del subsistema RFC

Ya inventariados en `02-flows.md` §3.13-3.19. Resumen de disparadores (confirmado contra el
JSON de cada flujo, bloque `triggers`):

| Flujo | Filtro OData del webhook |
|---|---|
| `Notifyreviewers` | `cr140_changestatus eq 796490001` (Submitted) |
| `Activaterow` | `cr140_changestatus eq 796490002 and sanic_type eq 159460003` |
| `Deactivaterow` | `cr140_changestatus eq 796490002 and sanic_type eq 159460001` |
| `Createclientdata` | `cr140_changestatus eq 796490002 and sanic_type eq 159460002` |
| `Applyclientdatachanges` | `cr140_changestatus eq 796490002 and sanic_type eq 159460000` |
| `NotifywhenRFCimplementationfinish` | `cr140_changestatus eq 796490003 or 796490004 or 796490005` |

### 1.5 Model-driven app y web resources — 5 scripts, no 1

`AppModule.xml`/`AppModuleSiteMap.xml` ya documentados en `04-apps-administrativas.md` §8. Este
deep-dive encontró que el paquete de web resources del lado model-driven tiene **5 scripts**,
no solo el `sanic_chwebrproposechange.js` ya conocido:

| Web resource | Función pretendida | Estado real |
|---|---|---|
| `sanic_chwebrproposechange.js` (64 líneas) | Abrir Quick Create de RFC prellenado desde un botón de Command Bar | Ver 2.6 — no se encontró wiring a ningún botón real. |
| `sanic_chwebrcrlinefilter.js` (72 líneas) | Filtrar el lookup "Field Definition" del Quick Create de línea a solo los campos permitidos (`sanic_isallowed`) de la tabla objetivo del RFC | Cargado en el form pero **nunca conectado a ningún evento** — ver 2.5. |
| `sanic_chwebrcrlineprefilloldvalue.js` (119 líneas) | Prellenar `sanic_oldvalue` leyendo el valor actual del registro objetivo | Cargado en el form pero **nunca conectado a ningún evento** — ver 2.5. |
| `sanic_chwebrcrlineduplicateguard.js` (101 líneas) | Impedir 2 líneas para el mismo campo en el mismo RFC + exigir RFC padre en Draft | Ni siquiera está en `formLibraries` del Quick Create de línea. |
| `sanic_chwebrcrlinesaveorchestrator.js` (105 líneas) | Lógica casi idéntica a `crlineduplicateguard.js` (duplicado/copia) | Cargado, pero su único evento `onload` está **deshabilitado** — ver 2.5. |
| `sanic_chwebrcrlinetest.js` (11 líneas) | — no analizado en detalle, nombre sugiere script de prueba | — |

---

## 2. Debilidades de SEGURIDAD

### 2.1 Ningún rol de seguridad exportado otorga privilegios sobre las 9 tablas del dominio

Se inspeccionaron los 6 roles de seguridad de la solución (`solutions/unpacked/core/Roles/*.xml`:
`CH-Role`, `CH-SR-AnalyticsRead`, `CH-SR-Counter`, `CH-SR-CountingSupervisor`, `CH-SR-OpsAdmin`,
`CH-SR-Receptionist`) con:

```
rg -io "prv[a-z]*sanic_[a-z0-9_]*" solutions/unpacked/core/Roles/*.xml | sort -u
```

Resultado: hay privilegios `sanic_CHTbl*` para **Bags, Checks, CountedMoneyDetails,
CountingDesk, Declaration, DeclaredBags/Checks/Deposits, DepositsRecounting, Locations,
MoneyDenomination, ReceptionBatch, Receptions, RecountingSession, Transactions,
TransportCompany, AppCatalog y ApprovalRequest** (esta última es una tabla de aprobación
genérica del subsistema de Bóveda/Recuento, distinta de `sanic_chtblclientdatachangerequest`).
**Cero coincidencias** para `sanic_chtblclient`, `sanic_chtblclientbranch`,
`sanic_chtblclientemail`, `sanic_chtblclientdatachangerequest`,
`sanic_chtblclientdatachangerequestline`, `sanic_chtblclientdatachangepermission`,
`sanic_chtblclientdatachangesaudittrail`, `sanic_chtblclienttablelogicalfieldname` ni
`sanic_chtblclientdeposithistory` en **ninguno** de los 6 roles.

**Esto es más severo que el "18/38" mencionado como referencia**: no es que falten privilegios
puntuales dentro de un rol — es que **ninguno de los 6 roles exportados en esta solución incluye
siquiera una fila para estas 9 tablas**. Dado que son `UserOwned` (sección 0), sin un rol que
otorgue como mínimo `prvReadsanic_CHTblClient` a nivel Global/Organización, un usuario normal no
puede ver ni editar estas tablas por más que la Canvas App se lo muestre en pantalla — el acceso
real hoy depende necesariamente de: (a) un rol como System Administrator/Customizer (privilegios
implícitos globales, no exportable/customizable, por eso no aparece en `Roles/`), o (b) un rol de
seguridad que exista en el ambiente pero no forme parte de este paquete de solución exportado.
**A verificar en ambiente**: cuál rol real tienen asignados los usuarios de Banca de Empresas y
Bóveda hoy — si es System Administrator, es un problema de sobre-privilegio, no solo de
gobernanza de tablas.

### 2.2 Auto-aprobación: posible, y la app tiene el patrón correcto pero no lo aplica acá

`screen ChangeRequestDetails.fx.yaml`, botones Aprobar/Rechazar:

```yaml
# línea 110 (Aprobar) y línea 150 (Rechazar)
Visible: =gvarSelectedChangeRequest.ChangeStatus = 'ChangeStatus (CH-Tbl-ClientDataChangeRequests)'.Submited
    && !IsBlank(LookUp(colClientDataPermissions, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver
        && Name = gvarSelectedChangeRequest.TargetTable))
```

Esta condición solo verifica **rol** (¿el usuario actual tiene permiso Approver para la tabla
objetivo?), nunca **identidad** (¿el usuario actual es distinto de quien creó el RFC?). En
contraste, los botones Enviar/Cancelar del mismo archivo sí comparan identidad:

```yaml
# línea 247 (Enviar) y línea 292 (Cancelar)
Visible: =gvarSelectedChangeRequest.ChangeStatus = ....Draft
    && gvarSelectedChangeRequest.'Created By'.User = gvarUser.User
```

La app **tiene el patrón** (`'Created By'.User = gvarUser.User`) y lo aplica a 2 de los 4
botones de transición de estado, pero no a Aprobar/Rechazar. Si el mismo usuario (o el mismo
grupo AAD) tiene asignado Contributor y Approver para la misma tabla objetivo — algo que nada en
el modelo de `sanic_CHTblClientDataChangePermission` impide, porque son 2 filas independientes
sin validación cruzada — puede crear un RFC y aprobarlo él mismo. **A verificar en ambiente**:
si en la práctica los grupos AAD de Contributor y Approver de cada área están efectivamente
separados (organizacionalmente sí, según el contexto de negocio, pero el sistema no lo garantiza
técnicamente).

### 2.3 Detección de conflictos: existe, pero con alcance global equivocado (ver también 3.3)

`screen ChangeRequestDetails.fx.yaml`, líneas 192-210 (al pulsar "Enviar") y líneas 1523-1552
(al agregar una línea nueva) buscan otra línea con el **mismo `'Field Definition'.LogicalName`**
en estado Submitted/Approved — pero `Field Definition` es un catálogo compartido por tabla
(p. ej. "CIFBCO de Cliente"), no por registro. La verificación no filtra por
`TargetRecordID`/cliente, así que dos RFCs sobre **dos clientes distintos** que cambian el mismo
campo (p. ej. ambos actualizan CIFBCO) se marcan como "en conflicto" entre sí, aunque no tengan
relación real. Es una validación de seguridad/integridad mal targeteada, no ausente — pero el
efecto en producción sería bloqueo cruzado entre clientes no relacionados.

### 2.4 Campo crítico `cr140_changestatus` pertenece a un publisher ajeno a la solución

Las 3 soluciones del repo (`CashHubApps`, `CashHubCore`, `CashHubFlows`) declaran el mismo
publisher `Sistemas_Abiertos_Nicaragua`, prefijo `sanic` (`solutions/unpacked/{apps,core,flow}/
Other/Solution.xml`). El campo que controla el estado de **todo** el subsistema RFC —
consumido por los 6 flujos, ambos web resources de línea, y las 4 pantallas de la Canvas App —
es `cr140_changestatus` (`solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequest/
Entity.xml`, atributo `PhysicalName="cr140_ChangeStatus"`). Es el **único** atributo custom de
esta tabla con prefijo `cr140` en vez de `sanic`. Riesgo de ALM: si el segmento de solución que
originó ese campo (probablemente un publisher/solución "default" del ambiente donde un maker
agregó la columna sin seleccionar el contexto correcto) no viaja junto en cada despliegue, el
campo puede quedar huérfano o el ALM de import fallar al no encontrar su publisher de origen en
un ambiente nuevo.

### 2.5 Guardas de negocio del lado model-driven: cargadas pero desconectadas

`solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequestLine/FormXml/quickCreate/
{34540c31-06a6-f011-bbd2-000d3a9e7944}.xml`, líneas 139-151:

```xml
<formLibraries>
    <Library name="sanic_chwebrcrlinefilter.js" .../>
    <Library name="sanic_chwebrcrlineprefilloldvalue.js" .../>
    <Library name="sanic_chwebrcrlinetest.js" .../>
    <Library name="sanic_chwebrcrlinesaveorchestrator.js" .../>
</formLibraries>
<events>
    <event name="onload" application="false" active="false">
        <Handlers>
            <Handler functionName="ch_crline_onload_register" libraryName="sanic_chwebrcrlinesaveorchestrator.js" enabled="true" .../>
        </Handlers>
    </event>
</events>
```

El atributo `active="false"` del `<event>` desactiva el evento **completo**, sin importar que el
handler individual tenga `enabled="true"`. Efecto medible: en el formulario Quick Create de
línea de RFC del lado model-driven (usado por cualquiera con acceso directo a la tabla, o si se
habilita el sitemap de Governance), hoy **no corre ninguna de las 4 validaciones que el código
implementa**:
- `ch_crline_onload` (`sanic_chwebrcrlinefilter.js`) — filtrar el lookup de campo a los
  permitidos para la tabla objetivo — no está ni siquiera referenciado en `<events>`.
- `ch_crline_prefill_oldvalue_onload` (`sanic_chwebrcrlineprefilloldvalue.js`) — mismo caso.
- `ch_crline_onload_register`/`ch_crline_onsave` (`sanic_chwebrcrlinesaveorchestrator.js`) —
  referenciado, pero el evento que lo dispara está deshabilitado.
- `sanic_chwebrcrlineduplicateguard.js` (casi idéntico al anterior) — ni siquiera está en
  `formLibraries` de este form.

Consecuencia: por esta vía, un usuario puede crear una línea de RFC para **cualquier campo de
cualquier tabla** (no solo los marcados `IsAllowed=true` para la tabla objetivo), sin
prellenado de valor actual, sin chequeo de duplicados, y sin exigir que el RFC padre esté en
Draft. Todo el trabajo de validación (que el lado Canvas App sí hace, con sus propios bugs de
alcance descritos en 2.3) queda anulado en esta superficie.

### 2.6 `sanic_chwebrproposechange.js`: posible desconexión + bug de nombre de parámetro

Búsqueda de `chwebrproposechange` en todo `RibbonDiff.xml` y `FormXml` del repo: **0
resultados** fuera del propio archivo `.js`/`.js.data.xml` y las referencias de `Solution.xml`
(que solo lo listan como componente). No se encontró ningún botón de Command Bar ni evento de
formulario que invoque `ch_openProposeChange`. **A verificar en ambiente** (puede estar cableado
vía el diseñador moderno de comandos, no capturado en estos archivos), pero con la evidencia
disponible el script luce huérfano.

Bug adicional confirmado en el propio código (`solutions/unpacked/apps/WebResources/
sanic_chwebrproposechange.js`, línea 50): el objeto `formParameters` pasado a
`Xrm.Navigation.openForm` usa la clave `sanic_changestatus`:
```js
const formParameters = {
    sanic_targettable: entityLogicalName,
    sanic_targetrecordid: recordId,
    sanic_type: typeValue,
    sanic_changestatus: draftStatusValue   // <-- debería ser cr140_changestatus
};
```
Pero el campo real de la cabecera es `cr140_changestatus` (confirmado en 2.4). Los parámetros de
`openForm` solo prellenan un campo si la clave coincide exactamente con el nombre lógico; con
esta discrepancia, el Quick Create se abre pero el status inicial (`Draft`) **no se prellena**,
silenciosamente — sin error visible para el usuario ni para quien pruebe el flujo superficialmente.

### 2.7 Audit trail: se llena en los casos conocidos, con huecos y un typo de esquema

`sanic_CHTblClientDataChangesAuditTrail` tiene los campos `sanic_message`,
`sanic_opeationtype` (typo de esquema: falta la "r" de "operation"), `sanic_relatedclient`,
`sanic_relatedcrl`, `sanic_relatedrfc`, `sanic_result`. Los 4 flujos de materialización sí crean
una fila por resultado (`02-flows.md` §3.13-3.16, ya documentado). El hueco confirmado ahí:
en `Activaterow`/`Deactivaterow`, la rama `default: Terminate` del switch por tabla objetivo (si
`sanic_targettable` no matchea ninguna de las 3 tablas conocidas) no pasa por ninguna de las 2
ramas de auditoría (`Añadir_AUDIT-TRAIL_para_caso_FAILED`/`_SUCCESS`) — un RFC con tabla
objetivo inválida termina sin rastro de auditoría y, según el `runAfter` de
`Actualizar_RFC_a_FAILED`, probablemente tampoco actualiza el estado del RFC (queda huérfano).

---

## 3. Debilidades de ROBUSTEZ / RESILIENCIA

### 3.1 Punto único de falla en `Applyclientdatachanges` y `Createclientdata`

Inspección directa del `runAfter` de las acciones de nivel superior de ambos flujos
(`solutions/unpacked/flow/Workflows/CH-Flow-Applyclientdatachanges-*.json` y
`CH-Flow-Createclientdata-*.json`):

```
Obtener_lineas_del_RFC        runAfter: {}
Inicializar_varAnyError       runAfter: {Obtener_lineas_del_RFC: [Succeeded]}
Inicializar_varNotes          runAfter: {Inicializar_varAnyError: [Succeeded]}
Aplicar_a_cada_linea_del_RFC  runAfter: {Inicializar_varNotes: [Succeeded]}
Hubo_algun_error              runAfter: {Aplicar_a_cada_linea_del_RFC: [Succeeded, Failed, TimedOut]}
```

Si la acción `Obtener_lineas_del_RFC` (el `ListRecords` inicial) falla —por throttling de
Dataverse, timeout, o un problema transitorio de conexión—, **ninguna** acción posterior corre:
ni el loop, ni `Hubo_algun_error`, ni la actualización final del RFC a `Failed`. El run de Power
Automate termina en estado `Failed` (visible solo en el historial de ejecuciones del flujo), pero
**el registro `cr140_changestatus` del RFC queda en `Approved` para siempre**, sin que
`NotifywhenRFCimplementationfinish` se dispare jamás (su filtro exige `Rejected`/`Applied`/
`Failed`, nunca llega). Ni el solicitante ni el aprobador se enteran. Esto confirma, con
evidencia concreta de la estructura del flujo, la sospecha planteada en el contexto de negocio
("¿queda el RFC aprobado pero sin aplicar?"): sí, en el escenario específico de que el primer
paso del flujo falle.

`Activaterow`/`Deactivaterow` están mejor diseñados en este aspecto puntual: su acción
`Por_tabla_objetivo` (el switch) tiene 2 ramas de continuación cubriendo `Succeeded` y
`[TimedOut, Failed]` (`Añadir_AUDIT-TRAIL_para_caso_SUCCESS`/`_FAILED`), así que si el `Switch`
falla, sí se actualiza el RFC a `Failed` — con la salvedad de la rama `default: Terminate`
descrita en 2.7, que no pasa por ninguna auditoría.

### 3.2 Cero `retryPolicy` en los 4 flujos de materialización

```
rg -c "retryPolicy" en Activaterow, Deactivaterow, Createclientdata, Applyclientdatachanges → 0 en los 4
```
Ningún `UpdateOnlyRecord`/`CreateRecord`/`ListRecords` de estos flujos define una política de
reintento explícita — dependen íntegramente del retry por defecto del conector (4 reintentos con
backoff exponencial, y solo ante códigos 429/5xx del conector, no ante errores de negocio como un
valor que no castea al tipo de columna). Combinado con 3.1, un fallo transitorio de Dataverse en
el momento exacto de aplicar un RFC puede dejarlo huérfano sin ningún mecanismo de recuperación
automática.

### 3.3 Notificaciones sin ninguna rama de manejo de error

`Notifyreviewers` y `NotifywhenRFCimplementationfinish` — inspección de `runAfter` de sus
acciones de nivel superior — son cadenas 100% lineales, cada acción condicionada únicamente a
`Succeeded` de la anterior, sin una sola rama `Failed`/`Skipped`/`TimedOut` en ninguna de las dos.
Si falla cualquier paso intermedio (por ejemplo, la búsqueda de la variable de entorno
`sanic_CHEVAppUrl_ClientAdmin` vía `ListRecords` crudo sobre `environmentvariablevalues`, ya
señalada como patrón fragil en `02-flows.md` §4), **la notificación completa no llega a nadie**,
sin reintento ni notificación alternativa. Esto agrava el hallazgo de UX 4.4 (el solicitante
depende de Teams para enterarse del resultado): si la cadena de Teams falla, tampoco hay
respaldo.

### 3.4 Gap de OptionSet: confirmado en ambos extremos (flujo y Canvas App)

Ya documentado en `02-flows.md` §3.15-3.16: el caso `OptionSet (159460005)` del switch de
conversión de tipo en `Createclientdata`/`Applyclientdatachanges` tiene acciones vacías. Este
deep-dive agrega el otro extremo: en la Canvas App, `screen Clients.fx.yaml` y
`screen ClientInformation.fx.yaml` **no tienen ningún mecanismo genérico basado en `DataType`**
(`rg "DataType"` sin resultados en ninguno de los 2 archivos) — el alta de campos está
hardcodeada por nombre fijo (`LookUp('CH-Tbl-ClientTableLogicalFieldNames', Name = "Nombre" &&
Table = "sanic_chtblclient")`, Clients.fx.yaml ~línea 26). El único campo tipo Option Set/Choice
manejado (`AlwaysNotify`, "Notificar siempre") se resuelve a mano
(`Text(DataCardValue8.Selected.Value)`, ClientInformation.fx.yaml ~línea 117), sin
generalización. Es decir: el problema de OptionSets no es solo que el flujo no lo aplique — la
propia Canvas App tampoco construyó nunca un camino genérico para ese tipo de dato; cada campo
Option Set nuevo requeriría código a medida en 3 lugares (Canvas, y los 2 switches de flujo).
`screen ChangeRequestDetails.fx.yaml` (panel de creación de línea genérica, líneas ~1470-1521) sí
tiene un switch por `DataType` con casteo — pero solo valida que el valor ingresado sea
"castable", no resuelve el problema de qué pasa cuando el flujo webhook recibe ese valor
OptionSet en la línea del RFC.

### 3.5 Condiciones de carrera: dos bugs, en direcciones opuestas

- **Sobre-bloqueo** (2.3): el chequeo de conflicto al Enviar/crear línea en
  `ChangeRequestDetails.fx.yaml` compara por `Field Definition` global, sin filtrar por cliente
  — bloquea RFCs de clientes no relacionados que casualmente tocan el mismo campo.
- **Sub-bloqueo**: en `screen Clients.fx.yaml`/`screen ClientInformation.fx.yaml` (donde nace la
  mayoría de los RFCs de alta), **no existe ningún chequeo de conflicto** antes de crear la
  primera línea — confirmado por el agente de exploración (`rg` sin resultados de
  `Filter`/`LookUp` sobre `ClientDataChangeRequestLines` filtrando por `ChangeStatus` en esos 2
  archivos). Dos personas pueden crear 2 RFCs distintos sobre el mismo campo del mismo cliente
  sin ninguna advertencia hasta que uno de los dos intenta "Enviar" el segundo — y ahí choca con
  el bug de alcance global de 2.3, dando un mensaje de conflicto potencialmente engañoso (puede
  reportar conflicto con un cliente no relacionado en lugar de avisar específicamente del choque
  real).

---

## 4. Debilidades de UX/UI y PERFORMANCE

### 4.1 Fricción: alta completa de un cliente = mínimo 3 aprobaciones separadas

Ver 1.2.1. Dar de alta un cliente nuevo con 1 correo autorizado y 1 sucursal requiere 3 RFCs
independientes (Cliente, Correo, Sucursal), cada uno con su propio ciclo Draft→Submitted→
Approved→Applied, potencialmente aprobados por personas distintas (Banca de Empresas vs. Bóveda)
en momentos distintos. No hay ningún mecanismo de "RFC compuesto" o checklist que agrupe las 3
solicitudes como una sola unidad de negocio ("dar de alta este cliente"), a pesar de que el
modelo de datos (`ClientDataChangeRequestLine` con `Field Definition` genérico) técnicamente
podría soportar líneas de distintas tablas bajo un mismo RFC si se rediseñara la cabecera.

### 4.2 Delegación: patrón `Search()` dentro de `SortByColumns()` en 2 pantallas

- `screen Clients.fx.yaml` (~línea 1987):
  ```
  SortByColumns(Search(Filter('CH-Tbl-Clients', 'CH-Tbl-Clients (Views)'.'Fluent Detail List Ready'),
      SearchBox1.SearchText, Name, CIFBCO, CIFCOM), "modifiedon", SortOrder.Descending)
  ```
- `screen RFC.fx.yaml` (~línea 11512):
  ```
  SortByColumns(Search(Filter('CH-Tbl-ClientDataChangeRequests', ...toggle...),
      SearchBox1_1.SearchText, Name), "createdon", SortOrder.Descending)
  ```

`Search()` sobre múltiples columnas de texto envuelto en `SortByColumns()` es el patrón clásico
que Power Apps evalúa localmente una vez superado el umbral de delegación (por defecto 500-2000
filas) — el editor normalmente marca esto con un ícono de advertencia amarillo que no persiste
como texto en el YAML exportado, así que no se puede confirmar el mensaje exacto sin abrir el
Studio. **A verificar en ambiente**: con la tabla de Clientes creciendo, la búsqueda + orden
puede dejar de reflejar registros más allá del límite de filas no delegadas.

### 4.3 Precarga en `App.OnStart`: secuencial contra Microsoft Graph

Ya documentado en `04-apps-administrativas.md` (líneas 234-238): el `ForAll` sobre
`CH-Tbl-ClientDataChangePermissions` llama `Office365Groups.ListGroupMembers(...)` **una vez por
cada fila de permisos**, de forma secuencial dentro del `ForAll`, en cada apertura de la app. Con
2 áreas × 2 roles (Contributor/Approver) × 3 tablas objetivo como mínimo, son ≥12 llamadas a
Graph API antes de que el usuario vea la primera pantalla — no hay evidencia de paralelización
(`Concurrent(`) ni de cacheo entre sesiones. Las pantallas `Clients`/`ClientInformation`/`RFC`
en sí mismas, en cambio, **no** tienen `ClearCollect`/`Collect` pesado en su propio `OnVisible`
(confirmado: `screen Clients.fx.yaml` líneas 4-5 y `screen RFC.fx.yaml` líneas 4-5 solo hacen
`UpdateContext` liviano) — el costo de precarga está concentrado en `App.OnStart`, no repartido.

### 4.4 Sin indicador de RFC pendiente ni de resultado fallido

Ni `screen Clients`, ni `ClientInformation`, ni `screen RFC` muestran una marca visual (badge,
color, ícono) cuando un cliente/sucursal/correo tiene un RFC pendiente de aprobación — un usuario
puede volver a editar el mismo registro sin saber que ya hay un cambio en curso (lo que además
alimenta el bug de sub-bloqueo de 3.5). En `screen RFC.fx.yaml`, la columna de estado
(`ColName: "cr140_changestatus"`) se muestra como texto plano del option set, sin
`ColCellType` de tipo badge y sin ningún `Switch`/color condicional para diferenciar visualmente
un RFC `Failed` de uno `Applied` (confirmado: 0 resultados para "Failed"/"Applied"/"Rejected"
como literales en todo el archivo). El solicitante debe interpretar el valor crudo del picklist,
y solo se entera de un resultado por Teams (`NotifywhenRFCimplementationfinish`) — con el
agravante de 3.3 (esa notificación puede no llegar nunca si falla algún paso intermedio).

### 4.5 Sin validación de correo duplicado por cliente en el Power Fx actual

Relevante para la decisión de negocio ya aprobada (a) (correo puede repetirse entre clientes,
pero el par (dirección, cliente) debe ser único): hoy, el alta de correo en
`ClientInformation.fx.yaml` (rama "Emails" del switch, ~líneas 83-125) hace `Patch` directo sin
ningún `LookUp`/`Filter` previo que verifique si ya existe ese mismo correo para ese mismo
cliente. Tampoco existe todavía la alternate key en el esquema: `sanic_CHTblClientEmail/
Entity.xml` no tiene ningún bloque `<entityKeys>`/`<EntityKey>` — confirmando que la decisión (a)
sigue sin implementar a nivel de esquema, consistente con lo registrado en la sesión de negocio
previa (ver Engram, tema `cashhub/decisiones-validacion-y-cuentas`).

---

## 5. PROPUESTA DE MEJORA (priorizada)

> **Estado 2026-08-05 (tanda 2 desplegada en DEV)**: ✅ #4/#5 (conflict-check por registro
> destino y excluyendo el propio RFC), ✅ #6 (Try/Catch en flujos de materialización), ✅ #7
> (retryPolicy ×47 acciones), ✅ #8 (CH-Flow-RFCWatchdog + env var de timeout), ✅ #9 (caso
> OptionSet en ambos flujos). Guard de líneas verificado E2E (RFC sin líneas → Failed sin crear
> registro). Pendiente: activación manual del watchdog (la conexión es del usuario) + su E2E.
> El #3 (validación server-side con plugin) sigue para tanda futura.
>
> **Estado 2026-08-04 (tanda 1 implementada en fuente, pendiente de deploy)**: ✅ #1 (roles
> `CH-SR-ClientAdminContributor`/`CH-SR-ClientAdminApprover` creados), ✅ #2 (chequeo de
> identidad en Aprobar/Rechazar), ✅ #10/#11 (model-driven eliminada de ambas soluciones).
> Migración `cr140_changestatus` preparada: campo nuevo en Core + 6 flujos migrados + runbook
> ([06-runbook-migracion-cr140.md](../desarrollo/06-runbook-migracion-cr140.md)) — el cutover de
> la canvas app se hace en Studio durante el deploy. Soluciones bumpeadas a 1.1.0.0.

Convención de esfuerzo: **S** = días, cambio aislado; **M** = 1-2 sprints, toca 2-3 capas; **L**
= rediseño de esquema o de flujo transaccional, requiere migración de datos/coordinación entre
áreas.

| # | Propuesta | Categoría | Esfuerzo | Qué rompe / no rompe |
|---|---|---|---|---|
| 1 | Crear roles de seguridad Dataverse reales para las 9 tablas del dominio (mínimo: rol "Client Admin Contributor" y "Client Admin Approver" con privilegios explícitos Create/Read/Write a nivel Organización sobre Client/ClientBranch/ClientEmail/ClientDataChangeRequest/Line; Read-only sobre ClientDataChangePermission/AuditTrail/LogicalFieldName) | Seguridad | **M** | No rompe nada existente (hoy no hay roles cubriendo esto); requiere confirmar en ambiente qué rol usan hoy los usuarios reales antes de retirarlo. |
| 2 | Agregar chequeo de identidad (`CreatedBy ≠ usuario actual`) a la condición `Visible` de los botones Aprobar/Rechazar en `ChangeRequestDetails.fx.yaml` (líneas 110 y 150), igual que ya existe para Enviar/Cancelar (líneas 247, 292) | Seguridad | **S** | Cambio acotado a 2 fórmulas de un control; no afecta datos ni otros flujos. |
| 3 | Mover la validación de "RFC en Draft" y "no duplicado" a un plugin/Custom API server-side (pre-validación en `Create`/`Update` de `ClientDataChangeRequestLine`), en vez de solo JS/Power Fx client-side | Seguridad/Robustez | **M** | No rompe la UI actual (las validaciones cliente pueden quedarse como UX-first-line); cierra el bypass vía Web API directa detectado en 2.5. |
| 4 | Reparar el chequeo de conflicto de campo para que filtre también por `TargetRecordID`/cliente (no solo por `Field Definition` global) — mismo punto en `ChangeRequestDetails.fx.yaml` líneas ~199-202 y ~1539-1544 | Robustez | **S** | Corrige un bug de alcance; no cambia el modelo de datos. |
| 5 | Agregar el mismo chequeo de conflicto (ausente hoy) en el punto de creación de línea desde `Clients`/`ClientInformation`, para no depender solo del momento de "Enviar" | Robustez/UX | **S** | Complementa el punto 4; evita crear RFCs condenados a chocar. |
| 6 | Envolver el primer paso (`Obtener_lineas_del_RFC`) de `Applyclientdatachanges`/`Createclientdata` en un `Scope` con rama de error que fuerce `cr140_changestatus = Failed` + entrada de auditoría ante cualquier fallo, en vez de dejar el `runAfter` lineal actual | Robustez | **M** | Cambia estructura interna del flujo, no su contrato de entrada/salida; requiere red-testear los 2 flujos. |
| 7 | Introducir reintentos explícitos (`retryPolicy`) en las acciones `ListRecords`/`UpdateOnlyRecord`/`CreateRecord` de los 4 flujos de materialización | Robustez | **S** | Cambio de configuración por acción, sin tocar lógica. |
| 8 | Separar el estado "Applied" de "Approved" con una verificación de timeout: un job/flow programado que detecte RFCs en `Approved` hace más de N minutos sin pasar a `Applied`/`Failed`, y los marque `Failed` + notifique — red de contención para el punto único de falla de 3.1 | Robustez | **M** | Aditivo, no reemplaza los flujos existentes. |
| 9 | Implementar el caso `OptionSet` en el switch de conversión de tipo de `Createclientdata`/`Applyclientdatachanges`, y construir el mapeo genérico `DataType → valor` también del lado Canvas App (hoy hardcodeado por campo) | Robustez | **M** | Toca 2 flujos + Canvas App; no rompe campos Text/Number/Date/Money existentes. |
| 10 | **[DECIDIDO 2026-08-04: la model-driven app se desecha]** Eliminar el AppModule `sanic_ChAppClientAdministration`, su sitemap, los 5 web resources de línea (`crlinefilter`, `crlineprefilloldvalue`, `crlineduplicateguard`, `crlinesaveorchestrator`, `crlinetest`) y la command library `sanic_chappclientadministrationdefaultcommand13815`. La canvas app queda como única vía oficial. Nota: esto NO sustituye los puntos 1 y 3 — sin roles reales ni validación server-side, las tablas siguen accesibles por Excel/Web API. | Seguridad/Limpieza | **S** | Elimina superficie sin dueño; verificar en ambiente que ningún usuario dependa del sitemap antes de borrar. |
| 11 | **[Absorbido por el punto 10]** `sanic_chwebrproposechange.js` (huérfano, con bug de parámetro `sanic_changestatus` vs `cr140_changestatus`) se elimina junto con la model-driven app. | Seguridad/Limpieza | **S** | Sin impacto: nada lo invoca según el fuente. |
| 12 | Agregar alternate key `(sanic_client, emailaddress)` en `sanic_CHTblClientEmail` + validación de "correo ya existe para este cliente" antes del `Patch` en `ClientInformation.fx.yaml` | Esquema (decisión a) | **M** | Requiere confirmar que no haya duplicados existentes en datos reales antes de crear la key; alinea con la decisión de negocio ya aprobada. |
| 13 | Nueva tabla maestro de Cuentas del cliente (número, cliente lookup, moneda, estado), administrada bajo el mismo modelo RFC — agregar `sanic_targettable="sanic_chtblclientaccount"` al catálogo `ClientTableLogicalFieldNames` y un permiso Contributor/Approver nuevo | Esquema (decisión b) | **L** | Nueva tabla + nuevas filas de permiso; no afecta las tablas existentes. Requiere que el punto 9 (OptionSet genérico) y el punto 1 (roles) estén resueltos antes para no heredar los mismos gaps. |
| 14 | Lookup a `Client` en `Declaration`/`ReceivedEmails` (decisión c) | Esquema (decisión c) | **M** | Fuera del alcance directo de Client Administration pero depende de que el modelo de Cliente (punto 13, cuentas) esté estable primero. |
| 15 | Rediseñar el alta de cliente completo como un único RFC "compuesto" (cabecera + líneas de Cliente + Sucursal + Correo en un solo ciclo de aprobación), o al menos agrupar visualmente los 3 RFCs relacionados en la UI con un identificador común | UX | **L** | Cambio de modelo de datos (relación entre RFCs) y de flujo de aprobación; impacto alto en Notifyreviewers/NotifywhenRFCimplementationfinish. Priorizar después de 1-9. |
| 16 | Agregar badges/color de estado en `screen RFC.fx.yaml` (Failed en rojo, Applied en verde, etc.) y un indicador de "RFC pendiente" en `Clients`/`ClientInformation` | UX | **S** | Solo UI, sin riesgo de romper datos. |
| 17 | Revisar `Search()+SortByColumns()` no delegable en `Clients`/`RFC` — confirmar en Studio el umbral real y, si aplica, mover el filtro de texto a una vista Dataverse indexada o a un control con delegación nativa | Performance | **M** | Requiere pruebas en ambiente con volumen real; no es bloqueante hoy si el volumen de clientes es bajo, pero escala mal. |

### Orden sugerido de ataque

1. **Seguridad primero** (1, 2, 3, 11): son los gaps más baratos de cerrar y los que más
   exposición real tienen hoy (tablas sin rol, auto-aprobación posible, JS deshabilitado).
2. **Robustez del subsistema RFC** (4, 5, 6, 7, 8, 9): antes de agregar la tabla de Cuentas
   (13), conviene que el motor de aplicación de cambios no tenga el punto único de falla de 3.1
   ni el gap de OptionSet — si no, la nueva tabla hereda los mismos problemas el día uno.
3. **Decisiones de negocio ya aprobadas** (12, 13, 14): una vez que el motor RFC es confiable.
4. **UX** (15, 16, 17): mejoras incrementales, no bloqueantes, pero 16 es tan barata que puede
   adelantarse en paralelo con el punto 1.
