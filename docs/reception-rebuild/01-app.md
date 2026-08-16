# Guía 01 — CH-App-Reception v2: la app, los datos, las fórmulas globales y las recetas

Se hace **UNA vez**, antes de tocar cualquier pantalla. Las guías `02-pantalla-captura.md`
y `03-pantalla-lotes.md` asumen TODO lo de acá y referencian sus recetas por número
(`receta 7.4`) y sus alias de estilo por nombre (`ESTILO-CELDA-NUM`).

Fuentes de verdad de este documento:

| Qué | Fuente | Regla |
|---|---|---|
| Diseño (layout, paleta, textos, flujos) | `docs/reception-rebuild/mockup-reception.html` (aprobado) | manda salvo que choque con el modelo |
| Modelo (columnas, option sets, PKs) | `Entity.xml` + DEV vivo (verificado 15-ago) | **manda siempre**; toda divergencia se declara |
| Decisiones de rediseño | `docs/reception-rebuild/00-analisis-y-plan.md` | manda sobre la v1 |

> **Sobre display names**: los de este documento están verificados contra el
> `Entity.xml` y contra el entorno DEV. Si el IntelliSense de Studio te ofrece otro
> nombre para la misma columna, **el suyo manda**: usalo y avisá para corregir esta guía.

---

## 0. Contrato de formato de las guías de pantalla (02 y 03)

Estas reglas son la spec contra la que se revisan `02` y `03`. Una violación es un
**bug de la guía**, no una licencia creativa.

1. **Árbol completo**: cada guía abre con el árbol de TODOS los controles de su
   pantalla, con el nombre final de cada uno. Nada se construye "a ojo".
2. **Cobertura total 1:1**: todo control del árbol tiene su descripción abajo —
   tabla propia, fila en tabla compartida, o **puntero explícito a una receta de
   esta guía** (`receta 7.2`) cuando la receta fija TODAS sus propiedades. No hay
   controles en el cuerpo que falten en el árbol ni al revés. Una receta con
   placeholders sin resolver NO cuenta como cobertura.
3. **Tipo explícito**: cada control declara QUÉ componente de Power Apps es, usando
   exactamente estos nombres (los del menú **Insertar**). Catálogo CERRADO: si un
   control no está acá, no entra en la app.

   | Tipo en la guía | En Studio (Insertar) | Dónde se usa |
   |---|---|---|
   | Etiqueta clásica | Clásico → **Etiqueta** (`Label`) | todo texto |
   | Botón clásico | Clásico → **Botón** (`Button`) | acciones, píldoras, superficies redondeadas, overlays de fila |
   | Entrada de texto clásica | Clásico → **Entrada de texto** (`Text input`) | N° de planilla, montos, N° de tula, búsqueda |
   | Cuadro combinado clásico | Clásico → **Cuadro combinado** (`Combo box`) | empresa/sucursal, transportadora (traen búsqueda) |
   | Menú desplegable clásico | Clásico → **Menú desplegable** (`Drop down`) | filtro de estado en scrLotes |
   | Icono clásico | Iconos → el icono nombrado (`Icon`) | glifos (papelera, recargar, lupa, check, ✕) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) | SOLO barras, líneas y separadores (esquina viva) |
   | Imagen clásica | Medios → **Imagen** (`Image`) | tile de marca del header |
   | Temporizador clásico | Clásico → **Temporizador** (`Timer`) | **UN** uso y ninguno más: el defer de un frame del guardado diferido (receta 7.11.C). Nunca como bucle ni como máquina de estados |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** | todas las grillas y listas |
   | Contenedor de diseño manual | Diseño → **Contenedor** (`groupContainer`) | cards redondeadas, overlay y card de los diálogos |
   | Lista de pestañas moderna | Moderno → **Lista de pestañas** (`TabList`) | conmutador Lotes/Planillas de scrLotes (foco y teclado nativos) |
   | Selector de fecha moderno | Moderno → **Selector de fecha** (`DatePicker`) | rango de fechas de scrLotes (el clásico tiene UX pobre de año) |
   | Spinner moderno | Moderno → **Spinner** | indicador de progreso indeterminado de un overlay de operación |

   > **Los TRES modernos permitidos** son exactamente los de las últimas tres filas
   > (Lista de pestañas, Selector de fecha, Spinner) — los habilita `00` §2.2 y son
   > plataforma nativa, no code components. **No hay más excepciones modernas**:
   > cualquier otro control moderno (`TextInput`, `NumberInput`, `Dropdown`,
   > `Toggle`, …) queda FUERA del catálogo. Su aspecto no se calca del mockup: se
   > aceptan con el chrome de plataforma.

   > **Regla de preferencia**: CLÁSICO por defecto. Quedan **prohibidos sin
   > excepción** todos los controles de código y de terceros que mataron a la v1:
   > `FluentDetailsList`, `CommandBar`, `SearchBox`, `KeyboardShortcuts` (Creator
   > Kit), `ExpandMenu`, `Dialog`, `Panel` (PowerCAT), `Table` (OneGrid), `Form` +
   > dataCards. Los contenedores de diseño son nativos de plataforma: entran sin
   > violar la regla.

   > 🧨 **Radius**: en esta plataforma solo tienen `Radius` el **Botón clásico**
   > (default `10`), la **Entrada de texto clásica** (default `5`), la **Imagen** y
   > el **Contenedor** (`groupContainer`, default `4`). El **Rectángulo NO** y la
   > **Etiqueta NO**. Por eso toda superficie con esquinas redondeadas es un Botón
   > clásico (receta 7.1) o un Contenedor, y el rectángulo queda para líneas y
   > barras de esquina viva. Si una tabla de `02`/`03` te pide `Radius` sobre un
   > rectángulo o una etiqueta, es un bug de la guía.

4. **Propiedades**: cada tabla lista SOLO las propiedades que cambian del default.
   Los defaults tipográficos y de caja viven en §6 y **mandan**: si una propiedad
   no figura en la tabla del control, vale lo que dice §6.
5. **Nombres únicos en TODA la app** (regla 0.5): en canvas el nombre de control es
   único a nivel **aplicación**, no de pantalla — si repetís un nombre, Studio lo
   renombra solo y te rompe las fórmulas de la guía. Por eso todo control cuyo
   patrón se repite entre pantallas lleva el sufijo de la suya: **`Cap`**
   (scrCaptura) y **`Lot`** (scrLotes) — `shpHeaderCap`, `segLotesCap`,
   `galLotesLot`, `btnFilaLot`… Los controles que ya son únicos por naturaleza
   igual llevan sufijo (la app tiene solo dos pantallas: es gratis y evita
   colisiones futuras).
6. **Orden del árbol = orden de INSERCIÓN = z-order**: lo insertado DESPUÉS queda
   ENCIMA. Donde el z-order importa (overlay de fila al FONDO, modal al TOPE), la
   guía lo dice explícito en la fila del control.
7. **Coordenadas dentro de una galería**: el `X`/`Y` de un control del template es
   **relativo al template**, que arranca en `0`. Regla derivada, que se enuncia una
   sola vez por guía: **X del control del template = X del encabezado − X de la
   galería**.
8. **Separador de fila**: `shp…Sep` (Rectángulo clásico), `Y` = **`TemplateSize − 1`**,
   `Height` `1`, `Fill` `ThemeLineSoft`. Nunca dos literales que se puedan
   contradecir al cambiar el `TemplateSize`.
9. **Cero números mágicos duplicados**: si dos controles comparten un borde, uno
   referencia al otro (`lblTotalCap.Y = lblTotalLabCap.Y + lblTotalLabCap.Height`),
   nunca dos literales que se puedan desincronizar.
10. **Cada guía abre con un CONTRATO DE ENTRADA** (qué `gvar`/`col` deben venir
    seteadas al entrar y quién las setea) **y cierra con un CHECKLIST DE SALIDA**
    verificable por el constructor. Sin eso, el revisor R2 no tiene contra qué
    verificar.
11. **Construcción en DOS PASES**: pase 1, insertar y renombrar todos los controles
    en el orden exacto del árbol (ese orden ES el z-order), sin tocar propiedades;
    pase 2, recorrer las tablas llenando propiedades. Si vas control por control
    completo, vas a escribir fórmulas contra controles que todavía no existen y
    Studio las rechaza.

---

## 1. Identidad de la app

1. `make.powerapps.com` → entorno **DEV** → **Apps → Nueva app → página en blanco →
   Tamaño: Tableta**.
2. **Configuración → General**:
   - Nombre: `CH-App-Reception v2` (se renombra a `CH-App-Reception` cuando reemplace
     a la v1 en producción).
   - Ícono: subí `docs/marca/icono-reception-512.png` (ya existe; fuente
     `docs/marca/generar-icono-reception.py`).
   - Color de fondo del ícono: `#C8102E`.

   > 🧨 **SON DOS CARGAS DISTINTAS, no una.** El ícono de *Configuración → General*
   > es metadata de la app: **no** se puede referenciar desde una fórmula. El tile
   > de marca del header de `02`/`03` necesita el mismo arte como **recurso de
   > MEDIOS**: Studio → **Medios → Cargar** → `docs/marca/icono-reception-192.png`,
   > renombrado a **`IconoReception192`**. Ese es el nombre que va en
   > `imgMarcaCap.Image` / `imgTileLot.Image` (`Image: IconoReception192`). Si
   > subís solo el ícono de app, el tile del header queda vacío.
   - **Límite de filas de datos: `2000`** (default 500 — la v1 lo dejó en 500 y de
     ahí salían truncamientos silenciosos). Es red de seguridad, **no** sustituto de
     delegar: las consultas de `02`/`03` están diseñadas para no depender del tope.
3. **Configuración → Pantalla y diseño**: Orientación **Horizontal**, Tamaño
   **1366 × 768**, **Escalar para ajustar: ACTIVADO**, Bloquear relación de aspecto:
   **Activado**.

   > **Decisión declarada**: la app se diseña a lienzo fijo 1366 × 768. Son dos
   > pantallas de grilla densa para un puesto de trabajo fijo (esclusa), y el plan
   > no pide responsive. Con *Escalar para ajustar* activado, 1 unidad de canvas =
   > 1 px del mockup y todas las coordenadas de `02`/`03` se copian tal cual. Si
   > alguna vez se pide responsive, hay que **apagarlo** y reescribir los `X/Width`
   > contra `Parent.Width` — no es un cambio cosmético.

4. **Configuración → Características próximas**: **nada experimental**. En
   particular, **nada de UDFs** (funciones de usuario con parámetros): todo lo
   compartido vive en `App.Formulas` como fórmula con nombre, sin parámetros.
5. **Conectores: NINGUNO.** Cero Office365, cero Graph. El acceso lo controlan el
   *sharing* de la canvas app y el rol `CH-SR-Receptionist` de Dataverse; la
   seguridad de operaciones sigue siendo server-side (`GuardReceptionPlugin`). La
   identidad del usuario en pantalla sale de `User()`, que es función nativa.
6. **Tema**: el tema de la solución declara **Open Sans** como fuente de cuerpo. Se
   respeta (§6). Ojo con los glifos: los que no existen en Open Sans salen como caja
   vacía (*tofu*) — por eso **todo glifo va como Icono clásico**, no como texto.
7. Agregá la app a la solución **CashHubApps** (portal de soluciones → Agregar
   existente → Aplicación → Canvas) para que export/deploy siga el pipeline actual.
8. Creá **las dos pantallas vacías de una** (Nueva pantalla → En blanco, renombrar y
   `Fill = ThemeBg`): `scrCaptura` y `scrLotes`. Motivo: los `Navigate()` cruzados
   del segmentado compilan desde el primer día. Borrá la pantalla en blanco que
   Studio crea por defecto.

---

## 2. Orígenes de datos (Dataverse)

### 2.1 Las 6 tablas — y ninguna más

Agregá EXACTAMENTE estas (Datos → Agregar datos). El display name es el que ves en
el panel. **El PK no se deduce del plural: se lee del `Entity.xml`.**

| # | Tabla (display) | PK (columna) | Para qué |
|---|---|---|---|
| 1 | `CH-Tbl-ReceptionBatches` | `CH-Tbl-ReceptionBatch` | Lotes de recepción (maestro de las dos pantallas) |
| 2 | `CH-Tbl-Receptions` | `CH-Tbl-Receptions` | Planillas del lote |
| 3 | `CH-Tbl-Bags` | `CH-Tbl-Bags` | Tulas |
| 4 | `CH-Tbl-ApprovalRequests` | `CH-Tbl-ApprovalRequest` | Solicitudes al supervisor (camino de bóveda) |
| 5 | `CH-Tbl-ClientBranches` | `CH-Tbl-ClientBranch` | Empresa/sucursal remitente del combo de captura |
| 6 | `CH-Tbl-TransportCompanies` | `CH-Tbl-TransportCompany` | Catálogo de transportadoras (solo `Name`) |

> 🚫 **NO se agregan** `CH-Tbl-Clients`, `Users`, ni ninguna tabla de samples. La v1
> las arrastraba sin usarlas y cada datasource pesa en el arranque. El nombre del
> autor de un lote sale de `'Created By'.'Full Name'` por navegación sobre el
> registro ya cargado (nunca dentro de un `Filter`), y el usuario actual de `User()`.

> 🧨 **`CH-Tbl-Clients` NO se agrega, y la excepción condicional de versiones
> anteriores de esta guía QUEDÓ SIN EFECTO** (QA humano 16-ago). Los dos
> disparadores que la justificaban ya no existen:
>
> 1. `RemitentClient` (lookup requerido a `sanic_chtblclient`) se llena con
>    `P.Empresa.Client`, y `P.Empresa` es un **registro fresco de
>    `CH-Tbl-ClientBranches`** traído por la receta 7.10 con un `LookUp` **contra el
>    datasource**, no contra una colección. Ese registro trae su lookup `Client`
>    navegable y tipado: no hace falta el datasource del otro lado.
> 2. La etiqueta *"Empresa · Sucursal"* del combo **ya no se materializa con
>    `Client.Name`**: el combo es server-side y muestra la sucursal + el espejo
>    `sanic_ClientNameMirror` (§2.2, **B0-9**). Cero dot-walk.
>
> Si alguna vez Studio pidiera el datasource para TIPAR, se agrega **solo para
> tipar, jamás para consultar** (cero `Filter`/`LookUp` sobre él) y se anota en el
> ledger de `00`. Con la app 1.31.0.4 desplegada, **no hizo falta**.

### 2.2 🧨 Metadata fresca: los TRES espejos server-side

Estas columnas **existen en DEV** pero **el metadata viejo del repo no las trae**:

| Columna (display exacto) | Tabla | Tipo | Quién la mantiene | Para qué la usa la app |
|---|---|---|---|---|
| `ReceptionBatchId` | `CH-Tbl-Bags` | Lookup → `CH-Tbl-ReceptionBatch` | `BagReceptionMirrorPlugin` (espejo **B-2**) | filtrar **tulas por lote** en una sola consulta delegable |
| `ReceiptIdMirror` | `CH-Tbl-Bags` | Texto | plugin (espejo **B-3**) | buscar/mostrar el N° de planilla de la tula sin navegar el lookup |
| `sanic_ClientNameMirror` | `CH-Tbl-ClientBranches` | Texto (850) | `ClientBranchNameMirrorPlugin` (espejo **B0-9**) | **buscar sucursales por nombre de EMPRESA** en el combo server-side de `02` (§2.5, `02` §5.4) sin navegar `Client.Name` |

Al **agregar la tabla desde el panel Datos de Studio** (no por script), la metadata
llega fresca y aparecen en IntelliSense. **Verificalo antes de escribir la primera
fórmula de `03`**: escribí
`Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = ` y mirá si
autocompleta. Si no aparecen: **quitá la tabla y volvé a agregarla** desde el panel
Datos (mismo remedio que se documentó en el Hub cuando el datasource generado por
script no traía la metadata de relación del lookup). Lo mismo para
`sanic_ClientNameMirror` en `CH-Tbl-ClientBranches`: **si no autocompleta, el combo
de empresa de `02` no busca por empresa** — re-agregá la tabla.

> ⚠️ En `CH-Tbl-ClientBranches` existe además una columna vieja **`sanic_clientname`
> que es HUÉRFANA**: `IsValidForCreate/Update = false`, `IsFilterable = false`, sin
> display name. **No es la del espejo y no se usa.** La que vale es
> `sanic_ClientNameMirror`.

**La app NUNCA escribe estas tres columnas.** Son espejos server-side: solo se leen.

### 2.3 Reglas duras de consulta (delegación) — sin excepción

Estas reglas mataron a la v1 (truncamientos silenciosos a 500 filas: duplicados que
entraban, borrados parciales, lotes "En bóveda" con tulas en esclusa). Se aplican a
TODA fórmula de `02` y `03`:

