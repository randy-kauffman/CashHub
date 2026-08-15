#!/usr/bin/env bash
# =============================================================================
# Registra los guards del dominio de digitalizacion y de la contabilidad de
# aprobaciones (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con los tipos nuevos dentro)
#   2. plugintypes     CashHub.Plugins.GuardTransactionPlugin          (maquina de estados)
#                      CashHub.Plugins.GuardSupervisorAccountingPlugin (contabilidad, B-5)
#   3. pasos SINCRONOS Pre-validation (stage 10) sobre:
#        sanic_chtbltransactions   Create rank 1 (maquina de estados)
#        sanic_chtbltransactions   Create rank 2 (contabilidad de asientos)
#        sanic_chtbltransactions   Update(+PreImage lote+status+dinero, SIN filtrado)
#        sanic_transactionbatches  Create / Update(+PreImage estado+ingresadopor, filtrado en estado) / Delete
#
# La pre-image del Update del lote trae ademas sanic_ingresadopor: es el insumo
# de la regla de segregacion de funciones (quien marco Ingresado no puede marcar
# Aplicado). El guard corre Pre-validation (10), ANTES del stamp (Pre-op 20), asi
# que la imagen refleja la transicion ANTERIOR — exactamente lo que la regla pide.
#
# Los pasos son sincronos a proposito: un control de seguridad debe abortar la
# operacion antes de que nada la tome. Las reglas viven en el dominio puro
# (TransactionGuard / AccountingGuard en CashHub.Validation/Security) con tests
# en CashHub.Validation.Tests.
#
# H-04 — POR QUE EL UPDATE DE TRANSACCIONES YA NO FILTRA: filtraba por
# sanic_transactionbatch,sanic_transactionstatus, asi que un request que SOLO
# reescribia montos (o la cuenta) de una transaccion ya aplicada en el Core ni
# siquiera despertaba al guard. Sin filtro, TODO update la despierta y las reglas
# de salida temprana ordenan el trafico: touchesOnlyMirror deja pasar la cascada
# del espejo sanic_batchstatus sin resolver lotes, y touchesAmounts congela
# montos/cuenta cuando la transaccion esta Aplicada o su lote ya entro al Core.
# En el lote la historia sigue igual: el Update filtra sanic_batchstatus y la
# regla touchesOnlyTotals cubre las escrituras de BatchTotalsCalculator.
#
# B-5 — dos plugin types sobre el MISMO Create: la maquina de estados (rank 1) no
# consulta depositos y la contabilidad (rank 2) no consulta lotes. Separados, cada
# uno falla con su mensaje de negocio y se prueba solo. El paso de contabilidad
# necesita ademas la alternate key sanic_KeyAsientoPorRecuento
# (scripts/create-alternate-keys.sh) para cerrar la carrera entre dos supervisores
# simultaneos, que ninguna consulta previa al Create puede cerrar sola.
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

# 2. plugintypes (dos: maquina de estados y contabilidad de asientos)
TYPES = [
    ('CashHub.Plugins.GuardTransactionPlugin', 'CashHub Guard Transaction', 'GuardTransaction'),
    ('CashHub.Plugins.GuardSupervisorAccountingPlugin', 'CashHub Guard Supervisor Accounting',
     'GuardSupervisorAccounting'),
]
type_ids = {}
for typename, friendly, short in TYPES:
    type_ids[short] = upsert('plugintypes', f"typename eq '{typename}'", {
        'typename': typename,
        'friendlyname': friendly,
        'name': typename,
        'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
    }, f'plugintype {typename.rsplit(".", 1)[-1]}', 'plugintypeid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

# (plugin, entidad, mensaje, rank, filteringattributes, preimage_attrs, descripcion)
#
# filteringattributes None = el paso corre en TODO update de la entidad. Es
# deliberado en sanic_chtbltransactions (H-04): con filtro, un update que solo
# tocaba montos no despertaba al guard. El costo lo absorben las salidas
# tempranas del dominio (touchesOnlyMirror sale sin una sola consulta).
STEPS = [
    ('GuardTransaction', 'sanic_chtbltransactions', 'Create', 1, None, None,
     'Una transaccion con lote preseteado solo entra a un lote Generado y sin nacer Aplicada.'),
    ('GuardSupervisorAccounting', 'sanic_chtbltransactions', 'Create', 2, None, None,
     'Contabilidad del circuito de aprobacion (B-5 / H-02): Sobrante, Faltante y '
     "'Sin declaracion' se recalculan server-side por componente contra el deposito "
     'recontado y su declarado; un solo asiento por (recuento, tipo).'),
    ('GuardTransaction', 'sanic_chtbltransactions', 'Update', 1, None,
     'sanic_transactionbatch,sanic_transactionstatus,sanic_cashamount,'
     'sanic_baclocalcheckamount,sanic_otherlocalcheckamount,sanic_accountnumber',
     'Aplicada es terminal; (des)asignacion de lote solo dentro del ciclo Generado/Ingresado; '
     'montos y cuenta congelados con la transaccion Aplicada o su lote ya cargado al Core (H-04).'),
    ('GuardTransaction', 'sanic_transactionbatches', 'Create', 1, None, None,
     'Un lote solo puede nacer Generado explicito.'),
    ('GuardTransaction', 'sanic_transactionbatches', 'Update', 1, 'sanic_batchstatus',
     'sanic_batchstatus,sanic_ingresadopor',
     'Transiciones solo hacia adelante Generado -> Ingresado -> Aplicado; Aplicado inmutable; '
     'quien marco Ingresado no marca Aplicado (segregacion de funciones).'),
    ('GuardTransaction', 'sanic_transactionbatches', 'Delete', 1, None, None,
     'Solo se elimina un lote Generado y sin transacciones (rollback del corte vacio).'),
]

for plugin, entity, message, rank, filtering, image_attrs, description in STEPS:
    flt = one('sdkmessagefilters',
        f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}", 'sdkmessagefilterid')
    name = f"CashHub.{plugin}: {message} de {entity}"
    body = {
        'name': name,
        'description': description,
        'plugintypeid@odata.bind': f"/plugintypes({type_ids[plugin]})",
        'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
        'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
        'stage': 10, 'mode': 0, 'rank': rank, 'supporteddeployment': 0,
        'asyncautodelete': False, 'invocationsource': 0,
    }
    # SIEMPRE presente, incluso en None: el PATCH tiene que BORRAR el filtro que el
    # paso de Update de transacciones ya tenia registrado (H-04). Omitir la clave
    # dejaria el filtro viejo intacto y el guard seguiria ciego a los montos.
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
