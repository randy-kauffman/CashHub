# Guía 04 — scrLotes (v2 — layout manual + fórmulas relativas + componentes)

Pantalla **más grande** de la app. Ciclo de vida del lote: **rail de cajeros con lotes
por aplicar** (componente `cmpRailCajeros`) + card de **lista de lotes** (segmentado por
estado, buscador, filtro de fecha) + card de **detalle** (header con **píldora
componente**, barra de ops, **KPIs por moneda componente**, grilla **delegable** de
transacciones del lote y, al seleccionar una tx, su **historial/timeline** con el
motivo). Corrige **D-1** (grid del lote vacío por dot-walk que trunca), **D-6**
(SortByColumns sobre set truncado) y **C-1** (`gvarTxLot` no se limpiaba al cambiar de
lote).

> **Paradigma v2 (01 §7.0).** Nada de contenedores de auto-ajuste. El esqueleto son
> **Contenedores MANUALES** anidados; cada control fija `X`/`Y`/`Width`/`Height` con
> **fórmulas relativas** a `Parent.Width`/`Parent.Height` + constantes `Lay*` (01 §3).
> Todo lo repetido es **COMPONENTE** (01 §8): `cmpHeader`, `cmpRailCajeros`, `cmpPildora`,
> `cmpKpi`, `cmpDialogo`, `cmpEmpty`. **Solo cambia el LAYOUT**: las fórmulas funcionales
> (rail, filtros, grid delegable, sacar del lote, CSV, timeline, KPIs) se preservan.

Fuente de verdad del diseño: `mockup-digitization.html` secciones **LO1** (lote Generado
+ rail + filtros + históricos), **LO2** (sacar del lote con motivo) y **LO3**
(historial/timeline). Todo lo global (paleta, `App.Formulas`, `ESTILO-*`, recetas
7.0–7.3, componentes 8.1–8.7) vive en `01-app.md` y se referencia por número — cumplirlo
es el contrato, no licencia creativa (01 §0).

**Componentes que esta pantalla INSTANCIA** (se construyen antes, 01 §8 / §9):

| Instancia (en scrLotes) | Componente (01 §) | Rol acá |
|---|---|---|
| `cmpHeaderLot` | `cmpHeader` §8.1 | Encabezado + nav (`Activo="Lotes"`) |
| `cmpRailLot` | `cmpRailCajeros` §8.2 | Rail de cajeros con lotes por aplicar (badge=conteo) |
| `cmpPilDetEstadoLot` | `cmpPildora` §8.3 | Píldora de estado en el header del detalle |
| `cmpKpiNioLot` / `cmpKpiUsdLot` / `cmpKpiEurLot` | `cmpKpi` §8.4 | KPI por moneda (espejo del lote) |
| `cmpDlgAplicadoLot` | `cmpDialogo` §8.5 | Confirmar Aplicado (texto + botones) |
| `cmpEmptyListaLot` | `cmpEmpty` §8.6 | Estado vacío de la lista de lotes |

> El diálogo **Sacar del lote** NO se componentiza: lleva **textarea de motivo** (contenido
> rico) y `cmpDialogo` es solo texto+botones (01 §8.5 nota). Va **directo en la pantalla**
> (§11), con la **misma estructura interna** que `cmpDialogo` + textarea + banner.

**Contrato de entrada / estado que gobierna esta pantalla** (cada pantalla limpia su
selección en `OnVisible` — mata C-1):

| Variable | Tipo | Qué es | Quién la setea |
|---|---|---|---|
| `colCajerosLot` | colección `{cajero:GUID, nombre, mesa, lotesPorAplicar, badge}` | Cajeros con lotes por aplicar (JSON de `sanic_CHWorkloadByCashier`, filtrado `>0`) | `OnVisible` (`ClearCollect`) |
| `gvarCajeroLot` | record de `colCajerosLot` | Cajero elegido en el rail | `OnVisible` (First) + `cmpRailLot.OnSeleccion` |
| `gvarLote` | record de `CH-Tbl-TransactionBatches` | Lote seleccionado de la lista | `OnVisible`/segmentado/fecha/rail (First) + `btnLoteFilaLot.OnSelect` |
| `gvarTxLot` | record de `CH-Tbl-Transactions` | Tx seleccionada del lote (historial + "sacar del lote") | `btnTxFilaLot.OnSelect`; se limpia al cambiar de lote/cajero/estado/fecha |
| `ctxFiltroEstadoLot` | text (catálogo cerrado) | `"Por aplicar"` \| `"Aplicados"` \| `"Todos"` | `OnVisible` (default) + segmentado |
| `ctxDesdeFechaLot` | dateTime | Umbral del filtro de fecha (constante plegable → delega) | `OnVisible` + `tabFechaLot.OnChange` |
| `ctxDescargandoLot` | boolean | CSV en curso (deshabilita el botón) | `btnDescargarCsvLot.OnSelect` |
| `ctxAplicandoLot` / `ctxSacandoLot` | boolean | Op en curso (anti-doble-click en cada diálogo) | diálogos |
| `ctxDlgAplicadoLot` / `ctxDlgSacarLot` | boolean | Visibilidad de cada diálogo | ops / diálogos |

> **Regla de delegación de esta pantalla** (01 §7.2 + `00 §1.2`):
> - **Lista de lotes**: `Filter('CH-Tbl-TransactionBatches', Cajero.'Primary Key' =
>   gvarCajeroLot.cajero, …)` — **igualdad de PK del lookup contra un GUID**, que Dataverse
>   pliega a `_…_value eq <guid>`. Orden por **columna propia `createdon`** (nunca
>   `SortByColumns` sobre un set ya truncado — corrige D-6).
> - **Grid del lote**: `Filter('CH-Tbl-Transactions', 'Transaction Batch'.TransactionBatches
>   = gvarLote.TransactionBatches)` — igualdad de lookup por PK (corrige D-1).
> - JAMÁS `Lookup.OtraColumna` (no-key) en el predicado: materializa 500 y filtra en
>   cliente. **Cero banda azul**.

---

## Árbol de controles

