# Guía 01 — CH-App-Recounting v2: la app, los datos, las fórmulas globales, las recetas y los componentes

Se hace **UNA vez**, antes de tocar cualquier pantalla. Las guías `02-pantalla-sesion.md`,
`03-pantalla-tulas.md` y `04-pantalla-conteo.md` asumen TODO lo de acá y referencian sus
recetas por número (`receta 7.4`), sus alias de estilo por nombre (`ESTILO-CELDA-NUM`) y
sus componentes por nombre (`cmpMatrizMoneda`, §8.1).

Fuentes de verdad de este documento:

| Qué | Fuente | Regla |
|---|---|---|
| Diseño (layout, paleta, textos, flujos) | `docs/recounting-rebuild/mockup-recounting.html` (aprobado) | manda salvo que choque con el modelo |
| Modelo (columnas, option sets, PKs) | `solutions/unpacked/core/Entities/*/Entity.xml` (auditado 16-ago) | **manda siempre**; toda divergencia se declara en el ledger de `00` §6 |
| Display names en Power Fx | `Entity.xml` **cruzado** con el `Screen1.fx.yaml` de la v1 desempaquetada (fórmulas que ya corrieron en producción) | si el IntelliSense de Studio difiere, **el suyo manda** |
| Contrato con CH-App-Supervisor | `src/backend/CashHub.Validation/Security/ApprovalRequestResources.cs` | **manda siempre** — es el guard que lee el JSON |
| Decisiones de rediseño | `docs/recounting-rebuild/00-analisis-y-plan.md` | manda sobre la v1 |
| Lenguaje visual, recetas, estilos | `docs/reception-rebuild/01-app.md` (post-QA humano 16-ago) | se **copia**: es la misma casa, el mismo tema y los mismos alias |

> **Sobre display names**: los de este documento están verificados contra el `Entity.xml`
> **y** contra fórmulas de la v1 que efectivamente corrieron. Si el IntelliSense de Studio
> te ofrece otro nombre para la misma columna, **el suyo manda**: usalo y avisá para
> corregir esta guía.

> **Sobre la v1**: `sanic_chapprecounting` (msapp 54962) es **una sola `Screen1` de 11.239
> líneas** con 26 bugs confirmados. De ella se copian **nombres de columna y literales de
> option set**, jamás lógica.

---

## 0. Contrato de formato de las guías de pantalla (02, 03 y 04)

Estas reglas son la spec contra la que se revisan `02`, `03` y `04`. Una violación es un
**bug de la guía**, no una licencia creativa. Son las mismas 11 reglas de
`reception-rebuild/01-app.md` §0, con el catálogo de tipos y los sufijos de esta app.

1. **Árbol completo**: cada guía abre con el árbol de TODOS los controles de su pantalla,
   con el nombre final de cada uno. Nada se construye "a ojo".
2. **Cobertura total 1:1**: todo control del árbol tiene su descripción abajo — tabla
   propia, fila en tabla compartida, o **puntero explícito a una receta de esta guía**
   (`receta 7.2`) / **a un componente** (`§8.1`) cuando la receta o el componente fija
   TODAS sus propiedades. No hay controles en el cuerpo que falten en el árbol ni al
   revés. Una receta con placeholders sin resolver NO cuenta como cobertura.
3. **Tipo explícito**: cada control declara QUÉ componente de Power Apps es, usando
   exactamente estos nombres (los del menú **Insertar**). Catálogo CERRADO: si un control
   no está acá, no entra en la app.

   | Tipo en la guía | En Studio (Insertar) | Dónde se usa |
   |---|---|---|
   | Etiqueta clásica | Clásico → **Etiqueta** (`Label`) | todo texto |
   | Botón clásico | Clásico → **Botón** (`Button`) | acciones, píldoras, superficies redondeadas, overlays de fila |
   | Entrada de texto clásica | Clásico → **Entrada de texto** (`Text input`) | N° de tula (scan), cantidad de denominación, N° de cuenta, N° y monto de cheque manual |
   | Menú desplegable clásico | Clásico → **Menú desplegable** (`Drop down`) | mesa de conteo (`02`), moneda del depósito, tipo de cheque manual |
   | Icono clásico | Iconos → el icono nombrado (`Icon`) | glifos (papelera, recargar, lupa, check, ✕, candado, ⓘ, ⊘, flecha atrás) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) | SOLO barras, líneas y separadores (esquina viva) |
   | Casilla clásica | Clásico → **Casilla** (`Check box`) | tildar un cheque declarado en la grilla de cheques (`04`) |
   | Temporizador clásico | Clásico → **Temporizador** (`Timer`) | DOS usos y ninguno más: el **polling de aprobación** (receta 7.7) y el **defer de un frame** del guardado diferido (receta 7.10.C). Nunca como bucle de iteración |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** | todas las grillas y listas |
   | Contenedor de diseño manual | Diseño → **Contenedor** (`groupContainer`) | card de los diálogos (es el único con `DropShadow`) y overlay de bloqueo |
   | Lista de pestañas moderna | Moderno → **Lista de pestañas** (`TabList`) | Declarado/Recontado y Billetes/Monedas/Cheques (`04`) |
   | Spinner moderno | Moderno → **Spinner** | espera de aprobación (`02` y `04`) |
   | Componente canvas | Insertar → **Componentes personalizados** | `cmpMatrizMoneda`, `cmpKpiSesion`, `cmpDialogo` (§8) |

   > **Los DOS modernos permitidos** son `Lista de pestañas` y `Spinner` — los habilita
   > `00` §2.4. **No hay más excepciones modernas**: cualquier otro control moderno
   > (`TextInput`, `NumberInput`, `Dropdown`, `Checkbox`, `Combobox`, `Button`, `Text`)
   > queda FUERA del catálogo, aunque la v1 lo usara.

   > **Regla de preferencia**: CLÁSICO por defecto. Quedan **prohibidos sin excepción**
   > los controles de código y de terceros que mataron a la v1: **PowerCAT `Dialog`**
   > (los 8 diálogos, cada uno con un `htmlViewer` a pantalla completa siempre
   > instanciado), `Panel`, y los **Formularios (`Form` + dataCards)** — incluidos los
   > "invisibles de 1×1 px" que la v1 usaba como mecanismo de guardado. El guardado es
   > `Patch` explícito.

   > 🧨 **Radius**: en esta plataforma solo tienen `Radius` el **Botón clásico**
   > (default `10`), la **Entrada de texto clásica** (default `5`) y el **Contenedor**
   > (`groupContainer`, default `4`). El **Rectángulo NO** y la **Etiqueta NO**. Por eso
   > toda superficie con esquinas redondeadas es un Botón clásico (receta 7.1) o un
   > Contenedor, y el rectángulo queda para líneas y barras de esquina viva. Si una tabla
   > de `02`/`03`/`04` te pide `Radius` sobre un rectángulo o una etiqueta, es un bug de
   > la guía.

4. **Propiedades**: cada tabla lista SOLO las propiedades que cambian del default. Los
   defaults tipográficos y de caja viven en §6 y **mandan**: si una propiedad no figura en
   la tabla del control, vale lo que dice §6.
5. **Nombres únicos en TODA la app**: en canvas el nombre de control es único a nivel
   **aplicación**, no de pantalla — si repetís un nombre, Studio lo renombra solo y te
   rompe las fórmulas de la guía. Todo control lleva el sufijo de su pantalla:

   | Pantalla | Sufijo | Ejemplos |
   |---|---|---|
   | `scrSesion` | **`Ses`** | `btnSolicitarSes`, `ddMesaSes`, `tmrPollSes`, `lblTituloSes` |
   | `scrTulas` | **`Tul`** | `inpScanTul`, `galTulasTul`, `btnAbrirTul`, `cmpKpiNIOTul` |
   | `scrConteo` | **`Con`** | `galDepRecontadosCon`, `tabDetalleCon`, `inpCantidadBilCon`, `cmpMatNIOCon` |

   Los controles **internos de un componente canvas** (§8) viven en su propio espacio de
   nombres: ahí el sufijo es el del componente (`Mat`, `Kpi`, `Dlg`) y **no** el de
   pantalla.
6. **Orden del árbol = orden de INSERCIÓN = z-order**: lo insertado DESPUÉS queda ENCIMA.
   Donde el z-order importa (overlay de fila al FONDO, modal al TOPE), la guía lo dice
   explícito en la fila del control.
7. **Coordenadas dentro de una galería**: el `X`/`Y` de un control del template es
   **relativo al template**, que arranca en `0`. Regla derivada, que se enuncia una sola
   vez por guía: **X del control del template = X del encabezado − X de la galería**.
8. **Separador de fila**: `shp…Sep` (Rectángulo clásico), `Y` = **`TemplateSize − 1`**,
   `Height` `1`, `Fill` `ThemeLineSoft`. Nunca dos literales que se puedan contradecir al
   cambiar el `TemplateSize`.
9. **Cero números mágicos duplicados**: si dos controles comparten un borde, uno
   referencia al otro (`lblTotalCon.Y = lblTotalLabCon.Y + lblTotalLabCon.Height`), nunca
   dos literales que se puedan desincronizar.
10. **Cada guía abre con un CONTRATO DE ENTRADA** (qué `gvar`/`col` deben venir seteadas
    al entrar y quién las setea) **y cierra con un CHECKLIST DE SALIDA** verificable por
    el constructor. Sin eso, el revisor R2 no tiene contra qué verificar.
11. **Construcción en DOS PASES**: pase 1, insertar y renombrar todos los controles en el
    orden exacto del árbol (ese orden ES el z-order), sin tocar propiedades; pase 2,
    recorrer las tablas llenando propiedades. Si vas control por control completo, vas a
    escribir fórmulas contra controles que todavía no existen y Studio las rechaza.

> **Regla 12, propia de esta app**: **los tres componentes de §8 se construyen ANTES que
> las pantallas.** `03` y `04` insertan instancias de `cmpKpiSesion`, `cmpMatrizMoneda` y
> `cmpDialogo` en su primer pase; si el componente no existe, el árbol no se puede armar.

---

## 1. Identidad de la app

1. `make.powerapps.com` → entorno **DEV** → **Apps → Nueva app → página en blanco →
   Tamaño: Tableta**.
2. **Configuración → General**:
   - Nombre: `CH-App-Recounting v2` (se renombra a `CH-App-Recounting` cuando reemplace a
     la v1 en producción).
   - Ícono: el PNG de marca de reconteo (paso 6 del pipeline de `00` §5). Color de fondo
     del ícono: `#C8102E`.

   > 🧨 **SON DOS CARGAS DISTINTAS, no una.** El ícono de *Configuración → General* es
   > metadata de la app: **no** se puede referenciar desde una fórmula. Esta app **no
   > necesita** el arte como recurso de medios: el tile de marca del header y del rail es
   > un **Botón clásico** con `Text: "C$"` (así lo dibuja el mockup, `.brandTile`), no una
   > Imagen. Si alguna vez se quisiera el PNG en pantalla, hay que subirlo aparte por
   > **Medios → Cargar**.

   - **Límite de filas de datos: `2000`** (default 500 — la v1 lo dejó en 500 y de ahí
     salían truncamientos silenciosos).

   > 🧨 **El tope es red de seguridad, NO un supuesto.** Las reglas de §2.3 y todas las
   > consultas de `02`/`03`/`04` están escritas asumiendo el **peor caso: 500**. Ninguna
   > fórmula de esta app puede depender de que el tope sea 2000: si una consulta solo
   > funciona con 2000, es un bug de la guía.

3. **Configuración → Pantalla y diseño**: Orientación **Horizontal**, Tamaño
   **1366 × 768**, **Escalar para ajustar: ACTIVADO**, Bloquear relación de aspecto:
   **Activado**.

   > **Decisión declarada**: la app se diseña a lienzo fijo 1366 × 768. Son tres pantallas
   > de grilla densa para un puesto de trabajo fijo (mesa de conteo), y el plan no pide
   > responsive. Con *Escalar para ajustar* activado, 1 unidad de canvas = 1 px del mockup
   > y todas las coordenadas de `02`/`03`/`04` se copian tal cual.

4. **Configuración → Características próximas**: se activan **DOS y solo dos**
   (divergencia declarada vs. Reception v2, que no activa ninguna — ledger `L-1`):

   | Bandera | Para qué | Si no está en tu Studio |
   |---|---|---|
   | **Funciones definidas por el usuario** (*User-defined functions*) | `HtmlEscape(t)` y las 14 funciones `fx…` de §3.1 llevan **parámetros**. Sin UDFs no hay función con parámetros y el HTML de approval se escribe inline, una vez por dato, en cada uno de los 3 lugares que lo arman | fallback en §3.1, nota **F**: cada `fx…` se copia inline en el `OnSelect` que lo usa, con `With()` en vez de parámetros. La expresión no cambia; sí se duplica |
   | **Propiedades de componente mejoradas** (*Enhanced component properties*) | `cmpDialogo` expone `OnConfirmar` / `OnCancelar`, que son propiedades de **comportamiento** | fallback en §8.3, nota **B**: el diálogo se arma con la receta 7.2 en cada pantalla y el componente se limita a las 3 confirmaciones de `04` |

   **Nada más.** Ninguna otra bandera experimental o de vista previa.

5. **Conectores: UNO SOLO.** `CH-Flow-ProcessXMLFiles` (Power Automate), que es el que lee
   los XML de la máquina contadora (§2.1, fila 16). **Cero Office365, cero Graph.** El
   acceso lo controlan el *sharing* de la canvas app y el rol de Dataverse; la seguridad
   de operaciones es **server-side** (los plugins de `00` §2.2). La identidad del usuario
   sale de `User()` y del registro de `Users` que resuelve `App.OnStart` (§3.2).
6. **Tema**: el tema de la solución declara **Open Sans** como fuente de cuerpo. Se respeta
   (§6). Ojo con los glifos: los que no existen en Open Sans salen como caja vacía
   (*tofu*) — por eso **todo glifo va como Icono clásico**, no como texto. Única excepción
   declarada: el texto `"C$"` del tile de marca, que son dos caracteres ASCII.
7. Agregá la app a la solución **CashHubApps** (portal de soluciones → Agregar existente →
   Aplicación → Canvas) para que export/deploy siga el pipeline actual.
8. Creá **las tres pantallas vacías de una** (Nueva pantalla → En blanco, renombrar y
   `Fill = ThemeBg`): `scrSesion`, `scrTulas`, `scrConteo`, **en ese orden**. Motivo: los
   `Navigate()` cruzados compilan desde el primer día y `scrSesion` queda primera (§3.4).
   Borrá la pantalla en blanco que Studio crea por defecto.

---

## 2. Orígenes de datos (Dataverse)

### 2.1 Las 15 tablas + 1 flujo — y nada más

Agregá EXACTAMENTE estos (Datos → Agregar datos). El display name es el que ves en el
panel. **El PK no se deduce del plural: se lee del `Entity.xml`.**

| # | Origen (display) | PK (columna) | Para qué |
|---|---|---|---|
| 1 | `CH-Tbl-RecountingSessions` | `CH-Tbl-RecountingSession` | Sesión diaria cajero + mesa |
| 2 | `CH-Tbl-RecountingSessionDetails` | `CH-Tbl-RecountingSessionDetail` | Un registro por **bolsa + sesión**. Es la **llave que la contadora usa** (`ID_Sesion`) |
| 3 | `CH-Tbl-CountingDesks` | `CH-Tbl-CountingDesk` | Mesas de conteo + serial de la contadora |
| 4 | `CH-Tbl-Bags` | `CH-Tbl-Bags` | Tulas |
| 5 | `CH-Tbl-Receptions` | `CH-Tbl-Receptions` | Planilla y cliente de la tula (`ReceiptID`, `RemitentName`) |
| 6 | `CH-Tbl-DeclaredBags` | `CH-Tbl-DeclaredBags` | Declaración de la tula (totales declarados) |
| 7 | `CH-Tbl-DeclaredDeposits` | `CH-Tbl-DeclaredDeposits` | Depósitos declarados |
| 8 | `CH-Tbl-DeclaredChecks` | `CH-Tbl-DeclaredChecks` | Cheques declarados |
| 9 | `CH-Tbl-DepositsRecountings` | `CH-Tbl-DepositsRecounting` | Depósitos recontados |
| 10 | `CH-Tbl-CountedMoneyDetails` | `CH-Tbl-CountedMoneyDetails` | Desglose de denominaciones por depósito |
| 11 | `CH-Tbl-Checks` | `CH-Tbl-Checks` | Cheques recontados |
| 12 | `CH-Tbl-MoneyDenominations` | `CH-Tbl-MoneyDenomination` | Catálogo de denominaciones por moneda y tipo |
| 13 | `CH-Tbl-XMLReadingDetails` | `CH-Tbl-XMLReadingDetail` | Lectura de la máquina contadora (columna **Auto**) |
| 14 | `CH-Tbl-ApprovalRequests` | `CH-Tbl-ApprovalRequest` | Solicitudes al supervisor + polling |
| 15 | `Users` | `User` | **Solo** para resolver `gvarUser` una vez en `OnStart` (§3.2) y para tipar el lookup `Cashier`. Cero `Filter` sobre ella |
| 16 | `CH-Flow-ProcessXMLFiles` | — (flujo) | Dispara el procesamiento de los XML de la contadora (receta 7.11) |

**Lo que la v1 traía y la v2 NO agrega — cada exclusión con su motivo:**

| Origen de la v1 | Por qué se va |
|---|---|
| `CH-Tbl-XMLReadings` | Con el espejo **E-4** (§2.2) la lectura de la contadora se resuelve en **un salto** sobre `CH-Tbl-XMLReadingDetails`. La v1 la usaba solo como escalón intermedio de un `in` con subconsulta que **jamás delegaba** (bug **D-1**) |
| `CH-Tbl-Transactions` | 🚫 **La app ya no las crea.** Los asientos los genera el plugin al pasar la bolsa a `Recontada` (`00` §2.2, punto 3). Sin escrituras, no hay motivo para cargar el datasource |
| `CH-Tbl-Declarations` | Todo lo que el diálogo de desambiguación de declaraciones (`B2`) necesita vive en `CH-Tbl-DeclaredBags`: `BagNumber`, `TotalNIO/USD/EUR`, `TotalDeposits` (rollup) y `'Created On'`. El **cliente** ya viene de la tula (`ReceptionID.RemitentName`). Ver divergencia `L-9` |
| `CH-Tbl-Received Emails` | Solo se usaba para mostrar el remitente del correo de la declaración. Es una tabla grande, un **dot-walk de 2 saltos** (`DeclarationID.ReceivedEmail.…`) y un dato que el cajero no necesita para decidir. Ver divergencia `L-9` |
| `CH-Tbl-ReceptionBatches` | El N° de lote del diálogo `B1` se lee **navegando el registro ya cargado** (`ThisItem.ReceptionBatchId.Name`) sobre una colección de ≤ decenas de filas. Navegar un lookup **no exige** el datasource del otro lado (mismo criterio que Reception `01` §2.1 con `CH-Tbl-Clients`) |
| `ComboBoxSample`, `CustomGallerySample`, `DropDownSample` | Samples de plantilla. Nunca se usaron; cada datasource pesa en el arranque |
| `cat_powercatcomponentlibrary` (librería PowerCAT) | 🚫 Los 8 diálogos pasan al patrón nativo (receta 7.2 / `cmpDialogo`). `00` §1.4 |

> Si Studio pidiera un datasource **solo para TIPAR** un lookup (por ejemplo
> `CH-Tbl-ReceptionBatches` al escribir `ReceptionBatchId.Name`), se agrega **solo para
> tipar, jamás para consultar** (cero `Filter`/`LookUp` sobre él) y se anota en el ledger
> de `00`.

### 2.2 🧨 Los CUATRO espejos server-side (backend que TODAVÍA NO EXISTE)

`00` §2.2 los declara **no opcionales**. Son columnas nuevas pobladas por plugin
Pre-operation (mismo patrón que `ClientBranchNameMirrorPlugin` de Reception). **La app
NUNCA las escribe: solo las lee.**

| ID | Columna (display exacto) | Tabla | Tipo | Mata | Estado |
|---|---|---|---|---|---|
| **E-1** | `RecountingSessionMirror` | `CH-Tbl-DepositsRecountings` | Lookup → `CH-Tbl-RecountingSessions` | **D-2**: los 12 `Sum(Filter(…, BagID.RecountingSession…))` de 2 saltos que mostraban cifras parciales de dinero | 🔴 **NO existe** |
| **E-2** | `Bag` | `CH-Tbl-Checks` | Lookup → `CH-Tbl-Bags` | **D-3**: el `UpdateIf` de 2 saltos que dejaba sin marcar los cheques después de los primeros 500 | 🟡 **La columna YA EXISTE** (`sanic_bag`, descripción *"Mantenido por plugin. No editar a mano."*). Falta confirmar que el plugin la puebla y **backfill** de lo histórico |
| **E-3** | `DeclaredBagMirror` | `CH-Tbl-DeclaredChecks` | Lookup → `CH-Tbl-DeclaredBags` | Los ~82 round-trips de la apertura de tula (un `Filter` remoto por depósito declarado) | 🔴 **NO existe** |
| **E-4** | `RecountingSessionDetailMirror` (lookup → `CH-Tbl-RecountingSessionDetails`) **+** `XMLReadingActiva` (Sí/No) | `CH-Tbl-XMLReadingDetails` | Lookup + bit | **D-1**: el `in` con subconsulta que jamás delegaba ⇒ columna **Auto** en 0 con >500 filas históricas | 🔴 **NO existen** |

> 🧨 **E-4 son DOS columnas, no una** (divergencia `L-3` vs. el plan). La consulta de la
> contadora necesita **dos** condiciones: la sesión-detalle **y** que la lectura esté
> activa (`XMLReadings.IsActive`). Con un solo espejo de lookup habría que volver a
> `CH-Tbl-XMLReadings` a preguntar `IsActive`, y con eso vuelve el segundo salto que E-4
> venía a matar. El mismo plugin estampa las dos y **repropaga `XMLReadingActiva` cuando
> la lectura padre cambia de estado**.

> 🧨 **E-2 ya existe: verificalo antes de escribir la primera fórmula de `04`.** Escribí
> `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = ` en el `Items` de una galería de prueba y
> mirá si IntelliSense autocompleta. Si no aparece: **quitá la tabla y volvé a agregarla**
> desde el panel Datos (el datasource generado por script no siempre trae la metadata de
> relación del lookup — remedio ya documentado en el Hub).

**Fallback "sin backend" de cada espejo** — toda fórmula de `02`/`03`/`04` que dependa de
un espejo lleva el marcador **🧨 espejo E-n** y su fallback al lado:

| Espejo | Fallback sin backend | Costo del fallback |
|---|---|---|
| **E-1** | `Clear(colDepositosSesionTul); ForAll(colTulasSesionTul As B, Collect(colDepositosSesionTul, Filter('CH-Tbl-DepositsRecountings', BagID.'CH-Tbl-Bags' = B.'CH-Tbl-Bags')))` | **N+1 acotado a las tulas de la sesión** (decenas). Es lento pero **no miente**: nunca trunca |
| **E-2** | Recorrer `colDepRecontadosCon` y hacer un `Patch` por depósito: `Filter('CH-Tbl-Checks', RelatedReCounterDepositID.'CH-Tbl-DepositsRecounting' = D.'CH-Tbl-DepositsRecounting')` (ese salto **sí** delega hoy) | N+1 acotado a los depósitos de la tula (unidades) |
| **E-3** | `Clear(colChequesDeclaradosCon); ForAll(colDepDeclaradosCon As D, Collect(colChequesDeclaradosCon, Filter('CH-Tbl-DeclaredChecks', DeclaredDepositID.'CH-Tbl-DeclaredDeposits' = D.'CH-Tbl-DeclaredDeposits')))` | N+1 acotado a los depósitos declarados de la tula |
| **E-4** | 🚫 **NO hay fallback delegable.** Hay que agregar `CH-Tbl-XMLReadings` como datasource #17 y hacer **dos** consultas: primero `ClearCollect(colLecturasCon, Filter('CH-Tbl-XMLReadings', RecountingSessionDetailId.'CH-Tbl-RecountingSessionDetail' = gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail' && IsActive = true))` y después un `ForAll` con un `Filter('CH-Tbl-XMLReadingDetails', XMLReading.'CH-Tbl-XMLReading' = L.'CH-Tbl-XMLReading')` por lectura | N+1 acotado a las lecturas de esa bolsa. **Sigue siendo delegable en cada salto** — que es lo único que la v1 no lograba |

> **Ninguno de los fallbacks trunca.** Ese es el criterio: preferimos N+1 acotado y
> correcto antes que una consulta rápida que devuelve 500 filas y calla.

### 2.3 Reglas duras de consulta (delegación) — sin excepción

Se aplican a TODA fórmula de `02`, `03` y `04`. **Todas se evalúan contra un
`DataRowLimit` de 500**, no contra el 2000 configurado.

| # | Prohibido | Qué se hace en su lugar |
|---|---|---|
| **D-1** | `a.b = c` navegando a una **columna de datos** del registro relacionado (`BagID.BagStatus`, `RecountingSessionId.CountingDesk.MachineSerialNumber`, `DeclarationID.ReceivedEmail…`) dentro de `Filter`/`LookUp`/`RemoveIf`/`Sum` **sobre un datasource** | Comparar **PK contra PK**: `Filter('CH-Tbl-Bags', RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession')` — ver 🧨 abajo |
| **D-2** | `Sum(Filter(ds, …), col)` / `CountRows(Filter(ds, …))` sobre un **datasource** | Sumar/contar sobre la **colección local** ya cargada, o leer un total materializado server-side (`Bags.TotalNIO/USD/EUR`, que recalcula el plugin) |
| **D-3** | `RemoveIf(ds, predicado)` / `UpdateIf(ds, predicado, …)` | `Remove(ds, registro)` / `Patch(ds, registro, {…})` con el registro explícito, dentro de un `ForAll` sobre la **colección local** |
| **D-4** | `Filter`/`LookUp`/`Search` en el `Items` de un control **dentro** del template de una galería | El `Items` apunta a una **colección local** cargada una vez en `OnVisible`. **Sin excepciones en esta app** |
| **D-5** | `Refresh(ds)` **como parche de estado local después de un `Patch`** (el vicio de la v1: 3 `Refresh` por `OnVisible` + 2 timers de 5 s con `Refresh` en cascada) | Actualizar la colección local con lo que devuelve el `Patch`, o recargar con UNA consulta delegable. **PERMITIDO en DOS formas acotadas** — ver 🧨 abajo |
| **D-6** | `in` con **subconsulta** (`X.Id in Filter(otraTabla, …).Id`) | Espejo server-side (§2.2). Es literalmente el bug **D-1** de `00` §1.1 |
| **D-7** | `Upper()` / `Lower()` / `Len()` sobre una columna de un datasource dentro de un predicado | Igualdad exacta o `StartsWith`. Los números de tula y de cheque se comparan con `Trim()` del lado del **cliente**, nunca con `Upper()` del lado del servidor |
| **D-8** | `Gallery.AllItems` como **fuente de un total** o de un `Filter` de negocio | Los totales salen SIEMPRE de las colecciones (`colDetalleConteoCon`, `colChecksCon`). `AllItems` se usa **solo** en el volcado de la receta 7.10.A y en ningún otro lado. La v1 lo usaba en 12 sumas y **des-virtualizaba** las galerías |
| **D-9** | Confiar en el orden que trae un datasource (o una vista) después de ponerle un `Filter` | **`SortByColumns` EXPLÍCITO** en el `Items` de toda galería maestra y en todo `ClearCollect` cuyo orden importe |
| **D-10** | Cachear en una **colección** una tabla de Dataverse que **puede crecer** | Colección **SOLO si la tabla es estructuralmente acotada (< 500 filas por diseño del negocio)**. Ver tabla abajo |
| **D-11** | `ClearCollect(col, If(cond, Filter(...)))` **sin rama else** | El `If` va **POR FUERA**: `If(cond, ClearCollect(col, …), Clear(col))`. Sin else, la fuente es `Blank` y revienta con `ErrorKind.Internal` |
| **D-12** | `Set()` dentro de un `ForAll` | Power Fx no lo permite y `pac canvas pack` **no valida fórmulas**: viaja publicado y explota en Studio. Se acumula con `Collect()` a una colección y **un solo** `Set()` al salir |
| **D-13** | 🧨 `IsBlank(<lookup>)` o `<lookup> = Blank()` **pelados** dentro de un `Filter`/`LookUp` sobre un **datasource** de Dataverse | **Siempre contra la PK del lookup**: `IsBlank(BagID.'CH-Tbl-Bags')`. Ver 🧨 abajo — el App Checker **NO** lo detecta |
| **D-14** | 🧨 `ForAll(<datasource o Filter sobre datasource>, Remove(<ese mismo datasource>, …))` — y lo mismo con `Patch` | **Patrón SNAPSHOT**: `ClearCollect(colTmp, Filter(ds, …))` → `ForAll(colTmp As X, Remove(ds, X))` → `Clear(colTmp)`. Ver 🧨 abajo |

#### 🧨 D-13 en detalle: el lookup pelado COMPILA y revienta en runtime

```powerfx
Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul && IsBlank(BagID))                    // 🚫 RUNTIME
Filter('CH-Tbl-DeclaredBags', BagNumber = ctxScanTul && IsBlank(BagID.'CH-Tbl-Bags'))      // ✅
```

**Jamás `IsBlank(<lookup>)` ni `<lookup> = Blank()` pelados en un `Filter` sobre
Dataverse — siempre contra la PK (`<lookup>.'PK de la tabla apuntada'`).** El lookup pelado
obliga al conector a resolver el **registro** relacionado: arma un `$expand` de la
*navigation property* y mete su nombre en el `$select` interno. Dataverse contesta

```
Could not find a property named 'sanic_Xxx' on type 'Microsoft.Dynamics.CRM.…'
```

