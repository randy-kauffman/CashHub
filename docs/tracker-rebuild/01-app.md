# Guía 01 — La app: creación, datos, fórmulas globales y convenciones

Esta guía se hace UNA vez, antes de cualquier pantalla. Las guías 02–06 asumen
todo lo de acá.

## 0. Contrato de formato de las guías de pantalla (02–06)

Toda guía de pantalla cumple estas reglas — si encontrás una violación, es un
bug de la guía, no una licencia creativa:

1. **Árbol completo**: el árbol del inicio lista TODOS los controles de la
   pantalla con su nombre final.
2. **Cobertura total**: todo control que aparece en el árbol tiene su
   descripción más abajo — con su propia tabla, con una fila en una tabla
   compartida, o con puntero explícito a una receta de esta guía 01
   (p. ej. "receta 7.5") cuando la receta ya fija TODAS sus propiedades.
3. **Tipo explícito**: la descripción de cada control declara QUÉ componente
   de Power Apps es, usando exactamente estos nombres (los del menú Insertar):

   | Tipo en la guía | En Studio (Insertar) |
   |---|---|
   | Etiqueta clásica | Entrada de texto clásica → **Etiqueta** (`Label`) |
   | Botón clásico | Clásico → **Botón** (`Button`) |
   | Botón moderno | Moderno → **Botón** (`Button`) |
   | Entrada de texto moderna | Moderno → **Entrada de texto** (`Text input`) |
   | Menú desplegable moderno | Moderno → **Menú desplegable** (`Drop down`) |
   | Lista de pestañas moderna | Moderno → **Lista de pestañas** (`Tab list`) |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** (`Blank vertical gallery`) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) |
   | Círculo clásico | Formas → **Círculo** (`Circle`) |
   | Texto HTML clásico | Clásico → **Texto HTML** (`HTML text`) |

4. **Propiedades**: cada tabla de control lista las propiedades que cambian
   del default (sección 6 fija los defaults tipográficos).
5. **Nombres únicos en TODA la app** (regla 0.5): en canvas apps el nombre de
   control es único a nivel **aplicación**, no de pantalla — si repetís un
   nombre, Studio lo renombra solo y te rompe las referencias. Por eso todo
   control cuyo patrón se repite entre pantallas lleva el sufijo de la suya:
   **`Busq`** (scrBusqueda), **`Lote`** (scrLote), **`Tula`** (scrTula),
   **`Dep`** (scrDeposito), **`Corr`** (scrCorreo) — `shpFichaCardLote`,
   `lblCrumbBusquedaTula`, `lblFichaK1Dep`… Los controles que ya son únicos por
   naturaleza (`galRecientes`, `shpTxsCard`…) NO llevan sufijo.

## 1. Crear la app

1. make.powerapps.com → entorno DEV → **Apps → Nueva app → Comenzar con una
   página en blanco → Tamaño: Tableta**.
2. **Configuración → General**:
   - Nombre: `CH-App-DepositsTracker` (reemplaza a la actual al final; mientras
     tanto podés llamarla `CH-App-DepositsTracker v2` y renombrar al publicar).
   - Ícono: subí `docs/marca/icono-deposits-tracker-512.png` · Color de fondo: `#C8102E`.
3. **Configuración → Pantalla**: Orientación **Horizontal**, Tamaño **1366 × 768**,
   **Escalar para ajustar: Activado**, Bloquear relación de aspecto: Activado.
4. **Configuración → Características próximas**: no actives nada experimental.
   (Las fórmulas de estas guías no dependen de features preview.)
5. Guardala ya en la solución: agregala a **CashHubApps** desde el portal de
   soluciones (Agregar existente → Aplicación → Canvas) para que el export/deploy
   siga el pipeline actual.

## 2. Orígenes de datos (Dataverse)

Agregá EXACTAMENTE estas tablas (Datos → Agregar datos). El display name es el
que vas a ver en el panel:

| # | Tabla (display) | Para qué |
|---|---|---|
| 1 | CH-Tbl-ReceptionBatches | Lotes de recepción |
| 2 | CH-Tbl-Receptions | Planillas |
| 3 | CH-Tbl-Bags | Tulas |
| 4 | CH-Tbl-RecountingSessions | Sesión/cajero |
| 5 | CH-Tbl-DepositsRecountings | Depósitos recontados |
| 6 | CH-Tbl-Checks | Cheques físicos |
| 7 | CH-Tbl-Transactions | Transacciones |
| 8 | CH-Tbl-TransactionBatches | Lotes de digitación TB |
| 9 | CH-Tbl-Declarations | Declaraciones |
| 10 | CH-Tbl-DeclaredBags | Bolsas declaradas |
| 11 | CH-Tbl-DeclaredDeposits | Depósitos declarados |
| 12 | CH-Tbl-DeclaredChecks | Cheques declarados |
| 13 | CH-Tbl-Received Emails | Correos (tracking) |
| 14 | CH-Tbl-EmailsAttachments | Adjuntos |
| 15 | CH-Tbl-ValidationRuns | Corridas de validación |
| 16 | CH-Tbl-ValidationResults | Resultados de validación |
| 17 | CH-Tbl-ValidationRules | Catálogo de reglas |
| 18 | Users | SOLO para navegar `'Full Name'` de cajeros/usuarios |
| 19 | CH-Tbl-Clients | Cliente de la planilla |
| 20 | CH-Tbl-Locations | Ubicación del lote (ficha de scrLote) |

