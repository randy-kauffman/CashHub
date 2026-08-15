# Guía 02 — scrResumen (Resumen de operación)

Conciliación del recuento del día por sesión de cajero. Reemplaza a
`Screen OperationSummary` (que tenía la cascada N+1 de ~973 requests, la pantalla
que llegaba vacía y los montos `C$ .00`). Acá: carga sola al entrar, **1 + S + S + B
consultas delegables** (S sesiones, B tulas del día), y una sola fuente de datos
para KPIs y tabla — reconcilian por construcción.

**CONTRATO DE ENTRADA**: ninguno. La pantalla es autosuficiente: no lee `gvar` ni
colecciones de otras pantallas. (La nav llega por `Navigate()` simple.)

**Variables/colecciones propias**: `ctxCargandoRes`, `ctxStaleRes`, `ctxUltimaActRes`,
`colSesDiaRes`, `colSesionesRes`, `colBagsRes`, `colDetallesRes`, `colChequesRes`,
`colCajerosRes`.

## Árbol de controles

```
scrResumen
├─ cmpNavRes                     (instancia de cmpNav, 01 §7.5)
├─ cntBodyRes                    (Contenedor vertical, auto-layout)
│  ├─ cntTituloRes               (Contenedor vertical)
│  │  ├─ lblTituloRes
│  │  └─ lblSubtituloRes
│  ├─ cardFiltrosRes             (Contenedor manual, receta 01 §7.1)
│  │  ├─ lblFecFiltroRes
│  │  ├─ dtFechaRes
│  │  ├─ lblCajFiltroRes
│  │  ├─ drpCajeroRes
│  │  ├─ lblSyncRes
│  │  ├─ cirSyncRes
│  │  └─ btnActualizarRes
│  ├─ cntStaleRes                (Contenedor manual — aviso desactualizado)
│  │  ├─ lblStaleRes
│  │  └─ btnStaleRes
│  ├─ cntKpisRes                 (Contenedor horizontal, LayoutWrap)
│  │  ├─ cmpKpiTulasRes          (instancia de cmpKpi, 01 §7.5b)
│  │  ├─ cmpKpiDifRes            (instancia de cmpKpi — navegable)
│  │  ├─ cmpKpiNioRes            (instancia de cmpKpi)
│  │  ├─ cmpKpiUsdRes            (instancia de cmpKpi)
│  │  └─ cmpKpiEurRes            (instancia de cmpKpi)
│  └─ cardSesionesRes            (Contenedor manual, receta 01 §7.1)
│     ├─ lblSesTituloRes
│     ├─ pilSesCountRes
│     ├─ shpLineaPanelRes
│     ├─ shpBandHeadRes
│     ├─ lblColSesionRes · lblColCajeroRes · lblColMesaRes · lblColTulasRes
│     │  · lblColChequesRes · lblColNioRes · lblColUsdRes · lblColEurRes · lblColEstadoRes
│     ├─ shpLineaHeadRes
│     ├─ galSesionesRes          (Galería vertical en blanco)
│     │  ├─ btnFilaRes           (overlay de fila, receta 01 §7.3 — al FONDO)
│     │  ├─ lblCelSesionRes · lblCelCajeroRes · lblCelMesaRes · lblCelTulasRes
│     │  │  · lblCelChequesRes · lblCelNioRes · lblCelUsdRes · lblCelEurRes
│     │  ├─ pilCelEstadoRes
│     │  └─ shpSepSesRes
│     ├─ shpBandTotRes
│     ├─ lblTotEtiquetaRes · lblTotTulasRes · lblTotChequesRes
│     │  · lblTotNioRes · lblTotUsdRes · lblTotEurRes
│     ├─ shpLineaTotRes
│     ├─ icoVacioRes
│     ├─ lblVacioTituloRes
│     └─ lblVacioTextoRes
└─ cntCargandoRes                (Contenedor manual — overlay, receta 01 §7.6)
   └─ lblCargandoRes
```

> **Convención del árbol**: está en orden de construcción back→front (lo primero
> queda al FONDO). El panel de árbol de Studio lista el control frontal ARRIBA —
> no copies el orden visual de Studio, respetá las anotaciones "al FONDO/al frente".

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | ver abajo — COMPLETO, listo para pegar |

```powerfx
UpdateContext({ctxStaleRes: false});
If(
    IsEmpty(colSesionesRes),
    Select(btnActualizarRes)   // primera visita: carga sola (fix del "0 fantasma")
);
Reset(galSesionesRes)
```

