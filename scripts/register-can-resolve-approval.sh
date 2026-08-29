#!/usr/bin/env bash
# =============================================================================
# Registra la Custom API sanic_CHCanResolveApproval (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.CanResolveApprovalPlugin
#   3. customapi       sanic_CHCanResolveApproval (global, POST; entrada
#                      ApprovalRequestId Guid REQUERIDO; respuestas
#                      Allowed Boolean y Message String)
#
# POR QUE EXISTE. Cuando GuardReceptionPlugin bloquea la resolucion de una
# solicitud, Dataverse devuelve el motivo correcto en el cuerpo HTTP (code
# 0x80040265 + message + @Microsoft.PowerApps.CDS.InnerError.Message) — se ve
# en el Monitor de Studio. Pero Power Fx arma FirstError con la descripcion
# GENERICA del codigo y descarta el mensaje custom: la app del supervisor
# terminaba mostrando "Network error ... The requested operation is invalid".
# Ni FirstError.Message ni AllErrors lo recuperan (ambos probados).
#
# Esta API es un PRE-VUELO de solo lectura: devuelve el veredicto como DATO
# (HTTP 200), nunca como fault, para que el canal de error de Power Fx no
# participe. NO resuelve ni escribe nada — la autoridad sigue siendo
# GuardReceptionPlugin sobre el Update. Ambos aplican la MISMA regla de dominio
# (ReceptionGuard.EvaluateApprovalUpdate), asi que no pueden divergir.
#
# executeprivilegename: prvWritesanic_CHTblApprovalRequest. Sin el, cualquier
# usuario del entorno podria sondear por Web API quien es aprobador. Con el,
# solo quien de verdad podria intentar resolver puede preguntar.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-can-resolve-approval.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CanResolveApprovalPlugin'", {
    'typename': 'CashHub.Plugins.CanResolveApprovalPlugin',
    'friendlyname': 'CashHub Can Resolve Approval',
    'name': 'CashHub.Plugins.CanResolveApprovalPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CanResolveApprovalPlugin', 'plugintypeid')

# 3. customapi + parametros. Tipos de customapi(request|response) properties:
#    Boolean=0, String=10, Guid=12 (mismos codigos que el resto del repo).
api_id = upsert('customapis', "uniquename eq 'sanic_CHCanResolveApproval'", {
    'uniquename': 'sanic_CHCanResolveApproval',
    'name': 'CH Can Resolve Approval',
    'displayname': 'CH Can Resolve Approval',
    # OJO: customapi.description tope 300 caracteres. El racional completo esta en
    # la cabecera de este script y en Schema.CanResolveApprovalApi.
    'description': (
        'Pre-vuelo de solo lectura: dice si quien llama puede resolver una solicitud '
        'de aprobacion, y por que no. Devuelve Allowed + Message como DATO (HTTP 200), '
        'nunca como fault: Power Fx descarta el mensaje de los faults de plugin. '
        'No escribe; la autoridad sigue siendo GuardReceptionPlugin.'
    ),
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvWritesanic_CHTblApprovalRequest',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHCanResolveApproval', 'customapiid')

for uname, label, ptype, optional in [
    ('ApprovalRequestId',
     'sanic_chtblapprovalrequestid de la solicitud a evaluar (requerido).', 12, False),
]:
    upsert('customapirequestparameters',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname,
        'description': label, 'type': ptype, 'isoptional': optional,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'parametro {uname}', 'customapirequestparameterid')

# OJO: Outcome es String, NO Boolean. Con un parametro de respuesta Boolean el
# conector Environment NO expone la API a las canvas apps (Power Fx: "unknown or
# unsupported function") aunque este en la solucion y se refresque el datasource.
# Ninguna de las Custom API del repo que funcionan usa Boolean. Se calca la forma
# de sanic_CHResolveChangeRequest: Outcome + Message, ambos String.
resp_ids = {}
for uname, ptype in [('Outcome', 10), ('Message', 10)]:
    resp_ids[uname] = upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

# 4. Alta en CashHubCore. IMPRESCINDIBLE, no es cosmetico: el conector Environment
#    de las canvas apps SOLO expone las Custom API que pertenecen a una solucion.
#    Quedandose en Default, la API funciona por Web API pero NO aparece en el WADL
#    (pkgs/Wadl/Environment.xml) y la app da "el nombre no es valido" por mas que
#    se refresque el datasource. Verificado comparando contra las 6 que si andan.
#    componenttype: CustomAPI=10036, RequestParameter=10037, ResponseProperty=10038.
req_ids = {}
for uname in ['ApprovalRequestId']:
    flt = "_customapiid_value eq {0} and uniquename eq '{1}'".format(api_id, uname)
    rows = find('/customapirequestparameters?$select=customapirequestparameterid'
                '&$filter=' + urllib.parse.quote(flt))
    req_ids[uname] = rows[0]['customapirequestparameterid']

for cid, ctype, label in (
    [(api_id, 10036, 'customapi')]
    + [(v, 10037, f'parametro {k}') for k, v in req_ids.items()]
    + [(v, 10038, f'respuesta {k}') for k, v in resp_ids.items()]
):
    req('POST', '/AddSolutionComponent', {
        'ComponentId': cid,
        'ComponentType': ctype,
        'SolutionUniqueName': 'CashHubCore',
        'AddRequiredComponents': False,
        'DoNotIncludeSubcomponents': False,
    })
    print(f"  agregado a CashHubCore: {label}")

print("Registro de sanic_CHCanResolveApproval completo.")
PYEOF
