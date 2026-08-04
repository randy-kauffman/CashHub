# Hallazgos y backlog de mejora — CashHub

> Fuente: hallazgos de [`docs/analysis/00-sintesis.md`](../analysis/00-sintesis.md) a [`04-apps-administrativas.md`](../analysis/04-apps-administrativas.md), reorganizados como backlog accionable. Evidencia verificada contra `solutions/unpacked/`. Esfuerzo: **S** (horas/una sesión), **M** (días, un solo componente), **L** (días/semanas, cruza componentes o requiere decisión de negocio/migración de datos).

## Resumen priorizado

| # | Ítem | Categoría | Impacto | Esfuerzo |
|---|---|---|---|---|
| 1 | 3 roles sin privilegios sobre ninguna tabla custom | Seguridad | Alto | M |
| 2 | 18/38 tablas sin privilegio en ningún rol | Seguridad | Alto | M |
| 3 | Auto-aprobación del cajero en su propia mesa | Seguridad | Alto | M |
| 4 | Apps sin verificación de rol en Power Fx | Seguridad | Alto | L |
| 5 | Monto de diferencia no persistido | Integridad | Medio-Alto | M |
| 6 | Estado fragmentado en ≥5 option sets | Integridad | Medio | L |
| 7 | Relaciones bidireccionales inconsistentes | Integridad | Bajo-Medio | M |
| 8 | Campo `cr140_changestatus` de otro publisher | Integridad | Medio | L |
| 9 | `IngestEmails` desactivado | Operación | Crítico | S |
| 10 | Buzón compartido hardcodeado | Operación | Alto | S |
| 11 | Excel temporal sin limpieza | Operación | Medio | S |
| 12 | TRNCODE/identidad de agencia hardcodeados | Operación | Medio-Alto | M |
| 13 | VR02/VR04 rotos | Operación | Bajo (hoy) / Alto (si se activan) | S |
| 14 | `ValidationRulesOrchestrator` stub | Operación | Bajo (hoy) / Alto (si se depende de él) | S–L según decisión |
| 15 | Typos en LogicalNames | Calidad | Bajo | S–L según alcance |
| 16 | Componentes compartidos sin uso | Calidad | Bajo-Medio | M–L |
| 17 | Código de debug / duplicado / comentado | Calidad | Bajo | S |

---

## Seguridad

### 1. Tres roles (`CH-Role`, `CH-SR-AnalyticsRead`, `CH-SR-OpsAdmin`) sin privilegios sobre ninguna tabla custom

**Descripción**: de los 6 Security Roles de CashHubCore, 3 solo tienen privilegios genéricos de plataforma (Workflow, SharePoint, Team, User). `CH-Role` al menos incluye `prvReadsanic_CHTblAppCatalog`; `CH-SR-AnalyticsRead` y `CH-SR-OpsAdmin` no tienen ni eso — cero privilegios sobre cualquier tabla `sanic_*`.

**Evidencia**: `solutions/unpacked/core/Roles/CH-Role.xml`, `CH-SR-AnalyticsRead.xml`, `CH-SR-OpsAdmin.xml`.

**Impacto**: un rol llamado "lectura de analítica" o "administración de operaciones" que no puede leer/escribir ninguna tabla de negocio no es funcional tal como está exportado. Si se asigna a usuarios reales pensando que da acceso, el usuario no podrá operar y el problema se detecta recién en producción.

**Esfuerzo**: M — requiere decisión de negocio (qué debería poder ver/hacer cada rol) antes de editar privilegios; la edición técnica en sí es rápida.

### 2. 18 de 38 tablas sin ningún privilegio en ningún rol

**Descripción**: ningún Security Role de la solución otorga privilegio sobre `sanic_CHTblClient`, `ClientBranch`, `ClientEmail`, `ClientDepositHistory`, todo el dominio de correo/validación (`ReceivedEmails`, `EmailsAttachments`, `XMLReading(Detail)`, `ValidationResults`, `ValidationRuns`, `ValidationRules`), `TransactionBatches`, y todo el módulo de cambio de datos de cliente (`ClientDataChangePermission`, `ClientDataChangeRequest(Line)`, `ClientDataChangesAuditTrail`, `ClientTableLogicalFieldName`).

**Evidencia**: ausencia confirmada revisando privilegios `prv*` en los 6 archivos de `solutions/unpacked/core/Roles/*.xml` contra las 38 carpetas de `solutions/unpacked/core/Entities/`.