| # | Prohibido | Qué se hace en su lugar |
|---|---|---|
| D-1 | `a.b = c` navegando a una **columna de datos** del registro relacionado (`ReceptionID.ReceptionBatch`, `ReceptionBatch.Name`, `Empresa.Client.Name`) dentro de `Filter`/`LookUp`/`RemoveIf` **sobre un datasource** | Comparar **PK contra PK**: `Filter('CH-Tbl-Receptions', ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch')` — ver 🧨 abajo |
| D-2 | `CountRows(Filter(ds, …))` | Contar sobre la **colección local** ya cargada, o espejo materializado server-side |
| D-3 | `RemoveIf(ds, predicado)` | `Remove(ds, registro)` con el registro/GUID explícito |
| D-4 | `Filter`/`LookUp`/`Search` en el `Items` de un control **dentro** del template de una galería | El `Items` apunta a una **colección local** cargada una vez en `OnVisible`. **ÚNICA excepción declarada**: un **Cuadro combinado con `IsSearchable true`** sobre una tabla que D-10 prohíbe cachear — consulta bajo demanda al desplegarse, no por fila renderizada (`02` §5.4, `cmbEmpresaCap`) |
| D-5 | `Refresh(ds)` **como parche de estado local después de un `Patch`** (el vicio de la v1) | Actualizar la colección local con lo que devuelve el `Patch` (o recargar con UNA consulta delegable). **PERMITIDO** en **TRES formas acotadas y en ningún lado más** — las tres viven en `scrLotes`, cuyas galerías van atadas al datasource (ver abajo) |
| D-6 | Filtrar planillas por el estado del lote navegando el lookup | `Filter('CH-Tbl-Receptions', BatchStatus = 159460001)` — espejo **entero**, comparado contra el número CRUDO (§5.1) |
| D-7 | Filtrar tulas por lote navegando `ReceptionID.ReceptionBatch` | `Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch')` — espejo B-2, PK contra PK |
| D-8 | `Upper()`/`Lower()`/`Sum()` sobre un datasource | Comparaciones exactas o `StartsWith`; sumas sobre colección local |
| D-9 | Confiar en el orden que trae una **vista** (o el datasource pelado) después de ponerle un `Filter` encima | **`SortByColumns` EXPLÍCITO en el `Items` de toda galería maestra.** Un `Filter` sobre un datasource **no garantiza** que sobreviva el `ORDER BY` de la vista |
| D-10 | Cachear una tabla de Dataverse en una **colección** para usarla de catálogo cuando la tabla **puede crecer** | Catálogo en colección **SOLO si la tabla es estructuralmente acotada (< 500 filas por diseño del negocio)**. Si no: `Items` **server-side** con `IsSearchable` y `SearchFields`. Ver 🧨 abajo |

> Todo `CountRows(Filter(...))` que veas en esta guía o en `02`/`03` está sobre
> **`colXxx` local**, nunca sobre un datasource. Sobre colecciones locales no hay
> delegación que romper: es memoria del cliente.

#### 🧨 D-1 en detalle: la igualdad de registro **NO COMPILA** — se compara PK contra PK

Versiones anteriores de esta guía escribían la igualdad de registro pelada:

```powerfx
Filter('CH-Tbl-Receptions', ReceptionBatch = gvarLoteActivo)      // 🚫 NO COMPILA
```

**Power Fx no acepta comparar una columna de lookup contra un registro.** El QA
humano de la 1.31 lo encontró en cada `Items` y cada `ClearCollect` que la usaba:
Studio la marca en rojo y `pac canvas pack` **no valida fórmulas**, así que viajaba
publicada. La forma **canónica y única** en toda la app es:

```powerfx
Filter(
    'CH-Tbl-Receptions',
    ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'
)
```

**Y sigue siendo DELEGABLE**: la PK de un lookup **no es un dot-walk a una columna
del registro relacionado** — Dataverse la devuelve como anotación del propio
registro (`_sanic_receptionbatch_value`) y el `$filter` se pliega a un `eq` de GUID.
Lo que D-1 prohíbe es navegar a una **columna de datos** del otro lado
(`ReceptionBatch.Name`, `ReceptionID.ReceptionBatch`, `Empresa.Client.Name`): **eso**
arrastra la tabla al cliente.

| Forma | ¿Compila? | ¿Delega? |
|---|---|---|
| `ReceptionBatch = gvarLoteActivo` | 🚫 **NO** | — |
| `ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch'` | ✅ | ✅ `eq` de GUID |
| `ReceptionBatch.Name = gvarLoteActivo.Name` | ✅ | 🚫 dot-walk a columna de datos (D-1) |

**Las dos PK van explícitas, las dos veces.** Escribir
`ReceptionBatch.'CH-Tbl-ReceptionBatch' = gvarLoteActivo` tampoco compila: el lado
derecho también tiene que ser el GUID. Lo mismo vale para el `<>`:
`'CH-Tbl-Receptions' <> galPlanillasLot.Selected.'CH-Tbl-Receptions'`, nunca
`<> galPlanillasLot.Selected`.

**Las PK de esta app** (§2.1): `'CH-Tbl-ReceptionBatch'`, `'CH-Tbl-Receptions'`,
`'CH-Tbl-Bags'`, `'CH-Tbl-ClientBranch'`, `'CH-Tbl-TransportCompany'`,
`'CH-Tbl-ApprovalRequest'`.

#### 🧨 D-10 en detalle: el catálogo local que truncó a 2000 (L-23)

La v2 arrancó cacheando las sucursales activas en `colSucursales` /
`colSucursalesRefCap` y atando el combo de empresa a la colección. **El QA humano lo
rompió a la primera**: el entorno tiene ~50 000 sucursales, el `ClearCollect` trunca
en `DataRowLimit` (2000) **en silencio** y el operador no encuentra su sucursal — el
mismo bug que motivó todo el rediseño (`00` §1.2), con ropa nueva.

**Regla**: un catálogo va a colección **solo si la tabla es estructuralmente acotada
por el negocio (< 500 filas) y no puede crecer**. Hoy en esta app:

| Catálogo | Filas | Dónde vive |
|---|---|---|
| `CH-Tbl-TransportCompanies` | decenas, acotado por contrato | ✅ colección `colTransportadoras` |
| `CH-Tbl-ClientBranches` | **~50 000 y creciendo** | 🚫 colección · ✅ **`Items` server-side** con `IsSearchable` (`02` §5.4) |

Y la contracara: un `Items` server-side **no puede vivir dentro del template de una
galería** por D-4… salvo que sea un **combo con `IsSearchable`**, que es exactamente
la excepción que D-4 declara abajo. Ver la nota de D-4.

#### 🧨 D-5 en detalle: el `Refresh` prohibido y las TRES formas permitidas

La regla D-5 nació contra el `Refresh(ds)` **reflejo** de la v1: patchear y llamar
a `Refresh` "por las dudas" para que la UI se entere. Eso está **PROHIBIDO sin
excepción** — el estado local se reconstruye con lo que devolvió el `Patch` o con
UNA consulta delegable (receta 7.5, paso 3).

**Lo que SÍ está permitido son TRES formas, todas en `scrLotes`** y todas por el
mismo motivo estructural: las galerías de `03` están atadas **al datasource**, no a
colecciones — atarlas a colecciones las clavaría en `DataRowLimit = 2000` y el plan
de estrés (`00` §4.5) siembra **más de 2000 lotes**, o sea el truncamiento
silencioso que motivó todo el rediseño (`00` §1.2). Un `Refresh()` sin filtro **no
es una consulta que se pueda truncar**: fuerza el round-trip y la galería sigue
paginando sola.

| # | Forma | Dónde | Por qué está acotada |
|---|---|---|---|
| 1 | **El botón ⟳** — `Refresh()` de las 3 tablas | `icoRefrescarLot.OnSelect` (`03` §4.1) | Es una **acción del usuario**: pide explícitamente traer lo que cambiaron otros |
| 2 | **`Select(icoRefrescarLot)`** al cerrar *Mover a bóveda* | `03` §8.2, última línea | La operación mueve **el lote y sus tulas**: las maestras TIENEN que recargar o la lista queda mostrando un estado que ya no existe |
| 3 | **`Refresh('CH-Tbl-Bags')` puntual** al cerrar las **4** operaciones de tula | `03` §14.1–§14.4 | **Solo la tabla afectada.** Recargar además las maestras les cambiaría el `Selected` a las galerías y el detalle se movería solo bajo los pies del usuario |

| | `Refresh` automático post-`Patch` | Las tres formas de arriba |
|---|---|---|
| Quién lo dispara | el código, siempre, "por las dudas" | el usuario, o una operación que **de verdad** invalidó lo que se ve |
| Para qué | parchear el estado local | traer cambios de OTROS usuarios / reflejar lo que la operación cambió en el servidor |
| Alcance | el datasource entero, sin criterio | acotado y **declarado**: 3 tablas / 3 tablas / **1** tabla |
| Veredicto | 🚫 **PROHIBIDO** (D-5) | ✅ **PERMITIDO** — estos tres lugares de toda la app, ninguno más |

> La divergencia **DL-10** de `03` planteó exactamente esto y su pushback era
> correcto: quedó **ACEPTADA** y absorbida en esta regla. Ojo con el matiz de la
> forma 3: **una operación de tula NO llama al ⟳**. Llamarlo sería recargar las dos
> maestras por una escritura que solo tocó `CH-Tbl-Bags` — el vicio de la v1 con
> ropa nueva. `02` (`scrCaptura`) **no tiene ninguna de las tres**: cero `Refresh`.

### 2.4 Columnas que la app escribe (y las que NO toca)

**`CH-Tbl-ReceptionBatches`**

| Columna | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `RB-{SEQNUM:5}`, maxlen 20 | 🚫 **NUNCA la patchea** — la genera Dataverse. Se lee para mostrar. |
| `ReceptionStatus` | Picklist (§5.1) | escribe: `En progreso` al crear, `Recibido` al cerrar, `En boveda` al mover |
| `Transportadora` | **Texto** `nvarchar(100)`, requerido | escribe el `Name` de la transportadora elegida en el combo |
| `Location` | Lookup, opcional | no la toca |
| `'Created By'` / `'Created On'` | Sistema | solo lectura (autor y fecha del lote) |

**`CH-Tbl-Receptions`**

| Columna | Tipo | La app |
|---|---|---|
| `ReceiptID` | Texto, requerido | N° de planilla que teclea el usuario |
| `Name` | `nvarchar(850)`, requerido | **se escribe con el mismo valor que `ReceiptID`** (decisión declarada: la columna es requerida y no hay otro dato natural) |
| `ReceptionBatch` | Lookup → lote | `gvarLoteActivo` |
| `RemitentClient` | Lookup → `sanic_chtblclient`, **REQUERIDO** | `P.Empresa.Client` (navegación sobre el registro **local** de sucursal — ver §2.1) |
| `BranchName` | Lookup → `CH-Tbl-ClientBranch` | `P.Empresa` |
| `RemitentName` | Texto | `P.Empresa.Name` |
| `NIOCash` / `NIOCheck` / `USDCash` / `USDCheck` | Decimal | montos de la fila |
| `Comments` | Texto | opcional, si `02` lo expone |
| `BatchStatus` | **Entero** | 🚫 **la app NO lo escribe** — espejo server-side. Solo se **lee** para filtrar (D-6) |

**`CH-Tbl-Bags`**

| Columna | Tipo | La app |
|---|---|---|
| `BagID` | Texto, requerido | N° de tula |
| `Name` | Requerido | **mismo valor que `BagID`** (decisión declarada) |
| `ReceptionID` | Lookup → planilla, **REQUERIDO** | la planilla dueña |
| `BagStatus` | Picklist (§5.2) | escribe según el camino (§5.4) |
| `TotalNIO` / `TotalUSD` / `TotalEUR` | Decimal | 🚫 **NO se tocan** — las escribe recuento |
| `RecountingSession` | Lookup | 🚫 **NO se toca** |
| `ReceptionBatchId` / `ReceiptIdMirror` | Espejos | 🚫 **NO se tocan** — solo lectura (§2.2) |

**`CH-Tbl-ApprovalRequests`**

| Columna | Tipo | La app |
|---|---|---|
| `Name` | **AUTONUMBER** `AR-{SEQNUM:8}` | 🚫 **NUNCA la patchea** |
| `RequestType` | **Texto libre**, requerido | uno de los 5 strings EXACTOS de §5.3 |
| `RequestDetails` | `ntext(10000)` | el **HTML** del template (receta 7.7) |
| `RequestResources` | `nvarchar(2000)` | el **JSON** de claves exactas (§5.5) |
| `ApprovalStatus` | Picklist, solo `Aprobado`/`Rechazado` | 🚫 **la app NUNCA lo setea** — en blanco = Pendiente |
| `StatusReason` / `ResolutionDatetime` / `SolvedBy` | — | 🚫 no se tocan (los escribe el Supervisor / el plugin) |

**`CH-Tbl-ClientBranches`**: `Name` (requerido), `Client` (lookup requerido),
`RecordStatus` (picklist **global** `sanic_chchrecordstatus`, **Active = 159460000**),
`sanic_ClientNameMirror` (espejo **B0-9**, §2.2). Solo lectura: el combo de empresa
las consulta server-side.

> 🧨 **El literal Power Fx de `RecordStatus`** — se filtra **contra el literal, nunca
> contra el entero crudo**: en una picklist, comparar contra `159460000` **NO
> delega**. Y como el option set es **GLOBAL** (`sanic_chchrecordstatus`), su enum
> **NO** se llama `'RecordStatus (CH-Tbl-ClientBranches)'` — ese nombre es el de un
> option set *local* y **no compila**. El literal correcto es el del option set
> global:
>
> ```powerfx
> Filter('CH-Tbl-ClientBranches', RecordStatus = 'CH-Ch-RecordStatus'.Active)
> ```
>
> (Verificado en Studio contra el entorno DEV, QA 16-ago. Si el IntelliSense de tu
> versión ofreciera otro nombre para el **mismo** option set global, el suyo manda:
> usalo y avisá para corregir esta guía. Lo que NO cambia es la forma: literal de
> option set **global**, no entero, no enum de tabla.)
> Contraste con `BatchStatus` (§5.1), que es una columna **entera** y por eso sí se
> compara contra el número crudo.

**`CH-Tbl-TransportCompanies`**: solo `Name`. Solo lectura.

### 2.5 Vistas usadas

Una vista es siempre **el ORIGEN de una carga**, nunca el `Items` de un control que
vive dentro de un template (regla D-4) ni una garantía de orden (regla D-9).

🧨 **Una vista NO puede ser el PRIMER argumento de `Filter`.** Escribir
`Filter('X (Views)'.'la vista', <predicado>)` **no compila**: el primer argumento de
`Filter` tiene que ser la **tabla**, y la vista va como **segundo** argumento
(`Filter(tabla, vista)`), que es una forma distinta y sin predicado. Es el segundo
error sistémico que encontró el QA humano de la 1.31. Las dos formas válidas:

| Qué querés | Cómo se escribe |
|---|---|
| La vista **entera**, sin predicado | `Filter('CH-Tbl-TransportCompanies', 'CH-Tbl-TransportCompanies (Views)'.'CH-Tbl-TransportCompany Lookup View')` |
| La tabla **con predicado propio** | `Filter('CH-Tbl-ReceptionBatches', <predicado>)` — **sin vista**, y con su `SortByColumns` explícito (D-9) |

| Vista | Dónde | Cómo se usa |
|---|---|---|
| `'CH-Tbl-TransportCompanies (Views)'.'CH-Tbl-TransportCompany Lookup View'` | combo de transportadora de `02` | **segundo argumento** de `Filter` sobre `CH-Tbl-TransportCompanies` — es el origen de `colTransportadoras` (`02` §1). El `Items` del combo apunta **siempre a la colección**, nunca a la vista |

> 🚫 **`'CH-Tbl-ReceptionBatches (Views)'.'Reception app view'` ya NO se usa.** El
> `Items` de `galLotesLot` (`03` §6.2) necesita un predicado propio (estado +
> búsqueda + rango de fechas), así que la vista no puede ser su origen sin violar la
> regla de arriba. Va `Filter('CH-Tbl-ReceptionBatches', …)` directo, con
> `SortByColumns("createdon", Descending)` explícito — que es lo que la regla **D-9**
> pedía igual, porque el `ORDER BY` de la vista nunca se heredaba.

> 🧨 **La vista es el origen, la colección es el `Items`.** Esta guía llegó a
> decirlo de las dos formas (hueco **H-1** de `02`): queda una sola, la de la tabla
> de arriba. Motivo: el combo de transportadora vive en un overlay que se abre y
> cierra, y un `Items` atado a la vista vuelve a consultar cada vez que se
> re-renderiza; la colección se carga **una sola vez por sesión** — y `CH-Tbl-
> TransportCompanies` es de las pocas tablas que **D-10 habilita** a cachear.
> **El combo de empresa NO sigue este patrón**: su tabla no es acotada (D-10), así
> que va server-side (`02` §5.4).

---

## 3. Propiedades de la App

### 3.1 `App.Formulas` — pegar TODO esto

Seleccioná **App** en el árbol → propiedad **Formulas**. Todo lo compartido vive
acá: se evalúa perezoso, existe en tiempo de diseño (no hace falta correr `OnStart`
para ver los colores) y hay **un solo lugar** donde viven las 6 reglas de validación.

