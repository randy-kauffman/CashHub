import { IInputs, IOutputs } from "./generated/ManifestTypes";

/**
 * CashHub.FileDownloader
 *
 * Baja un archivo generado en el servidor SIN navegar a Dataverse.
 *
 * EL PROBLEMA QUE RESUELVE. `Download(".../sanic_csvcore/$value")` es una NAVEGACION
 * del navegador. Power Fx no tiene forma de ponerle un header `Authorization`, asi que
 * lo unico que puede autenticarla es la cookie del org — que los usuarios de negocio
 * no tienen, porque viven en apps.powerapps.com. Les daba HTTP 401 y una pestaña en
 * blanco. Y muda: al no ser una operacion de la app, el fallo quedaba fuera del alcance
 * de cualquier `IfError`, asi que nadie se enteraba.
 *
 * Aca el contenido ya viene autenticado por el conector (Custom API
 * `sanic_CHGetBatchCsv`) y el archivo se arma en el navegador con un Blob. No se navega
 * a ningun lado, no viaja ninguna credencial y no se depende del estado de la sesion.
 */
export class FileDownloader
    implements ComponentFramework.StandardControl<IInputs, IOutputs>
{
    private notifyOutputChanged: () => void;

    /**
     * Ultimo token ya atendido. `updateView` corre ante CUALQUIER cambio de propiedad
     * y ante re-renders que no controlamos; sin esta memoria el usuario terminaria con
     * el archivo bajado varias veces por una sola pulsacion.
     */
    private lastTrigger = "";

    private status = "";
    private bytesDownloaded = 0;

    /** URL del objeto anterior, para revocarla y no filtrar memoria en la sesion. */
    private lastObjectUrl: string | null = null;

    public init(
        _context: ComponentFramework.Context<IInputs>,
        notifyOutputChanged: () => void,
        _state: ComponentFramework.Dictionary,
        _container: HTMLDivElement
    ): void {
        this.notifyOutputChanged = notifyOutputChanged;
        // Sin UI a proposito: es un componente de comportamiento, no de presentacion.
        // En la pantalla va como un cuadrito de 1x1 fuera de la vista.
    }

    public updateView(context: ComponentFramework.Context<IInputs>): void {
        const trigger = context.parameters.Trigger.raw ?? "";

        // Nada que hacer: o no hay disparo, o este token ya se atendio.
        if (trigger === "" || trigger === this.lastTrigger) {
            return;
        }
        this.lastTrigger = trigger;

        const content = context.parameters.Content.raw ?? "";
        const fileName = (context.parameters.FileName.raw ?? "").trim() || "archivo.csv";
        const mimeType =
            (context.parameters.MimeType.raw ?? "").trim() || "text/csv;charset=utf-8";
        const expectedBytes = context.parameters.ExpectedBytes.raw ?? 0;

        if (content === "") {
            // Bajar un archivo vacio es peor que no bajar nada: el usuario cree que
            // tiene el lote y lo que tiene es un archivo en blanco.
            this.report("sin-contenido", 0);
            return;
        }

        try {
            // UTF-8 explicito. El contenido viaja tal cual quedo archivado —saltos de
            // linea CRLF incluidos— y aca no se normaliza nada: lo que se baja tiene
            // que ser identico a lo que se mando al core, o el archivo no prueba nada.
            const bytes = new TextEncoder().encode(content);

            // VERIFICACION DE INTEGRIDAD. El servidor dice cuantos bytes archivo; se
            // comparan con los que de verdad se arman aca. Si no coinciden, el
            // contenido llego cortado y NO se baja: un CSV incompleto que entra a un
            // core bancario es mucho peor que una descarga que falla a la vista.
            if (expectedBytes > 0 && bytes.byteLength !== expectedBytes) {
                this.report(
                    `tamaño-no-coincide:esperados ${expectedBytes} bytes, llegaron ${bytes.byteLength}`,
                    bytes.byteLength
                );
                return;
            }

            const blob = new Blob([bytes], { type: mimeType });

            if (this.lastObjectUrl !== null) {
                URL.revokeObjectURL(this.lastObjectUrl);
            }
            this.lastObjectUrl = URL.createObjectURL(blob);

            const anchor = document.createElement("a");
            anchor.href = this.lastObjectUrl;
            anchor.download = fileName;
            anchor.style.display = "none";

            // El ancla tiene que estar en el documento para que el click cuente como
            // gesto de usuario en Firefox; en Chromium no hace falta, pero agregarla y
            // sacarla no cuesta nada y evita una diferencia entre navegadores.
            document.body.appendChild(anchor);
            anchor.click();
            document.body.removeChild(anchor);

            this.report("ok", bytes.byteLength);
        } catch (e) {
            this.report(`error:${e instanceof Error ? e.message : String(e)}`, 0);
        }
    }

    private report(status: string, bytes: number): void {
        this.status = status;
        this.bytesDownloaded = bytes;
        this.notifyOutputChanged();
    }

    public getOutputs(): IOutputs {
        return {
            Status: this.status,
            BytesDownloaded: this.bytesDownloaded,
        };
    }

    public destroy(): void {
        if (this.lastObjectUrl !== null) {
            URL.revokeObjectURL(this.lastObjectUrl);
            this.lastObjectUrl = null;
        }
    }
}
