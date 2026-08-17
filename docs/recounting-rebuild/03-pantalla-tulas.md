# Guía 03 — scrTulas (el hub de la sesión)

Pantalla **hub** de CH-App-Recounting v2. Reemplaza al panel lateral + el
`container Receipt_MoneyBagSelecction` de la v1 (la máquina de estados de
`colSecurityBagSelectionState` con sus 4 rutas de apertura, de las cuales **una
sola** creaba el `CH-Tbl-RecountingSessionDetails`, sus 12 `Sum` de 2 saltos y su
`ClearCollect` auto-referencial adentro de un `Concurrent`).

Acá: **rail de KPIs de sesión** (solo tulas `Recontada`), **barra scan-first**
(foco al entrar, Enter dispara, el lector de código de barras no necesita mouse),
**lista de tulas de la sesión** con fila clicable para retomar, y la **resolución
del número escaneado** con sus tres diálogos de desambiguación (`B1`, `B2`, `B3`),
su espera de aprobación y su **rechazo bloqueante**.

**CONTRATO DE ENTRADA** — lo setea `scrSesion` (`02`) antes de `Navigate(scrTulas)`,
y es **copia literal del contrato canónico de `01` §4.5**:

| Global | Qué tiene que traer | Quién la setea | Si viene en blanco |
|---|---|---|---|
| `gvarUser` | Registro de `Users` del cajero | `App.OnStart` (`01` §3.2) | la app está bloqueada por el overlay de identidad de `02`; `03` no se llega a ver |
| `gvarIdentidadOk` | `true` | `App.OnStart` | ídem |
| `gvarSesion` | Registro de `CH-Tbl-RecountingSessions` de **hoy**, ya confirmada por el servidor | `02` | **guarda de `§1`**: `Select(btnIrSesionTul)` ⇒ `Notify` + `Navigate(scrSesion)` en el motor (§1.1, `L-65`) |
| `gvarMesa` | `gvarSesion.CountingDesk` | `02` | chip de mesa vacío y `MachineSerialNumber` sin resolver para `04` |
| `gvarTula` · `gvarSesionDetalle` · `gvarDeclaredBag` · `gvarSolicitud` | **`Blank()`** | `02` | `03` los vuelve a poner en `Blank()` en su `OnVisible` (§1) |

Lo demás que consume son **de `App.Formulas`** (`01` §3.1): la paleta (`Theme*`), los
formatos (`FmtMoney`, `FmtFecha`, `FmtFechaCorta`, `FmtHora`), `UsuarioNombre`,
`UsuarioIniciales`, `SimboloNIO/USD/EUR`, `fxMonto()` y `fxHtmlSinDeclaracion()`.

**CONTRATO DE SALIDA hacia `scrConteo` (04)**: §8 — tabla completa, para las
**cuatro** rutas de apertura y para el retome.

**Variables y colecciones propias**. Globales: **ninguna nueva** — `03` solo escribe
las que `01` §4.1 ya inventaría (`gvarTula`, `gvarSesionDetalle`, `gvarDeclaredBag`,
`gvarSolicitud`, `gvarDepositoSinGuardar`). Colecciones: las **cuatro** de `01` §4.2
(`colTulasSesionTul`, `colDepositosSesionTul`, `colTulasCandidatasTul`,
`colDeclaradasCandidatasTul`) y **ninguna más**. Contexto local (todo `ctx…Tul`):

| `ctx` | Tipo | Para qué |
|---|---|---|
| `ctxScanTul` | Texto | el número escaneado ya `Trim()`-eado. **Se lee de acá, nunca de `inpScanTul.Text`**, porque el input se resetea en el medio del flujo |
| `ctxTulaElegidaTul` | Registro de `CH-Tbl-Bags` | la tula que se está abriendo (o retomando). `Defaults(…)` = ninguna |
| `ctxDeclElegidaTul` | Registro de `CH-Tbl-DeclaredBags` | la declaración resuelta. `Defaults(…)` = **ruta sin declaración** |
| `ctxPickTulaTul` | Registro de `CH-Tbl-Bags` | centinela de selección **dentro** de `B1` (receta 7.3, excepción del centinela) |
| `ctxPickDeclTul` | Registro de `CH-Tbl-DeclaredBags` | ídem para `B2` |
| `ctxTulaFrescaTul` | Registro de `CH-Tbl-Bags` | la re-lectura por PK del guard de apertura (§6.3) |
| `ctxDetalleTul` | Registro de `CH-Tbl-RecountingSessionDetails` | el detalle existente, si lo hay (§7) |
| `ctxSolicitudPrevTul` | Registro de `CH-Tbl-ApprovalRequests` | la solicitud previa de esta tula, para **no duplicarla** (§6.2) |
| `ctxDlgTulaTul` · `ctxDlgDeclTul` · `ctxDlgSinDecTul` | Booleanos | visibilidad de `B1` / `B2` / `B3` |
| `ctxEsperandoTul` · `ctxRechazadaTul` | Booleanos | overlay de espera / overlay de rechazo (`01` §5.8) |

> **Regla de coordenadas de esta guía** (`01` §0.7): el `X`/`Y` de un control que vive
> dentro del template de una galería es **relativo al template**, que arranca en `0`.
> Se enuncia una sola vez: **X del control del template = X del encabezado − X de la
> galería**. Las galerías de esta pantalla arrancan en `X = 270` (lista de tulas) y
> `X = 22` relativo a su card (las dos galerías de *pick cards* de `B1`/`B2`).

> **Lienzo**: 1366 × 768 con *Escalar para ajustar* activado (`01` §1.3) ⇒ 1 unidad =
> 1 px del mockup. Todas las coordenadas de abajo son absolutas salvo las de templates.

> 🧨 **Los tres componentes de `01` §8 ya tienen que existir** (regla `01` §0.12). Esta
> pantalla instancia `cmpKpiSesion` **tres veces**; si el componente no existe, el árbol
> no se puede armar.

---

## Árbol de controles

Orden **back → front**: el primero de cada nivel queda al FONDO del z-order y el último
arriba. Ese orden ES el orden de inserción (`01` §0.6 y §0.11: construí en **DOS pases**
— primero insertar y renombrar TODO, después llenar propiedades).

```
scrTulas
│   ── RAIL (250 px) ──
├─ shpRailTul                        (Rectángulo — fondo blanco del rail)
├─ shpRailLineaTul                   (Rectángulo — 1 px de borde derecho)
├─ btnMarcaTul                       (Botón clásico — tile "C$", receta 7.1)
├─ lblTituloTul
├─ lblSubtituloTul
├─ shpRailBrandLineaTul              (Rectángulo)
├─ btnChipUsuarioTul                 (receta 7.1)
├─ btnAvatarUsuarioTul               (receta 7.1, con texto)
├─ lblUsuarioTul
├─ btnChipMesaTul                    (receta 7.1)
├─ lblMesaTul
├─ lblRailLabTul
├─ cmpKpiNIOTul                      (Componente canvas — cmpKpiSesion, 01 §8.2)
├─ cmpKpiUSDTul                      (Componente canvas — cmpKpiSesion, 01 §8.2)
├─ cmpKpiEURTul                      (Componente canvas — cmpKpiSesion, 01 §8.2)
├─ shpRailFootLineaTul               (Rectángulo)
├─ lblFootFechaTul
├─ lblFootContadorTul
├─ lblFootNotaTul
│   ── BARRA SCAN-FIRST (receta 01 §7.8) ──
├─ btnScanSupTul
├─ lblScanTitTul
├─ btnScanCajaTul
├─ icoScanTul
├─ inpScanTul
├─ btnAbrirTul
├─ lblScanNotaTul
│   ── LISTA DE TULAS DE LA SESIÓN ──
├─ btnCardListaTul                   (receta 7.1)
├─ lblPanelTitTul
├─ lblPanelSubTul
├─ btnActualizarTul
├─ icoActualizarTul
├─ shpPanelLineaTul                  (Rectángulo)
├─ shpHeadListaTul                   (Rectángulo — banda del encabezado)
├─ lblHeadTulaTul
├─ lblHeadPlanillaTul
├─ lblHeadClienteTul
├─ lblHeadHoraTul
├─ lblHeadEstadoTul
├─ shpHeadLineaTul                   (Rectángulo)
├─ galTulasTul                       (Galería vertical en blanco — receta 7.3)
│  ├─ shpSelTul                      (Botón clásico — superficie de fila, al FONDO)
│  ├─ shpSelBarraTul                 (Rectángulo)
│  ├─ lblCelTulaTul
│  ├─ lblCelPlanillaTul
│  ├─ lblCelClienteTul
│  ├─ lblCelHoraTul
│  ├─ pilCelEstadoTul                (píldora, receta 7.4)
│  ├─ icoCelChevronTul
│  └─ shpSepTul                      (Rectángulo)
├─ lblVacioTitTul
├─ lblVacioTxtTul
│   ── MOTORES (botones de 1×1, D03-9) ──
├─ btnCargarSesionTul
├─ btnResolverDeclaracionTul
├─ btnAbrirNuevaTul
├─ btnReabrirTul
├─ btnEntrarConteoTul
├─ btnIrSesionTul                    (motor de SALIDA — §1.1, L-65)
├─ tmrPollTul                        (Temporizador clásico — receta 7.7)
│   ── DIÁLOGOS (tope del z-order, en este orden) ──
├─ cntDlgTulaTul                     B1 (receta 7.2)
│  ├─ btnBloqTulaTul
│  └─ cntCardTulaTul
│     ├─ lblDlgTitTulaTul
│     ├─ lblDlgTxtTulaTul
│     ├─ icoDlgXTulaTul
│     ├─ galPickTulasTul
│     │  ├─ btnPickTulaTul            (superficie de la pick card, al FONDO)
│     │  ├─ lblPickTulaTitTul
│     │  ├─ lblPickTulaMetaTul
│     │  ├─ lblPickTulaMotivoTul
│     │  └─ pilPickTulaTul            (píldora, receta 7.4)
│     ├─ btnAvisoTulaTul              (banner, receta 7.2.a)
│     ├─ icoAvisoTulaTul
│     ├─ lblAvisoTulaTul
│     ├─ btnDlgCancelTulaTul
│     └─ btnDlgOkTulaTul
├─ cntDlgDeclTul                     B2 (receta 7.2)
│  ├─ btnBloqDeclTul
│  └─ cntCardDeclTul
│     ├─ lblDlgTitDeclTul
│     ├─ lblDlgTxtDeclTul
│     ├─ icoDlgXDeclTul
│     ├─ galPickDeclTul
│     │  ├─ btnPickDeclTul            (superficie de la pick card, al FONDO)
│     │  ├─ lblPickDeclTitTul
│     │  ├─ lblPickDeclMetaTul
│     │  ├─ lblPickDeclMontoTul
│     │  └─ lblPickDeclMonto2Tul
│     ├─ btnAvisoDeclTul              (banner, receta 7.2.a)
│     ├─ icoAvisoDeclTul
│     ├─ lblAvisoDeclTul
│     ├─ btnDlgCancelDeclTul
│     └─ btnDlgOkDeclTul
├─ cntDlgSinDecTul                   B3 (receta 7.2 + 7.2.a + 7.2.b)
│  ├─ btnBloqSinDecTul
│  └─ cntCardSinDecTul
│     ├─ btnIcoSinDecTul
│     ├─ icoTipoSinDecTul
│     ├─ lblDlgTitSinDecTul
│     ├─ lblDlgTxtSinDecTul
│     ├─ icoDlgXSinDecTul
│     ├─ lblKvTulaEtqSinDecTul
│     ├─ lblKvTulaSinDecTul
│     ├─ lblKvPlanEtqSinDecTul
│     ├─ lblKvPlanSinDecTul
│     ├─ lblKvCliEtqSinDecTul
│     ├─ lblKvCliSinDecTul
│     ├─ lblKvMesaEtqSinDecTul
│     ├─ lblKvMesaSinDecTul
│     ├─ btnAvisoSinDecTul
│     ├─ icoAvisoSinDecTul
│     ├─ lblAvisoSinDecTul
│     ├─ btnDlgCancelSinDecTul
│     └─ btnDlgOkSinDecTul
├─ cntEsperaTul                      espera de aprobación (receta 7.2 + 7.7 + 7.2.b)
│  ├─ btnBloqEsperaTul
│  └─ cntCardEsperaTul
│     ├─ lblDlgTitEsperaTul
│     ├─ lblDlgTxtEsperaTul
│     ├─ btnWaitRowTul                (receta 7.1)
│     ├─ spnEsperaTul                 (Spinner MODERNO)
│     ├─ lblWaitTitTul
│     ├─ lblWaitSubTul
│     ├─ lblKvTulaEtqEsperaTul
│     ├─ lblKvTulaEsperaTul
│     ├─ lblKvPlanEtqEsperaTul
│     ├─ lblKvPlanEsperaTul
│     ├─ lblKvCliEtqEsperaTul
│     ├─ lblKvCliEsperaTul
│     └─ btnDlgCancelEsperaTul
└─ cntRechazoTul                     rechazo (receta 7.2 + 7.2.a — TOPE absoluto)
   ├─ btnBloqRechazoTul
   └─ cntCardRechazoTul
      ├─ btnIcoRechazoTul
      ├─ icoTipoRechazoTul
      ├─ lblDlgTitRechazoTul
      ├─ lblDlgTxtRechazoTul
      ├─ icoDlgXRechazoTul
      ├─ btnAvisoRechazoTul
      ├─ icoAvisoRechazoTul
      ├─ lblAvisoRechazoTul
      ├─ lblMotivoRechazoTul
      ├─ lblNotaRechazoTul
      └─ btnDlgOkRechazoTul
```

**143 controles**: 58 de pantalla (19 del rail + 7 de la barra scan + 25 de la lista,
templates incluidos + **6 motores + 1 temporizador**) y 85 repartidos en los cinco diálogos
(17 + 17 + 21 + 16 + 14). Todos con sufijo **`Tul`** (regla `01` §0.5).

> **Los motores son SEIS, con dos papeles distintos**: los **cinco de la apertura** (§0.1,
> §6) y **`btnIrSesionTul`**, el **motor de salida** (§1.1), que existe por una razón
> distinta —`Navigate()` está prohibido en un `OnVisible` (`L-65`)— y no participa de la
> cadena de apertura. Donde esta guía dice *"los cinco motores"* habla de la cadena.

---

## 0. Las tres reglas transversales de esta pantalla

### 0.1 Una sola definición de la apertura — los cinco motores

La v1 copiaba la lógica de apertura en **cuatro** `OnSelect` distintos
(`btn IngresarTula`, `btn EscogerTulaEsclusa`, `btn EscogerTulaDeclarada`,
`btn EscogerTulaDeclarada_1`) y por eso el `CH-Tbl-RecountingSessionDetails` se creaba
**en uno solo**. En la v2 la apertura está **factorizada en cinco motores** y cada paso
existe **una vez**:

```
inpScanTul.OnChange ─┐
                     ├─► btnAbrirTul ──► (A) sin candidatas      → Notify
galTulasTul.OnSelect ─┼─►               (B) homónimas           → B1 ─┐
                      │                 (C) ya abierta por mí   → btnReabrirTul ─┐
                      │                 (D) no está en bóveda   → Notify         │
                      │                 (E) una en bóveda       → btnResolverDeclaracionTul
                      │                                                          │
                      │  btnResolverDeclaracionTul ─ 0 decl. → B3 → solicitud → tmrPollTul ─┐
                      │                            ─ >1 decl. → B2 ─┐                       │
                      │                            ─ 1 decl.  ──────┼───────────────────────┤
                      │                                             ▼                       ▼
                      └────────────────────────────────────►  btnAbrirNuevaTul  ◄────────────
                                                                     │
                                                                     ▼
                                                            btnEntrarConteoTul
                                                    (detalle de sesión + contrato 03→04)
```

- **`btnEntrarConteoTul` es el ÚNICO lugar** donde nace el
  `CH-Tbl-RecountingSessionDetails` y el único que hace `Navigate(scrConteo)`. Las
  **cuatro rutas** de apertura y el **retome** pasan por él, sin excepción (`01` §2.4).
- **`btnAbrirNuevaTul` es el ÚNICO lugar** que hace el `Patch` de la bolsa y el enlace
  de la declaración.
- Si en esta guía ves un `Patch('CH-Tbl-Bags', …)` o un `Navigate(scrConteo)` fuera de
  esos dos botones, es un **bug de la guía**.

### 0.2 Estados vacíos = colección vacía, y "sin selección" se lee por PK

Las galerías clásicas **auto-seleccionan la primera fila** (`01` §7.3). Por eso:

- El estado vacío de la lista (`lblVacioTitTul` / `lblVacioTxtTul`, mockup `#b0-vacio`)
  se dispara con **`IsEmpty(colTulasSesionTul)`** y con nada más.
- Las dos galerías de *pick cards* (`B1`, `B2`) **no confían en `Selected`**: usan los
  centinelas `ctxPickTulaTul` / `ctxPickDeclTul`, inicializados en `Defaults(<tabla>)`
  (es la **excepción declarada** de la receta 7.3, y acá está justificada: elegir la
  tula o la declaración equivocada arruina el arqueo entero de la tula).
- 🧨 **"No hay selección" se escribe SIEMPRE contra la PK**:
  `IsBlank(ctxPickTulaTul.'CH-Tbl-Bags')`, nunca `IsBlank(ctxPickTulaTul)`. Con un
  registro tipado vacío, `IsBlank(<registro>)` puede dar `false`.

### 0.3 Todo lo que escribe Dataverse arranca con `gvarIdentidadOk`

`01` §3.2 lo pide y acá son **cuatro** botones: `btnAbrirTul`, `btnDlgOkTulaTul`,
`btnDlgOkDeclTul` y `btnDlgOkSinDecTul`. Los **motores**
NO llevan la guarda: son botones de 1×1 invocados con `Select()`, y un
`DisplayMode.Disabled` los volvería inertes en silencio, cortando la cadena a la mitad.
La guarda vive donde el usuario toca.

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `LoadingSpinnerColor` | `ThemeAccent` |
| `OnVisible` | ver abajo — COMPLETO, listo para pegar |

