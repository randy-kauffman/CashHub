# CH-App-DepositsTracker v2 — Análisis de brechas y plan de construcción

> Rediseño completo del tracker según el prototipo aprobado
> (artifact `tracking-prototype.html`). Este documento responde: **¿se puede
> construir con los datos que tenemos?** Sí — con el paquete de backend de la
> sección 3. Todo lo demás existe hoy en DEV.

## 1. Veredicto por pantalla

| Pantalla | ¿Construible hoy? | Qué falta |
|---|---|---|
| Búsqueda | ✅ Sí (sin contadores de planillas/tulas en "recientes") | B4 para mostrar montos del lote en la lista |
| Lote | ⚠️ Parcial | B4 (totales del lote), B5 (pill "con diferencia" delegable en tulas) |
| Tula | ⚠️ Parcial | B5 (diferencia materializada), B6 (cheques por tula en 1 query) |
| Depósito | ✅ Sí | — (transacciones, tipos, lote TB, acreditación: todo existe) |
| Correo | ⚠️ Parcial | B1 (lookup Declaración→Correo), B2 (validaciones "Omitida"), B3 (cheque declarado "ya relacionado"), B7 (denormalizar declaración en depósitos/cheques declarados) |

**Regla de oro del proyecto que este plan respeta**: los filtros de la app JAMÁS
usan columnas de entidad relacionada ni agregados client-side — para eso existen
las columnas espejo (`sanic_batchstatus` en transacciones y planillas) y los
totales materializados por plugin (`BatchTotalsCalculator`). El tracker clona
esos dos precedentes, no inventa mecanismos nuevos.

## 2. Datos verificados que la app usará tal cual

- **Tracking** = `sanic_name` de `sanic_chtblreceivedemails`, autonumber `Email-{SEQNUM:1}`.
  El correo tiene: `sanic_sourceemailaddress`, `sanic_receptiondatetime`, `sanic_subject`,
  `sanic_emailreviewstatus` (Pendiente/Aceptado/Omitido/Revision manual/Error/Denegado),
  respuesta al cliente en `sanic_replysubject`, `sanic_replybodyhtml`, `sanic_replydatetime`,
  `sanic_repliedby`, `sanic_emailreplystatus` (Pendiente/Respondido/Omitido).
- **Adjuntos**: tabla `sanic_chtblemailsattachments` (`sanic_filename`,
  `sanic_fileextention`, `sanic_tamanoenbytes`, File `sanic_filecontent`), lookup `sanic_email`.
- **Validaciones**: `sanic_chtblvalidationruns` (lookup real `sanic_receivedemail`,
  `sanic_runresult` Pass/Fail, `sanic_runstart/runend`) → `sanic_chtblvalidationresults`
  (lookups a run y a regla; `sanic_validationresult` Pass/Fail; `sanic_detail`,
  `sanic_sheet`, `sanic_cellref`, `sanic_rownumber`). Catálogo: `sanic_validationrules`
  (28 reglas sembradas, clave `sanic_validationname` = VR-xx, nombre legible en
  `sanic_name`, orden `sanic_validationorder`).
- **Declaración**: `sanic_chtbldeclaration` (`Decla-{SEQNUM:4}`) → `sanic_chtbldeclaredbags`
  (`sanic_bagnumber`, totales NIO/USD/EUR, **`sanic_bagid`** lookup a la tula física,
  `sanic_isrelatedtobag`) → `sanic_chtbldeclareddeposits` (`sanic_depositreference`,
  montos, `sanic_relatedrecounteddeposit` = "ya recontado") → `sanic_chtbldeclaredchecks`.
- **Cadena física**: `sanic_chtblreceptionbatch` (`RB-{SEQNUM:5}`, `sanic_transportadora`
  texto, `sanic_receptionstatus`: En progreso/Recibido/Anulado/En boveda) →
  `sanic_chtblreceptions` (planilla: `sanic_receiptid`, `sanic_remitentname`,
  declarados `sanic_niocash/niocheck/usdcash/usdcheck`, espejo `sanic_batchstatus`) →
  `sanic_chtblbags` (tula: `sanic_bagid`, `sanic_bagstatus` 6 estados
  ["Recibida en esclusa", "Almacenada en boveda", "Asignada a cajero", "Recontada",
  "Pendiente de revision por supervisor - recuento", "… - esclusa"], recontados
  `sanic_totalnio/usd/eur`, lookups `sanic_receptionid`, `sanic_recountingsession`,
  `sanic_bagdeclarationid`).
