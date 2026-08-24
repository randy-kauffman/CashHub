# Guía 02 — scrCorte (layout manual v2)

Consola de **corte**: elegís un cajero en el rail izquierdo (componente) y a la
derecha ves sus KPIs y la lista de transacciones de efectivo pendientes de cortar;
**Realizar corte** abre un diálogo de confirmación con el **conteo real
(server-side)** y genera el lote. Fuente de diseño: mockup **CO1** (rail + grid) y
**CO2** (diálogo).

> **Paradigma v2 (01 §7.0).** CERO auto-layout, CERO `FillPortions`. La pantalla se
> arma con **Contenedores manuales** anidados y **componentes** (01 §8); cada control
> se posiciona con `X`/`Y`/`Width`/`Height` como **fórmula relativa** a
> `Parent.Width`/`Parent.Height` y las constantes `Lay*` (01 §3). Los bloques
> repetidos — encabezado, rail, KPI, diálogo, estado vacío — son **instancias de
> componente**: esta guía **no** reconstruye su interior, solo fija posición y
> propiedades de entrada/salida.

**Contrato de entrada**: `gvarCajeroCor` = fila del cajero elegido en el rail, shape
del workload `{cajero, cajeroNombre, mesa, pendientesCorte, totalNio, totalUsd,
totalEur}`, donde **`cajero` es un GUID** (id de `systemuser`, ya casteado en
`colCajerosCor`) y `cajeroNombre` es el nombre plano. **Puede entrar `Blank()`**
(primera visita o tras un corte): el panel derecho muestra su estado vacío
(`cmpEmptyCor`) hasta que el usuario elige un cajero. `gvarCajeroCor` **no** se
auto-setea al entrar (la selección del rail es por `Set`, no por `IsSelected` — por
eso el estado vacío es legítimo).

## Árbol de controles

```
scrCorte                                   (Fill: ThemeBg · esqueleto receta 7.0)
├── cmpHeaderCor                           Componente cmpHeader (§8.1)
├── cntBodyCor                             Contenedor (manual)
│   ├── cmpRailCor                         Componente cmpRailCajeros (§8.2)
│   └── cntMainCor                         Contenedor (manual)
│       ├── cntToolbarCor                  Contenedor (manual)
│       │   ├── lblMetaCor                 Etiqueta clásica
│       │   └── btnCorteCor                Botón clásico (ESTILO-BTN-PRIMARIO)
│       ├── cmpKpiPendCor                  Componente cmpKpi (§8.4)
│       ├── cmpKpiNioCor                   Componente cmpKpi (§8.4)
│       ├── cmpKpiUsdCor                   Componente cmpKpi (§8.4)
│       ├── cmpKpiEurCor                   Componente cmpKpi (§8.4)
│       ├── cntGridCardCor                 Contenedor (manual) · card receta 7.1
│       │   ├── lblColRefCor               Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── lblColDepCor               Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── lblColCtaCor               Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── lblColMontoCor             Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── lblColMonCor               Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── lblColCreadaCor            Etiqueta clásica (ESTILO-HEADER-COL)
│       │   ├── shpGridLineaCor            Rectángulo clásico (línea 1 px)
│       │   ├── galTxCor                   Galería vertical en blanco · receta 7.2
│       │   │   ├── lblTxRefCor            Etiqueta clásica
│       │   │   ├── lblTxDepCor            Etiqueta clásica
│       │   │   ├── lblTxCtaCor            Etiqueta clásica
│       │   │   ├── lblTxMontoCor          Etiqueta clásica
│       │   │   ├── lblTxMonCor            Etiqueta clásica
│       │   │   ├── lblTxCreadaCor         Etiqueta clásica
│       │   │   └── shpTxSepCor            Rectángulo clásico (separador de fila)
│       │   ├── shpGridFootLinCor          Rectángulo clásico (línea superior del pie)
│       │   └── cntGridFootCor             Contenedor (manual) · fila total
│       │       ├── lblFootCntCor          Etiqueta clásica
│       │       ├── lblFootNioCor          Etiqueta clásica
│       │       └── lblFootUsdCor          Etiqueta clásica
│       └── cmpEmptyCor                    Componente cmpEmpty (§8.6)
└── cmpDlgCorteCor                         Componente cmpDialogo (§8.5 · overlay)
```

