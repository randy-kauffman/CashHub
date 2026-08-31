# Análisis de los roles de seguridad — CashHub

> Para qué sirve cada rol y si tiene los privilegios que su función necesita.
> Verificado el 2026-08-31 contra DEV (`org36e60d9d`), commit `f4d5d04`.
>
> **Método**: se cruzó la matriz de privilegios de cada `Roles/*.xml` contra las tablas que
> realmente consume su app — datasources declarados (`pkgs/TableDefinitions/*.json`), dot-walks en
> Power Fx, y `entityName` de los cloud flows. Se verificó lectura **y** escritura.

## Los 11 roles

| Rol | Para qué sirve | Consumidor | Tablas custom |
|---|---|---|---|
| `CH-SR-Hub` | Rol base: solo abrir el Hub | `CH-App-Hub` | 2 |
| `CH-SR-Cashier` | Cajero de recuento: cuenta bolsas, cheques y efectivo | `CH-App-Recounting` | 21 |
| `CH-SR-CountingSupervisor` | Supervisor: aprueba, reasigna y cierra sesiones de recuento | `CH-App-Supervisor` | 20 |
| `CH-SR-Receptionist` | Oficial de bóveda: recibe tulas en esclusa | `CH-App-Reception` | 11 |
| `CH-SR-Digitizer` | Digitador: arma cortes y transacciones al core | `CH-App-Digitization` | 9 |
| `CH-SR-DepositsTracker` | Visor de trazabilidad, **solo lectura** | `CH-App-DepositsTracker` | 22 |
| `CH-SR-ClientAdminApprover` | Aprueba/rechaza RFC de datos de cliente | `CH-App-ClientAdministration` | 11 |
| `CH-SR-ClientAdminContributor` | Crea RFC propios de datos de cliente | `CH-App-ClientAdministration` | 11 |
| `CH-SR-EmailOperationsConsole` | Opera la bandeja de ingesta de correos | `CH-App-IngestionConsole` | 13 |
| `CH-SR-EmailIngestionFlow` | Identidad de servicio de los flujos de ingesta | 4 cloud flows | 3 |
| `CH-SR-ConfigAdmin` | Administra catálogos y parámetros; **rol de soporte** | `CH-App-Configuration` | 11 |

`CH-SR-DepositsTracker` es el único con **cero privilegios de escritura** sobre las 22 tablas — es
un visor por diseño, y el diseño se cumple.

`CH-SR-ConfigAdmin` es además el **rol de soporte y diagnóstico**: desde el 2026-08-31 es el único
portador de `prvReadPluginTraceLog`, heredado del eliminado `CH-SR-OpsAdmin`.

---

## Brechas encontradas: 2 — ✅ AMBAS CORREGIDAS el 2026-08-31

### 1. `CH-SR-Receptionist` no puede leer `CH-Tbl-Client`

Tiene `prvAppendTosanic_CHTblClient` (Global) y **ningún `prvRead`**. Puede asociar registros al
cliente, pero no leerlo.

La app de Recepción lo necesita — `scrCaptura.fx.yaml:433`:

```powerfx
ctxPlanillaTulasEmpCap: Coalesce(cmbEmpresaCap.Selected.Client.Name, "Empresa sin seleccionar")
```

Es un dot-walk `ClientBranch → Client → Name`. El rol sí lee `ClientBranch`, pero al saltar a
`Client` no tiene permiso: el dot-walk devuelve vacío y el `Coalesce` cae al literal.

> **Síntoma**: la planilla de tulas muestra siempre *"Empresa sin seleccionar"* en vez del nombre
> del cliente, para todo usuario que tenga solo este rol. **No hay error ni pantalla rota** — por
> eso pudo pasar desapercibido.

**Hay precedente en el repo.** `provision-tracker-role.sh` documenta exactamente esta decisión para
el rol de Tracker:

> *"`prvReadsanic_CHTblClient` SÍ va: la guía 03 resuelve el cliente con
> `Coalesce(ReceptionID.RemitentClient.Name, ReceptionID.RemitentName, "—")`… **Sin el privilegio la
> pantalla no rompe pero degrada al texto plano.** Es lectura de nombre: Global es aceptable."*

El mismo razonamiento aplica acá y no se aplicó. **✅ APLICADO**: se agrego `prvReadsanic_CHTblClient` nivel Global a `CH-SR-Receptionist`
con `scripts/fix-roles-seguridad.py`.

### 2. `CH-SR-EmailIngestionFlow` no puede leer `CH-Tbl-ValidationRuns`

