# Guía 07 — Plan de pruebas de CH-App-DepositsTracker

Tres partes, en este orden: **delegación** (el enemigo silencioso), **funcionalidad**
y **performance**. El entorno DEV ya tiene el universo de estrés sembrado
(~30.000 registros marcados `STRESS`), y esta guía usa **registros centinela
reales** con sus valores esperados calculados contra el Web API — probás contra
la verdad del servidor, no contra lo que la app quiera mostrar.

## La verdad del servidor (calculada 2026-08-14)

| Centinela | Valor | Para qué sirve |
|---|---|---|
| Correo stress MÁS VIEJO | `Email-1115` | LookUp más allá de las primeras filas |
| Correo stress MÁS NUEVO | `Email-1238` | orden descendente correcto |
| Correo RECHAZADO | `Email-1045` | Falló + Omitidas + sin declaración |
| Lote MONSTRUO | `RB-01045` | 600 planillas · Decl. C$ **112,728,518.10** · US$ **8,324,767.85** · En progreso |
| Planilla MONSTRUO | `PL-STRESS-c83a2c-0` | 300 tulas en una planilla |
| Tula CON DIFERENCIA | `T-STRESS-2a7d72-1-0` | rec C$ 33,182.20 vs decl C$ 32,716.49 → **+465.71 (sobrante)** |
| Tula ACREDITADA | `T-STRESS-327d72-0-1` | su tx `T-0000001244` en lote **TB-1029** (Aplicado) |
| Tula rica en depósitos | `T-STRESS-04d450-1-0` | 4 depósitos (máximo actual del seed) |
| Totales globales | 220 correos stress · 2.149 tulas recontadas · **191 con diferencia** · 1.065 MatchedCheck · 546 resultados Omitida · 300 tx acreditadas | conteos de referencia |

---

## PARTE 1 — Delegación (lo que te quita el sueño, con razón)

**El problema en una frase**: cuando una fórmula no delega, Dataverse manda las
primeras N filas (tu límite de filas: 500 u 2000), Power Apps filtra ESO en el
cliente, y todo lo que está después de la fila N **desaparece sin error, sin
warning en runtime, sin nada**. Con 5.000+ transacciones y 3.275 tulas
sembradas, cualquier no-delegable oculto YA está escondiendo datos hoy.

### D0 — Los tres chequeos estáticos (5 minutos, antes de tocar la app)

1. **App Checker → Delegación**: tiene que estar en **CERO**. Un solo warning
   de delegación = parar y arreglar antes de seguir.
2. **Configuración → General → Límite de filas**: anotá el valor (500 default).
   No lo subas a 2000 "para que aparezca la data" — eso es esconder el problema
   debajo de la alfombra, no arreglarlo.
3. Buscá a mano los patrones prohibidos en las fórmulas (Ctrl+Shift+F en Studio
   si tenés el experimento de búsqueda, o revisá los Items/OnVisible):
   `Upper(` / `Lower(` sobre columnas · `CountRows('CH-Tbl-...')` directo a
   datasource · `Sum('CH-Tbl-...'` directo · filtros por columna de entidad
   relacionada (`Algo.Otro.Campo = x` DENTRO de un Filter a datasource) ·
   `IsBlank(lookup)` dentro de Filter a datasource. Si aparece uno, es un bug.

### D1 — El protocolo del límite en 1 (la prueba reina)

**Configuración → General → Límite de filas = 1. Guardá. Probá TODO. Restaurá al terminar.**

Con límite 1, toda operación delegable sigue encontrando CUALQUIER registro
(el servidor hace el trabajo); toda operación no-delegable queda ciega con
UNA fila. Las diferencias gritan:

| # | Prueba con límite = 1 | Esperado (si delega) | Si falla significa |
|---|---|---|---|
| D1.1 | Buscar Tracking `Email-1115` (el más viejo, fila ~500+ por fecha) | Abre el correo | El LookUp del tracking no delega |
| D1.2 | Buscar Lote `RB-01045` | Abre el lote monstruo | LookUp de lote no delega |
| D1.3 | Buscar Planilla `PL-STRESS-c83a2c-0` | Abre el lote con la planilla seleccionada | LookUp de planilla no delega |
| D1.4 | Buscar Tula `T-STRESS-2a7d72-1-0` | Abre la tula con su conciliación | LookUp de tula no delega |
| D1.5 | En `RB-01045`, scrollear `galPlanillas` | Van llegando páginas (1 por 1 con límite 1: lento pero LLEGAN) | El Filter de planillas no delega |
| D1.6 | Abrir `Email-1045` → pestaña Validaciones | Aparecen filas (la colección quedará corta — ver nota) | — |

