# Guía 02 — scrCaptura (Captura de lote)

La pantalla donde el oficial de esclusa **crea o retoma un lote**, teclea sus planillas
y sus tulas, valida, guarda y cierra. Reemplaza a `CreacionLotesRecepcion` de la v1
(la del `'timer Submit'`, el form oculto, `varCurrentRow`/`varFirstRow`/`varRowIndex`,
la renumeración manual O(n³), la paginación de 9 filas, los `Patch` sin `IfError` y el
trash que leía `ThisItem.GUID` **después** de mutar la colección).

Acá: **todo se edita en colecciones locales** (`colPlanillas`, `colTulas`), la
validación sale de un único lugar (`fxProblemas`, `01` §3.1), el guardado es **un
`Patch` con tabla** (receta `01` §7.5) y **todo borrado confirma**.

> **Todo lo que esta guía no define, lo define `01`.** Paleta, formatos, defaults
> tipográficos, alias `ESTILO-*`, catálogo cerrado de tipos, recetas `7.1..7.10` y
> reglas de delegación `D-1..D-9` viven allá y **mandan**. Acá solo hay: nombres finales,
> coordenadas, y las fórmulas propias de esta pantalla.

---

## CONTRATO DE ENTRADA

| Qué | Quién lo setea | Obligatorio |
|---|---|---|
| App creada con los **6 datasources** de `01` §2.1, `App.Formulas`, `App.OnStart`, `App.OnError`, `BackEnabled = false`, `StartScreen = scrCaptura`, `ConfirmExit` | Guía `01` §1–§3 | **Sí** — sin `App.OnStart` corrido, `colPlanillas`/`colTulas` no existen y media pantalla queda en rojo en tiempo de diseño |
| `gvarLoteActivo` | `App.OnStart` lo deja en `Blank()`. Lo setea **esta pantalla** (botón *Crear lote*, §7.1) o **`03`** (botón *Editar lote*) | No — `Blank()` es un estado válido: dispara el overlay de nuevo lote (§7.1) |
| `colPlanillas`, `colTulas`, `colPlanillasDV`, `colTulasDV` | `App.OnStart` las crea vacías con esquema. **`03` las llena** al hacer *Editar lote*, ANTES de `Navigate(scrCaptura)` | No — si llegan vacías con `gvarLoteActivo` seteado, el `OnVisible` las **re-hidrata solo** (§1, bloque 2). Esa es la red de seguridad del contrato |
| `gvarCambiosSinGuardar` | `App.OnStart` (`false`). Esta pantalla lo pone en `true` en cada edición y en `false` **solo** cuando el guardado no dejó fallidas | Sí |

### Qué debe hacer `03` al mandar un lote a esta pantalla

Este es el **contrato exacto** — copia literal del canónico de `01` §4.3, que es la
ÚNICA definición. `03` §9 pega este mismo bloque; si alguna vez difieren, manda `01`.

```powerfx
// btnEditarLoteLot.OnSelect — habilitado SOLO con el lote en 'En progreso'
Set(gvarLoteActivo, galLotesLot.Selected);

// ===== 1. Snapshots CRUDOS: dos consultas, una por tabla, para TODO el lote =====
//    PK contra PK (D-1): la igualdad de registro pelada NO COMPILA.
ClearCollect(
    colPlanillasDV,
    Filter(
        'CH-Tbl-Receptions',
        ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
    )
);
ClearCollect(
    colTulasDV,
    Filter(
        'CH-Tbl-Bags',
        ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
    )
);

// ===== 2. Vaciar las colecciones de EDICIÓN — 🧨 OBLIGATORIO =====
//    scrLotes NO las proyecta: lo hace el OnVisible de scrCaptura con la receta
//    01 §7.10. Vaciarlas es lo que HABILITA esa proyección: su guarda es
//    IsEmpty(colPlanillas). Si el usuario venía editando OTRO lote y no se
//    vacían, la proyección no dispara y scrCaptura muestra las filas del lote
//    anterior con el gvarLoteActivo nuevo.
Clear(colPlanillas); Clear(colTulas);
Clear(colProblemas); Clear(colFallidas);

// ===== 3. Salida =====
Set(gvarCambiosSinGuardar, false);
Notify("Lote " & galLotesLot.Selected.Name & " cargado en Captura.", NotificationType.Information);
Navigate(scrCaptura)
```

`03` **no proyecta** `colPlanillas`/`colTulas` y **no debe** hacerlo: el `OnVisible`
de esta pantalla las arma desde los dos snapshots `…DV` con la **receta `01` §7.10**,
que es la única definición de esa proyección. Dos motivos:

1. La proyección rehidrata `Empresa` como **registro fresco de sucursal** con un
   `LookUp` contra `CH-Tbl-ClientBranches`, y esta pantalla es la que lo consume: si
   `03` lo corriera, pagaría esas consultas antes de navegar, para nada.
2. Dos copias del mismo `ForAll` se desincronizan a la primera que se toque una — y
   entonces el re-keyado de la receta 7.5 deja de encontrar sus filas y el segundo
   guardado **duplica registros**.

> ⚠️ **`Empresa` NUNCA viaja como `D.BranchName`** (la referencia cruda del lookup):
> la receta 7.5 hace `P.Empresa.Client` y `P.Empresa.Name`, y con la referencia cada
> fila costaría un fetch del registro relacionado (N+1) **y `RemitentClient` se
> guardaría en blanco**. Se rehidrata contra el datasource, por PK.

> ⚠️ **`03` NO debe navegar acá con un lote que no sea `En progreso`.** Esta pantalla
> escribe planillas y tulas sin pedir aprobación: es el camino directo de `01` §5.4,
> fila `En progreso`. Los lotes `Recibido` y `En boveda` se operan desde `03`.

### Variables y colecciones **propias** de esta pantalla

Las globales (`gvarLoteActivo`, `gvarCambiosSinGuardar`) y las 6 colecciones
sembradas en `App.OnStart` están en `01` §4. Lo que **nace acá** y `01` §4 **no
declara** (el inventario de huecos de `01` está en §10.4):

| Nombre | Tipo | Para qué |
|---|---|---|
| `colTransportadoras` | Colección | `Items` del combo de transportadora (`01` §4). **Es el ÚNICO catálogo en colección** de esta pantalla: la tabla es estructuralmente acotada (regla **D-10**) |
| `colTulasEdit` | Colección | Copia temporal del panel de tulas. **Cancelar la descarta; solo Aplicar la vuelca** |
| `colTulasBorradasCap` | Colección | Tulas **con GUID** que el usuario quitó del panel: el guardado las borra de Dataverse |
| `colTulasNuevoCap` | Colección | Buffer de reconstrucción de `colTulas` (evita mutar una colección mientras un `Filter` la está leyendo) |
| `colErrBorrarCap` | Colección `{ Mensaje }` | Errores del borrado de planilla (no se puede `Set()` dentro de un `ForAll` — `01` §3.2) |
| `colEstadoGuardadoCap` | Colección `{ Planilla, Ok, Mensaje }` | Lista de estado por planilla del overlay de guardado |
| `colCambiosPlanCap` | Colección | **Delta de planillas** que el guardado va a patchear: nuevas + distintas del snapshot (`01` receta 7.11.D) |
| `colTulasNuevasCap` | Colección | **Delta de tulas**: `Filter(colTulas, IsBlank(GUID))` |
| `ctxDlgTulasCap`, `ctxDlgBorrarFilaCap`, `ctxDlgCerrarCap`, `ctxDlgSalirCap`, `ctxDlgResultadoCap` | Booleanos | `Visible` de cada overlay (`01` §4, `ctxDlg…`) |
| `ctxGuardandoCap` | Booleano | **Bandera "en vuelo" que SÍ se pinta.** Es el `Start` de `tmrGuardarCap` y el `Visible` del overlay D en modo *"Guardando N cambios…"*. La sube el `OnSelect`, la baja el `OnTimerEnd` (`01` receta 7.11.C) |
| `ctxSalirTrasGuardarCap` | Booleano | *Guardar y salir*: le dice al `OnTimerEnd` que navegue **después** de guardar. Los timers **no corren en pantallas no visibles** ⇒ navegar antes mata el guardado |
| `ctxScanDupCap` | Texto | Mensaje de duplicado del escáner de tulas (§7.2). `""` = sin error |
| `ctxGuardarDesdeCierreCap` | Booleano | **Discriminador de ORIGEN del guardado**: lo ponen en `true` §9.2 y §9.3 antes de `Select(btnGuardarCap)` y decide si el camino feliz abre el overlay D en modo resultado y si el `OnTimerEnd` sigue con el cierre (§8.4). Se consume al final del `OnTimerEnd` |
| `ctxPlanillaTulasCap`, `ctxPlanillaTulasTxtCap`, `ctxPlanillaTulasEmpCap` | Texto | Fila cuyo panel de tulas está abierto (`LocalId`, N° y empresa) |
| `ctxFilaBorrarCap`, `ctxFilaBorrarTxtCap`, `ctxFilaBorrarTulasCap`, `ctxFilaBorrarGuidCap` | Texto / número / GUID | Fila que el diálogo de borrado va a eliminar |

> **Por qué el `LocalId` y no el registro**: los diálogos guardan el **`LocalId`** de la
> fila, nunca `galPlanillasCap.Selected` ni el registro. Entre que abrís el diálogo y
> confirmás, la colección puede mutar; el `LocalId` es la única clave que sobrevive. Es
> el fix estructural del bug **N2** de la v1.

---

## Árbol de controles

Orden del árbol = **orden de inserción = z-order** (`01` §0.6): lo insertado después
queda encima. Construcción en **DOS PASES** (`01` §0.11): primero insertar y renombrar
todo en este orden exacto, después llenar propiedades.

```
scrCaptura
├─ shpHeaderCap                    Rectángulo clásico
├─ shpHeaderLineaCap               Rectángulo clásico
├─ imgMarcaCap                     Imagen clásica
├─ lblTituloCap                    Etiqueta clásica
├─ lblSubtituloCap                 Etiqueta clásica
├─ shpSegNavCap                    Botón clásico          (receta 7.8)
├─ segCapturaCap                   Botón clásico          (receta 7.8 — activo)
├─ segLotesCap                     Botón clásico          (receta 7.8 — inactivo + guarda)
├─ shpChipLoteCap                  Botón clásico          (receta 7.1)
├─ lblChipLoteNumCap               Etiqueta clásica
├─ lblChipLoteTransCap             Etiqueta clásica
├─ pilEstadoLoteCap                Botón clásico          (receta 7.4)
├─ shpChipUserCap                  Botón clásico          (receta 7.1)
├─ btnAvatarCap                    Botón clásico          (receta 7.1)
├─ lblUsuarioCap                   Etiqueta clásica
├─ lblMetaCap                      Etiqueta clásica
├─ btnValidarCap                   Botón clásico
├─ icoValidarCap                   Icono clásico  Icon.Check
├─ btnGuardarCap                   Botón clásico
├─ btnCerrarCap                    Botón clásico
├─ shpCardGrillaCap                Botón clásico          (receta 7.1 — card)
├─ shpHeadGrillaCap                Botón clásico          (receta 7.1 — banda de encabezados)
├─ lblColIdxCap                    Etiqueta clásica
├─ lblColEmpresaCap                Etiqueta clásica
├─ lblColPlanillaCap               Etiqueta clásica
├─ lblColChequeNIOCap              Etiqueta clásica
├─ lblColChequeUSDCap              Etiqueta clásica
├─ lblColEfectivoNIOCap            Etiqueta clásica
├─ lblColEfectivoUSDCap            Etiqueta clásica
├─ lblColTulasCap                  Etiqueta clásica
├─ shpLineaHeadGrillaCap           Rectángulo clásico
├─ galPlanillasCap                 Galería vertical en blanco
│  ├─ shpFilaCap                   Botón clásico          (receta 7.1 — fondo ámbar, al FONDO)
│  ├─ lblIdxCap                    Etiqueta clásica
│  ├─ cmbEmpresaCap                Cuadro combinado clásico
│  ├─ inpPlanillaCap               Entrada de texto clásica
│  ├─ inpChequeNIOCap              Entrada de texto clásica
│  ├─ inpChequeUSDCap              Entrada de texto clásica
│  ├─ inpEfectivoNIOCap            Entrada de texto clásica
│  ├─ inpEfectivoUSDCap            Entrada de texto clásica
│  ├─ btnChipTulasCap              Botón clásico
│  ├─ icoBorrarFilaCap             Icono clásico  Icon.Trash
│  └─ shpSepFilaCap                Rectángulo clásico
├─ lblVacioGrillaCap               Etiqueta clásica
├─ shpLineaAddCap                  Rectángulo clásico
├─ btnNuevaPlanillaCap             Botón clásico
├─ icoNuevaPlanillaCap             Icono clásico  Icon.Add
├─ shpTotChequeNIOCap              Botón clásico          (receta 7.1)
├─ lblTotChequeNIOLabCap           Etiqueta clásica
├─ lblTotChequeNIOCap              Etiqueta clásica
├─ shpTotChequeUSDCap              Botón clásico          (receta 7.1)
├─ lblTotChequeUSDLabCap           Etiqueta clásica
├─ lblTotChequeUSDCap              Etiqueta clásica
├─ shpTotEfectivoNIOCap            Botón clásico          (receta 7.1)
├─ lblTotEfectivoNIOLabCap         Etiqueta clásica
├─ lblTotEfectivoNIOCap            Etiqueta clásica
├─ shpTotEfectivoUSDCap            Botón clásico          (receta 7.1)
├─ lblTotEfectivoUSDLabCap         Etiqueta clásica
├─ lblTotEfectivoUSDCap            Etiqueta clásica
├─ shpPanelValCap                  Botón clásico          (receta 7.1)
├─ lblValTituloCap                 Etiqueta clásica
├─ shpDotOkValCap                  Botón clásico          (receta 7.1 — punto)
├─ lblValVacioCap                  Etiqueta clásica
├─ galProblemasCap                 Galería vertical en blanco
│  ├─ shpDotProblemaCap            Botón clásico          (receta 7.1 — punto)
│  └─ lblProblemaCap               Etiqueta clásica
├─ cntDlgNuevoLoteCap              Contenedor de diseño manual   ── OVERLAY A (receta 7.2)
│  ├─ btnDlgBloqueoNuevoLoteCap    Botón clásico
│  └─ cntDlgCardNuevoLoteCap       Contenedor de diseño manual
│     ├─ lblDlgTitNuevoLoteCap     Etiqueta clásica
│     ├─ lblDlgTxtNuevoLoteCap     Etiqueta clásica
│     ├─ lblCampoTransCap          Etiqueta clásica
│     ├─ cmbTransportadoraCap      Cuadro combinado clásico
│     ├─ btnIrALotesCap            Botón clásico
│     └─ btnCrearLoteCap           Botón clásico
├─ cntDlgTulasCap                  Contenedor de diseño manual   ── OVERLAY B (receta 7.2)
│  ├─ btnDlgBloqueoTulasCap        Botón clásico
│  └─ cntDlgCardTulasCap           Contenedor de diseño manual
│     ├─ lblDlgTitTulasCap         Etiqueta clásica
│     ├─ lblDlgTxtTulasCap         Etiqueta clásica
│     ├─ icoDlgXTulasCap           Icono clásico  Icon.Cancel
│     ├─ inpTulaScanCap            Entrada de texto clásica   ── ESCÁNER (§7.2)
│     ├─ lblErrScanCap             Etiqueta clásica
│     ├─ galTulasEditCap           Galería vertical en blanco
│     │  ├─ inpTulaEditCap         Entrada de texto clásica   (SOLO LECTURA)
│     │  ├─ icoBorrarTulaEditCap   Icono clásico  Icon.Trash
│     │  └─ lblErrTulaEditCap      Etiqueta clásica
│     ├─ lblVacioTulasCap          Etiqueta clásica
│     ├─ btnDlgCancelTulasCap      Botón clásico
│     └─ btnDlgOkTulasCap          Botón clásico
├─ cntDlgBorrarFilaCap             Contenedor de diseño manual   ── OVERLAY C (receta 7.2)
│  ├─ btnDlgBloqueoBorrarFilaCap   Botón clásico
│  └─ cntDlgCardBorrarFilaCap      Contenedor de diseño manual
│     ├─ lblDlgTitBorrarFilaCap    Etiqueta clásica
│     ├─ lblDlgTxtBorrarFilaCap    Etiqueta clásica
│     ├─ icoDlgXBorrarFilaCap      Icono clásico  Icon.Cancel
│     ├─ btnDlgCancelBorrarFilaCap Botón clásico
│     └─ btnDlgOkBorrarFilaCap     Botón clásico
├─ cntDlgGuardarCap                Contenedor de diseño manual   ── OVERLAY D (receta 7.2 + 7.5)
│  ├─ btnDlgBloqueoGuardarCap      Botón clásico
│  └─ cntDlgCardGuardarCap         Contenedor de diseño manual
│     ├─ lblDlgTitGuardarCap       Etiqueta clásica
│     ├─ shpBarraFondoCap          Botón clásico          (receta 7.1)
│     ├─ shpBarraProgresoCap       Botón clásico          (receta 7.1)
│     ├─ lblDlgTxtGuardarCap       Etiqueta clásica
│     ├─ galEstadoGuardarCap       Galería vertical en blanco
│     │  ├─ shpDotEstadoCap        Botón clásico          (receta 7.1 — punto)
│     │  └─ lblEstadoCap           Etiqueta clásica
│     ├─ btnDlgListoGuardarCap     Botón clásico
│     ├─ btnDlgCerrarGuardarCap    Botón clásico
│     └─ btnDlgOkGuardarCap        Botón clásico
├─ cntDlgCerrarCap                 Contenedor de diseño manual   ── OVERLAY E (receta 7.2 + 7.9)
│  ├─ btnDlgBloqueoCerrarCap       Botón clásico
│  └─ cntDlgCardCerrarCap          Contenedor de diseño manual
│     ├─ lblDlgTitCerrarCap        Etiqueta clásica
│     ├─ lblDlgTxtCerrarCap        Etiqueta clásica
│     ├─ icoDlgXCerrarCap          Icono clásico  Icon.Cancel
│     ├─ btnDlgCancelCerrarCap     Botón clásico
│     └─ btnDlgOkCerrarCap         Botón clásico
└─ cntDlgSalirCap                  Contenedor de diseño manual   ── OVERLAY F (receta 7.2 + 7.8)
   ├─ btnDlgBloqueoSalirCap        Botón clásico
   └─ cntDlgCardSalirCap           Contenedor de diseño manual
      ├─ lblDlgTitSalirCap         Etiqueta clásica
      ├─ lblDlgTxtSalirCap         Etiqueta clásica
      ├─ icoDlgXSalirCap           Icono clásico  Icon.Cancel
      ├─ btnDlgCancelSalirCap      Botón clásico
      ├─ btnDlgDescartarSalirCap   Botón clásico
      └─ btnDlgOkSalirCap          Botón clásico
   ── FUERA DE TODO OVERLAY, último de la pantalla ──
tmrGuardarCap                      Temporizador clásico   (§8.4, receta 01 §7.11.C)
```

**129 controles.** (Contra los 128 de la 1.31: **−2** por el panel de tulas
scan-first —mueren `btnAgregarTulaCap` e `icoAgregarTulaCap`—, **+2** por el escáner
—`inpTulaScanCap`, `lblErrScanCap`— y **+1** por `tmrGuardarCap`. `128 − 2 + 2 + 1 = 129`.)
El panel de árbol de Studio los lista **al revés** (el frontal arriba): de eso
dependen las anotaciones de z-order de esta guía — `shpFilaCap` al FONDO del
template, los 6 overlays al TOPE de la pantalla en el orden A→F.

> 🧨 **`tmrGuardarCap` va FUERA de los overlays y es el ÚLTIMO control de la
> pantalla.** Es invisible (`Visible false`) y no dibuja nada, pero **tiene que vivir
> en la pantalla, no dentro de un contenedor de diálogo**: un timer dentro de un
> contenedor con `Visible false` **no corre**, y el overlay D está oculto justo en el
> instante en que el timer tiene que arrancar.

### Regla de coordenadas de template (se enuncia UNA vez, `01` §0.7)

> **X de un control del template = X del encabezado − X de la galería.**
> `galPlanillasCap.X = 18`, así que todo control de su template lleva `X_encabezado − 18`.
> Lo mismo para `galProblemasCap` (`X = 1114`), `galTulasEditCap` (X relativo a su card)
> y `galEstadoGuardarCap`.

### Mapa de la pantalla (1366 × 768, medido contra el CSS del mockup)

| Banda | Y | Alto | De dónde sale |
|---|---|---|---|
| Header | `0` | `56` | `.hdr{height:56px}` |
| Toolbar | `68` | `32` | `.toolbar{padding:12px 18px 0}` + `.btn` de 32 ⇒ banda 56→100 (44 px) |
| Cuerpo | `112` | `640` | `.body2{padding:12px 18px 16px}` ⇒ `56+12+32+12 = 112`; abajo `768 − 16 = 752` |
| Card de grilla | X `18` · W `1068` | | `1366 − 18 − 18 − 248 − 14` (`.body2 gap:14`, `.rail width:248`) |
| Rail | X `1100` · W `248` | | `18 + 1068 + 14`; termina en `1348 = 1366 − 18` |

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | ver abajo — **COMPLETO, listo para pegar** |