```
scrLotes                                     (Fill: ThemeBg)
├── cmpHeaderLot                             Componente cmpHeader (§8.1) · X0 Y0 · W Parent.Width · H LayHeaderH
├── cntBodyLot                               Contenedor (manual) · X0 Y LayHeaderH · W Parent.Width · H Parent.Height-LayHeaderH
│   ├── cmpRailLot                           Componente cmpRailCajeros (§8.2) · X0 Y0 · W LayRailW · H Parent.Height
│   └── cntMainLot                           Contenedor (manual) · X LayRailW Y0 · W Parent.Width-LayRailW · H Parent.Height
│       ├── cntListaCardLot                  Contenedor (manual, card 7.1) — lista de lotes
│       │   ├── cntSegEstadoLot              Contenedor (manual) — segmentado estado
│       │   │   ├── btnSegPorAplicarLot      Botón clásico
│       │   │   ├── btnSegAplicadosLot       Botón clásico
│       │   │   └── btnSegTodosLot           Botón clásico
│       │   ├── inpBuscarLot                 Entrada de texto clásica
│       │   ├── tabFechaLot                  Lista de pestañas moderna (único control moderno — nota)
│       │   ├── lblListaSubLot               Etiqueta clásica ("Lotes de …")
│       │   ├── galLotesLot                  Galería vertical en blanco · TemplateSize 52
│       │   │   ├── shpLoteSelLot            Rectángulo (fondo fila sel.)
│       │   │   ├── shpLoteSelBarraLot       Rectángulo (barra acento)
│       │   │   ├── btnLoteFilaLot           Botón clásico (overlay 7.2)
│       │   │   ├── lblLoteNombreLot         Etiqueta clásica
│       │   │   ├── lblLoteMetaLot           Etiqueta clásica
│       │   │   └── pilLoteEstadoLot         Botón clásico (píldora 7.2)
│       │   └── cmpEmptyListaLot             Componente cmpEmpty (§8.6) · Visible sin lotes
│       └── cntDetalleCardLot                Contenedor (manual, card 7.1) — detalle (ancho ≈1.6×)
│           ├── cntDetHeadLot                Contenedor (manual) — header detalle
│           │   ├── lblDetTituloLot          Etiqueta clásica ("Lote TB-…")
│           │   ├── lblDetSubLot             Etiqueta clásica (cajero · generado …)
│           │   ├── cmpPilDetEstadoLot       Componente cmpPildora (§8.3)
│           │   └── shpDetHeadLineaLot       Rectángulo (borde inferior)
│           ├── cntOpsLot                    Contenedor (manual) — barra de operaciones
│           │   ├── btnMarcarIngresadoLot    Botón clásico (ESTILO-BTN-CONTORNO)
│           │   ├── btnMarcarAplicadoLot     Botón clásico (ESTILO-BTN-CONTORNO)
│           │   ├── btnDescargarCsvLot       Botón clásico (ESTILO-BTN-CHICO acc)
│           │   ├── btnSacarLoteLot          Botón clásico (ESTILO-BTN-CHICO dan)
│           │   └── shpOpsLineaLot           Rectángulo (borde inferior soft)
│           ├── cntProgresoLot               Contenedor (manual, overlay strip) · Visible ctxDescargandoLot
│           │   ├── lblProgresoLot           Etiqueta clásica
│           │   ├── shpProgresoTrackLot      Rectángulo (riel · Visible false, L-3)
│           │   └── shpProgresoFillLot       Rectángulo (relleno · Visible false, L-3)
│           ├── cmpKpiNioLot                 Componente cmpKpi (§8.4)
│           ├── cmpKpiUsdLot                 Componente cmpKpi (§8.4)
│           ├── cmpKpiEurLot                 Componente cmpKpi (§8.4)
│           ├── cntGridCardLot               Contenedor (manual, card 7.1) — grid tx del lote
│           │   ├── lblGCol1Lot              Etiqueta clásica ("REFERENCIA")
│           │   ├── lblGCol2Lot              Etiqueta clásica ("DEPÓSITO / CLIENTE")
│           │   ├── lblGCol3Lot              Etiqueta clásica ("CUENTA")
│           │   ├── lblGCol4Lot              Etiqueta clásica ("MONTO")
│           │   ├── lblGCol5Lot              Etiqueta clásica ("MON.")
│           │   ├── shpGridLineaLot          Rectángulo (línea 1 px)
│           │   └── galTxLot                 Galería vertical en blanco · TemplateSize 44
│           │       ├── shpTxSelLot          Rectángulo (fondo fila sel.)
│           │       ├── btnTxFilaLot         Botón clásico (overlay 7.2, al fondo)
│           │       ├── lblTxRefLot          Etiqueta clásica
│           │       ├── lblTxDepositoLot     Etiqueta clásica
│           │       ├── lblTxCuentaLot       Etiqueta clásica
│           │       ├── lblTxMontoLot        Etiqueta clásica
│           │       ├── lblTxMonedaLot       Etiqueta clásica
│           │       ├── shpTxSelBarraLot     Rectángulo (barra acento)
│           │       └── shpTxSepLot          Rectángulo (separador)
│           └── cntHistCardLot               Contenedor (manual, card 7.1) · Visible !IsBlank(gvarTxLot)
│               ├── lblHistTituloLot         Etiqueta clásica ("HISTORIAL · …")
│               └── galHistLot               Galería vertical en blanco · TemplateSize 58
│                   ├── shpHistDotLot        Rectángulo (punto de la línea)
│                   ├── lblHistItemLot       Etiqueta clásica (evento)
│                   ├── lblHistMetaLot       Etiqueta clásica (fecha · autor)
│                   └── lblHistMotivoLot     Etiqueta clásica (motivo · Wrap)
│
├── cmpDlgAplicadoLot                        Componente cmpDialogo (§8.5) — confirmar Aplicado · X0 Y0 · W Parent.Width · H Parent.Height
└── cntDlgSacarLot                           Contenedor (manual, overlay) — sacar del lote · X0 Y0 · W Parent.Width · H Parent.Height
    ├── btnDlgSacarBloqueoLot                Botón clásico (bloqueo de fondo)
    └── cntDlgSacarCardLot                   Contenedor (manual) — card del diálogo
        ├── shpDlgSacarIcoLot                Botón clásico (tile ícono peligro)
        ├── lblDlgSacarTitLot                Etiqueta clásica
        ├── lblDlgSacarTxtLot                Etiqueta clásica
        ├── icoDlgSacarXLot                  Icono (Icon.Cancel)
        ├── lblDlgSacarMotivoLabLot          Etiqueta clásica ("Motivo del rechazo del core")
        ├── inpMotivoLot                     Entrada de texto clásica (Multiline)
        ├── cntDlgSacarBannerLot             Contenedor (manual) — banner warn
        │   └── lblDlgSacarBannerLot         Etiqueta clásica
        ├── btnDlgSacarCancelLot             Botón clásico (ESTILO-BTN-FANTASMA)
        └── btnDlgSacarOkLot                 Botón clásico (ESTILO-BTN-PELIGRO)
```

> **Convenciones de esta guía** (para no repetir en cada tabla):
> - Todo control clásico hereda los **defaults tipográficos** (01 §6). Las tablas solo
>   listan lo que cambia.
> - `X`/`Y`/`Width`/`Height` de **todo** control son **fórmulas relativas** a
>   `Parent.Width`/`Parent.Height` + `Lay*` (01 §7.0). `Parent` = el contenedor manual que
>   lo aloja. Dos controles que comparten un borde: uno referencia al otro (nunca dos
>   literales que se contradigan).
> - Dentro de una **galería** el template usa `Parent.TemplateWidth`/`Parent.TemplateHeight`.
>   Anchos de columna = **fracción** del `TemplateWidth` (siguen siendo responsive).
> - Overlays de fila (`btn…Fila`): receta 7.2 — X 0 · Y 0 · W `Parent.TemplateWidth` · H
>   `TemplateSize` · Fill/Hover/Pressed transparentes · al **FONDO** del z-order · Text `""`.
>   Todo label del template delega su clic con `Select(Parent)`.
> - **Constantes de layout local de esta pantalla** (no repetir literales que se contradigan):
>   `PadCard = 12` (respiro interno de la card lista), `PadDet = 16` (respiro del detalle),
>   `RatioLista = 0.385` (fracción de ancho de la card lista; el detalle toma el resto por
>   referencia), `HistH = 200` (alto de la card historial). Se enuncian una vez y se usan
>   por su valor en las fórmulas de abajo.
> - **Reparto de columnas del grid** (mockup `1.1fr 2fr 1fr .9fr .6fr`, suma `5.6`): el
>   ancho de cada columna = `(<ancho útil> - 2*PadDet) * <fr>/5.6`; el `X` se **encadena**
>   desde `PadDet` (cada uno = anterior.X + anterior.Width). Se enuncia una vez acá y se
>   aplica idéntico en encabezado (`lblGCol*`) y template (`lblTx*`).

---

## 1. Pantalla y `OnVisible`

| Propiedad | Valor |
|---|---|
| `scrLotes.Fill` | `ThemeBg` |
| `scrLotes.OnVisible` | *(abajo — limpia selección, carga el rail y arma el estado inicial)* |

```powerfx
// Limpia la selección de tx (mata C-1) y fija el estado inicial de filtros.
Set(gvarTxLot, Blank());
UpdateContext({
    ctxFiltroEstadoLot: "Por aplicar",
    ctxDesdeFechaLot: DateValue("1900-01-01"),
    ctxDescargandoLot: false,
    ctxAplicandoLot: false,
    ctxSacandoLot: false,
    ctxDlgAplicadoLot: false,
    ctxDlgSacarLot: false
});
Reset(tabFechaLot);        // el estado de Tab list PERSISTE entre visitas (gotcha)
Reset(inpBuscarLot);
// Rail: cajeros con lotes por aplicar. Custom API vía Environment (01 §2) → JSON →
// colección tipada, filtrada a lotesPorAplicar > 0. 'badge' es lo que pinta el conteo
// dentro de cmpRailCajeros (§8.2).
ClearCollect(colCajerosLot,
    Filter(
        ForAll(
            ParseJSON(Environment.sanic_CHWorkloadByCashier().Workload) As w,
            {
                cajero:          GUID(Text(w.cajero)),
                cajeroNombre:    Text(w.cajeroNombre),
                mesa:            Text(w.mesa),
                lotesPorAplicar: Value(w.lotesPorAplicar),
                badge:           Value(w.lotesPorAplicar)
            }
        ),
        lotesPorAplicar > 0
    )
);
// Secuencial: gvarCajeroLot queda seteado ANTES de la línea siguiente.
Set(gvarCajeroLot, First(colCajerosLot));
// Primer lote del cajero según el filtro (§4). Sin selección de tx todavía.
Set(gvarLote, First(FiltroLotesLot))
```

> `FiltroLotesLot` es la **misma expresión** que alimenta `galLotesLot.Items` (§4.4). Se
> enuncia una vez ahí y se referencia por nombre acá y en cada handler que re-elige lote,
> para no duplicar. `First()` sobre ese `Filter` delegable pliega a la query.
>
> **Divergencia de arquitectura vs guía v1**: el **orden del rail** (Por aplicar / A–Z)
> ya **no** vive en la pantalla (no hay `ctxOrdenLot` acá): los dos botones de orden y su
> lógica están **dentro de `cmpRailCajeros`** (01 §8.2), que ordena su `Datos` internamente.
> La pantalla solo entrega la colección cruda (`colCajerosLot`).

### 1.1 Encabezado — `cmpHeaderLot` (instancia de `cmpHeader`, 01 §8.1)

`X 0 · Y 0 · Width Parent.Width · Height LayHeaderH`. El componente trae marca, título,
nav segmentado y chip de usuario; la pantalla solo cablea sus Custom Properties:

| Propiedad | Valor |
|---|---|
| `Titulo` | `"Digitación"` |
| `Subtitulo` | `"Lotes"` |
| `Activo` | `"Lotes"` *(pinta el tab Lotes activo)* |
| `OnCorte` | `Navigate(scrCorte)` |
| `OnManual` | `Navigate(scrManual)` |
| `OnLotes` | `Navigate(scrLotes)` *(inocuo)* |
| `OnCheques` | `Navigate(scrCheques)` |

> Un componente no puede `Navigate` a pantallas externas: navega por **behavior props**.
> Toda la lógica de "cuál tab está activo" (Fill/Color/FontWeight por `Activo="…"`) vive en
> §8.1 — acá no se repite.

---

## 2. Cuerpo y zonas (`cntBodyLot`, `cntMainLot`)

Esqueleto 7.0. Contenedores manuales que acotan el marco de referencia de sus hijos.

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `cntBodyLot` (Contenedor manual) | `0` | `LayHeaderH` | `Parent.Width` | `Parent.Height - LayHeaderH` | Fill `Color.Transparent` |
| `cmpRailLot` (§8.2) | `0` | `0` | `LayRailW` | `Parent.Height` | *(props §3)* |
| `cntMainLot` (Contenedor manual) | `LayRailW` | `0` | `Parent.Width - LayRailW` | `Parent.Height` | Fill `Color.Transparent` |

Dentro de `cntMainLot`, dos cards lado a lado (mockup: lista `flex:1`, detalle `flex:1.6`):

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `cntListaCardLot` (card 7.1) | `LayGutter` | `LayGutter` | `(Parent.Width - 2*LayGutter - LayGap) * 0.385` | `Parent.Height - 2*LayGutter` |
| `cntDetalleCardLot` (card 7.1) | `cntListaCardLot.X + cntListaCardLot.Width + LayGap` | `LayGutter` | `Parent.Width - LayGutter - Self.X` | `Parent.Height - 2*LayGutter` |

> El detalle **referencia** a la lista para su `X` y **rellena hasta el margen** con
> `Parent.Width - LayGutter - Self.X`: dos controles que comparten borde, uno apunta al
> otro. `0.385 ≈ 1 / 2.6` (reparto `1 : 1.6` del mockup). Card 7.1 = Fill `ThemeSurface`,
> Border `ThemeLine` 1, `RadiusTop/Bottom-Left/Right = LayCardR`.

---

## 3. Rail de cajeros — `cmpRailLot` (instancia de `cmpRailCajeros`, 01 §8.2)

El rail **es un componente**; su galería, template, barra de selección, badge y botones
de orden viven en §8.2. La pantalla lo posiciona (§2) y cablea sus Custom Properties:

| Propiedad | Valor |
|---|---|
| `Titulo` | `"CAJEROS CON LOTES"` |
| `Datos` | `colCajerosLot` *(la colección de §1; el componente muestra `nombre`/`mesa` y el badge desde `badge`)* |
| `CajeroSel` | *(Output — lo lee la pantalla como `cmpRailLot.CajeroSel`)* |
| `OnSeleccion` | *(abajo — al elegir un cajero, re-arma lote y limpia tx)* |

```powerfx
// cmpRailLot.OnSeleccion  — el componente ya actualizó su selección; la espejamos.
Set(gvarCajeroLot, cmpRailLot.CajeroSel);
Set(gvarTxLot, Blank());
Set(gvarLote, First(FiltroLotesLot))
```

> 🧨 **Selección por `gvarCajeroLot`, no por auto-select nativo**: el `OnVisible`
> pre-selecciona el primer cajero (`Set(gvarCajeroLot, First(colCajerosLot))`) y el detalle
> lee la **variable**. `cmpRailCajeros` expone `CajeroSel` = su `gal…Rail.Selected` y avisa
> el cambio por `OnSeleccion`. Mismo criterio que el tracker con `gvarPlanilla`.
>
> **Sin backend** (`sanic_CHWorkloadByCashier` no existe todavía) — el rail exige la Custom
> API (00 §2.2, dep B-workload): el agregado de lotes por cajero **no delega** en canvas.
> Fallback provisorio: `colCajerosLot` desde `Users` (`{cajero: 'Primary Key', nombre:
> 'Full Name', mesa: "", lotesPorAplicar: Blank(), badge: Blank()}`) y en el componente
> `pilBadgeRail.Visible = false`. Se marca provisorio.

---

## 4. Card de lista de lotes (`cntListaCardLot`)

Card 7.1. Sus hijos se apilan por **`Y` encadenado** (cada uno = el de arriba + su alto +
respiro). `PadCard = 12`.

### 4.1 Segmentado de estado (`cntSegEstadoLot`)

Contenedor manual: Fill `ThemeSurface2`, Border `ThemeLine` 1, Radius 9.

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `cntSegEstadoLot` | `PadCard` | `10` | `Parent.Width - 2*PadCard` | `34` |
| `btnSegPorAplicarLot` | `3` | `3` | `(Parent.Width - 6) / 3` | `Parent.Height - 6` |
| `btnSegAplicadosLot` | `btnSegPorAplicarLot.X + btnSegPorAplicarLot.Width` | `3` | `(Parent.Width - 6) / 3` | `Parent.Height - 6` |
| `btnSegTodosLot` | `btnSegAplicadosLot.X + btnSegAplicadosLot.Width` | `3` | `(Parent.Width - 6) / 3` | `Parent.Height - 6` |

Botones: Radius `7`, Size `9`, patrón activo/inactivo:

| Control | Text | Activo cuando | OnSelect |
|---|---|---|---|
| `btnSegPorAplicarLot` | `"Por aplicar"` | `ctxFiltroEstadoLot = "Por aplicar"` | *(abajo)* |
| `btnSegAplicadosLot` | `"Aplicados"` | `ctxFiltroEstadoLot = "Aplicados"` | *(abajo)* |
| `btnSegTodosLot` | `"Todos"` | `ctxFiltroEstadoLot = "Todos"` | *(abajo)* |

Activo: `Fill ThemeSurface`, `Color ThemeAccent`, `FontWeight Semibold`. Inactivo: `Fill
Color.Transparent`, `Color ThemeInk2`, `FontWeight Normal`.

`OnSelect` (idéntico salvo el literal — re-elige el primer lote y limpia la tx):

```powerfx
UpdateContext({ctxFiltroEstadoLot: "Por aplicar"});   // "Aplicados" / "Todos" según el botón
Set(gvarTxLot, Blank());
Set(gvarLote, First(FiltroLotesLot))
```

### 4.2 Buscador + filtro de fecha

Se apilan en dos filas (el Tab list de 4 opciones necesita el ancho completo de la card).

| Control | Tipo | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|---|
| `inpBuscarLot` | Entrada de texto clásica | `PadCard` | `cntSegEstadoLot.Y + cntSegEstadoLot.Height + 10` | `Parent.Width - 2*PadCard` | `32` | HintText `"⌕ N° de lote…"` · `DelayOutput true` · BorderColor `ThemeLine` · Radius 8 · Size 9 |
| `tabFechaLot` | Lista de pestañas moderna | `PadCard` | `inpBuscarLot.Y + inpBuscarLot.Height + 8` | `Parent.Width - 2*PadCard` | `30` | Items `Table({Value:"Cualquier fecha"},{Value:"Hoy"},{Value:"Últimos 7 días"},{Value:"Este mes"})` · campo `Value` |

> **Único control moderno de la app** (divergencia declarada, §13): el mockup usa un
> `<select>` de fecha; en canvas se resuelve con **Lista de pestañas moderna** (sancionada
> por el mockup: "TabList moderno donde haga falta"). No es PCF/Form/tercero, así que
> respeta el catálogo cerrado (01 §0). Se preserva su fórmula funcional intacta.

`tabFechaLot.OnChange` (calcula el umbral como **constante plegable** → el `>=` delega):

```powerfx
UpdateContext({ctxDesdeFechaLot:
    Switch(tabFechaLot.Selected.Value,
        "Hoy", Today(),
        "Últimos 7 días", DateAdd(Today(), -7, TimeUnit.Days),
        "Este mes", Date(Year(Today()), Month(Today()), 1),
        DateValue("1900-01-01"))});
Set(gvarTxLot, Blank());
Set(gvarLote, First(FiltroLotesLot))
```

### 4.3 Subtítulo de la lista

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `lblListaSubLot` | `0` | `tabFechaLot.Y + tabFechaLot.Height + 10` | `Parent.Width` | `28` | PaddingLeft `PadCard` · Fill `ThemeSurface2` · Text `$"Lotes de {Coalesce(gvarCajeroLot.cajeroNombre, "—")}"` · ESTILO-SECCION (sin MAYÚSCULAS forzadas) |

### 4.4 galLotesLot (Galería vertical en blanco, `TemplateSize 52`)

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `galLotesLot` | `PadCard` | `lblListaSubLot.Y + lblListaSubLot.Height` | `Parent.Width - 2*PadCard` | `Parent.Height - Self.Y - 8` | TemplateSize `52` |
| `cmpEmptyListaLot` (§8.6) | `PadCard` | `galLotesLot.Y` | `Parent.Width - 2*PadCard` | `galLotesLot.Height` | Visible `IsEmpty(FiltroLotesLot)` · Glifo `"⛁"` · Mensaje `"Sin lotes para este filtro."` |