> **Nota sobre colecciones**: `ClearCollect` respeta el límite de filas: con
> límite 1 las colecciones (`colDeps`, `colVals`, `colChequesTula`…) traen 1
> fila — ESO ES ESPERADO en esta prueba, no es un bug. Lo que esta prueba
> valida es que los `LookUp`/`Filter`/`Sort` lleguen al registro correcto.
> Restaurá el límite al terminar (500).

### D2 — Casos centinela con el límite normal (500)

| # | Prueba | Esperado EXACTO | Si falla significa |
|---|---|---|---|
| D2.1 | scrBusqueda → lotes recientes | El lote stress más nuevo arriba; `RB-01045` visible con Decl. C$ **112,728,518.10** | Sort no delega o totales B4 rotos |
| D2.2 | `RB-01045` → ficha | PLANILLAS = **lo que cargó la galería** (~100 por página; scrolleá y crece — límite documentado del contador con AllItemsCount) | — (limitación declarada en la guía 03) |
| D2.3 | `RB-01045` → seleccioná `PL-STRESS-c83a2c-0` | El panel de tulas se llena y scrollea hasta ~300 (pagina de a 100) | Filter de tulas no delega |
| D2.4 | `T-STRESS-2a7d72-1-0` | Pill **"Con diferencia"** rojo en ficha y grilla; Conciliación: Declarado 32,716.49 / Recontado 33,182.20 / Diferencia **+465.71** en rojo | Espejo B5 o colDeps rotos |
| D2.5 | La misma tula → Por depósito → depósito 1 → Ver transacciones | DOS transacciones: `Deposito` + **`Sobrante`** de 465.71 | Filter de transacciones no delega |
| D2.6 | `T-STRESS-327d72-0-1` → Por depósito → transacciones | `T-0000001244` con pill **"Acreditada · fecha"** y lote **TB-1029** | Espejo BatchStatus no llegó o el Filter falla |
| D2.7 | La misma tula → Cronología | Evento 5: "Transacciones acreditadas en core bancario (lote TB-1029)" | colTxTula/crono rotos |
| D2.8 | `Email-1045` → Validaciones | Filas **Pasó** + al menos una **Falló** (con hoja/celda) + varias **Omitida** grises | B2 o el AddColumns de reglas rotos |
| D2.9 | `Email-1045` → Declaración | Mensaje "no generó declaración" y NADA de grillas fantasma | Guard !IsBlank(gvarDecl) faltante |
| D2.10 | `Email-1238` → Declaración → una bolsa vinculada → clic tula | Navega a la tula correcta con migas completas | Set de gvars en el link roto |
| D2.11 | Cualquier correo aceptado → Cheques declarados | Los relacionados muestran **"Sí · T-STRESS-…"** clicable (hay 1.065) | Espejo B3 MatchedCheck roto |

### D3 — Los techos conocidos (documentados, no bugs)

Estas colecciones están acotadas por REGISTRO y su techo es el límite de filas
de la app. Con los datos actuales nadie lo roza; si el negocio algún día lo
roza, es cambio de diseño, no sorpresa:

| Colección | Acotada a | Techo actual | Peor caso sembrado |
|---|---|---|---|
| `colDeps` | depósitos de UNA tula | 500 | 4 |
| `colChequesTula` | cheques de UNA tula | 500 | ~12 |
| `colVals` | resultados de UN run | 500 | ~28-50 |
| `colBolsas`/`colDDeps`/`colDCheques` | de UNA declaración | 500 | 60/~480/~200 (la declaración monstruo) |

⚠️ El único que se ACERCA: `colDDeps` de la declaración monstruo (~480 de 500).
Si agrandás el seed, ese es el primero que trunca.

---

## PARTE 2 — Funcionalidad

