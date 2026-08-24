#!/usr/bin/env bash
# =============================================================================
# Registra el Custom API del rail de cajeros (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL)
#   2. plugintype      CashHub.Plugins.CHWorkloadByCashierPlugin
#   3. customapi       sanic_CHWorkloadByCashier (global, POST; sin entrada;
#                      respuesta Workload = JSON string)
#
# Agregado por cajero (pendientes de corte / manual / lotes por aplicar + totales)
# que alimenta el rail de scrCorte/scrManual/scrLotes: el GroupBy no delega en canvas.
# La app consume el JSON con ParseJSON en el OnVisible del rail (ver 06-backend.md).
# isfunction=False (Action) a proposito: una function cachearia el workload y quedaria
# viejo. executeprivilegename restringe la invocacion a los digitadores (V-1).
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-workload.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

ASSEMBLY="src/backend/CashHub.Plugins/bin/Release/net462/CashHub.Plugins.dll"
[ -f "$ASSEMBLY" ] || { echo "Falta $ASSEMBLY — compilar primero (dotnet build -c Release)"; exit 1; }

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL ASSEMBLY

python3 - <<'PYEOF'
import base64, json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Prefer: return=representation']

def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    tmp = None
    if body is not None:
        import tempfile
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(path): return req('GET', path).get('value', [])

def upsert(entityset, filter_expr, body, label, select_id):
    rows = find(f"/{entityset}?$select={select_id}&$filter={urllib.parse.quote(filter_expr)}")
    if rows:
        req('PATCH', f"/{entityset}({rows[0][select_id]})", body); print(f"  actualizado: {label}"); return rows[0][select_id]
    created = req('POST', f"/{entityset}", body); print(f"  creado: {label}"); return created[select_id]

content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content, 'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CHWorkloadByCashierPlugin'", {
    'typename': 'CashHub.Plugins.CHWorkloadByCashierPlugin',
    'friendlyname': 'CashHub Workload By Cashier',
    'name': 'CashHub.Plugins.CHWorkloadByCashierPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CHWorkloadByCashierPlugin', 'plugintypeid')

api_id = upsert('customapis', "uniquename eq 'sanic_CHWorkloadByCashier'", {
    'uniquename': 'sanic_CHWorkloadByCashier',
    'name': 'CH Workload By Cashier', 'displayname': 'CH Workload By Cashier',
    'description': 'Agregado por cajero (pendientes + lotes + totales) para el rail; JSON string.',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvCreatesanic_TransactionBatches',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHWorkloadByCashier', 'customapiid')

# Sin parametros de entrada. Respuesta Workload = JSON string (type String=10).
upsert('customapiresponseproperties',
    f"_customapiid_value eq {api_id} and uniquename eq 'Workload'", {
    'uniquename': 'Workload', 'name': 'Workload', 'displayname': 'Workload', 'type': 10,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'respuesta Workload', 'customapiresponsepropertyid')

print("Registro del rail de cajeros completo.")
PYEOF
