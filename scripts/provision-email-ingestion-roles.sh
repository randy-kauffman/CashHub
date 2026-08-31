#!/usr/bin/env bash
# =============================================================================
# Provisiona (idempotente) los 2 roles nuevos del proceso 02-recepcion-ingesta-emails,
# hoy sin ningun rol de seguridad definido. Deploy quirurgico por Web API,
# SIN import de solucion (hay cambios en el ambiente que el repo local no tiene
# y un import completo arriesga pisarlos).
#
#   1. CH-SR-EmailIngestionFlow    -> conexion Dataverse de los Cloud Flows de
#      ingesta (IngestEmails/ProcessEmail/MailboxSweep/Reply to client email).
#      El motor de validacion (ValidateReceivedEmailPlugin) corre como SYSTEM,
#      asi que este rol NO gobierna la validacion en si, solo lo que los flujos
#      tocan directo: alta/update de ReceivedEmails+Attachments y el heartbeat.
#   2. CH-SR-EmailOperationsConsole -> humanos de "Operaciones" en la consola
#      (reproceso, reenvio de respuesta, resuelto por fuera, override manual).
#      Solo lectura salvo Write sobre ReceivedEmails para esas acciones.
#      ValidationResults queda SOLO lectura a proposito (el doc dice
#      explicitamente "no editar a mano" — lo mantiene el plugin).
#
# PrivilegeDepth enum del Web API: Basic/Local/Deep/Global (string).
# depthmask de verificacion: Basic=1 Local=2 Deep=4 Global=8.
#
# Pendiente fuera de este script (no es un Security Role): sanic_sourceemailaddress
# tiene IsSecured=1 — EmailOperationsConsole va a necesitar ademas un Field
# Security Profile para leerlo, una vez que sepamos que team/usuarios lo usan.
#
# Requiere pp_secrets.env. Uso: ./scripts/provision-email-ingestion-roles.sh
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
import json, os, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Prefer: return=representation']
DEPTH_GLOBAL = 'Global'

ROLES = [
    {
        'name': 'CH-SR-EmailIngestionFlow',
        'description': (
            'Conexion Dataverse de los Cloud Flows de ingesta de correos '
            '(IngestEmails/ProcessEmail/MailboxSweep/Reply to client email). '
            'El motor de validacion corre como SYSTEM: este rol NO gobierna VR-01..2X, '
            'solo lo que los flujos tocan directo (alta/update de ReceivedEmails+Attachments '
            'y el heartbeat de MailboxSweep).'
        ),
        'grants': {
            'sanic_chtblreceivedemails': ['Create', 'Read', 'Write', 'Append', 'AppendTo'],
            'sanic_chtblemailsattachments': ['Create', 'Read', 'Append'],
            'sanic_chtblsystemheartbeat': ['Create', 'Read', 'Write'],
        },
    },
    {
        'name': 'CH-SR-EmailOperationsConsole',
        'description': (
            'Humanos de Operaciones en la consola de recepcion de correos: '
            'reproceso, reenvio de respuesta, resuelto por fuera, override manual. '
            'Solo lectura salvo Write sobre ReceivedEmails para esas acciones. '
            'ValidationResults queda solo lectura a proposito (lo mantiene el plugin, '
            'no se edita a mano). Requiere ademas un Field Security Profile aparte '
            'para leer sanic_sourceemailaddress (IsSecured=1) — pendiente.'
        ),
        'grants': {
            'sanic_chtblreceivedemails': ['Read', 'Write'],
            'sanic_chtblemailsattachments': ['Read'],
            'sanic_chtblvalidationruns': ['Read'],
            'sanic_chtblvalidationresults': ['Read'],
            'sanic_validationrules': ['Read'],
            'sanic_chtblclientemail': ['Read'],
            'sanic_chtblemailtemplate': ['Read'],
            'sanic_chtblplaybook': ['Read'],
            'sanic_chtbldeclaration': ['Read'],
            'sanic_chtbldeclaredbags': ['Read'],
            'sanic_chtbldeclareddeposits': ['Read'],
            'sanic_chtbldeclaredchecks': ['Read'],
            # LA BARRA DE SALUD DE LA CONSOLA LEE EL LATIDO. La pantalla arranca con
            # LookUp('CH-Tbl-SystemHeartbeat', Name = "ingesta-correos") en OnVisible, y sin
            # este Read la app abre con un error rojo de red antes de mostrar nada. Es Read
            # y nada mas: el latido lo escribe MailboxSweep con CH-SR-EmailIngestionFlow.
            'sanic_chtblsystemheartbeat': ['Read'],
            # EL AVISO A BANCA RESUELVE EL BUZON DESTINO POR VARIABLE DE ENTORNO
            # (sanic_CHEVBancaMailbox y sanic_CHEVAppUrl_ClientAdmin). Sin este Read el
            # LookUp devuelve blanco y el boton acusa "Falta configurar CH-EV-BancaMailbox",
            # que manda a revisar una configuracion que en realidad esta bien puesta.
            #
            # VA LA DEFINICION, NO EL VALOR: prvReadEnvironmentVariableValue NO EXISTE en
            # Dataverse. El acceso a environmentvariablevalue se gobierna por el privilegio
            # de environmentvariabledefinition. Pedir el del valor solo imprime un aviso de
            # privilegio no encontrado y no otorga nada.
            'environmentvariabledefinition': ['Read'],
        },
    },
]


