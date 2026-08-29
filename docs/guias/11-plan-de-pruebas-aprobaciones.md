# Plan de pruebas — Aprobaciones del supervisor

Esta guía recorre **los 10 tipos de solicitud** que llegan a `scrAprobaciones` de
CH-App-Supervisor, en el orden en que aparecen en el proceso real: primero las que
nacen en la **esclusa** (Reception), después las que nacen en el **recuento**
(Recounting).

Cada tipo se prueba **dos veces**: aprobando y rechazando. Un tipo no está probado
hasta que se verificó que el rechazo también hace lo que debe — que a veces es
*nada*, y eso también hay que confirmarlo.

> **Qué cambió y por qué importa para estas pruebas.** Desde Apps 1.33.2.0 la
> pantalla ya no ejecuta los efectos: llama a la Custom API
> `sanic_CHResolveApprovalRequest`, que autoriza, aplica el efecto y marca la
> solicitud **en una sola transacción**. O pasa todo, o no pasa nada. Antes eran
> dos escrituras sueltas y la solicitud podía quedar resuelta con el efecto sin
> ejecutar. **Si en alguna prueba ves la solicitud resuelta pero el efecto no
> ocurrió, es un bug grave: reportalo.**

---

## 1. Antes de empezar

### 1.1 Con qué usuario probar

| Necesitás | Por qué |
|---|---|
| Un usuario **miembro del grupo aprobador** | Es la puerta: sin eso todo se bloquea |
| Con rol **CH-SR-CountingSupervisor** | Sin privilegio de escritura sobre la solicitud, la API responde 403 antes de correr |
| **Distinto** del que creó la solicitud | Nadie resuelve lo suyo. Es una regla, no un descuido |

El grupo aprobador sale de `CH-Tbl-Parameter`, clave `Security.ApproverGroup.Supervisor`.

> ⚠️ **Trampa verificada.** Estar en el grupo AAD no alcanza: Dataverse
> materializa la membresía del equipo **cuando el usuario entra al entorno**. Un
> usuario recién agregado sigue dando "no pertenece al grupo aprobador" hasta que
> inicie sesión al menos una vez. Y estar en el equipo tampoco alcanza sin el rol:
> hay usuarios en el grupo aprobador **sin ningún rol CashHub**, que nunca podrían
> resolver nada.

### 1.2 Cómo se lee un resultado

La API contesta siempre con dos campos, y **nunca** con un error de red:

| `Outcome` | Qué significa | Qué se escribió |
|---|---|---|
| `Approved` | Se aprobó y el efecto corrió | Solicitud + efecto |
| `Rejected` | Se rechazó y la reversión corrió | Solicitud + reversión |
| `Blocked` | No se pudo, y `Message` dice por qué | **Nada. Cero escrituras** |

**Si ves un "Network error" o un mensaje genérico, eso es un bug.** El diseño
entero existe para que el motivo llegue en castellano: Power Fx descarta el
mensaje de los faults de plugin, por eso los veredictos viajan como dato.

### 1.3 Tabla de estados (para verificar en datos)

**Tula** — `sanic_bagstatus`

| Código | Etiqueta |
|---|---|
| 159460000 | Recibida en esclusa |
| 159460001 | Almacenada en boveda |
| 159460002 | Asignada a cajero |
| 159460003 | Recontada |
| 159460004 | Pendiente de revision por supervisor - recuento |
| 159460005 | Pendiente de revision por supervisor - esclusa |

**Solicitud** — `sanic_approvalstatus`: vacío = Pendiente · 159460000 = Aprobado · 159460001 = Rechazado

**Detalle de sesión** — `sanic_recountingsessiondetailstatus`: 159460000 En progreso · 159460001 Cerrado · 159460002 Cerrado con diferencias

**Cheque** — `sanic_checkstatus`: 159460000 Recontado · 159460003 Enviado a digitacion · 159460004 Recibido · 159460005 No Recibido

**Asiento** — `sanic_transactiontype`: 159460000 Deposito · 159460001 Faltante · 159460002 Sobrante · 159460003 Sin declaracion

---

## PARTE A — Aprobaciones que nacen en la esclusa (Reception)

