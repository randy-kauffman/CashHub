# CH-App-Configuration — la app model-driven de configuración

> Fuente: `scripts/provision-config-app.py` y verificación en vivo contra el ambiente
> (`org36e60d9d`) el 2026-08-31: metadatos de las 12 tablas, `ValidateApp`, y el contenido
> de la solución `CashHub_Patch_2`.

## 1. El problema que resuelve

CashHub tiene **12 tablas de configuración que ninguna canvas app administra**. Se tocaban
a mano en el editor de tablas de Dataverse (una URL distinta por tabla, sin columnas útiles
en las vistas, un formulario con un solo campo) o por script. Eso significa que la persona
que configura el sistema necesitaba saber el nombre lógico de cada tabla y navegar el maker
portal — no es administración, es arqueología.

Esta app es la puerta única: **una sola app, cinco grupos, doce tablas**, con vistas y
formularios pensados para configurar, no para mirar.

Es una app **de configuración, no de operación**: no toca depósitos, recuentos ni
transacciones. Ese es el criterio que decide qué entra y qué no.

## 2. Qué se creó

| Pieza | Identificador | Dónde vive |
|---|---|---|
| App model-driven | `sanic_CHAppConfiguration` — "CH-App-Configuration" | `CashHub_Patch_2` |
| SiteMap | `sanic_CHAppConfiguration` (5 grupos, 12 subáreas) | `CashHub_Patch_2` |
| Rol de seguridad | `CH-SR-ConfigAdmin` | `CashHub_Patch_2` |
| Icono | web resource SVG `sanic_/icons/cashhub-configuration.svg` | `CashHub_Patch_2` |
| Vistas | 12 vistas por defecto reescritas + 4 vistas operativas nuevas | `CashHub_Patch_2` |
| Formularios | 12 formularios principales reescritos | `CashHub_Patch_2` |

URL de la app: `<DATAVERSE_URL>/main.aspx?appid=9aa3c65d-0aa5-f111-aaac-000d3a3425f0`

**Todo queda en `CashHub_Patch_2`**, nunca en `CashHubCore`/`Apps`/`Flows`. Las 12 tablas
entran a la solución como componente raíz con `rootcomponentbehavior = 1` (*no incluye
subcomponentes*): la solución transporta nuestras vistas, formularios, app, sitemap, rol e
icono — **no** la definición de las tablas. Exportar este patch no pisa el esquema de Core.

## 3. Mapa de la app

```
Configuración (área)
├── Parámetros del sistema
│   └── Parámetros ................ CH-Tbl-Parameter
├── Acceso: Hub y módulo RFC
│   ├── Catálogo de apps .......... CH-Tbl-AppCatalog
│   ├── Acceso a apps (grupos AAD)  CH-Tbl-AppCatalogAccess
│   └── Permisos RFC de cliente ... CH-Tbl-ClientDataChangePermission
├── Motor de validación y correo
│   ├── Reglas de validación ...... CH-Tbl-ValidationRules
│   ├── Plantillas de correo ...... CH-Tbl-EmailTemplate
│   └── Playbook de la bandeja .... CH-Tbl-Playbook
├── Catálogos de negocio
│   ├── Campos editables por RFC .. CH-Tbl-ClientTableLogicalFieldName
│   └── Denominaciones de moneda .. CH-Tbl-MoneyDenomination
└── Maestras operativas
    ├── Ubicaciones ............... CH-Tbl-Locations
    ├── Mesas de conteo ........... CH-Tbl-CountingDesk
    └── Transportadoras ........... CH-Tbl-TransportCompany
```

Las tres últimas no son parámetros del sistema: son maestras de negocio. Van igual porque
**tampoco tienen app que las administre** — hoy las canvas apps solo las leen.

## 4. Las 4 vistas operativas que se agregaron

No son adorno: cada una responde una pregunta que hoy se contesta a ojo.

| Vista | Qué contesta |
|---|---|
| `CH-Config \| Parámetros sin valor o inactivos` | ¿Qué configuración falta? Si acá aparece un `Security.ApproverGroup.*`, la política fail-closed del backend está frenando aprobaciones. |
| `CH-Config \| Permisos RFC sin grupo AAD` | ¿Qué filas del módulo RFC siguen con el placeholder `PENDIENTE-AAD-*`? Sin esas filas nadie opera Client Administration. |
| `CH-Config \| Reglas inactivas` | ¿Qué reglas está salteando el motor de validación? Antes de culpar al parser, mirar acá. |
| `CH-Config \| Denominaciones deshabilitadas` | Qué denominaciones no acepta el banco. Se deshabilitan, **no** se borran: el histórico de conteo las referencia. |

## 5. El rol `CH-SR-ConfigAdmin`

- CRUD **Global** (Create/Read/Write/Delete/Append/AppendTo/Assign/Share) sobre las **11
  tablas asegurables**.
- Privilegios de plataforma para abrir la app y usarla: `prvReadAppModule`,
  `prvReadWebResource`, `prvReadQuery`, CRUD de `UserQuery` (vistas personales),
  `prvReadUser` (resuelve el lookup de cajero en Mesas de conteo), `prvReadBusinessUnit`,
  `prvReadTeam`. Total: **98 privilegios**.
