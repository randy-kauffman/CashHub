# Guía 05 — scrCheques (paradigma manual v2)

Recepción de cheques que el Supervisor envía a digitación. **Solo acuse físico**:
`Recibido` / `No recibido`. **NADA de Digitado/Rebotado** (fuera de alcance, 00-§4).
Esta pantalla **no tiene rail de cajeros**: filtra por **estado**, no por cajero
(01 §7.0 — `cntMainChq` ocupa el ancho completo dentro de `cntBodyChq`).

**Layout**: contenedores **manuales** + **fórmulas relativas** a `Parent.Width`/
`Parent.Height` + constantes `Lay*` (01 §7). Cero contenedores de auto-ajuste, cero
`FillPortions`. Lo repetido es **componente** (01 §8): `cmpHeader`, `cmpPildora`,
`cmpEmpty`. Fidelidad al mockup **CH1** (recepción) y **CH2** (ya recibido).

**Contrato de entrada**: pantalla autónoma. El segmentado del header llega con
`Navigate(scrCheques)` sin gvars previas. Se inicializa sola en `OnVisible`:

- `gvarChq` — cheque seleccionado (global; lo setea esta pantalla, lo consume el
  detalle). Puede quedar `Blank()` si el segmento activo está vacío.
- `ctxSegChq` — `"Enviados"` | `"Recibidos"` | `"No recibidos"` (segmento activo).
- `ctxEstadoChq` — el **valor de choice** de `CheckStatus` del segmento (hoisteado a
  variable para que el `Filter` delegue sin ambigüedad).
- `ctxFeedbackChq` / `ctxFeedbackMsgChq` — banner de resultado de la acción.
- `ctxBusyChq` — anti-doble-click de las acciones.

> **Esquema (display names EXACTOS).** Datasource `'CH-Tbl-Checks'`. Choice de estado:
> `'CheckStatus (CH-Tbl-Checks)'` con `.'Enviado a digitacion'` (159460003),
> `.Recibido` (159460004), `.'No Recibido'` (159460005). Columnas: `CheckNumber`,
> `CheckAmount`, `CheckCurrency`, `CheckTypes` (`'BAC Local'`/`'Otros Local'`),
> `'Enviado el'`, `'Recibido el'`, `'No recibido el'` (datetime), `RelatedReCounterDepositID`
> (lookup → `CH-Tbl-DepositsRecounting`), y los lookups de autoría **ya existentes**
> `SentToDigitizationBy` (`sanic_senttodigitizationby`) / `ReceivedBy` (`sanic_receivedby`)
> → `Users`. Si IntelliSense nombra distinto la PK o el enum, aceptá el suyo y avisá.

## Árbol de controles

```
scrCheques                                 (Fill: ThemeBg)
├── cmpHeaderChq                           Componente cmpHeader (01 §8.1) · Activo="Cheques"
└── cntBodyChq                             Contenedor (manual) · X 0 · Y LayHeaderH · W Parent.Width · H Parent.Height-LayHeaderH
    └── cntMainChq                         Contenedor (manual) · X 0 · Y 0 · W Parent.Width · H Parent.Height (SIN rail)
        ├── cntToolbarChq                  Contenedor (manual) · barra segmentado + buscador
        │   ├── cntSegChq                  Contenedor (manual) · píldora segmentada
        │   │   ├── btnSegEnvChq           Botón clásico
        │   │   ├── btnSegRecChq           Botón clásico
        │   │   └── btnSegNoRecChq         Botón clásico
        │   └── inpBuscarChq               Entrada de texto clásica
        ├── cntListaChq                    Contenedor (manual) · card (01 §7.1)
        │   ├── cntGridHeadChq             Contenedor (manual) · encabezados de grilla
        │   │   ├── lblColNumChq           Etiqueta clásica  "N° CHEQUE"
        │   │   ├── lblColClienteChq       Etiqueta clásica  "CLIENTE / DEPÓSITO"
        │   │   ├── lblColPorChq           Etiqueta clásica  (dinámico por segmento)
        │   │   ├── lblColMontoChq         Etiqueta clásica  "MONTO"
        │   │   ├── lblColFechaChq         Etiqueta clásica  (dinámico por segmento)
        │   │   ├── lblColEstadoChq        Etiqueta clásica  "ESTADO"
        │   │   └── shpGridHeadLineaChq    Rectángulo (línea inferior 1 px)
        │   ├── galChequesChq              Galería vertical en blanco · TemplateSize LayRowH
        │   │   ├── shpFilaSelChq          Rectángulo (fondo selección, PRIMERO)
        │   │   ├── shpFilaSelBarraChq     Rectángulo (barra de acento)
        │   │   ├── btnFilaChq             Botón clásico (overlay clicable, 01 §7.3)
        │   │   ├── lblFilaNumChq          Etiqueta clásica
        │   │   ├── lblFilaClienteChq      Etiqueta clásica
        │   │   ├── lblFilaPorChq          Etiqueta clásica
        │   │   ├── lblFilaMontoChq        Etiqueta clásica
        │   │   ├── lblFilaFechaChq        Etiqueta clásica
        │   │   ├── pilFilaEstadoChq       Botón clásico (píldora 7.2 · NO componente — PA2122)
        │   │   └── shpFilaSepChq          Rectángulo (separador 1 px)
        │   └── cmpVacioChq                Componente cmpEmpty (01 §8.6) · estado vacío CH3
        └── cntDetalleChq                  Contenedor (manual) · card (01 §7.1)
            ├── cntDetHeaderChq            Contenedor (manual)
            │   ├── lblDetTituloChq        Etiqueta clásica  "Cheque N°…"
            │   ├── lblDetSubChq           Etiqueta clásica  "cliente · depósito"
            │   ├── pilDetEstadoChq        Componente cmpPildora (01 §8.3)
            │   └── shpDetHeaderLineaChq   Rectángulo (línea inferior 1 px)
            ├── cntDetOpsChq               Contenedor (manual) · acciones
            │   ├── btnRecibidoChq         Botón clásico (ESTILO-BTN-PRIMARIO)
            │   ├── btnNoRecibidoChq       Botón clásico (ESTILO-BTN-FANTASMA)
            │   └── shpDetOpsLineaChq      Rectángulo (línea inferior 1 px)
            ├── cntDetBodyChq              Contenedor (manual) · feedback + datos + trazabilidad
            │   ├── lblFeedbackChq         Etiqueta clásica (banner de resultado)
            │   ├── lblSecDatosChq         Etiqueta clásica  "DATOS DEL CHEQUE" (ESTILO-SECCION)
            │   ├── cntKvNumChq            Contenedor (manual) → lblKNumChq / lblVNumChq
            │   ├── cntKvMontoChq          Contenedor (manual) → lblKMontoChq / lblVMontoChq
            │   ├── cntKvMonedaChq         Contenedor (manual) → lblKMonedaChq / lblVMonedaChq
            │   ├── cntKvTipoChq           Contenedor (manual) → lblKTipoChq / lblVTipoChq
            │   ├── cntKvClienteChq        Contenedor (manual) → lblKClienteChq / lblVClienteChq
            │   ├── cntKvDepositoChq       Contenedor (manual) → lblKDepositoChq / lblVDepositoChq
            │   ├── lblSecTrazaChq         Etiqueta clásica  "TRAZABILIDAD" (ESTILO-SECCION)
            │   ├── cntKvEnvPorChq         Contenedor (manual) → lblKEnvPorChq / lblVEnvPorChq
            │   ├── cntKvEnvElChq          Contenedor (manual) → lblKEnvElChq / lblVEnvElChq
            │   ├── cntKvRecPorChq         Contenedor (manual) → lblKRecPorChq / lblVRecPorChq
            │   └── cntKvRecElChq          Contenedor (manual) → lblKRecElChq / lblVRecElChq
            └── cmpDetVacioChq             Componente cmpEmpty (01 §8.6) · sin selección
```

