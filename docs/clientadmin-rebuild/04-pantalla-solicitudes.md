# Guía 04 — scrSolicitudes: bandeja de solicitudes con prioridad del aprobador

Fuente de diseño: vista **SO1** del mockup (`docs/clientadmin-rebuild/mockup-clientadmin.html`).
Convenciones, estilos `ESTILO-*`, recetas `7.x`, gotchas `8.x` y tokens `Theme*`/`fx*`: guía
`01-app.md`. Sufijo de esta pantalla: **`Sol`**.

**Propósito.** Bandeja de RFCs con tres segmentos — **Para mi decisión** (default del
aprobador: RFCs Enviadas cuya tabla objetivo tiene al usuario como Approver y que NO creó
él), **Mías** (creadas por el usuario, en cualquier estado) y **Todas**. Búsqueda delegable
por N° de solicitud o cliente, filtro por tabla objetivo, KPIs de la cola, grilla con badges
de tipo y estado, paginación. La fila clicable navega a `scrSolicitud`.

**CONTRATO DE ENTRADA** (quién lo setea: `App.OnStart`, guía 01 §4):

| Variable/colección | Contenido | La setea |
|---|---|---|
| `gvarUser` | Registro de `Users` del usuario actual | `App.OnStart` |
| `colMisPermisos` | Filas de `CH-Tbl-ClientDataChangePermissions` donde el usuario es miembro del grupo AAD | `App.OnStart` |
| `gvarRolTexto` | Texto del chip de rol del header | `App.OnStart` |

Esta pantalla NO exige `gvarSolicitudSel` ni `gvarClienteSel` al entrar — se llega desde el
header segmentado de cualquier pantalla.

**CONTRATO DE SALIDA** (qué setea esta pantalla al navegar):

| Variable | Contenido | Cuándo |
|---|---|---|
| `gvarSolicitudSel` | El RFC de la fila clicada (registro tomado de `colSolicitudesSol` — **es una copia materializada**; `scrSolicitud` debe re-hacer `LookUp` en su `OnVisible` si necesita datos frescos del servidor) | `galSolicitudesSol.OnSelect` |
| `gvarClienteSel` | `gvarSolicitudSel.RelatedClient` (puede ser `Blank()` si el RFC no tiene cliente relacionado) | `galSolicitudesSol.OnSelect` |

Esta pantalla **no escribe** en ninguna tabla y **no llama** la Custom API
`sanic_CHResolveChangeRequest` — las decisiones viven en `scrSolicitud` (guía 05).

---

## Árbol de controles

```
scrSolicitudes
├── cmpAppHeaderSol            Componente (cmpAppHeader, receta 7.2)
├── cntToolbarSol              Contenedor (manual)      toolbar bajo el header
│   ├── cntSegSol                  Contenedor (manual)  pista del segmentado (3 botones)
│   │   ├── btnSegMiSol                Botón clásico    "Para mi decisión (N)"
│   │   ├── btnSegMiasSol              Botón clásico    "Mías (N)"
│   │   └── btnSegTodasSol             Botón clásico    "Todas"
│   ├── cntBusqSol                 Contenedor (manual)  buscador (receta 7.9)
│   │   ├── icoLupaBusqSol             Icono clásico    Icon.Search
│   │   └── inpBusqSol                 Entrada de texto clásica
│   ├── drpTablaSol                Menú desplegable clásico   filtro por tabla objetivo
│   └── btnRefrescarSol            Botón clásico        "↻ Actualizar"
├── cntBodySol                 Contenedor (manual)      cuerpo (KPIs + grilla)
│   ├── cntKpiParaMiSol            Contenedor (manual)  KPI "Esperan mi decisión"
│   │   ├── lblKpiParaMiLabSol         Etiqueta clásica
│   │   ├── lblKpiParaMiValSol         Etiqueta clásica
│   │   └── lblKpiParaMiSubSol         Etiqueta clásica
│   ├── cntKpiResueltasSol         Contenedor (manual)  KPI "Resueltas hoy"
│   │   ├── lblKpiResueltasLabSol      Etiqueta clásica
│   │   ├── lblKpiResueltasValSol      Etiqueta clásica
│   │   └── lblKpiResueltasSubSol      Etiqueta clásica
│   ├── cntKpiFallidasSol          Contenedor (manual)  KPI "Fallidas (7 días)"
│   │   ├── lblKpiFallidasLabSol       Etiqueta clásica
│   │   ├── lblKpiFallidasValSol       Etiqueta clásica
│   │   └── lblKpiFallidasSubSol       Etiqueta clásica
│   └── cntGrillaSol               Contenedor (manual)  card de la grilla
│       ├── shpGrillaHeadSol           Rectángulo clásico   banda del encabezado
│       ├── lblColNumSol               Etiqueta clásica     "SOLICITUD"
│       ├── lblColClienteSol           Etiqueta clásica     "CLIENTE"
│       ├── lblColTablaSol             Etiqueta clásica     "TABLA"
│       ├── lblColTipoSol              Etiqueta clásica     "TIPO"
│       ├── lblColEstadoSol            Etiqueta clásica     "ESTADO"
│       ├── lblColCreadorSol           Etiqueta clásica     "CREADA POR"
│       ├── lblColFechaSol             Etiqueta clásica     "CREADA ▼/▲" (clic = orden)
│       ├── shpGrillaLineaSol          Rectángulo clásico   línea bajo el encabezado
│       ├── galSolicitudesSol          Galería vertical en blanco
│       │   ├── btnFilaSol                 Botón clásico    overlay clicable (FONDO del z-order)
│       │   ├── shpFilaSelSol              Rectángulo clásico   fondo de fila seleccionada (7.5)
│       │   ├── shpFilaSelBarraSol         Rectángulo clásico   barra izquierda de selección (7.5)
│       │   ├── lblFilaNumSol              Etiqueta clásica     N° de RFC
│       │   ├── lblFilaClienteSol          Etiqueta clásica     cliente
│       │   ├── lblFilaTablaSol            Etiqueta clásica     tabla objetivo
│       │   ├── pilFilaTipoSol             Botón clásico        badge de tipo (7.3, nativo — gotcha 8.5)
│       │   ├── pilFilaEstadoSol           Botón clásico        badge de estado (7.3, nativo — gotcha 8.5)
│       │   ├── lblFilaCreadorSol          Etiqueta clásica     creador
│       │   ├── lblFilaFechaSol            Etiqueta clásica     fecha relativa
│       │   └── shpFilaSepSol              Rectángulo clásico   separador (receta 7.10)
│       ├── lblVacioSol                Etiqueta clásica     estado vacío
│       └── cntPagSol                  Contenedor (manual)  paginación (receta 7.8)
│           ├── lblResumenPagSol           Etiqueta clásica
│           ├── btnPagPrevSol              Botón clásico    "‹"
│           └── btnPagSigSol               Botón clásico    "›"
```

