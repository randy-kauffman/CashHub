# Guía 05 — scrSolicitud (detalle y decisión del RFC, vistas SO2/SO3/SO4/SO5)

**Propósito.** Pantalla única de detalle de un RFC (`CH-Tbl-ClientDataChangeRequests`) que
cubre 4 estados del mockup con el MISMO árbol de controles: **SO2** decisión del aprobador
(Enviada + Approver + creador≠usuario), **SO3** resultado síncrono tras Aprobar/Rechazar
(Custom API `sanic_CHResolveChangeRequest`), **SO4** error del guard visible con motivo real,
**SO5** vista del creador en Borrador (agregar/quitar líneas, Enviar/Cancelar, aviso de
conflicto por registro). La decisión (Aprobar/Rechazar) viaja SIEMPRE por
`Environment.sanic_CHResolveChangeRequest` — nunca un `Patch` directo de la cabecera a
`Approved`. Enviar/Cancelar y agregar/quitar líneas SÍ son `Patch` directo del creador, con
revalidación de estado antes de escribir.

Convenciones: sufijo de pantalla **`Det`** (01 §0), tokens `Theme*`/`fx*` y formatos de
`App.Formulas` (01 §3), estilos `ESTILO-*` (01 §6), recetas 7.x (01 §7), gotchas (01 §8).

**CONTRATO DE ENTRADA**:

| Qué | Contenido | Quién lo setea |
|---|---|---|
| `gvarUser` | Registro de `Users` del usuario actual | `App.OnStart` |
| `colMisPermisos` | Filas de `CH-Tbl-ClientDataChangePermissions` donde el usuario es miembro del grupo AAD | `App.OnStart` |
| `gvarRolTexto` | Texto del chip de rol del header | `App.OnStart` |
| `gvarSolicitudSel` | Registro **fresco** de `CH-Tbl-ClientDataChangeRequests` a mostrar | `scrSolicitudes` (guía 04), `App.OnStart` (deep-link §4 de 01), o `scrCliente` (guía 03, `btnFilaSolicitudFic.OnSelect` §8) |
| `gvarClienteSel` | `gvarSolicitudSel.RelatedClient` | mismos orígenes que arriba |

Sin `gvarSolicitudSel` seteado esta pantalla no tiene nada que mostrar — ningún flujo de
navegación de la app llega acá sin haberlo seteado primero (los tres orígenes de la tabla de
arriba lo garantizan).

**CONTRATO DE SALIDA**: ninguno hacia otras pantallas. Esta pantalla **mantiene
`gvarSolicitudSel` al día por sí misma** (re-`LookUp` tras cada `Patch`/llamada a la Custom
API) para que sus propios controles sigan coherentes sin recargar. `btnVolverDet` navega
SIEMPRE a `scrSolicitudes` (guía 04), que reconstruye su propio estado en `OnVisible` — ver
Notas sobre por qué no se usa `Back()`.

---

## Árbol de controles

```
scrSolicitud
├── cmpAppHeaderDet                Componente (cmpAppHeader, receta 7.2)
├── cntCuerpoDet                   Contenedor (manual) — fila con las 2 cards (receta 7.1)
│   ├── cntCardPrincipalDet           Contenedor (manual) — card izquierda, flex ~0.62
│   │   ├── cntBarraDet                  Contenedor (manual) — volver + título + pill estado
│   │   │   ├── btnVolverDet                Botón clásico          "‹ Volver"
│   │   │   ├── lblTituloBarraDet           Etiqueta clásica        "RFC-00341 · Edición…"
│   │   │   └── pilEstadoBarraDet           Botón clásico           badge de estado (7.3)
│   │   ├── shpLineaBarraDet             Rectángulo clásico      separador bajo la barra
│   │   ├── cntKvDet                     Contenedor (manual) — cabecera kv (Cliente/Tabla/…)
│   │   │   ├── lblKClienteDet              Etiqueta clásica        "Cliente"
│   │   │   ├── lblVClienteDet              Etiqueta clásica        valor
│   │   │   ├── lblKRegistroDet             Etiqueta clásica        "Registro"/"Tabla"
│   │   │   ├── lblVRegistroDet             Etiqueta clásica        valor
│   │   │   ├── lblKCreadaPorDet            Etiqueta clásica        "Creada por"
│   │   │   ├── lblVCreadaPorDet            Etiqueta clásica        valor
│   │   │   ├── lblKFechaDet                Etiqueta clásica        "Creada"/"Enviada"/"Resuelta"
│   │   │   ├── lblVFechaDet                Etiqueta clásica        valor
│   │   │   ├── lblKMotivoDet               Etiqueta clásica        "Motivo"
│   │   │   └── lblVMotivoDet               Etiqueta clásica        valor (Wrap)
│   │   ├── shpLineaKvDet                 Rectángulo clásico     separador bajo el kv
│   │   ├── cntBackendPendienteDet        Contenedor (manual) — banner 7.11, fallback §7.6 de 01
│   │   │   └── lblBackendPendienteDet       Etiqueta clásica
│   │   ├── cntErrorDet                   Contenedor (manual) — banner de error (SO4, 7.11)
│   │   │   ├── lblErrorDet                  Etiqueta clásica
│   │   │   └── btnActualizarErrorDet        Botón clásico           "↻ Actualizar detalle"
│   │   ├── cntToolbarLineasDet           Contenedor (manual) — Agregar/Quitar cambio (SO5)
│   │   │   ├── btnAgregarCambioDet          Botón clásico           "+ Agregar cambio"
│   │   │   ├── btnQuitarCambioDet           Botón clásico           "🗑 Quitar cambio…"
│   │   │   └── lblContadorLineasDet         Etiqueta clásica        "N cambios en la solicitud"
│   │   ├── cntGrillaLineasDet            Contenedor (manual) — grilla diff (receta 7.4)
│   │   │   ├── lblHdrCampoDet               Etiqueta clásica        "CAMPO"
│   │   │   ├── lblHdrActualDet              Etiqueta clásica        "VALOR ACTUAL"
│   │   │   ├── lblHdrPropuestoDet           Etiqueta clásica        "VALOR PROPUESTO"
│   │   │   ├── lblHdrEstadoLinDet           Etiqueta clásica        "ESTADO"
│   │   │   ├── shpHdrLineaLinDet            Rectángulo clásico      línea bajo encabezado
│   │   │   ├── galLineasDet                 Galería vertical en blanco
│   │   │   │   ├── shpSelLinDet                Rectángulo clásico   fondo de fila seleccionada (7.5)
│   │   │   │   ├── shpSelBarraLinDet           Rectángulo clásico   barra izq. de selección (7.5)
│   │   │   │   ├── btnFilaLinDet               Botón clásico        overlay clicable (FONDO, 7.4)
│   │   │   │   ├── lblCampoLinDet              Etiqueta clásica     nombre del campo
│   │   │   │   ├── lblActualLinDet             Etiqueta clásica     valor actual (tachado)
│   │   │   │   ├── lblPropuestoLinDet          Etiqueta clásica     valor propuesto (verde)
│   │   │   │   ├── pilEstadoLinDet             Botón clásico        badge estado de línea
│   │   │   │   └── shpSepLinDet                Rectángulo clásico   separador (7.10)
│   │   │   └── lblVacioLineasDet            Etiqueta clásica        estado vacío
│   │   ├── cntConflictoDet               Contenedor (manual) — banner de conflicto (SO5, 7.11)
│   │   │   └── lblConflictoDet              Etiqueta clásica
│   │   └── cntAccionesDet                Contenedor (manual) — pie con botones de acción
│   │       ├── btnRechazarDet               Botón clásico           "Rechazar…" (SO2)
│   │       ├── btnAprobarDet                Botón clásico           "Aprobar…" (SO2)
│   │       ├── btnCancelarSolicitudDet      Botón clásico           "Cancelar solicitud…" (SO5)
│   │       └── btnEnviarDet                 Botón clásico           "Enviar a aprobación…" (SO5)
│   └── cntCardLateralDet             Contenedor (manual) — card derecha, flex ~0.38
│       ├── lblTituloLateralDet          Etiqueta clásica        "Historial…"/"¿Quién aprueba?"
│       ├── cntHistorialDet              Contenedor (manual) — SO2/SO3/SO4
│       │   ├── galHistorialDet             Galería vertical en blanco — timeline
│       │   │   ├── shpDotHistDet              Rectángulo clásico   punto de línea de tiempo
│       │   │   ├── lblTitHistDet              Etiqueta clásica     título del evento
│       │   │   ├── lblSubHistDet              Etiqueta clásica     fecha · autor
│       │   │   ├── lblTxtHistDet              Etiqueta clásica     detalle (Wrap)
│       │   │   └── shpSepHistDet              Rectángulo clásico   separador
│       │   └── lblVacioHistorialDet        Etiqueta clásica        estado vacío
│       └── cntQuienApruebaDet           Contenedor (manual) — SO5
│           ├── cntBannerQuienApruebaDet    Contenedor (manual) — banner 7.11
│           │   └── lblBannerQuienApruebaDet   Etiqueta clásica
│           ├── lblSecLabQuienApruebaDet    Etiqueta clásica        "AL ENVIAR"
│           ├── cntTl1QuienApruebaDet       Contenedor (manual)
│           │   ├── shpDotTl1QuienApruebaDet   Rectángulo clásico
│           │   ├── lblTitTl1QuienApruebaDet   Etiqueta clásica
│           │   └── lblSubTl1QuienApruebaDet   Etiqueta clásica
│           └── cntTl2QuienApruebaDet       Contenedor (manual)
│               ├── shpDotTl2QuienApruebaDet   Rectángulo clásico
│               ├── lblTitTl2QuienApruebaDet   Etiqueta clásica
│               └── lblSubTl2QuienApruebaDet   Etiqueta clásica
├── cmpConfirmDialogDet            Componente (cmpConfirmDialog, receta 7.6) — Aprobar/Rechazar/
│                                  Enviar/Cancelar/Quitar-cambio Y el resultado síncrono (SO3)
  (panel "Agregar cambio", receta 7.7 — SO5, hijos directos de la pantalla)
├── shpVeloPanelDet                Rectángulo clásico      velo full-screen
├── btnVeloClicPanelDet            Botón clásico (invisible) cierra al clic afuera
└── cntPanelDet                    Contenedor (manual)     card del panel
        ├── cntPanelHeadDet               Contenedor (manual)
        │   ├── lblTituloPanelDet            Etiqueta clásica     "Agregar cambio"
        │   ├── lblSubPanelDet               Etiqueta clásica     subtítulo
        │   └── icoCerrarPanelDet            Icono clásico (Icon.Cancel)
        ├── cntPanelBodyDet               Contenedor (manual)
        │   ├── lblCampoLabelDet             Etiqueta clásica     "Campo"
        │   ├── drpCampoDet                  Menú desplegable clásico
        │   ├── lblValorActualLabelDet       Etiqueta clásica     "Valor actual"
        │   ├── lblValorActualDet            Etiqueta clásica     valor (solo lectura)
        │   ├── lblValorNuevoLabelDet        Etiqueta clásica     "Valor propuesto"
        │   ├── inpValorNuevoDet             Entrada de texto clásica (SingleLine)
        │   └── lblValorNuevoErrorDet        Etiqueta clásica     error (ESTILO-CAMPO-ERROR)
        └── cntPanelFootDet                Contenedor (manual)
            ├── btnCancelarPanelDet           Botón clásico        "Cancelar"
            └── btnConfirmarPanelDet          Botón clásico        "Agregar"
```

