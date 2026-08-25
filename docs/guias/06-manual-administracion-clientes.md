# Manual de administración de datos maestros de clientes — App Client Administration v2

## Propósito del rol

Sos quien mantiene actualizados los datos maestros de clientes, sucursales,
correos autorizados y cuentas que usa todo el proceso de CashHub. Ningún
cambio se aplica directo: todo pasa por una **solicitud de cambio** — en el
sistema llamada **RFC** ("Request For Change", **no** es un dato de
identificación fiscal aunque coincida el nombre con el que usan otros países
para ese concepto) — que otra persona con permiso de aprobador tiene que
revisar y aprobar o rechazar antes de que el cambio se materialice.

Hay dos roles dentro de esta misma función:

- **Contributor** (solicitante): propone altas o cambios de cliente,
  sucursal, correo o cuenta.
- **Approver** (aprobador): revisa y aprueba o rechaza esas solicitudes. El
  permiso de aprobador está segmentado por tabla — podés ser aprobador de
  Cliente pero no de Sucursal, por ejemplo.

**Cambio de fondo respecto de la versión anterior de este manual**: aprobar o
rechazar ya no deja la solicitud "en tránsito" esperando un proceso
automático aparte. Al tocar Aprobar o Rechazar, el sistema resuelve todo en
el momento — maestro actualizado (si corresponde), bitácora y estado final —
y te devuelve el resultado en la misma pantalla, sin esperar.

## Prerequisitos

- Acceso a la app canvas **CH-App-ClientAdministration v2**.
- Pertenecer al grupo de Azure AD correspondiente a tu rol (Contributor o
  Approver) **para la tabla objetivo** sobre la que vas a operar (Cliente,
  Sucursal, Correo o Cuenta). Esto se verifica dentro de la app (oculta
  botones) y, lo que realmente importa, lo vuelve a verificar el servidor
  cada vez que intentás resolver una solicitud.

## Pantallas de la app

| Pantalla | Para qué sirve |
|---|---|
| **Splash** | Pantalla de arranque; si entrás desde un enlace de notificación de Teams, te lleva directo al detalle de esa solicitud. |
| **Clientes** | Listado de clientes con indicador de "RFC en curso" por fila; alta de un cliente nuevo vía panel lateral. |
| **Cliente** (ficha) | Datos del cliente en solo lectura + 5 pestañas: **Sucursales**, **Correos**, **Cuentas**, **Solicitudes** (las de este cliente) y **Bitácora**. |
| **Solicitudes** | Bandeja de RFC con tres vistas: **Para mi decisión**, **Mías** y **Todas**. |
| **Solicitud** (detalle) | Cabecera, grilla de cambios (valor actual → valor propuesto), historial, y los botones de acción según tu rol y el estado del RFC. |

La navegación entre **Clientes** y **Solicitudes** es un selector de dos
pestañas fijo en el encabezado de toda la app, no un menú aparte. La app
**model-driven** que existía en paralelo con esta canvas app en versiones
anteriores fue **eliminada por decisión de negocio** — si alguna captura o
documento viejo menciona áreas "Operations"/"Governance"/"Reviews" con
navegación estándar de Dataverse, describe algo que ya no existe. Esta es
hoy la única vía soportada.

## Paso a paso — como Contributor (solicitante)

### Dar de alta un cliente

1. En **Clientes**, abrí el panel "Nueva solicitud de cliente" (botón
   Nuevo).
2. Completá Nombre, CIF Banco y CIF Comercial.
3. Al confirmar, el sistema crea el RFC (tipo **Alta**) con sus tres líneas
   de cambio en **Borrador** y te lleva al detalle. Todavía no impacta en
   ningún lado — el cliente no existe hasta que el RFC se apruebe.

### Editar un cliente, sucursal, correo o cuenta existente

1. Desde **Clientes** (para el cliente) o desde la ficha del cliente →
   pestaña correspondiente (Sucursales/Correos/Cuentas), elegí Editar sobre
   el registro que querés modificar.
2. Se abre un panel con: el **campo** a modificar (elegís de una lista de
   campos habilitados para ese tipo de registro), el **valor actual** (de
   solo lectura, para que compares) y el **valor propuesto** (lo que
   escribís vos). Esto reemplaza al formulario completo de versiones
   anteriores: acá elegís un campo por vez y ves el antes/después con
   claridad.
3. Podés agregar más de un cambio al mismo RFC antes de enviarlo — cada
   cambio queda como una línea separada dentro de la misma solicitud, en
   **Borrador**.
4. El sistema **no modifica el dato maestro todavía**. Recién al Enviar
   (paso siguiente) la solicitud queda visible para los aprobadores.

### Enviar, agregar/quitar cambios o cancelar una solicitud propia

Mientras tu RFC siga en **Borrador**, desde la pantalla de detalle podés:

- **Agregar cambio**: sumar otra línea (otro campo) a la misma solicitud.
- **Quitar cambio**: sacar una línea que agregaste por error, con
  confirmación.
- **Enviar a aprobación**: pasa el RFC a **Enviada** y dispara la
  notificación por Teams a los aprobadores de esa tabla (crea el chat
  grupal si todavía no existe).