> `IsEmpty(FiltroLotesLot)` **delega** el chequeo de vacío (no materializa). El empty y la
> galería comparten `X`/`Y`/`Width`/`Height` (uno tapa al otro según haya datos).

**`FiltroLotesLot`** (Items de `galLotesLot`) — **el filtro de lotes** referenciado en todo
el archivo. Igualdad de PK del lookup (delega), estado, número y fecha; orden por columna
propia `createdon` (corrige D-6):

```powerfx
SortByColumns(
    Filter('CH-Tbl-TransactionBatches',
        Cajero.'Primary Key' = gvarCajeroLot.cajero,
        ( ctxFiltroEstadoLot = "Todos" )
        || ( ctxFiltroEstadoLot = "Aplicados"
             && BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado )
        || ( ctxFiltroEstadoLot = "Por aplicar"
             && ( BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Generado
                  || BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado
                  || BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Aprobado ) ),
        ( IsBlank(inpBuscarLot.Text) || StartsWith(Name, inpBuscarLot.Text) ),
        ( tabFechaLot.Selected.Value = "Cualquier fecha" || 'Created On' >= ctxDesdeFechaLot )
    ),
    "createdon", SortOrder.Descending
)
```

> 🧨 **Delegación**: `Cajero.'Primary Key' = gvarCajeroLot.cajero` es la **navegación a la
> PK del lookup** contra el GUID de `colCajerosLot` → Dataverse la pliega a
> `_…cajero…_value eq <guid>`. Es la forma delegable (≠ `Cajero.OtraColumna`, que navega a
> una columna no-key, materializa 500 y era la raíz de D-6). `BatchStatus = …`,
> `StartsWith(Name, …)` y `'Created On' >= …` delegan; los `ctxFiltroEstadoLot = "…"` y
> `tabFechaLot.Selected.Value = "…"` son **constantes de cliente** que Power Fx pliega
> antes de la query. `SortByColumns("createdon")` (lógico, columna propia) delega. **Cero
> banda azul**. Si IntelliSense nombra distinto la PK/`createdon`, aceptá el suyo.

**Template** (posicionamiento manual con `Parent.TemplateWidth`):

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `shpLoteSelLot` (Rectángulo, PRIMERO) | `0` | `0` | `Parent.TemplateWidth` | `52` | Fill `If(ThisItem.TransactionBatches = gvarLote.TransactionBatches, ThemeAccentSoft, Color.Transparent)` · Border 0 |
| `shpLoteSelBarraLot` (Rectángulo) | `0` | `0` | `3` | `52` | Fill `ThemeAccent` · Visible `ThisItem.TransactionBatches = gvarLote.TransactionBatches` |
| `btnLoteFilaLot` (Botón overlay 7.2) | `0` | `0` | `Parent.TemplateWidth` | `52` | Fill/Hover/Pressed transparentes · Text `""` · OnSelect `Set(gvarLote, ThisItem); Set(gvarTxLot, Blank())` · AccessibleLabel `"Abrir lote " & ThisItem.Name` |
| `lblLoteNombreLot` (Etiqueta) | `13` | `8` | `Parent.TemplateWidth - 110` | `20` | Text `ThisItem.Name` · FontWeight Semibold · OnSelect `Select(Parent)` |
| `lblLoteMetaLot` (Etiqueta) | `13` | `30` | `Parent.TemplateWidth - 110` | `16` | Text `Text(ThisItem.'Created On', FmtFecha)` · ESTILO-DATO-K · OnSelect `Select(Parent)` |
| `pilLoteEstadoLot` (Botón píldora 7.2) | `Parent.TemplateWidth - 96` | `15` | `84` | `22` | Radius 12 · DisplayMode View · OnSelect `Select(Parent)` · Text/Fill/Color = **PILDORA-BATCHSTATUS** (abajo) |

**PILDORA-BATCHSTATUS** (mismo texto que usa `cmpPilDetEstadoLot`; acá como Botón 7.2 con
`<reg> = ThisItem`):

```powerfx
// Text
Switch(<reg>.BatchStatus,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Generado,  "Generado",
    'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado, "Ingresado",
    'BatchStatus (CH-Tbl-TransactionBatches)'.Aprobado,  "Aprobado",
    'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado,  "Aplicado", "—")
// Fill  (Generado/Aprobado=Info, Ingresado=Warn, Aplicado=OK)
Switch(<reg>.BatchStatus,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado, ThemeWarnSoft,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado,  ThemeOkSoft, ThemeInfoSoft)
// Color
Switch(<reg>.BatchStatus,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado, ThemeWarn,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado,  ThemeOk, ThemeInfo)
```

**PILDORA-BATCHSTATUS-TONO** (para `cmpPildora`, que recibe un `Tono` en vez de RGBA):

```powerfx
Switch(<reg>.BatchStatus,
    'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado, "warn",
    'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado,  "ok", "info")
```

> **Divergencia declarada vs mockup (LO1)**: la meta de la fila muestra la fecha
> (`Created On`), **no** el "· 142 tx" del prototipo. No hay columna materializada con el
> conteo de tx por lote y contar la relación no delega. El conteo real vive en el detalle
> (§8), donde el filtro ya está acotado a un lote.

---

## 5. Card de detalle — header y operaciones (`cntDetalleCardLot`)

Card 7.1, ancho ≈1.6× la lista (§2). `PadDet = 16`. Sus zonas se apilan por `Y` encadenado.

> **Estado vacío del detalle**: como el `OnVisible`/handlers pre-seleccionan `First(FiltroLotesLot)`,
> `gvarLote` solo queda `Blank()` si el cajero no tiene lotes en el filtro — y ahí la lista
> ya muestra `cmpEmptyListaLot`. Para no pintar un detalle fantasma, las zonas del detalle
> llevan `Visible = !IsBlank(gvarLote)` (cabecera, ops, KPIs, grid). No requieren tabla
> aparte: es la misma condición en las cuatro.

### 5.1 Header del detalle (`cntDetHeadLot`)

Contenedor manual: `X 0 · Y 0 · Width Parent.Width · Height 54` · Visible `!IsBlank(gvarLote)`.

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `lblDetTituloLot` | `PadDet` | `8` | `Parent.Width - 130` | `22` | Text `$"Lote {gvarLote.Name}"` · Size 12 · FontWeight Bold |
| `lblDetSubLot` | `PadDet` | `30` | `Parent.Width - 130` | `16` | Text `$"{Coalesce(gvarCajeroLot.cajeroNombre, gvarLote.Cajero.'Full Name')} · generado {Text(gvarLote.'Created On', FmtFecha)}"` · ESTILO-APPSUB |
| `cmpPilDetEstadoLot` (§8.3) | `Parent.Width - PadDet - Self.Width` | `16` | `84` | `22` | Texto = **PILDORA-BATCHSTATUS Text** con `<reg>=gvarLote` · Tono = **PILDORA-BATCHSTATUS-TONO** con `<reg>=gvarLote` |
| `shpDetHeadLineaLot` (Rectángulo) | `0` | `Parent.Height - 1` | `Parent.Width` | `1` | Fill `ThemeLine` |

### 5.2 Barra de operaciones (`cntOpsLot`)

Contenedor manual: `X 0 · Y cntDetHeadLot.Y + cntDetHeadLot.Height · Width Parent.Width ·
Height 48` · Visible `!IsBlank(gvarLote)`.

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `btnMarcarIngresadoLot` | `PadDet` | `8` | `176` | `32` | ESTILO-BTN-CONTORNO · Text `"Marcar como Ingresado"` · **Visible** `gvarLote.BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Generado` · OnSelect §5.3 |
| `btnMarcarAplicadoLot` | `PadDet` | `8` | `176` | `32` | ESTILO-BTN-CONTORNO · Text `"Marcar como Aplicado"` · **Visible** §5.4 (estado + segregación) · OnSelect `UpdateContext({ctxDlgAplicadoLot: true})` |
| `btnDescargarCsvLot` | `Parent.Width - PadDet - Self.Width` | `11` | `118` | `26` | ESTILO-BTN-CHICO acc (`Color ThemeAccent`, `BorderColor ThemeAccent`) · Text `If(ctxDescargandoLot, "Generando CSV…", "Descargar CSV")` · DisplayMode `If(ctxDescargandoLot, DisplayMode.Disabled, DisplayMode.Edit)` · OnSelect §6 |
| `btnSacarLoteLot` | `btnDescargarCsvLot.X - 8 - Self.Width` | `11` | `108` | `26` | ESTILO-BTN-CHICO dan (`Color ThemeDanger`, `BorderColor ThemeDangerLine`) · Text `"Sacar del lote"` · **Visible** `Or(gvarLote.BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Generado, gvarLote.BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado)` · **DisplayMode** `If(!IsBlank(gvarTxLot), DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `Reset(inpMotivoLot); UpdateContext({ctxDlgSacarLot: true})` |
| `shpOpsLineaLot` (Rectángulo) | `0` | `Parent.Height - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

