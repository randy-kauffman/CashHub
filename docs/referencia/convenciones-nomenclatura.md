# Convenciones de nomenclatura — CashHub

Patrones de nombrado observados en las 3 soluciones y las excepciones/violaciones
detectadas contra el código real bajo `solutions/unpacked/`.

## 1. Patrones confirmados

| Elemento | Prefijo/patrón | Ejemplo | Fuente |
|---|---|---|---|
| Publisher | `sanic_` (`CustomizationPrefix` en `Other/Solution.xml`) | `sanic_CHTblClient` | `solutions/unpacked/core/Other/Solution.xml` |
| Tablas custom | `sanic_CHTbl*` | `sanic_CHTblBags`, `sanic_CHTblDeclaredDeposits` | `solutions/unpacked/core/Entities/` |
| Roles de seguridad | `CH-SR-*` | `CH-SR-Counter`, `CH-SR-Receptionist` | `solutions/unpacked/core/Roles/` |
| Variables de entorno | `sanic_CHEV*` | `sanic_CHEVFileSizeMaxMB` | `solutions/unpacked/core/environmentvariabledefinitions/` |
| Flujos | `CH-Flow-*` | `CH-Flow-IngestEmails`, `CH-Flow-ProcessXMLFiles` | `solutions/unpacked/flow/Workflows/` |
| Apps canvas | `sanic_chapp*` | `sanic_chappreception_9ff29`, `sanic_chapprecounting_54962` | `solutions/unpacked/apps/CanvasApps/src/` |
| Option sets globales | `sanic_ch*` en minúscula, `CH-Ch-*` como display name | `sanic_chchcurrency` / "CH-Ch-Currency" | `solutions/unpacked/core/OptionSets/` |
| Valores de option set locales | prefijo `159460xxx` | `159460000`, `159460001`... | Deriva de `CustomizationOptionValuePrefix=15946` en `Other/Solution.xml` |
| Componentes/librerías compartidas | `sanic_chcl*` | `sanic_chclsharedcomponents_85fca` | `solutions/unpacked/apps/CanvasApps/src/` |

## 2. Excepciones y violaciones detectadas

### 2.1 `cr140_ChangeStatus` — prefijo de otro publisher

El campo más crítico del flujo de cambio de datos de cliente,
`sanic_CHTblClientDataChangeRequest.cr140_changestatus`, usa el prefijo `cr140_` en
vez de `sanic_`. Confirmado en `solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequest/Entity.xml`.
Su optionset asociado mezcla un rango `796490xxx` (valores Draft/Submited/Approved/
Rejected/Applied/Failed) con un valor suelto `159460001` (Canceled) del rango propio
de la solución — evidencia de que el campo fue importado desde otra
solución/publisher y no recreado con el prefijo `sanic_`. Es el único campo de las 38
tablas con este patrón.

### 2.2 `Ch-Flow-VR02` — casing distinto al resto de los flujos

Todos los demás flujos usan el prefijo `CH-Flow-` (C y H mayúsculas). El archivo del
flujo de validación de email es literalmente `Ch-Flow-VR02-7AD3F050-B59E-F011-BBD2-7CED8D1ED971.json`
(h minúscula) — confirmado listando `solutions/unpacked/flow/Workflows/`. Inconsistencia
de nombrado dentro del propio subconjunto `VR01`–`VR05`, que por lo demás comparten
forma y convención.

### 2.3 Descripción oficial de la solución con typos

`Other/Solution.xml` de CashHubCore describe la solución literalmente como:

> "Cash Hub Solution: Dataverse tables, columns, relationships, **chouces**,
> environment variables, **connetion** references, security role"

("chouces" → "choices", "connetion" → "connection") — typos en el texto oficial de
metadata de la solución, no en un campo de negocio.

### 2.4 Idioma mixto en labels y nombres de campo

La mayoría del modelo está en inglés, pero varios campos/labels están en español sin
un criterio aparente: `TransactionBatches.sanic_comentarios`,
`ApprovalRequest.sanic_approvalstatus` (Aprobado/Rechazado),
`CountingDesk.sanic_machineserialnumber` (única `<Description>` de negocio real, en
español: "Número de serie de maquina BPSC1"), `EmailsAttachments.sanic_tamanoenbytes`.

### 2.5 Nombres de tabla en singular vs. plural inconsistentes en referencias de app

Las canvas apps referencian los data sources con nombres de esquema en plural
(`CH-Tbl-Bags`, `CH-Tbl-ReceptionBatches`, `CH-Tbl-DepositsRecountings`) mientras que
el nombre lógico de la tabla en Dataverse es singular (`sanic_CHTblBags` es plural en
sí mismo, pero `sanic_chtbldepositsrecounting` en singular se referencia como
`CH-Tbl-DepositsRecountings` en la UI de la app) — no es un error funcional (Dataverse
resuelve el `EntitySetName` automáticamente), pero refleja que el nombre visible en
Power Fx no siempre coincide 1:1 con el `SchemaName`.

### 2.6 Áreas del Site Map con typo (`AppModule Client Administration`)

En `solutions/unpacked/apps/AppModuleSiteMaps/sanic_ChAppClientAdministration/AppModuleSiteMap.xml`,
el grupo "Governance" tiene una subárea rotulada **"Gebernanza"** (sic, por
"Gobernanza") — typo de UI visible al usuario final, no un `LogicalName`.

### 2.7 Nombre de acción interno engañoso en un flujo

No es un problema de nomenclatura de esquema, pero es relevante para quien audite el
código: en `CH-Flow-Activaterow`, la acción que efectivamente pone el registro en
**Active** se llama `Actualizar_cliente_a_INACTIVE` (copiada de `Deactivaterow` y no
renombrada). Ver [conexiones-y-flujos.md](conexiones-y-flujos.md) §5.

## 3. Typos en `LogicalName` (no corregibles sin recrear el campo)

Estos son un caso distinto de los anteriores: no son errores de convención sino typos
grabados en el `LogicalName` interno de un campo, que en Dataverse **no se puede
renombrar** sin eliminar y recrear la columna. Detalle completo, con ruta exacta y
evaluación de impacto de cada uno, en [typos-conocidos.md](typos-conocidos.md).

Ver también: [modelo-de-datos.md](modelo-de-datos.md) para el catálogo completo de
tablas, [catalogo-de-choices.md](catalogo-de-choices.md) para los valores de cada
option set citado acá.