- **Recuento**: `sanic_chtblrecountingsession` (`RCS-{SEQNUM:4}`, `sanic_cashier`
  → systemuser, `sanic_confirmed`, `sanic_sessionforciblyclosed`);
  `sanic_chtbldepositsrecounting` (`DR-{SEQNUM:8}`, montos — ojo typo real
  **`sanic_otherlocacheckamount`** —, `sanic_bagid`, `sanic_relateddeclareddeposit`);
  `sanic_chtblchecks` (`CK-{SEQNUM:8}`, `sanic_checkstatus`: Recontado/Digitado/
  Rebotado/Enviado a digitacion/Recibido/No Recibido; `sanic_checktypes`: BAC Local/
  Otros Local; sellos `sanic_senttodigitizationon/receivedon/notreceivedon`;
  `sanic_relatedrecounterdepositid`, `sanic_relateddeclaredcheck`).
- **Transacciones**: `sanic_chtbltransactions` (`T-{SEQNUM:10}`,
  `sanic_transactiontype`: Deposito 159460000 / Faltante 159460001 / Sobrante
  159460002 / Sin declaracion 159460003; `sanic_transactionprocessingtype`
  Manual/Batch; `sanic_transactionstatus` con único valor Aplicada — pendiente = en blanco;
  `sanic_transactionreference` nvarchar(31); espejo `sanic_batchstatus`;
  lookups `sanic_relateddepositrecounting`, `sanic_transactionbatch`) y
  `sanic_transactionbatches` (`TB-{SEQNUM:4}`, `sanic_batchstatus`:
  Generado/Ingresado/Aprobado(dead-end)/Aplicado; `sanic_cajero`, `sanic_generadopor`,
  `sanic_ingresadopor/on`, `sanic_aplicadopor/on`; totales efectivo NIO/USD/EUR).

Ajustes que el prototipo debe absorber (el modelo manda, no el mock):

1. Estados del lote: **En progreso / Recibido / Anulado / En boveda** (no "En proceso/Completado").
2. Estados de tula: los **6 reales** de arriba (no Recibida/En recuento/Recontada).
3. Estados de cheque: los **6 reales** (no solo Digitado/Recibido/No recibido).
4. "Acreditada en core" = espejo `sanic_batchstatus` de la transacción = `159460003`
   (Aplicado) + fecha `sanic_aplicadoon` del lote TB. `sanic_transactionstatus`
   en blanco = pendiente.
5. **Conteos de tulas: se pierden en TODA la app** (lista de recientes, grilla
   de planillas y ficha del lote). `CountRows` sobre relaciones no delega y no
   hay contador materializado. Decisión consciente: los totales B4 en dinero
   cuentan la historia; si más adelante querés el conteo, se agrega un B4-bis
   (contador por plugin, mismo patrón).
6. **Sin modo oscuro**: las canvas apps no siguen el tema del sistema operativo.
   La app se construye con la paleta clara del prototipo; el toggle claro/oscuro
   era una comodidad del mockup HTML, no una feature de la app real.
7. **Pills sin el punto de color del mockup y cards sin sombra** (limitaciones
   del control clásico, aceptadas).
8. **Mejoras deliberadas sobre el mock**: columna TOTAL € propia en bolsas
   declaradas, estado de respuesta enriquecido (Enviada/Omitida/Pendiente),
   conteo de adjuntos (+N).

## 3. Paquete de backend (lo hago yo — orden de ejecución)

