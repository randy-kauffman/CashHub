#!/usr/bin/env bash
# =============================================================================
# Registra el GUARD server-side de CH-App-Recounting v2 (idempotente).
# Es la contraparte de seguridad del rebuild: la app canvas v2 ya NO escribe
# aprobaciones, ni totales, ni transacciones — eso pasa a estos plugins.
# Contrato: docs/recounting-rebuild/00-analisis-y-plan.md §2.2, puntos 1-6,
# y el mapping completo en docs/recounting-rebuild/05-backend-espejos.md §6.
#
#   1. pluginassembly  CashHub.Plugins (re-sube el DLL con los tipos nuevos dentro)
#   2. plugintypes + pasos SINCRONOS:
#
#      RecountingSessionAutoApprovePlugin  (punto 1 — mata S-1)
#        Pre-operation  (20) Create de sanic_chtblapprovalrequest
#            Si el tipo es EXACTAMENTE "Crear sesion de recuento", resuelve la
#            sesion del JSON de RequestResources, lee su mesa y su cajero COMO
#            SISTEMA y, si la mesa es del propio solicitante, estampa la
#            resolucion en el MISMO Create (ApprovalStatus=Aprobado, SolvedBy,
#            ResolutionDatetime). Mesa ajena o no verificable => queda pendiente.
#        Post-operation (40) Create de sanic_chtblapprovalrequest
#            Confirma la sesion (Confirmed + ConfirmedBy) SOLO si el paso Pre-op
#            auto-aprobo. Va despues a proposito: si el Create se cae, la sesion
#            no queda confirmada colgando de una solicitud que no existe.
#        SIN filteringattributes: el tipo de solicitud es texto libre, no una
#        columna filtrable — el descarte por tipo lo hace el plugin en 1 linea.
#
#      BagRecountClosurePlugin             (puntos 2 y 3 — matan S-2a y S-2b)
#        Pre-operation  (20) Update de sanic_chtblbags, filtrado en
#            sanic_bagstatus y con PreImage de esa columna:
#            al ENTRAR a Recontada (159460003) o a Pendiente de revision por
#            supervisor - recuento (159460004), recalcula TotalNIO/USD/EUR desde
#            los depositos PERSISTIDOS (query por sanic_bagid) y los PISA en el
#            Target. Lo que mande el cliente en esas columnas se descarta.
#        Post-operation (40) Update de sanic_chtblbags, mismo filtro y PreImage:
#            al ENTRAR a Recontada, crea un CH-Tbl-Transactions por deposito.
#            IDEMPOTENTE: consulta primero que depositos de la tula YA tienen un
#            asiento DE TIPO Deposito (misma clave (recuento, tipo) que usa
#            AccountingGuard para rechazar duplicados) — la tula puede volver a
#            Recontada tras un rechazo y reconteo.
#            🧨 CONVIVENCIA: estos Create pasan por GuardSupervisorAccountingPlugin
#            (Pre-validation del Create de sanic_chtbltransactions), que para el
#            tipo Deposito EXIGE moneda y montos identicos al deposito recontado.
#            El plugin los copia tal cual; hay un test que fija ese contrato. La
#            consulta de idempotencia no reemplaza a ese chequeo: lo ANTICIPA, para
#            que un duplicado saltee el asiento en vez de voltear la transicion
#            entera de la tula.
#        La PreImage es OBLIGATORIA en los dos: sin ella no se sabe si el estado
#        CAMBIO, y Dataverse dispara el paso filtrado aunque se reescriba el
#        mismo valor (recalculo y asientos por un update sin cambio real).
#        Rank 40 sobre el Update Post-op de tulas: corre DESPUES de los espejos
#        CheckMirrorStamp (rank 20) y DepositSessionMirror (rank 30), que son
#        los que dejan los hijos apuntando a la sesion correcta.
#
#      GuardRecountingPlugin               (puntos 4 y 5 — mata S-5 y habilita D02-4)
#        Pre-validation (10) Create/Update/Delete de
#            sanic_chtbldepositsrecounting, sanic_chtblchecks y
#            sanic_chtblcountedmoneydetails: se rechaza si la tula ANCESTRO no
#            esta 'Asignada a cajero' (159460002). Un Update que RE-CUELGA el
#            registro de otra tula exige que las DOS esten en la mesa.
#        Pre-validation (10) Delete de sanic_chtblapprovalrequest:
#            solo propias (createdby = actor) y SIN resolver.
#        Pre-validation (10) Delete de sanic_chtblrecountingsession:
#            solo propias (sanic_cashier = actor), sin Confirmed y sin solicitud
#            resuelta que las referencie.
#        Rank 5: despues de GuardReceptionPlugin (rank 1) donde convivan.
#
# EXCEPCIONES del gate (viven en RecountingGuard.IsWriteExempt, bajo test):
#   (a) Depth > 1 — el pipeline de plugins (espejos E-1/E-2/E-4 y sus cascadas).
#   (b) Update de cheque que toca UNICAMENTE sanic_checkstatus — el marcado a
#       Recontado del cierre de tula (04 §11.3 paso 2), que corre DESPUES de
#       mover la bolsa. CheckGuard sigue decidiendo si esa transicion es legal.
#   (c) Update que toca UNICAMENTE columnas ESPEJO derivadas
#       (deposito: sanic_recountingsessionmirror; cheque: sanic_bag y
#       sanic_recountingsession). Los BACKFILLS las estampan por Web API — depth
#       1 y sobre el historico ENTERO, con tulas en boveda y recontadas: sin esta
#       excepcion el gate voltea backfill-recounting-mirrors.py y backfill-tracker.py
#       completos. Es el mismo bypass touchesOnlyMirror de CheckGuard/ReceptionGuard.
#   (d) NO hay excepcion por usuario (SP de despliegue / seeder). Ningun guard
#       del repo la tiene: el seeder respeta el guard por ORDEN y, para el
#       --clean, desactiva TEMPORALMENTE los pasos Delete concretos (ver
#       seed-reception-stress.py). Los pasos Delete de este guard que un seeder
#       de reconteo tendria que apagar son:
#         CashHub.GuardRecounting: Delete de sanic_chtbldepositsrecounting
#         CashHub.GuardRecounting: Delete de sanic_chtblchecks
#         CashHub.GuardRecounting: Delete de sanic_chtblcountedmoneydetails
#
# FUERA de alcance a proposito:
#   - sanic_chtblbags NO se gatea aca: la apertura 'Almacenada en boveda' ->
#     'Asignada a cajero' la patchea el cajero (00 §2.2 punto 6) y ya esta en la
#     matriz legal de ReceptionGuard, que la impone. Verificado con test.
#   - sanic_chtbltransactions NO se gatea aca: los asientos los crea este mismo
#     guard con la tula YA en Recontada, y la tabla tiene su propio
#     GuardTransactionPlugin.
#
# ORDEN: correr DESPUES del build Release. NO depende de las columnas espejo
# (create-recounting-mirror-columns.sh): el guard no lee ningun espejo.
#
# Requiere pp_secrets.env y el assembly compilado en Release.
# Uso: ./scripts/register-recounting-guard.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

