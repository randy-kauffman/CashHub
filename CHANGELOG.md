# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto adhiere a [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

## [0.4.0] - 2026-08-05

### Added

- Mecanismo de seed data: datos de configuración versionados en `data/seed/` y siembra idempotente por ambiente con `scripts/seed-data.sh` (upsert por clave natural, resolución en vivo de URLs de canvas apps, omisión con advertencia de configuración pendiente).
- Datasets sembrados en DEV: catálogo de campos del RFC (6), catálogo de apps del Hub (7, con URLs reales del ambiente), denominaciones NIO/USD/EUR (39). Los permisos RFC por área (6) quedan definidos pero pendientes de los GUIDs de grupos AAD/Teams de negocio.
- Documentación en `docs/desarrollo/07-seed-data.md`.

## [0.3.0] - 2026-08-05

Tanda 2 de mejoras de Client Administration (robustez del motor RFC). Soluciones en 1.2.0.0.

### Added

- `CH-Flow-RFCWatchdog`: flujo programado (cada 15 min) que detecta RFCs en `Approved` más allá del timeout configurado y los marca `Failed` con auditoría; la notificación de Teams sale automáticamente vía `NotifywhenRFCimplementationfinish`.
- Variable de entorno `sanic_CHEVRfcApplyTimeoutMinutes` (default 30) para el timeout del watchdog.
- Caso `OptionSet` implementado en la conversión de tipos de `Createclientdata`/`Applyclientdatachanges` (contrato: `sanic_optionsetmap` = JSON etiqueta→valor).

### Changed

- `Createclientdata`/`Applyclientdatachanges` reestructurados con patrón Try/Catch: cualquier fallo — incluido el primer paso — marca el RFC `Failed` con auditoría (antes quedaba en `Approved` para siempre, sin notificación).
- Guard de líneas: un RFC aprobado sin líneas de cambio pasa a `Failed` sin crear el registro destino (verificado E2E: antes creaba un registro vacío y marcaba `Applied`).
- `retryPolicy` exponencial explícito en las 47 acciones Dataverse de los 4 flujos de materialización.
- Conflict-check de la canvas app corregido: exige mismo campo + mismo registro destino + RFC distinto (antes bloqueaba RFCs de clientes sin relación entre sí).

### Fixed

- Restaurado el RootComponent de la canvas app Client Administration (eliminado por error en la limpieza de la model-driven) y purgadas las asociaciones `appaction_appactionrule_classicrulesset` residuales de `Customizations.xml` que rompían el import.

## [0.2.0] - 2026-08-04

Tanda 1 de mejoras de Client Administration (seguridad). Soluciones en 1.1.0.0.

### Added

- Roles de seguridad `CH-SR-ClientAdminContributor` y `CH-SR-ClientAdminApprover` con privilegios explícitos sobre las 9 tablas del dominio cliente: solo lectura sobre los maestros (escribe únicamente la cuenta de servicio), creación/edición de solicitudes propias para Contributor y edición global de solicitudes para Approver.
- Campo `sanic_changestatus` en la cabecera del RFC (reemplaza a `cr140_changestatus`, de publisher ajeno), con valores alineados al esquema de la línea.
- Runbook de migración `docs/desarrollo/06-runbook-migracion-cr140.md` y script `scripts/migrate-cr140-changestatus.sh` con mapeo explícito de valores.

### Changed

- Los botones Aprobar/Rechazar de la canvas app Client Administration ahora exigen que el aprobador sea distinto de quien creó la solicitud (cierra la auto-aprobación de RFCs).
- Los 6 flujos del subsistema RFC pasan a operar sobre `sanic_changestatus` (cutover coordinado según runbook).

### Removed

- Model-driven app de Client Administration completa (decisión de negocio): AppModule, sitemap, command library y los 9 web resources de formulario (todos con validaciones desconectadas); limpiado el Quick Create de la línea de RFC que los referenciaba.

## [0.1.0] - 2026-08-04

### Added

- Fuente desempaquetado de las tres soluciones de Power Platform (`solutions/unpacked/`):
  - CashHubCore 1.0.0.5 — 38 tablas, 6 roles de seguridad, 3 option sets globales, 19 variables de entorno.
  - CashHubFlow 1.0.0.6 — 19 flujos de Power Automate.
  - CashHubApps 1.0.0.12 — 8 canvas apps por rol, 1 model-driven app y bibliotecas de componentes, con fuente YAML extraído de cada `.msapp`.
- Análisis técnico inicial del sistema as-is (`docs/analysis/`): síntesis de punta a punta, modelo de datos, flujos, apps operativas y administrativas, con hallazgos priorizados de seguridad, integridad de datos y calidad.
- Documentación por audiencia siguiendo el modelo del repo sigil: `docs/referencia/`, `docs/desarrollo/` y `docs/guias/`.
- Estructura base del repositorio: README, CHANGELOG y .gitignore.