> **Convención de par kv**: cada `cntKv{Campo}Chq` es un **Contenedor (manual)** con dos
> hijos `lblK{Campo}Chq` (clave, izq) y `lblV{Campo}Chq` (valor, der). Se describen por
> el patrón único de §5.3, no fila por fila del árbol — cierran el 1:1 por regla.

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | fórmula §1.1 |

### 1.1 scrCheques.OnVisible (pegar completo — SIN CAMBIOS de lógica)

```powerfx
UpdateContext({
    ctxSegChq: "Enviados",
    ctxEstadoChq: 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',
    ctxFeedbackChq: "",
    ctxFeedbackMsgChq: "",
    ctxBusyChq: false
});
Reset(inpBuscarChq);
Set(gvarChq,
    First(Filter('CH-Tbl-Checks',
        CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'))
)
```

> `First(Filter(...))` sobre la columna choice **propia** delega (igualdad de choice →
> `_sanic_checkstatus eq 159460003`). Preselecciona el primer cheque para que el detalle
> no arranque vacío. La selección se gobierna por `gvarChq`, **no** por `IsSelected`
> (divergencia **L-Chq-1**).

## 2. Encabezado — `cmpHeaderChq` (Componente cmpHeader, 01 §8.1)

Insertá **Insertar → Componentes personalizados → cmpHeader** y renombralo `cmpHeaderChq`.
El árbol y estilos internos ya viven en 01 §8.1; acá solo la instancia:

| Propiedad (instancia) | Fórmula |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `Parent.Width` / `LayHeaderH` |
| `Titulo` | `"Digitación"` |
| `Subtitulo` | `"Cheques"` |
| `Activo` | `"Cheques"` |
| `OnCorte` | `Navigate(scrCorte)` |
| `OnManual` | `Navigate(scrManual)` |
| `OnLotes` | `Navigate(scrLotes)` |
| `OnCheques` | `Navigate(scrCheques)` *(inocuo, ya estás)* |

> El nav navega por **behavior props** (01 §8.1): un componente no puede `Navigate` a
> pantallas externas. `Activo="Cheques"` prende la pastilla de Cheques y apaga las otras
> tres, fiel al `navSeg` de CH1/CH2.

## 3. Cuerpo — `cntBodyChq` / `cntMainChq` (SIN rail)

`cntBodyChq` (Contenedor manual): X `0` · Y `LayHeaderH` · Width `Parent.Width` ·
Height `Parent.Height - LayHeaderH` · Fill `ThemeBg`. Único hijo: `cntMainChq`.

`cntMainChq` (Contenedor manual): X `0` · Y `0` · Width `Parent.Width` ·
Height `Parent.Height` · Fill `Color.Transparent`. Hijos: `cntToolbarChq`,
`cntListaChq`, `cntDetalleChq`. (Sin `cmpRail` — esta pantalla no lo tiene.)

> **Sistema de referencia (01 §7.0).** Cada control usa `Parent.Width`/`Parent.Height`
> = los de SU contenedor. `cntMainChq` es el marco de las 3 zonas; las cards se dimensionan
> con fracciones de su ancho útil (`Parent.Width - 2*LayGutter - LayGap`).

## 4. Toolbar — `cntToolbarChq` (segmentado de estado + buscador)

`cntToolbarChq` (Contenedor manual): Fill `Color.Transparent`. Alto fijo `52`.

| Propiedad | Fórmula |
|---|---|
| X / Y | `0` / `0` |
| Width | `Parent.Width` |
| Height | `52` |

Hijos: `cntSegChq` (izquierda), `inpBuscarChq` (a su derecha). El resto del ancho queda
libre — el buscador **no** se estira (fidelidad CH1: seg + input pegados a la izquierda).

### 4.1 Segmentado `[Enviados | Recibidos | No recibidos]` — `cntSegChq`

`cntSegChq` (Contenedor manual): Fill `ThemeSurface2`, BorderColor `ThemeLine`,
BorderThickness `1`, RadiusTop/Bottom Left/Right `9`.

| Propiedad | Fórmula |
|---|---|
| X / Y | `LayGutter` / `(Parent.Height - Self.Height) / 2` |
| Width / Height | `360` / `34` |

