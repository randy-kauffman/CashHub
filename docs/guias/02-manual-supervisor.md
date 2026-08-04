# Manual del supervisor de recuento — App Supervisor

## Propósito del rol

Sos el punto de control del proceso: cada vez que Reception o Recounting
encuentran una excepción (una tula que no cuadra, una sesión de recuento en
mesa ajena, un cambio a un lote que ya cerró), el sistema te la manda a vos
como una **solicitud de aprobación**. Sos, además, el único actor que
efectivamente **contabiliza el faltante o sobrante** en el sistema: ni
Reception ni Recounting postean esa transacción por sí solos, la posteás vos
al aprobar. También administrás la ubicación de tulas, el resumen de
operaciones y el envío de cheques a digitación.

## Prerequisitos

- Acceso a la app **Supervisor** (`sanic_chappsupervisor`).
- Criterio para decidir si una diferencia de recuento es aceptable o si
  amerita investigación antes de aprobar — **aprobar una tula con diferencia
  postea la transacción de inmediato y no tiene deshacer**.
- La app tampoco verifica tu rol dentro del código (ver Limitaciones): el
  control de acceso depende de que el administrador te haya compartido esta
  app específicamente.

## Pantallas de la app

Al entrar, ves un menú lateral con cuatro opciones, en este orden, y aterrizás
en la primera:

| Pantalla | Para qué sirve |
|---|---|
| **Aprobaciones** | Bandeja de todas las solicitudes pendientes generadas por Reception y Recounting. Es tu pantalla principal de trabajo. |
| **Localización de tulas** | Consulta de dónde está y en qué estado se encuentra cada tula (solo lectura). |
| **Resumen de operaciones** | Resumen de recuentos por cajero/sesión (solo lectura). |
| **Administración de cheques** | Marca cheques recontados como enviados a digitación. |

## Paso a paso

### 1. Aprobaciones (pantalla principal)

1. En la bandeja vas a ver todas las solicitudes con estado pendiente (sin
   resolver todavía). Cada una tiene un **tipo de solicitud**, generado por
   Reception o por Recounting:

   | Tipo de solicitud | Quién la genera | Qué representa |
   |---|---|---|
   | Agregar tula / Eliminar tula / Remapear tula / Modificar número de planilla | Reception | Cambio a un lote que ya salió del estado de edición libre. |
   | Crear sesión de recuento | Recounting | El cajero abrió su sesión en una mesa que **no** es la suya asignada. |
   | Primera tula en planilla / Tula sin declaración | Recounting | El cajero escaneó una tula que no tiene una declaración del cliente asociada. |
   | Tula con diferencia | Recounting | El recuento del cajero no coincide con lo declarado por el cliente. |
   | Tula recontada sin declaración | Recounting | La tula se recontó pero nunca tuvo declaración de origen. |

2. Seleccioná una solicitud y elegí **Aprobar** o **Rechazar**.

   - **Aprobar** aplica el efecto real de la solicitud. Por ejemplo, para
     "Agregar tula" efectivamente da de alta la tula en el lote; para
     "Crear sesión de recuento" confirma la sesión del cajero.
   - **Rechazar** revierte la tula al estado que tenía antes de la solicitud,
     para los tipos donde eso aplica. Para "Agregar tula", "Modificar número
     de planilla" y "Primera tula en planilla", rechazar solo marca la
     solicitud como rechazada — no hay nada físico que revertir.

3. **Caso especial — "Tula con diferencia" (el más delicado):**
   Al aprobar, el sistema recalcula, depósito por depósito, la diferencia
   entre lo recontado y lo declarado, y postea automáticamente:
   - Una transacción **Sobrante** si lo recontado es mayor a lo declarado.
   - Una transacción **Faltante** si lo recontado es menor.
   - Siempre, además, la transacción base de **Depósito**.
   - Si hay un depósito declarado que nadie recontó, se postea como
     **Faltante + Depósito**.
   - Si hay un depósito recontado sin declaración de origen, se postea como
     **Depósito** con referencia a "Sobrante".

   Al cerrar, la tula queda en estado **Recontada**, los cheques asociados
   pasan a **Recontado**, y el detalle de la sesión de recuento queda como
   **"Cerrado con diferencias"**.

   **Esta acción no tiene deshacer.** Una vez aprobada, las transacciones ya
   quedaron posteadas.

### 2. Localización de tulas

Pantalla de consulta: buscá una tula para ver dónde está y en qué estado se
encuentra. No permite modificar nada desde acá.

### 3. Resumen de operaciones

Pantalla de consulta: mirá el resumen de recuentos por cajero y por sesión.
Tampoco permite modificar nada.

### 4. Administración de cheques

1. Vas a ver los cheques ya recontados.
2. Seleccioná los que correspondan y marcalos como **"Enviado a
   digitación"** — este es el paso que habilita que el digitador los procese
   en la app Digitization (ver [manual 05](05-manual-digitacion.md)). Es la
   única acción que esta pantalla permite.

## Limitaciones conocidas

- **No hay verificación de rol dentro de la app.** Cualquier usuario al que
  se le comparta Supervisor puede aprobar o rechazar solicitudes y postear
  faltantes/sobrantes, sin ninguna comprobación adicional en el código.
- **El comentario "no hacer nada" en el botón Rechazar no siempre se cumple
  al pie de la letra.** Para algunos tipos de solicitud (por ejemplo,
  "Eliminar tula"), el código sí ejecuta una acción real al rechazar pese a
  que el comentario interno dice lo contrario. En la práctica no debería
  cambiar lo que ves en pantalla, pero si el resultado de un rechazo no es el
  esperado, no asumas que "no hizo nada" — confirmá el estado de la tula en
  Localización de tulas.
- **El faltante/sobrante se registra por tipo, no siempre con el detalle
  exacto de la diferencia como un campo separado y auditable.** Si necesitás
  reconstruir el monto exacto de una diferencia para una auditoría, puede que
  tengas que compararlo manualmente contra lo declarado y lo recontado en vez
  de encontrarlo en un único campo.
- Puede aparecer una notificación de depuración ("Entra") al aprobar una
  solicitud de tipo "Tula recontada sin declaración" — es un mensaje de
  prueba que quedó en el código, no indica ningún error.
