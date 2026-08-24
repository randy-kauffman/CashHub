# Guía 01 — La app: creación, datos, fórmulas, componentes y convenciones (v2 — layout manual)

Esta guía se hace UNA vez, antes de cualquier pantalla. Las guías 02–05 asumen todo
lo de acá. Fuente de verdad del diseño: `mockup-digitization.html`.

> **Cambio de paradigma respecto a la primera versión.** El layout NO usa contenedores
> de auto-ajuste (`FillPortions`, vertical/horizontal container). Usa **contenedores
> MANUALES** (`groupContainer.manualLayoutContainer`) y **posiciones/tamaños calculados
> con FÓRMULAS RELATIVAS** a `Parent.Width`/`Parent.Height` + constantes. Así el layout
> es fiel al mockup y sigue siendo responsive (las fórmulas se recalculan con el tamaño).
> Y **todo lo repetido es un COMPONENTE** (§8): encabezado, rail de cajeros, píldora,
> KPI, diálogo, estado vacío.

## 0. Contrato de formato de las guías de pantalla (02–05)

1. **Árbol completo**: el árbol del inicio lista TODOS los controles con su nombre final.
2. **Cobertura total**: todo control del árbol se describe abajo (tabla propia, fila
   compartida, o puntero a receta/componente). Árbol ↔ cuerpo 1:1.
3. **Tipo explícito** por control, con estos nombres exactos (menú Insertar):

   | Tipo en la guía | En Studio |
   |---|---|
   | Contenedor (manual) | Diseño → **Contenedor** (`groupContainer.manualLayoutContainer`) |
   | Etiqueta clásica | Clásico → **Etiqueta** (`Label`) |
   | Botón clásico | Clásico → **Botón** (`Button`) |
   | Entrada de texto clásica | Clásico → **Entrada de texto** (`Text input`) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) |
   | Icono | Iconos → el glifo (`Icon.*`) |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** |
   | Componente | Insertar → **Componentes personalizados** → `cmpXxx` (§8) |

   > **Cerrado.** Cero PCF, cero Form/dataCards, cero contenedores de auto-ajuste, cero
   > terceros. El `Contenedor` es SIEMPRE manual (sus hijos se posicionan con X/Y).

4. **Posición y tamaño por fórmula relativa** (§7.0): `X`, `Y`, `Width`, `Height` de cada
   control se expresan con `Parent.Width`/`Parent.Height` y las constantes `Lay*` de §3 —
   **nunca** un número mágico suelto que se pueda desincronizar. Dos controles que
   comparten un borde: uno referencia al otro.
5. **Propiedades**: solo las que cambian del default (§6 fija los defaults y alias).
6. **Nombres únicos en TODA la app** con sufijo de pantalla: `Cor`/`Man`/`Lot`/`Chq`.
   Los controles internos de un componente llevan el sufijo del componente.

## 1. Crear la app

1. make.powerapps.com → DEV → **Apps → Nueva app → página en blanco → Tableta**.
2. **General**: `CH-App-Digitization v2` · ícono `docs/marca/icono-digitization-512.png` · fondo `#C8102E`.
3. **Pantalla**: Orientación **Horizontal**; **Escalar para ajustar: DESACTIVADO**;
   **Bloquear relación de aspecto: DESACTIVADO**; Bloquear orientación: Activado.
   > Con Escalar-para-ajustar OFF, `App.Width`/`App.Height` y `Parent.Width`/`Parent.Height`
   > reflejan el tamaño real — las fórmulas relativas responden de verdad.
4. Agregala a la solución **CashHubApps**.

## 2. Orígenes de datos

| Tabla (display) | Para qué |
|---|---|
| CH-Tbl-Transactions | Transacciones (corte, manual, del lote) |
| CH-Tbl-TransactionBatches | Lotes |
| CH-Tbl-Checks | Cheques |
| CH-Tbl-DepositsRecounting | Puente tx ↔ cheques |
| Users | `'Full Name'` / `'Primary Key'` de cajeros |
| **Environment** | Custom APIs directas: `sanic_CHWorkloadByCashier`, `sanic_CHPreviewTransactionCut`, `sanic_CHRemoveFromCut` |

**Invocación de Custom APIs (Environment)** — toman un **record de parámetros**, no
posicionales: `Environment.sanic_CHPreviewTransactionCut({CajeroId: <guid>})`,
`Environment.sanic_CHRemoveFromCut({TransactionId: <guid>, Motivo: <text>})`,
`Environment.sanic_CHWorkloadByCashier()` (sin params). Flujos con `.Run()`:
`'CH-Flow-CreateCut'.Run(<guid>)`, `'CH-Flow-DownloadData'.Run(<guid>, <name>)`.