def req(method, path, body=None, extra=None):
    cmd = ['curl', '-s', *H, *(extra or []), '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message', '')[:400]}")
    return d


def find(path):
    return req('GET', path).get('value', [])


root_bu = find("/businessunits?$select=businessunitid&$filter=" +
               urllib.parse.quote('_parentbusinessunitid_value eq null'))[0]['businessunitid']

schema_cache = {}


def schema_name(table):
    if table not in schema_cache:
        meta = req('GET', f"/EntityDefinitions(LogicalName='{table}')?$select=SchemaName")
        schema_cache[table] = meta['SchemaName']
    return schema_cache[table]


def privilege_id(op, table):
    priv_name = 'prv' + op + schema_name(table)
    rows = find(f"/privileges?$select=privilegeid&$filter=" +
                urllib.parse.quote(f"name eq '{priv_name}'"))
    if not rows:
        rows = [r for r in find(f"/privileges?$select=privilegeid,name&$filter=" +
                urllib.parse.quote(f"contains(name,'{table}')"))
                if r['name'].lower().startswith('prv' + op.lower())]
    if not rows:
        print(f"  !! privilegio no encontrado: {priv_name} — revisar a mano")
        return None
    return rows[0]['privilegeid']


results = {}
for role_def in ROLES:
    role_name = role_def['name']
    print(f"\n=== {role_name} ===")
    roles = find(f"/roles?$select=roleid&$filter=" +
                 urllib.parse.quote(f"name eq '{role_name}' and _businessunitid_value eq {root_bu}"))
    if roles:
        role_id = roles[0]['roleid']
        print(f"rol existente ({role_id})")
    else:
        created = req('POST', '/roles', {
            'name': role_name,
            'businessunitid@odata.bind': f"/businessunits({root_bu})",
            'description': role_def['description'],
        }, extra=['-H', 'MSCRM.SolutionUniqueName: CashHubCore'])
        role_id = created['roleid']
        print(f"rol creado ({role_id})")

    req('POST', '/AddSolutionComponent', {
        'ComponentType': 20, 'ComponentId': role_id,
        'SolutionUniqueName': 'CashHubCore', 'AddRequiredComponents': False,
    })
    print('agregado a CashHubCore')

    privileges = []
    granted = []
    for table, ops in role_def['grants'].items():
        for op in ops:
            pid = privilege_id(op, table)
            if pid is None:
                continue
            privileges.append({'PrivilegeId': pid, 'Depth': DEPTH_GLOBAL})
            granted.append((op, table, pid))
            print(f"  prv{op} Global: {table}")

    if privileges:
        req('POST', f"/roles({role_id})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
            {'Privileges': privileges})
    print(f"privilegios otorgados: {len(privileges)}")
    results[role_name] = (role_id, granted)

print("\n=== VERIFICACION (depthmask esperado 8=Global) ===")
for role_name, (role_id, granted) in results.items():
    print(f"\n{role_name} ({role_id}):")
    for op, table, pid in granted:
        r = find(f"/roleprivilegescollection?$filter=" +
                 urllib.parse.quote(f"roleid eq {role_id} and privilegeid eq {pid}"))
        mask = r[0]['privilegedepthmask'] if r else None
        print(f"  prv{op} {table}: depthmask {mask}")
PYEOF
