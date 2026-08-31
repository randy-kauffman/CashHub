#!/usr/bin/env bash
# =============================================================================
# Registra el Custom API del resumen por cuenta del lote (idempotente):
#   1. pluginassembly  CashHub.Plugins
#   2. plugintype      CashHub.Plugins.CHBatchSummaryPlugin
#   3. customapi       sanic_CHBatchSummary (global, POST; entrada BatchId Guid
#                      REQUERIDO + PastedSummary String OPCIONAL; respuestas
#                      Summary/Comparison/Outcome/Message)
#
# POR QUE EXISTE. El aprobador del IBS Branch NO ve el detalle transaccion por
# transaccion: ve un "Resumen por Cuenta" (Cuenta | Moneda | Monto | Cant TRN) y
# decide con eso. Comparar a mano contra el lote es lento y propenso a errores —el
# lote mas grande del entorno tiene 600 cuentas distintas— y el error ahi se paga
# aprobando o devolviendo la carga equivocada.
#
# Con PastedSummary vacio devuelve solo el resumen (para pintar la vista). Con el
# texto pegado del IBS, ademas lo parsea y compara cuenta por cuenta.
#
# El resumen se arma de las MISMAS filas que generan el CSV: si saliera por otro
# camino podria cuadrar contra el IBS mientras el archivo enviado dice otra cosa.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-batch-summary.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CHBatchSummaryPlugin'", {
    'typename': 'CashHub.Plugins.CHBatchSummaryPlugin',
    'friendlyname': 'CashHub Batch Summary',
    'name': 'CashHub.Plugins.CHBatchSummaryPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CHBatchSummaryPlugin', 'plugintypeid')

# 3. customapi. Tipos de parametro: Guid=12, String=10, Integer=7.
#
#    executeprivilegename: el MISMO que las otras APIs de digitacion. Restringe la
#    invocacion a quien puede crear lotes (CH-SR-Digitizer) en vez de dejarla
#    abierta a cualquier usuario autenticado del entorno. Ademas el plugin corre
#    en el contexto de quien llama: si el usuario no puede leer el lote, Dataverse
#    lo frena en el Retrieve. Son dos capas, no una.
api_id = upsert('customapis', "uniquename eq 'sanic_CHBatchSummary'", {
    'uniquename': 'sanic_CHBatchSummary',
    'name': 'CH Batch Summary',
    'displayname': 'CH Batch Summary',
    'description': 'Resumen por cuenta y moneda del lote (lo mismo que el aprobador ve en el IBS) '
                   'y, si se le pega el resumen del IBS, la comparacion cuenta por cuenta.',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvCreatesanic_TransactionBatches',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHBatchSummary', 'customapiid')

upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'BatchId'", {
    'uniquename': 'BatchId', 'name': 'BatchId', 'displayname': 'BatchId',
    'description': 'Id del lote del que se arma el resumen (requerido).',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro BatchId', 'customapirequestparameterid')

# OPCIONAL a proposito: sin texto pegado el API sirve para pintar la vista resumen;
# con texto, ademas compara. Una sola llamada, una sola fuente de verdad.
upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'PastedSummary'", {
    'uniquename': 'PastedSummary', 'name': 'PastedSummary', 'displayname': 'PastedSummary',
    'description': 'Texto del Resumen por Cuenta copiado del IBS Branch (opcional).',
    'type': 10, 'isoptional': True,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro PastedSummary', 'customapirequestparameterid')

for uname, ptype in [('Summary', 10), ('Comparison', 10), ('Outcome', 10), ('Message', 10)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro de sanic_CHBatchSummary completo.")
PYEOF
