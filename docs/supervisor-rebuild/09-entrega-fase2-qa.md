# Entrega Fase 2 — CH-App-Supervisor v2 (las 4 pantallas vestidas)

**Publicado en el entorno** (import `--publish-changes` OK). Este documento es tu
paquete de revisión: qué hay, qué verificar por pantalla, y qué queda pendiente
con su porqué.

## 1. Qué se entregó en este ciclo

| Pieza | Estado |
|---|---|
| scrResumen | Vestida en fase 1 + fixes de tu QA: N+1 de cheques muerto (1 consulta), KPI auto-encogible, símbolo US$ sin montar (ancho real + sin wrap), **DefaultDate=Today()** (el picker abre con hoy puesto) |
| scrAprobaciones | Vestida completa: bandeja delegable, motor de carga por solicitud, caja de diferencias POR COMPONENTE × moneda, cuatro ojos, confirmación con motivo → StatusReason, Aprobar/Rechazar con los 10 tipos y revalidación de estado |
| scrTulas | Vestida completa: consulta única con ventana 30 días, segmentado 6 estados + contadores, orden por columna, aviso de tope (2000), fecha-hasta inclusiva |
| scrCheques | Vestida completa: multiselección clásica (solo Recontado), envío con conteo real de éxitos/fallos, reenvío con confirmación modal, sellos visibles, **aviso de tope 2000 en la banda ámbar** (nuevo — la guía no lo tenía y tu universo de estrés lo exige) |
| Límite de filas | 500 → **2000** (manifest + 14 datasources) — mitigación, NO la solución (ver §3) |
| Backend B-9 | Espejo `sanic_recountingsession` en Checks: código + scripts + tests listos; **columnas B-2/B-3/B-6/B-9 YA CREADAS en el entorno** y backfill Web API corriendo |

**Hallazgo de plataforma corregido**: `SelectedDate` del datepicker moderno es
propiedad de SALIDA (jamás se le escribe — ese era tu error críptico de "Today");
la entrada real es `DefaultDate`. Verificado en `modernDatePicker_1.0.1.xml`.

## 2. Checklist de QA por pantalla (lo que te pido revisar)

**scrResumen** (re-verificación rápida)
- [ ] El selector de fecha abre con HOY puesto (ya no vacío) y sin error del Checker.
- [ ] El símbolo `US$` ya no se monta (ancho real, sin wrap).

**scrAprobaciones**
- [ ] La bandeja carga pendientes; el buscador filtra por tipo/número de solicitud.
- [ ] Seleccionar una "Tula con diferencia" muestra la caja POR COMPONENTE × moneda
      con columnas Sobrante/Faltante SIN compensar, y el aviso nombra los montos.
- [ ] Aprobar exige motivo, revalida estado (probá doble aprobación desde 2 sesiones),
      y el cuatro-ojos bloquea si vos creaste la solicitud.
- [ ] Rechazar cubre los 10 tipos (probá "Editar tula" — antes quedaba atrapada).
- [ ] Warnings de delegación ESPERADOS en los 2 dup-checks (fallback declarado hasta
      que B-2 entre con el refresh del datasource — ver §3).
- [ ] `lblKvCuentaApr` probablemente muestre "—": es el FSP pendiente (B-8), no un bug.

**scrTulas**
- [ ] Segmentado con los 6 estados + "Todas"; los dos estados antes invisibles
      (Asignada a cajero / Pend. revisión recuento) aparecen.
- [ ] "Fecha hasta = hoy" INCLUYE hoy. Orden por columna Tula/Creado funciona.
- [ ] Búsqueda por número de tula; el placeholder no promete recibo (B-3 pendiente).

**scrCheques**
- [ ] Solo se marcan cheques Recontado; enviar reporta "N enviados, M rechazados".
- [ ] Reenviar pide confirmación con el conteo. Sellos de fecha visibles.
- [ ] Con tu estrés (2.074 cheques): la banda ámbar avisa el TOPE de 2000 —
      honestidad, no solución (ver §3).

## 3. La solución REAL de delegación de cheques (B-9) — estado y pasos

Tu objeción era correcta: subir el límite no resuelve; materializar filas para
contar SIEMPRE topa. La solución: **contar en el servidor** (`CountRows` delegado)
vía espejo de sesión en el cheque.

- ✅ Columna `sanic_recountingsession` creada en el entorno (+ B-2/B-3/B-6).
- ⏳ Backfill Web API corriendo (puebla el histórico SIN desplegar plugins).
- ⬜ **TU PASO (2 clics)**: en Studio → Datos → `CH-Tbl-Checks` → **Actualizar** el
  datasource (la app no ve columnas nuevas sin esto). Ídem `CH-Tbl-Bags` y
  `CH-Tbl-ApprovalRequests` para B-2/B-3/B-6.
- ⬜ Con eso hecho, aviso y hago el swap de scrResumen a conteos delegados
  (adiós colección de cheques) y el upgrade de los dup-checks de Aprobaciones y la
  búsqueda por recibo de Tulas a sus espejos.
- ⬜ El plugin que mantiene los espejos al día viaja con el paquete backend
  (runbook 08) — hasta entonces, cheques nuevos quedan sin espejo y se re-corre el
  backfill.

## 4. Pendientes fuera de este ciclo (sin cambios)

- Runbook backend 08 completo (roles, guards, FSP `sanic_accountnumber` B-8 —
  bloqueante para publicar Aprobaciones en serio, parámetro del grupo aprobador).
- Regla de oro intacta: el guard de contabilidad B-5 se registra DESPUÉS de que
  Aprobaciones v2 esté validada.
- Re-exportar CashHubCore al repo (drift de esquema, B-1/B-9).