y la consulta vuelve **vacía**, sin error visible en la UI. Con la PK explícita, en cambio,
el predicado se pliega a un `eq null` sobre la anotación `_sanic_xxx_value` del propio
registro: **un solo salto, delegable** (es el mismo argumento de **D-1**).

🚨 **El App Checker NO lo detecta**: la fórmula compila en verde y ni siquiera marca
delegación. Falla **solo en runtime**, con datos reales. Es exactamente el mismo criterio de
**PK y nada más que PK** que ya rige la existencia de un registro
(`IsBlank(gvarSesion.'CH-Tbl-RecountingSession')`, §5.8): en esta app **un lookup nunca se
lee entero, se lee por su PK**. Ledger **`L-61`**.

#### 🧨 D-14 en detalle: no se itera un datasource mientras se lo modifica

```powerfx
// 🚫 El runtime aborta el ForAll a mitad de camino: la fuente se re-evalúa mientras se borra
ForAll(
    Filter('CH-Tbl-CountedMoneyDetails', RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = …) As M,
    Remove('CH-Tbl-CountedMoneyDetails', M)
);

// ✅ SNAPSHOT: se congela en una colección, se itera la colección, se limpia
ClearCollect(
    colBorrarTmpCon,
    Filter('CH-Tbl-CountedMoneyDetails', RelatedDepositRecountingID.'CH-Tbl-DepositsRecounting' = …)
);
ForAll(
    colBorrarTmpCon As M,
    IfError(
        Remove('CH-Tbl-CountedMoneyDetails', M),
        If(true,
            Collect(colFallidasCon, { Clave: Text(M.Denomination), Detalle: "denominación", Mensaje: FirstError.Message });
            Blank()
        )
    )
);
Clear(colBorrarTmpCon);
```

**`ForAll` sobre un datasource + `Remove`/`Patch` del MISMO datasource está prohibido por el
runtime**: la fuente del `ForAll` es una consulta **viva**, y borrar filas mientras se la
recorre deja el lote **a medio ejecutar y sin error**. El patrón canónico es el **snapshot**:
`ClearCollect` a una colección temporal → `ForAll` sobre la colección → `Clear`. Es el mismo
motivo por el que los pasos de baja de la receta §7.5 recorren `colDetalleDVCon` /
`colChecksDVCon` (que **ya son** snapshots) y no el datasource. Ledger **`L-63`**.

> **La colección temporal es LOCAL del paso**, no del inventario de §4.2: nace con el
> `ClearCollect`, muere con el `Clear` de la línea siguiente y **no se siembra en
> `App.OnStart`** — la siembra de §3.2 es para las colecciones que un `Items`, un `Visible`
> o un `IsEmpty()` leen antes de que su poblador haya corrido, y ninguna de estas lo es.

#### 🧨 D-1 en detalle: la igualdad de registro **NO COMPILA** — se compara PK contra PK

```powerfx
Filter('CH-Tbl-Bags', RecountingSession = gvarSesion)          // 🚫 NO COMPILA
```

**Power Fx no acepta comparar una columna de lookup contra un registro.** La forma
**canónica y única** en toda la app es:

```powerfx
Filter(
    'CH-Tbl-Bags',
    RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'
)
```

**Y sigue siendo DELEGABLE**: la PK de un lookup **no es un dot-walk a una columna del
registro relacionado** — Dataverse la devuelve como anotación del propio registro
(`_sanic_recountingsession_value`) y el `$filter` se pliega a un `eq` de GUID. Lo que D-1
prohíbe es navegar a una **columna de datos** del otro lado.

| Forma | ¿Compila? | ¿Delega? |
|---|---|---|
| `RecountingSession = gvarSesion` | 🚫 **NO** | — |
| `RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion.'CH-Tbl-RecountingSession'` | ✅ | ✅ `eq` de GUID |
| `RecountingSession.Confirmed = true` | ✅ | 🚫 dot-walk a columna de datos (D-1) |

**Las dos PK van explícitas, las dos veces.** Escribir
`RecountingSession.'CH-Tbl-RecountingSession' = gvarSesion` tampoco compila.

**Las PK de esta app** (§2.1): `'CH-Tbl-RecountingSession'`,
`'CH-Tbl-RecountingSessionDetail'`, `'CH-Tbl-CountingDesk'`, `'CH-Tbl-Bags'`,
`'CH-Tbl-Receptions'`, `'CH-Tbl-DeclaredBags'`, `'CH-Tbl-DeclaredDeposits'`,
`'CH-Tbl-DeclaredChecks'`, `'CH-Tbl-DepositsRecounting'`, `'CH-Tbl-CountedMoneyDetails'`,
`'CH-Tbl-Checks'`, **`'CH-Tbl--MoneyDenomination'`** (🧨 **DOS guiones** — verificado en
`Entities/sanic_CHTblMoneyDenomination/Entity.xml`, el `displayname` del atributo primario
es `CH-Tbl--MoneyDenomination` aunque la tabla se llame `CH-Tbl-MoneyDenomination`),
`'CH-Tbl-XMLReadingDetail'`, `'CH-Tbl-ApprovalRequest'`, `User`.

#### 🧨 D-10 en detalle: qué se puede cachear en esta app

| Catálogo | Filas | Dónde vive |
|---|---|---|
| `CH-Tbl-MoneyDenominations` | ~20 (3 monedas × billetes/monedas), acotado por el negocio | ✅ colección `colDenominacionesCon` |
| `CH-Tbl-CountingDesks` | decenas, acotado por el edificio | ✅ colección `colMesasSes` |
| Tulas de la sesión / depósitos de la tula / cheques del depósito | acotados por la **jornada de un cajero** (decenas) | ✅ colecciones locales |
| `CH-Tbl-Bags` · `CH-Tbl-Checks` · `CH-Tbl-DeclaredChecks` completas | **13k+ y creciendo** | 🚫 jamás en colección. Se consultan siempre **filtradas y delegables** |

#### 🧨 D-5 en detalle: el `Refresh` prohibido y las DOS formas permitidas

| # | Forma | Dónde | Por qué está acotada |
|---|---|---|---|
| 1 | **El botón ⟳** — `Refresh('CH-Tbl-Bags')` + recarga de `colTulasSesionTul` | `btnActualizarTul.OnSelect` (`03`) | Es una **acción del usuario**: pide explícitamente traer lo que cambiaron otros (típicamente el supervisor) |
| 2 | **Un `Refresh('CH-Tbl-Bags')` puntual** al detectar la resolución de una solicitud en el polling | receta 7.7, rama *Aprobado* y rama *Rechazado* | El servidor **acaba de cambiar** el estado de la bolsa. Sin ese refresh la app pinta un estado que ya no existe. **Solo esa tabla** |

> Todo lo demás se reconstruye con lo que devolvió el `Patch` o con UNA consulta delegable
> (receta 7.5). Los **dos timers de 5 s con `Refresh` en cascada** de la v1 (uno con
> `Start: =true` literal, corriendo siempre) **no existen** en la v2: hay **un** timer de
> polling por pantalla, a 15 s, y se apaga solo (receta 7.7).

### 2.4 Columnas que la app lee y las que escribe

Leyenda: **L** = la app la lee · **E** = la app la escribe · 🚫 = la app **no la toca**.

**`CH-Tbl-RecountingSessions`** (PK `'CH-Tbl-RecountingSession'`)

| Columna (display) | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `RCS-{SEQNUM:4}` | 🚫 la genera Dataverse |
| `Cashier` | Lookup → `Users`, **requerido** | **E** `gvarUser` al crear la sesión |
| `CountingDesk` | Lookup → `CH-Tbl-CountingDesks`, **requerido** | **E** la mesa elegida |
| `Confirmed` | Sí/No | 🚫 **NUNCA se escribe** — la decide el plugin de auto-aprobación (`00` §2.2, punto 1). Mata **S-1** |
| `ConfirmedBy` / `ForcedClosureBy` / `ReasonForForcedFlosure` | — | 🚫 |
| `SessionForciblyClosed` | Sí/No | 🚫 **la app NUNCA la escribe** · **L** — 🧨 **la LECTURA está permitida y es obligatoria**: las dos consultas de sesión de `02` (la del cajero y la del catálogo de mesas) llevan `SessionForciblyClosed = false`. Sin ese predicado, una sesión cerrada a la fuerza por el supervisor **atrapa** al cajero: el `OnVisible` la encuentra, la da por viva y navega a una sesión muerta. La v1 ya la leía en el mismo lugar |
| `'Created On'` | Sistema | **L** para acotar la sesión al día de hoy |

**`CH-Tbl-RecountingSessionDetails`** (PK `'CH-Tbl-RecountingSessionDetail'`)

| Columna (display) | Tipo | La app |
|---|---|---|
| `RecountingSessionDetailId` | **AUTONUMBER** `{SEQNUM:4}` — ⚠️ **es la columna primaria y NO se llama `Name`** | 🚫 |
| `RecountingSessionId` | Lookup → sesión, **requerido** | **E** |
| `BagId` | Lookup → tula, **requerido** | **E** |
| `RecountingSessionDetailStatus` | Picklist **global** `ch-recountingDetailsStatus` (§5.3) | **E** `En progreso` al abrir la tula; `Cerrado` / `Cerrado con diferencias` al terminarla |
| `SupervisorId` | Lookup → `Users` | 🚫 lo escribe el Supervisor |

> 🧨 **Se crea SIEMPRE al abrir una tula, en las CUATRO rutas** (recibida+declarada,
> recibida sin declaración, homónima desambiguada, tula ya abierta que se retoma). La v1
> lo creaba en **1 de 4** — y sin ese registro la contadora no tiene `ID_Sesion`, así que
> la columna **Auto** queda vacía y el cajero recuenta a ciegas. Es el contrato de entrada
> de `04`.

**`CH-Tbl-CountingDesks`** (PK `'CH-Tbl-CountingDesk'`) — **solo lectura**

`Name` · `Cajero` (lookup → `Users`, requerido) · `MachineSerialNumber` (texto,
requerido — es el `Serial_Maquina` del flujo) · `Enable` (Sí/No).

**`CH-Tbl-Bags`** (PK `'CH-Tbl-Bags'`)

| Columna (display) | Tipo | La app |
|---|---|---|
| `BagID` | **Texto** (50), requerido — ⚠️ **NO es un lookup** en esta tabla | **L** N° de tula (scan) |
| `Name` | Requerido | 🚫 |
| `BagStatus` | Picklist **local** (§5.1) | **E** `Asignada a cajero` al abrir · `Recontada` o `Pendiente de revision por supervisor - recuento` al terminar |
| `RecountingSession` | Lookup → sesión | **E** al abrir la tula |
| `BagDeclarationID` | Lookup → `CH-Tbl-DeclaredBags` | **E** al enlazar la declaración; queda en blanco en la ruta "sin declaración" |
| `ReceptionID` | Lookup → planilla, requerido | **L** (planilla y cliente) |
| `ReceiptIdMirror` | Texto | **L** N° de planilla sin navegar el lookup |
| `ReceptionBatchId` | Lookup → lote | **L** solo para el N° de lote del diálogo `B1` |
| `TotalNIO` / `TotalUSD` / `TotalEUR` | Decimal | 🚫 **NO se tocan** — los recalcula el plugin al transicionar `BagStatus` (`00` §2.2, punto 2). Mata **S-2a**. **L** para los KPIs del rail |
| `DeclTotalNIO` / `DeclTotalUSD` / `DeclTotalEUR` | Decimal | 🚫 · **L** |
| `HasDifference` | Sí/No | 🚫 lo escribe el plugin · **L** |

**`CH-Tbl-Receptions`** — **solo lectura**: `ReceiptID`, `RemitentName`, `BranchName`.

**`CH-Tbl-DeclaredBags`** — **casi** solo lectura

`BagNumber` (texto 50, requerido) · `TotalNIO` / `TotalUSD` / `TotalEUR` (decimal) ·
`TotalDeposits` (**rollup**, solo lectura) · `DeclarationID` · `IsRelatedToBag` (**campo
fórmula**, solo lectura) · `BagID` (lookup → `CH-Tbl-Bags`) ⇒ **E**: es la **única**
columna que la app escribe acá, al enlazar la tula con su declaración.

**`CH-Tbl-DeclaredDeposits`** — **solo lectura salvo una columna**

`AccountNumber` (**int**, 1–999999999, *field-level security*) · `Currency` (picklist
global) · `CashAmount`, `BACLocalCheckAmount`, `OtherLocalCheckAmount` (decimal) · `Total`
(**campo fórmula**, solo lectura) · `TransactionNo` (int 1–500) · `DepositReference` (texto
31, *FLS*) · `DeclaredBag` (lookup) · `RelatedRecountedDeposit` (lookup →
`CH-Tbl-DepositsRecounting`) ⇒ **E**: se escribe al **enlazar/desenlazar** un depósito
recontado con uno declarado.

> ⚠️ **`OtherLocalCheckAmount` en DeclaredDeposits vs. `OtherLocaCheckAmount` en
> DepositsRecountings.** No es un typo de esta guía: **son dos nombres distintos en el
> modelo** (`sanic_otherlocalcheckamount` y `sanic_otherlocacheckamount`, sin la `l`).
> Copiar el nombre de una tabla en la otra **no compila**.

**`CH-Tbl-DeclaredChecks`** — **casi** solo lectura

`CheckNumber` (texto 30, *FLS*) · `CheckAmount` (decimal, **mín. 1**) · `CheckCurrency`
(picklist global) · `CheckType` (picklist local) · `DeclaredDepositID` (lookup, requerido)
· `Declaration` (lookup) · `DeclaredBagMirror` (**espejo E-3**) ⇒ **E**: `MatchedCheck`
(lookup → `CH-Tbl-Checks`) al tildar/destildar un cheque declarado.

> 🧨 **`MatchedCheck`, NUNCA `DeclaredDepositID`.** El bug **D-6** de la v1 era
> `Patch('CH-Tbl-DeclaredChecks', …, {DeclaredDepositID: Blank()})` al destildar: limpiaba
> el **lookup padre** en vez del enlace, y el cheque declarado quedaba huérfano de su
> declaración **para siempre**. `DeclaredDepositID` es **requerido** y la app **jamás** lo
> escribe. La columna del enlace es `MatchedCheck`, cuya descripción en el modelo dice
> literalmente *"Mantenido por plugin. No editar a mano."* — la app la escribe **solo**
> para reflejar el tilde del cajero, y el plugin es la autoridad final.

**`CH-Tbl-DepositsRecountings`** (PK `'CH-Tbl-DepositsRecounting'`)

| Columna (display) | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `DR-{SEQNUM:8}` | 🚫 |
| `BagID` | Lookup → tula, **requerido** | **E** |
| `Currency` | Picklist **global** `CH-Ch-Currency` | **E** |
| `AccountNumber` | **int**, 1–999999999 (*FLS*) | **E** `Value(inpCuentaCon.Text)` — ver 🧨 abajo |
| `CashAmount` | Decimal | **E** billetes + monedas del depósito (manual **+ auto**) |
| `BACLocalCheckAmount` / `OtherLocaCheckAmount` | Decimal | **E** suma de los cheques del depósito por tipo |
| `TransactionNumber` | int 1–500 | **E** |
| `RelatedDeclaredDeposit` | Lookup → declarado | **E** al enlazar / `Blank()` al desenlazar |
| `Total` | **Campo fórmula** | 🚫 solo lectura |
| `RecountingSessionMirror` | **Espejo E-1** | 🚫 solo lectura |

> 🧨 **`AccountNumber` es un ENTERO de hasta 9 dígitos, no un texto con guiones.** El
> mockup dibuja cuentas como `100-501-2345` (10 dígitos + guiones): **el modelo no las
> admite** (`int`, máx. `999999999`). La entrada de `04` es una **Entrada de texto clásica
> con `Format: TextFormat.Number`** y el `Patch` escribe `Value(inpCuentaCon.Text)`; el
> display es `Text(D.AccountNumber)` sin máscara. Divergencia **`L-4`**.

**`CH-Tbl-CountedMoneyDetails`**

| Columna (display) | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `CMD-{SEQNUM:12}` | 🚫 |
| `RelatedDepositRecountingID` | Lookup → depósito, **requerido** | **E** |
| `Denomination` | Decimal, **requerido** | **E** el valor de la denominación |
| `DenominationType` | **Texto** (50) | **E** `"Billete"` / `"Moneda"` — ⚠️ es texto libre, **no** el option set de `MoneyDenominations` |
| `Quantity` | **int, mínimo 1** | **E** `manual + auto` |
| `TotalAmount` | **Campo fórmula** | 🚫 |

> 🧨 **`Quantity` tiene mínimo 1: una fila con 0 NO se puede guardar.** Por eso el guardado
> del depósito hace **dos** cosas (receta 7.9, paso 4): `Patch` de las filas con
> `manual + auto > 0`, y **`Remove` explícito** de las filas que existían y ahora quedaron
> en 0. Si solo se patchea, la denominación vieja sobrevive con su cantidad anterior y el
> arqueo no cuadra.

> 🧨 **La cantidad persistida es `manual + auto`** (decisión de `00` §2.6). La v1 sumaba lo
> automático a `CashAmount` pero **solo persistía la parte manual** en
> `CountedMoneyDetails` (bug **D-5**): el desglose nunca cuadraba con el monto y el arqueo
> era irreconstruible.

**`CH-Tbl-Checks`**

| Columna (display) | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `CK-{SEQNUM:8}` | 🚫 |
| `CheckNumber` | Texto (30), **requerido** | **E** |
| `CheckAmount` | Decimal, **mínimo 1**, requerido | **E** |
| `CheckCurrency` | Picklist **global**, requerido | **E** |
| `CheckTypes` | Picklist **local**, requerido — ⚠️ **`CheckTypes` en plural** acá; en `DeclaredChecks` es `CheckType` en singular | **E** |
| `CheckStatus` | Picklist **local** (§5.4) | **E** `Recontado` al terminar la tula |
| `RelatedReCounterDepositID` | Lookup → depósito recontado | **E** |
| `RelatedDeclaredCheck` | Lookup → cheque declarado | **E** (blanco = cheque **manual**) |
| `Bag` (**espejo E-2**) · `RecountingSession` · `'Recibido el'` · `'No recibido el'` · `'Enviado el'` | Lookups y fechas *"Mantenido por plugin"* | 🚫 solo lectura |

**`CH-Tbl-MoneyDenominations`** — **solo lectura**: `Value` (decimal), `Currency`
(picklist global), `Type` (picklist local: `Billete` / `Moneda`), `Enabled` (Sí/No),
`Name`.

**`CH-Tbl-XMLReadingDetails`** — **solo lectura**

`XMLReadingDetailId` (**AUTONUMBER**, es la primaria y **no** se llama `Name`) ·
`Denomination` (decimal) · `Quantity` (int) · `Currency` (**texto libre**, no picklist) ·
`Subtotal` (**campo fórmula**) · `XMLReading` (lookup) · `RecountingSessionDetailMirror` +
`XMLReadingActiva` (**espejo E-4**).

**`CH-Tbl-ApprovalRequests`**

| Columna (display) | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `AR-{SEQNUM:8}` | 🚫 |
| `RequestType` | **Texto libre** (100), requerido | **E** uno de los 4 strings EXACTOS de §5.6 |
| `RequestDetails` | `ntext(10000)` | **E** el HTML de §3.1 / receta 7.6 |
| `RequestResources` | `nvarchar(2000)` | **E** el **JSON** de claves exactas (§5.7), **siempre con `JSON()`** |
| `BagNumber` | Texto (100) | **E** el N° de tula, para que el Supervisor filtre sin parsear el JSON |
| `ApprovalStatus` | Picklist local, solo `Aprobado`/`Rechazado` | 🚫 **la app NUNCA lo setea** — en blanco = Pendiente. **L** en el polling (§5.8). Mata **S-1** |
| `StatusReason` | Texto (200) | 🚫 **L**: es el **motivo del rechazo** que se muestra en `A3` y `C8` |
| `ResolutionDatetime` | Fecha y hora | 🚫 · **L** (cuándo se resolvió) |
| `SolvedBy` | 🧨 **Lookup → `Users` (`SystemUser`)** — verificado en `Entity.xml` (`sanic_SolvedBy`, `Type: lookup`) y en `Other/Relationships/SystemUser.xml` (`sanic_chtblapprovalrequest_SolvedBy_systemuser`). **NO es texto** | 🚫 · **L** — el nombre del supervisor se lee **`gvarSolicitud.SolvedBy.'Full Name'`** (dot-walk de un salto sobre un registro **ya cargado**, en el `Text` de una etiqueta: no es un `Filter`/`LookUp` sobre datasource y por eso no rompe **D-1**). En blanco mientras la solicitud está pendiente ⇒ **siempre con `Coalesce`** |

> 🧨 **La v1 escribía `ApprovalStatus: Aprobado` desde el canvas** cuando la mesa era la
> propia. Ese es el bypass **S-1**: con `gvarUser` en blanco,
> `LookUp(CountingDesks, Cajero.User = Blank())` devolvía la primera mesa sin cajero y
> cualquiera entraba auto-aprobado. En la v2 **la app no escribe `ApprovalStatus` nunca**:
> el auto-approve por mesa propia lo decide el plugin comparando `CountingDesk.Cajero`
> contra el `InitiatingUserId` del contexto de ejecución.

**`Users`** — **solo lectura**: `'Primary Email'`, `'Full Name'`, `User` (PK). Un solo
`LookUp` en `App.OnStart` y ninguna consulta más.

### 2.5 Vistas usadas

**Ninguna.** Todas las consultas de esta app llevan predicado propio y `SortByColumns`
explícito (regla **D-9**), así que una vista no puede ser su origen.

🧨 **Recordatorio, por si alguien la agrega**: una vista **NO puede ser el PRIMER argumento
de `Filter`**. `Filter('X (Views)'.'la vista', <predicado>)` **no compila** — la vista va
como **segundo** argumento (`Filter(tabla, vista)`), forma que no admite predicado.

---

## 3. Propiedades de la App

### 3.1 `App.Formulas` — pegar TODO esto

Seleccioná **App** en el árbol → propiedad **Formulas**. Todo lo compartido vive acá: se
evalúa perezoso, existe en tiempo de diseño (no hace falta correr `OnStart` para ver los
colores) y hay **un solo lugar** donde vive el HTML de approval.

> 🧨 **Nota F — fallback si tu Studio no tiene UDFs.** Las funciones con paréntesis y
> parámetros (`HtmlEscape(t)`, `fxMonto(v, s)`, `fx…`) son **funciones definidas por el
> usuario** y necesitan la bandera de §1.4. Si no la tenés: los **tokens y las fórmulas
> con nombre sin parámetros** (paleta, formatos, `HtmlApEstilo`, `HtmlApTh…`) se pegan
> igual; cada `fx…` se copia **inline** en el `OnSelect` que la usa, reemplazando cada
> parámetro por su valor con un `With({…}, …)`. La expresión no cambia ni una coma; sí se
> duplica en los 3 lugares que arman HTML (`02` §-solicitud de mesa, `03` §-tula sin
> declaración, `04` §-terminar tula). **Probalo apenas pegues las fórmulas**, antes de
> construir pantallas.

