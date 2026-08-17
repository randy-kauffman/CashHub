# Guía 02 — scrSesion (Apertura de sesión de conteo)

La **puerta de entrada** de CH-App-Recounting v2 y la `StartScreen` de la app (`01` §3.4).
Acá el cajero elige su **mesa de conteo**, la solicita, espera la resolución del supervisor
y —si lo aprueban— entra a `scrTulas`. Si ya tiene una sesión de hoy avalada por el
servidor, la pantalla **no se ve**: navega sola.

Reemplaza al `container CountingDeskSeleccion` de la v1 (el del `Form1` invisible con dos
`comboBoxEditCard`, el `NewForm` + `SubmitForm` en el mismo tick, el
`LookUp('CH-Tbl-CountingDesks', Cajero.User = gvarUser.User)` que con `gvarUser` en blanco
devolvía **la primera mesa sin cajero**, la escritura client-side de `Confirmed: true` y
`ApprovalStatus: Aprobado`, el timer de 5 s con `Refresh` en `OnTimerStart` y el
`spinner WaitingForDeskApproval` con dos condiciones de `Visible` contradictorias
comentadas una encima de la otra).

Acá: **cero forms**, `Patch` explícito, **la app no escribe `Confirmed` ni
`ApprovalStatus`** (los decide el plugin — `00` §2.2 punto 1, mata **S-1**), **un** timer de
15 s, y el **rechazo es un estado de primera clase** que BLOQUEA en vez de liberar (mata
**S-4**).

> **Todo lo que esta guía no define, lo define `01`.** Paleta, formatos, defaults
> tipográficos, alias `ESTILO-*`, catálogo cerrado de tipos, recetas `7.1..7.11`,
> componentes `§8` y reglas de delegación `D-1..D-12` viven allá y **mandan**. Acá solo
> hay: nombres finales, coordenadas, y las fórmulas propias de esta pantalla.

---

## CONTRATO DE ENTRADA

| Qué | Quién lo setea | Obligatorio |
|---|---|---|
| App creada con los **15 datasources + el flujo** de `01` §2.1, `App.Formulas`, `App.OnStart`, `App.OnError`, `BackEnabled = false`, **`StartScreen = scrSesion`**, `ConfirmExit` | Guía `01` §1–§3 | **Sí** — sin `App.OnStart` corrido, `colMesasSes` no existe y el `Items` del desplegable queda en rojo en tiempo de diseño |
| **`gvarUser`** | `App.OnStart`, y **nadie más** (`01` §4.1) | **Sí** — es el `Cashier` de la sesión y el `'Created By'` contra el que se recupera la solicitud |
| **`gvarIdentidadOk`** | `App.OnStart` (`01` §3.2) | **Sí** — con `false` esta pantalla **se bloquea** con `cntBloqueoSes` (§11) y **no dispara ni una consulta**. Es el fix estructural de **S-1** |
| `gvarSesion`, `gvarMesa`, `gvarSolicitud` | `App.OnStart` los deja en `Blank()`. **Los setea esta pantalla** | No — `Blank()` es el estado válido de arranque |
| `gvarTula`, `gvarSesionDetalle`, `gvarDeclaredBag` | `App.OnStart` (`Blank()`) | No — esta pantalla los **blanquea** al navegar (contrato `01` §4.5) |
| **`cmpDialogo`** construido (`01` §8.3) | Guía `01` §8, **antes** que las pantallas (regla 0.12) | **Sí** — `cmpDlgCancelarSes` (§9) es una instancia. Si tu Studio no tiene *Propiedades de componente mejoradas*, aplicá el fallback de `01` §8.3 nota **B**: el diálogo se arma con la **receta 7.2** y el resto de la guía no cambia |

`cmpMatrizMoneda` y `cmpKpiSesion` **no se usan** en esta pantalla.

### Qué deja seteado esta pantalla al navegar a `scrTulas`

Copia literal del contrato canónico de `01` §4.5 — **esa es la única definición**. Si
alguna vez difieren, manda `01`.

```powerfx
// Se ejecuta cuando la sesión de hoy YA existe y está avalada por el servidor.
// Set(gvarSesion, <registro de CH-Tbl-RecountingSessions de hoy>);
//   ↑ 🧨 PRIMERA LÍNEA DEL CONTRATO CANÓNICO DE 01 §4.5, copiada acá COMENTADA a
//     propósito: en 02 `gvarSesion` YA está seteada cuando se llega a este punto —por el
//     bloque 2 del OnVisible (§1) en la rama APROBADA, o por la re-lectura por PK del
//     bloque 3 de la rama *Aprobado* del polling (§10)—. Escribirla de nuevo acá sería
//     una segunda definición del mismo valor. Se deja visible para que el contrato se
//     lea COMPLETO y nadie crea que 02 se saltea una línea.
Set(gvarMesa,   gvarSesion.CountingDesk);
Set(gvarTula, Blank());
Set(gvarDeclaredBag, Blank());
Set(gvarSesionDetalle, Blank());
Set(gvarSolicitud, Blank());
Set(gvarDepositoSinGuardar, false);
Navigate(scrTulas)
```

Sale por **dos** caminos y por ninguno más:

1. `scrSesion.OnVisible`, bloque 4, rama **APROBADA** — la sesión ya existía al entrar.
   🧨 **Vía `Select(btnEntrarTulasSes)`** (§1.1): un `OnVisible` **no puede** contener
   `Navigate()` (`L-65`). El contrato de arriba **es** el `OnSelect` de ese motor.
2. `tmrPollSes.OnTimerEnd`, rama **APROBADO** (§10) — la aprobación llegó mientras esperaba.
   Ahí el `Navigate` va **inline**: un `OnTimerEnd` es un manejador de comportamiento y es
   donde la receta `01` §7.7 manda poner la navegación diferida.

> 🧨 **`gvarMesa` sale SIEMPRE de `gvarSesion.CountingDesk`**, que es la anotación de un
> lookup sobre un registro **ya cargado** (no cuesta un fetch). **Jamás** de
> `LookUp('CH-Tbl-CountingDesks', Cajero.User = gvarUser.User)`: ese `LookUp` **es** el
> bypass **S-1** de la v1 — con `gvarUser` en blanco devuelve la primera mesa sin cajero.

### Variables y colecciones **propias** de esta pantalla

Las globales y `colMesasSes` están declaradas en `01` §4. Lo que **nace acá**:

| Nombre | Tipo | Para qué |
|---|---|---|
| `colMesasDVSes` | Colección | Snapshot **crudo** de las mesas habilitadas (`Enable = true`), ya ordenado. Es el origen del que se deriva `colMesasSes` sin volver a la red |
| `colSesionesHoySes` | Colección | Sesiones **de todas las mesas** abiertas hoy (no solo las del cajero). Es lo que permite restar las mesas ocupadas **en memoria**, sin un `in` con subconsulta (regla **D-6**) |
| `ctxEsperandoSes` | Booleano | `true` = tarjeta en modo **A2** (esperando aprobación). Nace del contrato `01` §5.8 (`ctxEsperando<Scr>`) |
| `ctxRechazadaSes` | Booleano | `true` = tarjeta en modo **A3** (solicitud rechazada). `ctxEsperandoSes` y `ctxRechazadaSes` **nunca** son `true` a la vez |
| `ctxDlgCancelarSes` | Booleano | `Visible` de `cmpDlgCancelarSes` (§9) |
| `ctxAbandonoOkSes` | Booleano | Bandera de resultado del abandono (§9). **Solo si las dos bajas pasaron** se vuelve al estado inicial: la UI **nunca** marca como liberado lo que Dataverse rechazó (receta `01` §7.5, regla 3) |

> **Los tres estados son EXCLUYENTES y salen de dos booleanos**, no de tres:
> `ctxRechazadaSes` ⇒ A3 · `ctxEsperandoSes` ⇒ A2 · ninguno ⇒ A1. El estado inicial es la
> ausencia de los otros dos: así no hay un tercer booleano que se pueda desincronizar.

---

## Árbol de controles

Orden del árbol = **orden de inserción = z-order** (`01` §0.6): lo insertado después queda
encima. Construcción en **DOS PASES** (`01` §0.11): primero insertar y renombrar todo en
este orden exacto, después llenar propiedades.

```
scrSesion
├─ shpHeaderSes                  Rectángulo clásico
├─ shpHeaderLineaSes             Rectángulo clásico
├─ btnTileHdrSes                 Botón clásico             (receta 7.1 + texto)
├─ lblTituloSes                  Etiqueta clásica
├─ lblSubtituloSes               Etiqueta clásica
├─ btnChipUserSes                Botón clásico             (receta 7.1)
├─ btnAvatarSes                  Botón clásico             (receta 7.1)
├─ lblUsuarioSes                 Etiqueta clásica
│  ── LA TARJETA: superficie al FONDO, contenido encima (receta 7.1) ──
├─ btnCardSes                    Botón clásico             (receta 7.1 — card)
├─ btnTileCardSes                Botón clásico             (receta 7.1 + texto)
├─ lblMarcaSes                   Etiqueta clásica
├─ lblMarcaSubSes                Etiqueta clásica
│  ── BLOQUE A1 · estado inicial ──
├─ lblLabMesaSes                 Etiqueta clásica
├─ ddMesaSes                     Menú desplegable clásico
├─ lblNotaMesaSes                Etiqueta clásica
│  ── BLOQUE A2 · esperando aprobación ──
├─ btnEsperaSes                  Botón clásico             (receta 7.1)
├─ spnEsperaSes                  Spinner moderno           (receta 7.7)
├─ lblEsperaTitSes               Etiqueta clásica
├─ lblEsperaSubSes               Etiqueta clásica
├─ lblKvMesaEtqSes               Etiqueta clásica
├─ lblKvMesaSes                  Etiqueta clásica
├─ lblKvFechaEtqSes              Etiqueta clásica
├─ lblKvFechaSes                 Etiqueta clásica
├─ lblKvCajeroEtqSes             Etiqueta clásica
├─ lblKvCajeroSes                Etiqueta clásica
│  ── BLOQUE A3 · solicitud rechazada ──
├─ btnAvisoRechazoSes            Botón clásico             (receta 7.2.a — superficie)
├─ icoAvisoRechazoSes            Icono clásico  Icon.Blocked
├─ lblAvisoRechazoTitSes         Etiqueta clásica
├─ lblAvisoRechazoSubSes         Etiqueta clásica
├─ lblLabMotivoSes               Etiqueta clásica
├─ btnCajaMotivoSes              Botón clásico             (receta 7.1)
├─ lblMotivoSes                  Etiqueta clásica
│  ── ACCIONES + PIE · anclados al FONDO de la tarjeta ──
├─ btnSolicitarSes               Botón clásico
├─ btnCancelarSolicitudSes       Botón clásico
├─ btnElegirOtraSes              Botón clásico
├─ lblPieSes                     Etiqueta clásica
│  ── MOTORES 1×1 y TEMPORIZADOR · cuelgan de la PANTALLA, nunca de un contenedor ──
├─ btnAbandonarSes               Botón clásico             (motor 1×1 transparente — §9)
├─ btnEntrarTulasSes             Botón clásico             (motor 1×1 transparente — §1.1)
├─ tmrPollSes                    Temporizador clásico      (receta 7.7 — §10)
│  ── MODAL · anteúltimo ──
├─ cmpDlgCancelarSes             Componente canvas         (cmpDialogo, 01 §8.3)
│  ── BLOQUEO DE IDENTIDAD · ÚLTIMO, tope absoluto del z-order ──
└─ cntBloqueoSes                 Contenedor de diseño manual
   ├─ btnBloqTragaSes            Botón clásico
   ├─ icoBloqueoSes              Icono clásico  Icon.Blocked
   └─ lblBloqueoSes              Etiqueta clásica
```

**44 controles.** El panel de árbol de Studio los lista **al revés** (el frontal arriba):
de eso dependen las anotaciones de z-order de esta guía — `btnCardSes` al FONDO de su
bloque, `cmpDlgCancelarSes` sobre todo el contenido, y `cntBloqueoSes` sobre **todo**,
incluido el modal.

> 🧨 **`cntBloqueoSes` va DESPUÉS de `cmpDlgCancelarSes`.** `01` §8.3 dice que la instancia
> de `cmpDialogo` se inserta "última en la pantalla"; acá la última es la guarda de
> identidad, porque un diálogo pintado **encima** del bloqueo sería una superficie
> interactiva en una app que no sabe quién la está usando. Divergencia **D02-16**.

> 🧨 **`tmrPollSes` y los DOS motores cuelgan de la PANTALLA, no de un contenedor.** Un
> control dentro de un contenedor con `Visible false` **no corre y no se puede
> `Select()`**, y `cntBloqueoSes` está oculto justo cuando los tres tienen que funcionar.
> Por el mismo motivo `btnAbandonarSes` y `btnEntrarTulasSes` **no** son `Visible: false`:
> son **botones transparentes de 1 × 1** apoyados en la última fila de píxeles
> (`L-53` / `03` `D03-9`).

### Reglas de coordenadas de esta pantalla

- **No hay galerías.** La regla 0.7 de `01` (X del template = X del encabezado − X de la
  galería) y la 0.8 (separador en `TemplateSize − 1`) **no aplican acá**. Toda coordenada
  es de pantalla, salvo las de los hijos de `cntBloqueoSes`, que son **relativas al
  contenedor** (`Parent.Width` / `Parent.Height` = 1366 × 768).