> `Select()` desde OnVisible dispara el OnSelect del botón de la misma pantalla —
> toda la lógica de carga vive en UN solo lugar (el botón). Si ya hay datos (vuelta
> desde otra pantalla), no recarga; el usuario decide con Actualizar.

## 2. Layout responsive (01 §8)

| Control | Tipo | Propiedades |
|---|---|---|
| `cmpNavRes` | Componente de la app (`cmpNav`) | X `0` · Y `0` · Width `214` · Height `Parent.Height` |
| `cntBodyRes` | Contenedor vertical | X `cmpNavRes.Width` · Y `0` · Width `Parent.Width - cmpNavRes.Width` · Height `Parent.Height` · PaddingLeft/Right `26` · PaddingTop `22` · LayoutGap `14` · Fill `Transparent` · **SIN scroll** (LayoutOverflowY: Ocultar) — los hijos se reparten el alto con `FillPortions`; scroll + FillPortions se contradicen |

Hijos directos de `cntBodyRes` (auto-layout: NO llevan X/Y; el orden del árbol es el
orden vertical): `cntTituloRes`, `cardFiltrosRes`, `cntStaleRes`, `cntKpisRes`,
`cardSesionesRes`.

## 3. Título

| Control | Tipo | Propiedades |
|---|---|---|
| `cntTituloRes` | Contenedor **vertical** | Height `52` · `LayoutMinHeight 52` · LayoutGap `3` · Fill `Transparent` — título ARRIBA, subtítulo DEBAJO (como el mockup) |
| `lblTituloRes` | Etiqueta clásica | ESTILO-TITULO · Text `"Resumen de operación"` · Width `480` · Height `28` |
| `lblSubtituloRes` | Etiqueta clásica | ESTILO-SUBTITULO · Text `"Conciliación del recuento del día por sesión de cajero."` · Width `480` · Height `20` |

## 4. Barra de filtros

| Control | Tipo | Propiedades |
|---|---|---|
| `cardFiltrosRes` | Contenedor manual (receta 01 §7.1) | Height `74` · `LayoutMinHeight 74` — hijos con X/Y |
| `lblFecFiltroRes` | Etiqueta clásica | X `16` · Y `10` · ESTILO-KPI-LABEL · Text `"FECHA DE OPERACIÓN"` |
| `dtFechaRes` | Selector de fecha moderno (excepción 01 §0.3) | X `16` · Y `30` · Width `230` · Height `34` · **DefaultDate `Today()`** — en el moderno `DefaultDate` ES la entrada (`direction="in"`, verificado en `modernDatePicker_1.0.1.xml`) y **`SelectedDate` es SALIDA: jamás se le escribe** (escribirle da el error críptico "'Today' is an unknown function"). Las fórmulas mantienen `Coalesce(dtFechaRes.SelectedDate, Today())` como red · OnChange `UpdateContext({ctxStaleRes: true})` |
| `lblCajFiltroRes` | Etiqueta clásica | X `266` · Y `10` · ESTILO-KPI-LABEL · Text `"CAJERO"` |
| `drpCajeroRes` | Menú desplegable clásico | X `266` · Y `30` · Width `250` · Height `34` · Items `colCajerosRes` · Value `CajeroNombre` · Default `LookUp(colCajerosRes, CajeroGuid = ctxCajeroSelRes).CajeroNombre` (re-ancla la selección tras reconstruir Items) · OnChange `UpdateContext({ctxStaleRes: true})` |
| `cirSyncRes` | Círculo clásico | X `cardFiltrosRes.Width - 320` · Y `40` · Width `8` · Height `8` · Fill `ThemeAccent` · BorderThickness `0` · Visible `scrResumen.Width >= 1180` |
| `lblSyncRes` | Etiqueta clásica | X `cardFiltrosRes.Width - 304` · Y `32` · Width `160` · Height `20` · Size `8.5` · Color `ThemeInk3` · Visible `scrResumen.Width >= 1180` (bajo ese ancho chocaría con el combo) · Text `If(IsBlank(ctxUltimaActRes), "Sin actualizar", "Actualizado " & Text(ctxUltimaActRes, "HH:mm:ss"))` |
| `btnActualizarRes` | Botón clásico | X `cardFiltrosRes.Width - 140` · Y `28` · Width `124` · Height `36` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · Text `"↻  Actualizar"` · FontWeight `Semibold` · OnSelect: ver abajo |

