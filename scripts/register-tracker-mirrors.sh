#!/usr/bin/env bash
# =============================================================================
# Registra los plugins del paquete tracker (idempotente):
#   1. pluginassembly CashHub.Plugins (re-sube el DLL con los tipos nuevos)
#   2. plugintypes + pasos SINCRONOS:
#      ReceptionBatchTotalsPlugin (B4) — Post-op (40) en sanic_chtblreceptions:
#        Create; Update (filtrado en 4 montos declarados + sanic_receptionbatch,
#        PreImage sanic_receptionbatch); Delete (PreImage sanic_receptionbatch).
#      BagReconciliationMirrorPlugin (B5) — Pre-op (20) en sanic_chtblbags:
#        Create; Update (filtrado en sanic_bagdeclarationid + totales + estado,
#        PreImage de esas columnas).
#      CheckMirrorStampPlugin (B3+B6+B-9) — Pre-op (20) en sanic_chtblchecks:
#        Create; Update (filtrado en deposito + declarado, PreImage declarado).
#        B-9 (espejo sanic_recountingsession) viaja en ESTOS MISMOS pasos: sale de la
#        misma cadena deposito -> tula -> sesion, asi que el filtro en
#        sanic_relatedrecounterdepositid ya es su disparador exacto. La CASCADA de
#        B-9 (Update de sanic_chtblbags filtrado en sanic_recountingsession) se
#        registra en scripts/register-supervisor-mirrors.sh, no aca.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-tracker-mirrors.sh
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

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

# (clase, friendly, entidad, mensaje, stage, filteringattributes, image_attrs, descripcion)
PLUGINS = [
    ('CashHub.Plugins.ReceptionBatchTotalsPlugin', 'CashHub Reception Batch Totals', [
        ('sanic_chtblreceptions', 'Create', 40, None, None,
         'Recomputa los totales declarados del lote al crear una planilla.'),
        ('sanic_chtblreceptions', 'Update', 40,
         'sanic_niocash,sanic_niocheck,sanic_usdcash,sanic_usdcheck,sanic_receptionbatch',
         'sanic_receptionbatch',
         'Recomputa los totales declarados al cambiar montos o mover la planilla de lote.'),
        ('sanic_chtblreceptions', 'Delete', 40, None, 'sanic_receptionbatch',
         'Recomputa los totales declarados del lote al borrar una planilla.'),
    ]),
    ('CashHub.Plugins.BagReconciliationMirrorPlugin', 'CashHub Bag Reconciliation Mirror', [
        ('sanic_chtblbags', 'Create', 20, None, None,
         'Materializa DeclTotal* y HasDifference al crear la tula.'),
        ('sanic_chtblbags', 'Update', 20,
         'sanic_bagdeclarationid,sanic_totalnio,sanic_totalusd,sanic_totaleur,sanic_bagstatus',
         'sanic_bagdeclarationid,sanic_totalnio,sanic_totalusd,sanic_totaleur,sanic_bagstatus',
         'Materializa DeclTotal* y HasDifference cuando cambia declaracion, totales o estado.'),
    ]),
    ('CashHub.Plugins.CheckMirrorStampPlugin', 'CashHub Check Mirror Stamp', [
        ('sanic_chtblchecks', 'Create', 20, None, None,
         'Estampa la tula (B6), la sesion de recuento (B-9) y el MatchedCheck del '
         'declarado (B3) al crear el cheque.'),
        ('sanic_chtblchecks', 'Update', 20,
         'sanic_relatedrecounterdepositid,sanic_relateddeclaredcheck',
         'sanic_relateddeclaredcheck',
         'Re-estampa tula, sesion de recuento y MatchedCheck al cambiar deposito o '
         'cheque declarado.'),
    ]),
]

for typename, friendly, steps in PLUGINS:
    type_id = upsert('plugintypes', f"typename eq '{typename}'", {
        'typename': typename, 'friendlyname': friendly, 'name': typename,
        'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
    }, f"plugintype {typename.split('.')[-1]}", 'plugintypeid')

    for entity, message, stage, filtering, image_attrs, description in steps:
        flt = one('sdkmessagefilters',
            f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}",
            'sdkmessagefilterid')
        name = f"CashHub.{typename.split('.')[-1].replace('Plugin', '')}: {message} de {entity}"
        body = {
            'name': name,
            'description': description,
            'plugintypeid@odata.bind': f"/plugintypes({type_id})",
            'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
            'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
            'stage': stage, 'mode': 0, 'rank': 10, 'supporteddeployment': 0,
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

print("Registro de los espejos del tracker completo.")
PYEOF
