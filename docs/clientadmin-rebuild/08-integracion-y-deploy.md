# Guía 08 — Integración y deploy de CH-App-ClientAdministration v2

Estado al 2026-08-25: backend desplegado (Custom API `sanic_CHResolveChangeRequest` +
guard de líneas `GuardChangeRequestLinePlugin`), app v2 vestida, empaquetada e importada
(`CashHubApps 1.33.0.0`, publicada en DEV), y estrés corrido contra el guard vivo. Este
documento es el runbook — qué se desplegó, en qué orden y con qué comandos exactos — y
deja registrados los pendientes conocidos para la siguiente sesión de limpieza.

## 1. Qué se desplegó

| Pieza | Contenido | Reemplaza |
|---|---|---|
| Custom API `sanic_CHResolveChangeRequest` | Backing `ResolveChangeRequestPlugin` (`CashHub.Plugins`); resuelve Approve/Reject de un RFC de forma síncrona y transaccional — valida, materializa el maestro, escribe bitácora y estado final en una sola operación | `CH-Flow-Applyclientdatachanges`, `CH-Flow-Createclientdata`, `CH-Flow-Activaterow`, `CH-Flow-Deactivaterow` |
| Guard `GuardChangeRequestLinePlugin` | Pre-op síncrono en Create/Update/Delete de `sanic_chtblclientdatachangerequestline`: RFC padre en Draft, sin líneas duplicadas por `sanic_fielddefinition`, solo creador o Approver de la tabla objetivo; exento por profundidad (`depth > 1`) para la propia escritura de `ResolveChangeRequestPlugin` | Cierra un hueco que nunca tuvo guard (hallazgo S3 del análisis) |
| Dominio puro nuevo | `ChangeRequestResolution`, `FieldValueConverter` (7 conversiones de tipo de dato), `ChangeRequestLineGuard` en `CashHub.Validation.Security` — 56 tests nuevos, suite `CashHub.Validation.Tests` completa en **680/680 verde** | El `Switch` por tipo de dato duplicado que tenían los 2 flujos aplicadores viejos |
| App `CH-App-ClientAdministration v2` | 5 pantallas (`scrSplash`, `scrClientes`, `scrCliente`, `scrSolicitudes`, `scrSolicitud`), controles clásicos nativos, componentes locales `cmpAppHeader`/`cmpConfirmDialog` de `CH-CL-SharedComponents`, sin Power CAT ni PCF, responsive horizontal. `CashHubApps 1.33.0.0`, publicada en DEV | La app v1 (mismo registro de app, `sanic_chappclientadministration_fd97f` — v2 reemplaza en el mismo id, no convive como app separada) |
| `CH-Flow-Applyclientdatachanges`, `CH-Flow-Createclientdata`, `CH-Flow-Activaterow`, `CH-Flow-Deactivaterow`, `CH-Flow-RFCWatchdog` | **Retirados** — desactivados en `CashHubFlow`, no eliminados | — |
| `CH-Flow-Notifyreviewers`, `CH-Flow-NotifywhenRFCimplementationfinish`, `CH-Flow-CreateaTeamschatgroup…` | **Vigentes, sin cambios de trigger** — la Custom API escribe los mismos estados (`Submitted` para el primero; `Applied`/`Rejected` para el segundo) que antes escribían los flujos aplicadores | — |

Ver el análisis completo en
[00-analisis-y-plan.md](00-analisis-y-plan.md) y el detalle de proceso en
[docs/procesos/01-administracion-datos-clientes.md](../procesos/01-administracion-datos-clientes.md).

## 2. Orden de deploy (NO invertir)

El backend va **antes** que la app: la app llama la Custom API por nombre y, si no
existe todavía, el datasource `Environment` no resuelve.

1. **Tests del dominio puro** (no requiere assembly ni conexión): `dotnet test
   src/backend/CashHub.Validation.Tests` — confirmar 680/680 verde antes de tocar el
   ambiente.
2. **Build Release del assembly**:
   ```bash
   dotnet build src/backend/CashHub.Plugins -c Release
   ```
   Genera `src/backend/CashHub.Plugins/bin/Release/net462/CashHub.Plugins.dll`, el
   binario que suben los dos scripts de registro siguientes.
3. **Registrar la Custom API** (idempotente):
   ```bash
   ./scripts/register-resolve-changerequest.sh
   ```
   Re-sube el `pluginassembly`, crea/actualiza el `plugintype`
   `CashHub.Plugins.ResolveChangeRequestPlugin` y la definición completa de
   `sanic_CHResolveChangeRequest` (parámetros `ChangeRequestId`/`Decision`/`Comment`,
   respuestas `Outcome`/`Message`, `executeprivilegename
   prvWritesanic_CHTblClientDataChangeRequest`).
4. **Registrar el guard de líneas** (idempotente):
   ```bash
   ./scripts/register-changerequestline-guard.sh
   ```
   Re-sube el mismo assembly, crea/actualiza el `plugintype`
   `CashHub.Plugins.GuardChangeRequestLinePlugin` y los 3 pasos síncronos
   Pre-operation (Create/Update/Delete) sobre
   `sanic_chtblclientdatachangerequestline`, con pre-image
   (`sanic_changerequestid,sanic_fielddefinition`) en Update y Delete.
