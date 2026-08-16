# Guía 01 — CH-App-Hub · `scrHub` (app completa en un solo documento)

CH-App-Hub es el **launcher corporativo** de CashHub: lee el catálogo de apps,
filtra por membresía de grupos de Azure AD y lanza la app destino en la misma
pestaña. Es una app de **UNA sola pantalla**, así que esta guía autocontiene lo
que en `tracker-rebuild/` vive repartido entre `00-analisis`, `01-app` y
`0N-pantalla`: contrato de formato, configuración de la app, `App.OnStart`
completo, el árbol de `scrHub`, la tabla de propiedades de cada control,
seguridad, performance, ledger de divergencias y checklist de salida.

**Lo que se reemplaza**: la pantalla `Screen1` actual (SVG estático animado con
contadores inventados + galería `Gallery1` con layout de examinar). El
`App.OnStart` actual **se conserva** — es correcto y está optimizado; solo se le
agregan los tokens de tema y dos variables nuevas.

---

## 0. Contrato de formato de esta guía

Estas reglas son la spec contra la que se revisa el documento. Si encontrás una
violación es un bug de la guía, no una licencia creativa:

1. **Árbol completo**: la sección 3 lista TODOS los controles de `scrHub` con su
   nombre final. Nada se construye "a ojo".
2. **Cobertura total 1:1**: todo control del árbol tiene su descripción abajo —
   tabla propia o fila en tabla compartida. No hay controles en el cuerpo que no
   estén en el árbol, ni en el árbol que no estén en el cuerpo.
3. **Tipo explícito**: cada control declara QUÉ componente de Power Apps es,
   usando exactamente estos nombres (los del menú **Insertar**):

   | Tipo en la guía | En Studio (Insertar) |
   |---|---|
   | Etiqueta clásica | Clásico → **Etiqueta** (`Label`) |
   | Botón clásico | Clásico → **Botón** (`Button`) |
   | Rectángulo clásico | Formas → **Rectángulo** (`Rectangle`) |
   | Imagen clásica | Medios → **Imagen** (`Image`) |
   | Icono clásico | Iconos → el icono nombrado (`Classic/Icon`) |
   | Texto HTML clásico | Clásico → **Texto HTML** (`HTML text`) |
   | Galería vertical en blanco | Diseño → **Galería vertical en blanco** (`Blank vertical gallery`) |

   > 🧨 **El Rectángulo clásico NO tiene `Radius`.** Verificado contra
   > `References\Themes.json` del `.msapp` publicado: `defaultRectangleStyle` no
   > expone `RadiusTopLeft/TopRight/BottomLeft/BottomRight` (ni
   > `HoverBorderColor`). La **Etiqueta** y el **Botón** clásicos SÍ los tienen.
   > Por eso en esta guía el rectángulo se usa **solo** para barras y líneas de
   > esquina viva, y **toda superficie con esquinas redondeadas** (tiles,
   > píldoras, cards) es una **Etiqueta clásica con `Text: ""`** o un **Botón
   > clásico** cuando además necesita estados de hover. Si una tabla te pide
   > `Radius` sobre un rectángulo, es un bug de la guía.

4. **Propiedades**: cada tabla lista SOLO las propiedades que cambian del
   default. Los defaults tipográficos y de caja están en §2.5, y **mandan**: si
   una propiedad no aparece en la tabla del control, vale lo que dice §2.5.
5. **Nombres únicos a nivel APP**: en canvas el nombre de control es único en
   toda la aplicación, no por pantalla. Como la app tiene una sola pantalla igual
   se usa el sufijo/infijo `Hub` en todo control (`shpHubHeader`, `galHubApps`,
   `lblHubTarjetaTitulo`…) para que un futuro `scrDetalle` no colisione y Studio
   no renombre en silencio rompiendo las fórmulas de este documento.
6. **Orden del árbol = orden de INSERCIÓN, que es el z-order** — no el orden en
   que se configuran las propiedades. En canvas, lo insertado DESPUÉS queda
   ENCIMA. Donde el z-order importa, la guía lo dice explícito.
7. **Coordenadas dentro de una galería**: el `X`/`Y` de un control del template
   es **relativo al template**, que arranca en `0` — no a la pantalla. Se enuncia
   una sola vez, acá, y aplica a `galHubApps` y `galHubSkeleton`.
8. **Cero números mágicos duplicados**: si dos controles comparten un borde,
   uno referencia al otro (`lblHubFecha.Y = htmHubSaludo.Y + htmHubSaludo.Height + 2`),
   nunca dos literales que se puedan contradecir al editar.
9. **Construcción en DOS PASES** (regla de orden, no de estilo — leer §4 antes de
   insertar el primer control). Las fórmulas de esta pantalla forman una red de
   referencias cruzadas que **ningún orden lineal de construcción puede
   satisfacer**: `galHubApps.Height` referencia a `shpHubFooter` (que se inserta
   después), `lblHubFooterIzq.X` referencia a `galHubApps` (que se insertó
   antes), e `icoHubBanner` —control 13— referencia a `galHubApps`, que es el 26.
   No es un defecto de la guía: es lo que pasa cuando el layout se ancla entre
   controles en vez de a números mágicos (regla 8). Se resuelve así:

   | Pase | Qué hacés | Qué NO hacés |
   |---|---|---|
   | **1 — Esqueleto** | Insertás y **renombrás** los 42 controles en el orden exacto del árbol de §3. Ese orden ES el z-order. | No tocás ninguna propiedad. No te preocupes por posición ni color: van a estar todos apilados y feos. |
   | **2 — Propiedades** | Recorrés §4.1 → §4.8 llenando las tablas. | No insertás ni reordenás controles. |

   Al empezar el pase 2 los 42 nombres ya existen, así que **ninguna fórmula
   queda huérfana** y Studio no te marca referencias rotas mientras construís.
   Si te salteás esto y vas control por control completo, vas a escribir
   fórmulas contra controles inexistentes y Studio las va a rechazar.

---

## 1. Propósito y contrato de entrada

### 1.1 Qué hace la pantalla

| Hace | No hace |
|---|---|
| Muestra las apps del catálogo a las que el usuario tiene acceso, ordenadas por `'Order'` | No navega a otras pantallas (no hay otras) |
| Lanza la app destino con `Launch(AppURL, {}, LaunchTarget.Replace)` | No escribe NADA en Dataverse |
| Informa cuántas apps ve sobre el total habilitado | No cuenta lotes, tulas ni transacciones (eso murió con el SVG) |
| Avisa cuando la validación de accesos falló (fail-open) | No hace ninguna llamada de red propia (§7) |

### 1.2 Contrato de entrada — lo que `App.OnStart` deja seteado

Todo lo que consume `scrHub` lo produce `App.OnStart` (§2.6). Ningún control
lee un datasource directamente.

| Nombre | Tipo | Lo setea | Lo consume |
|---|---|---|---|
| `ThemeAccent`, `ThemeAccentDark`, `ThemeAccentSoft`, `ThemeBg`, `ThemeSurface`, `ThemeSurface2`, `ThemeInk`, `ThemeInk2`, `ThemeInk3`, `ThemeLine`, `ThemeLineSoft`, `ThemeOk`, `ThemeOkSoft`, `ThemeInfo`, `ThemeInfoSoft` | Color | `OnStart` (bloque 1, antes de tocar red) | todos los controles |
| `gvarEntorno` | Texto | `OnStart` (bloque 1) | `pilHubEntorno` |
| `colCatalog` | Colección | `OnStart` (bloque 2) | denominador de la píldora "X de **Y**" |
| `colAccess`, `colGroupIds`, `colMyGroupIds` | Colecciones | `OnStart` (bloques 3–4) | nadie en pantalla (insumo del filtro) |
| `gvarAccessError` | Booleano | `OnStart` (bloques 3–4) | banner fail-open, visibilidad de la píldora |
| `colAppData` | Colección | `OnStart` (bloque 5) | `galHubApps.Items`, numerador de la píldora, estado vacío |
| `gvarLoaded` | Booleano | `OnStart` (bloques 1 y 6) | skeleton vs. contenido real |

> ⚠️ **Las variables de `Set()` no existen en el lienzo de Studio hasta que
> corrés `OnStart`.** Mientras diseñás vas a ver todo negro/transparente. Botón
> derecho sobre **App** en el árbol → **Ejecutar OnStart** cada vez que abrís
> Studio, y listo. (Alternativa: mover los tokens a `App.Formulas`, que se evalúa
> perezoso y SÍ existe en diseño; se descartó para mantener un solo lugar de
> arranque en una app que ya tiene un `OnStart` grande — decisión declarada en el
> ledger §8.)

---

## 2. La app: configuración, datos, OnStart y OnError

### 2.1 Configuración responsive (Configuración → Pantalla)

Esta app se rediseña para **monitores y tablets en horizontal Y pantallas en
vertical**. Teléfonos quedan **fuera de alcance** (implementación futura: abajo
de ~600px el layout de tarjetas de 300px no cierra, y el header con chip de
usuario tendría que colapsar a un menú).

| Opción | Valor | Por qué |
|---|---|---|
| Orientación | **Horizontal** | Es el diseño base (1366 × 768). No bloquea nada. |
| Tamaño | **1366 × 768** | Solo define el lienzo de diseño. |
| **Escala para ajustar** | **Desactivado** | Con escala activada la app se estira/encoge como una imagen: 1 unidad ≠ 1 píxel y el layout no se **reacomoda**, se deforma. Apagado, `Parent.Width` reporta el ancho REAL y las fórmulas responden. |
| **Bloquear relación de aspecto** | **Desactivado** | Requiere escala activada; con ella apagada no aplica. Dejalo apagado explícitamente. |
| **Bloquear orientación** | **Desactivado** | Es lo que habilita el vertical. |

> ✅ El `.msapp` actual ya trae `ScaleToFit=false`, `MaintainAspectRatio=false` y
> `LockOrientation=false`. **Verificalo, no lo asumas** — si alguien lo tocó, todo
> el §4 se rompe en silencio.

Propiedades de **App** (panel de propiedades avanzadas):

| Propiedad | Valor | Por qué |
|---|---|---|
| `MinScreenWidth` | `768` | Piso de tablet en vertical. Debajo de eso el navegador muestra scroll horizontal en vez de destruir el layout — es la exclusión de teléfonos hecha explícita, no un accidente. |
| `MinScreenHeight` | `640` | Piso para que header + saludo + una fila de tarjetas + footer entren sin canibalizarse. |
| `SizeBreakpoints` | `[768, 1024, 1440]` | Solo por coherencia documental. **Este diseño no ramifica con `Screen.Size`**: los breakpoints son escalones gruesos a nivel app; nosotros calculamos contra `Parent.Width` (continuo), que da mejor resultado en el rango tablet↔monitor. |

**Regla de oro del responsive de esta pantalla** (vale para todo el §4):

> ❌ **NUNCA** guardes una medida de layout en una variable
> (`Set(gvarAncho, Parent.Width)`). Las variables se congelan en el arranque y no
> se recalculan al rotar o redimensionar: el layout quedaría clavado al tamaño
> que tenía la ventana cuando abrió la app. Las medidas van **inline** en cada
> propiedad, referenciando `Parent.Width` / `Parent.Height` o el control vecino.

### 2.2 Orígenes de datos

Agregá EXACTAMENTE estos tres (Datos → Agregar datos). No sumes tablas "por si
acaso": cada datasource pesa en el arranque de un launcher que tiene que abrir en
menos de un segundo.

| # | Origen | Tipo | Para qué |
|---|---|---|---|
| 1 | `CH-Tbl-AppCatalogs` | Dataverse | Catálogo de apps (`sanic_chtblappcatalog`) |
| 2 | `CH-Tbl-AppCatalogAccesses` | Dataverse | Filas (app, grupo AAD) — el gating relacional |
| 3 | `Office365Groups` | Conector | Solo por `Office365Groups.HttpRequest` → Graph `checkMemberGroups` |

> Los dos nombres de tabla están confirmados contra el `App.pa.yaml` de la app
> publicada. **Si IntelliSense te ofrece otro nombre, el de Studio manda**:
> usalo, y avisá para corregir este documento.

### 2.3 Columnas reales que usa la guía (esquema verificado)

De **`CH-Tbl-AppCatalogs`** (display names de `Entity.xml`):

| Display name | Tipo | Uso en la guía |
|---|---|---|
| `'Display Name'` | Texto | título de la tarjeta (+ respaldo del respaldo: §4.6.1) |
| `Description` | Texto | bajada de la tarjeta |
| `AppURL` | Texto | `Launch()` — validada server-side (§6) |
| `Enabled` | Sí/No | filtro del catálogo en `OnStart` |
| `'Order'` | Entero (requerido) | orden de las tarjetas |
| `Icon` | **Imagen** | glifo de la tarjeta (hoy vacío en el seed → §4.6) |
| `'CH-Tbl-AppCatalog'` | GUID (PK) | join en memoria contra los accesos |
| `'App Key'` | Texto (índice único) | glifo de respaldo de dos letras cuando `Icon` está vacío (§4.6.1) |
| `Name`, `Tags` | Texto | **no se usan en pantalla** |
| ~~`'AAD Group Id'`~~ | Texto | 🚫 **DEPRECADA Y BLOQUEADA POR GUARD — la guía no la referencia jamás** |

De **`CH-Tbl-AppCatalogAccesses`**:

