#!/usr/bin/env bash
# =============================================================================
# Registra el Custom API que devuelve el CSV de un lote como texto (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.CHGetBatchCsvPlugin
#   3. customapi       sanic_CHGetBatchCsv (global, POST; entrada BatchId Guid
#                      REQUERIDO; respuestas Csv/FileName/SizeBytes)
#
# POR QUE EXISTE. La descarga vieja era Download() sobre .../sanic_csvcore/$value:
# una NAVEGACION del navegador, en la que Power Fx no puede poner un header
# Authorization. Lo unico que la autenticaba era la cookie del org, que los
# usuarios de negocio no tienen —viven en apps.powerapps.com—, asi que les daba
# HTTP 401 y una pestaña en blanco. Y muda: el Download() queda fuera del alcance
# de cualquier IfError, asi que la app ni se enteraba de que fallo.
#
# Este API viaja por el conector (autenticado) y el archivo lo arma el navegador
# con un Blob desde el componente de codigo. Sin cookie, sin navegacion.
#
# NO genera el CSV: lo LEE de la columna File que sube CH-Flow-DownloadData. El
# flujo sigue vivo a proposito hasta que el camino nuevo este probado al 100%.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-get-batch-csv.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CHGetBatchCsvPlugin'", {
    'typename': 'CashHub.Plugins.CHGetBatchCsvPlugin',
    'friendlyname': 'CashHub Get Batch CSV',
    'name': 'CashHub.Plugins.CHGetBatchCsvPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CHGetBatchCsvPlugin', 'plugintypeid')

# 3. customapi. Tipos de parametro: Guid=12, String=10, Integer=7.
#
#    executeprivilegename: el MISMO que las otras APIs de digitacion. Restringe la
#    invocacion a quien puede crear lotes (CH-SR-Digitizer) en vez de dejarla
#    abierta a cualquier usuario autenticado del entorno. Ademas el plugin corre
#    en el contexto de quien llama: si el usuario no puede leer el lote, Dataverse
#    lo frena en el Retrieve. Son dos capas, no una.
api_id = upsert('customapis', "uniquename eq 'sanic_CHGetBatchCsv'", {
    'uniquename': 'sanic_CHGetBatchCsv',
    'name': 'CH Get Batch CSV',
    'displayname': 'CH Get Batch CSV',
    'description': 'Devuelve como texto el CSV ya archivado en la columna File del lote, '
                   'para que la app lo baje sin navegar a Dataverse (mata el 401 de cookie).',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvCreatesanic_TransactionBatches',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHGetBatchCsv', 'customapiid')

upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'BatchId'", {
    'uniquename': 'BatchId', 'name': 'BatchId', 'displayname': 'BatchId',
    'description': 'Id del lote cuyo CSV se devuelve (requerido).',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro BatchId', 'customapirequestparameterid')

for uname, ptype in [('Csv', 10), ('FileName', 10), ('SizeBytes', 7)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro de sanic_CHGetBatchCsv completo.")
PYEOF