> ⚠️ NO agregues más tablas "por si acaso" — cada datasource pesa en el
> arranque. La app vieja arrastraba tablas sin uso; no repetimos eso.

## 3. App.Formulas (fórmulas con nombre)

Seleccioná **App** en el árbol → propiedad **Formulas** → pegá TODO esto:

```powerfx
// ===== Paleta (rojo BAC, prototipo v3) =====
ThemeAccent = ColorValue("#C8102E");
ThemeAccentSoft = ColorValue("#FBEBED");
ThemeBg = ColorValue("#F6F5F4");
ThemeSurface = Color.White;
ThemeSurface2 = ColorValue("#FAF8F7");
ThemeInk = ColorValue("#201C1D");
ThemeInk2 = ColorValue("#66595C");
ThemeInk3 = ColorValue("#948A8C");
ThemeLine = ColorValue("#E2DCDD");
ThemeLineSoft = ColorValue("#EDE8E9");
ThemeOk = ColorValue("#0E7A3C");   ThemeOkSoft = ColorValue("#E2F2E8");
ThemeWarn = ColorValue("#96610A"); ThemeWarnSoft = ColorValue("#F7EDD8");
ThemeCrit = ColorValue("#A4262C"); ThemeCritSoft = ColorValue("#F9E6E4");
ThemeInfo = ColorValue("#23557D"); ThemeInfoSoft = ColorValue("#E4EEF6");

// ===== Formato =====
FmtMoney = "#,##0.00";
FmtFecha = "dd/mm/yyyy HH:mm";   // HH = 24 horas; hh seria 12h sin AM/PM

// ===== Símbolos de moneda =====
SimboloNIO = "C$"; SimboloUSD = "US$"; SimboloEUR = "€";
```

> Si Studio rechaza `Color.White`, usá `ColorValue("#FFFFFF")`.

> **Sobre `FmtMoney`**: `Text()` sin tercer argumento usa el idioma del usuario;
> se acepta (el mockup fijaba es-NI). Si molesta, agregar `"es-NI"` como tercer
> argumento de `Text()`.

## 4. App.OnError (obligatorio — convención del proyecto)

Propiedad **OnError** de App:

```powerfx
Trace(
    $"[Tracker] {FirstError.Message} | Kind: {FirstError.Kind} | Origen: {FirstError.Source} | Obs: {FirstError.Observed}",
    TraceSeverity.Error
);
Notify(
    "Ocurrió un error inesperado. El detalle quedó registrado para soporte.",
    NotificationType.Error
);
```

**App.OnStart queda VACÍO.** Todo lo global vive en Formulas (se evalúa
perezoso y no bloquea el arranque).

## 5. Convenciones de nombres (no negociable — la app vieja murió por esto)

| Prefijo | Tipo de control | Ejemplo |
|---|---|---|
| `scr` | Pantalla | `scrBusqueda` |
| `con` | **NO USAR** — las cards son rectángulos (`shp`), ver receta 7.1 | — |
| `gal` | Galería | `galPlanillas` |
| `lbl` | Etiqueta clásica | `lblLoteTitulo` |
| `btn` | Botón moderno | `btnBuscar` |
| `pil` | Píldora de estado (Botón clásico en modo Ver) | `pilTulaEstado` |
| `inp` | Entrada de texto moderna | `inpBuscar` |
| `drp` | Menú desplegable moderno | `drpTipoBusqueda` |
| `tab` | Lista de pestañas moderna (Tab list) | `tabDetalleTula` |
| `shp` | Forma (rectángulo separador, card, círculo) | `shpHeaderLinea` |

Variables: `gvarXxx` (Set, cruzan pantallas), `ctxXxx` (UpdateContext, locales),
`colXxx` (colecciones). **Cero controles con nombre autogenerado.** Renombrás el
control en el MOMENTO en que lo insertás, antes de tocar cualquier propiedad.

## 6. Defaults tipográficos (aplican salvo que la guía diga otra cosa)