> 🧨 **`btnMarcarIngresadoLot` y `btnMarcarAplicadoLot` comparten el slot `X PadDet`**: son
> **mutuamente excluyentes** por estado (Ingresado se ve solo en Generado; Aplicado solo en
> Ingresado), así que nunca colisionan visualmente. Es el sustituto manual del "spacer"
> de auto-layout: los de la izquierda con `X` fijo, los de la derecha anclados con
> `Parent.Width - PadDet - Self.Width` y encadenados hacia la izquierda.
>
> 🧨 **"Sacar del lote" en Generado E Ingresado** (dominio + guard `TransactionGuard.cs`):
> se saca en **Generado** (revisar/quitar antes de subir) y en **Ingresado** (los rechazos
> del Core ocurren durante la carga, con el lote ya Ingresado). Un **Aplicado** es
> intocable. Se habilita solo con **tx seleccionada** (`gvarTxLot`), y como el `OnSelect`
> de fila limpia `gvarTxLot` al cambiar de lote, nunca se saca una tx de **otro** lote
> (corrige C-1). `Reset(inpMotivoLot)` es al **abrir** el diálogo.

### 5.3 `btnMarcarIngresadoLot.OnSelect` (Generado → Ingresado)

```powerfx
IfError(
    Patch('CH-Tbl-TransactionBatches', gvarLote,
        { BatchStatus: 'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado });
    // el servidor estampa 'Ingresado por' / 'Ingresado el' (BatchTransitionStampPlugin);
    // re-leemos para refrescar estado + espejos de total (sin Refresh de tabla).
    Set(gvarLote, LookUp('CH-Tbl-TransactionBatches',
        TransactionBatches = gvarLote.TransactionBatches));
    Notify("Lote marcado como Ingresado.", NotificationType.Success),
    Notify("No se pudo marcar Ingresado: " & FirstError.Message, NotificationType.Error)
)
```

### 5.4 `btnMarcarAplicadoLot` — visibilidad con segregación de funciones

```powerfx
// Visible
gvarLote.BatchStatus = 'BatchStatus (CH-Tbl-TransactionBatches)'.Ingresado
&& Lower(Coalesce(gvarLote.'Ingresado por'.'Primary Email', "")) <> Lower(User().Email)
```

> **Segregación**: quien Ingresó el lote no puede Aplicarlo (control dual). La UI lo
> **oculta**; el límite real es el **guard server-side** (`TransactionGuard`, 00 §1.1) — si
> alguien lo saltea por Web API, el guard rechaza. Por eso el confirmar (§10) envuelve el
> `Patch` en `IfError` y muestra el motivo: nunca un rechazo mudo (C-2). Si `'Ingresado
> por'.'Primary Email'` no resuelve, usá el display name del email que ofrezca IntelliSense;
> el guard sigue siendo la barrera efectiva.

---

## 6. Barra de progreso del CSV (`cntProgresoLot`) y `btnDescargarCsvLot.OnSelect`

`cntProgresoLot` es una **franja overlay** sobre la zona de KPIs (no consume layout): así
las cards de abajo mantienen su `Y` fijo mientras el `Run` bloquea la UI.

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `cntProgresoLot` (Contenedor manual) | `PadDet` | `cntOpsLot.Y + cntOpsLot.Height + 6` | `Parent.Width - 2*PadDet` | `22` | Fill `Color.Transparent` · **Visible** `ctxDescargandoLot` |
| `lblProgresoLot` | `0` | `0` | `Parent.Width` | `22` | Text `"Generando el CSV para el core… no cierres la app."` · Size 8.25 · Color `ThemeInfo` |
| `shpProgresoTrackLot` (Rectángulo) | `0` | `18` | `Parent.Width` | `4` | Fill `ThemeLineSoft` · **Visible false** (L-3) |
| `shpProgresoFillLot` (Rectángulo) | `0` | `18` | `Parent.Width` | `4` | Fill `ThemeAccent` · **Visible false** (L-3) |

`btnDescargarCsvLot.OnSelect` — **descarga sin salir de Dataverse** (S2/V-3): el flow
endurecido valida el LoteId (corta la inyección OData), corre como `invoker`, arma el CSV y
lo **sube a la columna Archivo `sanic_csvcore` del propio lote**; la app descarga esa columna
por su endpoint `$value`:

```powerfx
UpdateContext({ctxDescargandoLot: true});
IfError(
    With(
        { res: 'CH-Flow-DownloadData'.Run(gvarLote.TransactionBatches, gvarLote.Name) },
        Download("https://org36e60d9d.crm.dynamics.com/api/data/v9.2/sanic_transactionbatcheses("
                 & gvarLote.TransactionBatches & ")/sanic_csvcore/$value")
    ),
    Notify("No se pudo generar el CSV: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxDescargandoLot: false})
```

> 🧨 **Por qué NO un data URI (corregido en runtime).** El diseño original hacía
> `Download("data:text/csv;base64," & res.csvBase64)`. **No funciona**: `Download()` rechaza
> los `data:` URI ("The URL passed to the function is not valid") y `Launch()` los acepta
> pero **Chrome/Edge bloquean en silencio** la navegación a `data:` — no baja nada y no hay
> error. El control **Texto HTML** con `<a download>` tampoco sirve: sanitiza el ancla y la
> deja como texto plano. En canvas apps **no existe** descarga local con cero almacenamiento.
>
> La solución que sí funciona (verificada contra DEV) es la **columna de tipo Archivo**:
> `<entityset>(<id>)/<columnaFile>/$value` devuelve los **bytes decodificados** con
> `content-type: application/octet-stream` y `content-disposition`. Ojo: una **nota**
> (annotation) NO sirve — `documentbody` es un campo de texto y su `$value` devuelve el
> base64 crudo. En el flow, la acción *Cargar un archivo o una imagen* trae el parámetro
> **`x-ms-file-name` con el literal `"Untitled"`**: hay que reemplazarlo (`@{triggerBody()?['text_1']}.csv`)
> o el archivo baja sin nombre ni extensión. El nombre solo se fija al subir — patchear
> `sanic_csvcore_name` después devuelve 204 pero Dataverse lo ignora.
>
> **Divergencia L-3 (sigue vigente)**: `Run()` **bloquea el hilo de UI** hasta que el flow
> termina; entre `ctxDescargandoLot: true` y `false` la pantalla **no repinta**, así que una
> barra que se "llena" durante el `Run` es imposible. Por eso `shpProgresoFillLot.Visible = false`
> y solo se muestra el texto + el botón deshabilitado (anti-doble-click).

---

## 7. Fila de KPIs por moneda — `cmpKpi` × 3 (01 §8.4)

Tres instancias de `cmpKpi`, ancladas al detalle. `kpiY = cntOpsLot.Y + cntOpsLot.Height +
12`; `kpiW = (Parent.Width - 2*PadDet - 24) / 3` (24 = 2 respiros de 12). Los valores salen
del **espejo del lote** (columnas materializadas por plugin — nunca `Sum` sobre related):

| Instancia | X | Y | Width | Height | `Etiqueta` | `Valor` |
|---|---|---|---|---|---|---|
| `cmpKpiNioLot` | `PadDet` | `cntOpsLot.Y + cntOpsLot.Height + 12` | `(Parent.Width - 2*PadDet - 24) / 3` | `LayKpiH` | `"TOTAL C$"` | `SimboloNIO & " " & Text(gvarLote.TotalNIO, FmtMoney)` |
| `cmpKpiUsdLot` | `cmpKpiNioLot.X + cmpKpiNioLot.Width + 12` | `cmpKpiNioLot.Y` | `cmpKpiNioLot.Width` | `LayKpiH` | `"TOTAL US$"` | `SimboloUSD & " " & Text(gvarLote.TotalUSD, FmtMoney)` |
| `cmpKpiEurLot` | `cmpKpiUsdLot.X + cmpKpiUsdLot.Width + 12` | `cmpKpiNioLot.Y` | `cmpKpiNioLot.Width` | `LayKpiH` | `"TOTAL €"` | `SimboloEUR & " " & Text(gvarLote.TotalEUR, FmtMoney)` |

Todas con Visible `!IsBlank(gvarLote)`. La card blanca, radio, label y valor internos viven
en §8.4 — acá solo se posicionan y se cablean las dos props.

---

## 8. Grilla de transacciones del lote (`cntGridCardLot`) — DELEGABLE (corrige D-1)

Card 7.1. Su alto **reserva sitio** para el historial cuando hay tx seleccionada:

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `cntGridCardLot` (card 7.1) | `PadDet` | `cmpKpiNioLot.Y + LayKpiH + 12` | `Parent.Width - 2*PadDet` | `Parent.Height - Self.Y - PadDet - If(!IsBlank(gvarTxLot), cntHistCardLot.Height + LayGap, 0)` | Visible `!IsBlank(gvarLote)` |

