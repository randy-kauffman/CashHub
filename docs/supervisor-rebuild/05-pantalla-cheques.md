# Guía 05 — scrCheques (envío de cheques a digitación)

Entrega física de cheques del supervisor al digitador (la "esclusa"). Reemplaza a
`Screen ChecksSummary` — la pantalla que tenía el `DVFluentDetailsList` de PowerCAT
con `RecordCanSelect` **inerte** (marcaba cheques que el servidor iba a rechazar), el
query duplicado en **4 copias divergentes**, ramas `OR` sin cota temporal que truncaban
a 500, `ForAll(Patch(...))` **sin `IfError`** (fallos mudos) y un `Notify` de reenvío
que **mentía éxito incondicional**.

Acá: **una sola definición de consulta** con ventana temporal acotada, multiselección
clásica con colección propia (01 §7.4), y feedback REAL contado por cheque.

**El contrato del servidor manda.** `CheckGuard` (Pre-validation, síncrono, sin
`filteringattributes`) sólo acepta estas transiciones:

| Desde | Hacia | ¿Legal? |
|---|---|---|
| `Recontado` | `Enviado a digitacion` | ✅ — es lo que hace el botón Enviar |
| `No Recibido` | `Enviado a digitacion` | ✅ — es lo que hace el botón Reenviar |
| `Enviado a digitacion` | `Enviado a digitacion` | ❌ **BLOQUEADA** (no hay no-op de reenvío) |
| `Recibido` | cualquier cosa | ❌ **CONGELADO** (terminal: bloquea toda edición) |
| `Digitado` / `Rebotado` | cualquier cosa | ❌ reservados para la digitación al Core |

Por eso la UI **sólo deja seleccionar `Recontado`** y el reenvío toma **sólo
`No Recibido`**: el canvas no inventa reglas, replica las del guard y **cuenta los
rechazos que igual lleguen** (otro supervisor te ganó de mano entre la carga y el envío).

**El canvas NUNCA escribe fechas.** Los sellos `'Enviado el'` / `'No recibido el'` /
`'Recibido el'` los estampa el plugin server-side; acá sólo se leen.

**CONTRATO DE ENTRADA**: ninguno. La pantalla es autosuficiente — no lee ninguna
`gvar` ni colección de otras pantallas; se llega por `Navigate()` simple desde
`cmpNavChq`. Requiere de `01`: App.Formulas (paleta, `FmtMoney`, `FmtFechaCorta`,
`SimboloNIO/USD/EUR`), App.OnError, los componentes `cmpNav` y `cmpKpi`, y el
datasource **CH-Tbl-Checks** (con **CH-Tbl-Bags**/**CH-Tbl-Receptions**/**CH-Tbl-Clients**
para el dot-walk de Cliente/Tula).

**Variables/colecciones propias**: `ctxCargandoChq`, `ctxEnviandoChq`, `ctxStaleChq`,
`ctxUltimaActChq`, `ctxEnviadosChq`, `ctxRechazadosChq`, `ctxDescartadosChq`,
`ctxConfirmReenvChq`, `colChequesChq`, `colSelChq`, `colEnviarChq`, `colReenviarChq`,
`colEnvioResChq`.

## Árbol de controles

