# Contrato de formato — documentación por proceso

Todo documento de `docs/procesos/` sigue ESTA estructura, en este orden, sin
omitir secciones (si una sección no aplica, se declara "No aplica" con una
línea de porqué). Un archivo por proceso: `NN-<slug>.md`.

## Las 11 secciones obligatorias

| § | Sección | Qué responde |
|---|---|---|
| 1 | **Qué es este proceso** | Narrativa de negocio en 5-10 líneas + diagrama mermaid del flujo. Handoffs: qué recibe de otros procesos y qué entrega (con link al proceso vecino). |
| 2 | **Estados y transiciones** | Máquina de estados de cada entidad del proceso: tabla de valores (etiqueta + valor numérico `159460xxx`), quién transiciona, qué guard la defiende. |
| 3 | **Roles y acceso** | Roles `CH-SR-*`, grupos AAD, FLS, matriz quién-puede-qué. Distinguir SIEMPRE: qué verifica la app vs. qué impone el servidor. |
| 4 | **Aplicaciones** | Apps y pantallas involucradas (versión v2 donde exista), qué pantalla sirve a qué paso. |
| 5 | **Flujos Power Automate** | Por flujo: trigger, qué toca, estado ON/OFF, owner de la conexión. |
| 6 | **Tablas** | Modelo de datos del proceso. Marcar explícitamente las **columnas mantenidas por plugin** (espejos/materializadas): "Mantenido por plugin. No editar a mano." |
| 7 | **Custom APIs** | Contrato entrada/salida, `executeprivilegename`, quién la invoca. |
| 8 | **Steps de plugins** | Por step: mensaje, etapa (Pre-validation/Pre-op/Post-op), filtros, y **qué regla de negocio defiende** (no solo el registro técnico). |
| 9 | **Acciones** | Catálogo de acciones del proceso — formato fijo abajo. |
| 10 | **Supuestos y limitaciones** | Suposiciones de diseño + limitaciones conocidas + divergencias declaradas. |
| 11 | **Pruebas** | NO duplicar casos: linkear los IDs de la guía [09-plan-de-pruebas-integral](../guias/09-plan-de-pruebas-integral.md) que cubren este proceso (CA-*, CO-*, RC-*, RE-*, SU-*, DI-*, HU-*, TR-*). |

## Formato de cada Acción (§9)

Una tabla por acción, con estos 6 campos, siempre:

| Campo | Contenido |
|---|---|
| **Quién** | El rol físico que la ejecuta |
| **Cuándo** | Precondición de estado/datos |
| **Por qué / para qué** | El motivo de negocio y qué habilita después |
| **Cómo** | Pantalla → control → gesto, en orden |
| **Qué valida el servidor** | El guard/plugin/API que la defiende (la app NUNCA es la defensa real) |
| **Qué pasa si falla / deshacer** | Mensaje esperado ante rechazo y si la acción tiene deshacer (muchas NO lo tienen — decirlo) |

## Reglas del juego

1. **Fuente de verdad = código vivo del repo**: `solutions/unpacked/` (Core
   re-exportada 1.14.0.0, fresca), `src/backend/`, y las apps YAML. Los docs de
   `docs/soluciones/` y `docs/guias/` son buen punto de partida pero pueden
   estar desactualizados — ante conflicto, manda el código.
2. **CAVEAT flujos**: los estados ON/OFF de `solutions/unpacked/flow/`
   (versión 1.12.0.0 al 2026-08-24) son una foto del export, NO del entorno
   vivo: hubo reparaciones y reactivaciones posteriores por API (ver CHANGELOG:
   `Apply client data changes`). Estado de flujo que no puedas confirmar →
   escribir "verificar en vivo", jamás inventar.
3. **CAVEAT manuales**: `docs/guias/01..07` describen la semántica del proceso
   (vigente) pero varias apps fueron reconstruidas v2 (Reception, Recounting,
   Supervisor, Digitization, Hub, Tracker). Pantallas y nombres: mandan las
   guías de rebuild (`docs/*-rebuild/`) y el CHANGELOG.
4. **Referenciar, no duplicar**: choices, roles globales y modelo de datos
   completo viven en `docs/referencia/` — el proceso los linkea y solo detalla
   lo propio.
5. **Nombres**: display name Y logical name la primera vez que aparece cada
   tabla/columna (`'CH-Tbl-Bags'` / `sanic_chtblbags`). Option sets con su
   valor numérico.
6. **Nada de hechos inventados sobre el entorno vivo**: lo que no se pueda
   verificar en el repo se marca "verificar en vivo".
7. Extensión objetivo por documento: 250–500 líneas. La densidad es virtud;
   el relleno, defecto.

## Los 9 procesos

| # | Archivo | Proceso |
|---|---|---|
| 0 | `00-acceso-y-plataforma.md` | Acceso y plataforma (Hub, catálogo, grupos, roles) |
| 1 | `01-administracion-datos-clientes.md` | Administración de datos de clientes (altas, modificaciones, bajas) |
| 2 | `02-recepcion-ingesta-emails.md` | Recepción e ingesta de emails |
| 3 | `03-recepcion-tulas-esclusa.md` | Recepción de tulas en esclusa |
| 4 | `04-supervision-aprobaciones.md` | Supervisión y aprobaciones (transversal a 3 y 5) |
| 5 | `05-recuento-tulas.md` | Recuento de tulas |
| 6 | `06-digitacion-core-bancario.md` | Digitación en core bancario |
| 7 | `07-tracking-depositos.md` | Tracking sobre preguntas de clientes en depósitos |
| 8 | `08-operacion-y-soporte.md` | Operación y soporte (monitoreo, deploys, backfills) |
