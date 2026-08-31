# Análisis de las 23 variables de entorno — CashHub

> Categoría B de [`limpieza-candidatos.md`](limpieza-candidatos.md).
>
> ## ✅ EJECUTADO el 2026-08-31 — 21 de 23 eliminadas
>
> Aplicado contra DEV (`org36e60d9d`) con
> [`scripts/cleanup-env-vars.py`](../../scripts/cleanup-env-vars.py) tras confirmación explícita.
> **21 de 21 eliminadas, 0 bloqueadas.** Sobreviven exactamente dos:
> `sanic_CHEVSharedMailboxAddress` y `sanic_CHEVOpsTeamsGroupId`.
>
> Verificado después de ejecutar: `CH-Flow-MailboxSweep` sigue **Activated**, y ningún flujo
> declaraba una variable borrada como parámetro (la única referencia superviviente es el
> `$filter` por string de `CH-Flow-RFCWatchdog`, que no es dependencia dura).
>
> **Único valor perdido**: `CHEVEnvironmentURL` = `https://org36e60d9d.crm.dynamics.com/`.
> Las otras 20 no tenían registro de valor.
>
> Verificado el 2026-08-31 contra DEV (`org36e60d9d`): definiciones en
> `solutions/unpacked/core/environmentvariabledefinitions/`, valores por ambiente en la tabla
> `environmentvariablevalue`, y el contenido real de `CH-Tbl-Parameter`.

## El dato estructural que enmarca todo

**Ningún plugin C# lee `environmentvariablevalue`** — cero coincidencias en todo `src/backend/`.
En esta solución una variable de entorno solo puede consumirse **desde un cloud flow**. Eso acota
brutalmente dónde puede esconderse un uso, y es lo que hace confiable el veredicto de no-uso.

Segundo dato: **de las 23 variables, solo 3 tienen un registro de valor en DEV.** Las otras 20
caen a su `defaultvalue`, que en 14 casos está vacío.

| Variable | Valor cargado en DEV | ¿Alguien la lee? |
|---|---|---|
| `sanic_CHEVSharedMailboxAddress` | `cashhub@55xljh.onmicrosoft.com` | Sí — 4 flows |
| `sanic_CHEVOpsTeamsGroupId` | `19:db61af34…@thread.v2` | Sí — `CH-Flow-MailboxSweep` |
| `sanic_CHEVEnvironmentURL` | `https://org36e60d9d.crm.dynamics.com/` | **No** |

> **Sobre el alcance del análisis**: la ausencia de *valor* se verificó solo en DEV, y el valor es
> por ambiente — TEST o PROD podrían tenerlo cargado. Pero la ausencia de *consumidor* es a nivel
> de código, y el código es el mismo en todos los ambientes. Aunque PROD tenga valores cargados,
> **nada los lee ahí tampoco**. Ese es el hallazgo que sostiene el borrado.

## Las 3 que se usan hoy

| Variable | Consumidor | Nota |
|---|---|---|
| `CHEVSharedMailboxAddress` | 4 cloud flows, por parámetro | Viva |
| `CHEVOpsTeamsGroupId` | `CH-Flow-MailboxSweep` | Viva |
| `CHEVRfcApplyTimeoutMinutes` | `CH-Flow-RFCWatchdog` | **Acoplada a un flow apagado — ver abajo** |

**El caso `CHEVRfcApplyTimeoutMinutes`**: su único consumidor es `CH-Flow-RFCWatchdog`, que pasó
de ON a Draft después del 2026-08-24. El veredicto técnico "USADA" es correcto —el código existe
y la referencia está ahí— pero *no se ejecuta hoy*. Su destino queda atado a la decisión sobre ese
flow (categoría C). **No es una cuarta candidata: es una consecuencia de otra decisión.**

---

## Los 20 sin consumidor, agrupados por CAUSA

La causa importa más que el conteo, porque determina si el borrado es seguro o si primero hay que
entender qué se perdió en el camino.

### Grupo 1 — Reemplazadas: el consumidor migró a otro mecanismo (3)

Son las únicas tres donde hubo una **migración real y completa**. El consumidor existe, funciona,
y lee de otro lado. Borrado limpio.

| Variable | Default | Reemplazo | Prueba |
|---|---|---|---|
| `CHEVBancaMailbox` | `cashhub@55xljh.onmicrosoft.com` | `CH-Tbl-Parameter` → `Email.Banca.Mailbox` | **Mismo valor exacto** en la tabla. Leída en `screen ColaDeProcesamiento.fx.yaml:710` con `LookUp('CH-Tbl-Parameters', Key = "Email.Banca.Mailbox" …)` |
| `CHEVAppUrl_ClientAdmin` | *(vacío)* | `CH-Tbl-Parameter` → `Apps.ClientAdministration.Url` | Misma pantalla, línea 711/712, más 2 flows que filtran por OData contra esa clave |
| `CHEVFileSizeMaxMB` | `5` | Regla **VR-06** en `CH-Tbl-ValidationRules` | `EnvelopeRules.cs:107-111` aplica el tope leyendo `definition.Value` con fallback **`5120` KB = 5 MB**. El número coincide y prueba el linaje. |

