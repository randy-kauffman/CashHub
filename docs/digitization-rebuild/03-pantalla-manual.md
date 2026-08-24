# Guía 03 — scrManual (v2 — layout manual + componentes)

Aplicación **manual** de las transacciones que no van por corte automático (las que
traen cheques o no tienen número de cuenta). Rail de cajeros con manuales · segmentado
**Pendientes / Aplicadas** · grilla ordenable por Motivo · panel de detalle
**keyboard-first** para grabación rápida contra el Core. Fuente de verdad del diseño:
`mockup-digitization.html` secciones **MA1** (flujo teclado), **MA2** (Aplicadas +
motivo del rechazo), **MA3** (rechazo del guard). Recetas, estilos y componentes: `01-app.md`.

> **Paradigma (01 §7.0).** Esta pantalla NO usa auto-ajuste (`FillPortions`, contenedores
> verticales/horizontales). Usa **contenedores MANUALES** (`groupContainer.manualLayoutContainer`)
> y **posición/tamaño por fórmula relativa** a `Parent.Width`/`Parent.Height` + constantes
> `Lay*`. Lo repetido es **COMPONENTE**: encabezado `cmpHeader` (§8.1), rail `cmpRailCajeros`
> (§8.2), píldora `cmpPildora` (§8.3), estado vacío `cmpEmpty` (§8.6). El **panel copiable
> keyboard-first va DIRECTO en la pantalla** (receta 8.7): el `SetFocus`/`TabIndex` cruzan
> mal el borde de un componente. Las fórmulas FUNCIONALES (Items, Patch, cheques, teclado)
> son las de la v1 — se preservan tal cual; sólo cambia el LAYOUT.

**Contrato de entrada** — esta pantalla se autoabastece; los otros `scr*` solo hacen
`Navigate(scrManual)`. Su `OnVisible` (§1) siembra todo el estado local:

| Variable | Tipo | Qué contiene | Quién la setea |
|---|---|---|---|
| `gvarCajeroMan` | fila de `colCajerosMan` (`{cajero, nombre, mesa, pendientesManual, badge}`) | Cajero elegido en el rail | `OnVisible` (primero de la lista) y `cmpRailMan.OnSeleccion` |
| `gvarTxMan` | registro de `CH-Tbl-Transactions` | Transacción seleccionada / en foco | `OnVisible`, `galTxMan`, `cmpRailMan.OnSeleccion`, segmentado y **auto-avance** del `Patch` |
| `ctxSegManual` | texto `"Pendientes"` \| `"Aplicadas"` | Segmento activo de la lista | `OnVisible` + `btnSegPendientesMan` / `btnSegAplicadasMan` |
| `ctxOrdenMan` | texto `"Fecha"` \| `"Motivo"` | Orden de la **grilla** | `OnVisible` + `btnHdrMotivoMan` |
| `ctxErrorMan` | texto | Mensaje del guard cuando el `Patch` rechaza (MA3) | `btnAplicarMan` (IfError) y limpiadores |
| `colCajerosMan` | colección | Agregado por cajero (rail, §3). Alimenta `cmpRailMan.Datos` | `OnVisible` |

> **El orden del rail (Más pendientes / A–Z) ya NO vive en la pantalla**: es estado
> interno de `cmpRailCajeros` (§8.2). Por eso desaparece `ctxOrdenRailMan` de la v1 (L-D11).

> 🧨 **Una transacción NUNCA está en Manual y en un lote a la vez** (o tiene
> `BatchStatus`, o no). Por eso todos los filtros de esta pantalla exigen
> `IsBlank(BatchStatus)`. El "rechazo del guard" de MA3 **no** es "está en un lote":
> es un choque real de concurrencia (ya la aplicó otra sesión). Ver §8.

---

## Árbol de controles

```
scrManual                                        (Fill: ThemeBg)
├── cmpHeaderMan                                 Componente (cmpHeader §8.1) · Activo "Manual"
└── cntBodyMan                                   Contenedor (manual)          ← último hijo de la pantalla
    ├── cmpRailMan                               Componente (cmpRailCajeros §8.2)
    └── cntMainMan                               Contenedor (manual)
        ├── cntListaCardMan                      Contenedor (manual · card 7.1)
        │   ├── cntSegMan                        Contenedor (manual · segmentado)
        │   │   ├── btnSegPendientesMan          Botón clásico
        │   │   └── btnSegAplicadasMan           Botón clásico
        │   ├── lblOrdenMan                      Etiqueta clásica · "Orden: …"
        │   ├── shpGridHeadBgMan                 Rectángulo · fondo encabezados (ThemeSurface2)
        │   ├── lblHdrRefMan                     Etiqueta clásica · ESTILO-HEADER-COL
        │   ├── lblHdrDepMan                     Etiqueta clásica · ESTILO-HEADER-COL
        │   ├── lblHdrCtaMan                     Etiqueta clásica · ESTILO-HEADER-COL
        │   ├── lblHdrMontoMan                   Etiqueta clásica · ESTILO-HEADER-COL
        │   ├── btnHdrMotivoMan                  Botón clásico · encabezado ordenable
        │   ├── lblHdrEstadoMan                  Etiqueta clásica · ESTILO-HEADER-COL
        │   ├── shpGridLineaMan                  Rectángulo · línea 1 px
        │   ├── cmpEmptyListaMan                 Componente (cmpEmpty §8.6)
        │   └── galTxMan                         Galería vertical en blanco
        │       ├── shpTxSelMan                  Rectángulo · fondo fila sel (7.3)
        │       ├── shpTxSelBarraMan             Rectángulo · barra acento (7.3)
        │       ├── btnTxFilaMan                 Botón clásico · overlay (7.2)
        │       ├── lblTxRefMan                  Etiqueta clásica
        │       ├── lblTxDepMan                  Etiqueta clásica
        │       ├── lblTxCtaMan                  Etiqueta clásica
        │       ├── lblTxMontoMan                Etiqueta clásica
        │       ├── pilTxMotivoMan               Botón clásico (píldora 7.2) · motivo
        │       ├── pilTxEstadoMan               Botón clásico (píldora 7.2) · "Pendiente" (seg Pendientes)
        │       ├── lblTxAplicadaMan             Etiqueta clásica · hora (seg Aplicadas)
        │       └── shpTxFilaSepMan              Rectángulo · separador
        └── cntDetalleCardMan                    Contenedor (manual · card 7.1)
            ├── lblDetRefMan                     Etiqueta clásica · ESTILO-TITULO
            ├── lblDetSubMan                     Etiqueta clásica · ESTILO-APPSUB
            ├── cmpPilDetEstadoMan               Componente (cmpPildora §8.3)
            ├── shpDetLineaMan                   Rectángulo · línea 1 px (bajo cabecera)
            ├── cmpEmptyDetMan                   Componente (cmpEmpty §8.6)
            │                                    ══ grupo PENDIENTES (MA1) · Visible por segmento ══
            ├── cntKbHintMan                     Contenedor (manual · banner info Bnn)
            │   ├── icoKbHintMan                 Icono · Icon.Keyboard
            │   └── lblKbHintMan                 Etiqueta clásica
            ├── lblCampoCuentaLabMan             Etiqueta clásica            (TAB 1 · label)
            ├── inpCampoCuentaMan                Entrada de texto clásica    (TAB 1)
            ├── lblCampoMonedaLabMan             Etiqueta clásica            (TAB 2 · label)
            ├── inpCampoMonedaMan                Entrada de texto clásica    (TAB 2)
            ├── lblCampoMontoLabMan              Etiqueta clásica            (TAB 3 · label)
            ├── inpCampoMontoMan                 Entrada de texto clásica    (TAB 3)
            ├── lblCampoRefLabMan                Etiqueta clásica            (TAB 4 · label)
            ├── inpCampoRefMan                   Entrada de texto clásica    (TAB 4)
            ├── btnAplicarMan                    Botón clásico · ESTILO-BTN-PRIMARIO (TAB 5)
            ├── lblChequesSecMan                 Etiqueta clásica · ESTILO-SECCION
            ├── cntChequesHintMan                Contenedor (manual · banner info Bnn)
            │   ├── icoChequesHintMan            Icono · Icon.Information
            │   └── lblChequesHintMan            Etiqueta clásica
            ├── cmpEmptyChequesMan               Componente (cmpEmpty §8.6)
            ├── cntChequesGridMan                Contenedor (manual · card interna 7.1)
            │   ├── shpChqHeadBgMan              Rectángulo · fondo encabezado (ThemeSurface2)
            │   ├── lblChqHdrNumMan              Etiqueta clásica · ESTILO-HEADER-COL
            │   ├── lblChqHdrMontoMan            Etiqueta clásica · ESTILO-HEADER-COL
            │   ├── lblChqHdrMonMan              Etiqueta clásica · ESTILO-HEADER-COL
            │   ├── shpChqHeadLineaMan           Rectángulo · línea 1 px
            │   └── galChequesMan                Galería vertical en blanco
            │       ├── lblChqNumMan             Etiqueta clásica
            │       ├── lblChqMontoMan           Etiqueta clásica
            │       ├── lblChqMonMan             Etiqueta clásica
            │       └── shpChqSepMan             Rectángulo · separador
            │                                    ══ MA3 rechazo del guard · Visible con ctxErrorMan ══
            ├── cntDangerMan                     Contenedor (manual · banner peligro Bnn)
            │   ├── icoDangerMan                 Icono · Icon.Error
            │   └── lblDangerMan                 Etiqueta clásica
            ├── btnActualizarMan                 Botón clásico · ESTILO-BTN-CONTORNO
            │                                    ══ grupo APLICADAS (MA2) · Visible por segmento ══
            ├── cntInfoAplicadaMan               Contenedor (manual · banner info Bnn)
            │   ├── icoInfoAplicadaMan           Icono · Icon.Information
            │   └── lblInfoAplicadaMan           Etiqueta clásica
            ├── lblDatosSecMan                   Etiqueta clásica · ESTILO-SECCION
            ├── cntKvAplicadaMan                 Contenedor (manual)
            │   ├── lblKvK1Man / lblKvV1Man      Etiqueta clásica ×2
            │   ├── lblKvK2Man / lblKvV2Man      Etiqueta clásica ×2
            │   ├── lblKvK3Man / lblKvV3Man      Etiqueta clásica ×2
            │   ├── lblKvK4Man / lblKvV4Man      Etiqueta clásica ×2
            │   └── lblKvK5Man / lblKvV5Man      Etiqueta clásica ×2
            ├── lblMotivoSecMan                  Etiqueta clásica · ESTILO-SECCION
            └── cntWarnMotivoMan                 Contenedor (manual · banner warn Bnn)
                ├── icoWarnMotivoMan             Icono · Icon.Warning
                └── lblWarnMotivoMan             Etiqueta clásica
```

