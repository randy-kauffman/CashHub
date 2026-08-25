# Guía 02 — scrClientes (listado de clientes + alta vía RFC)

Pantalla principal del maestro de clientes (vistas **CL1** y **CL2** del mockup): KPIs del
maestro, segmentado por estado de registro, búsqueda delegable por Nombre/CIFBCO/CIFCOM,
grilla paginada con badge "RFC en curso" por fila, y el panel lateral **"Nueva solicitud de
alta de cliente"** que crea la cabecera RFC tipo **Alta** + sus **3 líneas** (Nombre, CIFBCO,
CIFCOM) contra el catálogo `CH-Tbl-ClientTableLogicalFieldNames`. Todo lo no explicado acá
(tipos de control, estilos `ESTILO-*`, recetas 7.x, gotchas 8.x, tokens `Theme*`/`fx*`) vive
en la **guía 01** y se referencia por número/alias.

**CONTRATO DE ENTRADA** (debe estar listo ANTES de construir/abrir esta pantalla):

| Qué | Quién lo setea |
|---|---|
| `gvarUser`, `colMisPermisos`, `gvarRolTexto` | `App.OnStart` (01 §4) |
| Tokens `Theme*`, constantes `fx*`, `FmtHora` | `App.Formulas` (01 §3) |
| `cmpAppHeader` extendido y publicado en `CH-CL-SharedComponents` | Guía 01 §7.2 (paso previo) |
| Orígenes de datos: `CH-Tbl-Clients`, `CH-Tbl-ClientDataChangeRequests`, `CH-Tbl-ClientDataChangeRequestLines`, `CH-Tbl-ClientTableLogicalFieldNames` | Guía 01 §2 |
| Pantallas `scrCliente` y `scrSolicitudes` creadas (aunque vacías) para que los `Navigate()` compilen | Guía 01, orden de construcción |

**CONTRATO DE SALIDA** (lo que esta pantalla deja seteado al navegar):

| Qué | Cuándo | Quién lo consume |
|---|---|---|
| `gvarClienteSel` (registro de `CH-Tbl-Clients`) | Al hacer clic en una fila de `galClientesCli` | `scrCliente` (guía 03) |
| `colRfcEnCursoCli` (RFCs sin resolver, filas completas) | En `OnVisible` y tras crear una solicitud desde el panel | Solo esta pantalla (badges + KPIs); las demás pantallas arman la suya |
| `colClientesCli` (resultado filtrado materializado) | En `OnVisible` y en cada cambio de segmento/búsqueda | Solo esta pantalla (grilla + paginación) |

Esta pantalla **escribe** únicamente en `CH-Tbl-ClientDataChangeRequests` (cabecera, Patch
directo con `'Change Status'` = Submitted) y `CH-Tbl-ClientDataChangeRequestLines` (3 líneas
del alta). No usa la Custom API `sanic_CHResolveChangeRequest` (eso es de `scrSolicitud`,
guía 05) — acá no hay fallback de backend que documentar.

---

## Árbol de controles

```
scrClientes
├── cmpAppHeaderCli            Componente (receta 7.2)
├── cntToolbarCli              Contenedor (manual)
│   ├── cntSegEstadoCli        Contenedor (manual) — pista del segmentado de estado
│   │   ├── btnSegActivosCli       Botón clásico
│   │   ├── btnSegInactivosCli     Botón clásico
│   │   ├── btnSegPendientesCli    Botón clásico
│   │   └── btnSegTodosCli         Botón clásico
│   ├── cntBusqCli             Contenedor (manual) — buscador (receta 7.9)
│   │   ├── icoLupaBusqCli         Icono clásico (Icon.Search)
│   │   └── inpBusqCli             Entrada de texto clásica
│   └── btnNuevoClienteCli     Botón clásico
├── cntBodyCli                 Contenedor (manual)
│   ├── cntKpiActivosCli       Contenedor (manual) — KPI "Clientes activos"
│   │   ├── lblKpiActivosLabCli    Etiqueta clásica
│   │   └── lblKpiActivosValCli    Etiqueta clásica
│   ├── cntKpiRfcCli           Contenedor (manual) — KPI "Con RFC en curso"
│   │   ├── lblKpiRfcLabCli        Etiqueta clásica
│   │   ├── lblKpiRfcValCli        Etiqueta clásica
│   │   └── lblKpiRfcSubCli        Etiqueta clásica
│   ├── cntKpiAltasCli         Contenedor (manual) — KPI "Altas pendientes"
│   │   ├── lblKpiAltasLabCli      Etiqueta clásica
│   │   ├── lblKpiAltasValCli      Etiqueta clásica
│   │   └── lblKpiAltasSubCli      Etiqueta clásica
│   ├── cntKpiHoyCli           Contenedor (manual) — KPI "Solicitudes hoy"
│   │   ├── lblKpiHoyLabCli        Etiqueta clásica
│   │   ├── lblKpiHoyValCli        Etiqueta clásica
│   │   └── lblKpiHoySubCli        Etiqueta clásica
│   └── cntGrillaCli           Contenedor (manual) — card de la grilla
│       ├── btnColClienteCli       Botón clásico — encabezado ordenable "Cliente"
│       ├── lblColCifBcoCli        Etiqueta clásica
│       ├── lblColCifComCli        Etiqueta clásica
│       ├── lblColEstadoCli        Etiqueta clásica
│       ├── lblColSolicitudCli     Etiqueta clásica
│       ├── lblColModifCli         Etiqueta clásica
│       ├── shpColLineaCli         Rectángulo clásico — línea bajo el encabezado
│       ├── galClientesCli         Galería vertical en blanco (receta 7.4)
│       │   ├── btnFilaCli             Botón clásico — overlay clicable (receta 7.4)
│       │   ├── lblNombreFilaCli       Etiqueta clásica
│       │   ├── lblCifBcoFilaCli       Etiqueta clásica
│       │   ├── lblCifComFilaCli       Etiqueta clásica
│       │   ├── pilEstadoFilaCli       Botón clásico — pill de estado (receta 7.3, gotcha 8.5)
│       │   ├── pilRfcFilaCli          Botón clásico — pill "RFC en curso" (receta 7.3, gotcha 8.5)
│       │   ├── lblModifFilaCli        Etiqueta clásica
│       │   └── shpSepFilaCli          Rectángulo clásico — separador (receta 7.10)
│       ├── lblVacioGrillaCli      Etiqueta clásica — estado vacío
│       └── cntPagCli              Contenedor (manual) — paginación (receta 7.8)
│           ├── lblResumenPagCli       Etiqueta clásica
│           ├── btnPagAntCli           Botón clásico "‹"
│           └── btnPagSigCli           Botón clásico "›"
├── shpVeloPanCli              Rectángulo clásico — velo del panel (receta 7.7)
├── btnVeloClicPanCli          Botón clásico — clic-afuera cierra (receta 7.7)
└── cntPanelAltaCli            Contenedor (manual) — panel lateral CL2 (receta 7.7)
    ├── cntPanelAltaHeadCli    Contenedor (manual)
    │   ├── lblPanelTitCli         Etiqueta clásica
    │   ├── lblPanelSubCli         Etiqueta clásica
    │   └── icoPanelXCli           Icono clásico (Icon.Cancel)
    ├── cntPanelAltaBodyCli    Contenedor (manual)
    │   ├── lblNombreLabPanCli     Etiqueta clásica
    │   ├── inpNombrePanCli        Entrada de texto clásica
    │   ├── lblCifBcoLabPanCli     Etiqueta clásica
    │   ├── inpCifBcoPanCli        Entrada de texto clásica
    │   ├── lblCifComLabPanCli     Etiqueta clásica
    │   ├── inpCifComPanCli        Entrada de texto clásica
    │   ├── lblCifComErrPanCli     Etiqueta clásica — error de formato
    │   ├── lblMotivoLabPanCli     Etiqueta clásica
    │   ├── inpMotivoPanCli        Entrada de texto clásica (MultiLine)
    │   ├── lblResumenSecPanCli    Etiqueta clásica — "RESUMEN DE LA SOLICITUD (3 CAMBIOS)"
    │   ├── cntResumenPanCli       Contenedor (manual) — mini-card del resumen
    │   │   ├── lblResumenHeadPanCli   Etiqueta clásica
    │   │   ├── lblResumenNombrePanCli Etiqueta clásica
    │   │   ├── lblResumenCifBcoPanCli Etiqueta clásica
    │   │   └── lblResumenCifComPanCli Etiqueta clásica
    │   └── cntBannerPanCli        Contenedor (manual) — banner info (receta 7.11)
    │       ├── shpBannerPanCli        Rectángulo clásico
    │       └── lblBannerPanCli        Etiqueta clásica
    └── cntPanelAltaFootCli    Contenedor (manual)
        ├── btnCancelarPanCli      Botón clásico
        └── btnCrearPanCli         Botón clásico
```

