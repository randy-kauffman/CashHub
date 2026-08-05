# Plan de pruebas — validación de correos y notificaciones al cliente

Batería para probar de punta a punta: ingesta del correo, las 27 reglas, la
materialización de la declaración y **el correo de respuesta al cliente**.

Los archivos están en [`docs/plantillas/pruebas/`](../plantillas/pruebas/) y se regeneran con
`python3 scripts/build-test-workbooks.py`. Cada uno está **verificado por tests automáticos** (`TestBatteryTests`): si alguien cambia una regla
y un archivo deja de probar lo que dice esta guía, la suite lo delata en CI.

## Antes de empezar

| Requisito | Cómo se verifica |
|---|---|
| Flujos activos: `CH-Flow-IngestEmails`, `CH-Flow-Reply to client email`, `CH-Flow-MailboxSweep` | make.powerautomate.com → Soluciones → CashHubFlows |
| Datos sintéticos sembrados | `./scripts/seed-data.sh data/synthetic` |
| Su remitente autorizado para **Distribuidora El Porvenir** (CIF `0012024001`) | Tabla `CH-Tbl-ClientEmail`, o agréguelo desde la app de administración de clientes |
| Plantillas de correo activas | Tabla `CH-Tbl-EmailTemplate`: `reply-accepted` y `reply-rejected` |

**Cliente de referencia**: Distribuidora El Porvenir, S.A. — CIF `0012024001`, cuentas
`100200111` (NIO) y `100200112` (USD).

**Cómo se envía cada prueba**: correo al buzón de CashHub con asunto que empiece con
**"Pre-registro"** y el archivo del caso como **único adjunto**.

**Dónde se verifica**:
- El correo de respuesta que llega a su bandeja (es lo que se está probando).
- Tabla `CH-Tbl-ReceivedEmails`: estado, número de gestión, texto de errores.
- Tabla `CH-Tbl-ValidationRuns` → `CH-Tbl-ValidationResults`: el log de las 27 reglas.
- Tabla `CH-Tbl-Declaration` y sus bolsas/depósitos/cheques (solo en los casos aceptados).

---

## Bloque A — Casos aceptados (notificación de éxito)

| # | Archivo | Qué prueba | Resultado esperado |
|---|---|---|---|
| A1 | `01-OK-completo.xlsx` | Camino feliz completo: 3 depósitos, 2 bolsas, dos monedas, efectivo + cheques | **Correo verde** "Declaración recibida y validada" con tabla de 2 bolsas (B-1001: USD 800 / NIO 15,000; B-1002: USD 1,200). Declaración materializada con 3 depósitos y 2 cheques colgando del depósito 1. |
| A2 | `02-OK-minimo.xlsx` | Mínimo aceptable: un depósito, solo efectivo, sin cheques | Correo verde con una sola bolsa (B-2001: NIO 5,000). |
| A3 | `14-OK-volumen.xlsx` | 25 depósitos en 25 bolsas alternando monedas | Correo verde con las 25 bolsas listadas. **Verificar que el correo no se corte y siga legible en celular.** |

**Qué mirar en el correo verde**: banner verde, tabla de bolsas con totales por moneda,
fila "Total" cuando hay más de una bolsa, bloque "Qué sigue" (entregar a la transportadora)
y al pie el **número de gestión** (`Email-N`) con el id de validación.

---

## Bloque B — Errores de negocio del cliente (notificación de corrección)

| # | Archivo | Regla | Resultado esperado en el correo |
|---|---|---|---|
| B1 | `03-ERR-cuenta-ajena.xlsx` | VR-23 | 1 error: la cuenta `999999999` (fila 7) no pertenece al cliente. **Con celda `Depositos!K7`.** |
| B2 | `04-ERR-sin-cif.xlsx` | VR-24 | 1 error: la celda `Depositos!J2` debe contener el CIF. |
| B3 | `05-ERR-cif-desconocido.xlsx` | VR-25 | 1 error: el CIF `0099999999` no corresponde a ningún cliente. El correo **no queda atribuido** a ningún cliente. |
| B4 | `06-ERR-sin-bolsa-ni-cuenta.xlsx` | VR-10, VR-11 | 2 errores ubicados: `J6` sin bolsa, `K7` sin cuenta. |
| B5 | `07-ERR-montos-invalidos.xlsx` | VR-20 | Errores por valores negativos en `O6` y `P6`. **Nota**: VR-21 (total en cero) se omite porque depende de VR-20 — es la cadena anti-ruido funcionando. |
| B6 | `08-ERR-tipos-y-moneda.xlsx` | VR-17, VR-18 | 2 errores: `O6` contiene texto donde va número; `L6` declara moneda `CAD` no soportada. |
| B7 | `09-ERR-cuenta-dos-monedas.xlsx` | VR-13 | 1 error: la cuenta `100200112` aparece con dos monedas (fila 7). |
| B8 | `10-ERR-cheques.xlsx` | VR-14 | Cheque `CHQ-REP` repetido (fila 3). **Nota**: VR-22 (huérfano) y VR-12 (descuadre) se omiten por dependencia de VR-14. |
| B9 | `11-ERR-cheque-incompleto.xlsx` | VR-15 | 2 errores: celdas `C2` (tipo) y `D2` (moneda) vacías en el cheque. |
| B10 | `12-ERR-sin-depositos.xlsx` | VR-09 | 1 error: no hay depósitos declarados. |
| B11 | `13-ERR-multiple.xlsx` | VR-10, VR-20, VR-13, VR-22, VR-23 | **6 errores mezclados**: es la prueba del agrupado del correo — verificar que la tabla los liste ordenados por hoja y fila, y que se lea sin esfuerzo. |
| B12 | `15-ERR-desglose-cheques.xlsx` | VR-28 | El **total** de cheques cuadra (300 = 300) pero el desglose está cruzado: se declaran como cheques del BAC y el cheque listado es de otro banco. 2 errores, uno por cada columna del desglose. |