```powerfx
// ===================== PALETA (RGBA exactos del mockup :root) =====================
// Idéntica a CH-App-Reception v2: es el mismo sistema de diseño.
ThemeAccent      = RGBA(200, 16, 46, 1);      // #C8102E
ThemeAccentDark  = RGBA(164, 13, 38, 1);      // #A40D26 — hover de boton primario
ThemeAccentSoft  = RGBA(251, 235, 237, 1);    // #FBEBED
ThemeBg          = RGBA(246, 245, 244, 1);    // #F6F5F4
ThemeSurface     = RGBA(255, 255, 255, 1);    // #FFFFFF
ThemeSurface2    = RGBA(250, 248, 247, 1);    // #FAF8F7
ThemeInk         = RGBA(32, 28, 29, 1);       // #201C1D
ThemeInk2        = RGBA(102, 89, 92, 1);      // #66595C
ThemeInk3        = RGBA(148, 138, 140, 1);    // #948A8C
ThemeLine        = RGBA(226, 220, 221, 1);    // #E2DCDD
ThemeLineSoft    = RGBA(237, 232, 233, 1);    // #EDE8E9
ThemeOk          = RGBA(14, 122, 60, 1);      // #0E7A3C
ThemeOkSoft      = RGBA(226, 242, 232, 1);    // #E2F2E8
ThemeInfo        = RGBA(35, 85, 125, 1);      // #23557D
ThemeInfoSoft    = RGBA(228, 238, 246, 1);    // #E4EEF6
ThemeWarn        = RGBA(154, 91, 0, 1);       // #9A5B00
ThemeWarnSoft    = RGBA(251, 240, 220, 1);    // #FBF0DC
ThemeWarnSoft2   = RGBA(246, 227, 192, 1);    // #F6E3C0 — chip "Manual" sobre fila ambar
ThemeDanger      = RGBA(179, 38, 30, 1);      // #B3261E
ThemeDangerSoft  = RGBA(252, 234, 232, 1);    // #FCEAE8
ThemeDangerLine  = RGBA(231, 185, 181, 1);    // #E7B9B5 — borde de boton chico peligroso
ThemeOverlay     = RGBA(40, 30, 32, 0.45);    // rgba(40,30,32,.45) — velo de los modales

// ===================== FORMATOS =====================
// El '0' fuerza el entero. "#,###" imprime cadena VACIA para el 0 y el monto sale ".00".
// El plan y el mockup escriben "#,###.00": divergencia L-2, declarada.
FmtMoney       = "#,##0.00";
FmtFecha       = "dd/mm/yyyy HH:mm";   // HH = 24 h. hh seria 12 h SIN AM/PM
FmtFechaCorta  = "dd/mm/yyyy";
FmtHora        = "HH:mm";
SimboloNIO = "C$ ";  SimboloUSD = "U$ ";  SimboloEUR = "€ ";

// ===================== IDENTIDAD =====================
UsuarioNombre = User().FullName;
UsuarioIniciales =
    Left(User().FullName, 1) &
    If(
        CountRows(Split(User().FullName, " ")) > 1,
        Left(Last(Split(User().FullName, " ")).Value, 1),
        ""
    );
// Si IntelliSense nombra la columna de Split() distinto de 'Value' (algunas versiones la
// llaman 'Result'), EL SUYO MANDA.

// ===================== FORMATO DE MONEDA Y DIFERENCIAS =====================
// El codigo de moneda llega como TEXTO ("NIO"/"USD"/"EUR"): es la etiqueta exacta del
// option set global CH-Ch-Currency, y se obtiene con Text(<registro>.Currency).
fxSimbolo(codigo: Text): Text =
    Switch(codigo, "NIO", SimboloNIO, "USD", SimboloUSD, "EUR", SimboloEUR, "");

// Signo ANTES del simbolo, como el mockup:  −C$ 17,500.00
fxMonto(v: Number, simbolo: Text): Text =
    If(v < 0, "-", "") & simbolo & Text(Abs(v), FmtMoney);

// Diferencia: el signo + tambien se muestra.
fxMontoDif(v: Number, simbolo: Text): Text =
    If(v > 0, "+", If(v < 0, "-", "")) & simbolo & Text(Abs(v), FmtMoney);

fxCantDif(n: Number): Text = If(n > 0, "+", "") & Text(n);

fxColorDif(v: Number): Color = If(v = 0, ThemeOk, ThemeDanger);

// ===================== ESCAPE HTML — OBLIGATORIO EN TODO DATO VARIABLE =====================
// El & SIEMPRE primero: si escapas < antes que &, el & de &lt; se vuelve &amp;lt;.
// Se escapan los CINCO: & < > " '  — esta app usa comillas SIMPLES en los atributos
// HTML, asi que el apostrofo tambien puede romper el markup. Mata S-3.
HtmlEscape(t: Text): Text =
    Substitute(
        Substitute(
            Substitute(
                Substitute(
                    Substitute(t, "&", "&amp;"),
                    "<", "&lt;"
                ),
                ">", "&gt;"
            ),
            """", "&quot;"
        ),
        "'", "&#39;"
    );

// ===================== HTML DE APPROVAL v2 — ESTILO =====================
// Literal SIN interpolacion: el $"..." exige duplicar TODA llave de CSS ({{ }}).
// Sin border-radius ni sombras: tiene que sobrevivir al render de Outlook/Approvals.
HtmlApEstilo =
"<style>
body{margin:0;padding:0;background:#E9E5E4}
.mail{width:640px;background:#FFFFFF;border:1px solid #D9D3D4;font-family:'Segoe UI',Arial,Helvetica,sans-serif;color:#201C1D}
.mailH{padding:17px 22px 15px;border-bottom:3px solid #C8102E}
.mailH .tag{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.9px;color:#C8102E;background:#FBEBED;padding:4px 10px;text-transform:uppercase}
.mailH h3{font-size:16px;margin:9px 0 0}
.mailH small{color:#948A8C;font-size:11.5px;display:block;margin-top:3px}
.ms{padding:15px 22px 3px}
.ms h4{font-size:11px;text-transform:uppercase;letter-spacing:.7px;color:#C8102E;font-weight:700;padding-bottom:6px;border-bottom:1px solid #E2DCDD;margin:0}
.ms p.note{font-size:12px;color:#66595C;line-height:1.55;margin:0}
table.mt{width:100%;border-collapse:collapse;font-size:11.5px;margin-top:9px}
table.mt th{background:#FAF8F7;text-align:left;font-size:9.5px;text-transform:uppercase;letter-spacing:.5px;color:#66595C;padding:6px 9px;border:1px solid #E2DCDD;font-weight:700}
table.mt th.r{text-align:right}
table.mt td{padding:6px 9px;border:1px solid #EDE8E9;vertical-align:top}
table.mt tr.zeb td{background:#FAF8F7}
table.mt tr.sub td{background:#F2EEED;font-weight:700}
table.mt td.n{text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums}
table.mt td.cur{font-weight:700}
table.mt td.bad{color:#B3261E}
table.mt td.ok{color:#0E7A3C}
table.kvt{width:100%;border-collapse:collapse;font-size:11.5px;margin-top:9px}
table.kvt td{padding:5px 9px;border-bottom:1px solid #EDE8E9}
table.kvt td.k{color:#66595C;width:34%;background:#FAF8F7;border-right:1px solid #EDE8E9}
.mailFoot{padding:12px 22px 15px;font-size:10.5px;color:#948A8C;line-height:1.6;border-top:1px solid #E2DCDD}
</style>";

// ===================== HTML DE APPROVAL v2 — ENCABEZADOS DE TABLA =====================
// Atributos HTML con comillas SIMPLES en TODA la plantilla: en Power Fx una comilla doble
// dentro de un literal hay que duplicarla ("") y el template se vuelve ilegible.
// Por eso HtmlEscape() tambien escapa el apostrofo (L-5).
HtmlApThGlobal =
"<tr><th>Moneda</th><th>Concepto</th><th class='r'>Cheques</th><th class='r'>Monto cheques</th><th class='r'>Efectivo</th></tr>";

HtmlApThTrx =
"<tr><th>Cuenta</th><th>Moneda</th><th class='r'>Dif. cheques</th><th class='r'>Dif. efectivo</th><th class='r'>Dif. total</th></tr>";

HtmlApThDeposito =
"<tr><th>Cuenta</th><th>Moneda</th><th class='r'>Cheques</th><th class='r'>Efectivo</th><th class='r'>Total</th></tr>";

HtmlApThChequeDec =
"<tr><th>N&deg; de cheque</th><th>Tipo</th><th>Moneda</th><th class='r'>Monto</th><th>Dep&oacute;sito declarado</th></tr>";

HtmlApThChequeMan =
"<tr><th>N&deg; de cheque</th><th>Tipo</th><th>Moneda</th><th class='r'>Monto</th><th>Dep&oacute;sito recontado</th></tr>";

// ===================== HTML DE APPROVAL v2 — PIEZAS =====================
// UN SOLO </body></html>, en fxHtmlDoc y en ningun otro lado. La v1 lo emitia dos veces.
fxHtmlDoc(cuerpo: Text): Text =
    "<html lang='es'><head><meta charset='UTF-8'><title>CashHub - Reconteo</title>" &
    HtmlApEstilo &
    "</head><body><div class='mail'>" & cuerpo & "</div></body></html>";

fxHtmlHead(etiqueta: Text, titulo: Text, subtitulo: Text): Text =
    "<div class='mailH'><span class='tag'>" & HtmlEscape(etiqueta) & "</span>" &
    "<h3>" & HtmlEscape(titulo) & "</h3>" &
    "<small>" & HtmlEscape(subtitulo) & "</small></div>";

// SECCION CONDICIONAL: si no hay contenido, NO se emite nada. Esto es lo que implementa
// "cada seccion se emite solo si tiene filas" sin un If por seccion en la pantalla.
fxHtmlSeccion(titulo: Text, contenido: Text): Text =
    If(
        IsBlank(contenido),
        "",
        "<div class='ms'><h4>" & HtmlEscape(titulo) & "</h4>" & contenido & "</div>"
    );

fxHtmlTabla(encabezado: Text, filas: Text): Text =
    If(IsBlank(filas), "", "<table class='mt'>" & encabezado & filas & "</table>");

fxHtmlKvt(filas: Text): Text = "<table class='kvt'>" & filas & "</table>";

fxHtmlKv(k: Text, v: Text): Text =
    "<tr><td class='k'>" & HtmlEscape(k) & "</td><td>" & HtmlEscape(v) & "</td></tr>";

// El pie acepta una NOTA opcional (segundo renglon). El mockup la usa en la solicitud de
// mesa; los otros dos documentos pasan "" y el pie sale de un solo renglon.
fxHtmlPie(usuario: Text, nota: Text): Text =
    "<div class='mailFoot'>Solicitud generada por <b>CH-App-Recounting</b> &middot; " &
    HtmlEscape(usuario) & " &middot; " & Text(Now(), FmtFecha) &
    If(IsBlank(nota), "", "<br>" & HtmlEscape(nota)) & "</div>";

// Clase de celda numerica segun la diferencia (verde 0 / rojo distinto de 0).
fxClaseDif(v: Number): Text = If(v = 0, "n ok", "n bad");

// ===================== HTML DE APPROVAL v2 — CONSTRUCTORES DE FILA =====================
// SECCION 1 — tres <tr> por moneda, con rowspan=3 en la celda de moneda.
// Las diferencias NO son parametros: se calculan aca. Una sola verdad.
fxHtmlFilaGlobal(
    moneda: Text, simbolo: Text,
    chqRec: Number, mtoRec: Number, efeRec: Number,
    chqDec: Number, mtoDec: Number, efeDec: Number
): Text =
    With(
        { dChq: chqRec - chqDec, dMto: mtoRec - mtoDec, dEfe: efeRec - efeDec },
        "<tr><td rowspan='3' class='cur'>" & HtmlEscape(moneda) & "</td>" &
            "<td>Recontado</td>" &
            "<td class='n'>" & Text(chqRec) & "</td>" &
            "<td class='n'>" & fxMonto(mtoRec, simbolo) & "</td>" &
            "<td class='n'>" & fxMonto(efeRec, simbolo) & "</td></tr>" &
        "<tr class='zeb'><td>Declarado</td>" &
            "<td class='n'>" & Text(chqDec) & "</td>" &
            "<td class='n'>" & fxMonto(mtoDec, simbolo) & "</td>" &
            "<td class='n'>" & fxMonto(efeDec, simbolo) & "</td></tr>" &
        "<tr class='sub'><td>Diferencia</td>" &
            "<td class='" & fxClaseDif(dChq) & "'>" & fxCantDif(dChq) & "</td>" &
            "<td class='" & fxClaseDif(dMto) & "'>" & fxMontoDif(dMto, simbolo) & "</td>" &
            "<td class='" & fxClaseDif(dEfe) & "'>" & fxMontoDif(dEfe, simbolo) & "</td></tr>"
    );

// SECCION 2 — diferencias por transaccion. La dif. total se calcula aca.
fxHtmlFilaTrx(
    cuenta: Text, moneda: Text, simbolo: Text,
    difChq: Number, difEfe: Number, zebra: Boolean
): Text =
    "<tr" & If(zebra, " class='zeb'", "") & ">" &
        "<td>" & HtmlEscape(cuenta) & "</td>" &
        "<td>" & HtmlEscape(moneda) & "</td>" &
        "<td class='" & fxClaseDif(difChq) & "'>" & fxMontoDif(difChq, simbolo) & "</td>" &
        "<td class='" & fxClaseDif(difEfe) & "'>" & fxMontoDif(difEfe, simbolo) & "</td>" &
        "<td class='" & fxClaseDif(difChq + difEfe) & "'>" & fxMontoDif(difChq + difEfe, simbolo) & "</td>" &
    "</tr>";

// SECCIONES 3 y 6 — depositos sin enlazar (recontados y declarados). El total se calcula aca.
fxHtmlFilaDeposito(
    cuenta: Text, moneda: Text, simbolo: Text,
    cheques: Number, efectivo: Number, zebra: Boolean
): Text =
    "<tr" & If(zebra, " class='zeb'", "") & ">" &
        "<td>" & HtmlEscape(cuenta) & "</td>" &
        "<td>" & HtmlEscape(moneda) & "</td>" &
        "<td class='n'>" & fxMonto(cheques, simbolo) & "</td>" &
        "<td class='n'>" & fxMonto(efectivo, simbolo) & "</td>" &
        "<td class='n'>" & fxMonto(cheques + efectivo, simbolo) & "</td>" &
    "</tr>";

// SECCIONES 4 y 5 — cheques (declarados sin enlazar / agregados manualmente).
fxHtmlFilaCheque(
    numero: Text, tipo: Text, moneda: Text, simbolo: Text,
    monto: Number, deposito: Text, zebra: Boolean
): Text =
    "<tr" & If(zebra, " class='zeb'", "") & ">" &
        "<td>" & HtmlEscape(numero) & "</td>" &
        "<td>" & HtmlEscape(tipo) & "</td>" &
        "<td>" & HtmlEscape(moneda) & "</td>" &
        "<td class='n'>" & fxMonto(monto, simbolo) & "</td>" &
        "<td>" & HtmlEscape(deposito) & "</td>" &
    "</tr>";

// SECCION 4 — fila de subtotal (solo esa seccion la lleva).
fxHtmlFilaTotalCheques(simbolo: Text, total: Number, cantidad: Number): Text =
    "<tr class='sub'><td colspan='3'>Total sin enlazar</td>" &
        "<td class='n'>" & fxMonto(total, simbolo) & "</td>" &
        "<td>" & Text(cantidad) & " cheques</td></tr>";

// ===================== HTML DE APPROVAL v2 — LOS TRES DOCUMENTOS =====================
// A) Revision de reconteo: "Tula con diferencia" y "Tula recontada sin declaracion".
//    sec1..sec6 son las FILAS ya concatenadas por la pantalla (receta 7.6, paso 3).
//    La seccion 1 nunca viene vacia (siempre son 3 monedas); las 2..6 sí.
fxHtmlRevision(
    etiqueta: Text, tula: Text, planilla: Text, cliente: Text,
    cajero: Text, mesa: Text, fechaSesion: Text,
    sec1: Text, sec2: Text, sec3: Text, sec4: Text, sec5: Text, sec6: Text
): Text =
    fxHtmlDoc(
        fxHtmlHead(etiqueta, "Tula " & tula & " — Planilla " & planilla, cliente) &
        fxHtmlSeccion(
            "Datos de la solicitud",
            fxHtmlKvt(
                fxHtmlKv("Cajero", cajero) &
                fxHtmlKv("Mesa de conteo", mesa) &
                fxHtmlKv("Sesión", fechaSesion) &
                fxHtmlKv("Fecha de la solicitud", Text(Now(), FmtFecha)) &
                fxHtmlKv("Planilla", planilla) &
                fxHtmlKv("Cliente", cliente)
            )
        ) &
        fxHtmlSeccion("1 · Recontado vs. declarado (global)", fxHtmlTabla(HtmlApThGlobal, sec1)) &
        fxHtmlSeccion("2 · Diferencias por transacción",      fxHtmlTabla(HtmlApThTrx, sec2)) &
        fxHtmlSeccion("3 · Depósitos recontados sin enlazar", fxHtmlTabla(HtmlApThDeposito, sec3)) &
        fxHtmlSeccion("4 · Cheques declarados sin enlazar",   fxHtmlTabla(HtmlApThChequeDec, sec4)) &
        fxHtmlSeccion("5 · Cheques agregados manualmente",    fxHtmlTabla(HtmlApThChequeMan, sec5)) &
        fxHtmlSeccion("6 · Depósitos declarados sin enlazar", fxHtmlTabla(HtmlApThDeposito, sec6)) &
        fxHtmlPie(cajero, "")
    );

// B) Solicitud de mesa: "Crear sesion de recuento".
fxHtmlMesa(cajero: Text, mesa: Text, fechaSesion: Text, hora: Text): Text =
    fxHtmlDoc(
        fxHtmlHead("Solicitud de mesa", "Apertura de sesión de reconteo", mesa) &
        "<div class='ms'><p class='note'>El cajero <b>" & HtmlEscape(cajero) &
            "</b> solicita abrir una sesión de conteo en la <b>" & HtmlEscape(mesa) &
            "</b> para el <b>" & HtmlEscape(fechaSesion) &
            "</b>. Al aprobar, la mesa queda asignada a este cajero por el resto del día.</p>" &
        fxHtmlKvt(
            fxHtmlKv("Cajero", cajero) &
            fxHtmlKv("Mesa solicitada", mesa) &
            fxHtmlKv("Fecha de la sesión", fechaSesion) &
            fxHtmlKv("Hora de la solicitud", hora)
        ) &
        "</div>" &
        fxHtmlPie(
            cajero,
            "Si la mesa ya pertenece al cajero, el servidor aprueba automáticamente y este correo no se emite."
        )
    );

// C) Tula sin declaracion: "Tula sin declaracion".
fxHtmlSinDeclaracion(
    tula: Text, planilla: Text, cliente: Text,
    cajero: Text, mesa: Text, fechaHora: Text
): Text =
    fxHtmlDoc(
        fxHtmlHead("Tula sin declaración", "Tula " & tula & " — Planilla " & planilla, cliente) &
        "<div class='ms'><p class='note'>La tula <b>" & HtmlEscape(tula) &
            "</b> no tiene declaración registrada del cliente. El cajero solicita " &
            "autorización para recontarla <b>sin comparación contra lo declarado</b>: " &
            "todas las diferencias globales quedarán sin base de contraste.</p>" &
        fxHtmlKvt(
            fxHtmlKv("Tula", tula) &
            fxHtmlKv("Planilla", planilla) &
            fxHtmlKv("Cliente", cliente) &
            fxHtmlKv("Cajero", cajero) &
            fxHtmlKv("Mesa", mesa) &
            fxHtmlKv("Fecha", fechaHora)
        ) &
        "</div>" &
        fxHtmlPie(cajero, "")
    );
```

**Reglas de las fórmulas de approval:**

1. **TODO dato variable pasa por `HtmlEscape()`** — y pasa **una sola vez**: el escape vive
   dentro de `fxHtmlKv`, `fxHtmlHead` y los `fxHtmlFila…`. Si además lo escapás afuera,
   el `&` de `&amp;` se vuelve a escapar y el supervisor ve basura. La inyección HTML de
   la v1 (**S-3**) estaba confirmada: `CheckNumber` es texto libre.
2. **Los números NO se escapan**: `Text(v, FmtMoney)` no puede producir markup.
2.b 🧨 **Tildes, `·` y `—`: carácter LITERAL, nunca entidad, en todo texto que pase por
   `HtmlEscape()`.** Los títulos de `fxHtmlSeccion`, el `titulo`/`subtitulo` de
   `fxHtmlHead`, las claves y valores de `fxHtmlKv` y la `nota` de `fxHtmlPie` **entran
   escapados**: un `&mdash;` ahí saldría impreso como `&amp;mdash;`. Por eso se escribe
   `"1 · Recontado vs. declarado (global)"` y `"Tula " & tula & " — Planilla " & planilla`
   con los caracteres UTF-8 reales (el documento declara `<meta charset='UTF-8'>`). Las
   **entidades** (`&deg;`, `&oacute;`, `&middot;`, `&mdash;`) quedan **solo** para el markup
   crudo que nunca pasa por `HtmlEscape()`: `HtmlApTh…` y los separadores de `fxHtmlPie`.
3. **Un solo `</body></html>`**, emitido por `fxHtmlDoc` y por nadie más.
4. **Los GUIDs NO viajan en el HTML**: van en `RequestResources` con `JSON()` (§5.7).
5. **Límite duro**: `RequestDetails` es `ntext(10000)` y `HtmlApEstilo` pesa ~1,6 k ⇒ las
   6 secciones tienen ~8,4 k. Con más de ~60 filas de detalle el HTML se trunca: la
   receta 7.6 (paso 5) **corta cada sección a las primeras 25 filas** y agrega una fila
   *"… y N más"*.

### 3.2 `App.OnStart` — mínimo, y con GUARDA DURA de identidad

```powerfx
// ===== 1. IDENTIDAD — lo PRIMERO, y es bloqueante =====
Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email));
Set(gvarIdentidadOk, !IsBlank(gvarUser) && !IsBlank(gvarUser.User));

// ===== 2. Semilla de ESQUEMA de las colecciones (Collect + Clear) =====
// Sin esto, las colecciones no existen en tiempo de diseno y toda referencia a sus
// columnas queda en rojo. Defaults(ds) da un registro TIPADO sin ninguna consulta.
//
// 🧨 REGLA: se siembran las 23 colecciones del inventario de §4.2, TODAS, sin
//    excepcion. No hay colecciones "que se llenan solas antes de leerse": la que se
//    lee en el Items de una galeria, en un Visible o en un IsEmpty() de una pantalla
//    a la que se puede entrar sin haber pasado por su poblador, queda en rojo en
//    diseno y en blanco en runtime. Si agregas una coleccion a §4.2, se siembra aca
//    EN EL MISMO COMMIT.

Collect(
    colDetalleConteoCon,
    {
        Denominacion: 0,
        Tipo: "",                                  // "Billete" | "Moneda"
        Auto: 0,
        Manual: 0,
        GUID: Defaults('CH-Tbl-CountedMoneyDetails').'CH-Tbl-CountedMoneyDetails'
    }
);
Clear(colDetalleConteoCon);

Collect(
    colChecksCon,
    {
        LocalId: "",
        GUID: Defaults('CH-Tbl-Checks').'CH-Tbl-Checks',
        DeclaradoGUID: Defaults('CH-Tbl-DeclaredChecks').'CH-Tbl-DeclaredChecks',
        Numero: "",
        Tipo: "",                                  // "BAC Local" | "Otros Local"
        Moneda: "",                                // "NIO" | "USD" | "EUR"
        Monto: 0,
        Seleccionado: false,
        EsManual: false
    }
);
Clear(colChecksCon);

Collect(colAutoContadoraCon, { Denominacion: 0, Cantidad: 0 });
Clear(colAutoContadoraCon);

Collect(colHallazgosCon, { Orden: 0, Texto: "" });
Clear(colHallazgosCon);

Collect(colTulasSesionTul, Defaults('CH-Tbl-Bags'));
Clear(colTulasSesionTul);

Collect(colTulasCandidatasTul, Defaults('CH-Tbl-Bags'));
Clear(colTulasCandidatasTul);

Collect(colDeclaradasCandidatasTul, Defaults('CH-Tbl-DeclaredBags'));
Clear(colDeclaradasCandidatasTul);

Collect(colDepositosSesionTul, Defaults('CH-Tbl-DepositsRecountings'));
Clear(colDepositosSesionTul);

Collect(colDepDeclaradosCon, Defaults('CH-Tbl-DeclaredDeposits'));
Clear(colDepDeclaradosCon);

Collect(colDepRecontadosCon, Defaults('CH-Tbl-DepositsRecountings'));
Clear(colDepRecontadosCon);

Collect(colChequesDeclaradosCon, Defaults('CH-Tbl-DeclaredChecks'));
Clear(colChequesDeclaradosCon);

Collect(colChecksTulaCon, Defaults('CH-Tbl-Checks'));
Clear(colChecksTulaCon);

// 🧨 colDetalleDVCon y colChecksDVCon son SNAPSHOTS de lo PERSISTIDO: llevan registros
//    de Dataverse, NO el esquema de colDetalleConteoCon / colChecksCon. Sembrarlas con
//    FirstN(colDetalleConteoCon, 0) las deja mal tipadas y el paso de bajas no compila.
Collect(colDetalleDVCon, Defaults('CH-Tbl-CountedMoneyDetails'));
Clear(colDetalleDVCon);

Collect(colChecksDVCon, Defaults('CH-Tbl-Checks'));
Clear(colChecksDVCon);

Collect(colDenominacionesCon, Defaults('CH-Tbl-MoneyDenominations'));
Clear(colDenominacionesCon);

Collect(colMesasSes, Defaults('CH-Tbl-CountingDesks'));
Clear(colMesasSes);

Collect(colFallidasCon, { Clave: "", Detalle: "", Mensaje: "" });
Clear(colFallidasCon);

// ----- las CINCO de hallazgos (§4.2). Todas llevan `Indice` (zebra del HTML) -----
Collect(colDifTrxCon, { Indice: 0, Cuenta: "", Moneda: "", DifCheques: 0, DifEfectivo: 0 });
Clear(colDifTrxCon);

Collect(colDepRecSinEnlazarCon, { Indice: 0, Cuenta: "", Moneda: "", Cheques: 0, Efectivo: 0 });
Clear(colDepRecSinEnlazarCon);

Collect(colDepDecSinEnlazarCon, { Indice: 0, Cuenta: "", Moneda: "", Cheques: 0, Efectivo: 0 });
Clear(colDepDecSinEnlazarCon);

Collect(colChqDecSinEnlazarCon, { Indice: 0, Numero: "", Tipo: "", Moneda: "", Monto: 0, Deposito: "" });
Clear(colChqDecSinEnlazarCon);

Collect(colChqManualCon, { Indice: 0, Numero: "", Tipo: "", Moneda: "", Monto: 0, Deposito: "" });
Clear(colChqManualCon);

// ----- auxiliar del write-back de GUIDs de cheques (§4.3, `04` §9.2 paso 3) -----
Collect(colKeysCon, { L: "", G: Defaults('CH-Tbl-Checks').'CH-Tbl-Checks' });
Clear(colKeysCon);

// ===== 3. Estado global inicial =====
Set(gvarSesion, Blank());
Set(gvarMesa, Blank());
Set(gvarSolicitud, Blank());
Set(gvarTula, Blank());
Set(gvarSesionDetalle, Blank());
Set(gvarDeclaredBag, Blank());
Set(gvarDepositoSinGuardar, false)
```

> 🧨 **GUARDA DURA: si `gvarIdentidadOk` es `false`, la app se BLOQUEA — no degrada.**
> Ese es el fix estructural del bug **S-1**: la v1 seguía andando con `gvarUser` en blanco
> y `LookUp('CH-Tbl-CountingDesks', Cajero.User = Blank())` le devolvía **la primera mesa
> sin cajero**, con lo cual cualquiera entraba auto-aprobado a la mesa de otro.
>
> Implementación (`02` la construye, es su primer bloque): `scrSesion` lleva un overlay
> `cntBloqueoSes` (**Contenedor de diseño manual**, insertado **ÚLTIMO** = tope del
> z-order) con `Visible: !gvarIdentidadOk`, que cubre 1366 × 768 con `Fill` `ThemeBg`,
> un `Icon.Blocked` en `ThemeDanger` y el texto *"No se pudo identificar su usuario en
> Dataverse. Cierre la aplicación y avise a soporte: sin identidad no se puede abrir una
> sesión de conteo."* **Sin botón de salida y sin `Navigate`.**
>
> Regla derivada, no negociable: **ninguna fórmula de `02`/`03`/`04` puede usar
> `gvarUser` sin que `gvarIdentidadOk` sea `true`.** Los `DisplayMode` de
> `btnSolicitarSes` y de todo lo que escriba Dataverse llevan `gvarIdentidadOk &&` como
> **primer** término de la conjunción.

> ⚠️ **Este `OnStart` lee UN solo datasource** (`Users`, un `LookUp` por PK de email). Los
> catálogos y los datos de trabajo se cargan en el `OnVisible` de cada pantalla, con una
> consulta delegable por vista. Es lo que hace que la app abra instantánea.

> 🧨 **PROHIBIDO `Set()` dentro de `ForAll`** (regla **D-12**). Este `OnStart` no tiene
> ningún `ForAll` y no se le agrega ninguno.

> Mientras diseñás, las colecciones no existen hasta que corras el `OnStart`: botón
> derecho sobre **App** en el árbol → **Ejecutar OnStart**, una vez por sesión de Studio.

> 🧨 **Las DOS únicas colecciones de la app que NO se siembran acá, y por qué.**
> `colMesasDVSes` y `colSesionesHoySes` son **locales de `02`** (declaradas en su propia
> tabla de §CONTRATO, no en §4.2 de esta guía): nacen y se leen **dentro del mismo
> `OnVisible`** de `scrSesion` (§1 bloque 1), en el orden en que están escritas, y ninguna
> propiedad de control las referencia. **Todo lo demás se siembra**: el inventario de §4.2
> y este bloque tienen que dar **el mismo número (23)**, y el checklist de §9 lo verifica.

### 3.3 `App.OnError` — la red final

```powerfx
Trace(
    $"[Recounting] {FirstError.Message} | Kind: {FirstError.Kind} | Origen: {FirstError.Source} | Obs: {FirstError.Observed}",
    TraceSeverity.Error
);
Notify(
    If(
        FirstError.Kind = ErrorKind.Validation || FirstError.Kind = ErrorKind.Conflict || FirstError.Kind = ErrorKind.Sync,
        FirstError.Message,   // mensaje de negocio del guard server-side, TAL CUAL
        "Ocurrió un error inesperado. El detalle quedó registrado para soporte."
    ),
    NotificationType.Error
)
```

`OnError` es la **red final**, no el manejo de errores. Regla del proyecto: **todo
`Patch`/`Remove` contra Dataverse va envuelto en `IfError` con decisión explícita**
(receta 7.5). Si `OnError` es el único que se entera de un fallo, es un bug de la guía.

> 🚫 **Cero `Notify` de debug.** La v1 dejó en producción un `Notify` con el GUID de la
> sesión y el **serial de la contadora** (**S-6**). Ningún `Notify` de esta app imprime
> GUIDs, seriales ni correos.

### 3.4 Otras propiedades de App

| Propiedad | Valor | Por qué |
|---|---|---|
| `BackEnabled` | `false` | Con el back del navegador habilitado el cajero sale de `scrConteo` salteándose la guarda del depósito sin guardar |
| `StartScreen` | `scrSesion` | Toda entrada pasa por la apertura/verificación de sesión. Si tu Studio no expone `StartScreen`, dejá `scrSesion` primera en el árbol de pantallas |
| `ConfirmExit` | `gvarDepositoSinGuardar` | Aviso del navegador al cerrar la pestaña con un depósito en curso |
| `ConfirmExitMessage` | `"Tenés un depósito en curso sin guardar. Si salís ahora se pierde el conteo."` | — |
| `Theme` | el de la solución (Open Sans) | §6 lo pisa control por control igual, para no depender de que el tema viaje |

---

## 4. Variables y colecciones

**Convención**: `gvarXxx` (globales, `Set`, cruzan pantallas), `ctxXxx` (locales,
`UpdateContext`, viven en una pantalla), `colXxx` (colecciones). Los tokens de paleta y las
`fx*` son **fórmulas con nombre / funciones**, no variables: no llevan prefijo.

### 4.1 Globales

| Nombre | Tipo | Quién la escribe | Quién la lee |
|---|---|---|---|
| `gvarUser` | Registro de `Users` (o `Blank()`) | `App.OnStart`, y **nadie más** | `Cashier` de la sesión, polling de aprobaciones, `'Full Name'` del HTML |
| `gvarIdentidadOk` | Booleano | `App.OnStart` | **guarda dura** (§3.2): overlay de bloqueo y primer término de todo `DisplayMode` que escriba |
| `gvarSesion` | Registro de `CH-Tbl-RecountingSessions` (o `Blank()`) | `02` al crear/encontrar la sesión de hoy | `03` (universo de tulas y KPIs), `04` (`RecountingSessionId` del detalle) |
| `gvarMesa` | Registro de `CH-Tbl-CountingDesks` | `02` | chip de mesa de `03`/`04`, `MachineSerialNumber` de la receta 7.11, HTML de approval |
| `gvarSolicitud` | Registro de `CH-Tbl-ApprovalRequests` (o `Blank()`) | quien crea la solicitud (receta 7.6) y el **polling** (receta 7.7), que lo re-lee por PK | `Start` del timer, estado de espera / rechazo, `StatusReason` del banner |
| `gvarTula` | Registro de `CH-Tbl-Bags` | `03` al abrir una tula | `04` (todo) |
| `gvarSesionDetalle` | Registro de `CH-Tbl-RecountingSessionDetails` | `03` al abrir la tula (**siempre**, las 4 rutas) | `04`: es el `ID_Sesion` del flujo y la llave de la lectura de contadora (espejo **E-4**) |
| `gvarDeclaredBag` | Registro de `CH-Tbl-DeclaredBags` (o `Blank()`) | `03` al resolver la declaración | `04`: universo de lo declarado. **`Blank()` = ruta "sin declaración"** |
| `gvarDepositoSinGuardar` | Booleano | `04` en cada edición (true) y tras guardar/descartar (false) | `App.ConfirmExit`, guarda de salida de `04` (receta 7.2, variante de 3 botones) |

### 4.2 Colecciones

