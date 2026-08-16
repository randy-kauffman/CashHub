#!/usr/bin/env bash
# =============================================================================
# Crea el espejo de nombre de cliente en la sucursal (CH-App-Reception v2),
# idempotente:
#   texto sanic_ClientNameMirror (850) en sanic_chtblclientbranch
#
# Es DERIVADO: lo mantiene ClientBranchNameMirrorPlugin. Por eso lleva la
# descripcion "Mantenido por plugin. No editar a mano." y NUNCA se edita desde
# una app.
#
# Existe porque el combo de sucursales de la v2 tiene que MOSTRAR y BUSCAR por
# nombre de CLIENTE, y hacerlo por la relacion (sanic_Client.sanic_name) NO
# delega: Power Fx se trae el tope de filas (500/2000), filtra en el cliente y
# deja sucursales afuera SIN error. Con ~50.000 sucursales el combo miente por
# omision.
#
# OJO — NO confundir con sanic_clientname, que YA existe en la tabla: es una
# columna huerfana (IsValidForCreate=false, IsValidForUpdate=false,
# IsFilterable=false, IsValidForAdvancedFind=false, sin display name) que ni
# siquiera se puede pedir por $select en el Web API. No sirve ni para escribir
# ni para buscar; por eso el espejo va en una columna nueva.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
#
# Requiere pp_secrets.env. Uso: ./scripts/create-clientbranch-mirror-column.sh
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
import json, os, subprocess, tempfile

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'MSCRM.SolutionUniqueName: CashHubCore']
NO_EDIT = "Mantenido por plugin. No editar a mano."

def req(method, path, body=None, ok404=False):
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *H, '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        raw = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    payload, _, code = raw.rpartition('\n')
    code = int(code)
    if code == 404 and ok404: return None
    d = json.loads(payload) if payload.strip() else {}
    if code >= 400:
        raise RuntimeError(f"{method} {path} -> {code}: {json.dumps(d)[:400]}")
    return d

def label(text):
    return {'LocalizedLabels': [{'Label': text, 'LanguageCode': 1033}]}

def attr_exists(entity, logical):
    return req('GET',
        f"/EntityDefinitions(LogicalName='{entity}')/Attributes(LogicalName='{logical}')?$select=LogicalName",
        ok404=True) is not None

def create_string(entity, schema, display, max_length, description):
    logical = schema.lower()
    if attr_exists(entity, logical):
        print(f"  ya existe: {entity}.{logical}"); return
    req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
        'SchemaName': schema,
        'DisplayName': label(display),
        'Description': label(description),
        'RequiredLevel': {'Value': 'None'},
        'MaxLength': max_length,
        'FormatName': {'Value': 'Text'},
        # Buscable/filtrable de verdad: es la razon de ser de la columna.
        'IsValidForAdvancedFind': {'Value': True, 'CanBeChanged': True,
                                   'ManagedPropertyLogicalName': 'canmodifysearchsettings'},
    })
    print(f"  creada: {entity}.{logical} ({display}, nvarchar {max_length})")

print("== Espejo de nombre de cliente en la sucursal (CH-App-Reception v2)")
# 850 = el MISMO largo que sanic_chtblclient.sanic_name: el espejo nunca recorta.
create_string('sanic_chtblclientbranch', 'sanic_ClientNameMirror', 'ClientNameMirror', 850,
    'Espejo del nombre del cliente de la sucursal, para mostrar y buscar por '
    'cliente en el combo de sucursales con StartsWith DELEGADO (sin navegar la '
    'relacion). ' + NO_EDIT)

print("== Publicando la entidad")
xml = ('<importexportxml><entities><entity>sanic_chtblclientbranch</entity>'
       '</entities></importexportxml>')
req('POST', '/PublishXml', {'ParameterXml': xml})
print("Esquema del espejo de sucursal completo.")
PYEOF
