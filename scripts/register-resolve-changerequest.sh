#!/usr/bin/env bash
# =============================================================================
# Registra la Custom API sanic_CHResolveChangeRequest (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.ResolveChangeRequestPlugin
#   3. customapi       sanic_CHResolveChangeRequest (global, POST; parametros de
#                      entrada ChangeRequestId Guid REQUERIDO, Decision String
#                      REQUERIDO ('Approve'|'Reject'), Comment String opcional;
#                      respuestas Outcome/Message)
#
# Reemplaza los 4 flujos materializadores de RFC (Applyclientdatachanges /
# Createclientdata / Activaterow / Deactivaterow — docs/clientadmin-rebuild/
# 00-analisis-y-plan.md §3.1). Resuelve Approve/Reject de forma SINCRONA y
# ATOMICA: valida separacion de funciones + membresia Approver (Fase 2), aplica
# el cambio al maestro (Update/Create/Activate/Deactivate segun sanic_type,
# tabla resuelta DINAMICAMENTE desde el RFC), marca lineas + bitacora + cabecera
# — todo en la misma transaccion del pipeline. Un fallo revierte TODO.
#
# executeprivilegename: prvWritesanic_CHTblClientDataChangeRequest (Global). Es
# el privilegio que YA tiene CH-SR-ClientAdminApprover (y NINGUN otro rol
# ClientAdmin) sobre la cabecera del RFC — exactamente quien deberia poder
# resolver un RFC. Sin este privilegio, cualquier usuario del entorno podria
# invocar la Custom API por Web API (mismo hueco que V-1 del corte de
# transacciones, ya corregido en register-transaction-cut.sh).
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-resolve-changerequest.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.ResolveChangeRequestPlugin'", {
    'typename': 'CashHub.Plugins.ResolveChangeRequestPlugin',
    'friendlyname': 'CashHub Resolve Change Request',
    'name': 'CashHub.Plugins.ResolveChangeRequestPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype ResolveChangeRequestPlugin', 'plugintypeid')

# 3. customapi + parametros. Tipos segun la tabla de customapi(request|response)
#    properties: Guid=12, String=10 (mismos codigos que el resto de las Custom
#    API del repo, ver register-transaction-cut.sh / register-remove-from-cut.sh).
api_id = upsert('customapis', "uniquename eq 'sanic_CHResolveChangeRequest'", {
    'uniquename': 'sanic_CHResolveChangeRequest',
    'name': 'CH Resolve Change Request',
    'displayname': 'CH Resolve Change Request',
    'description': (
        'Resuelve (Approve/Reject) un RFC de administracion de clientes: valida '
        'separacion de funciones + membresia Approver, materializa el cambio sobre '
        'el maestro segun sanic_type, y marca lineas/bitacora/cabecera — todo '
        'atomico. Reemplaza los 4 flujos materializadores de Power Automate.'
    ),
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    # SEGURIDAD: sin executeprivilegename cualquier usuario del entorno podria
    # invocar la resolucion por Web API. Este privilegio (Global) solo lo tiene
    # CH-SR-ClientAdminApprover — exactamente quien puede aprobar/rechazar un RFC.
    'executeprivilegename': 'prvWritesanic_CHTblClientDataChangeRequest',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHResolveChangeRequest', 'customapiid')

for uname, label, ptype, optional in [
    ('ChangeRequestId', 'sanic_chtblclientdatachangerequestid del RFC a resolver (requerido).', 12, False),
    ('Decision', "'Approve' o 'Reject' (requerido).", 10, False),
    ('Comment', 'Comentario del resolutor -> sanic_applynotes (opcional).', 10, True),
]:
    upsert('customapirequestparameters',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname,
        'description': label, 'type': ptype, 'isoptional': optional,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'parametro {uname}', 'customapirequestparameterid')

for uname, ptype in [('Outcome', 10), ('Message', 10)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro de sanic_CHResolveChangeRequest completo.")
PYEOF