- **La tarjeta NO es un contenedor**: es un Botón clásico (receta 7.1). Sus "hijos" son
  hermanos suyos en el árbol y se posicionan **contra ella**: todo X arranca en
  `btnCardSes.X + 30` y todo Y encadena contra el control de arriba (regla 0.9).

### Mapa de la pantalla (1366 × 768, medido contra el CSS del mockup)

| Banda | X · Y | Ancho · Alto | De dónde sale |
|---|---|---|---|
| Header | `0` · `0` | `1366` · `60` | `.hdr{height:60px}` |
| Línea del header | `0` · `60` | `1366` · `1` | `.hdr{border-bottom:1px solid var(--line)}` |
| Zona de la tarjeta | `0` · `61` | `1366` · `707` | `.sesWrap{flex:1}` ⇒ `768 − 61` |
| Tarjeta | `468` · centrada | `430` · **`379` \| `413` \| `444`** | `.sesCard{width:430px;padding:28px 30px 26px;gap:15px}`. `468 = (1366 − 430) / 2` |
| Contenido de la tarjeta | `btnCardSes.X + 30` | `370` | `430 − 30 − 30` |

**Los tres altos de la tarjeta**, con su aritmética (el mockup dibuja tres tarjetas que
ajustan a su contenido; canvas no tiene `AutoHeight` de card, así que el alto es un `If`
de tres valores y **cada uno se puede verificar sumando**):

| Estado | Aritmética | Alto |
|---|---|---|
| **A1** inicial | `28` pad + `52` tile + `9` + `23` h2 + **`9`** + `17` small + `17` gap + `15` label + `5` + `38` desplegable + `6` + `30` nota + `15` gap + `40` botón + `15` gap + `34` pie + `26` pad | **`379`** |
| **A2** esperando | `28 + 52 + 9 + 23` + **`9`** + `17 + 17` + `62` waitRow + `15` + **`17+21+21`** kv + `15` + `32` botón + `15` + `34` pie + `26` | **`413`** |
| **A3** rechazada | `28 + 52 + 9 + 23` + **`9`** + `17 + 17` + `58` banner + `15` + `15` label + `5` + `66` caja + `15` + `40` botón + `15` + `34` pie + `26` | **`444`** |

> El pie y los tres botones de acción se anclan **desde abajo**
> (`lblPieSes.Y = btnCardSes.Y + btnCardSes.Height − 26 − Self.Height`). Los tres altos de
> arriba son exactamente los que hacen que ese anclaje caiga sobre el final de cada bloque:
> es la prueba de que los números no están inventados. **Si cambiás el contenido de un
> bloque, recalculá su alto con la misma suma.**

> 🧨 **Los dos términos que cambiaron, y por qué.**
>
> 1. **El `9` nuevo entre el `<h2>` y el `<small>`.** El mockup separa `CashHub · Reconteo`
>    de su subtítulo (`.sesBrand{gap:9px}`); la guía tenía `lblMarcaSubSes.Y =
>    lblMarcaSes.Y + lblMarcaSes.Height`, es decir **pegados**. Con
>    `+ lblMarcaSes.Height + 9` el aire vuelve, y **los tres altos suben 9**
>    (`370→379`, `404→413`, `435→444`).
> 2. **El `kv` de `A2` son TRES pares, no cuatro.** La suma decía `17+21+21+17` = `76`, que
>    da `421` — y el alto publicado siempre fue `404`. El `kv` de §6.2 son **3 pares con paso
>    `21`**: dos pasos de `21` más los `17` de alto del último renglón ⇒ `17+21+21` = **`59`**.
>    El `+17` de más contaba un cuarto renglón que no existe: la aritmética **cerraba mal
>    contra su propio resultado**. Corregida, `404` se verifica; con el `9` de arriba, `413`.

---

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | ver abajo — **COMPLETO, listo para pegar** |

```powerfx
// ============================================================================
// BLOQUE 0 — ESTADO DE UI. Corre SIEMPRE, también con la identidad rota.
//   El estado de los diálogos PERSISTE entre visitas: sin este reset, volver
//   acá desde scrTulas te encuentra el modal de cancelación abierto.
// ============================================================================
UpdateContext({
    ctxEsperandoSes:   false,
    ctxRechazadaSes:   false,
    ctxDlgCancelarSes: false,
    ctxAbandonoOkSes:  true
});

// ============================================================================
// GUARDA DURA DE IDENTIDAD (01 §3.2, ledger L-16).
//   Con gvarIdentidadOk = false, cntBloqueoSes (§11) tapa la pantalla y NINGUNA
//   consulta de abajo tiene sentido: gvarUser está en blanco y
//   Cashier.User = Blank() devolvería basura. Ese era el bypass S-1 de la v1.
//   No hay Navigate ni salida: la app se BLOQUEA, no degrada.
// ============================================================================
If(
    gvarIdentidadOk,

    // ========================================================================
    // BLOQUE 1 — CATÁLOGO DE MESAS DISPONIBLES.  Q3 + Q4 (§2), delegables.
    //   Se carga SIEMPRE, en los tres estados. Motivo: "Cancelar solicitud" y
    //   "Elegir otra mesa" (§9) devuelven la tarjeta al estado inicial SIN
    //   volver a la red, y necesitan el catálogo ya en memoria. Son dos
    //   consultas sobre tablas estructuralmente acotadas (regla D-10).
    //
    //   🧨 La resta "mesas habilitadas MENOS mesas con sesión de hoy" es LOCAL.
    //   Hacerla en el servidor exigiría un `in` con subconsulta, que JAMÁS
    //   delega (regla D-6, bug D-1 de `00` §1.1). La v1 la hacía con un With()
    //   anidado que traía las dos tablas igual: mismo costo, sintaxis peor.
    // ========================================================================
    ClearCollect(
        colSesionesHoySes,
        Filter(
            'CH-Tbl-RecountingSessions',
            'Created On' >= Today() &&
            'Created On' <  DateAdd(Today(), 1, TimeUnit.Days) &&
            SessionForciblyClosed = false
        )
    );
    ClearCollect(
        colMesasDVSes,
        Sort(
            Filter('CH-Tbl-CountingDesks', Enable = true),
            Name
        )
    );
    ClearCollect(
        colMesasSes,
        Filter(
            colMesasDVSes As M,
            IsBlank(
                LookUp(
                    colSesionesHoySes,
                    CountingDesk.'CH-Tbl-CountingDesk' = M.'CH-Tbl-CountingDesk'
                )
            )
        )
    );

    // ========================================================================
    // BLOQUE 2 — LA SESIÓN DE HOY DEL CAJERO.  Q1 (§2), delegable.
    //   Sort + First, nunca LookUp a secas (regla D-9): si por un fallo previo
    //   hay más de una sesión del día, la que manda es la ÚLTIMA.
    // ========================================================================
    Set(
        gvarSesion,
        First(
            Sort(
                Filter(
                    'CH-Tbl-RecountingSessions',
                    Cashier.User = gvarUser.User &&
                    'Created On' >= Today() &&
                    'Created On' <  DateAdd(Today(), 1, TimeUnit.Days) &&
                    SessionForciblyClosed = false
                ),
                'Created On',
                SortOrder.Descending
            )
        )
    );

    // ========================================================================
    // BLOQUE 3 — LA SOLICITUD DE ESA SESIÓN.  Q2 (§2), delegable.
    //   El If va POR FUERA del Set: sin sesión no hay nada que recuperar y la
    //   consulta ni se dispara.
    //   🧨 La existencia se pregunta SIEMPRE por la PK: sobre un registro tipado
    //      vacío, IsBlank(<registro>) puede devolver false (01 §5.8).
    // ========================================================================
    If(
        IsBlank(gvarSesion.'CH-Tbl-RecountingSession'),
        Set(gvarSolicitud, Blank()),
        Set(
            gvarSolicitud,
            First(
                Sort(
                    Filter(
                        'CH-Tbl-ApprovalRequests',
                        'Created By'.User = gvarUser.User &&
                        RequestType = "Crear sesion de recuento" &&
                        'Created On' >= Today() &&
                        'Created On' <  DateAdd(Today(), 1, TimeUnit.Days)
                    ),
                    'Created On',
                    SortOrder.Descending
                )
            )
        )
    );

    // ========================================================================
    // BLOQUE 4 — MÁQUINA DE ESTADOS. Cuatro ramas, y ninguna más.
    //   🧨 El RECHAZO es un estado de PRIMERA CLASE (mata S-4). La v1 preguntaba
    //   `ApprovalStatus = Blank()` y fallaba ABIERTO: un rechazo liberaba al
    //   cajero exactamente igual que una aprobación.
    // ========================================================================
    If(
        // ---- 1. AVALADA POR EL SERVIDOR: se entra sin ver esta pantalla ------
        //    `Confirmed` lo escribe el PLUGIN (01 §2.4). La app nunca lo toca.
        //    Se acepta también el Aprobado de la solicitud por si el espejo
        //    `Confirmed` todavía no propagó: la autoridad es el ApprovalStatus.
        !IsBlank(gvarSesion.'CH-Tbl-RecountingSession') &&
        (
            gvarSesion.Confirmed = true ||
            gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado
        ),
            // ---- contrato de salida + Navigate: TODO en el motor btnEntrarTulasSes ----
            //    🧨 NUNCA Navigate() en un OnVisible (§1.1): el App Checker lo marca
            //       ("would automatically always navigate away") y el contrato de salida
            //       tiene que existir en UN solo lugar. Acá SOLO se lo invoca.
            Select(btnEntrarTulasSes),

        // ---- 2. RECHAZADA: BLOQUEA, no libera -------------------------------
        !IsBlank(gvarSesion.'CH-Tbl-RecountingSession') &&
        gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
            Set(gvarMesa, gvarSesion.CountingDesk);
            UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: true }),

        // ---- 3. PENDIENTE: el cajero cerró la app mientras esperaba ----------
        //    El timer arranca solo: su Start es ctxEsperandoSes (§10).
        !IsBlank(gvarSesion.'CH-Tbl-RecountingSession') &&
        !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest'),
            Set(gvarMesa, gvarSesion.CountingDesk);
            UpdateContext({ ctxEsperandoSes: true, ctxRechazadaSes: false }),

        // ---- 4. INICIAL -----------------------------------------------------
        //    Incluye el caso raro "sesión de hoy SIN solicitud" (el Patch de la
        //    solicitud falló Y la compensación de §8 también). Se degrada al
        //    estado inicial CON AVISO: la app no borra sola una sesión que no
        //    sabe quién creó. La mesa de esa sesión queda ocupada hasta que el
        //    supervisor la cierre. Divergencia D02-18.
        If(
            !IsBlank(gvarSesion.'CH-Tbl-RecountingSession'),
            Notify(
                "Tiene una sesión de hoy sin solicitud de aprobación: esa mesa puede aparecer ocupada. Elija otra mesa y avise al supervisor.",
                NotificationType.Warning
            )
        );
        Set(gvarSesion,    Blank());
        Set(gvarMesa,      Blank());
        Set(gvarSolicitud, Blank());
        UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: false })
    )
);

// ============================================================================
// BLOQUE 5 — RESET DE CONTROLES. SIEMPRE, y al final.
//   El desplegable clásico recuerda su selección entre visitas: sin el Reset,
//   volver acá tras cerrar una sesión deja marcada una mesa que ya no está en
//   colMesasSes.
// ============================================================================
Reset(ddMesaSes)
```

> 🧨 **Ni `Navigate()` ni `Select(<motor que navega>)` cortan la fórmula.** Los `Set` del
> contrato viven **dentro** del motor (§1.1); todo lo que venga **después** del `If` (el
> `Reset(ddMesaSes)` del bloque 5) igual se ejecuta. Por eso el bloque 5 es inofensivo y
> por eso el catálogo de mesas se carga **antes** de la máquina de estados: si dependiera
> de una rama, habría que repetirlo en tres.

> 🧨 **Todos los `UpdateContext` de esta fórmula se leen dentro de la MISMA fórmula.**
> `UpdateContext` y `Set` tienen efecto **inmediato** para las expresiones que siguen en la
> cadena de `;`. Si tu Studio se comportara distinto, es un bug del entorno: **avisá antes
> de reescribir la pantalla**.

### 1.1 `btnEntrarTulasSes` — el motor de salida a `scrTulas`

