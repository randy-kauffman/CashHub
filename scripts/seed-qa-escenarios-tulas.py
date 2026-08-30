#!/usr/bin/env python3
"""
Siembra los 6 escenarios de apertura de tula en CH-App-Recounting.

POR QUE EXISTE. El entorno no tenia ninguna combinacion util para probar la
pantalla de apertura: de 5128 tulas en boveda sin declaracion no habia UN SOLO
numero repetido, y las declaradas sembradas no traian cheques de los dos tipos
ni las tres monedas. Sin esa data no se puede ver como reacciona el dialogo de
desambiguacion, que es justo donde el cajero decide que tula tiene en la mesa.

LOS 6 ESCENARIOS (el numero de tula es el que se escanea):

  QA-E1  1 tula  · 1 declarada YA ENLAZADA a otra tula
  QA-E2  1 tula  · 1 declarada libre
  QA-E3  3 tulas · 1 declarada libre
  QA-E4  3 tulas · 3 declaradas libres
  QA-E5  3 tulas · 1 declarada YA ENLAZADA a otra tula
  QA-E6  3 tulas · 3 declaradas, TODAS ya enlazadas a otras tulas

"Ya enlazada" = la bolsa declarada tiene su lookup BagID apuntando a una tula
senuelo (QA-Ex-OTRA). Importa porque el escaneo solo considera declaradas con
BagID en blanco: Filter('CH-Tbl-DeclaredBags', BagNumber = X && IsBlank(BagID)).

CADA BOLSA DECLARADA trae las tres monedas y los dos tipos de cheque:
  deposito NIO  efectivo 15.000,50 · cheques BAC 2.500,00 · otros 1.200,00
  deposito USD  efectivo    800,25 · cheques BAC   300,00
  deposito EUR  efectivo    450,00 · cheques otros 220,00
Los cheques suman EXACTO el monto declarado de su deposito: si no, el recuento
arrastra una diferencia que no viene del conteo sino de la siembra.

Las tulas de un mismo numero se reparten entre tres planillas distintas
(sucursales distintas), que es lo que hace util al dialogo de desambiguacion.

Es RE-EJECUTABLE: borra lo que sembro antes (todo lo que empieza con QA-E)
antes de crear. No toca nada mas.

Uso: ./scripts/seed-qa-escenarios-tulas.py
"""
import json
import os
import subprocess
import sys
import urllib.parse
import uuid

# --- conexion -----------------------------------------------------------------
env = {}
for line in open(os.environ.get("ENV_FILE", "pp_secrets.env"), encoding="utf-8"):
    line = line.strip()
    if line and not line.startswith("#") and "=" in line:
        k, v = line.split("=", 1)
        env[k.strip()] = v.strip().rstrip("\r")

URL = env["DATAVERSE_URL"].rstrip("/")
tok = subprocess.run(
    ["curl", "-s", "-X", "POST",
     f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
     "-d", (f"grant_type=client_credentials&client_id={env['PP_CLIENT_ID']}"
            f"&client_secret={env['PP_CLIENT_SECRET']}&scope={URL}/.default")],
    capture_output=True, text=True).stdout
TOKEN = json.loads(tok)["access_token"]
API = URL + "/api/data/v9.2"
H = ["-H", f"Authorization: Bearer {TOKEN}", "-H", "Content-Type: application/json",
     "-H", "Accept: application/json"]