> El overlay `cmpDlgCorteCor` es **hijo directo de `scrCorte`** (fuera de
> `cntBodyCor`), a pantalla completa. Su visibilidad la gobierna el prop `Mostrar`
> (`ctxDlgCorteCor`); cuando es falso el componente no pinta nada.

## 1. Pantalla y esqueleto (receta 7.0)

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `scrCorte` | Fill | `ThemeBg` |
| | OnVisible | *(bloque completo abajo)* |
| `cntBodyCor` (Contenedor manual) | X / Y | `0` / `LayHeaderH` |
| | Width / Height | `Parent.Width` / `Parent.Height - LayHeaderH` |
| | Fill | `Color.Transparent` |

`scrCorte.OnVisible` (listo para pegar — carga el workload y normaliza estado):

```powerfx
UpdateContext({ctxDlgCorteCor: false, ctxCorriendoCor: false});
ClearCollect(
    colCajerosCor,
    ForAll(
        Table(ParseJSON(Environment.sanic_CHWorkloadByCashier().Workload)),
        {
            cajero:          GUID(Text(ThisRecord.Value.cajero)),
            cajeroNombre:    Text(ThisRecord.Value.cajeroNombre),
            mesa:            Text(ThisRecord.Value.mesa),
            pendientesCorte: Value(ThisRecord.Value.pendientesCorte),
            totalNio:        Value(ThisRecord.Value.totalNio),
            totalUsd:        Value(ThisRecord.Value.totalUsd),
            totalEur:        Value(ThisRecord.Value.totalEur)
        }
    )
)
```

> `OnVisible` NO setea `gvarCajeroCor` (respeta el contrato de entrada: puede quedar
> `Blank`). Los `Text()`/`Value()`/`GUID()` son **obligatorios**: `ParseJSON` entrega
> *untyped object* y sin casteo la asignación del record no compila (por eso la forma
> funcional canónica lleva los casteos explícitos, no el atajo `{cajeroNombre}`). El
> `Set(gvarCajeroCor, Blank())` post-corte y el `Reset` de selección viven en el
> propio componente rail (§3) y en `OnOk` del diálogo (§8).

## 2. Encabezado — instancia de `cmpHeader` (§8.1)

`cmpHeaderCor` es una **instancia** de `cmpHeader`; su interior (marca, título,
segmentado de navegación, chip de usuario) lo fija el componente. Acá solo posición e
inputs:

| Propiedad | Valor (fórmula) |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `Parent.Width` / `LayHeaderH` |
| `Titulo` | `"Digitación"` |
| `Subtitulo` | `"Corte de transacciones"` |
| `Activo` | `"Corte"` |
| `OnCorte` | `Navigate(scrCorte)` |
| `OnManual` | `Navigate(scrManual)` |
| `OnLotes` | `Navigate(scrLotes)` |
| `OnCheques` | `Navigate(scrCheques)` |

> El componente no puede `Navigate` a pantallas externas: navega por **behavior
> props**. `Activo="Corte"` resuelve el estado activo del segmentado; en scrCorte solo
> el botón "Corte" queda resaltado.

## 3. Rail de cajeros — instancia de `cmpRailCajeros` (§8.2)

`cmpRailCor` es una **instancia** de `cmpRailCajeros`; su interior (título, botones de
orden "Más pendientes / A–Z", galería con selección + badge tabular, receta 7.3) lo
fija el componente. Vive dentro de `cntBodyCor`, así que su `Parent` es el cuerpo:

| Propiedad | Valor (fórmula) |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `LayRailW` / `Parent.Height` |
| `Titulo` | `"CAJEROS CON CORTE PENDIENTE"` |
| `Datos` | `AddColumns(Filter(colCajerosCor, pendientesCorte > 0), "badge", pendientesCorte)` |
| `OnSeleccion` | `Set(gvarCajeroCor, cmpRailCor.CajeroSel)` |