Total: 51 controles + la pantalla (1 header + 10 toolbar + 1 body + 12 KPIs + 27
grilla/paginación).

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |

### 1.1 `OnVisible` — COMPLETO, listo para pegar

```powerfx
// ============ 1) Estado a default (gotcha 8.3: nada arrastra la visita anterior) ============
Reset(inpBusqSol);
Reset(drpTablaSol);
UpdateContext({
    ctxFilasPag: 25,
    ctxOrdenDesc: true,
    ctxPagina: 1,
    ctxBusqueda: "",
    ctxBusqClienteGuid: GUID("00000000-0000-0000-0000-000000000000"),
    ctxTabla: "",
    // ¿De qué tablas objetivo soy Approver? (colMisPermisos viene de App.OnStart; los
    // literales son el LOGICAL NAME de la entidad — D6 de 01 RESUELTA, ver 02 DC3 / 03
    // hallazgo crítico #3)
    ctxApruebaCliente:  CountRows(Filter(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = "sanic_chtblclient")) > 0,
    ctxApruebaSucursal: CountRows(Filter(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = "sanic_chtblclientbranch")) > 0,
    ctxApruebaCorreo:   CountRows(Filter(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = "sanic_chtblclientemail")) > 0,
    ctxApruebaCuenta:   CountRows(Filter(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = "sanic_chtblclientaccount")) > 0
});
// Segmento default: "Para mi decisión" si soy Approver de algo; si no, "Mías".
// (Va en un UpdateContext SEPARADO: un UpdateContext no puede leer claves que setea él mismo.)
UpdateContext({
    ctxSegmento: If(ctxApruebaCliente || ctxApruebaSucursal || ctxApruebaCorreo || ctxApruebaCuenta, "mi", "mias")
});

// ============ 2) BLOQUE-COMPLETO (cola + contadores + grilla) ============
// --- 2a. Cola "para mi decisión" — SIEMPRE se materializa (alimenta KPI 1, el contador del
//     segmento y la grilla del segmento "mi"). Filtro 100% delegable: picklist = opción,
//     desigualdad de GUID del lookup, OR de igualdades sobre TargetTable con booleanos
//     constantes de contexto. Sin AddColumns antes del filtro (gotcha 8.12).
ClearCollect(
    colParaMiSol,
    Sort(
        Filter(
            'CH-Tbl-ClientDataChangeRequests',
            'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
            && 'Created By'.User <> gvarUser.User
            && (
                   (ctxApruebaCliente  && TargetTable = "sanic_chtblclient")
                || (ctxApruebaSucursal && TargetTable = "sanic_chtblclientbranch")
                || (ctxApruebaCorreo   && TargetTable = "sanic_chtblclientemail")
                || (ctxApruebaCuenta   && TargetTable = "sanic_chtblclientaccount")
            )
        ),
        'Created On', SortOrder.Descending
    )
);
// --- 2b. Contadores server-side (CountRows delegado a Dataverse; ver "A verificar") ---
UpdateContext({
    ctxCountMias:        CountRows(Filter('CH-Tbl-ClientDataChangeRequests', 'Created By'.User = gvarUser.User)),
    ctxKpiResueltasHoy:  CountRows(Filter('CH-Tbl-ClientDataChangeRequests', SolvedOn >= Today())),
    ctxKpiAplicadasHoy:  CountRows(Filter('CH-Tbl-ClientDataChangeRequests', SolvedOn >= Today() && 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Applied)),
    ctxKpiRechazadasHoy: CountRows(Filter('CH-Tbl-ClientDataChangeRequests', SolvedOn >= Today() && 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Rejected)),
    ctxKpiFallidas7d:    CountRows(Filter('CH-Tbl-ClientDataChangeRequests', SolvedOn >= DateAdd(Today(), -7, TimeUnit.Days) && 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Failed))
});

// ============ 3) BLOQUE-GRILLA (la colección que pinta la galería, según segmento+filtros) ============
// If POR FUERA de cada ClearCollect, con else — gotcha 8.13. En "mi" se filtra EN MEMORIA
// sobre colParaMiSol (ya materializada y acotada); en "mias"/"todas" el filtro es server-side.
If(
    ctxSegmento = "mi",
    ClearCollect(
        colSolicitudesSol,
        Filter(
            colParaMiSol,
            (ctxTabla = "" || TargetTable = ctxTabla)
            && (ctxBusqueda = "" || StartsWith(Name, ctxBusqueda) || RelatedClient.'CH-Tbl-Client' = ctxBusqClienteGuid)
        )
    ),
    ctxSegmento = "mias",
    ClearCollect(
        colSolicitudesSol,
        Sort(
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                'Created By'.User = gvarUser.User
                && (ctxTabla = "" || TargetTable = ctxTabla)
                && (ctxBusqueda = "" || StartsWith(Name, ctxBusqueda) || RelatedClient.'CH-Tbl-Client' = ctxBusqClienteGuid)
            ),
            'Created On', SortOrder.Descending
        )
    ),
    ClearCollect(
        colSolicitudesSol,
        Sort(
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                (ctxTabla = "" || TargetTable = ctxTabla)
                && (ctxBusqueda = "" || StartsWith(Name, ctxBusqueda) || RelatedClient.'CH-Tbl-Client' = ctxBusqClienteGuid)
            ),
            'Created On', SortOrder.Descending
        )
    )
);
UpdateContext({ctxPagina: 1})
```

