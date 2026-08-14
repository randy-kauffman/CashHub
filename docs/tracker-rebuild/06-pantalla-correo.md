# Guía 06 — scrCorreo

La raíz de la trazabilidad: el correo del cliente, sus validaciones, la
declaración que generó, los cheques declarados y la respuesta enviada.
**Buscar por tracking SIEMPRE aterriza acá.**

**Contrato de entrada**: `gvarCorreo` con un registro COMPLETO de
CH-Tbl-Received Emails (siempre seteado con `LookUp`, nunca con un lookup
navegado a medias).

## Árbol de controles

```
scrCorreo                          (Fill: ThemeBg)
├── shpHeaderCorr / shpHeaderLineaCorr / shpMarcaCorr / lblMarcaCorr / lblMarcaSubCorr   (receta 7.5)
├── lblCrumbBusquedaCorr ─ lblCrumbSep1Corr ─ lblCrumbActualCorr
├── shpFichaCardCorr
│   ├── lblCorreoTitulo            "Correo Email-…"
│   ├── pilCorreoResultado         estado de revisión
│   └── lblFichaK1Corr..K6Corr / lblFichaV1Corr..V6Corr
├── shpDetalleCardCorr
│   ├── tabDetalleCorreo           4 pestañas
│   ├── ── pestaña Validaciones ──
│   │   ├── lblVCol1..4 / shpValsLinea
│   │   ├── galVals
│   │   │   └── lblVFilaRegla ─ lblVFilaNombre ─ pilVFilaResultado ─ lblVFilaDetalle ─ shpVFilaSep
│   │   └── lblValsVacio           (correo sin corridas)
│   ├── ── pestaña Declaración ──
│   │   ├── lblDeclVacio           (correo rechazado: sin declaración)
│   │   ├── lblBolsasTitulo / lblBCol1..7 / shpBolsasLinea
│   │   ├── galBolsas
│   │   │   └── lblBFilaBolsa ─ lblBFilaDeps ─ lblBFilaNio ─ lblBFilaUsd ─ lblBFilaEur ─ lblBFilaTula ─ pilBFilaRecuento
│   │   ├── lblDDepsTitulo / lblYCol1..7 / shpDDepsLinea
│   │   └── galDDeps
│   │       └── lblYFilaRef ─ lblYFilaBolsa ─ lblYFilaMon ─ lblYFilaEf ─ lblYFilaChq ─ lblYFilaTotal ─ lblYFilaRecuento
│   ├── ── pestaña Cheques declarados ──
│   │   ├── lblKCol1..5 / shpDChequesLinea
│   │   ├── galDCheques
│   │   │   └── lblKFilaNo ─ lblKFilaTipo ─ lblKFilaMon ─ lblKFilaMonto ─ lblKFilaRel
│   │   └── lblDChequesVacio
│   └── ── pestaña Respuesta al cliente ──
│       ├── lblRespK1..K3 / lblRespV1..V3
│       └── htmlRespuesta          (Texto HTML clásico)
```

## 1. Pantalla

Fill `ThemeBg`. **OnVisible** (pegar completo):