```powerfx
// ===================== PALETA (RGBA exactos del mockup :root) =====================
ThemeAccent      = RGBA(200, 16, 46, 1);      // #C8102E
ThemeAccentDark  = RGBA(164, 13, 38, 1);      // #A40D26 — hover de botón primario
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
ThemeInfoSoft2   = RGBA(212, 228, 242, 1);    // #D4E4F2 — hover del chip azul (.chip:hover)
ThemeWarn        = RGBA(154, 91, 0, 1);       // #9A5B00
ThemeWarnSoft    = RGBA(251, 240, 220, 1);    // #FBF0DC
ThemeWarnLine    = RGBA(228, 198, 140, 1);    // #E4C68C — borde de input en fila con problema
ThemeDanger      = RGBA(179, 38, 30, 1);      // #B3261E
ThemeDangerSoft  = RGBA(252, 234, 232, 1);    // #FCEAE8
ThemeDangerLine  = RGBA(231, 185, 181, 1);    // #E7B9B5 — borde de botón chico peligroso
ThemeOverlay     = RGBA(40, 30, 32, 0.45);    // rgba(40,30,32,.45) — velo de los modales
ThemeDotWarn     = RGBA(217, 154, 38, 1);     // #D99A26 — punto del panel de validación

// ===================== FORMATOS =====================
FmtMoney      = "#,##0.00";              // el '0' fuerza el entero: "#,###" daba "C$ .00"
FmtFecha      = "dd/mm/yyyy HH:mm";      // HH = 24 h. hh seria 12 h SIN AM/PM
FmtFechaCorta = "dd/mm HH:mm";
SimboloNIO = "C$";  SimboloUSD = "US$";

// ===================== IDENTIDAD (sin conectores) =====================
UsuarioNombre = User().FullName;
UsuarioIniciales =
    Left(User().FullName, 1) &
    If(
        CountRows(Split(User().FullName, " ")) > 1,
        Left(Last(Split(User().FullName, " ")).Value, 1),
        ""
    );
// Si IntelliSense nombra la columna de Split() distinto de 'Value' (algunas versiones
// la llaman 'Result'), EL SUYO MANDA.

// ===================== TOTALES DEL LOTE EN CAPTURA =====================
// Sobre la coleccion LOCAL: no hay delegacion que romper (regla D-2).
TotalChequeNIO   = Sum(colPlanillas, ChequeNIO);
TotalChequeUSD   = Sum(colPlanillas, ChequeUSD);
TotalEfectivoNIO = Sum(colPlanillas, EfectivoNIO);
TotalEfectivoUSD = Sum(colPlanillas, EfectivoUSD);
TotalTulas       = CountRows(colTulas);
TotalPlanillas   = CountRows(colPlanillas);

// ===================== VALIDACION — LAS 6 REGLAS DE LA v1, UNA SOLA VEZ =====================
// Devuelve una tabla { LocalId, Planilla, Regla, Malo } con SOLO las filas malas.
// Se materializa con ClearCollect(colProblemas, fxProblemas) desde Validar / Guardar / Cerrar.
fxProblemas =
    Filter(
        Ungroup(
            ForAll(
                colPlanillas As R,
                {
                    LocalId: R.LocalId,
                    Planilla: R.Planilla,
                    Fallas:
                        Table(
                            { Regla: "Empresa sin seleccionar",
                              Malo: IsBlank(R.Empresa.'CH-Tbl-ClientBranch') },
                            { Regla: "Número de planilla vacío",
                              Malo: IsBlank(Trim(R.Planilla)) },
                            { Regla: "Todos los montos en 0",
                              Malo: (R.ChequeNIO + R.ChequeUSD + R.EfectivoNIO + R.EfectivoUSD) = 0 },
                            { Regla: "Sin tulas asociadas",
                              Malo: CountRows(Filter(colTulas, PlanillaLocalId = R.LocalId)) = 0 },
                            { Regla: "Número de planilla duplicado en el lote",
                              Malo: !IsBlank(Trim(R.Planilla)) &&
                                    CountRows(Filter(colPlanillas, Trim(Planilla) = Trim(R.Planilla))) > 1 },
                            { Regla: "Contiene tulas duplicadas en el lote",
                              Malo: CountRows(
                                        Filter(
                                            colTulas As A,
                                            A.PlanillaLocalId = R.LocalId &&
                                            CountRows(Filter(colTulas As B, Trim(B.Numero) = Trim(A.Numero))) > 1
                                        )
                                    ) > 0 }
                        )
                }
            ),
            "Fallas"
        ),
        Malo
    );

fxHayProblemas = !IsEmpty(fxProblemas);

// Habilitacion de "Cerrar lote": hay lote, hay planillas y no hay problemas.
fxPuedeCerrar = !IsBlank(gvarLoteActivo) && CountRows(colPlanillas) > 0 && !fxHayProblemas;

// ===================== TEMPLATE HTML DE APPROVAL (receta 7.7) =====================
// Literal SIN interpolacion: el $"..." exige duplicar TODA llave de CSS ({{ }}).
// Adentro del CSS se usan comillas SIMPLES; en los atributos HTML, dobles ("" en Power Fx).
HtmlApEstilo =
"<style>
.ap{font-family:'Segoe UI',Arial,sans-serif;color:#201C1D;border:1px solid #E2DCDD;border-radius:12px;overflow:hidden;max-width:620px}
.ap .apHead{padding:13px 18px 11px;border-bottom:3px solid #C8102E}
.ap .tag{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.8px;color:#C8102E;background:#FBEBED;border-radius:999px;padding:3px 10px;text-transform:uppercase}
.ap h3{font-size:15px;margin:7px 0 2px}
.ap .sub{color:#948A8C;font-size:11px}
.ap table{width:100%;border-collapse:collapse;font-size:12px}
.ap th{text-align:left;font-size:10px;letter-spacing:.6px;text-transform:uppercase;color:#948A8C;padding:9px 18px 5px;border-bottom:1px solid #E2DCDD}
.ap td{padding:8px 18px;border-bottom:1px solid #EDE8E9;vertical-align:top}
.ap td.k{color:#66595C;width:38%}
.ap b.new{color:#C8102E}
.ap .foot{padding:10px 18px 12px;font-size:10.5px;color:#948A8C}
</style>";

// DOS encabezados, no uno (receta 7.7): COMPARATIVO cuando la solicitud muestra
// "de X a Y" (Remapear / Editar tula / Editar planilla) y SIMPLE cuando solo
// enumera campos (Agregar / Eliminar). Calcado de previewApproval(conActual).
HtmlApEncabezadoTabla =
"<table><tr><th>Campo</th><th>Actual</th><th>Solicitado</th></tr>";

HtmlApEncabezadoTablaSimple =
"<table><tr><th>Campo</th><th>Valor</th></tr>";
```

> Si Studio rechazara la referencia a **colecciones** desde `App.Formulas` (es una
> limitación que cambió entre versiones de la plataforma), el fallback es: `fxProblemas`
> se muda TAL CUAL al `OnSelect` de `btnValidarCap` como
> `ClearCollect(colProblemas, <la misma expresión>)`, y `Guardar`/`Cerrar` arrancan con
> `Select(btnValidarCap);`. La expresión no cambia ni una coma — sigue habiendo un
> único lugar donde viven las 6 reglas. **Probalo apenas pegues las fórmulas**, antes
> de construir pantallas.

### 3.2 `App.OnStart` — mínimo, sin red

```powerfx
// ===== 1. Semilla de ESQUEMA de las colecciones (Collect + Clear) =====
// Sin esto, las colecciones no existen en tiempo de diseño y toda referencia a sus
// columnas queda en rojo. Defaults(ds) da un registro TIPADO sin ninguna consulta.

Collect(
    colPlanillas,
    {
        LocalId: "",
        GUID: Defaults('CH-Tbl-Receptions').'CH-Tbl-Receptions',   // GUID en blanco, TIPADO
        Empresa: Defaults('CH-Tbl-ClientBranches'),                 // registro TIPADO de sucursal
        Planilla: "",
        ChequeNIO: 0,
        ChequeUSD: 0,
        EfectivoNIO: 0,
        EfectivoUSD: 0
    }
);
Clear(colPlanillas);

Collect(
    colTulas,
    {
        LocalId: "",
        PlanillaLocalId: "",
        Numero: "",
        GUID: Defaults('CH-Tbl-Bags').'CH-Tbl-Bags'
    }
);
Clear(colTulas);

Collect(colPlanillasDV, Defaults('CH-Tbl-Receptions'));
Clear(colPlanillasDV);

Collect(colTulasDV, Defaults('CH-Tbl-Bags'));
Clear(colTulasDV);

Collect(colProblemas, { LocalId: "", Planilla: "", Regla: "", Malo: false });
Clear(colProblemas);

Collect(colFallidas, { LocalId: "", Planilla: "", Mensaje: "" });
Clear(colFallidas);

// ===== 2. Estado global inicial =====
Set(gvarLoteActivo, Blank());          // sin lote => scrCaptura muestra el overlay de nuevo lote
Set(gvarCambiosSinGuardar, false)
```

> 🧨 **PROHIBIDO `Set()` dentro de `ForAll`.** Power Fx no lo permite y `pac canvas
> pack` **no valida fórmulas**: un `Set` adentro de un `ForAll` viaja publicado y
> explota recién cuando alguien abre la app en Studio (bug real de CH-App-Hub 1.27).
> Este `OnStart` no tiene ningún `ForAll` y no se le agrega ninguno. Si necesitás
> acumular dentro de un bucle, se hace con `Collect()` a una colección y **un solo**
> `Set()` al salir.

> ⚠️ **Este `OnStart` no lee ni un solo datasource.** El único catálogo local
> (`colTransportadoras`) y los datos de trabajo se cargan en el `OnVisible` de cada
> pantalla, con una consulta delegable por vista. Es lo que hace que la app abra
> instantánea. **No hay catálogo de sucursales** (L-23 / regla D-10).

> Mientras diseñás, las colecciones no existen hasta que corras el `OnStart`: botón
> derecho sobre **App** en el árbol → **Ejecutar OnStart**, una vez por sesión de Studio.

### 3.3 `App.OnError` — la red final

```powerfx
Trace(
    $"[Reception] {FirstError.Message} | Kind: {FirstError.Kind} | Origen: {FirstError.Source} | Obs: {FirstError.Observed}",
    TraceSeverity.Error
);
Notify(
    If(
        FirstError.Kind = ErrorKind.Validation || FirstError.Kind = ErrorKind.Conflict || FirstError.Kind = ErrorKind.Sync,
        FirstError.Message,   // mensaje de negocio del GuardReceptionPlugin, TAL CUAL
        "Ocurrió un error inesperado. El detalle quedó registrado para soporte."
    ),
    NotificationType.Error
)
```

`OnError` es la **red final**, no el manejo de errores. Regla del proyecto: **todo
`Patch`/`Remove` contra Dataverse va envuelto en `IfError` con decisión explícita**
(receta 7.5). Si `OnError` es el único que se entera de un fallo, es un bug de la
guía — así nacía el *commit parcial silencioso* de la v1.

### 3.4 Otras propiedades de App

| Propiedad | Valor | Por qué |
|---|---|---|
| `BackEnabled` | `false` | La navegación es el segmentado del header (receta 7.8). Con el back del navegador habilitado, el usuario sale de scrCaptura salteándose la guarda de cambios sin guardar. |
| `StartScreen` | `scrCaptura` | Arranca en captura. Si tu versión de Studio no expone `StartScreen`, dejá `scrCaptura` primera en el árbol de pantallas. |
| `ConfirmExit` | `gvarCambiosSinGuardar` | Aviso del navegador al cerrar la pestaña con cambios pendientes. |
| `ConfirmExitMessage` | `"Tenés cambios sin guardar en el lote. Si salís ahora se pierden."` | — |

---

## 4. Variables y colecciones

**Convención**: `gvarXxx` (globales, `Set`, cruzan pantallas), `ctxXxx` (locales,
`UpdateContext`, viven en una pantalla), `colXxx` (colecciones). Los tokens de paleta
y las `fx*` son **fórmulas con nombre**, no variables: no llevan prefijo.

| Nombre | Tipo | Quién la escribe | Quién la lee |
|---|---|---|---|
| `gvarLoteActivo` | Registro de `CH-Tbl-ReceptionBatches` (o `Blank()`) | `OnStart` (Blank); `02` al crear lote; `03` en "Editar lote" | `02` (todo), header (chip de lote), receta 7.9 |
| `gvarCambiosSinGuardar` | Booleano | `02` en cada edición (true) y tras guardar OK (false) | receta 7.8 (guarda de salida), `App.ConfirmExit` |
| `colPlanillas` | Colección de edición: `LocalId` (texto — **§4.1**), `GUID` (GUID Dataverse o blanco), `Empresa` (**registro PURO** de sucursal, no la referencia del lookup), `Planilla` (texto), `ChequeNIO`/`ChequeUSD`/`EfectivoNIO`/`EfectivoUSD` (decimal) | `OnStart` (esquema); `02` al cargar el lote (receta **7.10**) y en cada edición | grilla de captura, `fxProblemas`, totales, receta 7.5 |
| `colTulas` | Colección de edición: `LocalId` (**§4.1**), `PlanillaLocalId`, `Numero` (texto), `GUID` | `OnStart` (esquema); `02` (receta **7.10** y panel de tulas) | chip de tulas, `fxProblemas`, receta 7.5 |
| `colPlanillasDV` | Snapshot CRUDO de las planillas del lote (registros de Dataverse) | `02` al cargar el lote y después de guardar; `03` en "Editar lote" (**§4.3**) | **registro base del `Patch`** (evita un `LookUp` por fila), re-keyado de GUIDs, origen de la receta 7.10 |
| `colTulasDV` | Snapshot crudo de las tulas del lote | ídem | ídem |
| `colProblemas` | `LocalId`, `Planilla`, `Regla`, `Malo` | `ClearCollect(colProblemas, fxProblemas)` en Validar / Guardar / Cerrar | panel de validación, resaltado ámbar de la fila |
| `colFallidas` | `LocalId`, `Planilla`, `Mensaje` — **uso exclusivo del flujo de guardado** (**§4.2**) | receta 7.5 (fase B) | diálogo de resultado + botón "Reintentar fallidas", receta 7.9 |
| `colTransportadoras` | Catálogo de transportadoras (origen: la Lookup View de §2.5, como **2º argumento** de `Filter`) | `scrCaptura.OnVisible` | `Items` del combo de transportadora |
| `colCambiosPlanCap` / `colTulasNuevasCap` | **Delta** calculado por el guardado diferido (receta 7.11, `02` §8) | `btnGuardarCap.OnSelect` | `tmrGuardarCap.OnTimerEnd` — es lo ÚNICO que se patchea |
| `ctxDlg…` | Booleanos de visibilidad de cada modal | el botón que abre / cierra (receta 7.2) | `Visible` del overlay |
| `ctxE…` | Textos ya escapados para HTML | receta 7.6, justo antes de armar el HTML | receta 7.7 |

> 🧨 **`colSucursales` y `colSucursalesRefCap` NO EXISTEN — se eliminaron (L-23).**
> Eran el catálogo local de sucursales, y truncaban en `DataRowLimit` (regla
> **D-10**): el entorno tiene ~50 000 sucursales activas. El combo de empresa de
> `02` §5.4 es **server-side** (`Items` = `Filter('CH-Tbl-ClientBranches', …)` con
> `IsSearchable`), y lo que se guarda en `colPlanillas.Empresa` es el registro que
> devuelve `cmbEmpresaCap.Selected` — **ya puro**, directo del datasource, sin
> `AddColumns` que le cambie la forma. La receta 7.5 sigue haciendo
> `P.Empresa.Client` / `P.Empresa.Name` y asignando `BranchName: P.Empresa`: eso no
> cambió. Si volvés a ver un `AddColumns` sobre sucursales en `02`, es un bug de la
> guía.

**Colecciones y `ctx` de `03` (scrLotes)** — la pantalla es autosuficiente y **no
usa colecciones para sus galerías maestras**: sus cuatro `Items` van **delegables
directo al datasource** (`03` §4.2), por eso acá no hay `colLotes` /
`colPlanillasLot` / `colTulasLot`.

| Nombre | Tipo | Quién la escribe | Quién la lee |
|---|---|---|---|
| `colTulasBovedaLot` | Universo de trabajo de *Mover a bóveda*: tulas del lote en `'Recibida en esclusa'` | `btnMoverBovedaLot.OnSelect` (`03` §8.1) | contador del diálogo y el `Patch` con tabla (§8.2); se `Clear` siempre al terminar |
| `ctxTulaSelLot` | Registro de `CH-Tbl-Bags` (centinela `Defaults('CH-Tbl-Bags')` = "sin selección") | la fila de la mini-tabla de tulas; se resetea en cada cambio de filtro/tab/selección | habilitación de Editar/Remapear/Eliminar (`03` §11.6) |
| `ctxDesdeLot` / `ctxHastaLot` | Fecha (centinelas `1900-01-01` / `2100-01-01`) | `dtpDesdeLot`/`dtpHastaLot`.`OnChange`, `icoLimpiarFechasLot` | el rango del `Filter` delegable de las dos listas maestras |
| `ctxDlgBovedaLot`, `ctxDlgAgregarLot`, `ctxDlgEliminarLot`, `ctxDlgRemapearLot`, `ctxDlgEditarTulaLot`, `ctxDlgEditarPlanillaLot` | Booleanos | el botón que abre / cierra (receta 7.2) | `Visible` de los seis overlays |
| `ctxTulaNuevaLot` | Registro de `CH-Tbl-Bags` devuelto por el `Patch` de alta | `03` §14.1, camino de aprobación | `BagGUID` del JSON (receta 7.7 / §5.5) |
| `ctxBagPrevioLot` / `ctxEBagPrevioLot` | Texto (crudo / escapado) | bloque **P** de `03` §13.2 | `BagPreviousState` del JSON y el HTML |
| `ctxHtmlLot` / `ctxFilasLot` | Texto | bloques **H** y las 5 variantes (`03` §13.3, §14) | `RequestDetails` |
| `ctxEncabezadoLot` | Texto: `"simple"` o `"comparativo"` — qué encabezado de tabla lleva la solicitud (receta 7.7) | cada operación de `03` §14, junto con `ctxETipoLot` | bloque **H** (`03` §13.3) |
| `ctxETipoLot`, `ctxELoteLot`, `ctxETransportadoraLot`, `ctxEEstadoLoteLot`, `ctxEPlanillaLot`, `ctxEEmpresaLot`, `ctxEUsuarioLot`, `ctxETulaLot`, `ctxETulaNuevaLot`, `ctxEPlanillaDestinoLot`, `ctxEEmpresaDestinoLot`, `ctxEPlanillaNuevaLot` | Textos ya escapados | bloque **E** de `03` §13.1 (receta 7.6) | receta 7.7 |
| `ctxResetFechasLot` | Fecha, **solo fallback** | `icoLimpiarFechasLot`, si tu Studio no acepta `Reset()` sobre el Selector de fecha moderno | `DefaultDate` de los dos selectores |

