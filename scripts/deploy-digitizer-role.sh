#!/usr/bin/env bash
# =============================================================================
# Deploy quirurgico del rol CH-SR-Digitizer acotado (S3 / V-6), sin import de
# solucion. Baja los 3 prvWrite de dinero de Global a Local (AddPrivilegesRole
# actualiza el depth de un privilegio ya asignado) y quita prvReadTraceLog
# (RemovePrivilegeRole). Un solo rol (BU raiz), verificado.
#
# PrivilegeDepth enum: Basic=0 Local=1 Deep=2 Global=3 (NO confundir con el
# privilegedepthmask 1/2/4/8 que devuelve la verificacion).
#
# Requiere pp_secrets.env. Uso: ./scripts/deploy-digitizer-role.sh
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
import json, os, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
TOKEN = os.environ['TOKEN']
ROLE = '71b96c1d-a5a7-41a6-bb58-1421694d7cac'  # CH-SR-Digitizer (BU raiz)
WRITES = {  # a bajar a Local
    'prvWritesanic_CHTblChecks': '770addde-e609-4839-850b-4705f74cff64',
    'prvWritesanic_TransactionBatches': 'b5e22ba6-487e-442e-bf0f-c00cdfbfa88d',
    'prvWritesanic_CHTblTransactions': 'b835e524-ed8e-45a9-b092-cbe85b59b209',
}
TRACELOG = '6ea323ab-2e1e-414e-88f5-1f975b3069fd'  # prvReadTraceLog a quitar

def call(method, path, body=None):
    hdr = ['-H', f'Authorization: Bearer {TOKEN}', '-H', 'Content-Type: application/json']
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *hdr, '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close(); cmd += ['--data-binary', '@' + tmp.name]
    try:
        raw = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    payload, _, code = raw.rpartition('\n'); code = int(code)
    if code >= 400:
        raise RuntimeError(f"{method} {path} -> {code}: {payload[:400]}")
    return json.loads(payload) if payload.strip() else {}

# 1. Bajar los 3 Write a Local (AddPrivilegesRole actualiza el depth existente)
call('POST', f"/roles({ROLE})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {
    'Privileges': [{'PrivilegeId': pid, 'Depth': 'Local'} for pid in WRITES.values()]
})
print("Write de Checks/Transactions/TransactionBatches -> Local")

# 2. Quitar prvReadTraceLog
call('POST', f"/roles({ROLE})/Microsoft.Dynamics.CRM.RemovePrivilegeRole", {'PrivilegeId': TRACELOG})
print("prvReadTraceLog quitado")

# 3. Verificar el depthmask resultante (2=Local) y que traceLog ya no este
def dm(pid):
    r = call('GET', f"/roleprivilegescollection?$filter=" + urllib.parse.quote(
        f"roleid eq {ROLE} and privilegeid eq {pid}"))
    v = r.get('value', [])
    return v[0]['privilegedepthmask'] if v else None

print("VERIFICACION (2=Local, 8=Global, None=no asignado):")
for n, pid in WRITES.items():
    print(f"   {n}: depthmask {dm(pid)}")
print(f"   prvReadTraceLog: depthmask {dm(TRACELOG)}")
PYEOF
