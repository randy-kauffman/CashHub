# Roles y seguridad — CashHubCore

Fuente: `solutions/unpacked/core/Roles/*.xml` (6 Security Roles), verificado con
`rg 'name="prv...sanic_'` sobre cada archivo. Todos los roles son `IsAutoAssigned=0`.
Todos los privilegios sobre tablas custom están a nivel **Global** (organización
completa) — no hay restricción a nivel User/Business Unit en ninguna tabla de negocio.

## 1. Los 6 roles

| Rol | Rol físico esperado | App asociada | Privilegios sobre tablas custom |
|---|---|---|---|
| `CH-Role` | — (rol base/plantilla) | — | Solo `prvReadsanic_CHTblAppCatalog` (Global). Confirmado: es el único privilegio custom en el XML. |
| `CH-SR-AnalyticsRead` | Auditoría/consulta → app Deposits Tracker | Deposits Tracker | **Cero** privilegios sobre cualquier tabla custom. Confirmado: `rg` sobre `CH-SR-AnalyticsRead.xml` no devuelve ningún `prv*sanic_*`, ni siquiera `prvReadsanic_CHTblAppCatalog`. |
| `CH-SR-Counter` | Cajero de recuento → app Recounting | Recounting | 65 privilegios confirmados. CRUD completo sobre `Bags`, `Checks`, `CountedMoneyDetails`, `DepositsRecounting`, `RecountingSession`, `Transactions`, `ApprovalRequest`; lectura/append sobre `Declaration`, `DeclaredBags`, `DeclaredChecks`, `DeclaredDeposits`, `CountingDesk`, `Locations`, `MoneyDenomination`, `ReceptionBatch`, `Receptions`, `TransportCompany`. |
| `CH-SR-CountingSupervisor` | Supervisor de recuento → app Supervisor | Supervisor | Lectura amplia (mismas 17 tablas que Counter); Create limitado a `Bags`/`Checks`/`Transactions`; Write sobre `ApprovalRequest`/`Bags`/`Checks`/`ReceptionBatch`/`Receptions`/`RecountingSession`/`Transactions`; **Assign** sobre `ApprovalRequest`. |
| `CH-SR-OpsAdmin` | Administración de operaciones | — (sin app mapeada) | **Cero** privilegios sobre cualquier tabla custom. Confirmado: mismo resultado vacío que `AnalyticsRead` al correr `rg` sobre el XML. |
| `CH-SR-Receptionist` | Oficial de esclusa/bóveda → app Reception | Reception | CRUD sobre `Bags`/`ReceptionBatch`/`Receptions`/`ApprovalRequest`/`TransportCompany`; append/read sobre `DeclaredBags`/`Locations`. Ningún privilegio sobre tablas de conteo. |

**3 de 6 roles no otorgan acceso a ninguna tabla de negocio** (`CH-Role`,
`CH-SR-AnalyticsRead`, `CH-SR-OpsAdmin`). Un rol llamado "lectura de analítica" que no
puede leer ninguna tabla custom, tal como está exportado, no es funcional para su
propósito declarado.

## 2. Matriz Rol × Entidad

Solo entidades con algún privilegio en algún rol. C=Create R=Read W=Write D=Delete
A=Append T=AppendTo S=Assign; todo nivel Global.

| Entidad | CH-Role | AnalyticsRead | Counter | CountingSupervisor | OpsAdmin | Receptionist |
|---|---|---|---|---|---|---|
| AppCatalog | R | — | R | R | — | R |
| ApprovalRequest | — | — | C,R,W | A,T,S,R,W | — | C,R,W |
| Bags | — | — | A,T,R,W | C,R,W | — | A,T,C,D,R,W |
| Checks | — | — | A,T,C,D,R,W | C,R,W | — | — |
| CountedMoneyDetails | — | — | A,T,C,D,R,W | — | — | — |
| CountingDesk | — | — | A,T,R | R | — | — |
| Declaration | — | — | A,T,R | R | — | — |
| DeclaredBags | — | — | A,T,R | R | — | A,T,R |
| DeclaredChecks | — | — | A,T,R | R | — | — |
| DeclaredDeposits | — | — | A,T,R | R | — | — |
| DepositsRecounting | — | — | A,T,C,D,R,W | R | — | — |
| Locations | — | — | A,T,R | R | — | A,T,R |
| MoneyDenomination | — | — | A,T,R | R | — | — |
| ReceptionBatch | — | — | A,T,R | R,W | — | A,T,C,R,W |
| Receptions | — | — | A,T,R | R,W | — | A,T,C,D,R,W |
| RecountingSession | — | — | A,T,C,D,R,W | D,R,W | — | — |
| Transactions | — | — | A,T,C,R,W | C,R,W | — | — |
| TransportCompany | — | — | R | R | — | A,T,R,W |