> **Receta local de banner (Bnn — versión manual)** — reutilizada por `cntKbHintMan`,
> `cntChequesHintMan`, `cntDangerMan`, `cntInfoAplicadaMan`, `cntWarnMotivoMan`:
> **Contenedor manual** con `Fill` = tono soft, `RadiusTop/Bottom Left/Right = 9`,
> `BorderThickness 0`. `Height := lbl<Bnn>.Y + lbl<Bnn>.Height + 9` (el contenedor
> se dimensiona a su etiqueta — el padre puede referenciar al hijo). Hijos:
> - **Icono** 16×16: `X 13 · Y 11 · Width 16 · Height 16` · Color = tinta del tono.
> - **Etiqueta clásica**: `X 38 · Y 9 · Width Parent.Width - 51 · Height Auto` con
>   `AutoHeight true`, `Wrap true`, Size `9`, Color = tinta del tono.
>
> Tonos (tabla 7.2): info `ThemeInfoSoft`/`ThemeInfo` · peligro `ThemeDangerSoft`/`ThemeDanger`
> · warn `ThemeWarnSoft`/`ThemeWarn`. El **alto lo da el label con `AutoHeight`**; todo control
> que va debajo encadena su `Y` a `cnt<Bnn>.Y + cnt<Bnn>.Height + <gap>` (respuesta al Wrap).

---

## 1. Pantalla `scrManual`

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |

`OnVisible` — siembra el estado local y deja el foco listo para grabar. La forma del rail
es la canónica del contrato (Custom API directa `Environment.sanic_CHWorkloadByCashier`,
01 §2, que devuelve un JSON en `.Workload`):

```powerfx
// 1) Segmentos y orden por defecto (ctx = UpdateContext, estado LOCAL — §5 / 01)
UpdateContext({
    ctxSegManual: "Pendientes",
    ctxOrdenMan: "Fecha",
    ctxErrorMan: Blank()
});

// 2) Rail: agregado por cajero server-side. La Custom API devuelve un texto JSON en
//    .Workload; se parsea y proyecta a campos tipados. Se materializa en colección
//    para que el rail (cmpRailMan.Datos) y el bootstrap no dependan del timing de render.
ClearCollect(
    colCajerosMan,
    Filter(
        ForAll(
            Table(ParseJSON(Environment.sanic_CHWorkloadByCashier().Workload)),
            {
                cajero:           GUID(Text(ThisRecord.cajero)),
                cajeroNombre:     Text(ThisRecord.cajeroNombre),
                mesa:             Text(ThisRecord.mesa),
                pendientesManual: Value(ThisRecord.pendientesManual),
                badge:            Text(Value(ThisRecord.pendientesManual))
            }
        ),
        pendientesManual > 0
    )
);

// 3) Primer cajero (más pendientes primero = orden por defecto del rail) y su
//    primera manual pendiente (más vieja primero = orden de grabación)
Set(
    gvarCajeroMan,
    First(Sort(colCajerosMan, pendientesManual, SortOrder.Descending))
);
Set(
    gvarTxMan,
    First(
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus)
                    && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Ascending
        )
    )
);

// 4) Refrescar los campos copiables al registro sembrado y caer en Cuenta
Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
SetFocus(inpCampoCuentaMan)
```

> 🧨 Los `inp…` clásicos sólo recomputan su `Default` al **`Reset`**. Cada vez que cambia
> `gvarTxMan` (rail, clic de fila, segmento, auto-avance) hay que `Reset` de los cuatro
> campos — por eso el bloque se repite en cada uno de esos `OnSelect`.

> **Formas del JSON (asunción declarada)** — se asume que `.Workload` trae por cajero
> `{cajero, cajeroNombre, mesa, pendientesManual, ...}`. Si el contrato del API nombra distinto los
> campos, ajustar la proyección; el resto de la guía sólo depende de `colCajerosMan` con
> `{cajero (record Users), nombre, mesa, pendientesManual, badge}`.

---

## 2. Encabezado `cmpHeaderMan` — Componente (cmpHeader §8.1)

Una sola instancia del componente `cmpHeader`. Reemplaza a TODO el subárbol de header de
la v1 (marca, títulos, nav, chip) — ahora vive dentro del componente.

| Propiedad (nativa / custom) | Valor |
|---|---|
| `X` / `Y` | `0` / `0` |
| `Width` / `Height` | `Parent.Width` / `LayHeaderH` |
| `Titulo` (Input) | `"Digitación"` |
| `Subtitulo` (Input) | `"Aplicación manual"` |
| `Activo` (Input) | `"Manual"` |
| `OnCorte` (Behavior) | `Navigate(scrCorte)` |
| `OnManual` (Behavior) | `false` *(ya estamos acá)* |
| `OnLotes` (Behavior) | `Navigate(scrLotes)` |
| `OnCheques` (Behavior) | `Navigate(scrCheques)` |

> El componente resalta el toggle "Manual" porque su nav interno compara contra
> `cmpHeader.Activo = "Manual"` (§8.1). El usuario/marca/iniciales los pinta el propio
> componente con `UsuarioNombre` / `UsuarioIniciales` (01 §3).

---

## 3. Cuerpo `cntBodyMan` + rail + main

### 3.1 `cntBodyMan` (Contenedor manual)

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `0` / `LayHeaderH` |
| `Width` | `Parent.Width` |
| `Height` | `Parent.Height - LayHeaderH` |
| `Fill` | `Color.Transparent` |

### 3.2 `cmpRailMan` — rail de cajeros (cmpRailCajeros §8.2)

