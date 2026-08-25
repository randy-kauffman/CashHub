# Referencia — CashHub

> ⚠️ **Parcialmente desactualizada (agosto 2026).** Estos documentos se
> escribieron antes del rediseño del pipeline de correo. Los números actuales:
> **40 tablas** (no 38), **8 roles** (no 6), **21 variables de entorno** (no
> 19), **16 flujos** (no 19) y un plugin C# con 28 reglas que reemplazó al
> motor Power Fx de la Console. La referencia al día, solución por solución,
> está en [`../soluciones/`](../soluciones/00-vision-general.md); usá esta
> carpeta para el detalle fino (campos, choices, typos) sabiendo que puede
> estar corrida respecto de la fuente.

Documentación de referencia (tipo contrato) del sistema CashHub: digitalización del
procesamiento bancario de depósitos en efectivo enviados en bolsas de seguridad
(tulas) vía transportadora de valores. Reemplaza minuta física + digitación manual
por: plantilla Excel por correo → validación automática → recepción física en
esclusa → bóveda → recuento → carga masiva al core bancario **IBS Branch**.

Todo lo documentado acá sale de leer directamente los archivos fuente desempaquetados
bajo `/home/gmaker/projects/CashHub/solutions/unpacked/{core,flow,apps}/` (Entities,
Roles, OptionSets, environmentvariabledefinitions, Workflows, CanvasApps). Donde algo
no está confirmable en el código se dice explícitamente — no se inventa nada. El
análisis narrativo previo (más extenso, con hallazgos priorizados) vive en
[`../analysis/`](../analysis/00-sintesis.md); esta carpeta es la referencia normativa
y más corta, pensada para consultarse mientras se trabaja sobre el código.

## Contenido

| Documento | Qué responde |
|---|---|
| [`inventario-componentes.md`](inventario-componentes.md) | Inventario exhaustivo de TODO componente (tablas, campos, relaciones, option sets, variables de entorno, roles, plugin assembly/classes, Custom APIs, Canvas Apps, Cloud Flows) con estado de revisión por fila — base de trabajo para decidir qué se elimina, modifica o confirma. |
| [`modelo-de-datos.md`](modelo-de-datos.md) | Las 38 tablas Dataverse agrupadas por dominio funcional: propósito, campos clave, relaciones, diagrama ER. |
| [`roles-y-seguridad.md`](roles-y-seguridad.md) | Los 6 roles de seguridad `CH-SR-*`, matriz rol × entidad, grupos AAD, y las brechas de seguridad conocidas. |
| [`variables-de-entorno.md`](variables-de-entorno.md) | Las 19 variables `sanic_CHEV*`: tipo, default, quién las consume y cuáles no se usan hoy. |
| [`conexiones-y-flujos.md`](conexiones-y-flujos.md) | Los 19 flujos Power Automate: trigger, estado, conectores, cadena real de orquestación, flujos stub/bugueados. |
| [`catalogo-de-choices.md`](catalogo-de-choices.md) | Option sets globales y locales relevantes, con valores y las máquinas de estado que forman. |
| [`convenciones-nomenclatura.md`](convenciones-nomenclatura.md) | Prefijos y patrones de nombrado observados, y las excepciones/violaciones detectadas. |
| [`typos-conocidos.md`](typos-conocidos.md) | Typos grabados en `LogicalName` que no se pueden corregir sin recrear el campo, y su impacto. |

## Cómo leer estos documentos

- Cada tabla/flujo/regla referencia su ruta real bajo `solutions/unpacked/`. Ante la
  duda, el archivo fuente manda sobre este documento.
- Los nombres lógicos (`sanic_...`) están en minúscula tal como los usa Dataverse
  internamente; los nombres de esquema/visualización se citan tal como aparecen en
  el XML/JSON de origen, con sus propios errores de tipeo si los tienen.
- "Confirmado" significa verificado línea por línea contra el archivo fuente en esta
  tarea de documentación (agosto 2026). "Inferido" significa que no hay una llamada
  explícita en el código y se deduce del tipo de trigger o del nombre.
