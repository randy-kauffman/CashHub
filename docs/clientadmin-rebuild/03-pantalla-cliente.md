# Guía 03 — `scrCliente` (ficha del cliente, sufijo `Fic`)

Ficha de un cliente: datos maestros de solo lectura en el sidebar + 5 tabs (Sucursales /
Correos / Cuentas / Solicitudes / Bitácora) con sus grillas delegables. Desde acá se disparan
todas las solicitudes de cambio (RFC) que tocan a este cliente y a sus sucursales/correos/
cuentas: editar (CI2), desactivar/activar (CI3), y alta de sucursal/correo/cuenta. Corrige
el bug L2 de la v1 (Editar creaba RFC de tipo Update **sin líneas**): acá "Editar" siempre
captura el valor nuevo y arma una línea por campo modificado.

Esta guía asume construida `01-app.md` completa (App.Formulas, App.OnStart, `cmpAppHeader`,
`cmpConfirmDialog`, ESTILO-\*, recetas 7.x, gotchas §8). Fuente del diseño: mockup vistas
**CI1** (ficha + tabs), **CI2** (panel editar correo) y **CI3** (diálogo desactivar sucursal).

## CONTRATO DE ENTRADA

| Variable | Contenido esperado | Quién la setea |
|---|---|---|
| `colMisPermisos` | Filas de `CH-Tbl-ClientDataChangePermissions` del usuario | `App.OnStart` |
| `gvarRolTexto` | Texto del chip de rol del header | `App.OnStart` |
| `gvarClienteSel` | Registro de `CH-Tbl-Clients` a mostrar — **obligatorio, no blanco** | `App.OnStart` (deep-link vía `gvarSolicitudSel.RelatedClient`) o `scrClientes` (clic en fila) |

Si `gvarClienteSel` llega blanco (navegación directa sin pasar por `scrClientes`, error de
deep-link), `OnVisible` redirige a `scrClientes` en vez de mostrar una ficha vacía.

## CONTRATO DE SALIDA

| Variable | Cuándo se setea | Quién la lee |
|---|---|---|
| `gvarSolicitudSel` | Al tocar una fila de la tab **Solicitudes** | `scrSolicitud` (guía 05) |
| `gvarClienteSel` | `= ThisItem.RelatedClient`, en el mismo `OnSelect` que `gvarSolicitudSel` (`btnFilaSolicitudFic`, §8) — mismo patrón que `galSolicitudesSol.OnSelect` de la guía 04 | `scrSolicitud` (guía 05, CONTRATO DE ENTRADA) |

`gvarClienteSel` **no se limpia** al salir con `‹ Volver` (`Navigate(scrClientes)`) —
`scrClientes` no depende de su valor previo.

### Variables de contexto locales (solo esta pantalla, vía `UpdateContext`)

| Variable | Tipo | Uso |
|---|---|---|
| `ctxTabFic` | Texto | Tab activa: `"Sucursales"` / `"Correos"` / `"Cuentas"` / `"Solicitudes"` / `"Bitácora"` |
| `varPanelAbiertoFic` | Booleano | Visibilidad del panel lateral de alta/edición |
| `varPanelModoFic` | Texto | `"cliente"` / `"sucursal"` / `"correo"` / `"cuenta"` |
| `varPanelAccionFic` | Texto | `"Editar"` / `"Alta"` |
| `varPanelTargetIdFic` | GUID | PK del registro que se está editando (blanco en Alta) |
| `varPanelCargandoFic` | Booleano | Estado "aplicando…" de las 3 escrituras encadenadas de `btnPanelConfirmarFic.OnSelect` (§10.4) — anti doble-click, mismo patrón que `varDlgCargandoFic` (§11) |
| `varDlgAccionFic` | Texto | `"Desactivar"` / `"Activar"` (dialógo de confirmación) |
| `varDlgTargetTableFic` | Texto | Literal de tabla objetivo para el diálogo (ver §13.1) |
| `varDlgTargetTableLabelFic` | Texto | `"Clientes"` / `"Sucursales"` / `"Correos"` / `"Cuentas"` |
| `varDlgTargetTablePluralFic` | Texto | `sanic_chtblclients` / `...branchs` / `...emails` / `...accounts` |
| `varDlgTargetIdFic` | GUID | PK del registro objetivo del diálogo |
| `varDlgTargetNombreFic` | Texto | Nombre a mostrar en el título del diálogo |
| `varDlgCargandoFic` | Booleano | Estado "aplicando…" del diálogo de confirmación (§11) |
| `varRfcErrorFic` | Texto | Último `FirstError.Message` de la creación de RFC vía panel (§10.4), por si se quiere mostrar en pantalla además del `Notify` |

### Colecciones locales

| Colección | Contenido | Se recarga |
|---|---|---|
| `colRfcEnCursoFic` | RFCs `Draft`/`Submitted`/`Approved` cuyo `RelatedClient` es este cliente (cubre Cliente + sus Sucursales/Correos/Cuentas, denormalizado en cabecera) | `OnVisible` + al final de cada creación de RFC |
| `colCuentasRevFic` | PKs de `CH-Tbl-ClientAccount` con el número revelado (sin máscara) en la sesión actual | `OnVisible` (se vacía) |
| `colCambiosPanelFic` | Diff campo→(valor actual, valor propuesto) del panel lateral, solo campos que cambiaron | Cada `OnChange` de un campo del panel |
| `colNuevoRfcFic` | Colección técnica de 1 fila: la cabecera de RFC recién creada en Draft (para leer su PK en los pasos 2/3 de §10.4) | `Clear()` y `Collect()`, AMBOS en el paso 1 de `btnPanelConfirmarFic.OnSelect` (§10.4) — **no** al abrir el panel; los `OnSelect` que abren el panel (`btnEditarClienteFic` §2.2, `btnNuevoTabFic` §4, `btnFilaXFic.OnSelect` de Editar en §5–§7, R2-3) solo limpian `colCambiosPanelFic` |
| `colBranchesClienteFic` / `colEmailsClienteFic` / `colAccountsClienteFic` / `colSolicitudesClienteFic` / `colAuditClienteFic` | Todas las filas de Sucursales/Correos/Cuentas/Solicitudes/Bitácora de este cliente (sin filtro de estado) — materializadas para contar sin romper delegación (R2-6, gotcha 8.12 de `01-app.md`: `CountRows(Filter(...))` directo sobre el datasource no es delegable) | `OnVisible` |

---

## Árbol de controles

```
scrCliente
├── cmpAppHeaderFic                     Componente (cmpAppHeader, receta 7.2)
├── cntBodyFic                          Contenedor (manual)
│   ├── cntSideFic                      Contenedor (manual)
│   │   ├── cntTarjetaClienteFic        Contenedor (manual)
│   │   │   ├── cntTarjetaClienteHeadFic    Contenedor (manual)
│   │   │   │   ├── btnVolverFic            Botón clásico
│   │   │   │   └── pilEstadoClienteFic     Botón clásico (pill)
│   │   │   ├── lblNombreClienteFic         Etiqueta clásica
│   │   │   ├── lblClienteDesdeFic          Etiqueta clásica
│   │   │   ├── lblKvCifBcoLabFic           Etiqueta clásica
│   │   │   ├── lblKvCifBcoValFic           Etiqueta clásica
│   │   │   ├── lblKvCifComLabFic           Etiqueta clásica
│   │   │   ├── lblKvCifComValFic           Etiqueta clásica
│   │   │   ├── lblKvSucursalesLabFic       Etiqueta clásica
│   │   │   ├── lblKvSucursalesValFic       Etiqueta clásica
│   │   │   ├── lblKvCorreosLabFic          Etiqueta clásica
│   │   │   ├── lblKvCorreosValFic          Etiqueta clásica
│   │   │   ├── lblKvCuentasLabFic          Etiqueta clásica
│   │   │   └── lblKvCuentasValFic          Etiqueta clásica
│   │   └── cntAccionesClienteFic       Contenedor (manual)
│   │       ├── lblAccionesHeadFic          Etiqueta clásica
│   │       ├── btnEditarClienteFic         Botón clásico
│   │       ├── btnDesactivarClienteFic     Botón clásico
│   │       └── cntBannerRfcClienteFic      Contenedor (manual, receta 7.11)
│   │           └── lblBannerRfcClienteFic  Etiqueta clásica
│   └── cntMainFic                      Contenedor (manual)
│       ├── cntTabsFic                  Contenedor (manual)
│       │   ├── btnTabSucursalesFic         Botón clásico
│       │   ├── btnTabCorreosFic            Botón clásico
│       │   ├── btnTabCuentasFic            Botón clásico
│       │   ├── btnTabSolicitudesFic        Botón clásico
│       │   └── btnTabBitacoraFic           Botón clásico
│       ├── cntToolbarTabFic             Contenedor (manual)
│       │   ├── inpBuscarTabFic             Entrada de texto clásica
│       │   ├── lblGestionaTabFic           Etiqueta clásica
│       │   └── btnNuevoTabFic              Botón clásico
│       ├── cntGridSucursalesFic         Contenedor (manual, receta 7.4)
│       │   ├── lblHSucSucursalFic          Etiqueta clásica
│       │   ├── lblHSucEstadoFic            Etiqueta clásica
│       │   ├── lblHSucSolicitudFic         Etiqueta clásica
│       │   ├── shpHSucLineaFic             Rectángulo clásico
│       │   └── galSucursalesFic            Galería vertical en blanco
│       │       ├── btnFilaSucursalFic          Botón clásico
│       │       ├── shpSelSucursalFic           Rectángulo clásico
│       │       ├── shpSelBarraSucursalFic      Rectángulo clásico
│       │       ├── lblSucNombreFic             Etiqueta clásica
│       │       ├── pilSucEstadoFic             Botón clásico (pill)
│       │       ├── pilSucRfcFic                Botón clásico (pill)
│       │       └── shpSucSepFic                Rectángulo clásico
│       ├── cntGridCorreosFic             Contenedor (manual, receta 7.4)
│       │   ├── lblHCorCorreoFic
│       │   ├── lblHCorNotifFic
│       │   ├── lblHCorEstadoFic
│       │   ├── lblHCorSolicitudFic
│       │   ├── shpHCorLineaFic
│       │   └── galCorreosFic
│       │       ├── btnFilaCorreoFic
│       │       ├── shpSelCorreoFic
│       │       ├── shpSelBarraCorreoFic
│       │       ├── lblCorDireccionFic
│       │       ├── lblCorNotifFic
│       │       ├── pilCorEstadoFic
│       │       ├── pilCorRfcFic
│       │       └── shpCorSepFic
│       ├── cntGridCuentasFic             Contenedor (manual, receta 7.4)
│       │   ├── lblHCtaNumeroFic
│       │   ├── lblHCtaMonedaFic
│       │   ├── lblHCtaEstadoFic
│       │   ├── lblHCtaSolicitudFic
│       │   ├── shpHCtaLineaFic
│       │   └── galCuentasFic
│       │       ├── btnFilaCuentaFic
│       │       ├── shpSelCuentaFic
│       │       ├── shpSelBarraCuentaFic
│       │       ├── lblCtaNumeroFic
│       │       ├── btnCtaRevelarFic
│       │       ├── lblCtaMonedaFic
│       │       ├── pilCtaEstadoFic
│       │       ├── pilCtaRfcFic
│       │       └── shpCtaSepFic
│       ├── cntGridSolicitudesFic         Contenedor (manual, receta 7.4)
│       │   ├── lblHSolNumeroFic
│       │   ├── lblHSolTablaFic
│       │   ├── lblHSolTipoFic
│       │   ├── lblHSolEstadoFic
│       │   ├── lblHSolCreadaPorFic
│       │   ├── lblHSolFechaFic
│       │   ├── shpHSolLineaFic
│       │   └── galSolicitudesFic
│       │       ├── btnFilaSolicitudFic
│       │       ├── shpSelSolicitudFic
│       │       ├── shpSelBarraSolicitudFic
│       │       ├── lblSolNumeroFic
│       │       ├── lblSolTablaFic
│       │       ├── pilSolTipoFic
│       │       ├── pilSolEstadoFic
│       │       ├── lblSolCreadaPorFic
│       │       ├── lblSolFechaFic
│       │       └── shpSolSepFic
│       └── cntGridBitacoraFic            Contenedor (manual, receta 7.4)
│           ├── lblHBitFechaFic
│           ├── lblHBitOperacionFic
│           ├── lblHBitResultadoFic
│           ├── lblHBitMensajeFic
│           ├── shpHBitLineaFic
│           └── galBitacoraFic
│               ├── lblBitFechaFic
│               ├── lblBitOperacionFic
│               ├── pilBitResultadoFic
│               ├── lblBitMensajeFic
│               └── shpBitSepFic
├── cntPanelFic                          Contenedor (manual, chrome receta 7.7)
│   ├── shpVeloPanelFic                      Rectángulo clásico
│   ├── btnVeloClicPanelFic                  Botón clásico
│   └── cntPanelCardFic                      Contenedor (manual)
│       ├── cntPanelHeadFic                      Contenedor (manual)
│       │   ├── lblPanelTituloFic                    Etiqueta clásica
│       │   ├── lblPanelSubFic                       Etiqueta clásica
│       │   └── icoPanelCerrarFic                    Icono clásico
│       ├── cntPanelBodyFic                      Contenedor (manual)
│       │   ├── lblCampoALabelFic                    Etiqueta clásica
│       │   ├── lblCampoAActualFic                   Etiqueta clásica
│       │   ├── inpPanelCampoAFic                    Entrada de texto clásica
│       │   ├── lblCampoAErrorFic                    Etiqueta clásica
│       │   ├── lblCampoBLabelFic                    Etiqueta clásica
│       │   ├── lblCampoBActualFic                   Etiqueta clásica
│       │   ├── inpPanelCampoBFic                    Entrada de texto clásica
│       │   ├── drpPanelCampoBFic                    Menú desplegable clásico
│       │   ├── lblCampoBErrorFic                    Etiqueta clásica
│       │   ├── lblCampoCLabelFic                    Etiqueta clásica
│       │   ├── lblCampoCActualFic                   Etiqueta clásica
│       │   ├── inpPanelCampoCFic                    Entrada de texto clásica
│       │   ├── lblMotivoLabelPanelFic                Etiqueta clásica
│       │   ├── inpMotivoPanelFic                    Entrada de texto clásica (MultiLine)
│       │   ├── lblCambiosHeadFic                    Etiqueta clásica
│       │   ├── cntCambiosPanelFic                   Contenedor (manual)
│       │   │   ├── lblCambiosListHeadFic                Etiqueta clásica
│       │   │   └── galCambiosPanelFic                   Galería vertical en blanco
│       │   │       ├── lblCambioCampoFic
│       │   │       ├── lblCambioViejoFic
│       │   │       ├── lblCambioFlechaFic
│       │   │       └── lblCambioNuevoFic
│       │   ├── cntBannerPanelFic                    Contenedor (manual, receta 7.11)
│       │   │   └── lblBannerPanelFic                    Etiqueta clásica
│       │   └── cntBannerPanelConflictoFic           Contenedor (manual, receta 7.11)
│       │       └── lblBannerPanelConflictoFic           Etiqueta clásica
│       └── cntPanelFootFic                      Contenedor (manual)
│           ├── btnPanelCancelarFic                  Botón clásico
│           └── btnPanelConfirmarFic                 Botón clásico
└── cmpConfirmDialogFic                  Componente (cmpConfirmDialog, receta 7.6)
```

Total: **150 nodos** — 1 componente de header (`cmpAppHeaderFic`) + 1 componente de diálogo
(`cmpConfirmDialogFic`) + 148 controles clásicos/contenedores/galerías propios de esta
pantalla (145 originales + `cntBannerPanelConflictoFic`/`lblBannerPanelConflictoFic` de R1-3 +
`lblCambiosListHeadFic` de R1-6).

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | ver abajo — completo |

```powerfx
If(
    IsBlank(gvarClienteSel),
    Navigate(scrClientes, ScreenTransition.None),
    (
        UpdateContext(
            {
                ctxTabFic: "Sucursales",
                varPanelAbiertoFic: false,
                varPanelModoFic: "",
                varPanelAccionFic: "",
                varDlgAccionFic: ""
            }
        );
        Reset(inpBuscarTabFic);
        ClearCollect(
            colRfcEnCursoFic,
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
                && (
                    'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved
                )
            )
        );
        // R2-6: materializar los 5 conteos de sidebar/tabs — CountRows(Filter(...)) directo
        // sobre el datasource no es delegable (gotcha 8.12 de 01-app.md).
        ClearCollect(colBranchesClienteFic, Filter('CH-Tbl-ClientBranches', Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'));
        ClearCollect(colEmailsClienteFic, Filter('CH-Tbl-ClientEmails', Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'));
        ClearCollect(colAccountsClienteFic, Filter('CH-Tbl-ClientAccounts', Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'));
        ClearCollect(colSolicitudesClienteFic, Filter('CH-Tbl-ClientDataChangeRequests', RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'));
        ClearCollect(colAuditClienteFic, Filter('CH-Tbl-ClientDataChangesAuditTrails', RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'));
        Clear(colCuentasRevFic);
        Clear(colCambiosPanelFic);
        Reset(galSucursalesFic);
        Reset(galCorreosFic);
        Reset(galCuentasFic);
        Reset(galSolicitudesFic);
        Reset(galBitacoraFic)
    )
)
```