El rol tiene privilegios sobre 3 tablas: `ReceivedEmails`, `EmailsAttachments` y `SystemHeartbeat`.

Pero `CH-Flow-MailboxSweep` —que está **ACTIVO**— consulta una cuarta:

```
"entityName": "sanic_chtblvalidationrunses",
"operationId": "ListRecords",
"$filter": "sanic_sourcerecordid eq '...'"
```

`ListRecords` es lectura, y el rol tiene **cero** privilegios sobre esa tabla.

**✅ APLICADO**: se agrego `prvReadsanic_CHTblValidationRuns` nivel Global con
`scripts/fix-roles-seguridad.py`.

> **Matiz importante**: un cloud flow corre bajo la identidad del dueño de su conexión, no bajo un
> rol de seguridad. Si esa cuenta de servicio tiene además un rol amplio, la brecha no se
> manifiesta hoy. Pero el rol que lleva el nombre de estos flujos no alcanza para correrlos, y eso
> es deuda: el día que se restrinja la cuenta, el barrido falla.

---

## Renombre: `CH-Role` → `CH-SR-Hub` ✅ APLICADO

El rol se llamaba `CH-Role`, sin el sufijo `CH-SR-` del resto — era la única excepción de
nomenclatura entre los 11. Y el nombre no decía qué hacía.

Verificado antes de renombrar: sus únicos privilegios sobre tablas custom son **Read sobre
`AppCatalog` y `AppCatalogAccess`** — exactamente lo necesario para abrir el Hub y ver el catálogo.
El nombre `CH-SR-Hub` describe eso.

**Estaba en uso**: 3 usuarios y 2 equipos aprobadores (`CashHub - Banca de Empresas -Approver` y
`CashHub Aprobadores - sanic_chtblclientbranch`).

> El renombre **conserva el `roleid`** (`1ca520f9-c294-f011-b41b-6045bda7f41e`), así que las
> asignaciones sobreviven. Verificado después de aplicar: 3 usuarios y 2 equipos, los mismos.
> Mismo mecanismo que `CH-SR-Counter` → `CH-SR-Cashier`.

Se actualizaron las referencias por nombre en `provision-appcatalog-access.sh`,
`apply-supervisor-role-remediation.sh`, `cleanup-roles-vacios.py`, `docs/soluciones/01-core.md` y
`docs/soluciones/00-vision-general.md`. Sin ese cambio, esos scripts habrían fallado al no
encontrar el rol.

---

## Falsos positivos descartados

**`CH-Tbl-Parameter` aparecía como "sin permiso" en dos roles.** No es una brecha: la tabla es
`OwnershipTypeMask = UnOwned`, así que **Dataverse no genera privilegios para ella** y ningún rol
del sistema la referencia. Es legible por cualquiera con acceso al ambiente.

**La consola declara `CH-Tbl-Clients` como datasource.** No la usa: cero referencias en los tres
árboles de fuente y ningún dot-walk que llegue a `Client`. Es una declaración huérfana, no una
necesidad de permiso.

---

## Lo que está bien

- **Lectura**: los 9 roles con app asociada leen todas las tablas que su app consume, salvo la
  brecha 1.
- **Escritura**: los 7 roles que escriben tienen `Create`/`Write` sobre **todas** las tablas donde
  su app hace `Patch`, `Collect` o `Remove`. Cero brechas.
- **`CH-SR-ConfigAdmin`**: CRUD completo (`Create`/`Read`/`Write`/`Delete`/`Append`/`AppendTo`/
  `Assign`/`Share`, todos Global) sobre las **11 tablas asegurables** de su app.
- **`CH-SR-DepositsTracker`**: 22 tablas, **todas solo lectura**. El visor es un visor.

---

## Nota de método

El primer cruce automatizado reportó *"todas las tablas son legibles"* para los 11 roles — y era
**falso**. El script resolvía el logical name desde una clave `EntityMetadata` que viene `None` en
los `pkgs/TableDefinitions/*.json`, así que saltaba en silencio cada tabla y la lista de faltantes
quedaba vacía por construcción.

Se detectó porque **contradecía la matriz de privilegios** hecha a mano, que ya mostraba
`Receptionist → Client: solo AppendTo`.

La clave correcta es **`LocalReferenceDSJson.logicalName`**. Y la lección quedó en el script: si una
tabla no se puede resolver, **se reporta**, nunca se salta. Un chequeo que no puede fallar es un
chequeo que no verifica nada.
