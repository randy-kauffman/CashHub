#!/usr/bin/env python3
# =============================================================================
# Deja listo el rol de la cuenta de servicio de los flujos y se lo asigna a User 5.
# Ver docs/pruebas/01-cuenta-de-servicio-flujos.md.
#
# QUE HACE, EN ESTE ORDEN:
#   1. Agrega al rol los 13 privilegios que le faltan para correr los 11 flujos.
#   2. Renombra CH-SR-EmailIngestionFlow -> CH-SR-FlowService.
#   3. Asigna a User 5 ese rol y Basic User.
#
# POR QUE 13 PRIVILEGIOS: el rol se diseno solo para los flujos de ingesta y cubre
# 4 tablas, pero los 11 flujos tocan 16. La lista sale de cruzar el entityName y el
# operationId de cada accion Dataverse de los flujos contra los privilegios que el
# rol ya tenia.
#
# POR QUE GLOBAL EN TODOS: un flujo no tiene "sus" registros — procesa los de todos.
# Con Basic o Local solo veria los que le pertenecen y no procesaria nada.
#
# CH-Tbl-Parameter la leen los flujos y NO lleva privilegio: es OwnershipTypeMask
# UnOwned, o sea que Dataverse no genera privilegios para ella.
#
# POR QUE TAMBIEN BASIC USER: User 5 no tenia NINGUN rol directo. Sin Basic User la
# cuenta no puede operar en Dataverse por mas privilegios de tabla que tenga.
#
# OJO — ESTO NO ALCANZA PARA QUE LOS FLUJOS CORRAN COMO USER 5. Un cloud flow corre
# con la identidad de la CONEXION detras de cada connection reference, no con la del
# dueno del flujo ni con la de quien tiene el rol. Falta crear las 7 conexiones
# iniciando sesion COMO User 5 y re-apuntar las referencias CH-CR-*. Eso va por
# portal; ver los pasos 3 a 5 del documento.
#
# OJO — Depth es un ENUM de OData (Microsoft.Dynamics.CRM.PrivilegeDepth), NO un
# entero. Mandar 0/1/2/3 revienta el deserializador.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/setup-cuenta-servicio-flujos.py            # dry-run
#       ./scripts/setup-cuenta-servicio-flujos.py --apply    # ejecuta
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

ROL_ACTUAL = "CH-SR-EmailIngestionFlow"
ROL_NUEVO = "CH-SR-FlowService"
USUARIO = "# User 5"

# Los 13 que faltan, todos Global. Entre parentesis, el flujo que los necesita.
PRIVILEGIOS = [
    ("prvReadsanic_chtblclient",                      "UnblockOnClient: trigger sobre Client"),
    ("prvReadsanic_chtblclientemail",                 "UnblockOnClientEmail: trigger sobre ClientEmail"),
    ("prvReadsanic_chtblclientdatachangerequest",     "Notifyreviewers / NotifywhenRFCimplementationfinish"),
    ("prvReadsanic_chtblclientdatachangerequestline", "Notifyreviewers: loop Por_cada_CRL"),
    ("prvReadsanic_chtblclientdatachangepermission",  "Notifyreviewers: resuelve el grupo aprobador"),
    ("prvWritesanic_chtblclientdatachangepermission", "Notifyreviewers: actualiza el permiso"),
    ("prvReadsanic_chtbltransactions",                "DownloadData"),
    ("prvWritesanic_transactionbatches",              "DownloadData: estampa el lote"),
    ("prvCreatesanic_chtblxmlreading",                "ProcessXMLFiles"),
    ("prvReadsanic_chtblxmlreading",                  "ProcessXMLFiles"),
    ("prvWritesanic_chtblxmlreading",                 "ProcessXMLFiles"),
    ("prvCreatesanic_chtblxmlreadingdetail",          "ProcessXMLFiles: detalle"),
    ("prvWritesanic_chtblemailsattachments",          "ProcessEmail: actualiza el adjunto"),
]

# El rol de flujos se asigna por ROLEID, no por nombre: en dry-run el renombre
# todavia no ocurrio y buscarlo por el nombre nuevo lo saltearia en silencio.
ROLES_A_ASIGNAR_POR_NOMBRE = [
    ("Basic User", "minimo de plataforma; sin esto la cuenta no opera en Dataverse"),
]


def traza(etapa, msg):
    print(f"[{etapa}] {msg}", flush=True)


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


def rol(api, token, nombre):
    q = urllib.parse.urlencode({"$select": "roleid,name", "$filter": f"name eq '{nombre}'"})
    r = llamar(api, token, "GET", f"roles?{q}")["value"]
    return r[0] if r else None