## 3. App.Formulas

Seleccioná **App** → **Formulas** → pegá TODO:

```powerfx
// ===================== PALETA (RGBA exactos, gemela de Reception/Recounting v2) =====================
ThemeAccent      = RGBA(200, 16, 46, 1);      // #C8102E
ThemeAccentDark  = RGBA(164, 13, 38, 1);      // #A40D26
ThemeAccentSoft  = RGBA(251, 235, 237, 1);    // #FBEBED
ThemeBg          = RGBA(246, 245, 244, 1);    // #F6F5F4
ThemeSurface     = RGBA(255, 255, 255, 1);    // #FFFFFF
ThemeSurface2    = RGBA(250, 248, 247, 1);    // #FAF8F7
ThemeInk         = RGBA(32, 28, 29, 1);       // #201C1D
ThemeInk2        = RGBA(102, 89, 92, 1);      // #66595C
ThemeInk3        = RGBA(148, 138, 140, 1);    // #948A8C
ThemeLine        = RGBA(226, 220, 221, 1);    // #E2DCDD
ThemeLineSoft    = RGBA(237, 232, 233, 1);    // #EDE8E9
ThemeOk          = RGBA(14, 122, 60, 1);      ThemeOkSoft     = RGBA(226, 242, 232, 1);
ThemeInfo        = RGBA(35, 85, 125, 1);      ThemeInfoSoft   = RGBA(228, 238, 246, 1);
ThemeWarn        = RGBA(154, 91, 0, 1);       ThemeWarnSoft   = RGBA(251, 240, 220, 1);
ThemeDanger      = RGBA(179, 38, 30, 1);      ThemeDangerSoft = RGBA(252, 234, 232, 1);
ThemeDangerLine  = RGBA(231, 185, 181, 1);
ThemeOverlay     = RGBA(40, 30, 32, 0.45);
// ===================== CONSTANTES DE LAYOUT (el sistema de posicionamiento) =====================
LayHeaderH = 60;      // alto del encabezado
LayRailW   = 230;     // ancho del rail de cajeros
LayGutter  = 18;      // margen lateral / respiro entre zonas
LayGap     = 14;      // separación entre cards
LayCardR   = 13;      // radio de card
LayRowH    = 44;      // alto de fila de galería
LayKpiH    = 74;      // alto de KPI card
// ===================== FORMATOS =====================
FmtMoney = "#,##0.00";   FmtFecha = "dd/mm/yyyy HH:mm";   FmtHora = "HH:mm";
SimboloNIO = "C$";  SimboloUSD = "US$";  SimboloEUR = "€";
// ===================== IDENTIDAD =====================
UsuarioNombre = User().FullName;
UsuarioIniciales = Left(User().FullName,1) & If(CountRows(Split(User().FullName," "))>1, Left(Last(Split(User().FullName," ")).Value,1), "");
```

Tipografía: **`Font.'Open Sans'`** explícita en cada control; `Size` en puntos.

## 4. App.OnError

```powerfx
Trace($"[Digitization] {FirstError.Message} | {FirstError.Kind}", TraceSeverity.Error);
Notify("Ocurrió un error inesperado. Quedó registrado para soporte.", NotificationType.Error)
```
**OnStart VACÍO.**

## 5. Convenciones de nombres

`scr` pantalla · `cnt` Contenedor manual · `gal` galería · `lbl` etiqueta · `btn` botón ·
`pil` píldora · `inp` entrada de texto · `shp` rectángulo · `ico` icono · `cmp` instancia de
componente. Variables `gvar`/`ctx`/`col`. Sufijo de pantalla `Cor/Man/Lot/Chq` (o del
componente en sus hijos). Cero nombres autogenerados.

## 6. Defaults tipográficos y alias

Etiquetas: `Font.'Open Sans'`, Size `9`, Color `ThemeInk`, Padding `0`, `Wrap false`,
`VerticalAlign.Middle`. Alias: ESTILO-TITULO (14.25 Bold) · ESTILO-APPTITULO (10.5 Semibold) ·
ESTILO-APPSUB (8.25 Ink3) · ESTILO-SECCION (7.88 Semibold Ink3 MAYÚSC) · ESTILO-KPI-LAB
(7.88 Bold Ink3 MAYÚSC) · ESTILO-KPI-VAL (16.5 Bold) · ESTILO-HEADER-COL (7.5 Bold Ink2
MAYÚSC) · ESTILO-CELDA (9) · ESTILO-CELDA-NUM (9 Right) · ESTILO-DATO-K (9 Ink3) ·
ESTILO-DATO-V (9 Semibold Right) · ESTILO-VACIO (9.38 Ink3 Center Wrap).

