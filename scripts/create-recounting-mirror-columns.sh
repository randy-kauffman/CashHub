#!/usr/bin/env bash
# =============================================================================
# Crea el esquema de los espejos server-side de CH-App-Recounting v2
# (docs/recounting-rebuild/01-app.md §2.2), idempotente:
#
#   E-1  lookup  sanic_RecountingSessionMirror       (-> sanic_chtblrecountingsession)
#                en sanic_chtbldepositsrecounting
#   E-3  lookup  sanic_DeclaredBagMirror             (-> sanic_chtbldeclaredbags)
#                en sanic_chtbldeclaredchecks
#   E-4  lookup  sanic_RecountingSessionDetailMirror (-> sanic_chtblrecountingsessiondetail)
#        + bit   sanic_XMLReadingActiva
#                en sanic_chtblxmlreadingdetail
#
#   E-2 (sanic_bag en sanic_chtblchecks) NO se crea aca: YA EXISTE y la mantiene
#   CheckMirrorStampPlugin (espejo B6 del tracker, creada por
#   create-tracker-columns.sh). Ver ledger L-21.
#
# Los cuatro son DERIVADOS: los mantienen DepositSessionMirrorPlugin,
# DeclaredCheckBagMirrorPlugin y XmlReadingDetailMirrorPlugin. Por eso llevan la
# descripcion "Mantenido por plugin. No editar a mano." y la app NUNCA los
# escribe: solo los lee.
#
# Por que existen (los tres bugs que matan):
#   D-2  los 12 Sum(Filter(..., BagID.RecountingSession...)) del rail de scrTulas
#        son dot-walk de DOS saltos: NO delegan, Power Fx trunca en el tope del
#        cliente (500/2000) y el KPI muestra plata PARCIAL sin dar error.
#   E-3  la apertura de tula hacia un Filter remoto POR DEPOSITO declarado
#        (~82 round-trips medidos). Con el espejo es UNA query delegable.
#   D-1  "Leer contadora" escribia Filter(details, XMLReading.Id in Filter(...)):
#        un "in" con subconsulta que JAMAS delega; con >500 filas historicas la
#        columna Auto mostraba 0 y el cajero recontaba contra totales falsos.
#
# E-4 son DOS columnas a proposito (divergencia L-3): Q17 necesita la
# sesion-detalle Y que la lectura este activa. Con una sola habria que volver a
# CH-Tbl-XMLReadings a preguntar IsActive y vuelve el segundo salto que E-4 vino
# a matar.
#
# Los tres se pueden crear y backfillear HOY sin desplegar el assembly: la
# columna mas scripts/backfill-recounting-mirrors.py alcanzan para que las
# consultas deleguen sobre los datos que ya existen. Lo que queda pendiente hasta
# el deploy del plugin es SOLO el mantenimiento en vivo.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
#
# Requiere pp_secrets.env.  Uso: ./scripts/create-recounting-mirror-columns.sh
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

def create_boolean(entity, schema, display, description):
    logical = schema.lower()
    if attr_exists(entity, logical):
        print(f"  ya existe: {entity}.{logical}"); return
    req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata',
        'SchemaName': schema,
        'DisplayName': label(display),
        'Description': label(description),
        'RequiredLevel': {'Value': 'None'},
        # Default false: un detalle cuyo espejo todavia no se estampo NO puede
        # entrar a Q17 como si su lectura estuviera vigente.
        'DefaultValue': False,
        'OptionSet': {
            '@odata.type': 'Microsoft.Dynamics.CRM.BooleanOptionSetMetadata',
            'TrueOption': {'Value': 1, 'Label': label('Si')},
            'FalseOption': {'Value': 0, 'Label': label('No')},
        },
    })
    print(f"  creada: {entity}.{logical} ({display}, bit)")

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
        # RemoveLink: si se borra el registro referenciado, el espejo se limpia
        # solo — un espejo NUNCA arrastra al hijo (el vinculo REAL vive en otra
        # columna: sanic_bagid, sanic_declareddepositid, sanic_xmlreading).
        'CascadeConfiguration': {
            'Assign': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade',
            'Reparent': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade',
        },
    })
    print(f"  creada: relacion {rel_schema} (lookup {lookup_schema.lower()})")

print("== E-1: Deposito de recuento -> sesion (espejo del dot-walk de 2 saltos)")
create_lookup('sanic_chtbldepositsrecounting_RecountingSessionMirror_sanic_chtblrecountingsession',
    'sanic_chtblrecountingsession', 'sanic_chtbldepositsrecounting',
    'sanic_RecountingSessionMirror', 'RecountingSessionMirror',
    'Espejo de la sesion de recuento de la tula del deposito (E-1), para que los '
    'Sum de dinero por sesion deleguen. ' + NO_EDIT)

print("== E-3: Cheque declarado -> bolsa declarada (espejo del dot-walk de 2 saltos)")
create_lookup('sanic_chtbldeclaredchecks_DeclaredBagMirror_sanic_chtbldeclaredbags',
    'sanic_chtbldeclaredbags', 'sanic_chtbldeclaredchecks',
    'sanic_DeclaredBagMirror', 'DeclaredBagMirror',
    'Espejo de la bolsa declarada del deposito declarado del cheque (E-3), para '
    'traer los cheques declarados de la tula en UNA query delegable. ' + NO_EDIT)

print("== E-4a: Detalle de lectura -> sesion-detalle")
create_lookup('sanic_chtblxmlreadingdetail_RecountingSessionDetailMirror_sanic_chtblrecountingsessiondetail',
    'sanic_chtblrecountingsessiondetail', 'sanic_chtblxmlreadingdetail',
    'sanic_RecountingSessionDetailMirror', 'RecountingSessionDetailMirror',
    'Espejo de la sesion-detalle de la lectura del detalle (E-4), para que "Leer '
    'contadora" sea un Filter delegable de UN salto. ' + NO_EDIT)

print("== E-4b: Detalle de lectura -> lectura vigente (bit)")
# Segunda condicion de Q17. Sin ella habria que volver a CH-Tbl-XMLReadings a
# preguntar IsActive: vuelve el segundo salto que E-4 vino a matar (L-3).
create_boolean('sanic_chtblxmlreadingdetail', 'sanic_XMLReadingActiva', 'XMLReadingActiva',
    'Espejo de XMLReadings.IsActive en el detalle (E-4). Segunda condicion de la '
    'consulta de la contadora; se repropaga cuando la lectura padre se apaga. ' + NO_EDIT)

print("== Publicando entidades tocadas")
entities = ['sanic_chtbldepositsrecounting', 'sanic_chtbldeclaredchecks',
            'sanic_chtblxmlreadingdetail']
xml = '<importexportxml><entities>' + ''.join(f'<entity>{e}</entity>' for e in entities) + '</entities></importexportxml>'
req('POST', '/PublishXml', {'ParameterXml': xml})
print("Esquema de los espejos de recounting completo.")
PYEOF
