# Guía 04 — scrTulas (Localización de tulas)

Dónde está cada tula, en cualquier estado del circuito. **Ninguna queda invisible.**
Reemplaza a `Screen BagLocation` (la de los 4 tabs con `Table.pcfdataset` que dejaba
fuera de la vista los estados *Asignada a cajero* y *Pendiente de revisión —
recuento*, truncaba a 500 filas en TODOS los tabs, excluía el día elegido en "Fecha
hasta", listaba el directorio entero en el combo de creador, repetía el mismo
`NoDataText` ("No hay tulas en esclusa") en los 4 tabs y arrastraba 114 líneas de
diálogo de borrado en inglés que nadie podía disparar).

Acá: **una sola consulta delegable** con ventana temporal, colección local
`colTulasTula` con el segmento pre-calculado por fila, segmentado de 6 botones con
contadores reales, orden por dos columnas y estado vacío correcto por segmento.

**CONTRATO DE ENTRADA**

| Qué | Quién lo setea | Obligatorio |
|---|---|---|
| Nada de `gvar` ni colecciones de otras pantallas | — | La pantalla es **autosuficiente** |
| App creada con los datasources de `01` §2 — esta pantalla consulta **solo `CH-Tbl-Bags`**; los textos de `CH-Tbl-Receptions` y de `Users` llegan por **expansión de lookup**, sin datasource propio (se declara, `01` §2) | Guía `01` §2 | Sí |
| `App.Formulas` con la paleta, `FmtFechaCorta` y `UsuarioActual` | Guía `01` §3 | Sí |
| Componente `cmpNav` construido con **Acceso al ámbito de la aplicación** | Guía `01` §7.5 | Sí |
| **Configuración → General → Límite de filas de datos = `2000`** (default 500) | Constructor, UNA vez — **es un ajuste GLOBAL de la app**: pertenece a `01` §1 — **hoy NO está escrito ahí (pendiente de agregarlo)**, así que el constructor lo aplica desde acá o llega a esta pantalla con 500 y los contadores mintiendo | Sí — la ventana de 30 días de una BU chica ronda las 60–300 tulas, pero con 500 el contador de "Todas" empieza a mentir. Ver §7.2 |

> La nav llega por `Navigate(scrTulas)` desde `cmpNav` — sin parámetros.

**Variables/colecciones propias** (ninguna se comparte con `02`/`03`/`05`):
`ctxSegTula`, `ctxSortTula`, `ctxSortAscTula`, `ctxCargandoTula`, `ctxUltimaActTula`,
`ctxCreadorSelTula`, `colTulasTula`, `colCreadoresTula`.

## Árbol de controles

```
scrTulas
├─ cmpNavTula                    (instancia de cmpNav, 01 §7.5)
├─ cntBodyTula                   (Contenedor vertical, auto-layout)
│  ├─ cntTituloTula              (Contenedor vertical)
│  │  ├─ lblTituloTula
│  │  └─ lblSubtituloTula
│  ├─ cardFiltrosTula            (Contenedor manual, receta 01 §7.1)
│  │  ├─ lblFecDesdeTula
│  │  ├─ dtDesdeTula
│  │  ├─ lblFecHastaTula
│  │  ├─ dtHastaTula
│  │  ├─ lblBuscarTula
│  │  ├─ icoBuscarTula
│  │  ├─ inpBuscarTula
│  │  ├─ lblCreadoPorTula
│  │  ├─ drpCreadoPorTula
│  │  ├─ cirSyncTula
│  │  ├─ lblSyncTula
│  │  └─ btnActualizarTula
│  ├─ cntSegTula                 (Contenedor horizontal — segmentado, receta 01 §7.7)
│  │  ├─ segTodasTula
│  │  ├─ segEsclusaTula
│  │  ├─ segBovedaTula
│  │  ├─ segCajeroTula
│  │  ├─ segPendRecTula
│  │  └─ segProcesadasTula
│  └─ cardTablaTula              (Contenedor manual, receta 01 §7.1)
│     ├─ lblPanelTituloTula
│     ├─ pilCountTula
│     ├─ shpLineaPanelTula
│     ├─ shpBandHeadTula
│     ├─ btnSortTulaTula         (encabezado ordenable TULA)
│     ├─ lblColReciboTula
│     ├─ lblColClienteTula
│     ├─ lblColEstadoTula
│     ├─ btnSortCreadoTula       (encabezado ordenable CREADO)
│     ├─ lblColCreadoPorTula
│     ├─ shpLineaHeadTula
│     ├─ galTulasTula            (Galería vertical en blanco)
│     │  ├─ btnFilaTula          (overlay — al FONDO del z-order)
│     │  ├─ lblCelTulaTula
│     │  ├─ lblCelReciboTula
│     │  ├─ lblCelClienteTula
│     │  ├─ pilCelEstadoTula
│     │  ├─ lblCelCreadoTula
│     │  ├─ lblCelCreadoPorTula
│     │  └─ shpSepTula
│     ├─ icoVacioTula
│     ├─ lblVacioTituloTula
│     ├─ lblVacioTextoTula
│     └─ lblTopeTula
└─ cntCargandoTula               (Contenedor manual — overlay, receta 01 §7.6)
   └─ lblCargandoTula
```

> **Convención del árbol**: está en orden de construcción **back→front** (lo primero
> insertado queda al fondo del z-order). El panel de Studio lo lista **al revés** —
> el control frontal arriba. De esto dependen las tres anotaciones de z-order de esta
> guía: `shpBandHeadTula` al FONDO de los encabezados, `btnFilaTula` al FONDO del
> template y `cntCargandoTula` al frente de todo.

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | ver abajo — COMPLETO, listo para pegar |

```powerfx
UpdateContext({
    ctxSegTula: "esclusa",
    ctxSortTula: "creado",
    ctxSortAscTula: false
});
If(
    IsEmpty(colTulasTula),
    Select(btnActualizarTula)   // primera visita: carga sola
);
Reset(galTulasTula)
```

> El segmentado y la galería **persisten entre visitas** (00 §5): por eso el
> `UpdateContext` de reset va SIEMPRE, y el `Select()` de carga solo si la colección
> está vacía (volver de otra pantalla no vuelve a consultar; el usuario decide con
> Actualizar). El orden por defecto es **Creado ▼** (lo más nuevo arriba).

## 2. Layout responsive (01 §8)

| Control | Tipo | Propiedades |
|---|---|---|
| `cmpNavTula` | Componente de la app (`cmpNav`) | X `0` · Y `0` · Width `214` · Height `Parent.Height` |
| `cntBodyTula` | Contenedor vertical | X `cmpNavTula.Width` · Y `0` · Width `Parent.Width - cmpNavTula.Width` · Height `Parent.Height` · PaddingLeft/Right `26` · PaddingTop `22` · PaddingBottom `18` · LayoutGap `14` · Fill `Transparent` · **SIN scroll** (LayoutOverflowY: Ocultar) — los hijos se reparten el alto con `FillPortions`; scroll + FillPortions se contradicen (`02` §2) |

Hijos directos de `cntBodyTula` (auto-layout: **NO llevan X/Y**; el orden del árbol es
el orden vertical): `cntTituloTula`, `cardFiltrosTula`, `cntSegTula`, `cardTablaTula`.

## 3. Título