- **Cancelar solicitud**: la cierra en **Cancelada** sin haber tocado el
  maestro — disponible únicamente mientras siga en Borrador.

Si intentás editar un registro sobre el que ya hay otra solicitud sin
resolver, la fila (o el registro en la ficha) muestra la etiqueta **"RFC en
curso"** — no te bloquea armar el RFC, pero un aviso en la pantalla de
detalle te avisa si el nuevo cambio pisa el mismo campo que otro RFC ya en
trámite, para que lo revises antes de enviar.

## Paso a paso — como Approver (aprobador)

1. Entrá a **Solicitudes**. Por defecto ves la vista **"Para mi decisión"**
   — las RFC en estado **Enviada** sobre tablas donde sos aprobador y que
   **no** creaste vos mismo (no podés aprobar tu propia solicitud). También
   podés cambiar a **"Mías"** (tu propio historial de solicitudes, en
   cualquier estado) o **"Todas"**.
2. Abrí el detalle de una solicitud. Ahí ves la cabecera (cliente, tabla,
   quién la creó, cuándo, motivo) y la grilla de cambios con **valor actual
   → valor propuesto** por cada campo.
3. Si tenés permiso de aprobador para esa tabla y no sos quien la creó, ves
   los botones **Aprobar…** y **Rechazar…**. Al tocar cualquiera de los
   dos, el sistema te pide un **comentario obligatorio**.
4. **La decisión se resuelve en el momento, sin pasos intermedios**: al
   confirmar, el sistema valida separación de funciones y tu membresía
   real de Approver, y si todo está en orden:
   - Aplica el cambio sobre el dato maestro correspondiente (si el RFC es
     de tipo Alta, Edición, Activación o Desactivación) o descarta la
     propuesta (si rechazaste).
   - Registra la bitácora de auditoría.
   - Deja la solicitud en su estado final — **Aplicada** o **Rechazada** —
     todo en la misma operación.
5. La propia pantalla de detalle se actualiza sola con el resultado: vas a
   ver la solicitud pasar a Aplicada/Rechazada y el historial con el
   detalle de lo que se aplicó, sin necesidad de refrescar ni de esperar
   una notificación aparte. La notificación por Teams al creador y al
   aprobador sigue llegando igual que antes, mostrando algo que ya ves en
   pantalla.
6. Si la decisión no pasa la validación del servidor (por ejemplo, quisiste
   aprobar tu propia solicitud, o alguien más ya la resolvió, o dejaste de
   ser miembro del grupo de aprobadores), la pantalla muestra **el motivo
   real del rechazo**, tal como lo devuelve el servidor — nunca un mensaje
   genérico tipo "hubo un problema".
7. **Una solicitud de Alta, Activación o Desactivación de un registro que
   no tiene ningún cambio que aplicar es rechazada como error de negocio
   explícito** ("no hay líneas de cambio; no hay nada que aplicar") — no
   existe la posibilidad de aprobar "en el aire" un RFC vacío.

## Limitaciones conocidas

- **Ya no hay una ventana de "Aprobada pero todavía no aplicada".** En la
  versión anterior existía un estado intermedio donde el RFC podía quedar
  aprobado durante minutos (o quedarse trabado ahí si el proceso automático
  fallaba). Eso desapareció: el ciclo de estados salta directo de
  **Enviada** a **Aplicada** o **Fallida/Rechazada** en la misma acción del
  aprobador. Si ves documentación o capturas viejas que mencionan un estado
  "Aprobada" visible en pantalla, describen la versión anterior.
- **El nombre "RFC" puede confundir.** En este sistema significa
  exclusivamente "Request For Change" (solicitud de cambio); no tiene
  relación con ningún identificador fiscal del cliente.
- **La decisión se aplica a toda la solicitud, no línea por línea.** Si un
  RFC junta varios cambios, Aprobar o Rechazar afecta a todos los cambios de
  esa solicitud al mismo tiempo — para aprobar unos y rechazar otros hay que
  haberlos enviado como RFC separados desde el principio.
- **Un alta completa de cliente sigue siendo varias solicitudes
  independientes.** Cliente, Sucursal, Correo y Cuenta se piden por
  separado, cada uno con su propio ciclo de aprobación — no existe un "RFC
  compuesto" que agrupe todo el alta en una sola decisión.
- **La pestaña Cuentas puede aparecer vacía según tu perfil.** Ni el rol
  Contributor ni el Approver de esta función tienen, hoy, ningún privilegio
  declarado (ni siquiera de lectura) sobre la tabla de Cuentas — si no
  tenés otro privilegio de plataforma que te lo habilite, la sub-grilla de
  Cuentas de la ficha del cliente puede no mostrarte nada aunque existan
  cuentas cargadas. No es un bug de esta app; es una laguna del modelo de
  seguridad pendiente de resolución — repórtalo si te bloquea el trabajo.
- **No hay validación de correo duplicado por cliente.** El sistema no
  impide dar de alta el mismo correo autorizado dos veces para el mismo
  cliente a través de dos RFC distintos.
