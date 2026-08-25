#!/usr/bin/env bash
# =============================================================================
# Lista y activa/desactiva plugin steps del assembly CashHub.Plugins.
#
# Uso:
#   ./scripts/plugin-steps.sh list                    # todos los plugins + steps con su estado
#   ./scripts/plugin-steps.sh enable  <step-guid>     # activa el step
#   ./scripts/plugin-steps.sh disable <step-guid>     # desactiva el step
#
# Requiere pp_secrets.env (DATAVERSE_URL, PP_TENANT_ID, PP_CLIENT_ID, PP_CLIENT_SECRET).
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
API="$DATAVERSE_URL/api/data/v9.2"

case "${1:-list}" in
  list)
    curl -s -H "Authorization: Bearer $TOKEN" \
      "$API/sdkmessageprocessingsteps?\$select=sdkmessageprocessingstepid,name,stage,mode,statecode&\$expand=plugintypeid(\$select=typename)&\$filter=contains(plugintypeid/typename,'CashHub.Plugins')&\$orderby=name" \
      | python3 -c "
import sys, json
rows = json.load(sys.stdin)['value']
ETAPA = {10: 'Pre-val', 20: 'Pre-op', 40: 'Post-op'}
por_plugin = {}
for r in rows:
    por_plugin.setdefault(r['plugintypeid']['typename'], []).append(r)
for tipo in sorted(por_plugin):
    print(f'\n{tipo}')
    for r in sorted(por_plugin[tipo], key=lambda x: x['name']):
        estado = 'ACTIVO ' if r['statecode'] == 0 else 'INACTIVO'
        modo = 'sync' if r['mode'] == 0 else 'async'
        print(f\"  [{estado}] {ETAPA.get(r['stage'], r['stage']):7} {modo:5} {r['name']}\")
        print(f\"            id: {r['sdkmessageprocessingstepid']}\")
"
    ;;
  enable|disable)
    [ -n "${2:-}" ] || { echo "Falta el GUID del step. Uso: $0 $1 <step-guid>"; exit 1; }
    if [ "$1" = "enable" ]; then STATE=0; STATUS=1; else STATE=1; STATUS=2; fi
    HTTP=$(curl -s -o /tmp/pluginstep_resp.json -w '%{http_code}' -X PATCH \
      -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
      "$API/sdkmessageprocessingsteps($2)" \
      -d "{\"statecode\": $STATE, \"statuscode\": $STATUS}")
    if [ "$HTTP" = "204" ]; then
      echo "OK: step $2 → $([ "$1" = enable ] && echo ACTIVO || echo INACTIVO)"
    else
      echo "ERROR HTTP $HTTP:"; cat /tmp/pluginstep_resp.json; exit 1
    fi
    ;;
  *)
    echo "Uso: $0 {list|enable <guid>|disable <guid>}"; exit 1 ;;
esac