| Control | Tipo | Propiedades |
|---|---|---|
| `cntTituloTula` | Contenedor **vertical** | Height `52` · `LayoutMinHeight 52` · LayoutGap `3` · Fill `Transparent` — título ARRIBA, subtítulo DEBAJO (como el mockup) |
| `lblTituloTula` | Etiqueta clásica | ESTILO-TITULO · **Height `30`** · Text `"Localización de tulas"` · Width `480` — sin el `Height` explícito el default de 40 hace `40 + LayoutGap 3 + 18 = 61 > 52` y el contenedor recorta el subtítulo. Con 30: `30 + 3 + 18 = 51 ≤ 52` |
| `lblSubtituloTula` | Etiqueta clásica | ESTILO-SUBTITULO · Text `"Dónde está cada tula, en cualquier estado del circuito. Ninguna queda invisible."` · Width `720` · Height `18` (78 caracteres no entran en 480) |

## 4. Barra de filtros

`cardFiltrosTula` = **Contenedor manual** (receta 01 §7.1): Height `104` ·
`LayoutMinHeight 104` — los hijos van con X/Y. Dos filas: **arriba** los cuatro
filtros (Y `10`/`30`), **abajo** la franja de sincronía y Actualizar (Y `74`+).

| Control | Tipo | Propiedades |
|---|---|---|
| `lblFecDesdeTula` | Etiqueta clásica | X `16` · Y `10` · Width `120` · **Height `16`** · ESTILO-KPI-LABEL · Text `"FECHA DESDE"` |
| `dtDesdeTula` | Selector de fecha **moderno** (excepción 01 §0.3) | X `16` · Y `30` · Width `150` · Height `34` · **SelectedDate `DateAdd(Today(), -30, TimeUnit.Days)`** — el moderno **NO tiene `DefaultDate`**, esa propiedad es del clásico (verificado en `Screen OperationSummary.fx.yaml:109`) · OnChange `Select(btnActualizarTula)` |
| `lblFecHastaTula` | Etiqueta clásica | X `180` · Y `10` · Width `120` · **Height `16`** · ESTILO-KPI-LABEL · Text `"FECHA HASTA"` |
| `dtHastaTula` | Selector de fecha **moderno** | X `180` · Y `30` · Width `150` · Height `34` · **SelectedDate `Today()`** (ídem: nunca `DefaultDate`) · OnChange `Select(btnActualizarTula)` |
| `lblBuscarTula` | Etiqueta clásica | X `344` · Y `10` · Width `120` · **Height `16`** · ESTILO-KPI-LABEL · Text `"BUSCAR"` |
| `icoBuscarTula` | Icono | Icon `Icon.Search` · X `352` · Y `39` · 16×16 · Color `ThemeInk3` |
| `inpBuscarTula` | Entrada de texto clásica | X `344` · Y `30` · Width `cardFiltrosTula.Width - 344 - 236 - 12` · Height `34` · **DelayOutput `true`** · Default `""` · **HintText `"Número de tula…"`** (con B-3: `"Número de tula o de recibo…"` — §4.2) · Size `9.5` · BorderColor `ThemeLine` · Radius (las 4) `7` · Fill `ThemeSurface2` · PaddingLeft `26` · OnChange `Select(btnActualizarTula)` |
| `lblCreadoPorTula` | Etiqueta clásica | X `cardFiltrosTula.Width - 236` · Y `10` · Width `120` · **Height `16`** · ESTILO-KPI-LABEL · Text `"CREADO POR"` |
| `drpCreadoPorTula` | Menú desplegable clásico | X `cardFiltrosTula.Width - 236` · Y `30` · Width `220` · Height `34` · Items `colCreadoresTula` · Value `CreadorNombre` (**nombre de columna, SIN comillas**) · Default `Coalesce(LookUp(colCreadoresTula, CreadorGuid = ctxCreadorSelTula).CreadorNombre, "Cualquiera")` — re-ancla la selección después de reconstruir `Items` · OnChange: ver abajo |
| `cirSyncTula` | Círculo clásico | X `16` · Y `84` · 8×8 · Fill `ThemeAccent` · BorderThickness `0` |
| `lblSyncTula` | Etiqueta clásica | X `30` · Y `76` · Width `260` · Height `20` · Size `8.5` · Color `ThemeInk3` · Text `If(IsBlank(ctxUltimaActTula), "Sin actualizar", "Actualizado " & Text(ctxUltimaActTula, "HH:mm:ss"))` |
| `btnActualizarTula` | Botón clásico | X `cardFiltrosTula.Width - 140` · Y `74` · Width `124` · Height `26` · Radius (las 4) `6` · Fill `Transparent` · Color `ThemeAccent` · BorderColor `ThemeLine` · BorderThickness `1` · HoverFill `ThemeAccentSoft` · Text `"↻  Actualizar"` · Size `9` · FontWeight `Semibold` · OnSelect: §4.1 |

**`drpCreadoPorTula.OnChange` (COMPLETO):**

```powerfx
UpdateContext({ctxCreadorSelTula: drpCreadoPorTula.Selected.CreadorGuid});
Select(btnActualizarTula)
```

> **Gotcha del desplegable clásico**: cuando cambia su `Items`, el control **resetea
> `Selected`**. Por eso la selección se captura en `ctxCreadorSelTula` (un **GUID**,
> no un registro) *antes* de que la carga toque la colección, el `Filter` usa la
> variable —nunca `drp….Selected`— y el `Default` re-ancla el texto. Sin esto, elegir
> un creador dispara la carga, la carga reconstruye `Items`, `Selected` se vacía y el
> filtro se apaga solo en el mismo clic.

### 4.1 `btnActualizarTula.OnSelect` — COMPLETO, variante SIN backend (la que compila HOY)

> **Por qué esta variante y no la de B-3**: `sanic_receiptidmirror` **no existe todavía**
> (verificado en `sanic_CHTblBags/Entity.xml`). Un bloque marcado "listo para pegar"
> que le explota al constructor en IntelliSense no es una guía, es una trampa. El
> camino por defecto es el que funciona hoy; B-3 es el **upgrade** de una línea (§4.2).

```powerfx
UpdateContext({ctxCargandoTula: true});
IfError(
    // 1) LA consulta: una sola, delegable, con ventana temporal obligatoria.
    //    El AddColumns MATERIALIZA en la carga los cuatro textos de lookup y el
    //    segmento: la galería después no hace NI UN fetch por fila (fix del N+1),
    //    y el segmentado y los contadores trabajan 100% en local.
    ClearCollect(colTulasTula,
        AddColumns(
            Filter('CH-Tbl-Bags',
                'Created On' >= Coalesce(dtDesdeTula.SelectedDate, DateAdd(Today(), -30, TimeUnit.Days))
                && 'Created On' < DateAdd(Coalesce(dtHastaTula.SelectedDate, Today()), 1, TimeUnit.Days)
                && (IsBlank(ctxCreadorSelTula) || 'Created By'.User = ctxCreadorSelTula)
                && StartsWith(BagID, inpBuscarTula.Text)
                // CON B-3 en el entorno, reemplazar la línea de arriba por:
                //   && (StartsWith(BagID, inpBuscarTula.Text) || StartsWith(ReceiptIDMirror, inpBuscarTula.Text))
            ),
            SegTula,
            Switch(BagStatus,
                'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                            "esclusa",
                'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  "esclusa",
                'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            "boveda",
                'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               "cajero",
                'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento', "pendrec",
                'BagStatus (CH-Tbl-Bags)'.Recontada,                                         "procesadas",
                "otros"
            ),
            ReciboTula,      ReceptionID.ReceiptID,      // 1 salto, UNA vez por fila cargada
            ClienteTula,     ReceptionID.RemitentName,   // 1 salto (NO se dot-walkea a Client)
            CreadoPorTula,   'Created By'.'Full Name',   // 1 salto
            CreadorGuidTula, 'Created By'.User           // GUID: es lo que filtra, delegable
        )
    );

    // 2) Combo "Creado por" = los creadores REALES de la ventana cargada (fix del
    //    combo que listaba el directorio). Se reconstruye SOLO cuando no hay creador
    //    seleccionado: si hubiera uno, la colección tendría un único creador y el
    //    combo se quedaría sin las demás opciones. Cero consultas extra: los nombres
    //    y GUIDs ya vienen materializados del paso 1.
    //    Dedupe con GroupBy, NO con ForAll+LookUp: Power Fx no garantiza el orden de
    //    evaluación de ForAll, así que un ForAll que lee lo que escribió la iteración
    //    anterior puede dejar creadores DUPLICADOS en el combo, de forma intermitente.
    If(
        IsBlank(ctxCreadorSelTula),
        ClearCollect(colCreadoresTula,
            // GUID TIPADO: If(false, GUID(...)) siembra la columna con tipo.
            // Blank() pelado deja la columna sin tipo y el Collect siguiente falla.
            {CreadorNombre: "Cualquiera",
             CreadorGuid: If(false, GUID("00000000-0000-0000-0000-000000000000"))}
        );
        Collect(colCreadoresTula,
            ForAll(
                GroupBy(
                    Filter(colTulasTula, !IsBlank(CreadorGuidTula)),
                    "CreadoPorTula", "CreadorGuidTula", "FilasTula"
                ) As G,
                {CreadorNombre: G.CreadoPorTula, CreadorGuid: G.CreadorGuidTula}
            )
        )
    );

    UpdateContext({ctxUltimaActTula: Now()}),

    // rama de error del IfError
    Notify("No se pudieron cargar las tulas: " & FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxCargandoTula: false});
Reset(galTulasTula)
```