> **No existe `ctxSegLot`**: el conmutador Lotes/Planillas de `03` es una **Lista de
> pestañas moderna** (`tabVistaLot`, §0.3) y toda la conjunción se lee de
> `tabVistaLot.Selected.Value`. Cero variables de estado para una selección que el
> control ya expone.

**Colecciones y `ctx` de `02` (scrCaptura)** — cada una está detallada en la tabla
*Variables y colecciones propias de esta pantalla* de `02`; acá va el índice
completo para que este §4 sea el inventario único de toda la app:

| Nombre | Para qué |
|---|---|
| `colTransportadoras` | catálogo (ya en la tabla de arriba). **No hay catálogo de sucursales**: L-23 / regla D-10 |
| `colTulasEdit` | copia temporal del panel de tulas: Cancelar la descarta, solo Aplicar la vuelca |
| `colTulasBorradasCap` | tulas **con GUID** quitadas del panel ⇒ el **paso de bajas** de la receta 7.5 (regla 5) las borra de Dataverse |
| `colTulasNuevoCap` | buffer de reconstrucción de `colTulas` (no se muta una colección mientras un `Filter` la lee) |
| `colErrBorrarCap` `{ Mensaje }` | errores del borrado de planilla — **separada de `colFallidas`** a propósito (§4.2) |
| `colEstadoGuardadoCap` `{ Planilla, Ok, Mensaje }` | lista de estado por planilla del overlay de guardado |
| `colCambiosPlanCap` | **delta** de planillas: las nuevas + las que difieren del snapshot `colPlanillasDV` campo a campo (receta **7.11**). Es lo único que el guardado patchea |
| `colTulasNuevasCap` | **delta** de tulas: `Filter(colTulas, IsBlank(GUID))`. Las bajas ya eran delta (`colTulasBorradasCap`) |
| `ctxGuardandoCap` | booleano — **arranca `tmrGuardarCap`** y pinta el diálogo *"Guardando N cambios…"*. Es la bandera "en vuelo" que ahora SÍ se pinta, porque el trabajo se difiere un frame (receta **7.11**) |
| `ctxSalirTrasGuardarCap` | booleano — *Guardar y salir*: le dice al `OnTimerEnd` que navegue **después** de guardar (los timers no corren en pantallas no visibles) |
| `ctxScanDupCap` | texto — mensaje de duplicado del escáner de tulas (`02` §7.2); se resetea en el `OnVisible` |
| `ctxDlgTulasCap`, `ctxDlgBorrarFilaCap`, `ctxDlgCerrarCap`, `ctxDlgSalirCap`, `ctxDlgResultadoCap` | `Visible` de los overlays (familia `ctxDlg…` de la receta 7.2) |
| `ctxPlanillaTulasCap`, `ctxPlanillaTulasTxtCap`, `ctxPlanillaTulasEmpCap` | fila cuyo panel de tulas está abierto (`LocalId`, N° y empresa) |
| `ctxFilaBorrarCap`, `ctxFilaBorrarTxtCap`, `ctxFilaBorrarTulasCap`, `ctxFilaBorrarGuidCap` | fila que el diálogo de borrado va a eliminar |
| `ctxGuardarDesdeCierreCap` | booleano — **discriminador de ORIGEN del guardado** (no una bandera "en vuelo", §4.2): lo ponen en `true` *Cerrar lote* (`02` §9.2) y *Guardar y salir* (receta 7.8) antes de `Select(btnGuardarCap)`, y decide si el camino feliz abre el diálogo de resultado (`02` §8.2 paso 8). Se consume al final de ese mismo `OnSelect` |

> **Los diálogos guardan el `LocalId`, nunca el registro ni `gal….Selected`**: entre
> que abrís el diálogo y confirmás, la colección puede mutar y el `LocalId` es la
> única clave que sobrevive (§4.1). Es el fix estructural del bug **N2** de la v1.
>
> `ctxFilaBorrarGuidCap` se inicializa con `Defaults('CH-Tbl-Receptions').'CH-Tbl-Receptions'`
> y no con `Blank()` pelado: un `Blank()` sin tipo deja el `LookUp` posterior sin
> compilar.

### 4.1 🧨 `LocalId`: la regla que hace funcionar el re-keyado

`LocalId` es la clave que une `colPlanillas` ↔ `colTulas` ↔ `colProblemas` ↔
`colFallidas` y la que sobrevive a que la colección mute mientras un diálogo está
abierto (fix estructural del bug **N2** de la v1). Su valor **no es libre**:

| Origen de la fila | `LocalId` | Por qué |
|---|---|---|
| Fila **ya persistida** en Dataverse | **`Text(<GUID real>)`** — `Text(D.'CH-Tbl-Receptions')`, `Text(B.'CH-Tbl-Bags')` | es **estable entre visitas**: salir a Lotes y volver no reasigna claves, y el join local sigue apuntando a la misma fila |
| Fila **nueva** (nace en `02`) | `Text(GUID())` | aleatorio y local; al guardarse, el paso 3 de la receta 7.5 le escribe el `GUID` real en la columna `GUID` (el `LocalId` **no** se reescribe) |

> Sin esta regla, `02` y `03` eligen claves distintas para la misma fila y el
> re-keyado de la receta 7.5 deja de encontrarla ⇒ segundo guardado = registros
> duplicados. Es el hueco **H-4** de `02`.

### 4.2 🧨 `colFallidas` es de UNA operación a la vez

`colFallidas` pertenece **exclusivamente al flujo de guardado** (receta 7.5 fase B →
diálogo de resultado → *Reintentar fallidas* → receta 7.9, que decide si cierra el
lote). Se `Clear`ea al empezar cada guardado y se lee inmediatamente después.

Eso es seguro porque **hay una sola operación en vuelo**: todo el guardado corre
dentro de **un solo `OnSelect`**, que es atómico desde el punto de vista de la UI —
mientras corre, ningún otro control puede dispararse. Con eso alcanza — no hace falta
ninguna cola, ningún discriminador de origen **ni una bandera "en vuelo"** (la
`ctxGuardando` de las versiones anteriores de esta receta **no existe más**: canvas no
repinta a mitad de una fórmula, así que nunca llegaba a pintarse).

Corolario: **ningún otro flujo escribe `colFallidas`.** Cualquier otro bucle que
necesite acumular errores (no se puede `Set()` dentro de un `ForAll`, §3.2) usa su
**propia** colección — así lo hace `02` con `colErrBorrarCap` para el borrado de
planilla. Si un flujo nuevo escribiera `colFallidas`, pisaría el panel de
*Reintentar fallidas* y el usuario reintentaría lo que no falló. Es el hueco **H-8**
de `02`.

### 4.3 Contrato canónico `03 → 02` ("Editar lote")

**UNA sola definición para toda la app.** `03` §9 y el `CONTRATO DE ENTRADA` de `02`
**copian este bloque**; si alguna vez difieren, manda éste.

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

> **El `Notify` es parte del contrato, no adorno de `03`.** Va **antes** del
> `Navigate` y lee el registro **fuente** (`galLotesLot.Selected.Name`), no
> `gvarLoteActivo`, para no depender del orden del `Set` de arriba. Es la única
> señal de que el cambio de pantalla fue **intencional** y de **qué** lote quedó
> cargado: sin él, el operador aterriza en `scrCaptura` sin saber si agarró el lote
> correcto. Las tres copias del bloque lo llevan, idéntico.

**Quién proyecta `colPlanillas`/`colTulas`: `02`, no `03`.** El `OnVisible` de
`scrCaptura` las arma desde los dos snapshots `…DV` con la **receta 7.10**, que es
la ÚNICA definición de esa proyección. Dos motivos por los que `03` no la escribe:

1. La proyección rehidrata `Empresa` con un `LookUp` **sobre el datasource**
   `CH-Tbl-ClientBranches` (receta 7.10), y `02` es la pantalla que la consume: si
   `03` la corriera, pagaría esas consultas antes de navegar, para nada.
2. Si `03` copiara la proyección, habría **dos** definiciones del mismo `ForAll` y
   la primera vez que se toquen se van a desincronizar — que es exactamente lo que
   pasó (hueco **H-3** de `02`, hueco **H-9** de `03`).

> ⚠️ **`Empresa` es un REGISTRO de sucursal, nunca la referencia de `BranchName`.**
> La receta 7.5 hace `P.Empresa.Client` y `P.Empresa.Name`: con la referencia cruda
> del lookup, cada fila costaría un fetch del registro relacionado (N+1). La receta
> 7.10 la resuelve con un `LookUp` contra `CH-Tbl-ClientBranches` por PK — un
> registro **fresco y completo**, con su lookup `Client` navegable. Es lo que
> garantiza que `RemitentClient` **nunca** se guarde en blanco (L-23).

> ⚠️ **`03` NO navega acá con un lote que no sea `En progreso`.** `scrCaptura`
> escribe planillas y tulas sin pedir aprobación: es el camino directo de §5.4. Los
> lotes `Recibido` y `En boveda` se operan desde `03`.

> **Selección en las grillas**: se usa `gal….Selected` directamente (reactivo, cero
> variables). ⚠️ Las galerías clásicas **auto-seleccionan la primera fila** al cargar
> `Items` — el panel de detalle se diseña asumiéndolo, y los estados vacíos se
> reservan para **colección vacía**, nunca para "sin selección". La única excepción
> es la mini-tabla de tulas de `03` (§11.6), que usa el centinela `ctxTulaSelLot`
> porque de esa selección cuelgan operaciones **destructivas**.

---

## 5. Estados y contratos inter-app

### 5.1 `ReceptionStatus` — option set `'ReceptionStatus (CH-Tbl-ReceptionBatches)'`

| Literal Power Fx | Valor | Etiqueta en pantalla | Significado |
|---|---|---|---|
| `'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso'` | **159460000** | `"En progreso"` | lote editable en captura |
| `'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido` | **159460001** | `"Recibido"` | cerrado; tulas en esclusa |
| `'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado` | **159460002** | `"Anulado"` | muerto — fuera de alcance, pero se mapea el color por completitud |
| `'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda'` | **159460003** | `"En bóveda"` | toda modificación requiere aprobación |

> El literal del option set va **sin tilde** (`'En boveda'`) porque así está la
> etiqueta en Dataverse; el texto que se muestra en pantalla va **con tilde**
> (`"En bóveda"`). No es un descuido: es una traducción explícita en el `Switch` de
> la píldora (receta 7.4).

**Espejo entero en planillas** (`CH-Tbl-Receptions.BatchStatus`): se compara contra
los números **CRUDOS**, nunca contra el option set, porque es una columna `int`:

```powerfx
Filter('CH-Tbl-Receptions', BatchStatus = 159460003)   // planillas de lotes en boveda
```

### 5.2 `BagStatus` — option set `'BagStatus (CH-Tbl-Bags)'`

| Literal Power Fx | Valor | Etiqueta en pantalla | Quién lo produce |
|---|---|---|---|
| `'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'` | **159460000** | `"Recibida en esclusa"` | Reception (captura y alta directa) |
| `'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda'` | **159460001** | `"Almacenada en bóveda"` | Reception (mover a bóveda) |
| `'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa'` | **159460005** | `"Pendiente supervisor"` | Reception (todo camino de aprobación) |

Otros valores del option set (p. ej. `Asignada a cajero`) los produce Recuento:
Reception **solo los muestra**, con la píldora neutra.

### 5.3 `RequestType` — TEXTO LIBRE, strings EXACTOS

`RequestType` es `nvarchar` requerido, **no** un option set: el guard del Supervisor
compara el string tal cual. Copiar y pegar, sin tildes de más ni de menos:

| String exacto | Cuándo |
|---|---|
| `"Agregar tula"` | alta de tula en lote **En bóveda** |
| `"Eliminar tula"` | baja de tula en lote **En bóveda** |
| `"Remapear tula"` | mover tula a otra planilla del mismo lote, lote **En bóveda** |
| `"Editar tula"` | cambiar el N° de tula, lote **En bóveda** |
| `"Modificar numero de planilla"` | cambiar el N° de planilla — **SIEMPRE requiere aprobación**, sin importar el estado del lote |

### 5.4 Camino directo vs. camino de aprobación

| Estado del lote | Operación de tula | Qué hace la app |
|---|---|---|
| `En progreso` | todas | edición local en `colTulas`; se persiste con el guardado (receta 7.5); `BagStatus` = `'Recibida en esclusa'` (159460000) |
| `Recibido` | Agregar / Editar / Remapear / Eliminar | **acción directa**: `Patch`/`Remove` contra Dataverse dentro de `IfError`. Alta ⇒ `BagStatus` = `'Recibida en esclusa'` (159460000) |
| `En boveda` | Agregar / Editar / Remapear / Eliminar | **solicitud**: la tula queda en `'Pendiente de revision por supervisor - esclusa'` (159460005) + se crea el `ApprovalRequest`. En "Agregar" la tula **se crea** en ese estado (por eso el JSON lleva `BagGUID`) |
| cualquiera | Modificar número de planilla | **siempre solicitud** |

> **Sin rama muerta de `'En boveda'` en alta/edición de tula** (mata el bug N8 de la
> v1): el `BagStatus` destino se calcula **una sola vez y explícito por camino** —
> nunca "el estado que tenía el lote".

### 5.5 `RequestResources` — claves EXACTAS (contrato con CH-App-Supervisor)

Se arma **siempre con `JSON({...})`**, jamás por concatenación (la v1 concatenaba con
`""""` y el guard parsea ese JSON para decidir ownership). Los GUIDs viajan como
texto: `Text(registro.'PK')`.

| `RequestType` | Claves (exactas, en este orden) |
|---|---|
| `"Agregar tula"` | `ReceiptGUID`, `BagID`, `BagGUID`, `ReceptionBatchGUID` |
| `"Eliminar tula"` | `GUID`, `BagPreviousState` |
| `"Remapear tula"` | `GUID`, `ReceiptID`, `ReceiptGUID`, `BagPreviousState` |
| `"Modificar numero de planilla"` | `ReceiptGUID`, `NewReceiptID` |
| `"Editar tula"` | `ReceiptGUID`, `BagID`, `ReceptionBatchGUID`, `BagGUID` |

`BagPreviousState` es el **texto del estado actual de la tula** antes de moverla a
Pendiente. Se calcula así (una sola vez, antes de patchear):

```powerfx
UpdateContext({
    ctxBagPrevioLot:
        Switch(
            ctxTulaSelLot.BagStatus,
            'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                          "Recibida en esclusa",
            'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                         "Almacenada en boveda",
            'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',"Pendiente de revision por supervisor - esclusa",
            Text(ctxTulaSelLot.BagStatus)   // etiqueta cruda del option set (coerción a texto)
        )
})
```

> El texto va **sin tildes**, calcado de la etiqueta del option set: el Supervisor lo
> compara contra los mismos strings para restaurar el estado si rechaza.
> ⚠️ La unificación `GUID`/`BagGUID` (hallazgo N10) **NO** se toca en este rediseño:
> es una fase coordinada con Supervisor.

`ApprovalStatus` **nunca** se setea desde esta app: en blanco = Pendiente. `Name` es
autonumber `AR-{SEQNUM:8}`: nunca se patchea.

**Límites duros**: `RequestDetails` es `ntext(10000)` — el template HTML pesa ~1.3 k,
así que los datos interpolados tienen que caber en los ~8 k restantes.
`RequestResources` es `nvarchar(2000)`: el JSON de 4 GUIDs entra holgado, pero si
alguna vez se agrega una clave, verificá el largo.

---

## 6. Estilos: defaults y alias `ESTILO-*`

### 6.1 Defaults (MANDAN sobre las tablas de `02`/`03`)

Toda **Etiqueta clásica** y todo **Botón clásico** arrancan así, salvo que su tabla
diga otra cosa:

| Propiedad | Valor | Por qué se pisa el default |
|---|---|---|
| `Font` | `Font.'Open Sans'` | es la fuente del tema de la solución; se escribe explícita para que no dependa de que el tema viaje |
| `Size` | `9` | equivale a los 12 px del mockup |
| `Color` | `ThemeInk` | — |
| `PaddingLeft` / `Right` / `Top` / `Bottom` | `0` | la etiqueta clásica trae **5** en las cuatro: sin esto todo texto queda corrido 5 px |
| `Wrap` | `false` | el default de plataforma es `true`: una etiqueta de una línea con alto justo **envuelve y recorta** |
| `VerticalAlign` | `VerticalAlign.Middle` | — |
| `BorderThickness` (botones) | `0` | el botón clásico trae `2` |
| `HoverFill` / `PressedFill` (botones de solo lectura) | **iguales al `Fill`** | si no, la superficie/píldora parpadea al pasar el mouse |

**Entrada de texto clásica**: `BorderColor` `ThemeLine`, `BorderThickness` `1`,
`Radius` (las 4) `7`, `Size` `9`, `Color` `ThemeInk`, `Fill` `ThemeSurface`.
*(El color del `HintText` no es configurable en el control clásico — divergencia
aceptada vs. `.cin::placeholder` del mockup.)*

