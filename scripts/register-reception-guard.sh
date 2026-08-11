#!/usr/bin/env bash
# =============================================================================
# Registra el guard del dominio de recepcion (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.GuardReceptionPlugin
#   3. pasos SINCRONOS Pre-validation (stage 10) sobre:
#        sanic_chtblreceptionbatch   Create / Update(+PreImage estado) / Delete(+PreImage)
#        sanic_chtblreceptions       Create / Update(+PreImage lote)   / Delete(+PreImage)
#        sanic_chtblbags             Create / Update(+PreImage, filtrado en estado+identidad) / Delete(+PreImage)
#        sanic_chtblapprovalrequest  Create / Update(+PreImage estado+creador)
#
# Los pasos son sincronos a proposito: un control de seguridad debe abortar la
# operacion antes de que nada la tome. Las reglas viven en ReceptionGuard
# (CashHub.Validation/Security) con tests en CashHub.Validation.Tests.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-reception-guard.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardReceptionPlugin'", {
    'typename': 'CashHub.Plugins.GuardReceptionPlugin',
    'friendlyname': 'CashHub Guard Reception',
    'name': 'CashHub.Plugins.GuardReceptionPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardReceptionPlugin', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

# (entidad, mensaje, filteringattributes, preimage_attrs, descripcion)
STEPS = [
    ('sanic_chtblreceptionbatch', 'Create', None, None,
     'Un lote solo puede nacer En progreso.'),
    ('sanic_chtblreceptionbatch', 'Update', None, 'sanic_receptionstatus',
     'Transiciones legales del lote; en boveda queda congelado.'),
    ('sanic_chtblreceptionbatch', 'Delete', None, 'sanic_receptionstatus',
     'Solo se elimina un lote En progreso.'),
    ('sanic_chtblreceptions', 'Create', None, None,
     'No se crean planillas en un lote cerrado.'),
    ('sanic_chtblreceptions', 'Update', None, 'sanic_receptionbatch',
     'Planilla congelada con el lote en boveda; reparent solo a lote abierto.'),
    ('sanic_chtblreceptions', 'Delete', None, 'sanic_receptionbatch',
     'No se eliminan planillas de un lote cerrado.'),
    ('sanic_chtblbags', 'Create', None, None,
     'Una tula no puede nacer en estado de recuento.'),
    ('sanic_chtblbags', 'Update', 'sanic_bagstatus,sanic_bagid,sanic_name,sanic_receptionid',
     'sanic_bagstatus,sanic_receptionid',
     'Matriz de transiciones de BagStatus e identidad congelada en boveda.'),
    ('sanic_chtblbags', 'Delete', None, 'sanic_bagstatus,sanic_receptionid',
     'Borrado de tulas solo con lote abierto o tula pendiente de supervisor.'),
    ('sanic_chtblapprovalrequest', 'Create', None, None,
     'Una solicitud no puede nacer resuelta (salvo sesion de recuento de mesa propia).'),
    ('sanic_chtblapprovalrequest', 'Update', None, 'sanic_approvalstatus,createdby',
     'Separacion creador/resolutor y congelamiento post-decision.'),
]

for entity, message, filtering, image_attrs, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.GuardReception: {message} de {entity}"
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

print("Registro del guard de recepcion completo.")
PYEOF