> - El apagado del overlay va **FUERA** del `IfError`: un fallo nunca deja la pantalla
>   bloqueada.
> - `If(cond, ClearCollect(...))` con el `If` **por fuera** — nunca
>   `ClearCollect(col, If(cond, ...))` (gotcha 01 §7.8).
> - **Filtro de creador por GUID, no por registro**: `'Created By'.User = ctxCreadorSelTula`
>   es el patrón `Lookup.'PK' = PK` que manda `00` §5 y usa `02` §4. Power Fx **no
>   compara registro con registro** (`'Created By' = drp….Selected` no compila), y el
>   `.User` del lookup contra una constante SÍ delega. Lo que nunca delegó en la
>   pantalla vieja fue `StartsWith(ReceptionID.ReceiptID, …)`, no este predicado.
> - **El `Filter` lee la variable, no el control**: `ctxCreadorSelTula` sobrevive a la
>   reconstrucción de `Items` del desplegable (gotcha de §4).
> - **Escape si App Checker marca delegación en el predicado de creador** (§4.3): sacar
>   el predicado del `Filter` y desdoblar con el `If` **POR FUERA** del `ClearCollect`
>   (gotcha `01` §7.8, nunca adentro del `Filter`):
>   `If(IsBlank(ctxCreadorSelTula), <ClearCollect sin el predicado>, <ClearCollect con
>   `&& 'Created By'.User = ctxCreadorSelTula`>)`. El `AddColumns` es idéntico en las
>   dos ramas: lo único que cambia es esa línea.
> - El costo del `AddColumns` con dot-walk es de **carga** (una vez por fila traída),
>   no de **render** (una vez por fila dibujada y otra vez en cada scroll). Es la
>   diferencia entre pagar 63 expansiones una vez y pagarlas cada vez que el usuario
>   mueve la rueda.

### 4.2 Búsqueda por recibo — dependencia de backend B-3 (00 §2)

| Escenario | `inpBuscarTula.HintText` | Predicado de búsqueda dentro del `Filter` | Delegación |
|---|---|---|---|
| **HOY, sin backend** — es el código de §4.1 (`Entity.xml` de Bags NO tiene la columna espejo, verificado) | `"Número de tula…"` | `StartsWith(BagID, inpBuscarTula.Text)` | **Delegable** ✅ |
| **CON B-3** (`sanic_receiptidmirror` en Bags, display `ReceiptIDMirror`) — upgrade de UNA línea sobre §4.1 | `"Número de tula o de recibo…"` (el copy exacto del mockup) | `StartsWith(BagID, inpBuscarTula.Text) \|\| StartsWith(ReceiptIDMirror, inpBuscarTula.Text)` | **Delegable** ✅ |
| Variante de emergencia si el negocio exige buscar por recibo antes de B-3 | `"Número de tula o de recibo (lento)…"` | `StartsWith(BagID, …) \|\| StartsWith(ReceptionID.ReceiptID, …)` | **NO DELEGABLE — temporal**: entidad relacionada en el `Filter` ⇒ trunca a 2000 y aparece el aviso azul de delegación. Solo con `lblTopeTula` visible (§7.2) y con el visto bueno explícito del negocio |

> **Gotcha que hace innecesario el `IsBlank`**: `StartsWith(col, "")` devuelve
> **true** para todas las filas. Por eso el predicado de búsqueda va sin el
> `IsBlank(inpBuscarTula.Text) || …` que usaba la pantalla vieja — menos ruido y una
> condición menos que el motor de delegación tenga que plegar.

### 4.3 Clasificación de delegación (fórmula por fórmula)

| Fórmula | Dónde | Veredicto |
|---|---|---|
| `'Created On' >= …` / `'Created On' < DateAdd(…, 1, TimeUnit.Days)` | §4.1 | **Delegable** — y corrige el bug BL-02: la vieja usaba `<= dpFechaHasta.SelectedDate` (medianoche) y **excluía el día elegido** |
| `IsBlank(ctxCreadorSelTula) \|\| 'Created By'.User = ctxCreadorSelTula` | §4.1 | **Delegable — a CONFIRMAR en App Checker.** El `'Created By'.User = <constante>` es el patrón `Lookup.'PK' = PK` de `00` §5 y delega (compara GUID, no registro contra registro). Lo que NO está verificado es el `IsBlank(<variable>) \|\|`: un operando booleano constante dentro de un `Or` puede no plegarse. **Si aparece la advertencia azul**, aplicar el escape del pie de §4.1. *(Ojo: el mismo predicado en `02` §4 corre sobre una colección local — allá la delegación ni se juega, así que NO sirve como evidencia acá.)* |
| `StartsWith(BagID, …)` | §4.1 | **Delegable** |
| `StartsWith(ReceiptIDMirror, …)` | §4.2 (con B-3) | **Delegable** — requiere B-3 |
| `StartsWith(ReceptionID.ReceiptID, …)` | §4.2 variante de emergencia | **NO delegable** (declarado, temporal) — **este** era el predicado que truncaba a 500 en la pantalla vieja |
| `AddColumns(Filter(…), SegTula, ReciboTula, ClienteTula, CreadoPorTula, CreadorGuidTula, …)` | §4.1 | El `Filter` interno **delega**; el `AddColumns` se aplica sobre la página ya traída (mismo patrón que `02` §4). Los cuatro dot-walk se pagan **en la carga**, una vez por fila traída |
| `Collect(colCreadoresTula, ForAll(GroupBy(Filter(colTulasTula, …), "CreadoPorTula", "CreadorGuidTula", "FilasTula"), …))` | §4.1 paso 2 | **Local** — cero red (nombre y GUID ya vienen materializados del paso 1) y **sin dependencia entre iteraciones**: `GroupBy` dedupe de una sola vez, el `ForAll` solo proyecta. Un `ForAll` que lee lo que escribió su propia iteración anterior no tiene orden garantizado ⇒ duplicados intermitentes |
| `CountRows(Filter(colTulasTula, SegTula = "…"))` | §5 y §6 | **Local** — la colección ya está en memoria. **Nunca** `CountRows` sobre `'CH-Tbl-Bags'`: eso no delega y devuelve un número mentiroso |
| `Filter(colTulasTula, …)` + `SortByColumns(…)` | §6.3 | **Local** |
| Celdas del template (`ThisItem.ReciboTula`, `.ClienteTula`, `.CreadoPorTula`, `.SegTula`) | §6.4 | **Local, cero red** — son columnas materializadas en la carga. **Ningún dot-walk en el template**: ese era el N+1 por fila renderizada (costo declarado en §9.3) |