```powerfx
// ===== 0. GUARDA DE SESIÓN — 03 no existe sin sesión resuelta =====
//   🧨 Los bloques 1, 2 y 3 viven en el ELSE de esta guarda: sin sesión no se toca
//      el estado local ni se dispara UNA sola consulta. Ni `Navigate()` ni
//      `Select(<motor que navega>)` cortan la fórmula (01 §7.10), así que sin el
//      else las dos consultas de btnCargarSesionTul se disparaban igual, filtrando
//      por una PK vacía.
//   🧨 NUNCA Navigate() acá: el App Checker lo marca ("would automatically always
//      navigate away"). La salida vive en el motor btnIrSesionTul (§1.1, L-65).
If(
    IsBlank(gvarSesion.'CH-Tbl-RecountingSession'),

    Select(btnIrSesionTul),

    // ===== 1. ESTADO LOCAL — todo el contexto de la pantalla, de una =====
    UpdateContext(
        {
            ctxScanTul:          "",
            ctxTulaElegidaTul:   Defaults('CH-Tbl-Bags'),
            ctxDeclElegidaTul:   Defaults('CH-Tbl-DeclaredBags'),
            ctxPickTulaTul:      Defaults('CH-Tbl-Bags'),
            ctxPickDeclTul:      Defaults('CH-Tbl-DeclaredBags'),
            ctxTulaFrescaTul:    Defaults('CH-Tbl-Bags'),
            ctxDetalleTul:       Defaults('CH-Tbl-RecountingSessionDetails'),
            ctxSolicitudPrevTul: Defaults('CH-Tbl-ApprovalRequests'),
            ctxDlgTulaTul:       false,
            ctxDlgDeclTul:       false,
            ctxDlgSinDecTul:     false,
            ctxEsperandoTul:     false,
            ctxRechazadaTul:     false
        }
    );

    // ===== 2. GLOBALES DE TULA — el hub no tiene tula abierta =====
    Set(gvarTula, Blank());
    Set(gvarSesionDetalle, Blank());
    Set(gvarDeclaredBag, Blank());
    Set(gvarSolicitud, Blank());

    // ===== 3. DATOS DE LA SESIÓN (una consulta delegable por universo) =====
    //   El `Concurrent` de las dos consultas vive INTACTO dentro del motor
    //   btnCargarSesionTul (§5.2): acá solo se lo invoca.
    Clear(colTulasCandidatasTul);
    Clear(colDeclaradasCandidatasTul);
    Select(btnCargarSesionTul)
);

// ===== 4. UI: la galería y el input NO arrastran la visita anterior =====
//   Va FUERA de la guarda, siempre: son tres llamadas locales sin costo, y dejar el
//   input limpio y con foco también es lo correcto en el camino de salida.
Reset(galTulasTul);
Reset(inpScanTul);
SetFocus(inpScanTul)
```

> **Por qué así**:
> - 🧨 **La guarda de sesión gatea TODO lo que consulta o escribe estado.** Ni `Navigate()`
>   ni `Select(<motor que navega>)` **cortan la fórmula** (`01` §7.10 / `02` §12.2-1): sin
>   el `else`, los bloques 1-3 corrían igual y las dos consultas de `btnCargarSesionTul`
>   (§5.2) se disparaban filtrando por una **PK vacía** — dos viajes a la red garantizados
>   a devolver cero filas, en el único camino en el que la pantalla ya sabe que se va.
> - 🧨 **La rama de salida es UNA sola instrucción: `Select(btnIrSesionTul)`.** El `Notify`
>   **y** el `Navigate` viven en el `OnSelect` del motor (§1.1), no acá: un `OnVisible` con
>   `Navigate()` lo marca el App Checker (`L-65`). El cajero igual se entera —el motor
>   avisa antes de navegar—, pero el aviso tiene **un** lugar.
> - **Los `ctx…` de registro se siembran con `Defaults(<tabla>)`, nunca con `Blank()`**
>   (mismo truco que `01` §3.2): `Defaults(ds)` da un registro **TIPADO** sin consultar
>   nada. Con un `Blank()` pelado, toda referencia a `ctxTulaElegidaTul.BagID` queda en
>   rojo en tiempo de diseño.
> - 🧨 **`Set(gvarSolicitud, Blank())` no es cosmético.** `gvarSolicitud` es **un solo
>   global para tres pantallas** (`01` §4.1). Si `03` entrara con la solicitud que
>   dejó `02` o `04`, su `tmrPollTul` (§9) la adoptaría y correría la rama *Aprobado*
>   de **otra** pantalla. Limpiarlo al entrar es lo que hace que cada pantalla espere
>   **solo lo suyo**. Divergencia **D03-10**.
> - **El estado de una galería PERSISTE entre visitas** (gotcha de canvas): sin
>   `Reset(galTulasTul)`, volvés de `scrConteo` y la lista te deja parado en la tula
>   anterior, que además puede ya no estar en la colección.
> - **`SetFocus(inpScanTul)` es la ÚLTIMA línea** (receta `01` §7.8, regla 2). Apunta a
>   un control **de pantalla**; `SetFocus` sobre un control de template no está
>   soportado.
> - **Cero `Refresh()` acá** (regla D-5): las dos consultas de `btnCargarSesionTul` son
>   `ClearCollect`, y un `ClearCollect` **siempre** va al servidor. El `Refresh` existe
>   una sola vez en la pantalla, en el ⟳ (§5.2), que es la forma 1 que la regla declara.

### 1.1 `btnIrSesionTul` — el motor de SALIDA (sexto motor)

🧨 **`Navigate()` está PROHIBIDO en un `OnVisible`**: el App Checker lo marca como
*"would automatically always navigate away"*. La salida de la guarda de sesión pasa al
**patrón motor** de la casa (`L-53`): el `OnVisible` **invoca**, el motor **hace**. Ledger
**`L-65`**. Es el **sexto** motor de `03` y el único que **no** pertenece a la cadena de
apertura de §0.1.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnIrSesionTul` | Botón clásico — **motor 1 × 1** (`D03-9`, `L-53`) | X `10` · **Y `767`** · Width `1` · Height `1` · **`Visible` `true`** · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · `Text` `""` · `TabIndex` `-1` · **`DisplayMode` `DisplayMode.Edit`** (sin condición) · `AccessibleLabel` `""` · `OnSelect` ver abajo |

```powerfx
// ====================== btnIrSesionTul.OnSelect ======================
Notify("No hay una sesión activa.", NotificationType.Warning);
Navigate(scrSesion)
```

> **Llamador (lista CERRADA, 1)**: `scrTulas.OnVisible`, rama *sin sesión* (§1). Si aparece
> un segundo llamador, es un bug de la guía.

> 🧨 **No lleva la guarda `gvarIdentidadOk`** (§0.3): es un motor invocado con `Select()`,
> y un `DisplayMode.Disabled` lo volvería inerte **en silencio** — el cajero quedaría
> atrapado en un hub sin sesión. Misma regla que los otros cinco.

> **`Visible: true` y transparente, jamás `Visible: false`** (`D03-9`, `L-53`).

---

## 2. Rail izquierdo (250 px)

Mockup `#b0`, clase `.rail`: ancho `250`, `padding: 14px 12px`, `gap: 9px`, borde
derecho de 1 px. ⇒ **columna de contenido: `X = 12`, `Width = 226`**, que es
exactamente el ancho por defecto de `cmpKpiSesion` (`01` §8.2).

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `shpRailTul` | Rectángulo clásico | `0` · `0` · `250` · `768` | `Fill` `ThemeSurface` |
| `shpRailLineaTul` | Rectángulo clásico | `249` · `0` · `1` · `768` | `Fill` `ThemeLine` |
| `btnMarcaTul` | Botón clásico (receta 7.1, con texto) | `16` · `14` · `34` · `34` | `Radius` (las 4) `9` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccent` · `Color` `ThemeSurface` · `BorderThickness` `0` · Size `10.5` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `Text` `"C$"` · `DisplayMode` `DisplayMode.View` |
| `lblTituloTul` | Etiqueta clásica | `60` · `14` · `170` · `18` | `ESTILO-TITULO-APP` · `Text` `"Reconteo"` |
| `lblSubtituloTul` | Etiqueta clásica | `60` · `32` · `170` · `15` | `ESTILO-SUBTITULO-APP` · `Text` `"Sesión de conteo"` |
| `shpRailBrandLineaTul` | Rectángulo clásico | `16` · `58` · `218` · `1` | `Fill` `ThemeLineSoft` |
| `btnChipUsuarioTul` | Botón clásico (receta 7.1) | `12` · `68` · `226` · `34` | `Radius` (las 4) `17` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `btnAvatarUsuarioTul` | Botón clásico (receta 7.1, con texto) | `17` · `72` · `26` · `26` | `Radius` (las 4) `13` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccentSoft` · `Color` `ThemeAccent` · `BorderThickness` `0` · Size `7.88` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `Text` `UsuarioIniciales` · `DisplayMode` `DisplayMode.View` |
| `lblUsuarioTul` | Etiqueta clásica | `51` · `68` · `175` · `34` | Size `8.63` · `Color` `ThemeInk2` · `Text` `UsuarioNombre` |
| `btnChipMesaTul` | Botón clásico (receta 7.1) | `12` · `111` · `226` · `30` | `Radius` (las 4) `15` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `lblMesaTul` | Etiqueta clásica | `12` · `111` · `226` · `30` | Size `9` · `Color` `ThemeInk2` · `Align` `Align.Center` · `Text` `gvarMesa.Name` |
| `lblRailLabTul` | Etiqueta clásica | `16` · `150` · `218` · `18` | `ESTILO-HEADER-COL` · `Color` `ThemeInk3` · `Text` `"TOTALES DE LA SESIÓN"` (en MAYÚSCULAS en el `Text`: `text-transform` es CSS y la etiqueta clásica no lo tiene) |
| `cmpKpiNIOTul` | **Componente canvas** `cmpKpiSesion` (`01` §8.2) | `12` · `177` · `226` · **`100`** | propiedades en **§3** |
| `cmpKpiUSDTul` | **Componente canvas** `cmpKpiSesion` (`01` §8.2) | `12` · `cmpKpiNIOTul.Y + cmpKpiNIOTul.Height + 9` · `226` · **`100`** | propiedades en **§3** |
| `cmpKpiEURTul` | **Componente canvas** `cmpKpiSesion` (`01` §8.2) | `12` · `cmpKpiUSDTul.Y + cmpKpiUSDTul.Height + 9` · `226` · **`100`** | propiedades en **§3** |
| `shpRailFootLineaTul` | Rectángulo clásico | `16` · `692` · `218` · `1` | `Fill` `ThemeLineSoft` |
| `lblFootFechaTul` | Etiqueta clásica | `16` · `701` · `218` · `17` | `ESTILO-META-CHICA` · `Color` **`ThemeInk2`** · `Text` ver abajo |
| `lblFootContadorTul` | Etiqueta clásica | `16` · `lblFootFechaTul.Y + lblFootFechaTul.Height` · `218` · `17` | `ESTILO-META-CHICA` · `Text` ver abajo |
| `lblFootNotaTul` | Etiqueta clásica | `16` · `lblFootContadorTul.Y + lblFootContadorTul.Height` · `218` · `17` | `ESTILO-META-CHICA` · `Text` `"Los totales incluyen solo tulas recontadas."` |

**`lblFootFechaTul.Text`:**

```powerfx
Text(gvarSesion.'Created On', FmtFechaCorta) & "  ·  " & gvarMesa.Name
```

**`lblFootContadorTul.Text`:**

```powerfx
CountRows(colTulasSesionTul) & " tulas aperturadas · " &
CountRows(Filter(colTulasSesionTul, BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada)) &
" recontadas"
```

> **Los dos `CountRows` van sobre la COLECCIÓN local** (regla D-2). Sobre el datasource
> serían dos consultas no delegables que devolverían el conteo de las primeras 500 filas.
>
> **El pie son TRES etiquetas, no una con `<br>`.** El mockup mezcla pesos dentro de la
> primera línea (`<b>16 ago 2026</b> · Mesa 03`); una Etiqueta clásica tiene **un solo**
> `FontWeight`, así que la línea entera va en `ThemeInk2` y las otras dos en `ThemeInk3`.
> Divergencia **D03-13**, junto con el formato de fecha.
>
> **El chip de mesa muestra `gvarMesa.Name` y nada más.** El `· Bóveda B` del mockup es
> una ubicación que **no existe** en `CH-Tbl-CountingDesks` (`Name`, `Cajero`,
> `MachineSerialNumber`, `Enable` — `01` §2.4). Divergencia **D03-7**.

---

## 3. Los tres `cmpKpiSesion` del rail

`cmpKpiSesion` (`01` §8.2) fija **todas** sus propiedades internas: acá van solo las
**cinco entradas** de cada instancia. `Total` es una **salida** del componente
(`Efectivo + Cheques`) y no se escribe.

| Instancia | `Moneda` | `MonedaNombre` | `Simbolo` | `Efectivo` | `Cheques` |
|---|---|---|---|---|---|
| `cmpKpiNIOTul` | `"NIO"` | `"Córdobas"` | `SimboloNIO` | **EFECTIVO(NIO)** | **CHEQUES(NIO)** |
| `cmpKpiUSDTul` | `"USD"` | `"Dólares"` | `SimboloUSD` | **EFECTIVO(USD)** | **CHEQUES(USD)** |
| `cmpKpiEURTul` | `"EUR"` | `"Euros"` | `SimboloEUR` | **EFECTIVO(EUR)** | **CHEQUES(EUR)** |

**EFECTIVO(`<MON>`)** — copiá esto en `cmpKpi<MON>Tul.Efectivo` cambiando **solo** el
literal de moneda:

```powerfx
Sum(
    Filter(
        colDepositosSesionTul As D,
        D.Currency = 'CH-Ch-Currency'.NIO &&
        !IsBlank(
            LookUp(
                Filter(colTulasSesionTul, BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada),
                'CH-Tbl-Bags' = D.BagID.'CH-Tbl-Bags'
            ).'CH-Tbl-Bags'
        )
    ),
    CashAmount
)
```

**CHEQUES(`<MON>`)** — el mismo `Filter`, sumando las **dos** columnas de cheques:

```powerfx
Sum(
    Filter(
        colDepositosSesionTul As D,
        D.Currency = 'CH-Ch-Currency'.NIO &&
        !IsBlank(
            LookUp(
                Filter(colTulasSesionTul, BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada),
                'CH-Tbl-Bags' = D.BagID.'CH-Tbl-Bags'
            ).'CH-Tbl-Bags'
        )
    ),
    BACLocalCheckAmount + OtherLocaCheckAmount
)
```

> 🧨 **`OtherLocaCheckAmount`, sin la `l`** — en `CH-Tbl-DepositsRecountings` la columna
> se llama así (`sanic_otherlocacheckamount`). En `CH-Tbl-DeclaredDeposits` es
> `OtherLocalCheckAmount`, **con** la `l`. No es un typo de esta guía: son dos nombres
> distintos en el modelo (`01` §2.4) y copiar uno en la otra tabla **no compila**.
>
> 🧨 **Los 12 `Sum` de la v1 murieron acá.** La v1 escribía
> `Sum(Filter('CH-Tbl-DepositsRecountings', BagID.RecountingSession.… && BagID.BagStatus = …), CashAmount)`
> — dos saltos sobre un **datasource**, no delegable (bug **D-2**): el rail mostraba
> **cifras parciales de dinero sin aviso**. En la v2 las seis sumas corren sobre
> **colecciones locales** ya cargadas: cero red y cero truncamiento.
>
> 🧨 **`D.BagID.'CH-Tbl-Bags'` sobre un registro YA CARGADO no cuesta un fetch**: es la
> anotación del lookup que Dataverse devolvió con la fila. Lo que sí costaría es
> `D.BagID.BagStatus`, y por eso el estado se busca en `colTulasSesionTul`, que ya está
> en memoria (`01` §8.2).
>
> **Divergencia D03-8**: `01` §8.2 escribe la guarda como `!IsBlank(LookUp(…))`; acá va
> `!IsBlank(LookUp(…).'CH-Tbl-Bags')`. Es el mismo criterio de PK de §0.2: `IsBlank()`
> sobre un **registro** tipado vacío puede dar `false` y el KPI sumaría depósitos de
> tulas que todavía no están recontadas — justo el número que el rail promete filtrar.

---

## 4. Barra scan-first

La receta **`01` §7.8** fija **todas** las propiedades de los siete controles de este
bloque (tipo, estilos, `HintText`, `Format`, `TabIndex`, `DisplayMode`). Acá van solo
las coordenadas y las dos fórmulas de comportamiento.

Card blanca: `X 270 · Y 16 · Width 1076 · Height 144` (`.scan` del mockup: `padding
16px 18px`, `gap 10`) ⇒ columna de contenido `X = 288`, `Width = 1040`.

| Control | Tipo | X · Y · W · H | Lo específico |
|---|---|---|---|
| `btnScanSupTul` | Botón clásico (receta 7.8) | `270` · `16` · `1076` · `144` | — |
| `lblScanTitTul` | Etiqueta clásica (receta 7.8) | `288` · `32` · `300` · `18` | `Text` `"Abrir una tula"` |
| `btnScanCajaTul` | Botón clásico (receta 7.8) | `288` · `lblScanTitTul.Y + lblScanTitTul.Height + 10` · `900` · `44` | el marco de acento del campo |
| `icoScanTul` | Icono clásico `Icon.Search` (receta 7.8) | `btnScanCajaTul.X + 13` · `btnScanCajaTul.Y + 12` · `20` · `20` | `AccessibleLabel` `"Buscar tula"` |
| `inpScanTul` | Entrada de texto clásica (receta 7.8) | `icoScanTul.X + icoScanTul.Width + 10` · `btnScanCajaTul.Y` · `843` · **`44`** | `OnChange` ver abajo · `AccessibleLabel` `"Número de tula"` |
| `btnAbrirTul` | Botón clásico `ESTILO-BTN-GRANDE` (receta 7.8) | `btnScanCajaTul.X + btnScanCajaTul.Width + 10` · `btnScanCajaTul.Y` · `130` · `44` | `Text` `"Abrir tula"` · `OnSelect` **§6.1** |
| `lblScanNotaTul` | Etiqueta clásica (receta 7.8) | `288` · `btnScanCajaTul.Y + btnScanCajaTul.Height + 10` · `1040` · `30` | `Text` ver abajo |

**`lblScanNotaTul.Text`** (literal del mockup, `.note`):

```powerfx
"El campo toma el foco al entrar: el lector de código de barras dispara la apertura con Enter, sin tocar el mouse."
```

**`inpScanTul.OnChange`** — 🧨 **el Enter del lector de código de barras**:

```powerfx
If(!IsBlank(Trim(Self.Text)), Select(btnAbrirTul))
```

> 🧨 **El Enter se implementa con `OnChange`, no con un atajo de teclado** (receta 7.8,
> regla 1). En la **Entrada de texto clásica**, `OnChange` dispara **al presionar Enter**
> y al perder el foco: es el único mecanismo nativo, y el `KeyboardShortcuts` del
> Creator Kit está prohibido (`01` §0.3). El lector de código de barras se comporta como
> un teclado que termina la ristra con Enter ⇒ escanear **es** presionar Enter.
>
> **Corolario, y es lo que mata la duplicación**: TODA la lógica de apertura vive en
> `btnAbrirTul.OnSelect` (§6.1) y `OnChange` **solo lo invoca**. Dos copias serían dos
> verdades — exactamente el pecado de la v1.
>
> **El `OnChange` también dispara cuando el input pierde el foco.** Con el campo vacío
> (el caso de todo `Reset(inpScanTul)` de esta guía) la condición es falsa y no pasa
> nada: por eso los `Reset` son seguros.
>
> **`Format: TextFormat.Text`, nunca `Number`** (receta 7.8, regla 4): los números de
> tula pueden tener ceros a la izquierda (`ST000123`).
>
> **`DisplayMode` de `btnAbrirTul`** (receta 7.8):
> `If(gvarIdentidadOk && !IsBlank(Trim(inpScanTul.Text)), DisplayMode.Edit, DisplayMode.Disabled)`.