```powerfx
// ============================================================================
// BLOQUE 1 — CATÁLOGO. La ÚNICA consulta fija de esta pantalla.
//   Se carga una sola vez por sesión: volver de scrLotes no vuelve a consultar.
//   🧨 NO HAY CATÁLOGO DE SUCURSALES (L-23, regla D-10): la tabla tiene ~50.000
//   filas activas y un ClearCollect truncaba en DataRowLimit EN SILENCIO. El
//   combo de empresa va SERVER-SIDE (§5.4). Transportadoras sí: es una tabla
//   estructuralmente acotada (decenas de filas por contrato).
//   🧨 La vista va como SEGUNDO argumento de Filter: Filter('X (Views)'.'v', …)
//   NO COMPILA (01 §2.5).
// ============================================================================
If(
    IsEmpty(colTransportadoras),
    ClearCollect(
        colTransportadoras,
        Filter(
            'CH-Tbl-TransportCompanies',
            'CH-Tbl-TransportCompanies (Views)'.'CH-Tbl-TransportCompany Lookup View'
        )
    )
);

// ============================================================================
// BLOQUE 2 — RED DE SEGURIDAD DEL CONTRATO DE ENTRADA.
//   Si hay lote activo pero las colecciones de trabajo llegaron vacías (03 navegó
//   sin cargarlas, o la sesión se recuperó), se re-hidratan con 2 consultas
//   delegables. Con el contrato cumplido, este If no dispara ni una consulta.
//   El If va POR FUERA del ClearCollect (gotcha de 01: ClearCollect(col, If(cond,…))
//   sin else revienta con ErrorKind.Internal).
// ============================================================================
If(
    !IsBlank(gvarLoteActivo) && IsEmpty(colPlanillas) && IsEmpty(colPlanillasDV),
    ClearCollect(
        colPlanillasDV,
        Filter(
            'CH-Tbl-Receptions',
            ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
        )
    );
    ClearCollect(
        colTulasDV,
        Filter(
            'CH-Tbl-Bags',
            ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
        )
    )
);

// ============================================================================
// RECETA 01 §7.10 — proyección DV -> colecciones de EDICIÓN.
//   Es la ÚNICA definición de esta proyección en toda la app: 03 NO la escribe,
//   solo deja los snapshots …DV y las de edición vacías (contrato, 01 §4.3).
//   LocalId de una fila YA persistida = Text(GUID real)  -> estable entre visitas
//   LocalId de una fila nueva          = Text(GUID())    -> aleatorio, local (01 §4.1)
// ============================================================================
If(
    !IsBlank(gvarLoteActivo) && IsEmpty(colPlanillas) && !IsEmpty(colPlanillasDV),
    ClearCollect(
        colPlanillas,
        ForAll(
            colPlanillasDV As D,
            {
                LocalId:     Text(D.'CH-Tbl-Receptions'),
                GUID:        D.'CH-Tbl-Receptions',
                // Registro FRESCO del datasource por PK: trae el lookup Client
                // navegable => RemitentClient nunca sale en blanco (L-23).
                Empresa:     LookUp('CH-Tbl-ClientBranches',
                                    'CH-Tbl-ClientBranch' = D.BranchName.'CH-Tbl-ClientBranch'),
                Planilla:    D.ReceiptID,
                ChequeNIO:   D.NIOCheck,
                ChequeUSD:   D.USDCheck,
                EfectivoNIO: D.NIOCash,
                EfectivoUSD: D.USDCash
            }
        )
    );
    ClearCollect(
        colTulas,
        ForAll(
            colTulasDV As B,
            {
                LocalId:         Text(B.'CH-Tbl-Bags'),
                PlanillaLocalId: Text(B.ReceptionID.'CH-Tbl-Receptions'),
                Numero:          B.BagID,
                GUID:            B.'CH-Tbl-Bags'
            }
        )
    )
);

// ============================================================================
// BLOQUE 3 — ESQUEMA de las colecciones propias de la pantalla.
//   FirstN(col, 0) devuelve una tabla VACÍA con el esquema de col: siembra el
//   tipo sin consultar nada y sin duplicar la definición de columnas de 01 §3.2.
//   🧨 NO se usa Filter(col, false): compila, pero el linter de Studio lo marca
//   como predicado constante y deja un warning permanente en el App Checker.
// ============================================================================
ClearCollect(colTulasBorradasCap, FirstN(colTulas, 0));
ClearCollect(colTulasNuevoCap,    FirstN(colTulas, 0));
ClearCollect(colTulasNuevasCap,   FirstN(colTulas, 0));
ClearCollect(colCambiosPlanCap,   FirstN(colPlanillas, 0));
Collect(colErrBorrarCap,       { Mensaje: "" });                          Clear(colErrBorrarCap);
Collect(colEstadoGuardadoCap,  { Planilla: "", Ok: false, Mensaje: "" }); Clear(colEstadoGuardadoCap);
Clear(colTulasEdit);

// ============================================================================
// BLOQUE 4 — ESTADO DE UI. Todos los diálogos CERRADOS al entrar.
//   El estado de galerías y diálogos PERSISTE entre visitas: sin este reset,
//   volver de scrLotes te encuentra un modal abierto de la visita anterior.
// ============================================================================
UpdateContext({
    ctxDlgTulasCap:        false,
    ctxDlgBorrarFilaCap:   false,
    ctxDlgCerrarCap:       false,
    ctxDlgSalirCap:        false,
    ctxDlgResultadoCap:    false,
    ctxGuardandoCap:          false,   // baja el Start de tmrGuardarCap
    ctxSalirTrasGuardarCap:   false,
    ctxGuardarDesdeCierreCap: false,
    ctxScanDupCap:          "",
    ctxPlanillaTulasCap:    "",
    ctxPlanillaTulasTxtCap: "",
    ctxPlanillaTulasEmpCap: "",
    ctxFilaBorrarCap:       "",
    ctxFilaBorrarTxtCap:    "",
    ctxFilaBorrarTulasCap:  0
});
// GUID TIPADO en blanco: Blank() pelado deja la variable sin tipo y el
// LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = ctxFilaBorrarGuidCap) no compila.
UpdateContext({ ctxFilaBorrarGuidCap: Defaults('CH-Tbl-Receptions').'CH-Tbl-Receptions' });

Clear(colProblemas);
Clear(colFallidas);
Reset(galPlanillasCap);
Reset(cmbTransportadoraCap)
```

> - **Este `OnVisible` no tiene ningún `Set()` dentro de un `ForAll`** (`01` §3.2). Los
>   `ForAll` de arriba solo **proyectan registros**; no escriben variables.
> - **No borra `colPlanillas`/`colTulas`.** Si las borrara, retomar un lote desde `03`
>   sería imposible y cada ida y vuelta a *Lotes* perdería lo tecleado.
> - `Reset(galPlanillasCap)` re-ancla los `Default` de los inputs de fila al contenido
>   real de la colección (gotcha: el estado de una galería sobrevive entre visitas).

---

## 2. Consultas a Dataverse de esta pantalla — inventario CERRADO

Todo lo demás corre sobre colecciones locales: **no hay delegación que romper**
(`01` §2.3). Estas son **las únicas** llamadas a la red, con su regla `D-*`:

| # | Fórmula | Dónde | Veredicto |
|---|---|---|---|
| Q1 | `Filter('CH-Tbl-ClientBranches', RecordStatus = 'CH-Ch-RecordStatus'.Active)` | **§5.4 — `cmbEmpresaCap.Items`, SERVER-SIDE** | **Delegable** — igualdad contra el literal del option set **GLOBAL** (`01` §2.4). Comparar contra el entero crudo `159460000` **NO** delega en una picklist, y el enum `'RecordStatus (CH-Tbl-ClientBranches)'` **no existe** |
| Q2 | `Filter('CH-Tbl-TransportCompanies', 'CH-Tbl-TransportCompanies (Views)'.'…Lookup View')` | §1 bloque 1 | Vista completa de un catálogo de ~decenas de filas, **como 2º argumento** (`01` §2.5). No hay filtro que delegar |
| Q3 | `Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch')` | §1 bloque 2 · §8.4 · §7.3 borrado | **Delegable** — **PK contra PK** (regla **D-1**), jamás `ReceptionBatch = gvarLoteActivo` (no compila) ni `ReceptionBatch.Name = …` (no delega) |
| Q4 | `Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch')` | §1 bloque 2 · §8.4 · §7.3 borrado | **Delegable** — espejo **B-2** (regla **D-7**), PK contra PK, jamás `ReceptionID.ReceptionBatch` |
| Q5 | `Patch('CH-Tbl-ReceptionBatches', Defaults(…), {…})` | §7.1 crear lote | Escritura de 1 registro. `Name` es autonumber: **no se patchea** (`01` §2.4) |
| Q6 | `Patch('CH-Tbl-Receptions', <tabla>, <tabla>)` | §8.4 fases A/B | **UNA** llamada en el camino feliz, y **solo sobre el delta** `colCambiosPlanCap` (recetas 7.5 + 7.11.D) |
| Q7 | `Patch('CH-Tbl-Bags', <tabla>, <tabla>)` | §8.4 | Ídem sobre `colTulasNuevasCap`, **después** de las planillas (7.5 regla 4) |
| Q8 | `Remove('CH-Tbl-Bags', <registro>)` / `Remove('CH-Tbl-Receptions', <registro>)` | §7.3 · §8.4 paso de bajas | Registro explícito, **nunca `RemoveIf(ds, predicado)`** (regla **D-3**) |
| Q9 | `Patch('CH-Tbl-ReceptionBatches', gvarLoteActivo, { ReceptionStatus: Recibido })` | §8.4 continuación del cierre | Receta 7.9 |
| Q10 | `LookUp('CH-Tbl-ClientBranches', 'CH-Tbl-ClientBranch' = D.BranchName.'CH-Tbl-ClientBranch')` | §1, receta `01` §7.10 | **Delegable** (`eq` por PK). **Una por planilla del lote**, solo al retomar un lote — acotado al tamaño del lote, no a la tabla. Es el costo declarado de haber eliminado el catálogo local (**L-23**) |

**Cero `Refresh()`** (regla **D-5**) · **cero `CountRows(Filter(ds,…))`** (**D-2**) ·
**cero `Upper()`/`Sum()` sobre datasource** (**D-8**).

> 🧨 **Q1 es la ÚNICA excepción declarada a D-4** (`Filter` sobre datasource en el
> `Items` de un control **dentro** del template de una galería). La habilita `01`
> §2.3 D-4 y la obliga **D-10**: `CH-Tbl-ClientBranches` tiene ~50 000 filas activas
> y **no se puede cachear**. Un Cuadro combinado con `IsSearchable true` consulta
> **al desplegarse y al tipear**, no una vez por fila renderizada: el N+1 que D-4
> previene no ocurre. Cualquier OTRO `Filter`/`LookUp` sobre datasource dentro del
> template sigue prohibido.

> Todos los `CountRows(Filter(…))` de esta guía son sobre `colPlanillas`, `colTulas`,
> `colProblemas`, `colFallidas` o `colTulasEdit` — **memoria del cliente**.

---

## 3. Header (Y `0`–`56`)

| Control | Tipo | Propiedades |
|---|---|---|
| `shpHeaderCap` | Rectángulo clásico | X `0` · Y `0` · Width `Parent.Width` · Height `56` · `Fill` `ThemeSurface` — banda de esquina viva: por eso es Rectángulo y no Botón (`01` §0.3) |
| `shpHeaderLineaCap` | Rectángulo clásico | X `0` · Y `shpHeaderCap.Height` · Width `Parent.Width` · Height `1` · `Fill` `ThemeLine` |
| `imgMarcaCap` | Imagen clásica | X `18` · Y `11` · Width `34` · Height `34` · `Radius` (las 4) `9` · `Image` `IconoReception192` · `ImagePosition` `ImagePosition.Fill` |

> 🧨 **El ícono de la app NO sirve como `Image`.** `01` §1.2 lo sube como *ícono de la
> aplicación*, que es otra cosa. El medio se carga aparte y con el nombre canónico
> que fija `01` §1.2: **Medios → Cargar** → `docs/marca/icono-reception-192.png`,
> renombrado a **`IconoReception192`**. Es el MISMO recurso que usa `imgTileLot` en
> `03` (un solo medio para las dos pantallas). Hueco **H-9**, ya corregido en `01` §1.2.
| `lblTituloCap` | Etiqueta clásica | X `66` · Y `11` · Width `120` · Height `18` · **ESTILO-TITULO-APP** · `Text` `"Recepción"` |
| `lblSubtituloCap` | Etiqueta clásica | X `66` · Y `lblTituloCap.Y + lblTituloCap.Height` · Width `120` · Height `15` · **ESTILO-SUBTITULO-APP** · `Text` `"Captura de lote"` |
| `shpSegNavCap` | Botón clásico | **receta 7.8** · X `210` · Y `12` |
| `segCapturaCap` | Botón clásico | **receta 7.8**, columna **activo** · X `shpSegNavCap.X + 3` · Y `shpSegNavCap.Y + 3` · `Text` `"Captura"` · `OnSelect` `false` |
| `segLotesCap` | Botón clásico | **receta 7.8**, columna **inactivo** · X `segCapturaCap.X + segCapturaCap.Width + 3` · Y `segCapturaCap.Y` · `Text` `"Lotes"` · `OnSelect` ver abajo |
| `shpChipLoteCap` | Botón clásico | receta 7.1 · X `shpSegNavCap.X + shpSegNavCap.Width + 14` · Y `12` · Width `14 + lblChipLoteNumCap.Width + lblChipLoteTransCap.Width + 8 + pilEstadoLoteCap.Width + 8` · Height `32` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `16` · `DisplayMode` `DisplayMode.View` · **`Visible` `!IsBlank(gvarLoteActivo)`** |
| `lblChipLoteNumCap` | Etiqueta clásica | X `shpChipLoteCap.X + 14` · Y `shpChipLoteCap.Y` · Width `6.5 * Len(Self.Text) + 4` · Height `shpChipLoteCap.Height` · **ESTILO-CELDA** con `FontWeight` `FontWeight.Semibold` y `Color` `ThemeInk` · `Visible` `!IsBlank(gvarLoteActivo)` · `Text` `gvarLoteActivo.Name` |
| `lblChipLoteTransCap` | Etiqueta clásica | X `lblChipLoteNumCap.X + lblChipLoteNumCap.Width` · Y `shpChipLoteCap.Y` · Width `6.2 * Len(Self.Text) + 4` · Height `shpChipLoteCap.Height` · **ESTILO-CELDA** · `Color` `ThemeInk2` · `Visible` `!IsBlank(gvarLoteActivo)` · `Text` ver abajo |
| `pilEstadoLoteCap` | Botón clásico | **receta 7.4** · X `lblChipLoteTransCap.X + lblChipLoteTransCap.Width + 8` · Y `shpChipLoteCap.Y + (shpChipLoteCap.Height - Self.Height) / 2` (= `17`: centrada en el chip, no un literal que se desincroniza si el chip cambia de alto) · `Visible` `!IsBlank(gvarLoteActivo)` · `Text`/`Fill`/`Color` = los 3 `Switch` de 7.4 **con `gvarLoteActivo.ReceptionStatus` en lugar de `ThisItem.ReceptionStatus`** |
| `shpChipUserCap` | Botón clásico | receta 7.1 · X `Parent.Width - 18 - Self.Width` · Y `11` · Width `5 + btnAvatarCap.Width + 8 + lblUsuarioCap.Width + 12` · Height `34` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `17` · `DisplayMode` `DisplayMode.View` |
| `btnAvatarCap` | Botón clásico | receta 7.1 · X `shpChipUserCap.X + 5` · Y `15` · Width `26` · Height `26` · `Radius` (las 4) `13` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccentSoft` · `Color` `ThemeAccent` · `Size` `7.88` · `FontWeight` `FontWeight.Bold` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Text` `UsuarioIniciales` |
| `lblUsuarioCap` | Etiqueta clásica | X `btnAvatarCap.X + btnAvatarCap.Width + 8` · Y `shpChipUserCap.Y` · Width `5.8 * Len(Self.Text) + 6` · Height `shpChipUserCap.Height` · `Size` `8.63` · `Color` `ThemeInk2` · `Text` `UsuarioNombre` |

```powerfx
// lblChipLoteTransCap.Text
"   ·   " & gvarLoteActivo.Transportadora
```

> **Por qué el chip va en DOS etiquetas y no en una**: el mockup pinta el `Name` del lote
> en **semibold sobre `ThemeInk`** y la transportadora en **regular sobre `ThemeInk2`**.
> Una Etiqueta clásica tiene un solo `FontWeight` y un solo `Color` (mismo límite que
> `L-16`), así que la única forma nativa de conseguir el contraste es partirla. El ancho
> del semibold se estima con `6.5 * Len` en vez de `6.2 * Len`: pesa un poco más.

```powerfx
// segLotesCap.OnSelect — receta 7.8, con guarda de cambios sin guardar
If(
    gvarCambiosSinGuardar,
    UpdateContext({ ctxDlgSalirCap: true }),
    Navigate(scrLotes)
)
```

> Esto **compila desde el primer día** porque `01` §1.8 manda crear las dos pantallas
> vacías antes de vestir ninguna. Si `scrLotes` todavía no existe, no sigas: creala.

> **Cadena de anclaje del header** (`01` §0.9, cero números mágicos duplicados):
> `imgMarcaCap` → `lblTituloCap`/`lblSubtituloCap` → `shpSegNavCap` (X `210` = `66 + 120 + 24`,
> el `gap:14` del `.hdr` más el `margin-left:10` del `.navSeg`) → `shpChipLoteCap`
> → y desde la derecha, `shpChipUserCap` anclado a `Parent.Width - 18`.

> **Divergencia de coordenada declarada (`L-20`)**: el título usa ancho fijo `120`
> compartido con `scrLotes` (*"Administración de lotes"* lo necesita); por eso el
> segmentado queda en X `210` (el mockup, con ancho de contenido, daría ~`172`).

---

## 4. Toolbar (Y `68`–`100`)

Los tres botones se anclan **desde la derecha** (`1348 = Parent.Width − 18`), separados
por el `gap:10` del `.toolbar`. La meta ocupa la izquierda.

| Control | Tipo | Propiedades |
|---|---|---|
| `lblMetaCap` | Etiqueta clásica | X `18` · Y `68` · Width `640` · Height `32` · **ESTILO-CELDA** · `Color` `ThemeInk2` · `Text` ver abajo |
| `btnValidarCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnGuardarCap.X - 10 - Self.Width` · Y `68` · Width `104` · `Text` `"Validar"` · `Align` `Align.Left` · `PaddingLeft` `34` · `DisplayMode` ver abajo · `OnSelect` §8.1 |
| `icoValidarCap` | Icono clásico `Icon.Check` | X `btnValidarCap.X + 14` · Y `btnValidarCap.Y + 9` · Width `14` · Height `14` · `Color` `ThemeInk2` · `DisplayMode` `btnValidarCap.DisplayMode` · **`OnSelect` `Select(btnValidarCap)`** — está ENCIMA del botón: sin esto el usuario que le pega al glifo no dispara nada |
| `btnGuardarCap` | Botón clásico | **ESTILO-BTN-CONTORNO** · X `btnCerrarCap.X - 10 - Self.Width` · Y `68` · Width `96` · `Text` `"Guardar"` · `DisplayMode` ver abajo · `OnSelect` §8.2 |
| `btnCerrarCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 18 - Self.Width` · Y `68` · Width `108` · `Text` `"Cerrar lote"` · `DisplayMode` ver abajo · `OnSelect` §9.1 |

```powerfx
// lblMetaCap.Text — "N planillas · N tulas · creado <fecha>"
If(
    IsBlank(gvarLoteActivo),
    "",
    TotalPlanillas & " planillas   ·   " & TotalTulas & " tulas   ·   creado " &
    Text(gvarLoteActivo.'Created On', FmtFecha)
)
```

```powerfx
// DisplayMode de los TRES botones — la MISMA condición estructural.
// (01 §6.3: la habilitación va en DisplayMode, contra el control que la acción usa
//  Y contra el estado del lote — mata N7 y N9.)
If(
    !IsBlank(gvarLoteActivo),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

> **Por qué el gate es SOLO "hay lote activo" y no también `CountRows(colPlanillas) > 0`**:
> el mockup deja los tres botones vivos con el lote vacío y **contesta hablando**. Con
> `> 0` en el `DisplayMode`, las guardas parlantes de 0 planillas de §8.2 y §9.1 serían
> **código muerto**: nunca se llegaría a ejecutarlas. Con este gate, se ejecutan y el
> usuario se entera del motivo.
>
> **Por qué `btnCerrarCap` NO se deshabilita con `fxPuedeCerrar`**: si lo hiciera, un
> lote con problemas de validación mostraría un botón gris **sin decir por qué**. El
> gate de `DisplayMode` es *estructural* (hay lote); el gate de *validación* vive en el
> `OnSelect` (§9.1) y **habla**: `Notify` con el motivo exacto. Es lo que hace el mockup
> (`cerrarLote()` nunca deshabilita: valida y avisa). La receta 7.9 conserva igual su
> propia guarda `fxPuedeCerrar` como segunda red.

`TotalPlanillas`, `TotalTulas`, `FmtFecha` son fórmulas con nombre de `01` §3.1: no se
recalculan acá.

---

## 5. Grilla de planillas — card, encabezados y galería

`shpCardGrillaCap` es la superficie de la card; los encabezados y la galería **van
encima** (receta 7.1: la superficie al FONDO de su bloque).

### 5.1 Card y banda de encabezados

| Control | Tipo | Propiedades |
|---|---|---|
| `shpCardGrillaCap` | Botón clásico | receta 7.1 · X `18` · Y `112` · Width `1068` · Height `640` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `13` · `DisplayMode` `DisplayMode.View` |
| `shpHeadGrillaCap` | Botón clásico | receta 7.1 · X `shpCardGrillaCap.X` · Y `shpCardGrillaCap.Y` · Width `shpCardGrillaCap.Width` · Height `28` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderThickness` `0` · `Radius` **TopLeft `13` · TopRight `13` · BottomLeft `0` · BottomRight `0`** · `DisplayMode` `DisplayMode.View` |
| `shpLineaHeadGrillaCap` | Rectángulo clásico | X `shpCardGrillaCap.X` · Y `shpHeadGrillaCap.Y + shpHeadGrillaCap.Height - 1` · Width `shpCardGrillaCap.Width` · Height `1` · `Fill` `ThemeLine` |

### 5.2 Rejilla de columnas (medida contra `.tRow` del mockup)

`grid-template-columns:30px 1.6fr .8fr .75fr .75fr .75fr .75fr 62px 36px` · `gap:7px` ·
`padding:6px 14px`. Ancho útil = `1068 − 14 (padding izq.) − 22 (padding der. + barra de
scroll de la galería)` = **1032**. Fijas `30 + 62 + 36 = 128`; 8 gaps × 7 = 56; el
sobrante 848 se reparte entre 5.4 `fr` ⇒ **1 fr = 157**.