Los tres botones (Botón clásico) — patrón común: Font `Font.'Open Sans'`, Size `9`,
RadiusTop/Bottom Left/Right `7`, BorderThickness `0`, DisplayMode `Edit`,
Y `3`, Height `Parent.Height - 6`, Width `(Parent.Width - 12) / 3`. X encadenado:

| Control | X |
|---|---|
| `btnSegEnvChq` | `3` |
| `btnSegRecChq` | `btnSegEnvChq.X + btnSegEnvChq.Width + 3` |
| `btnSegNoRecChq` | `btnSegRecChq.X + btnSegRecChq.Width + 3` |

Propiedades por botón (Text con conteo, estado activo por `ctxSegChq`):

| Control | Text | Fill | Color | FontWeight | OnSelect |
|---|---|---|---|---|---|
| `btnSegEnvChq` | `$"Enviados ({CountRows(Filter('CH-Tbl-Checks', CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'))})"` | `If(ctxSegChq="Enviados", ThemeSurface, Color.Transparent)` | `If(ctxSegChq="Enviados", ThemeAccent, ThemeInk2)` | `If(ctxSegChq="Enviados", Font.Weight.Semibold, Font.Weight.Normal)` | §4.2 (Enviados) |
| `btnSegRecChq` | `$"Recibidos ({CountRows(Filter('CH-Tbl-Checks', CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recibido))})"` | `If(ctxSegChq="Recibidos", ThemeSurface, Color.Transparent)` | `If(ctxSegChq="Recibidos", ThemeAccent, ThemeInk2)` | `If(ctxSegChq="Recibidos", Font.Weight.Semibold, Font.Weight.Normal)` | §4.2 (Recibidos) |
| `btnSegNoRecChq` | `$"No recibidos ({CountRows(Filter('CH-Tbl-Checks', CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido'))})"` | `If(ctxSegChq="No recibidos", ThemeSurface, Color.Transparent)` | `If(ctxSegChq="No recibidos", ThemeAccent, ThemeInk2)` | `If(ctxSegChq="No recibidos", Font.Weight.Semibold, Font.Weight.Normal)` | §4.2 (No recibidos) |

> 🧨 **`CountRows` NO delega en Dataverse**: cuenta hasta `DataRowLimit` (500). Con colas
> históricas > 500 en Recibidos/No recibidos, el badge queda tapado en 500. Es un número
> **informativo**, no un conteo sobre el que se dé consentimiento (clase D-2 acotada). El
> `Filter(= <choice>)` interno **sí** delega; lo que no delega es el `CountRows` que lo
> envuelve. **Fallback declarado hasta backend**: dejarlo así, o quitar el número. El
> conteo exacto lo daría un agregado server-side de la familia `sanic_CHWorkloadByCashier`
> (00 §3.3).

### 4.2 OnSelect de cada botón de segmento (patrón — cambiá el literal)

Enviados:
```powerfx
UpdateContext({
    ctxSegChq: "Enviados",
    ctxEstadoChq: 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion',
    ctxFeedbackChq: ""
});
Reset(inpBuscarChq);
Set(gvarChq, First(Filter('CH-Tbl-Checks', CheckStatus = ctxEstadoChq)))
```
Recibidos: idem con `ctxSegChq: "Recibidos"` y `ctxEstadoChq: 'CheckStatus (CH-Tbl-Checks)'.Recibido`.
No recibidos: idem con `ctxSegChq: "No recibidos"` y `ctxEstadoChq: 'CheckStatus (CH-Tbl-Checks)'.'No Recibido'`.

> Al cambiar de segmento: limpio el buscador (`Reset`), reapunto `gvarChq` al primero del
> nuevo estado (delega) y borro el feedback. `First(Filter(…, CheckStatus = ctxEstadoChq))`
> delega porque compara la columna choice propia contra una variable que **contiene** un
> valor de choice.

### 4.3 Buscador — `inpBuscarChq` (Entrada de texto clásica)

| Propiedad | Fórmula |
|---|---|
| X / Y | `cntSegChq.X + cntSegChq.Width + 10` / `(Parent.Height - Self.Height) / 2` |
| Width / Height | `280` / `34` |
| HintText | `"⌕ N° de cheque o cliente…"` |
| BorderColor / RadiusTop/Bottom Left/Right | `ThemeLine` / `8` |
| Font / Size | `Font.'Open Sans'` / `9` |
| AccessibleLabel | `"Buscar cheque por número o cliente"` |

> **Enter no hace falta**: la galería filtra **en vivo** con `inpBuscarChq.Value` (§5.2.1).
> Cada tecla refina la lista.

## 5. Lista (card master) — `cntListaChq`

`cntListaChq` (Contenedor manual, card 01 §7.1): Fill `ThemeSurface`, BorderColor
`ThemeLine`, BorderThickness `1`, RadiusTop/Bottom Left/Right `LayCardR`.

| Propiedad | Fórmula |
|---|---|
| X | `LayGutter` |
| Y | `cntToolbarChq.Y + cntToolbarChq.Height` |
| Width | `(Parent.Width - 2*LayGutter - LayGap) * 0.6` |
| Height | `Parent.Height - Self.Y - LayGap` |

> Ancho **1.5×** del detalle (mockup `flex:1.5` vs `flex:1` → `0.6` / `0.4` del ancho útil).
> Hijos: `cntGridHeadChq`, `galChequesChq`, `cmpVacioChq`.

### 5.1 Encabezado de grilla — `cntGridHeadChq`

`cntGridHeadChq` (Contenedor manual): Fill `ThemeSurface2`, RadiusTop Left/Right `LayCardR`,
RadiusBottom Left/Right `0`.

| Propiedad | Fórmula |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `Parent.Width` / `34` |

