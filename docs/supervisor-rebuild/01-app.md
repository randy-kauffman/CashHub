# Guía 01 — La app: creación, datos, fórmulas globales y convenciones

Se hace UNA vez, antes de cualquier pantalla. Las guías 02–05 asumen todo lo de
acá. Paleta y layout salen del prototipo aprobado; el modelo manda sobre el mockup.

> **Verificación de plataforma**: las propiedades citadas se confirmaron contra los
> `pkgs/*.xml` de plantilla (`rectangle_2.3.0`, `button_2.2.0`, `groupContainer_1.5.0`,
> `datepicker_2.6.0`, `icon_2.5.0`, `label_2.5.1`). Donde algo depende del Studio del
> día se marca *a verificar en Studio*.

## 0. Contrato de formato de las guías de pantalla (02–05)

Una violación es un bug de la guía, no licencia creativa:

1. **Árbol completo**: la guía abre con el árbol de TODOS los controles con su
   nombre final.
2. **Cobertura total**: todo control del árbol se describe abajo — tabla propia,
   fila en tabla compartida, o puntero a una receta (§7.x) que fija TODAS sus
   propiedades. Árbol ↔ cuerpo 1:1. Una receta con placeholders (`<seleccionable>`)
   sin resolver NO cuenta como cobertura.
3. **Tipo explícito** por control, con estos nombres exactos (menú Insertar):

   | Tipo en la guía | En Studio (Insertar) |
   |---|---|
   | Etiqueta clásica | Presentación → **Etiqueta de texto** (`Label`) |
   | Botón clásico | Clásico → **Botón** (`Button`) |
   | Entrada de texto clásica | Clásico → **Entrada de texto** (`Text input`) |
   | Menú desplegable clásico | Clásico → **Menú desplegable** (`Drop down`) |
   | Selector de fecha clásico | Clásico → **Selector de fecha** (`Date Picker`) |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** |
   | Icono | Iconos → **(nombre del icono)** |
   | Círculo clásico | Formas → **Círculo** (`Circle`) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) — SIN radio, solo líneas/separadores |
   | Contenedor manual | Diseño → **Contenedor** (contenedor de diseño **manual**) |
   | Contenedor horizontal | Diseño → **Contenedor de diseño horizontal** (auto-layout) |
   | Contenedor vertical | Diseño → **Contenedor de diseño vertical** (auto-layout) |
   | Selector de fecha moderno | Moderno → **Selector de fecha** (`Date picker`) — excepción justificada, ver regla |
   | Componente de la app | Árbol → Componentes → **(nombre)** — definiciones en §7.5 (`cmpNav`) y §7.5b (`cmpKpi`) |

   > **Regla de preferencia de controles**: CLÁSICO por defecto — los modernos son
   > menos robustos y su theming/versionado ya nos costó caro. Un control moderno
   > entra SOLO si es claramente superior al clásico Y la guía lo justifica en el
   > lugar de uso. Excepción ya justificada: **Selector de fecha moderno** (el
   > clásico tiene UX pobre de año/navegación). Siguen PROHIBIDOS sin excepción los
   > de dataset y los de terceros: `Table` (OneGrid), `Combobox`/`Dropdown` pcf,
   > `Tab list`, `Form` + dataCards, `DVFluentDetailsList` (PowerCAT).
   > **Los contenedores de auto-layout/manual y los componentes de la app son
   > nativos, no controles de código** — entran sin violar la regla.

4. **Propiedades**: solo las que cambian del default; los defaults tipográficos y
   los alias ESTILO-* viven en §6.
5. **Nombres únicos en TODA la app** (regla 0.5): todo patrón repetido entre
   pantallas lleva sufijo de la suya: **`Res`** (scrResumen), **`Apr`**
   (scrAprobaciones), **`Tula`** (scrTulas), **`Chq`** (scrCheques).
6. **Cada guía de pantalla abre con un CONTRATO DE ENTRADA** (qué `gvar`/`col`
   deben venir seteadas al entrar y quién las setea) **y cierra con un CHECKLIST DE
   SALIDA** verificable por el constructor. Sin esto R2 no tiene contra qué verificar.

## 1. Crear la app

1. make.powerapps.com → entorno DEV → **Apps → Nueva app → página en blanco →
   Tamaño: Tableta**.
2. **Configuración → General**: Nombre `CH-App-Supervisor v2`. Ícono: subí
   `docs/marca/icono-supervisor-512.png` (balanza sobre verde-tesorería; fuente
   `icono-supervisor.svg` + `generar-icono-supervisor.py`) · Color de fondo `#0F7A5A`.
