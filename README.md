# CashHub

Plataforma de digitalización del procesamiento de depósitos en efectivo sobre Microsoft Power Platform.

Los clientes del banco envían efectivo y cheques en bolsas de seguridad (tulas) vía transportadora de valores, acompañados de una declaración digital (plantilla Excel enviada por correo). CashHub valida la declaración automáticamente (motor de 28 reglas en un plugin C#), traza la recepción física (esclusa → bóveda → recuento) y genera la carga masiva de transacciones para el core bancario IBS Branch, eliminando el área de digitación manual.

## Estructura del repositorio

```
CashHub/
├── docs/
│   ├── soluciones/        # Referencia actual solución por solución (componentes, flujos, apps, backend)
│   ├── referencia/        # Contratos vivos: modelo de datos, roles, choices, convenciones
│   ├── guias/             # Manuales por rol (recepción, recuento, supervisión…)
│   ├── desarrollo/        # Arquitectura y referencia técnica de implementación
│   ├── analysis/          # HISTÓRICO: análisis as-is inicial, pre-rediseño del pipeline
│   └── plantillas/        # Plantilla Excel oficial + libros de prueba
├── data/
│   ├── seed/              # Data de configuración (las 28 reglas de validación, playbook…)
│   └── synthetic/         # Data sintética de clientes para DEV
├── scripts/               # Registro del plugin, seed, claves alternas
├── solutions/
│   └── unpacked/          # Fuente versionada de las 3 soluciones (pac solution unpack)
│       ├── core/          # CashHubCore  — tablas, roles, option sets, variables de entorno
│       ├── flow/          # CashHubFlows — flujos Power Automate
│       └── apps/          # CashHubApps  — canvas apps (YAML) y component libraries
└── src/
    └── backend/           # Plugin C# + motor de validación + tests (xUnit)
```

## Componentes

| Pieza | Versión | Contenido |
|---|---|---|
| CashHubCore | 1.6.0.0 | 40 tablas Dataverse (`sanic_CHTbl*`), 8 roles de seguridad, 3 option sets globales, 21 variables de entorno (`sanic_CHEV*`) |
| CashHubFlows | 1.6.5.0 | 16 flujos Power Automate (`CH-Flow-*`): pipeline de correo, RFCs de datos de cliente, utilidades para apps, notificaciones Teams |
| CashHubApps | 1.15.3.0 | 8 canvas apps por rol (Hub, Reception, Recounting, Supervisor, Digitization, Deposits Tracker, Client Administration, Email Validation Console) + 2 component libraries |
| Backend C# | — | `CashHub.Plugins` (Custom API `sanic_CHValidateReceivedEmail`, 28 reglas VR-01..VR-28) — se registra con `scripts/register-validation-plugin.sh`, no viaja en solución |

## Primeros pasos

- **¿Nuevo en el proyecto?** Empezá por [docs/soluciones/00-vision-general.md](docs/soluciones/00-vision-general.md): qué es cada pieza y el flujo de punta a punta.
- **¿Buscás el detalle de una tabla, flujo, app o del motor?** Solución por solución en [docs/soluciones/](docs/soluciones/); contratos (modelo de datos, roles, choices) en [docs/referencia/](docs/referencia/).
- **¿Vas a operar el sistema?** Los manuales por rol están en [docs/guias/](docs/guias/).
- **¿Vas a desarrollar?** Arquitectura, build y ALM en [docs/desarrollo/](docs/desarrollo/).

## Trabajar con las soluciones

Las soluciones se versionan desempaquetadas con [Power Platform CLI](https://aka.ms/PowerPlatformCLI):

```bash
# Reempaquetar una solución para importar
pac solution pack --zipfile CashHubCore.zip --folder solutions/unpacked/core --packagetype Unmanaged

# Reempaquetar una canvas app editada desde su fuente YAML
pac canvas pack --sources solutions/unpacked/apps/CanvasApps/src/<app> --msapp <app>_DocumentUri.msapp

# Compilar y registrar el plugin de validación (requiere pp_secrets.env y CashHub.snk)
dotnet build src/backend/CashHub.Plugins -c Release
./scripts/register-validation-plugin.sh
```

Los paquetes `.zip` no se versionan en el repo: se publican en GitHub Releases.