**Coordenadas de columna (regla única, reusada por la galería en §5.2.2):** sea
`U = Parent.Width - 32` (16 px de respiro a cada lado). Pesos `[.15, .26, .15, .13, .16, .15]`
(fracción de `1fr 1.8fr 1fr .9fr 1.2fr 1fr` del mockup, §CH1) → acumulados
`0, .15, .41, .56, .69, .85`. Seis Etiquetas **ESTILO-HEADER-COL**, Y `0`, Height `34`,
VerticalAlign `Middle`, Wrap `false`:

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblColNumChq` | `"N° CHEQUE"` | `16` | `0.15*U` | Left |
| `lblColClienteChq` | `"CLIENTE / DEPÓSITO"` | `16 + 0.15*U` | `0.26*U` | Left |
| `lblColPorChq` | `Switch(ctxSegChq, "Enviados", "ENVIADO POR", "Recibidos", "RECIBIDO POR", "No recibidos", "NO RECIBIDO POR")` | `16 + 0.41*U` | `0.15*U` | Left |
| `lblColMontoChq` | `"MONTO"` | `16 + 0.56*U` | `0.13*U` | Right |
| `lblColFechaChq` | `Switch(ctxSegChq, "Enviados", "ENVIADO", "Recibidos", "RECIBIDO", "No recibidos", "NO RECIBIDO")` | `16 + 0.69*U` | `0.16*U` | Right |
| `lblColEstadoChq` | `"ESTADO"` | `16 + 0.85*U` | `0.15*U` | Left |

`shpGridHeadLineaChq` (Rectángulo): X `0` · Y `Parent.Height - 1` · Width `Parent.Width` ·
Height `1` · Fill `ThemeLine`.

### 5.2 `galChequesChq` (Galería vertical en blanco)

| Propiedad | Fórmula |
|---|---|
| X / Y | `0` / `cntGridHeadChq.Y + cntGridHeadChq.Height` |
| Width | `Parent.Width` |
| Height | `Parent.Height - Self.Y` |
| TemplateSize | `LayRowH` |
| TemplatePadding | `0` |
| ShowScrollbar | `true` |
| OnSelect | `Set(gvarChq, ThisItem); UpdateContext({ctxFeedbackChq: ""})` |
| Items | fórmula §5.2.1 |

#### 5.2.1 galChequesChq.Items (pegar completo — SIN CAMBIOS de lógica)

```powerfx
With({_q: Trim(inpBuscarChq.Value)},
    Filter(
        Filter('CH-Tbl-Checks', CheckStatus = ctxEstadoChq),
        _q = ""
            || StartsWith(CheckNumber, _q)
            || StartsWith(RelatedReCounterDepositID.Name, _q)
            || StartsWith(RelatedReCounterDepositID.ClientName, _q)
    )
)
```

> 🧨 **Delegación — límite declarado.** El **Filter interno** (`CheckStatus = ctxEstadoChq`)
> es el que importa por volumen y **delega** (choice propia = variable choice). El **Filter
> externo** (buscador) corre **client-side sobre la página ya traída** (≤ 500): `StartsWith`
> sobre `CheckNumber` **más** el dot-walk a `RelatedReCounterDepositID` mezclados con `||`
> no delega, se resuelve en memoria a propósito. En **Enviados** (cola acotada) es exacto;
> en **Recibidos/No recibidos** históricos > 500, un cheque viejo puede no aparecer por
> búsqueda. Mitigación futura: búsqueda delegable por `CheckNumber` o el agregado de 00 §3.3.
>
> **`ClientName` es placeholder**: el nombre real de la columna de cliente en
> `CH-Tbl-DepositsRecounting` lo confirma IntelliSense al escribir `RelatedReCounterDepositID.`.
> Si no existe columna de cliente navegable, **borrá esa tercera línea** — el buscador sigue
> por N° de cheque y por depósito (`DR-…`). El dot-walk acá es **solo para mostrar/buscar en
> memoria**, no en predicado delegado: no rompe nada.

#### 5.2.2 Template de la galería

Sea `Ut = Parent.TemplateWidth - 32` (mismo respiro de 16). Mismos pesos/acumulados que el
encabezado (§5.1) → **X template = X encabezado** (galería y card comparten borde izquierdo,
ambos con inset 16). Todos los labels: Y `0`, Height `Parent.TemplateHeight`, ESTILO-CELDA,
VerticalAlign `Middle`, Wrap `false`.

| Control | Propiedad | Valor |
|---|---|---|
| `shpFilaSelChq` (Rectángulo, **PRIMERO**) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` |
| | Fill | `If(!IsBlank(gvarChq) && ThisItem.'CH-Tbl-Checks' = gvarChq.'CH-Tbl-Checks', ThemeAccentSoft, Color.Transparent)` |
| | BorderThickness | `0` |
| `shpFilaSelBarraChq` (Rectángulo) | X/Y/W/H | `0` / `0` / `3` / `Parent.TemplateHeight` |
| | Fill / Visible | `ThemeAccent` / `!IsBlank(gvarChq) && ThisItem.'CH-Tbl-Checks' = gvarChq.'CH-Tbl-Checks'` |
| `btnFilaChq` (Botón clásico, overlay 01 §7.3) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` |
| | Text / BorderThickness | `""` / `0` |
| | Fill / HoverFill / PressedFill | `Color.Transparent` / `ThemeSurface2` / `ThemeAccentSoft` |
| | OnSelect | `Select(Parent)` *(la acción vive en `galChequesChq.OnSelect`)* |
| | AccessibleLabel | `"Ver cheque " & ThisItem.CheckNumber` |
| `lblFilaNumChq` | X / W | `16` / `0.15*Ut` |
| | Text / FontWeight | `ThisItem.CheckNumber` / `Font.Weight.Semibold` |
| | OnSelect | `Select(Parent)` |
| `lblFilaClienteChq` | X / W | `16 + 0.15*Ut` / `0.26*Ut` |
| | Text | `Coalesce(ThisItem.RelatedReCounterDepositID.ClientName, "—") & " · " & Coalesce(ThisItem.RelatedReCounterDepositID.Name, "—")` |
| | Color / OnSelect | `ThemeInk2` / `Select(Parent)` |
| `lblFilaPorChq` | X / W | `16 + 0.41*Ut` / `0.15*Ut` |
| | Text | `Switch(ctxSegChq, "Enviados", Coalesce(ThisItem.SentToDigitizationBy.'Full Name', "—"), Coalesce(ThisItem.ReceivedBy.'Full Name', "—"))` |
| | Color / OnSelect | `ThemeInk2` / `Select(Parent)` |
| `lblFilaMontoChq` | X / W / Align | `16 + 0.56*Ut` / `0.13*Ut` / `Right` |
| | Text / FontWeight | `Text(ThisItem.CheckAmount, FmtMoney)` / `Font.Weight.Semibold` |
| | OnSelect | `Select(Parent)` |
| `lblFilaFechaChq` | X / W / Align | `16 + 0.69*Ut` / `0.16*Ut` / `Right` |
| | Text | `Switch(ctxSegChq, "Enviados", Text(ThisItem.'Enviado el', FmtHora), "Recibidos", Text(ThisItem.'Recibido el', FmtFecha), "No recibidos", Text(ThisItem.'No recibido el', FmtFecha))` |
| | Color / OnSelect | `ThemeInk3` / `Select(Parent)` |
| `pilFilaEstadoChq` (Botón clásico · píldora 7.2) | Tipo | Botón clásico · DisplayMode `View` · Radius `12` · Size `7.88` · Semibold · BorderThickness `0` |
| | X / Y / W / H | `16 + 0.85*Ut` / `(Parent.TemplateHeight - 22) / 2` / `82` / `22` |
| | `Text` | `Switch(ThisItem.CheckStatus, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', "Enviado", 'CheckStatus (CH-Tbl-Checks)'.Recibido, "Recibido", 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', "No recibido", "—")` |
| | `Fill` | `With({s: ThisItem.CheckStatus}, Switch(s, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', ThemeInfoSoft, 'CheckStatus (CH-Tbl-Checks)'.Recibido, ThemeOkSoft, 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', ThemeDangerSoft, ThemeLineSoft))` |
| | `Color` | `With({s: ThisItem.CheckStatus}, Switch(s, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', ThemeInfo, 'CheckStatus (CH-Tbl-Checks)'.Recibido, ThemeOk, 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', ThemeDanger, ThemeInk3))` |
| `shpFilaSepChq` (Rectángulo) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · Fill `ThemeLineSoft` |

> **Píldora de fila = Botón NATIVO, no componente** (PA2122): un `CanvasComponent` no puede
> anidarse en una galería (el scope de fila `ThisItem` y el scope aislado del componente no
> se combinan). Por eso `pilFilaEstadoChq` es un Botón clásico que replica el estilo de
> `cmpPildora` (01 §8.3) leyendo `ThisItem.CheckStatus`. Es `DisplayMode.View`, así que no
> intercepta el clic: el tap cae al `btnFilaChq` / `galChequesChq.OnSelect`. Todo hijo del
> template delega con `Select(Parent)`; el overlay va al **fondo** del z-order (01 §7.3) para
> no tapar el texto con su `HoverFill`. La píldora de **detalle** (`pilDetEstadoChq`) sí queda
> como componente: vive fuera de galería.
>
> **Autoría — lookups reales.** `SentToDigitizationBy` (`sanic_senttodigitizationby`) y
> `ReceivedBy` (`sanic_receivedby`) **ya existen** en el backend (00 §3.3/§9). "Enviado por"
> = `SentToDigitizationBy.'Full Name'`; "Recibido/No recibido por" = `ReceivedBy.'Full Name'`.
> Las fechas también son reales (`'Enviado el'`, `'Recibido el'`, `'No recibido el'`).

### 5.3 Estado vacío — `cmpVacioChq` (Componente cmpEmpty, CH3)

Instancia de `cmpEmpty` (01 §8.6) superpuesta al área de la galería (overlay con X/Y **es
válido** dentro de un contenedor **manual**):

| Propiedad (instancia) | Fórmula |
|---|---|
| X / Y | `0` / `galChequesChq.Y` |
| Width / Height | `Parent.Width` / `Parent.Height - galChequesChq.Y` |
| Visible | `IsEmpty(galChequesChq.AllItems)` |
| `Glifo` | `"✓"` |
| `Mensaje` | `Switch(ctxSegChq, "Enviados", If(Trim(inpBuscarChq.Value) <> "", "Ningún cheque enviado coincide con la búsqueda.", "No hay cheques esperando recepción. Cuando el Supervisor envíe cheques a digitación, aparecen acá."), "Recibidos", "Todavía no marcaste cheques como recibidos en este filtro.", "No recibidos", "No hay cheques marcados como no recibidos.")` |

> `IsEmpty(galChequesChq.AllItems)` mira la página cargada — correcto para "0 filas". La
> galería auto-selecciona la 1ª fila cuando hay filas, así que vacío ⟺ colección vacía. El
> `Mensaje` entra por prop desde la pantalla (el componente no lee `ctx`/`gvar`, 01 §8).

## 6. Detalle (card del cheque seleccionado) — `cntDetalleChq`

`cntDetalleChq` (Contenedor manual, card 01 §7.1): Fill `ThemeSurface`, BorderColor
`ThemeLine`, BorderThickness `1`, RadiusTop/Bottom Left/Right `LayCardR`.

| Propiedad | Fórmula |
|---|---|
| X | `cntListaChq.X + cntListaChq.Width + LayGap` |
| Y | `cntListaChq.Y` |
| Width | `(Parent.Width - 2*LayGutter - LayGap) * 0.4` |
| Height | `cntListaChq.Height` |

Hijos: `cntDetHeaderChq`, `cntDetOpsChq`, `cntDetBodyChq`, `cmpDetVacioChq`. Con cheque
seleccionado se muestran los 3 primeros; sin selección, solo `cmpDetVacioChq`:

- `cntDetHeaderChq`, `cntDetBodyChq`: Visible `!IsBlank(gvarChq)`.
- `cntDetOpsChq`: Visible según §6.2 (además exige estado Enviado).
- `cmpDetVacioChq`: Visible `IsBlank(gvarChq)`.

### 6.1 Header del detalle — `cntDetHeaderChq`

`cntDetHeaderChq` (Contenedor manual): X `0` · Y `0` · Width `Parent.Width` · Height `56` ·
Fill `Color.Transparent` · Visible `!IsBlank(gvarChq)`.

| Control | Propiedad | Valor |
|---|---|---|
| `lblDetTituloChq` (Etiqueta clásica) | X / Y / W / H | `16` / `9` / `Parent.Width - 16 - 92 - 20` / `22` |
| | Text / Estilo | `$"Cheque {Coalesce(gvarChq.CheckNumber, "—")}"` · ESTILO-TITULO con Size `12` |
| `lblDetSubChq` (Etiqueta clásica) | X / Y / W / H | `16` / `31` / `Parent.Width - 16 - 92 - 20` / `16` |
| | Text | `Coalesce(gvarChq.RelatedReCounterDepositID.ClientName, "—") & " · " & Coalesce(gvarChq.RelatedReCounterDepositID.Name, "—")` |
| | Size / Color | `8.25` / `ThemeInk3` |
| `pilDetEstadoChq` (Componente cmpPildora) | X / Y / W / H | `Parent.Width - 16 - 92` / `(Parent.Height - 22) / 2` / `92` / `22` |
| | `Texto` | `Switch(gvarChq.CheckStatus, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', "Enviado", 'CheckStatus (CH-Tbl-Checks)'.Recibido, "Recibido", 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', "No recibido", "—")` |
| | `Tono` | `Switch(gvarChq.CheckStatus, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', "info", 'CheckStatus (CH-Tbl-Checks)'.Recibido, "ok", 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', "danger", "neutro")` |
| `shpDetHeaderLineaChq` (Rectángulo) | X/Y/W/H | `0` / `Parent.Height - 1` / `Parent.Width` / `1` · Fill `ThemeLine` |

### 6.2 Acciones — `cntDetOpsChq`

`cntDetOpsChq` (Contenedor manual): X `0` · Y `cntDetHeaderChq.Y + cntDetHeaderChq.Height` ·
Width `Parent.Width` · Height `52` · Fill `Color.Transparent`.
**Visible** (toda la barra): `!IsBlank(gvarChq) && gvarChq.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'`
— en Recibidos/No recibidos no hay acciones (el cheque ya está resuelto).

