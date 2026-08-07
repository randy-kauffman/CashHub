# Backend C# — el motor de validación de correos

> Fuente: `src/backend/`. Tres proyectos .NET sin `.sln`; el registro en
> Dataverse se hace con `scripts/register-validation-plugin.sh`. Complementa el
> [08-motor-validacion.md](../desarrollo/08-motor-validacion.md) de desarrollo.

| Proyecto | Target | Rol |
|---|---|---|
| `CashHub.Plugins` | net462, firmado (`CashHub.snk`, **no versionado** — prerrequisito de entorno) | La única DLL registrable: plugin + acceso a datos Dataverse. |
| `CashHub.Validation` | netstandard2.0, sin dependencias externas (requisito del sandbox) | Dominio puro: motor, 28 reglas, parser XLSX, triage, reporting. |
| `CashHub.Validation.Tests` | net8.0, xUnit | 59 métodos ≈ 91 casos, incluida la batería end-to-end contra los 15 xlsx de `docs/plantillas/pruebas/`. |

Empaquetado clave: el dominio **se compila dentro** del plugin
(`<Compile Include="../CashHub.Validation/**/*.cs">`) para producir una sola
DLL firmada; los tests referencian el proyecto aparte.

## Cadena de ejecución

```
ValidateReceivedEmailPlugin.Execute            (guarda anti-bucle Depth > 2; corre como sistema)
  └─ ReceivedEmailValidationService.Validate(emailId)   — 10 etapas trazadas
       1. Retrieve del correo (remitente, asunto, tracking)
       2. LoadAttachments + BuildEnvelope
       3. XlsxParser.Parse (error de archivo → WorkbookError, lo reporta VR-07)
       4. LoadRules desde sanic_validationrules (vacía → error que apunta al seed)
       5. ValidationEngine.Run — 28 reglas, sin cortocircuito global
       6. PersistRun → 1 ValidationRun + N ValidationResults (Skipped no genera fila)
       7. DeclarationMaterializer (solo si Passed + cliente resuelto + workbook)
       8. SenderTriage.Classify
       9. ClientReplyComposer (solo si MayReplyToSender)
      10. UpdateEmail — matriz de estados + triage + respuesta compuesta
```

Errores: `InvalidPluginExecutionException` se relanza tal cual; cualquier otra
excepción marca el correo `ReviewError` con texto explícito y **luego** propaga
para que el system job quede Failed (visible para operaciones).

## Las 28 reglas (VR-01..VR-28)

Catálogo paramétrico en `sanic_ValidationRules` (seed
`data/seed/50-validationrules.json`, 28 filas); estrategias en
`CashHub.Validation/Rules/`. El orden de ejecución es `sanic_validationorder`,
no el número. **El motor nunca corta en el primer fallo** — el cliente recibe
la lista completa; el único salto es por dependencia (madre ≠ Pass → Skipped).

| Categoría | Reglas | Qué cubren |
|---|---|---|
| A. Sobre del correo (`EnvelopeRules.cs`) | VR-01..VR-06 | Remitente presente y registrado, 1 adjunto, prefijo "Pre-registro" en el asunto, extensión .xlsx, tamaño ≤5 MB. |
| B. Estructura del archivo (`StructureRules.cs`) | VR-27, VR-07, VR-08, VR-19, VR-16 | Plantilla oficial y versión mínima (Metadata: `CH-TPL-Depositos` ≥1.1.0), 2 tablas (compuerta: reporta el error del parser), nombres Depositos/Cheques, columnas esperadas. |
| C. Contenido (`ContentRules.cs`) | VR-09..VR-15, VR-17, VR-18, VR-20..VR-22, VR-28 | Al menos un depósito; bolsa y cuenta por fila; numéricos válidos, no negativos, montos >0; monedas NIO/USD/EUR; cuenta con moneda única; cheques completos, sin repetidos, sin huérfanos; totales de cheques cuadran; **VR-28: desglose BAC vs Otros por columna** (tiempos de compensación distintos). |
| D. Negocio / atribución (`BusinessRules.cs`) | VR-24..VR-26, VR-23 | CIF declarado en `Depositos!J2`, cliente registrado (resuelve `ResolvedClientId` — "el contenido ATRIBUYE"), correo autorizado ("el correo AUTORIZA"), cuentas del cliente. |

Deuda conocida: VR-25 tiene la celda `Depositos!J2` hardcodeada (VR-24 la toma
del parámetro); el comentario del seed dice "26 (+VR-27)" pero son 28.

## SenderTriage — la compuerta de respuesta

Clasifica al remitente con los resultados ya evaluados de VR-03, VR-27 y VR-25
(cadenas de dependencia independientes: el CIF se resuelve aunque el remitente
sea desconocido):

