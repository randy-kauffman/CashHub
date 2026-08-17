# Guía 04 — scrConteo (Recuento de una tula)

El **corazón** de CH-App-Recounting v2: acá el cajero cuenta el dinero de una tula,
depósito por depósito, y la cierra. Reemplaza el 70 % de la `Screen1` monolítica de la v1
(la de los 4 forms ocultos, los 8 diálogos PowerCAT, los ~60 `Label6_*` sueltos, el mismo
`OnSelect` de 120 líneas copiado en 6-7 controles por fila, `colDenominationTotals` que
nunca se limpiaba y el `Patch` que borraba el lookup padre de un cheque declarado).

Acá: **todo se edita en colecciones locales** (`colDetalleConteoCon`, `colChecksCon`), los
totales salen **siempre** de esas colecciones (jamás de `Gallery.AllItems`), el guardado es
**`Patch` explícito con `IfError`** (cero forms), **toda acción destructiva confirma antes
de ejecutar** y **la lectura de la contadora se reinicia en cada depósito**.

> **Todo lo que esta guía no define, lo define `01`.** Paleta, formatos, defaults
> tipográficos, alias `ESTILO-*`, catálogo cerrado de tipos, componentes `§8`, recetas
> `7.1..7.11` y reglas de delegación `D-1..D-12` viven allá y **mandan**. Acá solo hay:
> nombres finales, coordenadas, y las fórmulas propias de esta pantalla.

> **Los tres componentes de `01` §8 tienen que existir ANTES** de armar este árbol (regla
> `01` §0.12): `cmpMatrizMoneda` (4 instancias), `cmpDialogo` (3 instancias). `cmpKpiSesion`
> no se usa acá.

---

## CONTRATO DE ENTRADA

| Qué | Quién lo setea | Obligatorio |
|---|---|---|
| App con los **15 datasources + el flujo** de `01` §2.1, `App.Formulas`, `App.OnStart` corrido, `App.OnError`, `BackEnabled = false`, `ConfirmExit = gvarDepositoSinGuardar` | Guía `01` §1–§3 | **Sí** — sin `App.OnStart` corrido las **23 colecciones** de `01` §4.2 no existen y media pantalla queda en rojo en tiempo de diseño |
| `gvarIdentidadOk` = `true` | `App.OnStart` (`01` §3.2) | **Sí** — es el primer término de TODO `DisplayMode` que escribe Dataverse |
| `gvarUser` | `App.OnStart` | **Sí** — `'Full Name'` del HTML de approval y `'Created By'.User` de la recuperación del polling |
| `gvarSesion` · `gvarMesa` | `02` (`01` §4.5) | **Sí** — `gvarMesa.MachineSerialNumber` es el `Serial_Maquina` del flujo de la contadora; `gvarSesion.'Created On'` es la fecha de sesión del HTML |
| `gvarTula` — registro de `CH-Tbl-Bags` **fresco**, devuelto por el `Patch` de apertura o releído por PK en el retome | `03` (§6.3 / §6.4) | **Sí** — sin él la pantalla no tiene sujeto. El `OnVisible` lo verifica y vuelve a `scrTulas`. 🧨 **No siempre llega en `Asignada a cajero`**: por la ruta *retome* (`03` §6.4) puede llegar en `Recontada` o `Pendiente de revision por supervisor - recuento`, y esta pantalla lo abre **en SOLO LECTURA** (§0.2). Es un estado de entrada **válido**, no un error |
| `gvarSesionDetalle` — registro de `CH-Tbl-RecountingSessionDetails`, **SIEMPRE presente** | `03` §7 (`btnEntrarConteoTul`), en las **cuatro** rutas de apertura **y en el retome** | **Sí** — es el `ID_Sesion` del flujo. `03` **no navega** si no lo consiguió. Si igual llegara en blanco, *Leer contadora* queda deshabilitado y la pantalla lo dice (nunca sigue en silencio) |
| `gvarDeclaredBag` — registro de `CH-Tbl-DeclaredBags` **o `Blank()`** | `03` (§6.3 / §6.4, releído **completo** por PK) | No — **`Blank()` es un estado válido**: es la ruta *"tula sin declaración"*. Todo lo declarado queda vacío y *Terminar tula* va **siempre** a revisión con `RequestType` `"Tula recontada sin declaracion"` |
| `gvarSolicitud` = **`Blank()`** | `03` §7, antes de navegar (`03` §8, divergencia `D03-10`) | **Sí** — `gvarSolicitud` es **un global para tres pantallas**. Si `03` no lo limpiara, el `tmrPollCon` de esta pantalla adoptaría la solicitud de apertura que acaba de resolverse y abriría el overlay `C7` sobre un conteo que nadie envió a revisión |
| `gvarDepositoSinGuardar` = `false` | `03` §7, antes de navegar | **Sí** — si llega en `true`, la guarda de salida dispara sobre un depósito que no existe |
| `colDetalleConteoCon`, `colChecksCon`, `colAutoContadoraCon`, `colDepDeclaradosCon`, `colDepRecontadosCon`, `colChequesDeclaradosCon`, `colChecksTulaCon`, `colDetalleDVCon`, `colChecksDVCon`, `colHallazgosCon` **vacías** | `03` §7, con los **diez** `Clear` del contrato (`01` §4.5) | **Sí** — el `OnVisible` las repuebla; los `Clear` de `03` son lo que **habilita** esa repoblación. Si llegan con datos de la tula anterior, el `OnVisible` los da por buenos |

### Qué hace `03` al mandar una tula a esta pantalla

🧨 **El contrato canónico es `03` §8** (*CONTRATO CANÓNICO `03` → `04`*), que a su vez
cumple el de `01` §4.5. Este bloque es su **copia verificada**, tal cual lo ejecuta
`btnEntrarConteoTul` (`03` §7). Si alguna vez difieren, **manda `03` §8**.

```powerfx
// gvarTula y gvarDeclaredBag ya vienen seteados por btnAbrirNuevaTul (03 §6.3) o por
// btnReabrirTul (03 §6.4). btnEntrarConteoTul agrega el detalle de sesión y el contrato:
Set(gvarSesionDetalle, <registro de CH-Tbl-RecountingSessionDetails, SIEMPRE presente>);
Set(gvarSolicitud, Blank());                 // 🧨 03 §8 · D03-10 — 01 §4.5 no lo listaba
Set(gvarDepositoSinGuardar, false);
Clear(colDetalleConteoCon); Clear(colChecksCon); Clear(colAutoContadoraCon);
Clear(colDepDeclaradosCon); Clear(colDepRecontadosCon); Clear(colChequesDeclaradosCon);
Clear(colChecksTulaCon);    Clear(colDetalleDVCon);    Clear(colChecksDVCon);
Clear(colHallazgosCon);
Notify("Tula " & gvarTula.BagID & " abierta.", NotificationType.Information);
Navigate(scrConteo)
```

> 🧨 **`03` NO crea ninguna variable de "datos de contadora".** La v1 tenía
> `gvarDatosConteo` con `.ID_Sesion` y `.Serial_Maquina`. En la v2 **esa variable no
> existe**: los dos parámetros del flujo salen de dos globales que ya están en el contrato
> — `Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail')` y
> `gvarMesa.MachineSerialNumber` (`01` §7.11). Divergencia **`D04-1`**, **reconciliada**:
> `03` §8 dice **exactamente lo mismo**, con la misma tabla de los cinco datos de contadora
> y sin ninguna variable nueva.

> 🧨 **`03` NO precarga ninguna colección de `04`.** Las cinco consultas de apertura las
> hace el `OnVisible` de esta pantalla, en **una sola tanda `Concurrent`** (§1). Dos copias
> de la misma carga se desincronizan a la primera corrección — mismo criterio que
> `reception-rebuild/02` §CONTRATO.

> **Lo que `03` NO limpia y esta pantalla NO toca**: `colTulasSesionTul`,
> `colDepositosSesionTul`, `colTulasCandidatasTul` y `colDeclaradasCandidatasTul` son de
> `03` y se quedan como están (`03` §8). `scrConteo` no las lee ni las escribe.

### Variables y colecciones **propias** de esta pantalla

Las globales y las colecciones sembradas en `App.OnStart` están en `01` §4. Lo que **nace
acá** y `01` §4 **no declara** (los huecos de `01` que esta pantalla detectó están en §14.4):

| Nombre | Tipo | Para qué |
|---|---|---|
| `colChecksTulaCon` | Colección de `CH-Tbl-Checks` | **Todos** los cheques recontados de la TULA (🧨 espejo **E-2**). Alimenta la cantidad y el monto de cheques *Recontado* de las 3 matrices, el pie por moneda (§5.3), el hallazgo de *cheques manuales* y el marcado a `Recontado` del cierre. ✅ **Ya declarada en `01` §4.2** (era el hueco `H04-1`) |
| `colDetalleDVCon` | Colección de `CH-Tbl-CountedMoneyDetails` | Snapshot de las filas **persistidas** del depósito en curso; origen del **paso de bajas** de denominaciones. ✅ **Ya declarada en `01` §4.2** (era el hueco `H04-2`) |
| `colChecksDVCon` | Colección de `CH-Tbl-Checks` | Snapshot de las filas **persistidas** del depósito en curso; origen del **paso de bajas** de cheques. ✅ **Ya declarada en `01` §4.2** (era el hueco `H04-3`) |
| `ctxDepositoCon` | Registro de `CH-Tbl-DepositsRecountings` | **El depósito en curso.** `Defaults('CH-Tbl-DepositsRecountings')` = depósito nuevo sin guardar. "Es nuevo" se lee como `IsBlank(ctxDepositoCon.'CH-Tbl-DepositsRecounting')` |
| `ctxRegDepCon` | Registro de `CH-Tbl-DepositsRecountings` | 🧨 **Parámetro del motor `btnCargarDepCon`** (Bloque C, §6.3): el depósito que se quiere cargar en el panel derecho. **Cada llamador lo setea en la instrucción inmediatamente anterior al `Select(btnCargarDepCon)`**, y nadie más lo lee. Centinela `Defaults(…)` |
| `ctxDepDecCursoCon` | Registro de `CH-Tbl-DeclaredDeposits` | El **declarado enlazado** al depósito en curso (o el centinela `Defaults(…)`). Se escribe **SIEMPRE junto** con `ctxDepositoCon`, en el Bloque **C** (§6.3) |
| `ctxHayDepositoCon` | Booleano | `false` = no hay depósito en curso ⇒ el panel derecho muestra el estado vacío del mockup `C4`. Arranca en `false` |
| `ctxDepSelCon` | Registro de `CH-Tbl-DepositsRecountings` | Fila **seleccionada** de la pestaña *Recontado*. Centinela `Defaults(…)` = sin selección (`01` §7.3, excepción declarada: de esta selección cuelga *Eliminar*) |
| `ctxDepDecSelCon` | Registro de `CH-Tbl-DeclaredDeposits` | Fila seleccionada de la pestaña *Declarado*. Mismo centinela |
| `ctxMonedaCon` | Texto `"NIO"` / `"USD"` / `"EUR"` | Moneda del depósito en curso. Gobierna `colDenominacionesCon` filtrada, `fxSimbolo()` y el filtro de cheques declarados |
| `ctxSoloLecturaCon` | Booleano | **Modo solo lectura de TODA la pantalla** (§0.2) |
| `ctxGuardandoCon` | Booleano | `Start` de `tmrGuardarCon` (receta `01` §7.10.C). La sube el `OnSelect`, la baja el `OnTimerEnd` |
| `ctxSalirTrasGuardarCon` | Booleano | *Guardar y salir*: le dice al `OnTimerEnd` que navegue **después** de guardar. Los timers no corren en pantallas no visibles |
| `ctxDlgMonedaCon`, `ctxDlgEliminarCon`, `ctxDlgDescartarCon`, `ctxDlgGuardandoCon`, `ctxDlgTerminarOkCon`, `ctxDlgTerminarDifCon`, `ctxDlgSalirCon` | Booleanos | `Visible` de cada diálogo (`01` §4, `ctxDlg…`) |
| `ctxEliminandoCon` | Booleano | **Discriminador de ORIGEN** de `btnEliminarDepositoCon.OnSelect`: `false` = el clic **abre** el diálogo; `true` = lo disparó el `OnConfirmar` y toca **ejecutar** (§13.3). Se consume en la primera línea |
| `ctxEsperandoCon`, `ctxRechazadaCon` | Booleanos | Overlays `C7` / `C8` del polling (`01` §5.8) |
| `ctxRespuestaFlujoCon` | Registro | Respuesta de `CH-Flow-ProcessXMLFiles` (`01` §7.11) |
| `ctxSec1Con` … `ctxSec6Con`, `ctxHtmlCon` | Texto | Las 6 secciones y el documento del HTML de approval (receta `01` §7.6) |
| `colDifTrxCon`, `colDepRecSinEnlazarCon`, `colChqDecSinEnlazarCon`, `colChqManualCon`, `colDepDecSinEnlazarCon` | Colecciones con `Indice` | Las 5 colecciones de hallazgos de `01` §4.2. **Se pueblan ANTES de validar** (§10.1) |
| `colBorrarTmpCon` · `colLecturasTmpCon` | Colecciones **TEMPORALES de un solo paso** | 🧨 **NO entran al inventario de `01` §4.2 ni a la siembra de `App.OnStart`.** `colBorrarTmpCon` congela el desglose a borrar en §5.5 paso 3 (regla **`D-14`**, snapshot obligatorio: no se itera un datasource mientras se lo borra, `L-63`); `colLecturasTmpCon` congela la lectura cruda de la contadora en §8.4 para agregarla con `Distinct` + `Sum` (`L-64`). Las dos **nacen con un `ClearCollect` y mueren con el `Clear` de dos instrucciones después**; ningún `Items`, `Visible` ni `IsEmpty()` las lee, que es la única razón por la que una colección necesita semilla de esquema (`01` §3.2) |

> 🧨 **`ctxDepositoCon` y `ctxDepDecCursoCon` se siembran con `Defaults(<ds>)`, nunca con
> `Blank()`.** `Defaults(ds)` da un registro **TIPADO** sin consultar nada (mismo truco que
> `01` §3.2). Un `Blank()` pelado deja el tipo sin inferir y toda referencia a
> `ctxDepositoCon.CashAmount` queda en rojo en tiempo de diseño.

---

## 0. Cinco reglas que gobiernan TODA la pantalla

### 0.1 La conjunción del tab — sin excepción

`tabDepositosCon` (Declarado/Recontado) y `tabDetalleCon` (Billetes/Monedas/Cheques) son
**Listas de pestañas modernas**: no crean pantallas, solo cambian un valor. Por eso **todo
control de CONTENIDO de una vista lleva la conjunción del tab en su `Visible`**:

```powerfx
tabDepositosCon.Selected.Value = "Recontado" && <el resto de la condición>
tabDetalleCon.Selected.Value   = "Billetes"  && <el resto de la condición>
```

**Las excepciones son el *chrome compartido*** — controles idénticos en las dos/tres vistas,
que por eso no llevan condición de tab: `shpCardDepCon`, `shpHeadDepCon`,
`shpLineaHeadDepCon`, `shpFootDepCon` y los 5 rótulos del pie (panel izquierdo);
`shpCardCurCon`, `shpHeadDetCon`, `shpLineaHeadDetCon`, `shpFootAccionesCon` y la botonera
(panel derecho). Nada más.

> 🧨 **`Reset(tabDepositosCon)` y `Reset(tabDetalleCon)` al final del `OnVisible`.** El
> estado de una Lista de pestañas **PERSISTE entre visitas**: sin el `Reset`, volvés de
> `scrTulas` con otra tula y te encontrás en *Cheques* de la anterior.

### 0.2 🧨 Modo SOLO LECTURA — el mecanismo, en un solo lugar

Los estados terminales (`Recontada`, `Pendiente de revision por supervisor - recuento`) y
cualquier otro que no sea `Asignada a cajero` dejan la pantalla **en solo lectura
completa**. El mecanismo es **una sola variable de pantalla**:

```powerfx
// ctxSoloLecturaCon — se escribe en EXACTAMENTE cuatro lugares, y en ningún otro:
//   1. scrConteo.OnVisible                              (§1, bloque 5)   ── DERIVADA
//   2. el confirmar de C4 — Terminar sin discrepancias  (§11.3)          ── literal `true`
//   3. el confirmar de C5 — Enviar a revisión           (§11.5, paso 9)  ── DERIVADA
//   4. la rama Rechazado del polling                    (§12.3)          ── DERIVADA
// 🧨 TRES de los cuatro usan la MISMA expresión derivada de abajo, y por el mismo
//    motivo: el Patch de BagStatus va con IfError y puede haber fallado. Forzar `true`
//    ahí congelaría una tula que en Dataverse sigue en 'Asignada a cajero' — la UI
//    mintiendo sobre el servidor. El único `true` literal es el de C4 (§11.3), y solo
//    porque ahí el Patch ya está verificado contra colFallidasCon antes de llegar.
UpdateContext({
    ctxSoloLecturaCon:
        gvarTula.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'
})
```

**Regla derivada, sin excepción.** Toda propiedad de habilitación de esta pantalla se
escribe con esta conjunción **literal**, que la guía llama **`EDITABLE`**:

```powerfx
// EDITABLE  (se escribe TAL CUAL en cada DisplayMode; no es una variable)
If(gvarIdentidadOk && !ctxSoloLecturaCon, DisplayMode.Edit, DisplayMode.Disabled)
```

La llevan: `ddMonedaCon`, `inpCuentaCon`, `inpCantidadBilCon`, `inpCantidadMonCon`,
`chkChequeCon`, `inpNumChequeCon`, `ddTipoChequeCon`, `inpMontoChequeCon`,
`btnEnlazarCon`, `btnDesenlazarCon`, `btnEliminarDepositoCon`, `btnLeerContadoraCon`,
`btnAgregarChequeCon`, `btnNuevoDepositoCon`, `btnDescartarCon`, `btnGuardarDepositoCon`,
`btnTerminarCon`. Y `icoBorrarChequeCon` lo lleva en su `Visible`.

**No lo llevan** (a propósito): `btnVolverCon`, `btnCerrarTulaCon` y todo lo decorativo —
de una tula recontada se sale igual.

> 🧨 **`gvarIdentidadOk &&` va PRIMERO** (`01` §3.2, §6.3). Una tula abierta con
> `gvarUser` en blanco no puede escribir ni un cheque.

### 0.3 🧨 Anclas relativas: los tres números que NO se escriben a mano

Tres literales aparecían **quince veces** repartidos por la guía, y los tres son
**derivables de la card que los contiene** (regla `01` §0.9: cero números mágicos
duplicados). Se escriben **así**, siempre:

| Literal viejo | Cuántas veces | Se escribe | Qué es |
|---|---|---|---|
| `1332 - Self.Width` | 5 | **`shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width`** | borde derecho interno del panel derecho (`736 + 612 − 16`) |
| `706 - Self.Width` / `706 - Self.X` | 2 | **`shpCardDepCon.X + shpCardDepCon.Width - 16 - Self.Width`** (o `- Self.X`) | borde derecho interno del panel izquierdo (`18 + 704 − 16`) |
| `X 752` | 8 | **`shpCardCurCon.X + 16`** | margen izquierdo interno del panel derecho (`736 + 16`) |

**Por qué**: si mañana el reparto de anchos cambia —`.card{flex:1.15}` contra `.card{flex:1}`
del mockup— con literales hay que cazar **quince** números en cinco secciones distintas y el
que se olvide queda **descolgado en silencio**, sin error de compilación y sin que nada se
vea roto en Studio hasta que alguien mire de cerca. Con anclas, el ancho vive en **un** lugar
(`shpCardDepCon.Width` / `shpCardCurCon.Width`) y todo lo demás lo sigue.

> **Los `754` NO se tocan.** `754 = 736 + 18` es el margen de las **grillas** de
> denominaciones (padding `18` del mockup), no el de la card (`16`): son dos medidas
> distintas que casualmente casi coinciden. Colapsarlas sería inventar una equivalencia que
> el mockup no tiene.

### 0.4 Coordenadas dentro de una galería (se enuncia UNA vez, `01` §0.7)

> **X de un control del template = X del encabezado − X de la galería.**
> `galDepDeclaradosCon.X = galDepRecontadosCon.X = 18` ⇒ restar **18**.
> `galBilletesCon.X = galMonedasCon.X = galChequesCon.X = 736` ⇒ restar **736**.
> `galHallazgosCon` y `galResumenCon` viven dentro de la card de su diálogo: su X es
> relativo a esa card.

### 0.5 Mapa de la pantalla (1366 × 768, medido contra el CSS del mockup)

| Banda | X · Y | Ancho · Alto | De dónde sale |
|---|---|---|---|
| Header | `0` · `0` | `1366` · `66` | `.hdr{height:66px}` de `#c0` |
| Franja de matrices | `18` · `78` | `1330` · `100` | `.matStrip{padding:12px 18px 0}` ⇒ `66 + 12 = 78`; alto = `cmpMatrizMoneda` (`01` §8.1) |
| Cuerpo | `18` · `190` | `1330` · `562` | `.body2{padding:12px 18px 16px}` ⇒ `78 + 100 + 12 = 190`; abajo `768 − 16 = 752` |
| Card **Depósitos** | `18` · `190` | `704` · `562` | `.card{flex:1.15}` de `(1366 − 36 − 14)` con `gap:14` |
| Card **Depósito en curso** | `736` · `190` | `612` · `562` | `.card{flex:1}` · `736 = 18 + 704 + 14` · termina en `1348 = 1366 − 18` |

Las 3 matrices: ancho `435` cada una con `gap 12` ⇒ X `18`, `465`, `912`
(`(1330 − 24) / 3 = 435,33`, se toma `435`; el sobrante de 1 px queda a la derecha).

---

## Árbol de controles

Orden del árbol = **orden de inserción = z-order** (`01` §0.6): lo insertado después queda
encima. Construcción en **DOS PASES** (`01` §0.11): primero insertar y renombrar TODO en
este orden exacto, después llenar propiedades.

```
scrConteo
│  ── HEADER (§2) ──
├─ shpHeaderCon                    Rectángulo clásico
├─ shpHeaderLineaCon               Rectángulo clásico
├─ btnVolverCon                    Botón clásico
├─ icoVolverCon                    Icono clásico  Icon.Back
├─ lblTulaCon                      Etiqueta clásica
├─ lblMetaPlanLabCon               Etiqueta clásica
├─ lblMetaPlanCon                  Etiqueta clásica
├─ lblMetaClienteCon               Etiqueta clásica
├─ pilEstadoTulaCon                Botón clásico          (receta 7.4, variante FUERA de template)
├─ shpChipMesaCon                  Botón clásico          (receta 7.1)
├─ lblChipMesaCon                  Etiqueta clásica
├─ btnCerrarTulaCon                Botón clásico
├─ btnTerminarCon                  Botón clásico
│  ── FRANJA DE MATRICES (§3) ──
├─ cmpMatNIOCon                    Componente canvas      (01 §8.1)
├─ cmpMatUSDCon                    Componente canvas      (01 §8.1)
├─ cmpMatEURCon                    Componente canvas      (01 §8.1)
│  ── PANEL IZQUIERDO · DEPÓSITOS (§4, §5) ──
├─ shpCardDepCon                   Botón clásico          (receta 7.1 — card)
├─ lblPanelDepTitCon               Etiqueta clásica
├─ lblPanelDepSubCon               Etiqueta clásica
├─ shpLineaPanelDepCon             Rectángulo clásico
├─ tabDepositosCon                 Lista de pestañas moderna
├─ shpLineaTabDepCon               Rectángulo clásico
├─ btnEnlazarCon                   Botón clásico
├─ btnDesenlazarCon                Botón clásico
├─ btnEliminarDepositoCon          Botón clásico
├─ lblOpsHintCon                   Etiqueta clásica
├─ shpLineaOpsDepCon               Rectángulo clásico
├─ shpHeadDepCon                   Botón clásico          (receta 7.1 — banda de encabezados)
├─ lblThCuentaCon                  Etiqueta clásica
├─ lblThMonedaCon                  Etiqueta clásica
├─ lblThChqCon                     Etiqueta clásica
├─ lblThMontoChqDepCon             Etiqueta clásica
├─ lblThEfectivoCon                Etiqueta clásica
├─ lblThTotalDepCon                Etiqueta clásica
├─ lblThEnlaceCon                  Etiqueta clásica
├─ shpLineaHeadDepCon              Rectángulo clásico
├─ galDepDeclaradosCon             Galería vertical en blanco
│  ├─ shpSelDepDecCon              Botón clásico          (receta 7.3 — al FONDO)
│  ├─ shpSelBarraDepDecCon         Rectángulo clásico
│  ├─ lblCuentaDepDecCon           Etiqueta clásica
│  ├─ lblMonedaDepDecCon           Etiqueta clásica
│  ├─ lblChqDepDecCon              Etiqueta clásica
│  ├─ lblMontoChqDepDecCon         Etiqueta clásica
│  ├─ lblEfectivoDepDecCon         Etiqueta clásica
│  ├─ lblTotalDepDecCon            Etiqueta clásica
│  ├─ pilEnlaceDepDecCon           Botón clásico          (receta 7.4, variante ENLACE)
│  └─ shpSepDepDecCon              Rectángulo clásico
├─ galDepRecontadosCon             Galería vertical en blanco
│  ├─ shpSelDepRecCon              Botón clásico          (receta 7.3 — al FONDO)
│  ├─ shpSelBarraDepRecCon         Rectángulo clásico
│  ├─ lblCuentaDepRecCon           Etiqueta clásica
│  ├─ lblMonedaDepRecCon           Etiqueta clásica
│  ├─ lblChqDepRecCon              Etiqueta clásica
│  ├─ lblMontoChqDepRecCon         Etiqueta clásica
│  ├─ lblEfectivoDepRecCon         Etiqueta clásica
│  ├─ lblTotalDepRecCon            Etiqueta clásica
│  ├─ pilEnlaceDepRecCon           Botón clásico          (receta 7.4, variante ENLACE)
│  └─ shpSepDepRecCon              Rectángulo clásico
├─ lblVacioDepCon                  Etiqueta clásica
├─ shpFootDepCon                   Botón clásico          (receta 7.1 — pie)
├─ lblFootDepLabCon                Etiqueta clásica
├─ lblFootDepChqCon                Etiqueta clásica
├─ lblFootDepMontoChqCon           Etiqueta clásica
├─ lblFootDepEfectivoCon           Etiqueta clásica
├─ lblFootDepTotalCon              Etiqueta clásica
│  ── PANEL DERECHO · DEPÓSITO EN CURSO (§6, §7, §8, §9) ──
├─ shpCardCurCon                   Botón clásico          (receta 7.1 — card)
├─ lblPanelCurTitCon               Etiqueta clásica
├─ lblPanelCurSubCon               Etiqueta clásica
├─ pilSinGuardarCon                Botón clásico          (receta 7.4)
├─ shpLineaPanelCurCon             Rectángulo clásico
├─ lblLabMonedaCon                 Etiqueta clásica
├─ ddMonedaCon                     Menú desplegable clásico
├─ lblLabCuentaCon                 Etiqueta clásica
├─ inpCuentaCon                    Entrada de texto clásica
├─ shpLineaCurRowCon               Rectángulo clásico
├─ shpBandaMatDepCon               Botón clásico          (receta 7.1 — banda ThemeSurface2)
├─ cmpMatDepositoCon               Componente canvas      (01 §8.1)
├─ shpLineaMatDepCon               Rectángulo clásico
├─ tabDetalleCon                   Lista de pestañas moderna
├─ shpLineaTabDetCon               Rectángulo clásico
├─ lblHintDetalleCon               Etiqueta clásica
├─ btnLeerContadoraCon             Botón clásico
├─ icoLeerContadoraCon             Icono clásico  Icon.Reload
├─ btnAgregarChequeCon             Botón clásico
├─ icoAgregarChequeCon             Icono clásico  Icon.Add
├─ shpLineaOpsDetCon               Rectángulo clásico
├─ shpHeadDetCon                   Botón clásico          (receta 7.1 — banda de encabezados)
├─ lblThDenBilCon                  Etiqueta clásica
├─ lblThAutoBilCon                 Etiqueta clásica
├─ lblThCantBilCon                 Etiqueta clásica
├─ lblThTotBilCon                  Etiqueta clásica
├─ lblThDenMonCon                  Etiqueta clásica
├─ lblThCantMonCon                 Etiqueta clásica
├─ lblThTotMonCon                  Etiqueta clásica
├─ lblThNumChqCon                  Etiqueta clásica
├─ lblThTipoChqCon                 Etiqueta clásica
├─ lblThMonChqCon                  Etiqueta clásica
├─ lblThMontoChqCon                Etiqueta clásica
├─ lblThOrigenChqCon               Etiqueta clásica
├─ shpLineaHeadDetCon              Rectángulo clásico
├─ galBilletesCon                  Galería vertical en blanco
│  ├─ lblDenBilCon                 Etiqueta clásica
│  ├─ btnAutoBilCon                Botón clásico          (ESTILO-AUTO, receta 7.9)
│  ├─ inpCantidadBilCon            Entrada de texto clásica
│  ├─ lblTotFilaBilCon             Etiqueta clásica
│  └─ shpSepBilCon                 Rectángulo clásico
├─ galMonedasCon                   Galería vertical en blanco
│  ├─ lblDenMonCon                 Etiqueta clásica
│  ├─ inpCantidadMonCon            Entrada de texto clásica
│  ├─ lblTotFilaMonCon             Etiqueta clásica
│  └─ shpSepMonCon                 Rectángulo clásico
├─ galChequesCon                   Galería vertical en blanco
│  ├─ shpFilaChqCon                Botón clásico          (receta 7.1 — fondo ámbar manual, al FONDO)
│  ├─ chkChequeCon                 Casilla clásica
│  ├─ lblGuionChqCon               Etiqueta clásica
│  ├─ lblNumChequeCon              Etiqueta clásica
│  ├─ inpNumChequeCon              Entrada de texto clásica
│  ├─ lblTipoChequeCon             Etiqueta clásica
│  ├─ ddTipoChequeCon              Menú desplegable clásico
│  ├─ lblMonChequeCon              Etiqueta clásica
│  ├─ lblMontoChequeCon            Etiqueta clásica
│  ├─ inpMontoChequeCon            Entrada de texto clásica
│  ├─ pilOrigenChequeCon           Botón clásico          (receta 7.4, variante ORIGEN)
│  ├─ icoBorrarChequeCon           Icono clásico  Icon.Trash
│  └─ shpSepChqCon                 Rectángulo clásico
├─ lblVacioDetalleCon              Etiqueta clásica
├─ lblVacioCursoCon                Etiqueta clásica
├─ shpFootBilCon                   Botón clásico          (receta 7.1 — pie)
├─ lblFootBilLabCon                Etiqueta clásica
├─ lblFootBilAutoCon               Etiqueta clásica
├─ lblFootBilManualCon             Etiqueta clásica
├─ lblFootBilTotalCon              Etiqueta clásica
├─ shpFootMonCon                   Botón clásico          (receta 7.1 — pie)
├─ lblFootMonLabCon                Etiqueta clásica
├─ lblFootMonCantCon               Etiqueta clásica
├─ lblFootMonTotalCon              Etiqueta clásica
├─ shpBandaEfectivoCon             Botón clásico          (receta 7.1 — banda)
├─ lblEfectivoDepositoCon          Etiqueta clásica
├─ shpFootChqCon                   Botón clásico          (receta 7.1 — pie)
├─ lblFootChqLabCon                Etiqueta clásica
├─ lblFootChqTotalCon              Etiqueta clásica
├─ shpFootAccionesCon              Botón clásico          (receta 7.1 — botonera)
├─ btnNuevoDepositoCon             Botón clásico
├─ btnDescartarCon                 Botón clásico
├─ lblTotalDepositoCon             Etiqueta clásica
├─ btnGuardarDepositoCon           Botón clásico
│  ── DIÁLOGOS Y OVERLAYS (§10..§12) — insertados ÚLTIMOS, en este orden ──
├─ cmpDlgMonedaCon                 Componente canvas      (01 §8.3 — C6-1)
├─ cmpDlgEliminarCon               Componente canvas      (01 §8.3 — C6-2)
├─ cmpDlgDescartarCon              Componente canvas      (01 §8.3 — C6-3)
├─ cntDlgGuardandoCon              Contenedor de diseño manual   ── OVERLAY A (receta 7.2)
│  ├─ btnBloqGuardandoCon          Botón clásico
│  └─ cntCardGuardandoCon          Contenedor de diseño manual
│     ├─ lblDlgTitGuardandoCon     Etiqueta clásica
│     └─ lblDlgTxtGuardandoCon     Etiqueta clásica
├─ cntDlgTerminarOkCon             Contenedor de diseño manual   ── OVERLAY B (C4)
│  ├─ btnBloqTerminarOkCon         Botón clásico
│  └─ cntCardTerminarOkCon         Contenedor de diseño manual
│     ├─ btnIcoTerminarOkCon       Botón clásico          (receta 7.1)
│     ├─ icoTipoTerminarOkCon      Icono clásico  Icon.Check
│     ├─ lblDlgTitTerminarOkCon    Etiqueta clásica
│     ├─ lblDlgTxtTerminarOkCon    Etiqueta clásica
│     ├─ icoDlgXTerminarOkCon      Icono clásico  Icon.Cancel
│     ├─ btnAvisoTerminarOkCon     Botón clásico          (receta 7.2.a)
│     ├─ icoAvisoTerminarOkCon     Icono clásico  Icon.Check
│     ├─ lblAvisoTerminarOkCon     Etiqueta clásica
│     ├─ shpHeadResumenCon         Botón clásico          (receta 7.1)
│     ├─ lblThResMonedaCon         Etiqueta clásica
│     ├─ lblThResChqCon            Etiqueta clásica
│     ├─ lblThResMontoCon          Etiqueta clásica
│     ├─ lblThResEfectivoCon       Etiqueta clásica
│     ├─ lblThResTotalCon          Etiqueta clásica
│     ├─ galResumenCon             Galería vertical en blanco
│     │  ├─ lblResMonedaCon        Etiqueta clásica
│     │  ├─ lblResChqCon           Etiqueta clásica
│     │  ├─ lblResMontoCon         Etiqueta clásica
│     │  ├─ lblResEfectivoCon      Etiqueta clásica
│     │  ├─ lblResTotalCon         Etiqueta clásica
│     │  └─ shpSepResumenCon       Rectángulo clásico
│     ├─ lblNotaTerminarOkCon      Etiqueta clásica
│     ├─ btnDlgCancelTerminarOkCon Botón clásico
│     └─ btnDlgOkTerminarOkCon     Botón clásico
├─ cntDlgTerminarDifCon            Contenedor de diseño manual   ── OVERLAY C (C5)
│  ├─ btnBloqTerminarDifCon        Botón clásico
│  └─ cntCardTerminarDifCon        Contenedor de diseño manual
│     ├─ btnIcoTerminarDifCon      Botón clásico          (receta 7.1)
│     ├─ icoTipoTerminarDifCon     Icono clásico  Icon.Warning
│     ├─ lblDlgTitTerminarDifCon   Etiqueta clásica
│     ├─ lblDlgTxtTerminarDifCon   Etiqueta clásica
│     ├─ icoDlgXTerminarDifCon     Icono clásico  Icon.Cancel
│     ├─ galHallazgosCon           Galería vertical en blanco
│     │  ├─ btnAvisoHallazgoCon    Botón clásico          (receta 7.2.a)
│     │  ├─ icoAvisoHallazgoCon    Icono clásico  Icon.Warning
│     │  └─ lblAvisoHallazgoCon    Etiqueta clásica
│     ├─ btnAvisoRevisionCon       Botón clásico          (receta 7.2.a)
│     ├─ icoAvisoRevisionCon       Icono clásico  Icon.Information
│     ├─ lblAvisoRevisionCon       Etiqueta clásica
│     ├─ btnDlgCancelTerminarDifCon Botón clásico
│     └─ btnDlgOkTerminarDifCon    Botón clásico
├─ cntDlgSalirCon                  Contenedor de diseño manual   ── OVERLAY D (guarda de salida)
│  ├─ btnBloqSalirCon              Botón clásico
│  └─ cntCardSalirCon              Contenedor de diseño manual
│     ├─ lblDlgTitSalirCon         Etiqueta clásica
│     ├─ lblDlgTxtSalirCon         Etiqueta clásica
│     ├─ icoDlgXSalirCon           Icono clásico  Icon.Cancel
│     ├─ btnDlgCancelSalirCon      Botón clásico
│     ├─ btnDlgDescartarSalirCon   Botón clásico
│     └─ btnDlgGuardarSalirCon     Botón clásico
├─ cntEsperaCon                    Contenedor de diseño manual   ── OVERLAY E (C7)
│  ├─ btnBloqEsperaCon             Botón clásico
│  └─ cntCardEsperaCon             Contenedor de diseño manual
│     ├─ lblDlgTitEsperaCon        Etiqueta clásica
│     ├─ lblDlgTxtEsperaCon        Etiqueta clásica
│     ├─ btnFilaEsperaCon          Botón clásico          (receta 7.1 — .waitRow)
│     ├─ spnEsperaCon              Spinner moderno
│     ├─ lblEsperaTitCon           Etiqueta clásica
│     ├─ lblEsperaSubCon           Etiqueta clásica
│     ├─ lblKvTulaEtqEspCon        Etiqueta clásica       (receta 7.2.b)
│     ├─ lblKvTulaEspCon           Etiqueta clásica
│     ├─ lblKvPlanEtqEspCon        Etiqueta clásica
│     ├─ lblKvPlanEspCon           Etiqueta clásica
│     ├─ lblKvHallEtqEspCon        Etiqueta clásica
│     ├─ lblKvHallEspCon           Etiqueta clásica
│     ├─ lblKvEnvEtqEspCon         Etiqueta clásica
│     ├─ lblKvEnvEspCon            Etiqueta clásica
│     └─ btnVolverTulasEspCon      Botón clásico
├─ cntRechazoCon                   Contenedor de diseño manual   ── OVERLAY F (C8)
│  ├─ btnBloqRechazoCon            Botón clásico
│  └─ cntCardRechazoCon            Contenedor de diseño manual
│     ├─ btnIcoRechazoCon          Botón clásico          (receta 7.1)
│     ├─ icoTipoRechazoCon         Icono clásico  Icon.Blocked
│     ├─ lblDlgTitRechazoCon       Etiqueta clásica
│     ├─ lblDlgTxtRechazoCon       Etiqueta clásica
│     ├─ icoDlgXRechazoCon         Icono clásico  Icon.Cancel
│     ├─ btnAvisoRechazoCon        Botón clásico          (receta 7.2.a)
│     ├─ icoAvisoRechazoCon        Icono clásico  Icon.Blocked
│     ├─ lblAvisoRechazoCon        Etiqueta clásica
│     ├─ lblNotaRechazoCon         Etiqueta clásica
│     ├─ btnVolverTulasRecCon      Botón clásico
│     └─ btnCorregirCon            Botón clásico
│  ── MOTORES 1×1 · FUERA DE TODO OVERLAY (§6) ──
├─ btnVolcarGrillaCon              Botón clásico          (motor 1×1 — Bloque V, §6.2)
├─ btnCargarDepCon                 Botón clásico          (motor 1×1 — Bloque C, §6.3)
├─ btnDepNuevoCon                  Botón clásico          (motor 1×1 — Bloque N, §6.4)
├─ btnRehacerMonedaCon             Botón clásico          (motor 1×1 — Bloque M, §6.5)
├─ btnIrTulasCon                   Botón clásico          (motor 1×1 — SALIDA, §1.1, L-65)
│  ── TEMPORIZADORES · FUERA DE TODO OVERLAY, últimos de la pantalla ──
├─ tmrGuardarCon                   Temporizador clásico   (receta 01 §7.10.C)
└─ tmrPollCon                      Temporizador clásico   (receta 01 §7.7)
```

**246 controles** — 13 header · 3 matrices · 49 panel izquierdo · 81 panel derecho ·
93 diálogos y overlays · **5 motores 1×1** · 2 temporizadores.

> **Los motores son CINCO, con dos papeles**: los **cuatro Bloques V/C/N/M** (§6), que
> factorizan rutinas de negocio, y **`btnIrTulasCon`** (§1.1), el **motor de salida**, que
> existe por otra razón — `Navigate()` está prohibido en un `OnVisible` (`L-65`). Donde
> esta guía dice *"los cuatro motores"* habla de los Bloques.

> 🧨 **Los CINCO motores 1×1 cuelgan de la PANTALLA y van DESPUÉS de los overlays.**
> Son botones transparentes de 1 × 1 apoyados en la última fila de píxeles, invocados
> con `Select()`; nunca `Visible: false` (`L-53`, `03` `D03-9`). Van después de los
> overlays por el mismo motivo que los timers: un control dentro de un contenedor con
> `Visible false` **no se puede `Select()`**, y `cmpDlgDescartarCon`, `cntDlgTerminarDifCon`
> y compañía están ocultos justo cuando sus `OnConfirmar` los invocan. Sus propiedades y sus
> `OnSelect` —que **son** los Bloques V, C, N y M— están en **§6**.

> 🧨 **Los DOS temporizadores van FUERA de los overlays y son los ÚLTIMOS controles de la
> pantalla.** Son invisibles y no dibujan nada, pero **tienen que colgar de la pantalla, no
> de un contenedor de diálogo**: un timer dentro de un contenedor con `Visible false` **no
> corre**, y `cntDlgGuardandoCon` está oculto justo en el instante en que `tmrGuardarCon`
> tiene que arrancar. Y **ningún timer corre en una pantalla que no está visible** — por eso
> todo `Navigate` de un flujo diferido vive dentro del `OnTimerEnd`.

> El panel de árbol de Studio los lista **al revés** (el frontal arriba). De eso dependen
> las anotaciones de z-order de esta guía: `shpSelDepDecCon`/`shpSelDepRecCon`/`shpFilaChqCon`
> al **FONDO** de su template, y los 6 overlays al **TOPE** de la pantalla en el orden A→F.

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | ver abajo — **COMPLETO, listo para pegar** |

```powerfx
// ============================================================================
// BLOQUE 0 — GUARDA DEL CONTRATO DE ENTRADA.
//   Sin tula no hay pantalla. No se degrada: se vuelve.
//   🧨 TODO lo que consulta o escribe estado vive en el ELSE de esta guarda:
//      `Navigate()` NO corta la fórmula (01 §7.10). Sin el else, las SEIS
//      consultas de los bloques 1 y 2 se disparaban igual, filtrando por una PK
//      vacía, en el único camino en el que la pantalla ya sabe que se va.
//      El bloque 7 (Reset de pestañas y galerías) queda FUERA: son llamadas
//      locales sin costo y dejan la pantalla limpia también al salir.
// ============================================================================
If(
    IsBlank(gvarTula.'CH-Tbl-Bags'),

    //    🧨 NUNCA Navigate() en un OnVisible (§1.1): el App Checker lo marca
    //       ("would automatically always navigate away"). El aviso y la vuelta
    //       viven en el motor btnIrTulasCon.
    Notify("No hay una tula abierta.", NotificationType.Error);
    Select(btnIrTulasCon),

    // ============================================================================
    // BLOQUE 1 — CARGA CONCURRENTE. Las 4 consultas que SIEMPRE corren.
    //   Las cuatro son delegables y PK contra PK (regla D-1). SortByColumns
    //   explícito por regla D-9 — el nombre de columna va con el NOMBRE LÓGICO.
    //   🧨 colDenominacionesCon trae TODAS las habilitadas (~20 filas, D-10) y se
    //      filtra por moneda EN LOCAL: cambiar de moneda no cuesta una consulta.
    // ============================================================================
    Concurrent(
        ClearCollect(
            colDepRecontadosCon,
            SortByColumns(
                Filter(
                    'CH-Tbl-DepositsRecountings',
                    BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'
                ),
                "createdon",
                SortOrder.Ascending
            )
        ),
        // 🧨 espejo E-2 (01 §2.2). Fallback sin backend: ver la nota al pie de este bloque.
        ClearCollect(
            colChecksTulaCon,
            Filter(
                'CH-Tbl-Checks',
                Bag.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'
            )
        ),
        ClearCollect(
            colDenominacionesCon,
            SortByColumns(
                Filter('CH-Tbl-MoneyDenominations', Enabled = true),
                "sanic_value",
                SortOrder.Descending
            )
        ),
        // Recuperación de solicitud pendiente (01 §5.8) — el cajero cerró la app esperando.
        //   🧨 ACOTADA AL TIPO DE ESTA PANTALLA Y A ESTA TULA. Traer "cualquier solicitud
        //      pendiente del cajero" abría el overlay C7 sobre una tula que nadie envió a
        //      revisión (bastaba una solicitud de mesa o de apertura en la cola).
        //      Los otros tres tipos los recupera la pantalla que los produce: 02 y 03.
        //   🧨 SIN `ApprovalStatus = Blank()` (01 §5.8, regla 4): trae la ÚLTIMA solicitud
        //      de esta tula, resuelta o no. Con el predicado puesto, un RECHAZO era
        //      invisible al reabrir la app y la tula quedaba en `Pendiente de revision` —
        //      o sea, en SOLO LECTURA (§0.2) — SIN ningún overlay que dijera por qué. El
        //      estado de la UI lo deriva el BLOQUE 5 del registro traído, en cliente.
        Set(
            gvarSolicitud,
            First(
                Sort(
                    Filter(
                        'CH-Tbl-ApprovalRequests',
                        'Created By'.User = gvarUser.User &&
                        BagNumber = Trim(gvarTula.BagID) &&
                        (
                            RequestType = "Tula con diferencia" ||
                            RequestType = "Tula recontada sin declaracion"
                        )
                    ),
                    'Created On',
                    SortOrder.Descending
                )
            )
        )
    );

    // ============================================================================
    // BLOQUE 2 — LO DECLARADO. Solo si la tula TIENE declaración.
    //   🧨 El If va POR FUERA del ClearCollect (regla D-11): ClearCollect(col,
    //      If(cond, Filter(...))) sin else revienta con ErrorKind.Internal.
    // ============================================================================
    If(
        IsBlank(gvarDeclaredBag.'CH-Tbl-DeclaredBags'),

        Clear(colDepDeclaradosCon);
        Clear(colChequesDeclaradosCon),

        Concurrent(
            ClearCollect(
                colDepDeclaradosCon,
                SortByColumns(
                    Filter(
                        'CH-Tbl-DeclaredDeposits',
                        DeclaredBag.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags'
                    ),
                    "createdon",
                    SortOrder.Ascending
                )
            ),
            // 🧨 espejo E-3 (01 §2.2). Fallback sin backend al pie de este bloque.
            ClearCollect(
                colChequesDeclaradosCon,
                Filter(
                    'CH-Tbl-DeclaredChecks',
                    DeclaredBagMirror.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags'
                )
            )
        )
    );

    // ============================================================================
    // BLOQUE 3 — ESTADO LIMPIO de las colecciones de trabajo de la pantalla.
    //   🧨 El ESQUEMA de las 23 colecciones ya lo sembró App.OnStart (01 §3.2),
    //      TODAS con Collect + Clear. Acá solo se VACÍAN: la pantalla se puede
    //      revisitar y no puede heredar hallazgos ni snapshots de la visita
    //      anterior. Sembrar el esquema dos veces era una segunda definición del
    //      tipo, y las dos se desincronizaban a la primera columna nueva.
    // ============================================================================
    Clear(colDetalleDVCon);
    Clear(colChecksDVCon);
    Clear(colKeysCon);
    Clear(colFallidasCon);
    Clear(colHallazgosCon);
    Clear(colDifTrxCon);        Clear(colDepRecSinEnlazarCon);
    Clear(colChqDecSinEnlazarCon); Clear(colChqManualCon); Clear(colDepDecSinEnlazarCon);

    // ============================================================================
    // BLOQUE 4 — SIN DEPÓSITO EN CURSO al entrar (estado vacío del mockup C4).
    //   Los tres centinelas van TIPADOS con Defaults(<ds>), nunca con Blank().
    // ============================================================================
    UpdateContext({
        ctxDepositoCon:     Defaults('CH-Tbl-DepositsRecountings'),
        ctxDepDecCursoCon:  Defaults('CH-Tbl-DeclaredDeposits'),
        ctxDepSelCon:       Defaults('CH-Tbl-DepositsRecountings'),
        ctxDepDecSelCon:    Defaults('CH-Tbl-DeclaredDeposits')
    });
    UpdateContext({ ctxHayDepositoCon: false, ctxMonedaCon: "NIO" });
    Clear(colDetalleConteoCon);
    Clear(colChecksCon);
    Clear(colAutoContadoraCon);        // 🧨 D-4 muerto: la contadora NO se hereda (01 §4.4)

    // ============================================================================
    // BLOQUE 5 — MODO SOLO LECTURA (§0.2) y ESTADO DE UI.
    //   Todos los diálogos CERRADOS al entrar: su estado PERSISTE entre visitas.
    // ============================================================================
    UpdateContext({
        ctxSoloLecturaCon:
            gvarTula.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'
    });
    UpdateContext({
        ctxDlgMonedaCon:       false,
        ctxDlgEliminarCon:     false,
        ctxDlgDescartarCon:    false,
        ctxDlgGuardandoCon:    false,
        ctxDlgTerminarOkCon:   false,
        ctxDlgTerminarDifCon:  false,
        ctxDlgSalirCon:        false,
        ctxGuardandoCon:       false,   // baja el Start de tmrGuardarCon
        ctxSalirTrasGuardarCon: false,
        ctxEliminandoCon:      false,   // discriminador de origen de btnEliminarDepositoCon
        ctxSec1Con: "", ctxSec2Con: "", ctxSec3Con: "",
        ctxSec4Con: "", ctxSec5Con: "", ctxSec6Con: "", ctxHtmlCon: ""
    });

    // ---- Espera / rechazo: se DERIVAN de la solicitud recuperada (Q4), no de una
    //      bandera suelta. Q4 ya no filtra por ApprovalStatus, así que acá se decide:
    //        · sin PK              ⇒ ninguna de las dos (no hay solicitud)
    //        · PK + status vacío   ⇒ ESPERANDO  ⇒ overlay C7 (§12.1)
    //        · PK + Rechazado      ⇒ RECHAZADA  ⇒ overlay C8 (§12.2), con StatusReason
    //        · PK + Aprobado       ⇒ ninguna: la tula ya está cerrada y la pantalla se
    //                                abre en solo lectura por su BagStatus (bloque 5).
    //      Las dos NUNCA son true a la vez: los predicados son excluyentes.
    UpdateContext({
        ctxEsperandoCon:
            !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest') &&
            IsBlank(gvarSolicitud.ApprovalStatus),
        ctxRechazadaCon:
            gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado
    });

    // ============================================================================
    // BLOQUE 6 — AVISO DURO si falta el detalle de sesión (contrato 01 §4.5).
    //   Nunca sigue en silencio: sin ID_Sesion la columna Auto queda en cero.
    // ============================================================================
    If(
        IsBlank(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'),
        Notify(
            "Esta tula no tiene detalle de sesión: la lectura automática no está disponible.",
            NotificationType.Warning
        )
    )
);

// ============================================================================
// BLOQUE 7 — RESET de pestañas y galerías (gotcha: el estado persiste).
// ============================================================================
Reset(tabDepositosCon);
Reset(tabDetalleCon);
Reset(galDepDeclaradosCon);
Reset(galDepRecontadosCon);
Reset(galBilletesCon);
Reset(galMonedasCon);
Reset(galChequesCon)
```

**Reglas de este `OnVisible`:**

1. **Cero `Set()` dentro de un `ForAll`** (regla **D-12**). No hay ningún `ForAll` acá.
2. **Cero `Refresh()`** (regla **D-5**). La v1 abría con **tres**.
3. **Cuatro consultas en la tanda base + dos condicionales** — todas delegables, todas
   PK contra PK. El detalle está en §1.2.
4. **`ctxMonedaCon` arranca en `"NIO"`** aunque no haya depósito: el `Default` de
   `ddMonedaCon` lo lee y sin valor el desplegable queda mudo.
5. 🧨 **Los bloques 1 a 6 viven DENTRO del `else` de la guarda del bloque 0.** Ni
   `Navigate()` ni `Select(<motor que navega>)` cortan la fórmula: sin el `else`, entrar sin
   `gvarTula` disparaba las seis consultas contra una PK vacía. El bloque 7 queda **fuera**
   a propósito: son `Reset` locales.
6. 🧨 **Acá NO se siembra ningún esquema de colección.** Eso es trabajo de `App.OnStart`
   (`01` §3.2), que siembra **las 23** con `Collect` + `Clear`. El bloque 3 solo **vacía**.
7. 🧨 **Cero `Navigate()` en este `OnVisible`** (`L-65`): la rama sin tula avisa y llama al
   **motor de salida** `btnIrTulasCon` (§1.1).

### 1.1 `btnIrTulasCon` — el motor de SALIDA (quinto motor)

🧨 **`Navigate()` está PROHIBIDO en un `OnVisible`**: el App Checker lo marca como
*"would automatically always navigate away"*, y una pantalla que navega sola desde su
`OnVisible` no se puede abrir en Studio para revisarla. La guarda del bloque 0 pasa al
**patrón motor** de la casa (`L-53`): el `OnVisible` **avisa e invoca**, el motor
**navega**. Ledger **`L-65`**.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnIrTulasCon` | Botón clásico — **motor 1 × 1** (`L-53`, `03` `D03-9`) | X `8` · **Y `767`** · Width `1` · Height `1` · **`Visible` `true`** · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · `Text` `""` · `TabIndex` `-1` · **`DisplayMode` `DisplayMode.Edit`** (sin condición) · `AccessibleLabel` `""` · `OnSelect` ver abajo |

```powerfx
// ====================== btnIrTulasCon.OnSelect ======================
Navigate(scrTulas)
```

> **Llamador (lista CERRADA, 1)**: `scrConteo.OnVisible`, bloque 0, rama *sin tula*. El
> `Notify("No hay una tula abierta.", …)` queda en el `OnVisible` —es el aviso de **esa**
> guarda—; el motor solo navega.

> 🧨 **Los OTROS cinco `Navigate(scrTulas)` de la pantalla NO pasan por el motor y está
> bien** (`btnVolverTulasEspCon` §12.1, `btnVolverTulasRecCon` §12.2, la salida de *Terminar
> tula* §11.3, `btnDlgDescartarSalirCon` §13.1 y el `OnTimerEnd` de `tmrPollCon` §12.3):
> son `OnSelect`/`OnTimerEnd`, **manejadores de comportamiento**, donde `Navigate()` es
> legítimo. La prohibición de `L-65` es **exclusiva del `OnVisible`**.

> **Cuelga de la PANTALLA, después de los overlays** y **`Visible: true`**, como los otros
> cuatro (§6.1): un control dentro de un contenedor oculto no se puede `Select()`.

### 1.2 🧨 Fallbacks de los espejos que usa este `OnVisible`

| Espejo | Qué usa el `OnVisible` | Fallback **sin backend** (`01` §2.2) |
|---|---|---|
| **E-2** `Bag` en `CH-Tbl-Checks` (🟡 la columna YA EXISTE) | `colChecksTulaCon` | `Clear(colChecksTulaCon); ForAll(colDepRecontadosCon As D, Collect(colChecksTulaCon, Filter('CH-Tbl-Checks', RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = D.'CH-Tbl-DepositsRecounting')))`. **Sale del `Concurrent`** y va **después** del bloque 1 (necesita `colDepRecontadosCon` ya cargada). N+1 acotado a los depósitos de la tula |
| **E-3** `DeclaredBagMirror` en `CH-Tbl-DeclaredChecks` (🔴 NO existe) | `colChequesDeclaradosCon` | `Clear(colChequesDeclaradosCon); ForAll(colDepDeclaradosCon As D, Collect(colChequesDeclaradosCon, Filter('CH-Tbl-DeclaredChecks', DeclaredDepositID.'CH-Tbl-DeclaredDeposits' = D.'CH-Tbl-DeclaredDeposits')))`. **Sale del `Concurrent`** del bloque 2 y va después del `ClearCollect(colDepDeclaradosCon, …)` |
| **E-4** `RecountingSessionDetailMirror` + `XMLReadingActiva` (🔴 NO existen) | *Leer contadora* (§8.4) | 🚫 **NO hay fallback delegable de un salto**: hay que agregar `CH-Tbl-XMLReadings` como datasource #17 y hacer **dos** consultas encadenadas (`01` §2.2). Se anota en el ledger de `00` |

> **Ninguno de los fallbacks trunca.** Preferimos N+1 acotado y correcto antes que una
> consulta rápida que devuelve 500 filas y calla.

---

## 2. Consultas a Dataverse de esta pantalla — inventario CERRADO

Todo lo demás corre sobre colecciones locales: **no hay delegación que romper** (`01` §2.3).
Estas son **las únicas** llamadas a la red.

| # | Fórmula | Dónde | Veredicto |
|---|---|---|---|
| Q1 | `Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')` + `SortByColumns` | §1 bloque 1 · recarga tras guardar/enlazar/eliminar | ✅ **Delegable** — PK contra PK (**D-1**) + orden explícito (**D-9**) |
| Q2 | `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')` | §1 bloque 1 · recarga tras guardar | ✅ **Delegable** — 🧨 **espejo E-2**, un salto |
| Q3 | `Filter('CH-Tbl-MoneyDenominations', Enabled = true)` + `SortByColumns` | §1 bloque 1 | ✅ **Delegable** — catálogo acotado (~20 filas, **D-10**). **Una sola vez por visita** |
| Q4 | `Filter('CH-Tbl-ApprovalRequests', 'Created By'.User = gvarUser.User && BagNumber = Trim(gvarTula.BagID) && (RequestType = "Tula con diferencia" \|\| RequestType = "Tula recontada sin declaracion"))` + `Sort` + `First` | §1 bloque 1 | ✅ **Delegable** — `'Created By'.User` es PK contra PK; `BagNumber` es texto propio (`L-18`); el `OR` de dos igualdades de string pliega. 🧨 **Acotada al tipo de esta pantalla y a esta tula** (`01` §5.8, regla 1) y **SIN `ApprovalStatus = Blank()`** (regla 4): trae la última, resuelta o no, y el estado lo deriva el bloque 5 |
| Q5 | `Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags')` + `SortByColumns` | §1 bloque 2 · recarga tras enlazar/desenlazar | ✅ **Delegable** — PK contra PK |
| Q6 | `Filter('CH-Tbl-DeclaredChecks', DeclaredBagMirror.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags')` | §1 bloque 2 | ✅ **Delegable** — 🧨 **espejo E-3** |
| Q6b | `Filter('CH-Tbl-CountedMoneyDetails', RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = <dep>.'CH-Tbl-DepositsRecounting')` | **Bloque C** (§6.3, paso 2) · §5.5 paso 3 | ✅ **Delegable** — PK contra PK. **Una consulta por depósito que se abre**, acotada a ~20 filas. No se cachea el desglose de todos los depósitos: el cajero trabaja de a uno |
| Q7 | `Patch('CH-Tbl-DepositsRecountings', <Defaults \| registro>, {…})` | §9.2 paso 2 | Escritura de 1 registro. `Name` es AUTONUMBER: **no se patchea** |
| Q8 | `Patch('CH-Tbl-CountedMoneyDetails', <tabla>, <tabla>)` + `Remove(…, <registro>)` | §9.2 pasos 3 y 5 | Receta `01` §7.5 forma B + paso de bajas. **Nunca `RemoveIf`** (**D-3**) |
| Q9 | `Patch('CH-Tbl-Checks', …)` / `Remove('CH-Tbl-Checks', <registro>)` | §9.2 pasos 4 y 5 | Ídem |
| Q10 | `Patch('CH-Tbl-DeclaredChecks', <registro>, { MatchedCheck: … })` | §9.2 paso 4 | 🧨 **`MatchedCheck`, NUNCA `DeclaredDepositID`** — es el fix de **D-6** |
| Q11 | `Patch('CH-Tbl-DeclaredDeposits', <registro>, { RelatedRecountedDeposit: … })` | §5.4 enlazar/desenlazar · §5.5 eliminar | Escritura de 1 registro, con el registro explícito |
| Q12 | `Remove('CH-Tbl-DepositsRecountings', <registro>)` | §5.5 | Registro explícito |
| Q13 | `Patch('CH-Tbl-Bags', gvarTula, { BagStatus: … })` | §11.2 · §11.4 · §12.3 | La app escribe **solo** `Asignada a cajero`, `Recontada` y `Pendiente … - recuento`. **Jamás los totales** (los recalcula el plugin) |
| Q14 | `Patch('CH-Tbl-RecountingSessionDetails', gvarSesionDetalle, { RecountingSessionDetailStatus: … })` | §11.2 · §11.4 · §12.3 | 1 registro |
| Q15 | `Patch('CH-Tbl-ApprovalRequests', Defaults(…), {…})` envuelto en `Set(gvarSolicitud, …)` | §11.4 | `01` §5.7. **`ApprovalStatus` NO se setea** (mata S-1) |
| Q16 | `'CH-Flow-ProcessXMLFiles'.Run(<ID_Sesion>, <Serial_Maquina>)` | §8.4 | Conector único de la app (`01` §1.5). Parámetros **POSICIONALES** |
| Q17 | `Filter('CH-Tbl-XMLReadingDetails', RecountingSessionDetailMirror.… && XMLReadingActiva = true)` → snapshot `colLecturasTmpCon` + `Distinct`/`Sum` **local** | §8.4 | ✅ **Delegable** — 🧨 **espejo E-4**. Mata **D-1**. 🚫 **Cero `GroupBy`** (`L-64`): la agregación es local |
| Q18 | `LookUp('CH-Tbl-ApprovalRequests', 'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest')` | §12.3 tick del polling | ✅ **Delegable** — por PK |
| Q19 | `Refresh('CH-Tbl-Bags')` | §12.3, ramas *Aprobado* y *Rechazado* **únicamente** | **D-5 forma 2**, la única permitida acá. **Cero `Refresh` en `OnTimerStart`** |

**Cero `Sum`/`CountRows` sobre datasource** (**D-2**) · **cero `Gallery.AllItems` como fuente
de un total** (**D-8**, `AllItems` solo aparece en el VOLCADO del bloque §6.2) ·
**cero `in` con subconsulta** (**D-6**) · **cero `Upper()`** (**D-7**) ·
**cero `Filter`/`LookUp` sobre datasource dentro del template de una galería** (**D-4**).

Todos los `Sum`/`CountRows`/`LookUp` de esta guía son sobre `colDepRecontadosCon`,
`colDepDeclaradosCon`, `colChecksTulaCon`, `colChequesDeclaradosCon`, `colDetalleConteoCon`,
`colChecksCon`, `colDenominacionesCon` o las 5 de hallazgos — **memoria del cliente**.

---

## 3. Header (Y `0`–`66`)

Los dos botones y el chip se anclan **desde la derecha** (`1346 = Parent.Width − 20`); el
título y la meta crecen desde la izquierda. Las tres etiquetas de la meta encadenan su `X`
contra la anterior (regla `01` §0.9): una Etiqueta clásica tiene **un solo `FontWeight` y un
solo `Color`**, y el mockup pinta *"Planilla"* en `ThemeInk2` regular, el número en
`ThemeInk` semibold y el cliente en `ThemeInk2` regular.

| Control | Tipo | Propiedades |
|---|---|---|
| `shpHeaderCon` | Rectángulo clásico | X `0` · Y `0` · Width `Parent.Width` · Height `66` · `Fill` `ThemeSurface` — banda de esquina viva: por eso Rectángulo y no Botón (`01` §0.3) |
| `shpHeaderLineaCon` | Rectángulo clásico | X `0` · Y `shpHeaderCon.Height` · Width `Parent.Width` · Height `1` · `Fill` `ThemeLine` |
| `btnVolverCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `20` · Y `17` · Width `32` · Height `32` · `Text` `""` · `OnSelect` **`Select(btnCerrarTulaCon)`** — una sola definición del "salir de la tula" (§13.1) |
| `icoVolverCon` | Icono clásico `Icon.Back` | X `btnVolverCon.X + 8` · Y `btnVolverCon.Y + 8` · Width `16` · Height `16` · `Color` `ThemeInk2` · **`OnSelect` `Select(btnVolverCon)`** — está ENCIMA del botón: sin esto el clic sobre el glifo no dispara nada · `TabIndex` `0` · `AccessibleLabel` `"Volver a las tulas"` |
| `lblTulaCon` | Etiqueta clásica | X `btnVolverCon.X + btnVolverCon.Width + 10` · Y `11` · Width `300` · Height `24` · **ESTILO-TITULO-GRANDE** · `Text` `"Tula " & Trim(gvarTula.BagID)` |
| `lblMetaPlanLabCon` | Etiqueta clásica | X `lblTulaCon.X` · Y `lblTulaCon.Y + lblTulaCon.Height + 3` · Width `48` · Height `16` · **ESTILO-META** · `Color` `ThemeInk2` · `Size` `8.63` · `Text` `"Planilla "` |
| `lblMetaPlanCon` | Etiqueta clásica | X `lblMetaPlanLabCon.X + lblMetaPlanLabCon.Width` · Y `lblMetaPlanLabCon.Y` · Width `6.5 * Len(Self.Text) + 4` · Height `16` · Size `8.63` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeInk` · `Text` `gvarTula.ReceiptIdMirror` |
| `lblMetaClienteCon` | Etiqueta clásica | X `lblMetaPlanCon.X + lblMetaPlanCon.Width` · Y `lblMetaPlanLabCon.Y` · Width `5.9 * Len(Self.Text) + 6` · Height `16` · Size `8.63` · `Color` `ThemeInk2` · `Text` ver abajo |
| `pilEstadoTulaCon` | Botón clásico | **receta 7.4**, variante **FUERA de template** · X `lblMetaClienteCon.X + lblMetaClienteCon.Width + 8` · Y `lblMetaPlanLabCon.Y + (lblMetaPlanLabCon.Height - Self.Height) / 2` · `Text`/`Fill`/`Color` = los tres `Switch` de 7.4 **con `gvarTula.BagStatus` en lugar de `ThisItem.BagStatus`** (sustitución mecánica y total) |
| `shpChipMesaCon` | Botón clásico | receta 7.1 · X `btnCerrarTulaCon.X - 14 - Self.Width` · Y `17` · Width `28 + lblChipMesaCon.Width` · Height `32` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `16` · `DisplayMode` `DisplayMode.View` |
| `lblChipMesaCon` | Etiqueta clásica | X `shpChipMesaCon.X + 14` · Y `shpChipMesaCon.Y` · Width `6.2 * Len(Self.Text) + 4` · Height `shpChipMesaCon.Height` · Size `9` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeInk` · `Text` `gvarMesa.Name` |
| `btnCerrarTulaCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnTerminarCon.X - 10 - Self.Width` · Y `17` · Width `84` · `Text` `"Cerrar"` · `OnSelect` **§13.1** |
| `btnTerminarCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `17` · Width `116` · `Text` `"Terminar tula"` · `DisplayMode` **EDITABLE** (§0.2) · `OnSelect` **§10.1** |

```powerfx
// lblMetaClienteCon.Text — el " · " es del mockup (.hMeta gap:8)
"   ·   " & gvarTula.ReceptionID.RemitentName
```

> `gvarTula.ReceptionID.RemitentName` es **navegación sobre un registro YA CARGADO** — la
> anotación del lookup que Dataverse devolvió con la fila. No cuesta un fetch y **no es
> D-1**: D-1 prohíbe el dot-walk **dentro de un `Filter` sobre un datasource**.

> 🧨 **Divergencia `D04-2` — el chip de mesa dice solo `Mesa 03`, sin `· Bóveda B`.**
> `CH-Tbl-CountingDesks` tiene exactamente cinco columnas (`Name`, `Cajero`,
> `MachineSerialNumber`, `Enable` y la PK): **no existe ninguna columna de bóveda ni de
> ubicación**. El mockup dibuja un dato que el modelo no tiene. El modelo manda.

> 🧨 **Divergencia `D04-3` — `←` y *Cerrar* hacen LO MISMO.** El mockup dibuja los dos y no
> define qué los diferencia. En la v2 los dos significan *"salir de la tula sin
> terminarla"*, y para que haya **una sola definición** `btnVolverCon.OnSelect` es
> literalmente `Select(btnCerrarTulaCon)`. Si mañana el proceso quiere que sean distintos,
> se separan en un solo lugar.

---

## 4. Franja de matrices (Y `78`–`178`)

Tres instancias de **`cmpMatrizMoneda`** (`01` §8.1). Todas sus propiedades internas las fija
el componente; acá solo van coordenadas y las **11 entradas** de cada una.

| Control | Tipo | X · Y · W · H |
|---|---|---|
| `cmpMatNIOCon` | Componente canvas `cmpMatrizMoneda` | `18` · `78` · `435` · `100` |
| `cmpMatUSDCon` | Componente canvas `cmpMatrizMoneda` | `cmpMatNIOCon.X + cmpMatNIOCon.Width + 12` · `78` · `435` · `100` |
| `cmpMatEURCon` | Componente canvas `cmpMatrizMoneda` | `cmpMatUSDCon.X + cmpMatUSDCon.Width + 12` · `78` · `435` · `100` |

**Entradas de `cmpMatNIOCon`** (las otras dos son la **sustitución mecánica** de la tabla
que sigue):

```powerfx
Moneda          = "NIO"
MonedaNombre    = "Córdobas"
Simbolo         = SimboloNIO
Titulo          = ""                                  // vacío ⇒ muestra Moneda + MonedaNombre
Card            = true                                // card de la franja: borde ThemeLine + Radius 11
ChequesRec      = CountRows(Filter(colChecksTulaCon,        CheckCurrency = 'CH-Ch-Currency'.NIO))
MontoChequesRec = Sum(Filter(colChecksTulaCon,              CheckCurrency = 'CH-Ch-Currency'.NIO), CheckAmount)
EfectivoRec     = Sum(Filter(colDepRecontadosCon,           Currency      = 'CH-Ch-Currency'.NIO), CashAmount)
ChequesDec      = CountRows(Filter(colChequesDeclaradosCon, CheckCurrency = 'CH-Ch-Currency'.NIO))
MontoChequesDec = Sum(Filter(colChequesDeclaradosCon,       CheckCurrency = 'CH-Ch-Currency'.NIO), CheckAmount)
EfectivoDec     = Sum(Filter(colDepDeclaradosCon,           Currency      = 'CH-Ch-Currency'.NIO), CashAmount)
```

| Instancia | `Moneda` | `MonedaNombre` | `Simbolo` | Literal del option set en los 6 filtros |
|---|---|---|---|---|
| `cmpMatNIOCon` | `"NIO"` | `"Córdobas"` | `SimboloNIO` | `'CH-Ch-Currency'.NIO` |
| `cmpMatUSDCon` | `"USD"` | `"Dólares"` | `SimboloUSD` | `'CH-Ch-Currency'.USD` |
| `cmpMatEURCon` | `"EUR"` | `"Euros"` | `SimboloEUR` | `'CH-Ch-Currency'.EUR` |

**Las tres diferencias NO se pasan: las calcula el componente** y salen como
`DifCheques` / `DifMonto` / `DifEfectivo` / `HayDiferencia` (`01` §8.1, divergencia `L-8`).
§10 y §11 las **leen**, no las recalculan.

> 🧨 **De dónde sale cada número, y por qué de ahí.**
>
> | Número | Origen | Motivo |
> |---|---|---|
> | Cheques recontados (cantidad y monto) | `colChecksTulaCon` (tabla `Checks`) | Es la **única** fuente que tiene la CANTIDAD de cheques; `DepositsRecountings` solo guarda importes |
> | Efectivo recontado | `colDepRecontadosCon.CashAmount` | Es el importe **persistido** del depósito. Coincide con `Σ Denominación × (Auto + Manual)` porque el guardado escribe exactamente eso (§9.2) |
> | Cheques declarados | `colChequesDeclaradosCon` | Ídem para el lado declarado |
> | Efectivo declarado | `colDepDeclaradosCon.CashAmount` | Ídem |
>
> **Las tres matrices de la franja muestran lo PERSISTIDO**, no lo que el cajero está
> tecleando. Lo que se está tecleando lo muestra la **cuarta** matriz, la del depósito en
> curso (§7.1). Es deliberado: la franja es el arqueo de la tula, y un arqueo no puede
> moverse con cada tecla. Divergencia **`D04-4`** vs. el mockup, que no distingue los dos
> planos.

---

## 5. Panel izquierdo — Depósitos (X `18`, W `704`)

### 5.1 Card, pestañas, botonera contextual y encabezados

| Control | Tipo | Propiedades |
|---|---|---|
| `shpCardDepCon` | Botón clásico | receta 7.1 · X `18` · Y `190` · Width `704` · Height `562` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `13` · `DisplayMode` `DisplayMode.View` |
| `lblPanelDepTitCon` | Etiqueta clásica | X `34` · Y `199` · Width `84` · Height `18` · **ESTILO-DETALLE-TITULO** · `Text` `"Depósitos"` |
| `lblPanelDepSubCon` | Etiqueta clásica | X `lblPanelDepTitCon.X + lblPanelDepTitCon.Width + 8` · Y `200` · Width `120` · Height `16` · **ESTILO-META-CHICA** · `Text` `"de la tula"` |
| `shpLineaPanelDepCon` | Rectángulo clásico | X `18` · Y `224` · Width `704` · Height `1` · `Fill` `ThemeLine` |
| `tabDepositosCon` | **Lista de pestañas moderna** | X `28` · Y `shpLineaPanelDepCon.Y + 1` · Width `240` · Height `34` · `Items` `["Declarado", "Recontado"]` · `DefaultSelectedItems` `["Declarado"]` · `OnChange` `UpdateContext({ctxDepSelCon: Defaults('CH-Tbl-DepositsRecountings'), ctxDepDecSelCon: Defaults('CH-Tbl-DeclaredDeposits')})` |
| `shpLineaTabDepCon` | Rectángulo clásico | X `18` · Y `tabDepositosCon.Y + tabDepositosCon.Height` · Width `704` · Height `1` · `Fill` `ThemeLine` |
| `btnEnlazarCon` | Botón clásico | **ESTILO-BTN-CHICO-ACC** · X `32` · Y `shpLineaTabDepCon.Y + 8` · Width `78` · Height `26` · `Text` `"Enlazar"` · `DisplayMode` ver §5.4 · `OnSelect` §5.4 |
| `btnDesenlazarCon` | Botón clásico | **ESTILO-BTN-CHICO** · X `btnEnlazarCon.X + btnEnlazarCon.Width + 7` · Y `btnEnlazarCon.Y` · Width `92` · Height `26` · `Text` `"Desenlazar"` · `DisplayMode` ver §5.4 · `OnSelect` §5.4 |
| `btnEliminarDepositoCon` | Botón clásico | **ESTILO-BTN-CHICO-DAN** · X `shpCardDepCon.X + shpCardDepCon.Width - 16 - Self.Width` · Y `btnEnlazarCon.Y` · Width `72` · Height `26` · `Text` `"Eliminar"` · **`Visible` `tabDepositosCon.Selected.Value = "Recontado"`** · `DisplayMode` ver §5.5 · `OnSelect` §5.5 |
| `lblOpsHintCon` | Etiqueta clásica | X `btnDesenlazarCon.X + btnDesenlazarCon.Width + 12` · Y `btnEnlazarCon.Y` · Width `shpCardDepCon.X + shpCardDepCon.Width - 16 - Self.X` · Height `26` · **ESTILO-META** · `Align` `Align.Right` · **`Visible` `tabDepositosCon.Selected.Value = "Declarado"`** · `Text` `"Seleccione un depósito declarado y uno recontado para enlazarlos."` |
| `shpLineaOpsDepCon` | Rectángulo clásico | X `18` · Y `btnEnlazarCon.Y + btnEnlazarCon.Height + 7` · Width `704` · Height `1` · `Fill` `ThemeLineSoft` |
| `shpHeadDepCon` | Botón clásico | receta 7.1 · X `18` · Y `shpLineaOpsDepCon.Y + 1` · Width `704` · Height `30` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderThickness` `0` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` |
| `shpLineaHeadDepCon` | Rectángulo clásico | X `18` · Y `shpHeadDepCon.Y + shpHeadDepCon.Height` · Width `704` · Height `1` · `Fill` `ThemeLine` |

### 5.2 Rejilla de columnas (medida contra `.dpHead/.dpRow` del mockup)

`grid-template-columns:1.25fr 62px 52px 1fr 1.05fr 1.05fr 96px` · `gap:8` · `padding:8px 16px`.
Ancho útil = `704 − 16` (padding izq.) `− 22` (padding der. + barra de scroll) = **666**.
Fijas `62 + 52 + 96 = 210`; 6 gaps × 8 = 48; el sobrante `408` se reparte entre `4.35 fr`
⇒ **1 fr ≈ 94**.

| Columna | Encabezado | X (absoluto) | Width | `Align` | X en el template (`− 18`) |
|---|---|---|---|---|---|
| Cuenta | `lblThCuentaCon` | `34` | `117` | `Align.Left` | `16` |
| Moneda | `lblThMonedaCon` | `159` | `62` | `Align.Left` | `141` |
| Chq | `lblThChqCon` | `229` | `52` | `Align.Right` | `211` |
| Monto cheques | `lblThMontoChqDepCon` | `289` | `94` | `Align.Right` | `271` |
| Efectivo | `lblThEfectivoCon` | `391` | `98` | `Align.Right` | `373` |
| Total | `lblThTotalDepCon` | `497` | `98` | `Align.Right` | `479` |
| Enlace | `lblThEnlaceCon` | `603` | `96` | `Align.Center` | `585` |

Los **7 encabezados** comparten: Tipo **Etiqueta clásica** · Y `shpHeadDepCon.Y + 8` ·
Height `14` · **ESTILO-HEADER-COL** (texto SIEMPRE en MAYÚSCULAS). Solo cambian `X`,
`Width`, `Align` y `Text`: `"CUENTA"`, `"MONEDA"`, `"CHQ"`, `"MONTO CHEQUES"`,
`"EFECTIVO"`, `"TOTAL"`, `"ENLACE"`.

### 5.3 Las dos galerías

Las dos comparten geometría y son **excluyentes por tab**. `TemplateSize` **`34`**
(receta 7.3, *tabla de depósitos*).

| Propiedad | `galDepDeclaradosCon` | `galDepRecontadosCon` |
|---|---|---|
| Tipo | Galería vertical en blanco | Galería vertical en blanco |
| X · Y | `18` · `shpLineaHeadDepCon.Y + 1` | ídem |
| Width | `704` | `704` |
| Height | `shpFootDepCon.Y - Self.Y` | ídem |
| `TemplateSize` | `34` | `34` |
| `TemplatePadding` | `0` | `0` |
| `ShowScrollbar` | `true` | `true` |
| `Visible` | `tabDepositosCon.Selected.Value = "Declarado"` | `tabDepositosCon.Selected.Value = "Recontado"` |
| `Items` | `colDepDeclaradosCon` | `colDepRecontadosCon` |
| `OnSelect` | ver abajo | ver abajo |

> **Regla D-4**: el `Items` es una **colección local**, cargada una vez en el `OnVisible`.
> Ninguna consulta vive dentro de un template en esta pantalla.

```powerfx
// galDepDeclaradosCon.OnSelect — TODA la lógica de la fila, en UN solo handler.
// Seleccionar un declarado NO toca el depósito en curso: solo alimenta Enlazar.
UpdateContext({ ctxDepDecSelCon: ThisItem })
```

```powerfx
// galDepRecontadosCon.OnSelect — TODA la lógica de la fila, en UN solo handler.
// 🧨 GUARDA ANTES DE PISAR: abrir otro depósito DESTRUYE lo que se está capturando.
//    Se valida ANTES de setear la selección; nunca se limpia y después se pregunta.
If(
    gvarDepositoSinGuardar,

    Notify(
        "Guarde o descarte el depósito en curso antes de abrir otro.",
        NotificationType.Warning
    ),

    UpdateContext({ ctxDepSelCon: ThisItem, ctxRegDepCon: ThisItem });
    Select(btnCargarDepCon)          // Bloque C — CARGAR DEPÓSITO EN CURSO (§6.3)
)
```

> 🧨 **UN solo handler por fila** (receta 7.3). La v1 copiaba el mismo `OnSelect` de
> ~90-120 líneas en **6-7 controles por fila** de estas dos galerías: ~1.400 líneas
> duplicadas que el motor indexa **por fila**. Acá **todos** los hijos del template hacen
> `Select(Parent)` y la lógica vive en la galería.

**Template de `galDepDeclaradosCon`** (`TemplateSize 34`, X del template = X absoluto − 18):

| Control | Tipo | Propiedades |
|---|---|---|
| `shpSelDepDecCon` | Botón clásico | **receta 7.3**, al **FONDO** del template · X `0` · Y `0` · Width `galDepDeclaradosCon.Width` · Height `galDepDeclaradosCon.TemplateSize` · `Fill` `If(ThisItem.'CH-Tbl-DeclaredDeposits' = ctxDepDecSelCon.'CH-Tbl-DeclaredDeposits', ThemeAccentSoft, Color.Transparent)` · `HoverFill` `If(ThisItem.'CH-Tbl-DeclaredDeposits' = ctxDepDecSelCon.'CH-Tbl-DeclaredDeposits', ThemeAccentSoft, ThemeSurface2)` · `PressedFill` `ThemeAccentSoft` · `Radius` `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` |
| `shpSelBarraDepDecCon` | Rectángulo clásico | X `0` · Y `0` · Width `3` · Height `galDepDeclaradosCon.TemplateSize` · `Fill` `ThemeAccent` · `Visible` `ThisItem.'CH-Tbl-DeclaredDeposits' = ctxDepDecSelCon.'CH-Tbl-DeclaredDeposits'` (es el `box-shadow: inset 3px 0` de `.dpSel`) |
| `lblCuentaDepDecCon` | Etiqueta clásica | X `16` · Y `0` · Width `117` · Height `34` · **ESTILO-CELDA-FUERTE** · `Text` `Text(ThisItem.AccountNumber)` · `OnSelect` `Select(Parent)` |
| `lblMonedaDepDecCon` | Etiqueta clásica | X `141` · Y `0` · Width `62` · Height `34` · **ESTILO-CELDA** · `Color` `ThemeInk2` · `Text` `Text(ThisItem.Currency)` · `OnSelect` `Select(Parent)` |
| `lblChqDepDecCon` | Etiqueta clásica | X `211` · Y `0` · Width `52` · Height `34` · **ESTILO-CELDA-NUM** · `Text` ver abajo · `OnSelect` `Select(Parent)` |
| `lblMontoChqDepDecCon` | Etiqueta clásica | X `271` · Y `0` · Width `94` · Height `34` · **ESTILO-CELDA-NUM** · `Text` `fxMonto(ThisItem.BACLocalCheckAmount + ThisItem.OtherLocalCheckAmount, fxSimbolo(Text(ThisItem.Currency)))` · `OnSelect` `Select(Parent)` |
| `lblEfectivoDepDecCon` | Etiqueta clásica | X `373` · Y `0` · Width `98` · Height `34` · **ESTILO-CELDA-NUM** · `Text` `fxMonto(ThisItem.CashAmount, fxSimbolo(Text(ThisItem.Currency)))` · `OnSelect` `Select(Parent)` |
| `lblTotalDepDecCon` | Etiqueta clásica | X `479` · Y `0` · Width `98` · Height `34` · **ESTILO-CELDA-NUM** con `FontWeight` `FontWeight.Semibold` · `Text` `fxMonto(ThisItem.Total, fxSimbolo(Text(ThisItem.Currency)))` · `OnSelect` `Select(Parent)` |
| `pilEnlaceDepDecCon` | Botón clásico | **receta 7.4, variante ENLACE** · X `585 + (96 - Self.Width) / 2` · Y `7` · Height `20` · `Text`/`Fill`/`Color` = los 3 `If` de 7.4 sobre **`ThisItem.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'`** · `DisplayMode` `DisplayMode.Edit` · **`OnSelect` `Select(Parent)`** (sin esto se come el clic de la fila) |
| `shpSepDepDecCon` | Rectángulo clásico | X `0` · Y `galDepDeclaradosCon.TemplateSize - 1` · Width `galDepDeclaradosCon.Width` · Height `1` · `Fill` `ThemeLineSoft` |

```powerfx
// lblChqDepDecCon.Text — cantidad de cheques DECLARADOS de este depósito declarado.
// Sobre colección local: cero red, cero delegación que romper.
Text(
    CountRows(
        Filter(
            colChequesDeclaradosCon,
            DeclaredDepositID.'CH-Tbl-DeclaredDeposits' = ThisItem.'CH-Tbl-DeclaredDeposits'
        )
    )
)
```

> ⚠️ **`OtherLocalCheckAmount` acá lleva la `l`.** En `CH-Tbl-DeclaredDeposits` la columna es
> `OtherLocalCheckAmount`; en `CH-Tbl-DepositsRecountings` es `OtherLocaCheckAmount`, **sin
> la `l`** (`01` §2.4). No es un typo de esta guía: son dos nombres distintos en el modelo y
> copiar uno en la otra tabla **no compila**.

**Template de `galDepRecontadosCon`** — es el **mismo template**, con una sustitución
mecánica y total. Los 10 controles quedan cubiertos por esta tabla:

| Control del recontado | Igual que | Sustituciones (todas, sin excepción) |
|---|---|---|
| `shpSelDepRecCon` | `shpSelDepDecCon` | `galDepDeclaradosCon` → `galDepRecontadosCon` · `ThisItem.'CH-Tbl-DeclaredDeposits' = ctxDepDecSelCon.'CH-Tbl-DeclaredDeposits'` → `ThisItem.'CH-Tbl-DepositsRecounting' = ctxDepSelCon.'CH-Tbl-DepositsRecounting'` |
| `shpSelBarraDepRecCon` | `shpSelBarraDepDecCon` | ídem |
| `lblCuentaDepRecCon` | `lblCuentaDepDecCon` | ninguna (`AccountNumber` se llama igual en las dos tablas) |
| `lblMonedaDepRecCon` | `lblMonedaDepDecCon` | ninguna |
| `lblChqDepRecCon` | `lblChqDepDecCon` | `Text` → `Text(CountRows(Filter(colChecksTulaCon, RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ThisItem.'CH-Tbl-DepositsRecounting')))` |
| `lblMontoChqDepRecCon` | `lblMontoChqDepDecCon` | `OtherLocalCheckAmount` → **`OtherLocaCheckAmount`** |
| `lblEfectivoDepRecCon` | `lblEfectivoDepDecCon` | ninguna |
| `lblTotalDepRecCon` | `lblTotalDepDecCon` | ninguna |
| `pilEnlaceDepRecCon` | `pilEnlaceDepDecCon` | `ThisItem.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'` → `ThisItem.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'` |
| `shpSepDepRecCon` | `shpSepDepDecCon` | `galDepDeclaradosCon` → `galDepRecontadosCon` |

**Estado vacío y pie:**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioDepCon` | Etiqueta clásica | X `34` · Y `galDepDeclaradosCon.Y + 60` · Width `672` · Height `60` · **ESTILO-VACIO** · `Wrap` `true` · `Text` ver abajo · `Visible` ver abajo |
| `shpFootDepCon` | Botón clásico | receta 7.1 · X `18` · **Y `shpCardDepCon.Y + shpCardDepCon.Height - Self.Height`** · Width `704` · **Height ver abajo (crece con la cantidad de monedas)** · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` **TopLeft `0` · TopRight `0` · BottomLeft `13` · BottomRight `13`** · `DisplayMode` `DisplayMode.View` |
| `lblFootDepLabCon` | Etiqueta clásica | X `34` · Y `shpFootDepCon.Y` · Width `117` · **Height `shpFootDepCon.Height`** · **ESTILO-FOOT** · `VerticalAlign` `VerticalAlign.Middle` · `Text` `If(tabDepositosCon.Selected.Value = "Declarado", "Total declarado", "Total recontado")` |
| `lblFootDepChqCon` | Etiqueta clásica | X `211` · Y `shpFootDepCon.Y` · Width `52` · **Height `shpFootDepCon.Height`** · **ESTILO-FOOT** · `Align` `Align.Right` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Middle` · `Text` ver abajo |
| `lblFootDepMontoChqCon` | Etiqueta clásica | X `271` · Y `shpFootDepCon.Y` · Width `94` · **Height `shpFootDepCon.Height`** · **ESTILO-FOOT** · `Align` `Align.Right` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Middle` · `Text` ver abajo |
| `lblFootDepEfectivoCon` | Etiqueta clásica | X `373` · Y `shpFootDepCon.Y` · Width `98` · **Height `shpFootDepCon.Height`** · **ESTILO-FOOT** · `Align` `Align.Right` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Middle` · `Text` ver abajo |
| `lblFootDepTotalCon` | Etiqueta clásica | X `479` · Y `shpFootDepCon.Y` · Width `98` · **Height `shpFootDepCon.Height`** · **ESTILO-FOOT** · `Align` `Align.Right` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Middle` · `Text` ver abajo |

```powerfx
// lblVacioDepCon.Text
If(
    tabDepositosCon.Selected.Value = "Declarado",
    If(
        IsBlank(gvarDeclaredBag.'CH-Tbl-DeclaredBags'),
        "Esta tula no tiene declaración del cliente. Al terminar, el recuento se envía a revisión del supervisor sin base de contraste.",
        "La declaración de esta tula no tiene depósitos cargados."
    ),
    "Todavía no hay depósitos recontados. Presione «Nuevo» en el panel derecho para capturar el primero."
)

// lblVacioDepCon.Visible
If(
    tabDepositosCon.Selected.Value = "Declarado",
    IsEmpty(colDepDeclaradosCon),
    IsEmpty(colDepRecontadosCon)
)
```

**🧨 El pie es POR MONEDA: una línea por moneda con su símbolo, y las monedas en cero no
se dibujan.** Nunca un solo número que sume NIO + USD + EUR. Las cuatro columnas numéricas
emiten **las mismas monedas, en el mismo orden**, así las líneas quedan alineadas entre sí.

```powerfx
// ============ BLOQUE F — MONEDAS DEL PIE ============
// Se pega TAL CUAL donde esta guía escriba <MONEDAS-PIE> (5 lugares: la Height del pie y
// los 4 Text numéricos). Devuelve UNA fila por moneda CON DATOS en la pestaña activa, en
// orden fijo NIO → USD → EUR, con sus tres importes ya calculados.
// 🧨 Todo corre sobre COLECCIONES LOCALES: cero consultas, cero delegación que romper.
Filter(
    ForAll(
        Table({ M: "NIO" }, { M: "USD" }, { M: "EUR" }) As Q,
        If(
            tabDepositosCon.Selected.Value = "Declarado",
            {
                Mon:  Q.M,
                Cant: CountRows(Filter(colChequesDeclaradosCon, Text(CheckCurrency) = Q.M)),
                Chq:  Coalesce(Sum(Filter(colChequesDeclaradosCon, Text(CheckCurrency) = Q.M), CheckAmount), 0),
                Efe:  Coalesce(Sum(Filter(colDepDeclaradosCon,     Text(Currency)     = Q.M), CashAmount),  0)
            },
            {
                Mon:  Q.M,
                Cant: CountRows(Filter(colChecksTulaCon, Text(CheckCurrency) = Q.M)),
                Chq:  Coalesce(Sum(Filter(colChecksTulaCon,    Text(CheckCurrency) = Q.M), CheckAmount), 0),
                Efe:  Coalesce(Sum(Filter(colDepRecontadosCon, Text(Currency)      = Q.M), CashAmount),  0)
            }
        )
    ),
    Cant > 0 || Chq > 0 || Efe > 0
)
```

```powerfx
// shpFootDepCon.Height — 22 px por moneda, mínimo los 32 del mockup
Max(32, 10 + 22 * CountRows(<MONEDAS-PIE>))

// lblFootDepChqCon.Text — cantidad de cheques, una línea por moneda
With(
    { P: <MONEDAS-PIE> },
    If(IsEmpty(P), "0", Concat(P As R, Text(R.Cant), Char(10)))
)

// lblFootDepMontoChqCon.Text — monto de cheques, CADA UNO CON SU SÍMBOLO
With(
    { P: <MONEDAS-PIE> },
    If(
        IsEmpty(P),
        fxMonto(0, SimboloNIO),
        Concat(P As R, fxMonto(R.Chq, fxSimbolo(R.Mon)), Char(10))
    )
)

// lblFootDepEfectivoCon.Text — efectivo
With(
    { P: <MONEDAS-PIE> },
    If(
        IsEmpty(P),
        fxMonto(0, SimboloNIO),
        Concat(P As R, fxMonto(R.Efe, fxSimbolo(R.Mon)), Char(10))
    )
)

// lblFootDepTotalCon.Text — total de la moneda: cheques + efectivo de ESA moneda
With(
    { P: <MONEDAS-PIE> },
    If(
        IsEmpty(P),
        fxMonto(0, SimboloNIO),
        Concat(P As R, fxMonto(R.Chq + R.Efe, fxSimbolo(R.Mon)), Char(10))
    )
)
```

> 🧨 **Divergencia `D04-5` — el pie NO sigue al mockup: va por moneda.** El mockup imprime
> **una sola línea** con `C$ 657,800.00` sobre una lista que incluye filas `USD` (verificado
> en `mockup-recounting.html`, las 8 apariciones de `.dpFoot`): es una suma de monedas
> distintas presentada con el símbolo del córdoba. **Un total que mezcla monedas es
> exactamente la clase de bug que esta reconstrucción existe para matar** (`00` §1.1, D-2).
> **Decisión tomada** (dueño del proceso, consolidación): una línea por moneda con su
> símbolo, omitiendo las monedas en cero, y **jamás** un número que sume monedas distintas.
> Con una tula de una sola moneda —el caso normal— el pie se ve **idéntico al mockup**: una
> línea de 32 px. Va al ledger de `00` §6 como divergencia declarada contra el mockup.

> **Cero consultas nuevas.** El Bloque F corre sobre `colChequesDeclaradosCon`,
> `colDepDeclaradosCon`, `colChecksTulaCon` y `colDepRecontadosCon`, que ya están en memoria
> (§1). El pie sigue sin tocar la red (**D-2**: cero `Sum`/`CountRows` sobre datasource).

### 5.4 Enlazar y desenlazar (mata **D-6** y **D-7**)

**`DisplayMode` de los dos botones** — el gate *estructural* vive acá; el gate de *negocio*
vive en el `OnSelect` y **habla**:

```powerfx
// btnEnlazarCon.DisplayMode
If(
    gvarIdentidadOk && !ctxSoloLecturaCon &&
    !IsBlank(ctxDepSelCon.'CH-Tbl-DepositsRecounting') &&
    !IsBlank(ctxDepDecSelCon.'CH-Tbl-DeclaredDeposits'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)

// btnDesenlazarCon.DisplayMode
If(
    gvarIdentidadOk && !ctxSoloLecturaCon &&
    !IsBlank(ctxDepSelCon.'CH-Tbl-DepositsRecounting') &&
    !IsBlank(ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

> **Por qué el gate NO incluye "misma moneda" ni "ninguno enlazado"**: si lo incluyera, el
> botón quedaría gris **sin decir por qué** y el cajero no sabría qué corregir. Esos dos
> chequeos viven en el `OnSelect` y avisan con el motivo exacto — mismo criterio que
> `reception-rebuild/02` §4 con *Cerrar lote*.

```powerfx
// ====================== btnEnlazarCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

If(
    // ---- 1. YA ENLAZADO: ninguno de los dos puede estarlo ----
    !IsBlank(ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits') ||
    !IsBlank(ctxDepDecSelCon.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'),
        Notify(
            "Uno de los dos depósitos ya está enlazado. Desenlácelo antes de volver a enlazar.",
            NotificationType.Error
        ),

    // ---- 2. MONEDAS DISTINTAS ----
    ctxDepSelCon.Currency <> ctxDepDecSelCon.Currency,
        Notify(
            "Las monedas no coinciden: el recontado es " & Text(ctxDepSelCon.Currency) &
            " y el declarado es " & Text(ctxDepDecSelCon.Currency) & ".",
            NotificationType.Error
        ),

    // ---- 3. TODO BIEN: dos escrituras, cada una con su IfError ----
    IfError(
        UpdateContext({
            ctxDepSelCon:
                Patch(
                    'CH-Tbl-DepositsRecountings',
                    ctxDepSelCon,
                    {
                        RelatedDeclaredDeposit: ctxDepDecSelCon,
                        AccountNumber:          ctxDepDecSelCon.AccountNumber,
                        TransactionNumber:      ctxDepDecSelCon.TransactionNo
                    }
                )
        }),
        Notify("No se pudo enlazar: " & FirstError.Message, NotificationType.Error)
    );

    If(
        // solo se escribe el otro lado si el primero entró
        !IsBlank(ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'),
        IfError(
            UpdateContext({
                ctxDepDecSelCon:
                    Patch(
                        'CH-Tbl-DeclaredDeposits',
                        ctxDepDecSelCon,
                        { RelatedRecountedDeposit: ctxDepSelCon }
                    )
            }),
            Notify(
                "El depósito quedó enlazado pero la declaración no se actualizó: " &
                FirstError.Message,
                NotificationType.Error
            )
        );

        // ---- 4. Recarga: Q1 + Q5, dos consultas delegables ----
        ClearCollect(
            colDepRecontadosCon,
            SortByColumns(
                Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
                "createdon", SortOrder.Ascending
            )
        );
        ClearCollect(
            colDepDeclaradosCon,
            SortByColumns(
                Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags'),
                "createdon", SortOrder.Ascending
            )
        );

        // ---- 5. Si el enlazado ES el depósito en curso, se recarga en pantalla ----
        //    🧨 El If tiene UNA sola rama: la recarga. El aviso va AFUERA y se emite
        //       una sola vez, porque se emite IGUAL en los dos casos.
        If(
            ctxDepSelCon.'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting',
            UpdateContext({ ctxRegDepCon: ctxDepSelCon });
            Select(btnCargarDepCon)      // Bloque C — CARGAR DEPÓSITO EN CURSO (§6.3)
        );
        Notify(
            "Depósito enlazado con la cuenta declarada " & Text(ctxDepDecSelCon.AccountNumber) & ".",
            NotificationType.Success
        )
    )
)
```

```powerfx
// ====================== btnDesenlazarCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

With(
    { Dec: LookUp(colDepDeclaradosCon, 'CH-Tbl-DeclaredDeposits' = ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits') },

    IfError(
        UpdateContext({
            ctxDepSelCon:
                Patch(
                    'CH-Tbl-DepositsRecountings',
                    ctxDepSelCon,
                    { RelatedDeclaredDeposit: Blank() }
                )
        }),
        Notify("No se pudo desenlazar: " & FirstError.Message, NotificationType.Error)
    );

    If(
        IsBlank(ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits') && !IsBlank(Dec.'CH-Tbl-DeclaredDeposits'),
        IfError(
            Patch('CH-Tbl-DeclaredDeposits', Dec, { RelatedRecountedDeposit: Blank() }),
            Notify(
                "El depósito quedó desenlazado pero la declaración no se actualizó: " & FirstError.Message,
                NotificationType.Error
            )
        )
    )
);

// ---- Recarga: Q1 + Q5, las mismas dos consultas delegables del paso 4 de btnEnlazarCon --
ClearCollect(
    colDepRecontadosCon,
    SortByColumns(
        Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
        "createdon", SortOrder.Ascending
    )
);
ClearCollect(
    colDepDeclaradosCon,
    SortByColumns(
        Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags'),
        "createdon", SortOrder.Ascending
    )
);

// ---- Si el desenlazado ES el depósito en curso, se recarga en pantalla ----
If(
    ctxDepSelCon.'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting',
    UpdateContext({ ctxRegDepCon: ctxDepSelCon });
    Select(btnCargarDepCon)          // Bloque C — CARGAR DEPÓSITO EN CURSO (§6.3)
);
Notify("Depósito desenlazado.", NotificationType.Success)
```

> 🧨 **`btnDesenlazarCon` solo se habilita con una declaración enlazada** (`DisplayMode`
> arriba), así que la recarga de `colDepDeclaradosCon` **siempre** tiene
> `gvarDeclaredBag` cargada: no hace falta el `If(!IsBlank(gvarDeclaredBag…))` que sí lleva
> el paso 5 de §5.5, donde se puede eliminar un depósito de una tula **sin** declaración.

**Las cuatro cosas que la v2 hace distinto (y por qué):**

1. 🧨 **Desenlazar NO borra cheques ni pone `AccountNumber` en blanco.** La v1 hacía
   `RemoveIf`/`Remove` de los cheques enlazados **dentro del desenlace**, y ponía
   `AccountNumber: Blank()`. En la v2 el desenlace toca **solo el enlace**: el número de
   cuenta es un dato del recuento (lo tecleó el cajero) y los cheques se desmarcan y se dan
   de baja en el **guardado**, con su paso de bajas (§9.2) y su `IfError`. Divergencia
   **`D04-6`**.
2. 🧨 **Nunca se patchea contra un declarado `Blank()`** — es el bug **D-7**. Las dos ramas
   están guardadas por `!IsBlank(<PK>)` **antes** de escribir.
3. **Cada escritura tiene su `IfError` y su mensaje propio.** Si la segunda falla, el
   mensaje lo dice: *"quedó enlazado pero la declaración no se actualizó"*. La UI **no
   miente**.
4. **`Dec` se resuelve con un `LookUp` sobre `colDepDeclaradosCon`** (colección local), no
   con `ctxDepSelCon.RelatedDeclaredDeposit` navegado — así se evita el fetch del registro
   relacionado y se trabaja sobre la copia que la pantalla ya tiene.

### 5.5 Eliminar un depósito recontado (confirmación C6-2)

🧨 **`btnEliminarDepositoCon` es UN SOLO control con DOS papeles**, discriminados por
`ctxEliminandoCon`: el **primer** clic (el del cajero, sobre la botonera) **abre el
diálogo**; el **segundo** —el que dispara el `OnConfirmar` de `cmpDlgEliminarCon` con
`Select()`— **ejecuta la eliminación**. El `OnSelect` de abajo es el **COMPLETO**: la guarda
del discriminador **y** los 6 pasos, en un solo bloque y en un solo lugar. §13.3 explica el
patrón y define el `OnConfirmar` que lo dispara, pero **no repite el código**. Receta 7.2,
regla 2: **confirmar ANTES de ejecutar**, y una sola definición de la eliminación.

```powerfx
// btnEliminarDepositoCon.DisplayMode
If(
    gvarIdentidadOk && !ctxSoloLecturaCon &&
    !IsBlank(ctxDepSelCon.'CH-Tbl-DepositsRecounting'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

```powerfx
// ====================== btnEliminarDepositoCon.OnSelect — COMPLETO ======================
// 🧨 UN control, DOS papeles, discriminados por ctxEliminandoCon (§13.3):
//      · 1er clic (el del cajero, sobre la botonera) ⇒ SOLO abre el diálogo.
//        Cero Clear/Remove/Patch en esa rama (receta 7.2, regla 2).
//      · 2º "clic" (el Select() del OnConfirmar, que ya puso ctxEliminandoCon: true)
//        ⇒ ejecuta los 6 pasos. El discriminador se CONSUME en la primera línea.
If(
    !ctxDlgEliminarCon && !ctxEliminandoCon,

    UpdateContext({ ctxDlgEliminarCon: true }),

    UpdateContext({ ctxEliminandoCon: false });
    Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2)
    Clear(colFallidasCon);

    // ---- 1. Soltar el enlace del declarado, si lo había (registro explícito, D-3) ----
    If(
        !IsBlank(ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'),
        IfError(
            Patch(
                'CH-Tbl-DeclaredDeposits',
                LookUp(colDepDeclaradosCon, 'CH-Tbl-DeclaredDeposits' = ctxDepSelCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'),
                { RelatedRecountedDeposit: Blank() }
            ),
            If(true,
                Collect(colFallidasCon, { Clave: "enlace", Detalle: "declarado", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ---- 2. Cheques del depósito: soltar MatchedCheck y borrar la fila de Checks ----
    //    🧨 MatchedCheck, NUNCA DeclaredDepositID (fix de D-6).
    //    🧨 Remove por REGISTRO sobre colección local, jamás RemoveIf(ds, predicado) (D-3).
    ForAll(
        Filter(
            colChecksTulaCon,
            RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ctxDepSelCon.'CH-Tbl-DepositsRecounting'
        ) As C,
        IfError(
            Patch(
                'CH-Tbl-DeclaredChecks',
                LookUp(colChequesDeclaradosCon, 'CH-Tbl-DeclaredChecks' = C.RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks'),
                { MatchedCheck: Blank() }
            ),
            If(true,
                Collect(colFallidasCon, { Clave: C.CheckNumber, Detalle: "MatchedCheck", Mensaje: FirstError.Message });
                Blank()
            )
        );
        IfError(
            Remove('CH-Tbl-Checks', C),
            If(true,
                Collect(colFallidasCon, { Clave: C.CheckNumber, Detalle: "cheque", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ---- 3. Desglose de denominaciones — patrón SNAPSHOT (regla D-14) ----
    //    🧨 NUNCA se itera un datasource mientras se lo borra: se congela en
    //       colBorrarTmpCon, se recorre la COLECCIÓN y se limpia.
    ClearCollect(
        colBorrarTmpCon,
        Filter(
            'CH-Tbl-CountedMoneyDetails',
            RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = ctxDepSelCon.'CH-Tbl-DepositsRecounting'
        )
    );
    ForAll(
        colBorrarTmpCon As M,
        IfError(
            Remove('CH-Tbl-CountedMoneyDetails', M),
            If(true,
                Collect(colFallidasCon, { Clave: Text(M.Denomination), Detalle: "denominación", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );
    Clear(colBorrarTmpCon);

    // ---- 4. El depósito ----
    IfError(
        Remove('CH-Tbl-DepositsRecountings', ctxDepSelCon),
        If(true,
            Collect(colFallidasCon, { Clave: Text(ctxDepSelCon.AccountNumber), Detalle: "depósito", Mensaje: FirstError.Message });
            Blank()
        )
    );

    // ---- 5. Recarga Q1 + Q2 + Q5 y limpieza de estado ----
    Concurrent(
        ClearCollect(
            colDepRecontadosCon,
            SortByColumns(
                Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
                "createdon", SortOrder.Ascending
            )
        ),
        ClearCollect(
            colChecksTulaCon,
            Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')
        )
    );
    If(
        !IsBlank(gvarDeclaredBag.'CH-Tbl-DeclaredBags'),
        ClearCollect(
            colDepDeclaradosCon,
            SortByColumns(
                Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.'CH-Tbl-DeclaredBags' = gvarDeclaredBag.'CH-Tbl-DeclaredBags'),
                "createdon", SortOrder.Ascending
            )
        )
    );

    // ---- 6. Si el eliminado ERA el depósito en curso: panel vacío (Bloque N, §6.4) ----
    //    🧨 La condición se evalúa ANTES de limpiar ctxDepSelCon, o compararía un
    //       centinela contra el depósito en curso y nunca daría true.
    //    🧨 `ctxHayDepositoCon: false` lo setea el LLAMADOR, no el motor (§6.4): acá el
    //       panel derecho queda en el estado vacío del mockup C4, sin depósito en curso.
    If(
        ctxDepSelCon.'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting',
        UpdateContext({ ctxHayDepositoCon: false });
        Select(btnDepNuevoCon)
    );
    UpdateContext({ ctxDepSelCon: Defaults('CH-Tbl-DepositsRecountings') });
    If(
        IsEmpty(colFallidasCon),
        Notify("Depósito eliminado.", NotificationType.Success),
        Notify(
            "El depósito se eliminó parcialmente: " & Concat(colFallidasCon, Detalle & " — " & Mensaje, " · "),
            NotificationType.Error
        )
    )
)
```

> 🧨 **`Filter('CH-Tbl-CountedMoneyDetails', RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = …)`
> es PK contra PK: delega** (D-1). Es la **única** consulta de esta pantalla a
> `CountedMoneyDetails` que no pasa por `colDetalleDVCon`, y solo corre en el borrado — no
> vale la pena cachear el desglose de un depósito que se está por eliminar.

> **El paso 3 no puede salir de `colDetalleDVCon`**: esa colección es del depósito **en
> curso**, y el que se elimina puede ser otro. Por eso —y solo por eso— consulta el
> datasource, y por eso necesita el snapshot.

> 🧨 **`ForAll` sobre un datasource + `Remove`/`Patch` del MISMO datasource está PROHIBIDO
> por el runtime** (regla **`D-14`** de `01` §2.3, ledger **`L-63`**): la fuente del `ForAll`
> es una consulta **viva** y borrar filas mientras se la recorre corta el lote a la mitad
> **sin error**. El patrón canónico es el **snapshot**: `ClearCollect` a una colección
> temporal → `ForAll` sobre la colección → `Clear`. `colBorrarTmpCon` es **local de este
> paso**: nace en la línea de arriba, muere en la de abajo y **NO** entra al inventario de
> `01` §4.2 ni se siembra en `App.OnStart`.
>
> **El paso 2 NO lleva snapshot y está bien**: itera `colChecksTulaCon`, que **ya es** una
> colección local, y borra sobre `'CH-Tbl-Checks'`. Fuente y destino son cosas distintas.

---

## 6. Los CUATRO motores 1×1 y sus bloques

La v1 copiaba el **mismo handler de negocio de 90-120 líneas en 6-7 controles de la MISMA
fila** de una galería, donde el motor lo indexa **por fila**. La v2 factoriza las cuatro
rutinas que se repiten —**volcar**, **cargar un depósito**, **vaciar el panel** y **rehacer
las grillas por moneda**— en **cuatro botones motor de 1 × 1**, invocados con `Select()`.
Cada rutina tiene **una sola definición escrita**, y **ningún** `OnSelect` de la pantalla la
duplica: la invoca.

### 6.1 Los cuatro motores — propiedades comunes

> **Son los cuatro de los Bloques V/C/N/M.** El **quinto** motor de la pantalla,
> `btnIrTulasCon` (§1.1), comparte **exactamente** estas propiedades con `X 8`, pero no es
> un Bloque: es el motor de **salida** que existe porque `Navigate()` está prohibido en un
> `OnVisible` (`L-65`).

Mismo patrón que los seis motores de `03` (`D03-9`) y misma regla del ledger `L-53`:
**`Select()` jamás apunta a un control invisible.** Son botones **de 1 × 1 transparentes**
apoyados en la última fila de píxeles de la pantalla, **fuera de todo overlay** (un control
dentro de un contenedor con `Visible false` no se puede `Select()`, y los `OnConfirmar` de
los diálogos los invocan justo cuando el diálogo se está cerrando).

| Propiedad (los 5 motores) | Valor |
|---|---|
| Tipo | **Botón clásico** |
| `Y` · `Width` · `Height` | `767` · `1` · `1` |
| `X` | `0` (`btnVolcarGrillaCon`) · `2` (`btnCargarDepCon`) · `4` (`btnDepNuevoCon`) · `6` (`btnRehacerMonedaCon`) · `8` (`btnIrTulasCon`, §1.1) |
| `Text` | `""` |
| `Visible` | **`true`** — nunca `false` (`L-53`) |
| `Fill` / `HoverFill` / `PressedFill` | `Color.Transparent` |
| `BorderThickness` | `0` |
| `TabIndex` | `-1` |
| `DisplayMode` | `DisplayMode.Edit` **sin condición** — un `Disabled` los volvería inertes **en silencio** y cortaría la cadena a la mitad. La guarda `gvarIdentidadOk && !ctxSoloLecturaCon` vive en los botones **visibles** que los invocan (§0.2) |
| `AccessibleLabel` | `""` |

| Motor | Bloque | Qué hace | Parámetro |
|---|---|---|---|
| `btnVolcarGrillaCon` | **V** (§6.2) | baja a las colecciones lo tecleado en las tres galerías | — |
| `btnCargarDepCon` | **C** (§6.3) | carga en el panel derecho un depósito **ya persistido** | **`ctxRegDepCon`** |
| `btnDepNuevoCon` | **N** (§6.4) | deja el panel derecho **en blanco** | **`ctxHayDepositoCon`** (lo setea el llamador) |
| `btnRehacerMonedaCon` | **M** (§6.5) | rehace las dos grillas con la moneda de `ddMonedaCon` | — |

> 🧨 **Los parámetros los setea SIEMPRE el llamador, en la instrucción INMEDIATAMENTE
> anterior al `Select()`.** `UpdateContext` tiene efecto **inmediato** para lo que sigue en
> la cadena de `;` (`02` §1). Ningún motor escribe su propio parámetro: si lo hiciera, habría
> dos lugares decidiendo lo mismo.
> · `btnCargarDepCon` lee `ctxRegDepCon` y **no lo escribe**.
> · `btnDepNuevoCon` **no toca `ctxHayDepositoCon`**: vale `true` cuando lo invoca *Nuevo*
>   (§9.1) y `false` cuando lo invocan el borrado del depósito en curso (§5.5 paso 6) o el
>   descarte de un depósito nuevo (§13.4).

### 6.2 Bloque **V** — VOLCADO = `btnVolcarGrillaCon.OnSelect` (receta `01` §7.10.A)

**Llamadores (lista CERRADA, 9)**, con `Select(btnVolcarGrillaCon)` como **primera**
instrucción: `btnGuardarDepositoCon` · `btnNuevoDepositoCon` · `btnDescartarCon` ·
`btnTerminarCon` · `btnLeerContadoraCon` · `btnEliminarDepositoCon` (en la rama que
**ejecuta**, §5.5 — la que solo abre el diálogo no lee las colecciones vivas) ·
`btnEnlazarCon` · `btnDesenlazarCon` · `btnAgregarChequeCon`.

```powerfx
// ============ BLOQUE V — VOLCADO — btnVolcarGrillaCon.OnSelect ============
// Los OnChange de fila NO patchean la colección (mata el foco). El valor lo baja esto.

ForAll(
    galBilletesCon.AllItems As Fila,
    Patch(
        colDetalleConteoCon,
        LookUp(colDetalleConteoCon, Denominacion = Fila.Denominacion && Tipo = Fila.Tipo),
        { Manual: Coalesce(Value(Fila.inpCantidadBilCon.Text), 0) }
    )
);

ForAll(
    galMonedasCon.AllItems As Fila,
    Patch(
        colDetalleConteoCon,
        LookUp(colDetalleConteoCon, Denominacion = Fila.Denominacion && Tipo = Fila.Tipo),
        { Manual: Coalesce(Value(Fila.inpCantidadMonCon.Text), 0) }
    )
);

ForAll(
    galChequesCon.AllItems As Fila,
    Patch(
        colChecksCon,
        LookUp(colChecksCon, LocalId = Fila.LocalId),
        {
            Seleccionado: If(Fila.EsManual, true, Fila.chkChequeCon.Value),
            Numero:       If(Fila.EsManual, Trim(Fila.inpNumChequeCon.Text),              Fila.Numero),
            Monto:        If(Fila.EsManual, Coalesce(Value(Fila.inpMontoChequeCon.Text), 0), Fila.Monto),
            Tipo:         If(Fila.EsManual, Fila.ddTipoChequeCon.Selected.Value,          Fila.Tipo)
        }
    )
)
```

**Reglas del volcado:**

1. **`As Fila` no es opcional**: sin el alias, `Fila.inpCantidadBilCon` no se resuelve y las
   columnas se ambiguan con las de la colección.
2. **Va PRIMERO, siempre.** Si agregás un botón que lea `colDetalleConteoCon` o
   `colChecksCon`, **lleva `Select(btnVolcarGrillaCon)`** y **entra en la lista de arriba y
   en la de `01` §7.10.A, en el mismo commit**.
3. **`gal….AllItems` solo devuelve las filas MATERIALIZADAS.** Una fila que nunca se pintó
   no está en `AllItems` — pero tampoco pudo editarse, así que su registro ya es el bueno.
   El `LookUp` por clave hace que el volcado sea un **merge**, no un reemplazo.
4. **`Coalesce(Value(…), 0)`**, nunca `Value(…)` pelado: un input vacío da *blank* y la suma
   entera se vuelve *blank*.
5. 🧨 **Las filas de cheque DECLARADO conservan su `Numero`, `Monto` y `Tipo`**: sus
   controles editables no existen (son etiquetas). Escribir `Fila.inpNumChequeCon.Text` en
   una fila declarada devolvería el texto de un control invisible.
6. **`AllItems` NO se usa para nada más** (regla **D-8**). La v1 lo usaba en 12 sumas y
   **des-virtualizaba** las tres galerías.
7. 🧨 **`Select()` ejecuta el `OnSelect` del destino ANTES de seguir con la cadena.** Es el
   supuesto sobre el que se apoya toda la app (los cinco motores de `03`, el
   `Select(btnAbandonarSes)` de `02` §9, el `Select(btnGuardarDepositoCon)` de §13.1). Si tu
   Studio se comportara distinto —el volcado no baja y el guardado escribe valores viejos—
   **es un bug del entorno: avisá antes de reescribir la pantalla**, no pegues el bloque
   inline en nueve botones.

### 6.3 Bloque **C** — CARGAR DEPÓSITO EN CURSO = `btnCargarDepCon.OnSelect`

**Llamadores (lista CERRADA, 5)**, cada uno con `UpdateContext({ ctxRegDepCon: <el
depósito> })` en la instrucción **inmediatamente anterior** al `Select(btnCargarDepCon)`:
`galDepRecontadosCon.OnSelect` (§5.3, `ThisItem`) · `btnEnlazarCon.OnSelect` paso 5 (§5.4,
`ctxDepSelCon`) · `btnDesenlazarCon.OnSelect` (§5.4, `ctxDepSelCon`) ·
`tmrGuardarCon.OnTimerEnd` paso 7 (§9.2, el depósito recién guardado, releído de
`colDepRecontadosCon`) · el `OnConfirmar` de `cmpDlgDescartarCon` (§13.4, el depósito
persistido que se estaba editando).

```powerfx
// ============ BLOQUE C — CARGAR DEPÓSITO EN CURSO — btnCargarDepCon.OnSelect ============
// ctxRegDepCon = registro de CH-Tbl-DepositsRecountings ya persistido, seteado por el
//                llamador en la instrucción inmediatamente anterior al Select().

UpdateContext({ ctxDepositoCon: ctxRegDepCon, ctxHayDepositoCon: true });
UpdateContext({
    ctxDepDecCursoCon:
        Coalesce(
            LookUp(
                colDepDeclaradosCon,
                'CH-Tbl-DeclaredDeposits' = ctxRegDepCon.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits'
            ),
            Defaults('CH-Tbl-DeclaredDeposits')
        )
});
UpdateContext({ ctxMonedaCon: Text(ctxRegDepCon.Currency) });

// ---- 1. 🧨 D-4 MUERTO: la lectura de la contadora NO se hereda entre depósitos ----
Clear(colAutoContadoraCon);

// ---- 2. Snapshot de lo PERSISTIDO de este depósito ----
//    colDetalleDVCon: UNA consulta delegable (Q6b), PK contra PK, ~20 filas.
//    colChecksDVCon:  filtro LOCAL sobre colChecksTulaCon — cero red.
ClearCollect(
    colDetalleDVCon,
    Filter(
        'CH-Tbl-CountedMoneyDetails',
        RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = ctxRegDepCon.'CH-Tbl-DepositsRecounting'
    )
);
ClearCollect(
    colChecksDVCon,
    Filter(
        colChecksTulaCon,
        RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ctxRegDepCon.'CH-Tbl-DepositsRecounting'
    )
);

// ---- 3. Grilla de denominaciones de la moneda, con Auto en 0 ----
//    Una fila por denominación habilitada de la moneda; Manual sale de lo persistido.
ClearCollect(
    colDetalleConteoCon,
    ForAll(
        Filter(colDenominacionesCon, Text(Currency) = ctxMonedaCon) As D,
        {
            Denominacion: D.Value,
            Tipo:         If(D.Type = 'Type (CH-Tbl-MoneyDenominations)'.Billete, "Billete", "Moneda"),
            Auto:         0,
            Manual:       Coalesce(LookUp(colDetalleDVCon, Denomination = D.Value).Quantity, 0),
            GUID:         LookUp(colDetalleDVCon, Denomination = D.Value).'CH-Tbl-CountedMoneyDetails'
        }
    )
);

// ---- 4. Cheques: declarados de la moneda NO tomados por OTRO depósito, + manuales ----
Clear(colChecksCon);
ForAll(
    Filter(
        colChequesDeclaradosCon,
        Text(CheckCurrency) = ctxMonedaCon
    ) As DC,
    With(
        {
            // fila de Checks que HOY representa a este cheque declarado (si existe)
            Rec: LookUp(colChecksTulaCon, RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks' = DC.'CH-Tbl-DeclaredChecks')
        },
        If(
            // se muestra si está libre o si ya pertenece a ESTE depósito
            IsBlank(Rec.'CH-Tbl-Checks') ||
            Rec.RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ctxRegDepCon.'CH-Tbl-DepositsRecounting',
            Collect(
                colChecksCon,
                {
                    LocalId:       Text(DC.'CH-Tbl-DeclaredChecks'),
                    GUID:          Rec.'CH-Tbl-Checks',
                    DeclaradoGUID: DC.'CH-Tbl-DeclaredChecks',
                    Numero:        DC.CheckNumber,
                    Tipo:          Text(DC.CheckType),
                    Moneda:        Text(DC.CheckCurrency),
                    Monto:         DC.CheckAmount,
                    Seleccionado:  !IsBlank(Rec.'CH-Tbl-Checks'),
                    EsManual:      false
                }
            )
        )
    )
);
ForAll(
    Filter(colChecksDVCon, IsBlank(RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks')) As MC,
    Collect(
        colChecksCon,
        {
            LocalId:       Text(MC.'CH-Tbl-Checks'),
            GUID:          MC.'CH-Tbl-Checks',
            DeclaradoGUID: Defaults('CH-Tbl-DeclaredChecks').'CH-Tbl-DeclaredChecks',
            Numero:        MC.CheckNumber,
            Tipo:          Text(MC.CheckTypes),
            Moneda:        Text(MC.CheckCurrency),
            Monto:         MC.CheckAmount,
            Seleccionado:  true,
            EsManual:      true
        }
    )
);

// ---- 5. Estado de edición y re-anclaje de los Default de las tres grillas ----
Set(gvarDepositoSinGuardar, false);
Reset(galBilletesCon); Reset(galMonedasCon); Reset(galChequesCon); Reset(ddMonedaCon)
```

**Reglas del Bloque C:**

1. 🧨 **`Clear(colAutoContadoraCon)` es el punto 1**, y la columna `Auto` de
   `colDetalleConteoCon` nace en `0`. Eso es **D-4 muerto** (`01` §4.4): en la v1
   `colDenominationTotals` era **por sesión** y nunca se limpiaba, así que el conteo de la
   máquina se heredaba en **cada** depósito y el efectivo se duplicaba o triplicaba.
2. 🧨 **`Manual` sale de lo persistido, `Auto` de la contadora — y lo que se guarda es
   `Auto + Manual`** (`01` §2.4, mata **D-5**). Al **recargar** un depósito ya guardado,
   todo lo persistido vuelve como `Manual`: el desglose es reconstruible aunque la máquina
   no vuelva a leerse.
3. **Los cheques declarados que otro depósito ya tomó NO aparecen.** Ese filtro es lo que
   impide que el mismo cheque entre en dos depósitos.
4. 🧨 **Los `GUID()` no aparecen por ningún lado.** Los `LocalId` salen de GUIDs **reales y
   estables** (`01` §4.3): `Text(<GUID de la DeclaredCheck>)` para los declarados y
   `Text(<GUID de la Check>)` para los manuales persistidos. `GUID()` **dentro de un
   `ForAll` repite** — por eso los cheques manuales nuevos se agregan **de a uno** (§8.5).
5. **`Reset` de las tres galerías**: re-ancla los `Default` de los inputs de fila al
   contenido real de la colección. Solo se hace acá y en el `OnVisible`, **nunca** en un
   `OnChange` (mata el foco, `01` §7.10.B).

### 6.4 Bloque **N** — DEPÓSITO NUEVO = `btnDepNuevoCon.OnSelect`

**Llamadores (lista CERRADA, 3)**, cada uno con su `ctxHayDepositoCon` seteado **antes** del
`Select(btnDepNuevoCon)`: `btnNuevoDepositoCon.OnSelect` (§9.1, **`true`**) ·
`btnEliminarDepositoCon.OnSelect` paso 6, solo si el eliminado era el en curso (§5.5,
**`false`**) · el `OnConfirmar` de `cmpDlgDescartarCon` cuando el depósito era nuevo (§13.4,
**`false`**).

```powerfx
// ============ BLOQUE N — DEPÓSITO NUEVO — btnDepNuevoCon.OnSelect ============
// 🧨 Este motor NO escribe ctxHayDepositoCon: lo setea el LLAMADOR en la instrucción
//    inmediatamente anterior al Select(). Con `true` el panel queda listo para capturar;
//    con `false` queda en el estado vacío del mockup C4.
UpdateContext({
    ctxDepositoCon:    Defaults('CH-Tbl-DepositsRecountings'),
    ctxDepDecCursoCon: Defaults('CH-Tbl-DeclaredDeposits')
});

Clear(colAutoContadoraCon);          // 🧨 D-4 (01 §4.4, punto 1)
Clear(colDetalleDVCon);
Clear(colChecksDVCon);

// Grilla de la moneda actual, TODO en cero y sin GUID
ClearCollect(
    colDetalleConteoCon,
    ForAll(
        Filter(colDenominacionesCon, Text(Currency) = ctxMonedaCon) As D,
        {
            Denominacion: D.Value,
            Tipo:         If(D.Type = 'Type (CH-Tbl-MoneyDenominations)'.Billete, "Billete", "Moneda"),
            Auto:         0,
            Manual:       0,
            GUID:         Defaults('CH-Tbl-CountedMoneyDetails').'CH-Tbl-CountedMoneyDetails'
        }
    )
);

// Cheques declarados LIBRES de la moneda (ninguno tomado por otro depósito)
Clear(colChecksCon);
ForAll(
    Filter(colChequesDeclaradosCon, Text(CheckCurrency) = ctxMonedaCon) As DC,
    If(
        IsBlank(LookUp(colChecksTulaCon, RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks' = DC.'CH-Tbl-DeclaredChecks').'CH-Tbl-Checks'),
        Collect(
            colChecksCon,
            {
                LocalId:       Text(DC.'CH-Tbl-DeclaredChecks'),
                GUID:          Defaults('CH-Tbl-Checks').'CH-Tbl-Checks',
                DeclaradoGUID: DC.'CH-Tbl-DeclaredChecks',
                Numero:        DC.CheckNumber,
                Tipo:          Text(DC.CheckType),
                Moneda:        Text(DC.CheckCurrency),
                Monto:         DC.CheckAmount,
                Seleccionado:  false,
                EsManual:      false
            }
        )
    )
);

Set(gvarDepositoSinGuardar, false);
Reset(galBilletesCon); Reset(galMonedasCon); Reset(galChequesCon); Reset(inpCuentaCon)
```

> **El Bloque N es el Bloque C con un `ctxRegDepCon` inexistente.** Es un motor aparte
> porque `Defaults(ds)` no tiene PK y los `LookUp` del Bloque C devolverían basura, no vacío.

> 🧨 **`ctxMonedaCon` NO se reinicia acá.** Si el cajero venía capturando USD, el depósito
> siguiente arranca en USD. Es lo que hace una mesa de conteo real: las tulas se separan por
> moneda. Divergencia **`D04-7`** (el mockup no dice nada del arranque).

### 6.5 Bloque **M** — REHACER GRILLAS POR MONEDA = `btnRehacerMonedaCon.OnSelect`

**Llamadores (lista CERRADA, 2):** `ddMonedaCon.OnChange`, rama *sin datos capturados*
(§7.2) · el `OnConfirmar` de `cmpDlgMonedaCon` (§13.2). Los dos llegan con la moneda nueva
**ya elegida en el desplegable**: el motor la lee de `ddMonedaCon.Selected.Value` y no
necesita parámetro.

```powerfx
// ====== BLOQUE M — REHACER GRILLAS POR MONEDA — btnRehacerMonedaCon.OnSelect ======
UpdateContext({ ctxMonedaCon: Left(ddMonedaCon.Selected.Value, 3) });

Clear(colAutoContadoraCon);          // 🧨 D-4 (01 §4.4, punto 3)

ClearCollect(
    colDetalleConteoCon,
    ForAll(
        Filter(colDenominacionesCon, Text(Currency) = ctxMonedaCon) As D,
        {
            Denominacion: D.Value,
            Tipo:         If(D.Type = 'Type (CH-Tbl-MoneyDenominations)'.Billete, "Billete", "Moneda"),
            Auto:         0,
            Manual:       0,
            GUID:         Defaults('CH-Tbl-CountedMoneyDetails').'CH-Tbl-CountedMoneyDetails'
        }
    )
);

// Los cheques marcados se DESMARCAN y la lista se rehace con la moneda nueva.
Clear(colChecksCon);
ForAll(
    Filter(colChequesDeclaradosCon, Text(CheckCurrency) = ctxMonedaCon) As DC,
    With(
        { Rec: LookUp(colChecksTulaCon, RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks' = DC.'CH-Tbl-DeclaredChecks') },
        If(
            IsBlank(Rec.'CH-Tbl-Checks') ||
            Rec.RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting',
            Collect(
                colChecksCon,
                {
                    LocalId: Text(DC.'CH-Tbl-DeclaredChecks'), GUID: Rec.'CH-Tbl-Checks',
                    DeclaradoGUID: DC.'CH-Tbl-DeclaredChecks', Numero: DC.CheckNumber,
                    Tipo: Text(DC.CheckType), Moneda: Text(DC.CheckCurrency),
                    Monto: DC.CheckAmount, Seleccionado: false, EsManual: false
                }
            )
        )
    )
);

Set(gvarDepositoSinGuardar, true);
Reset(galBilletesCon); Reset(galMonedasCon); Reset(galChequesCon)
```

> 🧨 **Los cheques manuales NO sobreviven al cambio de moneda**: nacieron en la moneda vieja
> y su `CheckCurrency` sería mentira. Los que ya estaban persistidos quedan en
> `colChecksDVCon` y el **paso de bajas** del guardado los borra (§9.2, paso 4). El diálogo
> `C6-1` lo dice literalmente antes de confirmar (§13.2).

> 🧨 **El motor NO limpia `colDetalleDVCon` ni `colChecksDVCon`, a propósito.** Son el
> snapshot de lo **persistido** del depósito en curso, que **no cambió**: el depósito sigue
> siendo el mismo, solo cambia la moneda con la que se lo captura. Ese snapshot es
> exactamente lo que el **paso de bajas** necesita para borrar las filas de la moneda vieja
> (`01` §7.5 regla 2, `D04-18`). Si lo limpiaras acá, las filas viejas quedarían colgadas del
> depósito **para siempre**.

---

## 7. Panel derecho — chrome del depósito en curso (X `736`, W `612`)

> 🧨 **REGLA DEL PANEL DERECHO.** Todo control **entre la línea del encabezado del panel y
> la botonera** lleva **`ctxHayDepositoCon &&`** como **primer** término de su `Visible`.
> Sin depósito en curso, el panel muestra el estado vacío del mockup `C4` y **nada más**.
> Los únicos exentos: `shpCardCurCon`, `lblPanelCurTitCon`, `lblPanelCurSubCon`,
> `shpLineaPanelCurCon`, `lblVacioCursoCon`, `shpFootAccionesCon` y los 4 controles de la
> botonera.

### 7.1 Card, encabezado, moneda, cuenta y matriz del depósito

| Control | Tipo | Propiedades |
|---|---|---|
| `shpCardCurCon` | Botón clásico | receta 7.1 · X `736` · Y `190` · Width `612` · Height `562` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `13` · `DisplayMode` `DisplayMode.View` |
| `lblPanelCurTitCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `199` · Width `130` · Height `18` · **ESTILO-DETALLE-TITULO** · `Text` `"Depósito en curso"` |
| `lblPanelCurSubCon` | Etiqueta clásica | X `lblPanelCurTitCon.X + lblPanelCurTitCon.Width + 8` · Y `200` · Width `pilSinGuardarCon.X - Self.X - 10` · Height `16` · **ESTILO-META-CHICA** · `Text` ver abajo |
| `pilSinGuardarCon` | Botón clásico | **receta 7.4** · X `shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width` · Y `197` · Height `22` · `Text` `"Sin guardar"` · `Fill`/`HoverFill`/`PressedFill` `ThemeInfoSoft` · `Color` `ThemeInfo` · `BorderThickness` `0` · **`Visible` `gvarDepositoSinGuardar`** |
| `shpLineaPanelCurCon` | Rectángulo clásico | X `736` · Y `224` · Width `612` · Height `1` · `Fill` `ThemeLine` |
| `lblLabMonedaCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `233` · Width `52` · Height `32` · **ESTILO-CAMPO-LAB** · `Text` `"Moneda"` · `Visible` `ctxHayDepositoCon` |
| `ddMonedaCon` | **Menú desplegable clásico** | X `lblLabMonedaCon.X + lblLabMonedaCon.Width + 9` · Y `233` · Width `150` · Height `32` · `Items` `["NIO — Córdobas", "USD — Dólares", "EUR — Euros"]` · `Default` ver abajo · `DisplayMode` **EDITABLE** · `Visible` `ctxHayDepositoCon` · `OnChange` §7.2 |
| `lblLabCuentaCon` | Etiqueta clásica | X `inpCuentaCon.X - 8 - Self.Width` · Y `233` · Width `50` · Height `32` · **ESTILO-CAMPO-LAB** · `Text` `"Cuenta"` · `Visible` `ctxHayDepositoCon` |
| `inpCuentaCon` | **Entrada de texto clásica** | X `shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width` · Y `233` · Width `160` · **Height `32`** (fuera de galería, `01` §6.1) · **`Format` `TextFormat.Number`** · `Align` `Align.Right` · `HintText` `"N° de cuenta"` · `Default` `If(IsBlank(ctxDepositoCon.AccountNumber), "", Text(ctxDepositoCon.AccountNumber))` · `DisplayMode` **EDITABLE** · `Visible` `ctxHayDepositoCon` · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `shpLineaCurRowCon` | Rectángulo clásico | X `736` · Y `273` · Width `612` · Height `1` · `Fill` `ThemeLineSoft` |
| `shpBandaMatDepCon` | Botón clásico | receta 7.1 · X `736` · Y `274` · Width `612` · Height `107` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderThickness` `0` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `ctxHayDepositoCon` |
| `cmpMatDepositoCon` | Componente canvas `cmpMatrizMoneda` | X `shpCardCurCon.X + 16` · Y **`278`** · Width `580` · **Height `100`** · `Visible` `ctxHayDepositoCon` · entradas abajo |
| `shpLineaMatDepCon` | Rectángulo clásico | X `736` · Y `shpBandaMatDepCon.Y + shpBandaMatDepCon.Height` · Width `612` · Height `1` · `Fill` `ThemeLineSoft` · `Visible` `ctxHayDepositoCon` |

```powerfx
// lblPanelCurSubCon.Text
If(
    !ctxHayDepositoCon,
    "ninguno",
    If(
        IsBlank(ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'),
        "sin enlazar a una cuenta declarada",
        "enlazado a la cuenta declarada " & Text(ctxDepDecCursoCon.AccountNumber)
    )
)
```

```powerfx
// ddMonedaCon.Default — el ítem completo, para que el desplegable muestre el nombre largo
Switch(
    ctxMonedaCon,
    "NIO", "NIO — Córdobas",
    "USD", "USD — Dólares",
    "EUR", "EUR — Euros",
    "NIO — Córdobas"
)
```

> 🧨 **`AccountNumber` es un ENTERO de hasta 9 dígitos** (`01` §2.4, divergencia `L-4`). El
> mockup dibuja `100-501-2345` (10 dígitos con guiones) y **el modelo no lo admite**: es
> `int`, máximo `999999999`. Por eso `Format: TextFormat.Number`, el `Patch` escribe
> `Value(inpCuentaCon.Text)` y el display es `Text(<registro>.AccountNumber)` **sin máscara**.

> 🧨 **El `Menú desplegable clásico` NO tiene `Radius`** (`01` §6.1, `L-13`): queda de
> esquina viva contra el `border-radius:7px` del mockup. El borde, el alto y el fondo sí se
> respetan.

> 🧨 **La banda sigue midiendo `107` y el componente ahora `100`, en `Y 278`.** El
> componente cierra su contenido en `95` (`01` §8.1): con `Height 92` la fila **Diferencia**
> del depósito en curso se **recortaba**. Con `278 + 100 = 378` entra completo dentro de la
> banda, que termina en `274 + 107 = 381`: quedan `4` de aire arriba y `3` abajo, y
> `shpLineaMatDepCon` —que encadena contra `shpBandaMatDepCon`, no contra el componente— **no
> se mueve**. `tabDetalleCon` (§7.3) tampoco: encadena contra `shpLineaMatDepCon.Y + 1`.

**Entradas de `cmpMatDepositoCon`** — es la variante con `Titulo`, y muestra **lo que se
está capturando ahora** (colecciones vivas), no lo persistido:

```powerfx
Moneda          = ctxMonedaCon
MonedaNombre    = ""
Simbolo         = fxSimbolo(ctxMonedaCon)
Titulo          = "Depósito " & If(IsBlank(Trim(inpCuentaCon.Text)), "sin cuenta", Trim(inpCuentaCon.Text))
Card            = false   // banda plana sobre ThemeSurface2: sin borde y sin radio (01 §8.1)

ChequesRec      = CountRows(Filter(colChecksCon, Seleccionado))
MontoChequesRec = Sum(Filter(colChecksCon, Seleccionado), Monto)
EfectivoRec     = Sum(colDetalleConteoCon, Denominacion * (Auto + Manual))

ChequesDec      = If(
                      IsBlank(ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'),
                      0,
                      CountRows(
                          Filter(
                              colChequesDeclaradosCon,
                              DeclaredDepositID.'CH-Tbl-DeclaredDeposits' = ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'
                          )
                      )
                  )
MontoChequesDec = If(
                      IsBlank(ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'),
                      0,
                      Sum(
                          Filter(
                              colChequesDeclaradosCon,
                              DeclaredDepositID.'CH-Tbl-DeclaredDeposits' = ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'
                          ),
                          CheckAmount
                      )
                  )
EfectivoDec     = If(IsBlank(ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'), 0, ctxDepDecCursoCon.CashAmount)
```

> 🧨 **Divergencia `D04-8` — los totales del panel derecho se refrescan con el VOLCADO, no
> con cada tecla.** El total de **la fila** sí es en vivo (lee el input de esa fila), pero la
> matriz del depósito, los pies de grilla y el *Total del depósito* salen de
> `colDetalleConteoCon` / `colChecksCon` — que es la regla **D-8** y la receta `01` §7.9. El
> valor tecleado baja a la colección cuando corre el **Bloque V** (al tocar cualquier botón).
> **Es el precio de no matarle el foco al cajero** (`01` §7.10.A): un `Patch` a la colección
> dentro de un `OnChange` muta el `Items` de la galería, la galería se re-renderiza y el
> cursor se va a mitad de la palabra. El mockup no distingue los dos planos.

### 7.2 Cambio de moneda — CONFIRMAR ANTES de rehacer las grillas

```powerfx
// ====================== ddMonedaCon.OnChange ======================
If(
    // el usuario "eligió" lo mismo que ya había: nada que hacer
    Left(Self.Selected.Value, 3) = ctxMonedaCon,
    false,

    // hay algo capturado ⇒ se PREGUNTA. Nada se borra todavía (7.2 regla 2).
    Sum(colDetalleConteoCon, Auto + Manual) > 0 || CountRows(Filter(colChecksCon, Seleccionado)) > 0,
        UpdateContext({ ctxDlgMonedaCon: true }),

    // no hay nada capturado ⇒ se rehace directo, sin preguntar
    Select(btnRehacerMonedaCon)      // Bloque M — REHACER GRILLAS POR MONEDA (§6.5)
)
```

> 🧨 **Si el cajero CANCELA el diálogo, el desplegable ya cambió visualmente.** Por eso el
> `OnCancelar` de `cmpDlgMonedaCon` hace **`Reset(ddMonedaCon)`**: el `Default` está anclado
> a `ctxMonedaCon`, que **no** se tocó. Sin ese `Reset`, la pantalla dice `USD` y la
> colección sigue en `NIO`.

### 7.3 Pestañas del detalle, banda de operaciones y encabezados

| Control | Tipo | Propiedades |
|---|---|---|
| `tabDetalleCon` | **Lista de pestañas moderna** | X `746` · Y `shpLineaMatDepCon.Y + 1` · Width `360` · Height `34` · `Items` `["Billetes", "Monedas", "Cheques"]` · `DefaultSelectedItems` `["Billetes"]` · `Visible` `ctxHayDepositoCon` |
| `shpLineaTabDetCon` | Rectángulo clásico | X `736` · Y `tabDetalleCon.Y + tabDetalleCon.Height` · Width `612` · Height `1` · `Fill` `ThemeLine` · `Visible` `ctxHayDepositoCon` |
| `lblHintDetalleCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `shpLineaTabDetCon.Y + 1` · Width `440` · Height `40` · **ESTILO-META** · `Visible` `ctxHayDepositoCon` · `Text` ver abajo |
| `btnLeerContadoraCon` | Botón clásico | **ESTILO-BTN-CHICO-ACC** · X `shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width` · Y `lblHintDetalleCon.Y + 7` · Width `122` · Height `26` · `Text` `"Leer contadora"` · `Align` `Align.Left` · `PaddingLeft` `28` · **`Visible` `ctxHayDepositoCon && tabDetalleCon.Selected.Value = "Billetes"`** · `DisplayMode` ver §8.4 · `OnSelect` §8.4 |
| `icoLeerContadoraCon` | Icono clásico `Icon.Reload` | X `btnLeerContadoraCon.X + 10` · Y `btnLeerContadoraCon.Y + 6` · Width `14` · Height `14` · `Color` `ThemeAccent` · `Visible` `btnLeerContadoraCon.Visible` · `DisplayMode` `btnLeerContadoraCon.DisplayMode` · **`OnSelect` `Select(btnLeerContadoraCon)`** · `TabIndex` `0` · `AccessibleLabel` `"Leer la máquina contadora"` |
| `btnAgregarChequeCon` | Botón clásico | **ESTILO-BTN-CHICO-ACC** · X `shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width` · Y `btnLeerContadoraCon.Y` · Width `132` · Height `26` · `Text` `"Agregar cheque"` · `Align` `Align.Left` · `PaddingLeft` `28` · **`Visible` `ctxHayDepositoCon && tabDetalleCon.Selected.Value = "Cheques"`** · `DisplayMode` **EDITABLE** · `OnSelect` §8.5 |
| `icoAgregarChequeCon` | Icono clásico `Icon.Add` | X `btnAgregarChequeCon.X + 10` · Y `btnAgregarChequeCon.Y + 6` · Width `14` · Height `14` · `Color` `ThemeAccent` · `Visible` `btnAgregarChequeCon.Visible` · `DisplayMode` `btnAgregarChequeCon.DisplayMode` · **`OnSelect` `Select(btnAgregarChequeCon)`** · `TabIndex` `0` · `AccessibleLabel` `"Agregar un cheque manual"` |
| `shpLineaOpsDetCon` | Rectángulo clásico | X `736` · Y `lblHintDetalleCon.Y + lblHintDetalleCon.Height` · Width `612` · Height `1` · `Fill` `ThemeLineSoft` · `Visible` `ctxHayDepositoCon` |
| `shpHeadDetCon` | Botón clásico | receta 7.1 · X `736` · Y `shpLineaOpsDetCon.Y + 1` · Width `612` · Height `26` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderThickness` `0` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `ctxHayDepositoCon` |
| `shpLineaHeadDetCon` | Rectángulo clásico | X `736` · Y `shpHeadDetCon.Y + shpHeadDetCon.Height` · Width `612` · Height `1` · `Fill` `ThemeLine` · `Visible` `ctxHayDepositoCon` |

```powerfx
// lblHintDetalleCon.Text — texto EXACTO del mockup, por pestaña
Switch(
    tabDetalleCon.Selected.Value,
    "Billetes", "Billetes · el conteo automático se reinicia en cada depósito",
    "Monedas",  "Monedas · sin lectura automática: la contadora no procesa moneda",
    "Cheques",  "Cheques · marque los declarados que entran en este depósito",
    ""
)
```

**Los 12 encabezados** comparten: Tipo **Etiqueta clásica** · Y `shpHeadDetCon.Y + 6` ·
Height `14` · **ESTILO-HEADER-COL** (MAYÚSCULAS) · `Visible` = `ctxHayDepositoCon &&
tabDetalleCon.Selected.Value = "<su pestaña>"`. Solo cambian `X`, `Width`, `Align` y `Text`:

| Pestaña | Control | X abs | Width | `Align` | `Text` | X en el template (`− 736`) |
|---|---|---|---|---|---|---|
| Billetes | `lblThDenBilCon` | `754` | `173` | `Align.Right` | `"DENOMINACIÓN"` | `18` |
| Billetes | `lblThAutoBilCon` | `937` | `74` | `Align.Center` | `"AUTO"` | `201` |
| Billetes | `lblThCantBilCon` | `1021` | `104` | `Align.Center` | `"CANTIDAD"` | `285` |
| Billetes | `lblThTotBilCon` | `1135` | `188` | `Align.Right` | `"TOTAL"` | `399` |
| Monedas | `lblThDenMonCon` | `754` | `214` | `Align.Right` | `"DENOMINACIÓN"` | `18` |
| Monedas | `lblThCantMonCon` | `978` | `104` | `Align.Center` | `"CANTIDAD"` | `242` |
| Monedas | `lblThTotMonCon` | `1092` | `232` | `Align.Right` | `"TOTAL"` | `356` |
| Cheques | `lblThNumChqCon` | `791` | `104` | `Align.Left` | `"N° CHEQUE"` | `55` |
| Cheques | `lblThTipoChqCon` | `904` | `99` | `Align.Left` | `"TIPO"` | `168` |
| Cheques | `lblThMonChqCon` | `1012` | `56` | `Align.Left` | `"MON."` | `276` |
| Cheques | `lblThMontoChqCon` | `1077` | `109` | `Align.Right` | `"MONTO"` | `341` |
| Cheques | `lblThOrigenChqCon` | `1195` | `92` | `Align.Center` | `"ORIGEN"` | `459` |

**Rejillas** (medidas contra `.denHead/.denRow` y `.chkHead/.chkRow` del mockup; ancho útil
de la card = `612 − 18 − 24` = **570** para denominaciones, `612 − 16 − 22` = **574** para
cheques):

| Grilla | `grid-template-columns` del mockup | Reparto |
|---|---|---|
| Billetes | `1.15fr 74px 104px 1.25fr` · gap `10` · padding `4px 18px` | `173 · 74 · 104 · 188` |
| Monedas | `1.15fr 104px 1.25fr` · gap `10` · padding `4px 18px` | `214 · 104 · 232` |
| Cheques | `30px 1.1fr 1.05fr 56px 1.15fr 92px 30px` · gap `9` · padding `5px 16px` | `30 · 104 · 99 · 56 · 109 · 92 · 30` |

Las dos columnas sin encabezado de la grilla de cheques (el tilde, X abs `752` W `30`, y la
papelera, X abs `1296` W `30`) no llevan etiqueta: el mockup tampoco.

---

## 8. Las tres grillas del detalle

### 8.1 Billetes (receta `01` §7.9)

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X · Y | `736` · `shpLineaHeadDetCon.Y + 1` |
| Width · Height | `612` · `shpFootBilCon.Y - Self.Y` |
| `TemplateSize` | **`32`** (receta 7.3, *grilla de denominaciones*) |
| `TemplatePadding` · `ShowScrollbar` | `0` · `true` |
| `Visible` | `ctxHayDepositoCon && tabDetalleCon.Selected.Value = "Billetes"` |
| `Items` | `SortByColumns(Filter(colDetalleConteoCon, Tipo = "Billete"), "Denominacion", SortOrder.Descending)` |
| `OnSelect` | `false` — **la fila NO navega.** Es la excepción explícita de la receta 7.3: los hijos del template son editables y tienen su propia acción, así que **ninguno lleva `Select(Parent)`** |

> **`SortByColumns` sobre una COLECCIÓN local usa el nombre de la columna de la colección**
> (`"Denominacion"`, sin acento, tal como se declara en `App.OnStart`) — no un nombre lógico
> de Dataverse. Acá no hay delegación en juego: son ~10 filas en memoria.

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDenBilCon` | Etiqueta clásica | X `18` · Y `0` · Width `173` · Height `32` · **ESTILO-DEN** · `Text` `fxSimbolo(ctxMonedaCon) & Text(ThisItem.Denominacion, "#,##0.##")` |
| `btnAutoBilCon` | Botón clásico | **ESTILO-AUTO** (`01` §6.4) · X `201 + (74 - Self.Width) / 2` · Y `5` · Width `54` · Height `22` · `Text` `Text(ThisItem.Auto)` · `Color` `If(ThisItem.Auto = 0, ThemeInk3, ThemeInk2)` · `DisplayMode` `DisplayMode.View` · **`TabIndex` `-1`** · `OnSelect` **vacío** |
| `inpCantidadBilCon` | **Entrada de texto clásica** | **ESTILO-QIN** (`01` §6.4) · X `285` · Y `3` · Width `104` · **Height `26`** (dentro de galería, `01` §6.1) · `Default` `Text(ThisItem.Manual)` · `Format` `TextFormat.Number` · **`TabIndex` `0`** · `BorderColor` `If(ThisItem.Manual > 0, ThemeAccent, ThemeLine)` · `BorderThickness` `If(ThisItem.Manual > 0, 2, 1)` · `DisplayMode` **EDITABLE** · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `lblTotFilaBilCon` | Etiqueta clásica | X `399` · Y `0` · Width `188` · Height `32` · **ESTILO-DEN-TOT** · `Text` ver abajo |
| `shpSepBilCon` | Rectángulo clásico | X `0` · Y `galBilletesCon.TemplateSize - 1` · Width `galBilletesCon.Width` · Height `1` · `Fill` `ThemeLineSoft` |

```powerfx
// lblTotFilaBilCon.Text — el ÚNICO total en vivo de la pantalla: lee el input de SU fila.
fxMonto(
    ThisItem.Denominacion * (ThisItem.Auto + Value(Coalesce(inpCantidadBilCon.Text, "0"))),
    fxSimbolo(ctxMonedaCon)
)
```

**Reglas del badge Auto** (`01` §7.9):

1. 🧨 **Auto es de SOLO LECTURA.** Es un **Botón clásico en `DisplayMode.View`**, no una
   entrada. El cajero escribe **solo** en *Cantidad*. La v1 mezclaba las dos y por eso nadie
   sabía qué parte del conteo venía de la máquina.
2. 🧨 **Auto se REINICIA por depósito** (Bloques C, N y M, §6). El encabezado se lo dice al
   cajero: *"Billetes · el conteo automático se reinicia en cada depósito"*.
3. **Lo persistido es `Auto + Manual`** (`01` §2.4, mata **D-5**). El pie muestra las dos
   sumas **por separado** para que el arqueo se reconstruya.
4. **`TabIndex -1` en el badge**: no debe entrar en el recorrido de teclado, o el TAB deja
   de bajar por *Cantidad*.
5. 🧨 **`TabIndex 0` en `inpCantidadBilCon`, jamás un positivo.** Dentro de un template cada
   fila **replica** el número y el orden global agrupa **por número**, no por fila: el TAB
   saltaría a la misma columna de todas las filas antes de bajar (`01` §7.10.B). Con `0` +
   orden geométrico, **TAB baja por la columna Cantidad**, que es lo que pide `00` §2.6.

**Pie de billetes:**

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFootBilCon` | Botón clásico | receta 7.1 · X `736` · Y `shpFootAccionesCon.Y - 32` · Width `612` · Height `32` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `galBilletesCon.Visible` |
| `lblFootBilLabCon` | Etiqueta clásica | X `754` · Y `shpFootBilCon.Y` · Width `173` · Height `32` · **ESTILO-FOOT** · `Text` `"Total billetes"` · `Visible` `galBilletesCon.Visible` |
| `lblFootBilAutoCon` | Etiqueta clásica | X `937` · Y `shpFootBilCon.Y` · Width `74` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Center` · `Text` `Text(Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Auto))` · `Visible` `galBilletesCon.Visible` |
| `lblFootBilManualCon` | Etiqueta clásica | X `1021` · Y `shpFootBilCon.Y` · Width `104` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Center` · `Text` `Text(Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Manual))` · `Visible` `galBilletesCon.Visible` |
| `lblFootBilTotalCon` | Etiqueta clásica | X `1135` · Y `shpFootBilCon.Y` · Width `188` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Right` · `Text` `fxMonto(Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Denominacion * (Auto + Manual)), fxSimbolo(ctxMonedaCon))` · `Visible` `galBilletesCon.Visible` |

> Los tres números salen de **`colDetalleConteoCon`**, jamás de `galBilletesCon.AllItems`
> (regla **D-8**). *Auto* y *Manual* van **separados**, como el mockup (`435 / 86`).

### 8.2 Monedas — la misma grilla **sin** columna Auto

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X · Y | `736` · `galBilletesCon.Y` |
| Width · Height | `612` · `shpFootMonCon.Y - Self.Y` |
| `TemplateSize` · `TemplatePadding` · `ShowScrollbar` | `32` · `0` · `true` |
| `Visible` | `ctxHayDepositoCon && tabDetalleCon.Selected.Value = "Monedas"` |
| `Items` | `SortByColumns(Filter(colDetalleConteoCon, Tipo = "Moneda"), "Denominacion", SortOrder.Descending)` |
| `OnSelect` | `false` |

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDenMonCon` | Etiqueta clásica | X `18` · Y `0` · Width `214` · Height `32` · **ESTILO-DEN** · `Text` `fxSimbolo(ctxMonedaCon) & Text(ThisItem.Denominacion, "#,##0.##")` |
| `inpCantidadMonCon` | Entrada de texto clásica | **ESTILO-QIN** · X `242` · Y `3` · Width `104` · Height `26` · `Default` `Text(ThisItem.Manual)` · `Format` `TextFormat.Number` · **`TabIndex` `0`** · `BorderColor` `If(ThisItem.Manual > 0, ThemeAccent, ThemeLine)` · `BorderThickness` `If(ThisItem.Manual > 0, 2, 1)` · `DisplayMode` **EDITABLE** · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `lblTotFilaMonCon` | Etiqueta clásica | X `356` · Y `0` · Width `232` · Height `32` · **ESTILO-DEN-TOT** · `Text` `fxMonto(ThisItem.Denominacion * Value(Coalesce(inpCantidadMonCon.Text, "0")), fxSimbolo(ctxMonedaCon))` |
| `shpSepMonCon` | Rectángulo clásico | X `0` · Y `galMonedasCon.TemplateSize - 1` · Width `galMonedasCon.Width` · Height `1` · `Fill` `ThemeLineSoft` |

> 🧨 **La pestaña *Monedas* NO tiene columna Auto y su fórmula de fila NO suma `ThisItem.Auto`**
> (`01` §7.9, regla 5): **la contadora no procesa moneda**. Si alguna vez la procesara, la
> columna `Auto` de esas filas quedaría en `0` y la suma seguiría siendo correcta — pero el
> aviso de la banda de operaciones estaría mintiendo, así que se cambia también.

**Pie de monedas + banda de efectivo total:**

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFootMonCon` | Botón clásico | receta 7.1 · X `736` · Y `shpBandaEfectivoCon.Y - 32` · Width `612` · Height `32` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `galMonedasCon.Visible` |
| `lblFootMonLabCon` | Etiqueta clásica | X `754` · Y `shpFootMonCon.Y` · Width `214` · Height `32` · **ESTILO-FOOT** · `Text` `"Total monedas"` · `Visible` `galMonedasCon.Visible` |
| `lblFootMonCantCon` | Etiqueta clásica | X `978` · Y `shpFootMonCon.Y` · Width `104` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Center` · `Text` `Text(Sum(Filter(colDetalleConteoCon, Tipo = "Moneda"), Manual))` · `Visible` `galMonedasCon.Visible` |
| `lblFootMonTotalCon` | Etiqueta clásica | X `1092` · Y `shpFootMonCon.Y` · Width `232` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Right` · `Text` `fxMonto(Sum(Filter(colDetalleConteoCon, Tipo = "Moneda"), Denominacion * Manual), fxSimbolo(ctxMonedaCon))` · `Visible` `galMonedasCon.Visible` |
| `shpBandaEfectivoCon` | Botón clásico | receta 7.1 · X `736` · Y `shpFootAccionesCon.Y - 36` · Width `612` · Height `36` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderThickness` `0` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `galMonedasCon.Visible` |
| `lblEfectivoDepositoCon` | Etiqueta clásica | X `754` · Y `shpBandaEfectivoCon.Y` · Width `570` · Height `36` · **ESTILO-META** · `Text` `"Efectivo del depósito (billetes + monedas): " & fxMonto(Sum(colDetalleConteoCon, Denominacion * (Auto + Manual)), fxSimbolo(ctxMonedaCon))` · `Visible` `galMonedasCon.Visible` |

### 8.3 Cheques

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X · Y | `736` · `galBilletesCon.Y` |
| Width · Height | `612` · `shpFootChqCon.Y - Self.Y` |
| `TemplateSize` | **`34`** (receta 7.3, *grilla de cheques*) |
| `TemplatePadding` · `ShowScrollbar` | `0` · `true` |
| `Visible` | `ctxHayDepositoCon && tabDetalleCon.Selected.Value = "Cheques"` |
| `Items` | `SortByColumns(colChecksCon, "EsManual", SortOrder.Ascending, "Numero", SortOrder.Ascending)` |
| `OnSelect` | `false` — los hijos editables tienen su propia acción (excepción de 7.3) |

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFilaChqCon` | Botón clásico | receta 7.1, **al FONDO del template** · X `0` · Y `0` · Width `galChequesCon.Width` · Height `galChequesCon.TemplateSize` · `Fill`/`HoverFill`/`PressedFill` **`If(ThisItem.EsManual, ThemeWarnSoft, Color.Transparent)`** (`ESTILO-FILA-MANUAL`, `01` §6.4) · `Radius` `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `TabIndex` `-1` |
| `chkChequeCon` | **Casilla clásica** | X `16 + 3` · Y `5` · Width `24` · Height `24` · `Text` `""` · `Default` `ThisItem.Seleccionado` · `Visible` `!ThisItem.EsManual` · `DisplayMode` **EDITABLE** · `TabIndex` `0` · 🧨 **`OnCheck` `Set(gvarDepositoSinGuardar, true)`** · 🧨 **`OnUncheck` `Set(gvarDepositoSinGuardar, true)`** — la Casilla clásica **NO expone `OnChange`** (`L-66`); las dos, idénticas, y **nada más** · `AccessibleLabel` `"Incluir el cheque " & ThisItem.Numero & " en este depósito"` |
| `lblGuionChqCon` | Etiqueta clásica | X `16` · Y `0` · Width `30` · Height `34` · **ESTILO-CELDA** · `Align` `Align.Center` · `Color` `ThemeInk3` · `Text` `"—"` · `Visible` `ThisItem.EsManual` |
| `lblNumChequeCon` | Etiqueta clásica | X `55` · Y `0` · Width `104` · Height `34` · **ESTILO-CELDA-FUERTE** · `Text` `ThisItem.Numero` · `Visible` `!ThisItem.EsManual` |
| `inpNumChequeCon` | Entrada de texto clásica | X `55` · Y `4` · Width `104` · Height `26` · `Size` `9` · **`Format` `TextFormat.Text`** · `Default` `ThisItem.Numero` · `HintText` `"N° cheque"` · `Visible` `ThisItem.EsManual` · `DisplayMode` **EDITABLE** · `TabIndex` `0` · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `lblTipoChequeCon` | Etiqueta clásica | X `168` · Y `0` · Width `99` · Height `34` · **ESTILO-CELDA** · `Color` `ThemeInk2` · `Text` `ThisItem.Tipo` · `Visible` `!ThisItem.EsManual` |
| `ddTipoChequeCon` | Menú desplegable clásico | X `168` · Y `4` · Width `99` · Height `26` · `Items` `["BAC Local", "Otros Local"]` · `Default` `ThisItem.Tipo` · `Size` `8.25` · `Visible` `ThisItem.EsManual` · `DisplayMode` **EDITABLE** · `TabIndex` `0` · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `lblMonChequeCon` | Etiqueta clásica | X `276` · Y `0` · Width `56` · Height `34` · **ESTILO-CELDA** · `Color` `ThemeInk2` · `Text` `Trim(fxSimbolo(ThisItem.Moneda))` |
| `lblMontoChequeCon` | Etiqueta clásica | X `341` · Y `0` · Width `109` · Height `34` · **ESTILO-CELDA-NUM** con `FontWeight` `FontWeight.Semibold` · `Text` `Text(ThisItem.Monto, FmtMoney)` · `Visible` `!ThisItem.EsManual` |
| `inpMontoChequeCon` | Entrada de texto clásica | X `341` · Y `4` · Width `109` · Height `26` · `Size` `9` · **`Format` `TextFormat.Number`** · `Align` `Align.Right` · `Default` `Text(ThisItem.Monto, "0.00")` · `Visible` `ThisItem.EsManual` · `DisplayMode` **EDITABLE** · `TabIndex` `0` · `BorderColor` `If(ThisItem.Monto > 0, ThemeLine, ThemeDanger)` · `BorderThickness` `If(ThisItem.Monto > 0, 1, 2)` · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `pilOrigenChequeCon` | Botón clásico | **receta 7.4, variante ORIGEN** · X `459 + (92 - Self.Width) / 2` · Y `7` · Height `20` · `Text` `If(ThisItem.EsManual, "Manual", "Declarado")` · `Fill`/`HoverFill`/`PressedFill` `If(ThisItem.EsManual, ThemeWarnSoft2, ThemeInfoSoft)` · `Color` `If(ThisItem.EsManual, ThemeWarn, ThemeInfo)` · `Size` `7.5` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoBorrarChequeCon` | Icono clásico `Icon.Trash` | X `560 + 7` · Y `9` · Width `16` · Height `16` · `Color` `ThemeInk3` · `HoverColor` `ThemeDanger` · **`Visible` `ThisItem.EsManual && gvarIdentidadOk && !ctxSoloLecturaCon`** · **`TabIndex` `-1`** · `OnSelect` ver abajo · `AccessibleLabel` `"Eliminar el cheque manual " & ThisItem.Numero` |
| `shpSepChqCon` | Rectángulo clásico | X `0` · Y `galChequesCon.TemplateSize - 1` · Width `galChequesCon.Width` · Height `1` · `Fill` `ThemeLineSoft` |

```powerfx
// icoBorrarChequeCon.OnSelect — el cheque manual se saca de la colección local, y nada más.
Remove(colChecksCon, LookUp(colChecksCon, LocalId = ThisItem.LocalId));
Set(gvarDepositoSinGuardar, true)
```

> 🧨 **NO existe ninguna colección de "cheques borrados", y no hace falta.** El **paso 4**
> del guardado (§9.2) da de baja **todo lo que está en `colChecksDVCon` —el snapshot de lo
> persistido del depósito— y ya NO está seleccionado en `colChecksCon`**:
>
> ```powerfx
> Filter(colChecksDVCon As X, IsBlank(LookUp(colChecksCon, GUID = X.'CH-Tbl-Checks' && Seleccionado)))
> ```
>
> Un cheque manual persistido que la papelera acaba de sacar de `colChecksCon` **no tiene
> fila que lo empareje** ⇒ el predicado lo alcanza y el `Remove('CH-Tbl-Checks', X)` lo
> borra. El mismo predicado cubre los otros dos casos de baja (declarado destildado y fila
> que desapareció por cambio de moneda) — es la regla 2 de la receta `01` §7.5, escrita
> **desde el snapshot hacia la colección viva**. Una colección paralela era un **cuarto**
> lugar donde vivía la misma verdad, con sus tres `Clear` para desincronizar. Ver §14.5.

> 🧨 **La papelera está FUERA del recorrido de TAB (`TabIndex -1`), a propósito** (`01`
> §7.10.B): un TAB de más y un Enter borrarían una fila. Se toca con el mouse.

> 🧨 **Los cheques manuales nacen `Seleccionado: true` y NO muestran casilla.** Un cheque que
> el cajero agregó a mano **está** en el depósito por definición; su "selección" no es una
> decisión. Por eso el volcado (§6.2) fuerza `Seleccionado: If(Fila.EsManual, true, …)` y
> todas las sumas usan un único predicado, `Filter(colChecksCon, Seleccionado)`.
> Divergencia **`D04-9`** (el mockup dibuja un `—` en esa celda, que es exactamente
> `lblGuionChqCon`).

> 🧨 **La Casilla clásica NO tiene `OnChange`: tiene `OnCheck` y `OnUncheck`** (`L-66`).
> Los otros seis controles editables de fila (las dos Entradas de cantidad, `inpNumChequeCon`,
> `ddTipoChequeCon`, `inpMontoChequeCon`, `inpCuentaCon`) sí llevan `OnChange`; `chkChequeCon`
> es la **única** excepción y lleva **las dos** propiedades con **la misma** fórmula —
> `Set(gvarDepositoSinGuardar, true)`— porque tildar y destildar ensucian el depósito por
> igual. Si escribís la marca de sucio en una sola de las dos, destildar un cheque no marca
> el depósito y el cajero sale sin que la guarda de salida (§13.1) le avise. **Cero `Patch`
> acá**: el valor lo baja el **VOLCADO** (§6.2), como en toda la pantalla (`01` §7.10.A).

**Pie de cheques:**

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFootChqCon` | Botón clásico | receta 7.1 · X `736` · Y `shpFootAccionesCon.Y - 32` · Width `612` · Height `32` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` · `Visible` `galChequesCon.Visible` |
| `lblFootChqLabCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `shpFootChqCon.Y` · Width `300` · Height `32` · **ESTILO-FOOT** · `Text` `"Cheques del depósito: " & Text(CountRows(Filter(colChecksCon, Seleccionado)))` · `Visible` `galChequesCon.Visible` |
| `lblFootChqTotalCon` | Etiqueta clásica | X `1062` · Y `shpFootChqCon.Y` · Width `262` · Height `32` · **ESTILO-FOOT** · `Align` `Align.Right` · `Text` `fxMonto(Sum(Filter(colChecksCon, Seleccionado), Monto), fxSimbolo(ctxMonedaCon))` · `Visible` `galChequesCon.Visible` |

**Estados vacíos del panel derecho:**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioDetalleCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `galBilletesCon.Y + 40` · Width `580` · Height `60` · **ESTILO-VACIO** · `Wrap` `true` · `Text` ver abajo · `Visible` ver abajo |
| `lblVacioCursoCon` | Etiqueta clásica | X `shpCardCurCon.X + 16` · Y `330` · Width `580` · Height `120` · **ESTILO-VACIO** · `Wrap` `true` · **`Visible` `!ctxHayDepositoCon`** · `Text` ver abajo |

```powerfx
// lblVacioDetalleCon.Text
Switch(
    tabDetalleCon.Selected.Value,
    "Billetes", "No hay billetes habilitados para " & ctxMonedaCon & ". Avise a soporte: falta el catálogo de denominaciones.",
    "Monedas",  "No hay monedas habilitadas para " & ctxMonedaCon & ".",
    "Cheques",  "No hay cheques declarados disponibles en " & ctxMonedaCon & ". Puede agregar uno con «Agregar cheque».",
    ""
)

// lblVacioDetalleCon.Visible
ctxHayDepositoCon &&
Switch(
    tabDetalleCon.Selected.Value,
    "Billetes", IsEmpty(Filter(colDetalleConteoCon, Tipo = "Billete")),
    "Monedas",  IsEmpty(Filter(colDetalleConteoCon, Tipo = "Moneda")),
    "Cheques",  IsEmpty(colChecksCon),
    false
)

// lblVacioCursoCon.Text — el texto del mockup C4, con una poda declarada (ver la nota)
"No hay un depósito en curso" & Char(10) & Char(10) &
"Los " & Text(CountRows(colDepRecontadosCon)) & " depósitos de la tula ya fueron guardados." & Char(10) &
"Presione «Nuevo» para capturar otro, o «Terminar tula» para cerrarla."
```

> 🧨 **Se poda *"y enlazados"*.** El mockup escribe *"ya fueron guardados **y enlazados**"*,
> y eso es falso en el caso general: **no todos los depósitos se enlazan** —el enlace es
> opcional y su ausencia es un hallazgo legítimo (`H5`, §10.1) que el cajero puede decidir
> mandar a revisión—. Prometer un enlace que puede no existir le haría dudar de un conteo
> correcto. El resto del texto sí es literal del mockup.

> Toda etiqueta con `Wrap true` lleva **`Height` explícito** (`01` §6.2): `60` para
> `lblVacioDetalleCon` (3 líneas de `9.38`), `120` para `lblVacioCursoCon` (4 líneas + dos
> saltos). Si el texto se corta en Studio, se sube el alto — **no** se quita el `Wrap`.

### 8.4 Leer contadora (receta `01` §7.11 — mata **D-1** y **D-4**)

```powerfx
// btnLeerContadoraCon.DisplayMode
If(
    gvarIdentidadOk && !ctxSoloLecturaCon &&
    !IsBlank(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail') &&
    !IsBlank(gvarMesa.MachineSerialNumber),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

```powerfx
// ====================== btnLeerContadoraCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

If(
    IsBlank(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail') ||
    IsBlank(gvarMesa.MachineSerialNumber),

    Notify(
        "Esta tula no tiene detalle de sesión o la mesa no tiene contadora configurada: la lectura automática no está disponible.",
        NotificationType.Warning
    ),

    // ----- 1. Disparar el flujo (parámetros POSICIONALES, en este orden) -----
    IfError(
        UpdateContext({
            ctxRespuestaFlujoCon:
                'CH-Flow-ProcessXMLFiles'.Run(
                    Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'),   // ID_Sesion
                    gvarMesa.MachineSerialNumber                                // Serial_Maquina
                )
        }),
        If(true,
            Notify("No se pudo contactar a la contadora: " & FirstError.Message, NotificationType.Error);
            UpdateContext({ ctxRespuestaFlujoCon: Blank() });
            Blank()
        )
    );

    If(
        ctxRespuestaFlujoCon.status <> "Completado",

        Notify("El reconteo de la máquina no pudo ser leído correctamente.", NotificationType.Error),

        // ----- 2. Snapshot local + agregación (🧨 espejo E-4; forma validada, L-64) -----
        //    NADA de GroupBy/AddColumns: Distinct + Sum sobre la colección temporal,
        //    con proyección EXPLÍCITA a { Denominacion, Cantidad } (01 §7.11).
        ClearCollect(
            colLecturasTmpCon,
            Filter(
                'CH-Tbl-XMLReadingDetails',
                RecountingSessionDetailMirror.'CH-Tbl-RecountingSessionDetail'
                    = gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'
                && XMLReadingActiva = true
            )
        );
        ClearCollect(
            colAutoContadoraCon,
            ForAll(
                Distinct(colLecturasTmpCon, Denomination) As D,
                {
                    Denominacion: D.Value,
                    Cantidad: Sum(
                        Filter(colLecturasTmpCon, Denomination = D.Value),
                        Quantity
                    )
                }
            )
        );
        Clear(colLecturasTmpCon);

        // ----- 3. Volcar Auto a la grilla del depósito EN CURSO -----
        ForAll(
            colDetalleConteoCon As M,
            Patch(
                colDetalleConteoCon,
                M,
                { Auto: Coalesce(LookUp(colAutoContadoraCon, Denominacion = M.Denominacion).Cantidad, 0) }
            )
        );
        Set(gvarDepositoSinGuardar, true);
        Reset(galBilletesCon);
        Notify("Reconteo de la máquina completado.", NotificationType.Success)
    )
)
```

**Reglas:**

1. 🧨 **El `Filter` es de UN salto gracias al espejo E-4.** La v1 escribía
   `Filter(XMLReadingDetails, XMLReading.XMLReadingId in Filter(XMLReadings, …).XMLReadingId)`
   — un `in` con subconsulta que **jamás delega** (regla **D-6**). Con >500 filas históricas
   la columna Auto mostraba **0** y el cajero recontaba con totales de máquina equivocados.
   **Fallback sin backend en `01` §2.2** (dos consultas encadenadas, delegables las dos).
2. 🧨 **`colAutoContadoraCon` está vacía al entrar a este botón**, porque los Bloques C, N y
   M la limpian en los **cuatro** puntos donde cambia el depósito en curso (`01` §4.4). Sin
   eso, la lectura de la máquina se hereda y el efectivo se duplica (**D-4**).
   🧨 **Su esquema es `{ Denominacion, Cantidad }` — en ESPAÑOL**, el mismo que siembra
   `App.OnStart` (`01` §3.2) y el que declara `01` §4.2. Por eso el `LookUp` del paso 3 lee
   `Denominacion = M.Denominacion` y **no** `Denomination = …`.
3. 🧨 **CERO `GroupBy`/`AddColumns`: `Distinct` + `Sum` sobre el snapshot** (`L-64`).
   `GroupBy` es **quisquilloso** —choque de nombres del grupo, fuentes que no acepta, y el
   `Sum(GruposXml, …)` de `AddColumns` fallando en runtime sobre el resultado de un `Filter`
   de datasource— y la lectura volvía **vacía sin error**: la columna **Auto** en cero, que
   es exactamente el bug **D-1** que esta sección promete matar. `Distinct()` devuelve la
   columna **`Value`** (de ahí `D.Value`), y la proyección explícita fija el esquema.
   **`colLecturasTmpCon` es LOCAL de este paso**: nace, se usa y se limpia en tres
   instrucciones; **no** entra al inventario de `01` §4.2 ni a la siembra de `App.OnStart`.
4. **`Auto` solo se escribe en las denominaciones del depósito EN CURSO**:
   `colDetalleConteoCon` ya está filtrada por la moneda del depósito, así que el `ForAll` no
   puede cruzar monedas.
5. **`Coalesce(…, 0)`**: una denominación que la máquina no leyó vale `0`, no *blank*.
6. **`Reset(galBilletesCon)`** después del volcado: los `Default` de los inputs no cambian
   (el cajero no perdió lo tecleado, porque `Manual` no se toca), pero la galería tiene que
   repintar el badge Auto de cada fila.
7. 🚫 **Cero `Notify` con el serial ni con GUIDs** (**S-6**). La v1 los imprimía en
   producción.
8. 🧨 **VERIFICAR EL TIPO DEL PRIMER PARÁMETRO CONTRA EL FLUJO REAL, al construir esto**
   (ledger **`L-60`**, `01` §7.11 regla 7). `'CH-Tbl-RecountingSessionDetail'` es un **GUID**
   y acá va envuelto en `Text()` porque así lo mandaba la v1 — es un **supuesto heredado**,
   no un hecho verificado. Abrí `CH-Flow-ProcessXMLFiles` en Power Automate → *Cuando Power
   Apps llama a un flujo* y mirá el tipo de `ID_Sesion`: **si espera un GUID crudo, se quita
   el `Text()`** y se anota en el ledger. Un tipo equivocado **no falla en tiempo de
   diseño**: falla en runtime con un mensaje del conector, y el cajero cree que la contadora
   no responde.

### 8.5 Agregar un cheque manual

```powerfx
// ====================== btnAgregarChequeCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

Collect(
    colChecksCon,
    {
        LocalId:       Text(GUID()),          // 🧨 UNO por clic: ver la regla de abajo
        GUID:          Defaults('CH-Tbl-Checks').'CH-Tbl-Checks',
        DeclaradoGUID: Defaults('CH-Tbl-DeclaredChecks').'CH-Tbl-DeclaredChecks',
        Numero:        "",
        Tipo:          "BAC Local",
        Moneda:        ctxMonedaCon,
        Monto:         0,
        Seleccionado:  true,
        EsManual:      true
    }
);
Set(gvarDepositoSinGuardar, true);
Reset(galChequesCon)
```

> 🚫 **`Max(col, OrderID) + 1` está PROHIBIDO** (`01` §4.3). Era el bug **D-9** de la v1: el
> `OrderID` se generaba **dentro de un `ForAll`**, así que todas las filas del lote recibían
> el mismo número ⇒ editar el monto de un cheque sobrescribía el de todos los que compartían
> `OrderID`, y tildar uno tildaba todos.
>
> 🧨 **`GUID()` dentro de un `ForAll` también repite.** Por eso los cheques manuales se
> agregan **de a uno**, con **un `Collect` por clic** de este botón, nunca en bucle. Es la
> única `GUID()` de toda la pantalla.

> **El cheque nace con `Numero` vacío y `Monto` 0**, y el guardado **no lo deja pasar así**:
> la validación de §9.1 exige número, tipo y **monto > 0**, y el borde de
> `inpMontoChequeCon` se pinta rojo mientras el monto sea 0. La v1 **reseteaba el monto en
> silencio** cuando no cumplía el mínimo del modelo (`CheckAmount` tiene mínimo 1) y el
> cajero no se enteraba. Divergencia **`D04-10`**: acá el feedback es visual y bloqueante.

---

## 9. Botonera del depósito y GUARDADO

### 9.1 La botonera (Y `704`–`752`)

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFootAccionesCon` | Botón clásico | receta 7.1 · X `736` · Y `shpCardCurCon.Y + shpCardCurCon.Height - 48` · Width `612` · Height `48` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` **TopLeft `0` · TopRight `0` · BottomLeft `13` · BottomRight `13`** · `DisplayMode` `DisplayMode.View` |
| `btnNuevoDepositoCon` | Botón clásico | **ESTILO-BTN-CHICO-ACC** · X `shpCardCurCon.X + 16` · Y `shpFootAccionesCon.Y + 11` · Width `66` · Height `26` · `Text` `"Nuevo"` · `DisplayMode` **EDITABLE** · `OnSelect` ver abajo |
| `btnDescartarCon` | Botón clásico | **ESTILO-BTN-CHICO** · X `btnNuevoDepositoCon.X + btnNuevoDepositoCon.Width + 8` · Y `btnNuevoDepositoCon.Y` · Width `84` · Height `26` · `Text` `"Descartar"` · `DisplayMode` ver abajo · `OnSelect` ver abajo |
| `lblTotalDepositoCon` | Etiqueta clásica | X `btnDescartarCon.X + btnDescartarCon.Width + 12` · Y `btnNuevoDepositoCon.Y` · Width `btnGuardarDepositoCon.X - 12 - Self.X` · Height `26` · **ESTILO-META** · `Align` `Align.Right` · `Visible` `ctxHayDepositoCon` · `Text` ver abajo |
| `btnGuardarDepositoCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `shpCardCurCon.X + shpCardCurCon.Width - 16 - Self.Width` · Y `shpFootAccionesCon.Y + 8` · Width `140` · Height `32` · `Text` `"Guardar depósito"` · `DisplayMode` ver abajo · `OnSelect` §9.2 |

```powerfx
// lblTotalDepositoCon.Text
"Total del depósito  " &
fxMonto(
    Sum(colDetalleConteoCon, Denominacion * (Auto + Manual)) +
    Sum(Filter(colChecksCon, Seleccionado), Monto),
    fxSimbolo(ctxMonedaCon)
)

// btnDescartarCon.DisplayMode  y  btnGuardarDepositoCon.DisplayMode — la MISMA condición
If(
    gvarIdentidadOk && !ctxSoloLecturaCon && ctxHayDepositoCon,
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

> **Por qué el gate NO incluye `gvarDepositoSinGuardar`**: guardar un depósito sin cambios es
> idempotente (re-patchea lo mismo) y deshabilitar el botón esconde el motivo. El gate es
> *estructural* (hay depósito en curso y la tula es editable); las guardas de negocio viven
> en el `OnSelect` y **hablan**. Mismo criterio que `reception-rebuild/02` §4.

```powerfx
// ====================== btnNuevoDepositoCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

If(
    gvarDepositoSinGuardar,
    Notify(
        "Guarde o descarte el depósito en curso antes de empezar otro.",
        NotificationType.Warning
    ),
    // 🧨 ctxHayDepositoCon lo setea el LLAMADOR, ANTES del Select (§6.4): acá `true`,
    //    porque el panel derecho queda listo para capturar el depósito nuevo.
    UpdateContext({ ctxHayDepositoCon: true });
    Select(btnDepNuevoCon);          // Bloque N — DEPÓSITO NUEVO (§6.4)
    UpdateContext({ ctxDepSelCon: Defaults('CH-Tbl-DepositsRecountings') });
    Notify("Depósito nuevo. Elija la moneda y empiece a contar.", NotificationType.Information)
)
```

```powerfx
// ====================== btnDescartarCon.OnSelect ======================
// 🧨 CONFIRMAR ANTES DE EJECUTAR (receta 7.2, regla 2): este botón NO borra nada.
//    La v1 hacía Clear(colRecountedMoneyDetails) ANTES de abrir el diálogo.
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

If(
    !gvarDepositoSinGuardar,
    Notify("No hay cambios que descartar.", NotificationType.Information),
    UpdateContext({ ctxDlgDescartarCon: true })
)
```

### 9.2 Guardar el depósito — barato en el botón, pesado en el timer

**`btnGuardarDepositoCon.OnSelect` — ni un `Patch` a Dataverse acá** (receta `01` §7.10.C):

```powerfx
// ====================== btnGuardarDepositoCon.OnSelect ======================
Select(btnVolcarGrillaCon);          // Bloque V — VOLCADO (§6.2), PRIMERA instrucción

// ---- Validación LOCAL, con guardas que HABLAN ----
//   🧨 LAS CUATRO RAMAS DE ERROR BAJAN `ctxSalirTrasGuardarCon`. Este OnSelect lo
//      dispara también *Guardar y salir* (§13.1) con la bandera en `true`: si una
//      validación corta acá, el timer NUNCA corre y la bandera queda encendida ⇒ el
//      siguiente "Guardar depósito" —uno normal— sacaría al cajero de la tula.
//      La bandera se sube en UN solo lugar (§13.1) y se baja en CINCO: estas cuatro
//      ramas y el paso 9 del OnTimerEnd (§9.2).
If(
    IsBlank(Trim(inpCuentaCon.Text)) || Value(inpCuentaCon.Text) <= 0,
        UpdateContext({ ctxSalirTrasGuardarCon: false });
        Notify("Escriba el número de cuenta del depósito.", NotificationType.Error),

    Value(inpCuentaCon.Text) > 999999999,
        UpdateContext({ ctxSalirTrasGuardarCon: false });
        Notify("El número de cuenta no puede tener más de 9 dígitos.", NotificationType.Error),

    CountRows(
        Filter(
            colChecksCon,
            EsManual && (IsBlank(Trim(Numero)) || IsBlank(Tipo) || Monto <= 0)
        )
    ) > 0,
        UpdateContext({ ctxSalirTrasGuardarCon: false });
        Notify(
            "Hay cheques manuales incompletos: cada uno necesita número, tipo y un monto mayor que cero.",
            NotificationType.Error
        ),

    Sum(colDetalleConteoCon, Denominacion * (Auto + Manual)) +
    Sum(Filter(colChecksCon, Seleccionado), Monto) <= 0,
        UpdateContext({ ctxSalirTrasGuardarCon: false });
        Notify("No hay nada que guardar en este depósito.", NotificationType.Error),

    // ---- Todo bien: se pinta el diálogo y el trabajo pesado corre un frame después ----
    UpdateContext({ ctxDlgGuardandoCon: true, ctxGuardandoCon: true })
)
```

> 🧨 **`"Escriba"`, no `"Escribí"`** — registro de **usted** en todo literal que el usuario
> lee (`L-54`). Era el único voseo que quedaba en esta pantalla.

> 🧨 **`Value(inpCuentaCon.Text) > 999999999` no es paranoia**: `AccountNumber` es `int` con
> máximo `999999999` (`01` §2.4). Sin esta guarda, el `Patch` lo rechaza el servidor con un
> mensaje genérico y el cajero no sabe qué corregir.

**`tmrGuardarCon` — el defer de UN frame** (receta `01` §7.10.C):

| Propiedad | Valor |
|---|---|
| Tipo | **Temporizador clásico** |
| `AutoStart` | `false` |
| `Duration` | `50` |
| `Repeat` | **`false`** |
| `Start` | **`ctxGuardandoCon`** |
| `Visible` | `false` |
| `OnTimerStart` | **vacío** |
| `OnTimerEnd` | todo el trabajo pesado, abajo |

```powerfx
// ====================== tmrGuardarCon.OnTimerEnd ======================
Clear(colFallidasCon);

// ============ PASO 1 — CABECERA DEL DEPÓSITO (crea o actualiza) ============
//   Name es AUTONUMBER DR-{SEQNUM:8}: NUNCA se patchea.
//   El registro BASE decide crear vs. actualizar. Cero forms.
IfError(
    UpdateContext({
        ctxDepositoCon:
            Patch(
                'CH-Tbl-DepositsRecountings',
                If(
                    IsBlank(ctxDepositoCon.'CH-Tbl-DepositsRecounting'),
                    Defaults('CH-Tbl-DepositsRecountings'),
                    ctxDepositoCon
                ),
                {
                    BagID:    gvarTula,
                    Currency: Switch(
                                  ctxMonedaCon,
                                  "NIO", 'CH-Ch-Currency'.NIO,
                                  "USD", 'CH-Ch-Currency'.USD,
                                  "EUR", 'CH-Ch-Currency'.EUR
                              ),
                    AccountNumber:        Value(inpCuentaCon.Text),
                    CashAmount:           Sum(colDetalleConteoCon, Denominacion * (Auto + Manual)),
                    BACLocalCheckAmount:  Sum(Filter(colChecksCon, Seleccionado && Tipo = "BAC Local"),   Monto),
                    OtherLocaCheckAmount: Sum(Filter(colChecksCon, Seleccionado && Tipo = "Otros Local"), Monto),
                    TransactionNumber: If(
                                           IsBlank(ctxDepDecCursoCon.'CH-Tbl-DeclaredDeposits'),
                                           Blank(),
                                           ctxDepDecCursoCon.TransactionNo
                                       )
                }
            )
    }),
    If(true,
        Collect(colFallidasCon, { Clave: Trim(inpCuentaCon.Text), Detalle: "depósito", Mensaje: FirstError.Message });
        Blank()
    )
);

// ============ GUARDA DURA: sin cabecera NO se escribe ni un hijo ============
//   🧨 Es el fix estructural de D-7: la v1 patcheaba hijos contra un padre Blank y
//      creaba filas fantasma.
If(
    IsBlank(ctxDepositoCon.'CH-Tbl-DepositsRecounting') || !IsEmpty(colFallidasCon),

    Notify(
        "No se pudo guardar el depósito: " &
        Concat(colFallidasCon, Detalle & " — " & Mensaje, " · "),
        NotificationType.Error
    ),

    // ================================================================
    // PASO 2 — DENOMINACIONES (receta 01 §7.5, forma B)
    //   Solo las filas con Auto + Manual > 0: Quantity tiene MÍNIMO 1.
    //   🧨 Lo persistido es Auto + Manual  ⇒  D-5 muerto.
    // ================================================================
    IfError(
        Patch(
            'CH-Tbl-CountedMoneyDetails',
            ForAll(
                Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
                If(
                    IsBlank(M.GUID),
                    Defaults('CH-Tbl-CountedMoneyDetails'),
                    LookUp(colDetalleDVCon, 'CH-Tbl-CountedMoneyDetails' = M.GUID)
                )
            ),
            ForAll(
                Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
                {
                    RelatedDepositRecountingID: ctxDepositoCon,
                    Denomination:               M.Denominacion,
                    DenominationType:           M.Tipo,            // "Billete" | "Moneda" (TEXTO)
                    Quantity:                   M.Auto + M.Manual
                }
            )
        ),

        // FASE B — reintento fila por fila para saber CUÁL falló
        ForAll(
            Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
            IfError(
                Patch(
                    'CH-Tbl-CountedMoneyDetails',
                    If(
                        IsBlank(M.GUID),
                        Defaults('CH-Tbl-CountedMoneyDetails'),
                        LookUp(colDetalleDVCon, 'CH-Tbl-CountedMoneyDetails' = M.GUID)
                    ),
                    {
                        RelatedDepositRecountingID: ctxDepositoCon,
                        Denomination:               M.Denominacion,
                        DenominationType:           M.Tipo,
                        Quantity:                   M.Auto + M.Manual
                    }
                ),
                If(true,
                    // 🧨 El brazo de fallo TERMINA en Blank(): Collect devuelve una TABLA y
                    //    IfError exige el tipo del primer argumento (un REGISTRO).
                    Collect(colFallidasCon, { Clave: Text(M.Denominacion), Detalle: M.Tipo, Mensaje: FirstError.Message });
                    Blank()
                )
            )
        )
    );

    // ================================================================
    // PASO 3 — CHEQUES seleccionados (alta o actualización), de a uno.
    //   De a uno porque cada fila necesita, DESPUÉS de escribirse, un segundo
    //   Patch a DeclaredChecks.MatchedCheck con el registro recién creado.
    //   🧨 EL WRITE-BACK DEL GUID NO VA ACÁ ADENTRO (01 §4.3): este ForAll recorre
    //      colChecksCon y un Patch(colChecksCon, …) dentro MUTA LA COLECCIÓN QUE SE
    //      ESTÁ RECORRIENDO. El GUID real se ACUMULA en colKeysCon y se escribe
    //      después del bucle, sobre una colección que ya nadie recorre.
    // ================================================================
    Clear(colKeysCon);
    ForAll(
        Filter(colChecksCon, Seleccionado) As C,
        IfError(
            With(
                {
                    Guardado:
                        Patch(
                            'CH-Tbl-Checks',
                            If(
                                IsBlank(C.GUID),
                                Defaults('CH-Tbl-Checks'),
                                LookUp(colChecksDVCon, 'CH-Tbl-Checks' = C.GUID)
                            ),
                            {
                                RelatedReCounterDepositID: ctxDepositoCon,
                                RelatedDeclaredCheck: If(
                                    C.EsManual,
                                    Blank(),
                                    LookUp(colChequesDeclaradosCon, 'CH-Tbl-DeclaredChecks' = C.DeclaradoGUID)
                                ),
                                CheckNumber:   Trim(C.Numero),
                                CheckAmount:   C.Monto,
                                CheckCurrency: Switch(
                                                   C.Moneda,
                                                   "NIO", 'CH-Ch-Currency'.NIO,
                                                   "USD", 'CH-Ch-Currency'.USD,
                                                   "EUR", 'CH-Ch-Currency'.EUR
                                               ),
                                CheckTypes: Switch(
                                                C.Tipo,
                                                "BAC Local",   'CheckTypes (CH-Tbl-Checks)'.'BAC Local',
                                                'CheckTypes (CH-Tbl-Checks)'.'Otros Local'
                                            )
                            }
                        )
                },
                // el GUID real se ACUMULA; el write-back corre después del ForAll
                Collect(colKeysCon, { L: C.LocalId, G: Guardado.'CH-Tbl-Checks' });

                // 🧨 EL ENLACE VA EN MatchedCheck, NUNCA en DeclaredDepositID (fix de D-6)
                If(
                    !C.EsManual,
                    Patch(
                        'CH-Tbl-DeclaredChecks',
                        LookUp(colChequesDeclaradosCon, 'CH-Tbl-DeclaredChecks' = C.DeclaradoGUID),
                        { MatchedCheck: Guardado }
                    )
                );
                Guardado
            ),
            If(true,
                Collect(colFallidasCon, { Clave: Trim(C.Numero), Detalle: "cheque", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ---- 3.b WRITE-BACK del GUID real, FUERA del ForAll de arriba (01 §4.3) ----
    //    Sin esto, el próximo guardado del mismo depósito CREARÍA un cheque nuevo en
    //    vez de actualizar el que acaba de escribir (el re-keyado de la receta 7.5).
    ForAll(
        colKeysCon As K,
        Patch(colChecksCon, LookUp(colChecksCon, LocalId = K.L), { GUID: K.G })
    );

    // ================================================================
    // PASO 4 — BAJAS DE CHEQUES. Va DESPUÉS del alta (receta 01 §7.5, regla 2).
    //   Cubre TRES casos con un solo predicado: declarado desmarcado, cheque
    //   manual borrado con la papelera, y fila que desapareció por cambio de moneda.
    // ================================================================
    ForAll(
        Filter(
            colChecksDVCon As X,
            IsBlank(LookUp(colChecksCon, GUID = X.'CH-Tbl-Checks' && Seleccionado))
        ) As X,
        // primero se suelta el enlace del declarado, después se borra el cheque
        If(
            !IsBlank(X.RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks'),
            IfError(
                Patch(
                    'CH-Tbl-DeclaredChecks',
                    LookUp(colChequesDeclaradosCon, 'CH-Tbl-DeclaredChecks' = X.RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks'),
                    { MatchedCheck: Blank() }
                ),
                If(true,
                    Collect(colFallidasCon, { Clave: X.CheckNumber, Detalle: "MatchedCheck", Mensaje: FirstError.Message });
                    Blank()
                )
            )
        );
        IfError(
            Remove('CH-Tbl-Checks', X),
            If(true,
                Collect(colFallidasCon, { Clave: X.CheckNumber, Detalle: "baja de cheque", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ================================================================
    // PASO 5 — BAJAS DE DENOMINACIONES. También DESPUÉS del alta.
    //   Cubre "quedó en 0" Y "la fila ya no existe" (cambio de moneda).
    //   🧨 Remove por REGISTRO, jamás RemoveIf(ds, predicado) (regla D-3).
    // ================================================================
    ForAll(
        Filter(
            colDetalleDVCon As X,
            IsBlank(
                LookUp(
                    colDetalleConteoCon,
                    GUID = X.'CH-Tbl-CountedMoneyDetails' && (Auto + Manual) > 0
                )
            )
        ) As X,
        IfError(
            Remove('CH-Tbl-CountedMoneyDetails', X),
            If(true,
                Collect(colFallidasCon, { Clave: Text(X.Denomination), Detalle: "baja de denominación", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ================================================================
    // PASO 6 — RECARGA: Q1 + Q2, dos consultas delegables. Cero Refresh().
    // ================================================================
    Concurrent(
        ClearCollect(
            colDepRecontadosCon,
            SortByColumns(
                Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
                "createdon", SortOrder.Ascending
            )
        ),
        ClearCollect(
            colChecksTulaCon,
            Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')
        )
    );

    // ================================================================
    // PASO 7 — Volver a cargar el depósito recién guardado en pantalla.
    //   El registro sale de colDepRecontadosCon, que el paso 6 ACABA de recargar:
    //   trae el CashAmount y los importes de cheques que el servidor persistió.
    // ================================================================
    UpdateContext({
        ctxRegDepCon:
            LookUp(
                colDepRecontadosCon,
                'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting'
            )
    });
    Select(btnCargarDepCon);          // Bloque C — CARGAR DEPÓSITO EN CURSO (§6.3)

    // ================================================================
    // PASO 8 — La UI NUNCA marca como guardado lo que el guard rechazó.
    // ================================================================
    Set(gvarDepositoSinGuardar, !IsEmpty(colFallidasCon));
    UpdateContext({ ctxDepSelCon: ctxDepositoCon });

    If(
        IsEmpty(colFallidasCon),
        Notify("Depósito guardado.", NotificationType.Success),
        Notify(
            "El depósito se guardó con errores: " &
            Concat(colFallidasCon, Detalle & " " & Clave & " — " & Mensaje, " · "),
            NotificationType.Error
        )
    );

    Blank()
);

// ================================================================
// PASO 9 — Continuación de "Guardar y salir" (§13.1). Va ACÁ, no en el
//   OnSelect del botón: los timers NO corren en pantallas no visibles.
//   🧨 Y va FUERA de la guarda dura del paso 1: si la cabecera falló, el flujo
//      entra por el brazo del Notify y este paso TIENE que correr igual — es el
//      único lugar donde la bandera baja.
// ================================================================
If(
    ctxSalirTrasGuardarCon && IsEmpty(colFallidasCon),
    Navigate(scrTulas)
);

// 🧨 La bandera baja SIEMPRE, y el Navigate es lo único condicionado. Si quedara
//    arriba (dentro del If), un guardado con errores la dejaba en `true` y el
//    SIGUIENTE clic en "Guardar depósito" —uno normal, sin intención de salir—
//    sacaba al cajero de la tula sin que nadie se lo pidiera.
UpdateContext({
    ctxSalirTrasGuardarCon: false,
    ctxDlgGuardandoCon:     false,
    ctxGuardandoCon:        false
})
```

**Reglas del guardado — las siete que matan bugs de la v1:**

1. 🧨 **Sin cabecera no se escribe ni un hijo** (**D-7**). La guarda dura después del paso 1
   es lo que impide crear filas fantasma colgadas de un padre `Blank()`.
2. 🧨 **`Quantity` persistida = `Auto + Manual`** (**D-5**). La v1 sumaba lo automático a
   `CashAmount` pero **solo persistía la parte manual** en `CountedMoneyDetails`: el
   desglose nunca cuadraba con el monto y el arqueo era irreconstruible.
3. 🧨 **`Quantity` tiene MÍNIMO 1**: una fila en `0` **no se puede patchear**, hay que
   **borrarla**. Por eso los pasos 4 y 5 existen, y por eso van **después** del alta.
4. 🧨 **El enlace de un cheque declarado se escribe en `MatchedCheck`** (**D-6**). La v1
   hacía `Patch('CH-Tbl-DeclaredChecks', …, {DeclaredDepositID: Blank()})` al destildar:
   limpiaba el **lookup padre requerido** en vez del enlace, y el cheque declarado quedaba
   huérfano de su declaración **para siempre**. `DeclaredDepositID` **la app no lo escribe
   nunca**.
5. **Todo brazo de fallo que acumula con `Collect` TERMINA en `; Blank()`**: `IfError`
   exige que el brazo tenga el tipo del primer argumento. Sin eso **no compila**.
6. **Cero `Refresh()`** (**D-5**). La v1 hacía tres seguidos al final del guardado.
7. **`gvarDepositoSinGuardar` baja a `false` SOLO si `colFallidasCon` está vacía.** Si el
   guard server-side rechazó algo, la píldora *Sin guardar* sigue encendida y la guarda de
   salida sigue armada.

> 🧨 **Esto NO resucita el `'timer Submit'` de la v1**, que era una **máquina de estados**
> con `varCurrentRow` / `varFirstRow` / `varRowIndex` iterando fila por fila. Acá el timer es
> un **defer de UN frame**: `Repeat false`, `Duration 50`, corre una sola vez por clic y no
> lleva estado de iteración (`01` §7.10.C, regla 5).

---

## 10. Terminar tula — POBLAR y después VALIDAR (mata **D-8**)

### 10.1 `btnTerminarCon.OnSelect` — el orden es la regla

La v1 validaba `colRecountedDeposits` **45 líneas antes** del `ClearCollect` que la poblaba
(bug **D-8**): el primer clic en *Terminar* **no reportaba ninguna diferencia por
transacción** al supervisor. En esta app el orden es, sin excepción:

```
1. VOLCADO (Bloque V)
2. GUARDAS que hablan
3. ClearCollect de las 5 colecciones de hallazgos   ← POBLAR
4. ClearCollect(colHallazgosCon, …)                 ← VALIDAR (lee las 5 de arriba)
5. If(IsEmpty(colHallazgosCon), <C4>, <C5>)
```

```powerfx
// ====================== btnTerminarCon.OnSelect ======================
// ---- 1. Bloque V — VOLCADO (§6.2), PRIMERA instrucción ----
Select(btnVolcarGrillaCon);

If(
    // ---- 2. GUARDAS ----
    gvarDepositoSinGuardar,
        Notify("Guarde o descarte el depósito en curso antes de terminar la tula.", NotificationType.Error),

    IsEmpty(colDepRecontadosCon),
        Notify("La tula no tiene ningún depósito recontado.", NotificationType.Error),

    // ---- 3. POBLAR las 5 colecciones de hallazgos. TODO desde colecciones locales ----
    ClearCollect(
        colDifTrxCon,
        ForAll(
            Filter(
                colDepRecontadosCon,
                !IsBlank(RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits')
            ) As R,
            With(
                { D: LookUp(colDepDeclaradosCon, 'CH-Tbl-DeclaredDeposits' = R.RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits') },
                {
                    Indice:      0,
                    Cuenta:      Text(R.AccountNumber),
                    Moneda:      Text(R.Currency),
                    DifCheques:  (R.BACLocalCheckAmount + R.OtherLocaCheckAmount) -
                                 (D.BACLocalCheckAmount + D.OtherLocalCheckAmount),
                    DifEfectivo: R.CashAmount - D.CashAmount
                }
            )
        )
    );
    // solo quedan los que EFECTIVAMENTE tienen diferencia
    RemoveIf(colDifTrxCon, DifCheques = 0 && DifEfectivo = 0);

    ClearCollect(
        colDepRecSinEnlazarCon,
        ForAll(
            Filter(colDepRecontadosCon, IsBlank(RelatedDeclaredDeposit.'CH-Tbl-DeclaredDeposits')) As R,
            {
                Indice:   0,
                Cuenta:   Text(R.AccountNumber),
                Moneda:   Text(R.Currency),
                Cheques:  R.BACLocalCheckAmount + R.OtherLocaCheckAmount,
                Efectivo: R.CashAmount
            }
        )
    );

    ClearCollect(
        colDepDecSinEnlazarCon,
        ForAll(
            Filter(colDepDeclaradosCon, IsBlank(RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting')) As D,
            {
                Indice:   0,
                Cuenta:   Text(D.AccountNumber),
                Moneda:   Text(D.Currency),
                Cheques:  D.BACLocalCheckAmount + D.OtherLocalCheckAmount,
                Efectivo: D.CashAmount
            }
        )
    );

    ClearCollect(
        colChqDecSinEnlazarCon,
        ForAll(
            Filter(
                colChequesDeclaradosCon As DC,
                IsBlank(
                    LookUp(colChecksTulaCon, RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks' = DC.'CH-Tbl-DeclaredChecks').'CH-Tbl-Checks'
                )
            ) As DC,
            {
                Indice:   0,
                Numero:   DC.CheckNumber,
                Tipo:     Text(DC.CheckType),
                Moneda:   Text(DC.CheckCurrency),
                Monto:    DC.CheckAmount,
                Deposito: Text(
                              LookUp(colDepDeclaradosCon, 'CH-Tbl-DeclaredDeposits' = DC.DeclaredDepositID.'CH-Tbl-DeclaredDeposits').AccountNumber
                          )
            }
        )
    );

    ClearCollect(
        colChqManualCon,
        ForAll(
            Filter(colChecksTulaCon, IsBlank(RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks')) As MC,
            {
                Indice:   0,
                Numero:   MC.CheckNumber,
                Tipo:     Text(MC.CheckTypes),
                Moneda:   Text(MC.CheckCurrency),
                Monto:    MC.CheckAmount,
                Deposito: Text(
                              LookUp(colDepRecontadosCon, 'CH-Tbl-DepositsRecounting' = MC.RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting').AccountNumber
                          )
            }
        )
    );

    // ---- 3.b MATERIALIZAR el Indice (zebra del HTML, 01 §4.2) ----
    ForAll(Sequence(CountRows(colDifTrxCon)) As S,           Patch(colDifTrxCon,           Index(colDifTrxCon, S.Value),           { Indice: S.Value }));
    ForAll(Sequence(CountRows(colDepRecSinEnlazarCon)) As S, Patch(colDepRecSinEnlazarCon, Index(colDepRecSinEnlazarCon, S.Value), { Indice: S.Value }));
    ForAll(Sequence(CountRows(colDepDecSinEnlazarCon)) As S, Patch(colDepDecSinEnlazarCon, Index(colDepDecSinEnlazarCon, S.Value), { Indice: S.Value }));
    ForAll(Sequence(CountRows(colChqDecSinEnlazarCon)) As S, Patch(colChqDecSinEnlazarCon, Index(colChqDecSinEnlazarCon, S.Value), { Indice: S.Value }));
    ForAll(Sequence(CountRows(colChqManualCon)) As S,        Patch(colChqManualCon,        Index(colChqManualCon, S.Value),        { Indice: S.Value }));

    // ---- 4. VALIDAR: los hallazgos LEEN las 5 colecciones recién pobladas ----
    Clear(colHallazgosCon);

    // H1 — tula sin declaración: no hay base de contraste. SIEMPRE va a revisión.
    If(
        IsBlank(gvarTula.BagDeclarationID.'CH-Tbl-DeclaredBags'),
        Collect(colHallazgosCon, {
            Orden: 1,
            Texto: "Esta tula NO tiene declaración del cliente: no hay base de contraste y todas las diferencias globales quedan sin comparar."
        })
    );

    // H2 — diferencias globales, leídas de las SALIDAS de las tres matrices (01 §8.1, L-8)
    If(
        cmpMatNIOCon.HayDiferencia || cmpMatUSDCon.HayDiferencia || cmpMatEURCon.HayDiferencia,
        Collect(colHallazgosCon, {
            Orden: 2,
            Texto: "Existen diferencias globales entre lo recontado y lo declarado — " &
                   If(cmpMatNIOCon.HayDiferencia, "NIO: " & fxMontoDif(cmpMatNIOCon.DifEfectivo, SimboloNIO) & " en efectivo y " & fxMontoDif(cmpMatNIOCon.DifMonto, SimboloNIO) & " en cheques. ", "") &
                   If(cmpMatUSDCon.HayDiferencia, "USD: " & fxMontoDif(cmpMatUSDCon.DifEfectivo, SimboloUSD) & " en efectivo y " & fxMontoDif(cmpMatUSDCon.DifMonto, SimboloUSD) & " en cheques. ", "") &
                   If(cmpMatEURCon.HayDiferencia, "EUR: " & fxMontoDif(cmpMatEURCon.DifEfectivo, SimboloEUR) & " en efectivo y " & fxMontoDif(cmpMatEURCon.DifMonto, SimboloEUR) & " en cheques.", "")
        })
    );

    // H3 — depósitos con diferencia por transacción
    If(
        CountRows(colDifTrxCon) > 0,
        Collect(colHallazgosCon, {
            Orden: 3,
            Texto: With(
                       { n: CountRows(colDifTrxCon) },
                       Text(n) & If(n = 1, " depósito con diferencia", " depósitos con diferencia")
                   ) & " por transacción — " &
                   Concat(
                       FirstN(SortByColumns(colDifTrxCon, "Indice"), 4) As T,
                       T.Cuenta & " (" & fxMontoDif(T.DifCheques + T.DifEfectivo, fxSimbolo(T.Moneda)) & ")",
                       ", "
                   ) & "."
        })
    );

    // H4 — cheques declarados sin enlazar. 🧨 El importe va POR MONEDA: sumar NIO + USD + EUR
    //      y estamparle "C$" es exactamente el bug D-2 de 00 §1.1 (L-42, misma clase).
    If(
        CountRows(colChqDecSinEnlazarCon) > 0,
        Collect(colHallazgosCon, {
            Orden: 4,
            Texto: With(
                { n: CountRows(colChqDecSinEnlazarCon) },
                Text(n) &
                If(n = 1, " cheque declarado sin enlazar: ", " cheques declarados sin enlazar: ") &
                Concat(
                    Filter(
                        ForAll(
                            Table({ M: "NIO" }, { M: "USD" }, { M: "EUR" }) As Q,
                            {
                                Mon:  Q.M,
                                Tot:  Coalesce(Sum(Filter(colChqDecSinEnlazarCon, Moneda = Q.M), Monto), 0),
                                Cant: CountRows(Filter(colChqDecSinEnlazarCon, Moneda = Q.M))
                            }
                        ),
                        Cant > 0
                    ) As S,
                    Text(S.Cant) & " por " & fxMonto(S.Tot, fxSimbolo(S.Mon)),
                    " · "
                ) &
                " (" & Concat(FirstN(SortByColumns(colChqDecSinEnlazarCon, "Indice"), 5) As C, C.Numero, ", ") & ")."
            )
        })
    );

    // H5 — depósitos recontados sin enlazar
    If(
        CountRows(colDepRecSinEnlazarCon) > 0,
        Collect(colHallazgosCon, {
            Orden: 5,
            Texto: With(
                       { n: CountRows(colDepRecSinEnlazarCon) },
                       Text(n) & If(n = 1, " depósito recontado sin enlazar", " depósitos recontados sin enlazar")
                   ) & " — " &
                   Concat(FirstN(SortByColumns(colDepRecSinEnlazarCon, "Indice"), 4) As D, D.Cuenta & " por " & fxMonto(D.Cheques + D.Efectivo, fxSimbolo(D.Moneda)), ", ") & "."
        })
    );

    // H6 — depósitos declarados sin enlazar
    If(
        CountRows(colDepDecSinEnlazarCon) > 0,
        Collect(colHallazgosCon, {
            Orden: 6,
            Texto: With(
                       { n: CountRows(colDepDecSinEnlazarCon) },
                       Text(n) & If(n = 1, " depósito declarado sin enlazar", " depósitos declarados sin enlazar")
                   ) & " — " &
                   Concat(FirstN(SortByColumns(colDepDecSinEnlazarCon, "Indice"), 4) As D, D.Cuenta & " por " & fxMonto(D.Cheques + D.Efectivo, fxSimbolo(D.Moneda)), ", ") & "."
        })
    );

    // H7 — cheques agregados manualmente
    If(
        CountRows(colChqManualCon) > 0,
        Collect(colHallazgosCon, {
            Orden: 7,
            Texto: With(
                       { n: CountRows(colChqManualCon) },
                       Text(n) & If(n = 1, " cheque agregado manualmente", " cheques agregados manualmente")
                   ) & " — " &
                   Concat(FirstN(SortByColumns(colChqManualCon, "Indice"), 4) As M, M.Numero & " · " & M.Tipo & " · " & fxMonto(M.Monto, fxSimbolo(M.Moneda)), "; ") & "."
        })
    );

    // ---- 5. ABRIR el diálogo que corresponda ----
    If(
        IsEmpty(colHallazgosCon),
        UpdateContext({ ctxDlgTerminarOkCon: true }),
        UpdateContext({ ctxDlgTerminarDifCon: true })
    )
)
```

**Reglas de la validación:**

1. 🧨 **POBLAR (paso 3) va ANTES de VALIDAR (paso 4).** Es literalmente el fix de **D-8**.
   Si en esta guía ves un `CountRows(colDifTrxCon)` **antes** del `ClearCollect` que la
   puebla, es un bug de la guía.
2. **Las diferencias globales NO se recalculan**: salen de `HayDiferencia`, `DifEfectivo` y
   `DifMonto`, que son **propiedades de SALIDA** de `cmpMatrizMoneda` (`01` §8.1,
   divergencia `L-8`). Una sola verdad para el mismo número.
3. **Todo sale de colecciones locales**: cero red, cero delegación que romper. La v1 hacía
   **un `LookUp` remoto por cheque declarado** — hasta ~200 round-trips secuenciales por
   clic.
4. **`RemoveIf(colDifTrxCon, …)` es sobre una COLECCIÓN**, no sobre un datasource: la regla
   **D-3** prohíbe `RemoveIf(ds, predicado)`, no el filtrado de memoria.
5. 🧨 **La tula sin declaración SIEMPRE produce el hallazgo H1** ⇒ nunca puede terminar por
   el camino `C4`. Es lo que hace que el `RequestType` sea
   `"Tula recontada sin declaracion"` (`01` §5.6).
6. **Los `Concat` del texto de cada hallazgo se cortan a 4-5 elementos**: el banner es un
   resumen; el detalle completo viaja en el HTML (§11.5).
7. **Plural natural con `If(n = 1, …)`, nunca `"depósito(s)"`.** Es lo que escribe el mockup
   `C5` (*"2 depósitos"*, *"1 cheque"*). El `With({ n: … }, …)` cuenta **una sola vez** y usa
   el mismo `n` en el número y en el sustantivo: dos `CountRows` sobre la misma colección
   serían dos verdades que un `RemoveIf` de mañana puede desincronizar.
8. 🧨 **Ningún hallazgo suma monedas distintas.** `H4` emite **una cifra por moneda
   presente** (`" · "` de separador) con el símbolo que le corresponde vía
   `fxSimbolo(S.Mon)`. Antes decía `fxMonto(Sum(colChqDecSinEnlazarCon, Monto), SimboloNIO)`:
   un cheque en USD se sumaba a los córdobas y salía rotulado `C$`. Es la misma clase de bug
   que **D-2** (`00` §1.1) y la misma decisión que `L-42` / `D04-5` en el pie de depósitos.
   `H3`, `H5`, `H6` y `H7` ya eran correctos: cada uno formatea **fila por fila** con
   `fxSimbolo(<fila>.Moneda)` y nunca imprimen un total agregado.

> 🧨 **Divergencia `D04-11` — siete hallazgos, siete banners.** El mockup `C5` junta
> *"1 depósito recontado sin enlazar"* y *"1 depósito declarado sin enlazar"* en **un** banner
> y no contempla el caso *"tula sin declaración"*. Acá cada hallazgo es su **propia fila** de
> `colHallazgosCon` (y por lo tanto su propio banner en la galería): la galería queda trivial,
> el orden es explícito (`Orden`) y agregar un hallazgo mañana no toca el layout.

### 10.2 Los seis números que el diálogo `C4` muestra

`galResumenCon.Items` **no necesita colección**: es una tabla literal sobre las propiedades
de las tres matrices, que ya son la única verdad.

```powerfx
// galResumenCon.Items
Table(
    {
        Moneda:   "Córdobas (NIO)",
        Simbolo:  SimboloNIO,
        Cheques:  cmpMatNIOCon.ChequesRec,
        Monto:    cmpMatNIOCon.MontoChequesRec,
        Efectivo: cmpMatNIOCon.EfectivoRec
    },
    {
        Moneda:   "Dólares (USD)",
        Simbolo:  SimboloUSD,
        Cheques:  cmpMatUSDCon.ChequesRec,
        Monto:    cmpMatUSDCon.MontoChequesRec,
        Efectivo: cmpMatUSDCon.EfectivoRec
    },
    {
        Moneda:   "Euros (EUR)",
        Simbolo:  SimboloEUR,
        Cheques:  cmpMatEURCon.ChequesRec,
        Monto:    cmpMatEURCon.MontoChequesRec,
        Efectivo: cmpMatEURCon.EfectivoRec
    }
)
```

> 🧨 **Se leen las propiedades de ENTRADA de una instancia de componente desde afuera**
> (`cmpMatNIOCon.ChequesRec`). Es válido: una propiedad personalizada de entrada es una
> propiedad **de la instancia**. **Si tu Studio no lo permite**, el fallback es el de
> `01` §7.6 paso 3: 18 variables de contexto `ctxRecChqNIOCon`… calculadas en el
> `btnTerminarCon.OnSelect` con las **mismas** expresiones de §4. Divergencia **`D04-12`**
> vs. el ejemplo de `01` §7.6 (no vs. su regla): acá hay **una** definición de cada número,
> allá había dos.

---

## 11. Los dos diálogos de cierre (`C4` y `C5`)

Los **seis** overlays de esta pantalla siguen la receta `01` §7.2 y se insertan **últimos**,
en el orden A→F. Cada uno tiene el mismo armazón, con estas propiedades **fijas por receta**
que **no se repiten** en las tablas de abajo:

| Control | Propiedades (receta 7.2) |
|---|---|
| `cntDlg<X>Con` / `cntEsperaCon` / `cntRechazoCon` | Contenedor de diseño manual · X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Fill` `ThemeOverlay` · `Radius` (las 4) `0` · `BorderThickness` `0` · `Visible` = su `ctx…` |
| `btnBloq<X>Con` | Botón clásico, **al FONDO del contenedor** · X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** · `TabIndex` `-1` |
| `cntCard<X>Con` | Contenedor de diseño manual · X `(Parent.Width - Self.Width) / 2` · Y `(Parent.Height - Self.Height) / 2` · `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · **`DropShadow` `DropShadow.Regular`** |
| `btnIco<X>Con` | Botón clásico (receta 7.1) · X `22` · Y `17` · Width `34` · Height `34` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Fill`/`HoverFill`/`PressedFill` = el tono del diálogo |
| `icoTipo<X>Con` | Icono clásico · X `btnIco<X>Con.X + 8` · Y `btnIco<X>Con.Y + 8` · Width `18` · Height `18` · `Color` = el color del tono |
| `icoDlgX<X>Con` | Icono clásico `Icon.Cancel` · X `Parent.Width - 36` · Y `17` · Width `18` · Height `18` · `Color` `ThemeInk3` · **`HoverColor` `ThemeInk`** · `TabIndex` `0` · **`OnSelect` = EXACTAMENTE el mismo del botón Cancelar** · `AccessibleLabel` `"Cerrar el diálogo"` |
| `btnAviso<X>Con` / `icoAviso<X>Con` / `lblAviso<X>Con` | **receta 7.2.a** — el `Visible` se escribe **una sola vez**, en la superficie; los otros dos lo referencian |
| `lblKv<Campo>Etq<X>Con` / `lblKv<Campo><X>Con` | **receta 7.2.b** — etiqueta X `22` W `110` H `18` **ESTILO-META-CHICA**; valor X `140` W `cntCard….Width - 162` H `18` **ESTILO-CELDA-FUERTE**; **paso vertical `22`** |

> 🧨 **`Parent` de todo lo que sigue a la card ES LA CARD.** Cada overlay tiene **exactamente
> dos hijos**: `btnBloq…Con` (primero, al FONDO) y `cntCard…Con`. **Todo el contenido cuelga
> de la card**, no del overlay — por eso las coordenadas dicen X `22` y
> `Width Parent.Width - 44` y **cierran**: `Parent.Width` es `400` o `560`, no `1366`. Si
> alguno de esos controles queda colgado del overlay, el botón se va a `Y 700` y la etiqueta
> mide `1322` de ancho. Es el error de construcción más caro de encontrar.

**Regla no negociable (7.2, regla 1): la ✕ CANCELA.** En los **4** overlays que la tienen
(B, C, D, F), `icoDlgX…Con.OnSelect` es **carácter por carácter** el `OnSelect` de su
`btnDlgCancel…Con`. Ninguna ✕ commitea nada.

**Los dos overlays SIN ✕**: **A** (*Guardando*, no hay nada que cancelar — el trabajo ya
está en vuelo) y **E** (*Esperando aprobación*, es un estado del servidor, no una decisión
del cajero).

### 11.1 Overlay A — Guardando (`cntDlgGuardandoCon`)

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgGuardandoCon` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgGuardandoCon`** |
| `btnBloqGuardandoCon` | Botón clásico | receta 7.2 |
| `cntCardGuardandoCon` | Contenedor de diseño manual | receta 7.2 · Width `400` · Height `lblDlgTxtGuardandoCon.Y + lblDlgTxtGuardandoCon.Height + 22` |
| `lblDlgTitGuardandoCon` | Etiqueta clásica | X `22` · Y `17` · Width `Parent.Width - 44` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Guardando el depósito…"` |
| `lblDlgTxtGuardandoCon` | Etiqueta clásica | X `22` · Y `lblDlgTitGuardandoCon.Y + lblDlgTitGuardandoCon.Height + 6` · Width `Parent.Width - 44` · **ESTILO-DLG-TEXTO** · `AutoHeight` `true` · `Text` `"Escribiendo el depósito, el desglose de denominaciones y los cheques. No cierres la aplicación."` |

> 🧨 **Sin `Spinner` acá, a propósito** (`01` §7.10.C, regla 4): entre el frame que pinta
> este diálogo y el `OnTimerEnd` **el hilo se bloquea igual**, así que un spinner quedaría
> congelado. **Texto quieto.** El Spinner moderno sí se usa en la espera de **aprobación**
> (§12.1), donde el hilo está libre.

### 11.2 Overlay B — `C4`, terminar SIN discrepancias (`cntDlgTerminarOkCon`)

Card **ancha (`560`)**, tono `ok`.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgTerminarOkCon` | Contenedor | receta 7.2 · **`Visible` `ctxDlgTerminarOkCon`** |
| `btnBloqTerminarOkCon` | Botón clásico | receta 7.2 |
| `cntCardTerminarOkCon` | Contenedor | receta 7.2 · Width `560` · Height `btnDlgOkTerminarOkCon.Y + btnDlgOkTerminarOkCon.Height + 19` |
| `btnIcoTerminarOkCon` | Botón clásico | receta 7.2 · `Fill`/`HoverFill`/`PressedFill` `ThemeOkSoft` |
| `icoTipoTerminarOkCon` | Icono clásico `Icon.Check` | receta 7.2 · `Color` `ThemeOk` · **`AccessibleLabel` `""`** (decorativo, §14.2-12) |
| `lblDlgTitTerminarOkCon` | Etiqueta clásica | X `btnIcoTerminarOkCon.X + btnIcoTerminarOkCon.Width + 10` · Y `17` · Width `Parent.Width - Self.X - 44` · Height `22` · **ESTILO-DLG-TITULO** · `Wrap` `true` · `Text` `"Terminar tula " & Trim(gvarTula.BagID)` |
| `lblDlgTxtTerminarOkCon` | Etiqueta clásica | X `lblDlgTitTerminarOkCon.X` · Y `lblDlgTitTerminarOkCon.Y + lblDlgTitTerminarOkCon.Height + 4` · Width `Parent.Width - Self.X - 22` · **ESTILO-DLG-TEXTO** · `AutoHeight` `true` · `Text` `"No se encontró ninguna discrepancia entre lo recontado y lo declarado."` |
| `icoDlgXTerminarOkCon` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelTerminarOkCon` |
| `btnAvisoTerminarOkCon` | Botón clásico | **receta 7.2.a** · X `22` · Y `lblDlgTxtTerminarOkCon.Y + lblDlgTxtTerminarOkCon.Height + 14` · Width `Parent.Width - 44` · Height `58` · `Fill`/`HoverFill`/`PressedFill` `ThemeOkSoft` · `Radius` (las 4) `9` · `Visible` `true` |
| `icoAvisoTerminarOkCon` | Icono clásico `Icon.Check` | receta 7.2.a · `Color` `ThemeOk` |
| `lblAvisoTerminarOkCon` | Etiqueta clásica | receta 7.2.a · `Color` `ThemeOk` · `Text` `"Las validaciones pasaron. La tula pasa directo a Recontada, sin solicitud al supervisor."` — ver la nota del número |
| `shpHeadResumenCon` | Botón clásico | receta 7.1 · X `22` · Y `btnAvisoTerminarOkCon.Y + btnAvisoTerminarOkCon.Height + 14` · Width `Parent.Width - 44` · Height `26` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `0` · `DisplayMode` `DisplayMode.View` |
| `galResumenCon` | Galería vertical en blanco | X `22` · Y `shpHeadResumenCon.Y + shpHeadResumenCon.Height` · Width `Parent.Width - 44` · Height `78` · `TemplateSize` `26` · `TemplatePadding` `0` · `ShowScrollbar` `false` · `OnSelect` `false` · `Items` **§10.2** |
| `lblNotaTerminarOkCon` | Etiqueta clásica | X `22` · Y `galResumenCon.Y + galResumenCon.Height + 10` · Width `Parent.Width - 44` · Height `32` · **ESTILO-META-CHICA** · `Wrap` `true` · `Text` `"Los totales de la bolsa y los asientos contables los recalcula el servidor al recibir el cambio de estado."` |
| `btnDlgCancelTerminarOkCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkTerminarOkCon.X - 8 - Self.Width` · Y `btnDlgOkTerminarOkCon.Y` · Width `92` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgTerminarOkCon: false})` |
| `btnDlgOkTerminarOkCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 22 - Self.Width` · Y `lblNotaTerminarOkCon.Y + lblNotaTerminarOkCon.Height + 16` · Width `124` · `Text` `"Terminar tula"` · `OnSelect` §11.3 |

**Rejilla del resumen** (dentro de `560 − 44 = 516`; X del template = X absoluto − X de la
galería, que es `22` ⇒ los 5 encabezados y las 5 celdas comparten los mismos números):

| Columna | Encabezado | X en la card | X en el template | Width | `Align` |
|---|---|---|---|---|---|
| Resumen por moneda | `lblThResMonedaCon` | `22` | `0` | `150` | `Align.Left` |
| Cheques | `lblThResChqCon` | `172` | `150` | `56` | `Align.Right` |
| Monto cheques | `lblThResMontoCon` | `228` | `206` | `110` | `Align.Right` |
| Efectivo | `lblThResEfectivoCon` | `338` | `316` | `100` | `Align.Right` |
| Total | `lblThResTotalCon` | `438` | `416` | `100` | `Align.Right` |

Los **5 encabezados**: Etiqueta clásica · Y `shpHeadResumenCon.Y + 6` · Height `14` ·
**ESTILO-HEADER-COL** · `Text` `"RESUMEN POR MONEDA"`, `"CHEQUES"`, `"MONTO CHEQUES"`,
`"EFECTIVO"`, `"TOTAL"`.

Las **6 celdas del template** (`TemplateSize 26`):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblResMonedaCon` | Etiqueta clásica | X `0` · Y `0` · Width `150` · Height `26` · **ESTILO-CELDA** · `Text` `ThisItem.Moneda` |
| `lblResChqCon` | Etiqueta clásica | X `150` · Y `0` · Width `56` · Height `26` · **ESTILO-CELDA-NUM** · `Text` `Text(ThisItem.Cheques)` |
| `lblResMontoCon` | Etiqueta clásica | X `206` · Y `0` · Width `110` · Height `26` · **ESTILO-CELDA-NUM** · `Text` `fxMonto(ThisItem.Monto, ThisItem.Simbolo)` |
| `lblResEfectivoCon` | Etiqueta clásica | X `316` · Y `0` · Width `100` · Height `26` · **ESTILO-CELDA-NUM** · `Text` `fxMonto(ThisItem.Efectivo, ThisItem.Simbolo)` |
| `lblResTotalCon` | Etiqueta clásica | X `416` · Y `0` · Width `100` · Height `26` · **ESTILO-CELDA-NUM** con `FontWeight` `FontWeight.Semibold` · `Text` `fxMonto(ThisItem.Monto + ThisItem.Efectivo, ThisItem.Simbolo)` |
| `shpSepResumenCon` | Rectángulo clásico | X `0` · Y `galResumenCon.TemplateSize - 1` · Width `galResumenCon.Width` · Height `1` · `Fill` `ThemeLineSoft` |

> 🧨 **El banner dice *"Las validaciones pasaron"*, SIN el número.** El mockup escribe
> *"Las 6 validaciones pasaron"*, pero acá las validaciones son **siete** (`D04-11`: siete
> hallazgos, siete banners). Poner `"Las 7 validaciones pasaron"` ataría un literal al
> tamaño de `colHallazgosCon` y quedaría mintiendo el día que se agregue el hallazgo ocho;
> poner `"Las 6"` es directamente falso. Sin número, el texto es verdadero para cualquier
> cantidad y no hay nada que mantener sincronizado.

### 11.3 `btnDlgOkTerminarOkCon.OnSelect` — cerrar la tula como **Recontada**

```powerfx
// La acción destructiva vive en el botón CONFIRMAR, y en ningún otro lado (7.2, regla 2).
UpdateContext({ ctxDlgTerminarOkCon: false });
Clear(colFallidasCon);

// ---- 1. La bolsa. 🚫 La app NO escribe TotalNIO/USD/EUR: los recalcula el plugin ----
IfError(
    Set(
        gvarTula,
        Patch(
            'CH-Tbl-Bags',
            gvarTula,
            { BagStatus: 'BagStatus (CH-Tbl-Bags)'.Recontada }
        )
    ),
    If(true,
        Notify("No se pudo cerrar la tula: " & FirstError.Message, NotificationType.Error);
        Collect(colFallidasCon, { Clave: gvarTula.BagID, Detalle: "bolsa", Mensaje: FirstError.Message });
        Blank()
    )
);

If(
    !IsEmpty(colFallidasCon),

    // la bolsa no cambió de estado: NO se marcan cheques ni se cierra el detalle
    Notify("La tula sigue asignada a su mesa. Vuelva a intentar.", NotificationType.Error),

    // ---- 2. Marcar los cheques de la tula como Recontado (🧨 espejo E-2) ----
    //    Registro EXPLÍCITO dentro de un ForAll sobre la colección local: jamás
    //    UpdateIf(ds, predicado) de 2 saltos, que era el bug D-3 (solo los primeros 500).
    ForAll(
        Filter(colChecksTulaCon, CheckStatus <> 'CheckStatus (CH-Tbl-Checks)'.Recontado) As C,
        IfError(
            Patch('CH-Tbl-Checks', C, { CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.Recontado }),
            If(true,
                Collect(colFallidasCon, { Clave: C.CheckNumber, Detalle: "cheque", Mensaje: FirstError.Message });
                Blank()
            )
        )
    );

    // ---- 3. Cerrar el detalle de sesión ----
    IfError(
        Set(
            gvarSesionDetalle,
            Patch(
                'CH-Tbl-RecountingSessionDetails',
                gvarSesionDetalle,
                { RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.Cerrado }
            )
        ),
        If(true,
            Collect(colFallidasCon, { Clave: "detalle", Detalle: "sesión", Mensaje: FirstError.Message });
            Blank()
        )
    );

    // ---- 4. La pantalla queda en SOLO LECTURA y se vuelve al hub ----
    UpdateContext({ ctxSoloLecturaCon: true });
    Set(gvarDepositoSinGuardar, false);
    If(
        IsEmpty(colFallidasCon),
        Notify("Tula " & Trim(gvarTula.BagID) & " recontada.", NotificationType.Success),
        Notify(
            "La tula quedó Recontada, pero algo no se pudo marcar: " &
            Concat(colFallidasCon, Detalle & " " & Clave, ", ") & ". Avise a soporte.",
            NotificationType.Warning
        )
    );
    Navigate(scrTulas)
)
```

> 🚫 **La app NO crea `CH-Tbl-Transactions` ni escribe `TotalNIO/USD/EUR`** (`00` §2.2,
> puntos 2 y 3; mata **S-2**). La v1 armaba los asientos contables **desde el dispositivo
> del cajero**, sumando colecciones en memoria. En la v2 los genera el plugin al recibir la
> transición a `Recontada`, y `CH-Tbl-Transactions` **ni siquiera es un datasource de esta
> app** (`01` §2.1).

### 11.4 Overlay C — `C5`, terminar CON discrepancias (`cntDlgTerminarDifCon`)

Card **ancha (`560`)**, tono `warn`.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgTerminarDifCon` | Contenedor | receta 7.2 · **`Visible` `ctxDlgTerminarDifCon`** |
| `btnBloqTerminarDifCon` | Botón clásico | receta 7.2 |
| `cntCardTerminarDifCon` | Contenedor | receta 7.2 · Width `560` · Height `btnDlgOkTerminarDifCon.Y + btnDlgOkTerminarDifCon.Height + 19` |
| `btnIcoTerminarDifCon` | Botón clásico | receta 7.2 · `Fill`/`HoverFill`/`PressedFill` `ThemeWarnSoft` |
| `icoTipoTerminarDifCon` | Icono clásico **`Icon.Warning`** | receta 7.2 · `Color` `ThemeWarn` — es el ⚠ que dibuja el mockup `C5`, y el glifo del catálogo cerrado (`01` §6.1) para **todo tono `warn`**. `Icon.Lock` queda solo para el candado real de `B3` · **`AccessibleLabel` `""`** (decorativo, §14.2-12) |
| `lblDlgTitTerminarDifCon` | Etiqueta clásica | igual que `lblDlgTitTerminarOkCon` · `Text` `"Terminar tula " & Trim(gvarTula.BagID)` |
| `lblDlgTxtTerminarDifCon` | Etiqueta clásica | igual que `lblDlgTxtTerminarOkCon` · `Text` `With({n: CountRows(colHallazgosCon)}, "Se encontr" & If(n = 1, "ó ", "aron ") & Text(n) & If(n = 1, " hallazgo que requiere", " hallazgos que requieren") & " revisión del supervisor.")` |
| `icoDlgXTerminarDifCon` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelTerminarDifCon` |
| `galHallazgosCon` | Galería vertical en blanco | X `22` · Y `lblDlgTxtTerminarDifCon.Y + lblDlgTxtTerminarDifCon.Height + 14` · Width `Parent.Width - 44` · Height `264` · `TemplateSize` `66` · `TemplatePadding` `0` · `ShowScrollbar` `true` · `OnSelect` `false` · `Items` `SortByColumns(colHallazgosCon, "Orden", SortOrder.Ascending)` |
| `btnAvisoRevisionCon` | Botón clásico | **receta 7.2.a** · X `22` · Y `galHallazgosCon.Y + galHallazgosCon.Height + 8` · Width `Parent.Width - 44` · Height `58` · `Fill`/`HoverFill`/`PressedFill` `ThemeInfoSoft` · `Radius` (las 4) `9` · `Visible` `true` |
| `icoAvisoRevisionCon` | Icono clásico **`Icon.Information`** | receta 7.2.a · `Color` `ThemeInfo` — banner de tono `info` (ℹ del mockup) |
| `lblAvisoRevisionCon` | Etiqueta clásica | receta 7.2.a · `Color` `ThemeInfo` · `Text` `"Se enviará una solicitud de revisión al supervisor con este detalle. La tula queda en Pendiente de revisión y no puede seguir editándose."` |
| `btnDlgCancelTerminarDifCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkTerminarDifCon.X - 8 - Self.Width` · Y `btnDlgOkTerminarDifCon.Y` · Width `92` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgTerminarDifCon: false})` |
| `btnDlgOkTerminarDifCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 22 - Self.Width` · Y `btnAvisoRevisionCon.Y + btnAvisoRevisionCon.Height + 16` · Width `138` · `Text` `"Enviar a revisión"` · `OnSelect` §11.5 |

**Template de `galHallazgosCon`** (`TemplateSize 66` = banner de `58` + `8` de aire; X del
template = X absoluto − `22`):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAvisoHallazgoCon` | Botón clásico | receta 7.1 · X `0` · Y `0` · Width `galHallazgosCon.Width` · Height `58` · `Fill`/`HoverFill`/`PressedFill` `ThemeWarnSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAvisoHallazgoCon` | Icono clásico **`Icon.Warning`** | X `13` · Y `12` · Width `16` · Height `16` · `Color` `ThemeWarn` — banner de tono `warn` (⚠ del mockup) |
| `lblAvisoHallazgoCon` | Etiqueta clásica | X `38` · Y `0` · Width `galHallazgosCon.Width - 50` · Height `58` · Size `9` · `Color` `ThemeWarn` · **`Wrap` `true`** · `Text` `ThisItem.Texto` |

> **`Height 58` con `Wrap true`** entra **dos líneas** de `Size 9`. Los textos de §10.1
> están cortados con `FirstN(…, 4)` justamente para que quepan. Si en Studio alguno se
> recorta, se sube el `TemplateSize` a `84` y el alto del banner a `76` — **los dos juntos**,
> nunca uno solo.

### 11.5 `btnDlgOkTerminarDifCon.OnSelect` — armar el HTML y enviar a revisión

```powerfx
// ---- 1. SECCIÓN 1 — siempre las 3 monedas (nunca queda vacía) ----
UpdateContext({
    ctxSec1Con:
        fxHtmlFilaGlobal(
            "NIO", SimboloNIO,
            cmpMatNIOCon.ChequesRec, cmpMatNIOCon.MontoChequesRec, cmpMatNIOCon.EfectivoRec,
            cmpMatNIOCon.ChequesDec, cmpMatNIOCon.MontoChequesDec, cmpMatNIOCon.EfectivoDec
        ) &
        fxHtmlFilaGlobal(
            "USD", SimboloUSD,
            cmpMatUSDCon.ChequesRec, cmpMatUSDCon.MontoChequesRec, cmpMatUSDCon.EfectivoRec,
            cmpMatUSDCon.ChequesDec, cmpMatUSDCon.MontoChequesDec, cmpMatUSDCon.EfectivoDec
        ) &
        fxHtmlFilaGlobal(
            "EUR", SimboloEUR,
            cmpMatEURCon.ChequesRec, cmpMatEURCon.MontoChequesRec, cmpMatEURCon.EfectivoRec,
            cmpMatEURCon.ChequesDec, cmpMatEURCon.MontoChequesDec, cmpMatEURCon.EfectivoDec
        )
});

// ---- 2. SECCIÓN 2 — diferencias por transacción ----
UpdateContext({
    ctxSec2Con:
        Concat(
            FirstN(SortByColumns(colDifTrxCon, "Indice"), 25) As T,
            fxHtmlFilaTrx(T.Cuenta, T.Moneda, fxSimbolo(T.Moneda), T.DifCheques, T.DifEfectivo, Mod(T.Indice, 2) = 1)
        ) &
        If(
            CountRows(colDifTrxCon) > 25,
            "<tr><td colspan='5'>… y " & Text(CountRows(colDifTrxCon) - 25) & " más</td></tr>",
            ""
        )
});

// ---- 3. SECCIÓN 3 — depósitos recontados sin enlazar ----
UpdateContext({
    ctxSec3Con:
        Concat(
            FirstN(SortByColumns(colDepRecSinEnlazarCon, "Indice"), 25) As D,
            fxHtmlFilaDeposito(D.Cuenta, D.Moneda, fxSimbolo(D.Moneda), D.Cheques, D.Efectivo, Mod(D.Indice, 2) = 1)
        ) &
        If(
            CountRows(colDepRecSinEnlazarCon) > 25,
            "<tr><td colspan='5'>… y " & Text(CountRows(colDepRecSinEnlazarCon) - 25) & " más</td></tr>",
            ""
        )
});

// ---- 4. SECCIÓN 4 — cheques declarados sin enlazar (la ÚNICA con subtotal) ----
UpdateContext({
    ctxSec4Con:
        Concat(
            FirstN(SortByColumns(colChqDecSinEnlazarCon, "Indice"), 25) As C,
            fxHtmlFilaCheque(C.Numero, C.Tipo, C.Moneda, fxSimbolo(C.Moneda), C.Monto, C.Deposito, Mod(C.Indice, 2) = 1)
        ) &
        If(
            CountRows(colChqDecSinEnlazarCon) > 25,
            "<tr><td colspan='5'>… y " & Text(CountRows(colChqDecSinEnlazarCon) - 25) & " más</td></tr>",
            ""
        ) &
        // 🧨 SUBTOTAL POR MONEDA: una fila `sub` por moneda presente, con SU símbolo.
        //    Las monedas sin cheques no emiten fila. JAMÁS un subtotal que sume monedas
        //    distintas: el subtotal es lo único de la sección que podía mentir (cada FILA
        //    ya sale con fxSimbolo(C.Moneda)).
        Concat(
            Filter(
                ForAll(
                    Table({ M: "NIO" }, { M: "USD" }, { M: "EUR" }) As Q,
                    {
                        Mon:  Q.M,
                        Tot:  Coalesce(Sum(Filter(colChqDecSinEnlazarCon, Moneda = Q.M), Monto), 0),
                        Cant: CountRows(Filter(colChqDecSinEnlazarCon, Moneda = Q.M))
                    }
                ),
                Cant > 0
            ) As S,
            fxHtmlFilaTotalCheques(fxSimbolo(S.Mon), S.Tot, S.Cant)
        )
});

// ---- 5. SECCIÓN 5 — cheques agregados manualmente ----
UpdateContext({
    ctxSec5Con:
        Concat(
            FirstN(SortByColumns(colChqManualCon, "Indice"), 25) As M,
            fxHtmlFilaCheque(M.Numero, M.Tipo, M.Moneda, fxSimbolo(M.Moneda), M.Monto, M.Deposito, Mod(M.Indice, 2) = 1)
        ) &
        If(
            CountRows(colChqManualCon) > 25,
            "<tr><td colspan='5'>… y " & Text(CountRows(colChqManualCon) - 25) & " más</td></tr>",
            ""
        )
});

// ---- 6. SECCIÓN 6 — depósitos declarados sin enlazar ----
UpdateContext({
    ctxSec6Con:
        Concat(
            FirstN(SortByColumns(colDepDecSinEnlazarCon, "Indice"), 25) As D,
            fxHtmlFilaDeposito(D.Cuenta, D.Moneda, fxSimbolo(D.Moneda), D.Cheques, D.Efectivo, Mod(D.Indice, 2) = 1)
        ) &
        If(
            CountRows(colDepDecSinEnlazarCon) > 25,
            "<tr><td colspan='5'>… y " & Text(CountRows(colDepDecSinEnlazarCon) - 25) & " más</td></tr>",
            ""
        )
});

// ---- 7. EL DOCUMENTO — se calcula UNA vez y se pasa al Patch ----
UpdateContext({
    ctxHtmlCon:
        fxHtmlRevision(
            If(IsBlank(gvarDeclaredBag.'CH-Tbl-DeclaredBags'), "Reconteo sin declaración", "Revisión de reconteo"),
            Trim(gvarTula.BagID),
            gvarTula.ReceiptIdMirror,
            gvarTula.ReceptionID.RemitentName,
            gvarUser.'Full Name',
            gvarMesa.Name,
            Text(gvarSesion.'Created On', FmtFechaCorta),
            ctxSec1Con, ctxSec2Con, ctxSec3Con, ctxSec4Con, ctxSec5Con, ctxSec6Con
        )
});

// ---- 8. LA SOLICITUD (01 §5.7). El Patch va envuelto en un Set: el polling
//         pregunta por PK, no adivina cuál es "su" solicitud. ----
IfError(
    Set(
        gvarSolicitud,
        Patch(
            'CH-Tbl-ApprovalRequests',
            Defaults('CH-Tbl-ApprovalRequests'),          // Name es AUTONUMBER: no se toca
            {
                RequestType: If(
                                 IsBlank(gvarTula.BagDeclarationID.'CH-Tbl-DeclaredBags'),
                                 "Tula recontada sin declaracion",
                                 "Tula con diferencia"
                             ),
                BagNumber:      Trim(gvarTula.BagID),
                RequestDetails: ctxHtmlCon,
                RequestResources: JSON({ BagGUID: Text(gvarTula.'CH-Tbl-Bags') })
                // ApprovalStatus NO se setea: en blanco = Pendiente (01 §5.5) — mata S-1
            }
        )
    ),
    If(true,
        Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error);
        Set(gvarSolicitud, Blank());
        Blank()
    )
);

// ---- 9. Sin solicitud NO se mueve la bolsa: el cajero se quedaría bloqueado sin cola ----
If(
    IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest'),

    UpdateContext({ ctxDlgTerminarDifCon: false }),

    IfError(
        Set(
            gvarTula,
            Patch(
                'CH-Tbl-Bags',
                gvarTula,
                { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento' }
            )
        ),
        Notify("La solicitud se creó pero la tula no cambió de estado: " & FirstError.Message, NotificationType.Error)
    );
    IfError(
        Set(
            gvarSesionDetalle,
            Patch(
                'CH-Tbl-RecountingSessionDetails',
                gvarSesionDetalle,
                { RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.'Cerrado con diferencias' }
            )
        ),
        Notify("El detalle de sesión no se pudo cerrar: " & FirstError.Message, NotificationType.Error)
    );

    // 🧨 ctxSoloLecturaCon se DERIVA de gvarTula, no se fuerza a `true` (§0.2 y §12.3):
    //    los dos Patch de arriba van con IfError y pueden haber fallado. Si el BagStatus
    //    no se movió, la tula sigue editable y la pantalla tiene que decir la verdad —
    //    congelarla igual dejaría al cajero mirando un conteo que sí puede corregir.
    //    Si el Patch entró, gvarTula ya trae `Pendiente de revision…` y la conjunción da
    //    `true` sola. Misma forma que la rama Rechazado del polling (§12.3).
    UpdateContext({
        ctxSoloLecturaCon:
            gvarTula.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',
        ctxDlgTerminarDifCon: false,
        ctxEsperandoCon:    true,
        ctxRechazadaCon:    false
    })
)
```

**Reglas del armado del HTML** (`01` §7.6):

1. **Cero escapes manuales en la pantalla.** El escape vive **dentro** de las `fx…` de
   `01` §3.1. Si en esta guía ves un `Substitute(…, "&", "&amp;")`, es un bug: el dato se
   escaparía **dos veces**. Mata **S-3** (la v1 concatenaba `CheckNumber`, que es texto
   libre, sin escapar).
2. **`FirstN(…, 25)` en cada sección** por el límite de `ntext(10000)` (`01` §3.1, regla 5),
   con la fila *"… y N más"*.
3. **`ctxHtmlCon` se calcula UNA vez** y se pasa al `Patch`. Nunca se rearma adentro.
4. **El tipo de solicitud lo decide `IsBlank(gvarTula.BagDeclarationID)`**, no una variable
   paralela (`01` §5.6).
5. **`RequestResources` se arma con `JSON({…})`**, jamás por concatenación de comillas
   (lo que hacía la v1). Para los dos tipos de `04` la clave es **una sola**: `BagGUID`
   (`01` §5.7).
6. 🚫 **La app NUNCA setea `ApprovalStatus`** — ni siquiera cuando "sabe" que va a
   aprobarse. Es el bypass **S-1**.

> ✅ **`D04-13` — RESUELTA en la consolidación: el subtotal de la sección 4 va POR MONEDA.**
> `fxHtmlFilaTotalCheques` recibe **un** símbolo, y la sección puede mezclar monedas. Cada
> **fila** ya llevaba su símbolo correcto (`fxSimbolo(C.Moneda)`); el que mentía era el
> **subtotal**. La solución **no toca `01` §3.1**: se **llama a la misma `fx…` una vez por
> moneda presente**, con su propio símbolo, su propio total y su propia cantidad, y las
> monedas sin cheques no emiten fila. Con una sección de una sola moneda —el caso normal—
> el HTML sale **idéntico** al de antes. Cero cambios en `App.Formulas`.

---

## 12. Espera de aprobación y rechazo (receta `01` §7.7 y contrato §5.8)

### 12.1 Overlay E — `C7`, esperando aprobación (`cntEsperaCon`)

Card **normal (`400`)**. **Sin ✕**: es un estado del servidor, no una decisión del cajero.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntEsperaCon` | Contenedor | receta 7.2 · **`Visible` `ctxEsperandoCon`** |
| `btnBloqEsperaCon` | Botón clásico | receta 7.2 |
| `cntCardEsperaCon` | Contenedor | receta 7.2 · Width `400` · Height `btnVolverTulasEspCon.Y + btnVolverTulasEspCon.Height + 19` |
| `lblDlgTitEsperaCon` | Etiqueta clásica | X `22` · Y `17` · Width `Parent.Width - 44` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Esperando aprobación del supervisor"` |
| `lblDlgTxtEsperaCon` | Etiqueta clásica | X `22` · Y `lblDlgTitEsperaCon.Y + lblDlgTitEsperaCon.Height + 6` · Width `Parent.Width - 44` · **ESTILO-DLG-TEXTO** · `AutoHeight` `true` · `Text` `"La pantalla queda bloqueada hasta que el supervisor responda."` |
| `btnFilaEsperaCon` | Botón clásico | receta 7.1 (`.waitRow`) · X `22` · Y `lblDlgTxtEsperaCon.Y + lblDlgTxtEsperaCon.Height + 14` · Width `Parent.Width - 44` · Height `62` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `11` · `DisplayMode` `DisplayMode.View` |
| `spnEsperaCon` | **Spinner moderno** | X `btnFilaEsperaCon.X + 16` · Y `btnFilaEsperaCon.Y + 14` · Width `34` · Height `34` · `Visible` `ctxEsperandoCon` |
| `lblEsperaTitCon` | Etiqueta clásica | X `spnEsperaCon.X + spnEsperaCon.Width + 14` · Y `btnFilaEsperaCon.Y + 12` · Width `Parent.Width - Self.X - 30` · Height `18` · **ESTILO-CELDA-FUERTE** · `Text` `"Solicitud de revisión enviada"` |
| `lblEsperaSubCon` | Etiqueta clásica | X `lblEsperaTitCon.X` · Y `lblEsperaTitCon.Y + lblEsperaTitCon.Height` · Width `lblEsperaTitCon.Width` · Height `18` · **ESTILO-META-CHICA** · `Text` `"Consultando estado cada 15 segundos · enviada " & Text(gvarSolicitud.'Created On', FmtHora)` |
| `lblKvTulaEtqEspCon` / `lblKvTulaEspCon` | Etiquetas clásicas | **receta 7.2.b** · Y `btnFilaEsperaCon.Y + btnFilaEsperaCon.Height + 12` · `Text` `"Tula"` / `Trim(gvarTula.BagID)` |
| `lblKvPlanEtqEspCon` / `lblKvPlanEspCon` | Etiquetas clásicas | receta 7.2.b · Y `lblKvTulaEtqEspCon.Y + 22` · `Text` `"Planilla"` / `gvarTula.ReceiptIdMirror` |
| `lblKvHallEtqEspCon` / `lblKvHallEspCon` | Etiquetas clásicas | receta 7.2.b · Y `lblKvPlanEtqEspCon.Y + 22` · `Text` `"Hallazgos"` / ver abajo |
| `lblKvEnvEtqEspCon` / `lblKvEnvEspCon` | Etiquetas clásicas | receta 7.2.b · Y `lblKvHallEtqEspCon.Y + 22` · `Text` `"Enviada"` / `Text(gvarSolicitud.'Created On', FmtFecha)` |
| `btnVolverTulasEspCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `Parent.Width - 22 - Self.Width` · Y `lblKvEnvEtqEspCon.Y + 22 + 16` · Width `146` · `Text` `"Volver a las tulas"` · `OnSelect` `Navigate(scrTulas)` |

```powerfx
// lblKvHallEspCon.Text — 🧨 APROXIMACIÓN HONESTA, no un dato recuperable.
//   colHallazgosCon es LOCAL y el OnVisible la VACÍA (§1, bloque 3): si el cajero cerró
//   la app y volvió a entrar, la solicitud se recupera (Q4) pero los hallazgos NO — se
//   calcularon en btnTerminarCon.OnSelect y nunca se persistieron. `Text(CountRows(…))`
//   sobre una colección vacía imprime un **0** que es MENTIRA: siempre hubo al menos uno,
//   porque sin hallazgos la tula termina por C4 y no hay solicitud que esperar.
If(IsEmpty(colHallazgosCon), "—", Text(CountRows(colHallazgosCon)))
```

> 🧨 **El `—` es la aproximación honesta, y está declarada** (`D04-26`, mismo criterio que
> `D03-2` con la columna *Actividad*): cuando el número existe se muestra; cuando la app no
> lo tiene, se dice que no lo tiene en vez de inventar un `0`. **El detalle completo de los
> hallazgos viaja en el HTML de la solicitud** (§11.5), que es donde el supervisor lo lee y
> el único lugar donde está persistido. Camino de mejora, fuera de esta guía: una columna
> `FindingsCount` en `CH-Tbl-ApprovalRequests` escrita al crear la solicitud.

> 🧨 Este botón **NO limpia `ctxEsperandoCon` ni `gvarSolicitud`, a propósito.** La
> solicitud sigue pendiente del lado del servidor: salir de la tula no la cancela. Al volver
> a abrir la misma tula, la consulta de recuperación **Q4** (§1, bloque 1) la vuelve a
> encontrar y el bloque 5 vuelve a levantar `ctxEsperandoCon` ⇒ el overlay `C7` reaparece
> solo. Por eso, y solo por eso, este `OnSelect` es **asimétrico** con el de
> `btnVolverTulasRecCon` (§12.2), que sí limpia: ahí la solicitud ya está **resuelta**.

> 🧨 **Divergencia `D04-14` — el `kv` dice *Enviada*, no *Supervisor*.** El mockup `C7`
> muestra *"Supervisor: R. Sequeira"*, pero **mientras la solicitud está PENDIENTE nadie la
> resolvió todavía**: `SolvedBy` está vacío por definición (`01` §2.4). Poner ahí un nombre
> exigiría un lookup de "supervisor de guardia" que el modelo no tiene. Se muestra la fecha
> y hora de envío, que sí existe y sí le sirve al cajero.

> **`FmtFecha` lleva `HH` (24 h), nunca `hh`** — `hh` es reloj de 12 h **sin AM/PM**
> (`01` §3.1).

### 12.2 Overlay F — `C8`, solicitud rechazada (`cntRechazoCon`)

Card **normal (`400`)**, tono `danger`.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntRechazoCon` | Contenedor | receta 7.2 · **`Visible` `ctxRechazadaCon`** |
| `btnBloqRechazoCon` | Botón clásico | receta 7.2 |
| `cntCardRechazoCon` | Contenedor | receta 7.2 · Width `400` · Height `btnCorregirCon.Y + btnCorregirCon.Height + 19` |
| `btnIcoRechazoCon` | Botón clásico | receta 7.2 · `Fill`/`HoverFill`/`PressedFill` `ThemeDangerSoft` |
| `icoTipoRechazoCon` | Icono clásico `Icon.Blocked` | receta 7.2 · `Color` `ThemeDanger` · **`AccessibleLabel` `""`** (decorativo, §14.2-12) |
| `lblDlgTitRechazoCon` | Etiqueta clásica | X `btnIcoRechazoCon.X + btnIcoRechazoCon.Width + 10` · Y `17` · Width `Parent.Width - Self.X - 44` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Solicitud rechazada"` |
| `lblDlgTxtRechazoCon` | Etiqueta clásica | X `lblDlgTitRechazoCon.X` · Y `lblDlgTitRechazoCon.Y + lblDlgTitRechazoCon.Height + 4` · Width `Parent.Width - Self.X - 22` · **ESTILO-DLG-TEXTO** · `AutoHeight` `true` · `Text` `"El supervisor rechazó la revisión de la tula " & Trim(gvarTula.BagID) & "."` |
| `icoDlgXRechazoCon` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnCorregirCon` |
| `btnAvisoRechazoCon` | Botón clásico | **receta 7.2.a** · X `22` · Y `lblDlgTxtRechazoCon.Y + lblDlgTxtRechazoCon.Height + 14` · Width `Parent.Width - 44` · Height `76` · `Fill`/`HoverFill`/`PressedFill` `ThemeDangerSoft` · `Radius` (las 4) `9` · `Visible` `true` |
| `icoAvisoRechazoCon` | Icono clásico `Icon.Blocked` | receta 7.2.a · `Color` `ThemeDanger` |
| `lblAvisoRechazoCon` | Etiqueta clásica | receta 7.2.a · `Height` `btnAvisoRechazoCon.Height` · `Color` `ThemeDanger` · **`Wrap` `true`** · `Text` ver abajo |
| `lblNotaRechazoCon` | Etiqueta clásica | X `22` · Y `btnAvisoRechazoCon.Y + btnAvisoRechazoCon.Height + 10` · Width `Parent.Width - 44` · Height `34` · **ESTILO-META-CHICA** · `Wrap` `true` · `Text` `"El rechazo NO libera la tula: sigue en su mesa y en estado Asignada a cajero hasta que corrija y reenvíe."` |
| `btnVolverTulasRecCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnCorregirCon.X - 8 - Self.Width` · Y `btnCorregirCon.Y` · Width `146` · `Text` `"Volver a las tulas"` · `OnSelect` `UpdateContext({ctxRechazadaCon: false}); Set(gvarSolicitud, Blank()); Navigate(scrTulas)` |
| `btnCorregirCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 22 - Self.Width` · Y `lblNotaRechazoCon.Y + lblNotaRechazoCon.Height + 16` · Width `146` · `Text` `"Corregir el conteo"` · `OnSelect` `UpdateContext({ctxRechazadaCon: false}); Set(gvarSolicitud, Blank())` |

```powerfx
// lblAvisoRechazoCon.Text — el MOTIVO que escribió el supervisor, tal cual
"Rechazada por " & Coalesce(gvarSolicitud.SolvedBy.'Full Name', "el supervisor") & " · " &
Text(gvarSolicitud.ResolutionDatetime, FmtFecha) & Char(10) &
Coalesce(gvarSolicitud.StatusReason, "Sin motivo registrado.")
```

> 🧨 **El rechazo BLOQUEA, no libera** (`01` §5.8, mata **S-4**). La v1 preguntaba
> `IsBlank(LookUp(…, ApprovalStatus = Blank()))` y **fallaba abierto**: un rechazo liberaba
> al cajero exactamente igual que una aprobación, porque en los dos casos la solicitud
> dejaba de estar pendiente.

> **`gvarSolicitud.SolvedBy.'Full Name'` es navegación sobre un registro YA CARGADO** (el que
> devolvió el `LookUp` por PK del tick). No es D-1.

### 12.3 `tmrPollCon` — un timer, 15 s, y se apaga solo

| Propiedad | Valor |
|---|---|
| Tipo | **Temporizador clásico** |
| `AutoStart` | `false` |
| `Duration` | `15000` |
| `Repeat` | `true` |
| `Start` | **`ctxEsperandoCon && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`** (contrato `01` §5.8) |
| `Visible` | `false` |
| `OnTimerStart` | **vacío** — cero `Refresh` |
| `OnTimerEnd` | abajo |

```powerfx
// ====================== tmrPollCon.OnTimerEnd ======================
// UNA sola consulta, delegable, por PK.
Set(
    gvarSolicitud,
    LookUp(
        'CH-Tbl-ApprovalRequests',
        'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest'
    )
);

If(
    // ---- APROBADO: el supervisor aceptó la revisión. La tula queda cerrada. ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
        Refresh('CH-Tbl-Bags');                                   // D-5, forma 2
        Set(gvarTula, LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'));
        UpdateContext({ ctxEsperandoCon: false, ctxRechazadaCon: false });
        Notify(
            "El supervisor aprobó la revisión de la tula " & Trim(gvarTula.BagID) & ".",
            NotificationType.Success
        );
        Navigate(scrTulas),

    // ---- RECHAZADO: BLOQUEA, no libera. Y devuelve la tula al cajero. ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
        Refresh('CH-Tbl-Bags');
        IfError(
            Set(
                gvarTula,
                Patch(
                    'CH-Tbl-Bags',
                    LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
                    { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero' }
                )
            ),
            Notify(
                "El supervisor rechazó la solicitud, pero la tula no volvió a Asignada a cajero: " &
                FirstError.Message,
                NotificationType.Error
            )
        );
        IfError(
            Set(
                gvarSesionDetalle,
                Patch(
                    'CH-Tbl-RecountingSessionDetails',
                    gvarSesionDetalle,
                    { RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.'En progreso' }
                )
            ),
            Notify("El detalle de sesión no volvió a En progreso: " & FirstError.Message, NotificationType.Error)
        );
        UpdateContext({
            ctxEsperandoCon:   false,
            ctxRechazadaCon:   true,
            ctxSoloLecturaCon: gvarTula.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'
        }),

    // ---- ni una ni otra: SIGUE PENDIENTE. No se hace NADA. ----
    false
)
```

**Reglas del polling** (`01` §7.7):

1. **UN timer por pantalla, y ninguno más.** La v1 tenía **tres**: dos con `Repeat true` a
   5 s con `Refresh` en `OnTimerStart` (≈10 consultas en cascada por ciclo) y uno con
   `Start: =true` **literal**, corriendo siempre aunque no hubiera nada que esperar.
2. **`Start` es una condición, jamás `true`.** Cuando la solicitud se resuelve, la condición
   se cae sola y el timer se apaga: no hace falta apagarlo a mano.
3. **`OnTimerStart` vacío.** Un `Refresh` ahí invalida la caché de todas las tablas
   relacionadas en **cada tick**.
4. **`Refresh('CH-Tbl-Bags')` solo en las dos ramas de resolución** (D-5, forma 2), y **solo
   esa tabla**.
5. **El `Navigate` de la rama *Aprobado* vive DENTRO del `OnTimerEnd`.** Los timers no
   corren en pantallas no visibles.

> ✅ **`D04-15` — RESUELTA por consolidación: hoy es CONTRATO en `01` §5.8.** El contrato
> madre dice ahora, explícitamente, que el rechazo de `"Tula con diferencia"` /
> `"Tula recontada sin declaracion"` **devuelve la bolsa a `Asignada a cajero` y el detalle
> a `En progreso`**. Motivo: la app **sí** mueve la bolsa a `Pendiente de revision por
> supervisor - recuento` al enviar (§11.5) —si no, el estado mentiría mientras el supervisor
> mira— y dejarla ahí tras un rechazo la deja **muerta**: en `Pendiente` la pantalla está en
> solo lectura (§0.2) y el cajero no puede corregir nada, que es justo lo que el rechazo le
> pide.
>
> 🧨 **Verificado contra `CH-App-Supervisor`** (`Screen Approvals.fx.yaml`): al **rechazar**,
> el supervisor **ya** hace `Patch('CH-Tbl-Bags', …, { BagStatus: 'Asignada a cajero' })`
> para los dos tipos. Este `Patch` es por lo tanto **idempotente y de refuerzo** —escribe el
> mismo valor—, no una carrera; y si mañana esa lógica se muda a un plugin y desaparece del
> canvas del supervisor, `04` sigue devolviendo la tula al cajero.

---

## 13. Salida de la tula y las tres confirmaciones simples (`C6`)

### 13.1 Guarda de salida — Overlay D (`cntDlgSalirCon`, 3 botones)

```powerfx
// ====================== btnCerrarTulaCon.OnSelect ======================
// (btnVolverCon.OnSelect es literalmente Select(btnCerrarTulaCon) — D04-3)
If(
    gvarDepositoSinGuardar,
    UpdateContext({ ctxDlgSalirCon: true }),
    Navigate(scrTulas)
)
```

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgSalirCon` | Contenedor | receta 7.2 · **`Visible` `ctxDlgSalirCon`** |
| `btnBloqSalirCon` | Botón clásico | receta 7.2 |
| `cntCardSalirCon` | Contenedor | receta 7.2 · Width `400` · Height `btnDlgGuardarSalirCon.Y + btnDlgGuardarSalirCon.Height + 19` |
| `lblDlgTitSalirCon` | Etiqueta clásica | X `22` · Y `17` · Width `Parent.Width - 66` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Hay un depósito sin guardar"` |
| `lblDlgTxtSalirCon` | Etiqueta clásica | X `22` · Y `lblDlgTitSalirCon.Y + lblDlgTitSalirCon.Height + 6` · Width `Parent.Width - 44` · **ESTILO-DLG-TEXTO** · `AutoHeight` `true` · `Text` ver abajo |
| `icoDlgXSalirCon` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelSalirCon` |
| `btnDlgCancelSalirCon` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `22` · Y `btnDlgGuardarSalirCon.Y` · Width `92` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgSalirCon: false})` |
| `btnDlgDescartarSalirCon` | Botón clásico | **ESTILO-BTN-CONTORNO** · X `btnDlgGuardarSalirCon.X - 8 - Self.Width` · Y `btnDlgGuardarSalirCon.Y` · Width `126` · `Text` `"Descartar y salir"` · `OnSelect` `UpdateContext({ctxDlgSalirCon: false}); Set(gvarDepositoSinGuardar, false); Navigate(scrTulas)` |
| `btnDlgGuardarSalirCon` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 22 - Self.Width` · Y `lblDlgTxtSalirCon.Y + lblDlgTxtSalirCon.Height + 20` · Width `124` · `Text` `"Guardar y salir"` · `OnSelect` ver abajo |

```powerfx
// lblDlgTxtSalirCon.Text — plural natural, como el mockup (nunca "unidad(es)")
With(
    {
        u: Sum(colDetalleConteoCon, Auto + Manual),
        c: CountRows(Filter(colChecksCon, Seleccionado))
    },
    "El depósito en curso tiene " &
    Text(u) & If(u = 1, " unidad", " unidades") & " de efectivo y " &
    Text(c) & If(c = 1, " cheque", " cheques") & " sin guardar. Si sale ahora se pierden."
)

// btnDlgGuardarSalirCon.OnSelect
// 🧨 El Navigate NO va acá: va en el paso 9 del OnTimerEnd (§9.2). Los timers no
//    corren en pantallas no visibles ⇒ navegar antes MATA el guardado.
UpdateContext({ ctxDlgSalirCon: false, ctxSalirTrasGuardarCon: true });
Select(btnGuardarDepositoCon)
```

> 🧨 **La ✕ y *Cancelar* no descartan nada**: se quedan en la tula. Solo *Descartar y salir*
> pierde el conteo, y lo dice con números.

### 13.2 `cmpDlgMonedaCon` — cambiar la moneda del depósito (`C6-1`)

Instancia de **`cmpDialogo`** (`01` §8.3). X `0` · Y `0` · Width `1366` · Height `768`.

```powerfx
Visible        = ctxDlgMonedaCon
Titulo         = "Cambiar la moneda del depósito"
Subtitulo      = "El depósito en curso pasa de " & ctxMonedaCon & " a " & Left(ddMonedaCon.Selected.Value, 3) &
                 ". La grilla de denominaciones se rehace y se pierde lo capturado: " &
                 Text(Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Auto + Manual)) & " billetes y " &
                 Text(Sum(Filter(colDetalleConteoCon, Tipo = "Moneda"), Manual)) & " monedas." & Char(10) & Char(10) &
                 "Los cheques ya marcados se desmarcan."
TextoConfirmar = "Cambiar moneda"
TextoCancelar  = "Cancelar"
Peligro        = false
Contorno       = false
AnchoCard      = 330
Icono          = "sync"          // ⇄ del mockup C6-1
Tono           = "info"
MostrarCerrar  = false           // el mockup dibuja Cancelar y nada más
OnCancelar     = UpdateContext({ctxDlgMonedaCon: false}); Reset(ddMonedaCon)
OnConfirmar    = UpdateContext({ctxDlgMonedaCon: false}); Select(btnRehacerMonedaCon)
```

> **`Select(btnRehacerMonedaCon)` es el Bloque M (§6.5)**, y es la **única** definición del
> rehacer: el otro llamador (`ddMonedaCon.OnChange`, rama sin datos capturados, §7.2) invoca
> el mismo motor. El motor lee la moneda de `ddMonedaCon.Selected.Value`, que ya cambió
> cuando el `OnChange` disparó.

> 🧨 **El `OnCancelar` hace `Reset(ddMonedaCon)`**: el desplegable ya cambió visualmente
> cuando disparó su `OnChange`. Su `Default` está anclado a `ctxMonedaCon`, que **no** se
> tocó, así que el `Reset` lo devuelve al valor real. Sin eso, la pantalla dice `USD` y la
> colección sigue en `NIO`.

### 13.3 `cmpDlgEliminarCon` — eliminar el depósito (`C6-2`)

Lo abre el botón visible; **la eliminación vive en `btnEliminarDepositoCon.OnSelect`** (§5.5)
y el diálogo solo la **dispara** (`01` §8.3, ejemplo de uso).

```powerfx
Visible        = ctxDlgEliminarCon
Titulo         = "Eliminar el depósito " & Text(ctxDepSelCon.AccountNumber)
Subtitulo      = "Se eliminan el desglose de denominaciones, los cheques manuales y el enlace con la declaración. Esta acción no se puede deshacer." & Char(10) & Char(10) &
                 "Efectivo: " & fxMonto(ctxDepSelCon.CashAmount, fxSimbolo(Text(ctxDepSelCon.Currency))) & Char(10) &
                 "Cheques: " & Text(CountRows(Filter(colChecksTulaCon, RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = ctxDepSelCon.'CH-Tbl-DepositsRecounting')))
TextoConfirmar = "Eliminar"
TextoCancelar  = "Cancelar"
Peligro        = true
Contorno       = false
AnchoCard      = 330
Icono          = "trash"         // 🗑 del mockup C6-2
Tono           = "danger"
MostrarCerrar  = false
OnCancelar     = UpdateContext({ctxDlgEliminarCon: false})
OnConfirmar    = UpdateContext({ctxDlgEliminarCon: false}); Select(btnEliminarDepositoCon)
```

> 🧨 **Divergencia `D04-23` — el `kv` del mockup se degrada a dos renglones de texto.** El
> mockup `C6-2` dibuja *Efectivo* y *Cheques* como un bloque `kv` (etiqueta gris a la
> izquierda, valor en semibold a la derecha, receta `01` §7.2.b). Acá van **dentro de
> `Subtitulo`**, separados con `Char(10)`, en una sola etiqueta y un solo peso. Es
> consecuencia directa de **`L-10`**: un componente canvas **no puede recibir controles
> hijos**, así que `cmpDialogo` no admite las cuatro etiquetas que un `kv` necesita. La
> alternativa era rearmar `C6-2` con la receta 7.2 suelta —11 controles más para dos
> renglones— y perder la única definición del chrome. Se prefirió el texto.

> **Quién abre el diálogo**: el botón visible de la botonera es `btnEliminarDepositoCon`,
> pero su `OnSelect` **es la eliminación**. Para no duplicar, en Studio el botón de la
> botonera lleva `OnSelect: UpdateContext({ctxDlgEliminarCon: true})` **solo si** se lo
> renombra; como el árbol tiene **un** botón, la regla es:
>
> 🧨 **`btnEliminarDepositoCon.OnSelect` empieza con la guarda del diálogo, y el bloque
> COMPLETO —guarda + volcado + los 6 pasos— está escrito una sola vez, en §5.5.** Acá no se
> repite: si se repitiera, serían dos definiciones de una acción que borra dinero contado.
>
> El `OnConfirmar` del diálogo pone `ctxEliminandoCon: true` **antes** del
> `Select(btnEliminarDepositoCon)`. Así hay **un** control, **una** definición de la
> eliminación, y **confirmar antes de ejecutar**. `ctxEliminandoCon` se suma al inventario
> de variables de §CONTRATO (booleano, arranca en `false` en el `OnVisible`, bloque 5).
>
> ```powerfx
> // cmpDlgEliminarCon.OnConfirmar — el orden IMPORTA: el discriminador va primero
> UpdateContext({ ctxDlgEliminarCon: false, ctxEliminandoCon: true });
> Select(btnEliminarDepositoCon)
> ```
>
> 🧨 **Por qué acá NO se usa un motor 1×1, si la pantalla tiene cuatro (§6).** Los cuatro
> motores de §6 factorizan **rutinas sin dueño**: volcar, cargar, vaciar, rehacer. No hay
> ningún control de la UI que "sea" esas acciones, así que un botón invisible es el lugar
> natural donde vivan. *Eliminar el depósito* es distinto: **el botón visible de la botonera
> ES la acción** (§9.1). Agregarle un motor gemelo serían **dos** controles con la misma
> acción destructiva — exactamente lo que la receta 7.2 prohíbe—, así que el mismo control
> cumple los dos papeles y el discriminador `ctxEliminandoCon` los separa.
>
> Las dos formas cumplen la misma regla —*una sola definición de la acción, ejecutada solo
> después del confirmar*— y la diferencia es **si el usuario toca o no el control**. Lo que
> comparten sin excepción, y es lo que dice `L-53`:
> **ningún botón que ABRE un diálogo contiene `Clear(`, `Remove(` o `Patch(`** (checklist
> §16), y **`Select()` NUNCA apunta a un control con `Visible: false`** — ni los cuatro
> motores de §6 (que son `Visible: true`, de 1 × 1 y transparentes) ni
> `btnEliminarDepositoCon` (que está siempre visible).

### 13.4 `cmpDlgDescartarCon` — descartar los cambios del depósito (`C6-3`)

```powerfx
Visible        = ctxDlgDescartarCon
Titulo         = "Descartar los cambios del depósito"
Subtitulo      = "El depósito en curso no está guardado. Si lo descarta se pierden " &
                 Text(Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Auto + Manual)) & " billetes, " &
                 Text(Sum(Filter(colDetalleConteoCon, Tipo = "Moneda"), Manual)) & " monedas y " &
                 Text(CountRows(Filter(colChecksCon, Seleccionado))) & " cheques capturados."
TextoConfirmar = "Descartar"
TextoCancelar  = "Seguir capturando"
Peligro        = false
Contorno       = true            // el mockup C6-3 dibuja "Descartar" con contorno, no lleno
AnchoCard      = 330
Icono          = "sync"          // ↺ del mockup C6-3
Tono           = "warn"
MostrarCerrar  = false
OnCancelar     = UpdateContext({ctxDlgDescartarCon: false})
OnConfirmar    = UpdateContext({ctxDlgDescartarCon: false});
                 If(
                     // era un depósito NUEVO: el panel vuelve al estado vacío del mockup C4
                     IsBlank(ctxDepositoCon.'CH-Tbl-DepositsRecounting'),

                     UpdateContext({ ctxHayDepositoCon: false });
                     Select(btnDepNuevoCon),            // Bloque N (§6.4)

                     // ya estaba persistido: se recarga tal cual está en Dataverse
                     UpdateContext({
                         ctxRegDepCon:
                             LookUp(
                                 colDepRecontadosCon,
                                 'CH-Tbl-DepositsRecounting' = ctxDepositoCon.'CH-Tbl-DepositsRecounting'
                             )
                     });
                     Select(btnCargarDepCon)            // Bloque C (§6.3)
                 );
                 Notify("Cambios descartados.", NotificationType.Information)
```

> 🧨 **Las dos ramas hacen cosas DISTINTAS y las dos son motores.** Antes eran dos
> comentarios: el constructor tenía que pegar a mano dos bloques de ~50 líneas dentro de una
> propiedad de comportamiento de un componente canvas, y **un `If` con las dos ramas
> comentadas compila como un `If` con las dos ramas vacías** — el diálogo confirmaba y no
> pasaba nada. Con los motores, cada rama es un `UpdateContext` + un `Select`.

> 🧨 **`TextoCancelar` = *"Seguir capturando"*** — es el texto exacto del mockup y el motivo
> por el que `cmpDialogo` expone esa propiedad (`01` §8.3, divergencia `L-7`).

> 🧨 **CONFIRMAR ANTES DE EJECUTAR.** Los tres `OnConfirmar` de arriba son los **únicos**
> lugares donde corre la acción destructiva. La v1 **borraba el conteo y después
> preguntaba** (`Clear(colRecountedMoneyDetails)` antes de abrir el diálogo). Si en esta
> guía ves un `Clear`/`Remove`/`Patch` en el botón que **abre** un diálogo, es un bug de la
> guía.

> 🧨 **El clic en el velo NO cierra** (`btnBloqueoDlg.OnSelect = false`, ledger `L-17`).
> Todos los diálogos de esta pantalla confirman acciones que pierden dinero contado o
> disparan solicitudes al supervisor.

---

## 14. Notas de la guía

### 14.1 Dependencias de backend y aproximaciones honestas

| ID | Qué | Dónde pega | Fallback / consecuencia sin backend |
|---|---|---|---|
| **E-1** | `RecountingSessionMirror` en `CH-Tbl-DepositsRecountings` (🔴 NO existe) | — | **Esta pantalla NO lo usa.** Es de `03` (KPIs del rail). Se anota para que nadie lo dé por hecho acá |
| **E-2** | `Bag` en `CH-Tbl-Checks` (🟡 la columna EXISTE; falta confirmar el plugin y el **backfill**) | §1 bloque 1 (Q2) · §11.3 (marcado a `Recontado`) | Fallback de `01` §2.2: N+1 acotado a los depósitos de la tula, con `RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting'` (ese salto **sí** delega hoy). ⚠️ **Sin backfill, una tula vieja muestra 0 cheques recontados en las matrices** aunque los tenga: probá el espejo ANTES de confiar en la franja |
| **E-3** | `DeclaredBagMirror` en `CH-Tbl-DeclaredChecks` (🔴 NO existe) | §1 bloque 2 (Q6) | Fallback de `01` §2.2: N+1 acotado a los depósitos declarados. **Bloquea la grilla de cheques** si no se aplica |
| **E-4** | `RecountingSessionDetailMirror` + `XMLReadingActiva` (🔴 NO existen) | §8.4 (Q17) | 🚫 **No hay fallback de un salto.** Hay que agregar `CH-Tbl-XMLReadings` como datasource #17 y encadenar dos consultas delegables (`01` §2.2). **Bloquea *Leer contadora*** |
| **P04-1** | Plugin de **totales de la bolsa** (`00` §2.2, punto 2) | §11.3 · §11.5 | Sin él, `TotalNIO/USD/EUR` de la bolsa quedan como estaban y `03` muestra KPIs viejos. **La app NO los escribe: eso es S-2 y no vuelve** |
| **P04-2** | Plugin de **`CH-Tbl-Transactions`** (`00` §2.2, punto 3) | §11.3 | Sin él **no se generan los asientos contables**. La app **no** los crea (la v1 sí, desde el dispositivo del cajero) y `CH-Tbl-Transactions` **ni siquiera es datasource** |
| **P04-3** | Plugin de **gates de estado** (`00` §2.2, punto 4) | todo el guardado | Sin él, la autorización de escritura queda solo en `DisplayMode`/`Visible` (**S-5**). El `IfError` de la receta 7.5 ya está listo para mostrar el mensaje del guard cuando exista |
| — | Supervisor: `"R. Sequeira"` en la espera | §12.1 | No existe: `SolvedBy` está vacío mientras la solicitud está pendiente. Divergencia **`D04-14`** |
| — | `"Bóveda B"` en el chip de mesa | §3 | No existe ninguna columna de ubicación en `CH-Tbl-CountingDesks`. Divergencia **`D04-2`** |

### 14.2 Gotchas de canvas que esta pantalla pisa

1. 🧨 **Un `Patch` a la colección en un `OnChange` de fila MATA EL FOCO** (`01` §7.10.A). Los
   **7** controles editables de template hacen **solo** `Set(gvarDepositoSinGuardar, true)`;
   el valor lo baja el **Bloque V** (§6.2). Es el hallazgo más caro del QA humano de
   Reception 1.31 y acá pega tres veces (billetes, monedas, cheques).
2. 🧨 **`TabIndex` positivo dentro de un template = filas entrelazadas** (`01` §7.10.B): las
   réplicas comparten el número y el recorrido global agrupa **por número**, no por fila.
   **Siempre `0`** + orden geométrico. El badge Auto y la papelera se quedan en **`-1`** a
   propósito.
3. 🧨 **`Reset(gal…)` con la galería visible re-ancla los `Default` y mata el foco.** Solo se
   hace en el `OnVisible` y en los Bloques C / N / M, **nunca** en un `OnChange`.
4. 🧨 **Un timer dentro de un contenedor invisible no corre, y ningún timer corre en una
   pantalla no visible.** Por eso `tmrGuardarCon` y `tmrPollCon` cuelgan de la pantalla y los
   dos `Navigate` diferidos viven dentro de sus `OnTimerEnd`.
5. **`ClearCollect(col, If(cond, …))` sin `else`** revienta con `ErrorKind.Internal`
   (**D-11**). En §1 bloque 2 el `If` va **siempre por fuera**.
6. **`Set()` dentro de `ForAll` está prohibido** (**D-12**) y `pac canvas pack` **no valida
   fórmulas**: viaja publicado y explota en Studio. En §5.5, §9.2 y §10.1 la acumulación se
   hace con `Collect()` y los `Set()` van **afuera**.
7. **El estado de galerías y de Listas de pestañas persiste entre visitas**: por eso el
   `OnVisible` cierra los 6 overlays, resetea los 2 tabs y las 5 galerías.
8. **Los `Fill` de superficies decorativas llevan `HoverFill` y `PressedFill` iguales**
   (receta 7.1): si no, la card parpadea al pasar el mouse.
9. **La sombra de `.card` / `.mat` / `.dlg` del mockup no existe** en el Botón clásico; solo
   el Contenedor tiene `DropShadow`, y por eso las cards de los 6 diálogos son Contenedores
   (`01` §7.1, ledger `L-12`).
10. **`Radius` solo lo tienen Botón clásico, Entrada de texto clásica y Contenedor.** Si en
    esta guía ves un `Radius` sobre un Rectángulo o una Etiqueta, es un bug: reportalo.
11. 🧨 **`hh` en `Text()` es reloj de 12 h SIN AM/PM.** Las dos fechas de esta pantalla
    (`lblEsperaSubCon`, `lblKvEnvEspCon`, `lblAvisoRechazoCon`) van con `FmtHora`/`FmtFecha`
    de `01` §3.1, que usan **`HH`**.
12. **`AccessibleLabel` — 9 con texto + 3 en blanco.** Lo llevan **con texto** los **9**
    controles interactivos: `icoVolverCon`, `icoLeerContadoraCon`, `icoAgregarChequeCon`,
    `icoBorrarChequeCon`, `chkChequeCon` y las **4 ✕** (`icoDlgXTerminarOkCon`,
    `icoDlgXTerminarDifCon`, `icoDlgXSalirCon`, `icoDlgXRechazoCon`). Y lo llevan **en
    blanco (`""`)** los **3** iconos **decorativos** de los tiles de diálogo:
    `icoTipoTerminarOkCon`, `icoTipoTerminarDifCon` e `icoTipoRechazoCon` — no son
    interactivos, el título del diálogo de al lado ya dice lo mismo, y un lector de pantalla
    que los anuncie repite la información. Sin ninguna de las dos cosas, el App Checker deja
    warnings de accesibilidad.
    (Los cinco motores 1×1 —los cuatro de §6 y `btnIrTulasCon` de §1.1— también van con
    `AccessibleLabel ""`.)
13. **Toda etiqueta con `Wrap true` lleva `Height` explícito** y verificado contra el
    `TemplateSize` de su galería: `lblAvisoHallazgoCon` (`58` dentro de un template de `66`),
    `lblVacioDepCon` (`60`), `lblVacioDetalleCon` (`60`), `lblVacioCursoCon` (`120`),
    `lblAvisoRechazoCon` (`76`), `lblNotaRechazoCon` (`34`), `lblNotaTerminarOkCon` (`32`).

### 14.3 Costo de red declarado

Toda la captura —teclear cantidades, marcar cheques, agregar y borrar cheques manuales, ver
totales de fila, cambiar de pestaña, seleccionar filas, ver las 4 matrices— corre a **cero
consultas**. La red se toca únicamente en:

| Momento | Consultas |
|---|---|
| Entrar a la pantalla | Q1 + Q2 + Q3 + Q4 (`Concurrent`) + Q5 + Q6 si hay declaración (`Concurrent`) — **6 como máximo, en 2 tandas paralelas** |
| Abrir un depósito de la lista (Bloque C) | Q6b — **una** consulta, acotada al desglose de ese depósito |
| Enlazar / desenlazar | Q11 (×1) + Q7 (×1) + Q1 + Q5 |
| Guardar un depósito | Q7 (1) + Q8 (1 `Patch` con tabla + N bajas) + Q9/Q10 (1 por cheque tocado) + Q1 + Q2 |
| Leer contadora | Q16 (flujo) + Q17 (1) |
| Eliminar un depósito | Q11 + N×(Q10 + Q9) + N×Q8-baja + Q12 + Q1 + Q2 + Q5 |
| Terminar sin discrepancias | Q13 + N×Q9 (cheques a `Recontado`) + Q14 |
| Terminar con discrepancias | Q15 + Q13 + Q14 |
| Polling | Q18 cada 15 s **mientras haya solicitud pendiente**, + Q19 una sola vez al resolverse |

**Ninguna consulta vive dentro del template de una galería** (**D-4**) — ese era el N+1 de
la v1: ~82 round-trips al abrir una tula y hasta ~200 al pulsar *Terminar*.

### 14.4 Huecos de `01` que esta pantalla detectó — ✅ los SEIS cerrados en `01`

Se detectaron acá y se **corrigieron en `01`** durante la consolidación. Esta tabla queda
como **trazabilidad**: la fuente de verdad de las tres colecciones y de las dos reglas es
`01`, no esta guía.

| # | Hueco en `01` | Cómo queda en `04` |
|---|---|---|
| **H04-1** | ✅ **CERRADO**: `colChecksTulaCon` ya está declarada en `01` §4.2 (contenido, quién la escribe y quién la lee) | Cargada con el espejo **E-2** en el `OnVisible` y recargada tras cada guardado y cada borrado (§1, §9.2 paso 6, §5.5 paso 5) |
| **H04-2** | ✅ **CERRADO**: `colDetalleDVCon` ya está declarada en `01` §4.2 como **snapshot de lo persistido** del depósito en curso | Cargada en el **Bloque C** (§6.3, paso 2) y vaciada en el **Bloque N** (§6.4) |
| **H04-3** | ✅ **CERRADO**: `colChecksDVCon` ya está declarada en `01` §4.2, mismo ciclo de vida | Sin snapshot no hay **paso de bajas** posible ni re-keyado del `Patch` |
| **H04-4** | ✅ **CERRADO**: `01` §4.3 ya apunta al **paso 3 de `04` §9.2** (receta **7.5**), con la fórmula del write-back. La errata *"paso 5 de la receta 7.9"* desapareció | `Patch(colChecksCon, LookUp(colChecksCon, LocalId = C.LocalId), { GUID: Guardado.'CH-Tbl-Checks' })`. Ver **`D04-19`** |
| **H04-5** | ✅ **CERRADO**: `01` §7.5 regla 2 se reescribió con el predicado **desde el snapshot `…DV` hacia la colección viva** y con los dos ejemplos (denominaciones y cheques), citando el Bloque **M** del cambio de moneda (§6.5) | Cubre *"quedó en 0"* y *"la fila desapareció por cambio de moneda"* con una sola condición. Ver **`D04-18`** |
| **H04-6** | ✅ **CERRADO**: `01` §2.3 ya lista `'CH-Tbl--MoneyDenomination'` (**dos guiones**), verificado en `Entities/sanic_CHTblMoneyDenomination/Entity.xml` línea 522 | Esta pantalla **no compara esa PK en ninguna fórmula** (el catálogo se filtra por `Currency` y `Type`). Ver **`D04-20`** |

### 14.5 🚫 Colecciones y variables que esta pantalla NO tiene (para que nadie las reponga)

| Qué NO existe | Por qué | Quién hace el trabajo |
|---|---|---|
| **`colChecksBorradosCon`** — la colección `{ LocalId, GUID }` de cheques manuales borrados con la papelera | Era un **cuarto** lugar donde vivía la misma verdad, con **tres `Clear`** (Bloques C y N + semilla) para desincronizar, y su única razón de ser era darle al guardado una lista de bajas que el guardado **ya sabía calcular solo** | El **paso 4** de §9.2: `Filter(colChecksDVCon As X, IsBlank(LookUp(colChecksCon, GUID = X.'CH-Tbl-Checks' && Seleccionado)))`. Con **un** predicado cubre los **tres** casos de baja —declarado destildado, manual borrado con la papelera, fila desaparecida por cambio de moneda— porque los tres dejan una fila en el snapshot **sin** contraparte seleccionada en la colección viva. Es la regla 2 de la receta `01` §7.5. Ledger **`L-58`** |
| **`gvarDatosConteo`** — el `{ ID_Sesion, Serial_Maquina }` de la v1 | Una tercera copia de dos datos que ya están en el contrato de entrada | `Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail')` y `gvarMesa.MachineSerialNumber` (`01` §7.11). Divergencia `D04-1` |
| **Las 18 variables `ctxRecChqNIOCon`…** del ejemplo de `01` §7.6 | Serían una segunda definición de cada número de las matrices | Las **propiedades de la instancia** (`cmpMatNIOCon.ChequesRec`, `.DifEfectivo`…). Divergencia `D04-12`, con su fallback declarado |

> 🧨 **Si en una revisión futura alguien propone reponer `colChecksBorradosCon`, la
> pregunta es una sola**: ¿qué caso de baja NO cubre el predicado del paso 4? Si la
> respuesta es "ninguno", la colección no vuelve.

---

## 15. Divergencias declaradas (para subir al ledger de `00` §6)

| # | Divergencia vs. mockup / `01` / la v1 | Motivo |
|---|---|---|
| **D04-1** | ✅ **RECONCILIADA con `03` §8.** `03` **no crea** ninguna variable de "datos de contadora" (la v1 tenía `gvarDatosConteo.ID_Sesion` / `.Serial_Maquina`) | Los dos parámetros del flujo ya están en el contrato de entrada: `Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail')` y `gvarMesa.MachineSerialNumber` (`01` §7.11). `03` §8 lo declara **idéntico**, con la misma tabla de los cinco datos de contadora. Una variable más es una verdad más que desincronizar |
| **D04-2** | El chip de mesa muestra solo `gvarMesa.Name`, sin `· Bóveda B` | `CH-Tbl-CountingDesks` no tiene ninguna columna de bóveda ni de ubicación. **El modelo manda sobre el mockup** |
| **D04-3** | El `←` del header y el botón *Cerrar* hacen **lo mismo**; `btnVolverCon.OnSelect` es literalmente `Select(btnCerrarTulaCon)` | El mockup dibuja los dos y no define qué los diferencia. Una sola definición de "salir de la tula" es lo único que no se puede desincronizar |
| **D04-4** | Las **3 matrices de la franja** muestran lo **PERSISTIDO**; la **4ª** (depósito en curso) muestra lo que se está capturando | Un arqueo de tula no puede moverse con cada tecla. El mockup no distingue los dos planos |
| **D04-5** | ✅ **DECIDIDA** — el pie de la tabla de depósitos muestra **una línea por moneda** con su símbolo, omitiendo las monedas en cero. **Nunca** un número que sume monedas distintas | El mockup muestra **solo el subtotal de la moneda mayoritaria y omite las demás**, con lo que un depósito USD **desaparece del pie**; la v2 emite **una línea por moneda presente**. Con una tula de una sola moneda —el caso normal— el pie se ve **idéntico al mockup**. Implementado en §5.3 (Bloque F), cero consultas nuevas |
| **D04-6** | *Desenlazar* **no** borra cheques ni pone `AccountNumber` en blanco (la v1 hacía las dos cosas) | El N° de cuenta es un dato del recuento, no del enlace; y borrar cheques desde el desenlace era una escritura destructiva sin `IfError` ni confirmación. Los cheques se desmarcan y se dan de baja en el guardado, con su paso de bajas |
| **D04-7** | `ctxMonedaCon` **no** se reinicia al crear un depósito nuevo | En una mesa real las tulas se separan por moneda: reiniciar a NIO obligaría a re-elegir en cada depósito. El mockup no dice nada del arranque |
| **D04-8** | Los pies de grilla, la matriz del depósito y *Total del depósito* se refrescan con el **VOLCADO**, no con cada tecla. El total de **fila** sí es en vivo | Regla **D-8** + receta `01` §7.10.A: un `Patch` a la colección en un `OnChange` re-renderiza la galería y **mata el foco**. Es el precio, y es barato |
| **D04-9** | Los cheques **manuales** nacen `Seleccionado: true` y **no muestran casilla** (el mockup dibuja un `—`) | Un cheque que el cajero agregó a mano **está** en el depósito por definición. Así todas las sumas usan un único predicado, `Filter(colChecksCon, Seleccionado)` |
| **D04-10** | Un cheque manual con **monto 0** bloquea el guardado con mensaje explícito, y su borde se pinta rojo mientras tanto | `CheckAmount` tiene **mínimo 1** en el modelo. La v1 **reseteaba el monto en silencio** y el cajero no se enteraba |
| **D04-11** | **Siete hallazgos, siete banners.** El mockup `C5` junta *"recontados sin enlazar"* y *"declarados sin enlazar"* en uno, y no contempla *"tula sin declaración"* | Una fila por hallazgo hace la galería trivial, el orden explícito (`Orden`) y agregar un hallazgo mañana no toca el layout |
| **D04-12** | El HTML y el resumen de `C4` leen las **propiedades de la instancia** (`cmpMatNIOCon.ChequesRec`, `.DifEfectivo`) en vez de las 18 variables de contexto del ejemplo de `01` §7.6 | Una sola definición de cada número. **Si tu Studio no permite leer una propiedad de entrada desde afuera**, se aplica el fallback de `01` §7.6 tal cual y se anota |
| **D04-13** | ✅ **DECIDIDA** — el subtotal de la **sección 4** del HTML sale **por moneda**: una fila `sub` por moneda presente, con su símbolo | Se llama a la misma `fxHtmlFilaTotalCheques` (`01` §3.1) **una vez por moneda**, sin tocar `App.Formulas`. Cada fila de detalle ya llevaba su símbolo; el que mentía era el subtotal. Implementado en §11.5 |
| **D04-14** | El `kv` de la espera dice **Enviada** (fecha y hora), no **Supervisor** | Mientras la solicitud está pendiente **nadie la resolvió**: `SolvedBy` está vacío por definición |
| **D04-15** | ✅ **RESUELTA por consolidación: hoy es CONTRATO en `01` §5.8.** Al **rechazar**, la app devuelve la bolsa a `Asignada a cajero` y el detalle de sesión a `En progreso` | Se envía en `Pendiente de revision…` (si no, el estado miente mientras el supervisor mira) y el rechazo la devuelve: es lo único que le deja al cajero **poder corregir**. Verificado contra `CH-App-Supervisor`: al rechazar, el supervisor **ya** hace ese mismo `Patch` ⇒ el de `04` es **idempotente y de refuerzo** |
| **D04-16** | ✅ **RESUELTA por consolidación → ver `01` §4.2.** `colDenominacionesCon` trae **TODAS** las denominaciones habilitadas (~20 filas) y se filtra por moneda **en local**; la fila de `01` §4.2 ya lo dice así | `01` §2.3 **D-10** ya la declaraba cacheable entera. Cero consultas al cambiar de moneda |
| **D04-17** | La grilla de cheques ofrece **todos los cheques declarados de la TULA** en la moneda del depósito (menos los que otro depósito ya tomó), no solo los del declarado enlazado | Es lo que dibuja el mockup `C3` (5 declarados, de los que 3 quedan sin enlazar en `C5`) y lo que permite marcar cheques en un depósito **sin** enlace. La v1 los ataba al enlace y por eso el desenlace tenía que borrarlos |
| **D04-18** | ✅ **RESUELTA por consolidación → ver `01` §7.5 regla 2.** El **paso de bajas** se escribe desde el snapshot `…DV` **hacia** la colección viva, y así quedó escrito en la receta madre (con los dos ejemplos, denominaciones y cheques) | Cubre con una sola condición *"quedó en 0"* **y** *"la fila desapareció por cambio de moneda"* (Bloque **M**, §6.5) |
| **D04-19** | ✅ **RESUELTA por consolidación → ver `01` §4.3.** El write-back del `GUID` de un cheque manual vive en el paso 3 de §9.2, bajo la **receta 7.5**, y `01` §4.3 ya apunta ahí (antes decía *"paso 5 de la receta 7.9"*, que es la fila de denominación) | Era una errata de `01`, corregida |
| **D04-20** | ✅ **RESUELTA por consolidación → ver `01` §2.3.** La PK de `CH-Tbl-MoneyDenominations` es **`'CH-Tbl--MoneyDenomination'`** (dos guiones), verificado en `Entities/sanic_CHTblMoneyDenomination/Entity.xml` línea 522, y `01` §2.3 ya la lista así | Esta pantalla no la compara en ninguna fórmula (el catálogo se filtra por `Currency` y `Type`), pero ya nadie la copia mal de `01` |
| **D04-21** | Seleccionar otro depósito recontado con cambios sin guardar **no abre un diálogo**: avisa y **no cambia** de depósito | Un diálogo con destino pendiente exige una variable más de estado (*"a cuál iba"*) que se puede desincronizar. El aviso es inmediato y el cajero decide con los botones que ya tiene |
| **D04-22** | La banda *"Efectivo del depósito (billetes + monedas)"* del mockup `C2` existe **solo en la pestaña Monedas** y ocupa `36` px propios; en Billetes y Cheques esa banda no se dibuja | Es exactamente lo que hace el mockup. Las tres pestañas comparten el resto del rítmo vertical: solo cambia el alto de la galería |
| **D04-23** | El **`kv`** del diálogo `C6-2` (*Efectivo* / *Cheques*) se degrada a **dos renglones de texto dentro de `Subtitulo`**, con un solo peso tipográfico | Consecuencia directa de **`L-10`**: un componente canvas **no puede recibir controles hijos**, y `cmpDialogo` no admite las 4 etiquetas de la receta `01` §7.2.b. Rearmar `C6-2` con la receta 7.2 suelta serían 11 controles más para dos renglones, y **dos** definiciones del chrome. §13.3 |
| **D04-24** | El estado vacío del panel derecho dice *"ya fueron guardados"*, **sin** el *"y enlazados"* del mockup `C4` | **No todos los depósitos se enlazan**: el enlace es opcional y su ausencia es un hallazgo legítimo (`H5`, §10.1). Prometer un enlace que puede no existir le hace dudar de un conteo correcto. §8.3 |
| **D04-25** | El banner de `C4` dice *"Las validaciones pasaron"*, **sin el número**; el mockup escribe *"Las 6 validaciones pasaron"* | Las validaciones son **siete** (`D04-11`). Un literal con el número queda mintiendo el día que se agregue el hallazgo ocho, y `"6"` ya es falso hoy. §11.2 |
| **D04-26** | El `kv` **Hallazgos** de la espera (`C7`) muestra **`—`** cuando `colHallazgosCon` está vacía, en vez del `0` que daría `Text(CountRows(…))`. El mockup dibuja siempre un número | **Aproximación honesta**, mismo criterio que `D03-2`. `colHallazgosCon` es **local** y el `OnVisible` la vacía (§1, bloque 3): tras cerrar y reabrir la app, la **solicitud** se recupera (Q4) pero los **hallazgos no** — se calcularon en `btnTerminarCon.OnSelect` y nunca se persistieron. Un `0` ahí sería **falso por construcción**: sin hallazgos la tula termina por `C4` y no hay nada que esperar. El detalle completo vive en el HTML de la solicitud (§11.5). Mejora futura: columna `FindingsCount` en `CH-Tbl-ApprovalRequests`. §12.1 |
| **D04-27** | Los cuatro **motores 1×1** (`btnVolcarGrillaCon`, `btnCargarDepCon`, `btnDepNuevoCon`, `btnRehacerMonedaCon`) — `04` usa el patrón de `03` (`D03-9`), que `L-53` presentaba como exclusivo de `03` | Los Bloques V, C, N y M se invocaban desde **19 puntos de pegado** escritos como **comentarios**: el constructor tenía que copiar a mano hasta 50 líneas en cada uno, dentro de `OnSelect` y de propiedades de comportamiento de componentes. Un `If` con las dos ramas comentadas (§13.4) **compila** como un `If` con las dos ramas vacías: el diálogo confirmaba y no pasaba nada. Con motores, cada punto es `UpdateContext(<parámetro>)` + `Select(<motor>)` y **cada bloque tiene una sola definición ejecutable**. `L-53` se amplía: el motor 1×1 es el patrón cuando la acción **no tiene un control de UI que la sea**; el discriminador de origen (`ctxEliminandoCon`) queda para cuando sí lo tiene (§13.3) |

---

## 16. Checklist de salida

Verificable en Studio, sin interpretación. **Corré `App.OnStart` antes de probar nada**
(botón derecho sobre **App** → *Ejecutar OnStart*), y entrá a la pantalla **desde `03`**, con
`gvarTula`, `gvarSesionDetalle` y `gvarDeclaredBag` seteadas.

**Estructura**

- [ ] Los **246 controles** del árbol existen, con **exactamente** estos nombres y en **este
      orden de inserción**. Búsqueda en el árbol: **no queda ningún nombre autogenerado**
      (`Label1`, `Button1`, `Gallery1`, `Checkbox1`…).
- [ ] **Todos** terminan en `Con` (regla `01` §0.5). Ni uno solo comparte nombre con un
      control de `scrSesion` (`Ses`) ni de `scrTulas` (`Tul`).
- [ ] `cmpMatrizMoneda` (×4 instancias) y `cmpDialogo` (×3 instancias) existen y en los dos
      **`AccessAppScope` está ACTIVADO** (`L-67`, `01` §8): sin la bandera, los `Theme*` y
      los `fx…` de `App.Formulas` quedan en rojo y el componente no compila. Prueba de que
      **igual no leen datos**: escribí `gvarTula` o `colChecksCon` dentro de un control del
      componente ⇒ **no debe existir ni uno solo en el `Src/` publicado**.
- [ ] Los 6 overlays son los **últimos** del árbol (antes de los 5 motores y los 2 timers),
      en el orden A→F, y cada uno tapa TODO lo que está debajo (abrí cada uno y hacé clic en
      las galerías: no pasa nada).
- [ ] Los **5 motores 1×1** (`btnVolcarGrillaCon`, `btnCargarDepCon`, `btnDepNuevoCon`,
      `btnRehacerMonedaCon` y `btnIrTulasCon`) cuelgan de **`scrConteo`**, miden `1 × 1` en
      `Y 767`, tienen `Visible` **`true`**, los tres fondos en `Color.Transparent`,
      `BorderThickness 0`, `TabIndex -1` y `DisplayMode.Edit` **sin condición** (§6.1 y
      §1.1). 🧨 **Ninguno con `Visible: false`**: `Select()` sobre un control oculto no está
      garantizado (`L-53`).
- [ ] 🧨 **Cero `Navigate(` en `scrConteo.OnVisible`** (`L-65`): la rama sin tula dice
      `Notify(…); Select(btnIrTulasCon)`. Prueba: App Checker **sin** el aviso *"would
      automatically always navigate away"*, y entrar a `04` con `gvarTula` en blanco ⇒
      aviso rojo y vuelta a `scrTulas`.
- [ ] Prueba de los motores: tocá una fila de *Recontado* ⇒ el panel derecho **carga ese
      depósito**. Tocá *Nuevo* ⇒ el panel queda **en blanco**. Cambiá de moneda con datos y
      confirmá ⇒ las grillas **se rehacen**. Si alguna de las tres no hace nada, el
      `Select()` murió.
- [ ] `tmrGuardarCon` y `tmrPollCon` cuelgan de **`scrConteo`**, NO de un contenedor.
      `rg -n 'Repeat: true' Src/scrConteo.fx.yaml` da **exactamente uno** (el de
      `tmrPollCon`); `tmrGuardarCon` tiene `Repeat: false`, `Duration: 50`,
      `Start: ctxGuardandoCon`.
- [ ] `shpSelDepDecCon`, `shpSelDepRecCon` y `shpFilaChqCon` están **al FONDO** de su
      template: el texto y los inputs de la fila se leen y se pueden tocar.

**Entrada y estado inicial**

- [ ] Al entrar, el header dice `Tula <BagID>`, la planilla en **semibold** y el cliente en
      gris, con la píldora **azul** *Asignada a cajero* pegada a la derecha **sin
      superponerse**.
- [ ] El panel derecho muestra **"No hay un depósito en curso"** y *Guardar depósito* /
      *Descartar* están **grises**; *Nuevo* está habilitado.
- [ ] Las **3 matrices** de la franja muestran los totales de la tula; con una tula sin
      recontar, la fila *Recontado* está en `0` y la *Diferencia* en **rojo** (si hay
      declaración) o en **verde con todo en 0** (si no la hay).
- [ ] Con una tula **sin declaración** (`gvarDeclaredBag` en blanco): la pestaña *Declarado*
      muestra el mensaje de estado vacío que lo dice, y las 3 matrices tienen la fila
      *Declarado* en `0`.
- [ ] **Sin `gvarSesionDetalle`**: aparece el toast de advertencia al entrar y *Leer
      contadora* queda **gris**. La app **no** sigue en silencio.

**Grilla de denominaciones (teclado-primero)**

- [ ] **PRUEBA DEL TAB**: parado en la cantidad de la fila 2, tabulá 3 veces: bajás por la
      columna **Cantidad** de las filas 3, 4 y 5 — **no** saltás a otra columna. El badge
      **Auto** no entra en el recorrido.
- [ ] **PRUEBA DE FOCO**: tecleá 4 dígitos **de corrido, sin pausas**, en la cantidad de la
      fila 3. El cursor **no** salta y el valor queda completo. Si salta, alguien puso un
      `Patch` en el `OnChange` (§14.2-1).
- [ ] El **total de la fila** cambia en vivo al tipear; el **pie** y la **matriz del
      depósito** cambian recién al tocar un botón (**D04-8**). Eso es lo esperado.
- [ ] El pie de *Billetes* muestra **Auto y Manual por separado** (`435 / 86`) y el importe
      con `FmtMoney`: `C$ 234,900.00`, nunca `C$ .00`.
- [ ] 🧨 **PRUEBA DEL PIE POR MONEDA (`D04-5`)**: con una tula que tenga depósitos en NIO
      **y** en USD, el pie de la tabla de depósitos muestra **dos líneas** —
      `C$ …` y `U$ …` — y **ningún número que sume las dos**. Con una tula de una sola
      moneda muestra **una** línea y el pie mide los `32` px del mockup. La moneda sin
      datos **no aparece**.
- [ ] La pestaña **Monedas** **no tiene columna Auto** y su aviso dice *"sin lectura
      automática: la contadora no procesa moneda"*.
- [ ] Un campo de cantidad con valor > 0 tiene borde **acento de 2 px**; en 0, borde
      `ThemeLine` de 1 px.

**Contadora (D-4 muerto)**

- [ ] *Leer contadora* con el espejo **E-4** desplegado: la columna **Auto** se puebla y el
      pie suma. Con >500 filas históricas de `XMLReadingDetails`, **sigue poblando** (si
      muestra 0, volvió el `in` con subconsulta de la v1).
- [ ] 🧨 **PRUEBA DE D-4**: leé la contadora en el depósito A, guardalo, tocá **Nuevo**. La
      columna **Auto** del depósito B tiene que estar en **CERO** en todas las filas. Si
      hereda los valores de A, falta el `Clear(colAutoContadoraCon)` del Bloque N.
- [ ] Lo mismo al **cambiar de moneda** y al **seleccionar otro depósito** de la pestaña
      *Recontado*: `Auto` vuelve a cero (los cuatro puntos de `01` §4.4).
- [ ] Ningún `Notify` de esta pantalla imprime GUIDs ni el serial de la contadora (**S-6**).

**Cheques**

- [ ] Las filas **manuales** tienen fondo **ámbar**, píldora *Manual*, número/tipo/monto
      **editables** y **papelera**; las **declaradas** tienen casilla, campos **de solo
      lectura** y píldora azul *Declarado*.
- [ ] *Agregar cheque* crea **una** fila por clic, y **dos clics crean dos filas
      distintas**: editar el monto de la segunda **no** cambia el de la primera (prueba de
      **D-9**: si cambian juntas, alguien resucitó `Max(OrderID)+1` o metió `GUID()` en un
      `ForAll`).
- [ ] Un cheque manual con **monto 0** o **sin número**: *Guardar depósito* **no llama a
      Dataverse** y el toast dice exactamente qué falta (**D04-10**).
- [ ] La papelera **no** entra en el recorrido de TAB y borra **solo** la fila que tocaste.
- [ ] 🧨 **PRUEBA DE LA PAPELERA SOBRE UN CHEQUE YA PERSISTIDO**: agregá un cheque manual,
      **guardá**, borralo con la papelera y **guardá otra vez**. En `CH-Tbl-Checks` esa fila
      **ya no está**. Lo hace el **paso 4** de §9.2, sin ninguna colección de "borrados"
      (§14.5): si el cheque sobrevive, alguien tocó el predicado de bajas.
- [ ] Un cheque declarado ya tomado por **otro** depósito **no aparece** en la grilla de este.

**Enlace (D-6 y D-7 muertos)**

- [ ] *Enlazar* con **monedas distintas** → toast con las dos monedas, **cero escrituras**.
- [ ] *Enlazar* cuando uno de los dos **ya está enlazado** → toast, **cero escrituras**.
- [ ] Al enlazar: el chip de la fila pasa a **Enlazado** en las **dos** pestañas y el
      `AccountNumber` del recontado toma el del declarado.
- [ ] 🧨 **PRUEBA DE D-6**: marcá un cheque declarado, guardá, **desmarcalo** y guardá otra
      vez. En Dataverse, ese `CH-Tbl-DeclaredChecks` tiene **`MatchedCheck` vacío** y
      **`DeclaredDepositID` INTACTO**. Si `DeclaredDepositID` quedó en blanco, volvió el bug
      de la v1 y el cheque quedó huérfano de su declaración para siempre.
- [ ] 🧨 **PRUEBA DE D-7**: guardá un depósito **sin enlazar** a ninguna declaración. En
      `CH-Tbl-DeclaredDeposits` **no** aparece ninguna fila fantasma nueva.

**Guardado (D-5 muerto)**

- [ ] **PRUEBA DEL DIÁLOGO EN VUELO**: con ~10 denominaciones cargadas, *Guardar depósito*
      muestra durante un instante *"Guardando el depósito…"* **sin botones y sin spinner**.
      Si nunca lo ves, `tmrGuardarCon` no está corriendo.
- [ ] 🧨 **PRUEBA DE D-5**: leé la contadora (Auto = 120) y tecleá 45 en la misma
      denominación. Guardá. En `CH-Tbl-CountedMoneyDetails` esa fila tiene **`Quantity =
      165`**, no 45. Y `CashAmount` del depósito coincide con
      `Σ Denominación × Quantity` de sus filas.
- [ ] **PRUEBA DE BAJAS**: guardá con una denominación en 12, después ponela en **0** y
      guardá otra vez. La fila **desapareció** de `CountedMoneyDetails` (no quedó en 12 ni
      en 0: `Quantity` tiene mínimo 1).
- [ ] **PRUEBA DE CAMBIO DE MONEDA**: guardá un depósito NIO con 3 denominaciones, cambiá a
      USD (confirmando el diálogo) y guardá. En Dataverse **no quedó ninguna fila NIO**
      colgada de ese depósito (**D04-18**).
- [ ] **Guardar dos veces seguidas NO duplica registros**: el conteo de
      `CountedMoneyDetails` y de `Checks` del depósito es el mismo (prueba del re-keyado).
- [ ] Con un error del guard: la píldora **Sin guardar** sigue encendida, el toast muestra
      el mensaje del plugin y `gvarDepositoSinGuardar` **sigue en `true`**. **La UI no marca
      como guardado lo que el guard rechazó.**
- [ ] 🧨 **PRUEBA DE `ctxSalirTrasGuardarCon`**: con un depósito sin guardar tocá `←` →
      *Guardar y salir*, y hacé que la validación **falle** (borrá el N° de cuenta antes).
      Sale el toast de error y **no se navega**. Ahora completá la cuenta y tocá **Guardar
      depósito** (el botón normal): guarda y **te quedás en la tula**. Si te saca a
      `scrTulas`, la bandera quedó encendida en una rama de validación (§9.1).

**Confirmaciones (confirmar ANTES de ejecutar)**

- [ ] *Descartar* con cambios → **diálogo**, con el conteo real de billetes, monedas y
      cheques. **Cancelar (*Seguir capturando*) no pierde nada.**
- [ ] *Eliminar* → **diálogo rojo** con efectivo y cheques. Cancelar **no borra nada**.
      Confirmar borra el depósito, sus denominaciones, sus cheques y suelta el enlace.
- [ ] *Cambiar moneda* con datos → **diálogo**. **Cancelar devuelve el desplegable a la
      moneda anterior** (prueba del `Reset(ddMonedaCon)`): si queda en la nueva, falta el
      `Reset` y la pantalla miente.
- [ ] En los tres, el **clic en el velo NO cierra** y la **✕ cancela** (donde la hay).
- [ ] Búsqueda en el YAML: **ningún** botón que ABRE un diálogo contiene `Clear(`, `Remove(`
      o `Patch(` — la acción destructiva vive **solo** en el confirmar.

**Terminar tula (D-8 muerto)**

- [ ] 🧨 **PRUEBA DE D-8**: con una tula que **sí** tiene diferencias por transacción, el
      **PRIMER** clic en *Terminar tula* ya las reporta. Si el primer clic dice *"sin
      discrepancias"* y el segundo las muestra, alguien validó antes de poblar.
- [ ] Sin ningún depósito recontado → toast de error, **sin diálogo**.
- [ ] Con el depósito en curso **sin guardar** → toast de error, **sin diálogo**.
- [ ] **Sin discrepancias**: diálogo `C4` con check **verde**, banner ok, tabla de 3
      monedas con `Total = Monto cheques + Efectivo`, y la nota de que los totales los
      recalcula el servidor.
- [ ] Confirmar `C4`: `BagStatus` pasa a **`Recontada` (159460003)**, **todos** los cheques
      de la tula quedan en `CheckStatus = Recontado`, el detalle de sesión pasa a `Cerrado`
      y la app navega a `scrTulas`. **`TotalNIO/USD/EUR` de la bolsa NO los escribió la
      app** (verificá el `Modified By` del registro: tiene que ser el plugin).
- [ ] En `CH-Tbl-Transactions` **no** hay ninguna fila creada por la app (la crea el plugin).
- [ ] **Con discrepancias**: diálogo `C5` con un banner **por hallazgo**, ninguno recortado,
      y el banner azul de *"se enviará una solicitud"*.
- [ ] Confirmar `C5`: se crea el `ApprovalRequest` con `RequestType` exactamente
      **`"Tula con diferencia"`** (o **`"Tula recontada sin declaracion"`** si no había
      declaración), `BagNumber` cargado, `RequestResources` = `{"BagGUID":"…"}` con `JSON()`
      y **`ApprovalStatus` VACÍO**. La bolsa pasa a **159460004** (`- recuento`, **nunca**
      `- esclusa`).
- [ ] El HTML de `RequestDetails` tiene **UN solo `</body></html>"`**, las secciones vacías
      **no se emiten**, y un cheque con `<b>` en el número aparece **escapado** (probá con un
      `CheckNumber` con `&` y `<`): mata **S-3**.
- [ ] 🧨 **SUBTOTAL DE LA SECCIÓN 4 POR MONEDA (`D04-13`)**: con cheques declarados sin
      enlazar en dos monedas, la sección *4 - Cheques declarados sin enlazar* termina con
      **dos** filas `sub`, cada una con **su** símbolo. Con una sola moneda, **una** fila.

**Espera y rechazo (S-4 muerto)**

- [ ] Tras enviar a revisión aparece `C7` con el **Spinner moderno**, la pantalla queda
      bloqueada y *Volver a las tulas* funciona.
- [ ] `tmrPollCon` corre cada **15 s** y **solo** mientras hay solicitud pendiente: al
      resolverla, el timer se apaga solo (`Start` es una condición, jamás `true`).
- [ ] 🧨 **PRUEBA DE S-4**: hacé que el supervisor **RECHACE** la solicitud. Aparece `C8`
      con el **motivo (`StatusReason`)**, quién y cuándo — **no** se libera la pantalla como
      si estuviera aprobada. Al tocar *Corregir el conteo*, la bolsa volvió a **`Asignada a
      cajero`** y la pantalla vuelve a ser **editable** (**D04-15**).
- [ ] Con una **aprobación**, la app navega a `scrTulas` y la bolsa quedó en **`Recontada`**
      (el canvas del Supervisor, al aprobar `"Tula con diferencia"`, hace
      `BagStatus: Recontada`, marca los cheques y cierra el detalle como *Cerrado con
      diferencias*). Esta pantalla **no** la patchea en la rama *Aprobado*: solo re-lee
      `gvarTula` por PK y navega.
- [ ] **Recuperación**: enviá a revisión, cerrá la app, volvé a entrar y abrí la tula. El
      overlay `C7` aparece solo (consulta de recuperación de `01` §5.8).
- [ ] 🧨 **LA RECUPERACIÓN ESTÁ ACOTADA** (`01` §5.8, regla 1): con una solicitud
      `"Crear sesion de recuento"` o `"Tula sin declaracion"` **pendiente** en la cola, abrir
      una tula **NO** abre el overlay `C7`. Si lo abre, alguien le sacó el
      `BagNumber = Trim(gvarTula.BagID)` o el `OR` de los **dos** tipos de esta pantalla a la
      Q4.
- [ ] `tmrPollCon.Start` es **`ctxEsperandoCon && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`**
      — ni `true` literal, ni `IsBlank(gvarSolicitud.ApprovalStatus)`.

**Modo solo lectura**

- [ ] Abrí una tula ya **`Recontada`**: la píldora del header dice *Recontada* en verde, y
      **TODO** está deshabilitado — moneda, cuenta, cantidades, casillas, papeleras,
      Enlazar, Desenlazar, Eliminar, Leer contadora, Agregar cheque, Nuevo, Descartar,
      Guardar y **Terminar tula**. Solo funcionan `←` y *Cerrar*.
- [ ] Lo mismo con una tula en **`Pendiente de revisión`**.
- [ ] Búsqueda en el YAML: **todo** `DisplayMode` de escritura contiene
      `gvarIdentidadOk && !ctxSoloLecturaCon`. `rg -n 'DisplayMode.Edit' Src/scrConteo.fx.yaml`
      no muestra ningún botón de escritura con `DisplayMode.Edit` literal.

**Delegación e higiene**

- [ ] **App Checker: cero errores y cero advertencias de delegación.** Si aparece el
      triángulo azul, alguien metió un `Filter`/`LookUp`/`Sum`/`CountRows` sobre un
      datasource donde va una colección local — cruzá contra el inventario cerrado de §2.
- [ ] `rg -n 'RemoveIf\(.CH-Tbl|UpdateIf\(.CH-Tbl|Refresh\(|hh:|AllItems' Src/scrConteo.fx.yaml`:
      **cero** `RemoveIf`/`UpdateIf` sobre datasource, **cero** `hh:`, **dos** `Refresh(` (las
      dos ramas del polling) y `AllItems` **solo** en los tres `ForAll` del Bloque V.
- [ ] `rg -n "BagID = |RelatedDeclaredDeposit = |RelatedReCounterDepositID = " Src/scrConteo.fx.yaml`
      da **cero**: toda comparación por lookup es **PK contra PK** (`01` §2.3, D-1).
- [ ] `rg -n 'DeclaredDepositID:' Src/scrConteo.fx.yaml` da **cero**: la app **jamás** escribe
      esa columna (**D-6**).
- [ ] `rg -n 'OtherLocalCheckAmount' Src/scrConteo.fx.yaml` aparece **solo** en fórmulas sobre
      `colDepDeclaradosCon` / `'CH-Tbl-DeclaredDeposits'`; `OtherLocaCheckAmount` (sin la `l`)
      **solo** sobre `colDepRecontadosCon` / `'CH-Tbl-DepositsRecountings'`.
- [ ] `rg -n 'Set\(' Src/scrConteo.fx.yaml` — ningún `Set()` cae dentro de un `ForAll`
      (**D-12**).
- [ ] `rg -n 'Substitute\(' Src/scrConteo.fx.yaml` da **cero**: el escape HTML vive **dentro**
      de las `fx…` de `01` §3.1 y escapar dos veces produce basura.
- [ ] Ninguna etiqueta con `Wrap true` recorta su texto (los 7 controles de §14.2-13).
- [ ] Ningún separador de fila se superpone con el contenido de la fila de abajo, en las
      **5** galerías.
- [ ] Las píldoras del header y de la columna *Enlace* usan los `Switch`/`If` de la receta
      7.4 y **ninguna recorta texto** (el `Width` es `14 + 6.2 * Len(Self.Text)`).
- [ ] Los **9 controles interactivos de §14.2-12** tienen su `AccessibleLabel` cargado con
      texto, y los **3 iconos decorativos** (`icoTipoTerminarOkCon`, `icoTipoTerminarDifCon`,
      `icoTipoRechazoCon`) lo tienen **en blanco** (`""`), igual que los 5 motores.