🧨 **`Navigate()` está PROHIBIDO en un `OnVisible`.** El App Checker lo marca como
*"would automatically always navigate away"* y tiene razón: una pantalla que navega sola
desde su propio `OnVisible` no se puede abrir en Studio para revisarla, y el `Navigate`
queda enterrado en una rama de una máquina de estados. La solución es el **patrón motor**
de la casa (`L-53`): el `OnVisible` **invoca**, el motor **hace**. Ledger **`L-65`**.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnEntrarTulasSes` | Botón clásico — **motor 1 × 1** (`L-53`) | X `2` · **Y `767`** · Width `1` · Height `1` · **`Visible` `true`** · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · `Text` `""` · `TabIndex` `-1` · **`DisplayMode` `DisplayMode.Edit`** (sin condición) · `AccessibleLabel` `""` · `OnSelect` ver abajo |

```powerfx
// ====================== btnEntrarTulasSes.OnSelect ======================
// CONTRATO DE SALIDA hacia scrTulas — copia literal de 01 §4.5.
// El OnVisible (bloque 4, rama 1) solo hace Select(): ahí NO puede haber Navigate.
Set(gvarMesa, gvarSesion.CountingDesk);
Set(gvarTula, Blank());
Set(gvarDeclaredBag, Blank());
Set(gvarSesionDetalle, Blank());
Set(gvarSolicitud, Blank());
Set(gvarDepositoSinGuardar, false);
Navigate(scrTulas)
```

> 🧨 **`Set(gvarDepositoSinGuardar, false)` va acá.** Es la bandera que gobierna
> `App.ConfirmExit` (`01` §3.4): entrar a una sesión nueva arrastrando el `true` de una tula
> abandonada le pone al cajero un "¿seguro que querés salir?" del navegador sin ningún
> depósito en curso.

> 🧨 **Es un motor, no un botón**: `Visible: true` y transparente, **nunca `Visible: false`**
> (`L-53`, `03` `D03-9`) — `Select()` sobre un control oculto no está garantizado por la
> plataforma, y un `Select()` que no dispara deja al cajero mirando la tarjeta de solicitud
> con la sesión **ya aprobada**. Mismo patrón, mismas propiedades y mismo `Y 767` que
> `btnAbandonarSes` (§9); solo cambia el `X` para que no se pisen.

> **Llamador (lista CERRADA, 1)**: `scrSesion.OnVisible`, bloque 4, rama 1 (*avalada por el
> servidor*).

> 🧨 **El OTRO camino de salida —`tmrPollSes.OnTimerEnd`, rama *Aprobado* (§10)— conserva su
> `Navigate(scrTulas)` INLINE, y está bien.** La prohibición es del `OnVisible`, no del
> `Navigate`: un `OnTimerEnd` **es** un manejador de comportamiento y es exactamente donde
> la receta `01` §7.7 manda poner la navegación diferida (regla 3 de §10: *"los timers no
> corren en una pantalla que no está visible"*). Son **dos** apariciones del contrato en
> esta pantalla —el motor y el timer—, y la definición **canónica** de las dos sigue siendo
> `01` §4.5. Estado real desplegado; ledger **`L-65`**.

---

## 2. Consultas a Dataverse de esta pantalla — inventario CERRADO

Todo lo demás corre sobre colecciones locales: **no hay delegación que romper** (`01` §2.3).
Estas son **las únicas** llamadas a la red, evaluadas contra un `DataRowLimit` de **500**
(`01` §1.2, ledger `L-15`).

| # | Fórmula | Dónde | Veredicto |
|---|---|---|---|
| **Q1** | `Filter('CH-Tbl-RecountingSessions', Cashier.User = gvarUser.User && 'Created On' >= Today() && 'Created On' < DateAdd(Today(),1,TimeUnit.Days) && SessionForciblyClosed = false)` | §1 bloque 2 | **Delegable** — `Cashier.User` es **PK contra PK** (regla **D-1**), el rango de `'Created On'` pliega a dos `ge`/`lt`, y el booleano a un `eq`. Devuelve **0 o 1** filas |
| **Q2** | `Filter('CH-Tbl-ApprovalRequests', 'Created By'.User = gvarUser.User && RequestType = "Crear sesion de recuento" && 'Created On' >= Today() && 'Created On' < DateAdd(Today(),1,TimeUnit.Days))` | §1 bloque 3 | **Delegable** — `'Created By'` es lookup y `.User` su PK (`01` §5.8, regla 1). `RequestType` es `nvarchar` ⇒ `eq` de string. **Nunca `'Created By'.'Full Name'`** |
| **Q3** | `Filter('CH-Tbl-RecountingSessions', 'Created On' >= Today() && 'Created On' < DateAdd(Today(),1,TimeUnit.Days) && SessionForciblyClosed = false)` | §1 bloque 1 | **Delegable**. Acotada por **el edificio**: como máximo una sesión viva por mesa y por día (regla **D-10**) |
| **Q4** | `Sort(Filter('CH-Tbl-CountingDesks', Enable = true), Name)` | §1 bloque 1 | **Delegable** — `eq` de booleano + `orderby` sobre `Name`. Catálogo acotado por el edificio (**D-10**) |
| **Q5** | `Patch('CH-Tbl-RecountingSessions', Defaults('CH-Tbl-RecountingSessions'), { Cashier, CountingDesk })` | §8 `btnSolicitarSes` | Escritura de 1 registro. `Name` es **AUTONUMBER**: no se patchea. **`Confirmed` NO se escribe** (`01` §2.4 — mata **S-1**) |
| **Q6** | `Patch('CH-Tbl-ApprovalRequests', Defaults('CH-Tbl-ApprovalRequests'), { RequestType, RequestDetails, RequestResources })` | §8 `btnSolicitarSes` | Escritura de 1 registro. **`ApprovalStatus` NO se escribe**: en blanco = Pendiente (`01` §5.5) |
| **Q7** | `Remove('CH-Tbl-RecountingSessions', gvarSesion)` | §8 (compensación) · §9 (abandono) | Registro **explícito**, jamás `RemoveIf(ds, predicado)` (regla **D-3**) |
| **Q8** | `Remove('CH-Tbl-ApprovalRequests', gvarSolicitud)` | §9 (abandono) | Ídem |
| **Q9** | `LookUp('CH-Tbl-ApprovalRequests', 'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest')` | §10 tick del timer · §9 re-lectura previa al abandono | **Delegable** — `eq` por PK. **Una** consulta por tick (`01` §5.8) |
| **Q10** | `LookUp('CH-Tbl-RecountingSessions', 'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession')` | §10 rama *Aprobado* | **Delegable** — `eq` por PK. Re-lee `Confirmed`, que el plugin **acaba** de escribir |

> 🧨 **CERO `Refresh()` en toda la pantalla.** `scrSesion` **no consulta `CH-Tbl-Bags`** —ni
> tulas, ni depósitos, ni cheques—: invalidarle la caché desde acá no le sirve a nadie y le
> cuesta una cascada de consultas a `scrTulas`, que **ya** trae lo suyo con `ClearCollect`
> (que siempre va al servidor) y tiene su propio ⟳ (`03` §5.2, regla **D-5** forma 1). Si en
> esta pantalla ves un `Refresh`, es un bug: reportalo.

**Cero `Refresh` en `OnTimerStart`** (la v1 lo tenía, ≈10 consultas en cascada por ciclo) ·
**cero `Sum`/`CountRows` sobre datasource** (**D-2**) · **cero `in` con subconsulta**
(**D-6**) · **cero `Upper()`** (**D-7**) · **cero `Filter` dentro del template de una
galería** (**D-4**: no hay galerías) · **cero `Set()` dentro de un `ForAll`** (**D-12**: no
hay `ForAll`).

> **Costo de red total de la pantalla**: 4 consultas al entrar (Q1–Q4), 2 escrituras al
> solicitar (Q5 + Q6), **1 consulta cada 15 s** mientras espera (Q9), y **1 más** al
> aprobarse (Q10). La v1 hacía **2 timers de 5 s con `Refresh` en cascada**, uno de
> ellos con `Start: =true` **literal**, corriendo siempre.

---

## 3. Header (Y `0`–`61`)

`.hdr{height:60px; background:surface; border-bottom:1px solid line; padding:0 20px; gap:14}`.
Header **mínimo**: marca a la izquierda, chip de usuario a la derecha, y nada más — todavía
no hay sesión, así que no hay chip de mesa (ese aparece en `03`/`04`).

| Control | Tipo | Propiedades |
|---|---|---|
| `shpHeaderSes` | Rectángulo clásico | X `0` · Y `0` · Width `Parent.Width` · Height `60` · `Fill` `ThemeSurface` — banda de esquina viva: por eso es Rectángulo y no Botón (`01` §0.3) |
| `shpHeaderLineaSes` | Rectángulo clásico | X `0` · Y `shpHeaderSes.Height` · Width `Parent.Width` · Height `1` · `Fill` `ThemeLine` |
| `btnTileHdrSes` | Botón clásico | receta 7.1 **con texto** · X `20` · Y `13` · Width `34` · Height `34` · `Radius` (las 4) `9` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccent` · `Color` `ThemeSurface` · `Size` `10.5` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Text` `"C$"` |
| `lblTituloSes` | Etiqueta clásica | X `btnTileHdrSes.X + btnTileHdrSes.Width + 14` · Y `13` · Width `180` · Height `18` · **ESTILO-TITULO-APP** · `Text` `"Reconteo"` |
| `lblSubtituloSes` | Etiqueta clásica | X `lblTituloSes.X` · Y `lblTituloSes.Y + lblTituloSes.Height` · Width `180` · Height `15` · **ESTILO-SUBTITULO-APP** · `Text` `"Apertura de sesión"` |
| `btnChipUserSes` | Botón clásico | receta 7.1 · X `Parent.Width - 20 - Self.Width` · Y `13` · Width `5 + btnAvatarSes.Width + 8 + lblUsuarioSes.Width + 12` · Height `34` · `Radius` (las 4) `17` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `btnAvatarSes` | Botón clásico | receta 7.1 **con texto** · X `btnChipUserSes.X + 5` · Y `17` · Width `26` · Height `26` · `Radius` (las 4) `13` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccentSoft` · `Color` `ThemeAccent` · `Size` `7.88` · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Text` `UsuarioIniciales` |
| `lblUsuarioSes` | Etiqueta clásica | X `btnAvatarSes.X + btnAvatarSes.Width + 8` · Y `btnChipUserSes.Y` · Width `5.8 * Len(Self.Text) + 6` · Height `btnChipUserSes.Height` · `Size` `8.63` · `Color` `ThemeInk2` · `Text` `UsuarioNombre` |

> **El tile de marca es un Botón con `Text "C$"`, no una Imagen.** Lo fija `01` §1.2: el
> ícono de *Configuración → General* es metadata y **no se puede referenciar desde una
> fórmula**. `"C$"` son dos caracteres ASCII — la única excepción declarada a la regla de
> "todo glifo va como Icono clásico" (`01` §1.6).

> **Cadena de anclaje del header** (regla 0.9, cero números mágicos duplicados):
> `btnTileHdrSes` → `lblTituloSes` → `lblSubtituloSes`; y desde la derecha,
> `btnChipUserSes` anclado a `Parent.Width − 20` → `btnAvatarSes` → `lblUsuarioSes`.
> El `Width` del chip se **deriva** de sus hijos, así que crece con el nombre del usuario.

> **Divergencia `D02-12`**: el mockup escribe *"Luis Martínez · Cajero de conteo"*. El chip
> muestra **solo `UsuarioNombre`**: `User()` da nombre y correo, **no** el puesto, y los
> conectores de directorio están prohibidos (`01` §1.5, un solo conector). Misma decisión
> ya tomada en Reception v2 (`L-12` de su ledger).

---

## 4. La tarjeta y su bloque de marca

`.sesCard{width:430px; padding:28px 30px 26px; gap:15px; border:1px solid line; border-radius:14px}`
sobre `.sesWrap`, que la centra en el espacio que queda bajo el header.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnCardSes` | Botón clásico | receta 7.1 · X `(Parent.Width - Self.Width) / 2` · Y ver abajo · Width `430` · Height ver abajo · `Radius` (las 4) **`14`** · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` |
| `btnTileCardSes` | Botón clásico | receta 7.1 **con texto** · X `btnCardSes.X + (btnCardSes.Width - Self.Width) / 2` · Y `btnCardSes.Y + 28` · Width `52` · Height `52` · `Radius` (las 4) `14` · `Fill`/`HoverFill`/`PressedFill` `ThemeAccent` · `Color` `ThemeSurface` · `Size` **`16.5`** · `FontWeight` `FontWeight.Bold` · `Align` `Align.Center` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Text` `"C$"` |
| `lblMarcaSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `btnTileCardSes.Y + btnTileCardSes.Height + 9` · Width `370` · Height `23` · `Size` **`12.75`** · `FontWeight` `FontWeight.Bold` · `Color` `ThemeInk` · `Align` `Align.Center` · `Text` `"CashHub · Reconteo"` |
| `lblMarcaSubSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `lblMarcaSes.Y + lblMarcaSes.Height + 9` · Width `370` · Height `17` · `Size` `9` · `Color` `ThemeInk3` · `Align` `Align.Center` · `Text` ver abajo |

```powerfx
// btnCardSes.Height — los tres altos de la tabla del mapa. Cada uno es una SUMA
// verificable, no un número a ojo. Si tocás el contenido de un bloque, recalculalo.
If(
    ctxRechazadaSes, 444,
    ctxEsperandoSes, 413,
    379
)
```

```powerfx
// btnCardSes.Y — centrada en la banda que queda BAJO el header.
//   No hay circularidad: Height no depende de Y.
shpHeaderLineaSes.Y + shpHeaderLineaSes.Height +
(Parent.Height - shpHeaderLineaSes.Y - shpHeaderLineaSes.Height - Self.Height) / 2
```

```powerfx
// lblMarcaSubSes.Text — el subtítulo dice en qué estado está la tarjeta (mockup A1/A2/A3)
If(
    ctxRechazadaSes, "Solicitud resuelta",
    ctxEsperandoSes, "Solicitud enviada",
    "Abra una sesión de conteo para comenzar"
)
```

> **`btnCardSes` va al FONDO de su bloque** (receta 7.1): se inserta ANTES que todo lo que
> lleva encima. Si al terminar el pase 1 la tarjeta tapa el contenido, tenés el z-order
> invertido — no lo arregles moviendo controles de a uno, rehacé el orden de inserción.

> **`lblMarcaSes` va en `FontWeight.Bold`**: el mockup lo escribe como `<h2>` y el
> navegador lo pinta bold. Mismo criterio que `ESTILO-DLG-TITULO` en `01` §6.2.

