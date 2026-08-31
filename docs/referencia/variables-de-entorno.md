# Variables de entorno — CashHubCore

> ⚠️ **OBSOLETO desde el 2026-08-31.** Este documento describe 19 variables; llegaron a ser 23 y
> hoy **quedan 2**: `sanic_CHEVSharedMailboxAddress` y `sanic_CHEVOpsTeamsGroupId`. Las otras 21 se
> eliminaron tras verificar que ningún flujo, plugin ni app las consumía. El análisis que sostuvo
> ese borrado —agrupado por causa de no-uso, con el reemplazo de cada una— está en
> [`limpieza-env-vars.md`](limpieza-env-vars.md). Lo que sigue se conserva como registro histórico
> de la configuración previa; **no lo uses como referencia del estado actual**.

Fuente: `solutions/unpacked/core/environmentvariabledefinitions/<nombre>/environmentvariabledefinition.xml`.
19 variables, todas `iscustomizable=1`, `isrequired=0`, `secretstore=0` (ninguna se
guarda en Azure Key Vault). Tipo `100000000` = String, `100000001` = Number (valores
estándar de la plataforma, no definidos en estos archivos individuales).

## 1. Catálogo completo

| Variable | Tipo | Default confirmado | Propósito | Consumida por |
|---|---|---|---|---|
| `sanic_CHEVAADGroup_Admin` | String | — (sin default) | Grupo AAD del rol de administración | No se encontró ninguna referencia textual a este nombre de esquema en flujos ni apps. Ver [roles-y-seguridad.md](roles-y-seguridad.md) §4 — el patrón real usa GUIDs en tablas de datos, no esta variable. |
| `sanic_CHEVAADGroup_ClientAdmin` | String | — | Grupo AAD de administración de datos del cliente | Ídem — sin referencia textual confirmada. |
| `sanic_CHEVAADGroup_Counting` | String | — | Grupo AAD de cajeros de recuento | Ídem — la app Recounting no la lee. |
| `sanic_CHEVAADGroup_EmailValidation` | String | — | Grupo AAD del módulo de validación de correo | Ídem — la app Email Validation Console no la lee. |
| `sanic_CHEVAADGroup_Reception` | String | — | Grupo AAD de oficiales de bóveda/recepción | Ídem — la app Reception no la lee. |
| `sanic_CHEVAADGroup_Supervisor` | String | — | Grupo AAD de supervisores de conteo | Ídem — la app Supervisor no la lee. |
| `sanic_CHEVAppUrl_ClientAdmin` | String | — | URL de la app model-driven de administración de clientes | **Sí se consume**: en los flujos `Notifyreviewers` y `NotifywhenRFCimplementationfinish`, vía `ListRecords` crudo sobre `environmentvariablevalues` filtrando `schemaname eq 'sanic_CHEVAppUrl_ClientAdmin'` (no usa la acción nativa "Get an environment variable value"). |
| `sanic_CHEVAppUrl_Counting` | String | — | URL de la app de conteo | No se encontró consumo en los 19 flujos analizados. |
| `sanic_CHEVAppUrl_EmailValidation` | String | — | URL de la app de validación de correo | No se encontró consumo en los 19 flujos. |
| `sanic_CHEVAppUrl_Reception` | String | — | URL de la app de recepción | No se encontró consumo en los 19 flujos. |
| `sanic_CHEVAppUrl_Supervisor` | String | — | URL de la app de supervisión | No se encontró consumo en los 19 flujos. |
| `sanic_CHEVCsvExportLibraryUrl` | String | — | URL de una librería para exportar CSV | No se encontró consumo en flujos ni en el código de las apps operativas. |
| `sanic_CHEVEmailSubjectToAccept` | String | — | Asunto de correo que el flujo debería reconocer | **No consumida** — `VR03` (regla de asunto) recibe su valor de referencia como parámetro de entrada del propio flujo (`text_1`), no lee esta variable de entorno directamente. |
| `sanic_CHEVFileSizeMaxMB` | Number | **`5`** (confirmado en XML) | Tamaño máximo de archivo adjunto permitido (MB) | `VR05` (tamaño de adjunto) recibe el máximo como parámetro de entrada (`ValidationValue`), no se confirmó una lectura directa de esta variable dentro del flujo — el valor de referencia lo pasa quien invoca (la app), probablemente poblado desde esta env var pero no verificable en el código de `VR05.json`. |
| `sanic_CHEVIbsBranchCodeList` | String | — | Lista de códigos de sucursal del core IBS Branch | **No explotada**: la app Digitization arma el archivo de carga masiva con `TRNCODE=8004` fijo y campos de identidad de agencia genéricos hardcodeados (`TIPOID`, `NACIONALIDAD`, `NUMEROID1`, `NUMEROID2`) en vez de resolver el código de sucursal contra esta lista. |
| `sanic_CHEVProcessingLibraryUrl` | String | — | URL de una librería de procesamiento (probablemente Azure Function) | No se encontró consumo en flujos ni apps analizados. |
| `sanic_CHEVSharedMailboxAddress` | String | **`no-reply-powerplatform-dev@baccredomatic.ni`** (confirmado en XML — dirección de ambiente **dev**, literal `-dev-` en el dominio) | Buzón compartido donde llegan los correos de clientes | **No consumida por `IngestEmails`**: el trigger `SharedMailboxOnNewEmailV2` de `IngestEmails` tiene el buzón **hardcodeado literalmente** (`no-reply-powerplatform-dev@baccredomatic.ni`) en el JSON del flujo, no referenciado vía esta variable de entorno. Si se promueve a producción sin editar el flujo a mano, sigue escuchando el buzón de dev. |
| `sanic_CHEVSharedMailboxArchiveFolderID` | String | `AAMkADgz...` (ID de carpeta Outlook, confirmado en XML) | Carpeta de archivo del buzón tras procesar el correo | **No consumida donde se esperaría**: en la app Email Validation Console, `varIDFolderArchivado` está hardcodeado en el código Power Fx con un comentario propio indicando que la env var real está deshabilitada. |
| `sanic_CHEVTimezone` | String | **`America/Managua`** (confirmado en XML) | Zona horaria de referencia del sistema | No se encontró una lectura explícita vía "Get environment variable" en los 19 flujos; `ExtractedTablesFromExcel` usa el literal `Central America Standard Time` hardcodeado en su acción `CreateFile`, no esta variable. |