| # | Cambio | Tipo | Detalle |
|---|---|---|---|
| B1 | `sanic_ReceivedEmail` lookup en `sanic_chtbldeclaration` | Columna + plugin + backfill | Clona el patrón de `ValidationRuns.sanic_receivedemail` (documentado en Schema.cs). `DeclarationMaterializer` lo estampa al crear; script de backfill parsea `sanic_sourcerecordid` existente. Mata la trampa #1 de delegación (GUID en texto). |
| B2 | Valor `159460002 Omitida` en `sanic_validationresult` + persistir Skipped | Option set + cambio en `PersistRun()` | Hoy `PersistRun` hace `continue` en los Skipped (ReceivedEmailValidationService.cs). Se quita el `continue` y se estampa Omitida. La pantalla Correo muestra el cuadro completo de las 28 reglas. |
| B3 | `sanic_matchedcheck` (lookup → `sanic_chtblchecks`) en `sanic_chtbldeclaredchecks` — display **`MatchedCheck`** | Columna espejo + plugin | Estampada cuando un cheque físico setea/limpia `sanic_relateddeclaredcheck` (registro en Create/Update de `sanic_chtblchecks`). Es LOOKUP y no bit para que la pantalla Correo pueda navegar del cheque declarado a su tula (`MatchedCheck.Bag.BagID`, combina con B6). Backfill inicial. |
| B4 | Totales declarados en `sanic_chtblreceptionbatch`: `sanic_decniocash`, `sanic_decniocheck`, `sanic_decusdcash`, `sanic_decusdcheck`, `sanic_decniototal`, `sanic_decusdtotal` — displays EXACTOS: **`DecNIOCash`, `DecNIOCheck`, `DecNIOTotal`, `DecUSDCash`, `DecUSDCheck`, `DecUSDTotal`** (moneda en mayúsculas, convención del esquema) | Columnas + plugin | Clona `BatchTotalsCalculator`: FetchXML aggregate sobre `sanic_chtblreceptions` agrupando por lote, recalcula en Create/Update/Delete de planilla (filtrado por columnas de montos + lookup al lote). Descripción de columna: "Mantenido por plugin. No editar a mano." |
| B5 | Conciliación materializada en `sanic_chtblbags`: `sanic_decltotalnio/usd/eur` (displays **`DeclTotalNIO`/`DeclTotalUSD`/`DeclTotalEUR`**) + `sanic_hasdifference` (bit, display **`HasDifference`**) | Columnas espejo + plugin | Plugin en Update de Bags (filtro: `sanic_bagdeclarationid`, `sanic_totalnio/usd/eur`, `sanic_bagstatus`): copia declarados y recalcula `hasdifference = (totalnio≠decltotalnio) OR (usd) OR (eur)` cuando la tula llega a Recontada. La grilla de tulas filtra/pinta por bit delegable. |
| B6 | `sanic_bag` lookup en `sanic_chtblchecks` — display **`Bag`** | Columna + plugin | Estampada en **Create Y Update** del cheque (filtrado por `sanic_relatedrecounterdepositid` — si el lookup al depósito se corrige después, la tula se re-estampa; misma lógica que B3). Derivada de `sanic_relatedrecounterdepositid.sanic_bagid`. La pantalla Tula trae TODOS los cheques en UNA query delegable. Backfill. |
| B7 | `sanic_declaration` lookup en `sanic_chtbldeclareddeposits` y `sanic_chtbldeclaredchecks` — display **`Declaration`** | Columnas + materializer + backfill | La pantalla Correo llena depósitos y cheques declarados con UNA query por tabla, sin ForAll anidados. |
| B8 | Rol NUEVO `CH-SR-DepositsTracker` (read-only, dedicado al tracker) | Seguridad | Ver sección 4. **Decisión del usuario (2026-08-12): `CH-SR-AnalyticsRead` NO era para esta app — se deja intacto.** |

Todo con STRICT TDD (dominio puro + adapter), solution `CashHubCore`, bump de
versión, y los espejos siempre con descripción "No editar a mano" — igual que
`TransactionBatchStatusMirrorPlugin` / `ReceptionBatchStatusMirrorPlugin`.

**Secuencia**: B1–B7 + backfills → regenerar datasources de la canvas
(`scripts/generate-canvas-datasource.py`, ojo grafía vieja "exclusa" cacheada) →
B8 + grupo AAD + team → construcción de UI (guías 01–06) → revisión.

## 4. Seguridad y roles

**Decisión del usuario (2026-08-12)**: `CH-SR-AnalyticsRead` NO está pensado
para esta aplicación — **se deja exactamente como está**. Para el tracker se
crea un rol dedicado nuevo: **`CH-SR-DepositsTracker`**.

Diseño (B8), siguiendo el modelo de seguridad del proyecto (sharing por grupo
AAD + roles vía group team + guards server-side; sin gate AAD en OnStart):

