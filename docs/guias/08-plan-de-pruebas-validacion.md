# Plan de pruebas — validación de correos y notificaciones al cliente

Batería para probar de punta a punta: ingesta del correo, las **29 reglas**, la
materialización de la declaración y **el correo de respuesta al cliente**.

**50 archivos** en [`docs/plantillas/pruebas/`](../plantillas/pruebas/), regenerables con
`python3 scripts/build-test-workbooks.py`. Los casos de archivo (01–40) están **verificados
por tests automáticos** (`TestBatteryTests`): si alguien cambia una regla y un archivo deja
de probar lo que dice esta guía, la suite lo delata en CI.

La batería tiene cuatro capas, y cada una responde una pregunta distinta:

| Capa | Casos | Pregunta que responde |
|---|---|---|
| **Reglas de negocio** | 01–22 | ¿El motor acepta lo válido y rechaza lo inválido, con un mensaje que el cliente entienda? |
| **Límites de columna** | 23–33 | ¿Qué pasa cuando el archivo pasa todas las reglas pero **no cabe** en la base? |
| **Valores raros** | 34–40 | ¿Se rompe con unicode, caracteres invisibles, inyección o volumen máximo? |
| **Matriz de remitentes** | 50–59 | ¿Le respondemos a quien corresponde, y le contamos solo lo que corresponde? |

## Antes de empezar

| Requisito | Cómo se verifica |
|---|---|
| Flujos activos: `CH-Flow-IngestEmails`, `CH-Flow-Reply to client email`, `CH-Flow-MailboxSweep` | make.powerautomate.com → Soluciones → CashHubFlows |
| **Fixture de pruebas montado** | `./scripts/seed-sender-matrix.py` (idempotente; `--dry-run` para ver qué haría) |
| Plantillas de correo activas | Tabla `CH-Tbl-EmailTemplate`: `reply-accepted`, `reply-rejected` y **`reply-generic`** |

`seed-sender-matrix.py` deja listo todo lo que la batería necesita: el cliente sintético de
los casos de archivo y los 10 clientes de la matriz de remitentes, cada uno en su estado.

**Cliente de referencia** (casos 01–40): Distribuidora El Porvenir, S.A. — CIF
**`00120240014`** (son 11 dígitos, no 10), cuentas `100200111` (NIO) y `100200112` (USD).

> ⚠️ Ese cliente tenía sus dos cuentas y el vínculo del remitente en **`PendingApproval`**.
> Desde que `recordstatus` gobierna la vigencia, `PendingApproval` **no autoriza**, así que
> la batería entera moría en VR-23/VR-26 antes de llegar a nada interesante. El fixture los
> deja en `Activo`. Si alguien vuelve a dejarlos pendientes, la batería vuelve a morir ahí.

### Las tres direcciones de prueba

No son intercambiables: cada una encarna un rol y **el resultado esperado depende de desde
cuál se envía**.

| Dirección | Rol | Vínculos que tiene |
|---|---|---|
| `randyekg@gmail.com` | **Rica** | Vínculos vigentes con varios clientes. La puerta de respuesta siempre le abre, así que aísla lo que depende del vínculo con *este* cliente. Es la que se usa para los casos 01–40. |
| `randyekg@outlook.com` | **Revocada** | Su único vínculo está `Inactivo`. Prueba "le cortamos en todos lados". |
| `edugonzalez95@hotmail.com` | **Desconocida** | Cero vínculos en toda la plataforma. Prueba "cliente por registrar" y "sospechoso". |

**Cómo se envía cada prueba**: correo al buzón de CashHub con asunto que empiece con
**"Pre-registro"** y el archivo del caso como **único adjunto**, desde la dirección que
indique el caso.

**Dónde se verifica**:
- El correo de respuesta que llega a su bandeja (es lo que se está probando).
- Tabla `CH-Tbl-ReceivedEmails`: estado, número de gestión, texto de errores, **clasificación
  del remitente** (`sanic_senderclassification`) y **motivo del triage** (`sanic_triagenotes`).
- Tabla `CH-Tbl-ValidationRuns` → `CH-Tbl-ValidationResults`: el log de las 29 reglas.
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
| B8 | `10-ERR-cheques.xlsx` | VR-14 | Cheque `700100` repetido (fila 3). **Nota**: VR-22 (huérfano) y VR-12 (descuadre) se omiten por dependencia de VR-14. |
| B9 | `11-ERR-cheque-incompleto.xlsx` | VR-15 | 2 errores: celdas `C2` (tipo) y `D2` (moneda) vacías en el cheque. **Nota**: VR-29 queda `Omitida` — depende de VR-15. |
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