Todos los **Label clásicos**:

| Propiedad | Valor |
|---|---|
| Font | `Font.'Segoe UI'` |
| Size | `10` |
| Color | `ThemeInk` |
| PaddingLeft/Right/Top/Bottom | `0` |

Variantes que las guías nombran por alias:

| Alias | Se logra con |
|---|---|
| ESTILO-TITULO | Size `15`, FontWeight `FontWeight.Semibold` |
| ESTILO-SECCION | Size `9`, FontWeight `FontWeight.Semibold`, Color `ThemeInk2` — texto SIEMPRE en MAYÚSCULAS |
| ESTILO-DATO-K | Size `8`, Color `ThemeInk3`, **Height `16`** — texto en MAYÚSCULAS |
| ESTILO-DATO-V | Size `10`, FontWeight `FontWeight.Semibold` |
| ESTILO-CELDA | Size `9.5` |
| ESTILO-CELDA-NUM | Size `9.5`, Align `Align.Right` |
| ESTILO-HEADER-COL | Size `8`, FontWeight `FontWeight.Semibold`, Color `ThemeInk3`, texto en MAYÚSCULAS |
| ESTILO-LINK | Color `ThemeAccent`, Underline `true` |

> **ESTILO-LINK**: el hover-underline del mock no existe en labels clásicos, así
> que el subrayado va FIJO como affordance de "esto se puede clickear".

## 7. Recetas reutilizables

### 7.1 Card (superficie blanca)

Rectángulo clásico (`shp…Card`): Fill `ThemeSurface`, BorderColor `ThemeLine`,
BorderThickness `1`, BorderStyle `BorderStyle.Solid`, **Radius (las 4) `8`**. Los
controles de contenido van ENCIMA (no hay contenedores anidados: posicionamiento
absoluto, X/Y de cada control vienen en cada guía).

> La sombra del mockup no existe en el rectángulo clásico — pérdida aceptada.

### 7.2 Píldora de estado (`pil…`)

Button **clásico** usado como badge de solo lectura:

| Propiedad | Valor |
|---|---|
| DisplayMode | `DisplayMode.View` |
| Radius (las 4) | `12` |
| Height | `26` |
| Font / Size | `Font.'Segoe UI'` / `9` |
| FontWeight | `FontWeight.Semibold` |
| BorderColor | `ThemeLineSoft` (fijo) |
| BorderThickness | `If(Self.Fill = ThemeSurface2, 1, 0)` |
| Text / Fill / Color | según la tabla de estados de cada guía |
| Width | ajustá a ojo hasta que el texto respire (± `8 + 7×caracteres`) |

**Caso NEUTRO**: la píldora que cae al default de su Switch (`Fill ThemeSurface2`
/ `Color ThemeInk2`) es la ÚNICA con borde — sin él, sobre una card blanca no se
ve. Por eso el BorderThickness se calcula solo: `If(Self.Fill = ThemeSurface2, 1, 0)`.
Igual que `.pill.mute` del mock. Esto aplica a TODAS las pills por herencia de
esta receta; las guías no lo repiten. Si Studio rechazara comparar colores,
repetí el mismo Switch de la Fill devolviendo `1` en la rama default y `0` en
las demás.

> El punto de 6px de las pills del mockup (`.pill::before`) se descarta — un
> círculo extra por cada pill no paga; divergencia declarada.

### 7.3 Grilla = galería + fila de encabezados

Cada "tabla" de la app es: labels ESTILO-HEADER-COL alineados con las columnas
+ `shpLinea…` (rectángulo alto 1, Fill `ThemeLine`) + **galería vertical en
blanco** debajo con:

| Propiedad | Valor |
|---|---|
| TemplateSize | `42` **por defecto; cada guía puede fijar otro** |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| Fill de fila seleccionable | ver 7.4 |

**Regla de coordenadas del template**: el X de cada control DENTRO del template
= X del encabezado − X de la galería (los templates arrancan en 0).

**Separador de fila** (`shp…Sep`, Rectángulo clásico): Y = **TemplateSize − 1**,
Height `1`, Fill `ThemeLineSoft`. Con TemplateSize `42` eso es Y `41`; con `40`,
Y `39`. Es OPCIONAL en grillas cortas o embebidas en paneles (mini-tablas): las
guías lo omiten donde no aporta.

