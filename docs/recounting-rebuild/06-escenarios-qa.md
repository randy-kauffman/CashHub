# 06 · Escenarios de QA humana — CH-App-Recounting v2 (DEV)

> Sembrado el **2026-08-17** contra `org36e60d9d.crm.dynamics.com` con el patrón de
> `scripts/seed-recounting-stress.py` (auth por `pp_secrets.env`, Web API `v9.2`,
> `$batch` con un changeset por request, marcadores rastreables).
> **El guard estuvo 100 % ACTIVO durante todo el sembrado**, salvo el paso que el
> dueño pidió apagar a propósito (§6).

> ✅ **CERRADO — `2026-08-17`, al cierre de la ronda de QA: el paso
> `CashHub.GuardReception: Update de sanic_chtblapprovalrequest` fue REACTIVADO.**
> Se corrió el procedimiento de la **§7** y se verificó **`statecode = 0`**
> (`ESTADO: ACTIVE`, `activos: 74 / 74`). **DEV vuelve a exigir resolutor ≠ creador y
> pertenencia al grupo AAD aprobador.** La advertencia de abajo queda como **registro
> histórico de la ventana de QA**, no como estado actual.
>
> 🧨 **Consecuencia operativa: `scripts/qa-approve-pending.py` YA NO FUNCIONA tal cual.**
> Ese script aprueba solicitudes **con la misma identidad que las creó**, que es
> exactamente lo que el paso reactivado bloquea. Para volver a usarlo hay que **apagar el
> paso otra vez** (§6), correrlo, y **volver a prenderlo** con la §7 verificando
> `statecode = 0`. Apagar el guard es una operación **deliberada, acotada y con
> re-activación obligatoria en el mismo turno** — nunca se deja apagado "hasta mañana".

---

## ⚠️ ADVERTENCIA — registro de la ventana de QA (paso ya REACTIVADO)

Durante la ronda de QA del **2026-08-17**,
`CashHub.GuardReception: Update de sanic_chtblapprovalrequest` estuvo **Disabled**
(`statecode = 1`). Mientras estuvo así, **cualquiera podía resolver su propia
solicitud** (no se exigía resolutor ≠ creador ni pertenencia al grupo AAD aprobador).
Fue TEMPORAL y pedido explícitamente para esa ronda.
**Al cierre se corrió la §7 y el paso quedó ACTIVO (`statecode = 0`), verificado.**
Si volvés a apagarlo (por ejemplo para `scripts/qa-approve-pending.py`), **la §7 es
obligatoria antes de terminar el turno.**

---

## 1. Índice de escenarios

| # | Escenario | Tula(s) | Qué prueba | Estado esperado |
|---|---|---|---|---|
| **E0** | Sesión poblada | 15 tulas ligadas a `RCS-1026` | La app muestra tulas reales en la sesión del dueño (10 recontadas, 3 pendientes de supervisor, 2 abiertas) | `scrSesion` lista 15 filas; los espejos E-1 de sus 49 depósitos apuntan a `RCS-1026` |
| **E1** | Tula gorda | `ST000517` | Rendimiento de `scrConteo` con **612 cheques** en una sola tula | La pantalla abre y pagina sin colgarse |
| **E2** | **Homónimas** (diálogo `B1`) | `ST990001` ×3 | Desambiguación por número repetido | `B1` con **3 cards**; **solo** la de *Almacenada en bóveda* es seleccionable |
| **E3** | **Sin declaración** (`B3` + approval) | `ST990002` | Ruta “no hay declaración para esta tula” | `B3` → solicitud **`Tula sin declaracion`** → aprobar → contar |
| **E4** | **Multi-moneda con diferencias y cheques** (`C5`) | `ST990003` | Conteo NIO/USD/EUR, cheque declarado sin marcar, cheque manual | `C5` con hallazgos **por moneda** + HTML en el `ApprovalRequest` |

---

## 2. E0 — La sesión `RCS-1026` del dueño

