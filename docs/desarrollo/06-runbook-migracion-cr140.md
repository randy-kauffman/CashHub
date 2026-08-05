# Runbook: migración de `cr140_changestatus` → `sanic_changestatus`

> Estado 2026-08-04: **pasos 1-3 EJECUTADOS en DEV**. Core 1.1.0.0 importado (campo y roles
> verificados vía Web API), datos migrados (0 registros existentes), Apps 1.1.0.0 importado
> (incluye el fix de auto-aprobación), y la model-driven purgada del ambiente (12 appactions
> con sus reglas desasociadas —no eliminadas, son compartidas—, appmodule, sitemap, command
> library y los 8 web resources). **Pendiente: paso 4 (Studio, manual) y paso 5 (importar
> Flow, bloqueado hasta completar el 4).** Los flujos del ambiente siguen en cr140 mientras tanto.

## Por qué

El campo que controla el ciclo de vida de todo el subsistema RFC (`Draft → Submitted →
Approved → Applied/Failed`) nació con el prefijo `cr140_` de un publisher ajeno a la solución
(ver [deep-dive §2.4](../analysis/05-client-administration-deep-dive.md)). Riesgo de ALM en cada
despliegue y valores del optionset mezclando rangos de dos publishers (el viejo `Canceled` vale
`159460001`, que colisiona con el `Submitted` del rango sanic).

## Qué ya está hecho en el fuente (este repo)

| Pieza | Estado | Dónde |
|---|---|---|
| Campo nuevo `sanic_changestatus` (display "Change Status", valores `159460000-159460006` alineados con la línea del RFC) | ✅ en fuente | `solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequest/Entity.xml` |
| 6 flujos apuntando al campo/valores nuevos | ✅ en fuente | `Notifyreviewers`, `Activaterow`, `Deactivaterow`, `Createclientdata`, `Applyclientdatachanges`, `NotifywhenRFCimplementationfinish` |
| Script de migración de datos con mapeo explícito | ✅ | `scripts/migrate-cr140-changestatus.sh` |
| Canvas app | ❌ **queda en `cr140` a propósito** — se migra en Studio (paso 4) | ver censo abajo |

La canvas app NO se migró offline a propósito: su esquema cacheado (`pkgs/TableDefinitions`)
son blobs de metadata del servidor que solo Studio regenera de forma confiable. Editarlos a mano
arriesga corromper el paquete.

## Mapeo de valores (¡nunca copiar el número crudo!)

| Estado | Viejo (`cr140_changestatus`) | Nuevo (`sanic_changestatus`) |
|---|---|---|
| Draft | 796490000 | 159460000 |
| Submited → Submitted | 796490001 | 159460001 |
| Approved | 796490002 | 159460002 |
| Rejected | 796490003 | 159460003 |
| Applied | 796490004 | 159460004 |
| Failed | 796490005 | 159460005 |
| Canceled | **159460001** ⚠ | 159460006 |

⚠ El viejo `Canceled` (159460001) colisiona numéricamente con el nuevo `Submitted`. El script
mapea explícitamente valor por valor.

## Orden de ejecución (ventana de mantenimiento — nadie usando Client Administration)

1. **Importar Core** (trae el campo nuevo; el viejo sigue existiendo — la migración es aditiva).
2. **Ejecutar `scripts/migrate-cr140-changestatus.sh --dry-run`**, revisar salida, y correrlo
   sin `--dry-run` (copia y mapea los valores de todos los RFCs existentes).
3. **NO importar Flow todavía.** Los flujos viejos del ambiente siguen operando sobre `cr140`
   mientras tanto.
4. **Migrar la canvas app en Studio** (Client Administration):
   1. Refrescar el origen de datos `CH-Tbl-ClientDataChangeRequests` (esto trae el campo nuevo
      al esquema cacheado).
   2. Reemplazar TODAS las referencias — censo exacto del fuente actual:
      - `'ChangeStatus (CH-Tbl-ClientDataChangeRequests)'.X` → `'Change Status (CH-Tbl-ClientDataChangeRequests)'.X`
        (28 ocurrencias: ChangeRequestDetails ×13, ClientInformation ×9, Clients ×5, RFC ×1).
        Ojo: el label viejo `Submited` pasa a `Submitted` con doble t en el optionset nuevo.
      - Referencias directas al nombre lógico `cr140_changestatus` (ColName de grillas y
        similares): ChangeRequestDetails ×2, ClientInformation ×5, Clients ×2, RFC ×2 →
        `sanic_changestatus`.
   3. Publicar la app.
5. **Importar Flow** (los flujos pasan a escuchar el campo nuevo).
6. **Verificación end-to-end**: crear RFC de prueba → Submit (dispara Notifyreviewers) →
   aprobar con otro usuario → verificar que el flujo materializa y marca `Applied` en
   `sanic_changestatus` → verificar notificación de Teams.
7. **Exportar la app migrada y re-sincronizar este repo** (`pac solution export` + unpack +
   commit) para que el fuente refleje la app post-Studio.
8. Etapa posterior (una vez estable): eliminar el campo `cr140_changestatus` y su optionset
   del ambiente y del fuente.

## Rollback

Hasta el paso 5, el sistema viejo sigue intacto (campo viejo + flujos viejos + app vieja).
Rollback = no importar Flow y revertir la app en Studio (restaurar versión anterior desde el
historial de versiones de la app). El campo nuevo poblado no molesta a nada.
