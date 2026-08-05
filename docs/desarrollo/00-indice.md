# Documentación de desarrollo — CashHub

Esta carpeta es la referencia técnica para quien va a **construir sobre** CashHub: arquitectura real (no la de diseño), cómo se arma/desarma el código fuente, patrones de las canvas apps, cómo desplegar entre ambientes, y el backlog de deuda técnica identificado.

Es complementaria a [`docs/analysis/`](../analysis/), que es el análisis forense as-is de las 3 soluciones (más detallado, organizado por solución). Esta carpeta reorganiza esos hallazgos por tarea de desarrollo: entender la arquitectura, hacer build, tocar una app, desplegar, priorizar mejoras.

Se complementa también con [`docs/referencia/`](../referencia/) (contratos vivos: modelo de datos, flujos, roles, choices, convenciones) y [`docs/guias/`](../guias/) (manuales por rol para el personal operativo).

## Contenido

| Documento | Para qué sirve |
|---|---|
| [01-arquitectura.md](01-arquitectura.md) | Visión de las 3 soluciones y su segmentación, el flujo de punta a punta con diagrama, y el hallazgo central: dónde vive REALMENTE la validación de correos (no es donde el nombre de los flujos sugiere). Punto de partida obligatorio antes de tocar cualquier pieza. |
| [02-estructura-y-build.md](02-estructura-y-build.md) | Layout del repo, comandos exactos de `pac solution unpack/pack` y `pac canvas unpack/pack`, versiones actuales de cada solución, y el ciclo de trabajo DEV → export → unpack → commit. |
| [03-apps-canvas.md](03-apps-canvas.md) | Patrones técnicos que se repiten (o no) entre las 8 canvas apps: detección de usuario, navegación, componentes compartidos, invocación de flujos, colecciones. Incluye tabla app × tablas Dataverse × flujos invocados. |
| [04-deploy-y-alm.md](04-deploy-y-alm.md) | Estrategia de despliegue entre ambientes: orden de importación, las 19 variables de entorno y cuáles defaults de dev hay que pisar, connection references, activación de flujos, grupos AAD, y el App User para despliegue automatizado vía `pac`. |
| [05-hallazgos-y-backlog.md](05-hallazgos-y-backlog.md) | Los hallazgos de `docs/analysis/` convertidos en backlog priorizado (Seguridad / Integridad / Operación / Calidad), con evidencia, impacto y esfuerzo estimado por ítem. |
| [06-runbook-migracion-cr140.md](06-runbook-migracion-cr140.md) | Runbook del cutover `cr140_changestatus` → `sanic_changestatus`: mapeo de valores, orden de importación, migración de datos y paso de Studio para la canvas app. |
| [07-seed-data.md](07-seed-data.md) | Datos de configuración versionados (`data/seed/`) y siembra idempotente por ambiente con `scripts/seed-data.sh`. |
| [08-motor-validacion.md](08-motor-validacion.md) | Diseño del motor de validación de correos: arquitectura de 3 piezas, contrato del Custom API, reglas paramétricas con dependencias, hallazgos con hoja/celda, detección de fallos y nuevo oficio de la Console. |

## Cómo leer esta carpeta

1. Si es tu primera vez en el proyecto: **01 → 02 → 03**.
2. Si vas a desplegar a un ambiente nuevo: **04**, con **01** como contexto.
3. Si vas a priorizar sprint de mejora técnica: **05** directo, usando **01-04** para entender el porqué de cada ítem.

## Fuentes

Todo lo afirmado en estos documentos proviene de:
- El análisis exhaustivo en [`docs/analysis/`](../analysis/) (00 a 04).
- Lectura directa del código fuente desempaquetado en [`solutions/unpacked/`](../../solutions/unpacked/) (`core/`, `flow/`, `apps/`).
- [`README.md`](../../README.md) y [`CHANGELOG.md`](../../CHANGELOG.md) de la raíz del repo.

No se inventa nada que no esté verificable en esas fuentes; donde algo no se puede confirmar, se indica explícitamente.