- **Rol NUEVO** `CH-SR-DepositsTracker`: `prvRead` con
  profundidad **Global** sobre: ReceptionBatch, Receptions, Bags,
  RecountingSession, RecountingSessionDetail, DepositsRecounting, Checks,
  Transactions, TransactionBatches, Declaration, DeclaredBags, DeclaredDeposits,
  DeclaredChecks, ReceivedEmails, EmailsAttachments, ValidationRuns,
  ValidationResults, ValidationRules, Client, Parameter, **Locations** (la ficha
  del lote muestra la ubicación) y AppCatalog/AppCatalogAccess (si el tracker se
  lanza desde el hub — hoy el rol NO los tiene, hay que agregarlos también).
  **Ningún privilegio de escritura: la app es un visor.** Trazabilidad
  transversal ⇒ profundidad Org (a diferencia de CH-SR-Counter, que quedó BU).
- **Decisión consciente a validar con vos**: el rol lee `ReceivedEmails`
  completo, lo que incluye `sanic_replybodyhtml` y `sanic_triagenotes`. Si el
  equipo de tracking NO debe ver notas de triage, se excluye la columna vía
  perfil de seguridad de columna (column security profile) — decímelo y lo
  agrego al paquete.
- **Grupo AAD** nuevo `CashHub - Tracker` → **group team** (teamtype 2) con el
  rol asignado → **compartir la app** con el grupo. Mismo patrón que
  "CashHub - Digitizer". El GUID del grupo se registra en `CH-Tbl-Parameter`.
- La app **no** hace chequeo de grupo en OnStart (decisión vigente del
  proyecto): sharing controla el lanzamiento, roles controlan los datos.

## 5. Performance — decisiones de diseño

1. **Cero agregados client-side sobre datasources**: todos los totales vienen
   materializados (B4, B5, totales existentes de TB y DeclaredBags).
2. **Cero filtros por columna de entidad relacionada**: espejos (existentes +
   B5/B6/B7) o lookup directo comparando GUID:
   `Filter(tabla, Lookup.'PK tabla' = gvar.'PK tabla')` — patrón delegable
   verificado del proyecto.
3. **Colecciones solo para datos acotados por registro** (depósitos de UNA tula,
   validaciones de UN run): `ClearCollect` + `Concurrent()` en OnVisible.
   Nada de coleccionar tablas enteras (el AppChecker ya marcó eso en la v1).
4. **Búsquedas por identificador** = `LookUp` sobre columnas autonumber/indexadas
   con igualdad simple (delegable, insensible a mayúsculas en Dataverse). Sin
   `Upper()`/`Lower()` sobre columnas (no delega).
5. **Navegación por registro** (`gvar.Lookup.Campo`) permitida: es 1 registro,
   no un filtro. Las galerías NO navegan lookups de lookups en columnas
   calientes; lo que la galería necesita viene en la fila o en la colección.
6. **`DelayOutput: true`** en inputs de búsqueda; **spinner overlay** global
   reutilizado; imágenes/íconos SVG inline (sin media externa).
   *(Descartado en la construcción: sin `OnChange` no hay `DelayOutput` que
   valga, y sin cargas largas no hay spinner — ver 02 §3.1.)*
7. App.OnError global con `Trace()` + `Notify()` — misma convención que
   Digitization.

## 6. Ícono

`docs/marca/icono-deposits-tracker-{32,48,96,192,512}.png` (convención de
tamaños del repo): fondo rojo BAC `#C8102E`, ruta de tracking punteada con
nodos y lupa con check. En Studio: Configuración → General → ícono de la
aplicación, subí el **512** con color de fondo `#C8102E`.

## 7. Estructura de las guías

| Archivo | Contenido |
|---|---|
| `01-app.md` | Creación de la app, datasources, App.Formulas/OnStart/OnError, convenciones de nombres, tokens de color/tipografía |
| `02-pantalla-busqueda.md` | scrBusqueda |
| `03-pantalla-lote.md` | scrLote |
| `04-pantalla-tula.md` | scrTula |
| `05-pantalla-deposito.md` | scrDeposito |
| `06-pantalla-correo.md` | scrCorreo |

Cada guía de pantalla: árbol completo de controles → tabla de propiedades por
control (solo propiedades que cambian del default) → fórmulas exactas listas
para pegar. Los nombres de columna en fórmulas usan los **display names** que
Studio muestra con el conector Dataverse (tal como los usa hoy la app vieja);
si Studio te ofrece otro nombre en el IntelliSense, ese manda — reportámelo y
ajusto la guía.