| Display name | Tipo | Uso |
|---|---|---|
| `'AAD Group Id'` | Texto | el GUID de grupo **vivo** (uno por fila) |
| `'App Catalog'` | Lookup | app a la que aplica la fila |
| `Name` | Texto | no se usa |

> ⚠️ **Las dos tablas tienen una columna con el mismo display name
> `'AAD Group Id'`.** La regla es tajante: `'AAD Group Id'` se lee **solo** desde
> `CH-Tbl-AppCatalogAccesses`. La del catálogo está deprecada y cualquier
> escritura rebota con 400 desde `GuardAppCatalogPlugin`. Si ves
> `'CH-Tbl-AppCatalogs'` y `'AAD Group Id'` en la misma fórmula, es un bug.

### 2.4 Convenciones de nombres

| Prefijo | Tipo de control | Ejemplo |
|---|---|---|
| `scr` | Pantalla | `scrHub` |
| `shp` | **Superficie**: barra o línea (Rectángulo clásico) **y también** tile / card / píldora contenedora construidos con Botón clásico en `DisplayMode.View`, o con Etiqueta clásica y `Text: ""` — porque el rectángulo no tiene `Radius` (§0.3). **La variante Etiqueta NO va en `DisplayMode.View`**: le mataría el `OnSelect: Select(Parent)` (el clic sobre el tile del glifo dejaría de abrir la app, prueba de §9.2). La tabla de cada control manda (§0.4) | `shpHubHeader` (rectángulo), `shpHubMarca` (botón), `shpHubVacio` (etiqueta) |
| `lbl` | Etiqueta clásica | `lblHubFecha` |
| `btn` | Botón clásico con acción | `btnHubTarjeta` |
| `pil` | Píldora (botón clásico de solo lectura) | `pilHubEntorno` |
| `ico` | Icono clásico | `icoHubAccesos` |
| `img` | Imagen clásica | `imgHubGlifo` |
| `htm` | Texto HTML clásico | `htmHubSaludo` |
| `gal` | Galería | `galHubApps` |

Variables globales `gvarXxx`, colecciones `colXxx`. **Excepción declarada**: los
tokens de paleta se llaman `ThemeXxx` sin prefijo `gvar` — son tokens de diseño,
no estado de la app, y así calcan 1:1 los nombres de `tracker-rebuild/01-app.md`
(donde viven como fórmulas con nombre). Cero controles con nombre autogenerado:
renombrás en el momento de insertar, antes de tocar propiedades.

### 2.5 Defaults tipográficos y de caja (MANDAN sobre las tablas de §4)

> ⚠️ **El tema de la app NO es Segoe UI.** `References\Themes.json` del `.msapp`
> declara `palette.TextBodyFontFace` = **Open Sans**, y ese es el font que van a
> heredar todos los controles nuevos. No es un detalle cosmético: el glifo de
> marca **`₵` (U+20B5) no existe en Open Sans** y sale como caja vacía (tofu).
> Por eso el `Font` se fija a mano, y **también en los Botones**, no solo en las
> etiquetas.

Toda **Etiqueta clásica** Y todo **Botón clásico** arrancan así, salvo que su
tabla de §4 diga otra cosa:

| Propiedad | Valor | Por qué se pisa el default |
|---|---|---|
| `Font` | `Font.'Segoe UI'` | el tema trae **Open Sans** → tofu en `₵` |
| `Size` | `9` | — |
| `Color` | `ThemeInk` (etiquetas) | — |
| `PaddingLeft` / `Right` / `Top` / `Bottom` | `0` | la etiqueta clásica trae **5** en las cuatro: sin esto, todo tile o píldora hecho con etiqueta desplaza su contenido 5px |
| `Wrap` | `false` | el default de plataforma es **`true`** (con `Overflow` oculto): una etiqueta de una línea con `Wrap true` y alto justo **envuelve y recorta** el texto en vez de dejarlo pasar de largo |
| `VerticalAlign` | `VerticalAlign.Middle` | — |

**Defaults propios del Botón clásico que hay que pisar SIEMPRE** (aplican a
`shpHubMarca`, `shpHubAvatar`, `pilHubEntorno` y `btnHubTarjeta`):

| Propiedad | Default del tema | Valor de esta guía |
|---|---|---|
| `BorderThickness` | `2` | `0` — salvo `btnHubTarjeta`, que lleva `1` |
| `RadiusTopLeft/TopRight/BottomLeft/BottomRight` | `10` | el que diga la tabla del control |
| `Align` / `VerticalAlign` | `Center` / `Middle` | se conservan (es lo que queremos en tiles y píldoras) |
| `HoverFill` / `PressedFill` | derivados del `Fill` | **iguales al `Fill`** en todo botón de solo lectura (§4.1) |

**Regla de lectura de las tablas de §4** (rule 0.4 en la práctica): si una
propiedad no figura en la tabla de un control, vale lo de acá. Por eso vas a ver
tablas sin `Size` (es `9`), sin `Wrap` (es `false`) y sin `BorderThickness` en
rectángulos y etiquetas (su default ya es `0`). Donde la guía **sí** lo escribe,
es porque cambia.

**Conversión de tamaños del mockup**: como *Escala para ajustar* está apagada,
1 unidad de canvas = 1 píxel CSS del mockup → los `X/Y/Width/Height` se copian
tal cual. Pero `Size` de una etiqueta está en **puntos**, no en píxeles, y la
regla de esta guía es **exacta, no redondeada**:

> **`Size = Round(px × 0.75, 2)`**

Se eligió el valor exacto sobre "el entero más cercano ±0.25pt" por una razón
concreta: los redondeos al entero acumulan hasta 0.75px de deriva por control y
rompen alineaciones que dependen del alto de línea (título y descripción de la
tarjeta, las dos líneas del chip de usuario). Power Apps acepta decimales en
`Size` sin problema. **Todas las tablas de §4 usan estos valores**:

| px del mockup | `Size` | Dónde |
|---|---|---|
| 19 | `14.25` | `shpHubMarca` (glifo `₵` del `.brandTile`) |
| 15 | `11.25` | `lblHubTarjetaTitulo` (`.card h3`), `lblHubGlifoInicial`, `lblHubVacioTitulo` |
| 14.5 | `10.88` | `lblHubMarca` |
| 13.5 | `10.13` | `lblHubFecha` (`.greet p`), `lblHubVacioSub` |
| 12 | `9` | `shpHubAvatar`, `lblHubUserNombre`, `lblHubAccesos`, `lblHubTarjetaDesc`, `lblHubTarjetaAbrir`, `lblHubTarjetaFlecha`, `lblHubBanner` |
| 11 | `8.25` | `lblHubMarcaSub`, `pilHubEntorno`, `lblHubFooterIzq`, `lblHubFooterDer` |
| 10.5 | `7.88` | `lblHubUserMail` |

`htmHubSaludo` queda **fuera de esta tabla**: es un control HTML, sus tamaños se
escriben en px CSS reales sin ninguna conversión (§4.3.1).

Dos valores no salen del mockup y se declaran como diseñados acá:
`lblHubGlifoInicial` (`11.25`) ocupa el lugar de un SVG de 23px, y `lblHubBanner`
(`9`) pertenece a un bloque que el mockup no dibuja (§4.2).

### 2.6 `App.OnStart` COMPLETO (pegar tal cual)

Bloque 1 (tokens y flags) es **nuevo**. Bloques 2–7 son el `OnStart` que hoy
está publicado y funcionando, verbatim.

```powerfx
// ===== 1. Tokens de tema y constantes (primero: cero red, nunca fallan) =====
Set(ThemeAccent,     ColorValue("#C8102E"));
Set(ThemeAccentDark, ColorValue("#A40D26"));
Set(ThemeAccentSoft, ColorValue("#FBEBED"));
Set(ThemeBg,         ColorValue("#F6F5F4"));
Set(ThemeSurface,    ColorValue("#FFFFFF"));
Set(ThemeSurface2,   ColorValue("#FAF8F7"));
Set(ThemeInk,        ColorValue("#201C1D"));
Set(ThemeInk2,       ColorValue("#66595C"));
Set(ThemeInk3,       ColorValue("#948A8C"));
Set(ThemeLine,       ColorValue("#E2DCDD"));
Set(ThemeLineSoft,   ColorValue("#EDE8E9"));
Set(ThemeOk,         ColorValue("#0E7A3C"));
Set(ThemeOkSoft,     ColorValue("#E2F2E8"));
Set(ThemeInfo,       ColorValue("#23557D"));
Set(ThemeInfoSoft,   ColorValue("#E4EEF6"));
Set(gvarEntorno, "Producción");
Set(gvarLoaded, false);

// ===== 2. Catálogo (1 lectura) =====
ClearCollect(
    colCatalog,
    Filter('CH-Tbl-AppCatalogs', Enabled)
);

// ===== 3. Accesos relacionales (1 lectura). Una fila = (app, grupo AAD).
//         Sin filas para una app, la app es pública. =====
ClearCollect(
    colAccess,
    ForAll(
        'CH-Tbl-AppCatalogAccesses' As row,
        {
            AppId: row.'App Catalog'.'CH-Tbl-AppCatalog',
            GroupId: Substitute(Trim(row.'AAD Group Id'), " ", "")
        }
    )
);
Set(gvarAccessError, false);
Clear(colMyGroupIds);
ClearCollect(colGroupIds, Distinct(colAccess, GroupId));

// ===== 4. Membresía transitiva. checkMemberGroups acepta hasta 20 grupos
//         por llamada: se resuelve en lotes de 20. =====
ForAll(
    Sequence(RoundUp(CountRows(colGroupIds) / 20, 0)) As batch,
    IfError(
        Collect(
            colMyGroupIds,
            ForAll(
                Table(
                    Office365Groups.HttpRequest(
                        "https://graph.microsoft.com/v1.0/me/checkMemberGroups",
                        "POST",
                        "{""groupIds"": [" &
                            Concat(
                                LastN(
                                    FirstN(colGroupIds, batch.Value * 20),
                                    Min(20, CountRows(colGroupIds) - (batch.Value - 1) * 20)
                                ),
                                """" & Value & """",
                                ","
                            ) &
                        "]}"
                    ).value
                ),
                {GroupId: Text(Value)}
            )
        ),
        // Si Graph falla no bloqueamos el hub: se muestra todo con aviso;
        // la autorización real la imponen las apps destino
        Set(gvarAccessError, true)
    )
);

// ===== 5. Join en memoria: catálogo × accesos × mis grupos =====
ClearCollect(
    colAppData,
    Filter(
        colCatalog As app,
        IsEmpty(Filter(colAccess, AppId = app.'CH-Tbl-AppCatalog')) ||
            gvarAccessError ||
            CountRows(
                Filter(
                    colAccess,
                    AppId = app.'CH-Tbl-AppCatalog' && GroupId in colMyGroupIds.GroupId
                )
            ) > 0
    )
);

// ===== 6. Listo: apaga el skeleton =====
Set(gvarLoaded, true);

// ===== 7. Si solo tenés una app, no te hacemos elegir entre una opción =====
If(
    CountRows(colAppData) = 1 && !gvarAccessError,
    Launch(First(colAppData).AppURL, {}, LaunchTarget.Replace)
)
```

**Los tres cambios respecto de lo publicado**, y por qué:

1. **Bloque 1 completo**: tokens de paleta + `gvarEntorno`. Va PRIMERO, antes de
   cualquier llamada a red, para que aunque Dataverse o Graph se caigan la
   pantalla tenga colores y no se renderice en negro sobre transparente.
2. **`Set(gvarLoaded, false)` al inicio**: hoy `gvarLoaded` solo se pone en
   `true`. En el primer arranque funciona porque una variable inexistente es
   *falsy*, pero si alguna vez se re-ejecuta `OnStart` el skeleton no volvería a
   aparecer y el usuario vería la lista vieja durante la revalidación. Un
   `Set(…, false)` cuesta cero y cierra el agujero.
3. Nada más. **El resto no se toca**: es la versión que arregló el bug del filtro
   muerto, y es óptima (§7).

> 🧨 **Gotcha que hay que respetar si algún día se edita este `OnStart`**:
> `ClearCollect(col, If(cond, Filter(...)))` **sin rama else** revienta con
> `ErrorKind.Internal` cuando `cond` es falsa (el `If` devuelve `Blank()` y
> `ClearCollect` no acepta un blanco como origen). El `If` va **por fuera**:
> `If(cond, ClearCollect(col, ...), Clear(col))`. En el bloque 7 el `If` sin else
> es legal porque envuelve un `Launch()`, no un `ClearCollect`.

### 2.7 `App.OnError` (queda igual que hoy)

```powerfx
Trace(
    "CH-App-Hub: error no controlado",
    TraceSeverity.Error,
    {
        Message: FirstError.Message,
        Kind: Value(FirstError.Kind),
        Source: FirstError.Source
    }
);
Notify(
    "Ocurrió un error inesperado en el hub. " & FirstError.Message,
    NotificationType.Error
)
```

### 2.8 Prerrequisito de datos: sembrar la columna `Icon` (OBLIGATORIO)

**Los glifos por app del mockup viajan por DATO, no por código.** La columna
`Icon` (`sanic_icon`, tipo **imagen**) de `CH-Tbl-AppCatalogs` es el único lugar
donde vive el ícono de cada tarjeta. Esto no es una preferencia estética: es lo
que permite que la app número 8 sea **una fila en Dataverse** y no una release de
la canvas app. La guía no tiene ni un `Switch` por `'App Key'`, y no debe tenerlo.

