# Guía 03 — scrAprobaciones (Aprobaciones)

Bandeja de solicitudes que requieren la firma del supervisor, con control de
cuatro ojos y detalle **por componente** antes de firmar. Reemplaza a
`Screen Approvals` (la del `Search(AddColumns(...))` no delegable, la del
`GUID(ParseJSON(...).BagID)`, la del `Abs()` sobre el neto que se comía las
discrepancias y la del `Rechazar` incondicional que dejaba tulas atrapadas).

Acá: bandeja **delegable**, detalle armado desde el contrato JSON + lookups
frescos, **asientos separados Sobrante/Faltante por componente**, revalidación
de estado antes de ejecutar y **la solicitud se resuelve ANTES que los efectos**.

**CONTRATO DE ENTRADA**: **ninguno**. La pantalla es autosuficiente — no lee
`gvar` ni colecciones de otras pantallas. `scrResumen` navega acá con
`Navigate(scrAprobaciones)` simple (KPI "CON DIFERENCIA" y filas de sesión), sin
pasar registro. Lo único global que consume es `UsuarioActual` y la paleta/
formatos de `App.Formulas` (01 §3).

**Variables/colecciones propias**: `ctxSegApr`, `ctxCargandoApr`, `ctxConfirmApr`,
`ctxUltimaActApr`, `ctxResApr`, `ctxBagGuidApr`, `ctxBagApr`, `ctxSesGuidApr`,
`ctxSesionApr`, `ctxCargadoIdApr`, `ctxFrescaApr`, `ctxResueltaApr`, `colRecApr`, `colDecApr`,
`colParApr`, `colDifApr`, `colTxApr`, `colChkApr`.

---

## Árbol de controles

```
scrAprobaciones
├─ cmpNavApr                          (instancia de cmpNav, 01 §7.5)
├─ cntBodyApr                         (Contenedor vertical, auto-layout)
│  ├─ cntTituloApr                    (Contenedor vertical)
│  │  ├─ lblTituloApr
│  │  └─ lblSubtituloApr
│  ├─ cardFiltrosApr                  (Contenedor manual, receta 01 §7.1)
│  │  ├─ segPendApr                   (segmentado, receta 01 §7.7)
│  │  ├─ segHistApr                   (segmentado, receta 01 §7.7)
│  │  ├─ lblBuscarApr
│  │  ├─ inpBuscarApr
│  │  ├─ icoBuscarApr
│  │  ├─ cirSyncApr
│  │  ├─ lblSyncApr
│  │  └─ btnActualizarApr
│  └─ cntMasterDetalleApr             (Contenedor horizontal, LayoutWrap true)
│     ├─ cardBandejaApr               (Contenedor manual — FillPortions 5)
│     │  ├─ lblBandejaTituloApr
│     │  ├─ pilBandejaCountApr        (Botón clásico — píldora, receta 01 §7.2)
│     │  ├─ shpLineaHeadApr
│     │  ├─ galBandejaApr             (Galería vertical en blanco)
│     │  │  ├─ btnFilaApr             (overlay de clic — al FONDO del template)
│     │  │  ├─ lblFilaTipoApr
│     │  │  ├─ lblFilaMetaApr
│     │  │  ├─ pilFilaEstadoApr       (Botón clásico — píldora, receta 01 §7.2)
│     │  │  ├─ lblFilaCuandoApr
│     │  │  └─ shpSepFilaApr
│     │  ├─ icoVacioBandejaApr
│     │  ├─ lblVacioBandejaTitApr
│     │  └─ lblVacioBandejaTxtApr
│     └─ cardDetalleApr               (Contenedor manual — FillPortions 4)
│        ├─ cntDetStackApr            (Contenedor vertical, auto-layout)
│        │  ├─ cntDetCabeceraApr      (Contenedor manual)
│        │  │  ├─ lblDetTituloApr
│        │  │  └─ lblDetSubApr
│        │  ├─ cntDetStaleApr         (Contenedor manual — aviso "detalle desfasado")
│        │  │  ├─ lblDetStaleApr
│        │  │  └─ btnDetStaleApr
│        │  ├─ cntKvApr               (Contenedor manual)
│        │  │  ├─ lblKvClienteEtqApr
│        │  │  ├─ lblKvClienteApr
│        │  │  ├─ lblKvCuentaEtqApr
│        │  │  ├─ lblKvCuentaApr
│        │  │  ├─ lblKvTulaEtqApr
│        │  │  └─ lblKvTulaApr
│        │  ├─ lblDetResumenApr
│        │  ├─ cntDifApr              (Contenedor manual — caja de diferencias)
│        │  │  ├─ shpBandDifApr       (banda gris del encabezado — al FONDO)
│        │  │  ├─ lblColCompApr
│        │  │  ├─ lblColDecApr
│        │  │  ├─ lblColRecApr
│        │  │  ├─ lblColDifApr
│        │  │  ├─ lblColAsientoApr
│        │  │  ├─ shpLineaDifApr
│        │  │  └─ galDifApr           (Galería vertical en blanco)
│        │  │     ├─ lblCelCompApr
│        │  │     ├─ lblCelDecApr
│        │  │     ├─ lblCelRecApr
│        │  │     ├─ lblCelDifApr
│        │  │     ├─ pilCelAsientoApr (Botón clásico — píldora, receta 01 §7.2)
│        │  │     └─ shpSepDifApr
│        │  ├─ cntAvisoAsientosApr    (Contenedor manual — aviso ámbar)
│        │  │  ├─ icoAvisoApr
│        │  │  └─ lblAvisoAsientosApr
│        │  ├─ cntAccionesApr         (Contenedor horizontal)
│        │  │  ├─ btnAprobarApr
│        │  │  └─ btnRechazarApr
│        │  └─ lblCuatroOjosApr
│        ├─ icoDetVacioApr
│        ├─ lblDetVacioApr
│        └─ btnRecargarDetalleApr     (SIEMPRE visible — motor de carga, §6)
├─ cntConfirmApr                      (Contenedor manual — overlay de confirmación)
│  └─ cardConfirmApr                  (Contenedor manual, receta 01 §7.1)
│     ├─ lblConfirmTituloApr
│     ├─ lblConfirmTextoApr
│     ├─ lblMotivoEtqApr
│     ├─ inpMotivoApr
│     ├─ btnCancelarConfirmApr
│     ├─ btnConfirmarAprobarApr
│     └─ btnConfirmarRechazarApr
└─ cntCargandoApr                     (Contenedor manual — overlay, receta 01 §7.6)
   └─ lblCargandoApr
```

> **Convención del árbol: orden back → front.** El primer hijo de cada nivel es
> el que queda **al fondo** del z-order y el último el que queda arriba. El árbol
> de controles de Studio lista al revés (el frontal arriba): al construir,
> insertá en este orden y verificá con *Reordenar → Traer al frente / Enviar al
> fondo*. Por eso `btnFilaApr` abre el template (overlay al fondo, receta 01 §7.3)
> y `shpBandDifApr` abre `cntDifApr`, mientras `cntConfirmApr` y
> `cntCargandoApr` cierran la pantalla.

---

## 0. Contrato de datos: `RequestResources` (verificado contra los productores)

`sanic_RequestType` es **texto libre** (`nvarchar`, display `RequestType`) —
NO es un optionset: por eso el `Switch` compara contra literales y por eso
`StartsWith(RequestType, …)` es delegable. `sanic_RequestResources` es un JSON
plano en texto. **Las claves NO son homogéneas** — esto es la raíz del bug de la
app vieja:

| RequestType (literal exacto) | Productor | Claves del JSON |
|---|---|---|
| `Remapear tula` | Reception `AdministracionDeLotes` | `GUID` (GUID de tula), `ReceiptID`, `ReceiptGUID`, `BagPreviousState` |
| `Agregar tula` | Reception | `ReceiptGUID`, `BagID` (**número** de tula, texto), `BagGUID`, `ReceptionBatchGUID` |
| `Eliminar tula` | Reception | `GUID` (GUID de tula), `BagPreviousState` |
| `Modificar numero de planilla` | Reception | `ReceiptGUID`, `NewReceiptID` |
| `Editar tula` | Reception | `ReceiptGUID`, `BagID` (**número** nuevo), `ReceptionBatchGUID`, `BagGUID` |
| `Crear sesion de recuento` | Recounting `Screen1` (**dos** productores) | `RecountingSessionGUID` |
| `Primera tula en planilla` | Recounting | `BagGUID`, `RecountingSessionGUID`, `DeclarationBagGUID` |
| `Tula sin declaracion` | Recounting | `BagGUID`, `RecountingSessionGUID` |
| `Tula con diferencia` | Recounting | `BagGUID` |
| `Tula recontada sin declaracion` | Recounting | `BagGUID` |

Reglas que salen de esa tabla y que la guía aplica sin excepción:

1. **`BagID` NUNCA es un GUID** — es el número de tula. La vieja hacía
   `GUID(ParseJSON(...).BagID)` en `Editar tula`: error garantizado. El GUID de
   tula está en `BagGUID` (o en `GUID` para *Remapear*/*Eliminar*).
2. Por eso el motor de detalle resuelve el GUID con
   `Coalesce(<BagGUID>, <GUID>)`, ambos envueltos en `IfError(...; Blank())`.
3. **`BagPreviousState` viene serializado por LABEL** (texto: `"Recibida en
   esclusa"`, …), no por valor. Se consume con guarda: si el `LookUp` sobre
   `Choices` da `Blank()`, **NO se patchea** `BagStatus` (la vieja escribía
   `BagStatus: Blank()` y borraba el estado de la tula).
4. `ParseJSON` de una clave ausente devuelve `Blank()`; `GUID("")` es error →
   siempre `IfError(GUID(Text(ParseJSON(x).clave)), Blank())`.
5. **`Crear sesion de recuento` tiene DOS productores** con el mismo JSON
   (`Screen1.fx.yaml` L10195 y L10214): si el cajero elige SU propia mesa, la
   solicitud **nace auto-aprobada** (`ApprovalStatus: Aprobado`) y **sin**
   `SolvedBy` ni `ResolutionDatetime`. Nunca llega a esta bandeja como pendiente,
   pero **sí aparece en el Histórico** — por eso `lblDetSubApr` (§7) guarda cada
   tramo de la resolución por separado. *(Que un cajero pueda auto-aprobarse una
   sesión por diseño es un hallazgo de gobierno, no de UI: se cierra con B-4 +
   los ajustes de rol de 00 §3.1.)*

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| LoadingSpinnerColor | `ThemeAccent` |
| OnVisible | ver abajo — COMPLETO, listo para pegar |

```powerfx
UpdateContext(
    {
        ctxSegApr: "pendientes",
        ctxConfirmApr: "",
        ctxCargandoApr: false,
        ctxResueltaApr: false,
        ctxCargadoIdApr: GUID("00000000-0000-0000-0000-000000000000")
    }
);
Reset(inpBuscarApr);
Reset(inpMotivoApr);
Reset(galBandejaApr);
Select(btnActualizarApr)
```

> **Por qué así**: el estado del segmentado y de la galería PERSISTE entre
> visitas (gotcha de canvas) → se resetea al entrar. `ctxConfirmApr` es texto
> (`""` = cerrado), nunca `Blank()`: inicializar un contexto con `Blank()` deja
> el tipo sin inferir. `ctxCargadoIdApr` se siembra con el **GUID cero tipado**
> por el mismo motivo (nunca `Blank()` pelado). Toda la carga vive en UN solo
> lugar (`btnActualizarApr`), igual que en la guía 02.

---

## 2. Layout responsive (01 §8)

| Control | Tipo | Propiedades |
|---|---|---|
| `cmpNavApr` | Componente de la app (`cmpNav`) | X `0` · Y `0` · Width `214` · Height `Parent.Height` |
| `cntBodyApr` | Contenedor vertical | X `cmpNavApr.Width` · Y `0` · Width `Parent.Width - cmpNavApr.Width` · Height `Parent.Height` · PaddingLeft/Right `26` · PaddingTop `22` · PaddingBottom `18` · LayoutGap `14` · Fill `Transparent` · **CON scroll** (`LayoutOverflowY: Desplazar`) |

Hijos directos de `cntBodyApr` (auto-layout: NO llevan X/Y; el orden del árbol es
el orden vertical): `cntTituloApr`, `cardFiltrosApr`, `cntMasterDetalleApr`.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntMasterDetalleApr` | Contenedor horizontal | **FillPortions `0`** · **Height** `If(scrAprobaciones.Width >= 1212, cntBodyApr.Height - 194, 1240)` · `LayoutMinHeight` = misma expresión · LayoutGap `16` · **LayoutWrap `true`** · Fill `Transparent` |
| `cardBandejaApr` | Contenedor manual (receta 01 §7.1) | **FillPortions `5`** · **LayoutMinWidth `380`** · **Height** `If(scrAprobaciones.Width >= 1212, cntMasterDetalleApr.Height, 610)` · hijos con X/Y |
| `cardDetalleApr` | Contenedor manual (receta 01 §7.1) | **FillPortions `4`** · **LayoutMinWidth `550`** · **Height** `If(scrAprobaciones.Width >= 1212, cntMasterDetalleApr.Height, 610)` · hijos con X/Y |

> **Divergencia de patrón vs 02/04 — deliberada.** Aquellas pantallas van **sin
> scroll** y reparten el alto con `FillPortions` entre los hijos del body; acá es
> al revés: `cntBodyApr` **scrollea** y **TODOS sus hijos llevan `FillPortions 0`**
> (`cntTituloApr`, `cardFiltrosApr`, `cntMasterDetalleApr`), igual que los 8 hijos
> de `cntDetStackApr` (§7-§10). Motivo: `cntMasterDetalleApr` es el único
> contenedor de las 4 pantallas que **envuelve**, y un contenedor que envuelve
> necesita alto propio — con `FillPortions` heredado quedaría con el alto de una
> sola fila y recortaría el detalle. Scroll + `FillPortions` se contradicen, así
> que acá se elige scroll y se apaga `FillPortions` en TODA la cadena. **Ningún
> hijo de un auto-layout de esta pantalla puede quedar con el `FillPortions 1` por
> defecto** — salvo `btnAprobarApr`/`btnRechazarApr`, que reparten ANCHO (no alto)
> dentro de `cntAccionesApr`.

**El breakpoint 1212 no es arbitrario** — se deriva: ancho disponible para
`cntMasterDetalleApr` = `scrAprobaciones.Width − 214 (nav) − 52 (padding L+R)`.
El wrap se dispara cuando ese ancho `< 380 + 16 + 550 = 946`, o sea cuando
`scrAprobaciones.Width < 1212`.

> **El `LayoutMinWidth 550` del detalle sale de la aritmética de §8, no del ojo**
> (01 §8.6 decía `420`; se corrigió allá también). Con 420 la columna DIFERENCIA
> quedaba en ~59 px y no entraba un `"+200,000.00 / −200,000.00"` (~144 px), que
> es justo el caso que el checklist §16 exige verificar: el supervisor se quedaba
> sin ver el monto que está por firmar. 550 es el ancho mínimo en el que las 5
> columnas rinden su contenido — ver el cálculo completo en §8.

> **Un contenedor con `LayoutWrap` NO crece solo**: si el detalle envuelve a una
> segunda fila, el contenedor sigue con el alto de una sola y el detalle queda
> recortado. Por eso `cntMasterDetalleApr` lleva `FillPortions 0` + **Height
> condicional al breakpoint** (`1240` = 610 + 16 + 610 + margen) y `cntBodyApr`
> lleva scroll vertical. Las dos cards llevan Height explícito por la misma
> razón — en un contenedor que envuelve, "estirar al alto del padre" ya no
> significa nada.
> **Breakpoint EN VIVO**: todo lo que depende del ancho referencia
> `scrAprobaciones.Width` **en la propiedad** — nunca una variable congelada en
> `OnVisible`.

---

## 3. Título

| Control | Tipo | Propiedades |
|---|---|---|
| `cntTituloApr` | Contenedor **vertical** | `FillPortions 0` · Height `52` · `LayoutMinHeight 52` · LayoutGap `3` · Fill `Transparent` — título ARRIBA, subtítulo DEBAJO (como el `.screen-head` del mockup y como `cntTituloRes` en 02) |
| `lblTituloApr` | Etiqueta clásica | `FillPortions 0` · Height `26` · ESTILO-TITULO · Text `"Aprobaciones"` |
| `lblSubtituloApr` | Etiqueta clásica | `FillPortions 0` · Height `20` · ESTILO-SUBTITULO · Wrap `true` · Text `"Solicitudes que requieren tu firma. Control de cuatro ojos: no podés aprobar lo que vos mismo solicitaste."` |

---

## 4. Barra de filtros

| Control | Tipo | Propiedades |
|---|---|---|
| `cardFiltrosApr` | Contenedor manual (receta 01 §7.1) | `FillPortions 0` · Height `74` · `LayoutMinHeight 74` — hijos con X/Y |
| `segPendApr` | Botón clásico (segmentado, receta 01 §7.7) | X `16` · Y `20` · Width `152` · Height `34` · RadiusTopLeft/BottomLeft `8` · RadiusTopRight/BottomRight `0` · BorderColor `ThemeLine` · BorderThickness `1` · Size `9.5` · FontWeight `Semibold` · Fill `If(ctxSegApr = "pendientes", ThemeAccent, ThemeSurface2)` · Color `If(ctxSegApr = "pendientes", ThemeAccentInk, ThemeInk2)` · Text `"Pendientes  " & CountRows(Filter('CH-Tbl-ApprovalRequests', ApprovalStatus = Blank()))` — **DELEGABLE** (`= Blank()` sobre picklist → `eq null`; `CountRows` sobre `Filter` delega en Dataverse). Es **la misma consulta** que el badge `pilNavBadgeApr` de `cmpNav` (01 §7.5): dos requests por el mismo número, aceptado a cambio de que el contador del segmento no dependa de la nav · OnSelect `Reset(inpMotivoApr); UpdateContext({ctxSegApr: "pendientes", ctxConfirmApr: ""}); Reset(galBandejaApr); Select(btnRecargarDetalleApr)` |
| `segHistApr` | Botón clásico (segmentado, receta 01 §7.7) | X `168` · Y `20` · Width `110` · Height `34` · RadiusTopRight/BottomRight `8` · RadiusTopLeft/BottomLeft `0` · BorderColor `ThemeLine` · BorderThickness `1` · Size `9.5` · FontWeight `Semibold` · Fill `If(ctxSegApr = "historico", ThemeAccent, ThemeSurface2)` · Color `If(ctxSegApr = "historico", ThemeAccentInk, ThemeInk2)` · Text `"Histórico"` · OnSelect `Reset(inpMotivoApr); UpdateContext({ctxSegApr: "historico", ctxConfirmApr: ""}); Reset(galBandejaApr); Select(btnRecargarDetalleApr)` |
| `lblBuscarApr` | Etiqueta clásica | X `298` · Y `10` · Width `120` · Height `14` · ESTILO-KPI-LABEL · Text `"BUSCAR"` |
| `inpBuscarApr` | Entrada de texto clásica | X `298` · Y `26` · Width `cardFiltrosApr.Width - 640` · Height `34` · PaddingLeft `30` · Size `9.5` · BorderColor `ThemeLine` · Fill `ThemeSurface2` · HintText `"Tipo de solicitud o número de solicitud…"` · Clear `true` |
| `icoBuscarApr` | Icono | Icon `Icon.Search` · X `306` · Y `35` · Width `16` · Height `16` · Color `ThemeInk3` · DisplayMode `DisplayMode.View` |
| `cirSyncApr` | Círculo clásico | X `cardFiltrosApr.Width - 320` · Y `40` · Width `8` · Height `8` · Fill `ThemeAccent` · BorderThickness `0` |
| `lblSyncApr` | Etiqueta clásica | X `cardFiltrosApr.Width - 304` · Y `32` · Width `160` · Height `16` · Size `8.5` · Color `ThemeInk3` · Text `If(IsBlank(ctxUltimaActApr), "Bandeja al día", "Actualizado " & Text(ctxUltimaActApr, "HH:mm:ss"))` |
| `btnActualizarApr` | Botón clásico | X `cardFiltrosApr.Width - 140` · Y `28` · Width `124` · Height `36` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · Text `"↻  Actualizar"` · FontWeight `Semibold` · Size `9.5` · OnSelect: ver abajo |

**`btnActualizarApr.OnSelect` (COMPLETO):**

```powerfx
Refresh('CH-Tbl-ApprovalRequests');
UpdateContext({ctxUltimaActApr: Now()});
Reset(galBandejaApr);
Select(btnRecargarDetalleApr)
```

> `icoBuscarApr` va **después** de `inpBuscarApr` en el árbol (= por encima en
> z-order) para que se vea sobre el fondo del input; el `PaddingLeft 30` del
> input deja el hueco.
> `inpBuscarApr` NO necesita `OnChange`: la galería filtra reactivamente sobre
> `inpBuscarApr.Text` (§5). El texto vacío no rompe nada porque
> `StartsWith(col, "")` es **true** para toda fila — por eso NO hace falta la
> guarda `IsBlank(...) || …`, que es justamente lo que suele romper la
> delegación.

---

## 5. Bandeja de solicitudes (`cardBandejaApr`)

| Control | Tipo | Propiedades |
|---|---|---|
| `lblBandejaTituloApr` | Etiqueta clásica | X `16` · Y `12` · Width `7 * Len(Self.Text) + 8` · Height `18` · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` · Text `"Bandeja de solicitudes"` — cabeza del panel, **NO es ESTILO-SECCION**: el mockup la da en *sentence case* y tinta fuerte (`.panel-head h3`) |
| `pilBandejaCountApr` | Botón clásico (píldora, receta 01 §7.2) | X `lblBandejaTituloApr.X + lblBandejaTituloApr.Width + 10` · Y `11` · **Width `12 + 7 * Len(Self.Text)`** · Fill `ThemeSunk` · Color `ThemeInk3` · Text `CountRows(galBandejaApr.AllItems) & If(ctxSegApr = "pendientes", " pendientes", " resueltas")` |
| `shpLineaHeadApr` | Rectángulo clásico | X `0` · Y `44` · Width `cardBandejaApr.Width` · Height `1` · Fill `ThemeLine` |

**`galBandejaApr`** (Galería vertical en blanco):

| Propiedad | Valor |
|---|---|
| X | `0` |
| Y | `45` |
| Width | `cardBandejaApr.Width` |
| Height | `cardBandejaApr.Height - 45` |
| TemplateSize | `58` |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| TemplateFill | `If(ThisItem.'CH-Tbl-ApprovalRequest' = galBandejaApr.Selected.'CH-Tbl-ApprovalRequest', ThemeAccentSoft, ThemeSurface)` |
| Items | ver abajo |
| OnSelect | `Reset(inpMotivoApr); UpdateContext({ctxConfirmApr: ""}); Select(btnRecargarDetalleApr)` |

**`galBandejaApr.Items` (COMPLETO):**

```powerfx
If(
    ctxSegApr = "pendientes",
    SortByColumns(
        Filter(
            'CH-Tbl-ApprovalRequests',
            ApprovalStatus = Blank()
            && (StartsWith(RequestType, inpBuscarApr.Text) || StartsWith(Name, inpBuscarApr.Text))
        ),
        "createdon",
        SortOrder.Descending
    ),
    SortByColumns(
        Filter(
            'CH-Tbl-ApprovalRequests',
            ApprovalStatus <> Blank()
            && (StartsWith(RequestType, inpBuscarApr.Text) || StartsWith(Name, inpBuscarApr.Text))
        ),
        "createdon",
        SortOrder.Descending
    )
)
```

> **Delegación — DELEGABLE**, y el `If` va en el nivel **MÁS EXTERNO** por una
> razón concreta: la cadena solo se pliega al servidor cuando el argumento de
> `SortByColumns` es un `Filter` **directo sobre el datasource**. Con la forma
> `SortByColumns(If(cond, Filter(...), Filter(...)), …)` el `If` devuelve una
> tabla ya materializada en el cliente y el `SortByColumns` externo **ordena lo
> truncado** — el mismo defecto del `Search(AddColumns(...))` viejo con otra
> cara. Por eso cada rama es una cadena completa `SortByColumns(Filter(ds, …))`,
> repetida a propósito.
> Dentro del predicado, todo es columna PROPIA de la tabla: `ApprovalStatus`
> (picklist, `= Blank()` / `<> Blank()` → `eq null` / `ne null`) y `StartsWith`
> sobre `RequestType` (`nvarchar`) y `Name` (`nvarchar`). `SortByColumns` usa el
> **nombre lógico** `"createdon"` (obligatorio).
> **Esto reemplaza** al `Search(AddColumns(...))` de la app vieja, que NO
> delegaba: truncaba a 500 filas y ordenaba lo truncado.
> *Si Studio marcara warning sobre `<> Blank()`, la variante equivalente y
> delegable es
> `ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado || ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado`.*

**Template de `galBandejaApr`** (X del template = X del encabezado − X de la
galería = X − 0; **todos los hijos con `OnSelect = Select(Parent)`**):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnFilaApr` | Botón clásico (overlay de fila, receta 01 §7.3) | X `0` · Y `0` · Width `galBandejaApr.Width` · Height `58` (= TemplateSize) · Fill `Transparent` · HoverFill `ThemeSurface2` · PressedFill `ThemeAccentSoft` · BorderThickness `0` · Text `""` · **al FONDO del z-order** · OnSelect `Select(Parent)` |
| `lblFilaTipoApr` | Etiqueta clásica | X `14` · Y `9` · **Width** = expresión de ancho útil (abajo) · Height `18` · Size `10.5` · FontWeight `Semibold` · Color `ThemeInk` · Text `ThisItem.RequestType` · OnSelect `Select(Parent)` |
| `lblFilaMetaApr` | Etiqueta clásica | X `14` · Y `29` · **Width** = misma expresión · Height `18` · Size `8.5` · Color `ThemeInk3` · Text `ThisItem.Name & "  ·  " & ThisItem.'Created By'.'Full Name'` · OnSelect `Select(Parent)` |
| `pilFilaEstadoApr` | Botón clásico (píldora, receta 01 §7.2) | **Width `12 + 7 * Len(Self.Text)`** · X `galBandejaApr.Width - If(scrAprobaciones.Width >= 1100, 130, 14) - Self.Width` · Y `18` · Height `22` · **Visible `ctxSegApr = "historico" && !IsBlank(ThisItem.ApprovalStatus)`** · Text `Text(ThisItem.ApprovalStatus)` · Fill `If(ThisItem.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado, ThemePosSoft, ThemeDangerSoft)` · Color `If(ThisItem.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado, ThemePos, ThemeDanger)` · OnSelect `Select(Parent)` |
| `lblFilaCuandoApr` | Etiqueta clásica | X `galBandejaApr.Width - 116` · Y `20` · Width `100` · Height `18` · Align `Align.Right` · Size `8.5` · Color `ThemeInk3` · **Visible `scrAprobaciones.Width >= 1100`** · Text: ver abajo · OnSelect `Select(Parent)` |
| `shpSepFilaApr` | Rectángulo clásico | X `0` · Y `57` (= TemplateSize − 1) · Width `galBandejaApr.Width` · Height `1` · Fill `ThemeLine` · OnSelect `Select(Parent)` (receta 01 §7.3: **todo** hijo del template, sin excepción — 1 px también se puede clickear) |

**Ancho útil de `lblFilaTipoApr` y `lblFilaMetaApr`** (idéntico en los dos —
reserva explícita del espacio de la píldora y del sello de tiempo, para que
nunca se pisen):

```powerfx
galBandejaApr.Width - 28
- If(ctxSegApr = "historico", 92, 0)
- If(scrAprobaciones.Width >= 1100, 116, 0)
```

**`lblFilaCuandoApr.Text`:**

```powerfx
With(
    { d: DateValue(ThisItem.'Created On') },
    If(
        d = Today(), "hoy " & Text(ThisItem.'Created On', "HH:mm"),
        d = DateAdd(Today(), -1, TimeUnit.Days), "ayer " & Text(ThisItem.'Created On', "HH:mm"),
        Text(ThisItem.'Created On', FmtFechaCorta)
    )
)
```

> `HH` (24 h) — `hh` sería reloj de 12 h sin AM/PM (regla 00 §L-5).
> **La lógica de fila vive en `galBandejaApr.OnSelect`**, no en
> `btnFilaApr.OnSelect`: si viviera en el overlay, los labels de arriba se
> comerían el clic (receta 01 §7.3).
>
> **Sobre el `'Created By'.'Full Name'` del template** — es la ÚNICA expansión de
> lookup permitida dentro de una galería en esta guía, y es deliberada:
> `'Full Name'` es el **nombre primario** de `Users`, y Dataverse lo devuelve
> como anotación en la misma respuesta que la fila (`FormattedValue` del
> lookup): **no** cuesta un request por fila. Cualquier OTRA columna del
> registro relacionado (`.'Primary Email'`, `.Title`, …) sí costaría un fetch por
> fila visible y está prohibida acá. La alternativa —`AddColumns` sobre el
> datasource para materializar el nombre— es exactamente lo que rompía la
> delegación en la app vieja: no se usa.

**Estado vacío de la bandeja** — los tres con
**Visible `CountRows(galBandejaApr.AllItems) = 0 && !ctxCargandoApr`**:

| Control | Tipo | Propiedades |
|---|---|---|
| `icoVacioBandejaApr` | Icono | Icon `Icon.CheckBadge` · X `cardBandejaApr.Width/2 - 16` · Y `cardBandejaApr.Height/2 - 60` · Width `32` · Height `32` · Color `ThemeInk3` |
| `lblVacioBandejaTitApr` | Etiqueta clásica | X `20` · Y `cardBandejaApr.Height/2 - 18` · Width `cardBandejaApr.Width - 40` · Height `20` · Size `12` · FontWeight `Semibold` · Color `ThemeInk2` · Align `Align.Center` · Text `If(ctxSegApr = "pendientes", "No hay solicitudes pendientes", "No hay solicitudes resueltas con ese criterio")` |
| `lblVacioBandejaTxtApr` | Etiqueta clásica | X `20` · Y `cardBandejaApr.Height/2 + 4` · Width `cardBandejaApr.Width - 40` · Height `36` · Size `9.5` · Color `ThemeInk3` · Align `Align.Center` · Wrap `true` · Text `If(ctxSegApr = "pendientes", "Todo lo que requería tu firma ya fue resuelto.", "Probá con otro texto de búsqueda o volvé a Pendientes.")` |

---

## 6. Motor de carga del detalle (`btnRecargarDetalleApr`)

Un único lugar que resuelve el registro seleccionado, el JSON, la tula y las
colecciones del cálculo. Lo disparan: `OnVisible`, `btnActualizarApr`, los dos
`seg…`, `galBandejaApr.OnSelect` y `btnDetStaleApr`.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnRecargarDetalleApr` | Botón clásico | **Hijo directo de `cardDetalleApr`, NO de `cntDetStackApr`** · X `cardDetalleApr.Width - 46` · Y `14` · Width `30` · Height `28` · Radius (las 4) `6` · Fill `Transparent` · HoverFill `ThemeSurface2` · Color `ThemeInk3` · BorderThickness `0` · Text `"↻"` · Size `11` · Tooltip `"Recargar el detalle"` · **Visible `true` (sin condición)** · OnSelect: ver abajo |

> **Por qué cuelga de la card y no del stack**: `Select()` **no dispara el
> `OnSelect` de un control invisible**. `cntDetStackApr` se oculta cuando no hay
> selección, así que un botón adentro sería inalcanzable justo cuando la bandeja
> pasa de vacía a con-filas — que es el caso de la auto-selección. Por eso vive
> afuera y siempre visible.

**`btnRecargarDetalleApr.OnSelect` (COMPLETO — listo para pegar):**

```powerfx
UpdateContext({ctxCargandoApr: true});

// 1) JSON del contrato (guardado contra selección vacía o campo nulo)
UpdateContext(
    {
        ctxResApr:
            If(
                IsBlank(galBandejaApr.Selected) || IsBlank(galBandejaApr.Selected.RequestResources),
                "{}",
                galBandejaApr.Selected.RequestResources
            )
    }
);

// 2) GUID de tula: "BagGUID" en 8 tipos, "GUID" en Remapear/Eliminar.
//    NUNCA "BagID" — esa clave es el NÚMERO de tula (bug de la app vieja).
UpdateContext(
    {
        ctxBagGuidApr:
            Coalesce(
                IfError(GUID(Text(ParseJSON(ctxResApr).BagGUID)), Blank()),
                IfError(GUID(Text(ParseJSON(ctxResApr).GUID)), Blank())
            )
    }
);

// 3) Registros frescos (1 request cada uno, por clave primaria)
UpdateContext(
    {
        ctxBagApr:
            If(
                IsBlank(ctxBagGuidApr),
                Blank(),
                LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr)
            )
    }
);
UpdateContext(
    {
        ctxSesGuidApr:
            Coalesce(
                ctxBagApr.RecountingSession.'CH-Tbl-RecountingSession',
                IfError(GUID(Text(ParseJSON(ctxResApr).RecountingSessionGUID)), Blank())
            )
    }
);
UpdateContext(
    {
        ctxSesionApr:
            If(
                IsBlank(ctxSesGuidApr),
                Blank(),
                LookUp('CH-Tbl-RecountingSessions', 'CH-Tbl-RecountingSession' = ctxSesGuidApr)
            )
    }
);

// 4) Depósitos RECONTADOS de la tula — comparación PK a PK (delegable)
If(
    IsBlank(ctxBagGuidApr),
    Clear(colRecApr),
    ClearCollect(
        colRecApr,
        Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = ctxBagGuidApr)
    )
);

// 5) Depósitos DECLARADOS de la bolsa declarada — 1 salto, delegable.
//    Trae también los declarados NO recontados (los que la vieja perdía).
If(
    IsBlank(ctxBagApr) || IsBlank(ctxBagApr.BagDeclarationID),
    Clear(colDecApr),
    ClearCollect(
        colDecApr,
        Filter(
            'CH-Tbl-DeclaredDeposits',
            DeclaredBag.'CH-Tbl-DeclaredBags' = ctxBagApr.BagDeclarationID.'CH-Tbl-DeclaredBags'
        )
    )
);

// 6.a) Pares declarado/recontado POR DEPÓSITO y POR COMPONENTE.
//      MISMA unidad de cálculo que el motor de asientos de §11 9.a/9.b:
//      lo que el supervisor firma es exactamente lo que se contabiliza.
Clear(colParApr);
ForAll(
    colRecApr As R,
    With(
        {
            D: LookUp(
                colDecApr,
                RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting' = R.'CH-Tbl-DepositsRecounting'
            )
        },
        Collect(
            colParApr,
            {Cur: Text(R.Currency), Comp: "Efectivo",      Dec: Coalesce(D.CashAmount, 0),            Rec: Coalesce(R.CashAmount, 0)},
            {Cur: Text(R.Currency), Comp: "Cheques BAC",   Dec: Coalesce(D.BACLocalCheckAmount, 0),   Rec: Coalesce(R.BACLocalCheckAmount, 0)},
            {Cur: Text(R.Currency), Comp: "Otros cheques", Dec: Coalesce(D.OtherLocalCheckAmount, 0), Rec: Coalesce(R.OtherLocaCheckAmount, 0)}
        )
    )
);
// declarados que NADIE recontó → Faltante puro (espeja §11 9.b)
ForAll(
    Filter(colDecApr, IsBlank(RelatedRecountedDeposit)) As D2,
    Collect(
        colParApr,
        {Cur: Text(D2.Currency), Comp: "Efectivo",      Dec: Coalesce(D2.CashAmount, 0),            Rec: 0},
        {Cur: Text(D2.Currency), Comp: "Cheques BAC",   Dec: Coalesce(D2.BACLocalCheckAmount, 0),   Rec: 0},
        {Cur: Text(D2.Currency), Comp: "Otros cheques", Dec: Coalesce(D2.OtherLocalCheckAmount, 0), Rec: 0}
    )
);

// 6.b) Una fila por componente × moneda, con Sobrante y Faltante SIN compensar
Clear(colDifApr);
ForAll(
    GroupBy(colParApr, "Cur", "Comp", "Filas") As G,
    With(
        {
            dec: Coalesce(Sum(G.Filas, Dec), 0),
            rec: Coalesce(Sum(G.Filas, Rec), 0),
            sob: Coalesce(Sum(G.Filas, Max(Rec - Dec, 0)), 0),
            fal: Coalesce(Sum(G.Filas, Abs(Min(Rec - Dec, 0))), 0),
            sim: Switch(G.Cur, "NIO", SimboloNIO, "USD", SimboloUSD, "EUR", SimboloEUR, G.Cur)
        },
        If(
            dec <> 0 || rec <> 0,
            Collect(colDifApr, {Comp: G.Comp & " " & sim, Dec: dec, Rec: rec, Sob: sob, Fal: fal})
        )
    )
);

Reset(galDifApr);
UpdateContext(
    {
        ctxCargadoIdApr:
            Coalesce(
                galBandejaApr.Selected.'CH-Tbl-ApprovalRequest',
                GUID("00000000-0000-0000-0000-000000000000")
            ),
        ctxCargandoApr: false
    }
)
```

> **Por qué el preview se calcula POR DEPÓSITO y recién después se agrupa**: si
> se sumaran primero todos los depósitos de la tula y se restara al final,
> dos depósitos de +200.000 y −200.000 de efectivo mostrarían "Cuadra" mientras
> el motor de §11 genera un Sobrante y un Faltante. Es el mismo bug del `Abs()`
> sobre el neto, un nivel más arriba. `colParApr` conserva el par
> declarado/recontado de CADA depósito; `Sum(..., Max(Rec - Dec, 0))` y
> `Sum(..., Abs(Min(Rec - Dec, 0)))` acumulan sobrantes y faltantes en columnas
> **separadas** que nunca se compensan.
>
> **Ortografía real de las columnas (00 §5.1) — verificada en `Entity.xml`**:
> en `CH-Tbl-DepositsRecountings` es **`OtherLocaCheckAmount`** (falta la
> segunda `l`); en `CH-Tbl-DeclaredDeposits` y `CH-Tbl-Transactions` es
> **`OtherLocalCheckAmount`** (bien escrito). Arriba cada colección usa la suya.
>
> **Delegación** — patrón `Lookup.'PK' = X` en todo el bloque, nunca
> `record = record`:
> - `LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr)` — **delegable** (PK).
> - `Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = ctxBagGuidApr)` —
>   **delegable**: PK del lookup contra un GUID. Ojo: la PK de esa tabla es
>   **`CH-Tbl-DepositsRecounting` en SINGULAR** (01 §2).
> - `Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.'CH-Tbl-DeclaredBags' = …)` —
>   **delegable**, 1 salto. *(La vieja filtraba por
>   `RelatedRecountedDeposit.BagID.'CH-Tbl-Bags'` — 2 saltos, no delegable, y
>   semánticamente perdía los declarados que nadie recontó.)*
> - Todo 6.a/6.b es **local** sobre colecciones ya cargadas: cero requests. El
>   join `LookUp(colDecApr, RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting' =
>   R.'CH-Tbl-DepositsRecounting')` es PK contra PK dentro de memoria — no
>   dispara ningún fetch (a diferencia de `R.RelatedDeclaredDeposit.CashAmount`,
>   que sí lo haría, una vez por fila).
>
> **Gotcha 01 §7.8 aplicado**: los `ClearCollect` de los pasos 4 y 5 tienen el
> `If` **por fuera** con su rama `Clear(...)`. Un `ClearCollect(col, If(cond,
> Filter(...)))` sin else revienta con `ErrorKind.Internal`.

---

## 7. Detalle — cabecera, aviso de desfase, datos y estado vacío

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDetStackApr` | Contenedor vertical | X `0` · Y `0` · Width `cardDetalleApr.Width` · Height `cardDetalleApr.Height` · PaddingLeft/Right `16` · PaddingTop `14` · PaddingBottom `14` · LayoutGap `12` · Fill `Transparent` · **CON scroll** (`LayoutOverflowY: Desplazar`) · **Visible `!IsBlank(galBandejaApr.Selected)`** |
| `cntDetCabeceraApr` | Contenedor manual | `FillPortions 0` · Height `54` · `LayoutMinHeight 54` · Fill `Transparent` — hijos con X/Y |
| `lblDetTituloApr` | Etiqueta clásica | X `0` · Y `0` · Width `cntDetCabeceraApr.Width - 46` · Height `24` · Size `13` · FontWeight `Semibold` · Color `ThemeInk` · Text: ver abajo |
| `lblDetSubApr` | Etiqueta clásica | X `0` · Y `26` · Width `cntDetCabeceraApr.Width - 46` · Height `26` · Size `9` · Color `ThemeInk3` · Wrap `true` · Text: ver abajo |

**`lblDetTituloApr.Text`:**

```powerfx
galBandejaApr.Selected.RequestType
& If(
    !IsBlank(ctxBagApr),
    " — " & ctxBagApr.BagID,
    IsBlank(galBandejaApr.Selected.Name),
    "",
    " — " & galBandejaApr.Selected.Name
)
```

**`lblDetSubApr.Text`:**

```powerfx
"Solicitada por " & galBandejaApr.Selected.'Created By'.'Full Name'
& If(IsBlank(ctxSesionApr.CountingDesk.Name), "", " (" & ctxSesionApr.CountingDesk.Name & ")")
& If(IsBlank(ctxSesionApr.Name), "", "  ·  sesión " & ctxSesionApr.Name)
& "  ·  " & Text(galBandejaApr.Selected.'Created On', FmtFecha)
& If(IsBlank(galBandejaApr.Selected.ApprovalStatus), "", "  ·  " & Text(galBandejaApr.Selected.ApprovalStatus))
& If(IsBlank(galBandejaApr.Selected.SolvedBy), "", " por " & galBandejaApr.Selected.SolvedBy.'Full Name')
& If(IsBlank(galBandejaApr.Selected.ResolutionDatetime), "", " el " & Text(galBandejaApr.Selected.ResolutionDatetime, FmtFecha))
```

> **Los tres tramos de la resolución van SEPARADOS, no anidados bajo un solo
> `If(IsBlank(ApprovalStatus))`.** Verificado en el productor: las sesiones de
> recuento de la propia mesa se **auto-aprueban** (`Screen1.fx.yaml` L10195-10199)
> escribiendo `ApprovalStatus: Aprobado` **sin** `SolvedBy` ni
> `ResolutionDatetime`. Con la guarda única, esas filas del Histórico rendían
> literalmente `"  ·  Aprobado por  el "`.

> **`" — " & Blank()` devuelve `" — "`, NO `Blank()`** — por eso cada tramo
> opcional va con su `If(IsBlank(x), "", " — " & x)` y **no** con `Coalesce`,
> que no sirve para concatenación.
> `ctxSesionApr.CountingDesk.Name` y `.'Created By'.'Full Name'` expanden un
> lookup sobre **un solo registro** (no dentro de una galería): 1 request, no N.
> `CountingDesks` **no es datasource** de esta app (01 §2) — se resuelve por
> expansión del lookup, exactamente como en 02.

**Aviso de detalle desfasado** (`cntDetStaleApr`) — se prende cuando la fila
seleccionada no es la que está cargada en las colecciones (puede pasar tras un
`Refresh` que saca la solicitud resuelta de la bandeja, o mientras la galería
resuelve su primer fetch). **Mientras esté prendido, los botones de firma están
deshabilitados** (§10): nunca se firma una previsualización vieja.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDetStaleApr` | Contenedor manual | `FillPortions 0` · Height `36` · `LayoutMinHeight 36` · Fill `ThemeWarnSoft` · BorderColor `ThemeWarn` · BorderThickness `1` · Radius (las 4) `8` · **Visible `ctxCargadoIdApr <> galBandejaApr.Selected.'CH-Tbl-ApprovalRequest'`** — hijos con X/Y |
| `lblDetStaleApr` | Etiqueta clásica | X `12` · Y `9` · Width `cntDetStaleApr.Width - 130` · Height `18` · Size `9` · FontWeight `Semibold` · Color `ThemeWarn` · Text `"El detalle no corresponde a la solicitud seleccionada."` |
| `btnDetStaleApr` | Botón clásico | X `cntDetStaleApr.Width - 118` · Y `6` · Width `106` · Height `24` · Radius (las 4) `6` · Fill `Transparent` · Color `ThemeWarn` · BorderColor `ThemeWarn` · BorderThickness `1` · Size `9` · Text `"↻  Cargar"` · OnSelect `Select(btnRecargarDetalleApr)` |

**Bloque clave-valor** (`cntKvApr`, Contenedor manual, Height `76` ·
`LayoutMinHeight 76` · `FillPortions 0` · Fill `ThemeSurface2` · Radius (las 4) `8` ·
BorderColor `ThemeLine` · BorderThickness `1`):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblKvClienteEtqApr` | Etiqueta clásica | X `12` · Y `8` · Width `100` · Height `18` · Size `9` · Color `ThemeInk3` · Text `"Cliente"` |
| `lblKvClienteApr` | Etiqueta clásica | X `116` · Y `8` · Width `cntKvApr.Width - 128` · Height `18` · Size `9.5` · FontWeight `Semibold` · Color `ThemeInk` · Align `Align.Right` · Text `Coalesce(ctxBagApr.ReceptionID.RemitentClient.Name, ctxBagApr.ReceptionID.RemitentName, "—")` |
| `lblKvCuentaEtqApr` | Etiqueta clásica | X `12` · Y `30` · Width `100` · Height `18` · Size `9` · Color `ThemeInk3` · Text `"Cuenta"` |
| `lblKvCuentaApr` | Etiqueta clásica | X `116` · Y `30` · Width `cntKvApr.Width - 128` · Height `18` · Size `9.5` · FontWeight `Semibold` · Color `ThemeInk` · Align `Align.Right` · Text: ver abajo |
| `lblKvTulaEtqApr` | Etiqueta clásica | X `12` · Y `52` · Width `100` · Height `18` · Size `9` · Color `ThemeInk3` · Text `"Tula"` |
| `lblKvTulaApr` | Etiqueta clásica | X `116` · Y `52` · Width `cntKvApr.Width - 128` · Height `18` · Size `9.5` · FontWeight `Semibold` · Color `ThemeInk` · Align `Align.Right` · Text `Coalesce(ctxBagApr.BagID, "—")` — **NO es ESTILO-MONO**: en el mockup el `dd` del `kv` va en tinta normal; el verde acento está reservado a los IDs de tabla (`.mono`) |

**`lblKvCuentaApr.Text`:**

```powerfx
If(
    CountRows(colRecApr) = 0, "—",
    CountRows(Distinct(colRecApr, AccountNumber)) > 1,
        CountRows(Distinct(colRecApr, AccountNumber)) & " cuentas",
    Text(First(colRecApr).AccountNumber, "0")
)
```

> `Distinct`/`CountRows` sobre colección local: cero requests. `"0"` como
> formato evita el separador de miles en un número de cuenta.
> **`RemitentClient.Name` exige `prvRead sanic_CHTblClient`** en el rol
> `CH-SR-CountingSupervisor` (00 §3.3): sin ese privilegio la expresión cae en
> `RemitentName` por el `Coalesce` y no rompe la pantalla.

**Resumen por tipo de solicitud** — cubre los 10 tipos, incluidos los que no
tienen tula:

| Control | Tipo | Propiedades |
|---|---|---|
| `lblDetResumenApr` | Etiqueta clásica | `FillPortions 0` · Height `46` · `LayoutMinHeight 46` · Width `cntDetStackApr.Width - 32` · Size `9.5` · Color `ThemeInk2` · Wrap `true` · Fill `Transparent` · Text: ver abajo |

```powerfx
Switch(
    galBandejaApr.Selected.RequestType,
    "Remapear tula",
        "Mover la tula a la planilla " & Text(ParseJSON(ctxResApr).ReceiptID)
        & ". Al rechazar se restituye el estado previo (" & Text(ParseJSON(ctxResApr).BagPreviousState) & ").",
    "Agregar tula",
        "Alta de la tula " & Text(ParseJSON(ctxResApr).BagID)
        & " en la planilla del lote. Al rechazar, la tula pendiente se ELIMINA.",
    "Eliminar tula",
        "Baja de la tula de su planilla. Al rechazar se restituye el estado previo ("
        & Text(ParseJSON(ctxResApr).BagPreviousState) & ").",
    "Modificar numero de planilla",
        "Cambiar el número de planilla a " & Text(ParseJSON(ctxResApr).NewReceiptID)
        & ". Se valida que no exista ese número en el mismo lote.",
    "Editar tula",
        "Cambiar el número de tula a " & Text(ParseJSON(ctxResApr).BagID)
        & ". Se valida que no exista ese número en el mismo lote de recepción.",
    "Crear sesion de recuento",
        "Confirmar la sesión de recuento del cajero. Al rechazar, la sesión se ELIMINA.",
    "Primera tula en planilla",
        "Asignar la primera tula de la planilla al cajero y enlazarla con su bolsa declarada.",
    "Tula sin declaracion",
        "Asignar al cajero una tula que no tiene bolsa declarada y abrir su detalle de sesión.",
    "Tula con diferencia",
        "Cerrar el recuento con diferencias. Se generan los asientos del cuadro de abajo.",
    "Tula recontada sin declaracion",
        "Cerrar el recuento de una tula sin declaración: cada depósito recontado genera un asiento «Sin declaracion».",
    "Solicitud sin resumen definido — revisá RequestResources antes de firmar."
)
```

**Estado vacío del detalle** — los dos con **Visible `IsBlank(galBandejaApr.Selected)`**
(hijos directos de `cardDetalleApr`, fuera del stack):

| Control | Tipo | Propiedades |
|---|---|---|
| `icoDetVacioApr` | Icono | Icon `Icon.DocumentWithContent` · X `cardDetalleApr.Width/2 - 16` · Y `cardDetalleApr.Height/2 - 46` · Width `32` · Height `32` · Color `ThemeInk3` |
| `lblDetVacioApr` | Etiqueta clásica | X `24` · Y `cardDetalleApr.Height/2 - 4` · Width `cardDetalleApr.Width - 48` · Height `36` · Size `9.5` · Color `ThemeInk3` · Align `Align.Center` · Wrap `true` · Text `"Elegí una solicitud de la bandeja para ver el detalle."` |

> **Gotcha**: las galerías clásicas **auto-seleccionan la 1ª fila**, así que el
> detalle nunca arranca vacío mientras haya solicitudes; este estado solo
> aparece con la bandeja vacía. Por eso el detalle lee
> `galBandejaApr.Selected` directamente y NO una variable seteada en
> `OnSelect` (que no dispararía en la auto-selección), y por eso existe el
> centinela `ctxCargadoIdApr` + `cntDetStaleApr`.

---

## 8. Detalle — diferencia POR COMPONENTE (`cntDifApr`)

Es el corazón del rediseño: **el neto no existe**. Cada componente
(efectivo / cheques BAC / otros cheques) de cada moneda muestra su sobrante y su
faltante **por separado**, y eso es exactamente lo que §11 contabiliza.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDifApr` | Contenedor manual | `FillPortions 0` · Height `26 + CountRows(colDifApr) * 30` · `LayoutMinHeight` = misma expresión · Radius (las 4) `8` · BorderColor `ThemeLine` · BorderThickness `1` · Fill `ThemeSurface` · **Visible `CountRows(colDifApr) > 0`** — hijos con X/Y |
| `shpBandDifApr` | Rectángulo clásico | X `0` · Y `0` · Width `cntDifApr.Width` · Height `25` · Fill `ThemeSurface2` · **al FONDO del z-order** de `cntDifApr` (banda gris del `thead`) |

**Encabezados** (Etiquetas clásicas ESTILO-HEADER-COL, Y `6`, Height `16`;
X/Width como fracción del ancho de la galería — **nombrando la galería**, nunca
`Self.Width`, que es circular):

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblColCompApr` | `"COMPONENTE"` | `10` | `galDifApr.Width * 0.22` | Left |
| `lblColDecApr` | `"DECLARADO"` | `10 + galDifApr.Width * 0.22` | `galDifApr.Width * 0.13` | Right |
| `lblColRecApr` | `"RECONTADO"` | `10 + galDifApr.Width * 0.35` | `galDifApr.Width * 0.13` | Right |
| `lblColDifApr` | `"DIFERENCIA"` | `10 + galDifApr.Width * 0.48` | `galDifApr.Width * 0.29` | Right |
| `lblColAsientoApr` | `"ASIENTO"` | `10 + galDifApr.Width * 0.77` | `galDifApr.Width * 0.23` | Left |

`shpLineaDifApr` (Rectángulo clásico): X `0` · Y `25` · Width `cntDifApr.Width` ·
Height `1` · Fill `ThemeLine`.

**`galDifApr`** (Galería vertical en blanco): X `10` · Y `26` ·
Width `cntDifApr.Width - 20` · Height `CountRows(colDifApr) * 30` ·
TemplateSize `30` · TemplatePadding `0` · ShowScrollbar `false` ·
Items `colDifApr` · OnSelect `false` *(la fila no es clicable — es lectura)*.

**Template** (X del template = X del encabezado − 10):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblCelCompApr` | Etiqueta clásica | X `0` · Y `7` · Width `galDifApr.Width * 0.22` · Height `18` · ESTILO-CELDA · Text `ThisItem.Comp` |
| `lblCelDecApr` | Etiqueta clásica | X `galDifApr.Width * 0.22` · Y `7` · Width `galDifApr.Width * 0.13` · Height `18` · ESTILO-CELDA-NUM · Text `Text(Coalesce(ThisItem.Dec, 0), FmtMoney)` |
| `lblCelRecApr` | Etiqueta clásica | X `galDifApr.Width * 0.35` · Y `7` · Width `galDifApr.Width * 0.13` · Height `18` · ESTILO-CELDA-NUM · Text `Text(Coalesce(ThisItem.Rec, 0), FmtMoney)` |
| `lblCelDifApr` | Etiqueta clásica | X `galDifApr.Width * 0.48` · Y `7` · Width `galDifApr.Width * 0.29` · Height `18` · ESTILO-CELDA-NUM (Semibold) · Color y Text: ver abajo |
| `pilCelAsientoApr` | Botón clásico (píldora, receta 01 §7.2) | **Width `12 + 7 * Len(Self.Text)`** · **X `galDifApr.Width - 4 - Self.Width`** (anclada a la DERECHA, crece hacia la izquierda) · Y `4` · Height `22` · Text, Fill y Color: ver abajo |
| `shpSepDifApr` | Rectángulo clásico | X `0` · Y `29` (= TemplateSize − 1) · Width `galDifApr.Width` · Height `1` · Fill `ThemeLine` |

**`lblCelDifApr.Text`:**

```powerfx
If(
    ThisItem.Sob > 0 && ThisItem.Fal > 0,
        "+" & Text(ThisItem.Sob, FmtMoney) & " / −" & Text(ThisItem.Fal, FmtMoney),
    ThisItem.Sob > 0, "+" & Text(ThisItem.Sob, FmtMoney),
    ThisItem.Fal > 0, "−" & Text(ThisItem.Fal, FmtMoney),
    Text(0, FmtMoney)
)
```

**`lblCelDifApr.Color`:**

```powerfx
If(
    ThisItem.Sob > 0 && ThisItem.Fal > 0, ThemeWarn,
    ThisItem.Sob > 0, ThemePos,
    ThisItem.Fal > 0, ThemeNeg,
    ThemeInk3
)
```

**`pilCelAsientoApr`** — cada propiedad lleva su fórmula COMPLETA (un `With` no
puede setear tres propiedades a la vez):

| Propiedad | Fórmula |
|---|---|
| Text | `If(ThisItem.Sob > 0 && ThisItem.Fal > 0, "Sobr. + Falt.", ThisItem.Sob > 0, "Sobrante", ThisItem.Fal > 0, "Faltante", "Cuadra")` |
| Fill | `If(ThisItem.Sob > 0 && ThisItem.Fal > 0, ThemeWarnSoft, ThisItem.Sob > 0, ThemePosSoft, ThisItem.Fal > 0, ThemeNegSoft, ThemeSunk)` |
| Color | `If(ThisItem.Sob > 0 && ThisItem.Fal > 0, ThemeWarn, ThisItem.Sob > 0, ThemePos, ThisItem.Fal > 0, ThemeNeg, ThemeInk3)` |

> **Aritmética de la grilla — las fracciones están calculadas, no elegidas a ojo.**
> Con el ancho mínimo de §2: `cardDetalleApr` 550 − 32 de padding = `cntDifApr`
> 518 → **`galDifApr.Width` = 498**. De ahí salen las 5 columnas:
>
> | Columna | Fracción | px a 498 | Contenido más largo | px que necesita |
> |---|---|---|---|---|
> | COMPONENTE | 0.22 | 110 | `"Otros cheques C$"` | ~106 |
> | DECLARADO | 0.13 | 65 | `"200,000.00"` | ~60 |
> | RECONTADO | 0.13 | 65 | `"200,000.00"` | ~60 |
> | DIFERENCIA | 0.29 | **144** | `"+200,000.00 / −200,000.00"` | ~144 |
> | ASIENTO | 0.23 | 115 | píldora `"Sobr. + Falt."` | 103 |
>
> **La píldora se ancla a la derecha, no a una fracción**: `X = galDifApr.Width -
> 4 - Self.Width`. Con una X fraccionaria, `"Sobr. + Falt."` (13 caracteres →
> `12 + 7×13` = 103 px) se salía de la galería o se montaba sobre el monto — y
> justo en el caso dual, que es cuando la columna DIFERENCIA lleva su texto más
> largo. Anclada a la derecha arranca en `498 − 4 − 103` = **391**, mientras
> DIFERENCIA termina en `0.77 × 498` = **383**: 8 px de aire, y alineada con su
> propio encabezado ASIENTO.
> **Por eso el detalle pide `LayoutMinWidth 550` y no 420** (§2): a 420 la
> galería mide 368, DIFERENCIA queda en 59 px y el monto dual —el que el
> checklist §16 obliga a verificar— se clipea al 41 %.
>
> **Responsive — decisión explícita**: esta grilla **NO oculta columnas** bajo el
> breakpoint. Las 5 son carga probatoria de una firma contable: esconderle el
> "Declarado" a quien está por generar un asiento es peor que un scroll. El
> mecanismo responsive del detalle es el `LayoutWrap` de §2
> (`LayoutMinWidth 550` garantiza el ancho mínimo legible de las 5 columnas, con
> la aritmética de arriba).
> Donde sí se aplica el breakpoint en vivo es en la bandeja (§5,
> `lblFilaCuandoApr.Visible`, el ancho útil de los labels de fila y la X de
> `pilFilaEstadoApr`).
> **Montos con `FmtMoney = "#,##0.00"`** — el cero se ve `0.00`, nunca `.00`
> (00 §L-7); todos con `Coalesce(..., 0)` porque `Sum` de tabla vacía da `Blank`.

---

## 9. Detalle — aviso de asientos y cuatro ojos

| Control | Tipo | Propiedades |
|---|---|---|
| `cntAvisoAsientosApr` | Contenedor manual | `FillPortions 0` · Height `66` · `LayoutMinHeight 66` · Fill `ThemeWarnSoft` · BorderColor `ThemeWarn` · BorderThickness `1` · Radius (las 4) `8` · **Visible `CountRows(colDifApr) > 0`** — hijos con X/Y |
| `icoAvisoApr` | Icono | Icon `Icon.Warning` · X `12` · Y `20` · Width `18` · Height `18` · Color `ThemeWarn` |
| `lblAvisoAsientosApr` | Etiqueta clásica | X `38` · Y `8` · Width `cntAvisoAsientosApr.Width - 50` · Height `50` · Size `9` · Color `ThemeWarn` · Wrap `true` · Text: ver abajo |

```powerfx
With(
    {
        s: Filter(colDifApr, Sob > 0),
        f: Filter(colDifApr, Fal > 0)
    },
    If(
        CountRows(s) + CountRows(f) = 0,
        "Sin diferencias por componente: solo se registran los asientos de depósito.",
        "Se generarán asientos separados — "
        & Concat(s, "un Sobrante de " & Text(Sob, FmtMoney) & " (" & Comp & ")", "; ")
        & If(CountRows(s) > 0 && CountRows(f) > 0, "; ", "")
        & Concat(f, "un Faltante de " & Text(Fal, FmtMoney) & " (" & Comp & ")", "; ")
        & ". No se compensan a un neto — cada discrepancia queda registrada."
    )
)
```

| Control | Tipo | Propiedades |
|---|---|---|
| `lblCuatroOjosApr` | Etiqueta clásica | `FillPortions 0` · Height `34` · `LayoutMinHeight 34` · Width `cntDetStackApr.Width - 32` · Size `8.5` · Wrap `true` · Color y Text: ver abajo (las **dos** propiedades con el MISMO encadenado de condiciones) |

**`lblCuatroOjosApr.Color`:**

```powerfx
If(
    !IsBlank(galBandejaApr.Selected.ApprovalStatus), ThemeInk3,
    galBandejaApr.Selected.'Created By'.User = UsuarioActual.User, ThemeDanger,
    ThemeInk3
)
```

**`lblCuatroOjosApr.Text`:**

```powerfx
If(
    !IsBlank(galBandejaApr.Selected.ApprovalStatus),
    "Solicitud ya resuelta — solo lectura.",
    galBandejaApr.Selected.'Created By'.User = UsuarioActual.User,
    "Cuatro ojos: esta solicitud la creaste vos (" & UsuarioActual.'Full Name'
    & "). No podés firmarla — pedile a otro supervisor que la resuelva.",
    "Solicitante ≠ aprobador: la solicitó " & galBandejaApr.Selected.'Created By'.'Full Name'
    & ", vos sos " & UsuarioActual.'Full Name'
    & " — habilitado. La app revalida el estado antes de ejecutar (anti doble-aprobación)."
)
```

> **El solicitante es `'Created By'`** — `CH-Tbl-ApprovalRequest` NO tiene
> columna propia de solicitante (00 §5.1). La comparación es **PK contra PK**
> (`.User` es el GUID de `Users`), nunca `record = record` ni por nombre.

---

## 10. Acciones y diálogo de confirmación

| Control | Tipo | Propiedades |
|---|---|---|
| `cntAccionesApr` | Contenedor horizontal | `FillPortions 0` · Height `40` · `LayoutMinHeight 40` · LayoutGap `10` · Fill `Transparent` · **Visible `ctxSegApr = "pendientes" && IsBlank(galBandejaApr.Selected.ApprovalStatus)`** |
| `btnAprobarApr` | Botón clásico | `FillPortions 1` · `LayoutMinWidth 200` · Height `38` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · HoverFill `ColorFade(ThemeAccent, -10%)` · DisabledFill `ThemeSunk` · DisabledColor `ThemeInk3` · Text `"✓  Aprobar y generar asientos"` · Size `9.5` · FontWeight `Semibold` · **DisplayMode**: ver abajo · OnSelect `Reset(inpMotivoApr); UpdateContext({ctxConfirmApr: "aprobar"})` |
| `btnRechazarApr` | Botón clásico | `FillPortions 1` · `LayoutMinWidth 140` · Height `38` · Radius (las 4) `8` · Fill `ThemeSurface2` · Color `ThemeDanger` · BorderColor `ThemeLine` · BorderThickness `1` · HoverFill `ThemeDangerSoft` · DisabledFill `ThemeSunk` · DisabledColor `ThemeInk3` · Text `"✕  Rechazar"` · Size `9.5` · FontWeight `Semibold` · **DisplayMode**: misma expresión · OnSelect `Reset(inpMotivoApr); UpdateContext({ctxConfirmApr: "rechazar"})` |

**`DisplayMode` de `btnAprobarApr` y `btnRechazarApr` (idéntica — regla única,
nunca dos reglas separadas que se contradigan):**

```powerfx
If(
    IsBlank(galBandejaApr.Selected)
    || !IsBlank(galBandejaApr.Selected.ApprovalStatus)
    || galBandejaApr.Selected.'Created By'.User = UsuarioActual.User
    || ctxCargadoIdApr <> galBandejaApr.Selected.'CH-Tbl-ApprovalRequest'
    || ctxCargandoApr,
    DisplayMode.Disabled,
    DisplayMode.Edit
)
```

> Los dos botones llevan `FillPortions 1` (mismo ancho, como el `flex:1` del
> mockup). La cuarta condición es el centinela de §7: **no se firma un detalle
> desfasado**.
> `cntAccionesApr` **no** lleva `LayoutWrap`: con `LayoutMinWidth 200 + 140 + 10`
> = 350 entra siempre en un detalle de 550 mínimo, y un contenedor que envuelve
> necesitaría Height condicional (§2) sin ganar nada acá.

**Overlay de confirmación** (hijo directo de la pantalla, arriba del body y
debajo del overlay de carga):

| Control | Tipo | Propiedades |
|---|---|---|
| `cntConfirmApr` | Contenedor manual | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxConfirmApr <> ""`** |
| `cardConfirmApr` | Contenedor manual (receta 01 §7.1) | X `(cntConfirmApr.Width - 460)/2` · Y `(cntConfirmApr.Height - 260)/2` · Width `460` · Height `260` — hijos con X/Y |
| `lblConfirmTituloApr` | Etiqueta clásica | X `20` · Y `18` · Width `420` · Height `22` · Size `13` · FontWeight `Semibold` · Color `ThemeInk` · Text `If(ctxConfirmApr = "aprobar", "Confirmar aprobación", "Confirmar rechazo")` |
| `lblConfirmTextoApr` | Etiqueta clásica | X `20` · Y `44` · Width `420` · Height `52` · Size `9.5` · Color `ThemeInk2` · Wrap `true` · Text: ver abajo |
| `lblMotivoEtqApr` | Etiqueta clásica | X `20` · Y `104` · Width `420` · Height `14` · ESTILO-KPI-LABEL · Text `"MOTIVO DEL RECHAZO (OBLIGATORIO)"` · **Visible `ctxConfirmApr = "rechazar"`** |
| `inpMotivoApr` | Entrada de texto clásica | X `20` · Y `124` · Width `420` · Height `56` · Mode `TextMode.MultiLine` · Size `9.5` · BorderColor `ThemeLine` · Fill `ThemeSurface2` · HintText `"Por qué se rechaza — queda en StatusReason"` · **Visible `ctxConfirmApr = "rechazar"`** |
| `btnCancelarConfirmApr` | Botón clásico | X `20` · Y `200` · Width `120` · Height `38` · Radius (las 4) `8` · Fill `Transparent` · Color `ThemeInk2` · BorderColor `ThemeLine` · BorderThickness `1` · Text `"Cancelar"` · Size `9.5` · OnSelect `Reset(inpMotivoApr); UpdateContext({ctxConfirmApr: ""})` |
| `btnConfirmarAprobarApr` | Botón clásico | X `232` · Y `200` · Width `208` · Height `38` · Radius (las 4) `8` · Fill `ThemeAccent` · Color `ThemeAccentInk` · Text `"Sí, aprobar y contabilizar"` · Size `9.5` · FontWeight `Semibold` · **Visible `ctxConfirmApr = "aprobar"`** · OnSelect: **§11** |
| `btnConfirmarRechazarApr` | Botón clásico | X `232` · Y `200` · Width `208` · Height `38` · Radius (las 4) `8` · Fill `ThemeDanger` · Color `ThemeAccentInk` · Text `"Sí, rechazar"` · Size `9.5` · FontWeight `Semibold` · **Visible `ctxConfirmApr = "rechazar"`** · **DisplayMode `If(IsBlank(Trim(inpMotivoApr.Text)), DisplayMode.Disabled, DisplayMode.Edit)`** · OnSelect: **§12** |

**`lblConfirmTextoApr.Text`:**

```powerfx
If(
    ctxConfirmApr = "aprobar",
    "Vas a aprobar «" & galBandejaApr.Selected.RequestType & "»"
    & If(IsBlank(ctxBagApr), "", " de la tula " & ctxBagApr.BagID)
    & If(
        galBandejaApr.Selected.RequestType = "Tula con diferencia"
        || galBandejaApr.Selected.RequestType = "Tula recontada sin declaracion",
        ". " & lblAvisoAsientosApr.Text,
        ". Esta solicitud no genera asientos contables."
    ),
    "Vas a rechazar «" & galBandejaApr.Selected.RequestType & "»"
    & If(IsBlank(ctxBagApr), "", " de la tula " & ctxBagApr.BagID)
    & ". La acción revierte lo que la solicitud haya dejado en curso."
)
```

---

## 11. `btnConfirmarAprobarApr.OnSelect` — COMPLETO

Orden no negociable: **(1)** revalidar estado y cuatro ojos con lookup FRESCO →
**(2)** patchear la SOLICITUD → **(3)** solo si el (2) tuvo éxito, ejecutar los
efectos, dentro de su propio `IfError` → **(4)** apagar el overlay SIEMPRE.

```powerfx
UpdateContext({ctxCargandoApr: true, ctxResueltaApr: false});

// (1) Revalidación con registro FRESCO (anti doble aprobación + cuatro ojos)
UpdateContext(
    {
        ctxFrescaApr:
            LookUp(
                'CH-Tbl-ApprovalRequests',
                'CH-Tbl-ApprovalRequest' = galBandejaApr.Selected.'CH-Tbl-ApprovalRequest'
            )
    }
);

If(
    IsBlank(ctxFrescaApr),
    Notify("La solicitud ya no existe. Actualizá la bandeja.", NotificationType.Warning),
    !IsBlank(ctxFrescaApr.ApprovalStatus),
    Notify(
        "Esta solicitud ya fue resuelta (" & Text(ctxFrescaApr.ApprovalStatus) & ") por "
        & Coalesce(ctxFrescaApr.SolvedBy.'Full Name', "otro supervisor") & ". No se ejecutó nada.",
        NotificationType.Warning
    ),
    ctxFrescaApr.'Created By'.User = UsuarioActual.User,
    Notify(
        "Cuatro ojos: no podés aprobar una solicitud creada por vos.",
        NotificationType.Error
    ),

    // (2) La SOLICITUD primero. Si esto falla, no se toca nada más.
    IfError(
        Patch(
            'CH-Tbl-ApprovalRequests',
            ctxFrescaApr,
            {
                ApprovalStatus: 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
                SolvedBy: UsuarioActual,
                ResolutionDatetime: Now()
            }
        );
        UpdateContext({ctxResueltaApr: true}),
        UpdateContext({ctxResueltaApr: false});
        Notify("No se pudo registrar la aprobación: " & FirstError.Message, NotificationType.Error)
    )
);

// (3) Efectos — SOLO si la solicitud quedó marcada Aprobado
If(
    ctxResueltaApr,
    IfError(
        Switch(
            ctxFrescaApr.RequestType,

            // ── 1) Remapear tula: mover a otra planilla y restituir su estado previo
            "Remapear tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    Planilla: LookUp(
                        'CH-Tbl-Receptions',
                        'CH-Tbl-Receptions' = IfError(GUID(Text(ParseJSON(ctxResApr).ReceiptGUID)), Blank())
                    ),
                    EstadoPrevio: LookUp(
                        Choices([@'CH-Tbl-Bags'].BagStatus),
                        Text(Value) = Text(ParseJSON(ctxResApr).BagPreviousState)
                    )
                },
                If(
                    IsBlank(Tula) || IsBlank(Planilla),
                    Notify("No se encontró la tula o la planilla destino.", NotificationType.Error),
                    // guarda: si el label no resolvió, NO se escribe BagStatus
                    IsBlank(EstadoPrevio),
                    Patch('CH-Tbl-Bags', Tula, {ReceptionID: Planilla});
                    Notify(
                        "Tula remapeada. El estado previo no pudo resolverse — revisalo a mano.",
                        NotificationType.Warning
                    ),
                    Patch('CH-Tbl-Bags', Tula, {ReceptionID: Planilla, BagStatus: EstadoPrevio.Value});
                    Notify("Tula remapeada.", NotificationType.Success)
                )
            ),

            // ── 2) Agregar tula: materializar la tula pendiente, con dup-check
            "Agregar tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    NumNuevo: Text(ParseJSON(ctxResApr).BagID),
                    LoteGuid: IfError(GUID(Text(ParseJSON(ctxResApr).ReceptionBatchGUID)), Blank())
                },
                If(
                    IsBlank(Tula),
                    Notify("La tula pendiente ya no existe.", NotificationType.Error),
                    !IsBlank(
                        LookUp(
                            'CH-Tbl-Bags',
                            ReceptionBatchID.'CH-Tbl-ReceptionBatch' = LoteGuid
                            && BagID = NumNuevo
                            && 'CH-Tbl-Bags' <> ctxBagGuidApr
                        )
                    ),
                    Notify(
                        "Ese número de tula ya existe en otra planilla del mismo lote. La solicitud quedó aprobada: corregí el número y volvé a intentar el alta.",
                        NotificationType.Error
                    ),
                    Patch('CH-Tbl-Bags', Tula, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'});
                    Notify("Tula dada de alta en bóveda.", NotificationType.Success)
                )
            ),

            // ── 3) Eliminar tula: baja efectiva
            "Eliminar tula",
            With(
                { Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr) },
                If(
                    IsBlank(Tula),
                    Notify("La tula ya no existe.", NotificationType.Warning),
                    Remove('CH-Tbl-Bags', Tula);
                    Notify("Tula eliminada.", NotificationType.Success)
                )
            ),

            // ── 4) Modificar numero de planilla: dup-check en el mismo lote
            "Modificar numero de planilla",
            With(
                {
                    Planilla: LookUp(
                        'CH-Tbl-Receptions',
                        'CH-Tbl-Receptions' = IfError(GUID(Text(ParseJSON(ctxResApr).ReceiptGUID)), Blank())
                    ),
                    NumNuevo: Text(ParseJSON(ctxResApr).NewReceiptID)
                },
                If(
                    IsBlank(Planilla),
                    Notify("No se encontró la planilla.", NotificationType.Error),
                    !IsBlank(
                        LookUp(
                            'CH-Tbl-Receptions',
                            ReceptionBatch.'CH-Tbl-ReceptionBatch' = Planilla.ReceptionBatch.'CH-Tbl-ReceptionBatch'
                            && ReceiptID = NumNuevo
                        )
                    ),
                    Notify(
                        "Ese número de planilla ya existe en el mismo lote de recepción.",
                        NotificationType.Error
                    ),
                    Patch('CH-Tbl-Receptions', Planilla, {ReceiptID: NumNuevo});
                    Notify("Número de planilla actualizado.", NotificationType.Success)
                )
            ),

            // ── 5) Editar tula: renombrar con dup-check
            "Editar tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    NumNuevo: Text(ParseJSON(ctxResApr).BagID),
                    LoteGuid: IfError(GUID(Text(ParseJSON(ctxResApr).ReceptionBatchGUID)), Blank())
                },
                If(
                    IsBlank(Tula),
                    Notify("La tula ya no existe.", NotificationType.Error),
                    !IsBlank(
                        LookUp(
                            'CH-Tbl-Bags',
                            ReceptionBatchID.'CH-Tbl-ReceptionBatch' = LoteGuid
                            && BagID = NumNuevo
                            && 'CH-Tbl-Bags' <> ctxBagGuidApr
                        )
                    ),
                    Notify(
                        "Ese número de tula ya existe en el mismo lote de recepción.",
                        NotificationType.Error
                    ),
                    Patch(
                        'CH-Tbl-Bags',
                        Tula,
                        {
                            Name: NumNuevo,
                            BagID: NumNuevo,
                            BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'
                        }
                    );
                    Notify("Número de tula actualizado.", NotificationType.Success)
                )
            ),

            // ── 6) Crear sesion de recuento: confirmar la sesión
            "Crear sesion de recuento",
            With(
                {
                    Ses: LookUp(
                        'CH-Tbl-RecountingSessions',
                        'CH-Tbl-RecountingSession' = IfError(GUID(Text(ParseJSON(ctxResApr).RecountingSessionGUID)), Blank())
                    )
                },
                If(
                    IsBlank(Ses),
                    Notify("La sesión de recuento ya no existe.", NotificationType.Error),
                    Patch('CH-Tbl-RecountingSessions', Ses, {Confirmed: true, ConfirmedBy: UsuarioActual});
                    Notify("Sesión de recuento confirmada.", NotificationType.Success)
                )
            ),

            // ── 7) Primera tula en planilla: COMPLETAR lo que el productor dejó a medias.
            //     El productor ya dejó BagStatus + RecountingSession (Screen1.fx.yaml
            //     L8789-8798) — reescribirlos acá es idempotente. Lo que aporta esta
            //     rama son las DOS líneas que el productor dejó COMENTADAS (L8797 y
            //     L8800-8801): BagDeclarationID y el back-link en DeclaredBags.
            "Primera tula en planilla",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    Ses: LookUp(
                        'CH-Tbl-RecountingSessions',
                        'CH-Tbl-RecountingSession' = IfError(GUID(Text(ParseJSON(ctxResApr).RecountingSessionGUID)), Blank())
                    ),
                    Declarada: LookUp(
                        'CH-Tbl-DeclaredBags',
                        'CH-Tbl-DeclaredBags' = IfError(GUID(Text(ParseJSON(ctxResApr).DeclarationBagGUID)), Blank())
                    )
                },
                If(
                    IsBlank(Tula) || IsBlank(Ses),
                    Notify("Falta la tula o la sesión de recuento.", NotificationType.Error),
                    // guarda: no se escribe BagDeclarationID en blanco
                    IsBlank(Declarada),
                    Patch(
                        'CH-Tbl-Bags',
                        Tula,
                        {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero', RecountingSession: Ses}
                    );
                    Notify(
                        "Tula asignada al cajero. No se pudo enlazar la bolsa declarada — revisalo a mano.",
                        NotificationType.Warning
                    ),
                    Patch(
                        'CH-Tbl-Bags',
                        Tula,
                        {
                            BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',
                            RecountingSession: Ses,
                            BagDeclarationID: Declarada
                        }
                    );
                    Patch('CH-Tbl-DeclaredBags', Declarada, {BagID: Tula});
                    Notify("Tula asignada al cajero y enlazada a su declaración.", NotificationType.Success)
                )
            ),

            // ── 8) Tula sin declaracion: asignar al cajero + abrir detalle de sesión
            "Tula sin declaracion",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    Ses: LookUp(
                        'CH-Tbl-RecountingSessions',
                        'CH-Tbl-RecountingSession' = IfError(GUID(Text(ParseJSON(ctxResApr).RecountingSessionGUID)), Blank())
                    )
                },
                If(
                    IsBlank(Tula) || IsBlank(Ses),
                    Notify("Falta la tula o la sesión de recuento.", NotificationType.Error),
                    Patch(
                        'CH-Tbl-Bags',
                        Tula,
                        {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero', RecountingSession: Ses}
                    );
                    Patch(
                        'CH-Tbl-RecountingSessionDetails',
                        Defaults('CH-Tbl-RecountingSessionDetails'),
                        {RecountingSessionId: Ses, BagId: Tula, SupervisorId: UsuarioActual}
                    );
                    Notify("Tula sin declaración asignada al cajero.", NotificationType.Success)
                )
            ),

            // ── 9) Tula con diferencia: asientos SEPARADOS por componente
            "Tula con diferencia",
            If(
                IsBlank(ctxBagApr),
                Notify("No se encontró la tula de la solicitud.", NotificationType.Error),
                Clear(colTxApr);

                // 9.a) Depósitos RECONTADOS (join LOCAL contra colDecApr — cero fetch por fila)
                ForAll(
                    colRecApr As R,
                    With(
                        {
                            D: LookUp(
                                colDecApr,
                                RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting' = R.'CH-Tbl-DepositsRecounting'
                            )
                        },
                        If(
                            IsBlank(D),
                            // recontado SIN declarado → Sobrante CON cuenta y CON el recuento enlazado
                            Collect(
                                colTxApr,
                                {
                                    AccountNumber: R.AccountNumber,
                                    Currency: R.Currency,
                                    CashAmount: Coalesce(R.CashAmount, 0),
                                    BACLocalCheckAmount: Coalesce(R.BACLocalCheckAmount, 0),
                                    OtherLocalCheckAmount: Coalesce(R.OtherLocaCheckAmount, 0),
                                    TransactionReference: "Sobrante sin declarar tula " & ctxBagApr.BagID,
                                    TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Sobrante,
                                    TransactionNumber: R.TransactionNumber,
                                    TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                                    RelatedDepositRecounting: R
                                }
                            ),
                            // enlazado → diferencia POR COMPONENTE, nunca por el neto
                            With(
                                {
                                    dCash: Coalesce(R.CashAmount, 0) - Coalesce(D.CashAmount, 0),
                                    dBac: Coalesce(R.BACLocalCheckAmount, 0) - Coalesce(D.BACLocalCheckAmount, 0),
                                    dOtro: Coalesce(R.OtherLocaCheckAmount, 0) - Coalesce(D.OtherLocalCheckAmount, 0)
                                },
                                If(
                                    Max(dCash, 0) + Max(dBac, 0) + Max(dOtro, 0) > 0,
                                    Collect(
                                        colTxApr,
                                        {
                                            AccountNumber: D.AccountNumber,
                                            Currency: R.Currency,
                                            CashAmount: Max(dCash, 0),
                                            BACLocalCheckAmount: Max(dBac, 0),
                                            OtherLocalCheckAmount: Max(dOtro, 0),
                                            TransactionReference: "Sobrante tula " & ctxBagApr.BagID,
                                            TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Sobrante,
                                            TransactionNumber: R.TransactionNumber,
                                            TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                                            RelatedDepositRecounting: R
                                        }
                                    )
                                );
                                If(
                                    Abs(Min(dCash, 0)) + Abs(Min(dBac, 0)) + Abs(Min(dOtro, 0)) > 0,
                                    Collect(
                                        colTxApr,
                                        {
                                            AccountNumber: D.AccountNumber,
                                            Currency: R.Currency,
                                            CashAmount: Abs(Min(dCash, 0)),
                                            BACLocalCheckAmount: Abs(Min(dBac, 0)),
                                            OtherLocalCheckAmount: Abs(Min(dOtro, 0)),
                                            TransactionReference: "Faltante tula " & ctxBagApr.BagID,
                                            TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Faltante,
                                            TransactionNumber: R.TransactionNumber,
                                            TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                                            RelatedDepositRecounting: R
                                        }
                                    )
                                );
                                // el depósito recontado en sí
                                Collect(
                                    colTxApr,
                                    {
                                        AccountNumber: D.AccountNumber,
                                        Currency: R.Currency,
                                        CashAmount: Coalesce(R.CashAmount, 0),
                                        BACLocalCheckAmount: Coalesce(R.BACLocalCheckAmount, 0),
                                        OtherLocalCheckAmount: Coalesce(R.OtherLocaCheckAmount, 0),
                                        TransactionReference: D.DepositReference,
                                        TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Deposito,
                                        TransactionNumber: R.TransactionNumber,
                                        TransactionProcessingType:
                                            If(
                                                R.BACLocalCheckAmount > 0 || R.OtherLocaCheckAmount > 0,
                                                'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                                                'TransactionProcessingType (CH-Tbl-Transactions)'.Batch
                                            ),
                                        RelatedDepositRecounting: R
                                    }
                                )
                            )
                        )
                    )
                );

                // 9.b) Declarados que NADIE recontó → Faltante + su contrapartida de depósito
                ForAll(
                    Filter(colDecApr, IsBlank(RelatedRecountedDeposit)) As D2,
                    Collect(
                        colTxApr,
                        {
                            AccountNumber: D2.AccountNumber,
                            Currency: D2.Currency,
                            CashAmount: Coalesce(D2.CashAmount, 0),
                            BACLocalCheckAmount: Coalesce(D2.BACLocalCheckAmount, 0),
                            OtherLocalCheckAmount: Coalesce(D2.OtherLocalCheckAmount, 0),
                            TransactionReference: "Faltante tula " & ctxBagApr.BagID,
                            TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Faltante,
                            TransactionNumber: D2.TransactionNo,
                            TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                            RelatedDepositRecounting: If(false, First(colRecApr))
                        }
                    );
                    Collect(
                        colTxApr,
                        {
                            AccountNumber: D2.AccountNumber,
                            Currency: D2.Currency,
                            CashAmount: Coalesce(D2.CashAmount, 0),
                            BACLocalCheckAmount: Coalesce(D2.BACLocalCheckAmount, 0),
                            OtherLocalCheckAmount: Coalesce(D2.OtherLocalCheckAmount, 0),
                            TransactionReference: D2.DepositReference,
                            TransactionType: 'TransactionType (CH-Tbl-Transactions)'.Deposito,
                            TransactionNumber: D2.TransactionNo,
                            TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                            RelatedDepositRecounting: If(false, First(colRecApr))
                        }
                    )
                );

                // 9.c) Alta MASIVA de asientos: UNA llamada, no un Patch por fila
                Patch(
                    'CH-Tbl-Transactions',
                    ForAll(
                        colTxApr As T,
                        Patch(
                            Defaults('CH-Tbl-Transactions'),
                            {
                                AccountNumber: T.AccountNumber,
                                Currency: T.Currency,
                                CashAmount: T.CashAmount,
                                BACLocalCheckAmount: T.BACLocalCheckAmount,
                                OtherLocalCheckAmount: T.OtherLocalCheckAmount,
                                TransactionReference: T.TransactionReference,
                                TransactionType: T.TransactionType,
                                TransactionNumber: T.TransactionNumber,
                                TransactionProcessingType: T.TransactionProcessingType,
                                RelatedDepositRecounting: T.RelatedDepositRecounting
                            }
                        )
                    )
                );

                // 9.d) Cheques de la tula a Recontado — también en UNA llamada
                ClearCollect(colChkApr, Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = ctxBagGuidApr));
                If(
                    CountRows(colChkApr) > 0,
                    Patch(
                        'CH-Tbl-Checks',
                        ForAll(colChkApr As C, Patch(C, {CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.Recontado}))
                    )
                );

                // 9.e) Tula recontada
                Patch('CH-Tbl-Bags', ctxBagApr, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.Recontada});

                // 9.f) Detalle de sesión — LookUp desambiguado (PK a PK) y con guarda
                With(
                    {
                        Det: LookUp(
                            'CH-Tbl-RecountingSessionDetails',
                            BagId.'CH-Tbl-Bags' = ctxBagGuidApr
                            && RecountingSessionId.'CH-Tbl-RecountingSession' = ctxBagApr.RecountingSession.'CH-Tbl-RecountingSession'
                        )
                    },
                    If(
                        IsBlank(Det),
                        Notify(
                            "Asientos generados, pero la tula no tiene detalle de sesión: cerralo a mano.",
                            NotificationType.Warning
                        ),
                        Patch(
                            'CH-Tbl-RecountingSessionDetails',
                            Det,
                            {RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.'Cerrado con diferencias'}
                        )
                    )
                );
                Notify(
                    "Recuento cerrado con diferencias: " & CountRows(colTxApr) & " asientos generados.",
                    NotificationType.Success
                )
            ),

            // ── 10) Tula recontada sin declaracion: un asiento «Sin declaracion» por depósito
            "Tula recontada sin declaracion",
            If(
                IsBlank(ctxBagApr),
                Notify("No se encontró la tula de la solicitud.", NotificationType.Error),
                If(
                    CountRows(colRecApr) = 0,
                    Notify("La tula no tiene depósitos recontados.", NotificationType.Warning),
                    Patch(
                        'CH-Tbl-Transactions',
                        ForAll(
                            colRecApr As R,
                            Patch(
                                Defaults('CH-Tbl-Transactions'),
                                {
                                    AccountNumber: R.AccountNumber,
                                    Currency: R.Currency,
                                    CashAmount: Coalesce(R.CashAmount, 0),
                                    BACLocalCheckAmount: Coalesce(R.BACLocalCheckAmount, 0),
                                    OtherLocalCheckAmount: Coalesce(R.OtherLocaCheckAmount, 0),
                                    TransactionReference: "Recuento sin declaracion tula " & ctxBagApr.BagID,
                                    TransactionType: 'TransactionType (CH-Tbl-Transactions)'.'Sin declaracion',
                                    TransactionNumber: R.TransactionNumber,
                                    TransactionProcessingType: 'TransactionProcessingType (CH-Tbl-Transactions)'.Manual,
                                    RelatedDepositRecounting: R
                                }
                            )
                        )
                    )
                );
                ClearCollect(colChkApr, Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = ctxBagGuidApr));
                If(
                    CountRows(colChkApr) > 0,
                    Patch(
                        'CH-Tbl-Checks',
                        ForAll(colChkApr As C, Patch(C, {CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.Recontado}))
                    )
                );
                Patch('CH-Tbl-Bags', ctxBagApr, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.Recontada});
                With(
                    {
                        Det: LookUp(
                            'CH-Tbl-RecountingSessionDetails',
                            BagId.'CH-Tbl-Bags' = ctxBagGuidApr
                            && RecountingSessionId.'CH-Tbl-RecountingSession' = ctxBagApr.RecountingSession.'CH-Tbl-RecountingSession'
                        )
                    },
                    If(
                        IsBlank(Det),
                        Notify(
                            "Asientos generados, pero la tula no tiene detalle de sesión: cerralo a mano.",
                            NotificationType.Warning
                        ),
                        Patch(
                            'CH-Tbl-RecountingSessionDetails',
                            Det,
                            {RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.Cerrado}
                        )
                    )
                );
                Notify("Recuento sin declaración cerrado.", NotificationType.Success)
            ),

            // ── por defecto: tipo desconocido (contrato nuevo sin soporte en la app)
            Notify(
                "Tipo de solicitud no soportado: «" & ctxFrescaApr.RequestType
                & "». La solicitud quedó APROBADA pero no se ejecutó ningún efecto. Avisá a soporte.",
                NotificationType.Error
            )
        ),

        // rama de error de la cadena de EFECTOS
        Notify(
            "La solicitud quedó APROBADA pero los efectos fallaron: " & FirstError.Message
            & " — revisá la tula antes de reintentar.",
            NotificationType.Error
        )
    )
);

// (4) Cierre — SIEMPRE fuera de la rama de éxito
Reset(inpMotivoApr);
UpdateContext({ctxCargandoApr: false, ctxConfirmApr: ""});
Refresh('CH-Tbl-ApprovalRequests');
Select(btnRecargarDetalleApr)
```

> **Qué se arregló acá, punto por punto**
> - **Orden invertido**: la vieja ejecutaba los efectos y recién después
>   patcheaba la solicitud → dos supervisores simultáneos duplicaban asientos.
>   Acá la solicitud se marca PRIMERO; los efectos solo corren si esa escritura
>   ganó.
> - **`Switch` con rama por defecto**: un `RequestType` nuevo ya no pasa mudo.
> - **`Max`/`Abs(Min(...))` por componente**: la vieja hacía
>   `Abs(recontado − declarado)` sobre el neto, con lo cual +200.000 de efectivo
>   y −100.000 de cheques se compensaban a +100.000 y desaparecía una
>   discrepancia. Acá salen DOS asientos: Sobrante(200.000 efectivo) y
>   Faltante(100.000 BAC) — y son **los mismos** que muestra §8, porque §6.a
>   calcula con la misma unidad (depósito × componente).
> - **Recontado sin declarar**: lleva `AccountNumber` y
>   `RelatedDepositRecounting`, y es `Sobrante` — la vieja ponía `Deposito` con
>   la referencia "Sobrante" y **sin cuenta**.
> - **Join local PK a PK**: `LookUp(colDecApr, RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting' = R.'CH-Tbl-DepositsRecounting')`
>   en vez de `R.RelatedDeclaredDeposit.CashAmount` (que dispara un fetch por fila).
> - **Patch masivo**: `Patch(ds, ForAll(...))` — una llamada, no N.
> - **Guardas `IsBlank`** antes de cada `Patch` sobre un `LookUp` (la vieja
>   patcheaba `Blank()` y borraba datos).
> - **`Blank()` TIPADO en columnas de lookup**: los asientos de 9.b no tienen
>   recuento asociado, y ahí va `RelatedDepositRecounting: If(false, First(colRecApr))`,
>   no `Blank()` pelado. Un `Blank()` sin tipo en una columna `lookup` deja el
>   esquema de `colTxApr` sin inferir y hace fallar el `Patch` masivo de 9.c
>   cuando la MISMA columna sí trae registro en las otras ramas.
>
> **Delegación de este bloque**: todos los `LookUp` usan **`Lookup.'PK' = X`** —
> nunca `record = record`. Las dos únicas dependencias de backend:
> `LookUp('CH-Tbl-Bags', ReceptionBatchID.'CH-Tbl-ReceptionBatch' = LoteGuid && …)`
> es **delegable SOLO con el espejo B-2** (`sanic_receptionbatchid` en Bags,
> 00 §2). **Fallback sin backend** (marcar como *no delegable — temporal*, trunca
> a 500):
> ```powerfx
> LookUp('CH-Tbl-Bags',
>     ReceptionID.ReceptionBatch.'CH-Tbl-ReceptionBatch' = LoteGuid
>     && BagID = NumNuevo && 'CH-Tbl-Bags' <> ctxBagGuidApr)
> ```
> El dup-check de PLANILLA (`ReceptionBatch.'CH-Tbl-ReceptionBatch' = …`) ya es
> delegable hoy: `ReceptionBatch` es lookup directo de `CH-Tbl-Receptions`, un
> solo salto — no necesita backend nuevo.
> `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = ctxBagGuidApr)` depende del espejo
> **B-1** (`sanic_bag`, columna VIVA en el entorno, ausente del repo).
> **Fallback**: `Filter('CH-Tbl-Checks', RelatedReCounterDepositID.BagID.'CH-Tbl-Bags' = ctxBagGuidApr)`
> — 2 saltos, **no delegable**.

---

## 12. `btnConfirmarRechazarApr.OnSelect` — COMPLETO

Mismo esqueleto que §11 (revalidar → resolver → efectos → cerrar). **Los 10
tipos están cubiertos**: la app vieja no tenía rama para `Tula sin declaracion`
ni para `Editar tula`, y esa última deja la tula clavada en *Pendiente de
revisión por supervisor - esclusa* para siempre.

```powerfx
UpdateContext({ctxCargandoApr: true, ctxResueltaApr: false});

// (1) Revalidación con registro FRESCO
UpdateContext(
    {
        ctxFrescaApr:
            LookUp(
                'CH-Tbl-ApprovalRequests',
                'CH-Tbl-ApprovalRequest' = galBandejaApr.Selected.'CH-Tbl-ApprovalRequest'
            )
    }
);

If(
    IsBlank(ctxFrescaApr),
    Notify("La solicitud ya no existe. Actualizá la bandeja.", NotificationType.Warning),
    !IsBlank(ctxFrescaApr.ApprovalStatus),
    Notify(
        "Esta solicitud ya fue resuelta (" & Text(ctxFrescaApr.ApprovalStatus) & ") por "
        & Coalesce(ctxFrescaApr.SolvedBy.'Full Name', "otro supervisor") & ". No se ejecutó nada.",
        NotificationType.Warning
    ),
    ctxFrescaApr.'Created By'.User = UsuarioActual.User,
    Notify("Cuatro ojos: no podés rechazar una solicitud creada por vos.", NotificationType.Error),

    // (2) La SOLICITUD primero — con motivo obligatorio
    IfError(
        Patch(
            'CH-Tbl-ApprovalRequests',
            ctxFrescaApr,
            {
                ApprovalStatus: 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
                SolvedBy: UsuarioActual,
                ResolutionDatetime: Now(),
                StatusReason: Trim(inpMotivoApr.Text)
            }
        );
        UpdateContext({ctxResueltaApr: true}),
        UpdateContext({ctxResueltaApr: false});
        Notify("No se pudo registrar el rechazo: " & FirstError.Message, NotificationType.Error)
    )
);

// (3) Reversión — SOLO si la solicitud quedó marcada Rechazado
If(
    ctxResueltaApr,
    IfError(
        Switch(
            ctxFrescaApr.RequestType,

            // ── 1) Remapear tula: restituir el estado previo (la tula NO se movió)
            "Remapear tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    EstadoPrevio: LookUp(
                        Choices([@'CH-Tbl-Bags'].BagStatus),
                        Text(Value) = Text(ParseJSON(ctxResApr).BagPreviousState)
                    )
                },
                If(
                    IsBlank(Tula), Notify("La tula ya no existe.", NotificationType.Warning),
                    IsBlank(EstadoPrevio),
                    Notify(
                        "Rechazada, pero el estado previo no pudo resolverse: la tula quedó en «"
                        & Text(Tula.BagStatus) & "». Corregilo a mano.",
                        NotificationType.Warning
                    ),
                    Patch('CH-Tbl-Bags', Tula, {BagStatus: EstadoPrevio.Value});
                    Notify("Remapeo rechazado, tula restituida.", NotificationType.Success)
                )
            ),

            // ── 2) Agregar tula: borrar la tula pendiente que Reception creó
            "Agregar tula",
            With(
                { Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr) },
                If(
                    IsBlank(Tula),
                    Notify("La tula pendiente ya no existe.", NotificationType.Warning),
                    Remove('CH-Tbl-Bags', Tula);
                    Notify("Alta rechazada: la tula pendiente fue eliminada.", NotificationType.Success)
                )
            ),

            // ── 3) Eliminar tula: restituir el estado previo
            "Eliminar tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    EstadoPrevio: LookUp(
                        Choices([@'CH-Tbl-Bags'].BagStatus),
                        Text(Value) = Text(ParseJSON(ctxResApr).BagPreviousState)
                    )
                },
                If(
                    IsBlank(Tula), Notify("La tula ya no existe.", NotificationType.Warning),
                    IsBlank(EstadoPrevio),
                    Notify(
                        "Rechazada, pero el estado previo no pudo resolverse: la tula quedó en «"
                        & Text(Tula.BagStatus) & "». Corregilo a mano.",
                        NotificationType.Warning
                    ),
                    Patch('CH-Tbl-Bags', Tula, {BagStatus: EstadoPrevio.Value});
                    Notify("Baja rechazada, tula restituida.", NotificationType.Success)
                )
            ),

            // ── 4) Modificar numero de planilla: nada que revertir
            "Modificar numero de planilla",
            Notify("Cambio de número de planilla rechazado. Nada que revertir.", NotificationType.Success),

            // ── 5) Editar tula: sacar la tula del limbo (el productor la dejó en revisión)
            "Editar tula",
            With(
                {
                    Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr),
                    EstadoPrevio: LookUp(
                        Choices([@'CH-Tbl-Bags'].BagStatus),
                        Text(Value) = Text(ParseJSON(ctxResApr).BagPreviousState)
                    )
                },
                If(
                    IsBlank(Tula),
                    Notify("La tula ya no existe.", NotificationType.Warning),
                    !IsBlank(EstadoPrevio),
                    Patch('CH-Tbl-Bags', Tula, {BagStatus: EstadoPrevio.Value});
                    Notify("Edición rechazada, tula restituida.", NotificationType.Success),
                    // sin BagPreviousState en el contrato → estado terminal seguro + aviso
                    Patch('CH-Tbl-Bags', Tula, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'});
                    Notify(
                        "Edición rechazada. El contrato de «Editar tula» no trae BagPreviousState: la tula se devolvió a «Almacenada en boveda». Verificá que sea el estado correcto.",
                        NotificationType.Warning
                    )
                )
            ),

            // ── 6) Crear sesion de recuento: eliminar la sesión creada en espera
            "Crear sesion de recuento",
            With(
                {
                    Ses: LookUp(
                        'CH-Tbl-RecountingSessions',
                        'CH-Tbl-RecountingSession' = IfError(GUID(Text(ParseJSON(ctxResApr).RecountingSessionGUID)), Blank())
                    )
                },
                If(
                    IsBlank(Ses),
                    Notify("La sesión ya no existe.", NotificationType.Warning),
                    Remove('CH-Tbl-RecountingSessions', Ses);
                    Notify("Sesión de recuento rechazada y eliminada.", NotificationType.Success)
                )
            ),

            // ── 7) Primera tula en planilla: el productor SÍ pre-patcheó la tula
            //     (Recounting Screen1.fx.yaml L8789-8798: BagStatus + RecountingSession
            //      ANTES de crear la solicitud) → hay que devolverla a bóveda y
            //      DESENLAZAR la sesión que nunca se aprobó.
            "Primera tula en planilla",
            With(
                { Tula: LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxBagGuidApr) },
                If(
                    IsBlank(Tula),
                    Notify("La tula ya no existe.", NotificationType.Warning),
                    Patch(
                        'CH-Tbl-Bags',
                        Tula,
                        {
                            BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',
                            RecountingSession: Blank()
                        }
                    );
                    Notify(
                        "Asignación rechazada: la tula vuelve a bóveda y se desenlaza de la sesión. El contrato no trae BagPreviousState — verificá que «Almacenada en boveda» sea el estado correcto.",
                        NotificationType.Warning
                    )
                )
            ),

            // ── 8) Tula sin declaracion: acá el productor NO pre-patchea
            //     (Screen1.fx.yaml L9192-9207: el Patch a Bags está COMENTADO) →
            //     no-op deliberado. Rama que la app vieja NO tenía: sin ella la
            //     solicitud quedaba sin resolver.
            "Tula sin declaracion",
            Notify(
                "Asignación rechazada. La tula sigue disponible para volver a asignarse.",
                NotificationType.Success
            ),

            // ── 9) Tula con diferencia: devolver la tula al cajero para recontar
            "Tula con diferencia",
            If(
                IsBlank(ctxBagApr),
                Notify("No se encontró la tula de la solicitud.", NotificationType.Warning),
                Patch('CH-Tbl-Bags', ctxBagApr, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'});
                Notify(
                    "Recuento rechazado: la tula vuelve al cajero. NO se generó ningún asiento.",
                    NotificationType.Success
                )
            ),

            // ── 10) Tula recontada sin declaracion: ídem
            "Tula recontada sin declaracion",
            If(
                IsBlank(ctxBagApr),
                Notify("No se encontró la tula de la solicitud.", NotificationType.Warning),
                Patch('CH-Tbl-Bags', ctxBagApr, {BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'});
                Notify(
                    "Recuento rechazado: la tula vuelve al cajero. NO se generó ningún asiento.",
                    NotificationType.Success
                )
            ),

            // ── por defecto
            Notify(
                "Tipo de solicitud no soportado: «" & ctxFrescaApr.RequestType
                & "». Quedó RECHAZADA pero no se revirtió nada. Avisá a soporte.",
                NotificationType.Error
            )
        ),

        // rama de error de la cadena de REVERSIÓN
        Notify(
            "La solicitud quedó RECHAZADA pero la reversión falló: " & FirstError.Message
            & " — revisá el estado de la tula.",
            NotificationType.Error
        )
    )
);

// (4) Cierre — SIEMPRE fuera de la rama de éxito
Reset(inpMotivoApr);
UpdateContext({ctxCargandoApr: false, ctxConfirmApr: ""});
Refresh('CH-Tbl-ApprovalRequests');
Select(btnRecargarDetalleApr)
```

---

## 13. Overlay de carga

| Control | Tipo | Propiedades |
|---|---|---|
| `cntCargandoApr` | Contenedor manual (receta 01 §7.6) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Fill `RGBA(0,0,0,0.35)` · **Visible `ctxCargandoApr`** · **último en el z-order de la pantalla** |
| `lblCargandoApr` | Etiqueta clásica | X `0` · Y `cntCargandoApr.Height/2 - 12` · Width `cntCargandoApr.Width` · Height `24` · Align `Align.Center` · Size `12` · Color `Color.White` · Text `"Procesando la solicitud..."` |

> El apagado (`ctxCargandoApr: false`) está en el paso (4) de §11 y §12, **fuera**
> de todo `IfError` y de toda rama de éxito: ningún camino de error deja el
> overlay pegado.

---

## 14. Camino B-4 (Custom API) — migración documentada

Todo §11/§12 es el flujo **client-side**, aceptable como transición: `IfError` +
revalidación de estado. Lo que NO da es **atomicidad** — si el paso 9.c crea 3 de
5 asientos y el 4º falla, la solicitud ya quedó Aprobada.

Con **B-4** (`sanic_CHResolveApprovalRequest`, plugin Pre-Op, 00 §2) todo eso pasa
al servidor en UNA transacción y el canvas queda así:

1. Agregar el datasource **Environment** (Datos → Agregar datos → Dataverse →
   *Environment*) — patrón NUEVO en este codebase (01 §2).
2. `btnConfirmarAprobarApr.OnSelect` completo se reduce a:

```powerfx
UpdateContext({ctxCargandoApr: true});
IfError(
    Environment.sanic_CHResolveApprovalRequest(
        {
            RequestId: Text(galBandejaApr.Selected.'CH-Tbl-ApprovalRequest'),
            Decision: "Approve",
            Reason: ""
        }
    );
    Notify("Solicitud aprobada.", NotificationType.Success),
    Notify(FirstError.Message, NotificationType.Error)
);
UpdateContext({ctxCargandoApr: false, ctxConfirmApr: ""});
Refresh('CH-Tbl-ApprovalRequests');
Select(btnRecargarDetalleApr)
```

3. `btnConfirmarRechazarApr` idem con `Decision: "Reject"` y
   `Reason: Trim(inpMotivoApr.Text)`.
4. El plugin queda como único dueño de: cuatro ojos, revalidación de estado,
   dup-checks y generación de asientos. La UI de §5–§10 **no cambia** — solo se
   reemplazan los dos `OnSelect`. El cuadro de diferencias sigue siendo la
   previsualización que el supervisor firma.

> Los nombres de los parámetros (`RequestId` / `Decision` / `Reason`) los fija la
> definición del Custom API; ajustalos a lo que registre B-4.
> **B-5** (`GuardSupervisorAccountingPlugin` + alternate key sobre
> `(RelatedDepositRecounting, TransactionType)`) es el complemento server-side:
> con él, un doble clic o un reintento no puede duplicar el asiento aunque la
> UI falle. Hasta que exista, la única defensa es el orden de §11.

---

## 15. Notas de la guía

**Dependencias de backend (con su fallback)**

| Ítem | Dónde se usa | Fallback sin backend |
|---|---|---|
| **B-1** `sanic_bag` en Checks (columna VIVA, falta en el repo) | §11 9.d y §11 caso 10: `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = ctxBagGuidApr)` | `Filter('CH-Tbl-Checks', RelatedReCounterDepositID.BagID.'CH-Tbl-Bags' = ctxBagGuidApr)` — 2 saltos, **no delegable** |
| **B-2** `sanic_receptionbatchid` en Bags (display `ReceptionBatchID` — confirmar en IntelliSense) | dup-checks de `Agregar tula` y `Editar tula` | dot-walk `ReceptionID.ReceptionBatch.'CH-Tbl-ReceptionBatch' = LoteGuid` — **no delegable, temporal** |
| **B-4** Custom API | §14 | §11/§12 client-side (lo escrito) |
| **B-5** guard + alternate key | protege §11 9.c | ninguno: la protección es el orden de §11 |
| **FSP `sanic_accountnumber`** — la columna tiene `<IsSecured>1</IsSecured>` en las **tres** tablas (`DepositsRecounting`, `DeclaredDeposits`, `Transactions`, verificado en `Entity.xml`) → hace falta un **Field Security Profile con Read+Update** para `CH-SR-CountingSupervisor` | `lblKvCuentaApr` (§7) y `AccountNumber` de TODOS los asientos de §11 9.a/9.b/caso 10 | **ninguno, y es el peor caso**: una columna asegurada sin FSP **no da error, devuelve Blank**. El detalle mostraría "—" y los asientos se escribirían con la cuenta VACÍA, en silencio. Es una rama que falla muda: hay que confirmar el FSP antes de publicar |
| **Rol** `prvRead sanic_CHTblClient` (00 §3.3) | `lblKvClienteApr` | el `Coalesce` cae a `RemitentName` |
| **Rol** C/R/W `sanic_CHTblRecountingSessionDetail` (00 §3.3) | §11 9.f y caso 8 | ninguno: sin el privilegio, esas ramas fallan y el `IfError` lo reporta |

**Divergencias declaradas vs mockup** (además del ledger 00 §4 — van propagadas a §4 del 00)

| # | En el mockup (`s-apr`) | En la app | Motivo |
|---|---|---|---|
| A-1 | `bmeta` de la fila: `"Tula 0412-07 · Ana Robles · Mesa 3"` | `Name & " · " & 'Created By'.'Full Name'` (ej. `AR-00000412 · Ana Robles`) | El número de tula vive DENTRO del JSON o detrás del lookup: mostrarlo por fila costaría un fetch por fila o rompería la delegación. El nombre del creador es el **nombre primario** del lookup (viene como anotación, sin request extra) |
| A-2 | Buscar: `"Tipo de solicitud o número de tula…"` | `"Tipo de solicitud o número de solicitud…"` — `StartsWith(RequestType)` \|\| `StartsWith(Name)` | Mismo motivo: `RequestResources` es un blob JSON. Una búsqueda real por número de tula exige un espejo `sanic_bagnumber` en `ApprovalRequest` → **propuesta B-6** |
| A-3 | La barra de filtros no tiene botón Actualizar | Se agrega `btnActualizarApr` | La bandeja no se auto-refresca cuando otro supervisor resuelve; el `sync` conserva el copy "Bandeja al día" como estado inicial |
| A-4 | Rechazar es un clic directo | Diálogo `cntConfirmApr` con **motivo obligatorio** → `StatusReason` | La nota de diseño del propio mockup pide "confirmación + revalidación de estado"; un rechazo sin motivo es un agujero de gobierno en un flujo de cuatro ojos |
| A-5 | El detalle no muestra el HTML de `RequestDetails` | Tampoco: se arma desde `RequestResources` + lookups (`lblDetResumenApr`) | `RequestDetails` trae `<style>` inline que la app vieja parcheaba con ~60 líneas de `Split`/`Substitute`. El mockup ya muestra campos derivados: se elimina el parser entero |
| A-6 | Tabla de diferencias con 3 filas fijas en C$ | Filas dinámicas **por componente × moneda** (`colDifApr`) | Una tula puede tener depósitos en NIO/USD/EUR; el agrupado por moneda blinda contra monedas nuevas |
| A-7 | `kv` con 3 filas (Cliente/Cuenta/Tula) | Las mismas 3 + `lblDetResumenApr` | Sin esa línea, 6 de los 10 tipos de solicitud (los que no tienen tula) mostrarían un detalle vacío |
| A-8 | Píldora de estado solo implícita | `pilFilaEstadoApr` visible solo en Histórico | En Pendientes el estado es siempre Blank: la píldora sería ruido |
| A-9 | La columna "Diferencia" es tinta plana; el color vive solo en el chip | Se colorea `ThemePos`/`ThemeNeg`/`ThemeWarn` igual que el chip | Redundancia deliberada: el color del monto y el del chip se validan mutuamente en la lectura previa a una firma contable |
| A-10 | El detalle no tiene control de recarga | `btnRecargarDetalleApr` ("↻", ghost, esquina superior derecha) | Es el motor de carga del detalle: canvas **no ejecuta `Select()` sobre un control invisible**, así que no puede esconderse |
| A-11 | El `when` de la fila existe en todo ancho | Se oculta con `scrAprobaciones.Width >= 1100` | Con la píldora del Histórico + tipos largos ("Tula recontada sin declaracion"), la fila de 380-500 px no entra; el sello siempre está en el `dsub` del detalle |
| A-12 | `.apr-grid` = 1,15 : 1 (`minmax(0,1.15fr) / minmax(0,1fr)`) | `FillPortions 5 / 4` = 1,25 : 1 | Manda el contrato de app (01 §8.6 fija 5/4 para el master-detail); la diferencia de 8 % es imperceptible y unifica el patrón con el resto de las pantallas |
| A-13 | Ninguna | `cntDetStaleApr` (aviso "el detalle no corresponde a la solicitud seleccionada") + firma bloqueada mientras está prendido | La galería puede cambiar `Selected` sin disparar su `OnSelect` (auto-selección, `Refresh` que saca la fila resuelta). Antes de firmar hay que garantizar que la previsualización es la de la solicitud elegida |

**Aproximaciones honestas**

- **Ni `Editar tula` ni `Primera tula en planilla` traen `BagPreviousState`** en
  su contrato, pero **los dos dejan la tula pre-patcheada** antes de pedir la
  aprobación: `Editar tula` en *Pendiente de revisión por supervisor - esclusa*
  (Reception `AdministracionDeLotes.fx.yaml` L13254-13258) y `Primera tula en
  planilla` en *Asignada a cajero* + `RecountingSession` seteada (Recounting
  `Screen1.fx.yaml` L8789-8798). Al rechazar, los dos se devuelven a *Almacenada
  en boveda* con `Notify` de advertencia — y `Primera tula` además **desenlaza la
  sesión** que nunca se aprobó. El fix correcto es que ambos productores incluyan
  la clave `BagPreviousState` (**propuesta B-7**); mientras tanto esto es mejor
  que la app vieja, que dejaba las dos tulas clavadas para siempre.
- `Tula sin declaracion` es el ÚNICO que **no revierte nada** al rechazar, y es
  deliberado: su `Patch` a `CH-Tbl-Bags` está **comentado** en el productor
  (`Screen1.fx.yaml` L9192-9207), así que la tula nunca cambió de estado. La
  rama existe igual — sin ella (como en la app vieja) la solicitud no se podía
  resolver.
- El contador de `pilBandejaCountApr` usa `galBandejaApr.AllItems`: refleja lo que
  hay en la bandeja (respeta la búsqueda), pero **`AllItems` solo devuelve las
  filas ya paginadas** — con más de una página de pendientes el número **miente
  hacia abajo** hasta que el usuario scrollea. El badge de `cmpNav` y el
  `segPendApr` usan en cambio el `CountRows(Filter(...))` delegable, que sí es el
  total. Los tres números pueden diferir legítimamente; el que manda para
  "cuánto falta firmar" es el del segmento.
- **No se usa un Temporizador** para autocurar el detalle: no está en el catálogo
  de tipos de 01 §0.3 y, sobre todo, un timer *ejecuta* la carga sin que nadie la
  pida — mientras que el centinela `ctxCargadoIdApr` **bloquea la firma** hasta
  que el operador confirma. Ante un detalle desfasado, bloquear es más seguro que
  recargar solo.

---

## 16. Checklist de salida

- [ ] Al entrar, la bandeja carga sola en **Pendientes**, ordenada por fecha
      descendente, y la **primera fila queda seleccionada** con su detalle
      cargado (auto-selección de galería clásica); el aviso `cntDetStaleApr`
      NO aparece.
- [ ] Escribir `Tula` en el buscador filtra sin warning de delegación; borrarlo
      devuelve todas las filas (`StartsWith(col, "")` = true).
- [ ] Cambiar a **Histórico** muestra resueltas con su píldora Aprobado/Rechazado
      y **oculta** los botones de acción; la píldora **nunca** se superpone al
      tipo ni al sello de tiempo, en ningún ancho de ventana.
- [ ] App Checker: **cero** warnings de delegación en `galBandejaApr.Items` y en
      los `Filter` de §6 (con B-1/B-2 presentes).
- [ ] Con una solicitud `Tula con diferencia` de prueba (+200.000 efectivo /
      −100.000 BAC): el cuadro muestra **3 filas** (Sobrante / Faltante / Cuadra)
      y el aviso nombra **los montos y componentes** ("un Sobrante de 200,000.00
      (Efectivo C$); un Faltante de 100,000.00 (Cheques BAC C$)").
- [ ] Con DOS depósitos opuestos en el MISMO componente (+200.000 y −200.000 de
      efectivo): la fila **no dice "Cuadra"** — muestra `+200,000.00 / −200,000.00`
      y el chip "Sobr. + Falt.". *(Es la prueba de que el preview no compensa.)*
- [ ] Al aprobar esa solicitud se crean **exactamente** los asientos del cuadro
      (uno Sobrante con el positivo por componente, uno Faltante con el valor
      absoluto del negativo) **+ el asiento Deposito por depósito** — verificado
      en `CH-Tbl-Transactions`. **Ningún asiento con neto compensado.**
- [ ] El asiento de un depósito recontado sin declarar tiene `AccountNumber`
      lleno, `RelatedDepositRecounting` apuntando al recuento y tipo **Sobrante**.
- [ ] Los montos se ven `C$ 0.00` en cero, nunca `C$ .00`; los sellos de tiempo
      usan **24 h** ("hoy 09:31", "ayer 17:20").
- [ ] Una solicitud creada por el propio usuario deja **ambos botones
      deshabilitados** y `lblCuatroOjosApr` en rojo con el texto de bloqueo.
- [ ] Doble aprobación: aprobar la misma solicitud desde dos sesiones →
      la segunda muestra "**ya fue resuelta (Aprobado) por &lt;nombre&gt;**" y **no
      crea asientos duplicados**.
- [ ] Rechazar sin motivo es imposible (botón deshabilitado); con motivo, el
      texto queda en `StatusReason` del registro.
- [ ] Rechazar `Tula con diferencia` devuelve la tula a *Asignada a cajero* y
      **no crea ningún asiento**.
- [ ] Rechazar `Editar tula` **saca la tula** de *Pendiente de revisión por
      supervisor - esclusa* (el bug de tulas atrapadas) y avisa del estado usado.
- [ ] Rechazar `Primera tula en planilla` deja la tula en *Almacenada en boveda*
      **y con `RecountingSession` VACÍA** — verificar el registro en Dataverse: si
      queda enlazada a la sesión no aprobada, el caso 7 de §12 no se aplicó.
- [ ] **Cuenta bancaria visible**: abrir una `Tula con diferencia` y confirmar que
      `lblKvCuentaApr` muestra un número, no "—". Si muestra "—" con depósitos
      cargados, falta el **Field Security Profile** sobre `sanic_accountnumber`
      (§15) y los asientos se van a escribir SIN cuenta, sin dar error.
- [ ] Una solicitud del Histórico **auto-aprobada** (`Crear sesion de recuento` de
      la propia mesa del cajero) muestra `"· Aprobado"` **sin** el `" por  el "`
      colgado — los tres tramos de la resolución son independientes.
- [ ] Rechazar `Remapear tula` / `Eliminar tula` con un `BagPreviousState`
      inválido **NO** escribe `BagStatus` — avisa y deja el estado actual.
- [ ] Cortar la red y aprobar: `Notify` con el error real y el overlay se
      **APAGA** (no queda pegado).
- [ ] Achicar la ventana por debajo de **1212 px**: el detalle **baja** debajo de
      la bandeja, ambas cards conservan su alto (610) y `cntBodyApr` scrollea;
      nada se recorta ni se superpone. El sello de tiempo de la fila desaparece
      bajo 1100 px.
- [ ] Los nombres de todos los controles terminan en `Apr` y ninguno choca con
      los de la guía 02 (`…Res`), 04 (`…Tula`), 05 (`…Chq`) ni con los hijos
      internos de `cmpNav`/`cmpKpi` (que no llevan sufijo).
