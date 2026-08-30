#!/usr/bin/env python3
"""
Rellena sanic_cajero (y donde se puede, sanic_supervisorautoriza) en los asientos
contables que ya existian antes de que las columnas existieran.

POR QUE EXISTE. El tablero de corte agrupaba por createdby —la identidad que
ESCRIBE la fila—, y el asiento lo escribe un plugin del servidor. Resultado: el
trabajo de todos los cajeros se apilaba bajo "SYSTEM" y un cajero real no aparecia
nunca. De 4 "cajeros" que mostraba el rail, ninguno era un cajero.

Desde ahora el cajero se ESTAMPA al crear el asiento. Este script arregla lo de
antes, y solo lo que se puede probar:

    transaccion -> RelatedDepositRecounting -> deposito
                -> RecountingSessionMirror  -> sesion -> Cashier

LO QUE NO SE PUEDE PROBAR NO SE INVENTA. Los asientos sin deposito asociado quedan
sin cajero: son 2968 y TODOS son sembrados —2660 del service principal, 308 de
Randy DEV, entre el 11 y el 29 de agosto—; ninguno salio del flujo real, porque un
asiento de verdad nace al cerrar una tula y siempre trae su deposito. Adivinarles
un dueño seria peor que dejarlos vacios: el vacio se ve, la mentira no.

El supervisor NO se rellena: no hay forma de reconstruir quien firmo una
aprobacion vieja sin cruzar solicitudes por tula y fecha, y una firma contable
inferida no es una firma. De ahora en adelante se estampa al crear.

Es IDEMPOTENTE: solo toca los que estan vacios.

Uso:
  ./scripts/backfill-cajero-en-transacciones.py            # dry-run
  ./scripts/backfill-cajero-en-transacciones.py --apply
"""
import json
import os
import subprocess
import sys
import urllib.parse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor

APPLY = "--apply" in sys.argv

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
    cmd = ["curl", "-s", "-w", "\n__C__%{http_code}", *H, "-X", method,
           path if path.startswith("http") else API + path]
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


def page(path):
    """Pagina completa: @odata.count topa en 5000, el nextLink no."""
    out, d = [], req("GET", path)
    out += d["value"]
    while "@odata.nextLink" in d:
        d = req("GET", d["@odata.nextLink"])
        out += d["value"]
    return out


print("leyendo asientos sin cajero...")
tx = page("/sanic_chtbltransactionses?$select=sanic_name,_sanic_relateddepositrecounting_value"
          "&$filter=" + urllib.parse.quote("_sanic_cajero_value eq null"))
print(f"  asientos sin cajero: {len(tx)}")

con = [t for t in tx if t.get("_sanic_relateddepositrecounting_value")]
print(f"  con deposito asociado (se pueden resolver): {len(con)}")
print(f"  sin deposito (quedan vacios, a proposito):  {len(tx) - len(con)}")

if not con:
    print("\nnada que rellenar.")
    sys.exit(0)

# Los dos saltos se resuelven UNA vez por deposito y una vez por sesion, no por
# asiento: varios asientos comparten deposito.
print("\nresolviendo deposito -> sesion -> cajero...")
dep_ids = {t["_sanic_relateddepositrecounting_value"] for t in con}
deps = page("/sanic_chtbldepositsrecountings?$select=_sanic_recountingsessionmirror_value")
dep2ses = {d["sanic_chtbldepositsrecountingid"]: d.get("_sanic_recountingsessionmirror_value")
           for d in deps if d["sanic_chtbldepositsrecountingid"] in dep_ids}

ses = page("/sanic_chtblrecountingsessions?$select=_sanic_cashier_value")
ses2caj = {s["sanic_chtblrecountingsessionid"]: s.get("_sanic_cashier_value") for s in ses}

plan, sin_cadena = [], 0
for t in con:
    sid = dep2ses.get(t["_sanic_relateddepositrecounting_value"])
    caj = ses2caj.get(sid) if sid else None
    if caj:
        plan.append((t["sanic_chtbltransactionsid"], caj))
    else:
        sin_cadena += 1

print(f"  resueltos: {len(plan)}")
print(f"  con deposito pero sin sesion o sin cajero: {sin_cadena}")

nombres = {}
for _, caj in plan:
    if caj not in nombres:
        nombres[caj] = req("GET", f"/systemusers({caj})?$select=fullname").get("fullname", "?")
print("\nquedarian atribuidos asi:")
for caj, n in Counter(c for _, c in plan).most_common(12):
    print(f"   {nombres[caj]:<28} {n}")

if not APPLY:
    print(f"\nDRY-RUN: no se escribio nada. Con --apply se rellenan {len(plan)} asientos.")
    sys.exit(0)


def patch(item):
    txid, caj = item
    try:
        req("PATCH", f"/sanic_chtbltransactionses({txid})",
            {"sanic_Cajero@odata.bind": f"/systemusers({caj})"})
        return None
    except Exception as e:
        return f"{txid}: {e}"


print(f"\nrellenando {len(plan)} asientos...")
with ThreadPoolExecutor(max_workers=8) as ex:
    errores = [e for e in ex.map(patch, plan) if e]
print(f"  ok: {len(plan) - len(errores)}   fallaron: {len(errores)}")
for e in errores[:5]:
    print(f"    {e}")

restan = page("/sanic_chtbltransactionses?$select=sanic_name&$filter="
              + urllib.parse.quote("_sanic_cajero_value eq null"))
print(f"\nasientos que siguen sin cajero: {len(restan)} "
      f"(los sembrados sin deposito; es lo esperado)")