3. **Configuración → Pantalla y diseño**: Orientación **Horizontal**, Tamaño
   **1366 × 768**, **Escalar para ajustar: DESACTIVADO**.
   **Configuración → General → Límite de filas de datos: 2000** (default 500 — la
   app vieja lo dejó en 500 y varios truncamientos venían de ahí; las guías igual
   diseñan las consultas para no depender del tope).
   > Al desactivar Escalar-para-ajustar, Studio deshabilita (gris) "Bloquear
   > relación de aspecto" y "Bloquear orientación" — es lo esperado. Escalar-para-
   > ajustar OFF es lo que hace que `Width`/`Height` reflejen el tamaño real del
   > dispositivo (base del responsive, §8).
4. **Características próximas**: nada experimental.
5. Agregala a la solución **CashHubApps** (Agregar existente → Aplicación → Canvas).

## 2. Orígenes de datos (Dataverse)

Agregá EXACTAMENTE estas tablas. Display name = lo que ves en el panel. **El PK NO
se deduce del plural — se lee del `Entity.xml`** (varias tablas plurales tienen PK
plural).

| # | Tabla (display) | PK (columna) | Para qué |
|---|---|---|---|
| 1 | CH-Tbl-ApprovalRequests | CH-Tbl-ApprovalRequest | Bandeja de aprobaciones |
| 2 | CH-Tbl-Transactions | **CH-Tbl-Transactions** | Asientos Sobrante/Faltante/Depósito |
| 3 | CH-Tbl-Bags | CH-Tbl-Bags | Tulas (localización) |
| 4 | CH-Tbl-Checks | CH-Tbl-Checks | Cheques físicos (handshake) |
| 5 | CH-Tbl-Receptions | CH-Tbl-Receptions | Planillas |
| 6 | CH-Tbl-ReceptionBatches | CH-Tbl-ReceptionBatch | Lotes de recepción |
| 7 | CH-Tbl-RecountingSessions | CH-Tbl-RecountingSession | Sesión/cajero |
| 8 | CH-Tbl-RecountingSessionDetails | CH-Tbl-RecountingSessionDetail | Detalle de sesión |
| 9 | CH-Tbl-DepositsRecountings | CH-Tbl-DepositsRecounting (SINGULAR) | Depósitos recontados |
| 10 | CH-Tbl-DeclaredDeposits | **CH-Tbl-DeclaredDeposits** | Depósitos declarados |
| 11 | CH-Tbl-DeclaredBags | CH-Tbl-DeclaredBags | Bolsas declaradas |
| 12 | CH-Tbl-Clients | CH-Tbl-Client | Cliente de la planilla — **requiere `prvRead sanic_CHTblClient`** (00 §3.3) |
| 13 | Users | (systemuserid → "User") | Navegar `'Full Name'` de cajeros/usuarios |

> `Users` es tabla de sistema: `SystemUser/Entity.xml` es un stub, no se verifica
> ahí. `'Primary Email'` está probado en producción; `'Windows Live ID'` es display
> name real de Dataverse pero *a verificar en Studio* (sin uso local previo).
> Si IntelliSense muestra otro display name, **el suyo manda**.
> `CH-Tbl-CountingDesks` NO se agrega como datasource: la mesa se resuelve por
> expansión del lookup `CountingDesk` de la sesión (display `Name`) — declarado.

**Custom API B-4** (si se adopta): para llamar `sanic_CHResolveApprovalRequest`
desde canvas, agregá el datasource **Environment** (Datos → Agregar datos →
Dataverse → *Environment*) y la guía 03 da la sintaxis. Es un patrón NUEVO para
este codebase (hoy no hay ninguna llamada así). Alternativa: consumir B-4 vía flujo.

## 3. App.Formulas (fórmulas con nombre)

Seleccioná **App** → **Formulas** → pegá TODO:

```powerfx
// ===== Paleta (verde-tesorería, prototipo aprobado) =====
ThemeAccent = ColorValue("#0F7A5A");
ThemeAccentSoft = ColorValue("#DCEFE7");
ThemeAccentInk = ColorValue("#FFFFFF");
ThemeBg = ColorValue("#EEF1EF");
ThemeSurface = ColorValue("#FFFFFF");
ThemeSurface2 = ColorValue("#F6F8F6");
ThemeSunk = ColorValue("#EAEEEB");
ThemeInk = ColorValue("#1A231E");
ThemeInk2 = ColorValue("#46534B");
ThemeInk3 = ColorValue("#6D7A72");
ThemeLine = ColorValue("#D6DDD8");
ThemeLineStrong = ColorValue("#BCC6C0");
ThemePos = ColorValue("#0E7490");   ThemePosSoft = ColorValue("#D7EEF3");   // sobrante (teal)
ThemeAccentMoney = ColorValue("#0F7772");  // barra de KPIs de dinero (mezcla accent/teal del mockup)
ThemeNeg = ColorValue("#B45309");   ThemeNegSoft = ColorValue("#FBEBD4");   // faltante (ámbar)
ThemeWarn = ColorValue("#A16207");  ThemeWarnSoft = ColorValue("#FBF1D3");
ThemeDanger = ColorValue("#B42318"); ThemeDangerSoft = ColorValue("#FBE6E3");

// ===== Formato =====
FmtMoney = "#,##0.00";                 // el '0' fuerza el entero; #,### daba "C$ .00"
FmtFecha = "dd/mm/yyyy HH:mm";         // HH = 24 h; hh seria 12 h sin AM/PM
FmtFechaCorta = "dd/mm HH:mm";

// ===== Símbolos de moneda (optionset sanic_chchcurrency: NIO/USD/EUR) =====
SimboloNIO = "C$"; SimboloUSD = "US$"; SimboloEUR = "€";

// ===== Usuario actual (fallback de identidad — UPN puede != Primary Email) =====
UsuarioActual = With(
    { u: LookUp(Users, 'Primary Email' = User().Email) },
    If(IsBlank(u.'Primary Email'), LookUp(Users, 'Windows Live ID' = User().Email), u)
);
```

> Si Studio rechaza un `ColorValue`, usá el hex directo. `FmtMoney` sin tercer
> argumento usa el idioma del usuario; si molesta, agregar `"es-NI"` en `Text()`.

## 4. App.OnError (obligatorio)

Distingue error de negocio (vuelve del guard con mensaje útil) de error interno —
así NO se entierra el mensaje del plugin, que era el problema del App.fx.yaml actual
(que directamente no tenía OnError):

```powerfx
Trace(
    $"[Supervisor] {FirstError.Message} | Kind: {FirstError.Kind} | Origen: {FirstError.Source}",
    TraceSeverity.Error
);
Notify(
    If(
        FirstError.Kind = ErrorKind.Validation || FirstError.Kind = ErrorKind.Conflict || FirstError.Kind = ErrorKind.Sync,
        FirstError.Message,   // mensaje de negocio del guard, tal cual
        "Ocurrió un error inesperado. El detalle quedó registrado para soporte."
    ),
    NotificationType.Error
)
```

**App.OnStart queda VACÍO.** Todo lo global vive en Formulas (evaluación perezosa).

## 5. Convenciones de nombres (no negociable)

| Prefijo | Tipo de control | Ejemplo |
|---|---|---|
| `scr` | Pantalla | `scrAprobaciones` |
| `cnt` | Contenedor (manual o auto-layout) | `cntFilaKpisRes` |
| `card` | **Contenedor manual** usado como card redondeada (receta 7.1) | `cardKpi1Res` |
| `gal` | Galería vertical en blanco | `galBandejaApr` |
| `lbl` | Etiqueta clásica | `lblResumenTitulo` |
| `btn` | Botón clásico (acción o overlay de clic) | `btnAprobarApr` |
| `seg` | Botón clásico de un segmentado (receta 7.7) | `segTodasTula` |
| `pil` | Píldora de estado (Botón clásico modo Ver, receta 7.2) | `pilEstadoTula` |
| `inp` | Entrada de texto clásica | `inpBuscarTula` |
| `drp` | Menú desplegable clásico | `drpCajeroRes` |
| `dt`  | Selector de fecha **moderno** (excepción §0.3) | `dtDesdeTula` |
| `ico` | Icono | `icoBuscarTula` |
| `cir` | Círculo clásico | `cirCheckChq` |
| `shp` | Rectángulo clásico (línea/separador, SIN radio) | `shpLineaHeadTula` |
| `cmp` | Instancia de componente de la app | `cmpNavRes`, `cmpKpiTulasRes` |

Variables: `gvarXxx` (Set, cruzan pantallas), `ctxXxx` (UpdateContext, locales),
`colXxx` (colecciones). **Cero controles autogenerados.** Renombrás al insertar.

## 6. Defaults tipográficos y alias de estilo

Todos los **Label clásicos**: Font `Font.'Segoe UI'`, Size `10`, Color `ThemeInk`,
Padding L/R/T/B `0`.

