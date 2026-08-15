#!/usr/bin/env bash
# =============================================================================
# Registra los espejos de CH-App-Supervisor v2 (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con los tipos nuevos dentro)
#   2. plugintypes + pasos SINCRONOS:
#
#      BagReceptionMirrorPlugin (B-2 + B-3)
#        Pre-operation  (20) Create de sanic_chtblbags
#            estampa sanic_receptionbatchid + sanic_receiptidmirror desde la planilla.
#        Pre-operation  (20) Update de sanic_chtblbags, filtrado en sanic_receptionid
#            re-estampa los dos espejos al re-colgar la tula de otra planilla.
#        Post-operation (40) Update de sanic_chtblreceptions, filtrado en
#            sanic_receptionbatch + sanic_receiptid, con PreImage de esas dos:
#            CASCADA a todas las tulas de la planilla (paginada de a 500).
#            La PreImage es obligatoria: sin ella no se sabe si el valor CAMBIO y la
#            cascada escribiria N tulas por un update sin cambio real.
#
#      ApprovalRequestBagNumberPlugin (B-6)
#        Pre-operation  (20) Create de sanic_chtblapprovalrequest
#            estampa sanic_bagnumber parseando el JSON de sanic_requestresources.
#            Nunca falla el Create: sin dato deja la columna en blanco y traza.
#
#      CheckMirrorStampPlugin (B-9, SOLO la cascada)
#        Post-operation (40) Update de sanic_chtblbags, filtrado en
#            sanic_recountingsession, con PreImage de esa columna:
#            CASCADA de la sesion a TODOS los cheques de la tula (paginada de a 500).
#            La PreImage es obligatoria: sin ella no se sabe si la sesion CAMBIO y la
#            cascada reescribiria N cheques por un update sin cambio real.
#
#        OJO — los pasos de sanic_chtblchecks de ESTE plugin (Create y Update
#        Pre-op 20) NO se tocan aca: los registra scripts/register-tracker-mirrors.sh
#        y ya cubren B-9 tal cual estan. El Update ya viene filtrado en
#        sanic_relatedrecounterdepositid, que es exactamente el disparador del espejo
#        de sesion (deposito -> tula -> sesion). Duplicar el step aca seria pelearse
#        por el mismo nombre de step con filtros distintos: gana el ultimo que corra.
#        El guard de cheques (register-check-guard.sh) tampoco cambia: su paso de
#        Update sigue SIN filteringattributes a proposito; lo que deja pasar la
#        cascada es el bypass touchesOnlyMirror de CheckGuard, que ahora incluye
#        sanic_recountingsession ademas de sanic_bag.
#
# Los espejos existen para que las consultas del supervisor DELEGUEN (dot-walk de
# 2 saltos y numero de tula dentro de un JSON no delegan). Ver
# docs/supervisor-rebuild/00-analisis-y-plan.md §2 y el ledger L-18/L-19.
#
# ORDEN: correr DESPUES de create-supervisor-mirror-columns.sh y del build Release.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-supervisor-mirrors.sh
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

# --- guarda: sin las columnas creadas, registrar los pasos es sembrar errores ---
def attr_exists(entity, logical):
    # GET directo: el $filter sobre metadata de Attributes da falsos negativos
    # (observado con sanic_bag: filtro vacio, GET directo 200). req() convierte
    # el error OData en RuntimeError -> "Could not find" = no existe.
    try:
        d = req('GET', "/EntityDefinitions(LogicalName='%s')/Attributes(LogicalName='%s')?$select=LogicalName"
                % (entity, logical))
        return bool(d.get('LogicalName'))
    except RuntimeError:
        return False

MISSING = [f"{e}.{a}" for e, a in [
    ('sanic_chtblbags', 'sanic_receptionbatchid'),
    ('sanic_chtblbags', 'sanic_receiptidmirror'),
    ('sanic_chtblapprovalrequest', 'sanic_bagnumber'),
    ('sanic_chtblchecks', 'sanic_recountingsession'),
] if not attr_exists(e, a)]
if MISSING:
    raise SystemExit("Faltan columnas espejo: " + ", ".join(MISSING)
                     + " — correr primero ./scripts/create-supervisor-mirror-columns.sh")

content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update')}

# (clase, friendly, [(entidad, mensaje, stage, filteringattributes, preimage_attrs, descripcion)])
PLUGINS = [
    ('CashHub.Plugins.BagReceptionMirrorPlugin', 'CashHub Bag Reception Mirror', [
        ('sanic_chtblbags', 'Create', 20, None, None,
         'Estampa el lote (B-2) y el recibo (B-3) de la planilla en la tula.'),
        ('sanic_chtblbags', 'Update', 20, 'sanic_receptionid', None,
         'Re-estampa lote y recibo al re-colgar la tula de otra planilla.'),
        ('sanic_chtblreceptions', 'Update', 40,
         'sanic_receptionbatch,sanic_receiptid',
         'sanic_receptionbatch,sanic_receiptid',
         'Propaga el lote y el recibo nuevos de la planilla a todas sus tulas.'),
    ]),
    ('CashHub.Plugins.ApprovalRequestBagNumberPlugin', 'CashHub Approval Bag Number', [
        ('sanic_chtblapprovalrequest', 'Create', 20, None, None,
         'Estampa sanic_bagnumber desde el JSON de RequestResources (B-6).'),
    ]),
    # B-9: SOLO la cascada. Los pasos de sanic_chtblchecks de este mismo plugintype
    # (Create/Update Pre-op 20) son de register-tracker-mirrors.sh y ya cubren B-9.
    # El upsert del plugintype por typename es idempotente: los dos scripts pueden
    # correr en cualquier orden sin pisarse.
    ('CashHub.Plugins.CheckMirrorStampPlugin', 'CashHub Check Mirror Stamp', [
        ('sanic_chtblbags', 'Update', 40,
         'sanic_recountingsession',
         'sanic_recountingsession',
         'Propaga la sesion de recuento nueva de la tula a todos sus cheques (B-9).'),
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
            # rank 20: en Create de sanic_chtblbags ya hay otro Pre-op(20) rank 10
        # (BagReconciliationMirrorPlugin). Escriben columnas DISJUNTAS, pero un orden
        # explicito vale mas que un empate resuelto por el motor.
        'stage': stage, 'mode': 0, 'rank': 20, 'supporteddeployment': 0,
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

print("Registro de los espejos del supervisor completo.")
PYEOF
