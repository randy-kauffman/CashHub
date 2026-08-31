#!/usr/bin/env bash
# =============================================================================
# Remediacion de ROLES del plan docs/supervisor-rebuild/00-analisis-y-plan.md §3
# (items 3.1 a 3.5). Converge los roles del ENTORNO contra el XML desunpackeado.
#
# -----------------------------------------------------------------------------
# MECANISMO — declarado (§3 pide decidirlo segun el patron real del repo)
# -----------------------------------------------------------------------------
# La FUENTE DE VERDAD es solutions/unpacked/core/Roles/*.xml. El repo ya viene
# cambiando roles ahi y viajando por solution import (commits aef5887 y 79f1a27,
# "importados y verificados"). Este script NO reemplaza ese camino: lo COMPLETA.
#
#   * ALTAS de privilegio y ALTAS de tabla viajan bien por solution import.
#   * BAJAS de privilegio y BAJAS de profundidad (Global -> Local) NO viajan de
#     forma confiable: el import de un rol es ADITIVO sobre el rol que ya existe
#     en destino. Un privilegio que sacas del XML sigue otorgado en el entorno,
#     y una profundidad que bajas en el XML puede quedar en la de destino.
#
# Y esta remediacion es, en su mayor parte, BAJAS: 5 privilegios fuera de Counter,
# 13 profundidades Global -> Local en Counter, y 5 prvReadPluginTraceLog repartidos.
# Confiarla solo al import dejaria la brecha ABIERTA con el repo diciendo que esta
# cerrada — el peor estado posible para un control de gobierno.
#
# Por eso el script converge cada rol con **ReplacePrivilegesRole**, alimentado
# por el propio XML: el rol del entorno queda EXACTAMENTE con el set del repo
# (altas, bajas y profundidades en una sola operacion, idempotente).
#
# La AUDITORIA por columna (§3.5) NO se toca por Web API: es metadato de solucion
# y viaja por import. El script solo la VERIFICA (GET) y reporta.
#
# -----------------------------------------------------------------------------
# QUE CAMBIA (contra el XML del repo, ya editado)
# -----------------------------------------------------------------------------
# CH-SR-Counter (147 -> 142 privilegios: 5 bajas, 13 cambios de profundidad)
#   - QUITA prvCreatesanic_CHTblApprovalRequest y prvWritesanic_CHTblApprovalRequest
#     -> cierra la auto/co-aprobacion por Web API sin escribir una linea de codigo.
#   - QUITA prvDeletesanic_CHTblRecountingSession y prvDeletesanic_CHTblCountedMoneyDetails.
#   - QUITA prvReadPluginTraceLog.
#   - BAJA a Local: Read de ApprovalRequest; Read/Write de Bags; y TODO
#     RecountingSession y CountedMoneyDetails (Append/AppendTo/Create/Read/Write).
#
# CH-SR-CountingSupervisor (113 -> 130 privilegios: 18 altas, 1 baja)
#   - QUITA prvReadPluginTraceLog.
#   - AGREGA (Global) lo que la app patchea y el rol no autorizaba — hoy esas
#     ramas fallan MUDAS: Delete/Append/AppendTo de Bags; AppendTo de Receptions;
#     Append/AppendTo de RecountingSession; Write/Append/AppendTo de DeclaredBags;
#     Create/Read/Write/Append/AppendTo de RecountingSessionDetail (el rol tenia
#     CERO sobre esa tabla); Append de Transactions; Write/AppendTo de
#     DepositsRecounting; y prvReadsanic_CHTblClient.
#   - prvReadsanic_CHTblClient SI va: la guia 03 resuelve el cliente con
#     Coalesce(ReceptionID.RemitentClient.Name, ReceptionID.RemitentName, "—"),
#     o sea el camino primario es la tabla Client (CH-Tbl-Clients es datasource
#     #12 de 01-app.md §2). Sin el privilegio la pantalla no rompe pero degrada
#     al texto plano. Es lectura de nombre: Global es aceptable.
#
# CH-SR-Hub (ex CH-Role) (83 -> 82) / CH-SR-ClientAdminContributor (67 -> 66) /
# CH-SR-ClientAdminApprover (67 -> 66):
#   QUITA prvReadPluginTraceLog (cada uno -1).
#   Queda SOLO en CH-SR-ConfigAdmin, que desde 2026-08-31 es el rol de soporte
#   explicito (antes lo era CH-SR-OpsAdmin, eliminado en la limpieza).
#   Motivo: los trace logs son semi-publicos y arrastran payloads de negocio;
#   leerlos no es una necesidad operativa de cajero, supervisor ni analista.
#
# -----------------------------------------------------------------------------
# USO
# -----------------------------------------------------------------------------
#   ./scripts/apply-supervisor-role-remediation.sh            # DRY-RUN (default)
#   ./scripts/apply-supervisor-role-remediation.sh --apply    # escribe
#
# OJO: a diferencia del resto de los scripts del repo, aca el default es DRY-RUN.
# Es un script que REVOCA privilegios: ver el diff antes de aplicarlo no es
# opcional. El dry-run imprime el diff exacto por rol y no escribe nada.
#
# Requiere pp_secrets.env. Corre desde la raiz del repo (lee los XML por ruta).
# Idempotente: correrlo dos veces deja el mismo resultado y el segundo diff vacio.
# =============================================================================
set -euo pipefail

