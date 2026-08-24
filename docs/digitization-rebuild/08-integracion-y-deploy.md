# Guía 08 — Integración y deploy de CH-App-Digitization v2

Estado al 2026-08-23: las 4 pantallas están **vestidas** (Open Sans, paleta BAC,
responsive, fórmulas delegables, consumos de backend) y el source **empaqueta limpio**
(`pac canvas pack` OK). Falta la **integración** (conectar la app con el backend nuevo) y
un pulido fino que se valida mejor en Studio con datos. Este documento es el runbook.

## 1. Orden de deploy (NO invertir)

El backend va **antes** que la app: la app referencia columnas y Custom APIs que tienen
que existir primero.

1. **Columnas** (no requiere assembly): `./scripts/create-digitization-columns.sh`
   (`sanic_motivorechazocore`, `sanic_senttodigitizationby`, `sanic_receivedby`).
2. **Assembly + plugins**: `dotnet build -c Release` de `CashHub.Plugins`, luego correr los
   `register-*.sh`: `register-check-stamp.sh` (B2 autoría), `register-remove-from-cut.sh`,
   `register-preview-cut.sh`, `register-workload.sh`, y `register-transaction-cut.sh` (S1).
   *(Los tests del dominio corren con `dotnet test` de `CashHub.Validation.Tests`.)*
3. **Flow + rol** (`pac solution import` de `CashHubCore`): trae el `CH-Flow-DownloadData`
   endurecido (S2) y el rol `CH-SR-Digitizer` acotado (S3). **Reactivar los cloud flows
   impersonando al owner** (patrón conocido del proyecto: el import como SP no registra el
   trigger).
4. **Conexión del corte (V-2)**: al abrir la app en Studio, bindear el corte a una
   **conexión de USUARIO** (no la de Service Principal `sanic_SigilConnDataverseSP`).
5. **Import de la app** (§3).

## 2. Conectar la app con el backend (datasources que faltan)

El scaffolding trae los datasources base (`CH-Tbl-Transactions/Checks/…`, `Users`,
`CH-Flow-CreateCut`, `CH-Flow-DownloadData`) pero **NO** las 3 Custom APIs nuevas. Antes de
que las fórmulas resuelvan, hay que **agregarlas como datasources** (Datos → Agregar datos):

| Backend | Cómo lo llama la app hoy | Acción de integración |
|---|---|---|
| `sanic_CHWorkloadByCashier` (rail) | `'sanic_CHWorkloadByCashier'.Run().Workload` | Agregar la Custom API como datasource, **o** crear un flow wrapper `CH-Flow-Workload` (clon de `CH-Flow-CreateCut`) y llamar `'CH-Flow-Workload'.Run()` |
| `sanic_CHRemoveFromCut` (sacar del lote) | `sanic_CHRemoveFromCut.Run(txId, motivo)` | idem — Custom API directa o flow wrapper `CH-Flow-RemoveFromCut` |
| `sanic_CHPreviewTransactionCut` (conteo) | `'CH-Flow-PreviewCut'.Run(cajero)` | crear el flow wrapper `CH-Flow-PreviewCut` (clon de CreateCut) **o** llamar la Custom API directa |

> **Decisión de mecanismo**: el proyecto ya usa **flows wrapper** para el corte
> (`CH-Flow-CreateCut` envuelve `sanic_CHCreateTransactionCut`). Lo consistente es crear 3
> flows wrapper análogos. La alternativa es agregar las Custom APIs unbound directo como
> acciones del conector Dataverse. **Elegir uno y unificar** los nombres de invocación en
> las fórmulas de la app.

Además: **borrar los datasources muertos** `ComboBoxSample` y `CustomGallerySample` (samples
de Studio, cero uso).

## 3. Empaquetar e importar la app

```bash
# desde el source vestido del repo
pac canvas pack --sources solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa \
  --msapp /tmp/digi.msapp --layout SourceCode
# reemplazar el .msapp en la solución, bump de Solution.xml (CashHubApps), pac solution pack, import
```
(Mismo flujo de deploy de canvas del proyecto: `pac canvas pack --overwrite` → bump
`Solution.xml` → `pac solution pack` → `import --publish-changes`.)

## 4. Pulido a validar en Studio (con datos reales)

Estos puntos quedaron marcados por los agentes de vestido; se resuelven mejor con la app
abierta y datos:

1. **Filtro por cajero — delegación** 🧨: el rail entrega `cajero` como **GUID** (de
   `ParseJSON`). Unificar los tres filtros a
   `'Created By' = LookUp(Users, <PK> = gvarCajeroX.cajero)` (Corte/Manual) y
   `Cajero = LookUp(Users, <PK> = gvarCajeroLot.cajero)` (Lotes), y **confirmar el display
   real del PK de `Users`** (`'User'` / `'Primary Key'` / `systemuserid`) y que **delegue**
   (sin banda azul). Si no delega, que `sanic_CHWorkloadByCashier` devuelva un valor
   filtrable o mantener la selección como registro `Users`.
2. **Tipos scaffoldeados**: convertir `shpMarca*` / `shpAvatar*` de **rectángulo → Botón
   clásico** en scrCorte/scrManual/scrCheques (scrLotes ya está) para que rendericen "C$" y
   las iniciales; los iconos de diálogo a **Botón**/`Icon.Cancel`. *(La conversión ya se
   probó viable — el pack la acepta.)*
3. **Orden de hermanos**: mover `shpMarca*` al inicio de su header; en scrManual, el `inp`
   va después de su label, la galería de cheques después de su encabezado, etc.
4. **Controles faltantes**: agregar en scrManual `shpGridLineaMan` (línea del encabezado) y
   `lblListaVacioMan` (estado vacío de la grilla).
5. **Typo**: renombrar `lblGCol4Lo` → `lblGCol4Lot` (scrLotes).
6. **Autoría de cheques**: cambiar el fallback `'Created By'`/`'Modified By'` por los lookups
   reales `sanic_senttodigitizationby` / `sanic_receivedby` (ya existen tras el paso 1).
7. **Cliente del cheque/tx**: `RelatedReCounterDepositID.ClientName` es placeholder —
   confirmar si el cliente es navegable barato; si no, mostrar solo la referencia del
   depósito (`DR-…`).
8. **Flujo de teclado (scrManual)**: validar en vivo el **auto-select del texto al enfocar**
   (Power Fx no tiene portapapeles; el foco/tab/enter es nativo y ya está — el auto-select
   es el único punto a confirmar).

## 5. App Checker

Al abrir en Studio, App Checker en **cero errores de delegación** (el punto 4.1 es el
crítico) y accesibilidad cubierta con los `AccessibleLabel` que las guías incluyeron.