> ❌ **Sin sombra** (`box-shadow` de `.sesCard`): el Botón clásico —única superficie con
> `Radius`— **no expone `DropShadow`**. Pérdida ya declarada en el ledger de `00` (**L-12**).

---

## 5. Bloque A1 — estado inicial (mockup `#a1`)

`Visible` de los tres controles: **`!ctxEsperandoSes && !ctxRechazadaSes`**.

| Control | Tipo | Propiedades |
|---|---|---|
| `lblLabMesaSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `lblMarcaSubSes.Y + lblMarcaSubSes.Height + 17` · Width `370` · Height `15` · **ESTILO-CAMPO-LAB** · `Text` `"Mesa de conteo"` · `Visible` `!ctxEsperandoSes && !ctxRechazadaSes` |
| `ddMesaSes` | **Menú desplegable clásico** | X `btnCardSes.X + 30` · Y `lblLabMesaSes.Y + lblLabMesaSes.Height + 5` · Width `370` · Height **`38`** · `Size` **`9.75`** · `Color` `ThemeInk` · `Fill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Items` `colMesasSes` · **`Value` `Name`** · `DisplayMode` ver abajo · `AccessibleLabel` `"Mesa de conteo"` · `TabIndex` `0` · `Visible` `!ctxEsperandoSes && !ctxRechazadaSes` |
| `lblNotaMesaSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `ddMesaSes.Y + ddMesaSes.Height + 6` · Width `370` · Height `30` · **ESTILO-META-CHICA** · `Wrap` **`true`** · `Color` ver abajo · `Text` ver abajo · `Visible` `!ctxEsperandoSes && !ctxRechazadaSes` |

```powerfx
// ddMesaSes.DisplayMode — sin mesas no hay nada que elegir
If(gvarIdentidadOk && !IsEmpty(colMesasSes), DisplayMode.Edit, DisplayMode.Disabled)
```

```powerfx
// lblNotaMesaSes.Text — la nota del mockup, con estado vacío (D02-15)
If(
    IsEmpty(colMesasSes),
    "No hay mesas disponibles: todas las mesas habilitadas ya tienen una sesión de hoy. Avise al supervisor.",
    "Solo se muestran las mesas sin sesión activa hoy."
)
```

```powerfx
// lblNotaMesaSes.Color
If(IsEmpty(colMesasSes), ThemeWarn, ThemeInk3)
```

**Cómo se llena el desplegable.** `Items` es la **colección local** `colMesasSes`, que el
`OnVisible` (§1 bloque 1) arma restando, **en memoria**, las mesas con sesión de hoy a las
mesas habilitadas. La columna que el control pinta se elige en el panel de propiedades,
campo **`Value`** → `Name`. `ddMesaSes.Selected` devuelve entonces el **registro completo**
de `CH-Tbl-CountingDesks`, que es lo que consume el `Patch` de §8.

> 🧨 **El desplegable clásico AUTO-SELECCIONA la primera fila.** Es el comportamiento que
> dibuja el mockup (`A1` muestra *Mesa 01* elegida) y por eso `AllowEmptySelection` queda
> en su default (`false`) y `Default` en blanco. Consecuencia asumida: *Solicitar mesa*
> nace **habilitado**. La guarda real vive igual en el `OnSelect` (§8) y **habla**.

> **Divergencia `D02-8`**: el mockup escribe *"Mesa 01 — Bóveda A"*.
> `CH-Tbl-CountingDesks` tiene `Name`, `Cajero`, `MachineSerialNumber` y `Enable` — **no
> tiene columna de ubicación** (`01` §2.4). El desplegable muestra `Name` a secas. **El
> modelo manda sobre el mockup.**

> **Divergencia `D02-9`**: `Height 38` y `Size 9.75` contra los `32` / `9` que `01` §6.1
> fija como default del desplegable clásico. Motivo: acá el campo **es** la pantalla
> (`.cin{min-height:38px;font-size:13px}` en el mockup), no un control que comparte fila
> con botones de 32.

> **Divergencia ya declarada en `00` (`L-13`)**: el desplegable clásico **no expone
> `Radius`** ⇒ queda de esquina viva contra el `border-radius:7px` del mockup. El borde, el
> alto y el fondo sí se respetan.

---

## 6. Bloque A2 — esperando aprobación (mockup `#a2`)

`Visible` de los diez controles: **`ctxEsperandoSes`**.

### 6.1 La franja de espera (`.waitRow`)

| Control | Tipo | Propiedades |
|---|---|---|
| `btnEsperaSes` | Botón clásico | receta 7.1 · X `btnCardSes.X + 30` · Y `lblLabMesaSes.Y` · Width `370` · Height `62` · `Radius` (las 4) `11` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` · `Visible` `ctxEsperandoSes` |
| `spnEsperaSes` | **Spinner moderno** | receta 7.7 · X `btnEsperaSes.X + 16` · Y `btnEsperaSes.Y + 14` · Width `34` · Height `34` · `Visible` `ctxEsperandoSes` |
| `lblEsperaTitSes` | Etiqueta clásica | X `spnEsperaSes.X + spnEsperaSes.Width + 14` · Y `btnEsperaSes.Y + 14` · Width `btnEsperaSes.Width - (Self.X - btnEsperaSes.X) - 16` · Height `18` · `Size` `9.75` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeInk` · `Text` `"Esperando aprobación del supervisor"` · `Visible` `ctxEsperandoSes` |
| `lblEsperaSubSes` | Etiqueta clásica | X `lblEsperaTitSes.X` · Y `lblEsperaTitSes.Y + lblEsperaTitSes.Height` · Width `lblEsperaTitSes.Width` · Height `16` · `Size` `8.63` · `Color` `ThemeInk3` · `Text` `"Se consulta el estado cada 15 segundos."` · `Visible` `ctxEsperandoSes` |

> **El texto *"cada 15 segundos"* NO es decorativo**: es el `Duration` de `tmrPollSes`
> (§10) escrito para el usuario. Si alguna vez cambia el intervalo, **cambian los dos** o
> la pantalla miente.

> **Divergencia `D02-19`**: el Spinner moderno **no expone el color de la pista**
> (`.spin{border:3px solid var(--line); border-top-color:var(--accent)}`): toma el tema de
> la app. Es uno de los **dos** modernos permitidos (`01` §0.3) y no hay equivalente
> clásico animado — el óvalo se acepta como viene.

### 6.2 El resumen `kv` (mockup `.kv`, 3 pares)

Dos etiquetas por par, sin galería (la cantidad de pares es fija y conocida — mismo patrón
que la receta 7.2.b, adaptado al ancho de esta tarjeta). **Paso vertical: `21`**
(`17` de línea + los `4` del `gap` del mockup), encadenado, nunca literales sueltos.

| Control | Tipo | Propiedades |
|---|---|---|
| `lblKvMesaEtqSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `btnEsperaSes.Y + btnEsperaSes.Height + 15` · Width `120` · Height `17` · `Size` `9` · `Color` `ThemeInk3` · `Text` `"Mesa solicitada"` · `Visible` `ctxEsperandoSes` |
| `lblKvMesaSes` | Etiqueta clásica | X `btnCardSes.X + 150` · Y `lblKvMesaEtqSes.Y` · Width `250` · Height `17` · `Size` `9` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeInk` · `Text` `gvarMesa.Name` · `Visible` `ctxEsperandoSes` |
| `lblKvFechaEtqSes` | Etiqueta clásica | X `lblKvMesaEtqSes.X` · Y `lblKvMesaEtqSes.Y + 21` · Width `120` · Height `17` · ídem `lblKvMesaEtqSes` · `Text` `"Solicitada"` |
| `lblKvFechaSes` | Etiqueta clásica | X `lblKvMesaSes.X` · Y `lblKvFechaEtqSes.Y` · Width `250` · Height `17` · ídem `lblKvMesaSes` · `Text` `Text(gvarSolicitud.'Created On', FmtFecha)` |
| `lblKvCajeroEtqSes` | Etiqueta clásica | X `lblKvMesaEtqSes.X` · Y `lblKvFechaEtqSes.Y + 21` · Width `120` · Height `17` · ídem `lblKvMesaEtqSes` · `Text` `"Cajero"` |
| `lblKvCajeroSes` | Etiqueta clásica | X `lblKvMesaSes.X` · Y `lblKvCajeroEtqSes.Y` · Width `250` · Height `17` · ídem `lblKvMesaSes` · `Text` `gvarUser.'Full Name'` |

> **Divergencia `D02-13`**: la fecha sale con **`FmtFecha`** (`"dd/mm/yyyy HH:mm"` ⇒
> `16/08/2026 07:41`), no con el *"16 ago 2026 · 07:41"* del mockup. `FmtFecha` es la única
> definición de formato de fecha de la app (`01` §3.1) y usa **`HH`** — `hh` sería reloj de
> 12 h **sin AM/PM**, que es el error que la skill marca como obligatorio evitar.

> **Dependencia declarada (`B02-1`, §12.1)**: `gvarSolicitud.'Created On'` sale del registro
> que devolvió el `Patch` (§8). Si tu entorno no devuelve las columnas de sistema en el
> registro patcheado, la fila queda **vacía como mucho 15 segundos**: el primer tick del
> polling re-lee la solicitud por PK (**Q9**) y la completa. No hace falta ningún parche.

---

## 7. Bloque A3 — solicitud rechazada (mockup `#a3`)

`Visible` de los siete controles: **`ctxRechazadaSes`**.

Es el estado que la v1 **no distinguía** (**S-4**): allá el polling preguntaba
`ApprovalStatus = Blank()` y un **rechazo** liberaba al cajero exactamente igual que una
aprobación. Acá el rechazo **bloquea**, muestra el motivo y obliga a elegir otra mesa.

### 7.1 El banner de peligro (receta 7.2.a, variante `danger`)

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAvisoRechazoSes` | Botón clásico | receta 7.2.a · X `btnCardSes.X + 30` · Y `lblLabMesaSes.Y` · Width `370` · Height **`58`** (2 líneas) · `Radius` (las 4) `9` · `Fill`/`HoverFill`/`PressedFill` `ThemeDangerSoft` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · **`Visible` `ctxRechazadaSes`** |
| `icoAvisoRechazoSes` | Icono clásico `Icon.Blocked` | X `btnAvisoRechazoSes.X + 13` · Y `btnAvisoRechazoSes.Y + 12` · Width `16` · Height `16` · `Color` `ThemeDanger` · `TabIndex` `-1` · `AccessibleLabel` `"Solicitud rechazada"` · `Visible` `btnAvisoRechazoSes.Visible` |
| `lblAvisoRechazoTitSes` | Etiqueta clásica | X `btnAvisoRechazoSes.X + 38` · Y `btnAvisoRechazoSes.Y + 10` · Width `btnAvisoRechazoSes.Width - 50` · Height `18` · `Size` `9` · `FontWeight` `FontWeight.Semibold` · `Color` `ThemeDanger` · `Text` `"Solicitud rechazada por el supervisor"` · `Visible` `btnAvisoRechazoSes.Visible` |
| `lblAvisoRechazoSubSes` | Etiqueta clásica | X `lblAvisoRechazoTitSes.X` · Y `lblAvisoRechazoTitSes.Y + lblAvisoRechazoTitSes.Height` · Width `lblAvisoRechazoTitSes.Width` · Height `18` · `Size` `9` · `Color` `ThemeDanger` · `Text` ver abajo · `Visible` `btnAvisoRechazoSes.Visible` |

```powerfx
// lblAvisoRechazoSubSes.Text — mesa · fecha de resolución · quién resolvió (01 §5.8)
gvarMesa.Name &
"   ·   " & Text(gvarSolicitud.ResolutionDatetime, FmtFecha) &
If(
    IsBlank(gvarSolicitud.SolvedBy.'Full Name'),
    "",
    "   ·   " & gvarSolicitud.SolvedBy.'Full Name'
)
```

> **`Visible` se escribe UNA sola vez** (en la superficie) y los otros tres lo referencian
> — regla de la receta 7.2.a.

> **Divergencia `D02-14`**: la receta 7.2.a define **una** etiqueta por banner; acá son
> **dos**, porque el mockup pinta la primera línea en `<b>` y la segunda en regular
> (`<b>Solicitud rechazada…</b><br>Mesa 03 · 16 ago 2026 07:48`). Una Etiqueta clásica
> tiene **un solo** `FontWeight`: partirla es la única forma nativa de conseguir el
> contraste (mismo criterio que el chip de lote de Reception v2).

> ✅ **`B02-2` — CERRADA con evidencia del modelo.** `SolvedBy` **es un lookup a `Users`
> (`SystemUser`)**, no texto: `solutions/unpacked/core/Entities/sanic_CHTblApprovalRequest/Entity.xml`
> declara `<attribute PhysicalName="sanic_SolvedBy"><Type>lookup</Type>` y
> `Other/Relationships/SystemUser.xml` la relación
> `sanic_chtblapprovalrequest_SolvedBy_systemuser`. **`gvarSolicitud.SolvedBy.'Full Name'`
> es la forma correcta y definitiva** (`01` §2.4): es un dot-walk de un salto sobre un
> registro ya cargado, dentro del `Text` de una etiqueta — no toca la delegación de nada.
> El `If(IsBlank(...))` se conserva porque `SolvedBy` está **vacío mientras la solicitud
> está pendiente**. Ya no es una dependencia abierta.

### 7.2 El motivo del rechazo

| Control | Tipo | Propiedades |
|---|---|---|
| `lblLabMotivoSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `btnAvisoRechazoSes.Y + btnAvisoRechazoSes.Height + 15` · Width `370` · Height `15` · **ESTILO-CAMPO-LAB** · `Text` `"Motivo indicado por el supervisor"` · `Visible` `ctxRechazadaSes` |
| `btnCajaMotivoSes` | Botón clásico | receta 7.1 · X `btnCardSes.X + 30` · Y `lblLabMotivoSes.Y + lblLabMotivoSes.Height + 5` · Width `370` · Height `66` · `Radius` (las 4) `8` · `Fill`/`HoverFill`/`PressedFill` `ThemeSurface` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` · `Visible` `ctxRechazadaSes` |
| `lblMotivoSes` | Etiqueta clásica | X `btnCajaMotivoSes.X + 10` · Y `btnCajaMotivoSes.Y + 7` · Width `btnCajaMotivoSes.Width - 20` · Height `btnCajaMotivoSes.Height - 14` · `Size` `9` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Top` · `Color` ver abajo · `Text` ver abajo · `Visible` `ctxRechazadaSes` |