| Campo | Valor |
|---|---|
| `sanic_name` | `RCS-1026` |
| GUID | `81fca3c2-639a-f111-b8dd-7ced8d6f268c` |
| Cajero (`Cashier`) | **Randy Kauffman** — `admin@55xljh.onmicrosoft.com` (`8bccbe02-f1ee-f011-8406-7ced8d70b036`) |
| Mesa (`CountingDesk`) | `SRC-MESA0019` (`00270490-539a-f111-b8dd-7ced8d6f268c`) |
| `createdon` | `2026-08-17T17:47:49Z` |
| `Confirmed` | `true` · `SessionForciblyClosed` `false` |

### 2.1 Las 15 tulas ligadas

Un solo `PATCH` por tula, **únicamente** `sanic_RecountingSession@odata.bind`.
No toca `BagStatus` ni identidad ⇒ `GuardReceptionPlugin` cae en `Allow()` por
contrato (`GuardReceptionPlugin.cs:259-278`: solo evalúa la matriz si el Target
contiene `sanic_bagstatus`, y la edición de identidad solo si toca `BagId` /
`Name` / `ReceptionID`). **Cero rechazos del guard** — ni siquiera sobre las
`Recontada`, que es un estado terminal para *transiciones*, no para lookups.

| # | Tula | GUID | BagStatus | Depósitos | Monedas | Planilla | Por qué está |
|---|---|---|---|---|---|---|---|
| 1 | `ST000517` | `1b70458e-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 6 | NIO·USD | `SR000241` | **la gorda: 612 cheques** |
| 2 | `ST000060` | `7ef24c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 5 | NIO·**EUR** | `SR000030` | cobertura EUR |
| 3 | `ST000036` | `bdf14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 1 | USD | `SR000018` | 1 solo depósito |
| 4 | `ST000107` | `d8f04c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 2 | NIO | `SR000051` | 2 depósitos |
| 5 | `ST000192` | `1d6f458e-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 3 | NIO·USD | `SR000091` | 3 depósitos |
| 6 | `ST000019` | `43f14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 4 | NIO·USD | `SR000010` | 4+ depósitos |
| 7 | `ST000111` | `f7f04c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 2 | NIO·USD | `SR000053` | **con** `DeclaredBag` enlazada |
| 8 | `ST000037` | `c7f14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 4 | NIO | `SR000019` | **sin** `DeclaredBag` enlazada |
| 9 | `ST000113` | `06f14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 4 | NIO·USD | `SR000055` | multi-moneda en la misma tula |
| 10 | `ST000122` | `4ff14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460003 Recontada | 4 | NIO·EUR | `SR000059` | segunda con EUR |
| 11 | `ST000120` | `3ff14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460004 Pend. supervisor | 3 | NIO·USD | `SR000058` | ruta de revisión |
| 12 | `ST000137` | `c1f14c88-9c99-f111-b8dd-7ced8d6f268c` | 159460004 Pend. supervisor | 4 | NIO·USD | `SR000067` | ruta de revisión |
| 13 | `ST000149` | `1ef24c88-9c99-f111-b8dd-7ced8d6f268c` | 159460004 Pend. supervisor | 3 | NIO·USD | `SR000073` | ruta de revisión |
| 14 | `ST000059` | `77f24c88-9c99-f111-b8dd-7ced8d6f268c` | 159460002 Asignada a cajero | 3 | NIO | `SR000029` | abierta, se puede seguir contando |
| 15 | `ST000110` | `eff04c88-9c99-f111-b8dd-7ced8d6f268c` | 159460002 Asignada a cajero | 1 | NIO | `SR000052` | abierta, se puede seguir contando |

Cobertura de monedas entre las 10 recontadas: **NIO, USD y EUR**. ✔

### 2.2 Verificación de la cascada E-1 (`DepositSessionMirror`)

El plugin `CashHub.DepositSessionMirror: Update de sanic_chtblbags` re-estampa
`sanic_RecountingSessionMirror` en los depósitos de la tula cuando cambia su
`RecountingSession`. **Verificado post-PATCH, sin backfill:**

| Tula | depósitos con `RecountingSessionMirror = RCS-1026` |
|---|---|
| `ST000019` 4/4 · `ST000036` 1/1 · `ST000037` 4/4 · `ST000059` 3/3 · `ST000060` 5/5 | |
| `ST000107` 2/2 · `ST000110` 1/1 · `ST000111` 2/2 · `ST000113` 4/4 · `ST000120` 3/3 | |
| `ST000122` 4/4 · `ST000137` 4/4 · `ST000149` 3/3 · `ST000192` 3/3 · `ST000517` 6/6 | |
| **TOTAL** | **49 / 49** |

Contrapeso global: `_sanic_recountingsessionmirror_value eq 81fca3c2-…` devuelve
**exactamente 49** filas en todo DEV. No hay fugas ni depósitos huérfanos.

```http
GET /api/data/v9.2/sanic_chtbldepositsrecountings
    ?$select=sanic_name,sanic_currency,_sanic_recountingsessionmirror_value
    &$filter=_sanic_recountingsessionmirror_value eq 81fca3c2-639a-f111-b8dd-7ced8d6f268c
