# Guía 03 — scrLotes (Administración de lotes)

Pantalla de **administración** master-detail de CH-App-Reception v2. Reemplaza a
`AdministracionDeLotes` de la v1 (la de los 4 `FluentDetailsList`, los 2
`CommandBar`, el `SearchBox`, el `ExpandMenu` con sus 11.280 líneas de SVG, los
dup-checks truncados a 500 filas y el `RemoveIf` que dejaba tulas huérfanas).

Acá: **dos vistas** (Lotes / Planillas) sobre un mismo esqueleto master-detail,
**una consulta delegable por vista**, **Mover a bóveda** en dos fases con
`IfError`, y las **5 operaciones** de tula/planilla con su **doble camino**
directo/aprobación resuelto por el estado del lote.

**CONTRATO DE ENTRADA**: **ninguno**. La pantalla es autosuficiente: no lee
`gvarLoteActivo` ni ninguna colección de `scrCaptura`. Se entra por el segmentado
del header (`segLotesCap`, receta 01 §7.8) con `Navigate(scrLotes)` pelado, o por
`App.StartScreen` si el usuario navega directo. Lo único global que consume son
la paleta, los formatos y `UsuarioNombre`/`UsuarioIniciales` de `App.Formulas`
(01 §3.1) y `HtmlApEstilo` / `HtmlApEncabezadoTabla` /
`HtmlApEncabezadoTablaSimple` (01 §3.1, receta 7.7).

**CONTRATO DE SALIDA hacia `scrCaptura` (02)** — lo setea "Editar lote" (§9), y es
**copia literal del contrato canónico de `01` §4.3**: `gvarLoteActivo` (registro de
`CH-Tbl-ReceptionBatches`), los dos snapshots crudos `colPlanillasDV` / `colTulasDV`
(una consulta delegable cada uno), `colPlanillas` / `colTulas` / `colProblemas` /
`colFallidas` **VACIADAS** y `gvarCambiosSinGuardar = false`.

⚠️ **Esta pantalla NO proyecta `colPlanillas`/`colTulas`** — las **vacía**. La
proyección `…DV → colecciones de edición` tiene **una sola definición en toda la
app**: la **receta `01` §7.10**, que corre en el `OnVisible` de `scrCaptura` y ahí
rehidrata `Empresa` como **registro fresco de sucursal** con un `LookUp` contra
`CH-Tbl-ClientBranches` (el catálogo local murió — **L-23**, regla **D-10**).
Vaciar las colecciones es lo que habilita esa proyección (su guarda es
`IsEmpty(colPlanillas)`). Ver §9 y `01` §4.3.

**Variables y colecciones propias** (ninguna global; todo `ctx`/`col` local):
`ctxTulaSelLot`, `ctxDesdeLot`, `ctxHastaLot`, `ctxDlgBovedaLot`,
`ctxDlgAgregarLot`, `ctxDlgEliminarLot`, `ctxDlgRemapearLot`,
`ctxDlgEditarTulaLot`, `ctxDlgEditarPlanillaLot`, `ctxTulaNuevaLot`,
`ctxBagPrevioLot`, `ctxHtmlLot`, `ctxFilasLot`, `ctxETipoLot`, `ctxELoteLot`,
`ctxETransportadoraLot`, `ctxEEstadoLoteLot`, `ctxEPlanillaLot`,
`ctxEEmpresaLot`, `ctxEUsuarioLot`, `ctxETulaLot`, `ctxETulaNuevaLot`,
`ctxEPlanillaDestinoLot`, `ctxEEmpresaDestinoLot`, `ctxEPlanillaNuevaLot`,
`ctxEBagPrevioLot`, `ctxEncabezadoLot`, `ctxResetFechasLot`, `colTulasBovedaLot`.
Están todas inventariadas en `01` §4. Las dos últimas suelen pasarse por alto:
`ctxEncabezadoLot` elige cuál de los **dos** encabezados de tabla lleva el HTML de
la solicitud (`"simple"` / `"comparativo"`, §13.3) y `ctxResetFechasLot` es el
**fallback declarado** del `Reset()` sobre el Selector de fecha moderno (§3).
**No existe `ctxSegLot`**: la conjunción de vista se lee de
`tabVistaLot.Selected.Value` (§0.1). **No existen** `colLotes` / `colPlanillasLot` /
`colTulasLot`: las cuatro galerías van delegables directo al datasource (§4.2).

> **Regla de coordenadas de esta guía** (01 §0.7): el `X`/`Y` de un control que
> vive dentro del template de una galería es **relativo al template**, que
> arranca en `0`. Se enuncia una sola vez: **X del control del template = X del
> encabezado − X de la galería**. Las galerías de esta pantalla arrancan en
> `X = 18` (maestras) y `X = 706` (detalle).

> **Lienzo**: 1366 × 768 con *Escalar para ajustar* activado (01 §1.3) ⇒ 1 unidad
> = 1 px. Todas las coordenadas de abajo son absolutas salvo las de templates.

---

## Árbol de controles

Orden **back → front**: el primero de cada nivel queda al FONDO del z-order y el
último arriba. Ese orden ES el orden de inserción (01 §0.6 y §0.11: construí en
DOS pases — primero insertar y renombrar TODO, después llenar propiedades).

```
scrLotes
├─ shpHeaderLot                      (Rectángulo — banda blanca 56 px)
├─ shpHeaderLineaLot                 (Rectángulo — 1 px inferior)
├─ imgTileLot                        (Imagen clásica — tile de marca)
├─ lblTituloLot
├─ lblSubtituloLot
├─ shpSegNavLot                      (receta 01 §7.8)
├─ segCapturaLot                     (receta 01 §7.8 — inactivo, navega)
├─ segLotesLot                       (receta 01 §7.8 — activo)
├─ btnChipUsuarioLot                 (superficie del chip, receta 01 §7.1)
├─ btnAvatarUsuarioLot               (iniciales, receta 01 §7.1)
├─ lblUsuarioLot
│   ── barra de herramientas ──
├─ tabVistaLot                       (Lista de pestañas MODERNA)
├─ inpBuscarLot
├─ icoBuscarLot
├─ drpEstadoLot                      (Menú desplegable clásico)
├─ dtpDesdeLot                       (Selector de fecha MODERNO)
├─ lblRangoLot
├─ dtpHastaLot                       (Selector de fecha MODERNO)
├─ icoLimpiarFechasLot
├─ icoRefrescarLot                   (MOTOR de recarga — §4)
│   ── card maestra (compartida por las dos vistas) ──
├─ btnCardListaLot                   (receta 01 §7.1)
├─ btnHeadListaLot                   (banda de encabezado, radio superior)
├─ lblHeadListaLotesLot
├─ lblHeadListaPlanLot
├─ shpLineaHeadListaLot
├─ galLotesLot                       (vista LOTES)
│  ├─ btnFilaLotesLot                (overlay de clic — al FONDO)
│  ├─ shpSelLotesLot
│  ├─ shpSelBarraLotesLot
│  ├─ lblFilaLoteNumLot
│  ├─ lblFilaLoteMetaLot
│  ├─ pilFilaLoteLot                 (píldora, receta 01 §7.4)
│  └─ shpSepLotesLot
├─ lblVacioLotesLot
├─ galPlanillasLot                   (vista PLANILLAS)
│  ├─ btnFilaPlanLot                 (overlay de clic — al FONDO)
│  ├─ shpSelPlanLot
│  ├─ shpSelBarraPlanLot
│  ├─ lblFilaPlanTitLot
│  ├─ lblFilaPlanMetaLot
│  ├─ lblFilaPlanLoteLot
│  ├─ pilFilaPlanLot                 (píldora por BatchStatus, §11.4)
│  └─ shpSepPlanLot
├─ lblVacioPlanillasLot
│   ── card de detalle (compartida por las dos vistas) ──
├─ btnCardDetLot                     (receta 01 §7.1)
│   ── detalle de la vista LOTES ──
├─ lblDetLoteTitLot
├─ lblDetLoteSubLot
├─ pilDetLoteLot                     (píldora, receta 01 §7.4)
├─ shpLineaDetLoteLot
├─ btnMoverBovedaLot
├─ btnEditarLoteLot
├─ shpLineaOpsLoteLot
├─ btnHeadPlanLoteLot
├─ lblHeadPlanLoteNumLot
├─ lblHeadPlanLoteRemLot
├─ lblHeadPlanLoteNioLot
├─ lblHeadPlanLoteUsdLot
├─ shpLineaHeadPlanLoteLot
├─ galPlanillasDelLoteLot            (mini-tabla, filas NO clicables)
│  ├─ lblCelPlanLoteNumLot
│  ├─ lblCelPlanLoteRemLot
│  ├─ lblCelPlanLoteNioLot
│  ├─ lblCelPlanLoteUsdLot
│  └─ shpSepPlanLoteLot
├─ lblVacioPlanLoteLot
├─ lblVacioDetLoteLot
│   ── detalle de la vista PLANILLAS ──
├─ lblDetPlanTitLot
├─ lblDetPlanSubLot
├─ pilDetPlanLot                     (píldora por BatchStatus, §11.4)
├─ shpLineaDetPlanLot
├─ btnBannerBovedaLot                (banner ámbar — superficie)
├─ icoBannerBovedaLot
├─ lblBannerBovedaLot
├─ btnAgregarTulaLot
├─ btnEditarPlanillaLot
├─ btnEditarTulaLot
├─ btnRemapearTulaLot
├─ btnEliminarTulaLot
├─ shpLineaOpsPlanLot
├─ btnHeadTulasLot
├─ lblHeadTulaNumLot
├─ lblHeadTulaEstadoLot
├─ lblHeadTulaFechaLot
├─ lblHeadTulaAutorLot
├─ shpLineaHeadTulasLot
├─ galTulasLot
│  ├─ btnFilaTulaLot                 (overlay de clic — al FONDO)
│  ├─ shpSelTulaLot
│  ├─ shpSelBarraTulaLot
│  ├─ lblCelTulaNumLot
│  ├─ pilCelTulaLot                  (píldora, receta 01 §7.4)
│  ├─ lblCelTulaFechaLot
│  ├─ lblCelTulaAutorLot
│  └─ shpSepTulaLot
├─ lblVacioTulasLot
├─ lblVacioDetPlanLot
│   ── DIÁLOGOS (tope del z-order, en este orden) ──
├─ cntDlgBovedaLot                   (receta 01 §7.2)
│  ├─ btnDlgBloqueoBovedaLot
│  └─ cntDlgCardBovedaLot
│     ├─ lblDlgTitBovedaLot
│     ├─ lblDlgTxtBovedaLot
│     ├─ icoDlgXBovedaLot
│     ├─ btnDlgCancelBovedaLot
│     └─ btnDlgOkBovedaLot
├─ cntDlgAgregarLot                  (receta 01 §7.2)
│  ├─ btnDlgBloqueoAgregarLot
│  └─ cntDlgCardAgregarLot
│     ├─ lblDlgTitAgregarLot
│     ├─ lblDlgTxtAgregarLot
│     ├─ icoDlgXAgregarLot
│     ├─ lblCampoTulaAgregarLot
│     ├─ inpTulaAgregarLot
│     ├─ lblErrTulaAgregarLot
│     ├─ btnAvisoAgregarLot
│     ├─ icoAvisoAgregarLot
│     ├─ lblAvisoAgregarLot
│     ├─ btnDlgCancelAgregarLot
│     └─ btnDlgOkAgregarLot
├─ cntDlgEliminarLot                 (receta 01 §7.2)
│  ├─ btnDlgBloqueoEliminarLot
│  └─ cntDlgCardEliminarLot
│     ├─ lblDlgTitEliminarLot
│     ├─ lblDlgTxtEliminarLot
│     ├─ icoDlgXEliminarLot
│     ├─ lblKvTulaEtqEliminarLot
│     ├─ lblKvTulaEliminarLot
│     ├─ lblKvPlanEtqEliminarLot
│     ├─ lblKvPlanEliminarLot
│     ├─ lblKvLoteEtqEliminarLot
│     ├─ lblKvLoteEliminarLot
│     ├─ btnAvisoEliminarLot
│     ├─ icoAvisoEliminarLot
│     ├─ lblAvisoEliminarLot
│     ├─ btnDlgCancelEliminarLot
│     └─ btnDlgOkEliminarLot
├─ cntDlgRemapearLot                 (receta 01 §7.2)
│  ├─ btnDlgBloqueoRemapearLot
│  └─ cntDlgCardRemapearLot
│     ├─ lblDlgTitRemapearLot
│     ├─ lblDlgTxtRemapearLot
│     ├─ icoDlgXRemapearLot
│     ├─ lblCampoDestinoRemapearLot
│     ├─ cmbDestinoRemapearLot
│     ├─ lblErrRemapearLot
│     ├─ btnAvisoRemapearLot
│     ├─ icoAvisoRemapearLot
│     ├─ lblAvisoRemapearLot
│     ├─ btnDlgCancelRemapearLot
│     └─ btnDlgOkRemapearLot
├─ cntDlgEditarTulaLot               (receta 01 §7.2)
│  ├─ btnDlgBloqueoEditarTulaLot
│  └─ cntDlgCardEditarTulaLot
│     ├─ lblDlgTitEditarTulaLot
│     ├─ lblDlgTxtEditarTulaLot
│     ├─ icoDlgXEditarTulaLot
│     ├─ lblCampoTulaEditarLot
│     ├─ inpTulaEditarLot
│     ├─ lblErrTulaEditarLot
│     ├─ btnAvisoEditarTulaLot
│     ├─ icoAvisoEditarTulaLot
│     ├─ lblAvisoEditarTulaLot
│     ├─ btnDlgCancelEditarTulaLot
│     └─ btnDlgOkEditarTulaLot
└─ cntDlgEditarPlanillaLot           (receta 01 §7.2 — TOPE absoluto)
   ├─ btnDlgBloqueoEditarPlanillaLot
   └─ cntDlgCardEditarPlanillaLot
      ├─ lblDlgTitEditarPlanillaLot
      ├─ lblDlgTxtEditarPlanillaLot
      ├─ icoDlgXEditarPlanillaLot
      ├─ lblCampoPlanillaEditarLot
      ├─ inpPlanillaEditarLot
      ├─ lblErrPlanillaEditarLot
      ├─ btnAvisoEditarPlanillaLot
      ├─ icoAvisoEditarPlanillaLot
      ├─ lblAvisoEditarPlanillaLot
      ├─ btnDlgCancelEditarPlanillaLot
      └─ btnDlgOkEditarPlanillaLot
```

**177 controles**: 96 de pantalla + 81 repartidos en los 6 diálogos (8 + 14 + 17
+ 14 + 14 + 14). Todos con sufijo **`Lot`** (regla 01 §0.5).

---

## 0. Las dos reglas transversales de esta pantalla

### 0.1 La conjunción del tab — sin excepción

`tabVistaLot` es una **Lista de pestañas moderna**: no crea pantallas, solo
cambia un valor. Por eso **todo control de CONTENIDO de una vista lleva la
conjunción del tab en su `Visible`**, aunque parezca obvio:

```powerfx
tabVistaLot.Selected.Value = "Lotes" && <el resto de la condición>
```

**Las CUATRO únicas excepciones**, declaradas acá y en ningún otro lado, todas
por el mismo motivo — son **chrome compartido**: existen idénticas en las dos
vistas, se dibujan igual en las dos y por eso no llevan condición.

| Excepción | Qué es |
|---|---|
| `btnCardListaLot` | superficie de la card maestra (§5) |
| `btnCardDetLot` | superficie de la card de detalle (§5) |
| `btnHeadListaLot` | banda del encabezado de la lista maestra (§5) — lo que **cambia** con el tab son sus dos etiquetas (`lblHeadListaLotesLot` / `lblHeadListaPlanLot`), que **sí** llevan conjunción |
| `shpLineaHeadListaLot` | línea de 1 px bajo esa banda (§5) |

**El header (§2), la barra de herramientas (§3) y los seis diálogos (§12) no son
contenido de vista y quedan fuera de la regla**: el header y la barra están vivos
en las dos vistas por definición, y los diálogos se gobiernan por su propio
`ctxDlg…Lot` (receta `01` §7.2). La regla aplica a **§5–§11**.

Cualquier otro control de §5–§11 sin conjunción es un **bug de construcción**.

### 0.2 Estados vacíos = colección vacía, nunca "sin selección"

Las galerías clásicas **auto-seleccionan la primera fila** (01 §4). El panel de
detalle se diseña asumiéndolo: los estados vacíos del detalle se disparan con
"no hay selección", que **solo** ocurre cuando la lista maestra no devolvió
ninguna fila. No existe un estado "seleccioná un lote": si hay lotes, hay uno
seleccionado.

🧨 **"No hay selección" se escribe SIEMPRE contra la PK, nunca contra el registro
pelado**: `IsBlank(galLotesLot.Selected.'CH-Tbl-ReceptionBatch')` y
`IsBlank(galPlanillasLot.Selected.'CH-Tbl-Receptions')`. Motivo: con `Items`
vacío, `gal….Selected` **no es blank** — es un **registro tipado vacío**, y
`IsBlank(<registro>)` sobre eso puede dar `false`. La PK en blanco sí es blank.
Es el mismo criterio que `ctxTulaSelLot` ya aplica en §11.6
(`IsBlank(ctxTulaSelLot.'CH-Tbl-Bags')`): **una sola forma en toda la pantalla**,
en las **6** apariciones (VIS-LOTE, VIS-PLAN y los cuatro `lblVacio…`).

⚠️ **Contraejemplo deliberado**: la selección de **tula** NO usa
`galTulasLot.Selected` justamente por eso — ver §11.6.

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `LoadingSpinnerColor` | `ThemeAccent` |
| `OnVisible` | ver abajo — COMPLETO, listo para pegar |

```powerfx
UpdateContext(
    {
        ctxDlgBovedaLot:         false,
        ctxDlgAgregarLot:        false,
        ctxDlgEliminarLot:       false,
        ctxDlgRemapearLot:       false,
        ctxDlgEditarTulaLot:     false,
        ctxDlgEditarPlanillaLot: false,
        ctxTulaSelLot:           Defaults('CH-Tbl-Bags'),
        ctxDesdeLot:             Date(1900, 1, 1),
        ctxHastaLot:             Date(2100, 1, 1)
    }
);
Reset(tabVistaLot);
Reset(inpBuscarLot);
Reset(drpEstadoLot);
Reset(dtpDesdeLot);
Reset(dtpHastaLot);
Reset(galLotesLot);
Reset(galPlanillasLot);
Clear(colTulasBovedaLot)
```

> **Por qué así**:
> - El estado de una **Lista de pestañas** y de una **galería** PERSISTE entre
>   visitas (gotcha de canvas). Sin los `Reset`, volvés de `scrCaptura` y te
>   encontrás en el tab "Planillas" con una planilla de otro lote seleccionada.
> - **`ctxTulaSelLot` se siembra con `Defaults('CH-Tbl-Bags')`, nunca con
>   `Blank()`**: `Defaults(ds)` da un registro **TIPADO** sin consultar nada
>   (mismo truco que 01 §3.2). Un `Blank()` pelado deja el tipo sin inferir y
>   toda referencia a `ctxTulaSelLot.BagID` queda en rojo en tiempo de diseño.
>   "Sin tula seleccionada" se lee como `IsBlank(ctxTulaSelLot.'CH-Tbl-Bags')`.
> - **`ctxDesdeLot`/`ctxHastaLot` arrancan como centinelas abiertos**
>   (1900-01-01 / 2100-01-01) y **nunca** quedan en blanco: si el predicado del
>   `Filter` comparara contra un `Blank()`, la fecha se volvería una condición
>   client-side y **rompería la delegación** de la consulta principal (§4.2).
> - 🧨 **Sin `Select(icoRefrescarLot)` acá, a propósito.** Las cuatro galerías
>   tienen su `Items` **atado al datasource** (§4.2): consultan solas al
>   renderizar. Entrar a la pantalla YA es una consulta fresca — un `Refresh()`
>   de las tres tablas en el `OnVisible` sería un round-trip extra por cada ida y
>   vuelta desde Captura, sin traer un solo dato que la galería no fuera a pedir
>   igual. El ⟳ existe para el caso que el render **no** cubre: datos que cambió
>   **otro usuario** mientras vos estabas parado en la pantalla.
> - El reset de `ctxTulaSelLot` **no dependía** de ese `Select`: ya está arriba, en
>   el `UpdateContext` de este mismo `OnVisible` (R-1 de §11.6).

---

## 2. Header (56 px)

Idéntico al de `scrCaptura` salvo el subtítulo, el segmento activo y la ausencia
del chip de lote (que solo existe en captura). Sufijo `Lot` en todo.

| Control | Tipo | Propiedades |
|---|---|---|
| `shpHeaderLot` | Rectángulo clásico | X `0` · Y `0` · Width `1366` · Height `56` · `Fill` `ThemeSurface` |
| `shpHeaderLineaLot` | Rectángulo clásico | X `0` · Y `55` · Width `1366` · Height `1` · `Fill` `ThemeLine` |
| `imgTileLot` | Imagen clásica | X `18` · Y `11` · Width `34` · Height `34` · `Radius` (las 4) `9` · `Image` `IconoReception192` · `ImagePosition` `ImagePosition.Fill` — el medio lo carga `01` §1.2 (`docs/marca/icono-reception-192.png` → **`IconoReception192`**); es el MISMO que usa `imgMarcaCap` en `02` |
| `lblTituloLot` | Etiqueta clásica | X `66` · Y `11` · Width `140` · Height `18` · `ESTILO-TITULO-APP` · `Text` `"Recepción"` |
| `lblSubtituloLot` | Etiqueta clásica | X `66` · Y `29` · Width `160` · Height `15` · `ESTILO-SUBTITULO-APP` · `Text` `"Administración de lotes"` |
| `shpSegNavLot` | Botón clásico (receta 01 §7.8) | X `230` · Y `12` · Width **`179`** (receta 01 §7.8: `3 + 85 + 3 + 85 + 3`, padding simétrico) · Height `32` |
| `segCapturaLot` | Botón clásico (receta 01 §7.8, **inactivo**) | X `shpSegNavLot.X + 3` · Y `shpSegNavLot.Y + 3` · Width `85` · Height `26` · `Text` `"Captura"` · `OnSelect` `Navigate(scrCaptura)` |
| `segLotesLot` | Botón clásico (receta 01 §7.8, **activo**) | X `segCapturaLot.X + segCapturaLot.Width + 3` · Y `segCapturaLot.Y` · Width `85` · Height `26` · `Text` `"Lotes"` · `OnSelect` `false` |
| `btnChipUsuarioLot` | Botón clásico (receta 01 §7.1) | X `1348 - Self.Width` · Y `11` · **Width `46 + 6.2 * Len(UsuarioNombre)`** · Height `34` · `Radius` (las 4) `17` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` |
| `btnAvatarUsuarioLot` | Botón clásico (receta 01 §7.1, con texto) | X `btnChipUsuarioLot.X + 5` · Y `btnChipUsuarioLot.Y + 4` · Width `26` · Height `26` · `Radius` (las 4) `13` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccentSoft` · `Color` `ThemeAccent` · `BorderThickness` `0` · Size `7.88` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `Text` `UsuarioIniciales` · `DisplayMode` `DisplayMode.View` |
| `lblUsuarioLot` | Etiqueta clásica | X `btnAvatarUsuarioLot.X + btnAvatarUsuarioLot.Width + 8` · Y `11` · Width `btnChipUsuarioLot.Width - 47` · Height `34` · Size `8.63` · `Color` `ThemeInk2` · `Text` `UsuarioNombre` |