#### 2.8.1 Qué hay que producir

Los 7 motivos del mockup, exportados de SVG a **PNG de 88 × 88 px**, fondo
**transparente**, trazo en **`#C8102E`** (el `.glyph svg` del mockup usa
`stroke:currentColor` con `color:var(--accent)`), extremos y uniones redondeados.

**La geometría del asset importa y es fácil de errar**: el PNG se muestra a
**44 × 44** (`imgHubGlifo`, §4.5), cubriendo el tile completo — o sea que 88 es
exactamente **2×** para HiDPI. Dentro de esos 88, el motivo va **centrado
ocupando ~46 × 46** (el `svg` de 23px del mockup, duplicado) con **grosor de
trazo 4**, que al mostrarse a la mitad se ve como los 2px del mockup. Si exportás
el trazo en 2 sobre el lienzo de 88, en pantalla queda en 1px y el ícono
desaparece:

| `sanic_appkey` | App | Motivo del mockup (líneas del HTML) |
|---|---|---|
| `reception` | Recepción | camión de reparto (`m 135`) |
| `supervisor` | Supervisor | balanza de justicia (`m 142`) |
| `recounting` | Recuento | dos monedas superpuestas (`m 149`) |
| `digitization` | Digitación | teclado (`m 156`) |
| `emailval` | Ingesta de correos | sobre (`m 163`) |
| `tracker` | Trazabilidad | eslabones de cadena (`m 170`) |
| `clientadmin` | Admin. de clientes | persona con "+" (`m 177`) |

> Peso objetivo: **< 10 KB por PNG**. Son íconos de 44px en pantalla; subir un
> PNG de 2MB por app son 7 descargas gordas en el arranque (§7.4).
>
> ✅ **Los 88 × 88 entran holgados en lo que Dataverse sirve**: la miniatura que
> devuelve una columna de imagen —y que es lo que consume `ThisItem.Icon` en
> canvas— está topeada en **144 × 144**. Si subieras un asset más grande, la
> plataforma te devolvería igual la miniatura reescalada.

#### 2.8.2 Dónde se agrega

La clave `sanic_icon` se agrega a las 7 filas de
**`data/seed/30-appcatalogs.json`**, junto a `sanic_appkey` / `sanic_displayname`
/ etc. (hoy ninguna fila la tiene — por eso la pantalla muestra el respaldo).

#### 2.8.3 ⚠️ Tarea de backend PENDIENTE (fuera del alcance de esta guía)

**El seeder actual no soporta columnas de imagen**, y además **el atributo está
declarado en el formato equivocado**. Antes de poder sembrar hay que:

0. 🚨 **Cambiar el formato de la columna de imagen de JPEG a PNG.**
   `Entity.xml` declara `<Format>jpeg</Format>` en `sanic_Icon`, y **JPEG no
   tiene canal alfa**: si subís los PNG transparentes de §2.8.1 tal cual, la
   plataforma los recodifica y el fondo transparente se convierte en **negro
   sólido** — siete cuadrados negros dentro de tiles rosados. Es el error más
   caro de esta lista porque no falla, sale feo en producción. Se cambia en el
   diseñador de la tabla (propiedad **Formato de imagen**) o en la metadata del
   atributo, y viaja en la solución Core.
1. Extender el seeder para reconocer columnas de imagen y enviar el contenido en
   base64 (leyendo el PNG desde una ruta relativa declarada en el JSON, p. ej.
   `"sanic_icon": "@file:assets/icons/reception.png"`, siguiendo el mismo patrón
   de resolución en vivo que ya usa `"@canvasapp:…"` para `sanic_appurl`).
2. Sembrar / actualizar las 7 filas.

**Esto se AGENDA, no se ejecuta en esta tarea.** No toques el seeder ni el JSON
mientras construís la pantalla.

#### 2.8.4 Fallback mientras la columna esté vacía

La pantalla **ya está diseñada para funcionar sin íconos**: `imgHubGlifo` se
oculta y `lblHubGlifoInicial` muestra un identificador de dos caracteres derivado
por fórmula de `'App Key'` (§4.6). Cuando alguien cargue las imágenes —por el
seeder o a mano por el formulario del catálogo— **las tarjetas las toman sin
republicar la app**.

Es un respaldo digno, no el diseño aprobado: mientras la columna esté vacía, la
pantalla se ve claramente peor que el mockup (§8.11).

---

## 3. Árbol de controles de `scrHub`

Renombrá `Screen1` → **`scrHub`**, borrá `Gallery1`, `Image2`, `AccessBanner1` y
`EmptyState1`, y construí este árbol. **El orden es el de inserción** (lo de
abajo queda encima de lo de arriba):

```
scrHub                                (Fill: ThemeBg)
├── shpHubHeader                      barra blanca del header
├── shpHubHeaderLinea                 línea inferior del header
├── shpHubMarca                       tile rojo "₵"
├── lblHubMarca                       "CashHub"
├── lblHubMarcaSub                    "Portal de operaciones"
├── pilHubEntorno                     píldora "Producción"
├── shpHubUserChip                    píldora contenedora del usuario
├── shpHubAvatar                      círculo con iniciales
├── lblHubUserNombre                  User().FullName
├── lblHubUserMail                    User().Email
├── shpHubBanner                      fondo del aviso fail-open
├── shpHubBannerBarra                 barra de acento del aviso
├── icoHubBanner                      icono de advertencia
├── lblHubBanner                      texto del aviso
├── htmHubSaludo                      "Buenas tardes, Randy" (bicolor)
├── lblHubFecha                       fecha larga + bajada / "Verificando tus accesos…"
├── shpHubPildoraAccesos              fondo de la píldora "X de Y"
├── icoHubAccesos                     check de la píldora
├── lblHubAccesos                     texto "Tenés acceso a X de Y…"
├── galHubSkeleton                    galería de carga (Visible: !gvarLoaded)
│   ├── shpHubSkTarjeta               card gris de la tarjeta fantasma
│   ├── shpHubSkGlifo                 bloque del glifo
│   ├── shpHubSkBarra1                barra del título
│   ├── shpHubSkBarra2                barra de texto 1
│   └── shpHubSkBarra3                barra de texto 2
├── galHubApps                        galería real (Visible: gvarLoaded)
│   ├── btnHubTarjeta                 LA TARJETA (superficie + clic) — al FONDO
│   ├── shpHubGlifo                   tile rosado del glifo
│   ├── imgHubGlifo                   imagen de la columna Icon (si existe)
│   ├── lblHubGlifoInicial            respaldo de 2 letras (si Icon está vacío)
│   ├── lblHubTarjetaTitulo           'Display Name'
│   ├── lblHubTarjetaDesc             Description
│   ├── shpHubTarjetaSep              separador interno de la tarjeta
│   ├── lblHubTarjetaAbrir            "Abrir aplicación"
│   └── lblHubTarjetaFlecha           "→"
├── shpHubVacio                       card del estado vacío
├── lblHubVacioTitulo                 "No tenés aplicaciones asignadas"
├── lblHubVacioSub                    "Contactá a tu supervisor…"
├── shpHubFooter                      barra inferior
├── shpHubFooterLinea                 línea superior del footer
├── lblHubFooterIzq                   "CashHub · CH-App-Hub v2.0"
└── lblHubFooterDer                   nota de administración de accesos
```

**42 controles.** La pantalla:

| Propiedad | Valor |
|---|---|
| `Fill` | `ThemeBg` |
| `OnVisible` | *(vacío — no hay nada que resetear: una sola pantalla, sin pestañas ni selección persistente)* |

---

## 4. Construcción por bloque

> 🛑 **Antes de tocar la primera propiedad: hacé el PASE 1 completo** (regla
> §0.9). Insertá y renombrá los 42 controles en el orden del árbol de §3, sin
> configurar nada. Las secciones que siguen están ordenadas por BLOQUE VISUAL,
> no por orden de construcción, y se referencian entre sí hacia adelante y hacia
> atrás: §4.2 (`icoHubBanner`, control 13) usa `galHubApps` (control 26), y §4.5
> (`galHubApps.Height`) usa `shpHubFooter` (control 39, en §4.8). Con el pase 1
> hecho, todas esas referencias resuelven al escribirlas.

### 4.0 El ancla de layout (leer antes que nada)

`galHubApps` es el **ancla horizontal** de toda la pantalla: define el margen
lateral y el ancho de contenido, y el resto de los controles lo referencian. Así
hay UN solo lugar donde vive el margen.

| Propiedad de `galHubApps` | Fórmula | Qué significa |
|---|---|---|
| `X` | `If(Parent.Width < 900, 20, 46)` | margen lateral: 46px en monitor/tablet horizontal (el del mockup), 20px en vertical |
| `Width` | `Parent.Width - Self.X * 2 + 18` | ancho de la grilla, **+18 de compensación**: el gap entre tarjetas se logra achicando la tarjeta dentro de su celda (`TemplateWidth - 18`), así que el gap sobrante de la última columna cae FUERA del margen derecho y las tarjetas quedan ópticamente justificadas |

> **Ancho de contenido** (para lo que no es la grilla: fecha, píldora, footer,
> estado vacío) = `galHubApps.Width - 18`. Está escrito así en cada tabla.

Verificación de la fórmula en los tres tamaños que importan:

| Ventana | `X` | `Width` | `WrapCount` | Ancho de tarjeta |
|---|---|---|---|---|
| 1366 (monitor / mockup) | 46 | 1292 | 4 | **≈305 px** ✅ ≈ los 305 del mockup |
| 1100 (ventana de trabajo) | 46 | 1026 | 3 | ≈324 px |
| 1024 (tablet horizontal) | 46 | 950 | 3 | ≈299 px |
| 768 (tablet vertical) | 20 | 746 | 2 | ≈355 px |

*(≈ porque cuando la barra de desplazamiento vertical aparece se come ~5px del
ancho útil. El cálculo es `Width / WrapCount − 18`.)*

### 4.1 Header (10 controles: 5 de barra y marca + 4 del chip + la píldora de entorno)

> **El header mide 62 en total, línea incluida.** El `.hdr` del mockup declara
> `height:62px` con `border-bottom:1px` y `box-sizing:border-box`: el borde va
> **adentro** de los 62. Por eso el rectángulo blanco es de **61** y la línea
> ocupa el píxel 61 → el bloque cierra en 62 y todo lo que sigue arranca en 62.
> La alternativa (rectángulo de 62 + línea en 62 = 63 de alto) empujaba un píxel
> todo el cuerpo respecto del mockup. Los controles del header se centran con
> `(shpHubHeader.Height - Self.Height) / 2` en vez de un `Y` literal: si mañana
> el header cambia de alto, se reacomodan solos (regla 0.8).

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubHeader` | Rectángulo clásico | X / Y / W / H | `0` / `0` / `Parent.Width` / `61` |
| | | Fill | `ThemeSurface` |
| `shpHubHeaderLinea` | Rectángulo clásico | X / W / H | `0` / `Parent.Width` / `1` |
| | | Y | `shpHubHeader.Y + shpHubHeader.Height` |
| | | Fill | `ThemeLine` |
| `shpHubMarca` | Botón clásico | X / W / H | `22` / `36` / `36` |
| | | Y | `(shpHubHeader.Height - Self.Height) / 2` |
| | | Text / Size / FontWeight | `"₵"` / `14.25` / `FontWeight.Bold` |
| | | Fill / HoverFill / PressedFill | `ThemeAccent` / `ThemeAccent` / `ThemeAccent` |
| | | Color / BorderThickness | `Color.White` / `0` |
| | | Radius (las 4) | `9` |
| | | DisplayMode | `DisplayMode.View` |
| `lblHubMarca` | Etiqueta clásica | X / W / H | `68` / `170` / `20` |
| | | Y | `shpHubMarca.Y` |
| | | Text / Size / FontWeight | `"CashHub"` / `10.88` / `FontWeight.Semibold` |
| `lblHubMarcaSub` | Etiqueta clásica | X / W / H | `68` / `220` / `16` |
| | | Y | `lblHubMarca.Y + lblHubMarca.Height` |
| | | Text / Size / Color | `"Portal de operaciones"` / `8.25` / `ThemeInk3` |

> `₵` (colón) es el glifo de marca del mockup y Segoe UI lo tiene. Si tu entorno
> lo dibuja como caja vacía, cambiá el `Text` a `"CH"` y anotalo.

**Chip de usuario** — se ancla al borde derecho con el **mismo margen de 22 que
la marca del lado izquierdo** (el mockup usa `padding:0 22px` en el header) y
**colapsa a solo avatar** por debajo de 900px de ancho (en vertical no hay lugar
para nombre + correo):

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubUserChip` | **Etiqueta clásica** *(no rectángulo: necesita `Radius`, §0.3)* | Text | `""` |
| | | Width / Height | `If(Parent.Width < 900, 40, 232)` / `40` |
| | | X | `Parent.Width - Self.Width - 22` |
| | | Y | `(shpHubHeader.Height - Self.Height) / 2` |
| | | Fill / BorderColor / BorderThickness | `ThemeSurface2` / `ThemeLineSoft` / `1` |
| | | Radius (las 4) | `20` |
| `shpHubAvatar` | Botón clásico | X / W / H | `shpHubUserChip.X + 5` / `30` / `30` |
| | | Y | `shpHubUserChip.Y + 5` |
| | | Radius (las 4) | `15` |
| | | Fill / HoverFill / PressedFill | `ThemeAccentSoft` / `ThemeAccentSoft` / `ThemeAccentSoft` |
| | | Color / FontWeight | `ThemeAccent` / `FontWeight.Bold` |
| | | BorderThickness / DisplayMode | `0` / `DisplayMode.View` |
| | | Text | fórmula §4.1.1 |
| | | Tooltip | `User().FullName & " · " & User().Email` |
| `lblHubUserNombre` | Etiqueta clásica | X / H | `shpHubAvatar.X + 38` / `15` |
| | | Width | `Max(0, shpHubUserChip.Width - 48)` |
| | | Y | `shpHubUserChip.Y + 5` |
| | | Text / FontWeight | `User().FullName` / `FontWeight.Semibold` |
| | | Visible | `Parent.Width >= 900` |
| `lblHubUserMail` | Etiqueta clásica | X / W / H | `lblHubUserNombre.X` / `lblHubUserNombre.Width` / `15` |
| | | Y | `lblHubUserNombre.Y + lblHubUserNombre.Height` |
| | | Text / Size / Color | `User().Email` / `7.88` / `ThemeInk3` |
| | | Visible | `Parent.Width >= 900` |