Botones (Open Sans, Size 9, Semibold, Radius 8, Height 32; DisabledFill `ThemeLineSoft`,
DisabledColor `ThemeInk3`, DisabledBorderColor `Transparent`):
ESTILO-BTN-PRIMARIO (Fill Accent, Color Surface, Hover AccentDark) · ESTILO-BTN-CONTORNO
(Fill Surface, Color Accent, Border Accent 1, Hover AccentSoft) · ESTILO-BTN-FANTASMA (Fill
Transparent, Color Ink2, Border Line 1, Hover Surface2) · ESTILO-BTN-PELIGRO (Fill Danger,
Color Surface) · ESTILO-BTN-CHICO (Fill Surface, Color Ink2, Border Line 1, Size 8.25,
Height 26, Radius 7).

## 7. Recetas de posicionamiento (el paradigma manual)

### 7.0 El sistema — contenedores manuales + fórmulas relativas

La pantalla se arma con **Contenedores manuales** anidados; cada control se posiciona con
`X`/`Y`/`Width`/`Height` en función de `Parent.Width`/`Parent.Height` y las constantes
`Lay*`. El esqueleto de TODAS las pantallas:

```
scrX  (Fill: ThemeBg)
├── cmpHeaderX          Componente (receta §8.1) · X 0 · Y 0 · Width Parent.Width · Height LayHeaderH
├── cntBodyX            Contenedor manual · X 0 · Y LayHeaderH · Width Parent.Width · Height Parent.Height - LayHeaderH
│   ├── cmpRailX        Componente rail (§8.2) · X 0 · Y 0 · Width LayRailW · Height Parent.Height
│   └── cntMainX        Contenedor manual · X LayRailW · Y 0 · Width Parent.Width - LayRailW · Height Parent.Height
│       └── (cards, KPIs, grillas — posicionadas con Parent.Width/Height del cntMainX)
└── cmpDlg…X            Componente diálogo (§8.5) · X 0 · Y 0 · Width Parent.Width · Height Parent.Height (overlay a pantalla completa; fuera de cntBody)
```

**Reglas de oro:**
- Un control dentro de un Contenedor usa `Parent.Width`/`Parent.Height` = los del contenedor.
  Anidar contenedores es cómo se acota el marco de referencia.
- Fracciones: `Width: =Parent.Width * 0.62`. Sumas: `X: =otroCtrl.X + otroCtrl.Width + LayGap`.
- Constantes `Lay*` para todo número que se repita; nunca dos literales que se contradigan.
- scrCheques **no** tiene rail: `cntMainChq` ocupa `X 0 · Width Parent.Width` dentro de `cntBodyChq`.

### 7.1 Card (superficie blanca con radio)

**Contenedor manual** (`cnt…Card`): Fill `ThemeSurface`, BorderColor `ThemeLine`,
BorderThickness `1`, **RadiusTop/Bottom Left/Right = LayCardR**. Los hijos se posicionan con
X/Y relativos a `Parent` (la card). Padding interno con constantes (X del primer hijo = 16).

> El Contenedor es la única superficie con Radius + hijos posicionables. El Rectángulo queda
> para líneas de 1 px y la barra de selección de 3 px.

### 7.2 Grilla = encabezado + galería (delegable)

Fila de labels `ESTILO-HEADER-COL` (posición X por fracción del ancho de la card) +
`shp…Linea` (Rectángulo H 1, Fill `ThemeLine`) + **Galería vertical en blanco** debajo
(`X`/`Width` = los de la card menos el padding; `Height` = `Parent.Height - <lo de arriba>`;
`TemplateSize LayRowH`). Dentro del template, cada celda posiciona su `X`/`Width` como la
**misma fracción** que su encabezado (X del template = X del encabezado − X de la galería).

Filtros SIEMPRE delegables: `Filter(t, Lookup.'Primary Key' = gvar.<guid>)` (igualdad de
GUID → `_x_value eq <guid>`). Nunca `Upper()`, dot-walk en el predicado, ni comparar
records con `=` (da "Record, Record" — comparar por `'Primary Key'`). Overlay de fila:
`btn…Fila` (Botón, X 0 Y 0 Width galería Height LayRowH, Fill Transparent, al fondo,
`OnSelect Select(Parent)`); los labels delegan con `Select(Parent)`.