> **`OnSelect` de `segLotesLot` es `false`** (ya estás en esta pantalla — el
> mockup también ignora el clic). **`segCapturaLot` navega SIN guarda**:
> `scrLotes` no acumula ediciones locales, toda su escritura es un `Patch`
> inmediato (receta 01 §7.8, segunda variante).
>
> **El chip de lote del mockup no existe acá**: `S.screen==='captura' && S.lote`
> es su condición en el prototipo (`header()`), y en `scrLotes` da falso siempre.

---

## 3. Barra de herramientas

Todos los controles centran su eje vertical en **Y = 85** (`.toolbar` del mockup:
`padding:12px 18px 0`, alto de fila 34 ⇒ 56 + 12 = 68 … 102).

| Control | Tipo | Propiedades |
|---|---|---|
| `tabVistaLot` | **Lista de pestañas moderna** (Insertar → Moderno → Lista de pestañas) | X `18` · Y `68` · Width `176` · Height `34` · `Items` `["Lotes", "Planillas"]` · `DefaultSelectedItems` `["Lotes"]` · `OnChange` ver abajo |
| `inpBuscarLot` | Entrada de texto clásica | X `204` · Y `69` · Width `320` · Height `32` · `PaddingLeft` `28` · `HintText` `If(tabVistaLot.Selected.Value = "Lotes", "Buscar por lote o transportadora…", "Buscar por planilla o empresa…")` · **`DelayOutput` `true`** · `Clear` `true` · `OnChange` `UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})` |
| `icoBuscarLot` | Icono clásico `Icon.Search` | X `214` · Y `77` · Width `16` · Height `16` · `Color` `ThemeInk3` · `DisplayMode` `DisplayMode.View` |
| `drpEstadoLot` | Menú desplegable clásico | X `534` · Y `69` · Width `150` · Height `32` · `Items` `["Todos", "En progreso", "Recibido", "En bóveda"]` · `Default` `"Todos"` · `OnChange` `UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})` |
| `dtpDesdeLot` | **Selector de fecha moderno** | X `694` · Y `69` · Width `150` · Height `32` · `OnChange` ver abajo |
| `lblRangoLot` | Etiqueta clásica | X `850` · Y `69` · Width `14` · Height `32` · `Align` `Align.Center` · `Color` `ThemeInk3` · `Text` `"—"` |
| `dtpHastaLot` | **Selector de fecha moderno** | X `870` · Y `69` · Width `150` · Height `32` · `OnChange` ver abajo |
| `icoLimpiarFechasLot` | Icono clásico `Icon.Cancel` | X `1030` · Y `77` · Width `16` · Height `16` · `Color` `ThemeInk3` · `OnSelect` ver abajo |
| `icoRefrescarLot` | Icono clásico `Icon.Reload` | X `1058` · Y `75` · Width `20` · Height `20` · `Color` `ThemeInk2` · `OnSelect` **§4.1** |

**`tabVistaLot.OnChange`:**

```powerfx
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

> 🧨 **Sin `Reset(galLotesLot)` / `Reset(galPlanillasLot)` acá, a propósito.**
> Cambiar de tab **no cambia el `Items` de ninguna de las dos galerías** (cada una
> tiene el suyo y las dos siguen vivas debajo del `Visible`): resetearlas tira la
> selección del usuario en la vista que está dejando, así que volver al tab
> anterior lo deposita en la primera fila en vez de donde estaba. Los dos `Reset`
> **siguen existiendo en el `OnVisible`** (§1), que es donde sí hacen falta —
> entrar a la pantalla de nuevo tiene que empezar limpio. Lo único que el tab sí
> tiene que soltar es la **tula** seleccionada, que es un `ctx` compartido por las
> dos vistas.

**`dtpDesdeLot.OnChange`:**

```powerfx
UpdateContext(
    {
        ctxDesdeLot:   Coalesce(dtpDesdeLot.SelectedDate, Date(1900, 1, 1)),
        ctxTulaSelLot: Defaults('CH-Tbl-Bags')
    }
)
```

**`dtpHastaLot.OnChange`:**

```powerfx
UpdateContext(
    {
        ctxHastaLot:   DateAdd(Coalesce(dtpHastaLot.SelectedDate, Date(2099, 12, 31)), 1, TimeUnit.Days),
        ctxTulaSelLot: Defaults('CH-Tbl-Bags')
    }
)
```

**`icoLimpiarFechasLot.OnSelect`:**

```powerfx
Reset(dtpDesdeLot);
Reset(dtpHastaLot);
UpdateContext(
    {
        ctxDesdeLot:   Date(1900, 1, 1),
        ctxHastaLot:   Date(2100, 1, 1),
        ctxTulaSelLot: Defaults('CH-Tbl-Bags')
    }
)
```

> **El `+1 día` de `ctxHastaLot` no es un descuido**: `'Created On'` es
> **DateTime**, y un lote creado el 15-ago 16:40 NO cumple
> `'Created On' <= Date(2026,8,15)` (que vale 15-ago 00:00). El predicado se
> escribe `'Created On' < ctxHastaLot` con el centinela corrido un día ⇒ el rango
> es **inclusivo del día "hasta" completo**, que es lo que espera el usuario.
> Escribirlo con `<=` y sin el `DateAdd` es el bug clásico de "no me aparece el
> lote de hoy".
>
> **El `HintText` del buscador es CONDICIONAL al tab, y no es cosmética**: el
> buscador **no busca lo mismo en las dos vistas**. En *Lotes* filtra por **N° de
> lote** y **transportadora** (§6.2); en *Planillas*, por **N° de planilla** y
> **remitente** (§10.2). Cada vista busca sobre las columnas **de su propia
> tabla**, que es lo único delegable: cruzar las jerarquías (buscar una planilla
> desde el tab Lotes) exigiría un `Filter` anidado por fila (D-1 + D-4). Un
> `HintText` fijo que prometa las cuatro cosas es una **mentira de UI** — el
> usuario tipea "00860" en Lotes, no aparece nada y culpa a la app. Divergencia
> **DL-17**; el tab *Planillas* cubre el caso.
>
> **`DelayOutput true` en el buscador** es obligatorio: sin él, cada tecla dispara
> la consulta delegable de la lista maestra Y los dup-checks de los diálogos.
> Con `true`, Studio espera ~0,5 s de inactividad.
>
> **Si el IntelliSense nombra `Value` en vez de `SelectedDate`** en el Selector de
> fecha moderno, **el suyo manda**: cambiá las dos referencias y avisá para
> corregir esta guía. Si tu versión no acepta `Reset()` sobre el control moderno,
> el equivalente es apuntar su `DefaultDate` a un `ctxResetFechasLot` que
> `icoLimpiarFechasLot` ponga en `Blank()`.
>
> 🪜 **ESCOTILLA 3 — los nombres de propiedad de la Lista de pestañas moderna.**
> Esta guía usa `tabVistaLot.Items`, `tabVistaLot.DefaultSelectedItems` y
> `tabVistaLot.Selected.Value`. Los controles **modernos** todavía mueven nombres
> entre versiones de la plataforma: hay builds donde la colección de opciones no se
> llama `Items`, donde la preselección no es `DefaultSelectedItems`, o donde la
> selección se lee `tabVistaLot.Selected.Text` en vez de `.Value`. **Si el
> IntelliSense de tu Studio difiere, EL SUYO MANDA**: usá el nombre que te ofrece y
> **cambialo en TODAS las apariciones** — la conjunción de §0.1 está en decenas de
> `Visible`, así que hacelo con buscar-y-reemplazar sobre el YAML desempaquetado y
> avisá para corregir esta guía. Lo que **no** cambia es el contrato: las dos
> opciones se llaman exactamente `"Lotes"` y `"Planillas"`, y la comparación es
> contra esas dos cadenas.

---

## 4. Motor de recarga (`icoRefrescarLot`)

### 4.1 `icoRefrescarLot.OnSelect` — COMPLETO

```powerfx
Refresh('CH-Tbl-ReceptionBatches');
Refresh('CH-Tbl-Receptions');
Refresh('CH-Tbl-Bags');
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

Un **único lugar** que fuerza el round-trip de **las tres** tablas de trabajo. Lo
disparan **dos**, y ninguno es automático de rutina:

| Quién | Por qué las TRES tablas |
|---|---|
| El usuario, tocando el ⟳ | quiere ver lo que cambiaron otros: no hay forma de saber qué tabla |
| *Mover a bóveda* al cerrar (§8.2) | la operación mueve **el lote y sus tulas**: cambian `CH-Tbl-ReceptionBatches` **y** `CH-Tbl-Bags`, y la lista maestra de lotes tiene que reflejar el estado nuevo |

**Las cuatro operaciones de tula (§14) NO lo llaman**: hacen un
`Refresh('CH-Tbl-Bags')` **puntual**, la única tabla que tocaron. Recargar además
las maestras les cambiaría el `Selected` a las galerías y la planilla del detalle
se movería sola bajo los pies del usuario. §14.5 (*Editar planilla*) no refresca
nada: solo crea una solicitud.

Cada quien pone su propio `Notify` de resultado **antes** de recargar, así nunca
se apilan dos toasts por una sola acción.

> ✅ **Esto NO es una violación de D-5: son las excepciones que la regla declara.**
> `01` §2.3 lo dice explícito — `Refresh(ds)` está **prohibido como parche de estado
> local después de un `Patch`** (el vicio de la v1) y **permitido** en las tres formas
> acotadas que la regla enumera: el ⟳ del usuario, el `Select(icoRefrescarLot)` del
> cierre de *Mover a bóveda* y el `Refresh('CH-Tbl-Bags')` puntual de las cuatro
> operaciones de tula. Trazabilidad: **DL-10 en §15.4**, hoy marcada como ACEPTADA.
> No lo cambies sin leer `01` §2.3.

### 4.2 Por qué las galerías maestras se atan al datasource y NO a una colección

`01 §4` llegó a anticipar `colLotes`/`colPlanillasLot`/`colTulasLot`. **No existen**
—ya se quitaron de `01` §4— y la razón es la que motivó todo el rediseño:

| | Galería atada al datasource (lo elegido) | Galería atada a una colección |
|---|---|---|
| Filas visibles | **sin techo** — la galería pagina sola | **techo de `DataRowLimit` = 2000** |
| Filtro/búsqueda | reactivos, cero plumbing | hay que re-ejecutar el `ClearCollect` por cada tecla |
| Truncamiento | imposible | **silencioso** con >2000 lotes |

El plan de estrés (00 §4.5) siembra **más de 2000 lotes**. Una colección se
comería la lista de administración exactamente igual que la v1 (00 §1.2). Por eso
las **cuatro** galerías de esta pantalla tienen `Items` delegable directo, y el
"actualizar" es un `Refresh()` explícito y voluntario, nunca un parche de estado
después de un `Patch` (que es lo que prohíbe la regla D-5).

---

## 5. Cards del master-detail (chrome compartido)

Geometría derivada del mockup (`.body2{padding:12px 18px 16px; gap:14px}`, master
`flex:1.05`, detalle `flex:1`): body de `Y = 114` a `Y = 752`, ancho útil
`1366 − 36 − 14 = 1316` ⇒ master `674`, detalle `642`.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnCardListaLot` | Botón clásico (receta 01 §7.1) | X `18` · Y `114` · Width `674` · Height `638` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `13` · `DisplayMode` `DisplayMode.View` · **sin conjunción de tab** (§0.1) |
| `btnHeadListaLot` | Botón clásico (receta 01 §7.1) | X `19` · Y `115` · Width `672` · Height `30` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderThickness` `0` · **`RadiusTopLeft`/`RadiusTopRight` `12`** · `RadiusBottomLeft`/`RadiusBottomRight` `0` · `DisplayMode` `DisplayMode.View` |
| `lblHeadListaLotesLot` | Etiqueta clásica | X `32` · Y `115` · Width `500` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"LOTE · TRANSPORTADORA · AUTOR"` · **`Visible` `tabVistaLot.Selected.Value = "Lotes"`** |
| `lblHeadListaPlanLot` | Etiqueta clásica | X `32` · Y `115` · Width `500` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"PLANILLA · REMITENTE — LOTE · ESTADO"` · **`Visible` `tabVistaLot.Selected.Value = "Planillas"`** |
| `shpLineaHeadListaLot` | Rectángulo clásico | X `18` · Y `145` · Width `674` · Height `1` · `Fill` `ThemeLine` |
| `btnCardDetLot` | Botón clásico (receta 01 §7.1) | X `706` · Y `114` · Width `642` · Height `638` · mismas propiedades que `btnCardListaLot` · **sin conjunción de tab** |

> **La banda del encabezado es un Botón clásico con radio superior 12, no un
> Rectángulo**: el Rectángulo no tiene `Radius` (01 §0.3) y sus esquinas vivas
> asomarían por fuera de la card redondeada. `12` = `13` de la card − `1` del
> borde. Divergencia **DL-13**.

---

## 6. Vista LOTES — lista maestra (`galLotesLot`)

### 6.1 La galería

| Propiedad | Valor |
|---|---|
| X | `18` |
| Y | `146` |
| Width | `674` |
| Height | `606` (= `btnCardListaLot.Y + btnCardListaLot.Height - Self.Y`) |
| `TemplateSize` | `46` (lista maestra de dos líneas, receta 01 §7.3) |
| `TemplatePadding` | `0` |
| `ShowScrollbar` | `true` |
| **`Visible`** | `tabVistaLot.Selected.Value = "Lotes"` |
| `Items` | ver §6.2 |
| `OnSelect` | `UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})` |

### 6.2 `galLotesLot.Items` — COMPLETO

```powerfx
If(
    drpEstadoLot.Selected.Value = "Todos",
    SortByColumns(
        Filter(
            'CH-Tbl-ReceptionBatches',
            (StartsWith(Name, inpBuscarLot.Text) || StartsWith(Transportadora, inpBuscarLot.Text))
            && 'Created On' >= ctxDesdeLot
            && 'Created On' <  ctxHastaLot
        ),
        "createdon",
        SortOrder.Descending
    ),
    SortByColumns(
        Filter(
            'CH-Tbl-ReceptionBatches',
            ReceptionStatus =
                Switch(
                    drpEstadoLot.Selected.Value,
                    "En progreso", 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso',
                    "Recibido",    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido,
                    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda'
                )
            && (StartsWith(Name, inpBuscarLot.Text) || StartsWith(Transportadora, inpBuscarLot.Text))
            && 'Created On' >= ctxDesdeLot
            && 'Created On' <  ctxHastaLot
        ),
        "createdon",
        SortOrder.Descending
    )
)
```

> **Delegación — DELEGABLE**, y el `If` va en el nivel **MÁS EXTERNO** a
> propósito. Con la forma `SortByColumns(If(cond, Filter(...), Filter(...)), …)`
> el `If` devuelve una tabla **ya materializada en el cliente** y el
> `SortByColumns` externo **ordena lo truncado**. Cada rama tiene que ser una
> cadena completa `SortByColumns(Filter(ds, …))`, repetida a propósito.
> **Este también es el motivo de no escribir `drp.Selected.Value = "Todos" || ReceptionStatus = …`**:
> ese `||` mezcla una constante de cliente con una columna y Dataverse no lo
> pliega.
>
> Dentro del predicado, TODO es columna propia de la tabla: `Name` (`nvarchar`,
> es el autonumber `RB-{SEQNUM:5}`), `Transportadora` (`nvarchar`),
> `ReceptionStatus` (picklist, `eq` contra un literal de option set) y
> `'Created On'` (datetime, `ge`/`lt`). `StartsWith` es `startswith` en OData;
> `startswith || startswith` delega.
>
> **El texto vacío no rompe nada**: `StartsWith(col, "")` es **true** para toda
> fila ⇒ NO hace falta la guarda `IsBlank(inpBuscarLot.Text) || …`, que es
> justamente lo que suele romper la delegación.
>
> 🧨 **La fuente es la TABLA, no la vista.** Versiones anteriores de esta guía
> escribían `Filter('CH-Tbl-ReceptionBatches (Views)'.'Reception app view', <predicado>)`
> y **eso NO COMPILA**: el primer argumento de `Filter` tiene que ser la tabla, y una
> vista solo puede ir como **segundo** argumento —forma que no admite predicado
> (`01` §2.5). Como este `Items` **necesita** predicado propio (estado + búsqueda +
> fechas), la vista queda fuera. No se pierde nada: su `ORDER BY createdon DESC`
> **nunca se heredaba** —es la regla dura **D-9** (`01` §2.3)— y por eso el
> `SortByColumns` explícito ya estaba. `"createdon"` va con el **nombre lógico**
> (obligatorio).
>
> 🚫 **El autor NO entra en el `Filter`** (ledger 00 §5, **L-2**): sin el espejo
> `sanic_createdbyname` (B0-1) no hay columna de texto delegable con el nombre
> del creador. El autor **se muestra** en la fila (§6.3) navegando el lookup del
> registro ya traído, que es otra cosa.

### 6.3 Template de `galLotesLot` (X del template = X del encabezado − 18)

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaLotesLot` | Botón clásico (overlay de fila, receta 01 §7.3) | X `0` · Y `0` · Width `galLotesLot.Width` · Height `46` (= `TemplateSize`) · **al FONDO del z-order del template** · `AccessibleLabel` `"Seleccionar lote " & ThisItem.Name` |
| `shpSelLotesLot` | Botón clásico (receta 01 §7.3) | X `0` · Y `0` · Width `galLotesLot.Width` · Height `46` · `Fill` `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` |
| `shpSelBarraLotesLot` | Rectángulo clásico (receta 01 §7.3) | X `0` · Y `0` · Width `3` · Height `46` · `Fill` `ThemeAccent` · `Visible` `ThisItem.IsSelected` |
| `lblFilaLoteNumLot` | Etiqueta clásica | X `14` · Y `6` · Width `300` · Height `16` · `ESTILO-CELDA-FUERTE` · `Text` `ThisItem.Name` · `OnSelect` `Select(Parent)` |
| `lblFilaLoteMetaLot` | Etiqueta clásica | X `14` · Y `24` · Width `440` · Height `15` · `ESTILO-META` · `Text` ver abajo · `OnSelect` `Select(Parent)` |
| `pilFilaLoteLot` | Botón clásico (píldora, receta 01 §7.4) | X `galLotesLot.Width - 14 - Self.Width` · Y `12` · Height `22` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `Text`/`Fill`/`Color` = los tres `Switch` de **`ReceptionStatus`** de la receta 01 §7.4, tal cual |
| `shpSepLotesLot` | Rectángulo clásico (receta 01 §7.3) | X `0` · Y `45` (= `TemplateSize − 1`) · Width `galLotesLot.Width` · Height `1` · `Fill` `ThemeLineSoft` · `OnSelect` `Select(Parent)` |

**`lblFilaLoteMetaLot.Text`:**

```powerfx
ThisItem.Transportadora & "  ·  " & Text(ThisItem.'Created On', FmtFechaCorta) &
"  ·  " & ThisItem.'Created By'.'Full Name'
```

> **`'Created By'.'Full Name'` es la ÚNICA expansión de lookup permitida dentro
> de una galería** (01 §2.1): `'Full Name'` es el **nombre primario** de `Users` y
> Dataverse lo devuelve como anotación en la misma respuesta que la fila — **no**
> cuesta un request por fila. Cualquier otra columna del registro relacionado sí
> costaría un fetch por fila visible y está prohibida.
>
> **La lógica de fila vive en `galLotesLot.OnSelect`**, no en el overlay, y
> **TODO hijo del template lleva `Select(Parent)`** — incluido el separador de 1 px
> (receta 01 §7.3). Si la acción viviera solo en `btnFilaLotesLot`, las etiquetas
> se comerían el clic y habría que cazar píxeles vacíos.
>
> **`FmtFechaCorta` = `"dd/mm HH:mm"`** — `HH` es 24 h; `hh` sería reloj de 12 h
> **sin AM/PM** (01 §3.1).

### 6.4 Estado vacío de la lista de lotes

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioLotesLot` | Etiqueta clásica | X `48` · Y `250` · Width `614` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Ningún lote coincide con el filtro."` · **`Visible` `tabVistaLot.Selected.Value = "Lotes" && IsBlank(galLotesLot.Selected.'CH-Tbl-ReceptionBatch')`** |

---

## 7. Vista LOTES — detalle (`btnCardDetLot`)

Todos los controles de este bloque llevan la conjunción
`tabVistaLot.Selected.Value = "Lotes" && !IsBlank(galLotesLot.Selected.'CH-Tbl-ReceptionBatch')` en su
`Visible`, **sin excepción** (§0.1). Se abrevia abajo como **`VIS-LOTE`**.

> **Por qué la PK y no `IsBlank(galLotesLot.Selected)` pelado** (§0.2): con `Items`
> vacío la galería no devuelve blank, devuelve un **registro tipado vacío** — y ese
> registro **no es blank**. La PK en blanco sí lo es. Misma forma en las 6 apariciones.

### 7.1 Cabecera del lote

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDetLoteTitLot` | Etiqueta clásica | X `722` · Y `125` · Width `380` · Height `18` · `ESTILO-DETALLE-TITULO` · `Text` `"Lote " & galLotesLot.Selected.Name` · `Visible` **VIS-LOTE** |
| `lblDetLoteSubLot` | Etiqueta clásica | X `722` · Y `144` · Width `440` · Height `14` · `ESTILO-META` · `Text` ver abajo · `Visible` **VIS-LOTE** |
| `pilDetLoteLot` | Botón clásico (píldora, receta 01 §7.4) | X `1332 - Self.Width` · Y `132` · Height `22` · `DisplayMode` `DisplayMode.View` · `Text`/`Fill`/`Color` = los tres `Switch` de la receta 01 §7.4 **cambiando `ThisItem.ReceptionStatus` por `galLotesLot.Selected.ReceptionStatus`** · `Visible` **VIS-LOTE** |
| `shpLineaDetLoteLot` | Rectángulo clásico | X `706` · Y `170` · Width `642` · Height `1` · `Fill` `ThemeLine` · `Visible` **VIS-LOTE** |

**`lblDetLoteSubLot.Text`:**

```powerfx
galLotesLot.Selected.Transportadora & "  ·  " &
Text(galLotesLot.Selected.'Created On', FmtFecha) & "  ·  " &
galLotesLot.Selected.'Created By'.'Full Name'
```

### 7.2 Comandos del lote

| Control | Tipo | Propiedades |
|---|---|---|
| `btnMoverBovedaLot` | Botón clásico `ESTILO-BTN-CHICO-ACC` | X `720` · Y `181` · Width `128` · Height `26` · `Text` `"Mover a bóveda"` · `DisplayMode` ver abajo · `OnSelect` **§8.1** · `Visible` **VIS-LOTE** |
| `btnEditarLoteLot` | Botón clásico `ESTILO-BTN-CHICO` | X `856` · Y `181` · Width `96` · Height `26` · `Text` `"Editar lote"` · `Tooltip` `"Solo lotes En progreso"` · `DisplayMode` ver abajo · `OnSelect` **§9** · `Visible` **VIS-LOTE** |
| `shpLineaOpsLoteLot` | Rectángulo clásico | X `706` · Y `btnMoverBovedaLot.Y + btnMoverBovedaLot.Height + 10` · Width `642` · Height `1` · `Fill` `ThemeLineSoft` · `Visible` **VIS-LOTE** |

```powerfx
// btnMoverBovedaLot.DisplayMode
If(
    galLotesLot.Selected.ReceptionStatus = 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido,
    DisplayMode.Edit,
    DisplayMode.Disabled
)