Instancia del componente rail. Reemplaza a TODO el subárbol de rail de la v1 (head, órdenes,
galería, filas). El **orden Más pendientes / A–Z es estado interno del componente** (L-D11).

| Propiedad (nativa / custom) | Valor |
|---|---|
| `X` / `Y` | `0` / `0` |
| `Width` / `Height` | `LayRailW` / `Parent.Height` |
| `Titulo` (Input) | `"CAJEROS CON MANUALES"` |
| `Datos` (Input, Table) | `colCajerosMan` *(filas `{cajeroNombre, mesa, badge}` para pintar + `{cajero, pendientesManual}` que la pantalla lee vía `CajeroSel`)* |
| `CajeroSel` (Output, Record) | *(la fila seleccionada; la pantalla la consume en `OnSeleccion`)* |
| `OnSeleccion` (Behavior) | bloque **[SELECCIONAR CAJERO]** ↓ |

Bloque **[SELECCIONAR CAJERO]** — `cmpRailMan.OnSeleccion`:

```powerfx
Set(gvarCajeroMan, cmpRailMan.CajeroSel);
UpdateContext({ctxSegManual: "Pendientes", ctxErrorMan: Blank()});
Set(
    gvarTxMan,
    First(
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus)
                    && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Ascending
        )
    )
);
Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
SetFocus(inpCampoCuentaMan)
```

> **Selección del rail = del componente** (`CajeroSel = galCajerosRail.Selected`, §8.2), NO
> por `gvarCajeroMan` (divergencia vs la grilla, L-D2): el cajero sólo se elige por clic en
> el rail — nunca se setea programáticamente a mitad de flujo (a diferencia de `gvarTxMan`,
> que sí en el auto-avance). El bootstrap siembra `gvarCajeroMan` con el de **más pendientes**
> para que coincida con la fila que el rail auto-selecciona en su orden por defecto.

> 🧨 **Dependencia B-workload** (`sanic_CHWorkloadByCashier`, 00 §3.3). **Fallback sin
> backend** — reemplazar el paso 2 del `OnVisible` por un agregado local sobre `Users`
> (correcto sólo < 500 filas por cajero, provisorio):
> ```powerfx
> ClearCollect(colCajerosMan,
>   AddColumns(
>     Filter(Users, /* …team digitación… */),
>     "cajero",           ThisRecord,
>     "nombre",           'Full Name',
>     "mesa",             "—",
>     "pendientesManual", CountRows(Filter('CH-Tbl-Transactions',
>         TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
>         && IsBlank(BatchStatus) && IsBlank(TransactionStatus) && 'Created By' = ThisRecord)),
>     "badge",            Text(CountRows(Filter('CH-Tbl-Transactions',
>         TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
>         && IsBlank(BatchStatus) && IsBlank(TransactionStatus) && 'Created By' = ThisRecord)))
>   )
> )
> ```
> En fallback `cajero` es el propio `Users`, así que en los filtros `'Created By'.'Primary Key' = gvarCajeroMan.cajero` sigue valiendo.

### 3.3 `cntMainMan` (Contenedor manual) — reparte lista + panel

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `LayRailW` / `0` |
| `Width` | `Parent.Width - LayRailW` |
| `Height` | `Parent.Height` |
| `Fill` | `Color.Transparent` |

Dos cards lado a lado (mock MA1: lista `flex 1.5`, panel `flex 1` → 0.6 / 0.4 del ancho útil).
El ancho útil descuenta `LayGutter` a cada lado y `LayGap` entre cards.

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `cntListaCardMan` | `LayGutter` | `LayGutter` | `(Parent.Width - LayGutter*2 - LayGap) * 0.6` | `Parent.Height - LayGutter*2` |
| `cntDetalleCardMan` | `cntListaCardMan.X + cntListaCardMan.Width + LayGap` | `LayGutter` | `Parent.Width - cntDetalleCardMan.X - LayGutter` | `Parent.Height - LayGutter*2` |

---

## 4. Card de lista (`cntListaCardMan`) — segmentado + grilla

Card receta **7.1**: Contenedor manual, `Fill ThemeSurface`, `BorderColor ThemeLine`,
`BorderThickness 1`, `RadiusTop/Bottom Left/Right = LayCardR`. Sus hijos se posicionan con
`X`/`Y`/`Width`/`Height` relativos a `Parent` (la card). Padding interno = **16** (constante local).

### 4.1 Toolbar: segmentado + orden

`cntSegMan` (Contenedor manual, segmentado): `Fill ThemeSurface2`, `Border ThemeLine 1`,
`Radius 9`.

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `cntSegMan` | `16` | `12` | `210` | `30` |
| `btnSegPendientesMan` | `3` | `3` | `(Parent.Width - 6) / 2` | `Parent.Height - 6` |
| `btnSegAplicadasMan` | `btnSegPendientesMan.X + btnSegPendientesMan.Width` | `3` | `(Parent.Width - 6) / 2` | `Parent.Height - 6` |
| `lblOrdenMan` | `Parent.Width - 16 - Self.Width` | `18` | `170` | `18` |

| Control | Propiedad | Valor |
|---|---|---|
| `btnSegPendientesMan` | Text | `"Pendientes" & If(ctxSegManual = "Pendientes", " (" & CountRows(galTxMan.AllItems) & ")", "")` *(conteo sólo en el activo — L-D6)* |
| | Radius | `7` |
| | Fill | `If(ctxSegManual = "Pendientes", ThemeSurface, Color.Transparent)` |
| | Color / FontWeight | `If(ctxSegManual = "Pendientes", ThemeAccent, ThemeInk2)` / `If(ctxSegManual = "Pendientes", Semibold, Normal)` |
| | OnSelect | bloque **[SEGMENTO PENDIENTES]** ↓ |
| `btnSegAplicadasMan` | Text | `"Aplicadas" & If(ctxSegManual = "Aplicadas", " (" & CountRows(galTxMan.AllItems) & ")", "")` |
| | Radius | `7` |
| | Fill / Color / FontWeight | idem con `ctxSegManual = "Aplicadas"` |
| | OnSelect | bloque **[SEGMENTO APLICADAS]** ↓ |
| `lblOrdenMan` | Text | `"Orden: " & If(ctxSegManual = "Aplicadas", "Aplicada ↓", If(ctxOrdenMan = "Motivo", "Motivo", "Fecha"))` · Color `ThemeInk2` · Align Right |

Bloque **[SEGMENTO PENDIENTES]** — `btnSegPendientesMan.OnSelect`:

```powerfx
UpdateContext({ctxSegManual: "Pendientes", ctxErrorMan: Blank()});
Set(
    gvarTxMan,
    First(
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus)
                    && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Ascending
        )
    )
);
Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
SetFocus(inpCampoCuentaMan)
```

Bloque **[SEGMENTO APLICADAS]** — `btnSegAplicadasMan.OnSelect`:

```powerfx
UpdateContext({ctxSegManual: "Aplicadas", ctxErrorMan: Blank()});
Set(
    gvarTxMan,
    First(
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus)
                    && TransactionStatus = 'TransactionStatus (CH-Tbl-Transactions)'.Aplicada
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Descending
        )
    )
)
```

### 4.2 Encabezados de columna (grilla, receta 7.2)

Fondo + 5 etiquetas ESTILO-HEADER-COL + 1 botón ordenable. Sea `HY = cntSegMan.Y + cntSegMan.Height + 12`
(= la Y del encabezado, encadenada al segmentado) y `HH = 30`. Las columnas se reparten por
**fracción de `Parent.Width`** (misma fracción que su celda en el template, 7.2).

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `shpGridHeadBgMan` (Rectángulo) | `0` | `HY` | `Parent.Width` | `HH` | Fill `ThemeSurface2` · BorderThickness `0` |
| `lblHdrRefMan` | `Parent.Width*0.03` | `HY` | `Parent.Width*0.13` | `HH` | `"REFERENCIA"` · Align Left |
| `lblHdrDepMan` | `Parent.Width*0.16` | `HY` | `Parent.Width*0.26` | `HH` | `"DEPÓSITO / CLIENTE"` · Align Left |
| `lblHdrCtaMan` | `Parent.Width*0.43` | `HY` | `Parent.Width*0.13` | `HH` | `"CUENTA"` · Align Right |
| `lblHdrMontoMan` | `Parent.Width*0.57` | `HY` | `Parent.Width*0.12` | `HH` | `"MONTO"` · Align Right |
| `btnHdrMotivoMan` | `Parent.Width*0.70` | `HY` | `Parent.Width*0.14` | `HH` | encabezado ordenable ↓ |
| `lblHdrEstadoMan` | `Parent.Width*0.84` | `HY` | `Parent.Width*0.14` | `HH` | `If(ctxSegManual = "Aplicadas", "APLICADA", "ESTADO")` · Align Left |