| Nombre | Contenido | Quién la escribe | Quién la lee |
|---|---|---|---|
| `colMesasSes` | Mesas habilitadas sin sesión de hoy | `scrSesion.OnVisible` | `Items` de `ddMesaSes` |
| `colTulasSesionTul` | **Tulas de la sesión** (registros de `CH-Tbl-Bags`), ordenadas por `Modified On` desc | `scrTulas.OnVisible` y el ⟳ (regla D-5, forma 1) | galería de tulas, KPIs del rail, contadores del panel |
| `colTulasCandidatasTul` | Tulas cuyo `BagID` coincide con lo escaneado | `btnAbrirTul.OnSelect` | diálogo `B1` (desambiguación de tula recibida) |
| `colDeclaradasCandidatasTul` | `DeclaredBags` cuyo `BagNumber` coincide | `btnAbrirTul.OnSelect` | diálogo `B2` (desambiguación de declaración) |
| `colDepositosSesionTul` | **Depósitos recontados de toda la sesión** (🧨 espejo **E-1**) | `scrTulas.OnVisible` | los 3 `cmpKpiSesion` del rail: todas las sumas son **locales** |
| `colDepDeclaradosCon` | `DeclaredDeposits` de la tula | `scrConteo.OnVisible` | pestaña *Declarado*, matrices, validaciones |
| `colDepRecontadosCon` | `DepositsRecountings` de la tula | `scrConteo.OnVisible` y tras cada guardado de depósito | pestaña *Recontado*, matrices, validaciones |
| `colChequesDeclaradosCon` | `DeclaredChecks` de la tula (🧨 espejo **E-3**) | `scrConteo.OnVisible` | grilla de cheques, sección 4 del HTML |
| `colChecksTulaCon` | **Todos los cheques RECONTADOS de la tula** (🧨 espejo **E-2**) | `scrConteo.OnVisible` y tras cada guardado y cada borrado de depósito | cantidad y monto de cheques *Recontado* de las 3 matrices, hallazgo de cheques manuales, marcado a `Recontado` del cierre |
| `colDenominacionesCon` | **TODAS** las denominaciones con `Enabled = true` (~20 filas, cacheable por **D-10**); el filtro por moneda es **local** | `scrConteo.OnVisible`, **una sola vez por visita** | grilla de billetes y de monedas |
| `colAutoContadoraCon` | `{ Denominacion, Cantidad }` — lo que leyó la máquina, agregado por denominación | receta 7.11 · **`Clear()` al crear o cambiar de depósito** (§4.4) | columna **Auto** de la grilla |
| `colDetalleConteoCon` | `{ Denominacion, Tipo, Auto, Manual, GUID }` — una fila por denominación del depósito en curso | al crear/cargar un depósito, y el volcado de la receta 7.10.A | grilla, totales en vivo, `Patch` de `CountedMoneyDetails` |
| `colDetalleDVCon` | 🧨 **Snapshot de lo PERSISTIDO** del depósito en curso — registros de `CH-Tbl-CountedMoneyDetails` tal cual están en Dataverse | al cargar un depósito (bloque *cargar depósito en curso* de `04` §6.1); se **vacía** al abrir un depósito nuevo | receta **7.5**: re-keyado del `Patch` y **paso de bajas** |
| `colChecksCon` | `{ LocalId, GUID, DeclaradoGUID, Numero, Tipo, Moneda, Monto, Seleccionado, EsManual }` | al crear/cargar un depósito y en cada edición | grilla de cheques, totales de cheques, `Patch` de `Checks` |
| `colChecksDVCon` | 🧨 **Snapshot de lo PERSISTIDO** del depósito en curso — registros de `CH-Tbl-Checks`. Mismo ciclo de vida que `colDetalleDVCon` | ídem | receta **7.5**: re-keyado del `Patch` y **paso de bajas** de cheques |
| `colHallazgosCon` | `{ Orden, Texto }` — los banners del diálogo *Terminar tula* | `btnTerminarCon.OnSelect`, **antes** de abrir el diálogo | diálogo `C5`, y define si hay o no discrepancias |
| `colDifTrxCon` | Diferencias por transacción, con `Indice` | `btnTerminarCon.OnSelect` | hallazgo 2 + **sección 2** del HTML |
| `colDepRecSinEnlazarCon` | Depósitos recontados sin enlace, con `Indice` | ídem | hallazgo 4 + **sección 3** |
| `colChqDecSinEnlazarCon` | Cheques declarados sin enlazar, con `Indice` | ídem | hallazgo 3 + **sección 4** |
| `colChqManualCon` | Cheques manuales de la tula, con `Indice` | ídem | hallazgo 5 + **sección 5** |
| `colDepDecSinEnlazarCon` | Depósitos declarados sin enlace, con `Indice` | ídem | hallazgo 4 + **sección 6** |
| `colFallidasCon` | `{ Clave, Detalle, Mensaje }` — errores de un guardado (receta 7.5) | receta 7.5, fase B | panel de resultado y decisión de "no cerrar la tula" |
| `colKeysCon` | 🧨 **Auxiliar de write-back**: `{ L, G }` — el `LocalId` de cada cheque guardado y el `GUID` real que devolvió Dataverse | `04` §9.2 paso 3, **dentro** del `ForAll` que guarda cheques | el `ForAll` de write-back que corre **después** de ese bucle y escribe los `GUID` en `colChecksCon` (§4.3) |

> 🧨 **Las 23 filas de esta tabla se siembran en `App.OnStart` (§3.2), TODAS.** El
> inventario y las semillas son **el mismo conjunto**: si agregás una colección acá, se
> siembra allá en el mismo commit. Las únicas dos colecciones de la app que no están en esta
> tabla ni en la semilla son `colMesasDVSes` y `colSesionesHoySes`, **locales de `02`**
> (nacen y mueren dentro de un solo `OnVisible`; `02` §CONTRATO las declara).

> 🧨 **`colDetalleConteoCon` y `colChecksCon` son la ÚNICA fuente de los totales.** Jamás
> `Sum(gal….AllItems, …)` (regla **D-8**). La v1 sumaba desde `'gal Checks'.AllItems` con
> 5 PCF por fila: se instanciaban **todos** los componentes React de **todas** las filas,
> visibles o no, y la virtualización quedaba anulada.

> 🧨 **La `Indice` de las 5 colecciones de hallazgos no es adorno**: es lo que permite la
> zebra del HTML (`Mod(Indice, 2) = 1`). Se materializa al construir la colección, con
> `ForAll(Sequence(CountRows(<origen>)) As S, Patch(<col>, …, { Indice: S.Value }))` o
> agregando la columna en el mismo `ClearCollect`. Sin ella no hay alternancia y la tabla
> del correo se lee peor (receta 7.6, paso 3).

### 4.3 🧨 `LocalId`: la clave local de `colChecksCon` (D-9 muerto)

`colChecksCon` mezcla cheques **declarados** (que ya existen en Dataverse) con cheques
**manuales** (que nacen en la pantalla). La clave que los une es `LocalId`, y su valor
**no es libre**:

| Origen de la fila | `LocalId` | Por qué |
|---|---|---|
| Cheque **declarado** | **`Text(<GUID de la DeclaredCheck>)`** | es estable entre visitas: salir de la tula y volver no reasigna claves |
| Cheque **recontado ya persistido** | **`Text(<GUID de la Check>)`** | ídem |
| Cheque **manual nuevo** | **`Text(GUID())`** | aleatorio y local; al guardarse, el **paso de cheques del guardado** (`04` §9.2, paso 3 — receta **7.5**, forma B) le escribe el `GUID` real en la columna `GUID` (el `LocalId` **no** se reescribe). La forma exacta del write-back está abajo |

🧨 **El write-back del `GUID` NO se hace adentro del `ForAll` que recorre `colChecksCon`.**
Ese bucle itera `Filter(colChecksCon, Seleccionado)` y un `Patch(colChecksCon, …)` **dentro**
muta la misma colección que se está recorriendo: el comportamiento no está definido y, si el
motor materializa el filtro de forma perezosa, algunas filas se saltan **en silencio** —
justo la clase de bug que `D-9` costó. Se acumula en una colección auxiliar y se escribe
**después**:

```powerfx
// ===== dentro del ForAll que guarda cheques (04 §9.2, paso 3) =====
//   Clear(colKeysCon) va ANTES del ForAll.
Collect(colKeysCon, { L: C.LocalId, G: Guardado.'CH-Tbl-Checks' });

// ===== DESPUÉS del ForAll: el write-back, sobre una colección que ya nadie recorre =====
ForAll(
    colKeysCon As K,
    Patch(colChecksCon, LookUp(colChecksCon, LocalId = K.L), { GUID: K.G })
)
```

> `colKeysCon` está en el inventario de §4.2 y sembrada en §3.2. Se limpia con `Clear` al
> entrar al paso 3 y no sobrevive al guardado: es un acumulador, no un estado.

> 🚫 **`Max(col, OrderID) + 1` está PROHIBIDO.** Era el bug **D-9** de la v1: el `OrderID`
> se generaba dentro de un `ForAll`, así que **todas** las filas del lote recibían el
> mismo número ⇒ editar el monto de un cheque sobrescribía el de todos los que compartían
> `OrderID`, y tildar uno tildaba todos. `GUID()` **dentro de un `ForAll` también repite**:
> por eso los cheques manuales se agregan **de a uno**, con un `Collect` por clic del botón
> *Agregar cheque*, nunca en bucle.

### 4.4 🧨 `colAutoContadoraCon` se limpia POR DEPÓSITO (D-4 muerto)

En la v1 `colDenominationTotals` era **por sesión** y **nunca** se limpiaba (el `Reset` de
la columna Auto estaba comentado): el conteo de la máquina se heredaba en **cada** depósito
de la tula ⇒ efectivo duplicado o triplicado en `CashAmount` y en los totales de la bolsa.

**Regla de la v2, sin excepción**: `Clear(colAutoContadoraCon)` corre en **los cuatro**
puntos donde cambia el depósito en curso:

1. `btnNuevoDepositoCon.OnSelect` (crear un depósito nuevo)
2. `galDepRecontadosCon.OnSelect` (seleccionar otro depósito recontado)
3. el confirmar del diálogo *Cambiar la moneda del depósito* (`C6-1`)
4. el confirmar del diálogo *Descartar los cambios del depósito* (`C6-3`)

Y en el mismo bloque, la columna `Auto` de `colDetalleConteoCon` se recalcula desde
`colAutoContadoraCon` (que acaba de quedar vacía) ⇒ vuelve a `0`.

### 4.5 Contratos de entrada entre pantallas

**`scrSesion` → `scrTulas`** (`02` navega solo con esto seteado):

```powerfx
// Se ejecuta cuando la sesión de hoy YA existe y está confirmada por el servidor.
// 🧨 VIVE EN UN MOTOR 1×1 (`02` §1.1, `btnEntrarTulasSes`): un OnVisible NO puede
//    contener Navigate() — el App Checker lo marca "would automatically always
//    navigate away" (ledger L-65). El OnVisible invoca, el motor hace.
Set(gvarSesion, <registro de CH-Tbl-RecountingSessions de hoy>);
Set(gvarMesa,   gvarSesion.CountingDesk);
Set(gvarTula, Blank());
Set(gvarDeclaredBag, Blank());
Set(gvarSesionDetalle, Blank());
Set(gvarSolicitud, Blank());
Set(gvarDepositoSinGuardar, false);   // 🧨 App.ConfirmExit (§3.4) no se hereda entre sesiones
Navigate(scrTulas)
```

> `gvarMesa` se toma de `gvarSesion.CountingDesk` (navegación sobre un registro **ya
> cargado**: es la anotación del lookup, no cuesta un fetch), **nunca** de
> `LookUp('CH-Tbl-CountingDesks', Cajero.User = gvarUser.User)`. Ese `LookUp` es
> exactamente el bypass **S-1**: con `gvarUser` en blanco devuelve la primera mesa sin
> cajero.

**`scrTulas` → `scrConteo`** (`03` navega solo con esto seteado):

```powerfx
// Ruta TULA NUEVA: el registro viene FRESCO del Patch de apertura, en 'Asignada a cajero'.
// Ruta RETOME:     el registro viene releído por PK y puede estar en 'Recontada' o
//                  'Pendiente de revision por supervisor - recuento' ⇒ 04 abre en SOLO
//                  LECTURA. Es un estado de entrada VÁLIDO, no un error.
Set(gvarTula, <registro de CH-Tbl-Bags fresco>);
Set(gvarDeclaredBag, <registro de CH-Tbl-DeclaredBags | Blank() en la ruta sin declaración>);
Set(gvarSesionDetalle, <registro de CH-Tbl-RecountingSessionDetails, SIEMPRE presente>);
Set(gvarSolicitud, Blank());          // 🧨 ver la nota de abajo
Set(gvarDepositoSinGuardar, false);
Clear(colDetalleConteoCon); Clear(colChecksCon); Clear(colAutoContadoraCon);
Clear(colDepDeclaradosCon); Clear(colDepRecontadosCon); Clear(colChequesDeclaradosCon);
Clear(colChecksTulaCon);    Clear(colDetalleDVCon);    Clear(colChecksDVCon);
Clear(colHallazgosCon);
Notify("Tula " & gvarTula.BagID & " abierta.", NotificationType.Information);
Navigate(scrConteo)
```

> 🧨 **`Set(gvarSolicitud, Blank())` es parte del contrato.** `gvarSolicitud` es **un
> global para tres pantallas** y cada una tiene su propio timer (§5.8): si `03` no lo
> limpiara antes de navegar, el `tmrPollCon` de `04` adoptaría la solicitud de apertura
> que acaba de resolverse y abriría el overlay de espera sobre un conteo que nadie envió a
> revisión. La contraparte está en `03` §8.

> 🧨 **`gvarSesionDetalle` NUNCA puede venir en blanco.** Es la precondición dura de `04`:
> sin él no hay `ID_Sesion` para el flujo de la contadora y la columna **Auto** queda en
> cero sin aviso. Las **cuatro** rutas de apertura de `03` lo crean o lo recuperan; el
> `OnVisible` de `04` lo verifica y, si está en blanco, deshabilita *Leer contadora* y
> muestra el aviso *"Esta tula no tiene detalle de sesión: la lectura automática no está
> disponible."* — nunca sigue en silencio.

> 🧨 **Las DIEZ colecciones de `04` se vacían en `03`, ANTES de navegar.** Si no se vacían,
> el `OnVisible` de `scrConteo` encuentra las de la tula anterior y las da por buenas. El
> `OnVisible` las repuebla; los `Clear` son lo que **habilita** esa repoblación.
>
> **Las tres del segundo renglón no son opcionales.** `colChecksTulaCon` alimenta la
> cantidad y el monto de cheques *Recontado* de las **tres matrices** de la franja (`04` §4)
> y el pie por moneda (`04` §5.3): con la de la tula anterior adentro, el arqueo de la tula
> nueva arranca con cheques ajenos. `colDetalleDVCon` y `colChecksDVCon` son los **snapshots
> de lo persistido** de los que sale el **paso de bajas** del guardado (receta §7.5, regla 2
> — `04` §9.2 pasos 4 y 5): con filas de otro depósito adentro, el guardado intentaría
> `Remove` de registros que no le pertenecen.

---

## 5. Estados y contratos inter-app

### 5.1 `BagStatus` — option set **local** `'BagStatus (CH-Tbl-Bags)'`

| Literal Power Fx | Valor | Etiqueta en pantalla | Quién lo produce |
|---|---|---|---|
| `'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'` | **159460000** | `"Recibida en esclusa"` | Reception |
| `'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'` | **159460001** | `"Almacenada en bóveda"` | Reception. **Es el ÚNICO estado desde el que Recounting puede abrir una tula** |
| `'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'` | **159460002** | `"Asignada a cajero"` | **Recounting**, al abrir la tula |
| `'BagStatus (CH-Tbl-Bags)'.Recontada` | **159460003** | `"Recontada"` | **Recounting**, al terminar sin discrepancias |
| `'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento'` | **159460004** | `"Pendiente de revisión"` | **Recounting**, al terminar con discrepancias |
| `'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'` | **159460005** | `"Pendiente supervisor (esclusa)"` | Reception. Recounting **solo lo muestra** |

> Los literales van **sin tilde** (`'Almacenada en boveda'`, `'Pendiente de revision…'`)
> porque así están las etiquetas en Dataverse; el texto que se muestra en pantalla va
> **con tilde**. No es un descuido: es una traducción explícita en el `Switch` de la
> píldora (receta 7.4).

> 🧨 **`- recuento` vs. `- esclusa`.** Son dos estados distintos y Recounting escribe
> **solo el de recuento** (159460004). Escribir el de esclusa mandaría la tula a la cola
> equivocada del Supervisor.

### 5.2 `Currency` — option set **GLOBAL** `'CH-Ch-Currency'`

| Literal Power Fx | Valor | Símbolo (§3.1) |
|---|---|---|
| `'CH-Ch-Currency'.NIO` | **159460000** | `SimboloNIO` = `"C$ "` |
| `'CH-Ch-Currency'.USD` | **159460001** | `SimboloUSD` = `"U$ "` |
| `'CH-Ch-Currency'.EUR` | **159460002** | `SimboloEUR` = `"€ "` |

> 🧨 **Es GLOBAL: el literal NO es `'Currency (CH-Tbl-DepositsRecountings)'`.** Ese nombre
> es el de un option set *local* y **no compila**. Vale para las 5 tablas que lo usan
> (`DepositsRecountings`, `DeclaredDeposits`, `Checks.CheckCurrency`,
> `DeclaredChecks.CheckCurrency`, `MoneyDenominations`).
>
> `Text(<registro>.Currency)` devuelve la **etiqueta**: `"NIO"`, `"USD"`, `"EUR"` — que es
> exactamente lo que espera `fxSimbolo()`.

### 5.3 `RecountingSessionDetailStatus` — option set **GLOBAL** `'ch-recountingDetailsStatus'`

| Literal Power Fx | Valor | Cuándo lo escribe la app |
|---|---|---|
| `'ch-recountingDetailsStatus'.'En progreso'` | **159460000** | al crear el detalle (apertura de tula) |
| `'ch-recountingDetailsStatus'.Cerrado` | **159460001** | al terminar la tula **sin** discrepancias |
| `'ch-recountingDetailsStatus'.'Cerrado con diferencias'` | **159460002** | al terminar la tula **con** discrepancias |

> El display name del option set global lleva minúscula inicial y camel case en el medio
> (`ch-recountingDetailsStatus`): es el literal del `OptionSets/` y **no** se corrige.

### 5.4 Option sets locales de cheques y denominaciones

| Literal Power Fx | Valores |
|---|---|
| `'CheckTypes (CH-Tbl-Checks)'` | `.'BAC Local'` **159460000** · `.'Otros Local'` **159460001** |
| `'CheckType (CH-Tbl-DeclaredChecks)'` | `.'BAC Local'` **159460000** · `.'Otros Local'` **159460001** |
| `'CheckStatus (CH-Tbl-Checks)'` | `.Recontado` **159460000** · `.Digitado` 159460001 · `.Rebotado` 159460002 · `.'Enviado a digitacion'` 159460003 · `.Recibido` 159460004 · `.'No Recibido'` 159460005 |
| `'Type (CH-Tbl-MoneyDenominations)'` | `.Billete` **159460000** · `.Moneda` **159460001** |

> ⚠️ **`CheckTypes` (plural) en `Checks` y `CheckType` (singular) en `DeclaredChecks`.** Son
> dos option sets **distintos** con las mismas dos etiquetas. Copiar el literal de una
> tabla en la otra **no compila**. La app escribe **solo** `CheckTypes` (en `Checks`) y
> **solo** `.Recontado` de `CheckStatus`.

> `DenominationType` de `CH-Tbl-CountedMoneyDetails` **no es un option set**: es
> **texto** (§2.4). Se escribe con los literales `"Billete"` y `"Moneda"`, calcados de las
> etiquetas de `'Type (CH-Tbl-MoneyDenominations)'`.

### 5.5 `ApprovalStatus` — option set local `'ApprovalStatus (CH-Tbl-ApprovalRequests)'`

| Literal Power Fx | Valor | Significado |
|---|---|---|
| *(en blanco)* | — | **Pendiente**. Es el estado con el que nace toda solicitud |
| `'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado` | **159460000** | el supervisor (o el plugin de auto-approve) aprobó |
| `'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado` | **159460001** | el supervisor rechazó. **El motivo viaja en `StatusReason`** |

> 🚫 **La app NUNCA setea `ApprovalStatus`.** Ni siquiera cuando la mesa es la propia
> (mata **S-1**).

### 5.6 `RequestType` — TEXTO LIBRE, strings EXACTOS

`RequestType` es `nvarchar(100)` requerido, **no** un option set: el guard del Supervisor
compara el string tal cual. Copiar y pegar, sin tildes de más ni de menos.

| String exacto | Cuándo lo crea la v2 | Pantalla |
|---|---|---|
| `"Crear sesion de recuento"` | el cajero pide una mesa | `02` |
| `"Tula sin declaracion"` | la tula escaneada **no tiene declaración** y el cajero confirma el diálogo `B3` | `03` |
| `"Tula con diferencia"` | *Terminar tula* **con** discrepancias y la tula **sí** tenía declaración | `04` |
| `"Tula recontada sin declaracion"` | *Terminar tula* y la tula **no** tenía declaración (`IsBlank(gvarTula.BagDeclarationID)`) | `04` |

> 🧨 **`"Primera tula en planilla"` EXISTE en el contrato pero la v2 NO lo produce.**
> Es una divergencia funcional real, declarada como **`L-6`** y **DECIDIDA con el dueño del
> proceso**: en la v1, abrir una tula **con** declaración también generaba una solicitud y
> el cajero quedaba esperando al supervisor. El mockup (`B1`/`B2`) manda al cajero
> **directo a `scrConteo`** al elegir la tula y la declaración, y así queda. Consecuencia
> aceptada: la transición `Almacenada en boveda → Asignada a cajero` deja de pasar por
> aprobación en el camino feliz, y el Supervisor deja de ver ese tipo desde Recounting.
>
> **La v2 tampoco lo espera** (§5.8, regla 5 de la recuperación): la ruta con declaración
> **no espera nada**, así que no hay timer ni consulta que lo mire. Una solicitud legada de
> ese tipo, creada por la v1 y todavía pendiente, es un problema de **convivencia
> server-side** —el canvas del Supervisor, al aprobarla, re-patchea la bolsa a
> `Asignada a cajero` con la sesión y la declaración viejas— y se resuelve en el guard de
> `00` §2.2. La app **no la crea, no la lee y no la espera**.

### 5.7 `RequestResources` — claves EXACTAS (contrato con CH-App-Supervisor)

Se arma **siempre con `JSON({...})`**, jamás por concatenación (la v1 concatenaba con
`""""`). Los GUIDs viajan como texto: `Text(registro.'PK')`.

**Fuente de verdad**: `src/backend/CashHub.Validation/Security/ApprovalRequestResources.cs`
— el guard inspecciona el JSON contra esta tabla y **traza el desvío**.

| `RequestType` | Claves (exactas, en este orden) |
|---|---|
| `"Crear sesion de recuento"` | `RecountingSessionGUID` |
| `"Tula sin declaracion"` | `BagGUID`, `RecountingSessionGUID` |
| `"Tula con diferencia"` | `BagGUID` |
| `"Tula recontada sin declaracion"` | `BagGUID` |
| `"Primera tula en planilla"` *(no la produce la v2)* | `BagGUID`, `RecountingSessionGUID`, `DeclarationBagGUID` |

```powerfx
// Ejemplo — "Tula sin declaracion" (03). MISMA forma para los otros tres tipos.
IfError(
    Set(
        gvarSolicitud,
        Patch(
            'CH-Tbl-ApprovalRequests',
            Defaults('CH-Tbl-ApprovalRequests'),      // Name es AUTONUMBER: no se toca
            {
                RequestType:    "Tula sin declaracion",
                BagNumber:      Trim(gvarTula.BagID),
                RequestDetails: fxHtmlSinDeclaracion(
                                    Trim(gvarTula.BagID),
                                    gvarTula.ReceiptIdMirror,
                                    gvarTula.ReceptionID.RemitentName,
                                    gvarUser.'Full Name',
                                    gvarMesa.Name,
                                    Text(Now(), FmtFecha)
                                ),
                RequestResources: JSON({
                    BagGUID:               Text(gvarTula.'CH-Tbl-Bags'),
                    RecountingSessionGUID: Text(gvarSesion.'CH-Tbl-RecountingSession')
                })
                // ApprovalStatus NO se setea: en blanco = Pendiente (§5.5)
            }
        )
    ),
    If(true,
        Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error);
        Set(gvarSolicitud, Blank());
        Blank()
    )
)
```

> 🧨 **El `Patch` va envuelto en un `Set`**: el registro que devuelve —con su PK— es lo que
> el polling usa para preguntar por PK (§5.8). Sin eso, el polling tiene que adivinar cuál
> es "su" solicitud, que es exactamente lo que hacía la v1.
>
> Si el `Patch` falla, `gvarSolicitud` queda en `Blank()` ⇒ el timer **no arranca** y la
> pantalla no se queda esperando a una solicitud que nunca existió.

**Límites duros**: `RequestDetails` es `ntext(10000)`; `RequestResources` es
`nvarchar(2000)` — el JSON de 2 GUIDs entra holgado.

### 5.8 🧨 Contrato de polling — UN timer POR PANTALLA QUE ESPERA, 15 s, y el rechazo es un estado de PRIMERA CLASE

Es el fix de **S-4**: la v1 preguntaba `IsBlank(LookUp(…, ApprovalStatus = Blank()))` y
**fallaba abierto** — un **rechazo** liberaba al cajero exactamente igual que una
aprobación, porque en los dos casos la solicitud dejaba de estar pendiente.

🧨 **Regla madre: la pantalla que CREA o ESPERA una solicitud tiene SU PROPIO timer.** No
hay un timer central ni una pantalla que espere por otra. Son **tres**, uno por pantalla, y
cada uno solo conoce **el tipo de solicitud que esa pantalla produce**:

| Pantalla | Timer | Qué espera | `RequestType` que le pertenece |
|---|---|---|---|
| `02` `scrSesion` | `tmrPollSes` | la **sesión** de conteo | `"Crear sesion de recuento"` |
| `03` `scrTulas` | `tmrPollTul` | la **tula sin declaración** | `"Tula sin declaracion"` |
| `04` `scrConteo` | `tmrPollCon` | la **tula terminada con diferencia** | `"Tula con diferencia"` · `"Tula recontada sin declaracion"` |

> Una pantalla **nunca** recupera ni observa la solicitud de otra. Si `02` adoptara una
> solicitud de tula, el cajero esperaría en la pantalla equivocada; si `04` adoptara la de
> apertura, el overlay `C7` se abriría sobre un conteo que nadie envió a revisión.

**Propiedades del temporizador — idénticas en las tres pantallas:**

| Regla | Valor |
|---|---|
| **Cuántos timers** | **UNO por pantalla que espera**: `tmrPollSes` (`02`), `tmrPollTul` (`03`), `tmrPollCon` (`04`). Ninguna pantalla tiene dos |
| Tipo | **Temporizador clásico** |
| `Duration` | `15000` (15 s) |
| `Repeat` | `true` |
| `AutoStart` | `false` |
| `Start` | 🧨 **`ctxEsperando<Scr> && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`** — el timer corre exactamente mientras **esa pantalla** cree que espera, y se desarma solo: las ramas que resuelven bajan `ctxEsperando<Scr>` |
| `Visible` | `false` |
| `OnTimerStart` | **vacío** — cero `Refresh` por tick |
| `Refresh` | un solo `Refresh('CH-Tbl-Bags')` en **cada rama de resolución** (regla D-5, forma 2) |

> 🧨 **Por qué `Start` cuelga de `ctxEsperando<Scr>` y NO de
> `IsBlank(gvarSolicitud.ApprovalStatus)`.** `gvarSolicitud` se re-lee por PK en varios
> puntos que no son el tick (la compensación de un abandono, la re-adopción de una
> solicitud previa, la recuperación del `OnVisible`). Cualquiera de esas lecturas puede
> dejar `ApprovalStatus` **ya resuelto** en la variable **antes** de que alguien pinte el
> estado: con `IsBlank(…ApprovalStatus)` el timer se apagaría ahí y la pantalla quedaría
> esperando para siempre. La bandera de espera es de la **pantalla**, y es la única que
> sabe si la UI todavía está en modo espera.
>
> 🧨 **`!IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`, no `!IsBlank(gvarSolicitud)`.**
> Sobre un **registro** tipado vacío `IsBlank()` puede devolver `false`: la existencia se
> pregunta **por la PK**, como en todo el resto de la app.

**Consulta del tick — UNA sola, delegable, por PK. La solicitud PUNTUAL, jamás una
búsqueda:**

```powerfx
// tmrPoll<Scr>.OnTimerEnd
Set(
    gvarSolicitud,
    LookUp(
        'CH-Tbl-ApprovalRequests',
        'CH-Tbl-ApprovalRequest' = gvarSolicitud.'CH-Tbl-ApprovalRequest'
    )
);
If(
    // ---- APROBADO ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Aprobado,
        Refresh('CH-Tbl-Bags');
        UpdateContext({ ctxEsperando<Scr>: false, ctxRechazada<Scr>: false });
        <continuación del flujo aprobado>,

    // ---- RECHAZADO: BLOQUEA, no libera ----
    gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado,
        Refresh('CH-Tbl-Bags');
        <devolución del sujeto al cajero — ver la tabla de abajo>;
        UpdateContext({ ctxEsperando<Scr>: false, ctxRechazada<Scr>: true }),

    // ---- ni una ni otra: SIGUE PENDIENTE. No se hace NADA. ----
    false
)
```

> Una pantalla puede agregar **una rama previa** para *"la solicitud ya no existe"*
> (`IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')` después de la re-lectura): esperar a
> algo que alguien borró es el mismo fallo abierto que estamos matando. `02` la tiene.
> Lo que **ninguna** puede hacer es liberar con `IsBlank(ApprovalStatus)`.

**Qué hace cada rama, por pantalla — contrato cerrado:**

| Pantalla | Rama *Aprobado* | Rama *Rechazado* |
|---|---|---|
| `02` | re-lee la sesión por PK (el plugin acaba de escribir `Confirmed`), arma el contrato de §4.5 y `Navigate(scrTulas)` | la tarjeta pasa al estado `A3`. La sesión **no** se confirma y el cajero **solo** puede elegir otra mesa |
| `03` | abre la tula **sin declaración** por el motor único de apertura (`btnAbrirNuevaTul`), que re-lee la bolsa por PK y verifica que siga en bóveda | overlay de rechazo con el motivo. La tula sigue **`Almacenada en boveda`** y **no** se creó ningún detalle de sesión |
| `04` | la tula queda **cerrada** por el supervisor; se re-lee `gvarTula` y se vuelve a `scrTulas` | 🧨 **la app devuelve la bolsa a `Asignada a cajero` y el detalle de sesión a `En progreso`**, y baja `ctxSoloLectura` ⇒ el cajero puede **corregir**. Ver la nota de abajo |

> 🧨 **El rechazo de `"Tula con diferencia"` / `"Tula recontada sin declaracion"` DEVUELVE
> la bolsa a `Asignada a cajero` (era la divergencia `D04-15`, hoy contrato).** Al enviar a
> revisión, `04` mueve la bolsa a `Pendiente de revision por supervisor - recuento` (§5.1) —
> si no lo hiciera, el estado mentiría mientras el supervisor mira. Un rechazo que dejara la
> bolsa ahí dejaría la tula **muerta**: en `Pendiente` la pantalla está en solo lectura
> (`04` §0.2) y el cajero no podría corregir nada, que es justo lo que el rechazo le pide.
>
> **Verificado contra `CH-App-Supervisor`** (`Screen Approvals.fx.yaml`): al **rechazar**,
> el supervisor **ya** hace `Patch('CH-Tbl-Bags', …, { BagStatus: 'Asignada a cajero' })`
> para los dos tipos. El `Patch` de `04` es por lo tanto **idempotente y de refuerzo**, no
> una carrera: si el supervisor ya lo hizo, escribe el mismo valor; si mañana esa lógica se
> mueve a un plugin y desaparece del canvas del supervisor, `04` sigue devolviendo la tula.
> Lo que la app **jamás** escribe es `ApprovalStatus` (§2.4, mata **S-1**).