93 controles. **Regla de coordenadas del template** (gotcha 8.14): `galLineasDet` va en `X 0`
dentro de `cntGrillaLineasDet`, así que X de cada control del template = X de su encabezado
(`lblCampoLinDet.X = lblHdrCampoDet.X`, etc.).

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |

**`scrSolicitud.OnVisible`** — COMPLETO, listo para pegar:

```powerfx
// --- 0) Estado local a default (gotcha 8.3: nunca arrastrar estado de la visita anterior) ---
UpdateContext({
    ctxErrorDet: Blank(),
    ctxCargandoDet: false,
    ctxMostrarDlgDet: false,
    ctxModoDlgDet: "confirmar",
    ctxAccionDet: "",
    ctxPanelAgregarDet: false,
    ctxErrorPanelDet: Blank(),
    // Backend desplegado y conectado (2026-08-25): sanic_CHResolveChangeRequest ya existe
    // como datasource Environment. Se deja el nombre de la bandera (histórico del fallback
    // de 01 §7.6) pero el literal real en el fuente es "true" — ver Notas.
    ctxResolveApiListoDet: true
});
Reset(drpCampoDet);
Reset(inpValorNuevoDet);

// --- 1) Líneas del RFC ---
ClearCollect(
    colLineasDet,
    Filter(
        'CH-Tbl-ClientDataChangeRequestLines',
        ChangeRequestID.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest'
    )
);

// --- 1b) Catálogo de campos editables de la tabla objetivo (materializado en memoria para
//     que drpCampoDet.Items filtre local en §10.2 — ver Notas, fix de delegación de "in").
ClearCollect(
    colCatalogoDet,
    Filter('CH-Tbl-ClientTableLogicalFieldNames', Table = gvarSolicitudSel.TargetTable && IsAllowed)
);

// --- 2) Valor ACTUAL del registro objetivo, en vivo — UN SOLO LookUp con ShowColumns.
//     Gotcha 8.9 aplicado tambien a COLUMNAS: Power Fx no indirecciona un nombre de columna
//     por variable, asi que el Switch de tabla trae explicitamente solo lo necesario por
//     tabla (ClearCollect+If con else explicito, gotcha 8.13; en Create no hay registro aun).
//     colTargetRecordDet se normaliza a pares {LogicalName, Valor} — un Switch de esquemas
//     mixtos (columnas distintas por tabla) dentro de un solo Table() colapsaba el tipo de la
//     colección resultante (bug real encontrado en Studio); With()+ClearCollect por rama lo
//     evita, a costa de repetir el ClearCollect en cada rama del Switch (literales = LOGICAL
//     NAME de la entidad, D6 de 01 RESUELTA — ver 02 DC3 / 03 hallazgo crítico #3).
If(
    gvarSolicitudSel.Type <> 'Type (CH-Tbl-ClientDataChangeRequests)'.Create
        && !IsBlank(gvarSolicitudSel.TargetRecordID),
    Switch(
        gvarSolicitudSel.TargetTable,
        "sanic_chtblclient",
        With({_r: LookUp('CH-Tbl-Clients', 'CH-Tbl-Client' = GUID(gvarSolicitudSel.TargetRecordID))},
            ClearCollect(colTargetRecordDet,
                {LogicalName: "sanic_name", Valor: _r.Name},
                {LogicalName: "sanic_cifbco", Valor: Text(_r.CIFBCO)},
                {LogicalName: "sanic_cifcom", Valor: Text(_r.CIFCOM)}
            )
        ),
        "sanic_chtblclientbranch",
        With({_r: LookUp('CH-Tbl-ClientBranches', 'CH-Tbl-ClientBranch' = GUID(gvarSolicitudSel.TargetRecordID))},
            ClearCollect(colTargetRecordDet,
                {LogicalName: "sanic_name", Valor: _r.Name}
            )
        ),
        "sanic_chtblclientemail",
        With({_r: LookUp('CH-Tbl-ClientEmails', 'CH-Tbl-ClientEmail' = GUID(gvarSolicitudSel.TargetRecordID))},
            ClearCollect(colTargetRecordDet,
                {LogicalName: "emailaddress", Valor: _r.'Email Address'},
                {LogicalName: "sanic_alwaysnotify", Valor: If(_r.AlwaysNotify, "Sí", "No")}
            )
        ),
        "sanic_chtblclientaccount",
        With({_r: LookUp('CH-Tbl-ClientAccounts', 'CH-Tbl-ClientAccount' = GUID(gvarSolicitudSel.TargetRecordID))},
            ClearCollect(colTargetRecordDet,
                {LogicalName: "sanic_name", Valor: _r.'Numero de cuenta'},
                {LogicalName: "sanic_currency", Valor: Text(_r.Moneda)}
            )
        ),
        Clear(colTargetRecordDet)
    ),
    Clear(colTargetRecordDet)
);

// --- 3) Auditoria real (solo existe si el RFC ya paso por la Custom API) ---
ClearCollect(
    colAuditoriaDet,
    Sort(
        Filter(
            'CH-Tbl-ClientDataChangesAuditTrails',
            RelatedRFC.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest'
        ),
        'Created On', SortOrder.Descending
    )
);

// --- 4) Historial unificado (real + sintetico) — ver Notas sobre por que no hay "Enviada" ---
ClearCollect(
    colTimelineDet,
    {
        Titulo: "Creada (Borrador)",
        SubTitulo: Text(gvarSolicitudSel.'Created On', FmtFecha) & " · " & gvarSolicitudSel.'Created By'.'Full Name',
        Texto: "",
        Tono: "neutro",
        Orden: gvarSolicitudSel.'Created On'
    }
);
ForAll(
    colAuditoriaDet As A,
    Collect(
        colTimelineDet,
        {
            Titulo: If(A.Result, "Aplicada", "Falló la aplicación"),
            SubTitulo: Text(A.'Created On', FmtFecha) & " · " & A.'Created By'.'Full Name',
            Texto: A.Message,
            Tono: If(A.Result, "ok", "danger"),
            Orden: A.'Created On'
        }
    )
);
If(
    gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Canceled,
    Collect(
        colTimelineDet,
        {
            Titulo: "Cancelada",
            SubTitulo: Text(gvarSolicitudSel.SolvedOn, FmtFecha) & " · " & gvarSolicitudSel.SolvedBy.'Full Name',
            Texto: Coalesce(gvarSolicitudSel.ReasonForDecision, ""),
            Tono: "neutro",
            Orden: gvarSolicitudSel.SolvedOn
        }
    )
);

// --- 5) Conflictos por REGISTRO (mismo campo + mismo TargetRecordID + otro RFC en curso) ---
//     Solo tiene sentido para el creador en Draft (SO5) — se acota para no pagar este Filter
//     caro (no delegable) en los demas estados. Reescrito: ForAll POR LINEA con un Filter
//     delegable por GUID de Field Definition — nada de "in" server-side ni dot-walk sobre una
//     colección (el intento anterior, "'Field Definition'.LogicalName in colLineasDet.'Field
//     Definition'.LogicalName", no compilaba: "Not operator along with the Custom Named
//     Condition operators is not allowed").
Clear(colConflictosDet);
If(
    gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
        && gvarSolicitudSel.'Created By'.User = gvarUser.User,
    ForAll(
        colLineasDet As L,
        Collect(
            colConflictosDet,
            Filter(
                'CH-Tbl-ClientDataChangeRequestLines',
                ChangeRequestID.TargetRecordID = gvarSolicitudSel.TargetRecordID
                    && ChangeRequestID.'CH-Tbl-ClientDataChangeRequest' <> gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest'
                    && (ChangeStatus = 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft
                        || ChangeStatus = 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Submitted)
                    && 'Field Definition'.'CH-Tbl-ClientTableLogicalFieldName' = L.'Field Definition'.'CH-Tbl-ClientTableLogicalFieldName'
            )
        )
    )
);
```

> 🧨 El estado a comparar en la línea del OTRO RFC es **`Draft`/`Submitted`** (no
> `Submitted`/`Approved` como sugería una versión anterior de esta guía) — verificado en el
> fuente real: un conflicto tiene sentido mientras el OTRO cambio todavía puede resolverse o
> perderse (Draft/Submitted); una vez `Approved` ya se aplicó o está por aplicarse de forma
> irreversible vía la Custom API, así que dejó de ser un "conflicto evitable" en el sentido que
> advierte este banner.

---

## 2. Header

**`cmpAppHeaderDet`** — instancia de `cmpAppHeader` (receta 7.2):

```powerfx
cmpAppHeaderDet As cmpAppHeader:
    Activo: ="Solicitudes"
    Height: =fxHeaderH
    OnClientes: =Navigate(scrClientes, ScreenTransition.None)
    OnSolicitudes: =Navigate(scrSolicitudes, ScreenTransition.None)
    RolTexto: =gvarRolTexto
    Subtitulo: ="Detalle de solicitud"
    Titulo: ="Administración de Clientes"
    Width: =Parent.Width
    X: =0
    Y: =0
```

---

## 3. Cuerpo — layout de las 2 cards

**`cntCuerpoDet`** (Contenedor manual, hijo de la pantalla — receta 7.1):

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `fxHeaderH` |
| Width / Height | `Parent.Width` / `Parent.Height - fxHeaderH` |

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntCardPrincipalDet` | Contenedor (manual) | `fxGutter` | `fxGutter` | `(Parent.Width - fxGutter*2 - fxGap) * 0.62` | `Parent.Height - fxGutter*2` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `fxCardR` (las 4) |
| `cntCardLateralDet` | Contenedor (manual) | `cntCardPrincipalDet.X + cntCardPrincipalDet.Width + fxGap` | `fxGutter` | `Parent.Width - Self.X - fxGutter` | `cntCardPrincipalDet.Height` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `fxCardR` (las 4) |

> La fracción `0.62` traduce el `flex:1.6` / `flex:1` del mockup (1.6 / 2.6 ≈ 0.615, redondeado).

---

## 4. Card principal — barra superior y cabecera kv

**Todos los controles de esta sección son hijos de `cntCardPrincipalDet`** (`Parent` =
`cntCardPrincipalDet`).

### 4.1 Barra superior

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntBarraDet` | Contenedor (manual) | `0` | `0` | `Parent.Width` | `44` | Fill `ThemeSurface2` · RadiusTopLeft/TopRight `fxCardR - 1` · RadiusBottomLeft/BottomRight `0` |
| `btnVolverDet` | Botón clásico | `12` | `(Parent.Height - 26)/2` | `74` | `26` | ESTILO-BTN-CHICO · Text `"‹ Volver"` · OnSelect `=Navigate(scrSolicitudes, ScreenTransition.None)` |
| `lblTituloBarraDet` | Etiqueta clásica | `btnVolverDet.X + btnVolverDet.Width + 10` | `0` | `Parent.Width - Self.X - pilEstadoBarraDet.Width - 26` | `Parent.Height` | Text abajo (a) · Size `9.75` · Semibold |
| `pilEstadoBarraDet` | Botón clásico | `Parent.Width - 16 - Self.Width` | `(Parent.Height - 20)/2` | `Len(Self.Text) * 5.4 + 18` | `20` | badge 7.3 — fórmulas (b) |