## 5. Segmentado (receta 01 §7.7)

`cntSegTula` = **Contenedor horizontal**: Height `If(scrTulas.Width < 1100, 68, 34)` ·
`LayoutMinHeight` **la misma fórmula** · LayoutGap `0` · **LayoutWrap `true`** ·
Fill `Transparent`.

> Los 6 botones suman ≈ 804 px: a 1366 entran en una fila, a 1024 (tablet landscape,
> dentro de alcance) **envuelven**. El wrap real dispara en `Width < 1070`
> (`804 > Width − 266`), no en 1100: entre 1070 y 1100 el contenedor mide 68 con una
> sola fila (34 px de aire muerto). Se elige fallar del lado seguro —nunca cortar— y
> reusar el mismo 1100 del resto de la pantalla; si molesta el aire, usar `1070`. Y un contenedor de auto-layout **NO crece solo**
> al envolver — con `Height 34` fijo, la segunda fila queda cortada. De ahí el alto
> condicional (lección de `02` §6).

Los 6 botones son **Botones clásicos** y comparten estas propiedades (solo cambian
`Text`, `Fill`, `Color`, `OnSelect` y el radio de los extremos):

| Propiedad común | Valor |
|---|---|
| Height | `34` · `LayoutMinHeight 34` |
| Width | `22 + 7 * Len(Self.Text)` |
| **LayoutMinWidth** | `22 + 7 * Len(Self.Text)` — **obligatorio**: sin él `FillPortions`/`LayoutWrap` solo encoge y el wrap NUNCA dispara (`01` §8.4) |
| Size / FontWeight | `9.5` / `FontWeight.Semibold` |
| BorderColor / BorderThickness | `ThemeLine` / `1` |
| Radius (las 4) | `0` — **excepto** `segTodasTula` (TopLeft/BottomLeft `8`) y `segProcesadasTula` (TopRight/BottomRight `8`) |
| HoverFill | `If(ctxSegTula = <clave>, ThemeAccent, ThemeSurface)` — **`<clave>` = el literal de la columna "Clave" de la tabla de abajo** (`"todas"`, `"esclusa"`, `"boveda"`, `"cajero"`, `"pendrec"`, `"procesadas"`), el mismo que ya usan el `Fill` y el `Color` de ese botón. El activo no cambia al pasar el mouse |

| Control | Clave | Text | Fill | Color | OnSelect |
|---|---|---|---|---|---|
| `segTodasTula` | `"todas"` | `"Todas  " & CountRows(colTulasTula)` | `If(ctxSegTula = "todas", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "todas", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "todas"}); Reset(galTulasTula)` |
| `segEsclusaTula` | `"esclusa"` | `"En esclusa  " & CountRows(Filter(colTulasTula, SegTula = "esclusa"))` | `If(ctxSegTula = "esclusa", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "esclusa", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "esclusa"}); Reset(galTulasTula)` |
| `segBovedaTula` | `"boveda"` | `"En bóveda  " & CountRows(Filter(colTulasTula, SegTula = "boveda"))` | `If(ctxSegTula = "boveda", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "boveda", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "boveda"}); Reset(galTulasTula)` |
| `segCajeroTula` | `"cajero"` | `"Asignada a cajero  " & CountRows(Filter(colTulasTula, SegTula = "cajero"))` | `If(ctxSegTula = "cajero", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "cajero", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "cajero"}); Reset(galTulasTula)` |
| `segPendRecTula` | `"pendrec"` | `"Pend. revisión recuento  " & CountRows(Filter(colTulasTula, SegTula = "pendrec"))` | `If(ctxSegTula = "pendrec", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "pendrec", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "pendrec"}); Reset(galTulasTula)` |
| `segProcesadasTula` | `"procesadas"` | `"Procesadas  " & CountRows(Filter(colTulasTula, SegTula = "procesadas"))` | `If(ctxSegTula = "procesadas", ThemeAccent, ThemeSurface2)` | `If(ctxSegTula = "procesadas", ThemeAccentInk, ThemeInk2)` | `UpdateContext({ctxSegTula: "procesadas"}); Reset(galTulasTula)` |

**Mapa segmento → estados del optionset `sanic_bagstatus`** (los 6 valores reales,
verificados en `sanic_CHTblBags/Entity.xml`):

| Segmento | Valor(es) | Label del optionset |
|---|---|---|
| En esclusa | `159460000` + `159460005` | *Recibida en esclusa* · *Pendiente de revision por supervisor - esclusa* |
| En bóveda | `159460001` | *Almacenada en boveda* |
| Asignada a cajero | `159460002` | *Asignada a cajero* ← **invisible en la app vieja** |
| Pend. revisión recuento | `159460004` | *Pendiente de revision por supervisor - recuento* ← **invisible en la app vieja** |
| Procesadas | `159460003` | *Recontada* |
| Todas | los 6 + cualquiera nuevo (`"otros"`) | blindaje del ledger L-4 |

> **Los contadores salen de la colección, no del datasource** (decisión declarada):
> `CountRows(Filter('CH-Tbl-Bags', …))` **no delega** — devolvería como mucho el tope
> de filas y el número sería falso. Por eso el diseño carga UNA ventana temporal y
> cuenta en local. El precio es que los contadores son **de la ventana filtrada**, no
> del histórico completo: si el usuario pone "desde 01/08", "Todas 63" significa
> 63 tulas *en ese rango y con esos filtros*. Es lo que el usuario ve en la tabla —
> reconcilia por construcción. Cuando la ventana toca el tope, `lblTopeTula` lo grita
> (§7.2).

## 6. Tabla de tulas

`cardTablaTula` = **Contenedor manual** (receta 01 §7.1): `FillPortions 1`
(ocupa el alto restante) · `LayoutMinHeight 320` — los hijos van con X/Y.

### 6.1 Cabecera del panel