def tiene_priv(api, token, roleid, priv):
    q = urllib.parse.urlencode({"$select": "name", "$filter": f"name eq '{priv}'"})
    return bool(llamar(api, token, "GET",
                       f"roles({roleid})/roleprivileges_association?{q}")["value"])


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR" if APPLY else
                    "MODO: DRY-RUN (no se toca nada; usar --apply)")
    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    # --- 1. privilegios ------------------------------------------------------
    r = rol(api, token, ROL_ACTUAL) or rol(api, token, ROL_NUEVO)
    if not r:
        raise SystemExit(f"No existe ni {ROL_ACTUAL} ni {ROL_NUEVO}.")
    rid = r["roleid"]
    traza("ROL", f"{r['name']} (roleid {rid})")

    a_agregar = []
    for priv, motivo in PRIVILEGIOS:
        if tiene_priv(api, token, rid, priv):
            traza("PRIVILEGIO", f"  ya tiene {priv}")
            continue
        q = urllib.parse.urlencode({"$select": "privilegeid", "$filter": f"name eq '{priv}'"})
        p = llamar(api, token, "GET", f"privileges?{q}")["value"]
        if not p:
            traza("PRIVILEGIO", f"  NO SE RESOLVIO {priv} — se salta")
            continue
        traza("PRIVILEGIO", f"  += {priv:<46}{motivo}")
        a_agregar.append(p[0]["privilegeid"])

    if a_agregar and APPLY:
        llamar(api, token, "POST",
               f"roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
               {"Privileges": [{"PrivilegeId": p, "Depth": "Global"} for p in a_agregar]})
        traza("PRIVILEGIO", f"  {len(a_agregar)} agregados")

    # --- 2. renombre ---------------------------------------------------------
    if r["name"] == ROL_NUEVO:
        traza("RENOMBRE", f"  ya se llama {ROL_NUEVO}")
    elif rol(api, token, ROL_NUEVO):
        traza("RENOMBRE", f"  ABORTA: ya existe otro rol llamado {ROL_NUEVO}")
    else:
        traza("RENOMBRE", f"  {ROL_ACTUAL} -> {ROL_NUEVO} (roleid no cambia)")
        if APPLY:
            llamar(api, token, "PATCH", f"roles({rid})", {"name": ROL_NUEVO})
            traza("RENOMBRE", "  OK")

    # --- 3. asignacion al usuario --------------------------------------------
    q = urllib.parse.urlencode({"$select": "systemuserid,fullname",
                                "$filter": f"fullname eq '{USUARIO}'"})
    us = llamar(api, token, "GET", f"systemusers?{q}")["value"]
    if not us:
        raise SystemExit(f"No se encontro el usuario {USUARIO}.")
    uid = us[0]["systemuserid"]
    actuales = {x["name"] for x in llamar(api, token, "GET",
                f"systemusers({uid})/systemuserroles_association?$select=name")["value"]}
    traza("USUARIO", f"{USUARIO} (systemuserid {uid}); roles directos hoy: "
                     f"{sorted(actuales) or '(ninguno)'}")
    # el rol de flujos, por roleid (ya resuelto arriba, sobrevive al renombre)
    objetivos = [(rid, ROL_NUEVO, "rol de la cuenta de servicio de los flujos")]
    for nombre, motivo in ROLES_A_ASIGNAR_POR_NOMBRE:
        rr = rol(api, token, nombre)
        if not rr:
            traza("ASIGNACION", f"  NO EXISTE el rol {nombre} — se salta")
            continue
        objetivos.append((rr["roleid"], nombre, motivo))

    for roleid, nombre, motivo in objetivos:
        if nombre in actuales or ROL_ACTUAL in actuales and roleid == rid:
            traza("ASIGNACION", f"  ya tiene {nombre}")
            continue
        traza("ASIGNACION", f"  += {nombre:<24}{motivo}")
        if APPLY:
            llamar(api, token, "POST",
                   f"systemusers({uid})/systemuserroles_association/$ref",
                   {"@odata.id": f"{api}/roles({roleid})"})
            traza("ASIGNACION", "  OK")

    print()
    if APPLY:
        traza("FIN", "Aplicado. FALTA lo de portal: crear las 7 conexiones COMO User 5, "
                     "re-apuntar las referencias CH-CR-* y reactivar los 11 flujos.")
        traza("FIN", "Reexportar CashHubCore y correr pac solution unpack.")
    else:
        traza("FIN", "Dry-run terminado. Nada fue modificado.")


if __name__ == "__main__":
    main()
