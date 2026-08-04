# Manual del oficial de esclusa/bóveda — App Reception

## Propósito del rol

Sos quien recibe físicamente, en la esclusa, las tulas (bolsas de seguridad) y
la planilla de conducción que trae la transportadora de valores. Tu trabajo en
CashHub es dejar registro digital de esa recepción —lote, planillas y tulas—
y, más adelante, mover la custodia de esas tulas de la esclusa a la bóveda
temporal. No contás ni validás el contenido de las tulas: eso lo hace el
cajero de recuento (ver [manual 03](03-manual-cajero-recuento.md)).

## Prerequisitos

- Acceso a la app **Reception** (`sanic_chappreception`) asignado por el
  administrador del entorno.
- Conocer los datos de la transportadora que entrega, el cliente/sucursal
  declarante y el número de planilla de conducción.
- **Importante**: la app no verifica que quien la usa tenga un rol de
  seguridad de "recepcionista" — el control de acceso depende exclusivamente
  de que el administrador te haya compartido la app. Ver Limitaciones.

## Pantallas de la app

La app tiene dos pantallas:

| Pantalla | Para qué sirve |
|---|---|
| **Administración de Lotes** | Ver los lotes y planillas ya creados, mover custodia a bóveda, y corregir tulas de un lote existente. |
| **Creación de Lotes de Recepción** | Dar de alta un lote nuevo: transportadora, planillas y sus tulas. |

Al abrir la app entrás a la vista de **Administración de Lotes**.

## Paso a paso

### 1. Crear un lote de recepción nuevo

1. Desde el menú lateral, entrá a **Creación de Lotes de Recepción**.
2. Completá el formulario del lote con los datos de la **transportadora de
   valores** que hizo la entrega.
3. Por cada **planilla de conducción** que trae la transportadora:
   - Cargá los datos de la planilla: cliente, sucursal y los montos que el
     cliente declaró para esa planilla.
   - Cargá cada **tula** física asociada a esa planilla (su código/etiqueta).
4. Cuando terminaste de cargar todas las planillas y tulas del lote, confirmá.
   El sistema guarda cada planilla una por una (puede tardar unos segundos si
   son muchas) y, al terminar, marca el lote como **Recibido**.

> El sistema no calcula en este paso si lo que registrás coincide con lo que
> declaró el cliente por correo — esa comparación ocurre recién en el
> recuento de caja (app Recounting). Acá solo estás dejando constancia de lo
> que físicamente entró por la esclusa.

### 2. Consultar y gestionar lotes existentes

En **Administración de Lotes**:

1. Buscá el lote o la planilla que necesitás usando el filtro/lista.
2. Desde la barra de comandos podés:
   - **Actualizar**: refresca la lista con los datos más recientes.
   - **Editar**: vuelve a abrir el lote en la pantalla de creación para
     corregirlo (si el lote todavía está en un estado editable libremente).
   - **Agregar / Eliminar / Remapear / Editar tula**: modifica una tula
     puntual del lote.

### 3. Mover custodia de esclusa a bóveda

1. Seleccioná el lote (o la planilla) que ya está físicamente resguardado en
   bóveda.
2. Usá la acción **Mover a bóveda** de la barra de comandos.
3. El sistema cambia el estado de todas las tulas del lote/planilla de
   **"Recibida en esclusa"** a **"Almacenada en bóveda"**, y el lote pasa a
   **"En bóveda"**.

### 4. Cuando el lote ya no admite edición libre

Si el lote ya avanzó de estado (por ejemplo, ya está **Recibido** o **En
bóveda**) y necesitás agregar, eliminar, remapear una tula, o corregir el
número de una planilla, el sistema **no aplica el cambio directo**: genera
una **solicitud de aprobación** y la tula queda en el estado
**"Pendiente de revisión por supervisor - esclusa"**. El cambio se aplica
recién cuando el supervisor la aprueba desde la app Supervisor (ver
[manual 02](02-manual-supervisor.md)).

## Estados que vas a ver

| Campo | Valores, en orden |
|---|---|
| Estado del lote (`ReceptionStatus`) | En progreso → Recibido → En bóveda |
| Estado de la tula (`BagStatus`), en esta app | Recibida en esclusa → Almacenada en bóveda → (más adelante, en Recounting: Asignada a cajero → Recontada) |

El estado transitorio **"Pendiente de revisión por supervisor - esclusa"**
aparece cuando una edición de tula necesita aprobación del supervisor.

## Limitaciones conocidas

- **La app no verifica tu rol.** Cualquier persona a la que le compartan
  Reception puede crear lotes, mover custodia a bóveda y editar tulas, sin
  ninguna comprobación adicional dentro de la aplicación. El único control
  real es a quién se le da acceso.
- **No hay forma de eliminar un lote completo.** Solo se pueden eliminar
  tulas individuales dentro de un lote.
- **Esta app no calcula ni muestra faltantes/sobrantes.** Su función termina
  en dejar constancia de la recepción física; la comparación contra lo
  declarado y el manejo de diferencias ocurre en el recuento de caja.
- **Confirmación en cascada, no instantánea.** Al confirmar un lote con
  varias planillas, el guardado se hace planilla por planilla; si la
  operación se interrumpe a la mitad, revisá en Administración de Lotes qué
  planillas quedaron efectivamente guardadas antes de reintentar.