> **Datos → badge**: el rail es genérico; el conteo del badge entra como columna
> `badge` de `Datos`. `Filter(colCajerosCor, pendientesCorte > 0)` deja solo cajeros
> con corte pendiente (equivalente al `Filter` del mockup CO1). El **orden** (cantidad
> / A–Z) lo resuelve el componente sobre su `Datos` — no es responsabilidad de la
> pantalla.
>
> **Selección**: al tocar una fila el componente publica `cmpRailCor.CajeroSel`
> (Output = fila seleccionada) y dispara `OnSeleccion`, donde la pantalla hace
> `Set(gvarCajeroCor, cmpRailCor.CajeroSel)`. No hay auto-selección → el estado vacío
> del panel derecho (§7) es válido.
>
> **Dep. backend `sanic_CHWorkloadByCashier`** (B-workload) — su `.Workload` es el
> JSON que `OnVisible` parsea a `colCajerosCor`. **Fallback sin backend**: poblar
> `colCajerosCor` desde `Users` con `pendientesCorte` vía
> `CountRows(Filter('CH-Tbl-Transactions', TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Batch && IsBlank(BatchStatus) && IsBlank(TransactionStatus) && 'Created By'.'Primary Key' = <guid>))`
> (correcto < 500 tx/cajero) y `totalNio/Usd/Eur` en `"—"` (sin `Sum` delegable).

## 4. Panel principal (`cntMainCor`)

Contenedor manual a la derecha del rail. Su `Parent` es `cntBodyCor`. Todo lo de
adentro (toolbar, KPIs, grid, vacío) se posiciona **relativo a `cntMainCor`**.

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `cntMainCor` (Contenedor manual) | X / Y | `LayRailW` / `0` |
| | Width / Height | `Parent.Width - LayRailW` / `Parent.Height` |
| | Fill | `Color.Transparent` |

### 4.1 Toolbar (`cntToolbarCor`)

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `cntToolbarCor` (Contenedor manual) | X / Y | `LayGutter` / `LayGutter` |
| | Width / Height | `Parent.Width - 2*LayGutter` / `40` |
| | Fill / Visible | `Color.Transparent` / `!IsBlank(gvarCajeroCor)` |
| `lblMetaCor` (Etiqueta clásica) | X / Y | `0` / `0` |
| | Width / Height | `Parent.Width - 160` / `Parent.Height` |
| | Text | `$"{gvarCajeroCor.cajeroNombre} · {gvarCajeroCor.mesa} — {gvarCajeroCor.pendientesCorte} transacciones de efectivo pendientes"` |
| | Color / Size / VerticalAlign | `ThemeInk2` / `9` / `VerticalAlign.Middle` |
| `btnCorteCor` (Botón clásico) | X / Y | `Parent.Width - Self.Width` / `(Parent.Height - Self.Height)/2` |
| | Width / Height | `150` / `32` · **ESTILO-BTN-PRIMARIO** |
| | Text | `"Realizar corte"` |
| | DisplayMode | `If(IsBlank(gvarCajeroCor), DisplayMode.Disabled, DisplayMode.Edit)` |
| | OnSelect | `UpdateContext({ctxCortePreviewCor: Environment.sanic_CHPreviewTransactionCut({CajeroId: gvarCajeroCor.cajero}), ctxCorriendoCor: false, ctxDlgCorteCor: true})` |

> **Preview antes de abrir**: `btnCorteCor.OnSelect` invoca el Custom API directo
> `sanic_CHPreviewTransactionCut({CajeroId: gvarCajeroCor.cajero})` (el `cajero` es
> GUID) y guarda `{EligibleCount, TotalNIO, TotalUSD}` en `ctxCortePreviewCor`; recién
> entonces abre el overlay. Normaliza `ctxCorriendoCor` a `false` (sin residuo del
> corte anterior).
>
> **Dep. backend `sanic_CHPreviewTransactionCut`** (B-preview) — mismo filtro que el
> plugin de corte. **Fallback**: `ctxCortePreviewCor` = `{EligibleCount:
> CountRows(Filter('CH-Tbl-Transactions', TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Batch && IsBlank(BatchStatus) && IsBlank(TransactionStatus) && 'Created By'.'Primary Key' = gvarCajeroCor.cajero)), TotalNIO: gvarCajeroCor.totalNio, TotalUSD: gvarCajeroCor.totalUsd}`.

