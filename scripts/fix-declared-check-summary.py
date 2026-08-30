#!/usr/bin/env python3
"""
Recalcula el resumen de cheques de cada deposito declarado desde SUS PROPIAS FILAS.

POR QUE EXISTE. Al terminar una tula, la misma pantalla contestaba dos cosas
distintas sobre el mismo numero: la matriz del encabezado marcaba diferencia
C$ 0,00 y el dialogo de cierre reportaba "+C$ 200.725,44 por transaccion".

Ninguna de las dos calculaba mal. Leen de lugares distintos:

  encabezado  MontoChequesDec = Sum(colChequesDeclaradosCon, CheckAmount)
              -> suma las FILAS de CH-Tbl-DeclaredChecks
  hallazgo H3 D.BACLocalCheckAmount + D.OtherLocalCheckAmount
              -> lee las COLUMNAS RESUMEN del deposito declarado

En produccion esas dos fuentes coinciden siempre, porque el motor de validacion
de correos las obliga a coincidir ANTES de que la declaracion entre a Dataverse:

  VR-12  por deposito, el monto declarado en cheques (BAC + Otros) coincide con
         la suma de sus cheques en la tabla Cheques
  VR-28  y el desglose por TIPO tambien, no solo el total

Pero la data de QA no entro por la ingesta: la escribio seed-recounting-stress.py
directo contra la tabla, con DOS TIRADAS DE RANDOM INDEPENDIENTES —una para el
resumen del deposito, otra para el importe de cada cheque—. Nunca cuadraron: 905
de 941 depositos declarados con cheques (96%) quedaron inconsistentes. El
generador ya esta arreglado (ver cheques_declarados()); esto repara lo sembrado.

QUE LADO MANDA Y POR QUE. Mandan las FILAS. Son las que el cajero marca en la
pestana de cheques y de las que sale el monto recontado, asi que ya estan
entretejidas con los recuentos hechos. Reescribir las filas para que sumen el
resumen romperia esos recuentos; reescribir el resumen no rompe nada, porque
sanic_Total es CAMPO FORMULA y se recalcula solo.

Un deposito SIN filas de cheque debe tener el resumen en cero: es el mismo VR-12
aplicado al caso vacio. Se cuentan aparte porque cambiarles el resumen les mueve
el Total, y conviene ver cuantos son antes de aplicar.

Es IDEMPOTENTE: solo escribe los que difieren, y correrlo dos veces no hace nada
la segunda vez.

Uso:
  ./scripts/fix-declared-check-summary.py            # dry-run, no escribe
  ./scripts/fix-declared-check-summary.py --apply    # escribe
"""
import json
import os
import subprocess
import sys
import urllib.parse
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor

APPLY = "--apply" in sys.argv
CHK_BAC, CHK_OTROS = 159460000, 159460001
TOL = 0.005          # medio centavo: por debajo de eso es ruido de redondeo

# --- conexion -----------------------------------------------------------------
env = {}
for line in open(os.environ.get("ENV_FILE", "pp_secrets.env"), encoding="utf-8"):
    line = line.strip()
    if line and not line.startswith("#") and "=" in line:
        k, v = line.split("=", 1)
        env[k.strip()] = v.strip().rstrip("\r")

URL = env["DATAVERSE_URL"].rstrip("/")
API = URL + "/api/data/v9.2"
tok = subprocess.run(
    ["curl", "-s", "-X", "POST",
     f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
     "-d", (f"grant_type=client_credentials&client_id={env['PP_CLIENT_ID']}"
            f"&client_secret={env['PP_CLIENT_SECRET']}&scope={URL}/.default")],
    capture_output=True, text=True).stdout
