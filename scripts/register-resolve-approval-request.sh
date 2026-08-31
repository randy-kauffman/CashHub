#!/usr/bin/env bash
# =============================================================================
# Registra la Custom API sanic_CHResolveApprovalRequest (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.ResolveApprovalRequestPlugin
#   3. customapi       sanic_CHResolveApprovalRequest (global, POST; entradas
#                      ApprovalRequestId Guid REQUERIDO, Decision String
#                      REQUERIDO, Reason String opcional; respuestas Outcome y
#                      Message, ambas String)
#   4. alta de los 6 componentes en CashHubCore + PublishAllXml
#
# POR QUE EXISTE. Resolver una solicitud eran DOS escrituras sueltas desde la
# app del supervisor: marcar la solicitud (Aprobada/Rechazada) y aplicar su
# efecto sobre tulas/planillas/sesiones. Dos escrituras que no son una unidad:
# si la segunda fallaba — y fallaba, porque los guards de RecountingGuard viven
# en esas otras tablas — la solicitud quedaba marcada como resuelta y el efecto
# sin ejecutar. La aprobacion de AR-00001023 se rechazo y la tula quedo en
# "Asignada a cajero": una decision apuntando a la nada.
#
# Invertir el orden en la app no alcanzaba: la verificacion de "sos del equipo
# aprobador" solo existe en el Update de la solicitud, no en bags ni sessions.
# Aplicar el efecto primero dejaria que un NO-aprobador destruyera datos y
# recien despues chocara contra el bloqueo.
#
# La solucion es esta CAPI: autoriza, y solo si autoriza aplica efecto + marca
# la solicitud EN LA MISMA EJECUCION. Si algo falla, la plataforma revierte
# todo. No hay medio estado posible.
#
# El veredicto negativo viaja como DATO (Outcome=
# Blocked + Message, HTTP 200) y nunca como fault: Power Fx descarta el mensaje
# custom de los faults de plugin y muestra la descripcion generica de
# 0x80040265 (probados FirstError.Message y AllErrors, ninguno lo recupera).
#
# FASE 1: los 8 tipos de efecto acotado. Los dos que generan asientos contables
# ("Tula con diferencia" y "Tula recontada sin declaracion") todavia los
# resuelve la app; la CAPI los rechaza explicitamente para que nadie crea que
# ya estan cubiertos.
#
# executeprivilegename: prvWritesanic_CHTblApprovalRequest. Sin el, cualquier
# usuario del entorno podria invocar la resolucion por Web API.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-resolve-approval-request.sh
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

# 1. pluginassembly — re-subirlo es lo que hace visible al tipo nuevo.
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.ResolveApprovalRequestPlugin'", {
    'typename': 'CashHub.Plugins.ResolveApprovalRequestPlugin',
    'friendlyname': 'CashHub Resolve Approval Request',
    'name': 'CashHub.Plugins.ResolveApprovalRequestPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype ResolveApprovalRequestPlugin', 'plugintypeid')

# 3. customapi + parametros. Tipos de customapi(request|response) properties:
#    Boolean=0, String=10, Guid=12 (mismos codigos que el resto del repo).
api_id = upsert('customapis', "uniquename eq 'sanic_CHResolveApprovalRequest'", {
    'uniquename': 'sanic_CHResolveApprovalRequest',
    'name': 'CH Resolve Approval Request',
    'displayname': 'CH Resolve Approval Request',
    # OJO: customapi.description tope 300 caracteres. El racional completo esta
    # en la cabecera de este script y en ResolveApprovalRequestPlugin.
    'description': (
        'Resuelve una solicitud de aprobacion y aplica su efecto como UNA transaccion: '
        'autoriza, y solo si autoriza escribe. Antes eran dos escrituras sueltas desde '
        'la app y la solicitud podia quedar resuelta con el efecto sin ejecutar. '
        'Devuelve Outcome + Message como DATO (HTTP 200), nunca como fault.'
    ),
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvWritesanic_CHTblApprovalRequest',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHResolveApprovalRequest', 'customapiid')

REQUEST_PARAMS = [
    ('ApprovalRequestId',
     'sanic_chtblapprovalrequestid de la solicitud a resolver (requerido).', 12, False),
    ('Decision',
     "'Approve' o 'Reject'. Cualquier otro valor se rechaza sin escribir nada.", 10, False),
    ('Reason',
     'Motivo de la resolucion. Obligatorio de hecho al rechazar: un rechazo sin '
     'motivo se bloquea.', 10, True),
]

for uname, label, ptype, optional in REQUEST_PARAMS:
    upsert('customapirequestparameters',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname,
        'description': label, 'type': ptype, 'isoptional': optional,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'parametro {uname}', 'customapirequestparameterid')

# OJO: Outcome es String, NO Boolean. Con un parametro de respuesta Boolean el
# conector Environment NO expone la API a las canvas apps (Power Fx: "unknown o
# unsupported function") aunque este en la solucion y se refresque el datasource.
# Ninguna de las Custom API del repo que funcionan usa Boolean.
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
#    se refresque el datasource.
#    componenttype: CustomAPI=10036, RequestParameter=10037, ResponseProperty=10038.
req_ids = {}
for uname, _, _, _ in REQUEST_PARAMS:
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

# 5. Publicar. Sin esto el WADL del conector Environment sigue sirviendo la
#    version anterior y la app no ve la API por mas que ya este en la solucion.
req('POST', '/PublishAllXml', {})
print("  publicado (PublishAllXml)")

print("Registro de sanic_CHResolveApprovalRequest completo.")
PYEOF
