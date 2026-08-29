#!/usr/bin/env bash
# =============================================================================
# Corrige la alternate key de CH-Tbl-ClientAccount, idempotente:
#   BAJA:  sanic_keynumerodecuenta   sobre (sanic_name)
#   ALTA:  sanic_KeyCuentaPorCliente sobre (sanic_client, sanic_name)
#
# POR QUE — la tabla estaba mal entendida. CH-Tbl-ClientAccount NO es el maestro
# de cuentas del banco ni dice "de quien es esta cuenta": dice "a que numeros de
# cuenta esta AUTORIZADO a depositar este cliente". Es una lista de autorizacion
# cliente<->recurso, exactamente la misma forma que CH-Tbl-ClientEmail.
#
# Con la key sobre sanic_name sola, la MISMA cuenta no se puede autorizar a dos
# clientes. Eso rompe casos legitimos y comunes en cash management: un grupo
# empresarial con varias razones sociales depositando en la misma cuenta
# recolectora, o una red de franquicias. Hoy la segunda alta falla con violacion
# de clave.
#
# La asimetria era la pista: CH-Tbl-ClientEmail —la misma forma de relacion— ya
# esta keyeada por el PAR con sanic_KeyCorreoPorCliente. Esta no.
#
# Lo que la key nueva SI sigue garantizando: un cliente no puede tener la misma
# cuenta registrada dos veces. Eso es lo que se queria proteger.
#
# NO REQUIERE CAMBIO DE CODIGO: ReceivedEmailValidationService.AccountBelongsToClient
# ya consulta por sanic_name Y sanic_client, o sea que siempre asumio el par.
#
# SEGURIDAD DEL CAMBIO: hoy hay 2239 filas con 2239 numeros distintos (cero
# repetidos), asi que el indice nuevo del par no puede encontrar duplicados y
# entra limpio. La creacion es ASINCRONA: verificar EntityKeyIndexStatus al final.
#
# Requiere pp_secrets.env. Uso: ./scripts/fix-clientaccount-alternate-key.sh [--dry-run]
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL
export DRY_RUN="${1:-}"

python3 - <<'PYEOF'
import json, os, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
DRY = os.environ.get('DRY_RUN') == '--dry-run'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'MSCRM.SolutionUniqueName: CashHubCore']

ENTITY = 'sanic_chtblclientaccount'
VIEJA = 'sanic_keynumerodecuenta'
NUEVA_SCHEMA = 'sanic_KeyCuentaPorCliente'
NUEVA = NUEVA_SCHEMA.lower()


def req(method, path, body=None):
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
    d = json.loads(payload) if payload.strip() else {}
    if code >= 400:
        raise RuntimeError(f'{method} {path} -> {code}: {json.dumps(d)[:400]}')
    return d


def keys():
    return req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys"
                      f"?$select=LogicalName,KeyAttributes,EntityKeyIndexStatus,MetadataId")['value']


print('== Estado inicial')
actuales = {k['LogicalName']: k for k in keys()}
for k in actuales.values():
    print(f"   {k['LogicalName']}  {k['KeyAttributes']}  {k['EntityKeyIndexStatus']}")

# Verificacion previa: el par no puede tener duplicados. Se comprueba de verdad y
# no se asume — si los hubiera, el indice nuevo quedaria en Failed y la unicidad
# no se aplicaria, en silencio.
print('\n== Verificando que (cliente, cuenta) no tenga duplicados')
filas, url = [], f"/sanic_chtblclientaccounts?{urllib.parse.urlencode({'$select': 'sanic_name,_sanic_client_value'})}"
while url:
    page = req('GET', url)
    filas += page['value']
    nxt = page.get('@odata.nextLink')
    url = nxt[len(API):] if nxt else None
