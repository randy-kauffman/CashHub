# Guía 01 — La app: creación, datos, fórmulas, componentes y convenciones (v2 — layout manual)

Esta guía se hace UNA vez, antes de cualquier pantalla. Las guías 02–05 (`scrClientes`,
`scrCliente`, `scrSolicitudes`, `scrSolicitud`) asumen todo lo de acá. Fuente de verdad del
diseño: `docs/clientadmin-rebuild/mockup-clientadmin.html` (10 vistas CL1/CL2, CI1/CI2/CI3,
SO1–SO5, RSP). Fuente de verdad del esquema: Entity.xml de Core + la app v1 desempaquetada
(`solutions/unpacked/apps/CanvasApps/src/sanic_chappclientadministration_fd97f/`).

> **Paradigma de layout.** Igual que Digitization/Reception/Recounting v2: cero auto-layout
> (`FillPortions`, vertical/horizontal container). Todo con **Contenedores manuales**
> (`groupContainer.manualLayoutContainer`) y **posiciones/tamaños por fórmula relativa** a
> `Parent.Width`/`Parent.Height`/`App.Width` + constantes `fx*`. Escalar-para-ajustar
> **DESACTIVADO**, orientación horizontal, responsive a cualquier ancho (ver vista RSP del
> mockup). 100% controles clásicos nativos — cero Power CAT, cero PCF, cero Forms conectados
> a tablas maestras.

> **Seguridad de escritura.** La app escribe SOLO en `CH-Tbl-ClientDataChangeRequests` y
> `CH-Tbl-ClientDataChangeRequestLines` (vía Patch directo del creador para Draft→Submitted→
> Canceled) y a través de la Custom API `sanic_CHResolveChangeRequest` para Aprobar/Rechazar
> (nunca Patch directo del estado a Approved). Las 9 tablas de dominio restantes son
> **solo lectura, siempre**.

---

## 0. Contrato de formato de las guías de pantalla (02–05)

Toda guía de pantalla (`02-pantalla-clientes.md`, `03-pantalla-cliente.md`,
`04-pantalla-solicitudes.md`, `05-pantalla-solicitud.md`) cumple estas reglas. Una violación
es un bug de la guía, no licencia creativa.

1. **Árbol completo**: el árbol del inicio de cada guía lista TODOS los controles de esa
   pantalla, con su nombre final (ya sufijado). Ningún control aparece en el cuerpo sin haber
   aparecido antes en el árbol, y viceversa.
2. **Cobertura total (árbol ↔ cuerpo 1:1)**: todo control del árbol tiene su descripción más
   abajo — tabla propia, fila en tabla compartida, o puntero explícito a una receta de esta
   guía 01 (p. ej. "receta 7.5") cuando la receta ya fija TODAS sus propiedades.
3. **Tipo explícito**, con el catálogo CERRADO de esta tabla (nombres del menú Insertar de
   Studio). Nada de "Label"/"Dropdown" sueltos ni tipos fuera de esta lista:

   | Tipo en la guía | En Studio (Insertar) |
   |---|---|
   | Contenedor (manual) | Diseño → **Contenedor** (`groupContainer.manualLayoutContainer`) |
   | Etiqueta clásica | Clásico → **Etiqueta** (`Label`) |
   | Botón clásico | Clásico → **Botón** (`Button`) |
   | Entrada de texto clásica | Clásico → **Entrada de texto** (`Text input`) — `Mode: TextMode.SingleLine` o `TextMode.MultiLine` según la guía (cubre también los `<textarea>` de Motivo/Comentario del mockup) |
   | Menú desplegable clásico | Clásico → **Menú desplegable** (`Drop down`) |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** (`Blank vertical gallery`) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) |
   | Icono clásico | Iconos → el glifo (`Icon.*`) |
   | Control de giro moderno (Spinner) | Moderno → **Control de giro** (`Spinner`) — ÚNICA excepción moderna de esta app, solo para el estado "aplicando…" del diálogo de decisión (llamada síncrona a la Custom API) |
   | Componente | Insertar → **Componentes** → `cmpAppHeader` / `cmpConfirmDialog` (biblioteca `CH-CL-SharedComponents`, ver §7.2 y §7.6) |

   > Cerrado. Cero PCF, cero Form/DataCards, cero contenedores de auto-ajuste, cero terceros.
   > El `Contenedor` es SIEMPRE manual (hijos posicionados con X/Y).
4. **Posición y tamaño por fórmula relativa**: `X`, `Y`, `Width`, `Height` de cada control se
   expresan con `Parent.Width`/`Parent.Height`/`App.Width` y las constantes `fx*` de §3 —
   nunca un número mágico suelto que se pueda desincronizar. Dos controles que comparten un
   borde: uno referencia al otro (`otroCtrl.X + otroCtrl.Width + fxGap`).
5. **Propiedades**: solo las que cambian del default (§6 fija los defaults tipográficos y
   los alias `ESTILO-*`).
6. **Nombres únicos a nivel APP** (no por pantalla): en canvas apps el nombre de control es
   único a nivel **aplicación**; si dos pantallas repiten un nombre, Studio renombra en
   silencio y las fórmulas de la guía dejan de corresponder. Sufijo de pantalla:

   | Pantalla | Sufijo |
   |---|---|
   | `scrClientes` | `Cli` |
   | `scrCliente` | `Fic` (ficha) |
   | `scrSolicitudes` | `Sol` |
   | `scrSolicitud` | `Det` (detalle) |
   | `scrSplash` | `Spl` |

   Ejemplos: `galClientesCli`, `lblCifBcoFic`, `pilEstadoSol`, `btnAprobarDet`. Los controles
   internos de un componente llevan el sufijo del **componente**, no de la pantalla que lo
   instancia (`lblTituloHdr` dentro de `cmpAppHeader`, no `lblTituloHdrCli`).

---

## 1. Crear la app

1. make.powerapps.com → entorno DEV → **Apps → Nueva app → Comenzar con una página en
   blanco → Tamaño: Tableta**.
2. **Configuración → General**:
   - Nombre: `CH-App-ClientAdministration v2` (reemplaza a la app v1 al publicar; hasta
     entonces conviven).
   - Ícono: subí `docs/marca/icono-client-administration-512.png` · Color de fondo: `#C8102E`.
3. **Configuración → Pantalla**: Orientación **Horizontal** · Tamaño **1366 × 768** (tamaño
   de diseño de referencia — no es un límite, ver nota) · **Escalar para ajustar:
   DESACTIVADO** · **Bloquear relación de aspecto: DESACTIVADO** · Bloquear orientación:
   Activado.
   > Con Escalar-para-ajustar OFF, `App.Width`/`App.Height` y `Parent.Width`/`Parent.Height`
   > reflejan el tamaño real de la ventana — las fórmulas relativas de §3/§7 responden de
   > verdad a cualquier ancho horizontal (ver vista RSP del mockup: ~1080px y ~840px sin
   > recortes ni superposiciones).
4. **Insertar → Bibliotecas de componentes → Importar componentes** → agregá
   `CH-CL-SharedComponents` como dependencia (necesario para instanciar `cmpAppHeader` y
   `cmpConfirmDialog`, ver §7.2/§7.6). **Antes de este paso, esa biblioteca necesita las
   extensiones descritas en §7.2/§7.6** — hoy `cmpAppHeader`/`cmpTheme` son placeholders
   vacíos (ver "Divergencias declaradas" al final).
5. Agregala a la solución **CashHubApps** (portal de soluciones → Agregar existente →
   Aplicación → Canvas) para que el export/deploy siga el pipeline actual.

---

## 2. Orígenes de datos

Agregá EXACTAMENTE estas tablas (Datos → Agregar datos). Nombres de display (plural,
tal como aparecen en el panel — verificados contra `LocalizedCollectionName` de cada
Entity.xml):

| # | Tabla (display) | Para qué | Escritura desde la app |
|---|---|---|---|
| 1 | `CH-Tbl-Clients` | Maestro de clientes | NO — solo lectura |
| 2 | `CH-Tbl-ClientBranches` | Sucursales | NO — solo lectura |
| 3 | `CH-Tbl-ClientEmails` | Correos autorizados | NO — solo lectura |
| 4 | `CH-Tbl-ClientAccounts` | Cuentas bancarias | NO — solo lectura |
| 5 | `CH-Tbl-ClientDataChangeRequests` | Cabecera de RFC | SÍ — Patch directo (Draft/Submitted/Canceled) + Custom API (Approved/Applied/Failed/Rejected) |
| 6 | `CH-Tbl-ClientDataChangeRequestLines` | Líneas de cambio del RFC | SÍ — Patch directo del creador mientras el RFC está en Draft |
| 7 | `CH-Tbl-ClientDataChangesAuditTrails` | Bitácora de aplicación | NO — solo lectura (la escribe la Custom API server-side) |
| 8 | `CH-Tbl-ClientDataChangePermissions` | Matriz tabla-objetivo × rol × grupo AAD | NO — solo lectura, se resuelve una vez en `App.OnStart` (§4) |
| 9 | `CH-Tbl-ClientTableLogicalFieldNames` | Catálogo de campos editables por tabla (motor genérico del RFC) | NO — solo lectura |
| 10 | `Users` | `'Full Name'` / `'Primary Email'` del usuario actual y de `createdby`/`solvedby` | NO — solo lectura |

Conectores adicionales:

| Conector | Para qué |
|---|---|
| `Office365Groups` | `ListGroupMembers(<guid>)` — resolución de membresía AAD en `App.OnStart` (§4) |
| **Environment** | Custom API directa `sanic_CHResolveChangeRequest` (§7.6) — agregala desde Datos → Agregar datos → busca "Environment" |

