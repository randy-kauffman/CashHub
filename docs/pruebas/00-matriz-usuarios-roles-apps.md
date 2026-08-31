# Matriz de usuarios, roles y apps — preparación de pruebas E2E

> Estado del ambiente DEV (`org36e60d9d`) al 2026-08-31. **Documento de preparación de pruebas**:
> sirve para saber, antes de correr un escenario, si un fallo es un bug o una falta de permiso.
>
> **Fuente**: `systemuserroles_association` (rol directo), `teammembership_association` +
> `teamroles_association` (rol por equipo) y `principalobjectaccessset` (apps compartidas).

## Matriz

Solo roles de CashHub. Los 10 usuarios tienen además roles de otros proyectos (WFE, Heartbeat,
Sigil, Procurement, CM) que se omiten por no ser relevantes acá.

| Usuario | Roles CashHub | Vía | Apps compartidas |
|---|---|---|---|
| **User 1** | `DepositsTracker`, `EmailOperationsConsole` | directo | IngestionConsole *(editar)* |
| | `ClientAdminContributor` | equipo *Banca de Empresas - Contributor* | ClientAdministration |
| **User 2** | `ClientAdminApprover`, `Hub` | equipo *Banca de Empresas -Approver* | ClientAdministration |
| **User 3** | `ClientAdminContributor` | equipo *Bóveda - Contributor* | ClientAdministration |
| **User 4** | `ClientAdminApprover`, `Hub` | equipo *Aprobadores - sanic_chtblclientbranch* | ClientAdministration |
| **User 5** | — | — | — |
| **User 6** | `CountingSupervisor`, `Receptionist`, `Hub` | directo | Reception, Supervisor |
| **User 7** | `Cashier`, `Hub` | directo | Recounting |
| **User 8** | `Digitizer`, `Hub` | directo | Digitization |
| **User 9** | — | — | — |
| **User 10** | `ClientAdminContributor`, `CountingSupervisor` | directo | Supervisor |
| | `ClientAdminApprover`, `Hub` | equipo *Banca de Empresas -Approver* | ClientAdministration |

## Cobertura por rol

| Rol | Quién lo tiene |
|---|---|
| `CH-SR-Cashier` | User 7 |
| `CH-SR-CountingSupervisor` | User 6, User 10 |
| `CH-SR-Receptionist` | User 6 |
| `CH-SR-Digitizer` | User 8 |
| `CH-SR-DepositsTracker` | User 1 |
| `CH-SR-EmailOperationsConsole` | User 1 |
| `CH-SR-ClientAdminContributor` | User 1, User 3, User 10 |
| `CH-SR-ClientAdminApprover` | User 2, User 4, User 10 |
| `CH-SR-Hub` | User 2, User 4, User 6, User 7, User 8, User 10 |
| `CH-SR-ConfigAdmin` | **nadie** |
| `CH-SR-EmailIngestionFlow` | nadie *(es identidad de servicio, correcto)* |

---

## Bloqueantes antes de poder probar

### 1. `CH-App-Hub` no está compartida con nadie

Seis usuarios tienen el rol `CH-SR-Hub`, que les da lectura del catálogo. Pero la app **no está
compartida con ningún usuario ni equipo**: `principalobjectaccessset` devuelve cero.

> **Nadie puede abrir el lanzador.** El rol sin la app compartida no alcanza — son dos permisos
> distintos y hacen falta los dos.

### 2. `CH-App-DepositsTracker` no está compartida con nadie

User 1 tiene el rol `CH-SR-DepositsTracker` (22 tablas, solo lectura) y **no puede abrir la app**.
El rol quedó asignado y la app nunca se compartió.

### 3. Nadie tiene `CH-SR-ConfigAdmin`

La app de Configuración —la model-driven que administra parámetros, catálogos y reglas de
validación— **no la puede usar nadie**. Si un escenario necesita tocar `CH-Tbl-Parameter`,
`ValidationRules` o `EmailTemplate`, primero hay que asignar ese rol.

---

## Riesgos para el diseño de los escenarios

### User 10 puede crear **y** aprobar sus propias solicitudes

Tiene `ClientAdminContributor` directo y `ClientAdminApprover` por el equipo *Banca de Empresas
-Approver*. Es la única persona con los dos.

Para el proceso de alta/baja de clientes eso significa dos cosas:

- **No sirve** como "solicitante" en un escenario de aprobación con dos personas: sus RFC las
  puede resolver él mismo.
- **Sí sirve** —y conviene— como caso negativo: verificar si el sistema **permite la
  autoaprobación**. `GuardChangeRequestApprovalPlugin` valida pertenencia al grupo aprobador, y
  User 10 pertenece; habría que confirmar si además bloquea que el creador sea el resolutor.

### El equipo *CashHub - Bóveda - Contributor* otorga `ClientAdminContributor`

User 3 recibe permisos de **administración de datos de cliente** desde un equipo que por nombre es
de **bóveda/recepción**. Puede ser deliberado, pero el nombre no lo dice y conviene confirmarlo
antes de usar a User 3 como caso de prueba de recepción — no tiene `CH-SR-Receptionist`.

### Solo hay una persona por rol operativo

`Cashier` → solo User 7. `Receptionist` → solo User 6. `Digitizer` → solo User 8.

Cualquier escenario que necesite **dos cajeros distintos** —reasignación de sesión, cierre forzado
por otro— no se puede armar hoy.

---

## Usuarios libres

**User 5** y **User 9** no tienen ningún rol ni app de CashHub. Son los candidatos naturales para
cubrir los huecos: un segundo cajero, un `ConfigAdmin`, o un solicitante limpio que no pueda
aprobarse a sí mismo.

## Recomendación de asignaciones antes de arrancar

| Qué | Para qué |
|---|---|
| Compartir **CH-App-Hub** con los 6 que tienen el rol | Sin esto no se entra por el lanzador |
| Compartir **CH-App-DepositsTracker** con User 1 | Ya tiene el rol |
| Dar **`CH-SR-ConfigAdmin`** a alguien (¿User 5?) | Preparar datos maestros de los escenarios |
| Dar **`CH-SR-ClientAdminContributor`** a User 5 o 9 | Un solicitante que NO pueda autoaprobar |
| Dar **`CH-SR-Cashier`** a User 9 | Un segundo cajero para escenarios de reasignación |
