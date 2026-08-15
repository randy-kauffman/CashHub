# Runbook — Despliegue del backend de seguridad (CH-App-Supervisor v2)

Consolida los 4 paquetes backend del rediseño. Todo está en el repo **sin build ni
commit**. Este documento fija el ORDEN (hay dependencias cruzadas y una restricción
crítica de co-despliegue con la app) y separa lo que va **por script/API** de lo que
va **a mano en el portal**.

> **Regla de oro del orden**: el guard de contabilidad (B-5) BLOQUEA la rama
> "recontado sin declarar" de la app VIEJA (que emitía `Deposito` sin ancla ni
> cuenta). **B-5 no se activa hasta que la pantalla Aprobaciones reconstruida (guía
> 03) esté publicada.** Si activás B-5 con la app vieja todavía en producción, esa
> rama falla. Ver §4.

## 0. Inventario de lo entregado

| Paquete | Archivos nuevos | Archivos modificados | Script |
|---|---|---|---|
| **Roles + auditoría** | — | 6 Roles/*.xml + 2 Entity.xml | `apply-supervisor-role-remediation.sh` |
| **Guard de aprobación** (H-01, H-08) | `ApproverGroupConfiguration.cs`, `ApprovalRequestResources.cs` (+tests) | `ReceptionGuard.cs`, `GuardReceptionPlugin.cs` | `register-reception-guard.sh` |
| **Guard de contabilidad** (B-5, H-04) | `AccountingGuard.cs`, `GuardSupervisorAccountingPlugin.cs` (+tests) | `Schema.cs`, `TransactionGuard.cs`, `GuardTransactionPlugin.cs` | `register-transaction-guard.sh`, `create-alternate-keys.sh` |
| **Espejos** (B-2, B-3, B-6) | `BagReceptionMirrorPlugin.cs`, `ApprovalRequestBagNumberPlugin.cs`, `SupervisorMirrorSchema.cs`, `FlatJson.cs`, `BagReceptionMirrors.cs`, `ApprovalBagNumber.cs` (+tests) | — | `create-supervisor-mirror-columns.sh`, `register-supervisor-mirrors.sh`, `backfill-supervisor-mirrors.py` |

**Coherencia verificada estáticamente** (no build): cero símbolos duplicados;
namespaces alineados (`CashHub.Plugins`, `CashHub.Validation.Security`,
`CashHub.Validation.Tests`); el guard consume `SupervisorMirrors.ApprovalRequest.*`
del paquete espejos y `FlatJson` es único. Los tres "schema-lets"
(`SupervisorMirrors`, `ApprovalNames` privado del guard, y lo agregado a `Schema`)
NO colisionan; **plegar `SupervisorMirrors` + `ApprovalNames` dentro de `Schema.cs`
es cleanup opcional** post-merge, no bloquea.

## 1. Prerrequisitos MANUALES (portal) — antes de cualquier registro

1. **Sembrar el parámetro del grupo aprobador** en `CH-Tbl-Parameter` (sin esto, el
   registro del guard de aprobación ABORTA y, si se forzara, nadie podría aprobar):
   | Campo | Valor |
   |---|---|
   | `sanic_name` | `Security.ApproverGroup.Supervisor` |
   | `sanic_value` | `17b5e5c7-e7a0-4b61-98cd-ceff67211797` (object id AAD del team "CashHub - Banca de Empresas -Approver") |
   | `sanic_category` | Security · `sanic_datatype` Guid · `sanic_isactive` true |
   > Agregar la fila al SEED de `scripts/provision-parameters-table.py` o crearla a mano.

2. **B-8 — Field Security Profile sobre `sanic_accountnumber`** (verificado: `IsSecured=1`
   en Transactions, DepositsRecounting, DeclaredDeposits; ninguno de los 4 FSP del
   entorno cubre al supervisor). Crear un FSP que dé **Read** a la autoridad del
   supervisor (el team de Banca de Empresas -Approver, o los roles del circuito)
   sobre `sanic_accountnumber` en las 3 tablas. **BLOQUEANTE de publicación**: sin
   esto la app lee la cuenta como Blank sin error → asientos con cuenta vacía.

3. **Verificar membresía materializada del team AAD**: los AAD group teams sincronizan
   `teammembership` al primer login del usuario. Un supervisor que nunca entró puede
   dar "no miembro" → probar el flujo con un usuario real ANTES de anunciar.

## 2. Build y tests (una vez, tras traer el código)

```bash
dotnet test  src/backend/CashHub.Validation.Tests/CashHub.Validation.Tests.csproj
dotnet build src/backend/CashHub.Plugins/CashHub.Plugins.csproj -c Release
```
Ningún agente buildeó (regla del repo). Los tests son de dominio puro (xUnit) — **no
hay FakeXrmEasy en el repo y no aplica**: los tests son net8.0 y los plugins net462,
no se referencian; la convención es decisión pura testeable + plugin adaptador delgado.

## 3. Despliegue por paquete (orden entre paquetes)

### 3.1 Roles + auditoría (independiente, se puede primero)
```bash
./scripts/apply-supervisor-role-remediation.sh            # DRY-RUN: revisá el diff
./scripts/apply-supervisor-role-remediation.sh --apply    # aplica vía ReplacePrivilegesRole
```
> El import de solución NO revoca privilegios (es aditivo) — por eso las bajas van por
> API con `ReplacePrivilegesRole`. La **auditoría por columna** (los 2 Entity.xml) SÍ
> viaja por **solution import** (es metadato); el script solo la verifica por GET.

### 3.2 Espejos B-2/B-3/B-6 (habilitan la delegación de las guías)
```bash
./scripts/create-supervisor-mirror-columns.sh   # 1º las columnas en CashHubCore
# (build ya hecho en §2)
./scripts/register-supervisor-mirrors.sh         # aborta si faltan las columnas
# BYPASS del guard ya integrado (§3.3 lo trae) → recién después:
python3 scripts/backfill-supervisor-mirrors.py   # backfill del histórico
```
> El bypass de congelamiento para `sanic_bagnumber` YA está integrado en el guard de
> aprobación (§3.3) — sin él, el backfill sobre solicitudes resueltas fallaría.

### 3.3 Guard de aprobación (H-01, H-08)
Requiere §1.1 (parámetro sembrado). Incluye el bypass `touchesOnlyMirror`
{`sanic_bagnumber`, PK} que desbloquea el backfill de B-6.
```bash
./scripts/register-reception-guard.sh            # aborta si el parámetro no está
```

### 3.4 Guard de contabilidad B-5 + H-04 — **VER §4 antes de correr**
```bash
./scripts/register-transaction-guard.sh          # 2º plugin type + step Create rank 2;
                                                 # BORRA filteringattributes del Update
./scripts/create-alternate-keys.sh               # key (recuento, tipo); verificar EntityKeyIndexStatus
```

## 4. Restricción CRÍTICA de co-despliegue (B-5 ↔ app)

La app **vieja** emite el caso "recontado sin declaración" como `Deposito` **sin
ancla y sin cuenta** — el bug que la guía 03 corrige a `Sobrante` anclado. El guard
B-5 **bloquea** ese asiento mal formado. Por lo tanto:

- **NO registrar el step de Create de B-5 hasta que la pantalla Aprobaciones
  reconstruida (guía 03) esté publicada y en uso.** Si no, esa rama de la app vieja
  falla contra el guard.
- Secuencia recomendada: construir/publicar la app v2 → smoke de Aprobaciones →
  recién ahí `register-transaction-guard.sh`.
- El resto de B-5 (H-04, el congelamiento de montos post-Aplicada) no tiene este
  problema, pero el script registra ambos a la vez — si hace falta, separar el step
  de Create en una segunda corrida.

**Alternate key — fallback B-5.1**: si los asientos sin ancla preexistentes hacen que
el índice trate `NULL = NULL`, la key queda en `Failed`. No forzar: la salida es
agregar el lookup `sanic_relateddeclareddeposit` a `sanic_chtbltransactions` y anclar
también esos asientos. El plugin NO depende de la key (la key solo cierra la carrera
entre dos supervisores simultáneos).

## 5. Post-despliegue

1. **Regenerar los datasources** de la canvas para que aparezcan las columnas espejo
   nuevas (`sanic_receptionbatchid`, `sanic_receiptidmirror`, `sanic_bagnumber`).
   Con B-3 se restituye el placeholder L-18 ("Número de tula o de recibo…"); con B-6,
   la búsqueda por nº de tula (L-19/A-2) pasa a delegable `StartsWith(BagNumber, …)`.
2. **Re-exportar CashHubCore** al repo: el `Entity.xml` de Bags ya está desincronizado
   (le faltan las columnas B-5 del tracker y ahora las 3 nuevas). Mismo síntoma que
   B-1. Hasta hacerlo, el repo miente por omisión sobre el esquema.
3. **Vigilar**: al quitar el `filteringattributes` del Update de transacciones, ahora
   TODO update despierta el guard (sale por `touchesOnlyMirror` sin query, pero son N
   invocaciones por transición de lote grande).

## 6. Qué queda pendiente / fuera de este paquete

- **B-4** (Custom API `sanic_CHResolveApprovalRequest`) — la resolución atómica de la
  aprobación. Los guards de arriba cierran la fabricación por API; B-4 cerraría la
  atomicidad/concurrencia de los EFECTOS. Fase siguiente.
- El diff de roles entorno-vs-repo salió limpio (solo `CH-SR-DepositsTracker` fuera de
  fuentes, ya conocido). No hay roles fantasma.
- `organization.isauditenabled` = true (retención 30 días) — la auditoría por columna
  sí grabará.
