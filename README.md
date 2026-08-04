# CashHub

Plataforma de digitalización del procesamiento de depósitos en efectivo sobre Microsoft Power Platform.

Los clientes del banco envían efectivo y cheques en bolsas de seguridad (tulas) vía transportadora de valores, acompañados de una declaración digital (plantilla Excel enviada por correo). CashHub valida la declaración automáticamente, traza la recepción física (esclusa → bóveda → recuento) y genera la carga masiva de transacciones para el core bancario IBS Branch, eliminando el área de digitación manual.

## Estructura del repositorio

```
CashHub/
├── docs/
│   ├── analysis/          # Análisis técnico inicial de las soluciones (as-is)
│   ├── desarrollo/        # Arquitectura y referencia técnica de implementación
│   ├── guias/             # Manuales por rol (recepción, recuento, supervisión…)
│   └── referencia/        # Contratos vivos: modelo de datos, flujos, roles, choices
└── solutions/
    └── unpacked/          # Fuente versionado de las 3 soluciones (pac solution unpack)
        ├── core/          # CashHubCore  — tablas, roles, option sets, variables de entorno
        ├── flow/          # CashHubFlow  — flujos Power Automate
        └── apps/          # CashHubApps  — canvas apps (YAML) y model-driven app
```

## Componentes

| Solución | Versión | Contenido |
|---|---|---|
| CashHubCore | 1.1.0.0 | 38 tablas Dataverse (`sanic_CHTbl*`), 8 roles de seguridad (`CH-SR-*`), 3 option sets globales, 19 variables de entorno (`sanic_CHEV*`) |
| CashHubFlow | 1.1.0.0 | 19 flujos Power Automate (`CH-Flow-*`): ingesta de correos, extracción de Excel, validaciones, RFCs de datos de cliente, notificaciones Teams |
| CashHubApps | 1.1.0.0 | 8 canvas apps por rol (Reception, Recounting, Supervisor, Digitization, Email Validation Console, Deposits Tracker, Client Administration, Hub) y bibliotecas de componentes |

## Primeros pasos

- **¿Nuevo en el proyecto?** Empezá por [docs/analysis/00-sintesis.md](docs/analysis/00-sintesis.md): el flujo de punta a punta y los hallazgos del análisis inicial.
- **¿Buscás el detalle de una tabla, flujo o rol?** Está en [docs/referencia/](docs/referencia/).
- **¿Vas a operar el sistema?** Los manuales por rol están en [docs/guias/](docs/guias/).
- **¿Vas a desarrollar?** Arquitectura, build y ALM en [docs/desarrollo/](docs/desarrollo/).

## Trabajar con las soluciones

Las soluciones se versionan desempaquetadas con [Power Platform CLI](https://aka.ms/PowerPlatformCLI):

```bash
# Reempaquetar una solución para importar
pac solution pack --zipfile CashHubCore.zip --folder solutions/unpacked/core --packagetype Unmanaged

# Reempaquetar una canvas app editada desde su fuente YAML
pac canvas pack --sources solutions/unpacked/apps/CanvasApps/src/<app> --msapp <app>_DocumentUri.msapp
```

Los paquetes `.zip` no se versionan en el repo: se publican en GitHub Releases.