```

---

## 3. E2 — HOMÓNIMAS `ST990001` (diálogo `B1`)

Tres tulas **con el mismo `BagID`** y **tres recepciones distintas** (por eso el
picker muestra planilla y cliente distintos en cada card — `03-pantalla-tulas.md`
§10.2 pinta `ThisItem.BagID & " · Planilla " & ThisItem.ReceiptIdMirror` y
`ThisItem.ReceptionID.RemitentName`).

| `sanic_name` (marcador) | GUID | `BagStatus` | Planilla | Cliente | En `B1` |
|---|---|---|---|---|---|
| `SRC-QA-B01` | `68e27cc4-679a-f111-b8dd-7ced8d6f268c` | **159460001 Almacenada en bóveda** | `SR000001` | Supermercados La Colonia Norte, S.A. | **SELECCIONABLE** |
| `SRC-QA-B02` | `6ae27cc4-679a-f111-b8dd-7ced8d6f268c` | 159460000 Recibida en esclusa | `SR000003` | Ferretería Central de Managua, S.A. | gris · “todavía está en esclusa” |
| `SRC-QA-B03` | `6ce27cc4-679a-f111-b8dd-7ced8d6f268c` | 159460002 Asignada a cajero | `SR000004` | Transportes del Lago, S.A. | gris · “ya está asignada a un cajero” |

`SRC-QA-B03` cuelga de la sesión del stress **`SRC-S000101`**
(`a1960396-539a-f111-b8dd-7ced8d6f268c`).

> **Orden legal respetado:** `ReceptionGuard.EvaluateBagCreate` **bloquea** que una
> tula nazca en un estado del ciclo de recuento. `SRC-QA-B03` nació en
> *Almacenada en bóveda* y recién después se la movió a *Asignada a cajero*
> (transición legal bóveda → cajero) en un `PATCH` que además colgó la sesión.

### Declaración suelta de `ST990001`

| Registro | `sanic_name` | GUID | Contenido |
|---|---|---|---|
| `CH-Tbl-DeclaredBags` | `SRC-QADB01` | `75e27cc4-679a-f111-b8dd-7ced8d6f268c` | `BagNumber = ST990001`, `BagID` **BLANCO** (⇒ enlazable), `TotalNIO 10.000,00` |
| `CH-Tbl-DeclaredDeposits` | `SRC-QADD0101` | `79e27cc4-679a-f111-b8dd-7ced8d6f268c` | NIO · cta `100200400` · cash `10.000,00` · 0 cheques (`Total` fórmula = `10.000,00`) |

**Qué probar:** escanear `ST990001` → la app resuelve **3 candidatas** → abre `B1`
con 3 cards → el botón *Abrir tula* nace deshabilitado → solo la card de bóveda
responde al clic → al abrirla salta `B2`/enlace con `SRC-QADB01` y se cuenta un
depósito NIO simple.

---

## 4. E3 — SIN DECLARACIÓN `ST990002` (diálogo `B3` + approval)

| Registro | `sanic_name` | GUID | Detalle |
|---|---|---|---|
| `CH-Tbl-Bags` | `SRC-QA-B04` | `6ee27cc4-679a-f111-b8dd-7ced8d6f268c` | `BagID = ST990002`, **159460001 Almacenada en bóveda**, planilla `SR000005`, cliente Distribuidora El Porvenir, S.A. |
| `CH-Tbl-DeclaredBags` | — | — | **NINGUNA** (a propósito) |

**Qué probar:** escanear `ST990002` → 1 candidata, 0 declaraciones → **`B3`** →
solicitud tipo **`Tula sin declaracion`** → **aprobarla** → la app deja abrir la
tula y contar sin declaración de referencia.

> Con el paso de la §6 apagado, el mismo usuario que crea la solicitud puede
> aprobarla. Es exactamente el atajo que el dueño pidió para poder probar solo.

---

## 5. E4 — MULTI-MONEDA con diferencias y cheques `ST990003` (diálogo `C5`)

| Registro | `sanic_name` | GUID |
|---|---|---|
| `CH-Tbl-Bags` | `SRC-QA-B05` | `70e27cc4-679a-f111-b8dd-7ced8d6f268c` |
| `CH-Tbl-DeclaredBags` | `SRC-QADB03` | `77e27cc4-679a-f111-b8dd-7ced8d6f268c` |
| `CH-Tbl-Declarations` (contenedora de las dos DeclaredBags QA) | `SRC-QADECL01` | `74e27cc4-679a-f111-b8dd-7ced8d6f268c` |

Tula `ST990003` en **159460001 Almacenada en bóveda**, planilla `SR000009`,
cliente Farmacias San Benito, S.A. La `DeclaredBag` tiene `BagID` **blanco**
(enlazable desde `B2`) y totales que **cierran contra la suma de sus depósitos**:
`TotalNIO 65.000,00` · `TotalUSD 1.500,00` · `TotalEUR 800,00`.

### 5.1 Los tres depósitos declarados

| `sanic_name` | GUID | Moneda | Cuenta | Cash | BAC Local | Otros Local | `Total` (fórmula) |
|---|---|---|---|---|---|---|---|
| `SRC-QADD0301` | `7be27cc4-679a-f111-b8dd-7ced8d6f268c` | **NIO** | `100200300` | `50.000,00` | `10.000,00` | `5.000,00` | `65.000,00` |
| `SRC-QADD0302` | `7de27cc4-679a-f111-b8dd-7ced8d6f268c` | **USD** | `100200301` | `1.200,00` | `300,00` | `0,00` | `1.500,00` |
| `SRC-QADD0303` | `7fe27cc4-679a-f111-b8dd-7ced8d6f268c` | **EUR** | `100200302` | `800,00` | `0,00` | `0,00` | `800,00` |

### 5.2 Los tres cheques declarados

| `sanic_name` | GUID | Depósito | Nº cheque | Importe | Moneda | Tipo | Espejo E-3 |
|---|---|---|---|---|---|---|---|
| `SRC-QADC0301` | `81e27cc4-679a-f111-b8dd-7ced8d6f268c` | `SRC-QADD0301` | `700001` | `10.000,00` | NIO | **BAC Local** | `SRC-QADB03` ✔ |
| `SRC-QADC0302` | `83e27cc4-679a-f111-b8dd-7ced8d6f268c` | `SRC-QADD0301` | `700002` | `5.000,00` | NIO | **Otros Local** | `SRC-QADB03` ✔ |
| `SRC-QADC0303` | `85e27cc4-679a-f111-b8dd-7ced8d6f268c` | `SRC-QADD0302` | `700003` | `300,00` | USD | **BAC Local** | `SRC-QADB03` ✔ |

Coherencia verificada por API: `BACLocalCheckAmount` y `OtherLocalCheckAmount` de
cada depósito **son exactamente** la suma de sus cheques declarados por tipo.

### 5.3 Guion de la prueba humana

1. Escanear `ST990003` → 1 candidata en bóveda → `B2` ofrece `SRC-QADB03` → enlazar.
2. Contar **NIO** dejando **diferencia** en efectivo (p. ej. arquear 49.500 contra 50.000).
3. Marcar el cheque `700001` (BAC 10.000) y **DEJAR SIN MARCAR el `700002` (Otros 5.000)**.
4. **Agregar un cheque manual** que no estaba declarado.
5. Contar **USD** con diferencia y marcar el cheque `700003`.
6. Contar **EUR** con diferencia (no tiene cheques).
7. Terminar → debe salir **`C5`** con hallazgos **por moneda** (3 bloques: NIO, USD, EUR),
   incluyendo el cheque declarado no encontrado y el cheque manual sobrante, y el
   `ApprovalRequest` `Tula con diferencia` debe llevar ese HTML en `RequestDetails`.

---

## 6. Paso del guard DESACTIVADO (auditoría)

| Campo | Valor |
|---|---|
| Nombre | `CashHub.GuardReception: Update de sanic_chtblapprovalrequest` |
| `sdkmessageprocessingstepid` | **`ee7c4081-0495-f111-b8db-6045bd099905`** |
| Antes | `statecode 0` / `statuscode 1` — **Active** |
| Después | `statecode 1` / `statuscode 2` — **Disabled** |
| Qué deja de exigir | resolutor ≠ creador **y** pertenencia al grupo AAD aprobador (`ReceptionGuard.EvaluateApprovalUpdate`) |
| Steps `CashHub.*` en DEV | **74** en total |
| Estado final | **1 Disabled** (este) · **73 Active** |
| Otros steps tocados | **NINGUNO** — verificado por diferencia de conjuntos antes/después |

---

## 7. 🔴 RE-ACTIVAR el paso (correr apenas termine la QA)

### Opción A — HTTP directo (Web API)

```http
PATCH /api/data/v9.2/sdkmessageprocessingsteps(ee7c4081-0495-f111-b8db-6045bd099905)
Content-Type: application/json