### 4.2 KPIs — 4 instancias de `cmpKpi` (§8.4)

Fila de 4 tarjetas de igual ancho, con `LayGap` entre ellas, a lo ancho de
`cntMainCor` (menos `LayGutter` a cada lado). Cada una es una **instancia** de
`cmpKpi`; su interior (label + valor) lo fija el componente. `Parent` = `cntMainCor`.
Constante de fila: **`Y`** común `= cntToolbarCor.Y + cntToolbarCor.Height + LayGap`,
**`Height`** común `= LayKpiH`, **`Width`** común `= (Parent.Width - 2*LayGutter - 3*LayGap)/4`.

| Instancia | X (fórmula) | `Etiqueta` | `Valor` |
|---|---|---|---|
| `cmpKpiPendCor` | `LayGutter` | `"PENDIENTES"` | `Text(gvarCajeroCor.pendientesCorte)` |
| `cmpKpiNioCor` | `cmpKpiPendCor.X + cmpKpiPendCor.Width + LayGap` | `"TOTAL C$"` | `SimboloNIO & " " & Text(gvarCajeroCor.totalNio, FmtMoney)` |
| `cmpKpiUsdCor` | `cmpKpiNioCor.X + cmpKpiNioCor.Width + LayGap` | `"TOTAL US$"` | `SimboloUSD & " " & Text(gvarCajeroCor.totalUsd, FmtMoney)` |
| `cmpKpiEurCor` | `cmpKpiUsdCor.X + cmpKpiUsdCor.Width + LayGap` | `"TOTAL €"` | `SimboloEUR & " " & Text(gvarCajeroCor.totalEur, FmtMoney)` |

Las cuatro: `Y` común, `Width` común, `Height = LayKpiH`, `Visible = !IsBlank(gvarCajeroCor)`.

> Totales/conteos vienen del **agregado server-side** (`gvarCajeroCor.total…`,
> B-workload). **Fallback**: `pendientesCorte` por `CountRows` (§3); los totales por
> moneda **no** tienen fallback delegable (`Sum` no delega) → mostrar `"—"`.

## 5. Grilla de transacciones (card receta 7.1 + grilla receta 7.2)

`cntGridCardCor` es la **card** (superficie blanca con radio, receta 7.1). `Parent` =
`cntMainCor`. Padding interno de card = **14** (mockup `.gRow` → `padding … 14px`;
constante local `PadCard`, usada consistentemente abajo).

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `cntGridCardCor` (Contenedor manual) | X | `LayGutter` |
| | Y | `cmpKpiPendCor.Y + cmpKpiPendCor.Height + LayGap` |
| | Width | `Parent.Width - 2*LayGutter` |
| | Height | `Parent.Height - Self.Y - LayGutter` |
| | Fill / BorderColor / BorderThickness | `ThemeSurface` / `ThemeLine` / `1` |
| | RadiusTop/Bottom·Left/Right | `LayCardR` (los 4) |
| | Visible | `!IsBlank(gvarCajeroCor)` |

### 5.1 Mapa de columnas (fracciones del mockup `1.1 2 1 .9 .6 .8`, gap 10)

Todas las columnas (encabezado, celdas del template, pie) comparten el **mismo
reparto**. Con `U` = unidad de fracción y gap de **10 px** entre columnas (5 gaps):

- **Encabezado y pie** (Parent = card / pie, ancho de contenido = ancho de `galTxCor`):
  `U = (galTxCor.Width - 50) / 6.4`
- **Celdas del template** (Parent = galería):
  `U = (Parent.TemplateWidth - 50) / 6.4`