> **Hallazgo de arquitectura**: este sistema tiene **tres mecanismos de configuración conviviendo**
> — variables de entorno, `CH-Tbl-Parameter` y `CH-Tbl-ValidationRules`. Las tres migraciones de
> arriba van del primero hacia los otros dos. La dirección es clara y correcta; lo que falta es
> terminarla y sacar el mecanismo viejo.

### Grupo 2 — Andamiaje nunca cableado (14)

Sin `defaultvalue`, sin valor en DEV, sin consumidor, **y el concepto no existe en el código**.
No son restos de algo que se removió: son variables que se crearon y nunca se conectaron a nada.

**Familia `CHEVAADGroup_*` (6)**: `_Admin`, `_ClientAdmin`, `_Counting`, `_EmailValidation`,
`_Reception`, `_Supervisor`.

**Familia `CHEVAppUrl_*` (4 restantes)**: `_Counting`, `_EmailValidation`, `_Reception`,
`_Supervisor`.

**Sueltas (4)**: `CHEVCsvExportLibraryUrl`, `CHEVEmailSubjectToAccept`, `CHEVIbsBranchCodeList`,
`CHEVProcessingLibraryUrl`. Para las cuatro, `rg -i` sobre el concepto (no solo el nombre de la
variable) en `src/backend/`, `scripts/` y los flows da **cero coincidencias**.

> **Ojo con la familia AAD**: `CH-Tbl-Parameter` sí tiene grupos AAD reales —
> `Security.ApproverGroup.Supervisor`, `.Boveda`, `.Banca`, con GUIDs cargados. O sea que el
> concepto "grupo AAD parametrizado" **existe y funciona**, pero con otros nombres y otro recorte
> (por rol de aprobación, no por app). No es una migración 1:1 de estas 6: es que el problema se
> resolvió distinto y estas quedaron colgadas.

### Grupo 3 — Con valor por defecto plausible, pero el concepto no existe (2)

| Variable | Default | Situación |
|---|---|---|
| `CHEVTimezone` | `America/Managua` | Ningún manejo de zona horaria en el código. Cero coincidencias de `Timezone`/`TimeZone` en `src/backend/`, `scripts/` y flows. |
| `CHEVSharedMailboxArchiveFolderID` | `AAMkADgzNTc3NDg0…` | Ningún archivado de correo implementado. Cero coincidencias de `ArchiveFolder`. |

Los dos tienen defaults que parecen reales —una zona horaria correcta para Nicaragua, un id de
carpeta de Exchange con formato válido— lo que sugiere que alguien los configuró pensando en usarlos.
Nunca se usaron.

### Grupo 4 — Configurada a propósito y sin lector (1)

`CHEVEnvironmentURL` = `https://org36e60d9d.crm.dynamics.com/`

La única de las 20 con un **valor cargado deliberadamente en DEV**, apuntando a la URL real del
ambiente. Alguien la creó, la llenó, y nada la lee. Es el patrón típico de una variable pensada
para armar deep links a registros que nunca se construyeron.

---

## Recomendación

| Grupo | Qué son | Recomendación |
|---|---|---|
| **1 (3)** | Migradas, con reemplazo probado y funcionando | **Borrar.** Riesgo mínimo: el reemplazo está verificado en código. |
| **2 (14)** | Andamiaje vacío, concepto inexistente | **Borrar.** No hay nada que perder: sin valor, sin default, sin consumidor, sin concepto. |
| **3 (2)** | Config sin funcionalidad detrás | **Preguntar primero.** ¿La zona horaria y el archivado de correo son features pendientes o descartadas? Si están en el backlog, la variable documenta la intención. |
| **4 (1)** | `CHEVEnvironmentURL`, configurada sin lector | **Preguntar primero.** Que alguien la haya llenado sugiere un propósito concreto que conviene recuperar antes de borrarla. |

**17 de las 20 son borrado seguro. 3 merecen una pregunta antes.**

## Antes de ejecutar

1. **La definición vive en `CashHubCore`.** Borrarla en DEV y después importar la solución a
   TEST/PROD la elimina también allá, **junto con su valor**. Confirmar que ningún ambiente
   downstream dependa de ellas — aunque, como se explicó arriba, ningún código las lee en ningún
   ambiente.
2. **Borrar la definición borra en cascada su `environmentvariablevalue`.** Para las 3 con valor
   cargado en DEV, anotar el valor antes por si hace falta reconstruirlo.
3. **Verificar la lista de flows.** Una env var se consume desde un flow por
   `parameters('$environment-variable-name')`. Si un flow apagado se reactiva, puede reaparecer un
   consumidor. Por eso `CHEVRfcApplyTimeoutMinutes` queda fuera de este lote.
