# Guía 02 — scrBusqueda

Pantalla de entrada: búsqueda por identificador + lotes recientes.

## Árbol de controles

```
scrBusqueda                       (Fill: ThemeBg)
├── shpHeaderBusq                 rectángulo header (receta 7.5)
├── shpHeaderLineaBusq
├── shpMarcaBusq
├── lblMarcaBusq                  "CashHub"
├── lblMarcaSubBusq               "· Tracking de Depósitos"
├── lblHeroTitulo                 "¿Qué querés rastrear?"
├── lblHeroSub                    subtítulo
├── drpTipoBusqueda               tipo de identificador
├── inpBuscar                     identificador
├── btnBuscar                     botón Buscar
├── lblErrorBusqueda              error inline (visible solo con error)
├── shpRecientesCard              card (receta 7.1)
├── lblRecientesTitulo            "LOTES DE RECEPCIÓN RECIENTES"
├── lblColLote ─ lblColRecibido ─ lblColPor ─ lblColTransp ─ lblColNio ─ lblColUsd ─ lblColEstado
├── shpRecientesLinea             línea bajo encabezados
└── galRecientes                  galería vertical en blanco
    ├── btnFilaAbrir              overlay clicable (toda la fila)
    ├── lblFilaLote
    ├── lblFilaFecha
    ├── lblFilaPor
    ├── lblFilaTransp
    ├── lblFilaNio
    ├── lblFilaUsd
    ├── pilFilaEstado
    └── shpFilaSep
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | `UpdateContext({ctxError: ""})` |

Insertá el encabezado de la **receta 7.5 — 5 controles**, con el sufijo `Busq`
de la regla 0.5: `shpHeaderBusq` (Rectángulo clásico), `shpHeaderLineaBusq`
(Rectángulo clásico), `shpMarcaBusq` (Botón clásico), `lblMarcaBusq` (Etiqueta
clásica) y `lblMarcaSubBusq` (Etiqueta clásica); propiedades fijadas en la
guía 01. *(Esta pantalla no tiene migas: es la raíz.)*

## 2. Hero

| Control | Propiedad | Valor |
|---|---|---|
| `lblHeroTitulo` (Etiqueta clásica) | X/Y/W/H | `343` / `92` / `680` / `40` |
| | Text | `"¿Qué querés rastrear?"` |
| | Estilo | ESTILO-TITULO con Size `17` (override deliberado del `15` del alias — es el título hero de la pantalla de entrada) · Align `Align.Center` |
| `lblHeroSub` (Etiqueta clásica) | X/Y/W/H | `343` / `132` / `680` / `24` |
| | Text | `"Buscá por tracking del correo, lote de recepción, planilla o tula. Te llevamos directo al nivel que corresponde."` |
| | Color / Size / Align | `ThemeInk2` / `9.5` / `Align.Center` |

## 3. Buscador

| Control | Propiedad | Valor |
|---|---|---|
| `drpTipoBusqueda` (Menú desplegable moderno) | X/Y/W/H | `343` / `170` / `190` / `40` |
| | Items | `["Tracking (correo)", "Lote de recepción", "Planilla", "Tula"]` |
| | DefaultSelectedItems | `["Tracking (correo)"]` |
| | AccessibleLabel | `"Tipo de identificador"` |
| `inpBuscar` (Entrada de texto moderna) | X/Y/W/H | `541` / `170` / `360` / `40` |
| | Placeholder | `Switch(drpTipoBusqueda.Selected.Value, "Tracking (correo)", "Ej.: Email-1042", "Lote de recepción", "Ej.: RB-00012", "Planilla", "Número de planilla (ReceiptID)", "Tula", "Número de tula impreso en la bolsa")` |
| | AccessibleLabel | `"Identificador a buscar"` |
| `btnBuscar` (Botón moderno) | X/Y/W/H | `909` / `170` / `114` / `40` |
| | Text | `"Buscar"` |
| | BasePaletteColor | `ThemeAccent` *(el botón moderno se colorea con BasePaletteColor — NO tiene Fill)* |
| | OnSelect | fórmula §3.1 |
| `lblErrorBusqueda` (Etiqueta clásica) | X/Y/W/H | `343` / `218` / `680` / `40` |
| | Text | `ctxError` |
| | Visible | `!IsBlank(ctxError)` |
| | Color / Fill | `ThemeWarn` / `ThemeWarnSoft` |
| | Size / PaddingLeft / PaddingTop | `9` / `12` / `6` |

> **Placeholders — divergencia vs mock deliberada**: los ejemplos PL-2044 /
> T-30291 del mockup eran ficción, y `T-` es el autonumber real de
> **TRANSACCIONES** — se usan descripciones ("Número de planilla (ReceiptID)",
> "Número de tula impreso en la bolsa") para no entrenar confusión.

### 3.1 btnBuscar.OnSelect (pegar completo)

```powerfx
UpdateContext({ctxError: ""});
With({_q: Trim(inpBuscar.Value)},
If(IsBlank(_q),
    UpdateContext({ctxError: "Escribí un identificador para buscar."}),
    Switch(drpTipoBusqueda.Selected.Value,

    "Tracking (correo)",
        With({_c: LookUp('CH-Tbl-Received Emails', Name = _q)},
        If(IsBlank(_c),
            UpdateContext({ctxError: $"Tracking ""{_q}"" no encontrado. Verificá el identificador (formato Email-N)."}),
            Set(gvarCorreo, _c); Navigate(scrCorreo))),

    "Lote de recepción",
        With({_rb: LookUp('CH-Tbl-ReceptionBatches', Name = _q)},
        If(IsBlank(_rb),
            UpdateContext({ctxError: $"Lote ""{_q}"" no encontrado. Verificá el identificador (formato RB-N)."}),
            Set(gvarLote, _rb); Set(gvarPlanilla, Blank()); Navigate(scrLote))),

    "Planilla",
        With({_p: LookUp('CH-Tbl-Receptions', ReceiptID = _q)},
        If(IsBlank(_p),
            UpdateContext({ctxError: $"Planilla ""{_q}"" no encontrada. Verificá el identificador."}),
        If(IsBlank(_p.ReceptionBatch),
            UpdateContext({ctxError: $"La planilla ""{_q}"" existe pero no está asociada a ningún lote de recepción."}),
            Set(gvarLote, _p.ReceptionBatch); Set(gvarPlanilla, _p); Navigate(scrLote)))),

    "Tula",
        With({_t: LookUp('CH-Tbl-Bags', BagID = _q)},
        If(IsBlank(_t),
            UpdateContext({ctxError: $"Tula ""{_q}"" no encontrada. Verificá el identificador."}),
            Set(gvarTula, _t);
            Set(gvarPlanilla, _t.ReceptionID);
            Set(gvarLote, _t.ReceptionID.ReceptionBatch);
            Navigate(scrTula)))
    )
))
```

> Los cuatro `LookUp` delegan (igualdad simple contra columna indexada, sin
> `Upper()`: Dataverse compara sin distinguir mayúsculas). Si el nombre de
> columna que te ofrece IntelliSense difiere (`Name`, `ReceiptID`, `BagID`,
> `ReceptionBatch`, `ReceptionID`), usá el de IntelliSense y avisame.

> El lookup de la planilla a su lote es *recommended* (no required) en el
> esquema: por eso la rama Planilla tiene guard extra para planillas huérfanas.

> **Mensajes de error sin "(Probá …)"**: el mock cerraba cada error con un
> ejemplo accionable. Acá no: planilla y tula NO tienen formato fijo, así que un
> ejemplo sería inventado. Tracking y lote sí lo conservan (formato Email-N /
> RB-N). Divergencia declarada.

**Enter para buscar** — decisión: el usuario hace clic en Buscar. Si querés
Enter, la opción es `inpBuscar.OnChange = Select(btnBuscar)` (el OnChange del
input moderno dispara al confirmar), PERO también dispara al perder el foco —
una búsqueda accidental por clic afuera. Lo dejamos SIN OnChange; si en el uso
real extrañás el Enter, lo revisamos.

## 4. Lotes recientes

| Control | Propiedad | Valor |
|---|---|---|
| `shpRecientesCard` (Rectángulo clásico) | X/Y/W/H | `123` / `290` / `1120` / `420` — receta 7.1 |
| `lblRecientesTitulo` (Etiqueta clásica) | X/Y/W/H | `147` / `306` / `500` / `20` |
| | Text | `"LOTES DE RECEPCIÓN RECIENTES"` · ESTILO-SECCION |

Encabezados de columna `lblColLote`, `lblColRecibido`, `lblColPor`,
`lblColTransp`, `lblColNio`, `lblColUsd`, `lblColEstado`: **Etiquetas clásicas,
ESTILO-HEADER-COL** (Y `338`, Height `18`). Los números X/Width valen también
para los labels de la fila:

| Control | Text | X | Width | Align |
|---|---|---|---|---|
| `lblColLote` | `"LOTE"` | `147` | `110` | Left |
| `lblColRecibido` | `"RECIBIDO"` | `257` | `150` | Left |
| `lblColPor` | `"RECIBIDO POR"` | `407` | `180` | Left |
| `lblColTransp` | `"TRANSPORTADORA"` | `587` | `150` | Left |
| `lblColNio` | `"DECL. C$"` | `737` | `140` | Right |
| `lblColUsd` | `"DECL. US$"` | `877` | `140` | Right |
| `lblColEstado` | `"ESTADO"` | `1037` | `170` | Left (X del pill: `1037`) |

> `DECL. C$` / `DECL. US$` unifican con la grilla de planillas de la guía 03
> (`lblPCol3`/`lblPCol4`). El mock decía "Total C$ / Total US$" — se cambió a
> "DECL." porque son montos DECLARADOS, no recontados.

`shpRecientesLinea` (Rectángulo clásico): X `147` · Y `358` · Width `1072` ·
Height `1` · Fill `ThemeLine`.

### galRecientes (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `147` / `360` / `1072` / `336` |
| Items | `FirstN(Sort('CH-Tbl-ReceptionBatches', 'Created On', SortOrder.Descending), 8)` |
| TemplateSize / TemplatePadding | `42` / `0` |
| ShowScrollbar | `true` |

Dentro del template (todos los labels: Y `10`, Height `22`, ESTILO-CELDA; X y
Width de la tabla de arriba, relativos al template — restá `147`):

| Control | Propiedad | Valor |
|---|---|---|
| `btnFilaAbrir` (Botón clásico, PRIMERO para que quede al fondo) | X/Y/W/H | `0` / `0` / `1072` / `42` |
| | Text / BorderThickness | `""` / `0` |
| | Fill / HoverFill / PressedFill | `Transparent` / `ThemeSurface2` / `ThemeAccentSoft` (receta 7.3) |
| | OnSelect | `Set(gvarLote, ThisItem); Set(gvarPlanilla, Blank()); Navigate(scrLote)` |
| | AccessibleLabel | `"Abrir lote " & ThisItem.Name` |
| `lblFilaLote` (Etiqueta clásica) | Text / FontWeight | `ThisItem.Name` / `FontWeight.Semibold` |
| `lblFilaFecha` (Etiqueta clásica) | Text | `Text(ThisItem.'Created On', FmtFecha)` |
| `lblFilaPor` (Etiqueta clásica) | Text | `ThisItem.'Created By'.'Full Name'` |
| `lblFilaTransp` (Etiqueta clásica) | Text | `ThisItem.Transportadora` |
| `lblFilaNio` (Etiqueta clásica) | Text / Align | `Text(Coalesce(ThisItem.DecNIOTotal, 0), FmtMoney)` / `Align.Right` |
| `lblFilaUsd` (Etiqueta clásica) | Text / Align | `Text(Coalesce(ThisItem.DecUSDTotal, 0), FmtMoney)` / `Align.Right` |
| `pilFilaEstado` (Botón clásico, receta 7.2) | X/Y/W/H | `890` / `8` / `150` / `26` |
| | Text | `ThisItem.ReceptionStatus` |
| | Fill | `Switch(ThisItem.ReceptionStatus, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso', ThemeWarnSoft, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido, ThemeInfoSoft, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda', ThemeOkSoft, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado, ThemeCritSoft, ThemeSurface2)` |
| | Color | `Switch(ThisItem.ReceptionStatus, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso', ThemeWarn, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido, ThemeInfo, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda', ThemeOk, 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado, ThemeCrit, ThemeInk2)` |
| `shpFilaSep` (Rectángulo clásico) | X/Y/W/H | `0` / `41` / `1072` / `1` · Fill `ThemeLineSoft` |

> `DecNIOTotal` / `DecUSDTotal` son las columnas del backend **B4** (displays
> exactos fijados en 00-§3). Hasta que el backend esté desplegado, dejá esos
> dos labels con Text `"—"` y seguí; se conectan al final.
>
> El nombre exacto del enum del estado (`'ReceptionStatus (CH-Tbl-ReceptionBatches)'`)
> te lo da IntelliSense al escribir `'ReceptionStatus`. Aceptá el que ofrezca.

## 5. Checklist de salida

- [ ] Buscar cada tipo con identificador válido navega a la pantalla correcta.
- [ ] Cada tipo con identificador inválido muestra `lblErrorBusqueda` y NO navega.
- [ ] Campo vacío + Buscar → mensaje "Escribí un identificador…".
- [ ] Clic en una fila reciente abre scrLote.
- [ ] Cero íconos de delegación (banda azul) en `galRecientes` ni en el OnSelect.
