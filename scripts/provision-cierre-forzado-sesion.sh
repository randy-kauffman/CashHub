#!/usr/bin/env bash
# =============================================================================
# Aprovisiona el CIERRE FORZADO DE SESION (P2). Idempotente.
#
#   1. pluginassembly CashHub.Plugins (re-sube el DLL con el tipo nuevo)
#   2. plugintype     CashHub.Plugins.ForceCloseRecountingSessionPlugin
#   3. customapi      sanic_CHForceCloseRecountingSession + 2 parametros + 2 respuestas
#   4. alta de los 5 componentes en CashHubCore + PublishAllXml
#
# POR QUE EXISTE. El supervisor tiene que poder terminar a la fuerza una sesion ya
# aprobada —el cajero se fue, se enfermo, se equivoco de mesa— y que las tulas que
# tenia en la mano queden libres en vez de enllavadas.
#
# LOS FRENOS. Se bloquea si hay una solicitud PENDIENTE de la sesion o una tula
# esperando revision, y el motivo las NOMBRA. Es la opcion conservadora a
# proposito: menos piezas moviles, y el supervisor esta ahi mismo para
# resolverlas. Ademas evita el caso feo de verdad: una solicitud aprobada DESPUES
# del cierre asignaria una tula a una sesion muerta, y ahi no hay reversion que
# valga porque el cajero ya no esta.
#
# POR QUE CUSTOM API Y NO UN PATCH DESDE LA APP. Cerrar la sesion es un Update de
# dos columnas: cualquiera con el rol de supervisor lo hace por Web API o desde
# Excel y se saltea los frenos, las tulas y el barrido. Poner los chequeos en
# Power Fx seria dejar la puerta abierta y un cartel de "no pasar". Ademas cierra
# la sesion Y vacia N tulas: media operacion dejaria la sesion cerrada con tulas
# colgando, que es peor que no haber hecho nada.
#
# executeprivilegename: prvWritesanic_CHTblRecountingSession.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/provision-cierre-forzado-sesion.sh
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

# =============================================================================
# 2 y 3. Assembly y tipo. Re-subir el assembly es lo que hace visible al tipo nuevo.
# =============================================================================
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.ForceCloseRecountingSessionPlugin'", {
    'typename': 'CashHub.Plugins.ForceCloseRecountingSessionPlugin',
    'friendlyname': 'CashHub Force Close Recounting Session',
    'name': 'CashHub.Plugins.ForceCloseRecountingSessionPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype ForceCloseRecountingSessionPlugin', 'plugintypeid')

# =============================================================================
# 4. La Custom API. Tipos: Boolean=0, String=10, Guid=12.
# =============================================================================
api_id = upsert('customapis', "uniquename eq 'sanic_CHForceCloseRecountingSession'", {
    'uniquename': 'sanic_CHForceCloseRecountingSession',
    'name': 'CH Force Close Recounting Session',
    'displayname': 'CH Force Close Recounting Session',
    # OJO: customapi.description tope 300 caracteres. El racional completo esta en
    # la cabecera de este script y en ForceCloseRecountingSessionPlugin.
    'description': (
        'Cierra a la fuerza una sesion de recuento y libera las tulas que el cajero '
        'tenia en la mesa, como UNA transaccion. Se frena si hay solicitudes sin '
        'resolver o tulas esperando revision, y el motivo las nombra. El guard vive '
        'aca y no en la app: cerrar la sesion es un Update que se puede hacer por API.'
    ),
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvWritesanic_CHTblRecountingSession',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHForceCloseRecountingSession', 'customapiid')

REQUEST_PARAMS = [
    ('SessionId', 'sanic_chtblrecountingsessionid de la sesion a cerrar (requerido).', 12, False),
    ('Reason', 'Motivo del cierre forzado. Queda en la sesion como constancia.', 10, True),
]

for uname, label, ptype, optional in REQUEST_PARAMS:
    upsert('customapirequestparameters',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname,
        'description': label, 'type': ptype, 'isoptional': optional,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'parametro {uname}', 'customapirequestparameterid')

resp_ids = {}
for uname, ptype in [('Outcome', 10), ('Message', 10)]:
    resp_ids[uname] = upsert('customapiresponseproperties',
        f"_customapiid_value eq {api_id} and uniquename eq '{uname}'", {
        'uniquename': uname, 'name': uname, 'displayname': uname, 'type': ptype,
        'CustomAPIId@odata.bind': f"/customapis({api_id})",
    }, f'respuesta {uname}', 'customapiresponsepropertyid')

# =============================================================================
# 5. Alta en CashHubCore. IMPRESCINDIBLE: el conector Environment de las canvas
#    apps SOLO expone las Custom API que pertenecen a una solucion. En Default la
#    API anda por Web API pero no aparece en el WADL y la app dice "el nombre no
#    es valido" por mas que se refresque el datasource.
#    componenttype: CustomAPI=10036, RequestParameter=10037, ResponseProperty=10038.
# =============================================================================
req_ids = {}
for uname, _, _, _ in REQUEST_PARAMS:
    flt = "_customapiid_value eq {0} and uniquename eq '{1}'".format(api_id, uname)
    rows = find('/customapirequestparameters?$select=customapirequestparameterid'
                '&$filter=' + urllib.parse.quote(flt))
    req_ids[uname] = rows[0]['customapirequestparameterid']

for cid, ctype, label in (
    [(api_id, 10036, 'customapi')]
    + [(v, 10037, f'parametro {k}') for k, v in req_ids.items()]
    + [(v, 10038, f'respuesta {k}') for k, v in resp_ids.items()]
):
    req('POST', '/AddSolutionComponent', {
        'ComponentId': cid,
        'ComponentType': ctype,
        'SolutionUniqueName': 'CashHubCore',
        'AddRequiredComponents': False,
        'DoNotIncludeSubcomponents': False,
    })
    print(f"  agregado a CashHubCore: {label}")

# Sin esto el WADL del conector Environment sigue sirviendo la version anterior y
# la app no ve la API por mas que ya este en la solucion.
req('POST', '/PublishAllXml', {})
print("  publicado (PublishAllXml)")
PYEOF