> `cntHistCardLot.Height` es `HistH` (200, constante), así que **no hay ciclo**: el grid
> resta un valor fijo; el historial (§9) referencia el borde inferior del grid.

### 8.1 Encabezados (dentro de `cntGridCardLot`, `Parent.Width` = ancho de la card)

Cinco etiquetas ESTILO-HEADER-COL con el **reparto de columnas** enunciado arriba (fr
`1.1/2/1/.9/.6`, suma `5.6`). `X` encadenado desde `PadDet`; ancho = `(Parent.Width -
2*PadDet) * fr/5.6`. `Y 8 · Height 16`.

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblGCol1Lot` | `"REFERENCIA"` | `PadDet` | `(Parent.Width - 2*PadDet) * 1.1/5.6` | Left |
| `lblGCol2Lot` | `"DEPÓSITO / CLIENTE"` | `lblGCol1Lot.X + lblGCol1Lot.Width` | `(Parent.Width - 2*PadDet) * 2/5.6` | Left |
| `lblGCol3Lot` | `"CUENTA"` | `lblGCol2Lot.X + lblGCol2Lot.Width` | `(Parent.Width - 2*PadDet) * 1/5.6` | Right |
| `lblGCol4Lot` | `"MONTO"` | `lblGCol3Lot.X + lblGCol3Lot.Width` | `(Parent.Width - 2*PadDet) * 0.9/5.6` | Right |
| `lblGCol5Lot` | `"MON."` | `lblGCol4Lot.X + lblGCol4Lot.Width` | `(Parent.Width - 2*PadDet) * 0.6/5.6` | Right |

`shpGridLineaLot` (Rectángulo): `X 0 · Y 30 · Width Parent.Width · Height 1 · Fill ThemeLine`.

### 8.2 galTxLot (Galería vertical en blanco, `TemplateSize 44`)

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `galTxLot` | `0` | `31` | `Parent.Width` | `Parent.Height - 31` |

**Items — el patrón delegable que corrige D-1** (igualdad de lookup por PK, sin dot-walk
que trunque):

```powerfx
Filter('CH-Tbl-Transactions',
    'Transaction Batch'.TransactionBatches = gvarLote.TransactionBatches)
```

> 🧨 EXACTAMENTE el fix de D-1 (00 §1.2). `'Transaction Batch'` es el lookup de la tx al
> lote; `.TransactionBatches` su **PK** → Dataverse pliega a `_sanic_transactionbatch_value
> eq <guid>`. **No** es el dot-walk prohibido `Lookup.OtraColumna` (materializa 500 y filtra
> en cliente, grid vacío con totales millonarios al lado). Cero banda azul. Si IntelliSense
> nombra distinto el lookup/la PK, aceptá el suyo.

**Template** — cellas por **fracción de `Parent.TemplateWidth`** (mismo reparto que los
encabezados; `X` cell = `X` header porque la galería arranca en `X 0` de la card):

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `shpTxSelLot` (Rectángulo, PRIMERO) | `0` | `0` | `Parent.TemplateWidth` | `44` | Fill `If(!IsBlank(gvarTxLot) && ThisItem.Transactions = gvarTxLot.Transactions, ThemeAccentSoft, Color.Transparent)` · Border 0 |
| `btnTxFilaLot` (Botón overlay 7.2) | `0` | `0` | `Parent.TemplateWidth` | `44` | Fill/Hover/Pressed transparentes · Text `""` · al FONDO · OnSelect §9 · AccessibleLabel `"Ver historial de " & ThisItem.Name` |
| `lblTxRefLot` | `PadDet` | `0` | `(Parent.TemplateWidth - 2*PadDet) * 1.1/5.6` | `44` | Text `ThisItem.Name` · FontWeight Semibold · OnSelect `Select(Parent)` |
| `lblTxDepositoLot` | `lblTxRefLot.X + lblTxRefLot.Width` | `0` | `(Parent.TemplateWidth - 2*PadDet) * 2/5.6` | `44` | Text `Coalesce(ThisItem.RelatedDepositRecounting.Name, "—")` · OnSelect `Select(Parent)` |
| `lblTxCuentaLot` | `lblTxDepositoLot.X + lblTxDepositoLot.Width` | `0` | `(Parent.TemplateWidth - 2*PadDet) * 1/5.6` | `44` | Text `Coalesce(Text(ThisItem.AccountNumber, "[$-en-US]0"), "—")` · Align Right · OnSelect `Select(Parent)` |
| `lblTxMontoLot` | `lblTxCuentaLot.X + lblTxCuentaLot.Width` | `0` | `(Parent.TemplateWidth - 2*PadDet) * 0.9/5.6` | `44` | Text `Text(ThisItem.Total, FmtMoney)` · ESTILO-CELDA-NUM · FontWeight Semibold · OnSelect `Select(Parent)` |
| `lblTxMonedaLot` | `lblTxMontoLot.X + lblTxMontoLot.Width` | `0` | `(Parent.TemplateWidth - 2*PadDet) * 0.6/5.6` | `44` | Text = **MONEDA-TX** (abajo) · Align Right · OnSelect `Select(Parent)` |
| `shpTxSelBarraLot` (Rectángulo) | `0` | `0` | `3` | `44` | Fill `ThemeAccent` · Visible `!IsBlank(gvarTxLot) && ThisItem.Transactions = gvarTxLot.Transactions` |
| `shpTxSepLot` (Rectángulo) | `0` | `Parent.TemplateHeight - 1` | `Parent.TemplateWidth` | `1` | Fill `ThemeLineSoft` |

**MONEDA-TX** — el símbolo de la moneda de la tx:

```powerfx
Switch( Text(ThisItem.Currency),
    "NIO", SimboloNIO, "USD", SimboloUSD, "EUR", SimboloEUR,
    Text(ThisItem.Currency) )   // si Currency es choice, usa su label; ajustá si difiere