78 controles. Regla de coordenadas del template (gotcha 8.14, se enuncia UNA vez): la galería
`galClientesCli` está en `X 0` dentro de `cntGrillaCli`, así que **X de cada control del
template = X del control de encabezado correspondiente** — por eso los controles de fila
referencian directamente `btnColClienteCli.X`, `lblColCifBcoCli.X`, etc.

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |

**`OnVisible`** — COMPLETO, listo para pegar (resetea estado por gotcha 8.3, carga RFCs en
curso y el listado con el segmento default "Activos"):

```powerfx
// 1) Estado local a default (gotcha 8.3 — no arrastrar estado de la visita anterior)
UpdateContext({
    ctxSegEstadoCli: "Activos",
    ctxPagCli: 1,
    ctxPagTamCli: 50,
    ctxOrdenAscCli: true,
    ctxPanelAltaCli: false,
    ctxGuardandoCli: false
});
Reset(inpBusqCli);
// 2) RFCs sin resolver (badge "RFC en curso" + KPIs) — acotada por ESTADO server-side.
//    Filtro delegable: ORs sobre el mismo picklist. Se re-ejecuta al volver de crear
//    una solicitud (el boton del panel la refresca, y este OnVisible tambien).
ClearCollect(
    colRfcEnCursoCli,
    Filter(
        'CH-Tbl-ClientDataChangeRequests',
        'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
        || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
        || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved
    )
);
// 3) Listado inicial: segmento "Activos", sin texto de busqueda (Search con "" devuelve todo)
ClearCollect(
    colClientesCli,
    Search(
        Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Active),
        Trim(inpBusqCli.Text),
        Name, CIFBCO, CIFCOM
    )
)
```

> 🧨 El enum del estado de registro es **`'CH-Ch-RecordStatus'`** (option set global
> `sanic_chchrecordstatus`) — así lo nombra Power Fx en el fuente REAL de la v1
> (`RecordStatus = 'CH-Ch-RecordStatus'.Inactive`), NO `'RecordStatus (CH-Tbl-Clients)'`
> como aproximó la guía 01 §7.3. Si tu IntelliSense ofrece otro nombre, **el suyo manda**
> (ver Notas). El filtro de igualdad de picklist es delegable (gotcha 8.12).

---

## 2. Encabezado — `cmpAppHeaderCli`

Instancia del componente compartido, TODAS sus propiedades fijadas por la **receta 7.2** de
la guía 01 — incluida la instanciación exacta para `scrClientes` que la receta trae como
ejemplo. Se pega tal cual:

```powerfx
cmpAppHeaderCli As cmpAppHeader:
    Activo: ="Clientes"
    Height: =fxHeaderH
    OnClientes: =false
    OnSolicitudes: =Navigate(scrSolicitudes, ScreenTransition.None)
    RolTexto: =gvarRolTexto
    Subtitulo: ="Maestro de clientes"
    Titulo: ="Administración de Clientes"
    Width: =Parent.Width
```

(`X 0 · Y 0` — esqueleto de la receta 7.1.)

---

## 3. Toolbar — segmentado de estado, buscador y "Nuevo cliente"

### 3.1 `cntToolbarCli`

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `fxHeaderH` |
| Width / Height | `Parent.Width` / `54` |
| Fill | `Color.Transparent` |

### 3.2 Segmentado de estado (`cntSegEstadoCli` + 4 botones)

Misma anatomía que la pista de nav de `cmpAppHeader` (receta 7.2): pista con borde + botones
píldora. `Parent` de los botones = `cntSegEstadoCli`.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntSegEstadoCli` | Contenedor (manual) | `fxGutter` | `(Parent.Height - 34)/2` | `btnSegTodosCli.X + btnSegTodosCli.Width + 3` | `34` | Fill `ThemeSurface2` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `9` |
| `btnSegActivosCli` | Botón clásico | `3` | `3` | `Len(Self.Text) * 6 + 22` | `28` | ver plantilla abajo |
| `btnSegInactivosCli` | Botón clásico | `btnSegActivosCli.X + btnSegActivosCli.Width + 3` | `3` | `Len(Self.Text) * 6 + 22` | `28` | ídem |
| `btnSegPendientesCli` | Botón clásico | `btnSegInactivosCli.X + btnSegInactivosCli.Width + 3` | `3` | `Len(Self.Text) * 6 + 22` | `28` | ídem |
| `btnSegTodosCli` | Botón clásico | `btnSegPendientesCli.X + btnSegPendientesCli.Width + 3` | `3` | `Len(Self.Text) * 6 + 22` | `28` | ídem |

**Plantilla de los 4 botones** (reemplazando `<Seg>` por `"Activos"` / `"Inactivos"` /
`"Pendientes"` / `"Todos"`): `Font.'Open Sans'` · Size `9` · Radius `7` · BorderThickness `0`
· Fill `If(ctxSegEstadoCli = "<Seg>", ThemeSurface, Color.Transparent)` · Color
`If(ctxSegEstadoCli = "<Seg>", ThemeAccent, ThemeInk2)` · FontWeight
`If(ctxSegEstadoCli = "<Seg>", FontWeight.Semibold, FontWeight.Normal)` · HoverFill
`If(ctxSegEstadoCli = "<Seg>", ThemeSurface, ThemeSurface2)`.

**`Text` de cada botón** (contadores delegables — CountRows+Filter simple sobre Dataverse):

```powerfx
// btnSegActivosCli
"Activos (" & CountRows(Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Active)) & ")"
// btnSegInactivosCli
"Inactivos (" & CountRows(Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Inactive)) & ")"
// btnSegPendientesCli
"Pendientes (" & CountRows(
    Filter(
        'CH-Tbl-Clients',
        RecordStatus = 'CH-Ch-RecordStatus'.PendingApproval
        || RecordStatus = 'CH-Ch-RecordStatus'.PendingDeactivation
    )
) & ")"
// btnSegTodosCli
"Todos"
```

**`OnSelect` de cada botón** — fija el segmento y re-materializa el listado (server-side,
delegable) con la búsqueda vigente:

```powerfx
// btnSegActivosCli
UpdateContext({ctxSegEstadoCli: "Activos", ctxPagCli: 1});
ClearCollect(
    colClientesCli,
    Search(
        Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Active),
        Trim(inpBusqCli.Text),
        Name, CIFBCO, CIFCOM
    )
)
```

```powerfx
// btnSegInactivosCli — igual, cambiando el literal y el filtro
UpdateContext({ctxSegEstadoCli: "Inactivos", ctxPagCli: 1});
ClearCollect(
    colClientesCli,
    Search(
        Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Inactive),
        Trim(inpBusqCli.Text),
        Name, CIFBCO, CIFCOM
    )
)
```

```powerfx
// btnSegPendientesCli
UpdateContext({ctxSegEstadoCli: "Pendientes", ctxPagCli: 1});
ClearCollect(
    colClientesCli,
    Search(
        Filter(
            'CH-Tbl-Clients',
            RecordStatus = 'CH-Ch-RecordStatus'.PendingApproval
            || RecordStatus = 'CH-Ch-RecordStatus'.PendingDeactivation
        ),
        Trim(inpBusqCli.Text),
        Name, CIFBCO, CIFCOM
    )
)
```

```powerfx
// btnSegTodosCli
UpdateContext({ctxSegEstadoCli: "Todos", ctxPagCli: 1});
ClearCollect(
    colClientesCli,
    Search('CH-Tbl-Clients', Trim(inpBusqCli.Text), Name, CIFBCO, CIFCOM)
)
```

### 3.3 Buscador (`cntBusqCli`) — receta 7.9

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntBusqCli` | Contenedor (manual) | `cntSegEstadoCli.X + cntSegEstadoCli.Width + fxGap` | `(Parent.Height - 32)/2` | `Min(340, Parent.Width * 0.25)` | `32` | resto: receta 7.9 |
| `icoLupaBusqCli` | Icono clásico | `10` | `(Parent.Height - 16)/2` | `16` | `16` | receta 7.9 (`Icon.Search` · Color `ThemeInk3`) — `Parent` = `cntBusqCli`, centrado verticalmente en sus 32px |
| `inpBusqCli` | Entrada de texto clásica | `32` | `0` | `Parent.Width - 32` | `Parent.Height` | receta 7.9 + `HintText: "Nombre, CIF BCO o CIF COM…"` · `Mode: TextMode.SingleLine` · `DelayOutput: true` · `OnChange` ↓ |

