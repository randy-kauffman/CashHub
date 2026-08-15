# Guía 00 — Análisis, plan y ledger de divergencias (CH-App-Supervisor v2)

Documento de acuerdo previo. Fija QUÉ se reconstruye, qué backend hace falta
antes de tocar la UI, qué se declara distinto del mockup y en qué orden se
construye. Las guías 01–05 asumen todo lo de acá.

**Fuente de verdad del diseño**: prototipo HTML aprobado por el usuario
(2026-08-14), 4 pantallas + nav clásica, controles clásicos, sin código.
**Fuente de verdad del modelo**: `solutions/unpacked/core/Entities/*/Entity.xml`
y las apps que ya funcionan. Cuando el mockup y el modelo chocan, **manda el
modelo** y la divergencia se declara en el ledger (§4).

---

## 1. Alcance de la reconstrucción

Se rehacen las 4 pantallas y se elimina TODO control de código/dataset,
reemplazándolo por controles clásicos nativos (decisión del usuario, mismo
enfoque que el rebuild del DepositsTracker).

| # | Pantalla v2 | Reemplaza a | Guía |
|---|---|---|---|
| 1 | `scrResumen` — Resumen de operación | Screen OperationSummary | `02` |
| 2 | `scrAprobaciones` — Aprobaciones | Screen Approvals | `03` |
| 3 | `scrTulas` — Localización de tulas | Screen BagLocation | `04` |
| 4 | `scrCheques` — Cheques | Screen ChecksSummary | `05` |

**Controles de código que MUEREN** (verificado en `ControlTemplates.json`):
`DVFluentDetailsList.pcfdataset` (PowerCAT/Creator Kit — el único de terceros),
`Table.pcfdataset` (OneGrid moderno), `Combobox.pcfdataset`, `Dropdown.pcfdataset`,
`ButtonCanvas`, `Toggle` moderno, `TabList` moderno, `Spinner` moderno,
`Form` + `typedDataCard fluentV9`. **Excepción**: el Selector de fecha moderno
(`DatePickerCanvas`) SE CONSERVA — regla de preferencia de 01 §0.3 (el clásico
tiene UX pobre de año/navegación). El `ExpandMenu` (componente canvas con 2.254
iconos SVG = 4,4 MB) se reemplaza por una nav clásica liviana (`cmpNav`).

---

## 2. Prerrequisitos de BACKEND (antes de las guías de pantalla)

Las guías marcan cada fórmula que depende de una columna/plugin nuevo con su
**fallback "sin backend"**, para que la UI se pueda construir en paralelo. Pero
sin estos ítems la app queda funcional pero no delegable / no atómica.