- Asociado a la app vía `appmoduleroles` — sin esa asociación el rol no abre la app aunque
  tenga los privilegios de tabla.

No otorga nada sobre datos operativos. Configurar el sistema y operarlo son dos oficios
distintos y dos roles distintos.

**Falta a mano**: asignar el rol a los usuarios o al team que administra configuración.

## 6. TRAMPA — `CH-Tbl-Parameter` no tiene control de acceso

`sanic_chtblparameter` se creó con `OwnershipType = None` (ver `scripts/provision-parameters-table.py`,
donde el comentario dice *"None = organization-owned"* — **eso es incorrecto**: `None` y
`OrganizationOwned` son valores distintos del enum).

Consecuencia verificada contra el ambiente:

```
GET /privileges?$filter=contains(name,'sanic_chtblparameter')  ->  []
```

**Dataverse no genera ni un solo privilegio para esa tabla.** No hay `prvRead`, no hay `prvWrite`.

### Lo que eso significa NO es lo que parece

Una tabla sin privilegios **no está cerrada: está abierta**. Sin privilegios no hay nada que
chequear, y la plataforma deja pasar a todo el mundo. Probado por impersonación
(`MSCRMCallerID`) con un usuario que solo tiene `CH-SR-DepositsTracker`,
`CH-SR-EmailOperationsConsole` y `Basic User` — **sin admin de ninguna clase**:

| Operación | Resultado |
|---|---|
| Leer las filas | `200` |
| Crear una fila | `204` |
| Actualizar una fila | `204` |
| Borrar una fila | `204` |

CRUD completo. Por eso `CH-SR-ConfigAdmin` no incluye esta tabla: no hay privilegio que otorgar,
y tampoco hace falta para que la app la lea.

**El riesgo real**: `Security.ApproverGroup.Supervisor` es el GUID del grupo AAD que decide quién
firma aprobaciones, y cualquier usuario del ambiente lo puede reescribir. Un cajero que lo apunte
a un grupo del que es miembro **se aprueba sus propias diferencias de recuento**. El fail-closed
de `ApproverGroupConfiguration.cs` no lo frena: protege contra config *ausente*, no contra config
*envenenada* — lee el valor, es un GUID válido, sigue adelante.

### El ownership NO se puede cambiar en el lugar — y la API miente

Probado contra el ambiente el 2026-08-31:

| Intento | Resultado |
|---|---|
| `PATCH /EntityDefinitions(...)` con `OwnershipType` | `405 Operation not supported on EntityMetadata` |
| `PATCH` no-op (misma Description) | `405` — el 405 es del método, no del campo |
| **`PUT /EntityDefinitions(<metadataid>)`** con `OwnershipType` | **`204 No Content`** |

Y el `204` **miente**: al comparar las 115 propiedades de la metadata antes y después, lo único
que cambió fue `ModifiedOn`. `OwnershipType` siguió en `None`. Dataverse acepta la request,
descarta el cambio y devuelve éxito.

**No vuelvas a intentarlo por esa vía.** `OwnershipType` es inmutable.

### Remediaciones posibles (ninguna aplicada)

1. **Plugin guard** en Pre-Validation de Create/Update/Delete sobre la tabla, con el idiom que ya
   usa el proyecto (`GuardReceptionPlugin`, `GuardAppCatalogPlugin`). Cierra el agujero sin
   recrear nada ni ventana de downtime. **Descartado por decisión del usuario.**
2. **Recrear la tabla** como `OrganizationOwned` y migrar las filas. Toca `Schema.cs`,
   `ApproverGroupConfiguration.cs`, `CH-Flow-DownloadData` y 3 scripts, más una ventana en la que
   el fail-closed bloquea todas las aprobaciones.
3. **Field Security Profile** sobre `sanic_value` — sin verificar si FLS aplica a una tabla sin
   ownership.

Hoy el agujero **sigue abierto**. Está documentado, no mitigado.

## 7. Cómo se re-aplica

```bash
python3 scripts/provision-config-app.py --dry-run   # ver qué haría
python3 scripts/provision-config-app.py             # aplicar
```

Idempotente de verdad: los GUID de celdas y secciones de los formularios salen de `uuid5`
sobre un namespace fijo, así que re-correr produce **exactamente** el mismo XML y el script
reporta `[=]` en vez de reescribir. Requiere `pp_secrets.env`.

## 8. Cosas que la API de Dataverse hace distinto de lo documentado

Tres trampas que costaron sangre y quedan acá para el próximo:

1. **Un `appmodule` sin publicar no existe para las consultas.** `GET /appmodules` no lo
   devuelve y el retrieve por id da **404** — pero el registro SÍ existe: reserva el
   `uniquename` (un segundo `POST` falla con `0x80050135`) y aparece en `solutioncomponents`
   con `componenttype = 80`. El script tiene una búsqueda de dos patas por eso.
