# Despliegue y ALM — CashHub

> Fuente: [`docs/analysis/01-core-data-model.md`](../analysis/01-core-data-model.md) §4-5, [`02-flows.md`](../analysis/02-flows.md), lectura directa de `solutions/unpacked/core/environmentvariabledefinitions/`, `solutions/unpacked/flow/Other/Customizations.xml` y `solutions/unpacked/flow/Workflows/`.

## 1. Orden de importación

Las 3 soluciones tienen dependencias implícitas por consumo de tablas y conectores, no declaradas como `MissingDependencies` cruzadas entre ellas en los `Solution.xml` (cada una solo declara dependencias con soluciones estándar de la plataforma: `account`/`systemuser` en Core). Aun así, el orden correcto por dependencia de datos/lógica es:

```
1. CashHubCore   (tablas + roles + option sets + variables de entorno)
2. CashHubFlow   (flujos: consumen tablas de Core, y las variables de entorno de Core)
3. CashHubApps   (apps: consumen tablas de Core y flujos de Flow)
```

```bash
pac solution import --path CashHubCore.zip --publish-changes
pac solution import --path CashHubFlow.zip --publish-changes
pac solution import --path CashHubApps.zip --publish-changes
```

Importar Apps antes que Flow dejaría los botones `.Run('CH-Flow-...')` de Recounting, Digitization y Email Validation Console apuntando a flujos inexistentes en ese ambiente. Importar Flow antes que Core dejaría los flujos sin las tablas que leen/escriben (`sanic_chtblreceivedemailses`, etc.) y sin las variables de entorno (`sanic_CHEVAppUrl_ClientAdmin`, la única consumida activamente por un flujo — ver [02-flows.md](../analysis/02-flows.md) §4).

## 2. Variables de entorno a configurar por ambiente

19 variables definidas en `solutions/unpacked/core/environmentvariabledefinitions/` (una carpeta por variable, `environmentvariabledefinition.xml`). Todas son `iscustomizable=1`, `isrequired=0`, `secretstore=0` — **ninguna se guarda en Azure Key Vault**, así que no hay secretos reales acá, pero sí configuración sensible al ambiente.

### 2.1 Deben sobreescribirse en TODO ambiente que no sea el DEV de origen

| Variable | Default exportado | Por qué hay que pisarlo |
|---|---|---|
| `sanic_CHEVSharedMailboxAddress` | `no-reply-powerplatform-dev@baccredomatic.ni` | El literal `-dev-` en el dominio confirma que es la casilla de desarrollo. Si se importa a TEST/PROD sin cambiar este valor, **y** sin corregir también el hardcodeo en el trigger del flujo (ver §3), el ambiente seguiría escuchando el buzón equivocado. |
| `sanic_CHEVSharedMailboxArchiveFolderID` | `AAMkADgz...` (ID de carpeta Outlook) | Es el ID de carpeta de ARCHIVO del buzón de dev; un ID de carpeta de Exchange no es portable entre buzones/ambientes. |
| `sanic_CHEVAADGroup_Admin` | (vacío) | GUID de grupo AAD — distinto por tenant/ambiente. Sin valor, el filtrado de acceso en Hub/Client Administration para ese rol simplemente no aplica ningún filtro (ver [01-arquitectura.md](01-arquitectura.md) §6). |
| `sanic_CHEVAADGroup_ClientAdmin` | (vacío) | Ídem. |
| `sanic_CHEVAADGroup_Counting` | (vacío) | Ídem. |
| `sanic_CHEVAADGroup_EmailValidation` | (vacío) | Ídem. |
| `sanic_CHEVAADGroup_Reception` | (vacío) | Ídem. |
| `sanic_CHEVAADGroup_Supervisor` | (vacío) | Ídem. |
| `sanic_CHEVAppUrl_ClientAdmin` | (vacío) | URL de la app — Power Apps genera una URL/GUID de app nueva en cada ambiente al importar. Esta es la única variable de entorno efectivamente **consumida por un flujo** (`Notifyreviewers`, `NotifywhenRFCimplementationfinish`, vía `ListRecords` sobre `environmentvariablevalues`) — si queda vacía, los mensajes de Teams del módulo RFC quedan sin link a la app. |
| `sanic_CHEVAppUrl_Counting` | (vacío) | Ídem (aunque no se detectó consumo activo en flujos ni apps — confirmar antes de asumir que no se usa). |
| `sanic_CHEVAppUrl_EmailValidation` | (vacío) | Ídem. |
| `sanic_CHEVAppUrl_Reception` | (vacío) | Ídem. |
| `sanic_CHEVAppUrl_Supervisor` | (vacío) | Ídem. |