**Impacto**: dentro de los archivos de esta solución, ningún usuario humano con estos 6 roles puede crear/leer/editar un Cliente, un correo recibido, una regla de validación o una solicitud de cambio de datos. En Dataverse esto normalmente implica que el acceso real se resuelve por otra vía (System Administrator implícito, Service Principal de un flujo con permisos elevados) — riesgo si eso no está documentado ni controlado fuera de este paquete.

**Esfuerzo**: M — definir y agregar privilegios por rol para 18 tablas es mecánico una vez decidido el modelo de acceso, pero exige primero mapear qué rol físico (Analista de correos, Administrador de clientes, etc.) necesita qué nivel sobre cuál tabla.

### 3. Auto-aprobación del cajero en Recounting

**Descripción**: cuando un cajero crea una sesión de recuento (`RecountingSession`) en su propia mesa asignada (`varIsOwnDesk = true`), el sistema hace `Patch(Confirmed: true)` directo y crea el `ApprovalRequest` ya con `ApprovalStatus = Aprobado`, sin intervención de un supervisor. El propio comentario en el código lo marca como fix: `// ✅ ESTE ES EL FIX DEFINITIVO`.

**Evidencia**: `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/Screen1.fx.yaml`, bloque de confirmación de sesión de recuento.

**Impacto**: brecha de segregación de funciones en un proceso de bóveda — el cajero que cuenta el efectivo también autoaprueba que su propia sesión de conteo sea válida, sin un segundo par de ojos. Vale confirmar con negocio si es una decisión consciente (por ejemplo, para no bloquear operación cuando el supervisor no está disponible) o un atajo de desarrollo.

**Esfuerzo**: M — requiere decisión de negocio primero; el cambio de Power Fx en sí (forzar `ApprovalRequest` pendiente siempre, sin importar la mesa) es acotado a una app.

### 4. Ninguna app operativa verifica rol/grupo AAD en su propio código

**Descripción**: Reception, Recounting, Supervisor, Digitization y Deposits Tracker no referencian `sanic_CHEVAADGroup_*` ni ningún mecanismo de verificación de rol en Power Fx. La identidad se resuelve, cuando se resuelve, solo con `LookUp(Users, 'Primary Email'=User().Email)` — sin chequear a qué grupo/rol pertenece ese usuario.

**Evidencia**: ausencia de la cadena `sanic_CHEVAADGroup_` en los `Src/*.fx.yaml` de esas 5 apps (confirmado en [03-apps-canvas.md](03-apps-canvas.md) §1).

**Impacto**: el control de "quién puede aprobar/contabilizar faltantes-sobrantes" (Supervisor) o "quién puede recibir tulas" (Reception) depende exclusivamente de compartir/no compartir la app y de los Security Roles subyacentes — que, por el hallazgo #1/#2, están incompletos para varias tablas. Combinado, esto significa que cualquier usuario con la app compartida y algún rol con privilegio de escritura puede ejecutar acciones fuera de su función física.

**Esfuerzo**: L — hay que instrumentar 5 apps con un patrón de gate consistente (idealmente reutilizando las 6 variables `sanic_CHEVAADGroup_*` que ya existen en Core sin consumidores en estas apps), y decidir el comportamiento cuando el chequeo falla (bloquear pantalla, ocultar botones, o ambos).

---

## Integridad de datos

### 5. El monto de la diferencia (faltante/sobrante) no se persiste

**Descripción**: `sanic_CHTblTransactions.sanic_transactiontype` solo guarda una etiqueta categórica (`Deposito`/`Faltante`/`Sobrante`/`Sin declaracion`). No existe un campo numérico que guarde **cuánto** fue la diferencia — el cálculo (`Diferencia = recontado - declarado`) ocurre en Power Fx dentro de la app Supervisor al momento de aprobar, pero el resultado numérico no se guarda en ningún lado, solo la clasificación.

**Evidencia**: `solutions/unpacked/core/Entities/sanic_CHTblTransactions/Entity.xml` (sin campo de monto de diferencia); lógica de cálculo en `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/Screen Approvals.fx.yaml`.

**Impacto**: cualquier reporte o auditoría de "cuánto se pierde/sobra en promedio por cliente/cajero/período" requiere recalcular restando `DepositsRecounting` contra `DeclaredDeposits` fuera de Dataverse — no hay una fuente única de verdad para ese número, con riesgo de que el cálculo diverja del que se hizo en el momento real de la aprobación.