// btnEditarLoteLot.DisplayMode
If(
    galLotesLot.Selected.ReceptionStatus = 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso',
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

> **La habilitación se escribe contra el ESTADO DEL LOTE** (mata el bug N9 de la
> v1) y contra el control que la acción realmente usa (mata N7): las dos
> condiciones miran `galLotesLot.Selected`, que es exactamente el registro que el
> `OnSelect` va a patchear.

### 7.3 Mini-tabla: planillas del lote

Encabezado (4 columnas — **sin columna "Tulas"**, divergencia **DL-3**):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnHeadPlanLoteLot` | Botón clásico (receta 01 §7.1) | X `707` · Y `shpLineaOpsLoteLot.Y + 1` · Width `640` · Height `30` · `Fill`/`Hover`/`Pressed` `ThemeSurface2` · `Radius` (las 4) `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Visible` **VIS-LOTE** |
| `lblHeadPlanLoteNumLot` | Etiqueta clásica | X `722` · Y `btnHeadPlanLoteLot.Y` · Width `96` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"PLANILLA"` · `Visible` **VIS-LOTE** |
| `lblHeadPlanLoteRemLot` | Etiqueta clásica | X `826` · Y `btnHeadPlanLoteLot.Y` · Width `246` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"REMITENTE"` · `Visible` **VIS-LOTE** |
| `lblHeadPlanLoteNioLot` | Etiqueta clásica | X `1076` · Y `btnHeadPlanLoteLot.Y` · Width `120` · Height `30` · `ESTILO-HEADER-COL` · `Align` `Align.Right` · `Text` `"TOTAL C$"` · `Visible` **VIS-LOTE** |
| `lblHeadPlanLoteUsdLot` | Etiqueta clásica | X `1206` · Y `btnHeadPlanLoteLot.Y` · Width `126` · Height `30` · `ESTILO-HEADER-COL` · `Align` `Align.Right` · `Text` `"TOTAL US$"` · `Visible` **VIS-LOTE** |
| `shpLineaHeadPlanLoteLot` | Rectángulo clásico | X `706` · Y `btnHeadPlanLoteLot.Y + btnHeadPlanLoteLot.Height` · Width `642` · Height `1` · `Fill` `ThemeLine` · `Visible` **VIS-LOTE** |

**`galPlanillasDelLoteLot`** (Galería vertical en blanco):

| Propiedad | Valor |
|---|---|
| X | `706` |
| Y | `shpLineaHeadPlanLoteLot.Y + 1` |
| Width | `642` |
| Height | `btnCardDetLot.Y + btnCardDetLot.Height - Self.Y` |
| `TemplateSize` | `36` (mini-tabla, receta 01 §7.3) |
| `TemplatePadding` | `0` · `ShowScrollbar` `true` |
| `Visible` | **VIS-LOTE** |
| `Items` | `SortByColumns(Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = galLotesLot.Selected.'CH-Tbl-ReceptionBatch'), "createdon", SortOrder.Ascending)` |

> 🧨 **DELEGABLE, y va PK contra PK** (regla D-1, `01` §2.3). La forma
> `ReceptionBatch = galLotesLot.Selected` **NO COMPILA**: Power Fx no compara una
> columna de lookup contra un registro. Y la PK del lookup **sí** delega — Dataverse
> la devuelve como anotación del propio registro (`_sanic_receptionbatch_value`) y el
> `$filter` se pliega a un `eq` de GUID. Lo que D-1 prohíbe es navegar a una **columna
> de datos** del otro lado (`ReceptionBatch.Name`), que sí arrastra la tabla al
> cliente.
>
> **El `SortByColumns` explícito es la regla dura D-9** (01 §2.3), igual que en las
> dos maestras: un `Filter` sobre un datasource **no garantiza** ningún orden, y sin
> él las planillas del lote salen en el orden que el servidor tenga ganas — que
> cambia entre cargas. **Ascendente** por `"createdon"` (nombre **lógico**,
> obligatorio): una mini-tabla de detalle se lee en el orden en que se cargaron, no
> al revés como la lista de administración.
>
> **Estas filas NO son clicables** (el mockup las dibuja como `div`, no como
> `button`): por eso el template **no lleva** overlay, ni `shpSel…`, ni barra de
> selección, ni `Select(Parent)`. La receta 01 §7.3 aplica solo a filas con
> acción.

Template (X del template = X del encabezado − 706):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblCelPlanLoteNumLot` | Etiqueta clásica | X `16` · Y `0` · Width `96` · Height `36` · `ESTILO-CELDA-FUERTE` · `Text` `ThisItem.ReceiptID` |
| `lblCelPlanLoteRemLot` | Etiqueta clásica | X `120` · Y `0` · Width `246` · Height `36` · `ESTILO-CELDA` · `Text` `ThisItem.RemitentName` |
| `lblCelPlanLoteNioLot` | Etiqueta clásica | X `370` · Y `0` · Width `120` · Height `36` · `ESTILO-CELDA-NUM` · `Text` `SimboloNIO & " " & Text(ThisItem.NIOCash + ThisItem.NIOCheck, FmtMoney)` |
| `lblCelPlanLoteUsdLot` | Etiqueta clásica | X `500` · Y `0` · Width `126` · Height `36` · `ESTILO-CELDA-NUM` · `Text` `SimboloUSD & " " & Text(ThisItem.USDCash + ThisItem.USDCheck, FmtMoney)` |
| `shpSepPlanLoteLot` | Rectángulo clásico | X `0` · Y `35` (= `TemplateSize − 1`) · Width `642` · Height `1` · `Fill` `ThemeLineSoft` |

> La suma `NIOCash + NIOCheck` es **por registro ya traído**, no un `Sum()` sobre
> el datasource: no toca la regla D-8.

### 7.4 Estados vacíos del detalle de lote

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioPlanLoteLot` | Etiqueta clásica | X `736` · Y `galPlanillasDelLoteLot.Y + 40` · Width `582` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Este lote no tiene planillas."` · `Visible` **VIS-LOTE** `&& IsEmpty(galPlanillasDelLoteLot.AllItems)` |
| `lblVacioDetLoteLot` | Etiqueta clásica | X `736` · Y `380` · Width `582` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Sin lote que mostrar: ningún lote coincide con el filtro."` · **`Visible` `tabVistaLot.Selected.Value = "Lotes" && IsBlank(galLotesLot.Selected.'CH-Tbl-ReceptionBatch')`** |

---

## 8. Mover a bóveda

Dos fases, **primero las tulas y solo si TODO salió bien el lote**. El guard
server-side (`GuardReceptionPlugin`) respalda, pero la app no se apoya en él para
decidir: cada fase tiene su `IfError` y su mensaje.

### 8.1 `btnMoverBovedaLot.OnSelect` — abre el diálogo y carga el universo

```powerfx
ClearCollect(
    colTulasBovedaLot,
    Filter(
        'CH-Tbl-Bags',
        // espejo B-2 (D-7) + PK contra PK (D-1): la igualdad de registro NO compila
        ReceptionBatchId.'CH-Tbl-ReceptionBatch' = galLotesLot.Selected.'CH-Tbl-ReceptionBatch'
        && BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'
    )
);
UpdateContext({ctxDlgBovedaLot: true})
```

> **DELEGABLE** por el espejo **B-2** `ReceptionBatchId` (01 §2.2, regla D-7):
> **una sola consulta** trae las tulas de TODO el lote sin navegar
> `ReceptionID.ReceptionBatch`. Si IntelliSense no autocompleta
> `ReceptionBatchId`, **parás acá**: quitá y volvé a agregar `CH-Tbl-Bags` desde
> el panel Datos (01 §2.2 — es el ítem de checklist de 01 que bloquea esta guía).
>
> **Solo las tulas en `'Recibida en esclusa'`** entran en la colección (igual que
> `doMover()` del mockup, que filtra `x.st==='esclusa'`). Eso hace la operación
> **idempotente**: si quedó a medias, volver a ejecutarla no vuelve a tocar lo
> que ya está en bóveda.
>
> El contador del diálogo se calcula con `CountRows(colTulasBovedaLot)` — sobre la
> **colección local**, nunca `CountRows(Filter(ds, …))` (regla D-2).

### 8.2 `btnDlgOkBovedaLot.OnSelect` — COMPLETO

```powerfx
UpdateContext({ctxDlgBovedaLot: false});

If(
    IsEmpty(colTulasBovedaLot),

    // ---- Caso A: no hay tulas en esclusa. Solo transiciona el lote. ----
    IfError(
        Patch(
            'CH-Tbl-ReceptionBatches',
            galLotesLot.Selected,
            { ReceptionStatus: 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda' }
        ),
        Notify("El lote no se movió: " & FirstError.Message, NotificationType.Error),
        Notify(
            "Lote " & galLotesLot.Selected.Name & " movido a bóveda (no tenía tulas en esclusa).",
            NotificationType.Success
        )
    ),

    // ---- Caso B: FASE 1 = las tulas, en UN SOLO Patch con tabla ----
    IfError(
        Patch(
            'CH-Tbl-Bags',
            colTulasBovedaLot,
            ForAll(
                colTulasBovedaLot,
                { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda' }
            )
        ),
        Notify(
            "Las tulas NO se movieron (" & FirstError.Message &
            "). El lote sigue en Recibido: no se cambió su estado.",
            NotificationType.Error
        ),

        // ---- FASE 2: el lote, SOLO si las tulas pasaron ----
        IfError(
            Patch(
                'CH-Tbl-ReceptionBatches',
                galLotesLot.Selected,
                { ReceptionStatus: 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda' }
            ),
            Notify(
                "Quedó A MEDIAS: las " & CountRows(colTulasBovedaLot) &
                " tula(s) ya están en bóveda pero el lote sigue en Recibido (" &
                FirstError.Message & "). Volvé a ejecutar «Mover a bóveda»: la operación es idempotente.",
                NotificationType.Error
            ),
            Notify(
                "Lote " & galLotesLot.Selected.Name & " movido a bóveda: " &
                CountRows(colTulasBovedaLot) & " tula(s) almacenadas.",
                NotificationType.Success
            )
        )
    )
);

Clear(colTulasBovedaLot);
Select(icoRefrescarLot)
```

**Reglas de este bloque** (receta 01 §7.9, regla 4):

1. **UN solo `Patch` con tabla**, jamás un `ForAll` de `Patch` individuales: el
   `ForAll` de acá construye la **tabla de cambios** alineada fila a fila con la
   tabla base (`colTulasBovedaLot`), igual que la FASE A de la receta 01 §7.5.
2. **El lote se mueve solo si las tulas se movieron.** No es atómico
   client-side y por eso el mensaje del caso "a medias" es explícito y accionable
   — se declara acá y en el checklist (§16). El cierre atómico real llegaría con
   una Custom API equivalente a B0-3, que hoy **no existe**.
3. `Clear(colTulasBovedaLot)` corre SIEMPRE, haya fallado o no: la colección es
   un universo de trabajo, no estado persistente.
4. **`Select(icoRefrescarLot)` va último**, después del `Notify` de resultado, así
   los toasts no se apilan (§4.1).

---

## 9. Editar lote → `scrCaptura`

`btnEditarLoteLot.OnSelect` — COMPLETO. Habilitado **solo** en `'En progreso'`
(§7.2). Carga el lote entero con **DOS** consultas delegables y navega.

Este bloque es **copia literal del contrato canónico de `01` §4.3** (que `02`
también reproduce en su CONTRATO DE ENTRADA). No se toca sin tocar `01`: es la
única definición, y las tres tienen que decir lo mismo.

```powerfx
// btnEditarLoteLot.OnSelect — habilitado SOLO con el lote en 'En progreso'
Set(gvarLoteActivo, galLotesLot.Selected);

// ===== 1. Snapshots CRUDOS: dos consultas, una por tabla, para TODO el lote =====
//    PK contra PK (D-1): la igualdad de registro pelada NO COMPILA.
ClearCollect(
    colPlanillasDV,
    Filter(
        'CH-Tbl-Receptions',
        ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
    )
);
ClearCollect(
    colTulasDV,
    Filter(
        'CH-Tbl-Bags',
        ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
    )
);

// ===== 2. Vaciar las colecciones de EDICIÓN — 🧨 OBLIGATORIO =====
//    scrLotes NO las proyecta: lo hace el OnVisible de scrCaptura con la receta
//    01 §7.10. Vaciarlas es lo que HABILITA esa proyección: su guarda es
//    IsEmpty(colPlanillas). Si el usuario venía editando OTRO lote y no se
//    vacían, la proyección no dispara y scrCaptura muestra las filas del lote
//    anterior con el gvarLoteActivo nuevo.
Clear(colPlanillas); Clear(colTulas);
Clear(colProblemas); Clear(colFallidas);

// ===== 3. Salida =====
Set(gvarCambiosSinGuardar, false);
Notify("Lote " & galLotesLot.Selected.Name & " cargado en Captura.", NotificationType.Information);
Navigate(scrCaptura)
```

**Por qué así**:

- **DOS consultas para el lote entero**, no una por planilla. Las tulas salen del
  espejo **B-2** (`ReceptionBatchId`, regla D-7): sin él haría falta un `ForAll`
  con una query anidada por planilla — el N+1 que mataba a la v1.
- **Esta pantalla NO proyecta `colPlanillas`/`colTulas`.** Antes lo hacía, con dos
  `ForAll` propios, y era un bug latente: (1) duplicaba una definición que también
  vive en `02`, y en cuanto una de las dos se toca el re-keyado de la receta 01
  §7.5 deja de encontrar sus filas ⇒ el segundo guardado **duplica registros**;
  (2) guardaba `Empresa: D.BranchName`, o sea la **referencia** del lookup, y la
  receta 7.5 hace `P.Empresa.Client` / `P.Empresa.Name` ⇒ **un fetch por fila** y
  `RemitentClient` en blanco. La proyección correcta rehidrata `Empresa` como
  **registro fresco de sucursal** con un `LookUp` contra `CH-Tbl-ClientBranches`, y
  la pantalla que la consume es `scrCaptura`. Por eso vive allá y no acá. Ver
  **receta 01 §7.10**.
- **`LocalId` lo fija la receta 7.10** (`01` §4.1): fila persistida ⇒
  `Text(GUID real)`, fila nueva ⇒ `Text(GUID())`. Y la receta rehidrata `Empresa`
  con un `LookUp` **contra `CH-Tbl-ClientBranches`**, no contra un catálogo local:
  ese catálogo se eliminó (**L-23**, regla **D-10**).
- **`gvarCambiosSinGuardar = false`**: se entra a captura sin deuda, así la guarda
  de salida de la receta 01 §7.8 no dispara un diálogo espurio.
- **Sin diálogo de confirmación**: cargar un lote no destruye nada y el mockup
  (`editarLote()`) navega directo. Sí conserva su **toast**: el `Notify` va
  **antes** del `Navigate` y se lee del registro fuente
  (`galLotesLot.Selected.Name`, no de `gvarLoteActivo`) para no depender del orden
  del `Set` de arriba. Es la única señal de que el cambio de pantalla fue
  intencional y de **qué** lote quedó cargado — sin él, el operador aterriza en
  `scrCaptura` sin saber si agarró el lote correcto.
- **Costo de red: exactamente 2 requests**, las dos delegables (Q-9). La proyección
  del otro lado corre sobre colecciones locales: cero consultas más.

---

## 10. Vista PLANILLAS — lista maestra (`galPlanillasLot`)

### 10.1 La galería

| Propiedad | Valor |
|---|---|
| X | `18` · Y `146` · Width `674` · Height `606` (mismas coordenadas que `galLotesLot`) |
| `TemplateSize` | `46` · `TemplatePadding` `0` · `ShowScrollbar` `true` |
| **`Visible`** | `tabVistaLot.Selected.Value = "Planillas"` |
| `Items` | ver §10.2 |
| `OnSelect` | `UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})` |

> `galLotesLot` y `galPlanillasLot` ocupan **exactamente el mismo rectángulo** y
> se excluyen por la conjunción del tab. Es la razón por la que la card maestra
> es chrome compartido (§0.1).

### 10.2 `galPlanillasLot.Items` — COMPLETO

```powerfx
If(
    drpEstadoLot.Selected.Value = "Todos",
    SortByColumns(
        Filter(
            'CH-Tbl-Receptions',
            (StartsWith(ReceiptID, inpBuscarLot.Text) || StartsWith(RemitentName, inpBuscarLot.Text))
            && 'Created On' >= ctxDesdeLot
            && 'Created On' <  ctxHastaLot
        ),
        "createdon",
        SortOrder.Descending
    ),
    SortByColumns(
        Filter(
            'CH-Tbl-Receptions',
            BatchStatus =
                Switch(
                    drpEstadoLot.Selected.Value,
                    "En progreso", 159460000,
                    "Recibido",    159460001,
                    159460003
                )
            && (StartsWith(ReceiptID, inpBuscarLot.Text) || StartsWith(RemitentName, inpBuscarLot.Text))
            && 'Created On' >= ctxDesdeLot
            && 'Created On' <  ctxHastaLot
        ),
        "createdon",
        SortOrder.Descending
    )
)
```

> **DELEGABLE.** El filtro por estado del lote va contra el **espejo entero**
> `BatchStatus` de `CH-Tbl-Receptions` y se compara contra los **números CRUDOS**
> (regla **D-6**, 01 §5.1): es una columna `int`, **no** un option set — escribir
> `BatchStatus = 'ReceptionStatus (…)'.Recibido` no compila.
> 🚫 Y por supuesto **nunca** `ReceptionBatch.ReceptionStatus = …`: eso es
> dot-walk (D-1) y arrastra la lista entera al cliente.
>
> `159460002` (**Anulado**) no está en el desplegable — el estado está muerto
> (00 §6) — pero la píldora igual lo mapea por completitud (§11.4).

### 10.3 Template de `galPlanillasLot` (X del template = X del encabezado − 18)

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaPlanLot` | Botón clásico (overlay de fila, receta 01 §7.3) | X `0` · Y `0` · Width `galPlanillasLot.Width` · Height `46` · **al FONDO del z-order del template** · `AccessibleLabel` `"Seleccionar planilla " & ThisItem.ReceiptID` |
| `shpSelPlanLot` | Botón clásico (receta 01 §7.3) | X `0` · Y `0` · Width `galPlanillasLot.Width` · Height `46` · `Fill` `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` |
| `shpSelBarraPlanLot` | Rectángulo clásico (receta 01 §7.3) | X `0` · Y `0` · Width `3` · Height `46` · `Fill` `ThemeAccent` · `Visible` `ThisItem.IsSelected` |
| `lblFilaPlanTitLot` | Etiqueta clásica | X `14` · Y `6` · Width `400` · Height `16` · `ESTILO-CELDA-FUERTE` · `Text` `ThisItem.ReceiptID & "  ·  " & ThisItem.RemitentName` · `OnSelect` `Select(Parent)` |
| `lblFilaPlanMetaLot` | Etiqueta clásica | X `14` · Y `24` · Width `400` · Height `15` · `ESTILO-META` · `Text` ver abajo · `OnSelect` `Select(Parent)` |
| `lblFilaPlanLoteLot` | Etiqueta clásica | X `galPlanillasLot.Width - 150` · Y `6` · Width `136` · Height `15` · `ESTILO-META` · `Align` `Align.Right` · `Text` `ThisItem.ReceptionBatch.Name` · `OnSelect` `Select(Parent)` |
| `pilFilaPlanLot` | Botón clásico (píldora, §11.4) | X `galPlanillasLot.Width - 14 - Self.Width` · Y `23` · Height `22` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `Text`/`Fill`/`Color` = los tres `Switch` de **§11.4** con `ThisItem.BatchStatus` |
| `shpSepPlanLot` | Rectángulo clásico | X `0` · Y `45` (= `TemplateSize − 1`) · Width `galPlanillasLot.Width` · Height `1` · `Fill` `ThemeLineSoft` · `OnSelect` `Select(Parent)` |

**`lblFilaPlanMetaLot.Text`:**

```powerfx
SimboloNIO & " " & Text(ThisItem.NIOCash + ThisItem.NIOCheck, FmtMoney) &
"  ·  " & SimboloUSD & " " & Text(ThisItem.USDCash + ThisItem.USDCheck, FmtMoney)
```

> **`ThisItem.ReceptionBatch.Name` es la segunda expansión de lookup permitida
> dentro de una galería, por el mismo motivo que `'Created By'.'Full Name'`**:
> `Name` es el **nombre primario** de `CH-Tbl-ReceptionBatches` (el autonumber
> `RB-{SEQNUM:5}`) y Dataverse lo devuelve como anotación del propio lookup. NO
> vale extender la licencia a `ThisItem.ReceptionBatch.Transportadora` ni a
> ninguna otra columna del lote: esas sí cuestan un fetch por fila y están
> prohibidas dentro de un template.
>
> **El meta NO trae "· N tulas"** (el mockup sí): sin el espejo `sanic_bagcount`
> (B0-2) no hay conteo delegable por fila — ledger 00 §5, **L-3**. Divergencia
> **DL-4**.

### 10.4 Estado vacío de la lista de planillas

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioPlanillasLot` | Etiqueta clásica | X `48` · Y `250` · Width `614` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Ninguna planilla coincide con el filtro."` · **`Visible` `tabVistaLot.Selected.Value = "Planillas" && IsBlank(galPlanillasLot.Selected.'CH-Tbl-Receptions')`** |

---

## 11. Vista PLANILLAS — detalle

Conjunción común de este bloque, abreviada **`VIS-PLAN`**:

```powerfx
tabVistaLot.Selected.Value = "Planillas" && !IsBlank(galPlanillasLot.Selected.'CH-Tbl-Receptions')
```

> **Por qué la PK y no `IsBlank(galPlanillasLot.Selected)` pelado** (§0.2): con `Items`
> vacío la galería devuelve un **registro tipado vacío**, que **no es blank**; la PK en
> blanco sí. Misma forma en las 6 apariciones.