```powerfx
// lblMotivoSes.Text — el motivo REAL que escribió el supervisor (01 §2.4: StatusReason)
Coalesce(
    Trim(gvarSolicitud.StatusReason),
    "El supervisor no indicó un motivo. Consúltelo antes de volver a solicitar la mesa."
)
```

```powerfx
// lblMotivoSes.Color
If(IsBlank(Trim(gvarSolicitud.StatusReason)), ThemeInk3, ThemeInk2)
```

> 🧨 **De dónde sale el motivo: `StatusReason`.** Auditado en `01` §2.4
> (`CH-Tbl-ApprovalRequests` → `StatusReason`, texto (200), la app **solo la lee**) y
> confirmado por `01` §5.8, que la nombra como *"el motivo del rechazo que se muestra en
> `A3` y `C8`"*. **No hace falta ninguna columna nueva.** El `Coalesce` es el fallback
> declarado para el caso en que el supervisor rechace sin escribir nada: la caja **nunca**
> queda vacía sin explicación.

> **La caja del motivo es una SUPERFICIE + una ETIQUETA, no una Entrada de texto.** Es un
> dato de solo lectura: una Entrada de texto deshabilitada se pintaría gris
> (`DisabledFill`) y entraría en el recorrido de TAB para nada.

---

## 8. Acciones, pie y la solicitud de mesa

