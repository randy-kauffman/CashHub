# CashHubApps — cada aplicación

> Fuente: `solutions/unpacked/apps/` (versión **1.15.3.0**, unmanaged). La
> solución contiene **solo canvas**: 8 apps + 2 component libraries. No hay
> model-driven apps, custom pages ni PCF propios; las tablas, flujos, variables
> de entorno y PCF del Creator Kit se declaran como `MissingDependencies`.
> Datasources: se listan los que la app **usa en fórmulas**; los declarados sin
> uso se marcan como residuales.

## Resumen

| App | Usuario | Propósito |
|---|---|---|
| CH-App-Hub | Cualquier usuario | Lanzadera: muestra las apps del catálogo según grupo AAD y las abre con `Launch()`. |
| CH-App-Reception | Recepción de valores | Crear lotes de recepción (planillas + tulas) y administrarlos; correcciones vía ApprovalRequests. |
| CH-App-Recounting | Cajero / recontador | Sesión de recuento por tula: efectivo y cheques por moneda contra lo declarado; lectura XML de contadora; aprobaciones. |
| CH-App-Supervisor | Supervisor de tesorería | Resolver ApprovalRequests y monitorear: localización de tulas, resumen por cajero, administración de cheques. |
| CH-App-Digitization | Digitación / aplicación contable | Transacciones y lotes: marcar Ingresado/Aplicado y descargar el CSV del lote. |
| CH-App-DepositsTracker | Consulta / soporte | Rastrear un identificador (lote, planilla, tula) y ver su recorrido completo. |
| CH-App-ClientAdministration | Banca de Empresas | Clientes con sucursales/cuentas/correos + ciclo completo de RFC. |
| CH-App-EmailValidationConsole | Operador de contingencia + Banca de Empresas | Bandeja de trabajo de correos que el motor no pudo resolver: playbook, override auditado, lectura del buzón vía Graph. |
| CH-CL-SharedComponents | — | Component library propia (`cmpAppHeader`, `cmpTheme`). **Hoy nadie la referencia.** |
| Power CAT Component Library | — | Creator Kit (`Dialog`, `ExpandMenu`, `Header`, `Panel`…), consumido por 5 apps. |

## CH-App-Hub

`OnStart` arma `colAppData` desde `CH-Tbl-AppCatalogs`: primero las filas con
grupo AAD donde `Office365Groups.ListGroupMembers()` contiene al usuario, luego
las habilitadas sin grupo (para todos). Una pantalla: galería de tarjetas
(ícono, nombre, descripción) que lanza `Launch(AppURL, {}, Replace)`.

- Datasources: AppCatalogs, Environment Variable Definitions/Values, conector
  Office365Groups.
- **Hallazgo**: la galería está ligada a `'CH-Tbl-AppCatalogs'` crudo, no a
  `colAppData` — el filtro por grupo se calcula pero no se aplica al render.

## CH-App-Reception

Dos pantallas grandes:

- **CreacionLotesRecepcion**: formulario de recepción (lote, cliente,
  sucursal), grilla de planillas con totales por moneda, botones Nuevo /
  Validar / Guardar / Cerrar lote; al confirmar, `Patch` del ReceptionBatch y
  alta de tulas en Bags.
- **AdministracionDeLotes**: consola de lotes recibidos (pivot Planilla/Lote,
  filtros por estado y fechas). Toda corrección genera una fila en
  ApprovalRequests: "Agregar tula", "Eliminar tula", "Remapear tula",
  "Modificar numero de planilla", "Editar tula" — nada se edita en caliente
  sin aprobación del supervisor.

Datasources: Receptions, ReceptionBatches, Bags, Clients, ClientBranches,
TransportCompanies, ApprovalRequests, Users. Sin flujos. Creator Kit: Dialog,
ExpandMenu, Header, Panel + PCF (FluentDetailsList, Command Bar, SearchBox,
Keyboard Shortcuts, Progress Bar).

## CH-App-Recounting

Una pantalla operativa (`Screen1`; `Screen2` está vacía). `OnVisible` carga la
sesión del día del cajero y las denominaciones. Bloques de captura de efectivo
y cheques por NIO/USD/EUR, comparativa Rec/Dec/Diff por moneda, selección de
mesa (CountingDesk), timer de espera de aprobación del supervisor y ~8 diálogos
de confirmación. Genera ApprovalRequests: "Primera tula en planilla", "Tula sin
declaracion", "Crear sesion de recuento", "Tula recontada sin declaracion",
"Tula con diferencia".