### 1.2 Bloques reutilizables (referencia para otros handlers)

- **BLOQUE-COMPLETO** = las secciones `2a + 2b + 3` del `OnVisible` de arriba (desde el
  `ClearCollect(colParaMiSol, …)` hasta el `UpdateContext({ctxPagina: 1})` final, inclusive).
  Se pega tal cual en `btnRefrescarSol.OnSelect` (precedido de un `Refresh`, ver §2).
- **BLOQUE-GRILLA** = solo la sección `3` (el `If(ctxSegmento = …)` completo + el
  `UpdateContext({ctxPagina: 1})` final). Se pega tal cual en el `OnSelect` de los 3 botones
  de segmento y en el `OnChange` de `inpBusqSol` y `drpTablaSol`, SIEMPRE después del
  `UpdateContext` propio de cada control.

> 🧨 Gotchas activos en esta pantalla: 8.3 (Reset en OnVisible), 8.12 (delegación: cero
> `AddColumns`/`Search` antes del filtro server-side — los `Switch` de badges van inline en
> las propiedades del template, nunca como columnas calculadas pre-filtro), 8.13
> (`ClearCollect`+`If` con else por fuera), 8.4/8.5 (fila clicable y pills nativas), 7.10
> (separador en `TemplateSize - 1`).

---

## 2. cmpAppHeaderSol y toolbar

### 2.1 `cmpAppHeaderSol` — Componente (receta 7.2, que fija todo lo interno)

```powerfx
cmpAppHeaderSol As cmpAppHeader:
    Activo: ="Solicitudes"
    Height: =fxHeaderH
    OnClientes: =Navigate(scrClientes, ScreenTransition.None)
    OnSolicitudes: =false
    RolTexto: =gvarRolTexto
    Subtitulo: ="Solicitudes de cambio"
    Titulo: ="Administración de Clientes"
    Width: =Parent.Width
    X: =0
    Y: =0
```

### 2.2 `cntToolbarSol` — Contenedor (manual)

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `fxHeaderH` |
| Width / Height | `Parent.Width` / `56` |
| Fill | `Color.Transparent` |

**`cntSegSol`** — Contenedor (manual), pista del segmentado (mismo patrón visual que
`cntNavHdr` de 7.2, radio 9 como el `.seg` del mockup):

| Propiedad | Valor |
|---|---|
| X / Y | `fxGutter` / `12` |
| Width / Height | `btnSegTodasSol.X + btnSegTodasSol.Width + 3` / `32` |
| Fill / BorderColor / BorderThickness | `ThemeSurface2` / `ThemeLine` / `1` |
| Radius (las 4) | `9` |

**Los 3 botones de segmento** (Botón clásico, hijos de `cntSegSol`; `Parent` = `cntSegSol`).
Comunes: Y `3` · Height `26` · Radius `7` · BorderThickness `0` · `Font.'Open Sans'` · Size `9`
· Width `Len(Self.Text) * 5.6 + 26`. Por botón (`<seg>` = su clave):

| Control | X | Text | `<seg>` |
|---|---|---|---|
| `btnSegMiSol` | `3` | `"Para mi decisión (" & CountRows(colParaMiSol) & ")"` | `"mi"` |
| `btnSegMiasSol` | `btnSegMiSol.X + btnSegMiSol.Width + 3` | `"Mías (" & ctxCountMias & ")"` | `"mias"` |
| `btnSegTodasSol` | `btnSegMiasSol.X + btnSegMiasSol.Width + 3` | `"Todas"` | `"todas"` |

| Prop (los 3) | Fórmula |
|---|---|
| Fill | `If(ctxSegmento = "<seg>", ThemeSurface, Color.Transparent)` |
| Color | `If(ctxSegmento = "<seg>", ThemeAccent, ThemeInk2)` |
| FontWeight | `If(ctxSegmento = "<seg>", FontWeight.Semibold, FontWeight.Normal)` |
| HoverFill | `If(ctxSegmento = "<seg>", ThemeSurface, ThemeSurface2)` |
| OnSelect | `UpdateContext({ctxSegmento: "<seg>"});` + **BLOQUE-GRILLA** (§1.2) |

**`cntBusqSol` / `icoLupaBusqSol` / `inpBusqSol`** — buscador según **receta 7.9** (la receta
fija posiciones internas, fills y bordes). Lo específico de esta pantalla:

| Propiedad | Valor |
|---|---|
| `cntBusqSol.X` / `Y` / `Width` / `Height` | `cntSegSol.X + cntSegSol.Width + fxGap` / `12` / `Min(300, Parent.Width * 0.25)` / `32` |
| `inpBusqSol.HintText` | `"N° de solicitud o cliente…"` (sin el glifo `⌕`: la receta 7.9 ya inserta `icoLupaBusqSol`/`Icon.Search` a la izquierda del input — repetirlo en el HintText duplicaría la lupa) |
| `inpBusqSol.OnChange` | ver abajo |

```powerfx
// inpBusqSol.OnChange — resuelve el "o cliente" de la búsqueda: el N° va delegable por
// StartsWith(Name, …); el cliente se resuelve UNA vez acá (LookUp delegable sobre el maestro)
// y entra al filtro como igualdad de GUID. Si nada matchea, GUID centinela que no matchea nunca
// (jamás Blank: compararía lookup vacío = Blank y matchearía RFCs sin cliente).
UpdateContext({
    ctxBusqueda: Trim(Self.Text),
    ctxBusqClienteGuid:
        If(
            IsBlank(Trim(Self.Text)),
            GUID("00000000-0000-0000-0000-000000000000"),
            Coalesce(
                LookUp('CH-Tbl-Clients', StartsWith(Name, Trim(Self.Text))).'CH-Tbl-Client',
                GUID("00000000-0000-0000-0000-000000000000")
            )
        )
});
// + pegá acá el BLOQUE-GRILLA (§1.2)
```

**`drpTablaSol`** — Menú desplegable clásico:

| Propiedad | Valor |
|---|---|
| X / Y / Width / Height | `cntBusqSol.X + cntBusqSol.Width + fxGap` / `12` / `170` / `32` |
| Items | ver abajo |
| Value | `"Etiqueta"` |
| Fill / BorderColor | `ThemeSurface` / `ThemeLine` |
| Font / Size | `Font.'Open Sans'` / `9` |
| OnChange | `UpdateContext({ctxTabla: Self.Selected.Tabla});` + **BLOQUE-GRILLA** (§1.2) |

```powerfx
// drpTablaSol.Items — los valores de la columna Tabla son el LOGICAL NAME de sanic_targettable
// (divergencia D6 de 01, RESUELTA — ver 02 DC3 / 03 hallazgo crítico #3)
Table(
    {Etiqueta: "Todas las tablas", Tabla: ""},
    {Etiqueta: "Clientes",   Tabla: "sanic_chtblclient"},
    {Etiqueta: "Sucursales", Tabla: "sanic_chtblclientbranch"},
    {Etiqueta: "Correos",    Tabla: "sanic_chtblclientemail"},
    {Etiqueta: "Cuentas",    Tabla: "sanic_chtblclientaccount"}
)
```

**`btnRefrescarSol`** — Botón clásico, `ESTILO-BTN-CHICO`:

| Propiedad | Valor |
|---|---|
| X / Y / Width / Height | `Parent.Width - fxGutter - Self.Width` / `(Parent.Height - 26)/2` / `110` / `26` |
| Text | `"↻ Actualizar"` |
| OnSelect | `Refresh('CH-Tbl-ClientDataChangeRequests');` + **BLOQUE-COMPLETO** (§1.2) |

---

## 3. cntBodySol y KPIs

**`cntBodySol`** — Contenedor (manual):

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `cntToolbarSol.Y + cntToolbarSol.Height` |
| Width / Height | `Parent.Width` / `Parent.Height - Self.Y` |
| Fill | `Color.Transparent` |