---

## 5. Lista de tulas de la sesión

Card: `X 270 · Y 172 · Width 1076 · Height 578` (llega hasta `Y = 750`, el borde
inferior del cuerpo). Tres bandas: encabezado del panel (43), encabezado de columnas
(32) y galería (501).

### 5.1 Card, encabezado del panel y encabezado de columnas

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnCardListaTul` | Botón clásico (receta 7.1) | `270` · `172` · `1076` · `578` | `Radius` (las 4) `13` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `lblPanelTitTul` | Etiqueta clásica | `286` · `181` · `200` · `26` | `ESTILO-DETALLE-TITULO` · `Text` `"Tulas aperturadas hoy"` |
| `lblPanelSubTul` | Etiqueta clásica | `lblPanelTitTul.X + lblPanelTitTul.Width + 10` · `181` · `220` · `26` | `ESTILO-META-CHICA` · `Text` ver abajo |
| `btnActualizarTul` | Botón clásico `ESTILO-BTN-CHICO` | `1226` · `181` · `104` · `26` | `Text` `"Actualizar"` · `PaddingLeft` `20` · `OnSelect` **§5.2** |
| `icoActualizarTul` | Icono clásico `Icon.Reload` | `btnActualizarTul.X + 9` · `btnActualizarTul.Y + 6` · `14` · `14` | `Color` `ThemeInk2` · `TabIndex` `-1` · **`AccessibleLabel` `"Actualizar la lista de tulas"`** · `OnSelect` `Select(btnActualizarTul)` |
| `shpPanelLineaTul` | Rectángulo clásico | `270` · `215` · `1076` · `1` | `Fill` `ThemeLine` |
| `shpHeadListaTul` | Rectángulo clásico | `270` · `216` · `1076` · `32` | `Fill` `ThemeSurface2` |
| `lblHeadTulaTul` | Etiqueta clásica | `286` · `216` · `150` · `32` | `ESTILO-HEADER-COL` · `Text` `"N° DE TULA"` |
| `lblHeadPlanillaTul` | Etiqueta clásica | `446` · `216` · `140` · `32` | `ESTILO-HEADER-COL` · `Text` `"PLANILLA"` |
| `lblHeadClienteTul` | Etiqueta clásica | `596` · `216` · `378` · `32` | `ESTILO-HEADER-COL` · `Text` `"CLIENTE"` |
| `lblHeadHoraTul` | Etiqueta clásica | `984` · `216` · `110` · `32` | `ESTILO-HEADER-COL` · `Text` `"ACTIVIDAD"` |
| `lblHeadEstadoTul` | Etiqueta clásica | `1104` · `216` · `190` · `32` | `ESTILO-HEADER-COL` · `Text` `"ESTADO"` |
| `shpHeadLineaTul` | Rectángulo clásico | `270` · `248` · `1076` · `1` | `Fill` `ThemeLine` |

**`lblPanelSubTul.Text`** — el mockup dice `5 tulas · 3 recontadas` con lista, y
`0 tulas` en el estado vacío (`#b0-vacio`):

```powerfx
If(
    IsEmpty(colTulasSesionTul),
    "0 tulas",
    CountRows(colTulasSesionTul) & " tulas · " &
    CountRows(Filter(colTulasSesionTul, BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada)) &
    " recontadas"
)
```

**Rejilla de columnas** (`.tuHead/.tuRow` del mockup:
`grid-template-columns: 150px 140px 1fr 110px 190px 26px`, `gap 10`, `padding 0 16px`
sobre 1044 de ancho útil ⇒ la columna `1fr` mide **378**):

| # | Columna | `X` absoluto | `Width` |
|---|---|---|---|
| 1 | N° de tula | `286` | `150` |
| 2 | Planilla | `446` | `140` |
| 3 | Cliente | `596` | `378` |
| 4 | Actividad | `984` | `110` |
| 5 | Estado | `1104` | `190` |
| 6 | chevron | `1304` | `26` |

> **`⟳ Actualizar` son DOS controles**, no uno: los glifos **nunca** van en el `Text`
> de un botón (salen como caja vacía en Open Sans — `01` §6.1). El `PaddingLeft 20`
> del botón corre el texto centrado lo justo para que el icono entre a su izquierda, y
> el icono delega el clic con `Select(btnActualizarTul)`. Divergencia **D03-16**.
>
> **La columna se llama "ACTIVIDAD", no "Apertura"** como el mockup: no existe un sello
> de apertura en `CH-Tbl-Bags` y el único dato delegable sin una quinta colección es
> `Modified On`. Es una **aproximación honesta**, no un dato real de apertura:
> divergencia **D03-2**, con su camino de mejora en §11.

### 5.2 `btnActualizarTul.OnSelect` y el motor de carga

```powerfx
// btnActualizarTul.OnSelect
Refresh('CH-Tbl-Bags');
Select(btnCargarSesionTul)
```

```powerfx
// btnCargarSesionTul.OnSelect — MOTOR: las dos consultas de la sesión
Concurrent(
    ClearCollect(
        colTulasSesionTul,
        SortByColumns(
            Filter(
                'CH-Tbl-Bags',
                RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
            ),
            "modifiedon",
            SortOrder.Descending
        )
    ),
    ClearCollect(
        colDepositosSesionTul,
        Filter(
            'CH-Tbl-DepositsRecountings',
            RecountingSessionMirror.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
        )                                          // 🧨 espejo E-1
    )
)
```

🧨 **Espejo `E-1` (`RecountingSessionMirror` en `CH-Tbl-DepositsRecountings`) — hoy NO
existe.** Fallback declarado de `01` §2.2, **y ojo con la forma**: el `ForAll` depende
de que `colTulasSesionTul` YA esté cargada, así que **no puede ir adentro del
`Concurrent`** (las ramas de `Concurrent` corren en cualquier orden y sin garantía de
haber terminado). Sin espejo, el motor se escribe **secuencial**:

```powerfx
// btnCargarSesionTul.OnSelect — FALLBACK sin el espejo E-1
ClearCollect(
    colTulasSesionTul,
    SortByColumns(
        Filter(
            'CH-Tbl-Bags',
            RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
        ),
        "modifiedon",
        SortOrder.Descending
    )
);
Clear(colDepositosSesionTul);
ForAll(
    colTulasSesionTul As B,
    Collect(
        colDepositosSesionTul,
        Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = B.'CH-Tbl-Bags')
    )
)
```

> **Costo del fallback**: N+1 **acotado a las tulas de la sesión** (decenas). Es lento
> pero **no miente**: cada salto sigue siendo delegable y ninguna consulta trunca.
> Cuando el espejo exista, se vuelve a la forma con `Concurrent` y se anota en el ledger
> de `00`.
>
> **`"modifiedon"` va con el NOMBRE LÓGICO**, no con el display name: `SortByColumns`
> sobre un datasource de Dataverse toma el nombre de columna como cadena, y el lógico es
> el que delega. El `SortByColumns` explícito es la regla dura **D-9**: sin él, el orden
> que devuelve el servidor **no está garantizado**.
>
> **El orden se fija en el `ClearCollect`, no en el `Items`.** Una colección conserva el
> orden con el que se llenó, y volver a ordenarla en el `Items` de la galería obligaría
> a nombrar la columna con su display name (`"Modified On"`), que es justo lo que
> `SortByColumns` sobre colecciones espera y sobre datasources no. Una sola forma, en el
> único lugar donde puede delegar.
>
> ✅ **El `Refresh('CH-Tbl-Bags')` del ⟳ NO viola D-5: es la forma 1 que la regla
> declara** (`01` §2.3). Es una **acción del usuario** que pide explícitamente traer lo
> que cambiaron otros — típicamente el supervisor resolviendo una revisión. Es el
> **único** `Refresh` de esta pantalla junto con los dos de la receta 7.7 (§9).

### 5.3 `galTulasTul` — la galería (receta 7.3)

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X · Y · W · H | `270` · `249` · `1076` · `501` |
| `TemplateSize` | `42` |
| `TemplatePadding` | `0` |
| `ShowScrollbar` | `true` |
| `DelayItemLoading` | `true` |
| `Items` | `colTulasSesionTul` |
| `OnSelect` | `UpdateContext({ ctxTulaElegidaTul: ThisItem }); Select(btnReabrirTul)` |

**Controles del template** — `X` del control = `X` del encabezado − `270`:

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `shpSelTul` | Botón clásico (receta 7.1) — **al FONDO del z-order del template** | `0` · `0` · `1076` · `42` | `Text` `""` · `Fill` `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` · `HoverFill` `If(ThisItem.IsSelected, ThemeAccentSoft, ThemeSurface2)` · `PressedFill` `ThemeAccentSoft` · `Radius` (las 4) `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `TabIndex` `0` |
| `shpSelBarraTul` | Rectángulo clásico | `0` · `0` · `3` · `42` | `Fill` `ThemeAccent` · `Visible` `ThisItem.IsSelected` (es el `box-shadow: inset 3px 0` de `.tuSel`) |
| `lblCelTulaTul` | Etiqueta clásica | `16` · `11` · `150` · `20` | `ESTILO-CELDA-FUERTE` · `Text` `ThisItem.BagID` · `OnSelect` `Select(Parent)` |
| `lblCelPlanillaTul` | Etiqueta clásica | `176` · `11` · `140` · `20` | `ESTILO-CELDA` · `Text` `ThisItem.ReceiptIdMirror` · `OnSelect` `Select(Parent)` |
| `lblCelClienteTul` | Etiqueta clásica | `326` · `11` · `378` · `20` | `ESTILO-CELDA` · `Color` `ThemeInk2` · `Text` `ThisItem.ReceptionID.RemitentName` · `OnSelect` `Select(Parent)` |
| `lblCelHoraTul` | Etiqueta clásica | `714` · `11` · `110` · `20` | `ESTILO-META-CHICA` · `Text` `Text(ThisItem.'Modified On', FmtHora)` · `OnSelect` `Select(Parent)` |
| `pilCelEstadoTul` | Botón clásico — **píldora, receta 7.4** | `834` · `10` · `14 + 6.2 * Len(Self.Text)` · `22` | los tres `Switch` de `BagStatus` de la receta 7.4, tal cual · **`DisplayMode` `DisplayMode.Edit`** + `OnSelect` `Select(Parent)` (si no, se come el clic de la fila) |
| `icoCelChevronTul` | Icono clásico `Icon.ChevronRight` | `1039` · `13` · `16` · `16` | `Color` `ThemeInk3` · `TabIndex` `-1` (decorativo) · `OnSelect` `Select(Parent)` |
| `shpSepTul` | Rectángulo clásico | `0` · **`41`** (= `TemplateSize − 1`) · `1076` · `1` | `Fill` `ThemeLineSoft` |

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `lblVacioTitTul` | Etiqueta clásica | `300` · `287` · `1016` · `20` | Size `9.38` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeInk2` · `Align` `Align.Center` · `Text` `"Aún no abrió ninguna tula en esta sesión"` · `Visible` `IsEmpty(colTulasSesionTul)` |
| `lblVacioTxtTul` | Etiqueta clásica | `300` · `lblVacioTitTul.Y + lblVacioTitTul.Height + 4` · `1016` · `40` | `ESTILO-VACIO` · `Wrap` `true` · `Text` `"Escanee el número de tula con el lector o escríbalo en la barra de arriba y presione Enter."` · `Visible` `lblVacioTitTul.Visible` |

> 🧨 **UN solo handler por fila** (receta 7.3). La acción vive en el `OnSelect` de la
> **galería**; los siete hijos del template delegan con `Select(Parent)`. La v1 copiaba
> el mismo `OnSelect` de ~90-120 líneas en 6-7 controles por fila: ~1.400 líneas que el
> motor indexa **por fila**. Si dos controles de este template tienen el mismo cuerpo de
> `OnSelect`, es un bug de construcción.
>
> **La superficie va al FONDO del z-order del template**: si `shpSelTul` queda arriba, su
> `HoverFill` opaco tapa el texto de la fila y el usuario tiene que cazar píxeles vacíos.
>
> **`ThisItem.ReceptionID.RemitentName` es un dot-walk de UN salto sobre un registro ya
> cargado**, en el `Text` de una etiqueta — **no** dentro de un `Filter`/`LookUp` sobre
> un datasource (que sería **D-1**) ni en el `Items` de un control del template (que
> sería **D-4**). Es la misma navegación que `01` §2.1 bendice para
> `ThisItem.ReceptionBatchId.Name`.
>
> **El estado vacío conserva el encabezado de columnas** (mockup `#b0-vacio`) y **no
> lleva el glifo `⛁`**: el catálogo de iconos de `01` §6.1 es cerrado y no tiene
> equivalente; meterlo como texto sería *tofu*. Divergencia **D03-3**.
>
> **La galería no se oculta cuando la colección está vacía**: con `Items` vacío no pinta
> ninguna fila, y las dos etiquetas de vacío se dibujan encima del área. Un `Visible`
> condicional en la galería sería un segundo lugar donde vive la misma verdad.

---

## 6. La apertura de tula — los cuatro motores

Los cinco motores de la **cadena de apertura** (`btnCargarSesionTul` ya quedó en §5.2)
comparten estas propiedades — y el **sexto**, `btnIrSesionTul` (§1.1, motor de SALIDA),
las comparte también, con `X 10`.
**No son `Visible: false`**: `Select()` sobre un control oculto no está garantizado por
la plataforma, así que son botones **de 1 × 1 transparentes**, apoyados en la última
fila de píxeles de la pantalla. Divergencia **D03-9**.

| Propiedad (los 6 motores) | Valor |
|---|---|
| Tipo | **Botón clásico** |
| `Y` · `Width` · `Height` | `767` · `1` · `1` |
| `X` | `0` (`btnCargarSesionTul`) · `2` (`btnResolverDeclaracionTul`) · `4` (`btnAbrirNuevaTul`) · `6` (`btnReabrirTul`) · `8` (`btnEntrarConteoTul`) · `10` (`btnIrSesionTul`, §1.1) |
| `Text` | `""` |
| `Fill` / `HoverFill` / `PressedFill` | `Color.Transparent` |
| `BorderThickness` | `0` |
| `TabIndex` | `-1` |
| `DisplayMode` | `DisplayMode.Edit` (§0.3: la guarda de identidad vive en los botones visibles) |
| `AccessibleLabel` | `""` |

### 6.1 `btnAbrirTul.OnSelect` — resolver el número escaneado

```powerfx
// ===== 1. Estado limpio: cada escaneo empieza de cero =====
UpdateContext(
    {
        ctxScanTul:        Trim(inpScanTul.Text),
        ctxTulaElegidaTul: Defaults('CH-Tbl-Bags'),
        ctxDeclElegidaTul: Defaults('CH-Tbl-DeclaredBags'),
        ctxPickTulaTul:    Defaults('CH-Tbl-Bags'),
        ctxPickDeclTul:    Defaults('CH-Tbl-DeclaredBags'),
        ctxDlgTulaTul:     false,
        ctxDlgDeclTul:     false,
        ctxDlgSinDecTul:   false
    }
);

If(
    IsBlank(ctxScanTul),
    Notify("Escanee o digite un número de tula.", NotificationType.Warning),

    // ===== 2. DOS consultas delegables, una por universo, en paralelo =====
    Concurrent(
        ClearCollect(
            colTulasCandidatasTul,
            SortByColumns(
                Filter(
                    'CH-Tbl-Bags',
                    BagID = ctxScanTul &&
                    (
                        BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda' ||
                        BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'  ||
                        BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'
                    )
                ),
                "createdon",
                SortOrder.Descending
            )
        ),
        ClearCollect(
            colDeclaradasCandidatasTul,
            SortByColumns(
                Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul && IsBlank(BagID.'CH-Tbl-Bags')),
                "createdon",
                SortOrder.Descending
            )
        )
    );

    // ===== 3. La decisión, en un solo If encadenado =====
    If(
        // (A) no hay ninguna tula recontable con ese número
        CountRows(colTulasCandidatasTul) = 0,
            Notify(
                "La tula " & ctxScanTul & " no está disponible para reconteo: no existe, no fue recibida o ya fue recontada.",
                NotificationType.Error
            );
            Reset(inpScanTul);
            SetFocus(inpScanTul),

        // (B) homónimas: el cajero desambigua (diálogo B1)
        CountRows(colTulasCandidatasTul) > 1,
            UpdateContext({ ctxDlgTulaTul: true }),

        // (C) una sola, y ya está abierta en ESTA sesión: se retoma
        First(colTulasCandidatasTul).BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero' &&
        First(colTulasCandidatasTul).RecountingSession.'CH-Tbl-RecountingSession'
            = gvarSesion.'CH-Tbl-RecountingSession',
            UpdateContext({ ctxTulaElegidaTul: First(colTulasCandidatasTul) });
            Select(btnReabrirTul),

        // (D) una sola, pero no se puede abrir
        First(colTulasCandidatasTul).BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',
            Notify(
                "La tula " & ctxScanTul & " no está almacenada en bóveda: puede estar todavía en esclusa o asignada a otro cajero.",
                NotificationType.Warning
            );
            Reset(inpScanTul);
            SetFocus(inpScanTul),

        // (E) una sola, en bóveda: seguimos con la declaración
        UpdateContext({ ctxTulaElegidaTul: First(colTulasCandidatasTul) });
        Select(btnResolverDeclaracionTul)
    )
)
```