### 7.3 Fila seleccionada

`shp…Sel` (Rectángulo primero en el template): Fill `If(ThisItem.IsSelected, ThemeAccentSoft,
Color.Transparent)`. `shp…SelBarra` (Rectángulo X 0 Y 0 W 3 H LayRowH Fill `ThemeAccent`
Visible `ThisItem.IsSelected`). 🧨 Las galerías clásicas AUTO-SELECCIONAN la 1ª fila.

## 8. Componentes reutilizables (se crean ANTES de las pantallas)

Cada uno es un **Componente canvas** (Árbol → Componentes → Nuevo componente). Reglas para
todos:
- **`AllowAccessToGlobals = true`** (leen los tokens `Theme*` y las constantes `Lay*`).
- **Cero datasources, cero `gvar`/`col` adentro** — los datos entran/salen por Custom
  Properties. El estado interno (orden del rail) usa `UpdateContext` en el scope del componente.
- **`Width`/`Height` NO se fijan dentro del componente**: son propiedades de la instancia, se
  dan al insertarlo en la pantalla (con fórmula relativa). Dentro, `Parent.Width`/`Parent.Height`
  = el tamaño de la instancia.
- Todo control interno lleva el sufijo del componente (`Hdr`, `Rail`, `Pil`, `Kpi`, `Dlg`, `Emp`).
- El contrato de formato (§0) aplica igual: árbol completo, tipo explícito, tabla de propiedades
  por control.

Se crean SEIS: `cmpHeader`, `cmpRailCajeros`, `cmpPildora`, `cmpKpi`, `cmpDialogo`, `cmpEmpty`.

---

### 8.1 `cmpHeader` — encabezado (las 4 pantallas)

**Custom Properties**
| Nombre | Tipo | Dir | Default | Uso |
|---|---|---|---|---|
| `Titulo` | Texto | Entrada | `"Digitación"` | título grande |
| `Subtitulo` | Texto | Entrada | `"Corte de transacciones"` | bajo el título |
| `Activo` | Texto | Entrada | `"Corte"` | pestaña resaltada: `"Corte"`/`"Manual"`/`"Lotes"`/`"Cheques"` |
| `OnCorte` | Comportamiento | Entrada | — | la pantalla cablea `Navigate(scrCorte)` |
| `OnManual` | Comportamiento | Entrada | — | `Navigate(scrManual)` |
| `OnLotes` | Comportamiento | Entrada | — | `Navigate(scrLotes)` |
| `OnCheques` | Comportamiento | Entrada | — | `Navigate(scrCheques)` |

**Árbol de controles**
```
cmpHeader
├── shpFondoHdr        Rectángulo clásico       banda blanca
├── shpLineaHdr        Rectángulo clásico       línea inferior 1 px
├── btnMarcaHdr        Botón clásico            tile "C$"
├── lblTituloHdr       Etiqueta clásica         título
├── lblSubHdr          Etiqueta clásica         subtítulo
├── cntNavHdr          Contenedor (manual)      pista del segmentado
│   ├── btnNavCorteHdr     Botón clásico
│   ├── btnNavManualHdr    Botón clásico
│   ├── btnNavLotesHdr     Botón clásico
│   └── btnNavChequesHdr   Botón clásico
└── cntUserHdr         Contenedor (manual)      chip de usuario
    ├── btnAvatarHdr       Botón clásico        círculo con iniciales
    └── lblUsuarioHdr      Etiqueta clásica     nombre
```

**Propiedades** (`Parent` = la instancia; `Height` de la instancia = `LayHeaderH`)
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpFondoHdr` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeSurface` · BorderThickness `0` |
| `shpLineaHdr` | Rectángulo | `0` | `Parent.Height-1` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `btnMarcaHdr` | Botón | `LayGutter` | `(Parent.Height-34)/2` | `34` | `34` | Radius `9` · Fill `ThemeAccent` · Color `ThemeSurface` · Text `"C$"` · Size `11` · Bold · DisplayMode `View` · BorderThickness `0` |
| `lblTituloHdr` | Etiqueta | `btnMarcaHdr.X + btnMarcaHdr.Width + 12` | `11` | `160` | `20` | Text `cmpHeader.Titulo` · ESTILO-APPTITULO |
| `lblSubHdr` | Etiqueta | `lblTituloHdr.X` | `lblTituloHdr.Y + 18` | `240` | `16` | Text `cmpHeader.Subtitulo` · ESTILO-APPSUB |
| `cntNavHdr` | Contenedor | `lblTituloHdr.X + 190` | `(Parent.Height-32)/2` | `360` | `32` | Fill `ThemeSurface2` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `16` |
| `cntUserHdr` | Contenedor | `Parent.Width - LayGutter - Self.Width` | `(Parent.Height-34)/2` | `lblUsuarioHdr.X + lblUsuarioHdr.Width + 12` | `34` | Fill `ThemeSurface2` · Border `ThemeLine` `1` · Radius `17` |