`btnHdrMotivoMan` (encabezado sortable): Text `"MOTIVO " & If(ctxOrdenMan = "Motivo", "▼", "")`,
Fill `Color.Transparent`, BorderThickness `0`, Color `If(ctxOrdenMan = "Motivo", ThemeAccent, ThemeInk2)`,
Size `7.5`, FontWeight `Bold`, Align `Align.Left`, PaddingLeft `0`,
`OnSelect: UpdateContext({ctxOrdenMan: If(ctxOrdenMan = "Motivo", "Fecha", "Motivo")})`,
AccessibleLabel `"Ordenar por motivo"`.

| Control | X | Y | Width | Height | props |
|---|---|---|---|---|---|
| `shpGridLineaMan` (Rectángulo) | `0` | `HY + HH` | `Parent.Width` | `1` | Fill `ThemeLine` |

### 4.3 Grilla de transacciones (`galTxMan`, Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `0` / `shpGridLineaMan.Y + 1` |
| `Width` | `Parent.Width` |
| `Height` | `Parent.Height - (shpGridLineaMan.Y + 1)` |
| `TemplateSize` | `LayRowH` (44) · `TemplatePadding 0` · `ShowScrollbar true` |
| `Items` | fórmula **[ITEMS GRILLA]** ↓ |
| `OnSelect` | bloque **[SELECCIONAR TX]** ↓ |

`cmpEmptyListaMan` (cmpEmpty §8.6) — overlay sobre el área de galería:

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `0` / `galTxMan.Y` |
| `Width` / `Height` | `Parent.Width` / `galTxMan.Height` |
| `Glifo` (Input) | `"⛁"` |
| `Mensaje` (Input) | `If(ctxSegManual = "Aplicadas", "Sin transacciones aplicadas para este cajero.", "Sin manuales pendientes para este cajero.")` |
| `Visible` | `CountRows(galTxMan.AllItems) = 0` |

Fórmula **[ITEMS GRILLA]** — `galTxMan.Items` (Filtros delegables; el orden por Motivo es
client-side sobre el set ya delegado ≤500 — declarado en §9):

```powerfx
If(
    ctxSegManual = "Aplicadas",
    // --- APLICADAS: más recientes primero ---
    Sort(
        Filter(
            'CH-Tbl-Transactions',
            TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                && IsBlank(BatchStatus)
                && TransactionStatus = 'TransactionStatus (CH-Tbl-Transactions)'.Aplicada
                && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
        ),
        'Created On', SortOrder.Descending
    ),
    // --- PENDIENTES ---
    If(
        ctxOrdenMan = "Motivo",
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus) && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            // rango de motivo (Rechazo core > Sin cuenta > Con cheques > —)
            Switch(
                true,
                !IsBlank(sanic_motivorechazocore), 3,
                IsBlank(AccountNumber), 2,
                !IsBlank(RelatedDepositRecounting), 1,
                0
            ),
            SortOrder.Descending
        ),
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus) && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Ascending
        )
    )
)
```

**Template** (columnas alineadas con §4.2; el orden visual = orden en árbol. `shp…Sel*` van
PRIMERO por z-order; `btn…Fila` overlay al FONDO. `TW = Parent.TemplateWidth`,
`TH = Parent.TemplateHeight`; **X del template = X del encabezado − X de la galería**; como
`galTxMan.X = 0`, las fracciones de `TW` coinciden con las del encabezado):

| Control | Tipo | X / Y / W / H | Propiedad · Valor |
|---|---|---|---|
| `shpTxSelMan` (7.3, PRIMERO) | Rectángulo | `0` / `0` / `TW` / `TH` | Fill `If(!IsBlank(gvarTxMan) && ThisItem.'CH-Tbl-Transactions' = gvarTxMan.'CH-Tbl-Transactions', ThemeAccentSoft, Color.Transparent)` · BorderThickness `0` · OnSelect `Select(Parent)` |
| `shpTxSelBarraMan` (7.3) | Rectángulo | `0` / `0` / `3` / `TH` | Fill `ThemeAccent` · Visible `!IsBlank(gvarTxMan) && ThisItem.'CH-Tbl-Transactions' = gvarTxMan.'CH-Tbl-Transactions'` |
| `btnTxFilaMan` (overlay 7.2, al FONDO) | Botón clásico | `0` / `0` / `TW` / `TH` | Fill/Hover/Pressed `Transparent`/`ThemeSurface2`/`ThemeAccentSoft` · Text `""` · BorderThickness `0` · OnSelect `Select(Parent)` · AccessibleLabel = ref de la fila (ver §6) |
| `lblTxRefMan` | Etiqueta clásica | `TW*0.03` / — / `TW*0.13` / — | Text = ref (§6) · Semibold · OnSelect `Select(Parent)` |
| `lblTxDepMan` | Etiqueta clásica | `TW*0.16` / — / `TW*0.26` / — | Text = depósito/cliente (§6) · OnSelect `Select(Parent)` |
| `lblTxCtaMan` | Etiqueta clásica | `TW*0.43` / — / `TW*0.13` / — | Align Right · Text `If(IsBlank(ThisItem.AccountNumber), "— sin cuenta —", Text(ThisItem.AccountNumber, "[$-en-US]0"))` · Color `If(IsBlank(ThisItem.AccountNumber), ThemeInk3, ThemeInk)` · OnSelect `Select(Parent)` |
| `lblTxMontoMan` | Etiqueta clásica | `TW*0.57` / — / `TW*0.12` / — | Align Right · ESTILO-CELDA-NUM · Text `Text(ThisItem.Total, FmtMoney)` · OnSelect `Select(Parent)` |
| `pilTxMotivoMan` (píldora 7.2) | Botón clásico | `TW*0.70` / `(TH-22)/2` / `40 + 7*Len(Self.Text)` / `22` | DisplayMode `View` · Radius `12` · Size `7.88` · Semibold · BorderThickness `0` · Text = **[MOTIVO]** ↓ · Fill = **[FILL MOTIVO]** ↓ · Color = **[COLOR MOTIVO]** ↓ |
| `pilTxEstadoMan` (píldora 7.2) | Botón clásico | `TW*0.84` / `(TH-22)/2` / `78` / `22` | DisplayMode `View` · Radius `12` · Size `7.88` · Semibold · Text `"Pendiente"` · Fill `ThemeLineSoft` · Color `ThemeInk3` · BorderThickness `1` · BorderColor `ThemeLineSoft` · Visible `ctxSegManual = "Pendientes"` |
| `lblTxAplicadaMan` | Etiqueta clásica | `TW*0.84` / — / `TW*0.14` / — | Text `Text(ThisItem.'Modified On', FmtHora)` · Visible `ctxSegManual = "Aplicadas"` · OnSelect `Select(Parent)` |
| `shpTxFilaSepMan` | Rectángulo | `0` / `TH-1` / `TW` / `1` | Fill `ThemeLineSoft` |

Fórmula **[MOTIVO]** — `pilTxMotivoMan.Text` (la clasificación del motivo; misma expresión
la reusan Fill y Color abajo — la píldora es un Botón nativo, no un componente, así que mapea
directo de `ThisItem` sin propiedad `Tono` intermedia):

```powerfx
Switch(
    true,
    !IsBlank(ThisItem.sanic_motivorechazocore), "Rechazo core",
    IsBlank(ThisItem.AccountNumber),            "Sin cuenta",
    !IsBlank(ThisItem.RelatedDepositRecounting),"Con cheques",
    "—"
)
```