Las cinco las genera **CH-App-Reception**. Todas trabajan sobre una tula que el
recepcionista dejó en estado **Pendiente de revisión por supervisor – esclusa**
(`159460005`) al pedir la excepción.

---

### A.1 · Agregar tula

Materializar una tula que no venía en la planilla de conducción.

**Cómo generarla:** en Reception, sobre un lote **ya en bóveda**, agregar una tula
que no figura en la planilla. La app la crea en `159460005` y pide aprobación.

**Contrato:** `BagGUID`, `BagID`, `ReceiptGUID`, `ReceptionBatchGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula pasa a **Almacenada en bóveda** (`159460001`). Mensaje: *"Tula dada de alta en boveda."* |
| ❌ **Rechazar** | La tula se **elimina**. Mensaje: *"Tula eliminada."* |

**Caso borde obligatorio — número duplicado.** Antes de aprobar, creá otra tula en
el mismo lote con el mismo número. Al aprobar debe salir:

> `Blocked` — *"El numero de tula 'XXX' ya existe en otra planilla del mismo lote."*

y la tula debe quedar **igual que estaba**, sin cambiar de estado.

---

### A.2 · Editar tula

Corregir el número de una tula con el lote ya cerrado en bóveda.

**Cómo generarla:** en Reception, editar el número de una tula de un lote ya en bóveda.

**Contrato:** `BagGUID`, `BagID`, `ReceiptGUID`, `ReceptionBatchGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula queda **renombrada** y vuelve a **bóveda** (`159460001`). Mensaje: *"Numero de tula actualizado."* |
| ❌ **Rechazar** | La tula vuelve a **bóveda** con aviso: *"El contrato de este tipo no trae estado previo: la tula se devolvio a boveda. Verifica que sea el estado correcto."* |

> **Por qué el rechazo avisa.** Es el único tipo cuyo contrato **no** guarda
> `BagPreviousState`, así que no hay estado previo que restituir y bóveda es el
> fallback seguro. El aviso es a propósito: verificá que bóveda sea correcto.

**Caso borde:** número duplicado en el lote → `Blocked`, tula intacta.

> ⚠️ **Trampa verificada.** Este efecto solo funciona si la tula está en
> `159460005`. Si por lo que sea quedó en bóveda, el guard responde *"No se puede
> editar la identidad de una tula de un lote que ya esta en boveda"*. No es un bug:
> el circuito de aprobación es justo la vía habilitada, y la tula tiene que venir
> por esa vía.

---

### A.3 · Eliminar tula

Dar de baja una tula que no debía estar.

**Cómo generarla:** en Reception, eliminar una tula de un lote ya en bóveda.

**Contrato:** `GUID`, `BagPreviousState`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula se **borra**. Mensaje: *"Tula eliminada."* |
| ❌ **Rechazar** | La tula vuelve a **su estado previo**, el que dice `BagPreviousState`. Mensaje: *"Rechazada. Tula restituida a su estado previo."* |

**El rechazo es el caso importante.** Verificá en datos que `sanic_bagstatus`
quedó **exactamente** en el valor que decía el contrato. Si quedó donde estaba
(`159460005`), el estado previo no se resolvió y vas a ver este mensaje:

> *"Rechazada, pero el estado previo no pudo resolverse: la tula quedo como estaba. Corregilo a mano."*

Eso **no es una falla silenciosa**: la guarda existe para no estamparle a una tula
un estado inventado. Pero si aparece, reportalo con el JSON del contrato.

> **Dato del port.** `BagPreviousState` guarda la **etiqueta** (`"Almacenada en
> boveda"`), no el código numérico. Se verificó contra las 107 solicitudes del
> entorno que traen la clave: 107 de 107 usan etiqueta.

---

### A.4 · Remapear tula

Mover una tula a otra planilla de conducción.

**Cómo generarla:** en Reception, cambiar de planilla una tula de un lote en bóveda.