## 2. Resumen: consumo real vs. declarado

**Solo 1 de las 19 variables tiene un consumo confirmado y funcional en el código
revisado**: `sanic_CHEVAppUrl_ClientAdmin` (usada por `Notifyreviewers` y
`NotifywhenRFCimplementationfinish`, aunque por un patrón `ListRecords` manual en vez
de la acción nativa de Dataverse para variables de entorno).

Las 18 restantes están definidas en la solución pero, contra la evidencia del código
de flujos y de las 8 canvas apps (`solutions/unpacked/apps/CanvasApps/src/`), **no se
encontró ninguna referencia textual a su `schemaname`** fuera de la propia definición.
Esto no prueba que no se usen en absoluto (podrían leerse dinámicamente en tiempo de
ejecución vía una acción "Get environment variable value" cuyo parámetro no incluya el
nombre literal en el JSON, o resolverse en un Custom Connector/Azure Function fuera de
este repo) — pero **dentro de los archivos de flow/apps analizados, el valor que
debería salir de la variable de entorno aparece hardcodeado en su lugar** en los tres
casos más críticos:

| Dónde debería usarse la env var | Qué hay hardcodeado en su lugar | Archivo |
|---|---|---|
| `sanic_CHEVSharedMailboxAddress` en el trigger de `IngestEmails` | `no-reply-powerplatform-dev@baccredomatic.ni` literal en el JSON del trigger | `solutions/unpacked/flow/Workflows/CH-Flow-IngestEmails-*.json` |
| `sanic_CHEVSharedMailboxArchiveFolderID` en Email Validation Console | `varIDFolderArchivado` hardcodeado en `App.OnStart` | `solutions/unpacked/apps/CanvasApps/src/sanic_chappemailvalidationconsole_f3b93/` |
| `sanic_CHEVIbsBranchCodeList` en el layout de carga masiva de Digitization | `TIPOID`/`NACIONALIDAD`/`NUMEROID1`/`NUMEROID2` fijos, identidad de agencia genérica | `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/` |

## 3. Riesgo de ALM

Ninguna variable usa `secretstore=1`, por lo que ninguna se resuelve vía Azure Key
Vault — todas dependen de que el valor por ambiente (`environmentvariablevalue`) se
sobreescriba manualmente al desplegar a un ambiente distinto de dev. El caso más
visible es `sanic_CHEVSharedMailboxAddress`: su default es una dirección de dev y, aun
si se sobreescribiera correctamente en producción, **no serviría de nada** porque
`IngestEmails` no la lee — el buzón real que escucha el flujo está fijo en el JSON.

Ver también: [conexiones-y-flujos.md](conexiones-y-flujos.md) para el detalle de
`IngestEmails` y de los flujos del subsistema RFC que sí consumen
`sanic_CHEVAppUrl_ClientAdmin`; [roles-y-seguridad.md](roles-y-seguridad.md) §4 para
el uso real (por GUID, no por variable) del control de acceso por grupo AAD.