**Consulta de RECUPERACIÓN** — para el caso *"el cajero cerró la app mientras esperaba"*.
Corre **una sola vez por visita**, en el `OnVisible` de la pantalla, y está **acotada al
tipo de solicitud de esa pantalla y a su sujeto**:

| Pantalla | Dónde | Predicado (además de `'Created By'.User = gvarUser.User`) |
|---|---|---|
| `02` | `OnVisible`, solo si hay **sesión de hoy** | `RequestType = "Crear sesion de recuento"` **+** ventana del día (`'Created On' >= Today() && < DateAdd(Today(),1,TimeUnit.Days)`) |
| `03` | `btnResolverDeclaracionTul`, rama *sin declaración* | `RequestType = "Tula sin declaracion"` **+** `BagNumber = <lo escaneado>` **+** ventana del día |
| `04` | `OnVisible` | `(RequestType = "Tula con diferencia" \|\| RequestType = "Tula recontada sin declaracion")` **+** `BagNumber = Trim(gvarTula.BagID)`. 🧨 **NO exige `ApprovalStatus = Blank()`** — misma excepción que `02`, ver la regla 4 |

```powerfx
// Forma canónica (ejemplo de 04). Sort + First, NUNCA LookUp a secas.
Set(
    gvarSolicitud,
    First(
        Sort(
            Filter(
                'CH-Tbl-ApprovalRequests',
                'Created By'.User = gvarUser.User &&
                BagNumber = Trim(gvarTula.BagID) &&
                (
                    RequestType = "Tula con diferencia" ||
                    RequestType = "Tula recontada sin declaracion"
                )
            ),
            'Created On',
            SortOrder.Descending
        )
    )
)
```

> 🧨 **La consulta trae la ÚLTIMA solicitud de esta tula, resuelta o no; el ESTADO de la UI
> se DERIVA de lo que traiga**, en el `OnVisible` de la pantalla y en un solo lugar:
>
> ```powerfx
> ctxEsperando<Scr>: !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest') &&
>                    IsBlank(gvarSolicitud.ApprovalStatus)
> ctxRechazada<Scr>: gvarSolicitud.ApprovalStatus = 'ApprovalStatus (CH-Tbl-ApprovalRequests)'.Rechazado
> ```
>
> Un `Aprobado` no levanta ninguna de las dos banderas: la tula ya está cerrada y la
> pantalla se abre en solo lectura por su `BagStatus`, que es la autoridad.

**Reglas de la recuperación:**

1. 🧨 **Acotada al tipo de la pantalla Y al sujeto.** Traer *"cualquier solicitud pendiente
   del cajero"* es lo que rompía: con una sesión pendiente en la cola, `04` abría el overlay
   de espera sobre una tula que nadie envió a revisión. El sujeto es la **sesión del día**
   en `02` y el **número de tula** (`BagNumber`, la columna que existe justo para esto,
   `L-18`) en `03` y `04`.
2. **`'Created By'.User` es PK contra PK** (regla D-1): `'Created By'` es un lookup y
   `.User` su PK ⇒ `eq` de GUID, delegable. Lo que NO se puede es
   `'Created By'.'Full Name'`.
3. **`Sort` + `First`, nunca `LookUp` a secas** (regla D-9): con varias pendientes, `LookUp`
   devuelve una cualquiera. La que importa es **la última**.
4. 🧨 **`ApprovalStatus = Blank()` NO va en la recuperación de `02` ni en la de `04`.**
   Delega bien (`eq null`), pero **filtra de más**: las dos pantallas necesitan ver también
   el **RECHAZO** al reabrir la app, y con el predicado puesto el rechazo es invisible.
   · En `02`, sin verlo, la tarjeta vuelve al estado inicial y deja la sesión no confirmada
   **bloqueando la mesa todo el día**.
   · En `04`, sin verlo, la tula queda en `Pendiente de revision por supervisor - recuento`
   —o sea, en **solo lectura** (`04` §0.2)— **sin ningún overlay que explique por qué**: el
   cajero abre una pantalla muerta y sin motivo. Con el rechazo visible se pinta `C8` con
   `StatusReason` y *Corregir el conteo*.
   Las dos derivan el estado de lo que traiga la consulta (bloque de arriba), no del
   predicado. `03` tampoco lo lleva, por un motivo distinto: su consulta no pinta un estado,
   **decide** entre re-adoptar una solicitud pendiente, consumir una aprobada o volver a
   pedir tras un rechazo (`03` §6.2), y necesita las tres. ⇒ **NINGUNA de las tres consultas
   de recuperación lleva `ApprovalStatus = Blank()`.** El estado siempre se lee **del
   registro traído**, en cliente, sobre un registro que ya está en memoria.
   Si tu Studio marcara alguno de los predicados que sí quedan como no delegable, **avisá**:
   el fallback es acotar por `'Created On' >= Today()` y filtrar en cliente — nunca quitar
   la condición.
5. 🧨 **`"Primera tula en planilla"` NO entra en ninguna recuperación.** Con `L-6` **decidido**
   (§5.6), la v2 no lo crea **y no espera por él**: la ruta con declaración va directo a
   `scrConteo`. Una solicitud legada de ese tipo, todavía pendiente, es un problema de
   **convivencia server-side** —aprobarla re-patchea la bolsa a `Asignada a cajero` con la
   sesión vieja— y se resuelve en el guard de `00` §2.2, no con un timer en el canvas.
6. **Si mañana se agrega un `RequestType`, se agrega a la pantalla que lo produce**, en el
   mismo commit. Una solicitud que su pantalla no ve deja al cajero operando con una
   aprobación pendiente invisible.

**Qué ve el cajero en cada estado** (mockup `A2`/`A3` en `02`, `C7`/`C8` en `03` y `04`):

| Estado | UI |
|---|---|
| Pendiente | Spinner moderno + *"Esperando aprobación del supervisor"* + *"Se consulta el estado cada 15 segundos."* + los datos de la solicitud. Salida: **Cancelar solicitud** (`02`) / **Cancelar la espera** (`03`) / **Volver a las tulas** (`04`) |
| **Rechazado** | Banner `bDanger` con **`gvarSolicitud.StatusReason`** (el motivo que escribió el supervisor), `SolvedBy` y `ResolutionDatetime`. **Bloquea**: `02` obliga a elegir otra mesa; `03` deja la tula en bóveda; `04` devuelve la tula a **`Asignada a cajero`** y el control al cajero para corregir |
| Aprobado | se cierra el overlay y sigue el flujo de la tabla de arriba |

---

## 6. Estilos: defaults y alias `ESTILO-*`

Idéntico sistema al de `reception-rebuild/01-app.md` §6 — mismos defaults, mismos alias,
mismos nombres. Lo que sigue es esa sección **más** los alias propios de reconteo (§6.4).

### 6.1 Defaults (MANDAN sobre las tablas de `02`/`03`/`04`)

Toda **Etiqueta clásica** y todo **Botón clásico** arrancan así, salvo que su tabla diga
otra cosa:

| Propiedad | Valor | Por qué se pisa el default |
|---|---|---|
| `Font` | `Font.'Open Sans'` | es la fuente del tema; se escribe explícita para no depender de que el tema viaje |
| `Size` | `9` | equivale a los 12 px del mockup |
| `Color` | `ThemeInk` | — |
| `PaddingLeft` / `Right` / `Top` / `Bottom` | `0` | la etiqueta clásica trae **5** en las cuatro: sin esto todo texto queda corrido 5 px |
| `Wrap` | `false` | el default de plataforma es `true`: una etiqueta de una línea con alto justo **envuelve y recorta** |
| `VerticalAlign` | `VerticalAlign.Middle` | — |
| `BorderThickness` (botones) | `0` | el botón clásico trae `2` |
| `HoverFill` / `PressedFill` (botones de solo lectura) | **iguales al `Fill`** | si no, la superficie/píldora parpadea al pasar el mouse |

**Entrada de texto clásica**: `BorderColor` `ThemeLine`, `BorderThickness` `1`, `Radius`
(las 4) `7`, `Size` `9`, `Color` `ThemeInk`, `Fill` `ThemeSurface`.
*(El color del `HintText` no es configurable en el control clásico — divergencia aceptada
vs. `.cin::placeholder` del mockup.)*

**`Height` de la Entrada de texto clásica — TRES valores, no "el que salga":**

| Contexto | `Height` | Por qué |
|---|---|---|
| Dentro del template de la grilla de denominaciones o de cheques | **`26`** | la fila mide 32-34: 26 deja aire arriba y abajo (`.qin` del mockup) |
| **Fuera de una galería**: barra de herramientas, cuerpo de un diálogo, N° de cuenta | **`32`** | comparte fila con Botones (32) y Menús desplegables (32) |
| **Barra scan-first** de `03` (`inpScanTul`) | **`44`** | es el foco de la pantalla: `.scanIn` mide 11 px de padding + 16 px de texto (receta 7.8) |

> Regla derivada: **si el input comparte fila con un control de 32, va en 32**.

**Menú desplegable clásico**: `BorderColor` `ThemeLine`, `BorderThickness` `1`, `Size` `9`,
`Color` `ThemeInk`, `Fill` `ThemeSurface`, `Height` `32`.
⚠️ **No tiene `Radius`** → queda de esquina viva. Divergencia declarada vs. el
`border-radius:7px` del mockup; el borde y el tamaño sí se respetan.

**Casilla clásica** (tilde de cheque declarado): `Size` `9`, `CheckmarkFill`
`ThemeSurface`, `CheckboxBorderColor` `ThemeInk3`, `CheckboxBackgroundFill`
`ThemeSurface`, `CheckboxSelectedFill` `ThemeAccent`, `Text` `""` (la etiqueta va aparte),
`Width` `24`, `Height` `24`.

**Glifos**: prohibido meter glifos en el `Text` de etiquetas o botones (tofu en Open Sans).
Todo glifo es un **Icono clásico**. Catálogo CERRADO de iconos de esta app:

| Icono | Dónde |
|---|---|
| `Icon.Trash` | eliminar (cheque manual, depósito) |
| `Icon.Add` | agregar cheque manual, nuevo depósito |
| `Icon.Reload` | ⟳ actualizar (`03`) y ⟳ **Leer contadora** (`04`) |
| `Icon.Search` | lupa de la barra scan-first |
| `Icon.Check` | diálogo *Terminar* sin discrepancias |
| `Icon.Cancel` | la ✕ de los diálogos |
| `Icon.Lock` | **candado real** — el 🔒 del mockup: *"esto va como solicitud al supervisor y la tula queda bloqueada"* (`B3`) |
| `Icon.Warning` | ⚠ del mockup — **todo banner y todo tile de tono `warn`** (`C5`: hallazgos y *Terminar con discrepancias*; `B2`) |
| `Icon.Information` | banner informativo (ⓘ) |
| `Icon.Blocked` | banner de **rechazo** (`A3`, `C8`) y overlay de bloqueo de identidad (§3.2) |
| `Icon.Back` | flecha ← del header de `04` |
| `Icon.ChevronRight` | el `›` al final de la fila de tula (`03`) |
| `Icon.Sync` | diálogo *Cambiar la moneda del depósito* (`C6-1`) |

> `Icon.Information` **existe** en el set clásico; **`Icon.Info` NO existe**. Si el
> IntelliSense de tu versión nombra distinto alguno de estos, **el suyo manda**.

**Conversión de tamaños**: con *Escalar para ajustar* activado, 1 unidad = 1 px del mockup,
así que `X/Y/Width/Height` se copian tal cual. `Size` está en **puntos**:

> **`Size = Round(px_del_mockup × 0.75, 2)`**

| px | `Size` | Dónde en el mockup |
|---|---|---|
| 22 | `16.5` | `.sesBrand .tile` (tile grande de `scrSesion`) |
| 19 | `14.25` | `.hBig` (título del header de `scrConteo`) |
| 17 | `12.75` | `.sesBrand h2` |
| 16 | `12` | `.scanIn input` (el campo de escaneo) |
| 15 | `11.25` | `.kpi .tot b` (total del KPI del rail) |
| 14.5 | `10.88` | `.dlgH h3` (título de diálogo), `.denRow .den` (denominación) |
| 14 | `10.5` | `.hTitle`, glifo del `.brandTile` |
| 13.5 | `10.13` | `.scan h3`, `.qin`, `.denFoot .tot` |
| 13 | `9.75` | `.panelH h4`, `.tuRow b`, `.pickCard b`, `.pickAmt`, `.denRow .tot`, `.waitRow b` |
| 12.5 | `9.38` | `.tuHead/.tuRow`, `.tabl button`, `.emptyMsg`, `.denFoot` |
| 12 | `9` | cuerpo general: `.btn`, `.in`, `.cin`, `.meta`, `.banner`, `.kv`, `.dlgB p` |
| 11.5 | `8.63` | `.hMeta`, `.userChip`, `.kpi .lin`, `.autoB`, `table.mt` |
| 11 | `8.25` | `.btnSm`, `.note`, `.railFoot`, `.field label`, `.curRow label`, `.mat h5 em`, `.tuRow .hr` |
| 10.5 | `7.88` | `.pill`, `.kpi .cur`, `.kpi .tot span`, `.lnkOk`, `.mat h5`, `.listHead`, `.mailFoot` |
| 10 | `7.5` | `.railLab`, `.thead`, `.tagDec`, `.tagMan` |
| 9.5 | `7.13` | `.matT th` (encabezados de la matriz de moneda) |

### 6.2 Alias de estilo — etiquetas

| Alias | Se logra con |
|---|---|
| `ESTILO-TITULO-APP` | Size `10.5`, FontWeight `Semibold`, Color `ThemeInk` |
| `ESTILO-SUBTITULO-APP` | Size `8.25`, Color `ThemeInk3` |
| `ESTILO-TITULO-GRANDE` | Size `14.25`, FontWeight `Bold`, Color `ThemeInk` — el `Tula ST000123` del header de `04` |
| `ESTILO-SECCION` | Size `8.25`, FontWeight `Semibold`, Color `ThemeInk2` — **SIEMPRE en MAYÚSCULAS** |
| `ESTILO-HEADER-COL` | Size `7.5`, FontWeight `Semibold`, Color `ThemeInk2` — MAYÚSCULAS (`.thead` del mockup) |
| `ESTILO-CELDA` | Size `9.38`, Color `ThemeInk` |
| `ESTILO-CELDA-NUM` | Size `9.38`, Color `ThemeInk`, Align `Align.Right` |
| `ESTILO-CELDA-FUERTE` | Size `9.75`, FontWeight `Semibold`, Color `ThemeInk` |
| `ESTILO-META` | Size `9`, Color `ThemeInk2` |
| `ESTILO-META-CHICA` | Size `8.25`, Color `ThemeInk3` — `.note`, `.railFoot`, `.tuRow .hr` |
| `ESTILO-DETALLE-TITULO` | Size `9.75`, FontWeight `Semibold`, Color `ThemeInk` — `.panelH h4` |
| `ESTILO-KPI-LAB` | Size `7.88`, FontWeight `Semibold`, Color `ThemeInk3` — MAYÚSCULAS |
| `ESTILO-KPI-VAL` | Size `11.25`, FontWeight `Bold`, Color `ThemeInk`, Align `Align.Right` |
| `ESTILO-DLG-TITULO` | Size `10.88`, FontWeight **`Bold`**, Color `ThemeInk` — es un `<h3>` y el navegador lo pinta bold |
| `ESTILO-DLG-TEXTO` | Size `9`, Color `ThemeInk2`, Wrap `true` + `Height` explícito (o `AutoHeight true`) |
| `ESTILO-CAMPO-LAB` | Size `8.25`, FontWeight `Semibold`, Color `ThemeInk2` |
| `ESTILO-ERROR` | Size `8.25`, Color `ThemeDanger`, Wrap `true` |
| `ESTILO-VACIO` | Size `9.38`, Color `ThemeInk3`, Align `Align.Center`, Wrap `true` |

> Toda etiqueta con `Wrap true` lleva `Height` explícito y verificado contra el
> `TemplateSize` de su galería: si no, el texto se corta en silencio.

### 6.3 Alias de estilo — botones

Todos con `Size` `9`, `FontWeight` `Semibold`, `Radius` (las 4) `8`, `Height` `32`, salvo
que se indique.

| Alias | Fill | Color | Borde | Hover / Pressed |
|---|---|---|---|---|
| `ESTILO-BTN-PRIMARIO` | `ThemeAccent` | `ThemeSurface` | `0` | `ThemeAccentDark` / `ThemeAccentDark` |
| `ESTILO-BTN-CONTORNO` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent`, `1` | `ThemeAccentSoft` / `ThemeAccentSoft` |
| `ESTILO-BTN-FANTASMA` | `Color.Transparent` | `ThemeInk2` | `ThemeLine`, `1` | `ThemeSurface2` / `ThemeSurface2` |
| `ESTILO-BTN-PELIGRO` | `ThemeDanger` | `ThemeSurface` | `0` | `ThemeDanger` / `ThemeDanger` *(sin tono oscuro en el mockup — declarado)* |
| `ESTILO-BTN-CHICO` | `ThemeSurface` | `ThemeInk2` | `ThemeLine`, `1` | `ThemeSurface2` — Size `8.25`, Height `26`, Radius `7` |
| `ESTILO-BTN-CHICO-ACC` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent`, `1` | `ThemeAccentSoft` — Size `8.25`, Height `26`, Radius `7` |
| `ESTILO-BTN-CHICO-DAN` | `ThemeSurface` | `ThemeDanger` | `ThemeDangerLine`, `1` | `ThemeDangerSoft` — Size `8.25`, Height `26`, Radius `7` |
| `ESTILO-BTN-GRANDE` | `ThemeAccent` | `ThemeSurface` | `0` | `ThemeAccentDark` — Size `9.75`, Height `44` (botón *Abrir tula* de la barra scan-first) |

**Deshabilitado (todos)**: `DisabledFill` `ThemeLineSoft`, `DisabledColor` `ThemeInk3`,
`DisabledBorderColor` `Color.Transparent`. La habilitación va siempre en `DisplayMode` con
la condición explícita, y **la condición se escribe contra el control o la colección que la
acción realmente usa**, no contra una variable paralela.

> 🧨 **Toda condición de `DisplayMode` que escriba Dataverse arranca con
> `gvarIdentidadOk &&`** (§3.2) **y sigue con el estado de la tula**:
> `gvarTula.BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero'`. La v1 dejaba la
> botonera de conteo viva sobre tulas ya recontadas.

### 6.4 Alias propios de reconteo

| Alias | Se logra con | Dónde |
|---|---|---|
| `ESTILO-DEN` | Size `10.88`, FontWeight `Semibold`, Color `ThemeInk`, Align `Align.Right` | la denominación (`C$ 1,000`) de la grilla |
| `ESTILO-DEN-TOT` | Size `9.75`, FontWeight `Semibold`, Color `ThemeInk`, Align `Align.Right` | el total de la fila de denominación |
| `ESTILO-QIN` | Entrada de texto clásica · `Format` `TextFormat.Number` · Align `Align.Right` · Size `10.13` · FontWeight `Semibold` · Height `26` · Radius `7` | la celda **Cantidad** |
| `ESTILO-AUTO` | **Botón clásico** en `DisplayMode.View` · Fill/Hover/Pressed `ThemeLineSoft` · Color `If(<cantidad> = 0, ThemeInk3, ThemeInk2)` · Radius `6` · Height `22` · Size `8.63` · BorderThickness `0` | el badge **Auto** de solo lectura (receta 7.9) |
| `ESTILO-MAT-LAB` | Size `8.25`, Color `ThemeInk2` | *Recontado* / *Declarado* / *Diferencia* de `cmpMatrizMoneda` |
| `ESTILO-MAT-VAL` | Size `9`, Color `ThemeInk`, Align `Align.Right` | valores de la matriz |
| `ESTILO-MAT-DIF` | Size `9`, FontWeight `Bold`, Color `fxColorDif(<dif>)`, Align `Align.Right` | fila *Diferencia* |
| `ESTILO-FOOT` | Size `9.38`, FontWeight `Bold`, Color `ThemeInk`, sobre `Fill` `ThemeSurface2` | pies de tabla (`.dpFoot`, `.denFoot`) |
| `ESTILO-TAG-DEC` | píldora (receta 7.4) · Fill `ThemeInfoSoft` · Color `ThemeInfo` · Size `7.5` · Text `"Declarado"` | origen del cheque |
| `ESTILO-TAG-MAN` | píldora · Fill `ThemeWarnSoft2` · Color `ThemeWarn` · Size `7.5` · Text `"Manual"` | origen del cheque |
| `ESTILO-FILA-MANUAL` | superficie de fila (receta 7.1) con Fill `ThemeWarnSoft` | fondo ámbar de la fila de cheque manual (`.chkRow.man`) |

---

## 7. Recetas reutilizables

Cada receta fija TODAS las propiedades de los controles que nombra. Cuando `02`/`03`/`04`
apuntan a una receta, no repiten sus propiedades: solo dan nombre final, coordenadas y las
fórmulas específicas.

### 7.1 Superficie redondeada = Botón clásico

Ni el Rectángulo ni la Etiqueta tienen `Radius` (§0.3). Toda card, chip, píldora o tile con
esquinas redondeadas es un **Botón clásico** con `Text: ""`:

| Propiedad | Valor |
|---|---|
| `Text` | `""` |
| `DisplayMode` | `DisplayMode.View` (decorativa) |
| `Fill` | el que pida la tabla (`ThemeSurface`, `ThemeSurface2`, …) |
| `HoverFill` / `PressedFill` | **iguales al `Fill`** (si no, parpadea) |
| `BorderColor` / `BorderThickness` | `ThemeLine` / `1` (o `0` si la card no lleva línea) |
| `Radius` (las 4) | `14` card de sesión · `13` cards y diálogos · `11` cards del rail, matrices y pick cards · `9` tiles y banners · `8` botones · `7` chips chicos · `6` badge Auto · `16` chips de alto 32 · `12` píldoras de estado |
| `Color` | irrelevante (no hay texto), dejalo en `ThemeInk` |

**Reglas de uso**:

- La superficie va **al FONDO del z-order** de su bloque; el contenido (etiquetas,
  entradas, iconos) se inserta **después**, encima. Si un control queda tapado o no recibe
  clic, tenés el z-order invertido.
- `DisplayMode.View` **no dispara `OnSelect`**. Si la superficie vive dentro del template
  de una galería y tiene que delegar el clic, va en `DisplayMode.Edit` con
  `OnSelect: Select(Parent)` (receta 7.3).
- ❌ **La sombra del mockup no existe** en el botón clásico (`box-shadow` de `.card`,
  `.dlg`). Pérdida aceptada y declarada: la separación la da el borde `ThemeLine`. La única
  sombra disponible es `DropShadow` del **Contenedor**, que se usa en la card del modal
  (7.2) y en `cmpDialogo` (§8.3).

### 7.2 Diálogo nativo de confirmación (el patrón que reemplaza a PowerCAT)

Estructura fija de **7 controles + contenido**. El overlay se inserta **ÚLTIMO en la
pantalla** (tope del z-order): si queda debajo de algo, el modal no tapa.

> Esta receta es la **misma** que implementa `cmpDialogo` (§8.3) por dentro. Se usa
> **directamente** cuando el diálogo lleva **cuerpo rico** (kv, banner, tarjetas de
> desambiguación, tablas) — un componente canvas **no puede recibir controles hijos**.
> Ver el reparto en §8.3, nota **C**.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlg<Nombre><Scr>` | Contenedor de diseño manual | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Fill` `ThemeOverlay` · `Radius` (las 4) `0` · `BorderThickness` `0` · **`Visible` `ctxDlg<Nombre><Scr>`** |
| `btnBloq<Nombre><Scr>` | Botón clásico (hijo, **al FONDO** del contenedor) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** — traga los clics del fondo (modalidad real) |
| `cntCard<Nombre><Scr>` | Contenedor de diseño manual (hijo) | X `(Parent.Width - Self.Width) / 2` · Y `(Parent.Height - Self.Height) / 2` · Width **`400`** (normal) \| **`560`** (ancho: `B1`, `B2`, `C4`, `C5`) \| **`330`** (angosto: `C6`) · Height la que pida el contenido · `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DropShadow` `DropShadow.Regular` |
| `btnIco<Nombre><Scr>` | Botón clásico (receta 7.1) — **opcional**, solo si el diálogo lleva glifo de estado | X `22` · Y `17` · Width `34` · Height `34` · `Radius` `9` · Fill `ThemeOkSoft` \| `ThemeWarnSoft` \| `ThemeDangerSoft` \| `ThemeInfoSoft` |
| `icoTipo<Nombre><Scr>` | Icono clásico (`Icon.Check` \| `Icon.Warning` \| `Icon.Lock` \| `Icon.Trash` \| `Icon.Sync` \| `Icon.Blocked`) | centrado sobre `btnIco…` · Width `18` · Height `18` · Color `ThemeOk` \| `ThemeWarn` \| `ThemeDanger` \| `ThemeInfo` |
| `lblDlgTit<Nombre><Scr>` | Etiqueta clásica | X `btnIco….X + btnIco….Width + 10` (o `22` sin glifo) · Y `17` · Width `cntCard….Width - Self.X - 44` · Height `22` · `ESTILO-DLG-TITULO` · Wrap `true` |
| `lblDlgTxt<Nombre><Scr>` | Etiqueta clásica | X = la de `lblDlgTit…` · Y `lblDlgTit….Y + lblDlgTit….Height + 4` · Width `cntCard….Width - Self.X - 22` · `ESTILO-DLG-TEXTO` · `AutoHeight` `true` |
| `icoDlgX<Nombre><Scr>` | Icono clásico `Icon.Cancel` | X `cntCard….Width - 36` · Y `17` · Width `18` · Height `18` · `Color` `ThemeInk3` · **`HoverColor` `ThemeInk`** · **`OnSelect` = EXACTAMENTE el mismo del botón Cancelar** |
| `btnDlgCancel<Nombre><Scr>` | Botón clásico | `ESTILO-BTN-FANTASMA` · Text `"Cancelar"` (o el que pida el mockup) · Y `cntCard….Height - 51` · `OnSelect` `UpdateContext({ctxDlg<Nombre><Scr>: false})` |
| `btnDlgOk<Nombre><Scr>` | Botón clásico | `ESTILO-BTN-PRIMARIO` (o `ESTILO-BTN-PELIGRO` si es destructivo) · Y = la de Cancelar · `OnSelect` la acción + `UpdateContext({ctxDlg<Nombre><Scr>: false})` |

**Reglas no negociables:**

1. **La ✕ SIEMPRE cancela.** `icoDlgX` ejecuta literalmente el mismo `OnSelect` que
   `btnDlgCancel`: cerrar y **no commitear nada**. Prohibido que la ✕ aplique cambios.
2. 🧨 **CONFIRMAR ANTES DE EJECUTAR.** La acción destructiva vive en el `OnSelect` del
   botón **confirmar**, y en ningún otro lado. La v1 **borraba el conteo y después
   preguntaba** (`Clear(colRecountedMoneyDetails)` antes del diálogo). Si en `02`/`03`/`04`
   ves un `Clear`/`Remove`/`Patch` en el botón que **abre** un diálogo, es un bug de la
   guía.
3. **Reset al ABRIR, no al cerrar.** El botón que abre hace
   `UpdateContext({ctxDlg<Nombre><Scr>: true}); Reset(inp<Campo>)` — así el diálogo nunca
   arrastra el valor anterior.
4. **Todo borrado es destructivo ⇒ tiene diálogo.**
5. El botón de confirmar arranca deshabilitado cuando la entrada es inválida:
   `DisplayMode: If(<condición de validez>, DisplayMode.Edit, DisplayMode.Disabled)`.

#### 7.2.a Sub-bloque OPCIONAL — banner (ok / info / warn / danger)

Franja de color en el cuerpo del diálogo. Son **3 controles**, insertados en este orden (la
superficie primero, regla 7.1):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAviso<Nombre><Scr>` | Botón clásico (receta 7.1) | X `22` · Width `cntCard….Width - 44` · Height **`40`** (1 línea) \| **`58`** (2 líneas) · `Fill`/`HoverFill`/`PressedFill` = `ThemeOkSoft` \| `ThemeInfoSoft` \| `ThemeWarnSoft` \| `ThemeDangerSoft` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAviso<Nombre><Scr>` | Icono clásico: `Icon.Check` (ok) \| `Icon.Information` (info) \| **`Icon.Warning`** (warn) \| `Icon.Blocked` (danger) | X `btnAviso….X + 13` · Y `btnAviso….Y + 12` · Width `16` · Height `16` · `Color` `ThemeOk` \| `ThemeInfo` \| `ThemeWarn` \| `ThemeDanger` · `Visible` `btnAviso….Visible` |
| `lblAviso<Nombre><Scr>` | Etiqueta clásica | X `btnAviso….X + 38` · Y `btnAviso….Y` · Width `btnAviso….Width - 50` · Height `btnAviso….Height` · Size `9` · `Color` = el del icono · **Wrap `true`** · `Visible` `btnAviso….Visible` |

**Reglas**: el `Visible` se escribe **una sola vez** (en la superficie) y los otros dos lo
referencian. Si el banner es condicional, todo lo que va **debajo** encadena su `Y` contra
`btnAviso….Y + btnAviso….Height`, jamás contra un literal.

> 🧨 **El tono `warn` lleva `Icon.Warning`, NO `Icon.Lock`** (§6.1). El ⚠ del mockup es
> "atención, hay un hallazgo"; el 🔒 es "esto queda bloqueado hasta que el supervisor
> responda" y aparece **solo** en `B3`. Confundirlos hace que la mitad de los banners de
> `C5` prometan un bloqueo que no existe.