> ⚠️ NO agregues `CH-Tbl-ClientDepositHistory` ni ninguna tabla fuera de esta lista — está
> fuera del ciclo RFC (la llena el proceso de tracking, no esta app). Tampoco arrastres los
> data sources de muestra de la v1 (`ComboBoxSample`, `DropDownSample`, `CustomGallerySample`,
> `Bulk Delete Failures`) — deuda identificada en `00-analisis-y-plan.md` §2.3/U7.

---

## 3. App.Formulas

Seleccioná **App** → propiedad **Formulas** → pegá TODO. Paleta y tipografía gemelas de
Digitization/Reception/Recounting v2 (tokens idénticos al `:root` del mockup, mismos RGBA que
`sanic_chappdigitization_278aa/Src/App.fx.yaml`):

```powerfx
// ===================== PALETA (RGBA exactos del mockup :root) =====================
ThemeAccent      = RGBA(200, 16, 46, 1);      // #C8102E
ThemeAccentDark  = RGBA(164, 13, 38, 1);      // #A40D26 — hover del boton primario
ThemeAccentSoft  = RGBA(251, 235, 237, 1);    // #FBEBED
ThemeBg          = RGBA(246, 245, 244, 1);    // #F6F5F4
ThemeSurface     = RGBA(255, 255, 255, 1);    // #FFFFFF
ThemeSurface2    = RGBA(250, 248, 247, 1);    // #FAF8F7
ThemeInk         = RGBA(32, 28, 29, 1);       // #201C1D
ThemeInk2        = RGBA(102, 89, 92, 1);      // #66595C
ThemeInk3        = RGBA(148, 138, 140, 1);    // #948A8C
ThemeLine        = RGBA(226, 220, 221, 1);    // #E2DCDD
ThemeLineSoft    = RGBA(237, 232, 233, 1);    // #EDE8E9
ThemeOk          = RGBA(14, 122, 60, 1);      ThemeOkSoft     = RGBA(226, 242, 232, 1);
ThemeInfo        = RGBA(35, 85, 125, 1);      ThemeInfoSoft   = RGBA(228, 238, 246, 1);
ThemeWarn        = RGBA(154, 91, 0, 1);       ThemeWarnSoft   = RGBA(251, 240, 220, 1);
ThemeDanger      = RGBA(179, 38, 30, 1);      ThemeDangerSoft = RGBA(252, 234, 232, 1);
ThemeDangerLine  = RGBA(231, 185, 181, 1);
ThemeOverlay     = RGBA(40, 30, 32, 0.45);    // velo de dialogos/panel lateral
// ===================== CONSTANTES DE LAYOUT (el sistema de posicionamiento) =====================
fxHeaderH   = 60;      // alto del encabezado (cmpAppHeader)
fxGutter    = 18;      // margen lateral / respiro entre zonas
fxGap       = 14;      // separacion entre cards
fxCardR     = 13;      // radio de card
fxRowH      = 44;      // alto de fila de galeria (grillas anchas: Clientes, Solicitudes)
fxRowHChica = 38;      // alto de fila de galeria en mini-grillas embebidas (lineas de RFC)
fxRowHHist  = 78;      // alto de fila de galeria del timeline/historial (scrSolicitud, 3 lineas de texto)
fxKpiH      = 66;      // alto de KPI card
fxSideW     = 290;     // ancho del sidebar de ficha de cliente (CI1)
fxPanelW    = Min(430, App.Width * 0.46);   // ancho del panel lateral (CL2/CI2) — nunca mas de 430px ni mas del 46% del ancho
fxDlgW      = 440;     // ancho maximo del dialogo modal de confirmacion (CI3/SO2)
// ===================== FORMATOS =====================
FmtMoney = "#,##0.00";              // el '0' fuerza el entero
FmtFecha = "dd/mm/yyyy HH:mm";      // HH = 24 h. hh seria 12h SIN AM/PM (gotcha 8.1)
FmtHora  = "HH:mm";
SimboloNIO = "C$";  SimboloUSD = "US$";  SimboloEUR = "€";
// ===================== MAPA AREA POR TABLA (derivado — CH-Tbl-ClientDataChangePermission no tiene columna de area) =====================
// La clave "Tabla" es el mismo texto libre que usa sanic_targettable (cabecera de RFC) y
// sanic_name (permiso) para identificar la tabla objetivo. Literales = LOGICAL NAME de la
// entidad, verificados contra data/seed/20-clientdatachangepermissions.json,
// data/seed/10-clienttablelogicalfieldnames.json y src/backend/CashHub.Plugins/Schema.cs
// (esto resuelve la D6 de "Divergencias declaradas" — ya NO es "a verificar en Studio").
fxAreaPorTabla = Table(
    {Tabla: "sanic_chtblclient",        Area: "Banca Empresas"},
    {Tabla: "sanic_chtblclientbranch",  Area: "Bóveda"},
    {Tabla: "sanic_chtblclientemail",   Area: "Banca Empresas"},
    {Tabla: "sanic_chtblclientaccount", Area: "Banca Empresas"}
);
// ===================== IDENTIDAD (sin conectores) =====================
UsuarioNombre = User().FullName;
UsuarioIniciales =
    Left(User().FullName, 1) &
    If(
        CountRows(Split(User().FullName, " ")) > 1,
        Left(Last(Split(User().FullName, " ")).Value, 1),
        ""
    );
// Si IntelliSense nombra la columna de Split() distinto de 'Value', EL SUYO MANDA.
```

Tipografía: **`Font.'Open Sans'`** explícita en cada control; `Size` en puntos.

> **Sobre los mapas de estado (RFC status, RFC type, RecordStatus)**: NO son named formulas
> porque dependen de un valor por-fila (`ThisItem.ChangeStatus`, `gvarSolicitudSel.Type`…) y
> las named formulas de `App.Formulas` no tienen acceso a `ThisItem`/parámetros. El patrón
> `Switch(...)` exacto para cada mapa vive en §7.3 (pill/badge) — se pega inline donde haga
> falta, con el valor de contexto de cada pantalla.

---

## 4. App.OnStart + App.OnError

**App.OnStart** resuelve identidad + permisos UNA sola vez y procesa el deep-link. Mejora
sobre v1 (que hacía esta misma resolución DOS veces — en `App.OnStart` Y en el `Timer` de
`Splash` — y llamaba `Office365Groups.ListGroupMembers` **una vez por cada fila de permisos**,
sin deduplicar):

```powerfx
// 1) Identidad
Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email));

// 2) Permisos — deduplicando por AADGroupID antes de llamar a Graph.
//    Con 2 areas x 2 roles x hasta 4 tablas, v1 hacia hasta 16 llamadas secuenciales a
//    Office365Groups.ListGroupMembers (una por fila de CH-Tbl-ClientDataChangePermissions).
//    Aca se llama UNA VEZ POR GRUPO AAD DISTINTO (tipicamente 4: Contributor/Approver x
//    Banca Empresas/Boveda) — el resto de las filas comparten grupo.
//    Nota: Concurrent() no aplica aca — necesita formulas SINTACTICAMENTE explicitas, no un
//    ForAll de longitud dinamica; la optimizacion real es la deduplicacion, no el paralelismo.
ClearCollect(colTodosLosPermisos, 'CH-Tbl-ClientDataChangePermissions');
ClearCollect(colGruposUnicosCDCP, Distinct(colTodosLosPermisos, AADGroupID));
Clear(colMisGruposCDCP);
ForAll(
    colGruposUnicosCDCP As Grupo,
    If(
        !IsBlank(LookUp(Office365Groups.ListGroupMembers(Text(Grupo.Value)).value, userPrincipalName = User().Email)),
        Collect(colMisGruposCDCP, {AADGroupID: Grupo.Value})
    )
);
ClearCollect(
    colMisPermisos,
    Filter(colTodosLosPermisos, AADGroupID in colMisGruposCDCP.AADGroupID)
);

// 3) Resumen de rol para el chip del header (cmpAppHeader.RolTexto) — aproximacion honesta:
//    si el usuario tuviera permisos en mas de un area (no es el caso de negocio esperado),
//    se muestra el area de la PRIMERA fila de colMisPermisos.
Set(
    gvarRolTexto,
    If(
        CountRows(colMisPermisos) = 0,
        "Sin acceso",
        (
            If(
                CountRows(Filter(colMisPermisos, Type = 'Type (CH-Tbl-ClientDataChangePermissions)'.Approver)) > 0,
                "Approver",
                "Contributor"
            )
        ) & " · " & LookUp(fxAreaPorTabla, Tabla = First(colMisPermisos).Name).Area
    )
);

// 4) Deep-link — UNA sola vez (no se repite en scrSplash.Timer, ver guia 05... esta app no
//    tiene guia numerada para Splash, va en esta misma guia §5).
If(
    !IsBlank(Param("RFC")),
    Set(
        gvarSolicitudSel,
        LookUp('CH-Tbl-ClientDataChangeRequests', 'CH-Tbl-ClientDataChangeRequest' = GUID(Param("RFC")))
    );
    Set(gvarClienteSel, gvarSolicitudSel.RelatedClient)
)
```

**App.OnError**:

```powerfx
Trace("[ClientAdmin] " & FirstError.Message & " | " & FirstError.Kind, TraceSeverity.Error);
Notify("Ocurrió un error inesperado: " & FirstError.Message, NotificationType.Error)
```

> 🧨 Patrón anti-genérico: el `Notify` SIEMPRE incluye `FirstError.Message` real. Está
> prohibido un mensaje fijo tipo "Hubo problemas al guardar…" (bug U4 de la v1, ver
> `00-analisis-y-plan.md`).