> 🧨 **El `Max(0, …)` del ancho del nombre no es paranoia**: cuando el chip
> colapsa a 40, `40 - 48 = -8`, y una propiedad `Width` negativa es un error en
> Studio. **`Visible = false` NO evita que la propiedad se evalúe** — los
> controles ocultos siguen calculando su geometría (es justamente lo que
> aprovechamos en §4.4 para el hueco de la píldora). Sin el `Max`, el App Checker
> te tira un error en un control que "no se ve".

#### 4.1.1 `shpHubAvatar.Text` — iniciales sin ninguna llamada de red

```powerfx
Upper(
    Left(First(Split(Trim(User().FullName), " ")).Value, 1) &
    If(
        CountRows(Split(Trim(User().FullName), " ")) > 1,
        Left(Last(FirstN(Split(Trim(User().FullName), " "), 2)).Value, 1),
        ""
    )
)
```

"Randy García" → `RG`; "Randy" → `R`. El `If` evita el clásico `RR` de tomar dos
veces la misma palabra. `User()` sale del token de sesión: **cero llamadas**
(ver §7 — por eso no hay `Office365Users.UserPhoto`).

**Píldora de entorno**:

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `pilHubEntorno` | Botón clásico | Width / Height | `92` / `22` |
| | | X | `shpHubUserChip.X - Self.Width - 14` |
| | | Y | `(shpHubHeader.Height - Self.Height) / 2` |
| | | Text | `gvarEntorno` |
| | | Size / FontWeight | `8.25` / `FontWeight.Semibold` |
| | | Fill / HoverFill / PressedFill | `ThemeInfoSoft` / `ThemeInfoSoft` / `ThemeInfoSoft` |
| | | Color / BorderThickness | `ThemeInfo` / `0` |
| | | Radius (las 4) | `11` |
| | | DisplayMode | `DisplayMode.View` |
| | | Visible | `Parent.Width >= 900` |

> **Receta de píldora** (vale para `pilHubEntorno` y para cualquier badge que
> agregues): Botón clásico en `DisplayMode.View`, `Radius 11–12`, `BorderThickness 0`,
> y **`HoverFill` y `PressedFill` iguales al `Fill`** — si no, un badge de solo
> lectura parpadea al pasar el mouse y parece clicable. `DisplayMode.View` ya
> evita el clic; los tres fills iguales evitan la mentira visual.

> ⚠️ `gvarEntorno` es un **literal** en `OnStart`, no un dato del entorno. En DEV
> va a decir "Producción" salvo que lo cambies a mano antes de exportar.
> Divergencia declarada §8.8 con la alternativa y su costo.

### 4.2 Banner de fail-open (no está en el mockup — diseñado con los mismos tokens)

Aparece **solo** cuando `gvarAccessError` es verdadero, y **empuja** todo el
cuerpo hacia abajo (los bloques siguientes se anclan a él, §4.3).

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubBanner` | Rectángulo clásico *(esquina viva: no necesita `Radius`)* | X / W / H | `0` / `Parent.Width` / `44` |
| | | Y | `shpHubHeaderLinea.Y + shpHubHeaderLinea.Height` *(= 62)* |
| | | Fill | `ThemeAccentSoft` |
| | | Visible | `gvarAccessError` |
| `shpHubBannerBarra` | Rectángulo clásico | X / Y / W / H | `0` / `shpHubBanner.Y` / `3` / `shpHubBanner.Height` |
| | | Fill | `ThemeAccent` |
| | | Visible | `gvarAccessError` |
| `icoHubBanner` | Icono clásico (**Advertencia**) | X / Y / W / H | `galHubApps.X` / `shpHubBanner.Y + 14` / `16` / `16` |
| | | Icon | `Icon.Warning` |
| | | Color / HoverColor / PressedColor | `ThemeAccent` / `ThemeAccent` / `ThemeAccent` |
| | | Visible | `gvarAccessError` |
| `lblHubBanner` | Etiqueta clásica | X / Y | `icoHubBanner.X + 24` / `shpHubBanner.Y` |
| | | W / H | `galHubApps.Width - 18 - 24` / `shpHubBanner.Height` |
| | | Text | `"No pudimos verificar tus accesos con Azure AD. Se muestran todas las aplicaciones; si alguna no te corresponde, no vas a poder usarla."` |
| | | Color / FontWeight | `ThemeAccentDark` / `FontWeight.Semibold` |
| | | Wrap | `true` *(único caso del banner: el texto es largo y ocupa dos líneas)* |
| | | Visible | `gvarAccessError` |

> **`HoverColor` / `PressedColor` iguales al `Color` en los dos íconos clásicos**
> (`icoHubBanner`, `icoHubAccesos`): el ícono clásico trae por default un
> `ColorFade()` del color base al pasar el mouse. Son elementos **decorativos**,
> no botones — si se aclaran al pasar por encima, el usuario cree que son
> clicables y no pasa nada. Es la misma receta que la de las píldoras (§4.1).

> El texto es deliberadamente honesto: no promete que las apps de más funcionen.
> No pueden — los roles de Dataverse de cada app destino siguen mandando (§6).
> Este banner es **la única razón** por la que el fail-open es aceptable: sin
> aviso visible, un usuario sin permisos culparía a la app destino.

### 4.3 Saludo, fecha y píldora de accesos

**Cadena vertical**: cada control se ancla al anterior, así el banner los empuja
solo cuando existe. Es la implementación de la regla 0.8.

El `+34` del `Y` del saludo es el **padding superior del cuerpo** del mockup
(`.body{padding:34px 46px 30px}`) — el mismo bloque de donde sale el margen
lateral de 46 del §4.0. No es un número elegido a ojo.

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `htmHubSaludo` | Texto HTML clásico | X / W | `galHubApps.X` / `galHubApps.Width - 18` |
| | | Y | `shpHubBanner.Y + If(gvarAccessError, shpHubBanner.Height, 0) + 34` |
| | | Height | `44` |
| | | PaddingLeft / Right / Top / Bottom | `0` |
| | | HtmlText | fórmula §4.3.1 |
| `lblHubFecha` | Etiqueta clásica | X / W | `galHubApps.X` / `galHubApps.Width - 18` |
| | | Y / H | `htmHubSaludo.Y + htmHubSaludo.Height + 2` / `22` |
| | | Text | fórmula §4.3.2 |
| | | Size / Color | `10.13` / `ThemeInk2` |
| `shpHubPildoraAccesos` | **Etiqueta clásica** *(no rectángulo: necesita `Radius`, §0.3)* | Text | `""` |
| | | X | `galHubApps.X` |
| | | Y | `lblHubFecha.Y + lblHubFecha.Height + 14` |
| | | Width | `Min(galHubApps.Width - 18, Len(lblHubAccesos.Text) * 6.4 + 46)` |
| | | Height / Radius (las 4) | `26` / `13` |
| | | Fill | `If(CountRows(colAppData) = CountRows(colCatalog), ThemeOkSoft, ThemeInfoSoft)` |
| | | Visible | `gvarLoaded && !gvarAccessError && CountRows(colAppData) > 0` |
| `icoHubAccesos` | Icono clásico (**Marca de verificación**) | X / Y / W / H | `shpHubPildoraAccesos.X + 12` / `shpHubPildoraAccesos.Y + 6` / `14` / `14` |
| | | Icon | `Icon.Check` |
| | | Color / HoverColor / PressedColor | las tres con `If(CountRows(colAppData) = CountRows(colCatalog), ThemeOk, ThemeInfo)` |
| | | Visible | `shpHubPildoraAccesos.Visible` |
| `lblHubAccesos` | Etiqueta clásica | X | `icoHubAccesos.X + icoHubAccesos.Width + 7` |
| | | Y / H | `shpHubPildoraAccesos.Y` / `shpHubPildoraAccesos.Height` |
| | | Width | `shpHubPildoraAccesos.Width - 46` |
| | | Text | `$"Tenés acceso a {CountRows(colAppData)} de {CountRows(colCatalog)} aplicaciones según tus grupos"` |
| | | FontWeight | `FontWeight.Semibold` |
| | | Color | `If(CountRows(colAppData) = CountRows(colCatalog), ThemeOk, ThemeInfo)` |
| | | Visible | `shpHubPildoraAccesos.Visible` |

Tres cosas que valen la pena entender acá, porque son las que suelen construirse mal:

1. **La píldora se autodimensiona por el TEXTO, no por el ancho del label**:
   `shpHubPildoraAccesos.Width` depende de `lblHubAccesos.Text`, y
   `lblHubAccesos.Width` depende de `shpHubPildoraAccesos.Width`. Parece circular
   y no lo es: Power Fx resuelve dependencias **por propiedad**, y `Text` no
   depende de ningún `Width`. El `46` es el espacio del ícono más los paddings.
   El coeficiente es **`6.4`, no `5.9`**: 5.9 es el ancho medio de carácter de
   Segoe UI a `Size 9` en peso **Regular**, y este texto es **Semibold**
   (≈6.2 px/carácter). Con 5.9 la píldora quedaba más angosta que su contenido y
   —como el default de plataforma de `Wrap` es `true`— el texto **envolvía a una
   segunda línea dentro de una caja de 26px de alto y se recortaba**. Acá es
   donde el `Wrap = false` de §2.5 se gana el sueldo: sin él, el bug vuelve.
   El `6.4` deja además ~3% de holgura para que un futuro "10 de 12" no raspe.
2. **Verde vs. azul**: acceso total (`X = Y`) usa `ThemeOk`/`ThemeOkSoft` como el
   estado principal del mockup; acceso parcial usa `ThemeInfo`/`ThemeInfoSoft`,
   igual que la `.limNote` del estado "accesos limitados". **Es la misma píldora**
   — no hay una pantalla ni un control distinto para el usuario con accesos
   limitados, solo cambia el número y el color.
3. **Con `gvarAccessError` la píldora DESAPARECE.** Es lo correcto: en fail-open
   `colAppData` es el catálogo entero, así que decir "tenés acceso a 7 de 7 según
   tus grupos" sería mentira — no se verificó ningún grupo. El banner ocupa ese
   rol comunicacional.

#### 4.3.1 `htmHubSaludo.HtmlText`

```powerfx
"<div style='font-family:Segoe UI,sans-serif;font-size:26px;font-weight:600;color:#201C1D;letter-spacing:-0.2px'>" &
If(
    Hour(Now()) < 12, "Buenos días",
    Hour(Now()) < 19, "Buenas tardes",
    "Buenas noches"
) &
", <span style='color:#C8102E'>" &
First(Split(Trim(User().FullName), " ")).Value &
"</span></div>"
```

- **Por qué Texto HTML y no dos etiquetas**: el saludo es **bicolor** (tinta +
  rojo en el nombre) y una etiqueta clásica tiene un solo `Color`. Con dos
  etiquetas habría que calcular el ancho del texto de la primera para posicionar
  la segunda — canvas no tiene auto-ancho, así que el nombre quedaría flotando a
  una distancia fija y feo con nombres cortos o largos. El control HTML resuelve
  el flujo de texto gratis.
- Los atributos van con **comillas simples** a propósito: dentro de una cadena de
  Power Fx, las comillas dobles habría que duplicarlas (`""`) y la fórmula se
  vuelve ilegible. HTML acepta comillas simples sin problema.
- Los `px` de acá son CSS reales (el control HTML no usa puntos): 26px es el
  tamaño del `h1` del mockup, sin conversión.
- Cortes horarios: `< 12` días, `< 19` tardes, resto noches. `Hour(Now())` usa la
  hora **local del cliente**, que es lo que corresponde para un saludo.
- 🧨 **`PaddingLeft/Right/Top/Bottom = 0` no es opcional**: el visor HTML clásico
  trae **5** en las cuatro, igual que la etiqueta. Con el default, el saludo
  arrancaría 5px a la derecha de la fecha y de la píldora —que sí están en
  `galHubApps.X`— y toda la columna izquierda del cuerpo quedaría desalineada por
  una razón invisible en el editor.
- 🧨 **`Height = 44`, no 36**: el control HTML **no** tiene `AutoHeight`, y una
  caja de texto de 26px necesita ~34px de línea. Con 36 el descendente de la "g"
  de "tardes" y de la "y" de un nombre quedaban cortados contra el borde
  inferior. 44 da aire sin abrir un hueco visible (el `+2` de la fecha se ancla a
  `Height`, así que la cadena vertical se reacomoda sola).

#### 4.3.2 `lblHubFecha.Text`

```powerfx
If(
    gvarLoaded,
    $"{Text(Now(), "dddd", "es-ES")} {Day(Now())} de {Text(Now(), "mmmm", "es-ES")} · Estas son tus aplicaciones de tesorería.",
    "Verificando tus accesos…"
)
```

→ `viernes 15 de agosto · Estas son tus aplicaciones de tesorería.`

- La fecha se arma **por partes** (`dddd`, `Day()`, `mmmm`) en vez de meter el
  literal " de " dentro del formato: los literales dentro de una cadena de
  formato de Power Fx requieren su propio escapado y es una fuente clásica de
  errores mudos.
- El `[$-es-ES]` no hace falta porque el idioma va como tercer argumento de
  `Text()`. Se fija explícito para que la app no cambie de idioma según el
  navegador del usuario.
- **Regla de la casa aunque acá no aplique**: si alguna vez agregás una hora a
  esta pantalla, el formato es `HH` (24 h). `hh` es reloj de 12 h **sin AM/PM** —
  a las 15:00 muestra "03" y nadie se entera hasta que alguien reporta un
  incidente.
- Mientras carga, la bajada se reemplaza por "Verificando tus accesos…", calcando
  el estado de carga del mockup.

### 4.4 Skeleton de carga (`galHubSkeleton`)

Visible solo mientras `!gvarLoaded`. Ocupa exactamente el lugar de la grilla real
para que no haya salto de layout cuando llegan los datos.

**Dos decisiones anti-salto que hay que respetar al construirlo**:

1. **El hueco de la píldora se mantiene aunque la píldora esté oculta.**
   `galHubSkeleton.Y = galHubApps.Y`, y ese `Y` se calcula desde
   `shpHubPildoraAccesos.Y + Height + 24` — un control invisible **igual computa
   su geometría**. Por eso el skeleton arranca en el mismo píxel donde después va
   a arrancar la grilla real, en vez de subir a pegarse a la fecha y bajar de
   golpe cuando aparece la píldora.
2. **El fantasma calca la tarjeta REAL, no el mini del mockup.** El diagrama de
   "estado cargando" usa `.miniCard` (glifo de 30, una sola barra) porque es una
   maqueta a escala reducida dentro del HTML de presentación, no una pantalla.
   Acá el fantasma tiene el glifo de 44 y tres barras: título + dos líneas de
   descripción, exactamente la anatomía de `galHubApps`. Un skeleton que no calca
   lo que viene después es peor que no tener skeleton.

| Propiedad de `galHubSkeleton` | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| X / Y / Width / Height | `galHubApps.X` / `galHubApps.Y` / `galHubApps.Width` / `galHubApps.Height` |
| `Items` | `Sequence(6)` |
| `WrapCount` | `Max(1, RoundDown(Self.Width / 300, 0))` |
| `TemplateSize` / `TemplatePadding` | `214` / `0` |
| `ShowScrollbar` | `false` |
| `Visible` | `!gvarLoaded` |
| `TemplateFill` | `Color.Transparent` |

`Sequence(6)` genera seis filas en memoria: **cero red**, y respeta el mismo
`WrapCount` que la grilla real, así que en vertical vas a ver 2 columnas de
fantasmas y en monitor 4.

Dentro del template (recordá la regla 0.7: `X`/`Y` relativos al template).

**Los cinco son Etiquetas clásicas con `Text: ""`**, no rectángulos: todas llevan
`Radius`, y el Rectángulo clásico no lo tiene (§0.3). El `Text` vacío + los
`Padding` en `0` de §2.5 las convierten en superficies puras.

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubSkTarjeta` | Etiqueta clásica | Text | `""` |
| | | X / Y | `0` / `0` |
| | | W / H | `Parent.TemplateWidth - 18` / `Parent.TemplateHeight - 18` |
| | | Fill / BorderColor / BorderThickness | `ThemeSurface` / `ThemeLine` / `1` |
| | | Radius (las 4) | `13` |
| `shpHubSkGlifo` | Etiqueta clásica | Text | `""` |
| | | X / Y / W / H | `20` / `20` / `44` / `44` |
| | | Fill / Radius (las 4) | `ThemeLineSoft` / `11` |
| `shpHubSkBarra1` | Etiqueta clásica | Text | `""` |
| | | X / Y / H | `20` / `82` / `12` |
| | | Width | `(Parent.TemplateWidth - 58) * 0.55` |
| | | Fill / Radius (las 4) | `ThemeLineSoft` / `6` |
| `shpHubSkBarra2` | Etiqueta clásica | Text | `""` |
| | | X / Y / H | `20` / `106` / `10` |
| | | Width | `(Parent.TemplateWidth - 58) * 0.85` |
| | | Fill / Radius (las 4) | `ThemeLineSoft` / `6` |
| `shpHubSkBarra3` | Etiqueta clásica | Text | `""` |
| | | X / Y / H | `20` / `124` / `10` |
| | | Width | `(Parent.TemplateWidth - 58) * 0.6` |
| | | Fill / Radius (las 4) | `ThemeLineSoft` / `6` |

