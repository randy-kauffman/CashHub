#!/usr/bin/env bash
# =============================================================================
# Crea las alternate keys de CashHub vía Web API (idempotente).
#
# 1. sanic_chtblclientemail: par (sanic_name, sanic_client) — un correo puede
#    pertenecer a varios clientes, pero el par dirección+cliente es único.
#    (Decisión de negocio 2026-08-04: el correo AUTORIZA, el contenido ATRIBUYE.)
# 2. sanic_chtblclientaccount: OJO — esta key era (sanic_name) sola, bajo la
#    suposición equivocada de que "un número de cuenta existe una sola vez en el
#    banco". La tabla NO es el maestro de cuentas: dice a qué números de cuenta
#    está AUTORIZADO a depositar cada cliente, así que la misma cuenta puede
#    estar autorizada a varios clientes (grupo empresarial con cuenta recolectora,
#    red de franquicias). La reemplaza sanic_KeyCuentaPorCliente sobre el PAR
#    (sanic_client, sanic_name) — misma forma que sanic_KeyCorreoPorCliente.
#    Ver scripts/fix-clientaccount-alternate-key.sh.
# 4. sanic_chtbltransactions: par (sanic_relateddepositrecounting,
#    sanic_transactiontype) — un depósito recontado genera COMO MÁXIMO UN asiento
#    de cada tipo. En particular los `Deposito` legítimos por depósito recontado
#    son EXACTAMENTE 1 (lo crea CH-App-Recounting cuando la tula cierra sin
#    diferencia, o el supervisor al aprobar la tula con diferencia — nunca los
#    dos), y lo mismo vale para Sobrante, Faltante y 'Sin declaracion'. La key lo
#    GARANTIZA a nivel de índice: el chequeo previo del plugin
#    (GuardSupervisorAccountingPlugin) da el mensaje de negocio, pero entre dos
#    supervisores simultáneos hay una ventana entre el SELECT y el INSERT que solo
#    un índice único cierra.
#
#    OJO — atributo de lookup en la key: va el LOGICAL NAME pelado
#    (`sanic_relateddepositrecounting`), NO la forma `_..._value`. `_x_value` es la
#    convención del Web API para LEER/referenciar, no para declarar metadata.
#    Precedente verificado en este mismo repo: la key sanic_KeyCorreoPorCliente se
#    creó con `sanic_client` (lookup) y volvió del export como
#    <AttributeName>sanic_client</AttributeName>.
#
#    OJO — asientos SIN ancla: el par Faltante+Deposito de un depósito declarado
#    que nadie recontó nace con `sanic_relateddepositrecounting` VACÍO (guía 03 §11
#    9.b y la app vieja). Si el índice único de la plataforma trata NULL = NULL,
#    la key entra en estado Failed por datos preexistentes. En ese caso NO se
#    fuerza: la salida es B-5.1 (agregar el lookup `sanic_relateddeclareddeposit` a
#    sanic_chtbltransactions y anclar también esos asientos). Verificar el estado
#    con EntityKeys?$select=EntityKeyIndexStatus después de correr el script.
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
    # 3. Un correo entrante se registra UNA sola vez. Con el trigger y el barrido
    #    alimentando al mismo procesador, la dedup de "buscar y despues crear" deja de
    #    alcanzar: sin indice unico dos corridas simultaneas crean dos registros. El
    #    campo es String(300) = 600 bytes, holgado bajo el limite de 1700 del indice.
    ("sanic_chtblreceivedemails", "sanic_KeyInternetMessageId", "Correo entrante unico",
     ["sanic_receivedemailinternetmessageid"]),
    # 4. Un solo asiento por (deposito recontado, tipo). Cierra la ventana de carrera
    #    que el chequeo de duplicados del plugin no puede cerrar solo.
    ("sanic_chtbltransactions", "sanic_KeyAsientoPorRecuento",
     "Asiento unico por recuento y tipo",
     ["sanic_relateddepositrecounting", "sanic_transactiontype"]),
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
