#!/usr/bin/env bash
# =============================================================================
# Crea el esquema del paquete backend del tracker (B1-B7), idempotente:
#   B1  lookup sanic_receivedemail (ReceivedEmail) en sanic_chtbldeclaration
#   B2  valor 159460002 "Omitida" en sanic_validationresult (ValidationResults)
#   B3  lookup sanic_matchedcheck (MatchedCheck) en sanic_chtbldeclaredchecks
#   B4  6 decimales DecNIO*/DecUSD* en sanic_chtblreceptionbatch
#   B5  3 decimales DeclTotal* + bit HasDifference en sanic_chtblbags
#   B6  lookup sanic_bag (Bag) en sanic_chtblchecks
#   B7  lookup sanic_declaration (Declaration) en declareddeposits y declaredchecks
#
# Todos los espejos llevan descripcion "Mantenido por plugin. No editar a mano."
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
#
# Requiere pp_secrets.env.  Uso: ./scripts/create-tracker-columns.sh
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

def create_decimal(entity, schema, display):
    logical = schema.lower()
    if attr_exists(entity, logical):
        print(f"  ya existe: {entity}.{logical}"); return
    req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.DecimalAttributeMetadata',
        'SchemaName': schema,
        'DisplayName': label(display),
        'Description': label(NO_EDIT),
        'RequiredLevel': {'Value': 'None'},
        'Precision': 2,
        'MinValue': -100000000000.0, 'MaxValue': 100000000000.0,
    })
    print(f"  creada: {entity}.{logical} ({display})")

def create_boolean(entity, schema, display):
    logical = schema.lower()
    if attr_exists(entity, logical):
        print(f"  ya existe: {entity}.{logical}"); return
    req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata',
        'SchemaName': schema,
        'DisplayName': label(display),
        'Description': label(NO_EDIT),
        'RequiredLevel': {'Value': 'None'},
        'DefaultValue': False,
        'OptionSet': {
            '@odata.type': 'Microsoft.Dynamics.CRM.BooleanOptionSetMetadata',
            'TrueOption': {'Value': 1, 'Label': label('Si')},
            'FalseOption': {'Value': 0, 'Label': label('No')},
        },
    })
    print(f"  creada: {entity}.{logical} ({display})")

def create_lookup(rel_schema, referenced, referencing, lookup_schema, display, description):
    if req('GET', f"/RelationshipDefinitions(SchemaName='{rel_schema}')?$select=SchemaName", ok404=True):
        print(f"  ya existe: relacion {rel_schema}"); return
    req('POST', '/RelationshipDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata',
        'SchemaName': rel_schema,
        'ReferencedEntity': referenced,
        'ReferencingEntity': referencing,
        'Lookup': {
            'SchemaName': lookup_schema,
            'DisplayName': label(display),
            'Description': label(description),
            'RequiredLevel': {'Value': 'None'},
        },
        'AssociatedMenuConfiguration': {'Behavior': 'DoNotDisplay'},
        'CascadeConfiguration': {
            'Assign': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade',
            'Reparent': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade',
        },
    })
    print(f"  creada: relacion {rel_schema} (lookup {lookup_schema.lower()})")

print("== B4: totales declarados del lote (sanic_chtblreceptionbatch)")
for schema, display in [
    ('sanic_DecNIOCash', 'DecNIOCash'), ('sanic_DecNIOCheck', 'DecNIOCheck'),
    ('sanic_DecNIOTotal', 'DecNIOTotal'), ('sanic_DecUSDCash', 'DecUSDCash'),
    ('sanic_DecUSDCheck', 'DecUSDCheck'), ('sanic_DecUSDTotal', 'DecUSDTotal'),
]:
    create_decimal('sanic_chtblreceptionbatch', schema, display)

print("== B5: conciliacion materializada de la tula (sanic_chtblbags)")
for schema, display in [
    ('sanic_DeclTotalNIO', 'DeclTotalNIO'), ('sanic_DeclTotalUSD', 'DeclTotalUSD'),
    ('sanic_DeclTotalEUR', 'DeclTotalEUR'),
]:
    create_decimal('sanic_chtblbags', schema, display)
create_boolean('sanic_chtblbags', 'sanic_HasDifference', 'HasDifference')

print("== B1: Declaracion -> Correo")
create_lookup('sanic_CHTblReceivedEmails_Declarations',
    'sanic_chtblreceivedemails', 'sanic_chtbldeclaration',
    'sanic_ReceivedEmail', 'ReceivedEmail',
    'Vinculo real al correo de origen (B1). SourceRecordId queda por compatibilidad; '
    'este lookup existe porque un GUID en texto no delega.')

print("== B3: DeclaredCheck -> cheque fisico (espejo inverso)")
create_lookup('sanic_chtbldeclaredchecks_MatchedCheck_sanic_chtblchecks',
    'sanic_chtblchecks', 'sanic_chtbldeclaredchecks',
    'sanic_MatchedCheck', 'MatchedCheck', NO_EDIT)

print("== B6: Cheque -> tula")
create_lookup('sanic_chtblchecks_Bag_sanic_chtblbags',
    'sanic_chtblbags', 'sanic_chtblchecks',
    'sanic_Bag', 'Bag', NO_EDIT)

print("== B7: DeclaredDeposits/DeclaredChecks -> Declaracion raiz")
create_lookup('sanic_chtbldeclareddeposits_Declaration_sanic_chtbldeclaration',
    'sanic_chtbldeclaration', 'sanic_chtbldeclareddeposits',
    'sanic_Declaration', 'Declaration',
    'Denormalizacion a la declaracion raiz (B7), estampada por el materializador.')
create_lookup('sanic_chtbldeclaredchecks_Declaration_sanic_chtbldeclaration',
    'sanic_chtbldeclaration', 'sanic_chtbldeclaredchecks',
    'sanic_Declaration', 'Declaration',
    'Denormalizacion a la declaracion raiz (B7), estampada por el materializador.')

print("== B2: valor Omitida en sanic_validationresult")
opts = req('GET',
    "/EntityDefinitions(LogicalName='sanic_chtblvalidationresults')"
    "/Attributes(LogicalName='sanic_validationresult')"
    "/Microsoft.Dynamics.CRM.PicklistAttributeMetadata"
    "?$select=LogicalName&$expand=OptionSet($select=Options)")
values = [o['Value'] for o in opts['OptionSet']['Options']]
if 159460002 in values:
    print("  ya existe: 159460002 Omitida")
else:
    req('POST', '/InsertOptionValue', {
        'AttributeLogicalName': 'sanic_validationresult',
        'EntityLogicalName': 'sanic_chtblvalidationresults',
        'Value': 159460002,
        'Label': label('Omitida'),
        'SolutionUniqueName': 'CashHubCore',
    })
    print("  agregado: 159460002 Omitida")

print("== Publicando entidades tocadas")
entities = ['sanic_chtblreceptionbatch', 'sanic_chtblbags', 'sanic_chtbldeclaration',
            'sanic_chtbldeclaredchecks', 'sanic_chtblchecks', 'sanic_chtbldeclareddeposits',
            'sanic_chtblvalidationresults']
xml = '<importexportxml><entities>' + ''.join(f'<entity>{e}</entity>' for e in entities) + '</entities></importexportxml>'
req('POST', '/PublishXml', {'ParameterXml': xml})
print("Esquema del tracker completo.")
PYEOF