## Bloque E — VR-29: el número de cheque son solo dígitos

Enviar desde **gmail**. El campo *serial number* de la línea MICR contiene únicamente
dígitos; los símbolos impresos son delimitadores. Wells Fargo especifica 4–10 dígitos, el
Reserve Bank of India 6. Ni letras ni guiones.

| # | Archivo | Qué prueba | Resultado esperado |
|---|---|---|---|
| E1 | `16-OK-cheque-ceros-izquierda.xlsx` | `0001234` y `0000001` | **Aceptado.** Verificar en `CH-Tbl-DeclaredChecks` que `sanic_checknumber` conserve los ceros: si guarda `1234` se perdió información y son cheques distintos. |
| E2 | `17-OK-cheque-espacios-alrededor.xlsx` | `"   700501   "` | **Aceptado** y guardado como `700501`. Un espacio de Excel no puede rechazar una declaración legítima. |
| E3 | `18-ERR-cheque-alfanumerico.xlsx` | `ABC123` | Rechazo VR-29. |
| E4 | `19-ERR-cheque-con-guion.xlsx` | `123-456` | Rechazo VR-29. |
| E5 | `20-ERR-cheque-no-entero.xlsx` | `1234.56`, `-500`, `1e5` | Rechazo VR-29 **con 3 hallazgos**. Son los tres que `TryDecimal` (VR-17) dejaría pasar: por eso VR-29 no se implementó agregando la columna a `NumericColumns`. |
| E6 | `21-ERR-cheque-espacio-interno.xlsx` | `12 34` | Rechazo VR-29 (distinto de E2). |
| E7 | `22-ERR-cheque-digitos-no-ascii.xlsx` | `١٢٣٤٥٦` | Rechazo VR-29. `char.IsDigit` los aceptaría; la validación compara contra `'0'..'9'` a propósito. |

---

## Bloque F — Límites de columna

Enviar desde **gmail**. Estos archivos **pasan todas las reglas**. Lo que se prueba es qué
pasa después: si el valor no cabe en la columna, el `Create` falla, el correo termina en
**Revisión con error** y el cliente recibe un error de sistema en vez de un mensaje claro.

Límites reales, verificados contra el ambiente:

| Columna | Límite |
|---|---|
| `sanic_chtbldeclaredbags.sanic_bagnumber` | String(50) |
| `sanic_chtbldeclaredchecks.sanic_checknumber` | String(30) |
| `sanic_chtbldeclareddeposits.sanic_accountnumber` | Integer 1 … 999.999.999 |
| `sanic_totalnio` / `sanic_totalusd` / `sanic_totaleur` | Decimal 0 … 100.000.000.000, precisión 2 |
| `sanic_chtblreceivedemails.sanic_declaredcif` | String(50) |

| # | Archivo | Qué prueba | Resultado esperado |
|---|---|---|---|
| F1 | `23-LIM-tula-50-exacto.xlsx` | Tula de 50 caracteres | **Aceptado**, cabe justo. |
| F2 | `24-LIM-tula-51-excede.xlsx` | Tula de 51 | 🔴 **HUECO CONOCIDO.** Ninguna regla mide el largo de la tula. Se espera error de sistema al materializar. |
| F3 | `25-LIM-cheque-30-exacto.xlsx` | Cheque de 30 dígitos | **Aceptado**, cabe justo. |
| F4 | `26-LIM-cheque-31-excede.xlsx` | Cheque de 31 dígitos | 🔴 **HUECO CONOCIDO.** VR-29 valida la *forma*, no el *largo*. |
| F5 | `27-LIM-cuenta-10-digitos.xlsx` | Cuenta `1000000000` | Rechazo VR-23. La cuenta no es del cliente, así que VR-23 la ataja **antes** del desborde del entero. El límite solo se alcanzaría si esa cuenta estuviera registrada al cliente. |
| F6 | `28-LIM-cuenta-cero.xlsx` | Cuenta `0` | Rechazo VR-23, mismo razonamiento que F5. |
| F7 | `29-LIM-monto-tope-exacto.xlsx` | Monto = 100.000.000.000 | **Aceptado**, tope exacto. |
| F8 | `30-LIM-monto-excede.xlsx` | Monto = tope + 1 | 🔴 **HUECO CONOCIDO.** |
| F9 | `31-LIM-decimales-de-mas.xlsx` | `100.005` contra precisión 2 | ❓ **A determinar**: ¿redondea silenciosamente o falla? Anotar cuál de las dos. Si redondea, el cliente declaró un monto y se guardó otro. |
| F10 | `32-LIM-suma-bolsa-excede.xlsx` | 3 depósitos de 5e10 en la **misma** tula | 🔴 **HUECO CONOCIDO.** Cada depósito pasa VR-20/VR-21 por separado; nadie valida el **total de la bolsa**, que es lo que se persiste. |
| F11 | `33-LIM-cif-excede.xlsx` | CIF de 60 caracteres | Rechazo VR-25 **y** posible fallo al estampar `sanic_declaredcif` (String(50)). Si el correo queda en Error en vez de Rechazado, es el segundo problema tapando al primero. |