Fórmula **[FILL MOTIVO]** — `pilTxMotivoMan.Fill` (`With` evita recalcular la clasificación
tres veces; los `Theme*Soft` son los mismos que usa `cmpPildora` §8.3):

```powerfx
With(
    { m: Switch(true,
        !IsBlank(ThisItem.sanic_motivorechazocore), "Rechazo core",
        IsBlank(ThisItem.AccountNumber),            "Sin cuenta",
        !IsBlank(ThisItem.RelatedDepositRecounting),"Con cheques",
        "—") },
    Switch(m,
        "Rechazo core", ThemeDangerSoft,
        "Sin cuenta",   ThemeWarnSoft,
        "Con cheques",  ThemeInfoSoft,
        ThemeLineSoft)
)
```

Fórmula **[COLOR MOTIVO]** — `pilTxMotivoMan.Color` (mismo `m`, tinta fuerte):

```powerfx
With(
    { m: Switch(true,
        !IsBlank(ThisItem.sanic_motivorechazocore), "Rechazo core",
        IsBlank(ThisItem.AccountNumber),            "Sin cuenta",
        !IsBlank(ThisItem.RelatedDepositRecounting),"Con cheques",
        "—") },
    Switch(m,
        "Rechazo core", ThemeDanger,
        "Sin cuenta",   ThemeWarn,
        "Con cheques",  ThemeInfo,
        ThemeInk3)
)
```

Bloque **[SELECCIONAR TX]** — `galTxMan.OnSelect` (la acción vive en la galería; cada hijo
delega con `Select(Parent)` — receta 7.2):

```powerfx
Set(gvarTxMan, galTxMan.Selected);
UpdateContext({ctxErrorMan: Blank()});
Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
If(ctxSegManual = "Pendientes", SetFocus(inpCampoCuentaMan))
```

> **Divergencia declarada vs receta 7.3** (igual que el molde del tracker): la selección de
> la grilla se resuelve por `gvarTxMan`, NO por `ThisItem.IsSelected`, porque `gvarTxMan`
> también se setea **programáticamente** en el auto-avance del `Patch`, que `IsSelected` no
> conoce.

> **Píldora en la fila = Botón NATIVO, no componente** (L-D9): un `CanvasComponent` **no
> puede** vivir dentro de una galería (Power Apps rechaza con `PA2122` — el scope de fila
> `ThisItem` y el scope aislado del componente no se anidan). Por eso `pilTxMotivoMan` /
> `pilTxEstadoMan` son Botones clásicos que replican el estilo de `cmpPildora` §8.3 pero leen
> directo de `ThisItem`. Son `DisplayMode.View` (no interactivos), así que **no delegan**
> `Select(Parent)`; la selección de fila la garantiza `btnTxFilaMan` al fondo del z-order en
> todo el resto de la fila. Un clic exactamente sobre la píldora puede no seleccionar — costo
> aceptado (es chica y el resto de la fila responde). Las píldoras de **detalle**
> (`cmpPilDetEstadoMan`) sí quedan como componente: viven fuera de galería, ahí es legal.

---

## 5. Panel de detalle (`cntDetalleCardMan`)

Card receta **7.1**. Padding interno = **16**. Todo se posiciona manual, relativo a `Parent`
(la card). Los bloques PENDIENTES / MA3 / APLICADAS **se superponen en Y** (sólo uno visible
a la vez por sus `Visible`), por eso no se estorban.

### 5.1 Cabecera

| Control | X | Y | Width | Height | props |
|---|---|---|---|---|---|
| `lblDetRefMan` | `16` | `12` | `Parent.Width - 130` | `22` | Text = ref de `gvarTxMan` (§6) · ESTILO-TITULO |
| `lblDetSubMan` | `16` | `34` | `Parent.Width - 130` | `16` | Text = depósito/cliente de `gvarTxMan` (§6) · ESTILO-APPSUB |
| `cmpPilDetEstadoMan` (cmpPildora §8.3) | `Parent.Width - 16 - Self.Width` | `16` | `40 + 7*Len(Self.Texto)` | `22` | ver ↓ |
| `shpDetLineaMan` (Rectángulo) | `0` | `52` | `Parent.Width` | `1` | Fill `ThemeLine` |

`cmpPilDetEstadoMan`:
- `Texto` = `If(ctxSegManual = "Aplicadas", "Aplicada", Switch(true, !IsBlank(gvarTxMan.sanic_motivorechazocore), "Rechazo core", IsBlank(gvarTxMan.AccountNumber), "Sin cuenta", !IsBlank(gvarTxMan.RelatedDepositRecounting), "Con cheques", "—"))` *(= [MOTIVO] aplicado a `gvarTxMan`)*.
- `Tono` = `If(ctxSegManual = "Aplicadas", "ok", Switch(Self.Texto, "Rechazo core", "danger", "Sin cuenta", "warn", "Con cheques", "info", "neutro"))`.

`cmpEmptyDetMan` (cmpEmpty §8.6):

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `0` / `53` |
| `Width` / `Height` | `Parent.Width` / `Parent.Height - 53` |
| `Glifo` (Input) | `"⌨"` |
| `Mensaje` (Input) | `"Seleccioná una transacción para grabarla."` |
| `Visible` | `IsBlank(gvarTxMan)` |

Sea **`BODY = shpDetLineaMan.Y + 14`** (= 66) la Y de arranque del cuerpo, y `CW = Parent.Width - 32`
el ancho de contenido. Todo lo que sigue está a `X 16` salvo aclaración.

### 5.2 Grupo PENDIENTES (MA1) — flujo keyboard-first

Cada control lleva en `Visible` la conjunción del segmento (gotcha de pestaña simulada).
Sea **`VIS_P = ctxSegManual = "Pendientes" && IsBlank(ctxErrorMan) && !IsBlank(gvarTxMan)`**.

**Banner de ayuda** — `cntKbHintMan` (banner Bnn info, tono info):

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `cntKbHintMan` | `16` | `BODY` | `CW` | `lblKbHintMan.Y + lblKbHintMan.Height + 9` |

`cntKbHintMan.Visible = VIS_P`. `icoKbHintMan` = `Icon.Keyboard`. `lblKbHintMan.Text`:
`"Copiá y tabulá: Cuenta → Tab → Moneda → … → Referencia → Tab → Enter aplica y pasa al siguiente."`

**Campos copiables (receta 8.7)** — cada uno es una **Etiqueta clásica** (label) + una
**Entrada de texto clásica** (`inp…`) `DisplayMode Edit`, `Fill ThemeSurface2`,
`BorderColor ThemeLine`, `Radius 7`, `FocusedBorderColor ThemeAccent`, `FocusedBorderThickness 2`,
`OnChange: Reset(Self)` (revierte cualquier edición — los datos del cliente NO se modifican).
El **label lleva el número de orden visible**; el `inp` lleva el `TabIndex` real. Todos
`Visible = VIS_P`. Y encadenada (cada control referencia al anterior):

| Control | X | Y | Width | Height |
|---|---|---|---|---|
| `lblCampoCuentaLabMan` | `16` | `cntKbHintMan.Y + cntKbHintMan.Height + 12` | `CW` | `16` |
| `inpCampoCuentaMan` | `16` | `lblCampoCuentaLabMan.Y + 18` | `CW` | `34` |
| `lblCampoMonedaLabMan` | `16` | `inpCampoCuentaMan.Y + inpCampoCuentaMan.Height + 10` | `CW` | `16` |
| `inpCampoMonedaMan` | `16` | `lblCampoMonedaLabMan.Y + 18` | `CW` | `34` |
| `lblCampoMontoLabMan` | `16` | `inpCampoMonedaMan.Y + inpCampoMonedaMan.Height + 10` | `CW` | `16` |
| `inpCampoMontoMan` | `16` | `lblCampoMontoLabMan.Y + 18` | `CW` | `34` |
| `lblCampoRefLabMan` | `16` | `inpCampoMontoMan.Y + inpCampoMontoMan.Height + 10` | `CW` | `16` |
| `inpCampoRefMan` | `16` | `lblCampoRefLabMan.Y + 18` | `CW` | `34` |