**Contrato:** `GUID`, `BagPreviousState`, `ReceiptGUID`, `ReceiptID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula queda colgando de la **planilla destino** (`sanic_receptionid`) **y** vuelve a su estado previo. Mensaje: *"Tula remapeada."* |
| ❌ **Rechazar** | Vuelve a su estado previo, **sin cambiar de planilla**. |

**Verificá las dos cosas al aprobar**: que cambió el lookup de planilla *y* que
cambió el estado. Si solo cambió una, hay un problema.

**Caso borde:** si la planilla destino ya no existe → `Blocked` con *"No se
encontro la planilla destino del remapeo."* y cero escrituras.

---

### A.5 · Modificar número de planilla

Renombrar la planilla de conducción completa.

**Cómo generarla:** en Reception, cambiar el número de una planilla de un lote en bóveda.

**Contrato:** `ReceiptGUID`, `NewReceiptID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La planilla queda renombrada (`sanic_receiptid` **y** `sanic_name`). Mensaje: *"Numero de planilla actualizado."* |
| ❌ **Rechazar** | **Nada.** Mensaje: *"Resuelta. No habia efecto que aplicar."* |

**El rechazo no hace nada, y está bien.** El productor no pre-modifica la planilla
al pedir el cambio, así que no hay nada que revertir. Verificá que el número
siguió igual.

**Casos borde obligatorios — acá vivía un bug:**

| Caso | Resultado esperado |
|---|---|
| Renombrar a un número **que ya usa otra planilla del mismo lote** | `Blocked` — *"El numero de planilla 'XXX' ya existe en el mismo lote de recepcion."* |
| Renombrar a **su propio número actual** | `Approved` — no es un duplicado |
| Renombrar a un número libre | `Approved` |
| Solicitud **sin** `NewReceiptID` | `Blocked` — *"La solicitud no trae el nuevo numero de planilla (NewReceiptID)."* |

> **Por qué probar los cuatro.** La versión anterior comparaba el **lote** de cada
> planilla contra el **ID** de la planilla — identificadores de tablas distintas,
> jamás coincidían. El dup-check **nunca disparó**. Estos cuatro casos son
> exactamente los que lo destapan.

---

## PARTE B — Aprobaciones que nacen en el recuento (Recounting)

---

### B.1 · Crear sesión de recuento

El cajero abre una sesión en una mesa de conteo.

**Cómo generarla:** en Recounting, iniciar sesión eligiendo una mesa.

**Contrato:** `RecountingSessionGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La sesión queda **confirmada** (`sanic_confirmed = true`) y `sanic_confirmedby` = el supervisor. Mensaje: *"Sesion de recuento confirmada."* |
| ❌ **Rechazar** | La sesión se **elimina**. Mensaje: *"Sesion de recuento rechazada y eliminada."* |

> ⚠️ **Trampa verificada, y es la razón por la que este tipo cuesta probar.** Si el
> cajero abre sesión en **su propia mesa**, `RecountingSessionAutoApprovePlugin` la
> auto-aprueba en el mismo Create: la solicitud nace ya resuelta y **nunca llega al
> supervisor**. Para probar el circuito manual, la sesión tiene que ser en una
> **mesa de otro cajero**.

**Verificación adicional del rechazo:** la baja de la sesión vuelve a pasar por
`GuardRecountingPlugin`, que pregunta si quien borra es el cajero dueño **o un
aprobador**. Con el supervisor aprobador debe permitirlo. Si sale *"Solo el cajero
dueno de la sesion puede darla de baja"*, reportalo: es exactamente el bug que se
corrigió.

---

### B.2 · Tula sin declaración

El cajero escanea una tula que no tiene declaración del cliente.

**Cómo generarla:** en Recounting → `scrTulas`, escanear una tula sin bolsa declarada.

**Contrato:** `BagGUID`, `RecountingSessionGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula pasa a **Asignada a cajero** (`159460002`), queda enlazada a la sesión, y **se crea un detalle de sesión** con el supervisor como responsable. Mensaje: *"Tula sin declaracion asignada al cajero."* |
| ❌ **Rechazar** | **Nada.** Mensaje: *"Resuelta. No habia efecto que aplicar."* |

**Verificá las tres cosas al aprobar**: estado de la tula, lookup de sesión, y que
el detalle de sesión existe.

> **El rechazo no-op es deliberado.** El productor no pre-modifica la tula al pedir
> la excepción, así que no hay estado que restituir — y coherentemente, el contrato
> de este tipo **no** declara `BagPreviousState`. La tula sigue disponible.

---