ASSEMBLY="src/backend/CashHub.Plugins/bin/Release/net462/CashHub.Plugins.dll"
[ -f "$ASSEMBLY" ] || { echo "Falta $ASSEMBLY — compilar primero (dotnet build -c Release)"; exit 1; }

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL ASSEMBLY

python3 - <<'PYEOF'
import base64, json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Prefer: return=representation']


def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    tmp = None
    if body is not None:
        import tempfile
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp:
            os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d


def find(path):
    return req('GET', path).get('value', [])


def one(entityset, filter_expr, select):
    rows = find(f"/{entityset}?$select={select}&$filter={urllib.parse.quote(filter_expr)}")
    if not rows:
        raise RuntimeError(f"No se encontro {entityset} con filtro: {filter_expr}")
    return rows[0][select]


def upsert(entityset, filter_expr, body, label, select_id):
    rows = find(f"/{entityset}?$select={select_id}&$filter={urllib.parse.quote(filter_expr)}")
    if rows:
        req('PATCH', f"/{entityset}({rows[0][select_id]})", body)
        print(f"  actualizado: {label}")
        return rows[0][select_id]
    created = req('POST', f"/{entityset}", body)
    print(f"  creado: {label}")
    return created[select_id]


content = base64.b64encode(open(os.environ['ASSEMBLY'], 'rb').read()).decode()

