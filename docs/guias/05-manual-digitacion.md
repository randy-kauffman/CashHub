# Manual del digitador — App Digitization

## Propósito del rol

Sos el último paso antes de que el dinero quede acreditado en el core
bancario **IBS Branch**. Agrupás en lotes las transacciones de efectivo puro
que ya pasaron por recuento (y, si hubo diferencia, por la aprobación del
supervisor), descargás el archivo de carga masiva para ese lote, y lo subís a
IBS Branch. Las transacciones que traen cheques de otros bancos no entran en
la carga masiva: las digitás una por una directamente en IBS Branch.

## Prerequisitos

- Acceso a la app **Digitization** (`sanic_chappdigitization`).
- Acceso a **IBS Branch** para subir el archivo de carga masiva y para
  digitar manualmente las transacciones con cheques — este paso ocurre
  **fuera de CashHub**, en el core bancario.

## Pantalla de la app

Digitization tiene una sola pantalla, organizada en tres pestañas:

| Pestaña | Para qué sirve |
|---|---|
| **Tx sin lote** | Transacciones de efectivo puro, todavía no agrupadas en un lote. |
| **Tx Manuales** | Transacciones que traen cheques y no pueden ir por carga masiva. |
| **Lotes** | Ciclo de vida de cada lote y descarga del archivo para IBS Branch. |

## Paso a paso

### 1. Tx sin lote — armar el lote del día

1. En esta pestaña ves todas las transacciones de efectivo puro pendientes de
   agrupar.
2. Usá **"Realizar corte"** para crear un lote nuevo. El sistema asigna
   automáticamente a ese lote todas las transacciones sin lote que no traigan
   cheques.

### 2. Tx Manuales — lo que no entra en el corte

1. Acá ves las transacciones que quedaron afuera del corte porque traen
   cheques (BAC u otros bancos).
2. Estas **no se cargan de forma masiva**: digitalas una por una directamente
   en IBS Branch.
3. Una vez digitada en IBS Branch, marcala como **"Aplicado"** en esta
   pantalla. El sistema avanza automáticamente a la siguiente transacción
   pendiente.

### 3. Lotes — descarga y ciclo de vida

1. Seleccioná un lote para ver sus totales por moneda (córdobas, dólares,
   euros) y su detalle.
2. Usá **"Descargar lote"**. El sistema arma el archivo con el formato que
   espera IBS Branch (código de operación, cuenta, monto, moneda, referencia,
   etc.) y te lo entrega para descargar.
3. **Subí el archivo manualmente a IBS Branch** — este paso es externo a
   CashHub, no hay integración automática.
4. Cuando confirmes que subiste el archivo, marcá el lote como
   **"Ingresado"** (esta opción solo aparece si el lote está en estado
   "Generado").
5. Cuando el core confirme que el lote fue procesado, marcalo como
   **"Aplicado"** (esta opción solo aparece si el lote está en estado
   "Ingresado"). Con esto cerrás el ciclo del lote.

## Limitaciones conocidas

- **No hay confirmación automática desde IBS Branch.** El paso de "subir el
  archivo" y las marcas de "Ingresado"/"Aplicado" son 100% manuales — el
  sistema confía en que vos actualices el estado correctamente después de
  confirmar en el core. Si te olvidás de marcar un lote, CashHub va a seguir
  mostrándolo como pendiente aunque ya esté acreditado.
- **Algunos campos del archivo de carga masiva son valores fijos**, no
  calculados por depositante: disponibilidad inmediata, manifiesto, tipo de
  identificación, nacionalidad y los dos números de identificación van con un
  valor genérico igual para todas las transacciones del lote. Esto sugiere
  que la carga se hace bajo una identidad genérica/de agencia en IBS Branch,
  no bajo la identidad de cada cliente depositante — si tu banco necesita otra
  cosa, es un punto para confirmar con el equipo de negocio, no algo que
  puedas cambiar desde la app.
- **La app no genera el archivo final por sí sola**: arma los datos y se los
  entrega a un flujo automático que produce el archivo descargable. Si ese
  flujo falla (por ejemplo, por un problema de conexión con OneDrive), el
  botón "Descargar lote" puede no traerte el archivo — reintentá antes de
  reportarlo como error del lote en sí.
- **No hay verificación de rol dentro de la app.** Cualquier persona a la que
  se le comparta Digitization puede hacer cortes, marcar transacciones como
  aplicadas y descargar lotes.