### 11.1 Cabecera de la planilla

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDetPlanTitLot` | Etiqueta clásica | X `722` · Y `125` · Width `440` · Height `18` · `ESTILO-DETALLE-TITULO` · `Text` `"Planilla " & galPlanillasLot.Selected.ReceiptID & " — " & galPlanillasLot.Selected.RemitentName` · `Visible` **VIS-PLAN** |
| `lblDetPlanSubLot` | Etiqueta clásica | X `722` · Y `144` · Width `440` · Height `14` · `ESTILO-META` · `Text` ver abajo · `Visible` **VIS-PLAN** |
| `pilDetPlanLot` | Botón clásico (píldora, §11.4) | X `1332 - Self.Width` · Y `132` · Height `22` · `DisplayMode` `DisplayMode.View` · `Text`/`Fill`/`Color` = los tres `Switch` de §11.4 con `galPlanillasLot.Selected.BatchStatus` · `Visible` **VIS-PLAN** |
| `shpLineaDetPlanLot` | Rectángulo clásico | X `706` · Y `170` · Width `642` · Height `1` · `Fill` `ThemeLine` · `Visible` **VIS-PLAN** |

**`lblDetPlanSubLot.Text`:**

```powerfx
"Lote " & galPlanillasLot.Selected.ReceptionBatch.Name &
"  ·  " & galPlanillasLot.Selected.ReceptionBatch.Transportadora
```

> **Acá SÍ vale el dot-walk a `Transportadora`**: `galPlanillasLot.Selected` es
> **UN** registro, no una tabla. Resolver su lookup cuesta **un** request por
> selección — no uno por fila. Es la misma licencia que 01 §2.1 da para el autor
> del lote. Dentro de un template seguiría prohibido (§10.3).

### 11.2 Banner de bóveda

| Control | Tipo | Propiedades |
|---|---|---|
| `btnBannerBovedaLot` | Botón clásico (receta 01 §7.1) | X `720` · Y `179` · Width `614` · Height `34` · `Fill`/`HoverFill`/`PressedFill` `ThemeWarnSoft` · `BorderThickness` `0` · `Radius` (las 4) `9` · `DisplayMode` `DisplayMode.View` · **`Visible` VIS-PLAN `&& galPlanillasLot.Selected.BatchStatus = 159460003`** |
| `icoBannerBovedaLot` | Icono clásico `Icon.Lock` | X `732` · Y `187` · Width `16` · Height `16` · `Color` `ThemeWarn` · `DisplayMode` `DisplayMode.View` · `Visible` `btnBannerBovedaLot.Visible` |
| `lblBannerBovedaLot` | Etiqueta clásica | X `756` · Y `179` · Width `566` · Height `34` · Size `8.63` · `Color` `ThemeWarn` · `Text` `"Lote en bóveda: toda operación genera una solicitud de aprobación al supervisor."` · `Visible` `btnBannerBovedaLot.Visible` |

> El icono y la etiqueta referencian `btnBannerBovedaLot.Visible` en vez de
> repetir la condición: una sola regla, imposible de desincronizar (01 §0.9).

### 11.3 Fila de comandos

**Todo lo que va debajo del banner encadena su `Y` al banner**, así el layout se
corre solo cuando el banner aparece y no hay dos números mágicos que se
contradigan (01 §0.9):

```powerfx
// btnAgregarTulaLot.Y  — de acá cuelga TODO el resto del detalle
If(
    btnBannerBovedaLot.Visible,
    btnBannerBovedaLot.Y + btnBannerBovedaLot.Height + 9,
    shpLineaDetPlanLot.Y + 11
)
```

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAgregarTulaLot` | Botón clásico `ESTILO-BTN-CHICO-ACC` | X `720` · Y ver arriba · Width `118` · Height `26` · `Text` `"Agregar tula"` · `Tooltip` **TIP-PROG** · `DisplayMode` **G-PLAN** · `OnSelect` `UpdateContext({ctxDlgAgregarLot: true}); Reset(inpTulaAgregarLot)` · `Visible` **VIS-PLAN** |
| `btnEditarPlanillaLot` | Botón clásico `ESTILO-BTN-CHICO` | X `845` · Y `btnAgregarTulaLot.Y` · Width `108` · Height `26` · `Text` `"Editar planilla"` · `Tooltip` **TIP-PROG** · `DisplayMode` **G-PLAN** · `OnSelect` `UpdateContext({ctxDlgEditarPlanillaLot: true}); Reset(inpPlanillaEditarLot)` · `Visible` **VIS-PLAN** |
| `btnEditarTulaLot` | Botón clásico `ESTILO-BTN-CHICO` | X `1080` · Y `btnAgregarTulaLot.Y` · Width `72` · Height `26` · `Text` `"Editar"` · `Tooltip` **TIP-TULA** · `DisplayMode` **G-TULA** · `OnSelect` `UpdateContext({ctxDlgEditarTulaLot: true}); Reset(inpTulaEditarLot)` · `Visible` **VIS-PLAN** |
| `btnRemapearTulaLot` | Botón clásico `ESTILO-BTN-CHICO` | X `1159` · Y `btnAgregarTulaLot.Y` · Width `88` · Height `26` · `Text` `"Remapear"` · `Tooltip` **TIP-ULTIMA** · `DisplayMode` **G-TULA-ULTIMA** · `OnSelect` `UpdateContext({ctxDlgRemapearLot: true}); Reset(cmbDestinoRemapearLot)` · `Visible` **VIS-PLAN** |
| `btnEliminarTulaLot` | Botón clásico `ESTILO-BTN-CHICO-DAN` | X `1254` · Y `btnAgregarTulaLot.Y` · Width `78` · Height `26` · `Text` `"Eliminar"` · `Tooltip` **TIP-ULTIMA** · `DisplayMode` **G-TULA-ULTIMA** · `OnSelect` `UpdateContext({ctxDlgEliminarLot: true})` · `Visible` **VIS-PLAN** |
| `shpLineaOpsPlanLot` | Rectángulo clásico | X `706` · Y `btnAgregarTulaLot.Y + btnAgregarTulaLot.Height + 10` · Width `642` · Height `1` · `Fill` `ThemeLineSoft` · `Visible` **VIS-PLAN** |

**`G-PLAN`** — habilitación de las dos acciones de planilla (idéntica en las dos):

```powerfx
If(
    IsBlank(galPlanillasLot.Selected.'CH-Tbl-Receptions')
    || galPlanillasLot.Selected.BatchStatus = 159460000,
    DisplayMode.Disabled,
    DisplayMode.Edit
)
```

**`G-TULA`** — habilitación de **`btnEditarTulaLot`**. Cuatro causas de
deshabilitación:

```powerfx
If(
    IsBlank(ctxTulaSelLot.'CH-Tbl-Bags')
    || ctxTulaSelLot.ReceptionID.'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'
    || ctxTulaSelLot.BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'
    || galPlanillasLot.Selected.BatchStatus = 159460000,
    DisplayMode.Disabled,
    DisplayMode.Edit
)
```

**`G-TULA-ULTIMA`** — habilitación de **`btnEliminarTulaLot`** y
**`btnRemapearTulaLot`**: es `G-TULA` **más un quinto término** (**DL-22**):

```powerfx
If(
    IsBlank(ctxTulaSelLot.'CH-Tbl-Bags')
    || ctxTulaSelLot.ReceptionID.'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'
    || ctxTulaSelLot.BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'
    || galPlanillasLot.Selected.BatchStatus = 159460000
    || CountRows(galTulasLot.AllItems) <= 1,          // 🧨 DL-22: la última tula NO se toca
    DisplayMode.Disabled,
    DisplayMode.Edit
)
```

> 🧨 **DL-22 — la última tula de una planilla es intocable desde Administración.**
> *Eliminar* la borra y *Remapear* la manda a otra planilla: las **dos** dejan la
> planilla con **cero tulas**. Y una planilla sin tulas es exactamente lo que la
> regla de validación *"Sin tulas asociadas"* de `fxProblemas` (`01` §3.1) impide
> crear en Captura: sin este término, **Administración fabricaría desde el otro lado
> el registro que Captura tiene prohibido guardar** — y el operador se lo encuentra
> al retomar el lote, sin saber quién lo dejó así.
>
> *Editar* **no** lleva el término: cambiar el número de una tula no vacía la
> planilla.
>
> **`CountRows(galTulasLot.AllItems)` es sobre la GALERÍA ya cargada**, no un
> `CountRows(Filter(ds,…))`: cero consultas, cero D-2. `AllItems` de una galería con
> `Items` delegable devuelve **las filas materializadas**; con una sola tula, `1`.
> *(Caso borde: con `Items` vacío `AllItems` da `0` y el gate deshabilita igual — que
> es lo correcto, porque tampoco hay tula seleccionada.)*
>
> **La salida es Captura**: para vaciar una planilla se borra **la planilla entera**
> desde `scrCaptura` (§7.3 de `02`), que borra sus tulas junto con ella. Es la misma
> lógica de **DL-20**: cada operación tiene una sola pantalla donde vive.

> Los CUATRO términos, en orden:
> 1. **No hay tula seleccionada** (§11.6).
> 2. 🧨 **CENTINELA**: la tula seleccionada no pertenece a la planilla que hoy
>    está en el detalle. La galería maestra puede cambiar su `Selected` **sin
>    disparar su `OnSelect`** (auto-selección al recargar `Items`, cambio de
>    filtro, fila que desaparece tras un `Refresh`). Sin este término, el usuario
>    operaría una tula de OTRA planilla creyendo que opera la de la pantalla.
>    *(Delegación: `ctxTulaSelLot.ReceptionID.'CH-Tbl-Receptions'` es la **PK del
>    lookup**, que Dataverse devuelve como anotación del propio registro ya traído —
>    clase **Q-11** de §15.1, cero requests extra. No es un dot-walk a una columna
>    del registro relacionado.)*
> 3. Una tula ya **Pendiente de revisión** no se vuelve a operar — es el `tulaOk`
>    del mockup (`tulaSel.st==='esclusa'||'boveda'`).
> 4. 🧨 **El lote está `En progreso` (159460000)**: esos lotes **no se operan desde
>    acá** — ver el bloque de abajo y **DL-20** (§15.4).
>
> Los tres gates dicen lo mismo en negado: el término 4 de `G-TULA` /
> `G-TULA-ULTIMA` y el término 2 de `G-PLAN` son, en la forma positiva,
> **`&& galPlanillasLot.Selected.BatchStatus <> 159460000`** — la condición de
> habilitación. Se escriben negados porque los tres `If` enumeran **causas de
> deshabilitación**, no de habilitación.
>
> **Los diálogos hacen `Reset` de sus entradas AL ABRIR, no al cerrar** (receta 01
> §7.2, regla 2 — mata N6, el remapeo silencioso).

**`TIP-PROG`**, **`TIP-TULA`** y **`TIP-ULTIMA`** — los `Tooltip` de los 5 comandos.
**Un tooltip que dice el motivo real** es lo único que salva a un botón gris de
parecer un bug:

```powerfx
// TIP-PROG  (btnAgregarTulaLot, btnEditarPlanillaLot)
If(
    galPlanillasLot.Selected.BatchStatus = 159460000,
    "Los lotes En progreso se editan en Captura",
    ""
)

// TIP-TULA  (btnEditarTulaLot)
If(
    galPlanillasLot.Selected.BatchStatus = 159460000,
    "Los lotes En progreso se editan en Captura",
    "Seleccioná una tula"
)

// TIP-ULTIMA  (btnRemapearTulaLot, btnEliminarTulaLot) — DL-22
If(
    galPlanillasLot.Selected.BatchStatus = 159460000,
    "Los lotes En progreso se editan en Captura",
    CountRows(galTulasLot.AllItems) <= 1,
    "Es la última tula de la planilla: para vaciarla, eliminá la planilla desde Captura",
    "Seleccioná una tula"
)
```

> **El orden de las ramas del `Switch`/`If` importa**: el motivo *"lote En
> progreso"* gana sobre *"última tula"*, porque es el que bloquea **los cinco**
> comandos y el que el operador tiene que resolver primero.

> 🧨 **Por qué los lotes `En progreso` NO se operan desde acá** (decisión, **DL-20**):
> un lote `En progreso` es un lote **en captura**. Sus planillas y sus tulas viven en
> `colPlanillas`/`colTulas` del otro lado, en memoria, sin guardar. Si esta pantalla
> escribe una tula de ese lote, la sesión de Captura queda con datos viejos y su
> próximo guardado **pisa el cambio sin avisar** — o lo duplica, si el re-keyado de la
> receta `01` §7.5 ya no encuentra la fila. El camino está definido y es único:
> `En progreso` ⇒ **Captura** (contrato `01` §4.3 y §5.4); `Recibido` / `En boveda` ⇒
> **Lotes**. El mockup permitía el camino directo desde acá; la v2 lo cierra.

### 11.4 Píldora por `BatchStatus` (espejo entero)

Es la **variante B de la receta 01 §7.4** (espejo entero), que nació acá y ya vive
allá. En la vista Planillas el estado del lote llega por el espejo **entero**
`BatchStatus` (columna `int`, no option set), así que el `Switch` compara contra los
números CRUDOS. Se usa en `pilFilaPlanLot` (con `ThisItem.BatchStatus`) y en
`pilDetPlanLot` (con `galPlanillasLot.Selected.BatchStatus` — **variante C** de la
receta: fuera de un template no existe `ThisItem`). Todo lo demás — `Radius` 12,
`Height` 22, `Width` `14 + 6.2 * Len(Self.Text)`, `BorderThickness`
`If(Self.Fill = ThemeLineSoft, 1, 0)`, `Hover`/`Pressed` = `Fill`, Size `7.88`,
`FontWeight` `Semibold` — sale **tal cual de la receta 01 §7.4**.

```powerfx
// Text
Switch(ThisItem.BatchStatus,
    159460000, "En progreso",
    159460001, "Recibido",
    159460003, "En bóveda",
    159460002, "Anulado",
    "—")

// Fill
Switch(ThisItem.BatchStatus,
    159460000, ThemeInfoSoft,
    159460001, ThemeOkSoft,
    159460003, ThemeLineSoft,
    159460002, ThemeDangerSoft,
    ThemeLineSoft)

// Color
Switch(ThisItem.BatchStatus,
    159460000, ThemeInfo,
    159460001, ThemeOk,
    159460003, ThemeInk3,
    159460002, ThemeDanger,
    ThemeInk3)
```

### 11.5 Mini-tabla: tulas de la planilla

| Control | Tipo | Propiedades |
|---|---|---|
| `btnHeadTulasLot` | Botón clásico (receta 01 §7.1) | X `707` · Y `shpLineaOpsPlanLot.Y + 1` · Width `640` · Height `30` · `Fill`/`Hover`/`Pressed` `ThemeSurface2` · `Radius` (las 4) `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Visible` **VIS-PLAN** |
| `lblHeadTulaNumLot` | Etiqueta clásica | X `722` · Y `btnHeadTulasLot.Y` · Width `130` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"N° TULA"` · `Visible` **VIS-PLAN** |
| `lblHeadTulaEstadoLot` | Etiqueta clásica | X `860` · Y `btnHeadTulasLot.Y` · Width `200` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"ESTADO"` · `Visible` **VIS-PLAN** |
| `lblHeadTulaFechaLot` | Etiqueta clásica | X `1068` · Y `btnHeadTulasLot.Y` · Width `128` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"CREADA"` · `Visible` **VIS-PLAN** |
| `lblHeadTulaAutorLot` | Etiqueta clásica | X `1204` · Y `btnHeadTulasLot.Y` · Width `128` · Height `30` · `ESTILO-HEADER-COL` · `Text` `"POR"` · `Visible` **VIS-PLAN** |
| `shpLineaHeadTulasLot` | Rectángulo clásico | X `706` · Y `btnHeadTulasLot.Y + btnHeadTulasLot.Height` · Width `642` · Height `1` · `Fill` `ThemeLine` · `Visible` **VIS-PLAN** |

**`galTulasLot`** (Galería vertical en blanco):

| Propiedad | Valor |
|---|---|
| X | `706` · Y `shpLineaHeadTulasLot.Y + 1` · Width `642` |
| Height | `btnCardDetLot.Y + btnCardDetLot.Height - Self.Y` |
| `TemplateSize` | **`36`** (mini-tabla, receta `01` §7.3 — es el mismo valor que `galPlanillasDelLoteLot`; con `34` la píldora de 22 quedaba con 6 px de aire arriba y abajo y la fila se leía apretada) · `TemplatePadding` `0` · `ShowScrollbar` `true` |
| `Visible` | **VIS-PLAN** |
| `Items` | `SortByColumns(Filter('CH-Tbl-Bags', ReceptionID.'CH-Tbl-Receptions' = galPlanillasLot.Selected.'CH-Tbl-Receptions'), "createdon", SortOrder.Ascending)` |
| `OnSelect` | `UpdateContext({ctxTulaSelLot: ThisItem})` |

> 🧨 **DELEGABLE, PK contra PK** sobre el lookup `ReceptionID` (regla D-1, `01`
> §2.3). `ReceptionID = galPlanillasLot.Selected` **NO COMPILA**.
> Con `SortByColumns("createdon", Ascending)` por la regla dura **D-9**, mismo
> criterio que la mini-tabla de planillas (§7.3): orden **estable** entre cargas y
> en el sentido en que el operador cargó las tulas. Sin él, tras cada
> `Refresh('CH-Tbl-Bags')` las filas podrían reordenarse solas.

Template (X del template = X del encabezado − 706):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaTulaLot` | Botón clásico (overlay de fila, receta 01 §7.3) | X `0` · Y `0` · Width `galTulasLot.Width` · Height `36` (= `TemplateSize`) · **al FONDO del z-order del template** · `AccessibleLabel` `"Seleccionar tula " & ThisItem.BagID` |
| `shpSelTulaLot` | Botón clásico (receta 01 §7.3) | X `0` · Y `0` · Width `galTulasLot.Width` · Height `36` · **`Fill` `If(ThisItem.'CH-Tbl-Bags' = ctxTulaSelLot.'CH-Tbl-Bags', ThemeAccentSoft, Color.Transparent)`** |
| `shpSelBarraTulaLot` | Rectángulo clásico | X `0` · Y `0` · Width `3` · Height `36` · `Fill` `ThemeAccent` · **`Visible` `ThisItem.'CH-Tbl-Bags' = ctxTulaSelLot.'CH-Tbl-Bags'`** |
| `lblCelTulaNumLot` | Etiqueta clásica | X `16` · Y `0` · Width `130` · Height `36` · `ESTILO-CELDA-FUERTE` · `Text` `ThisItem.BagID` · `OnSelect` `Select(Parent)` |
| `pilCelTulaLot` | Botón clásico (píldora, receta 01 §7.4) | X `154` · Y `7` (= `(36 - 22) / 2`, centrada en el template) · Height `22` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `Text`/`Fill`/`Color` = los tres `Switch` de **`BagStatus`** de la receta 01 §7.4, tal cual |
| `lblCelTulaFechaLot` | Etiqueta clásica | X `362` · Y `0` · Width `128` · Height `36` · `ESTILO-CELDA` · `Color` `ThemeInk2` · `Text` `Text(ThisItem.'Created On', FmtFechaCorta)` · `OnSelect` `Select(Parent)` |
| `lblCelTulaAutorLot` | Etiqueta clásica | X `498` · Y `0` · Width `128` · Height `36` · `ESTILO-CELDA` · `Color` `ThemeInk2` · `Text` `ThisItem.'Created By'.'Full Name'` · `OnSelect` `Select(Parent)` |
| `shpSepTulaLot` | Rectángulo clásico | X `0` · Y `35` (= `TemplateSize − 1`) · Width `galTulasLot.Width` · Height `1` · `Fill` `ThemeLineSoft` · `OnSelect` `Select(Parent)` |

### 11.6 🧨 Selección de tula: por qué NO se usa `galTulasLot.Selected`

La galería clásica **auto-selecciona la primera fila** apenas carga `Items`. Si
los tres botones de tula colgaran de `galTulasLot.Selected`, entrar a una
planilla dejaría **Eliminar habilitado sobre una tula que el usuario nunca
tocó** — un borrado destructivo a un clic de distancia por un default de
plataforma. Inaceptable.

**Patrón adoptado**: variable de selección **explícita** + overlay clicable
(receta 01 §7.3).

| | |
|---|---|
| Estado | `ctxTulaSelLot` — **registro tipado** de `CH-Tbl-Bags` |
| Semilla | `Defaults('CH-Tbl-Bags')` (registro TIPADO con GUID en blanco, 01 §3.2) — **nunca `Blank()` pelado**, que dejaría el tipo sin inferir |
| "No hay selección" | `IsBlank(ctxTulaSelLot.'CH-Tbl-Bags')` |
| Se escribe | `galTulasLot.OnSelect` → `UpdateContext({ctxTulaSelLot: ThisItem})` |
| Se pinta | `shpSelTulaLot.Fill` y `shpSelBarraTulaLot.Visible` comparan GUIDs, **no** `ThisItem.IsSelected` |

**Los SIETE puntos de reset** — todos hacen
`UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})`:

| # | Dónde | Por qué |
|---|---|---|
| R-1 | `scrLotes.OnVisible` (§1) | el estado persiste entre visitas |
| R-2 | `tabVistaLot.OnChange` (§3) | el mockup hace `S.selTula=null` al cambiar de tab |
| R-3 | `galPlanillasLot.OnSelect` (§10.1) | cambiar de planilla invalida la tula |
| R-4 | `galLotesLot.OnSelect` (§6.1) | por simetría: la vista Lotes no debe dejar residuo |
| R-5 | `inpBuscarLot.OnChange` y `drpEstadoLot.OnChange` (§3) | el filtro puede sacar la planilla de la lista |
| R-6 | `dtpDesdeLot.OnChange`, `dtpHastaLot.OnChange`, `icoLimpiarFechasLot.OnSelect` (§3) | ídem |
| R-7 | `icoRefrescarLot.OnSelect` (§4.1) y el final de **cada** operación de tula (§14.1–§14.4, con su `UpdateContext` **explícito** — ya no vienen "de regalo" con el `Select(icoRefrescarLot)`) | tras operar, el registro pudo cambiar de estado, de planilla o dejar de existir |

> **R-3 no alcanza solo**: la auto-selección de la maestra cambia `Selected`
> **sin** disparar `OnSelect`. Por eso el gate `G-TULA` (§11.3) lleva además el
> **centinela** que compara la planilla de la tula con la planilla del detalle.
> Reset + centinela: uno limpia, el otro bloquea.

### 11.7 Estados vacíos del detalle de planilla

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioTulasLot` | Etiqueta clásica | X `736` · Y `galTulasLot.Y + 40` · Width `582` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Esta planilla no tiene tulas."` · `Visible` **VIS-PLAN** `&& IsEmpty(galTulasLot.AllItems)` |
| `lblVacioDetPlanLot` | Etiqueta clásica | X `736` · Y `380` · Width `582` · Height `40` · `ESTILO-VACIO` · Wrap `true` · `Text` `"Sin planilla que mostrar: ninguna planilla coincide con el filtro."` · **`Visible` `tabVistaLot.Selected.Value = "Planillas" && IsBlank(galPlanillasLot.Selected.'CH-Tbl-Receptions')`** |

---

## 12. Los seis diálogos

Los seis siguen la **receta 01 §7.2**, que fija TODAS las propiedades del chrome
(contenedor overlay, botón de bloqueo, card, título, texto, ✕, Cancelar, OK). Acá
van **solo** el nombre final, el tamaño de la card y lo específico de cada uno.

**Reglas no negociables que la receta ya impone y que acá se aplican sin
excepción**:

1. **La ✕ SIEMPRE cancela**: `icoDlgX…Lot.OnSelect` es **literalmente** el mismo
   `OnSelect` que `btnDlgCancel…Lot` — cerrar y no commitear nada (mata N4).
2. **Reset al ABRIR**, en el botón que abre (§7.2, §11.3) — nunca al cerrar
   (mata N6).
3. Los seis contenedores van al **TOPE del z-order** de la pantalla, en el orden
   del árbol.
4. `btnDlgBloqueo…Lot` (hijo, al FONDO del contenedor) con `OnSelect` `false`:
   traga los clics del fondo. Modalidad real.

### 12.1 Chrome — una fila por diálogo

**Los nombres van elididos en toda esta sección**: cada `…` de abajo es el nombre
del diálogo de la primera columna (`Boveda`, `Agregar`, `Eliminar`, `Remapear`,
`EditarTula`, `EditarPlanilla`), exactamente como los escribe el árbol de §0. Así,
la fila *Mover a bóveda* define `cntDlgBovedaLot` (el overlay, `Visible` =
`ctxDlgBovedaLot`), `btnDlgBloqueoBovedaLot` (su primer hijo, al FONDO),
`cntDlgCardBovedaLot` (la card, con la W × H de la tercera columna) y sus
`lblDlgTitBovedaLot` / `icoDlgXBovedaLot` / `btnDlgCancelBovedaLot` /
`btnDlgOkBovedaLot`. **Los 6 × 7 = 42 controles de chrome salen de esta tabla**;
las 6 tablas de §12.2 agregan solo el cuerpo de cada uno.

| Diálogo | `ctx` | Card (W × H) | Título (`lblDlgTit…Lot.Text`) | `btnDlgOk…Lot` |
|---|---|---|---|---|
| Mover a bóveda | `ctxDlgBovedaLot` | `380 × 214` | `"¿Mover el lote " & galLotesLot.Selected.Name & " a bóveda?"` | `ESTILO-BTN-PRIMARIO` · Text `"Mover a bóveda"` |
| Agregar tula | `ctxDlgAgregarLot` | `380 × 258` | `"Agregar tula"` | `ESTILO-BTN-PRIMARIO` · Text `If(galPlanillasLot.Selected.BatchStatus = 159460003, "Solicitar", "Agregar")` |
| Eliminar tula | `ctxDlgEliminarLot` | `380 × 286` | `"Eliminar tula"` | **`ESTILO-BTN-PELIGRO`** · Text `If(galPlanillasLot.Selected.BatchStatus = 159460003, "Solicitar", "Eliminar")` |
| Remapear tula | `ctxDlgRemapearLot` | `380 × 276` | `"Remapear tula " & ctxTulaSelLot.BagID` | `ESTILO-BTN-PRIMARIO` · Text `If(galPlanillasLot.Selected.BatchStatus = 159460003, "Solicitar", "Remapear")` |
| Editar tula | `ctxDlgEditarTulaLot` | `380 × 258` | `"Editar tula " & ctxTulaSelLot.BagID` | `ESTILO-BTN-PRIMARIO` · Text `If(galPlanillasLot.Selected.BatchStatus = 159460003, "Solicitar", "Actualizar")` |
| Editar planilla | `ctxDlgEditarPlanillaLot` | `380 × 276` | `"Editar N° de planilla"` | `ESTILO-BTN-PRIMARIO` · Text `"Solicitar"` (SIEMPRE) |