```powerfx
Set(gvarRun,
    First(Sort(
        Filter('CH-Tbl-ValidationRuns',
            ReceivedEmail.'CH-Tbl-Received Emails' = gvarCorreo.'CH-Tbl-Received Emails'),
        RunStart, SortOrder.Descending)));

Set(gvarDecl,
    LookUp('CH-Tbl-Declarations',
        ReceivedEmail.'CH-Tbl-Received Emails' = gvarCorreo.'CH-Tbl-Received Emails'));

Concurrent(
    ClearCollect(colVals,
        AddColumns(
            Filter('CH-Tbl-ValidationResults',
                ValidationRun.'CH-Tbl-ValidationRuns' = gvarRun.'CH-Tbl-ValidationRuns'),
            "regla",       ValidationRule.'Validation Name',
            "reglaNombre", ValidationRule.Name,
            "reglaOrden",  ValidationRule.ValidationOrder)),
    ClearCollect(colAdjuntos,
        Filter('CH-Tbl-EmailsAttachments',
            Email.'CH-Tbl-Received Emails' = gvarCorreo.'CH-Tbl-Received Emails'))
);

If(IsBlank(gvarDecl),
    Clear(colBolsas); Clear(colDDeps); Clear(colDCheques),
    Concurrent(
        ClearCollect(colBolsas,
            Filter('CH-Tbl-DeclaredBags',
                DeclarationID.'CH-Tbl-Declaration' = gvarDecl.'CH-Tbl-Declaration')),
        ClearCollect(colDDeps,
            AddColumns(
                Filter('CH-Tbl-DeclaredDeposits',
                    Declaration.'CH-Tbl-Declaration' = gvarDecl.'CH-Tbl-Declaration'),
                "bolsaNumero", DeclaredBag.BagNumber)),
        ClearCollect(colDCheques,
            Filter('CH-Tbl-DeclaredChecks',
                Declaration.'CH-Tbl-Declaration' = gvarDecl.'CH-Tbl-Declaration'))
    ));

// cada correo arranca en Validaciones
Reset(tabDetalleCorreo)
```

> - `ReceivedEmail` en Declarations es **B1**; `Declaration` en
>   DeclaredDeposits/DeclaredChecks es **B7**. Sin backend, provisorio NO
>   delegable, marcado con TODO: depósitos con
>   `Filter('CH-Tbl-DeclaredDeposits', DeclaredBag.DeclarationID.'CH-Tbl-Declaration' = …)`
>   y cheques con
>   `Filter('CH-Tbl-DeclaredChecks', DeclaredDepositID.DeclaredBag.DeclarationID.'CH-Tbl-Declaration' = …)`
>   (los cheques declarados NO tienen lookup a la bolsa: su única cadena es
>   vía `DeclaredDepositID`, 3 saltos).
> - `RunStart` es UNA palabra (display real del esquema); `Detalle`, `Hoja`,
>   `Celda` y `Fila` en ValidationResults están en ESPAÑOL.
> - Si el correo fue rechazado, `gvarDecl` queda en blanco y la pestaña
>   Declaración muestra `lblDeclVacio`.

## 2. Migas y ficha

Encabezado: **receta 7.5 — 5 controles**, con el sufijo `Corr` de la regla 0.5:
`shpHeaderCorr` (Rectángulo clásico), `shpHeaderLineaCorr` (Rectángulo clásico),
`shpMarcaCorr` (Botón clásico), `lblMarcaCorr` (Etiqueta clásica) y
`lblMarcaSubCorr` (Etiqueta clásica); propiedades fijadas en la guía 01.

Migas (receta 7.6, Y `64`, Height `22`, Size `9`) — los **tres** controles son
**Etiquetas clásicas**: `lblCrumbBusquedaCorr` (`24`/`70`, → `Navigate(scrBusqueda)`) ·
`lblCrumbSep1Corr` (`94`/`14`, Text `"›"`, Color `ThemeInk3`) ·
`lblCrumbActualCorr` (`108`/`220`, `$"Correo {gvarCorreo.Name}"`, Color `ThemeInk2`).