MODE="${1:-}"
if [ -n "${MODE}" ] && [ "${MODE}" != "--apply" ] && [ "${MODE}" != "--dry-run" ]; then
  echo "uso: $0 [--apply|--dry-run]   (default: --dry-run)" >&2
  exit 2
fi

source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL APPLY_MODE="${MODE}"

python3 - <<'PYEOF'
import json, os, re, subprocess, sys, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json']
APPLY = os.environ.get('APPLY_MODE') == '--apply'
ROLES_DIR = 'solutions/unpacked/core/Roles'

# Roles tocados por §3.
# Actualizado 2026-08-31 por la limpieza de componentes muertos:
#   - CH-SR-Counter -> renombrado a CH-SR-Cashier (mismo GUID)
#   - CH-SR-AnalyticsRead -> eliminado (0 privilegios de negocio, 0 asignaciones)
#   - CH-SR-OpsAdmin -> eliminado. Era el unico portador de prvReadPluginTraceLog;
#     ese privilegio y los otros 27 de LECTURA de su delta pasaron a
#     CH-SR-ConfigAdmin, que es hoy el rol de soporte y diagnostico. Los 37 de
#     autoria sobre Workflow/NewProcess NO se transfirieron a proposito.
# ConfigAdmin NO va en esta lista: conserva prvReadPluginTraceLog a proposito, igual
# que antes lo hacia OpsAdmin, y esta remediacion no lo cambia.
ROLE_FILES = [
    'CH-SR-Cashier', 'CH-SR-CountingSupervisor', 'CH-SR-Hub',
    'CH-SR-ClientAdminContributor', 'CH-SR-ClientAdminApprover',
]

# Auditoria por columna (§3.5): viaja por solution import — aca solo se VERIFICA.
AUDIT_EXPECTED = [
    ('sanic_chtbltransactions', 'sanic_baclocalcheckamount'),
    ('sanic_chtbltransactions', 'sanic_total'),
    ('sanic_chtblapprovalrequest', 'sanic_solvedby'),
]

DEPTHS = ('Basic', 'Local', 'Deep', 'Global')


def req(method, path, body=None):
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *H, '-X', method, API + path]
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout.rsplit('\n', 1)
    finally:
        if tmp: os.unlink(tmp.name)
    code = out[-1].strip()
    d = json.loads(out[0]) if out[0].strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path} [{code}]: {d['error'].get('message', '')[:400]}")
    if code and not code.startswith('2'):
        raise RuntimeError(f"{method} {path}: HTTP {code}")
    return d


def find(path):
    return req('GET', path).get('value', [])


def norm_depth(v):
    """El Web API devuelve Depth como nombre de enum; algunos builds como int."""
    if isinstance(v, int):
        return DEPTHS[v]
    s = str(v).split('.')[-1]
    if s.isdigit():
        return DEPTHS[int(s)]
    return s


def desired_from_xml(role_name):
    """El XML desunpackeado ES el contrato. Se lee tal cual, sin interpretar."""
    with open(f'{ROLES_DIR}/{role_name}.xml', encoding='utf-8') as fh:
        xml = fh.read()
    pairs = re.findall(r'<RolePrivilege\s+name="([^"]+)"\s+level="([^"]+)"\s*/>', xml)
    out = {}
    for name, level in pairs:
        if level not in DEPTHS:
            raise RuntimeError(f'{role_name}: level desconocido "{level}" en {name}')
        if name in out:
            raise RuntimeError(f'{role_name}: privilegio duplicado {name}')
        out[name] = level
    if not out:
        raise RuntimeError(f'{role_name}: el XML no tiene RolePrivilege — abortando '
                           'antes de vaciar un rol del entorno')
    return out


# --- resolucion de nombre de privilegio -> GUID (cache global) ---------------
PRIV_CACHE = {}


def privilege_id(name):
    if name not in PRIV_CACHE:
        rows = find('/privileges?$select=privilegeid,name&$filter=' +
                    urllib.parse.quote(f"name eq '{name}'"))
        PRIV_CACHE[name] = rows[0]['privilegeid'] if rows else None
    return PRIV_CACHE[name]


root_bu = find('/businessunits?$select=businessunitid&$filter=' +
               urllib.parse.quote('_parentbusinessunitid_value eq null'))[0]['businessunitid']

print('=' * 78)
print('MODO:', 'APPLY (escribe)' if APPLY else 'DRY-RUN (no escribe nada)')
print('Fuente de verdad:', ROLES_DIR)
print('=' * 78)

problems = []
plans = []