Variables globales que las guías 02–05 asumen ya seteadas por esta sección:

| Variable | Contenido | Quién la setea |
|---|---|---|
| `gvarUser` | Registro de `Users` del usuario actual | `App.OnStart` |
| `colMisPermisos` | Filas de `CH-Tbl-ClientDataChangePermissions` donde el usuario es miembro del grupo AAD | `App.OnStart` |
| `gvarRolTexto` | Texto del chip de rol (`"Contributor · Banca Empresas"`) | `App.OnStart` |
| `gvarSolicitudSel` | RFC seleccionado (deep-link o navegación normal) | `App.OnStart` (deep-link) o `scrSolicitudes`/`scrCliente` (navegación) |
| `gvarClienteSel` | Cliente seleccionado | `App.OnStart` (deep-link) o `scrClientes` (navegación) |

---

## 5. scrSplash

Pantalla sin UI visible en el mockup (`"scrSplash (deep-link, sin UI)"`) — se agrega un
indicador mínimo de carga por buena práctica (no es una divergencia del mockup: el mockup no
diseñó esta pantalla).

**Árbol de controles**
```
scrSplash
├── lblMarcaSpl        Etiqueta clásica       "C$" — eco de marca mientras carga
├── lblCargandoSpl     Etiqueta clásica       "Cargando…"
└── tmrSplash          Control de giro moderno (Spinner) — NO: es Timer, ver nota abajo
```

> Nota de tipo: el temporizador de navegación es un **Timer** clásico (`Insertar → Clásico →
> Timer`), no está en el catálogo cerrado de §0 porque esta app lo usa una única vez, solo en
> esta pantalla. Se agrega como excepción puntual documentada acá: `Timer` clásico
> (`Insertar → Clásico → Control de temporizador`).

**1. Pantalla**

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | vacío — toda la lógica de deep-link ya corrió en `App.OnStart` (§4); esta pantalla NO repite el `LookUp` (a diferencia de v1, que lo hacía en `App.OnStart` Y en el `Timer`) |

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `lblMarcaSpl` | Etiqueta clásica | `App.Width/2 - 20` | `App.Height/2 - 60` | `40` | `40` | Text `"C$"` · Size `18` · Bold · Color `ThemeSurface` · Fill `ThemeAccent` · Align `Center` — **sin `Radius`**: la Etiqueta clásica no soporta esquinas redondeadas; el fuente real (`scrSplash.fx.yaml`) confirma que no se seteó ningún `Radius`, el tile queda cuadrado |
| `lblCargandoSpl` | Etiqueta clásica | `App.Width/2 - 80` | `App.Height/2 - 8` | `160` | `20` | Text `"Cargando…"` · Size `9.5` · Color `ThemeInk3` · Align `Center` |
| `tmrSplash` | Control de temporizador clásico | `App.Width/2 - 20` | `App.Height/2 + 20` | `40` | `4` | `AutoStart: true` · `Duration: 350` · `Repeat: false` · `Visible: false` (el temporizador es invisible; solo dispara la navegación) |

**`tmrSplash.OnTimerEnd`**:
```powerfx
If(
    !IsBlank(gvarSolicitudSel),
    Navigate(scrSolicitud, ScreenTransition.Fade),
    Navigate(scrClientes, ScreenTransition.Fade)
)
```

---

## 6. Defaults tipográficos y ESTILO-*

Etiquetas: `Font.'Open Sans'`, Size `9`, Color `ThemeInk`, Padding `0`, `Wrap: false`,
`VerticalAlign.Middle`.

| Alias | Se logra con |
|---|---|
| `ESTILO-TITULO` | Size `15`, Bold |
| `ESTILO-SUBTITULO` | Size `11`, Color `ThemeInk2` |
| `ESTILO-SECCION` | Size `7.88`, Semibold, Color `ThemeInk3`, texto en MAYÚSCULAS |
| `ESTILO-KPI-LAB` | Size `7.88`, Bold, Color `ThemeInk3`, MAYÚSCULAS |
| `ESTILO-KPI-VAL` | Size `16.5`, Bold |
| `ESTILO-KPI-SUB` | Size `8.25`, Color `ThemeInk2` |
| `ESTILO-HEADER-COL` | Size `7.5`, Bold, Color `ThemeInk2`, MAYÚSCULAS |
| `ESTILO-CELDA` | Size `9` |
| `ESTILO-CELDA-NUM` | Size `9`, Align `Right`, `Format:` tabular (dígitos monoespaciados no existen en clásico — usar `Align.Right` alcanza) |
| `ESTILO-META` | Size `8.63`, Color `ThemeInk2` |
| `ESTILO-DATO-K` | Size `9`, Color `ThemeInk3` |
| `ESTILO-DATO-V` | Size `9`, Semibold, Align `Right` |
| `ESTILO-VACIO` | Size `9.38`, Color `ThemeInk3`, Align `Center`, `Wrap: true` |
| `ESTILO-CAMPO-LABEL` | Size `8.25`, Semibold, Color `ThemeInk2` |
| `ESTILO-CAMPO-ERROR` | Size `8.25`, Color `ThemeDanger` |
| `ESTILO-BREADCRUMB` | Size `9`, Color `ThemeAccent` |

Botones (`Font.'Open Sans'`, Size `9`, Semibold, Radius `8`, Height `32`; `DisabledFill:
ThemeLineSoft`, `DisabledColor: ThemeInk3`, `DisabledBorderColor: Transparent`):

| Alias | Fill | Color | Border | Hover |
|---|---|---|---|---|
| `ESTILO-BTN-PRIMARIO` | `ThemeAccent` | `ThemeSurface` | — | Fill `ThemeAccentDark` |
| `ESTILO-BTN-CONTORNO` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent` `1` | Fill `ThemeAccentSoft` |
| `ESTILO-BTN-FANTASMA` | `Transparent` | `ThemeInk2` | `ThemeLine` `1` | Fill `ThemeSurface2` |
| `ESTILO-BTN-PELIGRO` | `ThemeDanger` | `ThemeSurface` | — | Fill `RGBA(153, 30, 24, 1)` |
| `ESTILO-BTN-OK` | `ThemeOk` | `ThemeSurface` | — | Fill `RGBA(11, 97, 48, 1)` |
| `ESTILO-BTN-CHICO` | `ThemeSurface` | `ThemeInk2` | `ThemeLine` `1` | Size `8.25`, Height `26`, Radius `7`, Fill hover `ThemeSurface2` |
| `ESTILO-BTN-CHICO-ACC` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent` `1` | igual que CHICO pero acento (equivalente a `.btnSmAcc` del mock) |
| `ESTILO-BTN-CHICO-DAN` | `ThemeSurface` | `ThemeDanger` | `ThemeDangerLine` `1` | igual que CHICO pero peligro (`.btnSmDan`) |

---

## 7. Recetas reutilizables numeradas

Cada receta fija TODAS las propiedades relevantes — las guías 02–05 solo apuntan a ella
(`"card: receta 7.1"`) en vez de repetir la tabla completa.

### 7.1 El sistema de layout — contenedores manuales + fórmulas relativas

Esqueleto de las 4 pantallas con datos (`scrClientes`, `scrCliente`, `scrSolicitudes`,
`scrSolicitud`):

```
scrX  (Fill: ThemeBg)
├── cmpAppHeaderX      Componente (receta 7.2) · X 0 · Y 0 · Width Parent.Width · Height fxHeaderH
├── cntToolbarX        Contenedor manual · X 0 · Y fxHeaderH · Width Parent.Width · Height <según pantalla>
├── cntBodyX           Contenedor manual · X 0 · Y cntToolbarX.Y + cntToolbarX.Height · Width Parent.Width · Height Parent.Height - Self.Y
│   └── (KPIs, cards, grillas — X/Y/Width/Height relativos a cntBodyX)
├── cmpPanelX          Componente/receta de panel lateral (7.7) · Visible condicional · SOLO en scrClientes/scrCliente
└── cmpConfirmDialogX  Componente (receta 7.6) · X 0 · Y 0 · Width Parent.Width · Height Parent.Height (overlay a pantalla completa)
```

**Reglas de oro:**
- Un control dentro de un Contenedor usa `Parent.Width`/`Parent.Height` = los del contenedor.
  Anidar contenedores es cómo se acota el marco de referencia.
- Fracciones: `Width: =Parent.Width * 0.62`. Sumas: `X: =otroCtrl.X + otroCtrl.Width + fxGap`.
- Constantes `fx*` para todo número que se repita; nunca dos literales que se contradigan.
- `scrCliente` (CI1) es la única con **sidebar fijo** (`fxSideW`) además del cuerpo — ver
  receta específica en la guía 03.

### 7.2 `cmpAppHeader` — header + navegación segmentada (extensión del componente compartido)

> **Punto de partida real (a verificar en Studio antes de tocarlo)**: hoy
> `sanic_chclsharedcomponents_85fca/Src/Components/cmpAppHeader.fx.yaml` es un placeholder de
> 8 líneas — un rectángulo con borde `App.Theme.Colors.Darker40` y `Height 64`, sin ninguna
> Custom Property, sin theming de CashHub, y **ninguna app v2 lo consume todavía**
> (Digitization v2 construyó su propio `cmpHeader` LOCAL en vez de este). `CH-App-
> ClientAdministration v2` es la PRIMERA app que lo usa de verdad — por eso el paso 0 de esta
> receta es **extenderlo en la biblioteca compartida**, no solo instanciarlo.

**Extensión a aplicar en `CH-CL-SharedComponents` → `cmpAppHeader` (una vez, antes de
`scrClientes`):**

