# Guía 03 — scrLote

Detalle del lote de recepción: ficha, montos declarados, planillas (master) y
tulas de la planilla seleccionada (detail).

**Contrato de entrada**: `gvarLote` SIEMPRE tiene un registro de
CH-Tbl-ReceptionBatches. `gvarPlanilla` puede venir con una planilla
preseleccionada (búsqueda por planilla) o `Blank()`.

## Árbol de controles

```
scrLote                            (Fill: ThemeBg)
├── shpHeaderLote / shpHeaderLineaLote / shpMarcaLote / lblMarcaLote / lblMarcaSubLote   (receta 7.5)
├── lblCrumbBusquedaLote ─ lblCrumbSep1Lote ─ lblCrumbActualLote      (migas, receta 7.6)
├── shpFichaCardLote               card ficha del lote
│   ├── lblLoteTitulo              "Lote de recepción RB-…"
│   ├── pilLoteEstado              estado del lote
│   └── lblFichaK1Lote..K5Lote / lblFichaV1Lote..V5Lote   pares dato (recibido, por, transportadora, ubicación, planillas)
├── shpMontosCard                  card montos declarados
│   ├── lblMontosTitulo
│   ├── lblMonCol1..4              encabezados (MONEDA/EFECTIVO/CHEQUES/TOTAL)
│   ├── shpMontosLinea
│   ├── lblMonNio1..4              fila NIO (oculta si el total NIO es 0)
│   ├── lblMonUsd1..4              fila USD (oculta si el total USD es 0)
│   └── lblMontosVacioLote         "Sin montos declarados todavía."
├── shpPlanillasCard               card izquierda
│   ├── lblPlanillasTitulo
│   ├── lblPCol1..4 / shpPlanillasLinea
│   └── galPlanillas
│       ├── shpPlanillaSel         fondo de fila seleccionada
│       ├── shpPlanillaSelBarra    barra lateral de acento (receta 7.4)
│       ├── btnPlanillaFila        overlay clicable
│       ├── lblPFilaId / lblPFilaSucursal / lblPFilaNio / lblPFilaUsd
│       └── shpPFilaSep
└── shpTulasCard                   card derecha
    ├── lblTulasTitulo
    ├── lblTCol1..3 / shpTulasLinea
    ├── lblTulasVacio              "Seleccioná una planilla…"
    └── galTulas
        ├── btnTulaFila            overlay → scrTula
        ├── lblTFilaId
        ├── pilTFilaEstado
        ├── pilTFilaDif
        └── shpTFilaSep
```

## 1. Pantalla y migas

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | *(nada — todo viene por gvar y filtros delegables)* |

Encabezado: **receta 7.5 — 5 controles**, con el sufijo `Lote` de la regla 0.5:
`shpHeaderLote` (Rectángulo clásico), `shpHeaderLineaLote` (Rectángulo clásico),
`shpMarcaLote` (Botón clásico), `lblMarcaLote` (Etiqueta clásica) y
`lblMarcaSubLote` (Etiqueta clásica); propiedades fijadas en la guía 01.

Migas (receta 7.6, Y `64`, Height `22`, Size `9`) — **las tres son Etiquetas
clásicas**:

| Control | Propiedad | Valor |
|---|---|---|
| `lblCrumbBusquedaLote` (Etiqueta clásica) | X/W | `24` / `70` · Text `"Búsqueda"` · Color `ThemeAccent` |
| | OnSelect | `Navigate(scrBusqueda)` |
| `lblCrumbSep1Lote` (Etiqueta clásica) | X/W | `94` / `14` · Text `"›"` · Color `ThemeInk3` |
| `lblCrumbActualLote` (Etiqueta clásica) | X/W | `108` / `200` · Text `$"Lote {gvarLote.Name}"` · Color `ThemeInk2` |

## 2. Ficha del lote

