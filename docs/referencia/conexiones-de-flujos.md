# Conexiones de los cloud flows — estado y plan de normalización

> Inventario de las connection references que usan los 11 flujos de CashHub, verificado el
> 2026-08-31 contra DEV (`org36e60d9d`).
>
> **Fuente**: el nodo `connectionReferences` de cada `Workflows/*.json`, cruzado contra
> `Other/Customizations.xml` de cada solución y contra la tabla `solutioncomponents` del ambiente.

## Lo primero, la buena noticia

**Las 22 conexiones de los 11 flujos son por *connection reference*. Cero conexiones directas.**

Eso significa que el problema es de **higiene**, no de arquitectura: los flujos ya están
preparados para viajar entre ambientes; lo que falla es *a qué referencia* apuntan.

## El problema: 12 referencias distintas para 6 conectores

| Conector | Flujos | Referencia propia de CashHub | Estado |
|---|---|---|---|
| `shared_commondataserviceforapps` | 9 | `sanic_CHCRDataverse` (**CH-CR-Dataverse**) | ✅ existe |
| `shared_office365` (Outlook) | 4 | `sanic_CHCROutlook` (**CH-CR-Outlook**) | ✅ existe |
| `shared_onedriveforbusiness` | 1 | `sanic_CHCROneDrive` (**CH-CR-OneDrive**) | ✅ existe, **sin usar** |
| `shared_teams` | 4 | — | ❌ **falta crear** |
| `shared_office365users` | 1 | — | ❌ **falta crear** |
| `shared_office365groups` | 1 | — | ❌ **falta crear** |

`CashHubCore` ya tiene **4** referencias `CH-CR-*`: Dataverse, Outlook, OneDrive y Sharepoint.
La de Sharepoint no la usa ningún flujo.

## Hay que crear 3

Con el publisher `sanic_` y agregándolas a **CashHubCore**, siguiendo la convención `CH-CR-*`:

| Nombre sugerido | Conector | Para qué |
|---|---|---|
| **CH-CR-Teams** | `shared_teams` | Alertas a Teams: `MailboxSweep`, `Notifyreviewers`, `NotifywhenRFCimplementationfinish`, `CreateaTeamschatgroup` |
| **CH-CR-Office365Users** | `shared_office365users` | Resolver miembros: `CreateaTeamschatgroup` |
| **CH-CR-Office365Groups** | `shared_office365groups` | Listar el grupo O365: `CreateaTeamschatgroup` |

## El riesgo concreto: 3 referencias compartidas con otros proyectos

Esto es lo más serio del diagnóstico. Estas referencias **pertenecen también a la solución de otro
proyecto**, verificado en `solutioncomponents`:

| Referencia | Display | También vive en |
|---|---|---|
| `sanic_hb_conr_dataverse` | HB \| CONR \| Dataverse | **`sanic_heartbeat`** |
| `wf_sharedcommondataserviceforapps_3bc81` | Microsoft Dataverse WFEngineEval | **`WFEngineEvaluation`**, **`smlt_core_smelter`** |
| `sanic_conn_wfe_office365outlook` | CONN \| WFE \| Office 365 Outlook | **`WFE_Core_WorkflowEngine`** |

> Si alguien actualiza o quita esas referencias desde la solución del **otro** proyecto, los flujos
> de CashHub que las usan se rompen. Y nadie de este lado se entera hasta que falla.

Las otras cuatro ajenas (`sanic_AEDDConnDataverse`, `sanic_CEADCONNOutlook365`,
`new_sharedteams_084e4`, `new_sharedonedriveforbusiness_a6c30`) no están en la solución de otro
proyecto, pero tampoco son de CashHub: dos llevan el prefijo `new_`, o sea el **publisher por
defecto**, y dos tienen nombre de proyectos ajenos.

## Los 14 reemplazos

Cuando estén creadas las 3 nuevas:

| Flujo | Conector | Usa hoy | Reemplazar por |
|---|---|---|---|
| `CreateaTeamschatgroup…` | office365groups | `sanic_sharedoffice365groups_cff38` | **CH-CR-Office365Groups** |
| `CreateaTeamschatgroup…` | office365users | `sanic_sharedoffice365users_dd86c` | **CH-CR-Office365Users** |
| `CreateaTeamschatgroup…` | teams | `new_sharedteams_084e4` | **CH-CR-Teams** |
| `DownloadData` | dataverse | `sanic_hb_conr_dataverse` | **CH-CR-Dataverse** |
| `DownloadData` | dataverse | `wf_sharedcommondataserviceforapps_3bc81` | **CH-CR-Dataverse** |
| `IngestEmails` | office365 | `sanic_conn_wfe_office365outlook` | **CH-CR-Outlook** |
| `MailboxSweep` | teams | `new_sharedteams_084e4` | **CH-CR-Teams** |
| `Notifyreviewers` | dataverse | `sanic_AEDDConnDataverse` | **CH-CR-Dataverse** |
| `Notifyreviewers` | teams | `new_sharedteams_084e4` | **CH-CR-Teams** |
| `NotifywhenRFCimplementationfinish` | teams | `new_sharedteams_084e4` | **CH-CR-Teams** |
| `ProcessEmail` | office365 | `sanic_conn_wfe_office365outlook` | **CH-CR-Outlook** |
| `ProcessXMLFiles` | dataverse | `sanic_sharedcommondataserviceforapps_025cb` | **CH-CR-Dataverse** |
| `ProcessXMLFiles` | onedrive | `new_sharedonedriveforbusiness_a6c30` | **CH-CR-OneDrive** |
| `Replytoclientemail` | office365 | `sanic_CEADCONNOutlook365` | **CH-CR-Outlook** |

Las 8 restantes ya apuntan a `CH-CR-Dataverse` o `CH-CR-Outlook` y no se tocan.

> **`DownloadData` usa DOS referencias del mismo conector** (`sanic_hb_conr_dataverse` y
> `wf_sharedcommondataserviceforapps_3bc81`), las dos de proyectos ajenos. Es el flujo más
> desordenado del lote y conviene revisarlo con cuidado: puede que tenga dos acciones Dataverse
> apuntando a conexiones distintas sin motivo.

## Deuda secundaria: referencias ajenas dentro de CashHubFlows

`CashHubFlows` arrastra **15 connection references** en su `Customizations.xml`, y varias no las usa
ningún flujo suyo:

`sanic_AEDDConnOneDrive`, `sanic_CEADCONNExcelOnlineBusiness`, `sanic_sharedoffice365users_3b441`
(un segundo Office 365 Users duplicado), `sanic_sharedonedriveforbusiness_0d05f`.

Una vez hecho el reemplazo, todas las referencias ajenas deberían salir de la solución. Eso es lo
que hace que la solución sea realmente portable.

## Nota de método

Las connection references **no se exportan en una carpeta `connectionreferences/`** — viven en
`Other/Customizations.xml`. Buscarlas por carpeta da cero y hace creer que no existen.

Su `componenttype` en `solutioncomponents` es **10150**, no 10059. Con el número equivocado, el
`Solution.xml` parece no tener ninguna.