| ID | Qué | Por qué | Estado |
|---|---|---|---|
| B-1 | Re-exportar **CashHubCore**: `sanic_bag` (lookup a Bags) ya existe en el ENTORNO pero falta en el `Entity.xml` del repo | El repo miente por omisión; el fix delegable de cheques lo consume | Columna viva ✅ — falta sync de repo |
| B-2 | Columna espejo **`sanic_receptionbatchid`** (lookup a ReceptionBatch) en `sanic_chtblbags`, estampada por plugin desde `sanic_receptionid.sanic_receptionbatch` | Dup-check de tulas y búsquedas hoy usan dot-walk de 2 saltos → NO delegan (truncan a 500). Patrón ya usado: `ReceptionBatchStatusMirrorPlugin` | Nuevo |
| B-3 | Columna espejo **`sanic_receiptidmirror`** (texto) en `sanic_chtblbags` desde `sanic_receptionid.sanic_receiptid` | Búsqueda por recibo en scrTulas hoy es `StartsWith(ReceptionID.ReceiptID,…)` no delegable | Nuevo |
| B-4 | **Custom API `sanic_CHResolveApprovalRequest`** (plugin Pre-Op) que resuelve la solicitud + efectos (transacciones, tula, sesión) en UNA transacción | Cierra atomicidad, concurrencia (doble aprobación), auto-aprobación por API y N+1 de red. Patrón montado en `GuardChangeRequestApprovalPlugin` | Nuevo — recomendado |
| B-5 | **`GuardSupervisorAccountingPlugin`** Pre-validation(10) sobre Create de `sanic_chtbltransactions` + alternate key `(sanic_relateddepositrecounting, sanic_transactiontype)` | Bloquea fabricación/duplicación de asientos Sobrante/Faltante por Web API | Nuevo |
| B-6 | Espejo **`sanic_bagnumber`** (texto) en `sanic_chtblapprovalrequest`, estampado por plugin desde el JSON de RequestResources al crear | Habilita búsqueda delegable por nº de TULA en la bandeja (hoy el nº vive dentro del JSON — L-19) | Nuevo — opcional |
| B-7 | Productores (Reception `Editar tula`, Recounting `Primera tula en planilla`) deben incluir **`BagPreviousState`** en RequestResources | Sin él, el Rechazar de esos tipos no puede revertir el estado con certeza | Cambio en apps productoras |
| B-8 | **Field Security Profile** sobre `sanic_accountnumber` para el rol supervisor (la columna tiene `IsSecured=1` en las 3 tablas) | Sin FSP devuelve **Blank SIN error** → asientos con cuenta vacía en silencio. **BLOQUEANTE de publicación de la 03** | Config — verificar en entorno |
| B-9 | Espejo **`sanic_recountingsession`** (lookup a RecountingSession) en `sanic_chtblchecks`, estampado por `CheckMirrorStampPlugin` desde `sanic_bag.sanic_recountingsession` (+ backfill Web API) | Los conteos de cheques de scrResumen deben ser **CountRows DELEGADOS al servidor** — materializar filas en colección topa en el límite del cliente (500/2000) sin importar cuánto se suba; con el espejo, conteo por sesión y total del día son exactos a cualquier volumen. QA del usuario con 2.074 cheques lo probó | Nuevo — descubierto en QA fase 1 |

> **Sin B-4**: la guía `03` documenta el flujo client-side con `IfError` +
> revalidación de estado como fallback aceptable de transición. Con B-4, el
> canvas solo llama al Custom API. Ambos caminos están escritos.
> **Consumo de B-4 desde canvas**: requiere agregar el datasource **Environment**
> del conector Dataverse (patrón NUEVO en este codebase — hoy no hay ninguna llamada
> a Custom API desde canvas) o consumirlo vía flujo. Se define en `01` §2 y `03`.

---

## 3. Prerrequisitos de SEGURIDAD (config de roles — sin código)

Estos NO son de UI pero son bloqueantes de gobierno. Se aplican por script/portal,
no en las guías de pantalla. Orden por impacto:

1. **`CH-SR-Counter.xml`**: borrar `prvCreatesanic_CHTblApprovalRequest` (L50) y
   `prvWritesanic_CHTblApprovalRequest` (L140); bajar `prvRead` (L97) a Local.
   Cierra la auto/co-aprobación por Web API **hoy, sin código**.
2. **`CH-SR-Counter.xml`**: bajar `Bags` (W/R L141/98), `RecountingSession` y
   `CountedMoneyDetails` de Global a Local; quitar `prvDelete` de esas tres.
3. **`CH-SR-CountingSupervisor.xml`**: AGREGAR los privilegios que la app usa y el
   rol NO tiene (verificado, 115 privilegios) — hoy ramas enteras fallan mudas:
   `prvRead sanic_CHTblClient` (**REQUERIDO** — la guía 03 usa `RemitentClient.Name`
   de la tabla Client como camino primario, con `RemitentName` solo de fallback; sin
   el privilegio la 03 degrada a texto plano. 04/05 sí usan solo `RemitentName`),
   `prvWrite sanic_CHTblDeclaredBags`, `prvWrite/prvCreate
   sanic_CHTblDepositsRecounting`, `prvDelete sanic_CHTblBags`, `prvAppend/AppendTo`
   varios, y **C/R/W sanic_CHTblRecountingSessionDetail** (el rol tiene CERO sobre esa
   tabla). Esta lista es la misma que la app patchea en §7 — reconciliadas.
4. Quitar `prvReadPluginTraceLog` Global — está en **7 roles**, no 2: `CH-Role.xml:49`,
   `CH-SR-Counter.xml:89`, **`CH-SR-CountingSupervisor.xml:56`** (¡el de esta app!),
   `CH-SR-OpsAdmin`, `CH-SR-AnalyticsRead`, `CH-SR-ClientAdminContributor`,
   `CH-SR-ClientAdminApprover`. Dejarlo solo en un rol de soporte explícito.
