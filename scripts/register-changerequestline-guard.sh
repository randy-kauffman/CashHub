#!/usr/bin/env bash
# =============================================================================
# Registra el guard de lineas de RFC de administracion de clientes (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.GuardChangeRequestLinePlugin
#   3. pasos SINCRONOS Pre-operation (stage 20) en Create/Update/Delete de
#      sanic_chtblclientdatachangerequestline, con PreImage
#      (sanic_changerequestid,sanic_fielddefinition) en Update y Delete.
#
# Cierra S3 del analisis (docs/clientadmin-rebuild/00-analisis-y-plan.md): no
# habia guard sobre las lineas del RFC — crear o borrar lineas de un RFC ajeno
# en Draft por Web API era posible para cualquier Approver con Write global.
# Reglas (dominio puro ChangeRequestLineGuard, tests en CashHub.Validation.Tests):
# el RFC padre debe estar en Draft, sin lineas duplicadas por (RFC,
# FieldDefinition), y solo el creador del RFC o un aprobador de la tabla
# objetivo puede tocar sus lineas. Mismo stage (20, Pre-operation) que
# GuardChangeRequestApprovalPlugin, la contraparte de cabecera.
#
# Este guard NO se auto-bloquea: cuando sanic_CHResolveChangeRequest
# (ResolveChangeRequestPlugin) marca las lineas Applied/Denied con el RFC ya en
# Submitted, esa escritura corre a profundidad de pipeline > 1 y el guard la
# deja pasar (ChangeRequestLineGuard.IsWriteExempt) — ver register-resolve-
# changerequest.sh para la Custom API que dispara esa escritura.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-changerequestline-guard.sh
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

ENTITY = 'sanic_chtblclientdatachangerequestline'
IMAGE_ATTRS = 'sanic_changerequestid,sanic_fielddefinition'

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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardChangeRequestLinePlugin'", {
    'typename': 'CashHub.Plugins.GuardChangeRequestLinePlugin',
    'friendlyname': 'CashHub Guard Change Request Line',
    'name': 'CashHub.Plugins.GuardChangeRequestLinePlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardChangeRequestLinePlugin', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

# (mensaje, preimage_attrs, descripcion)
STEPS = [
    ('Create', None,
     'El RFC padre debe estar en Draft; el creador del RFC (o un aprobador) y sin '
     'lineas duplicadas por (RFC, FieldDefinition).'),
    ('Update', IMAGE_ATTRS,
     'Mismas reglas que Create; el chequeo de duplicado solo corre si cambia el campo.'),
    ('Delete', IMAGE_ATTRS,
     'Solo se borran lineas de un RFC en Draft, por su creador o un aprobador.'),
]

for message, image_attrs, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{ENTITY}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.GuardChangeRequestLine: {message} de linea"
    body = {
        'name': name,
        'description': description,
        'plugintypeid@odata.bind': f"/plugintypes({type_id})",
        'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
        'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
        'stage': 20, 'mode': 0, 'rank': 1, 'supporteddeployment': 0,
        'asyncautodelete': False, 'invocationsource': 0,
    }
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

print("Registro del guard de lineas de RFC completo.")
PYEOF
