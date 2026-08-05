#!/usr/bin/env bash
# =============================================================================
# Crea las alternate keys de CashHub vía Web API (idempotente).
#
# 1. sanic_chtblclientemail: par (sanic_name, sanic_client) — un correo puede
#    pertenecer a varios clientes, pero el par dirección+cliente es único.
#    (Decisión de negocio 2026-08-04: el correo AUTORIZA, el contenido ATRIBUYE.)
# 2. sanic_chtblclientaccount: sanic_name único — un número de cuenta existe
#    una sola vez en el banco.
#
# Se crean por API y no en el Entity.xml para no adivinar el formato de
# serialización de EntityKeys; el próximo export de la solución las trae al
# fuente automáticamente. La activación del índice es asíncrona (EntityKeyIndexStatus).
#
# Uso: ./scripts/create-alternate-keys.sh
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
import json, os, subprocess

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json']

def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    return json.loads(out) if out.strip() else {}

def label(t):
    return {"@odata.type": "Microsoft.Dynamics.CRM.Label",
            "LocalizedLabels": [{"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": t, "LanguageCode": 1033}]}

KEYS = [
    ("sanic_chtblclientemail",   "sanic_KeyCorreoPorCliente", "Correo unico por cliente", ["sanic_name", "sanic_client"]),
    ("sanic_chtblclientaccount", "sanic_KeyNumeroDeCuenta",   "Numero de cuenta unico",   ["sanic_name"]),
]
for entity, schema, display, attrs in KEYS:
    existing = req('GET', f"/EntityDefinitions(LogicalName='{entity}')/Keys?$select=SchemaName").get('value', [])
    if any(k.get('SchemaName') == schema for k in existing):
        print(f"  ya existe: {entity}.{schema}")
        continue
    r = req('POST', f"/EntityDefinitions(LogicalName='{entity}')/Keys", {
        "SchemaName": schema, "DisplayName": label(display), "KeyAttributes": attrs})
    if 'error' in r:
        raise SystemExit(f"  ERROR {entity}.{schema}: {r['error'].get('message','')[:250]}")
    print(f"  creada: {entity}.{schema} sobre {attrs}")
print("Listo. El índice se activa de forma asíncrona; si hay datos duplicados preexistentes la key queda en estado Failed y hay que depurar los duplicados.")
PYEOF
