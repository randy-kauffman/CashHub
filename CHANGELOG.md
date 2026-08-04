# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto adhiere a [Versionado Semántico](https://semver.org/lang/es/).

## [Unreleased]

## [0.1.0] - 2026-08-04

### Added

- Fuente desempaquetado de las tres soluciones de Power Platform (`solutions/unpacked/`):
  - CashHubCore 1.0.0.5 — 38 tablas, 6 roles de seguridad, 3 option sets globales, 19 variables de entorno.
  - CashHubFlow 1.0.0.6 — 19 flujos de Power Automate.
  - CashHubApps 1.0.0.12 — 8 canvas apps por rol, 1 model-driven app y bibliotecas de componentes, con fuente YAML extraído de cada `.msapp`.
- Análisis técnico inicial del sistema as-is (`docs/analysis/`): síntesis de punta a punta, modelo de datos, flujos, apps operativas y administrativas, con hallazgos priorizados de seguridad, integridad de datos y calidad.
- Documentación por audiencia siguiendo el modelo del repo sigil: `docs/referencia/`, `docs/desarrollo/` y `docs/guias/`.
- Estructura base del repositorio: README, CHANGELOG y .gitignore.
