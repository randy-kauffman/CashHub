#!/usr/bin/env bash
# =============================================================================
# Deploy quirurgico de los dos flujos de notificacion del modulo RFC, sin import
# de solucion (no pisa otros cambios del disenador web):
#
#   CH-Flow-Notifyreviewers
#   CH-Flow-NotifywhenRFCimplementationfinish
#
# Cambio que traen: la accion "Obtener_enlace_a_la_app_de_adminsitracion_de_clientes"
# deja de leer la environment variable sanic_CHEVAppUrl_ClientAdmin y pasa a leer
# el parametro Apps.ClientAdministration.Url de CH-Tbl-Parameter. Con eso la URL de
# Client Administration tiene UNA sola fuente de verdad (la misma que ya usa
# CH-App-IngestionConsole) y se administra desde CH-App-Configuration.
#
# La environment variable estaba SIN default y SIN valor asignado: el link
# "See details in powerapps" de los mensajes de Teams nunca se renderizaba.
#
# Patron del proyecto (deploy-cloud-flows-owner-reactivation): desactivar, patch
# del clientdata, y reactivar IMPERSONANDO AL OWNER — como SP el trigger no queda
# registrado. Ver scripts/deploy-downloaddata-flow.sh.
#
# Requiere pp_secrets.env. Uso: ./scripts/deploy-rfc-notify-flows.sh [--dry-run]
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL DRY="${1:-}"

python3 - <<'PYEOF'
import json, os, subprocess, tempfile

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
TOKEN = os.environ['TOKEN']
DRY = os.environ.get('DRY') == '--dry-run'

FLOWS = [
    ('445602a6-aea8-f011-bbd2-7c1e5280d4ac',
     'solutions/unpacked/flow/Workflows/CH-Flow-Notifyreviewers-445602A6-AEA8-F011-BBD2-7C1E5280D4AC.json'),
    ('94eafcb8-6bab-f011-bbd2-000d3a52eba2',
     'solutions/unpacked/flow/Workflows/CH-Flow-NotifywhenRFCimplementationfinish-94EAFCB8-6BAB-F011-BBD2-000D3A52EBA2.json'),
]
ACTION = 'Obtener_enlace_a_la_app_de_adminsitracion_de_clientes'


def call(method, path, body=None, caller=None):
    hdr = ['-H', f'Authorization: Bearer {TOKEN}', '-H', 'Content-Type: application/json',
           '-H', 'If-Match: *']
    if caller:
        hdr += ['-H', f'MSCRMCallerID: {caller}']
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *hdr, '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close(); cmd += ['--data-binary', '@' + tmp.name]
    try:
        raw = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    payload, _, code = raw.rpartition('\n')
    code = int(code)
    if code >= 400:
        raise RuntimeError(f"{method} {path} -> {code}: {payload[:400]}")
    return json.loads(payload) if payload.strip() else {}


for wf, src in FLOWS:
    d = call('GET', f"/workflows({wf})?$select=name,statecode,statuscode,_ownerid_value,clientdata")
    owner = d['_ownerid_value']
    live = json.loads(d['clientdata'])
    mine = json.load(open(src))
    print(f"\n== {d['name']}  (statecode {d['statecode']}/{d['statuscode']}, owner {owner})")

    live_prm = live['properties']['definition']['actions'][ACTION]['inputs']['parameters']
    mine_prm = mine['properties']['definition']['actions'][ACTION]['inputs']['parameters']
    print(f"   vivo : {live_prm.get('entityName')}  |  {live_prm.get('$filter')}")
    print(f"   nuevo: {mine_prm.get('entityName')}  |  {mine_prm.get('$filter')}")

    if live_prm.get('entityName') == mine_prm.get('entityName'):
        print("   [=] ya migrado, no se toca")
        continue

    # Guardia de drift: el resto de la definicion tiene que coincidir. Si alguien
    # edito el flow en el disenador, se frena en vez de pisarle el trabajo.
    a, b = json.loads(json.dumps(live['properties']['definition'])), json.loads(json.dumps(mine['properties']['definition']))
    a['actions'][ACTION]['inputs']['parameters'] = b['actions'][ACTION]['inputs']['parameters']
    a = json.dumps(a, sort_keys=True).replace("?['body/value'])?['value']", "?['body/value'])?['sanic_value']")
    if a != json.dumps(b, sort_keys=True):
        print("   !! DRIFT: el flow vivo difiere del repo mas alla de este cambio. NO se despliega.")
        print("      Exporta la solucion y re-unpack antes de re-correr.")
        continue

    if DRY:
        print("   [dry] desactivaria, patchearia el clientdata y reactivaria como owner")
        continue

    mine.get('properties', {}).pop('templateName', None)
    clientdata = json.dumps(mine, separators=(',', ':'), ensure_ascii=False)
    try:
        call('PATCH', f"/workflows({wf})", {'statecode': 0, 'statuscode': 1})
        print("   desactivado")
        call('PATCH', f"/workflows({wf})", {'clientdata': clientdata})
        print(f"   clientdata actualizado ({len(clientdata)} chars)")
        call('PATCH', f"/workflows({wf})", {'statecode': 1, 'statuscode': 2}, caller=owner)
        print("   reactivado impersonando al owner")
    except Exception as e:
        print(f"   ERROR: {e}")
        print("   !! El flow puede haber quedado DESACTIVADO — revisar en el portal.")
        raise

    chk = call('GET', f"/workflows({wf})?$select=statecode,statuscode,clientdata")
    prm = json.loads(chk['clientdata'])['properties']['definition']['actions'][ACTION]['inputs']['parameters']
    print(f"   VERIFICACION -> statecode {chk['statecode']}/{chk['statuscode']} (1/2 = activo)"
          f"  entityName={prm.get('entityName')}")
PYEOF
