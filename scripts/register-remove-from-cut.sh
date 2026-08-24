#!/usr/bin/env bash
# =============================================================================
# Registra el Custom API de sacar del lote (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.CHRemoveFromCutPlugin
#   3. customapi       sanic_CHRemoveFromCut (global, POST; entrada TransactionId
#                      Guid REQUERIDO + Motivo String opcional; respuestas
#                      BatchId + TotalNIO/USD/EUR)
#
# Saca una transaccion de su lote y la devuelve a Manual (rechazos del Core /
# revision antes de subir), persiste el motivo y recomputa los totales — atomico.
# Solo se saca de un lote Generado o Ingresado (regla TransactionGuard.CanRemoveFromCut).
#
# SEGURIDAD (fix V-1): a diferencia del corte legacy, esta API nace con
# executeprivilegename = prvCreatesanic_TransactionBatches. Ese privilegio lo tiene
# el rol CH-SR-Digitizer y es el que la operacion necesita (toca lotes/transacciones):
# restringe la invocacion a los digitadores, sin exigir que el llamante sea el cajero
# (el digitador opera a nombre de otros — la autorizacion va por privilegio, no por
# igualdad de identidad).
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-remove-from-cut.sh
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

# 1. pluginassembly (mismo assembly compartido; re-subida completa)
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

# 2. plugintype
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.CHRemoveFromCutPlugin'", {
    'typename': 'CashHub.Plugins.CHRemoveFromCutPlugin',
    'friendlyname': 'CashHub Remove From Cut',
    'name': 'CashHub.Plugins.CHRemoveFromCutPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype CHRemoveFromCutPlugin', 'plugintypeid')

# 3. customapi CON executeprivilegename (fix V-1). Tipos de parametro:
#    Guid=12, String=10, Decimal=2.
api_id = upsert('customapis', "uniquename eq 'sanic_CHRemoveFromCut'", {
    'uniquename': 'sanic_CHRemoveFromCut',
    'name': 'CH Remove From Cut',
    'displayname': 'CH Remove From Cut',
    'description': 'Saca una transaccion de su lote (Generado/Ingresado), la devuelve a '
                   'Manual con el motivo del rechazo del Core y recomputa los totales.',
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    # Gate de ejecucion: solo quien puede crear lotes (los digitadores) invoca la API.
    'executeprivilegename': 'prvCreatesanic_TransactionBatches',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHRemoveFromCut', 'customapiid')

# Entrada: TransactionId (Guid, requerido) + Motivo (String, opcional).
upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'TransactionId'", {
    'uniquename': 'TransactionId', 'name': 'TransactionId', 'displayname': 'TransactionId',
    'description': 'sanic_chtbltransactionsid de la transaccion a desasignar (requerido).',
    'type': 12, 'isoptional': False,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro TransactionId', 'customapirequestparameterid')

upsert('customapirequestparameters',
    f"_customapiid_value eq {api_id} and uniquename eq 'Motivo'", {
    'uniquename': 'Motivo', 'name': 'Motivo', 'displayname': 'Motivo',
    'description': 'Motivo del rechazo del Core; se persiste en sanic_motivorechazocore (opcional).',
    'type': 10, 'isoptional': True,
    'CustomAPIId@odata.bind': f"/customapis({api_id})",
}, 'parametro Motivo', 'customapirequestparameterid')

# Respuestas: BatchId (Guid) + los tres totales recalculados (Decimal).
for uname, ptype in [('BatchId', 12), ('TotalNIO', 2), ('TotalUSD', 2), ('TotalEUR', 2)]:
    upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

print("Registro de sacar del lote completo.")
PYEOF
