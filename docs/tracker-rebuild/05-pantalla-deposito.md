# Guía 05 — scrDeposito

Detalle de un depósito recontado: ficha, transacciones generadas y cheques.

**Contrato de entrada**: `gvarDep` (registro de colDeps, trae las columnas
`decEf/decBac/decOtr/decRef`) + `gvarTula`, `gvarPlanilla`, `gvarLote`, y la
colección **`colChequesTula`** (cargada por `scrTula.OnVisible` — scrDeposito
SOLO se alcanza desde scrTula; cualquier entrada futura por otro camino debe
recargarla).

## Árbol de controles

```
scrDeposito                        (Fill: ThemeBg)
├── shpHeaderDep / shpHeaderLineaDep / shpMarcaDep / lblMarcaDep / lblMarcaSubDep   (receta 7.5)
├── lblCrumbBusquedaDep ─ lblCrumbSep1Dep ─ lblCrumbLoteDep ─ lblCrumbSep2Dep ─ lblCrumbPlanillaDep ─ lblCrumbSep3Dep ─ lblCrumbTulaDep ─ lblCrumbSep4Dep ─ lblCrumbActualDep
├── shpFichaCardDep
│   ├── lblDepTitulo               "Depósito DEP-… / DR-…"
│   ├── pilDepDif                  Sin diferencia / Faltante X / Sobrante X
│   └── lblFichaK1Dep..K8Dep / lblFichaV1Dep..V8Dep
├── shpTxsCard
│   ├── lblTxsTitulo
│   ├── lblXCol1..8 / shpTxsLinea
│   ├── galTxs
│   │   ├── lblXFilaId ─ pilXFilaTipo ─ lblXFilaMonto ─ lblXFilaLote ─ lblXFilaGen ─ lblXFilaProc ─ pilXFilaCore ─ lblXFilaRef
│   │   └── shpXFilaSep
│   ├── lblTxsVacio
│   └── lblTxsNota
└── shpChequesCard
    ├── lblChequesTitulo
    ├── lblQCol1..5 / shpQLineaDep
    ├── galChequesDep              (filtra colChequesTula por este depósito)
    │   └── lblQFilaNo ─ lblQFilaTipo ─ lblQFilaMon ─ lblQFilaMonto ─ pilQFilaEstado
    └── lblChequesDepVacio
```

## 1. Pantalla y migas

Fill `ThemeBg` · OnVisible: *(nada — todo llega por gvar/colecciones)*.

Encabezado: **receta 7.5 — 5 controles**, con el sufijo `Dep` de la regla 0.5:
`shpHeaderDep` (Rectángulo clásico), `shpHeaderLineaDep` (Rectángulo clásico),
`shpMarcaDep` (Botón clásico), `lblMarcaDep` (Etiqueta clásica) y
`lblMarcaSubDep` (Etiqueta clásica); propiedades fijadas en la guía 01.

Migas (receta 7.6, Y `64`, Height `22`, Size `9`) — los **nueve** controles son
**Etiquetas clásicas**: `lblCrumbBusquedaDep` (`24`/`70`, → `Navigate(scrBusqueda)`) ·
`lblCrumbSep1Dep` (`94`/`14`) ·
`lblCrumbLoteDep` (`108`/`110`, `$"Lote {gvarLote.Name}"`, → `Navigate(scrLote)`) ·
`lblCrumbSep2Dep` (`218`/`14`) · `lblCrumbPlanillaDep` (`232`/`100`, `gvarPlanilla.ReceiptID`, →
`Navigate(scrLote)`) · `lblCrumbSep3Dep` (`332`/`14`) · `lblCrumbTulaDep` (`346`/`120`,
`$"Tula {gvarTula.BagID}"`, Color `ThemeAccent`, → `Navigate(scrTula)`) ·
`lblCrumbSep4Dep` (`466`/`14`) ·
`lblCrumbActualDep` (`480`/`220`, `$"Depósito {Coalesce(gvarDep.decRef, gvarDep.Name)}"`,
Color `ThemeInk2`).

Los cuatro separadores (`lblCrumbSep1Dep`..`lblCrumbSep4Dep`) llevan Text `"›"` y
Color `ThemeInk3`.

## 2. Ficha del depósito

