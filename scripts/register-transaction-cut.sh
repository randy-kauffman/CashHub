#!/usr/bin/env bash
# =============================================================================
# Registra el corte de transacciones a lote (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.CreateTransactionCutPlugin
#   3. customapi       sanic_CHCreateTransactionCut (global, POST; parametro de
#                      entrada CajeroId Guid REQUERIDO; respuestas
#                      BatchId/BatchName/AssignedCount)
#
# Reemplaza el UpdateIf client-side de CH-App-Digitization (cap 2000, no atomico,
# sin excluir Aplicadas). El corte corre server-side y POR CAJERO: crea el lote en
# Generado explicito estampado con sanic_cajero/sanic_generadopor, asigna las
# transacciones elegibles del cajero (createdby) en lotes de 500 y no deja lotes
# vacios (regla W6).
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-transaction-cut.sh
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
    # El assembly en base64 supera el limite de argumentos del SO: el body va por archivo.
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    tmp = None
    if body is not None:
        import tempfile
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp)
        tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp:
            os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(path):
    return req('GET', path).get('value', [])

def upsert(entityset, filter_expr, body, label, select_id):
    rows = find(f"/{entityset}?$select={select_id}&$filter={urllib.parse.quote(filter_expr)}")
    if rows:
        req('PATCH', f"/{entityset}({rows[0][select_id]})", body)
        print(f"  actualizado: {label}")
        return rows[0][select_id]
    created = req('POST', f"/{entityset}", body)
    print(f"  creado: {label}")
    return created[select_id]

content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

# 1. pluginassembly
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CreateTransactionCutPlugin'", {
    'typename': 'CashHub.Plugins.CreateTransactionCutPlugin',
    'friendlyname': 'CashHub Create Transaction Cut',
    'name': 'CashHub.Plugins.CreateTransactionCutPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CreateTransactionCutPlugin', 'plugintypeid')

# 3. customapi + parametro de entrada + respuestas. El corte es por cajero:
#    CajeroId (Guid, REQUERIDO) es el systemuserid cuyas transacciones toma el
#    corte. Tipos segun la tabla de customapi(request|response)properties:
#    Guid=12, String=10, Integer=7 (mismo codigo que usa FailedFindings en
#    register-validation-plugin.sh; 6 es Float, no Integer).
api_id = upsert('customapis', "uniquename eq 'sanic_CHCreateTransactionCut'", {
    'uniquename': 'sanic_CHCreateTransactionCut',
    'name': 'CH Create Transaction Cut',
    'displayname': 'CH Create Transaction Cut',
    'description': 'Corta las transacciones Batch pendientes sin lote a un lote nuevo (atomico, server-side).',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHCreateTransactionCut', 'customapiid')

# Parametro de entrada CajeroId (upsert idempotente, como las respuestas).
upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'CajeroId'", {
    'uniquename': 'CajeroId', 'name': 'CajeroId', 'displayname': 'CajeroId',
    'description': 'systemuserid del cajero cuyas transacciones toma el corte (requerido).',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro CajeroId', 'customapirequestparameterid')

for uname, ptype in [('BatchId', 12), ('BatchName', 10), ('AssignedCount', 7)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro del corte de transacciones completo.")
PYEOF
