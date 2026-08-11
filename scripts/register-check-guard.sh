#!/usr/bin/env bash
# =============================================================================
# Registra el guard del handshake de recepcion de cheques (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con CheckGuard dentro)
#   2. plugintype      CashHub.Plugins.GuardTransactionPlugin (YA EXISTE: upsert)
#   3. pasos SINCRONOS Pre-validation (stage 10) sobre:
#        sanic_chtblchecks  Create (sin filtro) / Update (+PreImage estado)
#
# El paso de Update NO lleva filteringattributes A PROPOSITO: Recibido congela
# el registro completo, asi que el guard tiene que ver CUALQUIER edicion del
# cheque, no solo las que tocan sanic_checkstatus. Las reglas viven en
# CheckGuard (CashHub.Validation/Security) con tests en CashHub.Validation.Tests.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-check-guard.sh
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

# 1. pluginassembly (mismo assembly del suite; se re-sube con CheckGuard dentro)
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype (ya registrado por register-transaction-guard.sh: upsert)
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardTransactionPlugin'", {
    'typename': 'CashHub.Plugins.GuardTransactionPlugin',
    'friendlyname': 'CashHub Guard Transaction',
    'name': 'CashHub.Plugins.GuardTransactionPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardTransactionPlugin', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update')}

# (entidad, mensaje, filteringattributes, preimage_attrs, descripcion)
STEPS = [
    ('sanic_chtblchecks', 'Create', None, None,
     'Un cheque solo puede nacer sin estado o Recontado.'),
    # SIN filteringattributes: el congelamiento de Recibido aplica a cualquier edicion.
    ('sanic_chtblchecks', 'Update', None, 'sanic_checkstatus',
     'Handshake Recontado -> Enviado a digitacion -> Recibido/No Recibido; Recibido congela; Digitado/Rebotado reservados.'),
]

for entity, message, filtering, image_attrs, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.GuardTransaction: {message} de {entity}"
    body = {
        'name': name,
        'description': description,
        'plugintypeid@odata.bind': f"/plugintypes({type_id})",
        'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
        'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
        'stage': 10, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
        'asyncautodelete': False, 'invocationsource': 0,
    }
    if filtering:
        body['filteringattributes'] = filtering
    step_id = upsert('sdkmessageprocessingsteps', f"name eq '{name}'", body, name,
                     'sdkmessageprocessingstepid')
    if image_attrs:
        upsert('sdkmessageprocessingstepimages',
            f"_sdkmessageprocessingstepid_value eq {step_id} and entityalias eq 'PreImage'", {
            'name': 'PreImage', 'entityalias': 'PreImage',
            'imagetype': 0, 'messagepropertyname': 'Target',
            'attributes': image_attrs,
            'sdkmessageprocessingstepid@odata.bind': f"/sdkmessageprocessingsteps({step_id})",
        }, f"pre-image de {name}", 'sdkmessageprocessingstepimageid')

print("Registro del guard de cheques completo.")
PYEOF
