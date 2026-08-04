# Manual de administración de datos maestros de clientes — App Client Administration

## Propósito del rol

Sos quien mantiene actualizados los datos maestros de clientes, sucursales y
correos autorizados que usa todo el proceso de CashHub. Ningún cambio se
aplica directo: todo pasa por una **solicitud de cambio** — en el sistema
llamada **RFC** ("Request For Change", **no** es un dato de identificación
fiscal aunque coincida el nombre con el que usan otros países para ese
concepto) — que otra persona con permiso de aprobador tiene que revisar y
aprobar antes de que el cambio se materialice.

Hay dos roles dentro de esta misma función:

- **Contributor** (solicitante): propone altas o cambios de cliente,
  sucursal o correo.
- **Approver** (aprobador): revisa y aprueba o rechaza esas solicitudes. El
  permiso de aprobador está segmentado por tabla — podés ser aprobador de
  Cliente pero no de Sucursal, por ejemplo.

## Prerequisitos

- Acceso a la app canvas **Client Administration**
  (`sanic_chappclientadministration`) o a la app model-driven equivalente,
  según cómo te la haya asignado el administrador.
- Pertenecer al grupo de Azure AD correspondiente a tu rol (Contributor o
  Approver) **para la tabla objetivo** sobre la que vas a operar (Cliente,
  Sucursal o Correo). Esto sí se verifica dentro de la app, a diferencia de
  las apps operativas descriptas en los otros manuales.

## Pantallas de la app (canvas)

| Pantalla | Para qué sirve |
|---|---|
| **Splash** | Pantalla de arranque; si entrás desde un enlace de notificación de Teams, te lleva directo al detalle de esa solicitud. |
| **Clients** | Listado de clientes; crear/editar/activar/desactivar genera una solicitud de cambio. |
| **ClientInformation** | Ficha de un cliente: datos generales, sucursales y correos autorizados asociados. |
| **RFC** | Listado de solicitudes de cambio, con filtro de pendientes o todas. |
| **ChangeRequestDetails** | Pantalla de aprobación: cabecera de la solicitud, líneas de cambio (valor viejo/valor nuevo), botones Aprobar/Rechazar. |

También existe una app **model-driven** equivalente (la que ves con la
navegación estándar de Dataverse, con áreas "Operations", "Governance" y
"Reviews"), que accede a las mismas tablas. Desde ahí, un botón de la barra de
comandos ("Proponer cambio") abre un formulario rápido pre-cargado para
generar la misma solicitud de cambio sin pasar por la app canvas.

## Paso a paso — como Contributor (solicitante)

1. Entrá a **Clients** (para un cliente nuevo) o a **ClientInformation**
   (para editar uno existente, o para agregar/editar una sucursal o un correo
   autorizado).
2. Completá el formulario con los datos nuevos o corregidos.
3. Al guardar, **el sistema no modifica el dato maestro todavía**: crea una
   solicitud de cambio (una cabecera) con una línea por cada campo
   modificado, en estado **Enviada ("Submitted")**.
4. Podés revisar, reenviar o cancelar tu solicitud desde
   **ChangeRequestDetails** mientras siga en un estado que lo permita.
5. Al enviarla, el sistema notifica automáticamente por Teams a los
   aprobadores correspondientes (crea el chat grupal si todavía no existe).
6. Si ya existe otra solicitud pendiente que modifica el mismo campo del
   mismo registro, el sistema **bloquea el envío** para evitar ediciones en
   conflicto — vas a tener que esperar a que se resuelva la otra solicitud
   primero.

## Paso a paso — como Approver (aprobador)

1. Entrá por **"Solicitudes de cambio"** (pantalla RFC). Acá ves **todas**
   las solicitudes, no solo las que te tocan a vos.
2. Abrí el detalle de una solicitud en **ChangeRequestDetails**. Los botones
   **Aprobar**/**Rechazar** solo aparecen si la solicitud está en estado
   **Enviada** y si tenés permiso de aprobador para esa tabla objetivo en
   particular.
3. Al resolver, el sistema te va a pedir un **comentario obligatorio**.
4. La decisión se aplica a **todas las líneas de la solicitud al mismo
   tiempo** — no podés aprobar una línea y rechazar otra dentro de la misma
   solicitud, aunque el sistema internamente sí guarda un estado por línea.
5. **Importante: aprobar acá no aplica el cambio.** La app deja la solicitud
   en estado **Aprobada** y ahí termina su trabajo. La escritura real sobre
   el dato del cliente/sucursal/correo ocurre después, automáticamente, por
   flujos separados del sistema (según el tipo de cambio: Crear, Actualizar,
   Activar o Desactivar), que también son los que finalmente mueven la
   solicitud a **Aplicada** o **Fallida**. Una vez resuelto ese paso
   automático, el sistema te notifica por Teams (a vos y a quien la solicitó)
   el resultado final.

## Limitaciones conocidas

- **La aplicación del cambio no es inmediata ni depende de esta app.** Entre
  que aprobás una solicitud y el dato del cliente queda realmente actualizado
  pasa un procesamiento automático externo a Client Administration. Si un
  cliente te consulta por qué su dato "aprobado" todavía no se ve actualizado,
  puede ser normal (está en tránsito) o puede indicar que ese procesamiento
  automático falló — en ese caso la solicitud debería quedar en **Fallida**
  en vez de **Aplicada**; si la ves trabada en **Aprobada** por mucho tiempo,
  repórtalo al equipo técnico.
- **Los cambios sobre campos de tipo "lista de opciones" (option set) no se
  aplican.** Si una solicitud modifica un campo de ese tipo, el mecanismo
  automático que aplica el cambio real **lo omite silenciosamente** — no da
  error, simplemente ese campo específico no queda actualizado aunque el
  resto de la solicitud sí. Si aprobaste un cambio que incluye un campo de
  ese tipo, confirmá manualmente que se aplicó.
- **El nombre "RFC" puede confundir.** En este sistema significa
  exclusivamente "Request For Change" (solicitud de cambio); no tiene
  relación con ningún identificador fiscal del cliente.
- En la app model-driven, las áreas "Pending reviews" y "Applied changes"
  (dentro de "Reviews") apuntan a la misma tabla de solicitudes; la
  diferenciación entre pendientes y aplicadas depende de una vista o filtro
  que no está garantizado por defecto — si no ves la distinción esperada,
  usá los filtros de la vista para separarlas manualmente.