| Alias | Se logra con |
|---|---|
| ESTILO-TITULO | Size `18`, FontWeight `FontWeight.Semibold`, Color `ThemeInk` |
| ESTILO-SUBTITULO | Size `10.5`, Color `ThemeInk3` |
| ESTILO-SECCION | Size `9`, FontWeight `Semibold`, Color `ThemeInk3` — MAYÚSCULAS |
| ESTILO-KPI-LABEL | Size `8.5`, FontWeight `Semibold`, Color `ThemeInk3` — MAYÚSCULAS |
| ESTILO-KPI-VAL | Size `20`, FontWeight `Bold`, Color `ThemeInk` |
| ESTILO-KPI-FOOT | Size `8.5`, Color `ThemeInk3` |
| ESTILO-HEADER-COL | Size `8`, FontWeight `Semibold`, Color `ThemeInk3` — MAYÚSCULAS |
| ESTILO-CELDA | Size `9.5`, Color `ThemeInk` |
| ESTILO-CELDA-NUM | Size `9.5`, Align `Align.Right`, Color `ThemeInk` (FontWeight `Semibold` en montos) |
| ESTILO-MONO | Size `9`, FontWeight `Semibold`, Color `ThemeAccent` (IDs: sesión, tula, cheque) |
| ESTILO-LINK | Color `ThemeAccent`, Underline `true` (subrayado FIJO, §L-8 del 00) |

## 7. Recetas reutilizables

### 7.1 Card redondeada (`card…`) = Contenedor de diseño MANUAL

**CORRECCIÓN vs tracker**: la card NO es un Rectángulo (el `Rectangle` clásico no
tiene radio) NI un Botón (no admite hijos). Es un **Contenedor de diseño manual**,
que expone Fill/Border/Radius/DropShadow **y** cuyos hijos se posicionan con X/Y.

| Propiedad del contenedor manual | Valor |
|---|---|
| Fill | `ThemeSurface` |
| BorderColor / BorderThickness / BorderStyle | `ThemeLine` / `1` / `Solid` |
| RadiusTopLeft / TopRight / BottomLeft / BottomRight | `10` |
| DropShadow | `DropShadow.Light` (recupera la sombra del mockup) |

Los hijos (labels, pills, iconos, la barra de acento) van adentro con X/Y relativos
al contenedor. La **barra de acento** = `shp…AccentBar` (Rectángulo clásico): X `0`,
Y `0`, Width `3`, Height = alto del contenedor, Fill `ThemeAccent`.

- **Si la card entera navega/actúa**: los contenedores **no tienen `OnSelect`**;
  poné un **Botón clásico** `btn…Card` transparente como hijo (X `0`, Y `0`, Width/
  Height = los del contenedor, Fill/Hover/Pressed `Transparent`, BorderThickness `0`,
  Text `""`) al FONDO del z-order, con la navegación en SU `OnSelect`.
- Dentro de una fila responsive, este contenedor manual es el que lleva
  `FillPortions` y `LayoutMinWidth` (§8).

### 7.2 Píldora de estado (`pil…`)

Botón clásico como badge de solo lectura: DisplayMode `View`, Radius (las 4) `10`,
Height `22`, Size `9`, FontWeight `Semibold`, BorderColor `ThemeLine`,
BorderThickness `If(Self.Fill = ThemeSunk, 1, 0)` (la variante neutra es la única con
borde), Text/Fill/Color según la tabla de estados de cada guía. Width ≈ `12 + 7×caracteres`.

### 7.3 Grilla = encabezados + galería (clásica)

Labels ESTILO-HEADER-COL + `shp…LineaHead` (Rectángulo alto 1, Fill `ThemeLine`) +
**Galería vertical en blanco**.

| Propiedad de la galería | Valor |
|---|---|
| TemplateSize | `44` por defecto (cada guía puede fijar otro) |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| Items | Filter delegable de la guía (nunca `AddColumns`/`Search` sobre datasource) |

- **X del template** = X del encabezado − X de la galería (templates arrancan en 0).
- **Separador** (`shp…Sep`, Rectángulo): Y = `TemplateSize − 1`, Height `1`, Fill `ThemeLine`.
- **Fila clicable** (contrato de la skill): la lógica de la fila va en el
  **`OnSelect` de la GALERÍA** (`gal….OnSelect`). El overlay `btn…Fila` (Botón
  clásico: X `0` · Y `0` · Width = Width de la galería · Height = TemplateSize · Fill
  `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` ·
  BorderThickness `0` · Text `""` · al FONDO del z-order) y **todo hijo del template**
  (labels, pills) llevan `OnSelect = Select(Parent)` — que dispara el `OnSelect` de la
  galería, no el del overlay. *(Nunca poner la lógica en `btn…Fila.OnSelect`: los
  labels se comen el clic.)*
