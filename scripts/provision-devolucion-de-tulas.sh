#!/usr/bin/env bash
# =============================================================================
# Aprovisiona la DEVOLUCION DE TULAS PARA REASIGNACION. Idempotente.
#
#   1. opcion 159460006 "Devuelta para reasignacion" en sanic_bagstatus
#   2. pluginassembly CashHub.Plugins (re-sube el DLL con el tipo nuevo)
#   3. plugintype     CashHub.Plugins.ReturnBagForReassignmentPlugin
#   4. customapi      sanic_CHReturnBagForReassignment + 2 parametros + 2 respuestas
#   5. alta de los 5 componentes en CashHubCore + PublishAllXml
#
# POR QUE EXISTE. Una tula que entraba en "Asignada a cajero" quedaba ENLLAVADA:
# sus unicas salidas eran Recontada (terminal) y el estado de revision del
# supervisor, que significa otra cosa —hay una diferencia que aprobar—. Un cajero
# que se va, un cierre de jornada o un escaneo equivocado la dejaban trabada para
# siempre. Al escribir esto habia 747 tulas en ese estado.
#
# EL ESTADO ES PROPIO, NO SE REUSA EL DE SUPERVISOR. En la pantalla del supervisor
# "esto hay que revisarlo" y "esto esta libre" son dos colas distintas; mezclarlas
# volveria inutiles a las dos.
#
# POR QUE CUSTOM API Y NO PATCH DESDE LA APP. La devolucion BORRA: denominaciones,
# cheques, depositos y el detalle de sesion, y ademas desvincula las tres puntas
# declaradas. Son ocho operaciones sobre cinco tablas. Sueltas desde Power Fx, el
# dia que la sexta falla la tula queda a medio limpiar y nadie se entera. Aca o
# pasa todo o no pasa nada.
#
# executeprivilegename: prvWritesanic_CHTblBags. Sin el, cualquier usuario del
# entorno podria borrar el recuento de una tula por Web API.
#
# El veredicto negativo viaja como DATO (Outcome=Blocked + Message, HTTP 200) y
# nunca como fault: Power Fx descarta el mensaje custom de los faults de plugin.
# Y sin Boolean en las respuestas — con una salida booleana el conector Environment
# no expone la API a las canvas apps.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/provision-devolucion-de-tulas.sh
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


# =============================================================================
# 1. La opcion nueva del option set de estado de tula.
#    Es un option set LOCAL (sanic_chtblbags_sanic_bagstatus), asi que
#    InsertOptionValue va con EntityLogicalName + AttributeLogicalName. Con un
#    global habria que mandar OptionSetName y fallaria con "attribute not found".
# =============================================================================
NUEVO_VALOR = 159460006
NUEVA_ETIQUETA = 'Devuelta para reasignacion'

meta = req('GET', "/EntityDefinitions(LogicalName='sanic_chtblbags')/Attributes"
                  "(LogicalName='sanic_bagstatus')/Microsoft.Dynamics.CRM."
                  "PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet")
existentes = {o['Value'] for o in meta['OptionSet']['Options']}

if NUEVO_VALOR in existentes:
    print(f"  ya existe: opcion {NUEVO_VALOR} '{NUEVA_ETIQUETA}'")
else:
    req('POST', '/InsertOptionValue', {
        'EntityLogicalName': 'sanic_chtblbags',
        'AttributeLogicalName': 'sanic_bagstatus',
        'Value': NUEVO_VALOR,
        'Label': {
            '@odata.type': 'Microsoft.Dynamics.CRM.Label',
            'LocalizedLabels': [{
                '@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel',
                'Label': NUEVA_ETIQUETA,
                'LanguageCode': 1033,
            }],
        },
        'SolutionUniqueName': 'CashHubCore',
    })
    print(f"  creada: opcion {NUEVO_VALOR} '{NUEVA_ETIQUETA}'")

content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

# =============================================================================
# 2 y 3. Assembly y tipo. Re-subir el assembly es lo que hace visible al tipo nuevo.
# =============================================================================
asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

type_id = upsert('plugintypes', "typename eq 'CashHub.Plugins.ReturnBagForReassignmentPlugin'", {
    'typename': 'CashHub.Plugins.ReturnBagForReassignmentPlugin',
    'friendlyname': 'CashHub Return Bag For Reassignment',
    'name': 'CashHub.Plugins.ReturnBagForReassignmentPlugin',
    'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
}, 'plugintype ReturnBagForReassignmentPlugin', 'plugintypeid')

# =============================================================================
# 4. La Custom API. Tipos: Boolean=0, String=10, Guid=12.
# =============================================================================
api_id = upsert('customapis', "uniquename eq 'sanic_CHReturnBagForReassignment'", {
    'uniquename': 'sanic_CHReturnBagForReassignment',
    'name': 'CH Return Bag For Reassignment',
    'displayname': 'CH Return Bag For Reassignment',
    # OJO: customapi.description tope 300 caracteres. El racional completo esta en
    # la cabecera de este script y en ReturnBagForReassignmentPlugin.
    'description': (
        'Saca una tula de la mesa y la deja libre, borrando su recuento como UNA '
        'transaccion: denominaciones, cheques, depositos y detalle de sesion, y '
        'desvincula lo declarado. Solo desde "Asignada a cajero", que es lo que '
        'garantiza que no haya asientos contables que revertir.'
    ),
    'bindingtype': 0, 'isfunction': False, 'isprivate': False,
    'allowedcustomprocessingsteptype': 0,
    'executeprivilegename': 'prvWritesanic_CHTblBags',
    'PluginTypeId@odata.bind': f"/plugintypes({type_id})",
}, 'customapi sanic_CHReturnBagForReassignment', 'customapiid')

REQUEST_PARAMS = [
    ('BagId', 'sanic_chtblbagsid de la tula a devolver (requerido).', 12, False),
    ('Reason', 'Motivo de la devolucion, para la traza del plugin.', 10, True),
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
