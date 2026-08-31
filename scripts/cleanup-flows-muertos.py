#!/usr/bin/env python3
# =============================================================================
# Categoria C: elimina cloud flows sin uso — docs/referencia/limpieza-candidatos.md
#
# LOS 7 OBJETIVOS, por dos causas distintas:
#
# (a) Retirados y reemplazados por Custom API — los 5 en Draft. El usuario confirmo
#     el 2026-08-31 que se apagaron a proposito porque las Custom API los
#     reemplazaron; no fue una desactivacion accidental.
#
# (b) Encendidos pero imposibles de disparar — los 2 con trigger PowerAppV2. Un
#     flujo con trigger de canvas app SOLO puede ejecutarse si una app lo invoca.
#     Se verificaron los tres arboles de fuente: ninguna app los llama. No es que
#     "no se usen": es que no pueden correr.
#
# OJO — HAY QUE DESACTIVAR ANTES DE BORRAR. Dataverse rechaza el DELETE de un
# workflow activo. El script pone statecode=0 primero y despues borra.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/cleanup-flows-muertos.py            # dry-run
#       ./scripts/cleanup-flows-muertos.py --apply    # ejecuta
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

FLOWS = [
    # (a) retirados, reemplazados por Custom API
    ("CH-Flow-Create client data",
     "Draft. Reemplazado por sanic_CHResolveChangeRequest, que es lo que hoy mueve "
     "recordstatus a Active al aprobarse un RFC de creacion. Confirmado por el usuario"),
    ("CH-Flow-Apply client data changes",
     "Draft. Mismo reemplazo por Custom API"),
    ("CH-Flow-Activate row",
     "Draft. Manipulaba recordstatus; reemplazado por logica server-side"),
    ("CH-Flow-Deactivate row",
     "Draft. Idem Activate row"),
    ("CH-Flow-RFCWatchdog",
     "Draft. Unico consumidor de la env var CHEVRfcApplyTimeoutMinutes, ya eliminada"),

    # (b) encendidos pero sin llamador posible
    ("CH-Flow-ExtractedTablesFromExcel",
     "ON pero trigger PowerAppV2 y NINGUNA app lo invoca: no puede dispararse"),
    ("CH-Flow-Get JSON data from attachment",
     "ON pero trigger PowerAppV2 y NINGUNA app lo invoca: no puede dispararse"),
]

# Nunca tocar. Verificados con consumidor real.
NO_TOCAR = {
    "CH-Flow-MailboxSweep", "CH-Flow-IngestEmails", "CH-Flow-ProcessEmail",
    "CH-Flow-DownloadData", "CH-Flow-Notify reviewers", "CH-Flow-Process XML Files",
    "CH-Flow-Reply to client email", "CH-Flow-UnblockOnClient",
    "CH-Flow-UnblockOnClientEmail", "CH-Flow-Notify when RFC implementation finish",
    "CH-Flow-Create a Teams chat group based on Office 365 Group Members",
}


def traza(etapa, mensaje):
    print(f"[{etapa}] {mensaje}", flush=True)


def cargar_secretos():
    ruta = os.environ.get("ENV_FILE", "pp_secrets.env")
    if not os.path.exists(ruta):
        raise SystemExit(f"No existe {ruta}.")
    v = {}
    with open(ruta, encoding="utf-8") as fh:
        for l in fh:
            l = l.strip()
            if not l or l.startswith("#") or "=" not in l:
                continue
            k, _, val = l.partition("=")
            v[k.strip()] = val.strip().strip('"').strip("'").rstrip("\r")
    faltan = [k for k in ("DATAVERSE_URL", "PP_TENANT_ID", "PP_CLIENT_ID",
                          "PP_CLIENT_SECRET") if not v.get(k)]
    if faltan:
        raise SystemExit(f"Faltan claves en {ruta}: {', '.join(faltan)}")
    v["DATAVERSE_URL"] = v["DATAVERSE_URL"].rstrip("/")
    return v


def obtener_token(sec):
    datos = urllib.parse.urlencode({
        "grant_type": "client_credentials", "client_id": sec["PP_CLIENT_ID"],
        "client_secret": sec["PP_CLIENT_SECRET"],
        "scope": sec["DATAVERSE_URL"] + "/.default"}).encode()
    url = f"https://login.microsoftonline.com/{sec['PP_TENANT_ID']}/oauth2/v2.0/token"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=datos)) as r:
            return json.load(r)["access_token"]
    except urllib.error.HTTPError as e:
        raise SystemExit(f"Fallo la autenticacion contra Azure AD ({e.code}): "
                         f"{e.read().decode(errors='replace')[:300]}")


def llamar(api, token, metodo, ruta, cuerpo=None):
    url = ruta if ruta.startswith("http") else f"{api}/{ruta}"
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(url, data=datos, method=metodo)
    req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/json")
    req.add_header("OData-MaxVersion", "4.0")
    req.add_header("OData-Version", "4.0")
    if datos:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as r:
            crudo = r.read()
            return json.loads(crudo) if crudo else None
    except urllib.error.HTTPError as e:
        d = e.read().decode(errors="replace")
        try:
            d = json.loads(d)["error"]["message"]
        except Exception:
            d = d[:400]
        raise RuntimeError(f"{metodo} {ruta} -> HTTP {e.code}: {d}")


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR" if APPLY else
                    "MODO: DRY-RUN (no se toca nada; usar --apply)")
    traza("INICIO", f"{len(FLOWS)} flujos objetivo | {len(NO_TOCAR)} protegidos")

    solapado = {n for n, _ in FLOWS} & NO_TOCAR
    if solapado:
        raise SystemExit(f"ERROR DE CONFIGURACION: {solapado} esta en las dos listas.")

    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    objetivos = []
    for nombre, razon in FLOWS:
        q = urllib.parse.urlencode({
            "$select": "workflowid,name,statecode,statuscode",
            "$filter": f"name eq '{nombre.replace(chr(39), chr(39)*2)}' and category eq 5"})
        filas = llamar(api, token, "GET", f"workflows?{q}")["value"]
        if not filas:
            traza("VERIFICACION", f"  {nombre}: NO EXISTE (ya borrado)")
            continue
        w = filas[0]
        estado = "ON" if w["statecode"] == 1 else "OFF"
        traza("VERIFICACION", f"  [{estado}] {nombre}")
        traza("VERIFICACION", f"      {razon}")
        objetivos.append((w["workflowid"], nombre, w["statecode"]))

    print()
    traza("REPORTE", f"A borrar: {len(objetivos)}")
    if not APPLY:
        traza("FIN", "Dry-run terminado. Nada fue modificado.")
        return

    print()
    traza("BORRADO", "Aplicando. Los activos se desactivan primero.")
    hechos = 0
    for wid, nombre, statecode in objetivos:
        try:
            if statecode == 1:
                # Dataverse rechaza el DELETE de un workflow activo.
                llamar(api, token, "PATCH", f"workflows({wid})",
                       {"statecode": 0, "statuscode": 1})
                traza("BORRADO", f"  desactivado: {nombre}")
            llamar(api, token, "DELETE", f"workflows({wid})")
            traza("BORRADO", f"  eliminado: {nombre}")
            hechos += 1
        except RuntimeError as e:
            traza("BORRADO", f"  FALLO en {nombre}: {e}")

    print()
    traza("FIN", f"{hechos} de {len(objetivos)} flujos eliminados.")
    if hechos:
        traza("FIN", "Reexportar CashHubFlows y correr pac solution unpack.")


if __name__ == "__main__":
    main()