| Campo | Label (Text, Size 8.25 Semibold `ThemeInk2`) | `inp` Default | TabIndex |
|---|---|---|---|
| Cuenta | `"TAB 1 · Número de cuenta"` | `If(IsBlank(gvarTxMan.AccountNumber), "— sin cuenta —", Text(gvarTxMan.AccountNumber, "[$-en-US]0"))` | `1` |
| Moneda | `"TAB 2 · Moneda"` | `gvarTxMan.Currency` *(si es Choice: `gvarTxMan.Currency.Value`)* | `2` |
| Monto | `"TAB 3 · Monto total"` | `Text(gvarTxMan.Total, FmtMoney)` *(calculado — sólo lectura)* | `3` |
| Referencia | `"TAB 4 · Referencia"` | `gvarTxMan.TransactionReference` | `4` |

**Botón aplicar** — `btnAplicarMan` (ESTILO-BTN-PRIMARIO):

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `16` / `inpCampoRefMan.Y + inpCampoRefMan.Height + 14` |
| `Width` / `Height` | `CW` / `40` |
| Text | `"↵  Marcar como aplicada (Enter)"` |
| `TabIndex` | `5` |
| `AutoDisableOnSelect` | `true` *(anti doble-click)* |
| `Visible` | `VIS_P` |
| AccessibleLabel | `"Marcar como aplicada y pasar a la siguiente"` |
| OnSelect | bloque **[APLICAR]** ↓ |

> Con foco en `inpCampoRefMan`, **Tab** cae en `btnAplicarMan` (TabIndex 5) y **Enter**
> dispara su `OnSelect` — comportamiento nativo del Botón clásico, garantizado.

Bloque **[APLICAR]** — `btnAplicarMan.OnSelect` (corrige P-1, P-2, C-2, W-1):

```powerfx
IfError(
    // 1 sola columna, SIN SubmitForm, SIN Refresh de tabla completa
    Patch(
        'CH-Tbl-Transactions',
        gvarTxMan,
        { TransactionStatus: 'TransactionStatus (CH-Tbl-Transactions)'.Aplicada }
    ),
    // --- Fallback: el guard rechazó / hubo error → feedback VISIBLE (MA3) ---
    UpdateContext({ctxErrorMan: Coalesce(FirstError.Message, "No se pudo aplicar la transacción.")});
    Notify(ctxErrorMan, NotificationType.Error),
    // --- DefaultResult (sin error): éxito + auto-avance ---
    UpdateContext({ctxErrorMan: Blank()});
    Notify("Transacción aplicada.", NotificationType.Success);
    // la tx recién aplicada ya tiene TransactionStatus → sale del filtro sin Refresh
    Set(
        gvarTxMan,
        First(
            Sort(
                Filter(
                    'CH-Tbl-Transactions',
                    TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                        && IsBlank(BatchStatus)
                        && IsBlank(TransactionStatus)
                        && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
                ),
                'Created On', SortOrder.Ascending
            )
        )
    );
    Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
    Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
    SetFocus(inpCampoCuentaMan)
)
```

> `SetFocus` cae en `inpCampoCuentaMan`, que **está visible** en el éxito (corrige W-1: el
> bug viejo enfocaba un control recién oculto). Cuando ya no quedan pendientes, `gvarTxMan`
> queda Blank y aparece `cmpEmptyDetMan`.

**Cheques de la transacción** (cotejo contra los físicos en mano) — sea
**`VIS_CHQ = VIS_P && !IsBlank(gvarTxMan)`** (ya cubierto por `VIS_P`):

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `lblChequesSecMan` (ESTILO-SECCION) | `16` | `btnAplicarMan.Y + btnAplicarMan.Height + 16` | `CW` | `16` | `"CHEQUES DE LA TRANSACCIÓN (" & CountRows(galChequesMan.AllItems) & ")"` |
| `cntChequesHintMan` (banner Bnn info) | `16` | `lblChequesSecMan.Y + lblChequesSecMan.Height + 8` | `CW` | `lblChequesHintMan.Y + lblChequesHintMan.Height + 9` | icono `Icon.Information` · label `"Cotejá contra los cheques que tenés en mano antes de aplicar."` |

`cmpEmptyChequesMan` (cmpEmpty §8.6) — Glifo `"—"`, Mensaje `"Esta transacción no tiene
cheques asociados."`, Visible `VIS_P && CountRows(galChequesMan.AllItems) = 0`:

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `16` / `cntChequesHintMan.Y + cntChequesHintMan.Height + 8` |
| `Width` / `Height` | `CW` / `70` |

`cntChequesGridMan` (card interna 7.1, `Radius 9`, `Border ThemeLine 1`) — Visible
`VIS_P && CountRows(galChequesMan.AllItems) > 0`:

| Propiedad | Valor |
|---|---|
| `X` / `Y` | `16` / `cntChequesHintMan.Y + cntChequesHintMan.Height + 8` |
| `Width` | `CW` |
| `Height` | `26 + Min(CountRows(galChequesMan.AllItems), 4) * 34 + 2` *(alto acotado, ≤4 filas visibles; el resto scrollea — L-D10)* |

Hijos de `cntChequesGridMan` (Parent = card interna; `IW = Parent.Width`):

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `shpChqHeadBgMan` (Rectángulo) | `0` | `0` | `IW` | `26` | Fill `ThemeSurface2` |
| `lblChqHdrNumMan` (ESTILO-HEADER-COL) | `IW*0.04` | `0` | `IW*0.50` | `26` | `"N° CHEQUE"` |
| `lblChqHdrMontoMan` (ESTILO-HEADER-COL) | `IW*0.54` | `0` | `IW*0.28` | `26` | `"MONTO"` · Align Right |
| `lblChqHdrMonMan` (ESTILO-HEADER-COL) | `IW*0.82` | `0` | `IW*0.14` | `26` | `"MON."` · Align Right |
| `shpChqHeadLineaMan` (Rectángulo) | `0` | `26` | `IW` | `1` | Fill `ThemeLine` |
| `galChequesMan` (Galería vertical en blanco) | `0` | `27` | `IW` | `Parent.Height - 27` | `TemplateSize 34` · `Items` = **[ITEMS CHEQUES]** ↓ |

Template de `galChequesMan` (`TWc = Parent.TemplateWidth`, `THc = Parent.TemplateHeight`):

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `lblChqNumMan` | `TWc*0.04` | — | `TWc*0.50` | — | `ThisItem.CheckNumber` · Semibold |
| `lblChqMontoMan` | `TWc*0.54` | — | `TWc*0.28` | — | `Text(ThisItem.CheckAmount, FmtMoney)` · Align Right |
| `lblChqMonMan` | `TWc*0.82` | — | `TWc*0.14` | — | `ThisItem.CheckCurrency` · Align Right |
| `shpChqSepMan` (Rectángulo) | `0` | `THc - 1` | `TWc` | `1` | Fill `ThemeLineSoft` |

Fórmula **[ITEMS CHEQUES]** — `galChequesMan.Items` (delegable, guarda contra huérfanos — corrige W-2 y D-5):

```powerfx
If(
    !IsBlank(gvarTxMan.RelatedDepositRecounting),
    Filter(
        'CH-Tbl-Checks',
        RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting'
            = gvarTxMan.RelatedDepositRecounting.'CH-Tbl-DepositsRecounting'
    ),
    // else explícito (nunca Blank como fuente → evita ErrorKind.Internal)
    Filter('CH-Tbl-Checks', false)
)
```

### 5.3 MA3 — rechazo del guard (dentro de Pendientes, `ctxErrorMan` seteado)

Sea **`VIS_ERR = ctxSegManual = "Pendientes" && !IsBlank(ctxErrorMan)`**. Ocupa el mismo
espacio Y que los campos (que se ocultan porque su `Visible` exige `IsBlank(ctxErrorMan)`).

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `cntDangerMan` (banner Bnn peligro) | `16` | `BODY` | `CW` | `lblDangerMan.Y + lblDangerMan.Height + 9` | icono `Icon.Error` · label = `ctxErrorMan` (mensaje **real** del guard) · Visible `VIS_ERR` |
| `btnActualizarMan` (ESTILO-BTN-CONTORNO) | `16` | `cntDangerMan.Y + cntDangerMan.Height + 14` | `CW` | `32` | Text `"↻ Actualizar lista"` · Visible `VIS_ERR` · OnSelect **[ACTUALIZAR]** ↓ |