asm_id = upsert('pluginassemblies', "name eq 'CashHub.Plugins'", {
    'name': 'CashHub.Plugins', 'content': content,
    'isolationmode': 2, 'sourcetype': 0,
}, 'pluginassembly CashHub.Plugins', 'pluginassemblyid')

msg = {m: one('sdkmessages', f"name eq '{m}'", 'sdkmessageid') for m in ('Create', 'Update', 'Delete')}

DEPOSITO = 'sanic_chtbldepositsrecounting'
CHEQUE = 'sanic_chtblchecks'
ARQUEO = 'sanic_chtblcountedmoneydetails'
TULA = 'sanic_chtblbags'
SOLICITUD = 'sanic_chtblapprovalrequest'
SESION = 'sanic_chtblrecountingsession'

# (clase, friendly, corto,
#  [(entidad, mensaje, stage, rank, filteringattributes, preimage_attrs, sufijo, descripcion)])
#
# El sufijo desambigua pasos que comparten entidad+mensaje y solo difieren en
# stage: sin el, los dos steps tendrian el MISMO nombre y el upsert por nombre
# pisaria uno con el otro (bug silencioso: el segundo paso nunca existiria).
PLUGINS = [
    ('CashHub.Plugins.RecountingSessionAutoApprovePlugin',
     'CashHub Recounting Session Auto Approve', 'RecountingAutoApprove', [
         (SOLICITUD, 'Create', 20, 20, None, None, ' (Pre-op)',
          'Auto-aprueba la solicitud de sesion de recuento cuando la mesa referenciada es '
          'la del propio solicitante, verificado server-side (00 §2.2 punto 1, mata S-1).'),
         (SOLICITUD, 'Create', 40, 20, None, None, ' (Post-op)',
          'Confirma la sesion de recuento (Confirmed + ConfirmedBy) cuando la solicitud '
          'se auto-aprobo en el paso Pre-op.'),
     ]),
    ('CashHub.Plugins.BagRecountClosurePlugin',
     'CashHub Bag Recount Closure', 'BagRecountClosure', [
         (TULA, 'Update', 20, 40, 'sanic_bagstatus', 'sanic_bagstatus', ' (totales)',
          'Recalcula TotalNIO/USD/EUR de la tula desde los depositos persistidos al entrar '
          'a Recontada o a Pendiente de revision - recuento (00 §2.2 punto 2, mata S-2a).'),
         (TULA, 'Update', 40, 40, 'sanic_bagstatus', 'sanic_bagstatus', ' (asientos)',
          'Crea un CH-Tbl-Transactions por deposito al entrar a Recontada, de forma '
          'IDEMPOTENTE (00 §2.2 punto 3, mata S-2b).'),
     ]),
    ('CashHub.Plugins.GuardRecountingPlugin',
     'CashHub Guard Recounting', 'GuardRecounting', [
         (DEPOSITO, 'Create', 10, 5, None, None, '',
          'No se crean depositos de recuento sobre una tula que no este Asignada a cajero.'),
         (DEPOSITO, 'Update', 10, 5, None, 'sanic_bagid', '',
          'No se editan depositos de una tula fuera de la mesa; el re-colgado exige que la '
          'tula destino tambien este Asignada a cajero.'),
         (DEPOSITO, 'Delete', 10, 5, None, 'sanic_bagid', '',
          'No se borran depositos de una tula fuera de la mesa.'),
         (CHEQUE, 'Create', 10, 5, None, None, '',
          'No se crean cheques sobre una tula que no este Asignada a cajero.'),
         (CHEQUE, 'Update', 10, 5, None, 'sanic_relatedrecounterdepositid', '',
          'No se editan cheques de una tula fuera de la mesa. EXENTO el Update que toca '
          'solo sanic_checkstatus (cierre de tula, 04 §11.3): esa transicion la gobierna '
          'CheckGuard via GuardTransactionPlugin.'),
         (CHEQUE, 'Delete', 10, 5, None, 'sanic_relatedrecounterdepositid', '',
          'No se borran cheques de una tula fuera de la mesa.'),
         (ARQUEO, 'Create', 10, 5, None, None, '',
          'No se crean detalles de arqueo sobre una tula que no este Asignada a cajero.'),
         (ARQUEO, 'Update', 10, 5, None, 'sanic_relateddepositrecountingid', '',
          'No se editan detalles de arqueo de una tula fuera de la mesa.'),
         (ARQUEO, 'Delete', 10, 5, None, 'sanic_relateddepositrecountingid', '',
          'No se borran detalles de arqueo de una tula fuera de la mesa.'),
         (SOLICITUD, 'Delete', 10, 5, None, 'createdby,sanic_approvalstatus', '',
          'Baja acotada (D02-4): solo solicitudes PROPIAS y SIN resolver. La resuelta queda '
          'como registro de la decision del supervisor.'),
         (SESION, 'Delete', 10, 5, None, 'sanic_cashier,sanic_confirmed', '',
          'Baja acotada (D02-4): solo sesiones PROPIAS (sanic_cashier), sin Confirmed y sin '
          'solicitud resuelta que las referencie.'),
     ]),
]