> **El shimmer del mockup no existe en canvas clásico** (es una animación CSS de
> `background-position`). Quedan rectángulos grises estáticos. Divergencia §8.3.
> Ningún control del skeleton tiene `OnSelect`: no es clicable ni pretende serlo.

### 4.5 La grilla real (`galHubApps`)

| Propiedad | Valor |
|---|---|
| Tipo | **Galería vertical en blanco** |
| `X` | `If(Parent.Width < 900, 20, 46)` |
| `Y` | `shpHubPildoraAccesos.Y + shpHubPildoraAccesos.Height + 24` |
| `Width` | `Parent.Width - Self.X * 2 + 18` |
| `Height` | `shpHubFooter.Y - Self.Y - 20` |
| `Items` | `Sort(colAppData, 'Order')` |
| `WrapCount` | `Max(1, RoundDown(Self.Width / 300, 0))` |
| `TemplateSize` | `214` |
| `TemplatePadding` | `0` |
| `TemplateWidth` | **NO SE TOCA** — es de solo lectura: la galería la deriva de `Width / WrapCount`. Es la que consumen los controles del template |
| `Visible` | `gvarLoaded && CountRows(colAppData) > 0` |
| `TemplateFill` | `Color.Transparent` |
| `OnSelect` | `Launch(galHubApps.Selected.AppURL, {}, LaunchTarget.Replace)` |

*(`ShowScrollbar` se deja en su default `true`: con más apps que filas visibles,
la grilla scrollea. Ojo — la barra descuenta ~5px del ancho útil cuando aparece,
así que los anchos calculados abajo son el techo, no el valor exacto.)*

**`WrapCount` explicado**, porque es el corazón del responsive:
`RoundDown(Self.Width / 300, 0)` = cuántas **celdas** de 300 entran en el ancho de
la galería. El `Max(1, …)` evita `WrapCount = 0` (galería sin columnas = pantalla
en blanco) en cualquier ventana angosta. No hay tope superior a propósito: en un
monitor ultra-ancho preferimos más columnas que cuatro tarjetas estiradas a 600px.

> ⚠️ **Precisión honesta: el 300 es el mínimo de la CELDA, no de la tarjeta.**
> La tarjeta mide `celda − 18` (el gap sale de adentro de la celda, §4.0), así que
> el **ancho mínimo real de tarjeta es 282**, que ocurre justo en cada salto de
> columna. Decir "tarjetas de 300 mínimo" sería falso; el rango real está en §5.
>
> **La alternativa evaluada y descartada** era dividir por `318` (= 300 + 18) para
> garantizar tarjetas de ≥300: cuesta una columna en tablet horizontal — a 1024px
> daría **2** columnas de 457px en vez de **3** de 299, que es peor diseño por
> 1px de purismo. Se conserva el `300` y se documenta el rango verdadero.