Bloque **[ACTUALIZAR]** — `btnActualizarMan.OnSelect`:

```powerfx
Refresh('CH-Tbl-Transactions');   // Refresh DELIBERADO iniciado por el usuario (≠ P-1: no es por-registro)
UpdateContext({ctxErrorMan: Blank()});
Set(
    gvarTxMan,
    First(
        Sort(
            Filter(
                'CH-Tbl-Transactions',
                TransactionProcessingType = 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual
                    && IsBlank(BatchStatus) && IsBlank(TransactionStatus)
                    && 'Created By'.'Primary Key' = gvarCajeroMan.cajero
            ),
            'Created On', SortOrder.Ascending
        )
    )
);
Reset(inpCampoCuentaMan); Reset(inpCampoMonedaMan);
Reset(inpCampoMontoMan);  Reset(inpCampoRefMan);
SetFocus(inpCampoCuentaMan)
```

> **Divergencia declarada vs MA3** (L-D3): el mock repite un `kv` de datos bajo el banner.
> Acá se omite ese `kv` redundante — los datos ya se veían en los campos copiables; en el
> rechazo se prioriza el banner + "Actualizar lista". Los campos y la sección de cheques se
> **ocultan** mientras `ctxErrorMan` está seteado (por eso su `Visible` incluye `IsBlank(ctxErrorMan)`).

### 5.4 Grupo APLICADAS (MA2) — trazabilidad

Sea **`VIS_A = ctxSegManual = "Aplicadas" && !IsBlank(gvarTxMan)`**. El banner de origen es
condicional (`&& !IsBlank(gvarTxMan.sanic_motivorechazocore)`); para no dejar un hueco cuando
falta, la Y del bloque siguiente **colapsa con `If`** sobre esa misma condición.

| Control | X | Y | Width | Height | Text / props |
|---|---|---|---|---|---|
| `cntInfoAplicadaMan` (banner Bnn info) | `16` | `BODY` | `CW` | `lblInfoAplicadaMan.Y + lblInfoAplicadaMan.Height + 9` | icono `Icon.Information` · Visible `VIS_A && !IsBlank(gvarTxMan.sanic_motivorechazocore)` · label ↓ |
| `lblDatosSecMan` (ESTILO-SECCION) | `16` | `BODY + If(!IsBlank(gvarTxMan.sanic_motivorechazocore), cntInfoAplicadaMan.Height + 12, 0)` | `CW` | `16` | `"DATOS"` · Visible `VIS_A` |
| `cntKvAplicadaMan` (Contenedor manual) | `16` | `lblDatosSecMan.Y + lblDatosSecMan.Height + 8` | `CW` | `5 * 24` | Visible `VIS_A` |
| `lblMotivoSecMan` (ESTILO-SECCION) | `16` | `cntKvAplicadaMan.Y + cntKvAplicadaMan.Height + 12` | `CW` | `16` | `"MOTIVO DEL RECHAZO DEL CORE"` · Visible `VIS_A && !IsBlank(gvarTxMan.sanic_motivorechazocore)` |
| `cntWarnMotivoMan` (banner Bnn warn) | `16` | `lblMotivoSecMan.Y + lblMotivoSecMan.Height + 8` | `CW` | `lblWarnMotivoMan.Y + lblWarnMotivoMan.Height + 9` | icono `Icon.Warning` · misma Visible que `lblMotivoSecMan` · label ↓ |

`lblInfoAplicadaMan.Text`: `"Esta transacción fue sacada de un lote por rechazo del core y
aplicada manualmente después."` *(L-D8: no se nombra el nº de lote — no persiste tras salir)*.

`lblWarnMotivoMan.Text`: `Coalesce(gvarTxMan.sanic_motivorechazocore, gvarTxMan.Comentarios)`
*(fallback sin backend)*.

`cntKvAplicadaMan` — 5 filas. Cada fila `N`: `lblKvKNMan` (ESTILO-DATO-K, `X 0`, `Width Parent.Width*0.4`,
Align Left) + `lblKvVNMan` (ESTILO-DATO-V, `X Parent.Width*0.4`, `Width Parent.Width*0.6`, Align Right).
`Y` de la fila `N` = `(N-1) * 24`; `Height 22`.

| Fila | K (`lblKvKNMan.Text`) | V (`lblKvVNMan.Text`) |
|---|---|---|
| 1 | `"Cuenta"` | `If(IsBlank(gvarTxMan.AccountNumber), "— sin cuenta —", Text(gvarTxMan.AccountNumber, "[$-en-US]0"))` |
| 2 | `"Moneda"` | `gvarTxMan.Currency` |
| 3 | `"Total"` | `Text(gvarTxMan.Total, FmtMoney)` |
| 4 | `"Aplicada por"` | `gvarTxMan.'Modified By'.'Full Name'` *(aproximación honesta — no hay sello propio de "aplicada por")* |
| 5 | `"Aplicada"` | `Text(gvarTxMan.'Modified On', FmtFecha)` *(aproximación — ver §8)* |

---

## 6. Nota de referencia y depósito/cliente (columnas y cabecera)

`lblTxRefMan`, `lblDetRefMan`, `lblTxDepMan`, `lblDetSubMan` y la AccessibleLabel de
`btnTxFilaMan` usan la **columna nombre primaria** de la transacción (autonumber tipo
`TRX-0098310`) y el nombre del depósito/cliente. El esquema real no me fue dado con esos
display names exactos, así que:

- **Referencia (identificador de la tx)**: usar la columna nombre primaria del registro. En
  el template `ThisItem.'<NombrePrimario>'`; en la cabecera `gvarTxMan.'<NombrePrimario>'`.
  **Si IntelliSense la nombra distinto, el suyo manda.** (`TransactionReference` es OTRA cosa
  — es el TAB 4 del panel, p. ej. `REF-4480-03`.)
- **Depósito / cliente** — el **orden difiere** entre grilla y cabecera (fiel al mock):
  - **grilla** (`lblTxDepMan`): depósito primero →
    `ThisItem.RelatedDepositRecounting.'<nombre depósito>' & " · " & <cliente>` (ej `DR-4480 · Farmacias Kielsa`).
  - **cabecera de detalle** (`lblDetSubMan`): **cliente primero** →
    `<cliente> & " · " & gvarTxMan.RelatedDepositRecounting.'<nombre depósito>'` (ej `Farmacias Kielsa · DR-4480`) — así se ve en MA1/MA2/MA3.
  El cliente vive en el depósito o en la tx; confirmar el dot-walk en IntelliSense. El
  dot-walk en **display** (no en el predicado de un `Filter`) es legal, pero hace un lookup
  por fila → **candidato a materializar** un espejo `sanic_depositocliente` si el volumen lo
  pide (misma lógica que el espejo de cajero, 00 §3.1).

---

## 7. Honestidad técnica del flujo de teclado (receta 8.7)

- **Nativo y garantizado**: `SetFocus` (foco en Cuenta al seleccionar/aplicar), `TabIndex`
  secuencial (1→5), **Enter** sobre el botón enfocado, y el auto-avance en el `DefaultResult`
  del `IfError`. Nada de esto depende de trucos. Va **directo en la pantalla** (no componente):
  el `SetFocus`/`TabIndex` cruzan mal el borde de un componente (01 §8.7).
- **NO existe en Power Fx**: función de portapapeles. `Copy`/`SetClipboard` no existen. El
  `Ctrl+C` lo hace el **navegador** sobre el `inp…` enfocado; por eso los campos son Entrada
  de texto clásica en `DisplayMode.Edit` (permiten foco/selección/copia) con `OnChange:
  Reset(Self)` para no persistir ediciones.
- **A validar en el vestido**: **auto-seleccionar** el texto al enfocar (para copiar sin
  `Ctrl+A`). Si no sale nativo, el costo es **una tecla extra** (`Ctrl+A`), no rompe el ritmo.

---

## 8. Dependencias de backend (con fallback)

