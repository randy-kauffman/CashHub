# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto adhiere a [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

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