**Qué mirar en el correo rojo**: banner rojo con el conteo correcto ("Encontramos N puntos por
corregir"), sección "Sobre el correo y el archivo" solo si hay errores sin celda, sección
"En el archivo de Excel" con la columna **Hoja / Celda** en formato monoespaciado, el tip de
`Ctrl+I`, el bloque "Cómo continuar" y el número de gestión al pie.

---

## Bloque C — Errores del sobre (sin abrir el archivo)

Estos no necesitan archivo especial: se prueban con cualquiera de los anteriores.

| # | Cómo provocarlo | Regla | Resultado esperado |
|---|---|---|---|
| C1 | Enviar con asunto **sin** "Pre-registro" (ej. "Depósitos de hoy") | VR-04 | Error de sobre: el asunto debe comenzar con 'Pre-registro'. |
| C2 | Enviar **sin adjunto** | VR-02 | Error: debe traer 1 adjunto y trae 0. Las reglas del archivo se omiten en cadena. |
| C3 | Enviar con **dos adjuntos** | VR-02 | Error: debe traer 1 adjunto y trae 2. |
| C4 | Enviar un **PDF o Word** en vez del Excel | VR-05 | Error: el adjunto debe ser .xlsx. |
| C5 | Enviar desde una dirección **no registrada** | VR-03 | Error: el correo no está registrado como remitente autorizado. |
| C6 | Enviar un Excel cualquiera (no la plantilla) | VR-27 / VR-07 | Error: el archivo no es la plantilla oficial o no tiene las 2 tablas. |
| C7 | Enviar un archivo `.xlsx` **corrupto** (renombrar un .txt) | VR-07 | Error explícito: no se pudo abrir como paquete ZIP. |

---

## Bloque D — Contingencia y operación

| # | Escenario | Cómo probarlo | Resultado esperado |
|---|---|---|---|
| D1 | **Reproceso manual** | Sobre un correo ya validado, invocar el Custom API `sanic_CHValidateReceivedEmail` con su id | Nueva corrida de validación; el correo se re-evalúa y se recompone la respuesta. |
| D2 | **Correo huérfano** | Apagar `CH-Flow-IngestEmails`, enviar un correo, esperar 10 min | El barrido detecta el flujo apagado / el correo sin registrar y alerta por Teams (si está configurada la variable `sanic_CHEVOpsTeamsGroupId`); deja latido en `CH-Tbl-SystemHeartbeat`. |
| D3 | **Pendiente sin validar** | Crear a mano un `ReceivedEmail` en estado Pendiente | El barrido lo re-valida solo en su próxima corrida (≤10 min). |
| D4 | **Plantilla de correo editada** | Cambiar un texto o color en `CH-Tbl-EmailTemplate` (`reply-rejected`) y reenviar un caso del bloque B | El correo sale con el diseño nuevo **sin redesplegar nada**. |
| D5 | **Plantilla desactivada** | Poner `IsActive = No` en `reply-accepted` y enviar `02-OK-minimo.xlsx` | El correo llega igual, con el diseño embebido de respaldo (nunca queda sin respuesta). |
| D6 | **Duplicado** | Reenviar exactamente el mismo correo | El segundo no se procesa (deduplicación por `InternetMessageId`): no debe crearse un segundo registro ni una segunda declaración. |

---

## Registro de resultados

| Caso | Fecha | Estado esperado | ¿Llegó el correo? | ¿Diseño correcto? | Observaciones |
|---|---|---|---|---|---|
| A1 | | Aceptado | | | |
| A2 | | Aceptado | | | |
| A3 | | Aceptado | | | |
| B1 | | Rechazado | | | |
| B2 | | Rechazado | | | |
| B3 | | Rechazado | | | |
| B4 | | Rechazado | | | |
| B5 | | Rechazado | | | |
| B6 | | Rechazado | | | |
| B7 | | Rechazado | | | |
| B8 | | Rechazado | | | |
| B9 | | Rechazado | | | |
| B10 | | Rechazado | | | |
| B11 | | Rechazado | | | |
| B12 | | Rechazado | | | |
| C1–C7 | | Rechazado | | | |
| D1–D6 | | — | | | |

## Cómo leer el log de una prueba

Cada correo procesado deja: una fila en `CH-Tbl-ValidationRuns` (resultado global) y **27 filas**
en `CH-Tbl-ValidationResults` — una por regla, con estado, duración en milisegundos y, en las que
fallan, la hoja, celda, fila y el mensaje exacto que se le mostró al cliente. Las reglas omitidas
por dependencia no se registran (evitan ruido).

Para consultar por número de gestión: busque el correo por su `Name` (`Email-N`) en
`CH-Tbl-ReceivedEmails` y siga sus corridas.