```
scrCheques
├─ cmpNavChq                      (instancia de cmpNav, 01 §7.5)
├─ cntBodyChq                     (Contenedor vertical, auto-layout)
│  ├─ cntTituloChq                (Contenedor vertical)
│  │  ├─ lblTituloChq
│  │  └─ lblSubtituloChq
│  ├─ cardFiltrosChq              (Contenedor manual, receta 01 §7.1)
│  │  ├─ lblFecFiltroChq
│  │  ├─ dtFechaChq
│  │  ├─ lblEstFiltroChq
│  │  ├─ drpEstadoChq
│  │  ├─ cirSyncChq
│  │  ├─ lblSyncChq
│  │  └─ btnActualizarChq
│  ├─ cntStaleChq                 (Contenedor manual — aviso desactualizado)
│  │  ├─ lblStaleChq
│  │  └─ btnStaleChq
│  ├─ cntKpisChq                  (Contenedor horizontal, LayoutWrap)
│  │  ├─ cmpKpiPorEnviarChq       (instancia de cmpKpi, 01 §7.5b)
│  │  ├─ cmpKpiNioChq             (instancia de cmpKpi)
│  │  ├─ cmpKpiUsdChq             (instancia de cmpKpi)
│  │  └─ cmpKpiNoRecChq           (instancia de cmpKpi)
│  └─ cardChequesChq              (Contenedor manual, receta 01 §7.1)
│     ├─ lblChqTituloChq
│     ├─ pilSelCountChq
│     ├─ btnReenviarChq
│     ├─ btnEnviarChq
│     ├─ shpBandHeadChq
│     ├─ lblColCheckChq · lblColNumeroChq · lblColClienteChq · lblColTulaChq
│     │  · lblColMontoChq · lblColEstadoChq · lblColSellosChq
│     ├─ shpLineaHeadChq
│     ├─ galChequesChq            (Galería vertical en blanco)
│     │  ├─ btnFilaChq            (overlay de clic, AL FONDO del z-order)
│     │  ├─ icoCheckChq           (ÚNICO hijo con OnSelect propio — receta 01 §7.4)
│     │  ├─ lblCelNumeroChq
│     │  ├─ lblCelClienteChq
│     │  ├─ lblCelTulaChq
│     │  ├─ lblCelMontoChq
│     │  ├─ pilCelEstadoChq
│     │  ├─ lblCelSellosChq
│     │  └─ shpSepChq
│     ├─ icoVacioChq
│     ├─ lblVacioTituloChq
│     └─ lblVacioTextoChq
├─ cntConfirmChq                  (Contenedor manual — VELO del reenvío masivo)
│  └─ cardConfirmChq              (Contenedor manual, receta 01 §7.1)
│     ├─ lblConfirmChq
│     ├─ btnConfirmNoChq
│     └─ btnConfirmSiChq
└─ cntCargandoChq                 (Contenedor manual — overlay, receta 01 §7.6)
   └─ lblCargandoChq
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | ver abajo — COMPLETO, listo para pegar |

```powerfx
UpdateContext({ctxStaleChq: false, ctxConfirmReenvChq: false, ctxEnviandoChq: false});
Clear(colSelChq);                  // selección stale = doble envío (bug de la vieja)
If(
    IsEmpty(colChequesChq),
    Select(btnActualizarChq)       // primera visita: carga sola
);
Reset(drpEstadoChq);
Reset(galChequesChq)
```

> `Select()` desde OnVisible dispara el `OnSelect` del botón de la misma pantalla:
> **toda la lógica de carga vive en UN solo lugar** (`btnActualizarChq`) — es el fix
> de las 4 copias divergentes del query de la app vieja. Si ya hay datos (vuelta desde
> otra pantalla) no recarga; el usuario decide con Actualizar.
> `dtFechaChq` **NO se resetea** a propósito: la colección cargada corresponde a la
> fecha que muestra el picker; resetearla a `Today()` haría que el picker mienta sobre
> los datos en pantalla.

## 2. Layout responsive (01 §8)

| Control | Tipo | Propiedades |
|---|---|---|
| `cmpNavChq` | Componente de la app (`cmpNav`) | X `0` · Y `0` · Width `214` · Height `Parent.Height` |
| `cntBodyChq` | Contenedor vertical | X `cmpNavChq.Width` · Y `0` · Width `Parent.Width - cmpNavChq.Width` · Height `Parent.Height` · PaddingLeft/Right `26` · PaddingTop `22` · PaddingBottom `18` · LayoutGap `14` · Fill `Transparent` · Scroll vertical activado |

Hijos directos de `cntBodyChq` (auto-layout: **NO llevan X/Y**; el orden del árbol es el
orden vertical): `cntTituloChq`, `cardFiltrosChq`, `cntStaleChq`, `cntKpisChq`,
`cardChequesChq`.

## 3. Título

| Control | Tipo | Propiedades |
|---|---|---|
| `cntTituloChq` | Contenedor **vertical** | Height `52` · `LayoutMinHeight 52` · LayoutGap `3` · Fill `Transparent` — título ARRIBA, subtítulo DEBAJO (como el mockup) |
| `lblTituloChq` | Etiqueta clásica | ESTILO-TITULO · Text `"Cheques"` · Width `560` |
| `lblSubtituloChq` | Etiqueta clásica | ESTILO-SUBTITULO · Text `"Entrega de cheques a digitación. Solo se pueden seleccionar los cheques recontados."` · Width `560` |

## 4. Barra de filtros

| Control | Tipo | Propiedades |
|---|---|---|
| `cardFiltrosChq` | Contenedor manual (receta 01 §7.1) | Height `74` · `LayoutMinHeight 74` — hijos con X/Y |
| `lblFecFiltroChq` | Etiqueta clásica | X `16` · Y `10` · ESTILO-KPI-LABEL · Text `"FECHA"` |
| `dtFechaChq` | Selector de fecha **moderno** (excepción 01 §0.3) | X `16` · Y `30` · Width `200` · Height `34` · **`SelectedDate` `Today()`** (el moderno NO tiene `DefaultDate` — esa es del clásico) · OnChange `UpdateContext({ctxStaleChq: true}); Clear(colSelChq)` |
| `lblEstFiltroChq` | Etiqueta clásica | X `236` · Y `10` · ESTILO-KPI-LABEL · Text `"ESTADO"` |
| `drpEstadoChq` | Menú desplegable clásico | X `236` · Y `30` · Width `230` · Height `34` · Items `["Pendientes de enviar", "Enviados", "No recibidos", "Todos"]` · **Default `"Todos"`** · OnChange `Clear(colSelChq)` |
| `cirSyncChq` | Círculo clásico | X `cardFiltrosChq.Width - 320` · Y `40` · Width `8` · Height `8` · Fill `ThemeAccent` · BorderThickness `0` · **Visible `scrCheques.Width >= 1130`** |
| `lblSyncChq` | Etiqueta clásica | X `cardFiltrosChq.Width - 304` · Y `32` · Width `160` · Height `20` · Size `8.5` · Color `ThemeInk3` · **Visible `scrCheques.Width >= 1130`** (bajo ese ancho chocaría con el combo de Estado) · Text `If(IsBlank(ctxUltimaActChq), "Sin actualizar", "Actualizado " & Text(ctxUltimaActChq, "HH:mm:ss"))` |
| `btnActualizarChq` | Botón clásico | X `cardFiltrosChq.Width - 140` · Y `28` · Width `124` · Height `36` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · Text `"↻  Actualizar"` · FontWeight `Semibold` · OnSelect: ver abajo |

> **Por qué este `drp` no sufre el reseteo del clásico**: un Drop down clásico pierde su
> `Selected` cuando cambia su `Items`. Acá `Items` es una **lista literal constante** —
> no se reconstruye nunca desde una colección — así que la selección sobrevive a cada
> Actualizar. (Por eso tampoco hace falta capturarla en una `ctx` antes de recargar.)
>
> **Umbral de 1130**: `cardFiltrosChq.Width = scrCheques.Width - 266`, así que el punto
> de sync arranca en `scrCheques.Width - 586` y el combo de Estado termina en `466`;
> se pisarían bajo ~1052 px. Se apaga antes de que eso pase — el dato de "última
> actualización" es secundario, el filtro no.

> **El filtro de Estado NO va al servidor**: filtra la colección ya cargada (§7), así
> que cambiarlo **no marca stale** — sólo limpia la selección (una selección hecha en
> "Todos" que después se esconde detrás de "Enviados" es exactamente la selección
> fantasma que producía el doble envío). La **fecha SÍ** marca stale: cambia la ventana
> de la consulta remota.

### 4.1 `btnActualizarChq.OnSelect` — LA ÚNICA definición de la consulta

```powerfx
UpdateContext({ctxCargandoChq: true, ctxStaleChq: false});
Clear(colSelChq);
IfError(
    // Ventana ACOTADA (fix del OR sin cota que truncaba a 500):
    //   (a) TODO lo creado el día elegido, en cualquier estado, y
    //   (b) el ARRASTRE: sólo lo que sigue pendiente de acción (Recontado / No
    //       Recibido) de los últimos 30 días.
    // Todo el predicado es delegable: comparaciones de fecha + igualdad de choice,
    // combinadas con && / ||.
    ClearCollect(colChequesChq,
        Filter('CH-Tbl-Checks',
            (
                'Created On' >= Coalesce(dtFechaChq.SelectedDate, Today())
                && 'Created On' < DateAdd(Coalesce(dtFechaChq.SelectedDate, Today()), 1, TimeUnit.Days)
            )
            ||
            (
                (
                    CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado
                    || CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido'
                )
                && 'Created On' >= DateAdd(Coalesce(dtFechaChq.SelectedDate, Today()), -30, TimeUnit.Days)
                && 'Created On' < DateAdd(Coalesce(dtFechaChq.SelectedDate, Today()), 1, TimeUnit.Days)
            )
        )
    );
    UpdateContext({ctxUltimaActChq: Now()}),

    // rama de error del IfError
    Notify("No se pudieron cargar los cheques: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxCargandoChq: false});
Reset(galChequesChq)
```

> - **1 sola consulta remota** por actualización. `colChequesChq` guarda **registros
>   crudos del datasource** (sin `AddColumns`/`ShowColumns`): así conservan identidad y
>   el `Patch` del envío no necesita un `LookUp` extra por cheque (2N → N requests).
> - `< DateAdd(fecha, 1, Days)` — nunca `<= fecha` (00 §5, bug BL-02).
> - El apagado del overlay va **FUERA** del `IfError`: un fallo nunca deja la pantalla
>   bloqueada.
> - **Límite de filas**: Configuración → General → *Límite de filas de datos* = `2000`.
>   Un día de cheques + 30 días de pendientes queda muy por debajo; si alguna vez lo
>   rozara, el síntoma sería un contador de KPI corto — no un truncado silencioso a 500
>   como en la vieja.

## 5. Aviso "datos desactualizados"

| Control | Tipo | Propiedades |
|---|---|---|
| `cntStaleChq` | Contenedor manual | Height `40` · `LayoutMinHeight 40` · Fill `ThemeWarnSoft` · BorderColor `ThemeWarn` · BorderThickness `1` · Radius (las 4) `8` · **Visible `ctxStaleChq`** |
| `lblStaleChq` | Etiqueta clásica | X `14` · Y `10` · Width `620` · Size `9` · Color `ThemeWarn` · FontWeight `Semibold` · Text `"Cambiaste la fecha — los cheques de abajo corresponden a la selección anterior."` |
| `btnStaleChq` | Botón clásico | X `cntStaleChq.Width - 150` · Y `7` · Width `136` · Height `26` · Radius (las 4) `6` · Fill `Transparent` · Color `ThemeWarn` · BorderColor `ThemeWarn` · BorderThickness `1` · Size `9` · Text `"Actualizar ahora"` · OnSelect `Select(btnActualizarChq)` |

> En auto-layout un control con `Visible = false` libera su espacio — el aviso no deja
> hueco cuando está oculto.

## 6. Fila de KPIs (instancias de `cmpKpi`, 01 §7.5b)

| Control | Tipo | Propiedades |
|---|---|---|
| `cntKpisChq` | Contenedor horizontal | Height `If(scrCheques.Width < 1142, 204, 96)` · `LayoutMinHeight` **misma fórmula** (al envolver, la segunda fila necesita su alto — un contenedor de auto-layout **NO crece solo**) · **LayoutWrap `true`** · LayoutGap `12` · Fill `Transparent` |

> **De dónde sale el 1142**: el ancho interno del cuerpo es `scrCheques.Width - 214 - 52`
> (nav + paddings) y las 4 tarjetas suman `4 × 210 + 3 × 12 = 876` de mínimo → envuelven
> bajo `876 + 266 = 1142`. Con `Height` fijo en 96 la segunda fila quedaría recortada.

Las 4 instancias llevan `FillPortions 1` + `LayoutMinWidth 210` (regla 01 §8.4).
**Misma fuente que la grilla** (`colChequesChq`) — reconcilian por construcción.

| Instancia | KpiLabel | KpiValor | KpiPie | KpiAccent | KpiPieColor | OnKpiSelect |
|---|---|---|---|---|---|---|
| `cmpKpiPorEnviarChq` | `"POR ENVIAR"` | `Text(CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado)))` | `"cheques recontados"` | `ThemeAccent` | `ThemeInk3` | `false` |
| `cmpKpiNioChq` | `"MONTO · C$"` | `SimboloNIO & " " & Text(Coalesce(Sum(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado && CheckCurrency = 'CH-Ch-Currency'.NIO), CheckAmount), 0), FmtMoney)` | `"seleccionados: " & CountRows(Filter(colSelChq, CheckCurrency = 'CH-Ch-Currency'.NIO))` | `ThemeAccentMoney` | `ThemeInk3` | `false` |
| `cmpKpiUsdChq` | `"MONTO · US$"` | `SimboloUSD & " " & Text(Coalesce(Sum(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado && CheckCurrency = 'CH-Ch-Currency'.USD), CheckAmount), 0), FmtMoney)` | `"seleccionados: " & CountRows(Filter(colSelChq, CheckCurrency = 'CH-Ch-Currency'.USD))` | `ThemeAccentMoney` | `ThemeInk3` | `false` |
| `cmpKpiNoRecChq` | `"NO RECIBIDOS"` | `Text(CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')))` | `"para reenviar"` | `ThemeAccent` | `ThemeInk3` | `false` |

> - El **valor** de los KPI de monto suma lo que está **por enviar** (`Recontado`), no
>   lo seleccionado; el **pie** muestra cuántos hay seleccionados en esa moneda. Es
>   exactamente el comportamiento del prototipo (US$ 3.200 con "seleccionados: 0").
> - `Sum`/`CountRows`/`Filter` acá corren sobre **colecciones locales** — cero costo
>   remoto, cero riesgo de delegación (la vieja hacía `Sum(Filter(colChecksR,…))` sobre
>   una colección truncada a 500, que es distinto: el número estaba MAL). El `Coalesce`
>   de los montos es porque `Sum` sobre colección vacía devuelve *Blank* y
>   `Text(Blank(), FmtMoney)` sale vacío: sin él la tarjeta diría `C$` a secas.
> - Barra de las tarjetas de monto en `ThemeAccentMoney` (01 §3) — es la mezcla
>   accent/teal de `.kpi.money` del prototipo; las de conteo van en `ThemeAccent`.
> - No hay KPI de **€**: el prototipo trae 4 tarjetas (`k4`). Un cheque en EUR se ve en
>   la grilla con su símbolo, pero no tiene tarjeta propia. Declarado en las notas.

## 7. Grilla de cheques

| Control | Tipo | Propiedades |
|---|---|---|
| `cardChequesChq` | Contenedor manual (receta 01 §7.1) | `FillPortions 1` (ocupa el alto restante) · `LayoutMinHeight 300` · hijos con X/Y |
| `lblChqTituloChq` | Etiqueta clásica | X `16` · Y `12` · Width `200` · Height `20` · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` · Text `"Cheques del día y pendientes"` — **NO es ESTILO-SECCION**: el mockup da la cabeza del panel en sentence case y tinta fuerte |
| `pilSelCountChq` | **Botón clásico** (píldora, receta 01 §7.2) | X `220` · Y `10` · Width `Len(Self.Text) * 7 + 12` · Height `22` · DisplayMode `View` · Fill `ThemeSunk` · Color `ThemeInk3` · **BorderThickness `0`** (override de la receta — el mockup no lleva borde en el contador) · **Visible `scrCheques.Width >= 1100`** (ver la nota de geometría) · Text `CountRows(colSelChq) & " seleccionado" & If(CountRows(colSelChq) = 1, "", "s")` |
| `btnReenviarChq` | Botón clásico | Width `If(scrCheques.Width >= 1100, 216, 150)` · X `cardChequesChq.Width - 16 - If(scrCheques.Width >= 1100, 224, 160) - 8 - Self.Width` · Y `8` · Height `34` · Radius (las 4) `8` · Fill `ThemeSurface2` · Color `ThemeInk2` · BorderColor `ThemeLine` · BorderThickness `1` · HoverFill `ThemeSunk` · Size `9.5` · FontWeight `Semibold` · **Text `If(scrCheques.Width >= 1100, "↩  Reenviar " & CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')) & " no recibidos", "↩  " & CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')))`** · DisplayMode `If(CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')) = 0, DisplayMode.Disabled, DisplayMode.Edit)` · **OnSelect `UpdateContext({ctxConfirmReenvChq: true})`** (el reenvío real vive en §7.4, detrás de la confirmación de §7.3) |
| `btnEnviarChq` | Botón clásico | Width `If(scrCheques.Width >= 1100, 224, 160)` · X `cardChequesChq.Width - 16 - Self.Width` · Y `8` · Height `34` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · BorderThickness `0` · Size `9.5` · FontWeight `Semibold` · **Text `If(scrCheques.Width >= 1100, "→  Enviar " & CountRows(colSelChq) & " a digitación", "→  Enviar " & CountRows(colSelChq))`** · **DisplayMode `If(CountRows(colSelChq) = 0, DisplayMode.Disabled, DisplayMode.Edit)`** · OnSelect: §7.2 |

> **Geometría de la cabecera, recalculada entera** (`cardChequesChq.Width = scrCheques.Width - 266`,
> con la nav de 214 y los paddings de 26+26 del cuerpo):
> el contador termina en `220 + 7·Len + 12` ≈ **337** con `"0 seleccionados"`; con los
> botones anclados a la derecha, `btnReenviarChq.X = scrCheques.Width - 730`. Se pisaban
> **bajo 1067 px** — y 1024 es un ancho de ventana real, porque *Escalar para ajustar*
> está DESACTIVADO (01 §1) y `scrCheques.Width` es el ancho real. Por eso bajo 1100 px
> el contador se apaga (el mismo N sigue a la vista en el texto de Enviar) y los dos
> botones se encogen a texto corto: con eso `btnReenviarChq.X = scrCheques.Width - 600`
> y el choque con el título recién ocurriría a 816 px, fuera del rango soportado.

`shpBandHeadChq` (Rectángulo clásico — la banda gris del `thead` del mockup): X `16` ·
Y `54` · Width `cardChequesChq.Width - 32` · Height `24` · Fill `ThemeSurface2` ·
**al FONDO del z-order de los encabezados** · Visible `CountRows(colChequesChq) > 0`.

**Encabezados** (Etiquetas clásicas ESTILO-HEADER-COL, Y `58`, Height `18`; X/Width como
fracción del ancho de la galería — **nombrando la galería**, nunca `Self.Width`):

| Control | Text | X | Width | Align | Visible |
|---|---|---|---|---|---|
| `lblColCheckChq` | `""` | `16` | `34` | Left | `CountRows(colChequesChq) > 0` |
| `lblColNumeroChq` | `"CHEQUE"` | `16 + 34` | `galChequesChq.Width * 0.14` | Left | `CountRows(colChequesChq) > 0` |
| `lblColClienteChq` | `"CLIENTE"` | `16 + 34 + galChequesChq.Width * 0.14` | `galChequesChq.Width * 0.24` | Left | `CountRows(colChequesChq) > 0 && scrCheques.Width >= 1100` |
| `lblColTulaChq` | `"TULA"` | `16 + 34 + galChequesChq.Width * 0.38` | `galChequesChq.Width * 0.10` | Left | `CountRows(colChequesChq) > 0` |
| `lblColMontoChq` | `"MONTO"` | `16 + 34 + galChequesChq.Width * 0.48` | `galChequesChq.Width * 0.16` | **Right** | `CountRows(colChequesChq) > 0` |
| `lblColEstadoChq` | `"ESTADO"` | `16 + 34 + galChequesChq.Width * 0.64` | `galChequesChq.Width * 0.16` | Left | `CountRows(colChequesChq) > 0` |
| `lblColSellosChq` | `"SELLOS"` | `16 + 34 + galChequesChq.Width * 0.80` | `galChequesChq.Width * 0.16 - 34` | Left | `CountRows(colChequesChq) > 0 && scrCheques.Width >= 1100` |

> Los encabezados de CLIENTE y SELLOS llevan **el mismo `Visible`** que sus celdas: si
> no, bajo 1100 px quedarían dos títulos flotando sobre columnas vacías. Y los siete se
> apagan con la colección vacía, igual que `shpBandHeadChq` y `shpLineaHeadChq` — el
> estado vacío (§8) no comparte espacio con una cabecera huérfana.
> El `- 34` del ancho de SELLOS compensa el offset de la columna del check: los
> encabezados arrancan en `16 + 34` pero las fracciones se calculan sobre el ancho
> completo de la galería, así que sin ese ajuste la última columna desbordaría ~2 px
> justo en el breakpoint.

`shpLineaHeadChq` (Rectángulo clásico): X `16` · Y `78` · Width
`cardChequesChq.Width - 32` · Height `1` · Fill `ThemeLine` ·
Visible `CountRows(colChequesChq) > 0`.

**`galChequesChq`** (Galería vertical en blanco):

| Propiedad | Valor |
|---|---|
| X / Y | `16` / `80` |
| Width | `cardChequesChq.Width - 32` |
| Height | `cardChequesChq.Height - 96` |
| TemplateSize | `44` |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| Items | ver abajo |
| OnSelect | ver abajo (toggle de selección de la fila) |

```powerfx
// Items — filtro de estado y orden, TODO sobre la colección local.
// Orden por ACCIONABILIDAD (no por valor del option set: ordenar por valor dejaría
// 'No Recibido' — el estado sobre el que actúa Reenviar — ÚLTIMO, debajo de tres
// estados terminales). Clave secundaria: más reciente primero.
Sort(
    SortByColumns(
        Switch(drpEstadoChq.Selected.Value,
            "Pendientes de enviar", Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado),
            "Enviados",             Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'),
            "No recibidos",         Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido'),
            colChequesChq
        ),
        "createdon", SortOrder.Descending
    ),
    Switch(CheckStatus,
        'CheckStatus (CH-Tbl-Checks)'.Recontado,              1,
        'CheckStatus (CH-Tbl-Checks)'.'No Recibido',          2,
        'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',  3,
        'CheckStatus (CH-Tbl-Checks)'.Recibido,               4,
        'CheckStatus (CH-Tbl-Checks)'.Digitado,               5,
        'CheckStatus (CH-Tbl-Checks)'.Rebotado,               6,
        9),
    SortOrder.Ascending
)
```

> - El nombre de columna de `SortByColumns` es el **lógico**: `"createdon"` (columna de
>   sistema, **sin** prefijo `sanic_` — `"sanic_createdon"` no existe y dejaría la
>   galería en error de autor, tumbando la pantalla entera).
> - El orden primario va con `Sort(<expresión>)` y no con `SortByColumns` sobre
>   `CheckStatus`: **SortByColumns no ordena columnas de tipo choice**, y además el
>   orden que queremos no es el de los valores del option set.
> - El orden secundario (fecha desc dentro de cada estado) depende de que el `Sort`
>   externo conserve el orden de entrada dentro de cada grupo. **Verificar en Studio**;
>   si no lo hiciera, el impacto es cosmético — el agrupado por estado se mantiene.

```powerfx
// OnSelect de la GALERÍA (receta 01 §7.3: la lógica de fila vive acá, no en el overlay).
// Mismo toggle que el icono: clic en cualquier parte de la fila selecciona, con el
// MISMO guard de Recontado. No hay doble disparo — el icono no llama a Select(Parent).
If( ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado,
    If( !IsBlank(LookUp(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks')),
        RemoveIf(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks'),
        Collect(colSelChq, ThisItem) ) )
```

**Template** (X del template = X del encabezado − `16`, porque la galería arranca en
X `16`). **Todos los hijos llevan `OnSelect = Select(Parent)` — EXCEPTO `icoCheckChq`,
que es la excepción declarada de la receta 01 §7.4 y tiene OnSelect PROPIO.**

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaChq` | Botón clásico (overlay de clic, receta 01 §7.3) | X `0` · Y `0` · Width `galChequesChq.Width` · Height `44` (= TemplateSize) · Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · **AL FONDO del z-order** · OnSelect `Select(Parent)` |
| `icoCheckChq` | Icono (receta 01 §7.4) | X `7` · Y `12` · 20×20 · ver §7.1 |
| `lblCelNumeroChq` | Etiqueta clásica | ESTILO-MONO · X `34` · Y `12` · Width `galChequesChq.Width * 0.14` · Height `20` · Text `ThisItem.CheckNumber` · OnSelect `Select(Parent)` |
| `lblCelClienteChq` | Etiqueta clásica | ESTILO-CELDA · X `34 + galChequesChq.Width * 0.14` · Y `12` · Width `galChequesChq.Width * 0.24` · Height `20` · Text `Coalesce(ThisItem.Bag.ReceptionID.RemitentClient.Name, ThisItem.Bag.ReceptionID.RemitentName, "—")` · **Visible `scrCheques.Width >= 1100`** · OnSelect `Select(Parent)` |
| `lblCelTulaChq` | Etiqueta clásica | ESTILO-CELDA · Color `ThemeInk2` · X `34 + galChequesChq.Width * 0.38` · Y `12` · Width `galChequesChq.Width * 0.10` · Height `20` · Text `Coalesce(ThisItem.Bag.BagID, "—")` · OnSelect `Select(Parent)` |
| `lblCelMontoChq` | Etiqueta clásica | ESTILO-CELDA-NUM (FontWeight `Semibold`) · X `34 + galChequesChq.Width * 0.48` · Y `12` · Width `galChequesChq.Width * 0.16` · Height `20` · Align `Align.Right` · Text: ver §7.1 · OnSelect `Select(Parent)` |
| `pilCelEstadoChq` | **Botón clásico** (píldora, receta 01 §7.2) | X `34 + galChequesChq.Width * 0.64` · Y `11` · Width `Len(Self.Text) * 7 + 12` · Height `22` · DisplayMode `View` (lo impone la receta) · Text/Fill/Color: ver §7.1 · **OnSelect `Select(Parent)`** *(por contrato de 01 §7.3 — todo hijo del template delega; en modo Ver no llega a dispararse y el clic lo recoge `btnFilaChq`)* |
| `lblCelSellosChq` | Etiqueta clásica | ESTILO-CELDA · Size `9` · Color `ThemeInk2` (`.sub2` del mockup) · X `34 + galChequesChq.Width * 0.80` · Y `12` · Width `galChequesChq.Width * 0.16 - 34` · Height `20` · **Visible `scrCheques.Width >= 1100`** · Text: ver §7.1 · OnSelect `Select(Parent)` |
| `shpSepChq` | Rectángulo clásico | X `0` · Y `43` (= TemplateSize − 1) · Width `galChequesChq.Width` · Height `1` · Fill `ThemeLine` |

> **`Cliente` sin backend nuevo**: el `Coalesce` cae a **`RemitentName`** (`sanic_remitentname`,
> texto plano en `CH-Tbl-Receptions` — display name `RemitentName`, verificado en
> `Entity.xml`) cuando `RemitentClient.Name` no se puede leer. Esto importa porque el rol
> `CH-SR-CountingSupervisor` **hoy NO tiene `prvRead sanic_CHTblClient`** (00 §3.3): sin
> el fallback la columna saldría `"—"` en el 100% de las filas desde el día uno. Mismo
> patrón que usa la guía 03 en `lblKvClienteApr`.
>
> `Coalesce` cubre **Blank**, no **Error**: si la falta del privilegio se manifestara
> como error de expansión del lookup (y no como blank), la celda quedaría en error sin
> llegar nunca al fallback. Por eso el `Text` de `lblCelClienteChq` va envuelto:
> ```powerfx
> IfError(
>     Coalesce(ThisItem.Bag.ReceptionID.RemitentClient.Name, ThisItem.Bag.ReceptionID.RemitentName, "—"),
>     Coalesce(ThisItem.Bag.ReceptionID.RemitentName, "—")
> )
> ```

### 7.1 Fórmulas del template

**`icoCheckChq`** (receta 01 §7.4 — resuelta acá, sin placeholders):

| Propiedad | Valor |
|---|---|
| Icon | `If(ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado, Icon.CheckBadge, Icon.Blocked)` |
| Color | `If(!IsBlank(LookUp(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks')), ThemeAccent, ThemeLineStrong)` |
| DisplayMode | `If(ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado, DisplayMode.Edit, DisplayMode.Disabled)` |
| DisabledColor | `ThemeLine` |
| AccessibleLabel | `"Seleccionar cheque " & ThisItem.CheckNumber` |
| OnSelect | ver abajo — **propio, NO `Select(Parent)`** |

```powerfx
// icoCheckChq.OnSelect — el guard de selección deja de ser cosmético:
// sólo Recontado entra a colSelChq (CheckGuard rechazaría cualquier otro).
If( ThisItem.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado,
    If( !IsBlank(LookUp(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks')),
        RemoveIf(colSelChq, 'CH-Tbl-Checks' = ThisItem.'CH-Tbl-Checks'),
        Collect(colSelChq, ThisItem) ) )
```

**`lblCelMontoChq.Text`** (símbolo por moneda, `FmtMoney` — el `#,###` de la vieja
mostraba `C$ .00`):

```powerfx
Switch(ThisItem.CheckCurrency,
    'CH-Ch-Currency'.NIO, SimboloNIO,
    'CH-Ch-Currency'.USD, SimboloUSD,
    'CH-Ch-Currency'.EUR, SimboloEUR,
    ""
) & " " & Text(ThisItem.CheckAmount, FmtMoney)
```

**`pilCelEstadoChq`** — un `Switch` por propiedad (los 6 miembros del option set, no
sólo los 4 del prototipo):

```powerfx
// Text
Switch(ThisItem.CheckStatus,
    'CheckStatus (CH-Tbl-Checks)'.Recontado,               "Recontado",
    'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',  "Enviado",
    'CheckStatus (CH-Tbl-Checks)'.'No Recibido',           "No recibido",
    'CheckStatus (CH-Tbl-Checks)'.Recibido,                "Recibido",
    'CheckStatus (CH-Tbl-Checks)'.Digitado,                "Digitado",
    'CheckStatus (CH-Tbl-Checks)'.Rebotado,                "Rebotado",
    "Sin estado")
```
```powerfx
// Fill
Switch(ThisItem.CheckStatus,
    'CheckStatus (CH-Tbl-Checks)'.Recontado,               ThemePosSoft,
    'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',  ThemeAccentSoft,
    'CheckStatus (CH-Tbl-Checks)'.'No Recibido',           ThemeNegSoft,
    'CheckStatus (CH-Tbl-Checks)'.Recibido,                ThemeAccent,
    'CheckStatus (CH-Tbl-Checks)'.Rebotado,                ThemeDangerSoft,
    ThemeSunk)
```
```powerfx
// Color
Switch(ThisItem.CheckStatus,
    'CheckStatus (CH-Tbl-Checks)'.Recontado,               ThemePos,
    'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',  ThemeAccent,
    'CheckStatus (CH-Tbl-Checks)'.'No Recibido',           ThemeNeg,
    'CheckStatus (CH-Tbl-Checks)'.Recibido,                ThemeAccentInk,
    'CheckStatus (CH-Tbl-Checks)'.Rebotado,                ThemeDanger,
    ThemeInk3)
```

> `Digitado` y "Sin estado" caen al default neutro `ThemeSunk`/`ThemeInk3` — que es la
> variante con borde de la receta 01 §7.2 (`BorderThickness = If(Self.Fill = ThemeSunk, 1, 0)`).

**`lblCelSellosChq.Text`** (`FmtFechaCorta` = `"dd/mm HH:mm"`, **24 h** — ledger L-5;
`"—"` cuando el sello está en blanco):

```powerfx
Switch(ThisItem.CheckStatus,
    'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',
        If(IsBlank(ThisItem.'Enviado el'), "—", "Env. " & Text(ThisItem.'Enviado el', FmtFechaCorta)),
    'CheckStatus (CH-Tbl-Checks)'.'No Recibido',
        If(IsBlank(ThisItem.'No recibido el'), "—", "No rec. " & Text(ThisItem.'No recibido el', FmtFechaCorta)),
    'CheckStatus (CH-Tbl-Checks)'.Recibido,
        If(IsBlank(ThisItem.'Recibido el'), "—", "Rec. " & Text(ThisItem.'Recibido el', FmtFechaCorta)),
    "—")
```

### 7.2 `btnEnviarChq.OnSelect` — COMPLETO

```powerfx
UpdateContext({ctxCargandoChq: true, ctxEnviandoChq: true});

// 1) CINTURÓN: aunque el icono sólo deja marcar Recontado, la colección pudo quedar
//    stale (otro supervisor envió el cheque hace 40 segundos). Sólo Recontado sale.
ClearCollect(colEnviarChq,
    Filter(colSelChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recontado)
);
UpdateContext({ctxDescartadosChq: CountRows(colSelChq) - CountRows(colEnviarChq)});

// 2) UN Patch por cheque, cada uno envuelto en su IfError: se cuentan éxitos y
//    fallos REALES (la vieja hacía ForAll(Patch(...)) sin IfError: fallos mudos).
//    IfError(Valor, Fallback, Default): con 3 argumentos, el último se devuelve
//    cuando NO hubo error.
ClearCollect(colEnvioResChq,
    ForAll(colEnviarChq As C,
        IfError(
            Patch('CH-Tbl-Checks', C,
                { CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion' }
            ),
            { OkChq: false, MsgChq: FirstError.Message },
            { OkChq: true,  MsgChq: "" }
        )
    )
);
UpdateContext({
    ctxEnviadosChq:   CountRows(Filter(colEnvioResChq, OkChq)),
    ctxRechazadosChq: CountRows(Filter(colEnvioResChq, !OkChq))
});

// 3) Los sellos ('Enviado el') los estampa el plugin server-side: hay que RELEER.
//    La recarga pasa por el ÚNICO camino de consulta — que además hace Clear(colSelChq).
Refresh('CH-Tbl-Checks');
Select(btnActualizarChq);

// 4) Notificación HONESTA (la vieja no notificaba nada en Enviar)
Notify(
    ctxEnviadosChq & " enviado(s) a digitación"
    & If(ctxRechazadosChq > 0,
         " · " & ctxRechazadosChq & " rechazado(s) por el servidor: "
         & Coalesce(LookUp(colEnvioResChq, !OkChq, MsgChq), ""),
         "")
    & If(ctxDescartadosChq > 0,
         " · " & ctxDescartadosChq & " descartado(s) por no estar en Recontado",
         ""),
    If(ctxRechazadosChq > 0, NotificationType.Warning, NotificationType.Success)
);
UpdateContext({ctxCargandoChq: false, ctxEnviandoChq: false})
```

> **Por qué `ForAll` + `IfError` y NO `Patch(ds, ForAll(...))` masivo**: el Patch masivo
> manda un lote y devuelve **un** error agregado — no se puede decir "4 enviados, 1
> rechazado", que es justo el requisito. Con N `Patch` individuales pagamos N requests
> (N = cheques seleccionados, decenas como mucho) y ganamos el conteo real.
> **Decidido y declarado.**
>
> El overlay se apaga en la última línea, **fuera de cualquier rama de error**: como
> cada `Patch` está atajado por su `IfError`, ningún error escapa y la línea final
> siempre corre.
>
> `Patch('CH-Tbl-Checks', C, {...})` usa el registro tal cual vino de la colección
> (por eso `colChequesChq` NO lleva `AddColumns`). *Si Studio se quejara de identidad
> del registro*, el segundo argumento pasa a ser
> `LookUp('CH-Tbl-Checks', 'CH-Tbl-Checks' = C.'CH-Tbl-Checks')` — cuesta un request
> más por cheque.
>
> *Si Studio rechazara el `IfError` por unificación de tipos* (el `Value1` devuelve un
> registro de `CH-Tbl-Checks` y las otras dos ramas un registro de 2 campos), descartá
> el valor del `Patch` encadenando un booleano:
> ```powerfx
> IfError( Patch('CH-Tbl-Checks', C, { CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion' }); true,
>          { OkChq: false, MsgChq: FirstError.Message },
>          { OkChq: true,  MsgChq: "" } )
> ```

### 7.3 Confirmación del reenvío masivo (`cntConfirmChq`)

El reenvío **no opera sobre la selección**: barre TODOS los `No Recibido` de la ventana
cargada — que incluye el arrastre de 30 días y, por lo tanto, cheques que el filtro de
Estado puede tener escondidos en ese momento. Una escritura masiva cuyo alcance no está
a la vista es exactamente el pecado que esta guía le reprocha a la app vieja, así que
el alcance se muestra en el `Text` del botón **y** se confirma antes de escribir.

**Es un diálogo MODAL de verdad**: velo a pantalla completa. Sin el velo, la cabecera de
la card (que vive en `Y 8`, muy por encima del diálogo centrado) queda clickeable y el
usuario puede enviar la selección, cambiar la fecha o recargar **con la confirmación
abierta** — y después confirmar sobre una `colChequesChq` que ya no es la que vio. Mismo
patrón que `cntConfirmApr` en la guía 03.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntConfirmChq` | Contenedor manual (velo de pantalla — NO lleva prefijo `card`: no es superficie blanca) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxConfirmReenvChq`** · por encima de `cntBodyChq` y **por debajo de `cntCargandoChq`** en el z-order de la pantalla |
| `cardConfirmChq` | Contenedor manual (receta 01 §7.1) | X `(cntConfirmChq.Width - 460) / 2` · Y `(cntConfirmChq.Height - 176) / 2` · Width `460` · Height `176` · Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius (las 4) `10` · DropShadow `DropShadow.Light` — hijos con X/Y relativos a ÉL |
| `lblConfirmChq` | Etiqueta clásica | X `20` · Y `18` · Width `420` · **Height `84`** · Size `10.5` · Color `ThemeInk` · Wrap `true` · Text `"Se van a reenviar a digitación los " & CountRows(Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')) & " cheques No recibido de la ventana cargada (el día elegido más los pendientes de los últimos 30 días), estén o no visibles con el filtro actual."` |
| `btnConfirmNoChq` | Botón clásico | X `200` · Y `118` · Width `110` · Height `34` · Radius (las 4) `8` · Fill `Transparent` · Color `ThemeInk2` · BorderColor `ThemeLine` · BorderThickness `1` · Size `9.5` · Text `"Cancelar"` · OnSelect `UpdateContext({ctxConfirmReenvChq: false})` |
| `btnConfirmSiChq` | Botón clásico | X `320` · Y `118` · Width `130` · Height `34` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · BorderThickness `0` · Size `9.5` · FontWeight `Semibold` · Text `"Sí, reenviar"` · OnSelect: **§7.4** |

> **Alto del label, calculado**: el texto con N de 2 dígitos son ~179 caracteres; a Size
> 10.5 entran ~57 por línea en 420 px de ancho → 4 líneas ≈ 76 px. Con `Height 60` se
> cortaba la última línea, que es justo *"estén o no visibles con el filtro actual"* —
> la frase por la que existe el diálogo. De ahí `Height 84` y la card de 176.

### 7.4 `btnConfirmSiChq.OnSelect` — el reenvío masivo, COMPLETO

*(Los controles de este botón se crean en §7.3. `btnReenviarChq` sólo abre el diálogo.)*

```powerfx
UpdateContext({ctxConfirmReenvChq: false, ctxCargandoChq: true, ctxEnviandoChq: true});

// Sólo 'No Recibido' → 'Enviado a digitacion' es reenvío legal (CheckGuard).
// Se toma de la colección cargada, NO de la selección: los No Recibido salen
// deshabilitados en la grilla y nunca entran a colSelChq.
ClearCollect(colReenviarChq,
    Filter(colChequesChq, CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido')
);

ClearCollect(colEnvioResChq,
    ForAll(colReenviarChq As C,
        IfError(
            Patch('CH-Tbl-Checks', C,
                { CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion' }
            ),
            { OkChq: false, MsgChq: FirstError.Message },
            { OkChq: true,  MsgChq: "" }
        )
    )
);
UpdateContext({
    ctxEnviadosChq:   CountRows(Filter(colEnvioResChq, OkChq)),
    ctxRechazadosChq: CountRows(Filter(colEnvioResChq, !OkChq)),
    ctxDescartadosChq: 0
});

Refresh('CH-Tbl-Checks');
Select(btnActualizarChq);

Notify(
    ctxEnviadosChq & " reenviado(s) a digitación"
    & If(ctxRechazadosChq > 0,
         " · " & ctxRechazadosChq & " rechazado(s) por el servidor: "
         & Coalesce(LookUp(colEnvioResChq, !OkChq, MsgChq), ""),
         ""),
    If(ctxRechazadosChq > 0, NotificationType.Warning, NotificationType.Success)
);
UpdateContext({ctxCargandoChq: false, ctxEnviandoChq: false})
```

> La vieja mostraba `"Se reenviaron N cheque(s)"` con N **contado ANTES de patchear** y
> `NotificationType.Success` incondicional: si el guard rechazaba los N, el usuario
> igual leía "se reenviaron N". Acá N es el conteo de `Patch` que **volvieron sin
> error**, y con rechazos la notificación es `Warning`.
>
> `ctxConfirmReenvChq` se apaga en la **primera** línea, en el mismo `UpdateContext` que
> prende el overlay de carga: así no queda ni un frame sin feedback visual. (La guía 03
> lo apaga al final; acá se declara la diferencia a propósito — el `OnSelect` corre
> completo aunque el control que lo disparó se oculte.)

## 8. Estado vacío

Los tres controles con
**Visible `CountRows(galChequesChq.AllItems) = 0 && !ctxCargandoChq`**, centrados en
`cardChequesChq`:

| Control | Tipo | Propiedades |
|---|---|---|
| `icoVacioChq` | Icono | X `cardChequesChq.Width / 2 - 16` · Y `cardChequesChq.Height / 2 - 60` · Width `32` · Height `32` · Icon `Icon.Journal` · Color `ThemeInk3` |
| `lblVacioTituloChq` | Etiqueta clásica | X `16` · Y `cardChequesChq.Height / 2 - 18` · Width `cardChequesChq.Width - 32` · Height `22` · Size `12` · FontWeight `Semibold` · Color `ThemeInk2` · Align `Align.Center` · Text `If(CountRows(colChequesChq) = 0, "No hay cheques de esta fecha ni pendientes de los últimos 30 días", "Ningún cheque en este estado")` |
| `lblVacioTextoChq` | Etiqueta clásica | X `16` · Y `cardChequesChq.Height / 2 + 6` · Width `cardChequesChq.Width - 32` · Height `40` · Size `9.5` · Color `ThemeInk3` · Align `Align.Center` · Wrap `true` · Text `If(CountRows(colChequesChq) = 0, "Probá otra fecha: los cheques Recontado y No recibido de los últimos 30 días se arrastran solos.", "Cambiá el filtro de Estado o elegí «Todos».")` |

> "Centrado" **no es una propiedad de canvas**: los tres controles llevan X/Y/Width/
> Height explícitos y el centrado se logra con `Align.Center` + el ancho completo de la
> card.

> `galChequesChq.AllItems` sobre una galería alimentada por una **colección local**
> devuelve todas las filas filtradas (el windowing de AllItems muerde con datasources
> remotos, no acá) — por eso el estado vacío distingue "no hay datos" de "el filtro de
> estado no deja pasar nada", en vez de dejar la grilla en blanco sin explicación.

## 9. Overlay de carga

| Control | Tipo | Propiedades |
|---|---|---|
| `cntCargandoChq` | Contenedor manual (receta 01 §7.6) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxCargandoChq`** · último en el z-order de la pantalla |
| `lblCargandoChq` | Etiqueta clásica | X `0` · Y `Parent.Height / 2 - 12` · Width `Parent.Width` · Height `24` · Size `12` · Color `Color.White` · Align `Align.Center` · Text `If(ctxEnviandoChq, "Enviando cheques...", "Actualizando...")` |

> El texto se decide con la bandera dedicada `ctxEnviandoChq` (se prende y se apaga en
> §7.2/§7.4), **no** con los contadores de resultado: esos quedan > 0 después del primer
> envío y harían que todo Actualizar posterior dijera "Enviando cheques…".

## 10. Clasificación de delegación

| Fórmula | Dónde | Clase |
|---|---|---|
| `Filter('CH-Tbl-Checks', <fechas> && <choices>)` de §4.1 | `btnActualizarChq.OnSelect` | **DELEGABLE** — comparaciones de fecha + igualdad de choice con `&&`/`||`. 1 request. Cota: 1 día + 30 días de pendientes; límite de filas `2000` |
| `Switch(drpEstadoChq…, Filter(colChequesChq, …))` + `SortByColumns` | `galChequesChq.Items` | **LOCAL** — colección en memoria, sin request |
| `CountRows`/`Sum`/`Filter` de los 4 KPI y del pill | `cmpKpi…Chq`, `pilSelCountChq` | **LOCAL** — misma colección que la grilla |
| `LookUp(colSelChq, …)` del icono y del toggle | template | **LOCAL** — colección de selección |
| `CountRows(Filter(colChequesChq, CheckStatus = …'No Recibido'))` | `btnReenviarChq.Text` / `.DisplayMode`, `lblConfirmChq.Text`, `cmpKpiNoRecChq` | **LOCAL** — el contador del botón, el del diálogo y el del KPI son la MISMA expresión sobre la MISMA colección: no pueden divergir |
| `CountRows(galChequesChq.AllItems)` | `icoVacioChq`, `lblVacioTituloChq`, `lblVacioTextoChq` (§8) | **LOCAL** — `AllItems` sobre galería alimentada por colección |
| `ThisItem.Bag.BagID` (columna TULA) | template | **1 salto por fila** sobre el registro ya cargado — expansión de lookup en cliente. Aceptado: sólo las filas visibles |
| `Coalesce(ThisItem.Bag.ReceptionID.RemitentClient.Name, ThisItem.Bag.ReceptionID.RemitentName, "—")` (columna CLIENTE) | template | **2-3 saltos por fila** — el costo más alto de la pantalla. Se paga sólo con `scrCheques.Width >= 1100` y sólo en filas visibles. El fallback `RemitentName` es texto plano en Receptions (2 saltos) y es el que va a servir mientras el rol no tenga `prvRead sanic_CHTblClient` |
| `Patch('CH-Tbl-Checks', C, {...})` en `ForAll` | §7.2 / §7.4 | **ESCRITURA, N requests** — no delegable por definición; cada uno atajado con `IfError` |
| `Refresh('CH-Tbl-Checks')` | §7.2 / §7.4 | invalida caché; la lectura real la hace `Select(btnActualizarChq)` |

**PROHIBIDO en esta pantalla** (y verificado que no aparece): `Search`, `StartsWith`,
`Upper()`, `Sum`/`CountRows` sobre datasource, `AddColumns` sobre datasource,
dot-walk dentro de un `Filter` remoto, `RecordCanSelect` y cualquier control de
dataset o de terceros. Si alguna vez hiciera falta filtrar por la tula, la forma
delegable es `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = X.'CH-Tbl-Bags')` — **nunca**
`Bag = <record>`, que revienta si el registro viene de un `AddColumns`.

### 10.1 Decisión declarada: NO se materializan Tula/Cliente con `AddColumns`

La app vieja resolvía Tula y Cliente con
`AddColumns(Filter(...), BagID, RelatedReCounterDepositID.BagID.BagID, Cliente, …)`
**en la carga**. Acá se descartó, por dos razones:

1. **`AddColumns` expande TODAS las filas traídas, no las visibles.** Con 1 día + 30
   días de arrastre, eso es exactamente el N+1 que hundía la pantalla vieja. El
   dot-walk en el `Text` de la celda sólo expande las filas **renderizadas** (una
   docena) — y `Cliente`, que es el caro, además está oculto bajo 1100 px.
2. **`AddColumns` rompe la identidad del registro para `Patch`.** Con la colección
   pura, `Patch('CH-Tbl-Checks', C, {...})` es 1 request por cheque; con columnas
   agregadas habría que resolver cada registro con un `LookUp` previo (2 requests por
   cheque) o arriesgar el error de "columna inexistente" al patchear.

**Sin backend nuevo la columna igual funciona**: el `Coalesce` a `RemitentName`
(2 saltos, texto plano, sin privilegios extra) la hace legible hoy. B-6 (abajo) es una
optimización opcional, no un bloqueante.

> **Notas de la guía**
>
> - **Backend B-1 (`sanic_bag`)**: las columnas TULA y CLIENTE cuelgan del espejo
>   `Bag` en `CH-Tbl-Checks` — **columna VIVA en el entorno**, ausente del `Entity.xml`
>   del repo (00 §2). El display name (`Bag`) se confirma en IntelliSense; **si el suyo
>   difiere, el suyo manda**. *Fallback sin backend* (si IntelliSense no la muestra ni
>   refrescando el datasource): `ThisItem.RelatedReCounterDepositID.BagID.BagID` para
>   Tula y `ThisItem.RelatedReCounterDepositID.BagID.ReceptionID.RemitentClient.Name`
>   para Cliente — un salto más caro cada una.
> - **Permisos y columna CLIENTE**: `RemitentClient.Name` requiere
>   `prvRead sanic_CHTblClient`, que el rol `CH-SR-CountingSupervisor` **hoy NO tiene**
>   (00 §3.3) — sin fallback la columna saldría `"—"` en todas las filas. Por eso el
>   `Coalesce` cae a **`RemitentName`** (`sanic_remitentname`, texto plano en
>   `CH-Tbl-Receptions`), igual que hace la guía 03. **No hace falta backend nuevo.**
> - **Backend B-6 (OPCIONAL, optimización — no bloquea)**: columna espejo
>   **`sanic_clientname`** (texto) en `sanic_CHTblChecks`, estampada por el
>   `CheckMirrorStampPlugin` que **ya navega ese mismo camino** para `sanic_bag`. Con
>   ella la columna CLIENTE pasa de 2-3 saltos por fila a una lectura plana y podría
>   mostrarse en todos los anchos. Sin ella la pantalla funciona igual.
> - **Sellos**: el canvas **no escribe fechas**. `'Enviado el'`, `'No recibido el'` y
>   `'Recibido el'` (`sanic_SentToDigitizationOn` / `sanic_NotReceivedOn` /
>   `sanic_ReceivedOn`) los estampa el plugin; por eso el envío hace `Refresh` +
>   recarga antes de que el usuario los lea.
> - **Divergencias declaradas vs mockup** (además del ledger 00 §4):
>   1. **Botón "↻ Actualizar" agregado** a la barra de filtros: el prototipo de esta
>      pantalla sólo tenía el punto de sync. Es el ÚNICO camino de carga
>      (`Select(btnActualizarChq)` desde OnVisible y desde los dos envíos) y lo exige la
>      receta 01 §7.6 para poder apagar el aviso de desactualizado.
>   2. **Default del filtro de Estado = "Todos"**: el `<select>` del prototipo mostraba
>      "Pendientes de enviar" pero su tabla listaba los 6 cheques de todos los estados.
>      Se resuelve la contradicción a favor de la tabla.
>   3. **Chip "Recibido" en verde sólido** (`ThemeAccent`/`ThemeAccentInk`): en el CSS
>      del prototipo `.chip` y `.chip.blue` son **el mismo color** (`pos-soft`/`pos`),
>      con lo que "Recontado" y "Recibido" quedaban indistinguibles. Recibido es el
>      estado terminal (congela el registro) y se marca distinto.
>   4. **Chips de `Digitado` y `Rebotado`**: el prototipo mostraba 4 estados; el option
>      set tiene **6**. Se agregan neutro y `ThemeDangerSoft`/`ThemeDanger`
>      respectivamente (el modelo manda sobre el mockup).
>   5. **Orden de la grilla por ACCIONABILIDAD**: Recontado → No recibido → Enviado →
>      Recibido → Digitado → Rebotado, y dentro de cada grupo el más reciente primero.
>      El prototipo tenía un orden manual de maqueta; ordenar por el valor del option
>      set dejaría `No Recibido` último, debajo de tres estados terminales.
>   6. **Columna SELLOS con fecha y hora** (`dd/mm HH:mm`), no sólo hora como en la
>      maqueta: con 30 días de arrastre, "09:12" sin día es ambiguo. Ledger L-5 fija el
>      `HH` de 24 h.
>   7. **Checkbox cuadrado → Icono** `CheckBadge`/`Blocked` (receta 01 §7.4). El motivo
>      **no** es que falte una casilla clásica — existe (Clásico → *Casilla*): es que
>      la casilla mantiene **estado interno propio** por fila y se desincroniza de
>      `colSelChq`, que es justamente la fuente única de verdad de la selección
>      (01 §7.4). *Costo asumido*: el prototipo distinguía marcado/no marcado por FORMA
>      (caja vacía → caja verde con ✓); el icono lo distingue sólo por COLOR. Mejorarlo
>      (círculo/cuadro + icono superpuesto) toca la receta **01 §7.4**, compartida —
>      no se cambia desde una guía de pantalla; queda anotado para el orquestador.
>   8. **Clic en toda la fila selecciona**: el prototipo sólo togglea desde el checkbox
>      (sus filas de cheques no llevan `.rowclick`). La receta 01 §7.3 exige que la
>      lógica de fila viva en `gal.OnSelect`, y el mismo guard de `Recontado` aplica en
>      los dos caminos. Si en pruebas resultara propenso a selección accidental, se
>      revierte poniendo `btnFilaChq.OnSelect = false` y dejando el toggle sólo en el
>      icono.
>   9. **"Reenviar no recibidos" se deshabilita** cuando no hay ninguno, **lleva el
>      contador en el texto** (`"↩ Reenviar N no recibidos"`) **y pide confirmación**
>      (§7.3, modal con velo). El prototipo sólo deshabilitaba el botón de Enviar y no
>      confirmaba nada.
>      Motivo: es la única escritura de la pantalla cuyo alcance NO es la selección
>      visible — barre los `No Recibido` de toda la ventana cargada, incluidos los que
>      el filtro de Estado esconde. Una escritura masiva ciega es el pecado que esta
>      misma guía le reprocha a la app vieja.
>   10. **Sin KPI de €**: el prototipo trae 4 tarjetas. Los cheques en EUR se ven en la
>      grilla con su símbolo pero no tienen tarjeta.
>   11. **Monto en `Semibold`** (ESTILO-CELDA-NUM de 01 §6): en el prototipo las celdas
>      del `tbody` son de peso normal. Es convención de sistema de la app, no del
>      prototipo — se deja por coherencia con las otras tres pantallas.
>   12. **Título del panel: "Cheques del día y pendientes"** (el prototipo decía
>      "Cheques del día"). La consulta trae el día elegido **más** el arrastre de 30
>      días de pendientes: el título del prototipo mentiría sobre el alcance de la
>      grilla. Mismo motivo en el texto del estado vacío.
> - **Responsive** (01 §8), **cuatro** umbrales, todos EN VIVO (nunca congelados en
>   variable):
>   **1142** — los KPIs envuelven a 2 filas (`LayoutWrap` + `LayoutMinWidth 210`) y
>   `cntKpisChq` crece a 204 de alto, porque un contenedor de auto-layout no crece solo;
>   **1130** — se apagan `cirSyncChq`/`lblSyncChq` antes de que choquen con el combo de
>   Estado; **1100** — se ocultan las columnas de baja prioridad **CLIENTE** (la cara) y
>   **SELLOS**, celda **y encabezado** con el mismo `Visible`; **1100** también apaga
>   `pilSelCountChq` y encoge los dos botones de la cabecera (sin eso se solapaban a
>   1024 px). Regla aprendida en revisión: **si se mueve un control de una fila, se
>   recalcula la fila entera**, no sólo el que se tocó.
> - **Concurrencia**: dos supervisores enviando el mismo cheque a la vez no se pisan —
>   el segundo `Patch` vuelve con el mensaje del `CheckGuard`
>   (*"Transicion de estado de cheque no permitida…"*) y se cuenta como rechazado. El
>   `App.OnError` de 01 §4 **no** se dispara: el `IfError` ya lo atajó.

## 11. Checklist de salida

- [ ] Al entrar por primera vez la pantalla CARGA SOLA (sin apretar Actualizar) y
      muestra "Actualizado HH:mm:ss".
- [ ] El check **sólo** se puede marcar en filas `Recontado`; en `Enviado`,
      `No recibido`, `Recibido`, `Digitado` y `Rebotado` sale gris y no responde
      (ni por el icono ni haciendo clic en la fila).
- [ ] Clic en cualquier parte de la fila de un `Recontado` marca/desmarca igual que el
      icono, y **una sola vez** (no hay doble toggle).
- [ ] El pill dice "1 seleccionado" / "N seleccionados" y el botón dice
      "→ Enviar N a digitación"; con N = 0 el botón está deshabilitado.
- [ ] Los pies de los KPI de monto muestran cuántos hay seleccionados por moneda y
      cambian al marcar/desmarcar.
- [ ] Cambiar la **fecha** muestra el aviso ámbar y **borra la selección**; cambiar el
      **Estado** borra la selección **sin** aviso (filtra local).
- [ ] Enviar 3 cheques: notificación "3 enviado(s) a digitación", la selección queda
      vacía, la grilla se recarga y esos cheques pasan a "Enviado" con su sello
      `Env. dd/mm HH:mm`.
- [ ] Enviar un cheque que otro supervisor ya envió (cambiarlo por la Web API entre la
      carga y el envío): la notificación dice "0 enviado(s) · 1 rechazado(s) por el
      servidor: …" en **amarillo**, con el mensaje del guard — nunca un éxito falso.
- [ ] "Reenviar N no recibidos" muestra el MISMO N que el KPI "No recibidos" y está
      deshabilitado con N = 0.
- [ ] Con N ≥ 2 y el filtro en "Enviados" (o sea, con los No recibido fuera de la
      vista): apretar Reenviar abre la confirmación que dice cuántos y de qué ventana,
      **con el texto completo visible hasta "…estén o no visibles con el filtro actual"**;
      "Cancelar" no escribe NADA; "Sí, reenviar" los pasa a "Enviado" y notifica el
      conteo real.
- [ ] Con la confirmación abierta, el **velo bloquea el resto de la pantalla**: no se
      puede apretar Enviar, ni Actualizar, ni cambiar la fecha por detrás del diálogo.
- [ ] Montos en cero se ven `C$ 0.00` (nunca `C$ .00`); los sellos en blanco muestran `—`.
- [ ] Fecha sin cheques: estado vacío con mensaje, **sin** cabecera de columnas ni línea
      colgando encima; filtro de estado sin resultados: "Ningún cheque en este estado".
- [ ] Desconectar la red y Actualizar: `Notify` de error y el overlay se **APAGA**.
- [ ] La columna **CLIENTE** muestra un nombre real, no `"—"` en el 100% de las filas:
      con el rol `CH-SR-CountingSupervisor` sin `prvRead sanic_CHTblClient` debe verse
      el `RemitentName` de la recepción.
- [ ] Achicar la ventana **hasta 1024 px** y recorrer los cuatro umbrales: bajo 1142 los
      KPIs envuelven a 2 filas **y la fila crece a 204 px** (ninguna tarjeta recortada);
      bajo 1130 desaparece el "Actualizado HH:mm:ss" sin pisar el combo de Estado; bajo
      1100 se ocultan CLIENTE y SELLOS **con sus encabezados**, se apaga el contador de
      seleccionados y los dos botones de la cabecera se encogen. **Nada se superpone en
      ningún ancho entre 1024 y 1366.**
- [ ] App Checker: cero errores y **cero warnings de delegación en
      `btnActualizarChq.OnSelect`** — el `Filter` de §4.1 es la ÚNICA fórmula remota de
      la pantalla. Si Studio marcara el `||` de nivel superior, partirlo en dos pasos
      sobre la misma colección: `ClearCollect` de la ventana del día + `Collect` del
      arrastre, cada uno con predicado puramente `&&`.