- Un label por columna, mismos X/Width que su encabezado, alto ≤ TemplateSize.

### 7.4 Multiselección clásica (grilla de scrCheques)

Sin `RecordCanSelect` (inerte en el PowerCAT): la selección es una colección propia
`colSelChq`. En el template:

- `icoCheckChq` = **Icono** — Icon `If(ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado, Icon.CheckBadge, Icon.Blocked)`;
  Color `If(!IsBlank(LookUp(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks')), ThemeAccent, ThemeLineStrong)`;
  DisplayMode `If(ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado, DisplayMode.Edit, DisplayMode.Disabled)`.
  > `Icon.CircleRing` NO existe — el estado "seleccionable no marcado" es
  > `Icon.CheckBadge` en gris (`ThemeLineStrong`). `Icon.CheckBadge`/`Icon.Blocked` sí existen.
- `icoCheckChq.OnSelect`:
  ```powerfx
  If( ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado,
      If( !IsBlank(LookUp(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks')),
          RemoveIf(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks'),
          Collect(colSelChq, ThisItem) ) )
  ```
- El botón de acción usa `colSelChq` (nunca `Gallery.Selected` múltiple).
- **Por qué icono y no la casilla clásica**: la casilla (Check box) mantiene estado
  interno propio y se DESINCRONIZA de la colección al recargar/limpiar. El costo: el
  icono distingue marcado/no-marcado solo por color. **Variante opcional** si se
  quiere la forma de caja del mockup: `cir…Check` (Círculo clásico, Fill
  `ThemeSurface`, borde `ThemeLineStrong`) DEBAJO + el icono `CheckBadge` encima
  Visible solo si está en la colección — misma lógica, señal por forma y color.

### 7.5 Componente `cmpNav` (reemplazo del ExpandMenu) — UNA definición

La nav es una barra izquierda fija de 214 px, igual en las 4 pantallas. Se hace
**Componente de la app** (Árbol de controles → pestaña **Componentes** → Nuevo
componente), NO de biblioteca. **Requisito clave**: en las propiedades del
componente, activar **"Acceso al ámbito de la aplicación"** (Access app scope) —
con eso el componente puede `Navigate`, leer `App.ActiveScreen` y `UsuarioActual`.
*Sin ese switch, nada de esto compila* (a verificar en Studio el nombre exacto del
toggle en español).

**Definición del componente `cmpNav`** (Width `214`, Height `768`; los hijos de un
componente se posicionan con X/Y — el lienzo del componente es manual):