**Overlay de fila navegable** (`btn…Fila`, Botón clásico): X `0` · Y `0` ·
Width = **Width de la galería** · Height = **TemplateSize** · Fill `Transparent` ·
HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` ·
Text `""`, con el OnSelect de la guía. *(Hover gris neutro como el mock; el
rosado se reserva al pressed y a la fila seleccionada.)*

Dentro del template va, además, un label por columna (mismos X/Width que su
encabezado).

### 7.4 Fila seleccionada

En galerías con selección persistente (no navegación):
- rectángulo de fondo del template (`shp…Sel`): Fill
  `If(ThisItem.IsSelected, ThemeAccentSoft, Transparent)`
- barra lateral de acento (`shp…SelBarra`): X `0` · Y `0` · Width `3` · Height
  = TemplateSize · Fill `ThemeAccent` · Visible `ThisItem.IsSelected`

⚠️ **Las galerías clásicas AUTO-SELECCIONAN la primera fila** al cargar Items.
En esta app eso es comportamiento BUSCADO (los paneles "del seleccionado"
arrancan mostrando el primer registro, sin panel muerto). Los estados vacíos se
reservan para colecciones sin filas, nunca para "sin selección".

### 7.5 Encabezado de pantalla (va en TODAS las pantallas)

**5 controles.** Los nombres de la tabla son la base: en cada pantalla llevan su
sufijo de la regla 0.5 (en scrLote, `shpHeader` se llama `shpHeaderLote`).

| Control | Tipo | Propiedades |
|---|---|---|
| `shpHeader` | Rectángulo clásico | X `0` · Y `0` · Width `1366` · Height `52` · Fill `ThemeSurface` · BorderThickness `0` |
| `shpHeaderLinea` | Rectángulo clásico | X `0` · Y `52` · Width `1366` · Height `1` · Fill `ThemeLine` |
| `shpMarca` | Botón clásico | X `24` · Y `11` · Width `30` · Height `30` · Radius `7` · Fill `ThemeAccent` · Color `Color.White` · Text `"CH"` · Size `9` · FontWeight `FontWeight.Bold` · DisplayMode `DisplayMode.View` · BorderThickness `0` |
| `lblMarca` | Etiqueta clásica | X `64` · Y `11` · Width `90` · Height `30` · Text `"CashHub"` · Size `11` · FontWeight `FontWeight.Semibold` · Color `ThemeInk` |
| `lblMarcaSub` | Etiqueta clásica | X `154` · Y `11` · Width `330` · Height `30` · Text `"· Tracking de Depósitos"` · Size `11` · FontWeight `FontWeight.Normal` · Color `ThemeInk3` |

> La marca va partida en DOS etiquetas para lograr el bicolor del mock (nombre
> en tinta fuerte, descriptor apagado) — un solo Label no admite dos colores.

Fondo de TODAS las pantallas: propiedad **Fill** de la pantalla = `ThemeBg`.

### 7.6 Migas de pan (breadcrumb)

Fila de labels clicables a Y `64`, Height `22`, Size `9`:
los eslabones navegables son **Etiquetas clásicas** con Color `ThemeAccent` y su
fórmula OnSelect (los labels clásicos tienen OnSelect); los separadores son
Etiquetas clásicas `"›"` Color `ThemeInk3` Width `14`; el eslabón actual es una
Etiqueta clásica Color `ThemeInk2`.
Cada guía da los textos, X y fórmulas exactas.

## 8. Orden de construcción

1. Esta guía (app + datos + formulas + OnError).
2. **Creá las 5 pantallas VACÍAS de una** (Nueva pantalla → En blanco, solo
   renombrar y Fill `ThemeBg`): `scrBusqueda`, `scrLote`, `scrTula`,
   `scrDeposito`, `scrCorreo`. Motivo: los `Navigate()` cruzados compilan desde
   el primer día — si no, el OnSelect del buscador queda en rojo hasta la
   última guía. Borrá la pantalla en blanco por defecto de Studio.
3. Llenalas en orden: `02` scrBusqueda → `03` scrLote → `04` scrTula →
   `05` scrDeposito → `06` scrCorreo.
4. Al final: borrá la pantalla en blanco inicial si quedó, verificá el App
   Checker (cero errores; los warnings de accesibilidad se resuelven con
   `AccessibleLabel` que cada guía ya incluye) y probá la lista de humo de la
   sección 9.

## 9. Prueba de humo (después de la última pantalla)

1. Buscar un tracking real (`Email-…`) → aterriza en scrCorreo con validaciones.
2. Desde una bolsa declarada vinculada → clic tula → scrTula con conciliación.
3. scrTula → tab Por depósito → seleccioná → "Ver transacciones" → scrDeposito
   muestra tipo/lote TB/acreditación.
4. Breadcrumb: Tula → Planilla → Lote → Búsqueda, sin residuos visuales.
5. Buscar tula directa (`…bagid…`) → scrTula muestra ESA tula (bug histórico:
   verificá que la conciliación cambió).
6. Un identificador inexistente por cada tipo → mensaje inline, nunca pantalla
   rota ni spinner colgado.