Los tres botones de acción son **mutuamente excluyentes** y comparten la misma `Y`: solo
uno está visible por estado, así que pueden solaparse sin conflicto. Se anclan **desde
abajo**, contra el pie.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnSolicitarSes` | Botón clásico | **ESTILO-BTN-PRIMARIO** con `Size` `9.75` y `Height` `40` · X `btnCardSes.X + 30` · Y `lblPieSes.Y - 15 - Self.Height` · Width `370` · `Text` `"Solicitar mesa"` · `Visible` `!ctxEsperandoSes && !ctxRechazadaSes` · `DisplayMode` ver abajo · `AccessibleLabel` `"Solicitar la mesa de conteo seleccionada"` · `OnSelect` §8.1 |
| `btnCancelarSolicitudSes` | Botón clásico | **ESTILO-BTN-FANTASMA** (`Height` `32`) · X `btnCardSes.X + 30` · Y `lblPieSes.Y - 15 - Self.Height` · Width `370` · `Text` `"Cancelar solicitud"` · `Visible` `ctxEsperandoSes` · `DisplayMode` `If(gvarIdentidadOk, DisplayMode.Edit, DisplayMode.Disabled)` · `AccessibleLabel` `"Cancelar la solicitud de mesa"` · `OnSelect` `UpdateContext({ ctxDlgCancelarSes: true })` |
| `btnElegirOtraSes` | Botón clásico | **ESTILO-BTN-PRIMARIO** con `Size` `9.75` y `Height` `40` · X `btnCardSes.X + 30` · Y `lblPieSes.Y - 15 - Self.Height` · Width `370` · `Text` `"Elegir otra mesa"` · `Visible` `ctxRechazadaSes` · `DisplayMode` `If(gvarIdentidadOk, DisplayMode.Edit, DisplayMode.Disabled)` · `AccessibleLabel` `"Volver a elegir mesa"` · `OnSelect` **`Select(btnAbandonarSes)`** (§9) |
| `lblPieSes` | Etiqueta clásica | X `btnCardSes.X + 30` · Y `btnCardSes.Y + btnCardSes.Height - 26 - Self.Height` · Width `370` · Height `34` · **ESTILO-META-CHICA** · `Align` `Align.Center` · **`Wrap` `true`** · `Text` ver abajo |

```powerfx
// btnSolicitarSes.DisplayMode
//   gvarIdentidadOk va como PRIMER término, sin excepción (01 §3.2 / §6.3).
//   La segunda condición se escribe contra el control que la acción REALMENTE usa.
If(
    gvarIdentidadOk && !IsBlank(ddMesaSes.Selected.'CH-Tbl-CountingDesk'),
    DisplayMode.Edit,
    DisplayMode.Disabled
)
```

```powerfx
// lblPieSes.Text — la nota al pie, textual del mockup en los tres estados
If(
    ctxRechazadaSes, "El rechazo bloquea el ingreso: sin aprobación no se abre sesión.",
    ctxEsperandoSes, "No cierre la aplicación: al aprobarse, la sesión se abre sola.",
    "La solicitud queda pendiente hasta que el supervisor la apruebe. Si la mesa ya es suya, la aprobación es automática del lado del servidor."
)
```

### 8.1 `btnSolicitarSes.OnSelect` — crear la sesión y la solicitud

**Dos escrituras, en este orden, y la segunda compensa a la primera si falla.** Sin forms,
sin `NewForm`+`SubmitForm`, sin `Confirmed`, sin `ApprovalStatus`.

```powerfx
// ============================================================================
// GUARDA PARLANTE. El DisplayMode ya cubre el caso, pero si alguna vez se
// aflojara, esta guarda HABLA en vez de fallar en silencio.
// ============================================================================
If(
    IsBlank(ddMesaSes.Selected.'CH-Tbl-CountingDesk'),
    Notify("Elija una mesa de conteo antes de solicitar.", NotificationType.Warning),

    // ========================================================================
    // 1. LA SESIÓN.  Q5, receta 7.5 forma A.
    //    🧨 `Confirmed` NO se escribe: lo decide el plugin comparando
    //    CountingDesk.Cajero contra el InitiatingUserId del contexto de
    //    ejecución (00 §2.2 punto 1). Ahí muere S-1: la v1 escribía
    //    `Confirmed: true` desde el canvas cuando "la mesa era la propia", y
    //    con gvarUser en blanco cualquiera era dueño de la primera mesa libre.
    // ========================================================================
    IfError(
        Set(
            gvarSesion,
            Patch(
                'CH-Tbl-RecountingSessions',
                Defaults('CH-Tbl-RecountingSessions'),   // Name es AUTONUMBER: no se toca
                {
                    Cashier:      gvarUser,
                    CountingDesk: ddMesaSes.Selected
                }
            )
        ),
        If(true,
            Notify("No se pudo crear la sesión: " & FirstError.Message, NotificationType.Error);
            Set(gvarSesion, Blank());
            Blank()
        )
    );

    If(
        !IsBlank(gvarSesion.'CH-Tbl-RecountingSession'),

        // ====================================================================
        // 2. LA SOLICITUD.  Q6, contrato de claves de 01 §5.7.
        //    - RequestType: string EXACTO de 01 §5.6.
        //    - RequestDetails: fxHtmlMesa de 01 §3.1. CERO escapes manuales
        //      acá: el HtmlEscape() vive DENTRO de las fx… (receta 7.6 regla 1).
        //      Si escapás afuera, el & de &amp; se re-escapa y el supervisor ve
        //      basura. La inyección HTML de la v1 (S-3) estaba confirmada.
        //    - RequestResources: SIEMPRE con JSON(), jamás concatenando (la v1
        //      concatenaba con """" y era ilegible además de frágil).
        //    - ApprovalStatus: NO se setea. En blanco = Pendiente (01 §5.5).
        //    - BagNumber: NO se escribe. Esta solicitud no tiene tula (D02-17).
        // ====================================================================
        Set(gvarMesa, ddMesaSes.Selected);
        IfError(
            Set(
                gvarSolicitud,
                Patch(
                    'CH-Tbl-ApprovalRequests',
                    Defaults('CH-Tbl-ApprovalRequests'),
                    {
                        RequestType:    "Crear sesion de recuento",
                        RequestDetails: fxHtmlMesa(
                                            gvarUser.'Full Name',
                                            gvarMesa.Name,
                                            Text(Today(), FmtFechaCorta),
                                            Text(Now(),   FmtHora)
                                        ),
                        RequestResources: JSON({
                            RecountingSessionGUID: Text(gvarSesion.'CH-Tbl-RecountingSession')
                        })
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

            // ================================================================
            // 3. COMPENSACIÓN.  Q7.
            //    Una sesión sin solicitud bloquea la mesa TODO el día y nadie
            //    puede aprobarla: no hay solicitud que aprobar. Se revierte.
            //    Divergencia D02-7: 01 §5.7 solo blanquea gvarSolicitud.
            // ================================================================
            IfError(
                Remove('CH-Tbl-RecountingSessions', gvarSesion),
                Notify(
                    "La sesión quedó creada sin solicitud y no se pudo revertir. Avise al supervisor antes de reintentar.",
                    NotificationType.Error
                )
            );
            Set(gvarSesion, Blank());
            Set(gvarMesa,   Blank()),

            // ================================================================
            // 4. CAMINO FELIZ: a esperar. El timer arranca SOLO (§10).
            // ================================================================
            UpdateContext({ ctxEsperandoSes: true, ctxRechazadaSes: false })
        )
    )
)
```

> 🧨 **El `Patch` va envuelto en un `Set`, las dos veces.** El registro que devuelve —con su
> PK— es lo que el polling usa para preguntar por PK (`01` §5.7). Sin eso, el polling
> tiene que **adivinar** cuál es "su" solicitud, que es exactamente lo que hacía la v1.

> 🧨 **`Confirmed` y `ApprovalStatus` no aparecen en ninguna de las dos escrituras.** Si
> alguna vez ves uno de los dos en un `Patch` de esta app, es **S-1 volviendo**: reportalo.

> **Por qué la sesión va PRIMERO y la solicitud después**: `RequestResources` necesita el
> GUID de la sesión (`RecountingSessionGUID`, `01` §5.7). Al revés no se puede — y por eso
> existe el paso 3.

---

## 9. Abandonar la sesión en curso (`Cancelar solicitud` / `Elegir otra mesa`)

`01` §5 **no contempla la cancelación de una solicitud**: define cómo se crea y cómo se
resuelve, no cómo se retira. Esta guía la define y la declara (**D02-4**).

**Qué significa abandonar**: dar de baja la solicitud **si sigue sin resolver** (sale de la
cola del supervisor) y dar de baja la sesión (libera la mesa), y recién entonces volver la
tarjeta al estado inicial. **Las bajas que corresponden, o ninguna**: la UI nunca marca como
liberado lo que Dataverse rechazó (receta `01` §7.5, regla 3). Desde **A3** la solicitud ya
está **rechazada** y **no se toca** (`D02-5`): se da de baja **solo la sesión**.

Los **dos** botones del mockup terminan en la misma acción, así que la acción vive en **un
solo lugar**: `btnAbandonarSes`.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAbandonarSes` | Botón clásico — **motor 1 × 1** (`L-53`) | X `0` · **Y `767`** · Width `1` · Height `1` · **`Visible` `true`** · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · `Text` `""` · `TabIndex` `-1` · **`DisplayMode` `DisplayMode.Edit`** (sin condición) · `AccessibleLabel` `""` · `OnSelect` ver abajo |
| `cmpDlgCancelarSes` | **Componente canvas** `cmpDialogo` (`01` §8.3) | X `0` · Y `0` · Width `1366` · Height `768` · **anteúltimo del árbol** · propiedades ver abajo |

> 🧨 **`btnAbandonarSes` NO es `Visible: false`: es un motor de 1 × 1 transparente.**
> `Select()` sobre un control **oculto** no está garantizado por la plataforma, y acá un
> `Select()` que no dispara deja la solicitud viva y la mesa ocupada **en silencio**. Es el
> mismo patrón que los cinco motores de `03` (`D03-9`) y la regla común del ledger `L-53`:
> **`Select()` jamás apunta a un control invisible.** El botón mide 1 × 1, es transparente,
> está apoyado en la última fila de píxeles de la pantalla (`Y 767`) y sale del recorrido de
> TAB con `TabIndex -1`: el cajero no lo ve ni lo puede tocar.

> 🧨 **`Select()` NO funciona sobre `DisplayMode.Disabled`**: por eso `btnAbandonarSes` va
> en `DisplayMode.Edit` **sin condición**, y la guarda de identidad vive en los dos botones
> visibles que lo invocan (`btnCancelarSolicitudSes` y `btnElegirOtraSes`).

### 9.1 El diálogo de confirmación (solo para *Cancelar solicitud*)

```powerfx
// ===== cmpDlgCancelarSes — propiedades de la INSTANCIA =====
Visible        = ctxDlgCancelarSes
Titulo         = "Cancelar la solicitud de mesa"
Subtitulo      = "Se retira la solicitud de la cola del supervisor y se libera la mesa " &
                 gvarMesa.Name & "." & Char(10) & Char(10) &
                 "Vuelve a la selección de mesa. La solicitud no se puede reactivar: hay que pedirla de nuevo."
TextoConfirmar = "Cancelar la solicitud"
TextoCancelar  = "Seguir esperando"
Peligro        = true
AnchoCard      = 400
OnCancelar     = UpdateContext({ ctxDlgCancelarSes: false })
OnConfirmar    = UpdateContext({ ctxDlgCancelarSes: false }); Select(btnAbandonarSes)
```

> 🧨 **CONFIRMAR ANTES DE EJECUTAR** (receta 7.2, regla 2): el `OnSelect` de
> `btnCancelarSolicitudSes` **solo abre el diálogo**. La baja vive en el `OnConfirmar`, y de
> ahí en `btnAbandonarSes`. Si en esta guía ves un `Remove` en el botón que **abre** un
> diálogo, es un bug.

> 🧨 **El `OnConfirmar` INVOCA, no duplica** (`01` §8.3, regla 4): `Select(btnAbandonarSes)`
> en vez de copiar las 40 líneas. La baja vive en **un** lugar.

> **Divergencia `D02-5`**: *Elegir otra mesa* (A3) **no** lleva diálogo, contra la regla 4
> de la receta 7.2 (*"todo borrado es destructivo ⇒ tiene diálogo"*). Motivo: lo que se da
> de baja ahí es una sesión que el supervisor **ya rechazó** y que nunca estuvo confirmada
> —no hay nada que perder— y la solicitud rechazada **no se toca**: queda como registro. En
> A2, en cambio, se retira una solicitud que el supervisor está mirando: esa sí confirma.

> **Divergencia `D02-6`**: `01` §8.3 nota **C** reparte los diálogos de la app entre
> `cmpDialogo` y la receta 7.2, y **no lista ninguno de `02`**. Este entra por
> `cmpDialogo`: es un diálogo de **texto puro** (título + subtítulo + dos acciones), que es
> exactamente lo que el componente cubre. Fallback documentado en `01` §8.3 nota **B**.

### 9.2 `btnAbandonarSes.OnSelect`

```powerfx
UpdateContext({ ctxAbandonoOkSes: true });

// ============================================================================
// 1. RE-LECTURA POR PK (Q9). Entre que el cajero abrió el diálogo y confirmó,
//    el supervisor pudo resolver. Cancelar una solicitud YA RESUELTA sería
//    borrar la evidencia de una decisión del supervisor.
// ============================================================================
If(
    !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest'),
    Set(
        gvarSolicitud,
        LookUp(
            'CH-Tbl-ApprovalRequests',
            'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest'
        )
    )
);

If(
    // ========================================================================
    // CARRERA (solo posible desde A2): ya la resolvieron. NO se borra NADA.
    //   El timer sigue armado —su Start es ctxEsperandoSes (§10)— y en el
    //   próximo tick pinta el estado real. Como mucho, 15 segundos.
    // ========================================================================
    ctxEsperandoSes && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest') && !IsBlank(gvarSolicitud.ApprovalStatus),
        Notify(
            "El supervisor ya resolvió la solicitud: la pantalla se actualiza sola en unos segundos.",
            NotificationType.Information
        ),

    // ========================================================================
    // ABANDONO REAL
    // ========================================================================
    // ---- 1. La solicitud primero: sale de la cola del supervisor (Q8) -------
    //    🧨 GATE DOBLE — PK **y** estado:
    //      · PK: la existencia se pregunta por 'CH-Tbl-ApprovalRequest', nunca con
    //        IsBlank(<registro>), que sobre un registro tipado vacío puede dar false.
    //      · Estado: SOLO se borra una solicitud **sin resolver**. Desde A3 la
    //        solicitud está RECHAZADA: es la evidencia de una decisión del
    //        supervisor y se conserva como registro (D02-5). Esta rama se SALTEA,
    //        `ctxAbandonoOkSes` queda en `true` y los pasos 2 y 3 siguen encadenando
    //        normalmente ⇒ se borra solo la sesión y la tarjeta vuelve a A1.
    //      · Es también el privilegio que el guard concede (`00` §2.2 punto 5:
    //        Delete de solicitudes PROPIAS y SIN RESOLVER).
    If(
        !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest') &&
        IsBlank(gvarSolicitud.ApprovalStatus),
        IfError(
            Remove('CH-Tbl-ApprovalRequests', gvarSolicitud),
            If(true,
                Notify("No se pudo cancelar la solicitud: " & FirstError.Message, NotificationType.Error);
                UpdateContext({ ctxAbandonoOkSes: false });
                Blank()
            )
        )
    );

    // ---- 2. La sesión después: libera la mesa (Q7) --------------------------
    If(
        ctxAbandonoOkSes,
        IfError(
            Remove('CH-Tbl-RecountingSessions', gvarSesion),
            If(true,
                Notify("No se pudo liberar la mesa: " & FirstError.Message, NotificationType.Error);
                UpdateContext({ ctxAbandonoOkSes: false });
                Blank()
            )
        )
    );

    // ---- 3. Solo con las DOS bajas hechas se vuelve al estado inicial -------
    //    El catálogo se recompone en MEMORIA (cero red): se quita la sesión
    //    liberada de colSesionesHoySes y se recalcula colMesasSes desde el
    //    snapshot colMesasDVSes que el OnVisible ya dejó cargado. Así la mesa
    //    recién liberada vuelve a aparecer en el desplegable EN EL ACTO.
    If(
        ctxAbandonoOkSes,
        Remove(
            colSesionesHoySes,
            LookUp(
                colSesionesHoySes,
                'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
            )
        );
        ClearCollect(
            colMesasSes,
            Filter(
                colMesasDVSes As M,
                IsBlank(
                    LookUp(
                        colSesionesHoySes,
                        CountingDesk.'CH-Tbl-CountingDesk' = M.'CH-Tbl-CountingDesk'
                    )
                )
            )
        );
        Set(gvarSolicitud, Blank());
        Set(gvarSesion,    Blank());
        Set(gvarMesa,      Blank());
        // El aviso se emite ANTES de bajar las banderas: desde A3 la solicitud
        // rechazada NO se borró, y decir "solicitud cancelada" sería mentir.
        Notify(
            If(
                ctxRechazadaSes,
                "Mesa liberada. La solicitud rechazada queda registrada. Elija otra mesa.",
                "Solicitud cancelada. Elija otra mesa."
            ),
            NotificationType.Information
        );
        UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: false });
        Reset(ddMesaSes)
    )
)
```

> 🧨 **Las dos entradas del abandono, y qué borra cada una** (gate del paso 1):
>
> | Desde | `ApprovalStatus` | Paso 1 (solicitud) | Pasos 2 y 3 |
> |---|---|---|---|
> | **A2** — *Cancelar solicitud* | en blanco (Pendiente) | **`Remove`**: sale de la cola del supervisor | corren: se borra la sesión y la tarjeta vuelve a A1 |
> | **A3** — *Elegir otra mesa* | `Rechazado` | **se saltea**: la solicitud rechazada **queda como registro** | corren **igual**: `ctxAbandonoOkSes` sigue en `true` porque el paso 1 no falló — no se ejecutó |
>
> Es exactamente lo que promete `D02-5` (*"la solicitud rechazada **no se toca**: queda como
> registro"*) y lo que verifica el checklist de §14 (*"Elegir otra mesa … **deja intacta** la
> solicitud rechazada"*). Sin el gate, `Remove` intentaba borrar una solicitud **ya
> resuelta**: el guard la rechaza (`00` §2.2 punto 5 solo permite `Delete` de solicitudes
> **sin resolver**), `ctxAbandonoOkSes` bajaba a `false` y **la sesión nunca se borraba** ⇒
> la mesa quedaba ocupada todo el día y el cajero atrapado en A3.

> **`Remove` por REGISTRO, nunca `RemoveIf(ds, predicado)`** (regla **D-3**).

> **Dependencia declarada (`B02-3`, §12.1)**: el abandono exige privilegio de **Delete**
> del cajero sobre `CH-Tbl-ApprovalRequests` y `CH-Tbl-RecountingSessions` (sus propios
> registros). Si el rol no lo tiene, los dos `IfError` **hablan** con el mensaje del guard
> y la tarjeta se queda donde estaba — nada queda mintiendo. **Fallback operativo**: el
> cajero pide al supervisor que rechace o cierre la sesión; el polling y el `OnVisible`
> hacen el resto sin tocar la app.

---

## 10. Polling de aprobación (receta `01` §7.7 + contrato §5.8)

**UN** timer, **15 s**, **una** consulta delegable por tick, y el rechazo como estado de
primera clase.

| Control | Tipo | Propiedades |
|---|---|---|
| `tmrPollSes` | **Temporizador clásico** | X `0` · Y `0` · Width `1` · Height `1` · `AutoStart` `false` · `Duration` **`15000`** · `Repeat` `true` · **`Start`** ver abajo · **`Visible` `false`** · `OnTimerStart` **VACÍO** · `OnTimerEnd` ver abajo |

```powerfx
// tmrPollSes.Start — es una CONDICIÓN, jamás `true` (contrato 01 §5.8).
//   Cuando la solicitud se resuelve, la rama que la resuelve baja
//   ctxEsperandoSes y el timer se apaga SOLO: no hace falta apagarlo a mano.
//   🧨 La existencia se pregunta POR LA PK: sobre un registro tipado vacío
//      IsBlank(<registro>) puede devolver false.
ctxEsperandoSes && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')
```

```powerfx
// ===================== tmrPollSes.OnTimerEnd =====================
// Q9: UNA consulta, delegable, por PK. Cero Refresh acá arriba.
Set(
    gvarSolicitud,
    LookUp(
        'CH-Tbl-ApprovalRequests',
        'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest'
    )
);

If(
    // ================= LA SOLICITUD DESAPARECIÓ =================
    //   Alguien la borró (otro dispositivo, el supervisor, una limpieza).
    //   Esperar a algo que no existe es exactamente el fallo abierto que
    //   estamos matando: se vuelve al estado inicial y se AVISA.
    IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest'),
        UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: false });
        Set(gvarSolicitud, Blank());
        Set(gvarSesion,    Blank());
        Set(gvarMesa,      Blank());
        Notify("La solicitud ya no existe. Elija una mesa de nuevo.", NotificationType.Warning),

    // ================= APROBADO =================
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
        UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: false });
        // Q10 — el plugin ACABA de escribir Confirmed: se re-lee por PK.
        Set(
            gvarSesion,
            LookUp(
                'CH-Tbl-RecountingSessions',
                'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
            )
        );
        // ---- contrato de salida, copia literal de 01 §4.5 ----
        //    🧨 Acá el Navigate va INLINE, no por motor: un OnTimerEnd ES un manejador
        //       de comportamiento (receta 01 §7.7). La prohibición de L-65 es del
        //       OnVisible. La rama 1 del OnVisible sí usa Select(btnEntrarTulasSes) (§1.1).
        Set(gvarMesa, gvarSesion.CountingDesk);
        Set(gvarTula, Blank());
        Set(gvarDeclaredBag, Blank());
        Set(gvarSesionDetalle, Blank());
        Set(gvarSolicitud, Blank());
        Set(gvarDepositoSinGuardar, false);
        Navigate(scrTulas),

    // ================= RECHAZADO: BLOQUEA, no libera =================
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
        UpdateContext({ ctxEsperandoSes: false, ctxRechazadaSes: true }),

    // ================= SIGUE PENDIENTE: NO se hace NADA =================
    false
)
```

**Reglas que esta pantalla cumple al pie de la letra (receta 7.7):**

1. **UN timer, y ninguno más.** La v1 tenía **tres**: dos con `Repeat true` a 5 s con
   `Refresh` en `OnTimerStart` (≈10 consultas en cascada por ciclo) y uno con
   `Start: =true` **literal**, corriendo siempre.
2. **`OnTimerStart` vacío, y CERO `Refresh` en toda la pantalla.** Un `Refresh` en
   `OnTimerStart` invalida la caché de todas las tablas relacionadas en cada tick; y un
   `Refresh('CH-Tbl-Bags')` en las ramas de resolución tampoco tiene sentido acá:
   **`scrSesion` no lee `CH-Tbl-Bags`**. Quien las lee es `03`, que las trae con
   `ClearCollect` —que siempre va al servidor— y tiene su propio ⟳ (regla **D-5**, forma 1).
3. **El `Navigate` sale DEL `OnTimerEnd`**, no de un `Select()` posterior: los timers **no
   corren en pantallas que no están visibles** (`01` §7.10.C, regla 2).
4. **El rechazo BLOQUEA.** Nunca `IsBlank(…)` como condición de liberación.

> ✅ **`D02-2` — RESUELTA por consolidación → ver `01` §5.8.** El contrato madre fija
> `Start` = **`ctxEsperando<Scr> && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`** para
> las **tres** pantallas, por el mismo motivo que declaraba esta guía: `gvarSolicitud` se
> re-lee por PK en puntos que no son el tick (la compensación del abandono de §9.2, la
> re-adopción, la recuperación del `OnVisible`) y cualquiera de esas lecturas puede dejar
> `ApprovalStatus` ya resuelto **antes** de que la UI pinte el estado. `02` cumple el
> contrato tal cual: **las tres ramas que resuelven bajan `ctxEsperandoSes`**.
>
> 🧨 Única precisión sobre lo escrito arriba: la existencia se pregunta **por la PK**
> (`!IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`), no con `!IsBlank(gvarSolicitud)`.

> ✅ **`D02-3` — RESUELTA por consolidación → ver `01` §5.8.** El contrato madre ya no pide
> un `OR` de cinco tipos: **cada pantalla recupera SOLO el `RequestType` que produce**, y
> `02` produce `"Crear sesion de recuento"`. La consulta de §1 bloque 3 **es** la forma
> canónica para esta pantalla (tipo propio + ventana del día + solo si hay sesión de hoy).
> Se conserva **una** excepción declarada en el propio `01` §5.8 (regla 4 de la
> recuperación): `02` **no** exige `ApprovalStatus = Blank()`, porque necesita ver también
> el **rechazo** al reabrir la app — si no, vuelve al estado inicial y deja la sesión no
> confirmada **bloqueando la mesa todo el día**.

---

## 11. Overlay de bloqueo de identidad (`01` §3.2, ledger `L-16`)

`01` §3.2 lo declara **construido por `02`**. Cubre la pantalla entera cuando
`gvarIdentidadOk` es `false`, **sin botón de salida y sin `Navigate`**: la app se bloquea,
no degrada. Es el fix estructural de **S-1** — la v1 seguía andando con `gvarUser` en
blanco y `LookUp('CH-Tbl-CountingDesks', Cajero.User = Blank())` le entregaba **la primera
mesa sin cajero** a cualquiera.

**Coordenadas de los hijos: relativas al contenedor** (`Parent` = `cntBloqueoSes`).

| Control | Tipo | Propiedades |
|---|---|---|
| `cntBloqueoSes` | **Contenedor de diseño manual** | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Fill` `ThemeBg` · `Radius` (las 4) `0` · `BorderThickness` `0` · **`Visible` `!gvarIdentidadOk`** · **ÚLTIMO del árbol** |
| `btnBloqTragaSes` | Botón clásico (hijo, **al FONDO** del contenedor) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** · `TabIndex` `-1` — traga los clics y saca del recorrido de TAB todo lo que quedó debajo |
| `icoBloqueoSes` | Icono clásico `Icon.Blocked` | X `(Parent.Width - Self.Width) / 2` · Y `(Parent.Height - Self.Height) / 2 - 60` · Width `56` · Height `56` · `Color` `ThemeDanger` · `TabIndex` `-1` · `AccessibleLabel` `"Acceso bloqueado"` |
| `lblBloqueoSes` | Etiqueta clásica | X `(Parent.Width - Self.Width) / 2` · Y `icoBloqueoSes.Y + icoBloqueoSes.Height + 18` · Width `520` · Height `80` · **ESTILO-DLG-TEXTO** · `Align` `Align.Center` · **`Wrap` `true`** · `VerticalAlign` `VerticalAlign.Top` · `Text` ver abajo |

```powerfx
// lblBloqueoSes.Text — literal EXACTO de 01 §3.2. No se reescribe.
"No se pudo identificar su usuario en Dataverse. Cierre la aplicación y avise a soporte: sin identidad no se puede abrir una sesión de conteo."
```

> 🧨 **El overlay no es la única defensa: es la última.** La regla derivada de `01` §3.2 se
> cumple en toda la pantalla — **`gvarIdentidadOk &&` es el PRIMER término** de los
> `DisplayMode` de `btnSolicitarSes`, `btnCancelarSolicitudSes`, `btnElegirOtraSes` y
> `ddMesaSes`, y el `OnVisible` (§1) **no dispara ni una consulta** sin identidad resuelta.

---

## 12. Notas de la guía

### 12.1 Dependencias de backend y aproximaciones honestas

| ID | Qué | Dónde pega | Fallback declarado |
|---|---|---|---|
| **P02-1** | 🧨 **Plugin de auto-aprobación** en Create de `CH-Tbl-ApprovalRequests` tipo *"Crear sesion de recuento"*: compara `CountingDesk.Cajero` contra el `InitiatingUserId` y escribe `ApprovalStatus` + `Confirmed` (`00` §2.2 punto 1) | §8.1, §10 | 🚫 **NO hay fallback client-side.** Ponerlo de vuelta en el canvas **es** el bug **S-1**. Sin el plugin, **toda** solicitud —incluida la de la mesa propia— espera a un supervisor humano. La app funciona; el cajero espera más. **Nunca** se escribe `Confirmed` desde acá |
| **B02-1** | `'Created On'` en el registro devuelto por el `Patch` de la solicitud | §6.2 (`lblKvFechaSes`) | Si el entorno no devuelve columnas de sistema, la fila queda vacía **≤ 15 s**: el primer tick del polling re-lee por PK (**Q9**) y la completa. Sin parche |
| **B02-2** | ✅ **CERRADA** — `SolvedBy` **es** lookup a `Users` (`Entity.xml`: `Type: lookup`; relación `sanic_chtblapprovalrequest_SolvedBy_systemuser`) ⇒ `.'Full Name'` es correcto | §7.1 (`lblAvisoRechazoSubSes`) | Ninguno: deja de ser dependencia. Solo se mantiene el `Coalesce`/`If(IsBlank(…))` porque está vacío mientras la solicitud está pendiente |
| **B02-3** | Privilegio de **Delete** del cajero sobre `CH-Tbl-ApprovalRequests` y `CH-Tbl-RecountingSessions` (registros propios) | §8.1 paso 3 · §9.2 | Los dos `IfError` **hablan** con el mensaje del guard y la tarjeta no cambia de estado. Operativamente: el supervisor rechaza o cierra la sesión, y el polling / `OnVisible` hacen el resto |
| **B02-4** | Que `StatusReason` venga cargado en el rechazo | §7.2 (`lblMotivoSes`) | `Coalesce` con *"El supervisor no indicó un motivo…"*. La caja **nunca** queda vacía sin explicación |
| — | Ubicación de la mesa (*"— Bóveda A"*) | §5 | **No existe en el modelo.** Ver `D02-8` |
| — | Puesto del usuario (*"· Cajero de conteo"*) | §3 | **No existe sin conectores de directorio** (`01` §1.5). Ver `D02-12` |

> **Ningún espejo de `01` §2.2 (E-1..E-4) toca esta pantalla.** `scrSesion` no consulta
> tulas, depósitos, cheques ni lecturas de contadora. Es la **única** de las tres pantallas
> que se puede construir y probar de punta a punta **sin nada de backend nuevo** salvo
> `P02-1`.

### 12.2 Gotchas de canvas que esta pantalla pisa

1. 🧨 **`Navigate()` no corta la fórmula.** Todo lo que va después del `If` del `OnVisible`
   se ejecuta igual. Por eso el catálogo de mesas se carga **antes** de la máquina de
   estados y el `Reset(ddMesaSes)` va al final, inofensivo.
2. 🧨 **El desplegable clásico AUTO-SELECCIONA la primera fila** y **recuerda su selección
   entre visitas**: de ahí el `Reset(ddMesaSes)` al final del `OnVisible` y en §9.2.
3. 🧨 **Ni el Rectángulo ni la Etiqueta tienen `Radius`** (`01` §0.3). La tarjeta, el tile,
   el chip, el avatar, la franja de espera, el banner y la caja del motivo son **Botones
   clásicos** (receta 7.1). Si en esta guía ves un `Radius` sobre un Rectángulo o una
   Etiqueta, es un bug: reportalo.
4. 🧨 **Las superficies decorativas llevan `HoverFill` y `PressedFill` iguales al `Fill`**
   (receta 7.1) y `DisplayMode.View`: si no, la card **parpadea** al pasar el mouse.
5. 🧨 **Un timer no corre dentro de un contenedor invisible, ni en una pantalla no
   visible.** `tmrPollSes` cuelga de la **pantalla** y el `Navigate` de la aprobación vive
   **dentro** del `OnTimerEnd`.
6. 🧨 **`Select()` no funciona sobre `DisplayMode.Disabled`, y tampoco está garantizado
   sobre un control invisible** (`L-53`): `btnAbandonarSes` (§9) y `btnEntrarTulasSes`
   (§1.1) van en `DisplayMode.Edit` sin condición **y** `Visible true`, como **motores de
   1 × 1 transparentes** en `Y 767`.
7. 🧨 **`Navigate()` está PROHIBIDO en un `OnVisible`** (`L-65`): el App Checker lo marca
   *"would automatically always navigate away"*. La rama que navegaba pasa a
   `Select(<motor>)` y el `Navigate` vive en el `OnSelect` del motor (§1.1).
8. 🧨 **`ClearCollect(col, If(cond, …))` sin `else`** revienta con `ErrorKind.Internal`
   (regla **D-11**). En §1 y §9.2 el `If` va **siempre por fuera** del `ClearCollect`.
9. 🧨 **Cero escapes manuales de HTML en la pantalla** (receta 7.6, regla 1): `HtmlEscape()`
   vive **dentro** de `fxHtmlMesa`. Un `Substitute(…, "&", "&amp;")` acá sería doble escape
   y el supervisor vería basura.
10. **Toda etiqueta con `Wrap true` lleva `Height` explícito**: `lblNotaMesaSes` (30),
   `lblPieSes` (34), `lblMotivoSes` (`Height` derivado de su caja), `lblBloqueoSes` (80).
   Sin eso el texto se corta **en silencio**.
11. **`AccessibleLabel`**: cargarlo en `ddMesaSes`, en los tres botones de acción y en los
    dos iconos (`icoAvisoRechazoSes`, `icoBloqueoSes`). Sin eso, App Checker deja warnings
    de accesibilidad (`01` §9).
12. **Los glifos van como Icono clásico, nunca en el `Text`** (`01` §1.6 — tofu en Open
    Sans). El `⊘` del banner del mockup es `Icon.Blocked`. Única excepción: el `"C$"` de los
    dos tiles de marca, que son dos caracteres ASCII.

### 12.3 Costo de red declarado

Elegir mesa, leer la nota, ver el motivo del rechazo y abrir/cerrar el diálogo de
cancelación corren a **cero consultas**. La red se toca únicamente al entrar (Q1–Q4),
al solicitar (Q5 + Q6, más Q7 solo si hay que compensar), **una vez cada 15 s** mientras
espera (Q9), al aprobarse (Q10) y al abandonar (Q9 + Q8 + Q7, o Q9 + Q7 desde A3, donde la
solicitud rechazada **no se borra**). El catálogo de mesas se **recompone en memoria** tras
un abandono: no se vuelve a la red. **Cero `Refresh` en toda la pantalla.**

---

## 13. Divergencias declaradas (para subir al ledger de `00` §6)

Nacen en esta pantalla. Ninguna es silenciosa.

| # | Divergencia vs. mockup / `01` | Motivo |
|---|---|---|
| **D02-1** | La **espera** (A2) y el **rechazo** (A3) no son overlays (los `cntEspera<Scr>` / `cntRechazo<Scr>` de la receta 7.7): son **modos de la misma tarjeta**, y esta pantalla **no construye ninguno de los dos contenedores** | Es lo que dibuja el mockup: `A1`/`A2`/`A3` son el mismo lienzo con distinto contenido. La regla 5 de la receta 7.7 justifica el overlay en `04` (**hay conteo detrás que preservar**); acá **no hay nada detrás**: un overlay sobre una tarjeta vacía es chrome por chrome |
| **D02-2** | ✅ **RESUELTA por consolidación → ver `01` §5.8.** Ya **no** es divergencia: el contrato madre fija `Start` = `ctxEsperando<Scr> && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')` para las tres pantallas, con el motivo que declaraba esta guía | — |
| **D02-3** | ✅ **RESUELTA por consolidación → ver `01` §5.8.** El contrato madre ahora acota la recuperación al `RequestType` propio de cada pantalla; la consulta de §1 bloque 3 **es** la canónica de `02`. Sobrevive **una** excepción, ya declarada en `01` §5.8 regla 4: `02` no exige `ApprovalStatus = Blank()` para poder ver el **rechazo** al reabrir la app | Sin esa excepción, reabrir tras un rechazo volvía al estado inicial y dejaba la sesión no confirmada **bloqueando la mesa todo el día** |
| **D02-4** | *Cancelar solicitud* (A2) y *Elegir otra mesa* (A3) se implementan como **abandono con bajas**: `Remove` de la solicitud + `Remove` de la sesión, en ese orden, y **las dos o ninguna** — con **un gate**: la solicitud se borra **solo si está sin resolver** (`IsBlank(ApprovalStatus)`), así que desde A3 se da de baja **solo la sesión** | `01` §5 define cómo se crea y cómo se resuelve una solicitud, **no cómo se retira**. Sin las bajas, la mesa queda ocupada todo el día y la solicitud sigue en la cola del supervisor. El gate es lo que hace compatible A3 con `D02-5` (la solicitud rechazada queda como registro) y con el privilegio acotado de `00` §2.2 punto 5. Dependencia de privilegios declarada en `B02-3` |
| **D02-5** | Solo *Cancelar solicitud* lleva diálogo de confirmación; *Elegir otra mesa* no, contra la regla 4 de la receta 7.2 | En A3 se da de baja una sesión **ya rechazada** y nunca confirmada, y la solicitud rechazada **no se toca**: no hay nada que perder. En A2 se retira una solicitud que el supervisor está mirando |
| **D02-6** | `cmpDialogo` se usa en `02` (`cmpDlgCancelarSes`); la tabla de `01` §8.3 nota **C** no listaba ningún diálogo de esta pantalla | Es un diálogo de **texto puro** (título + subtítulo + dos acciones), exactamente lo que el componente cubre. `01` §8.3 nota **C** ya explica el criterio: cuerpo rico ⇒ receta 7.2, texto ⇒ componente |
| **D02-7** | Si el `Patch` de la solicitud falla, la sesión recién creada se **borra** (compensación). `01` §5.7 solo blanquea `gvarSolicitud` | Una sesión sin solicitud **bloquea la mesa todo el día y nadie puede aprobarla**: no hay solicitud que aprobar. La compensación es la única forma de dejar el sistema como estaba |
| **D02-8** | El desplegable muestra **`Name` a secas**; el mockup dibuja *"Mesa 01 — Bóveda A"* | `CH-Tbl-CountingDesks` tiene `Name`, `Cajero`, `MachineSerialNumber` y `Enable` (`01` §2.4): **no hay columna de ubicación**. El modelo manda sobre el mockup |
| **D02-9** | `ddMesaSes` va en `Height 38` / `Size 9.75`, contra los `32` / `9` que `01` §6.1 fija como default del desplegable clásico | Acá el campo **es** la pantalla (`.cin{min-height:38px;font-size:13px}` del mockup), no un control que comparte fila con botones de 32 |
| **D02-10** | ✅ **RESUELTA por consolidación → ver `01` §2.4.** La **lectura** de `SessionForciblyClosed` quedó legalizada y declarada obligatoria en el contrato madre (fila propia, `L` + 🚫 de escritura). Ya no es divergencia | Sin ese predicado, una sesión cerrada a la fuerza por el supervisor **atrapa** al cajero. **Escribirla sigue prohibido** |
| **D02-11** | El orden de las mesas se pide con **`Sort(Filter(…), Name)`**, no con `SortByColumns(…, "…")` como pide la regla **D-9** | `SortByColumns` exige el nombre de columna **como texto** y en Dataverse el que manda es el **lógico** (`sanic_name`): un display name ahí compila y falla en runtime. `Sort` con la columna tipada no tiene esa ambigüedad y **delega igual**. El espíritu de D-9 —orden explícito, nunca el que traiga el datasource— se cumple |
| **D02-12** | El chip de usuario muestra solo `UsuarioNombre`, sin *"· Cajero de conteo"* | `User()` da nombre y correo, **no** el puesto, y los conectores de directorio están prohibidos (`01` §1.5). Misma decisión que Reception v2 (`L-12` de su ledger) |
| **D02-13** | Las fechas usan **`FmtFecha`** (`16/08/2026 07:41`), no el *"16 ago 2026 · 07:41"* del mockup | `FmtFecha` es la única definición de formato de fecha de la app (`01` §3.1) y usa **`HH`**: `hh` sería reloj de 12 h **sin AM/PM** |
| **D02-14** | El banner de rechazo lleva **dos** etiquetas (título semibold + subtítulo regular); la receta 7.2.a define una | El mockup pinta la primera línea en `<b>` y la segunda en regular. Una Etiqueta clásica tiene **un solo** `FontWeight` |
| **D02-15** | `lblNotaMesaSes` tiene **estado vacío** (*"No hay mesas disponibles…"* en `ThemeWarn`) y `ddMesaSes` queda deshabilitado; el mockup no lo contempla | **Mejora deliberada**: con todas las mesas ocupadas, el mockup deja un desplegable vacío y un botón activo. El cajero no sabe si la app cargó o si no hay mesas. Un renglón lo resuelve, y el `DisplayMode` evita el clic al vacío |
| **D02-16** | `cntBloqueoSes` se inserta **después** de `cmpDlgCancelarSes`, aunque `01` §8.3 diga que la instancia de `cmpDialogo` va "insertada ÚLTIMA en la pantalla" | Un diálogo pintado **encima** del bloqueo de identidad sería una superficie interactiva en una app que no sabe quién la usa. El bloqueo es el tope absoluto del z-order y no cede el lugar a nada |
| **D02-17** | La solicitud de mesa **no escribe `BagNumber`**, que `01` §2.4 y el ledger `L-18` presentan como columna que la app escribe | `BagNumber` es el N° de tula, para que el Supervisor filtre sin parsear el JSON. **Esta solicitud no tiene tula**: escribir un valor inventado sería peor que dejarlo vacío. Los tres tipos de `03`/`04` sí la escriben |
| **D02-18** | Una **sesión de hoy sin solicitud** (huérfana) se degrada al estado inicial **con aviso**, y la app **no la borra sola** | Es un estado que solo se alcanza si fallaron el `Patch` de la solicitud **y** su compensación (D02-7). Borrar automáticamente una sesión cuyo origen la app no conoce es peor que avisar: la mesa queda ocupada hasta que el supervisor la cierre. **Candidato a GC server-side** en el guard de `00` §2.2 |
| **D02-19** | El Spinner moderno no reproduce la pista gris del mockup (`.spin{border:3px solid var(--line)}`): toma el color del tema | Es uno de los **dos** modernos permitidos (`01` §0.3) y no expone el color de la pista. No hay equivalente clásico animado |

---

## 14. Checklist de salida

Verificable en Studio, sin interpretación. **Corré `App.OnStart` antes de probar nada**
(botón derecho sobre **App** → *Ejecutar OnStart*).

**Estructura**

- [ ] `scrSesion` tiene **44 controles** con los nombres exactos del árbol, y **todos**
      terminan en el sufijo **`Ses`** (`01` §0.5).
- [ ] El orden del árbol de Studio, leído **de abajo hacia arriba**, es el del árbol de esta
      guía: `shpHeaderSes` primero (fondo) y `cntBloqueoSes` último (tope).
- [ ] `btnCardSes` está **debajo** de todo el contenido de la tarjeta: al pasar el mouse por
      la tarjeta **nada parpadea** y todos los textos se ven.
- [ ] `tmrPollSes`, `btnAbandonarSes` y `btnEntrarTulasSes` cuelgan de la **pantalla**, no de
      `cntBloqueoSes` ni de `cmpDlgCancelarSes`.
- [ ] 🧨 **Cero `Navigate(` en `scrSesion.OnVisible`** (`L-65`). Prueba: App Checker sin el
      aviso *"would automatically always navigate away"*, y la rama 1 del bloque 4 dice
      `Select(btnEntrarTulasSes)`. Con la sesión **ya aprobada**, abrir la app entra sola a
      `scrTulas`: si se queda en la tarjeta, el motor no disparó.
- [ ] `App.StartScreen = scrSesion` (`01` §3.4).

**Estado inicial (A1)**

- [ ] Con una sesión libre, la tarjeta mide **430 × 379** y se ve centrada bajo el header.
- [ ] `ddMesaSes` lista **solo** mesas con `Enable = true` y **sin sesión de hoy**. Prueba:
      abrí una sesión con otro usuario en una mesa y verificá que **desaparece** del
      desplegable al recargar la pantalla.
- [ ] El desplegable muestra el **`Name`** de la mesa (no un GUID ni "Record"). Si muestra
      otra cosa, el campo **`Value`** del panel de propiedades no está en `Name`.
- [ ] Con `colMesasSes` vacía, `lblNotaMesaSes` dice *"No hay mesas disponibles…"* en ámbar
      y `ddMesaSes` queda **deshabilitado**.
- [ ] `btnSolicitarSes` se **deshabilita** si no hay mesa seleccionada.

**La solicitud**

- [ ] Al tocar *Solicitar mesa* se crea **una** fila en `CH-Tbl-RecountingSessions` con
      `Cashier` = tu usuario y `CountingDesk` = la mesa elegida, y **`Confirmed` vacío o
      `false`**. Si viene en `true` sin plugin, **pará**: alguien resucitó **S-1**.
- [ ] Se crea **una** fila en `CH-Tbl-ApprovalRequests` con
      `RequestType = "Crear sesion de recuento"` (string exacto, sin tildes),
      **`ApprovalStatus` VACÍO** y `RequestResources` = `{"RecountingSessionGUID":"<guid>"}`.
- [ ] `RequestDetails` abre en un visor HTML como la plantilla de `01` §3.1 y tiene **UN
      solo** `</body></html>`.
- [ ] Prueba de compensación: deshabilitá temporalmente la escritura en
      `CH-Tbl-ApprovalRequests` (o desconectá la red al tocar el botón) ⇒ aparece el
      `Notify` de error **y la fila de `CH-Tbl-RecountingSessions` NO queda**.

**Estado esperando (A2)**

- [ ] Tras solicitar, la tarjeta pasa a **430 × 413**, el Spinner gira y el pie dice
      *"No cierre la aplicación…"*.
- [ ] Los tres pares `kv` muestran mesa, fecha (`dd/mm/yyyy HH:mm`, **24 h**) y cajero.
- [ ] **Cerrá y reabrí la app**: vuelve sola al estado A2 con la misma solicitud
      (recuperación de §1 bloque 3).
- [ ] *Cancelar solicitud* abre `cmpDlgCancelarSes`; **el diálogo no borra nada hasta
      confirmar**. Al confirmar, desaparecen la fila de `CH-Tbl-ApprovalRequests` **y** la de
      `CH-Tbl-RecountingSessions`, y la mesa **vuelve a aparecer en el desplegable en el
      acto**, sin recargar la pantalla.

**Polling y aprobación**

- [ ] **Hay UN solo Temporizador en la pantalla.** `Duration 15000`, `Repeat true`,
      `AutoStart false`, `OnTimerStart` **vacío**.
- [ ] Aprobá la solicitud desde el Supervisor (o a mano en Dataverse: `ApprovalStatus` =
      `Aprobado`) ⇒ en **≤ 15 s** la app navega a `scrTulas` con `gvarSesion` y `gvarMesa`
      cargados y `gvarSolicitud`, `gvarTula`, `gvarSesionDetalle`, `gvarDeclaredBag` en
      blanco (verificable en **Ver → Variables**).
- [ ] Entrá de nuevo a la app con la sesión ya confirmada ⇒ **`scrSesion` no se ve**: navega
      sola a `scrTulas`.

**Estado rechazado (A3)**

- [ ] Rechazá la solicitud con un `StatusReason` cargado ⇒ en **≤ 15 s** la tarjeta pasa a
      **430 × 444**, con banner rojo, `Icon.Blocked`, mesa + fecha de resolución + quién
      resolvió, y **el motivo textual del supervisor** en la caja.
- [ ] Rechazá **sin** `StatusReason` ⇒ la caja dice *"El supervisor no indicó un motivo…"*
      en `ThemeInk3`. **Nunca** queda vacía.
- [ ] 🧨 **El rechazo NO libera**: no aparece el desplegable, no hay forma de entrar a
      `scrTulas`, y la única salida es *Elegir otra mesa*. Ese es el fix de **S-4**.
- [ ] *Elegir otra mesa* devuelve la tarjeta al estado A1, borra la fila de
      `CH-Tbl-RecountingSessions` y **deja intacta** la solicitud rechazada (sigue en
      `CH-Tbl-ApprovalRequests` con `ApprovalStatus = Rechazado` — es el gate del paso 1
      de §9.2). El toast dice *"Mesa liberada…"*, **no** *"Solicitud cancelada"*.
- [ ] **Cerrá y reabrí la app tras el rechazo**: vuelve al estado A3, **no** al inicial
      (es lo que evita la sesión no confirmada bloqueando la mesa — `D02-3`).

**Guarda de identidad**

- [ ] Forzá `Set(gvarIdentidadOk, false)` desde la barra de fórmulas ⇒ `cntBloqueoSes` tapa
      **toda** la pantalla, con el icono rojo y el texto exacto de `01` §3.2, **sin ningún
      botón de salida**.
- [ ] Con el bloqueo activo, el TAB **no alcanza** el desplegable ni los botones de la
      tarjeta, y el clic sobre el fondo **no hace nada**.
- [ ] Con `gvarIdentidadOk = false`, el `OnVisible` **no dispara ninguna consulta**
      (verificable en **Monitor**: cero llamadas a `CH-Tbl-RecountingSessions` y
      `CH-Tbl-CountingDesks`).

**Higiene**

- [ ] **App Checker: cero errores y cero advertencias de delegación** en esta pantalla. Si
      alguna consulta de §2 sale con el triángulo azul, **avisá** — no la "arregles"
      quitándole una condición.
- [ ] En **Monitor**, entrar a la pantalla dispara exactamente **4** consultas (Q1–Q4), y
      cada tick del timer exactamente **1** (Q9).
- [ ] **No hay NINGÚN `Refresh` en toda la pantalla** (regla **D-5**): `scrSesion` no lee
      `CH-Tbl-Bags`. Prueba: buscá `Refresh(` en el código de `scrSesion` ⇒ **cero
      resultados**.
- [ ] `btnAbandonarSes` y `btnEntrarTulasSes` son **motores de 1 × 1 transparentes**
      (`Visible true`, `Y 767`, `TabIndex -1`, fondos `Color.Transparent`), **no** controles
      con `Visible false` (`L-53`). Prueba: *Cancelar solicitud* → *Confirmar* **da de baja
      las dos filas**; si no pasa nada, el `Select()` murió sobre un control oculto.
- [ ] No hay ningún `ApprovalStatus:` ni `Confirmed:` del lado izquierdo de un `Patch` en
      toda la pantalla. Prueba: buscá `Confirmed` en el código de `scrSesion` — solo puede
      aparecer **leyéndose** en el `OnVisible`.
- [ ] Los 6 controles con `AccessibleLabel` de §12.2-11 lo tienen cargado.