| Control | Tipo | Propiedades |
|---|---|---|
| `lblPanelTituloTula` | Etiqueta clásica | X `16` · Y `12` · Width `7 * Len(Self.Text) + 8` · **Height `22`** · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` · Text: ver abajo — cabeza del panel, **NO es ESTILO-SECCION**: el mockup la da en *sentence case* y tinta fuerte (`.panel-head h3`) |
| `pilCountTula` | **Botón clásico** (píldora, receta 01 §7.2) | X `lblPanelTituloTula.X + lblPanelTituloTula.Width + 10` · Y `10` · Fill `ThemeSunk` · Color `ThemeInk3` · **BorderThickness `0`** (override de la receta — el mockup no le pone borde al contador) · Width `12 + 7 * Len(Self.Text)` · Text `CountRows(Filter(colTulasTula, ctxSegTula = "todas" \|\| SegTula = ctxSegTula)) & " tulas"` |
| `shpLineaPanelTula` | Rectángulo clásico | X `0` · Y `40` · Width `cardTablaTula.Width` · Height `1` · Fill `ThemeLine` (línea bajo la cabeza del panel) |
| `shpBandHeadTula` | Rectángulo clásico | X `16` · Y `42` · Width `cardTablaTula.Width - 32` · Height `22` · Fill `ThemeSurface2` · **al FONDO del z-order de los encabezados** (banda gris del `thead`) · Visible `CountRows(Filter(colTulasTula, ctxSegTula = "todas" \|\| SegTula = ctxSegTula)) > 0` |

```powerfx
// lblPanelTituloTula.Text
Switch(ctxSegTula,
    "todas",      "Todas las tulas",
    "esclusa",    "En esclusa",
    "boveda",     "En bóveda",
    "cajero",     "Asignadas a cajero",
    "pendrec",    "Pendientes de revisión — recuento",
    "procesadas", "Procesadas",
    "Tulas"
)
```

### 6.2 Encabezados de columna

Y `44`, Height `18`; X/Width como **fracción del ancho de la galería** — nombrando la
galería, NUNCA `Self.Width` (sería circular). Los dos ordenables son **Botones
clásicos**; el resto, Etiquetas clásicas ESTILO-HEADER-COL.

| Control | Tipo | Text | X | Width | Align |
|---|---|---|---|---|---|
| `btnSortTulaTula` | Botón clásico | `"TULA " & If(ctxSortTula = "tula", If(ctxSortAscTula, "▲", "▼"), "")` | `16` | `galTulasTula.Width * 0.10` | Left |
| `lblColReciboTula` | Etiqueta clásica | `"RECIBO"` | `16 + galTulasTula.Width * 0.10` | `galTulasTula.Width * 0.12` | Left |
| `lblColClienteTula` | Etiqueta clásica | `"CLIENTE"` | `16 + galTulasTula.Width * 0.22` | `galTulasTula.Width * 0.21` | Left |
| `lblColEstadoTula` | Etiqueta clásica | `"ESTADO"` | `16 + galTulasTula.Width * 0.43` | `galTulasTula.Width * 0.24` | Left |
| `btnSortCreadoTula` | Botón clásico | `"CREADO " & If(ctxSortTula = "creado", If(ctxSortAscTula, "▲", "▼"), "")` | `16 + galTulasTula.Width * 0.67` | `galTulasTula.Width * 0.16` | Left |
| `lblColCreadoPorTula` | Etiqueta clásica | `"CREADO POR"` | `16 + galTulasTula.Width * 0.83` | `galTulasTula.Width * 0.17` | Left |

**Propiedades de los dos botones-encabezado** (para que se vean IGUAL que las
etiquetas ESTILO-HEADER-COL): Height `18` · Size `8` · FontWeight `Semibold` ·
Color `ThemeInk3` · Fill `Transparent` · HoverFill `Transparent` · PressedFill
`Transparent` · HoverColor `ThemeInk` · PressedColor `ThemeInk` · BorderThickness `0` ·
Align `Align.Left` · PaddingLeft/Right/Top/Bottom `0`.

| Control | OnSelect |
|---|---|
| `btnSortTulaTula` | `If(ctxSortTula = "tula", UpdateContext({ctxSortAscTula: !ctxSortAscTula}), UpdateContext({ctxSortTula: "tula", ctxSortAscTula: true})); Reset(galTulasTula)` |
| `btnSortCreadoTula` | `If(ctxSortTula = "creado", UpdateContext({ctxSortAscTula: !ctxSortAscTula}), UpdateContext({ctxSortTula: "creado", ctxSortAscTula: false})); Reset(galTulasTula)` |

`shpLineaHeadTula` (Rectángulo clásico): X `16` · Y `64` · Width
`cardTablaTula.Width - 32` · Height `1` · Fill `ThemeLine`.

**Visibilidad en vacío** (regla de `02` §7 — sin esto quedan encabezados huérfanos
flotando sobre el mensaje de "no hay tulas"). Los **6 encabezados**,
`shpLineaHeadTula` y `shpBandHeadTula` llevan:

```powerfx
CountRows(Filter(colTulasTula, ctxSegTula = "todas" || SegTula = ctxSegTula)) > 0
```

Y las **dos columnas de baja prioridad** combinan las dos condiciones con un AND
explícito escrito (`lblColClienteTula` y `lblColCreadoPorTula`):

```powerfx
CountRows(Filter(colTulasTula, ctxSegTula = "todas" || SegTula = ctxSegTula)) > 0
&& scrTulas.Width >= 1100
```

### 6.3 La galería

`galTulasTula` (**Galería vertical en blanco**):

| Propiedad | Valor |
|---|---|
| X / Y | `16` / `66` |
| Width | `cardTablaTula.Width - 32` |
| Height | `cardTablaTula.Height - 90` |
| TemplateSize | `44` |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| **OnSelect** | `false` — la fila **no navega** (el mockup no marca `rowclick` en esta tabla). Si mañana se abre un detalle, la lógica va acá y no en `btnFilaTula` (receta 01 §7.3) |
| Items | ver abajo |

```powerfx
With(
    { ord: If(ctxSortAscTula, SortOrder.Ascending, SortOrder.Descending) },
    With(
        { base: Filter(colTulasTula, ctxSegTula = "todas" || SegTula = ctxSegTula) },
        If(
            ctxSortTula = "tula",
            SortByColumns(base, "sanic_bagid", ord),
            SortByColumns(base, "createdon", ord)
        )
    )
)
```

> - El nombre de columna de `SortByColumns` es **literal** (nombre lógico, igual que
>   `02` §7 con `"sanic_name"`); lo que varía por variable es el `SortOrder`. Si el
>   Studio del día rechaza `ord` como argumento, desdoblar en cuatro ramas
>   (`tula/creado` × `asc/desc`) — el resultado es el mismo.
> - Todo esto corre **sobre la colección local**: cero consultas al ordenar o cambiar
>   de segmento. La pantalla vieja re-consultaba el datasource en cada tab.

### 6.4 Template de fila

**X del template = X del encabezado − 16** (X de la galería); los templates arrancan
en 0. **Todas las celdas: `Y 12 · Height 20`** — el default de una Etiqueta clásica es
40 de alto y con `Y 12` se comería el separador de `Y 43` y se metería en la fila de
abajo. Todos los hijos llevan `OnSelect = Select(Parent)` (contrato 01 §7.3) aunque hoy
`galTulasTula.OnSelect` sea `false`: así el día que la fila tenga acción no hay que
tocar seis controles, y ninguna etiqueta se come el clic.

**Ni una sola celda dot-walkea**: todas leen columnas materializadas en la carga
(§4.1). Eso es lo que mata el N+1 por fila.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaTula` | Botón clásico (**overlay, al FONDO del z-order**) | X `0` · Y `0` · Width `galTulasTula.Width` · Height `44` (= TemplateSize) · Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · OnSelect `Select(Parent)` |
| `lblCelTulaTula` | Etiqueta clásica | ESTILO-MONO · X `0` · Width `galTulasTula.Width * 0.10` · Y `12` · Height `20` · Text `ThisItem.BagID` · OnSelect `Select(Parent)` |
| `lblCelReciboTula` | Etiqueta clásica | ESTILO-CELDA · Color `ThemeInk2` · X `galTulasTula.Width * 0.10` · Width `galTulasTula.Width * 0.12` · Y `12` · Height `20` · Text `Coalesce(ThisItem.ReciboTula, "—")` (columna materializada — §6.5) · OnSelect `Select(Parent)` |
| `lblCelClienteTula` | Etiqueta clásica | ESTILO-CELDA · X `galTulasTula.Width * 0.22` · Width `galTulasTula.Width * 0.21` · Y `12` · Height `20` · Text `Coalesce(ThisItem.ClienteTula, "—")` (materializada desde `ReceptionID.RemitentName` — **1 salto**, ver §9.3) · **Visible `scrTulas.Width >= 1100`** · OnSelect `Select(Parent)` |
| `pilCelEstadoTula` | **Botón clásico** (píldora, receta 01 §7.2) | X `galTulasTula.Width * 0.43` · Y `11` · Width `Min(12 + 7 * Len(Self.Text), galTulasTula.Width * 0.235)` (el tope impide que la píldora más larga se coma la columna CREADO en anchos chicos — ver §6.6) · Height `22` · **Text, Fill y Color llevan CADA UNO su propio `Switch` completo** (§6.6 — un `With` no puede setear tres propiedades) · DisplayMode `View` · **OnSelect `Select(Parent)`** (por contrato, aunque en modo Ver no se dispare) |
| `lblCelCreadoTula` | Etiqueta clásica | ESTILO-CELDA · Color `ThemeInk2` · X `galTulasTula.Width * 0.67` · Width `galTulasTula.Width * 0.16` · Y `12` · Height `20` · Text `Text(ThisItem.'Created On', FmtFechaCorta)` · OnSelect `Select(Parent)` |
| `lblCelCreadoPorTula` | Etiqueta clásica | ESTILO-CELDA · Color `ThemeInk2` · X `galTulasTula.Width * 0.83` · Width `galTulasTula.Width * 0.17` · Y `12` · Height `20` · Text `Coalesce(ThisItem.CreadoPorTula, "—")` (columna materializada) · **Visible `scrTulas.Width >= 1100`** · OnSelect `Select(Parent)` |
| `shpSepTula` | Rectángulo clásico | X `0` · Y `43` (= TemplateSize − 1) · Width `galTulasTula.Width` · Height `1` · Fill `ThemeLine` |