#### 7.2.b Sub-bloque OPCIONAL — resumen `kv` (pares etiqueta/valor)

**Dos etiquetas por par**, sin galería (la cantidad de pares es fija y conocida):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblKv<Campo>Etq<Nombre><Scr>` | Etiqueta clásica | X `22` · Width `110` · Height `18` · **`ESTILO-META-CHICA`** · `Text` la etiqueta (*"Tula"*, *"Planilla"*, *"Mesa"*) |
| `lblKv<Campo><Nombre><Scr>` | Etiqueta clásica | X `140` · Width `cntCard….Width - 162` · Height `18` · **`ESTILO-CELDA-FUERTE`** · `Text` el valor |

**Paso vertical entre pares: `22`.** El primer par arranca en
`Y = lblDlgTxt….Y + lblDlgTxt….Height + 12`; cada par siguiente encadena contra el anterior
(`Y = <par anterior>.Y + 22`), nunca con literales sueltos (regla 0.9).

> Cuándo usar cuál: `kv` cuando el usuario tiene que **verificar datos** antes de confirmar
> (`B3`, `C7`); banner cuando tiene que **entender la consecuencia**. Un diálogo puede
> llevar los dos (`B3` los lleva).

### 7.3 Fila clicable de galería

| Control | Tipo | Propiedades |
|---|---|---|
| `gal<Nombre><Scr>` | Galería vertical en blanco | `TemplatePadding` `0` · `ShowScrollbar` `true` · `TemplateSize`: `42` lista de tulas · `34` tabla de depósitos · `32` grilla de denominaciones · `34` grilla de cheques · `TemplateSize` `74` pick cards de `B1`/`B2` · `Items` = **colección local** (regla D-4) · **`OnSelect` = TODA la lógica de la fila** |
| `shpSel<Nombre><Scr>` | Botón clásico (receta 7.1), **al FONDO del z-order del template** | X `0` · Y `0` · Width = `gal<Nombre><Scr>.Width` · Height = `gal<Nombre><Scr>.TemplateSize` · `Fill` `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` · `HoverFill` `If(ThisItem.IsSelected, ThemeAccentSoft, ThemeSurface2)` · `PressedFill` `ThemeAccentSoft` · `Radius` `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` |
| `shpSelBarra<Nombre><Scr>` | Rectángulo clásico | X `0` · Y `0` · Width `3` · Height = `TemplateSize` · `Fill` `ThemeAccent` · `Visible` `ThisItem.IsSelected` (es el `box-shadow: inset 3px 0` del mockup, `.tuSel` / `.dpSel`) |
| `shpSep<Nombre><Scr>` | Rectángulo clásico | X `0` · Y = **`TemplateSize − 1`** · Width = Width de la galería · Height `1` · `Fill` `ThemeLineSoft` |

**Reglas**:

- La acción va en el **`OnSelect` de la GALERÍA**. **TODO hijo del template** (etiquetas,
  píldoras, superficies) lleva `OnSelect: Select(Parent)`. Si la lógica vive solo en un
  botón overlay, las etiquetas se comen el clic y el usuario tiene que cazar píxeles
  vacíos.
- 🧨 **UN solo handler por fila.** La v1 copiaba el mismo `OnSelect` de ~90-120 líneas en
  **6-7 controles por fila** de las dos galerías de depósitos: ~1.400 líneas duplicadas que
  el motor indexa **por fila**. Si dos controles del mismo template tienen el mismo cuerpo
  de `OnSelect`, es un bug de la guía.
- La superficie de selección va **al FONDO** del z-order del template: si queda arriba, su
  `HoverFill` opaco tapa el texto de la fila.
- **Excepción**: los controles *editables* del template (entrada de cantidad, casilla del
  cheque, papelera del cheque manual) **no** llevan `Select(Parent)` — tienen su propia
  acción.
- Coordenadas: `X` del control del template = `X` del encabezado − `X` de la galería.
- Galerías clásicas **auto-seleccionan la primera fila** al cargar `Items`. El panel de
  detalle se diseña asumiéndolo; los estados vacíos se reservan para **colección vacía**.
  ⚠️ **Excepción**: la tabla de depósitos recontados de `04`, de cuya selección cuelgan
  operaciones **destructivas** (*Eliminar*), usa el centinela `ctxDepSelCon` inicializado
  en `Defaults('CH-Tbl-DepositsRecountings')` = "sin selección".

### 7.4 Píldora de estado (`pil…`)

Botón clásico como badge de solo lectura (receta 7.1):

| Propiedad | Valor |
|---|---|
| `DisplayMode` | `DisplayMode.View` (o `Edit` + `OnSelect: Select(Parent)` si está dentro de un template clicable) |
| `Radius` (las 4) | `12` |
| `Height` | `22` |
| `Size` / `FontWeight` | `7.88` / `FontWeight.Semibold` |
| `Width` | `14 + 6.2 * Len(Self.Text)` |
| `BorderColor` | `ThemeLine` |
| `BorderThickness` | `If(Self.Fill = ThemeLineSoft, 1, 0)` — la variante **neutra** es la única con borde: sin él no se ve sobre card blanca |
| `HoverFill` / `PressedFill` | iguales al `Fill` |

**Mapeo `BagStatus`** (`Text`, `Fill`, `Color` — los tres con el mismo `Switch`):

```powerfx
// Text
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             "Recibida en esclusa",   // 159460000
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            "Almacenada en bóveda",  // 159460001
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               "Asignada a cajero",     // 159460002
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         "Recontada",             // 159460003
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento',  "Pendiente de revisión", // 159460004
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',   "Pendiente supervisor (esclusa)", // 159460005
    "—"
)
// Fill
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             ThemeLineSoft,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            ThemeOkSoft,
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               ThemeInfoSoft,
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         ThemeOkSoft,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento',  ThemeWarnSoft,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',   ThemeWarnSoft,
    ThemeLineSoft
)
// Color
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                             ThemeInk3,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                            ThemeOk,
    'BagStatus (CH-Tbl-Bags)'.'Asignada a cajero',                               ThemeInfo,
    'BagStatus (CH-Tbl-Bags)'.Recontada,                                         ThemeOk,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - recuento',  ThemeWarn,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',   ThemeWarn,
    ThemeInk3
)
```

**Variante ENLACE** (`.lnkOk` / `.lnkNo` del mockup — columna *Enlace* de la tabla de
depósitos). Mismo botón, `Radius` `12`, `Height` `20`, `Size` `7.88`:

```powerfx
// Text  (pestaña Declarado: RelatedRecountedDeposit · pestaña Recontado: RelatedDeclaredDeposit)
If(IsBlank(ThisItem.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'), "Sin enlazar", "Enlazado")
// Fill
If(IsBlank(ThisItem.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'), ThemeLineSoft, ThemeOkSoft)
// Color
If(IsBlank(ThisItem.RelatedRecountedDeposit.'CH-Tbl-DepositsRecounting'), ThemeInk3, ThemeOk)
```

**Variante ORIGEN DE CHEQUE**: `ESTILO-TAG-DEC` / `ESTILO-TAG-MAN` (§6.4), con
`Text`/`Fill`/`Color` decididos por `ThisItem.EsManual`.

**Variante FUERA de un template**. Los `Switch` de arriba usan `ThisItem`, que **solo existe
dentro del template de una galería**. En el header de `04` la píldora vive fuera: la
sustitución es **mecánica y total**, `ThisItem` → `gvarTula`, en las tres propiedades.

> ⚠️ Si la píldora está **dentro** de un template clicable, además lleva `DisplayMode.Edit`
> + `OnSelect: Select(Parent)` (receta 7.3): sin eso se come el clic de la fila.

### 7.5 Escritura contra Dataverse con `IfError` (y el reintento fila por fila)

**Todo `Patch`/`Remove` va envuelto en `IfError` con decisión explícita.** `App.OnError`
(§3.3) es la red final, no el manejo de errores.

**Forma A — escritura única** (crear la sesión, abrir una tula, transicionar la bolsa):

```powerfx
IfError(
    Set(gvarTula, Patch('CH-Tbl-Bags', <registro>, { <cambios> })),
    If(true,
        Notify("No se pudo abrir la tula: " & FirstError.Message, NotificationType.Error);
        Set(gvarTula, Blank());           // ← el estado local NO queda mintiendo
        Blank()
    )
)
```

**Forma B — escritura por lote con reintento** (denominaciones, cheques):

```powerfx
Clear(colFallidasCon);

// --- FASE A: un solo Patch con tabla (camino feliz) ---
IfError(
    Patch(
        'CH-Tbl-CountedMoneyDetails',
        // registros BASE, alineados fila a fila con el tercer argumento
        ForAll(
            Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
            If(
                IsBlank(M.GUID),
                Defaults('CH-Tbl-CountedMoneyDetails'),
                LookUp(colDetalleDVCon, 'CH-Tbl-CountedMoneyDetails' = M.GUID)
            )
        ),
        // registros de CAMBIO
        ForAll(
            Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
            {
                RelatedDepositRecountingID: ctxDepositoCon,
                Denomination:               M.Denominacion,
                DenominationType:           M.Tipo,          // "Billete" | "Moneda"
                Quantity:                   M.Auto + M.Manual   // ← D-5 muerto
            }
        )
    ),

    // --- FASE B: algo falló. Reintento fila por fila para saber CUÁL ---
    ForAll(
        Filter(colDetalleConteoCon, (Auto + Manual) > 0) As M,
        IfError(
            Patch(
                'CH-Tbl-CountedMoneyDetails',
                If(
                    IsBlank(M.GUID),
                    Defaults('CH-Tbl-CountedMoneyDetails'),
                    LookUp(colDetalleDVCon, 'CH-Tbl-CountedMoneyDetails' = M.GUID)
                ),
                {
                    RelatedDepositRecountingID: ctxDepositoCon,
                    Denomination:               M.Denominacion,
                    DenominationType:           M.Tipo,
                    Quantity:                   M.Auto + M.Manual
                }
            ),
            If(true,
                // 🧨 El brazo de fallo TERMINA en Blank(): Collect() devuelve una TABLA y
                //    IfError exige que el brazo tenga el tipo del Patch (un REGISTRO).
                //    Sin el "; Blank()" NO COMPILA.
                Collect(
                    colFallidasCon,
                    { Clave: Text(M.Denominacion), Detalle: M.Tipo, Mensaje: FirstError.Message }
                );
                Blank()
            )
        )
    )
);
```

**Reglas de la receta:**

1. 🧨🧨 **TODO brazo de fallo con MÁS DE UNA instrucción va envuelto en `If(true, … ; Blank())`.**
   Es la **forma canónica y única** de esta app, y es la que está escrita en las **32**
   apariciones de las cuatro guías (**6** en esta receta, **4** en `02`, **6** en `03` y
   **16** en `04`):

   ```powerfx
   IfError(x, a; b)                  // 🚫 NO — el parser no acepta una cadena de `;` como argumento
   IfError(x, If(true, a; b; Blank()))   // ✅ forma validada en Studio
   ```

   Son **dos** problemas en el mismo lugar, y el `If(true, …)` mata los dos:

   - **El parser.** `IfError(valor, brazo)` es una **llamada de función**, y una cadena
     `a; b` **no es un argumento válido**: el `;` corta la lista de argumentos y Studio
     marca *"Se esperaba un operador"* / *"Número de argumentos no válido"* en fórmulas que
     en otro contexto compilan. `If(true, …)` es un **bloque de comportamiento** legítimo y
     ahí adentro el `;` sí encadena.
   - **El tipado.** `IfError(valor, brazo)` exige que el brazo tenga **el tipo del primer
     argumento**. `Notify()` devuelve booleano, `Collect()` devuelve una **tabla**,
     `Patch()` devuelve un **registro**: sin unificar, no compila. Por eso la cadena
     **TERMINA SIEMPRE en `Blank()`**, que unifica contra cualquier tipo.

   🚨 **`pac canvas pack` NO valida fórmulas**: la forma mala **se empaqueta, se importa y
   se publica sin chistar**, y explota recién al abrir la app en Studio o al ejecutar la
   rama de error. Ledger **`L-62`**.

   > Un brazo de **una sola** instrucción (`IfError(x, Notify(…))`) **no** lleva envoltorio:
   > no hay `;` que envolver. Se envuelve **desde dos instrucciones en adelante**.
2. 🧨 **PASO DE BAJAS — obligatorio, y va DESPUÉS del alta/actualización.** Una denominación
   que quedó en `0` **no se puede patchear** (`Quantity` tiene mínimo 1, §2.4): hay que
   **borrarla**. Lo mismo con un cheque manual que el cajero eliminó.

   🧨 **El predicado se escribe desde el SNAPSHOT `…DV` HACIA la colección viva, nunca al
   revés.** Son **dos** casos, no uno: la fila *quedó en 0* **y** la fila *desapareció de la
   colección viva* (es lo que pasa al **cambiar la moneda del depósito**: `04` §7.2, Bloque
   **M**, rehace `colDetalleConteoCon` y `colChecksCon` con las denominaciones y los cheques
   de la moneda nueva, y las filas de la moneda vieja dejan de existir en local aunque sigan
   persistidas). Recorrer la colección viva buscando ceros **no ve** ese segundo caso y deja
   filas NIO colgadas de un depósito USD. Recorrer el snapshot los cubre **con una sola
   condición**:

   ```powerfx
   // Remove por REGISTRO, nunca RemoveIf (regla D-3).
   // Se recorre el SNAPSHOT; se borra todo lo persistido que ya no tiene contraparte VIVA
   // con cantidad > 0  ⇒  cubre "quedó en 0" Y "desapareció por cambio de moneda".
   ForAll(
       Filter(
           colDetalleDVCon As X,
           IsBlank(
               LookUp(
                   colDetalleConteoCon,
                   GUID = X.'CH-Tbl-CountedMoneyDetails' && (Auto + Manual) > 0
               )
           )
       ) As X,
       IfError(
           Remove('CH-Tbl-CountedMoneyDetails', X),
           If(true,
               Collect(colFallidasCon, { Clave: Text(X.Denomination), Detalle: "baja de denominación", Mensaje: FirstError.Message });
               Blank()
           )
       )
   );
   ```

   ```powerfx
   // MISMA forma para los cheques. Cubre TRES casos con un solo predicado:
   //   declarado desmarcado · manual borrado con la papelera · fila que desapareció
   //   porque cambió la moneda del depósito.
   //   🧨 Primero se suelta MatchedCheck del declarado, DESPUÉS se borra el cheque.
   ForAll(
       Filter(
           colChecksDVCon As X,
           IsBlank(LookUp(colChecksCon, GUID = X.'CH-Tbl-Checks' && Seleccionado))
       ) As X,
       If(
           !IsBlank(X.RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks'),
           IfError(
               Patch(
                   'CH-Tbl-DeclaredChecks',
                   LookUp(colChequesDeclaradosCon, 'CH-Tbl-DeclaredChecks' = X.RelatedDeclaredCheck.'CH-Tbl-DeclaredChecks'),
                   { MatchedCheck: Blank() }
               ),
               If(true,
                   Collect(colFallidasCon, { Clave: X.CheckNumber, Detalle: "MatchedCheck", Mensaje: FirstError.Message });
                   Blank()
               )
           )
       );
       IfError(
           Remove('CH-Tbl-Checks', X),
           If(true,
               Collect(colFallidasCon, { Clave: X.CheckNumber, Detalle: "baja de cheque", Mensaje: FirstError.Message });
               Blank()
           )
       )
   );
   ```

   - **El snapshot es la lista de lo que EXISTE en Dataverse** (`colDetalleDVCon` /
     `colChecksDVCon`, §4.2): una fila viva que nunca se guardó no aparece ahí y por eso no
     hay nada que borrar. Ese es el rol que antes cumplía el `!IsBlank(GUID)`.
   - Cada baja va en su **propio `IfError`** y una baja fallida **cuenta como fallida**.
   - **`MatchedCheck`, jamás `DeclaredDepositID`** (§2.4, fix de **D-6**).
3. **La UI NUNCA marca como guardado lo que el guard rechazó.** `gvarDepositoSinGuardar`
   pasa a `false` **solo si `colFallidasCon` está vacía**.
4. **Cero `Refresh()`** (regla D-5): el estado local se actualiza con lo que devolvió el
   `Patch` o con UNA consulta delegable.
5. **La receta define QUÉ se escribe, no CUÁNDO se dispara.** El disparo lo define la
   receta 7.10.C.

### 7.6 Armado del HTML de approval (las 6 secciones)

Las funciones viven en `App.Formulas` (§3.1). La pantalla solo **arma las filas** y llama.

**Paso 1 — materializar las 5 colecciones de hallazgos, con `Indice`.** Todas salen de
colecciones **locales** ya cargadas: cero red, cero delegación que romper.

**Paso 2 — 🧨 validar ANTES de leer.** La v1 validaba `colRecountedDeposits` **45 líneas
antes** del `ClearCollect` que la poblaba (bug **D-8**): el primer clic en *Terminar* no
reportaba ninguna diferencia por transacción. **En esta app el orden es, sin excepción:**

```
1. VOLCADO (receta 7.10.A)
2. ClearCollect de las 5 colecciones de hallazgos
3. ClearCollect(colHallazgosCon, …)   ← lee las 5 de arriba
4. If(IsEmpty(colHallazgosCon), <diálogo C4>, <diálogo C5>)
```

**Paso 3 — concatenar las filas.** `Concat()` sobre colección local, con el `Indice` para
la zebra:

```powerfx
// Seccion 1 — SIEMPRE las 3 monedas (nunca queda vacía)
UpdateContext({
    ctxSec1Con:
        fxHtmlFilaGlobal("NIO", SimboloNIO,
            ctxRecChqNIOCon, ctxRecMtoNIOCon, ctxRecEfeNIOCon,
            ctxDecChqNIOCon, ctxDecMtoNIOCon, ctxDecEfeNIOCon) &
        fxHtmlFilaGlobal("USD", SimboloUSD, /* … los 6 de USD … */) &
        fxHtmlFilaGlobal("EUR", SimboloEUR, /* … los 6 de EUR … */)
});

// Seccion 2 — diferencias por transacción
UpdateContext({
    ctxSec2Con:
        Concat(
            FirstN(SortByColumns(colDifTrxCon, "Indice"), 25) As T,
            fxHtmlFilaTrx(
                Text(T.Cuenta), T.Moneda, fxSimbolo(T.Moneda),
                T.DifCheques, T.DifEfectivo, Mod(T.Indice, 2) = 1
            )
        )
});

// Secciones 3 y 6 — depósitos sin enlazar (misma función, distinta colección)
// Secciones 4 y 5 — cheques (misma función, distinta colección); la 4 agrega
//                    fxHtmlFilaTotalCheques(...) al final.
```

**Paso 4 — armar el documento y patchear:**

```powerfx
UpdateContext({
    ctxHtmlCon:
        fxHtmlRevision(
            If(IsBlank(gvarDeclaredBag), "Reconteo sin declaracion", "Revision de reconteo"),
            Trim(gvarTula.BagID),
            gvarTula.ReceiptIdMirror,
            gvarTula.ReceptionID.RemitentName,
            gvarUser.'Full Name',
            gvarMesa.Name,
            Text(gvarSesion.'Created On', FmtFechaCorta),
            ctxSec1Con, ctxSec2Con, ctxSec3Con, ctxSec4Con, ctxSec5Con, ctxSec6Con
        )
});
// … y el Patch de §5.7 con RequestDetails: ctxHtmlCon
```

**Reglas:**

1. **Cero escapes manuales en la pantalla.** El escape vive dentro de las `fx…` (§3.1,
   regla 1). Si ves un `Substitute(…, "&", "&amp;")` en `02`/`03`/`04`, es un bug de la
   guía: significa que el dato se va a escapar **dos veces**.
2. **`FirstN(…, 25)` en cada sección**, por el límite de `ntext(10000)` (§3.1, regla 5). Si
   la colección tenía más, se agrega una fila `<tr><td colspan='5'>… y N más</td></tr>`.
3. **`ctxHtmlCon` se calcula UNA vez** y se pasa al `Patch`. Nunca se rearma dentro del
   `Patch`.
4. **El tipo de solicitud lo decide `IsBlank(gvarTula.BagDeclarationID)`**, no una variable
   paralela (§5.6).

### 7.7 Polling de aprobación (un timer, 15 s)

| Control | Tipo | Propiedades |
|---|---|---|
| `tmrPoll<Scr>` | **Temporizador clásico** | `AutoStart` `false` · `Duration` `15000` · `Repeat` `true` · **`Start` `ctxEsperando<Scr> && !IsBlank(gvarSolicitud.'CH-Tbl-ApprovalRequest')`** (§5.8) · `Visible` `false` · `OnTimerStart` **vacío** (cero `Refresh`) · `OnTimerEnd` = el bloque de §5.8 |
| `spnEspera<Scr>` | **Spinner moderno** | Width `34` · Height `34` · `Visible` `ctxEsperando<Scr>` — vive **dentro** de `cntEspera<Scr>` |
| `cntEspera<Scr>` | Contenedor de diseño manual (receta 7.2) | overlay bloqueante de **ESPERA** — **`Visible` `ctxEsperando<Scr>`** |
| `cntRechazo<Scr>` | Contenedor de diseño manual (receta 7.2) | overlay bloqueante de **RECHAZO** — **`Visible` `ctxRechazada<Scr>`** |

> 🧨 **Son DOS contenedores, no uno con `Visible: ctxEsperando \|\| ctxRechazada`.** Los dos
> estados no comparten **ni un** control: la espera lleva `.waitRow` + Spinner + `kv` +
> *Cancelar / Volver*; el rechazo lleva tile de peligro + banner con `StatusReason` +
> `SolvedBy` + *Entendido / Corregir*. Con un solo contenedor, **cada** hijo tendría que
> repetir su propia condición de `Visible` —dos verdades por control, ~20 lugares donde
> desincronizar— y la card no podría tener dos alturas encadenadas distintas. Con dos
> contenedores, la condición se escribe **una vez por overlay** y los hijos no llevan
> ninguna.

> **Los nombres son `cntEspera<Scr>` y `cntRechazo<Scr>`, SIN `Dlg`.** No son diálogos de
> decisión (no tienen `ctxDlg…`): son overlays de **estado del servidor**, y su `Visible` es
> la bandera de polling. `03` los nombra `cntEsperaTul` / `cntRechazoTul` y `04`
> `cntEsperaCon` / `cntRechazoCon`. `02` es la **excepción declarada** (`D02-1` / `L-22`):
> ahí la espera y el rechazo son **modos de la misma tarjeta**, no overlays, porque no hay
> nada detrás que preservar.

**Reglas:**

1. **UN timer por pantalla que espera, y ninguno más** (§5.8): `02`, `03` y `04` tienen el
   suyo; ninguna pantalla tiene dos y ninguna observa la solicitud de otra. La v1 tenía
   **tres en una sola pantalla**: dos con `Repeat true` a 5 s con `Refresh` en
   `OnTimerStart` (≈10 consultas en cascada por ciclo) y uno con `Start: =true` **literal**,
   corriendo siempre aunque no hubiera nada que esperar.
2. **`Start` es una condición, jamás `true`**, y cuelga del **estado de espera de su propia
   pantalla** (§5.8). Cuando la solicitud se resuelve, la rama que la resuelve baja
   `ctxEsperando<Scr>` y el timer se apaga solo: no hace falta apagarlo a mano.
3. **`OnTimerStart` vacío.** Un `Refresh` ahí invalida la caché de todas las tablas
   relacionadas en cada tick.
4. **El rechazo BLOQUEA** (§5.8). Nunca `IsBlank(…)` como condición de liberación.
5. **La espera es un overlay, no una pantalla**: mantiene el contexto detrás y evita una
   cuarta pantalla (decisión del mockup). **Excepción declarada**: en `02` no hay nada
   detrás que preservar y la espera es un **modo de la misma tarjeta** (`D02-1`).

### 7.8 Barra scan-first (`03`)

El input toma el foco al entrar y el lector de código de barras dispara la apertura con
**Enter**, sin tocar el mouse.

| Control | Tipo | Propiedades |
|---|---|---|
| `btnScanSupTul` | Botón clásico (receta 7.1) | la card blanca de la barra · `Radius` `13` · `Fill` `ThemeSurface` · `BorderColor` `ThemeLine`, `1` · **al FONDO** |
| `lblScanTitTul` | Etiqueta clásica | `Text` `"Abrir una tula"` · Size `10.13` · FontWeight `Semibold` |
| `btnScanCajaTul` | Botón clásico (receta 7.1) | el **marco de acento** del campo (`.scanIn`) · Height `44` · `Radius` `9` · `Fill`/`Hover`/`Pressed` `ThemeSurface` · `BorderColor` **`ThemeAccent`** · `BorderThickness` **`2`** · `DisplayMode` `DisplayMode.View` |
| `icoScanTul` | Icono clásico `Icon.Search` | X `btnScanCajaTul.X + 13` · Width `20` · Height `20` · `Color` `ThemeInk3` · `TabIndex` `-1` (decorativo) |
| `inpScanTul` | Entrada de texto clásica | X `icoScanTul.X + icoScanTul.Width + 10` · Height **`44`** · `Size` `12` · `FontWeight` `Semibold` · `BorderThickness` **`0`** (el borde lo dibuja `btnScanCajaTul`) · `Fill` `Color.Transparent` · `HintText` `"Escanee o digite el número de tula…"` · **`Format` `TextFormat.Text`** · `TabIndex` `0` |
| `btnAbrirTul` | Botón clásico | `ESTILO-BTN-GRANDE` · `Text` `"Abrir tula"` · Height `44` · `DisplayMode` `If(gvarIdentidadOk && !IsBlank(Trim(inpScanTul.Text)), DisplayMode.Edit, DisplayMode.Disabled)` |
| `lblScanNotaTul` | Etiqueta clásica | `ESTILO-META-CHICA` · Wrap `true` · Height `30` · el texto de ayuda del mockup |

**Reglas:**

1. 🧨 **El Enter se implementa con `OnChange`, no con un atajo de teclado.** En la Entrada
   de texto clásica **`OnChange` dispara al presionar Enter** (y al perder el foco). Es el
   único mecanismo nativo: el `KeyboardShortcuts` del Creator Kit está prohibido (§0.3).

   ```powerfx
   // inpScanTul.OnChange
   If(!IsBlank(Trim(Self.Text)), Select(btnAbrirTul))
   ```

   Corolario: **toda la lógica de apertura vive en `btnAbrirTul.OnSelect`**, y `OnChange`
   solo lo invoca. Dos copias serían dos verdades.
2. **El foco al entrar**: `SetFocus(inpScanTul)` como **última** línea del `OnVisible` de
   `scrTulas`. `SetFocus` apunta a un control **de pantalla**; nunca a uno de template.
3. **Después de abrir la tula** (o de cancelar un diálogo de desambiguación):
   `Reset(inpScanTul); SetFocus(inpScanTul)` — el cajero encadena tulas sin tocar el mouse.
4. **`Format: TextFormat.Text`**, nunca `Number`: los números de tula pueden tener ceros a
   la izquierda (`ST000123`).

### 7.9 Fila de denominación y badge **Auto** de solo lectura

Grilla teclado-primero: TAB baja por la columna **Cantidad** y los totales se recalculan en
vivo desde `colDetalleConteoCon`.

**Columnas** (mockup `.denHead/.denRow`, `grid-template-columns: 1.15fr 74px 104px 1.25fr`):

> 🧨 **ESTA RECETA SE DESDOBLA EN DOS GRILLAS.** `04` no tiene una grilla de denominaciones:
> tiene **dos** —*Billetes* y *Monedas*, pestañas distintas de `tabDetalleCon`—, y por eso
> los nombres de abajo llevan el **placeholder `<Bil|Mon>`**: `Bil` en la de billetes, `Mon`
> en la de monedas. Los nombres finales, con sus coordenadas, están en **`04` §8.1**
> (billetes) y **`04` §8.2** (monedas); acá viven las propiedades comunes.
> **`btnAuto<Bil|Mon>Con` existe SOLO en la variante `Bil`**: la contadora no procesa moneda
> (regla 5), así que la grilla de monedas no tiene columna *Auto* ni badge. **No existe
> ningún control llamado `lblDenCon`, `btnAutoCon`, `inpCantidadCon`, `lblTotFilaCon` ni
> `galDenominacionesCon`**: si los ves escritos en algún lado, es una guía vieja.

| Columna | Ancho | Alineación | Control |
|---|---|---|---|
| Denominación | `1.15fr` | derecha | `lblDen<Bil\|Mon>Con` — `ESTILO-DEN`, `Text` `fxSimbolo(ctxMonedaCon) & Text(ThisItem.Denominacion, "#,##0.##")` |
| **Auto** *(solo `Bil`)* | `74` | centro | `btnAutoBilCon` — badge, `ESTILO-AUTO` |
| Cantidad | `104` | derecha | `inpCantidad<Bil\|Mon>Con` — `ESTILO-QIN` |
| Total | `1.25fr` | derecha | `lblTotFila<Bil\|Mon>Con` — `ESTILO-DEN-TOT` |

| Control | Tipo | Propiedades clave |
|---|---|---|
| `btnAutoBilCon` *(solo Billetes)* | **Botón clásico** en `DisplayMode.View` (receta 7.1) | `Text` `Text(ThisItem.Auto)` · `ESTILO-AUTO` · **`TabIndex` `-1`** · `OnSelect` **vacío** |
| `inpCantidad<Bil\|Mon>Con` | Entrada de texto clásica | `Default` `Text(ThisItem.Manual)` · `Format` `TextFormat.Number` · `ESTILO-QIN` · **`TabIndex` `0`** · `OnChange` **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** (receta 7.10.A) · `BorderColor` `If(ThisItem.Manual > 0, ThemeAccent, ThemeLine)` · `BorderThickness` `If(ThisItem.Manual > 0, 2, 1)` |
| `lblTotFilaBilCon` | Etiqueta clásica | `Text` `fxMonto(ThisItem.Denominacion * (ThisItem.Auto + Value(Coalesce(inpCantidadBilCon.Text, "0"))), fxSimbolo(ctxMonedaCon))` |
| `lblTotFilaMonCon` | Etiqueta clásica | ídem **SIN `ThisItem.Auto`** (regla 5): `fxMonto(ThisItem.Denominacion * Value(Coalesce(inpCantidadMonCon.Text, "0")), fxSimbolo(ctxMonedaCon))` |

**Reglas del badge Auto:**

1. 🧨 **Auto es de SOLO LECTURA.** Es un **Botón clásico en `DisplayMode.View`**, no una
   entrada. El cajero escribe **solo** en *Cantidad*. La v1 mezclaba las dos y por eso
   nadie sabía qué parte del conteo venía de la máquina.
2. 🧨 **Auto se REINICIA por depósito** (§4.4). El texto del encabezado lo dice al usuario:
   *"Billetes · el conteo automático se reinicia en cada depósito"*.
3. **Lo persistido es `Auto + Manual`** (§2.4). El pie de la grilla muestra **las dos sumas
   por separado** (`Total billetes: 435 / 86`) para que el arqueo se reconstruya.
4. **`TabIndex -1` en el badge**: no debe entrar en el recorrido de teclado, o el TAB
   deja de bajar por *Cantidad*.
5. **La pestaña *Monedas* NO tiene columna Auto** (la contadora no procesa moneda): su
   grilla usa 3 columnas (`1.15fr 104px 1.25fr`), no lleva `btnAutoMonCon` —**ese control no
   existe**— y su total de fila no suma `ThisItem.Auto`. El aviso de la banda lo dice: *"sin
   lectura automática: la contadora no procesa moneda"*.

**Pie de la grilla** (`denFoot`): `Fill` `ThemeSurface2`, borde superior `ThemeLine`,
`ESTILO-FOOT`. Los tres números salen de `colDetalleConteoCon`, **nunca** de `AllItems`
(el ejemplo es el de *Billetes*; en *Monedas* se filtra `Tipo = "Moneda"` y **no hay** suma
de `Auto`):

```powerfx
Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Auto)                       // total auto
Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Manual)                     // total manual
Sum(Filter(colDetalleConteoCon, Tipo = "Billete"), Denominacion * (Auto + Manual))  // importe
```

### 7.10 🧨 Volcado, foco y guardado diferido (patrón de grilla editable)

Heredada de `reception-rebuild/01-app.md` §7.11, nacida del QA humano de la 1.31. Cubre
tres problemas que se manifiestan juntos en toda grilla canvas editable.

#### A. El `OnChange` NO patchea la colección — el VOLCADO va en los botones

**Síntoma**: el cajero teclea tres caracteres en una cantidad y el foco se le va.
**Causa**: un `Patch` a la colección dentro de un `OnChange` **muta el `Items` de la
galería**, la galería se re-renderiza y el control que tenía el foco deja de ser el mismo.
Ninguna cantidad de `DelayOutput` lo arregla.

| Dónde | Qué corre |
|---|---|
| `OnChange` de **cada** control editable de la fila | **`Set(gvarDepositoSinGuardar, true)` y NADA MÁS** |
| `OnSelect` de **cada** botón que necesita la colección al día | el **VOLCADO**, como **primera** instrucción |

🧨 **La forma canónica del volcado es el BLOQUE V de `04` §6.2, y no hay una segunda.**
Son **tres** `ForAll` —uno por galería: `galBilletesCon`, `galMonedasCon`, `galChequesCon`—
con los nombres finales de los controles de cada template y con el tratamiento de las filas
**manuales** de cheques (que conservan `Seleccionado: true` y leen sus inputs, mientras las
declaradas conservan `Numero`, `Monto` y `Tipo` porque sus controles editables **no
existen**). Vive **una sola vez escrito**, en el `OnSelect` del motor `btnVolcarGrillaCon`
(`04` §6.2), y los nueve botones de la regla 2 lo invocan con `Select(btnVolcarGrillaCon)`
como **primera** instrucción. **Acá viven las reglas; el código vive allá.**

**Reglas del volcado:**

1. **`As Fila` no es opcional**: sin el alias, `Fila.inpCantidadBilCon` no se resuelve y las
   columnas se ambiguan con las de la colección.
2. **Va PRIMERO, siempre.** Lista cerrada de los botones de `04` que lo llevan (los nueve
   arrancan con `Select(btnVolcarGrillaCon)`): `btnGuardarDepositoCon`,
   `btnNuevoDepositoCon`, `btnDescartarCon`, `btnTerminarCon`, `btnLeerContadoraCon`,
   `btnEliminarDepositoCon`, `btnEnlazarCon`, `btnDesenlazarCon`, `btnAgregarChequeCon`. Si
   agregás un botón que lea `colDetalleConteoCon` o `colChecksCon`, **lleva volcado** y
   **entra en esta lista y en la de `04` §6.2, en el mismo commit**.
3. **`gal….AllItems` solo devuelve las filas MATERIALIZADAS.** Una fila que nunca se pintó
   no está en `AllItems` — pero tampoco pudo editarse, así que su registro ya es el bueno.
   El `LookUp` por clave hace que el volcado sea un *merge*, no un reemplazo.
4. **`Coalesce(Value(…), 0)`**, nunca `Value(…)` pelado: un input vacío da *blank* y la
   suma entera se vuelve *blank*.
5. **`AllItems` NO se usa para nada más** (regla D-8).

#### B. Gotchas de FOCO en galerías (tabla cerrada)

| Gotcha | Qué pasa | Qué se hace |
|---|---|---|
| **`TabIndex` positivo dentro de un template** | Cada fila **replica** el mismo número y el orden global agrupa **por número**, no por fila: el TAB salta a la misma columna de todas las filas antes de bajar | **`TabIndex` = `0` SIEMPRE** en todo control de template + orden **geométrico**. Un `TabIndex` positivo dentro de una galería es un bug de la guía |
| **Íconos, Imágenes y Formas nacen con `TabIndex -1`** | Quedan fuera del recorrido de TAB | `TabIndex` **`0` explícito** en todo Icono/Forma **interactivo**; los decorativos (lupa, badge Auto, chevron) se dejan en `-1` |
| **Acciones destructivas en el recorrido** | Un TAB de más y un Enter borran una fila | La papelera del cheque manual queda **FUERA del TAB** (`TabIndex -1`): se toca con el mouse, a propósito |
| **`Patch` a la colección en un `OnChange`** | Re-render ⇒ **pérdida de foco** | Bloque **A** |
| **`Reset(gal…)` con la galería visible** | Re-ancla los `Default` y **mata el foco** | Solo en `OnVisible` o al cambiar de depósito, nunca en un `OnChange` |
| **`SetFocus` a un control de template** | No está soportado | El foco se devuelve a un control **de pantalla** (`SetFocus(inpScanTul)`) |

#### C. Guardado diferido: el timer de UN frame

**Síntoma**: el diálogo *"Guardando…"* nunca se ve. **Causa**: canvas **no repinta a mitad
de una fórmula**.

| Control | Propiedades |
|---|---|
| `tmrGuardarCon` | **Temporizador clásico** · `AutoStart` `false` · `Duration` `50` · `Repeat` **`false`** · **`Start` = `ctxGuardandoCon`** · `Visible` `false` · `OnTimerEnd` = TODO el trabajo pesado |

```powerfx
// ===== btnGuardarDepositoCon.OnSelect — BARATO. Ni un Patch a Dataverse acá. =====
<VOLCADO del bloque A>;
<validación local>;
UpdateContext({ ctxDlgGuardandoCon: true, ctxGuardandoCon: true })

// ===== tmrGuardarCon.OnTimerEnd — TODO el trabajo pesado =====
<receta 7.5: depósito, denominaciones, cheques, bajas>;
<recarga de colDepRecontadosCon con UNA consulta delegable>;
UpdateContext({ ctxDlgGuardandoCon: false, ctxGuardandoCon: false })
```

**Reglas:**

1. **`Start` es la bandera, no `true`.** `Repeat false` + `Duration 50` = **una** corrida
   por clic.
2. 🧨 **Los timers NO corren en pantallas que no están visibles.** Todo flujo que termine
   navegando **navega DESDE el `OnTimerEnd`**, nunca antes.
3. **Toda continuación va adentro del `OnTimerEnd`**, no después del `Select(btn…)` que lo
   disparó.
4. **Sin spinner animado en este diálogo**: entre el frame que lo pinta y el `OnTimerEnd`
   el hilo se bloquea igual. Texto quieto. *(El Spinner moderno sí se usa en la espera de
   **aprobación**, receta 7.7, donde el hilo está libre.)*
5. 🧨 **Esto NO resucita el `'timer Submit'` de la v1**, que era una **máquina de estados**
   con `varCurrentRow`/`varFirstRow`/`varRowIndex` iterando fila por fila. Acá el timer es
   un **defer de UN frame**, corre una sola vez y no lleva estado de iteración. Si ves un
   `Repeat true` o un contador de fila en un timer de guardado, eso sí es la v1 volviendo.

### 7.11 Lectura de la máquina contadora

Un flujo, una consulta delegable y una regla de reinicio. Mata **D-1** y **D-4**.

> 🧨 **El `OnSelect` COMPLETO de `btnLeerContadoraCon` vive en `04` §8.4, y esa es la ÚNICA
> forma canónica.** Acá viven **las reglas, los dos parámetros del flujo y la consulta
> delegable**; allá vive el código, con sus guardas, sus `IfError`, el
> `Set(gvarDepositoSinGuardar, true)` y el `Reset(galBilletesCon)` que repinta el badge de
> cada fila. Antes esta sección repetía el `OnSelect` entero **sin** esas dos últimas líneas:
> dos versiones del mismo botón, y la de acá ya estaba desincronizada.

**Los dos parámetros del flujo — POSICIONALES, en este orden:**

```powerfx
'CH-Flow-ProcessXMLFiles'.Run(
    Text(gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'),   // 1º ID_Sesion
    gvarMesa.MachineSerialNumber                                // 2º Serial_Maquina
)
// El flujo responde { status: "Completado" }; cualquier otro valor es error.
```

**La consulta — UNA, delegable, de un salto (🧨 espejo E-4) + agregación LOCAL sobre el
snapshot:**

```powerfx
// ----- a) SNAPSHOT: la consulta delegable, cruda, a una colección temporal -----
ClearCollect(
    colLecturasTmpCon,
    Filter(
        'CH-Tbl-XMLReadingDetails',
        RecountingSessionDetailMirror.'CH-Tbl-RecountingSessionDetail'
            = gvarSesionDetalle.'CH-Tbl-RecountingSessionDetail'
        && XMLReadingActiva = true
    )
);

// ----- b) AGREGACIÓN LOCAL: Distinct + Sum, proyectando al esquema de la colección -----
ClearCollect(
    colAutoContadoraCon,
    ForAll(
        Distinct(colLecturasTmpCon, Denomination) As D,
        {
            Denominacion: D.Value,
            Cantidad: Sum(
                Filter(colLecturasTmpCon, Denomination = D.Value),
                Quantity
            )
        }
    )
);
Clear(colLecturasTmpCon);
```

> 🧨 **NADA de `GroupBy`/`AddColumns` acá.** `GroupBy` es **quisquilloso**: exige que el
> nombre del grupo no choque con ninguna columna existente, no acepta cualquier fuente y la
> tabla anidada que produce (`GruposXml`) hace que el `Sum` de `AddColumns` falle en runtime
> contra el resultado de un `Filter` sobre datasource. La forma **validada en Studio** —y la
> que está desplegada— es **`Distinct` + `Sum` sobre un snapshot local**, proyectando
> explícitamente a `{ Denominacion, Cantidad }`. Ledger **`L-64`**.
>
> **`Distinct()` devuelve una columna llamada `Value`**: por eso se lee `D.Value`, no
> `D.Denomination`. Y la proyección es lo que fija el esquema en **español**
> (`Denominacion`), que es el que declara `01` §4.2 y el que siembra `App.OnStart` —así el
> `LookUp` del volcado no depende de cómo nombre las columnas el agregador de turno.
>
> **`colLecturasTmpCon` es LOCAL del paso**: nace en (a), muere en el `Clear` de (b), y **no**
> entra al inventario de §4.2 **ni se siembra en `App.OnStart`** — ningún `Items`, `Visible`
> ni `IsEmpty()` la lee.

**El volcado de `Auto` a la grilla del depósito EN CURSO:**

```powerfx
ForAll(
    colDetalleConteoCon As M,
    Patch(
        colDetalleConteoCon,
        M,
        { Auto: Coalesce(LookUp(colAutoContadoraCon, Denominacion = M.Denominacion).Cantidad, 0) }
    )
)
```

**Reglas:**

1. 🧨 **El `Filter` es de UN salto gracias al espejo E-4.** La v1 escribía
   `Filter(XMLReadingDetails, XMLReading.XMLReadingId in Filter(XMLReadings, …).XMLReadingId)`
   — un `in` con subconsulta que **jamás delega** (regla D-6). Con >500 filas históricas la
   columna Auto mostraba **0** y el cajero recontaba con totales de máquina equivocados.
   Fallback sin backend en §2.2.
2. 🧨 **`Clear(colAutoContadoraCon)` al cambiar de depósito** (§4.4). Sin eso, la lectura de
   la máquina se hereda en cada depósito y el efectivo se duplica.
3. **`Auto` solo se escribe en las denominaciones del depósito EN CURSO**, y solo si la
   moneda coincide: `colDetalleConteoCon` ya está filtrada por la moneda del depósito
   (§4.2), así que el `ForAll` no puede cruzar monedas.
4. **`Coalesce(…, 0)`**: una denominación que la máquina no leyó vale `0`, no *blank*.
5. **Los parámetros del flujo son POSICIONALES** (`Run(ID_Sesion, Serial_Maquina)`), en ese
   orden. El flujo responde `{ status: "Completado" }`; cualquier otro valor es error.
6. **Cero `Notify` con el serial ni con GUIDs** (**S-6**).
7. 🧨 **El `Text()` del primer parámetro es un supuesto, no un hecho verificado** (ledger
   `L-60`). `'CH-Tbl-RecountingSessionDetail'` es un **GUID**, y acá se manda como **texto**
   porque el `ID_Sesion` que la v1 pasaba viajaba como texto. **Al construir `04` §8.4 hay
   que verificar el tipo del parámetro contra el flujo real** (Power Automate → *Cuando Power
   Apps llama a un flujo* → tipo del parámetro): **si el flujo espera un GUID crudo, se quita
   el `Text()`** y se anota. Un tipo equivocado no falla en tiempo de diseño: falla en
   runtime con un mensaje del conector, y el cajero cree que la contadora no responde.

---

## 8. Componentes canvas

Se construyen **ANTES que las pantallas** (regla 0.12): `03` y `04` insertan sus instancias
en el primer pase del árbol.

**Cómo se crean**: árbol de la izquierda → **Componentes** → *Nuevo componente* → renombrar
→ fijar `Width`/`Height` en el panel de propiedades → *Nueva propiedad personalizada* para
cada entrada/salida.

> 🧨🧨 **`AccessAppScope` (*Permitir el acceso a ámbitos de la aplicación*, `AllowAccessToGlobals`)
> va en **`true`** en LOS TRES.** Es el estado real desplegado, y no es opcional: los tres
> componentes leen los **tokens `Theme*`** de la paleta —los tres— y `cmpMatrizMoneda` y
> `cmpKpiSesion` leen además las **`fx…`** (`fxMonto`, `fxMontoDif`, `fxCantDif`,
> `fxColorDif`) de **`App.Formulas`** (§3.1), que **es ámbito de aplicación**.
> Con la bandera en `false` **los tres revientan**:
> cada `ThemeInk`, cada `fxMonto(…)` queda en rojo y el componente no compila. Ledger
> **`L-67`**.

> 🧨 **La bandera NO es una puerta para los datos.** Habilitarla no cambia el contrato:
> **cero datasources, cero variables globales (`gvar…`), cero colecciones (`col…`) dentro de
> un componente.** Los datos **siguen entrando por propiedades y saliendo por propiedades**,
> sin excepción. Lo único que la bandera concede —y lo único que se usa— es leer el
> **sistema de diseño** (`Theme*`) y las **funciones puras** de `App.Formulas`, que son
> constantes de la app, no estado. Un componente que lea `gvarTula` o `colChecksCon` deja de
> ser reutilizable y se vuelve imposible de testear: **eso** sigue siendo un bug de la guía.
>
> **Verificación**: en el `Src/` desempaquetado, los tres `Components/cmp*.json` tienen
> `"AllowAccessToGlobals": true`, y `rg "gvar|colChecksCon|'CH-Tbl-" Src/Components/*.fx.yaml`
> devuelve **cero**.

> 🧨 **`Visible`, `Width`, `Height`, `X`, `Y`, `Fill` NO se declaran como propiedades
> personalizadas**: ya son propiedades **nativas de la instancia**. Studio rechaza una
> propiedad personalizada con esos nombres. Donde el plan pide *"Visible (booleano de
> entrada)"*, se usa la propiedad nativa `Visible` de la instancia (divergencia **`L-7`**).

**Tipos de propiedad usados** (los tres únicos que hacen falta): **Entrada** (`Texto`,
`Número`, `Booleano`), **Salida** (`Número`, `Booleano`) y **Comportamiento** (los
`On…`, que necesitan la bandera de §1.4).

---

### 8.1 `cmpMatrizMoneda` — matriz Rec / Dec / Diff × (Cheques · Monto cheques · Efectivo)

Reemplaza los ~60 labels sueltos (`Label6_*`) con los que la v1 dibujaba la misma matriz
cuatro veces. Mockup `#c0`, clases `.mat` / `.matT`.

**Tamaño por defecto: `435 × 100`.** *(La franja de `04` mide `1366 − 36 = 1330` de ancho
útil con `gap 12` ⇒ `(1330 − 24) / 3 = 435,33`. Las instancias encadenan su `X` contra la
anterior, regla 0.9.)*

**Instancias**: `cmpMatNIOCon`, `cmpMatUSDCon`, `cmpMatEURCon` (franja global de `04`, las
tres con el tamaño por defecto y `Card` `true`) y `cmpMatDepositoCon` (matriz del depósito
en curso, panel derecho — `580 × 100`, con **`Card` `false`**: sin borde, sin radio y sobre
`ThemeSurface2`, que es la banda plana que dibuja el mockup).

> 🧨 **`Height 100`, no `92`.** El contenido del componente cierra en `95`
> (`lblDifLabMat.Y 79 + Height 16`): con `92` la fila **Diferencia** —la que trae el número
> que importa— se recorta. El alto es el mismo en las cuatro instancias.

#### Propiedades personalizadas

| Nombre | Dirección | Tipo | Default | Para qué |
|---|---|---|---|---|
| `Moneda` | Entrada | Texto | `"NIO"` | código en mayúsculas del encabezado |
| `MonedaNombre` | Entrada | Texto | `"Córdobas"` | nombre largo, al lado del código |
| `Simbolo` | Entrada | Texto | `"C$ "` | prefijo de los importes (viene de `fxSimbolo()`) |
| `Titulo` | Entrada | Texto | `""` | si **no** está en blanco, reemplaza `Moneda`/`MonedaNombre` por un título libre (*"Depósito 100501234"*) — es la variante del panel derecho |
| `Card` | Entrada | Booleano | `true` | `true` = card (`ThemeSurface` + borde `ThemeLine` + `Radius 11`), la franja de `04`. `false` = **banda plana** (`ThemeSurface2`, sin borde ni radio), la matriz del depósito en curso |
| `ChequesRec` | Entrada | Número | `0` | cantidad de cheques recontados |
| `MontoChequesRec` | Entrada | Número | `0` | importe de cheques recontados |
| `EfectivoRec` | Entrada | Número | `0` | efectivo recontado |
| `ChequesDec` | Entrada | Número | `0` | cantidad de cheques declarados |
| `MontoChequesDec` | Entrada | Número | `0` | importe de cheques declarados |
| `EfectivoDec` | Entrada | Número | `0` | efectivo declarado |
| `DifCheques` | **Salida** | Número | — | `ChequesRec - ChequesDec` |
| `DifMonto` | **Salida** | Número | — | `MontoChequesRec - MontoChequesDec` |
| `DifEfectivo` | **Salida** | Número | — | `EfectivoRec - EfectivoDec` |
| `HayDiferencia` | **Salida** | Booleano | — | `DifCheques <> 0 \|\| DifMonto <> 0 \|\| DifEfectivo <> 0` |

> 🧨 **Las 3 diferencias NO son entradas: se CALCULAN dentro** (divergencia **`L-8`** — el
> plan hablaba de "los 9 valores"). Si entraran como parámetro habría **dos verdades** para
> el mismo número y la primera vez que se toque una fórmula se van a desincronizar. Salen
> como **propiedades de salida** para que `04` las use en las validaciones de *Terminar*
> sin recalcularlas.

#### Árbol de controles

```
cmpMatrizMoneda                       (435 × 100)
├─ btnSupMat            Botón clásico   (superficie, receta 7.1)  ← al FONDO
├─ lblCodigoMat         Etiqueta clásica
├─ lblNombreMat         Etiqueta clásica
├─ lblThChqMat          Etiqueta clásica
├─ lblThMtoMat          Etiqueta clásica
├─ lblThEfeMat          Etiqueta clásica
├─ shpLineaThMat        Rectángulo clásico
├─ lblRecLabMat         Etiqueta clásica
├─ lblRecChqMat         Etiqueta clásica
├─ lblRecMtoMat         Etiqueta clásica
├─ lblRecEfeMat         Etiqueta clásica
├─ lblDecLabMat         Etiqueta clásica
├─ lblDecChqMat         Etiqueta clásica
├─ lblDecMtoMat         Etiqueta clásica
├─ lblDecEfeMat         Etiqueta clásica
├─ shpLineaDifMat       Rectángulo clásico
├─ lblDifLabMat         Etiqueta clásica
├─ lblDifChqMat         Etiqueta clásica
├─ lblDifMtoMat         Etiqueta clásica
└─ lblDifEfeMat         Etiqueta clásica
```

#### Tabla de propiedades

**Rejilla de columnas** (se enuncia una vez): margen interno `14`; columna de etiqueta
`X 14 W 100`. Las tres numéricas **se anclan al borde DERECHO** y encadenan hacia la
izquierda, para que el componente sirva en sus dos anchos:

| Columna | Width | `X` (una sola definición, en la cabecera) |
|---|---|---|
| `Efectivo` | `107` | `lblThEfeMat.X = Parent.Width - 14 - 107` |
| `Monto cheques` | `110` | `lblThMtoMat.X = lblThEfeMat.X - 110` |
| `Cheques` | `90` | `lblThChqMat.X = lblThMtoMat.X - 90` |

Las **12 celdas** de las filas *Recontado* / *Declarado* / *Diferencia* copian la `X` de su
encabezado (`lblRecEfeMat.X = lblThEfeMat.X`, etc.): la rejilla tiene **una** definición.
Las tres numéricas van `Align.Right`.

> 🧨 **Por qué parametrizado y no literales.** Con `Width 435` (las tres instancias de la
> franja) da exactamente `314 / 204 / 114`: **la franja de `04` no se mueve un píxel**. Con
> `Width 580` (`cmpMatDepositoCon`) da `459 / 349 / 259` y los importes quedan **pegados al
> borde derecho**, como el mockup — con los literales viejos el bloque numérico quedaba
> flotando a 145 px del borde, con un hueco muerto a la derecha.

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnSupMat` | Botón clásico | `0` · `0` · `Parent.Width` · `Parent.Height` | receta 7.1 · `Fill`/`HoverFill`/`PressedFill` **`If(cmpMatrizMoneda.Card, ThemeSurface, ThemeSurface2)`** · `BorderColor` `ThemeLine` · `BorderThickness` **`If(cmpMatrizMoneda.Card, 1, 0)`** · `Radius` (las 4) **`If(cmpMatrizMoneda.Card, 11, 0)`** · `DisplayMode` `DisplayMode.View` |
| `lblCodigoMat` | Etiqueta clásica | `14` · `8` · `44` · `15` | `ESTILO-KPI-LAB` · `Text` `If(IsBlank(cmpMatrizMoneda.Titulo), Upper(cmpMatrizMoneda.Moneda), "")` · `Visible` `IsBlank(cmpMatrizMoneda.Titulo)` |
| `lblNombreMat` | Etiqueta clásica | `lblCodigoMat.X + lblCodigoMat.Width + 7` · `8` · `Parent.Width - Self.X - 14` · `15` | Size `8.25` · Color `ThemeInk` · `Text` `If(IsBlank(cmpMatrizMoneda.Titulo), cmpMatrizMoneda.MonedaNombre, cmpMatrizMoneda.Titulo)` |
| `lblThChqMat` | Etiqueta clásica | **`lblThMtoMat.X - 90`** · `27` · `90` · `12` | Size `7.13` · FontWeight `Semibold` · Color `ThemeInk3` · Align `Align.Right` · `Text` `"CHEQUES"` |
| `lblThMtoMat` | Etiqueta clásica | **`lblThEfeMat.X - 110`** · `27` · `110` · `12` | ídem · `Text` `"MONTO CHEQUES"` |
| `lblThEfeMat` | Etiqueta clásica | **`Parent.Width - 14 - 107`** · `27` · `107` · `12` | ídem · `Text` `"EFECTIVO"` |
| `shpLineaThMat` | Rectángulo clásico | `14` · `lblThChqMat.Y + lblThChqMat.Height + 2` · `Parent.Width - 28` · `1` | `Fill` `ThemeLineSoft` |
| `lblRecLabMat` | Etiqueta clásica | `14` · `shpLineaThMat.Y + 3` · `100` · `15` | `ESTILO-MAT-LAB` · `Text` `"Recontado"` |
| `lblRecChqMat` | Etiqueta clásica | **`lblThChqMat.X`** · `lblRecLabMat.Y` · `90` · `15` | `ESTILO-MAT-VAL` · `Text` `Text(cmpMatrizMoneda.ChequesRec)` |
| `lblRecMtoMat` | Etiqueta clásica | **`lblThMtoMat.X`** · `lblRecLabMat.Y` · `110` · `15` | `ESTILO-MAT-VAL` · `Text` `fxMonto(cmpMatrizMoneda.MontoChequesRec, cmpMatrizMoneda.Simbolo)` |
| `lblRecEfeMat` | Etiqueta clásica | **`lblThEfeMat.X`** · `lblRecLabMat.Y` · `107` · `15` | `ESTILO-MAT-VAL` · `Text` `fxMonto(cmpMatrizMoneda.EfectivoRec, cmpMatrizMoneda.Simbolo)` |
| `lblDecLabMat` | Etiqueta clásica | `14` · `lblRecLabMat.Y + lblRecLabMat.Height` · `100` · `15` | `ESTILO-MAT-LAB` · `Text` `"Declarado"` |
| `lblDecChqMat` | Etiqueta clásica | **`lblThChqMat.X`** · `lblDecLabMat.Y` · `90` · `15` | `ESTILO-MAT-VAL` · `Text` `Text(cmpMatrizMoneda.ChequesDec)` |
| `lblDecMtoMat` | Etiqueta clásica | **`lblThMtoMat.X`** · `lblDecLabMat.Y` · `110` · `15` | `ESTILO-MAT-VAL` · `Text` `fxMonto(cmpMatrizMoneda.MontoChequesDec, cmpMatrizMoneda.Simbolo)` |
| `lblDecEfeMat` | Etiqueta clásica | **`lblThEfeMat.X`** · `lblDecLabMat.Y` · `107` · `15` | `ESTILO-MAT-VAL` · `Text` `fxMonto(cmpMatrizMoneda.EfectivoDec, cmpMatrizMoneda.Simbolo)` |
| `shpLineaDifMat` | Rectángulo clásico | `14` · `lblDecLabMat.Y + lblDecLabMat.Height + 2` · `Parent.Width - 28` · `1` | `Fill` `ThemeLineSoft` |
| `lblDifLabMat` | Etiqueta clásica | `14` · `shpLineaDifMat.Y + 3` · `100` · `16` | `ESTILO-MAT-LAB` · FontWeight `Bold` · `Text` `"Diferencia"` |
| `lblDifChqMat` | Etiqueta clásica | **`lblThChqMat.X`** · `lblDifLabMat.Y` · `90` · `16` | `ESTILO-MAT-DIF` · `Text` `fxCantDif(cmpMatrizMoneda.DifCheques)` · `Color` `fxColorDif(cmpMatrizMoneda.DifCheques)` |
| `lblDifMtoMat` | Etiqueta clásica | **`lblThMtoMat.X`** · `lblDifLabMat.Y` · `110` · `16` | `ESTILO-MAT-DIF` · `Text` `fxMontoDif(cmpMatrizMoneda.DifMonto, cmpMatrizMoneda.Simbolo)` · `Color` `fxColorDif(cmpMatrizMoneda.DifMonto)` |
| `lblDifEfeMat` | Etiqueta clásica | **`lblThEfeMat.X`** · `lblDifLabMat.Y` · `107` · `16` | `ESTILO-MAT-DIF` · `Text` `fxMontoDif(cmpMatrizMoneda.DifEfectivo, cmpMatrizMoneda.Simbolo)` · `Color` `fxColorDif(cmpMatrizMoneda.DifEfectivo)` |

**Fórmulas de las propiedades de SALIDA** (se escriben en el panel de propiedades del
componente, no en un control):

```powerfx
DifCheques    = cmpMatrizMoneda.ChequesRec      - cmpMatrizMoneda.ChequesDec
DifMonto      = cmpMatrizMoneda.MontoChequesRec - cmpMatrizMoneda.MontoChequesDec
DifEfectivo   = cmpMatrizMoneda.EfectivoRec     - cmpMatrizMoneda.EfectivoDec
HayDiferencia = cmpMatrizMoneda.DifCheques <> 0 || cmpMatrizMoneda.DifMonto <> 0 || cmpMatrizMoneda.DifEfectivo <> 0
```

> **Divergencias declaradas vs. el mockup**: el `border-collapse` con `border-bottom` en el
> `th` se aproxima con `shpLineaThMat` (1 px, `ThemeLineSoft`); el `font-variant-numeric:
> tabular-nums` **no existe** en canvas — los números se alinean a la derecha y se acepta
> el ancho variable de los dígitos de Open Sans.

---

### 8.2 `cmpKpiSesion` — tarjeta KPI del rail de `scrTulas`

Mockup `#b0`, clase `.kpi`. **Instancias**: `cmpKpiNIOTul`, `cmpKpiUSDTul`, `cmpKpiEURTul`.

**Tamaño por defecto: `226 × 100`.** *(El rail mide `250` con `padding: 14px 12px` ⇒
`250 − 24 = 226`. El alto es `100`, no `86`: con `86` el bloque **TOTAL** —`lblTotValKpi`,
Size `11.25`— quedaba pegado a la línea y sin aire abajo. Con `100` el contenido cierra en
`97` y quedan `3` de aire, la misma respiración que el `.kpi` del mockup.)*

#### Propiedades personalizadas

| Nombre | Dirección | Tipo | Default | Para qué |
|---|---|---|---|---|
| `Moneda` | Entrada | Texto | `"NIO"` | código, arriba a la derecha |
| `MonedaNombre` | Entrada | Texto | `"Córdobas"` | nombre largo, arriba a la izquierda |
| `Simbolo` | Entrada | Texto | `"C$ "` | prefijo de los importes |
| `Efectivo` | Entrada | Número | `0` | efectivo de las tulas **recontadas** de la sesión |
| `Cheques` | Entrada | Número | `0` | cheques de las tulas **recontadas** de la sesión |
| `Total` | **Salida** | Número | — | `Efectivo + Cheques` |

#### Árbol de controles

```
cmpKpiSesion                          (226 × 100)
├─ btnSupKpi            Botón clásico   (superficie, receta 7.1)  ← al FONDO
├─ lblNombreKpi         Etiqueta clásica
├─ lblCodigoKpi         Etiqueta clásica
├─ lblEfeLabKpi         Etiqueta clásica
├─ lblEfeValKpi         Etiqueta clásica
├─ lblChqLabKpi         Etiqueta clásica
├─ lblChqValKpi         Etiqueta clásica
├─ shpLineaKpi          Rectángulo clásico
├─ lblTotLabKpi         Etiqueta clásica
└─ lblTotValKpi         Etiqueta clásica
```

#### Tabla de propiedades

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| `btnSupKpi` | Botón clásico | `0` · `0` · `Parent.Width` · `Parent.Height` | receta 7.1 · `Fill`/`HoverFill`/`PressedFill` **`ThemeSurface2`** · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `Radius` (las 4) `11` · `DisplayMode` `DisplayMode.View` |
| `lblNombreKpi` | Etiqueta clásica | `12` · `9` · `110` · `14` | `ESTILO-KPI-LAB` · `Text` `Upper(cmpKpiSesion.MonedaNombre)` |
| `lblCodigoKpi` | Etiqueta clásica | `Parent.Width - 12 - Self.Width` · `9` · `60` · `14` | `ESTILO-KPI-LAB` · Align `Align.Right` · `Text` `Upper(cmpKpiSesion.Moneda)` |
| `lblEfeLabKpi` | Etiqueta clásica | `12` · **`28`** · `80` · `15` | Size `8.63` · Color `ThemeInk2` · `Text` `"Efectivo"` |
| `lblEfeValKpi` | Etiqueta clásica | `Parent.Width - 12 - Self.Width` · **`lblEfeLabKpi.Y`** · `132` · `15` | Size `8.63` · FontWeight `Semibold` · Color `ThemeInk` · Align `Align.Right` · `Text` `fxMonto(cmpKpiSesion.Efectivo, cmpKpiSesion.Simbolo)` |
| `lblChqLabKpi` | Etiqueta clásica | `12` · `lblEfeLabKpi.Y + lblEfeLabKpi.Height` · `80` · `15` | ídem `lblEfeLabKpi` · `Text` `"Cheques"` |
| `lblChqValKpi` | Etiqueta clásica | `lblEfeValKpi.X` · `lblChqLabKpi.Y` · `132` · `15` | ídem `lblEfeValKpi` · `Text` `fxMonto(cmpKpiSesion.Cheques, cmpKpiSesion.Simbolo)` |
| `shpLineaKpi` | Rectángulo clásico | `12` · **`lblChqLabKpi.Y + lblChqLabKpi.Height + 8`** · `Parent.Width - 24` · `1` | `Fill` `ThemeLine` |
| `lblTotLabKpi` | Etiqueta clásica | `12` · `shpLineaKpi.Y + 6` · `70` · **`25`** | `ESTILO-KPI-LAB` · `Text` `"TOTAL"` |
| `lblTotValKpi` | Etiqueta clásica | `82` · `shpLineaKpi.Y + 5` · `Parent.Width - 94` · **`26`** | `ESTILO-KPI-VAL` · `Text` `fxMonto(cmpKpiSesion.Total, cmpKpiSesion.Simbolo)` |

**La aritmética del alto `100`** (cada término es verificable, regla 0.9):

| Control | `Y` | `Height` | Cierra en |
|---|---|---|---|
| `lblNombreKpi` / `lblCodigoKpi` | `9` | `14` | `23` |
| `lblEfeLabKpi` / `lblEfeValKpi` | `28` | `15` | `43` |
| `lblChqLabKpi` / `lblChqValKpi` | `43` | `15` | `58` |
| `shpLineaKpi` | `58 + 8 = 66` | `1` | `67` |
| `lblTotValKpi` | `66 + 5 = 71` | `26` | **`97`** |
| `lblTotLabKpi` | `66 + 6 = 72` | `25` | **`97`** |

⇒ `100 − 97 = 3` de aire abajo. **Si tocás cualquiera de esos términos, rehacé la suma.**

**Fórmula de la propiedad de SALIDA**:

```powerfx
Total = cmpKpiSesion.Efectivo + cmpKpiSesion.Cheques
```

**Cómo `03` alimenta las instancias** (todo sobre colecciones **locales** — regla D-2):

```powerfx
// cmpKpiNIOTul.Efectivo
Sum(
    Filter(
        colDepositosSesionTul As D,
        D.Currency = 'CH-Ch-Currency'.NIO &&
        !IsBlank(
            LookUp(
                Filter(colTulasSesionTul, BagStatus = 'BagStatus (CH-Tbl-Bags)'.Recontada),
                'CH-Tbl-Bags' = D.BagID.'CH-Tbl-Bags'
            )
        )
    ),
    CashAmount
)

// cmpKpiNIOTul.Cheques  → mismo Filter, sumando BACLocalCheckAmount + OtherLocaCheckAmount
```

> 🧨 **`D.BagID.'CH-Tbl-Bags'` sobre un registro YA CARGADO no cuesta un fetch**: es la
> anotación del lookup que Dataverse ya devolvió con la fila. Lo que sí costaría es
> `D.BagID.BagStatus` — por eso el estado se busca en `colTulasSesionTul`, que ya está en
> memoria.

---

### 8.3 `cmpDialogo` — chrome de diálogo nativo (overlay + card + acciones)

Reemplaza el chrome de los diálogos PowerCAT. Mockup `#b3` / `#c1` / `#c3`.

**Tamaño por defecto: `1366 × 768`** (cubre la pantalla entera). La instancia va en
`X 0 · Y 0`, **insertada ÚLTIMA en la pantalla** (tope del z-order).

> 🧨 **Nota C — QUÉ diálogos usa este componente y cuáles NO.** Un componente canvas **no
> puede recibir controles hijos**: lo que hay dentro es lo que trae el componente. Por eso
> `cmpDialogo` cubre **solo los diálogos de texto**, y los de **cuerpo rico** se arman con
> la **receta 7.2** directamente. El plan (`00` §2.5) estimaba ~8 usos; el reparto real es:

| Diálogo | Mockup | Cómo se arma |
|---|---|---|
| Cambiar la moneda del depósito | `C6-1` | ✅ **`cmpDialogo`** (`cmpDlgMonedaCon`) |
| Eliminar el depósito | `C6-2` | ✅ **`cmpDialogo`** (`cmpDlgEliminarCon`, `Peligro = true`) |
| Descartar los cambios del depósito | `C6-3` | ✅ **`cmpDialogo`** (`cmpDlgDescartarCon`) |
| Seleccione la tula recibida | `B1` | 🔨 receta 7.2 (galería de *pick cards*) |
| Seleccione la declaración | `B2` | 🔨 receta 7.2 (galería de *pick cards*) |
| Tula sin declaración | `B3` | 🔨 receta 7.2 + 7.2.a (banner) + 7.2.b (kv) |
| Terminar tula — sin discrepancias | `C4` | 🔨 receta 7.2 + 7.2.a + tabla de resumen |
| Terminar tula — con discrepancias | `C5` | 🔨 receta 7.2 + galería de banners |
| Esperando aprobación | `C7` / `A2` | 🔨 receta 7.2 + Spinner moderno + kv |
| Solicitud rechazada | `C8` / `A3` | 🔨 receta 7.2 + 7.2.a (banner `danger`) |
| Guardando depósito | — | 🔨 receta 7.2 (texto quieto, receta 7.10.C) |

> 🧨 **Nota B — fallback sin propiedades de comportamiento.** Si tu Studio no ofrece
> *Propiedades de componente mejoradas* (§1.4), `OnConfirmar`/`OnCancelar` no se pueden
> declarar. Fallback: **no se crea el componente** y las 3 confirmaciones de `04` se arman
> con la receta 7.2, igual que las otras 8. La app queda idéntica; solo se repite el
> chrome tres veces. Anotalo en el ledger de `00` si tenés que aplicarlo.

#### Propiedades personalizadas

| Nombre | Dirección | Tipo | Default | Para qué |
|---|---|---|---|---|
| `Titulo` | Entrada | Texto | `"¿Confirmás?"` | `<h3>` del diálogo |
| `Subtitulo` | Entrada | Texto | `""` | cuerpo. **Multilínea**: acepta `Char(10)` para separar párrafos |
| `TextoConfirmar` | Entrada | Texto | `"Continuar"` | rótulo del botón primario |
| `TextoCancelar` | Entrada | Texto | `"Cancelar"` | rótulo del botón fantasma. **Extra declarado** (`L-7`): `C6-3` lo necesita en *"Seguir capturando"* |
| `Peligro` | Entrada | Booleano | `false` | pinta el confirmar en `ESTILO-BTN-PELIGRO` |
| `Contorno` | Entrada | Booleano | `false` | pinta el confirmar en `ESTILO-BTN-CONTORNO` (fondo `ThemeSurface`, texto y borde `ThemeAccent`). Es el botón *Descartar* de `C6-3`, que el mockup **no** dibuja lleno |
| `AnchoCard` | Entrada | Número | `400` | `400` normal · `560` ancho · `330` angosto. **Extra declarado** (`L-7`) |
| `Icono` | Entrada | Texto | `""` | glifo del tile de estado: `"check"` \| `"lock"` \| `"trash"` \| `"sync"` \| `"blocked"` \| `"warning"`. **`""` = sin tile** (el diálogo arranca en `X 22`) |
| `Tono` | Entrada | Texto | `"info"` | color del tile: `"ok"` \| `"warn"` \| `"danger"` \| `"info"` — el mapeo de §7.2.a |
| `MostrarCerrar` | Entrada | Booleano | `true` | `false` esconde la ✕. Las tres confirmaciones de `C6` la esconden: el mockup dibuja `Cancelar` y nada más |
| `OnConfirmar` | **Comportamiento** | — | — | lo que ejecuta el botón primario |
| `OnCancelar` | **Comportamiento** | — | — | lo que ejecutan *Cancelar* **y** la ✕ |

> **`Visible` es la propiedad NATIVA de la instancia** (`cmpDlgMonedaCon.Visible =
> ctxDlgMonedaCon`), no una propiedad personalizada. Ver el 🧨 del encabezado de §8.

#### Árbol de controles

```
cmpDialogo                            (1366 × 768, Fill = ThemeOverlay)
├─ btnBloqueoDlg        Botón clásico            ← al FONDO, traga los clics
└─ cntCardDlg           Contenedor de diseño manual
    ├─ btnIcoDlg        Botón clásico            (tile del glifo, receta 7.1) ← al FONDO del bloque
    ├─ icoTipoDlg       Icono clásico            (el glifo, mapeado de Icono)
    ├─ lblTituloDlg     Etiqueta clásica
    ├─ lblSubtituloDlg  Etiqueta clásica
    ├─ icoCerrarDlg     Icono clásico (Icon.Cancel)
    ├─ btnCancelarDlg   Botón clásico
    └─ btnConfirmarDlg  Botón clásico
```

#### Tabla de propiedades

| Control | Tipo | X · Y · W · H | Propiedades y fórmulas |
|---|---|---|---|
| *(raíz del componente)* | Componente canvas | `0` · `0` · `1366` · `768` | **`Fill` `ThemeOverlay`** |
| `btnBloqueoDlg` | Botón clásico | `0` · `0` · `Parent.Width` · `Parent.Height` | `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** — modalidad real: el clic fuera de la card **no cierra** (una acción destructiva no se cancela por accidente) · `TabIndex` `-1` |
| `cntCardDlg` | Contenedor de diseño manual | `(Parent.Width - Self.Width) / 2` · `(Parent.Height - Self.Height) / 2` · `cmpDialogo.AnchoCard` · `btnConfirmarDlg.Y + btnConfirmarDlg.Height + 19` | `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · **`DropShadow` `DropShadow.Regular`** |
| `btnIcoDlg` | Botón clásico (receta 7.1) | `22` · `17` · `34` · `34` | `Text` `""` · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` · `Fill`/`HoverFill`/`PressedFill` `Switch(cmpDialogo.Tono, "ok", ThemeOkSoft, "warn", ThemeWarnSoft, "danger", ThemeDangerSoft, ThemeInfoSoft)` · **`Visible` `!IsBlank(cmpDialogo.Icono)`** |
| `icoTipoDlg` | Icono clásico | `btnIcoDlg.X + 8` · `btnIcoDlg.Y + 8` · `18` · `18` | `Icon` `Switch(cmpDialogo.Icono, "check", Icon.Check, "lock", Icon.Lock, "trash", Icon.Trash, "sync", Icon.Sync, "blocked", Icon.Blocked, "warning", Icon.Warning, Icon.Information)` · `Color` `Switch(cmpDialogo.Tono, "ok", ThemeOk, "warn", ThemeWarn, "danger", ThemeDanger, ThemeInfo)` · **`Visible` `btnIcoDlg.Visible`** |
| `lblTituloDlg` | Etiqueta clásica | **`If(IsBlank(cmpDialogo.Icono), 22, btnIcoDlg.X + btnIcoDlg.Width + 10)`** · `17` · **`Parent.Width - Self.X - 44`** · `22` | `ESTILO-DLG-TITULO` · `Wrap` `true` · `AutoHeight` `true` · `Text` `cmpDialogo.Titulo` |
| `lblSubtituloDlg` | Etiqueta clásica | **`lblTituloDlg.X`** · `lblTituloDlg.Y + lblTituloDlg.Height + 6` · **`Parent.Width - Self.X - 22`** · `54` | `ESTILO-DLG-TEXTO` · **`Wrap` `true`** · **`AutoHeight` `true`** · `Text` `cmpDialogo.Subtitulo` · `Visible` `!IsBlank(cmpDialogo.Subtitulo)` |
| `icoCerrarDlg` | Icono clásico `Icon.Cancel` | `Parent.Width - 36` · `17` · `18` · `18` | `Color` `ThemeInk3` · **`HoverColor` `ThemeInk`** · `TabIndex` `0` · **`Visible` `cmpDialogo.MostrarCerrar`** · **`OnSelect` `Select(btnCancelarDlg)`** — una sola definición del cancelar |
| `btnCancelarDlg` | Botón clásico | `btnConfirmarDlg.X - 8 - Self.Width` · `btnConfirmarDlg.Y` · `14 + 6.6 * Len(Self.Text)` · `32` | `ESTILO-BTN-FANTASMA` · `Text` `cmpDialogo.TextoCancelar` · **`OnSelect` `cmpDialogo.OnCancelar()`** |
| `btnConfirmarDlg` | Botón clásico | `Parent.Width - 22 - Self.Width` · `lblSubtituloDlg.Y + lblSubtituloDlg.Height + 20` · `14 + 6.6 * Len(Self.Text)` · `32` | `Fill` `If(cmpDialogo.Contorno, ThemeSurface, If(cmpDialogo.Peligro, ThemeDanger, ThemeAccent))` · `HoverFill`/`PressedFill` `If(cmpDialogo.Contorno, ThemeAccentSoft, If(cmpDialogo.Peligro, ThemeDanger, ThemeAccentDark))` · `Color` `If(cmpDialogo.Contorno, ThemeAccent, ThemeSurface)` · `BorderColor` `ThemeAccent` · `BorderThickness` `If(cmpDialogo.Contorno, 1, 0)` · `Radius` (las 4) `8` · Size `9` · FontWeight `Semibold` · `Text` `cmpDialogo.TextoConfirmar` · **`OnSelect` `cmpDialogo.OnConfirmar()`** |

**Reglas del componente:**

1. 🧨 **La ✕ y *Cancelar* ejecutan LO MISMO.** `icoCerrarDlg.OnSelect` es
   `Select(btnCancelarDlg)`, no una copia del `OnCancelar()`. Una sola definición.
2. 🧨 **El componente NO cierra el diálogo: eso lo decide la pantalla.** `OnConfirmar` y
   `OnCancelar` los escribe `04`, y **ambos** terminan en
   `UpdateContext({ctxDlg<Nombre>Con: false})`. Si el componente bajara la bandera por su
   cuenta necesitaría escribir una variable de pantalla, y eso rompe la regla de
   aislamiento.
3. **La altura de la card se encadena** desde el subtítulo (`AutoHeight`) hasta el botón:
   un subtítulo de 4 líneas crece la card sola, sin tocar un número (regla 0.9).
4. **CONFIRMAR ANTES DE EJECUTAR** (receta 7.2, regla 2): la acción destructiva vive en el
   `OnConfirmar` de la instancia, nunca en el botón que la hace visible.
5. **`Icono` y `Tono` son dos ejes independientes.** `Icono` decide el glifo (y si el tile
   existe: `""` ⇒ `btnIcoDlg` invisible y el título vuelve a `X 22`); `Tono` decide los dos
   colores, con el mapeo de §7.2.a. Un `Tono` sin `Icono` no pinta nada: el tile no se
   dibuja. Los tres diálogos de `04` §13 los pasan **siempre juntos**.
6. **`Contorno` gana sobre `Peligro`** en `btnConfirmarDlg` (se lee primero en los tres
   `If`). No se pasan los dos `true` en la misma instancia: `Peligro` es *"esto borra"* y
   `Contorno` es *"esto no es la acción principal"*, y una instancia es una cosa o la otra.
7. **`MostrarCerrar false` NO cambia la salida del diálogo**: `btnCancelarDlg` sigue ahí y
   sigue ejecutando `OnCancelar()`. Solo desaparece el atajo de la esquina (regla 1 de la
   receta 7.2: la ✕ y *Cancelar* eran lo mismo; sin ✕ queda una sola puerta).

**Ejemplo de uso** (`04`, diálogo `C6-2`):

```powerfx
// cmpDlgEliminarCon
Visible         = ctxDlgEliminarCon
Titulo          = "Eliminar el depósito " & Text(ctxDepSelCon.AccountNumber)
Subtitulo       = "Se eliminan el desglose de denominaciones, los cheques manuales y el enlace con la declaración. Esta acción no se puede deshacer." & Char(10) & Char(10) &
                  "Efectivo: " & fxMonto(ctxDepSelCon.CashAmount, fxSimbolo(Text(ctxDepSelCon.Currency))) & Char(10) &
                  "Cheques: " & Text(CountRows(Filter(colChecksCon, Seleccionado)))
TextoConfirmar  = "Eliminar"
TextoCancelar   = "Cancelar"
Peligro         = true
Contorno        = false
AnchoCard       = 330
Icono           = "trash"
Tono            = "danger"
MostrarCerrar   = false
OnCancelar      = UpdateContext({ctxDlgEliminarCon: false})
OnConfirmar     = UpdateContext({ctxDlgEliminarCon: false}); Select(btnEliminarDepositoCon)
```

> El `OnConfirmar` **invoca** al botón que tiene la lógica (`Select(btnEliminarDepositoCon)`)
> en vez de duplicarla: la eliminación vive en **un** lugar (receta 7.5) y el diálogo solo
> la dispara.

---

## 9. Checklist de salida del constructor (después de esta guía, antes de `02`)

Verificable, sin interpretación. Todo ítem se prueba en Studio.

**Identidad y configuración**

- [ ] La app se llama `CH-App-Recounting v2`, tiene su ícono con fondo `#C8102E` y está
      agregada a la solución **CashHubApps**.
- [ ] **Pantalla**: Horizontal · 1366 × 768 · Escalar para ajustar **ACTIVADO** ·
      **Límite de filas de datos = 2000**.
- [ ] **Características próximas**: **exactamente dos** activadas — *Funciones definidas
      por el usuario* y *Propiedades de componente mejoradas* (§1.4). Ninguna más. Si
      alguna de las dos no existe en tu Studio, aplicaste el fallback documentado (§3.1
      nota **F** / §8.3 nota **B**) y lo anotaste en el ledger de `00`.
- [ ] `App.BackEnabled = false`, `App.StartScreen = scrSesion`, `ConfirmExit` y
      `ConfirmExitMessage` seteados (§3.4).
- [ ] Existen las **tres pantallas vacías** `scrSesion`, `scrTulas`, `scrConteo`, con
      `Fill = ThemeBg` y en ese orden, y la pantalla en blanco por defecto de Studio fue
      borrada.

**Datos**

- [ ] Los **15 datasources + el flujo** de §2.1 están agregados, y NINGÚN otro: en el panel
      de Datos **no** figuran `CH-Tbl-XMLReadings`, `CH-Tbl-Transactions`,
      `CH-Tbl-Declarations`, `CH-Tbl-Received Emails`, `CH-Tbl-ReceptionBatches`, ningún
      sample ni la librería PowerCAT.
- [ ] **Conectores: solo `CH-Flow-ProcessXMLFiles`.** Cero Office365, cero Graph.
- [ ] **Prueba del espejo E-2** (§2.2): pegá
      `Filter('CH-Tbl-Checks', Bag.'CH-Tbl-Bags' = First('CH-Tbl-Bags').'CH-Tbl-Bags')` en
      el `Items` de una galería de prueba ⇒ **compila**. Si `Bag` no autocompleta: quitá y
      volvé a agregar `CH-Tbl-Checks` desde el panel Datos. Borrá la galería.
      **Este ítem bloquea el cierre de tula de `04`.**
- [ ] **Estado de los espejos E-1, E-3 y E-4 anotado**: hoy **no existen**. Verificá si el
      backend ya los desplegó escribiendo
      `Filter('CH-Tbl-DepositsRecountings', RecountingSessionMirror.'CH-Tbl-RecountingSession' = First('CH-Tbl-RecountingSessions').'CH-Tbl-RecountingSession')`.
      Si sale en rojo, `03` y `04` van con los **fallbacks** de §2.2 y queda anotado en el
      ledger de `00`. **No se inventa la columna.**
- [ ] **Prueba de la igualdad PK contra PK (D-1)**: pegá
      `Filter('CH-Tbl-Bags', RecountingSession = First('CH-Tbl-RecountingSessions'))` ⇒
      **tiene que salir en ROJO**. Cambiala a
      `RecountingSession.'CH-Tbl-RecountingSession' = First('CH-Tbl-RecountingSessions').'CH-Tbl-RecountingSession'`
      y el rojo se va. Borralo después.

**Fórmulas**

- [ ] `App.Formulas` pegado completo: escribí `ThemeAccent` en el `Fill` de una pantalla y
      **el rojo se ve en tiempo de diseño, sin correr OnStart**.
- [ ] **`HtmlEscape("a & b <c> ""d"" 'e'")` compila y devuelve**
      `a &amp; b &lt;c&gt; &quot;d&quot; &#39;e&#39;` (probalo en el `Text` de una etiqueta
      temporal, y borrala).
- [ ] **`fxMonto(-17500, "C$ ")` devuelve `-C$ 17,500.00`** y **`fxMonto(0, "C$ ")` devuelve
      `C$ 0.00`** (no `C$ .00` — es la razón de `FmtMoney = "#,##0.00"`, divergencia `L-2`).
- [ ] **`fxHtmlSeccion("Prueba", "")` devuelve cadena vacía** — es lo que implementa "cada
      sección se emite solo si tiene filas".
- [ ] **El HTML de approval tiene UN solo `</body></html>`**: pegá
      `fxHtmlMesa("Ana", "Mesa 03", "16/08/2026", "07:41")` en el `Text` de una etiqueta
      con `Wrap true` y contá. Borrala después.
- [ ] **Literales de option set verificados en Studio** (§5): `'BagStatus (CH-Tbl-Bags)'`,
      `'CH-Ch-Currency'`, `'ch-recountingDetailsStatus'`,
      `'ApprovalStatus (CH-Tbl-ApprovalRequests)'`, `'CheckTypes (CH-Tbl-Checks)'`,
      `'CheckType (CH-Tbl-DeclaredChecks)'`, `'CheckStatus (CH-Tbl-Checks)'`,
      `'Type (CH-Tbl-MoneyDenominations)'`. Si alguno difiere, **el de tu Studio manda**:
      corregilo y avisá.
- [ ] `App.OnStart` pegado; **botón derecho sobre App → Ejecutar OnStart** deja las **23
      colecciones** de §4.2 creadas y vacías (verificable en Ver → Colecciones) y `gvarUser`
      resuelto con tu usuario. **23 filas en §4.2 = 23 pares `Collect`+`Clear` en §3.2**: si
      los números no coinciden, falta una semilla y una pantalla va a arrancar con una
      colección en rojo.
- [ ] Las **únicas dos** colecciones de la app que NO aparecen en *Ver → Colecciones* tras
      el `OnStart` son `colMesasDVSes` y `colSesionesHoySes`, locales de `02` (§3.2, nota
      final). Cualquier otra que falte es una semilla olvidada.
- [ ] `App.OnStart` **no contiene ningún `ForAll`** y por lo tanto ningún `Set` dentro de
      uno (regla D-12).
- [ ] **`gvarIdentidadOk` es `true` con tu usuario.** Si diera `false`, tu usuario no está
      en `Users` con ese `'Primary Email'` — **avisá antes de seguir**: es la guarda dura de
      §3.2 y `02` no puede construirse sin ella resuelta.
- [ ] `App.OnError` pegado, con el prefijo `[Recounting]`.

**Componentes**

- [ ] Los **tres componentes** de §8 existen con su nombre exacto: `cmpMatrizMoneda`,
      `cmpKpiSesion`, `cmpDialogo`.
- [ ] En los tres, **`AccessAppScope` está ACTIVADO** (`L-67`). Prueba: `ThemeInk` y
      `fxMonto(0, "C$ ")` dentro de un control del componente **compilan**. Con la bandera
      apagada quedan en rojo y el componente no se puede construir.
- [ ] **Ningún componente lee datos de la app**: buscá en el `Src/` desempaquetado
      `Components/cmp*.fx.yaml` ⇒ **cero** `gvar`, **cero** `col…`, **cero** `'CH-Tbl-`.
      Todo dato entra y sale **por propiedades**. La bandera concede el sistema de diseño
      (`Theme*`) y las funciones puras de `App.Formulas`, nada más.
- [ ] `cmpMatrizMoneda` expone las **11 entradas** y las **4 salidas** de §8.1, y con
      `ChequesRec = 4 / ChequesDec = 6` la fila *Diferencia* muestra **`-2` en rojo**; con
      valores iguales, **`0` en verde**.
- [ ] `cmpKpiSesion` expone las **5 entradas** y la salida `Total`, y con
      `Efectivo = 1284500 / Cheques = 96300 / Simbolo = "C$ "` el total dice
      **`C$ 1,380,800.00`**.
- [ ] `cmpDialogo` expone las **10 entradas** `Titulo`, `Subtitulo`, `TextoConfirmar`,
      `TextoCancelar`, `Peligro`, `Contorno`, `AnchoCard`, `Icono`, `Tono`, `MostrarCerrar`
      y `OnConfirmar` / `OnCancelar` (**comportamiento**). Con `Peligro = true` el botón
      primario se pinta `ThemeDanger`; con `Contorno = true`, blanco con borde y texto
      `ThemeAccent`.
- [ ] Con `Icono = "trash"` y `Tono = "danger"` el tile `btnIcoDlg` sale rojo suave con la
      papelera y el título arranca en `X 66`; con `Icono = ""` el tile desaparece y el
      título vuelve a `X 22`. Con `MostrarCerrar = false` **no** se dibuja la ✕.
- [ ] En `cmpDialogo`, **no existe** una propiedad personalizada llamada `Visible`: se usa
      la nativa de la instancia (`L-7`).

**Higiene**

- [ ] **App Checker en CERO errores** (los warnings de accesibilidad se resuelven en
      `02`/`03`/`04` con el `AccessibleLabel` de cada control).
- [ ] **Los dos modernos del catálogo (§0.3) existen en tu Studio**: Insertar → Moderno
      tiene **Lista de pestañas** y **Spinner**. Si alguno falta, avisá antes de arrancar
      `04` — no lo sustituyas por un control de código.
- [ ] Prueba de humo de la paleta: un Botón clásico con `Fill = ThemeAccent`, `Radius 8` y
      `Text "prueba"` se ve rojo BAC con esquinas redondeadas y fuente Open Sans. Borralo
      después.

---