5. Auditoría por columna (verificado — hoy está al revés): HABILITAR
   `IsAuditEnabled=1` en `sanic_CHTblTransactions.sanic_BACLocalCheckAmount` y
   `.sanic_Total` (hoy NO auditadas — y es la tabla que B-5 blinda) y en
   `sanic_CHTblApprovalRequest.sanic_SolvedBy` (hoy NO auditada — es el dato central
   del cuatro ojos). **No existe `TotalAmount`** en las tablas de esta app: en
   Transactions/DepositsRecounting la columna es `sanic_Total`. Confirmar además
   `organization.isauditenabled` en el entorno (con org-level OFF no se graba nada).
6. Diff de roles ENTORNO vs repo: la app patchea `DeclaredBags` y
   `RecountingSessionDetails` sin que ningún rol del repo lo autorice → hay roles
   asignados fuera de control de fuentes.
7. **Gobierno de "Crear sesion de recuento"** (hallazgo de la guía 03): la excepción
   del guard que deja NACER aprobada esa solicitud (H-08 de la auditoría) tiene dos
   productores, y si el cajero elige su PROPIA mesa la solicitud nace auto-aprobada
   por el propio cajero — colapso del cuatro ojos en ese tipo. Endurecer en el
   guard/B-4: la excepción solo aplica si la sesión referenciada pertenece al
   solicitante Y el tipo lo autoriza server-side (no por texto libre).

---

## 4. LEDGER de divergencias declaradas (mockup vs modelo / plataforma)

Toda diferencia entre el prototipo HTML y lo que se construye, explícita:

| # | En el mockup | En la app real | Motivo |
|---|---|---|---|
| L-1 | Cards con sombra y esquinas redondeadas | Card = **Contenedor de diseño manual** con las 4 `Radius` **y `DropShadow.Light`** (la sombra SÍ se recupera) | El `Rectangle` clásico NO expone Radius y el Botón no admite hijos (verificado en `pkgs/*.xml`); el Contenedor manual expone Radius + DropShadow + hijos con X/Y |
| L-2 | Chips con punto de color (`::before`) | Píldora = Botón clásico modo Ver, sin punto | Un círculo extra por pill no paga (receta 01 §7.2) |
| L-3 | KPI "Tulas con diferencia" como número exacto | Se deriva de `CountRows(Filter(...))` sobre depósitos con diferencia | No hay columna materializada; se calcula sobre la colección ya cargada |
| L-4 | Estados de tula en tabs fijos | Segmentado con **"Todas" + columna Estado** | Blinda contra nuevos valores del optionset `sanic_bagstatus` (hoy 6) |
| L-5 | Sellos de tiempo "Env. 09:12" | `Text(ThisItem.'Enviado el', FmtFecha)` con `HH` (24 h) — canvas resuelve por DISPLAY name (`sanic_SentToDigitizationOn` = "Enviado el"), no por lógico | `hh` sería reloj de 12 h sin AM/PM |
| L-6 | Diferencia Sobrante/Faltante mostrada por componente | Se calcula por componente (efectivo/BAC/otros) — NO por neto | Es el fix del bug del `Abs` sobre el neto; se muestra tal cual se contabiliza |
| L-7 | Símbolos `C$ / US$ / €` juntos | `SimboloNIO/USD/EUR` de App.Formulas; montos con `FmtMoney = "#,##0.00"` | El `#,###` del original mostraba `C$ .00` en cero |
| L-8 | Hover-underline de links | Subrayado FIJO en labels clásicos | Los labels clásicos no tienen hover |
| L-9 | Chip de sesión tri-estado ("Faltante ×N" ámbar / "Sobrante ×N" teal / "Cuadrado") | Chip BI-estado: "Diferencias ×N" (ámbar) / "Cuadrado" (neutro) | El estado del detalle de sesión no lleva dirección; el signo vive en Transactions vía depósito→tula (cadena que scrResumen no carga — reintroducir el join sería otro N+1). El teal ThemePos se usa en scrCheques |
| L-10 | Toggle de tema + paleta oscura completa | App de UN solo tema (claro) | Canvas no tiene theming por variable sin duplicar cada Fill; fuera de alcance. El toggle del mockup no se construye |
| L-11 | scrCheques: solo punto de sync en filtros; filtro Estado nace en "Pendientes de enviar" | Botón "↻ Actualizar" agregado (único camino de carga); default de Estado = "Todos" | Sin botón no hay recarga explícita; el mockup listaba los 6 estados con el filtro diciendo otra cosa |
| L-12 | scrCheques: chips `.chip` y `.chip.blue` con el MISMO color; solo 4 estados visibles | "Recibido" en verde sólido distinto de "Recontado"; chips para los 6 valores del option set (incl. Digitado/Rebotado) | Recontado y Recibido eran indistinguibles; el modelo manda (6 estados reales) |
| L-13 | scrCheques: orden de grilla implícito; sellos "09:12" sin día | Orden por accionabilidad (Recontado → No recibido → Enviado → Recibido → reservados); sellos `dd/mm HH:mm` | Con 30 días de arrastre, la hora sin día es ambigua |
| L-14 | scrCheques: checkbox cuadrado; toggle solo desde la casilla; reenvío directo | Icono CheckBadge/Blocked (distingue por color, no por forma); clic en toda la fila selecciona; "Reenviar" con contador + confirmación modal | La casilla clásica mantiene estado interno y se desincroniza de la colección de selección; escritura masiva ciega necesita confirmación |
| L-15 | scrCheques: 4 KPIs (incl. €) | 3 KPIs + "No recibidos" (sin KPI de €) | Volumen de cheques en € marginal; el panel "Cheques del día y pendientes" renombrado para no mentir el alcance |
| L-16 | Notas de diseño `.cap` al pie de cada pantalla | No se construyen | Son anotaciones del prototipo para el aprobador; su contenido se tradujo a requisitos verificables en los checklists |
| L-17 | Chips y contadores sin borde | Píldora neutra CON borde (receta 01 §7.2), excepto contadores de panel-head (`BorderThickness 0` explícito) | Sobre card blanca, `ThemeSunk` sin borde desaparece |
| L-18 | scrTulas: placeholder "Número de tula o de recibo…" | Sin el espejo B-3: "Número de tula…" | No se promete lo que la consulta no puede entregar; con B-3 desplegado se restituye el placeholder completo |
| L-19 | scrAprobaciones: fila de bandeja con tula/mesa; búsqueda por nº de tula | Meta de fila = `Name · Created By`; búsqueda por nº de SOLICITUD; botón Actualizar agregado | Sin espejo (B-6) el nº de tula vive dentro del JSON de RequestResources — no delegable; el nº de solicitud sí |
| L-20 | scrAprobaciones: Aprobar/Rechazar directos | Diálogo de confirmación con MOTIVO OBLIGATORIO → se persiste en `StatusReason` | Trazabilidad de la decisión; el mockup no lo pedía pero el circuito contable sí |
| L-21 | (app vieja) detalle renderizando HTML de RequestDetails | El detalle se arma desde RequestResources + lookups; el HTML no se renderiza | Mueren ~60 líneas de Split/Substitute frágiles; el dato manda sobre el markup |
| L-22 | scrAprobaciones: tabla de diferencia con 3 filas fijas | Filas por componente × MONEDA, columnas Sob/Fal separadas, línea de resumen por tipo, columna Diferencia coloreada | La unidad del preview = la unidad del motor de asientos (depósito × componente); sin eso dos depósitos opuestos mostraban "Cuadra" mientras se generaban asientos |
| L-23 | — | Centinela `ctxCargadoIdApr`: si la solicitud cambió tras cargar el detalle → aviso + firma BLOQUEADA + botón recargar; píldora de estado solo en Histórico; sello de tiempo oculto <1100 px; master-detail con proporción de 01 §8.6 (detalle `LayoutMinWidth 550`, breakpoint 1212) | Bloquear es más seguro que auto-recargar; el Timer no está en el catálogo de controles |

---

## 5. Correcciones de PLATAFORMA que las guías respetan (no negociable)

- **Redondeo**: cards y pills con `Radius` en **Botón clásico** o **Container**,
  jamás en `Rectangle` (§L-1). Verificado.