> `colRfcEnCursoFic` es la pieza central del gotcha "conflictos por registro, no globales"
> (nota del mockup): filtra por `RelatedClient`, que la cabecera del RFC lleva denormalizado
> **siempre** (aunque la tabla objetivo sea Sucursal/Correo/Cuenta — ver `Schema.cs`
> `ChangeRequest.RelatedClient`), así que UNA sola consulta cubre los 4 niveles. Contra esta
> colección se resuelve, en memoria: el banner del sidebar, la píldora "RFC en curso" de cada
> fila, y el bloqueo de los botones Editar/Desactivar/Activar — todo por `TargetTable` +
> `TargetRecordID` del registro puntual, nunca "cualquier RFC que toque esta tabla".

---

## 2. `cntBodyFic` y `cntSideFic` — esqueleto

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cmpAppHeaderFic` | Componente (`cmpAppHeader`) | `0` | `0` | `Parent.Width` | `fxHeaderH` | ver instanciación abajo |
| `cntBodyFic` | Contenedor | `0` | `cmpAppHeaderFic.Y + cmpAppHeaderFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` |
| `cntSideFic` | Contenedor | `fxGutter` | `fxGutter` | `fxSideW` | `Parent.Height - fxGutter*2` | Fill `Transparent` |
| `cntMainFic` | Contenedor | `cntSideFic.X + cntSideFic.Width + fxGap` | `fxGutter` | `Parent.Width - Self.X - fxGutter` | `Parent.Height - fxGutter*2` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `fxCardR` |

**Instanciación de `cmpAppHeaderFic`** (Custom Properties, patrón exacto de 01-app.md §7.2):

```powerfx
cmpAppHeaderFic As cmpAppHeader:
    Activo: ="Clientes"
    Height: =fxHeaderH
    OnClientes: =false
    OnSolicitudes: =Navigate(scrSolicitudes, ScreenTransition.None)
    RolTexto: =gvarRolTexto
    Subtitulo: ="Ficha de cliente"
    Titulo: ="Administración de Clientes"
    Width: =Parent.Width
```

### 2.1 `cntTarjetaClienteFic` — datos maestros del cliente

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntTarjetaClienteFic` | Contenedor | `0` | `0` | `Parent.Width` | `lblKvCuentasValFic.Y + lblKvCuentasValFic.Height + 14` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `fxCardR` |
| `cntTarjetaClienteHeadFic` | Contenedor | `0` | `0` | `Parent.Width` | `38` | Fill `ThemeSurface2` · BorderColor `ThemeLine` (inferior, `1`) |
| `btnVolverFic` | Botón | `12` | `(Parent.Height-26)/2` | `62` | `26` | ESTILO-BTN-CHICO · Text `"‹ Volver"` · `OnSelect: =Navigate(scrClientes, ScreenTransition.None)` |
| `pilEstadoClienteFic` | Botón (pill) | `Parent.Width - 12 - Self.Width` | `(Parent.Height-20)/2` | `Len(Switch(gvarClienteSel.RecordStatus, 'RecordStatus (CH-Tbl-Clients)'.Active,"Activo",'RecordStatus (CH-Tbl-Clients)'.Inactive,"Inactivo",'RecordStatus (CH-Tbl-Clients)'.PendingApproval,"Pend. aprobación",'RecordStatus (CH-Tbl-Clients)'.PendingDeactivation,"Pend. desactivación","—")) * 5.4 + 18` | `20` | receta 7.3 — ver fórmula completa abajo |
| `lblNombreClienteFic` | Etiqueta | `14` | `cntTarjetaClienteHeadFic.Y + cntTarjetaClienteHeadFic.Height + 12` | `Parent.Width - 28` | `20` | Text `gvarClienteSel.Name` · Size `10.88` · Bold · `Wrap: false` |
| `lblClienteDesdeFic` | Etiqueta | `14` | `lblNombreClienteFic.Y + lblNombreClienteFic.Height + 2` | `Parent.Width - 28` | `16` | ESTILO-META · Text `"Cliente desde " & Lower(Text(gvarClienteSel.'Created On', "mmm yyyy"))` |
| `lblKvCifBcoLabFic` | Etiqueta | `14` | `lblClienteDesdeFic.Y + lblClienteDesdeFic.Height + 10` | `90` | `16` | ESTILO-DATO-K · Text `"CIF BCO"` |
| `lblKvCifBcoValFic` | Etiqueta | `lblKvCifBcoLabFic.X + lblKvCifBcoLabFic.Width` | `lblKvCifBcoLabFic.Y` | `Parent.Width - 28 - Self.X` | `16` | ESTILO-DATO-V · Text `gvarClienteSel.CIFBCO` |
| `lblKvCifComLabFic` | Etiqueta | `14` | `lblKvCifBcoLabFic.Y + lblKvCifBcoLabFic.Height + 5` | `90` | `16` | ESTILO-DATO-K · Text `"CIF COM"` |
| `lblKvCifComValFic` | Etiqueta | `lblKvCifComLabFic.X + lblKvCifComLabFic.Width` | `lblKvCifComLabFic.Y` | `Parent.Width - 28 - Self.X` | `16` | ESTILO-DATO-V · Text `gvarClienteSel.CIFCOM` |
| `lblKvSucursalesLabFic` | Etiqueta | `14` | `lblKvCifComLabFic.Y + lblKvCifComLabFic.Height + 5` | `90` | `16` | ESTILO-DATO-K · Text `"Sucursales"` |
| `lblKvSucursalesValFic` | Etiqueta | `lblKvSucursalesLabFic.X + lblKvSucursalesLabFic.Width` | `lblKvSucursalesLabFic.Y` | `Parent.Width - 28 - Self.X` | `16` | ESTILO-DATO-V · Text `CountRows(Filter(colBranchesClienteFic, RecordStatus = 'RecordStatus (CH-Tbl-ClientBranches)'.Active)) & " activas"` (R2-6: cuenta sobre `colBranchesClienteFic`, materializada en `OnVisible`, no sobre el datasource) |
| `lblKvCorreosLabFic` | Etiqueta | `14` | `lblKvSucursalesLabFic.Y + lblKvSucursalesLabFic.Height + 5` | `90` | `16` | ESTILO-DATO-K · Text `"Correos"` |
| `lblKvCorreosValFic` | Etiqueta | `lblKvCorreosLabFic.X + lblKvCorreosLabFic.Width` | `lblKvCorreosLabFic.Y` | `Parent.Width - 28 - Self.X` | `16` | ESTILO-DATO-V · Text `CountRows(Filter(colEmailsClienteFic, RecordStatus = 'RecordStatus (CH-Tbl-ClientEmails)'.Active)) & " autorizados"` (R2-6: sobre `colEmailsClienteFic` materializada) |
| `lblKvCuentasLabFic` | Etiqueta | `14` | `lblKvCorreosLabFic.Y + lblKvCorreosLabFic.Height + 5` | `90` | `16` | ESTILO-DATO-K · Text `"Cuentas"` |
| `lblKvCuentasValFic` | Etiqueta | `lblKvCuentasLabFic.X + lblKvCuentasLabFic.Width` | `lblKvCuentasLabFic.Y` | `Parent.Width - 28 - Self.X` | `16` | ESTILO-DATO-V · Text `Text(CountRows(colAccountsClienteFic))` (R2-6: sobre `colAccountsClienteFic` materializada) |

`pilEstadoClienteFic` — propiedades completas (receta 7.3, `<tono>` = mapa de `RecordStatus`):

| Prop | Fórmula |
|---|---|
| Text | `Switch(gvarClienteSel.RecordStatus, 'RecordStatus (CH-Tbl-Clients)'.Active,"Activo", 'RecordStatus (CH-Tbl-Clients)'.Inactive,"Inactivo", 'RecordStatus (CH-Tbl-Clients)'.PendingApproval,"Pend. aprobación", 'RecordStatus (CH-Tbl-Clients)'.PendingDeactivation,"Pend. desactivación", "—")` |
| Fill | `Switch(gvarClienteSel.RecordStatus, 'RecordStatus (CH-Tbl-Clients)'.Active,ThemeOkSoft, 'RecordStatus (CH-Tbl-Clients)'.Inactive,ThemeLineSoft, 'RecordStatus (CH-Tbl-Clients)'.PendingApproval,ThemeWarnSoft, 'RecordStatus (CH-Tbl-Clients)'.PendingDeactivation,ThemeWarnSoft, ThemeLineSoft)` |
| Color | mismo `Switch`, sustituyendo `*Soft` por el tono sólido (`ThemeOk`/`ThemeInk3`/`ThemeWarn`/`ThemeWarn`) |
| Radius (las 4) / Height / DisplayMode / BorderThickness | `12` / `20` / `DisplayMode.View` / `0` |

### 2.2 `cntAccionesClienteFic` — acciones sobre el cliente

Named helper (se repite en varias fórmulas de esta sección, se escribe inline cada vez —
no es una named formula de `App.Formulas` porque depende de `gvarClienteSel`, que no existe
en ese scope): **hay RFC en curso sobre el Cliente** =
`!IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclient" && TargetRecordID = Text(gvarClienteSel.'CH-Tbl-Client')))`.

**Soy Contributor de Clientes** = `!IsBlank(LookUp(colMisPermisos, Name = "sanic_chtblclient" && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))`.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntAccionesClienteFic` | Contenedor | `0` | `cntTarjetaClienteFic.Y + cntTarjetaClienteFic.Height + fxGap` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `ThemeSurface` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `fxCardR` |
| `lblAccionesHeadFic` | Etiqueta | `0` | `0` | `Parent.Width` | `30` | ESTILO-HEADER-COL · Text `"ACCIONES SOBRE EL CLIENTE"` · Fill `ThemeSurface2` · Padding-X `14` (usar `X:14` del texto interno o `Align.Left` con `PaddingLeft` no existe en clásico: dejar `X:14`, `Width:Parent.Width-14`) |
| `btnEditarClienteFic` | Botón | `14` | `lblAccionesHeadFic.Y + lblAccionesHeadFic.Height + 9` | `Parent.Width - 28` | `26` | ESTILO-BTN-CHICO-ACC · Text `"✎ Solicitar edición de datos"` · ver `DisplayMode`/`OnSelect` abajo |
| `btnDesactivarClienteFic` | Botón | `14` | `btnEditarClienteFic.Y + btnEditarClienteFic.Height + 8` | `Parent.Width - 28` | `26` | ESTILO-BTN-CHICO-DAN · `Text: =If(gvarClienteSel.RecordStatus = 'RecordStatus (CH-Tbl-Clients)'.Inactive, "⏻ Solicitar activación", "⏻ Solicitar desactivación")` · ver `DisplayMode`/`OnSelect` abajo |
| `cntBannerRfcClienteFic` | Contenedor (receta 7.11, tono `warn`) | `14` | `btnDesactivarClienteFic.Y + btnDesactivarClienteFic.Height + 10` | `Parent.Width - 28` | `lblBannerRfcClienteFic.Height + 14` | Fill `ThemeWarnSoft` · Radius `9` · `Visible: =!IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclient" && TargetRecordID = Text(gvarClienteSel.'CH-Tbl-Client')))` |
| `lblBannerRfcClienteFic` | Etiqueta | `10` | `7` | `Parent.Width - 20` | *(AutoHeight)* | Size `7.5` · Color `ThemeWarn` · `Wrap: true` · `AutoHeight: true` · Text `"Hay 1 solicitud en curso sobre este cliente — no se puede pedir otra edición del mismo campo hasta que se resuelva."` |

`btnEditarClienteFic` — `DisplayMode` y `OnSelect` completos:

```powerfx
// DisplayMode
=If(
    !IsBlank(LookUp(colMisPermisos, Name = "sanic_chtblclient" && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))
    && gvarClienteSel.RecordStatus = 'RecordStatus (CH-Tbl-Clients)'.Active
    && IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclient" && TargetRecordID = Text(gvarClienteSel.'CH-Tbl-Client'))),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

```powerfx
// OnSelect
=UpdateContext(
    {
        varPanelModoFic: "cliente",
        varPanelAccionFic: "Editar",
        varPanelTargetIdFic: gvarClienteSel.'CH-Tbl-Client',
        varPanelAbiertoFic: true
    }
);
Reset(inpPanelCampoAFic);
Reset(inpPanelCampoBFic);
Reset(inpPanelCampoCFic);
Reset(inpMotivoPanelFic);
Clear(colCambiosPanelFic)
```

`btnDesactivarClienteFic` — mismo `DisplayMode` que `btnEditarClienteFic` (Contributor + sin
RFC en curso; **sin** la condición de `RecordStatus = Active`, porque este botón sirve tanto
para desactivar un cliente activo como para activar uno inactivo). `OnSelect`:

```powerfx
=UpdateContext(
    {
        varDlgAccionFic: If(gvarClienteSel.RecordStatus = 'RecordStatus (CH-Tbl-Clients)'.Inactive, "Activar", "Desactivar"),
        varDlgTargetTableFic: "sanic_chtblclient",
        varDlgTargetTableLabelFic: "Clientes",
        varDlgTargetTablePluralFic: "sanic_chtblclients",
        varDlgTargetIdFic: gvarClienteSel.'CH-Tbl-Client',
        varDlgTargetNombreFic: gvarClienteSel.Name
    }
)
```

La instanciación de `cmpConfirmDialogFic` (§12) lee estas 6 variables para armar
`Titulo`/`Texto`/`Tono` y para el `Patch` de `OnOk`.

---

## 3. `cntTabsFic` — pestañas de la ficha

Patrón de pestañas: `ctxTabFic` (contexto de pantalla, seteado en `OnVisible`) guarda el
texto de la tab activa; cada botón la fija con `UpdateContext` en su `OnSelect`; **todo**
control de contenido tabulado lleva la conjunción completa (gotcha 8.6) — acá,
`cntGridXFic.Visible = ctxTabFic = "<Tab>"` sin excepción.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntTabsFic` | Contenedor | `0` | `0` | `Parent.Width` | `40` | Fill `Transparent` · BorderColor `ThemeLine` (inferior, `1`) |

Los 5 botones (hijos de `cntTabsFic`, `Parent` = `cntTabsFic`; encadenados por `X`, todos
`Y:0`, `Height:Parent.Height`, `Size:9`, `BorderThickness:0`, `BorderRadius:0`,
`BorderBottomThickness:2.5` simulado con un `shp…SubFic` — ver nota):

| Control | Fórmula de `X` | `<Tab>` | `<Contador>` |
|---|---|---|---|
| `btnTabSucursalesFic` | `14` | `"Sucursales"` | `CountRows(colBranchesClienteFic)` (R2-6) |
| `btnTabCorreosFic` | `btnTabSucursalesFic.X + btnTabSucursalesFic.Width` | `"Correos"` | `CountRows(colEmailsClienteFic)` (R2-6) |
| `btnTabCuentasFic` | `btnTabCorreosFic.X + btnTabCorreosFic.Width` | `"Cuentas"` | `CountRows(colAccountsClienteFic)` (R2-6) |
| `btnTabSolicitudesFic` | `btnTabCuentasFic.X + btnTabCuentasFic.Width` | `"Solicitudes"` | `CountRows(colSolicitudesClienteFic)` (R2-6) |
| `btnTabBitacoraFic` | `btnTabSolicitudesFic.X + btnTabSolicitudesFic.Width` | `"Bitácora"` | `CountRows(colAuditClienteFic)` (R2-6) |

Cada botón, `Width: =Len("<Tab>") * 6.4 + 56`, y:

| Prop | Fórmula |
|---|---|
| Text | `"<Tab> (" & <Contador> & ")"` |
| Fill | `Transparent` |
| Color | `If(ctxTabFic = "<Tab>", ThemeAccent, ThemeInk2)` |
| FontWeight | `If(ctxTabFic = "<Tab>", FontWeight.Semibold, FontWeight.Normal)` |
| HoverFill | `ThemeSurface2` |
| OnSelect | `UpdateContext({ctxTabFic: "<Tab>"}); Reset(inpBuscarTabFic)` |

> El subrayado de 2.5px de la tab activa del mockup (`.tabRow button.on`) no tiene
> equivalente de borde-por-lado en el Botón clásico (`BorderThickness` es uniforme en las 4
> aristas) — se aproxima con el cambio de `Color`/`FontWeight` arriba; si se quiere el
> subrayado literal, agregar un `shp…SubFic` (Rectángulo, `H:2.5`, `Y:Parent.Height-2.5`,
> `Fill: If(ctxTabFic="<Tab>", ThemeAccent, Transparent)`) como hijo de cada botón — **a
> verificar en Studio** si el ahorro visual justifica los 5 rectángulos extra (no están en
> el árbol de controles de esta guía; agregarlos es una mejora opcional, no obligatoria).

> **R1-4 (declarada, mismo tratamiento que el subrayado de arriba)**: el mockup dibuja el
> contador de cada tab como una píldora independiente (`.cnt`, fondo `--line-soft` en reposo /
> `--accent-soft` en la tab activa — un elemento visual con color propio, no texto plano). El
> `Text: "<Tab> (" & <Contador> & ")"` de arriba lo simplifica a texto concatenado dentro de
> la MISMA Etiqueta/Botón, sin el badge de color — divergencia D12 (ver "Divergencias
> declaradas" al final). Si se quiere el badge literal, agregar `lblCnt…SubFic` (Etiqueta
> chica, hija de cada botón de tab, `Fill: If(ctxTabFic=<Tab>, ThemeAccentSoft, ThemeLineSoft)`,
> `Color: If(ctxTabFic=<Tab>, ThemeAccent, ThemeInk2)`, Radius `9`, Text solo el número) — no
> está en el árbol de esta guía; agregarlo es una mejora opcional, no obligatoria (mismo
> criterio que el subrayado).