### 2.2 Revisar antes de asumir que el default sirve

| Variable | Default exportado | Nota |
|---|---|---|
| `sanic_CHEVIbsBranchCodeList` | (vacío) | Existe para parametrizar la identidad de agencia en la carga masiva a IBS Branch, pero **no está siendo leída** por la app Digitization (que usa valores fijos, `TIPOID`/`NACIONALIDAD`/etc. hardcodeados — ver [01-arquitectura.md](01-arquitectura.md) §4). Configurarla hoy no tiene efecto hasta que se refactorice la app para leerla. |
| `sanic_CHEVProcessingLibraryUrl` | (vacío) | Probable URL de Azure Function; no se encontró consumo confirmado en el código de flujos/apps analizado — verificar antes de asumir que un ambiente nuevo la necesita. |
| `sanic_CHEVCsvExportLibraryUrl` | (vacío) | Ídem, no se confirmó consumo. |
| `sanic_CHEVEmailSubjectToAccept` | (vacío) | Debería alinearse con el asunto real que el cliente usa al enviar el Excel — VR03 (si se llegara a activar) compara contra esto. |

### 2.3 Probablemente estables entre ambientes (mismo país/operación)

| Variable | Default exportado |
|---|---|
| `sanic_CHEVFileSizeMaxMB` | `5` |
| `sanic_CHEVTimezone` | `America/Managua` |

## 3. Connection references

19 flujos usan **13 connection references** distintas, declaradas en `solutions/unpacked/flow/Other/Customizations.xml` (`<connectionreferences>`):

| Connection reference | Conector | Uso |
|---|---|---|
| `sanic_CEADCONNOutlook365` | Office 365 Outlook | Trigger de `IngestEmails`, `Replytoclientemail` |
| `sanic_CHCRDataverse` | Dataverse | Mayoría de flujos (CRUD sobre tablas Core) |
| `sanic_AEDDConnDataverse` | Dataverse | Segunda conexión Dataverse en `Notifyreviewers` (revisar si es necesaria o duplicado sin reconciliar) |
| `sanic_CHCROneDrive` | OneDrive for Business | `ExtractedTablesFromExcel` |
| `sanic_AEDDConnOneDrive` | OneDrive for Business | `DownloadData` |
| `new_sharedonedriveforbusiness_a6c30` | OneDrive for Business | `ProcessXMLFiles` |
| `sanic_CEADCONNExcelOnlineBusiness` | Excel Online Business | `ExtractedTablesFromExcel` |
| `sanic_sharedcommondataserviceforapps_025cb` | Dataverse | `ProcessXMLFiles` |
| `new_sharedteams_084e4` | Microsoft Teams | `Notifyreviewers` |
| `sanic_sharedoffice365groups_cff38` | Office 365 Groups | `CreateaTeamschatgroupbasedonOffice365Group` |
| `sanic_sharedoffice365users_3b441` / `_dd86c` | Office 365 Users | `CreateaTeamschatgroupbasedonOffice365Group` (dos referencias) |

Cada una necesita reconectarse (`pac connection` / UI de Power Automate) al importar en un ambiente nuevo — `pac solution import` no crea las conexiones automáticamente, solo las connection references (que quedan sin `connectionid` hasta que alguien las apunta a una conexión real). Varios flujos usan `runtimeSource: invoker` (p. ej. `Replytoclientemail`, `ExtractedTablesFromExcel`) — la conexión la aporta el usuario que ejecuta el flujo, no una cuenta de servicio; tenerlo en cuenta al decidir qué cuenta necesita permisos de Outlook/OneDrive en el ambiente destino.

## 4. Activación de flujos

**`CH-Flow-IngestEmails` está exportado en `StateCode=0 / StatusCode=1` (Draft/Off)** — confirmado en `solutions/unpacked/flow/Workflows/CH-Flow-IngestEmails-4FDD7239-309E-F011-BBD2-7CED8D1ED971.json.data.xml`. Es el único de los 19 flujos sin activar; todos los demás importan como activados (`StateCode=1`).

Si se importa el paquete tal cual a un ambiente nuevo, **el punto de entrada de todo el sistema (recepción de correos) queda apagado** y hay que activarlo manualmente después de importar:

```bash
# Activar tras importar (vía UI de Power Automate, o pac si el flujo expone un comando de activación en tu versión de pac)
```

Antes de activarlo, confirmar que `sanic_CHEVSharedMailboxAddress` ya esté seteado al buzón correcto del ambiente (§2.1) — el trigger de `IngestEmails` tiene el buzón **hardcodeado directamente**, no lee la variable de entorno, así que corregir solo la variable de entorno no alcanza: hay que editar el trigger del flujo (o completar el refactor pendiente) para que la apunte.