> `FmtFechaCorta` = `"dd/mm HH:mm"` (01 §3) → `14/08 08:41`, igual que el mockup.
> **`HH`, nunca `hh`** (gotcha de la skill; `hh` sería reloj de 12 h sin AM/PM).

### 6.5 Columna RECIBO — dependencia B-3

| Escenario | Qué se materializa en §4.1 | `lblCelReciboTula.Text` | Costo |
|---|---|---|---|
| **HOY, sin backend** | `ReciboTula, ReceptionID.ReceiptID` | `Coalesce(ThisItem.ReciboTula, "—")` | 1 salto **en la carga**, una vez por fila traída |
| **CON B-3** | `ReciboTula, ReceiptIDMirror` (sin dot-walk) | igual — la celda no cambia | **Cero** |

> Fijate el patrón: lo que cambia con el backend es **la línea del `AddColumns`**, no
> la celda. Esa es toda la gracia de materializar en la carga.

### 6.6 Píldora de estado — tabla de estados

Cada propiedad lleva su propio `Switch(ThisItem.BagStatus, …)` (Text, Fill y Color son
tres fórmulas independientes):

| `ThisItem.BagStatus` | Text | Fill | Color | Chip del mockup |
|---|---|---|---|---|
| `'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'` | `"Recibida en esclusa"` | `ThemeWarnSoft` | `ThemeWarn` | `.chip.warn` |
| `'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'` | `"Pend. rev. — esclusa"` | `ThemeAccentSoft` | `ThemeAccent` | `.chip.accent` |
| `'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento'` | `"Pend. rev. — recuento"` | `ThemeAccentSoft` | `ThemeAccent` | `.chip.accent` |
| `'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'` | `"En bóveda"` | `ThemePosSoft` | `ThemePos` | — (no dibujado) |
| `'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'` | `"Asignada a cajero"` | `ThemeSunk` | `ThemeInk3` | — (no dibujado) → se usa `.chip.flat` |
| `'BagStatus (CH-Tbl-Bags)'.Recontada` | `"Recontada"` | `ThemeSunk` | `ThemeInk3` | — (no dibujado) → se usa `.chip.flat` |
| *default* (valor nuevo del optionset) | `"Estado " & Text(ThisItem.BagStatus)` | `ThemeSunk` | `ThemeInk3` | — |

`Text` completo, listo para pegar:

```powerfx
Switch(ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             "Recibida en esclusa",
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  "Pend. rev. — esclusa",
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento', "Pend. rev. — recuento",
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            "En bóveda",
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               "Asignada a cajero",
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         "Recontada",
    "Estado " & Text(ThisItem.BagStatus)
)
```

`Fill` completo:

```powerfx
Switch(ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             ThemeWarnSoft,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  ThemeAccentSoft,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento', ThemeAccentSoft,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            ThemePosSoft,
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               ThemeSunk,
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         ThemeSunk,
    ThemeSunk
)
```

`Color` completo:

```powerfx
Switch(ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             ThemeWarn,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  ThemeAccent,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento', ThemeAccent,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            ThemePos,
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               ThemeInk3,
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         ThemeInk3,
    ThemeInk3
)
```

> - **Ancho de la píldora**: el texto más largo (`"Pend. rev. — recuento"`, 21 car.)
>   mide 159 px con la fórmula `12 + 7 × Len`. La columna ESTADO mide `0.24 × ancho de
>   galería`: 192 px a 1366 y 174 px a 1024 — entra en los dos. El `Min(…, 0.235 ×
>   ancho)` de §6.4 es la red de seguridad por si alguien alarga un texto. **Si
>   agregás o alargás un estado, recalculá**: `12 + 7 × Len(texto) ≤ 0.235 × (ancho de
>   pantalla − 298)`.
> - Las variantes neutras (`ThemeSunk`) son las únicas que llevan borde — lo resuelve
>   la receta 01 §7.2 con `BorderThickness = If(Self.Fill = ThemeSunk, 1, 0)`, sin
>   tocar nada acá. (El contador `pilCountTula` es la excepción declarada en §6.1.)
> - **Por qué las dos neutras comparten Fill y Color**: el único chip neutro del
>   mockup es `.chip.flat` (`surface-sunk` / `ink-3`) — `ink-2` no existe como color
>   de chip. *Asignada a cajero* y *Recontada* se distinguen por su **texto**, no por
>   su color: ninguna de las dos requiere acción del supervisor.
> - **Decisión sobre "Recontada" (queda anotada a propósito)**: en `05` el cheque
>   *Recontado* va teal (`ThemePos`, `.chip.blue` del mockup) porque ahí marca "listo
>   para enviar" — es un estado **accionable**. Acá la tula *Recontada* es terminal y
>   no acciona nada, así que va neutra. Mismo lexema, dos semánticas; si el negocio
>   prefiere unificar en teal, es un cambio de dos celdas en esta tabla.

## 7. Estado vacío y aviso de tope

### 7.1 Estado vacío — mensaje POR SEGMENTO

Los tres controles con
**Visible `CountRows(Filter(colTulasTula, ctxSegTula = "todas" || SegTula = ctxSegTula)) = 0 && !ctxCargandoTula`**,
centrados en `cardTablaTula`:

| Control | Tipo | Propiedades |
|---|---|---|
| `icoVacioTula` | Icono | Icon `Icon.Folder` · X `cardTablaTula.Width/2 - 16` · Y `cardTablaTula.Height/2 - 60` · 32×32 · Color `ThemeInk3` |
| `lblVacioTituloTula` | Etiqueta clásica | X `16` · Y `cardTablaTula.Height/2 - 18` · Width `cardTablaTula.Width - 32` · Height `22` · Size `12` · FontWeight `Semibold` · Color `ThemeInk2` · Align `Align.Center` · Text: ver abajo |
| `lblVacioTextoTula` | Etiqueta clásica | X `16` · Y `cardTablaTula.Height/2 + 6` · Width `cardTablaTula.Width - 32` · Height `36` · Size `9.5` · Color `ThemeInk3` · Align `Align.Center` · Wrap `true` · Text `"Ampliá el rango de fechas, limpiá la búsqueda o probá con otro segmento."` |