| Col | `Off` (offset desde inicio de fila) | `Ancho` | Align |
|---|---|---|---|
| Referencia | `0` | `1.1*U` | Left |
| Depósito / cliente | `1.1*U + 10` | `2*U` | Left |
| Cuenta | `3.1*U + 20` | `1*U` | Left |
| Monto | `4.1*U + 30` | `0.9*U` | Right |
| Mon. | `5*U + 40` | `0.6*U` | Right |
| Creada | `5.6*U + 50` | `0.8*U` | Right |

Regla de aplicación (receta 7.2 — *X template = X encabezado − X galería*):
- **Encabezado**: `X = PadCard + Off`, `Width = Ancho` (PadCard = 14; `Off`/`Ancho` con la `U` del ancho de card).
- **Celda del template**: `X = Off`, `Width = Ancho` (con la `U` de `Parent.TemplateWidth`).
- **Pie**: `X = Off`, `Width = Ancho` (el pie ya arranca en PadCard, ver §5.4).

Usá `With` para no repetir la fracción, p. ej. columna Cuenta del encabezado:
`X = 14 + With({U:(galTxCor.Width-50)/6.4}, 3.1*U + 20)`.

### 5.2 Encabezado de columnas + línea

Seis etiquetas **ESTILO-HEADER-COL**, `Parent` = card, `Y = 12`, `Height = 22`, `X` y
`Width` por §5.1 (encabezado). Las tres numéricas con `Align.Right`.

| Control | Columna (§5.1) | Text |
|---|---|---|
| `lblColRefCor` | Referencia | `"REFERENCIA"` |
| `lblColDepCor` | Depósito / cliente | `"DEPÓSITO / CLIENTE"` |
| `lblColCtaCor` | Cuenta | `"CUENTA"` |
| `lblColMontoCor` | Monto | `"MONTO"` (Align Right) |
| `lblColMonCor` | Mon. | `"MON."` (Align Right) |
| `lblColCreadaCor` | Creada | `"CREADA"` (Align Right) |

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `shpGridLineaCor` (Rectángulo) | X / Y | `PadCard` / `lblColRefCor.Y + lblColRefCor.Height` |
| | Width / Height | `Parent.Width - 2*PadCard` / `1` |
| | Fill | `ThemeLine` |

### 5.3 `galTxCor` (Galería vertical en blanco, receta 7.2)

| Propiedad | Valor (fórmula) |
|---|---|
| X / Y | `PadCard` / `shpGridLineaCor.Y + 6` |
| Width | `Parent.Width - 2*PadCard` |
| Height | `cntGridFootCor.Y - Self.Y` |
| TemplateSize / TemplatePadding | `LayRowH` / `0` |
| ShowScrollbar | `true` |
| Items | *(bloque abajo)* |

```powerfx
Sort(
    Filter(
        'CH-Tbl-Transactions',
        TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Batch
            && IsBlank(BatchStatus)
            && IsBlank(TransactionStatus)
            && 'Created By'.'Primary Key' = gvarCajeroCor.cajero
    ),
    'Created On',
    SortOrder.Ascending
)
```

> **Filtro delegable**: igualdad de GUID por clave primaria del lookup
> (`'Created By'.'Primary Key' = gvarCajeroCor.cajero` → `_ownerid_value eq <guid>`),
> choice por miembro (`.Batch`), `IsBlank` sobre INT espejo (`BatchStatus`) y sobre
> choice (`TransactionStatus`). Cero `Upper`/`Sum`, cero comparación de records. `Sort`
> por `'Created On'` delega. `gvarCajeroCor.cajero` **ya es GUID** (casteado en §1).

Template — 6 celdas + separador. `Parent` = galería; `X`/`Width` por §5.1 (template),
`Y = 0`, `Height = LayRowH`, `VerticalAlign.Middle`:

| Control | Columna | Text / estilo |
|---|---|---|
| `lblTxRefCor` | Referencia | `ThisItem.Name` · FontWeight Semibold · ESTILO-CELDA |
| `lblTxDepCor` | Depósito / cliente | `Coalesce(ThisItem.RelatedDepositRecounting.Name, "—")` · ESTILO-CELDA |
| `lblTxCtaCor` | Cuenta | `Coalesce(Text(ThisItem.AccountNumber, "[$-en-US]0"), "—")` · ESTILO-CELDA-NUM |
| `lblTxMontoCor` | Monto | `Text(ThisItem.CashAmount, FmtMoney)` · FontWeight Semibold · ESTILO-CELDA-NUM |
| `lblTxMonCor` | Mon. | `Switch(ThisItem.Currency, 'Currency (CH-Tbl-Transactions)'.NIO, SimboloNIO, 'Currency (CH-Tbl-Transactions)'.USD, SimboloUSD, 'Currency (CH-Tbl-Transactions)'.EUR, SimboloEUR, "—")` · ESTILO-CELDA-NUM |
| `lblTxCreadaCor` | Creada | `Text(ThisItem.'Created On', FmtHora)` · ESTILO-CELDA-NUM |

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `shpTxSepCor` (Rectángulo) | X / Y | `0` / `LayRowH - 1` |
| | Width / Height | `Parent.TemplateWidth` / `1` |
| | Fill | `ThemeLineSoft` |

> **Divergencia**: en CO1 las filas son `div` (no `button`) → el grid de corte **no es
> navegable ni seleccionable**. El template **no** lleva overlay `btn…Fila`,
> `shp…Sel` ni barra de selección (receta 7.3 se aplica solo al rail). `shpTxSepCor`
> se posiciona por regla (`Y = LayRowH − 1`).

### 5.4 Fila total (pie)

Línea superior + contenedor de pie anclado al fondo de la card. `Parent` = card.

| Control | Propiedad | Valor (fórmula) |
|---|---|---|
| `shpGridFootLinCor` (Rectángulo) | X / Y | `PadCard` / `cntGridFootCor.Y` |
| | Width / Height | `Parent.Width - 2*PadCard` / `1` · Fill `ThemeLine` |
| `cntGridFootCor` (Contenedor manual) | X / Y | `PadCard` / `Parent.Height - PadCard - 36` |
| | Width / Height | `Parent.Width - 2*PadCard` / `36` |
| | Fill | `ThemeSurface2` |

Etiquetas del pie — `Parent` = `cntGridFootCor`, `Y = 0`, `Height = Parent.Height`,
`VerticalAlign.Middle`, FontWeight Bold, Size 9. `X`/`Width` por §5.1 (pie):

| Control | Columnas ocupadas | Text |
|---|---|---|
| `lblFootCntCor` | Referencia → fin de Cuenta (`X = 0`, `Width = With({U:(cntGridFootCor.Width-50)/6.4}, 3.1*U + 20 + 1*U)`) | `$"{gvarCajeroCor.pendientesCorte} transacciones"` |
| `lblFootNioCor` | Monto (Align Right) | `SimboloNIO & " " & Text(gvarCajeroCor.totalNio, FmtMoney)` |
| `lblFootUsdCor` | Mon. (Align Right) | `SimboloUSD & " " & Text(gvarCajeroCor.totalUsd, FmtMoney)` |

> El conteo "137 transacciones" y los totales vienen del **agregado server-side**
> (`gvarCajeroCor`), no de `CountRows`/`Sum` del grid. La columna Creada del pie va
> vacía (mockup CO1) → sin control. Fallback idéntico a §4.2.

## 6. Estado vacío — instancia de `cmpEmpty` (§8.6)

`cmpEmptyCor` cubre el panel principal cuando no hay cajero. `Parent` = `cntMainCor`:

| Propiedad | Valor (fórmula) |
|---|---|
| X / Y | `LayGutter` / `LayGutter` |
| Width / Height | `Parent.Width - 2*LayGutter` / `Parent.Height - 2*LayGutter` |
| `Glifo` | `"⛁"` |
| `Mensaje` | `"Elegí un cajero del panel izquierdo para ver sus transacciones pendientes y realizar el corte."` |
| Visible | `IsBlank(gvarCajeroCor)` |