| Dep | Dónde se usa | Fallback sin backend |
|---|---|---|
| `sanic_CHWorkloadByCashier` (agregado por cajero, JSON en `.Workload`) | rail `colCajerosMan` (§1) → `cmpRailMan.Datos` | Agregado local sobre `Users` + `CountRows` (correcto < 500 por cajero), provisorio (§3.2) |
| `sanic_motivorechazocore` (texto en `CH-Tbl-Transactions`) | badge "Rechazo core" (§4.3), orden por Motivo, banner MA2 | Quitar la rama `!IsBlank(sanic_motivorechazocore)` del `Switch`/Sort; el banner MA2 cae a `gvarTxMan.Comentarios` |
| Flag/conteo de cheques materializado (`sanic_tienecheques`) | badge "Con cheques" (§4.3) | Aproximación `!IsBlank(RelatedDepositRecounting)` (tiene depósito) — declarado, no es "tiene cheques" exacto |
| Sello real de aplicación (por/quién) | `kv` MA2 filas 4–5 (§5.4) | `Modified By` / `Modified On` como aproximación honesta |

---

## 9. Divergencias declaradas (ledger local, referencia a `00 §6`)

- **L-D1** — **Orden por Motivo es client-side**. El motivo es derivado (`Switch` sobre
  cuenta/depósito/columna nueva); no hay columna única sortable, así que el `Sort` corre
  **sobre el set ya delegado** por el `Filter` (≤500 por cajero — muy por encima del volumen
  real de manuales de un cajero). El orden por **Fecha** (`'Created On'`) y el de **Aplicadas**
  SÍ delegan. El auto-avance usa siempre `'Created On' Ascending` (determinista).
- **L-D2** — Selección de la **grilla** por `gvarTxMan` en vez de `IsSelected` (necesario por
  el auto-avance). La del **rail** sí es por el componente (`CajeroSel = galCajerosRail.Selected`,
  §3.2) — el cajero nunca se setea programáticamente a mitad de flujo.
- **L-D3** — MA3 sin `kv` redundante (§5.3): banner + "Actualizar lista"; los campos se ocultan.
- **L-D4** — `Refresh('CH-Tbl-Transactions')` **sólo** en "Actualizar lista" (acción explícita
  de recuperación). NO se hace `Refresh` por cada aplicación (ese era P-1).
- **L-D5** — Alineación grilla: encabezado y template comparten **fracción del ancho**
  (`Parent.Width` / `Parent.TemplateWidth`), no `FillPortions`. Los pesos del mock
  (1.1/2/1/0.9/1/1) se traducen a las fracciones 0.03/0.16/0.43/0.57/0.70/0.84; el ajuste
  fino de píxeles se hace en el vestido.
- **L-D6** — **Conteo del segmentado sólo en el activo**. El mock pinta un conteo en los dos
  toggles a la vez. Con `CountRows(galTxMan.AllItems)` sólo es exacto el del segmento cargado;
  se muestra el del activo y se omite el del inactivo — nunca un número stale.
- **L-D7** — **Indicador `🔒 solo copia`**. El mock lo dibuja como sufijo dentro de cada
  campo; una Entrada de texto clásica no admite sufijo inline. La semántica de solo-lectura
  la garantiza `OnChange: Reset(Self)` (§7) y la comunica el `kbHint`; no se reproduce el
  candado dentro del control.
- **L-D8** — **Nº de lote en el banner MA2**. Tras salir del lote la tx ya no conserva ese
  vínculo, así que `lblInfoAplicadaMan` se generaliza a "sacada de un lote" — no se inventa
  un nº que no persiste.
- **L-D9** — **Píldoras de fila = Botón NATIVO** (`pilTxMotivoMan`/`pilTxEstadoMan`), no
  componente: un `CanvasComponent` no se puede anidar en una galería (`PA2122`). Replican el
  estilo de `cmpPildora` §8.3 leyendo de `ThisItem`. `DisplayMode.View` → no delegan
  `Select(Parent)`; la selección de fila la cubre `btnTxFilaMan` (§4.3). La píldora de
  **detalle** (`cmpPilDetEstadoMan`) sí es componente: fuera de galería, ahí es legal.
- **L-D10** — **Panel de detalle sin scroll nativo** (contenedor manual). El contenido cabe
  en el alto de tablet; sólo las **galerías** (`galTxMan`, `galChequesMan`) scrollean solas.
  `galChequesMan` se acota a ≤4 filas visibles. El sentido vertical queda fuera de alcance,
  igual que en el mock.
- **L-D11** — **Orden del rail dentro de `cmpRailCajeros`** (contexto interno del componente,
  §3.2): la pantalla ya no maneja `ctxOrdenRailMan`. El bootstrap siembra el cajero de más
  pendientes para coincidir con el orden por defecto del rail.
- **L-D12** — **Fondos de encabezado por Rectángulo** (`shpGridHeadBgMan`, `shpChqHeadBgMan`):
  en layout manual la card no tiene "fila de header" con `Fill` propio, así que la banda
  `ThemeSurface2` del mock se pinta con un rectángulo detrás de las etiquetas.
- **L-1/L-3** (globales, 00 §6): sin `DropShadow` en Botón clásico (borde `ThemeLine`); sin
  spinner animado durante el `Patch` (el hilo de UI está tomado).
- Los conteos con `CountRows(gal….AllItems)` cuentan **la página cargada**; con volúmenes de
  un cajero es exacto.

---

## 10. Checklist de salida (verificable por el constructor)

- [ ] **Esqueleto 7.0**: `scrManual` = `cmpHeaderMan` (Activo "Manual") + `cntBodyMan` →
      `cmpRailMan` + `cntMainMan`. Cero contenedores de auto-ajuste; todo X/Y/W/H por fórmula
      relativa a `Parent.Width`/`Parent.Height` + `Lay*`.
- [ ] Componentes en su lugar: `cmpHeaderMan`, `cmpRailMan` (badge = `pendientesManual`),
      `cmpPilDetEstadoMan` (detalle), `cmpEmpty*` (vacíos). Las píldoras de FILA
      (`pilTxMotivoMan`/`pilTxEstadoMan`) son **Botones nativos** (no componente — `PA2122`).
      Cero fórmula de layout con número mágico suelto.
- [ ] `App Checker` en cero errores; **cero banda azul de delegación** en `galTxMan` y
      `galChequesMan` (los `Filter` delegan; el `Sort` por Motivo es el único client-side, declarado).
- [ ] Al entrar (`OnVisible`): rail con el primer cajero resaltado, grilla con sus pendientes,
      panel con la más vieja en foco sobre **Cuenta** (borde de acento).
- [ ] Tab recorre Cuenta(1) → Moneda(2) → Monto(3) → Referencia(4) → botón(5); **Enter** en
      el botón aplica.
- [ ] Aplicar: `Patch` de **una** columna (`TransactionStatus = Aplicada`), sin `SubmitForm`,
      sin `Refresh`; toast de éxito; **auto-avance** a la siguiente con foco en Cuenta.
- [ ] Editar un campo y salir revierte el valor (`OnChange: Reset(Self)`) — los datos del
      cliente no se modifican.
- [ ] Simular rechazo del guard (aplicar una tx ya aplicada por otra sesión): banner
      `cntDangerMan` con el mensaje real + "Actualizar lista"; campos y cheques ocultos.
- [ ] Cambiar a **Aplicadas**: lista de aplicadas; el detalle de una que vino de lote muestra
      el banner MA2, el `kv` y el motivo del rechazo del core (sin hueco cuando no vino de lote).
- [ ] Cheques de la transacción: sólo los del depósito de la tx (nunca huérfanos si
      `RelatedDepositRecounting` es Blank); columnas CheckNumber / CheckAmount / CheckCurrency.
- [ ] Ordenar por Motivo (clic en `btnHdrMotivoMan`) reagrupa; el rail ordena por
      "Más pendientes" / "A–Z" dentro del componente.
- [ ] Sin cuenta: la columna y el campo muestran "— sin cuenta —"; la app **no** ofrece capturarla.
- [ ] Responsive: a ~1080 px y ~840 px las cards se acotan (fracciones de `Parent.Width`), la
      grilla comprime columnas y el panel sigue legible (sin scroll horizontal del body).
```