**Los 4 botones de nav** (hijos de `cntNavHdr`; `Parent` = `cntNavHdr`): Height `26`, Y `3`,
Width `(Parent.Width - 6)/4`, Radius `13`, BorderThickness `0`, Size `9`. X encadenado:
`btnNavCorteHdr.X = 3`; los demás `X = <anterior>.X + <anterior>.Width`. Cada uno, con su
`<Tab>` = `"Corte"`/`"Manual"`/`"Lotes"`/`"Cheques"` y su `<OnX>` = `cmpHeader.OnCorte`/etc.:
| Prop | Fórmula |
|---|---|
| Text | `"<Tab>"` |
| Fill | `If(cmpHeader.Activo = "<Tab>", ThemeSurface, Color.Transparent)` |
| Color | `If(cmpHeader.Activo = "<Tab>", ThemeAccent, ThemeInk2)` |
| FontWeight | `If(cmpHeader.Activo = "<Tab>", FontWeight.Semibold, FontWeight.Normal)` |
| HoverFill | `If(cmpHeader.Activo = "<Tab>", ThemeSurface, ThemeSurface2)` |
| OnSelect | `cmpHeader.<OnX>` |

**Chip de usuario** (hijos de `cntUserHdr`; `Parent` = `cntUserHdr`):
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnAvatarHdr` | Botón | `4` | `4` | `26` | `26` | Radius `13` · Fill `ThemeAccentSoft` · Color `ThemeAccent` · Text `UsuarioIniciales` · Size `7.88` · Bold · DisplayMode `View` · BorderThickness `0` |
| `lblUsuarioHdr` | Etiqueta | `btnAvatarHdr.X + btnAvatarHdr.Width + 8` | `0` | `Min(160, ...)` | `Parent.Height` | Text `UsuarioNombre` · Size `8.63` · Color `ThemeInk2` |

> 🧨 Nav por **behavior properties** (un componente no puede `Navigate` a pantallas externas).
> Cada pantalla instancia `cmpHeaderX`, fija `Activo: ="Corte"` según la propia y cablea
> `OnCorte: =Navigate(scrCorte, ScreenTransition.None)`, etc.

---

### 8.2 `cmpRailCajeros` — rail de cajeros (Corte / Manual / Lotes)

**Custom Properties**
| Nombre | Tipo | Dir | Uso |
|---|---|---|---|
| `Titulo` | Texto | Entrada | `"CAJEROS CON CORTE PENDIENTE"` (cada pantalla el suyo) |
| `Datos` | Tabla | Entrada | la colección `colCajerosX` (filas con `cajeroNombre`, `mesa`, `badge`, `cajero`) |
| `CajeroSel` | Registro | **Salida** | `galCajerosRail.Selected` — la pantalla la lee |
| `OnSeleccion` | Comportamiento | Entrada | la pantalla hace `Set(gvarCajeroX, cmpRailX.CajeroSel)` |

**Árbol de controles**
```
cmpRailCajeros
├── shpFondoRail       Rectángulo clásico       fondo blanco del rail
├── shpBordeRail       Rectángulo clásico       línea derecha 1 px
├── lblTituloRail      Etiqueta clásica         título de sección
├── cntOrdenRail       Contenedor (manual)      botones de orden
│   ├── btnOrdCantRail    Botón clásico         "Más pendientes"
│   └── btnOrdAZRail      Botón clásico         "A–Z"
└── galCajerosRail     Galería vertical en blanco
    ├── shpSelRail        Rectángulo clásico    fondo fila seleccionada (receta 7.3)
    ├── shpSelBarraRail   Rectángulo clásico    barra de acento (receta 7.3)
    ├── btnFilaRail       Botón clásico         overlay clicable (al fondo del z-order)
    ├── lblNombreRail     Etiqueta clásica      cajeroNombre
    ├── lblMesaRail       Etiqueta clásica      mesa
    └── pilBadgeRail      Botón clásico         píldora con el conteo