**`Height` de la Entrada de texto clásica — DOS valores, no "el que salga"**:

| Contexto | `Height` | Por qué |
|---|---|---|
| Dentro del template de una galería (grilla de captura) | **`28`** | la fila de la grilla mide 40: 28 deja 6 px de aire arriba y abajo |
| **Fuera de una galería**: barra de herramientas, cuerpo de un diálogo | **`32`** | comparte fila con Botones (32), Cuadros combinados (32) y Menús desplegables (32). Con 28 el input queda 4 px más bajo y la fila se ve chueca |

> Regla derivada, para no discutirla control por control: **si el input comparte
> fila con un control de 32, va en 32**. Es lo que resuelve el hueco **H-11** de
> `03`: sus inputs de barra y de diálogo son todos `32`.

**Cuadro combinado y Menú desplegable clásicos**: `BorderColor` `ThemeLine`,
`BorderThickness` `1`, `Size` `9`, `Color` `ThemeInk`, `Fill` `ThemeSurface`.
⚠️ **No tienen `Radius`** → quedan de esquina viva. Divergencia declarada vs. el
`border-radius:7px` del mockup; el borde y el tamaño sí se respetan.

**Glifos**: prohibido meter glifos en el `Text` de etiquetas o botones (tofu en Open
Sans). Todo glifo es un **Icono clásico**. Catálogo CERRADO de iconos de esta app:

| Icono | Dónde |
|---|---|
| `Icon.Trash` | eliminar (fila, tula) |
| `Icon.Add` | nueva fila / agregar tula |
| `Icon.Reload` | ⟳ actualizar (`03` §4.1) |
| `Icon.Search` | lupa del buscador |
| `Icon.Check` | validar |
| `Icon.Cancel` | la ✕ de los diálogos y el limpiar-fechas |
| `Icon.Lock` / `Icon.LockOpen` | banner de bóveda y avisos de "esto va como solicitud" |
| `Icon.Information` | banner **informativo** (ⓘ) del diálogo *Editar planilla* de `03` |

> `Icon.Information` **existe** en el set clásico de canvas — es el que va en el
> banner azul. Ojo: **`Icon.Info` NO existe**; si escribís ese, Studio te lo marca
> en rojo. Vale la regla general de la guía: si el IntelliSense de tu versión
> nombra distinto alguno de estos, **el suyo manda**.

**Conversión de tamaños**: con *Escalar para ajustar* activado, 1 unidad = 1 px del
mockup, así que `X/Y/Width/Height` se copian tal cual. `Size` está en **puntos**:

> **`Size = Round(px_del_mockup × 0.75, 2)`**

| px | `Size` | Dónde en el mockup |
|---|---|---|
| 16.5 | `12.38` | `.totCard .val` (totales del rail) |
| 15 | `11.25` | `.apHead h3` |
| 14.5 | `10.88` | `.dlgH h3` (título de diálogo) |
| 14 | `10.5` | `.hTitle`, glifo del `.brandTile` |
| 13.5 | `10.13` | `.detH .t b` (título del panel de detalle) |
| 12.5 | `9.38` | `.lRow .main b`, `.emptyMsg` |
| 12 | `9` | cuerpo general: `.tRow`, `.btn`, `.in`, `.meta`, `.navSeg button` |
| 11.5 | `8.63` | `.userChip`, `.valItem`, `.banner` |
| 11 | `8.25` | `.btnSm`, `.tulaChip`, `.errTxt`, `.field label`, `.idTag`, `.hTitle small` |
| 10.5 | `7.88` | `.pill`, `.tHead`, `.listHead`, `.totCard .lab`, `.apFoot` |

### 6.2 Alias de estilo — etiquetas

| Alias | Se logra con |
|---|---|
| `ESTILO-TITULO-APP` | Size `10.5`, FontWeight `Semibold`, Color `ThemeInk` |
| `ESTILO-SUBTITULO-APP` | Size `8.25`, Color `ThemeInk3` |
| `ESTILO-SECCION` | Size `8.25` (11 px), FontWeight `Semibold`, Color `ThemeInk2` — texto **SIEMPRE en MAYÚSCULAS**. **No** es el mismo tamaño que `ESTILO-HEADER-COL`: un título de sección (*VALIDACIÓN*) rotula un panel entero; un encabezado de columna rotula una celda y va un escalón más chico |
| `ESTILO-HEADER-COL` | Size `7.88`, FontWeight `Semibold`, Color `ThemeInk2` — MAYÚSCULAS |
| `ESTILO-CELDA` | Size `9`, Color `ThemeInk` |
| `ESTILO-CELDA-NUM` | Size `9`, Color `ThemeInk`, Align `Align.Right` |
| `ESTILO-CELDA-FUERTE` | Size `9.38`, FontWeight `Semibold`, Color `ThemeInk` |
| `ESTILO-META` | Size `8.25`, Color `ThemeInk3` |
| `ESTILO-DETALLE-TITULO` | Size `10.13`, FontWeight `Semibold`, Color `ThemeInk` |
| `ESTILO-KPI-LAB` | Size `7.88`, FontWeight `Semibold`, Color `ThemeInk3` — MAYÚSCULAS |
| `ESTILO-KPI-VAL` | Size `12.38`, FontWeight `Bold`, Color `ThemeInk` |
| `ESTILO-DLG-TITULO` | Size `10.88`, FontWeight **`Bold`**, Color `ThemeInk` — el título del diálogo es un `<h3>` en el mockup y el navegador lo pinta **bold** por default; `Semibold` lo dejaba más liviano que el original |
| `ESTILO-DLG-TEXTO` | Size `9`, Color `ThemeInk2`, Wrap `true` + `Height` explícito |
| `ESTILO-CAMPO-LAB` | Size `8.25`, FontWeight `Semibold`, Color `ThemeInk2` |
| `ESTILO-ERROR` | Size `8.25`, Color `ThemeDanger`, Wrap `true` |
| `ESTILO-VALITEM` | Size `8.63`, Color `ThemeInk2`, Wrap `true` + `Height` explícito |
| `ESTILO-VACIO` | Size `9.38`, Color `ThemeInk3`, Align `Align.Center` |
| `ESTILO-ID` | Size `8.25`, Color `ThemeInk3` (el `#` de fila, `.idTag`) |

> Toda etiqueta con `Wrap true` lleva `Height` explícito y verificado contra el
> `TemplateSize` de su galería: si no, el texto se corta en silencio.

### 6.3 Alias de estilo — botones

Todos con `Size` `9`, `FontWeight` `Semibold`, `Radius` (las 4) `8`, `Height` `32`,
salvo que se indique.

| Alias | Fill | Color | Borde | Hover / Pressed |
|---|---|---|---|---|
| `ESTILO-BTN-PRIMARIO` | `ThemeAccent` | `ThemeSurface` | `0` | `ThemeAccentDark` / `ThemeAccentDark` |
| `ESTILO-BTN-CONTORNO` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent`, `1` | `ThemeAccentSoft` / `ThemeAccentSoft` |
| `ESTILO-BTN-FANTASMA` | `Color.Transparent` | `ThemeInk2` | `ThemeLine`, `1` | `ThemeSurface2` / `ThemeSurface2` |
| `ESTILO-BTN-PELIGRO` | `ThemeDanger` | `ThemeSurface` | `0` | `ThemeDanger` / `ThemeDanger` *(sin tono oscuro en el mockup — declarado)* |
| `ESTILO-BTN-CHICO` | `ThemeSurface` | `ThemeInk2` | `ThemeLine`, `1` | `ThemeSurface2` / `ThemeSurface2` — Size `8.25`, Height `26`, Radius `7` |
| `ESTILO-BTN-CHICO-ACC` | `ThemeSurface` | `ThemeAccent` | `ThemeAccent`, `1` | `ThemeAccentSoft` — Size `8.25`, Height `26`, Radius `7` |
| `ESTILO-BTN-CHICO-DAN` | `ThemeSurface` | `ThemeDanger` | `ThemeDangerLine`, `1` | `ThemeDangerSoft` — Size `8.25`, Height `26`, Radius `7` |

**Deshabilitado (todos)**: `DisabledFill` `ThemeLineSoft`, `DisabledColor`
`ThemeInk3`, `DisabledBorderColor` `Color.Transparent`. La habilitación va siempre en
`DisplayMode` con la condición explícita, y **la condición se escribe contra el
control que la acción realmente usa** (mata el bug N7 de la v1) **y contra el estado
del lote** (mata N9).

---

## 7. Recetas reutilizables

Cada receta fija TODAS las propiedades de los controles que nombra. Cuando `02`/`03`
apuntan a una receta, no repiten sus propiedades: solo dan nombre final, coordenadas
y las fórmulas específicas.

### 7.1 Superficie redondeada = Botón clásico

Ni el Rectángulo ni la Etiqueta tienen `Radius` (§0.3). Toda card, chip, píldora o
tile con esquinas redondeadas es un **Botón clásico** con `Text: ""`:

| Propiedad | Valor |
|---|---|
| `Text` | `""` |
| `DisplayMode` | `DisplayMode.View` (decorativa) |
| `Fill` | el que pida la tabla (`ThemeSurface`, `ThemeSurface2`, …) |
| `HoverFill` / `PressedFill` | **iguales al `Fill`** (si no, parpadea) |
| `BorderColor` / `BorderThickness` | `ThemeLine` / `1` (o `0` si la card no lleva línea) |
| `Radius` (las 4) | `13` cards · `11` cards chicas del rail · `9` tiles · `8` botones · `16` píldoras de alto 32 · `12` píldoras de estado |
| `Color` | irrelevante (no hay texto), dejalo en `ThemeInk` |

**Reglas de uso**:

- La superficie va **al FONDO del z-order** de su bloque; el contenido (etiquetas,
  entradas, iconos) se inserta **después**, encima. Si un control queda tapado o no
  recibe clic, tenés el z-order invertido.
- `DisplayMode.View` **no dispara `OnSelect`**. Si la superficie vive dentro del
  template de una galería y tiene que delegar el clic, va en `DisplayMode.Edit` con
  `OnSelect: Select(Parent)` (receta 7.3).
- Variante `DisplayMode.Disabled` + `DisabledFill`: solo cuando la superficie es el
  fondo de un bloque **deshabilitado** y querés el gris de estado sin cambiar `Fill`.
- ❌ **La sombra del mockup no existe** en el botón clásico (`box-shadow` de `.card`,
  `.dlg`, `.navSeg button.on`). Pérdida aceptada y declarada: la separación la da el
  borde `ThemeLine`. La única sombra disponible es `DropShadow` del **Contenedor**,
  que se usa en la card del modal (7.2).

### 7.2 Diálogo modal de confirmación

Estructura fija de **6 controles + contenido**, todos hijos del overlay. El overlay
se inserta **ÚLTIMO en la pantalla** (tope del z-order): si queda debajo de algo, el
modal no tapa.

| Control | Tipo | Propiedades |
|---|---|---|
| `cntDlg<Nombre><Scr>` | Contenedor de diseño manual | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Fill` `ThemeOverlay` · `Radius` (las 4) `0` · `BorderThickness` `0` · **`Visible` `ctxDlg<Nombre>`** |
| `btnDlgBloqueo<Nombre><Scr>` | Botón clásico (hijo, **al FONDO** del contenedor) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · `Text` `""` · `Fill`/`HoverFill`/`PressedFill` `Color.Transparent` · `BorderThickness` `0` · **`OnSelect` `false`** — traga los clics del fondo (modalidad real) |
| `cntDlgCard<Nombre><Scr>` | Contenedor de diseño manual (hijo) | X `(Parent.Width - Self.Width) / 2` · Y `(Parent.Height - Self.Height) / 2` · Width `380` (`520` si es ancho) · Height la que pida el contenido · `Fill` `ThemeSurface` · `Radius` (las 4) `13` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DropShadow` `DropShadow.Regular` |
| `lblDlgTit<Nombre><Scr>` | Etiqueta clásica (hija de la card) | X `20` · Y `16` · Width `Parent.Width - 66` · Height `22` · `ESTILO-DLG-TITULO` · Wrap `true` |
| `lblDlgTxt<Nombre><Scr>` | Etiqueta clásica (hija) | X `20` · Y `lblDlgTit….Y + lblDlgTit….Height + 4` · Width `Parent.Width - 40` · `ESTILO-DLG-TEXTO` · Height explícito |
| `icoDlgX<Nombre><Scr>` | Icono clásico `Icon.Cancel` (hijo) | X `Parent.Width - 34` · Y `16` · Width `18` · Height `18` · `Color` `ThemeInk3` · **`HoverColor` `ThemeInk`** — el **Icono clásico SÍ expone `HoverColor`** (es el mismo que usa la papelera de fila de `02` §5.4 con `ThemeDanger`); no dejarlo en el mismo tono que `Color`, porque entonces la ✕ no contesta al mouse · **`OnSelect` = EXACTAMENTE el mismo del botón Cancelar** |
| `btnDlgCancel<Nombre><Scr>` | Botón clásico | `ESTILO-BTN-FANTASMA` · Text `"Cancelar"` · Y `Parent.Height - 50` · `OnSelect` `UpdateContext({ctxDlg<Nombre>: false})` |
| `btnDlgOk<Nombre><Scr>` | Botón clásico | `ESTILO-BTN-PRIMARIO` (o `ESTILO-BTN-PELIGRO` si es destructivo) · Text según el caso · `OnSelect` la acción + `UpdateContext({ctxDlg<Nombre>: false})` |

**Reglas no negociables** (fixes heredados de la v1):

1. **La ✕ SIEMPRE cancela.** `icoDlgX` ejecuta literalmente el mismo `OnSelect` que
   `btnDlgCancel`: cerrar y **no commitear nada** (mata el bug N4). Prohibido que la
   ✕ aplique cambios "porque ya estaban tipeados".
2. **Reset al ABRIR, no al cerrar.** El botón que abre el diálogo hace:
   `UpdateContext({ctxDlg<Nombre>: true}); Reset(inp<Campo>); Reset(cmb<Campo>)` —
   así el diálogo nunca arrastra el valor de la vez anterior (mata N6, el remapeo
   silencioso). El `Default` de cada entrada apunta al valor actual del registro
   seleccionado.
3. **Todo borrado es destructivo ⇒ tiene diálogo.** La v1 no tenía ninguno.
4. El botón de confirmar arranca deshabilitado cuando la entrada es inválida:
   `DisplayMode: If(<condición de validez>, DisplayMode.Edit, DisplayMode.Disabled)`.

**Variante "cambios sin guardar"** (3 botones): `Cancelar` (fantasma) ·
`Descartar` (contorno) · `Guardar y salir` (primario). Ver receta 7.8.

#### 7.2.a Sub-bloque OPCIONAL — banner (info / warn)

Franja de color al pie del cuerpo del diálogo, para avisar que la acción **va a
generar una solicitud de aprobación** (warn) o para una nota permanente (info).
Son **3 controles**, insertados en este orden (la superficie primero, regla 7.1):

| Control | Tipo | Propiedades |
|---|---|---|
| `btnAviso<Nombre><Scr>` | Botón clásico (receta 7.1) | X `20` · Width `Parent.Width - 40` (`340` en card de 380) · Height **`40`** (1 línea de texto) o **`58`** (2 líneas) · `Fill`/`HoverFill`/`PressedFill` = `ThemeWarnSoft` (warn) \| `ThemeInfoSoft` (info) · `Radius` (las 4) `9` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.View` |
| `icoAviso<Nombre><Scr>` | Icono clásico: `Icon.Lock` (warn) \| `Icon.Information` (info) | X `btnAviso….X + 12` · Y `btnAviso….Y + 12` · Width `16` · Height `16` · `Color` `ThemeWarn` \| `ThemeInfo` · `Visible` `btnAviso….Visible` |
| `lblAviso<Nombre><Scr>` | Etiqueta clásica | X `btnAviso….X + 36` · Y `btnAviso….Y` · Width `btnAviso….Width - 48` · Height `btnAviso….Height` · Size `8.63` · `Color` `ThemeWarn` \| `ThemeInfo` · **Wrap `true`** · `Visible` `btnAviso….Visible` |

**Reglas**: el `Visible` se escribe **una sola vez** (en la superficie) y los otros
dos lo referencian — nunca tres copias de la misma condición. Si el banner es
condicional, todo lo que va **debajo** encadena su `Y` contra
`btnAviso….Y + btnAviso….Height`, jamás contra un literal.

#### 7.2.b Sub-bloque OPCIONAL — resumen `kv` (pares etiqueta/valor)

El "¿confirmás esto?" de un diálogo destructivo: en vez de meter los datos en el
párrafo, van en una grilla de pares. **Dos etiquetas por par**, sin galería (la
cantidad de pares es fija y conocida):

| Control | Tipo | Propiedades |
|---|---|---|
| `lblKv<Campo>Etq<Nombre><Scr>` | Etiqueta clásica | X `20` · Width `80` · Height `18` · **`ESTILO-META`** · `Text` la etiqueta (*"Tula"*, *"Planilla"*, *"Lote"*) |
| `lblKv<Campo><Nombre><Scr>` | Etiqueta clásica | X `108` · Width `Parent.Width - 128` (`252` en card de 380) · Height `18` · **`ESTILO-CELDA-FUERTE`** · `Text` el valor |

**Paso vertical entre pares: `22`** (18 de alto + 4 de aire). El primer par arranca
en `Y = lblDlgTxt….Y + lblDlgTxt….Height + 12`; cada par siguiente encadena contra
el anterior (`Y = <par anterior>.Y + 22`), nunca con literales sueltos (regla 0.9).

