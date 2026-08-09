#!/usr/bin/env bash
# =============================================================================
# Asigna los roles de seguridad a los AAD group teams de ClientAdmin (idempotente):
#   - grupos Approver    -> rol CH-SR-ClientAdminApprover
#   - grupos Contributor -> rol CH-SR-ClientAdminContributor
# Los miembros del team heredan el rol (asi User1..4 pueden crear/aprobar RFCs).
#
# Data-driven desde CH-Tbl-ClientDataChangePermission. Respeta que el rol y el
# team esten en la misma business unit (si no, Dataverse rechaza la asociacion).
#
# Requiere pp_secrets.env.  Uso: ./scripts/assign-clientadmin-team-roles.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL

python3 - <<'PYEOF'
import json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json']
APPROVER, CONTRIBUTOR = 159460001, 159460000
ROLE_BY_TYPE = {APPROVER: 'CH-SR-ClientAdminApprover', CONTRIBUTOR: 'CH-SR-ClientAdminContributor'}

def req(method, path, body=None):
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['--data-binary', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout.rsplit('\n', 1)
    code = out[-1]
    d = json.loads(out[0]) if out[0].strip() else {}
    return code, d

def find(path):
    _, d = req('GET', path)
    return d.get('value', [])

def role_in_bu(name, bu):
    rows = find(f"/roles?$select=roleid&$filter=" +
        urllib.parse.quote(f"name eq '{name}' and _businessunitid_value eq {bu}"))
    return rows[0]['roleid'] if rows else None

perms = find("/sanic_chtblclientdatachangepermissions?$select=sanic_name,sanic_aadgroupid,sanic_type")
seen = set()
for p in perms:
    gid = (p.get('sanic_aadgroupid') or '').strip()
    ptype = p.get('sanic_type')
    if not gid or ptype not in ROLE_BY_TYPE or (gid, ptype) in seen:
        continue
    seen.add((gid, ptype))

    teams = find(f"/teams?$select=teamid,name,_businessunitid_value&$filter=" +
        urllib.parse.quote(f"azureactivedirectoryobjectid eq {gid}"))
    if not teams:
        print(f"  SIN TEAM para grupo {gid} ({ROLE_BY_TYPE[ptype]}) — omitido"); continue
    team = teams[0]
    role_name = ROLE_BY_TYPE[ptype]
    role_id = role_in_bu(role_name, team['_businessunitid_value'])
    if not role_id:
        print(f"  ROL {role_name} no existe en la BU del team '{team['name']}' — omitido"); continue

    # ¿ya asignado?
    assigned = find(f"/teams({team['teamid']})/teamroles_association?$select=roleid&$filter=" +
        urllib.parse.quote(f"roleid eq {role_id}"))
    if assigned:
        print(f"  ya asignado: {role_name} -> {team['name']}"); continue

    code, d = req('POST', f"/teams({team['teamid']})/teamroles_association/$ref",
        {"@odata.id": f"{API}/roles({role_id})"})
    if code.startswith('2'):
        print(f"  asignado: {role_name} -> {team['name']}")
    else:
        print(f"  ERROR asignando {role_name} -> {team['name']}: {code} {(d.get('error') or {}).get('message','')[:160]}")

print("Asignacion de roles a teams completa.")
PYEOF