**(a) `lblTituloBarraDet.Text`**:

```powerfx
=gvarSolicitudSel.Name & " · " &
    Switch(
        gvarSolicitudSel.Type,
        'Type (CH-Tbl-ClientDataChangeRequests)'.Update,     "Edición de " & Lower(Coalesce(gvarSolicitudSel.TargetTableDisplayName, gvarSolicitudSel.TargetTable)),
        'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate, "Desactivación de " & Lower(Coalesce(gvarSolicitudSel.TargetTableDisplayName, gvarSolicitudSel.TargetTable)),
        'Type (CH-Tbl-ClientDataChangeRequests)'.Create,     "Alta de " & Lower(Coalesce(gvarSolicitudSel.TargetTableDisplayName, gvarSolicitudSel.TargetTable)),
        'Type (CH-Tbl-ClientDataChangeRequests)'.Activate,   "Activación de " & Lower(Coalesce(gvarSolicitudSel.TargetTableDisplayName, gvarSolicitudSel.TargetTable)),
        "Solicitud de cambio"
    )
```

**(b) `pilEstadoBarraDet`** — copia escalar del mapa de ESTADO de 01 §7.3 evaluada sobre
`gvarSolicitudSel.'Change Status'` en vez de `ThisItem.'Change Status'` (01 §3 ya aclara que
estos mapas se pegan inline por pantalla). Base: `DisplayMode.View` · Radius `12` (las 4) ·
Size `7.88` · Semibold · **`BorderThickness: 0` en TODOS los tonos, incluido el neutro
(Borrador/Cancelada)** — se aparta del default `If(<tono>="neutro", 1, 0)` de la receta 7.3,
extendiendo acá la divergencia S11 de `04-pantalla-solicitudes.md` (mismo pill de ESTADO del
RFC: el CSS del mockup no define `border` en ninguna variante de `.pill`, verificado en el
`<style>` completo) · BorderColor `ThemeLineSoft` (inerte con thickness `0`, se deja seteado
por si S11 se revierte más adelante).

```powerfx
// Text / Fill / Color: MISMO Switch de 01 §7.3 "Mapa de texto/tono para el ESTADO del RFC",
// reemplazando ThisItem.'Change Status' por gvarSolicitudSel.'Change Status'.
```

### 4.2 Separador y cabecera kv

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpLineaBarraDet` | Rectángulo clásico | `0` | `cntBarraDet.Y + cntBarraDet.Height` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |
| `cntKvDet` | Contenedor (manual) | `0` | `shpLineaBarraDet.Y + 1` | `Parent.Width` | `lblVMotivoDet.Y + lblVMotivoDet.Height + 12` | Fill `Transparent` |

Dentro de `cntKvDet` (`Parent` = `cntKvDet`), 2 columnas (`colIzqX = 16`,
`colDerX = Parent.Width/2 + 8`), filas de `22` con label (`ESTILO-DATO-K`, Width `78`) +
valor (`ESTILO-DATO-V` con **`Align: Align.Left`** — el mockup sobre-escribe el `text-align:
right` default de la receta en este bloque específico, ver Notas):

| Control | X | Y | Width | Height | Text |
|---|---|---|---|---|---|
| `lblKClienteDet` | `16` | `14` | `78` | `18` | `="Cliente"` |
| `lblVClienteDet` | `98` | `14` | `Parent.Width/2 - 106` | `18` | `=Coalesce(gvarClienteSel.Name, "—")` |
| `lblKRegistroDet` | `Parent.Width/2 + 8` | `14` | `78` | `18` | fórmula (c) |
| `lblVRegistroDet` | `Parent.Width/2 + 90` | `14` | `Parent.Width/2 - 106` | `18` | fórmula (d) |
| `lblKCreadaPorDet` | `16` | `40` | `78` | `18` | `="Creada por"` |
| `lblVCreadaPorDet` | `98` | `40` | `Parent.Width/2 - 106` | `18` | `=gvarSolicitudSel.'Created By'.'Full Name'` |
| `lblKFechaDet` | `Parent.Width/2 + 8` | `40` | `78` | `18` | fórmula (e) |
| `lblVFechaDet` | `Parent.Width/2 + 90` | `40` | `Parent.Width/2 - 106` | `18` | fórmula (f) |
| `lblKMotivoDet` | `16` | `66` | `78` | `18` | `="Motivo"` |
| `lblVMotivoDet` | `98` | `66` | `Parent.Width - 114` | *(ver nota)* | `=Coalesce(gvarSolicitudSel.Reason, "—")` · `Wrap: true` · `AutoHeight: true` (gotcha 8.15) |

**(c) `lblKRegistroDet.Text`** — evita redundancia cuando el RFC edita el cliente mismo:

```powerfx
=If(gvarSolicitudSel.TargetTable = "sanic_chtblclient", "Tabla", "Registro")
```

**(d) `lblVRegistroDet.Text`** — con `colTargetRecordDet` normalizado a pares
`{LogicalName, Valor}` (§1 paso 2), la lectura es un `LookUp` directo por `LogicalName` en vez
del `Switch` por tabla que exigía el esquema mixto anterior; el único caso especial que queda
es `"sanic_chtblclientemail"`, cuyo `LogicalName` de nombre es `"emailaddress"` en vez de
`"sanic_name"` (fuente real, `scrSolicitud.fx.yaml`):

```powerfx
=If(
    gvarSolicitudSel.Type = 'Type (CH-Tbl-ClientDataChangeRequests)'.Create,
    "(registro nuevo — se crea al aprobar)",
    Coalesce(
        LookUp(
            colTargetRecordDet,
            LogicalName = If(
                gvarSolicitudSel.TargetTable = "sanic_chtblclientemail",
                "emailaddress",
                "sanic_name"
            )
        ).Valor,
        "—"
    )
)
```

> A diferencia de una versión anterior de esta guía, esta fórmula YA NO distingue
> `"sanic_chtblclient"` como caso aparte con `TargetTableDisplayName` — el registro objetivo
> Cliente también queda en `colTargetRecordDet` con `LogicalName: "sanic_name"` (§1 paso 2), así
> que el mismo `LookUp` genérico lo resuelve sin rama especial.

**(e) `lblKFechaDet.Text`**:

```powerfx
=Switch(
    gvarSolicitudSel.'Change Status',
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft, "Creada",
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted, "Enviada",
    "Resuelta"
)
```

**(f) `lblVFechaDet.Text`**:

```powerfx
=Text(
    If(
        gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,
        gvarSolicitudSel.'Created On',
        Coalesce(gvarSolicitudSel.SolvedOn, gvarSolicitudSel.'Created On')
    ),
    FmtFecha
)
```

> Fecha SIEMPRE con `FmtFecha` (`HH`, gotcha 8.1). `lblVMotivoDet.Height` queda `AutoHeight` —
> `cntKvDet.Height` referencia a `lblVMotivoDet.Y + lblVMotivoDet.Height + 12` así que crece
> con el texto sin números mágicos.

---

## 5. Card principal — banners (backend pendiente / error) y toolbar de líneas

Hijos de `cntCardPrincipalDet`, apilados debajo de `cntKvDet` con `shpLineaKvDet` como
separador.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpLineaKvDet` | Rectángulo clásico | `0` | `cntKvDet.Y + cntKvDet.Height` | `Parent.Width` | `1` | Fill `ThemeLine` |

### 5.1 Banner "backend pendiente" (fallback de 01 §7.6)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntBackendPendienteDet` | Contenedor (manual, receta 7.11) | `12` | `shpLineaKvDet.Y + 1 + 10` | `Parent.Width - 24` | `lblBackendPendienteDet.Height + 18` | Fill `ThemeWarnSoft` · Radius `9` (las 4) · Visible fórmula (g) |
| `lblBackendPendienteDet` | Etiqueta clásica | `13` | `9` | `Parent.Width - 26` | *(AutoHeight)* | Text `"Backend pendiente: la Custom API sanic_CHResolveChangeRequest todavía no está desplegada."` · Size `8.63` · Color `ThemeWarn` · `Wrap: true` · `AutoHeight: true` |

**(g) `cntBackendPendienteDet.Visible`** — solo tiene sentido si de otro modo se vería
Aprobar/Rechazar:

```powerfx
=!ctxResolveApiListoDet
    && gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
    && !IsBlank(LookUp(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = gvarSolicitudSel.TargetTable))
    && gvarSolicitudSel.'Created By'.User <> gvarUser.User
```

### 5.2 Banner de error (SO4)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntErrorDet` | Contenedor (manual, receta 7.11) | `12` | `cntBackendPendienteDet.Y + If(cntBackendPendienteDet.Visible, cntBackendPendienteDet.Height + 10, 0)` | `Parent.Width - 24` | `lblErrorDet.Height + 46` | Fill `ThemeDangerSoft` · Radius `9` (las 4) · Visible `=!IsBlank(ctxErrorDet)` |
| `lblErrorDet` | Etiqueta clásica | `13` | `9` | `Parent.Width - 26` | *(AutoHeight)* | Text `="No se pudo completar la acción: " & ctxErrorDet` · Size `8.63` · Color `ThemeDanger` · `Wrap: true` · `AutoHeight: true` |
| `btnActualizarErrorDet` | Botón clásico | `13` | `lblErrorDet.Y + lblErrorDet.Height + 8` | `140` | `26` | ESTILO-BTN-CONTORNO · Text `"↻ Actualizar detalle"` · OnSelect fórmula (h) |

**(h) `btnActualizarErrorDet.OnSelect`** — `Select(scrSolicitud)` NO existe como forma de
re-disparar `OnVisible` (no es un patrón válido de canvas apps). La corrección real recarga
INLINE, tras refrescar `gvarSolicitudSel` desde el datasource, solo `colLineasDet` +
`colAuditoriaDet` + `colTimelineDet` (no repite `colTargetRecordDet` ni `colConflictosDet` —
en el camino de error esos dos no son la causa típica del fallo y su recarga es más cara;
si hiciera falta refrescarlos también, repetir los pasos 1b/2/5 completos del `OnVisible`).
Fuente real (`scrSolicitud.fx.yaml`):

```powerfx
=Set(
    gvarSolicitudSel,
    LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')
);
Set(gvarClienteSel, gvarSolicitudSel.RelatedClient);
UpdateContext({ctxErrorDet: Blank()});
ClearCollect(
    colLineasDet,
    Filter(
        'CH-Tbl-ClientDataChangeRequestLines',
        ChangeRequestID.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest'
    )
);
ClearCollect(
    colAuditoriaDet,
    Sort(
        Filter(
            'CH-Tbl-ClientDataChangesAuditTrails',
            RelatedRFC.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest'
        ),
        'Created On', SortOrder.Descending
    )
);
ClearCollect(
    colTimelineDet,
    {
        Titulo: "Creada (Borrador)",
        SubTitulo: Text(gvarSolicitudSel.'Created On', FmtFecha) & " · " & gvarSolicitudSel.'Created By'.'Full Name',
        Texto: "",
        Tono: "neutro",
        Orden: gvarSolicitudSel.'Created On'
    }
);
ForAll(
    colAuditoriaDet As A,
    Collect(
        colTimelineDet,
        {
            Titulo: If(A.Result, "Aplicada", "Falló la aplicación"),
            SubTitulo: Text(A.'Created On', FmtFecha) & " · " & A.'Created By'.'Full Name',
            Texto: A.Message,
            Tono: If(A.Result, "ok", "danger"),
            Orden: A.'Created On'
        }
    )
)
```