```

**Propiedades** (`Parent` = la instancia; estado de orden en `ctxOrdenRail`, `OnVisible`
del componente no existe → seteá el default en la propia galería con `Coalesce`)
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpFondoRail` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeSurface` · Border 0 |
| `shpBordeRail` | Rectángulo | `Parent.Width-1` | `0` | `1` | `Parent.Height` | Fill `ThemeLine` |
| `lblTituloRail` | Etiqueta | `13` | `11` | `Parent.Width-26` | `16` | Text `cmpRailCajeros.Titulo` · ESTILO-SECCION |
| `cntOrdenRail` | Contenedor | `13` | `lblTituloRail.Y + lblTituloRail.Height + 8` | `Parent.Width-26` | `26` | Fill transparente |
| `btnOrdCantRail` | Botón | `0` | `0` | `(Parent.Width-6)/2` | `26` | ESTILO-BTN-CHICO · Text `"Más pendientes"` · estado activo por `ctxOrdenRail="cant"` · OnSelect `UpdateContext({ctxOrdenRail:"cant"})` |
| `btnOrdAZRail` | Botón | `btnOrdCantRail.X + btnOrdCantRail.Width + 6` | `0` | `btnOrdCantRail.Width` | `26` | ESTILO-BTN-CHICO · Text `"A–Z"` · OnSelect `UpdateContext({ctxOrdenRail:"az"})` |
| `galCajerosRail` | Galería vertical | `0` | `cntOrdenRail.Y + cntOrdenRail.Height + 8` | `Parent.Width` | `Parent.Height - Self.Y` | TemplateSize `52` · TemplatePadding `0` · Items `Sort(cmpRailCajeros.Datos, If(Coalesce(ctxOrdenRail,"cant")="cant", badge, cajeroNombre), If(Coalesce(ctxOrdenRail,"cant")="cant", SortOrder.Descending, SortOrder.Ascending))` |

**Template de `galCajerosRail`** (X relativo a la galería; `Parent.TemplateHeight` = 52):
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpSelRail` | Rectángulo | `0` | `0` | `Parent.TemplateWidth` | `Parent.TemplateHeight` | Fill `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` · Border 0 |
| `shpSelBarraRail` | Rectángulo | `0` | `0` | `3` | `Parent.TemplateHeight` | Fill `ThemeAccent` · Visible `ThisItem.IsSelected` |
| `btnFilaRail` | Botón | `0` | `0` | `Parent.TemplateWidth` | `Parent.TemplateHeight` | Fill `Color.Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness 0 · Text `""` · OnSelect `Select(Parent); cmpRailCajeros.OnSeleccion` |
| `lblNombreRail` | Etiqueta | `13` | `9` | `Parent.TemplateWidth - 70` | `18` | Text `ThisItem.cajeroNombre` · Size `9` · Semibold · OnSelect `Select(Parent)` |
| `lblMesaRail` | Etiqueta | `13` | `28` | `Parent.TemplateWidth - 70` | `14` | Text `ThisItem.mesa` · ESTILO-DATO-K · OnSelect `Select(Parent)` |
| `pilBadgeRail` | Botón | `Parent.TemplateWidth - 52` | `15` | `40` | `22` | Radius `12` · Fill `ThemeAccentSoft` · Color `ThemeAccent` · Text `Text(ThisItem.badge)` · Size `9` · Semibold · DisplayMode `View` · BorderThickness 0 |

> `CajeroSel` (Output) = `galCajerosRail.Selected`. `OnSeleccion` avisa a la pantalla el cambio.

---

### 8.3 `cmpPildora` — píldora de estado

**Custom Properties**
| Nombre | Tipo | Dir | Uso |
|---|---|---|---|
| `Texto` | Texto | Entrada | la etiqueta ("Generado", "Con cheques"…) |
| `Tono` | Texto | Entrada | `"ok"` / `"warn"` / `"danger"` / `"info"` / `"neutro"` |

**Árbol de controles**
```
cmpPildora
└── btnPilPil          Botón clásico
```
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnPilPil` | Botón | `0` | `0` | `Parent.Width` | `Parent.Height` | DisplayMode `View` · Radius `12` · Size `7.88` · Semibold · Text `cmpPildora.Texto` · Fill `Switch(cmpPildora.Tono, "ok",ThemeOkSoft, "warn",ThemeWarnSoft, "danger",ThemeDangerSoft, "info",ThemeInfoSoft, ThemeLineSoft)` · Color `Switch(cmpPildora.Tono, "ok",ThemeOk, "warn",ThemeWarn, "danger",ThemeDanger, "info",ThemeInfo, ThemeInk3)` · BorderThickness `If(cmpPildora.Tono="neutro",1,0)` · BorderColor `ThemeLineSoft` |

