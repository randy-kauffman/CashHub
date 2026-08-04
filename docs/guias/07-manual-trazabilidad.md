# Manual de consulta y trazabilidad — Hub y App Deposits Tracker

## Propósito del rol

Este manual es para cualquier persona que necesite **consultar** el estado de
un lote, una planilla, una tula o un depósito sin operar sobre él — auditoría,
soporte, gerencia de operaciones, o cualquier rol que necesite trazabilidad
sin tocar los datos. El **Hub** es la puerta de entrada a todas las apps de
CashHub; **Deposits Tracker** es la app de consulta en sí, de solo lectura.

## Prerequisitos

- Acceso al **Hub** (`sanic_chapphub`), que lista las apps disponibles para
  vos.
- Acceso a la app **Deposits Tracker** (`sanic_chappdepositstracker`) para
  poder efectivamente entrar a consultar (ver Limitaciones sobre el filtrado
  del Hub).

## El Hub — punto de entrada

1. Al entrar al Hub, ves una galería de tarjetas, una por cada app disponible
   en el catálogo.
2. Hacé clic en la tarjeta de la app que necesitás (por ejemplo, "Deposits
   Tracker"). El sistema **reemplaza la pestaña actual** por esa app — no
   abre una pestaña nueva.
3. Los indicadores visuales que ves en el Hub (contadores tipo "Procesadas:
   128", "En revisión: 7", luces de estado) son **puramente decorativos**:
   son una imagen fija, no están conectados a datos reales del sistema. No
   los uses como referencia de estado real.

## Deposits Tracker — consulta paso a paso

1. En la sección **Buscar**, elegí el tipo de identificador que tenés a mano:
   **Lote de recepción**, **Planilla** o **Tula**, e ingresá el
   código/número.
2. **Vista de Recepción**: te muestra el detalle del lote encontrado y la
   tabla de tulas asociadas a ese lote.
3. **Vista de Tula**: seleccioná una tula para ver su detalle, con dos
   pestañas:
   - **General**: datos de la sesión de recuento, el cajero que la contó, el
     cliente, y la comparación **Declarado vs. Recontado** por moneda.
   - **Por depósito**: una grilla con el desglose de cada depósito dentro de
     la tula (efectivo vs. recontado).
4. **Vista de Cheques**: los cheques asociados a la tula, con monto, moneda,
   número, estado y tipo.

## Limitaciones conocidas

- **El Hub no filtra realmente las apps por tu grupo de acceso**, pese a que
  internamente arma una lista filtrada por membresía de grupo de Azure AD:
  la galería que ves en pantalla muestra **todas** las apps habilitadas en el
  catálogo, sin aplicar ese filtro. En la práctica, esto significa que podés
  ver en el Hub tarjetas de apps para las que en realidad no tenés acceso
  real — al hacer clic, la app te va a bloquear igual si no tenés permiso
  (la seguridad real vive en Dataverse/en a quién se comparte cada app, no en
  el Hub), pero no te sorprenda ver más opciones de las que finalmente podés
  usar.
- **Deposits Tracker es exclusivamente de consulta.** No hay ninguna acción
  de edición, y no vas a poder corregir un dato incorrecto desde acá — para
  eso hay que ir a la app operativa correspondiente (Reception, Recounting o
  Supervisor).
- **Los botones "Declaration", "Deposits" y "Timeline" existen en la
  pantalla pero están deshabilitados** — es funcionalidad planeada que
  todavía no está implementada. No es un error si no responden al hacer
  clic.
- **No hay un estado visible de "acreditado en IBS Branch" ni "rechazado" a
  nivel de depósito** dentro de esta app. Si necesitás confirmar si un
  depósito específico ya fue acreditado en el core, tenés que consultarlo en
  la app Digitization (estado del lote) o directamente en IBS Branch — el
  Tracker no lo muestra.