### 5.3 Toolbar Agregar/Quitar cambio (SO5)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntToolbarLineasDet` | Contenedor (manual) | `0` | `cntErrorDet.Y + If(cntErrorDet.Visible, cntErrorDet.Height + 10, 0)` | `Parent.Width` | `34` | Fill `Transparent` · Visible fórmula (i) |
| `btnAgregarCambioDet` | Botón clásico | `12` | `4` | `120` | `26` | ESTILO-BTN-CHICO-ACC · Text `"+ Agregar cambio"` · OnSelect `=UpdateContext({ctxPanelAgregarDet: true, ctxErrorPanelDet: Blank()}); Reset(drpCampoDet); Reset(inpValorNuevoDet)` |
| `btnQuitarCambioDet` | Botón clásico | `btnAgregarCambioDet.X + btnAgregarCambioDet.Width + 8` | `4` | `130` | `26` | ESTILO-BTN-CHICO-DAN · Text `"🗑 Quitar cambio…"` · `DisplayMode: =If(IsBlank(galLineasDet.Selected), DisplayMode.Disabled, DisplayMode.Edit)` · OnSelect `=UpdateContext({ctxAccionDet: "quitarlinea", ctxModoDlgDet: "confirmar", ctxMostrarDlgDet: true})` |
| `lblContadorLineasDet` | Etiqueta clásica | `Parent.Width - 16 - Self.Width` | `4` | `160` | `26` | Text `=CountRows(colLineasDet) & " cambios en la solicitud"` · ESTILO-META · Align `Align.Right` |

**(i) `cntToolbarLineasDet.Visible`**:

```powerfx
=gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
    && gvarSolicitudSel.'Created By'.User = gvarUser.User
```

---

## 6. Card principal — grilla de líneas (diff actual → propuesto)

**`cntGrillaLineasDet`** (Contenedor manual, hijo de `cntCardPrincipalDet` — receta 7.4):

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `cntToolbarLineasDet.Y + If(cntToolbarLineasDet.Visible, cntToolbarLineasDet.Height, 0)` |
| Width / Height | `Parent.Width` / `cntAccionesDet.Y - Self.Y - (If(cntConflictoDet.Visible, cntConflictoDet.Height + 10, 0))` |
| Visible | `=IsBlank(ctxErrorDet)` — SO4 esconde la grilla y muestra solo el banner de §5.2 |

Fracciones de columna sobre `(Parent.Width - 32)`, traducción de `1.4fr 1.4fr 1.4fr .8fr`:
Campo `.28` · Valor actual `.28` · Valor propuesto `.28` · Estado `.16`.

### 6.1 Encabezado

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblHdrCampoDet` | Etiqueta clásica | `16` | `8` | `(Parent.Width - 32) * 0.28` | `20` | Text `"CAMPO"` · ESTILO-HEADER-COL |
| `lblHdrActualDet` | Etiqueta clásica | `lblHdrCampoDet.X + lblHdrCampoDet.Width` | `8` | `(Parent.Width - 32) * 0.28` | `20` | Text `"VALOR ACTUAL"` · ESTILO-HEADER-COL |
| `lblHdrPropuestoDet` | Etiqueta clásica | `lblHdrActualDet.X + lblHdrActualDet.Width` | `8` | `(Parent.Width - 32) * 0.28` | `20` | Text `"VALOR PROPUESTO"` · ESTILO-HEADER-COL |
| `lblHdrEstadoLinDet` | Etiqueta clásica | `lblHdrPropuestoDet.X + lblHdrPropuestoDet.Width` | `8` | `(Parent.Width - 32) * 0.16` | `20` | Text `"ESTADO"` · ESTILO-HEADER-COL |
| `shpHdrLineaLinDet` | Rectángulo clásico | `0` | `30` | `Parent.Width` | `1` | Fill `ThemeLine` |

### 6.2 Galería

**`galLineasDet`**:

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `shpHdrLineaLinDet.Y + shpHdrLineaLinDet.Height` |
| Width / Height | `Parent.Width` / `Parent.Height - Self.Y` |
| TemplateSize / TemplatePadding | `fxRowHChica` / `0` |
| Items | `=colLineasDet` |

**Template** — insertar PRIMERO `shpSelLinDet`, luego `shpSelBarraLinDet`, luego
`btnFilaLinDet` (fondo del z-order, receta 7.4/gotcha 8.4), y encima el resto. Todos delegan
el clic con `OnSelect: =Select(Parent)`.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpSelLinDet` | Rectángulo clásico | `0` | `0` | `galLineasDet.Width` | `fxRowHChica` | Fill `=If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` (receta 7.5) |
| `shpSelBarraLinDet` | Rectángulo clásico | `0` | `0` | `3` | `fxRowHChica` | Fill `ThemeAccent` · Visible `=ThisItem.IsSelected` |
| `btnFilaLinDet` | Botón clásico | `0` | `0` | `galLineasDet.Width` | `fxRowHChica` | Text `""` · Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · OnSelect `=Select(Parent)` |
| `lblCampoLinDet` | Etiqueta clásica | `lblHdrCampoDet.X` | `0` | `lblHdrCampoDet.Width` | `fxRowHChica` | Text `=ThisItem.'Field Definition'.Name` · ESTILO-CELDA · Semibold · OnSelect `=Select(Parent)` |
| `lblActualLinDet` | Etiqueta clásica | `lblHdrActualDet.X` | `0` | `lblHdrActualDet.Width` | `fxRowHChica` | Text fórmula (j) · ESTILO-CELDA · Color `ThemeInk3` · `Strikethrough: true` (receta 7.12) · OnSelect `=Select(Parent)` |
| `lblPropuestoLinDet` | Etiqueta clásica | `lblHdrPropuestoDet.X` | `0` | `lblHdrPropuestoDet.Width` | `fxRowHChica` | Text `=Coalesce(ThisItem.NewValue, "—")` · ESTILO-CELDA · Semibold · Color `ThemeOk` (receta 7.12) · OnSelect `=Select(Parent)` |
| `pilEstadoLinDet` | Botón clásico | `lblHdrEstadoLinDet.X` | `(fxRowHChica - 20)/2` | `84` | `20` | badge — fórmulas (k) · OnSelect `=Select(Parent)` |
| `shpSepLinDet` | Rectángulo clásico | `16` | `fxRowHChica - 1` | `galLineasDet.Width - 32` | `1` | Fill `ThemeLineSoft` (receta 7.10) |

**(j) `lblActualLinDet.Text`** — usa el valor EN VIVO ya cargado en `colTargetRecordDet`
(§1 paso 2), no el `OldValue` guardado (que puede haber quedado desactualizado entre la
creación del RFC y hoy — mismo criterio del mockup SO2: "el valor actual se lee en vivo").
Con `colTargetRecordDet` normalizado a pares `{LogicalName, Valor}`, esta fórmula se reduce a
un `LookUp` genérico por `'Field Definition'.LogicalName` de la línea — YA NO hace falta el
`Switch` por columna (el `Switch` de 6 ramas de una versión anterior de esta guía asumía el
esquema mixto viejo, donde `colTargetRecordDet` tenía columnas distintas por tabla; con el
esquema normalizado ese `Switch` es redundante). Fuente real (`scrSolicitud.fx.yaml`):

```powerfx
=Coalesce(
    LookUp(colTargetRecordDet, LogicalName = ThisItem.'Field Definition'.LogicalName).Valor,
    Coalesce(ThisItem.OldValue, "—") // Create (sin registro objetivo aun) usa el snapshot guardado
)
```

**(k) `pilEstadoLinDet`** — MISMO mapa de tono de 01 §7.3, pero sobre el enum de **LÍNEA**
(gotcha 8.8: `.Denied`, no `.Rejected`):

```powerfx
// Text
=Switch(
    ThisItem.ChangeStatus,
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft,     "Borrador",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Submitted, "Enviada",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Approved,  "Aprobada",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Denied,    "Rechazada",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Applied,   "Aplicada",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Failed,    "Fallida",
    'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Canceled,  "Cancelada",
    "—"
)
// Fill / Color: mismo mapa de tono que 01 §7.3 (Draft/Canceled = neutro; Submitted = warn;
// Approved = info; Denied/Failed = danger; Applied = ok)
// BorderThickness: =0 en TODOS los tonos — se aparta del default "neutro con borde" de la
// receta 7.3, extendiendo la divergencia S11 de 04-pantalla-solicitudes.md a este pill (mismo
// dato de ESTADO del RFC/línea; el CSS del mockup no define `border` en ninguna variante de
// `.pill`, verificado en el <style> completo)
```

> 🧨 Igual que en 04 §5.2 (gotcha 8.4): `pilEstadoLinDet.DisplayMode = DisplayMode.Edit`, con
> `HoverFill`/`PressedFill` iguales al `Fill` — un botón en `View` no deja pasar el clic.

### 6.3 Estado vacío

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblVacioLineasDet` | Etiqueta clásica | `0` | `galLineasDet.Y + 40` | `Parent.Width` | `36` | ESTILO-VACIO · `Wrap: true` · Visible `=CountRows(colLineasDet) = 0` · Text `="Esta solicitud todavía no tiene cambios."` |

---

## 7. Card principal — banner de conflicto y acciones

Hijos de `cntCardPrincipalDet`.

### 7.1 Banner de conflicto por registro (SO5, receta 7.11)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntConflictoDet` | Contenedor (manual) | `12` | `cntAccionesDet.Y - Self.Height - 10` | `Parent.Width - 24` | `lblConflictoDet.Height + 18` | Fill `ThemeWarnSoft` · Radius `9` (las 4) · Visible `=!IsEmpty(colConflictosDet)` |
| `lblConflictoDet` | Etiqueta clásica | `13` | `9` | `Parent.Width - 26` | *(AutoHeight)* | Text fórmula (l) · Size `8.63` · Color `ThemeWarn` · `Wrap: true` · `AutoHeight: true` |

**(l) `lblConflictoDet.Text`**:

```powerfx
=With(
    {primero: First(colConflictosDet)},
    "⚠ Otra solicitud en curso (" & primero.ChangeRequestID.Name & ", de " &
        primero.ChangeRequestID.'Created By'.'Full Name' &
        ") ya propone cambios sobre este mismo registro. Revisala antes de enviar para no pisarse." &
        If(CountRows(colConflictosDet) > 1, " (+" & (CountRows(colConflictosDet) - 1) & " más)", "")
)
```

> Es un AVISO, no un bloqueo: el guard server-side (`GuardChangeRequestLinePlugin`) no exige
> esta regla — bloquear en el cliente algo que el servidor no aplica sería falsa seguridad
> (ver Notas).

