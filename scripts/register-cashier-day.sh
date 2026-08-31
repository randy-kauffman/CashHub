#!/usr/bin/env bash
# =============================================================================
# Registra el Custom API del cubo de un cajero en un dia (idempotente):
#   customapi sanic_CHCashierDay — entradas CajeroId (Guid), Desde/Hasta (DateTime);
#   respuestas Cube (JSON) y TotalCount (Integer).
#
# Devuelve UN CUBO, no un total: una fila por moneda x tipo x via, con efectivo,
# cheque BAC, cheque otros, total y conteo. Son a lo sumo 3 x 4 x 2 = 24 filas, y
# con eso la app arma el resumen grande (que sigue a los filtros) y los contadores
# de cada chip sin volver a consultar al tocar uno.
#
# Agregado server-side porque un dia de un cajero real son 5.442 transacciones:
# sumarlas en el cliente toparia en 500 y daria un resumen incompleto SIN avisar.
# El detalle no pasa por aca — la galeria se ata al datasource con un Filter
# delegable y muestra TODAS las del dia.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-cashier-day.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CHCashierDayPlugin'", {
    'typename': 'CashHub.Plugins.CHCashierDayPlugin',
    'friendlyname': 'CashHub Cashier Day',
    'name': 'CashHub.Plugins.CHCashierDayPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CHCashierDayPlugin', 'plugintypeid')

# 3. customapi. Tipos de parametro: Guid=12, String=10, Integer=7.
#
#    executeprivilegename: el MISMO que las otras APIs de digitacion. Restringe la
#    invocacion a quien puede crear lotes (CH-SR-Digitizer) en vez de dejarla
#    abierta a cualquier usuario autenticado del entorno. Ademas el plugin corre
#    en el contexto de quien llama: si el usuario no puede leer el lote, Dataverse
#    lo frena en el Retrieve. Son dos capas, no una.
api_id = upsert('customapis', "uniquename eq 'sanic_CHCashierDay'", {
    'uniquename': 'sanic_CHCashierDay',
    'name': 'CH Cashier Day',
    'displayname': 'CH Cashier Day',
    'description': 'Cubo de un cajero en un dia: una fila por moneda x tipo x via con efectivo, '
                   'cheque BAC, cheque otros, total y conteo. La app arma el resumen y los '
                   'contadores de los filtros sobre ese cubo, sin re-consultar.',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvCreatesanic_TransactionBatches',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHCashierDay', 'customapiid')

upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'CajeroId'", {
    'uniquename': 'CajeroId', 'name': 'CajeroId', 'displayname': 'CajeroId',
    'description': 'systemuserid del cajero (requerido).',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro CajeroId', 'customapirequestparameterid')

# DateTime = 1. La app manda los limites ya resueltos (medianoche local y la del dia
# siguiente): la conversion de zona la hace el cliente, que es el que sabe en que
# huso esta el operador. El plugin solo los pasa a UTC para el FetchXML.
for uname in ('Desde', 'Hasta'):
    upsert('customapirequestparameters',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname,
        'description': f'{uname} del dia a consultar (requerido).',
        'type': 1, 'isoptional': False,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'parametro {uname}', 'customapirequestparameterid')

for uname, ptype in [('Cube', 10), ('TotalCount', 7), ('Days', 10)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro de sanic_CHCashierDay completo.")
PYEOF