## 4. `cntToolbarTabFic` — buscador y comandos de la tab activa

Un solo buscador y un solo botón "+ Nueva…" compartidos por las 4 tabs que los usan
(Sucursales/Correos/Cuentas/Solicitudes); cambian de `HintText`/`Text`/`OnSelect` según
`ctxTabFic`. Bitácora no tiene buscador ni alta.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntToolbarTabFic` | Contenedor | `0` | `cntTabsFic.Y + cntTabsFic.Height` | `Parent.Width` | `46` | Fill `Transparent` |
| `inpBuscarTabFic` | Entrada de texto (SingleLine) | `14` | `9` | `260` | `28` | Fill `ThemeSurface` · BorderColor `ThemeLine` · Radius `8` · `Visible: =ctxTabFic <> "Bitácora"` · `HintText` y `OnChange` abajo |
| `lblGestionaTabFic` | Etiqueta | `btnNuevoTabFic.X - 8 - Self.Width` | `13` | `Len(Self.Text) * 4.9` | `20` | Size `7.5` · Color `ThemeInk3` · `Visible: =ctxTabFic in ["Sucursales","Correos","Cuentas"] && IsBlank(LookUp(colMisPermisos, Name = <tabla activa> && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))` · `Text` abajo |
| `btnNuevoTabFic` | Botón | `Parent.Width - 14 - Self.Width` | `9` | `Len(Self.Text) * 5.6 + 24` | `28` | ESTILO-BTN-CHICO-ACC · `Visible: =ctxTabFic in ["Sucursales","Correos","Cuentas"]` · `Text`/`DisplayMode`/`OnSelect` abajo |

`inpBuscarTabFic.HintText`:

```powerfx
=Switch(
    ctxTabFic,
    "Sucursales", "⌕ Buscar sucursal…",
    "Correos", "⌕ Buscar correo…",
    "Cuentas", "⌕ Buscar cuenta…",
    "Solicitudes", "⌕ N° de solicitud…",
    "⌕ Buscar…"
)
```

`inpBuscarTabFic.OnChange = false` (el filtro se lee en vivo del `.Text` dentro de cada
`Items` de galería — no hace falta lógica en el `OnChange`, ver §5–§9).

`lblGestionaTabFic.Text` (nota de "no tenés rol Contributor en X", como en el mockup):

```powerfx
="Gestiona: " & Switch(ctxTabFic, "Sucursales","Bóveda", "Correos","Banca Empresas", "Cuentas","Banca Empresas", "") &
  " — no tenés rol Contributor en " & ctxTabFic
