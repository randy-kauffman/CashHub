#!/usr/bin/env bash
# =============================================================================
# Registra la validacion del catalogo de apps del hub (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.GuardAppCatalogPlugin
#   3. paso SINCRONO Pre-validation en Create de sanic_chtblappcatalog
#   4. paso SINCRONO Pre-validation en Update (filtrado en sanic_appurl,sanic_aadgroupid)
#
# El hub lanza sanic_appurl tal cual (Launch): la URL debe apuntar al player de
# Power Apps si o si, y sanic_aadgroupid debe ser GUIDs validos separados por ';'.
# Pre-validation a proposito: aborta antes de abrir la transaccion.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-appcatalog-guard.sh
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

ENTITY = 'sanic_chtblappcatalog'

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

# 1. pluginassembly (mismo assembly compartido; lo re-subimos con el tipo nuevo dentro)
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardAppCatalogPlugin'", {
    'typename': 'CashHub.Plugins.GuardAppCatalogPlugin',
    'friendlyname': 'CashHub Guard App Catalog',
    'name': 'CashHub.Plugins.GuardAppCatalogPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardAppCatalogPlugin', 'plugintypeid')

# ids de mensajes y filtros
msg_update = one('sdkmessages', "name eq 'Update'", 'sdkmessageid')
msg_create = one('sdkmessages', "name eq 'Create'", 'sdkmessageid')
filter_update = one('sdkmessagefilters',
    f"primaryobjecttypecode eq '{ENTITY}' and _sdkmessageid_value eq {msg_update}", 'sdkmessagefilterid')
filter_create = one('sdkmessagefilters',
    f"primaryobjecttypecode eq '{ENTITY}' and _sdkmessageid_value eq {msg_create}", 'sdkmessagefilterid')

# 3. paso Create (sincrono, Pre-validation=10)
upsert('sdkmessageprocessingsteps',
    "name eq 'CashHub.GuardAppCatalog: Create de app del catalogo'", {
    'name': 'CashHub.GuardAppCatalog: Create de app del catalogo',
    'description': 'Valida sanic_appurl (solo player de Power Apps) y sanic_aadgroupid (GUIDs separados por ;).',
    'plugintypeid@odata.bind': f"/plugintypes({type_id})",
    'sdkmessageid@odata.bind': f"/sdkmessages({msg_create})",
    'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({filter_create})",
    'stage': 10, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
    'asyncautodelete': False, 'invocationsource': 0,
}, 'paso Create catalogo', 'sdkmessageprocessingstepid')

# 4. paso Update (sincrono, Pre-validation=10, filtrado en los dos campos validados)
upsert('sdkmessageprocessingsteps',
    "name eq 'CashHub.GuardAppCatalog: Update de app del catalogo'", {
    'name': 'CashHub.GuardAppCatalog: Update de app del catalogo',
    'description': 'Valida sanic_appurl (solo player de Power Apps) y sanic_aadgroupid (GUIDs separados por ;).',
    'plugintypeid@odata.bind': f"/plugintypes({type_id})",
    'sdkmessageid@odata.bind': f"/sdkmessages({msg_update})",
    'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({filter_update})",
    'stage': 10, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
    'filteringattributes': 'sanic_appurl,sanic_aadgroupid',
    'asyncautodelete': False, 'invocationsource': 0,
}, 'paso Update catalogo', 'sdkmessageprocessingstepid')

print("Registro del guard del catalogo completo.")
PYEOF
