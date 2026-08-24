# Guía 06 — Backend de CH-App-Digitization v2

Track de `CashHubCore` que las guías de pantalla asumen (con fallback declarado).
Todo con **Strict TDD** (dominio puro `CashHub.Validation` + tests xUnit; los
plugins-adapter no se testean, clonan patrón). Registro por los **scripts bash+Web
API v9.2** del repo (no `pac plugin`/spkl). Assembly: `dotnet build -c Release` →
re-subida completa por `register-*.sh` (el nombre `CashHub.Plugins` es la identidad,
no hay bump de versión de DLL).

## Estado

| # | Pieza | Estado | Archivos |
|---|---|---|---|
| B1 | Columnas nuevas (motivo + autoría cheques) | ✅ **hecho** | `scripts/create-digitization-columns.sh`, `Schema.cs` |
| B2 | Estampa de autoría de cheques | ✅ **hecho** | `CheckStatusStampPlugin.cs` (+ re-registro sin cambios de step) |
| B3 | Custom API `sanic_CHRemoveFromCut` (sacar del lote + motivo) | ✅ **hecho** | `TransactionGuard.cs`+tests, `CHRemoveFromCutPlugin.cs`, `Schema.cs`, `register-remove-from-cut.sh` |
| B4 | Custom API `sanic_CHWorkloadByCashier` (rail agregado) | ✅ **hecho** | `CHWorkloadByCashierPlugin.cs`, `Schema.cs`, `register-workload.sh` |
| B5 | Custom API `sanic_CHPreviewTransactionCut` (conteo del corte) | ✅ **hecho** | `CHPreviewTransactionCutPlugin.cs`, `Schema.cs`, `register-preview-cut.sh` |
| S1 | Fix V-1 (`executeprivilegename` en el corte) | ✅ **hecho** | `register-transaction-cut.sh` |
| S2 | Fix V-3 (validación + `invoker` + descarga local) | ✅ **hecho** | `CH-Flow-DownloadData-*.json`, guía 04 §6 |
| S3 | Fix V-6 (rol Write Global → Local + trace log) | ✅ **hecho** | `core/Roles/CH-SR-Digitizer.xml` |
| S4 | V-2 (conexión SP → usuario) | 📋 **recomendación de config** (no código) | scaffolding del usuario |

## B1 — Columnas nuevas ✅

`scripts/create-digitization-columns.sh` (clona `create-recounting-mirror-columns.sh`:
Web API v9.2 + header `MSCRM.SolutionUniqueName: CashHubCore` + `PublishXml`,
idempotente). Crea:

| Columna | Tabla | Tipo | Escrita por |
|---|---|---|---|
| `sanic_motivorechazocore` | Transactions | Memo 2000 | `sanic_CHRemoveFromCut` (B3) |
| `sanic_senttodigitizationby` | Checks | Lookup → systemuser | `CheckStatusStampPlugin` (B2) |
| `sanic_receivedby` | Checks | Lookup → systemuser | `CheckStatusStampPlugin` (B2) |

Constantes en `Schema.cs`: `Transaction.MotivoRechazoCore`, `Check.SentToDigitizationBy`,
`Check.ReceivedBy`. Los dos lookups llevan "No editar a mano." (autoría derivada del
actor); el memo es dato de negocio persistido una vez.

**Deploy**: `./scripts/create-digitization-columns.sh` (crea + publica). No requiere el
assembly; el mantenimiento en vivo de los lookups llega con B2 desplegado.

## B2 — Estampa de autoría de cheques ✅

`CheckStatusStampPlugin` extendido: junto al sello de fecha que ya ponía, estampa el
**actor** (`context.InitiatingUserId`) en el Target:

- `'Enviado a digitacion'` → `sanic_senttodigitizationon` + **`sanic_senttodigitizationby`** (Supervisor)
- `Recibido` → `sanic_receivedon` + **`sanic_receivedby`** (Digitador)
- `No Recibido` → `sanic_notreceivedon` + **`sanic_receivedby`** (Digitador)