```

`btnNuevoTabFic.Text = "+ Nueva " & Switch(ctxTabFic, "Sucursales","sucursal", "Correos","correo", "Cuentas","cuenta", "")`.

`btnNuevoTabFic.DisplayMode`:

```powerfx
=If(
    !IsBlank(
        LookUp(
            colMisPermisos,
            Name = Switch(ctxTabFic, "Sucursales","sanic_chtblclientbranch", "Correos","sanic_chtblclientemail", "Cuentas","sanic_chtblclientaccount", "")
            && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor
        )
    ),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

`btnNuevoTabFic.OnSelect`:

```powerfx
=UpdateContext(
    {
        varPanelModoFic: Switch(ctxTabFic, "Sucursales","sucursal", "Correos","correo", "Cuentas","cuenta", ""),
        varPanelAccionFic: "Alta",
        varPanelTargetIdFic: Blank(),
        varPanelAbiertoFic: true
    }
);
Reset(inpPanelCampoAFic);
Reset(inpPanelCampoBFic);
Reset(inpPanelCampoCFic);
Reset(inpMotivoPanelFic);
Clear(colCambiosPanelFic)
```

---

## 5. `cntGridSucursalesFic` — grilla de Sucursales

> **Divergencia D5** (heredada de `01-app.md`): la columna "Dirección" del mockup (CI1:
> "Km 4.5 Carretera Norte") **no existe** — `CH-Tbl-ClientBranch` solo tiene `Name`/`Client`/
> `ClientNameMirror`/`ClientStatus`/`RecordStatus` (verificado en `Entity.xml`). La grilla
> queda en 3 columnas (Sucursal · Estado · Solicitud) en vez de las 4 del mockup.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntGridSucursalesFic` | Contenedor | `0` | `cntToolbarTabFic.Y + cntToolbarTabFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` · `Visible: =ctxTabFic = "Sucursales"` |
| `lblHSucSucursalFic` | Etiqueta | `16` | `10` | `(Self.Parent.Width - 32) * 0.5` | `24` | ESTILO-HEADER-COL · Text `"SUCURSAL"` |
| `lblHSucEstadoFic` | Etiqueta | `lblHSucSucursalFic.X + lblHSucSucursalFic.Width` | `10` | `(cntGridSucursalesFic.Width - 32) * 0.25` | `24` | ESTILO-HEADER-COL · Text `"ESTADO"` |
| `lblHSucSolicitudFic` | Etiqueta | `lblHSucEstadoFic.X + lblHSucEstadoFic.Width` | `10` | `(cntGridSucursalesFic.Width - 32) * 0.25` | `24` | ESTILO-HEADER-COL · Text `"SOLICITUD"` |
| `shpHSucLineaFic` | Rectángulo | `0` | `lblHSucSucursalFic.Y + lblHSucSucursalFic.Height + 6` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `galSucursalesFic` | Galería vertical en blanco | `0` | `shpHSucLineaFic.Y + shpHSucLineaFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | `TemplateSize: =fxRowH` · `Items` abajo |

`galSucursalesFic.Items` (delegable: igualdad de lookup + `StartsWith` sobre texto,
ambas delegables en el conector de Dataverse):

```powerfx
=Filter(
    'CH-Tbl-ClientBranches',
    Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
    && (IsBlank(inpBuscarTabFic.Text) || StartsWith(Name, inpBuscarTabFic.Text))
)
```

**Regla de coordenadas del template**: X de cada control dentro del template = X del
encabezado − X de la galería (acá `galSucursalesFic.X = 0`, así que coincide 1:1 con las
X de los `lblH…`).

Controles del template (`Parent` = `galSucursalesFic`, `ThisItem` = la fila):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnFilaSucursalFic` | Botón (overlay, receta 7.4, AL FONDO del z-order) | `0` | `0` | `Parent.Width` | `TemplateSize` | Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · `OnSelect` = la acción real de fila (R2-3, ver fórmula abajo — antes vivía en `galSucursalesFic.OnSelect`) |
| `shpSelSucursalFic` | Rectángulo (receta 7.5) | `0` | `0` | `Parent.Width` | `TemplateSize` | Fill `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` |
| `shpSelBarraSucursalFic` | Rectángulo (receta 7.5) | `0` | `0` | `3` | `TemplateSize` | Fill `ThemeAccent` · `Visible: =ThisItem.IsSelected` |
| `lblSucNombreFic` | Etiqueta | `16` | `0` | `lblHSucSucursalFic.Width - 16` | `TemplateSize` | ESTILO-CELDA · Semibold · Text `ThisItem.Name` · `OnSelect: =Select(Parent)` |
| `pilSucEstadoFic` | Botón (pill, receta 7.3) | `lblHSucEstadoFic.X` | `(TemplateSize-20)/2` | `100` | `20` | Text/Fill/Color explícitos abajo (R1-7, enum `'RecordStatus (CH-Tbl-ClientBranches)'`, concordancia femenina "Activa"/"Inactiva") · `DisplayMode` y `OnSelect` propios, ver abajo (doble función: estado + disparador de Desactivar/Activar) |
| `pilSucRfcFic` | Botón (pill) | `lblHSucSolicitudFic.X` | `(TemplateSize-20)/2` | `100` | `20` | `Visible: =!IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclientbranch" && TargetRecordID = Text(ThisItem.'CH-Tbl-ClientBranch')))` · Fill `ThemeWarnSoft` · Color `ThemeWarn` · Text `"RFC en curso"` · DisplayMode `View` · Radius `12` · `OnSelect: =Select(Parent)` |
| `shpSucSepFic` | Rectángulo (receta 7.10) | `0` | `TemplateSize - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

**Decisión de diseño — Desactivar/Activar por fila**: el mockup CI1 solo dibuja
Sucursal/Estado/Solicitud (sin columna de acciones) y CI3 muestra el diálogo ya abierto sin
mostrar su disparador. En vez de agregar una 4ª columna/botón que el mockup no contempla, la
propia píldora de estado (`pilSucEstadoFic`) hace doble función — igual que el patrón
"click en la píldora cambia el estado" ya usado para las decisiones de RFC: si el usuario
tiene permiso y no hay RFC en curso sobre esa fila, el pill es clicable y abre el diálogo de
confirmación con la acción opuesta al estado actual (Activo → ofrece Desactivar; Inactivo →
ofrece Activar); si no, se comporta como una píldora de solo lectura que delega el clic a la
selección de fila (mismo patrón que el resto de las píldoras). Se declara acá para que quien
construya no la lea con `DisplayMode.View` fijo y crea que falta un botón.

`pilSucEstadoFic` — Text/Fill/Color explícitos (R1-7, receta 7.3; Sucursal es de género
femenino en español — "Activa"/"Inactiva", NO "Activo"/"Inactivo" como Cliente — a diferencia
de `pilEstadoClienteFic` (§2.1), donde esta guía ya escribía el `Switch` completo, acá se
delegaba a "el mapa genérico de §7.3" sin verificar la concordancia; corregido):

| Prop | Fórmula |
|---|---|
| Text | `Switch(ThisItem.RecordStatus, 'RecordStatus (CH-Tbl-ClientBranches)'.Active,"Activa", 'RecordStatus (CH-Tbl-ClientBranches)'.Inactive,"Inactiva", 'RecordStatus (CH-Tbl-ClientBranches)'.PendingApproval,"Pend. aprobación", 'RecordStatus (CH-Tbl-ClientBranches)'.PendingDeactivation,"Pend. desactivación", "—")` |
| Fill | `Switch(ThisItem.RecordStatus, 'RecordStatus (CH-Tbl-ClientBranches)'.Active,ThemeOkSoft, 'RecordStatus (CH-Tbl-ClientBranches)'.Inactive,ThemeLineSoft, 'RecordStatus (CH-Tbl-ClientBranches)'.PendingApproval,ThemeWarnSoft, 'RecordStatus (CH-Tbl-ClientBranches)'.PendingDeactivation,ThemeWarnSoft, ThemeLineSoft)` |
| Color | mismo `Switch`, sustituyendo `*Soft` por el tono sólido (`ThemeOk`/`ThemeInk3`/`ThemeWarn`/`ThemeWarn`) |
| Radius (las 4) / BorderThickness | `12` / `0` |

`pilSucEstadoFic.DisplayMode`:

```powerfx
=If(
    !IsBlank(LookUp(colMisPermisos, Name = "sanic_chtblclientbranch" && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))
    && IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclientbranch" && TargetRecordID = Text(ThisItem.'CH-Tbl-ClientBranch'))),
    DisplayMode.Edit,
    DisplayMode.View
)
```

`pilSucEstadoFic.OnSelect`:

```powerfx
=If(
    Self.DisplayMode = DisplayMode.Edit,
    UpdateContext(
        {
            varDlgAccionFic: If(ThisItem.RecordStatus = 'RecordStatus (CH-Tbl-ClientBranches)'.Inactive, "Activar", "Desactivar"),
            varDlgTargetTableFic: "sanic_chtblclientbranch",
            varDlgTargetTableLabelFic: "Sucursales",
            varDlgTargetTablePluralFic: "sanic_chtblclientbranchs",
            varDlgTargetIdFic: ThisItem.'CH-Tbl-ClientBranch',
            varDlgTargetNombreFic: ThisItem.Name
        }
    ),
    Select(Parent)
)
```

`btnFilaSucursalFic.OnSelect` (R2-3: la acción real de fila vive ACÁ, no en
`galSucursalesFic.OnSelect` — las galerías clásicas auto-seleccionan la 1ª fila al cargar
`Items`, lo que DISPARA el `OnSelect` de la GALERÍA sin clic del usuario (gotcha 8.2 de
`01-app.md`); si la acción viviera ahí, cualquier cliente con ≥1 sucursal abriría el panel de
edición automáticamente al entrar a la ficha. El `OnSelect` del BOTÓN overlay, en cambio, NO
se autodispara con el auto-select — solo corre con un tap real. `Select(Parent)` de los hijos
del template (arriba y en `pilSucEstadoFic.OnSelect`) queda intacto SOLO como resaltado de
fila, ya no como disparador de negocio — abre el panel de edición SOLO si hay permiso y no hay
RFC en curso sobre esa sucursal puntual):

```powerfx
=If(
    !IsBlank(LookUp(colMisPermisos, Name = "sanic_chtblclientbranch" && Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Contributor))
    && ThisItem.RecordStatus = 'RecordStatus (CH-Tbl-ClientBranches)'.Active
    && IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclientbranch" && TargetRecordID = Text(ThisItem.'CH-Tbl-ClientBranch'))),
    (
        UpdateContext(
            {
                varPanelModoFic: "sucursal",
                varPanelAccionFic: "Editar",
                varPanelTargetIdFic: ThisItem.'CH-Tbl-ClientBranch',
                varPanelAbiertoFic: true
            }
        );
        Reset(inpPanelCampoAFic);
        Reset(inpMotivoPanelFic);
        Clear(colCambiosPanelFic)
    )
)
```

> `galSucursalesFic.OnSelect` queda SIN fórmula (default de Studio) — el resaltado de fila
> (`shpSelSucursalFic`/`shpSelBarraSucursalFic`) no depende de esta propiedad, lee
> `ThisItem.IsSelected`, que Power Apps actualiza igual con o sin `OnSelect`. Este `OnSelect`
> del botón overlay cubre "Editar sucursal" (clic en cualquier punto de la fila que no esté
> tapado por un label/pill). "Desactivar/Activar sucursal" (CI3) sale de `pilSucEstadoFic` —
> ver la decisión de diseño documentada junto a esa fila arriba.

---

## 6. `cntGridCorreosFic` — grilla de Correos

> **Hallazgo crítico** (ver Notas al final): el catálogo `CH-Tbl-ClientTableLogicalFieldNames`
> mapea el campo "Correo electronico" a `sanic_name` — pero esa columna en
> `CH-Tbl-ClientEmail` es un **autonumérico de solo lectura** (`CE-{SEQNUM:1}`), NO la
> dirección de correo. La dirección real vive en la columna **`EmailAddress`** (display
> `'Email Address'`). Esta guía lee/muestra `'Email Address'` en todos lados (es la única
> lectura correcta); el `FieldDefinition` que se referencia al crear la línea del RFC sigue
> apuntando al registro sembrado tal cual está HOY — que aplicará mal en el backend hasta que
> se corrija `data/seed/10-clienttablelogicalfieldnames.json`. Ver detalle abajo.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntGridCorreosFic` | Contenedor | `0` | `cntToolbarTabFic.Y + cntToolbarTabFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` · `Visible: =ctxTabFic = "Correos"` |
| `lblHCorCorreoFic` | Etiqueta | `16` | `10` | `(cntGridCorreosFic.Width - 32) * 0.4` | `24` | ESTILO-HEADER-COL · Text `"CORREO"` |
| `lblHCorNotifFic` | Etiqueta | `lblHCorCorreoFic.X + lblHCorCorreoFic.Width` | `10` | `(cntGridCorreosFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"NOTIFICAR SIEMPRE"` |
| `lblHCorEstadoFic` | Etiqueta | `lblHCorNotifFic.X + lblHCorNotifFic.Width` | `10` | `(cntGridCorreosFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"ESTADO"` |
| `lblHCorSolicitudFic` | Etiqueta | `lblHCorEstadoFic.X + lblHCorEstadoFic.Width` | `10` | `(cntGridCorreosFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"SOLICITUD"` |
| `shpHCorLineaFic` | Rectángulo | `0` | `lblHCorCorreoFic.Y + lblHCorCorreoFic.Height + 6` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `galCorreosFic` | Galería vertical en blanco | `0` | `shpHCorLineaFic.Y + shpHCorLineaFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | `TemplateSize: =fxRowH` · `Items` abajo |

`galCorreosFic.Items`:

```powerfx
=Filter(
    'CH-Tbl-ClientEmails',
    Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
    && (IsBlank(inpBuscarTabFic.Text) || StartsWith('Email Address', inpBuscarTabFic.Text))
)
```

Controles del template (`Parent` = `galCorreosFic`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnFilaCorreoFic` | Botón (overlay) | `0` | `0` | `Parent.Width` | `TemplateSize` | mismo estilo (Fill/Hover/Pressed/BorderThickness/Text) que `btnFilaSucursalFic`; `OnSelect` propio abajo (R2-3) |
| `shpSelCorreoFic` | Rectángulo (receta 7.5) | `0` | `0` | `Parent.Width` | `TemplateSize` | igual que `shpSelSucursalFic` |
| `shpSelBarraCorreoFic` | Rectángulo (receta 7.5) | `0` | `0` | `3` | `TemplateSize` | igual que `shpSelBarraSucursalFic` |
| `lblCorDireccionFic` | Etiqueta | `16` | `0` | `lblHCorCorreoFic.Width - 16` | `TemplateSize` | ESTILO-CELDA · Semibold · Text `ThisItem.'Email Address'` · `OnSelect: =Select(Parent)` |
| `lblCorNotifFic` | Etiqueta | `lblHCorNotifFic.X` | `0` | `lblHCorNotifFic.Width` | `TemplateSize` | ESTILO-CELDA · `Text: =If(ThisItem.AlwaysNotify, "Sí", "No")` · `OnSelect: =Select(Parent)` |
| `pilCorEstadoFic` | Botón (pill, receta 7.3) | `lblHCorEstadoFic.X` | `(TemplateSize-20)/2` | `100` | `20` | Text/Fill/Color explícitos abajo (R1-7, enum `'RecordStatus (CH-Tbl-ClientEmails)'`, concordancia masculina "Activo"/"Inactivo") · mismo doble-uso que `pilSucEstadoFic` (ver `DisplayMode`/`OnSelect` abajo) |
| `pilCorRfcFic` | Botón (pill) | `lblHCorSolicitudFic.X` | `(TemplateSize-20)/2` | `100` | `20` | `Visible: =!IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclientemail" && TargetRecordID = Text(ThisItem.'CH-Tbl-ClientEmail')))` · igual estilo que `pilSucRfcFic` |
| `shpCorSepFic` | Rectángulo (receta 7.10) | `0` | `TemplateSize - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

`pilCorEstadoFic` — Text/Fill/Color explícitos (R1-7, receta 7.3; Correo es de género
masculino — "Activo"/"Inactivo"):

| Prop | Fórmula |
|---|---|
| Text | `Switch(ThisItem.RecordStatus, 'RecordStatus (CH-Tbl-ClientEmails)'.Active,"Activo", 'RecordStatus (CH-Tbl-ClientEmails)'.Inactive,"Inactivo", 'RecordStatus (CH-Tbl-ClientEmails)'.PendingApproval,"Pend. aprobación", 'RecordStatus (CH-Tbl-ClientEmails)'.PendingDeactivation,"Pend. desactivación", "—")` |
| Fill | `Switch(ThisItem.RecordStatus, 'RecordStatus (CH-Tbl-ClientEmails)'.Active,ThemeOkSoft, 'RecordStatus (CH-Tbl-ClientEmails)'.Inactive,ThemeLineSoft, 'RecordStatus (CH-Tbl-ClientEmails)'.PendingApproval,ThemeWarnSoft, 'RecordStatus (CH-Tbl-ClientEmails)'.PendingDeactivation,ThemeWarnSoft, ThemeLineSoft)` |
| Color | mismo `Switch`, sustituyendo `*Soft` por el tono sólido (`ThemeOk`/`ThemeInk3`/`ThemeWarn`/`ThemeWarn`) |
| Radius (las 4) / BorderThickness | `12` / `0` |

`pilCorEstadoFic.DisplayMode` y `.OnSelect`: idénticos a `pilSucEstadoFic`, sustituyendo
`"sanic_chtblclientbranch"` → `"sanic_chtblclientemail"`, `'CH-Tbl-ClientBranch'` →
`'CH-Tbl-ClientEmail'`, `RecordStatus (CH-Tbl-ClientBranches)` → `RecordStatus (CH-Tbl-
ClientEmails)`, `varDlgTargetTableLabelFic: "Correos"`, `varDlgTargetTablePluralFic:
"sanic_chtblclientemails"`, `varDlgTargetNombreFic: ThisItem.'Email Address'`.

`btnFilaCorreoFic.OnSelect` (R2-3, mismo motivo que `btnFilaSucursalFic.OnSelect` en §5 — la
acción NO puede vivir en `galCorreosFic.OnSelect` porque el auto-select de la 1ª fila la
dispararía sin clic del usuario): idéntico a `btnFilaSucursalFic.OnSelect`, mismas
sustituciones (`sanic_chtblclientemail`, `varPanelModoFic: "correo"`, `varPanelTargetIdFic:
ThisItem.'CH-Tbl-ClientEmail'`, `RecordStatus (CH-Tbl-ClientEmails)`). `galCorreosFic.OnSelect`
queda sin fórmula, igual que `galSucursalesFic.OnSelect` (§5); `Select(Parent)` de los hijos
del template (`lblCorDireccionFic`, `lblCorNotifFic`, `pilCorRfcFic`, fallback de
`pilCorEstadoFic`) queda intacto solo como resaltado de fila.

---

## 7. `cntGridCuentasFic` — grilla de Cuentas (números enmascarados)

> Gotcha 8.7: `CH-Tbl-ClientAccount` expone su estado como **`CH-Ch-RecordStatus`**, no
> `RecordStatus` — copiar/pegar la fórmula de estado de Sucursales/Correos sin ajustar el
> nombre de columna rompe acá. Gotcha 8.16: `sanic_Name` (display "Numero de cuenta") es el
> número CRUDO — se enmascara 100% client-side, nunca confiar en que ya viene enmascarado.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntGridCuentasFic` | Contenedor | `0` | `cntToolbarTabFic.Y + cntToolbarTabFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` · `Visible: =ctxTabFic = "Cuentas"` |
| `lblHCtaNumeroFic` | Etiqueta | `16` | `10` | `(cntGridCuentasFic.Width - 32) * 0.4` | `24` | ESTILO-HEADER-COL · Text `"NÚMERO DE CUENTA"` |
| `lblHCtaMonedaFic` | Etiqueta | `lblHCtaNumeroFic.X + lblHCtaNumeroFic.Width` | `10` | `(cntGridCuentasFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"MONEDA"` |
| `lblHCtaEstadoFic` | Etiqueta | `lblHCtaMonedaFic.X + lblHCtaMonedaFic.Width` | `10` | `(cntGridCuentasFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"ESTADO"` |
| `lblHCtaSolicitudFic` | Etiqueta | `lblHCtaEstadoFic.X + lblHCtaEstadoFic.Width` | `10` | `(cntGridCuentasFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"SOLICITUD"` |
| `shpHCtaLineaFic` | Rectángulo | `0` | `lblHCtaNumeroFic.Y + lblHCtaNumeroFic.Height + 6` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `galCuentasFic` | Galería vertical en blanco | `0` | `shpHCtaLineaFic.Y + shpHCtaLineaFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | `TemplateSize: =fxRowH` · `Items` abajo |

`galCuentasFic.Items` (R2-7, corrige prosa inconsistente con el propio `Visible` de §4 — el
buscador NO filtra por número de cuenta — filtrar server-side por un dato sensible sin
enmascarar sería exponerlo en la telemetría de red. `inpBuscarTabFic.Visible` de §4 **no**
excluye `"Cuentas"`, así que el buscador queda VISIBLE también en esta tab, pero inerte: ningún
texto tipeado filtra `galCuentasFic.Items` de abajo, esta tab se navega por scroll — divergencia
declarada, ver "Aproximaciones honestas" en Notas):

```powerfx
=Filter('CH-Tbl-ClientAccounts', Client.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client')
```

Controles del template (`Parent` = `galCuentasFic`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnFilaCuentaFic` | Botón (overlay) | `0` | `0` | `Parent.Width` | `TemplateSize` | mismo estilo que `btnFilaSucursalFic`; `OnSelect` propio abajo (R2-3) |
| `shpSelCuentaFic` | Rectángulo (receta 7.5) | `0` | `0` | `Parent.Width` | `TemplateSize` | igual que `shpSelSucursalFic` |
| `shpSelBarraCuentaFic` | Rectángulo (receta 7.5) | `0` | `0` | `3` | `TemplateSize` | igual que `shpSelBarraSucursalFic` |
| `lblCtaNumeroFic` | Etiqueta | `16` | `0` | `lblHCtaNumeroFic.Width - 44` | `TemplateSize` | ESTILO-CELDA-NUM · Semibold · `Text` abajo · `OnSelect: =Select(Parent)` |
| `btnCtaRevelarFic` | Botón (icono) | `lblCtaNumeroFic.X + lblCtaNumeroFic.Width` | `(TemplateSize-24)/2` | `24` | `24` | Fill `Transparent` · BorderThickness `0` · `Text` abajo · `OnSelect` abajo |
| `lblCtaMonedaFic` | Etiqueta | `lblHCtaMonedaFic.X` | `0` | `lblHCtaMonedaFic.Width` | `TemplateSize` | ESTILO-CELDA · `Text: =Switch(ThisItem.Moneda, 'CH-Ch-Currency'.NIO,"NIO", 'CH-Ch-Currency'.USD,"USD", 'CH-Ch-Currency'.EUR,"EUR", "—")` · `OnSelect: =Select(Parent)` |
| `pilCtaEstadoFic` | Botón (pill, receta 7.3) | `lblHCtaEstadoFic.X` | `(TemplateSize-20)/2` | `100` | `20` | Text/Fill/Color explícitos abajo (R1-7, enum `'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'`, gotcha 8.7 — **no** `RecordStatus` — concordancia femenina "Activa"/"Inactiva") · mismo doble-uso que `pilSucEstadoFic`, ver abajo |
| `pilCtaRfcFic` | Botón (pill) | `lblHCtaSolicitudFic.X` | `(TemplateSize-20)/2` | `100` | `20` | `Visible: =!IsBlank(LookUp(colRfcEnCursoFic, TargetTable = "sanic_chtblclientaccount" && TargetRecordID = Text(ThisItem.'CH-Tbl-ClientAccount')))` · igual estilo que `pilSucRfcFic` |
| `shpCtaSepFic` | Rectángulo (receta 7.10) | `0` | `TemplateSize - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

`lblCtaNumeroFic.Text` (máscara + revelado bajo demanda vía `colCuentasRevFic`):

```powerfx
=If(
    ThisItem.'CH-Tbl-ClientAccount' in colCuentasRevFic.Id,
    ThisItem.'Numero de cuenta',
    "•••• " & Right(ThisItem.'Numero de cuenta', 4)
)
```

`btnCtaRevelarFic.Text = If(ThisItem.'CH-Tbl-ClientAccount' in colCuentasRevFic.Id, "🙈", "👁")`.

`btnCtaRevelarFic.OnSelect`:

```powerfx
=If(
    ThisItem.'CH-Tbl-ClientAccount' in colCuentasRevFic.Id,
    Remove(colCuentasRevFic, LookUp(colCuentasRevFic, Id = ThisItem.'CH-Tbl-ClientAccount')),
    Collect(colCuentasRevFic, {Id: ThisItem.'CH-Tbl-ClientAccount'})
)
```

> `btnCtaRevelarFic` NO llama `Select(Parent)` — es la única excepción al patrón "todo hijo
> del template delega a la galería" (gotcha 8.4): revelar/ocultar el número es una acción
> propia de esa celda, no debe además seleccionar la fila ni disparar el flujo de edición.

`pilCtaEstadoFic` — Text/Fill/Color explícitos (R1-7, receta 7.3; Cuenta es de género
femenino — "Activa"/"Inactiva"; nótese `ThisItem.'CH-Ch-RecordStatus'`, NO
`ThisItem.RecordStatus` — gotcha 8.7):

| Prop | Fórmula |
|---|---|
| Text | `Switch(ThisItem.'CH-Ch-RecordStatus', 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.Active,"Activa", 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.Inactive,"Inactiva", 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.PendingApproval,"Pend. aprobación", 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.PendingDeactivation,"Pend. desactivación", "—")` |
| Fill | `Switch(ThisItem.'CH-Ch-RecordStatus', 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.Active,ThemeOkSoft, 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.Inactive,ThemeLineSoft, 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.PendingApproval,ThemeWarnSoft, 'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.PendingDeactivation,ThemeWarnSoft, ThemeLineSoft)` |
| Color | mismo `Switch`, sustituyendo `*Soft` por el tono sólido (`ThemeOk`/`ThemeInk3`/`ThemeWarn`/`ThemeWarn`) |
| Radius (las 4) / BorderThickness | `12` / `0` |

`pilCtaEstadoFic.DisplayMode` y `.OnSelect`: mismo patrón que `pilSucEstadoFic`, con estas
sustituciones — `Name = "sanic_chtblclientaccount"`, `ThisItem.'CH-Ch-RecordStatus'` (NO
`ThisItem.RecordStatus` — gotcha 8.7), enum `'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'`,
`varDlgTargetTableLabelFic: "Cuentas"`, `varDlgTargetTablePluralFic:
"sanic_chtblclientaccounts"`, `varDlgTargetNombreFic: "•••• " & Right(ThisItem.'Numero de
cuenta', 4)` (el título del diálogo de confirmación tampoco expone el número completo).

`btnFilaCuentaFic.OnSelect` (R2-3, mismo motivo que §5/§6 — no puede vivir en
`galCuentasFic.OnSelect`, que se autodispara con el auto-select de la 1ª fila): mismo patrón
que `btnFilaSucursalFic.OnSelect`, con `sanic_chtblclientaccount`, `varPanelModoFic: "cuenta"`,
`varPanelTargetIdFic: ThisItem.'CH-Tbl-ClientAccount'`, `ThisItem.'CH-Ch-RecordStatus' =
'CH-Ch-RecordStatus (CH-Tbl-ClientAccounts)'.Active` (gotcha 8.7 aplica también acá).
`galCuentasFic.OnSelect` queda sin fórmula, igual que en §5/§6; `Select(Parent)` de
`lblCtaNumeroFic`, `lblCtaMonedaFic`, `pilCtaRfcFic` y el fallback de `pilCtaEstadoFic` queda
intacto solo como resaltado de fila (`btnCtaRevelarFic` sigue siendo la única excepción, ver
nota arriba — no delega a nada, ni al `Select(Parent)` ni al botón overlay).

---

## 8. `cntGridSolicitudesFic` — grilla de Solicitudes (RFCs de este cliente)

Todos los RFC relacionados a este cliente, de **cualquier tabla objetivo** (Cliente,
Sucursales, Correos o Cuentas) — solo lectura, sin gate de Contributor: cualquier usuario
con acceso a la ficha puede VER el historial de solicitudes; decidir (Aprobar/Rechazar) pasa
por `scrSolicitud` (guía 05), gateado ahí por rol Approver.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntGridSolicitudesFic` | Contenedor | `0` | `cntToolbarTabFic.Y + cntToolbarTabFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` · `Visible: =ctxTabFic = "Solicitudes"` |
| `lblHSolNumeroFic` | Etiqueta | `16` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.15` | `24` | ESTILO-HEADER-COL · Text `"SOLICITUD"` |
| `lblHSolTablaFic` | Etiqueta | `lblHSolNumeroFic.X + lblHSolNumeroFic.Width` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.15` | `24` | ESTILO-HEADER-COL · Text `"TABLA"` |
| `lblHSolTipoFic` | Etiqueta | `lblHSolTablaFic.X + lblHSolTablaFic.Width` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.15` | `24` | ESTILO-HEADER-COL · Text `"TIPO"` |
| `lblHSolEstadoFic` | Etiqueta | `lblHSolTipoFic.X + lblHSolTipoFic.Width` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.15` | `24` | ESTILO-HEADER-COL · Text `"ESTADO"` |
| `lblHSolCreadaPorFic` | Etiqueta | `lblHSolEstadoFic.X + lblHSolEstadoFic.Width` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"CREADA POR"` |
| `lblHSolFechaFic` | Etiqueta | `lblHSolCreadaPorFic.X + lblHSolCreadaPorFic.Width` | `10` | `(cntGridSolicitudesFic.Width - 32) * 0.2` | `24` | ESTILO-HEADER-COL · Text `"ENVIADA"` · Align `Right` |
| `shpHSolLineaFic` | Rectángulo | `0` | `lblHSolNumeroFic.Y + lblHSolNumeroFic.Height + 6` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `galSolicitudesFic` | Galería vertical en blanco | `0` | `shpHSolLineaFic.Y + shpHSolLineaFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | `TemplateSize: =fxRowH` · `Items` abajo |

`galSolicitudesFic.Items`:

```powerfx
=SortByColumns(
    Filter(
        'CH-Tbl-ClientDataChangeRequests',
        RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
        && (IsBlank(inpBuscarTabFic.Text) || StartsWith(Name, inpBuscarTabFic.Text))
    ),
    "CreatedOn",
    SortOrder.Descending
)
```

Controles del template (`Parent` = `galSolicitudesFic`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnFilaSolicitudFic` | Botón (overlay) | `0` | `0` | `Parent.Width` | `TemplateSize` | mismo estilo que `btnFilaSucursalFic`; `OnSelect` propio abajo (R2-3 — CONTRATO DE SALIDA) |
| `shpSelSolicitudFic` | Rectángulo (receta 7.5) | `0` | `0` | `Parent.Width` | `TemplateSize` | igual que `shpSelSucursalFic` |
| `shpSelBarraSolicitudFic` | Rectángulo (receta 7.5) | `0` | `0` | `3` | `TemplateSize` | igual que `shpSelBarraSucursalFic` |
| `lblSolNumeroFic` | Etiqueta | `16` | `0` | `lblHSolNumeroFic.Width - 16` | `TemplateSize` | ESTILO-CELDA · Semibold · Text `ThisItem.Name` · `OnSelect: =Select(Parent)` |
| `lblSolTablaFic` | Etiqueta | `lblHSolTablaFic.X` | `0` | `lblHSolTablaFic.Width` | `TemplateSize` | ESTILO-CELDA · Text `ThisItem.TargetTableDisplayName` · `OnSelect: =Select(Parent)` |
| `pilSolTipoFic` | Botón (pill, receta 7.3) | `lblHSolTipoFic.X` | `(TemplateSize-20)/2` | `90` | `20` | mapa de TIPO (§7.3 de 01-app.md) sobre `ThisItem.Type` · `OnSelect: =Select(Parent)` |
| `pilSolEstadoFic` | Botón (pill, receta 7.3) | `lblHSolEstadoFic.X` | `(TemplateSize-20)/2` | `90` | `20` | mapa de ESTADO del RFC (§7.3) sobre `ThisItem.'Change Status'` · **`BorderThickness: 0` en TODOS los tonos, incluido el neutro (Borrador/Cancelada)** — se aparta del default de la receta 7.3 (divergencia D13, ver abajo) · `OnSelect: =Select(Parent)` |
| `lblSolCreadaPorFic` | Etiqueta | `lblHSolCreadaPorFic.X` | `0` | `lblHSolCreadaPorFic.Width` | `TemplateSize` | ESTILO-META · Text `ThisItem.'Created By'.'Full Name'` · `OnSelect: =Select(Parent)` |
| `lblSolFechaFic` | Etiqueta | `lblHSolFechaFic.X` | `0` | `lblHSolFechaFic.Width - 16` | `TemplateSize` | ESTILO-CELDA-NUM · Text `Text(ThisItem.'Created On', FmtFecha)` · `OnSelect: =Select(Parent)` |
| `shpSolSepFic` | Rectángulo (receta 7.10) | `0` | `TemplateSize - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

`btnFilaSolicitudFic.OnSelect` (**CONTRATO DE SALIDA**: única salida de `gvarSolicitudSel`/
`gvarClienteSel` de toda esta pantalla. R2-3, ALTA severidad acá en particular: si esta
fórmula viviera en `galSolicitudesFic.OnSelect` como en el diseño original, el auto-select de
la 1ª fila al cargar `Items` (gotcha 8.2) dispararía una **navegación fantasma a
`scrSolicitud` apenas se entra a la ficha**, sin que el usuario haya tocado nada — cualquier
cliente con ≥1 solicitud expulsaría de `scrCliente` automáticamente. Por eso la navegación va
en el `OnSelect` del botón overlay, que solo corre con un tap real. Además setea
`gvarClienteSel` explícitamente — hallazgo de consistencia cruzada 03↔05: el CONTRATO DE
ENTRADA de `05-pantalla-solicitud.md` ya listaba esta pantalla como origen de
`gvarClienteSel`, pero antes de esta corrección solo "funcionaba" porque la variable ya traía
el mismo cliente de la navegación previa hacia `scrCliente` — no estaba garantizado; mismo
patrón que `galSolicitudesSol.OnSelect` de `04-pantalla-solicitudes.md`):

```powerfx
=Set(gvarSolicitudSel, ThisItem);
Set(gvarClienteSel, ThisItem.RelatedClient);
Navigate(scrSolicitud, ScreenTransition.None)
```

> `galSolicitudesFic.OnSelect` queda sin fórmula. `Select(Parent)` de `lblSolNumeroFic`,
> `lblSolTablaFic`, `pilSolTipoFic`, `pilSolEstadoFic`, `lblSolCreadaPorFic`, `lblSolFechaFic`
> queda intacto solo como resaltado de fila, ya no dispara la navegación.

---

## 9. `cntGridBitacoraFic` — grilla de Bitácora (auditoría)

Solo lectura, sin buscador (`inpBuscarTabFic.Visible = ctxTabFic <> "Bitácora"`, §4), sin
gate de permisos — la escribe la Custom API server-side (§2 de `01-app.md`).

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntGridBitacoraFic` | Contenedor | `0` | `cntToolbarTabFic.Y + cntToolbarTabFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | Fill `Transparent` · `Visible: =ctxTabFic = "Bitácora"` |
| `lblHBitFechaFic` | Etiqueta | `16` | `10` | `(cntGridBitacoraFic.Width - 32) * 0.18` | `24` | ESTILO-HEADER-COL · Text `"FECHA"` |
| `lblHBitOperacionFic` | Etiqueta | `lblHBitFechaFic.X + lblHBitFechaFic.Width` | `10` | `(cntGridBitacoraFic.Width - 32) * 0.16` | `24` | ESTILO-HEADER-COL · Text `"OPERACIÓN"` |
| `lblHBitResultadoFic` | Etiqueta | `lblHBitOperacionFic.X + lblHBitOperacionFic.Width` | `10` | `(cntGridBitacoraFic.Width - 32) * 0.14` | `24` | ESTILO-HEADER-COL · Text `"RESULTADO"` |
| `lblHBitMensajeFic` | Etiqueta | `lblHBitResultadoFic.X + lblHBitResultadoFic.Width` | `10` | `(cntGridBitacoraFic.Width - 32) * 0.52` | `24` | ESTILO-HEADER-COL · Text `"MENSAJE"` |
| `shpHBitLineaFic` | Rectángulo | `0` | `lblHBitFechaFic.Y + lblHBitFechaFic.Height + 6` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `galBitacoraFic` | Galería vertical en blanco | `0` | `shpHBitLineaFic.Y + shpHBitLineaFic.Height` | `Parent.Width` | `Parent.Height - Self.Y` | `TemplateSize: =60` (mayor que `fxRowH`: el mensaje envuelve, gotcha 8.15) · `Items` abajo |

`galBitacoraFic.Items`:

```powerfx
=SortByColumns(
    Filter('CH-Tbl-ClientDataChangesAuditTrails', RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'),
    "CreatedOn",
    SortOrder.Descending
)
```

Controles del template (`Parent` = `galBitacoraFic`; sin overlay ni fila clicable — la
bitácora es puramente informativa, no navega a ningún lado):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblBitFechaFic` | Etiqueta | `16` | `0` | `lblHBitFechaFic.Width - 16` | `TemplateSize` | ESTILO-CELDA · Text `Text(ThisItem.'Created On', FmtFecha)` |
| `lblBitOperacionFic` | Etiqueta | `lblHBitOperacionFic.X` | `0` | `lblHBitOperacionFic.Width` | `TemplateSize` | ESTILO-CELDA · `Text: =Switch(ThisItem.OpeationType, 'OpeationType (CH-Tbl-ClientDataChangesAuditTrails)'.Create,"Alta", 'OpeationType (CH-Tbl-ClientDataChangesAuditTrails)'.Update,"Edición", 'OpeationType (CH-Tbl-ClientDataChangesAuditTrails)'.Deactivate,"Desactivación", 'OpeationType (CH-Tbl-ClientDataChangesAuditTrails)'.Activate,"Activación", "—")` |
| `pilBitResultadoFic` | Botón (pill, receta 7.3) | `lblHBitResultadoFic.X` | `(TemplateSize-20)/2` | `80` | `20` | `Text: =If(ThisItem.Result, "Éxito", "Fallo")` · Fill `If(ThisItem.Result, ThemeOkSoft, ThemeDangerSoft)` · Color `If(ThisItem.Result, ThemeOk, ThemeDanger)` · Radius `12` · DisplayMode `View` |
| `lblBitMensajeFic` | Etiqueta | `lblHBitMensajeFic.X` | `8` | `lblHBitMensajeFic.Width - 16` | `TemplateSize - 16` | ESTILO-META · Text `ThisItem.Message` · `Wrap: true` |
| `shpBitSepFic` | Rectángulo (receta 7.10) | `0` | `TemplateSize - 1` | `Parent.Width` | `1` | Fill `ThemeLineSoft` |

> `sanic_OpeationType` — typo real del esquema (falta la "r" de "Operation", ver `Schema.cs`
> `ChangeRequestAuditTrail.OperationType`: `"sanic_opeationtype"`). **NO corregir** al
> escribir el nombre del option set en Power Fx (`'OpeationType (CH-Tbl-
> ClientDataChangesAuditTrails)'`) — IntelliSense lo autogenera con el typo tal cual está en
> el esquema publicado.

---

## 10. `cntPanelFic` — panel lateral de alta/edición (CI2)

Chrome fijo por receta 7.7 de `01-app.md`; el CUERPO cambia según `varPanelModoFic`. Un solo
panel reusado por los 4 modos (cliente/sucursal/correo/cuenta) y las 2 acciones
(Editar/Alta) — evita 8 componentes casi idénticos.

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpVeloPanelFic` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · `Visible: =varPanelAbiertoFic` |
| `btnVeloClicPanelFic` | Botón | `0` | `0` | `shpVeloPanelFic.Width - fxPanelW` | `Parent.Height` | Fill/Hover `Transparent` · Text `""` · `Visible: =varPanelAbiertoFic` · `OnSelect: =UpdateContext({varPanelAbiertoFic: false})` |
| `cntPanelCardFic` | Contenedor | `Parent.Width - fxPanelW` | `0` | `fxPanelW` | `Parent.Height` | Fill `ThemeSurface` · BorderColor `ThemeLine` (izquierdo, `1`) · `DropShadow: DropShadow.Regular` · `Visible: =varPanelAbiertoFic` |
| `cntPanelHeadFic` | Contenedor | `0` | `0` | `Parent.Width` | `64` | Fill `Transparent` · BorderColor `ThemeLine` (inferior, `1`) |
| `lblPanelTituloFic` | Etiqueta | `20` | `16` | `Parent.Width - 60` | `20` | Size `10.88` · Bold · `Wrap: true` · Text abajo |
| `lblPanelSubFic` | Etiqueta | `20` | `lblPanelTituloFic.Y + lblPanelTituloFic.Height + 2` | `Parent.Width - 40` | `28` | Size `8.63` · Color `ThemeInk3` · `Wrap: true` · `AutoHeight: true` · Text abajo |
| `icoPanelCerrarFic` | Icono (`Icon.Cancel`) | `Parent.Width - 30` | `18` | `18` | `18` | Color `ThemeInk3` · HoverColor `ThemeInk` · `OnSelect: =UpdateContext({varPanelAbiertoFic: false})` |
| `cntPanelBodyFic` | Contenedor | `0` | `cntPanelHeadFic.Y + cntPanelHeadFic.Height` | `Parent.Width` | `Parent.Height - Self.Y - 64` | Fill `Transparent` |
| `cntPanelFootFic` | Contenedor | `0` | `Parent.Height - 64` | `Parent.Width` | `64` | Fill `Transparent` · BorderColor `ThemeLine` (superior, `1`) |
| `btnPanelCancelarFic` | Botón | `btnPanelConfirmarFic.X - 8 - Self.Width` | `16` | `100` | `32` | ESTILO-BTN-FANTASMA · Text `"Cancelar"` · `OnSelect: =UpdateContext({varPanelAbiertoFic: false})` |
| `btnPanelConfirmarFic` | Botón | `Parent.Width - 20 - Self.Width` | `16` | `200` | `32` | ESTILO-BTN-PRIMARIO · Text/`DisplayMode`/`OnSelect` en §10.4 (`DisplayMode` también se apaga con `varPanelCargandoFic`, R2-5) |

`lblPanelTituloFic.Text`:

```powerfx
=Switch(
    varPanelModoFic,
    "cliente", "Solicitar edición de datos del cliente",
    "sucursal", If(varPanelAccionFic = "Alta", "Solicitar alta de sucursal", "Solicitar edición de sucursal"),
    "correo", If(varPanelAccionFic = "Alta", "Solicitar alta de correo autorizado", "Solicitar edición de correo autorizado"),
    "cuenta", If(varPanelAccionFic = "Alta", "Solicitar alta de cuenta bancaria", "Solicitar edición de cuenta bancaria"),
    ""
)
```

`lblPanelSubFic.Text` (nombre del cliente + registro puntual + a quién le toca aprobar, como
en CI2 — "Distribuidora La Colonia · correo **pagos@lacolonia.com.ni**"; en modo `"cliente"`
y en Alta no hay un registro puntual previo que citar, así que el `Switch` cae al `""` por
defecto y el subtítulo queda solo con el nombre del cliente):

```powerfx
=gvarClienteSel.Name &
  If(
      varPanelAccionFic = "Editar",
      Switch(
          varPanelModoFic,
          "sucursal", " · sucursal " & LookUp('CH-Tbl-ClientBranches', 'CH-Tbl-ClientBranch' = varPanelTargetIdFic).Name,
          "correo", " · correo " & LookUp('CH-Tbl-ClientEmails', 'CH-Tbl-ClientEmail' = varPanelTargetIdFic).'Email Address',
          "cuenta", " · cuenta •••• " & Right(LookUp('CH-Tbl-ClientAccounts', 'CH-Tbl-ClientAccount' = varPanelTargetIdFic).'Numero de cuenta', 4),
          ""
      ),
      ""
  ) & ". Los cambios se aplican solo cuando otro miembro de " &
  Switch(varPanelModoFic, "sucursal","Bóveda", "Banca de Empresas") & " apruebe la solicitud."
```

> El número de cuenta se cita enmascarado (`"•••• " & Right(...,4)`), igual que
> `lblCampoAActualFic` en modo `"cuenta"` (§10.1) — este subtítulo es persistente en pantalla,
> no el propio campo de edición, así que aplica la misma regla de nunca mostrar el número
> completo fuera del input que el usuario ya está tipeando (gotcha 8.16).

`btnPanelConfirmarFic.Text = If(varPanelCargandoFic, "Aplicando…", "Crear y enviar solicitud")`
(mismo texto "Crear y enviar solicitud" en ambas acciones — el mockup CI2 usa exactamente esta
leyenda; `"Aplicando…"` mientras corre la secuencia de 3 escrituras de §10.4, R2-5, mismo
patrón que `btnOkDlg` de `cmpConfirmDialog` en `01-app.md` §7.6).

### 10.1 Cuerpo del panel — campos por `varPanelModoFic`

Molde por campo (receta 7.7 de `01-app.md`): `lblCampoXLabelFic` (ESTILO-CAMPO-LABEL) +
`lblCampoXActualFic` (ESTILO-META, "Actual: **valor**", oculto en Alta) + `inpPanelCampoXFic`
o `drpPanelCampoXFic` (Fill `ThemeSurface` · BorderColor `ThemeLine` o `ThemeDanger` si hay
error · Radius `7`) + `lblCampoXErrorFic` (ESTILO-CAMPO-ERROR, visible si aplica).

| Control | Tipo | X | Y | Width | Height |
|---|---|---|---|---|---|
| `lblCampoALabelFic` | Etiqueta | `20` | `0` | `Parent.Width - 40` | `16` |
| `lblCampoAActualFic` | Etiqueta | `20` | `lblCampoALabelFic.Y + lblCampoALabelFic.Height + 2` | `Parent.Width - 40` | `16` |
| `inpPanelCampoAFic` | Entrada de texto (SingleLine) | `20` | `lblCampoAActualFic.Y + lblCampoAActualFic.Height + 4` | `Parent.Width - 40` | `34` |
| `lblCampoAErrorFic` | Etiqueta | `20` | `inpPanelCampoAFic.Y + inpPanelCampoAFic.Height + 3` | `Parent.Width - 40` | `14` |
| `lblCampoBLabelFic` | Etiqueta | `20` | `lblCampoAErrorFic.Y + lblCampoAErrorFic.Height + 12` | `Parent.Width - 40` | `16` |
| `lblCampoBActualFic` | Etiqueta | `20` | `lblCampoBLabelFic.Y + lblCampoBLabelFic.Height + 2` | `Parent.Width - 40` | `16` |
| `inpPanelCampoBFic` | Entrada de texto (SingleLine) | `20` | `lblCampoBActualFic.Y + lblCampoBActualFic.Height + 4` | `Parent.Width - 40` | `34` |
| `drpPanelCampoBFic` | Menú desplegable | `20` | `lblCampoBActualFic.Y + lblCampoBActualFic.Height + 4` | `Parent.Width - 40` | `34` |
| `lblCampoBErrorFic` | Etiqueta | `20` | `inpPanelCampoBFic.Y + inpPanelCampoBFic.Height + 3` | `Parent.Width - 40` | `14` |
| `lblCampoCLabelFic` | Etiqueta | `20` | `lblCampoBErrorFic.Y + lblCampoBErrorFic.Height + 12` | `Parent.Width - 40` | `16` |
| `lblCampoCActualFic` | Etiqueta | `20` | `lblCampoCLabelFic.Y + lblCampoCLabelFic.Height + 2` | `Parent.Width - 40` | `16` |
| `inpPanelCampoCFic` | Entrada de texto (SingleLine) | `20` | `lblCampoCActualFic.Y + lblCampoCActualFic.Height + 4` | `Parent.Width - 40` | `34` |
| `lblMotivoLabelPanelFic` | Etiqueta | `20` | `<Y del último campo visible> + <su Height> + 12` | `Parent.Width - 40` | `16` |
| `inpMotivoPanelFic` | Entrada de texto (MultiLine) | `20` | `lblMotivoLabelPanelFic.Y + lblMotivoLabelPanelFic.Height + 4` | `Parent.Width - 40` | `56` |
| `lblCambiosHeadFic` | Etiqueta | `20` | `inpMotivoPanelFic.Y + inpMotivoPanelFic.Height + 14` | `Parent.Width - 40` | `16` |
| `cntCambiosPanelFic` | Contenedor | `20` | `lblCambiosHeadFic.Y + lblCambiosHeadFic.Height + 6` | `Parent.Width - 40` | `lblCambiosListHeadFic.Height + CountRows(colCambiosPanelFic) * fxRowHChica + 10` |
| `lblCambiosListHeadFic` | Etiqueta (hijo de `cntCambiosPanelFic`) | `14` | `0` | `Parent.Width - 14` | `22` |
| `cntBannerPanelFic` | Contenedor (receta 7.11, tono `info`) | `20` | `cntCambiosPanelFic.Y + cntCambiosPanelFic.Height + 10` | `Parent.Width - 40` | `lblBannerPanelFic.Height + 14` |
| `lblBannerPanelFic` | Etiqueta | `10` | `7` | `Parent.Width - 20` | *(AutoHeight)* |
| `cntBannerPanelConflictoFic` | Contenedor (receta 7.11, tono `warn`) | `20` | `cntBannerPanelFic.Y + If(cntBannerPanelFic.Visible, cntBannerPanelFic.Height, 0) + 8` | `Parent.Width - 40` | `lblBannerPanelConflictoFic.Height + 14` |
| `lblBannerPanelConflictoFic` | Etiqueta | `10` | `7` | `Parent.Width - 20` | *(AutoHeight)* |

> `<Y del último campo visible>`: en modo `"cliente"` es `lblCampoCErrorFic`-equivalente (no
> existe fila de error para C; usar `inpPanelCampoCFic.Y + inpPanelCampoCFic.Height`); en
> modo `"sucursal"` es `lblCampoAErrorFic.Y + lblCampoAErrorFic.Height`; en `"correo"`/
> `"cuenta"` es `lblCampoBErrorFic.Y + lblCampoBErrorFic.Height`. Como Power Fx no tiene
> `If` de fórmula-de-posición sin costo, `lblMotivoLabelPanelFic.Y` se escribe exactamente
> así:
> ```powerfx
> =Switch(
>     varPanelModoFic,
>     "cliente", inpPanelCampoCFic.Y + inpPanelCampoCFic.Height + 12,
>     "sucursal", lblCampoAErrorFic.Y + lblCampoAErrorFic.Height + 12,
>     lblCampoBErrorFic.Y + lblCampoBErrorFic.Height + 12
> )
> ```

**Visible de cada campo, por modo** (conjunción completa, gotcha 8.6 — vale para el molde
de las 4 filas: label/actual/input/error):

| Control | `Visible` |
|---|---|
| `lblCampoALabelFic` / `lblCampoAActualFic` / `inpPanelCampoAFic` / `lblCampoAErrorFic` | `=true` (todos los modos usan el campo A) |
| `lblCampoBLabelFic` / `lblCampoBActualFic` / `lblCampoBErrorFic` | `=varPanelModoFic <> "sucursal"` |
| `inpPanelCampoBFic` | `=varPanelModoFic = "cliente"` |
| `drpPanelCampoBFic` | `=varPanelModoFic = "correo" || varPanelModoFic = "cuenta"` |
| `lblCampoCLabelFic` / `lblCampoCActualFic` / `inpPanelCampoCFic` | `=varPanelModoFic = "cliente"` |
| `lblCampoAActualFic` / `lblCampoBActualFic` / `lblCampoCActualFic` | además `=varPanelAccionFic = "Editar"` (en Alta no hay "valor actual") |

**Labels y bindings por modo** — tabla completa (`lblCampoALabelFic.Text`, etc.):

| `varPanelModoFic` | Campo A (label / Actual / Default input) | Campo B (label / Actual / Default) | Campo C (label / Actual / Default) |
|---|---|---|---|
| `"cliente"` | `"Nombre"` / `"Actual: " & gvarClienteSel.Name` / `gvarClienteSel.Name` | `"CIF BCO"` / `"Actual: " & gvarClienteSel.CIFBCO` / `gvarClienteSel.CIFBCO` | `"CIF COM"` / `"Actual: " & gvarClienteSel.CIFCOM` / `gvarClienteSel.CIFCOM` |
| `"sucursal"` | `"Nombre de la sucursal"` / `"Actual: " & LookUp('CH-Tbl-ClientBranches','CH-Tbl-ClientBranch'=varPanelTargetIdFic).Name` / igual | — | — |
| `"correo"` | `"Dirección de correo"` / `"Actual: " & LookUp('CH-Tbl-ClientEmails','CH-Tbl-ClientEmail'=varPanelTargetIdFic).'Email Address'` / igual | `"Notificar siempre"` / `"Actual: " & If(LookUp('CH-Tbl-ClientEmails','CH-Tbl-ClientEmail'=varPanelTargetIdFic).AlwaysNotify,"Sí","No")` / — (dropdown, ver abajo) | — |
| `"cuenta"` | `"Número de cuenta"` / `"Actual: •••• " & Right(LookUp('CH-Tbl-ClientAccounts','CH-Tbl-ClientAccount'=varPanelTargetIdFic).'Numero de cuenta',4)` / **NO precargar el número completo** en Alta ni en Actual — solo en el `Default` del input si `varPanelAccionFic="Editar"` (ver nota) | `"Moneda"` / `"Actual: " & Switch(LookUp(...).Moneda, ...)` / — (dropdown) | — |

`inpPanelCampoAFic.Default` (fórmula única, cubre los 4 modos + Alta con blanco):

```powerfx
=Switch(
    varPanelModoFic,
    "cliente", If(varPanelAccionFic = "Editar", gvarClienteSel.Name, ""),
    "sucursal", If(varPanelAccionFic = "Editar", LookUp('CH-Tbl-ClientBranches', 'CH-Tbl-ClientBranch' = varPanelTargetIdFic).Name, ""),
    "correo", If(varPanelAccionFic = "Editar", LookUp('CH-Tbl-ClientEmails', 'CH-Tbl-ClientEmail' = varPanelTargetIdFic).'Email Address', ""),
    "cuenta", If(varPanelAccionFic = "Editar", LookUp('CH-Tbl-ClientAccounts', 'CH-Tbl-ClientAccount' = varPanelTargetIdFic).'Numero de cuenta', ""),
    ""
)
```

> ⚠️ En modo `"cuenta"` + Alta/Editar, el campo A **muestra el número completo sin
> enmascarar** (es el propio usuario tipeándolo/corrigiéndolo — la máscara del §7 es solo
> para la GRILLA de solo lectura, no para el formulario donde el dato ya lo conoce quien
> firma la solicitud). `lblCampoAActualFic` sí queda enmascarado (ver tabla arriba) porque
> es una referencia visual, no el campo editable.

`drpPanelCampoBFic.Items` (por modo) — `Choices()` crudo NO alcanza: el `Value` de un menú
desplegable clásico espera texto, no el record de opción que devuelve `Choices()` directo, así
que cada rama envuelve con `ForAll(..., {Value: Text(ThisRecord.Value)})` (fuente real,
`scrCliente.fx.yaml`):

```powerfx
=If(varPanelModoFic = "correo",
    ForAll(Choices('AlwaysNotify (CH-Tbl-ClientEmails)'), {Value: Text(ThisRecord.Value)}),
    ForAll(Choices('CH-Ch-Currency'), {Value: Text(ThisRecord.Value)})
)
```

> 🧨 El option set de Moneda es el GLOBAL `'CH-Ch-Currency'`, NO `'Moneda (CH-Tbl-
> ClientAccounts)'` como aproximaba una versión anterior de esta guía — mismo patrón que
> `CH-Ch-RecordStatus` en `02-pantalla-clientes.md` (gotcha 8.7 de `01-app.md`): `CH-Tbl-
> ClientAccount` expone sus option sets de catálogo compartido con el prefijo global
> `CH-Ch-*`, no el nombre calificado por tabla. Verificado en `drpPanelCampoBFic`,
> `lblCampoBActualFic.Text` y el `ClearCollect` de `colCambiosPanelFic` (§10.3) del fuente
> real — los 3 usan `'CH-Ch-Currency'.NIO/USD/EUR`.

`drpPanelCampoBFic.Default`:

```powerfx
=If(
    varPanelAccionFic <> "Editar",
    Blank(),
    If(
        varPanelModoFic = "correo",
        If(LookUp('CH-Tbl-ClientEmails', 'CH-Tbl-ClientEmail' = varPanelTargetIdFic).AlwaysNotify, "Yes", "No"),
        Switch(
            LookUp('CH-Tbl-ClientAccounts', 'CH-Tbl-ClientAccount' = varPanelTargetIdFic).Moneda,
            'CH-Ch-Currency'.NIO, "NIO",
            'CH-Ch-Currency'.USD, "USD",
            'CH-Ch-Currency'.EUR, "EUR"
        )
    )
)
```

`inpPanelCampoBFic.Default = If(varPanelAccionFic = "Editar", gvarClienteSel.CIFBCO, "")`
(único uso: modo `"cliente"`).

`inpPanelCampoCFic.Default = If(varPanelAccionFic = "Editar", gvarClienteSel.CIFCOM, "")`
(único uso: modo `"cliente"`).

### 10.2 Validación no destructiva (corrige bug U5 de la v1)

Solo el campo A de modo `"correo"` valida formato de email; el resto valida "no vacío" en
Alta. Ningún `OnChange`/`OnSelect` de esta pantalla hace `Reset(Self)` sobre un input de
texto tipeado por el usuario — el error se marca, el texto tipeado queda.

`lblCampoAErrorFic.Text` / `.Visible`:

```powerfx
// Text
="El formato del correo no es válido (ej: usuario@dominio.com)."
// Visible
=varPanelModoFic = "correo"
    && !IsBlank(inpPanelCampoAFic.Text)
    && !IsMatch(inpPanelCampoAFic.Text, "^[^@\s]+@[^@\s]+\.[^@\s]+$")
```

`inpPanelCampoAFic.BorderColor = If(lblCampoAErrorFic.Visible, ThemeDanger, ThemeLine)`.

### 10.3 `colCambiosPanelFic` — diff en vivo (CI2 "Cambios que viajan en la solicitud")

> 🧨 Cada uno de los 4 controles lleva su `OnChange` con el bloque `ClearCollect(colCambiosPanelFic,
> ...)` COMPLETO — nunca una referencia cruzada tipo `OnChange: =inpPanelCampoAFic.OnChange`
> (`OnChange` es una propiedad de comportamiento; no se puede leer/reenviar como si fuera un
> valor, eso no compila). El fuente real (`scrCliente.fx.yaml`) confirma que los 4 controles
> (`inpPanelCampoAFic`, `inpPanelCampoBFic`, `drpPanelCampoBFic`, `inpPanelCampoCFic`) repiten
> el bloque completo abajo, IDÉNTICO en los 4 — no hay atajo de reutilización.

Se recalcula en el `OnChange` de **cada** input/dropdown del panel (`inpPanelCampoAFic`,
`inpPanelCampoBFic`, `drpPanelCampoBFic`, `inpPanelCampoCFic`) — la MISMA fórmula, pegada
igual en los 4:

```powerfx
=ClearCollect(
    colCambiosPanelFic,
    Filter(
        Switch(
            varPanelModoFic,
            "cliente", Table(
                {Campo: "Nombre", ValorViejo: If(varPanelAccionFic="Editar", gvarClienteSel.Name, ""), ValorNuevo: Trim(inpPanelCampoAFic.Text)},
                {Campo: "CIFBCO", ValorViejo: If(varPanelAccionFic="Editar", gvarClienteSel.CIFBCO, ""), ValorNuevo: Trim(inpPanelCampoBFic.Text)},
                {Campo: "CIFCOM", ValorViejo: If(varPanelAccionFic="Editar", gvarClienteSel.CIFCOM, ""), ValorNuevo: Trim(inpPanelCampoCFic.Text)}
            ),
            "sucursal", Table(
                {Campo: "Sucursal", ValorViejo: If(varPanelAccionFic="Editar", LookUp('CH-Tbl-ClientBranches','CH-Tbl-ClientBranch'=varPanelTargetIdFic).Name, ""), ValorNuevo: Trim(inpPanelCampoAFic.Text)}
            ),
            "correo", Table(
                {Campo: "Correo electronico", ValorViejo: If(varPanelAccionFic="Editar", LookUp('CH-Tbl-ClientEmails','CH-Tbl-ClientEmail'=varPanelTargetIdFic).'Email Address', ""), ValorNuevo: Trim(inpPanelCampoAFic.Text)},
                {Campo: "Notificar siempre", ValorViejo: If(varPanelAccionFic="Editar", If(LookUp('CH-Tbl-ClientEmails','CH-Tbl-ClientEmail'=varPanelTargetIdFic).AlwaysNotify,"Sí","No"), ""), ValorNuevo: If(drpPanelCampoBFic.Selected.Value = "Yes", "Sí", "No")}
            ),
            /* cuenta */ Table(
                {Campo: "Numero de cuenta", ValorViejo: If(varPanelAccionFic="Editar", LookUp('CH-Tbl-ClientAccounts','CH-Tbl-ClientAccount'=varPanelTargetIdFic).'Numero de cuenta', ""), ValorNuevo: Trim(inpPanelCampoAFic.Text)},
                {Campo: "Moneda", ValorViejo: If(varPanelAccionFic="Editar", Switch(LookUp('CH-Tbl-ClientAccounts','CH-Tbl-ClientAccount'=varPanelTargetIdFic).Moneda, 'CH-Ch-Currency'.NIO,"NIO",'CH-Ch-Currency'.USD,"USD",'CH-Ch-Currency'.EUR,"EUR"), ""), ValorNuevo: drpPanelCampoBFic.Selected.Value}
            )
        ),
        ValorViejo <> ValorNuevo && !IsBlank(ValorNuevo)
    )
)
```

> El `Campo` de cada fila es el literal EXACTO de `sanic_name` en `CH-Tbl-
> ClientTableLogicalFieldNames` (verificado contra `data/seed/10-clienttablelogicalfieldnames.json`)
> — se usa después para resolver `FieldDefinition` al armar la línea (§13.1). No es texto
> libre: si el seed cambia estos literales, esta fórmula deja de encontrar el catálogo y
> `btnPanelConfirmarFic.OnSelect` falla con "no se encontró la definición del campo".

`cntCambiosPanelFic` — chrome de tarjeta (R1-6, mockup CI2: `.card{border-radius:9px}` con
`.listHead` arriba de la lista, no un contenedor transparente) + su header:

| Prop | Fórmula |
|---|---|
| `cntCambiosPanelFic.Fill` | `ThemeSurface` |
| `cntCambiosPanelFic.BorderColor` | `ThemeLine` |
| `cntCambiosPanelFic.BorderThickness` | `1` |
| `cntCambiosPanelFic.Radius` (las 4) | `9` |
| `lblCambiosListHeadFic.Text` | `"CAMPO · ACTUAL → PROPUESTO"` |
| `lblCambiosListHeadFic` estilo | ESTILO-HEADER-COL · Fill `ThemeSurface2` |

`galCambiosPanelFic` (dentro de `cntCambiosPanelFic`, DEBAJO del nuevo header; receta 7.12
"diff actual → propuesto", `X: 0` · `Y: lblCambiosListHeadFic.Y + lblCambiosListHeadFic.Height`
· `Width: Parent.Width` · `Height: Parent.Height - Self.Y` · `TemplateSize: =fxRowHChica` ·
`Items: =colCambiosPanelFic`):

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblCambioCampoFic` | Etiqueta | `10` | `0` | `Parent.Width * 0.32` | `TemplateSize` | ESTILO-CELDA · Text `ThisItem.Campo` |
| `lblCambioViejoFic` | Etiqueta | `lblCambioCampoFic.X + lblCambioCampoFic.Width` | `0` | `Parent.Width * 0.3` | `TemplateSize` | Size `8.25` · Color `ThemeInk3` · `Strikethrough: true` · Text `ThisItem.ValorViejo` |
| `lblCambioFlechaFic` | Etiqueta | `lblCambioViejoFic.X + lblCambioViejoFic.Width` | `0` | `18` | `TemplateSize` | Size `8.25` · Color `ThemeInk3` · Text `"→"` · Align `Center` |
| `lblCambioNuevoFic` | Etiqueta | `lblCambioFlechaFic.X + lblCambioFlechaFic.Width` | `0` | `Parent.Width - Self.X - 10` | `TemplateSize` | Size `8.25` · Semibold · Color `ThemeOk` · Text `ThisItem.ValorNuevo` |

`lblCambiosHeadFic.Text = "Cambios que viajan en la solicitud (" & CountRows(colCambiosPanelFic) & ")"`.

**R1-3**: el mockup CI2 muestra el banner informativo visible con 1 cambio y SIN conflicto
("'Notificar siempre' no cambió — no genera línea..."), escenario que la condición excluyente
original (0 cambios ⊻ conflicto) dejaba oculto. Se separa en dos banners independientes:
`cntBannerPanelFic` (tono `info`, "qué campo(s) no cambiaron todavía") se muestra siempre que
aplique, haya o no conflicto; `cntBannerPanelConflictoFic` (tono `warn`, nuevo — ver árbol de
controles) es el aviso de conflicto real, condicionado solo a `colRfcEnCursoFic`, ya no
mezclado con el mensaje anterior.

`cntBannerPanelFic.Visible` / `lblBannerPanelFic.Text` (tono `info`, patrón CI2 — lista qué
campo(s) visibles del modo activo NO cambiaron; nace en Editar, no en Alta, porque en Alta no
hay "valor actual" con el que comparar):

```powerfx
// cntBannerPanelFic.Visible
=varPanelAccionFic = "Editar"
    && CountRows(colCambiosPanelFic) < Switch(varPanelModoFic, "cliente", 3, "sucursal", 1, 2)
```

```powerfx
// lblBannerPanelFic.Text
=With(
    {
        _camposModo: Switch(
            varPanelModoFic,
            "cliente", ["Nombre", "CIFBCO", "CIFCOM"],
            "sucursal", ["Sucursal"],
            "correo", ["Correo electronico", "Notificar siempre"],
            ["Numero de cuenta", "Moneda"]
        )
    },
    If(
        CountRows(colCambiosPanelFic) = 0,
        "Ningún campo cambió todavía — no hay nada para enviar.",
        With(
            {_sinCambiar: Filter(_camposModo, !(Value in colCambiosPanelFic.Campo))},
            Concat(_sinCambiar, """" & Value & """", ", ") &
                If(CountRows(_sinCambiar) = 1, " no cambió — no genera línea.", " no cambiaron — no generan línea.")
        )
    )
)
```

> El literal de cada tabla `_camposModo` es el mismo `Campo` que arma `colCambiosPanelFic` en
> el `ClearCollect` de arriba (mismos literales de `sanic_name` del catálogo, ver nota debajo
> de esa fórmula) — si el seed cambia esos literales, ambas fórmulas hay que actualizarlas
> juntas.

`cntBannerPanelConflictoFic.Visible` / `lblBannerPanelConflictoFic.Text` (tono `warn`, nuevo
por R1-3 — SOLO el aviso de conflicto real por registro, ya no mezclado con "0 cambios"):

```powerfx
// cntBannerPanelConflictoFic.Visible
=!IsBlank(
    LookUp(
        colRfcEnCursoFic,
        TargetTable = Switch(varPanelModoFic, "cliente","sanic_chtblclient", "sucursal","sanic_chtblclientbranch", "correo","sanic_chtblclientemail", "sanic_chtblclientaccount")
        && TargetRecordID = Text(varPanelTargetIdFic)
    )
)
```

```powerfx
// lblBannerPanelConflictoFic.Text
="Ya hay una solicitud en curso sobre este registro — el envío se bloqueará hasta que se resuelva."
```

> Nota heredada (sigue aplicando a ambas fórmulas de arriba): la comparación NO usa
> `varDlgTargetTableFic` (esa variable la setea el flujo de Desactivar/Activar, §5/§12, y para
> el panel estaría en blanco) — se usa el mismo `Switch(varPanelModoFic, ...)` inline explícito
> que ya usa `btnPanelConfirmarFic.DisplayMode`/`OnSelect` (§10.4), no una referencia cruzada a
> una variable de otro flujo.

### 10.4 `btnPanelConfirmarFic` — crear y enviar la solicitud (Draft → líneas → Submitted)

> 🧨 **Hallazgo crítico de backend (cambia el diseño frente al mockup)**: `CI2` muestra un
> único botón "Crear y enviar solicitud" como si fuera un paso atómico, pero
> `ChangeRequestLineGuard.Evaluate` (`src/backend/CashHub.Validation/Security/
> ChangeRequestLineGuard.cs:93`) **bloquea crear/editar/borrar líneas de un RFC que no esté
> en estado Draft**. Si la cabecera naciera directo en `Submitted` (como sugiere el mockup),
> el guard rechazaría la creación de cada línea con "Solo se pueden crear, editar o borrar
> líneas de un RFC que esté en estado Draft". La única secuencia que el backend acepta es:
> **(1) cabecera en Draft → (2) líneas mientras está Draft → (3) cabecera a Submitted**
> (`ChangeRequestApprovalGuard.Evaluate` sí permite la transición `Draft→Submitted` sin
> restricciones). El usuario ve un solo clic; la app hace 3 escrituras encadenadas.

`btnPanelConfirmarFic.DisplayMode` (R2-9: la versión expandida original tenía una subcláusula
muerta — `IsBlank(lblCampoAErrorFic.Text)` nunca es verdadera porque ese `Text` es un literal
fijo, nunca blanco — y `!lblCampoAErrorFic.Visible` duplicado; se deja directamente la forma
simplificada. R2-5: se agrega `!varPanelCargandoFic` para que el botón se deshabilite mientras
corren las 3 escrituras encadenadas de §10.4, anti doble-click — sin esto, un doble clic
durante el `ForAll` de líneas puede crear 2 cabeceras de RFC con líneas duplicadas):

```powerfx
=If(
    !varPanelCargandoFic
    && CountRows(colCambiosPanelFic) > 0
    && !lblCampoAErrorFic.Visible
    && !IsBlank(Trim(inpMotivoPanelFic.Text))
    && IsBlank(
        LookUp(
            colRfcEnCursoFic,
            TargetTable = Switch(varPanelModoFic, "cliente","sanic_chtblclient", "sucursal","sanic_chtblclientbranch", "correo","sanic_chtblclientemail", "sanic_chtblclientaccount")
            && TargetRecordID = Text(varPanelTargetIdFic)
        )
    ),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

`btnPanelConfirmarFic.OnSelect` (R2-5: arranca con `varPanelCargandoFic: true` y lo libera en
AMBAS ramas del `IfError` — mismo patrón anti doble-click que `varDlgCargandoFic` de §11 — para
que el `DisplayMode` de arriba corte el botón mientras corren las 3 escrituras encadenadas):

```powerfx
=UpdateContext({varPanelCargandoFic: true});
Set(varRfcErrorFic, "");
IfError(
    (
        // 1. Cabecera en Draft.
        Clear(colNuevoRfcFic);
        Collect(
            colNuevoRfcFic,
            Patch(
                'CH-Tbl-ClientDataChangeRequests',
                Defaults('CH-Tbl-ClientDataChangeRequests'),
                {
                    'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,
                    Type: If(varPanelAccionFic = "Alta", 'Type (CH-Tbl-ClientDataChangeRequests)'.Create, 'Type (CH-Tbl-ClientDataChangeRequests)'.Update),
                    TargetTable: Switch(varPanelModoFic, "cliente","sanic_chtblclient", "sucursal","sanic_chtblclientbranch", "correo","sanic_chtblclientemail", "sanic_chtblclientaccount"),
                    TargetTableDisplayName: Switch(varPanelModoFic, "cliente","Clientes", "sucursal","Sucursales", "correo","Correos", "Cuentas"),
                    TargetTablePluralName: Switch(varPanelModoFic, "cliente","sanic_chtblclients", "sucursal","sanic_chtblclientbranchs", "correo","sanic_chtblclientemails", "sanic_chtblclientaccounts"),
                    TargetRecordID: If(varPanelAccionFic = "Alta", Blank(), Text(varPanelTargetIdFic)),
                    RelatedClient: gvarClienteSel,
                    Reason: Trim(inpMotivoPanelFic.Text)
                }
            )
        );
        // 2. Una linea por cada fila de colCambiosPanelFic, con la cabecera TODAVIA en Draft.
        ForAll(
            colCambiosPanelFic As Cambio,
            Patch(
                'CH-Tbl-ClientDataChangeRequestLines',
                Defaults('CH-Tbl-ClientDataChangeRequestLines'),
                {
                    ChangeRequestID: Last(colNuevoRfcFic),
                    'Field Definition': LookUp(
                        'CH-Tbl-ClientTableLogicalFieldNames',
                        Table = Switch(varPanelModoFic, "cliente","sanic_chtblclient", "sucursal","sanic_chtblclientbranch", "correo","sanic_chtblclientemail", "sanic_chtblclientaccount")
                        && Name = Cambio.Campo
                    ),
                    OldValue: Cambio.ValorViejo,
                    NewValue: Cambio.ValorNuevo
                }
            )
        );
        // 3. Cabecera a Submitted (unica transicion humana que ChangeRequestApprovalGuard permite tras Draft).
        Patch(
            'CH-Tbl-ClientDataChangeRequests',
            Last(colNuevoRfcFic),
            {'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted}
        );
        // 4. Refresco y cierre.
        Refresh('CH-Tbl-ClientDataChangeRequests');
        Refresh('CH-Tbl-ClientDataChangeRequestLines');
        ClearCollect(
            colRfcEnCursoFic,
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
                && (
                    'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved
                )
            )
        );
        UpdateContext({varPanelAbiertoFic: false, varPanelCargandoFic: false});
        Notify("Solicitud creada y enviada a aprobación.", NotificationType.Success)
    ),
    UpdateContext({varPanelCargandoFic: false});
    Set(varRfcErrorFic, FirstError.Message);
    Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error)
)
```

> 🧨 Anti-genérico (gotcha del proyecto): el `Notify` de error **siempre** incluye
> `FirstError.Message` real — nunca un texto fijo tipo "Hubo problemas al guardar…" (bug U4
> de la v1). `colNuevoRfcFic` es una colección técnica de un solo elemento (no aparece en el
> árbol de controles porque no es un control — se declara/limpia junto a las demás
> colecciones locales de §13.2).
>
> Si el paso 3 (Patch a Submitted) falla después de que el paso 1/2 tuvieron éxito, el RFC
> queda en Draft — visible para el creador en la tab Solicitudes de este mismo cliente como
> "Borrador" (bug conocido L8 de la v1, mitigado no eliminado: la cabecera SÍ quedó creada de
> forma consistente con sus líneas, a diferencia de la v1 donde el Update parcial podía dejar
> campos sueltos sin línea. El usuario puede reintentar el envío desde `scrSolicitud`,
> guía 05, con el botón "Enviar a aprobación" de la vista SO5).

---

## 11. `cmpConfirmDialogFic` — Desactivar / Activar (CI3, sin líneas)

A diferencia del panel de edición, Desactivar/Activar **no** requiere el ciclo Draft→líneas→
Submitted: `ChangeRequestResolution.RequiresLines` (backend) solo exige líneas para
`Update`/`Create`; `Deactivate`/`Activate` se crean directo en `Submitted` en un único
`Patch` (`ChangeRequestApprovalGuard.Evaluate` permite nacer en `Submitted`, ver `Create`
event). Instancia única del componente compartido (receta 7.6 de `01-app.md`), reusada
para Cliente/Sucursal/Correo/Cuenta vía las variables `varDlgAccionFic`/`varDlgTargetTableFic`/
`varDlgTargetTableLabelFic`/`varDlgTargetTablePluralFic`/`varDlgTargetIdFic`/
`varDlgTargetNombreFic` seteadas por cada disparador (§2.2 a nivel Cliente, `pilSucEstadoFic`/
`pilCorEstadoFic`/`pilCtaEstadoFic` a nivel Sucursal/Correo/Cuenta).

```powerfx
cmpConfirmDialogFic As cmpConfirmDialog:
    Cargando: =varDlgCargandoFic
    EtiquetaCampo: ="Motivo de la solicitud"
    Mostrar: =!IsBlank(varDlgAccionFic)
    MostrarCampo: =true
    OkHabilitado: =!IsBlank(Trim(cmpConfirmDialogFic.CampoTexto))
    OnCancelar: =UpdateContext({varDlgAccionFic: "", varDlgCargandoFic: false})
    OnOk: <ver abajo>
    PlaceholderCampo: =Switch(
        varDlgTargetTableFic,
        "sanic_chtblclient", "Ej: cierre de operaciones del cliente…",
        "sanic_chtblclientbranch", "Ej: cierre de la plaza por mudanza del cliente…",
        "sanic_chtblclientemail", "Ej: la casilla dejó de usarse para este cliente…",
        "sanic_chtblclientaccount", "Ej: la cuenta fue cancelada por el banco…",
        "Ej: motivo de la solicitud…"
    )
    TextoCancelar: ="Cancelar"
    TextoOk: ="Crear y enviar solicitud"
    Titulo: =With(
        {
            _art: Switch(
                varDlgTargetTableFic,
                "sanic_chtblclient", "el cliente",
                "sanic_chtblclientbranch", "la sucursal",
                "sanic_chtblclientemail", "el correo",
                "sanic_chtblclientaccount", "la cuenta",
                "el registro"
            )
        },
        If(
            varDlgAccionFic = "Activar",
            "¿Solicitar la activación de " & _art & " " & varDlgTargetNombreFic & "?",
            "¿Solicitar la desactivación de " & _art & " " & varDlgTargetNombreFic & "?"
        )
    )
    Tono: =If(varDlgAccionFic = "Activar", "ok", "danger")
    Texto: =With(
        {
            _esFem: varDlgTargetTableFic in ["sanic_chtblclientbranch", "sanic_chtblclientaccount"],
            _sujeto: Switch(
                varDlgTargetTableFic,
                "sanic_chtblclient", "El cliente",
                "sanic_chtblclientbranch", "La sucursal",
                "sanic_chtblclientemail", "El correo",
                "sanic_chtblclientaccount", "La cuenta",
                "El registro"
            )
        },
        "Se creará una solicitud tipo **" & If(varDlgAccionFic = "Activar", "Activación", "Desactivación") &
            "** para " & gvarClienteSel.Name & " · " & varDlgTargetNombreFic & ". " & _sujeto & " sigue **" &
            If(varDlgAccionFic = "Desactivar", "Activ", "Inactiv") & If(_esFem, "a", "o") &
            "** hasta que un Approver de " & If(varDlgTargetTableFic = "sanic_chtblclientbranch", "Bóveda", "Banca Empresas") & " apruebe."
    )
    ValorCampo: =""
```

> **R1-1 (fidelidad vs mockup)**: `Titulo`/`Texto` reproducen literal el patrón de CI3
> ("¿Solicitar la desactivación de **la sucursal** Chinandega?" / "...tipo **Desactivación**...
> **La sucursal sigue Activa**...") — nombre singular con artículo de género en vez de la
> etiqueta plural (`varDlgTargetTableLabelFic`, que sigue existiendo y se usa tal cual para
> `TargetTableDisplayName` en el `Patch` de `OnOk`, porque ahí sí corresponde el plural — ver
> SO1/SO2/SO5), sustantivo "Activación"/"Desactivación" (coherente con el `Type` del RFC en
> toda la app) en vez del infinitivo, y estado actual real con concordancia de género en vez
> de "su estado actual" genérico. `varDlgAccionFic` (contrato de variables, arriba) **sigue**
> guardando el infinitivo `"Activar"`/`"Desactivar"` — es el que gobierna el `If` de `Type` en
> `OnOk` y el `Tono`; solo el TEXTO visible pasa por el `Switch`/`With` de arriba.
>
> **R1-9**: `PlaceholderCampo` no existe hoy en la Custom Property table de `cmpConfirmDialog`
> (receta 7.6 de `01-app.md`) — se agrega ahí (ver nota al pie de esta sección) porque sin esa
> prop el placeholder de motivo del mockup ("Ej: cierre de la plaza por mudanza del cliente…",
> CI3) no tiene forma de llegar al `inpCampoDlg` interno del componente.

`cmpConfirmDialogFic.OnOk`:

```powerfx
=UpdateContext({varDlgCargandoFic: true});
IfError(
    (
        Patch(
            'CH-Tbl-ClientDataChangeRequests',
            Defaults('CH-Tbl-ClientDataChangeRequests'),
            {
                'Change Status': 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted,
                Type: If(varDlgAccionFic = "Activar", 'Type (CH-Tbl-ClientDataChangeRequests)'.Activate, 'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate),
                TargetTable: varDlgTargetTableFic,
                TargetTableDisplayName: varDlgTargetTableLabelFic,
                TargetTablePluralName: varDlgTargetTablePluralFic,
                TargetRecordID: Text(varDlgTargetIdFic),
                RelatedClient: gvarClienteSel,
                Reason: Trim(cmpConfirmDialogFic.CampoTexto)
            }
        );
        Refresh('CH-Tbl-ClientDataChangeRequests');
        ClearCollect(
            colRfcEnCursoFic,
            Filter(
                'CH-Tbl-ClientDataChangeRequests',
                RelatedClient.'CH-Tbl-Client' = gvarClienteSel.'CH-Tbl-Client'
                && (
                    'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted
                    || 'Change Status' = 'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved
                )
            )
        );
        UpdateContext({varDlgAccionFic: "", varDlgCargandoFic: false});
        Notify("Solicitud creada y enviada a aprobación.", NotificationType.Success)
    ),
    UpdateContext({varDlgCargandoFic: false});
    Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error)
)
```

> `varDlgCargandoFic` (Booleano, contexto local, agregarlo a la tabla de variables del
> encabezado) alimenta `cmpConfirmDialogFic.Cargando` — deshabilita el diálogo entero y
> muestra el `Spinner` interno mientras corre el `Patch` (anti doble-click, corrige L7 de la
> v1: acá el estado SIEMPRE se libera, tanto en éxito como en el `IfError`).

---

## 12. Colecciones y contexto — resumen de referencia rápida

Ya declaradas en el encabezado de la guía; se listan de nuevo acá con el control/sección que
las produce, para que quien construya no tenga que buscar hacia arriba:

| Nombre | Tipo | Se llena en | Se usa en |
|---|---|---|---|
| `ctxTabFic` | ctx Texto | `OnVisible` (§1), botones de `cntTabsFic` (§3) | `Visible` de las 5 grillas, `HintText`/`Text` de la toolbar (§4) |
| `varPanelAbiertoFic` / `varPanelModoFic` / `varPanelAccionFic` / `varPanelTargetIdFic` | ctx | `btnEditarClienteFic`, `btnNuevoTabFic`, `btnFilaXFic.OnSelect` (§2.2, §4, §5–§7, R2-3) | `cntPanelFic` completo (§10) |
| `varDlgAccionFic` / `varDlgTargetTableFic` / `varDlgTargetTableLabelFic` / `varDlgTargetTablePluralFic` / `varDlgTargetIdFic` / `varDlgTargetNombreFic` | ctx | `btnDesactivarClienteFic` (§2.2), `pilSucEstadoFic`/`pilCorEstadoFic`/`pilCtaEstadoFic` (§5–§7) | `cmpConfirmDialogFic` (§11) |
| `varDlgCargandoFic` | ctx Booleano | `cmpConfirmDialogFic.OnOk` (§11) | `cmpConfirmDialogFic.Cargando` |
| `varPanelCargandoFic` | ctx Booleano | `btnPanelConfirmarFic.OnSelect` (§10.4, R2-5) | `btnPanelConfirmarFic.DisplayMode`/`.Text` |
| `colRfcEnCursoFic` | Colección | `OnVisible` (§1), fin de §10.4 y §11 | Banner sidebar (§2.2), píldoras "RFC en curso" (§5–§7), gates de Editar/Desactivar |
| `colBranchesClienteFic` / `colEmailsClienteFic` / `colAccountsClienteFic` / `colSolicitudesClienteFic` / `colAuditClienteFic` | Colección | `OnVisible` (§1, R2-6) | KPIs del sidebar (§2.1), contadores de `cntTabsFic` (§3) — nunca `CountRows(Filter(datasource,...))` directo |
| `colCuentasRevFic` | Colección | `OnVisible` (vacía), `btnCtaRevelarFic.OnSelect` (§7) | Máscara de `lblCtaNumeroFic` (§7) |
| `colCambiosPanelFic` | Colección | `OnChange` de los campos del panel (§10.3) | `galCambiosPanelFic`, gate de `btnPanelConfirmarFic` (§10.4) |
| `colNuevoRfcFic` | Colección técnica (1 fila) | Paso 1 de `btnPanelConfirmarFic.OnSelect` (§10.4) | Pasos 2 y 3 del mismo `OnSelect` |

---

## Notas

### Divergencias declaradas vs. mockup

| # | Divergencia | Por qué |
|---|---|---|
| D5 (heredada de `01-app.md`) | Grilla de Sucursales sin columna "Dirección" | `CH-Tbl-ClientBranch` no tiene esa columna — verificado en `Entity.xml` (§5) |
| D9 | `pilSucEstadoFic`/`pilCorEstadoFic`/`pilCtaEstadoFic` hacen doble función (muestran estado Y disparan Desactivar/Activar) | El mockup CI1 no dibuja una columna de acciones por fila ni muestra el disparador de CI3; se decidió reusar la píldora de estado en vez de agregar una 4ª columna que el mockup no contempla — **confirmar con el usuario** si prefiere una columna de acciones explícita antes de construir |
| D10 | `TargetTableDisplayName` de la cabecera del RFC usa las etiquetas en español del mockup ("Clientes"/"Sucursales"/"Correos"/"Cuentas", como se ve en SO1/SO2/SO5) y NO los valores `sanic_tabledisplayname` del catálogo `CH-Tbl-ClientTableLogicalFieldNames` (que trae `"CH-Tbl-ClientBranch"` etc., pensados para identificar la entidad, no para mostrar al usuario) | El campo es texto libre en la cabecera del RFC, sin relación estructural con el catálogo — se prioriza lo que el mockup efectivamente muestra en pantalla |
| D11 | El botón "+ Nueva sucursal/correo/cuenta" del toolbar (§4) sigue el MISMO flujo Draft→líneas→Submitted que Editar (§10.4), con `ValorViejo` en blanco para cada campo tipeado | El mockup no detalla el flujo de Alta a este nivel (solo lo muestra deshabilitado en CI1); se generalizó el mismo panel/diff en vez de construir un flujo aparte — ver hallazgo crítico de guard de líneas abajo, que aplica igual a Alta (`Create` también requiere líneas) |
| D12 (hallazgo R1-4) | El contador de cada tab (§3, `"<Tab> (" & <Contador> & ")"`) es texto plano dentro de la misma Etiqueta/Botón, no la píldora de color independiente (`.cnt`) que dibuja el mockup | Reproducir el badge de color exige un control adicional por tab (5 Etiquetas hijas) que esta guía no agrega al árbol — mismo criterio que el subrayado de la tab activa (§3): aproximación declarada, mejora opcional si se quiere el trazo literal |
| D13 (consistencia cruzada con 04 S11) | `pilSolEstadoFic` (§8) usa `BorderThickness: 0` en TODOS los tonos, incluido el neutro (Borrador/Cancelada), en vez del default `If(<tono>="neutro", 1, 0)` de la receta 7.3 de `01-app.md` | Extiende a esta guía la divergencia S11 de `04-pantalla-solicitudes.md` (mismo pill de ESTADO del RFC, mismo dato): el CSS del mockup (`.pill` y sus tonos, incluido `.pMuted`) no define `border` en NINGUNA variante — verificado en el `<style>` completo del mockup, mismo razonamiento que ya aplica `pilEstadoFilaCli` en `02-pantalla-clientes.md` §5.4 para el pill de `RecordStatus`. Sin esta corrección el mismo RFC en Borrador se vería CON borde acá y SIN borde en la bandeja de `04` — inconsistencia visual del mismo dato entre pantallas |

### Hallazgos críticos (no declarados en `01-app.md`, encontrados al cruzar seeds + backend)

1. **El guard de líneas exige Draft, no permite crear líneas contra un RFC ya Submitted**
   (`ChangeRequestLineGuard.cs:93`, ver §10.4). Esto **cambia el diseño del flujo de creación**
   respecto de lo que el mockup CI2/CI3 sugiere con un solo botón: la app debe hacer 3
   escrituras encadenadas (Draft → líneas → Submitted) para cualquier RFC con líneas
   (`Update`/`Create`); los RFC sin líneas (`Deactivate`/`Activate`) sí pueden nacer
   directamente en `Submitted` en un solo `Patch`. **Esto aplica también a las guías 02/04/05**
   si reconstruyen flujos de creación de RFC con líneas — no es exclusivo de `scrCliente`.

2. **El catálogo `CH-Tbl-ClientTableLogicalFieldNames` mapea "Correo electronico" a la
   columna equivocada.** `data/seed/10-clienttablelogicalfieldnames.json` fija
   `sanic_logicalname: "sanic_name"` para el campo "Correo electronico" de
   `sanic_chtblclientemail" — pero `sanic_Name` en `CH-Tbl-ClientEmail` es un **autonumérico
   de solo lectura** (`AutoNumberFormat: "CE-{SEQNUM:1}"`, `DisplayMask` incluye
   `ReadOnlyInUI`), no la dirección de correo. La columna real y editable es **`emailaddress`**
   (display `'Email Address'`). Si `ResolveChangeRequestPlugin` llega a aprobar un RFC de
   edición de correo con este catálogo sin corregir, escribiría (o fallaría al escribir) sobre
   el campo equivocado — el `entity[logicalName] = ...` de `ApplyLineValues` usaría
   `sanic_name`, no `emailaddress`. **Bloqueante para que CI2 funcione de punta a punta**:
   hay que corregir el seed (`sanic_logicalname: "emailaddress"` en esa fila) y re-sembrar
   antes de que el flujo de "Editar correo" quede operativo en producción. Esta guía ya
   construye toda la UI/lectura contra `'Email Address'` (la columna correcta); lo que queda
   pendiente es el dato del catálogo que resuelve la escritura server-side.

3. **`sanic_targettable`/`sanic_name` de `CH-Tbl-ClientDataChangePermissions` usan el
   LOGICAL NAME de la entidad** (`sanic_chtblclient`, `sanic_chtblclientbranch`,
   `sanic_chtblclientemail`, `sanic_chtblclientaccount`), NO los literales `"Client"`/
   `"ClientBranch"`/`"ClientEmail"`/`"ClientAccount"` — confirmado en `data/seed/
   20-clientdatachangepermissions.json`, `data/seed/10-clienttablelogicalfieldnames.json`
   (columna `sanic_table`) y `src/backend/CashHub.Plugins/Schema.cs`
   (`ChangePermission.TargetTable = "sanic_name"`, usado luego como `new Entity(targetTable,
   ...)` — un literal Dataverse real, no una etiqueta). Esto **resuelve la divergencia D6**
   que `01-app.md` dejaba como "a verificar en Studio". Esta guía usa los literales
   verificados en todas sus fórmulas (§2.2, §5–§7, §10.4, §11).
   **Consecuencia sobre `01-app.md` — CORREGIDA** (revisión de consistencia cruzada 01↔05):
   `App.Formulas` → `fxAreaPorTabla` (§3 de `01-app.md`) definía `Tabla: "Client"` /
   `"ClientBranch"` / `"ClientEmail"` / `"ClientAccount"`, y `App.OnStart` (§4) hace
   `LookUp(fxAreaPorTabla, Tabla = First(colMisPermisos).Name).Area` — como
   `colMisPermisos.Name` en realidad trae `"sanic_chtblclient"` etc. (el literal real
   sembrado), ese `LookUp` no encontraba fila y `gvarRolTexto` quedaba con el área en blanco
   (`" · "` sin área) para TODOS los usuarios; el mismo problema afectaba a `04` (`ctxAprueba*`,
   `drpTablaSol`, `lblFilaTablaSol`) y a `05` (`colTargetRecordDet`, `lblKRegistroDet`/
   `lblVRegistroDet`, los Switch acotados de "valor actual" de §6.2/§10.2). `fxAreaPorTabla`
   de `01-app.md` §3 y los literales de `04-pantalla-solicitudes.md`/
   `05-pantalla-solicitud.md` ya usan los 4 valores verificados acá.

### Dependencias de backend con fallback

- La Custom API `sanic_CHResolveChangeRequest` (`ResolveChangeRequestPlugin.cs`) **ya está
  escrita** en el repo, pero no aparece registrada en el solution desempaquetado
  (`solutions/unpacked/core` no tiene ningún componente `CHResolveChangeRequest`) — no está
  desplegada todavía. **No afecta a esta pantalla**: `scrCliente` nunca llama esa Custom API,
  solo crea RFC en `Draft`/`Submitted` vía `Patch` directo (permitido para el creador, ver
  `ChangeRequestApprovalGuard`). Sí afecta a `scrSolicitud` (guía 05), donde si la Custom API
  no está desplegada, `Environment.sanic_CHResolveChangeRequest` no existirá como función en
  Power Fx y los botones Aprobar/Rechazar deben quedar en `DisplayMode.Disabled` con el
  banner "backend pendiente" (ya documentado en `01-app.md` §7.6).
- El guard de líneas (`GuardChangeRequestLinePlugin`) y el de cabecera
  (`GuardChangeRequestApprovalPlugin`) **si están registrados** (no hay evidencia de lo
  contrario en el repo) bloquearán en el servidor cualquier secuencia de escritura que no
  respete Draft→líneas→Submitted — **a verificar en Studio/entorno** antes de dar por buena
  esta guía: probar la secuencia completa de §10.4 contra el entorno real y confirmar que no
  tira `InvalidPluginExecutionException`.

### Aproximaciones honestas / a verificar en Studio

- `Len(texto) * 5.x` en anchos de píldoras/labels dinámicos (`pilEstadoClienteFic`,
  `lblGestionaTabFic`, `btnTabXFic`, `btnNuevoTabFic`) es una aproximación de kerning de
  Open Sans, no una medición exacta — **verificar en Studio** con nombres largos reales
  (clientes con razón social larga, usuarios con nombre largo) y ajustar el multiplicador si
  el texto se corta.
- El buscador de la tab Cuentas (`inpBuscarTabFic` cuando `ctxTabFic = "Cuentas"`) queda
  visualmente presente pero sin filtro activo (§7) — filtrar por número de cuenta
  desenmascarado expondría el dato completo en el texto tipeado sin ninguna protección
  adicional. Si el negocio pide poder buscar cuentas, la vía seria filtrar por los últimos
  4 dígitos (`EndsWith`) — no implementado acá porque no está en el mockup ni en el pedido
  original; queda como mejora futura, no como bug.
- `TargetTablePluralName` (`sanic_chtblclients`/`...branchs`/`...emails`/`...accounts`) se
  fija con los mismos literales que trae `data/seed/10-clienttablelogicalfieldnames.json`
  (`sanic_tablepluralname`) porque no hay documentación de quién consume esta columna en v2
  — **a verificar en Studio/backend** si algún consumidor espera un valor distinto.
- El subrayado de 2.5px de la tab activa (§3) se aproxima con color/peso de fuente en vez de
  un borde por lado — ver nota de §3 para la variante con `shp…SubFic` si se quiere el
  trazo literal.
- `pilSucEstadoFic`/`pilCorEstadoFic`/`pilCtaEstadoFic` como disparador de Desactivar/Activar
  (D9) es una decisión de diseño de esta guía, no algo dibujado en el mockup — confirmar con
  el usuario antes de construir; la alternativa (columna de acciones dedicada) es un cambio
  de ancho de columnas menor si se prefiere.

---

## Correcciones post-build 2026-08-25

Durante el debugging en Studio se verificaron dos fórmulas del panel lateral (§10) contra el
fuente REAL (`scrCliente.fx.yaml`). El fuente mandó:

| # | Qué estaba mal | Por qué (fuente real) |
|---|---|---|
| PB1 | Se sospechaba un `OnChange: =inpPanelCampoAFic.OnChange` (referencia a una propiedad de comportamiento, inválido — `OnChange` no se puede leer/reenviar como valor) en los 4 campos del panel | Verificado contra el fuente: NO existe tal referencia. Los 4 controles (`inpPanelCampoAFic`, `inpPanelCampoBFic`, `drpPanelCampoBFic`, `inpPanelCampoCFic`) ya llevan el bloque `ClearCollect(colCambiosPanelFic, ...)` COMPLETO e IDÉNTICO en su propio `OnChange` (§10.3) — esta guía ya documentaba el patrón correcto. Se agregó igual una nota explícita (🧨 en §10.3) para que quien construya no intente "ahorrar" con una referencia cruzada |
| PB2 | `drpPanelCampoBFic.Items` (§10.1) usaba `Choices('Moneda (CH-Tbl-ClientAccounts)')` crudo | Un menú desplegable clásico necesita `{Value: Text(...)}` por fila, no el record que devuelve `Choices()` directo — el fuente real envuelve con `ForAll(Choices(...), {Value: Text(ThisRecord.Value)})` en las 2 ramas (correo/cuenta) |
| PB3 | El enum de Moneda se citaba como `'Moneda (CH-Tbl-ClientAccounts)'` en `drpPanelCampoBFic.Items`, `.Default`, el `ClearCollect` de `colCambiosPanelFic` (§10.3) y `lblCtaMonedaFic.Text` (§7, grilla de Cuentas) | El fuente real usa el option set GLOBAL `'CH-Ch-Currency'` (mismo patrón que `CH-Ch-RecordStatus`, gotcha 8.7 de `01-app.md`) — corregido en las 4 fórmulas citadas |

---

## Checklist de salida

- [ ] Árbol completo insertado y renombrado (150 nodos: 148 controles clásicos/contenedores/
      galerías + 2 componentes) antes de tocar ninguna fórmula — construcción en dos pasadas,
      como indica `01-app.md` §"Orden de construcción".
- [ ] `OnVisible` de la pantalla pegado completo (§1): guarda contra `gvarClienteSel` blanco,
      resetea `ctxTabFic`/panel/diálogo, carga `colRfcEnCursoFic`, vacía `colCuentasRevFic`.
- [ ] Sidebar (§2): tarjeta de datos maestros de solo lectura + tarjeta de acciones con
      `DisplayMode` gateado por Contributor + `RecordStatus` + `colRfcEnCursoFic`.
- [ ] 5 tabs funcionando con la conjunción `ctxTabFic = "<Tab>"` en las 5 grillas, sin
      excepciones (gotcha 8.6).
- [ ] Grilla de Sucursales sin columna "Dirección" (D5), 3 columnas reales.
- [ ] Grilla de Correos muestra `'Email Address'`, NUNCA `sanic_name` (hallazgo crítico #2).
- [ ] Grilla de Cuentas: números enmascarados por defecto, revelado por fila vía
      `colCuentasRevFic`, estado leído de `'CH-Ch-RecordStatus'` (gotcha 8.7, no
      `RecordStatus`).
- [ ] Grilla de Solicitudes navega a `scrSolicitud` seteando `gvarSolicitudSel` **y**
      `gvarClienteSel` (CONTRATO DE SALIDA de esta pantalla — `btnFilaSolicitudFic.OnSelect`,
      §8).
- [ ] Grilla de Bitácora usa el option set con el typo real `OpeationType` (sin "r") tal
      cual está en el esquema — no "corregido".
- [ ] Panel lateral (§10): un solo panel para los 4 modos × 2 acciones, diff en vivo contra
      `colCambiosPanelFic`, validación de email no destructiva (sin `Reset(Self)`).
- [ ] `btnPanelConfirmarFic.OnSelect` hace las 3 escrituras encadenadas Draft→líneas→
      Submitted (§10.4) — probado contra el guard de líneas real en el entorno antes de dar
      por cerrada la pantalla.
- [ ] Diálogo de Desactivar/Activar (§11) crea el RFC directo en `Submitted`, sin líneas.
- [ ] Todos los `Notify` de error usan `FirstError.Message` real (cero mensajes genéricos).
- [ ] App Checker en cero errores, incluida delegación (ningún warning amarillo en los
      `Filter`/`Items` de esta guía).
- [ ] Hallazgo crítico #2 (catálogo de "Correo electronico") escalado al equipo de datos
      antes de habilitar CI2 en producción.
- [ ] Hallazgo crítico #3 (`fxAreaPorTabla` de `01-app.md`) escalado para corrección en la
      guía 01, ya que afecta el chip de rol en TODAS las pantallas de la app.
