# Seed data: datos de configuración versionados

Las soluciones de Power Platform empaquetan **esquema**, no **datos** — y CashHub no funciona
sin sus datos de configuración: catálogo de campos del RFC, permisos por área, catálogo de
apps del Hub, denominaciones de moneda. Un ambiente recién importado queda con todas esas
tablas vacías (así encontramos DEV). Este mecanismo versiona esos datos junto al código y los
siembra de forma **idempotente** en cualquier ambiente.

## Cómo funciona

- `data/seed/*.json` — un archivo por tabla, con `entitySet`, `naturalKey` (clave natural del
  upsert) y `rows`. Los archivos se procesan en orden alfabético (por eso el prefijo numérico).
- `scripts/seed-data.sh [--dry-run]` — para cada fila: si no existe por clave natural la crea;
  si existe con diferencias la actualiza; si es idéntica no toca nada. Ejecutarlo N veces da el
  mismo resultado.
- Valores especiales:
  - `"PENDIENTE*"` — la fila se **omite** con advertencia: configuración que negocio aún no
    definió (ej. GUIDs de grupos AAD). El script avisa pero no falla.
  - `"@canvasapp:<nombre>"` — se resuelve en vivo a la URL real (`appopenuri`) de la canvas
    app en el ambiente destino. Así el catálogo del Hub apunta siempre a las apps del ambiente
    correcto sin hardcodear URLs.
  - `""` vs `null`: Dataverse guarda cadena vacía como null; el script normaliza la
    comparación para no generar updates eternos.

## Datasets actuales

| Archivo | Tabla | Filas | Estado |
|---|---|---|---|
| `10-clienttablelogicalfieldnames.json` | Catálogo de campos permitidos por el RFC (Cliente: Nombre/CIFBCO/CIFCOM; Sucursal: Nombre; Correo: Nombre/Notificar siempre) | 6 | ✅ sembrado |
| `20-clientdatachangepermissions.json` | Permisos RFC por área: Banca de Empresas (cliente+correo), Bóveda (sucursal), Contributor+Approver | 6 | ⚠ **omitido — faltan los GUIDs de grupos AAD y Teams de negocio** |
| `30-appcatalogs.json` | Catálogo de apps del Hub (7 apps, URLs resueltas en vivo) | 7 | ✅ sembrado |
| `40-moneydenominations.json` | Denominaciones NIO/USD/EUR (billetes y monedas estándar) | 39 | ✅ sembrado — **negocio debe validar cuáles acepta el banco** (deshabilitar con `sanic_enabled=false`, no borrar) |

## Pendientes conocidos

1. **Permisos RFC**: reemplazar los `PENDIENTE-AAD-*` / `PENDIENTE-TEAMS-*` de
   `20-clientdatachangepermissions.json` con los GUIDs reales (4 grupos AAD: Banca
   Contributor/Approver y Bóveda Contributor/Approver; 2 grupos de Teams para notificaciones)
   y re-ejecutar el script. **Sin estas filas, nadie puede operar Client Administration.**
2. **Reglas de validación** (`sanic_validationruleses`): la tabla tiene estructura paramétrica
   (nombre, orden, operación, valor, mensaje, activa) pero su contrato con la Email Validation
   Console se define en la fase de validación de correos — se sembrará entonces.
3. Tablas maestras operativas (clientes, transportadoras, mesas, ubicaciones) NO van en seed:
   son datos de negocio, no configuración.

## Uso

```bash
./scripts/seed-data.sh --dry-run   # ver qué haría
./scripts/seed-data.sh             # sembrar
```

Requiere `pp_secrets.env` en la raíz (ver [04-deploy-y-alm.md](04-deploy-y-alm.md)).
