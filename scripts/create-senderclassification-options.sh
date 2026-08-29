#!/usr/bin/env bash
# =============================================================================
# Agrega los baldes de triage del rediseno de vinculos a
# sanic_chtblreceivedemails.sanic_senderclassification, idempotente:
#   159460004 Acceso no autorizado para el cliente
#   159460005 Contacto de otro cliente
#   159460006 Revocado
#
# POR QUE POR API Y NO CON UN IMPORT DE LA SOLUCION: el Entity.xml ya tiene los
# valores (son el fuente), pero importar CashHubCore entero empujaria TODO lo que
# este drifteado en solutions/unpacked/core por tres opciones. Mismo criterio que
# create-alternate-keys.sh. El proximo export confirma el fuente contra el ambiente.
#
# ORDEN DE DESPLIEGUE: esto va ANTES del assembly. ReceivedEmailValidationService
# escribe estos valores al clasificar; si el assembly llega primero, el primer
# correo que caiga en un balde nuevo escribe un valor que la columna no conoce.
#
# Requiere pp_secrets.env. Uso: ./scripts/create-senderclassification-options.sh
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

ENTITY = 'sanic_chtblreceivedemails'
ATTR = 'sanic_senderclassification'

OPCIONES = [
    (159460004, 'Acceso no autorizado para el cliente',
     'El vinculo entre el remitente y el cliente declarado no autoriza (revocado, o '
     'pendiente de aprobacion) y aun asi intento declarar por ese cliente. Revisar el '
     'recordstatus del vinculo: Inactivo = acceso revocado, lo mira seguridad; Pendiente '
     'de aprobacion = hay un RFC de alta esperando firma de Banca.'),
    (159460005, 'Contacto de otro cliente',
     'El remitente no tiene vinculo con el cliente declarado pero esta vigente con otro. '
     'Tipicamente un CIF equivocado. Se le respondio en terminos genericos.'),
    (159460006, 'Revocado',
     'Todos los vinculos del remitente estan revocados. No se le responde: escribirle '
     'contradiria el acto de revocar.'),
]

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
        raise RuntimeError(f"{method} {path} -> {code}: {json.dumps(d)[:400]}")
    return d

def label(text):
    return {'@odata.type': 'Microsoft.Dynamics.CRM.Label',
            'LocalizedLabels': [{'@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel',
                                 'Label': text, 'LanguageCode': 1033}]}

meta = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='{ATTR}')"
                  f"/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$expand=OptionSet")
existentes = {o['Value'] for o in meta['OptionSet']['Options']}
print(f"== {ATTR}: {len(existentes)} opciones actuales")

nuevas = 0
for value, etiqueta, descripcion in OPCIONES:
    if value in existentes:
        print(f"  ya existe: {value} ({etiqueta})")
        continue
    req('POST', '/InsertOptionValue', {
        'EntityLogicalName': ENTITY,
        'AttributeLogicalName': ATTR,
        'Value': value,
        'Label': label(etiqueta),
        'Description': label(descripcion),
    })
    print(f"  creada: {value} ({etiqueta})")
    nuevas += 1

if nuevas:
    print("== Publicando la entidad")
    req('POST', '/PublishXml', {'ParameterXml':
        f'<importexportxml><entities><entity>{ENTITY}</entity></entities></importexportxml>'})

meta = req('GET', f"/EntityDefinitions(LogicalName='{ENTITY}')/Attributes(LogicalName='{ATTR}')"
                  f"/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$expand=OptionSet")
print("== Estado final:")
for o in sorted(meta['OptionSet']['Options'], key=lambda x: x['Value']):
    print(f"   {o['Value']}  {o['Label']['LocalizedLabels'][0]['Label']}")
PYEOF
