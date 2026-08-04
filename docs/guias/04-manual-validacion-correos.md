# Manual del analista de correos — App Email Validation Console

## Propósito del rol

Sos el primer filtro del proceso: revisás los correos que llegan al buzón
compartido con la plantilla Excel que el cliente usa para declarar el
contenido de sus tulas, corrés las validaciones automáticas sobre esa
plantilla, y según el resultado, dejás la declaración cargada en el sistema o
le respondés al cliente explicando el rechazo.

## Prerequisitos

- Acceso a la app **Email Validation Console**
  (`sanic_chappemailvalidationconsole`).
- Acceso al buzón de correo compartido del banco (la app lo lee vía
  Microsoft Graph/Outlook).
- No hace falta que sepas qué reglas exactas corre el sistema para tu trabajo
  diario, pero si necesitás explicarle a un cliente por qué se rechazó su
  declaración, el detalle de cada regla (VR-01 a VR-23) está documentado en
  [`../referencia/conexiones-y-flujos.md`](../referencia/conexiones-y-flujos.md).

## Pantallas de la app

| Pantalla | Para qué sirve |
|---|---|
| **Correos electrónicos** | Bandeja de entrada del buzón compartido: ver correos, adjuntos y darlos de alta en el sistema. |
| **Cola de procesamiento** | Corre las validaciones sobre el Excel de cada correo dado de alta, y responde al cliente. |
| **TestUDF** | Pantalla de pruebas técnicas del motor de reglas — **no forma parte de tu trabajo diario**, es de uso de quien mantiene el sistema. |

## Paso a paso

### 1. Correos electrónicos

1. Al entrar, la app carga el contenido del buzón compartido.
2. Seleccioná un correo para ver su cuerpo y sus adjuntos.
3. Si corresponde procesarlo, usá **"Incluir + Validar"**:
   - El sistema da de alta el correo en el sistema.
   - Descarga sus adjuntos.
   - Archiva el correo original en Outlook (para no volver a verlo en la
     bandeja de entrada general).

### 2. Cola de procesamiento

1. Seleccioná el correo pendiente que diste de alta en el paso anterior. El
   sistema te muestra una previsualización de los datos del Excel adjunto.
2. Usá **"Crear lote de validación"** para arrancar una corrida de
   validación sobre ese correo.
3. Usá **"Correr todas las validaciones"**. El sistema ejecuta, en orden, las
   23 reglas de validación (VR-01 a VR-23) sobre el Excel, y guarda el
   resultado (pasó/no pasó) de cada una. A alto nivel, estas reglas cubren:
   - Cantidad, extensión y tamaño de los adjuntos.
   - Que el remitente sea un correo de cliente autorizado y activo.
   - Que el asunto tenga el formato esperado.
   - Que el Excel se pueda leer correctamente y tenga las columnas
     esperadas.
   - Que no falten filas de bolsa/cuenta, ni campos obligatorios.
   - Que los montos de cheques y depósitos cuadren por moneda.
   - Que no haya cuentas declarando dos monedas distintas, cheques
     duplicados, montos negativos o en cero, ni cheques sin depósito
     asociado.

   El detalle exacto de cada regla (qué compara y con qué criterio) está en
   [`../referencia/conexiones-y-flujos.md`](../referencia/conexiones-y-flujos.md) —
   no lo repetimos acá para no duplicar y desactualizar esa referencia.

4. **Si alguna regla falla**: la corrida queda marcada como fallida y el
   correo pasa automáticamente a **"Denegado"**.
5. **Si todas las reglas pasan**: la corrida queda en **"Aprobado"** y se
   habilita el botón **"Guardar resultados"**, que crea en el sistema, en
   cascada, la Declaración con sus bolsas, depósitos y cheques, y marca el
   correo como **"Aceptado"**.
6. **Responder correo**: arma automáticamente un correo en HTML explicando si
   la declaración fue aceptada o rechazada (con el detalle de qué reglas
   fallaron, si corresponde) y lo envía al cliente.

## Limitaciones conocidas

- **El flujo que ingesta correos nuevos automáticamente al sistema
  (`CH-Flow-IngestEmails`) está desactivado** en la configuración analizada.
  Esto significa que, salvo que el equipo técnico lo reactive, los correos
  nuevos que llegan al buzón compartido pueden no estar generando
  automáticamente el registro que esta app necesita para mostrarlos en la
  cola. Si notás que correos que sabés que llegaron no aparecen en la app,
  confirmá con el equipo de sistemas si este flujo está activo antes de
  asumir que el correo no llegó.
- **Dos de las 23 reglas no validan realmente lo que su nombre sugiere:**
  - La regla que debería validar el dominio/formato del correo remitente
    siempre da "Pass", sin comparar nada — no bloquea correos de remitentes
    no autorizados por sí sola.
  - La regla que debería validar la extensión del archivo adjunto también
    siempre da "Pass" por un error interno, aunque la comparación esté
    calculada — nunca puede fallar.

  En la práctica, esto significa que **no confíes únicamente en que "pasaron
  todas las validaciones" para dar por buenos el remitente o el formato del
  archivo** — hacé una revisión visual básica de esos dos puntos antes de
  aceptar una declaración.
- **No hay verificación de rol dentro de la app.** Cualquier persona a la que
  se le comparta esta app puede validar y aceptar/rechazar declaraciones.
- La pantalla "TestUDF" no es parte del proceso de negocio: es una herramienta
  de pruebas técnicas, no la uses para procesar correos reales.