---

## Bloque G — Valores raros

Enviar desde **gmail**.

| # | Archivo | Qué prueba | Resultado esperado |
|---|---|---|---|
| G1 | `34-OK-tula-case-y-espacios.xlsx` | `tula-34`, `TULA-34` y `"  tula-34  "` en el mismo archivo | **Aceptado con UNA sola bolsa**, guardada como `TULA-34` en mayúsculas. Verificar en `CH-Tbl-DeclaredBags`: si aparecen 2 o 3 bolsas, la normalización no está funcionando. |
| G2 | `35-RARO-unicode.xlsx` | Acentos, emoji y CJK en tula y referencia | Aceptado. Verificar que el **correo de respuesta** los muestre bien y no queden como `?` o mojibake. |
| G3 | `36-RARO-invisibles.xlsx` | Zero-width, marca RTL, tabs y saltos de línea | Aceptado sin romper el XML ni el HTML de la respuesta. Revisar que el correo no se vea desmaquetado. |
| G4 | `37-RARO-inyeccion.xlsx` | `<script>`, SQL, fórmula de Excel y **`{{cliente}} {{gestion}}`** en la referencia | Aceptado, y el correo de respuesta debe mostrar esos textos **literales**. El último es el que importa: la respuesta se arma con plantillas `{{token}}`; si el token del cliente se sustituye, hay inyección de plantilla. |
| G5 | `38-RARO-solo-espacios.xlsx` | Tula y cuenta con solo espacios | Rechazo VR-10 y VR-11: los espacios se tratan como vacío. |
| G6 | `39-RARO-referencia-larga.xlsx` | Referencia de 1000 caracteres | Anotar si se guarda completa, se recorta o falla. |
| G7 | `40-OK-volumen-tope.xlsx` | **200 depósitos y 50 cheques** (tope del rango de la plantilla) | Aceptado. **Medir el tiempo de ejecución** y verificar que el correo de respuesta corte el resumen con aviso en vez de reventar. |

---

## Bloque H — Matriz de remitentes

**Es el bloque que prueba la lógica de vínculos.** El archivo es trivial y siempre válido a
propósito: lo que se prueba es **quién envía** contra **el estado del cliente y del vínculo**.

Requiere `./scripts/seed-sender-matrix.py`. Recordar las dos puertas:

- **Puerta de respuesta**: ¿sale un correo? → ¿el remitente tiene *algún* vínculo vigente con el banco?
- **Puerta de contenido**: ¿qué le decimos? → ¿el vínculo con *este* cliente está vigente?