> Conceptual **CO3** (no está en el mockup). Toolbar (§4.1), KPIs (§4.2) y grid (§5)
> llevan `Visible = !IsBlank(gvarCajeroCor)`; este componente el complemento
> `IsBlank(...)`. Como el layout es manual, ambos ocupan el mismo rectángulo y solo
> uno se ve — no dependemos del colapso de auto-layout.

## 7. Diálogo de confirmación — instancia de `cmpDialogo` (§8.5)

`cmpDlgCorteCor` es una **instancia** de `cmpDialogo` (overlay + card + icono + título
+ texto + ✕ + 2 botones — todo lo fija el componente). **Hijo directo de `scrCorte`**,
a pantalla completa. `Parent` = `scrCorte`:

| Propiedad | Valor (fórmula) |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `Parent.Width` / `Parent.Height` |
| `Mostrar` | `ctxDlgCorteCor` |
| `Tono` | `"warn"` |
| `AnchoCard` | `440` |
| `Titulo` | `$"¿Generar el corte de {gvarCajeroCor.cajeroNombre}?"` |
| `Texto` | *(bloque abajo)* |
| `TextoCancelar` | `"Cancelar"` |
| `TextoOk` | `"Sí, generar corte"` |
| `OkHabilitado` | `!ctxCorriendoCor` |
| `OnCancelar` | `UpdateContext({ctxDlgCorteCor: false})` |
| `OnOk` | *(bloque abajo)* |

`Texto` (conteo + totales server-side; el banner de CO2 se **funde** acá — ver
divergencias):

```powerfx
$"Se creará un lote en estado Generado con las {ctxCortePreviewCor.EligibleCount} transacciones de efectivo pendientes del cajero (" &
SimboloNIO & " " & Text(ctxCortePreviewCor.TotalNIO, FmtMoney) & " · " &
SimboloUSD & " " & Text(ctxCortePreviewCor.TotalUSD, FmtMoney) &
"). El lote queda a su nombre. El conteo lo confirma el servidor en el momento del corte: las transacciones creadas después entran en el próximo corte."
```

`OnOk` (ejecuta el corte con anti-doble-click — corrige **C-3**):

```powerfx
UpdateContext({ctxCorriendoCor: true});
'CH-Flow-CreateCut'.Run(gvarCajeroCor.cajero);
UpdateContext({ctxCorriendoCor: false, ctxDlgCorteCor: false});
Set(gvarCajeroCor, Blank());
Notify("Corte generado. El lote quedó en estado Generado.", NotificationType.Success)
```

> 🧨 **C-3**: `OkHabilitado = !ctxCorriendoCor` deshabilita "Sí, generar corte"
> mientras corre; al terminar cierra el overlay (`ctxDlgCorteCor: false`) y limpia
> `gvarCajeroCor` (el cajero ya no tiene corte pendiente → vuelve el estado vacío). El
> ✕ y "Cancelar" comparten `OnCancelar` (cierran sin cortar). La acción destructiva
> vive **solo** en `OnOk`.
>
> **Conteo = server-side** (`ctxCortePreviewCor.EligibleCount`, seteado en §4.1): no
> `CountRows` con dot-walk (bug D-2). `'CH-Flow-CreateCut'.Run(gvarCajeroCor.cajero)`
> recibe el **GUID** directo.
>
> **Dep. backend** `sanic_CHCreateTransactionCut` vía `'CH-Flow-CreateCut'`. Cierres
> pendientes (00 §V-1/V-2): `executeprivilegename` + autorización
> `CajeroId == InitiatingUserId` en el plugin y **conexión de usuario** (no SP) en el
> slot Dataverse del flow. No son de la app, pero el corte no debe desplegarse sin ellos.

## 8. Divergencias declaradas vs mockup

- **Referencia de la tx**: el mockup muestra `TRX-0098231`; el esquema real usa `Name`
  autonumerado con prefijo **`T-…`**. Se usa `ThisItem.Name` (dato real).