{ "statecode": 0, "statuscode": 1 }
```

### Opción B — one-liner con los helpers del seeder

```bash
cd /home/gmaker/projects/CashHub && python3 - <<'EOF'
import importlib.util, sys
spec = importlib.util.spec_from_file_location('s', 'scripts/seed-recounting-stress.py')
S = importlib.util.module_from_spec(spec); sys.modules['s'] = S; spec.loader.exec_module(S)
STEP = 'CashHub.GuardReception: Update de sanic_chtblapprovalrequest'
S.set_step(S.leer_steps()[STEP]['sdkmessageprocessingstepid'], True)
d = S.leer_steps()[STEP]
print('ESTADO:', 'ACTIVE' if (d['statecode'] == 0 and d['statuscode'] == 1) else 'SIGUE DISABLED', d)
print('activos:', sum(1 for v in S.leer_steps().values()
                      if v['statecode'] == 0 and v['statuscode'] == 1), '/ 74')
EOF
```

Debe imprimir `ESTADO: ACTIVE` y `activos: 74 / 74`.

---

## 8. Inventario y limpieza selectiva

Todo lo **nuevo** lleva `sanic_name` con prefijo **`SRC-QA`**. Las tulas llevan
además el número REAL en `sanic_bagid` (`ST99000x`), que es lo que la app matchea
al escanear — el marcador va en `sanic_name`, que la UI no muestra.

| Tabla | Entity set | Filtro | Conteo |
|---|---|---|---|
| `CH-Tbl-Bags` | `sanic_chtblbagses` | `startswith(sanic_name,'SRC-QA')` | **5** |
| `CH-Tbl-Declarations` | `sanic_chtbldeclarations` | idem | **1** |
| `CH-Tbl-DeclaredBags` | `sanic_chtbldeclaredbagses` | idem | **2** |
| `CH-Tbl-DeclaredDeposits` | `sanic_chtbldeclareddepositses` | idem | **4** |
| `CH-Tbl-DeclaredChecks` | `sanic_chtbldeclaredcheckses` | idem | **3** |

> Las 15 tulas de la §2 **no son nuevas**: son del stress (`ST*`) y solo se les
> repunteó el lookup `RecountingSession`. Para revertirlas basta un `PATCH` del
> mismo lookup a su sesión anterior (tabla de abajo); **no** hace falta apagar
> ningún step porque no se toca `BagStatus`.

### 8.1 Sesión ANTERIOR de cada tula re-ligada (para revertir)

`PATCH /sanic_chtblbagses(<GUID tula>)` con
`{"sanic_RecountingSession@odata.bind": "/sanic_chtblrecountingsessions(<GUID sesión previa>)"}`.

| Tula | GUID tula | Sesión previa |
|---|---|---|
| `ST000517` | `1b70458e-9c99-f111-b8dd-7ced8d6f268c` | `6a970396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000060` | `7ef24c88-9c99-f111-b8dd-7ced8d6f268c` | `ca9a0396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000036` | `bdf14c88-9c99-f111-b8dd-7ced8d6f268c` | `38980396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000107` | `d8f04c88-9c99-f111-b8dd-7ced8d6f268c` | `3a990396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000192` | `1d6f458e-9c99-f111-b8dd-7ced8d6f268c` | `e8980396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000019` | `43f14c88-9c99-f111-b8dd-7ced8d6f268c` | `f8970396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000111` | `f7f04c88-9c99-f111-b8dd-7ced8d6f268c` | `ab970396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000037` | `c7f14c88-9c99-f111-b8dd-7ced8d6f268c` | `02970396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000113` | `06f14c88-9c99-f111-b8dd-7ced8d6f268c` | `e5990396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000122` | `4ff14c88-9c99-f111-b8dd-7ced8d6f268c` | `cc980396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000120` | `3ff14c88-9c99-f111-b8dd-7ced8d6f268c` | `2e970396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000137` | `c1f14c88-9c99-f111-b8dd-7ced8d6f268c` | `a29a0396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000149` | `1ef24c88-9c99-f111-b8dd-7ced8d6f268c` | `6b9a0396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000059` | `77f24c88-9c99-f111-b8dd-7ced8d6f268c` | `1a9a0396-539a-f111-b8dd-7ced8d6f268c` |
| `ST000110` | `eff04c88-9c99-f111-b8dd-7ced8d6f268c` | `f79a0396-539a-f111-b8dd-7ced8d6f268c` |

Revertir el lookup **también revierte el espejo E-1** de sus depósitos (mismo
plugin `DepositSessionMirror`), sin backfill.

### Orden de borrado (inverso al de creación)

`DeclaredChecks` → `DeclaredDeposits` → `DeclaredBags` → `Declarations` → `Bags`.
Las tulas QA se borran **sin apagar ningún step** salvo `SRC-QA-B03`, que está en
*Asignada a cajero*: hay que devolverla a bóveda primero (transición legal
cajero → … **no existe** hacia bóveda) o apagar temporalmente
`CashHub.GuardReception: Update de sanic_chtblbags`, igual que hace el `--clean`
del seeder de estrés.

---

## 9. Resolución por número — verificación final

El escaneo de `scrTulas` filtra por **igualdad exacta**
(`BagID = ctxScanTul`, `03-pantalla-tulas.md` §… `colTulasCandidatasTul`), no por
`startswith`. El `startswith` sirve para el inventario y la limpieza.

| Consulta | Resultado |
|---|---|
| `sanic_bagid eq 'ST990001'` + estados recontables | **3** candidatas (159460000 / 159460001 / 159460002) · 1 `DeclaredBag` enlazable (`SRC-QADB01`) |
| `sanic_bagid eq 'ST990002'` | **1** candidata (159460001) · **0** `DeclaredBags` |
| `sanic_bagid eq 'ST990003'` | **1** candidata (159460001) · 1 `DeclaredBag` enlazable (`SRC-QADB03`) |
| `startswith(sanic_bagid,'ST9900')` | **5** tulas · **2** `DeclaredBags` (`startswith(sanic_bagnumber,'ST9900')`) |

Los espejos de recepción (`ReceiptIdMirror`, `ReceptionBatchId`) quedaron
estampados por sus plugins en las 5 tulas nuevas — no se mandaron en el JSON.

> **Nota:** no se crearon `Checks` recontados ni `DepositsRecountings` para las
> tulas QA — eso lo produce la app durante la prueba humana. Por eso el espejo
> **E-2 (`Bag` en cheques) no aplica** a este sembrado; sí se verificó **E-3**
> (`DeclaredBagMirror`, §5.2) y **E-1** (`RecountingSessionMirror`, §2.2).