- **Flujo invocado**: `'CH-Flow-ProcessXMLFiles'.Run(ID_Sesion, Serial_Maquina)`
  para leer los XML de la máquina contadora — **flujo hoy apagado**.
- Datasources (18 tablas): RecountingSessions(+Details), DepositsRecountings,
  CountedMoneyDetails, CountingDesks, MoneyDenominations, Bags, Checks,
  Receptions, DeclaredBags/Checks/Deposits, Transactions,
  XMLReadings(+Details), ApprovalRequests, Received Emails, Users.
  Residual: Declarations. Creator Kit: Dialog.

## CH-App-Supervisor

Cuatro pantallas (menú `gvarNavItems`):

- **Approvals**: bandeja de ApprovalRequests, toggle pendientes/resueltas,
  Aprobar / Rechazar / Cancelar.
- **BagLocation**: localización de tulas por pivot Esclusa / Bóveda temporal /
  Declaraciones pendientes / Procesadas, con filtros.
- **OperationSummary**: KPIs de tulas recontadas, monto recontado, cheques; y
  el resumen por cajero-mesa con totales por moneda.
- **ChecksSummary**: administración de cheques (KPIs, detalle por cajero) y el
  botón **"Enviar a digitacion"** que mueve los cheques Recontados al dominio
  de digitación.

Datasources (13): ApprovalRequests, Bags, Checks, DeclaredBags/Deposits,
DepositsRecountings, ReceptionBatches (residual), Receptions,
RecountingSessions(+Details), Transactions, CountingDesks (residual), Users.
Sin flujos. Creator Kit: ExpandMenu.

## CH-App-Digitization

Una pantalla con pivot "Tx sin lote / Tx Manuales / Lotes" + "Realizar corte":

- Transacciones: formulario (estado, tipo, moneda), "Marcar como Aplicado",
  detalle de cheques asociados.
- Lotes: totales por moneda, "Marcar como Ingresado/Aplicado" y **"Descargar
  lote"**: `'CH-Flow-DownloadData'.Run(JSON(colTx), varLoteSelected.Name)` →
  `Download()` del CSV de carga masiva IBS.

Datasources: Transactions, TransactionBatches, Checks, DepositsRecountings
(residual), CH-Flow-DownloadData. Sin component libraries.

## CH-App-DepositsTracker

App de solo lectura (en inglés). Una pantalla: búsqueda por identificador con
tipo `Lote de recepcion / Planilla / Tula` y pestañas Reception / Bag /
Declaration / Checks / Deposits / Timeline sobre el recorrido del depósito.

- Datasources: Bags, Checks, ReceptionBatches, Receptions, DepositsRecountings,
  Users (+ residuales: Clients, DeclaredBags/Deposits, RecountingSessions).
- **Hallazgo**: el bloque "Bag details" tiene valores de demo hardcodeados
  ("RB-110", "PROVAL", nombres propios) — sección aún no enlazada a datos.

## CH-App-ClientAdministration

Cinco pantallas; el acceso por tipo de cambio se resuelve con
ClientDataChangePermission + `Office365Groups.ListGroupMembers()`. Soporta
deep-link `Param("RFC")` desde las notificaciones de Teams.

- **Splash**: enruta según venga o no el parámetro RFC.
- **Clients**: listado maestro + formulario, y creación de RFC (al crear
  cliente, genera las líneas del RFC tipo Create).
- **ClientInformation**: ficha del cliente con pivot Sucursales / Correos
  autorizados / Cuentas / Solicitudes de cambio / Bitácora; bloquea ediciones
  si hay RFC pendiente sobre el registro.
- **RFC**: bandeja de solicitudes con grid Fluent y paginación.
- **ChangeRequestDetails**: detalle del RFC con Aprobar / Rechazar / Enviar /
  Cancelar y edición de líneas limitada a los campos permitidos del metamodelo
  (`CH-Tbl-ClientTableLogicalFieldNames`, `IsAllowed`).

Datasources (11 tablas + conector): todo el dominio cliente + RFC, Users,
Office365Groups. Residual: ClientDepositHistories. Creator Kit: ExpandMenu,
Panel, Dialog + PCF (DVFluentDetailsList(+WithContextMenu), Command Bar,
SearchBox).

## CH-App-EmailValidationConsole — estado actual