- **Columna "Depósito / cliente"**: el mockup combina `DR-4460 · Distribuidora La
  Colonia`. El esquema solo expone la referencia del depósito recontado
  (`RelatedDepositRecounting.Name`) a un dot-walk; el nombre de cliente exige
  navegación profunda no fiable en galería. Se muestra la referencia; el nombre de
  cliente queda como **dep. backend** (espejo `sanic_clientname` o expansión en
  B-workload). Fallback: solo la referencia.
- **Cuenta (`AccountNumber`)**: columna **`IsSecured`**; sin el perfil de seguridad de
  campo devuelve `Blank` → la celda muestra `"—"` (aproximación honesta, no un `0`).
- **Grid no interactivo**: el corte no navega ni selecciona fila (mockup usa `div`);
  se omite el overlay clicable de receta 7.3 en el template (§5.3).
- **Banner informativo (CO2)**: `cmpDialogo` (§8.5) es *solo texto + botones* — no
  tiene slot de banner. La frase del banner azul se **funde en `Texto`** (§7). Se
  pierde el fondo `ThemeInfoSoft` del prototipo; el contenido se preserva.
- **Estado vacío (CO3)**: agregado a conciencia (no está en el mockup) porque el
  contrato admite `gvarCajeroCor` Blank.
- **`cajero` como GUID**: el shape v2 del workload guarda `cajero` como GUID casteado
  (`GUID(Text(...))`), no como referencia `systemuser`. Por eso el grid compara con
  `'Created By'.'Primary Key'` y el preview/flow reciben `gvarCajeroCor.cajero` directo
  — sin `.User` ni `.'Full Name'` (el nombre lo da `cajeroNombre`).
- **Constantes locales**: `PadCard = 14` (padding de card, mockup `.gRow`), gap de
  columnas `= 10` (mockup `.coRow gap`) y alto de pie `= 36`. Se usan encadenadas por
  fórmula (nunca dos literales que se contradigan); no ameritan constante `Lay*` global
  porque son propias de esta grilla.

## 9. Checklist de salida

- [ ] Entrar sin cajero (`gvarCajeroCor` Blank): solo `cmpEmptyCor`; toolbar, KPIs y
      grid ocultos; `btnCorteCor` deshabilitado.
- [ ] El rail lista **solo** cajeros con `pendientesCorte > 0`, con badge de conteo, y
      ordena por "Más pendientes" / "A–Z" (internamente en `cmpRailCor`).
- [ ] Clic en un cajero: `OnSeleccion` setea `gvarCajeroCor`; la fila se resalta, la
      toolbar muestra su meta, los 4 KPIs y el grid se llenan.
- [ ] Grid **sin banda azul de delegación** (filtro por `'Primary Key'`, §5.3).
- [ ] Columnas del grid alineadas con su encabezado (mismo mapa §5.1); separador a
      `Y = LayRowH − 1`; montos con `FmtMoney`; "Creada" con `FmtHora` (`HH:mm`).
- [ ] "Realizar corte" abre `cmpDlgCorteCor` con **conteo y totales server-side**
      (preview), no el `CountRows` del grid.
- [ ] ✕ y "Cancelar" cierran sin cortar; el corte se dispara **solo** desde "Sí,
      generar corte".
- [ ] Durante el corte, "Sí, generar corte" queda deshabilitado (`OkHabilitado`); al
      terminar, el overlay cierra y `gvarCajeroCor` se limpia (C-3 resuelto).
- [ ] Layout **sin `FillPortions` ni contenedores de auto-ajuste**: todo `X/Y/Width/Height`
      es fórmula de `Parent.*` + `Lay*` (paradigma 7.0).
- [ ] App Checker en cero errores.
- [ ] Dependencias de backend marcadas: B-workload (rail/KPIs vía `.Workload`),
      B-preview (`sanic_CHPreviewTransactionCut`), `sanic_CHCreateTransactionCut`
      (+ cierres V-1/V-2), nombre de cliente del grid.
```
