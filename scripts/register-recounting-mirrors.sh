#!/usr/bin/env bash
# =============================================================================
# Registra los espejos server-side de CH-App-Recounting v2 (idempotente):
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con los tipos nuevos dentro)
#   2. plugintypes + pasos SINCRONOS:
#
#      DepositSessionMirrorPlugin (E-1)
#        Pre-operation  (20) Create de sanic_chtbldepositsrecounting
#            estampa sanic_recountingsessionmirror desde BagID.RecountingSession.
#        Pre-operation  (20) Update de sanic_chtbldepositsrecounting, filtrado en
#            sanic_bagid: re-estampa al re-colgar el deposito de otra tula.
#        Post-operation (40) Update de sanic_chtblbags, filtrado en
#            sanic_recountingsession y con PreImage de esa columna:
#            CASCADA de la sesion a TODOS los depositos de la tula (paginada de a 500).
#            La PreImage es OBLIGATORIA: sin ella no se sabe si la sesion CAMBIO y la
#            cascada reescribiria N depositos por un update sin cambio real.
#
#            OJO — este paso CONVIVE con "CashHub.CheckMirrorStamp: Update de
#            sanic_chtblbags" (B-9, misma entidad, mismo stage, mismo filtro). Son
#            plugintypes distintos, escriben TABLAS distintas (depositos vs cheques) y
#            tienen nombres de step distintos, asi que no se pisan. Rank 30 vs 20 del
#            de cheques: un orden explicito vale mas que un empate resuelto por el motor.
#
#      DeclaredCheckBagMirrorPlugin (E-3)
#        Pre-operation  (20) Create de sanic_chtbldeclaredchecks
#            estampa sanic_declaredbagmirror desde DeclaredDepositID.DeclaredBag.
#        Pre-operation  (20) Update de sanic_chtbldeclaredchecks, filtrado en
#            sanic_declareddepositid: re-estampa al re-colgar el cheque declarado.
#        SIN cascada a proposito: DeclarationMaterializer escribe sanic_declaredbag en
#        el CREATE del deposito declarado y nadie re-parenta despues (la declaracion es
#        inmutable). La cobertura es el backfill. Ver el header del plugin.
#
#      XmlReadingDetailMirrorPlugin (E-4 — DOS columnas)
#        Pre-operation  (20) Create de sanic_chtblxmlreadingdetail
#            estampa sanic_recountingsessiondetailmirror + sanic_xmlreadingactiva.
#        Pre-operation  (20) Update de sanic_chtblxmlreadingdetail, filtrado en
#            sanic_xmlreading: re-estampa al re-colgar el detalle de otra lectura.
#        Post-operation (40) Update de sanic_chtblxmlreading, filtrado en
#            sanic_recountingsessiondetailid + sanic_isactive, con PreImage de las dos:
#            CASCADA a TODOS los detalles de la lectura (paginada de a 500).
#            Este paso NO es opcional: cuando llega una lectura nueva, la vieja se apaga
#            (IsActive -> false) DESPUES de que sus detalles ya existen. Sin el, Q17
#            sigue sumando la lectura muerta y la columna Auto del cajero miente.
#
#   E-2 (sanic_bag en sanic_chtblchecks) NO se toca aca: ya lo registra
#   scripts/register-tracker-mirrors.sh (CheckMirrorStampPlugin, Create + Update
#   filtrado en sanic_relatedrecounterdepositid) y esta ACTIVO en DEV. Ver ledger L-21.
#
# ORDEN: correr DESPUES de create-recounting-mirror-columns.sh y del build Release.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-recounting-mirrors.sh
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
    # (observado con sanic_bag). req() convierte el error OData en RuntimeError.
    try:
        d = req('GET', "/EntityDefinitions(LogicalName='%s')/Attributes(LogicalName='%s')?$select=LogicalName"
                % (entity, logical))
        return bool(d.get('LogicalName'))
    except RuntimeError:
        return False

MISSING = [f"{e}.{a}" for e, a in [
    ('sanic_chtbldepositsrecounting', 'sanic_recountingsessionmirror'),
    ('sanic_chtbldeclaredchecks', 'sanic_declaredbagmirror'),
    ('sanic_chtblxmlreadingdetail', 'sanic_recountingsessiondetailmirror'),
    ('sanic_chtblxmlreadingdetail', 'sanic_xmlreadingactiva'),
] if not attr_exists(e, a)]
if MISSING:
    raise SystemExit("Faltan columnas espejo: " + ", ".join(MISSING)
                     + " — correr primero ./scripts/create-recounting-mirror-columns.sh")

content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update')}

# (clase, friendly, [(entidad, mensaje, stage, rank, filteringattributes, preimage_attrs, descripcion)])
PLUGINS = [
    ('CashHub.Plugins.DepositSessionMirrorPlugin', 'CashHub Deposit Session Mirror', [
        ('sanic_chtbldepositsrecounting', 'Create', 20, 20, None, None,
         'Estampa la sesion de recuento de la tula en el deposito (E-1).'),
        ('sanic_chtbldepositsrecounting', 'Update', 20, 20, 'sanic_bagid', None,
         'Re-estampa la sesion al re-colgar el deposito de otra tula (E-1).'),
        # rank 30: convive con el paso de cheques (B-9, rank 20) sobre la MISMA
        # entidad/stage/filtro. Escriben tablas disjuntas; el orden es explicito igual.
        ('sanic_chtblbags', 'Update', 40, 30,
         'sanic_recountingsession',
         'sanic_recountingsession',
         'Propaga la sesion de recuento nueva de la tula a todos sus depositos (E-1).'),
    ]),
    ('CashHub.Plugins.DeclaredCheckBagMirrorPlugin', 'CashHub Declared Check Bag Mirror', [
        ('sanic_chtbldeclaredchecks', 'Create', 20, 20, None, None,
         'Estampa la bolsa declarada del deposito declarado en el cheque declarado (E-3).'),
        ('sanic_chtbldeclaredchecks', 'Update', 20, 20, 'sanic_declareddepositid', None,
         'Re-estampa la bolsa al re-colgar el cheque declarado de otro deposito (E-3).'),
    ]),
    ('CashHub.Plugins.XmlReadingDetailMirrorPlugin', 'CashHub XML Reading Detail Mirror', [
        ('sanic_chtblxmlreadingdetail', 'Create', 20, 20, None, None,
         'Estampa sesion-detalle y vigencia de la lectura en el detalle (E-4).'),
        ('sanic_chtblxmlreadingdetail', 'Update', 20, 20, 'sanic_xmlreading', None,
         'Re-estampa las dos columnas al re-colgar el detalle de otra lectura (E-4).'),
        ('sanic_chtblxmlreading', 'Update', 40, 20,
         'sanic_recountingsessiondetailid,sanic_isactive',
         'sanic_recountingsessiondetailid,sanic_isactive',
         'Propaga sesion-detalle y vigencia a todos los detalles de la lectura (E-4). '
         'Es lo que mantiene viva la consulta de la contadora cuando la lectura se apaga.'),
    ]),
]

for typename, friendly, steps in PLUGINS:
    type_id = upsert('plugintypes', f"typename eq '{typename}'", {
        'typename': typename, 'friendlyname': friendly, 'name': typename,
        'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
    }, f"plugintype {typename.split('.')[-1]}", 'plugintypeid')

    for entity, message, stage, rank, filtering, image_attrs, description in steps:
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
            'stage': stage, 'mode': 0, 'rank': rank, 'supporteddeployment': 0,
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

print("Registro de los espejos de recounting completo.")
PYEOF
