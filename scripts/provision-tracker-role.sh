#!/usr/bin/env bash
# =============================================================================
# B8 — Rol CH-SR-DepositsTracker (idempotente):
#   1. Crea el rol en la BU raiz (si no existe) y lo agrega a CashHubCore.
#   2. Le otorga prvRead con profundidad GLOBAL sobre las tablas de la cadena de
#      trazabilidad (la app es un VISOR: cero privilegios de escritura).
#   3. (Opcional) AAD_GROUP_ID=<guid> crea/actualiza el group team
#      "CashHub - Tracker", le asigna el rol y registra el GUID en
#      CH-Tbl-Parameter — mismo patron que los approver teams.
#
# Decision del usuario 2026-08-12: CH-SR-AnalyticsRead NO se toca.
# Requiere pp_secrets.env.
# Uso: ./scripts/provision-tracker-role.sh
#      AAD_GROUP_ID=xxxx ./scripts/provision-tracker-role.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL AAD_GROUP_ID="${AAD_GROUP_ID:-}"

python3 - <<'PYEOF'
import json, os, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Prefer: return=representation']

ROLE_NAME = 'CH-SR-DepositsTracker'
TEAM_NAME = 'CashHub - Tracker'
PARAM_KEY = 'aad-group-tracker'

# Tablas con prvRead GLOBAL (visor de trazabilidad transversal).
TABLES = [
    'sanic_chtblreceptionbatch', 'sanic_chtblreceptions', 'sanic_chtblbags',
    'sanic_chtblrecountingsession', 'sanic_chtblrecountingsessiondetail',
    'sanic_chtbldepositsrecounting', 'sanic_chtblchecks',
    'sanic_chtbltransactions', 'sanic_transactionbatches',
    'sanic_chtbldeclaration', 'sanic_chtbldeclaredbags',
    'sanic_chtbldeclareddeposits', 'sanic_chtbldeclaredchecks',
    'sanic_chtblreceivedemails', 'sanic_chtblemailsattachments',
    'sanic_chtblvalidationruns', 'sanic_chtblvalidationresults',
    'sanic_validationrules', 'sanic_chtblclient',
    'sanic_chtbllocations', 'sanic_chtblappcatalog', 'sanic_chtblappcatalogaccess',
    # sanic_chtblparameter NO va: es organization-owned sin privilegios generados
    # (ningun rol del proyecto la referencia; la lectura no se gobierna por rol).
]
DEPTH_GLOBAL = 'Global'  # el Web API espera el enum como string: Basic/Local/Deep/Global


def req(method, path, body=None, extra=None):
    cmd = ['curl', '-s', *H, *(extra or []), '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:400]}")
    return d


def find(path):
    return req('GET', path).get('value', [])


# 1. Rol en la BU raiz
root_bu = find("/businessunits?$select=businessunitid&$filter=" +
               urllib.parse.quote('_parentbusinessunitid_value eq null'))[0]['businessunitid']
roles = find(f"/roles?$select=roleid&$filter=" +
             urllib.parse.quote(f"name eq '{ROLE_NAME}' and _businessunitid_value eq {root_bu}"))
if roles:
    role_id = roles[0]['roleid']
    print(f"rol existente: {ROLE_NAME} ({role_id})")
else:
    created = req('POST', '/roles', {
        'name': ROLE_NAME,
        'businessunitid@odata.bind': f"/businessunits({root_bu})",
        'description': 'Visor del Deposits Tracker: lectura Global de la cadena de '
                       'trazabilidad (correo->declaracion->recepcion->recuento->transaccion). '
                       'Sin escritura. NO confundir con CH-SR-AnalyticsRead.',
    }, extra=['-H', 'MSCRM.SolutionUniqueName: CashHubCore'])
    role_id = created['roleid']
    print(f"rol creado: {ROLE_NAME} ({role_id})")

req('POST', '/AddSolutionComponent', {
    'ComponentType': 20, 'ComponentId': role_id,
    'SolutionUniqueName': 'CashHubCore', 'AddRequiredComponents': False,
})
print('rol agregado a CashHubCore')

# 2. Privilegios prvRead Global (idempotente: se saltan los ya otorgados)
existing = {p['privilegeid'] for p in find(
    f"/roles({role_id})/roleprivileges_association?$select=privilegeid")}

privileges = []
for table in TABLES:
    meta = req('GET', f"/EntityDefinitions(LogicalName='{table}')?$select=SchemaName")
    priv_name = 'prvRead' + meta['SchemaName']
    rows = find(f"/privileges?$select=privilegeid&$filter=" +
                urllib.parse.quote(f"name eq '{priv_name}'"))
    if not rows:
        # Fallback: el SchemaName del privilegio no siempre calza con el de la
        # entidad (tablas creadas por API); se busca por contains.
        rows = [r for r in find(f"/privileges?$select=privilegeid,name&$filter=" +
                urllib.parse.quote(f"contains(name,'{table}')"))
                if r['name'].lower().startswith('prvread')]
    if not rows:
        print(f"  !! privilegio no encontrado: {priv_name} — revisar a mano")
        continue
    if rows[0]['privilegeid'] in existing:
        print(f"  ya otorgado: {table}")
        continue
    privileges.append({'PrivilegeId': rows[0]['privilegeid'], 'Depth': DEPTH_GLOBAL})
    print(f"  prvRead Global: {table}")

if privileges:
    req('POST', f"/roles({role_id})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
        {'Privileges': privileges})
print(f"privilegios otorgados: {len(privileges)}")

# 3. Team AAD opcional
aad = os.environ.get('AAD_GROUP_ID', '').strip()
if not aad:
    print('AAD_GROUP_ID no provisto: team y sharing quedan pendientes '
          '(crear el grupo en Entra y re-correr con AAD_GROUP_ID=<guid>).')
else:
    teams = find(f"/teams?$select=teamid&$filter=" +
                 urllib.parse.quote(f"name eq '{TEAM_NAME}'"))
    if teams:
        team_id = teams[0]['teamid']
        print(f"team existente: {TEAM_NAME} ({team_id})")
    else:
        created = req('POST', '/teams', {
            'name': TEAM_NAME, 'teamtype': 2,
            'azureactivedirectoryobjectid': aad,
            'businessunitid@odata.bind': f"/businessunits({root_bu})",
        })
        team_id = created['teamid']
        print(f"team creado: {TEAM_NAME} ({team_id})")

    assigned = find(f"/teams({team_id})/teamroles_association?$select=roleid&$filter=" +
                    urllib.parse.quote(f"roleid eq {role_id}"))
    if not assigned:
        req('POST', f"/teams({team_id})/teamroles_association/$ref",
            {'@odata.id': f"{API}/roles({role_id})"})
        print('rol asignado al team')
    else:
        print('rol ya asignado al team')

    params = find(f"/sanic_chtblparameters?$select=sanic_chtblparameterid&$filter=" +
                  urllib.parse.quote(f"sanic_name eq '{PARAM_KEY}'"))
    body = {'sanic_name': PARAM_KEY, 'sanic_value': aad}
    if params:
        req('PATCH', f"/sanic_chtblparameters({params[0]['sanic_chtblparameterid']})", body)
        print(f"parametro actualizado: {PARAM_KEY}")
    else:
        req('POST', '/sanic_chtblparameters', body)
        print(f"parametro creado: {PARAM_KEY}")

print('Provision del rol del tracker completa. Recorda COMPARTIR la app con el grupo AAD al publicarla.')
PYEOF
