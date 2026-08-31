#!/usr/bin/env bash
# =============================================================================
# Da a la CONSOLA DE INGESTA permiso sobre las columnas de CH-Tbl-Received Emails
# que tienen Field Level Security y que la app necesita.
#
# POR QUE HACE FALTA. sanic_subject y sanic_sourceemailaddress tienen IsSecured=1 y
# el UNICO perfil con permiso sobre ellas era System Administrator. Dataverse no
# falla al leerlas sin permiso: las ENMASCARA y devuelve null. Resultado en la
# consola: la galeria dibujaba las filas con la fecha y la pildora pero con el
# asunto y el remitente en blanco, y el buscador —que busca justamente en esas dos
# columnas— no encontraba nada nunca. Silencioso, no explota.
#
# Esto ya estaba anotado como pendiente en provision-email-ingestion-roles.sh:
# "va a necesitar ademas un Field Security Profile para leerlo, una vez que
# sepamos que team/usuarios lo usan". Ya lo sabemos: los del rol de la consola.
#
#   perfil : CashHub | FLS | Consola Ingesta
#     sanic_subject             read=SI update=NO create=NO
#     sanic_sourceemailaddress  read=SI update=NO create=NO
#     sanic_repliedby           read=SI update=SI create=NO
#
# POR QUE repliedby LLEVA UPDATE Y LAS OTRAS DOS NO. "Omitir respuesta" graba
# RepliedBy con el correo de quien apreto el boton, para que el correo omitido no
# quede sin dueño —hoy 0 de 25 tienen ese dato—. Sin update, Dataverse RECHAZA el
# Patch entero y el boton deja de funcionar. El asunto y el remitente los escribe
# la ingesta, no el operador: van solo lectura a proposito.
#
# ES UNA AMPLIACION DE ALCANCE: alguien aseguro esas columnas deliberadamente.
# Se otorga lo minimo que la app usa y nada mas; las otras tres aseguradas
# (replysubject, overrideby, attachment) NO se tocan porque la app no las lee.
#
# Idempotente: crea lo que falte y reasigna sin duplicar. Toma a los usuarios con
# el rol CH-SR-EmailOperationsConsole (directo o por equipo), asi que al sumar un
# operador nuevo basta con volver a correrlo.
#
# Uso: ./scripts/grant-console-email-fields.sh
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

PROFILE = 'CashHub | FLS | Consola Ingesta'
ENTITY = 'sanic_chtblreceivedemails'
ROLE = 'CH-SR-EmailOperationsConsole'

ALLOWED, NOT_ALLOWED = 4, 0

# (columna, update)  -- read siempre SI, create siempre NO
CAMPOS = [
    ('sanic_subject', NOT_ALLOWED),
    ('sanic_sourceemailaddress', NOT_ALLOWED),
    ('sanic_repliedby', ALLOWED),
]


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
    req('POST', "/fieldsecurityprofiles", {
        'name': PROFILE,
        'description': ('Lectura del asunto y el remitente de los correos recibidos, para que la '
                        'consola de ingesta pueda mostrarlos y buscarlos. RepliedBy lleva ademas '
                        'update porque "Omitir respuesta" graba quien omitio el correo.'),
    }, ok=(200, 201, 204))
    profile_id = find("/fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter="
                      + urllib.parse.quote(f"name eq '{PROFILE}'"))[0]['fieldsecurityprofileid']
    print(f"  perfil creado: {PROFILE}")

# 2. Permisos por columna
for attr, update in CAMPOS:
    perms = find("/fieldpermissions?$select=fieldpermissionid&$filter="
                 + urllib.parse.quote(
                     f"_fieldsecurityprofileid_value eq {profile_id} and "
                     f"entityname eq '{ENTITY}' and attributelogicalname eq '{attr}'"))
    body = {'canread': ALLOWED, 'canupdate': update, 'cancreate': NOT_ALLOWED}
    etiqueta = f"read=SI update={'SI' if update else 'NO'} create=NO"
    if perms:
        req('PATCH', f"/fieldpermissions({perms[0]['fieldpermissionid']})", body)
        print(f"  permiso actualizado: {attr}  {etiqueta}")
    else:
        req('POST', "/fieldpermissions", dict(body, **{
            'entityname': ENTITY,
            'attributelogicalname': attr,
            'fieldsecurityprofileid@odata.bind': f"/fieldsecurityprofiles({profile_id})",
        }))
        print(f"  permiso creado: {attr}  {etiqueta}")

# 3. Operadores: los que tienen el rol directo y los de los equipos que lo tienen.
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

print(f"Listo: {len(usuarios)} operador(es) pueden LEER el asunto y el remitente.")
PYEOF