**Los 3 KPI cards** — Contenedor (manual) cada uno; `Parent` = `cntBodySol`. Comunes:
Y `0` · Height `fxKpiH` · Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1`
· Radius (las 4) `11` · Width `(Parent.Width - fxGutter*2 - fxGap*2) / 3`:

| Control | X |
|---|---|
| `cntKpiParaMiSol` | `fxGutter` |
| `cntKpiResueltasSol` | `cntKpiParaMiSol.X + cntKpiParaMiSol.Width + fxGap` |
| `cntKpiFallidasSol` | `cntKpiResueltasSol.X + cntKpiResueltasSol.Width + fxGap` |

**Labels internos** (hijos de su card; `Parent` = el card). Posiciones idénticas en los 3:
`lblKpi…LabSol` X `15` Y `8` Width `Parent.Width - 30` Height `14` (`ESTILO-KPI-LAB`);
`lblKpi…ValSol` X `15` Y `22` Width `Parent.Width - 30` Height `26` (`ESTILO-KPI-VAL`);
`lblKpi…SubSol` X `15` Y `48` Width `Parent.Width - 30` Height `14` (`ESTILO-KPI-SUB`).
Textos y particularidades:

| Control | Text |
|---|---|
| `lblKpiParaMiLabSol` | `"ESPERAN MI DECISIÓN"` |
| `lblKpiParaMiValSol` | `CountRows(colParaMiSol)` |
| `lblKpiParaMiSubSol` | ver fórmula abajo |
| `lblKpiResueltasLabSol` | `"RESUELTAS HOY"` |
| `lblKpiResueltasValSol` | `ctxKpiResueltasHoy` |
| `lblKpiResueltasSubSol` | `ctxKpiAplicadasHoy & " aplicadas · " & ctxKpiRechazadasHoy & " rechazadas"` |
| `lblKpiFallidasLabSol` | `"FALLIDAS (7 DÍAS)"` |
| `lblKpiFallidasValSol` | `ctxKpiFallidas7d` · **Color** `If(ctxKpiFallidas7d > 0, ThemeDanger, ThemeInk)` |
| `lblKpiFallidasSubSol` | `If(ctxKpiFallidas7d > 0, "requiere revisión", "sin fallas recientes")` |

```powerfx
// lblKpiParaMiSubSol.Text — antigüedad de la más vieja de MI cola (sobre la colección local;
// la fecha base es Created On: no existe sello de "enviada", ver Divergencias)
If(
    CountRows(colParaMiSol) = 0,
    "sin pendientes",
    With(
        {vieja: First(Sort(colParaMiSol, 'Created On', SortOrder.Ascending)).'Created On'},
        If(
            DateDiff(vieja, Now(), TimeUnit.Hours) < 24,
            "la más vieja: hace " & DateDiff(vieja, Now(), TimeUnit.Hours) & " h",
            "la más vieja: hace " & DateDiff(vieja, Now(), TimeUnit.Days) & " días"
        )
    )
)
```

---

## 4. Grilla de solicitudes

**`cntGrillaSol`** — Contenedor (manual), card según receta 7.4; `Parent` = `cntBodySol`:

| Propiedad | Valor |
|---|---|
| X / Y | `fxGutter` / `fxKpiH + fxGap` |
| Width / Height | `Parent.Width - fxGutter*2` / `Parent.Height - Self.Y - 16` |
| Fill / BorderColor / BorderThickness | `ThemeSurface` / `ThemeLine` / `1` |
| Radius (las 4) | `fxCardR` |

### 4.1 Encabezado de columnas

**`shpGrillaHeadSol`** — Rectángulo clásico: X `0` · Y `0` · Width `Parent.Width` · Height
`34` · Fill `ThemeSurface2` · BorderThickness `0` · `RadiusTopLeft`/`RadiusTopRight` `fxCardR`
· `RadiusBottomLeft`/`RadiusBottomRight` `0` (fidelidad de esquinas: `cntGrillaSol` tiene
`Radius fxCardR` pero los contenedores/formas manuales no recortan hijos como el
`overflow:hidden` del `.card` del mockup — sin este radio propio, la banda rectangular tapa
las esquinas superiores redondeadas de la card).

**Las 7 etiquetas de columna** — Etiqueta clásica, `ESTILO-HEADER-COL`, Y `0`, Height `34`.
El ancho útil se reparte por fracción del ancho de la card (suma de fracciones = 1.00) y las
X van encadenadas (regla §0.4 de 01). `<W>` = `(Parent.Width - 92)`:

| Control | X | Width | Text |
|---|---|---|---|
| `lblColNumSol` | `16` | `<W> * 0.14` | `"SOLICITUD"` |
| `lblColClienteSol` | `lblColNumSol.X + lblColNumSol.Width + 10` | `<W> * 0.22` | `"CLIENTE"` |
| `lblColTablaSol` | `lblColClienteSol.X + lblColClienteSol.Width + 10` | `<W> * 0.14` | `"TABLA"` |
| `lblColTipoSol` | `lblColTablaSol.X + lblColTablaSol.Width + 10` | `<W> * 0.14` | `"TIPO"` |
| `lblColEstadoSol` | `lblColTipoSol.X + lblColTipoSol.Width + 10` | `<W> * 0.12` | `"ESTADO"` |
| `lblColCreadorSol` | `lblColEstadoSol.X + lblColEstadoSol.Width + 10` | `<W> * 0.12` | `"CREADA POR"` |
| `lblColFechaSol` | `lblColCreadorSol.X + lblColCreadorSol.Width + 10` | `<W> * 0.12` | ver abajo |

`lblColFechaSol` es el único encabezado interactivo (orden asc/desc, flecha como el
`sortable` del mockup): Align `Right` · Text `"CREADA " & If(ctxOrdenDesc, "▼", "▲")` ·
Color `ThemeInk2` (mismo tono que los demás encabezados — en el mockup solo la flecha
`.sortable .arr` es acento, el texto conserva `ink2`; un solo Label no admite dos colores,
divergencia S10) · OnSelect `UpdateContext({ctxOrdenDesc: !ctxOrdenDesc, ctxPagina: 1})`
(reordena en memoria vía el `Sort` del `Items` de la galería — no re-consulta).

**`shpGrillaLineaSol`** — Rectángulo clásico: X `0` · Y `33` · Width `Parent.Width` · Height
`1` · Fill `ThemeLine`.

### 4.2 `galSolicitudesSol` — Galería vertical en blanco

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `34` |
| Width / Height | `Parent.Width` / `Parent.Height - 34 - cntPagSol.Height` |
| TemplateSize / TemplatePadding | `fxRowH` / `0` |
| Items | ver abajo |
| OnSelect | ver abajo |

```powerfx
// Items — paginación receta 7.8: corte de página EN MEMORIA sobre la colección ya
// materializada; el orden se resuelve acá para que el toggle del encabezado no re-consulte.
// Skip() NO EXISTE en Power Fx (gotcha 7.8/01-app.md) — el corte real es FirstN(LastN(...)):
// LastN recorta desde el final del resultado ordenado hasta lo que queda tras descartar las
// paginas previas, y el FirstN exterior corta esa cola al tamaño de una pagina.
FirstN(
    LastN(
        Sort(colSolicitudesSol, 'Created On', If(ctxOrdenDesc, SortOrder.Descending, SortOrder.Ascending)),
        CountRows(colSolicitudesSol) - (ctxPagina - 1) * ctxFilasPag
    ),
    ctxFilasPag
)
```

```powerfx
// OnSelect — CONTRATO DE SALIDA de la pantalla (la acción vive acá, gotcha 8.4;
// todos los hijos del template delegan con Select(Parent))
Set(gvarSolicitudSel, ThisItem);
Set(gvarClienteSel, ThisItem.RelatedClient);
Navigate(scrSolicitud, ScreenTransition.None)
```

> La galería clásica auto-selecciona la 1ª fila al cargar `Items` (gotcha 8.2) — eso solo
> pinta la fila con el fondo de selección (7.5), NO dispara `OnSelect` ni navega. Correcto y
> buscado: replica la fila `gSel` del mockup SO1.

**Template** — regla de coordenadas (gotcha 8.14): la galería está en X `0` dentro de la
card, así que **X de cada control del template = X del encabezado correspondiente** — se
referencia el control del encabezado directamente, nunca un literal duplicado. Orden de
inserción (z-order): `btnFilaSol` PRIMERO (queda al fondo), después `shpFilaSelSol`,
`shpFilaSelBarraSol`, luego el contenido, `shpFilaSepSol` al final.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnFilaSol` | Botón clásico | `0` | `0` | `galSolicitudesSol.Width` | `fxRowH` | Receta 7.4 (overlay): Fill `Color.Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · OnSelect `Select(Parent)` |
| `shpFilaSelSol` | Rectángulo clásico | `0` | `0` | `galSolicitudesSol.Width` | `fxRowH` | Receta 7.5: Fill `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` |
| `shpFilaSelBarraSol` | Rectángulo clásico | `0` | `0` | `3` | `fxRowH` | Receta 7.5: Fill `ThemeAccent` · Visible `ThisItem.IsSelected` |
| `lblFilaNumSol` | Etiqueta clásica | `lblColNumSol.X` | `0` | `lblColNumSol.Width` | `fxRowH` | `ESTILO-CELDA` · Bold · Text `ThisItem.Name` · OnSelect `Select(Parent)` |
| `lblFilaClienteSol` | Etiqueta clásica | `lblColClienteSol.X` | `0` | `lblColClienteSol.Width` | `fxRowH` | `ESTILO-CELDA` · Text `Coalesce(ThisItem.RelatedClient.Name, "—")` · OnSelect `Select(Parent)` |
| `lblFilaTablaSol` | Etiqueta clásica | `lblColTablaSol.X` | `0` | `lblColTablaSol.Width` | `fxRowH` | `ESTILO-CELDA` · Text: ver fórmula abajo · OnSelect `Select(Parent)` |
| `pilFilaTipoSol` | Botón clásico | `lblColTipoSol.X` | `(fxRowH - 20)/2` | `Len(Self.Text) * 5.4 + 18` | `20` | Receta 7.3 (badge nativo, gotcha 8.5) · fórmulas abajo · OnSelect `Select(Parent)` |
| `pilFilaEstadoSol` | Botón clásico | `lblColEstadoSol.X` | `(fxRowH - 20)/2` | `Len(Self.Text) * 5.4 + 18` | `20` | Receta 7.3 · fórmulas abajo · OnSelect `Select(Parent)` |
| `lblFilaCreadorSol` | Etiqueta clásica | `lblColCreadorSol.X` | `0` | `lblColCreadorSol.Width` | `fxRowH` | `ESTILO-META` · Text `Coalesce(ThisItem.'Created By'.'Full Name', "—")` · OnSelect `Select(Parent)` |
| `lblFilaFechaSol` | Etiqueta clásica | `lblColFechaSol.X` | `0` | `lblColFechaSol.Width` | `fxRowH` | `ESTILO-CELDA-NUM` · Text: ver fórmula abajo · OnSelect `Select(Parent)` |
| `shpFilaSepSol` | Rectángulo clásico | `0` | `fxRowH - 1` | `galSolicitudesSol.Width` | `1` | Receta 7.10: Fill `ThemeLineSoft` |

```powerfx
// lblFilaTablaSol.Text — el MODELO manda: la etiqueta se deriva del código TargetTable
// (Switch fijo, literales = LOGICAL NAME — D6 de 01 RESUELTA), con fallback al texto
// sembrado TargetTableDisplayName (ver Divergencias)
Switch(
    ThisItem.TargetTable,
    "sanic_chtblclient",        "Clientes",
    "sanic_chtblclientbranch",  "Sucursales",
    "sanic_chtblclientemail",   "Correos",
    "sanic_chtblclientaccount", "Cuentas",
    Coalesce(ThisItem.TargetTableDisplayName, "—")
)
```

```powerfx
// pilFilaTipoSol — mapa TIPO de la receta 7.3, inline (los mapas no pueden ser named
// formulas: dependen de ThisItem — nota de 01 §3)
// Text:
Switch(
    ThisItem.Type,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Update,     "Edición",
    'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate, "Desactivación",
    'Type (CH-Tbl-ClientDataChangeRequests)'.Create,     "Alta",
    'Type (CH-Tbl-ClientDataChangeRequests)'.Activate,   "Activación",
    "—"
)
// Fill:
Switch(
    ThisItem.Type,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Update,     ThemeInfoSoft,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate, ThemeDangerSoft,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Create,     ThemeAccentSoft,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Activate,   ThemeOkSoft,
    ThemeLineSoft
)
// Color:
Switch(
    ThisItem.Type,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Update,     ThemeInfo,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate, ThemeDanger,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Create,     ThemeAccent,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Activate,   ThemeOk,
    ThemeInk3
)
// BorderThickness: =0  (ningún tono de tipo es "neutro")
```

```powerfx
// pilFilaEstadoSol — mapa ESTADO de la receta 7.3, inline
// Text:
Switch(
    ThisItem.'Change Status',
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,     "Borrador",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted, "Enviada",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved,  "Aprobada",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Rejected,  "Rechazada",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Applied,   "Aplicada",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Failed,    "Fallida",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Canceled,  "Cancelada",
    "—"
)
// Fill:
Switch(
    ThisItem.'Change Status',
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted, ThemeWarnSoft,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved,  ThemeInfoSoft,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Rejected,  ThemeDangerSoft,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Applied,   ThemeOkSoft,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Failed,    ThemeDangerSoft,
    ThemeLineSoft   // Draft, Canceled y default = neutro
)
// Color:
Switch(
    ThisItem.'Change Status',
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted, ThemeWarn,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved,  ThemeInfo,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Rejected,  ThemeDanger,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Applied,   ThemeOk,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Failed,    ThemeDanger,
    ThemeInk3
)
// BorderThickness: =0  (fidelidad SO1: `.pMuted` del mockup NO lleva borde en ningún tono,
// ni siquiera el neutro Borrador/Cancelada — diverge a propósito de la regla general de 7.3,
// que sí pone borde 1 en la variante neutra para distinguirla de fondo en hover; ver
// divergencia S11. El fondo `ThemeLineSoft` del pill ya se distingue del hover de fila
// `ThemeSurface2`, así que no hace falta el borde para esa distinción)
// BorderColor: =ThemeLineSoft (inerte con BorderThickness 0; se deja seteado por si el
// hallazgo S11 se revierte más adelante)
```

```powerfx
// lblFilaFechaSol.Text — fecha relativa como el mockup ("hoy 09:41" / "ayer 16:20" / "21 ago");
// base Created On (no existe sello de envío — ver Divergencias). FmtHora usa HH (gotcha 8.1).
With(
    {d: ThisItem.'Created On'},
    If(
        Int(d) = Int(Today()), "hoy " & Text(d, FmtHora),
        Int(d) = Int(Today()) - 1, "ayer " & Text(d, FmtHora),
        Text(d, "dd mmm", "es-ES")
    )
)
```

### 4.3 Estado vacío

**`lblVacioSol`** — Etiqueta clásica, `ESTILO-VACIO` (incluye `Wrap: true` — gotcha 8.15):

| Propiedad | Valor |
|---|---|
| X / Y | `Parent.Width * 0.2` / `120` |
| Width / Height | `Parent.Width * 0.6` / `60` |
| Visible | `CountRows(colSolicitudesSol) = 0` |
| Text | ver abajo |

```powerfx
Switch(
    ctxSegmento,
    "mi",   "No hay solicitudes esperando tu decisión." & If(ctxBusqueda <> "" || ctxTabla <> "", " Probá quitando la búsqueda o el filtro de tabla.", ""),
    "mias", "Todavía no tenés solicitudes" & If(ctxBusqueda <> "" || ctxTabla <> "", " que coincidan con los filtros.", " creadas."),
    "No hay solicitudes que coincidan con los filtros."
)
```

### 4.4 Paginación

**`cntPagSol`** — Contenedor (manual) según receta 7.8: X `0` · Y `Parent.Height - 36` ·
Width `Parent.Width` · Height `36` · Fill `ThemeSurface2` · BorderColor `ThemeLine` ·
BorderThickness `1` (borde SOLO superior, mismo patrón de borde de un solo lado documentado
en 7.7 para `cnt…Panel`; réplica del `border-top: 1px solid var(--line)` del `.foot` del
mockup) · `RadiusBottomLeft`/`RadiusBottomRight` `fxCardR` · `RadiusTopLeft`/`RadiusTopRight`
`0` (fidelidad de esquinas inferiores de la card, mismo motivo que `shpGrillaHeadSol` en
§4.1) (pie como el `.foot` del mockup). Hijos (`Parent` = `cntPagSol`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblResumenPagSol` | Etiqueta clásica | `16` | `0` | `Parent.Width * 0.5` | `Parent.Height` | Size `9.38` · Bold · Color `ThemeInk` (el `.foot` del mockup es bold/12.5px/tinta plena, no `ESTILO-META`) · Text: ver abajo |
| `btnPagSigSol` | Botón clásico | `Parent.Width - 16 - Self.Width` | `4` | `28` | `28` | `ESTILO-BTN-CHICO` · Radius `6` · Text `"›"` · DisplayMode `If(ctxPagina < Max(1, RoundUp(CountRows(colSolicitudesSol) / ctxFilasPag, 0)), DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `UpdateContext({ctxPagina: ctxPagina + 1})` |
| `btnPagPrevSol` | Botón clásico | `btnPagSigSol.X - 6 - Self.Width` | `4` | `28` | `28` | `ESTILO-BTN-CHICO` · Radius `6` · Text `"‹"` · DisplayMode `If(ctxPagina > 1, DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `UpdateContext({ctxPagina: ctxPagina - 1})` |

```powerfx
// lblResumenPagSol.Text
CountRows(colSolicitudesSol) & " solicitudes · página " & ctxPagina & " de " &
Max(1, RoundUp(CountRows(colSolicitudesSol) / ctxFilasPag, 0))
```

---

> ## Notas
>
> **Dependencias de backend.** Ninguna nueva: esta pantalla solo LEE
> `CH-Tbl-ClientDataChangeRequests` (+ `CH-Tbl-Clients` para resolver la búsqueda por
> cliente) y no llama la Custom API — el fallback "backend pendiente" de
> `sanic_CHResolveChangeRequest` aplica a `scrSolicitud` (guía 05), no acá.
>
> **Divergencias declaradas vs mockup (SO1)**
>
> | # | Divergencia | Por qué |
> |---|---|---|
> | S1 | La columna del mockup **"Enviada"** se construye como **"CREADA"** con `'Created On'` | El esquema real de `sanic_CHTblClientDataChangeRequest` NO tiene sello de envío (columnas de fecha: `createdon`, `modifiedon`, `sanic_solvedon`, `sanic_applyon` — verificado en el Entity.xml completo). `'Modified On'` mentiría en filas resueltas (marcaría la resolución, no el envío). La v1 también ordenaba por `createdon`. Aproximación honesta, no silenciosa |
> | S2 | El atenuado `opacity: .75` de las filas resueltas del mockup **se omite** | En canvas exigiría duplicar un `Color` condicional en los 7 controles del template o un velo que robaría el clic de la fila; el badge de estado (Aplicada/Fallida/…) ya comunica el cierre |
> | S3 | Paginación **sin botones numerados** (`‹ 1 2 ›` del mockup → `‹` `›` + resumen) | Botones de número dinámicos requieren una galería horizontal extra sin valor funcional con 2–7 páginas; la receta 7.8 admite la variante mínima |
> | S4 | La columna **"Tabla"** se deriva con `Switch` fijo sobre `TargetTable` (fallback `TargetTableDisplayName`) | El mockup muestra plurales en español y el valor sembrado de `sanic_targettabledisplayname` lo escribía la v1 con el bug L9 (singular/plural inconsistente) — el código `TargetTable` es la fuente estable |
> | S5 | Búsqueda "N° de solicitud **o cliente**": la parte cliente se resuelve con el **primer** cliente cuyo `Name` empieza con el texto (`LookUp` delegable → igualdad de GUID en el filtro) | La cabecera del RFC no tiene mirror de nombre de cliente y filtrar por columna de entidad relacionada no delega. Si dos clientes comparten prefijo, solo matchea el primero — aproximación honesta |
> | S6 | Contador **"Mías (N)"** cuenta TODAS las solicitudes del usuario (cualquier estado); KPI **"Resueltas hoy"** es global del entorno, no "resueltas por mí" | El mockup no define el corte; se eligió el más útil para bandeja (Mías = mi historial completo; Resueltas hoy = pulso de la cola) |
> | S7 | Fechas cortas tipo `"21 ago"` via `Text(d, "dd mmm", "es-ES")` | El locale puede renderear `"ago."` (con punto) según el runtime — cosmética, no se pelea contra el locale |
> | S8 | "Creada por" muestra el `'Full Name'` completo (mockup: "María J. Duarte" abreviado) | Abreviar segundo nombre en Power Fx es frágil con nombres compuestos; el ancho de columna trunca con elipsis nativa |
> | S9 | La "columna **Resuelta**" que el caption descriptivo de SO1 promete ("muestra el resultado inmediato de la Custom API") NO existe en la grilla renderizada del mockup | Contradicción interna del propio mockup: la grilla SO1 solo tiene 7 columnas (Solicitud/Cliente/Tabla/Tipo/Estado/Creada por/Enviada) y ninguna se llama "Resuelta". Esta guía sigue la grilla renderizada (fuente de verdad visual), no el texto descriptivo; el resultado síncrono se comunica igual, vía la columna **ESTADO** (`Aplicada`/`Fallida`) |
> | S10 | `lblColFechaSol` usa Color `ThemeInk2` para TODO el texto ("CREADA ▼"), no solo la flecha | En el mockup solo `.sortable .arr` es acento (`var(--accent)`); el texto "Enviada"/"CREADA" conserva `ink2` como los demás encabezados (mismo patrón que "Cliente ▼" en CL1). Un solo Label clásico no admite dos colores en una misma cadena — partirlo en dos Labels (texto + flecha) agregaría 2 controles a los 51 del árbol sin necesidad funcional; se prioriza fidelidad de color del texto sobre la del ícono |
> | S11 | `pilFilaEstadoSol` con `BorderThickness: 0` en TODOS los tonos, incluido el neutro (Borrador/Cancelada) | La receta 7.3 (general, `01-app.md`) pone borde `1` en la variante neutra para distinguirla del fondo en hover de fila. Acá se prioriza la fidelidad literal del `.pMuted` del mockup (sin borde) porque el fondo del pill (`ThemeLineSoft`) ya es visualmente distinto del hover de la fila (`ThemeSurface2`) — el borde no aportaba distinción real en esta grilla. Otras pantallas que sigan la receta 7.3 tal cual no se ven afectadas por esta divergencia puntual |
>
> **A verificar en Studio**
>
> - ~~Literales de `TargetTable`~~ — **RESUELTO**: son el LOGICAL NAME de la entidad
>   (`sanic_chtblclient`/`sanic_chtblclientbranch`/`sanic_chtblclientemail`/
>   `sanic_chtblclientaccount`), no `"Client"`/`"ClientBranch"`/`"ClientEmail"`/
>   `"ClientAccount"` — divergencia D6 de la guía 01, resuelta contra el seed y `Schema.cs`
>   (ver 02 DC3 / 03 hallazgo crítico #3). Ya aplicados en `ctxAprueba*` del OnVisible,
>   `drpTablaSol.Items`, el filtro de `colParaMiSol` y el `Switch` de `lblFilaTablaSol`.
> - `'Created By'.User` / `gvarUser.User`: se asume que la clave primaria de `Users` se llama
>   `User` en Power Fx. **Si IntelliSense la nombra distinto, el suyo manda** (igual que los
>   nombres de enum `'Change Status (…)'` / `'Type (…)'`).
> - Delegación del OR compuesto de `colParaMiSol` (4 ramas `ctxAprueba* && TargetTable = …`).
>   Si el App Checker lo subraya, fallback equivalente: `Clear(colParaMiSol);` + cuatro
>   `If(ctxAprueba<T>, Collect(colParaMiSol, Filter('CH-Tbl-ClientDataChangeRequests',
>   'Change Status' = Submitted && 'Created By'.User <> gvarUser.User && TargetTable =
>   "<T>")));` — cada `Collect` con filtro simple 100% delegable.
> - `CountRows` delegado a Dataverse en los 5 contadores de `2b`. Si el App Checker lo marca
>   no-delegable en este entorno, materializar cada conteo con un `ClearCollect` filtrado
>   (server-side) y contar la colección.
> - Que `ClearCollect` de `colParaMiSol`/`colSolicitudesSol` respete el límite de filas del
>   entorno (500/2000): con >2000 solicitudes históricas, el segmento **Todas** queda
>   truncado a las 2000 más recientes (el `Sort` descendente server-side garantiza que lo
>   truncado sea lo viejo). "Para mi decisión" y "Mías" no deberían acercarse al límite.

---

## Correcciones post-build 2026-08-25

Durante el debugging en Studio se corrigió una fórmula que esta guía mostraba en su versión
vieja (rota). El fuente REAL (`scrSolicitudes.fx.yaml`) mandó:

| # | Qué estaba mal | Por qué (fuente real) |
|---|---|---|
| PB1 | `galSolicitudesSol.Items` (§4.2) usaba `FirstN(Skip(...), ...)` | `Skip()` no existe en Power Fx — no compila. El fuente real usa `FirstN(LastN(Sort(colSolicitudesSol, 'Created On', ...), CountRows(colSolicitudesSol) - (ctxPagina-1)*ctxFilasPag), ctxFilasPag)` (ver 01 §7.8) |

---

## Checklist de salida

- [ ] Árbol completo insertado y renombrado ANTES de pegar fórmulas (los 51 controles, con
      sufijo `Sol`); `btnFilaSol` insertado PRIMERO en el template (queda al fondo del
      z-order) y `shpFilaSepSol` al final.
- [ ] `OnVisible` pegado completo (§1.1); entrar a la pantalla, salir y volver: búsqueda,
      dropdown de tabla, segmento, orden y página vuelven a default (gotcha 8.3).
- [ ] BLOQUE-GRILLA pegado en los 3 segmentos + `inpBusqSol.OnChange` +
      `drpTablaSol.OnChange`; BLOQUE-COMPLETO (con `Refresh`) en `btnRefrescarSol`.
- [ ] App Checker sin warnings de delegación en `colParaMiSol`, las 3 ramas de
      `colSolicitudesSol` y los 5 `CountRows` (si aparecen, aplicar los fallbacks de Notas).
- [ ] Con un usuario **Approver**: la pantalla abre en "Para mi decisión"; la cola excluye
      RFCs creados por él y RFCs de tablas donde no es Approver; el contador del segmento =
      KPI "Esperan mi decisión" = filas de la grilla (sin búsqueda/filtro activos).
- [ ] Con un usuario **Contributor puro**: abre en "Mías"; "Para mi decisión (0)".
- [ ] Buscar un N° (`RFC-003…`) filtra delegable; buscar un nombre de cliente
      (`Distribuidora…`) trae sus RFCs; texto sin match → grilla vacía con `lblVacioSol`.
- [ ] Filtro "Correos" + segmento "Todas" muestra solo `TargetTable = "sanic_chtblclientemail"`.
- [ ] Badges: Enviada ámbar, Aplicada verde, Fallida/Rechazada/Desactivación rojas, Alta
      acento, Edición info, Borrador/Cancelada neutras CON borde (7.3).
- [ ] Clic en cualquier punto de la fila (incluido sobre labels y pills) navega a
      `scrSolicitud` con `gvarSolicitudSel`/`gvarClienteSel` seteados; la auto-selección de
      la 1ª fila al cargar NO navega sola.
- [ ] Encabezado "CREADA ▼" clicable invierte el orden sin re-consultar; paginación `‹`/`›`
      se deshabilita en los extremos y el resumen muestra `N solicitudes · página p de t`.
- [ ] Fechas: fila de hoy = `hoy HH:mm` (24 h — nunca `hh`, gotcha 8.1); ayer = `ayer HH:mm`;
      anteriores = `dd mmm`.
- [ ] Angostar la ventana (~1080 y ~840 px, vista RSP): KPIs, toolbar y columnas se comprimen
      por fórmula sin recortes ni superposiciones.