**Custom Properties**
| Nombre | Tipo | Dir | Default | Uso |
|---|---|---|---|---|
| `Titulo` | Texto | Entrada | `"Administración de Clientes"` | título grande |
| `Subtitulo` | Texto | Entrada | `"Maestro de clientes"` | bajo el título |
| `Activo` | Texto | Entrada | `"Clientes"` | pestaña resaltada: `"Clientes"` / `"Solicitudes"` |
| `RolTexto` | Texto | Entrada | `""` | chip de rol (`"Contributor · Banca Empresas"`) |
| `OnClientes` | Comportamiento | Entrada | — | la pantalla cablea `Navigate(scrClientes)` |
| `OnSolicitudes` | Comportamiento | Entrada | — | la pantalla cablea `Navigate(scrSolicitudes)` |

**Árbol de controles**
```
cmpAppHeader
├── shpFondoHdr        Rectángulo clásico       banda blanca
├── shpLineaHdr        Rectángulo clásico       línea inferior 1 px
├── btnMarcaHdr        Botón clásico            tile "C$"
├── lblTituloHdr       Etiqueta clásica         título
├── lblSubHdr          Etiqueta clásica         subtítulo
├── cntNavHdr          Contenedor (manual)      pista del segmentado (2 botones)
│   ├── btnNavClientesHdr      Botón clásico
│   └── btnNavSolicitudesHdr   Botón clásico
└── cntUserHdr         Contenedor (manual)      chip de usuario
    ├── btnAvatarHdr       Botón clásico        círculo con iniciales
    ├── lblUsuarioHdr      Etiqueta clásica     nombre
    └── pilRolHdr          Botón clásico        píldora de rol (siempre tono info)
```

**Propiedades** (`Parent` = la instancia; `Height` de la instancia = `fxHeaderH`)
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpFondoHdr` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeSurface` · BorderThickness `0` |
| `shpLineaHdr` | Rectángulo | `0` | `Parent.Height-1` | `Parent.Width` | `1` | Fill `ThemeLine` |
| `btnMarcaHdr` | Botón | `fxGutter` | `(Parent.Height-34)/2` | `34` | `34` | Radius `9` · Fill `ThemeAccent` · Color `ThemeSurface` · Text `"C$"` · Size `11` · Bold · DisplayMode `View` · BorderThickness `0` |
| `lblTituloHdr` | Etiqueta | `btnMarcaHdr.X + btnMarcaHdr.Width + 12` | `10` | `220` | `20` | Text `cmpAppHeader.Titulo` · Size `10.5` · Semibold |
| `lblSubHdr` | Etiqueta | `lblTituloHdr.X` | `lblTituloHdr.Y + 18` | `220` | `16` | Text `cmpAppHeader.Subtitulo` · Size `8.25` · Color `ThemeInk3` |
| `cntNavHdr` | Contenedor | `lblTituloHdr.X + 230` | `(Parent.Height-32)/2` | `200` | `32` | Fill `ThemeSurface2` · BorderColor `ThemeLine` · BorderThickness `1` · Radius `16` |
| `cntUserHdr` | Contenedor | `Parent.Width - fxGutter - Self.Width` | `(Parent.Height-32)/2` | `pilRolHdr.X + pilRolHdr.Width + 4` | `32` | Fill `ThemeSurface2` · Border `ThemeLine` `1` · Radius `16` |

**Los 2 botones de nav** (hijos de `cntNavHdr`; `Parent` = `cntNavHdr`): Height `26`, Y `3`,
Width `(Parent.Width - 6)/2`, Radius `13`, BorderThickness `0`, Size `9`. X encadenado:
`btnNavClientesHdr.X = 3`; `btnNavSolicitudesHdr.X = btnNavClientesHdr.X + btnNavClientesHdr.Width`.
Cada uno con su `<Tab>` = `"Clientes"`/`"Solicitudes"` y su `<OnX>` = `cmpAppHeader.OnClientes`/
`cmpAppHeader.OnSolicitudes`:
| Prop | Fórmula |
|---|---|
| Text | `"<Tab>"` |
| Fill | `If(cmpAppHeader.Activo = "<Tab>", ThemeSurface, Color.Transparent)` |
| Color | `If(cmpAppHeader.Activo = "<Tab>", ThemeAccent, ThemeInk2)` |
| FontWeight | `If(cmpAppHeader.Activo = "<Tab>", FontWeight.Semibold, FontWeight.Normal)` |
| HoverFill | `If(cmpAppHeader.Activo = "<Tab>", ThemeSurface, ThemeSurface2)` |
| OnSelect | `cmpAppHeader.<OnX>` |

**Chip de usuario** (hijos de `cntUserHdr`; `Parent` = `cntUserHdr`):
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `btnAvatarHdr` | Botón | `4` | `3` | `26` | `26` | Radius `13` · Fill `ThemeAccentSoft` · Color `ThemeAccent` · Text `UsuarioIniciales` · Size `7.88` · Bold · DisplayMode `View` · BorderThickness `0` |
| `lblUsuarioHdr` | Etiqueta | `btnAvatarHdr.X + btnAvatarHdr.Width + 8` | `0` | `Len(UsuarioNombre) * 5.6` | `Parent.Height` | Text `UsuarioNombre` · Size `8.63` · Color `ThemeInk2` |
| `pilRolHdr` | Botón | `lblUsuarioHdr.X + lblUsuarioHdr.Width + 8` | `(Parent.Height-18)/2` | `Len(cmpAppHeader.RolTexto) * 5.2 + 16` | `18` | Radius `9` · Fill `ThemeInfoSoft` · Color `ThemeInfo` · Text `cmpAppHeader.RolTexto` · Size `7.5` · Bold · DisplayMode `View` · BorderThickness `0` |

> 🧨 Nav por **behavior properties** (un componente no puede `Navigate` a pantallas de la app
> host). Cada pantalla instancia `cmpAppHeaderX`, fija `Activo`/`Titulo`/`Subtitulo`/`RolTexto`
> según la propia, y cablea `OnClientes: =Navigate(scrClientes, ScreenTransition.None)`,
> `OnSolicitudes: =Navigate(scrSolicitudes, ScreenTransition.None)` (la pantalla propia cablea
> su propio `OnX` en `=false` para no auto-navegar). `AllowAccessToGlobals = true` en el
> componente (lee `Theme*`, `fx*`, `UsuarioNombre`/`UsuarioIniciales` directo).

**Instanciación en cada pantalla** (ejemplo `scrClientes`):
```powerfx
cmpAppHeaderCli As cmpAppHeader:
    Activo: ="Clientes"
    Height: =fxHeaderH
    OnClientes: =false
    OnSolicitudes: =Navigate(scrSolicitudes, ScreenTransition.None)
    RolTexto: =gvarRolTexto
    Subtitulo: ="Maestro de clientes"
    Titulo: ="Administración de Clientes"
    Width: =Parent.Width
```

### 7.3 Pill / badge de estado

Botón clásico usado como badge de solo lectura (fuera de galería: `pil…`; dentro de galería:
mismo patrón pero como control nativo del template, ver 7.4/gotcha 8.5):

| Propiedad | Valor |
|---|---|
| DisplayMode | `DisplayMode.View` |
| Radius (las 4) | `12` |
| Height | `20` |
| Font / Size | `Font.'Open Sans'` / `7.88` |
| FontWeight | `Semibold` |
| BorderThickness | `If(<tono>="neutro", 1, 0)` |
| BorderColor | `ThemeLineSoft` |
| Width | `Len(<texto>) * 5.4 + 18` (o ancho fijo por columna en grillas) |

**Mapa de tono → Fill/Color** (`Switch` sobre un texto `"ok"/"warn"/"danger"/"info"/"accent"/
"neutro"`):
```powerfx
Switch(
    <tono>,
    "ok",     ThemeOkSoft,
    "warn",   ThemeWarnSoft,
    "danger", ThemeDangerSoft,
    "info",   ThemeInfoSoft,
    "accent", ThemeAccentSoft,
    /* neutro */ ThemeLineSoft
)
// Color: mismo Switch reemplazando *Soft por el tono solido (ThemeOk, ThemeWarn, ThemeDanger,
// ThemeInfo, ThemeAccent; "neutro" -> ThemeInk3)
```

**Mapa de texto/tono para el ESTADO del RFC** (columna `Change Status` — nombre verificado en
`sanic_CHTblClientDataChangeRequest/Entity.xml`; **si IntelliSense nombra el enum distinto,
el suyo manda**):
```powerfx
Switch(
    <valorChangeStatus>,
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Draft,     {Texto: "Borrador",  Tono: "neutro"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Submitted, {Texto: "Enviada",   Tono: "warn"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Approved,  {Texto: "Aprobada",  Tono: "info"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Rejected,  {Texto: "Rechazada", Tono: "danger"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Applied,   {Texto: "Aplicada",  Tono: "ok"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Failed,    {Texto: "Fallida",   Tono: "danger"},
    'Change Status (CH-Tbl-ClientDataChangeRequests)'.Canceled,  {Texto: "Cancelada", Tono: "neutro"},
    {Texto: "—", Tono: "neutro"}
)
```