| Columna | Encabezado | X (absoluto) | Width | X en el template (`− 18`) |
|---|---|---|---|---|
| # | `lblColIdxCap` | `32` | `30` | `14` |
| Empresa / sucursal | `lblColEmpresaCap` | `69` | `250` | `51` |
| N° planilla | `lblColPlanillaCap` | `326` | `126` | `308` |
| Cheque C$ | `lblColChequeNIOCap` | `459` | `118` | `441` |
| Cheque US$ | `lblColChequeUSDCap` | `584` | `118` | `566` |
| Efectivo C$ | `lblColEfectivoNIOCap` | `709` | `118` | `691` |
| Efectivo US$ | `lblColEfectivoUSDCap` | `834` | `118` | `816` |
| Tulas | `lblColTulasCap` | `959` | `62` | `941` |
| *(papelera)* | — sin encabezado | `1028` | `36` | `1010` |

Los **8 encabezados** comparten: Tipo **Etiqueta clásica** · Y `119` · Height `14` ·
**ESTILO-HEADER-COL** (texto SIEMPRE en MAYÚSCULAS). Solo cambian `X`, `Width`, `Text`
y `Align`:

| Control | `Text` | `Align` |
|---|---|---|
| `lblColIdxCap` | `"#"` | `Align.Left` |
| `lblColEmpresaCap` | `"EMPRESA / SUCURSAL"` | `Align.Left` |
| `lblColPlanillaCap` | `"N° PLANILLA"` | `Align.Left` |
| `lblColChequeNIOCap` | `"CHEQUE C$"` | `Align.Right` |
| `lblColChequeUSDCap` | `"CHEQUE US$"` | `Align.Right` |
| `lblColEfectivoNIOCap` | `"EFECTIVO C$"` | `Align.Right` |
| `lblColEfectivoUSDCap` | `"EFECTIVO US$"` | `Align.Right` |
| `lblColTulasCap` | `"TULAS"` | `Align.Center` |

### 5.3 La galería

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X / Y | `18` / `shpLineaHeadGrillaCap.Y + shpLineaHeadGrillaCap.Height` (= `140`) |
| Width | `shpCardGrillaCap.Width` (= `1068`) |
| Height | `shpCardGrillaCap.Height - shpHeadGrillaCap.Height - 52` (= `560`) — banda de encabezados arriba (por NOMBRE, no `28` repetido), pie de "Nueva planilla" abajo |
| `TemplateSize` | **`40`** (receta 7.3: `40` para la grilla de captura) |
| `TemplatePadding` | `0` |
| `ShowScrollbar` | `true` |
| `OnSelect` | `false` — **la fila NO navega**. Es la excepción explícita de la receta 7.3: los hijos del template son editables y tienen su propia acción, así que **ninguno lleva `Select(Parent)`** |
| `Items` | ver abajo |

```powerfx
// galPlanillasCap.Items — proyección que agrega el número de fila VIVO.
// Patch(registro, {cambios}) en su forma de DOS argumentos devuelve el registro
// fusionado SIN tocar el origen: es una proyección, no una escritura.
ForAll(
    Sequence(CountRows(colPlanillas)) As S,
    Patch(Index(colPlanillas, S.Value), { Fila: S.Value })
)
```

> 🧨 **Consecuencia obligatoria de la proyección**: `ThisItem` **no es** el registro de
> `colPlanillas`, es una copia con una columna de más. Por eso **todas** las escrituras
> inline de esta galería patchean contra
> `LookUp(colPlanillas, LocalId = ThisItem.LocalId)` y **nunca** contra `ThisItem`.
> Si alguien escribe `Patch(colPlanillas, ThisItem, …)`, Studio compila y en runtime
> **agrega una fila nueva** en vez de editar la existente.
>
> **Escape declarado**: si en Studio ves que los inputs pierden el foco o el cursor
> salta al tipear (la proyección se recalcula en cada `OnChange`), cambiá
> `Items` a `colPlanillas` a secas y `lblIdxCap.Text` a `""`. Se pierde la columna `#`
> y se gana estabilidad; queda anotado en el ledger de `00` como **L-8**.

### 5.4 Template de fila (`TemplateSize 40`)

Los 5 campos editables llevan `Height 28` y `Y 6` (el `padding:6px` de `.tRow` más el
`min-height:28px` de `.cin`). **Ninguno lleva `Select(Parent)`**: son editables.

**Condición de "fila marcada"** — se escribe IGUAL en `shpFilaCap.Fill` y en el
`BorderColor` de los 5 inputs y del combo. **Son DOS orígenes, no uno**: la validación
local (`colProblemas`) y el **rechazo de Dataverse** (`colFallidas`, `01` §4.2 — su
`LocalId` es el de la **planilla dueña**, también cuando lo que falló fue una tula):

```powerfx
!IsEmpty(Filter(colProblemas, LocalId = ThisItem.LocalId)) ||
!IsEmpty(Filter(colFallidas,  LocalId = ThisItem.LocalId))
```

> **Tras cerrar el overlay D con fallidas, la grilla las mantiene marcadas**: `colFallidas`
> sobrevive al cierre del diálogo (solo la limpia el guardado siguiente), así que el
> usuario cierra el modal y **ve en la grilla exactamente qué filas rechazó el guard**.
> Sin este término, cerrar el overlay borraba toda pista visual de qué había que arreglar.