| Control | Propiedad | Valor |
|---|---|---|
| `shpFichaCardLote` (Rectángulo clásico) | X/Y/W/H | `24` / `94` / `1318` / `110` — receta 7.1 |
| `lblLoteTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `108` / `420` / `30` |
| | Text | `$"Lote de recepción {gvarLote.Name}"` · ESTILO-TITULO |
| `pilLoteEstado` (Botón clásico, receta 7.2) | X/Y/W/H | `470` / `110` / `150` / `26` |
| | Text / Fill / Color | igual que `pilFilaEstado` de la guía 02 pero con `gvarLote.ReceptionStatus` |

Pares dato — **Etiquetas clásicas** `lblFichaK1Lote`..`lblFichaK5Lote` (Y `148`,
ESTILO-DATO-K) y `lblFichaV1Lote`..`lblFichaV5Lote` (Y `164`, ESTILO-DATO-V),
Height `16` (K, parte de ESTILO-DATO-K) / `22` (V). La fila N de la tabla es el
par `lblFichaKNLote` / `lblFichaVNLote`:

| K (Text) | V (Text) | X | Width |
|---|---|---|---|
| `"RECIBIDO"` | `Text(gvarLote.'Created On', FmtFecha)` | `44` | `180` |
| `"RECIBIDO POR"` | `gvarLote.'Created By'.'Full Name'` | `244` | `220` |
| `"TRANSPORTADORA"` | `gvarLote.Transportadora` | `484` | `200` |
| `"UBICACIÓN"` | `Coalesce(gvarLote.Location.Name, "—")` | `704` | `200` |
| `"PLANILLAS"` | `Text(CountRows(galPlanillas.AllItems))` | `924` | `120` |

> - `Location` requiere el datasource **CH-Tbl-Locations** (guía 01 §2, #20).
> - El conteo usa `galPlanillas.AllItems` (client-side): NO uses `CountRows`
>   sobre la relación del datasource — no delega. Límite aceptado: `AllItems`
>   cuenta la página cargada (~100); los lotes reales tienen <20 planillas.
> - El prototipo mostraba también conteo de TULAS (ficha y grilla de
>   planillas): se quitó a consciencia — no hay contador materializado y
>   contar relaciones no delega (decisión en 00-§2.5).
> - **RECIBIDO / RECIBIDO POR usan `Created On` / `Created By` del lote** —
>   aproximación honesta: no existen columnas de recepción propias. Mismo
>   criterio que `Modified On` en la tula (guía 04 §3).

## 3. Montos declarados del lote (backend B4)

| Control | Propiedad | Valor |
|---|---|---|
| `shpMontosCard` (Rectángulo clásico) | X/Y/W/H | `24` / `216` / `1318` / `140` — receta 7.1 |
| `lblMontosTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `230` / `600` / `20` · Text `"MONTOS DECLARADOS EN LAS PLANILLAS DEL LOTE"` · ESTILO-SECCION |

Encabezados `lblMonCol1`..`lblMonCol4`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `258`). Filas `lblMonNio1`..`lblMonNio4` (Y `282`) y `lblMonUsd1`..`lblMonUsd4`
(Y `310`): **Etiquetas clásicas**, Height `24`, ESTILO-CELDA; los montos
(columnas 2 a 4) ESTILO-CELDA-NUM. La columna N de la tabla es la terna
`lblMonColN` / `lblMonNioN` / `lblMonUsdN`:

| Columna | X | Width | Fila NIO (Text) | Fila USD (Text) |
|---|---|---|---|---|
| `"MONEDA"` | `44` | `160` | `"NIO · C$"` (FontWeight Semibold) | `"USD · US$"` (FontWeight Semibold) |
| `"EFECTIVO"` | `204` | `170` | `Text(Coalesce(gvarLote.DecNIOCash, 0), FmtMoney)` | `Text(Coalesce(gvarLote.DecUSDCash, 0), FmtMoney)` |
| `"CHEQUES"` | `374` | `170` | `Text(Coalesce(gvarLote.DecNIOCheck, 0), FmtMoney)` | `Text(Coalesce(gvarLote.DecUSDCheck, 0), FmtMoney)` |
| `"TOTAL"` | `544` | `170` | `Text(Coalesce(gvarLote.DecNIOTotal, 0), FmtMoney)` (Semibold) | `Text(Coalesce(gvarLote.DecUSDTotal, 0), FmtMoney)` (Semibold) |

`shpMontosLinea` (Rectángulo clásico): X `44` · Y `278` · W `670` · H `1` ·
Fill `ThemeLine`.

**Visibilidad de las filas** — igual que el mock, la moneda sin movimiento no se
muestra:

- los CUATRO labels `lblMonNio1`..`lblMonNio4`: Visible
  `Coalesce(gvarLote.DecNIOTotal, 0) > 0`
- los CUATRO labels `lblMonUsd1`..`lblMonUsd4`: Visible
  `Coalesce(gvarLote.DecUSDTotal, 0) > 0`

`lblMontosVacioLote` (Etiqueta clásica): X `44` · Y `290` · W `670` · H `24` ·
Align `Align.Center` · Color `ThemeInk3` · Text
`"Sin montos declarados todavía."` · Visible
`Coalesce(gvarLote.DecNIOTotal, 0) = 0 && Coalesce(gvarLote.DecUSDTotal, 0) = 0`.