pares = [(f.get('_sanic_client_value'), f.get('sanic_name')) for f in filas]
dups = {p for p in pares if pares.count(p) > 1} if len(pares) < 5000 else set()
print(f'   {len(filas)} filas · {len(set(pares))} pares distintos · {len(dups)} duplicados')
if dups:
    print('   *** HAY DUPLICADOS: depurarlos antes de crear la key. Abortando.')
    raise SystemExit(1)

if DRY:
    print(f'\n[dry] DELETE {VIEJA}')
    print(f'[dry] CREATE {NUEVA_SCHEMA} sobre (sanic_client, sanic_name)')
    raise SystemExit(0)

if VIEJA in actuales:
    print(f'\n== Bajando {VIEJA} (sobre sanic_name sola)')
    req('DELETE', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys({actuales[VIEJA]['MetadataId']})")
    print('   eliminada')
else:
    print(f'\n== {VIEJA} ya no existe')

# El indice de una alternate key no puede pasar de 1700 bytes. sanic_name estaba
# declarada con MaxLength 850 -> 850*2 = 1700 bytes de nvarchar, y sumando los 16
# del GUID del lookup da 1716: se pasa por 16 bytes. 850 ademas es absurdo para un
# numero de cuenta (el mas largo de las 2239 filas mide 10). Se achica a 50, que
# deja margen de sobra y es el mismo largo que sanic_bagnumber.
LARGO_CUENTA = 50
meta = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='sanic_name')"
                  f"/Microsoft.Dynamics.CRM.StringAttributeMetadata?$select=MaxLength,MetadataId")
if meta['MaxLength'] > LARGO_CUENTA:
    largo_real = max((len(f.get('sanic_name') or '') for f in filas), default=0)
    if largo_real > LARGO_CUENTA:
        print(f'   *** hay datos de {largo_real} caracteres; no se puede achicar a {LARGO_CUENTA}. Abortando.')
        raise SystemExit(1)
    print(f"== Achicando sanic_name de {meta['MaxLength']} a {LARGO_CUENTA} "
          f"(el dato mas largo mide {largo_real})")
    req('PUT', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes({meta['MetadataId']})", {
        '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
        'MetadataId': meta['MetadataId'],
        'LogicalName': 'sanic_name',
        'SchemaName': 'sanic_name',
        'AttributeType': 'String',
        'AttributeTypeName': {'Value': 'StringType'},
        'MaxLength': LARGO_CUENTA,
        'RequiredLevel': {'Value': 'ApplicationRequired'},
    })
    print('   achicada')
else:
    print(f"== sanic_name ya mide {meta['MaxLength']}")

if NUEVA not in actuales:
    print(f'== Creando {NUEVA_SCHEMA} sobre (sanic_client, sanic_name)')
    # El atributo de lookup va con el LOGICAL NAME pelado (sanic_client), NO como
    # _sanic_client_value: esa forma es del Web API para leer, no para declarar
    # metadata. Precedente en este repo: sanic_KeyCorreoPorCliente.
    req('POST', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys", {
        'SchemaName': NUEVA_SCHEMA,
        'DisplayName': {'LocalizedLabels': [
            {'Label': 'Cuenta unica por cliente', 'LanguageCode': 1033}]},
        'KeyAttributes': ['sanic_client', 'sanic_name'],
    })
    print('   creada')
else:
    print(f'== {NUEVA_SCHEMA} ya existe')

print('== Publicando la entidad')
req('POST', '/PublishXml', {'ParameterXml':
    f'<importexportxml><entities><entity>{ENTITY}</entity></entities></importexportxml>'})

print('\n== Estado final')
for k in keys():
    marca = ''
    if k['EntityKeyIndexStatus'] == 'Failed':
        marca = '   *** FAILED: la unicidad NO se esta aplicando'
    print(f"   {k['LogicalName']}  {k['KeyAttributes']}  {k['EntityKeyIndexStatus']}{marca}")
print('\nLa activacion del indice es asincrona: Pending/InProgress es normal, Failed no.')
PYEOF
