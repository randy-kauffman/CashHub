# Motor de validación de correos — diseño

> Fase A del proceso de validación de declaraciones. Estado: diseño aprobado, seed de reglas
> sembrado; el motor C# se construye en las fases B-C.

## Arquitectura

```
[1. TRANSPORTE]              [2. MOTOR]                         [3. RESPUESTA]
CH-Flow-IngestEmails         Custom API sanic_CHValidateReceivedEmail   CH-Flow-Replytoclientemail
(Power Automate mínimo)  →   (plugin C#, asíncrono)             →       (correo OK / errores)
solo mueve el correo         valida TODO, paramétrico                    con hoja y celda
```

**Por qué así**: Power Automate queda reducido a lo que hace bien (transporte con conector de
correo); la validación vive en un plugin C# — rápido (ms), transaccional, y **testeable con
FakeXrmEasy regla por regla antes de desplegar**. Un solo motor con dos puertas: el trigger
automático y el botón "Reprocesar" de la Console.

## Estados del correo (picklists existentes — sin cambios de esquema)

| Momento | `ValidationStatus` | `EmailReviewStatus` | `EmailReviewMethod` |
|---|---|---|---|
| Ingresado por el flujo | Pendiente | Pendiente | — |
| Motor: todo pasa | Valido | Aceptado | Auto |
| Motor: alguna regla falla | Invalido | Denegado | Auto |
| Motor revienta (excepción) | Pendiente | **Error** | Auto |
| Override humano en Console | (la que decida) | Aceptado/Denegado | **Manual** |

`sanic_Client` (lookup de atribución, tanda 3) se setea cuando VR-25 resuelve el CIF.

## Contrato del Custom API

`sanic_CHValidateReceivedEmail` — entrada: `ReceivedEmailId` (GUID). Salida: `RunId`,
`Result` (Pass/Fail/SystemError), `FailedFindings` (int). Registrado como paso asíncrono en
Create de `sanic_chtblreceivedemails` + invocable directo (Console, barrido, pruebas).

## Motor de reglas

- Cada regla es una **estrategia C#** registrada por `ValidationName` (VR-01…VR-27). El motor
  carga las filas activas de `sanic_ValidationRules` ordenadas por `ValidationOrder` y ejecuta
  la estrategia correspondiente con sus parámetros.
- **Negocio controla sin código**: `IsActive`, orden, `ValidationValue` (parámetro; JSON si son
  varios) y `ValidationFailMessage` con placeholders `{hoja} {celda} {fila} {valor} {esperado}`.
- **Dependencias** (`ValidationDependency`): si la regla madre falló, la hija se omite — evita
  cascadas de ruido (sin tabla Depositos no se reportan 40 celdas inexistentes).
- **No se detiene en el primer error**: acumula TODOS los hallazgos de todas las reglas
  ejecutables (requisito de negocio: el cliente recibe la lista completa para reparar de una).
- Familias y orden: sobre (10-65) → estructura (70-84) → contenido (90-200) → negocio (210-240).
  Catálogo completo en `data/seed/50-validationrules.json`.

## Modelo de hallazgos

- `ValidationRun`: una por ejecución (correo, inicio/fin, resultado global).
- `ValidationResult`: **una fila por hallazgo** (no por regla): una regla que falla en 5 celdas
  produce 5 filas, cada una con `Hoja`, `Celda`, `Fila`, `Detalle` (el mensaje ya renderizado)
  — campos agregados en Core 1.4.0.0. Las reglas que pasan escriben una fila Pass (sin ubicación)
  para trazabilidad y métricas (`Durationms`).

## Correo de respuesta (requisito: todos los errores, con hoja y celda)

`CH-Flow-Replytoclientemail` se dispara al quedar el correo Valido/Invalido:
- **OK**: confirmación con resumen de lo declarado (depósitos, total por moneda).
- **Errores**: tabla HTML desde los `ValidationResults` Fail del run — primero los de sobre
  (sin ubicación), luego los de contenido con `Hoja` + `Celda`/`Fila` y el `Detalle` renderizado.
  El texto de cada error lo redacta negocio en `ValidationFailMessage`.

## Detección de fallos (el buzón ES la cola — nada se pierde, solo se atrasa)

1. **Barrido de rezagados** (flujo programado, 5-10 min, parametrizable): (a) correos en el
   buzón sin fila en `ReceivedEmails` más viejos que N min → re-ingesta + alerta Teams;
   (b) `ReceivedEmails` en Pendiente sin `ValidationRun` → reinvoca el Custom API + alerta.
   El barrido usa consulta activa (camino distinto al webhook del trigger) — sobrevive a la
   muerte del trigger.
2. **Heartbeat**: cada corrida del barrido deja constancia; la Console muestra el semáforo
   ("última revisión hace X min"). Si el barrido muere, el latido envejece y el semáforo lo
   delata — el vigilante del vigilante es la ausencia de latido, no otro proceso.
3. **Excepción del motor** → correo en `EmailReviewStatus=Error` + alerta. Nunca un limbo.

Peor caso de reacción = intervalo del barrido.

## Nuevo oficio de la Email Validation Console

Deja de validar (las 23 reglas Power Fx se retiran — muere la doble implementación) y pasa a:
(1) contingencia: cola, Reprocesar (mismo Custom API), override humano auditado (el caso
"minuta parece alterada"); (2) tablero de salud (semáforo, cola, errores de sistema);
(3) administración de reglas (IsActive, parámetros, mensajes).

## Fases

| Fase | Contenido | Estado |
|---|---|---|
| A | Este diseño + seed de 27 reglas + `ValidationResults` extendida | ✅ |
| B | Proyecto C# (plugin package), motor + reglas de sobre + tests FakeXrmEasy | — |
| C | Parser xlsx (plantilla v1.1.0) + reglas estructura/contenido/negocio + materialización de `Declaration` atribuida | — |
| D | `IngestEmails` reescrito (dedup por `InternetMessageId`, buzón por env var, Try/Catch) + barrido + heartbeat | — |
| E | Correo de respuesta + Console recableada | — |

## Decisiones registradas

- El parámetro de cada regla vive en `ValidationValue` (fuente única); la env var
  `sanic_CHEVFileSizeMaxMB` queda deprecada a favor del parámetro de VR-06.
- VR-27 (guardián de plantilla oficial/versión) es recomendación de arquitectura, apagable.
- Una regla nueva que requiera lógica inexistente = estrategia C# nueva (límite honesto de lo
  paramétrico); cambiar umbrales/mensajes/orden/activación = solo tabla.