**Rareza confirmada**: el proceso narrativo dice que "el supervisor asigna bolsas a
cajeros", pero `CH-SR-CountingSupervisor` **no tiene Create sobre `RecountingSession`**
(solo Delete) — es `CH-SR-Counter` quien puede crearla. La creación técnica de la
sesión de conteo queda en manos del cajero, no del supervisor.

## 3. Entidades SIN ningún privilegio en ningún rol (18 de 38)

Ninguno de los 6 Security Roles otorga ningún privilegio sobre:

- **Todo el dominio correo/validación**: `ReceivedEmails`, `EmailsAttachments`,
  `XMLReading`, `XMLReadingDetail`, `ValidationResults`, `ValidationRuns`,
  `ValidationRules`.
- **Todo el módulo de cambio de datos del cliente**: `ClientDataChangePermission`,
  `ClientDataChangeRequest`, `ClientDataChangeRequestLine`,
  `ClientDataChangesAuditTrail`, `ClientTableLogicalFieldName`.
- **Maestro de Clientes**: `Client`, `ClientBranch`, `ClientEmail`,
  `ClientDepositHistory`.
- `TransactionBatches`.
- Las estándar `Account`/`SystemUser`.

Dentro de los archivos de esta solución, **ningún usuario humano con estos 6 roles**
puede crear, leer ni editar un Cliente, un correo recibido, una regla de validación o
una solicitud de cambio de datos. Ese acceso, si existe, se resuelve fuera de este
paquete (System Administrator implícito, un Application User/Service Principal de un
flujo con permisos elevados, u otra solución no incluida acá).

## 4. Grupos AAD (`sanic_CHEVAADGroup_*`) y dónde se usan realmente

6 variables de entorno String, todas sin `defaultvalue` (se completan por ambiente):
`sanic_CHEVAADGroup_Admin`, `_ClientAdmin`, `_Counting`, `_EmailValidation`,
`_Reception`, `_Supervisor`. Ver detalle en
[variables-de-entorno.md](variables-de-entorno.md).

**Hallazgo verificado contra el código de las apps** (`solutions/unpacked/apps/CanvasApps/src/`):
ninguna de las 8 canvas apps hace referencia textual a la cadena
`sanic_CHEVAADGroup_*` en su Power Fx. El mecanismo de pertenencia a grupo que sí
existe está implementado por **GUID crudo**, no por el nombre de la variable de
entorno:

| App | Mecanismo de "rol" en runtime | Usa `sanic_CHEVAADGroup_*` |
|---|---|---|
| Hub (`sanic_chapphub_2038c`) | `ClearCollect(colAppData, Filter('CH-Tbl-AppCatalogs', ... Office365Groups.ListGroupMembers(Trim('AAD Group Id'))...))` — filtra por el GUID guardado en cada fila de `sanic_CHTblAppCatalog`. **Bug adicional**: la galería principal (`Gallery1.Items`) apunta a la tabla completa `'CH-Tbl-AppCatalogs'`, no a `colAppData` — el filtro de grupo se calcula pero no se aplica a lo que ve el usuario. | No |
| Client Administration (`sanic_chappclientadministration_fd97f`) | `ForAll('CH-Tbl-ClientDataChangePermissions' As Permission, If(!IsBlank(LookUp(Office365Groups.ListGroupMembers(Permission.AADGroupID).value, ...)), Collect(colClientDataPermissions, Permission)))` — por GUID en `ClientDataChangePermission.sanic_aadgroupid`, segmentado por `sanic_type` (Contributor/Approver). | No |
| Reception, Recounting, Supervisor, Digitization, Deposits Tracker, Email Validation Console | Identidad resuelta únicamente vía `Set(gvarUser, LookUp(Users, 'Primary Email' = User().Email))` contra `systemuser`. **Ninguna verifica pertenencia a grupo ni rol de seguridad dentro de Power Fx.** | No |