**Esfuerzo**: M — agregar un campo decimal (`sanic_differenceamount` o similar) a `Transactions`, y modificar la fórmula de Supervisor para persistirlo junto al `TransactionType` al momento del `Patch`.

### 6. Estado del proceso fragmentado en al menos 5 option sets desconectados

**Descripción**: no existe un "estado maestro" del expediente de depósito de punta a punta. El estado vive repartido en: `ReceptionBatch.ReceptionStatus`, `Bags.BagStatus`, `RecountingSessionDetail` (global `sanic_chrecountingdetailsstatus`), `Transactions.TransactionType/Status`, `TransactionBatches.BatchStatus` — sin relación declarada entre ellos.

**Evidencia**: `solutions/unpacked/core/Entities/sanic_CHTblReceptionBatch/Entity.xml`, `sanic_CHTblBags/Entity.xml`, `sanic_CHTblRecountingSessionDetail/Entity.xml`, `sanic_CHTblTransactions/Entity.xml`, `sanic_TransactionBatches/Entity.xml`, y `solutions/unpacked/core/OptionSets/sanic_chrecountingdetailsstatus.xml`.

**Impacto**: saber "en qué etapa está un depósito" hoy requiere consultar y correlacionar 5 tablas distintas; la app Deposits Tracker hace exactamente eso manualmente. Cualquier reporte ejecutivo de estado global necesita la misma correlación manual, con riesgo de inconsistencia si una de las 5 no se actualiza en el momento correcto.

**Esfuerzo**: L — diseñar un estado maestro (campo calculado, tabla de "expediente" nueva, o vista consolidada) es una decisión de arquitectura de datos que toca las 3 apps operativas.

### 7. Relaciones bidireccionales inconsistentes

**Descripción**: `Bags`↔`DeclaredBags` y `DeclaredDeposits`↔`DepositsRecounting` tienen lookups cruzados en ambos sentidos (una tabla apunta a la otra y viceversa). El par equivalente de cheques, `Checks`↔`DeclaredChecks`, **no** es bidireccional: solo `Checks.sanic_RelatedDeclaredCheck → DeclaredChecks`, sin el lookup inverso.

**Evidencia**: `solutions/unpacked/core/Entities/sanic_CHTblBags/Entity.xml` (`sanic_bagdeclarationid`), `sanic_CHTblDeclaredBags/Entity.xml` (`sanic_bagid`), `sanic_CHTblDeclaredDeposits/Entity.xml` (`sanic_relatedrecounteddeposit`), `sanic_CHTblDepositsRecounting/Entity.xml` (`sanic_relateddeclareddeposit`), `sanic_CHTblChecks/Entity.xml` (`sanic_relateddeclaredcheck`).

**Impacto**: bajo en el día a día (las apps ya saben qué dirección usar), pero alto para cualquier desarrollador nuevo que intente navegar el modelo de datos por primera vez o generar reportes genéricos — el patrón "declarado↔real" no es predecible entre los 3 pares que deberían seguirlo igual.

**Esfuerzo**: M — decidir un patrón único (¿siempre bidireccional? ¿siempre unidireccional con el sentido "real→declarado"?) y agregar el lookup faltante en `DeclaredChecks→Checks`, o remover la redundancia en los otros dos pares — cualquiera de las dos direcciones es un cambio de esquema con impacto en las apps que ya usan las relaciones actuales.

### 8. Campo `cr140_changestatus` con prefijo de otro publisher

**Descripción**: el campo de estado más crítico del módulo de RFC de cambios de cliente, `ChangeStatus` en `sanic_CHTblClientDataChangeRequest`, tiene `LogicalName = cr140_changestatus` — prefijo ajeno a `sanic_`, evidencia de un campo importado de otra solución/publisher. Su optionset mezcla rangos `796490xxx` con un valor suelto `159460001` (Canceled).

**Evidencia**: `solutions/unpacked/core/Entities/sanic_CHTblClientDataChangeRequest/Entity.xml`; los 4 flujos webhook (`Activaterow`, `Deactivaterow`, `Createclientdata`, `Applyclientdatachanges`, `Notifyreviewers`, `NotifywhenRFCimplementationfinish`) filtran su trigger sobre este campo exacto (`cr140_changestatus eq 796490002`, etc., en `solutions/unpacked/flow/Workflows/`).

**Impacto**: confuso para cualquiera que audite el módulo esperando el prefijo `sanic_` consistente; más grave, el campo no se puede renombrar (`LogicalName`) sin recrearlo, y recrearlo rompe los 6 flujos que filtran su webhook exactamente sobre ese nombre — cualquier corrección exige coordinar el cambio en Core y en los 6 flujos de Flow a la vez.