No cambia el registro del step (`register-check-stamp.sh`): sigue Pre-op stage 20,
filtrado en `sanic_checkstatus`, escribiendo en el Target. Solo hay que **re-subir el
assembly** (`dotnet build -c Release` + correr el `register-check-stamp.sh` para el
upsert del `pluginassemblies`). Backfill de autoría histórica: opcional, por auditoría
(no bloquea la app — los cheques nuevos ya nacen con autor).

## B3 — `sanic_CHRemoveFromCut` (sacar del lote) ⏳

Reemplaza el `Patch('Transaction Batch': Blank())` client-side (P-5, W-3, C-1).

- **Dominio** (`CashHub.Validation/Security/`): regla pura `CanRemoveFromCut(batchStatus)`
  → permite en **`Generado` o `Ingresado`** (decisión del usuario 2026-08-23: los rechazos
  del Core ocurren con el lote ya Ingresado; alineado con `TransactionGuard` que ya lo
  permite). Tests xUnit (Generado/Ingresado→permite, Aplicado/Aprobado/null→bloquea).
- **Plugin** (clona `CreateTransactionCutPlugin`): Input `TransactionId` (Guid, req),
  `Motivo` (String, opcional); valida contra el dominio y contra el estado real del
  lote (relee como sistema); desasigna (`sanic_transactionbatch = null` → el mirror
  plugin la vuelve a Manual por F3); escribe `sanic_motivorechazocore = Motivo`;
  `BatchTotalsCalculator.Recompute(...)`; Output `TotalNIO/USD/EUR` nuevos. Doble
  identidad de servicio como el corte.
- **Registro** (clona `register-transaction-cut.sh`) **con `executeprivilegename`**
  desde el nacimiento (no repetir el hueco V-1).

## B4 — `sanic_CHWorkloadByCashier` (rail de cajeros) ⏳

Agregado por cajero que alimenta el rail (receta 7.7). `GroupBy` no delega en canvas.

- **Plugin**: FetchXML aggregate (como `BatchTotalsCalculator`) sobre Transactions
  agrupando por `createdby`, contando pendientes de corte (Batch, sin lote, sin status)
  y de manual (Manual, sin lote, sin status), y sumando totales; + lotes por aplicar
  agrupando TransactionBatches por `sanic_cajero` en Generado/Ingresado. Devuelve un
  string JSON o una EntityCollection con `{cajero, cajeroNombre, mesa, pendientesCorte,
  pendientesManual, lotesPorAplicar, totalNio/usd/eur}`.
- **Shape** = el que consumen 02/03/04 (rail). Ordenable por cantidad o nombre en la app.
- **Registro** con `executeprivilegename` (privilegio de lectura del rol Digitizer).

> Fallback sin B4 (en las guías): rail sobre `Users`, sin badge de conteo.

## B5 — `sanic_CHPreviewTransactionCut` (conteo del corte) ⏳

Dry-run del corte: cuenta los elegibles **con la misma query** que
`CreateTransactionCutPlugin` (misma semántica, incluido el filtro `createdon <= now`),
sin crear nada. Input `CajeroId`; Output `EligibleCount`, `TotalNIO/USD/EUR`. El diálogo
de confirmación de scrCorte lo llama para que el número que confirma el operador y el
que ejecuta el corte salgan de la misma fuente (mata D-2 de raíz, no solo por delegación).

> Fallback sin B5: `CountRows` delegado por `'Created By' = gvarCajero` (correcto <500 lotes).

## S1 — Fix V-1: privilegio de ejecución del corte ⏳

`scripts/register-transaction-cut.sh`: el `customapis` body hoy va **sin
`executeprivilegename`** e `isprivate: False` → cualquier usuario del entorno invoca el
corte por Web API.

**Fix**: agregar `'executeprivilegename': 'prvCreatesanic_TransactionBatches'` al body del
`customapis`. Ese privilegio ya lo tiene `CH-SR-Digitizer` (Create Basic sobre
TransactionBatches) y es exactamente el que la operación usa (crea el lote). Restringe la
API a los digitadores.