Conclusión: las 6 variables `sanic_CHEVAADGroup_*` no aparecen citadas por su nombre
de esquema en ningún archivo de app ni de flujo revisado — el patrón real de control
de acceso usa GUIDs almacenados en tablas de datos (`AppCatalog.sanic_aadgroupid`,
`ClientDataChangePermission.sanic_aadgroupid`), probablemente poblados a partir del
valor de esas variables de entorno en tiempo de configuración, pero sin una lectura
programática verificable de la variable en sí dentro de este código.

## 5. Brechas de seguridad conocidas

### 5.1 Apps sin verificación de rol dentro de Power Fx

Las 4 apps operativas principales (Reception, Recounting, Supervisor, Email
Validation Console) y Digitization/Deposits Tracker resuelven identidad con
`LookUp(Users, PrimaryEmail=User().Email)` y **no comprueban** si ese usuario
pertenece al grupo o rol esperado antes de mostrar pantallas o habilitar acciones. El
control de acceso, tal como está escrito, depende exclusivamente de:
- a quién se comparte la app en Power Platform, y
- los privilegios del Security Role Dataverse subyacente del usuario (con las
  brechas de la sección 3 de este documento).

Fuente: confirmado en `03-apps-operativas.md` §4.1 y `04-apps-administrativas.md` §9.1
del análisis original, contra el código real de cada app.

### 5.2 Auto-aprobación del cajero en su propia mesa (Recounting)

En `sanic_chapprecounting_54962` (`Screen1.fx.yaml`), al confirmar la creación de una
`RecountingSession`: si la mesa elegida coincide con la mesa asignada al cajero
(`varAssignedDesk = LookUp('CH-Tbl-CountingDesks', Cajero.User = gvarUser.User)`), el
código ejecuta `varIsOwnDesk = true` y hace `Patch(Confirmed: true)` directo sobre la
sesión, creando el `ApprovalRequest` correspondiente **ya en estado `Aprobado`** — sin
intervención real de un supervisor. El propio código tiene el comentario
`// ✅ ESTE ES EL FIX DEFINITIVO` en ese punto. Esto es una brecha de segregación de
funciones: el cajero puede auto-aprobar la apertura de su propia sesión de recuento.

### 5.3 Resumen de las 18 tablas sin privilegios

Ver sección 3 más arriba — cubre por completo los dominios de correo/validación y
cambio de datos del cliente, más el maestro de Clientes y `TransactionBatches`.

### 5.4 Roles vacíos asignables

`CH-SR-AnalyticsRead` y `CH-SR-OpsAdmin` existen como roles nombrados y asignables
pero no otorgan ningún privilegio funcional — si se asignan a un usuario real
esperando que le den acceso de lectura/administración, el usuario no podrá operar
ninguna tabla de negocio de CashHub.

## 6. Roles vs. apps (tabla de referencia rápida)

| Rol físico | App | Rol de seguridad esperado |
|---|---|---|
| Oficial de esclusa/bóveda | Reception | `CH-SR-Receptionist` |
| Cajero de recuento | Recounting | `CH-SR-Counter` |
| Supervisor de recuento | Supervisor | `CH-SR-CountingSupervisor` |
| Digitador (transición a IBS Branch) | Digitization | — (sin rol mapeado en las 6 roles existentes) |
| Analista de correos | Email Validation Console | — (sin rol mapeado) |
| Auditoría/consulta | Deposits Tracker | `CH-SR-AnalyticsRead` (hoy vacío, ver §5.4) |
| Administración de clientes | Client Administration (canvas + model-driven) | Grupos AAD Contributor/Approver por GUID, no Security Role |
| Todos | Hub | Filtro por `AppCatalog.sanic_aadgroupid`, con el bug de §4 |

Ver también: [variables-de-entorno.md](variables-de-entorno.md) para el detalle de
cada `sanic_CHEVAADGroup_*`, [modelo-de-datos.md](modelo-de-datos.md) para el esquema
completo de las tablas citadas.
