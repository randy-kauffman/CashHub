#!/usr/bin/env python3
"""
Mueve el trabajo de una sesion de recuento a otra. Para QA.

POR QUE EXISTE. El cajero NO elige su sesion: la app le da la del dia
(scrSesion, "la sesion de HOY del cajero", filtrada por Cashier + Created On).
Asi que no hay forma de abrir una sesion vieja para probar contra volumen —y sin
volumen no se puede ver si los conteos delegan o mienten por omision a partir de
las 500 filas—. Este script trae el trabajo a la sesion que el cajero SI tiene
abierta.

COMO FUNCIONA, Y POR QUE ALCANZA CON MOVER LAS TULAS. El vinculo del cheque con
la sesion (sanic_recountingsession, espejo B-9) NO se escribe a mano: lo deriva
el plugin CashHub.CheckMirrorStamp desde la sesion de SU TULA, y hay un step
registrado sobre el Update de sanic_chtblbags. Verificado: al mover una tula de
5 cheques, los 5 cheques la siguieron solos; al revertirla, volvieron.

Los depositos y los detalles de sesion NO cuelgan de ese step y se mueven aca.

ES SIMETRICO: para deshacerlo, invertí --from y --to. No hay estado escondido.

Uso:
  ./scripts/reasignar-sesion-recuento.py --from RCS-1026 --to RCS-1033
  ./scripts/reasignar-sesion-recuento.py --from RCS-1026 --to RCS-1033 --apply
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.parse
from concurrent.futures import ThreadPoolExecutor

ap = argparse.ArgumentParser()
ap.add_argument("--from", dest="origen", required=True, help="nombre de la sesion origen, p.ej. RCS-1026")
ap.add_argument("--to", dest="destino", required=True, help="nombre de la sesion destino, p.ej. RCS-1033")
ap.add_argument("--apply", action="store_true", help="sin esto es dry-run")
a = ap.parse_args()

# --- conexion -----------------------------------------------------------------
env = {}
for line in open(os.environ.get("ENV_FILE", "pp_secrets.env"), encoding="utf-8"):
    line = line.strip()
    if line and not line.startswith("#") and "=" in line:
        k, v = line.split("=", 1)
        env[k.strip()] = v.strip().rstrip("\r")

URL = env["DATAVERSE_URL"].rstrip("/")
API = URL + "/api/data/v9.2"
TOKEN = json.loads(subprocess.run(
    ["curl", "-s", "-X", "POST",
     f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
     "-d", (f"grant_type=client_credentials&client_id={env['PP_CLIENT_ID']}"
            f"&client_secret={env['PP_CLIENT_SECRET']}&scope={URL}/.default")],
    capture_output=True, text=True).stdout)["access_token"]
H = ["-H", f"Authorization: Bearer {TOKEN}", "-H", "Content-Type: application/json",
     "-H", "Accept: application/json"]


def req(method, path, body=None):
    cmd = ["curl", "-s", "-w", "\n__C__%{http_code}", *H, "-X", method, API + path]
    if body is not None:
        cmd += ["-d", json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    payload, _, code = out.rpartition("\n__C__")
    if not payload.strip():
        if 200 <= int(code or 0) < 300:
            return {}
        raise RuntimeError(f"{method} {path}: HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if "error" in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message', '')[:250]}")
    return d


def q(es, select, filtro):
    return req("GET", f"/{es}?$select={select}&$filter={urllib.parse.quote(filtro)}").get("value", [])


def cnt(es, filtro):
    return req("GET", f"/{es}?$select=createdon&$filter={urllib.parse.quote(filtro)}"
                      "&$count=true&$top=1")["@odata.count"]


def sesion(nombre):
    r = q("sanic_chtblrecountingsessions", "sanic_name", f"sanic_name eq '{nombre}'")
    if not r:
        raise SystemExit(f"No existe la sesion {nombre}.")
    return r[0]["sanic_chtblrecountingsessionid"]


SRC, DST = sesion(a.origen), sesion(a.destino)
BIND = lambda sid: f"/sanic_chtblrecountingsessions({sid})"

# (entity set, columna del lookup, schema name para @odata.bind, etiqueta)
# Las TULAS van primero: el plugin CheckMirrorStamp propaga a los cheques desde
# el Update de la tula, asi que los cheques no se tocan aca.
TABLAS = [
    ("sanic_chtblbagses", "_sanic_recountingsession_value",
     "sanic_RecountingSession", "tulas"),
    ("sanic_chtbldepositsrecountings", "_sanic_recountingsessionmirror_value",
     "sanic_RecountingSessionMirror", "depositos"),
    ("sanic_chtblrecountingsessiondetails", "_sanic_recountingsessionid_value",
     "sanic_RecountingSessionId", "detalles de sesion"),
]
CHEQUES = ("sanic_chtblcheckses", "_sanic_recountingsession_value")


def foto(titulo):
    print(f"\n{titulo}")
    for nombre, sid in ((a.origen, SRC), (a.destino, DST)):
        partes = [f"{et}={cnt(es, f'{col} eq {sid}')}" for es, col, _, et in TABLAS]
        partes.append(f"cheques={cnt(CHEQUES[0], f'{CHEQUES[1]} eq {sid}')}")
        print(f"   {nombre:<12} " + "  ".join(partes))


foto("ESTADO ACTUAL")

pendientes = []
for es, col, schema, etiqueta in TABLAS:
    pk = es[:-2] + "id" if es.endswith("es") else es[:-1] + "id"
    filas = q(es, "createdon", f"{col} eq {SRC}")
    pendientes.append((es, schema, etiqueta, filas))
    print(f"\n{etiqueta}: {len(filas)} a mover")

total = sum(len(f) for _, _, _, f in pendientes)
if not total:
    print(f"\nNada que mover desde {a.origen}.")
    sys.exit(0)

if not a.apply:
    print(f"\nDRY-RUN: no se escribio nada. Con --apply se mueven {total} registros "
          f"de {a.origen} a {a.destino} (los cheques los arrastra el plugin).")
    sys.exit(0)


def mover(args):
    es, schema, rid = args
    try:
        req("PATCH", f"/{es}({rid})", {f"{schema}@odata.bind": BIND(DST)})
        return None
    except Exception as e:
        return f"{es} {rid}: {e}"


for es, schema, etiqueta, filas in pendientes:
    if not filas:
        continue
    pkcol = next(k for k in filas[0] if k.endswith("id") and k.startswith("sanic_"))
    print(f"\nmoviendo {len(filas)} {etiqueta}...")
    # Las tulas de a una y en serie: cada Update dispara el plugin que re-estampa
    # TODOS los cheques de esa tula. En paralelo se pisan entre si.
    workers = 1 if etiqueta == "tulas" else 8
    with ThreadPoolExecutor(max_workers=workers) as ex:
        errores = [e for e in ex.map(mover, [(es, schema, f[pkcol]) for f in filas]) if e]
    print(f"   ok: {len(filas) - len(errores)}   fallaron: {len(errores)}")
    for e in errores[:5]:
        print(f"     {e}")

foto("ESTADO DESPUES")
print(f"\nPara deshacerlo: --from {a.destino} --to {a.origen}")
