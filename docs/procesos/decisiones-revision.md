# Decisiones — revisión de docs/procesos

> Bitácora de decisiones tomadas durante la revisión guiada de `docs/procesos/`.
> Cada entrada referencia el ID del componente tal como aparece en
> [`docs/referencia/inventario-componentes.md`](../referencia/inventario-componentes.md), donde se
> refleja el cambio de estado y se completan las columnas Objetivo / Dónde se usa / Documentos de
> referencia correspondientes.

## Cómo leer esta bitácora

| Campo | Significado |
|---|---|
| **Guía** | Archivo de `docs/procesos/` que disparó la decisión |
| **Componente(s)** | ID(s) del inventario afectado(s) (logical name / schema name) |
| **Decisión** | Eliminar / Modificar / Marcar correcto |
| **Motivo** | Por qué — el razonamiento del usuario |
| **Acción de seguimiento** | Qué falta hacer en código/solución para ejecutar la decisión (si aplica) |

---

### 2026-08-25 — Actualización de `01-administracion-datos-clientes.md`

| Campo | Contenido |
|---|---|
| **Guía** | `01-administracion-datos-clientes.md` |
| **Componente(s)** | `CH-SR-ClientAdminContributor`, `CH-SR-ClientAdminApprover`, `CH-Flow-Notifyreviewers`, `CH-Flow-Createclientdata`, `CH-Flow-RFCWatchdog`, `sanic_chtblclientdatachangerequestline` (campos `sanic_solvedby`/`sanic_solvedon`), `sanic_chtblclientdatachangerequest` (campo legacy `cr140_changestatus`) |
| **Decisión** | Actualizar el doc (no fue el usuario leyendo línea por línea — fue una verificación puntual pedida directamente sobre esta guía) |
| **Motivo** | El usuario reportó sospecha de desactualización por cambios de UX/UI, plugins y steps. Verificación contra código vivo confirmó: (1) GAP S2 (privilegio ClientAccount) ya cerrado en código pero el doc seguía describiéndolo como abierto; (2) **discrepancia real, no solo documental**, entre el diseño (qué flujos deberían estar ON/OFF tras la migración a Custom API síncrona) y el `StateCode` real en `solutions/unpacked/flow/`: `Notify reviewers` aparece OFF debiendo estar ON, `Create client data` y `RFCWatchdog` aparecen ON debiendo estar retirados; (3) versión de `CashHubApps` desactualizada en el doc (1.33.0.0 vs. 1.33.0.3 real); (4) campos `sanic_solvedby`/`sanic_solvedon` de la línea de RFC no listados en §6; (5) el campo legacy `cr140_changestatus` sigue en el esquema, no se limpió como daba a entender el doc. |
| **Acción de seguimiento** | **Verificar en vivo con el propietario del ambiente Dev el estado real de los 3 flujos en discrepancia** (no resoluble solo con el repo) y corregirlo en Power Automate si el entorno vivo no coincide con lo esperado — riesgo de doble materialización / falta de notificación a aprobadores mientras esto no se confirme. Inventario (`docs/referencia/inventario-componentes.md`) actualizado con Objetivo/Dónde se usa/Documentos de referencia para los 3 flujos y los 2 roles afectados. |

<!-- Las entradas se van agregando acá, en orden cronológico, a medida que surgen. -->
