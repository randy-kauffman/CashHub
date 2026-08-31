#!/usr/bin/env bash
# =============================================================================
# Provisiona la tabla CH-Tbl-AppCatalogAccess (idempotente):
#   1. entidad  sanic_chtblappcatalogaccess (UserOwned, en la solucion CashHubCore)
#   2. columna  sanic_aadgroupid (un GUID de grupo AAD por fila)
#   3. relacion 1:N sanic_chtblappcatalog -> access (lookup sanic_appcatalog, delete cascade)
#   4. publish de la entidad
#   5. prvRead Global para los 9 roles CH-SR-* (todos los usuarios del hub leen esta tabla)
#   6. migracion: filas de acceso desde el sanic_aadgroupid multi-valor del catalogo
#      (';'-separado) y limpieza de esa columna
#
# Reemplaza el modelo "GUIDs separados por ';' en el catalogo" por N filas con
# relacion real. El hub resuelve visibilidad leyendo esta tabla.
#
# Requiere pp_secrets.env.
# Uso: ./scripts/provision-appcatalog-access.sh
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
import json, os, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
SOLUTION = 'CashHubCore'
ENTITY = 'sanic_chtblappcatalogaccess'
# Actualizado 2026-08-31. La lista anterior nombraba tres roles que ya no existen y
# habria hecho fallar la corrida:
#   - CH-SR-Counter    -> renombrado a CH-SR-Cashier (mismo GUID); estaba roto desde
#                         antes de esta limpieza
#   - CH-SR-OpsAdmin   -> eliminado; su capacidad de diagnostico paso a CH-SR-ConfigAdmin
#   - CH-SR-AnalyticsRead -> eliminado
# Esta lista refleja los 9 roles que HOY tienen prvReadsanic_CHTblAppCatalogAccess.
ROLES = ['CH-SR-Hub', 'CH-SR-Cashier', 'CH-SR-Receptionist', 'CH-SR-CountingSupervisor',
         'CH-SR-ClientAdminApprover', 'CH-SR-ClientAdminContributor',
         'CH-SR-ConfigAdmin', 'CH-SR-DepositsTracker', 'CH-SR-Digitizer']

def req(method, path, body=None, extra_headers=None, ok404=False):
    out_file = tempfile.NamedTemporaryFile('r', suffix='.out', delete=False, encoding='utf-8')
    out_file.close()
    cmd = ['curl', '-s', '-o', out_file.name, '-w', '%{http_code}',
           '-H', f"Authorization: Bearer {os.environ['TOKEN']}",
           '-H', 'Content-Type: application/json',
           '-H', 'Prefer: return=representation']
    for h in (extra_headers or []):
        cmd += ['-H', h]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    cmd += ['-X', method, API + path]
    try:
        code = int(subprocess.run(cmd, capture_output=True, text=True).stdout.strip())
        raw = open(out_file.name, encoding='utf-8').read()
    finally:
        if tmp:
            os.unlink(tmp.name)
        os.unlink(out_file.name)
    if code == 404 and ok404:
        return None
    d = json.loads(raw) if raw.strip() else {}
    if code >= 400 or 'error' in d:
        msg = d.get('error', {}).get('message', raw[:300])
        raise RuntimeError(f"{method} {path} -> {code}: {msg}")
    return d

def find(path):
    return req('GET', path).get('value', [])

SOLN = ['MSCRM.SolutionUniqueName: ' + SOLUTION]

# --- 1. entidad ---
existing = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')?$select=MetadataId", ok404=True)
if existing:
    print('entidad ya existe:', ENTITY)
else:
    req('POST', '/EntityDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.EntityMetadata',
        'SchemaName': 'sanic_CHTblAppCatalogAccess',
        'DisplayName': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
            {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'CH-Tbl-AppCatalogAccess', 'LanguageCode': 1033}]},
        'DisplayCollectionName': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
            {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'CH-Tbl-AppCatalogAccesses', 'LanguageCode': 1033}]},
        'Description': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
            {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel',
             'Label': 'Grupos AAD habilitados por app del hub (una fila = un grupo). Reemplaza el multi-valor con ; del catalogo.',
             'LanguageCode': 1033}]},
        'OwnershipType': 'UserOwned',
        'HasNotes': False,
        'HasActivities': False,
        'Attributes': [{
            '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
            'SchemaName': 'sanic_Name',
            'IsPrimaryName': True,
            'RequiredLevel': {'Value': 'None'},
            'MaxLength': 200,
            'FormatName': {'Value': 'Text'},
            'DisplayName': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
                {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'Name', 'LanguageCode': 1033}]},
        }],
    }, extra_headers=SOLN)
    print('entidad creada:', ENTITY)

# --- 2. columna sanic_aadgroupid ---
attr = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='sanic_aadgroupid')?$select=MetadataId", ok404=True)
if attr:
    print('columna ya existe: sanic_aadgroupid')
