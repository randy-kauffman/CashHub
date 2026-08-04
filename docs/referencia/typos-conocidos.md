# Typos conocidos en LogicalNames — CashHubCore

Estos son errores de tipeo grabados en el **`LogicalName`** interno de un campo (o en
un label de choice consumido como literal en Power Fx). En Dataverse el `LogicalName`
de un campo **no se puede renombrar** una vez creado — la única forma de corregirlo es
eliminar la columna y recrearla, con toda la migración de datos y de referencias que
eso implica. Por eso se documentan acá como "conocidos y aceptados", no como bugs a
arreglar en un refactor menor.

Todos confirmados con `rg` contra los archivos reales bajo `solutions/unpacked/`.

## 1. `AlternetKey` (por "Alternate Key")

- **Tabla**: `sanic_CHTblClient`
- **Campos**: `sanic_AlternetKey_CIFBCO`, `sanic_AlternetKey_CIFCOM`
- **Ruta**: `solutions/unpacked/core/Entities/sanic_CHTblClient/Entity.xml`
- **Impacto**: cosmético/de mantenimiento. Son las claves alternas del Cliente hacia
  sistemas externos (CIFBCO/CIFCOM); el typo está en el nombre de la clave alterna
  (`AlternateKey` de Dataverse), no en el campo de negocio (`sanic_cifbco`/
  `sanic_cifcom` en sí están bien escritos). No afecta lógica, solo aparece en el
  nombre técnico de la clave si alguien la referencia por API.

## 2. `ReasonForForcedFlosure` (por "Forced Closure")

- **Tabla**: `sanic_CHTblRecountingSession`
- **Campo**: `sanic_reasonforforcedflosure`
- **Ruta**: `solutions/unpacked/core/Entities/sanic_CHTblRecountingSession/Entity.xml`
- **Impacto**: cosmético. Guarda el motivo de un cierre forzado de sesión de conteo
  (`sanic_sessionforciblyclosed=true`). No se detectó ningún filtro o fórmula que
  dependa del nombre exacto del campo más allá de leer/escribir su valor por lookup
  de esquema — el typo es visible solo para quien inspeccione el modelo o escriba
  Power Fx/Web API contra esta columna directamente.

## 3. `OtherLocaCheckAmount` (por "Other Local Check Amount", falta la "l")

- **Tabla**: `sanic_CHTblDepositsRecounting`
- **Campo**: `sanic_otherlocacheckamount`
- **Ruta**: `solutions/unpacked/core/Entities/sanic_CHTblDepositsRecounting/Entity.xml`
  (y su fórmula calculada en
  `solutions/unpacked/core/Entities/sanic_CHTblDepositsRecounting/Formulas/sanic_chtbldepositsrecounting-FormulaDefinitions.yaml`)
- **Impacto**: bajo pero **confirmado como consumido correctamente** por la app
  Digitization — la fórmula de layout de carga masiva usa literalmente
  `RelatedDepositRecounting.OtherLocaCheckAmount` (sin "l") porque así se llama la
  columna real en Dataverse. Es decir, el typo está en el esquema y las apps que lo
  consumen lo respetan correctamente; el riesgo es solo para quien escriba una nueva
  integración asumiendo el nombre "correcto" (`OtherLocalCheckAmount`) y falle por
  columna inexistente. Nótese que la tabla hermana `sanic_CHTblDeclaredDeposits` sí
  tiene el campo equivalente bien escrito (`sanic_otherlocalcheckamount`) y
  `sanic_CHTblTransactions` también lo tiene bien escrito — la inconsistencia de
  nombre entre 3 tablas que representan el mismo concepto (declarado / recontado /
  transacción final) es la parte relevante para cualquier integración cruzada.

## 4. `opeationtype` (por "OperationType", falta la "r")

- **Tabla**: `sanic_CHTblClientDataChangesAuditTrail`
- **Campo**: `sanic_opeationtype`
- **Ruta**: `solutions/unpacked/core/Entities/sanic_CHTblClientDataChangesAuditTrail/Entity.xml`
- **Impacto**: cosmético. Optionset local con valores Create/Update/Deactivate/
  Activate para clasificar la fila de auditoría. No se detectó lógica de flujo que
  dependa del nombre de esquema fuera de las acciones de `Createclientdata`/
  `Applyclientdatachanges`/`Activaterow`/`Deactivaterow`, que lo escriben por
  referencia de campo (no por texto), así que el typo no rompe nada en tiempo de
  ejecución.

## 5. `fileextention` (por "FileExtension")

- **Tabla**: `sanic_CHTblEmailsAttachments`
- **Campo**: `sanic_fileextention`
- **Ruta**: `solutions/unpacked/core/Entities/sanic_CHTblEmailsAttachments/Entity.xml`
  (también aparece en el FormXml del formulario principal de la tabla,
  `solutions/unpacked/core/Entities/sanic_CHTblEmailsAttachments/FormXml/main/{404d1964-2d87-45a7-9a58-c2f676a53415}.xml`)
- **Impacto**: cosmético, pero relevante porque es el campo que debería alimentar
  `VR04` (validación de extensión de adjunto) — y `VR04` está bugueado por otra razón
  (ambas ramas responden Pass, ver [conexiones-y-flujos.md](conexiones-y-flujos.md)
  §5), así que el typo en el nombre del campo no es hoy el problema práctico de esa
  regla, pero sí lo sería si se corrige el bug de `VR04` sin revisar contra qué
  columna real se compara.