for typename, friendly, short, steps in PLUGINS:
    type_id = upsert('plugintypes', f"typename eq '{typename}'", {
        'typename': typename, 'friendlyname': friendly, 'name': typename,
        'pluginassemblyid@odata.bind': f"/pluginassemblies({asm_id})",
    }, f"plugintype {typename.split('.')[-1]}", 'plugintypeid')

    for entity, message, stage, rank, filtering, image_attrs, suffix, description in steps:
        flt = one('sdkmessagefilters',
                  f"primaryobjecttypecode eq '{entity}' and _sdkmessageid_value eq {msg[message]}",
                  'sdkmessagefilterid')
        name = f"CashHub.{short}: {message} de {entity}{suffix}"
        body = {
            'name': name,
            'description': description,
            'plugintypeid@odata.bind': f"/plugintypes({type_id})",
            'sdkmessageid@odata.bind': f"/sdkmessages({msg[message]})",
            'sdkmessagefilterid@odata.bind': f"/sdkmessagefilters({flt})",
            'stage': stage, 'mode': 0, 'rank': rank, 'supporteddeployment': 0,
            'asyncautodelete': False, 'invocationsource': 0,
        }
        if filtering:
            body['filteringattributes'] = filtering
        step_id = upsert('sdkmessageprocessingsteps', f"name eq '{name}'", body, name,
                         'sdkmessageprocessingstepid')

        if image_attrs:
            upsert('sdkmessageprocessingstepimages',
                   f"_sdkmessageprocessingstepid_value eq {step_id} and entityalias eq 'PreImage'", {
                       'name': 'PreImage', 'entityalias': 'PreImage',
                       'imagetype': 0, 'messagepropertyname': 'Target',
                       'attributes': image_attrs,
                       'sdkmessageprocessingstepid@odata.bind': f"/sdkmessageprocessingsteps({step_id})",
                   }, f"pre-image de {name}", 'sdkmessageprocessingstepimageid')

print("Registro del guard de recounting completo.")
PYEOF
