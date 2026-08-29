#!/usr/bin/env bash
# =============================================================================
# Crea la identificacion tributaria del cliente, idempotente:
#   texto sanic_TaxId (20) en sanic_chtblclient
#   alternate key sanic_KeyTaxId sobre esa columna
#
# QUE ES: RUC (Nicaragua, Panama), NIT (Guatemala), cedula juridica (Costa Rica).
# Un mismo concepto con nombre distinto en cada pais.
#
# OJO — POR QUE NO SE LLAMA LegalIdentifier: "Legal Entity Identifier" (LEI) es un
# estandar ISO 17442 real y su largo es EXACTAMENTE 20 caracteres, el mismo de esta
# columna. Quien leyera sanic_legalidentifier(20) asumiria LEI y no RUC. TaxId es
# neutro entre paises y no colisiona con ningun estandar.
#
# PARA QUE: es el TERCER identificador con el que un cliente puede declararse en la
# plantilla (Depositos!J2). ReceivedEmailValidationService.FindClientByCif hace un OR
# sobre sanic_cifbco, sanic_cifcom y sanic_taxid — el cliente declara con el numero
# que tiene a mano y no tiene por que adivinar cual guardamos nosotros.
#
# OJO — NULOS Y LA ALTERNATE KEY: al crear la columna TODOS los clientes existentes
# quedan con sanic_taxid vacio. Eso NO rompe la key: Dataverse no indexa los NULL,
# permite multiples filas nulas y solo exige unicidad entre los valores no nulos.
# Lo que SI la puede tumbar son DUPLICADOS reales una vez que se backfillee: la
# creacion del indice es asincrona y termina en Failed si encuentra repetidos.
# Verificar SIEMPRE despues de correr:
#   GET /EntityDefinitions(LogicalName='sanic_chtblclient')/Keys?$select=LogicalName,EntityKeyIndexStatus
#
# OJO — AMBIGUEDAD ENTRE COLUMNAS: la key garantiza que el taxid no se repita entre
# clientes, pero NO impide que el taxid de un cliente coincida con el cifbco de otro.
# Ese caso lo detecta FindClientByCif (TopCount = 2) y aborta la validacion con error
# en vez de atribuir la declaracion al cliente equivocado.
#
# Todo entra a la solucion CashHubCore (header MSCRM.SolutionUniqueName).
#
# Requiere pp_secrets.env. Uso: ./scripts/create-client-taxid-column.sh
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

ENTITY = 'sanic_chtblclient'

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

print("== Columna de identificacion tributaria del cliente")
existing = req('GET',
    f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='sanic_taxid')?$select=LogicalName",
    ok404=True)
if existing is not None:
    print(f"  ya existe: {ENTITY}.sanic_taxid")
else:
    req('POST', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
        'SchemaName': 'sanic_TaxId',
        'DisplayName': label('TaxId'),
        'Description': label(
            'Identificacion tributaria del cliente: RUC, NIT o cedula juridica segun el '
            'pais. Es uno de los tres identificadores con los que un cliente puede '
            'declararse en la plantilla de depositos (junto con CIFBCO y CIFCOM). '
            'Unico entre clientes: tiene alternate key.'),
        'RequiredLevel': {'Value': 'None'},
        'MaxLength': 20,
        'FormatName': {'Value': 'Text'},
        # Buscable de verdad: se usa para resolver al cliente desde el correo.
        'IsValidForAdvancedFind': {'Value': True, 'CanBeChanged': True,
                                   'ManagedPropertyLogicalName': 'canmodifysearchsettings'},
    })
    print(f"  creada: {ENTITY}.sanic_taxid (TaxId, nvarchar 20)")

print("== Alternate key sobre la identificacion tributaria")
keys = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys"
                  f"?$select=LogicalName,EntityKeyIndexStatus")
by_name = {k['LogicalName']: k for k in (keys or {}).get('value', [])}
if 'sanic_keytaxid' in by_name:
    print(f"  ya existe: sanic_keytaxid (estado {by_name['sanic_keytaxid']['EntityKeyIndexStatus']})")
else:
    req('POST', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys", {
        'SchemaName': 'sanic_KeyTaxId',
        'DisplayName': label('Identificacion tributaria unica'),
        'KeyAttributes': ['sanic_taxid'],
    })
    print("  creada: sanic_keytaxid")

print("== Publicando la entidad")
req('POST', '/PublishXml', {'ParameterXml':
    f'<importexportxml><entities><entity>{ENTITY}</entity></entities></importexportxml>'})

# La activacion del indice es ASINCRONA: 'Pending'/'InProgress' aca es normal, pero
# 'Failed' significa que hay taxid duplicados y la unicidad NO se esta aplicando.
estado = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Keys"
                    f"?$select=LogicalName,EntityKeyIndexStatus")
for k in estado.get('value', []):
    if k['LogicalName'] == 'sanic_keytaxid':
        print(f"== Estado del indice: {k['EntityKeyIndexStatus']}")
        if k['EntityKeyIndexStatus'] == 'Failed':
            print("   FAILED — hay sanic_taxid duplicados. La unicidad NO se aplica.")
            print("   Depurar los duplicados y volver a correr este script.")
print("Esquema de identificacion tributaria completo.")
PYEOF