| Control | Propiedad | Valor |
|---|---|---|
| `btnRecibidoChq` (Botón clásico, ESTILO-BTN-PRIMARIO) | X / Y / W / H | `16` / `10` / `(Parent.Width - 32 - 8) / 2` / `32` |
| | Text | `"Recibido"` |
| | DisplayMode | `If(ctxBusyChq, DisplayMode.Disabled, DisplayMode.Edit)` |
| | AccessibleLabel | `"Marcar el cheque como recibido"` |
| | OnSelect | fórmula §6.2.1 |
| `btnNoRecibidoChq` (Botón clásico, ESTILO-BTN-FANTASMA) | X / Y / W / H | `btnRecibidoChq.X + btnRecibidoChq.Width + 8` / `10` / `(Parent.Width - 32 - 8) / 2` / `32` |
| | Text | `"No recibido"` |
| | DisplayMode | `If(ctxBusyChq, DisplayMode.Disabled, DisplayMode.Edit)` |
| | AccessibleLabel | `"Marcar el cheque como no recibido"` |
| | OnSelect | fórmula §6.2.2 |
| `shpDetOpsLineaChq` (Rectángulo) | X/Y/W/H | `0` / `Parent.Height - 1` / `Parent.Width` / `1` · Fill `ThemeLineSoft` |