else:
    req('POST', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
        'SchemaName': 'sanic_AADGroupId',
        'RequiredLevel': {'Value': 'ApplicationRequired'},
        'MaxLength': 100,
        'FormatName': {'Value': 'Text'},
        'DisplayName': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
            {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'AAD Group Id', 'LanguageCode': 1033}]},
        'Description': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
            {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'GUID del grupo de Azure AD habilitado para la app.', 'LanguageCode': 1033}]},
    }, extra_headers=SOLN)
    print('columna creada: sanic_aadgroupid')

# --- 3. relacion 1:N con lookup ---
rel = req('GET', "/RelationshipDefinitions(SchemaName='sanic_CHTblAppCatalog_CHTblAppCatalogAccess')?$select=MetadataId", ok404=True)
if rel:
    print('relacion ya existe')
else:
    req('POST', '/RelationshipDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata',
        'SchemaName': 'sanic_CHTblAppCatalog_CHTblAppCatalogAccess',
        'ReferencedEntity': 'sanic_chtblappcatalog',
        'ReferencingEntity': ENTITY,
        'CascadeConfiguration': {
            'Assign': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade',
            'Reparent': 'NoCascade', 'Delete': 'Cascade', 'Merge': 'NoCascade',
        },
        'Lookup': {
            'AttributeType': 'Lookup',
            'AttributeTypeName': {'Value': 'LookupType'},
            'SchemaName': 'sanic_AppCatalog',
            'RequiredLevel': {'Value': 'ApplicationRequired'},
            'DisplayName': {'@odata.type': 'Microsoft.Dynamics.CRM.Label', 'LocalizedLabels': [
                {'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', 'Label': 'App Catalog', 'LanguageCode': 1033}]},
        },
    }, extra_headers=SOLN)
    print('relacion creada: sanic_CHTblAppCatalog_CHTblAppCatalogAccess')

# --- 4. publish ---
req('POST', '/PublishXml', {
    'ParameterXml': f'<importexportxml><entities><entity>{ENTITY}</entity></entities></importexportxml>'})
print('publish OK')

# --- 5. prvRead Global para los roles CH ---
priv_rows = find("/privileges?$select=privilegeid&$filter=" + urllib.parse.quote("name eq 'prvReadsanic_CHTblAppCatalogAccess'"))
if not priv_rows:
    raise RuntimeError('No aparece prvReadsanic_CHTblAppCatalogAccess (publish pendiente?)')
priv_id = priv_rows[0]['privilegeid']
for role_name in ROLES:
    role_rows = find("/roles?$select=roleid&$filter=" + urllib.parse.quote(f"name eq '{role_name}' and _parentroleid_value eq null"))
    if not role_rows:
        print(f'  AVISO: rol no encontrado: {role_name}')
        continue
    role_id = role_rows[0]['roleid']
    has = find(f"/roles({role_id})/roleprivileges_association?$select=privilegeid&$filter=" + urllib.parse.quote(f"privilegeid eq {priv_id}"))
    if has:
        print(f'  rol ya tiene lectura: {role_name}')
        continue
    req('POST', f"/roles({role_id})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {
        'Privileges': [{'PrivilegeId': priv_id, 'Depth': 'Global'}]})
    print(f'  lectura otorgada: {role_name}')

# --- 6. migracion de datos: multi-valor ';' del catalogo -> filas de acceso ---
catalog = find('/sanic_chtblappcatalogs?$select=sanic_appkey,sanic_aadgroupid')
for app in catalog:
    raw = (app.get('sanic_aadgroupid') or '').strip()
    if not raw:
        continue
    app_id = app['sanic_chtblappcatalogid']
    for gid in [t.strip() for t in raw.split(';') if t.strip()]:
        dup = find(f"/sanic_chtblappcatalogaccesses?$select=sanic_chtblappcatalogaccessid&$filter=" +
                   urllib.parse.quote(f"_sanic_appcatalog_value eq {app_id} and sanic_aadgroupid eq '{gid}'"))
        if dup:
            print(f"  acceso ya existe: {app['sanic_appkey']} -> {gid}")
            continue
        req('POST', '/sanic_chtblappcatalogaccesses', {
            'sanic_name': f"{app['sanic_appkey']} - {gid}",
            'sanic_aadgroupid': gid,
            'sanic_AppCatalog@odata.bind': f"/sanic_chtblappcatalogs({app_id})",
        })
        print(f"  acceso creado: {app['sanic_appkey']} -> {gid}")
    req('PATCH', f"/sanic_chtblappcatalogs({app_id})", {'sanic_aadgroupid': None})
    print(f"  catalogo limpiado: {app['sanic_appkey']} (aadgroupid -> null)")

total = find('/sanic_chtblappcatalogaccesses?$select=sanic_name')
print(f"Provision completa. Filas de acceso: {len(total)}")
PYEOF