> Al instanciar, `Height` = `22` y `Width` = `40 + 7*Len(<texto>)` (o ancho fijo por columna).

> ⚠️ **NO usar `cmpPildora` dentro de una galería.** Power Apps rechaza cualquier
> `CanvasComponent` como hijo/descendiente de una `Gallery` (`PA2122`): el scope de fila
> (`ThisItem`) y el scope aislado de propiedades custom del componente no se anidan. En filas
> de galería la píldora se arma como **Botón clásico nativo** replicando estas mismas props
> pero leyendo de `ThisItem` (Fill/Color con `Switch`/`With` en vez de la entrada `Tono`).
> Molde canónico: `pilLoteEstadoLot` (04 §…), `pilTxMotivoMan`/`pilTxEstadoMan` (03 §4.3),
> `pilFilaEstadoChq` (05). `cmpPildora` queda para píldoras de **panel de detalle** (fuera de
> galería), donde sí es legal.

---

### 8.4 `cmpKpi` — KPI card por moneda

**Custom Properties**
| Nombre | Tipo | Dir | Uso |
|---|---|---|---|
| `Etiqueta` | Texto | Entrada | `"TOTAL C$"` |
| `Valor` | Texto | Entrada | ya formateado (`SimboloNIO & " " & Text(...,FmtMoney)`) |

**Árbol de controles**
```
cmpKpi
└── cntFondoKpi        Contenedor (manual)      superficie de la card
    ├── lblLabKpi          Etiqueta clásica     etiqueta MAYÚSC
    └── lblValKpi          Etiqueta clásica     valor grande
```
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntFondoKpi` | Contenedor | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `11` |
| `lblLabKpi` | Etiqueta | `15` | `10` | `Parent.Width-30` | `14` | Text `cmpKpi.Etiqueta` · ESTILO-KPI-LAB |
| `lblValKpi` | Etiqueta | `15` | `32` | `Parent.Width-30` | `26` | Text `cmpKpi.Valor` · ESTILO-KPI-VAL |

---

### 8.5 `cmpDialogo` — diálogo modal (solo texto + botones)

**Custom Properties**
| Nombre | Tipo | Dir | Default | Uso |
|---|---|---|---|---|
| `Mostrar` | Booleano | Entrada | `false` | visibilidad del overlay |
| `Titulo` | Texto | Entrada | — | título |
| `Texto` | Texto | Entrada | — | cuerpo |
| `TextoOk` | Texto | Entrada | `"Confirmar"` | botón OK |
| `TextoCancelar` | Texto | Entrada | `"Cancelar"` | botón cancelar |
| `Tono` | Texto | Entrada | `"info"` | `"info"`/`"warn"`/`"danger"`/`"ok"` (icono + color del OK) |
| `AnchoCard` | Número | Entrada | `400` | ancho de la card |
| `OkHabilitado` | Booleano | Entrada | `true` | anti-doble-click / validación |
| `OnOk` | Comportamiento | Entrada | — | acción del confirmar |
| `OnCancelar` | Comportamiento | Entrada | — | acción del cancelar (y de la ✕) |

**Árbol de controles**
```
cmpDialogo
└── cntFondoDlg        Contenedor (manual)      velo full-screen
    ├── btnBloqueoDlg      Botón clásico         se come el clic del fondo (modalidad)
    └── cntCardDlg         Contenedor (manual)   card centrada
        ├── icoTonoDlg        Icono               glifo por tono
        ├── lblTitDlg         Etiqueta clásica    título
        ├── lblTxtDlg         Etiqueta clásica    cuerpo
        ├── icoXDlg           Icono (Icon.Cancel) cerrar (= cancelar)
        ├── btnCancelDlg      Botón clásico       cancelar
        └── btnOkDlg          Botón clásico       confirmar