> Columnas del backend **B4**. Mientras no exista el backend: Text `"—"`.
> Las planillas solo declaran NIO y USD (no hay EUR a nivel planilla — así es
> el modelo, no es un faltante de la guía).

## 4. Planillas (card izquierda)

| Control | Propiedad | Valor |
|---|---|---|
| `shpPlanillasCard` (Rectángulo clásico) | X/Y/W/H | `24` / `368` / `652` / `376` — receta 7.1 |
| `lblPlanillasTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `382` / `400` / `20` · Text `"PLANILLAS DEL LOTE"` · ESTILO-SECCION |

Encabezados `lblPCol1`..`lblPCol4`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `410`):

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblPCol1` | `"PLANILLA"` | `44` | `120` | Left |
| `lblPCol2` | `"SUCURSAL"` | `164` | `240` | Left |
| `lblPCol3` | `"DECL. C$"` | `404` | `115` | Right |
| `lblPCol4` | `"DECL. US$"` | `519` | `115` | Right |

`shpPlanillasLinea` (Rectángulo clásico): X `44` · Y `430` · W `590` · H `1` ·
Fill `ThemeLine`.

### galPlanillas (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `432` / `590` / `296` |
| Items | `Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLote.'CH-Tbl-ReceptionBatch')` |
| TemplateSize | `42` |

> El patrón `Lookup.'PK' = registro.'PK'` es EL patrón delegable del proyecto.
> Si IntelliSense nombra distinto la PK (`'CH-Tbl-ReceptionBatch'`), aceptá el
> suyo. Cero banda azul o algo está mal.

Template (labels Y `10`, H `22`, X/W de la tabla de encabezados menos `44`;
`lblPCol1`→`lblPFilaId`, `lblPCol2`→`lblPFilaSucursal`, `lblPCol3`→`lblPFilaNio`,
`lblPCol4`→`lblPFilaUsd`):

| Control | Propiedad | Valor |
|---|---|---|
| `shpPlanillaSel` (Rectángulo clásico, PRIMERO) | X/Y/W/H | `0`/`0`/`590`/`42` |
| | Fill | `If(!IsBlank(gvarPlanilla) && ThisItem.'CH-Tbl-Receptions' = gvarPlanilla.'CH-Tbl-Receptions', ThemeAccentSoft, Transparent)` |
| | BorderThickness | `0` |
| `shpPlanillaSelBarra` (Rectángulo clásico) | X/Y/W/H | `0`/`0`/`3`/`42` · Fill `ThemeAccent` |
| | Visible | `!IsBlank(gvarPlanilla) && ThisItem.'CH-Tbl-Receptions' = gvarPlanilla.'CH-Tbl-Receptions'` |
| `btnPlanillaFila` (Botón clásico, overlay receta 7.3) | X/Y/W/H | `0`/`0`/`590`/`42` |
| | OnSelect | `Set(gvarPlanilla, ThisItem)` |
| | AccessibleLabel | `"Ver tulas de la planilla " & ThisItem.ReceiptID` |
| `lblPFilaId` (Etiqueta clásica) | Text / FontWeight | `ThisItem.ReceiptID` / Semibold |
| `lblPFilaSucursal` (Etiqueta clásica) | Text | `ThisItem.RemitentName` |
| `lblPFilaNio` (Etiqueta clásica) | Text / Align | `With({_t: Coalesce(ThisItem.NIOCash, 0) + Coalesce(ThisItem.NIOCheck, 0)}, If(_t = 0, "—", Text(_t, FmtMoney)))` / Right |
| `lblPFilaUsd` (Etiqueta clásica) | Text / Align | `With({_t: Coalesce(ThisItem.USDCash, 0) + Coalesce(ThisItem.USDCheck, 0)}, If(_t = 0, "—", Text(_t, FmtMoney)))` / Right |
| `shpPFilaSep` (Rectángulo clásico) | X/Y/W/H | `0`/`41`/`590`/`1` · Fill `ThemeLineSoft` |

> ⚠️ Casing real del esquema: **`NIOCash`, `NIOCheck`, `USDCash`, `USDCheck`**
> (moneda en mayúsculas). Power Fx es case-sensitive con display names.

> **Divergencia declarada vs receta 7.4**: la selección de planilla se maneja por
> `gvarPlanilla`, NO por `ThisItem.IsSelected`, porque puede llegar preseteada
> desde la búsqueda por planilla (guía 02 §3.1) — `IsSelected` solo conoce clics
> del usuario dentro de la galería.

## 5. Tulas (card derecha)