### 7.2 Acciones (pie de la card)

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntAccionesDet` | Contenedor (manual) | `0` | `Parent.Height - 52` | `Parent.Width` | `52` | Fill `Transparent` · BorderColor `ThemeLine` (superior, `1`) |
| `btnAprobarDet` | Botón clásico | `Parent.Width - 16 - Self.Width` | `10` | `110` | `32` | ESTILO-BTN-OK · Text `"Aprobar…"` · Visible fórmula (m) · `DisplayMode: =If(ctxResolveApiListoDet, DisplayMode.Edit, DisplayMode.Disabled)` · OnSelect `=UpdateContext({ctxAccionDet: "aprobar", ctxModoDlgDet: "confirmar", ctxMostrarDlgDet: true})` |
| `btnRechazarDet` | Botón clásico | `btnAprobarDet.X - 8 - Self.Width` | `10` | `110` | `32` | ESTILO-BTN-PELIGRO · Text `"Rechazar…"` · Visible `=btnAprobarDet.Visible` · `DisplayMode: =btnAprobarDet.DisplayMode` · OnSelect `=UpdateContext({ctxAccionDet: "rechazar", ctxModoDlgDet: "confirmar", ctxMostrarDlgDet: true})` |
| `btnEnviarDet` | Botón clásico | `Parent.Width - 16 - Self.Width` | `10` | `150` | `32` | ESTILO-BTN-PRIMARIO · Text `"Enviar a aprobación…"` · Visible fórmula (n) · OnSelect `=UpdateContext({ctxAccionDet: "enviar", ctxModoDlgDet: "confirmar", ctxMostrarDlgDet: true})` |
| `btnCancelarSolicitudDet` | Botón clásico | `btnEnviarDet.X - 8 - Self.Width` | `10` | `150` | `32` | ESTILO-BTN-FANTASMA · Text `"Cancelar solicitud…"` · Visible `=btnEnviarDet.Visible` · OnSelect `=UpdateContext({ctxAccionDet: "cancelar", ctxModoDlgDet: "confirmar", ctxMostrarDlgDet: true})` |

**(m) `btnAprobarDet.Visible`**:

```powerfx
=gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
    && !IsBlank(LookUp(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver && Name = gvarSolicitudSel.TargetTable))
    && gvarSolicitudSel.'Created By'.User <> gvarUser.User
```

**(n) `btnEnviarDet.Visible`** (idéntica a `cntToolbarLineasDet.Visible` de §5.3):

```powerfx
=gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
    && gvarSolicitudSel.'Created By'.User = gvarUser.User
```

---

## 8. Card lateral — historial y "¿Quién aprueba?"

**Todos los controles de esta sección son hijos de `cntCardLateralDet`.**

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblTituloLateralDet` | Etiqueta clásica | `16` | `12` | `Parent.Width - 32` | `20` | Text `=If(cntQuienApruebaDet.Visible, "¿Quién aprueba?", "Historial de la solicitud")` · ESTILO-SUBTITULO · Semibold |

### 8.1 Historial (SO2/SO3/SO4)

**`cntHistorialDet`**:

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `lblTituloLateralDet.Y + lblTituloLateralDet.Height + 6` |
| Width / Height | `Parent.Width` / `Parent.Height - Self.Y` |
| Visible | `=!cntQuienApruebaDet.Visible` |

**`galHistorialDet`**: X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` ·
`TemplateSize` dinámico — `lblTxtHistDet.AutoHeight` **no** soportado dentro de galería de
altura fija; en su lugar, fijar `TemplateSize: =fxRowHHist` (constante `78` de 01 §3, suficiente
para 3 líneas de `lblTxtHistDet`) y `Wrap: true` sin `AutoHeight` en el template. Items:

```powerfx
=Sort(colTimelineDet, Orden, SortOrder.Descending)
```

**Template** (`Parent` = `galHistorialDet`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpDotHistDet` | Rectángulo clásico | `0` | `6` | `9` | `9` | Radius `5` (las 4) · Fill fórmula (o) |
| `lblTitHistDet` | Etiqueta clásica | `20` | `0` | `Parent.Width - 20` | `16` | Text `=ThisItem.Titulo` · Size `9.75` · Semibold |
| `lblSubHistDet` | Etiqueta clásica | `20` | `16` | `Parent.Width - 20` | `14` | Text `=ThisItem.SubTitulo` · Size `7.88` · Color `ThemeInk3` |
| `lblTxtHistDet` | Etiqueta clásica | `20` | `30` | `Parent.Width - 20` | `40` | Text `=ThisItem.Texto` · Size `8.63` · Color `ThemeInk2` · `Wrap: true` · Visible `=!IsBlank(ThisItem.Texto)` |
| `shpSepHistDet` | Rectángulo clásico | `0` | `77` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

**(o) `shpDotHistDet.Fill`**: mismo mapa de tono sólido de 01 §7.3 aplicado a
`ThisItem.Tono`.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblVacioHistorialDet` | Etiqueta clásica | `0` | `40` | `Parent.Width` | `36` | ESTILO-VACIO · Visible `=CountRows(colTimelineDet) = 0` · Text `="Sin eventos registrados."` |

### 8.2 "¿Quién aprueba?" (SO5)

**`cntQuienApruebaDet`**:

| Propiedad | Valor |
|---|---|
| X / Y | `0` / `cntHistorialDet.Y` |
| Width / Height | `cntHistorialDet.Width` / `cntHistorialDet.Height` |
| Visible | `=gvarSolicitudSel.'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft && gvarSolicitudSel.'Created By'.User = gvarUser.User` |

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntBannerQuienApruebaDet` | Contenedor (manual, 7.11) | `16` | `0` | `Parent.Width - 32` | `lblBannerQuienApruebaDet.Height + 18` | Fill `ThemeInfoSoft` · Radius `9` (las 4) |
| `lblBannerQuienApruebaDet` | Etiqueta clásica | `13` | `9` | `Parent.Width - 26` | *(AutoHeight)* | Text fórmula (p) · Size `8.63` · Color `ThemeInfo` · `Wrap: true` · `AutoHeight: true` |
| `lblSecLabQuienApruebaDet` | Etiqueta clásica | `16` | `cntBannerQuienApruebaDet.Y + cntBannerQuienApruebaDet.Height + 10` | `Parent.Width - 32` | `16` | Text `="AL ENVIAR"` · ESTILO-SECCION |
| `cntTl1QuienApruebaDet` | Contenedor (manual) | `16` | `lblSecLabQuienApruebaDet.Y + lblSecLabQuienApruebaDet.Height + 6` | `Parent.Width - 32` | `40` | Fill `Transparent` |
| `cntTl2QuienApruebaDet` | Contenedor (manual) | `16` | `cntTl1QuienApruebaDet.Y + cntTl1QuienApruebaDet.Height` | `Parent.Width - 32` | `40` | Fill `Transparent` |

**(p) `lblBannerQuienApruebaDet.Text`**:

```powerfx
=With(
    {area: LookUp(fxAreaPorTabla, Tabla = gvarSolicitudSel.TargetTable).Area},
    "Esta solicitud la decide un Approver de " &
        Coalesce(gvarSolicitudSel.TargetTableDisplayName, gvarSolicitudSel.TargetTable) &
        If(!IsBlank(area), " (" & area & ").", ".") &
        " Vos no podés aprobar tu propia solicitud — lo valida también el servidor."
)
```

Dentro de `cntTl1QuienApruebaDet` / `cntTl2QuienApruebaDet` (mismo patrón que 8.1, sin
`shpSep`): `shpDotTlNQuienApruebaDet` (Rectángulo, X `0` Y `6` W `9` H `9` Radius `5`, Fill
`ThemeInfo`), `lblTitTlNQuienApruebaDet` (Etiqueta, X `20` Y `0` W `Parent.Width - 20` H `16`,
Size `9.75` Semibold), `lblSubTlNQuienApruebaDet` (Etiqueta, X `20` Y `16` W `Parent.Width -
20` H `24`, Size `8.25` Color `ThemeInk3` `Wrap: true`). Textos:

| Control | Text |
|---|---|
| `lblTitTl1QuienApruebaDet` | `="Se notifica al chat de Approvers"` |
| `lblSubTl1QuienApruebaDet` | `="Adaptive Card en Teams con el detalle y link directo"` |
| `lblTitTl2QuienApruebaDet` | `="Queda bloqueada la edición de líneas"` |
| `lblSubTl2QuienApruebaDet` | `="hasta que se resuelva o se cancele"` |

---

## 9. `cmpConfirmDialogDet` — confirmación (Aprobar/Rechazar/Enviar/Cancelar/Quitar) y resultado (SO3)

Instancia de `cmpConfirmDialog` (receta 7.6), hija de la pantalla (`X 0 · Y 0 · Width
Parent.Width · Height Parent.Height`, overlay a pantalla completa). **Una sola instancia
reutilizada en 6 situaciones** (5 confirmaciones + 1 resultado), gobernada por `ctxAccionDet`
("aprobar"/"rechazar"/"enviar"/"cancelar"/"quitarlinea") y `ctxModoDlgDet`
("confirmar"/"resultado").

```powerfx
cmpConfirmDialogDet As cmpConfirmDialog:
    Mostrar: =ctxMostrarDlgDet
    Cargando: =ctxCargandoDet
    Titulo: <fórmula (q)>
    Texto: <fórmula (r)>
    MostrarCampo: <fórmula (s)>
    EtiquetaCampo: =Switch(ctxAccionDet, "cancelar", "Motivo de la cancelación", "Comentario de la decisión")
    ValorCampo: =""
    Tono: =Switch(ctxAccionDet, "aprobar", "ok", "enviar", "info", "info-tono-default", "info", "rechazar", "danger", "cancelar", "danger", "quitarlinea", "danger", "info")
    TextoOk: <fórmula (t)>
    TextoCancelar: ="Cancelar"
    OkHabilitado: <fórmula (u)>
    OnOk: <fórmula (v) — ver bloque completo abajo>
    OnCancelar: =UpdateContext({ctxMostrarDlgDet: false, ctxAccionDet: "", ctxModoDlgDet: "confirmar"})