#### 6.2.1 btnRecibidoChq.OnSelect (pegar completo — SIN CAMBIOS de lógica)

```powerfx
UpdateContext({ctxBusyChq: true, ctxFeedbackChq: ""});
IfError(
    Patch('CH-Tbl-Checks', gvarChq,
        {CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.Recibido});
    UpdateContext({ctxFeedbackChq: "ok",
        ctxFeedbackMsgChq: $"Cheque {gvarChq.CheckNumber} marcado como Recibido."});
    Set(gvarChq, First(Filter('CH-Tbl-Checks', CheckStatus = ctxEstadoChq))),
    UpdateContext({ctxFeedbackChq: "error",
        ctxFeedbackMsgChq: $"No se pudo marcar como Recibido. {FirstError.Message}"})
);
UpdateContext({ctxBusyChq: false})
```

#### 6.2.2 btnNoRecibidoChq.OnSelect (pegar completo — SIN CAMBIOS de lógica)

```powerfx
UpdateContext({ctxBusyChq: true, ctxFeedbackChq: ""});
IfError(
    Patch('CH-Tbl-Checks', gvarChq,
        {CheckStatus: 'CheckStatus (CH-Tbl-Checks)'.'No Recibido'});
    UpdateContext({ctxFeedbackChq: "ok",
        ctxFeedbackMsgChq: $"Cheque {gvarChq.CheckNumber} marcado como No recibido."});
    Set(gvarChq, First(Filter('CH-Tbl-Checks', CheckStatus = ctxEstadoChq))),
    UpdateContext({ctxFeedbackChq: "error",
        ctxFeedbackMsgChq: $"No se pudo marcar como No recibido. {FirstError.Message}"})
);
UpdateContext({ctxBusyChq: false})
```

> 🧨 **Una sola columna, sin `Refresh` de tabla** (mata P-1/P-2). El `Patch` escribe **solo**
> `CheckStatus`; los sellos de fecha y los lookups de autoría los estampa el plugin
> server-side (`CheckStatusStampPlugin`, 00 §4.3), no la app. Tras el éxito reapunto `gvarChq`
> al **siguiente** del segmento activo (`ctxEstadoChq`): el cheque recién tocado ya salió del
> filtro y `First(...)` trae al siguiente pendiente (auto-avance). Si no queda ninguno,
> `gvarChq` = `Blank()` → detalle vacío + `cmpVacioChq`. El `IfError` de 2 argumentos corta
> la cadena en el `Patch`: si falla, **no** marca "ok" ni avanza, y muestra el motivo del
> guard (`CheckGuard`) — feedback visible, nunca mudo (C-2).
>
> `ctxBusyChq` es el anti-doble-click; además, al cambiar el estado el cheque deja de cumplir
> la `Visible` de `cntDetOpsChq`, así que la barra desaparece sola. Sin diálogo (el mockup CH1
> modela acción directa; la recepción es reversible por reenvío del Supervisor). Sin spinner
> (L-3): el feedback es el banner (§6.3.1).

