#!/usr/bin/env bash
# =============================================================================
# Registra el guard del dominio de digitalizacion (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con el tipo nuevo dentro)
#   2. plugintype      CashHub.Plugins.GuardTransactionPlugin
#   3. pasos SINCRONOS Pre-validation (stage 10) sobre:
#        sanic_chtbltransactions   Create / Update(+PreImage lote+status, filtrado en lote+status)
#        sanic_transactionbatches  Create / Update(+PreImage estado+ingresadopor, filtrado en estado) / Delete
#
# La pre-image del Update del lote trae ademas sanic_ingresadopor: es el insumo
# de la regla de segregacion de funciones (quien marco Ingresado no puede marcar
# Aplicado). El guard corre Pre-validation (10), ANTES del stamp (Pre-op 20), asi
# que la imagen refleja la transicion ANTERIOR — exactamente lo que la regla pide.
#
# Los pasos son sincronos a proposito: un control de seguridad debe abortar la
# operacion antes de que nada la tome. Las reglas viven en TransactionGuard
# (CashHub.Validation/Security) con tests en CashHub.Validation.Tests.
#
# OJO — bypass del espejo: el paso de Update de transacciones NO filtra por
# sanic_batchstatus, asi que la cascada del mirror plugin ni siquiera dispara el
# guard; la regla touchesOnlyMirror cubre el resto. Idem los totales del lote:
# el Update del lote filtra solo sanic_batchstatus y la regla touchesOnlyTotals
# cubre las escrituras de BatchTotalsCalculator.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-transaction-guard.sh
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
type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.GuardTransactionPlugin'", {
    'typename': 'CashHub.Plugins.GuardTransactionPlugin',
    'friendlyname': 'CashHub Guard Transaction',
    'name': 'CashHub.Plugins.GuardTransactionPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype GuardTransactionPlugin', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

# (entidad, mensaje, filteringattributes, preimage_attrs, descripcion)
STEPS = [
    ('sanic_chtbltransactions', 'Create', None, None,
     'Una transaccion con lote preseteado solo entra a un lote Generado y sin nacer Aplicada.'),
    ('sanic_chtbltransactions', 'Update', 'sanic_transactionbatch,sanic_transactionstatus',
     'sanic_transactionbatch,sanic_transactionstatus',
     'Aplicada es terminal; (des)asignacion de lote solo dentro del ciclo Generado/Ingresado.'),
    ('sanic_transactionbatches', 'Create', None, None,
     'Un lote solo puede nacer Generado explicito.'),
    ('sanic_transactionbatches', 'Update', 'sanic_batchstatus', 'sanic_batchstatus,sanic_ingresadopor',
     'Transiciones solo hacia adelante Generado -> Ingresado -> Aplicado; Aplicado inmutable; '
     'quien marco Ingresado no marca Aplicado (segregacion de funciones).'),
    ('sanic_transactionbatches', 'Delete', None, None,
     'Solo se elimina un lote Generado y sin transacciones (rollback del corte vacio).'),
]

for entity, message, filtering, image_attrs, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.GuardTransaction: {message} de {entity}"
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

print("Registro del guard de transacciones completo.")
PYEOF