> 🧨 **NO** validar `CajeroId == InitiatingUserId` en el plugin: el digitador corta a
> nombre de OTRO (el cajero) — es el flujo de negocio correcto. La autorización va por
> `executeprivilegename` (quién puede invocar), no por igualdad de identidad. El auditor
> original propuso la igualdad; rompería el flujo. Defensa en profundidad opcional:
> trazar `InitiatingUserId` (ya lo hace) y, si se quiere, verificar pertenencia al team
> Digitizer dentro del plugin — pero el privilegio de ejecución ya es el gate real.

Aplica el mismo `executeprivilegename` a B3/B4/B5 al registrarlas.

## S2 — Fix V-3: endurecer el export CSV ✅

`CH-Flow-DownloadData-*.json` reescrito (decisión del usuario: **descarga local, nada en
la nube**):

1. **`Validar_LoteId`** (acción `If`): rechaza con `Terminate` si `text` no es un GUID
   canónico — longitud 36 + al quitar hex/guion queda vacío (sin regex, robusto en WDL).
   Corta la inyección OData / IDOR (`… or sanic_total gt 0` tiene espacios y >36 chars).
2. **`runtimeSource`: `embedded` → `invoker`** en la conexión Dataverse: el export corre
   con los privilegios del usuario real, no los del dueño de la conexión.
3. **Sin almacenamiento**: eliminadas `Create_file` (OneDrive) y `Create_share_link`
   (scope Organization) + la conexión OneDrive. El `Respond` devuelve **`csvBase64`**
   (`base64(body(Create_CSV_table))`); la app descarga con `Download("data:text/csv;…")`
   local. Cero archivos con cuentas bancarias en la nube (mata V-4 de raíz).

Deploy: `pac solution import` (los flows son fuente en el unpacked) + reactivación como
owner (patrón conocido del proyecto para cloud flows). La app consume `csvBase64` en
`btnDescargarCsvLot.OnSelect` (guía 04 §6).

## S3 — Fix V-6: acotar el rol ✅

`core/Roles/CH-SR-Digitizer.xml`: los tres `prvWrite` de dinero (Checks / Transactions /
TransactionBatches) bajados de **Global** a **Local** (BU) — el corte reasigna owner al
cajero, así que Local alcanza. Quitado `prvReadTraceLog` Global (fuga de datos operativos).

> **Nota de precaución**: `prvWorkflowExecution` (Global) es el privilegio que permite
> EJECUTAR los cloud flows del corte y del export — **NO se toca**. Los `prvCreateWorkflow`
> / `prvWriteWorkflow` (Basic) son autoría acotada; se dejaron por ahora (quitarlos exige
> verificar que no afecte el runtime de flows, y siendo Basic el riesgo es bajo). Deploy
> por `pac solution import`.

## S4 — V-2: conexión del corte (recomendación de config) 📋

La app declara una conexión Dataverse de **Service Principal**
(`sanic_SigilConnDataverseSP`, `authenticationType: ServicePrincipalOauth`). Al armar el
scaffolding v2, **usar una conexión de USUARIO** para el datasource/flow del corte, no la
SP: así el corte corre con la identidad y los privilegios del digitador real, y
`createdby`/la segregación de funciones que el guard implementa funcionan de verdad. No es
un cambio de código del repo — es qué conexión bindeás en Studio. (El flow
`CH-Flow-CreateCut` ya usa `runtimeSource: invoker`, así que con conexión de usuario queda
correcto de punta a punta.)

## Orden de deploy

1. B1 `create-digitization-columns.sh` (columnas) — no necesita assembly.
2. `dotnet build -c Release` + re-registrar B2 (`register-check-stamp.sh`) — autoría en vivo.
3. B3/B4/B5: dominio + tests (rojo→verde) → plugin → `register-*.sh` con `executeprivilegename`.
4. S1 (editar el script del corte) — re-registrar la Custom API del corte.
5. S2/S3 (flow + rol) → `pac solution import` + reactivación de flows como owner.
6. Backfill opcional de autoría de cheques (auditoría).

> **CHANGELOG**: una entrada por feature (backend + app + guías), formato Keep a
> Changelog es-ES, con el bug que mata y los archivos tocados.