**Mapa de texto/tono para el TIPO del RFC** (columna `Type`, valores reales
`159460000..159460003`):
```powerfx
Switch(
    <valorType>,
    'Type (CH-Tbl-ClientDataChangeRequests)'.Update,     {Texto: "Edición",       Tono: "info"},
    'Type (CH-Tbl-ClientDataChangeRequests)'.Deactivate, {Texto: "Desactivación", Tono: "danger"},
    'Type (CH-Tbl-ClientDataChangeRequests)'.Create,     {Texto: "Alta",          Tono: "accent"},
    'Type (CH-Tbl-ClientDataChangeRequests)'.Activate,   {Texto: "Activación",    Tono: "ok"},
    {Texto: "—", Tono: "neutro"}
)
```
> El mockup solo muestra Edición/Alta/Desactivación en sus grillas; "Activación" (tono `ok`)
> es una extensión consistente no visible en ninguna vista del mockup — declarado en
> "Divergencias" al final.

**Mapa de texto/tono para `RecordStatus`** (Cliente/Sucursal/Correo/Cuenta — option set
global `CH-Ch-RecordStatus`, mismo para las 4 tablas maestras):
```powerfx
Switch(
    <valorRecordStatus>,
    'RecordStatus (CH-Tbl-Clients)'.Active,               {Texto: "Activo",              Tono: "ok"},
    'RecordStatus (CH-Tbl-Clients)'.Inactive,             {Texto: "Inactivo",            Tono: "neutro"},
    'RecordStatus (CH-Tbl-Clients)'.PendingApproval,      {Texto: "Pend. aprobación",    Tono: "warn"},
    'RecordStatus (CH-Tbl-Clients)'.PendingDeactivation,  {Texto: "Pend. desactivación", Tono: "warn"},
    {Texto: "—", Tono: "neutro"}
)
```
> Nombre de enum a ajustar por tabla (`RecordStatus (CH-Tbl-ClientBranches)` en sucursales,
> etc.) — mismos 4 valores porque comparten el option set global `sanic_chchrecordstatus`.
> `CH-Tbl-ClientAccount` usa el campo `CH-Ch-RecordStatus` (no `RecordStatus`) — ver
> gotcha 8.7.

### 7.4 Grilla = encabezado + galería (delegable)

Fila de labels `ESTILO-HEADER-COL` (X por fracción del ancho de la card) + `shp…Linea`
(Rectángulo H `1`, Fill `ThemeLine`) + **Galería vertical en blanco** debajo (`X`/`Width` =
los de la card menos el padding; `Height` = `Parent.Height - <lo de arriba>`; `TemplateSize
fxRowH` en grillas principales, `fxRowHChica` en mini-grillas embebidas de detalle).

**Regla de coordenadas del template**: X de cada control DENTRO del template = X del
encabezado − X de la galería (los templates arrancan en 0).

Filtros SIEMPRE delegables: `Filter(t, Lookup.'Primary Key' = gvar.<guid>)` (igualdad de GUID
→ `_x_value eq <guid>`), `Filter(t, 'Change Status' = <opción>)` (picklist delegable). Nunca
`Upper()`, `AddColumns` antes de filtrar, dot-walk multi-nivel en el predicado, ni comparar
records con `=` (da "Record, Record" — comparar por la clave primaria).

**Overlay clicable de fila** (`btn…Fila`, Botón clásico, PRIMERO en el z-order visual pero
AL FONDO en orden de inserción — insertalo primero, todo lo demás va encima): `X 0 · Y 0 ·
Width = Width de la galería · Height = TemplateSize · Fill Transparent · HoverFill
ThemeSurface2 · PressedFill ThemeAccentSoft · BorderThickness 0 · Text "" · OnSelect
Select(Parent)`. Los labels/pills del template heredan el clic con `OnSelect: =Select(Parent)`
en cada uno (la acción real vive en el `OnSelect` de la GALERÍA, no del botón — ver
gotcha 8.4).

### 7.5 Fila seleccionada

`shp…Sel` (Rectángulo, primero después del overlay en el z-order): Fill
`If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)`. `shp…SelBarra` (Rectángulo
X `0` Y `0` W `3` H `TemplateSize` Fill `ThemeAccent` Visible `ThisItem.IsSelected`).
🧨 Las galerías clásicas AUTO-SELECCIONAN la 1ª fila al cargar `Items` — es comportamiento
BUSCADO en los paneles "del seleccionado" (scrCliente, scrSolicitud): arrancan mostrando el
primer registro sin pantalla muerta. Los estados vacíos son solo para colecciones SIN filas.

### 7.6 `cmpConfirmDialog` — diálogo modal de confirmación (componente NUEVO, compartido)

**Decisión: SÍ se componentiza, y vive en `CH-CL-SharedComponents`** (no app-local). Se repite
IDÉNTICO en 4+ puntos del mockup con la MISMA forma de contenido (ícono por tono, título,
cuerpo, un campo de texto opcional de una sola pieza — "Motivo"/"Comentario" —, 2 botones):
CI3 (desactivar sucursal), su equivalente para cuentas/correos (mismo patrón, no dibujado
aparte en el mockup pero mismo flujo), SO2→SO3/SO4 (decisión Aprobar/Rechazar del RFC). A
diferencia del panel lateral (7.7), acá el CONTENIDO siempre tiene la misma forma —
justamente el criterio que digitización usó para su propio `cmpDialogo` (app-local). Se sube
a la librería compartida porque, a diferencia de digitización, esta app YA depende de
`CH-CL-SharedComponents` para el header — tiene sentido que el próximo consumidor (Tracker,
Reception) lo reuse sin duplicar.

**Custom Properties**
| Nombre | Tipo | Dir | Default | Uso |
|---|---|---|---|---|
| `Mostrar` | Booleano | Entrada | `false` | visibilidad del overlay |
| `Titulo` | Texto | Entrada | — | título |
| `Texto` | Texto | Entrada | — | cuerpo (soporta el patrón "Se creará una solicitud tipo **X**…") |
| `MostrarCampo` | Booleano | Entrada | `true` | si se muestra el campo de motivo/comentario |
| `EtiquetaCampo` | Texto | Entrada | `"Motivo de la solicitud"` | label del campo |
| `PlaceholderCampo` | Texto | Entrada | `""` | `HintText` de `inpCampoDlg` — placeholder de ejemplo (p. ej. "Ej: cierre de la plaza por mudanza del cliente…" en CI3); agregada por hallazgo R1-9 de `03-pantalla-cliente.md`, sin equivalente en la v1 de esta receta |
| `ValorCampo` | Texto | Entrada | `""` | valor tipeado (la pantalla lo lee con `cmpConfirmDialogX.ValorCampo` — Output, ver abajo) |
| `Tono` | Texto | Entrada | `"info"` | `"info"`/`"warn"`/`"danger"`/`"ok"` — icono y color del botón OK |
| `TextoOk` | Texto | Entrada | `"Confirmar"` | botón OK |
| `TextoCancelar` | Texto | Entrada | `"Cancelar"` | botón cancelar |
| `Cargando` | Booleano | Entrada | `false` | estado "aplicando…" — deshabilita todo, muestra Spinner (anti doble-click, corrige L7 de la v1: el spinner que quedaba trabado) |
| `OkHabilitado` | Booleano | Entrada | `true` | además de `!Cargando`, validación del campo (p. ej. motivo no vacío) |
| `CampoTexto` | Texto | **Salida** | — | `inpCampoDlg.Text` — la pantalla lo lee al confirmar |
| `OnOk` | Comportamiento | Entrada | — | acción del confirmar |
| `OnCancelar` | Comportamiento | Entrada | — | acción del cancelar (y de la ✕) |

**Árbol de controles**
```
cmpConfirmDialog
└── cntFondoDlg        Contenedor (manual)      velo full-screen
    ├── btnBloqueoDlg      Botón clásico         se come el clic del fondo (modalidad)
    └── cntCardDlg         Contenedor (manual)   card centrada
        ├── icoTonoDlg        Icono clásico       glifo por tono
        ├── lblTitDlg         Etiqueta clásica    título
        ├── icoXDlg           Icono clásico (Icon.Cancel)  cerrar (= cancelar)
        ├── lblTxtDlg         Etiqueta clásica    cuerpo
        ├── lblCampoLblDlg    Etiqueta clásica    label del campo (si MostrarCampo)
        ├── inpCampoDlg       Entrada de texto clásica (MultiLine)  motivo/comentario
        ├── spnCargaDlg       Control de giro moderno (Spinner)  visible si Cargando
        ├── btnCancelDlg      Botón clásico       cancelar
        └── btnOkDlg          Botón clásico       confirmar
```

| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `cntFondoDlg` | Contenedor | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · Visible `cmpConfirmDialog.Mostrar` |
| `btnBloqueoDlg` | Botón | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill/Hover/Pressed `Transparent` · Text `""` · **OnSelect `false`** · TabIndex `-1` |
| `cntCardDlg` | Contenedor | `(Parent.Width - fxDlgW)/2` | `(Parent.Height - Self.Height)/2` | `fxDlgW` | `<dinámica, ver nota>` | Fill `ThemeSurface` · Radius `13` · Border `ThemeLine` `1` · DropShadow `DropShadow.Regular` |
| `icoTonoDlg` | Icono | `22` | `20` | `34` | `34` | Icon `Switch(cmpConfirmDialog.Tono,"danger",Icon.Warning,"warn",Icon.Warning,"ok",Icon.CheckBadge,Icon.Information)` · color por tono (mismo Switch de 7.3) |
| `lblTitDlg` | Etiqueta | `icoTonoDlg.X + icoTonoDlg.Width + 12` | `22` | `Parent.Width - icoTonoDlg.Width - 76` | `24` | Text `cmpConfirmDialog.Titulo` · Size `10.88` · Bold · `Wrap: true` |
| `icoXDlg` | Icono | `Parent.Width - 30` | `22` | `18` | `18` | Icon `Icon.Cancel` · Color `ThemeInk3` · HoverColor `ThemeInk` · OnSelect `cmpConfirmDialog.OnCancelar` · `DisplayMode: If(cmpConfirmDialog.Cargando, Disabled, Edit)` |
| `lblTxtDlg` | Etiqueta | `lblTitDlg.X` | `lblTitDlg.Y + lblTitDlg.Height + 2` | `lblTitDlg.Width` | *(AutoHeight)* | Text `cmpConfirmDialog.Texto` · Size `9` · Color `ThemeInk2` · `AutoHeight: true` · `Wrap: true` |
| `lblCampoLblDlg` | Etiqueta | `lblTitDlg.X` | `lblTxtDlg.Y + lblTxtDlg.Height + 10` | `lblTitDlg.Width` | `16` | Text `cmpConfirmDialog.EtiquetaCampo` · ESTILO-CAMPO-LABEL · Visible `cmpConfirmDialog.MostrarCampo` |
| `inpCampoDlg` | Entrada de texto (MultiLine) | `lblTitDlg.X` | `lblCampoLblDlg.Y + lblCampoLblDlg.Height + 4` | `lblTitDlg.Width` | `64` | Default `cmpConfirmDialog.ValorCampo` · `HintText: cmpConfirmDialog.PlaceholderCampo` · Visible `cmpConfirmDialog.MostrarCampo` · `DisplayMode: If(cmpConfirmDialog.Cargando, Disabled, Edit)` · Fill `ThemeSurface2` · BorderColor `ThemeLine` |
| `spnCargaDlg` | Control de giro | `Parent.Width/2 - 9` | `Parent.Height - 56` | `18` | `18` | Visible `cmpConfirmDialog.Cargando` |
| `btnCancelDlg` | Botón | `btnOkDlg.X - 8 - Self.Width` | `Parent.Height - 50` | `110` | `32` | ESTILO-BTN-FANTASMA · Text `cmpConfirmDialog.TextoCancelar` · OnSelect `cmpConfirmDialog.OnCancelar` · `DisplayMode: If(cmpConfirmDialog.Cargando, Disabled, Edit)` |
| `btnOkDlg` | Botón | `Parent.Width - 22 - Self.Width` | `Parent.Height - 50` | `150` | `32` | Text `If(cmpConfirmDialog.Cargando, "Aplicando…", cmpConfirmDialog.TextoOk)` · Fill `Switch(cmpConfirmDialog.Tono,"danger",ThemeDanger,"ok",ThemeOk,ThemeAccent)` · Color `ThemeSurface` · `DisplayMode: If(cmpConfirmDialog.OkHabilitado && !cmpConfirmDialog.Cargando, Edit, Disabled)` · OnSelect `cmpConfirmDialog.OnOk` |

`cntCardDlg.Height` = `lblCampoLblDlg.Y + lblCampoLblDlg.Height + If(cmpConfirmDialog.MostrarCampo, inpCampoDlg.Height + 12, 0) + 66`
(crece si el diálogo lleva campo de motivo; se achica si `MostrarCampo = false`, como en SO3
que solo repite el comentario ya escrito en modo disabled).

`CampoTexto` (Output) `= inpCampoDlg.Text`.

> **Backend desplegado y conectado (2026-08-25)**: `sanic_CHResolveChangeRequest` ya está
> registrada como Custom API, el datasource `Environment` está agregado (§2,
> `DataSources/Environment.json` presente en el fuente) y `ctxResolveApiListoDet` quedó en
> `true` en el `OnVisible` real de `scrSolicitud` — `btnAprobarDet`/`btnRechazarDet` operan en
> `DisplayMode.Edit` sin banner de espera. Se conserva como referencia histórica breve el
> patrón de fallback usado mientras el backend no existía (§7.6 aplica a SO2 Aprobar/
> Rechazar): dejar los botones en `DisplayMode.Disabled`
> (`If(ctxResolveApiListoDet, DisplayMode.Edit, DisplayMode.Disabled)`) y mostrar un banner
> (receta 7.11) `"Backend pendiente: la Custom API sanic_CHResolveChangeRequest todavía no
> está desplegada."` — útil si se necesita replicar el patrón con otra Custom API que aún no
> esté desplegada. Detalle completo en la guía 05 §5.1/§9.

### 7.7 Panel lateral de alta/edición (RECETA, NO componente)

**Decisión: NO se componentiza.** A diferencia del diálogo de confirmación, el panel lateral
(CL2 alta de cliente, CI2 edición de correo — y por extensión, edición de sucursal/cuenta) NO
tiene forma de contenido fija: CL2 pide 3 campos (Nombre/CIFBCO/CIFCOM) + resumen de 3
cambios; CI2 pide 2 campos (Dirección/Notificar) con vista "actual→propuesto" por campo. Un
componente canvas no puede recibir hijos arbitrarios — forzarlo a un molde único de N campos
fijos sería sobre-ingeniería (mismo criterio que usó digitización para NO componentizar su
diálogo con textarea de motivo). Se fija el CHROME como receta; el CUERPO (campos) lo define
cada guía de pantalla.

**Chrome fijo** (`cnt…Panel`, Contenedor manual, ancla a la derecha):
| Control | Tipo | X | Y | Width | Height | Otras |
|---|---|---|---|---|---|---|
| `shpVeloPanel` | Rectángulo | `0` | `0` | `Parent.Width` | `Parent.Height` | Fill `ThemeOverlay` · Visible `<ctxPanelAbierto>` |
| `btnVeloClicPanel` | Botón (invisible) | `0` | `0` | `shpVeloPanel.X + shpVeloPanel.Width - fxPanelW` | `Parent.Height` | Fill/Hover `Transparent` · Text `""` · OnSelect cierra el panel (equivalente al clic-afuera) |
| `cnt…Panel` | Contenedor | `Parent.Width - fxPanelW` | `0` | `fxPanelW` | `Parent.Height` | Fill `ThemeSurface` · BorderColor `ThemeLine` (izquierdo, `1`) · `DropShadow: DropShadow.Regular` · Visible `<ctxPanelAbierto>` |
| `cnt…PanelHead` | Contenedor (hijo de `cnt…Panel`) | `0` | `0` | `Parent.Width` | `64` | Fill `Transparent` — contiene título+subtítulo (izq) + ícono `Icon.Cancel` (der, cierra el panel) |
| `cnt…PanelBody` | Contenedor (hijo, con **scroll**: usar Galería vertical en blanco de 1 columna si el contenido excede alto, o `Height` fijo con controles apilados por fórmula si entra) | `0` | `cnt…PanelHead.Y + cnt…PanelHead.Height` | `Parent.Width` | `Parent.Height - Self.Y - 64` | Fill `Transparent` — campos específicos de cada pantalla, apilados con `Y = <anterior>.Y + <anterior>.Height + 13` |
| `cnt…PanelFoot` | Contenedor (hijo) | `0` | `Parent.Height - 64` | `Parent.Width` | `64` | Fill `Transparent` — 2 botones alineados a la derecha (Cancelar fantasma + Confirmar primario/disabled) |

Cada campo de captura dentro de `PanelBody` sigue el mismo molde: `lblXLabel` (ESTILO-CAMPO-
LABEL) + `inpX`/`drpX` (Entrada de texto o Menú desplegable clásico, Fill `ThemeSurface`,
BorderColor `ThemeLine`, Radius `7`) + `lblXError` (ESTILO-CAMPO-ERROR, Visible condicional,
solo si el campo tiene validación) — detalle exacto de campos y validaciones en 02/03.

### 7.8 Paginación de galería

Fila fija al pie de la card (`cnt…Pag`, Contenedor, Height `36`, `Y = Parent.Height - 36`):
`lblResumenPag` (Etiqueta, `"<N> <entidad> · página <p> de <t>"`, ESTILO-META, X `16`) +
botones `‹`/números/`›` (Botón clásico, `28×28`, Radius `6`, alineados a la derecha con
`X = Parent.Width - 16 - Self.Width` encadenado hacia la izquierda). Paginación real con
`FirstN(LastN(...), <tamPágina>)` sobre el resultado YA filtrado (delegable hasta el filtro;
el corte de página es siempre en memoria sobre el resultado materializado del `ClearCollect`,
nunca sobre la tabla completa — evita traer miles de filas).

> 🧨 **`Skip()` no existe en Power Fx** — no es una función del lenguaje (a diferencia de otros
> motores de consulta) y usarla no compila. El patrón real de paginación, verificado contra el
> fuente (`scrClientes.fx.yaml`/`scrSolicitudes.fx.yaml`), es
> `FirstN(LastN(<ordenado>, CountRows(<col>) - (<pág> - 1) * <tam>), <tam>)`: `LastN` recorta
> desde el final del resultado ordenado hasta lo que queda después de descartar las páginas
> previas, y el `FirstN` exterior corta esa cola al tamaño de una página. Fórmula completa
> aplicada en 02 §5.3 (`galClientesCli.Items`) y 04 §4.2 (`galSolicitudesSol.Items`).

### 7.9 Buscador con ícono

`cnt…Busq` (Contenedor, Height `32`): `icoLupaBusq` (Icono `Icon.Search`, `16×16`, X `10`,
Color `ThemeInk3`) + `inp…Busq` (Entrada de texto clásica, X `32`, `Width: Parent.Width - 32`,
`HintText` según pantalla, Fill `ThemeSurface`, BorderColor `ThemeLine`, Radius `8`).
`OnChange` dispara el filtro delegable (`Filter` sobre el datasource, nunca sobre una
colección ya materializada con `AddColumns`).

### 7.10 Separador de fila

`shp…Sep` (Rectángulo clásico): Y = **TemplateSize − 1** (regla, no dos literales que se
contradigan), Height `1`, Fill `ThemeLineSoft`. Con `fxRowH` (`44`) eso es Y `43`; con
`fxRowHChica` (`38`), Y `37`.