## 5. Grupos AAD requeridos

Antes de que el control de acceso data-driven del Hub y de Client Administration tenga efecto, deben existir en el tenant y estar poblados con los usuarios correctos:

- Grupo mapeado a `sanic_CHEVAADGroup_Admin`
- Grupo mapeado a `sanic_CHEVAADGroup_ClientAdmin`
- Grupo mapeado a `sanic_CHEVAADGroup_Counting`
- Grupo mapeado a `sanic_CHEVAADGroup_EmailValidation`
- Grupo mapeado a `sanic_CHEVAADGroup_Reception`
- Grupo mapeado a `sanic_CHEVAADGroup_Supervisor`

Además, para el módulo de RFC de cambios de cliente, cada fila de `sanic_CHTblClientDataChangePermission` referencia un `AADGroupID` (GUID) por tabla objetivo (Cliente/Sucursal/Email) y tipo (Contributor/Approver) — estos grupos son datos de configuración de negocio, no variables de entorno, y hay que cargarlos manualmente (o vía script de datos) después de importar Core.

**Recordatorio de alcance real**: como se documenta en [03-apps-canvas.md](03-apps-canvas.md) §1, solo el Hub y Client Administration verifican grupo AAD en su código Power Fx. Configurar estos grupos no habilita control de acceso en Reception, Recounting, Supervisor, Digitization ni Deposits Tracker — ahí la seguridad depende exclusivamente de compartir la app y de los roles de seguridad Dataverse (con las brechas descritas en [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md)).

## 6. App User para despliegue automatizado

Para automatizar `pac solution import`/`export` vía pipeline (en vez de un usuario interactivo), Dataverse requiere un **Application User** (Service Principal / App Registration en Entra ID) con:

- Rol de seguridad **System Administrator** (para import/export de soluciones sin restricciones de privilegio), o al menos **System Customizer** si se quiere acotar el alcance y la pipeline no necesita administrar seguridad/usuarios.
- Acceso concedido explícitamente en el ambiente destino (`Configuración avanzada → Usuarios de la aplicación`), no alcanza con crear el App Registration en Entra ID.

Autenticación desde `pac` con Service Principal:

```bash
pac auth create \
  --url https://<ambiente>.crm.dynamics.com \
  --applicationId $CH_APP_ID \
  --clientSecret $CH_CLIENT_SECRET \
  --tenant $CH_TENANT_ID
```

**Las credenciales (`CH_APP_ID`, `CH_CLIENT_SECRET`, `CH_TENANT_ID`) van en un archivo `.env` local, NUNCA versionado** — confirmado que `.env` y `.env.*` están en [`.gitignore`](../../.gitignore) junto con `*.pem`/`*.pfx`. En un pipeline de CI/CD, estos mismos valores van como secretos del pipeline (GitHub Actions secrets / Azure DevOps variable groups marcados como secret), no como archivo en el repo.

## 7. Checklist de despliegue a un ambiente nuevo

1. [ ] Crear/verificar el Application User con rol System Administrator/Customizer en el ambiente destino.
2. [ ] Configurar `.env` local (o secretos de pipeline) con `applicationId`/`clientSecret`/`tenant` — nunca commitear.
3. [ ] `pac solution import` en orden: Core → Flow → Apps (§1).
4. [ ] Reconectar las 13 connection references de Flow (§3) a conexiones reales del ambiente.
5. [ ] Sobreescribir las 11 variables de entorno críticas de §2.1 (buzón, carpeta de archivo, 6 grupos AAD, 4 URLs de app — ver también las 5 App Url si aplican todas).
6. [ ] Crear/poblar los 6 grupos AAD (§5) y las filas de `sanic_CHTblClientDataChangePermission` con sus GUIDs reales.
7. [ ] Activar `CH-Flow-IngestEmails` manualmente (§4) — recordar que primero hay que resolver el hardcodeo del buzón en el trigger, no solo la variable de entorno.
8. [ ] Verificar los 6 Security Roles (`CH-SR-*`/`CH-Role`) y asignarlos a los usuarios reales — atención a los 3 roles sin privilegios sobre tablas custom (`CH-Role`, `AnalyticsRead`, `OpsAdmin`), documentados en [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md).
9. [ ] Smoke test: enviar un correo de prueba al buzón configurado y seguir el flujo hasta Email Validation Console.

## 8. Ver también

- [02-estructura-y-build.md](02-estructura-y-build.md) — comandos `pac solution`/`canvas` unpack/pack usados en este ciclo.
- [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md) — hallazgos de seguridad y operación que condicionan qué tan seguro es desplegar tal cual está el paquete hoy.