| Control | Tipo | Propiedades |
|---|---|---|
| `shpFilaCap` | Botón clásico (receta 7.1) — **al FONDO del z-order del template** | X `0` · Y `0` · Width `galPlanillasCap.Width` · Height `galPlanillasCap.TemplateSize` · `Radius` (las 4) `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Fill`/`HoverFill`/`PressedFill` (las tres iguales) `If(!IsEmpty(Filter(colProblemas, LocalId = ThisItem.LocalId)) || !IsEmpty(Filter(colFallidas, LocalId = ThisItem.LocalId)), ThemeWarnSoft, Color.Transparent)` |
| `lblIdxCap` | Etiqueta clásica | X `14` · Y `10` · Width `30` · Height `20` · **ESTILO-ID** · `Text` `Text(ThisItem.Fila)` |
| `cmbEmpresaCap` | Cuadro combinado clásico | X `51` · Y `6` · Width `250` · Height `28` · **`Items` server-side (ver abajo)** · **`Template`** `ListItemTemplate.Double` · `DisplayFields` `["sanic_name","sanic_clientnamemirror"]` · `SearchFields` `["sanic_name","sanic_clientnamemirror"]` · `IsSearchable` `true` · `SelectMultiple` `false` · `InputTextPlaceholder` `"Buscá sucursal o empresa…"` · `TabIndex` `0` · `BorderColor` ver abajo · `DefaultSelectedItems` y `OnChange` ver abajo |
| `inpPlanillaCap` | Entrada de texto clásica | X `308` · Y `6` · Width `126` · Height `28` · `Default` `ThisItem.Planilla` · `HintText` `"N° planilla"` · **`DelayOutput` `true`** · `BorderColor` ver abajo · `OnChange` ver abajo |
| `inpChequeNIOCap` | Entrada de texto clásica | X `441` · Y `6` · Width `118` · Height `28` · `Format` `TextFormat.Number` · `Align` `Align.Right` · `HintText` `"0.00"` · **`DelayOutput` `true`** · `Default` `If(ThisItem.ChequeNIO = 0, "", Text(ThisItem.ChequeNIO))` · `BorderColor` ver abajo · `OnChange` ver abajo |
| `inpChequeUSDCap` | Entrada de texto clásica | ídem, X `566` · `Default` con `ThisItem.ChequeUSD` |
| `inpEfectivoNIOCap` | Entrada de texto clásica | ídem, X `691` · `Default` con `ThisItem.EfectivoNIO` |
| `inpEfectivoUSDCap` | Entrada de texto clásica | ídem, X `816` · `Default` con `ThisItem.EfectivoUSD` |
| `btnChipTulasCap` | Botón clásico | X `950` · Y `8` · Width `44` · Height `24` · `Radius` (las 4) `12` · `Size` `8.25` · `FontWeight` `FontWeight.Semibold` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.Edit` · **`TabIndex` `0`** · `Text`/`Fill`/`Color`/`HoverFill`/`PressedFill`/`OnSelect` ver abajo |
| `icoBorrarFilaCap` | Icono clásico `Icon.Trash` | X `1020` · Y `12` · Width `16` · Height `16` · `Color` `ThemeInk3` · **`HoverColor` `ThemeDanger`** · **`TabIndex` `-1`** — acción **destructiva**: queda FUERA del recorrido de Tab a propósito (receta `01` §7.11.B) · `OnSelect` ver §7.3 |
| `shpSepFilaCap` | Rectángulo clásico | X `0` · Y `galPlanillasCap.TemplateSize - 1` · Width `galPlanillasCap.Width` · Height `1` · `Fill` `ThemeLineSoft` |

**`BorderColor` de `cmbEmpresaCap` y de los 5 inputs** (idéntico en los 6 —
`.rowBad .cin{border-color:#E4C68C}`):

```powerfx
If(
    !IsEmpty(Filter(colProblemas, LocalId = ThisItem.LocalId)) ||
    !IsEmpty(Filter(colFallidas,  LocalId = ThisItem.LocalId)),
    ThemeWarnLine,
    ThemeLine
)
```

**Combo de empresa — SERVER-SIDE (L-23):**

```powerfx
// cmbEmpresaCap.Items — 🧨 DIRECTO al datasource, sin colección de por medio.
// Regla D-10 (01 §2.3): CH-Tbl-ClientBranches tiene ~50.000 sucursales activas;
// un ClearCollect truncaba en DataRowLimit EN SILENCIO y el operador no
// encontraba su sucursal. Con IsSearchable, el combo consulta al desplegarse y
// al tipear — es la ÚNICA excepción declarada a D-4 (§2, nota de Q1).
// El literal es el del option set GLOBAL: 'CH-Ch-RecordStatus'.Active (01 §2.4).
Filter('CH-Tbl-ClientBranches', RecordStatus = 'CH-Ch-RecordStatus'.Active)
```

```powerfx
// cmbEmpresaCap.DefaultSelectedItems — 🧨 NO se filtra el datasource acá.
// Table(<registro>) envuelve el registro que YA vive en la colección: el combo
// espera una TABLA de seleccionados. Un Filter sobre 'CH-Tbl-ClientBranches' en
// esta propiedad sería una consulta POR FILA RENDERIZADA — el N+1 que D-4 mata.
If(
    IsBlank(ThisItem.Empresa.'CH-Tbl-ClientBranch'),
    Blank(),
    Table(ThisItem.Empresa)
)
```

```powerfx
// cmbEmpresaCap.OnChange — 🧨 SOLO la bandera. Ni un Patch. (receta 01 §7.11.A)
// Un Patch a colPlanillas acá muta el Items de la galería, la galería se
// re-renderiza y el combo PIERDE EL FOCO a mitad de la selección. El valor lo
// baja el VOLCADO, que corre en el OnSelect de los 6 botones (§5.6).
Set(gvarCambiosSinGuardar, true)
```

> **`cmbEmpresaCap.Selected` ya es el registro que necesita el guardado**: sale
> derecho del datasource, sin `AddColumns` que le cambie la forma, con su lookup
> `Client` navegable. Por eso el volcado escribe `Empresa: Fila.cmbEmpresaCap.Selected`
> sin ningún `LookUp` intermedio, y `RemitentClient` **nunca** sale en blanco.
>
> **`Template ListItemTemplate.Double` + los dos `DisplayFields`** pintan la sucursal
> arriba (`sanic_name`) y la empresa abajo (`sanic_clientnamemirror`, espejo **B0-9**
> de `01` §2.2). Los dos están en `SearchFields`, así que **`StartsWith` delega por
> los dos** y el operador puede tipear el nombre de la empresa o el de la sucursal.
> Los nombres van con el **nombre lógico** (`sanic_…`) porque `DisplayFields` y
> `SearchFields` son listas de strings, no de columnas: el display name no sirve.
> Si tu Studio los nombra distinto, el suyo manda. Muere la columna `EtiquetaCap`
> y con ella el `AddColumns` y el dot-walk `Client.Name`.

**Inputs de la fila** — los seis controles editables llevan **exactamente lo mismo**:

```powerfx
// OnChange de inpPlanillaCap, inpChequeNIOCap, inpChequeUSDCap,
//            inpEfectivoNIOCap, inpEfectivoUSDCap y cmbEmpresaCap
// 🧨 Los SEIS. Sin Patch, sin excepción (receta 01 §7.11.A).
Set(gvarCambiosSinGuardar, true)
```

> 🧨 **Por qué el `OnChange` NO patchea (hallazgo del QA humano de la 1.31)**: cada
> `Patch(colPlanillas, …)` en un `OnChange` muta el `Items` de `galPlanillasCap`, la
> galería **se re-renderiza** y el control que tenía el foco deja de ser el mismo
> control. El operador tipeaba tres dígitos de un monto y el cursor se le iba. **No
> lo arregla `DelayOutput`**: `DelayOutput` retrasa el `OnChange`, no evita el
> re-render. El valor se baja a la colección con el **VOLCADO** (§5.6), que corre en
> el `OnSelect` de los botones — o sea, cuando el foco ya no importa.
>
> - **`Coalesce(Value(…), 0)` y no `Value(…)` pelado** — vive ahora en el volcado:
>   con el input vacío `Value("")` devuelve *blank*, y un blank metido en
>   `fxProblemas` hace que `ChequeNIO + ChequeUSD + …` sea blank en vez de `0`, así
>   que la regla "todos los montos en 0" dejaría de disparar. Es un `Coalesce` que
>   **compra una regla de validación**.
> - **`DelayOutput = true` sigue en los 5 inputs**: ya no hay `Patch` que retrasar,
>   pero sí evita que `gvarCambiosSinGuardar` se reescriba en cada pulsación.
> - **`TabIndex 0` en los 6** (receta `01` §7.11.B): un `TabIndex` positivo dentro de
>   un template hace que **todas las filas compartan el mismo número** y el Tab
>   recorra la misma columna de todas las filas antes de bajar — filas entrelazadas.
>   Con `0`, el orden es el geométrico y el Tab recorre la fila de izquierda a
>   derecha, que es lo que espera el operador.

#### 5.4.a 🧨 El VOLCADO

(receta `01` §7.11.A). Es la **primera** instrucción del
`OnSelect` de estos **seis** controles, sin excepción: `btnNuevaPlanillaCap`,
`btnValidarCap`, `btnGuardarCap`, `btnCerrarCap`, `btnChipTulasCap` y
`icoBorrarFilaCap`.

```powerfx
// >>> VOLCADO <<<  — se pega TAL CUAL, como primera línea, en los 6 OnSelect.
ForAll(
    galPlanillasCap.AllItems As Fila,
    Patch(
        colPlanillas,
        LookUp(colPlanillas, LocalId = Fila.LocalId),
        {
            Empresa:     Fila.cmbEmpresaCap.Selected,
            Planilla:    Trim(Fila.inpPlanillaCap.Text),
            ChequeNIO:   Coalesce(Value(Fila.inpChequeNIOCap.Text), 0),
            ChequeUSD:   Coalesce(Value(Fila.inpChequeUSDCap.Text), 0),
            EfectivoNIO: Coalesce(Value(Fila.inpEfectivoNIOCap.Text), 0),
            EfectivoUSD: Coalesce(Value(Fila.inpEfectivoUSDCap.Text), 0)
        }
    )
);
```

> **`As Fila` no es opcional**: sin el alias, `Fila.inpPlanillaCap` no resuelve y el
> `LocalId` del `LookUp` se ambigua con el de la colección. **Es un `ForAll` que
> proyecta y patchea una COLECCIÓN LOCAL**: no hay `Set()` adentro (`01` §3.2) ni red
> que tocar. Si agregás un botón que lea `colPlanillas`, **lleva volcado**; si no,
> opera sobre lo que había antes de la última tecla.

**Chip de tulas** (receta 7.4 / 7.1 — botón clásico con `Radius`):

```powerfx
// btnChipTulasCap.Text
Text(CountRows(Filter(colTulas, PlanillaLocalId = ThisItem.LocalId)))

// btnChipTulasCap.Fill  (y PressedFill con la MISMA fórmula: PressedFill = Fill)
If(CountRows(Filter(colTulas, PlanillaLocalId = ThisItem.LocalId)) = 0, ThemeWarnSoft, ThemeInfoSoft)

// btnChipTulasCap.HoverFill — el chip SÍ es clicable, así que el hover tiene que
// contestar (el .chip:hover del mockup oscurece un punto el azul). En el chip ámbar
// (cero tulas) el hover NO cambia: el ámbar ya es una llamada de atención.
If(CountRows(Filter(colTulas, PlanillaLocalId = ThisItem.LocalId)) = 0, ThemeWarnSoft, ThemeInfoSoft2)

// btnChipTulasCap.Color
If(CountRows(Filter(colTulas, PlanillaLocalId = ThisItem.LocalId)) = 0, ThemeWarn, ThemeInfo)

// btnChipTulasCap.OnSelect — abre el panel de tulas de ESTA fila (§7.2)
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<
//
// 🧨 La planilla y la empresa se leen de los CONTROLES HERMANOS del template
//    (inpPlanillaCap / cmbEmpresaCap), NO de ThisItem: ThisItem viene de la
//    colección, y la colección recién se pone al día en la línea de arriba. Si el
//    operador tipea "00871", toca el chip y el panel dice "Planilla 00870", eso
//    es STALENESS: el título miente sobre la fila que está editando.
UpdateContext({
    ctxPlanillaTulasCap:    ThisItem.LocalId,
    ctxPlanillaTulasTxtCap: Trim(inpPlanillaCap.Text),
    ctxPlanillaTulasEmpCap: Coalesce(
                                cmbEmpresaCap.Selected.sanic_clientnamemirror,
                                "Empresa sin seleccionar"
                            ),
    ctxScanDupCap:          ""
});
ClearCollect(colTulasEdit, Filter(colTulas, PlanillaLocalId = ThisItem.LocalId));
Reset(galTulasEditCap);
UpdateContext({ ctxDlgTulasCap: true });
SetFocus(inpTulaScanCap)
```

> Los `CountRows(Filter(colTulas, …))` de este chip están **dentro del template** —
> pero sobre una **colección local**. La regla `D-4` prohíbe `Filter`/`LookUp`/`Search`
> sobre un **datasource** en el `Items` de un control del template; acá no hay red que
> tocar (`01` §2.3, nota final). El `ClearCollect` del `OnSelect` corre **una vez por
> clic**, no por render.
>
> **Chip sin glifo**: el mockup dibuja `N ⛁`. Open Sans no tiene ese glifo y `01` §6.1
> prohíbe glifos en el `Text`; el catálogo cerrado de iconos no trae uno de tula. El
> chip muestra **solo el número**. Divergencia **L-6**.
>
> **`SetFocus(inpTulaScanCap)` es la última línea**, después de abrir el overlay: el
> operador viene del escáner de mano y tiene que poder disparar el gatillo sin tocar
> el mouse. `SetFocus` apunta a un control **de pantalla** (el escáner vive en la
> card del diálogo, no en un template), que es la única forma soportada — a un
> control de template no se le puede dar foco porque hay N réplicas
> (`01` §7.11.B).

### 5.5 Pie y estado vacío

| Control | Tipo | Propiedades |
|---|---|---|
| `lblVacioGrillaCap` | Etiqueta clásica | X `shpCardGrillaCap.X` · Y `galPlanillasCap.Y + (galPlanillasCap.Height - Self.Height) / 2` (centrado vertical en la banda de la galería vacía) · Width `shpCardGrillaCap.Width` · Height `40` · **ESTILO-VACIO** · `Wrap` `true` · **`Visible` `IsEmpty(colPlanillas) && !IsBlank(gvarLoteActivo)`** · `Text` `"Este lote todavía no tiene planillas. Tocá «Nueva planilla» para empezar."` |
| `shpLineaAddCap` | Rectángulo clásico | X `shpCardGrillaCap.X` · Y `galPlanillasCap.Y + galPlanillasCap.Height` · Width `shpCardGrillaCap.Width` · Height `1` · `Fill` `ThemeLineSoft` |
| `btnNuevaPlanillaCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `shpCardGrillaCap.X + 14` (= `32`: el mismo padding de 14 de la card que usan los encabezados) · Y `shpLineaAddCap.Y + 10` · Width `160` · `Text` `"Nueva planilla"` · `Align` `Align.Left` · `PaddingLeft` `34` · `DisplayMode` `If(!IsBlank(gvarLoteActivo), DisplayMode.Edit, DisplayMode.Disabled)` · `OnSelect` ver abajo |
| `icoNuevaPlanillaCap` | Icono clásico `Icon.Add` | X `btnNuevaPlanillaCap.X + 14` · Y `btnNuevaPlanillaCap.Y + 9` · Width `14` · Height `14` · `Color` `ThemeInk2` · `DisplayMode` `btnNuevaPlanillaCap.DisplayMode` · **`OnSelect` `Select(btnNuevaPlanillaCap)`** |

```powerfx
// btnNuevaPlanillaCap.OnSelect
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<
//     Sin él, agregar una fila re-renderiza la galería y lo tecleado en la fila
//     que tenía el foco se pierde: nunca llegó a la colección.
//
// El registro nace con EXACTAMENTE el esquema sembrado en App.OnStart (01 §3.2):
// GUID y Empresa van con Defaults(...) para nacer EN BLANCO PERO TIPADOS.
Collect(
    colPlanillas,
    {
        LocalId:     Text(GUID()),
        GUID:        Defaults('CH-Tbl-Receptions').'CH-Tbl-Receptions',
        Empresa:     Defaults('CH-Tbl-ClientBranches'),
        Planilla:    "",
        ChequeNIO:   0,
        ChequeUSD:   0,
        EfectivoNIO: 0,
        EfectivoUSD: 0
    }
);
Set(gvarCambiosSinGuardar, true)
```

---

## 6. Rail derecho (X `1100`, ancho `248`)

### 6.1 Las 4 tarjetas de totales

Todas idénticas salvo `Y`, `Text` de la etiqueta y fórmula del valor. `Y` de cada
tarjeta se **encadena** a la anterior (`01` §0.9): `54` de alto + `9` de `gap`.

| Tarjeta | Superficie (Botón clásico, receta 7.1) | Y de la superficie |
|---|---|---|
| Cheque C$ | `shpTotChequeNIOCap` | `112` |
| Cheque US$ | `shpTotChequeUSDCap` | `shpTotChequeNIOCap.Y + shpTotChequeNIOCap.Height + 9` |
| Efectivo C$ | `shpTotEfectivoNIOCap` | `shpTotChequeUSDCap.Y + shpTotChequeUSDCap.Height + 9` |
| Efectivo US$ | `shpTotEfectivoUSDCap` | `shpTotEfectivoNIOCap.Y + shpTotEfectivoNIOCap.Height + 9` |

**Propiedades comunes de las 4 superficies**: X `1100` · Width `248` · Height `54` ·
`Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` ·
`BorderThickness` `1` · `Radius` (las 4) **`11`** (receta 7.1, "cards chicas del rail") ·
`DisplayMode` `DisplayMode.View`.

**Propiedades comunes de las 8 etiquetas** — X `1114` (`= superficie.X + 14`), Width `220`:

| Etiqueta | Y | Height | Estilo | `Text` |
|---|---|---|---|---|
| `lblTotChequeNIOLabCap` | `shpTotChequeNIOCap.Y + 9` | `13` | **ESTILO-KPI-LAB** | `"CHEQUE C$"` |
| `lblTotChequeNIOCap` | `lblTotChequeNIOLabCap.Y + lblTotChequeNIOLabCap.Height + 1` | `22` | **ESTILO-KPI-VAL** | `Text(TotalChequeNIO, FmtMoney)` |
| `lblTotChequeUSDLabCap` | `shpTotChequeUSDCap.Y + 9` | `13` | ESTILO-KPI-LAB | `"CHEQUE US$"` |
| `lblTotChequeUSDCap` | `lblTotChequeUSDLabCap.Y + lblTotChequeUSDLabCap.Height + 1` | `22` | ESTILO-KPI-VAL | `Text(TotalChequeUSD, FmtMoney)` |
| `lblTotEfectivoNIOLabCap` | `shpTotEfectivoNIOCap.Y + 9` | `13` | ESTILO-KPI-LAB | `"EFECTIVO C$"` |
| `lblTotEfectivoNIOCap` | `lblTotEfectivoNIOLabCap.Y + lblTotEfectivoNIOLabCap.Height + 1` | `22` | ESTILO-KPI-VAL | `Text(TotalEfectivoNIO, FmtMoney)` |
| `lblTotEfectivoUSDLabCap` | `shpTotEfectivoUSDCap.Y + 9` | `13` | ESTILO-KPI-LAB | `"EFECTIVO US$"` |
| `lblTotEfectivoUSDCap` | `lblTotEfectivoUSDLabCap.Y + lblTotEfectivoUSDLabCap.Height + 1` | `22` | ESTILO-KPI-VAL | `Text(TotalEfectivoUSD, FmtMoney)` |

> **Sin prefijo de moneda en el valor** (igual que el mockup): la etiqueta de la tarjeta
> ya dice `CHEQUE C$` / `CHEQUE US$`. Repetir el símbolo abajo (`C$ 1,250.00`) lo dice
> dos veces y le come ancho a un rail de 248. `SimboloNIO`/`SimboloUSD` de `01` §3.1
> **no se usan acá**.

> `TotalChequeNIO`, `TotalChequeUSD`, `TotalEfectivoNIO`, `TotalEfectivoUSD` y
> `FmtMoney` son fórmulas con nombre de `01` §3.1 y suman
> sobre **`colPlanillas` local** (regla `D-8`: nunca `Sum()` sobre un datasource).
> Los 4 totales **se actualizan solos** al tipear: son reactivos, no hay ningún
> `updTot()` que llamar como en el mockup.

### 6.2 Panel de validación

| Control | Tipo | Propiedades |
|---|---|---|
| `shpPanelValCap` | Botón clásico (receta 7.1) | X `1100` · Y `shpTotEfectivoUSDCap.Y + shpTotEfectivoUSDCap.Height + 9` (= `364`) · Width `248` · Height `Parent.Height - 16 - Self.Y` (= `388`) · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `11` · `DisplayMode` `DisplayMode.View` |
| `lblValTituloCap` | Etiqueta clásica | X `1114` · Y `shpPanelValCap.Y + 11` · Width `220` · Height `16` — `ESTILO-SECCION` ahora es Size `8.25`, y con `14` la tilde de *VALIDACIÓN* se recorta · **ESTILO-SECCION** · `Text` `"VALIDACIÓN"` |
| `shpDotOkValCap` | Botón clásico (receta 7.1) | X `1114` · Y `lblValTituloCap.Y + lblValTituloCap.Height + 12` · Width `7` · Height `7` · `Radius` (las 4) `4` · `Fill`/`HoverFill`/`PressedFill` `ThemeOk` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · **`Visible` `IsEmpty(colProblemas)`** |
| `lblValVacioCap` | Etiqueta clásica | X `1128` · Y `lblValTituloCap.Y + lblValTituloCap.Height + 6` · Width `206` · Height `32` · **ESTILO-VALITEM** · `Wrap` `true` · **`Visible` `IsEmpty(colProblemas)`** · `Text` `"Sin problemas detectados. Validá para confirmar."` |
| `galProblemasCap` | Galería vertical en blanco | X `1114` · Y `lblValTituloCap.Y + lblValTituloCap.Height + 6` (= `397`) · Width `220` · Height `shpPanelValCap.Y + shpPanelValCap.Height - 12 - Self.Y` (= `343`) · `TemplateSize` `40` · `TemplatePadding` `0` · `ShowScrollbar` `true` · `OnSelect` `false` · `Items` `colProblemas` · **`Visible` `!IsEmpty(colProblemas)`** |

**Template de `galProblemasCap`** (X de template = X de encabezado − `1114`):

| Control | Tipo | Propiedades |
|---|---|---|
| `shpDotProblemaCap` | Botón clásico (receta 7.1) | X `0` · Y `10` · Width `7` · Height `7` · `Radius` (las 4) `4` · `Fill`/`HoverFill`/`PressedFill` `ThemeDotWarn` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `lblProblemaCap` | Etiqueta clásica | X `14` · Y `4` · Width `galProblemasCap.Width - 20` (= `200`) · Height `32` · **ESTILO-VALITEM** · `Wrap` `true` · `Text` ver abajo — el ancho deja libre la **barra de scroll** de la galería (`ShowScrollbar true`) |

```powerfx
// lblProblemaCap.Text
If(
    IsBlank(Trim(ThisItem.Planilla)),
    "Planilla sin número",
    "Planilla " & Trim(ThisItem.Planilla)
) & " — " & ThisItem.Regla
```

> **Divergencia L-7**: el mockup rotula cada problema como *"Fila 4 — …"*. Acá se rotula
> por **N° de planilla**. Motivo técnico, no estético: `colProblemas` guarda `LocalId` y
> `Planilla` (`01` §3.1), **no** un índice; y el índice de fila de una colección
> editable **cambia** cada vez que se agrega o borra una planilla, así que un mensaje
> "Fila 4" apuntaría a otra fila apenas el usuario corrija algo. El N° de planilla es
> además lo que el usuario **lee en la grilla** para encontrarla.

---

## 7. Overlays A · B · C

Los **6 overlays** siguen la receta `01` §7.2 y se insertan **últimos**, en el orden
A→F: el z-order de un overlay es su seguro de vida. Cada uno tiene los mismos **cuatro**
controles de armazón (**y dos overlays no llevan `icoDlgX…Cap`: A** — *Nuevo lote*, gate
de estado — **y D** — resultado del guardado —, así que esos dos tienen **tres**), con
estas propiedades **fijas por receta** (no se repiten en las tablas de abajo):

| Control | Propiedades (receta 7.2) |
|---|---|
| `cntDlg<X>Cap` | Contenedor de diseño manual · X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Fill` `ThemeOverlay` · `Radius` (las 4) `0` · `BorderThickness` `0` · `Visible` = su `ctxDlg…` |
| `btnDlgBloqueo<X>Cap` | Botón clásico, **al FONDO del contenedor** · X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** |
| `cntDlgCard<X>Cap` | Contenedor de diseño manual · X `(Parent.Width - Self.Width) / 2` · Y `(Parent.Height - Self.Height) / 2` · `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DropShadow` `DropShadow.Regular` |
| `icoDlgX<X>Cap` | Icono clásico `Icon.Cancel` · X `Parent.Width - 34` · Y `16` · Width `18` · Height `18` · `Color` `ThemeInk3` · **`HoverColor` `ThemeInk`** (el Icono clásico **sí** lo expone, `01` §7.2) · **`OnSelect` = EXACTAMENTE el mismo del botón Cancelar** |

> 🧨 **`Parent` de todo lo que sigue a la card ES LA CARD.** En el árbol de §0, el
> overlay tiene **exactamente dos hijos**: `btnDlgBloqueo<X>Cap` (primero, al FONDO) y
> `cntDlgCard<X>Cap`. **Todo el contenido cuelga de la card**, no del overlay — por eso
> las coordenadas de las tablas de abajo dicen X `20`, Width `Parent.Width - 40` y
> Y `Parent.Height - 50` y **cierran**: `Parent.Width` es `380`/`420`/`520`, no `1366`.
> Si alguno de esos controles queda colgado del overlay, el botón se va a `Y 718` y la
> etiqueta mide 1326 de ancho. Es el error de construcción más caro de encontrar.

**Regla no negociable (7.2, mata N4): la ✕ CANCELA.** En los **4** overlays que la
tienen (B, C, E, F), `icoDlgX…Cap.OnSelect` es carácter por carácter el `OnSelect` de su
`btnDlgCancel…Cap`. Ninguna ✕ commitea nada.

**Los dos overlays SIN ✕** son los que no tienen nada que cancelar: **A** (*Nuevo lote*)
es un gate de estado (`L-14`), y **D** (resultado del guardado, §8.3) es un informe de
algo que ya pasó — se cierra con su botón, no se descarta.

### 7.1 Overlay A — Nuevo lote (`cntDlgNuevoLoteCap`)

**Es un *gate de estado*, no un diálogo cancelable**: se muestra mientras no haya lote
activo y **no tiene ✕ ni Cancelar** — no hay nada a qué volver. Por eso también
**arranca debajo del header**: el segmentado `[Captura|Lotes]` y el chip de usuario
siguen accesibles, igual que en `renderNuevoLote()` del mockup.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgNuevoLoteCap` | Contenedor de diseño manual | receta 7.2 **con tres overrides**: X `0` · Y `shpHeaderCap.Height + 1` · Width `Parent.Width` · Height `Parent.Height - Self.Y` · **`Fill` `ThemeBg`** — opaco, **NO** `ThemeOverlay`: no es un modal sobre contenido, es el estado "no hay lote"; el mockup renderiza la pantalla limpia detrás · **`Visible` `IsBlank(gvarLoteActivo)`** |
| `btnDlgBloqueoNuevoLoteCap` | Botón clásico | receta 7.2 |
| `cntDlgCardNuevoLoteCap` | Contenedor de diseño manual | receta 7.2 · Width `380` · Height `224` |
| `lblDlgTitNuevoLoteCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 40` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Nuevo lote de recepción"` |
| `lblDlgTxtNuevoLoteCap` | Etiqueta clásica | X `20` · Y `lblDlgTitNuevoLoteCap.Y + lblDlgTitNuevoLoteCap.Height + 4` · Width `Parent.Width - 40` · Height `34` · **ESTILO-DLG-TEXTO** · `Text` `"El lote se crea en estado En progreso. El número (RB-…) lo asigna Dataverse."` |
| `lblCampoTransCap` | Etiqueta clásica | X `20` · Y `lblDlgTxtNuevoLoteCap.Y + lblDlgTxtNuevoLoteCap.Height + 10` · Width `200` · Height `16` · **ESTILO-CAMPO-LAB** · `Text` `"Transportadora"` |
| `cmbTransportadoraCap` | Cuadro combinado clásico | X `20` · Y `lblCampoTransCap.Y + lblCampoTransCap.Height + 4` · Width `Parent.Width - 40` · Height `32` · `Items` **`colTransportadoras`** · `DisplayFields` `["Name"]` · `SearchFields` `["Name"]` · `IsSearchable` `true` · `SelectMultiple` `false` · `InputTextPlaceholder` `"Elegí la transportadora…"` |
| `btnCrearLoteCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `108` · `Text` `"Crear lote"` · `DisplayMode` `If(!IsBlank(cmbTransportadoraCap.Selected), DisplayMode.Edit, DisplayMode.Disabled)` (7.2 regla 4) · `OnSelect` ver abajo |
| `btnIrALotesCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnCrearLoteCap.X - 8 - Self.Width` · Y `btnCrearLoteCap.Y` · Width `96` · `Text` `"Ir a Lotes"` · `OnSelect` `Navigate(scrLotes)` — **sin guarda**: sin lote activo no hay nada que perder |

```powerfx
// btnCrearLoteCap.OnSelect
// Q5 de §2. 'Name' es AUTONUMBER RB-{SEQNUM:5}: NUNCA se patchea (01 §2.4).
// El lote nace 'En progreso' (159460000) — estado explícito, no heredado.
IfError(
    Set(
        gvarLoteActivo,
        Patch(
            'CH-Tbl-ReceptionBatches',
            Defaults('CH-Tbl-ReceptionBatches'),
            {
                ReceptionStatus: 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso',
                Transportadora:  cmbTransportadoraCap.Selected.Name
            }
        )
    ),

    // --- rama de ERROR: la variable NO puede quedar con un valor de error pegado ---
    Set(gvarLoteActivo, Blank());
    Notify("No se pudo crear el lote: " & FirstError.Message, NotificationType.Error),

    // --- rama de ÉXITO (3er argumento de IfError) ---
    Clear(colPlanillas);   Clear(colTulas);
    Clear(colPlanillasDV); Clear(colTulasDV);
    Clear(colProblemas);   Clear(colFallidas);
    Clear(colTulasBorradasCap); Clear(colEstadoGuardadoCap);
    Set(gvarCambiosSinGuardar, false);
    Reset(cmbTransportadoraCap);
    Notify("Lote " & gvarLoteActivo.Name & " creado en estado En progreso.", NotificationType.Success)
)
```

> `Transportadora` es **texto** `nvarchar(100)` requerido (`01` §2.4), no un lookup: se
> escribe el `Name` elegido. Y `Set()` está dentro de un `IfError`, **no** dentro de un
> `ForAll` — la prohibición de `01` §3.2 es sobre `ForAll`.

### 7.2 Overlay B — Panel de tulas **scan-first** (`cntDlgTulasCap`)

Edita **una copia** (`colTulasEdit`). **Cancelar y la ✕ descartan; solo *Aplicar*
vuelca a `colTulas`.** Lo abre `btnChipTulasCap` (§5.4), que hace el `ClearCollect` de
la copia, el `Reset` de la galería (**Reset al ABRIR**, 7.2 regla 2) y el
`SetFocus(inpTulaScanCap)`.

> 🧨 **REDISEÑO del QA humano (16-ago): el panel es SCAN-FIRST.** El flujo real de la
> esclusa es *gatillo · gatillo · gatillo*: el operador tiene un escáner de mano y
> **no toca el mouse**. El panel original —*Agregar tula* crea una fila vacía, el
> operador va al input de la fila y tipea— exigía **un clic por tula** y, peor, cada
> fila nueva re-renderizaba la galería y le robaba el foco al input recién creado.
>
> | Muere | Nace |
> |---|---|
> | `btnAgregarTulaCap` + `icoAgregarTulaCap` | **`inpTulaScanCap`**: UN input fijo arriba de la lista. Escaneás → Enter → la tula entra y el input se limpia y **se queda con el foco** |
> | `inpTulaEditCap` editable por fila | `inpTulaEditCap` en **`DisplayMode.View`**: la lista es el **registro de lo escaneado**, no un formulario |
> | Corregir un número tipeando encima | **Corregir = borrar y volver a escanear.** Es más rápido con un escáner en la mano que posicionar el cursor en una fila |
>
> Y el dup-check se movió al **momento del escaneo**, que es cuando el operador
> todavía tiene la tula en la mano: avisar al *Aplicar* que hay un duplicado, con 40
> tulas ya escaneadas, es avisar tarde.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgTulasCap` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgTulasCap`** |
| `btnDlgBloqueoTulasCap` | Botón clásico | receta 7.2 |
| `cntDlgCardTulasCap` | Contenedor de diseño manual | receta 7.2 · Width `520` (variante ancha) · Height `460` |
| `lblDlgTitTulasCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 66` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Tulas — Planilla " & If(IsBlank(Trim(ctxPlanillaTulasTxtCap)), "s/n", Trim(ctxPlanillaTulasTxtCap))` |
| `lblDlgTxtTulasCap` | Etiqueta clásica | X `20` · Y `lblDlgTitTulasCap.Y + lblDlgTitTulasCap.Height + 4` · Width `Parent.Width - 66` · Height `18` · **ESTILO-DLG-TEXTO** · `Wrap` `false` · `Text` `ctxPlanillaTulasEmpCap` |
| `icoDlgXTulasCap` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelTulasCap` |
| `inpTulaScanCap` | **Entrada de texto clásica** | X `20` · Y `lblDlgTxtTulasCap.Y + lblDlgTxtTulasCap.Height + 12` (= `72`) · Width `Parent.Width - 40` · **Height `32`** (fuera de galería, `01` §6.1) · `HintText` `"Escaneá el código o escribí el N° y Enter"` · **`DelayOutput` `false`** — 🧨 el escáner "teclea" el código y manda Enter en milisegundos: con `DelayOutput true` el `OnChange` llega tarde y se comen escaneos · `Clear` `true` · `TabIndex` `0` · `BorderColor` `If(!IsBlank(ctxScanDupCap), ThemeWarnLine, ThemeLine)` · `OnChange` ver abajo |
| `lblErrScanCap` | Etiqueta clásica | X `20` · Y `inpTulaScanCap.Y + inpTulaScanCap.Height + 2` · Width `Parent.Width - 40` · Height `18` · **ESTILO-ERROR** · **`Visible` `!IsBlank(ctxScanDupCap)`** · `Text` `ctxScanDupCap` |
| `galTulasEditCap` | Galería vertical en blanco | X `20` · Y `lblErrScanCap.Y + lblErrScanCap.Height + 6` (= `128`) · Width `Parent.Width - 40` · Height `212` (= `btnDlgOkTulasCap.Y - 18 - Self.Y`) · `TemplateSize` **`32`** · `TemplatePadding` `0` · `ShowScrollbar` `true` · `OnSelect` `false` · `Items` **`colTulasEdit`** |
| `lblVacioTulasCap` | Etiqueta clásica | X `20` · Y `galTulasEditCap.Y` (arranca **donde arrancaría la primera fila** — por eso se encadena y no lleva literal) · Width `Parent.Width - 40` · Height `24` · `Size` `9` · `Color` `ThemeInk3` · `Align` `Align.Left` · **`Visible` `IsEmpty(colTulasEdit)`** · `Text` `"Sin tulas todavía. Escaneá la primera."` |
| `btnDlgCancelTulasCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkTulasCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Cancelar"` · `OnSelect` ver abajo |
| `btnDlgOkTulasCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Aplicar"` · `DisplayMode` ver abajo · `OnSelect` ver abajo |

> **`lblErrScanCap` ocupa su alto SIEMPRE** (la galería cuelga de `lblErrScanCap.Y +
> Height`, no de su `Visible`): si la lista subiera y bajara 18 px con cada
> duplicado, la fila que el operador está mirando se movería sola. Es la misma
> decisión que **L-9**, un nivel más arriba.

> **`lblVacioTulasCap` NO usa `ESTILO-VACIO`** y por eso lleva sus tres propiedades
> explícitas. `ESTILO-VACIO` es `Align.Center` + Size `9.38`: sirve para el vacío de una
> card ancha (`lblVacioGrillaCap`), no para una línea que arranca en el borde izquierdo
> de la lista de tulas, alineada con los inputs que va a reemplazar. Acá: Size `9`,
> `ThemeInk3`, `Align.Left`.

**`inpTulaScanCap.OnChange` — el corazón del panel:**

```powerfx
// Corre en cada Enter del escáner (o del teclado). Dos capas de dup-check:
//   INTRA-planilla  -> contra colTulasEdit (lo que se está escaneando ahora)
//   CROSS-planilla  -> contra colTulas, EXCLUYENDO esta planilla (el resto del lote)
// Las dos son sobre COLECCIONES LOCALES: cero red, cero delegación que romper.
If(
    IsBlank(Trim(inpTulaScanCap.Text)),

    // Enter en vacío: no es un error, no es una tula. Se ignora en silencio.
    Reset(inpTulaScanCap);
    SetFocus(inpTulaScanCap),

    If(
        // ---- capa 1: ya está en ESTA planilla ----
        !IsEmpty(
            Filter(colTulasEdit As X, Trim(X.Numero) = Trim(inpTulaScanCap.Text))
        ),
        UpdateContext({
            ctxScanDupCap:
                "La tula " & Trim(inpTulaScanCap.Text) & " ya está en esta planilla."
        });
        Reset(inpTulaScanCap);
        SetFocus(inpTulaScanCap),

        If(
            // ---- capa 2: está en OTRA planilla del mismo lote ----
            !IsEmpty(
                Filter(
                    colTulas As T,
                    Trim(T.Numero) = Trim(inpTulaScanCap.Text) &&
                    T.PlanillaLocalId <> ctxPlanillaTulasCap
                )
            ),
            UpdateContext({
                ctxScanDupCap:
                    "La tula " & Trim(inpTulaScanCap.Text) & " ya está en la planilla " &
                    Coalesce(
                        LookUp(
                            colPlanillas,
                            LocalId = LookUp(
                                          colTulas As T,
                                          Trim(T.Numero) = Trim(inpTulaScanCap.Text) &&
                                          T.PlanillaLocalId <> ctxPlanillaTulasCap
                                      ).PlanillaLocalId
                        ).Planilla,
                        "s/n"
                    ) & "."
            });
            Reset(inpTulaScanCap);
            SetFocus(inpTulaScanCap),

            // ---- entra ----
            Collect(
                colTulasEdit,
                {
                    LocalId:         Text(GUID()),
                    PlanillaLocalId: ctxPlanillaTulasCap,
                    Numero:          Trim(inpTulaScanCap.Text),
                    // GUID en blanco pero TIPADO (mismo criterio que App.OnStart, 01 §3.2)
                    GUID:            Defaults('CH-Tbl-Bags').'CH-Tbl-Bags'
                }
            );
            UpdateContext({ ctxScanDupCap: "" });
            Reset(inpTulaScanCap);
            SetFocus(inpTulaScanCap)
        )
    )
)
```

> **`Reset(Self)` + `SetFocus(Self)` en las CUATRO ramas**, incluida la de error: el
> operador no puede quedarse con el input sucio ni sin foco después de un escaneo
> fallido — el siguiente gatillo tiene que entrar igual. (Si tu Studio no acepta
> `Self` en `Reset`/`SetFocus` desde el propio `OnChange`, escribí el nombre:
> `Reset(inpTulaScanCap)` / `SetFocus(inpTulaScanCap)` — es lo que hace el bloque de
> arriba.)
>
> **El mensaje cross-planilla dice el NÚMERO de la otra planilla**, no "está
> duplicada": el operador tiene que saber **dónde** buscarla. Se resuelve con dos
> `LookUp` locales encadenados (tula → `PlanillaLocalId` → `colPlanillas.Planilla`) y
> un `Coalesce` a `"s/n"` para la planilla que todavía no tiene número.
>
> **Los dos dup-checks son local-only y eso es a propósito**: el duplicado contra
> **otro lote** del entorno lo rechaza el `GuardReceptionPlugin` al guardar y el
> mensaje llega por `IfError` (misma limitación honesta que `01` §7.9).

**Template de `galTulasEditCap`** (`TemplateSize 32` — la fila ya no lleva hueco de
error reservado: el error vive **arriba**, en `lblErrScanCap`, junto al escáner):

| Control | Tipo | Propiedades |
|---|---|---|
| `inpTulaEditCap` | Entrada de texto clásica | X `0` · Y `2` · Width `galTulasEditCap.Width - 34` · Height `28` · **`DisplayMode` `DisplayMode.View`** — 🧨 **SOLO LECTURA**: la lista es el registro de lo escaneado · `Default` `ThisItem.Numero` · **`Fill` `ThemeSurface2`** (le dice al ojo que no se edita) · `TabIndex` `-1` · `BorderThickness` `If(<duplicada-en-el-lote>, 1, 0)` · `BorderColor` `ThemeDanger` · **sin `OnChange`** |
| `icoBorrarTulaEditCap` | Icono clásico `Icon.Trash` | X `galTulasEditCap.Width - 26` · Y `8` · Width `16` · Height `16` · `Color` `ThemeInk3` · **`HoverColor` `ThemeDanger`** · `TabIndex` `-1` (destructiva, fuera del Tab) · `OnSelect` `Remove(colTulasEdit, ThisItem)` |
| `lblErrTulaEditCap` | Etiqueta clásica | X `0` · Y `2` · Width `galTulasEditCap.Width - 34` · Height `28` · **ESTILO-ERROR** · `Align` `Align.Right` · `PaddingRight` `10` · `Visible` `<duplicada-en-el-lote>` · `Text` `"duplicada"` |

```powerfx
// <duplicada-en-el-lote> — la MISMA expresión en lblErrTulaEditCap.Visible y en
// inpTulaEditCap.BorderThickness. Mira TODO el lote, no solo esta planilla: una
// tula que ya vivía en colTulas de otra planilla y llegó acá por una edición
// anterior tiene que gritar igual.
// El alias As X desambigua: sin él, 'Numero' adentro del Filter se resuelve al
// scope interno y la comparación queda contra sí misma (siempre true).
!IsBlank(Trim(ThisItem.Numero)) &&
(
    CountRows(Filter(colTulasEdit As X, Trim(X.Numero) = Trim(ThisItem.Numero))) > 1
    ||
    !IsEmpty(
        Filter(
            colTulas As T,
            Trim(T.Numero) = Trim(ThisItem.Numero) &&
            T.PlanillaLocalId <> ctxPlanillaTulasCap
        )
    )
)
```

> **La corrección es BORRAR y RE-ESCANEAR**, no editar. Con `inpTulaEditCap` en
> `DisplayMode.View` no hay `OnChange`, no hay `Patch` a `colTulasEdit` por tecla y
> por lo tanto **no hay re-render que le robe el foco al escáner**. El operador
> saca la fila mala con la papelera y vuelve a pasar el gatillo: dos gestos, ninguno
> con el teclado.

```powerfx
// btnDlgCancelTulasCap.OnSelect  (y, literal, icoDlgXTulasCap.OnSelect)
// DESCARTA la copia: colTulas no se toca. Es el fix del bug N4.
Clear(colTulasEdit);
UpdateContext({ ctxDlgTulasCap: false, ctxScanDupCap: "" })
```

```powerfx
// btnDlgOkTulasCap.DisplayMode — no se aplica con duplicados adentro (7.2 regla 4).
// El escáner ya los bloquea al entrar; esto es el cinturón para las tulas que
// llegaron de Dataverse con la planilla (colTulasEdit se siembra desde colTulas).
If(
    CountRows(
        Filter(
            colTulasEdit As X,
            !IsBlank(Trim(X.Numero)) &&
            CountRows(Filter(colTulasEdit As Y, Trim(Y.Numero) = Trim(X.Numero))) > 1
        )
    ) = 0,
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

```powerfx
// btnDlgOkTulasCap.OnSelect — VOLCADO de la copia a colTulas
// 1) Las tulas que YA vivían en Dataverse y el usuario sacó del panel quedan
//    anotadas: el guardado (§8.2 paso 4) las borra de Dataverse con Remove
//    de registro explícito. Sin esto, quitarlas del panel las dejaba huérfanas.
Collect(
    colTulasBorradasCap,
    Filter(
        colTulas As T,
        T.PlanillaLocalId = ctxPlanillaTulasCap &&
        !IsBlank(T.GUID) &&
        IsEmpty(Filter(colTulasEdit As E, E.LocalId = T.LocalId))
    )
);

// 2) Reconstrucción de colTulas en un BUFFER. Nunca se muta una colección mientras
//    un Filter la está leyendo: eso deja filas fantasma de forma intermitente.
ClearCollect(colTulasNuevoCap, Filter(colTulas, PlanillaLocalId <> ctxPlanillaTulasCap));
Collect(colTulasNuevoCap, Filter(colTulasEdit, !IsBlank(Trim(Numero))));
ClearCollect(colTulas, colTulasNuevoCap);
Clear(colTulasNuevoCap);
Clear(colTulasEdit);

// 3) Si el panel de validación estaba poblado, se recalcula: la regla "sin tulas
//    asociadas" y la de "tulas duplicadas" acaban de cambiar de verdad.
If(!IsEmpty(colProblemas), ClearCollect(colProblemas, fxProblemas));

Set(gvarCambiosSinGuardar, true);
UpdateContext({ ctxDlgTulasCap: false, ctxScanDupCap: "" });
Notify("Tulas de la planilla actualizadas (solo en memoria — guardá el lote para persistir).", NotificationType.Information)
```

> **Divergencia L-9 — reformulada por el rediseño scan-first**: el mensaje de
> duplicado ya **no vive en la fila** (que ahora mide `32` y es de solo lectura),
> sino **arriba**, junto al escáner (`lblErrScanCap`), donde el operador está
> mirando cuando pasa el gatillo. El hueco de 18 px de ese mensaje **sí** está
> siempre reservado, por el mismo motivo de siempre: si la lista subiera y bajara,
> la fila que el operador mira se movería sola.

### 7.3 Overlay C — Confirmar borrado de planilla (`cntDlgBorrarFilaCap`)

Lo abre `icoBorrarFilaCap` del template (§5.4). **Todo borrado es destructivo ⇒ tiene
diálogo** (7.2 regla 3: la v1 no tenía ninguno).

```powerfx
// icoBorrarFilaCap.OnSelect — abre el diálogo capturando la fila POR LocalId
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<
//     El título del diálogo dice el N° de planilla: sin volcado diría el viejo.
UpdateContext({
    ctxFilaBorrarCap:      ThisItem.LocalId,
    ctxFilaBorrarTxtCap:   Trim(inpPlanillaCap.Text),   // control hermano, no ThisItem
    ctxFilaBorrarGuidCap:  ThisItem.GUID,
    ctxFilaBorrarTulasCap: CountRows(Filter(colTulas, PlanillaLocalId = ThisItem.LocalId)),
    ctxDlgBorrarFilaCap:   true
})
```

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgBorrarFilaCap` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgBorrarFilaCap`** |
| `btnDlgBloqueoBorrarFilaCap` | Botón clásico | receta 7.2 |
| `cntDlgCardBorrarFilaCap` | Contenedor de diseño manual | receta 7.2 · Width `380` · Height `196` |
| `lblDlgTitBorrarFilaCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 66` · Height `44` · **ESTILO-DLG-TITULO** · `Wrap` `true` · `Text` `"¿Eliminar la planilla " & If(IsBlank(Trim(ctxFilaBorrarTxtCap)), "sin número", Trim(ctxFilaBorrarTxtCap)) & "?"` |
| `lblDlgTxtBorrarFilaCap` | Etiqueta clásica | X `20` · Y `lblDlgTitBorrarFilaCap.Y + lblDlgTitBorrarFilaCap.Height + 4` · Width `Parent.Width - 40` · Height `56` · **ESTILO-DLG-TEXTO** · `Text` ver abajo |
| `icoDlgXBorrarFilaCap` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelBorrarFilaCap` |
| `btnDlgCancelBorrarFilaCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkBorrarFilaCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgBorrarFilaCap: false})` |
| `btnDlgOkBorrarFilaCap` | Botón clásico | **ESTILO-BTN-PELIGRO** (es destructivo) · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `100` · `Text` `"Eliminar"` · `OnSelect` ver abajo |

```powerfx
// lblDlgTxtBorrarFilaCap.Text
"Se eliminará" &
If(ctxFilaBorrarTulasCap > 0, "n también sus " & ctxFilaBorrarTulasCap & " tulas", " la fila") &
If(!IsBlank(ctxFilaBorrarGuidCap), " de Dataverse", "") &
". Esta acción no se puede deshacer."
```

```powerfx
// btnDlgOkBorrarFilaCap.OnSelect — DATAVERSE PRIMERO, colección local DESPUÉS.
// Ese orden es el fix del bug N2: la v1 mutaba la colección y después leía
// ThisItem.GUID (ya inexistente), dejando tulas huérfanas en Dataverse.
Clear(colErrBorrarCap);

If(
    !IsBlank(ctxFilaBorrarGuidCap),

    // --- a) sus tulas persistidas. Remove con REGISTRO explícito (regla D-3),
    //        jamás RemoveIf(ds, predicado). Sin Set() adentro del ForAll (01 §3.2):
    //        se acumula con Collect y se decide afuera.
    ForAll(
        Filter(colTulas, PlanillaLocalId = ctxFilaBorrarCap && !IsBlank(GUID)) As B,
        IfError(
            Remove('CH-Tbl-Bags', LookUp(colTulasDV, 'CH-Tbl-Bags' = B.GUID)),
            // 🧨 "; Blank()" obligatorio: Collect devuelve TABLA e IfError exige el
            //    tipo del primer argumento (01 receta 7.5, regla 7).
            Collect(colErrBorrarCap, { Mensaje: "Tula " & B.Numero & ": " & FirstError.Message });
            Blank()
        )
    );

    // --- b) la planilla, SOLO si ninguna tula falló ---
    If(
        IsEmpty(colErrBorrarCap),
        IfError(
            Remove('CH-Tbl-Receptions', LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = ctxFilaBorrarGuidCap)),
            Collect(colErrBorrarCap, { Mensaje: "Planilla: " & FirstError.Message });
            Blank()
        )
    )
);

If(
    !IsEmpty(colErrBorrarCap),
    // Dataverse rechazó algo: la pantalla NO miente. No se toca nada local.
    Notify(
        "No se pudo eliminar: " & First(colErrBorrarCap).Mensaje & " — no se modificó nada en pantalla.",
        NotificationType.Error
    ),

    // --- c) recién ahora se muta lo local ---
    ClearCollect(colTulasNuevoCap, Filter(colTulas, PlanillaLocalId <> ctxFilaBorrarCap));
    ClearCollect(colTulas, colTulasNuevoCap);
    Clear(colTulasNuevoCap);
    Remove(colPlanillas, LookUp(colPlanillas, LocalId = ctxFilaBorrarCap));

    // Snapshots al día (Q3 y Q4) — solo si de verdad tocamos Dataverse.
    If(
        !IsBlank(ctxFilaBorrarGuidCap),
        ClearCollect(
            colPlanillasDV,
            Filter(
                'CH-Tbl-Receptions',
                ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
            )
        );
        ClearCollect(
            colTulasDV,
            Filter(
                'CH-Tbl-Bags',
                ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
            )
        )
    );

    If(!IsEmpty(colProblemas), ClearCollect(colProblemas, fxProblemas));
    Set(gvarCambiosSinGuardar, true);
    Notify("Planilla eliminada junto con sus tulas.", NotificationType.Success)
);

UpdateContext({ ctxDlgBorrarFilaCap: false })
```

---

## 8. Validar y Guardar

### 8.1 `btnValidarCap.OnSelect` — las 6 reglas, UNA sola vez

```powerfx
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<
//     Sin él, Validar juzga la colección de ANTES de la última tecla: el operador
//     corrige la fila, toca Validar y le sigue apareciendo el mismo problema.
//
// fxProblemas es la ÚNICA definición de las 6 reglas (01 §3.1). Acá no se
// reescribe ni una: se materializa. Validar, Guardar y Cerrar usan la misma.
ClearCollect(colProblemas, fxProblemas);
If(
    IsEmpty(colProblemas),
    Notify("Todos los registros son válidos.", NotificationType.Success),
    Notify(
        CountRows(Distinct(colProblemas, LocalId)) &
        If(CountRows(Distinct(colProblemas, LocalId)) > 1, " planillas", " planilla") &
        " con problemas — mirá el panel de validación",
        NotificationType.Error
    )
)
```

> `Distinct(colProblemas, LocalId)` cuenta **planillas**, no problemas: una fila con 3
> reglas rotas es *una* planilla con problemas, igual que en el mockup (`validar()`
> cuenta `Object.keys(probs).length`). Todo local.
>
> **Pluralización real, no `planilla(s)`**: el mockup escribe *"1 planilla con problemas"*
> / *"3 planillas con problemas"*. El `If(… > 1, …)` cuesta una expresión y saca de la
> pantalla la muletilla burocrática del paréntesis.
>
> Si `01` §3.1 tuvo que aplicar su fallback (Studio rechazando colecciones en
> `App.Formulas`), **la expresión completa de `fxProblemas` vive en este `OnSelect`** y
> §8.2/§9.2 arrancan con `Select(btnValidarCap);` en lugar del `ClearCollect`. La
> expresión no cambia ni una coma.

### 8.2 `btnGuardarCap.OnSelect` — volcado, validación y **delta**. Nada más.

🧨 **Este `OnSelect` NO escribe en Dataverse.** Hace lo barato —volcar, validar,
calcular el delta— y **cede el frame** prendiendo `tmrGuardarCap`. El guardado real
vive en §8.4. Motivo (receta `01` §7.11.C): canvas **no repinta a mitad de una
fórmula**, así que un diálogo *"Guardando…"* abierto en la misma fórmula que
patchea **no llega nunca al render**. Con el defer de un frame, el diálogo se pinta
primero y el trabajo corre después.

```powerfx
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<

// ===== 0. Guardas =====
If(
    CountRows(colPlanillas) = 0,
    Notify("No hay planillas que guardar.", NotificationType.Error),

    // ===== 1. Validación (única fuente: fxProblemas) =====
    ClearCollect(colProblemas, fxProblemas);
    If(
        !IsEmpty(colProblemas),
        Notify("Corregí los problemas de validación antes de guardar.", NotificationType.Error),

        // ===== 2. DELTA — se patchea lo que CAMBIÓ (receta 01 §7.11.D) =====
        //    Todo sobre colecciones locales: cero red.
        ClearCollect(
            colCambiosPlanCap,
            Filter(
                colPlanillas As P,
                IsBlank(P.GUID) ||
                With(
                    { D: LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = P.GUID) },
                    Trim(P.Planilla) <> Trim(D.ReceiptID) ||
                    P.Empresa.'CH-Tbl-ClientBranch' <> D.BranchName.'CH-Tbl-ClientBranch' ||
                    P.ChequeNIO   <> D.NIOCheck ||
                    P.ChequeUSD   <> D.USDCheck ||
                    P.EfectivoNIO <> D.NIOCash  ||
                    P.EfectivoUSD <> D.USDCash
                )
            )
        );
        ClearCollect(colTulasNuevasCap, Filter(colTulas, IsBlank(GUID)));

        // ===== 3. Rama SIN CAMBIOS — ni diálogo ni red =====
        If(
            IsEmpty(colCambiosPlanCap)
            && IsEmpty(colTulasNuevasCap)
            && IsEmpty(Filter(colTulasBorradasCap, !IsBlank(GUID))),

            Notify("No hay cambios que guardar.", NotificationType.Information);
            Set(gvarCambiosSinGuardar, false),

            // ===== 4. Rama CON CAMBIOS — abre el diálogo y CEDE EL FRAME =====
            //    ctxGuardandoCap es el Start de tmrGuardarCap: el timer arranca,
            //    canvas pinta "Guardando N cambios…" y 50 ms después corre §8.4.
            Clear(colFallidas);
            Clear(colEstadoGuardadoCap);
            UpdateContext({ ctxDlgResultadoCap: true, ctxGuardandoCap: true })
        )
    )
)
```

**Las tres decisiones de este bloque**:

| # | Decisión | Por qué |
|---|---|---|
| 1 | **La señal de origen NO se consume acá.** `ctxGuardarDesdeCierreCap` y `ctxSalirTrasGuardarCap` los lee y los apaga el `OnTimerEnd` (§8.4) | Cuando este `OnSelect` termina, el guardado **todavía no corrió**. Consumir la señal acá la dejaría en `false` justo antes de que el timer la necesite |
| 2 | **"Sin cambios" es una rama de primera clase**: `Notify` informativo, **sin diálogo**, `gvarCambiosSinGuardar` a `false` | Abrir un modal para decir "no hice nada" es un clic robado. Y bajar el flag es correcto: si no hay delta, no hay deuda |
| 3 | **Las bajas (`colTulasBorradasCap`) ya eran delta por construcción** | El panel de tulas solo mete ahí las que **tenían GUID** y el usuario sacó. No hay que calcularlas: hay que mirar si están |

### 8.2.a El guardado real — receta 7.5 sobre el delta

Lo que sigue es el cuerpo del `OnTimerEnd` de §8.4. Se documenta acá, junto a su
`OnSelect`, porque es **una sola operación partida en dos frames**.

```powerfx
        // ---------- FASE A: PLANILLAS, un solo Patch con tabla (camino feliz) ----------
        //    🧨 Sobre colCambiosPlanCap (el DELTA), NO sobre colPlanillas entera.
        IfError(
            Patch(
                'CH-Tbl-Receptions',
                // registros BASE, alineados fila a fila con el 3er argumento.
                // Defaults() crea; el LookUp es sobre colección LOCAL (cero consultas).
                ForAll(
                    colCambiosPlanCap As P,
                    If(
                        IsBlank(P.GUID),
                        Defaults('CH-Tbl-Receptions'),
                        LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = P.GUID)
                    )
                ),
                // registros de CAMBIO
                ForAll(
                    colCambiosPlanCap As P,
                    {
                        ReceiptID:      Trim(P.Planilla),
                        Name:           Trim(P.Planilla),
                        ReceptionBatch: gvarLoteActivo,
                        RemitentClient: P.Empresa.Client,
                        BranchName:     P.Empresa,
                        RemitentName:   P.Empresa.Name,
                        NIOCheck:       P.ChequeNIO,
                        USDCheck:       P.ChequeUSD,
                        NIOCash:        P.EfectivoNIO,
                        USDCash:        P.EfectivoUSD
                    }
                )
            ),

            // ---------- FASE B: algo falló. Fila por fila para saber CUÁL ----------
            ForAll(
                colCambiosPlanCap As P,
                IfError(
                    Patch(
                        'CH-Tbl-Receptions',
                        If(
                            IsBlank(P.GUID),
                            Defaults('CH-Tbl-Receptions'),
                            LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = P.GUID)
                        ),
                        {
                            ReceiptID:      Trim(P.Planilla),
                            Name:           Trim(P.Planilla),
                            ReceptionBatch: gvarLoteActivo,
                            RemitentClient: P.Empresa.Client,
                            BranchName:     P.Empresa,
                            RemitentName:   P.Empresa.Name,
                            NIOCheck:       P.ChequeNIO,
                            USDCheck:       P.ChequeUSD,
                            NIOCash:        P.EfectivoNIO,
                            USDCash:        P.EfectivoUSD
                        }
                    ),
                    // 🧨 "; Blank()" obligatorio (01 receta 7.5, regla 7): Collect
                    //    devuelve TABLA e IfError exige el tipo del Patch (REGISTRO).
                    Collect(
                        colFallidas,
                        { LocalId: P.LocalId, Planilla: P.Planilla, Mensaje: FirstError.Message }
                    );
                    Blank()
                )
            )
        );

        // ===== 3. Re-hidratación (Q3) y RE-KEYADO de GUIDs =====
        //    El N° de planilla es único dentro del lote (reglas 5 y 6 de fxProblemas):
        //    sirve de clave natural. Sin este paso, un segundo guardado DUPLICA registros.
        //    🧨 SE RE-HIDRATA colPlanillasDV — NO colTulasDV. Escribir la colección
        //    equivocada acá fue un bug real de la 1.31: el snapshot de planillas
        //    quedaba viejo, el delta de la vuelta siguiente creía que TODAS las
        //    planillas eran nuevas y el SEGUNDO GUARDADO DUPLICABA registros.
        ClearCollect(
            colPlanillasDV,
            Filter(
                'CH-Tbl-Receptions',
                ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
            )
        );
        ForAll(
            colPlanillasDV As D,
            Patch(
                colPlanillas,
                LookUp(colPlanillas, Trim(Planilla) = Trim(D.ReceiptID)),
                { GUID: D.'CH-Tbl-Receptions' }
            )
        );

        // ===== 4. PASO DE BAJAS (receta 01 §7.5, regla 5) =====
        //    Tulas quitadas del panel que YA existían en Dataverse (Q8).
        //    Remove con registro explícito, nunca RemoveIf(ds, predicado) (D-3).
        //    Una baja fallida CUENTA como fallida => el lote no se cierra.
        ForAll(
            Filter(colTulasBorradasCap, !IsBlank(GUID)) As X,
            IfError(
                Remove('CH-Tbl-Bags', LookUp(colTulasDV, 'CH-Tbl-Bags' = X.GUID)),
                Collect(
                    colFallidas,
                    { LocalId: X.PlanillaLocalId, Planilla: "tula " & X.Numero, Mensaje: FirstError.Message }
                );
                Blank()
            )
        );

        // ===== 5. TULAS: mismo patrón, DESPUÉS de las planillas (7.5 regla 4) =====
        //    🧨 Sobre colTulasNuevasCap (el DELTA), NO sobre colTulas entera: una
        //    tula ya persistida no cambia nunca desde esta pantalla (el panel es
        //    scan-first: se agrega o se borra, no se edita — §7.2).
        //    Necesitan el GUID de la planilla dueña YA re-keyado en el paso 3.
        //    BagStatus se calcula EXPLÍCITO por camino, nunca "el que tenía el lote"
        //    (01 §5.4 — mata la rama muerta N8).
        IfError(
            Patch(
                'CH-Tbl-Bags',
                ForAll(
                    colTulasNuevasCap As T,
                    If(
                        IsBlank(T.GUID),
                        Defaults('CH-Tbl-Bags'),
                        LookUp(colTulasDV, 'CH-Tbl-Bags' = T.GUID)
                    )
                ),
                ForAll(
                    colTulasNuevasCap As T,
                    {
                        BagID:       Trim(T.Numero),
                        Name:        Trim(T.Numero),
                        ReceptionID: LookUp(
                                        colPlanillasDV,
                                        'CH-Tbl-Receptions' = LookUp(colPlanillas, LocalId = T.PlanillaLocalId).GUID
                                     ),
                        BagStatus:   'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'   // 159460000
                    }
                )
            ),
            ForAll(
                colTulasNuevasCap As T,
                IfError(
                    Patch(
                        'CH-Tbl-Bags',
                        If(
                            IsBlank(T.GUID),
                            Defaults('CH-Tbl-Bags'),
                            LookUp(colTulasDV, 'CH-Tbl-Bags' = T.GUID)
                        ),
                        {
                            BagID:       Trim(T.Numero),
                            Name:        Trim(T.Numero),
                            ReceptionID: LookUp(
                                            colPlanillasDV,
                                            'CH-Tbl-Receptions' = LookUp(colPlanillas, LocalId = T.PlanillaLocalId).GUID
                                         ),
                            BagStatus:   'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'
                        }
                    ),
                    Collect(
                        colFallidas,
                        { LocalId: T.PlanillaLocalId, Planilla: "tula " & T.Numero, Mensaje: FirstError.Message }
                    );
                    Blank()
                )
            )
        );

        // ===== 6. Re-hidratación de tulas (Q4, espejo B-2 / regla D-7) y re-keyado =====
        //    Acá SÍ va colTulasDV: cada tabla re-hidrata la SUYA (ver paso 3).
        ClearCollect(
            colTulasDV,
            Filter(
                'CH-Tbl-Bags',
                ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
            )
        );
        ForAll(
            colTulasDV As B,
            Patch(colTulas, LookUp(colTulas, Trim(Numero) = Trim(B.BagID)), { GUID: B.'CH-Tbl-Bags' })
        );
        // PURGA de las bajas que SÍ se ejecutaron. NO es un Clear() a ciegas: una baja
        // que Dataverse rechazó sigue viva en colTulasDV (el snapshot recién
        // re-hidratado) y TIENE que quedar en colTulasBorradasCap para el reintento.
        ClearCollect(colTulasNuevoCap, Filter(colTulasBorradasCap As X, !IsBlank(LookUp(colTulasDV, 'CH-Tbl-Bags' = X.GUID))));
        ClearCollect(colTulasBorradasCap, colTulasNuevoCap);
        Clear(colTulasNuevoCap);

        // ===== 7. Estado POR PLANILLA para el overlay de guardado (§8.3) =====
        ClearCollect(
            colEstadoGuardadoCap,
            ForAll(
                colPlanillas As P,
                {
                    Planilla: P.Planilla,
                    Ok:       IsEmpty(Filter(colFallidas, LocalId = P.LocalId)),
                    Mensaje:  Coalesce(LookUp(colFallidas, LocalId = P.LocalId).Mensaje, "Guardada")
                }
            )
        );

        // ===== 8. CONTINUACIÓN DEL FLUJO QUE INVOCÓ EL GUARDADO =====
        //   🧨 Va ACÁ, dentro del OnTimerEnd, y NO después del Select(btnGuardarCap)
        //   del botón invocador: cuando ese OnSelect retoma, el guardado TODAVÍA NO
        //   CORRIÓ (receta 01 §7.11.C, regla 3).
        If(
            IsEmpty(colFallidas),
            Set(gvarCambiosSinGuardar, false);

            // --- 8a. ¿venía de "Cerrar lote"? Se transiciona el lote (receta 7.9) ---
            If(
                ctxGuardarDesdeCierreCap,
                IfError(
                    Patch(
                        'CH-Tbl-ReceptionBatches',
                        gvarLoteActivo,
                        { ReceptionStatus: 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido }
                    ),
                    Notify("El cierre fue rechazado: " & FirstError.Message, NotificationType.Error),
                    // rama de ÉXITO de IfError
                    Set(gvarLoteActivo, Blank());
                    Clear(colPlanillas);   Clear(colTulas);
                    Clear(colPlanillasDV); Clear(colTulasDV);
                    Clear(colProblemas);   Clear(colFallidas);
                    Clear(colTulasEdit);   Clear(colTulasBorradasCap);
                    Clear(colEstadoGuardadoCap);
                    Clear(colCambiosPlanCap); Clear(colTulasNuevasCap);
                    UpdateContext({ ctxDlgResultadoCap: false });
                    // El overlay A vuelve solo (Visible = IsBlank(gvarLoteActivo)) y su
                    // combo tiene que volver VACÍO: sin este Reset, el próximo lote nace
                    // con la transportadora residual del que se acaba de cerrar (L-19).
                    Reset(cmbTransportadoraCap);
                    Notify("Lote cerrado. Pasó a Recibido y ya aparece en la pantalla Lotes.", NotificationType.Success)
                ),
                Notify(
                    "Lote guardado: " & CountRows(colCambiosPlanCap) & " planilla(s) actualizada(s).",
                    NotificationType.Success
                )
            ),

            // --- 8b. hubo fallidas: el overlay D se queda abierto con el detalle ---
            Notify(
                CountRows(colFallidas) & " registro(s) rechazados — revisá el panel y reintentá.",
                NotificationType.Error
            );
            If(
                ctxGuardarDesdeCierreCap,
                Notify(
                    "El lote NO se cerró: hay registros rechazados. Reintentá y volvé a cerrar.",
                    NotificationType.Error
                )
            )
        );

        // ===== 9. NAVEGACIÓN de "Guardar y salir" — SIEMPRE lo último =====
        //   🧨 Los timers NO corren en pantallas que no están visibles: navegar antes
        //   de este punto mata el guardado en el aire (01 §7.11.C, regla 2).
        If(
            ctxSalirTrasGuardarCap && IsEmpty(colFallidas),
            UpdateContext({ ctxDlgResultadoCap: false });
            Navigate(scrLotes)
        );

        // ===== 10. Se consumen las señales y se APAGA el timer =====
        UpdateContext({
            ctxGuardarDesdeCierreCap: false,
            ctxSalirTrasGuardarCap:   false,
            ctxGuardandoCap:          false
        })
```

**Las reglas que este bloque hace cumplir** (recetas 7.5 y 7.11):

1. **`gvarCambiosSinGuardar` pasa a `false` SOLO si `colFallidas` está vacía.** La UI
   nunca marca como guardado lo que el guard rechazó (bug **N3**).
2. **Re-keyado desde Dataverse** por `ReceiptID` / `BagID`, no por el orden de retorno
   del `Patch`, y **cada tabla re-hidrata la SUYA** (paso 3 → `colPlanillasDV`;
   paso 6 → `colTulasDV`). Sin esto, el segundo guardado duplica.
3. **Las tulas van después** de las planillas, y su `ReceptionID` sale del snapshot
   re-hidratado.
4. **Cero `Refresh()`** (regla `D-5`), **cero `RemoveIf`** (regla `D-3`).
5. `colEstadoGuardadoCap` marca `Ok = false` también cuando lo que falló fue una
   **tula** de esa planilla: `colFallidas.LocalId` de una tula lleva el `LocalId` de su
   **planilla dueña**.
6. **`ctxGuardandoCap` SÍ existe y SÍ se pinta**, al revés que en la 1.31. Lo que
   cambió no es la bandera sino **dónde corre el trabajo**: con el defer de un frame
   (`tmrGuardarCap`), canvas alcanza a pintar el diálogo antes de bloquearse. La
   bandera es además el `Start` del timer: una sola variable, dos trabajos.
7. **Todo lo que "sigue después de guardar" vive acá dentro** — la transición a
   `Recibido` del cierre y el `Navigate` de *Guardar y salir*. Fuera del `OnTimerEnd`
   correrían **antes** del guardado.
8. **Se patchea el DELTA, no el lote entero**: `colCambiosPlanCap` y
   `colTulasNuevasCap`. Guardar 40 planillas idénticas en cada click dispara el
   `GuardReceptionPlugin` 40 veces para nada.

### 8.3 Overlay D — **Guardando** y resultado (`cntDlgGuardarCap`)

🧨 **Ahora SÍ hay estado "en vuelo", y se pinta.** El overlay tiene **dos estados**,
gobernados por `ctxGuardandoCap`:

| `ctxGuardandoCap` | Título | Cuerpo | Botones |
|---|---|---|---|
| `true` — **en vuelo** | `"Guardando " & <N> & " cambios…"` | la barra en tono suave, sin lista | **ninguno visible** (no hay nada que decidir todavía) |
| `false` — **resultado** | `"Lote guardado"` / `"Guardado con errores"` | barra con el porcentaje real + lista por planilla | *Listo* · o *Cerrar* + *Reintentar fallidas* |

Lo que lo hace posible es el **defer de un frame** de `tmrGuardarCap` (§8.4, receta
`01` §7.11.C): el `OnSelect` abre el diálogo y **termina**; canvas pinta; 50 ms
después corre el guardado. En la 1.31 todo colgaba de un `OnSelect` y el estado "en
vuelo" **no llegaba nunca al render** — de ahí la vieja redacción de **L-10**.

**Sigue sin llevar ✕** y sigue **sin `Spinner`**: un diálogo de resultado no se
cancela, y durante el `OnTimerEnd` el hilo se bloquea igual, así que un spinner se
congelaría a mitad de giro (ver la nota al pie de esta sección).

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgGuardarCap` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgResultadoCap`** |
| `btnDlgBloqueoGuardarCap` | Botón clásico | receta 7.2 |
| `cntDlgCardGuardarCap` | Contenedor de diseño manual | receta 7.2 · Width `520` · Height `400` |
| `lblDlgTitGuardarCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 40` · Height `22` · **ESTILO-DLG-TITULO** · `Text` ver abajo — **tres** estados, no dos |
| `shpBarraFondoCap` | Botón clásico (receta 7.1) | X `20` · Y `50` · Width `Parent.Width - 40` · Height `7` · `Radius` (las 4) `4` · `Fill`/`HoverFill`/`PressedFill` `ThemeLineSoft` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `shpBarraProgresoCap` | Botón clásico (receta 7.1) | X `shpBarraFondoCap.X` · Y `shpBarraFondoCap.Y` · Height `shpBarraFondoCap.Height` · `Radius` (las 4) `4` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccent` · `Width` ver abajo |
| `lblDlgTxtGuardarCap` | Etiqueta clásica | X `20` · Y `shpBarraFondoCap.Y + shpBarraFondoCap.Height + 7` · Width `Parent.Width - 40` · Height `18` · `Size` `8.63` · `Color` `ThemeInk2` · `Text` ver abajo |
| `galEstadoGuardarCap` | Galería vertical en blanco | X `20` · Y `lblDlgTxtGuardarCap.Y + lblDlgTxtGuardarCap.Height + 8` (= `90`; y `lblDlgTxtGuardarCap` ya cuelga de `shpBarraFondoCap`, así que toda la columna se corre sola si la barra cambia de alto) · Width `Parent.Width - 40` · Height `254` (= `btnDlgListoGuardarCap.Y - 6 - Self.Y`: la galería termina **6 px antes** de la fila de botones; con `260` quedaba pegada) · `TemplateSize` **`34`** · `TemplatePadding` `0` · `ShowScrollbar` `true` · `OnSelect` `false` · `Items` **`colEstadoGuardadoCap`** · **`Visible` `!ctxGuardandoCap`** |
| `btnDlgListoGuardarCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Listo"` · **`Visible` `!ctxGuardandoCap && IsEmpty(colFallidas)`** · `OnSelect` `UpdateContext({ctxDlgResultadoCap: false}); Clear(colEstadoGuardadoCap)` |
| `btnDlgCerrarGuardarCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkGuardarCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Cerrar"` · **`Visible` `!ctxGuardandoCap && !IsEmpty(colFallidas)`** · `OnSelect` = **carácter por carácter** el de `btnDlgListoGuardarCap` |
| `btnDlgOkGuardarCap` | Botón clásico | **ESTILO-BTN-CONTORNO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `150` · `Text` `"Reintentar fallidas"` · **`Visible` `!ctxGuardandoCap && !IsEmpty(colFallidas)`** · `OnSelect` ver abajo |

> 🧨 **Los tres botones y la galería llevan `!ctxGuardandoCap`**: mientras el
> guardado está en vuelo no hay nada que decidir ni nada que leer, y un *Listo*
> clicable sobre un resultado que todavía no existe es una trampa. Con las tres
> `Visible` en `false`, el diálogo "en vuelo" es exactamente: título, barra y
> subtítulo.

> **Por qué DOS botones y no uno con `Text` condicional**: el estilo tiene que cambiar
> con el estado. *Listo* es el cierre de un camino feliz ⇒ **primario**. *Cerrar* convive
> con *Reintentar fallidas*, que es la acción que el usuario tiene que ver primero ⇒
> *Cerrar* baja a **fantasma**. `ESTILO-BTN-*` son alias de **varias** propiedades
> (`Fill`, `Color`, `BorderColor`, `BorderThickness`): no se conmutan con un `If` en el
> `Text`. Sus `Visible` son **excluyentes**, así que en pantalla siempre hay exactamente
> uno.
>
> **Ocupan slots distintos a propósito**: con `colFallidas` vacía, *Reintentar fallidas*
> está oculto y *Listo* toma el slot primario de la derecha (`Parent.Width - 20 - Self.Width`);
> con fallidas, *Reintentar* ocupa ese slot y *Cerrar* se ancla a su izquierda. Anclar
> los dos al mismo X dejaría un hueco de 158 px a la derecha en el camino feliz.

**Template de `galEstadoGuardarCap`** (`TemplateSize 34`):

| Control | Tipo | Propiedades |
|---|---|---|
| `shpDotEstadoCap` | Botón clásico (receta 7.1) | X `0` · Y `11` · Width `7` · Height `7` · `Radius` (las 4) `4` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Fill`/`HoverFill`/`PressedFill` `If(ThisItem.Ok, ThemeOk, ThemeDanger)` |
| `lblEstadoCap` | Etiqueta clásica | X `14` · Y `3` · Width `galEstadoGuardarCap.Width - 28` · Height `28` · **ESTILO-VALITEM** · `Wrap` `true` · `Text` ver abajo — el `- 28` (y no `- 14`) deja los 14 px de la derecha libres para la **barra de scroll** de la galería: con `- 14` el texto pasa por debajo |

```powerfx
// lblDlgTitGuardarCap.Text — TRES estados
If(
    ctxGuardandoCap,
    "Guardando " &
        (CountRows(colCambiosPlanCap) + CountRows(colTulasNuevasCap) +
         CountRows(Filter(colTulasBorradasCap, !IsBlank(GUID)))) &
        " cambios…",
    IsEmpty(colFallidas), "Lote guardado",
    "Guardado con errores"
)

// lblDlgTxtGuardarCap.Text
If(
    ctxGuardandoCap,
    "No cierres la app ni cambies de pantalla.",
    CountRows(colCambiosPlanCap) - CountRows(Distinct(colFallidas, LocalId)) & " de " &
    CountRows(colCambiosPlanCap) & " planillas guardadas."
)

// shpBarraProgresoCap.Width — porcentaje REAL, no animación
If(
    ctxGuardandoCap,
    // En vuelo no hay progreso que reportar: la barra queda a un tercio, quieta.
    shpBarraFondoCap.Width / 3,
    shpBarraFondoCap.Width *
        (CountRows(colCambiosPlanCap) - CountRows(Distinct(colFallidas, LocalId))) /
        Max(CountRows(colCambiosPlanCap), 1)
)

// lblEstadoCap.Text
"Planilla " &
If(IsBlank(Trim(ThisItem.Planilla)), "sin número", Trim(ThisItem.Planilla)) &
" — " & ThisItem.Mensaje
```

> **El contador del título suma las TRES clases de cambio** (planillas del delta +
> tulas nuevas + bajas con GUID): es lo que el operador entiende por "cambios", y es
> exactamente lo que el `OnTimerEnd` va a mandar. La rama "sin cambios" de §8.2 ni
> abre este diálogo, así que el contador nunca dice `0`.
>
> **Las cuentas del resultado son sobre `colCambiosPlanCap`, no sobre
> `colPlanillas`**: decir *"38 de 40 planillas guardadas"* cuando solo se mandaron 2
> es mentir sobre lo que pasó.

```powerfx
// btnDlgOkGuardarCap.OnSelect — "Reintentar fallidas"
// Se vuelve a correr EL guardado real, no una copia. Es idempotente: después del
// re-keyado del paso 3, cada fila ya tiene su GUID y el Patch la ACTUALIZA en vez
// de crearla; las que ya habían entrado se re-patchean con el mismo contenido.
// 🧨 NO se limpia colCambiosPlanCap: el delta del intento anterior es EXACTAMENTE
//    lo que hay que reintentar. Select(btnGuardarCap) lo recalcula igual, y las
//    filas que sí entraron ya no van a diferir del snapshot re-hidratado.
Clear(colFallidas);
Clear(colEstadoGuardadoCap);
UpdateContext({ ctxDlgResultadoCap: false });
Select(btnGuardarCap)
```

> ⚠️ **Sin barra de progreso animada y sin `Spinner`** — pero **CON** estado "en
> vuelo". El camino feliz es **una sola llamada** (`Patch` con tabla): no hay
> progreso por fila que reportar. Y durante el `OnTimerEnd` el hilo se bloquea igual,
> así que un `Spinner` **se congelaría a mitad de giro** y comunicaría peor que un
> texto quieto. Lo que sí se ganó con el defer de un frame es que el diálogo
> *"Guardando N cambios…"* **se pinta de verdad**: la parte de **L-10** que decía "el
> estado en vuelo no llega a pintarse" quedó **superada** (ver ledger de `00`, L-25).
> **L-11** (sin `Spinner`) sigue vigente, y ahora con un motivo más fuerte.

### 8.4 `tmrGuardarCap` — el defer de UN frame

| Propiedad | Valor |
|---|---|
| Tipo | **Temporizador clásico** (Insertar → Clásico → Temporizador) — catálogo `01` §0.3 |
| Padre | **la pantalla**, no un contenedor de diálogo. Último control del árbol |
| `X` / `Y` / `Width` / `Height` | `0` · `0` · `40` · `40` (irrelevantes: no se ve) |
| `AutoStart` | **`false`** |
| `Duration` | **`50`** (ms) |
| `Repeat` | **`false`** |
| **`Start`** | **`ctxGuardandoCap`** |
| `Visible` | **`false`** |
| `OnTimerEnd` | **todo el bloque de §8.2.a**, tal cual, de la FASE A al paso 10 |

> 🧨 **Las cuatro cosas que lo hacen funcionar** (receta `01` §7.11.C):
>
> 1. **`Start` es la bandera, no `true`.** El timer arranca cuando `ctxGuardandoCap`
>    sube (§8.2 paso 4) y se desarma cuando el `OnTimerEnd` la baja (paso 10). Con
>    `Repeat false`, corre **exactamente una vez por click**.
> 2. **`Visible false` PERO en la pantalla.** Un timer dentro de un contenedor
>    invisible **no corre**, y el overlay D está oculto justo en el instante en que
>    el timer tiene que arrancar. Por eso vive suelto en `scrCaptura`. *(Si tu
>    versión de Studio tampoco corre un timer con `Visible false` a nivel pantalla,
>    la escotilla es dejarlo `Visible true` con `Width`/`Height` `1` en una esquina:
>    anotalo como divergencia, no lo apliques callado.)*
> 3. **Los timers NO corren en pantallas que no están visibles.** Por eso el
>    `Navigate(scrLotes)` de *Guardar y salir* vive en el **paso 9 del `OnTimerEnd`**
>    y no en el `OnSelect` del botón: navegar antes mata el guardado en el aire.
> 4. **Esto NO es el `'timer Submit'` de la v1.** Aquel era una **máquina de
>    estados** con `varCurrentRow`/`varFirstRow`/`varRowIndex` que iteraba fila por
>    fila usando el timer como bucle. Éste corre **una sola vez**, sin estado de
>    iteración y sin `Repeat`. La prohibición de `00` §2.4 queda **matizada, no
>    levantada**: si alguna vez ves un `Repeat true` o un contador de fila en un
>    timer de esta app, eso sí es la v1 volviendo.

---

## 9. Cerrar lote (receta 7.9) y guarda de salida (receta 7.8)

### 9.1 `btnCerrarCap.OnSelect` — valida y ABRE la confirmación

**Confirmación previa obligatoria** (7.9 regla 3): el botón nunca cierra; abre el
diálogo. El bloque de cierre real vive en el botón *confirmar* (§9.2).

```powerfx
// >>> VOLCADO (§5.4.a) — PRIMERA LÍNEA <<<
If(
    CountRows(colPlanillas) = 0,
    Notify("El lote no tiene planillas.", NotificationType.Error),

    ClearCollect(colProblemas, fxProblemas);
    If(
        !IsEmpty(colProblemas),
        Notify("Corregí los problemas de validación antes de cerrar.", NotificationType.Error),
        UpdateContext({ ctxDlgCerrarCap: true })
    )
)
```

### 9.2 Overlay E — Confirmar cierre (`cntDlgCerrarCap`)

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgCerrarCap` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgCerrarCap`** |
| `btnDlgBloqueoCerrarCap` | Botón clásico | receta 7.2 |
| `cntDlgCardCerrarCap` | Contenedor de diseño manual | receta 7.2 · Width `380` · Height `220` |
| `lblDlgTitCerrarCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 66` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"¿Cerrar el lote " & gvarLoteActivo.Name & "?"` |
| `lblDlgTxtCerrarCap` | Etiqueta clásica | X `20` · Y `lblDlgTitCerrarCap.Y + lblDlgTitCerrarCap.Height + 4` · Width `Parent.Width - 40` · Height `96` · **ESTILO-DLG-TEXTO** · `Text` ver abajo |
| `icoDlgXCerrarCap` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelCerrarCap` |
| `btnDlgCancelCerrarCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgOkCerrarCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgCerrarCap: false})` |
| `btnDlgOkCerrarCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `108` · `Text` `"Cerrar lote"` · `OnSelect` ver abajo |

