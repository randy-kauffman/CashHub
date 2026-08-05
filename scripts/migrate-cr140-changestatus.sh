#!/usr/bin/env bash
# =============================================================================
# Migración de datos: cr140_changestatus -> sanic_changestatus
# Tabla: sanic_chtblclientdatachangerequest (cabecera de RFC)
#
# Copia el valor del campo viejo al nuevo en TODOS los registros, con mapeo
# de valores (el viejo optionset usa el rango 796490xxx del publisher ajeno;
# el nuevo usa el rango 15946000x del publisher sanic).
#
# ¡OJO!: el viejo "Canceled" vale 159460001, que en el esquema NUEVO es
# "Submitted". Por eso el mapeo es explícito, valor por valor — jamás copiar
# el número crudo.
#
# Requiere en pp_secrets.env (NO versionado, en la raíz del repo):
#   DATAVERSE_URL, PP_TENANT_ID, PP_CLIENT_ID, PP_CLIENT_SECRET
#   (app user con System Administrator en el ambiente destino)
#
# Uso:  ./scripts/migrate-cr140-changestatus.sh [--dry-run]
# =============================================================================
set -euo pipefail

DRY_RUN="${1:-}"
source "${ENV_FILE:-pp_secrets.env}"
# Robustez: CRLF y barra final rompen la URL del token de forma silenciosa
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

API="${DATAVERSE_URL}/api/data/v9.2"
H=(-H "Authorization: Bearer ${TOKEN}" -H "Content-Type: application/json" -H "OData-MaxVersion: 4.0" -H "OData-Version: 4.0")

# Mapeo viejo (cr140, rango 79649/15946 mixto) -> nuevo (sanic, rango 15946)
declare -A MAP=(
  [796490000]=159460000  # Draft
  [796490001]=159460001  # Submited -> Submitted
  [796490002]=159460002  # Approved
  [796490003]=159460003  # Rejected
  [796490004]=159460004  # Applied
  [796490005]=159460005  # Failed
  [159460001]=159460006  # Canceled (viejo valor colisiona con Submitted nuevo)
)

echo "== Leyendo RFCs con cr140_changestatus poblado =="
ROWS=$(curl -sf "${H[@]}" \
  "${API}/sanic_chtblclientdatachangerequests?\$select=sanic_chtblclientdatachangerequestid,cr140_changestatus&\$filter=cr140_changestatus%20ne%20null" \
  | python3 -c "
import sys, json
for r in json.load(sys.stdin)['value']:
    print(r['sanic_chtblclientdatachangerequestid'], r['cr140_changestatus'])")

TOTAL=0; MIGRADOS=0
while read -r ID OLD; do
  [ -z "${ID}" ] && continue
  TOTAL=$((TOTAL+1))
  NEW="${MAP[$OLD]:-}"
  if [ -z "${NEW}" ]; then
    echo "  !! ${ID}: valor viejo desconocido ${OLD} — REVISAR A MANO"
    continue
  fi
  if [ "${DRY_RUN}" = "--dry-run" ]; then
    echo "  (dry-run) ${ID}: ${OLD} -> ${NEW}"
  else
    curl -sf "${H[@]}" -X PATCH "${API}/sanic_chtblclientdatachangerequests(${ID})" \
      -d "{\"sanic_changestatus\": ${NEW}}" > /dev/null
    echo "  OK ${ID}: ${OLD} -> ${NEW}"
    MIGRADOS=$((MIGRADOS+1))
  fi
done <<< "${ROWS}"

echo "== Fin: ${MIGRADOS}/${TOTAL} registros migrados =="
