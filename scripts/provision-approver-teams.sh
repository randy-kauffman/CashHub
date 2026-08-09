#!/usr/bin/env bash
# =============================================================================
# Provisiona los AAD group teams que el guard de aprobacion (Fase 2) necesita
# para resolver membresia desde el plugin (idempotente).
#
# Fuente de verdad: CH-Tbl-ClientDataChangePermission (filas type=Approver).
# Para cada grupo AAD distinto usado como aprobador, crea (si no existe) un team
# teamtype=2 (Azure AD Security Group) con azureactivedirectoryobjectid = grupo.
# La membresia se materializa sola cuando el usuario accede al entorno.
#
# Requiere pp_secrets.env.
# Uso: ./scripts/provision-approver-teams.sh
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
import json, os, subprocess, urllib.parse, collections

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Prefer: return=representation']
APPROVER = 159460001

def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['--data-binary', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(path):
    return req('GET', path).get('value', [])

# Business unit raiz + service principal (administrador del team)
root_bu = find("/businessunits?$select=businessunitid&$filter=" +
    urllib.parse.quote("_parentbusinessunitid_value eq null"))[0]['businessunitid']
me = req('GET', '/WhoAmI')['UserId']

# Grupos Approver distintos y las tablas que cubre cada uno
perms = find("/sanic_chtblclientdatachangepermissions?$select=sanic_name,sanic_aadgroupid,sanic_type&$filter=" +
    urllib.parse.quote(f"sanic_type eq {APPROVER}"))
groups = collections.defaultdict(list)
for p in perms:
    gid = (p.get('sanic_aadgroupid') or '').strip()
    if gid:
        groups[gid].append(p.get('sanic_name'))

if not groups:
    print("No hay filas Approver con grupo AAD en CH-Tbl-ClientDataChangePermission."); raise SystemExit(1)

for gid, tables in groups.items():
    existing = find(f"/teams?$select=teamid,name&$filter=" +
        urllib.parse.quote(f"azureactivedirectoryobjectid eq {gid}"))
    label = "CashHub Aprobadores - " + ", ".join(sorted(set(tables)))
    if existing:
        print(f"  ya existe: team {gid} ({existing[0].get('name')})")
        continue
    req('POST', '/teams', {
        'name': label[:160],
        'teamtype': 2,                       # Azure AD Security Group team
        'membershiptype': 0,
        'azureactivedirectoryobjectid': gid,
        'businessunitid@odata.bind': f"/businessunits({root_bu})",
        'administratorid@odata.bind': f"/systemusers({me})",
    })
    print(f"  creado: team {gid} -> {label}")

print("Provision de teams de aprobadores completa.")
print("Nota: la membresia se materializa cuando cada aprobador accede al entorno.")
PYEOF