### 7.11 Banner informativo

`cnt…Banner` (Contenedor, `Height: AutoHeight` vía `lblBanner.Height + 18`): rectángulo de
fondo suave + `lblBanner` (Etiqueta, `Wrap: true`, Size `8.63`, `AutoHeight: true`). Colores
por tono (mismo mapa de 7.3, usando la variante `*Soft` de Fill y el tono sólido de Color del
texto):
| Tono | Fill | Color texto |
|---|---|---|
| info | `ThemeInfoSoft` | `ThemeInfo` |
| warn | `ThemeWarnSoft` | `ThemeWarn` |
| danger | `ThemeDangerSoft` | `ThemeDanger` |
| ok | `ThemeOkSoft` | `ThemeOk` |

### 7.12 Diff actual → propuesto

Tres controles en fila: `lblActual…` (Etiqueta, Color `ThemeInk3`, **`Strikethrough: true`**
— propiedad nativa de Etiqueta clásica, no HTML) + `lblFlecha…` (Etiqueta, Text `"→"`, Color
`ThemeInk3`) + `lblNuevo…` (Etiqueta, Color `ThemeOk`, Semibold). Usado en: resumen de
línea del panel lateral (7.7), grilla de líneas de `scrSolicitud` (columnas "Valor actual" /
"Valor propuesto" del mockup SO2/SO5 — ahí van en columnas separadas de la grilla, no en la
misma fila; la variante en-línea con flecha es para los paneles CL2/CI2).

---

## 8. Gotchas de canvas

| # | Gotcha | Regla |
|---|---|---|
| 8.1 | `hh` en `Text()` es reloj de 12h SIN AM/PM | Formatos de fecha SIEMPRE con `HH` (`FmtFecha`/`FmtHora` de §3 ya lo respetan) |
| 8.2 | Galerías clásicas AUTO-SELECCIONAN la 1ª fila | Diseñar el panel "del seleccionado" (scrCliente, scrSolicitud) asumiéndolo; estados vacíos SOLO para colección vacía |
| 8.3 | `Reset()` de estados en `OnVisible` | Cada pantalla que reusa `ctx` de pestaña/orden/filtro debe `Reset()` sus controles y `UpdateContext` a default al entrar, o arrastra estado de la visita anterior |
| 8.4 | Fila clicable | La acción va en el `OnSelect` de la GALERÍA; TODO hijo del template (labels, pills, shapes) delega con `Select(Parent)`. Si la acción vive solo en el botón overlay al fondo del z-order, las etiquetas se comen el clic |
| 8.5 | Pills DENTRO de galería | `cmpConfirmDialog`/`cmpAppHeader` NO pueden ir dentro de una Galería (`PA2122` — canvas components no se anidan en el scope de fila). Las pills de fila (estado, RFC-en-curso) son **Botón clásico nativo** dentro del template, leyendo `ThisItem` con el mismo `Switch` de 7.3, no una instancia de componente |
| 8.6 | Visibilidad por pestaña simulada | Todo `Visible` de contenido de tab lleva la conjunción `tabSel = "X" && …` completa, sin excepción — ninguna pestaña se activa "por default de Studio" |
| 8.7 | `CH-Tbl-ClientAccount` usa `CH-Ch-RecordStatus`, NO `RecordStatus` | Las otras 3 tablas maestras (`Client`, `ClientBranch`, `ClientEmail`) exponen su estado como columna `RecordStatus`; `ClientAccount` la expone como `CH-Ch-RecordStatus` — mismo option set global `sanic_chchrecordstatus`, nombre de columna distinto. Copiar/pegar la fórmula de estado de una tabla a otra sin ajustar el nombre de columna rompe en Cuentas |
| 8.8 | `ChangeStatus` de la LÍNEA ≠ el de la CABECERA | La cabecera (`sanic_changestatus`) tiene el valor `Rejected` en `159460003`; la LÍNEA (`CH-Tbl-ClientDataChangeRequestLine.sanic_changestatus`) tiene `Denied` en el MISMO código `159460003`. Son dos enums de Power Fx distintos con nombres distintos — `'ChangeStatus (CH-Tbl-ClientDataChangeRequestLines)'.Denied`, nunca `.Rejected`, en fórmulas sobre líneas |
| 8.9 | `TargetTable`/`TargetRecordID` son TEXTO, no lookup polimórfico | Para leer el registro objetivo del RFC hace falta un `Switch(TargetTable, "sanic_chtblclient", LookUp('CH-Tbl-Clients', 'CH-Tbl-Client'=GUID(TargetRecordID)), "sanic_chtblclientbranch", LookUp('CH-Tbl-ClientBranches', 'CH-Tbl-ClientBranch'=GUID(TargetRecordID)), …)` explícito por tabla (literales = LOGICAL NAME, D6 RESUELTA) — Power Fx no tiene indirección dinámica de nombre de tabla |
| 8.10 | `OldValue`/`NewValue` de la línea son texto plano (100 car.) | Ya vienen formateados como el usuario los ve (label de option set, no código; texto de lookup, no GUID) — la pantalla que arma la línea (03/04) debe formatear ANTES del `Patch`, nunca dejarle el crudo a la vista de detalle |
| 8.11 | `sanic_name` en `CH-Tbl-ClientDataChangePermission` = la TABLA objetivo | Pese al nombre, no es un nombre libre descriptivo — es el mismo texto que `sanic_targettable` de la cabecera del RFC. Confirmado en `docs/procesos/01-administracion-datos-clientes.md` y en el patrón real de la v1 (`Name = gvarSelectedChangeRequest.TargetTable`) |
| 8.12 | Delegación | Filtros `Filter(t, campo = valor)` sobre columnas indexadas SON delegables; nada de `Upper()`, `AddColumns` antes de filtrar, ni `Sum`/agregados directos sobre el datasource — materializar con `ClearCollect` primero si hace falta agregar |
| 8.13 | `ClearCollect` + `If` sin `else` | `ClearCollect(col, If(cond, Filter(...)))` sin rama else revienta con `ErrorKind.Internal` cuando `cond` es falso (Blank como fuente). El `If` va POR FUERA: `If(cond, ClearCollect(col, Filter(...)), Clear(col))` |
| 8.14 | Coordenadas de template de galería | X del template = X del encabezado − X de la galería; se enuncia una vez por guía, no dos veces con números distintos |
| 8.15 | Labels con texto largo | `Wrap: true` + `Height` explícito (o `AutoHeight: true` cuando el contenedor padre también crece) — si no, el texto se corta contra `TemplateSize`/`Height` fijo |
| 8.16 | Números de cuenta sin enmascarar | El campo `sanic_Name` de `CH-Tbl-ClientAccount` (display "Numero de cuenta") es el NÚMERO CRUDO — enmascarar client-side (`"•••• " & Right(Text(...), 4)`) con revelado bajo demanda, NUNCA confiar en que el dato ya viene enmascarado del maestro |

### 8.1 Gotchas del formato `.fx.yaml` (vestido por fuente)

Aprendidos empaquetando/desempaquetando esta app real con `pac canvas pack`/`unpack` durante el
debugging en Studio — aplican a cualquier guía que se termine vistiendo control por control y
después se valide contra el `.fx.yaml`:

| # | Gotcha | Regla |
|---|---|---|
| 8.17 | Líneas en blanco dentro de un bloque `\|-` | Una línea vacía en medio de una fórmula multilínea (bloque YAML literal `\|-`) rompe `pac canvas pack` — la fórmula debe quedar sin líneas en blanco internas, aunque en Studio se vea con espaciado entre secciones |
| 8.18 | Comentarios `#` no soportados en YAML de propiedades | El formato `.fx.yaml` no admite `#` como comentario de línea a nivel de propiedad; dentro de una fórmula Power Fx el único comentario válido es `//` (o `/* … */`), nunca `#` |
| 8.19 | `#` en un scalar de una sola línea arranca comentario YAML | Si una fórmula de una sola línea contiene el carácter `#` (p. ej. un literal con `#` dentro de un string), YAML lo interpreta como inicio de comentario y trunca el resto — esa fórmula tiene que ir en bloque `\|-` aunque entre en una línea |
| 8.20 | Fórmulas con `{clave: valor}` o literales `"texto:"` | Cualquier fórmula que contenga un record inline (`{Campo: "X"}`) o un string con `:` seguido de espacio (que YAML puede confundir con separador clave-valor) va SIEMPRE en bloque `\|-`, nunca como scalar de una línea |
| 8.21 | Comillas dentro de fórmulas Power Fx | Se escapan DUPLICÁNDOLAS (`""""`), NUNCA con backslash (`\"`) — es la regla de escape de string de Power Fx, no de YAML; `\"` produce una fórmula rota o mal interpretada al empaquetar |

---

## 9. Checklist de salida de esta guía

- [ ] App creada con nombre/ícono/orientación/tamaño de diseño/Escalar-para-ajustar exactos (§1).
- [ ] `CH-CL-SharedComponents` agregada como dependencia de biblioteca de componentes.
- [ ] `cmpAppHeader` extendido en la biblioteca compartida con las Custom Properties y el
      árbol de §7.2 (título, subtítulo, activo, rol, nav de 2 pestañas).
- [ ] `cmpConfirmDialog` creado en la biblioteca compartida con las Custom Properties y el
      árbol de §7.6.
- [ ] Las 10 tablas + `Office365Groups` + `Environment` agregados como orígenes de datos (§2),
      sin datasources de muestra de la v1.