```

> El `Switch` de `Tono` tiene una rama redundante (`"info-tono-default"`) que nunca matchea —
> es intencional para que el `Switch` quede con firma clara "acción → tono" antes de la rama
> default; si tu Studio marca la rama muerta como advertencia, borrala (el default ya cubre
> "enviar").

**(q) `Titulo`**:

```powerfx
=If(
    ctxModoDlgDet = "resultado",
    "Solicitud " & gvarSolicitudSel.Name & " " & If(ctxAccionDet = "aprobar", "aplicada", "rechazada"),
    Switch(
        ctxAccionDet,
        "aprobar", "Confirmar aprobación de RFC",
        "rechazar", "Confirmar rechazo de RFC",
        "enviar", "Confirmar envío de RFC",
        "cancelar", "Cancelar solicitud",
        "quitarlinea", "Quitar cambio de la solicitud",
        ""
    )
)
```

**(r) `Texto`** — compuesto CLIENTE-SIDE con datos ya conocidos en pantalla; el resultado de
`Environment.sanic_CHResolveChangeRequest` NO se captura en ningún `ctx*` (ver `OnOk` (v) más
abajo) ni se usa como texto principal (ver Notas — el `Message` real de la Custom API es
técnico, no la frase amigable del mockup):

```powerfx
=If(
    ctxModoDlgDet = "resultado",
    If(
        ctxAccionDet = "aprobar",
        "El cambio se aplicó al maestro. La bitácora registró el resultado y se notificó a " &
            gvarSolicitudSel.'Created By'.'Full Name' & ".",
        "La solicitud quedó rechazada. Se notificó a " & gvarSolicitudSel.'Created By'.'Full Name' & "."
    ),
    Switch(
        ctxAccionDet,
        "aprobar", "Se aprobará " & gvarSolicitudSel.Name & " y el cambio se aplicará al maestro de inmediato (Custom API síncrona y transaccional).",
        "rechazar", "Se rechazará " & gvarSolicitudSel.Name & ". El creador será notificado.",
        "enviar", "Una vez enviada no vas a poder editar esta solicitud. ¿Confirmás el envío?",
        "cancelar", "Se cancelará " & gvarSolicitudSel.Name & ". Esta acción no se puede deshacer.",
        "quitarlinea", "Se eliminará el cambio sobre «" & galLineasDet.Selected.'Field Definition'.Name & "» de esta solicitud.",
        ""
    )
)
```

**(s) `MostrarCampo`**:

```powerfx
=If(
    ctxModoDlgDet = "resultado", false,
    ctxAccionDet = "aprobar" || ctxAccionDet = "rechazar" || ctxAccionDet = "cancelar"
)
```

**(t) `TextoOk`**:

```powerfx
=If(
    ctxModoDlgDet = "resultado", "Entendido",
    Switch(
        ctxAccionDet,
        "aprobar", "Aprobar",
        "rechazar", "Rechazar",
        "enviar", "Enviar",
        "cancelar", "Cancelar solicitud",
        "quitarlinea", "Quitar",
        "Confirmar"
    )
)
```

**(u) `OkHabilitado`**:

```powerfx
=If(
    ctxModoDlgDet = "resultado", true,
    If(
        ctxAccionDet = "aprobar" || ctxAccionDet = "rechazar" || ctxAccionDet = "cancelar",
        Len(Trim(cmpConfirmDialogDet.CampoTexto)) > 0,
        true
    )
)
```

**(v) `OnOk`** — COMPLETO, listo para pegar. **Backend desplegado y conectado (2026-08-25)**:
`Environment.sanic_CHResolveChangeRequest` ya existe como función tipada en Studio (ver
"Dependencias de backend" en las Notas al final) — las ramas `aprobar`/`rechazar` de abajo van
tal cual, sin stub. Se conserva la nota histórica: si en algún entorno el conector
`Environment` se agrega ANTES del deploy del backend, hace falta actualizarlo desde Datos →
orígenes de datos para que Studio regenere la función, y mientras tanto esas 2 ramas pueden ir
con un stub `UpdateContext({ctxErrorDet: "Backend pendiente…"})`:

```powerfx
=If(
    ctxModoDlgDet = "resultado",
    UpdateContext({ctxMostrarDlgDet: false, ctxModoDlgDet: "confirmar", ctxAccionDet: ""}),

    ctxAccionDet = "aprobar" || ctxAccionDet = "rechazar",
    UpdateContext({ctxCargandoDet: true, ctxErrorDet: Blank()});
    IfError(
        // El resultado NO se captura (ningún control/fórmula de esta guía lo lee — ver
        // fórmula (r) más arriba): se llama a la Custom API y se descarta el retorno.
        Environment.sanic_CHResolveChangeRequest({
            ChangeRequestId: gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest',
            Decision: If(ctxAccionDet = "aprobar", "Approve", "Reject"),
            Comment: Trim(cmpConfirmDialogDet.CampoTexto)
        });
        Set(
            gvarSolicitudSel,
            LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')
        );
        UpdateContext({ctxModoDlgDet: "resultado", ctxCargandoDet: false})
        ,
        UpdateContext({ctxErrorDet: FirstError.Message, ctxCargandoDet: false, ctxMostrarDlgDet: false})
    ),

    ctxAccionDet = "enviar",
    UpdateContext({ctxCargandoDet: true, ctxErrorDet: Blank()});
    IfError(
        With(
            {fresco: LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')},
            If(
                fresco.'Change Status' <> 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,
                UpdateContext({ctxErrorDet: "La solicitud ya no está en Borrador — alguien la modificó. Actualizá el detalle."}),
                ForAll(
                    colLineasDet As Linea,
                    Patch('CH-Tbl-ClientDataChangeRequestLines', Linea, {ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Submitted})
                );
                Patch('CH-Tbl-ClientDataChangeRequests', fresco, {'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted});
                Set(gvarSolicitudSel, LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = fresco.'CH-Tbl-ClientDataChangeRequest'));
                Notify("Solicitud enviada a aprobación.", NotificationType.Success);
                UpdateContext({ctxMostrarDlgDet: false, ctxAccionDet: ""})
            )
        ),
        UpdateContext({ctxErrorDet: FirstError.Message})
    );
    UpdateContext({ctxCargandoDet: false}),

    ctxAccionDet = "cancelar",
    UpdateContext({ctxCargandoDet: true, ctxErrorDet: Blank()});
    IfError(
        With(
            {fresco: LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')},
            If(
                fresco.'Change Status' <> 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,
                UpdateContext({ctxErrorDet: "La solicitud ya no está en Borrador — alguien la modificó. Actualizá el detalle."}),
                ForAll(
                    colLineasDet As Linea,
                    Patch(
                        'CH-Tbl-ClientDataChangeRequestLines', Linea,
                        {ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Canceled, SolvedBy: gvarUser, SolvedOn: Now()}
                    )
                );
                Patch(
                    'CH-Tbl-ClientDataChangeRequests', fresco,
                    {
                        'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Canceled,
                        SolvedBy: gvarUser,
                        SolvedOn: Now(),
                        ReasonForDecision: Trim(cmpConfirmDialogDet.CampoTexto)
                    }
                );
                Set(gvarSolicitudSel, LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = fresco.'CH-Tbl-ClientDataChangeRequest'));
                Notify("Solicitud cancelada.", NotificationType.Success);
                UpdateContext({ctxMostrarDlgDet: false, ctxAccionDet: ""})
            )
        ),
        UpdateContext({ctxErrorDet: FirstError.Message})
    );
    UpdateContext({ctxCargandoDet: false}),

    ctxAccionDet = "quitarlinea",
    UpdateContext({ctxCargandoDet: true, ctxErrorDet: Blank()});
    IfError(
        Remove('CH-Tbl-ClientDataChangeRequestLines', galLineasDet.Selected);
        ClearCollect(
            colLineasDet,
            Filter('CH-Tbl-ClientDataChangeRequestLines', ChangeRequestID.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')
        );
        Notify("Cambio eliminado de la solicitud.", NotificationType.Success);
        UpdateContext({ctxMostrarDlgDet: false, ctxAccionDet: ""})
        ,
        UpdateContext({ctxErrorDet: FirstError.Message})
    );
    UpdateContext({ctxCargandoDet: false})
)
```

> 🧨 Spinner SIEMPRE liberado (corrige L7 de la v1): en los 4 caminos de arriba,
> `UpdateContext({ctxCargandoDet: false})` está en TODAS las ramas (éxito y error) — nunca
> dentro de un `IfError` que pueda no ejecutarse.

---

## 10. Panel "Agregar cambio" (SO5, receta 7.7)

Chrome fijo de 7.7, hijo de la pantalla, ancla a la derecha:

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpVeloPanelDet` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · Visible `=ctxPanelAgregarDet` |
| `btnVeloClicPanelDet` | Botón (invisible) | `0` | `0` | `cntPanelDet.X` | `Parent.Height` | Fill/Hover `Transparent` · Text `""` · OnSelect `=UpdateContext({ctxPanelAgregarDet: false})` |
| `cntPanelDet` | Contenedor | `Parent.Width - fxPanelW` | `0` | `fxPanelW` | `Parent.Height` | Fill `ThemeSurface` · BorderColor `ThemeLine` (izq., `1`) · `DropShadow: DropShadow.Regular` · Visible `=ctxPanelAgregarDet` |

### 10.1 Head

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntPanelHeadDet` | Contenedor | `0` | `0` | `Parent.Width` | `64` | Fill `Transparent` |
| `lblTituloPanelDet` | Etiqueta clásica | `20` | `16` | `Parent.Width - 60` | `20` | Text `="Agregar cambio"` · ESTILO-TITULO (tamaño `12.5`) |
| `lblSubPanelDet` | Etiqueta clásica | `20` | `36` | `Parent.Width - 60` | `16` | Text `="Elegí un campo permitido y escribí el valor propuesto"` · ESTILO-SUBTITULO |
| `icoCerrarPanelDet` | Icono clásico (`Icon.Cancel`) | `Parent.Width - 34` | `18` | `18` | `18` | Color `ThemeInk3` · HoverColor `ThemeInk` · OnSelect `=UpdateContext({ctxPanelAgregarDet: false})` |

### 10.2 Body

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntPanelBodyDet` | Contenedor | `0` | `64` | `Parent.Width` | `Parent.Height - 128` | Fill `Transparent` |
| `lblCampoLabelDet` | Etiqueta clásica | `20` | `0` | `Parent.Width - 40` | `16` | Text `="Campo"` · ESTILO-CAMPO-LABEL |
| `drpCampoDet` | Menú desplegable clásico | `20` | `lblCampoLabelDet.Y + lblCampoLabelDet.Height + 4` | `Parent.Width - 40` | `32` | fórmulas abajo |
| `lblValorActualLabelDet` | Etiqueta clásica | `20` | `drpCampoDet.Y + drpCampoDet.Height + 13` | `Parent.Width - 40` | `16` | Text `="Valor actual"` · ESTILO-CAMPO-LABEL |
| `lblValorActualDet` | Etiqueta clásica | `20` | `lblValorActualLabelDet.Y + lblValorActualLabelDet.Height + 4` | `Parent.Width - 40` | `20` | Text fórmula (w) · ESTILO-CELDA · Color `ThemeInk2` |
| `lblValorNuevoLabelDet` | Etiqueta clásica | `20` | `lblValorActualDet.Y + lblValorActualDet.Height + 13` | `Parent.Width - 40` | `16` | Text `="Valor propuesto"` · ESTILO-CAMPO-LABEL |
| `inpValorNuevoDet` | Entrada de texto (SingleLine) | `20` | `lblValorNuevoLabelDet.Y + lblValorNuevoLabelDet.Height + 4` | `Parent.Width - 40` | `36` | `MaxLength: 100` (gotcha 8.10) · Fill `ThemeSurface2` · BorderColor `=If(!IsBlank(ctxErrorPanelDet), ThemeDanger, ThemeLine)` · Radius `7` |
| `lblValorNuevoErrorDet` | Etiqueta clásica | `20` | `inpValorNuevoDet.Y + inpValorNuevoDet.Height + 4` | `Parent.Width - 40` | `16` | ESTILO-CAMPO-ERROR · Text `=ctxErrorPanelDet` · Visible `=!IsBlank(ctxErrorPanelDet)` · `Wrap: true` |

**`drpCampoDet`**:

| Prop | Fórmula |
|---|---|
| Items | ver fórmula abajo |
| Value | `Name` (panel de propiedades: campo a mostrar) |
| OnChange | `=UpdateContext({ctxErrorPanelDet: Blank()})` |
| Fill / BorderColor | `ThemeSurface` / `ThemeLine` |

`drpCampoDet.Items` — filtra sobre `colCatalogoDet` (materializada EN MEMORIA en `OnVisible`
§1 paso 1b), no sobre el datasource: `Filter('CH-Tbl-ClientTableLogicalFieldNames', ... &&
!(LogicalName in colLineasDet...))` reventaba en Studio con **"Not operator along with the
Custom Named Condition operators is not allowed"** — `!(... in ...)` no delega contra
Dataverse. La corrección materializa el catálogo primero (§1 paso 1b) y filtra el `in` sobre
la colección local, donde sí es válido (fuente real, `scrSolicitud.fx.yaml`):

```powerfx
=With(
    {_yaUsados: ForAll(colLineasDet As L, {Value: Text(L.'Field Definition'.LogicalName)})},
    Filter(colCatalogoDet, !(LogicalName in _yaUsados))
)
```

**(w) `lblValorActualDet.Text`** — con `colTargetRecordDet` normalizado a pares
`{LogicalName, Valor}` (§1 paso 2), esta fórmula es un `LookUp` genérico por
`drpCampoDet.Selected.LogicalName` — YA NO hace falta el `Switch` de 6 ramas de una versión
anterior de esta guía (ese `Switch` asumía el esquema mixto viejo). Fuente real
(`scrSolicitud.fx.yaml`):

```powerfx
=If(
    IsBlank(drpCampoDet.Selected),
    "—",
    If(
        IsBlank(colTargetRecordDet) || CountRows(colTargetRecordDet) = 0,
        "(sin registro objetivo)",
        Coalesce(
            LookUp(colTargetRecordDet, LogicalName = drpCampoDet.Selected.LogicalName).Valor,
            "—"
        )
    )
)
```

### 10.3 Foot

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntPanelFootDet` | Contenedor | `0` | `Parent.Height - 64` | `Parent.Width` | `64` | Fill `Transparent` |
| `btnCancelarPanelDet` | Botón clásico | `btnConfirmarPanelDet.X - 8 - Self.Width` | `16` | `100` | `32` | ESTILO-BTN-FANTASMA · Text `"Cancelar"` · OnSelect `=UpdateContext({ctxPanelAgregarDet: false})` |
| `btnConfirmarPanelDet` | Botón clásico | `Parent.Width - 20 - Self.Width` | `16` | `110` | `32` | ESTILO-BTN-PRIMARIO · Text `"Agregar"` · `DisplayMode: =If(IsBlank(drpCampoDet.Selected), DisplayMode.Disabled, DisplayMode.Edit)` · OnSelect fórmula (x) |

**(x) `btnConfirmarPanelDet.OnSelect`**:

```powerfx
=UpdateContext({ctxErrorPanelDet: Blank()});
If(
    IsBlank(drpCampoDet.Selected),
    UpdateContext({ctxErrorPanelDet: "Elegí un campo."}),
    Len(Trim(inpValorNuevoDet.Text)) = 0,
    UpdateContext({ctxErrorPanelDet: "Escribí el valor propuesto."}),
    IfError(
        Patch(
            'CH-Tbl-ClientDataChangeRequestLines',
            Defaults('CH-Tbl-ClientDataChangeRequestLines'),
            {
                ChangeRequestID: gvarSolicitudSel,
                'Field Definition': drpCampoDet.Selected,
                OldValue: Left(lblValorActualDet.Text, 100),
                NewValue: Left(Trim(inpValorNuevoDet.Text), 100),
                ChangeStatus: 'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Draft
            }
        );
        ClearCollect(
            colLineasDet,
            Filter('CH-Tbl-ClientDataChangeRequestLines', ChangeRequestID.'CH-Tbl-ClientDataChangeRequest' = gvarSolicitudSel.'CH-Tbl-ClientDataChangeRequest')
        );
        Reset(drpCampoDet);
        Reset(inpValorNuevoDet);
        UpdateContext({ctxPanelAgregarDet: false})
        ,
        UpdateContext({ctxErrorPanelDet: FirstError.Message})
    )
)
```

> El guard server-side (`GuardChangeRequestLinePlugin`) re-valida Draft + creador/aprobador +
> sin duplicados por `FieldDefinition` — el `Items` de `drpCampoDet` ya excluye los campos
> repetidos client-side, pero el guard es la barrera real (defensa en profundidad).

---

> ## Notas — dependencias, divergencias declaradas y aproximaciones honestas
>
> **Dependencias de backend**
>
> | Dependencia | Estado | Fallback |
> |---|---|---|
> | `Environment.sanic_CHResolveChangeRequest` | **Desplegada y conectada (2026-08-25)**: registrada en la solución Core, el datasource `Environment` está agregado (`DataSources/Environment.json`) y `ctxResolveApiListoDet` quedó en `true` en el `OnVisible` real — `btnAprobarDet`/`btnRechazarDet` operan en `DisplayMode.Edit` sin banner de espera | Se conserva como referencia histórica breve el fallback usado mientras el backend no existía: si en algún entorno el conector `Environment` se agrega ANTES del deploy del backend, Studio no genera todavía la función tipada y la fórmula (v) de §9 NO compila — mientras tanto, pegar un stub en las 2 ramas `aprobar`/`rechazar` de `OnOk` (`UpdateContext({ctxErrorDet: "Backend pendiente: la Custom API todavía no está desplegada."})`) y dejar `ctxResolveApiListoDet: false` para que `btnAprobarDet`/`btnRechazarDet` queden en `DisplayMode.Disabled` con el banner de §5.1. Cuando el backend se despliegue: Datos → orígenes de datos → **Actualizar** sobre `Environment`, reemplazar el stub por el bloque real de (v), y cambiar el literal a `true` |
>
> **Divergencias declaradas vs mockup**
>
> | # | Divergencia | Por qué |
> |---|---|---|
> | W1 | El `Message` de salida de `sanic_CHResolveChangeRequest` (`"RFC {guid} resuelto: Applied por {resolverId}."`, verificado en `ResolveChangeRequestPlugin.cs:228-229`) es TÉCNICO, no la frase amigable del mockup SO3 ("El correo autorizado de Distribuidora La Colonia se actualizó a…") | El plugin no compone un mensaje de negocio — la pantalla arma su propio texto amigable con datos que YA tiene en memoria (`gvarSolicitudSel`, líneas), en vez de mostrar el `Message` de la respuesta como cuerpo principal (fórmula (r) de §9). Por eso `OnOk` (v) ni siquiera captura el resultado de la llamada — no hay ningún consumidor de ese payload en esta guía |
> | W2 | `Comment` del diálogo Aprobar/Rechazar se guarda en `sanic_applynotes` (`ApplyNotes`), NO en `sanic_reasonfordecision` como hacía la v1 | Verificado en `ResolveChangeRequestPlugin.cs:72-74,200-203`: el input `Comment` de la Custom API mapea a `ApplyNotes`. `ReasonForDecision` queda reservado para el comentario de **Cancelar** (Patch directo del creador, fuera de la Custom API) — separación limpia entre "motivo de una decisión resuelta por el backend" y "motivo de una acción del creador" |
> | W3 | El diálogo de resultado (SO3) NO muestra un campo de texto deshabilitado repitiendo el comentario — usa `MostrarCampo: false` y embebe el mensaje en `Texto` | `cmpConfirmDialog` (01 §7.6) solo tiene `Cargando` para deshabilitar el campo, que vuelve a `Edit` apenas termina la llamada — no hay un estado "resultado congelado" nativo del componente. Modificar el componente compartido está fuera de alcance de esta guía; la adaptación reutiliza el contrato existente sin tocar `CH-CL-SharedComponents` |
> | W4 | El historial (§8.1) NO tiene una entrada "Enviada a aprobación" con timestamp propio, a diferencia del mockup SO2 | Heredado de la divergencia V1 de la guía 04: la cabecera del RFC no tiene columna de fecha de envío (atributos verificados en `Entity.xml`: solo `createdon`/`solvedon`). Sintetizar esa entrada con una fecha inventada sería peor que omitirla — el timeline muestra "Creada (Borrador)" + eventos reales de auditoría + "Cancelada" sintética (única transición sin backend que la registra) |
> | W5 | El aviso de conflicto por registro (§7.1) es informativo, NO bloquea "Enviar" | `GuardChangeRequestLinePlugin` no valida conflictos entre RFCs distintos — solo duplicados DENTRO del mismo RFC. Bloquear client-side una regla que el servidor no aplica sería seguridad de fachada; el texto del propio mockup ("revisala antes de enviar") ya es advisory, no imperativo |
> | W6 | Cabecera kv unificada (§4.2) muestra SIEMPRE "Creada por" + fecha, aunque el mockup SO5 no las dibuja | Un solo bloque de controles cubre SO2 y SO5 sin duplicar 6 controles condicionales; mostrar de más nunca perjudica al creador (ve quién creó y cuándo, dato que ya es suyo) |
> | W7 | "Registro"/"Tabla" (§4.2, fórmula (c)) es una sola fila con label dinámico, en vez de dos filas separadas del mockup | Cuando `TargetTable = "sanic_chtblclient"` mostrar "Registro" repetiría el mismo valor que "Cliente" (redundante); se muestra "Tabla" en su lugar. Ídem justificación de compactar controles que W6 |
> | W8 | El botón "‹ Volver" navega SIEMPRE a `scrSolicitudes`, nunca `Back()` | `scrSolicitud` también se alcanza por deep-link (`App.OnStart` navega `scrSplash → scrSolicitud` sin pasar por `scrSolicitudes`); `Back()` en ese caso volvería a `scrSplash` (pantalla sin UI útil). Navegar explícito a `scrSolicitudes` es determinístico en los 3 orígenes de la tabla de CONTRATO DE ENTRADA |
> | W9 (RESUELTA — post-build 2026-08-25) | Una versión anterior de esta guía resolvía "valor actual" con un `Switch` bounded de 6 `LogicalName` de ejemplo en (j) §6.2 y (w) §10.2 (`sanic_cifbco`, `sanic_cifcom`, `sanic_name`, `emailaddress`, `sanic_alwaysnotify`, `sanic_currency`), con el resto de columnas "sin mapear todavía" | El fuente real normaliza `colTargetRecordDet` a pares `{LogicalName, Valor}` (§1 paso 2: un `ClearCollect` explícito por tabla dentro del `Switch(gvarSolicitudSel.TargetTable, ...)`, no por columna) — (j) y (w) ya NO necesitan ningún `Switch` propio: son un `LookUp(colTargetRecordDet, LogicalName = ...).Valor` genérico. El `Switch` por COLUMNA sigue sin existir en Power Fx (gotcha 8.9 sigue vigente), pero el problema se resolvió moviendo el mapeo columna→valor al ÚNICO lugar que ya necesitaba conocer las columnas reales de cada tabla (§1 paso 2, 4 ramas completas, una por tabla objetivo — no quedan columnas "sin mapear") |
> | W10 (consistencia cruzada con 04 S11) | `pilEstadoBarraDet` (§4.1) y `pilEstadoLinDet` (§6.2) usan `BorderThickness: 0` en TODOS los tonos, incluido el neutro (Borrador/Cancelada), en vez del default `If(<tono>="neutro", 1, 0)` de la receta 7.3 de `01-app.md` | Extiende a esta guía la divergencia S11 de `04-pantalla-solicitudes.md` (mismo pill de ESTADO del RFC/línea): el CSS del mockup (`.pill` y sus tonos, incluido `.pMuted`) no define `border` en NINGUNA variante — verificado en el `<style>` completo, mismo razonamiento que `pilEstadoFilaCli` en `02-pantalla-clientes.md` §5.4. Sin esta corrección el mismo RFC en Borrador se vería CON borde acá (ficha/detalle) y SIN borde en la bandeja de `04` |
> | W11 (consistencia cruzada con 04) | `lblVClienteDet.Text` (§4.2) usa `Coalesce(gvarClienteSel.Name, "—")`, no `gvarClienteSel.Name` a secas | El CONTRATO DE SALIDA de `04-pantalla-solicitudes.md` documenta que `gvarClienteSel` puede ser `Blank()` para un RFC de Alta (`Type = Create`, sin cliente relacionado todavía) — sin el `Coalesce` el valor quedaría en blanco en vez de mostrar `"—"` como el resto de la app (mismo patrón que `lblFilaClienteSol` de `04`, §4.2) |
>
> **Aproximaciones honestas adicionales**: (1) (RESUELTA — post-build 2026-08-25)
> `btnActualizarErrorDet` (§5.2, fórmula (h)) ya NO usa `Select(scrSolicitud)` — esa función no
> re-dispara `OnVisible` en canvas apps clásico y el fuente real nunca la usó; la corrección
> repite INLINE los `ClearCollect` de `colLineasDet`/`colAuditoriaDet`/`colTimelineDet` después
> del `Set()`. (2) El ancho de `pilEstadoBarraDet`/`pilEstadoLinDet` usa `Len(Texto) * 5.4` —
> aproximación de kerning de Open Sans, no medición exacta (misma salvedad que 01 §7.2/§7.3).
> (3) La conflict check de §1 paso 5 (`colConflictosDet`) NO es delegable (`ForAll` por línea +
> `Filter` por línea) — aceptable porque solo corre para el creador en Draft y el volumen de
> `CH-Tbl-ClientDataChangeRequestLines` es chico hoy; si crece, materializar un índice de
> conflictos server-side (Custom API o vista) en vez de este `Filter` por línea.
>
> **A verificar en Studio** (además de lo heredado de 01/04): (1) (RESUELTA — post-build
> 2026-08-25) el `in` de `drpCampoDet.Items` y del conflict check ya NO corre contra el
> datasource: `drpCampoDet.Items` filtra `colCatalogoDet` (colección local materializada en
> `OnVisible` §1 paso 1b) y el conflict check usa `ForAll`+`Filter` delegable por GUID de
> `Field Definition`, sin `in` server-side en ningún punto — confirmado contra el fuente real,
> ningún workaround pendiente. (2) el catálogo REAL de filas de
> `CH-Tbl-ClientTableLogicalFieldNames` por tabla (`sanic_LogicalName`, `sanic_IsAllowed`) —
> ya no bloquea construcción (W9 resuelta), pero conviene revisarlo igual contra Dataverse.
> (3) nombre real del enum de Power Fx para `'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'`
> si IntelliSense lo autogenera distinto — el suyo manda.

---

## Correcciones post-build 2026-08-25

Durante el debugging en Studio de `scrSolicitud` se corrigieron varias fórmulas que esta guía
todavía mostraba en su versión vieja (rota). El fuente REAL (`scrSolicitud.fx.yaml`) mandó en
cada caso:

| # | Qué estaba mal | Por qué (fuente real) |
|---|---|---|
| PB1 | `OnVisible` paso 2 armaba `colTargetRecordDet` con `ClearCollect(colTargetRecordDet, Table(Switch(TargetTable, ..., ShowColumns(...))))` — un solo `Table()` con columnas DISTINTAS por rama del `Switch` | El esquema mixto colapsaba el tipo de la colección resultante (bug real en Studio). El fuente normaliza a pares homogéneos `{LogicalName, Valor}`: `Switch(TargetTable, "sanic_chtblclient", With({_r: LookUp(...)}, ClearCollect(colTargetRecordDet, {LogicalName:"sanic_name", Valor:_r.Name}, ...)), ...)`, repitiendo el `ClearCollect` completo en cada rama en vez de un `Table()` único |
| PB2 | `lblActualLinDet.Text` (j, §6.2) y `lblValorActualDet.Text` (w, §10.2) resolvían "valor actual" con un `Switch` bounded de 6 `LogicalName` de ejemplo, dejando el resto "sin mapear todavía" (W9) | Con `colTargetRecordDet` normalizado (PB1), ambas fórmulas se reducen a `LookUp(colTargetRecordDet, LogicalName = <clave>).Valor` — sin `Switch` propio. W9 queda resuelta, no solo mitigada |
| PB3 | `lblVRegistroDet.Text` (d, §4.2) traía el valor con un `Switch` de 3 tablas + `First(colTargetRecordDet).Name`/`.'Numero de cuenta'` | Con `colTargetRecordDet` normalizado, se reduce a un `LookUp` por `LogicalName` (con el único caso especial de `"emailaddress"` vs `"sanic_name"` según la tabla, ver fórmula corregida) |
| PB4 | `drpCampoDet.Items` (§10.2) filtraba directo sobre el datasource: `Filter('CH-Tbl-ClientTableLogicalFieldNames', ... && !(LogicalName in colLineasDet.'Field Definition'.LogicalName))` | Revienta en Studio con **"Not operator along with the Custom Named Condition operators is not allowed"** — `!(... in ...)` no delega. El fuente materializa `colCatalogoDet` en `OnVisible` (paso 1b, agregado a esta guía) y filtra el `in` en memoria sobre esa colección local, donde sí es válido |
| PB5 | `OnVisible` paso 5 (conflictos) filtraba con `'Field Definition'.LogicalName in colLineasDet.'Field Definition'.LogicalName` dentro de un `Filter` server-side | Mismo error de delegación que PB4, aplicado al conflict check. El fuente reescribe el bloque como `ForAll(colLineasDet As L, Collect(colConflictosDet, Filter('CH-Tbl-ClientDataChangeRequestLines', ... && 'Field Definition'.'CH-Tbl-ClientTableLogicalFieldName' = L.'Field Definition'.'CH-Tbl-ClientTableLogicalFieldName')))` — un `Filter` por línea, delegable por GUID de Field Definition, sin `in` server-side ni dot-walk sobre una colección. De paso se corrigió el estado a comparar: `Draft`/`Submitted` (no `Submitted`/`Approved`) |
| PB6 | `btnActualizarErrorDet.OnSelect` (h, §5.2) terminaba con `Select(scrSolicitud)` para re-disparar `OnVisible` | `Select()` sobre la propia pantalla no re-dispara `OnVisible` en canvas apps clásico — el fuente recarga INLINE `colLineasDet` + `colAuditoriaDet` + `colTimelineDet` después del `Set(gvarSolicitudSel, ...)`, sin depender de ningún comportamiento no documentado |
| PB7 | `ctxResolveApiListoDet: false` en `OnVisible`, banner "backend pendiente" y botones Aprobar/Rechazar deshabilitados por defecto; Notas describían `Environment.sanic_CHResolveChangeRequest` como "no desplegada" | El datasource `Environment` YA está agregado y `ctxResolveApiListoDet` quedó en `true` en el fuente real — backend desplegado y conectado desde 2026-08-25. Se actualizaron `OnVisible`, §5.1, §9 (v), la tabla de "Dependencias de backend" y el checklist; se conservó el patrón de fallback como referencia histórica breve, no como estado actual |

Ver también las correcciones equivalentes en `01-app.md`, `02-pantalla-clientes.md`,
`03-pantalla-cliente.md` y `04-pantalla-solicitudes.md`.

---

## Checklist de salida

- [ ] Los 93 controles del árbol existen con esos nombres EXACTOS (sufijo `Det`).
- [ ] `OnVisible` pegado completo: reset de estado (incluido `ctxResolveApiListoDet: true` —
      backend desplegado y conectado desde 2026-08-25), líneas, catálogo de campos
      (`colCatalogoDet`), valor actual del registro objetivo normalizado a
      `{LogicalName, Valor}`, auditoría, timeline unificado, conflictos (solo Draft+creador,
      `ForAll` por línea).
- [ ] **SO2** (Enviada + Approver + creador≠usuario): `btnAprobarDet`/`btnRechazarDet`
      visibles y en `DisplayMode.Edit` (backend conectado); si en algún entorno
      `ctxResolveApiListoDet = false` (fallback, ver Notas), quedan en `Disabled` con el
      banner de §5.1.
- [ ] **SO3**: tras Aprobar/Rechazar exitoso, el diálogo pasa a `ctxModoDlgDet = "resultado"`
      con título/texto de éxito y un solo botón "Entendido"; `gvarSolicitudSel` se refresca del
      datasource antes de mostrar el resultado.
- [ ] **SO4**: un `IfError` en cualquier acción llena `ctxErrorDet` con `FirstError.Message`
      REAL (nunca texto fijo), esconde la grilla de líneas y muestra el banner + "↻ Actualizar
      detalle"; el spinner (`ctxCargandoDet`) queda en `false` en TODAS las ramas.
- [ ] **SO5** (Draft + creador): `btnEnviarDet`/`btnCancelarSolicitudDet` y
      `cntToolbarLineasDet` (Agregar/Quitar cambio) visibles; "Quitar cambio…" deshabilitado
      sin fila seleccionada; el panel "Agregar cambio" valida campo + valor antes de `Patch`.
- [ ] El banner de conflicto (§7.1) aparece cuando otro RFC en curso toca el mismo
      `TargetRecordID` + `FieldDefinition`, y es SOLO informativo (no bloquea Enviar).
- [ ] "Enviar"/"Cancelar" revalidan el estado (`LookUp` fresco = Draft) antes de escribir —
      si cambió, banner de error en vez de un Patch a ciegas.
- [ ] La grilla de líneas usa `fxRowHChica`, columnas alineadas al encabezado (gotcha 8.14),
      separador en `TemplateSize - 1` (7.10), y `pilEstadoLinDet` en `DisplayMode.Edit`
      (gotcha 8.4/8.5).
- [ ] "Valor actual" de cada línea (grilla y panel "Agregar cambio") sale de un `LookUp` en
      vivo sobre `colTargetRecordDet` (normalizada a pares `{LogicalName, Valor}` en
      `OnVisible`, no del `OldValue` guardado — salvo cuando no hay registro objetivo
      (`Type = Create`).
- [ ] `drpCampoDet.Items` filtra `colCatalogoDet` (colección local) en vez del datasource —
      confirmar que el catálogo de `CH-Tbl-ClientTableLogicalFieldNames` (`sanic_LogicalName`,
      `sanic_IsAllowed`) trae las filas reales esperadas por tabla objetivo.
- [ ] Fechas SIEMPRE con `FmtFecha`/`HH` (gotcha 8.1). "‹ Volver" navega a `scrSolicitudes`
      (nunca `Back()`, W8).
- [ ] App Checker en cero errores; delegación revisada en el `Filter` de conflictos (§1 paso
      5 — no delegable, aceptado y documentado, no un descuido).