| Control | Propiedad | Valor |
|---|---|---|
| `shpFichaCardCorr` (Rectángulo clásico) | X/Y/W/H | `24` / `94` / `1318` / `150` — receta 7.1 |
| `lblCorreoTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `108` / `300` / `30` · Text `$"Correo {gvarCorreo.Name}"` · ESTILO-TITULO |
| `pilCorreoResultado` (Botón clásico, receta 7.2) | X/Y/W/H | `350` / `110` / `230` / `26` |
| | Text | `gvarCorreo.EmailReviewStatus` |
| | Fill | `Switch(gvarCorreo.EmailReviewStatus, 'EmailReviewStatus (CH-Tbl-Received Emails)'.Aceptado, ThemeOkSoft, 'EmailReviewStatus (CH-Tbl-Received Emails)'.Denegado, ThemeCritSoft, 'EmailReviewStatus (CH-Tbl-Received Emails)'.Error, ThemeCritSoft, 'EmailReviewStatus (CH-Tbl-Received Emails)'.'Revision manual', ThemeWarnSoft, 'EmailReviewStatus (CH-Tbl-Received Emails)'.Pendiente, ThemeWarnSoft, ThemeSurface2)` |
| | Color | mismo Switch con `ThemeOk / ThemeCrit / ThemeCrit / ThemeWarn / ThemeWarn / ThemeInk2` |

> El pill muestra el label CRUDO del option set ("Aceptado", "Denegado"…), no
> los textos del prototipo ("Declaración aceptada/rechazada") — el modelo
> manda. Divergencia declarada.

> **Par 5 (RESPUESTA) — mejora deliberada sobre el mock**, que solo mostraba la
> fecha: acá el estado va enriquecido (Enviada · fecha / Omitida (triage) /
> Pendiente de envío), que es lo que de verdad distingue los tres casos.

Pares dato (fila 1: K Y `148` / V Y `164` · fila 2: K Y `194` / V Y `210`).
Las claves son **Etiquetas clásicas** `lblFichaK1Corr`..`lblFichaK6Corr`
(ESTILO-DATO-K, Height `16`) y los valores **Etiquetas clásicas**
`lblFichaV1Corr`..`lblFichaV6Corr` (ESTILO-DATO-V, Height `22`); el par N es
`lblFichaKNCorr` / `lblFichaVNCorr`:

| Par | K | V (Text) | X / W |
|---|---|---|---|
| 1 | `"CLIENTE"` | `Coalesce(gvarCorreo.'Cliente atribuido'.Name, "Sin atribuir")` *(si IntelliSense llama distinto al lookup del cliente, usá el suyo)* | `44` / `260` |
| 2 | `"REMITENTE"` | `gvarCorreo.SourceEmailAddress` | `324` / `280` |
| 3 | `"RECIBIDO EL"` | `Text(gvarCorreo.ReceptionDateTime, FmtFecha)` | `624` / `170` |
| 4 | `"ADJUNTO"` | `If(CountRows(colAdjuntos) = 0, "—", "📎 " & First(colAdjuntos).Filename & If(CountRows(colAdjuntos) > 1, $" (+{CountRows(colAdjuntos) - 1})", ""))` *(display real: `Filename`, n minúscula)* | `814` / `300` |
| 5 | `"RESPUESTA"` | `Switch(gvarCorreo.EmailReplyStatus, 'EmailReplyStatus (CH-Tbl-Received Emails)'.Respondido, $"Enviada · {Text(gvarCorreo.ReplyDateTime, FmtFecha)}", 'EmailReplyStatus (CH-Tbl-Received Emails)'.Omitido, "Omitida (triage)", "Pendiente de envío")` | `44` / `280` |
| 6 | `"BOLSAS DECLARADAS"` | `If(IsBlank(gvarDecl), "—", Text(CountRows(colBolsas)))` | `344` / `170` |

## 3. Card de detalle + pestañas

| Control | Propiedad | Valor |
|---|---|---|
| `shpDetalleCardCorr` (Rectángulo clásico) | X/Y/W/H | `24` / `256` / `1318` / `488` — receta 7.1 |
| `tabDetalleCorreo` (Lista de pestañas moderna) | X/Y/W/H | `44` / `268` / `760` / `40` |
| | Items | `["Validaciones", "Declaración", "Cheques declarados", "Respuesta al cliente"]` |
| | DefaultSelectedItems | `["Validaciones"]` |

Todos los controles de cada bloque llevan
`Visible: tabDetalleCorreo.Selected.Value = "<pestaña>"`.

## 4. Pestaña Validaciones

Encabezados `lblVCol1`..`lblVCol4`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `320`): `lblVCol1` `"REGLA"` X `44` W `100` ·
`lblVCol2` `"DESCRIPCIÓN"` X `144` W `430` · `lblVCol3` `"RESULTADO"` X `574`
W `130` · `lblVCol4` `"DETALLE"`
X `704` W `600`. `shpValsLinea` (Rectángulo clásico) X `44` Y `340` W `1260`
H `1` · Fill `ThemeLine`.

### galVals (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `342` / `1260` / `380` · TemplateSize `44` |
| Items | `SortByColumns(colVals, "reglaOrden", SortOrder.Ascending)` |

Template (labels Y `11` H `22`, **salvo `lblVFilaNombre` y `lblVFilaDetalle`**,
que van a Y `2` H `40` con Wrap — el nombre de regla y el detalle con
hoja!celda no se pueden cortar, y `2 + 40` entra en el TemplateSize de `44`):

| Control | Propiedad | Valor |
|---|---|---|
| `lblVFilaRegla` (Etiqueta clásica) | X/W | `0` / `100` · Text `ThisItem.regla` · Semibold |
| `lblVFilaNombre` (Etiqueta clásica) | X/Y/W/H | `100` / `2` / `430` / `40` · Text `ThisItem.reglaNombre` · Wrap `true` |
| `pilVFilaResultado` (Botón clásico, receta 7.2) | X/Y/W/H | `530` / `9` / `110` / `26` |
| | Text | `Switch(ThisItem.ValidationResult, 'ValidationResult (CH-Tbl-ValidationResults)'.Pass, "Pasó", 'ValidationResult (CH-Tbl-ValidationResults)'.Fail, "Falló", "Omitida")` |
| | Fill | `Switch(ThisItem.ValidationResult, 'ValidationResult (CH-Tbl-ValidationResults)'.Pass, ThemeOkSoft, 'ValidationResult (CH-Tbl-ValidationResults)'.Fail, ThemeCritSoft, ThemeSurface2)` |
| | Color | mismo Switch con `ThemeOk / ThemeCrit / ThemeInk2` |
| `lblVFilaDetalle` (Etiqueta clásica) | X/Y/W/H | `660` / `2` / `600` / `40` · Wrap `true` |
| | Text | `Coalesce(ThisItem.Detalle, "—") & If(!IsBlank(ThisItem.Hoja), $"  ·  {ThisItem.Hoja}" & If(!IsBlank(ThisItem.Celda), $"!{ThisItem.Celda}", ""), "")` |
| | Size / Color | `8.5` / `ThemeInk2` |
| `shpVFilaSep` (Rectángulo clásico) | Y `43` · H `1` · W `1260` · Fill `ThemeLineSoft` |

> El valor "Omitida" del option set es **B2**. Sin backend solo verás Pasó/Falló
> (las reglas omitidas no generan fila — comportamiento actual del plugin).
> Un `Falló` puede repetir la MISMA regla varias veces: es un resultado POR
> HALLAZGO (hoja/celda/fila distintos). Es correcto, no es un bug.

`lblValsVacio` (Etiqueta clásica — X `44` · Y `420` · W `1260` · H `40` · Align Center · Color `ThemeInk3`):
Text `"Este correo aún no tiene corridas de validación."` · Visible
`tabDetalleCorreo.Selected.Value = "Validaciones" && CountRows(colVals) = 0`
(pasa con correos en estado Pendiente o Error temprano).

## 5. Pestaña Declaración

| Control | Propiedad | Valor |
|---|---|---|
| `lblDeclVacio` (Etiqueta clásica) | X/Y/W/H | `44` / `420` / `1260` / `40` · Align Center · Color `ThemeInk3` |
| | Text | `"Este correo no generó declaración (fue rechazado por las validaciones)."` |
| | Visible | `tabDetalleCorreo.Selected.Value = "Declaración" && IsBlank(gvarDecl)` |

El resto del bloque lleva Visible
`tabDetalleCorreo.Selected.Value = "Declaración" && !IsBlank(gvarDecl)`.

`lblBolsasTitulo` (Etiqueta clásica — X `44` Y `318` W `400` H `18` ·
ESTILO-SECCION · Text `"BOLSAS DECLARADAS"`). Encabezados `lblBCol1`..`lblBCol7`:
**Etiquetas clásicas, ESTILO-HEADER-COL** (Y `342`): `lblBCol1` `"BOLSA"` X `44`
W `120` · `lblBCol2` `"DEPÓSITOS"` X `164` W `100` Right · `lblBCol3`
`"TOTAL C$"` X `264` W `140` Right · `lblBCol4` `"TOTAL US$"` X `404` W `140`
Right · `lblBCol5` `"TOTAL €"` X `544` W `120` Right · `lblBCol6`
`"TULA VINCULADA"` X `684` W `220` · `lblBCol7` `"RECUENTO"` X `904` W `240`.
`shpBolsasLinea` (Rectángulo clásico) X `44` Y `362` W `1160` H `1` ·
Fill `ThemeLine`.

### galBolsas (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `364` / `1160` / `120` · TemplateSize `40` |
| Items | `colBolsas` |

Template (labels Y `9`, H `22`; X/W = encabezado − `44`; `lblBCol1`→`lblBFilaBolsa`,
`lblBCol2`→`lblBFilaDeps`, `lblBCol3..5`→`lblBFilaNio`/`lblBFilaUsd`/`lblBFilaEur`,
`lblBCol6`→`lblBFilaTula`, `lblBCol7`→`pilBFilaRecuento`):

| Control | Propiedad | Valor |
|---|---|---|
| `lblBFilaBolsa` (Etiqueta clásica) | Text | `ThisItem.BagNumber` · Semibold |
| `lblBFilaDeps` (Etiqueta clásica) | Text / Align | `Text(Coalesce(ThisItem.TotalDeposits, 0))` / Right |
| `lblBFilaNio` / `lblBFilaUsd` / `lblBFilaEur` (Etiquetas clásicas) | Text / Align | `If(Coalesce(ThisItem.TotalNIO, 0) = 0, "—", Text(ThisItem.TotalNIO, FmtMoney))` (ídem USD/EUR) / Right |
| `lblBFilaTula` (Etiqueta clásica, LINK) | X/W | `640` / `220` |
| | Text | `If(IsBlank(ThisItem.BagID), "Sin tula vinculada todavía", ThisItem.BagID.BagID)` |
| | Color | `If(IsBlank(ThisItem.BagID), ThemeInk3, ThemeAccent)` |
| | Underline | `!IsBlank(ThisItem.BagID)` *(ESTILO-LINK solo cuando navega)* |
| | OnSelect | `If(!IsBlank(ThisItem.BagID), Set(gvarTula, LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ThisItem.BagID.'CH-Tbl-Bags')); Set(gvarPlanilla, gvarTula.ReceptionID); Set(gvarLote, gvarTula.ReceptionID.ReceptionBatch); Navigate(scrTula))` |
| `pilBFilaRecuento` (Botón clásico, receta 7.2) | X/Y/W/H | `860` / `7` / `240` / `26` |
| | Text | `If(IsBlank(ThisItem.BagID), "—", ThisItem.BagID.BagStatus)` |
| | Fill / Color | mismo Switch de estados de tula (guía 03 §5) sobre `ThisItem.BagID.BagStatus`, con default `ThemeSurface2`/`ThemeInk2` |

> `TotalDeposits` es rollup de Dataverse: se recalcula asíncrono (hasta ~1 h).
> Si el conteo importa fresco, contá client-side:
> `CountRows(Filter(colDDeps, bolsaNumero = ThisItem.BagNumber))`.

> **Columna TOTAL € propia**: corrección consciente del mock, que metía los EUR
> en la columna US$. El modelo tiene los tres totales separados; los mostramos
> separados.

> **Estados no-vinculados como texto plano**: "Sin tula vinculada todavía"
> (`lblBFilaTula`), "Pendiente" (`lblYFilaRecuento`) y "No" (`lblKFilaRel`, §6)
> van en texto `ThemeInk3`, no en pills mute como el mock — divergencia de forma
> declarada: son la ausencia de un link, no un estado del dominio.

`lblDDepsTitulo` (Etiqueta clásica — X `44` Y `496` W `400` H `18` ·
ESTILO-SECCION · Text `"DEPÓSITOS DECLARADOS"`). Encabezados `lblYCol1`..`lblYCol7`: **Etiquetas
clásicas, ESTILO-HEADER-COL** (Y `520`): `lblYCol1` `"REFERENCIA"` X `44` W `140`
· `lblYCol2` `"BOLSA"` X `184` W `100` ·
`lblYCol3` `"MONEDA"` X `284` W `90` · `lblYCol4` `"EFECTIVO"` X `374` W `130`
Right · `lblYCol5` `"CHEQUES"`
X `504` W `130` Right · `lblYCol6` `"TOTAL"` X `634` W `130` Right · `lblYCol7`
`"RECUENTO"` X `764`
W `260`. `shpDDepsLinea` (Rectángulo clásico) X `44` Y `540` W `1160` H `1` ·
Fill `ThemeLine`.

### galDDeps (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `542` / `1160` / `180` · TemplateSize `40` |
| Items | `colDDeps` |

Template (labels Y `9`, H `22`; X/W = encabezado − `44`; la columna N es
`lblYColN` → la fila de la tabla de abajo, en el mismo orden):

| Control | Propiedad | Valor |
|---|---|---|
| `lblYFilaRef` (Etiqueta clásica) | Text | `Coalesce(ThisItem.DepositReference, ThisItem.Name)` · Semibold |
| `lblYFilaBolsa` (Etiqueta clásica) | Text | `ThisItem.bolsaNumero` |
| `lblYFilaMon` (Etiqueta clásica) | Text | `ThisItem.Currency` |
| `lblYFilaEf` (Etiqueta clásica) | Text / Align | `Text(ThisItem.CashAmount, FmtMoney)` / Right |
| `lblYFilaChq` (Etiqueta clásica) | Text / Align | `Text(ThisItem.BACLocalCheckAmount + ThisItem.OtherLocalCheckAmount, FmtMoney)` / Right |
| `lblYFilaTotal` (Etiqueta clásica) | Text / Align | `Text(ThisItem.Total, FmtMoney)` / Right · Semibold |
| `lblYFilaRecuento` (Etiqueta clásica, LINK) | X/W | `720` / `260` |
| | Text | `If(IsBlank(ThisItem.RelatedRecountedDeposit), "Pendiente", $"Recontado en {ThisItem.RelatedRecountedDeposit.BagID.BagID}")` |
| | Color | `If(IsBlank(ThisItem.RelatedRecountedDeposit), ThemeInk3, ThemeAccent)` |
| | Underline | `!IsBlank(ThisItem.RelatedRecountedDeposit)` *(ESTILO-LINK solo cuando navega)* |
| | OnSelect | `If(!IsBlank(ThisItem.RelatedRecountedDeposit), Set(gvarTula, LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ThisItem.RelatedRecountedDeposit.BagID.'CH-Tbl-Bags')); Set(gvarPlanilla, gvarTula.ReceptionID); Set(gvarLote, gvarTula.ReceptionID.ReceptionBatch); Navigate(scrTula))` |

## 6. Pestaña Cheques declarados

Encabezados `lblKCol1`..`lblKCol5`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `320`): `lblKCol1` `"NO. CHEQUE"` X `44` W `160` · `lblKCol2` `"TIPO"` X `204`
W `160` · `lblKCol3` `"MONEDA"` X `364` W `90` · `lblKCol4` `"MONTO"` X `454`
W `140` Right · `lblKCol5`
`"RELACIONADO CON RECUENTO"` X `614` W `260`. `shpDChequesLinea` (Rectángulo
clásico) X `44` Y `340` W `1260` H `1` · Fill `ThemeLine`.

### galDCheques (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `342` / `1260` / `380` · TemplateSize `40` |
| Items | `colDCheques` |

Filas — los cinco son **Etiquetas clásicas** (Y `9`, H `22`; X/W = encabezado − `44`):
`lblKFilaNo` `ThisItem.CheckNumber` Semibold · `lblKFilaTipo`
`ThisItem.CheckType` · `lblKFilaMon` `ThisItem.CheckCurrency` · `lblKFilaMonto`
`Text(ThisItem.CheckAmount, FmtMoney)` Right · `lblKFilaRel` (**LINK**, X `570`
W `260`, igual que el prototipo — navega a la tula donde se recontó el cheque):

| Propiedad | Valor |
|---|---|
| Text | `If(IsBlank(ThisItem.MatchedCheck), "No", $"Sí · {ThisItem.MatchedCheck.Bag.BagID}")` |
| Color | `If(IsBlank(ThisItem.MatchedCheck), ThemeInk3, ThemeAccent)` |
| Underline | `!IsBlank(ThisItem.MatchedCheck)` *(ESTILO-LINK solo cuando navega)* |
| OnSelect | `If(!IsBlank(ThisItem.MatchedCheck), Set(gvarTula, LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ThisItem.MatchedCheck.Bag.'CH-Tbl-Bags')); Set(gvarPlanilla, gvarTula.ReceptionID); Set(gvarLote, gvarTula.ReceptionID.ReceptionBatch); Navigate(scrTula))` |

`lblDChequesVacio` (Etiqueta clásica): X `44` · Y `420` · W `1260` · H `40` ·
Align Center · Color `ThemeInk3` · Text `"La declaración no incluía cheques."` · Visible
`tabDetalleCorreo.Selected.Value = "Cheques declarados" && CountRows(colDCheques) = 0`.

> `MatchedCheck` (lookup al cheque físico) es **B3** y `Bag` sobre el cheque es
> **B6** — este link necesita ambos. Sin backend: label estático `"—"`.

## 7. Pestaña Respuesta al cliente

Pares dato (K Y `320` / V Y `336`). Las claves son **Etiquetas clásicas**
`lblRespK1`..`lblRespK3` (ESTILO-DATO-K, Height `16`) y los valores **Etiquetas
clásicas** `lblRespV1`..`lblRespV3` (ESTILO-DATO-V, Height `22`); el par N es
`lblRespKN` / `lblRespVN`:

| Par | K | V (Text) | X / W |
|---|---|---|---|
| 1 | `"ESTADO"` | mismo Switch del par 5 de la ficha (§2) | `44` / `260` |
| 2 | `"ENVIADA EL"` | `If(IsBlank(gvarCorreo.ReplyDateTime), "—", Text(gvarCorreo.ReplyDateTime, FmtFecha))` | `324` / `180` |
| 3 | `"ASUNTO"` | `Coalesce(gvarCorreo.ReplySubject, "—")` | `524` / `600` |

| Control | Propiedad | Valor |
|---|---|---|
| `htmlRespuesta` (Texto HTML clásico) | X/Y/W/H | `44` / `370` / `1260` / `350` |
| | HtmlText | `Coalesce(gvarCorreo.ReplyBodyHtml, "<i>Sin respuesta registrada.</i>")` |
| | Size / Font | `9.5` / (hereda) |
| | BorderColor / BorderThickness | `ThemeLineSoft` / `1` |
| | Fill | `ThemeSurface2` |

## 8. Checklist de salida

- [ ] Correo aceptado: validaciones en orden VR-01…VR-28, bolsas con links a tulas.
- [ ] Correo rechazado: pill de resultado crítico, pestaña Declaración con mensaje de vacío, la regla que falló en rojo con detalle hoja/celda.
- [ ] Una regla puede aparecer N veces en Falló (una por hallazgo) — esperado.
- [ ] Respuesta: HTML renderizado legible, asunto y fecha correctos.
- [ ] Links de bolsas/depósitos navegan a la tula correcta con migas completas.
- [ ] Banda azul: cero (con B1/B7 desplegados).
