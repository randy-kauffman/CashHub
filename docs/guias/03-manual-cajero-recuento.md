# Manual del cajero de recuento — App Recounting

## Propósito del rol

Sos quien abre físicamente cada tula que ya está en bóveda y cuenta su
contenido —efectivo por denominación y cheques— contra lo que el cliente
declaró por correo. Si tu recuento coincide con lo declarado, el sistema
postea la transacción de depósito directamente. Si no coincide, tu recuento
queda pendiente hasta que el supervisor lo revise y apruebe.

## Prerequisitos

- Acceso a la app **Recounting** (`sanic_chapprecounting`).
- Tener asignada una mesa de conteo (`CH-Tbl-CountingDesks`) — afecta si tu
  sesión se aprueba automáticamente o no (ver más abajo).
- Idealmente, contar con la máquina contadora de efectivo conectada y con su
  número de serie a mano, para importar la lectura automática.

## Pantalla de la app

Recounting tiene una sola pantalla real de trabajo. Todo el recuento —abrir
sesión, escanear tula, contar, cerrar— ocurre ahí.

## Paso a paso

### 1. Abrir tu sesión de recuento del día

1. Al entrar a la app, el sistema revisa si ya tenés una sesión de recuento
   abierta hoy. Si no la tenés, te pide crear una: elegí tu usuario (se
   completa solo) y la **mesa de conteo** donde vas a trabajar.
2. Al confirmar:
   - Si la mesa que elegiste es la que tenés **asignada**, el sistema
     **aprueba tu sesión automáticamente**, sin que el supervisor intervenga.
   - Si elegís una mesa **distinta** a la tuya, el sistema crea una solicitud
     de aprobación y **la pantalla queda bloqueada** con el mensaje
     "Esperando aprobación del supervisor" hasta que el supervisor la
     resuelva desde su app.

### 2. Escanear la tula

1. Escaneá o ingresá el código de la tula que vas a recontar.
2. El sistema la busca entre las tulas que están **almacenadas en bóveda** o
   **asignadas a vos**.
3. Si la tula escaneada **no tiene una declaración asociada** (el cliente no
   la declaró, o la declaración no llegó a asociarse), el sistema genera una
   solicitud de aprobación de tipo "Tula sin declaración" y queda esperando
   al supervisor.

### 3. Contar el contenido

1. Por cada depósito dentro de la tula, cargá:
   - El **efectivo**, denominación por denominación.
   - Los **cheques** que traiga.
2. El sistema va comparando en vivo lo que cargás contra lo que el cliente
   declaró para ese depósito.

### 4. Importar la lectura de la máquina contadora (opcional, recomendado)

1. Usá el botón **"Obtener recuento de la máquina contadora"** (ícono de
   recarga).
2. El sistema busca el archivo XML que generó la máquina para tu sesión y
   número de serie, y carga esos datos automáticamente en el recuento —te
   ahorra la carga manual de las denominaciones que la máquina ya contó.

### 5. Cerrar la tula

1. Cuando terminaste de contar todos los depósitos de la tula, confirmá el
   cierre. El sistema calcula las diferencias por moneda (córdobas, dólares,
   euros) y arma un reporte con el detalle.
2. **Si no hay diferencias:**
   - El sistema postea los totales sobre la tula, marca los cheques como
     **Recontado**, la tula pasa a **Recontada**, y **crea directamente la
     transacción de Depósito** — no necesita aprobación de nadie.
3. **Si hay diferencias** (o la tula nunca tuvo declaración):
   - La tula pasa a **"Pendiente de revisión por supervisor - recuento"**.
   - Se genera una solicitud de aprobación ("Tula con diferencia" o "Tula
     recontada sin declaración").
   - Quedás bloqueado en esa tula hasta que el supervisor la resuelva desde
     la app Supervisor (ver [manual 02](02-manual-supervisor.md)). El
     supervisor es quien decide si postea el faltante o el sobrante.

## Limitaciones conocidas

- **Auto-aprobación en tu propia mesa.** Si abrís tu sesión en la mesa que
  tenés asignada, el sistema la aprueba solo, sin que ningún supervisor la
  revise. Esto es una decisión de diseño del sistema tal como está hoy, no un
  error tuyo — pero es importante que lo sepas porque rompe el control de
  "cuatro ojos" que uno esperaría para este tipo de operación. Si tu banco
  espera que toda sesión pase por un supervisor, esto no ocurre en la
  práctica salvo que uses una mesa distinta a la asignada.
- **Si escaneás una tula y el sistema no la encuentra**, confirmá primero
  con el oficial de esclusa/bóveda que efectivamente la movió a bóveda: en la
  operación normal, para cuando la tula llega a tu mesa ya debería estar en
  "Almacenada en bóveda". (Nota técnica: esta app usa la grafía correcta
  "esclusa", igual que el esquema vivo de la tabla; son otras apps las que
  conservan una copia interna desactualizada con "exclusa" — ver
  [typos conocidos](../referencia/typos-conocidos.md).)
- **No hay verificación de rol dentro de la app.** Cualquier persona a la que
  se le comparta Recounting puede operar como cajero, sin comprobación
  adicional.
- Una tula sin declaración asociada, o con diferencia, **queda bloqueada**
  hasta que el supervisor resuelva la solicitud generada — no hay forma de
  "saltear" ese estado desde tu app; el destrabe depende enteramente de que el
  supervisor la apruebe o la rechace desde la suya.