**`TemplateSize = 214`**: 196 de tarjeta + 18 de gap. El mockup usa
`min-height:172` porque en CSS la tarjeta es un flex que crece con el texto; en
canvas la altura es fija, y las descripciones **reales** del seed llegan a 88
caracteres ("Monitoreo de la ingesta de pre-registros y atencion de los correos
que quedaron trabados"), que a 300px de ancho son 3 líneas. 172 las cortaba.
Divergencia §8.4.

> ⚠️ **Las galerías clásicas auto-seleccionan la primera fila** al cargar
> `Items`. Acá es inofensivo y hay que saber por qué: la auto-selección
> **no dispara `OnSelect`** (solo lo hace la selección del usuario), así que
> nadie va a ser lanzado a la app de Recepción por abrir el hub. Y no hay ningún
> estilo atado a `ThisItem.IsSelected`, así que tampoco se ve una tarjeta
> "elegida" sin motivo.

#### La tarjeta (template de `galHubApps`)

**Toda la tarjeta es clicable.** La acción vive en el `OnSelect` de la GALERÍA y
**todos** los hijos del template delegan con `Select(Parent)`. Si la acción
viviera solo en el botón del fondo, las etiquetas se comerían el clic y el
usuario tendría que cazar píxeles vacíos entre el título y la descripción.

**`btnHubTarjeta` se inserta PRIMERO** para que quede al fondo del z-order: es a
la vez la superficie blanca de la tarjeta y su zona clicable.

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `btnHubTarjeta` | Botón clásico | X / Y | `0` / `0` |
| | | W / H | `Parent.TemplateWidth - 18` / `Parent.TemplateHeight - 18` |
| | | `Text` | `""` |
| | | `Fill` / `HoverFill` | `ThemeSurface` / `ThemeSurface` |
| | | `PressedFill` | `ThemeAccentSoft` |
| | | `BorderColor` / `BorderThickness` | `ThemeLine` / `1` |
| | | `HoverBorderColor` / `PressedBorderColor` | `ThemeAccent` / `ThemeAccent` |
| | | Radius (las 4) | `13` |
| | | `OnSelect` | `Select(Parent)` |
| | | `AccessibleLabel` | `"Abrir " & ThisItem.'Display Name'` |
| `shpHubGlifo` | **Etiqueta clásica** *(no rectángulo: necesita `Radius`, §0.3)* | Text | `""` |
| | | X / Y / W / H | `20` / `20` / `44` / `44` |
| | | Fill / Radius (las 4) | `ThemeAccentSoft` / `11` |
| | | `OnSelect` | `Select(Parent)` |
| `imgHubGlifo` | Imagen clásica | X / Y / W / H | `20` / `20` / `44` / `44` |
| | | `Image` | `ThisItem.Icon` |
| | | `ImagePosition` | `ImagePosition.Fit` |
| | | `Visible` | `!IsBlank(ThisItem.Icon)` |
| | | `OnSelect` | `Select(Parent)` |
| `lblHubGlifoInicial` | Etiqueta clásica | X / Y / W / H | `20` / `20` / `44` / `44` |
| | | `Text` | fórmula §4.6.1 |
| | | FontWeight / Color | `FontWeight.Bold` / `ThemeAccent` |
| | | Size / Align | `11.25` / `Align.Center` |
| | | `Visible` | `IsBlank(ThisItem.Icon)` |
| | | `OnSelect` | `Select(Parent)` |
| `lblHubTarjetaTitulo` | Etiqueta clásica | X / Y / H | `20` / `78` / `22` |
| | | Width | `Parent.TemplateWidth - 58` |
| | | `Text` | `ThisItem.'Display Name'` |
| | | Size / FontWeight | `11.25` / `FontWeight.Semibold` |
| | | `OnSelect` | `Select(Parent)` |
| `lblHubTarjetaDesc` | Etiqueta clásica | X / Y | `20` / `102` |
| | | Width | `Parent.TemplateWidth - 58` |
| | | Height | `Parent.TemplateHeight - 163` |
| | | `Text` | `ThisItem.Description` |
| | | Color | `ThemeInk2` |
| | | Wrap / VerticalAlign | `true` *(pisa el default de §2.5: acá SÍ queremos 3 líneas)* / `VerticalAlign.Top` |
| | | `OnSelect` | `Select(Parent)` |
| `shpHubTarjetaSep` | Rectángulo clásico | X / H | `20` / `1` |
| | | Y | `Parent.TemplateHeight - 61` |
| | | Width | `Parent.TemplateWidth - 58` |
| | | Fill | `ThemeLineSoft` |
| | | `OnSelect` | `Select(Parent)` |
| `lblHubTarjetaAbrir` | Etiqueta clásica | X / W / H | `20` / `140` / `18` |
| | | Y | `shpHubTarjetaSep.Y + 12` |
| | | `Text` | `"Abrir aplicación"` |
| | | FontWeight / Color | `FontWeight.Semibold` / `ThemeAccent` |
| | | `OnSelect` | `Select(Parent)` |
| `lblHubTarjetaFlecha` | Etiqueta clásica | X | `Parent.TemplateWidth - 52` |
| | | Y / W / H | `lblHubTarjetaAbrir.Y` / `14` / `18` |
| | | `Text` / Color / Align | `"→"` / `ThemeInk3` / `Align.Right` |
| | | `OnSelect` | `Select(Parent)` |

> **`PressedFill = ThemeAccentSoft` es un agregado, no está en el mockup.** El
> HTML solo define `:hover` — no tiene estado `:active`. Se agrega porque en
> tablet **no hay hover**: sin feedback de presionado, el usuario toca la tarjeta,
> no ve nada durante el segundo que tarda el `Launch()` y vuelve a tocar. Usa un
> token existente (`ThemeAccentSoft`, el mismo del glifo), así que no inventa
> paleta. Declarado en §8.19; si en la revisión visual molesta, se cambia a
> `ThemeSurface2` o se iguala al `Fill` y listo.

**De dónde salen los números derivados de la tarjeta** (altura de tarjeta =
`TemplateHeight − 18`, hoy 196; **jamás escribas 196 literal**, así cambiar
`TemplateSize` reacomoda todo solo):

| Medida | Fórmula | Valor con `TemplateSize 214` |
|---|---|---|
| Ancho útil de texto | `TemplateWidth − 18 − 40` = `TemplateWidth − 58` | — |
| Alto de la descripción | `(TemplateHeight − 18) − 102 − 43` = `TemplateHeight − 163` | 51 (3 líneas a `Size 9`) |
| Y del separador | `(TemplateHeight − 18) − 43` = `TemplateHeight − 61` | 153 |
| Y de "Abrir aplicación" | `separador + 12` | 165 |
| X de la flecha | `(TemplateWidth − 18) − 20 − 14` = `TemplateWidth − 52` | — |

El `43` es el bloque inferior de la tarjeta y se descompone **exactamente** así:
**12** de aire entre el separador y la fila "Abrir aplicación" + **18** de alto de
esa fila (`lblHubTarjetaAbrir.Height`) + **13** de padding inferior de la tarjeta
= 43. (El mockup usa 12 + 15 + 16 = 43 con una fila de 15px; acá la fila mide 18
porque una etiqueta clásica a `Size 9` necesita ese alto para no recortar, y los
3px salen del padding inferior. Mismo total, distinta repartición.)

> **`imgHubGlifo` ocupa el tile ENTERO (44 × 44 en 20/20), no un recuadro
> interior.** Está encima de `shpHubGlifo`, así que el fondo `ThemeAccentSoft`
> del tile se ve a través del PNG transparente. El margen interior del ícono
> viene **horneado en el asset** (motivo de ~46 dentro de 88, §2.8.1). La
> alternativa —imagen de 24 centrada— obligaba a exportar el trazo a 0.55px
> efectivos y el ícono desaparecía.

### 4.6 El glifo: 100% dinámico, con respaldo

**No hay un solo `Switch` por app key en esta guía, y no debe haberlo.** Agregar
la app número 8 al catálogo tiene que ser una fila en Dataverse, no una release
de la canvas app.

El glifo del mockup (camión, balanza, teclado, sobre, eslabones…) **es el diseño
aprobado y se implementa** — pero viaja por **DATO**, no por código:

- Si `'Icon'` (columna de **imagen** del catálogo) tiene contenido →
  `imgHubGlifo` lo muestra a 44 × 44 **cubriendo el tile completo**, con el fondo
  `ThemeAccentSoft` visible a través de la transparencia del PNG. Es el estado
  objetivo, y el prerrequisito para llegar ahí está en **§2.8**.
- Si está en blanco → `lblHubGlifoInicial` muestra un **identificador de dos
  caracteres** en `ThemeAccent` sobre el mismo tile.

#### 4.6.1 `lblHubGlifoInicial.Text` — dos caracteres, deterministas, sin mapa

```powerfx
Upper(
    With(
        { _k: Trim(Coalesce(ThisItem.'App Key', ThisItem.'Display Name')) },
        Left(_k, 1) & If(Len(_k) > 1, Right(_k, 1), "")
    )
)
```

**Primera + última letra de `'App Key'`.** Un solo carácter NO sirve: con el
catálogo real, `reception` y `recounting` dan las dos "R" (y por display name,
"Recepción" y "Recuento" también) — dos tarjetas vecinas con el mismo glifo es
peor que no tener glifo. Con la regla de dos caracteres, el seed actual queda:

| `'App Key'` | Glifo de respaldo | App |
|---|---|---|
| `reception` | **RN** | Recepción |
| `supervisor` | **SR** | Supervisor |
| `recounting` | **RG** | Recuento |
| `digitization` | **DN** | Digitación |
| `emailval` | **EL** | Ingesta de correos |
| `tracker` | **TR** | Trazabilidad |
| `clientadmin` | **CN** | Admin. de clientes |

Siete valores **distintos**, sin ningún `Switch`, sin tabla de mapeo y sin tocar
la app cuando se agregue la octava. Detalles del diseño de la fórmula:

- Se prefiere `'App Key'` sobre `'Display Name'` porque la key es el
  identificador estable del catálogo (tiene índice único: `sanic_AppKey_Unique`)
  y no cambia si mañana "Recepción" pasa a llamarse "Recepción de tulas".
  El `Coalesce` cae al display name solo si la key estuviera vacía.
- **Primera + última** y no "primeras dos" ni "primera + tercera": con este seed,
  `Left(k, 2)` da RE/SU/**RE** (colisiona) y `Left(k,1) & Mid(k,3,1)` da
  RC/SP/**RC** (colisiona). Primera+última es la única de las tres reglas simples
  que separa `reception` de `recounting`.
- El `If(Len(_k) > 1, …)` cubre una key de un solo carácter sin duplicarla
  ("X" → `X`, no `XX`).
- ⚠️ **La unicidad no está garantizada por construcción**: dos keys futuras
  podrían empezar y terminar igual. Si pasa, el arreglo es elegir otra key o —
  mejor — cargar el `Icon`; **nunca** un `Switch` en la app.

> 🔎 **Hoy vas a ver SIEMPRE el respaldo de dos letras**: el seed
> (`data/seed/30-appcatalogs.json`) no carga `sanic_icon` en ninguna de las 7
> filas y el seeder todavía no soporta columnas de imagen (§2.8.3). Eso no es un
> bug de la pantalla: es el estado real del dato, y la pantalla se ve
> **claramente peor que el mockup** hasta que se resuelva (§8.11). Cuando las
> imágenes se carguen —por el seeder o a mano desde el formulario del catálogo—
> las tarjetas las toman **sin republicar la app**.
>
> Si Studio se queja del tipo en `IsBlank(ThisItem.Icon)` (las columnas de imagen
> se exponen como blob, no como texto), usá `IsBlank(Text(ThisItem.Icon))` en las
> dos propiedades `Visible` — y siempre las dos, o vas a ver el glifo duplicado.

### 4.7 Estado vacío

Visible cuando la carga terminó y no hay ni una app para mostrar.

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubVacio` | **Etiqueta clásica** *(no rectángulo: necesita `Radius`, §0.3)* | Text | `""` |
| | | X / Y | `galHubApps.X` / `galHubApps.Y` |
| | | W / H | `galHubApps.Width - 18` / `150` |
| | | Fill / BorderColor / BorderThickness | `ThemeSurface` / `ThemeLine` / `1` |
| | | Radius (las 4) | `13` |
| | | `Visible` | `gvarLoaded && CountRows(colAppData) = 0` |
| `lblHubVacioTitulo` | Etiqueta clásica | X / Y | `shpHubVacio.X + 28` / `shpHubVacio.Y + 44` |
| | | W / H | `shpHubVacio.Width - 56` / `26` |
| | | `Text` | `"No tenés aplicaciones asignadas"` |
| | | Size / FontWeight | `11.25` / `FontWeight.Semibold` |
| | | `Visible` | `shpHubVacio.Visible` |
| `lblHubVacioSub` | Etiqueta clásica | X / Y | `lblHubVacioTitulo.X` / `lblHubVacioTitulo.Y + 30` |
| | | W / H | `lblHubVacioTitulo.Width` / `40` |
| | | `Text` | `"Contactá a tu supervisor para que te agregue a los grupos de Azure AD que correspondan a tu rol."` |
| | | Size / Color / Wrap | `10.13` / `ThemeInk2` / `true` |
| | | `Visible` | `shpHubVacio.Visible` |

> **Sobre la condición de visibilidad**: el pedido original incluía
> `&& !gvarAccessError`. Se omite **a propósito** y acá está el porqué: en
> fail-open `colAppData` = `colCatalog` (se copia el catálogo entero), así que
> `CountRows(colAppData) = 0` con `gvarAccessError` verdadero solo puede pasar si
> el catálogo mismo está vacío — y en ese caso este mensaje también es el
> correcto, con el banner arriba explicando que además falló la validación. La
> conjunción extra sería código muerto que engaña al que lo lea después.
> Declarado en §8.10.
>
> Este estado **no aparece por "no hay fila seleccionada"** — solo por colección
> vacía. Es la aplicación de la regla de galerías clásicas (§4.5).
>
> **Complemento obligatorio en `galHubApps`**: su `Visible` es
> `gvarLoaded && CountRows(colAppData) > 0`, exactamente el complemento de este
> `Visible`. Sin el `> 0` la galería seguiría "visible" (vacía, invisible en la
> práctica) ocupando el mismo espacio, y el "sin grilla" del checklist §9.2 no
> sería literal ni verificable: los dos estados son mutuamente excluyentes por
> construcción, no por casualidad visual.

### 4.8 Footer

Anclado al borde inferior, así que en vertical sube solo.

| Control | Tipo | Propiedad | Valor |
|---|---|---|---|
| `shpHubFooter` | Rectángulo clásico | X / W / H | `0` / `Parent.Width` / `46` |
| | | Y | `Parent.Height - Self.Height` |
| | | Fill | `ThemeSurface2` |
| `shpHubFooterLinea` | Rectángulo clásico | X / Y / W / H | `0` / `shpHubFooter.Y` / `Parent.Width` / `1` |
| | | Fill | `ThemeLineSoft` |
| `lblHubFooterIzq` | Etiqueta clásica | X / W / H | `galHubApps.X` / `240` / `46` |
| | | Y | `shpHubFooter.Y` |
| | | `Text` | `"CashHub · CH-App-Hub v2.0"` |
| | | Size / Color | `8.25` / `ThemeInk3` |
| `lblHubFooterDer` | Etiqueta clásica | Width | `Min(560, galHubApps.Width - 18 - 260)` |
| | | X | `galHubApps.X + galHubApps.Width - 18 - Self.Width` |
| | | Y / H | `shpHubFooter.Y` / `46` |
| | | `Text` | `"Los accesos se administran por grupos de Azure AD · ¿Te falta una app? Contactá a tu supervisor"` |
| | | Size / Color / Align | `8.25` / `ThemeInk3` / `Align.Right` |
| | | `Visible` | `Parent.Width >= 900` |

> En vertical el footer conserva solo la firma de versión; la nota larga se
> esconde porque a 768px chocaría con ella. La misma información vive en el
> estado vacío, que es donde el usuario que **necesita** ese dato la va a leer.
> Divergencia §8.12.

---

## 5. Cambios de tamaño: qué pasa exactamente

Cómo se reacomoda la pantalla, control por control, sin scroll horizontal en
ningún punto por encima de `MinScreenWidth`:

| Elemento | ≥ 900px (monitor / tablet horizontal) | < 900px (tablet vertical) |
|---|---|---|
| Márgenes laterales | 46 | 20 |
| Chip de usuario | avatar + nombre + correo (232px) | solo avatar (40px) |
| Píldora "Producción" | visible | oculta |
| Saludo / fecha | ancho completo de contenido | ídem (se re-fluyen solos) |
| Píldora de accesos | ancho según texto, tope = ancho de contenido | ídem (el `Min()` la recorta) |
| Grilla | 2, 3, 4 o más columnas según el ancho — tabla de abajo | 2 columnas |
| Alto de la grilla | `footer.Y − gal.Y − 20` (scroll vertical si sobran filas) | ídem |
| Footer | firma + nota larga | solo firma |

### 5.1 Columnas y ancho de tarjeta: los números reales

Nada de esto es aproximado — sale de aplicar
`n = RoundDown((W − 2·gutter + 18) / 300)` y `tarjeta = ancho/n − 18`:

| Ancho de ventana | Columnas | Ancho de tarjeta |
|---|---|---|
| 768 – 899 (vertical, gutter 20) | **2** | 355 → 420 |
| 900 – 973 (gutter 46) | **2** | 395 → 431 |
| 974 – 1273 | **3** | 282 → 382 |
| 1274 – 1573 | **4** | 282 → 357 |
| 1574 – 1873 | **5** | 282 → 342 |

**El rango real de ancho de tarjeta es 282 – 431**, no "siempre 300–355": la
tarjeta es más angosta justo después de cada salto de columna (282) y más ancha
justo antes del siguiente. Cualquier afirmación de esta guía que diga otra cosa
es un bug de la guía.

> ⚠️ **Hay una discontinuidad conocida en 900**: al pasar de 899 a 900 el margen
> lateral salta de 20 a 46, así que la tarjeta **se achica** de ~420 a ~395
> mientras la ventana **crece** 1px. Es contraintuitivo pero deliberado: 900 es
> donde el header cambia de forma (chip colapsado, píldora de entorno oculta),
> o sea que el layout entero se reconfigura ahí — un salto de 25px en el ancho de
> tarjeta en medio de esa transición no se percibe. La alternativa era un gutter
> continuo tipo `Min(46, Max(20, (Parent.Width - 768) * 0.2 + 20))`: elimina el
> salto a cambio de una fórmula que nadie puede leer de un vistazo, en un punto
> que ningún usuario va a pisar salvo redimensionando a mano. Declarado en §8.21.

**Lo que NO cambia**: alturas de header (62) y footer (46), altura de tarjeta
(214 de template), tipografías. Un launcher no necesita tipografía fluida; la
necesita la grilla, y esa sí lo es.

---

## 6. Seguridad por diseño

### 6.1 El filtro de este hub es UX, no una frontera de seguridad

`colAppData` decide **qué ve** el usuario, no **qué puede hacer**. La
autorización real la imponen los **roles de Dataverse de cada app destino**: si
alguien llega a la URL de Recuento sin el rol `CH-SR-Counter`, la app abre y no
puede leer ni escribir nada. Consecuencias prácticas:

- Que el hub muestre de más (caso fail-open) **no es una escalada de privilegios**.
- Que el hub muestre de menos es un problema de **experiencia** (el usuario cree
  que no tiene la app), no de seguridad — y se arregla con una fila en
  `CH-Tbl-AppCatalogAccesses`, no tocando la canvas app.
- Ningún control de esta pantalla escribe en Dataverse. La app es **read-only**.

### 6.2 Por qué `Launch(AppURL)` es confiable

`AppURL` viene de una tabla de Dataverse que un administrador puede editar por
Web API o Excel. Sin validación, escribir ahí una URL arbitraria convertiría al
hub en un **lanzador de phishing con identidad corporativa**: el usuario hace
clic dentro de una app de la empresa y aterriza en una página que pide sus
credenciales.

Por eso existe **`GuardAppCatalogPlugin`**, síncrono en **Pre-validation** de
`Create`/`Update` de `sanic_chtblappcatalog`: la URL debe ser **https absoluta
con host exactamente `apps.powerapps.com`** en puerto estándar. Bloquea `http`,
hosts ajenos, sufijos tipo `apps.powerapps.com.evil.com`, trucos de userinfo con
`@` y puertos raros. Blanco es válido (fila en preparación). Ningún rol CH tiene
write sobre el catálogo — solo lectura — así que la escritura queda en
System Administrator/System Customizer, y **también para ellos rige el guard**.

**Conclusión operativa**: el `Launch()` de la galería no necesita validar nada en
cliente. Y si alguna vez alguien propone "démosle una URL configurable al hub sin
pasar por el catálogo", la respuesta es no, y este párrafo es el porqué.

### 6.3 Fail-open: se conserva, pero se hace visible

Si Graph falla, `IfError` pone `gvarAccessError = true` y el filtro deja pasar
todo el catálogo. Es la decisión correcta para un launcher: caer cerrado dejaría
a toda la operación sin acceso a sus apps por una intermitencia de red,
**sin ganar nada** (la autorización real está río abajo, §6.1).

Lo que agrega este rediseño es que **el degradado se ve**:

- `shpHubBanner` + `lblHubBanner` (§4.2) lo anuncian con texto honesto.
- La píldora de accesos (`shpHubPildoraAccesos` + `icoHubAccesos` +
  `lblHubAccesos`) **se oculta**, porque su mensaje ("X de Y según tus grupos")
  sería falso.
- El **redirect de app única** (`OnStart` bloque 7) lleva `&& !gvarAccessError`:
  con la validación caída no se decide por el usuario.

### 6.4 La columna deprecada

`'AAD Group Id'` **del catálogo** (`sanic_aadgroupid` en `sanic_chtblappcatalog`)
está **deprecada y bloqueada**: cualquier escritura no vacía rebota señalando la
tabla nueva. Esta guía **no la referencia en ninguna fórmula** y no debe hacerlo
nunca. El gating vive en `CH-Tbl-AppCatalogAccesses` (una fila = una app + un
grupo, GUID normalizado y sin duplicados por `GuardAppCatalogAccessPlugin`).

Si en una revisión aparece `'CH-Tbl-AppCatalogs'` y `'AAD Group Id'` en la misma
fórmula, es un hallazgo crítico, no un detalle de estilo.

### 6.5 Lectura del catálogo

Los 8 roles CH tienen `prvRead` **Global** sobre `sanic_CHTblAppCatalog` y sobre
`sanic_CHTblAppCatalogAccess`. Un usuario sin ningún rol CH ve el **estado
vacío** (§4.7), no un error de permisos — que es exactamente el mensaje que
necesita ("contactá a tu supervisor").

### 6.6 `LaunchTarget.Replace`

Reemplaza la pestaña del hub por la app destino. Es deliberado (el hub es un
trampolín, no un tablero) y significa que **no hay vuelta atrás dentro de la
app**: el usuario vuelve con el botón atrás del navegador o reabriendo el hub. Si
alguna vez se pide "que abra en pestaña nueva", el cambio es
`LaunchTarget.New` — pero entonces conviven dos sesiones de Power Apps y sube el
consumo de licencia y memoria del cliente. Se mantiene `Replace`.

---

## 7. Performance: el presupuesto de llamadas

### 7.1 Al abrir la app

| # | Llamada | Cuándo | Cuántas |
|---|---|---|---|
| 1 | `Filter('CH-Tbl-AppCatalogs', Enabled)` | `OnStart` bloque 2 | **1** (delegada: filtro booleano sobre Dataverse) |
| 2 | `ForAll('CH-Tbl-AppCatalogAccesses', …)` | `OnStart` bloque 3 | **1** |
| 3 | `checkMemberGroups` vía `Office365Groups.HttpRequest` | `OnStart` bloque 4 | **`ceil(N/20)`**, N = grupos distintos en la tabla de accesos |

Con el dato de hoy (7 apps habilitadas, los 4 grupos de ClientAdministration):
**3 llamadas en total**. La pantalla agrega **0**.

### 7.2 Por qué la pantalla no agrega ninguna

| Elemento | Cómo se resuelve | Costo |
|---|---|---|
| Avatar | iniciales de `User().FullName` | 0 — sale del token de sesión |
| Nombre y correo del chip | `User().FullName` / `User().Email` | 0 — ídem |
| Saludo | `Hour(Now())` | 0 — reloj del cliente |
| Fecha | `Text(Now(), …, "es-ES")` | 0 |
| Píldora "X de Y" | `CountRows(colAppData)` / `CountRows(colCatalog)` | 0 — colecciones en memoria |
| Tarjetas | `Sort(colAppData, 'Order')` | 0 — colección en memoria |
| Skeleton | `Sequence(6)` | 0 — tabla generada |

> ❌ **Lo que deliberadamente NO se hace**: `Office365Users.UserPhoto()` /
> `Office365Users.MyProfileV2()` para el avatar. Serían 1–2 llamadas más contra
> Graph, en el camino crítico del arranque, para dibujar un círculo de 30px.
> Las iniciales cuestan cero y comunican lo mismo.

### 7.3 Por qué no hay N+1

El anti-patrón clásico de un hub es resolver el acceso **dentro del template de
la galería**: `Filter('CH-Tbl-AppCatalogAccesses', 'App Catalog' = ThisItem…)`
por tarjeta. Eso es **una llamada por tarjeta por render** (y las galerías
re-renderizan al scrollear, redimensionar y rotar): 7 apps ≈ 7 llamadas que se
repiten sin control.

El `OnStart` actual hace lo correcto: **lee las dos tablas UNA vez y hace el join
en memoria** (`colCatalog` × `colAccess` × `colMyGroupIds`). El costo es
constante respecto de la cantidad de apps. Por eso este `OnStart` no se toca.

**Regla para el que mantenga esto**: si te encontrás escribiendo el nombre de un
datasource (`'CH-Tbl-…'`) dentro del template de `galHubApps`, pará. Ese dato
tiene que venir precomputado en `colAppData` desde `OnStart`.

### 7.4 Dos advertencias honestas

1. **Límite de filas de datos**: `ForAll('CH-Tbl-AppCatalogAccesses', …)`
   materializa la tabla y **no es delegable** — trae hasta el límite de filas de
   la app (default **500**). Hoy la tabla tiene un puñado de filas, así que no
   hay problema; el techo es 500 filas de (app, grupo). Subilo a 2000 en
   **Configuración → General → Límite de filas de datos** si el catálogo llegara
   a crecer, y si alguna vez se acerca a eso, la solución no es subir el número:
   es mover el filtro a una vista o a un Custom API.
2. **La columna `Icon` sí cuesta ancho de banda cuando se llene**: las columnas
   de imagen de Dataverse se entregan como URL, y el control `imgHubGlifo` hace
   un `GET` por tarjeta la primera vez (después queda en caché del navegador).
   Son 7 descargas pequeñas fuera de Power Fx, no llamadas de datos. Vale la
   pena saberlo antes de subir PNG de 2MB como íconos de 44px.

---

## 8. Ledger de divergencias declaradas vs. mockup

Nada de esto es un accidente. Si el resultado en Studio no coincide con el HTML,
la explicación está en esta tabla — y si NO está acá, es un bug.

> ### 8.0 — Enmienda a la Decisión #7 del mockup (leer antes que la tabla)
>
> La última nota de "Decisiones del rediseño" del mockup afirma: *"Todo
> construible con controles clásicos: tarjetas planas con borde 1px, radios 8–13,
> píldoras y glifos — **la sombra del hover es la única pérdida aceptada** al
> pasar a canvas"*.
>
> **Eso era optimista y este ledger lo enmienda de frente.** Al bajar el HTML a
> controles clásicos aparecen **seis** pérdidas visuales, no una:
>
> | | Pérdida | Fila |
> |---|---|---|
> | 1 | sombra del hover *(la que el mockup sí anticipaba)* | 8.1 |
> | 2 | `translateY(-2px)` del hover | 8.1 |
> | 3 | el hover de borde no responde sobre las etiquetas | 8.2 |
> | 4 | shimmer animado del skeleton | 8.3 |
> | 5 | `translateX(3px)` de la flecha en hover | 8.5 |
> | 6 | elipsis "…" al truncar la descripción | 8.14 |
>
> Las seis son **limitaciones duras de los controles clásicos de canvas**
> (sin sombras, sin transformaciones CSS, sin animación de fills, sin propagación
> de hover entre controles, sin `text-overflow`), no decisiones de diseño ni
> atajos del constructor. **Ninguna es funcional**: no se pierde ni un clic, ni
> un dato, ni un estado. Se enumeran para que nadie las descubra en la revisión
> visual y crea que la guía se construyó mal.

| # | Divergencia | Por qué | Impacto |
|---|---|---|---|
| 8.1 | **Sombra y `translateY(-2px)` del hover de la tarjeta**: se pierden | El botón clásico no tiene sombra ni transformaciones. Queda el cambio de borde a `ThemeAccent` (`HoverBorderColor`) | Bajo — el hover sigue siendo legible |
| 8.2 | **El borde de hover solo responde sobre la superficie libre de la tarjeta**, no cuando el puntero está sobre una etiqueta hija | La limitación real es una sola: **el hover no se propaga al control de abajo**. (Que las etiquetas clásicas "no tienen `HoverColor`" es FALSO — sí lo tienen, junto con `HoverFill` y `HoverBorderColor`; en CashHub hay apps que los usan.) Pero eso no ayuda acá: el borde que cambia vive en `btnHubTarjeta`, y ninguna propiedad de una etiqueta hija puede modificar el borde de otro control. Darle `HoverFill` a cada hija produciría un parche de color con la forma del label, peor que no hacer nada. El clic SÍ funciona en toda la tarjeta (`Select(Parent)` en cada hija) | Medio visual, **cero funcional** |
| 8.3 | **Skeleton: shimmer perdido + anatomía cambiada.** (a) El shimmer animado se pierde: quedan rectángulos `ThemeLineSoft` estáticos. (b) El fantasma calca la tarjeta REAL (glifo 44 + 3 barras) y **no** el `.miniCard` del diagrama (glifo 30 + 1 barra). (c) El bloque arranca en el mismo `Y` que la grilla real, **conservando el hueco de la píldora oculta** en vez de apretarse contra la fecha | (a) es animación CSS (`background-position`) y canvas clásico no anima fills. (b) el `.miniCard` era una maqueta a escala reducida dentro del HTML de presentación, no una pantalla: un skeleton que no calca lo que viene después miente sobre el layout. (c) sin el hueco, la píldora al aparecer empujaría toda la grilla hacia abajo de golpe | (a) Bajo · (b) y (c) **mejoras** sobre el mockup |
| 8.4 | **Alto de tarjeta 172 → 196** (`TemplateSize 214`) | Las descripciones reales del seed llegan a 88 caracteres = 3 líneas a 300px de ancho. El mockup usaba textos acortados y una card flex | Ninguno — mejora la fidelidad al DATO |
| 8.5 | **`translateX(3px)` de la flecha en hover**: se pierde | Sin transformaciones en canvas clásico | Nulo |
| 8.6 | **Banner de fail-open**: no existe en el mockup, se diseñó acá | Requisito de seguridad (§6.3). Usa tokens del mockup: `ThemeAccentSoft` de fondo, `ThemeAccentDark` de texto, barra `ThemeAccent` de 3px, `Icon.Warning` | Suma |
| 8.7 | **Estado vacío**: no existe en el mockup, se diseñó acá | Requisito funcional. Card `ThemeSurface`+`ThemeLine` radio 13, misma familia | Suma |
| 8.8 | **Píldora "Producción"** es un literal (`gvarEntorno` en `OnStart`) | La alternativa honesta es una variable de entorno de solución, que cuesta un datasource más y un `LookUp` en el arranque para dibujar 92px de texto. **En DEV va a decir "Producción" hasta que lo cambies a mano** | Bajo, pero es deuda: anotada |
| 8.9 | **Icono de escudo (`shield-check`) de la píldora de accesos → `Icon.Check`** | El set de iconos clásicos de canvas no tiene escudo. El check sostiene el mismo significado | Bajo |
| 8.10 | **Estado vacío sin la conjunción `&& !gvarAccessError`** | Sería código muerto: en fail-open `colAppData` = catálogo completo, así que `= 0` implica catálogo vacío, donde el mismo mensaje aplica (razonamiento completo en §4.7) | Ninguno |
| 8.11 | **Los 7 glifos del mockup SÍ se implementan, pero por DATO**: viajan en la columna de imagen `Icon` del catálogo (PNG 88×88 en `#C8102E`), no en el código de la app. Hasta que se siembren, cada tarjeta muestra un **respaldo de dos letras** derivado de `'App Key'` (RN, SR, RG, DN, EL, TR, CN) | Hardcodear los SVG violaría el requisito de tarjetas 100% dinámicas: un `Switch` por app key = una release de la canvas app por cada app nueva. La ruta correcta es el dato, y está especificada como **prerrequisito obligatorio en §2.8** (exportar los 7 motivos a PNG + agregar `sanic_icon` a `data/seed/30-appcatalogs.json` + extender el seeder, que **todavía no soporta columnas de imagen** — tarea de backend agendada, §2.8.3). El respaldo de dos letras existe porque una sola inicial colisiona: Recepción y Recuento dan ambas "R" (§4.6.1) | **ALTO visual mientras la columna esté vacía** — que es HOY. Nulo una vez sembrada. Beneficio de mantenimiento alto y permanente |
| 8.12 | **Tres elementos se ocultan bajo 900px de ancho**: la nota larga del footer, el nombre + correo del chip de usuario (colapsa a solo avatar) y la **píldora de entorno `pilHubEntorno`** | No entran en tablet vertical sin superponerse: chip (232) + píldora (92) + gaps + marca ya suman más que 768 | Bajo — la nota del footer vive también en el estado vacío, y nombre + correo se recuperan del `Tooltip` del avatar. El entorno **no** tiene recuperación en vertical: es informativo y ya está en la URL |
| 8.13 | **Saludo bicolor con Texto HTML** en vez de dos etiquetas | Canvas no tiene auto-ancho: dos etiquetas dejarían el nombre flotando a distancia fija (feo con "Ana" y roto con "Maximiliano") | Ninguno |
| 8.14 | **Descripciones largas se cortan a 3 líneas, sin elipsis** | Las etiquetas clásicas recortan sin `…` | Bajo |
| 8.15 | **Teléfonos fuera de alcance** (`MinScreenWidth 768`) | Abajo de ~600px la grilla de 300px y el header no cierran. Se declara como implementación futura, no como olvido | Declarado |
| 8.16 | **Los tokens de tema viven en `Set()` dentro de `OnStart`**, no en `App.Formulas` | Pedido explícito: un solo lugar de arranque. Costo: no existen en el lienzo hasta ejecutar `OnStart` (§1.2) | Bajo, con workaround documentado |
| 8.17 | **MUERE el SVG estático con contadores falsos** (`Image2`: "Procesadas: 128", "En revisión: 7", odómetros animados, luces de estado) | Eran **números inventados**, no datos: una app corporativa que miente en el arranque destruye la confianza en todo lo demás que muestra. Lo reemplaza un dato honesto y barato: cuántas apps ves de cuántas hay | Alto y buscado |
| 8.18 | **MUERE `Gallery1`** (layout de examinar con `Container1`/`Title1`/`Body1`/`NextArrow1`) | Se reemplaza por `galHubApps` con tarjetas propias. Los `OnSelect` con `Launch` repartidos en 4 hijos se unifican en el `OnSelect` de la galería | Alto y buscado |
| 8.19 | **`PressedFill = ThemeAccentSoft` en `btnHubTarjeta`**: estado agregado, sin fuente en el mockup | El HTML define `:hover` pero no `:active`. En tablet **no hay hover**: sin feedback de presionado el usuario toca, no ve nada mientras corre el `Launch()` y vuelve a tocar. Usa un token existente, no inventa paleta | Suma (y es reversible: igualarlo al `Fill` lo elimina) |
| 8.20 | **El breakpoint fijo de 1050px del mockup (`@media(max-width:1050px)` → 2 columnas) se reemplaza por una regla continua**: `WrapCount = Max(1, RoundDown(Width / 300, 0))` | Un breakpoint único parte el rango tablet↔monitor en dos escalones y desperdicia ancho: entre 1024 y 1050 el mockup daría **2** columnas (tarjetas de ~455px, ridículamente anchas) y la guía da **3** (~299px). El precio de la regla continua es que el ancho de tarjeta **no** es constante: el rango real es **282 – 431px** (§5.1), no un número fijo. 282 es el piso, y ocurre justo después de cada salto de columna | **Mejora** — más fiel al espíritu del diseño que a su media query |
| 8.21 | **Discontinuidad en 900px**: la tarjeta se achica de ~420 a ~395 cuando la ventana crece de 899 a 900 | El gutter salta de 20 a 46 en ese punto. Coincide con la reconfiguración del header (chip colapsado, píldora de entorno), así que el salto queda enmascarado. La alternativa —gutter continuo interpolado— cambia legibilidad de la fórmula por un caso que solo se ve redimensionando a mano (§5.1) | Nulo en uso real |

---

## 9. Checklist de salida

El constructor no cierra la tarea hasta que TODO esto esté tildado.

### 9.0 Prerrequisitos de datos (§2.8)

- [ ] 🚨 **`sanic_Icon` está en formato PNG, no JPEG** (§2.8.3 paso 0).
      **Hasta que esto se haga, los ítems de abajo NO son verificables**: podés
      subir el PNG y la plataforma te lo devuelve con fondo negro, así que un
      "sí, cargué el ícono" sin este paso es un falso positivo.
- [ ] Los 7 motivos del mockup están exportados a **PNG 88 × 88, motivo centrado
      de ~46 × 46, trazo 4 en `#C8102E`, fondo transparente, < 10 KB** cada uno.
- [ ] El seeder soporta columnas de imagen (base64) — **tarea de backend**, hoy
      pendiente.
- [ ] `data/seed/30-appcatalogs.json` tiene la clave `sanic_icon` en las 7 filas.
- [ ] **Las 7 filas del catálogo tienen `Icon` cargado en el entorno.**
      Verificalo en la tabla, no en la app.

> ⚠️ Este bloque es **prerrequisito de la prueba visual (§9.2)**, no de la
> construcción de la pantalla. Podés construir y probar todo el comportamiento
> con la columna vacía; lo que NO podés es dar por aprobada la fidelidad visual
> contra el mockup mientras las tarjetas muestren el respaldo de dos letras.
> Si el seeder todavía no está y hay que desbloquear la revisión, cargá las 7
> imágenes a mano desde el formulario del catálogo y anotalo.

### 9.1 Construcción

- [ ] La pantalla se llama `scrHub` y es la **única**; `Screen1`, `Gallery1`,
      `Image2`, `AccessBanner1` y `EmptyState1` ya no existen.
- [ ] Los **42 controles** del árbol §3 existen, con esos nombres exactos. Cero
      nombres autogenerados (`Label1`, `Rectangle3`…).
- [ ] Se construyó en **dos pases** (§0.9): primero los 42 controles insertados y
      renombrados en el orden del árbol, después las propiedades.
- [ ] `Configuración → Pantalla`: Escala para ajustar **OFF**, **Bloquear**
      relación de aspecto **OFF**, **Bloquear orientación OFF**.
      `App.MinScreenWidth = 768`, `App.MinScreenHeight = 640`.
- [ ] `App.OnStart` es exactamente el de §2.6 (tokens arriba, bloques 2–7
      intactos) y `App.OnError` el de §2.7.
- [ ] **Ningún Rectángulo clásico tiene `Radius`** (§0.3): las 9 superficies
      redondeadas (`shpHubUserChip`, `shpHubPildoraAccesos`, `shpHubSkTarjeta`,
      `shpHubSkGlifo`, `shpHubSkBarra1/2/3`, `shpHubGlifo`, `shpHubVacio`) son
      **Etiquetas clásicas con `Text: ""`**.
- [ ] **Todo Botón y toda Etiqueta tienen `Font = Font.'Segoe UI'`** (§2.5). En
      particular, `shpHubMarca` muestra el glifo **`₵`** y no una caja vacía — si
      ves tofu, quedó en Open Sans.
- [ ] Los 4 botones tienen `BorderThickness` pisado (`0`, salvo `btnHubTarjeta`
      que va en `1`): el default del tema es **2**.
- [ ] Las etiquetas usadas como superficie tienen `Padding*` en `0` y `Wrap` en
      `false` (§2.5).
- [ ] **App Checker en cero errores.** Ningún ícono de delegación (banda azul)
      en `galHubApps` ni en `galHubSkeleton` — sus `Items` son colecciones.
- [ ] Ninguna fórmula **de pantalla** menciona `'CH-Tbl-AppCatalogs'`,
      `'CH-Tbl-AppCatalogAccesses'` ni `Office365Groups` (todo viene de `OnStart`).
- [ ] Ninguna fórmula referencia `'AAD Group Id'` junto a `'CH-Tbl-AppCatalogs'`.
      El **único** uso permitido en toda la app es `row.'AAD Group Id'` sobre
      `'CH-Tbl-AppCatalogAccesses'`, en el bloque 3 de `App.OnStart` (§6.4).
- [ ] Búsqueda de `hh:` en las fórmulas: **cero resultados**.

### 9.2 Comportamiento (prueba de humo)

- [ ] **Usuario con acceso total**: ve las 7 tarjetas ordenadas por `'Order'`
      (Recepción, Supervisor, Recuento, Digitación, Ingesta de correos,
      Trazabilidad, Admin. de clientes) y la píldora **verde** "7 de 7".
- [ ] **Usuario con acceso limitado** (cajero, 2 grupos): misma pantalla, solo
      sus tarjetas, píldora **azul** "2 de 7". No aparece ninguna pantalla ni
      mensaje distinto.
- [ ] **Usuario sin apps**: card de estado vacío con "No tenés aplicaciones
      asignadas" + la bajada del supervisor. Sin píldora, sin grilla, sin error.
- [ ] **Graph caído (simulado)**: apagá temporalmente la conexión del conector
      `Office365Groups` (o poné un GUID de grupo inválido en la tabla de accesos
      para forzar el `IfError`) → banner rojo suave visible, píldora **oculta**,
      se ven todas las apps, **no** redirige aunque quede una sola.
- [ ] **Redirect de app única**: usuario con acceso a exactamente 1 app y Graph
      OK → aterriza directo en esa app sin ver el hub.
- [ ] **Clic en cualquier punto de la tarjeta** abre la app: sobre el título,
      sobre la descripción, sobre el glifo, sobre "Abrir aplicación", sobre la
      flecha y sobre el espacio vacío. Los seis casos, uno por uno.
- [ ] **Skeleton**: al abrir con red lenta (throttling "Slow 3G" en DevTools) se
      ven las tarjetas fantasma y "Verificando tus accesos…", y NO parpadea la
      lista completa antes de filtrar.
- [ ] **Con §9.0 cumplido**: las 7 tarjetas muestran su glifo del mockup
      (camión, balanza, monedas, teclado, sobre, eslabones, persona+) a 44 × 44
      sobre el tile rosado, **con el fondo rosado visible alrededor del trazo**
      (si ves un cuadrado negro, el atributo quedó en JPEG: §2.8.3 paso 0).
- [ ] **Prueba del respaldo**: vaciá el `Icon` de UNA sola app en la tabla → esa
      tarjeta pasa al identificador de dos letras (`reception` → **RN**) **sin
      republicar la app**, y las otras seis siguen mostrando su imagen. Volvé a
      cargarla y verificá que vuelve la imagen.
- [ ] Con la columna vacía en las 7, los respaldos son **RN, SR, RG, DN, EL, TR,
      CN** — siete valores distintos, sin dos tarjetas vecinas con el mismo glifo.

### 9.3 Responsive

- [ ] **1366 × 768**: 4 columnas, márgenes de 46, chip completo, píldora de
      entorno visible, footer con las dos leyendas. Sin scroll horizontal.
- [ ] **1100 × 800** (ventana de trabajo típica): **3 columnas** de ~324px.
- [ ] **1024 × 768** (tablet horizontal): **3 columnas** de ~299px, todo legible.
- [ ] **920 × 800** (el caso que sorprende): **2 columnas** de ~400px, header ya
      en modo ancho (chip completo + píldora de entorno). Es correcto — el umbral
      de la 3ª columna está en 974, no en 900 (§5.1). Si acá ves 3 columnas, la
      fórmula de `WrapCount` o la del gutter quedaron mal.
- [ ] **768 × 1024** (tablet vertical): 2 columnas, márgenes de 20, chip
      colapsado a avatar, sin píldora de entorno, footer solo con la versión.
      **Sin scroll horizontal.**
- [ ] Redimensionar la ventana del navegador **en vivo** de ancha a angosta: las
      columnas se recalculan sin recargar (si algo queda clavado, alguien guardó
      una medida en una variable — §2.1).
- [ ] Con el **banner de fail-open visible**, todo el cuerpo baja 44px y la
      grilla sigue entrando arriba del footer.
- [ ] Con más apps de las que entran en pantalla: la grilla scrollea vertical
      dentro de su alto, el header y el footer se quedan quietos.