```powerfx
// lblDlgTxtCerrarCap.Text
"Se persisten las " & CountRows(colPlanillas) & " planillas con sus " & TotalTulas &
" tulas y el lote pasa a Recibido. Después del cierre, toda modificación se hace desde la pantalla Lotes."
```

```powerfx
// btnDlgOkCerrarCap.OnSelect — receta 7.9 (cierre CLIENT-SIDE, ver nota B0-3 abajo)
// 🧨 ESTE BOTÓN NO CIERRA EL LOTE. Valida, marca el ORIGEN y delega en el guardado
//    diferido: la transición a 'Recibido' vive en el paso 8a del OnTimerEnd (§8.2.a).
//    Motivo (01 §7.11.C, regla 3): cuando el Select(btnGuardarCap) de abajo retoma,
//    el guardado TODAVÍA NO CORRIÓ — leer colFallidas acá daría siempre vacío y el
//    lote se cerraría aunque el guard hubiera rechazado todo.
UpdateContext({ ctxDlgCerrarCap: false });

// ===== 1. Guarda de habilitación (misma condición que fxPuedeCerrar) =====
If(
    !fxPuedeCerrar,
    Notify(
        If(
            IsBlank(gvarLoteActivo),        "No hay un lote activo.",
            CountRows(colPlanillas) = 0,    "El lote no tiene planillas.",
                                            "Corregí los problemas de validación antes de cerrar."
        ),
        NotificationType.Error
    ),

    // ===== 2. Se invoca el guardado REAL, no una copia =====
    //    La señal de ORIGEN va ANTES del Select. El OnTimerEnd la lee en su paso 8a
    //    y, si no hubo fallidas, transiciona el lote y limpia todo.
    UpdateContext({ ctxGuardarDesdeCierreCap: true });
    Select(btnGuardarCap)
)
```