> Cuándo usar cuál: `kv` cuando el usuario tiene que **verificar datos** antes de
> confirmar (Eliminar tula); banner cuando tiene que **entender la consecuencia**
> (esto va como solicitud). Un diálogo puede llevar los dos.

### 7.3 Fila clicable de galería

| Control | Tipo | Propiedades |
|---|---|---|
| `gal<Nombre><Scr>` | Galería vertical en blanco | `TemplatePadding` `0` · `ShowScrollbar` `true` · `TemplateSize`: `40` grilla de captura · `46` lista maestra de dos líneas · `36` mini-tabla · `Items` = **colección local** (nunca un `Filter` sobre datasource dentro de una galería) · **`OnSelect` = TODA la lógica de la fila** |
| `btnFila<Nombre><Scr>` | Botón clásico, **al FONDO del z-order del template** | X `0` · Y `0` · Width `gal<Nombre><Scr>.Width` · Height `gal<Nombre><Scr>.TemplateSize` · `Text` `""` · `Fill` `Color.Transparent` · `HoverFill` `ThemeSurface2` · `PressedFill` `ThemeAccentSoft` · `BorderThickness` `0` · **`OnSelect` `Select(Parent)`** |
| `shpSel<Nombre><Scr>` | Botón clásico (receta 7.1), debajo de todo salvo el overlay | X `0` · Y `0` · Width = Width de la galería · Height = `TemplateSize` · `Fill` `If(ThisItem.IsSelected, ThemeAccentSoft, Color.Transparent)` · `Radius` `0` · `BorderThickness` `0` · `DisplayMode` `DisplayMode.Edit` · `OnSelect` `Select(Parent)` |
| `shpSelBarra<Nombre><Scr>` | Rectángulo clásico | X `0` · Y `0` · Width `3` · Height = `TemplateSize` · `Fill` `ThemeAccent` · `Visible` `ThisItem.IsSelected` |
| `shpSep<Nombre><Scr>` | Rectángulo clásico | X `0` · Y = **`TemplateSize − 1`** · Width = Width de la galería · Height `1` · `Fill` `ThemeLineSoft` |

**Reglas**:

- La acción va en el **`OnSelect` de la GALERÍA**. **TODO hijo del template**
  (etiquetas, píldoras, superficies) lleva `OnSelect: Select(Parent)`. Si la lógica
  vive solo en el botón overlay, las etiquetas se comen el clic y el usuario tiene
  que cazar píxeles vacíos.
- El overlay va **al FONDO** del z-order del template: si queda arriba, su
  `HoverFill` opaco tapa el texto de la fila.
- **Excepción**: los controles del template que son *editables* (entradas de texto,
  combos, botón de papelera, chip de tulas) **no** llevan `Select(Parent)` — tienen
  su propia acción. Es el caso de la grilla de captura, donde la fila no "navega".
- Coordenadas: `X` del control del template = `X` del encabezado − `X` de la galería.

### 7.4 Píldora de estado (`pil…`)

Botón clásico como badge de solo lectura (receta 7.1):

| Propiedad | Valor |
|---|---|
| `DisplayMode` | `DisplayMode.View` (o `Edit` + `OnSelect: Select(Parent)` si está dentro de un template clicable) |
| `Radius` (las 4) | `12` |
| `Height` | `22` |
| `Size` / `FontWeight` | `7.88` / `FontWeight.Semibold` |
| `Width` | `14 + 6.2 * Len(Self.Text)` (ajustá a ojo si el texto respira mal) |
| `BorderColor` | `ThemeLine` |
| `BorderThickness` | `If(Self.Fill = ThemeLineSoft, 1, 0)` — la variante **neutra** es la única con borde: sin él no se ve sobre card blanca (igual que `.pMuted` del mock) |
| `HoverFill` / `PressedFill` | iguales al `Fill` |
| `Text` / `Fill` / `Color` | según la tabla de mapeo |

**Mapeo `ReceptionStatus`** (`Text`, `Fill`, `Color` — los tres con el mismo `Switch`):

```powerfx
// Text
Switch(
    ThisItem.ReceptionStatus,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso', "En progreso",   // 159460000
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido,      "Recibido",      // 159460001
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda',   "En bóveda",     // 159460003
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado,       "Anulado",       // 159460002
    "—"
)
// Fill
Switch(
    ThisItem.ReceptionStatus,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso', ThemeInfoSoft,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido,      ThemeOkSoft,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda',   ThemeLineSoft,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado,       ThemeDangerSoft,
    ThemeLineSoft
)
// Color
Switch(
    ThisItem.ReceptionStatus,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En progreso', ThemeInfo,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido,      ThemeOk,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.'En boveda',   ThemeInk3,
    'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Anulado,       ThemeDanger,
    ThemeInk3
)
```

**Mapeo `BagStatus`**:

```powerfx
// Text
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                           "Recibida en esclusa",   // 159460000
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                          "Almacenada en bóveda",  // 159460001
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',"Pendiente supervisor",  // 159460005
    ThisItem.BagStatus   // etiqueta cruda del option set; si Studio pide conversión, Text(ThisItem.BagStatus)
)
// Fill
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                            ThemeInfoSoft,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                           ThemeOkSoft,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  ThemeWarnSoft,
    ThemeLineSoft
)
// Color
Switch(
    ThisItem.BagStatus,
    'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa',                            ThemeInfo,
    'BagStatus (CH-Tbl-Bags)'.'Almacenada en boveda',                           ThemeOk,
    'BagStatus (CH-Tbl-Bags)'.'Pendiente de revision por supervisor - esclusa',  ThemeWarn,
    ThemeInk3
)
```

> El punto de 6 px que el mockup dibuja dentro de la píldora (`.pill::before`) se
> descarta: un control extra por píldora no paga. Divergencia declarada.

**Variante B — espejo ENTERO `BatchStatus`** (planillas). `CH-Tbl-Receptions.BatchStatus`
es una columna `int` (§5.1), **no** un option set: el `Switch` compara contra los
números CRUDOS. Todo lo demás (`Radius` 12, `Height` 22, `Width`, `BorderThickness`,
`Hover`/`Pressed`, `Size`, `FontWeight`) sale **tal cual** de la tabla de arriba.

```powerfx
// Text
Switch(ThisItem.BatchStatus,
    159460000, "En progreso",
    159460001, "Recibido",
    159460003, "En bóveda",
    159460002, "Anulado",
    "—")
// Fill
Switch(ThisItem.BatchStatus,
    159460000, ThemeInfoSoft,
    159460001, ThemeOkSoft,
    159460003, ThemeLineSoft,
    159460002, ThemeDangerSoft,
    ThemeLineSoft)
// Color
Switch(ThisItem.BatchStatus,
    159460000, ThemeInfo,
    159460001, ThemeOk,
    159460003, ThemeInk3,
    159460002, ThemeDanger,
    ThemeInk3)
```

> Los colores son **los mismos** que los del option set `ReceptionStatus`: es el
> mismo estado visto por otro camino. Si cambiás un color, cambialo en las dos.

**Variante C — píldora FUERA de un template (`.Selected`)**. Los `Switch` de arriba
usan `ThisItem`, que **solo existe dentro del template de una galería**. En un panel
de detalle la píldora vive fuera, y `ThisItem` no compila. La sustitución es
**mecánica y total**: `ThisItem` → `gal<Lista>.Selected`, en las tres propiedades.

| Dónde vive la píldora | Qué va en el `Switch` |
|---|---|
| Template de galería | `ThisItem.ReceptionStatus` · `ThisItem.BatchStatus` · `ThisItem.BagStatus` |
| Panel de detalle / header / diálogo | `galLotesLot.Selected.ReceptionStatus` · `galPlanillasLot.Selected.BatchStatus` · `ctxTulaSelLot.BagStatus` |
| Chip del header de `02` (no hay galería) | `gvarLoteActivo.ReceptionStatus` |

> ⚠️ Si la píldora está **dentro** de un template clicable, además lleva
> `DisplayMode.Edit` + `OnSelect: Select(Parent)` (receta 7.3): sin eso se come el
> clic de la fila. Fuera del template va en `DisplayMode.View`.

### 7.5 Guardado por lote con `IfError` (sin timer, sin form oculto)

Muere `'timer Submit'` + `varCurrentRow`/`varFirstRow`/`varRowIndex` +
`colPlanillasGuardadas` de la v1. El guardado es **una sola llamada** en el camino
feliz, y **fila por fila SOLO si algo falló** (que es cuando de verdad necesitás
saber cuál).

```powerfx
// ===== 0. Guardas =====
If(
    CountRows(colPlanillas) = 0,
    Notify("No hay planillas que guardar.", NotificationType.Warning),

    // ===== 1. Validación (única fuente: fxProblemas) =====
    ClearCollect(colProblemas, fxProblemas);
    If(
        !IsEmpty(colProblemas),
        Notify("Corregí los problemas de validación antes de guardar.", NotificationType.Error),

        // ===== 2. Guardado =====
        // SIN bandera "en vuelo": canvas NO repinta a mitad de una fórmula y todo
        // esto corre dentro de UN solo OnSelect ⇒ un ctxGuardando: true acá no
        // llega a pintarse NUNCA. El overlay se abre directamente con el resultado.
        Clear(colFallidas);

        // --- FASE A: un solo Patch con tabla (camino feliz) ---
        IfError(
            Patch(
                'CH-Tbl-Receptions',
                // registros BASE: alineados fila a fila con el tercer argumento.
                // Defaults() crea; el LookUp es sobre colección LOCAL (cero consultas).
                ForAll(
                    colPlanillas As P,
                    If(
                        IsBlank(P.GUID),
                        Defaults('CH-Tbl-Receptions'),
                        LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = P.GUID)
                    )
                ),
                // registros de CAMBIO
                ForAll(
                    colPlanillas As P,
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

            // --- FASE B: algo falló. Reintento fila por fila para saber CUÁL ---
            ForAll(
                colPlanillas As P,
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
                    // 🧨 El brazo de fallo TERMINA en Blank(): Collect() devuelve una
                    //    TABLA y IfError exige que el brazo tenga el tipo del Patch
                    //    (un REGISTRO). Sin el "; Blank()" NO COMPILA.
                    Collect(
                        colFallidas,
                        { LocalId: P.LocalId, Planilla: P.Planilla, Mensaje: FirstError.Message }
                    );
                    Blank()
                )
            )
        );

        // ===== 3. Re-hidratación: UNA consulta delegable, y re-keyado de GUIDs =====
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

        // ===== 4. Cierre =====
        If(
            IsEmpty(colFallidas),
            Set(gvarCambiosSinGuardar, false);
            Notify("Lote guardado: " & CountRows(colPlanillas) & " planillas.", NotificationType.Success),
            Notify(
                CountRows(colFallidas) & " planilla(s) rechazadas — revisá el panel y reintentá.",
                NotificationType.Error
            )
        )
    )
)
```

**Reglas de la receta**:

1. **`gvarCambiosSinGuardar` solo pasa a `false` si `colFallidas` está vacía.** La UI
   NUNCA marca como guardado lo que el guard rechazó (mata el bug N3 de la v1:
   `OnSuccess` marcaba planillas guardadas aunque el guard rechazara las bolsas).
2. **`colPlanillas` se re-keya desde Dataverse**, no desde el orden de retorno del
   `Patch`: el N° de planilla es único dentro del lote (lo garantizan las reglas 5 y 6
   de validación), así que sirve de clave natural para traer el GUID real. Sin esto,
   un segundo guardado duplica registros.
3. **Reintentar fallidas**: el botón del diálogo de resultado ejecuta la MISMA
   estructura, pero el `ForAll` corre sobre
   `Filter(colPlanillas, LocalId in colFallidas.LocalId)` (o
   `Filter(colPlanillas, !IsBlank(LookUp(colFallidas, LocalId = ThisRecord.LocalId)))`).
   Antes de reintentar: `Clear(colFallidas)`.
4. **Las tulas van en un segundo bloque idéntico** contra `CH-Tbl-Bags`, con base
   `LookUp(colTulasDV, 'CH-Tbl-Bags' = T.GUID)`, cambio
   `{ BagID: Trim(T.Numero), Name: Trim(T.Numero), ReceptionID: <planilla dueña>,
   BagStatus: 'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa' }` (159460000) y
   re-hidratación con
   `Filter('CH-Tbl-Bags', ReceptionBatchId.'CH-Tbl-ReceptionBatch' = gvarLoteActivo.'CH-Tbl-ReceptionBatch')`
   (espejo B-2, regla D-7). La planilla dueña se resuelve con
   `LookUp(colPlanillasDV, 'CH-Tbl-Receptions' = LookUp(colPlanillas, LocalId = T.PlanillaLocalId).GUID)`
   — todo sobre colecciones locales. **Las tulas se guardan DESPUÉS de las planillas**
   (necesitan el GUID de la planilla ya re-keyado).
5. 🧨 **PASO DE BAJAS — obligatorio, y va ANTES del alta/actualización de tulas.**
   Quitar una tula del panel de edición **no la borra de Dataverse**: si el paso no
   existe, la tula queda **huérfana** en la base (era el bug **N2** de la v1 por otra
   puerta). El panel de tulas acumula los quitados **que ya tenían GUID** en una
   colección de bajas (`colTulasBorradasCap`, `02` §7.2), y el guardado la vacía así:

   ```powerfx
   // ===== PASO DE BAJAS: Remove por REGISTRO, nunca RemoveIf (regla D-3) =====
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
   ```

   Reglas del paso:
   - **`Filter(…, !IsBlank(GUID))`**: una tula que nunca se guardó no tiene nada que
     borrar; intentarlo tira error espurio.
   - **`Remove` con el registro explícito** sacado de `colTulasDV`, jamás
     `RemoveIf(ds, predicado)` (regla D-3).
   - Cada baja va en su **propio `IfError`** y una baja fallida **cuenta como
     fallida** (`colFallidas`) ⇒ `gvarCambiosSinGuardar` sigue en `true` y el lote
     **no se cierra**.
   - **La colección local de bajas se `Clear`ea DESPUÉS** de la re-hidratación del
     paso 6, nunca antes: si se limpia primero y el `Remove` falló, la baja se pierde
     en silencio.
   - `02` lo implementa en su §8.2 **paso 4** — misma forma, mismos nombres.
6. **Cero `Refresh()`** (regla D-5): el estado local se actualiza con la
   re-hidratación del paso 3. Esta receta **nunca** llama a `Refresh`.
7. 🧨 **Todo brazo de fallo de un `IfError` que acumula con `Collect` TERMINA en
   `; Blank()`.** `IfError(valor, brazo)` exige que el brazo tenga **el tipo del
   primer argumento**: el `Patch` devuelve un **registro** y `Collect` devuelve una
   **tabla** ⇒ `IfError(Patch(…), Collect(colFallidas, {…}))` **no compila**. El
   `; Blank()` final le da al brazo el tipo correcto y no cambia una coma de la
   lógica. Vale para las TRES apariciones: fase B de planillas, fase B de tulas y el
   paso de bajas. (Hallazgo del QA humano 16-ago.)
8. 🧨 **Esta receta define QUÉ se escribe, no CUÁNDO se dispara.** El disparo lo
   define la **receta 7.11**: el `OnSelect` del botón calcula el delta y prende el
   timer; el guardado real corre en el `OnTimerEnd`. Nunca metas este bloque entero
   en un `OnSelect` — el diálogo *"Guardando…"* no llegaría a pintarse.

### 7.6 Escape HTML (obligatorio en TODO dato interpolado)

Sin UDFs no hay función con parámetros, así que el escape se escribe **inline**, una
vez por dato, dejando el resultado en una variable de contexto que después consume el
template (7.7):

```powerfx
UpdateContext({
    ctxEPlanilla:
        Substitute(
            Substitute(
                Substitute( Trim(galPlanillasLot.Selected.ReceiptID), "&", "&amp;" ),
                "<", "&lt;"
            ),
            ">", "&gt;"
        )
})
```

**Reglas**:

1. **El `&` SIEMPRE primero.** Si escapás `<` antes que `&`, después el `&` de
   `&lt;` se vuelve `&amp;lt;` y el supervisor ve basura.
2. Se escapan **`&`, `<`, `>`** — y con eso alcanza porque **está prohibido
   interpolar datos dentro de un atributo HTML**. Todo dato va en un nodo de texto
   (`<td>…</td>`, `<h3>…</h3>`). Si alguna vez hiciera falta un atributo, hay que
   agregar `"` → `&quot;` a la cadena.
3. Se escapa **TODO** lo que venga de una entrada de usuario o de Dataverse: N° de
   planilla, N° de tula, nombre de empresa, transportadora, nombre de usuario. La
   inyección HTML de la v1 estaba confirmada.
4. Los valores numéricos formateados (`Text(x, FmtMoney)`) y los literales de la app
   no necesitan escape, pero escaparlos no rompe nada.

### 7.7 Template HTML de solicitud de aprobación

**Un solo template** para los 5 tipos. El visor del supervisor extrae el bloque
`<style>` y lo aplica, así que el estilo viaja con la solicitud.

- El `<style>` completo está en `App.Formulas` como `HtmlApEstilo` (§3.1) — literal
  **sin** `$"..."`: si usás interpolación, **cada llave de CSS hay que duplicarla**
  (`{{`/`}}`) y el template se vuelve ilegible.
- Dentro del CSS se usan **comillas simples**; en los atributos HTML, comillas dobles
  → en Power Fx se escriben **duplicadas** (`class=""ap""`).