**`inpBusqCli.OnChange`** — re-materializa con el segmento vigente (`Switch` porque acá no
sabemos cuál está activo):

```powerfx
UpdateContext({ctxPagCli: 1});
Switch(
    ctxSegEstadoCli,
    "Activos",
    ClearCollect(
        colClientesCli,
        Search(
            Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Active),
            Trim(inpBusqCli.Text), Name, CIFBCO, CIFCOM
        )
    ),
    "Inactivos",
    ClearCollect(
        colClientesCli,
        Search(
            Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Inactive),
            Trim(inpBusqCli.Text), Name, CIFBCO, CIFCOM
        )
    ),
    "Pendientes",
    ClearCollect(
        colClientesCli,
        Search(
            Filter(
                'CH-Tbl-Clients',
                RecordStatus = 'CH-Ch-RecordStatus'.PendingApproval
                || RecordStatus = 'CH-Ch-RecordStatus'.PendingDeactivation
            ),
            Trim(inpBusqCli.Text), Name, CIFBCO, CIFCOM
        )
    ),
    ClearCollect(
        colClientesCli,
        Search('CH-Tbl-Clients', Trim(inpBusqCli.Text), Name, CIFBCO, CIFCOM)
    )
)
```

> `Search()` es delegable sobre columnas de texto de Dataverse (la v1 ya usaba exactamente
> `Search(..., Name, CIFBCO, CIFCOM)` contra esta tabla). Si el App Checker marcara
> advertencia de delegación en tu versión, reemplazá cada `Search(<origen>, <txt>, Name,
> CIFBCO, CIFCOM)` por
> `Filter(<origen>, StartsWith(Name, <txt>) || StartsWith(CIFBCO, <txt>) || StartsWith(CIFCOM, <txt>))`
> (StartsWith sí está en la lista delegable) — se pierde el "contiene", se gana certeza.

### 3.4 `btnNuevoClienteCli`

| Propiedad | Valor |
|---|---|
| X / Y | `Parent.Width - fxGutter - Self.Width` / `(Parent.Height - 32)/2` |
| Width / Height | `150` / `32` |
| Estilo | `ESTILO-BTN-PRIMARIO` |
| Text | `"+ Nuevo cliente"` |
| Visible | `!IsBlank(LookUp(colMisPermisos, Name = "sanic_chtblclient" && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))` |
| OnSelect | ↓ |

```powerfx
Reset(inpNombrePanCli);
Reset(inpCifBcoPanCli);
Reset(inpCifComPanCli);
Reset(inpMotivoPanCli);
UpdateContext({ctxPanelAltaCli: true})
```

> 🧨 El literal de tabla en `colMisPermisos.Name` es **`"sanic_chtblclient"`** (nombre
> lógico) — verificado en el fuente v1 (`LookUp(colClientDataPermissions, Name =
> "sanic_chtblclient" && Type = ...Contributor)`) y en el seed. NO es `"Client"` como
> aproximaba `fxAreaPorTabla` en 01 §3 — ver Divergencias (resuelve la D6 de 01).

---

## 4. KPIs — `cntBodyCli` y las 4 cards

### 4.1 `cntBodyCli`

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `cntToolbarCli.Y + cntToolbarCli.Height` |
| Width / Height | `Parent.Width` / `Parent.Height - Self.Y` |
| Fill | `Color.Transparent` |

### 4.2 Las 4 cards de KPI

`Parent` = `cntBodyCli`. Chrome común de las 4: Fill `ThemeSurface` · BorderColor
`ThemeLine` · BorderThickness `1` · Radius `11` · Y `0` · Height `fxKpiH` · Width
`(Parent.Width - fxGutter*2 - fxGap*3) / 4`. X encadenado:

| Control | X |
|---|---|
| `cntKpiActivosCli` | `fxGutter` |
| `cntKpiRfcCli` | `cntKpiActivosCli.X + cntKpiActivosCli.Width + fxGap` |
| `cntKpiAltasCli` | `cntKpiRfcCli.X + cntKpiRfcCli.Width + fxGap` |
| `cntKpiHoyCli` | `cntKpiAltasCli.X + cntKpiAltasCli.Width + fxGap` |

Hijos (en cada card, `Parent` = su contenedor; molde idéntico **salvo `cntKpiActivosCli`**,
que solo tiene Lab+Val — sin Sub, tal como fija el árbol de arriba):
`lblKpi…LabCli` (X `15` · Y `8` · Width `Parent.Width - 30` · Height `14` ·
`ESTILO-KPI-LAB`), `lblKpi…ValCli` (X `15` · Y `22` · Width `Parent.Width - 30` · Height
`26` · `ESTILO-KPI-VAL`), `lblKpi…SubCli` (X `15` · Y `48` · Width `Parent.Width - 30` ·
Height `14` · `ESTILO-KPI-SUB` — **no aplica a `cntKpiActivosCli`**, solo a las otras 3
cards).

**Textos** (los `Lab` en MAYÚSCULAS por `ESTILO-KPI-LAB`):

| Control | Text |
|---|---|
| `lblKpiActivosLabCli` | `"CLIENTES ACTIVOS"` |
| `lblKpiActivosValCli` | `Text(CountRows(Filter('CH-Tbl-Clients', RecordStatus = 'CH-Ch-RecordStatus'.Active)))` |
| `lblKpiRfcLabCli` | `"CON RFC EN CURSO"` |
| `lblKpiRfcValCli` | `Text(CountRows(Distinct(Filter(colRfcEnCursoCli, !IsBlank(RelatedClient)), Text(RelatedClient.'CH-Tbl-Client'))))` |
| `lblKpiRfcSubCli` | `"solicitudes sin resolver"` |
| `lblKpiAltasLabCli` | `"ALTAS PENDIENTES"` |
| `lblKpiAltasValCli` | `Text(CountRows(Filter(colRfcEnCursoCli, Type = 'Type (CH-Tbl-ClientDataChangeRequests)'.Create && 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted)))` |
| `lblKpiAltasSubCli` | `"esperando aprobación"` |
| `lblKpiHoyLabCli` | `"SOLICITUDES HOY"` |
| `lblKpiHoyValCli` | `Text(CountRows(Filter('CH-Tbl-ClientDataChangeRequests', 'Created On' >= Today())))` |
| `lblKpiHoySubCli` | `CountRows(Filter(colRfcEnCursoCli, 'Created On' >= Today())) & " sin resolver"` |

> El sub del 4º KPI difiere del mockup ("4 esperan a Banca Empresas") — ver Divergencias.
> Los dos `CountRows(Filter('CH-Tbl-…'))` server-side son delegables en Dataverse
> (contadores con filtro simple); el resto opera sobre la colección local.

---

## 5. Grilla de clientes — `cntGrillaCli`

### 5.1 La card

