#!/usr/bin/env python3
# =============================================================================
# Cierra la categoria B: borra la Custom API sanic_CHCanResolveApproval y la
# libreria de componentes CH-CL-SharedComponents.
#
# POR QUE LA CUSTOM API: registrada con clase backing y script, pero NADIE la
# invoca. Verificado el 2026-08-31 contra un export FRESCO de CashHubApps (no
# contra la foto vieja del repo), desempaquetando los 9 .msapp a YAML y buscando
# en los tres arboles de fuente + los flujos. Cero coincidencias en Power Fx
# ejecutable. Los unicos hits estan en DataSources/Users.json y pkgs/Wadl/
# Environment.xml, que son catalogos autogenerados del conector y listan TODAS las
# acciones del ambiente — no son invocaciones.
#
# DESAMBIGUACION (hay tres APIs con nombres parecidos):
#   sanic_CHCanResolveApproval    -> nadie                            <- se borra
#   sanic_CHResolveApprovalRequest-> CH-App-Supervisor
#   sanic_CHResolveChangeRequest  -> CH-App-ClientAdministration      <- el boton
#                                    de resolver que el usuario usa hoy
#
# POR QUE LA LIBRERIA: los 9 ComponentReferences.json (8 apps + la propia
# libreria) estan VACIOS — ninguna app la importa. Verificado contra el export
# fresco. Contiene 2 componentes: cmpAppHeader (que ClientAdministration tiene
# duplicado en local) y cmpTheme (design tokens que no usa nadie). Decision del
# usuario: borrarla y convivir con la duplicacion; adoptarla seria un refactor
# grande de las apps que hoy no se va a hacer.
#
# EL FUENTE NO SE PIERDE: la libreria esta commiteada en
# solutions/unpacked/apps/CanvasApps/src/sanic_chclsharedcomponents_85fca/ y sigue
# recuperable desde git aunque se borre del ambiente.
#
# OJO — QUEDA UN PENDIENTE DE CODIGO: la clase C# CanResolveApprovalPlugin sigue en
# el assembly. No se toca desde aca: sacarla exige recompilar y desplegar, que es
# una operacion aparte. El plugintype se re-sincroniza desde el DLL, asi que
# borrarlo por API no serviria de nada mientras la clase siga compilada.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/cleanup-api-y-libreria.py            # dry-run
#       ./scripts/cleanup-api-y-libreria.py --apply    # ejecuta
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

CUSTOM_API = "sanic_CHCanResolveApproval"
LIBRERIA = "sanic_chclsharedcomponents_85fca"

# Nunca tocar. Son las dos APIs de nombre parecido que SI se usan.
NO_TOCAR = {"sanic_CHResolveApprovalRequest", "sanic_CHResolveChangeRequest"}


def traza(etapa, mensaje):
    print(f"[{etapa}] {mensaje}", flush=True)


def cargar_secretos():
    ruta = os.environ.get("ENV_FILE", "pp_secrets.env")
    if not os.path.exists(ruta):
        raise SystemExit(f"No existe {ruta}.")
    valores = {}
    with open(ruta, encoding="utf-8") as fh:
        for linea in fh:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            k, _, v = linea.partition("=")
            valores[k.strip()] = v.strip().strip('"').strip("'").rstrip("\r")
    faltan = [k for k in ("DATAVERSE_URL", "PP_TENANT_ID", "PP_CLIENT_ID",
                          "PP_CLIENT_SECRET") if not valores.get(k)]
    if faltan:
        raise SystemExit(f"Faltan claves en {ruta}: {', '.join(faltan)}")
    valores["DATAVERSE_URL"] = valores["DATAVERSE_URL"].rstrip("/")
    return valores


def obtener_token(sec):
    datos = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": sec["PP_CLIENT_ID"],
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


def borrar_custom_api(api, token):
    q = urllib.parse.urlencode({
        "$select": "customapiid,uniquename,name",
        "$filter": f"uniquename eq '{CUSTOM_API}'"})
    filas = llamar(api, token, "GET", f"customapis?{q}")["value"]
    if not filas:
        traza("API", f"  {CUSTOM_API}: no existe (ya borrada)")
        return False
    c = filas[0]
    if c["uniquename"] in NO_TOCAR:
        raise SystemExit(f"ERROR DE CONFIGURACION: {c['uniquename']} esta protegida.")
    cid = c["customapiid"]

    # Los hijos primero: Dataverse no borra la API si le cuelgan parametros.
    # El nombre del campo id va EXPLICITO: no se puede derivar quitando la 's' del
    # entity set, porque el plural de customapiresponseproperty es irregular.
    for ent, campo_id in (("customapirequestparameters", "customapirequestparameterid"),
                          ("customapiresponseproperties", "customapiresponsepropertyid")):
        qq = urllib.parse.urlencode({
            "$select": f"{campo_id},uniquename",
            "$filter": f"_customapiid_value eq {cid}"})
        hijos = llamar(api, token, "GET", f"{ent}?{qq}")["value"]
        for h in hijos:
            if APPLY:
                llamar(api, token, "DELETE", f"{ent}({h[campo_id]})")
                traza("API", f"    borrado de {ent}: {h['uniquename']}")
            else:
                traza("API", f"    se borraria de {ent}: {h['uniquename']}")

    if APPLY:
        llamar(api, token, "DELETE", f"customapis({cid})")
        traza("API", f"  Custom API eliminada: {CUSTOM_API}")
    else:
        traza("API", f"  se eliminaria la Custom API: {CUSTOM_API}")
    return True


def borrar_libreria(api, token):
    q = urllib.parse.urlencode({
        "$select": "canvasappid,name,displayname,componentstate",
        "$filter": f"name eq '{LIBRERIA}'"})
    filas = llamar(api, token, "GET", f"canvasapps?{q}")["value"]
    if not filas:
        traza("LIBRERIA", f"  {LIBRERIA}: no existe (ya borrada)")
        return False
    a = filas[0]
    traza("LIBRERIA", f"  encontrada: {a.get('displayname')} ({a['name']})")
    if APPLY:
        llamar(api, token, "DELETE", f"canvasapps({a['canvasappid']})")
        traza("LIBRERIA", f"  eliminada: {LIBRERIA}")
    else:
        traza("LIBRERIA", f"  se eliminaria: {LIBRERIA}")
    return True


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR" if APPLY else
                    "MODO: DRY-RUN (no se toca nada; usar --apply)")
    traza("INICIO", f"Protegidas y nunca tocadas: {', '.join(sorted(NO_TOCAR))}")

    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    traza("API", f"Custom API objetivo: {CUSTOM_API}")
    borrar_custom_api(api, token)

    traza("LIBRERIA", f"Libreria objetivo: {LIBRERIA}")
    borrar_libreria(api, token)

    print()
    if APPLY:
        traza("FIN", "Aplicado. Reexportar CashHubCore y CashHubApps, y correr "
                     "pac solution unpack para que el repo refleje el ambiente.")
        traza("FIN", "PENDIENTE DE CODIGO: la clase CanResolveApprovalPlugin sigue "
                     "en el assembly. Sacarla exige recompilar y desplegar.")
    else:
        traza("FIN", "Dry-run terminado. Nada fue modificado.")


if __name__ == "__main__":
    main()