**`btnActualizarRes.OnSelect` (COMPLETO — la única lógica de carga):**

```powerfx
UpdateContext({ctxCargandoRes: true, ctxStaleRes: false,
    ctxCajeroSelRes: drpCajeroRes.Selected.CajeroGuid});
IfError(
    // 1) Sesiones confirmadas del día — SIN filtro de cajero (1 consulta delegable).
    //    El día completo se guarda aparte para que el combo NUNCA se auto-colapse.
    ClearCollect(colSesDiaRes,
        Filter('CH-Tbl-RecountingSessions',
            'Created On' >= Coalesce(dtFechaRes.SelectedDate, Today())
            && 'Created On' < DateAdd(Coalesce(dtFechaRes.SelectedDate, Today()), 1, TimeUnit.Days)
            && Confirmed = true
        )
    );

    // 2) Combo de cajeros: población COMPLETA del día, dedup con GroupBy (fix OS-06).
    //    NUNCA ForAll leyendo la colección que él mismo escribe: Power Fx no
    //    garantiza el orden de evaluación de ForAll → duplicados intermitentes.
    //    La fila "Todos" siembra la columna GUID con tipo (If(false,...) da Blank tipado).
    ClearCollect(colCajerosRes,
        {CajeroNombre: "Todos los cajeros confirmados",
         CajeroGuid: If(false, GUID("00000000-0000-0000-0000-000000000000"))});
    ForAll(
        GroupBy(
            AddColumns(colSesDiaRes,
                CajGuid, Cashier.User,
                CajNom, Cashier.'Full Name' &
                    If(IsBlank(CountingDesk.Name), "", " — " & CountingDesk.Name)),
            CajGuid, CajNom, RestoSesiones),
        Collect(colCajerosRes, {CajeroNombre: CajNom, CajeroGuid: CajGuid})
    );

    // 3) Filtro de cajero LOCAL contra la selección CAPTURADA ANTES de reconstruir
    //    el combo (un dropdown resetea Selected cuando cambia su Items) + textos de
    //    cajero/mesa MATERIALIZADOS: el template lee columnas planas, cero dot-walk
    //    por fila (fix OS-05 de verdad)
    ClearCollect(colSesionesRes,
        AddColumns(
            Filter(colSesDiaRes,
                IsBlank(ctxCajeroSelRes) || Cashier.User = ctxCajeroSelRes),
            CajeroNombre, Cashier.'Full Name',
            MesaNombre, CountingDesk.Name
        )
    );

    // 4) Tulas recontadas por sesión — S consultas delegables (patrón Lookup.'PK' = PK)
    Clear(colBagsRes);
    ForAll(colSesionesRes As RS,
        Collect(colBagsRes,
            AddColumns(
                Filter('CH-Tbl-Bags',
                    RecountingSession.'CH-Tbl-RecountingSession' = RS.'CH-Tbl-RecountingSession'
                    && BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada),
                SesGUID, RS.'CH-Tbl-RecountingSession'
            )
        )
    );

    // 5) Detalles de sesión — S consultas (trae TODOS los estados; el KPI y el chip
    //    filtran 'Cerrado con diferencias' después, en local)
    Clear(colDetallesRes);
    ForAll(colSesionesRes As RS,
        Collect(colDetallesRes,
            AddColumns(
                Filter('CH-Tbl-RecountingSessionDetails',
                    RecountingSessionId.'CH-Tbl-RecountingSession' = RS.'CH-Tbl-RecountingSession'),
                SesDetGUID, RS.'CH-Tbl-RecountingSession'
            )
        )
    );

    // 6) Cheques del día — UNA consulta delegable por ventana de fecha + join
    //    LOCAL a la sesión (vía el espejo sanic_bag ya cargado en colBagsRes).
    //    NUNCA una consulta por tula: con miles de tulas (universo de estrés,
    //    2.149) el N+1 tardaba ~8 minutos; esto tarda segundos.
    ClearCollect(colChequesRes,
        AddColumns(
            Filter('CH-Tbl-Checks',
                'Created On' >= Coalesce(dtFechaRes.SelectedDate, Today())
                && 'Created On' < DateAdd(Coalesce(dtFechaRes.SelectedDate, Today()), 1, TimeUnit.Days)
            ),
            ChequeSesGUID, LookUp(colBagsRes, 'CH-Tbl-Bags' = Bag.'CH-Tbl-Bags').SesGUID
        )
    );

    UpdateContext({ctxUltimaActRes: Now()}),

    // rama de error del IfError
    Notify("No se pudo actualizar el resumen: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxCargandoRes: false});
Reset(galSesionesRes)
```