```

> **`Total` vs `CashAmount`**: usá la columna de monto real del esquema; si el importe vive
> en `CashAmount`, cambiá `lblTxMontoLot.Text` a `Text(ThisItem.CashAmount, FmtMoney)`.
> **Divergencia declarada (§13)**: la columna "Depósito / cliente" muestra solo la ref del
> depósito (`RelatedDepositRecounting.Name`); el nombre de cliente exige dot-walk profundo
> (igual que 02). Es dot-walk de **display**, no de filtro — no afecta la delegación del
> grid (que sigue igualdad de lookup por PK).

---

## 9. Historial / timeline de la transacción (`cntHistCardLot`)

Card 7.1, `Visible !IsBlank(gvarTxLot)` (solo con tx seleccionada — LO3). Se ancla al borde
inferior del grid:

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `cntHistCardLot` (card 7.1) | `PadDet` | `cntGridCardLot.Y + cntGridCardLot.Height + LayGap` | `Parent.Width - 2*PadDet` | `HistH` (200) | Visible `!IsBlank(gvarTxLot)` |
| `lblHistTituloLot` | `14` | `10` | `Parent.Width - 28` | `18` | Text `$"HISTORIAL · {gvarTxLot.Name}"` · ESTILO-SECCION |
| `galHistLot` (Galería vertical en blanco) | `14` | `32` | `Parent.Width - 28` | `Parent.Height - 40` | Items `colHistLot` · TemplateSize `58` |

`btnTxFilaLot.OnSelect` (§8.2) — setea la tx **y compone el timeline** con columnas reales
de la tx (aproximación honesta; ver nota):

```powerfx
Set(gvarTxLot, ThisItem);
ClearCollect(colHistLot,
    Filter(
        Table(
            { orden: 1, titulo: "Incluida en el corte del lote",
              meta:   Text(ThisItem.'Created On', FmtFecha) & " · " & ThisItem.'Created By'.'Full Name',
              motivo: "", dot: ThemeInk3, mostrar: true },
            { orden: 2, titulo: "Sacada del lote → circuito manual",
              meta:   Text(ThisItem.'Modified On', FmtFecha),
              motivo: Coalesce(ThisItem.sanic_motivorechazocore, ""),
              dot:    ThemeWarn,
              mostrar: !IsBlank(ThisItem.sanic_motivorechazocore) },
            { orden: 3, titulo: "Aplicada",
              meta:   Text(ThisItem.'Modified On', FmtFecha),
              motivo: "", dot: ThemeOk,
              mostrar: ThisItem.TransactionStatus = 'TransactionStatus (CH-Tbl-Transactions)'.Aplicada }
        ),
        mostrar
    )
)
```

**Template de `galHistLot`:**

| Control | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|
| `shpHistDotLot` (Rectángulo) | `2` | `8` | `9` | `9` | Fill `ThisItem.dot` |
| `lblHistItemLot` (Etiqueta) | `22` | `4` | `Parent.TemplateWidth - 30` | `18` | Text `ThisItem.titulo` · FontWeight Semibold · Size 9 |
| `lblHistMetaLot` (Etiqueta) | `22` | `22` | `Parent.TemplateWidth - 30` | `16` | Text `ThisItem.meta` · ESTILO-DATO-K |
| `lblHistMotivoLot` (Etiqueta) | `22` | `38` | `Parent.TemplateWidth - 30` | `18` | Text `ThisItem.motivo` · Size 9 · Color `ThemeInk2` · Wrap true · Visible `!IsBlank(ThisItem.motivo)` |

> 🧨 **Aproximación honesta declarada**: no hay tabla de eventos como datasource, así que el
> timeline **no** es un log de auditoría real — se **deriva** de columnas de la propia tx
> (`Created On`/`Created By` → entró al corte; `sanic_motivorechazocore` presente → salió a
> manual con ese motivo; `TransactionStatus = Aplicada` → aplicada). Cada entrada aparece
> solo si su dato existe (`mostrar`), así que nunca se inventa un paso. La línea
> "Reincorporada al lote" del mockup (LO3) se **omite**: no hay columna que la respalde.
> **Backend**: `sanic_motivorechazocore` (texto en `CH-Tbl-Transactions`, 00 §3.3) lo
> escribe `sanic_CHRemoveFromCut` (§11). **Sin ese backend**, reemplazá
> `ThisItem.sanic_motivorechazocore` por `ThisItem.Comentarios` (si existe) o por `""`. Si
> el literal del choice `TransactionStatus` difiere, aceptá el de IntelliSense.

---

## 10. Diálogo confirmar Aplicado — `cmpDlgAplicadoLot` (instancia de `cmpDialogo`, 01 §8.5)

Solo texto + botones → **es componente**. Instancia a **pantalla completa**, fuera de
`cntBodyLot` (esqueleto 7.0). `X 0 · Y 0 · Width Parent.Width · Height Parent.Height`. Toda
la estructura (overlay, bloqueo, card centrada, ícono, ✕, botones) vive en §8.5; la pantalla
cablea sus props:

| Propiedad | Valor |
|---|---|
| `Mostrar` | `ctxDlgAplicadoLot` |
| `Tono` | `"ok"` |
| `Titulo` | `$"¿Marcar el lote {gvarLote.Name} como Aplicado?"` |
| `Texto` | `"El lote pasa a Aplicado (ya ingresado al core). El servidor registra quién y cuándo. Esta acción cierra el ciclo del lote."` |
| `TextoOk` | `"Sí, marcar Aplicado"` |
| `TextoCancelar` | `"Cancelar"` |
| `AnchoCard` | `420` |
| `OkHabilitado` | `!ctxAplicandoLot` |
| `OnCancelar` | `UpdateContext({ctxDlgAplicadoLot: false})` |
| `OnOk` | *(abajo)* |

```powerfx
// cmpDlgAplicadoLot.OnOk  — confirmar ANTES de ejecutar (§8.5)
UpdateContext({ctxAplicandoLot: true});
IfError(
    Patch('CH-Tbl-TransactionBatches', gvarLote,
        { BatchStatus: 'BatchStatus (CH-Tbl-TransactionBatches)'.Aplicado });
    Set(gvarLote, LookUp('CH-Tbl-TransactionBatches',
        TransactionBatches = gvarLote.TransactionBatches));
    Notify("Lote aplicado.", NotificationType.Success),
    // el guard rechaza si se viola la segregación o el estado: feedback, nunca mudo (C-2)
    Notify("No se pudo aplicar: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxAplicandoLot: false, ctxDlgAplicadoLot: false})
```

> Reglas §8.5: la ✕ tiene el **mismo** OnSelect que Cancelar (interno del componente); la
> acción destructiva vive **solo** en `OnOk`; el OK se deshabilita mientras corre
> (`OkHabilitado = !ctxAplicandoLot`, anti-doble-click) y el diálogo se cierra al terminar.

---

## 11. Diálogo sacar del lote — `cntDlgSacarLot` (DIRECTO en la pantalla, LO2)

Lleva **textarea de motivo** (contenido rico) → **no** se componentiza (01 §8.5 nota). Se
construye **directo en la pantalla** con la **misma estructura interna** que `cmpDialogo`
(§8.5) + el textarea + el banner. Overlay a pantalla completa, fuera de `cntBodyLot`.

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `cntDlgSacarLot` (Contenedor manual) | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · **Visible** `ctxDlgSacarLot` |
| `btnDlgSacarBloqueoLot` (Botón) | `0` | `0` | `Parent.Width` | `Parent.Height` | al FONDO · Fill/Hover/Pressed transparentes · Text `""` · **OnSelect `false`** |
| `cntDlgSacarCardLot` (Contenedor manual) | `(Parent.Width - Self.Width) / 2` | `(Parent.Height - Self.Height) / 2` | `440` | `312` | Fill `ThemeSurface` · Radius 13 · Border `ThemeLine` 1 · **DropShadow.Regular** |

**Hijos de `cntDlgSacarCardLot`** (`Parent.Width` = 440; `PadDlg = 20`):

| Control | X | Y | Width | Height | Propiedades |
|---|---|---|---|---|---|
| `shpDlgSacarIcoLot` (Botón) | `PadDlg` | `PadDlg` | `34` | `34` | Radius 9 · DisplayMode View · Fill `ThemeDangerSoft` · Color `ThemeDanger` · Text `"↩"` · Border 0 |
| `lblDlgSacarTitLot` (Etiqueta) | `shpDlgSacarIcoLot.X + shpDlgSacarIcoLot.Width + 10` | `PadDlg` | `Parent.Width - Self.X - 44` | `22` | Text `$"¿Sacar {gvarTxLot.Name} del lote {gvarLote.Name}?"` · Size 10.88 · Bold |
| `lblDlgSacarTxtLot` (Etiqueta) | `lblDlgSacarTitLot.X` | `lblDlgSacarTitLot.Y + 24` | `Parent.Width - Self.X - PadDlg` | `40` | Text ↓ · Size 9 · Color `ThemeInk2` · Wrap true · AutoHeight true |
| `icoDlgSacarXLot` (Icono `Icon.Cancel`) | `Parent.Width - PadDlg - 18` | `PadDlg` | `18` | `18` | Color `ThemeInk3` · HoverColor `ThemeInk` · **OnSelect = el del Cancelar** |
| `lblDlgSacarMotivoLabLot` (Etiqueta) | `PadDlg` | `96` | `Parent.Width - 2*PadDlg` | `16` | Text `"Motivo del rechazo del core"` · Size 8.25 · FontWeight Semibold · Color `ThemeInk2` |
| `inpMotivoLot` (Entrada de texto clásica) | `PadDlg` | `116` | `Parent.Width - 2*PadDlg` | `64` | **Mode `TextMode.MultiLine`** · HintText `"Ej: cuenta inexistente en IBS Branch, moneda no habilitada…"` · Fill `ThemeSurface` · BorderColor `ThemeLine` · Radius 7 · Size 9 |
| `cntDlgSacarBannerLot` (Contenedor manual) | `PadDlg` | `188` | `Parent.Width - 2*PadDlg` | `52` | Fill `ThemeWarnSoft` · Radius 9 |
| `lblDlgSacarBannerLot` (Etiqueta) | `13` | `8` | `Parent.Width - 26` | `36` | Text ↓ · Size 8.25 · Color `ThemeWarn` · Wrap true · AutoHeight true |
| `btnDlgSacarOkLot` (Botón) | `Parent.Width - PadDlg - Self.Width` | `Parent.Height - PadDlg - 32` | `130` | `32` | ESTILO-BTN-PELIGRO · Text `"Sacar del lote"` · **DisplayMode** `If(ctxSacandoLot Or IsBlank(inpMotivoLot.Text), DisplayMode.Disabled, DisplayMode.Edit)` · OnSelect ↓ |
| `btnDlgSacarCancelLot` (Botón) | `btnDlgSacarOkLot.X - 10 - Self.Width` | `btnDlgSacarOkLot.Y` | `96` | `32` | ESTILO-BTN-FANTASMA · Text `"Cancelar"` · OnSelect `UpdateContext({ctxDlgSacarLot: false})` |

- `lblDlgSacarTxtLot.Text`:
  `$"La transacción ({MONEDA-TX del gvarTxLot} {Text(gvarTxLot.Total, FmtMoney)} · {Coalesce(gvarTxLot.RelatedDepositRecounting.Name, "—")}) se desasigna del lote y vuelve al circuito manual. Los totales del lote se recalculan."`
- `lblDlgSacarBannerLot.Text`:
  `"El motivo queda registrado en la transacción para auditoría (se ve luego en su historial). Fuera del lote, la transacción no vuelve sola a un corte automático."`

`btnDlgSacarOkLot.OnSelect` — **Custom API vía Environment** (01 §2): valida (Generado o
Ingresado), desasigna, recalcula y **devuelve** los totales nuevos (cero Refresh, cero
carrera):

```powerfx
UpdateContext({ctxSacandoLot: true});
IfError(
    With(
        { r: Environment.sanic_CHRemoveFromCut(
                { TransactionId: gvarTxLot.Transactions, Motivo: inpMotivoLot.Text }) },
        // el API devuelve los totales recalculados del lote: parcheamos el espejo local
        // y REASIGNAMOS a gvarLote (Patch en forma de mezcla de records DEVUELVE el record
        // fusionado; hay que Set-earlo o los KPIs no cambian).
        Set(gvarLote, Patch(gvarLote,
            { TotalNIO: r.TotalNIO, TotalUSD: r.TotalUSD, TotalEUR: r.TotalEUR }))
    );
    Set(gvarTxLot, Blank());
    Clear(colHistLot);
    Notify("Transacción sacada del lote y devuelta a Manual.", NotificationType.Success),
    Notify("No se pudo sacar del lote: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxSacandoLot: false, ctxDlgSacarLot: false})
```

> 🧨 **Sacar en Generado o Ingresado**: el botón que abre este diálogo ya lo garantiza
> (§5.2), pero el **guard server-side** (`sanic_CHRemoveFromCut` valida contra
> `TransactionGuard`) es la barrera real — el `IfError` muestra su rechazo. La tx, al
> desasignarse, vuelve al pool **Manual** (aparece en scrManual → Pendientes). El motivo se
> persiste en `sanic_motivorechazocore` y se ve en el historial (§9) y en Manual → Aplicadas
> (MA2). La ✕ tiene el **mismo** OnSelect que Cancelar; la acción destructiva vive **solo**
> en el OnSelect del confirmar; `Reset(inpMotivoLot)` es al **abrir** (§5.2), no al cerrar.
>
> **Sin backend** (`sanic_CHRemoveFromCut` no existe) — fallback 00 §9, sin recálculo
> garantizado en el mismo turno:
> ```powerfx
> IfError(
>     Patch('CH-Tbl-Transactions', gvarTxLot,
>         { 'Transaction Batch': Blank(),
>           sanic_motivorechazocore: inpMotivoLot.Text });   // o Comentarios si no existe
>     Set(gvarLote, LookUp('CH-Tbl-TransactionBatches',
>         TransactionBatches = gvarLote.TransactionBatches));   // espejos: plugin asíncrono
>     Set(gvarTxLot, Blank()); Clear(colHistLot);
>     Notify("Transacción sacada del lote.", NotificationType.Success),
>     Notify("No se pudo sacar del lote: " & FirstError.Message, NotificationType.Error)
> )
> ```
> Los nombres de los campos de retorno del API (`TotalNIO/TotalUSD/TotalEUR`) pueden diferir
> — usá los que exponga la acción.

---

## 12. Dependencias de backend (fallbacks) — resumen

| Dep | Dónde en esta guía | Fallback sin backend |
|---|---|---|
| `sanic_CHWorkloadByCashier` | §1/§3 rail con badge de conteo | `colCajerosLot` sobre `Users`, sin badge; conteo por cajero no delega (provisorio) |
| `sanic_CHRemoveFromCut({TransactionId, Motivo})` | §11 sacar del lote | `Patch('Transaction Batch': Blank())` + `Patch` del motivo; recálculo de espejos no garantizado en el turno |
| `sanic_motivorechazocore` (texto en Transactions) | §9 historial, §11 motivo | Guardar el motivo en `Comentarios`; historial usa `Comentarios`/`Modified On` como aproximación |
| `CH-Flow-DownloadData` (export CSV) | §6 | Ya existe (pagina 100k); barra de progreso **real** solo con patrón asíncrono — sin él, texto + botón deshabilitado (L-3) |

Transiciones (`Ingresado`/`Aplicado`) NO necesitan backend nuevo: el `Patch` de
`BatchStatus` dispara `BatchTransitionStampPlugin` (estampa por/on) y `TransactionGuard`
(valida transición + segregación). Ya maduros (00 §1.1).

---

## 13. Divergencias declaradas vs mockup

- **L-3** (heredada de 00 §6) — sin spinner animado durante el `Run` del CSV: el hilo está
  bloqueado; texto estático + botón deshabilitado. Barra "real" solo con patrón asíncrono (§6).
- **Selección por `gvar` y no por `IsSelected`** (§3, §4, §8): el `OnVisible` pre-selecciona
  cajero y lote; el resaltado lee la variable. Mismo criterio que el tracker (`gvarPlanilla`).
- **Filtro de fecha con Tab list moderna** (§4.2): el mockup usa un `<select>`; canvas lo
  resuelve con una Lista de pestañas moderna — el único control no clásico, sancionado por el
  mockup ("TabList moderno donde haga falta") y sin violar el catálogo cerrado (no es
  PCF/Form/tercero).
- **Meta de la fila de lote sin "· N tx"** (§4.4): no hay conteo materializado por lote y
  contar la relación no delega. El conteo real vive en el detalle, acotado a un lote.
- **Columna "Depósito / cliente"** (§8.2, texto del diálogo §11): el mockup combina `DR-4462
  · Casa Pellas`; el esquema solo expone la ref del depósito a un dot-walk simple
  (`RelatedDepositRecounting.Name`); el nombre del cliente exige navegación profunda no
  fiable en galería. Se muestra la ref con fallback `"—"` (misma decisión y dep
  `sanic_clientname` que 02). Es dot-walk de **display**, no de filtro.
- **Timeline derivado, no auditado** (§9): sin tabla de eventos, el historial se deriva de
  columnas de la tx; la entrada "Reincorporada al lote" del mockup (LO3) se omite por falta
  de dato que la respalde.

---

## 14. Checklist de salida (verificable por el constructor)

- [ ] **Árbol ↔ cuerpo 1:1**: todo control del árbol tiene tabla o puntero a receta/componente; nada de más.
- [ ] **Todo control** fija `X`/`Y`/`Width`/`Height` con **fórmula relativa** a `Parent.Width`/`Parent.Height` + `Lay*` (01 §7.0); cero número mágico suelto; bordes compartidos por referencia.
- [ ] Esqueleto 7.0: `cmpHeaderLot` (`Activo="Lotes"`) + `cntBodyLot` manual → `cmpRailLot` + `cntMainLot` manual; los dos diálogos como overlays **fuera** de `cntBodyLot`.
- [ ] Componentes instanciados y cableados: `cmpHeader`, `cmpRailCajeros` (badge=lotesPorAplicar), `cmpPildora` (detalle), `cmpKpi` ×3, `cmpDialogo` (Aplicado), `cmpEmpty` (lista).
- [ ] Rail: `colCajerosLot` desde `ParseJSON(Environment.sanic_CHWorkloadByCashier().Workload)`, filtrado `lotesPorAplicar > 0`; `OnSeleccion` espeja a `gvarCajeroLot` y limpia tx (o fallback declarado sin badge).
- [ ] Lista de lotes: segmentado Por aplicar / Aplicados / Todos + buscador + Tab list de fecha; `Cajero.'Primary Key' = gvarCajeroLot.cajero`; ordena por **`createdon`**; **cero banda azul** (corrige D-6); `cmpEmptyListaLot` con `IsEmpty`.
- [ ] Grid del lote **no vacío** con datos reales y **cero banda azul** (corrige D-1); columnas alineadas header↔fila por la **misma fracción** de ancho (reparto `/5.6`).
- [ ] KPIs C$/US$/€ vía `cmpKpi` desde el **espejo** del lote (no `Sum` sobre related).
- [ ] "Marcar Ingresado" visible solo en **Generado**; "Marcar Aplicado" solo en **Ingresado** y si el usuario **no** es quien Ingresó (segregación); el guard rechaza con feedback, nunca mudo (corrige C-2).
- [ ] "Sacar del lote" visible en **Generado o Ingresado** y habilitado solo con **tx seleccionada**; al cambiar de lote/cajero/estado/fecha `gvarTxLot` se limpia (corrige C-1).
- [ ] Diálogo Aplicado (`cmpDialogo`): ✕ = Cancelar; acción solo en `OnOk`; `OkHabilitado = !ctxAplicandoLot`; cierre al terminar.
- [ ] Diálogo Sacar (directo, §11): textarea `inpMotivoLot` MultiLine; `Reset(inpMotivoLot)` al **abrir**; OK deshabilitado sin motivo o mientras corre; ✕ = Cancelar; `Environment.sanic_CHRemoveFromCut({TransactionId, Motivo})` → `Set(gvarLote, Patch(..., {TotalNIO, TotalUSD, TotalEUR}))`.
- [ ] Historial visible solo con tx seleccionada; el grid reserva su alto (`HistH`) cuando hay tx; motivo del rechazo visible **después** de sacar (§9).
- [ ] Descargar CSV: botón se deshabilita durante el `Run`; sin barra falsa (L-3).
- [ ] `OnVisible` limpia `gvarTxLot`, resetea `tabFechaLot`/`inpBuscarLot`, carga `colCajerosLot` y pre-selecciona cajero + lote.
- [ ] App Checker: cero errores; `AccessibleLabel` en overlays de fila e íconos.
