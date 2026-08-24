#!/usr/bin/env bash
# =============================================================================
# Crea el esquema de las columnas nuevas de CH-App-Digitization v2
# (docs/digitization-rebuild/06-backend.md), idempotente:
#
#   sanic_MotivoRechazoCore  memo (multilinea, 2000)  en sanic_chtbltransactions
#        Motivo por el que el Core bancario rechazo la transaccion y se la saco
#        del lote (regla "sacar del lote" -> Manual). La ESCRIBE server-side el
#        Custom API sanic_CHRemoveFromCut; queda visible en el historial de la
#        transaccion (scrLotes timeline, scrManual > Aplicadas). NO es un espejo:
#        es dato de negocio persistido una vez, no derivado.
#
#   sanic_SentToDigitizationBy  lookup (-> systemuser)  en sanic_chtblchecks
#        Quien envio el cheque a digitacion (el Supervisor). La estampa
#        CheckStatusStampPlugin con InitiatingUserId al pasar a 'Enviado a
#        digitacion', junto al sello de fecha sanic_senttodigitizationon que ya
#        existe. Derivada del actor -> "No editar a mano."
#
#   sanic_ReceivedBy  lookup (-> systemuser)  en sanic_chtblchecks
#        Quien acuso recibo del cheque (el Digitador). La estampa
#        CheckStatusStampPlugin con InitiatingUserId al pasar a Recibido / No
#        Recibido, junto a los sellos sanic_receivedon / sanic_notreceivedon.
#        Derivada del actor -> "No editar a mano."
#
# Por que existen: el esquema de cheques solo tenia las FECHAS del handshake
# (senttodigitizationon / receivedon / notreceivedon), no el ACTOR. scrCheques v2
# muestra "Enviado por" y "Recibido por" (trazabilidad pedida por el usuario); sin
# estas columnas habria que caer al fallback honesto Created By / Modified By, que
# no distingue enviar de recibir.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
# Las columnas se pueden crear HOY sin desplegar el assembly; el mantenimiento en
# vivo de los dos lookups queda pendiente hasta el deploy de CheckStatusStampPlugin
# extendido (ver 06-backend.md) + backfill opcional por auditoria.
#
# Requiere pp_secrets.env.  Uso: ./scripts/create-digitization-columns.sh
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

def create_memo(entity, schema, display, description, max_length=2000):
    logical = schema.lower()
    if attr_exists(entity, logical):
        print(f"  ya existe: {entity}.{logical}"); return
    req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.MemoAttributeMetadata',
        'SchemaName': schema,
        'DisplayName': label(display),
        'Description': label(description),
        'RequiredLevel': {'Value': 'None'},
        'MaxLength': max_length,
        'Format': 'Text',
    })
    print(f"  creada: {entity}.{logical} ({display}, memo {max_length})")

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
        # RemoveLink: si se borra el usuario referenciado, el sello de autoria se
        # limpia solo. Un lookup de autoria NUNCA arrastra al systemuser.
        'CascadeConfiguration': {
            'Assign': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade',
            'Reparent': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade',
        },
    })
    print(f"  creada: relacion {rel_schema} (lookup {lookup_schema.lower()})")

print("== Motivo del rechazo del Core (dato de negocio, escrito por sanic_CHRemoveFromCut)")
create_memo('sanic_chtbltransactions', 'sanic_MotivoRechazoCore', 'MotivoRechazoCore',
    'Motivo por el que el Core rechazo la transaccion al sacarla del lote y '
    'devolverla al circuito Manual. Lo escribe el Custom API sanic_CHRemoveFromCut; '
    'queda visible en el historial de la transaccion.')

print("== Autoria del handshake de cheques (estampada por CheckStatusStampPlugin)")
create_lookup('sanic_chtblchecks_SentToDigitizationBy_systemuser',
    'systemuser', 'sanic_chtblchecks',
    'sanic_SentToDigitizationBy', 'SentToDigitizationBy',
    'Usuario que envio el cheque a digitacion (el Supervisor). Estampado al pasar '
    "a 'Enviado a digitacion', junto al sello de fecha sanic_senttodigitizationon. " + NO_EDIT)

create_lookup('sanic_chtblchecks_ReceivedBy_systemuser',
    'systemuser', 'sanic_chtblchecks',
    'sanic_ReceivedBy', 'ReceivedBy',
    'Usuario que acuso recibo del cheque (el Digitador). Estampado al pasar a '
    'Recibido / No Recibido, junto a los sellos sanic_receivedon / sanic_notreceivedon. ' + NO_EDIT)

print("== Publicando entidades tocadas")
entities = ['sanic_chtbltransactions', 'sanic_chtblchecks']
xml = '<importexportxml><entities>' + ''.join(f'<entity>{e}</entity>' for e in entities) + '</entities></importexportxml>'
req('POST', '/PublishXml', {'ParameterXml': xml})
print("Esquema de columnas de digitization v2 completo.")
PYEOF