> 🧨 **Caso borde declarado — "cerrar sin cambios que guardar".** Si el operador toca
> *Cerrar lote* con el lote **ya guardado**, la rama "sin cambios" de §8.2 no prende
> el timer y **el paso 8a nunca corre**: el lote no se cierra y el único mensaje es
> *"No hay cambios que guardar."* Para que eso no pase, la rama sin cambios de §8.2
> lleva esta línea al final:
>
> ```powerfx
> // dentro de la rama SIN CAMBIOS de §8.2, después de Set(gvarCambiosSinGuardar, false)
> If(ctxGuardarDesdeCierreCap || ctxSalirTrasGuardarCap,
>    UpdateContext({ ctxGuardandoCap: true }))   // hay continuación: igual hay que correr el timer
> ```
>
> Con eso, el timer corre igual, las tres fases de guardado no mandan nada (los
> deltas están vacíos) y el paso 8a cierra el lote. **Es el ítem de checklist
> "cerrar un lote recién guardado".**

> **Al limpiar `gvarLoteActivo` (paso 8a), el overlay A vuelve solo**
> (`Visible = IsBlank(gvarLoteActivo)`): el operador queda listo para el lote
> siguiente sin tocar nada. Es exactamente lo que hace `doCerrar()` en el mockup.
>
> `varSoloGuardar` **no existe** (bug **N1**): la decisión de cerrar o no se toma con
> `colFallidas`, que se limpia al empezar cada guardado.
>
> 🧨 **Dependencia de backend B0-3 (divergencia `00` §5 → L-1)**: la Custom API
> `sanic_CloseReceptionBatch` **no existe todavía**. Cuando exista, el paso 8a del
> `OnTimerEnd` (§8.2.a) se reemplaza por el bloque de `01` §7.9 (`Environment.sanic_CloseReceptionBatch(...)`
> dentro de `IfError`) y el cierre pasa a ser **atómico** con dup-check server-side
> contra la tabla completa. Hasta entonces, el duplicado cross-lote lo rechaza el
> `GuardReceptionPlugin` en el `Patch` y el mensaje llega por `IfError` — feo, pero
> **no silencioso**.