```powerfx
// lblVacioTituloTula.Text — el fix del NoDataText clonado de la app vieja
Switch(ctxSegTula,
    "todas",      "No hay tulas en el rango de fechas seleccionado",
    "esclusa",    "No hay tulas en esclusa en este rango",
    "boveda",     "No hay tulas almacenadas en bóveda en este rango",
    "cajero",     "No hay tulas asignadas a cajero en este rango",
    "pendrec",    "No hay tulas pendientes de revisión por recuento en este rango",
    "procesadas", "No hay tulas recontadas en este rango",
    "No hay tulas para mostrar"
)
```

> La vieja repetía **"No hay tulas en esclusa."** en los cuatro tabs, incluidos Bóveda
> y Procesadas. Este `Switch` es el fix, y es la razón de que el segmento viva en una
> variable de contexto y no en un control de dataset.

### 7.2 Aviso de tope de la ventana

| Control | Tipo | Propiedades |
|---|---|---|
| `lblTopeTula` | Etiqueta clásica | X `16` · Y `cardTablaTula.Height - 22` · Width `cardTablaTula.Width - 32` · Height `18` (la banda libre bajo la galería es de 24 px) · Size `8.5` · Color `ThemeWarn` · FontWeight `Semibold` · **Visible `CountRows(colTulasTula) >= 2000`** · Text `"Se alcanzó el tope de 2.000 filas: los contadores y los totales son parciales. Acotá el rango de fechas."` |

> Honestidad de números: con el Límite de filas de datos en `2000` (contrato de
> entrada), una ventana enorme puede truncar. En vez de mentir en silencio —que es
> exactamente lo que hacía la vieja con sus 500— la pantalla lo dice.

## 8. Overlay de carga (receta 01 §7.6)