> 🧨 **Cada rama del `Concurrent` es UNA sola expresión.** La v1 metía **dos**
> `ClearCollect` encadenados con `;` en la segunda rama, y el segundo era
> **auto-referencial** (`ClearCollect(colMatchingDeclaredBags, Filter(colMatchingDeclaredBags, IsBlank(<lookup>)))`):
> leer y escribir la misma colección adentro de un bloque cuyo orden de ejecución **no
> está definido**. En la v2 el predicado `IsBlank(BagID.'CH-Tbl-Bags')` viaja **dentro** del
> `Filter` delegable y no hay segunda pasada.
>
> 🧨 **`IsBlank(BagID)` —el lookup PELADO— revienta en RUNTIME.** El predicado se escribe
> **SIEMPRE contra la PK del lookup**: `IsBlank(BagID.'CH-Tbl-Bags')`. Con el lookup pelado,
> el conector arma un `$expand` de la *navigation property* y mete su nombre en el `$select`
> interno; Dataverse responde **`Could not find a property named 'sanic_Xxx'`** y el
> `ClearCollect` vuelve vacío. **El App Checker NO lo detecta**: compila en verde, no marca
> delegación, y falla recién al escanear una tula. Es la regla dura **D-13** de `01` §2.3, y
> el ledger la registra en **`L-61`**.
>
> **Si tu Studio marcara `IsBlank(BagID.'CH-Tbl-Bags')` como no delegable**: el fallback es
> `Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul)` —igualdad sobre texto, devuelve
> un puñado de filas— y el descarte de las ya enlazadas se hace **en cliente, contra OTRA
> colección**, nunca `ClearCollect(col, Filter(col, …))`. **No** se usa `IsRelatedToBag`:
> es un **campo fórmula** y los campos fórmula no delegan.
>
> **`BagID` de `CH-Tbl-Bags` es TEXTO (50), no un lookup** (`01` §2.4): por eso
> `BagID = ctxScanTul` delega como igualdad de cadena. En `CH-Tbl-DeclaredBags`, en
> cambio, `BagID` **sí** es el lookup a la tula —y por eso ahí, y solo ahí, se escribe
> `BagID.'CH-Tbl-Bags'`—: dos columnas homónimas en dos tablas, y esta fórmula usa las dos.
> Leelo dos veces antes de tocarlo.
>
> **Cero `Upper()`/`Lower()`** (regla **D-7**): el número se normaliza con `Trim()` del
> lado del **cliente**, una sola vez, en `ctxScanTul`.
>
> **La rama (C) es la cuarta ruta de apertura** (`01` §2.4: *"tula ya abierta que se
> retoma"*) llegando por escaneo en vez de por clic en la lista. Las dos terminan en el
> mismo motor, `btnReabrirTul`.
>
> **La rama (D) NO nombra al cajero que tiene la tula**: `RecountingSession.Cashier.'Full Name'`
> es un dot-walk de **dos** saltos. Divergencia **D03-5**.

### 6.2 `btnResolverDeclaracionTul.OnSelect` — resolver la declaración

```powerfx
If(
    // ===== (1) SIN DECLARACIÓN: ruta de solicitud al supervisor =====
    CountRows(colDeclaradasCandidatasTul) = 0,

        // ¿ya hay una solicitud de HOY, de este cajero, para este número de tula?
        UpdateContext(
            {
                ctxSolicitudPrevTul:
                    First(
                        Sort(
                            Filter(
                                'CH-Tbl-ApprovalRequests',
                                RequestType = "Tula sin declaracion" &&
                                BagNumber   = ctxScanTul &&
                                'Created By'.User = gvarUser.User &&
                                'Created On' >= Today()
                            ),
                            'Created On',
                            SortOrder.Descending
                        )
                    )
            }
        );
        If(
            // ¿es de ESTA tula y de ESTA sesión? Los dos GUID viajan en RequestResources
            !IsBlank(ctxSolicitudPrevTul.'CH-Tbl-ApprovalRequest') &&
            !IsBlank(Find(Text(ctxTulaElegidaTul.'CH-Tbl-Bags'), ctxSolicitudPrevTul.RequestResources)) &&
            !IsBlank(Find(Text(gvarSesion.'CH-Tbl-RecountingSession'), ctxSolicitudPrevTul.RequestResources)),

            If(
                // sigue pendiente ⇒ NO se duplica: se re-adopta y se vuelve a esperar
                IsBlank(ctxSolicitudPrevTul.ApprovalStatus),
                    Set(gvarSolicitud, ctxSolicitudPrevTul);
                    UpdateContext({ ctxEsperandoTul: true, ctxRechazadaTul: false }),

                // ya aprobada ⇒ se consume: abrimos sin declaración
                ctxSolicitudPrevTul.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
                    Set(gvarSolicitud, ctxSolicitudPrevTul);
                    UpdateContext({ ctxDeclElegidaTul: Defaults('CH-Tbl-DeclaredBags') });
                    Select(btnAbrirNuevaTul),

                // rechazada ⇒ el cajero puede volver a pedirla: diálogo B3
                UpdateContext({ ctxDlgSinDecTul: true })
            ),
            UpdateContext({ ctxDlgSinDecTul: true })
        ),

    // ===== (2) MÁS DE UNA DECLARACIÓN: el cajero desambigua (diálogo B2) =====
    CountRows(colDeclaradasCandidatasTul) > 1,
        UpdateContext({ ctxDlgDeclTul: true }),

    // ===== (3) EXACTAMENTE UNA: camino feliz, directo al conteo =====
    UpdateContext({ ctxDeclElegidaTul: First(colDeclaradasCandidatasTul) });
    Select(btnAbrirNuevaTul)
)
```

> 🧨 **La rama (3) NO crea ninguna solicitud** — es la divergencia **`L-6`** de `00` §6,
> ✅ **DECIDIDA y comunicada al dueño del proceso**: en la v1, abrir una tula **con**
> declaración generaba un `"Primera tula en planilla"` y el cajero quedaba esperando al
> supervisor. El mockup (`B1`/`B2`) manda al cajero **directo a `scrConteo`**, y así queda.
> La v2 **no lo crea y tampoco lo espera**: ninguna consulta de recuperación lo incluye
> (`01` §5.8, regla 5), porque esta rama no espera nada. Las solicitudes legadas de ese
> tipo todavía pendientes las cierra el guard (`00` §2.2, punto 7).
>
> 🧨 **La re-adopción existe para no duplicar solicitudes.** El cajero que cancela la
> espera (§9.3) deja una solicitud viva: la app **nunca** escribe `ApprovalStatus`
> (`01` §5.5), así que cancelar la espera **no cancela la solicitud**. Sin este bloque,
> volver a escanear la misma tula le mandaría al supervisor una segunda solicitud
> idéntica. Divergencia **D03-15**.
>
> **La consulta es delegable de punta a punta**: cuatro igualdades/comparaciones sobre
> columnas propias (`RequestType` texto, `BagNumber` texto — la columna que existe justo
> para esto, `L-18` —, `'Created By'.User` **PK contra PK** y `'Created On' >= Today()`).
> El `Sort` + `First` es la regla **D-9**: con varias pendientes, un `LookUp` a secas
> devuelve **una cualquiera**, y la que importa es **la última**.
>
> **El `Find()` de los dos GUID corre en CLIENTE, sobre UN registro.** Es lo que ata la
> solicitud a **esta** tula (no a otra homónima con el mismo `BagNumber`) y a **esta**
> sesión. No toca la delegación de nada: el registro ya está en memoria.

### 6.3 `btnAbrirNuevaTul.OnSelect` — el ÚNICO `Patch` de la bolsa

```powerfx
// ===== 1. RE-LECTURA POR PK: la tula pudo cambiar mientras el cajero decidía =====
UpdateContext(
    {
        ctxTulaFrescaTul:
            LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxTulaElegidaTul.'CH-Tbl-Bags')
    }
);

If(
    // ---- ya no existe ----
    IsBlank(ctxTulaFrescaTul.'CH-Tbl-Bags'),
        Notify("La tula ya no está disponible. Actualice la lista y vuelva a escanearla.", NotificationType.Error);
        Reset(inpScanTul);
        SetFocus(inpScanTul),

    // ---- se la llevó otro (o cambió de estado) ----
    ctxTulaFrescaTul.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',
        Notify(
            "La tula " & ctxTulaFrescaTul.BagID & " dejó de estar almacenada en bóveda: otro cajero pudo tomarla. No se abrió nada.",
            NotificationType.Error
        );
        Reset(inpScanTul);
        SetFocus(inpScanTul),

    // ---- camino feliz ----
    If(
        IsBlank(ctxDeclElegidaTul.'CH-Tbl-DeclaredBags'),

        // (a) SIN declaración: BagDeclarationID queda en blanco
        IfError(
            Set(
                gvarTula,
                Patch(
                    'CH-Tbl-Bags',
                    ctxTulaFrescaTul,
                    {
                        BagStatus:         'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',
                        RecountingSession: gvarSesion
                    }
                )
            ),
            If(true,
                Notify("No se pudo abrir la tula: " & FirstError.Message, NotificationType.Error);
                Set(gvarTula, Blank());
                Blank()
            )
        );
        Set(gvarDeclaredBag, Blank()),

        // (b) CON declaración: se escribe el enlace en las DOS puntas
        IfError(
            Set(
                gvarTula,
                Patch(
                    'CH-Tbl-Bags',
                    ctxTulaFrescaTul,
                    {
                        BagStatus:        'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',
                        RecountingSession: gvarSesion,
                        BagDeclarationID:  ctxDeclElegidaTul
                    }
                )
            ),
            If(true,
                Notify("No se pudo abrir la tula: " & FirstError.Message, NotificationType.Error);
                Set(gvarTula, Blank());
                Blank()
            )
        );
        If(
            IsBlank(gvarTula.'CH-Tbl-Bags'),
            Set(gvarDeclaredBag, Blank()),
            IfError(
                Set(
                    gvarDeclaredBag,
                    Patch('CH-Tbl-DeclaredBags', ctxDeclElegidaTul, { BagID: gvarTula })
                ),
                If(true,
                    Notify(
                        "La tula se abrió, pero no se pudo enlazar la declaración: " & FirstError.Message,
                        NotificationType.Error
                    );
                    Set(gvarDeclaredBag, Blank());
                    Blank()
                )
            )
        )
    );

    // ---- 2. sin tula patcheada NO se sigue ----
    If(
        IsBlank(gvarTula.'CH-Tbl-Bags'),
        Reset(inpScanTul); SetFocus(inpScanTul),
        Select(btnEntrarConteoTul)
    )
)
```

> 🧨 **El guard de estado es lo que separa esto de la v1.** Entre que el cajero escanea
> y confirma (o entre que pide la aprobación y el supervisor responde) pueden pasar
> minutos: la tula puede haber cambiado de manos. La re-lectura **por PK** es una
> consulta delegable y barata, y **el `Patch` va contra el registro fresco**, no contra
> el que quedó guardado en la colección.
>
> 🧨 **Son DOS ramas de `Patch`, no un `If` adentro del registro de cambios.** Un
> `BagDeclarationID: If(cond, Blank(), <registro>)` mezcla tipos dentro de una columna de
> lookup; la forma explícita es dos `Patch` completos, cada uno con su `IfError`.
>
> **Todo `Patch` va envuelto en `IfError` con decisión explícita** (receta 7.5, forma A),
> y el brazo de fallo **deja el estado local diciendo la verdad** (`Set(gvarTula, Blank())`).
> `App.OnError` es la red final, no el manejo de errores.
>
> **`DeclaredBags.BagID` es la ÚNICA columna que la app escribe en esa tabla**
> (`01` §2.4). El enlace se escribe en las dos puntas — `Bags.BagDeclarationID` y
> `DeclaredBags.BagID` — porque el modelo lo tiene así; si la segunda falla, la primera
> ya está y el aviso lo dice, en vez de callar.
>
> **Cero `Refresh()` acá** (regla **D-5**): el estado local sale de lo que devolvió el
> `Patch`. La v1 hacía `Refresh('CH-Tbl-Bags'); Refresh('CH-Tbl-DeclaredBags')` y después
> un `LookUp` para releer lo que el `Patch` ya le había devuelto.

### 6.4 `btnReabrirTul.OnSelect` — retomar una tula ya abierta

```powerfx
IfError(
    Set(gvarTula, LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = ctxTulaElegidaTul.'CH-Tbl-Bags')),
    If(true,
        Notify("No se pudo releer la tula: " & FirstError.Message, NotificationType.Error);
        Set(gvarTula, Blank());
        Blank()
    )
);

If(
    IsBlank(gvarTula.'CH-Tbl-Bags'),
    Notify("La tula ya no está disponible. Toque ⟳ para actualizar la lista.", NotificationType.Error),

    // la declaración se relee COMPLETA por PK: 04 necesita sus totales, no la anotación
    If(
        IsBlank(gvarTula.BagDeclarationID.'CH-Tbl-DeclaredBags'),
        Set(gvarDeclaredBag, Blank()),
        Set(
            gvarDeclaredBag,
            LookUp(
                'CH-Tbl-DeclaredBags',
                'CH-Tbl-DeclaredBags' = gvarTula.BagDeclarationID.'CH-Tbl-DeclaredBags'
            )
        )
    );
    Select(btnEntrarConteoTul)
)
```

> **Retomar NO patchea nada.** La tula ya es del cajero (o ya está `Recontada`): tocarle
> el `BagStatus` la haría retroceder de estado. `04` gobierna qué se puede editar con
> `gvarTula.BagStatus = 'Asignada a cajero'` en sus `DisplayMode` (`01` §6.3), así que
> una tula `Recontada` se abre **en solo lectura** — que es lo que el mockup promete al
> dibujar el chevron en **todas** las filas.
>
> 🧨 **`gvarTula.BagDeclarationID` navegado da la ANOTACIÓN del lookup** (PK + nombre
> primario), no los `TotalNIO/USD/EUR` que `04` necesita para la columna *Declarado*.
> Por eso el `LookUp` completo por PK. Es una consulta, delegable, y solo cuando hay
> declaración.

---

## 7. `btnEntrarConteoTul` — el detalle de sesión y la salida a `04`

**Precondición**: `gvarTula` y `gvarDeclaredBag` ya seteados por `btnAbrirNuevaTul`
(§6.3) o por `btnReabrirTul` (§6.4). Este motor es el **único** que crea el
`CH-Tbl-RecountingSessionDetails` y el **único** que navega.

```powerfx
If(
    IsBlank(gvarTula.'CH-Tbl-Bags'),
    Notify("No hay ninguna tula para abrir.", NotificationType.Error),

    // ===== 1. EL DETALLE DE SESIÓN — SIEMPRE, en las CUATRO rutas y en el retome =====
    UpdateContext(
        {
            ctxDetalleTul:
                LookUp(
                    'CH-Tbl-RecountingSessionDetails',
                    BagId.'CH-Tbl-Bags' = gvarTula.'CH-Tbl-Bags' &&
                    RecountingSessionId.'CH-Tbl-RecountingSession'
                        = gvarSesion.'CH-Tbl-RecountingSession'
                )
        }
    );
    If(
        IsBlank(ctxDetalleTul.'CH-Tbl-RecountingSessionDetail'),
        IfError(
            Set(
                gvarSesionDetalle,
                Patch(
                    'CH-Tbl-RecountingSessionDetails',
                    Defaults('CH-Tbl-RecountingSessionDetails'),
                    {
                        RecountingSessionId:           gvarSesion,
                        BagId:                         gvarTula,
                        RecountingSessionDetailStatus: 'ch-recountingDetailsStatus'.'En progreso'
                    }
                )
            ),
            If(true,
                Notify(
                    "No se pudo crear el detalle de sesión de la tula: " & FirstError.Message,
                    NotificationType.Error
                );
                Set(gvarSesionDetalle, Blank());
                Blank()
            )
        ),
        Set(gvarSesionDetalle, ctxDetalleTul)
    );

    // ===== 2. SIN DETALLE NO SE NAVEGA. Es la precondición dura de 04 =====
    If(
        IsBlank(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'),
        Notify(
            "La tula no se abrió: sin detalle de sesión la máquina contadora no tiene ID_Sesion y el reconteo sería a ciegas.",
            NotificationType.Error
        ),

        // ===== 3. CONTRATO 03 → 04 (01 §4.5) =====
        Set(gvarSolicitud, Blank());
        Set(gvarDepositoSinGuardar, false);
        Clear(colDetalleConteoCon);
        Clear(colChecksCon);
        Clear(colAutoContadoraCon);
        Clear(colDepDeclaradosCon);
        Clear(colDepRecontadosCon);
        Clear(colChequesDeclaradosCon);
        Clear(colChecksTulaCon);          // 🧨 los cheques recontados de la tula ANTERIOR
        Clear(colDetalleDVCon);           // 🧨 snapshot persistido del depósito anterior
        Clear(colChecksDVCon);            // 🧨 ídem, cheques
        Clear(colHallazgosCon);
        UpdateContext({ ctxEsperandoTul: false, ctxRechazadaTul: false });
        Reset(inpScanTul);
        Notify("Tula " & gvarTula.BagID & " abierta.", NotificationType.Information);
        Navigate(scrConteo)
    )
)
```

> 🧨 **Este bloque es el fix del bug más caro de la v1.** El
> `CH-Tbl-RecountingSessionDetails` se creaba en **1 de 4** rutas: en las otras tres, la
> contadora no tenía `ID_Sesion`, la columna **Auto** quedaba vacía y el cajero recontaba
> a ciegas (`01` §2.4). Acá se crea **o se recupera** en un solo lugar, y **si no hay
> detalle no se navega**.
>
> **El `LookUp` es PK contra PK en las DOS condiciones** (regla **D-1**):
> `BagId.'CH-Tbl-Bags'` y `RecountingSessionId.'CH-Tbl-RecountingSession'`. Delegable, un
> `eq` de GUID por lado. Lo que **no** se puede es `BagId.BagStatus`.
>
> **`RecountingSessionDetailId` es AUTONUMBER y es la columna primaria — NO se llama
> `Name` y NO se escribe** (`01` §2.4). `SupervisorId` tampoco: lo escribe el Supervisor.
>
> 🧨 **Las DIEZ colecciones de `04` se vacían ACÁ, antes de navegar** (`01` §4.5). Si no se
> vacían, el `OnVisible` de `scrConteo` encuentra las de la tula anterior y las da por
> buenas. Los `Clear` son lo que **habilita** que `04` las repueble.
>
> 🧨 **Las tres últimas (`colChecksTulaCon`, `colDetalleDVCon`, `colChecksDVCon`) se
> agregaron al contrato porque son las que MIENTEN MÁS CARO si sobreviven**:
> `colChecksTulaCon` alimenta la cantidad y el monto de cheques *Recontado* de las **tres
> matrices** de `04` §4 y el pie por moneda de §5.3 ⇒ con la de la tula anterior, el arqueo
> de la tula nueva arranca con cheques que no son suyos. `colDetalleDVCon` y `colChecksDVCon`
> son los snapshots de lo persistido de los que sale el **paso de bajas** del guardado
> (`04` §9.2, pasos 4 y 5): con filas de otro depósito adentro, el guardado intentaría
> `Remove` de registros ajenos. `04` §1 las repuebla igual, pero el contrato no puede
> depender de que la pantalla de destino limpie lo que la de origen ensució.
>
> **Los datos de la contadora NO viajan en una variable propia.** La v1 tenía
> `gvarDatosConteo = {ID_Sesion, Serial_Maquina}`; `01` §4.1 **no la inventaría** y la
> receta 7.11 lee los dos datos de donde ya viven:
> `gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'` (el `ID_Sesion`) y
> `gvarMesa.MachineSerialNumber` (el `Serial_Maquina`, que `02` ya dejó seteado). Una
> tercera copia sería una tercera verdad.
>
> **Y cero `Notify` con GUIDs o seriales** (**S-6**): la v1 imprimía en producción un
> toast con el GUID de la sesión y el **serial de la contadora** en este mismo punto.

---

## 8. 🧨 CONTRATO CANÓNICO `03` → `04`

Lo que queda seteado **exactamente** al ejecutarse `Navigate(scrConteo)` (§7). Es la
contraparte del contrato `02` → `03` del encabezado, y `04` puede **asumirlo entero**.

| Global | Valor al navegar | Ruta *tula nueva* | Ruta *retome* | Quién lo escribe |
|---|---|---|---|---|
| `gvarTula` | Registro de `CH-Tbl-Bags` **fresco**, devuelto por el `Patch` | `BagStatus` = `'Asignada a cajero'`, `RecountingSession` = `gvarSesion`, `BagDeclarationID` escrito o en blanco | tal cual está en Dataverse (puede ser `Asignada a cajero` **o** `Recontada` / `Pendiente de revision…`) | `btnAbrirNuevaTul` (§6.3) / `btnReabrirTul` (§6.4) |
| `gvarDeclaredBag` | Registro de `CH-Tbl-DeclaredBags` **o `Blank()`** | el registro elegido (ya con `BagID` apuntando a la tula) · `Blank()` en la ruta *sin declaración* | releído **completo** por PK, o `Blank()` si la tula no tiene declaración | ídem |
| `gvarSesionDetalle` | Registro de `CH-Tbl-RecountingSessionDetails` — **NUNCA en blanco** | creado con `RecountingSessionDetailStatus` = `'En progreso'` | recuperado por `LookUp`, o **creado** si no existía | `btnEntrarConteoTul` (§7) |
| `gvarSesion` | Registro de la sesión de hoy | sin cambios (viene de `02`) | sin cambios | `02` |
| `gvarMesa` | `gvarSesion.CountingDesk` — trae el `MachineSerialNumber` de la receta 7.11 | sin cambios | sin cambios | `02` |
| `gvarUser` · `gvarIdentidadOk` | sin cambios | — | — | `App.OnStart` |
| `gvarSolicitud` | **`Blank()`** | siempre | siempre | `btnEntrarConteoTul` (§7) |
| `gvarDepositoSinGuardar` | **`false`** | siempre | siempre | `btnEntrarConteoTul` (§7) |

| Colección | Estado al navegar |
|---|---|
| `colDetalleConteoCon` · `colChecksCon` · `colAutoContadoraCon` | **VACÍAS** (`Clear`) |
| `colDepDeclaradosCon` · `colDepRecontadosCon` · `colChequesDeclaradosCon` | **VACÍAS** (`Clear`) |
| `colChecksTulaCon` · `colDetalleDVCon` · `colChecksDVCon` | **VACÍAS** (`Clear`) |
| `colHallazgosCon` | **VACÍA** (`Clear`) |
| `colTulasSesionTul` · `colDepositosSesionTul` | **se dejan como están**: son de `03` y se recargan solas al volver (§1) |
| `colTulasCandidatasTul` · `colDeclaradasCandidatasTul` | **se dejan como están**: solo viven durante una resolución y el próximo escaneo las pisa |

**Los cinco datos de contadora que `04` va a necesitar** y de dónde salen —
**ninguno** es una variable nueva:

| Dato de la receta 7.11 | Fórmula en `04` |
|---|---|
| `ID_Sesion` | `Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail')` |
| `Serial_Maquina` | `gvarMesa.MachineSerialNumber` |
| Universo de la lectura (espejo **E-4**) | `RecountingSessionDetailMirror.'CH-Tbl-RecountingSessionDetail' = gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'` |
| Estado del detalle al cerrar | `Patch` sobre `gvarSesionDetalle` con `'ch-recountingDetailsStatus'.Cerrado` / `.'Cerrado con diferencias'` |
| Tipo de solicitud al terminar | `IsBlank(gvarTula.BagDeclarationID)` (`01` §5.6), **no** una variable paralela |

> 🧨 **`gvarSesionDetalle` NUNCA puede venir en blanco** (`01` §4.5). `03` lo garantiza
> con la guarda del paso 2 de §7. `04` igual lo verifica en su `OnVisible` y, si está en
> blanco, deshabilita *Leer contadora* con un aviso — **nunca sigue en silencio**.
>
> ✅ **`D03-10` — RESUELTA por consolidación → ver `01` §4.5.** `Set(gvarSolicitud, Blank())`
> quedó incorporado al contrato canónico de `01` §4.5 (con su motivo) y al CONTRATO DE
> ENTRADA de `04`. Ya no es divergencia: es contrato en las tres guías.

---

## 9. Espera de aprobación, polling y rechazo

Solo la ruta **tula sin declaración** (`B3`) espera al supervisor. El chrome de los dos
overlays sale de la tabla de **§10.1**; acá van el timer, los cuerpos y las salidas.

### 9.1 `tmrPollTul` — un timer, 15 s, y el rechazo BLOQUEA

| Propiedad | Valor |
|---|---|
| Tipo | **Temporizador clásico** |
| X · Y · W · H | `10` · `767` · `1` · `1` |
| `AutoStart` | `false` |
| `Duration` | `15000` |
| `Repeat` | `true` |
| `Start` | **`ctxEsperandoTul && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`** (contrato `01` §5.8) |
| `Visible` | `false` |
| `OnTimerStart` | **vacío** |
| `OnTimerEnd` | ver abajo |

```powerfx
// tmrPollTul.OnTimerEnd — UNA consulta delegable, por PK
Set(
    gvarSolicitud,
    LookUp(
        'CH-Tbl-ApprovalRequests',
        'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest'
    )
);
If(
    // ---- APROBADO: se abre la tula, sin declaración ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
        Refresh('CH-Tbl-Bags');
        UpdateContext(
            {
                ctxEsperandoTul:   false,
                ctxRechazadaTul:   false,
                ctxDeclElegidaTul: Defaults('CH-Tbl-DeclaredBags')
            }
        );
        Select(btnAbrirNuevaTul),

    // ---- RECHAZADO: BLOQUEA, no libera ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
        Refresh('CH-Tbl-Bags');
        UpdateContext({ ctxEsperandoTul: false, ctxRechazadaTul: true }),

    // ---- ni una ni otra: SIGUE PENDIENTE. No se hace NADA ----
    false
)
```

> ✅ **`D03-1` — RESUELTA por consolidación → ver `01` §5.8.** El contrato madre ya **no**
> dice *"`03` no tiene timer"*: fija **un timer por pantalla que crea o espera una
> solicitud**, y `03` crea `"Tula sin declaracion"` (`01` §5.6) sobre un mockup (`B3`) que
> promete *"La tula queda bloqueada hasta que responda"*. Sin timer, el cajero confirma la
> solicitud y **queda en la nada**. `tmrPollTul` es la receta 7.7 aplicada tal cual, con
> `<Scr>` = `Tul`, y esta pantalla **solo** conoce su propio `RequestType`.
>
> 🧨 **`Start` es una CONDICIÓN, jamás `true`, y cuelga de `ctxEsperandoTul`** (`01` §5.8).
> Cuando la solicitud se resuelve —o cuando el cajero cancela la espera y `gvarSolicitud`
> vuelve a `Blank()`— la condición se cae sola y el timer se apaga: no hay que apagarlo a
> mano. **No** se usa `IsBlank(gvarSolicitud.ApprovalStatus)`: la re-adopción de §6.2 puede
> dejar una solicitud **ya resuelta** en la variable antes de que la UI pinte el estado. La
> v1 tenía **tres** timers, uno con `Start: =true` **literal**, corriendo siempre.
>
> 🧨 **`OnTimerStart` vacío, cero `Refresh` por tick.** Los dos `Refresh('CH-Tbl-Bags')`
> de arriba son la **forma 2** que la regla **D-5** declara: corren **una vez**, en la
> rama de resolución, porque el servidor **acaba de cambiar** el estado de la bolsa. La
> v1 tenía dos timers de 5 s con `Refresh` en `OnTimerStart` ⇒ ~10 consultas en cascada
> por ciclo.
>
> 🧨 **El rechazo BLOQUEA** (fix de **S-4**). La v1 preguntaba
> `IsBlank(LookUp(…, ApprovalStatus = Blank()))` y **fallaba abierto**: un rechazo
> liberaba al cajero exactamente igual que una aprobación. Acá son dos ramas explícitas
> y el `else` es `false` — no hacer nada.
>
> **La rama aprobada NO patchea nada por su cuenta**: delega en `btnAbrirNuevaTul`, que
> vuelve a leer la tula por PK y verifica que siga en bóveda (§6.3). Si en el ínterin se
> la llevó otro cajero, el cajero se entera con un `Notify` y no se abre nada.
>
> **Los timers no corren en pantallas que no están visibles** (receta 7.10.C, regla 2).
> Por eso el `Navigate` de la rama aprobada ocurre **adentro** de la cadena que arranca
> en este `OnTimerEnd`, con `scrTulas` todavía visible, y no antes.

### 9.2 Overlay de espera (`cntEsperaTul`, mockup `C7`/`A2`)

Card de **400**. `Visible` del overlay: **`ctxEsperandoTul`**.

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `lblDlgTitEsperaTul` | Etiqueta clásica | `22` · `17` · `356` · `22` | `ESTILO-DLG-TITULO` · `Text` `"Esperando aprobación del supervisor"` · `Wrap` `true` · `AutoHeight` `true` |
| `lblDlgTxtEsperaTul` | Etiqueta clásica | `22` · `lblDlgTitEsperaTul.Y + lblDlgTitEsperaTul.Height + 6` · `356` · — | `ESTILO-DLG-TEXTO` · `AutoHeight` `true` · `Text` `"La tula queda bloqueada hasta que el supervisor responda."` |
| `btnWaitRowTul` | Botón clásico (receta 7.1) | `22` · `lblDlgTxtEsperaTul.Y + lblDlgTxtEsperaTul.Height + 13` · `356` · `62` | `Radius` (las 4) `11` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `spnEsperaTul` | **Spinner MODERNO** | `btnWaitRowTul.X + 16` · `btnWaitRowTul.Y + 14` · `34` · `34` | `Visible` `ctxEsperandoTul` |
| `lblWaitTitTul` | Etiqueta clásica | `spnEsperaTul.X + spnEsperaTul.Width + 14` · `btnWaitRowTul.Y + 13` · `270` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `"Solicitud enviada al supervisor"` |
| `lblWaitSubTul` | Etiqueta clásica | `lblWaitTitTul.X` · `lblWaitTitTul.Y + lblWaitTitTul.Height` · `270` · `16` | `ESTILO-META-CHICA` · `Text` ver abajo |
| `lblKvTulaEtqEsperaTul` | Etiqueta clásica | `22` · `btnWaitRowTul.Y + btnWaitRowTul.Height + 13` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Tula"` |
| `lblKvTulaEsperaTul` | Etiqueta clásica | `140` · `lblKvTulaEtqEsperaTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.BagID` |
| `lblKvPlanEtqEsperaTul` | Etiqueta clásica | `22` · `lblKvTulaEtqEsperaTul.Y + 22` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Planilla"` |
| `lblKvPlanEsperaTul` | Etiqueta clásica | `140` · `lblKvPlanEtqEsperaTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.ReceiptIdMirror` |
| `lblKvCliEtqEsperaTul` | Etiqueta clásica | `22` · `lblKvPlanEtqEsperaTul.Y + 22` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Cliente"` |
| `lblKvCliEsperaTul` | Etiqueta clásica | `140` · `lblKvCliEtqEsperaTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.ReceptionID.RemitentName` |
| `btnDlgCancelEsperaTul` | Botón clásico | `cntCardEsperaTul.Width - 22 - Self.Width` · `lblKvCliEtqEsperaTul.Y + lblKvCliEtqEsperaTul.Height + 15` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-FANTASMA` · `Text` `"Cancelar la espera"` · `OnSelect` **§9.3** |

**`lblWaitSubTul.Text`:**

```powerfx
"Se consulta el estado cada 15 segundos · enviada " & Text(gvarSolicitud.'Created On', FmtHora)
```

**`cntCardEsperaTul.Height`** = `btnDlgCancelEsperaTul.Y + btnDlgCancelEsperaTul.Height + 19`.

> **Este diálogo NO lleva ✕** (mockup `C7`): la única salida es el botón, y es explícita.
> Su `btnBloqEsperaTul` traga los clics del fondo igual que los demás (`L-17`).
>
> **El Spinner moderno se usa acá y solo acá en esta pantalla**: durante la espera el
> hilo está **libre** (el trabajo lo hace el supervisor, del otro lado), así que la
> animación es real. El diálogo *"Guardando…"* de `04` no lleva spinner por el motivo
> opuesto (receta 7.10.C, regla 4).
>
> **La espera es un overlay, no una pantalla** (receta 7.7, regla 5): mantiene el hub
> detrás y evita una cuarta pantalla.

### 9.3 `btnDlgCancelEsperaTul.OnSelect` — cancelar la ESPERA, no la solicitud

```powerfx
UpdateContext({ ctxEsperandoTul: false });
Set(gvarSolicitud, Blank());
Notify(
    "La solicitud sigue pendiente: el supervisor todavía tiene que responderla. Vuelva a escanear la tula más tarde.",
    NotificationType.Information
);
Reset(inpScanTul);
SetFocus(inpScanTul)
```

> 🧨 **El botón cancela la ESPERA, no la SOLICITUD, y el texto lo dice.** La app **nunca
> escribe `ApprovalStatus`** (`01` §5.5, mata **S-1**): no tiene forma de anular una
> solicitud. Prometer *"Cancelar solicitud"* sería mentir. Cuando el cajero vuelva a
> escanear esa tula, §6.2 **re-adopta** la solicitud pendiente en vez de crear otra.
> Divergencia **D03-14**.
>
> **Las dos primeras líneas son las que apagan el timer**: su `Start` es
> `ctxEsperandoTul && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')` (`01` §5.8) y acá
> caen **los dos** términos. No hace falta tocar `tmrPollTul`.

### 9.4 Overlay de rechazo (`cntRechazoTul`, mockup `C8`/`A3`)

Card de **400**. `Visible` del overlay: **`ctxRechazadaTul`**.

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnIcoRechazoTul` | Botón clásico (receta 7.1) | `22` · `17` · `34` · `34` | `Radius` (las 4) `9` · `Fill`/`HoverFill`/`PressedFill` `ThemeDangerSoft` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoTipoRechazoTul` | Icono clásico `Icon.Blocked` | `btnIcoRechazoTul.X + 8` · `btnIcoRechazoTul.Y + 8` · `18` · `18` | `Color` `ThemeDanger` |
| `lblDlgTitRechazoTul` | Etiqueta clásica | `btnIcoRechazoTul.X + btnIcoRechazoTul.Width + 10` · `17` · `278` · `22` | `ESTILO-DLG-TITULO` · `Text` `"Solicitud rechazada"` |
| `lblDlgTxtRechazoTul` | Etiqueta clásica | `lblDlgTitRechazoTul.X` · `lblDlgTitRechazoTul.Y + lblDlgTitRechazoTul.Height + 4` · `312` · — | `ESTILO-DLG-TEXTO` · `AutoHeight` `true` · `Text` ver abajo |
| `icoDlgXRechazoTul` | Icono clásico `Icon.Cancel` | `cntCardRechazoTul.Width - 36` · `17` · `18` · `18` | `Color` `ThemeInk3` · `HoverColor` `ThemeInk` · `TabIndex` `0` · **`AccessibleLabel` `"Cerrar el diálogo"`** · `OnSelect` **el mismo de `btnDlgOkRechazoTul`** |
| `btnAvisoRechazoTul` | Botón clásico (receta 7.2.a) | `22` · `lblDlgTxtRechazoTul.Y + lblDlgTxtRechazoTul.Height + 13` · `356` · `76` | `Fill`/`HoverFill`/`PressedFill` `ThemeDangerSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAvisoRechazoTul` | Icono clásico `Icon.Blocked` | `btnAvisoRechazoTul.X + 13` · `btnAvisoRechazoTul.Y + 12` · `16` · `16` | `Color` `ThemeDanger` · `Visible` `btnAvisoRechazoTul.Visible` |
| `lblAvisoRechazoTul` | Etiqueta clásica | `btnAvisoRechazoTul.X + 38` · `btnAvisoRechazoTul.Y + 9` · `306` · `18` | Size `9` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeDanger` · `Text` ver abajo · `Visible` `btnAvisoRechazoTul.Visible` |
| `lblMotivoRechazoTul` | Etiqueta clásica | `lblAvisoRechazoTul.X` · `lblAvisoRechazoTul.Y + lblAvisoRechazoTul.Height` · `306` · `40` | Size `9` · `Color` `ThemeDanger` · **`Wrap` `true`** · `Text` `gvarSolicitud.StatusReason` · `Visible` `btnAvisoRechazoTul.Visible` |
| `lblNotaRechazoTul` | Etiqueta clásica | `22` · `btnAvisoRechazoTul.Y + btnAvisoRechazoTul.Height + 12` · `356` · `34` | `ESTILO-DLG-TEXTO` · `Wrap` `true` · `Text` `"El rechazo no abre la tula: sigue almacenada en bóveda y no se creó ningún detalle de reconteo."` |
| `btnDlgOkRechazoTul` | Botón clásico | `cntCardRechazoTul.Width - 22 - Self.Width` · `lblNotaRechazoTul.Y + lblNotaRechazoTul.Height + 15` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-PRIMARIO` · `Text` `"Entendido"` · `OnSelect` ver abajo |

**`lblDlgTxtRechazoTul.Text`:**

```powerfx
"El supervisor rechazó la apertura de la tula " & ctxTulaElegidaTul.BagID & " sin declaración del cliente."
```

**`lblAvisoRechazoTul.Text`:**

```powerfx
"Rechazada por " & gvarSolicitud.SolvedBy.'Full Name' & "  ·  " &
Text(gvarSolicitud.ResolutionDatetime, FmtFecha)
```

**`btnDlgOkRechazoTul.OnSelect`** (y, literalmente igual, `icoDlgXRechazoTul.OnSelect`):

```powerfx
UpdateContext({ ctxRechazadaTul: false });
Set(gvarSolicitud, Blank());
Reset(inpScanTul);
SetFocus(inpScanTul)
```

**`cntCardRechazoTul.Height`** = `btnDlgOkRechazoTul.Y + btnDlgOkRechazoTul.Height + 19`.

> 🧨 **El motivo del rechazo es `StatusReason`, y se muestra SIEMPRE.** Es lo que
> escribió el supervisor (`01` §2.4) y la única forma de que el cajero sepa qué corregir.
> Un rechazo sin motivo visible es un rechazo que el cajero va a repetir.
>
> **`SolvedBy` es un lookup a `SystemUser`** (verificado contra `Other/Relationships`):
> `gvarSolicitud.SolvedBy.'Full Name'` es un dot-walk de **un** salto sobre un registro
> ya cargado, fuera de todo `Filter`. Si en tu Studio no resuelve el nombre, dejá solo la
> fecha y **avisá** para corregir esta guía.
>
> **La ✕ hace LO MISMO que *Entendido*** (receta 7.2, regla 1): cerrar y no commitear
> nada. Acá "no commitear" es literal — el rechazo no cambió nada en Dataverse desde el
> lado de la app.

---

## 10. Los tres diálogos de desambiguación (`B1`, `B2`, `B3`)

Los cinco diálogos de la pantalla siguen la **receta `01` §7.2**, que fija TODAS las
propiedades del chrome. **Ninguno usa `cmpDialogo`**: los cinco tienen **cuerpo rico**
(galerías de *pick cards*, banners, resúmenes `kv`, spinner) y un componente canvas **no
puede recibir controles hijos** (`01` §8.3, nota **C** y `00` `L-10`).

### 10.1 Chrome — mapeo mecánico, una fila por diálogo

**Los nombres van elididos en toda esta tabla**: cada `…` es el nombre del diálogo de la
primera columna (`Tula`, `Decl`, `SinDec`, `Espera`, `Rechazo`), exactamente como los
escribe el árbol, seguido del sufijo `Tul`. Así, la fila *`B1`* define
`cntDlgTulaTul` (el overlay, `Visible` = `ctxDlgTulaTul`), `btnBloqTulaTul` (su primer
hijo, **al FONDO**) y `cntCardTulaTul` (la card, con el ancho de la cuarta columna).
**Los 5 × 3 = 15 controles de chrome salen de esta tabla y de ningún otro lado**:

| Fila | `cnt…` | `btnBloq…` | `cntCard…` |
|---|---|---|---|
| `B1` | `cntDlgTulaTul` | `btnBloqTulaTul` | `cntCardTulaTul` |
| `B2` | `cntDlgDeclTul` | `btnBloqDeclTul` | `cntCardDeclTul` |
| `B3` | `cntDlgSinDecTul` | `btnBloqSinDecTul` | `cntCardSinDecTul` |
| Espera | `cntEsperaTul` | `btnBloqEsperaTul` | `cntCardEsperaTul` |
| Rechazo | `cntRechazoTul` | `btnBloqRechazoTul` | `cntCardRechazoTul` |

> 🧨 **Las dos últimas filas rompen la elisión `cntDlg…` a propósito: son `cntEsperaTul` y
> `cntRechazoTul`, SIN el `Dlg`.** No son diálogos de decisión del cajero: son los **dos
> overlays de estado** que la receta `01` §7.7 nombra `cntEspera<Scr>` y `cntRechazo<Scr>`,
> con `Visible` = `ctxEsperandoTul` / `ctxRechazadaTul` (no un `ctxDlg…`). `04` ya los nombra
> así (`cntEsperaCon` / `cntRechazoCon`): con esto **las tres pantallas usan el mismo
> nombre para la misma cosa**. Los nombres de la tabla **mandan** sobre la elisión.

Las secciones de abajo (y §9.2 / §9.4) agregan solo el **cuerpo** de cada uno.

| Diálogo | Mockup | `cnt…` → `Visible` | `cntCard….Width` | Título (`lblDlgTit…Tul.Text`) |
|---|---|---|---|---|
| `B1` Seleccione la tula recibida | `#b1` | `ctxDlgTulaTul` | **`560`** | `"Seleccione la tula recibida"` |
| `B2` Seleccione la declaración | `#b2` | `ctxDlgDeclTul` | **`560`** | `"Seleccione la declaración"` |
| `B3` Tula sin declaración | `#b3` | `ctxDlgSinDecTul` | **`400`** | `"Tula sin declaración"` |
| Espera de aprobación | `C7`/`A2` | `ctxEsperandoTul` | **`400`** | §9.2 |
| Solicitud rechazada | `C8`/`A3` | `ctxRechazadaTul` | **`400`** | §9.4 |

Propiedades **idénticas en los cinco** (receta 7.2):

| Control | Tipo | X · Y · W · H | Propiedades |
|---|---|---|---|
| `cntDlg…Tul` | Contenedor de diseño manual | `0` · `0` · `Parent.Width` · `Parent.Height` | `Fill` `ThemeOverlay` · `Radius` (las 4) `0` · `BorderThickness` `0` · `Visible` = la columna 3 de la tabla de arriba |
| `btnBloq…Tul` | Botón clásico (hijo, **al FONDO** del contenedor) | `0` · `0` · `Parent.Width` · `Parent.Height` | `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · `TabIndex` `-1` · **`OnSelect` `false`** |
| `cntCard…Tul` | Contenedor de diseño manual (hijo) | `(Parent.Width - Self.Width) / 2` · `(Parent.Height - Self.Height) / 2` · la columna 4 · **encadenada** (ver abajo) | `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DropShadow` `DropShadow.Regular` |

> 🧨 **La altura de la card se encadena hacia ABAJO, no al revés.** La receta 7.2 ofrece
> `btnDlgCancel….Y = cntCard….Height - 51`; esta guía usa la dirección de `cmpDialogo`
> (`01` §8.3): **`cntCard….Height = <último botón>.Y + <último botón>.Height + 19`**, con
> cada bloque encadenado al anterior. Motivo: los subtítulos llevan `AutoHeight true` y
> los cuerpos tienen alturas variables (la galería de `B1` crece con la cantidad de
> candidatas); con la altura fija primero, el contenido se sale de la card. Divergencia
> **D03-11** — las dos formas son de la misma receta, pero **no se mezclan en un mismo
> diálogo**.
>
> **Los cinco contenedores van al TOPE del z-order**, en el orden del árbol.
> `btnBloq…Tul` con `OnSelect: false` traga los clics del fondo: **el clic en el velo NO
> cierra** (`L-17`) — todos estos diálogos deciden qué tula se recuenta o disparan una
> solicitud al supervisor.
>
> **Reset al ABRIR, no al cerrar** (receta 7.2, regla 3): `B1` y `B2` no tienen campos de
> texto, pero sí centinelas de selección, y **`btnAbrirTul.OnSelect` los deja en
> `Defaults(…)` antes de prender el diálogo** (§6.1, paso 1). Es el mismo principio.
>
> **CONFIRMAR ANTES DE EJECUTAR** (receta 7.2, regla 2): ni un `Patch`, ni un `Clear`, ni
> un `Remove` en el botón que **abre** un diálogo. El `Patch` de la bolsa vive en
> `btnAbrirNuevaTul` y el de la solicitud en el confirmar de `B3`.

### 10.2 `B1` — Seleccione la tula recibida (mockup `#b1`)

Columna de contenido de la card: `X = 22`, `Width = 516`.

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `lblDlgTitTulaTul` | Etiqueta clásica | `22` · `17` · `494` · `22` | `ESTILO-DLG-TITULO` |
| `lblDlgTxtTulaTul` | Etiqueta clásica | `22` · `lblDlgTitTulaTul.Y + lblDlgTitTulaTul.Height + 4` · `516` · — | `ESTILO-DLG-TEXTO` · `AutoHeight` `true` · `Text` ver abajo |
| `icoDlgXTulaTul` | Icono clásico `Icon.Cancel` | `cntCardTulaTul.Width - 36` · `17` · `18` · `18` | `Color` `ThemeInk3` · `HoverColor` `ThemeInk` · `TabIndex` `0` · **`AccessibleLabel` `"Cerrar el diálogo"`** · `OnSelect` **el mismo de `btnDlgCancelTulaTul`** |
| `galPickTulasTul` | **Galería vertical en blanco** | `22` · `lblDlgTxtTulaTul.Y + lblDlgTxtTulaTul.Height + 13` · `516` · `Min(CountRows(colTulasCandidatasTul), 3) * 74` | `TemplateSize` `74` · `TemplatePadding` `0` · `ShowScrollbar` `true` · `Items` `colTulasCandidatasTul` · `OnSelect` ver abajo |
| `btnAvisoTulaTul` | Botón clásico (receta 7.2.a) | `22` · `galPickTulasTul.Y + galPickTulasTul.Height + 11` · `516` · `40` | `Fill`/`HoverFill`/`PressedFill` `ThemeInfoSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAvisoTulaTul` | Icono clásico `Icon.Information` | `btnAvisoTulaTul.X + 13` · `btnAvisoTulaTul.Y + 12` · `16` · `16` | `Color` `ThemeInfo` · `Visible` `btnAvisoTulaTul.Visible` |
| `lblAvisoTulaTul` | Etiqueta clásica | `btnAvisoTulaTul.X + 38` · `btnAvisoTulaTul.Y` · `466` · `40` | Size `9` · `Color` `ThemeInfo` · `Wrap` `true` · `Text` `"Solo las tulas Almacenadas en bóveda pueden abrirse para reconteo."` · `Visible` `btnAvisoTulaTul.Visible` |
| `btnDlgCancelTulaTul` | Botón clásico | `btnDlgOkTulaTul.X - 8 - Self.Width` · `btnDlgOkTulaTul.Y` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-FANTASMA` · `Text` `"Cancelar"` · `OnSelect` ver abajo |
| `btnDlgOkTulaTul` | Botón clásico | `cntCardTulaTul.Width - 22 - Self.Width` · `btnAvisoTulaTul.Y + btnAvisoTulaTul.Height + 15` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-PRIMARIO` · `Text` `"Abrir tula"` · `DisplayMode` y `OnSelect` ver abajo |

**Controles del template de `galPickTulasTul`** (`X` relativo al template, que arranca
en `0`; la *pick card* mide `516 × 63` y el resto del `TemplateSize` es el `gap 11` del
mockup):

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnPickTulaTul` | Botón clásico (receta 7.1) — **al FONDO** | `0` · `0` · `516` · `63` | `Text` `""` · `Radius` (las 4) `11` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `TabIndex` `0` · `Fill` / `HoverFill` / `PressedFill` / `BorderColor` ver abajo |
| `lblPickTulaTitTul` | Etiqueta clásica | `14` · `9` · `340` · `18` | `ESTILO-CELDA-FUERTE` · `Color` `If(ThisItem.BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda', ThemeInk, ThemeInk3)` · `Text` `ThisItem.BagID & " · Planilla " & ThisItem.ReceiptIdMirror` · `OnSelect` `Select(Parent)` |
| `lblPickTulaMetaTul` | Etiqueta clásica | `14` · `27` · `340` · `16` | `ESTILO-META-CHICA` · `Text` ver abajo · `OnSelect` `Select(Parent)` |
| `lblPickTulaMotivoTul` | Etiqueta clásica | `14` · `43` · `340` · `16` | Size `8.25` · `Color` `ThemeWarn` · `Text` ver abajo · `Visible` `ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'` · `OnSelect` `Select(Parent)` |
| `pilPickTulaTul` | Botón clásico — **píldora, receta 7.4** | `516 - 14 - Self.Width` · `20` · `14 + 6.2 * Len(Self.Text)` · `22` | los tres `Switch` de `BagStatus` de la receta 7.4 · `DisplayMode` `DisplayMode.Edit` + `OnSelect` `Select(Parent)` |

**`lblDlgTxtTulaTul.Text`:**

```powerfx
"Hay " & CountRows(colTulasCandidatasTul) & " tulas con el número " & ctxScanTul &
" en el sistema. Elija la que tiene físicamente en la mesa."
```

**`lblPickTulaMetaTul.Text`:**

```powerfx
ThisItem.ReceptionID.RemitentName & "  ·  Recibida " &
Text(ThisItem.'Created On', FmtFechaCorta) & "  ·  Lote " & ThisItem.ReceptionBatchId.Name
```

**`lblPickTulaMotivoTul.Text`:**

```powerfx
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',  "No seleccionable: todavía está en esclusa, sin almacenar en bóveda.",
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',    "No seleccionable: ya está asignada a un cajero.",
    "No seleccionable en este estado."
)
```

**`btnPickTulaTul.Fill`** (y `HoverFill` / `PressedFill`, ver la nota):

```powerfx
If(
    ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda', ThemeSurface2,
    ThisItem.'CH-Tbl-Bags' = ctxPickTulaTul.'CH-Tbl-Bags',                  ThemeAccentSoft,
    ThemeSurface
)
```

**`btnPickTulaTul.HoverFill`** y **`.PressedFill`**:

```powerfx
If(
    ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda', ThemeSurface2,
    ThemeAccentSoft
)
```

**`btnPickTulaTul.BorderColor`:**

```powerfx
If(
    ThisItem.BagStatus <> 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda', ThemeLineSoft,
    ThisItem.'CH-Tbl-Bags' = ctxPickTulaTul.'CH-Tbl-Bags',                  ThemeAccent,
    ThemeLine
)
```

**`galPickTulasTul.OnSelect`** — el ÚNICO handler de la fila:

```powerfx
If(
    ThisItem.BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',
    UpdateContext({ ctxPickTulaTul: ThisItem })
)
```

**`btnDlgOkTulaTul.DisplayMode`:**

```powerfx
If(
    gvarIdentidadOk && !IsBlank(ctxPickTulaTul.'CH-Tbl-Bags'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

**`btnDlgOkTulaTul.OnSelect`:**

```powerfx
UpdateContext({ ctxDlgTulaTul: false, ctxTulaElegidaTul: ctxPickTulaTul });
Select(btnResolverDeclaracionTul)
```

**`btnDlgCancelTulaTul.OnSelect`** (y, literalmente igual, `icoDlgXTulaTul.OnSelect`):

```powerfx
UpdateContext({ ctxDlgTulaTul: false });
Reset(inpScanTul);
SetFocus(inpScanTul)
```

> **`Almacenadas en bóveda` va con A mayúscula**: no es un adjetivo, es la **etiqueta exacta
> del option set** `'BagStatus (CH-Tbl-Bags)'` (`01` §5.1) — la misma que el cajero lee en la
> píldora de cada fila. En minúscula parece una descripción y deja de ser buscable.
>
> **Se pierde el énfasis**: el mockup escribe `Solo las tulas <b>Almacenadas en bóveda</b>
> pueden abrirse`. Una **Etiqueta clásica tiene un solo `FontWeight`** y este texto entra en
> el banner de la receta 7.2.a, que es **una** etiqueta: partirlo en dos —el patrón de
> `D03-13`— exigiría tres etiquetas con anchos calculados a mano dentro de un banner de 40
> px de alto y `Wrap true`, que es peor que perder la negrita. Divergencia **`D03-13`**,
> misma familia.

> 🧨 **Una fila no seleccionable NO cambia el centinela**, y el `OnSelect` de la galería
> es el único lugar donde eso se decide. El `DisplayMode.Edit` de `btnPickTulaTul` es
> necesario para que la superficie delegue el clic (receta 7.1), pero el clic muere en el
> `If` de la galería.
>
> **El botón nace DESHABILITADO**: `ctxPickTulaTul` arranca en `Defaults('CH-Tbl-Bags')`
> y el mockup dibuja la primera tarjeta con `pickSel` **solo como ilustración**. Elegir
> la tula equivocada arruina el arqueo de toda la tula: acá no hay preselección.
> Divergencia **D03-4**.
>
> **La tarjeta no seleccionable no lleva borde punteado** (`.pickOff` usa
> `border-style: dashed`, que el Botón clásico no expone) **ni nombra al cajero y la mesa
> que la tienen** (`RecountingSession.Cashier.'Full Name'` y `.CountingDesk.Name` son
> dot-walks de **dos** saltos, regla **D-1**, y exigirían dos datasources más). El motivo
> se dice en palabras. Divergencia **D03-5**.
>
> **`ThisItem.ReceptionBatchId.Name` es la razón por la que `CH-Tbl-ReceptionBatches`
> NO es datasource** (`01` §2.1): navegar un lookup sobre un registro ya cargado **no
> exige** la tabla del otro lado.
>
> **Cancelar devuelve el foco al escáner** (receta 7.8, regla 3): el cajero encadena
> tulas sin tocar el mouse.

### 10.3 `B2` — Seleccione la declaración (mockup `#b2`)

Misma geometría que `B1` (card `560`, contenido en `X 22 / W 516`). Solo cambian el
cuerpo de las tarjetas, el banner y los rótulos.

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `lblDlgTitDeclTul` | Etiqueta clásica | `22` · `17` · `494` · `22` | `ESTILO-DLG-TITULO` |
| `lblDlgTxtDeclTul` | Etiqueta clásica | `22` · `lblDlgTitDeclTul.Y + lblDlgTitDeclTul.Height + 4` · `516` · — | `ESTILO-DLG-TEXTO` · `AutoHeight` `true` · `Text` ver abajo |
| `icoDlgXDeclTul` | Icono clásico `Icon.Cancel` | `cntCardDeclTul.Width - 36` · `17` · `18` · `18` | `Color` `ThemeInk3` · `HoverColor` `ThemeInk` · `TabIndex` `0` · **`AccessibleLabel` `"Cerrar el diálogo"`** · `OnSelect` **el mismo de `btnDlgCancelDeclTul`** |
| `galPickDeclTul` | **Galería vertical en blanco** | `22` · `lblDlgTxtDeclTul.Y + lblDlgTxtDeclTul.Height + 13` · `516` · `Min(CountRows(colDeclaradasCandidatasTul), 3) * 74` | `TemplateSize` `74` · `TemplatePadding` `0` · `ShowScrollbar` `true` · `Items` `colDeclaradasCandidatasTul` · `OnSelect` `UpdateContext({ ctxPickDeclTul: ThisItem })` |
| `btnAvisoDeclTul` | Botón clásico (receta 7.2.a) | `22` · `galPickDeclTul.Y + galPickDeclTul.Height + 11` · `516` · `58` | `Fill`/`HoverFill`/`PressedFill` **`ThemeWarnSoft`** · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAvisoDeclTul` | Icono clásico **`Icon.Warning`** | `btnAvisoDeclTul.X + 13` · `btnAvisoDeclTul.Y + 12` · `16` · `16` | `Color` `ThemeWarn` · `Visible` `btnAvisoDeclTul.Visible` |
| `lblAvisoDeclTul` | Etiqueta clásica | `btnAvisoDeclTul.X + 38` · `btnAvisoDeclTul.Y` · `466` · `58` | Size `9` · `Color` `ThemeWarn` · `Wrap` `true` · `Text` `"La declaración elegida define lo Declarado de toda la tula. Si elige la incorrecta, todas las diferencias van a estar mal."` · `Visible` `btnAvisoDeclTul.Visible` |
| `btnDlgCancelDeclTul` | Botón clásico | `btnDlgOkDeclTul.X - 8 - Self.Width` · `btnDlgOkDeclTul.Y` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-FANTASMA` · `Text` `"Cancelar"` · `OnSelect` ver abajo |
| `btnDlgOkDeclTul` | Botón clásico | `cntCardDeclTul.Width - 22 - Self.Width` · `btnAvisoDeclTul.Y + btnAvisoDeclTul.Height + 15` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-PRIMARIO` · `Text` `"Usar esta declaración"` · `DisplayMode` y `OnSelect` ver abajo |

**Controles del template de `galPickDeclTul`** (*pick card* de `516 × 63`):

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnPickDeclTul` | Botón clásico (receta 7.1) — **al FONDO** | `0` · `0` · `516` · `63` | `Text` `""` · `Radius` (las 4) `11` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` · `TabIndex` `0` · `Fill` `If(ThisItem.'CH-Tbl-DeclaredBags' = ctxPickDeclTul.'CH-Tbl-DeclaredBags', ThemeAccentSoft, ThemeSurface)` · `HoverFill`/`PressedFill` `ThemeAccentSoft` · `BorderColor` `If(ThisItem.'CH-Tbl-DeclaredBags' = ctxPickDeclTul.'CH-Tbl-DeclaredBags', ThemeAccent, ThemeLine)` |
| `lblPickDeclTitTul` | Etiqueta clásica | `14` · `11` · `330` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.ReceptionID.RemitentName` · `OnSelect` `Select(Parent)` |
| `lblPickDeclMetaTul` | Etiqueta clásica | `14` · `29` · `330` · `32` | `ESTILO-META-CHICA` · `Wrap` `true` · `Text` ver abajo · `OnSelect` `Select(Parent)` |
| `lblPickDeclMontoTul` | Etiqueta clásica | `362` · `13` · `140` · `18` | Size `9.75` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Right` · `Text` `fxMonto(ThisItem.TotalNIO, SimboloNIO)` · `OnSelect` `Select(Parent)` |
| `lblPickDeclMonto2Tul` | Etiqueta clásica | `362` · `lblPickDeclMontoTul.Y + lblPickDeclMontoTul.Height` · `140` · `16` | `ESTILO-META-CHICA` · `Align` `Align.Right` · `Text` ver abajo · `OnSelect` `Select(Parent)` |

**`lblDlgTxtDeclTul.Text`:**

```powerfx
"El cliente " & ctxTulaElegidaTul.ReceptionID.RemitentName & " tiene " &
CountRows(colDeclaradasCandidatasTul) & " declaraciones vigentes para la tula " &
ctxScanTul & "."
```

**`lblPickDeclMetaTul.Text`:**

```powerfx
"Declarada " & Text(ThisItem.'Created On', FmtFecha) & "  ·  " &
Text(ThisItem.TotalDeposits) & " depósitos declarados"
```

**`lblPickDeclMonto2Tul.Text`:**

```powerfx
fxMonto(ThisItem.TotalUSD, SimboloUSD) & "  ·  " & fxMonto(ThisItem.TotalEUR, SimboloEUR)
```

**`btnDlgOkDeclTul.DisplayMode`:**

```powerfx
If(
    gvarIdentidadOk && !IsBlank(ctxPickDeclTul.'CH-Tbl-DeclaredBags'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

**`btnDlgOkDeclTul.OnSelect`:**

```powerfx
UpdateContext({ ctxDlgDeclTul: false, ctxDeclElegidaTul: ctxPickDeclTul });
Select(btnAbrirNuevaTul)
```

**`btnDlgCancelDeclTul.OnSelect`** (y `icoDlgXDeclTul.OnSelect`, idéntico):

```powerfx
UpdateContext({ ctxDlgDeclTul: false });
Reset(inpScanTul);
SetFocus(inpScanTul)
```

> 🧨 **El cliente sale de la TULA, no de la declaración** (`L-9`): `CH-Tbl-DeclaredBags`
> no lo tiene, y llegar a él exigiría `DeclarationID.…` (dos saltos). Por eso las dos
> tarjetas muestran el mismo cliente — igual que el mockup, que las dibuja idénticas.
>
> 🧨 **Ni origen del correo ni conteo de cheques** (`L-9` y `L-20`): el remitente exige
> `CH-Tbl-Received Emails` (tabla grande) y un dot-walk de **dos** saltos; el conteo de
> cheques exige cargar `CH-Tbl-DeclaredChecks` **antes** de que el cajero haya elegido
> declaración. La tarjeta conserva lo que **sí** decide: cliente, fecha, `TotalDeposits`
> (rollup nativo) y el monto declarado por moneda.
>
> **El renglón chico agrega EUR** — el mockup solo dibuja USD. Una declaración con euros
> quedaría invisible justo en la pantalla donde se elige contra qué se va a comparar toda
> la tula. Divergencia **D03-6**.
>
> **`TotalDeposits` es un ROLLUP de solo lectura** (`01` §2.4). Se muestra, no se toca.
>
> **`Items` es la colección local** (`colDeclaradasCandidatasTul`, regla **D-4**): las
> candidatas se resolvieron con **una** consulta delegable en §6.1 y esta galería no
> vuelve a la red.

### 10.4 `B3` — Tula sin declaración (mockup `#b3`)

Card de **400** ⇒ contenido en `X 22 / W 356`. Lleva glifo de estado (receta 7.2),
resumen `kv` (receta 7.2.b) y banner (receta 7.2.a).

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnIcoSinDecTul` | Botón clásico (receta 7.1) | `22` · `17` · `34` · `34` | `Radius` (las 4) `9` · `Fill`/`HoverFill`/`PressedFill` `ThemeWarnSoft` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoTipoSinDecTul` | Icono clásico `Icon.Lock` (el 🔒 del mockup `B3`: **candado real**, `01` §6.1) | `btnIcoSinDecTul.X + 8` · `btnIcoSinDecTul.Y + 8` · `18` · `18` | `Color` `ThemeWarn` |
| `lblDlgTitSinDecTul` | Etiqueta clásica | `btnIcoSinDecTul.X + btnIcoSinDecTul.Width + 10` · `17` · `290` · `22` | `ESTILO-DLG-TITULO` |
| `lblDlgTxtSinDecTul` | Etiqueta clásica | `lblDlgTitSinDecTul.X` · `lblDlgTitSinDecTul.Y + lblDlgTitSinDecTul.Height + 4` · `312` · — | `ESTILO-DLG-TEXTO` · `AutoHeight` `true` · `Text` ver abajo |
| `icoDlgXSinDecTul` | Icono clásico `Icon.Cancel` | `cntCardSinDecTul.Width - 36` · `17` · `18` · `18` | `Color` `ThemeInk3` · `HoverColor` `ThemeInk` · `TabIndex` `0` · **`AccessibleLabel` `"Cerrar el diálogo"`** · `OnSelect` **el mismo de `btnDlgCancelSinDecTul`** |
| `lblKvTulaEtqSinDecTul` | Etiqueta clásica | `22` · `lblDlgTxtSinDecTul.Y + lblDlgTxtSinDecTul.Height + 12` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Tula"` |
| `lblKvTulaSinDecTul` | Etiqueta clásica | `140` · `lblKvTulaEtqSinDecTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.BagID` |
| `lblKvPlanEtqSinDecTul` | Etiqueta clásica | `22` · `lblKvTulaEtqSinDecTul.Y + 22` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Planilla"` |
| `lblKvPlanSinDecTul` | Etiqueta clásica | `140` · `lblKvPlanEtqSinDecTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.ReceiptIdMirror` |
| `lblKvCliEtqSinDecTul` | Etiqueta clásica | `22` · `lblKvPlanEtqSinDecTul.Y + 22` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Cliente"` |
| `lblKvCliSinDecTul` | Etiqueta clásica | `140` · `lblKvCliEtqSinDecTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `ctxTulaElegidaTul.ReceptionID.RemitentName` |
| `lblKvMesaEtqSinDecTul` | Etiqueta clásica | `22` · `lblKvCliEtqSinDecTul.Y + 22` · `110` · `18` | `ESTILO-META-CHICA` · `Text` `"Mesa"` |
| `lblKvMesaSinDecTul` | Etiqueta clásica | `140` · `lblKvMesaEtqSinDecTul.Y` · `238` · `18` | `ESTILO-CELDA-FUERTE` · `Text` `gvarMesa.Name` |
| `btnAvisoSinDecTul` | Botón clásico (receta 7.2.a) | `22` · `lblKvMesaEtqSinDecTul.Y + lblKvMesaEtqSinDecTul.Height + 14` · `356` · `58` | `Fill`/`HoverFill`/`PressedFill` `ThemeWarnSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAvisoSinDecTul` | Icono clásico **`Icon.Warning`** | `btnAvisoSinDecTul.X + 13` · `btnAvisoSinDecTul.Y + 12` · `16` · `16` | `Color` `ThemeWarn` · `Visible` `btnAvisoSinDecTul.Visible` |
| `lblAvisoSinDecTul` | Etiqueta clásica | `btnAvisoSinDecTul.X + 38` · `btnAvisoSinDecTul.Y` · `306` · `58` | Size `9` · `Color` `ThemeWarn` · `Wrap` `true` · `Text` `"Se enviará una solicitud de aprobación al supervisor. La tula queda bloqueada hasta que responda."` · `Visible` `btnAvisoSinDecTul.Visible` |
| `btnDlgCancelSinDecTul` | Botón clásico | `btnDlgOkSinDecTul.X - 8 - Self.Width` · `btnDlgOkSinDecTul.Y` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-FANTASMA` · `Text` `"Cancelar"` · `OnSelect` ver abajo |
| `btnDlgOkSinDecTul` | Botón clásico | `cntCardSinDecTul.Width - 22 - Self.Width` · `btnAvisoSinDecTul.Y + btnAvisoSinDecTul.Height + 15` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-PRIMARIO` · `Text` `"Enviar solicitud"` · `DisplayMode` `If(gvarIdentidadOk && !IsBlank(ctxTulaElegidaTul.'CH-Tbl-Bags'), DisplayMode.Edit, DisplayMode.Disabled)` · `OnSelect` ver abajo |

**`lblDlgTxtSinDecTul.Text`:**

```powerfx
"La tula " & ctxTulaElegidaTul.BagID & " no fue declarada por el cliente " &
ctxTulaElegidaTul.ReceptionID.RemitentName & "."
```

**`btnDlgOkSinDecTul.OnSelect`** — la solicitud al supervisor (`01` §5.7, forma exacta):

```powerfx
UpdateContext({ ctxDlgSinDecTul: false });

IfError(
    Set(
        gvarSolicitud,
        Patch(
            'CH-Tbl-ApprovalRequests',
            Defaults('CH-Tbl-ApprovalRequests'),      // Name es AUTONUMBER: no se toca
            {
                RequestType:    "Tula sin declaracion",
                BagNumber:      Trim(ctxTulaElegidaTul.BagID),
                RequestDetails: fxHtmlSinDeclaracion(
                                    Trim(ctxTulaElegidaTul.BagID),
                                    ctxTulaElegidaTul.ReceiptIdMirror,
                                    ctxTulaElegidaTul.ReceptionID.RemitentName,
                                    gvarUser.'Full Name',
                                    gvarMesa.Name,
                                    Text(Now(), FmtFecha)
                                ),
                RequestResources: JSON({
                    BagGUID:               Text(ctxTulaElegidaTul.'CH-Tbl-Bags'),
                    RecountingSessionGUID: Text(gvarSesion.'CH-Tbl-RecountingSession')
                })
                // ApprovalStatus NO se setea: en blanco = Pendiente (01 §5.5)
            }
        )
    ),
    If(true,
        Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error);
        Set(gvarSolicitud, Blank());
        Blank()
    )
);

If(
    IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest'),
    Reset(inpScanTul); SetFocus(inpScanTul),
    UpdateContext({ ctxEsperandoTul: true, ctxRechazadaTul: false })
)
```

**`btnDlgCancelSinDecTul.OnSelect`** (y `icoDlgXSinDecTul.OnSelect`, idéntico):

```powerfx
UpdateContext({ ctxDlgSinDecTul: false });
Reset(inpScanTul);
SetFocus(inpScanTul)
```

> 🧨 **El `Patch` va envuelto en un `Set`**: el registro que devuelve —con su PK— es lo
> que el polling usa para preguntar por PK (`01` §5.8). Si el `Patch` falla,
> `gvarSolicitud` queda en `Blank()`, el timer **no arranca** y la pantalla no se queda
> esperando una solicitud que nunca existió.
>
> 🧨 **`ctxTulaElegidaTul`, NO `gvarTula`.** `01` §5.7 escribe este mismo bloque contra
> `gvarTula`, pero en `03` la tula **todavía no es del cajero**: el `Patch` de la bolsa
> ocurre **después** de la aprobación (§6.3). La sustitución es **mecánica y total** —
> las cuatro apariciones de `gvarTula` pasan a `ctxTulaElegidaTul`. Divergencia
> **D03-12**.
>
> 🧨 **Cero escapes manuales acá.** `HtmlEscape()` vive **dentro** de `fxHtmlSinDeclaracion`
> y sus `fxHtmlKv` (`01` §3.1, regla 1). Un `Substitute(…, "&", "&amp;")` en esta pantalla
> significaría que el dato se escapa **dos veces** y el supervisor ve basura. La inyección
> HTML de la v1 (**S-3**) estaba confirmada: la v1 concatenaba `RemitentName` y
> `ReceiptID` **sin escapar**, y armaba el `RequestResources` con `Concatenate` y
> comillas dobles duplicadas en vez de `JSON()`.
>
> **Las claves del JSON son EXACTAS y en ese orden** (`01` §5.7, contrato con
> CH-App-Supervisor): `BagGUID`, `RecountingSessionGUID`. El guard server-side las
> inspecciona y **traza el desvío**.
>
> **`BagNumber` se escribe** (`L-18`): le permite al supervisor filtrar por N° de tula
> sin parsear el JSON, y es la columna sobre la que §6.2 hace la búsqueda de duplicados.

---

## 11. Notas de la guía

### 11.1 Mapa de consultas — todas delegables, todas contra 500 filas

| # | Dónde | Consulta | Veredicto |
|---|---|---|---|
| Q-1 | `btnCargarSesionTul` (§5.2) | `SortByColumns(Filter('CH-Tbl-Bags', RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.…), "modifiedon", Descending)` | ✅ **PK contra PK** (D-1) + `SortByColumns` explícito (D-9) |
| Q-2 | `btnCargarSesionTul` (§5.2) | `Filter('CH-Tbl-DepositsRecountings', RecountingSessionMirror.'CH-Tbl-RecountingSession' = gvarSesion.…)` | ✅ **con el espejo E-1**. 🔴 Hoy **no existe** ⇒ fallback N+1 acotado, declarado en §5.2 |
| Q-3 | `btnAbrirTul` (§6.1) | `Filter('CH-Tbl-Bags', BagID = ctxScanTul && (BagStatus = … \|\| … \|\| …))` | ✅ igualdad de texto + `in`-menos-`OR` de option set. **Cero `Upper()`** (D-7) |
| Q-4 | `btnAbrirTul` (§6.1) | `Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul && IsBlank(BagID.'CH-Tbl-Bags'))` | ✅ igualdad + `eq null` **contra la PK del lookup** (`D-13`). 🚫 `IsBlank(BagID)` pelado **compila y revienta en runtime** (`L-61`). Fallback client-side declarado en §6.1 |
| Q-5 | `btnResolverDeclaracionTul` (§6.2) | `Sort(Filter('CH-Tbl-ApprovalRequests', RequestType = … && BagNumber = … && 'Created By'.User = gvarUser.User && 'Created On' >= Today()), 'Created On', Descending)` | ✅ 4 predicados delegables (uno **PK contra PK**) + `Sort`/`First` (D-9) |
| Q-6 | `btnAbrirNuevaTul` (§6.3) · `btnReabrirTul` (§6.4) | `LookUp('CH-Tbl-Bags', 'CH-Tbl-Bags' = <PK>)` | ✅ `eq` de GUID |
| Q-7 | `btnReabrirTul` (§6.4) | `LookUp('CH-Tbl-DeclaredBags', 'CH-Tbl-DeclaredBags' = gvarTula.BagDeclarationID.'CH-Tbl-DeclaredBags')` | ✅ `eq` de GUID (la PK del lookup es anotación del registro ya cargado) |
| Q-8 | `btnEntrarConteoTul` (§7) | `LookUp('CH-Tbl-RecountingSessionDetails', BagId.'CH-Tbl-Bags' = … && RecountingSessionId.'CH-Tbl-RecountingSession' = …)` | ✅ **PK contra PK en las dos condiciones** |
| Q-9 | `tmrPollTul` (§9.1) | `LookUp('CH-Tbl-ApprovalRequests', 'CH-Tbl-ApprovalRequest' = gvarSolicitud.…)` | ✅ `eq` de GUID, una por tick |

**Cero `Sum`/`CountRows` sobre datasource** (D-2): los seis totales del rail y los cuatro
contadores del panel corren sobre `colDepositosSesionTul` / `colTulasSesionTul`.
**Cero `AllItems`** (D-8). **Cero `RemoveIf`/`UpdateIf`** (D-3): esta pantalla no borra
nada. **Cero `Set()` dentro de `ForAll`** (D-12): el único `ForAll` de la guía es el
fallback de `E-1`, y acumula con `Collect`.

### 11.2 Divergencias declaradas (`D03-*`)

| ID | Qué | Por qué | Dónde |
|---|---|---|---|
| **D03-1** | ✅ **RESUELTA por consolidación → ver `01` §5.8.** Ya **no** es divergencia: el contrato madre fija un timer por pantalla que crea o espera una solicitud, y `03` crea `"Tula sin declaracion"` | — | §9.1 |
| **D03-2** | La columna del mockup **"Apertura"** se llama **"Actividad"** y muestra `Modified On` | **No existe un sello de apertura** en `CH-Tbl-Bags`. El único dato delegable sin una quinta colección es `Modified On`, que para una tula abierta ≈ la apertura y para una recontada es el cierre. Aproximación **honesta**, no un dato real. Camino de mejora en §11.4 | §5.1, §5.3 |
| **D03-3** | El estado vacío **no lleva el glifo `⛁`** del mockup | El catálogo de iconos de `01` §6.1 es **cerrado** y no tiene equivalente; como texto sale *tofu* en Open Sans | §5.3 |
| **D03-4** | `B1` y `B2` **no pre-seleccionan** ninguna tarjeta; el mockup dibuja la primera con `pickSel` | Elegir la tula o la declaración equivocada arruina el arqueo entero. Centinela `ctx…` en `Defaults(<tabla>)` y botón deshabilitado hasta el clic explícito (excepción declarada de la receta 7.3) | §0.2, §10.2, §10.3 |
| **D03-5** | La tarjeta no seleccionable de `B1` **no tiene borde punteado** ni **nombra al cajero/mesa** que la tiene | El Botón clásico no expone `border-style: dashed`; `RecountingSession.Cashier.'Full Name'` y `.CountingDesk.Name` son dot-walks de **dos** saltos (**D-1**) y exigirían dos datasources más. El motivo se dice en palabras | §6.1, §10.2 |
| **D03-6** | `B2` **no muestra origen del correo ni conteo de cheques** (`L-9`), y **agrega el total EUR** al renglón chico (el mockup solo dibuja USD) | El remitente exige `CH-Tbl-Received Emails` + 2 saltos (`L-20`) y el conteo de cheques exige cargar `DeclaredChecks` antes de elegir. El EUR se agrega porque una declaración en euros quedaría **invisible** justo donde se elige contra qué se compara la tula | §10.3 |
| **D03-7** | El chip de mesa muestra **solo `gvarMesa.Name`** | El `· Bóveda B` del mockup es una ubicación que **no existe** en `CH-Tbl-CountingDesks` (`01` §2.4) | §2 |
| **D03-8** | Los KPI usan `!IsBlank(LookUp(…).'CH-Tbl-Bags')`; `01` §8.2 escribe `!IsBlank(LookUp(…))` | `IsBlank()` sobre un **registro** tipado vacío puede dar `false` ⇒ el KPI sumaría depósitos de tulas **no recontadas**, que es justo lo que el rail promete filtrar. Mismo criterio de PK que el resto de la guía | §3 |
| **D03-9** | Los **seis** motores (los cinco de la apertura + `btnIrSesionTul`, el de salida) son **botones de 1 × 1 transparentes**, no `Visible: false` | `Select()` sobre un control **oculto** no está garantizado por la plataforma; con la cadena de motores de §0.1, un `Select()` que no dispara deja la apertura a la mitad **en silencio** —o, en `btnIrSesionTul`, al cajero atrapado en un hub sin sesión— | §1.1, §6 |
| **D03-10** | ✅ **RESUELTA por consolidación → ver `01` §4.5.** `Set(gvarSolicitud, Blank())` es ahora parte del contrato canónico de `01` y del CONTRATO DE ENTRADA de `04` | — | §1, §7, §8 |
| **D03-11** | La altura de las cards de diálogo se **encadena hacia abajo** (`cntCard.Height = <último botón>.Y + Height + 19`), no `Y = cntCard.Height − 51` | Los subtítulos llevan `AutoHeight true` y la galería de `B1`/`B2` crece con la cantidad de candidatas: con la altura fija primero, el contenido se sale de la card. Las dos formas son de la misma receta (`01` §7.2 vs. §8.3) y **no se mezclan** en un mismo diálogo | §10.1 |
| **D03-12** | La solicitud `"Tula sin declaracion"` se arma contra **`ctxTulaElegidaTul`**; `01` §5.7 la escribe contra `gvarTula` | En `03` la tula **todavía no es del cajero**: el `Patch` de la bolsa ocurre **después** de la aprobación. Sustitución mecánica y total en las 4 apariciones | §10.4 |
| **D03-13** | El pie del rail son **tres etiquetas** y la fecha usa `FmtFechaCorta` (`16/08/2026`); el mockup escribe `16 ago 2026` con la fecha en negrita dentro de la línea | Una Etiqueta clásica tiene **un solo** `FontWeight`, y `01` §3.1 define **tres** formatos de fecha: inventar un `"dd mmm yyyy"` acá sería un cuarto formato fuera del inventario | §2 |
| **D03-14** | La salida de la espera se llama **"Cancelar la espera"**, no *"Cancelar solicitud"*, y **no anula** la solicitud | La app **nunca escribe `ApprovalStatus`** (`01` §5.5, mata **S-1**): no tiene forma de anular una solicitud. Prometerlo sería mentir | §9.3 |
| **D03-15** | Antes de crear una solicitud `"Tula sin declaracion"`, `03` **busca la previa** (una consulta delegable + verificación client-side de los dos GUID con `Find()`) y la **re-adopta** si sigue pendiente o la **consume** si ya está aprobada | Cancelar la espera deja la solicitud viva. Sin esto, volver a escanear la misma tula le manda al supervisor una segunda solicitud idéntica | §6.2 |
| **D03-16** | El `⟳ Actualizar` son **dos controles** (Botón clásico + Icono clásico), no un botón con el glifo en el `Text` | Los glifos fuera de Open Sans salen como caja vacía (`01` §6.1): **todo glifo es un Icono clásico** | §5.1 |
| **D03-17** | El subtítulo del panel dice `"0 tulas"` (sin *"· 0 recontadas"*) cuando la lista está vacía | Es literal del mockup `#b0-vacio`, y evita el ridículo de *"0 tulas · 0 recontadas"* | §5.1 |

### 11.3 Lo que esta pantalla mata de la v1

| Bug de la v1 | Cómo muere acá |
|---|---|
| **D-2** — 12 `Sum(Filter(ds, BagID.RecountingSession.… && BagID.BagStatus = …))` de 2 saltos en el panel lateral: **cifras parciales de dinero sin aviso** | §3: las seis sumas corren sobre `colDepositosSesionTul` y `colTulasSesionTul`, colecciones locales cargadas con **una** consulta delegable cada una |
| **`RecountingSessionDetail` creado en 1 de 4 rutas** ⇒ contadora sin `ID_Sesion`, columna **Auto** en cero, cajero recontando a ciegas | §7: `btnEntrarConteoTul` es el **único** lugar que lo crea o lo recupera, y **si falta, no navega** |
| `ClearCollect` **auto-referencial** adentro de un `Concurrent` (`ClearCollect(col, Filter(col, IsBlank(<lookup>)))`) | §6.1: el predicado viaja **dentro** del `Filter` delegable y **contra la PK** (`IsBlank(BagID.'CH-Tbl-Bags')`, `D-13`); cada rama del `Concurrent` es **una** expresión |
| Máquina de estados `colSecurityBagSelectionState` con `UpdateIf(col, true, {State: …})` y cuatro copias de la lógica de apertura | §0.1: cinco motores, cada paso **una** vez. Cero colección de estado: el estado es `ctx…` |
| **S-4** — el polling **fallaba abierto**: un rechazo liberaba igual que una aprobación | §9.1: dos ramas explícitas, `else = false`, y el rechazo abre un overlay **bloqueante** con el motivo |
| **S-3** — `RequestDetails` armado con `Concatenate` sin escapar y `RequestResources` con comillas dobles duplicadas | §10.4: `fxHtmlSinDeclaracion()` (que escapa adentro) y `JSON({…})` |
| **S-6** — `Notify` de debug en producción con el GUID de la sesión y el **serial de la contadora** | §7: ningún `Notify` de esta pantalla imprime GUIDs, seriales ni correos |
| `Refresh('CH-Tbl-Bags')` + `Refresh('CH-Tbl-DeclaredBags')` después de cada `Patch`, y un `LookUp` para releer lo que el `Patch` ya devolvió | §6.3: el estado local sale del `Patch`. Los únicos `Refresh` son el del ⟳ (D-5 forma 1) y los dos de la resolución del polling (forma 2) |
| Tulas sin filtro de estado en la lista y `Items` atado a un `If` sin `else` | §5.2/§5.3: `ClearCollect` con `SortByColumns` explícito e `Items` = colección |

### 11.4 Dudas — cuatro CERRADAS en la consolidación, dos abiertas

**Cerradas (con evidencia o con decisión del dueño del proceso):**

1. ✅ **`L-6` — DECIDIDO.** La v2 **no** crea `"Primera tula en planilla"`: abrir una tula
   **con** declaración va **directo** a `scrConteo`, sin aprobación y sin espera (§6.2,
   rama 3). Ya está comunicado al dueño del proceso y el ledger de `00` §6 lo registra como
   **DECIDIDO**, no como pendiente. Consecuencia aceptada: la transición
   `Almacenada en boveda → Asignada a cajero` deja de pasar por aprobación en el camino
   feliz. La v2 tampoco **espera** ese tipo (`01` §5.8, regla 5 de la recuperación); una
   solicitud legada todavía pendiente es un asunto del guard de `00` §2.2.
2. ✅ **¿Quién consume la aprobación de `"Tula sin declaracion"`? — LOS DOS, y está bien.**
   Verificado contra `CH-App-Supervisor`
   (`solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/Screen Approvals.fx.yaml`):
   al **aprobar** ese tipo, el supervisor **ya** hace
   `Patch('CH-Tbl-Bags', …, { BagStatus: 'Asignada a cajero', RecountingSession: …, BagDeclarationID: … })`
   **y** crea el `CH-Tbl-RecountingSessionDetails`. La rama *Aprobado* de §9.1 delega en
   `btnAbrirNuevaTul`, que **re-lee la bolsa por PK y verifica el estado antes de patchear**
   (§6.3): si el supervisor ya la movió, el `Patch` es **idempotente**; si mañana esa lógica
   se muda a un plugin y desaparece del canvas del supervisor, `03` sigue abriendo la tula.
   Y `btnEntrarConteoTul` **recupera** el detalle que creó el supervisor en vez de duplicarlo
   (§7, paso 1). **Decisión pragmática declarada**: se conserva el camino client-side —hoy
   no existe ningún plugin que haga la transición— y el guard de `00` §2.2 debe **tolerar y
   validar** esa escritura del cajero.
3. ✅ **¿Se puede reabrir una tula ya `Recontada` desde la lista? — SÍ, en SOLO LECTURA.**
   `galTulasTul.OnSelect` → `btnReabrirTul` (§6.4) **no patchea nada** y `btnEntrarConteoTul`
   (§7) **no bloquea** ningún `BagStatus`: el mockup dibuja el chevron en **todas** las
   filas. Quien gobierna la edición es `04` §0.2, con `ctxSoloLecturaCon` derivado de
   `gvarTula.BagStatus <> 'Asignada a cajero'` ⇒ una tula `Recontada` o
   `Pendiente de revision por supervisor - recuento` se abre y **todo** está deshabilitado.
   Si el negocio quisiera prohibirlo, se condiciona `galTulasTul.OnSelect` — hoy **no** se
   prohíbe.
4. ✅ **¿`IsBlank(<lookup>)` en `CH-Tbl-DeclaredBags`? — CERRADA POR RUNTIME: se escribe
   contra la PK, `IsBlank(BagID.'CH-Tbl-Bags')`.** La pregunta original era de delegación;
   la respondió el QA con algo peor. El lookup **pelado** (`IsBlank(BagID)`) **compila en
   verde, no marca delegación y el App Checker lo deja pasar**, pero en runtime el conector
   emite el `$expand` de la *navigation property* y pide su nombre en el `$select` interno:
   Dataverse contesta **`Could not find a property named 'sanic_Xxx'`** y el `ClearCollect`
   vuelve **vacío** ⇒ toda tula con declaración caía por la rama *"sin declaración"* (`B3`)
   y le pedía aprobación al supervisor. Con `BagID.'CH-Tbl-Bags'` la consulta pliega a un
   `eq null` de GUID y delega. Regla dura **`D-13`** de `01` §2.3, ledger **`L-61`**.
   El fallback client-side de §6.1 sigue escrito por si **la delegación** fallara —traer
   `Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul)`, acotado por número de tula, y
   filtrar en memoria—, pero **nunca** se quita la condición ni se vuelve al lookup pelado.

**Abiertas (dependen de backend, no de esta guía):**

5. **Sello real de apertura** (`D03-2`). Dos caminos, los dos fuera de esta guía: (a) una
   columna nueva `OpenedOn` en `CH-Tbl-Bags` escrita por el plugin al transicionar a
   `Asignada a cajero`; (b) una **quinta colección local** con los
   `CH-Tbl-RecountingSessionDetails` de la sesión (una consulta delegable, decenas de
   filas) y leer su `'Created On'` con un `LookUp` local en el template. (b) no requiere
   backend pero agrega una colección que `01` §4.2 no inventaría.
6. **Espejo `E-1` (`RecountingSessionMirror`) — 🔴 hoy NO existe.** Hasta que el backend
   lo despliegue, el rail arranca con el **fallback N+1** de §5.2. Verificalo con la
   prueba del checklist de `01` §9 antes de construir §5.2.

---

## 12. Checklist de salida del constructor

Verificable, sin interpretación. Todo ítem se prueba en Studio con la app corriendo.

**Estructura**

- [ ] El árbol de `scrTulas` tiene **143 controles**, todos con sufijo `Tul`, en el
      **orden exacto** de la sección *Árbol* (ese orden ES el z-order).
- [ ] Los tres `cmpKpiSesion` del rail son **instancias del componente** de `01` §8.2, no
      etiquetas sueltas.
- [ ] Los **cinco contenedores de diálogo** están al **tope** del z-order y en este orden:
      `cntDlgTulaTul`, `cntDlgDeclTul`, `cntDlgSinDecTul`, `cntEsperaTul`,
      `cntRechazoTul`.
- [ ] **Ningún Rectángulo y ninguna Etiqueta tienen `Radius`** (`01` §0.3): las cinco
      superficies redondeadas de la pantalla son Botones clásicos (receta 7.1) y las tres
      cards de diálogo son Contenedores.
- [ ] 🧨 **Cero `Navigate(` en `scrTulas.OnVisible`** (`L-65`): la rama sin sesión dice
      `Select(btnIrSesionTul)` y nada más. Prueba: App Checker **sin** el aviso *"would
      automatically always navigate away"*, y entrar a `03` sin sesión ⇒ aviso ámbar
      *"No hay una sesión activa."* y vuelta a `scrSesion`. Si se queda en el hub vacío,
      el motor no disparó.
- [ ] Los **seis** motores (`btnCargarSesionTul`, `btnResolverDeclaracionTul`,
      `btnAbrirNuevaTul`, `btnReabrirTul`, `btnEntrarConteoTul`, `btnIrSesionTul`) son
      botones **1 × 1 transparentes** con `Visible true`, `Y 767`, `TabIndex -1` y
      `DisplayMode.Edit` sin condición (`D03-9`, `L-53`). **Ninguno** con `Visible false`.
- [ ] **App Checker en cero errores** y los **seis** Iconos clásicos interactivos con su
      `AccessibleLabel` cargado: `icoScanTul` (*"Buscar tula"*), `icoActualizarTul`
      (*"Actualizar la lista de tulas"*) y los **cuatro** `icoDlgX…Tul` (*"Cerrar el
      diálogo"*: `icoDlgXTulaTul`, `icoDlgXDeclTul`, `icoDlgXSinDecTul`,
      `icoDlgXRechazoTul`). Ninguno en blanco.

**Rail y KPIs**

- [ ] Con la sesión abierta, el chip de usuario muestra tu nombre e iniciales y el de mesa
      el `Name` de `gvarMesa`.
- [ ] Los tres KPI muestran **solo** lo de las tulas `Recontada`: abrí una tula, cargale
      un depósito, volvé a `scrTulas` **sin terminarla** ⇒ los KPI **no** se mueven.
      Terminala ⇒ suben.
- [ ] El pie del rail dice `N tulas aperturadas · M recontadas` y los dos números coinciden
      con la lista.
- [ ] `fxMonto(0, "C$ ")` en un KPI vacío muestra **`C$ 0.00`**, no `C$ .00` (`L-2`).

**Barra scan-first**

- [ ] Al entrar a la pantalla el cursor **ya está** en `inpScanTul` (sin tocar el mouse).
- [ ] Tipear un número y presionar **Enter** dispara la apertura (el `OnChange` de la
      Entrada de texto clásica). Probalo también con el **lector de código de barras**.
- [ ] `btnAbrirTul` está **deshabilitado** con el campo vacío.
- [ ] Escanear `AAA` (inexistente) ⇒ `Notify` rojo, el campo se limpia y **recupera el
      foco**.

**Lista**

- [ ] Sin tulas: se ven las dos etiquetas de vacío, el **encabezado de columnas sigue
      visible** y el subtítulo dice `0 tulas`.
- [ ] Con tulas: la fila es clicable **en toda su superficie** — probá clic sobre el
      **texto del cliente** y sobre la **píldora de estado**, no solo en el hueco vacío.
- [ ] La píldora traduce los seis `BagStatus` con **tilde** en pantalla y literal **sin
      tilde** en la fórmula (receta 7.4).
- [ ] El separador de fila está en `Y = 41` (= `TemplateSize − 1`) y la barra de acento
      solo se ve en la fila seleccionada.
- [ ] ⟳ trae lo que cambió **otro** usuario (pedile al supervisor que resuelva algo
      mientras estás parado en la pantalla).

**Apertura — las cuatro rutas + el retome**

- [ ] **Ruta 1 (recibida + 1 declaración)**: escanear ⇒ va **directo** a `scrConteo`, sin
      diálogos. En Dataverse: `BagStatus = Asignada a cajero`, `RecountingSession` seteada,
      `BagDeclarationID` apuntando a la declaración **y** `DeclaredBags.BagID` apuntando a
      la tula (las **dos** puntas).
- [ ] **Ruta 2 (homónimas)**: `B1` se abre, el botón **Abrir tula** nace **deshabilitado**,
      las filas que no están en bóveda **no se pueden seleccionar** y muestran su motivo.
- [ ] **Ruta 3 (varias declaraciones)**: `B2` se abre y el botón nace deshabilitado.
- [ ] **Ruta 4 (sin declaración)**: `B3` muestra los cuatro `kv` y el banner ámbar;
      *Enviar solicitud* crea el `ApprovalRequest` con `RequestType` **exactamente**
      `"Tula sin declaracion"`, `BagNumber` cargado y `RequestResources` con las claves
      `BagGUID` y `RecountingSessionGUID`. **`ApprovalStatus` queda en blanco.**
- [ ] **Retome**: clic en una fila de la lista ⇒ entra a `scrConteo` **sin** volver a
      patchear la bolsa (mirá `Modified On` antes y después: no cambia).
- [ ] 🧨 **`CH-Tbl-RecountingSessionDetails` existe DESPUÉS DE LAS CINCO** — no de una.
      Verificalo en la tabla: un registro por bolsa + sesión, con
      `RecountingSessionDetailStatus = En progreso`.
- [ ] Abrir **dos veces** la misma tula **no** crea un segundo detalle.

**Aprobación**

- [ ] Con la solicitud pendiente, el overlay de espera muestra el **Spinner moderno**
      girando, la hora de envío y los tres `kv`. El clic **fuera** de la card **no cierra**.
- [ ] El supervisor **aprueba** ⇒ en ≤ 15 s la tula se abre sola y entra a `scrConteo`.
- [ ] El supervisor **rechaza** ⇒ aparece el overlay de rechazo con **`StatusReason`**,
      quién resolvió y cuándo. La tula sigue **`Almacenada en bóveda`** y **no** se creó
      ningún detalle.
- [ ] *Cancelar la espera* ⇒ el timer se apaga (`gvarSolicitud` en blanco). Volver a
      escanear la misma tula **NO** crea una segunda solicitud: re-adopta la pendiente.
- [ ] Solo hay **UN** temporizador en la pantalla (`tmrPollTul`), con `Duration 15000`,
      `Repeat true`, `AutoStart false` y `OnTimerStart` **vacío**.

**Delegación (con el `DataRowLimit` en 500, no en 2000)**

- [ ] Ninguna de las nueve consultas de §11.1 muestra el triángulo azul de delegación.
- [ ] Buscá en el `Src/` desempaquetado: **cero** `Upper(`, **cero** `RemoveIf(`, **cero**
      `UpdateIf(`, **cero** `.AllItems`, **cero** `Sum(Filter('CH-Tbl-` (todos los `Sum`
      son sobre colecciones).
- [ ] `Set(` no aparece dentro de ningún `ForAll(` (regla **D-12**).
- [ ] Con **más de 500 tulas históricas con el mismo número**, `B1` sigue mostrando las
      candidatas correctas (el filtro es delegable, no un truncamiento).

**Contrato de salida**

- [ ] Al entrar a `scrConteo`, en *Ver → Variables* están seteadas **las ocho** de §8, con
      `gvarSesionDetalle` **NUNCA** en blanco y `gvarSolicitud` en `Blank()`.
- [ ] Las **diez** colecciones de `04` están **vacías** al llegar (*Ver → Colecciones*):
      `colDetalleConteoCon`, `colChecksCon`, `colAutoContadoraCon`, `colDepDeclaradosCon`,
      `colDepRecontadosCon`, `colChequesDeclaradosCon`, `colChecksTulaCon`,
      `colDetalleDVCon`, `colChecksDVCon` y `colHallazgosCon`.
- [ ] `gvarMesa.MachineSerialNumber` tiene valor: sin él, *Leer contadora* de `04` no
      funciona.






