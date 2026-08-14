# Guía 04 — scrTula

Ficha de la tula + 4 pestañas: Conciliación · Por depósito · Cheques · Cronología.

**Contrato de entrada**: `gvarTula`, `gvarPlanilla` y `gvarLote` seteados
(la búsqueda por tula los deriva; scrLote los deja listos).

## Árbol de controles

```
scrTula                            (Fill: ThemeBg)
├── shpHeaderTula / shpHeaderLineaTula / shpMarcaTula / lblMarcaTula / lblMarcaSubTula   (receta 7.5)
├── lblCrumbBusquedaTula ─ lblCrumbSep1Tula ─ lblCrumbLoteTula ─ lblCrumbSep2Tula ─ lblCrumbPlanillaTula ─ lblCrumbSep3Tula ─ lblCrumbActualTula
├── shpFichaCardTula
│   ├── lblTulaTitulo              "Tula T-…"
│   ├── pilTulaEstado
│   ├── pilTulaDif                 (oculta si la tula no está Recontada)
│   ├── lblFichaK1Tula..K8Tula     (claves de los 8 pares dato)
│   ├── lblFichaV1Tula / lblFichaV5Tula..V8Tula   (valores de los pares 1 y 5 a 8)
│   └── lblLinkTracking / lblLinkLote / lblLinkPlanilla   (valores LINK de los pares 2, 3 y 4)
├── shpDetalleCardTula
│   ├── tabDetalleTula             Lista de pestañas moderna (4 pestañas)
│   ├── ── pestaña Conciliación ──
│   │   ├── lblCCol1..4 / shpConcLinea
│   │   ├── galConc                (colConc: filas moneda/concepto)
│   │   │   └── lblConcConcepto ─ lblConcDec ─ lblConcRec ─ lblConcDif
│   │   ├── lblConcVacio           (tula sin depósitos)
│   │   └── lblConcNota
│   ├── ── pestaña Por depósito ──
│   │   ├── lblDCol1..5 / shpDepsLinea
│   │   ├── galDepsTula            (selección persistente, auto-selecciona 1ª fila)
│   │   │   ├── shpDepSel          fondo de fila seleccionada
│   │   │   ├── shpDepSelBarra     barra lateral de acento (receta 7.4)
│   │   │   ├── lblDFilaFecha ─ lblDFilaMon ─ lblDFilaEf ─ lblDFilaChq ─ lblDFilaTotal
│   │   │   └── shpDepSep
│   │   ├── lblDepSelVacio         (tula sin depósitos)
│   │   ├── lblDepSelTitulo
│   │   ├── lblMCol1..4
│   │   ├── galDepConcMini
│   │   │   └── lblMFilaC ─ lblMFilaDec ─ lblMFilaRec ─ lblMFilaDif
│   │   ├── lblDepConcNota
│   │   └── btnVerDeposito         → scrDeposito
│   ├── ── pestaña Cheques ──
│   │   ├── lblChCol1..5 / shpChequesLineaTula
│   │   ├── galChequesTula
│   │   │   └── lblChFilaNo ─ lblChFilaTipo ─ lblChFilaMon ─ lblChFilaMonto ─ pilChFilaEstado
│   │   └── lblChequesVacio
│   └── ── pestaña Cronología ──
│       └── galCrono
│           ├── shpCronoLinea (vertical) / shpCronoDot
│           └── lblCronoCuando / lblCronoQue / lblCronoQuien
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `ThemeBg` |
| OnVisible | fórmula §1.1 COMPLETA |

Encabezado: **receta 7.5 — 5 controles**, con el sufijo `Tula` de la regla 0.5:
`shpHeaderTula` (Rectángulo clásico), `shpHeaderLineaTula` (Rectángulo clásico),
`shpMarcaTula` (Botón clásico), `lblMarcaTula` (Etiqueta clásica) y
`lblMarcaSubTula` (Etiqueta clásica); propiedades fijadas en la guía 01.

### 1.1 scrTula.OnVisible

```powerfx
Concurrent(
    ClearCollect(colDeps,
        AddColumns(
            Filter('CH-Tbl-DepositsRecountings',
                BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'),
            "decEf",  Coalesce(RelatedDeclaredDeposit.CashAmount, 0),
            "decBac", Coalesce(RelatedDeclaredDeposit.BACLocalCheckAmount, 0),
            "decOtr", Coalesce(RelatedDeclaredDeposit.OtherLocalCheckAmount, 0),
            "decRef", RelatedDeclaredDeposit.DepositReference
        )),
    ClearCollect(colChequesTula,
        Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags'))
);

// ----- Conciliación por moneda (client-side sobre colDeps, acotado) -----
Clear(colConc);
ForAll(
    Table(
        {mon: "NIO", monval: 'CH-Ch-Currency'.NIO, simbolo: SimboloNIO},
        {mon: "USD", monval: 'CH-Ch-Currency'.USD, simbolo: SimboloUSD},
        {mon: "EUR", monval: 'CH-Ch-Currency'.EUR, simbolo: SimboloEUR}
    ) As m,
    With({_ds: Filter(colDeps, Currency = m.monval)},
        If(CountRows(_ds) > 0,
            Collect(colConc,
                {mon: m.mon, orden: 0, concepto: $"{m.mon} · {m.simbolo}", dec: Blank(), rec: Blank()},
                {mon: m.mon, orden: 1, concepto: "Efectivo",
                 dec: Sum(_ds, decEf), rec: Sum(_ds, CashAmount)},
                {mon: m.mon, orden: 2, concepto: "Cheques",
                 dec: Sum(_ds, decBac + decOtr),
                 rec: Sum(_ds, BACLocalCheckAmount + OtherLocaCheckAmount)},
                {mon: m.mon, orden: 3, concepto: "Total",
                 dec: Sum(_ds, decEf + decBac + decOtr),
                 rec: Sum(_ds, CashAmount + BACLocalCheckAmount + OtherLocaCheckAmount)}
            ))));

// ----- Transacciones de la tula (para el evento de acreditación) -----
// Acotado: una query delegable POR depósito de ESTA tula (1-10 típico).
Clear(colTxTula);
ForAll(colDeps As d,
    Collect(colTxTula,
        Filter('CH-Tbl-Transactions',
            RelatedDepositRecounting.'CH-Tbl-DepositsRecounting' = d.'CH-Tbl-DepositsRecounting')));

// ----- Cronología (hechos con sello real + hitos PENDIENTES punteados) -----
Clear(colCrono);
If(!IsBlank(gvarTula.BagDeclarationID),
    Collect(colCrono, {orden: 1, pendiente: false,
        cuando: gvarTula.BagDeclarationID.DeclarationID.'Created On',
        que: "Depósito declarado por el cliente" &
            If(!IsBlank(gvarTula.BagDeclarationID.DeclarationID.ReceivedEmail),
               $" · Tracking {gvarTula.BagDeclarationID.DeclarationID.ReceivedEmail.Name}", ""),
        quien: Coalesce(gvarTula.BagDeclarationID.DeclarationID.'Cliente atribuido'.Name, "")}));
Collect(colCrono, {orden: 2, pendiente: false,
    cuando: gvarLote.'Created On',
    que: $"Tula recibida en lote {gvarLote.Name}",
    quien: gvarLote.'Created By'.'Full Name' & " · " & gvarLote.Transportadora});
If(IsBlank(gvarTula.RecountingSession),
    Collect(colCrono, {orden: 3, pendiente: true, cuando: Blank(),
        que: "Asignación a sesión de recuento pendiente", quien: ""}),
    Collect(colCrono, {orden: 3, pendiente: false,
        cuando: gvarTula.RecountingSession.'Created On',
        que: $"Asignada a sesión de recuento {gvarTula.RecountingSession.Name}",
        quien: gvarTula.RecountingSession.Cashier.'Full Name'});
    If(gvarTula.BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada,
        Collect(colCrono, {orden: 4, pendiente: false,
            cuando: gvarTula.'Modified On',
            que: If(gvarTula.HasDifference,
                "Recuento completado — con diferencias",
                "Recuento completado — sin diferencias"),
            quien: gvarTula.RecountingSession.Cashier.'Full Name'});
        With({_aplicadas: Filter(colTxTula, BatchStatus = 159460003)},
        If(CountRows(_aplicadas) > 0,
            Collect(colCrono, {orden: 5, pendiente: false,
                cuando: First(_aplicadas).'Transaction Batch'.'Aplicado el',
                que: $"Transacciones acreditadas en core bancario (lote {First(_aplicadas).'Transaction Batch'.Name})",
                quien: Coalesce(First(_aplicadas).'Transaction Batch'.'Aplicado por'.'Full Name', "")}),
            Collect(colCrono, {orden: 5, pendiente: true, cuando: Blank(),
                que: "Acreditación en core bancario pendiente", quien: ""}))),
        Collect(colCrono, {orden: 4, pendiente: true, cuando: Blank(),
            que: "Recuento pendiente", quien: ""})));

// cada tula arranca en Conciliación (el estado del tab persiste entre visitas)
Reset(tabDetalleTula)
```

> - `Bag` en el filtro de cheques es la columna del backend **B6**. Sin backend,
>   provisorio (NO delegable, solo para avanzar): `Filter('CH-Tbl-Checks',
>   RelatedReCounterDepositID.BagID.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags')`.
> - `gvarTula.HasDifference` es **B5**. Sin backend: reemplazalo por `false`
>   (texto genérico) en ESTA fórmula y en `pilTulaDif` — si no, el OnVisible
>   entero no compila y la pantalla nace muerta.
> - Typo REAL del esquema: en DepositsRecountings la columna es
>   **`OtherLocaCheckAmount`** (sin la ele); en DeclaredDeposits es
>   `OtherLocalCheckAmount` (con ele). No es errata de la guía.
> - El lookup del cliente en Declaration se llama **`'Cliente atribuido'`** y
>   el del lote TB en la transacción **`'Transaction Batch'`** (columnas
>   `'Aplicado el'`, `'Aplicado por'`) — si IntelliSense difiere, el suyo manda.
> - La fecha del evento 4 usa `Modified On` de la tula (aproximación honesta:
>   no existe sello "recontada el"). El texto no incluye el monto de la
>   diferencia (el prototipo lo mostraba): el monto vive en la pestaña
>   Conciliación — divergencia declarada.
> - La PK de DepositsRecounting es **`'CH-Tbl-DepositsRecounting'`** (singular).

## 2. Migas (receta 7.6, Y `64`)

Los **siete** controles son **Etiquetas clásicas** (Height `22`, Size `9`); los
separadores llevan Color `ThemeInk3`:

| Control | X / W | Text | OnSelect |
|---|---|---|---|
| `lblCrumbBusquedaTula` (Etiqueta clásica) | `24` / `70` | `"Búsqueda"` | `Navigate(scrBusqueda)` |
| `lblCrumbSep1Tula` (Etiqueta clásica) | `94` / `14` | `"›"` | — |
| `lblCrumbLoteTula` (Etiqueta clásica) | `108` / `110` | `$"Lote {gvarLote.Name}"` · Color `ThemeAccent` | `Navigate(scrLote)` |
| `lblCrumbSep2Tula` (Etiqueta clásica) | `218` / `14` | `"›"` | — |
| `lblCrumbPlanillaTula` (Etiqueta clásica) | `232` / `100` | `gvarPlanilla.ReceiptID` · Color `ThemeAccent` | `Navigate(scrLote)` |
| `lblCrumbSep3Tula` (Etiqueta clásica) | `332` / `14` | `"›"` | — |
| `lblCrumbActualTula` (Etiqueta clásica) | `346` / `160` | `$"Tula {gvarTula.BagID}"` · Color `ThemeInk2` | — |

## 3. Ficha

| Control | Propiedad | Valor |
|---|---|---|
| `shpFichaCardTula` (Rectángulo clásico) | X/Y/W/H | `24` / `94` / `1318` / `150` — receta 7.1 |
| `lblTulaTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `108` / `260` / `30` · Text `$"Tula {gvarTula.BagID}"` · ESTILO-TITULO |
| `pilTulaEstado` (Botón clásico, receta 7.2) | X/Y/W/H | `310` / `110` / `250` / `26` — mismo Switch de estado que `pilTFilaEstado` (guía 03 §5) con `gvarTula.BagStatus` |
| `pilTulaDif` (Botón clásico, receta 7.2) | X/Y/W/H | `570` / `110` / `150` / `26` — mismo If de diferencia que `pilTFilaDif` (guía 03 §5) con `gvarTula` |
| | Visible | `gvarTula.BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada` *(en la ficha no tiene sentido un "Pendiente" al lado del estado; en la grilla de la guía 03 SÍ se mantiene, ahí la columna necesita valor en todas las filas)* |

Pares dato — fila 1 (K en Y `148`, V en Y `164`) y fila 2 (K en Y `194`, V en Y `210`).
Las claves son **Etiquetas clásicas** `lblFichaK1Tula`..`lblFichaK8Tula`
(ESTILO-DATO-K) y los valores **Etiquetas clásicas** ESTILO-DATO-V: el par N usa
`lblFichaVNTula` — o sea `lblFichaV1Tula`, `lblFichaV5Tula`, `lblFichaV6Tula`,
`lblFichaV7Tula` y `lblFichaV8Tula` —, salvo los pares 2, 3 y 4 cuyo valor es el
link `lblLinkTracking`, `lblLinkLote` y `lblLinkPlanilla` respectivamente
(Etiquetas clásicas con **ESTILO-LINK** y OnSelect — un label clásico dispara
OnSelect, receta 7.6). Height `16` (K, parte de ESTILO-DATO-K) / `22` (V):

| Par | K (Text) | V | X / W |
|---|---|---|---|
| 1 | `"CLIENTE"` | label: `Coalesce(gvarTula.ReceptionID.RemitentClient.Name, gvarTula.ReceptionID.RemitentName)` | `44` / `280` |
| 2 | `"TRACKING (CORREO)"` | **LINK** `lblLinkTracking` §3.1 | `344` / `170` |
| 3 | `"LOTE DE RECEPCIÓN"` | **LINK** `lblLinkLote`: Text `gvarLote.Name` · **ESTILO-LINK** · OnSelect `Navigate(scrLote)` | `534` / `150` |
| 4 | `"PLANILLA"` | **LINK** `lblLinkPlanilla`: Text `$"{gvarPlanilla.ReceiptID} · {gvarPlanilla.RemitentName}"` · **ESTILO-LINK** · OnSelect `Navigate(scrLote)` | `704` / `170` |
| 5 | `"SESIÓN DE RECUENTO"` | `Coalesce(gvarTula.RecountingSession.Name, "—")` | `44` / `200` |
| 6 | `"CAJERO"` | `Coalesce(gvarTula.RecountingSession.Cashier.'Full Name', "—")` | `264` / `220` |
| 7 | `"FECHA DE RECUENTO"` | `If(gvarTula.BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada, Text(gvarTula.'Modified On', FmtFecha), "—")` | `504` / `180` |
| 8 | `"DECLARACIÓN RELACIONADA"` | `If(IsBlank(gvarTula.BagDeclarationID), "No", "Sí")` | `704` / `200` |

### 3.1 lblLinkTracking (Etiqueta clásica)

| Propiedad | Valor |
|---|---|
| Text | `If(IsBlank(gvarTula.BagDeclarationID), "—", Coalesce(gvarTula.BagDeclarationID.DeclarationID.ReceivedEmail.Name, "—"))` |
| Color | `If(IsBlank(gvarTula.BagDeclarationID), ThemeInk, ThemeAccent)` |
| Underline | `!IsBlank(gvarTula.BagDeclarationID)` *(ESTILO-LINK solo cuando de verdad navega)* |
| OnSelect | `If(!IsBlank(gvarTula.BagDeclarationID) && !IsBlank(gvarTula.BagDeclarationID.DeclarationID.ReceivedEmail), Set(gvarCorreo, LookUp('CH-Tbl-Received Emails', 'CH-Tbl-Received Emails' = gvarTula.BagDeclarationID.DeclarationID.ReceivedEmail.'CH-Tbl-Received Emails')); Navigate(scrCorreo))` |

> `ReceivedEmail` en Declaration es la columna del backend **B1**. Sin backend:
> Text `"—"` fijo.

## 4. Card de detalle + pestañas

| Control | Propiedad | Valor |
|---|---|---|
| `shpDetalleCardTula` (Rectángulo clásico) | X/Y/W/H | `24` / `256` / `1318` / `488` — receta 7.1 |
| `tabDetalleTula` (Lista de pestañas moderna) | X/Y/W/H | `44` / `268` / `700` / `40` |
| | Items | `["Conciliación", "Por depósito", "Cheques", "Cronología"]` |
| | DefaultSelectedItems | `["Conciliación"]` |
| | AccessibleLabel | `"Detalle de la tula"` |

**Cada bloque de pestaña**: su control raíz y todos los internos llevan
`Visible: tabDetalleTula.Selected.Value = "<nombre de la pestaña>"`.

## 5. Pestaña Conciliación

Encabezados `lblCCol1`..`lblCCol4`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `320`): `lblCCol1` `"MONEDA / CONCEPTO"` X `44`
W `300` · `lblCCol2` `"DECLARADO"` X `344` W `180` Right · `lblCCol3` `"RECONTADO"`
X `524` W `180` Right · `lblCCol4` `"DIFERENCIA"` X `704` W `180` Right.
`shpConcLinea` (Rectángulo clásico): X `44` Y `340` W `840` H `1` · Fill `ThemeLine`.

### galConc (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `342` / `840` / `340` |
| Items | `SortByColumns(colConc, "mon", SortOrder.Ascending, "orden", SortOrder.Ascending)` |
| TemplateSize | `34` |

Template (labels Y `6`, H `22`):

| Control | Propiedad | Valor |
|---|---|---|
| `lblConcConcepto` (Etiqueta clásica) | X/W | `0` / `300` |
| | Text | `ThisItem.concepto` |
| | FontWeight | `If(ThisItem.orden = 0 \|\| ThisItem.orden = 3, FontWeight.Semibold, FontWeight.Normal)` |
| | Color | `If(ThisItem.orden = 0, ThemeInk, If(ThisItem.orden = 3, ThemeInk, ThemeInk2))` |
| | PaddingLeft | `If(ThisItem.orden = 1 \|\| ThisItem.orden = 2, 20, 0)` *(la fila Total NO se indenta — cierra el bloque de la moneda, como el mock)* |
| `lblConcDec` (Etiqueta clásica) | X/W · Align | `300` / `180` · Right |
| | Text | `If(ThisItem.orden = 0, "", Text(ThisItem.dec, FmtMoney))` |
| | FontWeight | `If(ThisItem.orden = 3, FontWeight.Semibold, FontWeight.Normal)` |
| `lblConcRec` (Etiqueta clásica) | X/W · Align | `480` / `180` · Right — mismas fórmulas con `ThisItem.rec` |
| `lblConcDif` (Etiqueta clásica) | X/W · Align | `660` / `180` · Right |
| | Text | `If(ThisItem.orden = 0, "", With({_d: ThisItem.rec - ThisItem.dec}, If(Abs(_d) < 0.005, "0.00", If(_d > 0, "+", "−") & Text(Abs(_d), FmtMoney))))` |
| | Color | `With({_d: Coalesce(ThisItem.rec, 0) - Coalesce(ThisItem.dec, 0)}, If(Abs(_d) < 0.005, ThemeOk, ThemeCrit))` |
| | FontWeight | `FontWeight.Semibold` |

`lblConcNota` (Etiqueta clásica — X `44` · Y `690` · W `840` · H `20` · Size `8.5` · Color `ThemeInk2`):
`"Diferencia = Recontado − Declarado. Solo se listan monedas con movimientos en la tula."`

`lblConcVacio` (Etiqueta clásica — X `44` · Y `420` · W `840` · H `40` · Color `ThemeInk3` · Align
Center): Text `"Sin depósitos recontados todavía."` · Visible
`tabDetalleTula.Selected.Value = "Conciliación" && CountRows(colConc) = 0`.

## 6. Pestaña Por depósito

Mitad izquierda — encabezados `lblDCol1`..`lblDCol5`: **Etiquetas clásicas,
ESTILO-HEADER-COL** (Y `320`): `lblDCol1` `"FECHA RECUENTO"` X `44` W `160` ·
`lblDCol2` `"MONEDA"` X `204` W `90` · `lblDCol3` `"EFECTIVO"` X `294` W `130`
Right · `lblDCol4` `"CHEQUES"` X `424` W `130` Right · `lblDCol5` `"TOTAL"`
X `554` W `130` Right. `shpDepsLinea` (Rectángulo clásico) X `44`
Y `340` W `640` H `1` · Fill `ThemeLine`.

### galDepsTula (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `342` / `640` / `380` |
| Items | `SortByColumns(colDeps, "createdon", SortOrder.Descending)` *(si IntelliSense no acepta `"createdon"`, usá `Sort(colDeps, 'Created On', SortOrder.Descending)`)* |
| TemplateSize | `40` |

Template: fondo `shpDepSel` (**Rectángulo clásico**, PRIMERO para que quede al
fondo — X/Y/W/H `0`/`0`/`640`/`40`) con Fill
`If(ThisItem.IsSelected, ThemeAccentSoft, Transparent)`; barra lateral de acento
`shpDepSelBarra` (**Rectángulo clásico**, receta 7.4 — X `0` · Y `0` · W `3` ·
H `40` · Fill `ThemeAccent` · Visible `ThisItem.IsSelected`); labels (**Etiquetas
clásicas**, Y `9`, H `22`, X/W de los encabezados menos `44`):
`lblDFilaFecha` fecha `Text(ThisItem.'Created On', FmtFecha)`,
`lblDFilaMon` moneda `ThisItem.Currency`, `lblDFilaEf` efectivo `Text(ThisItem.CashAmount, FmtMoney)`,
`lblDFilaChq` cheques `Text(ThisItem.BACLocalCheckAmount + ThisItem.OtherLocaCheckAmount, FmtMoney)`,
`lblDFilaTotal` total `Text(ThisItem.Total, FmtMoney)` Semibold. `OnSelect` de la galería: *(nada
— la selección basta)*. Separador `shpDepSep` (**Rectángulo clásico**) X `0` ·
Y `39` · W `640` · H `1` · Fill `ThemeLineSoft`.

> **Comportamiento declarado**: las galerías clásicas auto-seleccionan la
> primera fila (receta 7.4) — el panel derecho arranca mostrando el PRIMER
> depósito, sin panel muerto. (El prototipo esperaba un clic; esto es mejor y
> queda declarado.) El estado vacío es solo para tulas SIN depósitos.

> **Estado vacío de la grilla izquierda**: el mock también vacía la tabla
> izquierda cuando no hay depósitos; acá alcanza con el mensaje del panel
> derecho (`lblDepSelVacio`) — simplificación aceptada.

> **Sin hover en `galDepsTula`**: esta galería NO lleva overlay `btn…Fila` — la
> selección de la galería ya da feedback (fondo + barra de acento). El hover del
> mock se pierde — aceptado.

Mitad derecha:

| Control | Propiedad | Valor |
|---|---|---|
| `lblDepSelVacio` (Etiqueta clásica) | X/Y/W/H | `724` / `430` / `560` / `40` · Text `"Sin depósitos recontados todavía."` · Color `ThemeInk3` · Align Center |
| | Visible | `tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) = 0` |
| `lblDepSelTitulo` (Etiqueta clásica) | X/Y/W/H | `724` / `320` / `560` / `22` · ESTILO-SECCION |
| | Text | `Upper($"DEPÓSITO {Coalesce(galDepsTula.Selected.decRef, galDepsTula.Selected.Name)} · {galDepsTula.Selected.Currency}")` |
| | Visible | `tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0` |

⚠️ **El Visible del panel derecho NO se hereda: va control por control.** Estos
llevan EXACTAMENTE
`tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0`:
`lblDepSelTitulo`, `lblMCol1`, `lblMCol2`, `lblMCol3`, `lblMCol4`,
`galDepConcMini`, `lblDepConcNota` y `btnVerDeposito`. Sin la conjunción del tab,
el panel derecho se ve encima de las otras pestañas.

Encabezados mini `lblMCol1`..`lblMCol4`: **Etiquetas clásicas,
ESTILO-HEADER-COL** (Y `350`), Visible
`tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0`:
`lblMCol1` `"CONCEPTO"` X `724` W `160` ·
`lblMCol2` `"DECLARADO"` X `884` W `130` Right · `lblMCol3` `"RECONTADO"`
X `1014` W `130` Right · `lblMCol4` `"DIFERENCIA"` X `1144` W `130` Right.

### galDepConcMini (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `724` / `372` / `550` / `140` |
| TemplateSize | `32` |
| Visible | `tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0` |
| Items | fórmula ↓ |

```powerfx
With({_d: galDepsTula.Selected},
Table(
    {c: "Efectivo",     dec: _d.decEf,  rec: _d.CashAmount},
    {c: "Cheque BAC",   dec: _d.decBac, rec: _d.BACLocalCheckAmount},
    {c: "Cheque otros", dec: _d.decOtr, rec: _d.OtherLocaCheckAmount},
    {c: "Total", dec: _d.decEf + _d.decBac + _d.decOtr,
     rec: _d.CashAmount + _d.BACLocalCheckAmount + _d.OtherLocaCheckAmount}
))
```

Template — los cuatro son **Etiquetas clásicas** (Y `5`, H `22`; X/W = encabezado
− 724, el X de ESTA galería): `lblMFilaC` X `0` W
`160` (Text `ThisItem.c`, Semibold solo en "Total"), `lblMFilaDec` X `160` W
`130` / `lblMFilaRec` X `290` W `130` (`Text(ThisItem.dec/rec, FmtMoney)`
Right) y `lblMFilaDif` X `420` W `130` con las MISMAS fórmulas de texto/color
que `lblConcDif` (§5).

| Control | Propiedad | Valor |
|---|---|---|
| `lblDepConcNota` (Etiqueta clásica) | X/Y/W/H | `724` / `514` / `550` / `18` · Size `8.5` · Color `ThemeInk2` |
| | Text | `$"Montos en {galDepsTula.Selected.Currency} ({Switch(galDepsTula.Selected.Currency, 'CH-Ch-Currency'.NIO, SimboloNIO, 'CH-Ch-Currency'.USD, SimboloUSD, SimboloEUR)}). Declarado proviene del depósito declarado relacionado."` |
| | Visible | `tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0` |
| `btnVerDeposito` (Botón moderno) | X/Y/W/H | `724` / `540` / `340` / `36` |
| | Text | `"Ver transacciones y cheques del depósito →"` |
| | BasePaletteColor | `ThemeAccent` |
| | Visible | `tabDetalleTula.Selected.Value = "Por depósito" && CountRows(colDeps) > 0` |
| | OnSelect | `Set(gvarDep, galDepsTula.Selected); Navigate(scrDeposito)` |

## 7. Pestaña Cheques

Encabezados `lblChCol1`..`lblChCol5`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `320`): `lblChCol1` `"NO. CHEQUE"` X `44` W `160` · `lblChCol2` `"TIPO"`
X `204` W `160` · `lblChCol3` `"MONEDA"` X `364` W `90` · `lblChCol4` `"MONTO"`
X `454` W `140` Right · `lblChCol5` `"ESTADO"` X `614` W `220`.
`shpChequesLineaTula` (Rectángulo clásico) X `44` Y `340` W `840` H `1` ·
Fill `ThemeLine`.

### galChequesTula (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `342` / `840` / `380` · TemplateSize `40` |
| Items | `colChequesTula` |

Template (los cuatro labels son **Etiquetas clásicas**, Y `9`, H `22`, X/W de los
encabezados menos `44`): `lblChFilaNo` `ThisItem.CheckNumber` Semibold ·
`lblChFilaTipo`
`ThisItem.CheckTypes` · `lblChFilaMon` `ThisItem.CheckCurrency` · `lblChFilaMonto`
`Text(ThisItem.CheckAmount, FmtMoney)` Right · `pilChFilaEstado` (**Botón
clásico**, receta 7.2, X `570` · Y `7` · W `210`):

- Text: `ThisItem.CheckStatus`
- Fill: `Switch(ThisItem.CheckStatus, 'CheckStatus (CH-Tbl-Checks)'.Recontado, ThemeInfoSoft, 'CheckStatus (CH-Tbl-Checks)'.Digitado, ThemeOkSoft, 'CheckStatus (CH-Tbl-Checks)'.'Enviado a digitacion', ThemeWarnSoft, 'CheckStatus (CH-Tbl-Checks)'.Recibido, ThemeInfoSoft, 'CheckStatus (CH-Tbl-Checks)'.'No Recibido', ThemeCritSoft, 'CheckStatus (CH-Tbl-Checks)'.Rebotado, ThemeCritSoft, ThemeSurface2)`
- Color: mismo Switch con `ThemeInfo / ThemeOk / ThemeWarn / ThemeInfo / ThemeCrit / ThemeCrit / ThemeInk2`

`lblChequesVacio` (Etiqueta clásica): X `44` Y `420` W `840` H `40` Text
`"Esta tula no tiene cheques registrados."` · Visible
`tabDetalleTula.Selected.Value = "Cheques" && CountRows(colChequesTula) = 0` ·
Color `ThemeInk3` · Align Center.

## 8. Pestaña Cronología

### galCrono (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `330` / `840` / `390` · TemplateSize `74` |
| Items | `SortByColumns(colCrono, "orden", SortOrder.Ascending)` |

Template:

| Control | Propiedad | Valor |
|---|---|---|
| `shpCronoLinea` (Rectángulo clásico) | X/Y/W/H | `7` / `0` / `2` / `74` · Fill `ThemeLine` |
| `shpCronoDot` (Círculo clásico) | X/Y/W/H | `0` / `6` / `16` / `16` · Fill `ThemeSurface` · BorderThickness `2` |
| | BorderColor | `If(ThisItem.pendiente, ThemeInk3, ThemeAccent)` |
| | BorderStyle | `If(ThisItem.pendiente, BorderStyle.Dashed, BorderStyle.Solid)` |
| `lblCronoCuando` (Etiqueta clásica) | X/Y/W/H | `30` / `2` / `300` / `16` · Text `If(ThisItem.pendiente, "", Text(ThisItem.cuando, FmtFecha))` · Size `8` · Color `ThemeInk3` |
| `lblCronoQue` (Etiqueta clásica) | X/Y/W/H | `30` / `18` / `780` / `22` · Text `ThisItem.que` · Semibold |
| | Color | `If(ThisItem.que = "Recuento completado — con diferencias", ThemeCrit, If(ThisItem.pendiente, ThemeInk3, ThemeInk))` |
| `lblCronoQuien` (Etiqueta clásica) | X/Y/W/H | `30` / `40` / `780` / `18` · Text `ThisItem.quien` · Size `9` · Color `ThemeInk2` |

## 9. Checklist de salida

- [ ] Conciliación muestra solo monedas con depósitos; totales cuadran contra la app Recounting.
- [ ] Buscar tula directa desde scrBusqueda refresca colecciones (cambiá de tula y verificá que cambian los números — EL bug de la app vieja).
- [ ] Seleccionar depósito llena el panel derecho; "Ver transacciones" navega.
- [ ] Links Tracking/Lote/Planilla navegan bien.
- [ ] Cronología ordenada y con actor por evento.
- [ ] Banda azul de delegación: cero (con backend B6 aplicado).