| Control | Tipo | Propiedades |
|---|---|---|
| `cntCargandoTula` | Contenedor manual | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxCargandoTula`** · **último en el z-order de la pantalla** |
| `lblCargandoTula` | Etiqueta clásica | X `0` · Y `Parent.Height/2 - 12` · Width `Parent.Width` · Height `24` · Size `12` · Color `Color.White` · Align `Align.Center` · Text `"Cargando tulas..."` |

## 9. Notas de la guía

### 9.1 Dependencias de backend

| ID | Qué | Dónde pega | Fallback sin backend |
|---|---|---|---|
| **B-3** | `sanic_receiptidmirror` (texto) en Bags | §4.2 (búsqueda) y §6.5 (columna RECIBO) | Búsqueda **solo por número de tula** (delegable) + recibo materializado con 1 salto en la carga. **Verificado hoy**: la columna NO está en `sanic_CHTblBags/Entity.xml` |
| **B-6 (opcional, NO bloqueante)** | `sanic_clientnamemirror` (texto) en Bags | Solo si se quiere hacer el nombre de cliente **buscable y delegable** desde Bags | **Ninguno hace falta para mostrarlo**: `sanic_RemitentName` (nvarchar 200, display `RemitentName`) ya existe **en Receptions** y la escribe `CH-App-Reception`. La columna CLIENTE se resuelve con **1 salto**, igual que RECIBO |

> **Lo que NO hace falta** (y estaba de más en la primera versión de esta guía): no se
> dot-walkea a `RemitentClient.Name`, así que esta pantalla **no requiere
> `prvRead sanic_CHTblClient`** (00 §3.3 sigue vigente para las otras pantallas) y el
> espejo B-6 deja de ser prerrequisito. Un `Entity.xml` bien leído ahorró una columna
> nueva, un plugin y un privilegio.

### 9.2 Divergencias declaradas vs mockup (además del ledger 00 §4)

1. **Punto de sincronía + botón "Actualizar"** en la barra de filtros: el mockup de
   `s-tulas` no los dibuja (sí los dibuja `s-op`). Se agregan porque la pantalla
   trabaja sobre una **colección local** y el usuario necesita saber de cuándo son los
   datos y poder recargar sin tocar los filtros. Los filtros igual auto-aplican
   (`OnChange = Select(btnActualizarTula)`), así que la sensación "sin botón" del
   mockup se mantiene. Y por eso mismo el botón va **secundario (fantasma)** y no
   primario como el de `s-op`/`02`: acá refrescar es redundante, no es la acción
   principal de la pantalla. Misma diferencia de jerarquía, a propósito.
2. **Contador dentro del texto del botón** del segmentado: el mockup lo pinta con
   opacidad `.7` en un `<span>`; un Botón clásico tiene un solo color de texto, así
   que el número va en el mismo `Text` separado por dos espacios.
3. **Flecha de orden solo en la columna activa**: el mockup dibuja `▼` en *Tula* **y**
   en *Creado* a la vez (es HTML estático). En la app hay un único criterio de orden
   activo, así que la flecha aparece solo ahí. Orden por defecto: **Creado ▼**.
   *Nota*: las filas de ejemplo del mockup están en orden **ascendente** (08:41 →
   09:19), contradiciendo su propia flecha. Se prioriza el glifo (`▼`) y el criterio
   de negocio "lo más nuevo arriba".
4. **Textos de las píldoras acortados** respecto a los labels del optionset:
   *"Pendiente de revision por supervisor - esclusa"* → **"Pend. rev. — esclusa"**
   (idem recuento), y *"Almacenada en boveda"* → **"En bóveda"**. Motivo: el label
   real no entra en la columna y viene sin tildes. El mockup ya acortaba a *"Pend.
   revisión — esclusa"*; se acorta un paso más (`rev.`) para que la píldora entre en
   la columna ESTADO también a 1024 px (§6.6). **El valor filtrado siempre es el del
   optionset**; solo cambia lo que se muestra. El botón del segmentado conserva el
   copy largo del mockup (*"Pend. revisión recuento"*): ahí sí hay lugar.
5. **La fila no es clicable**: fiel al mockup (la tabla de `s-tulas` no tiene
   `rowclick`, a diferencia de la de `s-op`). El overlay `btnFilaTula` existe solo
   para recuperar el `tr:hover` y queda cableado con `Select(Parent)`.
6. **Tab "Declaraciones pendientes" NO se porta**: la pantalla vieja tenía un cuarto
   tab sobre `CH-Tbl-DeclaredBags` (`IsRelatedToBag = false`). El mockup aprobado no
   lo incluye y esta pantalla es de **localización de tulas**, no de declaraciones.
   Si el negocio lo reclama, es una pantalla/segmento aparte — no entra por la
   ventana.
7. **Contadores del segmentado = de la ventana filtrada**, no del histórico (§5).
8. **Placeholder del buscador**: el mockup dice *"Número de tula o de recibo…"*; sin
   B-3 la app dice *"Número de tula…"*. No se promete lo que la consulta no puede
   entregar. Con B-3 vuelve al copy exacto del mockup (§4.2).
9. **Aviso de tope (`lblTopeTula`) y overlay de carga (`cntCargandoTula`)**: no están
   en el mockup. El primero es honestidad de números (la ventana es truncable, §7.2);
   el segundo es la receta 01 §7.6. Ambos son **invisibles en el camino feliz**.
10. **Ventana por defecto = últimos 30 días** (`Today() - 30` → `Today()`); el mockup
    muestra `01/08 → 14/08`, que en la fecha del prototipo es "mes en curso". Se elige
    la ventana móvil porque no colapsa el día 1 de cada mes (un "mes en curso" el 1
    de septiembre mostraría una sola jornada). Si el negocio pide mes en curso, es
    cambiar el `SelectedDate` de `dtDesdeTula` por
    `Date(Year(Today()), Month(Today()), 1)`.
11. **La nota de diseño al pie (`.cap`)** del mockup no se construye: es anotación
    del prototipo para el aprobador, no UI del producto. Su contenido está traducido a
    requisitos verificables en §7.1 (mensaje por segmento) y §4.3 (fecha inclusiva).
12. **El `tr:hover` del mockup no cubre toda la fila**: el `HoverFill` de
    `btnFilaTula` solo se pinta donde el overlay queda expuesto — al pasar sobre una
    etiqueta hija (que es un control propio) el hover no se dispara. Canvas clásico no
    tiene hover heredado; se acepta el resaltado parcial antes que meter un control
    por celda.
13. **Radios de extremo del segmentado al envolver**: `segTodasTula` (izquierda) y
    `segProcesadasTula` (derecha) llevan radio externo pensado para UNA fila; cuando
    el contenedor envuelve a dos, los redondeos quedan en el medio de la caja. Es
    cosmético y solo bajo 1100 px.

### 9.3 Costo declarado del dot-walk por fila

| Columna materializada en §4.1 | Origen | Saltos | Cuándo se paga |
|---|---|---|---|
| `ReciboTula` | `ReceptionID.ReceiptID` (sin B-3) | 1 | **En la carga**, una vez por fila traída |
| `ClienteTula` | `ReceptionID.RemitentName` | **1** — no 2: `RemitentName` es el nombre del remitente ya materializado en Receptions (verificado en `sanic_CHTblReceptions/Entity.xml`, lo escribe `CH-App-Reception`) | Ídem |
| `CreadoPorTula` / `CreadorGuidTula` | `'Created By'.'Full Name'` / `.User` | 1 | Ídem |

**Ninguna columna de esta pantalla cuesta 2 saltos, y ninguna se paga por render.**
La diferencia entre materializar en la carga y dot-walkear en el template es pagar
N expansiones **una vez** contra pagarlas **en cada scroll**.

Mitigaciones adicionales: (a) las dos columnas de **baja prioridad** —CLIENTE y CREADO
POR— llevan `Visible = scrTulas.Width >= 1100` (menos ruido en tablet, aunque el costo
ya está pago en la carga); (b) `TemplateSize 44` mantiene bajo el número de filas
renderizadas. **Ninguno de estos campos se usa para filtrar ni ordenar** — filtrar por
ellos sería el error de la app vieja.

### 9.4 Responsive (01 §8)

- Breakpoint **EN VIVO**: `scrTulas.Width >= 1100` referenciado directamente en cada
  `Visible` — nunca congelado en una variable de `OnVisible`.
- `cntSegTula` con `LayoutWrap = true` **y alto condicional**
  (`If(scrTulas.Width < 1100, 68, 34)`): los 6 botones envuelven a dos filas bajo
  1100 px y el contenedor crece para alojarlas — un auto-layout no crece solo. Cada
  botón lleva su `LayoutMinWidth` o el wrap nunca dispara (`01` §8.4).
- Columnas ocultas bajo 1100 px: **CLIENTE** (`lblColClienteTula` +
  `lblCelClienteTula`) y **CREADO POR** (`lblColCreadoPorTula` +
  `lblCelCreadoPorTula`). El **encabezado** lleva además la condición de "hay filas"
  (`CountRows(...) > 0 &&`, §6.2) porque vive FUERA de la galería; la **celda** no la
  necesita — sin filas no hay template que renderizar. Si uno se oculta y el otro no,
  queda un encabezado fantasma.
- Los X/Width de columna son fracciones del `galTulasTula.Width`: al ocultar columnas
  las demás **no se corren** (quedan huecos). Es a propósito: la fila no baila cuando
  el usuario redimensiona.

## 10. Checklist de salida

- [ ] Al entrar por primera vez la pantalla **carga sola** (sin apretar Actualizar),
      cae en el segmento **En esclusa** y muestra "Actualizado HH:mm:ss".
- [ ] Los **6 segmentos** existen y suman: la cuenta de "Todas" es exactamente la suma
      de los otros cinco (si no, hay un estado cayendo en `"otros"` → optionset nuevo).
- [ ] **Asignada a cajero** y **Pend. revisión recuento** muestran filas reales — el
      bug central de la pantalla vieja.
- [ ] Con "Fecha hasta = hoy" aparecen las tulas creadas **hoy** (bug BL-02 cerrado).
- [ ] Cambiar fecha, creador o texto de búsqueda **recarga solo** (sin apretar nada) y
      el reloj de "Actualizado" cambia.
- [ ] Escribir un número de tula parcial filtra por prefijo; borrarlo devuelve todo.
- [ ] Con B-3 en el entorno: buscar por número de **recibo** devuelve resultados.
      Sin B-3: el placeholder dice solo "Número de tula…" (nada promete lo que no hay).
- [ ] El desplegable **Creado por** abre con "Cualquiera" primero y lista **solo los
      creadores que realmente aparecen en la ventana cargada** — no el directorio.
- [ ] Elegir un creador filtra la tabla **y el desplegable sigue mostrando ese
      nombre** después de que termina la carga (si se vacía solo, falta el `Default`
      re-anclado o el `Filter` está leyendo `drp….Selected` en vez de la variable).
- [ ] Volver a "Cualquiera" repuebla el desplegable con todos los creadores.
- [ ] Los dos selectores de fecha abren con el rango de 30 días ya puesto (si abren
      vacíos, alguien escribió `DefaultDate` en vez de `SelectedDate`).
- [ ] Con la tabla vacía **no quedan encabezados huérfanos**: desaparecen los 6
      títulos de columna, la banda gris y la línea.
- [ ] Ninguna celda pisa el separador de la fila de abajo (todas con `Height 20`).
- [ ] Verificar **Configuración → General → Límite de filas de datos = 2000**. Con una
      ventana enorme que llegue a 2.000 filas aparece el aviso ámbar de tope (§7.2);
      con una ventana normal NO aparece.
- [ ] Clic en **TULA** y en **CREADO** ordena y la flecha ▲/▼ cambia solo en la
      columna activa; volver a hacer clic invierte el orden. **No dispara consultas**
      (el reloj de "Actualizado" no cambia).
- [ ] Cada segmento vacío muestra **su propio** mensaje (Bóveda no dice "esclusa").
- [ ] Las píldoras de estado usan los colores de la tabla §6.6 y ninguna recorta
      texto a 1366 px.
- [ ] A **exactamente 1100 px** y a **1024 px** de ancho de ventana: la píldora más
      larga ("Pend. rev. — recuento") NO tapa la fecha de la columna CREADO.
- [ ] El desplegable **Creado por** no repite ningún nombre (si aparecen duplicados,
      alguien volvió al `ForAll` que se lee a sí mismo en vez del `GroupBy`).
- [ ] Achicar la ventana bajo 1100 px: se ocultan **CLIENTE** y **CREADO POR**
      (encabezado y celda juntos) y el segmentado envuelve; nada se superpone.
- [ ] Pasar el mouse por una fila la resalta; **hacer clic no navega** a ningún lado.
- [ ] Desconectar la red y apretar Actualizar: `Notify` de error y el overlay
      **se apaga** (no queda pegado).
- [ ] App Checker: **cero errores** y **cero advertencias de delegación** en
      `btnActualizarTula.OnSelect` (si aparece la del triángulo azul, alguien dejó la
      variante de emergencia de §4.2).
- [ ] Búsqueda en el árbol: no existe ningún control con nombre autogenerado
      (`Label1`, `Button1`, …) ni resto de `deleteMode`/`newMode`/`CurrentItem`.