> `SesDetGUID` y `ChequeSesGUID` guardan el GUID de la SESIÓN (clave de join local),
> no del detalle/cheque — el nombre indica "a qué sesión pertenece".

**Clasificación de delegación (00 §5 la exige):**

| Fórmula | ¿Delegable? | Volumen esperado |
|---|---|---|
| Paso 1 — Filter sesiones (fechas + bit) | ✅ | < 20 filas/día |
| Paso 3 — Filter + AddColumns sobre `colSesDiaRes` | n/a (local) | ídem |
| Paso 4 — Filter Bags (`Lookup.'PK' = PK` + optionset) | ✅ ×S | < 30 tulas/sesión |
| Paso 5 — Filter Detalles (`Lookup.'PK' = PK`) | ✅ ×S | ídem |
| Paso 6 — Filter Checks (`Lookup.'PK' = PK`, espejo B-1) | ✅ ×B | < 50 cheques/tula |

> **Cuenta de requests honesta**: 1 consulta + ~2S expansiones de lookup (nombre y
> mesa del cajero en pasos 2-3) + S + S + B consultas. Día típico (3 sesiones, 18
> tulas) ≈ **31 round-trips**, contra ~973 de la pantalla vieja. Cada Filter delega;
> el resultado de cada uno queda igualmente sujeto al límite de filas del cliente
> (2000, fijado en 01 §1) — con los volúmenes por-sesión/por-tula de la tabla, no
> es alcanzable.
> El apagado del overlay va FUERA del `IfError` — un fallo nunca deja la pantalla
> bloqueada (fix OS-10).

## 5. Aviso "datos desactualizados"

| Control | Tipo | Propiedades |
|---|---|---|
| `cntStaleRes` | Contenedor manual | Height `40` · `LayoutMinHeight 40` · Fill `ThemeWarnSoft` · BorderColor `ThemeWarn` · BorderThickness `1` · Radius (las 4) `8` · **Visible `ctxStaleRes`** |
| `lblStaleRes` | Etiqueta clásica | X `14` · Y `10` · Width `560` · Size `9` · Color `ThemeWarn` · FontWeight `Semibold` · Text `"Cambiaste los filtros — los datos de abajo corresponden a la selección anterior."` |
| `btnStaleRes` | Botón clásico | X `cntStaleRes.Width - 150` · Y `7` · Width `136` · Height `26` · Radius `6` · Fill `Transparent` · Color `ThemeWarn` · BorderColor `ThemeWarn` · BorderThickness `1` · Text `"Actualizar ahora"` · Size `9` · OnSelect `Select(btnActualizarRes)` |

> En auto-layout un control con `Visible = false` libera su espacio — el aviso no
> deja hueco cuando está oculto.

## 6. Fila de KPIs (instancias de `cmpKpi`, 01 §7.5b)

| Control | Tipo | Propiedades |
|---|---|---|
| `cntKpisRes` | Contenedor horizontal | Height `If(scrResumen.Width < 1364, 204, 96)` · `LayoutMinHeight` misma fórmula (al envolver, la segunda fila necesita su alto — un contenedor auto-layout NO crece solo) · **LayoutWrap `true`** · LayoutGap `12` · Fill `Transparent` |

Las 5 instancias son **Componente de la app (`cmpKpi`)** y llevan `FillPortions 1`
+ `LayoutMinWidth 196` (5×196 + 4×12 = 1028 < 1100 px disponibles a 1366 — con 210
el margen era de 2 px y cualquier ventana menor envolvía). `cmpKpiEurRes` además
lleva `Visible = scrResumen.Width >= 1100` (coherente con la columna € oculta).