- **El encabezado de tabla tiene DOS variantes** (§3.1), y la elige `ctxEncabezado`:
  - **SIMPLE** — `HtmlApEncabezadoTablaSimple` (`Campo / Valor`, 2 columnas) para
    **Agregar tula** y **Eliminar tula**: no hay "valor actual" contra "valor
    solicitado", solo se enumeran campos. En esas dos variantes las filas van **sin
    `colspan`** (una tabla de 2 columnas no necesita fusionar nada).
  - **COMPARATIVA** — `HtmlApEncabezadoTabla` (`Campo / Actual / Solicitado`, 3
    columnas) para **Remapear tula**, **Editar tula** y **Modificar numero de
    planilla**: ahí sí hay un antes y un después. Las filas de esas variantes que no
    tienen "valor actual" usan `colspan="2"`.

  Es exactamente lo que hace el mockup con `previewApproval(conActual)`: el mismo
  template, dos encabezados. Un `colspan="2"` sobre la tabla simple pintaría una
  columna fantasma.

**Esqueleto común** (lo que envuelve a las filas de cada variante):

```powerfx
UpdateContext({
    ctxHtml:
        HtmlApEstilo &
        "<div class=""ap"">" &
          "<div class=""apHead"">" &
            "<span class=""tag"">" & ctxETipo & "</span>" &
            "<h3>Lote " & ctxELote & "</h3>" &
            "<div class=""sub"">" & ctxETransportadora & " · " & ctxEEstadoLote & "</div>" &
          "</div>" &
          If(ctxEncabezado = "simple", HtmlApEncabezadoTablaSimple, HtmlApEncabezadoTabla) &
            ctxFilas &                      // <- lo arma cada variante (abajo)
          "</table>" &
          "<div class=""foot"">Solicitud generada por CH-App-Reception · " &
            ctxEUsuario & " · " & Text(Now(), FmtFecha) & "</div>" &
        "</div>"
})
```

Donde `ctxETipo` es la **etiqueta de presentación** del tipo, en MAYÚSCULAS: para los
cuatro tipos cuyo `RequestType` ya está bien escrito alcanza con `Upper(...)` sobre el
string de §5.3 (`Upper` sobre un literal local no toca delegación). Para
`"Modificar numero de planilla"` **no**: ese string es **contrato** con
CH-App-Supervisor y viaja tal cual en el `Patch`, pero la etiqueta que ve el humano se
escribe como literal aparte (`"MODIFICAR N° DE PLANILLA"`). **Contrato y presentación
son dos cadenas distintas** — arreglar la ortografía del `RequestType` crearía un tipo
fantasma. `ctxEncabezado` es `"simple"` o `"comparativo"` (arriba), y `ctxELote`,
`ctxETransportadora`, `ctxEEstadoLote`, `ctxEUsuario` vienen escapados por 7.6.

> **Sobre los nombres `ctx…` de esta receta y de 7.6**: son variables **de
> contexto**, o sea de pantalla — la regla de nombre único de §0.5 aplica a
> **controles**, no a ellas. Aun así, cada pantalla les pone su sufijo para que el
> YAML se lea sin ambigüedad: las cinco variantes viven en `scrLotes`, así que los
> nombres reales son `ctxETipoLot`, `ctxELoteLot`, `ctxFilasLot`, `ctxHtmlLot`, …
> (inventario completo en §4). Leé `ctxXxx` acá como `ctxXxxLot` allá.

**Las 5 variantes de armado de filas** (`ctxFilas`):

```powerfx
// 1) "Agregar tula"  — encabezado SIMPLE (ctxEncabezado = "simple"), sin colspan
"<tr><td class=""k"">Planilla</td><td>" & ctxEPlanilla & " — " & ctxEEmpresa & "</td></tr>" &
"<tr><td class=""k"">Tula solicitada</td><td><b class=""new"">" & ctxETula & "</b></td></tr>" &
"<tr><td class=""k"">Estado al aprobar</td><td>Almacenada en bóveda</td></tr>"

// 2) "Eliminar tula"  — encabezado SIMPLE (ctxEncabezado = "simple"), sin colspan
"<tr><td class=""k"">Planilla</td><td>" & ctxEPlanilla & " — " & ctxEEmpresa & "</td></tr>" &
"<tr><td class=""k"">Tula a eliminar</td><td><b>" & ctxETula & "</b></td></tr>" &
"<tr><td class=""k"">Estado actual</td><td>" & ctxEBagPrevio & "</td></tr>"

// 3) "Remapear tula"  — encabezado COMPARATIVO (ctxEncabezado = "comparativo")
"<tr><td class=""k"">Tula</td><td colspan=""2""><b>" & ctxETula & "</b></td></tr>" &
"<tr><td class=""k"">Planilla</td><td>" & ctxEPlanilla & " — " & ctxEEmpresa & "</td>" &
    "<td><b class=""new"">" & ctxEPlanillaDestino & " — " & ctxEEmpresaDestino & "</b></td></tr>" &
"<tr><td class=""k"">Estado actual</td><td colspan=""2"">" & ctxEBagPrevio & "</td></tr>"

// 4) "Editar tula"  — encabezado COMPARATIVO (ctxEncabezado = "comparativo")
"<tr><td class=""k"">Planilla</td><td colspan=""2"">" & ctxEPlanilla & " — " & ctxEEmpresa & "</td></tr>" &
"<tr><td class=""k"">N° de tula</td><td>" & ctxETula & "</td>" &
    "<td><b class=""new"">" & ctxETulaNueva & "</b></td></tr>" &
"<tr><td class=""k"">Estado de la tula</td><td colspan=""2"">Pendiente de revisión por supervisor</td></tr>"

// 5) "Modificar numero de planilla"  — encabezado COMPARATIVO (ctxEncabezado = "comparativo")
"<tr><td class=""k"">Empresa</td><td colspan=""2"">" & ctxEEmpresa & "</td></tr>" &
"<tr><td class=""k"">N° de planilla</td><td>" & ctxEPlanilla & "</td>" &
    "<td><b class=""new"">" & ctxEPlanillaNueva & "</b></td></tr>"
```

**El `Patch` de la solicitud** (mismo para los 5, cambia `RequestType` y el JSON):

```powerfx
IfError(
    Patch(
        'CH-Tbl-ApprovalRequests',
        Defaults('CH-Tbl-ApprovalRequests'),          // Name es AUTONUMBER: no se toca
        {
            RequestType:      "Agregar tula",         // string EXACTO de §5.3
            RequestDetails:   ctxHtml,
            RequestResources: JSON({
                ReceiptGUID:         Text(galPlanillasLot.Selected.'CH-Tbl-Receptions'),
                BagID:               Trim(inpTulaAgregarLot.Text),
                BagGUID:             Text(ctxTulaNuevaLot.'CH-Tbl-Bags'),
                ReceptionBatchGUID:  Text(galPlanillasLot.Selected.ReceptionBatch.'CH-Tbl-ReceptionBatch')
            })
            // ApprovalStatus NO se setea: en blanco = Pendiente (§5.5)
        }
    ),
    Notify("No se pudo crear la solicitud: " & FirstError.Message, NotificationType.Error)
)
```

> Las claves del JSON son **contrato con CH-App-Supervisor**: los nombres y el
> conjunto por tipo salen de §5.5 y no se tocan. `JSON()` **siempre** — nunca
> concatenación de comillas.

### 7.8 Segmentado de navegación del header

Reemplaza al `ExpandMenu` (y a sus 11 280 líneas de SVG por pantalla). Va en el
header de **las dos** pantallas, con sufijo distinto (regla 0.5).

| Control | Tipo | Propiedades |
|---|---|---|
| `shpSegNav<Scr>` | Botón clásico (receta 7.1) | Width **`179`** (= `3` + `85` + `3` + `85` + `3`: padding **simétrico** de 3 px a los cuatro lados; con `176` el último segmento quedaba pegado al borde derecho) · Height `32` · `Radius` (las 4) `16` · `Fill`/`Hover`/`Pressed` `ThemeSurface2` · `BorderColor` `ThemeLine` · `BorderThickness` `1` · `DisplayMode` `DisplayMode.View` · **al FONDO** |
| `segCaptura<Scr>` | Botón clásico | X `shpSegNav<Scr>.X + 3` · Y `shpSegNav<Scr>.Y + 3` · Width `85` · Height `26` · `Radius` (las 4) `13` · Size `9` · `Text` `"Captura"` |
| `segLotes<Scr>` | Botón clásico | X `segCaptura<Scr>.X + segCaptura<Scr>.Width + 3` · Y = la de `segCaptura<Scr>` · Width `85` · Height `26` · `Radius` (las 4) `13` · Size `9` · `Text` `"Lotes"` |

**Estado activo/inactivo** (cada segmento compara contra SU pantalla):

| Propiedad | En `segCapturaCap` / `segLotesLot` (activo) | En el otro (inactivo) |
|---|---|---|
| `Fill` / `HoverFill` / `PressedFill` | `ThemeSurface` (las tres) | `Color.Transparent` / `ThemeSurface` / `ThemeSurface` |
| `Color` | `ThemeAccent` | `ThemeInk2` |
| `FontWeight` | `FontWeight.Semibold` | `FontWeight.Normal` |
| `BorderThickness` | `0` | `0` |
| `OnSelect` | `false` (ya estás ahí — el mockup también ignora el clic) | ver abajo |

**`OnSelect` de los inactivos**:

```powerfx
// segLotesCap (en scrCaptura) — con guarda de cambios sin guardar
If(
    gvarCambiosSinGuardar,
    UpdateContext({ctxDlgSalirCap: true}),   // diálogo de 3 botones (receta 7.2)
    Navigate(scrLotes)
)

// segCapturaLot (en scrLotes) — sin guarda: scrLotes no acumula ediciones locales
Navigate(scrCaptura)
```

**Diálogo de salida `ctxDlgSalirCap`** (receta 7.2, variante de 3 botones):

| Botón | Estilo | `OnSelect` |
|---|---|---|
| Cancelar | `ESTILO-BTN-FANTASMA` | `UpdateContext({ctxDlgSalirCap: false})` |
| Descartar | `ESTILO-BTN-CONTORNO` | `UpdateContext({ctxDlgSalirCap: false}); Set(gvarCambiosSinGuardar, false); Clear(colPlanillas); Clear(colTulas); Clear(colTulasBorradasCap); Navigate(scrLotes)` |
| Guardar y salir | `ESTILO-BTN-PRIMARIO` | `UpdateContext({ctxDlgSalirCap: false, ctxSalirTrasGuardarCap: true}); Select(btnGuardarCap)` — 🧨 **SIN `Navigate`**: lo hace el `OnTimerEnd` (receta 7.11.C, regla 2) |

> 🧨 **`Descartar` VACÍA las colecciones de edición**, no solo baja el flag. Si
> bajara el flag y nada más, "descartar" sería una **mentira**: lo tecleado sigue
> vivo en memoria, volver a Captura lo muestra igual y a la primera edición se
> guarda todo junto. Los snapshots `…DV` **no** se tocan: son la copia fiel de
> Dataverse y son los que hacen que el `OnVisible` re-proyecte el lote **como está
> guardado** (receta 7.10, guarda `IsEmpty(colPlanillas)`).
>
> 🧨 **"Guardar y salir" NO navega en su `OnSelect`.** Los timers **no corren en
> pantallas que no están visibles** (receta 7.11.C, regla 2): un `Navigate()` acá
> deja la pantalla invisible antes de que dispare `tmrGuardarCap` y el guardado **no
> ocurre nunca**. La navegación vive en el `OnTimerEnd`, condicionada a
> `ctxSalirTrasGuardarCap && IsEmpty(colFallidas)` — o sea, se navega **solo si no
> hubo fallidas**; si el guard rechazó algo, te quedás en Captura con el panel de
> fallidas a la vista. La ✕ del diálogo ejecuta lo mismo que Cancelar.
> `App.BackEnabled = false` (§3.4) es lo que impide saltarse esta guarda con el back
> del navegador.
>
> **Son DOS señales distintas, no una**: `ctxGuardarDesdeCierreCap` dispara la
> transición del lote a `Recibido`; `ctxSalirTrasGuardarCap` dispara la navegación.
> Confundirlas cierra el lote cuando el usuario solo quería salir.

### 7.9 Cierre de lote client-side (con nota B0-3)

> ⚠️ **Dependencia de backend declarada**: la Custom API `sanic_CloseReceptionBatch`
> (tarea **B0-3** del plan) **NO EXISTE todavía**. Hasta que exista, el cierre se hace
> client-side con esta receta. **Cuando B0-3 exista, TODO el bloque 2–3 de abajo se
> reemplaza por**:
> ```powerfx
> IfError(
>     Environment.sanic_CloseReceptionBatch({ ReceptionBatchId: Text(gvarLoteActivo.'CH-Tbl-ReceptionBatch') }),
>     Notify("El cierre fue rechazado: " & FirstError.Message, NotificationType.Error)
> )
> ```
> (requiere agregar el datasource **Environment**). Con B0-3 el cierre es **atómico** y
> el chequeo de duplicados se hace contra la tabla COMPLETA server-side.

**Limitación honesta del cierre client-side**: valida duplicados **solo dentro del
lote en edición** (reglas 5 y 6 de `fxProblemas`, sobre colecciones locales).
**No** puede detectar una tula duplicada en OTRO lote del entorno sin hacer una
consulta por tula (N+1, y con `RemoveIf`/`CountRows` sobre datasource violaría D-2).
Ese chequeo es exactamente lo que aporta B0-3. Mientras tanto, el duplicado
cross-lote lo rechaza el `GuardReceptionPlugin` en el `Patch` y el mensaje llega por
`IfError` — feo, pero no silencioso.

```powerfx
// ===== 1. Guarda de habilitación (mismo criterio que DisplayMode del botón) =====
If(
    !fxPuedeCerrar,
    Notify(
        If(
            IsBlank(gvarLoteActivo), "No hay un lote activo.",
            CountRows(colPlanillas) = 0, "El lote no tiene planillas.",
            "Corregí los problemas de validación antes de cerrar."
        ),
        NotificationType.Error
    ),

    // ===== 2. Persistir TODO primero (receta 7.5 completa: planillas y tulas) =====
    //     Se invoca el guardado real, no una copia: Select(btnGuardarCap).
    Select(btnGuardarCap);

    // ===== 3. Transición del lote — SOLO si el guardado no dejó fallidas =====
    If(
        !IsEmpty(colFallidas),
        Notify(
            "El lote NO se cerró: hay planillas rechazadas. Reintentá y volvé a cerrar.",
            NotificationType.Error
        ),
        IfError(
            Patch(
                'CH-Tbl-ReceptionBatches',
                gvarLoteActivo,
                { ReceptionStatus: 'ReceptionStatus (CH-Tbl-ReceptionBatches)'.Recibido }   // 159460001
            ),
            Notify("El cierre fue rechazado: " & FirstError.Message, NotificationType.Error),
            // rama de ÉXITO de IfError
            Set(gvarLoteActivo, Blank());
            Clear(colPlanillas); Clear(colTulas);
            Clear(colPlanillasDV); Clear(colTulasDV);
            Clear(colProblemas); Clear(colFallidas);
            Set(gvarCambiosSinGuardar, false);
            Notify("Lote cerrado. Pasó a Recibido y ya aparece en la pantalla Lotes.", NotificationType.Success)
        )
    )
)
```

**Reglas**:

1. **`varSoloGuardar` muere.** El bug N1 de la v1 era una variable de estado que
   quedaba pegada tras un fallo y convertía "Cerrar lote" en un no-op. Acá la
   decisión se toma con `colFallidas`, que se limpia al empezar cada guardado.
2. **El cierre nunca se ejecuta con fallidas pendientes.** Es la diferencia entre
   "cerrado con datos a medias" y "no cerrado".
3. **Confirmación previa obligatoria** (receta 7.2): el botón `Cerrar lote` abre el
   diálogo, y este bloque vive en el `OnSelect` del botón **confirmar** del diálogo.
4. **"Mover a bóveda"** (en `03`) sigue la misma forma: `Patch` con tabla sobre
   `CH-Tbl-Bags` + `Patch` del lote a `'En boveda'` (159460003), ambos dentro de
   `IfError`, y el lote se mueve **solo si las tulas se movieron**. No es atómico
   client-side: se declara en el checklist de `03`.

   🧨 **Pasan SOLO las tulas en `'Recibida en esclusa'` (159460000), NO todas las del
   lote.** El universo se carga con **una** consulta delegable:

   ```powerfx
   ClearCollect(
       colTulasBovedaLot,
       Filter(
           'CH-Tbl-Bags',
           // espejo B-2 (D-7), PK contra PK (D-1): la igualdad de registro NO compila
           ReceptionBatchId.'CH-Tbl-ReceptionBatch' = galLotesLot.Selected.'CH-Tbl-ReceptionBatch'
           && BagStatus = 'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa' // 159460000
       )
   )
   ```

   Por qué el filtro es parte de la regla y no un detalle:
   - **Da IDEMPOTENCIA.** Si el `Patch` de tulas pasó pero el del lote falló (el caso
     "quedó a medias", que client-side es posible), volver a ejecutar *Mover a
     bóveda* recarga el universo y encuentra **cero** tulas en esclusa: no vuelve a
     tocar lo que ya está en bóveda, solo reintenta la transición del lote.
   - **No pisa estados de otros dueños.** Un lote puede tener tulas en
     `'Pendiente de revision por supervisor - esclusa'` (159460005) o en
     `Asignada a cajero` (Recuento). Mandarlas a `'Almacenada en boveda'` sería
     pisar una decisión que no es de esta app.
   - El contador del diálogo sale de `CountRows(colTulasBovedaLot)` — **colección
     local**, nunca `CountRows(Filter(ds, …))` (regla D-2).
   - **Caso borde declarado**: si la colección queda vacía, el lote igual transiciona
     a `'En boveda'` (no tenía tulas en esclusa). Es una rama explícita, no un
     no-op silencioso.