| Propiedad | Valor |
|---|---|
| X / Y | `fxGutter` / `cntKpiActivosCli.Y + cntKpiActivosCli.Height + fxGap` |
| Width / Height | `Parent.Width - fxGutter*2` / `Parent.Height - Self.Y - fxGap` |
| Fill / BorderColor / BorderThickness / Radius | `ThemeSurface` / `ThemeLine` / `1` / `fxCardR` |

### 5.2 Encabezado de columnas (receta 7.4)

Anchos por fracción del ancho de la card (proporciones del mockup `2fr 1fr 1fr 1fr 1.1fr
.9fr`, normalizadas): Cliente `0.29` · CIF BCO `0.14` · CIF COM `0.14` · Estado `0.14` ·
Solicitud `0.16` · Modificado `0.13`. `Parent` = `cntGrillaCli`. Todos los `lblCol…` con
`ESTILO-HEADER-COL`, Y `10`, Height `16`.

> La base de las fracciones es **`(Parent.Width - 32 - fxGap)`**, NO `(Parent.Width - 32)`:
> hay un `fxGap` intencional entre el grupo identificador (Cliente/CIF BCO/CIF COM) y el
> grupo de seguimiento (Estado/Solicitud/Modificado) — ver el `+ fxGap` en `lblColEstadoCli.X`
> abajo. Si la base no lo descuenta, el borde derecho de `lblColModifCli` cae en
> `Parent.Width - 2` en vez de `Parent.Width - 16`, rompiendo el margen simétrico de 16px que
> fija `btnColClienteCli.X = 16`. Con la base corregida, `lblColModifCli.X + lblColModifCli.Width`
> vuelve a dar exactamente `Parent.Width - 16`. Esta misma `X` la hereda el template de fila
> (gotcha 8.14), así que `lblModifFilaCli` (§5.4) queda alineado sin tocar nada ahí.

| Control | Tipo | X | Width | Text | Otras |
|---|---|---|---|---|---|
| `btnColClienteCli` | Botón clásico | `16` | `(Parent.Width - 32 - fxGap) * 0.29` | `"CLIENTE " & If(ctxOrdenAscCli, "▼", "▲")` | Y `6` · Height `24` · Size `7.5` · Bold · Color `ThemeInk2` · Align `Align.Left` · Fill `Color.Transparent` · BorderThickness `0` · HoverFill `ThemeSurface2` · OnSelect `UpdateContext({ctxOrdenAscCli: !ctxOrdenAscCli, ctxPagCli: 1})` |
| `lblColCifBcoCli` | Etiqueta clásica | `btnColClienteCli.X + btnColClienteCli.Width` | `(Parent.Width - 32 - fxGap) * 0.14` | `"CIF BCO"` | — |
| `lblColCifComCli` | Etiqueta clásica | `lblColCifBcoCli.X + lblColCifBcoCli.Width` | `(Parent.Width - 32 - fxGap) * 0.14` | `"CIF COM"` | — |
| `lblColEstadoCli` | Etiqueta clásica | `lblColCifComCli.X + lblColCifComCli.Width + fxGap` | `(Parent.Width - 32 - fxGap) * 0.14` | `"ESTADO"` | — |
| `lblColSolicitudCli` | Etiqueta clásica | `lblColEstadoCli.X + lblColEstadoCli.Width` | `(Parent.Width - 32 - fxGap) * 0.16` | `"SOLICITUD"` | — |
| `lblColModifCli` | Etiqueta clásica | `lblColSolicitudCli.X + lblColSolicitudCli.Width` | `(Parent.Width - 32 - fxGap) * 0.13` | `"MODIFICADO"` | Align `Align.Right` |
| `shpColLineaCli` | Rectángulo clásico | `0` | `Parent.Width` | — | Y `36` · Height `1` · Fill `ThemeLine` |

> `lblColCifBcoCli`/`lblColCifComCli` van SIN `Align.Right`: en el mockup la clase `.num`
> (que aplica `text-align:right`) solo la llevan los `<span>` de DATO (`clRow`) de esas
> columnas — los `<span>` de ENCABEZADO (`clHead`) de "CIF BCO"/"CIF COM" no la llevan y
> quedan alineados a la izquierda por default; únicamente "Modificado" es `.num` también en
> el encabezado. El alineado a la derecha del DATO ya está correcto vía `ESTILO-CELDA-NUM`
> en `lblCifBcoFilaCli`/`lblCifComFilaCli` (§5.4) — no se toca.

### 5.3 `galClientesCli`

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `37` |
| Width / Height | `Parent.Width` / `Parent.Height - 37 - cntPagCli.Height` |
| TemplateSize | `fxRowH` |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| Items | ↓ |
| OnSelect | ↓ |

```powerfx
// Items — orden en memoria (la coleccion ya vino filtrada server-side) + corte de pagina (receta 7.8)
// Skip() NO EXISTE en Power Fx (gotcha 7.8/01-app.md) — el corte real es FirstN(LastN(...)):
// LastN recorta desde el final del resultado ordenado hasta lo que queda tras descartar las
// paginas previas, y el FirstN exterior corta esa cola al tamaño de una pagina.
FirstN(
    LastN(
        Sort(colClientesCli, Name, If(ctxOrdenAscCli, SortOrder.Ascending, SortOrder.Descending)),
        CountRows(colClientesCli) - (ctxPagCli - 1) * ctxPagTamCli
    ),
    ctxPagTamCli
)
```

```powerfx
// OnSelect — la accion de la fila vive ACA (gotcha 8.4)
Set(gvarClienteSel, ThisItem);
Navigate(scrCliente, ScreenTransition.None)
```

### 5.4 Template de fila

`btnFilaCli` primero (queda AL FONDO del z-order — insertalo antes que todo lo demás):
TODAS sus propiedades las fija la **receta 7.4** (X `0` · Y `0` · Width
`Parent.TemplateWidth` · Height `fxRowH` · Fill `Transparent` · HoverFill `ThemeSurface2` ·
PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · OnSelect `Select(Parent)`).

Los 3 labels llevan `OnSelect: =Select(Parent)` (gotcha 8.4); las 2 pills en
`DisplayMode.View` no interceptan el clic (pasa a la fila). Y de labels `0`, Height `fxRowH`
(VerticalAlign Middle del default); pills Y `(fxRowH - 20)/2`, Height `20`.

| Control | Tipo | X | Width | Propiedades |
|---|---|---|---|---|
| `lblNombreFilaCli` | Etiqueta clásica | `btnColClienteCli.X` | `btnColClienteCli.Width` | Text `ThisItem.Name` · Size `9` · Semibold · OnSelect `Select(Parent)` |
| `lblCifBcoFilaCli` | Etiqueta clásica | `lblColCifBcoCli.X` | `lblColCifBcoCli.Width` | Text `ThisItem.CIFBCO` · `ESTILO-CELDA-NUM` · OnSelect `Select(Parent)` |
| `lblCifComFilaCli` | Etiqueta clásica | `lblColCifComCli.X` | `lblColCifComCli.Width` | Text `ThisItem.CIFCOM` · `ESTILO-CELDA-NUM` · OnSelect `Select(Parent)` |
| `pilEstadoFilaCli` | Botón clásico | `lblColEstadoCli.X` | `Len(Self.Text) * 5.4 + 18` | receta 7.3 (DisplayMode `View`, Radius `12`, Height `20`, Size `7.88`, Semibold) + mapa ↓ |
| `pilRfcFilaCli` | Botón clásico | `lblColSolicitudCli.X` | `Len(Self.Text) * 5.4 + 18` | receta 7.3 + Text `"RFC en curso"` · Fill `ThemeWarnSoft` · Color `ThemeWarn` · BorderThickness `0` · Visible ↓ |
| `lblModifFilaCli` | Etiqueta clásica | `lblColModifCli.X` | `lblColModifCli.Width` | `ESTILO-META` · Align `Align.Right` · OnSelect `Select(Parent)` · Text ↓ |
| `shpSepFilaCli` | Rectángulo clásico | `0` | `Parent.TemplateWidth` | receta 7.10 → Y `fxRowH - 1` · Height `1` · Fill `ThemeLineSoft` |