for role_name in ROLE_FILES:
    desired = desired_from_xml(role_name)

    roles = find('/roles?$select=roleid&$filter=' +
                 urllib.parse.quote(f"name eq '{role_name}' and _businessunitid_value eq {root_bu}"))
    if not roles:
        problems.append(f'{role_name}: NO existe en la BU raiz del entorno')
        print(f'\n## {role_name}: no existe en el entorno — se salta')
        continue
    role_id = roles[0]['roleid']

    current = {}
    for rp in req('GET', f'/RetrieveRolePrivilegesRole(RoleId={role_id})') \
            .get('RolePrivileges', []):
        current[rp['PrivilegeId']] = norm_depth(rp.get('Depth'))

    # Resolucion: todo nombre del XML tiene que existir en el entorno. Si uno no
    # resuelve se ABORTA el rol: con ReplacePrivilegesRole, un nombre no resuelto
    # no es "un privilegio menos", es un privilegio REVOCADO en silencio.
    payload, unresolved, by_id = [], [], {}
    for name, depth in sorted(desired.items()):
        pid = privilege_id(name)
        if not pid:
            unresolved.append(name)
            continue
        by_id[pid] = (name, depth)
        payload.append({'PrivilegeId': pid, 'Depth': depth})

    if unresolved:
        problems.append(f'{role_name}: privilegios del XML que NO existen en el entorno: '
                        + ', '.join(unresolved))
        print(f'\n## {role_name}: {len(unresolved)} privilegio(s) sin resolver — se SALTA')
        for n in unresolved:
            print(f'   !! {n}')
        continue

    adds   = [(n, d) for pid, (n, d) in by_id.items() if pid not in current]
    changes = [(n, current[pid], d) for pid, (n, d) in by_id.items()
               if pid in current and current[pid] != d]
    removes = [pid for pid in current if pid not in by_id]

    # nombre legible de lo que se revoca (una consulta por privilegio, son pocos)
    remove_names = []
    for pid in removes:
        try:
            remove_names.append(req('GET', f'/privileges({pid})?$select=name').get('name', pid))
        except RuntimeError:
            remove_names.append(pid)

    print(f'\n## {role_name}  ({len(current)} en entorno -> {len(payload)} en repo)')
    for n, d in sorted(adds):
        print(f'   + {n} [{d}]')
    for n, o, d in sorted(changes):
        print(f'   ~ {n} [{o} -> {d}]')
    for n in sorted(remove_names):
        print(f'   - {n}')
    if not (adds or changes or removes):
        print('   (ya converge — sin cambios)')

    plans.append((role_name, role_id, payload, len(adds) + len(changes) + len(removes)))

# --- escritura ---------------------------------------------------------------
if APPLY:
    print('\n' + '=' * 78)
    for role_name, role_id, payload, delta in plans:
        if delta == 0:
            print(f'{role_name}: sin cambios')
            continue
        # ReplacePrivilegesRole deja el rol EXACTAMENTE con este set: es la unica
        # forma de que bajas y profundidades converjan en una sola operacion.
        req('POST', f'/roles({role_id})/Microsoft.Dynamics.CRM.ReplacePrivilegesRole',
            {'Privileges': payload})
        print(f'{role_name}: aplicado ({len(payload)} privilegios, {delta} diferencias)')

    print('\n--- verificacion post-escritura ---')
    for role_name, role_id, payload, _ in plans:
        got = {rp['PrivilegeId']: norm_depth(rp.get('Depth')) for rp in
               req('GET', f'/RetrieveRolePrivilegesRole(RoleId={role_id})')
               .get('RolePrivileges', [])}
        want = {p['PrivilegeId']: p['Depth'] for p in payload}
        if got == want:
            print(f'  OK  {role_name}: {len(got)} privilegios, converge con el repo')
        else:
            problems.append(f'{role_name}: NO converge tras aplicar '
                            f'(entorno {len(got)} vs repo {len(want)})')
            print(f'  BAD {role_name}: entorno {len(got)} vs repo {len(want)}')
else:
    print('\n(dry-run: no se escribio nada. Re-corre con --apply para aplicar.)')

# --- §3.5 auditoria: solo verificacion, viaja por solution import ------------
print('\n' + '=' * 78)
print('AUDITORIA POR COLUMNA (§3.5) — viaja por solution import, aca solo se verifica')

org = find('/organizations?$select=isauditenabled')
org_on = bool(org and org[0].get('isauditenabled'))
print(f"  organization.isauditenabled = {org_on}"
      + ('' if org_on else '   <-- OFF: con esto en OFF NO se graba NADA (§7)'))
if not org_on:
    problems.append('organization.isauditenabled = OFF: la auditoria por columna no graba nada')

for table, column in AUDIT_EXPECTED:
    try:
        meta = req('GET', f"/EntityDefinitions(LogicalName='{table}')/Attributes"
                          f"(LogicalName='{column}')?$select=IsAuditEnabled")
        val = meta.get('IsAuditEnabled', {})
        on = bool(val.get('Value')) if isinstance(val, dict) else bool(val)
        print(f"  {'OK ' if on else 'PEND'} {table}.{column} IsAuditEnabled={on}"
              + ('' if on else '  <-- pendiente de solution import'))
    except RuntimeError as e:
        print(f"  !!  {table}.{column}: {e}")

print('\n' + '=' * 78)
if problems:
    print('PENDIENTES / PROBLEMAS:')
    for p in problems:
        print('  - ' + p)
    sys.exit(1)
print('Sin pendientes.')
PYEOF