```
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntFondoDlg` | Contenedor | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · Visible `cmpDialogo.Mostrar` |
| `btnBloqueoDlg` | Botón | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill/Hover/Pressed `Color.Transparent` · Text `""` · **OnSelect `false`** · TabIndex `-1` |
| `cntCardDlg` | Contenedor | `(Parent.Width - cmpDialogo.AnchoCard)/2` | `(Parent.Height - Self.Height)/2` | `cmpDialogo.AnchoCard` | `lblTxtDlg.Y + lblTxtDlg.Height + 66` | Fill `ThemeSurface` · Radius `13` · Border `ThemeLine` `1` · DropShadow `DropShadow.Regular` |
| `icoTonoDlg` | Icono | `22` | `20` | `34` | `34` | Icon `Switch(cmpDialogo.Tono,"danger",Icon.Warning,"warn",Icon.Warning,"ok",Icon.CheckBadge,Icon.Information)` · Color por tono |
| `lblTitDlg` | Etiqueta | `icoTonoDlg.X + icoTonoDlg.Width + 12` | `22` | `Parent.Width - icoTonoDlg.Width - 76` | `24` | Text `cmpDialogo.Titulo` · Size `10.88` · Bold |
| `lblTxtDlg` | Etiqueta | `lblTitDlg.X` | `lblTitDlg.Y + lblTitDlg.Height + 4` | `lblTitDlg.Width` | *(AutoHeight)* | Text `cmpDialogo.Texto` · Size `9` · Color `ThemeInk2` · **AutoHeight `true`** · Wrap `true` |
| `icoXDlg` | Icono | `Parent.Width - 30` | `22` | `18` | `18` | Icon `Icon.Cancel` · Color `ThemeInk3` · HoverColor `ThemeInk` · OnSelect `cmpDialogo.OnCancelar` |
| `btnCancelDlg` | Botón | `btnOkDlg.X - 8 - Self.Width` | `Parent.Height - 50` | `110` | `32` | ESTILO-BTN-FANTASMA · Text `cmpDialogo.TextoCancelar` · OnSelect `cmpDialogo.OnCancelar` |
| `btnOkDlg` | Botón | `Parent.Width - 22 - Self.Width` | `Parent.Height - 50` | `130` | `32` | Text `cmpDialogo.TextoOk` · Fill `If(cmpDialogo.Tono="danger", ThemeDanger, ThemeAccent)` · Color `ThemeSurface` · DisplayMode `If(cmpDialogo.OkHabilitado, DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `cmpDialogo.OnOk` |

> 🧨 Diálogos con **contenido rico** (textarea de motivo en scrLotes, radios) NO se
> componentizan (un componente no recibe hijos): van con la receta 7.2 DIRECTA en la pantalla.
> `cmpDialogo` cubre los de solo texto+botones (confirmar corte, Ingresado, Aplicado, recibir).

---

### 8.6 `cmpEmpty` — estado vacío

**Custom Properties**
| Nombre | Tipo | Dir | Uso |
|---|---|---|---|
| `Glifo` | Texto | Entrada | `"⛁"` / `"✓"` |
| `Mensaje` | Texto | Entrada | el texto que orienta qué hacer |

**Árbol de controles**
```
cmpEmpty
├── lblGlifoEmpty      Etiqueta clásica         glifo grande
└── lblMsgEmpty        Etiqueta clásica         mensaje
```
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblGlifoEmpty` | Etiqueta | `0` | `Parent.Height/2 - 44` | `Parent.Width` | `36` | Text `cmpEmpty.Glifo` · Size `30` · Color `ThemeInk3` · Align `Center` |
| `lblMsgEmpty` | Etiqueta | `Parent.Width*0.15` | `lblGlifoEmpty.Y + lblGlifoEmpty.Height + 8` | `Parent.Width*0.7` | `48` | Text `cmpEmpty.Mensaje` · ESTILO-VACIO |

---

### 8.7 Campo copiable keyboard-first (scrManual) — RECETA, NO componente

El `SetFocus`/`TabIndex` cruza el borde de un componente de forma frágil, así que el panel de
captura de scrManual va **directo en la pantalla**: cada campo es una **Entrada de texto
clásica** (`inp…Man`) con `Default` = el valor de `gvarTxMan` (solo lectura), `TabIndex`
secuencial `1..N`, `OnChange: =Reset(Self)` (no persiste cambios), Fill `ThemeSurface2`. El
botón "Marcar como aplicada" con el `TabIndex` siguiente. Detalle en 03.
> Power Fx no tiene portapapeles; `Ctrl+C` es del navegador. Foco/Tab/Enter nativos.

## 9. Orden de construcción

1. Esta guía (app + datos + Formulas + OnError).
2. **Componentes PRIMERO** (§8): `cmpHeader`, `cmpRailCajeros`, `cmpPildora`, `cmpKpi`,
   `cmpDialogo`, `cmpEmpty`. Se prueban aislados antes de usarlos.
3. Crear las 4 pantallas vacías (Fill `ThemeBg`) para que los `Navigate` compilen.
4. Llenar en orden: `02` scrCorte → `03` scrManual → `04` scrLotes → `05` scrCheques.
   Construcción en DOS PASES por pantalla: insertar+renombrar el árbol, luego llenar fórmulas.
5. App Checker en cero errores (delegación incluida) + prueba de humo.