| Control | Propiedad | Valor |
|---|---|---|
| `shpTulasCard` (Rectángulo clásico) | X/Y/W/H | `690` / `368` / `652` / `376` — receta 7.1 |
| `lblTulasTitulo` (Etiqueta clásica) | X/Y/W/H | `710` / `382` / `560` / `20` · ESTILO-SECCION |
| | Text | `If(IsBlank(gvarPlanilla), "TULAS DE LA PLANILLA", Upper($"TULAS DE {gvarPlanilla.ReceiptID} · {gvarPlanilla.RemitentName}"))` |
| `lblTulasVacio` (Etiqueta clásica) | X/Y/W/H | `710` / `520` / `560` / `40` · Text `"Seleccioná una planilla para ver sus tulas."` · Color `ThemeInk3` · Align Center |
| | Visible | `IsBlank(gvarPlanilla)` |

Encabezados `lblTCol1`..`lblTCol3`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `410`): `lblTCol1` `"NO. TULA"` X `710` W `140`
· `lblTCol2` `"ESTADO"` X `850` W `260` · `lblTCol3` `"DIFERENCIA"` X `1110` W `160`.
`shpTulasLinea` (Rectángulo clásico): X `710` · Y `430` · W `600` · H `1` ·
Fill `ThemeLine`.

### galTulas (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `710` / `432` / `600` / `296` |
| Visible | `!IsBlank(gvarPlanilla)` |
| Items | `Filter('CH-Tbl-Bags', ReceptionID.'CH-Tbl-Receptions' = gvarPlanilla.'CH-Tbl-Receptions')` |
| TemplateSize | `42` |

Template:

| Control | Propiedad | Valor |
|---|---|---|
| `btnTulaFila` (Botón clásico, overlay receta 7.3) | X/Y/W/H | `0`/`0`/`600`/`42` |
| | OnSelect | `Set(gvarTula, ThisItem); Navigate(scrTula)` |
| | AccessibleLabel | `"Abrir tula " & ThisItem.BagID` |
| `lblTFilaId` (Etiqueta clásica) | X/Y/W/H | `0`/`10`/`140`/`22` · Text `ThisItem.BagID` · Semibold |
| `pilTFilaEstado` (Botón clásico, receta 7.2) | X/Y/W/H | `140`/`8`/`250`/`26` |
| | Text | `ThisItem.BagStatus` |
| | Fill | `Switch(ThisItem.BagStatus, 'BagStatus (CH-Tbl-Bags)'.Recontada, ThemeOkSoft, 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero', ThemeWarnSoft, 'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa', ThemeInfoSoft, 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda', ThemeInfoSoft, ThemeCritSoft)` *(los dos "Pendiente de revision…" caen al default crítico)* |
| | Color | mismo Switch con `ThemeOk / ThemeWarn / ThemeInfo / ThemeInfo / ThemeCrit` |
| `pilTFilaDif` (Botón clásico, receta 7.2) | X/Y/W/H | `400`/`8`/`150`/`26` |
| | Text | `If(ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.Recontada, "Pendiente", If(ThisItem.HasDifference, "Con diferencia", "Sin diferencia"))` |
| | Fill | `If(ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.Recontada, ThemeSurface2, If(ThisItem.HasDifference, ThemeCritSoft, ThemeOkSoft))` |
| | Color | `If(ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.Recontada, ThemeInk2, If(ThisItem.HasDifference, ThemeCrit, ThemeOk))` |
| `shpTFilaSep` (Rectángulo clásico) | X/Y/W/H | `0`/`41`/`600`/`1` · Fill `ThemeLineSoft` |

> `HasDifference` es la columna espejo del backend **B5**. Sin backend: Text
> `"Pendiente"` fijo y seguís.
>
> **Nota**: `btnTulaFila` NO setea `gvarPlanilla` ni `gvarLote` — ya están
> seteados en esta pantalla, y `gvarTula` navega con su contexto completo.

> **Divergencia declarada vs prototipo**: el prototipo auto-seleccionaba la
> primera planilla al entrar al lote. Acá NO: entrar por lote deja el panel de
> tulas con su mensaje hasta que hagas clic (evita una query extra y un estado
> ambiguo). Si en el uso real molesta, se agrega en OnVisible.

## 6. Checklist de salida

- [ ] Entrar por búsqueda de lote: planillas cargadas, tulas vacías con mensaje.
- [ ] Entrar por búsqueda de planilla: esa planilla resaltada y sus tulas cargadas.
- [ ] Clic en planilla resalta la fila y refresca tulas.
- [ ] Clic en tula navega a scrTula.
- [ ] Cero banda azul de delegación en las dos galerías.