### 6.3 Cuerpo del detalle — `cntDetBodyChq`

`cntDetBodyChq` (Contenedor manual): X `0` · Width `Parent.Width` · Fill `Color.Transparent` ·
Visible `!IsBlank(gvarChq)`. **Y y Height reflúyen por fórmula** según si la barra de
acciones está visible (no hay auto-layout que reordene — 01 §7.0; el "reflow" se hace con `If`):

| Propiedad | Fórmula |
|---|---|
| Y | `If(cntDetOpsChq.Visible, cntDetOpsChq.Y + cntDetOpsChq.Height, cntDetHeaderChq.Y + cntDetHeaderChq.Height)` |
| Height | `Parent.Height - Self.Y` |

**Stacking interno** (X `16`, Width `Parent.Width - 32` salvo aclaración; cada control
referencia la Y del anterior — 01 §7.0 "uno referencia al otro"). Sea el desplazamiento del
banner `fbOffset = If(ctxFeedbackChq <> "", 52, 0)` (alto del banner 40 + respiro 12):

| Control | Y | Height |
|---|---|---|
| `lblFeedbackChq` | `14` | `40` |
| `lblSecDatosChq` | `14 + fbOffset` | `16` |
| `cntKvNumChq` | `lblSecDatosChq.Y + lblSecDatosChq.Height + 8` | `20` |
| `cntKvMontoChq` | `cntKvNumChq.Y + cntKvNumChq.Height + 2` | `20` |
| `cntKvMonedaChq` | `cntKvMontoChq.Y + 22` | `20` |
| `cntKvTipoChq` | `cntKvMonedaChq.Y + 22` | `20` |
| `cntKvClienteChq` | `cntKvTipoChq.Y + 22` | `20` |
| `cntKvDepositoChq` | `cntKvClienteChq.Y + 22` | `20` |
| `lblSecTrazaChq` | `cntKvDepositoChq.Y + cntKvDepositoChq.Height + 14` | `16` |
| `cntKvEnvPorChq` | `lblSecTrazaChq.Y + lblSecTrazaChq.Height + 8` | `20` |
| `cntKvEnvElChq` | `cntKvEnvPorChq.Y + 22` | `20` |
| `cntKvRecPorChq` | `cntKvEnvElChq.Y + 22` | `20` |
| `cntKvRecElChq` | `cntKvRecPorChq.Y + 22` | `20` |

> Como `lblFeedbackChq` ocupa su franja aunque esté oculto, los controles siguientes se
> desplazan con `fbOffset` (no con el flujo). `cntKvRecPorChq`/`cntKvRecElChq` son los
> **últimos** y condicionalmente ocultos: invisibles dejan aire al pie, sin hueco intermedio.

**Patrón de par kv** (cada `cntKv{Campo}Chq`, Contenedor manual, Width `Parent.Width - 32`,
Height `20`): dos etiquetas —

| Hijo | X / Y | Width / Align | Estilo |
|---|---|---|---|
| `lblK{Campo}Chq` | `0` / `0` | `120` / Left | ESTILO-DATO-K |
| `lblV{Campo}Chq` | `128` / `0` | `Parent.Width - 128` / Right | ESTILO-DATO-V |

#### 6.3.1 Banner de resultado — `lblFeedbackChq`

| Propiedad | Valor |
|---|---|
| Visible | `ctxFeedbackChq <> ""` |
| Text | `ctxFeedbackMsgChq` |
| Fill | `If(ctxFeedbackChq = "ok", ThemeOkSoft, ThemeDangerSoft)` |
| Color | `If(ctxFeedbackChq = "ok", ThemeOk, ThemeDanger)` |
| Size / Wrap | `9` / `true` |
| PaddingLeft/Right/Top/Bottom | `12` / `12` / `9` / `9` |

> Alto fijo `40` (no AutoHeight) para que `fbOffset` sea determinista; con `Wrap true` el
> mensaje corto entra holgado. Si el motivo del guard es larguísimo, recorta — aceptable
> para un banner (el motivo completo va igual en `ctxFeedbackMsgChq`).

#### 6.3.2 Datos del cheque

`lblSecDatosChq` (Etiqueta clásica): Text `"DATOS DEL CHEQUE"` · ESTILO-SECCION.

| Par (`cntKv…Chq`) | K (Text) | V (Text) |
|---|---|---|
| `cntKvNumChq` | `"Número"` | `Coalesce(gvarChq.CheckNumber, "—")` |
| `cntKvMontoChq` | `"Monto"` | `Text(gvarChq.CheckAmount, FmtMoney)` |
| `cntKvMonedaChq` | `"Moneda"` | `Coalesce(gvarChq.CheckCurrency, "—")` |
| `cntKvTipoChq` | `"Tipo"` | `Coalesce(gvarChq.CheckTypes, "—")` |
| `cntKvClienteChq` | `"Cliente"` | `Coalesce(gvarChq.RelatedReCounterDepositID.ClientName, "—")` |
| `cntKvDepositoChq` | `"Depósito"` | `Coalesce(gvarChq.RelatedReCounterDepositID.Name, "—")` |

#### 6.3.3 Trazabilidad

`lblSecTrazaChq` (Etiqueta clásica): Text `"TRAZABILIDAD"` · ESTILO-SECCION.

| Par (`cntKv…Chq`) | K (Text) | V (Text) | Visible del contenedor |
|---|---|---|---|
| `cntKvEnvPorChq` | `"Enviado por"` | `Coalesce(gvarChq.SentToDigitizationBy.'Full Name', "—")` | *(siempre)* |
| `cntKvEnvElChq` | `"Enviado a digitación"` | `Text(gvarChq.'Enviado el', FmtFecha)` | *(siempre)* |
| `cntKvRecPorChq` | `If(gvarChq.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', "No recibido por", "Recibido por")` | `Coalesce(gvarChq.ReceivedBy.'Full Name', "—")` | `gvarChq.CheckStatus <> 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'` |
| `cntKvRecElChq` | `If(gvarChq.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', "No recibido el", "Recibido el")` | `Text(If(gvarChq.CheckStatus = 'CheckStatus (CH-Tbl-Checks)'.Recibido, gvarChq.'Recibido el', gvarChq.'No recibido el'), FmtFecha)` | `gvarChq.CheckStatus <> 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion'` |