- **Responsive horizontal** (tablet landscape → desktop; sin verticales ni
  celulares): App con **Escalar para ajustar OFF** y Bloquear relación de
  aspecto **OFF**; estructura en **containers de auto-layout** con `FillPortions`
  **+ `LayoutMinWidth`** (sin él `FillPortions` solo encoge y nunca envuelve) y
  `LayoutWrap`; anchos de columna como fracción del `Width` de su galería; el
  breakpoint se referencia EN VIVO (`<scr>.Width >= 1100`), nunca congelado en una
  variable. Detalle en `01` §8.
- **Delegación**: `Filter(t, Lookup.'PK' = gvar.'PK')`; nunca `Upper()`,
  entidad-relacionada ni `Sum` sobre datasource; espejos (B-2/B-3) y totales
  materializados server-side. Cada guía clasifica sus fórmulas.
- **Fecha "hasta"**: SIEMPRE `< DateAdd(fecha, 1, TimeUnit.Days)`, nunca
  `<= fecha` (bug BL-02, excluía el día elegido).
- **Galerías clásicas auto-seleccionan la 1ª fila**: los paneles "del
  seleccionado" se diseñan asumiéndolo; estados vacíos solo para colección vacía.
- **Reset de estado** al final de cada `OnVisible` que cambia de registro (el
  estado de galerías y del segmentado persiste entre visitas): `Reset(gal…)` y
  `UpdateContext({ctxSeg…: "<default>"})`. (No hay Tab list — el segmentado es una
  fila de Botones clásicos con variable de contexto, receta 01 §7.7.)

### 5.1 Trampas de esquema declaradas (para las fórmulas de las guías)

- **Solicitante de una aprobación = `'Created By'`** (columna de sistema).
  `sanic_CHTblApprovalRequest` NO tiene columna propia de solicitante — el cuatro
  ojos compara `'Created By'` contra `UsuarioActual`.
- **`sanic_OtherLocaCheckAmount`** (falta la "l", display `"OtherLocaCheckAmount"`)
  en `sanic_CHTblDepositsRecounting`, PERO `sanic_OtherLocalCheckAmount` (bien
  escrito) en `sanic_CHTblTransactions` y `DeclaredDeposits`. Las fórmulas del cálculo
  Sobrante/Faltante (L-6) tienen que usar cada uno con su ortografía real.
- **`sanic_batchstatus` en Receptions es `int`**, no picklist — se compara contra un
  entero, no contra un miembro de optionset.

---

## 6. Orden de construcción y protocolo de revisión

**Construcción:**
1. `01-app.md` — app, datasources, App.Formulas/OnError, estilos ESTILO-*,
   nav clásica, recetas (card-botón, pill, grilla, breakpoint responsive).
2. Crear las 4 pantallas VACÍAS de una (para que los `Navigate()` compilen).
3. Llenar en orden: `02` scrResumen → `03` scrAprobaciones → `04` scrTulas →
   `05` scrCheques.

**Revisión (por cada guía, incluida 01) — flujo fijado por el usuario:**
1. Crear la guía.
2. **Revisor OPUS R1 — FIDELIDAD**: contra el prototipo HTML aprobado, función por
   función; ninguna desviación del diseño aprobado sin entrada en el ledger (§4).
3. **Revisor OPUS R2 — ANTAGONISTA**: formato, completitud y coherencia contra el
   contrato de `01 §0` (árbol↔cuerpo 1:1, tipos explícitos, nombres únicos,
   contrato de entrada + checklist de salida).
4. Los hallazgos de cada revisor se APLICAN antes de pasar al siguiente/cerrar.
- Al final del build humano: `pac canvas unpack` del msapp publicado y comparación
  código vs guías.

---

## 7. Riesgos abiertos (a confirmar en el entorno, no bloquean el diseño)

- **B-1**: `sanic_bag` viva en el entorno pero ausente del `Entity.xml` del repo
  → re-exportar CashHubCore para que el repo vuelva a ser fuente de verdad.
- **Roles fuera de fuentes** (§3.6): la app patchea `DeclaredBags` y
  `RecountingSessionDetails` sin que ningún rol del repo lo autorice → hay
  asignaciones de rol en el entorno que no están versionadas. Diff obligatorio.
- **`organization.isauditenabled`**: con auditoría a nivel entidad ON pero
  org-level OFF, no se graba nada. Confirmar por Web API.