### 9.3 Overlay F — Cambios sin guardar (`cntDlgSalirCap`, receta 7.8)

Lo abre `segLotesCap` cuando `gvarCambiosSinGuardar` es `true` (§3). Es la **variante de
3 botones** de la receta 7.2.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlgSalirCap` | Contenedor de diseño manual | receta 7.2 · **`Visible` `ctxDlgSalirCap`** |
| `btnDlgBloqueoSalirCap` | Botón clásico | receta 7.2 |
| `cntDlgCardSalirCap` | Contenedor de diseño manual | receta 7.2 · Width `420` · Height `190` |
| `lblDlgTitSalirCap` | Etiqueta clásica | X `20` · Y `16` · Width `Parent.Width - 66` · Height `22` · **ESTILO-DLG-TITULO** · `Text` `"Tenés cambios sin guardar"` |
| `lblDlgTxtSalirCap` | Etiqueta clásica | X `20` · Y `lblDlgTitSalirCap.Y + lblDlgTitSalirCap.Height + 4` · Width `Parent.Width - 40` · Height `56` · **ESTILO-DLG-TEXTO** · `Text` `"Si salís sin guardar, los cambios de este lote se pierden."` |
| `icoDlgXSalirCap` | Icono clásico `Icon.Cancel` | receta 7.2 · `OnSelect` = el de `btnDlgCancelSalirCap` |
| `btnDlgCancelSalirCap` | Botón clásico | **ESTILO-BTN-FANTASMA** · X `btnDlgDescartarSalirCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `96` · `Text` `"Cancelar"` · `OnSelect` `UpdateContext({ctxDlgSalirCap: false})` |
| `btnDlgDescartarSalirCap` | Botón clásico | **ESTILO-BTN-CONTORNO** · X `btnDlgOkSalirCap.X - 8 - Self.Width` · Y `Parent.Height - 50` · Width `100` · `Text` `"Descartar"` · `OnSelect` `UpdateContext({ctxDlgSalirCap: false}); Set(gvarCambiosSinGuardar, false); Clear(colPlanillas); Clear(colTulas); Clear(colTulasBorradasCap); Navigate(scrLotes)` |
| `btnDlgOkSalirCap` | Botón clásico | **ESTILO-BTN-PRIMARIO** · X `Parent.Width - 20 - Self.Width` · Y `Parent.Height - 50` · Width `126` · `Text` `"Guardar y salir"` · `OnSelect` `UpdateContext({ctxDlgSalirCap: false, ctxSalirTrasGuardarCap: true}); Select(btnGuardarCap)` — 🧨 **SIN `Navigate` acá** |

> 🧨 **El `Navigate` NO va en este `OnSelect`. Va en el paso 9 del `OnTimerEnd`**
> (§8.2.a). Motivo (receta `01` §7.11.C, regla 2): **los timers no corren en pantallas
> que no están visibles.** Un `Navigate(scrLotes)` acá deja `scrCaptura` invisible
> **antes** de que `tmrGuardarCap` dispare, y el guardado **no ocurre nunca** — el
> operador se va a Lotes convencido de que guardó. La versión 1.31 tenía
> `Select(btnGuardarCap); If(!gvarCambiosSinGuardar, Navigate(scrLotes))`, que además
> leía el flag **antes** de que el guardado lo tocara.
>
> **"Guardar y salir" solo navega si el guardado quedó sin fallidas** — la condición
> vive en el paso 9: `If(ctxSalirTrasGuardarCap && IsEmpty(colFallidas), … Navigate())`.
> Si el guard rechazó algo, te quedás en Captura con el overlay D a la vista.
> `App.BackEnabled = false` (`01` §3.4) es lo que impide saltarse esta guarda con el
> back del navegador.
>
> **`ctxSalirTrasGuardarCap`, no `ctxGuardarDesdeCierreCap`**: son **dos señales
> distintas** y hacen cosas distintas. `ctxGuardarDesdeCierreCap` dispara la
> transición del lote a `Recibido` (paso 8a); `ctxSalirTrasGuardarCap` dispara la
> navegación (paso 9). Confundirlas cierra el lote cuando el usuario solo quería
> salir.
>
> 🧨 **`Descartar` VACÍA las colecciones de edición** (`colPlanillas`, `colTulas`,
> `colTulasBorradasCap`) además de bajar el flag. Sin eso, "descartar" es una mentira:
> lo tecleado sigue vivo en memoria y volver a Captura lo muestra igual, con el flag en
> `false` — el usuario cree que descartó y a la primera edición se le guarda todo junto.
> Los snapshots `…DV` **no** se tocan a propósito: son la copia fiel de Dataverse, y
> dejarlos es lo que hace que el `OnVisible` re-proyecte el lote **como está guardado**
> (receta `01` §7.10, guarda `IsEmpty(colPlanillas)`).

---

## 10. Notas de la guía

### 10.1 Dependencias de backend y aproximaciones honestas

| ID | Qué | Dónde pega | Fallback sin backend |
|---|---|---|---|
| **B0-3** | Custom API `sanic_CloseReceptionBatch` | §9.2 | Cierre **client-side**: `Patch` + `IfError` + validación local. **No** detecta tulas duplicadas en OTRO lote del entorno (haría falta una consulta por tula ⇒ N+1 y violación de `D-2`). Ese chequeo lo rechaza hoy el `GuardReceptionPlugin`. Ledger `00` **L-1** |
| **B-2** (`ReceptionBatchId` en Bags) | **YA EXISTE en DEV** | Q4 (§2) | **Ninguno**: sin él no hay forma delegable de traer las tulas de un lote. Si IntelliSense no lo autocompleta, **quitá y re-agregá `CH-Tbl-Bags`** desde el panel Datos (`01` §2.2). **Bloquea esta pantalla** |
| **B0-9** `sanic_ClientNameMirror` en `CH-Tbl-ClientBranches` | **YA EXISTE en DEV** (`ClientBranchNameMirrorPlugin`, `01` §2.2) | §5.4 (`DisplayFields`/`SearchFields` del combo server-side) | **Ninguno**: sin el espejo, buscar por nombre de empresa exigiría `Client.Name` dentro de un `Filter` — dot-walk, **no delegable** (D-1). Si IntelliSense no lo trae, **quitá y re-agregá `CH-Tbl-ClientBranches`**. **Bloquea el combo de empresa** |
| `CH-Tbl-Clients` como datasource | 🚫 **NO se agrega** (`01` §2.1) | — | La excepción condicional **quedó sin efecto** tras L-23: `P.Empresa` es un registro **fresco del datasource** (receta 7.10) con su lookup `Client` navegable, y la etiqueta del combo ya no usa `Client.Name` sino el espejo B0-9. Con 1.31.0.4 desplegada, **no hizo falta** |
| Cargo del usuario en el chip | — | §3 (`lblUsuarioCap`) | El mockup dice *"María Gómez · Oficial de esclusa"*. **Cero conectores** (`01` §1.5): `User()` da nombre y correo, **no** el puesto. El chip muestra solo `UsuarioNombre`. Divergencia **L-12** |
| Fecha de creación del lote (`lblMetaCap`) | — | §4 | Sale de `'Created On'` del sistema. El mockup dice *"creado hoy 08:12"*; acá va la fecha completa con `FmtFecha` (`HH`, 24 h — **nunca `hh`**, que es reloj de 12 h sin AM/PM) |

### 10.2 Gotchas de canvas que esta pantalla pisa

1. **`ThisItem` de `galPlanillasCap` es una proyección, no el registro.** Toda escritura
   va por `LookUp(colPlanillas, LocalId = ThisItem.LocalId)` (§5.3). Es el error más
   fácil de cometer al vestir el YAML.
2. **El combo dentro del template va SERVER-SIDE** (§5.4, L-23): su `Items` es un
   `Filter` sobre `CH-Tbl-ClientBranches` — la **única excepción declarada a `D-4`**,
   habilitada por `IsSearchable` y obligada por `D-10` (la tabla tiene ~50 000 filas y
   no se puede cachear). **Verificá en Studio** que la lista desplegable no quede
   recortada por el borde de la galería. Si se recorta, subí el `TemplateSize` o
   movés la selección de empresa a un diálogo chico (divergencia nueva: anotala, no
   la apliques callado).
3. **`ClearCollect(col, If(cond, …))` sin `else`** revienta con `ErrorKind.Internal`. En
   §1 el `If` va **siempre por fuera**.
4. **`Set()` dentro de `ForAll` está prohibido** y `pac canvas pack` **no valida
   fórmulas**: viaja publicado y explota en Studio (bug real del Hub 1.27). En §7.3 y
   §8.2 la acumulación se hace con `Collect()` y el `Set()` va **afuera**.
5. **El estado de galerías y diálogos persiste entre visitas**: por eso el `OnVisible`
   cierra los 6 overlays y hace `Reset(galPlanillasCap)`.
6. **Los `Fill` de superficies decorativas llevan `HoverFill` y `PressedFill` iguales**
   (receta 7.1): si no, la card parpadea al pasar el mouse.
7. **La sombra de `.card` / `.dlg` del mockup no existe** en el Botón clásico; solo el
   Contenedor tiene `DropShadow`, y por eso las cards de los 6 diálogos son
   Contenedores. Pérdida declarada en `01` §7.1.
8. **`Radius` solo lo tienen Botón, Entrada de texto, Imagen y Contenedor.** Si en esta
   guía ves un `Radius` sobre un Rectángulo o una Etiqueta, es un bug: reportalo.
9. **`AccessibleLabel`**: poner uno en `imgMarcaCap`, en los 4 iconos (`icoValidarCap`,
   `icoNuevaPlanillaCap`, `icoBorrarFilaCap`, `icoBorrarTulaEditCap`) y en las 4 ✕.
   Sin eso, App Checker deja warnings de accesibilidad (`01` §8).
10. 🧨 **Un `Patch` a la colección en un `OnChange` de fila MATA EL FOCO** (`01`
    §7.11.A). Los 6 controles editables del template hacen **solo**
    `Set(gvarCambiosSinGuardar, true)`; el valor lo baja el **volcado** (§5.4.a).
    Es el hallazgo más caro del QA humano de la 1.31.
11. 🧨 **`TabIndex` positivo dentro de un template = filas entrelazadas** (`01`
    §7.11.B): las réplicas comparten el número y el recorrido global agrupa por
    número, no por fila. **Siempre `0`** + orden geométrico. Y los Íconos/Imágenes/
    Formas nacen en `-1`: si son interactivos, van a `0` explícito; si son
    destructivos, se quedan en `-1` a propósito.
12. 🧨 **Un timer dentro de un contenedor invisible no corre, y ningún timer corre en
    una pantalla no visible** (§8.4). Por eso `tmrGuardarCap` cuelga de la pantalla y
    el `Navigate` de *Guardar y salir* vive en el `OnTimerEnd`.

### 10.3 Costo de red declarado

Toda la edición —tipear montos, cambiar empresa, agregar/quitar tulas, validar, ver
totales, resaltar filas— corre a **cero consultas**. La red se toca únicamente en:
entrar la primera vez (Q1 + Q2, catálogos), retomar un lote (Q3 + Q4), crear (Q5),
guardar (Q6 + Q7 + Q8 + Q3 + Q4), borrar una planilla persistida (Q8 + Q3 + Q4) y
cerrar (todo lo del guardado + Q9). **Ninguna consulta vive dentro del template de una
galería** — ese era el N+1 de la v1.

### 10.4 Huecos de `01` que esta pantalla detectó — **TODOS CERRADOS**

Los nueve se resolvieron **hacia arriba**: `01` ya los incorpora y es la fuente de
verdad. Esta tabla queda como trazabilidad; **la columna de la derecha es dónde vive
hoy la respuesta**, no una tarea pendiente.

| # | Hueco que había en `01` | Cómo quedó | Dónde vive ahora |
|---|---|---|---|
| **H-1** | **`colTransportadoras` vs. la vista.** §2.5 mandaba la *Lookup View* al `Items` del combo; §4 decía que el combo sale de `colTransportadoras`. Se contradecían | La vista es el **ORIGEN**, la colección es el `Items`. Siempre | ✅ `01` §2.5 (tabla + nota) y §4 |
| **H-2** | **Literal del option set `RecordStatus`.** §2.4 daba solo el crudo `159460000` y el nombre lógico `sanic_chchrecordstatus` | **`'CH-Ch-RecordStatus'.Active`** — literal del option set **GLOBAL**, nunca el entero (en una picklist el crudo no delega) y nunca `'RecordStatus (CH-Tbl-ClientBranches)'`, que es la forma de un option set **local** y **no compila**. Corregido por el QA humano 16-ago | ✅ `01` §2.4 (con la salvedad "si IntelliSense difiere, el suyo manda") |
| **H-3** | **Proyección `…DV → colPlanillas`/`colTulas`.** §4 describía las columnas pero nunca daba el `ForAll`, y de él depende el re-keyado de 7.5 | Es una **receta numerada** con una sola definición; la corre el `OnVisible` de esta pantalla y `03` **no** la copia | ✅ `01` **receta 7.10** + contrato `01` §4.3 |
| **H-4** | **Convención de `LocalId`.** §4 decía *"texto, `GUID()` local"* sin decir qué pasa con las filas ya persistidas | Persistida ⇒ `Text(GUID real)`; nueva ⇒ `Text(GUID())` | ✅ `01` §4.1 |
| **H-5** | **Borrado de tulas quitadas del panel.** No había colección de bajas ⇒ tula huérfana en Dataverse | `colTulasBorradasCap` (§7.2 → §8.2 paso 4), y el **paso de bajas** es ahora regla de la receta | ✅ `01` receta 7.5, **regla 5** (calca el paso 4 de §8.2) |
| **H-6** | **Etiqueta del combo de empresa** (*"Empresa · Sucursal"*) y la excepción de `CH-Tbl-Clients` | ⚠️ **La respuesta de la 1.31 (`AddColumns` + `colSucursalesRefCap`) quedó OBSOLETA por L-23**: ese catálogo truncaba a 2000. Hoy: combo **server-side** con `Template Double` sobre `sanic_name` + el espejo `sanic_ClientNameMirror` (**B0-9**), y `CH-Tbl-Clients` **no se agrega** | ✅ `01` §2.2 (espejo B0-9), §2.3 (**D-10**), §2.1 (excepción sin efecto) · §5.4 de esta guía |
| **H-7** | **`Spinner` fantasma.** `00` §2.2 lo permitía; el catálogo cerrado de §0.3 no lo incluía | El catálogo suma **tres** modernos permitidos: Lista de pestañas, Selector de fecha y Spinner. Nada más | ✅ `01` §0.3 · esta pantalla igual **no** usa Spinner por diseño (**L-11**) |
| **H-8** | **`colFallidas` es de una sola operación.** §4 la asignaba a 7.5, pero el borrado de planilla también acumula errores en un `ForAll` | `colFallidas` es de **uso exclusivo del flujo de guardado**; hay una sola operación en vuelo porque el overlay bloquea la UI. Cualquier otro bucle usa su propia colección (`colErrBorrarCap`) | ✅ `01` §4.2 |
| **H-9** | **El ícono de la app no es un medio.** §1.2 subía el PNG como *ícono de aplicación*, que no se puede referenciar desde una fórmula | Son **dos cargas**: el ícono de app (512) y el **medio** `IconoReception192` (desde `docs/marca/icono-reception-192.png`), que es el que va en `imgMarcaCap.Image` y en `imgTileLot.Image` de `03` | ✅ `01` §1.2 + ítem de checklist en `01` §8 |

---

## 11. Divergencias declaradas (para subir al ledger de `00` §5)

`L-1` a `L-5` ya están en `00`. Estas son **nuevas** y nacen en esta pantalla:

| # | Divergencia vs. mockup / `01` | Motivo |
|---|---|---|
| **L-6** | El chip de tulas de la fila muestra **solo el número**, sin el glifo `⛁` | `01` §6.1 prohíbe glifos en el `Text` (tofu en Open Sans) y el catálogo cerrado de iconos no tiene uno de tula |
| **L-7** | El panel de validación rotula **"Planilla ‹N°› — regla"**, no *"Fila N — regla"* | `colProblemas` no guarda índice, y el índice de una colección editable **cambia** al agregar o borrar filas: "Fila 4" apuntaría a otra planilla apenas el usuario corrija algo |
| **L-8** | *(condicional)* Si la proyección de índice de `galPlanillasCap.Items` provoca pérdida de foco al tipear, se cae a `Items = colPlanillas` y la columna `#` queda **vacía** | Estabilidad de entrada > columna cosmética. Se decide **en Studio**, con el ítem de checklist que lo prueba |
| **L-9** | En el panel de tulas, el hueco del mensaje de duplicado está **siempre reservado** (18 px bajo el escáner) en vez de aparecer/desaparecer. *(Reformulada por el rediseño scan-first: el error ya no vive en la fila, vive arriba junto a `inpTulaScanCap`.)* | Si el mensaje empujara la lista, la fila que el operador está mirando se movería sola en medio de una tanda de escaneos |
| **L-10** | El overlay de guardado **no anima** el progreso fila por fila y su barra se pinta una sola vez, con el porcentaje real | El camino feliz es **un solo `Patch` con tabla** — no hay progreso por fila que reportar, y animarlo exigiría un `Repeat true` que sí sería la máquina de estados de la v1. ⚠️ **La segunda mitad de esta divergencia quedó SUPERADA**: el estado "en vuelo" (*Guardando N cambios…*) **ahora SÍ se pinta**, gracias al defer de un frame de `tmrGuardarCap` (§8.4). Ver **L-25** en el ledger de `00` |
| **L-11** | **No hay `Spinner`** en el overlay de guardado: la espera se comunica con la barra en tono suave | El catálogo de `01` §0.3 **ya lo permite** (hueco H-7 cerrado), pero la barra ya cubre el estado y el camino feliz es **un solo `Patch`**: agregar un control más no aporta información. Decisión de diseño, no una limitación — si se quiere el spinner, entra sin tocar `01` |
| **L-12** | El chip de usuario muestra `User().FullName` sin el cargo (*"· Oficial de esclusa"*) | Cero conectores (`01` §1.5): `User()` no expone el puesto y Office365Users está prohibido |
| **L-13** | *"Reintentar fallidas"* re-ejecuta **el guardado completo** en vez de solo las filas de `colFallidas` (receta 7.5 regla 3) | Después del re-keyado del paso 3 el guardado es **idempotente** (cada fila ya tiene GUID ⇒ actualiza, no crea). Duplicar el bloque de 7.5 en la guía es exactamente el código que se pudre. Costo: se re-patchean filas que ya habían entrado |
| **L-14** | El overlay de *Nuevo lote* arranca **debajo del header** (Y `57`) y **no tiene ✕ ni Cancelar** | Es un **gate de estado**, no un diálogo cancelable: sin lote activo no hay nada a qué volver. Y el header debe seguir usable para irse a *Lotes*. Es lo que hace `renderNuevoLote()` en el mockup |
| **L-15** | Los combos (`cmbEmpresaCap`, `cmbTransportadoraCap`) quedan de **esquina viva** | Ya declarado como `L-4` en `00`: el Cuadro combinado clásico no expone `Radius` |
| **L-16** | `lblMetaCap` no pone en **negrita** los números (*"**7 planillas** · **21 tulas**"*) | Una Etiqueta clásica tiene un solo `FontWeight`. Partirla en 5 etiquetas por un matiz tipográfico no paga |
| **L-17** | Los diálogos de **tulas** y **guardado** miden `520` de ancho y el de **salida** `420`, contra los `380` del mockup | En `380` la galería de estado del guardado corta los mensajes del guard a dos líneas por fila, y el footer de **3 botones** de la guarda de salida (*Cancelar* · *Descartar* · *Guardar y salir*) no entra sin apretarse. El resto de los diálogos sí quedan en `380` |
| **L-18** | La grilla de planillas tiene **estado vacío** (`lblVacioGrillaCap`: *"Este lote todavía no tiene planillas. Tocá «Nueva planilla» para empezar."*); el mockup no lo tiene | **Mejora deliberada**: el mockup deja la card en blanco y el operador recién llegado no sabe si la app cargó o si el lote está vacío. Un renglón lo resuelve |
| **L-19** | El overlay de *Nuevo lote* agrega el texto *"El número (RB-…) lo asigna Dataverse."* y la transportadora arranca **sin preselección** (*Crear lote* gris hasta elegir) | **Mejora deliberada**: el mockup preselecciona la última transportadora usada y *Crear* nace habilitado ⇒ el clic apurado crea el lote con la transportadora residual del lote anterior. Sin preselección ese bug no existe. Y decir de dónde sale el `Name` mata la pregunta de "¿por qué no puedo escribir el número?" |
| **L-20** | El segmentado del header arranca en X `210`; con el ancho de contenido del mockup daría ~`172` | `lblTituloCap`/`lblSubtituloCap` usan ancho **fijo `120`**, compartido con `scrLotes`, donde *"Administración de lotes"* lo necesita. Un ancho por pantalla desincronizaría los dos headers, que son el mismo bloque visual |
| **L-21** | Los toasts del mockup (card oscura abajo a la derecha) se materializan como **`Notify()` nativo**: banner de plataforma, arriba, ancho completo. *(También aplica a `03`.)* | Canvas no tiene equivalente nativo **posicionable**. Replicarlo exigiría un contenedor propio + `Timer` por toast, con su propia cola de estado — mucho aparato para mover un mensaje de esquina |
| **L-22** | El panel de tulas **descarta en silencio** las filas con `Numero` en blanco al *Aplicar* (`Filter(colTulasEdit, !IsBlank(Trim(Numero)))`, §7.2) | **El mockup hace exactamente lo mismo** (`applyTulas` filtra los vacíos). ⚠️ Con el panel **scan-first** el caso es hoy casi imposible: `inpTulaScanCap` **ignora el Enter en vacío** y nunca crea una fila sin número. El `Filter` queda como cinturón para las que pudieran llegar de Dataverse. Se declara igual para que el checklist lo pruebe |
| **L-23** | El combo de empresa es **server-side** (`Items` = `Filter('CH-Tbl-ClientBranches', RecordStatus = 'CH-Ch-RecordStatus'.Active)`, §5.4) y **NO hay catálogo local de sucursales** | **QA humano 16-ago**: el entorno tiene ~50 000 sucursales activas y el `ClearCollect` a `colSucursales`/`colSucursalesRefCap` truncaba en `DataRowLimit` (2000) **en silencio** — el operador no encontraba su sucursal. Es el bug que motivó todo el rediseño (`00` §1.2) con ropa nueva. Regla nueva: **D-10** (`01` §2.3). Habilita la búsqueda por empresa el espejo **B0-9** `sanic_ClientNameMirror` |
| **L-24** | Los `OnChange` de los 6 controles de fila hacen **solo** `Set(gvarCambiosSinGuardar, true)`; el valor se baja a `colPlanillas` con el **volcado** (§5.4.a) que corre en el `OnSelect` de 6 botones | **QA humano 16-ago**: un `Patch` a la colección en un `OnChange` muta el `Items` de la galería, la galería **se re-renderiza** y el control **pierde el foco** a mitad de la palabra. No lo arregla `DelayOutput`. Receta `01` §7.11.A |
| **L-25** | El guardado corre **diferido un frame** en `tmrGuardarCap.OnTimerEnd` (§8.4) y **solo sobre el delta** (`colCambiosPlanCap` / `colTulasNuevasCap`), no sobre el lote entero | **QA humano 16-ago**: canvas no repinta a mitad de una fórmula ⇒ el diálogo *"Guardando…"* nunca se veía. El defer de un frame lo hace visible. Y patchear 40 planillas idénticas por click disparaba el `GuardReceptionPlugin` 40 veces para nada. **No resucita el timer de la v1**: corre una vez, sin `Repeat`, sin estado de iteración (`01` §7.11.C, regla 5) |

---

## 12. Checklist de salida

Verificable en Studio, sin interpretación. **Corré `App.OnStart` antes de probar nada**
(botón derecho sobre **App** → *Ejecutar OnStart*).

**Estructura**

- [ ] Los **129 controles** del árbol existen, con **exactamente** estos nombres y en
      **este orden de inserción**. Búsqueda en el árbol: **no queda ningún nombre
      autogenerado** (`Label1`, `Button1`, `ComboBox1`, `Gallery1`…).
- [ ] **Todos** terminan en `Cap` (regla `01` §0.5). Ni uno solo comparte nombre con un
      control de `scrLotes`.
- [ ] Los 6 overlays son los **últimos** del árbol, en el orden A→F, y cada uno tapa
      TODO lo que está debajo (probá abriendo cada uno y haciendo clic en la grilla: no
      pasa nada).
- [ ] `shpFilaCap` está **al FONDO del template**: el texto y los inputs de la fila se
      leen y se pueden tocar (si el overlay quedara arriba, taparía todo).

**Arranque y estado vacío**

- [ ] Con la app recién abierta (sin lote), aparece el **overlay de Nuevo lote** con el
      header visible arriba y el segmentado usable.
- [ ] *Crear lote* está **deshabilitado** hasta elegir transportadora.
- [ ] Al crear, aparece el toast con un `Name` tipo `RB-00042` — **generado por
      Dataverse**, nunca escrito por la app.
- [ ] Después de crear, el chip del header muestra `Name · Transportadora` con el `Name`
      en **semibold** (`lblChipLoteNumCap`) y la transportadora en gris
      (`lblChipLoteTransCap`), la píldora azul **En progreso** pegada a la derecha del
      texto **sin superponerse**, y el overlay desaparece solo.
- [ ] El overlay de *Nuevo lote* es **opaco** (`ThemeBg`): NO se ve la grilla velada
      detrás, se ve la pantalla limpia.
- [ ] Con lote y **sin planillas**: se ve el mensaje vacío de la grilla y los tres
      botones del toolbar están **habilitados**; tocar cada uno tostea su motivo
      (*"No hay planillas que guardar."*, *"El lote no tiene planillas."*) en **rojo**.

**Grilla**

- [ ] *Nueva planilla* agrega una fila; el `#` de la columna izquierda va `1, 2, 3…` y
      **se renumera** al borrar una fila del medio.
- [ ] **PRUEBA DE L-8**: tecleá un número de planilla de 6 dígitos de corrido en la fila
      3. Si el cursor salta o el campo pierde el foco antes de terminar → aplicá el
      escape de §5.3 y anotá **L-8** como *aplicada* en `00`.
- [ ] **PRUEBA DE L-23 (server-side)**: el combo de empresa muestra **dos líneas**
      (sucursal arriba, empresa abajo) y **busca por las dos**. Con el entorno
      sembrado (~50 000 sucursales), tecleá el nombre de una sucursal que **NO** esté
      entre las 2000 primeras por orden alfabético: **tiene que aparecer**. Si no
      aparece, alguien volvió a meter un `ClearCollect` de sucursales. Su lista
      desplegable **no queda recortada** por el borde de la galería (probá en la
      última fila visible).
- [ ] **PRUEBA DE L-24 (foco)**: tecleá los 6 dígitos de un monto **de corrido, sin
      pausas**, en la fila 3, y después tabulá a la siguiente columna. El cursor
      **no** salta, el campo **no** pierde el foco y el valor queda completo. Repetilo
      con el combo de empresa: elegir una sucursal **no** re-renderiza la fila.
- [ ] **PRUEBA DEL TAB (L-24 / `01` §7.11.B)**: parado en el N° de planilla de la fila
      2, tabulá 4 veces: recorrés los 4 montos **de esa misma fila**, no el mismo
      campo de 4 filas distintas. La papelera **no** entra en el recorrido.
- [ ] **PRUEBA DEL VOLCADO**: tecleá un monto y, **sin salir del campo**, tocá
      *Validar*. El total del rail y el panel de validación tienen que reflejar **lo
      recién tecleado**, no el valor anterior.
- [ ] Tipear en un monto **actualiza los 4 totales del rail al instante**, sin apretar
      nada.
- [ ] Los montos muestran separador de miles y 2 decimales (`FmtMoney`): `1,250.00`,
      nunca `.00`. En las tarjetas del rail el valor va **sin símbolo de moneda**: el
      símbolo lo dice la etiqueta de arriba (`CHEQUE C$`), no el número.
- [ ] Dejar un monto vacío lo guarda como **`0`**, no como blanco (verificalo: una fila
      con los 4 vacíos debe disparar la regla *"Todos los montos en 0"*).

**Validación**

- [ ] *Validar* con todo bien → toast verde y el panel dice *"Sin problemas detectados"*.
- [ ] Armá una fila con **empresa sin elegir + sin N° + montos en 0 + sin tulas**:
      *Validar* la pinta de **ámbar** (fila y bordes de sus 6 campos) y el panel lista
      sus **4 problemas** con punto ámbar.
- [ ] Dos filas con el **mismo N° de planilla** → las dos se marcan con *"Número de
      planilla duplicado en el lote"*.
- [ ] La **misma tula** en dos planillas distintas → las dos se marcan con *"Contiene
      tulas duplicadas en el lote"*.
- [ ] El toast cuenta **planillas**, no problemas, y **pluraliza de verdad**: una fila
      con 4 reglas rotas dice *"1 planilla con problemas"*; dos filas rotas dicen
      *"2 planillas con problemas"*. En ningún toast aparece `(s)`.

**Panel de tulas**

- [ ] El chip de una fila **sin tulas** es **ámbar**; con tulas, **azul**, y el número
      coincide con la cantidad real.
- [ ] **El panel abre con el foco YA en `inpTulaScanCap`**: escribís sin tocar el
      mouse. Y el título dice el N° de planilla **que acabás de tipear en la fila**,
      no el anterior (prueba del volcado + controles hermanos, §5.4).
- [ ] **TANDA DE ESCANEO**: escribí `0101` + Enter, `0102` + Enter, `0103` + Enter
      seguidos, sin tocar nada más. Entran las **3**, el input queda vacío después de
      cada una y **nunca perdés el foco**.
- [ ] **Enter en vacío** no crea ninguna fila y no muestra error.
- [ ] **DUP INTRA-planilla**: repetí un número ya escaneado → *"ya está en esta
      planilla"* arriba, la tula **no entra** y el foco sigue en el escáner.
- [ ] **DUP CROSS-planilla**: escaneá en la planilla B una tula que ya está en la
      planilla A del mismo lote → el mensaje dice **el número de la planilla A**
      (*"ya está en la planilla 00860"*), no un genérico.
- [ ] Las filas de la lista son **de solo lectura** (fondo `ThemeSurface2`): no se
      puede tipear encima. Corregir = **papelera + re-escanear**.
- [ ] Abrir el panel, escanear 2 tulas, tocar **✕** → `colTulas` **no cambió** y el
      chip sigue igual. Lo mismo con **Cancelar**.
- [ ] **Aplicar** vuelca los cambios, el chip se actualiza y sale el toast de *"solo en
      memoria — guardá el lote para persistir"*.
- [ ] El chip de tulas **cambia de tono al pasar el mouse** cuando tiene tulas (azul un
      punto más oscuro) y **no cambia** cuando está en ámbar. La papelera de la fila se
      pone **roja** (`ThemeDanger`) al hover.
- [ ] Volver a abrir el panel de otra fila: **no arrastra** las tulas de la anterior ni
      el mensaje de duplicado (Reset al abrir, 7.2 regla 2 + `ctxScanDupCap: ""`).
- [ ] **No existen** `btnAgregarTulaCap` ni `icoAgregarTulaCap` en el árbol: murieron
      con el rediseño scan-first.

**Guardado**

- [ ] *Guardar* con problemas de validación → **no** llama a Dataverse: toast de error y
      el panel se puebla.
- [ ] **PRUEBA DE L-25 (el estado en vuelo SE VE)**: con ~20 planillas modificadas,
      *Guardar* muestra durante un instante el diálogo *"Guardando N cambios…"* con la
      barra a un tercio y **sin botones**, y recién después el resultado. Si nunca lo
      ves, `tmrGuardarCap` no está corriendo: revisá `Start`, `AutoStart`, `Repeat` y
      **que el timer cuelgue de la pantalla, no de un contenedor** (§8.4).
- [ ] **PRUEBA DEL DELTA**: guardá, no toques nada y volvé a *Guardar* → toast
      *"No hay cambios que guardar."*, **sin diálogo** y **sin una sola request** en el
      monitor. Después cambiá **un** monto de **una** fila y guardá: el monitor muestra
      **una** escritura, no N.
- [ ] **PRUEBA DEL RE-KEYADO (bug de la 1.31)**: creá 3 planillas nuevas, guardá,
      cambiá un monto y guardá **otra vez**. En Dataverse tiene que haber **3**
      planillas, no 6. *(Si hay 6, el paso 3 está re-hidratando `colTulasDV` en vez de
      `colPlanillasDV`.)*
- [ ] *Guardar* con todo bien → overlay con la barra **llena**, la lista de planillas con
      punto **verde** y el botón **Listo** (primario, a la derecha). **No hay ✕** en este
      overlay y **no hay ningún `Spinner`**.
- [ ] Con fallidas: el botón de la izquierda dice **Cerrar** en estilo **fantasma** y a
      su derecha aparece *Reintentar fallidas*. Nunca se ven *Listo* y *Cerrar* juntos.
- [ ] **Guardar dos veces seguidas NO duplica registros** en Dataverse (verificá el
      conteo de `CH-Tbl-Receptions` del lote: es el re-keyado del paso 3).
- [ ] Después de un guardado limpio, el segmentado *Lotes* navega **sin** pedir
      confirmación (`gvarCambiosSinGuardar` quedó en `false`).
- [ ] Forzá un rechazo del guard (por ejemplo, tulas repetidas contra otro lote): el
      overlay muestra puntos **rojos** con el mensaje del plugin, aparece *Reintentar
      fallidas*, y `gvarCambiosSinGuardar` sigue en **`true`**. **La UI no marca como
      guardado lo que el guard rechazó** (bug N3 cerrado).
- [ ] *Reintentar fallidas* vuelve a correr y, si ya no falla, deja el overlay en verde.

**Borrado**

- [ ] La papelera de una fila **siempre** abre el diálogo — nunca borra directo.
- [ ] El texto del diálogo dice el número de tulas y agrega *"de Dataverse"* **solo** si
      la fila ya estaba guardada.
- [ ] La **✕ y Cancelar no borran nada**.
- [ ] Borrar una planilla **ya guardada** elimina también **sus tulas de Dataverse**
      (verificá `CH-Tbl-Bags`: cero huérfanas — bug N2 cerrado).
- [ ] Si Dataverse rechaza el borrado, la fila **sigue en pantalla** y el toast lo dice.

**Cierre y navegación**

- [ ] *Cerrar lote* con problemas → avisa el motivo y **no** abre el diálogo.
- [ ] *Cerrar lote* con todo bien → diálogo de confirmación; **Cancelar y ✕ no cierran
      nada**.
- [ ] Confirmar: guarda, el lote pasa a **Recibido** (159460001) y la pantalla vuelve
      sola al **overlay de Nuevo lote** con todas las colecciones vacías.
- [ ] **Cerrar un lote RECIÉN GUARDADO** (sin cambios pendientes) **igual lo cierra**:
      es el caso borde de §9.2 — la rama "sin cambios" tiene que prender el timer
      cuando hay continuación pendiente. Si el toast dice *"No hay cambios que
      guardar."* y el lote **sigue En progreso**, falta esa línea.
- [ ] Si el guardado del cierre deja fallidas, **el lote NO se cierra** y el toast lo
      dice. *(Prueba de que la transición vive en el paso 8a del `OnTimerEnd` y no
      después del `Select`: si estuviera en el `OnSelect`, `colFallidas` se leería
      vacía y el lote se cerraría igual.)*
- [ ] Con cambios sin guardar, tocar *Lotes* abre el diálogo de 3 botones.
      **Descartar** navega y limpia el flag; **Cancelar y ✕** se quedan en Captura.
- [ ] **PRUEBA DE "Guardar y salir"**: con cambios pendientes, tocá *Lotes* →
      *Guardar y salir*. La app **guarda de verdad** (verificá el lote en Dataverse) y
      **después** navega. Si navega sin guardar, el `Navigate` quedó en el `OnSelect`
      del botón en vez del paso 9 del `OnTimerEnd`: **los timers no corren en pantallas
      no visibles** (§8.4).
- [ ] Cerrar la pestaña del navegador con cambios pendientes dispara el aviso de
      `App.ConfirmExit`.

**Fidelidad y calidad**

- [ ] Las píldoras del header usan los colores del `Switch` de la receta 7.4 y ninguna
      recorta texto.
- [ ] Ninguna etiqueta con `Wrap true` recorta su texto (panel de validación, textos de
      los 6 diálogos, estado vacío).
- [ ] Ningún separador de fila se superpone con el contenido de la fila de abajo.
- [ ] Ninguna fecha de la pantalla usa `hh` (12 h sin AM/PM): todas van con `FmtFecha`.
- [ ] Los **9 controles de §10.2-9** tienen su `AccessibleLabel` cargado: `imgMarcaCap`,
      los 4 iconos (`icoValidarCap`, `icoNuevaPlanillaCap`, `icoBorrarFilaCap`,
      `icoBorrarTulaEditCap`) y las 4 ✕ (`icoDlgXTulasCap`, `icoDlgXBorrarFilaCap`,
      `icoDlgXCerrarCap`, `icoDlgXSalirCap`). Ninguno en blanco.
- [ ] **App Checker: cero errores y cero advertencias de delegación.** Si aparece el
      triángulo azul, alguien metió un `Filter`/`LookUp`/`CountRows` sobre un datasource
      donde va una colección local — cruzá contra el inventario cerrado de §2. El
      `Items` server-side de `cmbEmpresaCap` (**Q1**) es delegable: **no** debe
      producir warning.
- [ ] Búsqueda en el YAML desempaquetado sobre `Src/scrCaptura.fx.yaml`: **cero
      resultados** para
      `rg -n 'RemoveIf\(|Refresh\(|hh:|Patch\(colPlanillas, ThisItem|colSucursales|AddColumns|Filter\(col\w+, false\)'`.
- [ ] **Cero igualdades de registro peladas**:
      `rg -n 'ReceptionBatch = |ReceptionBatchId = |ReceptionID = ' Src/scrCaptura.fx.yaml`
      da **cero**. Toda comparación por lookup es **PK contra PK** (`01` §2.3, D-1).
- [ ] **Cero `Patch` en los `OnChange` del template**: los 6 controles editables de
      `galPlanillasCap` muestran **solo** `Set(gvarCambiosSinGuardar, true)` (**L-24**).
- [ ] **`tmrGuardarCap` existe, cuelga de `scrCaptura` (NO de un contenedor), y tiene
      `AutoStart: false`, `Repeat: false`, `Duration: 50`, `Start: ctxGuardandoCap`.**
      `rg -n 'Repeat: true' Src/scrCaptura.fx.yaml` da **cero** — un `Repeat true`
      sería el timer de la v1 volviendo (`01` §7.11.C, regla 4).