TOKEN = json.loads(tok)["access_token"]
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
        raise SystemExit(f"{method} {path}: HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if "error" in d:
        raise SystemExit(f"{method} {path}: {d['error'].get('message', '')[:300]}")
    return d


def page(path):
    """Pagina completa: Dataverse corta de a 5000 y devuelve @odata.nextLink."""
    out, d = [], req("GET", path)
    out += d["value"]
    while "@odata.nextLink" in d:
        d = req("GET", d["@odata.nextLink"])
        out += d["value"]
    return out


# --- 1. leer los dos lados ----------------------------------------------------
print("leyendo depositos declarados y sus cheques...")
deps = page("/sanic_chtbldeclareddepositses?$select=sanic_name,sanic_cashamount,"
            "sanic_baclocalcheckamount,sanic_otherlocalcheckamount,sanic_total,"
            "_sanic_relatedrecounteddeposit_value")
chks = page("/sanic_chtbldeclaredcheckses?$select=sanic_checkamount,sanic_checktype,"
            "_sanic_declareddepositid_value")

suma = defaultdict(lambda: [0.0, 0.0, 0])
for c in chks:
    did = c.get("_sanic_declareddepositid_value")
    if not did:
        continue
    i = 0 if c.get("sanic_checktype") == CHK_BAC else 1
    suma[did][i] += c.get("sanic_checkamount") or 0
    suma[did][2] += 1

print(f"  depositos declarados: {len(deps)}")
print(f"  cheques declarados:   {len(chks)}  (repartidos en {len(suma)} depositos)")

# --- 2. calcular quien esta mal ----------------------------------------------
con_filas, sin_filas = [], []
for d in deps:
    did = d["sanic_chtbldeclareddepositsid"]
    bac, otros, n = suma.get(did, (0.0, 0.0, 0))
    bac, otros = round(bac, 2), round(otros, 2)
    rb = d.get("sanic_baclocalcheckamount") or 0
    ro = d.get("sanic_otherlocalcheckamount") or 0
    if abs(bac - rb) <= TOL and abs(otros - ro) <= TOL:
        continue
    fila = (d, bac, otros, rb, ro, n)
    (con_filas if n else sin_filas).append(fila)

enlazados = sum(1 for f in con_filas + sin_filas
                if f[0].get("_sanic_relatedrecounteddeposit_value"))

print(f"\nA CORREGIR:")
print(f"  con filas de cheque (el resumen no suma lo que suman sus cheques): {len(con_filas)}")
print(f"  sin filas de cheque (declaran monto en cheques pero no hay cheques): {len(sin_filas)}")
print(f"  de esos, ya enlazados a un deposito recontado: {enlazados}")
print(f"  ya consistentes (no se tocan): {len(deps) - len(con_filas) - len(sin_filas)}")

print("\nmuestra (primeros 10):")
for d, bac, otros, rb, ro, n in (con_filas + sin_filas)[:10]:
    print(f"  {d['sanic_name']:<16} filas={n:<3} "
          f"resumen {rb:>12,.2f}/{ro:>12,.2f}  ->  {bac:>12,.2f}/{otros:>12,.2f}")

objetivo = con_filas + sin_filas
if not objetivo:
    print("\nnada que corregir.")
    sys.exit(0)

if not APPLY:
    print(f"\nDRY-RUN: no se escribio nada. Volve a correr con --apply para "
          f"actualizar {len(objetivo)} depositos.")
    sys.exit(0)


# --- 3. escribir --------------------------------------------------------------
# sanic_Total NO se manda: es campo formula y se recalcula solo.
def patch(f):
    d, bac, otros, _rb, _ro, _n = f
    try:
        req("PATCH", f"/sanic_chtbldeclareddepositses({d['sanic_chtbldeclareddepositsid']})",
            {"sanic_baclocalcheckamount": bac, "sanic_otherlocalcheckamount": otros})
        return None
    except SystemExit as e:
        return f"{d['sanic_name']}: {e}"


print(f"\nactualizando {len(objetivo)} depositos...")
with ThreadPoolExecutor(max_workers=8) as ex:
    errores = [e for e in ex.map(patch, objetivo) if e]

print(f"  ok:      {len(objetivo) - len(errores)}")
print(f"  fallaron:{len(errores)}")
for e in errores[:10]:
    print(f"    {e}")

# --- 4. verificar releyendo ---------------------------------------------------
print("\nreverificando contra Dataverse...")
deps2 = page("/sanic_chtbldeclareddepositses?$select=sanic_name,"
             "sanic_baclocalcheckamount,sanic_otherlocalcheckamount")
malos = 0
for d in deps2:
    did = d["sanic_chtbldeclareddepositsid"]
    bac, otros, _ = suma.get(did, (0.0, 0.0, 0))
    if (abs(round(bac, 2) - (d.get("sanic_baclocalcheckamount") or 0)) > TOL or
            abs(round(otros, 2) - (d.get("sanic_otherlocalcheckamount") or 0)) > TOL):
        malos += 1
print(f"  depositos que SIGUEN inconsistentes: {malos}"
      f"{'  <- revisar' if malos else '  (VR-12 y VR-28 cumplidas)'}")
