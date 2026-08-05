#!/usr/bin/env bash
# =============================================================================
# Registra el motor de validación en el ambiente (idempotente):
#   1. pluginassembly  CashHub.Plugins (contenido base64, sandbox, firmado)
#   2. plugintype      CashHub.Plugins.ValidateReceivedEmailPlugin
#   3. customapi       sanic_CHValidateReceivedEmail (+ parámetro y respuestas)
#   4. sdkmessageprocessingstep asíncrono en Create de sanic_chtblreceivedemails
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-validation-plugin.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.ValidateReceivedEmailPlugin'", {
    'typename': 'CashHub.Plugins.ValidateReceivedEmailPlugin',
    'friendlyname': 'CashHub Validate Received Email',
    'name': 'CashHub.Plugins.ValidateReceivedEmailPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype ValidateReceivedEmailPlugin', 'plugintypeid')

# 3. customapi + parámetro + respuestas
api_id = upsert('customapis', "uniquename eq 'sanic_CHValidateReceivedEmail'", {
    'uniquename': 'sanic_CHValidateReceivedEmail',
    'name': 'CH Validate Received Email',
    'displayname': 'CH Validate Received Email',
    'description': 'Valida un correo recibido con el motor de reglas parametricas.',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHValidateReceivedEmail', 'customapiid')

upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'ReceivedEmailId'", {
    'uniquename': 'ReceivedEmailId', 'name': 'ReceivedEmailId', 'displayname': 'ReceivedEmailId',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro ReceivedEmailId', 'customapirequestparameterid')

for uname, ptype in [('RunId', 12), ('Result', 10), ('FailedFindings', 7)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

# 4. El paso asíncrono en Create fue RETIRADO (fase D): la ingesta invoca el Custom API
# explícitamente tras subir los adjuntos (orden garantizado) y el barrido re-valida
# pendientes. Si el paso existe de una versión anterior, se elimina.
steps = find("/sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid&$filter=" +
    urllib.parse.quote("name eq 'CashHub.ValidateReceivedEmail: Create de correo recibido'"))
for s in steps:
    req('DELETE', f"/sdkmessageprocessingsteps({s['sdkmessageprocessingstepid']})")
    print("  eliminado: paso asincrono Create receivedemails (reemplazado por invocacion explicita)")
if not steps:
    print("  paso asincrono: no existe (correcto)")

print("Registro completo.")
PYEOF
