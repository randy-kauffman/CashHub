# Guía: poner a User 5 como cuenta de servicio de los flujos

> Preparado el 2026-08-31 contra DEV (`org36e60d9d`). Los pasos que tocan el portal los hace una
> persona; los que tocan privilegios pueden ir por script.

## El concepto que decide todo

Un cloud flow **no corre con la identidad de su dueño**. Corre con la identidad de la **conexión**
que hay detrás de cada connection reference.

Por eso mover el dueño del flujo no cambia nada. Lo que hay que mover son **las conexiones**.

## Estado de hoy

Las 7 connection references `CH-CR-*` pertenecen a **Randy Kauffman** — una cuenta personal:

| Referencia | Dueño |
|---|---|
| CH-CR-Dataverse, Outlook, Teams, OneDrive, Office365Users, Office365Groups, Sharepoint | Randy Kauffman |

Y los flujos están repartidos entre dos dueños: 7 de Randy Kauffman y 4 de `PowerPlatform-Dev`.

> **El riesgo actual**: si esa cuenta cambia la contraseña, se le revoca el acceso o se va, los 11
> flujos dejan de correr. Sin aviso previo.

## Paso 0 — Requisitos de User 5

- **Licencia**: necesita Power Automate (o la incluida en M365 según los conectores). Sin licencia
  las conexiones no se pueden crear.
- **Contraseña sin expiración** y **sin MFA interactivo**, o las conexiones se van a caer cada vez
  que caduque el token. Esto es lo que en la práctica rompe más cuentas de servicio.
- Conviene marcarla en AAD como cuenta de servicio para que no entre en las políticas de rotación
  de usuarios normales.

## Paso 1 — Ampliar el rol ✅ HECHO

El rol existe y está pensado para esto, pero **cubre 4 tablas y los 11 flujos tocan 16**.

Faltan **13 privilegios**:

```
prvReadsanic_chtblclient
prvReadsanic_chtblclientemail
prvReadsanic_chtblclientdatachangerequest
prvReadsanic_chtblclientdatachangerequestline
prvReadsanic_chtblclientdatachangepermission
prvWritesanic_chtblclientdatachangepermission
prvReadsanic_chtbltransactions
prvWritesanic_transactionbatches
prvCreatesanic_chtblxmlreading
prvReadsanic_chtblxmlreading
prvWritesanic_chtblxmlreading
prvCreatesanic_chtblxmlreadingdetail
prvWritesanic_chtblemailsattachments
```

**Profundidad**: `Global` en todos. Un flujo no tiene "sus" registros — procesa los de todos.

`CH-Tbl-Parameter` la leen los flujos y **no necesita privilegio**: es `UnOwned`.

> **Ojo con el nombre del rol.** Si va a correr los 11 flujos y no solo los de ingesta, conviene
> renombrarlo a algo como `CH-SR-FlowService`. El rename conserva el GUID y las asignaciones —
> igual que hicimos con `CH-Role` → `CH-SR-Hub`.

## Paso 2 — Asignar el rol a User 5 ✅ HECHO

Portal de administración → Usuarios → User 5 → Administrar roles → marcar el rol.

También necesita **Basic User**, que es lo mínimo de plataforma. Los demás usuarios ya lo tienen.

> **Pasos 1 y 2 ejecutados el 2026-08-31** con `scripts/setup-cuenta-servicio-flujos.py`:
> se agregaron los 13 privilegios, el rol se renombró a **`CH-SR-FlowService`** (el `roleid`
> `d7d07f26-…` no cambió) y se le asignó a User 5 junto con **Basic User** — que no tenía, y sin
> el cual la cuenta no opera en Dataverse.
>
> Verificado: el rol cubre las 13 tablas que tocan los flujos con las operaciones exactas.
>
> **Lo que sigue es de portal y queda de tu lado.**

## Paso 3 — Crear las 7 conexiones COMO User 5

Este es el paso que realmente cambia la identidad de ejecución, y **hay que hacerlo iniciando
sesión como User 5**, no delegando.

1. Entrar a `make.powerapps.com` **con la cuenta de User 5**
2. Conexiones → Nueva conexión, y crear una por conector:
   `Microsoft Dataverse`, `Office 365 Outlook`, `Microsoft Teams`, `Office 365 Users`,
   `Office 365 Groups`, `OneDrive for Business` *(y SharePoint si se piensa usar)*
3. Autenticar cada una con las credenciales de User 5

> No sirve crear las conexiones desde tu cuenta y "compartirlas": la conexión guarda el token de
> quien la autenticó. Tiene que ser User 5 el que se autentique.

## Paso 4 — Re-apuntar las connection references

En la solución **CashHubCore** → Connection references → cada `CH-CR-*` → editar → elegir la
conexión nueva de User 5.

**No crear referencias nuevas.** Se reutilizan las 7 que ya normalizamos; solo cambia la conexión
que tienen detrás. Si se crean nuevas, hay que rehacer los 20 apuntamientos de los flujos.

## Paso 5 — Reactivar los flujos

Este paso tiene una trampa conocida y documentada en este repo:

> **Cambiar `statecode` por API con una cuenta de servicio NO registra el trigger.** El flujo queda
> marcado como activo pero no dispara. Hay que **desactivar y reactivar desde el portal con la
> identidad del dueño**.

Para cada uno de los 11: desactivar → activar. Después verificar que el trigger quedó registrado
haciendo una prueba real, no mirando el estado.

## Paso 6 — Verificación

Cuando termines, avisá y corro esto contra el ambiente:

- Las 7 referencias apuntan a conexiones de User 5
- Los 11 flujos siguen ON
- User 5 tiene los 13 privilegios nuevos
- Ninguna referencia quedó sin conexión asociada

## Lo que NO hay que hacer

- **No cambiar el dueño de los flujos** creyendo que eso cambia la identidad de ejecución. No lo
  hace. Igual conviene unificarlo después, por prolijidad.
- **No borrar las conexiones viejas de Randy** hasta confirmar que los 11 flujos corrieron bien con
  las nuevas. Si algo falla, son el camino de vuelta.
- **No dar System Administrator a User 5.** Es tentador y resuelve todo, pero convierte la cuenta
  de servicio en la llave maestra del ambiente. El rol acotado es más trabajo una vez y menos
  riesgo siempre.