| Control | Propiedad | Valor |
|---|---|---|
| `shpFichaCardDep` (Rectángulo clásico) | X/Y/W/H | `24` / `94` / `1318` / `150` — receta 7.1 |
| `lblDepTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `108` / `420` / `30` · ESTILO-TITULO |
| | Text | `$"Depósito {Coalesce(gvarDep.decRef, gvarDep.Name)}"` |
| `pilDepDif` (Botón clásico, receta 7.2) | X/Y/W/H | `470` / `110` / `260` / `26` |
| | Text | fórmula ↓ |
| | Fill / Color | `If(Abs(<dif>) < 0.005, ThemeOkSoft, ThemeCritSoft)` / ídem con `ThemeOk`/`ThemeCrit` |

```powerfx
// pilDepDif.Text — la diferencia del depósito, CON símbolo de moneda
With({
    _dif: (gvarDep.CashAmount + gvarDep.BACLocalCheckAmount + gvarDep.OtherLocaCheckAmount)
        - (gvarDep.decEf + gvarDep.decBac + gvarDep.decOtr),
    _sim: Switch(gvarDep.Currency,
        'CH-Ch-Currency'.NIO, SimboloNIO,
        'CH-Ch-Currency'.USD, SimboloUSD,
        SimboloEUR)},
If(Abs(_dif) < 0.005, "Sin diferencia",
   If(_dif < 0, $"Faltante {_sim} {Text(Abs(_dif), FmtMoney)}",
      $"Sobrante {_sim} {Text(_dif, FmtMoney)}")))
```

*(En Fill y Color repetí el mismo `With` — los labels clásicos no comparten
variables entre propiedades.)*

Pares dato (fila 1: K Y `148` / V Y `164` · fila 2: K Y `194` / V Y `210`).
Las claves son **Etiquetas clásicas** `lblFichaK1Dep`..`lblFichaK8Dep`
(ESTILO-DATO-K, Height `16`) y los valores **Etiquetas clásicas**
`lblFichaV1Dep`..`lblFichaV8Dep` (ESTILO-DATO-V, Height `22`); el par N es
`lblFichaKNDep` / `lblFichaVNDep`. El valor del par 1 es un link (Etiqueta
clásica con OnSelect, receta 7.6):

| Par | K | V (Text) | X / W |
|---|---|---|---|
| 1 | `"TULA"` | **LINK**: `gvarTula.BagID` · **ESTILO-LINK** · OnSelect `Navigate(scrTula)` | `44` / `140` |
| 2 | `"CLIENTE"` | `Coalesce(gvarTula.ReceptionID.RemitentClient.Name, gvarTula.ReceptionID.RemitentName)` | `204` / `260` |
| 3 | `"MONEDA"` | `$"{gvarDep.Currency} · {Switch(gvarDep.Currency, 'CH-Ch-Currency'.NIO, SimboloNIO, 'CH-Ch-Currency'.USD, SimboloUSD, SimboloEUR)}"` | `484` / `100` |
| 4 | `"FECHA DE RECUENTO"` | `Text(gvarDep.'Created On', FmtFecha)` | `604` / `180` |
| 5 | `"RECONTADO EFECTIVO"` | `Text(gvarDep.CashAmount, FmtMoney)` | `44` / `170` |
| 6 | `"RECONTADO CHEQUES"` | `Text(gvarDep.BACLocalCheckAmount + gvarDep.OtherLocaCheckAmount, FmtMoney)` | `234` / `170` |
| 7 | `"TOTAL RECONTADO"` | `Text(gvarDep.Total, FmtMoney)` | `424` / `170` |
| 8 | `"TOTAL DECLARADO"` | `Text(gvarDep.decEf + gvarDep.decBac + gvarDep.decOtr, FmtMoney)` | `614` / `170` |

## 3. Transacciones generadas

| Control | Propiedad | Valor |
|---|---|---|
| `shpTxsCard` (Rectángulo clásico) | X/Y/W/H | `24` / `256` / `1318` / `280` — receta 7.1 |
| `lblTxsTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `270` / `600` / `20` · Text `"TRANSACCIONES GENERADAS POR EL DEPÓSITO"` · ESTILO-SECCION |

Encabezados `lblXCol1`..`lblXCol8`: **Etiquetas clásicas, ESTILO-HEADER-COL**
(Y `298`):