## 6. `esclusa` / `exclusa` — inconsistencia real, verificada con corrección respecto al análisis previo

> **Nota de verificación**: el análisis previo ([`03-apps-operativas.md`](../analysis/03-apps-operativas.md)
> hallazgo 2.8.1) afirmaba que el valor **correcto** era `'Recibida en exclusa'` (con
> "x") y que la app Recounting tenía el error al usar `'Recibida en esclusa'` (con
> "s"). Al verificar directamente contra `Entity.xml` (la fuente de verdad del
> esquema Dataverse, no la copia cacheada por app) **se encontró exactamente lo
> contrario**. Se documenta acá el resultado verificado.

- **Definición viva del choice** (`solutions/unpacked/core/Entities/sanic_CHTblBags/Entity.xml`,
  optionset `sanic_chtblbags_sanic_bagstatus`): valor `159460000` = **`'Recibida en
  esclusa'`** (con "s") y valor `159460005` = **`'Pendiente de revision por
  supervisor - esclusa'`** (también con "s"). "Esclusa" es además la grafía
  correcta en español para el concepto de negocio (cámara de seguridad/compuerta de
  acceso a bóveda) y la que usa el resto de la documentación narrativa del sistema
  ("recepción física en esclusa", "oficial de esclusa/bóveda").
- **App Recounting** (`solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/Screen1.fx.yaml`,
  6 ocurrencias) y su copia cacheada del esquema
  (`.../sanic_chapprecounting_54962/pkgs/TableDefinitions/CH-Tbl-Bags.json`, 4
  ocurrencias) usan **`'Recibida en esclusa'`** (con "s") — **coincide con el
  esquema vivo**.
- **Apps Reception, Supervisor, Deposits Tracker y Email Validation Console**: tanto
  su código (`Screen AdministracionDeLotes.fx.yaml` en Reception, `Screen
  BagLocation.fx.yaml` en Supervisor) como su copia cacheada del esquema
  (`pkgs/TableDefinitions/CH-Tbl-Bags.json`, 4 ocurrencias en cada una de las 4 apps)
  usan **`'Recibida en exclusa'`** (con "x") — **no coincide con el esquema vivo
  actual** (no existe ningún option con esa grafía en el `Entity.xml` de esta
  exportación).
- **Qué significa esto en la práctica**: cada Canvas App de Power Platform guarda
  junto a su código fuente una copia local cacheada del esquema de las tablas que usa
  (`pkgs/TableDefinitions/`), tomada la última vez que la app se sincronizó/publicó
  contra Dataverse. Las referencias de choice en Power Fx (`'BagStatus (CH-Tbl-Bags)'.'Recibida en esclusa'`)
  se resuelven contra esa copia local al empaquetar la app, no contra el `Entity.xml`
  en tiempo real. El hecho de que **4 de las 5 apps que referencian este choice
  tengan una copia local con la grafía "exclusa"**, mientras que el `Entity.xml`
  vigente en esta exportación (y la copia local de Recounting) tienen "esclusa",
  indica que esas 4 apps no se han vuelto a sincronizar con Dataverse desde antes de
  que la etiqueta se corrigiera (o cambiara) a "esclusa" — o que Recounting fue la
  única republicada después de ese cambio.
- **Impacto funcional**: no se puede confirmar con certeza solo desde los archivos
  estáticos si esto rompe un `Filter` en tiempo de ejecución hoy (depende de si
  Power Apps Studio revalida el literal contra el esquema vivo al reabrir/republicar
  cada app, algo no verificable sin el entorno real). Lo que sí queda **confirmado
  por archivo**: hay una inconsistencia real de sincronización de esquema entre 4 de
  las 5 apps y el `Entity.xml` de esta solución, y el sentido de la inconsistencia es
  el inverso al que reportaba el análisis previo — conviene re-verificar contra el
  entorno Dataverse real antes de decidir qué app corregir.

## 2. Resumen de severidad

| Typo | Tabla.Campo | Severidad | Corregible sin recrear el campo |
|---|---|---|---|
| `AlternetKey` | Client (clave alterna) | Cosmética | No |
| `ReasonForForcedFlosure` | RecountingSession | Cosmética | No |
| `OtherLocaCheckAmount` | DepositsRecounting | Baja (inconsistencia de nombre entre 3 tablas del mismo concepto) | No |
| `opeationtype` | ClientDataChangesAuditTrail | Cosmética | No |
| `fileextention` | EmailsAttachments | Cosmética | No |
| `esclusa`/`exclusa` | Choice label de `Bags.sanic_bagstatus`; esquema vivo = "esclusa", 4 de 5 apps consumidoras cacheadas con "exclusa" | Verificada, dirección **corregida respecto al análisis previo** (ver detalle arriba) — impacto en runtime no confirmable solo con archivos estáticos | El esquema no necesita corrección (ya usa la grafía correcta); las 4 apps desincronizadas necesitarían resincronizar su copia local del esquema |

Ver también: [modelo-de-datos.md](modelo-de-datos.md) para el esquema completo de
cada tabla citada, [catalogo-de-choices.md](catalogo-de-choices.md) para la máquina de
estados de `BagStatus` afectada por el caso 6.
