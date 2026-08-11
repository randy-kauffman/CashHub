#!/usr/bin/env bash
# =============================================================================
# Registra el sellado server-side de las transiciones del lote (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.BatchTransitionStampPlugin (NUEVO)
#   3. paso SINCRONO Pre-operation (stage 20) sobre:
#        sanic_transactionbatches  Update, filtrado en sanic_batchstatus, rank 10
#
# El paso corre DESPUES del guard (Pre-validation, rank 1): cuando estampa, la
# transicion ya es legal (incluida la segregacion de funciones). Escribe quien y
# cuando EN EL TARGET (sin update extra): Ingresado -> sanic_ingresadopor +
# sanic_ingresadoon; Aplicado -> sanic_aplicadopor + sanic_aplicadoon. El actor
# es InitiatingUserId y la hora la del servidor. Sin pre-image: solo necesita el
# estado que trae el request.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-batch-stamp.sh
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

# 1. pluginassembly (mismo assembly del suite; se re-sube con el tipo nuevo)
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.BatchTransitionStampPlugin'", {
    'typename': 'CashHub.Plugins.BatchTransitionStampPlugin',
    'friendlyname': 'CashHub Batch Transition Stamp',
    'name': 'CashHub.Plugins.BatchTransitionStampPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype BatchTransitionStampPlugin', 'plugintypeid')

# 3. paso Pre-operation Update filtrado en el estado
msg_update = one('sdkmessages', "name eq 'Update'", 'sdkmessageid')
flt = one('sdkmessagefilters',
    f"primaryobjecttypecode eq 'sanic_transactionbatches' and _sdkmessageid_value eq {msg_update}",
    'sdkmessagefilterid')

name = "CashHub.BatchTransitionStamp: Update de sanic_transactionbatches"
upsert('sdkmessageprocessingsteps', f"name eq '{name}'", {
    'name': name,
    'description': 'Estampa server-side quien y cuando ejecuta cada transicion del lote en el Target.',
    'plugintypeid@odata.bind': f"/plugintypes({type_id})",
    'sdkmessageid@odata.bind': f"/sdkmessages({msg_update})",
    'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
    'filteringattributes': 'sanic_batchstatus',
    'stage': 20, 'mode': 0, 'rank': 10, 'supporteddeployment': 0,
    'asyncautodelete': False, 'invocationsource': 0,
}, name, 'sdkmessageprocessingstepid')

print("Registro del sellado de transiciones de lote completo.")
PYEOF