| Control | Text | X | W | Align |
|---|---|---|---|---|
| `lblXCol1` | `"TRANSACCIÓN"` | `44` | `130` | Left |
| `lblXCol2` | `"TIPO"` | `174` | `140` | Left |
| `lblXCol3` | `"MONTO"` | `314` | `120` | Right |
| `lblXCol4` | `"LOTE DIGITACIÓN"` | `434` | `120` | Left |
| `lblXCol5` | `"GENERADO POR"` | `554` | `180` | Left |
| `lblXCol6` | `"PROC."` | `734` | `80` | Left |
| `lblXCol7` | `"CORE BANCARIO"` | `814` | `240` | Left |
| `lblXCol8` | `"REFERENCIA"` | `1054` | `240` | Left |

`shpTxsLinea` (Rectángulo clásico) X `44` Y `318` W `1278` H `1` · Fill `ThemeLine`.

### galTxs (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `44` / `320` / `1278` / `170` · TemplateSize `42` |
| Items | `Filter('CH-Tbl-Transactions', RelatedDepositRecounting.'CH-Tbl-DepositsRecounting' = gvarDep.'CH-Tbl-DepositsRecounting')` |

> ⚠️ La PK se llama **`'CH-Tbl-DepositsRecounting'`** (SINGULAR — solo el
> nombre del datasource es plural). Verificado contra la app Recounting.

Template (labels Y `10` H `22`; X/W = encabezados − `44`):

| Control | Propiedad | Valor |
|---|---|---|
| `lblXFilaId` (Etiqueta clásica) | Text / FontWeight | `ThisItem.Name` / Semibold |
| `pilXFilaTipo` (Botón clásico, receta 7.2) | X/Y/W/H | `130` / `8` / `130` / `26` |
| | Text | `ThisItem.TransactionType` |
| | Fill | `Switch(ThisItem.TransactionType, 'TransactionType (CH-Tbl-Transactions)'.Deposito, ThemeInfoSoft, 'TransactionType (CH-Tbl-Transactions)'.Faltante, ThemeCritSoft, 'TransactionType (CH-Tbl-Transactions)'.Sobrante, ThemeWarnSoft, ThemeSurface2)` |
| | Color | mismo Switch con `ThemeInfo / ThemeCrit / ThemeWarn / ThemeInk2` |
| `lblXFilaMonto` (Etiqueta clásica) | Text / Align | `Text(ThisItem.Total, FmtMoney)` / Right |
| `lblXFilaLote` (Etiqueta clásica) | Text | `ThisItem.'Transaction Batch'.Name` |
| `lblXFilaGen` (Etiqueta clásica) | Text | `Coalesce(ThisItem.'Transaction Batch'.'Generado por'.'Full Name', "—")` |
| `lblXFilaProc` (Etiqueta clásica) | Text | `ThisItem.TransactionProcessingType` |
| `pilXFilaCore` (Botón clásico, receta 7.2) | X/Y/W/H | `770` / `8` / `230` / `26` |
| | Text | `If(ThisItem.BatchStatus = 159460003, $"Acreditada · {Text(ThisItem.'Transaction Batch'.'Aplicado el', FmtFecha)}", "Pendiente")` |
| | Fill / Color | `If(ThisItem.BatchStatus = 159460003, ThemeOkSoft, ThemeWarnSoft)` / ídem `ThemeOk`/`ThemeWarn` |
| `lblXFilaRef` (Etiqueta clásica) | Text | `Coalesce(ThisItem.TransactionReference, "—")` |
| `shpXFilaSep` (Rectángulo clásico) | Y `41` · H `1` · W `1278` · Fill `ThemeLineSoft` |

> - El lookup al lote se llama **`'Transaction Batch'`** (con espacio) y sus
>   columnas están en español: **`'Generado por'`**, **`'Aplicado el'`**
>   (verificado en la app Digitization). Si IntelliSense ofrece otra grafía,
>   la suya manda.
> - `BatchStatus` en la transacción es la **columna espejo int** (existe hoy,
>   mantenida por plugin): `159460003` = lote Aplicado = acreditada en core.
>   No compares contra `TransactionStatus` (pendiente = en blanco) ni contra el
>   estado del lote vía lookup (no delega).
> - Excepción CONSCIENTE a la regla 00-§5.5 (sin lookups anidados en galerías):
>   `galTxs` navega Transacción→TB→Usuario, pero el conjunto está acotado a las
>   transacciones de UN depósito (1–3 filas típicas). Con ese volumen, el costo
>   es despreciable y evita otra colección.
> - Los encabezados `"PROC."` y `"REFERENCIA"` abrevian "Procesamiento" y
>   "Referencia core" del prototipo — decisión de espacio, no un olvido.