Coordenadas fijas dentro de cada card (receta 01 §7.2):
`lblDlgTit…Lot` X `20` · Y `16` · Width `Parent.Width - 66` · Height `22` (Height
`44` y Wrap `true` en **Mover a bóveda**, cuyo título ocupa dos líneas) ·
`icoDlgX…Lot` X `Parent.Width - 34` · Y `16` · 18 × 18 ·
`btnDlgCancel…Lot` X `20` · Y `Parent.Height - 50` · Width `100` · Height `32` ·
`btnDlgOk…Lot` X `240` · Y `Parent.Height - 50` · Width `120` · Height `32`.

### 12.2 Cuerpo de cada diálogo

Dos piezas se repiten y **ya son receta**: el **banner** info/warn (`btnAviso…` +
`icoAviso…` + `lblAviso…`) es la **receta 01 §7.2.a**, y el **resumen `kv`** de
*Eliminar tula* es la **receta 01 §7.2.b**. Acá van solo sus nombres finales,
coordenadas y textos.

> **`Height` de las entradas: `32`** en los tres inputs de diálogo — regla de `01`
> §6.1 ("dentro de una galería 28, fuera 32"): comparten fila y bloque con
> Cuadros combinados y Botones de 32. Por eso la etiqueta de error de cada uno
> arranca en `Y 132` (`98 + 32 + 2`).
>
> **Las etiquetas de campo van en *sentence case***, no en VERSALITA: el mockup las
> pinta en mayúsculas con `text-transform:uppercase`, que es **CSS** — una Etiqueta
> clásica no tiene esa propiedad, así que copiarlo obligaría a escribir el texto
> gritado en el `Text`. `ESTILO-CAMPO-LAB` ya diferencia la etiqueta (Size `8.25`,
> Semibold, `ThemeInk2`) sin gritar, y es lo que `02` ya hace en `lblCampoTransCap`
> ("Transportadora"). Dos guías, un solo criterio.

**Mover a bóveda**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtBovedaLot` | Etiqueta clásica | X `20` · Y `lblDlgTitBovedaLot.Y + lblDlgTitBovedaLot.Height + 4` · Width `340` · Height `66` · `ESTILO-DLG-TEXTO` · Wrap `true` · `Text` ver abajo |

```powerfx
CountRows(colTulasBovedaLot) &
" tula(s) en esclusa pasan a Almacenada en bóveda y el lote a En bóveda. " &
"Desde ahí, toda modificación requiere aprobación del supervisor."
```

**Agregar tula**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtAgregarLot` | Etiqueta clásica | X `20` · Y `42` · Width `340` · Height `32` · `ESTILO-DLG-TEXTO` · Wrap `true` · `Text` `"Planilla " & galPlanillasLot.Selected.ReceiptID & "  ·  Lote " & galPlanillasLot.Selected.ReceptionBatch.Name` |
| `lblCampoTulaAgregarLot` | Etiqueta clásica | X `20` · Y `82` · Width `340` · Height `14` · `ESTILO-CAMPO-LAB` · `Text` `"N° de tula"` |
| `inpTulaAgregarLot` | Entrada de texto clásica | X `20` · Y `98` · Width `340` · **Height `32`** (01 §6.1: fuera de galería, 32) · `HintText` `"Ej: 0470"` · **`DelayOutput` `true`** · `BorderColor` `If(lblErrTulaAgregarLot.Visible, ThemeWarnLine, ThemeLine)` |
| `lblErrTulaAgregarLot` | Etiqueta clásica | X `20` · Y `132` · Width `340` · Height `30` · `ESTILO-ERROR` · Wrap `true` · `Text` `"La tula " & Trim(inpTulaAgregarLot.Text) & " ya existe en este lote."` · `Visible` = **DUP-AGREGAR** |
| `btnAvisoAgregarLot` | Botón clásico (receta 01 §7.1) | X `20` · Y `lblErrTulaAgregarLot.Y + lblErrTulaAgregarLot.Height + 2` (= `164`) · Width `340` · Height `40` · `Fill`/`Hover`/`Pressed` `ThemeWarnSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · **`Visible` `galPlanillasLot.Selected.BatchStatus = 159460003`** |
| `icoAvisoAgregarLot` | Icono clásico `Icon.Lock` | X `btnAvisoAgregarLot.X + 12` (= `32`) · Y `btnAvisoAgregarLot.Y + 12` (= `176`) · 16 × 16 · `Color` `ThemeWarn` · `Visible` `btnAvisoAgregarLot.Visible` |
| `lblAvisoAgregarLot` | Etiqueta clásica | X `btnAvisoAgregarLot.X + 36` (= `56`) · Y `btnAvisoAgregarLot.Y` · Width `292` · Height `btnAvisoAgregarLot.Height` · Size `8.63` · `Color` `ThemeWarn` · Wrap `true` · `Text` `"Se enviará como solicitud de aprobación al supervisor."` · `Visible` `btnAvisoAgregarLot.Visible` |

**Eliminar tula** — resumen `kv` de 3 pares (receta `01` §7.2.b: etiqueta X `20` W `80`, valor X `108` W `252`, Height `18`). Las tres `Y` van **encadenadas**, nunca con literales: el primer par cuelga del párrafo y cada par siguiente del anterior con el paso de `22` que fija la receta (= `72`, `94`, `116`):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtEliminarLot` | Etiqueta clásica | X `20` · Y `42` · Width `340` · Height `18` · `ESTILO-DLG-TEXTO` · `Text` `"Revisá el detalle antes de confirmar."` |
| `lblKvTulaEtqEliminarLot` | Etiqueta clásica | Y `lblDlgTxtEliminarLot.Y + lblDlgTxtEliminarLot.Height + 12` · `ESTILO-META` · `Text` `"Tula"` |
| `lblKvTulaEliminarLot` | Etiqueta clásica | Y `lblKvTulaEtqEliminarLot.Y` · `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaSelLot.BagID` |
| `lblKvPlanEtqEliminarLot` | Etiqueta clásica | Y `lblKvTulaEtqEliminarLot.Y + 22` · `ESTILO-META` · `Text` `"Planilla"` |
| `lblKvPlanEliminarLot` | Etiqueta clásica | Y `lblKvPlanEtqEliminarLot.Y` · `ESTILO-CELDA-FUERTE` · `Text` `galPlanillasLot.Selected.ReceiptID & "  ·  " & galPlanillasLot.Selected.RemitentName` |
| `lblKvLoteEtqEliminarLot` | Etiqueta clásica | Y `lblKvPlanEtqEliminarLot.Y + 22` · `ESTILO-META` · `Text` `"Lote"` |
| `lblKvLoteEliminarLot` | Etiqueta clásica | Y `lblKvLoteEtqEliminarLot.Y` · `ESTILO-CELDA-FUERTE` · `Text` `galPlanillasLot.Selected.ReceptionBatch.Name` |
| `btnAvisoEliminarLot` | Botón clásico (receta 01 §7.1) | X `20` · Y `lblKvLoteEtqEliminarLot.Y + lblKvLoteEtqEliminarLot.Height + 14` (= `148`) · Width `340` · Height `40` · `Fill` `ThemeWarnSoft` · `Radius` `9` · `DisplayMode` `DisplayMode.View` · `Visible` `galPlanillasLot.Selected.BatchStatus = 159460003` |
| `icoAvisoEliminarLot` | Icono clásico `Icon.Lock` | X `btnAvisoEliminarLot.X + 12` (= `32`) · Y `btnAvisoEliminarLot.Y + 12` (= `160`) · 16 × 16 · `Color` `ThemeWarn` · `Visible` `btnAvisoEliminarLot.Visible` |
| `lblAvisoEliminarLot` | Etiqueta clásica | X `If(btnAvisoEliminarLot.Visible, 56, 20)` · Y `btnAvisoEliminarLot.Y` · Width `If(btnAvisoEliminarLot.Visible, 292, 328)` · Height `btnAvisoEliminarLot.Height` · Size `8.63` · Wrap `true` · `Color` `If(btnAvisoEliminarLot.Visible, ThemeWarn, ThemeInk2)` · **`Visible` `true`** · `Text` `If(btnAvisoEliminarLot.Visible, "Se enviará como solicitud de aprobación al supervisor.", "Esta acción no se puede deshacer.")` |

> 🧨 **`lblAvisoEliminarLot` tiene DOS papeles y por eso es el único `lblAviso…Lot`
> con `Visible true` fijo**, distinto de la receta `01` §7.2.a (donde la etiqueta
> referencia el `Visible` de su superficie):
>
> | Estado del lote | Papel | Qué se ve |
> |---|---|---|
> | `En boveda` (159460003) | **banner warn** de la receta 7.2.a | superficie ámbar + candado + texto *"Se enviará como solicitud…"* |
> | cualquier otro | **advertencia suelta** del diálogo destructivo | **sin** superficie y **sin** icono: solo el texto *"Esta acción no se puede deshacer."* en `ThemeInk2` |
>
> De ahí las **cuatro** propiedades condicionadas al mismo `btnAvisoEliminarLot.Visible`
> (`X`, `Width`, `Color`, `Text`): sin el banner no hay candado que esquivar, así que
> el texto **arranca en el margen del diálogo** (X `20`) y usa el ancho completo
> (`328` = `Parent.Width - 40 - 12`), en vez de quedar indentado en un hueco vacío.
> Un borrado **siempre** avisa que no se puede deshacer — por eso el texto nunca
> desaparece, solo cambia de traje.