| Instancia | KpiLabel | KpiValor | KpiPie | KpiAccent | KpiPieColor | OnKpiSelect |
|---|---|---|---|---|---|---|
| `cmpKpiTulasRes` | `"◈ TULAS RECONTADAS"` | `Text(CountRows(colBagsRes))` | `"en " & CountRows(colSesionesRes) & " sesiones confirmadas"` | `ThemeAccent` | `ThemeInk3` | `false` |
| `cmpKpiDifRes` | `"⚖ TULAS CON DIFERENCIA"` | `Text(CountRows(Filter(colDetallesRes, RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias')))` | `"ver en Aprobaciones →"` | `ThemeNeg` | `ThemeAccent` | `Navigate(scrAprobaciones)` |
| `cmpKpiNioRes` | `"TOTAL EFECTIVO · C$"` | `Text(Coalesce(Sum(colBagsRes, TotalNIO), 0), FmtMoney)` + **KpiCur `SimboloNIO`** | `"córdoba nicaragüense"` | `ThemeAccentMoney` | `ThemeInk3` | `false` |
| `cmpKpiUsdRes` | `"TOTAL EFECTIVO · US$"` | `Text(Coalesce(Sum(colBagsRes, TotalUSD), 0), FmtMoney)` + **KpiCur `SimboloUSD`** | `"dólar estadounidense"` | `ThemeAccentMoney` | `ThemeInk3` | `false` |
| `cmpKpiEurRes` | `"TOTAL EFECTIVO · €"` | `Text(Coalesce(Sum(colBagsRes, TotalEUR), 0), FmtMoney)` + **KpiCur `SimboloEUR`** | `"euro"` | `ThemeAccentMoney` | `ThemeInk3` | `false` |

> Los tres KPI de dinero: `Sum` envuelto en `Coalesce(..., 0)` (con colección vacía
> muestran `0.00`, nunca vacío) y el símbolo va por la propiedad **`KpiCur`** del
> componente — dos tonos como el mockup (`.k-val .cur`), no concatenado al valor.

> El KPI "TULAS CON DIFERENCIA" cuenta detalles de sesión 'Cerrado con diferencias':
> el detalle tiene `sanic_bagid` → hay UN detalle POR TULA, así que la unidad ES la
> tula (misma semántica que el mockup).

> `TotalNIO/TotalUSD/TotalEUR` son los display names de `sanic_totalnio/usd/eur` en
> Bags (decimales simples, estampados — verificado). Si IntelliSense difiere, el
> suyo manda. `Sum` sobre colección local: los KPIs y la tabla salen de `colBagsRes`
> — **misma fuente** (fix OS-14). Con colección vacía `Sum` da Blank: `Text(Blank(),
> FmtMoney)` muestra vacío — aceptado porque el estado vacío (§8) tapa el caso.

## 7. Tabla de sesiones

| Control | Tipo | Propiedades |
|---|---|---|
| `cardSesionesRes` | Contenedor manual (receta 01 §7.1) | `FillPortions 1` (ocupa el alto restante) · `LayoutMinHeight 320` · hijos con X/Y |
| `lblSesTituloRes` | Etiqueta clásica | X `16` · Y `12` · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` · Text `"Sesiones de recuento"` (cabeza del panel — NO es ESTILO-SECCION, el mockup lo da en sentence case y tinta fuerte) |
| `pilSesCountRes` | Botón clásico (píldora, receta 01 §7.2) | X `190` · Y `10` · Width `12 + 7 * Len(Self.Text)` · Fill `ThemeSunk` · Color `ThemeInk3` · **BorderThickness `0`** (override de la receta — el mockup no lleva borde en el contador) · Text `CountRows(colSesionesRes) & " sesiones"` |
| `shpLineaPanelRes` | Rectángulo clásico | X `0` · Y `40` · Width `cardSesionesRes.Width` · Height `1` · Fill `ThemeLine` (línea bajo la cabeza del panel) |
| `shpBandHeadRes` | Rectángulo clásico | X `16` · Y `42` · Width `cardSesionesRes.Width - 32` · Height `22` · Fill `ThemeSurface2` · al FONDO del z-order de los encabezados (banda gris del thead) · Visible `CountRows(colSesionesRes) > 0` |

**Encabezados** (Etiquetas clásicas ESTILO-HEADER-COL, Y `44`, Height `18`; X/Width
como fracción del ancho de la galería — nombrando la galería, NUNCA `Self.Width`):

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblColSesionRes` | `"SESIÓN"` | `16` | `galSesionesRes.Width * 0.10` | Left |
| `lblColCajeroRes` | `"CAJERO"` | `16 + galSesionesRes.Width * 0.10` | `galSesionesRes.Width * 0.16` | Left |
| `lblColMesaRes` | `"MESA"` — Visible `CountRows(colSesionesRes) > 0 && scrResumen.Width >= 1100` | `16 + galSesionesRes.Width * 0.26` | `galSesionesRes.Width * 0.08` | Left |
| `lblColTulasRes` | `"TULAS"` | `16 + galSesionesRes.Width * 0.34` | `galSesionesRes.Width * 0.07` | Right |
| `lblColChequesRes` | `"CHEQUES"` | `16 + galSesionesRes.Width * 0.41` | `galSesionesRes.Width * 0.08` | Right |
| `lblColNioRes` | `"TOTAL C$"` | `16 + galSesionesRes.Width * 0.49` | `galSesionesRes.Width * 0.13` | Right |
| `lblColUsdRes` | `"TOTAL US$"` | `16 + galSesionesRes.Width * 0.62` | `galSesionesRes.Width * 0.11` | Right |
| `lblColEurRes` | `"TOTAL €"` — Visible `CountRows(colSesionesRes) > 0 && scrResumen.Width >= 1100` | `16 + galSesionesRes.Width * 0.73` | `galSesionesRes.Width * 0.10` | Right |
| `lblColEstadoRes` | `"ESTADO"` | `16 + galSesionesRes.Width * 0.85` | `galSesionesRes.Width * 0.13` | Left |