| # | Archivo | Enviar desde | Cliente | Vínculo | Esperado |
|---|---|---|---|---|---|
| H1 | `50-MTX-vigente.xlsx` | gmail | Activo | Activo | ✅ **Aceptación con detalle completo.** Clasificación `Remitente registrado`. |
| H2 | `51-MTX-otro.xlsx` | gmail | Activo | **ninguno** | ✅ Responde **genérico** (sin nombre de cliente ni hallazgos). Clasificación `Contacto de otro cliente`, revisión `Denegado`. |
| H3 | `52-MTX-revocado.xlsx` | gmail | Activo | **Inactivo** | ✅ Responde **genérico**. Clasificación `Acceso no autorizado para el cliente`, revisión **`Manual`**. El motivo dice `REVOCADO`. |
| H4 | `53-MTX-pendiente.xlsx` | gmail | Activo | **PendingApproval** | ✅ Igual que H3, pero el motivo dice **`PENDIENTE DE APROBACION`**. Mismo balde, distinto diagnóstico. |
| H5 | `54-MTX-pdeact.xlsx` | gmail | Activo | **PendingDeactivation** | ✅ **Aceptación con detalle.** La baja se pidió pero nadie firmó: el vínculo **sigue operando**. |
| H6 | `55-MTX-cli-inactivo.xlsx` | gmail | **Inactivo** | Activo | ✅ **Rechazo con detalle** (es su contacto autorizado). Mensaje: *"no corresponde a ningún cliente activo"*. Verificar que el correo **quede atribuido** al cliente en `sanic_client`: si queda huérfano, se perdió la trazabilidad. |
| H7 | `56-MTX-cli-pending.xlsx` | gmail | **PendingApproval** | Activo | ✅ Rechazo con detalle. El `PendingApproval` del **cliente** tampoco autoriza. |
| H8 | `57-MTX-cli-pdeact.xlsx` | gmail | **PendingDeactivation** | Activo | ✅ **Aceptado.** El cliente opera hasta que firmen la baja. Es el contraste exacto con H7. |
| H9 | `58-MTX-taxid.xlsx` | gmail | Activo | Activo | ✅ Aceptado, **resuelto por `sanic_taxid`** (`J0310000000009`) y no por CIFBCO/CIFCOM. Prueba el tercer identificador. |
| H10 | `50-MTX-vigente.xlsx` | **outlook** | Activo | ninguno con outlook | ❌ **No responde.** El único vínculo de outlook está revocado. Clasificación `Revocado`, revisión `Omitido`. |
| H11 | `50-MTX-vigente.xlsx` | **hotmail** | Activo | ninguno | ❌ **No responde.** Clasificación `Cliente sin registrar`, revisión **`Manual`** → lo levanta Banca de Empresas. |
| H12 | `59-MTX-cif-inexistente.xlsx` | **hotmail** | no existe | ninguno | ❌ **No responde.** Clasificación **`Sospechoso`** + alerta: alguien tiene la plantilla oficial y el CIF no existe. |

> **La diferencia entre H11 y H12 es el corazón del triage.** Los dos son desconocidos con
> la plantilla oficial; el primero es trabajo administrativo y el segundo es un evento de
> seguridad. Si los dos caen en el mismo balde, el triage está roto.

---

## Huecos conocidos que esta batería expone

No son fallas de las plantillas: son validaciones que **faltan** y que los casos F2, F4, F8,
F10 y (según resultado) F9 y F11 hacen visibles. Documentados acá para que el resultado
"error de sistema" se lea como esperado y no como sorpresa.

| Hueco | Caso | Qué haría falta |
|---|---|---|
| Largo de la tula sin validar (String 50) | F2 | Regla nueva, o recorte explícito con aviso al cliente |
| Largo del número de cheque sin validar (String 30) | F4 | Extender VR-29 con un largo máximo parametrizable |
| Monto por depósito sin tope (Decimal 1e11) | F8 | Regla nueva sobre el máximo |
| Suma por bolsa sin tope | F10 | Regla nueva que sume por tula antes de materializar |
| Precisión decimal | F9 | Definir si se rechaza o se redondea, y decirlo |
| CIF más largo que su columna | F11 | Validar largo en VR-24 |

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
| E1–E2 | | Aceptado | | | |
| E3–E7 | | Rechazado VR-29 | | | |
| F1, F3, F7 | | Aceptado | | | |
| F2, F4, F8, F10 | | 🔴 Error de sistema (hueco) | | | |
| F5, F6, F11 | | Rechazado | | | |
| F9 | | ❓ a determinar | | | |
| G1–G4, G6, G7 | | Aceptado | | | |
| G5 | | Rechazado | | | |
| H1, H5, H8, H9 | | Aceptado + detalle | | | |
| H2, H3, H4 | | Respuesta GENERICA | | | |
| H6, H7 | | Rechazo + detalle | | | |
| H10, H11, H12 | | SIN respuesta | | | |

## Cómo leer el log de una prueba

Cada correo procesado deja: una fila en `CH-Tbl-ValidationRuns` (resultado global) y **29 filas**
en `CH-Tbl-ValidationResults` — una por regla, con estado, duración en milisegundos y, en las que
fallan, la hoja, celda, fila y el mensaje exacto que se le mostró al cliente. Las reglas omitidas
por dependencia **sí se registran** como `Omitida`, con la regla madre que las bloqueó en la nota.

Para los casos del bloque H, el registro que importa no está en los resultados sino en el
correo mismo: `sanic_senderclassification` (en qué balde cayó el remitente) y
`sanic_triagenotes` (el motivo completo, con las reglas que lo sustentan). Si la clasificación
es la correcta pero el motivo es genérico, el triage está adivinando.

Para consultar por número de gestión: busque el correo por su `Name` (`Email-N`) en
`CH-Tbl-ReceivedEmails` y siga sus corridas.