| VR-03 | VR-27 | VR-25 | Clasificación | ¿Responder? |
|---|---|---|---|---|
| Pass | — | — | Registrado | **Sí** (único caso) |
| ≠Pass | Pass | Pass | Cliente sin registrar | No — revisión de Banca de Empresas |
| ≠Pass | Pass | ≠Pass | Sospechoso | No — cuarentena + alerta |
| ≠Pass | ≠Pass | — | No corresponde | No — cuarentena |

Regla de la casa: omitida por dependencia NO es aprobada; el `Reason` (persistido
en `sanic_triagenotes`) siempre cita los VR concretos, nunca es genérico. La
razón de seguridad: responder a una dirección sin verificar le confirmaría a un
tercero que ese CIF es cliente del banco.

## Materialización y respuesta

- **DeclarationMaterializer**: Declaration → DeclaredBags (agrupa por bolsa,
  totales por moneda; no escribe el rollup `sanic_totaldeposits`) →
  DeclaredDeposits (cuenta como int — deuda de esquema documentada) →
  DeclaredChecks (tipo por substring "BAC"). Sus errores nombran la invariante
  que debió garantizarlos ("esto no debería pasar tras VR-xx").
- **ClientReplyComposer**: prioriza la plantilla editable en
  `sanic_chtblemailtemplate` (`reply-accepted`/`reply-rejected`); ante
  ausencia, inactividad o error cae al HTML embebido de `ClientReplyBuilder`
  (tablas + estilos inline, sin imágenes externas, tope 640px — reglas de
  compatibilidad Gmail/Outlook). `TemplateEngine` es un mustache-lite:
  `{{token}}` escapado, `{{{token}}}` crudo, `{{#lista}}`, `{{#si:token}}`.
  Falla de sistema sin findings de negocio → **no se responde al cliente**.

## Parser XLSX

`Parsing/XlsxParser.cs`: OpenXML crudo vía `ZipArchive` + `XDocument`, todo en
memoria (sandbox). Lee sharedStrings, catálogo de hojas, celdas (inline/shared/
directas, marca fórmulas), tablas (`xl/tables/*.xml` con su rango y columnas) y
la hoja `Metadata` (clave en columna A, valor en B). Filas donde todo es vacío
o valor cacheado de fórmula se descartan: los totales pre-armados de la
plantilla no cuentan como datos. Todo error se envuelve en `XlsxParseException`
nombrando la parte que falló.

## Registración y puertas de entrada

`scripts/register-validation-plugin.sh` (Web API v9.2, idempotente; requiere
`pp_secrets.env` y el build Release): assembly sandbox + plugin type + Custom
API global **`sanic_CHValidateReceivedEmail`** (in: `ReceivedEmailId`; out:
`RunId`, `Result`, `FailedFindings`). **El paso asíncrono en Create fue
retirado** — el script lo borra si sobrevive. Puertas reales hoy:

1. `CH-Flow-ProcessEmail` — tras subir los adjuntos (orden garantizado).
2. `CH-Flow-MailboxSweep` — revalida pendientes cada 10 minutos.
3. La Console — "Marcar para reproceso" (indirecta, vía el barrido).

## Tests

| Archivo | Cubre |
|---|---|
| `EnvelopeRulesTests` | VR-01..06 aisladas + integración (no-cortocircuito, Skipped por dependencia). |
| `ContentAndBusinessRulesTests` | Estructura, contenido, cadena de atribución completa, 4 tests de VR-28. |
| `EngineTests` | Semántica del motor: dependencias, NotImplemented, aislamiento de excepciones, orden. |
| `SenderTriageTests` | Las 4 ramas del triage + "Skipped no es aprobada" + motivo nunca genérico. |
| `XlsxParserTests` | Contra la plantilla real `CH-TPL-Depositos-v1.1.0.xlsx` + corruptos. |
| `ClientReplyBuilderTests` / `TemplateEngineTests` | HTML de correo (contrato inline-styles, anti-XSS) y el motor de plantillas. |
| `TestBatteryTests` | **End-to-end**: los 15 xlsx de `docs/plantillas/pruebas/` disparan exactamente las reglas del plan de pruebas; réplica en código del seed (detecta divergencia seed↔motor). |

## Deuda técnica del backend

1. `CashHub.snk` no versionado: el build Release no es reproducible desde un
   clone limpio.
2. Sin `.sln` ni CI; el comando de build solo está documentado dentro del
   mensaje de error del script de registro.
3. VR-25 con `Depositos!J2` hardcodeado.
4. `sanic_accountnumber` int de 32 bits (el materializador lanza error explícito
   si la cuenta no parsea).
5. El doc-comment del plugin aún describe la arquitectura de "dos puertas" con
   el step en Create ya retirado.
6. Comentario del seed desactualizado ("26 reglas" — son 28).
