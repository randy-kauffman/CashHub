#!/usr/bin/env bash
# =============================================================================
# Da al DIGITADOR permiso de LECTURA (y solo lectura) sobre
# sanic_chtbltransactions.sanic_accountnumber, que tiene Field Level Security.
#
# POR QUE HACE FALTA. El CSV al core lleva la cuenta en la columna CUENTA. Cuando
# la generacion paso del flujo (que corria con la identidad del dueño de la
# conexion) al Custom API (que corre en el contexto de QUIEN LLAMA), el digitador
# dejo de poder leer la columna: Dataverse no falla, la enmascara y devuelve null.
# Resultado: las 108 filas del lote salian con CUENTA 000000000 y el core habria
# recibido todos los depositos contra la cuenta cero. Silencioso, no explota.
#
# POR QUE UN PERFIL NUEVO Y NO "CashHub | FLS | Supervisor Cuentas". Ese perfil da
# read=4 update=4 create=4 sobre esa misma columna: asignarselo al digitador no le
# daria "ver la cuenta", le daria CAMBIARLA. El digitador tiene que poder leer lo
# que exporta, no reescribirlo.
#
#   perfil : CashHub | FLS | Digitador Lectura Cuenta
#   permiso: sanic_chtbltransactions.sanic_accountnumber  read=SI  update=NO  create=NO
#
# Idempotente: crea lo que falte y reasigna sin duplicar. Toma a los usuarios con
# el rol CH-SR-Digitizer (directo o por equipo), asi que al sumar un digitador
# nuevo basta con volver a correrlo.
#
# Uso: ./scripts/grant-digitizer-account-read.sh
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
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Accept: application/json']

PROFILE = 'CashHub | FLS | Digitador Lectura Cuenta'
ENTITY = 'sanic_chtbltransactions'
ATTRIBUTE = 'sanic_accountnumber'
ROLE = 'CH-SR-Digitizer'

ALLOWED, NOT_ALLOWED = 4, 0


def req(method, path, body=None, ok=(200, 201, 204)):
    cmd = ['curl', '-s', '-w', '\n__C__%{http_code}', *H, '-X', method,
           path if path.startswith('http') else API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    payload, _, code = out.rpartition('\n__C__')
    code = int(code or 0)
    if not payload.strip():
        if code in ok:
            return {}
        raise RuntimeError(f"{method} {path}: HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d


def find(path):
    return req('GET', path).get('value', [])


# 1. Perfil
rows = find("/fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter="
            + urllib.parse.quote(f"name eq '{PROFILE}'"))
if rows:
    profile_id = rows[0]['fieldsecurityprofileid']
    print(f"  perfil ya existe: {PROFILE}")
else:
    profile_id = req('POST', "/fieldsecurityprofiles", {
        'name': PROFILE,
        'description': ('Lectura de la cuenta destino en los asientos contables, para que el '
                        'digitador pueda ver y exportar lo que manda al core. SOLO LECTURA: '
                        'no puede modificarla ni crearla.'),
    }, ok=(200, 201, 204))
    profile_id = find("/fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter="
                      + urllib.parse.quote(f"name eq '{PROFILE}'"))[0]['fieldsecurityprofileid']
    print(f"  perfil creado: {PROFILE}")

# 2. Permiso de solo lectura sobre la columna
perms = find("/fieldpermissions?$select=fieldpermissionid,canread,canupdate,cancreate&$filter="
             + urllib.parse.quote(
                 f"_fieldsecurityprofileid_value eq {profile_id} and "
                 f"entityname eq '{ENTITY}' and attributelogicalname eq '{ATTRIBUTE}'"))
body = {'canread': ALLOWED, 'canupdate': NOT_ALLOWED, 'cancreate': NOT_ALLOWED}
if perms:
    req('PATCH', f"/fieldpermissions({perms[0]['fieldpermissionid']})", body)
    print(f"  permiso actualizado: {ENTITY}.{ATTRIBUTE}  read=SI update=NO create=NO")
else:
    req('POST', "/fieldpermissions", dict(body, **{
        'entityname': ENTITY,
        'attributelogicalname': ATTRIBUTE,
        'fieldsecurityprofileid@odata.bind': f"/fieldsecurityprofiles({profile_id})",
    }))
    print(f"  permiso creado: {ENTITY}.{ATTRIBUTE}  read=SI update=NO create=NO")

# 3. Digitadores: los que tienen el rol directo y los de los equipos que lo tienen.
role = find("/roles?$select=roleid&$filter=" + urllib.parse.quote(f"name eq '{ROLE}'"))
if not role:
    raise SystemExit(f"No existe el rol {ROLE}.")
role_id = role[0]['roleid']

usuarios = {u['systemuserid']: u['fullname']
            for u in find(f"/roles({role_id})/systemuserroles_association?$select=fullname,isdisabled")
            if not u.get('isdisabled')}
for team in find(f"/roles({role_id})/teamroles_association?$select=teamid,name"):
    for u in find(f"/teams({team['teamid']})/teammembership_association?$select=fullname,isdisabled"):
        if not u.get('isdisabled'):
            usuarios[u['systemuserid']] = u['fullname']

ya = {u['systemuserid'] for u in
      find(f"/fieldsecurityprofiles({profile_id})/systemuserprofiles_association?$select=systemuserid")}

for uid, nombre in sorted(usuarios.items(), key=lambda kv: kv[1]):
    if uid in ya:
        print(f"  ya asignado: {nombre}")
        continue
    req('POST', f"/fieldsecurityprofiles({profile_id})/systemuserprofiles_association/$ref",
        {'@odata.id': f"{API}/systemusers({uid})"})
    print(f"  asignado: {nombre}")

print(f"Listo: {len(usuarios)} digitador(es) pueden LEER {ENTITY}.{ATTRIBUTE}.")
PYEOF