**Mapa de `pilEstadoFilaCli`** (receta 7.3, enum verificado en v1 — pill de fila es botón
nativo, gotcha 8.5):

```powerfx
// Text
Switch(
    ThisItem.RecordStatus,
    'CH-Ch-RecordStatus'.Active,               "Activo",
    'CH-Ch-RecordStatus'.Inactive,             "Inactivo",
    'CH-Ch-RecordStatus'.PendingApproval,      "Pend. aprobación",
    'CH-Ch-RecordStatus'.PendingDeactivation,  "Pend. desactivación",
    "—"
)
// Fill
Switch(
    ThisItem.RecordStatus,
    'CH-Ch-RecordStatus'.Active,               ThemeOkSoft,
    'CH-Ch-RecordStatus'.Inactive,             ThemeLineSoft,
    'CH-Ch-RecordStatus'.PendingApproval,      ThemeWarnSoft,
    'CH-Ch-RecordStatus'.PendingDeactivation,  ThemeWarnSoft,
    ThemeLineSoft
)
// Color
Switch(
    ThisItem.RecordStatus,
    'CH-Ch-RecordStatus'.Active,               ThemeOk,
    'CH-Ch-RecordStatus'.Inactive,             ThemeInk3,
    'CH-Ch-RecordStatus'.PendingApproval,      ThemeWarn,
    'CH-Ch-RecordStatus'.PendingDeactivation,  ThemeWarn,
    ThemeInk3
)
// BorderThickness — SIEMPRE 0, apartándose de la variante "neutro con borde" que sugiere
// la receta 7.3 de la guía 01: el CSS del mockup (.pill y sus 6 tonos, incluida .pMuted
// que es la que usa "Inactivo") no define `border` en NINGUNA variante — verificado en el
// <style> completo del mockup. Sin BorderColor porque el thickness es 0.
0
```

**`pilRfcFilaCli.Visible`** — hay al menos un RFC sin resolver que apunte a este cliente
(`RelatedClient` viene seteado en TODOS los RFC de la v1 salvo las altas de cliente, que no
tienen fila en el maestro — ver Divergencias):

```powerfx
!IsBlank(LookUp(colRfcEnCursoCli, RelatedClient.'CH-Tbl-Client' = ThisItem.'CH-Tbl-Client'))
```

**`lblModifFilaCli.Text`** — fecha relativa como el mockup ("hoy 09:41" / "ayer 16:20" /
"21 ago"), con `FmtHora` (HH de 24 h, gotcha 8.1):

```powerfx
If(
    IsToday(ThisItem.'Modified On'),
    "hoy " & Text(ThisItem.'Modified On', FmtHora),
    IsToday(DateAdd(ThisItem.'Modified On', 1, TimeUnit.Days)),
    "ayer " & Text(ThisItem.'Modified On', FmtHora),
    Text(ThisItem.'Modified On', "dd mmm", "es-ES")
)
```

### 5.5 Estado vacío

| Control | Propiedades |
|---|---|
| `lblVacioGrillaCli` | X `0` · Y `galClientesCli.Y + 40` · Width `Parent.Width` · Height `40` · `ESTILO-VACIO` · Text `"No hay clientes que coincidan con el segmento o la búsqueda."` · Visible `CountRows(colClientesCli) = 0` |

### 5.6 Paginación — `cntPagCli` (receta 7.8)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntPagCli` | Contenedor (manual) | `0` | `Parent.Height - 36` | `Parent.Width` | `36` | Fill `ThemeSurface2` |
| `lblResumenPagCli` | Etiqueta clásica | `16` | `0` | `Parent.Width * 0.5` | `Parent.Height` | `ESTILO-META` · Text ↓ |
| `btnPagSigCli` | Botón clásico | `Parent.Width - 16 - Self.Width` | `(Parent.Height - 28)/2` | `28` | `28` | `ESTILO-BTN-CHICO` · Radius `6` · Text `"›"` · DisplayMode ↓ · OnSelect ↓ |
| `btnPagAntCli` | Botón clásico | `btnPagSigCli.X - 6 - Self.Width` | `(Parent.Height - 28)/2` | `28` | `28` | `ESTILO-BTN-CHICO` · Radius `6` · Text `"‹"` · DisplayMode `If(ctxPagCli > 1, DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `UpdateContext({ctxPagCli: ctxPagCli - 1})` |

```powerfx
// lblResumenPagCli.Text
CountRows(colClientesCli) & " clientes · página " & ctxPagCli & " de "
    & Max(1, RoundUp(CountRows(colClientesCli) / ctxPagTamCli, 0))