5. **Desactivar los 4 flujos materializadores + el watchdog** (si no se hizo ya al
   cerrar la fase de backend): confirmar su estado con
   ```bash
   ./scripts/plugin-steps.sh list
   ```
   Este comando lista los steps del propio assembly `CashHub.Plugins` (los guards y
   la Custom API), no los flujos de Power Automate — el apagado de
   `Applyclientdatachanges`/`Createclientdata`/`Activaterow`/`Deactivaterow`/`RFCWatchdog`
   se hace desde make.powerautomate.com (Desactivar) o por `pac solution` sobre
   `CashHubFlow`, no por este script. Confirmar que quedaron OFF antes de habilitar la
   app v2 en producción — con los dos caminos activos a la vez, un RFC aprobado por la
   app dispara la Custom API Y (si alguien reactivara un flujo) un webhook duplicado.
6. **Import de la app** (§3).
7. **Seeder de estrés** (§4), opcional pero recomendado antes de dar por cerrado el
   deploy — corre contra los guards ya activos y confirma que el circuito completo
   (crear → enviar → resolver) funciona a volumen.

## 3. Empaquetar e importar la app

```bash
# desde el source vestido del repo
pac canvas pack --sources solutions/unpacked/apps/CanvasApps/src/sanic_chappclientadministration_fd97f \
  --msapp /tmp/clientadmin.msapp --layout SourceCode
# reemplazar el .msapp en la solución, bump de Solution.xml (CashHubApps a 1.33.0.0),
# pac solution pack, import
```
(Mismo flujo de deploy de canvas del proyecto: `pac canvas pack --overwrite` → bump
`Solution.xml` → `pac solution pack` → `import --publish-changes`.)

Antes de empaquetar, en Studio: agregar el datasource **Environment** (Datos → Agregar
datos → buscar "Environment") si todavía no está en el `.msapp` — es el datasource que
resuelve `Environment.sanic_CHResolveChangeRequest(...)`; sin él, la fórmula de
Aprobar/Rechazar no compila.

## 4. Seeder de estrés

```bash
python3 scripts/seed-clientadmin-stress.py --dry-run   # plan, cero escrituras
python3 scripts/seed-clientadmin-stress.py --all       # ~1.500 clientes + hijos + 600 RFC
python3 scripts/seed-clientadmin-stress.py --wipe      # limpieza, por marcador (nunca cruza tablas sin filtro)
```

Respeta los guards **por construcción** — no los apaga: la secuencia obligatoria es
cabecera en Draft → crear líneas → PATCH cabecera a Submitted (nunca al revés, porque
`GuardChangeRequestLinePlugin` exige el RFC padre en Draft para tocar sus líneas). Para
sembrar el **historial ya resuelto** (RFC en Applied/Failed con su bitácora), usa
`MSCRM.BypassCustomPluginExecution: true` — únicamente para esas transiciones de
sistema, nunca para las escrituras que el guard debe validar de verdad. Ver el
encabezado del script para el detalle completo de marcadores por tabla y las fases
`--only-clients` / `--only-rfcs`.

## 5. Pendientes conocidos

1. ~~Borrar los 3 datasources de muestra en Studio~~ — **RESUELTO COMO NO-ACCIÓN
   (2026-08-25)**: `ComboBoxSample`/`DropDownSample`/`CustomGallerySample` NO son
   basura de la v1 sino datos de plantilla que Studio auto-agrega con los controles
   clásicos (galería/dropdown) — TODAS las apps v2 del proyecto los cargan
   (verificado en digitización y reception). Removerlos por cirugía de fuente
   arriesga el `.msapp` sin ganancia: Studio los re-agrega al insertar el próximo
   control. Se aceptan como equipaje inerte del formato.
2. **Los 4 flujos materializadores + `CH-Flow-RFCWatchdog` pueden ELIMINARSE de la
   solución en una limpieza futura.** Hoy quedan desactivados pero presentes en
   `CashHubCore`/`CashHubFlow` — no estorban mientras sigan OFF, pero no cumplen
   ninguna función y son candidatos directos a una poda de solución (sacarlos de
   `CashHubFlow`, re-exportar, confirmar que ningún otro componente los referencia).
3. ~~Re-exportar `CashHubApps` al repo~~ — **RESUELTO (2026-08-25)**: fuente de la
   app v2 sincronizado post-debugging (export fresco verificado idéntico) y
   `solutions/unpacked/apps/Other/Solution.xml` actualizado a `1.33.0.0`. Queda
   pendiente solo el re-export completo de `CashHubCore`/`CashHubFlow` (metadata de
   flujos con statecode desactivado) para una limpieza futura.
5. **GAP S2 — CERRADO (2026-08-25)**: `prvReadsanic_CHTblClientAccount` (Global)
   agregado a `CH-SR-ClientAdminContributor` y `CH-SR-ClientAdminApprover` — en los
   XML del repo (`solutions/unpacked/core/Roles/`) y aplicado en vivo vía
   `AddPrivilegesRole` (verificado `privilegedepthmask = 8` en ambos roles). La tab
   Cuentas de la ficha ya no depende de privilegios de plataforma. Solo lectura:
   la escritura del maestro sigue siendo exclusiva de la Custom API como sistema.
4. **QA funcional en vivo con usuarios reales** de los dos roles (Contributor y
   Approver) sobre la app v2 — el estrés valida volumen y guards, no reemplaza una
   pasada de UX con gente real tocando los 5 flujos de pantalla.