- [ ] `App.Formulas` pegado completo (§3): paleta, constantes `fx*`, formatos, mapa de área,
      identidad.
- [ ] `App.OnStart` pegado completo (§4): identidad, permisos deduplicados, rol de header,
      deep-link — UNA sola resolución, no repetida en `scrSplash`.
- [ ] `App.OnError` pegado (§4), con `FirstError.Message` real, nunca genérico.
- [ ] `scrSplash` construida completa (§5): árbol, timer, navegación por `gvarSolicitudSel`.
- [ ] Las 4 pantallas de datos (`scrClientes`, `scrCliente`, `scrSolicitudes`,
      `scrSolicitud`) creadas VACÍAS (`Fill: ThemeBg`) para que los `Navigate()` cruzados
      compilen desde el día 1.
- [ ] Alias `ESTILO-*` de §6 disponibles como referencia mientras se llenan las guías 02–05.
- [ ] App Checker en cero errores tras este paso (antes de tocar guía 02).

---

## Orden de construcción

1. Esta guía (app + datos + `Formulas` + `OnStart`/`OnError`).
2. Extender `cmpAppHeader` y crear `cmpConfirmDialog` en `CH-CL-SharedComponents` (§7.2/§7.6)
   — se prueban aislados (con datos de ejemplo hardcodeados en las Custom Properties) antes
   de consumirlos desde la app.
3. Publicar la biblioteca `CH-CL-SharedComponents` y agregarla como dependencia de
   `CH-App-ClientAdministration v2`.
4. Crear las 5 pantallas vacías (`scrSplash`, `scrClientes`, `scrCliente`, `scrSolicitudes`,
   `scrSolicitud`) para que los `Navigate()` compilen desde el primer día.
5. Llenar en orden: `02` scrClientes → `03` scrCliente → `04` scrSolicitudes →
   `05` scrSolicitud. Construcción en DOS PASES por pantalla: insertar+renombrar el árbol,
   luego llenar fórmulas.
6. App Checker en cero errores (delegación incluida) + prueba de humo: deep-link
   `?RFC=<guid>` aterriza en `scrSolicitud`; crear un RFC de alta, enviarlo, y verificar que
   los botones Aprobar/Rechazar decidan de verdad contra `sanic_CHResolveChangeRequest`
   (backend desplegado y conectado desde 2026-08-25 — ya no hay banner de espera; si en algún
   entorno la Custom API todavía no existiera, replicar el fallback documentado en 05 §5.1/§9).

---

## Divergencias declaradas vs mockup

| # | Divergencia | Por qué |
|---|---|---|
| D1 | `cmpAppHeader`/`cmpTheme` de `CH-CL-SharedComponents` son placeholders vacíos hoy, no componentes listos para usar | Verificado en el código: `cmpAppHeader.fx.yaml` tiene 8 líneas (un rectángulo, `Height 64`, sin Custom Properties, sin theming CashHub) y **ninguna app v2 existente lo consume** — Digitization v2 construyó su propio `cmpHeader` local en vez de este. `CH-App-ClientAdministration v2` es la primera app que lo usa de verdad; por eso §7.2 documenta la EXTENSIÓN completa como paso previo, no solo la instanciación |
| D2 | `cmpTheme` NO se reutiliza literalmente — se mantiene `App.Formulas` con tokens nombrados | `cmpTheme.fx.yaml` expone un `Record` de tema con paleta morada/índigo (`Primary: "#5B4BDF"`) ajena a la marca CashHub, sin relación con el rojo `#C8102E` del mockup, y NINGUNA app v2 (Digitization, Tracker) lo consume — todas usan `App.Formulas` con tokens `Theme*`. Repetir ese patrón probado (idéntico en Digitization/Tracker) es más consistente, más delegable y evita la indirección `cmpThemeX.Theme.Colors.Primary`. Se declara como mejora pendiente de ALM retirar o repropósito `cmpTheme`, fuera del alcance de este rebuild |
| D3 | El token `--sel: #BBD6F2` del `:root` del mockup no se traduce a ningún `Theme*` | Está definido en el CSS pero no se usa en ninguna vista del mockup (`.gSel` usa `var(--accent-soft)` + `box-shadow inset`, no `--sel`) — verificado por búsqueda en el HTML completo. Nada que fielizar |
| D4 | Tipo de RFC `"Activate"` mapeado a tono `ok` y texto `"Activación"` | El mockup no muestra ninguna vista con este tipo de RFC (solo Alta/Edición/Desactivación aparecen en las grillas) — mapeo por consistencia con el resto de §7.3, no verificado visualmente contra el mockup |
| D5 | Columna "Dirección" de sucursales (CI1: "Km 4.5 Carretera Norte") NO existe en el esquema real | `CH-Tbl-ClientBranch` no tiene ninguna columna de dirección/texto libre además de `Name`, `Client`, `ClientNameMirror`, `ClientStatus`, `RecordStatus` — verificado leyendo el Entity.xml completo. La guía 03 (scrCliente) debe OMITIR esa columna de la grilla de Sucursales o resolver el campo faltante con el equipo de datos antes de construir — no se inventa un campo que no existe |
| D6 | Texto exacto de `sanic_name` en `CH-Tbl-ClientDataChangePermission` — **RESUELTA**: es el LOGICAL NAME de la entidad (`sanic_chtblclient`/`sanic_chtblclientbranch`/`sanic_chtblclientemail`/`sanic_chtblclientaccount`), no la convención "Client"/"ClientBranch"/"ClientEmail"/"ClientAccount" que aproximaba esta tabla originalmente | Confirmado contra `data/seed/20-clientdatachangepermissions.json`, `data/seed/10-clienttablelogicalfieldnames.json` y `src/backend/CashHub.Plugins/Schema.cs` (`ChangePermission.TargetTable = "sanic_name"`, usado luego como `new Entity(targetTable, ...)` — un literal Dataverse real). Verificado también en las guías 02 (DC3) y 03 (hallazgo crítico #3). `fxAreaPorTabla` de §3 ya usa los literales correctos |
| D7 | Chip de rol (`gvarRolTexto`) asume que el usuario opera en UNA sola área | Ningún caso de negocio documentado contempla un usuario con permisos mixtos Banca Empresas + Bóveda simultáneos; si ocurriera, el chip muestra el área de la PRIMERA fila de `colMisPermisos` — aproximación honesta, no un bug oculto |
| D8 | Old app usaba `cr140_changestatus` (con el typo real `"Submited"`, un solo `d`); esta guía usa `sanic_changestatus` | El campo migró de publisher (`cr140_` → `sanic_`) y el option set NUEVO corrige el typo: sus 7 valores son `Draft/Submitted/Approved/Rejected/Applied/Failed/Canceled`, todos bien escritos — verificado en `Entity.xml` líneas 709-777. No hay typo que arrastrar en v2 |

**A verificar en Studio** (fuera de lo declarado arriba): nombre exacto del enum de Power Fx
para columnas cuyo display name colisiona entre tablas (`'Change Status (CH-Tbl-
ClientDataChangeRequests)'` vs como IntelliSense lo autogenere realmente); ancho real de
`lblUsuarioHdr`/`pilRolHdr` con nombres largos (la fórmula `Len(texto) * 5.x` es una
aproximación de kerning, no una medición exacta de Open Sans); si el ambiente permite
`Distinct()` sobre una colección local con más de unas pocas decenas de filas sin problema de
performance (la tabla de permisos es chica, no debería ser un problema, pero no se midió).

---

## Correcciones post-build 2026-08-25

Durante el debugging en Studio de `CH-App-ClientAdministration v2` se corrigieron fórmulas que
esta guía todavía mostraba en su versión vieja (rota). El fuente REAL y verificado
(`solutions/unpacked/apps/CanvasApps/src/sanic_chappclientadministration_fd97f/Src/`) mandó en
cada caso. Ledger de lo corregido en ESTA guía (01):

| # | Qué estaba mal | Por qué (fuente real) |
|---|---|---|
| PB1 | §7.8 describía la paginación con `FirstN`/`Skip` | `Skip()` no existe en Power Fx — no compila. El patrón real (verificado en `scrClientes.fx.yaml`/`scrSolicitudes.fx.yaml`) es `FirstN(LastN(<ordenado>, CountRows(<col>) - (<pág>-1)*<tam>), <tam>)` |
| PB2 | `lblMarcaSpl` (§5) listaba `Radius: 9` | La Etiqueta clásica no soporta `Radius` — el fuente real (`scrSplash.fx.yaml`) no setea esa propiedad; el tile queda cuadrado, no redondeado |
| PB3 | §7.6 y el checklist de §9 describían `sanic_CHResolveChangeRequest` como backend pendiente | El datasource `Environment` ya está agregado (`DataSources/Environment.json` presente) y `ctxResolveApiListoDet` quedó en `true` en el fuente de `scrSolicitud` — backend desplegado y conectado desde 2026-08-25. Se conservó el patrón de fallback como referencia histórica breve |
| PB4 | Faltaba documentar los gotchas del propio formato `.fx.yaml` descubiertos al empaquetar la app vestida | Se agregó §8.1 (gotchas 8.17–8.21): líneas en blanco dentro de bloques `\|-`, comentarios `#` no soportados, `#` en scalar de una línea arranca comentario YAML, fórmulas con `{clave: valor}`/`"texto:"` van en bloque `\|-`, comillas se escapan duplicando (`""""`) nunca con `\"` |

Ver también las correcciones equivalentes en `02-pantalla-clientes.md`, `03-pantalla-cliente.md`,
`04-pantalla-solicitudes.md` y `05-pantalla-solicitud.md` (cada una con su propio ledger de
"Correcciones post-build 2026-08-25").