// btnPagSigCli.DisplayMode
If(
    ctxPagCli < Max(1, RoundUp(CountRows(colClientesCli) / ctxPagTamCli, 0)),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
// btnPagSigCli.OnSelect
UpdateContext({ctxPagCli: ctxPagCli + 1})
```

---

## 6. Panel lateral — "Nueva solicitud de alta de cliente" (CL2)

Chrome completo por **receta 7.7** con `<ctxPanelAbierto>` = `ctxPanelAltaCli`:

| Control | Receta 7.7 | Ajustes de esta pantalla |
|---|---|---|
| `shpVeloPanCli` | `shpVeloPanel` | Visible `ctxPanelAltaCli` |
| `btnVeloClicPanCli` | `btnVeloClicPanel` | Visible `ctxPanelAltaCli` · OnSelect `UpdateContext({ctxPanelAltaCli: false})` |
| `cntPanelAltaCli` | `cnt…Panel` | Visible `ctxPanelAltaCli` |
| `cntPanelAltaHeadCli` | `cnt…PanelHead` | contenido ↓ |
| `cntPanelAltaBodyCli` | `cnt…PanelBody` | Sin ajuste — usa tal cual la fórmula dinámica de la receta 7.7 (`Height: Parent.Height - Self.Y - 64`); el contenido apilado de §6.2/6.3 (~510-520 px) entra cómodo dentro de esa altura |
| `cntPanelAltaFootCli` | `cnt…PanelFoot` | contenido ↓ |

### 6.1 Head (`Parent` = `cntPanelAltaHeadCli`)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblPanelTitCli` | Etiqueta clásica | `20` | `10` | `Parent.Width - 70` | `20` | Text `"Nueva solicitud de alta de cliente"` · Size `11` · Bold |
| `lblPanelSubCli` | Etiqueta clásica | `20` | `30` | `Parent.Width - 70` | `30` | Text `"Se creará una solicitud tipo Alta que debe aprobar otro miembro de Banca de Empresas. El cliente aparecerá en el maestro cuando la solicitud se apruebe y aplique."` · Size `8.25` · Color `ThemeInk3` · `Wrap: true` (gotcha 8.15) |
| `icoPanelXCli` | Icono clásico | `Parent.Width - 34` | `12` | `18` | `18` | Icon `Icon.Cancel` · Color `ThemeInk3` · HoverColor `ThemeInk` · OnSelect `UpdateContext({ctxPanelAltaCli: false})` |

### 6.2 Body — campos (`Parent` = `cntPanelAltaBodyCli`; molde de campo de receta 7.7)

Todos los inputs: Fill `ThemeSurface` · BorderColor `ThemeLine` · Radius `7` · X `20` ·
Width `Parent.Width - 40` · Size `9`. Labels con `ESTILO-CAMPO-LABEL`, X `20`, Width
`Parent.Width - 40`, Height `16`.

| Control | Tipo | Y | Height | Otras |
|---|---|---|---|---|
| `lblNombreLabPanCli` | Etiqueta clásica | `8` | `16` | Text `"Nombre del cliente *"` |
| `inpNombrePanCli` | Entrada de texto clásica | `lblNombreLabPanCli.Y + lblNombreLabPanCli.Height + 4` | `34` | `Mode: TextMode.SingleLine` · HintText `"Razón social completa"` · MaxLength `100` |
| `lblCifBcoLabPanCli` | Etiqueta clásica | `inpNombrePanCli.Y + inpNombrePanCli.Height + 13` | `16` | Text `"CIF BCO *"` |
| `inpCifBcoPanCli` | Entrada de texto clásica | `lblCifBcoLabPanCli.Y + lblCifBcoLabPanCli.Height + 4` | `34` | `Mode: TextMode.SingleLine` · MaxLength `100` |
| `lblCifComLabPanCli` | Etiqueta clásica | `inpCifBcoPanCli.Y + inpCifBcoPanCli.Height + 13` | `16` | Text `"CIF COM *"` |
| `inpCifComPanCli` | Entrada de texto clásica | `lblCifComLabPanCli.Y + lblCifComLabPanCli.Height + 4` | `34` | `Mode: TextMode.SingleLine` · MaxLength `100` · BorderColor ↓ |
| `lblCifComErrPanCli` | Etiqueta clásica | `inpCifComPanCli.Y + inpCifComPanCli.Height + 4` | `28` | `ESTILO-CAMPO-ERROR` · `Wrap: true` · Text `"El CIF COM debe tener el formato C-#####. Corregilo para continuar."` · Visible ↓ |
| `lblMotivoLabPanCli` | Etiqueta clásica | `lblCifComErrPanCli.Y + lblCifComErrPanCli.Height + 6` | `16` | Text `"Motivo de la solicitud *"` |
| `inpMotivoPanCli` | Entrada de texto clásica | `lblMotivoLabPanCli.Y + lblMotivoLabPanCli.Height + 4` | `56` | `Mode: TextMode.MultiLine` · HintText `"Ej: cliente nuevo de Banca de Empresas, convenio firmado…"` |

```powerfx
// inpCifComPanCli.BorderColor — el error NO borra lo tipeado (validacion no destructiva, bug U5 de v1)
If(
    Len(Trim(inpCifComPanCli.Text)) > 0 && !IsMatch(Trim(inpCifComPanCli.Text), "C-\d{5}"),
    ThemeDanger,
    ThemeLine
)
// lblCifComErrPanCli.Visible
Len(Trim(inpCifComPanCli.Text)) > 0 && !IsMatch(Trim(inpCifComPanCli.Text), "C-\d{5}")
```

> El hueco del error queda SIEMPRE reservado (el layout manual no refluye) — con el error
> oculto se ve un respiro extra de 28 px entre CIF COM y Motivo. Aproximación honesta.

### 6.3 Body — resumen y banner

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblResumenSecPanCli` | Etiqueta clásica | `20` | `inpMotivoPanCli.Y + inpMotivoPanCli.Height + 13` | `Parent.Width - 40` | `16` | `ESTILO-SECCION` · Text `"RESUMEN DE LA SOLICITUD (3 CAMBIOS)"` |
| `cntResumenPanCli` | Contenedor (manual) | `20` | `lblResumenSecPanCli.Y + lblResumenSecPanCli.Height + 4` | `Parent.Width - 40` | `104` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `9` |
| `lblResumenHeadPanCli` | Etiqueta clásica | `0` | `0` | `Parent.Width` | `28` | Fill `ThemeSurface2` · `ESTILO-HEADER-COL` · Text `"CAMPO → VALOR PROPUESTO"` · PaddingLeft `14` |
| `lblResumenNombrePanCli` | Etiqueta clásica | `14` | `32` | `Parent.Width - 28` | `22` | Size `9` · Text `"Nombre → " & Trim(inpNombrePanCli.Text)` |
| `lblResumenCifBcoPanCli` | Etiqueta clásica | `14` | `54` | `Parent.Width - 28` | `22` | Size `9` · Text `"CIFBCO → " & Trim(inpCifBcoPanCli.Text)` |
| `lblResumenCifComPanCli` | Etiqueta clásica | `14` | `76` | `Parent.Width - 28` | `22` | Size `9` · Text ↓ · Color ↓ |
| `cntBannerPanCli` | Contenedor (manual) | `20` | `cntResumenPanCli.Y + cntResumenPanCli.Height + 13` | `Parent.Width - 40` | `lblBannerPanCli.Height + 18` | receta 7.11, tono info |
| `shpBannerPanCli` | Rectángulo clásico | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeInfoSoft` (receta 7.11) |
| `lblBannerPanCli` | Etiqueta clásica | `13` | `9` | `Parent.Width - 26` | *(AutoHeight)* | `AutoHeight: true` · `Wrap: true` · Size `8.63` · Color `ThemeInfo` · Text `"Sucursales, correos y cuentas se agregan desde la ficha del cliente una vez aprobada el alta — cada una genera su propia solicitud."` |

```powerfx
// lblResumenCifComPanCli.Text
"CIFCOM → " & Trim(inpCifComPanCli.Text)
    & If(
        Len(Trim(inpCifComPanCli.Text)) > 0 && !IsMatch(Trim(inpCifComPanCli.Text), "C-\d{5}"),
        "  (inválido)",
        ""
    )
// lblResumenCifComPanCli.Color
If(
    Len(Trim(inpCifComPanCli.Text)) > 0 && !IsMatch(Trim(inpCifComPanCli.Text), "C-\d{5}"),
    ThemeDanger,
    ThemeInk
)
```

### 6.4 Foot (`Parent` = `cntPanelAltaFootCli`)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnCrearPanCli` | Botón clásico | `Parent.Width - 20 - Self.Width` | `(Parent.Height - 32)/2` | `190` | `32` | `ESTILO-BTN-PRIMARIO` · Text `If(ctxGuardandoCli, "Enviando…", "Crear y enviar solicitud")` · DisplayMode ↓ · OnSelect ↓ |
| `btnCancelarPanCli` | Botón clásico | `btnCrearPanCli.X - 8 - Self.Width` | `(Parent.Height - 32)/2` | `110` | `32` | `ESTILO-BTN-FANTASMA` · Text `"Cancelar"` · OnSelect `UpdateContext({ctxPanelAltaCli: false})` |

```powerfx
// btnCrearPanCli.DisplayMode — deshabilitado real, no oculto (mockup CL2: btnDis)
If(
    !ctxGuardandoCli
    && Len(Trim(inpNombrePanCli.Text)) > 0
    && Len(Trim(inpCifBcoPanCli.Text)) > 0
    && IsMatch(Trim(inpCifComPanCli.Text), "C-\d{5}")
    && Len(Trim(inpMotivoPanCli.Text)) > 0,
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

> 🧨 **Hallazgo crítico de backend (cambia el diseño frente al mockup)**: `ChangeRequestLineGuard.Evaluate`
> (`src/backend/CashHub.Validation/Security/ChangeRequestLineGuard.cs:93`) **bloquea crear
> líneas contra un RFC que no esté en estado Draft**. Si la cabecera naciera directo en
> `Submitted` (como sugería una versión anterior de esta guía, calcada del botón único "Crear
> y enviar solicitud" del mockup CL2), el guard rechazaría la creación de las 3 líneas con
> "Solo se pueden crear, editar o borrar líneas de un RFC que esté en estado Draft". La única
> secuencia que el backend acepta es **(1) cabecera en Draft → (2) las 3 líneas mientras está
> Draft → (3) cabecera a Submitted** (`ChangeRequestApprovalGuard.Evaluate` sí permite la
> transición `Draft→Submitted` sin restricciones) — mismo hallazgo y misma secuencia que
> `03-pantalla-cliente.md` §10.4, que además avisa explícitamente que aplica a esta guía. El
> usuario ve un solo clic; la app hace 4 escrituras encadenadas.

**`btnCrearPanCli.OnSelect`** — cabecera en Draft + 3 líneas + cabecera a Submitted + refresh
del badge, con error real y spinner que SIEMPRE se libera (corrige L7/U4 y el bug L9 del
plural en singular de la v1). Los literales `"Nombre"`, `"CIFBCO"`, `"CIFCOM"` y
`"sanic_chtblclient"` están verificados contra el seed
(`data/seed/10-clienttablelogicalfieldnames.json`) y contra los `LookUp()` del fuente v1 —
deben coincidir EXACTO:

```powerfx
UpdateContext({ctxGuardandoCli: true});
IfError(
    With(
        {
            // 1. Cabecera en Draft — el guard de lineas lo exige para el paso 2.
            rfc: Patch(
                'CH-Tbl-ClientDataChangeRequests',
                Defaults('CH-Tbl-ClientDataChangeRequests'),
                {
                    Type: 'Type (CH-Tbl-ClientDataChangeRequests)'.Create,
                    'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,
                    Reason: Trim(inpMotivoPanCli.Text),
                    TargetTable: "sanic_chtblclient",
                    TargetTableDisplayName: "Clientes",
                    TargetTablePluralName: "sanic_chtblclients"
                }
            )
        },
        // 2. Las 3 lineas, con la cabecera TODAVIA en Draft.
        // Linea 1 — Nombre
        Patch(
            'CH-Tbl-ClientDataChangeRequestLines',
            Defaults('CH-Tbl-ClientDataChangeRequestLines'),
            {
                ChangeRequestID: rfc,
                'Field Definition': LookUp(
                    'CH-Tbl-ClientTableLogicalFieldNames',
                    Name = "Nombre" && Table = "sanic_chtblclient"
                ),
                NewValue: Trim(inpNombrePanCli.Text),
                ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft
            }
        );
        // Linea 2 — CIFBCO
        Patch(
            'CH-Tbl-ClientDataChangeRequestLines',
            Defaults('CH-Tbl-ClientDataChangeRequestLines'),
            {
                ChangeRequestID: rfc,
                'Field Definition': LookUp(
                    'CH-Tbl-ClientTableLogicalFieldNames',
                    Name = "CIFBCO" && Table = "sanic_chtblclient"
                ),
                NewValue: Trim(inpCifBcoPanCli.Text),
                ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft
            }
        );
        // Linea 3 — CIFCOM
        Patch(
            'CH-Tbl-ClientDataChangeRequestLines',
            Defaults('CH-Tbl-ClientDataChangeRequestLines'),
            {
                ChangeRequestID: rfc,
                'Field Definition': LookUp(
                    'CH-Tbl-ClientTableLogicalFieldNames',
                    Name = "CIFCOM" && Table = "sanic_chtblclient"
                ),
                NewValue: Trim(inpCifComPanCli.Text),
                ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft
            }
        );
        // 3. Cabecera a Submitted — unica transicion humana que ChangeRequestApprovalGuard
        //    permite tras Draft (misma secuencia que 03 §10.4).
        Patch(
            'CH-Tbl-ClientDataChangeRequests',
            rfc,
            {'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted}
        );
        // 4. Refresh del badge/KPIs — el mockup lo exige "al volver de crear la solicitud"
        ClearCollect(
            colRfcEnCursoCli,
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
                || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
                || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved
            )
        );
        UpdateContext({ctxPanelAltaCli: false});
        Notify(
            "Solicitud " & rfc.Name & " creada y enviada a aprobación.",
            NotificationType.Success
        )
    ),
    // Error REAL, nunca generico (bug U4 de v1); el panel queda abierto con lo tipeado
    Notify(
        "No se pudo crear la solicitud: " & FirstError.Message,
        NotificationType.Error
    )
);
UpdateContext({ctxGuardandoCli: false})  // fuera del IfError: el "spinner" se libera SIEMPRE (bug L7 de v1)
```

> La cabecera nace en **Draft** y pasa a **Submitted** recién después de que las 3 líneas se
> crearon con éxito (paso 3 de arriba) — "crear y enviar" sigue siendo un solo clic para el
> usuario, pero ya no un solo `Patch` para el backend (ver hallazgo de arriba). `Name` de la
> cabecera es autonumber (`RFC-{SEQNUM}`) — no se setea. `TargetRecordID` y `RelatedClient`
> quedan en blanco: el cliente aún no existe (ver Divergencias). El enum de la LÍNEA es
> `'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'` — sin espacio, distinto del de la
> cabecera (gotcha 8.8). Las 3 líneas quedan en `ChangeStatus.Draft` aunque la cabecera ya esté
> en `Submitted` (mismo comportamiento que 03 §10.4 — `ResolveChangeRequestPlugin.RetrieveLines`
> trae TODAS las líneas del RFC sin filtrar por su `ChangeStatus`, así que esto no bloquea la
> aplicación posterior).

---

> ## Notas
>
> **Dependencias de backend.** Ninguna nueva: esta pantalla solo hace Patch a las 2 tablas
> RFC (permitido para Contributor a nivel Basic) y lecturas delegables. La Custom API
> `sanic_CHResolveChangeRequest` NO se invoca acá. El estado `Approved` se incluye en
> `colRfcEnCursoCli` por robustez: con la Custom API síncrona es transitorio, pero si el
> backend aún corre con los flujos v1 puede existir la ventana "aprobada sin aplicar".
>
> **Divergencias declaradas vs mockup (CL1/CL2):**
>
> | # | Divergencia | Por qué |
> |---|---|---|
> | DC1 | La fila "Café Soluble · Pend. aprobación · **Alta enviada**" del mockup NO es representable | El cliente NO existe en `CH-Tbl-Clients` hasta que el RFC de alta se aplica: el materializador crea el registro recién al aprobar, con `RecordStatus = Active` directo (verificado en `docs/procesos/01-administracion-datos-clientes.md` §2.4). Las altas en curso se ven en el KPI "Altas pendientes" y en `scrSolicitudes`; el badge `pInfo "Alta enviada"` no se construye. El subtítulo del panel se ajustó en consecuencia ("El cliente aparecerá en el maestro cuando la solicitud se apruebe y aplique" en vez de "nace en estado Pendiente de aprobación"). |
> | DC2 | El segmento "Pendientes" puede estar vacío en producción | `PendingApproval`/`PendingDeactivation` existen en el option set y el filtro es válido, pero hoy ningún proceso automatizado siembra esos estados (mismo doc, §2.4: "verificar en vivo"). Se construye igual: si el backend los usa, la UI ya está lista. |
> | DC3 | Literales de tabla: esta guía usa `"sanic_chtblclient"` / `"sanic_chtblclients"`, no `"Client"` | Verificado en el fuente v1 (`varTargetTable`, `LookUp(colClientDataPermissions, Name = "sanic_chtblclient")`) y en el seed de permisos/catálogo. **Esto resuelve la D6 de la guía 01**: `fxAreaPorTabla` de 01 §3 debe corregirse a `{Tabla: "sanic_chtblclient", …}`, `{Tabla: "sanic_chtblclientbranch", …}`, `{Tabla: "sanic_chtblclientemail", …}`, `{Tabla: "sanic_chtblclientaccount", …}` antes de pegar `App.Formulas`. |
> | DC4 | Enum de estado de registro: `'CH-Ch-RecordStatus'.Active`, no `'RecordStatus (CH-Tbl-Clients)'.Active` | Es el nombre REAL que Power Fx le dio al option set global en el fuente v1 que funcionó. La guía 01 §7.3 ya avisaba "si IntelliSense difiere, el suyo manda" — acá se usa el verificado. |
> | DC5 | Sub del KPI "Solicitudes hoy": `"N sin resolver"` en vez de "4 esperan a Banca Empresas" | El desglose por área exigiría mapear `TargetTable`→área por fila con `fxAreaPorTabla` (aún por corregir, DC3) para un subtexto — costo/fragilidad sin valor decisorio. |
> | DC6 | Paginación con `‹` / `›` + "página X de Y", sin botones numerados `1 2 3 … 7` | El número de páginas es dinámico; una botonera numerada exigiría N botones estáticos con visibilidad calculada — ruido de mantenimiento sin ganancia funcional. La receta 7.8 admite ambas formas. |
> | DC7 | Resumen del panel sin negrita parcial; asterisco de requerido sin color propio | Una Etiqueta clásica tiene UN solo FontWeight y UN solo Color — el mockup mezcla pesos/colores dentro de la misma línea. Partirlo en 2 labels por línea (6 extra) no paga. |
> | DC8 | Validación `C-#####` del CIF COM es regla del MOCKUP, no del modelo | En el esquema `sanic_cifcom` es texto libre (100). Se implementa la regla del mockup (`IsMatch(txt, "C-\d{5}")`) — **a validar con negocio** si el formato real admite otra cantidad de dígitos; ajustar la regex y el mensaje en un solo lugar cada uno. |
> | DC9 | Sin resaltado persistente de fila seleccionada (el `gSel` del mockup) | En esta pantalla la fila navega a `scrCliente` — no hay panel "del seleccionado" que justifique la receta 7.5. El feedback visual es el HoverFill/PressedFill del overlay 7.4. |
> | DC10 | "Consulta acotada" del badge = acotada por ESTADO (solo RFCs sin resolver), no por el conjunto visible de filas | Un `in` sobre los IDs visibles no delega. El volumen de RFCs abiertos es chico por naturaleza de negocio (se resuelven); se trae la fila completa (sin `ShowColumns`) para evitar el pantano de versiones display-name/logical-name de `ShowColumns` en Studio. |
> | DC11 | El botón "+ Nuevo cliente" no acorta su texto en anchos chicos (el RSP muestra "+ Nuevo") | Texto condicional por ancho es posible (`If(App.Width < 900, …)`) pero multiplica casos de prueba; ancho fijo 150 entra hasta ~840 px. |
> | DC12 | El segmentado de estado (`cntSegEstadoCli`) mantiene los 4 botones fijos (Activos/Inactivos/Pendientes/Todos) en cualquier ancho; el mockup RSP (~840px) los reduce a 2 (Activos/Todos) | Colapsar a 2 exigiría una segunda fórmula de `Visible`/ancho para cada botón por debajo de un breakpoint, más el recálculo en cadena de `cntSegEstadoCli.Width` — mismo criterio costo/mantenimiento que DC6/DC11. La promesa de 01 §1 ("sin recortes ni superposiciones") se sostiene: el sistema de fórmulas proporcionales no corta texto ni superpone controles en anchos menores; solo no imita la reducción puntual de 4→2 botones del mockup. |
> | DC13 | Las 4 cards de KPI mantienen labels largos en MAYÚSCULAS y sus 3 sub-labels (`lblKpi…SubCli`) siempre visibles en cualquier ancho; el mockup RSP (~1080px) abrevia los labels ("Activos"/"RFC en curso"/"Altas pend."/"Hoy") y oculta los 3 `sub` | Mismo criterio que DC12: exigiría lógica condicional por `App.Width` en 4 `Text` + 3 `Visible`, sin cambiar el número mostrado (el dato decisorio del KPI no varía). Aproximación honesta, igual que DC11. |
> | DC14 | El encabezado y las filas de `cntGrillaCli` siempre reparten las 6 columnas por fracción de ancho; el mockup RSP (~840px) OCULTA directamente "CIF COM" y "Modificado" (`grid-template-columns:2fr 1fr 1fr .9fr`, 4 columnas) en vez de encogerlas | Ocultar columnas exigiría redistribuir las fracciones de las 4 restantes por debajo de un breakpoint (`Visible` + recálculo en cadena de `X`/`Width` de 4 controles de encabezado + 4 de fila) — mismo criterio costo/mantenimiento que DC6/DC11/DC12/DC13. El reparto proporcional actual sigue sin cortar texto ni superponer columnas en anchos menores; solo no imita la ocultación puntual del mockup. |
> | DC15 | `btnColClienteCli` (encabezado "CLIENTE ▼") usa un único `Color ThemeInk2` para todo el `Text`; el mockup pinta la flecha `▼` en `--accent` (9px) distinto del texto "Cliente" (`--ink2`, 10px) vía `<span class="arr">` propio | Un Botón clásico tiene un solo `Color` para todo su `Text` — no admite mezclar colores dentro del mismo control, misma limitación de fondo que DC7 (Etiqueta con un solo `Color`/`FontWeight`). Partir el control en label+ícono superpuestos (2 controles en vez de 1) no paga para un acento de 9px. |
>
> **Aproximaciones honestas:** "Modificado" usa `'Modified On'` del registro — cualquier
> update del sistema (p. ej. la aplicación de un RFC) lo mueve, no solo cambios de negocio;
> no existe un sello de negocio dedicado. Los contadores del segmentado y el KPI 1 disparan
> una consulta `CountRows` delegable cada uno por evaluación — con la tabla indexada es
> barato, pero son 4-5 llamadas por visita. `colClientesCli` materializa como máximo el
> límite de filas de datos (2000): con más clientes que eso en un mismo segmento, la
> paginación queda corta y hay que afinar con la búsqueda (declarado, no oculto).

---

## Correcciones post-build 2026-08-25

Durante el debugging en Studio se corrigió una fórmula que esta guía mostraba en su versión
vieja (rota). El fuente REAL (`scrClientes.fx.yaml`) mandó:

| # | Qué estaba mal | Por qué (fuente real) |
|---|---|---|
| PB1 | `galClientesCli.Items` (§5.3) usaba `FirstN(Skip(...), ...)` | `Skip()` no existe en Power Fx — no compila. El fuente real usa `FirstN(LastN(Sort(colClientesCli, ...), CountRows(colClientesCli) - (ctxPagCli-1)*ctxPagTamCli), ctxPagTamCli)` (ver 01 §7.8) |

---

## Checklist de salida

- [ ] Árbol completo: **78 controles** insertados y renombrados EXACTO como el árbol (pasada
      1: insertar+renombrar; pasada 2: fórmulas — orden de construcción de 01).
- [ ] `btnFilaCli` insertado PRIMERO en el template (queda al fondo del z-order; overlay
      receta 7.4) y `shpSepFilaCli` en Y `fxRowH - 1`.
- [ ] `OnVisible` pegado completo; al entrar: segmento "Activos" activo, página 1, panel
      cerrado, búsqueda vacía.
- [ ] Los 4 segmentos filtran server-side y muestran contador; "Todos" lista sin filtro de
      estado.
- [ ] Búsqueda: tipear parte de un nombre/CIF y salir del campo (o pausa con DelayOutput)
      re-filtra; sin advertencias de delegación en App Checker (si las hay, aplicar la
      alternativa StartsWith de §3.3).
- [ ] Orden: clic en "CLIENTE ▼" invierte el orden y vuelve a página 1.
- [ ] Paginación: `‹`/`›` deshabilitados en los extremos; resumen "N clientes · página X de Y"
      consistente con `ctxPagTamCli = 50`.
- [ ] Fila: clic en CUALQUIER punto (incluido sobre los labels) setea `gvarClienteSel` y
      navega a `scrCliente` (gotcha 8.4 verificado).
- [ ] Pills de estado con los 4 mapas (Activo/Inactivo/Pend. aprobación/Pend. desactivación);
      NINGUNA lleva borde (SIN borde en "Inactivo" tampoco — se aparta de la variante
      "neutro con borde" de la receta 7.3 de la guía 01, ver §5.4).
- [ ] Badge "RFC en curso" visible en clientes con RFC sin resolver; crear una solicitud
      de edición desde `scrCliente` y volver → el badge aparece sin recargar la app.
- [ ] "+ Nuevo cliente" solo visible para Contributor de `sanic_chtblclient`
      (`colMisPermisos`).
- [ ] Panel CL2: botón "Crear y enviar" deshabilitado hasta que Nombre/CIFBCO/Motivo tengan
      texto y CIFCOM cumpla `C-#####`; el error de CIFCOM NO borra lo tipeado.
- [ ] Crear un alta de prueba: verificar en Dataverse la cabecera (Type Create, Change
      Status Submitted, TargetTable `sanic_chtblclient`, TargetTablePluralName
      `sanic_chtblclients`) y las **3 líneas** con `Field Definition` resuelto (ninguna con
      lookup en blanco) y ChangeStatus de línea Draft (el guard de líneas exigió crearlas
      con la cabecera todavía en Draft — ver hallazgo crítico de §6).
- [ ] Simular un error (p. ej. desconectar red / quitar permiso en DEV): el Notify muestra
      `FirstError.Message` real y el botón vuelve de "Enviando…" a habilitado.
- [ ] KPIs coherentes entre sí y con la bandeja de `scrSolicitudes` (Altas pendientes =
      RFCs Create+Submitted).
- [ ] App Checker en cero errores; cero advertencias de delegación en esta pantalla.