| # | Escenario | Esperado |
|---|---|---|
| F1 | Búsqueda: los 4 tipos con identificador INEXISTENTE (`Email-9999`, `RB-99999`, `X`, `T-NADA`) | Mensaje inline por tipo; NUNCA navega ni rompe |
| F2 | Búsqueda con campo vacío | "Escribí un identificador…" |
| F3 | Tracking en minúsculas (`email-1115`) | Lo encuentra igual (Dataverse no distingue mayúsculas) |
| F4 | Drill completo: `Email-1238` → bolsa → tula → depósito → transacción, y VOLVER por migas hasta Búsqueda | Cada nivel con datos correctos; migas siempre navegables; cero residuos visuales |
| F5 | El bug histórico: abrí la tula `T-STRESS-2a7d72-1-0`, volvé, buscá OTRA tula directa | La conciliación muestra los números de la NUEVA tula (nunca los de la anterior) |
| F6 | Reset de pestañas: en una tula dejá "Cheques" activo, salí, entrá a otra tula | Arranca en **Conciliación** (ídem correo → Validaciones) |
| F7 | Tula en esclusa (cualquier `T-STRESS` sin recontar) | Ficha con "—" en sesión/cajero/fecha; Conciliación y Por depósito con "Sin depósitos recontados todavía."; Cronología con hitos punteados PENDIENTES |
| F8 | Estados de pill — recorrer hasta ver TODOS | Lote: En progreso/Recibido/En boveda · Tula: los 6 · Cheque: Recontado/Enviado a digitacion/Recibido/No Recibido · Tx: Deposito/Faltante/Sobrante · Core: Acreditada/Pendiente · Validación: Pasó/Falló/Omitida |
| F9 | Correo `Email-1115` → Respuesta al cliente | Asunto + HTML renderizado legible + fecha |
| F10 | Adjunto "—" en correos stress | Correcto: el seed no crea archivos adjuntos (no es bug) |
| F11 | Planilla del lote monstruo con sucursal larga | El texto NO se corta en dos líneas mochas (Wrap false aplicado) |
| F12 | Hover sobre filas en las 3 grillas maestras (recientes, planillas, tulas) | El texto de la fila SIGUE visible al hacer hover (los z-order arreglados) |

---

## PARTE 3 — Performance

### P1 — Presupuesto de queries por pantalla (Studio → Monitor)

Abrí **App Checker → Monitor** (o Ctrl+Shift+M), navegá y contá las llamadas a
Dataverse. El presupuesto de diseño:

| Acción | Queries esperadas | Bandera roja |
|---|---|---|
| Abrir la app | 1-2 (recientes + metadata) | Queries de tablas que la pantalla no usa |
| Buscar (cualquier tipo) | 1 LookUp (+2 derivados en tula) | — |
| Entrar a scrLote | 2 (planillas + tulas si hay preselección) | Una query POR FILA de la galería |
| Entrar a scrTula | 2 (ola Concurrent) + 1 por depósito (colTxTula: acotado, 1-4) | Más de ~8 en una tula normal |
| Entrar a scrCorreo | 3 (ola 1) + 4 (ola 2) = **7 máx** | Queries secuenciales en cascada (>2 olas) |
| Entrar a scrDeposito | 1 (galTxs) — 0 si adoptaste el filtro sobre colTxTula | Retrieves extra por fila (lookups TB) — hasta 3 filas es aceptable |
| Scrollear galería grande | 1 por página (~100 filas) | 1 por fila |

**La señal de muerte**: en el Monitor, un patrón de N llamadas idénticas con
GUID distinto al renderizar una galería = lookup anidado por fila. En esta app
solo `galTxs` lo tiene permitido (excepción declarada, ≤3 filas).

### P2 — Cronómetro (límite de filas en 500, red normal)

| Benchmark | Objetivo | Inaceptable |
|---|---|---|
| Arranque en frío | < 5 s | > 10 s |
| Búsqueda → pantalla destino | < 2 s | > 4 s |
| `RB-01045` (600 planillas), primera pintada | < 3 s | > 6 s |
| Seleccionar `PL-STRESS-c83a2c-0` (300 tulas) | < 2 s | > 5 s |
| Tula con conciliación completa | < 2.5 s | > 5 s |
| `Email-1238` completo (7 queries) | < 3 s | > 6 s |
| Cambiar de pestaña (sin red) | instantáneo | cualquier spinner |

Cada benchmark: 3 veces, quedate con la mediana (la primera siempre paga
metadata). Si algo excede el objetivo, el Monitor te dice si es red (muchas
queries), payload (queries gordas: mirá los bytes) o render (pocas queries y
lento igual: exceso de controles/fórmulas repetidas).

### P3 — Panel Performance de Studio

Tras aplicar las fórmulas optimizadas (scrTula.OnVisible con Ungroup,
AllItemsCount ×3, scrCorreo en 2 olas):

- "Mutating a collection inside ForAll" → debe DESAPARECER.
- "AllItemsCount" ×3 → deben DESAPARECER.
- "Collection initialized but never updated" ×4 → **se quedan y está BIEN**
  (son cachés de solo lectura; la alternativa es peor). Documentado, no ignorado.

---

## Registro de resultados

Corré las tres partes en orden y anotá: `[ok]` / `[FALLA + captura]` por caso.
Cualquier D-falla es **bloqueante** (datos escondidos). Las F-fallas se juntan
para una pasada de fixes. Las P-fallas piden Monitor antes de tocar nada.
Cuando termines, pasame la lista: los fixes van con fórmula exacta como siempre.