def call(method, path, body=None, prefer=None):
    cmd = ["curl", "-s", "-w", "\n__C__%{http_code}", *H]
    if prefer:
        cmd += ["-H", f"Prefer: {prefer}"]
    cmd += ["-X", method, API + path]
    tmp = None
    if body is not None:
        import tempfile
        tmp = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8")
        json.dump(body, tmp)
        tmp.close()
        cmd += ["--data-binary", "@" + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp:
            os.unlink(tmp.name)
    payload, _, code = out.rpartition("\n__C__")
    if not payload.strip():
        if 200 <= int(code or 0) < 300:
            return {}
        raise RuntimeError(f"{method} {path} -> HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if "error" in d:
        raise RuntimeError(f"{method} {path} -> {d['error'].get('message','')[:300]}")
    return d


def q(entityset, select=None, filter=None, top=None):
    p = []
    if select:
        p.append("$select=" + urllib.parse.quote(select))
    if filter:
        p.append("$filter=" + urllib.parse.quote(filter))
    if top:
        p.append(f"$top={top}")
    return call("GET", "/" + entityset + ("?" + "&".join(p) if p else "")).get("value", [])


def crear(entityset, body):
    return call("POST", "/" + entityset, body, prefer="return=representation")


# --- catalogos: las tres planillas dan sucursal, lote y cliente distintos -----
PLANILLAS = []
for receipt in ("53543656", "523452345", "5234503"):
    r = q("sanic_chtblreceptionses",
          select="sanic_receiptid,sanic_remitentname,_sanic_receptionbatch_value,_sanic_remitentclient_value",
          filter=f"sanic_receiptid eq '{receipt}'")
    if not r:
        sys.exit(f"No existe la planilla {receipt}; ajusta el catalogo del script.")
    PLANILLAS.append(r[0])

NIO, USD, EUR = 159460000, 159460001, 159460002
BAC, OTROS = 159460000, 159460001
BOVEDA = 159460001

# El contenido declarado, identico para todas: tres monedas y los dos tipos de
# cheque. Los cheques suman EXACTO el monto declarado de su deposito.
DEPOSITOS = [
    dict(moneda=NIO, cuenta=100200111, efectivo=15000.50, bac=2500.00, otros=1200.00,
         ref="Venta semana 31", cheques=[(BAC, 1500.00), (BAC, 1000.00), (OTROS, 1200.00)]),
    dict(moneda=USD, cuenta=100200112, efectivo=800.25, bac=300.00, otros=0.00,
         ref="Exportacion agosto", cheques=[(BAC, 300.00)]),
    dict(moneda=EUR, cuenta=100200113, efectivo=450.00, bac=0.00, otros=220.00,
         ref="Cobro Europa", cheques=[(OTROS, 220.00)]),
]
TOTAL_NIO = 15000.50 + 2500.00 + 1200.00
TOTAL_USD = 800.25 + 300.00
TOTAL_EUR = 450.00 + 220.00

ESCENARIOS = [
    ("QA-E1", 1, 1, True,  "1 tula  · 1 declarada YA ENLAZADA a otra tula"),
    ("QA-E2", 1, 1, False, "1 tula  · 1 declarada libre"),
    ("QA-E3", 3, 1, False, "3 tulas · 1 declarada libre"),
    ("QA-E4", 3, 3, False, "3 tulas · 3 declaradas libres"),
    ("QA-E5", 3, 1, True,  "3 tulas · 1 declarada YA ENLAZADA a otra tula"),
    ("QA-E6", 3, 3, True,  "3 tulas · 3 declaradas, TODAS ya enlazadas"),
]


def limpiar():
    """Borra lo sembrado antes. De hijo a padre, que es el unico orden que Dataverse acepta."""
    borrados = 0
    dbs = [x for x in q("sanic_chtbldeclaredbagses", select="sanic_bagnumber", top=5000)
           if (x.get("sanic_bagnumber") or "").startswith("QA-E")]
    for db in dbs:
        did = db["sanic_chtbldeclaredbagsid"]
        for dep in q("sanic_chtbldeclareddepositses", select="sanic_name",
                     filter=f"_sanic_declaredbag_value eq {did}"):
            pid = dep["sanic_chtbldeclareddepositsid"]
            for ch in q("sanic_chtbldeclaredcheckses", select="sanic_name",
                        filter=f"_sanic_declareddepositid_value eq {pid}"):
                call("DELETE", f"/sanic_chtbldeclaredcheckses({ch['sanic_chtbldeclaredchecksid']})")
                borrados += 1
            call("DELETE", f"/sanic_chtbldeclareddepositses({pid})")
            borrados += 1
        call("DELETE", f"/sanic_chtbldeclaredbagses({did})")
        borrados += 1
    for b in [x for x in q("sanic_chtblbagses", select="sanic_bagid", top=5000)
              if (x.get("sanic_bagid") or "").startswith("QA-E")]:
        bid = b["sanic_chtblbagsid"]
        # El guard no deja borrar una tula de un lote ya en boveda, salvo que este
        # PENDIENTE DE SUPERVISOR — que es la puerta que el circuito de aprobacion
        # usa justamente para dar de baja. Se la pone ahi y recien despues se borra.
        try:
            call("PATCH", f"/sanic_chtblbagses({bid})", {"sanic_bagstatus": 159460005})
            call("DELETE", f"/sanic_chtblbagses({bid})")
            borrados += 1
        except RuntimeError as e:
            print(f"     no se pudo borrar la tula {b.get('sanic_bagid')}: {str(e)[-110:]}")
    for d in [x for x in q("sanic_chtbldeclarations", select="sanic_name", top=5000)
              if (x.get("sanic_name") or "").startswith("QA-E")]:
        call("DELETE", f"/sanic_chtbldeclarations({d['sanic_chtbldeclarationid']})")
        borrados += 1
    print(f"  limpieza: {borrados} registros previos borrados\n")


def crear_tula(numero, planilla, estado=BOVEDA):
    return crear("sanic_chtblbagses", {
        "sanic_name": numero,
        "sanic_bagid": numero,
        "sanic_bagstatus": estado,
        "sanic_ReceptionID@odata.bind": f"/sanic_chtblreceptionses({planilla['sanic_chtblreceptionsid']})",
        "sanic_ReceptionBatchId@odata.bind":
            f"/sanic_chtblreceptionbatchs({planilla['_sanic_receptionbatch_value']})",
        "sanic_totalnio": 0, "sanic_totalusd": 0, "sanic_totaleur": 0,
    })["sanic_chtblbagsid"]


def crear_declarada(numero, planilla, idx, enlazar_a=None):
    decl = crear("sanic_chtbldeclarations", {
        "sanic_name": f"{numero} declaracion {idx}",
        "sanic_sourceentity": "sanic_chtblreceivedemails",
        "sanic_sourcerecordid": str(uuid.uuid4()),
        "sanic_Client@odata.bind": f"/sanic_chtblclients({planilla['_sanic_remitentclient_value']})",
    })["sanic_chtbldeclarationid"]

    cuerpo = {
        "sanic_name": f"{numero}-D{idx}",
        "sanic_bagnumber": numero,
        "sanic_totalnio": TOTAL_NIO, "sanic_totalusd": TOTAL_USD, "sanic_totaleur": TOTAL_EUR,
        "sanic_totaldeposits": len(DEPOSITOS),
        "sanic_DeclarationID@odata.bind": f"/sanic_chtbldeclarations({decl})",
    }
    if enlazar_a:
        cuerpo["sanic_BagID@odata.bind"] = f"/sanic_chtblbagses({enlazar_a})"
    db = crear("sanic_chtbldeclaredbagses", cuerpo)["sanic_chtbldeclaredbagsid"]

    ncheque = 0
    for n, dep in enumerate(DEPOSITOS, start=1):
        pid = crear("sanic_chtbldeclareddepositses", {
            "sanic_name": f"Deposito {n}",
            "sanic_accountnumber": dep["cuenta"],
            "sanic_currency": dep["moneda"],
            "sanic_cashamount": dep["efectivo"],
            "sanic_baclocalcheckamount": dep["bac"],
            "sanic_otherlocalcheckamount": dep["otros"],
            "sanic_depositreference": dep["ref"],
            "sanic_transactionno": n,
            "sanic_DeclaredBag@odata.bind": f"/sanic_chtbldeclaredbagses({db})",
            "sanic_Declaration@odata.bind": f"/sanic_chtbldeclarations({decl})",
        })["sanic_chtbldeclareddepositsid"]
        for tipo, monto in dep["cheques"]:
            ncheque += 1
            num = f"9{idx}{n}{ncheque:04d}"
            crear("sanic_chtbldeclaredcheckses", {
                "sanic_name": f"Cheque {num}",
                "sanic_checknumber": num,
                "sanic_checkamount": monto,
                "sanic_checkcurrency": dep["moneda"],
                "sanic_checktype": tipo,
                "sanic_DeclaredDepositID@odata.bind": f"/sanic_chtbldeclareddepositses({pid})",
                "sanic_DeclaredBagMirror@odata.bind": f"/sanic_chtbldeclaredbagses({db})",
                "sanic_Declaration@odata.bind": f"/sanic_chtbldeclarations({decl})",
            })
    return db, ncheque


print("Sembrando escenarios de apertura de tula\n")
limpiar()

for numero, n_tulas, n_decl, enlazadas, desc in ESCENARIOS:
    print(f"{numero}  {desc}")
    for i in range(n_tulas):
        pl = PLANILLAS[i % len(PLANILLAS)]
        crear_tula(numero, pl)
        print(f"     tula en boveda · planilla {pl['sanic_receiptid']} · {pl['sanic_remitentname']}")
    for j in range(n_decl):
        pl = PLANILLAS[j % len(PLANILLAS)]
        senuelo = None
        if enlazadas:
            # La senuelo nace en boveda como cualquier otra: el guard del servidor
            # no deja que una tula nazca en un estado del ciclo de recuento. No
            # compite en el escaneo porque lleva OTRO numero (QA-Ex-OTRA); lo unico
            # que hace es ocupar el enlace de la declarada.
            senuelo = crear_tula(f"{numero}-OTRA", pl)
        db, nch = crear_declarada(numero, pl, j + 1, enlazar_a=senuelo)
        if senuelo:
            call("PATCH", f"/sanic_chtblbagses({senuelo})",
                 {"sanic_BagDeclarationID@odata.bind": f"/sanic_chtbldeclaredbagses({db})"})
        estado = f"ENLAZADA a {numero}-OTRA" if senuelo else "libre"
        print(f"     declarada {numero}-D{j+1} · {len(DEPOSITOS)} depositos · {nch} cheques · {estado}")
    print()

print("Listo. Escanea QA-E1 .. QA-E6 en CH-App-Recounting.")