**Remapear tula**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtRemapearLot` | Etiqueta clásica | X `20` · Y `42` · Width `340` · Height `18` · `ESTILO-DLG-TEXTO` · `Text` `"Mover a otra planilla del mismo lote."` |
| `lblCampoDestinoRemapearLot` | Etiqueta clásica | X `20` · Y `74` · Width `340` · Height `14` · `ESTILO-CAMPO-LAB` · `Text` `"Planilla destino"` |
| `cmbDestinoRemapearLot` | Cuadro combinado clásico | X `20` · Y `lblCampoDestinoRemapearLot.Y + lblCampoDestinoRemapearLot.Height + 2` (= `90`) · Width `340` · **Height `44`** — **excepción declarada** a los `32` de `01` §6.1: este combo tiene **DOS** `DisplayFields`, y el Cuadro combinado clásico los renderiza en **dos líneas** (DL-5); en `32` la segunda línea se corta a la mitad · `SelectMultiple` `false` · `IsSearchable` `true` · `DisplayFields` `["ReceiptID", "RemitentName"]` · `SearchFields` `["ReceiptID", "RemitentName"]` · `Items` ver abajo · `DefaultSelectedItems` ver abajo |
| `lblErrRemapearLot` | Etiqueta clásica | X `20` · Y `cmbDestinoRemapearLot.Y + cmbDestinoRemapearLot.Height + 4` (= `138`) · Width `340` · Height `28` · `ESTILO-ERROR` · Wrap `true` · `Text` `"Elegí una planilla destino: este lote no tiene otra."` · **`Visible` `IsBlank(cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions')`** — 🧨 **NO** `IsEmpty(cmb….Items)`: leer `.Items` desde otra fórmula no está soportado (**ESCOTILLA 1**, §12.3) |
| `btnAvisoRemapearLot` | Botón clásico (receta 01 §7.1) | X `20` · Y `lblErrRemapearLot.Y + lblErrRemapearLot.Height + 4` (= `170`) · Width `340` · Height `40` · lo demás igual que `btnAvisoAgregarLot` |
| `icoAvisoRemapearLot` | Icono clásico `Icon.Lock` | X `btnAvisoRemapearLot.X + 12` (= `32`) · Y `btnAvisoRemapearLot.Y + 12` (= `182`) · 16 × 16 · `Color` `ThemeWarn` · `Visible` `btnAvisoRemapearLot.Visible` |
| `lblAvisoRemapearLot` | Etiqueta clásica | X `btnAvisoRemapearLot.X + 36` (= `56`) · Y `btnAvisoRemapearLot.Y` (= `170`) · Width `292` · Height `btnAvisoRemapearLot.Height` · lo demás igual que `lblAvisoAgregarLot` |

**`cmbDestinoRemapearLot.Items`:**

```powerfx
Filter(
    Filter(
        'CH-Tbl-Receptions',
        ReceptionBatch.'CH-Tbl-ReceptionBatch' =
            galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
    ),
    'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'
)
```

**`cmbDestinoRemapearLot.DefaultSelectedItems`** — el diálogo abre con la primera
planilla destino **ya elegida**:

```powerfx
Table(
    First(
        Filter(
            Filter(
                'CH-Tbl-Receptions',
                ReceptionBatch.'CH-Tbl-ReceptionBatch' =
                    galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
            ),
            'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'
        )
    )
)
```

> 🧨 **Es la MISMA expresión del `Items`, envuelta en `Table(First(…))`** — el
> Cuadro combinado clásico espera una **tabla** de registros seleccionados, no un
> registro pelado. Si el `Items` cambia, esta cambia igual: son la misma consulta y
> el `First` corre sobre el resultado **ya acotado a las planillas de un lote**, no
> sobre la tabla.
>
> **Por qué preseleccionar**: el diálogo se abría **vacío y con el error rojo
> puesto** —"elegí una planilla destino"— sin que el usuario hubiera hecho nada
> mal. Un error inline es para **corregir**, no para **instruir**. Con la
> preselección, el único caso en que el error aparece es el que de verdad es un
> error: **el lote no tiene otra planilla** (`IsEmpty(…Items)`), y ahí el texto
> dice exactamente eso y el OK ya está gris. Es la misma decisión que `02` tomó al
> revés en *Nuevo lote* (**L-19**): allá preseleccionar la transportadora
> **residual** deja crear el lote equivocado con un clic; acá los ítems son **las
> planillas de ESTE lote**, calculadas al abrir — no hay residuo posible.
>
> **Delegación**: el `Filter` **interno** es la consulta real y es **DELEGABLE**
> (igualdad de registro sobre el lookup). El **externo** corre en el cliente
> sobre lo que devolvió el interno — un conjunto acotado a **las planillas de UN
> lote**, jamás a la tabla entera. Studio va a marcar un warning de delegación
> sobre el externo: es esperado y está acotado por construcción.
>
> 🧨 **El combo mide `44` de alto, no `32`** — es la **única excepción declarada** a
> la regla de `01` §6.1 ("dentro de galería 28, fuera 32"), y no es cosmética: los
> **dos** `DisplayFields` (`ReceiptID` + `RemitentName`, **DL-5**) se pintan en **dos
> líneas** y en `32` la segunda queda cortada por la mitad. Por eso la card de este
> diálogo pasó de `264` a **`276`** (§12.1): los `12` px que creció el combo se los
> come el alto de la card, y todo lo de abajo (`lblErrRemapearLot`, el banner) ya
> venía **encadenado**, así que baja solo — cero coordenadas que retocar a mano.
>
> 🧨 **Sin `AddColumns`, a propósito.** El registro que sale de este combo se
> asigna directo a un lookup (`ReceptionID: cmbDestinoRemapearLot.Selected`,
> §14.3). Un registro **ensanchado por `AddColumns`** tiene otra forma y la
> asignación al lookup se rompe. El display "planilla — empresa" se logra con los
> dos `DisplayFields`, que el combo clásico renderiza en dos líneas. Divergencia
> **DL-5**.

**Editar tula**

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtEditarTulaLot` | Etiqueta clásica | X `20` · Y `42` · Width `340` · Height `32` · `ESTILO-DLG-TEXTO` · Wrap `true` · `Text` `"Planilla " & galPlanillasLot.Selected.ReceiptID & "  ·  Lote " & galPlanillasLot.Selected.ReceptionBatch.Name` |
| `lblCampoTulaEditarLot` | Etiqueta clásica | X `20` · Y `82` · Width `340` · Height `14` · `ESTILO-CAMPO-LAB` · `Text` `"Nuevo número"` |
| `inpTulaEditarLot` | Entrada de texto clásica | X `20` · Y `98` · Width `340` · **Height `32`** · **`Default` `ctxTulaSelLot.BagID`** · `DelayOutput` `true` · `BorderColor` `If(lblErrTulaEditarLot.Visible, ThemeWarnLine, ThemeLine)` |
| `lblErrTulaEditarLot` | Etiqueta clásica | X `20` · Y `inpTulaEditarLot.Y + inpTulaEditarLot.Height + 2` (= `132`) · Width `340` · Height `30` · `ESTILO-ERROR` · Wrap `true` · `Text` `"La tula " & Trim(inpTulaEditarLot.Text) & " ya existe en este lote."` · `Visible` = **DUP-EDITAR-TULA** |
| `btnAvisoEditarTulaLot` | Botón clásico (receta 01 §7.1) | X `20` · Y `lblErrTulaEditarLot.Y + lblErrTulaEditarLot.Height + 2` (= `164`) · Width `340` · Height `40` · `Fill`/`Hover`/`Pressed` `ThemeWarnSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · **`Visible` `galPlanillasLot.Selected.BatchStatus = 159460003`** |
| `icoAvisoEditarTulaLot` | Icono clásico `Icon.Lock` | X `btnAvisoEditarTulaLot.X + 12` (= `32`) · Y `btnAvisoEditarTulaLot.Y + 12` (= `176`) · Width `16` · Height `16` · `Color` `ThemeWarn` · `Visible` `btnAvisoEditarTulaLot.Visible` |
| `lblAvisoEditarTulaLot` | Etiqueta clásica | X `btnAvisoEditarTulaLot.X + 36` (= `56`) · Y `btnAvisoEditarTulaLot.Y` (= `164`) · Width `292` · Height `btnAvisoEditarTulaLot.Height` (= `40`) · Size `8.63` · `Color` `ThemeWarn` · Wrap `true` · `Text` `"Se enviará como solicitud de aprobación al supervisor."` · `Visible` `btnAvisoEditarTulaLot.Visible` |

**Editar planilla** — el aviso es **INFORMATIVO y SIEMPRE visible**:

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDlgTxtEditarPlanillaLot` | Etiqueta clásica | X `20` · Y `42` · Width `340` · Height `32` · `ESTILO-DLG-TEXTO` · Wrap `true` · `Text` `"Planilla " & galPlanillasLot.Selected.ReceiptID & "  ·  Lote " & galPlanillasLot.Selected.ReceptionBatch.Name` |
| `lblCampoPlanillaEditarLot` | Etiqueta clásica | X `20` · Y `82` · Width `340` · Height `14` · `ESTILO-CAMPO-LAB` · `Text` `"Nuevo número"` |
| `inpPlanillaEditarLot` | Entrada de texto clásica | X `20` · Y `98` · Width `340` · **Height `32`** · **`Default` `galPlanillasLot.Selected.ReceiptID`** · `DelayOutput` `true` · `BorderColor` `If(lblErrPlanillaEditarLot.Visible, ThemeWarnLine, ThemeLine)` |
| `lblErrPlanillaEditarLot` | Etiqueta clásica | X `20` · Y `inpPlanillaEditarLot.Y + inpPlanillaEditarLot.Height + 2` (= `132`) · Width `340` · Height `30` · `ESTILO-ERROR` · Wrap `true` · `Text` `"La planilla " & Trim(inpPlanillaEditarLot.Text) & " ya existe en este lote."` · `Visible` = **DUP-EDITAR-PLANILLA** |
| `btnAvisoEditarPlanillaLot` | Botón clásico (receta 01 §7.1) | X `20` · Y `lblErrPlanillaEditarLot.Y + lblErrPlanillaEditarLot.Height + 2` (= `164`) · Width `340` · Height `58` · `Fill`/`Hover`/`Pressed` **`ThemeInfoSoft`** · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · **`Visible` `true`** |
| `icoAvisoEditarPlanillaLot` | Icono clásico `Icon.Information` | X `btnAvisoEditarPlanillaLot.X + 12` (= `32`) · Y `btnAvisoEditarPlanillaLot.Y + 12` (= `176`) · 16 × 16 · `Color` `ThemeInfo` |
| `lblAvisoEditarPlanillaLot` | Etiqueta clásica | X `btnAvisoEditarPlanillaLot.X + 36` (= `56`) · Y `btnAvisoEditarPlanillaLot.Y` · Width `292` · Height `btnAvisoEditarPlanillaLot.Height` · Size `8.63` · `Color` `ThemeInfo` · Wrap `true` · `Text` `"El cambio de número de planilla SIEMPRE requiere aprobación del supervisor, sin importar el estado del lote."` |

### 12.3 Dup-checks delegables y habilitación del botón OK

Los tres chequeos corren **antes** del alta/edición, contra Dataverse, sobre el
**lote entero** (no sobre la planilla): un número de tula duplicado en otra
planilla del mismo lote es igual de inválido. Con `DelayOutput true` en las
entradas, se disparan ~0,5 s después de la última tecla.

```powerfx
// DUP-AGREGAR  (Visible de lblErrTulaAgregarLot)
!IsBlank(Trim(inpTulaAgregarLot.Text))
&& !IsBlank(
       LookUp(
           'CH-Tbl-Bags',
           ReceptionBatchId.'CH-Tbl-ReceptionBatch' =
               galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
           && BagID = Trim(inpTulaAgregarLot.Text)
       )
   )

// DUP-EDITAR-TULA  (Visible de lblErrTulaEditarLot)
!IsBlank(Trim(inpTulaEditarLot.Text))
&& With(
       {
           d: LookUp(
                  'CH-Tbl-Bags',
                  ReceptionBatchId.'CH-Tbl-ReceptionBatch' =
                      galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
                  && BagID = Trim(inpTulaEditarLot.Text)
              )
       },
       !IsBlank(d) && d.'CH-Tbl-Bags' <> ctxTulaSelLot.'CH-Tbl-Bags'
   )

// DUP-EDITAR-PLANILLA  (Visible de lblErrPlanillaEditarLot)
!IsBlank(Trim(inpPlanillaEditarLot.Text))
&& With(
       {
           d: LookUp(
                  'CH-Tbl-Receptions',
                  ReceptionBatch.'CH-Tbl-ReceptionBatch' =
                      galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
                  && ReceiptID = Trim(inpPlanillaEditarLot.Text)
              )
       },
       !IsBlank(d) && d.'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'
   )
```

> 🧨 **PK contra PK en los tres** (regla D-1, `01` §2.3):
> `ReceptionBatchId = galPlanillasLot.Selected.ReceptionBatch` **NO COMPILA**. Los
> dos lados van con su `.'CH-Tbl-ReceptionBatch'` explícito.
>
> **Los tres `LookUp` son DELEGABLES**: dos igualdades (`eq` de GUID sobre el
> lookup + `eq` sobre un `nvarchar`). El de tulas usa el espejo **B-2**
> `ReceptionBatchId` — sin él habría que navegar `ReceptionID.ReceptionBatch`
> (D-1/D-7) y el dup-check volvería a truncar como en la v1 (00 §1.2, hallazgos
> C1/C2).
> El `With` es solo para **no repetir el `LookUp` dos veces** (una para el
> `IsBlank` y otra para comparar el GUID): una sola consulta, dos usos.
>
> ⚠️ **Este chequeo cubre el lote, no el entorno.** Un duplicado en OTRO lote lo
> rechaza el `GuardReceptionPlugin` y el mensaje llega por el `IfError` de §14 —
> feo, pero **no silencioso** (misma limitación honesta que 01 §7.9).

**`DisplayMode` de cada `btnDlgOk…Lot`** (receta 01 §7.2, regla 4 — el botón
arranca deshabilitado cuando la entrada es inválida):

```powerfx
// btnDlgOkBovedaLot
DisplayMode.Edit        // no hay entrada que validar

// btnDlgOkAgregarLot
If(IsBlank(Trim(inpTulaAgregarLot.Text)) || lblErrTulaAgregarLot.Visible,
   DisplayMode.Disabled, DisplayMode.Edit)

// btnDlgOkEliminarLot
If(IsBlank(ctxTulaSelLot.'CH-Tbl-Bags'), DisplayMode.Disabled, DisplayMode.Edit)

// btnDlgOkRemapearLot
If(
    IsBlank(cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions')
    || cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions' = galPlanillasLot.Selected.'CH-Tbl-Receptions',
    DisplayMode.Disabled,
    DisplayMode.Edit
)

// btnDlgOkEditarTulaLot
If(
    IsBlank(Trim(inpTulaEditarLot.Text))
    || Trim(inpTulaEditarLot.Text) = ctxTulaSelLot.BagID
    || lblErrTulaEditarLot.Visible,
    DisplayMode.Disabled,
    DisplayMode.Edit
)

// btnDlgOkEditarPlanillaLot
If(
    IsBlank(Trim(inpPlanillaEditarLot.Text))
    || Trim(inpPlanillaEditarLot.Text) = galPlanillasLot.Selected.ReceiptID
    || lblErrPlanillaEditarLot.Visible,
    DisplayMode.Disabled,
    DisplayMode.Edit
)
```

> Los gates referencian **`lblErr….Visible`**, no una copia del `LookUp`: la
> condición de duplicado se escribe **una sola vez** en la app (01 §0.9) y de
> paso se ahorra una segunda consulta idéntica por tecla.
> `btnDlgOkRemapearLot` no lee `lblErrRemapearLot.Visible` sino la selección del
> combo — ver la escotilla 1, que es **la forma primaria** de esta guía.

> 🪜 **ESCOTILLA 1 — leer `cmb….Items` desde OTRA fórmula NO ESTÁ SOPORTADO.**
> 🧨 **Esta escotilla es LA FORMA PRIMARIA, no un plan B.** Confirmado por el QA
> humano de la 1.31: `IsEmpty(cmbDestinoRemapearLot.Items)` queda **en rojo** —
> la propiedad `Items` de un control no es legible desde otra fórmula. Por eso, en
> **las dos** apariciones (`lblErrRemapearLot.Visible` y el gate de
> `btnDlgOkRemapearLot`), la condición se escribe:
> ```powerfx
> IsBlank(cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions')
> ```
> Es equivalente **por construcción**: el combo tiene `DefaultSelectedItems`
> preseleccionado (§12.2), así que "no hay selección" ⇔ "no había de dónde elegir".
> Lo que se pierde es el matiz del mensaje (el error inline significa "sin destino
> elegido" en vez de "el lote no tiene otra planilla"), y `btnDlgOkRemapearLot` queda
> con **dos** términos, no tres: el primero se volvió idéntico al segundo. El `Text`
> de `lblErrRemapearLot` pasa a **`"Elegí una planilla destino: este lote no tiene
> otra."`**, que es verdad en los dos casos.
>
> *(Si tu versión de Studio SÍ acepta leer `.Items` desde otra fórmula, podés volver
> al matiz de los dos mensajes — pero no es lo que esta guía manda construir.)*

### 12.4 🧨 Dup-check en DOS CAPAS: `DisplayMode` **y** guarda en `OnSelect`

Los dup-checks de *Agregar tula* y *Editar tula* viven en **dos lugares**, con la
**misma expresión**:

| Capa | Dónde | Qué hace |
|---|---|---|
| **1 — preventiva** | `DisplayMode` del `btnDlgOk…Lot` (§12.3) | el botón nace gris: el operador **no llega** a tocarlo |
| **2 — de guarda** | primera línea del `OnSelect` del mismo botón (§14.1, §14.4) | si el estado cambió **entre el render y el click**, `Notify` y **corta** |

**Por qué la capa 2 no es paranoia**: `DelayOutput true` hace que el dup-check se
evalúe ~0,5 s **después** de la última tecla, y el `LookUp` contra Dataverse tarda lo
suyo. Entre que la fórmula se resuelve y el usuario hace click hay una ventana real —
y ahí adentro **otro operador** puede haber creado esa misma tula. Un botón que ya
estaba habilitado cuando se dibujó **no se deshabilita solo** en medio del click.

Sin la capa 2, el resultado es una tula duplicada en el lote que el
`GuardReceptionPlugin` rechaza con un mensaje críptico… o que entra, si el guard no
cubre ese caso. Con la capa 2, el operador lee **el mismo mensaje** que ya tenía
delante.

```powerfx
// >>> GUARDA (capa 2) — primera línea del OnSelect de btnDlgOkAgregarLot <<<
If(
    !IsBlank(
        LookUp(
            'CH-Tbl-Bags',
            ReceptionBatchId.'CH-Tbl-ReceptionBatch' =
                galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
            && BagID = Trim(inpTulaAgregarLot.Text)
        )
    ),
    Notify(
        "La tula " & Trim(inpTulaAgregarLot.Text) &
        " ya existe en este lote. No se agregó nada.",
        NotificationType.Error
    ),

    <TODO el cuerpo de §14.1, sin cambiar una coma>
);
```

```powerfx
// >>> GUARDA (capa 2) — primera línea del OnSelect de btnDlgOkEditarTulaLot <<<
// Igual que la de arriba PERO con AUTO-EXCLUSIÓN: la tula que se está editando no
// es duplicado de sí misma. Sin el segundo término, renombrar 0470 a 0470 (o
// corregir solo un espacio) se rechazaría a sí mismo.
With(
    {
        d: LookUp(
               'CH-Tbl-Bags',
               ReceptionBatchId.'CH-Tbl-ReceptionBatch' =
                   galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'
               && BagID = Trim(inpTulaEditarLot.Text)
           )
    },
    If(
        !IsBlank(d) && d.'CH-Tbl-Bags' <> ctxTulaSelLot.'CH-Tbl-Bags',
        Notify(
            "La tula " & Trim(inpTulaEditarLot.Text) &
            " ya existe en este lote. No se cambió nada.",
            NotificationType.Error
        ),

        <TODO el cuerpo de §14.4, sin cambiar una coma>
    )
);
```

> **Reglas de la capa 2**:
>
> 1. **Es la MISMA expresión que el `Visible` de su `lblErr…Lot`** (§12.3), con la
>    misma auto-exclusión donde corresponde. Si tocás una, tocás las dos.
> 2. **`UpdateContext({ctxDlg…Lot: false})` va ANTES de la guarda**, como está hoy:
>    el diálogo se cierra igual y el `Notify` se lee sobre la pantalla. Dejarlo
>    abierto obligaría a un clic más para leer un mensaje que ya está arriba.
> 3. **Solo Agregar y Editar tula.** *Eliminar* y *Remapear* no crean números nuevos,
>    así que no tienen duplicado que chequear; *Editar planilla* no escribe nada —
>    solo crea una solicitud que el Supervisor valida de su lado.
> 4. **Cuesta UNA consulta delegable por click** (`eq` + `eq`, espejo B-2). Es el
>    precio de no meter un duplicado en el lote.

---

## 13. Bloques compartidos de las operaciones

Tres bloques que se **pegan tal cual** dentro de los `OnSelect` de §14, en el
orden que indica cada operación. Existen para que el escape (receta 01 §7.6) y el
template HTML (receta 01 §7.7) se escriban **una vez** y no cinco.

### 13.1 Bloque **E** — escapes comunes (receta 01 §7.6)

```powerfx
UpdateContext(
    {
        ctxELoteLot:
            Substitute(Substitute(Substitute(
                Trim(galPlanillasLot.Selected.ReceptionBatch.Name), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
        ctxETransportadoraLot:
            Substitute(Substitute(Substitute(
                Trim(galPlanillasLot.Selected.ReceptionBatch.Transportadora), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
        ctxEPlanillaLot:
            Substitute(Substitute(Substitute(
                Trim(galPlanillasLot.Selected.ReceiptID), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
        ctxEEmpresaLot:
            Substitute(Substitute(Substitute(
                Trim(galPlanillasLot.Selected.RemitentName), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
        ctxEUsuarioLot:
            Substitute(Substitute(Substitute(
                UsuarioNombre, "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
        ctxEEstadoLoteLot:
            Switch(
                galPlanillasLot.Selected.BatchStatus,
                159460000, "En progreso",
                159460001, "Recibido",
                159460003, "En bóveda",
                159460002, "Anulado",
                "—"
            )
    }
)
```

> **El `&` va SIEMPRE primero** (receta 01 §7.6, regla 1): escapando `<` antes,
> el `&` de `&lt;` se volvería `&amp;lt;` y el supervisor vería basura.
> Se escapa **todo** lo que venga de Dataverse o del usuario — la inyección HTML
> de la v1 estaba confirmada (00 §1.4).
> `ctxEEstadoLoteLot` es un literal de la app, no necesita escape.

### 13.2 Bloque **P** — `BagPreviousState` (01 §5.5)

**Se corre ANTES de tocar el `BagStatus` de la tula.** Solo lo usan *Eliminar* y
*Remapear* (son los dos únicos `RequestType` cuyo contrato lleva la clave).

```powerfx
UpdateContext(
    {
        ctxBagPrevioLot:
            Switch(
                ctxTulaSelLot.BagStatus,
                'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                            "Recibida en esclusa",
                'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                           "Almacenada en boveda",
                'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa', "Pendiente de revision por supervisor - esclusa",
                Text(ctxTulaSelLot.BagStatus)
            )
    }
);
UpdateContext(
    {
        ctxEBagPrevioLot:
            Substitute(Substitute(Substitute(ctxBagPrevioLot, "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
    }
)
```

> **Dos `UpdateContext` separados, no uno**: dentro de un mismo registro no
> podés leer una variable que estás definiendo en ese mismo registro.
> El texto va **sin tildes**, calcado de la etiqueta del option set: el Supervisor
> compara contra esos mismos strings para restaurar el estado si rechaza.

### 13.3 Bloque **H** — esqueleto HTML (receta 01 §7.7)

Se corre **después** de fijar `ctxETipoLot`, `ctxEncabezadoLot` y `ctxFilasLot`.

```powerfx
UpdateContext(
    {
        ctxHtmlLot:
            HtmlApEstilo &
            "<div class=""ap"">" &
              "<div class=""apHead"">" &
                "<span class=""tag"">" & ctxETipoLot & "</span>" &
                "<h3>Lote " & ctxELoteLot & "</h3>" &
                "<div class=""sub"">" & ctxETransportadoraLot & " · " & ctxEEstadoLoteLot & "</div>" &
              "</div>" &
              If(ctxEncabezadoLot = "simple", HtmlApEncabezadoTablaSimple, HtmlApEncabezadoTabla) &
                ctxFilasLot &
              "</table>" &
              "<div class=""foot"">Solicitud generada por CH-App-Reception · " &
                ctxEUsuarioLot & " · " & Text(Now(), FmtFecha) & "</div>" &
            "</div>"
    }
)
```

> `HtmlApEstilo`, `HtmlApEncabezadoTabla` y `HtmlApEncabezadoTablaSimple` viven en
> `App.Formulas` (01 §3.1) — literales **sin** `$"..."`, porque la interpolación
> obligaría a duplicar cada llave del CSS. Las comillas dobles de los atributos
> HTML se escriben **duplicadas** en Power Fx (`class=""ap""`).
>
> 🧨 **DOS encabezados, no uno** (receta 01 §7.7, calcado de `previewApproval(conActual)`
> del mockup). `ctxEncabezadoLot` lo elige y **cada operación de §14 lo setea junto
> con `ctxETipoLot`**, sin excepción:
>
> | Operación | `ctxEncabezadoLot` | Encabezado | Filas |
> |---|---|---|---|
> | Agregar tula (§14.1) | `"simple"` | `Campo / Valor` | **sin `colspan`** |
> | Eliminar tula (§14.2) | `"simple"` | `Campo / Valor` | **sin `colspan`** |
> | Remapear tula (§14.3) | `"comparativo"` | `Campo / Actual / Solicitado` | `colspan=""2""` en las que no comparan |
> | Editar tula (§14.4) | `"comparativo"` | `Campo / Actual / Solicitado` | ídem |
> | Editar planilla (§14.5) | `"comparativo"` | `Campo / Actual / Solicitado` | ídem |
>
> **Agregar y Eliminar no comparan nada**: no existe un "valor actual" de una tula
> que todavía no está o que se va. Pintarles el encabezado de 3 columnas deja una
> columna **"Solicitado" vacía en todas las filas** y obliga a un `colspan` que no
> tapa el hueco del `<th>`. Un `colspan=""2""` sobre la tabla simple hace lo
> inverso: fusiona una columna que no existe. **El encabezado y las filas se
> cambian juntos o no se cambian.**

---

## 14. Las cinco operaciones — `OnSelect` completos

**El doble camino se decide con UNA sola expresión**, la misma en las cuatro
operaciones de tula (`esAprobacion()` del mockup es `l.estado==='En boveda'`):

```powerfx
galPlanillasLot.Selected.BatchStatus = 159460003
```

**Sin rama muerta** (mata el bug N8 de la v1, 01 §5.4): el `BagStatus` destino se
escribe **explícito por camino**, nunca "el estado que tenía el lote". Y
`ApprovalStatus` **jamás** se setea: en blanco = Pendiente (01 §5.5).

> **Los cuatro toasts del camino directo terminan en `" (acción directa)."`** —
> "agregada", "eliminada", "remapeada", "actualizada". No es adorno: las mismas
> cuatro operaciones, con el lote **En bóveda**, tostean *"Solicitud «…» enviada al
> supervisor"* y **no cambian nada todavía**. El operador tiene que poder
> distinguir de un vistazo **"ya está hecho"** de **"queda esperando aprobación"**,
> y el estado del lote no siempre lo tiene presente. Es el mismo par de mensajes
> que el mockup emite. **El toast de *Mover a bóveda* (§8.2) queda como está** y
> **no** copia el "operación atómica" del mockup: acá son dos fases con `IfError`
> —tulas primero, lote después— y decir "atómica" sería mentir sobre lo que pasa si
> la segunda falla. Divergencia **DL-19**.

### 14.1 Agregar tula — `btnDlgOkAgregarLot.OnSelect`

🧨 **Este `OnSelect` arranca con la GUARDA de la capa 2 (§12.4)**: el bloque de abajo
va **dentro** de su rama `else`. Se muestra separado para no duplicarlo, pero en
Studio se pega uno adentro del otro.

```powerfx
UpdateContext({ctxDlgAgregarLot: false});

// >>> GUARDA capa 2 (§12.4) — envuelve TODO lo que sigue <<<

If(
    galPlanillasLot.Selected.BatchStatus = 159460003,

    // ================== CAMINO APROBACIÓN ==================
    // La tula se CREA ya en Pendiente: por eso el JSON puede llevar BagGUID.
    IfError(
        UpdateContext(
            {
                ctxTulaNuevaLot:
                    Patch(
                        'CH-Tbl-Bags',
                        Defaults('CH-Tbl-Bags'),
                        {
                            ReceptionID: galPlanillasLot.Selected,
                            BagID:       Trim(inpTulaAgregarLot.Text),
                            Name:        Trim(inpTulaAgregarLot.Text),
                            BagStatus:   'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'
                        }
                    )
            }
        ),
        Notify("No se pudo crear la tula: " & FirstError.Message, NotificationType.Error),

        // ---- ÉXITO: la tula existe. Ahora la solicitud. ----
        // >>> pegar BLOQUE E (§13.1) <<<
        UpdateContext(
            {
                ctxETipoLot:       Upper("Agregar tula"),
                ctxEncabezadoLot:  "simple",          // no hay "valor actual" que comparar
                ctxETulaLot:
                    Substitute(Substitute(Substitute(
                        Trim(inpTulaAgregarLot.Text), "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
            }
        );
        UpdateContext(
            {
                ctxFilasLot:
                    "<tr><td class=""k"">Planilla</td><td>" & ctxEPlanillaLot & " — " & ctxEEmpresaLot & "</td></tr>" &
                    "<tr><td class=""k"">Tula solicitada</td><td><b class=""new"">" & ctxETulaLot & "</b></td></tr>" &
                    "<tr><td class=""k"">Estado al aprobar</td><td>Almacenada en bóveda</td></tr>"
            }
        );
        // >>> pegar BLOQUE H (§13.3) <<<
        IfError(
            Patch(
                'CH-Tbl-ApprovalRequests',
                Defaults('CH-Tbl-ApprovalRequests'),
                {
                    RequestType:      "Agregar tula",
                    RequestDetails:   ctxHtmlLot,
                    RequestResources: JSON(
                        {
                            ReceiptGUID:        Text(galPlanillasLot.Selected.'CH-Tbl-Receptions'),
                            BagID:              Trim(inpTulaAgregarLot.Text),
                            BagGUID:            Text(ctxTulaNuevaLot.'CH-Tbl-Bags'),
                            ReceptionBatchGUID: Text(galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch')
                        }
                    )
                }
            ),
            Notify(
                "La tula quedó creada en Pendiente de revisión pero NO se pudo crear la solicitud: " &
                FirstError.Message & " Avisale al supervisor.",
                NotificationType.Error
            ),
            Notify("Solicitud «Agregar tula» enviada al supervisor.", NotificationType.Success)
        )
    ),

    // ================== CAMINO DIRECTO ('En progreso' / 'Recibido') ==================
    IfError(
        Patch(
            'CH-Tbl-Bags',
            Defaults('CH-Tbl-Bags'),
            {
                ReceptionID: galPlanillasLot.Selected,
                BagID:       Trim(inpTulaAgregarLot.Text),
                Name:        Trim(inpTulaAgregarLot.Text),
                BagStatus:   'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'
            }
        ),
        Notify("No se pudo agregar la tula: " & FirstError.Message, NotificationType.Error),
        Notify("Tula " & Trim(inpTulaAgregarLot.Text) & " agregada (acción directa).", NotificationType.Success)
    )
);

// Recarga PUNTUAL: la única tabla que esta operación tocó. NO se llama a
// icoRefrescarLot — ese recarga TAMBIÉN lotes y planillas, y recargar la maestra
// le cambia el Selected a la galería: la planilla del detalle se mueve sola bajo
// los pies del usuario (§4.2 + §0.2). Ver DL-21 y la regla D-5 de 01 §2.3.
Refresh('CH-Tbl-Bags');
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

> **`Name` se escribe con el mismo valor que `BagID`** (01 §2.4, decisión
> declarada: la columna es requerida y no hay otro dato natural).
> **`RequestResources` con `JSON()` y las claves EXACTAS de 01 §5.5, en ese
> orden** — es contrato con CH-App-Supervisor. Jamás por concatenación.
> El mensaje de la rama fea es específico a propósito: la tula existe y está
> pendiente, pero **no hay solicitud** — el operador tiene que saberlo.

> 🪜 **ESCOTILLA 2 — si el `IfError` no captura el error A TRAVÉS del `UpdateContext`.**
> El camino de aprobación envuelve un `UpdateContext({ctxTulaNuevaLot: Patch(…)})`
> dentro de un `IfError`. La captura de errores **a través de** una función de
> comportamiento intermedia no está garantizada en todas las versiones: si probás
> con un `Patch` que falla y **no** ves el `Notify` de error (o peor: se crea la
> solicitud con `BagGUID` en blanco), partilo en dos y decidí con el GUID, que es
> el dato que de verdad importa:
> ```powerfx
> UpdateContext(
>     {
>         ctxTulaNuevaLot:
>             Patch(
>                 'CH-Tbl-Bags',
>                 Defaults('CH-Tbl-Bags'),
>                 {
>                     ReceptionID: galPlanillasLot.Selected,
>                     BagID:       Trim(inpTulaAgregarLot.Text),
>                     Name:        Trim(inpTulaAgregarLot.Text),
>                     BagStatus:   'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'
>                 }
>             )
>     }
> );
> If(
>     IsBlank(ctxTulaNuevaLot.'CH-Tbl-Bags'),
>     Notify("No se pudo crear la tula. No se creó la solicitud.", NotificationType.Error),
>     <TODO el resto de la rama de ÉXITO, sin cambiar una coma>
> )
> ```
> La regla que **no** se negocia en ninguna de las dos formas: **si la tula no
> existe, la solicitud NO se crea.** Un `ApprovalRequest` con `BagGUID` en blanco es
> basura que el supervisor no puede resolver.

### 14.2 Eliminar tula — `btnDlgOkEliminarLot.OnSelect`

```powerfx
UpdateContext({ctxDlgEliminarLot: false});

If(
    galPlanillasLot.Selected.BatchStatus = 159460003,

    // ================== CAMINO APROBACIÓN ==================
    // >>> pegar BLOQUE P (§13.2) — ANTES de tocar el BagStatus <<<
    // >>> pegar BLOQUE E (§13.1) <<<
    UpdateContext(
        {
            ctxETipoLot:       Upper("Eliminar tula"),
            ctxEncabezadoLot:  "simple",              // no hay "valor solicitado" que comparar
            ctxETulaLot:
                Substitute(Substitute(Substitute(
                    Trim(ctxTulaSelLot.BagID), "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
        }
    );
    UpdateContext(
        {
            ctxFilasLot:
                "<tr><td class=""k"">Planilla</td><td>" & ctxEPlanillaLot & " — " & ctxEEmpresaLot & "</td></tr>" &
                "<tr><td class=""k"">Tula a eliminar</td><td><b>" & ctxETulaLot & "</b></td></tr>" &
                "<tr><td class=""k"">Estado actual</td><td>" & ctxEBagPrevioLot & "</td></tr>"
        }
    );
    // >>> pegar BLOQUE H (§13.3) <<<
    IfError(
        Patch(
            'CH-Tbl-Bags',
            ctxTulaSelLot,
            { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa' }
        ),
        Notify(
            "No se pudo marcar la tula como pendiente (" & FirstError.Message &
            "). NO se creó la solicitud.",
            NotificationType.Error
        ),
        IfError(
            Patch(
                'CH-Tbl-ApprovalRequests',
                Defaults('CH-Tbl-ApprovalRequests'),
                {
                    RequestType:      "Eliminar tula",
                    RequestDetails:   ctxHtmlLot,
                    RequestResources: JSON(
                        {
                            GUID:             Text(ctxTulaSelLot.'CH-Tbl-Bags'),
                            BagPreviousState: ctxBagPrevioLot
                        }
                    )
                }
            ),
            Notify(
                "La tula quedó en Pendiente de revisión pero NO se creó la solicitud: " &
                FirstError.Message & " Avisale al supervisor.",
                NotificationType.Error
            ),
            Notify("Solicitud «Eliminar tula» enviada al supervisor.", NotificationType.Success)
        )
    ),

    // ================== CAMINO DIRECTO ==================
    IfError(
        Remove('CH-Tbl-Bags', ctxTulaSelLot),
        Notify("No se pudo eliminar la tula: " & FirstError.Message, NotificationType.Error),
        Notify("Tula " & ctxTulaSelLot.BagID & " eliminada (acción directa).", NotificationType.Success)
    )
);

// Recarga PUNTUAL de la única tabla tocada (ver §14.1): icoRefrescarLot recargaría
// además lotes y planillas, y eso le cambia el Selected a la galería maestra.
Refresh('CH-Tbl-Bags');
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

> **`Remove(ds, registro)`, nunca `RemoveIf(ds, predicado)`** (regla D-3): el
> `RemoveIf` de la v1 truncaba y dejaba borrados parciales.
> **El `Notify` lee `ctxTulaSelLot.BagID` DESPUÉS del `Remove`** y está bien:
> `ctxTulaSelLot` es un **snapshot local**, no una fila de la colección que
> acabamos de mutar. Ese es exactamente el orden que la v1 invertía (bug N2:
> leía `ThisItem.GUID` después de mutar la colección y dejaba huérfanos).
> **`BagPreviousState` viaja SIN escapar en el JSON y escapado en el HTML**: son
> dos consumidores distintos — `ctxBagPrevioLot` para `RequestResources`,
> `ctxEBagPrevioLot` para `RequestDetails`.

### 14.3 Remapear tula — `btnDlgOkRemapearLot.OnSelect`

```powerfx
UpdateContext({ctxDlgRemapearLot: false});

If(
    galPlanillasLot.Selected.BatchStatus = 159460003,

    // ================== CAMINO APROBACIÓN ==================
    // >>> pegar BLOQUE P (§13.2) <<<
    // >>> pegar BLOQUE E (§13.1) <<<
    UpdateContext(
        {
            ctxETipoLot:       Upper("Remapear tula"),
            ctxEncabezadoLot:  "comparativo",         // planilla actual → planilla destino
            ctxETulaLot:
                Substitute(Substitute(Substitute(
                    Trim(ctxTulaSelLot.BagID), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
            ctxEPlanillaDestinoLot:
                Substitute(Substitute(Substitute(
                    Trim(cmbDestinoRemapearLot.Selected.ReceiptID), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
            ctxEEmpresaDestinoLot:
                Substitute(Substitute(Substitute(
                    Trim(cmbDestinoRemapearLot.Selected.RemitentName), "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
        }
    );
    UpdateContext(
        {
            ctxFilasLot:
                "<tr><td class=""k"">Tula</td><td colspan=""2""><b>" & ctxETulaLot & "</b></td></tr>" &
                "<tr><td class=""k"">Planilla</td><td>" & ctxEPlanillaLot & " — " & ctxEEmpresaLot & "</td>" &
                    "<td><b class=""new"">" & ctxEPlanillaDestinoLot & " — " & ctxEEmpresaDestinoLot & "</b></td></tr>" &
                "<tr><td class=""k"">Estado actual</td><td colspan=""2"">" & ctxEBagPrevioLot & "</td></tr>"
        }
    );
    // >>> pegar BLOQUE H (§13.3) <<<
    IfError(
        Patch(
            'CH-Tbl-Bags',
            ctxTulaSelLot,
            { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa' }
        ),
        Notify(
            "No se pudo marcar la tula como pendiente (" & FirstError.Message &
            "). NO se creó la solicitud.",
            NotificationType.Error
        ),
        IfError(
            Patch(
                'CH-Tbl-ApprovalRequests',
                Defaults('CH-Tbl-ApprovalRequests'),
                {
                    RequestType:      "Remapear tula",
                    RequestDetails:   ctxHtmlLot,
                    RequestResources: JSON(
                        {
                            GUID:             Text(ctxTulaSelLot.'CH-Tbl-Bags'),
                            ReceiptID:        Trim(cmbDestinoRemapearLot.Selected.ReceiptID),
                            ReceiptGUID:      Text(cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions'),
                            BagPreviousState: ctxBagPrevioLot
                        }
                    )
                }
            ),
            Notify(
                "La tula quedó en Pendiente de revisión pero NO se creó la solicitud: " &
                FirstError.Message & " Avisale al supervisor.",
                NotificationType.Error
            ),
            Notify("Solicitud «Remapear tula» enviada al supervisor.", NotificationType.Success)
        )
    ),

    // ================== CAMINO DIRECTO ==================
    IfError(
        Patch('CH-Tbl-Bags', ctxTulaSelLot, { ReceptionID: cmbDestinoRemapearLot.Selected }),
        Notify("No se pudo remapear la tula: " & FirstError.Message, NotificationType.Error),
        Notify(
            "Tula " & ctxTulaSelLot.BagID & " remapeada a la planilla " &
            cmbDestinoRemapearLot.Selected.ReceiptID & " (acción directa).",
            NotificationType.Success
        )
    )
);

// Recarga PUNTUAL de la única tabla tocada (ver §14.1): icoRefrescarLot recargaría
// además lotes y planillas, y eso le cambia el Selected a la galería maestra.
Refresh('CH-Tbl-Bags');
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

> El camino directo **solo mueve el lookup `ReceptionID`**: el `BagStatus` NO se
> toca (la tula sigue donde estaba, solo cambia de planilla).
> `cmbDestinoRemapearLot.Selected` es un registro **pristino** de
> `CH-Tbl-Receptions` (§12.2): por eso se puede asignar directo al lookup.

### 14.4 Editar tula — `btnDlgOkEditarTulaLot.OnSelect`

🧨 **Arranca con la GUARDA de la capa 2 (§12.4)**, la variante **con
auto-exclusión**: el bloque de abajo va dentro de su rama `else`.

```powerfx
UpdateContext({ctxDlgEditarTulaLot: false});

// >>> GUARDA capa 2 CON AUTO-EXCLUSIÓN (§12.4) — envuelve TODO lo que sigue <<<

If(
    galPlanillasLot.Selected.BatchStatus = 159460003,

    // ================== CAMINO APROBACIÓN ==================
    // (SIN bloque P: el contrato de "Editar tula" no lleva BagPreviousState — 01 §5.5)
    // >>> pegar BLOQUE E (§13.1) <<<
    UpdateContext(
        {
            ctxETipoLot:       Upper("Editar tula"),
            ctxEncabezadoLot:  "comparativo",         // N° actual → N° nuevo
            ctxETulaLot:
                Substitute(Substitute(Substitute(
                    Trim(ctxTulaSelLot.BagID), "&", "&amp;"), "<", "&lt;"), ">", "&gt;"),
            ctxETulaNuevaLot:
                Substitute(Substitute(Substitute(
                    Trim(inpTulaEditarLot.Text), "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
        }
    );
    UpdateContext(
        {
            ctxFilasLot:
                "<tr><td class=""k"">Planilla</td><td colspan=""2"">" & ctxEPlanillaLot & " — " & ctxEEmpresaLot & "</td></tr>" &
                "<tr><td class=""k"">N° de tula</td><td>" & ctxETulaLot & "</td>" &
                    "<td><b class=""new"">" & ctxETulaNuevaLot & "</b></td></tr>" &
                "<tr><td class=""k"">Estado de la tula</td><td colspan=""2"">Pendiente de revisión por supervisor</td></tr>"
        }
    );
    // >>> pegar BLOQUE H (§13.3) <<<
    IfError(
        Patch(
            'CH-Tbl-Bags',
            ctxTulaSelLot,
            { BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa' }
        ),
        Notify(
            "No se pudo marcar la tula como pendiente (" & FirstError.Message &
            "). NO se creó la solicitud.",
            NotificationType.Error
        ),
        IfError(
            Patch(
                'CH-Tbl-ApprovalRequests',
                Defaults('CH-Tbl-ApprovalRequests'),
                {
                    RequestType:      "Editar tula",
                    RequestDetails:   ctxHtmlLot,
                    RequestResources: JSON(
                        {
                            ReceiptGUID:        Text(galPlanillasLot.Selected.'CH-Tbl-Receptions'),
                            BagID:              Trim(inpTulaEditarLot.Text),
                            ReceptionBatchGUID: Text(galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch'),
                            BagGUID:            Text(ctxTulaSelLot.'CH-Tbl-Bags')
                        }
                    )
                }
            ),
            Notify(
                "La tula quedó en Pendiente de revisión pero NO se creó la solicitud: " &
                FirstError.Message & " Avisale al supervisor.",
                NotificationType.Error
            ),
            Notify("Solicitud «Editar tula» enviada al supervisor.", NotificationType.Success)
        )
    ),

    // ================== CAMINO DIRECTO ==================
    IfError(
        Patch(
            'CH-Tbl-Bags',
            ctxTulaSelLot,
            { BagID: Trim(inpTulaEditarLot.Text), Name: Trim(inpTulaEditarLot.Text) }
        ),
        Notify("No se pudo actualizar la tula: " & FirstError.Message, NotificationType.Error),
        Notify("Tula actualizada a " & Trim(inpTulaEditarLot.Text) & " (acción directa).", NotificationType.Success)
    )
);

// Recarga PUNTUAL de la única tabla tocada (ver §14.1): icoRefrescarLot recargaría
// además lotes y planillas, y eso le cambia el Selected a la galería maestra.
Refresh('CH-Tbl-Bags');
UpdateContext({ctxTulaSelLot: Defaults('CH-Tbl-Bags')})
```

> 🧨 **`BagID` en el JSON de "Editar tula" es el NÚMERO NUEVO, no un GUID.** El
> GUID de la tula viaja en `BagGUID`. La app vieja del supervisor hacía
> `GUID(ParseJSON(...).BagID)` sobre este mismo contrato y reventaba: acá el
> productor respeta 01 §5.5 al pie de la letra.

### 14.5 Editar planilla — `btnDlgOkEditarPlanillaLot.OnSelect`

**SIEMPRE aprobación**, sin importar el estado del lote (01 §5.3 y §5.4).

```powerfx
UpdateContext({ctxDlgEditarPlanillaLot: false});

// >>> pegar BLOQUE E (§13.1) <<<
UpdateContext(
    {
        // PRESENTACIÓN, no contrato: literal propio, con ° y sin Upper().
        // El RequestType que viaja abajo NO se toca (ver la nota al pie).
        ctxETipoLot:          "MODIFICAR N° DE PLANILLA",
        ctxEncabezadoLot:     "comparativo",          // N° actual → N° nuevo
        ctxEPlanillaNuevaLot:
            Substitute(Substitute(Substitute(
                Trim(inpPlanillaEditarLot.Text), "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
    }
);
UpdateContext(
    {
        ctxFilasLot:
            "<tr><td class=""k"">Empresa</td><td colspan=""2"">" & ctxEEmpresaLot & "</td></tr>" &
            "<tr><td class=""k"">N° de planilla</td><td>" & ctxEPlanillaLot & "</td>" &
                "<td><b class=""new"">" & ctxEPlanillaNuevaLot & "</b></td></tr>"
    }
);
// >>> pegar BLOQUE H (§13.3) <<<
IfError(
    Patch(
        'CH-Tbl-ApprovalRequests',
        Defaults('CH-Tbl-ApprovalRequests'),
        {
            RequestType:      "Modificar numero de planilla",
            RequestDetails:   ctxHtmlLot,
            RequestResources: JSON(
                {
                    ReceiptGUID:  Text(galPlanillasLot.Selected.'CH-Tbl-Receptions'),
                    NewReceiptID: Trim(inpPlanillaEditarLot.Text)
                }
            )
        }
    ),
    Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error),
    Notify("Solicitud «Modificar número de planilla» enviada al supervisor.", NotificationType.Success)
)
```

> 🧨 **`RequestType` y lo que lee el humano son DOS cadenas distintas, y no se
> sincronizan.**
>
> | Dónde | Cadena | Por qué |
> |---|---|---|
> | `RequestType` del `Patch` | `"Modificar numero de planilla"` | **CONTRATO** con CH-App-Supervisor (01 §5.3). `RequestType` es **texto libre**: "arreglarle" la ortografía crea un **tipo fantasma** que ninguna app del otro lado matchea. **INTOCABLE**, tal cual, sin tilde y con "numero" |
> | `ctxETipoLot` (tag del HTML) | `"MODIFICAR N° DE PLANILLA"` | **PRESENTACIÓN**. Literal propio, **sin `Upper()`**: `Upper("Modificar numero de planilla")` arrastraría el "NUMERO" sin tilde al encabezado que ve el supervisor |
> | Toast | `"Solicitud «Modificar número de planilla» enviada al supervisor."` | **PRESENTACIÓN**. Con tilde, como se escribe en castellano |
>
> Las otras cuatro operaciones **sí** usan `Upper(<el RequestType>)` porque sus
> strings de contrato ya están bien escritos y coinciden con la presentación. Esta
> es la única donde el contrato tiene una falta de ortografía **congelada**, y la
> regla es: **el contrato no se corrige, se envuelve**.
>
> **Sin `Patch` a `CH-Tbl-Receptions` y sin recarga de ningún tipo** (ni el ⟳ ni el
> `Refresh('CH-Tbl-Bags')` puntual de §14.1–§14.4): esta
> operación **no cambia nada** en las listas — solo crea la solicitud. El número
> de planilla lo cambia el Supervisor al aprobar. Refrescar acá sería ruido.

### 14.6 Por qué no hay preview del HTML

El mockup abre `previewApproval()` ("Así la verá el supervisor") después de cada
solicitud. **No se construye** — ledger 00 §5, **L-5**: era didáctico del
prototipo. En producción el toast confirma el envío y el HTML lo ve el supervisor
en su visor. Ese diálogo habría costado ~10 controles más y una segunda copia del
armado del HTML.

---

## 15. Notas de la guía

### 15.1 Consultas de la pantalla y su veredicto de delegación

**Inventario completo**: toda expresión de esta guía que toca un datasource está
acá. Si construís algo que no figura, es un bug de la guía.

| # | Dónde | Consulta | Veredicto |
|---|---|---|---|
| Q-1 | `galLotesLot.Items` (§6.2) | `If(…, SortByColumns(Filter('CH-Tbl-ReceptionBatches', StartsWith×2 && fechas)), SortByColumns(Filter('CH-Tbl-ReceptionBatches', ReceptionStatus = … && StartsWith×2 && fechas)))` | ✅ **DELEGABLE** — dos cadenas completas, `If` en el nivel más externo. 🧨 **Sobre la TABLA, no sobre la vista**: `Filter('X (Views)'.'v', predicado)` no compila (`01` §2.5) |
| Q-2 | `galPlanillasLot.Items` (§10.2) | ídem sobre `CH-Tbl-Receptions` con `BatchStatus` **entero crudo** (D-6) | ✅ **DELEGABLE** |
| Q-3 | `galPlanillasDelLoteLot.Items` (§7.3) | `SortByColumns(Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = galLotesLot.Selected.'CH-Tbl-ReceptionBatch'), "createdon", Ascending)` | ✅ **DELEGABLE** — **PK contra PK** (D-1) + `SortByColumns` explícito por **D-9** |
| Q-4 | `galTulasLot.Items` (§11.5) | `SortByColumns(Filter('CH-Tbl-Bags', ReceptionID.'CH-Tbl-Receptions' = galPlanillasLot.Selected.'CH-Tbl-Receptions'), "createdon", Ascending)` | ✅ **DELEGABLE** — PK contra PK + `SortByColumns` explícito por **D-9** |
| Q-5 | `btnMoverBovedaLot.OnSelect` (§8.1) | `ClearCollect(colTulasBovedaLot, Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch' && BagStatus = …))` | ✅ **DELEGABLE** — espejo **B-2** (D-7), PK contra PK |
| Q-6 | `lblErrTulaAgregarLot.Visible` / `lblErrTulaEditarLot.Visible` (§12.3) **y las guardas de §12.4** | `LookUp('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch' && BagID = texto)` | ✅ **DELEGABLE** — `eq` + `eq`, espejo B-2. Corre **dos veces por operación**: una por render (capa 1) y una por click (capa 2, §12.4) |
| Q-7 | `lblErrPlanillaEditarLot.Visible` (§12.3) | `LookUp('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch' && ReceiptID = texto)` | ✅ **DELEGABLE** |
| Q-8 | `cmbDestinoRemapearLot.Items` (§12.2) | `Filter(Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch'), PK <> actual)` | ⚠️ **interno DELEGABLE**, externo client-side sobre las planillas de UN lote (warning esperado y acotado) |
| Q-8b | `cmbDestinoRemapearLot.DefaultSelectedItems` (§12.2) | `Table(First(<la MISMA expresión de Q-8>))` | ⚠️ **Mismo veredicto que Q-8** — es literalmente la misma consulta — **más** un `First()` que también corre **client-side**, sobre el resultado ya acotado a las planillas de UN lote. Es el **segundo** warning de delegación esperado de la pantalla, y el último |
| Q-9 | `btnEditarLoteLot.OnSelect` (§9) | `Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch')` + `Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = lote.'CH-Tbl-ReceptionBatch')` | ✅ **DELEGABLES** — **2 consultas para TODO el lote**, cero N+1. *(La proyección del otro lado suma un `LookUp` por planilla contra `CH-Tbl-ClientBranches` — `02` Q10, costo declarado de L-23.)* |
| Q-10 | `lblDetPlanSubLot.Text` (§11.1) y Bloque E (§13.1) | `galPlanillasLot.Selected.ReceptionBatch.Transportadora` | ✅ navegación sobre **UN** registro (1 request por selección, no por fila) |
| Q-11 | Templates de galería (§6.3, §10.3, §11.5) | `'Created By'.'Full Name'`, `ReceptionBatch.Name` | ✅ **nombres primarios** — llegan como anotación del lookup, sin request extra |
| Q-12 | `icoRefrescarLot.OnSelect` (§4.1) y el cierre de *Mover a bóveda* (§8.2) | `Refresh()` × 3 tablas | ✅ no es consulta filtrada — ver **DL-10** y la tabla de las TRES formas de `01` §2.3 |
| Q-12b | Cierre de §14.1-§14.4 | `Refresh('CH-Tbl-Bags')` — **solo la tabla afectada**, nunca las tres | ✅ misma clase que Q-12, con alcance mínimo: no toca las maestras, así que no les mueve el `Selected` |
| W-1 | §8.2 | `Patch('CH-Tbl-Bags', colTulasBovedaLot, ForAll(colTulasBovedaLot, {…}))` | escritura — **un solo `Patch` con tabla**, sin `ForAll` de `Patch` |
| W-2 | §14.1-§14.5 | `Patch`/`Remove` por registro, cada uno en su `IfError` | escrituras — cero `RemoveIf` (D-3) |

**Lo que NO hay en esta pantalla, y es a propósito**: ni un `CountRows(Filter(ds, …))`
(D-2 — los `CountRows` de §8 y §12.2 son sobre `colTulasBovedaLot`, colección
**local**, y el de **DL-22** en §11.3 es sobre `galTulasLot.AllItems`, o sea la
galería ya cargada), ni un `RemoveIf` (D-3), ni un `Filter`/`LookUp`/`Search` dentro
del template de una galería (D-4), ni un `Upper()`/`Lower()`/`Sum()` sobre un
datasource (D-8 — los `Upper()` de §14 son sobre **literales de la app**), ni un
dot-walk **a una columna de datos** dentro de un `Filter` (D-1/D-6/D-7).

> 🧨 **Lo que sí hay en TODOS los `Filter` por lote/planilla es el dot-walk a la
> PK del lookup** (`ReceptionBatch.'CH-Tbl-ReceptionBatch'`), y **eso no viola
> D-1**: la PK viaja como anotación del propio registro y el `$filter` la pliega a un
> `eq` de GUID. Es además **la única forma que compila** — la igualdad de registro
> pelada (`ReceptionBatch = lote`) queda en rojo en Studio (`01` §2.3, D-1 en
> detalle).

### 15.2 Accesibilidad

Regla de la pantalla: **todo Icono clásico y todo Botón clásico con `Text` `""`
lleva `AccessibleLabel`.**

| Control | `AccessibleLabel` |
|---|---|
| `imgTileLot` | `"CashHub Recepción"` |
| `icoBuscarLot` | `"Buscar"` |
| `icoLimpiarFechasLot` | `"Limpiar el rango de fechas"` |
| `icoRefrescarLot` | `"Actualizar las listas"` |
| `btnCardListaLot` / `btnCardDetLot` / `btnHeadListaLot` / `btnHeadPlanLoteLot` / `btnHeadTulasLot` | `""` — decorativas, `DisplayMode.View` |
| `btnChipUsuarioLot` | `"Usuario conectado"` |
| `btnFilaLotesLot` / `btnFilaPlanLot` / `btnFilaTulaLot` | ver §6.3 / §10.3 / §11.5 |
| `icoDlgX…Lot` (× 6) | `"Cerrar sin guardar"` |
| `btnDlgBloqueo…Lot` (× 6) | `""` — bloqueo, `OnSelect` `false` |
| `icoBannerBovedaLot`, `icoAviso…Lot` | `""` — el texto está en la etiqueta de al lado |
| `shpSel…Lot` × 3 (`shpSelLotesLot`, `shpSelPlanLot`, `shpSelTulaLot`), `shpSegNavLot`, `btnBannerBovedaLot`, `btnAviso…Lot` × 5 | `""` — **decorativos** (`DisplayMode.View`, sin acción) o **duplican la etiqueta vecina**: un lector de pantalla que los anuncie repite dos veces el mismo texto |

### 15.3 Dependencias de backend (con su fallback)

| Ítem | Dónde se usa | Fallback sin backend |
|---|---|---|
| **B-2** `ReceptionBatchId` en `CH-Tbl-Bags` (01 §2.2 — **existe en DEV**, falta en el metadata viejo del repo) | Q-5, Q-6, Q-9 | dot-walk `ReceptionID.ReceptionBatch = lote` — **NO delegable**, revive los truncamientos de la v1. **No es un fallback aceptable: si IntelliSense no lo trae, se re-agrega la tabla y punto** |
| **`BatchStatus`** espejo entero en `CH-Tbl-Receptions` (ya existe) | Q-2, banner de bóveda, `esAprobacion`, píldoras §11.4 | dot-walk `ReceptionBatch.ReceptionStatus` — no delegable |
| **B0-1** `sanic_createdbyname` — **NO EXISTE** | búsqueda por autor | omitida (ledger **L-2**) |
| **B0-2** `sanic_bagcount` — **NO EXISTE** | contadores de tulas por fila | omitidos (ledger **L-3**, divergencias DL-2/DL-3/DL-4) |
| **Custom API de cierre/movimiento atómico** — **NO EXISTE** | §8.2 | el bloque de dos fases con `IfError` que está escrito, con su mensaje de "quedó a medias" |
| Vista `'Reception app view'` (01 §2.5) | **ya NO se usa** | Q-1 va contra `'CH-Tbl-ReceptionBatches'` pelado + su `SortByColumns` explícito: la vista **no puede** ser el primer argumento de un `Filter` con predicado (`01` §2.5) y su orden nunca se heredaba (D-9). **No es un fallback: es lo que la guía manda** |

### 15.4 Divergencias declaradas vs mockup

Las heredadas del ledger de `00 §5` (**L-2** búsqueda sin autor, **L-3** sin
contador de tulas, **L-4** combos de esquina viva, **L-5** sin preview del HTML)
siguen vigentes y no se repiten acá. Estas son **nuevas** y salen de construir
esta pantalla:

| # | En el mockup | En la app | Motivo |
|---|---|---|---|
| DL-1 | Tab list dibujado como píldora con sombra (`.seg`) | **Lista de pestañas MODERNA** con el chrome de plataforma | 00 §2.2 la habilita explícitamente; su gestión de foco/teclado es nativa. El costo es que su aspecto no se calca — y el catálogo de 01 §0.3 hay que ampliarlo (hueco **H-1**) |
| DL-2 | Columna derecha de la fila de lote: `"3 planillas · 9 tulas"` | solo la píldora de estado | Extensión de **L-3**: sin `bagcount` (B0-2) **ni** un contador de planillas por lote, ambos números exigirían `CountRows(Filter(ds,…))` por fila = D-2 + N+1 |
| DL-3 | Mini-tabla del lote con columna **Tulas** (chip) | columnas `Planilla / Remitente / Total C$ / **Total US$**` | ídem; el ancho liberado se usa para el total en dólares, que el mockup escondía |
| DL-4 | Meta de la fila de planilla: `"C$ … · US$ … · 4 tulas"` | `"C$ … · US$ …"` | ídem |
| DL-5 | `<select>` de remapeo con `"00860 — Supermercados La Unión"` en una línea | Cuadro combinado con `DisplayFields ["ReceiptID","RemitentName"]` (dos líneas), **sin `AddColumns`** | El registro elegido se asigna directo al lookup `ReceptionID` (§14.3): un registro ensanchado por `AddColumns` tiene otra forma y rompe la asignación |
| DL-6 | Remapear con lote de una sola planilla ⇒ toast `"El lote no tiene otra planilla destino"` **antes** de abrir | el diálogo abre, el combo queda vacío, el OK deshabilitado y hay error inline | Pre-validar exigiría un `CountRows(Filter(ds,…))` (D-2). El costo es un clic, no un dato |
| DL-7 | El ⟳ tostea `"Listas actualizadas"` | sin toast | Las operaciones ya emiten su propio `Notify` y recargan después (§4.1): el toast se duplicaría en cada acción |
| DL-8 | Glifos dentro de los botones (`＋`, `⛁`, `🗑`, `🔒`) | sin glifos en botones; los que quedan son **Iconos clásicos** separados | Open Sans no tiene esos glifos ⇒ *tofu* (01 §6.1). Un icono no se puede meter dentro de un Botón clásico |
| DL-9 | `box-shadow` en cards, diálogos y segmento activo | solo borde `ThemeLine`; sombra únicamente en la card del modal (`DropShadow` del Contenedor) | El Botón clásico no expone sombra (receta 01 §7.1) |
| DL-10 | — | **el ⟳ ejecuta `Refresh(ds)` × 3** | ✅ **YA NO ES DIVERGENCIA — ACEPTADA y absorbida como regla en `01` §2.3 (D-5).** El pushback era correcto y quedó escrito arriba: `Refresh(ds)` sigue **PROHIBIDO** como paso automático post-operación (el vicio de la v1) y queda **PERMITIDO** únicamente como acción explícita del usuario en el ⟳ de esta pantalla. Evidencia que lo sostuvo: (1) D-5 apuntaba al *estado local después de un `Patch`* — esta guía nunca usa `Refresh` para eso; (2) atar las maestras a colecciones las clavaría en `DataRowLimit = 2000` y el plan de estrés (00 §4.5) siembra **>2000 lotes** ⇒ truncamiento silencioso, el bug que motivó el rediseño (00 §1.2); (3) la guía hermana aprobada `supervisor-rebuild/03 §4` ya lo hace así. Se lista igual en el ledger de `00` **como aceptada**, para que quede la trazabilidad |
| DL-11 | Fecha de la tula hardcodeada `"15 ago 09:05"` | `Text(ThisItem.'Created On', FmtFechaCorta)` real, 24 h | El mockup es de utilería |
| DL-12 | Banner con emoji `🔒` | `Icon.Lock` + etiqueta | DL-8 |
| DL-13 | Cabeceras de lista con esquina redondeada por `overflow:hidden` de la card | Botón clásico con `RadiusTopLeft/TopRight 12` y radios inferiores `0` | El Rectángulo no tiene `Radius` (01 §0.3) y sus esquinas vivas asomarían fuera de la card |
| DL-14 | Rango de fechas **decorativo** (`title="decorativo en el prototipo"`) | funcional, delegable, e **inclusivo del día "hasta" completo** (§3) | Un filtro de fechas que excluye el día de hoy es peor que no tenerlo |
| DL-15 | Chip de usuario: `"María Gómez · Oficial de esclusa"` | `UsuarioNombre` solamente | El rol/puesto no está disponible: la app va **sin conectores** (01 §1.5) y `User()` no lo expone |
| DL-16 | `.pill::before` (punto de 6 px dentro de la píldora) | sin punto | Ya declarado en la receta 01 §7.4; se repite porque esta pantalla tiene 4 píldoras |
| DL-17 | Un solo buscador que cruza las dos jerarquías: planilla/empresa desde *Lotes*, lote desde *Planillas* | **la búsqueda es por vista**: *Lotes* filtra por N° de lote y transportadora (§6.2); *Planillas*, por N° de planilla y remitente (§10.2). El `HintText` lo dice según el tab (§3) | Cruzar las jerarquías exige un `Filter` anidado por fila (**D-1 + D-4**): buscar una planilla desde *Lotes* es "traeme los lotes que TENGAN una planilla que…". Mitigación: el tab *Planillas* cubre esa búsqueda, a un clic |
| DL-18 | Estados vacíos del detalle: "Seleccioná un lote", "Seleccioná una planilla" | "Sin lote que mostrar…", "Sin planilla que mostrar…", "Esta planilla no tiene tulas.", "Este lote no tiene planillas." y, en el diálogo Eliminar, "Revisá el detalle antes de confirmar." | Los textos del mockup **nunca serían verdad**: la galería clásica auto-selecciona la primera fila (§0.2), así que "sin selección" solo ocurre cuando la lista vino **vacía** — y ahí el problema es el filtro, no la selección. Los dos textos nuevos cubren huecos que el mockup dejaba en blanco |
| DL-19 | El toast de *Mover a bóveda* dice que es una "operación atómica" | dice qué pasó de verdad: cuántas tulas pasaron y en qué quedó el lote (§8.2) | Emparentado con **L-1**: client-side **no hay atomicidad**. Son dos fases con su propio `IfError` (tulas → lote); si la segunda falla, las tulas ya se movieron. Prometer atomicidad sin la Custom API (B0-3) es mentirle al operador justo en la operación que menos se puede deshacer |
| DL-20 | Se podían **operar tulas de lotes `En progreso` desde Lotes** (camino directo) | **bloqueado**: `G-PLAN` y `G-TULA` (§11.3) suman `galPlanillasLot.Selected.BatchStatus <> 159460000` a la habilitación y los 5 comandos quedan grises con el `Tooltip` *"Los lotes En progreso se editan en Captura"* | Un lote `En progreso` está **en captura**: sus filas viven sin guardar en `colPlanillas`/`colTulas`. Escribirlas desde acá deja esa sesión con datos viejos y su guardado las pisa o las duplica (re-keyado de `01` §7.5). El camino es único: `En progreso` ⇒ Captura (contrato `01` §4.3 y §5.4); `Recibido` / `En boveda` ⇒ Lotes |
| DL-21 | — | tras **Mover a bóveda** con el filtro de estado en **Recibido**, el lote **sale de la lista** y la selección salta a otra fila: el detalle muestra un lote que el usuario no pidió | **No es un bug y no hay nada que arreglar**: el lote dejó de ser `Recibido`, así que el filtro lo excluye con razón, y la galería clásica auto-selecciona la primera fila que queda (§0.2). Se declara para que no se reporte como defecto y para que el checklist (§16) lo espere |
| **DL-22** | Se podía eliminar o remapear **la única tula** de una planilla | **bloqueado**: `btnEliminarTulaLot` y `btnRemapearTulaLot` usan **`G-TULA-ULTIMA`** (§11.3), que suma `\|\| CountRows(galTulasLot.AllItems) <= 1`, con `Tooltip` *"Es la última tula de la planilla: para vaciarla, eliminá la planilla desde Captura"* | **QA humano 16-ago**: las dos operaciones dejaban la planilla con **cero tulas**, que es exactamente el registro que la regla *"Sin tulas asociadas"* de `fxProblemas` (`01` §3.1) impide crear en Captura. Administración no puede fabricar por la puerta de atrás lo que Captura tiene prohibido guardar — el operador se lo encuentra al retomar el lote sin saber quién lo dejó así. Misma lógica que **DL-20**: cada operación tiene **una** pantalla donde vive. *Editar tula* no lleva el término (cambiar el número no vacía la planilla) |

### 15.5 Huecos de `01-app.md` detectados al escribir esta guía — **TODOS CERRADOS**

Los doce se corrigieron **en `01`**, que es la fuente de verdad. Esta tabla queda
como trazabilidad; la columna de la derecha dice **dónde vive hoy la respuesta**, no
una tarea pendiente.

| # | Hueco que había en `01` | Cómo quedó | Dónde vive ahora |
|---|---|---|---|
| **H-1** | §0.3: el catálogo CERRADO no incluía la Lista de pestañas moderna y llamaba al Selector de fecha "la ÚNICA excepción moderna", contra `00 §2.2` | El catálogo suma **TRES** modernos permitidos —Lista de pestañas, Selector de fecha y **Spinner**— y nada más | ✅ `01` §0.3 |
| **H-2** | §7.4 mapeaba solo el **option set** `ReceptionStatus`; faltaba la variante del espejo **entero** `BatchStatus` que consume la vista Planillas | §11.4 de esta guía subió a la receta como **variante B**, con los mismos colores | ✅ `01` receta 7.4, variante B |
| **H-3** | §7.4 escribía los `Switch` con `ThisItem`, que no existe fuera de un template | La sustitución `ThisItem` → `gal….Selected` / `ctxTulaSelLot` / `gvarLoteActivo` está enunciada y tabulada | ✅ `01` receta 7.4, variante C |
| **H-4** | §4 listaba `colLotes` / `colPlanillasLot` / `colTulasLot` y `ctxSegLot` — **ninguno existe** en esta pantalla | Esas cuatro filas se borraron; §4 lista ahora `colTulasBovedaLot`, `ctxTulaSelLot`, `ctxDesdeLot`/`ctxHastaLot`, los seis `ctxDlg…Lot`, los `ctxE…Lot` y `ctxResetFechasLot` | ✅ `01` §4 (bloque "Colecciones y `ctx` de `03`") |
| **H-5** | D-5 prohibía `Refresh(ds)` sin distinguir "parche de estado tras `Patch`" de "recarga voluntaria del usuario" | D-5 reescrita: **prohibido como paso automático post-operación**, **permitido** como acción explícita del usuario en el ⟳. La divergencia **DL-10** quedó ACEPTADA | ✅ `01` §2.3 (D-5 + tabla de contraste) |
| **H-6** | §7.2 no cubría el **banner** (info/warn) ni el **resumen `kv`** que usan los diálogos de acá | Son dos sub-bloques opcionales de la receta, con sus medidas | ✅ `01` recetas **7.2.a** (banner) y **7.2.b** (kv) |
| **H-7** | §6.1 no listaba `Icon.Information` | Verificado: **`Icon.Information` existe** en el set clásico (`Icon.Info` **NO** existe). El catálogo de iconos de §6.1 ahora es una tabla cerrada y lo incluye | ✅ `01` §6.1 |
| **H-8** | §7.9 regla 4 decía que "Mover a bóveda" pasa **todas** las tulas del lote | Pasan **SOLO** las que están en `'Recibida en esclusa'` (159460000) — es lo que da idempotencia y lo que ya hace §8.1 de esta guía | ✅ `01` receta 7.9, regla 4 (con el `Filter` completo) |
| **H-9** | No había contrato escrito de qué recibe `scrCaptura` en "Editar lote", y `Empresa` viajaba como referencia de `BranchName` | Hay **contrato canónico único** y la proyección es una **receta**: esta pantalla ya no proyecta, solo vacía las colecciones de edición. `Empresa` se rehidrata como registro puro en `02` | ✅ `01` §4.3 (contrato) + receta **7.10** (proyección) · §9 de esta guía reescrita |
| **H-10** | §2.5 no aclaraba si el orden de la vista sobrevive a un `Filter` encima | **No se garantiza**: es la nueva regla dura **D-9**, `SortByColumns` explícito en todo `Items` de galería maestra (lo que Q-1/Q-2 ya hacían) | ✅ `01` §2.3 (**D-9**) y §2.5 |
| **H-11** | §6.1 fijaba `Height 28` para la Entrada de texto clásica; los inputs de barra y de diálogo de acá necesitaban más | Regla: **28 dentro del template de una galería, 32 fuera** ("si comparte fila con un control de 32, va en 32"). Los inputs de diálogo de §12.2 pasaron de `30` a **`32`** | ✅ `01` §6.1 · §12.2 de esta guía |
| **H-12** | §7.7 decía que el encabezado de tabla del HTML de approval es **SIEMPRE de 3 columnas**; las variantes *Agregar* y *Eliminar* no comparan nada y quedaban con una columna "Solicitado" vacía y `colspan="2"` en todas sus filas | **DOS encabezados**: `HtmlApEncabezadoTablaSimple` (`Campo / Valor`) para Agregar/Eliminar y `HtmlApEncabezadoTabla` (`Campo / Actual / Solicitado`) para Remapear/Editar tula/Editar planilla, elegidos por `ctxEncabezadoLot` — calcado de `previewApproval(conActual)` del mockup | ✅ `01` §3.1 (las dos named formulas) + §7.7 (la regla) · §13.3 y §14 de esta guía |

---

## 16. Checklist de salida

Verificable en Studio, sin interpretación. **Prerrequisito que bloquea todo**:
el ítem de `01 §8` sobre `ReceptionBatchId`/`ReceiptIdMirror` en IntelliSense
tiene que estar tildado.

**Estructura y nombres**

- [ ] Los **177 controles** del árbol existen, con **el nombre exacto** y todos
      terminados en `Lot`; ninguno choca con los de `02` (`…Cap`).
- [ ] Los seis contenedores de diálogo son los **últimos** del árbol y, abiertos,
      tapan TODO (incluidas las galerías y el header).
- [ ] Los tres overlays de fila (`btnFilaLotesLot`, `btnFilaPlanLot`,
      `btnFilaTulaLot`) están **al FONDO** de su template: el texto de la fila se
      ve al pasar el mouse y **no** desaparece bajo el `HoverFill`.
- [ ] **Buscá con Ctrl+F en el árbol**: los controles de **§5–§11** que **NO** estén
      en la lista de **cuatro excepciones de §0.1** (`btnCardListaLot`,
      `btnCardDetLot`, `btnHeadListaLot`, `shpLineaHeadListaLot`) llevan la
      conjunción `tabVistaLot.Selected.Value = …` en su `Visible`. El header (§2),
      la barra (§3) y los seis diálogos (§12) quedan fuera de la regla.

**Comportamiento base**

- [ ] Entrar desde Captura deja el tab en **Lotes**, el buscador vacío, el estado
      en **Todos**, las dos fechas en blanco y la **primera fila seleccionada**
      con su detalle cargado.
- [ ] Volver a Captura y volver a Lotes **no** conserva el tab "Planillas" ni la
      selección anterior (los `Reset` del `OnVisible`).
- [ ] Escribir en el buscador filtra **sin warning de delegación** en el App
      Checker; borrarlo devuelve todas las filas (`StartsWith(col, "")` = true).
- [ ] Elegir **En bóveda** en el desplegable filtra en las **dos** vistas, y en
      Planillas lo hace por `BatchStatus = 159460003` (verificable en el monitor:
      la request lleva `$filter=…batchstatus eq 159460003`).
- [ ] Poner "desde" y "hasta" **en el mismo día de hoy** muestra los lotes
      creados hoy a cualquier hora (prueba del `DateAdd(+1)` — §3).
- [ ] El ⟳ trae los cambios hechos por otro usuario sin salir de la pantalla.

**Vista Lotes**

- [ ] Con >2000 lotes en el entorno (seeder STRESS-RCP), la lista scrollea sin
      cortarse en 500 ni en 2000 filas y **sin warning de delegación**.
- [ ] "Mover a bóveda" está habilitado **solo** en lotes `Recibido`; "Editar
      lote", **solo** en `En progreso`. En cualquier otro estado, los dos grises.
- [ ] El diálogo de bóveda dice el número **real** de tulas en esclusa del lote.
- [ ] Después de mover: las tulas quedan en `Almacenada en boveda` (159460001) y
      el lote en `En boveda` (159460003) — verificado **en Dataverse**, no en
      pantalla.
- [ ] **Prueba del camino feo**: simulá el rechazo del guard en el `Patch` de
      tulas ⇒ el lote **sigue en Recibido** y el mensaje lo dice. Volver a
      ejecutar "Mover a bóveda" no duplica nada (idempotencia).
- [ ] Tras *Mover a bóveda* con el **filtro de estado en `Recibido`**: el lote
      **sale de la lista** y el detalle pasa a **otro** lote. Verificá que el
      detalle sea **exactamente el lote que quedó seleccionado en la maestra** y
      no el que acabás de mover con datos viejos. El salto es **DL-21**, no un bug.
- [ ] "Editar lote" abre Captura con **todas** las planillas y **todas** las
      tulas del lote, y el monitor muestra **exactamente 2 requests** de carga
      (no una por planilla).

**Vista Planillas**

- [ ] Al entrar al tab, con una planilla auto-seleccionada, **Editar / Remapear /
      Eliminar están DESHABILITADOS**. Se habilitan recién al tocar una tula.
      *(Es la prueba de §11.6: sin ella, Eliminar quedaría armado sobre una tula
      que nadie eligió.)*
- [ ] Seleccionar una tula, cambiar de planilla y volver: los tres botones
      quedan deshabilitados (reset R-3 + centinela de `G-TULA`).
- [ ] Una tula en **Pendiente supervisor** no se puede operar.
- [ ] **PRUEBA DE DL-22**: parate en una planilla con **UNA sola tula** y seleccionala.
      **Editar** queda habilitado; **Remapear** y **Eliminar** quedan **grises**, y su
      tooltip dice *"Es la última tula de la planilla: para vaciarla, eliminá la
      planilla desde Captura"*. Agregá una segunda tula: los tres se habilitan.
- [ ] El banner ámbar aparece **solo** con lote en bóveda, y la fila de comandos
      y la tabla de tulas **bajan** 41 px sin superponerse (encadenado de §11.3).
- [ ] Con una **planilla de un lote `En progreso`** seleccionada, los **5 comandos**
      (*Agregar tula*, *Editar planilla*, *Editar*, *Remapear*, *Eliminar*) están
      **deshabilitados**, y al pasar el mouse el tooltip dice *"Los lotes En
      progreso se editan en Captura"* (**DL-20**).
- [ ] Tras **agregar una tula** por el camino directo: la **MISMA planilla sigue
      seleccionada** en la lista maestra (no salta a otra) y la tula nueva aparece
      en la mini-tabla. *(Es la prueba del `Refresh('CH-Tbl-Bags')` puntual de
      §14.1: con `Select(icoRefrescarLot)` la maestra recargaba y movía el
      `Selected`.)*

**Operaciones — camino directo (lote `Recibido`)**

- [ ] Agregar tula ⇒ nace en `Recibida en esclusa` (159460000) con `Name = BagID`.
- [ ] Editar tula ⇒ cambian `BagID` **y** `Name`; el `BagStatus` **no** se toca.
- [ ] Remapear ⇒ cambia `ReceptionID`; el `BagStatus` **no** se toca.
- [ ] Eliminar ⇒ pide confirmación **siempre** y el registro desaparece de
      Dataverse. **Cero `RemoveIf` en el código.**
- [ ] En los cuatro casos **no** se crea ningún `ApprovalRequest`.

**Operaciones — camino de aprobación (lote `En boveda`)**

- [ ] Las cuatro dejan la tula en `Pendiente de revision por supervisor - esclusa`
      (159460005) y crean **un** `CH-Tbl-ApprovalRequests` con
      `ApprovalStatus` **en blanco** y `Name` autonumerado (nunca patcheado).
- [ ] `RequestType` es **exactamente** uno de los 5 strings de 01 §5.3 — copiado,
      no tecleado.
- [ ] `RequestResources` parsea como JSON válido y trae **las claves exactas de
      01 §5.5, ninguna de más**. En "Agregar tula", `BagGUID` apunta a la tula
      recién creada; en "Editar tula", `BagID` es el **número nuevo** y `BagGUID`
      el GUID.
- [ ] `RequestDetails` abre en el visor del supervisor **con estilo** (la tabla
      Campo / Actual / Solicitado, el borde rojo del encabezado) y pesa < 10 000
      caracteres.
- [ ] **Prueba de inyección**: crear una tula llamada `<b>&x</b>` ⇒ en el visor
      del supervisor se lee **literal** `<b>&x</b>`, no en negrita. Si sale en
      negrita, falta el Bloque E en alguna rama.
- [ ] "Editar planilla" genera solicitud **también** con lote `En progreso` y
      `Recibido`, y **no** modifica `CH-Tbl-Receptions`.

**Validación y diálogos**

- [ ] Tipear un N° de tula que ya existe **en otra planilla del mismo lote**
      muestra el error inline y deshabilita el OK (dup-check por B-2, no por
      planilla).
- [ ] **PRUEBA DE LA CAPA 2 (§12.4)**: abrí *Agregar tula*, escribí un número
      libre, y **antes de tocar OK** creá esa misma tula desde otra sesión (o desde
      el maker portal). Tocá OK: sale *"La tula NNN ya existe en este lote. No se
      agregó nada."* y **no se crea nada**. Sin la guarda, el botón seguía habilitado
      y la tula entraba duplicada.
- [ ] **Auto-exclusión en Editar tula**: abrí *Editar* sobre una tula y tocá OK
      **sin cambiar el número**. El botón está gris (`Trim(…) = ctxTulaSelLot.BagID`);
      si lo forzás cambiando y volviendo, la guarda **no** lo trata como duplicado de
      sí misma.
- [ ] **Remapear**: `lblErrRemapearLot` y el gate del OK usan
      `IsBlank(cmbDestinoRemapearLot.Selected.'CH-Tbl-Receptions')`, **no**
      `IsEmpty(cmbDestinoRemapearLot.Items)` — que no compila (**ESCOTILLA 1**,
      §12.3). `rg -n 'cmbDestinoRemapearLot\.Items' Src/scrLotes.fx.yaml` da **cero**.
- [ ] Cerrar un diálogo con la **✕** no commitea nada; reabrirlo muestra el campo
      **reseteado** al valor actual, nunca el de la vez anterior (mata N6).
- [ ] Los seis diálogos: clic en el velo del fondo **no** cierra ni ejecuta nada.

**Formato y cierre**

- [ ] Los montos se ven `C$ 0.00` en cero, nunca `C$ .00`; los sellos de tiempo
      son de **24 h** (`HH`), verificado con un lote creado después de las 13:00.
- [ ] Las cuatro galerías muestran su estado vacío correcto y **ninguno** dice
      "seleccioná…" cuando hay filas.
- [ ] **App Checker en CERO errores.** Los warnings de delegación **esperados** son
      los **dos** de `cmbDestinoRemapearLot` — `Items` (**Q-8**) y
      `DefaultSelectedItems` (**Q-8b**) — **y ninguno más**. Cualquier otro es un
      bug: cruzalo contra el inventario cerrado de §15.1.
- [ ] Ningún `Set()` dentro de un `ForAll` en toda la pantalla (el único `ForAll`
      que queda es el de §8.2, y solo construye la tabla de cambios del `Patch`).
- [ ] **Cero igualdades de registro peladas**:
      `rg -n 'ReceptionBatch = |ReceptionBatchId = |ReceptionID = ' Src/scrLotes.fx.yaml`
      da **cero**. Toda comparación por lookup es **PK contra PK** (`01` §2.3, D-1) —
      la forma pelada **no compila** y `pac canvas pack` **no valida fórmulas**, así
      que viajaría publicada.
- [ ] **Cero vistas como primer argumento de `Filter`**:
      `rg -n "Filter\('CH-Tbl-\w+ \(Views\)'" Src/scrLotes.fx.yaml` da **cero**
      (`01` §2.5). `galLotesLot.Items` va contra `'CH-Tbl-ReceptionBatches'` pelado.
- [ ] **Prueba del contrato de "Editar lote" (`01` §4.3)**: editá un lote A, volvé a
      Lotes **sin guardar nada**, y editá un lote B distinto. Captura tiene que
      mostrar las planillas de **B**, no las de A. *(Es la prueba de los
      `Clear(colPlanillas); Clear(colTulas)` de §9: sin ellos la proyección de la
      receta `01` §7.10 no dispara y se ve el lote anterior con el chip del nuevo.)*
- [ ] Al entrar a Captura desde acá, el combo de empresa de cada fila **muestra la
      sucursal ya elegida** (`Empresa` llegó como registro puro, no como referencia
      de lookup) y guardar **no** dispara una consulta por fila.