`shpLineaHeadRes` (Rectángulo clásico): X `16` · Y `64` · Width
`cardSesionesRes.Width - 32` · Height `1` · Fill `ThemeLine`.

**`galSesionesRes`** (Galería vertical en blanco): X `16` · Y `66` · Width
`cardSesionesRes.Width - 32` · Height `cardSesionesRes.Height - 130` ·
TemplateSize `44` · TemplatePadding `0` · ShowScrollbar `true` ·
Items `SortByColumns(colSesionesRes, "sanic_name", SortOrder.Ascending)` ·
**OnSelect** (receta 01 §7.3 — solo navegan las filas CON diferencia, como el mockup):

```powerfx
If( CountRows(Filter(colDetallesRes,
        SesDetGUID = ThisItem.'CH-Tbl-RecountingSession'
        && RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias')) > 0,
    Navigate(scrAprobaciones) )
```

**Template** (X del template = X del encabezado − 16; todos los hijos con
`OnSelect = Select(Parent)`):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaRes` | Botón clásico (overlay de fila, receta 01 §7.3) | X `0` · Y `0` · Width = Width de la galería · Height `44` (TemplateSize) · Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · al FONDO del z-order · OnSelect `Select(Parent)` |
| `lblCelSesionRes` | Etiqueta clásica | ESTILO-MONO · Text `ThisItem.Name` · X/Width = col SESIÓN · Y `12` · Height `20` |
| `lblCelCajeroRes` | Etiqueta clásica | ESTILO-CELDA · Text `ThisItem.CajeroNombre` (columna materializada en el paso 3 — cero dot-walk por fila) · Y `12` · Height `20` |
| `lblCelMesaRes` | Etiqueta clásica | ESTILO-CELDA · Color `ThemeInk2` · Text `ThisItem.MesaNombre` (materializada; el lookup real es `CountingDesk`, verificado en Entity.xml, `required`) · Y `12` · Height `20` · Visible `scrResumen.Width >= 1100` |
| `lblCelTulasRes` | Etiqueta clásica | ESTILO-CELDA-NUM · Text `Text(CountRows(Filter(colBagsRes, SesGUID = ThisItem.'CH-Tbl-RecountingSession')))` · Y `12` · Height `20` |
| `lblCelChequesRes` | Etiqueta clásica | ESTILO-CELDA-NUM · Text `Text(CountRows(Filter(colChequesRes, ChequeSesGUID = ThisItem.'CH-Tbl-RecountingSession')))` · Y `12` · Height `20` |
| `lblCelNioRes` | Etiqueta clásica | ESTILO-CELDA-NUM (Semibold) · Text `Text(Coalesce(Sum(Filter(colBagsRes, SesGUID = ThisItem.'CH-Tbl-RecountingSession'), TotalNIO), 0), FmtMoney)` · Y `12` · Height `20` |
| `lblCelUsdRes` | Etiqueta clásica | ídem con `TotalUSD` (mismos X/Width que su encabezado, regla 01 §7.3) |
| `lblCelEurRes` | Etiqueta clásica | ídem con `TotalEUR` · Visible `scrResumen.Width >= 1100` |
| `pilCelEstadoRes` | Botón clásico (píldora, receta 01 §7.2) | X = col ESTADO · Y `10` · Width `12 + 7 * Len(Self.Text)` · **OnSelect `Select(Parent)`** · las TRES fórmulas completas abajo |
| `shpSepSesRes` | Rectángulo clásico | X `0` · Y `43` (TemplateSize − 1) · Width = Width de la galería · Height `1` · Fill `ThemeLine` |

**Fórmulas de `pilCelEstadoRes`** (cada propiedad con la suya — un `With` no setea
tres propiedades; la subconsulta se repite):

```powerfx
// Text
With({d: CountRows(Filter(colDetallesRes,
        SesDetGUID = ThisItem.'CH-Tbl-RecountingSession'
        && RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias'))},
    If(d > 0, "Diferencias ×" & d, "Cuadrado"))
// Fill
With({d: CountRows(Filter(colDetallesRes,
        SesDetGUID = ThisItem.'CH-Tbl-RecountingSession'
        && RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias'))},
    If(d > 0, ThemeNegSoft, ThemeSunk))
// Color
With({d: CountRows(Filter(colDetallesRes,
        SesDetGUID = ThisItem.'CH-Tbl-RecountingSession'
        && RecountingSessionDetailStatus = 'ch-recountingDetailsStatus'.'Cerrado con diferencias'))},
    If(d > 0, ThemeNeg, ThemeInk3))
```

> Todas las operaciones del template son sobre COLECCIONES locales con la columna
> `SesGUID`/`SesDetGUID`/`ChequeSesGUID` pre-materializada — cero fetch remoto por
> fila (fix OS-05). **Divergencia L-9 del ledger (00 §4)**: el mockup mostraba chips
> tri-estado "Faltante ×N / Sobrante ×N / Cuadrado"; el estado del detalle de sesión
> no lleva dirección (el signo vive en Transactions, cadena que esta pantalla no
> carga), así que el chip es bi-estado "Diferencias ×N" / "Cuadrado".

**Fila de totales** (fix OS-14: suma de las MISMAS colecciones que los KPIs —
Etiquetas clásicas, Y `= galSesionesRes.Y + galSesionesRes.Height + 8`, FontWeight
`Bold`, Size `9.5`):

| Control | Text | X/Width/Align |
|---|---|---|
| `lblTotEtiquetaRes` | `"TOTAL OPERACIÓN"` | X `16` · ESTILO-SECCION (manda el alias; sin Bold 9.5 aparte) |
| `lblTotTulasRes` | `Text(CountRows(colBagsRes))` | = col TULAS · Right · Bold `9.5` |
| `lblTotChequesRes` | `If(CountRows(colChequesRes) >= 2000, "2,000+", Text(CountRows(colChequesRes)))` — la consulta del paso 6 topa en el límite de filas (2000); el "+" avisa en vez de mentir | = col CHEQUES · Right · Bold `9.5` |
| `lblTotNioRes` | `Text(Coalesce(Sum(colBagsRes, TotalNIO), 0), FmtMoney)` | = col TOTAL C$ · Right · Bold `9.5` |
| `lblTotUsdRes` | `Text(Coalesce(Sum(colBagsRes, TotalUSD), 0), FmtMoney)` | = col TOTAL US$ · Right · Bold `9.5` |
| `lblTotEurRes` | `Text(Coalesce(Sum(colBagsRes, TotalEUR), 0), FmtMoney)` | = col TOTAL € · Right · Bold `9.5` · Visible `CountRows(colSesionesRes) > 0 && scrResumen.Width >= 1100` |

`shpLineaTotRes` (Rectángulo clásico): X `16` · Y `= galSesionesRes.Y +
galSesionesRes.Height + 4` · Width `cardSesionesRes.Width - 32` · Height `2` ·
Fill `ThemeLineStrong`.

`shpBandTotRes` (Rectángulo clásico): X `16` · Y `= shpLineaTotRes.Y + 2` · Width
`cardSesionesRes.Width - 32` · Height `28` · Fill `ThemeSurface2` · al FONDO del
z-order de la fila de totales (banda gris del tfoot del mockup).

**Visibilidad en vacío**: TODOS los controles de encabezados (los 9 `lblCol…`,
`shpLineaHeadRes`, `shpBandHeadRes`) y de totales (`lblTot…`, `shpLineaTotRes`,
`shpBandTotRes`) llevan `Visible = CountRows(colSesionesRes) > 0` — con la tabla
vacía solo se ve el estado vacío (§8), sin encabezados huérfanos ni totales en cero.

## 8. Estado vacío

Los tres controles con **Visible `CountRows(colSesionesRes) = 0 && !ctxCargandoRes`**,
centrados en `cardSesionesRes`:

| Control | Tipo | Propiedades |
|---|---|---|
| `icoVacioRes` | Icono | Icon `Icon.People` · X `cardSesionesRes.Width/2 - 16` · Y `cardSesionesRes.Height/2 - 60` · Width `32` · Height `32` · Color `ThemeInk3` |
| `lblVacioTituloRes` | Etiqueta clásica | X `0` · Y `cardSesionesRes.Height/2 - 18` · Width `cardSesionesRes.Width` · Height `24` · Size `12` · FontWeight `Semibold` · Color `ThemeInk2` · Align Center · Text `"No hay sesiones confirmadas en esta fecha"` |
| `lblVacioTextoRes` | Etiqueta clásica | X `0` · Y `cardSesionesRes.Height/2 + 6` · Width `cardSesionesRes.Width` · Height `20` · Size `9.5` · Color `ThemeInk3` · Align Center · Text `"Elegí otra fecha o esperá a que los cajeros confirmen su recuento."` |

## 9. Overlay de carga

| Control | Tipo | Propiedades |
|---|---|---|
| `cntCargandoRes` | Contenedor manual (receta 01 §7.6) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxCargandoRes`** · último en el z-order de la pantalla |
| `lblCargandoRes` | Etiqueta clásica | X `0` · Y `cntCargandoRes.Height/2 - 20` · Width `cntCargandoRes.Width` · Height `40` · Align Center · Size `12` · Color `ColorValue("#FFFFFF")` · Text `"Actualizando..."` |

> **Notas de la guía**
> - **Backend**: el paso 6 del botón depende del espejo `sanic_bag` en Checks
>   (B-1 del 00 — columna VIVA en el entorno, falta en el repo; refrescar el
>   datasource si IntelliSense no la muestra). **Fallback si no estuviera
>   disponible**: omitir el paso 6 y poner `Text = "—"` en `lblCelChequesRes` y
>   `lblTotChequesRes` (la alternativa vieja — cadena Bags→DepositsRecountings→
>   Checks — reintroduce +B consultas y el dot-walk; no la reproduzcas).
> - **Navegación (contrato con 03)**: `Navigate(scrAprobaciones)` no setea ningún
>   `gvar` — la guía 03 define su bandeja como autosuficiente (carga sus propias
>   colecciones); si 03 declarara un contrato de entrada, esta pantalla debe
>   cumplirlo (verificar al cerrar 03).
> - **Divergencias vs mockup** (además de las del ledger 00 §4, que ya incluye L-9
>   chip bi-estado y L-10 sin toggle de tema): el punto de sync no lleva halo; el
>   texto "Sin actualizar" antes de la primera carga es agregado de la guía (el
>   mockup siempre muestra hora). (El símbolo de moneda a dos tonos SÍ se logra —
>   propiedad `KpiCur` de cmpKpi, 01 §7.5b.)
> - **Responsive**: en `<scr>.Width < 1100`, `lblColMesaRes`/`lblCelMesaRes` y
>   `lblColEurRes`/`lblCelEurRes`/`lblTotEurRes` llevan
>   `Visible = scrResumen.Width >= 1100` (columnas de baja prioridad).

## 10. Checklist de salida

- [ ] Al entrar por primera vez la pantalla CARGA SOLA (sin apretar Actualizar) y
      muestra "Actualizado HH:mm:ss".
- [ ] Cambiar fecha o cajero muestra el aviso ámbar; "Actualizar ahora" lo apaga.
- [ ] Los 5 KPIs y la fila de totales muestran EXACTAMENTE los mismos números
      (misma fuente): tulas, cheques y 3 monedas.
- [ ] Montos en cero se ven `C$ 0.00` (nunca `C$ .00` ni el símbolo pelado —
      los Sum van con Coalesce).
- [ ] Con una fecha sin sesiones: estado vacío con mensaje, KPIs en 0, sin
      encabezados ni totales huérfanos, sin errores.
- [ ] Clic en el KPI "TULAS CON DIFERENCIA" navega a scrAprobaciones; una fila
      CON diferencias navega; una fila "Cuadrado" no hace nada.
- [ ] Elegir un cajero en el combo filtra la tabla Y el combo conserva a TODOS
      los cajeros del día (no se auto-colapsa).
- [ ] Desconectar red y Actualizar: Notify de error y el overlay se APAGA (no queda
      pegado).
- [ ] Achicar la ventana: los KPIs envuelven a 2 filas; columnas Mesa y € se ocultan
      bajo 1100 px; nada se superpone.
- [ ] App Checker: cero errores; galería sin warnings de delegación.