| Control | Propiedad | Valor |
|---|---|---|
| `lblTxsVacio` (Etiqueta clásica) | X/Y/W/H | `44` / `380` / `1278` / `30` · Text `"Todavía no se han generado transacciones para este depósito."` · Visible `CountRows(galTxs.AllItems) = 0` · Color `ThemeInk3` · Align Center |
| `lblTxsNota` (Etiqueta clásica) | X/Y/W/H | `44` / `498` / `1278` / `30` · Size `8.5` · Color `ThemeInk2` |
| | Text | `"Un depósito puede generar más de una transacción: el monto recontado viaja como Deposito y las diferencias como Faltante o Sobrante. Si la tula llegó sin declaración, la transacción nace como Sin declaracion."` |

## 4. Cheques del depósito

| Control | Propiedad | Valor |
|---|---|---|
| `shpChequesCard` (Rectángulo clásico) | X/Y/W/H | `24` / `548` / `1318` / `196` — receta 7.1 |
| `lblChequesTitulo` (Etiqueta clásica) | X/Y/W/H | `44` / `562` / `400` / `20` · Text `"CHEQUES DEL DEPÓSITO"` · ESTILO-SECCION |

Encabezados `lblQCol1`..`lblQCol5` (**Etiquetas clásicas, ESTILO-HEADER-COL**,
Y `590`) y galería `galChequesDep` (**Galería vertical en blanco**, X `44`
Y `612` W `1278` H `120`, TemplateSize `40`): **idénticos a la pestaña Cheques
de la guía 04 §7** (mismas columnas — `"NO. CHEQUE"` X `44` W `160` · `"TIPO"`
X `204` W `160` · `"MONEDA"` X `364` W `90` · `"MONTO"` X `454` W `140` Right ·
`"ESTADO"` X `614` W `220` —, mismos pills), pero con Items:

```powerfx
Filter(colChequesTula,
    RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = gvarDep.'CH-Tbl-DepositsRecounting')
```

*(Filtra la colección ya cargada en scrTula: client-side, cero consultas.)*

`shpQLineaDep` (**Rectángulo clásico**): X `44` · Y `610` · W `1278` · H `1` ·
Fill `ThemeLine` (misma relación que en el resto de las grillas: la línea va dos
píxeles arriba de la galería). *El nombre lleva `Q`+`Dep` porque
`shpChequesLineaTula` ya ocupa el patrón en scrTula — regla 0.5.*

Los controles del template llevan la letra `Q` de esta card, igual que los
encabezados (`lblQCol…`), y son el calco 1:1 de los de la guía 04 §7 — labels
Y `9`, H `22`, X/W = encabezado − `44`:

| Guía 04 §7 | Acá | Tipo |
|---|---|---|
| `lblChFilaNo` | `lblQFilaNo` | Etiqueta clásica |
| `lblChFilaTipo` | `lblQFilaTipo` | Etiqueta clásica |
| `lblChFilaMon` | `lblQFilaMon` | Etiqueta clásica |
| `lblChFilaMonto` | `lblQFilaMonto` | Etiqueta clásica |
| `pilChFilaEstado` | `pilQFilaEstado` | Botón clásico, receta 7.2 (X `570` · Y `7` · W `210`) |

`lblChequesDepVacio` (**Etiqueta clásica**): X `44` · Y `660` · W `1278` · H `30`
· Text `"Este depósito no incluye cheques."` · Visible
`CountRows(galChequesDep.AllItems) = 0` · Color `ThemeInk3` · Align Center.

## 5. Checklist de salida

- [ ] Un depósito con faltante muestra 2+ transacciones (Deposito + Faltante) y el pill rojo de faltante en la ficha.
- [ ] Transacción de lote Aplicado muestra "Acreditada · fecha"; lote Generado/Ingresado muestra "Pendiente".
- [ ] "Generado por" trae el nombre del operador del lote TB.
- [ ] Migas: Tula/Planilla/Lote navegan.
- [ ] Banda azul: solo puede aparecer en galTxs si escribiste mal el patrón de PK.
