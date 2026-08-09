#!/usr/bin/env bash
# =============================================================================
# Registra la separacion de funciones de los RFC de administracion de clientes
# (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.GuardChangeRequestApprovalPlugin
#   3. paso SINCRONO Pre-operation en Update de sanic_chtblclientdatachangerequest
#      (filtrado en sanic_changestatus) + pre-image (sanic_changestatus, createdby, sanic_targettable)
#   4. paso SINCRONO Pre-operation en Create de sanic_chtblclientdatachangerequest
#
# El paso es sincrono a proposito: un control de seguridad debe abortar la
# transaccion antes de que los flujos de aplicacion tomen el cambio.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-clientadmin-guard.sh
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

ENTITY = 'sanic_chtblclientdatachangerequest'

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

# 1. pluginassembly (mismo assembly que el motor de validacion; lo re-subimos con el tipo nuevo)
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardChangeRequestApprovalPlugin'", {
    'typename': 'CashHub.Plugins.GuardChangeRequestApprovalPlugin',
    'friendlyname': 'CashHub Guard Change Request Approval',
    'name': 'CashHub.Plugins.GuardChangeRequestApprovalPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardChangeRequestApprovalPlugin', 'plugintypeid')

# ids de mensajes y filtros
msg_update = one('sdkmessages', "name eq 'Update'", 'sdkmessageid')
msg_create = one('sdkmessages', "name eq 'Create'", 'sdkmessageid')
filter_update = one('sdkmessagefilters',
    f"primaryobjecttypecode eq '{ENTITY}' and _sdkmessageid_value eq {msg_update}", 'sdkmessagefilterid')
filter_create = one('sdkmessagefilters',
    f"primaryobjecttypecode eq '{ENTITY}' and _sdkmessageid_value eq {msg_create}", 'sdkmessagefilterid')

# 3. paso Update (sincrono, Pre-operation=20, filtrado en sanic_changestatus)
step_update = upsert('sdkmessageprocessingsteps',
    "name eq 'CashHub.GuardChangeRequestApproval: Update de RFC'", {
    'name': 'CashHub.GuardChangeRequestApproval: Update de RFC',
    'description': 'Separacion de funciones: bloquea aprobar/rechazar por el creador o desde estado no-Submitted.',
    'plugintypeid@odata.bind': f"/plugintypes({type_id})",
    'sdkmessageid@odata.bind': f"/sdkmessages({msg_update})",
    'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({filter_update})",
    'stage': 20, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
    'filteringattributes': 'sanic_changestatus',
    'asyncautodelete': False, 'invocationsource': 0,
}, 'paso Update RFC', 'sdkmessageprocessingstepid')

# 3b. pre-image del paso Update
upsert('sdkmessageprocessingstepimages',
    f"_sdkmessageprocessingstepid_value eq {step_update} and entityalias eq 'PreImage'", {
    'name': 'PreImage', 'entityalias': 'PreImage',
    'imagetype': 0, 'messagepropertyname': 'Target',
    'attributes': 'sanic_changestatus,createdby,sanic_targettable',
    'sdkmessageprocessingstepid@odata.bind': f"/sdkmessageprocessingsteps({step_update})",
}, 'pre-image del paso Update', 'sdkmessageprocessingstepimageid')

# 4. paso Create (sincrono, Pre-operation=20)
upsert('sdkmessageprocessingsteps',
    "name eq 'CashHub.GuardChangeRequestApproval: Create de RFC'", {
    'name': 'CashHub.GuardChangeRequestApproval: Create de RFC',
    'description': 'Separacion de funciones: impide crear un RFC ya resuelto (Approved/Rejected/Applied/Failed).',
    'plugintypeid@odata.bind': f"/plugintypes({type_id})",
    'sdkmessageid@odata.bind': f"/sdkmessages({msg_create})",
    'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({filter_create})",
    'stage': 20, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
    'asyncautodelete': False, 'invocationsource': 0,
}, 'paso Create RFC', 'sdkmessageprocessingstepid')

print("Registro del guard completo.")
PYEOF
