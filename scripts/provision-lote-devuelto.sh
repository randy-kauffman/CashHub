#!/usr/bin/env bash
# =============================================================================
# Provisiona el estado DEVUELTO del lote de transacciones (idempotente):
#   1. opcion 159460004 'Devuelto' en sanic_transactionbatches_sanic_batchstatus
#   2. columnas de la devolucion en CH-Tbl-TransactionBatches:
#        sanic_motivodevolucion  (Memo, 2000)  — POR QUE lo devolvio el IBS
#        sanic_devueltopor       (Lookup user) — quien lo registro
#        sanic_devueltoon        (DateTime)    — cuando
#
# POR QUE EXISTE. El aprobador revisa la carga en el IBS Branch y, si algo esta
# mal, la DEVUELVE. Ese camino no estaba reflejado: el lote quedaba atrapado en
# 'Ingresado' —un estado que afirma "ya entro al Core"— cuando en realidad no
# entro nada y hay que corregirlo. La aplicacion mentia sobre donde estaba la
# plata.
#
# Es el PRIMER retroceso legal del ciclo del lote, que hasta ahora era
# estrictamente hacia adelante. La maquina queda:
#
#   Generado -> Ingresado -> Aplicado
#                  |  ^
#                  v  |
#               Devuelto        (se corrige y se vuelve a presentar al IBS)
#
# Reglas que lo defienden (TransactionGuard):
#   - Solo se entra desde Ingresado. Un lote que nunca se cargo no puede volver.
#   - Solo se sale hacia Ingresado. NO hay atajo a Aplicado: si lo hubiera, se
#     marcaria como cargado algo que el Core rechazo y nadie volvio a presentar.
#   - SEGREGACION: quien marco 'Ingresado' no puede registrar el veredicto de la
#     revision, ni Aplicado ni Devuelto. Aplicar y devolver son la misma decision
#     del aprobador con distinto signo.
#   - MOTIVO OBLIGATORIO: quien tenga que corregir necesita saber que rechazo el
#     IBS. Una devolucion sin motivo dice "esta mal" y nada mas.
#   - Se pueden SACAR transacciones del lote SOLO en Generado y Devuelto. En
#     Ingresado NO: el lote esta en manos del aprobador en el IBS, y sacarle una
#     transaccion por atras dejaria el archivo que el tiene en pantalla diciendo
#     una cosa y el lote de CashHub diciendo otra.
#   - Los montos siguen congelados en Devuelto: el lote ya se mando al Core una
#     vez, y la correccion es sacar la transaccion, no reescribirle el importe.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
# Uso: ./scripts/provision-lote-devuelto.sh
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
BASE = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
        '-H', 'Accept: application/json', '-H', 'MSCRM.SolutionUniqueName: CashHubCore']

ENTITY = 'sanic_transactionbatches'
OPTIONSET = 'sanic_transactionbatches_sanic_batchstatus'
DEVUELTO = 159460004


def req(method, path, body=None, ok=(200, 201, 204)):
    cmd = ['curl', '-s', '-w', '\n__C__%{http_code}', *BASE, '-X', method,
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


# ---- 1. La opcion Devuelto ----
meta = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='sanic_batchstatus')"
                  "/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet")
valores = {o['Value'] for o in meta['OptionSet']['Options']}
if DEVUELTO in valores:
    print(f"  [=] opcion {DEVUELTO} 'Devuelto' ya existe")
else:
    # Option set LOCAL: se direcciona por entidad+columna. Por nombre solo se puede
    # llegar a los globales — con OptionSetName responde "the option set is not Global".
    req('POST', "/InsertOptionValue", {
        'EntityLogicalName': ENTITY,
        'AttributeLogicalName': 'sanic_batchstatus',
        'Value': DEVUELTO,
        'Label': {'LocalizedLabels': [{'Label': 'Devuelto', 'LanguageCode': 1033,
                                       '@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel'}],
                  '@odata.type': 'Microsoft.Dynamics.CRM.Label'},
        'Description': {'LocalizedLabels': [{'Label': 'El aprobador devolvio la carga en el IBS Branch: '
                                                      'hay que corregirla y volver a presentarla.',
                                             'LanguageCode': 1033,
                                             '@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel'}],
                        '@odata.type': 'Microsoft.Dynamics.CRM.Label'},
    })
    print(f"  [+] opcion {DEVUELTO} 'Devuelto' creada")


# ---- 2. Columnas de la devolucion ----
def label(text):
    return {'LocalizedLabels': [{'Label': text, 'LanguageCode': 1033,
                                 '@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel'}],
            '@odata.type': 'Microsoft.Dynamics.CRM.Label'}


def existe(logical):
    try:
        req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='{logical}')?$select=LogicalName")
        return True
    except RuntimeError:
        return False


columnas = [
    ('sanic_motivodevolucion', {
        '@odata.type': 'Microsoft.Dynamics.CRM.MemoAttributeMetadata',
        'SchemaName': 'sanic_MotivoDevolucion',
        'MaxLength': 2000,
        'FormatName': {'Value': 'TextArea'},
        'RequiredLevel': {'Value': 'None'},
        'DisplayName': label('Motivo de la devolucion'),
        'Description': label('Por que el aprobador devolvio la carga en el IBS. Obligatorio al '
                             'marcar el lote Devuelto: quien lo corrija necesita saber que rechazo '
                             'el Core.'),
    }),
    ('sanic_devueltoon', {
        '@odata.type': 'Microsoft.Dynamics.CRM.DateTimeAttributeMetadata',
        'SchemaName': 'sanic_DevueltoOn',
        'Format': 'DateAndTime',
        'RequiredLevel': {'Value': 'None'},
        'DisplayName': label('Devuelto el'),
        'Description': label('Cuando se registro la devolucion. Lo estampa el servidor, no el cliente.'),
    }),
]

for logical, body in columnas:
    if existe(logical):
        print(f"  [=] columna {logical} ya existe")
        continue
    req('POST', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes", body)
    print(f"  [+] columna {logical} creada")

# El lookup va por relacion 1:N, no por Attributes.
if existe('sanic_devueltopor'):
    print("  [=] lookup sanic_devueltopor ya existe")
else:
    req('POST', "/RelationshipDefinitions", {
        '@odata.type': 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata',
        'SchemaName': 'sanic_systemuser_transactionbatches_devueltopor',
        'ReferencedEntity': 'systemuser',
        'ReferencingEntity': ENTITY,
        'CascadeConfiguration': {'Assign': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade',
                                 'Reparent': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade'},
        'Lookup': {
            '@odata.type': 'Microsoft.Dynamics.CRM.LookupAttributeMetadata',
            'SchemaName': 'sanic_DevueltoPor',
            'RequiredLevel': {'Value': 'None'},
            'DisplayName': label('Devuelto por'),
            'Description': label('Quien registro la devolucion del IBS. Lo estampa el servidor con '
                                 'la identidad real del actor, no con lo que mande el cliente.'),
        },
        'AssociatedMenuConfiguration': {'Behavior': 'DoNotDisplay', 'Group': 'Details', 'Order': 10000},
    })
    print("  [+] lookup sanic_devueltopor creado")

req('POST', '/PublishXml',
    {'ParameterXml': f"<importexportxml><entities><entity>{ENTITY}</entity></entities></importexportxml>"})
print("  publicado")

print("Estado 'Devuelto' provisionado.")
PYEOF