> La `Visible` de los dos últimos pares va en el **Contenedor** `cntKvRecPorChq` /
> `cntKvRecElChq`. **Autoría real**: `SentToDigitizationBy` (`sanic_senttodigitizationby`) y
> `ReceivedBy` (`sanic_receivedby`) ya existen (00 §3.3/§9); "Enviado por" / "Recibido por"
> salen de esos lookups, no de `Created By`/`Modified By`. Fechas reales.

### 6.4 Sin selección — `cmpDetVacioChq` (Componente cmpEmpty)

Instancia de `cmpEmpty` (01 §8.6), superpuesta a toda la card de detalle:

| Propiedad (instancia) | Fórmula |
|---|---|
| X / Y | `0` / `0` |
| Width / Height | `Parent.Width` / `Parent.Height` |
| Visible | `IsBlank(gvarChq)` |
| `Glifo` | `"🧾"` |
| `Mensaje` | `"Seleccioná un cheque de la lista para ver su detalle y su trazabilidad."` |

## 7. Notas de backend y divergencias declaradas

**Dependencias de backend:**

- **Autoría real** (`SentToDigitizationBy` / `sanic_senttodigitizationby`, `ReceivedBy` /
  `sanic_receivedby`) — **ya existen** (00 §3.3/§9). La grilla y la trazabilidad los usan
  directo; no queda fallback de `Created By`/`Modified By`.
- **Conteos exactos de segmento** — `CountRows` no delega (tope 500). Fallback vigente: el
  badge queda tapado > 500; el conteo fiel lo daría el agregado server-side de la familia
  `sanic_CHWorkloadByCashier` (00 §3.3). Sigue siendo la única dependencia abierta.

**Ledger de divergencias vs mockup:**

- **L-Chq-1** — Selección por `gvarChq`, **no** por `ThisItem.IsSelected`: `gvarChq` se setea
  programáticamente en `OnVisible`, al cambiar de segmento y en el auto-avance tras `Patch`;
  `IsSelected` solo conoce clics dentro de la galería y no reflejaría esos casos.
- **L-Chq-2** — Fecha de la grilla: **hora** en Enviados (cola del día, como el mockup) pero
  **fecha completa** (`FmtFecha`) en Recibidos/No recibidos: los segmentos históricos abarcan
  varios días y la hora sola sería ambigua.
- **L-Chq-3** — CH3 (estado vacío) se arma con el componente `cmpEmpty` (no tiene sección
  propia en el mockup; copy que orienta por segmento, §5.3).
- **L-Chq-4** — Reflow por fórmula (paradigma manual, 01 §7.0): al ocultarse la barra de
  acciones (Recibidos/No recibidos), `cntDetBodyChq.Y` sube por `If`, no por auto-layout; el
  banner desplaza el contenido con `fbOffset`. El detalle **no** tiene scroll nativo (un
  Contenedor manual no hace overflow): el contenido (banner + 6 datos + 4 traza) cabe en el
  alto de tablet. Si un build necesitara scroll, envolver el cuerpo en una galería de 1
  template — no requerido hoy.
- **L-Chq-5** — Heredadas de 01: sin `DropShadow` en superficies de card (L-1); sin spinner
  animado durante `Patch` (L-3) — el feedback es el banner.

## 8. Checklist de salida

- [ ] Header = `cmpHeaderChq` con `Activo="Cheques"`: pastilla Cheques prendida, las otras
      tres navegan por behavior props; chip de usuario con `UsuarioNombre`.
- [ ] Layout **manual**: cero contenedores de auto-ajuste, cero `FillPortions`; todo X/Y/W/H
      es fórmula relativa a `Parent.Width`/`Parent.Height` + `Lay*`. `cntMainChq` ocupa el
      ancho completo (sin rail).
- [ ] Card lista **1.5×** más ancha que la de detalle (`0.6` vs `0.4` del ancho útil), como CH1.
- [ ] Entrar a scrCheques: segmento **Enviados** activo, primer cheque preseleccionado,
      detalle poblado.
- [ ] Los tres segmentos filtran (choice propia → **cero banda azul** en el Filter interno de
      `galChequesChq`).
- [ ] Buscar por N° de cheque y por cliente/depósito refina la lista en vivo (client-side
      sobre la página; sin error de delegación bloqueante).
- [ ] En **Enviados**, un cheque seleccionado muestra **Recibido** (primario) y **No recibido**
      (fantasma); en Recibidos/No recibidos la barra **no aparece** y el cuerpo sube (reflow por `If`).
- [ ] `Recibido` mueve el cheque fuera de Enviados, muestra banner OK y auto-avanza al siguiente
      pendiente; sin `Refresh` de tabla.
- [ ] `No recibido` idem con estado `'No Recibido'`.
- [ ] Un rechazo del `CheckGuard` (forzado) muestra banner de **peligro** con el motivo —
      nunca mudo (C-2).
- [ ] Doble-click en una acción no la ejecuta dos veces (`ctxBusyChq`).
- [ ] Segmento sin filas → `cmpVacioChq` con el mensaje del segmento; detalle con
      `cmpDetVacioChq`.
- [ ] Trazabilidad: "Enviado por/el" siempre (desde `SentToDigitizationBy`); "Recibido/No
      recibido por/el" solo en cheques resueltos (desde `ReceivedBy`), con la etiqueta correcta.
- [ ] Píldora de estado de **fila** (`pilFilaEstadoChq`) = **Botón clásico** nativo (NO
      componente — `PA2122`); la de **detalle** (`pilDetEstadoChq`) = `cmpPildora`. Ambas
      `info`/`ok`/`danger` según estado.
- [ ] App Checker: cero errores; `AccessibleLabel` en overlay de fila, buscador y botones de acción.
```