| Control (dentro del componente — nombres SIN sufijo, viven una sola vez) | Tipo | Propiedades clave |
|---|---|---|
| `shpNavFondo` | Rectángulo clásico | X `0` · Y `0` · Width `cmpNav.Width` · Height `cmpNav.Height` · Fill `ThemeSurface` |
| `shpNavBorde` | Rectángulo clásico | X `cmpNav.Width - 1` · Y `0` · Width `1` · Height `cmpNav.Height` · Fill `ThemeLine` |
| `cardMarcaNav` | Contenedor manual (receta 7.1, Radius `9`) | X `12` · Y `14` · Width `34` · Height `34` · Fill `ThemeAccent` · BorderThickness `0` |
| `lblMarcaGlifoNav` | Etiqueta clásica (hijo de `cardMarcaNav`) | X `0` · Y `0` · Width `34` · Height `34` · Text `"₵"` · Size `14` · FontWeight `Bold` · Color `ThemeAccentInk` · Align `Align.Center` |
| `lblMarcaNav` | Etiqueta clásica | X `54` · Y `16` · Width `110` · Height `16` · Text `"CashHub"` · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` |
| `lblMarcaSubNav` | Etiqueta clásica | X `54` · Y `32` · Width `110` · Height `14` · Text `"Supervisor"` · Size `8.5` · Color `ThemeInk3` |
| `btnNavResumen` · `btnNavAprob` · `btnNavTulas` · `btnNavChq` | Botón clásico | Y `70/112/154/196` · X `10` · Width `194` · Height `36` · Align `Align.Left` · Radius (las 4) `8` · BorderThickness `0` · HoverFill `ThemeSurface2` · Text con glifo incluido (`"◈  Resumen de operación"`, `"⚖  Aprobaciones"`, `"▦  Localización de tulas"`, `"✓  Cheques"`) · Fill `If(App.ActiveScreen = scrResumen, ThemeAccentSoft, ThemeSurface)` · Color `If(App.ActiveScreen = scrResumen, ThemeAccent, ThemeInk2)` (cada botón compara contra SU pantalla) · OnSelect `Navigate(scrResumen)` (cada botón a la suya) |
| `pilNavBadgeApr` | Botón clásico (píldora, receta 7.2) | X `176` · Y `120` · Width `12 + 7 * Len(Self.Text)` · Height `18` · contador de pendientes: Text `Text(CountRows(Filter('CH-Tbl-ApprovalRequests', ApprovalStatus = Blank())))` · Fill `ThemeNegSoft` · Color `ThemeNeg` · Visible `Self.Text <> "0"` |
| `lblQuienNav` | Etiqueta clásica | X `12` · Y `cmpNav.Height - 48` · Width `190` · Height `32` · Text `UsuarioActual.'Full Name' & " · Supervisor"` · Size `8.5` · Color `ThemeInk3` · Wrap `true` |

> El glifo va como prefijo del `Text` del botón — sin iconos superpuestos, cero
> ambigüedad de clic. El `Filter` del badge es delegable (`= Blank()` sobre picklist).

**Instancias**: en cada pantalla, insertar el componente y renombrarlo con sufijo
(regla 0.5): `cmpNavRes`, `cmpNavApr`, `cmpNavTula`, `cmpNavChq` — X `0` · Y `0` ·
Width `214` · Height `Parent.Height`. El body de cada pantalla referencia el ancho
de SU instancia (§8.3): `cntBodyRes.X = cmpNavRes.Width`.

### 7.5b Componente `cmpKpi` (card de indicador) — UNA definición

Las cards de KPI se repiten en scrResumen (5) y scrCheques (4) → componente con
**propiedades de entrada** (tipo Entrada, se crean en el panel del componente):

| Propiedad de entrada | Tipo | Ejemplo |
|---|---|---|
| `KpiLabel` | Texto | `"TULAS RECONTADAS"` |
| `KpiValor` | Texto | `Text(CountRows(colBags))` — el CALLER formatea (montos con `FmtMoney`) |
| `KpiPie` | Texto | `"en 3 sesiones confirmadas"` |
| `KpiAccent` | Color | `ThemeAccent` (barra izquierda; `ThemeNeg` para "con diferencia"; `ThemeAccentMoney` para KPIs de dinero) |
| `KpiPieColor` | Color | `ThemeInk3` (default); `ThemeAccent` en KPIs navegables — el pie "ver en … →" es la única affordance de clic |
| `KpiCur` | Texto | `""` (default). Símbolo de moneda en dos tonos: si no está vacío, `lblKpiCur` (Size `11`, Semibold, `ThemeInk3`) lo pinta ANTES del valor y `KpiValor` va SIN símbolo — reproduce el `.k-val .cur` del mockup. Con `""`, `lblKpiCur` no ocupa lugar |

**Dentro del componente** (lienzo del componente: Width `220`, Height `92`; los
componentes NO exponen Radius/DropShadow de lienzo, así que el fondo redondeado lo
da `cntKpiCard`). Árbol y geometría COMPLETOS — orden back→front:

| Control | Tipo | Propiedades |
|---|---|---|
| `cntKpiCard` | Contenedor manual (receta 7.1) | X `0` · Y `0` · Width `cmpKpi.Width` · Height `cmpKpi.Height` · Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius (las 4) `10` · DropShadow `Light` — TODOS los demás controles son sus hijos |
| `btnKpiClick` | Botón clásico (hijo, al FONDO del z-order) | X `0` · Y `0` · Width `cntKpiCard.Width` · Height `cntKpiCard.Height` · Fill/HoverFill/PressedFill `Transparent` · BorderThickness `0` · Text `""` · OnSelect `cmpKpi.OnKpiSelect()` |
| `shpKpiBarra` | Rectángulo clásico (hijo) | X `0` · Y `0` · Width `3` · Height `cntKpiCard.Height` · Fill `cmpKpi.KpiAccent` |
| `lblKpiLabel` | Etiqueta clásica (hijo) | X `14` · Y `12` · Width `cntKpiCard.Width - 28` · Height `14` · ESTILO-KPI-LABEL · Text `cmpKpi.KpiLabel` |
| `lblKpiCur` | Etiqueta clásica (hijo) | X `14` · Y `34` · Width `If(IsBlank(cmpKpi.KpiCur), 0, 7 * Len(cmpKpi.KpiCur) + 4)` · Height `24` · Text `cmpKpi.KpiCur` · Size `11` · FontWeight `Semibold` · Color `ThemeInk3` · alineado abajo (VerticalAlign `Bottom`) — con `KpiCur = ""` no ocupa lugar |
| `lblKpiValor` | Etiqueta clásica (hijo) | X `lblKpiCur.X + lblKpiCur.Width` · Y `28` · Width `cntKpiCard.Width - Self.X - 12` · Height `30` · ESTILO-KPI-VAL pero **Size AUTO-ENCOGIBLE**: `If(Len(cmpKpi.KpiValor) > 12, 12.5, If(Len(cmpKpi.KpiValor) > 9, 16, 20))` · **Wrap `false`** · Text `cmpKpi.KpiValor` — aguanta hasta `999,999,999.99` sin montarse (QA del usuario) |
| `lblKpiPie` | Etiqueta clásica (hijo) | X `14` · Y `62` · Width `cntKpiCard.Width - 28` · Height `14` · ESTILO-KPI-FOOT · Text `cmpKpi.KpiPie` · **Color `cmpKpi.KpiPieColor`** |

> Todos los hijos de `cntKpiCard` que NO son el botón llevan `OnSelect =
> Select(btnKpiClick)` para que el clic en cualquier parte de la card dispare la
> propiedad de comportamiento (los KPI no navegables la dejan en `false` y el clic
> no hace nada).

> **Clic (KPI navegable, ej. "con diferencia → Aprobaciones")**: los componentes
> exponen su propio **`OnSelect`**-like solo vía una propiedad de comportamiento.
> Crear propiedad de **Comportamiento** `OnKpiSelect` y un `btnKpiClick` (Botón
> clásico transparente a lienzo completo, al FONDO) con `OnSelect = cmpKpi.OnKpiSelect()`.
> El caller define `cmpKpiDifRes.OnKpiSelect = Navigate(scrAprobaciones)`. Los KPI
> no navegables dejan la propiedad vacía (`false`).

**Instancias con sufijo**: `cmpKpiTulasRes`, `cmpKpiDifRes`, `cmpKpiNioRes`, … —
cada instancia lleva `FillPortions 1` + `LayoutMinWidth 196` dentro de la fila (§8.4;
el 196 sale de la aritmética de la guía 02: 5×196 + 4×12 de gap = 1028 < 1100 px
disponibles a 1366 — con 210 el margen era de 2 px y cualquier ventana menor envolvía).

### 7.6 Filtros y overlay de carga

- **Barra de filtros**: contenedor `cnt…Filtros` (receta card 7.1) con `drp…`/`dt…`/
  `inp…` clásicos y `btn…Actualizar`.
- **Aviso "datos desactualizados"** (`cnt…Stale`, Visible `ctx…Stale`): se prende en
  el `OnChange` de cada filtro, se apaga al Actualizar.
- **Bloqueo de carga**: la pantalla tiene `LoadingSpinner`/`LoadingSpinnerColor`
  nativos (útiles para cargas del datasource). Para bloquear interacción durante una
  cadena de `Patch`, además un overlay `cntCargando…` (Contenedor manual Fill
  `RGBA(0,0,0,0.35)`, Visible `ctx…Cargando`) + `lbl…Cargando`. Se apaga SIEMPRE por
  fuera de la rama de éxito (`IfError(<cadena>; UpdateContext({ctxCargando:false}), UpdateContext({ctxCargando:false}))`),
  para que un fallo no lo deje pegado.
  > Este overlay NO lleva prefijo `card` (esa receta obliga Fill `ThemeSurface`); es `cnt…`.

### 7.7 Segmentado (reemplazo de Tab list) — fila de Botones clásicos

Cada "pestaña"/segmento es un **Botón clásico** `seg…` en una fila; una variable
`ctxSeg<Scr>` guarda el activo:

| Propiedad | Valor |
|---|---|
| Fill | `If(ctxSegTula = "todas", ThemeAccent, ThemeSurface2)` |
| Color | `If(ctxSegTula = "todas", ThemeAccentInk, ThemeInk2)` |
| Radius | `0` (los extremos con Radius solo del lado externo si se quiere; opcional) |
| BorderColor / Thickness | `ThemeLine` / `1` |
| OnSelect | `UpdateContext({ctxSegTula: "todas"})` |

La galería filtra por `ctxSeg<Scr>`. Reset al entrar: `UpdateContext({ctxSegTula: "esclusa"})` en OnVisible.

### 7.8 Gotcha `ClearCollect` + `If` (regla, no opcional)

`ClearCollect(col, If(cond, Filter(...)))` **SIN else revienta** con
`ErrorKind.Internal` cuando `cond` es falsa (Blank como fuente). El `If` va POR
FUERA: `If(cond, ClearCollect(col, Filter(...)), Clear(col))`.

## 8. Responsive horizontal (tablet landscape → desktop)

Sin verticales ni celulares (futuro). Reglas:

1. **App con Escalar-para-ajustar OFF** (§1) → `Width`/`Height` reales.
2. **Breakpoint EN VIVO, no congelado**: el árbol referencia el ancho reactivo
   `<scrNombre>.Width` (o `Parent.Width` cuando el padre es la pantalla/su
   contenedor). Ejemplo: columna de baja prioridad `Visible = scrTulas.Width >= 1100`.
   > NO usar `UpdateContext({ctx: Self.Width})` en OnVisible: se congela y no
   > reacciona al redimensionar la ventana. No hace falta variable.
3. **Estructura en contenedores de auto-layout**: el cuerpo = **Contenedor vertical**
   `cntBody…`, X `= cmpNav<Scr>.Width`, Width `= Parent.Width - cmpNav<Scr>.Width`,
   Height `= Parent.Height`. Filas = **contenedores horizontales**.
4. **Fila de KPIs** (contenedor horizontal): cada card (contenedor manual) lleva
   `FillPortions = 1` **y `LayoutMinWidth = 196`**. Para que envuelva a 2 filas en
   anchos chicos, el contenedor horizontal lleva **`LayoutWrap = true`** (NO `Wrap`,
   que es del Label). Regla: el wrap se dispara cuando `Σ LayoutMinWidth > ancho del
   contenedor` — por eso `LayoutMinWidth` es obligatorio, sin él `FillPortions` solo
   encoge y nunca envuelve.
5. **Grilla**: Width de la galería = ancho de su contenedor; X/Width de columnas como
   fracción del ancho de la galería (`galX.Width * 0.18`, nombrando la galería —
   nunca `Self.Width`, que es circular). Columnas de baja prioridad:
   `Visible = <scr>.Width >= 1100`.
6. **Master-detail (scrAprobaciones)**: contenedor horizontal con bandeja
   `FillPortions 5` (`LayoutMinWidth 380`) y detalle `FillPortions 4`
   (**`LayoutMinWidth 550`**), `LayoutWrap = true` → el detalle baja solo cuando no
   caben. Breakpoint resultante: `214 + 52 + 380 + 16 + 550` = **1212**.
   > El `550` sale de la aritmética de columnas de `03` §8, no del ojo: con `420`
   > la columna DIFERENCIA de la caja de diferencias queda en ~59 px y no rinde un
   > `"+200,000.00 / −200,000.00"` (~144 px). Es el ancho mínimo en el que el
   > supervisor ve completo el monto que está por firmar.

## 9. Orden de construcción

1. Esta guía (app + datos + formulas + OnError).
2. Creá las 4 pantallas VACÍAS de una (renombrar, Fill `ThemeBg`): `scrResumen`,
   `scrAprobaciones`, `scrTulas`, `scrCheques`. Borrá la pantalla en blanco default.
3. Construí los COMPONENTES (§7.5 `cmpNav` con app scope, §7.5b `cmpKpi`) antes de
   cualquier pantalla, e insertá `cmpNav<Scr>` en las 4.
4. Llená en orden: `02` → `03` → `04` → `05`.
5. Al final: App Checker en cero errores; prueba de humo §10.

## 10. Prueba de humo (después de la última pantalla)

1. Resumen carga solo al entrar; cambiar fecha muestra el aviso de desactualizado.
2. Tarjeta/fila "con diferencia" → navega a Aprobaciones con la solicitud.
3. Aprobar una solicitud cuyo `'Created By'` = `UsuarioActual` queda bloqueado
   (cuatro ojos: solicitante = `'Created By'`, no hay columna de solicitante propia);
   doble aprobación revalida estado y no duplica asientos.
4. scrTulas: "fecha hasta = hoy" muestra las tulas de hoy; tab "Todas" lista los 6
   estados; búsqueda por recibo devuelve resultados (espejo delegable B-3).
5. scrCheques: solo se marcan cheques Recontado; enviar da feedback real; sellos de
   tiempo (`'Enviado el'`, `'No recibido el'`) visibles.
6. Un identificador inexistente por pantalla → mensaje inline, nunca spinner colgado.
