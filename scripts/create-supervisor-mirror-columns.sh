#!/usr/bin/env bash
# =============================================================================
# Crea el esquema de los espejos de CH-App-Supervisor v2 (B-2, B-3, B-6, B-9),
# idempotente:
#   B-2  lookup sanic_ReceptionBatchId  (-> sanic_chtblreceptionbatch) en sanic_chtblbags
#   B-3  texto  sanic_ReceiptIdMirror   (100) en sanic_chtblbags
#   B-6  texto  sanic_BagNumber         (100) en sanic_chtblapprovalrequest
#   B-9  lookup sanic_RecountingSession (-> sanic_chtblrecountingsession) en sanic_chtblchecks
#
# Los cuatro son DERIVADOS: los mantienen BagReceptionMirrorPlugin,
# ApprovalRequestBagNumberPlugin y CheckMirrorStampPlugin. Por eso llevan la
# descripcion "Mantenido por plugin. No editar a mano." y NUNCA se editan desde
# una app.
#
# Existen porque el dot-walk de DOS saltos (tula -> planilla -> lote/recibo;
# cheque -> tula -> sesion) y el numero de tula escondido dentro del JSON de
# RequestResources NO delegan: Power Fx se trae el tope de filas, filtra en el
# cliente y deja registros afuera SIN error.
# Ver docs/supervisor-rebuild/00-analisis-y-plan.md §2 (B-2/B-3/B-6/B-9) y L-18/L-19.
#
# B-9 se puede crear y backfillear HOY sin desplegar el assembly: la columna mas
# scripts/backfill-supervisor-mirrors.py alcanzan para que scrResumen cuente con
# CountRows DELEGADO sobre los datos que ya existen.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
#
# Requiere pp_secrets.env.  Uso: ./scripts/create-supervisor-mirror-columns.sh
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
    })
    print(f"  creada: {entity}.{logical} ({display}, nvarchar {max_length})")

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
        # RemoveLink: si se borra el lote, el espejo se limpia solo — nunca
        # arrastra la tula (el vinculo REAL de la tula es con la planilla).
        'CascadeConfiguration': {
            'Assign': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade',
            'Reparent': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade',
        },
    })
    print(f"  creada: relacion {rel_schema} (lookup {lookup_schema.lower()})")

print("== B-2: Tula -> lote de recepcion (espejo del dot-walk de 2 saltos)")
create_lookup('sanic_chtblbags_ReceptionBatch_sanic_chtblreceptionbatch',
    'sanic_chtblreceptionbatch', 'sanic_chtblbags',
    'sanic_ReceptionBatchId', 'ReceptionBatchId',
    'Espejo del lote de la planilla de la tula (B-2). ' + NO_EDIT)

print("== B-3: Tula -> numero de recibo de su planilla")
create_string('sanic_chtblbags', 'sanic_ReceiptIdMirror', 'ReceiptIdMirror', 100,
    'Espejo del ReceiptID de la planilla de la tula (B-3), para buscar por recibo '
    'con delegacion. ' + NO_EDIT)

print("== B-6: Solicitud de aprobacion -> numero de tula")
create_string('sanic_chtblapprovalrequest', 'sanic_BagNumber', 'BagNumber', 100,
    'Numero de tula extraido del JSON de RequestResources al crear la solicitud '
    '(B-6), para buscar por tula en la bandeja con delegacion. ' + NO_EDIT)

print("== B-9: Cheque -> sesion de recuento (espejo del dot-walk de 2 saltos)")
# Sin esta columna, contar cheques por sesion obliga a Cheque -> Tula -> Sesion:
# NO delega, Power Fx materializa hasta el tope de filas del cliente (500/2000) y
# el conteo miente por omision SIN error. QA lo probo con 2.074 cheques.
create_lookup('sanic_chtblchecks_RecountingSession_sanic_chtblrecountingsession',
    'sanic_chtblrecountingsession', 'sanic_chtblchecks',
    'sanic_RecountingSession', 'RecountingSession',
    'Espejo de la sesion de recuento de la tula del cheque (B-9), para contar '
    'cheques por sesion con CountRows delegado. ' + NO_EDIT)

print("== Publicando entidades tocadas")
entities = ['sanic_chtblbags', 'sanic_chtblapprovalrequest', 'sanic_chtblchecks']
xml = '<importexportxml><entities>' + ''.join(f'<entity>{e}</entity>' for e in entities) + '</entities></importexportxml>'
req('POST', '/PublishXml', {'ParameterXml': xml})
print("Esquema de los espejos del supervisor completo.")
PYEOF
