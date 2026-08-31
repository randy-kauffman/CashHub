#!/usr/bin/env python3
# =============================================================================
# Cierra las 2 brechas de privilegios detectadas en
# docs/referencia/analisis-roles-seguridad.md y renombra CH-Role -> CH-SR-Hub.
#
# BRECHA 1 — CH-SR-Receptionist sin Read sobre CH-Tbl-Client:
#   La app de Recepcion hace un dot-walk ClientBranch -> Client -> Name en
#   scrCaptura.fx.yaml:433. Sin el privilegio el dot-walk devuelve vacio y la
#   planilla muestra siempre "Empresa sin seleccionar". No rompe la pantalla, por
#   eso paso desapercibido. Hay precedente: provision-tracker-role.sh tomo la MISMA
#   decision para el rol de Tracker, con el mismo razonamiento y Global.
#
# BRECHA 2 — CH-SR-EmailIngestionFlow sin Read sobre CH-Tbl-ValidationRuns:
#   CH-Flow-MailboxSweep (ACTIVO) hace ListRecords sobre esa tabla.
#
# RENOMBRE — CH-Role -> CH-SR-Hub:
#   Los unicos privilegios custom que da son Read sobre AppCatalog y
#   AppCatalogAccess: es el rol de acceso al Hub. El nombre sin sufijo SR- no lo
#   decia. El GUID no cambia, asi que las asignaciones (3 usuarios y 2 equipos
#   aprobadores) sobreviven — mismo caso que CH-SR-Counter -> CH-SR-Cashier.
#
# OJO — Depth es un ENUM de OData (Microsoft.Dynamics.CRM.PrivilegeDepth), NO un
# entero. Mandar 0/1/2/3 hace reventar el deserializador con un error de enum.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/fix-roles-seguridad.py            # dry-run
#       ./scripts/fix-roles-seguridad.py --apply    # ejecuta
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

# (rol, privilegio, profundidad, motivo)
PRIVILEGIOS = [
    ("CH-SR-Receptionist", "prvReadsanic_CHTblClient", "Global",
     "dot-walk ClientBranch->Client->Name en scrCaptura.fx.yaml:433; es lectura de "
     "nombre, Global es aceptable (mismo criterio que provision-tracker-role.sh)"),
    ("CH-SR-EmailIngestionFlow", "prvReadsanic_CHTblValidationRuns", "Global",
     "CH-Flow-MailboxSweep hace ListRecords sobre sanic_chtblvalidationrunses"),
]

RENOMBRES = [("CH-Role", "CH-SR-Hub",
              "Solo da Read sobre AppCatalog y AppCatalogAccess: es el rol de acceso "
              "al Hub. El GUID no cambia y las asignaciones sobreviven.")]


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
    q = urllib.parse.urlencode({"$select": "roleid,name",
                                "$filter": f"name eq '{nombre}'"})
    r = llamar(api, token, "GET", f"roles?{q}")["value"]
    return r[0] if r else None


def tiene_privilegio(api, token, roleid, priv):
    q = urllib.parse.urlencode({"$select": "name", "$filter": f"name eq '{priv}'"})
    r = llamar(api, token, "GET",
               f"roles({roleid})/roleprivileges_association?{q}")["value"]
    return bool(r)


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR" if APPLY else
                    "MODO: DRY-RUN (no se toca nada; usar --apply)")
    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    # --- 1. privilegios faltantes --------------------------------------------
    for nombre, priv, depth, motivo in PRIVILEGIOS:
        r = rol(api, token, nombre)
        if not r:
            traza("PRIVILEGIO", f"  {nombre}: NO EXISTE. Se salta.")
            continue
        if tiene_privilegio(api, token, r["roleid"], priv):
            traza("PRIVILEGIO", f"  {nombre}: ya tiene {priv}. Nada que hacer.")
            continue
        q = urllib.parse.urlencode({"$select": "privilegeid",
                                    "$filter": f"name eq '{priv}'"})
        p = llamar(api, token, "GET", f"privileges?{q}")["value"]
        if not p:
            traza("PRIVILEGIO", f"  {nombre}: NO se resolvio el privilegio {priv}. Se salta.")
            continue
        traza("PRIVILEGIO", f"  {nombre} += {priv} ({depth})")
        traza("PRIVILEGIO", f"      motivo: {motivo}")
        if APPLY:
            llamar(api, token, "POST",
                   f"roles({r['roleid']})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
                   {"Privileges": [{"PrivilegeId": p[0]["privilegeid"],
                                    "Depth": depth}]})
            traza("PRIVILEGIO", "      OK")

    # --- 2. renombres ---------------------------------------------------------
    for viejo, nuevo, motivo in RENOMBRES:
        if rol(api, token, nuevo):
            traza("RENOMBRE", f"  {nuevo} ya existe. Nada que hacer.")
            continue
        r = rol(api, token, viejo)
        if not r:
            traza("RENOMBRE", f"  {viejo}: NO EXISTE. Se salta.")
            continue
        traza("RENOMBRE", f"  {viejo} -> {nuevo}  (roleid {r['roleid']}, no cambia)")
        traza("RENOMBRE", f"      motivo: {motivo}")
        if APPLY:
            llamar(api, token, "PATCH", f"roles({r['roleid']})", {"name": nuevo})
            traza("RENOMBRE", "      OK")

    print()
    if APPLY:
        traza("FIN", "Aplicado. Reexportar CashHubCore y correr pac solution unpack.")
    else:
        traza("FIN", "Dry-run terminado. Nada fue modificado.")


if __name__ == "__main__":
    main()