2. **`webresourceid` es `SystemRequired` al crear un appmodule.** Sin icono no hay app: el
   web resource va primero, siempre.
3. **`appmodulecomponents` no acepta POST directo**: `appmoduleidunique` no está expuesto
   como navigation property. La única vía es la acción **`AddAppComponents`**, con
   `{"@odata.type": "Microsoft.Dynamics.CRM.entity", "entityid": "<MetadataId>"}` para
   tablas y `Microsoft.Dynamics.CRM.sitemap` para el sitemap.

`ValidateApp` cierra en **0 errores**. La advertencia *"Entity doesn't reference a form or
view"* es esperada y deseada: las tablas entran completas, así el administrador también
tiene a mano las vistas `Inactive …` sin que haya que declararlas una por una.

## 9. El icono

`docs/marca/icono-configuration.svg`, en la línea del resto de CashHub: cuadro rojo BAC
`#C8102E` con `rx=112`, glifo blanco de trazo `28` con extremos redondeados. El glifo son
tres deslizadores con perilla — parámetros que se ajustan, no un engranaje genérico. Las
perillas usan la misma construcción que los nodos del icono de Deposits Tracker (círculo
blanco con núcleo rojo).

## 9. Migración de environment variables a parámetros

El 2026-08-31 se sacaron de circulación las dos environment variables que leía
**CH-App-IngestionConsole** (`sanic_chappemailvalidationconsole_f3b93` — ojo: el nombre interno
dice "emailvalidationconsole", el display dice "IngestionConsole").

| Environment variable | Parámetro que la reemplaza | Category / Type |
|---|---|---|
| `sanic_CHEVBancaMailbox` | `Email.Banca.Mailbox` | Email / Email |
| `sanic_CHEVAppUrl_ClientAdmin` | `Apps.ClientAdministration.Url` | General / Url |

Las dos filas viven en la lista `SEED` de `scripts/provision-parameters-table.py`, así que se
siembran solas en cualquier ambiente nuevo.

### Consumidores migrados

| Consumidor | Qué cambió |
|---|---|
| `CH-App-IngestionConsole` | screen `ColaDeProcesamiento`, control `btnAvisarBanca`, `OnSelect`: los dos `LookUp('Environment Variable Values', …)` pasaron a `LookUp('CH-Tbl-Parameters', …)` |
| `CH-Flow-Notifyreviewers` | acción `Obtener_enlace_a_la_app_de_adminsitracion_de_clientes` |
| `CH-Flow-NotifywhenRFCimplementationfinish` | ídem |

Los flujos se desplegaron con `scripts/deploy-rfc-notify-flows.sh` (deploy quirúrgico, sin import
de solución), con guarda de drift: si el flujo vivo difiere del repo más allá de este cambio, el
script **no despliega**. Los dos quedaron en `statecode 1 / statuscode 2`.

### Detalles que rompen si no se miran

1. **La data source va en PLURAL**: `'CH-Tbl-Parameters'`, no `'CH-Tbl-Parameter'`. Las canvas apps
   nombran la fuente Dataverse con el **DisplayCollectionName**, y en esta tabla el singular y el
   plural difieren (a diferencia de `CH-Tbl-Received Emails`, donde son iguales).
2. **`'Is Active'` lleva comillas simples** — el display name tiene un espacio.
3. **En los flujos cambia el nombre de la columna del resultado**: la expresión de consumo pasó de
   `first(outputs(…)?['body/value'])?['value']` a `…?['sanic_value']`. Si migrás la acción y te
   olvidás de la expresión, el link sale vacío y no falla nada — silencioso.
4. La fórmula de la canvas app está duplicada en **tres** archivos por copia de la app
   (`Src/*.fx.yaml`, `Src/EditorState/*.editorstate.json`, `Other/Src/*.pa.yaml`) y hay **dos**
   copias en el repo (`apps/` y `patch/`). Son seis archivos.

### Estado

- `sanic_CHEVAppUrl_ClientAdmin`: sin consumidores. Estaba **sin default y sin valor asignado**, así
  que el link "See details in powerapps" de los mensajes de Teams y el "Abrir Client Administration"
  del correo **nunca se renderizaron**. El parámetro sí lleva la URL real.
- `sanic_CHEVBancaMailbox`: sin consumidores.
- **Ninguna de las dos se eliminó**: viven en `CashHubCore` y quitarlas es tocar la solución grande.
- **Falta a mano**: aplicar el cambio de fórmula en Studio para CH-App-IngestionConsole (agregar
  `CH-Tbl-Parameters` como fuente de datos y pegar las dos líneas). El repo ya lo tiene; el ambiente
  no. Convención del proyecto: las canvas apps no se empaquetan desde acá.

## 10. Ver también

- [07-seed-data.md](07-seed-data.md) — los datos que se cargan en varias de estas tablas.
- [04-deploy-y-alm.md](04-deploy-y-alm.md) — orden de importación y variables de entorno.
- [../referencia/roles-y-seguridad.md](../referencia/roles-y-seguridad.md) — el resto de los roles `CH-SR-*`.
