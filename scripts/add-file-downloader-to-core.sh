#!/usr/bin/env bash
# =============================================================================
# Mete el code component CashHub.FileDownloader en la solucion CashHubCore
# (idempotente), y de paso el Custom API sanic_CHGetBatchCsv que lo alimenta.
#
# POR QUE HACE FALTA UN SCRIPT PARA ESTO. `pac pcf push` deja el componente en la
# solucion **Default**, con el publisher "Default Publisher for org...". En este
# entorno eso es inaceptable: conviven Sigil, Workflow Engine, Cobranza Movil y
# Smelter, y las piezas de CashHub no pueden quedar sueltas en la Default.
#
# Y NO ALCANZA CON `--publisher-prefix sanic`: hay DOS publishers con ese mismo
# prefijo —"Sanic Corp" (uniquename `sanic`) y "Sistemas Abiertos Nicaragua"
# (uniquename `Sistemas_Abiertos_Nicaragua`)—. El prefijo NO desambigua. El que
# usan todas las soluciones CashHub es Sistemas Abiertos Nicaragua, y la unica
# forma confiable de dejar el componente bajo ese publisher es meterlo en una
# solucion suya: CashHubCore.
#
# Mismo mecanismo que el resto del backend del proyecto (AddSolutionComponent,
# nunca import de solucion) para no pisar cambios hechos en el diseñador web.
#
# El script FALLA si CashHubCore no pertenece al publisher esperado: prefiere
# frenarse a dejar la pieza en el lugar equivocado en silencio.
#
# Uso: ./scripts/add-file-downloader-to-core.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL

python3 - <<'PYEOF'
import json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Accept: application/json']

SOLUTION = 'CashHubCore'
EXPECTED_PUBLISHER = 'Sistemas_Abiertos_Nicaragua'
CONTROL_NAME = 'sanic_CashHub.FileDownloader'

# Tipos de componente de solucion.
CUSTOM_CONTROL = 66
CUSTOM_API, CUSTOM_API_REQ, CUSTOM_API_RESP = 10036, 10037, 10038


def req(method, path, body=None):
    cmd = ['curl', '-s', '-w', '\n__C__%{http_code}', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    payload, _, code = out.rpartition('\n__C__')
    if not payload.strip():
        if 200 <= int(code or 0) < 300:
            return {}
        raise RuntimeError(f"{method} {path}: HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d


def find(entityset, select, filter_expr):
    return req('GET', f"/{entityset}?$select={select}&$filter={urllib.parse.quote(filter_expr)}").get('value', [])


# 0. La solucion destino tiene que ser la del publisher correcto. Si no, se frena.
sol = req('GET', "/solutions?$select=uniquename&$expand=publisherid($select=uniquename,friendlyname)"
                 "&$filter=" + urllib.parse.quote(f"uniquename eq '{SOLUTION}'")).get('value', [])
if not sol:
    raise SystemExit(f"No existe la solucion {SOLUTION}.")
pub = sol[0].get('publisherid') or {}
if pub.get('uniquename') != EXPECTED_PUBLISHER:
    raise SystemExit(
        f"{SOLUTION} pertenece a '{pub.get('friendlyname')}' ({pub.get('uniquename')}), "
        f"no a {EXPECTED_PUBLISHER}. Se aborta para no dejar el componente mal ubicado.")
print(f"  solucion {SOLUTION} -> publisher {pub.get('friendlyname')}  OK")

# 1. El code component.
ctrl = find('customcontrols', 'customcontrolid,name,version', f"name eq '{CONTROL_NAME}'")
if not ctrl:
    raise SystemExit(f"No existe el componente {CONTROL_NAME}. Correr antes: "
                     f"cd src/pcf/FileDownloader && pac pcf push --publisher-prefix sanic")
piezas = [(ctrl[0]['customcontrolid'], CUSTOM_CONTROL, f"componente {CONTROL_NAME} v{ctrl[0]['version']}")]

# 2. El Custom API que lo alimenta, con su parametro y sus respuestas.
api = find('customapis', 'customapiid,uniquename', "uniquename eq 'sanic_CHGetBatchCsv'")
if api:
    api_id = api[0]['customapiid']
    piezas.append((api_id, CUSTOM_API, 'customapi sanic_CHGetBatchCsv'))
    for p in find('customapirequestparameters', 'customapirequestparameterid,uniquename',
                  f"_customapiid_value eq {api_id}"):
        piezas.append((p['customapirequestparameterid'], CUSTOM_API_REQ, f"parametro {p['uniquename']}"))
    for p in find('customapiresponseproperties', 'customapiresponsepropertyid,uniquename',
                  f"_customapiid_value eq {api_id}"):
        piezas.append((p['customapiresponsepropertyid'], CUSTOM_API_RESP, f"respuesta {p['uniquename']}"))
else:
    print("  aviso: sanic_CHGetBatchCsv no existe todavia (correr register-get-batch-csv.sh)")

for cid, ctype, label in piezas:
    req('POST', '/AddSolutionComponent', {
        'ComponentId': cid,
        'ComponentType': ctype,
        'SolutionUniqueName': SOLUTION,
        'AddRequiredComponents': False,
        'DoNotIncludeSubcomponents': False,
    })
    print(f"  agregado a {SOLUTION}: {label}")

print(f"Listo: {len(piezas)} componentes bajo {pub.get('friendlyname')}.")
PYEOF