### B.3 · Tula con diferencia — ⚠️ genera asientos contables

El cajero terminó de contar y lo contado no coincide con lo declarado.

**Cómo generarla:** en Recounting → `scrConteo`, cerrar el conteo de una tula
**que tiene bolsa declarada** y cuyos montos no cuadran.

**Contrato:** `BagGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | Se generan los asientos, los cheques sin estado pasan a **Recontado**, la tula pasa a **Recontada** (`159460003`) y el detalle de sesión a **Cerrado con diferencias** (`159460002`). Mensaje: *"Recuento cerrado con diferencias: N asientos generados."* |
| ❌ **Rechazar** | La tula vuelve a **Asignada a cajero** (`159460002`) para rehacer el conteo, y **NO se genera ningún asiento**. Mensaje: *"Recuento rechazado: la tula vuelve al cajero. No se genero ningun asiento."* |

#### Cómo verificar los asientos — la parte que de verdad importa

**La regla: la diferencia se calcula POR COMPONENTE, nunca por el neto.** Efectivo,
cheque BAC y cheque otros van por separado.

Para cada depósito recontado:

| Situación | Asientos esperados |
|---|---|
| Recontado **sin** declarado enlazado | 1 **Sobrante** con los montos contados, cuenta del **recuento**, ref `SS/<tula>` |
| Enlazado, con componentes de más | 1 **Sobrante** solo con las diferencias positivas, ref `S/<tula>` |
| Enlazado, con componentes de menos | 1 **Faltante** solo con las diferencias negativas (en positivo), ref `F/<tula>` |
| Enlazado, siempre | 1 **Depósito** con los montos **recontados**, ref = la del Excel del cliente |
| Declarado que **nadie** recontó | 1 **Faltante** + 1 **Depósito**, ambos Manual, sin recuento enlazado |

**La prueba que no podés saltear.** Armá una tula con un depósito que tenga
**+100 en efectivo y −100 en cheques**. Por el neto daría cero y no se asentaría
nada — plata que desaparece. Tienen que salir **los dos**: un Sobrante de 100 en
efectivo *y* un Faltante de 100 en cheques.

Otras verificaciones:

- **La cuenta de las diferencias sale del DECLARADO**, no del recuento: la plata se
  le acredita o debita a la cuenta que el cliente declaró.
- **El depósito lleva los montos recontados**, no los declarados.
- **Procesamiento**: si el depósito recontado tiene cheques → `Manual`; si es solo
  efectivo → `Batch`.
- **La referencia del Excel no se toca.** Solo se sobrescribe en sobrante, faltante
  y sin declaración.
- **Ninguna referencia puede pasar de 30 caracteres** — ese es el tope del core, no
  el de la columna. Si una tula es tan larga que no entra, el cierre se **bloquea**
  con mensaje claro y cero asientos, en vez de mandar al core una referencia
  mutilada.
- **Los cheques que ya avanzaron no se tocan.** Solo se estampan los de estado
  vacío. Un cheque en "Enviado a digitación" se deja donde está: retrocederlo
  tumbaría el cierre entero.

---

### B.4 · Tula recontada sin declaración — ⚠️ genera asientos contables

El cajero contó una tula que nunca tuvo declaración.

**Cómo generarla:** en Recounting → `scrConteo`, cerrar el conteo de una tula
**sin bolsa declarada**. La app elige el tipo sola:
`If(IsBlank(BagDeclarationID), "Tula recontada sin declaracion", "Tula con diferencia")`.

**Contrato:** `BagGUID`

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | **Un asiento `Sin declaracion` por cada depósito recontado**, ref `SD/<tula>`, todos Manual. Cheques sin estado → Recontado. Tula → **Recontada**. Detalle de sesión → **Cerrado** (`159460001`, sin "con diferencias"). Mensaje: *"Recuento sin declaracion cerrado: N asientos generados."* |
| ❌ **Rechazar** | Igual que B.3: la tula vuelve al cajero, cero asientos. |

**Sin declaración no hay sobrante ni faltante posible** — no existe el otro lado
contra el cual comparar. Si ves un Sobrante o un Faltante acá, es un bug.

> **Este flujo nunca funcionó antes.** El texto de la referencia que armaba la app
> ocupaba 30 de los 31 caracteres permitidos, así que fallaba con cualquier tula de
> 2 caracteres o más — es decir, siempre. En el entorno hay **32 solicitudes de
> este tipo aprobadas sin un solo asiento generado**. Al probar, confirmá que ahora
> sí se generan.

**Nota sobre asientos de tipo Depósito.** Después del cierre vas a ver también
asientos `Deposito` que **no** creó la aprobación: los agrega
`BagRecountClosurePlugin` cuando la tula pasa a Recontada. Es idempotente por
(depósito, tipo), así que en "Tula con diferencia" no los duplica. **No los cuentes
como error.**

---

### B.5 · Primera tula en planilla — ⚠️ sin productor en las apps

Asignar la primera tula de una planilla al cajero y enlazarla con su declaración.

| | Qué debe pasar |
|---|---|
| ✅ **Aprobar** | La tula pasa a **Asignada a cajero**, queda enlazada a la sesión **y** a la bolsa declarada; la bolsa declarada apunta de vuelta a la tula. Mensaje: *"Tula asignada al cajero y enlazada a su declaracion."* |
| ❌ **Rechazar** | La tula vuelve a **bóveda** (`159460001`) y se desenlaza de la sesión. |

**Contrato:** `BagGUID`, `DeclarationBagGUID`, `RecountingSessionGUID`

> 🚨 **Hueco conocido — leelo antes de intentar probarlo.** Este tipo **no lo crea
> ninguna app**. El texto solo aparece en CH-App-Supervisor como etiqueta
> descriptiva. Las 80 solicitudes que hay en el entorno vienen de
> `scripts/seed-supervisor-aprobaciones.py` (`AR-STRESS-*`) y de pruebas manuales.
>
> **Desde la UI no se puede generar.** O se prueba sobre las sembradas, o hay que
> decidir si el tipo sigue vivo. Es una pregunta de negocio, no técnica:
> ¿el flujo se movió a "Tula sin declaración" y este tipo quedó huérfano?

**Si probás sobre una sembrada:** la tula tiene que estar en **bóveda**
(`159460001`). Con una tula en otro estado el guard rechaza la transición, y con
razón — no es un bug del efecto.

---

## PARTE C — Pruebas transversales

Estas van **una vez**, con cualquier tipo. Prueban el mecanismo, no el efecto.

### C.1 · Nadie resuelve lo suyo

Con el mismo usuario que creó la solicitud, intentá aprobarla.

> `Blocked` — *"No podes resolver una solicitud creada por vos."*

### C.2 · Sin grupo aprobador no se resuelve

Con un usuario que **no** esté en el grupo aprobador:

> `Blocked` — *"No pertenece al grupo aprobador de supervisores; no puede resolver esta solicitud."*

### C.3 · Una solicitud resuelta no se re-resuelve

Aprobá una, después intentá rechazarla:

> `Blocked` — *"La solicitud ya fue resuelta y quedo congelada; no admite modificaciones."*

**Y verificá que el efecto no se ejecutó dos veces.**

### C.4 · Un rechazo exige motivo

Rechazá dejando el motivo vacío:

> `Blocked` — *"Un rechazo exige motivo. Escriba por que se rechaza y vuelva a enviarlo."*

### C.5 · Transaccionalidad — la prueba estrella

**Esta es la que justifica todo el rediseño.** En **cualquier** caso que termine en
`Blocked`, verificá en datos que:

1. La solicitud sigue **Pendiente** (`sanic_approvalstatus` vacío)
2. La tula / planilla / sesión quedó **exactamente** como estaba
3. **No se creó ningún asiento**

Si encontrás una sola combinación donde la solicitud quedó resuelta y el efecto no
ocurrió —o al revés— **paren todo y reporten**. Ese era el bug original y no puede
volver.

### C.6 · El mensaje llega en castellano

En cualquier `Blocked`, el toast tiene que mostrar el motivo real. Si ves *"Network
error"*, *"The requested operation is invalid"* o cualquier texto genérico en
inglés, es un bug: significa que el error volvió como fault en vez de como dato.

---

## Apéndice A — Efectos declarados, de un vistazo

| Tipo | Origen | Aprobar → | Rechazar → |
|---|---|---|---|
| Agregar tula | Reception | Alta en bóveda | **Elimina la tula** |
| Editar tula | Reception | Renombra + bóveda | Bóveda con aviso |
| Eliminar tula | Reception | **Elimina la tula** | Restituye estado previo |
| Modificar número de planilla | Reception | Renombra planilla | **Nada** |
| Remapear tula | Reception | Mueve + restituye estado | Restituye estado previo |
| Crear sesión de recuento | Recounting | Confirma sesión | **Elimina la sesión** |
| Tula sin declaración | Recounting | Asigna a cajero + detalle | **Nada** |
| Tula con diferencia | Recounting | **Asientos** + cierra | Devuelve al cajero |
| Tula recontada sin declaración | Recounting | **Asientos** + cierra | Devuelve al cajero |
| Primera tula en planilla | *(sin productor)* | Asigna + enlaza declaración | Libera a bóveda |

**Los cuatro destructivos** están en negrita: dos eliminan tulas, uno elimina una
sesión, dos generan asientos contables. Probá esos con datos que puedas perder.

---

## Apéndice B — Contratos (`RequestResources`)

| Tipo | Claves |
|---|---|
| Agregar tula | `BagGUID`, `BagID`, `ReceiptGUID`, `ReceptionBatchGUID` |
| Editar tula | `BagGUID`, `BagID`, `ReceiptGUID`, `ReceptionBatchGUID` |
| Eliminar tula | `GUID`, `BagPreviousState` |
| Modificar número de planilla | `ReceiptGUID`, `NewReceiptID` |
| Remapear tula | `GUID`, `BagPreviousState`, `ReceiptGUID`, `ReceiptID` |
| Crear sesión de recuento | `RecountingSessionGUID` |
| Tula sin declaración | `BagGUID`, `RecountingSessionGUID` |
| Tula con diferencia | `BagGUID` |
| Tula recontada sin declaración | `BagGUID` |
| Primera tula en planilla | `BagGUID`, `DeclarationBagGUID`, `RecountingSessionGUID` |

**Ojo con los nombres:** "Eliminar tula" y "Remapear tula" usan `GUID` para la
tula; el resto usa `BagGUID`. No es un error de esta tabla, es el contrato real.

---

## Apéndice C — Planilla de resultados

| # | Tipo | Aprobar | Rechazar | Borde | Observaciones |
|---|---|---|---|---|---|
| A.1 | Agregar tula | ☐ | ☐ | ☐ dup | |
| A.2 | Editar tula | ☐ | ☐ | ☐ dup | |
| A.3 | Eliminar tula | ☐ | ☐ | ☐ estado previo | |
| A.4 | Remapear tula | ☐ | ☐ | ☐ planilla inexistente | |
| A.5 | Modificar número de planilla | ☐ | ☐ | ☐☐☐☐ los cuatro | |
| B.1 | Crear sesión de recuento | ☐ | ☐ | ☐ mesa ajena | |
| B.2 | Tula sin declaración | ☐ | ☐ | — | |
| B.3 | Tula con diferencia | ☐ | ☐ | ☐ +100/−100 | |
| B.4 | Tula recontada sin declaración | ☐ | ☐ | ☐ cheques en digitación | |
| B.5 | Primera tula en planilla | ☐ | ☐ | ⚠ sin productor | |
| C.1 | Nadie resuelve lo suyo | ☐ | | | |
| C.2 | Sin grupo aprobador | ☐ | | | |
| C.3 | Ya resuelta | ☐ | | | |
| C.4 | Rechazo sin motivo | ☐ | | | |
| C.5 | Transaccionalidad | ☐ | | | |
| C.6 | Mensaje en castellano | ☐ | | | |

---

## Apéndice D — Qué reportar cuando algo falla

1. El **nombre de la solicitud** (`AR-…` o `SRC-AR…`)
2. El **`RequestResources`** completo (el JSON)
3. El texto **exacto** del toast
4. El **estado antes y después** de la tula / planilla / sesión
5. Si generó asientos: cuáles, con tipo, montos y referencia

Con eso se reproduce en el `plugintracelog`, que registra cada paso del resolver:
qué actor, qué veredicto, qué efecto y cuántos asientos.
