#!/usr/bin/env bash
# =============================================================================
# Deploy quirurgico de CH-Flow-DownloadData endurecido (S2 / V-3), sin import de
# solucion (no pisa otros cambios del disenador web). Patch del clientdata +
# reactivacion impersonando al owner (el SP no registra el trigger; patron del
# proyecto deploy-cloud-flows-owner-reactivation).
#
# Cambios que trae: Validar_LoteId (corta inyeccion OData), Dataverse invoker (no
# embedded), sin OneDrive ni share link (descarga local via csvBase64).
#
# Requiere pp_secrets.env. Uso: ./scripts/deploy-downloaddata-flow.sh
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
import json, os, subprocess, tempfile

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
TOKEN = os.environ['TOKEN']
WF = '64049da2-f2c6-f011-8543-7c1e5267db57'
SRC = 'solutions/unpacked/flow/Workflows/CH-Flow-DownloadData-64049DA2-F2C6-F011-8543-7C1E5267DB57.json'

def call(method, path, body=None, caller=None):
    hdr = ['-H', f'Authorization: Bearer {TOKEN}', '-H', 'Content-Type: application/json',
           '-H', 'If-Match: *']
    if caller:
        hdr += ['-H', f'MSCRMCallerID: {caller}']
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *hdr, '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close(); cmd += ['--data-binary', '@' + tmp.name]
    try:
        raw = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    payload, _, code = raw.rpartition('\n')
    code = int(code)
    if code >= 400:
        raise RuntimeError(f"{method} {path} -> {code}: {payload[:400]}")
    return json.loads(payload) if payload.strip() else {}

# 0. Owner actual (para reactivar como el)
wf = call('GET', f"/workflows({WF})?$select=_ownerid_value,statecode")
owner = wf['_ownerid_value']
print(f"owner del flow: {owner} (statecode actual {wf['statecode']})")

# 1. clientdata nuevo desde el source, quitando templateName (Dev no lo trae)
mine = json.load(open(SRC))
mine['properties'].pop('templateName', None)
clientdata = json.dumps(mine, separators=(',', ':'), ensure_ascii=False)
print(f"clientdata nuevo: {len(clientdata)} chars, acciones: {list(mine['properties']['definition']['actions'].keys())}")

try:
    # 2. desactivar (statecode 0 / statuscode 1)
    call('PATCH', f"/workflows({WF})", {'statecode': 0, 'statuscode': 1})
    print("flow desactivado")

    # 3. patch del clientdata
    call('PATCH', f"/workflows({WF})", {'clientdata': clientdata})
    print("clientdata actualizado")

    # 4. reactivar impersonando al owner (el SP no registra el trigger)
    call('PATCH', f"/workflows({WF})", {'statecode': 1, 'statuscode': 2}, caller=owner)
    print("flow reactivado como owner")
except Exception as e:
    print(f"ERROR: {e}")
    print("El flow puede haber quedado DESACTIVADO — reactivar manualmente en el portal si hace falta.")
    raise

# 5. verificar
chk = call('GET', f"/workflows({WF})?$select=statecode,statuscode")
print(f"VERIFICACION -> statecode {chk['statecode']} statuscode {chk['statuscode']} (1/2 = activado OK)")
PYEOF