### 7.10 Proyección `…DV → colPlanillas` / `colTulas`

**La ÚNICA definición de esta proyección en toda la app.** La corre el `OnVisible`
de `scrCaptura` (`02` §1, bloque 2); `03` **no** la escribe — deja los dos snapshots
`…DV` y las colecciones de edición vacías (§4.3) y esta receta hace el resto. Si
alguna vez existieran dos copias, el re-keyado de la receta 7.5 dejaría de encontrar
sus filas y el segundo guardado duplicaría registros.

**Precondición**: ninguna. La rehidratación de `Empresa` va **contra el datasource**
`CH-Tbl-ClientBranches`, no contra un catálogo local (L-23 / regla **D-10**: el
catálogo local truncaba a 2000 y se eliminó).

```powerfx
// Se dispara SOLO si hay lote, las de edición están vacías y los snapshots no.
// El If va POR FUERA del ClearCollect: ClearCollect(col, If(cond, …)) SIN else
// revienta con ErrorKind.Internal.
If(
    !IsBlank(gvarLoteActivo) && IsEmpty(colPlanillas) && !IsEmpty(colPlanillasDV),

    ClearCollect(
        colPlanillas,
        ForAll(
            colPlanillasDV As D,
            {
                LocalId:     Text(D.'CH-Tbl-Receptions'),   // §4.1: persistida => GUID real en texto
                GUID:        D.'CH-Tbl-Receptions',
                // Empresa = REGISTRO FRESCO y COMPLETO de sucursal, traido del
                // DATASOURCE por PK. Guardar D.BranchName (la referencia del lookup)
                // haria que P.Empresa.Client / P.Empresa.Name cuesten un fetch POR
                // FILA en el Patch — y ademas RemitentClient saldria en blanco.
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
                // El GUID del lookup viaja como anotacion en la fila YA traida:
                // no cuesta un fetch. Y calza con el LocalId de su planilla dueña.
                PlanillaLocalId: Text(B.ReceptionID.'CH-Tbl-Receptions'),
                Numero:          B.BagID,
                GUID:            B.'CH-Tbl-Bags'
            }
        )
    )
)
```

**Reglas de la receta**:

1. **Los dos `ForAll` iteran colecciones LOCALES** (`colPlanillasDV`, `colTulasDV`),
   nunca un datasource: cero consultas anidadas por el lado de la iteración.
2. **Ningún `Set()` adentro** (§3.2): estos `ForAll` solo **proyectan registros**.
3. **`PlanillaLocalId` = `Text(<GUID de la planilla dueña>)`** y `LocalId` de la
   planilla = `Text(<su GUID>)` ⇒ el join local cierra por construcción (§4.1).
4. 🧨 **El `LookUp` de `Empresa` SÍ toca la red — una vez por planilla del lote, y
   está aceptado.** Es un `eq` por PK contra `CH-Tbl-ClientBranches`: delegable,
   barato y **acotado al tamaño del lote** (decenas de planillas), no a la tabla de
   sucursales. Es el precio de haber eliminado el catálogo local (L-23), y se paga
   **solo al retomar un lote**, nunca al tipear. La alternativa —guardar
   `D.BranchName`— cuesta un fetch por fila **en cada guardado** y deja
   `RemitentClient` en blanco: mucho peor.
5. Si el `LookUp` no encuentra la sucursal (sucursal desactivada después de crear la
   planilla), `Empresa` queda en blanco y la regla *"Empresa sin seleccionar"* de
   `fxProblemas` marca la fila. Es el comportamiento correcto: el usuario tiene que
   reelegirla antes de guardar.

### 7.11 🧨 Volcado, foco y guardado diferido (patrón de grilla editable)

**La receta que nació del QA humano de la 1.31.** Cubre tres problemas que se
manifiestan juntos en toda grilla canvas editable y que se resuelven juntos.

#### A. El `OnChange` NO patchea la colección — el VOLCADO va en los botones

**Síntoma**: el operador teclea tres caracteres en un monto y el foco se le va; en
un combo, la selección "no toma". **Causa**: un `Patch` a la colección dentro de un
`OnChange` **muta el `Items` de la galería**, la galería se re-renderiza y el
control que tenía el foco deja de ser el mismo control. Ninguna cantidad de
`DelayOutput` lo arregla: `DelayOutput` retrasa el `OnChange`, no evita el
re-render.

**Patrón**:

| Dónde | Qué corre |
|---|---|
| `OnChange` de **cada** control editable de la fila | **`Set(gvarCambiosSinGuardar, true)` y NADA MÁS** |
| `OnSelect` de **cada** botón que necesita la colección al día | el **VOLCADO**, como **primera** instrucción |

El volcado lee los **controles del template** por `gal….AllItems` y los baja a la
colección de una sola pasada:

```powerfx
// ===== VOLCADO — primera línea de todo botón que lea colPlanillas =====
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

**Reglas del volcado**:

1. **`As Fila` no es opcional**: sin el alias, `Fila.inpPlanillaCap` no se resuelve
   y `LocalId` adentro del `LookUp` se ambigua con el de la colección.
2. **Va PRIMERO, siempre.** La lista cerrada de botones que lo llevan en `02`:
   `btnNuevaPlanillaCap`, `btnValidarCap`, `btnGuardarCap`, `btnCerrarCap`,
   `btnChipTulasCap`, `icoBorrarFilaCap`. Si agregás un botón que lea
   `colPlanillas`, **lleva volcado**; si no, opera sobre datos viejos.
3. **`gal….AllItems` solo devuelve las filas MATERIALIZADAS.** Con scroll virtual,
   una fila que nunca se pintó no está en `AllItems` — pero tampoco pudo editarse,
   así que su registro en la colección ya es el bueno. El `LookUp` por `LocalId`
   hace que el volcado sea un *merge*, no un reemplazo.
4. **`Coalesce(Value(…), 0)`**, nunca `Value(…)` pelado: un input vacío da *blank* y
   la regla de validación "todos los montos en 0" dejaría de disparar.

#### B. Gotchas de FOCO en galerías (tabla cerrada)

| Gotcha | Qué pasa | Qué se hace |
|---|---|---|
| **`TabIndex` positivo dentro de un template** | Cada fila **replica** el mismo número de `TabIndex`, y el orden global de tabulación agrupa **por número**, no por fila: el Tab salta a la misma columna de todas las filas antes de bajar. Filas entrelazadas | **`TabIndex` = `0` SIEMPRE** en todo control de template + orden **geométrico** (el z-order/posición manda). Un `TabIndex` positivo dentro de una galería es un bug de la guía |
| **Íconos, Imágenes y Formas nacen con `TabIndex -1`** | Quedan **fuera** del recorrido de Tab por default de plataforma. Si el ícono es la única forma de disparar una acción, esa acción es inalcanzable por teclado | `TabIndex` **`0` explícito** en todo Icono/Imagen/Forma **interactivo** (los decorativos se dejan en `-1`) |
| **Acciones destructivas en el recorrido** | Un Tab de más y un Enter borran una fila | Las destructivas (papelera de fila, borrar tula) quedan **FUERA del Tab** (`TabIndex -1`): se tocan con el mouse, a propósito |
| **`Patch` a la colección en un `OnChange`** | Re-render de la galería ⇒ **pérdida de foco** | Bloque **A** de esta receta |
| **`Reset(gal…)` con la galería visible** | Re-ancla los `Default` y **mata el foco** | Solo en `OnVisible`, nunca en un `OnChange` |
| **`SetFocus` a un control de template** | No está soportado: `SetFocus` apunta a un control, y en una galería hay N réplicas | El foco se devuelve a un control **de pantalla** (`SetFocus(inpTulaScanCap)`), nunca a uno de fila |

#### C. Guardado diferido: el timer de UN frame

**Síntoma**: el diálogo *"Guardando…"* nunca se ve; la pantalla se congela y después
aparece el resultado. **Causa**: canvas **no repinta a mitad de una fórmula**, y todo
el guardado corre dentro de un `OnSelect`. Un `ctxGuardando: true` al empezar y
`false` al terminar **no llega nunca al render**.

**Patrón**: el `OnSelect` hace lo barato y **cede el frame**; el trabajo pesado corre
en el `OnTimerEnd` de un timer de 50 ms.

| Control | Propiedades |
|---|---|
| `tmr<Accion><Scr>` | **Temporizador** (Insertar → Entrada → Temporizador) · `AutoStart` `false` · `Duration` `50` · `Repeat` `false` · **`Start` = la bandera `ctx…` "en vuelo"** · `Visible` `false` · `OnTimerEnd` = TODO el trabajo |

```powerfx
// ===== btn<Accion>.OnSelect — BARATO. Ni un Patch a Dataverse acá. =====
<VOLCADO del bloque A>;
<validación local>;
<cálculo del DELTA — ver bloque D>;
If(
    <no hay nada que hacer>,
    Notify("No hay cambios que guardar.", NotificationType.Information);
    Set(gvarCambiosSinGuardar, false),

    UpdateContext({ ctxDlg…: true, ctxGuardando…: true })   // abre el diálogo Y prende el timer
)

// ===== tmr<Accion><Scr>.OnTimerEnd — TODO el trabajo pesado =====
<el guardado real, sobre el DELTA>;
<la continuación del flujo que lo invocó>;
UpdateContext({ ctxGuardando…: false })                      // apaga el timer
```

**Reglas**:

1. **`Start` es la bandera, no `true`.** El timer arranca cuando la bandera sube y se
   desarma cuando el `OnTimerEnd` la baja. `Repeat false` + `Duration 50` = **una**
   corrida por click.
2. 🧨 **Los timers NO corren en pantallas que no están visibles.** Todo flujo que
   termine navegando (*Guardar y salir*) **navega DESDE el `OnTimerEnd`**, nunca
   antes: un `Navigate()` en el `OnSelect` mata el guardado en el aire. La señal
   viaja en una bandera (`ctxSalirTrasGuardar…`) que el `OnTimerEnd` consume.
3. **Toda continuación va adentro del `OnTimerEnd`**, no después del `Select(btn…)`
   que lo disparó: cuando el `OnSelect` del botón invocador retoma, el guardado
   **todavía no corrió**. Esto vale para el cierre de lote (`Patch` a `Recibido`) y
   para la navegación de salida.
4. **Sin spinner animado.** Entre el frame que pinta el diálogo y el `OnTimerEnd` el
   hilo se bloquea igual: un spinner se congelaría a mitad de giro y quedaría peor
   que un texto quieto. El diálogo dice *"Guardando N cambios…"* y listo.
5. 🧨 **Esto NO resucita el timer de la v1.** La prohibición de `00` §2.4 y del
   encabezado de 7.5 (*"sin timer, sin form oculto"*) apunta al `'timer Submit'` de
   la v1: una **máquina de estados** con `varCurrentRow`/`varFirstRow`/`varRowIndex`
   que iteraba **fila por fila**, con el timer como bucle. Acá el timer es un **defer
   de UN frame**, corre **una sola vez** y no lleva estado de iteración. Son dos
   cosas distintas con el mismo control: la prohibición queda **matizada**, no
   levantada. Si alguna vez ves un `Repeat true` o un contador de fila en un timer de
   esta app, eso sí es la v1 volviendo.

#### D. Delta: se patchea lo que CAMBIÓ, no todo

Guardar las N planillas del lote en cada click reescribe filas idénticas, dispara el
`GuardReceptionPlugin` de más y hace el guardado O(N) cuando debería ser O(cambios).
El `OnSelect` calcula el delta **antes** de prender el timer:

```powerfx
// Planillas: las NUEVAS (sin GUID) + las que difieren del snapshot campo a campo
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

// Tulas: las nuevas. Las BAJAS ya eran delta por construcción (colTulasBorradasCap).
ClearCollect(colTulasNuevasCap, Filter(colTulas, IsBlank(GUID)));
```

**Reglas**:

1. **Todo corre sobre colecciones locales** (`colPlanillas` ↔ `colPlanillasDV`):
   cero red, cero delegación que romper.
2. **La comparación de `Empresa` es por PK**, no por registro (D-1).
3. **"Sin cambios" es una rama de primera clase**: `Notify` informativo, **sin
   diálogo**, y `gvarCambiosSinGuardar` a `false`. Un modal para decir "no hice nada"
   es un clic robado.
4. 🧨 **Después de guardar, el re-keyado re-hidrata `colPlanillasDV`** —
   **`colPlanillasDV`, no `colTulasDV`**. Escribir mal esa colección fue un bug real
   de la 1.31: el snapshot de planillas quedaba viejo, el delta de la vuelta
   siguiente creía que **todas** las planillas eran nuevas y el **segundo guardado
   duplicaba registros**. Cada tabla re-hidrata la suya.

---

## 8. Checklist de salida del constructor (después de esta guía, antes de `02`)

Verificable, sin interpretación. Todo ítem se prueba en Studio:

- [ ] La app se llama `CH-App-Reception v2`, tiene el ícono
      `docs/marca/icono-reception-512.png` con fondo `#C8102E` y está agregada a la
      solución **CashHubApps**.
- [ ] **El MEDIO está cargado aparte del ícono** (§1.2): en el panel **Medios**
      figura `IconoReception192` (subido desde `docs/marca/icono-reception-192.png`).
      Prueba: insertá una Imagen clásica, escribí `IconoReception192` en su `Image`
      y **se ve el arte**. Borrala después. **Este ítem bloquea el header de `02` y `03`.**
- [ ] **Pantalla**: Horizontal · 1366 × 768 · Escalar para ajustar **ACTIVADO** ·
      **Límite de filas de datos = 2000**.
- [ ] **Características próximas**: nada experimental activado. **Cero conectores**
      en el panel de Datos.
- [ ] Los **6 datasources** de §2.1 están agregados, y NINGÚN otro (ni Clients, ni
      Users, ni samples).
- [ ] En `CH-Tbl-Bags`, IntelliSense autocompleta **`ReceptionBatchId`** y
      **`ReceiptIdMirror`**. Si no: quitar y re-agregar la tabla desde el panel Datos
      (§2.2). **Este ítem bloquea `03`.**
- [ ] En `CH-Tbl-ClientBranches`, IntelliSense autocompleta **`sanic_ClientNameMirror`**
      (espejo **B0-9**, §2.2). Si no: quitar y re-agregar la tabla. **Este ítem
      bloquea el combo de empresa de `02` §5.4** — sin el espejo, el combo no busca
      por nombre de empresa.
- [ ] **Prueba de la igualdad PK contra PK (D-1)**: pegá
      `Filter('CH-Tbl-Receptions', ReceptionBatch = First('CH-Tbl-ReceptionBatches'))`
      en el `Items` de una galería de prueba: **tiene que salir en ROJO**. Cambiala a
      `ReceptionBatch.'CH-Tbl-ReceptionBatch' = First('CH-Tbl-ReceptionBatches').'CH-Tbl-ReceptionBatch'`
      y el rojo se va. Borrá la galería. Es la forma que usan `02` y `03` en **todas**
      sus consultas por lote/planilla.
- [ ] **Prueba de la vista como 2º argumento (§2.5)**: pegá
      `Filter('CH-Tbl-TransportCompanies (Views)'.'CH-Tbl-TransportCompany Lookup View', true)`
      ⇒ **rojo**. `Filter('CH-Tbl-TransportCompanies', 'CH-Tbl-TransportCompanies (Views)'.'CH-Tbl-TransportCompany Lookup View')`
      ⇒ compila. Borralo después.
- [ ] `App.Formulas` pegado completo: escribí `ThemeAccent` en el `Fill` de una
      pantalla y **el rojo se ve en tiempo de diseño, sin correr OnStart**.
- [ ] `fxProblemas` **compila** (cero rojo en el editor). Si Studio rechaza la
      referencia a colecciones, aplicaste el fallback documentado en §3.1 y lo
      anotaste en el ledger de `00`.
- [ ] `App.OnStart` pegado; **botón derecho sobre App → Ejecutar OnStart** deja las
      6 colecciones creadas y vacías (verificable en Ver → Colecciones).
- [ ] `App.OnStart` **no contiene ningún `ForAll`** y por lo tanto ningún `Set`
      dentro de uno.
- [ ] `App.OnError`, `App.BackEnabled = false`, `App.StartScreen = scrCaptura`,
      `ConfirmExit` y `ConfirmExitMessage` seteados.
- [ ] Existen las **dos pantallas vacías** `scrCaptura` y `scrLotes`, con
      `Fill = ThemeBg`, y la pantalla en blanco por defecto de Studio fue borrada.
- [ ] **App Checker en CERO errores** (los warnings de accesibilidad se resuelven en
      `02`/`03` con el `AccessibleLabel` de cada control).
- [ ] **Los tres modernos del catálogo (§0.3) existen en tu Studio**: Insertar →
      Moderno tiene **Lista de pestañas**, **Selector de fecha** y **Spinner**. Si
      alguno falta, avisá antes de arrancar `03` — no lo sustituyas por un control
      de código.
- [ ] Prueba de humo de la paleta: un Botón clásico con `Fill = ThemeAccent`,
      `Radius 8` y `Text "prueba"` se ve rojo BAC con esquinas redondeadas y fuente
      Open Sans. Borralo después.