**Esfuerzo**: L — migración de campo (crear `sanic_changestatus`, copiar datos, repuntar los 6 triggers de flujo y todas las referencias en las 2 apps de administración de clientes) antes de poder deprecar el campo viejo.

---

## Operación

### 9. `CH-Flow-IngestEmails` desactivado (Draft)

**Descripción**: el flujo que dispara toda la cadena (recepción de correo del cliente) está exportado en `StateCode=0/StatusCode=1` — el único de los 19 flujos sin activar.

**Evidencia**: `solutions/unpacked/flow/Workflows/CH-Flow-IngestEmails-4FDD7239-309E-F011-BBD2-7CED8D1ED971.json.data.xml`.

**Impacto**: crítico — si este export refleja el ambiente real, el sistema completo no está procesando correos entrantes; nada aguas abajo (Email Validation Console, recepción, recuento, digitación) tiene con qué trabajar.

**Esfuerzo**: S — activar el flujo tras verificar que el buzón configurado sea el correcto (ítem #10).

### 10. Buzón compartido hardcodeado en el trigger

**Descripción**: el trigger de `IngestEmails` apunta literalmente a `no-reply-powerplatform-dev@baccredomatic.ni` en el JSON del flujo, en vez de leer `sanic_CHEVSharedMailboxAddress` (que existe en Core con ese mismo valor de dev como default).

**Evidencia**: `solutions/unpacked/flow/Workflows/CH-Flow-IngestEmails-4FDD7239-309E-F011-BBD2-7CED8D1ED971.json` (parámetros del trigger `SharedMailboxOnNewEmailV2`).

**Impacto**: alto — imposibilita promover la solución entre ambientes (dev/test/prod) sin editar manualmente el JSON del flujo cada vez; si se olvida, el ambiente productivo escucharía el buzón de desarrollo.

**Esfuerzo**: S — editar el trigger para que lea la variable de entorno en vez del literal (cambio de una acción en un flujo), luego probar en cada ambiente.

### 11. Excel temporal en OneDrive nunca se borra

**Descripción**: `CH-Flow-ExtractedTablesFromExcel` sube el adjunto a `/tmp` en OneDrive for Business para poder leerlo con el conector de Excel Online, pero no hay ninguna acción `DeleteFile` en el flujo — el archivo queda ahí indefinidamente.

**Evidencia**: `solutions/unpacked/flow/Workflows/CH-Flow-ExtractedTablesFromExcel-C90419B3-A09E-F011-BBD2-7CED8D1ED971.json` (acciones `CreateFile`/`GetTables`/`GetItems` sin `DeleteFile` posterior).

**Impacto**: con uso real (un archivo por cada correo procesado), esto acumula archivos sin límite — consumo de storage creciente y sin política de retención visible para ese contenido (que además puede tener datos sensibles del cliente).

**Esfuerzo**: S — agregar un paso `DeleteFile` al final del flujo (o un flujo de limpieza programado aparte si se prefiere desacoplar).

### 12. TRNCODE e identidad de agencia hardcodeados en la carga masiva IBS

**Descripción**: la app Digitization arma el layout de columnas para IBS Branch con varios valores fijos: `TRNCODE=8004`, `DISPONIBILIDADINMEDIATA="0"`, `MANIFIESTO=1`, `TIPOID="0000000000001"`, `NACIONALIDAD="0000000000558"`, `NUMEROID1="4411001991"`, `NUMEROID2="003A"`. Existe `sanic_CHEVIbsBranchCodeList` en Core pero no está siendo leída por la app.

**Evidencia**: `solutions/unpacked/apps/CanvasApps/src/sanic_chappdigitization_278aa/Src/Screen1.fx.yaml` (armado de `colTx` en el botón "Descargar lote"); `solutions/unpacked/core/environmentvariabledefinitions/sanic_CHEVIbsBranchCodeList/environmentvariabledefinition.xml`.

**Impacto**: medio-alto — sugiere que todo el lote se carga bajo una identidad genérica/de agencia única en IBS Branch, no bajo la identidad de cada depositante o sucursal real; si el negocio opera con múltiples agencias/identidades, esto podría estar generando cargas incorrectas (a confirmar con negocio, no concluyente solo desde el código).

**Esfuerzo**: M — refactorizar la fórmula de armado de `colTx` para leer de `sanic_CHEVIbsBranchCodeList` o de una tabla de mapeo por sucursal, en vez de literales.

### 13. VR02 y VR04 no validan realmente nada

**Descripción**: `VR02` (debería validar dominio/formato del email remitente) responde `Pass` incondicional sin ningún `If`. `VR04` (debería validar extensión de adjunto) calcula la comparación pero **ambas** ramas del `If` responden `Pass` — el bug hace que nunca pueda fallar.

**Evidencia**: `solutions/unpacked/flow/Workflows/Ch-Flow-VR02-7AD3F050-B59E-F011-BBD2-7CED8D1ED971.json`, `solutions/unpacked/flow/Workflows/CH-Flow-VR04-225BA167-B59E-F011-BBD2-7CED8D1ED971.json`.

**Impacto**: bajo hoy (estos flujos no están conectados a nada — la validación real vive en Email Validation Console, ver [01-arquitectura.md](01-arquitectura.md) §3), pero alto si en algún momento se retoma el plan de mover la validación al backend vía estos flujos sin corregir el bug primero.

**Esfuerzo**: S cada uno — el fix de VR04 es cambiar la respuesta de la rama `then` de `Pass` a un resultado derivado de la comparación; VR02 requiere agregar la lógica de validación de dominio que hoy no existe (más que un fix, es completar la regla desde cero).

### 14. `ValidationRulesOrchestrator` es un stub vacío

**Descripción**: el único `Switch` del flujo tiene `case "VR01": {actions: {}}` y `default: {actions: {}}` — no llama a ningún VR0x, no responde nada.

**Evidencia**: `solutions/unpacked/flow/Workflows/CH-Flow-ValidationRulesOrchestrator-21A1F8D2-B79E-F011-BBD2-7CED8D1ED971.json`.

**Impacto**: bajo mientras la validación siga viviendo en Email Validation Console (arquitectura actual real). Alto si se documenta o comunica como si fuera el orquestador activo — cualquiera que lea el nombre del flujo asume que orquesta algo.

**Esfuerzo**: depende de la decisión de arquitectura — **S** si se opta por documentar/eliminar el flujo (dejar constancia de que la validación vive en la app); **L** si se opta por completarlo de verdad como reemplazo del motor `EvaluateEV` de la app (implica reescribir 23 reglas como flujos o child-flows, más migrar la app para invocarlo).

### Adicional — gap silencioso en cambios de tipo Option Set (RFC de cliente)

**Descripción**: en `Createclientdata` y `Applyclientdatachanges`, el caso del switch para `sanic_datatype = OptionSet (159460005)` tiene acciones vacías — un RFC que cambie un campo de tipo Option Set queda sin ese campo actualizado, sin generar ningún error visible.

**Evidencia**: `solutions/unpacked/flow/Workflows/CH-Flow-Createclientdata-D521D4BC-4AA9-F011-BBD2-7C1E5280D4AC.json`, `solutions/unpacked/flow/Workflows/CH-Flow-Applyclientdatachanges-458DDC1F-0BA9-F011-BBD2-7C1E5280D4AC.json`.

**Impacto**: medio — falla silenciosa; el RFC se marca `Applied` igual aunque el campo Option Set solicitado nunca se haya tocado, generando una discrepancia entre lo que el aprobador cree que pasó y lo que realmente se aplicó.

**Esfuerzo**: S — completar el `case` faltante en ambos flujos con la conversión de valor de option set (mapear label→valor numérico).

---

## Calidad de código

### 15. Typos en `LogicalName`s no corregibles sin recrear el campo

**Descripción**: `sanic_AlternetKey_CIFBCO`/`CIFCOM` (Client, "Alternet" por "Alternate"), `sanic_ReasonForForcedFlosure` (RecountingSession, "Flosure" por "Closure"), `sanic_opeationtype` (ClientDataChangesAuditTrail, falta la "r" de "Operation"), `sanic_fileextention` (EmailsAttachments), `sanic_otherlocacheckamount` (DepositsRecounting, falta una "l"). Además la inconsistencia "esclusa"/"exclusa": el esquema vivo (`sanic_CHTblBags/Entity.xml`) usa la grafía correcta `'Recibida en esclusa'` y la app Recounting coincide con él; son Reception, Supervisor, Deposits Tracker y Email Validation Console las que conservan una copia local cacheada del esquema (`pkgs/TableDefinitions/CH-Tbl-Bags.json`) desincronizada con la grafía `'exclusa'` (ver [typos conocidos §6](../referencia/typos-conocidos.md), que corrige la dirección reportada originalmente en el análisis).

**Evidencia**: `solutions/unpacked/core/Entities/sanic_CHTblClient/Entity.xml`, `sanic_CHTblRecountingSession/Entity.xml`, `sanic_CHTblClientDataChangesAuditTrail/Entity.xml`, `sanic_CHTblEmailsAttachments/Entity.xml`, `sanic_CHTblDepositsRecounting/Entity.xml`; typo funcional en `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/Screen1.fx.yaml` (~línea 8968/9918).

**Impacto**: los typos de `LogicalName` son cosméticos (afectan legibilidad de queries/código, no comportamiento) — bajo impacto pero no corregibles sin migración de esquema. Las copias de esquema desincronizadas ("exclusa") probablemente no afecten runtime — las comparaciones de option set se resuelven por valor numérico, no por etiqueta — pero son deuda de sincronización: cualquier edición de esas apps contra el esquema viejo puede introducir errores sutiles.

**Esfuerzo**: S para resincronizar el esquema cacheado de las 4 apps desactualizadas (refrescar el origen de datos y republicar); L si se decide además recrear los campos con `LogicalName` corregido (migración de datos + actualizar todas las referencias en flujos/apps).

### 16. Componentes compartidos (`sanic_chclsharedcomponents`) incompletos y sin uso

**Descripción**: la biblioteca dedicada a componentes compartidos existe pero `cmpAppHeader` no tiene ninguna propiedad de entrada/salida (placeholder sin terminar) y `cmpTheme` es un set estático de un único tema. Ninguna de las 8 canvas apps de negocio la consume — cada una mantiene su propio `Dialog`/`Header`/`Panel`/`ExpandMenu` local duplicado.

**Evidencia**: `solutions/unpacked/apps/CanvasApps/src/sanic_chclsharedcomponents_85fca/` (CustomProperties vacías de `cmpAppHeader`); `ComponentReferences.json` vacío o sin referencia a esta librería en las 8 apps de negocio.

**Impacto**: bajo-medio — cualquier cambio visual (color, layout de header, menú) requiere tocar cada app por separado, con riesgo creciente de divergencia visual/funcional entre apps a medida que cada una evoluciona su copia local por separado.

**Esfuerzo**: M para completar `cmpAppHeader` con propiedades reales; L si además se migra cada una de las 3+ apps que ya tienen su propio header/menú/dialog local para que consuman la librería compartida.

### 17. Código de debug, comentado y duplicado sin limpiar

**Descripción**: `Notify("Entra")` en un branch del botón Aprobar de Supervisor; bloque `OnVisible` de Recounting duplicado dos veces consecutivas (la segunda marcada `// CODIGO NUEVO: 17/06/2026`); bloque de código comentado (`/* ... */`) sin remover en Recounting; nombre de acción engañoso en el flujo `Activaterow` (`Actualizar_cliente_a_INACTIVE` para la acción que en realidad pone el registro en **Active**, copy-paste de `Deactivaterow` sin renombrar).

**Evidencia**: `solutions/unpacked/apps/CanvasApps/src/sanic_chappsupervisor_75678/Src/Screen Approvals.fx.yaml` (~línea 1554); `solutions/unpacked/apps/CanvasApps/src/sanic_chapprecounting_54962/Src/Screen1.fx.yaml` (`OnVisible` duplicado, bloque comentado ~línea 9200); `solutions/unpacked/flow/Workflows/CH-Flow-Activaterow-D3A3460E-BDAA-F011-BBD2-000D3A52EBA2.json` (nombre de acción).

**Impacto**: bajo funcionalmente (el valor que setea `Activaterow` es correcto pese al nombre; `Notify("Entra")` no rompe nada), pero es ruido real para cualquiera que audite o dé mantenimiento — señal de cambios que se subieron a producción sin una pasada de limpieza/code review.

**Esfuerzo**: S — pasada de limpieza puntual en los 2 archivos de app y renombrado de la acción del flujo (renombrar una acción de Power Automate no requiere reimportar dependencias externas, solo republicar el flujo).

## Ver también

- [01-arquitectura.md](01-arquitectura.md) — contexto de cada componente mencionado acá.
- [03-apps-canvas.md](03-apps-canvas.md) — detalle de patrones por app referenciado en los ítems de Seguridad y Calidad.
- [04-deploy-y-alm.md](04-deploy-y-alm.md) — cómo los ítems de Operación (#9-#12) condicionan el checklist de despliegue.
