#!/usr/bin/env bash
# =============================================================================
# Registra el espejo de estado de lote en transacciones (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.TransactionBatchStatusMirrorPlugin
#   3. pasos SINCRONOS:
#        Post-operation (40) Update de sanic_transactionbatches (filtrado en
#          sanic_batchstatus): propaga el estado a las transacciones del lote.
#        Pre-operation (20) Create de sanic_chtbltransactions: estampa el espejo.
#        Pre-operation (20) Update de sanic_chtbltransactions (filtrado en
#          sanic_transactionbatch): re-estampa al re-colgar de otro lote.
#
# El espejo (sanic_batchstatus, entero) existe para que las apps filtren
# transacciones por estado de lote con delegacion. El futuro guard de transacciones debe dejar pasar las escrituras del espejo.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-transaction-mirror.sh
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
        if tmp:
            os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(path):
    return req('GET', path).get('value', [])

def one(entityset, filter_expr, select):
    rows = find(f"/{entityset}?$select={select}&$filter={urllib.parse.quote(filter_expr)}")
    if not rows:
        raise RuntimeError(f"No se encontro {entityset} con filtro: {filter_expr}")
    return rows[0][select]

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

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.TransactionBatchStatusMirrorPlugin'", {
    'typename': 'CashHub.Plugins.TransactionBatchStatusMirrorPlugin',
    'friendlyname': 'CashHub Transaction Batch Status Mirror',
    'name': 'CashHub.Plugins.TransactionBatchStatusMirrorPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype TransactionBatchStatusMirrorPlugin', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update')}

# (entidad, mensaje, stage, filteringattributes, descripcion)
STEPS = [
    ('sanic_transactionbatches', 'Update', 40, 'sanic_batchstatus',
     'Propaga el estado del lote al espejo sanic_batchstatus de sus transacciones.'),
    ('sanic_chtbltransactions', 'Create', 20, None,
     'Estampa sanic_batchstatus desde el lote destino.'),
    ('sanic_chtbltransactions', 'Update', 20, 'sanic_transactionbatch',
     'Re-estampa sanic_batchstatus al re-colgar la transaccion de lote (corte o desasignacion).'),
]

for entity, message, stage, filtering, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.TransactionMirror: {message} de {entity}"
    body = {
        'name': name,
        'description': description,
        'plugintypeid@odata.bind': f"/plugintypes({type_id})",
        'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
        'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
        'stage': stage, 'mode': 0, 'rank': 10, 'supporteddeployment': 0,
        'asyncautodelete': False, 'invocationsource': 0,
    }
    if filtering:
        body['filteringattributes'] = filtering
    upsert('sdkmessageprocessingsteps', f"name eq '{name}'", body, name, 'sdkmessageprocessingstepid')

print("Registro del espejo de estado completo.")
PYEOF
