#!/usr/bin/env bash
# =============================================================================
# Le da al CAJERO lectura del numero de cuenta, sin escritura.
# Idempotente: crea lo que falta y deja lo que ya esta.
#
# POR QUE EXISTE. En la grilla de depositos declarados la columna CUENTA salia
# vacia para el cajero. No era un bug de la app ni de la data: los campos de
# numero de cuenta tienen SEGURIDAD A NIVEL DE CAMPO, y el unico perfil que los
# habilitaba —"CashHub | FLS | Supervisor Cuentas"— esta asignado al equipo
# aprobador. El cajero los leia como null, EN SILENCIO: sin error, sin aviso,
# solo una columna en blanco.
#
# Campos con FLS en CashHub y quien los ve hoy:
#   declareddeposits.accountnumber      supervisor
#   depositsrecounting.accountnumber    supervisor
#   transactions.accountnumber          supervisor
#   declareddeposits.depositreference   SOLO admin
#   declaredchecks.checknumber          SOLO admin
#
# ESTE SCRIPT CAMBIA UNA DECISION DE SEGURIDAD, y por eso conviene que se lea
# antes de correrlo: el cajero pasa a VER a que cuenta va cada deposito. Se le da
# LECTURA y nada mas — sin update ni create, ni sobre transactions, ni sobre los
# dos campos que hoy son solo-admin.
#
# Se asigna a todos los usuarios que tengan el rol del cajero. Cuando exista un
# equipo de cajeros conviene moverlo al equipo y re-correr: asignar de a un
# usuario no escala, pero hoy no hay equipo al que colgarlo.
#
# Uso: ./scripts/grant-cashier-account-read.sh
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

def req(method, path, body=None, prefer=None):
    cmd = ['curl', '-s', '-w', '\n__C__%{http_code}', *H]
    if prefer:
        cmd += ['-H', f'Prefer: {prefer}']
    cmd += ['-X', method, API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    payload, _, code = out.rpartition('\n__C__')
    if not payload.strip():
        if 200 <= int(code or 0) < 300:
            return {}
        raise SystemExit(f"{method} {path}: HTTP {code} sin cuerpo")
    d = json.loads(payload)
    if 'error' in d:
        raise SystemExit(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(entityset, filtro, select):
    return req('GET', f"/{entityset}?$select={select}&$filter={urllib.parse.quote(filtro)}").get('value', [])

PERFIL = 'CashHub | FLS | Cajero Lectura'
ROL_CAJERO = 'f9bee50d-6a75-f011-b4cb-7c1e5248985f'   # CH-SR-Cashier (por ID: el nombre difiere entre entorno y repo)

# Solo lectura, y solo sobre los dos campos que el cajero necesita para trabajar.
# transactions.accountnumber queda AFUERA a proposito: el cajero no contabiliza.
CAMPOS = [
    ('sanic_chtbldeclareddeposits',   'sanic_accountnumber'),
    ('sanic_chtbldepositsrecounting', 'sanic_accountnumber'),
    # El numero del cheque declarado era SOLO-ADMIN: ni el supervisor lo veia. El
    # cajero tiene que marcar CUALES cheques declarados entran en el deposito, y
    # sin numero solo los distingue por el monto — con dos del mismo monto, no los
    # distingue. Se abre para cajero y supervisor (ver el bloque de mas abajo).
    ('sanic_chtbldeclaredchecks',     'sanic_checknumber'),
]

perfil = find('fieldsecurityprofiles', f"name eq '{PERFIL}'", 'name')
if perfil:
    pid = perfil[0]['fieldsecurityprofileid']
    print(f"perfil ya existe: {PERFIL}")
else:
    pid = req('POST', '/fieldsecurityprofiles', {
        'name': PERFIL,
        'description': ('Lectura del numero de cuenta para el cajero de recuento. Sin update ni '
                        'create. La columna CUENTA de los depositos declarados salia vacia porque '
                        'esos campos tienen seguridad a nivel de campo y el unico perfil que los '
                        'habilitaba estaba asignado al equipo aprobador.'),
    }, prefer='return=representation')['fieldsecurityprofileid']
    print(f"perfil creado: {PERFIL}")

for entidad, campo in CAMPOS:
    ya = find('fieldpermissions',
              f"_fieldsecurityprofileid_value eq {pid} and entityname eq '{entidad}' "
              f"and attributelogicalname eq '{campo}'", 'attributelogicalname')
    if ya:
        print(f"  ya estaba: {entidad}.{campo}")
        continue
    req('POST', '/fieldpermissions', {
        'entityname': entidad,
        'attributelogicalname': campo,
        'canread': 4,       # 4 = permitido
        'canupdate': 0,     # 0 = denegado. El cajero NO escribe cuentas.
        'cancreate': 0,
        'fieldsecurityprofileid@odata.bind': f"/fieldsecurityprofiles({pid})",
    })
    print(f"  lectura otorgada: {entidad}.{campo}")

cajeros = req('GET', f"/roles({ROL_CAJERO})/systemuserroles_association"
                     "?$select=fullname,systemuserid").get('value', [])
asignados = {u['systemuserid'] for u in
             req('GET', f"/fieldsecurityprofiles({pid})/systemuserprofiles_association"
                        "?$select=systemuserid").get('value', [])}
print(f"\nusuarios con el rol del cajero: {len(cajeros)}")
for u in cajeros:
    if u['systemuserid'] in asignados:
        print(f"  ya asignado: {u.get('fullname')}")
        continue
    req('POST', f"/fieldsecurityprofiles({pid})/systemuserprofiles_association/$ref",
        {'@odata.id': f"{API}/systemusers({u['systemuserid']})"})
    print(f"  asignado:    {u.get('fullname')}")

# El supervisor tiene su propio perfil; se le agrega ahi el numero de cheque para
# no mezclar audiencias en un mismo perfil.
SUP = 'CashHub | FLS | Supervisor Cuentas'
sup = find('fieldsecurityprofiles', f"name eq '{SUP}'", 'name')
if not sup:
    print(f"\nNo existe el perfil {SUP}; el supervisor queda sin numero de cheque.")
else:
    sid = sup[0]['fieldsecurityprofileid']
    ya = find('fieldpermissions',
              f"_fieldsecurityprofileid_value eq {sid} and entityname eq 'sanic_chtbldeclaredchecks' "
              "and attributelogicalname eq 'sanic_checknumber'", 'attributelogicalname')
    if ya:
        print(f"\n{SUP}: ya tenia el numero de cheque")
    else:
        req('POST', '/fieldpermissions', {
            'entityname': 'sanic_chtbldeclaredchecks',
            'attributelogicalname': 'sanic_checknumber',
            'canread': 4, 'canupdate': 0, 'cancreate': 0,
            'fieldsecurityprofileid@odata.bind': f"/fieldsecurityprofiles({sid})",
        })
        print(f"\n{SUP}: lectura del numero de cheque otorgada")

print("\nOJO: hoy se asigna usuario por usuario porque no existe un equipo de "
      "cajeros. Cuando exista, colgar el perfil del equipo y re-correr esto.")
PYEOF