**Objetivo**: con el pipeline automático de punta a punta, nadie debería abrir
esta app para los correos que salieron bien. Es la **bandeja de contingencia**:
solo casos que requieren decisión humana. El control de acceso real es el rol
de Dataverse; la app solo oculta botones que no corresponden.

Dos pantallas (la pantalla de plantillas de correo NO existe en la app: su
fuente quedó estacionada en `docs/pendiente/screen-PlantillasCorreo.fx.yaml.guardado`):

### screen ColaDeProcesamiento — la bandeja de trabajo

1. **Banda de salud**: semáforo contra el heartbeat `ingesta-correos` de
   SystemHeartbeat (sin latido / operando ≤20 min / atrasado ≤45 / detenida) +
   4 contadores: Esperando a Banca, Sin validar, Errores, Sin responder.
2. **Vistas**: "Todo lo accionable" (default; en un día sano, vacía),
   "Esperando a Banca" (Revision manual), "Sospechosos", "Errores",
   "Sin responder" — con búsqueda por asunto/remitente.
3. **Panel de detalle** por correo: cabecera (gestión, remitente,
   clasificación), **"Que paso"** (TriageNotes → ErrorText → aviso de que el
   motor no llegó a evaluar), **"Que hacer"** (playbook numerado desde
   CH-Tbl-Playbook por clave de caso), **"Reglas que no pasaron"** (última
   ValidationRun + Results: fallos con hoja/celda y las no intentadas por
   dependencia), y detalle técnico plegable.
4. **Acciones**: Registrar correo (crea RFC de alta de remitente vía
   ClientDataChangeRequests, solo para quien pertenece al grupo aprobador y
   con cliente atribuido), Marcar para reproceso (lo levanta el barrido en ≤10
   min), Reenviar respuesta, Resuelto por fuera (cierra con nota, Omitido) y
   **Denegar/Aceptar manualmente** (override auditado: motivo ≥10 caracteres,
   `OverrideBy/On/Reason`, `EmailReviewMethod = Manual`).

### screen CorreosElectronicos — bandeja del buzón compartido

Vía manual: lee el buzón con `Office365Outlook.HttpRequest` a **Microsoft
Graph** (`Prefer: IdType="ImmutableId"`, top 500), lista correos con adjuntos y
cuerpo, e "Incluir + Validar Manual" crea el ReceivedEmail + adjuntos (base64 →
FileContent) y archiva con `MoveV2`. También "Archivar" sin registrar. Timer de
refresco cada 30 s.

- Conectores: Office365Outlook (HttpRequest/MoveV2), Office365Users,
  Office365Groups. **No invoca ningún flujo Power Automate.**
- Datasources en uso: Received Emails, EmailsAttachments, Playbook,
  SystemHeartbeat, ValidationRules/Runs/Results, ClientDataChangeRequests
  (+Lines), ClientDataChangePermissions, ClientTableLogicalFieldNames, EV
  Definitions/Values. **Residuales** (declarados sin uso): Bags, ClientEmails,
  Clients, Declarations, DeclaredBags/Checks/Deposits, EmailTemplate.
- `OnStart`: el folder de archivado está **hardcodeado** (la lectura de la EV
  `sanic_CHEVSharedMailboxArchiveFolderID` quedó comentada); la pantalla de
  correos sí lo resuelve por EV en su `OnVisible`.

## Component libraries

- **CH-CL-SharedComponents** (`cmpAppHeader`, `cmpTheme`): ninguna app la
  referencia hoy — candidata a adoptarse o eliminarse.
- **Power CAT Component Library** (Creator Kit): Dialog, ExpandMenu, Header,
  Material, Panel. La consumen ClientAdministration, Console, Reception,
  Recounting y Supervisor. Los PCF del Creator Kit (CommandBar, SearchBox,
  FluentDetailsList, DVFluentDetailsList…, KeyboardShortcuts) entran como
  MissingDependencies de las soluciones CreatorKitCore / DataverseAccelerator /
  PowerCATToolkit.

## Hallazgos transversales

1. Ninguna app viaja con connection references de solución: las conexiones van
   "sueltas" en `Connections.json` de cada app (trabajo manual post-import).
2. Datasources residuales en 7 de 8 apps (ruido de ALM; listados arriba).
3. Recounting depende de un flujo apagado (ProcessXMLFiles) y Digitization de
   DownloadData (ON) — únicas dos dependencias app→flujo reales.
4. Hub: filtro por grupo AAD calculado pero no aplicado a la galería.
5. DepositsTracker: sección "Bag details" con datos de demo hardcodeados.
