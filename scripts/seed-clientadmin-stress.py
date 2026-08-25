#!/usr/bin/env python3
"""Seeder de ESTRES del dominio de ADMINISTRACION DE CLIENTES para
CH-App-ClientAdministration v2.

Hermano de `seed-digitization-stress.py` / `seed-recounting-stress.py`: MISMA
arquitectura ($batch con UN changeset por request, marcadores rastreables,
fases `--only-*`, reintentos con backoff ante 429/503, progreso por consola,
auth por `pp_secrets.env`). Lo que cambia es el dominio: clientes + sus 3
tablas hijas (sucursal/correo/cuenta) y el circuito completo de RFC (Request
For Change) con sus lineas y bitacora de auditoria.

    OBJETIVO DE VOLUMEN (medir DELEGACION, paginacion, badges y bandeja):
      F1 clientes   : ~1.500 sanic_chtblclients (93% Activo, 5% Inactivo,
                      2% PendingApproval — los 3 segmentos que la app filtra).
      F2 hijos      : 1-4 sucursales, 1-3 correos (~20% alwaysnotify),
                      0-3 cuentas POR CLIENTE — decenas de miles de filas para
                      reventar CountRows() delegado en los sub-grids.
      F3 RFCs       : ~600 sanic_chtblclientdatachangerequests repartidos en
                      4 segmentos (bandeja del aprobador, borradores, historial
                      resuelto con bitacora, cancelados) + sus lineas
                      (sanic_chtblclientdatachangerequestlines) referenciando
                      filas REALES del catalogo LogicalFieldNames.

-----------------------------------------------------------------------------
EL MARCADOR: TODO ES IDENTIFICABLE Y LIMPIABLE SIN CRUZAR TABLAS
-----------------------------------------------------------------------------
Cada tabla lleva SU PROPIO marcador textual filtrable server-side, para que
`--wipe` nunca necesite reconstruir el arbol cliente->hijos leyendo miles de
lookups:
  - Cliente  : sanic_name empieza con 'STRESS-' (startswith, nvarchar).
  - Sucursal : sanic_name empieza con 'STRESS-' (sin alternate key propia:
               SOLO se crean sucursales de un cliente RECIEN creado esta
               corrida — ver "IDEMPOTENCIA" abajo — para no duplicarlas sin
               control en una corrida repetida).
  - Correo   : sanic_name (=direccion) termina en '@stress.example.com'
               (alternate key compuesta sanic_client+sanic_name: unica por
               cliente, y nuestras direcciones son ademas UNICAS GLOBALES).
  - Cuenta   : sanic_name (=numero) empieza con '9990' (alternate key GLOBAL
               sanic_KeyNumeroDeCuenta sobre sanic_name sola). Prefijo de 4
               digitos (no 2) a proposito: un numero real de 10 digitos que
               empiece por 2 digitos fijos tiene 1/100 de chance de colisionar
               con datos organicos; con 4 digitos fijos baja a 1/10.000.
  - RFC      : sanic_reason empieza con '[STRESS]' (nvarchar, requerido,
               siempre presente).
  - Linea / Bitacora: NO tienen marcador textual propio (linea = ntext en
               oldvalue/newvalue sin alternate key; bitacora = sanic_message
               es ntext y Dataverse Web API NO soporta $filter sobre memo).
               Se identifican SIEMPRE por membresia: "lineas cuyo
               sanic_changerequestid apunta a un RFC marcado" / "bitacora cuya
               sanic_relatedrfc apunta a un RFC marcado". El wipe arma esa
               membresia con un filtro OR troceado en lotes de 15 GUIDs
               (`_sanic_changerequestid_value eq {id} or ...`), nunca trayendo
               la tabla entera.

-----------------------------------------------------------------------------
GUARDS DEL BACKEND v2 — QUE SE RESPETA POR CONSTRUCCION Y QUE NECESITA BYPASS
-----------------------------------------------------------------------------
Fuente de verdad leida antes de escribir una linea de este script:
  src/backend/CashHub.Validation/Security/ChangeRequestApprovalGuard.cs
  src/backend/CashHub.Validation/Security/ChangeRequestLineGuard.cs
  src/backend/CashHub.Plugins/GuardChangeRequestApprovalPlugin.cs
  src/backend/CashHub.Plugins/GuardChangeRequestLinePlugin.cs
  src/backend/CashHub.Plugins/ResolveChangeRequestPlugin.cs (flujo real de
    aplicacion — se clona su formato de mensaje de bitacora al pie de la letra)

  GuardChangeRequestLinePlugin — Create/Update/Delete de una LINEA de RFC
      Regla real (ChangeRequestLineGuard.Evaluate): el RFC PADRE debe estar en
      Draft (159460000); si no, bloquea — SALVO que la escritura venga de otro
      plugin del mismo pipeline (depth > 1, `IsWriteExempt`), que es como
      ResolveChangeRequestPlugin marca sus propias lineas al resolver un RFC
      real. Este script, corriendo por Web API, SIEMPRE entra a depth 1 (igual
      que un humano o la app canvas): por eso la secuencia es OBLIGATORIA
      "cabecera en Draft -> crear lineas -> PATCH cabecera a Submitted", NUNCA
      al reves. Una vez que la cabecera sale de Draft, cualquier Update/Delete
      posterior de sus lineas (marcar Applied/Denied/Failed, o el --wipe)
      vuelve a caer bajo el guard y EXIGE bypass.

  GuardChangeRequestApprovalPlugin — Create/Update de la CABECERA de RFC
      Regla real (ChangeRequestApprovalGuard.Evaluate), leida linea por linea:
        - Create: bloquea SOLO si sanic_changestatus nace en un estado
          RESUELTO (Approved/Rejected/Applied/Failed). Draft, Submitted y
          Canceled SI pueden nacer directo — el guard no los toca. Por eso los
          50 RFC "Canceled" de F3 se crean en UN SOLO Create, sin pasar por
          Draft ni por lineas.
        - Update: SOLO evalua algo cuando `newStatus` es Approved o Rejected
          (`if (newStatus != Approved && newStatus != Rejected) return
          Allow();`). Es decir Draft->Submitted, Submitted->Applied,
          Submitted->Failed y cualquier ->Canceled pasan LIBRES para
          cualquier actor (incluido el SP) sin evaluar nada mas.
        - Solo Approved/Rejected exigen: estado previo == Submitted, actor !=
          creador, Y actor miembro AAD real del grupo Approver de la tabla
          objetivo (ChangeRequestApproverResolver, Fase 2). El SP no es
          miembro de ningun grupo AAD de negocio: NO puede resolver un RFC
          por las buenas.

      🧨 Consecuencia poco intuitiva (verificada en el guard, no asumida): de
      las 4 transiciones de "cierre" (Applied/Rejected/Failed/Canceled), SOLO
      Rejected esta realmente bloqueada para el SP a nivel de CABECERA
      (Approved tambien, pero este script nunca pasa por Approved: salta
      Submitted -> {Applied|Rejected|Failed} directo, que es legal para
      Applied/Failed). Aun asi este script pide el bypass
      `MSCRM.BypassCustomPluginExecution: true` para LAS TRES transiciones de
      cierre (Applied/Rejected/Failed) de forma UNIFORME, por dos motivos
      reales, no cosmeticos:
        1. Las LINEAS de esos RFC si estan bloqueadas SIEMPRE (GuardChange-
           RequestLinePlugin no distingue Applied/Rejected/Failed: el padre ya
           no esta en Draft, punto) — sin bypass no se pueden marcar
           Applied/Denied/Failed en las lineas ni escribir la bitacora que las
           referencia.
        2. Uniformidad y auditabilidad: una sola politica de escritura para
           "todo el cierre historico de un RFC" es mas facil de razonar y de
           auditar en logs que memorizar cual de las 3 transiciones "en
           realidad no lo necesitaba". El SP tiene el privilegio
           prvBypassCustomPlugins (es admin del entorno DEV): usar el bypass
           aca es intencional y esta confinado a estas escrituras via el
           flag `bypass=True` explicito en cada llamada — nunca se usa en
           F1/F2 (clientes/hijos) ni en Draft/Submitted/Canceled de F3.
      Si preferis no tocar el bypass en absoluto, `--no-bypass` omite TODO el
      segmento de historial resuelto (Applied/Rejected/Failed) y reparte esos
      RFC como Submitted en su lugar (bandeja mas grande, cero bitacora).

-----------------------------------------------------------------------------
IDEMPOTENCIA (razonable, no perfecta — documentada donde no lo es)
-----------------------------------------------------------------------------
Semilla de `random` FIJA (misma logica que los otros seeders del repo): con
`--clients N` fijo, la corrida SIEMPRE genera la MISMA secuencia de nombres,
CIFBCO/CIFCOM y numeros de cuenta. Antes de crear, el script consulta contra
Dataverse (via un filtro OR troceado, NUNCA trayendo la tabla entera) cuales
de esos valores YA EXISTEN y los salta:
  - Cliente : se saltea por sanic_cifbco (alternate key). Si el cliente YA
    existia, tambien se SALTEAN sus sucursales (sin alternate key: crearlas
    de nuevo las duplicaria sin control) — se asume que una corrida previa ya
    las sembro completas. Los correos y las cuentas de ese cliente SI se
    reintentan (tienen alternate key propia: un duplicado se detecta y
    saltea, nunca se duplica).
  - Correo  : se saltea por (sanic_client, sanic_name) — no aplica si el
    cliente es nuevo esta corrida.
  - Cuenta  : se saltea por sanic_name (numero, alternate key GLOBAL).
  - RFC     : NO es idempotente — no tiene alternate key, y cada corrida
    referencia clientes/hijos "actuales" al azar. Correr `--only-rfcs` dos
    veces AGREGA 600 RFC mas. Usar `--wipe` antes de resembrar limpio.
Ademas de la pre-verificacion, cualquier 409/"duplicate" que igual llegue (p.
ej. dos corridas concurrentes) se clasifica como saltear-y-seguir-contando,
nunca como error fatal.

-----------------------------------------------------------------------------
Uso:
    python3 scripts/seed-clientadmin-stress.py --dry-run              # plan, cero escrituras
    python3 scripts/seed-clientadmin-stress.py --all
    python3 scripts/seed-clientadmin-stress.py --only-clients         # clientes + hijos
    python3 scripts/seed-clientadmin-stress.py --only-rfcs            # requiere clientes ya sembrados
    python3 scripts/seed-clientadmin-stress.py --clients 3000 --rfcs 1200 --all
    python3 scripts/seed-clientadmin-stress.py --no-bypass --only-rfcs
    python3 scripts/seed-clientadmin-stress.py --verify
    python3 scripts/seed-clientadmin-stress.py --wipe
Requiere pp_secrets.env en la raiz. NO correr en produccion.
"""
import argparse
import http.client
import json
import os
import random
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta, timezone

# Semilla FIJA a proposito: sostiene la idempotencia por candidato descrita
# arriba (misma corrida --clients N siempre genera los mismos CIF/cuentas).
random.seed(20260825)

# ============================================================ marcadores propios
MARK_CLIENT = 'STRESS-'                # sanic_name de Cliente y Sucursal
EMAIL_DOMAIN = 'stress.example.com'    # sanic_name (direccion) de Correo
ACCOUNT_PREFIX = '9990'                # sanic_name (numero) de Cuenta, 10 digitos
RFC_MARK = '[STRESS]'                  # sanic_reason del RFC

# ================================================================ option sets
# sanic_chchrecordstatus — GLOBAL, comun a Client/ClientBranch/ClientEmail/
# ClientAccount (Schema.cs: MasterRecordStatus). OJO ClientAccount tiene
# ADEMAS un campo legacy sanic_chchrecordstatus (mismo nombre de option set,
# columna DISTINTA) que Schema.cs NO usa — no se toca en este script.
RS_ACTIVE = 159460000
RS_INACTIVE = 159460001
RS_PENDING = 159460002
RS_LABEL = {RS_ACTIVE: 'Activo', RS_INACTIVE: 'Inactivo', RS_PENDING: 'PendingApproval'}

# sanic_changestatus — RFC (cabecera) y linea comparten el MISMO esquema de
# valores (ChangeRequestApprovalGuard.cs). La linea llama 159460003 "Denied"
# en vez de "Rejected" pero es el mismo codigo numerico.
CS_DRAFT = 159460000
CS_SUBMITTED = 159460001
CS_APPROVED = 159460002
CS_REJECTED = 159460003   # linea: "Denied"
CS_APPLIED = 159460004
CS_FAILED = 159460005
CS_CANCELED = 159460006

# sanic_type (RFC) — Schema.cs
TYPE_UPDATE = 159460000
TYPE_DEACTIVATE = 159460001
TYPE_CREATE = 159460002
TYPE_ACTIVATE = 159460003
TYPE_LABEL = {TYPE_UPDATE: 'Update', TYPE_DEACTIVATE: 'Deactivate',
              TYPE_CREATE: 'Create', TYPE_ACTIVATE: 'Activate'}

# sanic_opeationtype (bitacora) — Schema.ChangeRequestAuditTrail.MapOperationType
# 🧨 el ORDEN NO coincide con sanic_type salvo en Activate: se mapea EXPLICITO.
OP_CREATE = 159460000
OP_UPDATE = 159460001
OP_DEACTIVATE = 159460002
OP_ACTIVATE = 159460003
MAP_TYPE_TO_OPERATION = {
    TYPE_CREATE: OP_CREATE, TYPE_UPDATE: OP_UPDATE,
    TYPE_DEACTIVATE: OP_DEACTIVATE, TYPE_ACTIVATE: OP_ACTIVATE,
}

# sanic_datatype (catalogo LogicalFieldName)
DT_TEXT = 159460000
DT_NUMBER = 159460001
DT_DATE = 159460002
DT_MONEY = 159460003
DT_LOOKUP = 159460004
DT_OPTIONSET = 159460005
DT_TWOOPTIONS = 159460006

# sanic_currency (ClientAccount) — mismos codigos que el mapa NIO/USD/EUR del catalogo
CUR_NIO, CUR_USD, CUR_EUR = 159460000, 159460001, 159460002
CURW = [CUR_NIO] * 70 + [CUR_USD] * 25 + [CUR_EUR] * 5

# ===================================================== entity sets / tablas
ES_CLIENT = 'sanic_chtblclients'
ES_BRANCH = 'sanic_chtblclientbranchs'
ES_EMAIL = 'sanic_chtblclientemails'
ES_ACCOUNT = 'sanic_chtblclientaccounts'
ES_RFC = 'sanic_chtblclientdatachangerequests'
ES_LINE = 'sanic_chtblclientdatachangerequestlines'
ES_AUDIT = 'sanic_chtblclientdatachangesaudittrails'
ES_FIELDDEF = 'sanic_chtblclienttablelogicalfieldnames'
ES_USER = 'systemusers'

PK = {
    ES_CLIENT: 'sanic_chtblclientid', ES_BRANCH: 'sanic_chtblclientbranchid',
    ES_EMAIL: 'sanic_chtblclientemailid', ES_ACCOUNT: 'sanic_chtblclientaccountid',
    ES_RFC: 'sanic_chtblclientdatachangerequestid', ES_LINE: 'sanic_chtblclientdatachangerequestlineid',
    ES_AUDIT: 'sanic_chtblclientdatachangesaudittrailid', ES_FIELDDEF: 'sanic_chtblclienttablelogicalfieldnameid',
    ES_USER: 'systemuserid',
}

# Tabla objetivo del RFC: logical name (sanic_targettable) -> plural/display,
# TAL COMO estan sembradas en el catalogo real (data/seed/10-clienttablelogicalfieldnames.json).
TABLES = {
    'sanic_chtblclient': {'plural': ES_CLIENT, 'display': 'CH-Tbl-Client'},
    'sanic_chtblclientbranch': {'plural': ES_BRANCH, 'display': 'CH-Tbl-ClientBranch'},
    'sanic_chtblclientemail': {'plural': ES_EMAIL, 'display': 'CH-Tbl-ClientEmail'},
    'sanic_chtblclientaccount': {'plural': ES_ACCOUNT, 'display': 'CH-Tbl-ClientAccount'},
}
TABLE_KEYS = list(TABLES.keys())

# ===========================================================================
# auth + http — patron identico al resto de los seeders del repo
# ===========================================================================
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
env = {}
with open(os.path.join(ROOT, os.environ.get('ENV_FILE', 'pp_secrets.env'))) as f:
    for line in f:
        line = line.strip().rstrip('\r')
        if line and not line.startswith('#') and '=' in line:
            k, v = line.split('=', 1)
            env[k.strip()] = v.strip()
URL = env['DATAVERSE_URL'].rstrip('/')
API_PATH = '/api/data/v9.2'
API = URL + API_PATH
_tok_lock = threading.Lock()
_token = {'v': None}

REINTENTOS = {}
_ret_lock = threading.Lock()
NOWRITE = {'on': False, 'n': 0}


def _reintento(causa):
    with _ret_lock:
        REINTENTOS[causa] = REINTENTOS.get(causa, 0) + 1


def mostrar_reintentos(titulo='REINTENTOS DE RED CONSUMIDOS'):
    print(f'\n  {titulo}: ' + (', '.join(f'{k}={v}' for k, v in sorted(REINTENTOS.items()))
                               or 'ninguno'))


def _fetch_token():
    body = urllib.parse.urlencode({
        'grant_type': 'client_credentials', 'client_id': env['PP_CLIENT_ID'],
        'client_secret': env['PP_CLIENT_SECRET'], 'scope': URL + '/.default'}).encode()
    d = json.load(urllib.request.urlopen(urllib.request.Request(
        f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token", data=body)))
    return d['access_token']


def token():
    with _tok_lock:
        if _token['v'] is None:
            _token['v'] = _fetch_token()
        return _token['v']


def req(method, path, body=None, retry=6, prefer=None):
    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + path.replace(' ', '%20'), method=method,
                                   data=json.dumps(body, ensure_ascii=False).encode()
                                   if body is not None else None)
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', 'application/json')
        r.add_header('Accept', 'application/json')
        if prefer:
            r.add_header('Prefer', prefer)
        try:
            with urllib.request.urlopen(r, timeout=300) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw.strip() else {}
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:400]
            last = f'{method} {path[:140]}: {e.code} {msg}'
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                _reintento('req/401')
                continue
            if e.code in (429, 503) and attempt < retry - 1:
                _reintento(f'req/{e.code}')
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            raise RuntimeError(last) from e
        except urllib.error.URLError as e:
            last = f'{method} {path[:140]}: {e}'
            if attempt < retry - 1:
                _reintento('req/urlerror')
                time.sleep(3)
                continue
            raise RuntimeError(last) from e
        except (TimeoutError, OSError, http.client.HTTPException) as e:
            last = f'{method} {path[:140]}: {type(e).__name__} {e}'
            if attempt < retry - 1:
                _reintento(f'req/{type(e).__name__}')
                time.sleep(5)
                continue
            raise RuntimeError(last) from e
    raise RuntimeError(last or 'sin reintentos')


def get_all(path, page=5000):
    rows, url = [], path
    while url:
        d = req('GET', url, prefer=f'odata.maxpagesize={page}')
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def count_of(entity_set, filtro):
    n, url = 0, f'/{entity_set}?$select={PK[entity_set]}&$filter={urllib.parse.quote(filtro)}'
    while url:
        d = req('GET', url, prefer='odata.maxpagesize=5000')
        n += len(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return n


def chunked(seq, n):
    seq = list(seq)
    for i in range(0, len(seq), n):
        yield seq[i:i + n]


def or_filter(field, values):
    return ' or '.join(f"{field} eq {v}" for v in values)


def or_filter_quoted(field, values):
    return ' or '.join(f"{field} eq '{v}'" for v in values)


def fetch_by_values(entity_set, field, values, select, quoted=False, chunk=15):
    """Trae filas cuyo `field` esta en `values`, troceando en OR de a `chunk`
    (evita URLs gigantes y evita traer la tabla entera). `field` puede ser un
    campo simple (nvarchar) o un lookup crudo (_sanic_x_value)."""
    out = []
    for grupo in chunked(values, chunk):
        if not grupo:
            continue
        flt = or_filter_quoted(field, grupo) if quoted else or_filter(field, grupo)
        out.extend(get_all(f'/{entity_set}?$select={select}&$filter=' + urllib.parse.quote(flt)))
    return out


# ===========================================================================
# $batch — un changeset POR REQUEST (igual a seed-digitization-stress.py);
# variante propia: agrupa por `bypass` (MSCRM.BypassCustomPluginExecution en
# el SOBRE del $batch) en vez de por `caller` — este seeder no impersona.
# ===========================================================================
def batch_exec(requests_, bypass=False, retry=6):
    """requests_ = [(method, path_relativo, body|None)]. Devuelve
    [(status:int, entity_id:str|None, error:str|None)] en ORDEN."""
    if not requests_:
        return []
    if NOWRITE['on']:
        with _ret_lock:
            NOWRITE['n'] += len(requests_)
        return [(204 if m != 'POST' else 201, str(uuid.uuid4()), None)
                for (m, _p, _b) in requests_]
    bb = 'batch_' + uuid.uuid4().hex
    parts = []
    for i, (method, path, body) in enumerate(requests_, 1):
        cs = 'cs_' + uuid.uuid4().hex
        payload = json.dumps(body, ensure_ascii=False) if body is not None else ''
        inner = (
            f'--{cs}\r\n'
            'Content-Type: application/http\r\n'
            'Content-Transfer-Encoding: binary\r\n'
            f'Content-ID: {i}\r\n\r\n'
            f'{method} {API_PATH}{path} HTTP/1.1\r\n'
            'Content-Type: application/json;type=entry\r\n'
            'Accept: application/json\r\n'
            + ('OData-Version: 4.0\r\n' if body is not None else '')
            + f'\r\n{payload}\r\n'
            f'--{cs}--\r\n')
        parts.append(f'--{bb}\r\n'
                     f'Content-Type: multipart/mixed; boundary={cs}\r\n\r\n'
                     + inner)
    payload = ''.join(parts) + f'--{bb}--\r\n'

    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + '/$batch', method='POST', data=payload.encode('utf-8'))
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', f'multipart/mixed; boundary={bb}')
        r.add_header('Accept', 'application/json')
        if bypass:
            # 🧨 SOLO para el cierre historico de RFC (header->Applied/Rejected/
            # Failed, sus lineas y su bitacora) y para el --wipe de lineas/
            # bitacora ya cerradas. El SP tiene prvBypassCustomPlugins (admin
            # de DEV). Ver docstring del modulo, seccion GUARDS.
            r.add_header('MSCRM.BypassCustomPluginExecution', 'true')
        try:
            with urllib.request.urlopen(r, timeout=600) as resp:
                return _parse_batch(resp.read().decode('utf-8'), len(requests_))
        except urllib.error.HTTPError as e:
            cuerpo = e.read().decode()
            if 'batchresponse' in cuerpo and 'HTTP/1.1 ' in cuerpo:
                return _parse_batch(cuerpo, len(requests_))
            msg = cuerpo[:400]
            last = f'$batch: {e.code} {msg}'
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                _reintento('batch/401')
                continue
            if e.code in (429, 503) and attempt < retry - 1:
                _reintento(f'batch/{e.code}')
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            break
        except urllib.error.URLError as e:
            last = f'$batch: {e}'
            if attempt < retry - 1:
                _reintento('batch/urlerror')
                time.sleep(3)
                continue
            break
        except (TimeoutError, OSError, http.client.HTTPException) as e:
            last = f'$batch: {type(e).__name__} {e}'
            if attempt < retry - 1:
                _reintento(f'batch/{type(e).__name__}')
                time.sleep(5)
                continue
            break
    return [(0, None, last)] * len(requests_)


def _parse_batch(text, expected):
    out, cur, in_headers, body_lines = [], None, False, []
    for raw in text.split('\n'):
        line = raw.rstrip('\r')
        if line.startswith('--') and ('batch' in line or 'cs_' in line or
                                      'changesetresponse' in line.lower()):
            if cur is not None:
                cur['body'] = '\n'.join(body_lines).strip()
                out.append(cur)
                cur = None
            body_lines = []
            in_headers = False
            continue
        if line.startswith('HTTP/1.1 '):
            if cur is not None:
                cur['body'] = '\n'.join(body_lines).strip()
                out.append(cur)
            try:
                code = int(line.split()[1])
            except (IndexError, ValueError):
                code = 0
            cur = {'status': code, 'id': None}
            body_lines = []
            in_headers = True
            continue
        if cur is None:
            continue
        if in_headers:
            if line.strip() == '':
                in_headers = False
                continue
            low = line.lower()
            if low.startswith('odata-entityid:'):
                v = line.split(':', 1)[1].strip()
                if '(' in v and v.endswith(')'):
                    cur['id'] = v[v.rfind('(') + 1:-1]
            continue
        body_lines.append(line)
    if cur is not None:
        cur['body'] = '\n'.join(body_lines).strip()
        out.append(cur)

    res = []
    for p in out:
        st = p['status']
        err = None
        if st == 0 or st >= 400:
            b = p.get('body') or ''
            try:
                err = json.loads(b)['error']['message'][:300]
            except Exception:                                          # noqa: BLE001
                err = (b[:300] or f'HTTP {st}')
        res.append((st, p.get('id'), err))
    if len(res) != expected:
        return [(0, None, f'respuesta $batch ilegible ({len(res)} partes de {expected})')] * expected
    return res


def is_duplicate_key_error(err):
    if not err:
        return False
    low = err.lower()
    return ('duplicate' in low or 'already exists' in low or 'ya existe' in low
            or 'unique' in low and 'key' in low)


class Progress:
    def __init__(self, total, label):
        self.total, self.label, self.done, self.err = total, label, 0, 0
        self.t0 = time.perf_counter()
        self.lock = threading.Lock()
        self.samples = []

    def add(self, ok, err, samples):
        with self.lock:
            self.done += ok + err
            self.err += err
            for s in samples:
                if len(self.samples) < 8:
                    self.samples.append(s)
            el = time.perf_counter() - self.t0
            rate = self.done / el if el else 0
            print(f'    {self.label}: {self.done}/{self.total} '
                  f'({rate:.1f} op/s, errores {self.err})', flush=True)


def run_batches(requests_, label, chunk=100, workers=6):
    """requests_ = [(method, path, body)] o [(method, path, body, bypass)].
    Agrupa por `bypass` (el header es del sobre $batch entero, no mezclable
    dentro de un mismo chunk), chunkea y ejecuta en paralelo. Devuelve
    (resultados_map {idx: (status,id,err)}, segundos)."""
    if not requests_:
        print(f'  {label}: nada que hacer')
        return {}, 0.0
    norm = []
    for idx, r in enumerate(requests_):
        if len(r) == 3:
            m, p, b = r
            byp = False
        else:
            m, p, b, byp = r
        norm.append((idx, m, p, b, byp))
    grupos = {}
    for idx, m, p, b, byp in norm:
        grupos.setdefault(byp, []).append((idx, m, p, b))
    tareas = []
    for byp, filas in grupos.items():
        for i in range(0, len(filas), chunk):
            tareas.append((byp, filas[i:i + chunk]))

    pr = Progress(len(norm), label)
    resultados = {}
    t0 = time.perf_counter()

    def _run(tarea):
        byp, filas = tarea
        reqs = [(m, p, b) for (_i, m, p, b) in filas]
        res = batch_exec(reqs, bypass=byp)
        return filas, res

    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = [ex.submit(_run, t) for t in tareas]
        for fut in as_completed(futs):
            filas, res = fut.result()
            errs = [r[2] for r in res if r[2]]
            for (idx, _m, _p, _b), r in zip(filas, res):
                resultados[idx] = r
            pr.add(len(res) - len(errs), len(errs), errs)
    dt = time.perf_counter() - t0
    n_err = sum(1 for r in resultados.values() if r[2])
    n_dup = sum(1 for r in resultados.values() if r[2] and is_duplicate_key_error(r[2]))
    print(f'  {label}: {len(resultados) - n_err} ok · {n_err - n_dup} errores · '
          f'{n_dup} duplicados-salteados · {dt:.1f}s '
          f'({len(resultados) / dt if dt else 0:.1f} op/s)')
    for s in pr.samples:
        if not is_duplicate_key_error(s):
            print(f'      !! {s}', file=sys.stderr)
    return resultados, dt


# ===========================================================================
# generadores de datos
# ===========================================================================
SECTORES = ['Distribuidora', 'Supermercados', 'Ferreteria', 'Farmacias', 'Transportes',
            'Comercial', 'Repuestos', 'Panaderia', 'Constructora', 'Agroindustrial',
            'Textiles', 'Importadora', 'Exportadora', 'Servicios', 'Maderas',
            'Licoreria', 'Papeleria', 'Electronica', 'Calzado', 'Mueblería'.replace('í', 'i')]
LUGARES = ['El Porvenir', 'La Colonia Norte', 'Central de Managua', 'San Benito',
           'del Lago', 'Las Segovias', 'del Pacifico', 'San Marcos', 'Nueva Esperanza',
           'El Rosario', 'Santa Rosa', 'Los Robles', 'del Norte', 'San Judas',
           'Bello Horizonte', 'Altagracia', 'Monte Fresco', 'Villa Fontana',
           'La Fuente', 'San Sebastian']
SUFIJOS = ['S.A.', 'Ltda.', 'S.A. de C.V.', 'y Compania']
SUCURSALES_CIUDADES = ['Metrocentro', 'Leon', 'Masaya', 'Esteli', 'Granada', 'Chinandega',
                       'Matagalpa', 'Rivas', 'Jinotega', 'Bluefields', 'Juigalpa', 'Ocotal',
                       'Managua Centro', 'Bello Horizonte', 'Ciudad Sandino']

RFC_REASONS = [
    'Actualizacion de datos de contacto', 'Correccion de CIF por error de digitacion',
    'Apertura de nueva sucursal', 'Cierre de sucursal por reestructuracion',
    'Cambio de cuenta bancaria destino', 'Actualizacion de correo de notificaciones',
    'Activacion de cliente reactivado', 'Desactivacion por mora',
    'Correccion de nombre comercial', 'Cambio de moneda operativa',
]
FAILED_ERRORS = [
    'No se pudo aplicar: el registro objetivo ya no existe (fue desactivado antes de resolver el RFC).',
    'No se pudo aplicar: el campo referenciado ya no esta habilitado para edicion (sanic_isallowed=false).',
    'No se pudo aplicar: conflicto de concurrencia, el registro fue modificado por otro proceso.',
    'No se pudo aplicar: el valor nuevo no es convertible al tipo de dato esperado por el catalogo.',
    'No se pudo aplicar: se perdio la conexion con el origen de datos durante la materializacion.',
]


def nombre_empresa():
    return f'{random.choice(SECTORES)} {random.choice(LUGARES)}, {random.choice(SUFIJOS)}'


def cifbco(seq):
    return f'9{seq:09d}'          # 10 digitos, ej. brief: '9XXXXXXXX secuencial'


def cifcom(seq):
    return f'J031{seq:010d}'      # 14 chars, mismo largo que la muestra real 'J0310000112233'


def account_number(seq):
    return f'{ACCOUNT_PREFIX}{seq:06d}'   # 10 digitos totales


def email_address(seq):
    return f'stress+{seq}@{EMAIL_DOMAIN}'


def iso(dt):
    return dt.strftime('%Y-%m-%dT%H:%M:%SZ')


# ===========================================================================
# catalogo LogicalFieldName — YA SEMBRADO, solo lectura
# ===========================================================================
def load_field_catalog():
    rows = get_all(
        f'/{ES_FIELDDEF}?$select={PK[ES_FIELDDEF]},sanic_table,sanic_logicalname,'
        f'sanic_datatype,sanic_optionsetmap,sanic_lookuptargettable,sanic_isallowed')
    by_table = {t: [] for t in TABLE_KEYS}
    for r in rows:
        t = r.get('sanic_table')
        if t in by_table and r.get('sanic_isallowed'):
            by_table[t].append(r)
    return by_table


def line_values(fielddef, relatedclient_id, all_client_ids, is_create):
    """(oldvalue, newvalue) coherentes con sanic_datatype. oldvalue=None se
    omite del body (RequiredLevel=recommended); newvalue SIEMPRE se devuelve
    (RequiredLevel=required en la linea)."""
    dt = fielddef.get('sanic_datatype')
    logical = (fielddef.get('sanic_logicalname') or '').lower()

    if dt == DT_OPTIONSET:
        try:
            mapa = json.loads(fielddef.get('sanic_optionsetmap') or '{}')
        except (ValueError, TypeError):
            mapa = {}
        labels = list(mapa.keys()) or ['NIO', 'USD', 'EUR']
        old, new = (random.sample(labels, 2) if len(labels) >= 2 else (labels[0], labels[0]))
        return (None if is_create else old), new

    if dt == DT_TWOOPTIONS:
        old, new = ('true', 'false') if random.random() < 0.5 else ('false', 'true')
        return (None if is_create else old), new

    if dt == DT_LOOKUP:
        old = relatedclient_id
        new = relatedclient_id or (random.choice(all_client_ids) if all_client_ids else None)
        if not is_create:
            otros = [c for c in all_client_ids if c != old]
            if otros:
                new = random.choice(otros)
        return (None if is_create else old), (new or '00000000-0000-0000-0000-000000000000')

    if dt in (DT_NUMBER, DT_MONEY):
        old, new = round(random.uniform(100, 90000), 2), round(random.uniform(100, 90000), 2)
        return (None if is_create else str(old)), str(new)

    if dt == DT_DATE:
        base = datetime(2026, 1, 1)
        old = base + timedelta(days=random.randint(0, 300))
        new = base + timedelta(days=random.randint(0, 300))
        return (None if is_create else old.strftime('%Y-%m-%d')), new.strftime('%Y-%m-%d')

    # DT_TEXT y cualquier tipo no contemplado: texto generico coherente con el campo
    if 'cif' in logical:
        old, new = cifbco(random.randint(0, 999_999_999)), cifbco(random.randint(0, 999_999_999))
    elif 'name' in logical or not logical:
        old = f'{nombre_empresa()} (anterior)'
        new = f'{nombre_empresa()} (actualizado)'
    else:
        old, new = 'Valor anterior de prueba', 'Valor nuevo de prueba'
    return (None if is_create else old), new


# ===========================================================================
# F1 — CLIENTES
# ===========================================================================
def plan_clients(n):
    n_pending = max(1, round(n * 0.02))
    n_inactive = max(1, round(n * 0.05))
    n_active = n - n_pending - n_inactive
    cand = []
    for seq in range(1, n + 1):
        if seq <= n_active:
            status = RS_ACTIVE
        elif seq <= n_active + n_inactive:
            status = RS_INACTIVE
        else:
            status = RS_PENDING
        cand.append({
            'seq': seq,
            'name': f'{MARK_CLIENT}{nombre_empresa()}',
            'cifbco': cifbco(seq),
            'cifcom': cifcom(seq),
            'status': status,
        })
    random.shuffle(cand)   # que los 3 segmentos no queden en bloques contiguos
    return cand


def seed_clients(a):
    print('\n' + '=' * 78)
    print(f'F1 — CLIENTES ({a.clients} objetivo, 93% Activo / 5% Inactivo / 2% PendingApproval)')
    print('=' * 78)
    t0 = time.perf_counter()

    candidatos = plan_clients(a.clients)
    existentes = fetch_by_values(
        ES_CLIENT, 'sanic_cifbco', [c['cifbco'] for c in candidatos],
        f'{PK[ES_CLIENT]},sanic_cifbco,sanic_name', quoted=True)
    ya = {r['sanic_cifbco']: r[PK[ES_CLIENT]] for r in existentes}
    print(f'  candidatos: {len(candidatos)} · ya sembrados (mismo CIFBCO): {len(ya)}')

    nuevos = [c for c in candidatos if c['cifbco'] not in ya]
    reqs = [('POST', f'/{ES_CLIENT}', {
        'sanic_name': c['name'], 'sanic_cifbco': c['cifbco'], 'sanic_cifcom': c['cifcom'],
        'sanic_recordstatus': c['status'],
    }) for c in nuevos]
    res, dt = run_batches(reqs, 'creando clientes', a.chunk, a.workers)

    clientes = []
    for c in candidatos:
        if c['cifbco'] in ya:
            clientes.append({'id': ya[c['cifbco']], 'seq': c['seq'], 'status': c['status'],
                             'nuevo': False})
    for c, idx in zip(nuevos, range(len(nuevos))):
        st, cid, err = res.get(idx, (0, None, 'sin resultado'))
        if err or not cid:
            continue
        clientes.append({'id': cid, 'seq': c['seq'], 'status': c['status'], 'nuevo': True})

    print(f'  clientes totales disponibles (nuevos + preexistentes): {len(clientes)}')
    print(f'  TOTAL F1 {time.perf_counter() - t0:.1f}s')
    return clientes


# ===========================================================================
# F2 — HIJOS: sucursales / correos / cuentas
# ===========================================================================
def seed_children(a, clientes):
    print('\n' + '=' * 78)
    print('F2 — SUCURSALES / CORREOS / CUENTAS (hijos de cada cliente)')
    print('=' * 78)
    t0 = time.perf_counter()

    # -------- sucursales: SOLO para clientes NUEVOS esta corrida (sin --------
    # alternate key propia: recrearlas para un cliente preexistente las
    # duplicaria sin control).
    branch_reqs = []
    email_cand = []   # [(client_id, address, alwaysnotify)]
    account_cand = []  # [(client_id, seq)]

    eseq = _next_email_seq()
    aseq = _next_account_seq()

    for c in clientes:
        if c['nuevo']:
            n_branches = random.randint(1, 4)
            for _ in range(n_branches):
                branch_reqs.append(('POST', f'/{ES_BRANCH}', {
                    'sanic_name': f"{MARK_CLIENT}Sucursal {random.choice(SUCURSALES_CIUDADES)}",
                    'sanic_Client@odata.bind': f'/{ES_CLIENT}({c["id"]})',
                    'sanic_recordstatus': RS_ACTIVE,
                }))

        n_emails = random.randint(1, 3)
        for _ in range(n_emails):
            email_cand.append((c['id'], email_address(eseq), random.random() < 0.20))
            eseq += 1

        n_accounts = random.randint(0, 3)
        for _ in range(n_accounts):
            account_cand.append((c['id'], aseq))
            aseq += 1

    print('\n== 1) SUCURSALES ==')
    res_b, _ = run_batches(branch_reqs, 'creando sucursales', a.chunk, a.workers)
    ok_b = sum(1 for r in res_b.values() if not r[2])
    print(f'  sucursales creadas: {ok_b}/{len(branch_reqs)} (solo clientes nuevos esta corrida)')

    print('\n== 2) CORREOS ==')
    existentes_email = set(r['sanic_name'] for r in fetch_by_values(
        ES_EMAIL, 'sanic_name', [e[1] for e in email_cand], f'{PK[ES_EMAIL]},sanic_name', quoted=True))
    email_reqs = [('POST', f'/{ES_EMAIL}', {
        'sanic_name': addr, 'sanic_Client@odata.bind': f'/{ES_CLIENT}({cid})',
        'sanic_alwaysnotify': notify, 'sanic_recordstatus': RS_ACTIVE,
    }) for (cid, addr, notify) in email_cand if addr not in existentes_email]
    res_e, _ = run_batches(email_reqs, 'creando correos', a.chunk, a.workers)
    ok_e = sum(1 for r in res_e.values() if not r[2])
    print(f'  correos creados: {ok_e}/{len(email_reqs)} '
          f'(saltados por ya-existir: {len(email_cand) - len(email_reqs)})')

    print('\n== 3) CUENTAS ==')
    numeros = [account_number(seq) for (_cid, seq) in account_cand]
    existentes_acc = set(r['sanic_name'] for r in fetch_by_values(
        ES_ACCOUNT, 'sanic_name', numeros, f'{PK[ES_ACCOUNT]},sanic_name', quoted=True))
    account_reqs = []
    for (cid, seq) in account_cand:
        numero = account_number(seq)
        if numero in existentes_acc:
            continue
        account_reqs.append(('POST', f'/{ES_ACCOUNT}', {
            'sanic_name': numero, 'sanic_Client@odata.bind': f'/{ES_CLIENT}({cid})',
            'sanic_currency': random.choice(CURW), 'sanic_recordstatus': RS_ACTIVE,
        }))
    res_a, _ = run_batches(account_reqs, 'creando cuentas', a.chunk, a.workers)
    ok_a = sum(1 for r in res_a.values() if not r[2])
    print(f'  cuentas creadas: {ok_a}/{len(account_reqs)} '
          f'(saltados por ya-existir: {len(account_cand) - len(account_reqs)})')

    print(f'  TOTAL F2 {time.perf_counter() - t0:.1f}s')
    return {'sucursales': ok_b, 'correos': ok_e, 'cuentas': ok_a}


def _next_email_seq():
    rows = get_all(f'/{ES_EMAIL}?$select=sanic_name&$filter='
                   + urllib.parse.quote(f"contains(sanic_name,'@{EMAIL_DOMAIN}')"))
    top = 0
    for r in rows:
        addr = r.get('sanic_name') or ''
        local = addr.split('@')[0]
        if local.startswith('stress+') and local[7:].isdigit():
            top = max(top, int(local[7:]))
    return top + 1


def _next_account_seq():
    rows = get_all(f'/{ES_ACCOUNT}?$select=sanic_name&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{ACCOUNT_PREFIX}')"))
    top = 0
    for r in rows:
        v = (r.get('sanic_name') or '')[len(ACCOUNT_PREFIX):]
        if v.isdigit():
            top = max(top, int(v))
    return top + 1


# ===========================================================================
# F3 — RFCs (cabecera + lineas + cierre historico con bypass)
# ===========================================================================
def fetch_target_pools():
    """Pools de registros REALES marcados STRESS- para usar como target de un
    RFC Update/Activate/Deactivate. {tabla_logica: [{'id':..,'client_id':..}]}"""
    clientes = get_all(f'/{ES_CLIENT}?$select={PK[ES_CLIENT]}&$filter='
                       + urllib.parse.quote(f"startswith(sanic_name,'{MARK_CLIENT}')"))
    branches = get_all(f'/{ES_BRANCH}?$select={PK[ES_BRANCH]},_sanic_client_value&$filter='
                       + urllib.parse.quote(f"startswith(sanic_name,'{MARK_CLIENT}')"))
    emails = get_all(f'/{ES_EMAIL}?$select={PK[ES_EMAIL]},_sanic_client_value&$filter='
                     + urllib.parse.quote(f"contains(sanic_name,'@{EMAIL_DOMAIN}')"))
    accounts = get_all(f'/{ES_ACCOUNT}?$select={PK[ES_ACCOUNT]},_sanic_client_value&$filter='
                       + urllib.parse.quote(f"startswith(sanic_name,'{ACCOUNT_PREFIX}')"))
    return {
        'sanic_chtblclient': [{'id': r[PK[ES_CLIENT]], 'client_id': r[PK[ES_CLIENT]]} for r in clientes],
        'sanic_chtblclientbranch': [{'id': r[PK[ES_BRANCH]], 'client_id': r.get('_sanic_client_value')}
                                    for r in branches],
        'sanic_chtblclientemail': [{'id': r[PK[ES_EMAIL]], 'client_id': r.get('_sanic_client_value')}
                                   for r in emails],
        'sanic_chtblclientaccount': [{'id': r[PK[ES_ACCOUNT]], 'client_id': r.get('_sanic_client_value')}
                                     for r in accounts],
    }


def load_solvers(n=10):
    """Usuarios reales para sanic_solvedby del cierre historico (bypass evita
    que el guard verifique membresia AAD; se usa un usuario real igual, para
    que la bitacora no muestre al SP resolviendose a si mismo)."""
    rows = get_all(f'/{ES_USER}?$select=systemuserid,fullname'
                   f'&$filter=isdisabled eq false and applicationid eq null'
                   f'&$orderby=fullname asc&$top={n}')
    return [r['systemuserid'] for r in rows] or [req('GET', '/WhoAmI()')['UserId']]


def plan_rfcs(total):
    """Reparte `total` en los 4 segmentos del brief (200/100/250/50 de 600),
    y dentro del resuelto separa ~20 Failed + Applied/Rejected 70/30. El
    ultimo segmento (Canceled) absorbe el resto del redondeo."""
    n_submitted = round(total * 200 / 600)
    n_draft = round(total * 100 / 600)
    n_resolved = round(total * 250 / 600)
    n_canceled = total - n_submitted - n_draft - n_resolved
    if n_canceled < 0:
        n_canceled = 0
        n_resolved = total - n_submitted - n_draft

    n_failed = max(0, round(n_resolved * 20 / 250)) if n_resolved else 0
    n_rest = n_resolved - n_failed
    n_applied = round(n_rest * 0.7)
    n_rejected = n_rest - n_applied

    buckets = (['submitted'] * n_submitted + ['draft'] * n_draft
              + ['applied'] * n_applied + ['rejected'] * n_rejected
              + ['failed'] * n_failed + ['canceled'] * n_canceled)
    random.shuffle(buckets)
    return buckets, {'submitted': n_submitted, 'draft': n_draft, 'applied': n_applied,
                     'rejected': n_rejected, 'failed': n_failed, 'canceled': n_canceled}


def seed_rfcs(a):
    print('\n' + '=' * 78)
    print(f'F3 — RFCs ({a.rfcs} objetivo) — cabecera + lineas + cierre historico')
    print('=' * 78)
    t0 = time.perf_counter()

    catalogo = load_field_catalog()
    pools = fetch_target_pools()
    if not pools['sanic_chtblclient']:
        raise RuntimeError(
            f"no hay clientes '{MARK_CLIENT}*' en el entorno — correr antes --only-clients")
    all_client_ids = [c['id'] for c in pools['sanic_chtblclient']]
    solvers = load_solvers()

    buckets, resumen = plan_rfcs(a.rfcs)
    if a.no_bypass:
        # Sin bypass: el historial resuelto se degrada a Submitted (bandeja
        # mas grande, cero bitacora) — ver docstring del modulo.
        n_hist = resumen['applied'] + resumen['rejected'] + resumen['failed']
        buckets = [b if b not in ('applied', 'rejected', 'failed') else 'submitted' for b in buckets]
        resumen['submitted'] += n_hist
        resumen['applied'] = resumen['rejected'] = resumen['failed'] = 0
        print('  --no-bypass: historial resuelto omitido, degradado a Submitted')

    print('  reparto: ' + ' · '.join(f'{k}={v}' for k, v in resumen.items()))

    plan = []
    for bucket in buckets:
        tipo = random.choice([TYPE_UPDATE, TYPE_UPDATE, TYPE_CREATE, TYPE_ACTIVATE, TYPE_DEACTIVATE])
        tabla = random.choice(TABLE_KEYS)
        pool = pools.get(tabla, [])
        if tipo != TYPE_CREATE and not pool:
            tipo = TYPE_CREATE   # sin targets disponibles: se degrada a Create
        target = None if tipo == TYPE_CREATE else random.choice(pool)
        if tipo == TYPE_CREATE and tabla == 'sanic_chtblclient':
            relatedclient = None
        elif tipo == TYPE_CREATE:
            relatedclient = random.choice(all_client_ids)
        else:
            relatedclient = target['client_id'] or random.choice(all_client_ids)
        plan.append({'bucket': bucket, 'type': tipo, 'table': tabla, 'target': target,
                     'relatedclient': relatedclient})

    # -------- 1) cabeceras: TODAS nacen Draft, SALVO 'canceled' (guard las --
    # deja nacer resueltas... salvo Approved/Rejected/Applied/Failed; Canceled
    # SI puede nacer directo — ver docstring GUARDS).
    print('\n== 1) CABECERAS ==')
    header_reqs = []
    for p in plan:
        info = TABLES[p['table']]
        body = {
            'sanic_reason': f'{RFC_MARK} {random.choice(RFC_REASONS)} (lote de estres)',
            'sanic_type': p['type'],
            'sanic_targettable': p['table'],
            'sanic_targettabledisplayname': info['display'],
            'sanic_targettablepluralname': info['plural'],
            'sanic_changestatus': CS_CANCELED if p['bucket'] == 'canceled' else CS_DRAFT,
        }
        if p['target'] is not None:
            body['sanic_targetrecordid'] = p['target']['id']
        if p['relatedclient']:
            body['sanic_RelatedClient@odata.bind'] = f'/{ES_CLIENT}({p["relatedclient"]})'
        header_reqs.append(('POST', f'/{ES_RFC}', body))
    res_h, _ = run_batches(header_reqs, 'creando cabeceras RFC', a.chunk, a.workers)

    for p, idx in zip(plan, range(len(plan))):
        st, rid, err = res_h.get(idx, (0, None, 'sin resultado'))
        p['id'] = rid if not err else None
    ok_h = sum(1 for p in plan if p['id'])
    print(f'  cabeceras creadas: {ok_h}/{len(plan)}')
    plan = [p for p in plan if p['id']]

    # -------- 2) lineas: solo mientras el padre esta Draft (todo menos --------
    # 'canceled', que nacio Canceled y el guard ya no deja crearle lineas).
    print('\n== 2) LINEAS ==')
    line_reqs, line_meta = [], []
    for p in plan:
        if p['bucket'] == 'canceled':
            continue
        candidatos = catalogo.get(p['table'], [])
        if not candidatos:
            continue
        k = min(random.randint(1, 3), len(candidatos))
        elegidos = random.sample(candidatos, k)
        is_create = p['type'] == TYPE_CREATE
        for fd in elegidos:
            old, new = line_values(fd, p['relatedclient'], all_client_ids, is_create)
            body = {
                'sanic_ChangeRequestID@odata.bind': f'/{ES_RFC}({p["id"]})',
                'sanic_FieldDefinition@odata.bind': f'/{ES_FIELDDEF}({fd[PK[ES_FIELDDEF]]})',
                'sanic_newvalue': new,
                'sanic_changestatus': CS_DRAFT,
            }
            if old is not None:
                body['sanic_oldvalue'] = old
            line_reqs.append(('POST', f'/{ES_LINE}', body))
            line_meta.append({'rfc': p, 'old': old, 'new': new})
    res_l, _ = run_batches(line_reqs, 'creando lineas', a.chunk, a.workers)
    ok_l = 0
    for meta, idx in zip(line_meta, range(len(line_meta))):
        st, lid, err = res_l.get(idx, (0, None, 'sin resultado'))
        if err or not lid:
            continue
        ok_l += 1
        meta['id'] = lid
        meta['rfc'].setdefault('lines_ok', []).append(meta)
    print(f'  lineas creadas: {ok_l}/{len(line_reqs)}')

    # -------- 3) Draft -> Submitted: todos salvo 'draft' y 'canceled'. --------
    # Libre para cualquier actor (ChangeRequestApprovalGuard solo evalua
    # Approved/Rejected en Update) — SIN bypass.
    print('\n== 3) Draft -> Submitted ==')
    submit_reqs, submit_targets = [], []
    for p in plan:
        if p['bucket'] in ('draft', 'canceled'):
            continue
        submit_reqs.append(('PATCH', f'/{ES_RFC}({p["id"]})', {'sanic_changestatus': CS_SUBMITTED}))
        submit_targets.append(p)
    res_s, _ = run_batches(submit_reqs, 'RFC -> Submitted', a.chunk, a.workers)
    ok_s = sum(1 for r in res_s.values() if not r[2])
    print(f'  a Submitted: {ok_s}/{len(submit_reqs)}')

    # -------- 4) cierre historico (Applied/Rejected/Failed) — CON bypass. -----
    resueltos = [p for p in plan if p['bucket'] in ('applied', 'rejected', 'failed')]
    if resueltos:
        print(f"\n== 4) CIERRE HISTORICO ({len(resueltos)} RFC — bypass activo) ==")
        _resolver_historico(a, resueltos, solvers)

    print(f'  TOTAL F3 {time.perf_counter() - t0:.1f}s')
    return resumen


def _resolver_historico(a, resueltos, solvers):
    now = datetime.now(timezone.utc)
    header_reqs, header_meta = [], []
    line_reqs = []
    audit_reqs = []

    for p in resueltos:
        solver = random.choice(solvers)
        solved_on = now - timedelta(days=random.randint(0, 45), hours=random.randint(0, 23))
        body = {
            'sanic_SolvedBy@odata.bind': f'/{ES_USER}({solver})',
            'sanic_solvedon': iso(solved_on),
        }
        op = MAP_TYPE_TO_OPERATION[p['type']]

        if p['bucket'] == 'applied':
            body['sanic_changestatus'] = CS_APPLIED
            body['sanic_applyon'] = iso(solved_on)
            body['sanic_applynotes'] = 'Aplicado por lote de estres (bypass, ver docstring del seeder).'
            body['sanic_reasonfordecision'] = 'Cumple los requisitos de aprobacion.'
            line_status, decision_label, result = CS_APPLIED, 'Aplicado', True
        elif p['bucket'] == 'rejected':
            body['sanic_changestatus'] = CS_REJECTED
            body['sanic_reasonfordecision'] = 'No cumple los requisitos de aprobacion (dato de estres).'
            line_status, decision_label, result = CS_REJECTED, 'Rechazado', True
        else:  # failed
            error_msg = random.choice(FAILED_ERRORS)
            body['sanic_changestatus'] = CS_FAILED
            body['sanic_applynotes'] = error_msg
            body['sanic_reasonfordecision'] = error_msg
            line_status, decision_label, result = CS_FAILED, 'Fallido', False

        header_reqs.append(('PATCH', f'/{ES_RFC}({p["id"]})', body, True))
        header_meta.append(p)

        lineas_ok = p.get('lines_ok', [])
        for meta in lineas_ok:
            line_reqs.append(('PATCH', f'/{ES_LINE}({meta["id"]})', {
                'sanic_changestatus': line_status,
                'sanic_SolvedBy@odata.bind': f'/{ES_USER}({solver})',
                'sanic_solvedon': iso(solved_on),
            }, True))

        if p['bucket'] == 'failed':
            msg = f'{RFC_MARK} RFC {p["id"]}: {decision_label} — {error_msg}'[:1900]
            audit = {'sanic_message': msg, 'sanic_opeationtype': op,
                    'sanic_RelatedRFC@odata.bind': f'/{ES_RFC}({p["id"]})',
                    'sanic_result': False}
            if p['relatedclient']:
                audit['sanic_RelatedClient@odata.bind'] = f'/{ES_CLIENT}({p["relatedclient"]})'
            audit_reqs.append(('POST', f'/{ES_AUDIT}', audit, True))
        else:
            for meta in lineas_ok:
                msg = (f'{RFC_MARK} RFC {p["id"]} linea {meta["id"]}: {decision_label} '
                      f"('{meta['old']}' -> '{meta['new']}').")[:1900]
                audit = {'sanic_message': msg, 'sanic_opeationtype': op,
                        'sanic_RelatedRFC@odata.bind': f'/{ES_RFC}({p["id"]})',
                        'sanic_RelatedCRL@odata.bind': f'/{ES_LINE}({meta["id"]})',
                        'sanic_result': result}
                if p['relatedclient']:
                    audit['sanic_RelatedClient@odata.bind'] = f'/{ES_CLIENT}({p["relatedclient"]})'
                audit_reqs.append(('POST', f'/{ES_AUDIT}', audit, True))

    res_h, _ = run_batches(header_reqs, 'cerrando cabeceras (bypass)', a.chunk, a.workers)
    ok_h = sum(1 for r in res_h.values() if not r[2])
    print(f'  cabeceras cerradas: {ok_h}/{len(header_reqs)}')

    res_l, _ = run_batches(line_reqs, 'cerrando lineas (bypass)', a.chunk, a.workers)
    ok_l = sum(1 for r in res_l.values() if not r[2])
    print(f'  lineas cerradas: {ok_l}/{len(line_reqs)}')

    res_a, _ = run_batches(audit_reqs, 'creando bitacora (bypass)', a.chunk, a.workers)
    ok_a = sum(1 for r in res_a.values() if not r[2])
    print(f'  filas de bitacora creadas: {ok_a}/{len(audit_reqs)}')


# ===========================================================================
# VERIFY
# ===========================================================================
def verify(a):
    print('\n== VERIFICACION ==')
    n_cli = count_of(ES_CLIENT, f"startswith(sanic_name,'{MARK_CLIENT}')")
    n_cli_act = count_of(ES_CLIENT, f"startswith(sanic_name,'{MARK_CLIENT}') and sanic_recordstatus eq {RS_ACTIVE}")
    n_cli_ina = count_of(ES_CLIENT, f"startswith(sanic_name,'{MARK_CLIENT}') and sanic_recordstatus eq {RS_INACTIVE}")
    n_cli_pen = count_of(ES_CLIENT, f"startswith(sanic_name,'{MARK_CLIENT}') and sanic_recordstatus eq {RS_PENDING}")
    print(f'  clientes STRESS-*                : {n_cli}  '
          f'(Activo={n_cli_act} · Inactivo={n_cli_ina} · PendingApproval={n_cli_pen})')

    n_branch = count_of(ES_BRANCH, f"startswith(sanic_name,'{MARK_CLIENT}')")
    n_email = count_of(ES_EMAIL, f"contains(sanic_name,'@{EMAIL_DOMAIN}')")
    n_account = count_of(ES_ACCOUNT, f"startswith(sanic_name,'{ACCOUNT_PREFIX}')")
    print(f'  sucursales STRESS-*               : {n_branch}')
    print(f'  correos *@{EMAIL_DOMAIN}      : {n_email}')
    print(f'  cuentas {ACCOUNT_PREFIX}*                    : {n_account}')

    n_rfc = count_of(ES_RFC, f"startswith(sanic_reason,'{RFC_MARK}')")
    por_estado = {}
    for label, code in (('Draft', CS_DRAFT), ('Submitted', CS_SUBMITTED), ('Applied', CS_APPLIED),
                        ('Rejected', CS_REJECTED), ('Failed', CS_FAILED), ('Canceled', CS_CANCELED)):
        por_estado[label] = count_of(
            ES_RFC, f"startswith(sanic_reason,'{RFC_MARK}') and sanic_changestatus eq {code}")
    print(f'  RFC [STRESS]*                     : {n_rfc}')
    print('    ... por estado: ' + ' · '.join(f'{k}={v}' for k, v in por_estado.items()))

    ids = [r[PK[ES_RFC]] for r in get_all(
        f'/{ES_RFC}?$select={PK[ES_RFC]}&$filter=' + urllib.parse.quote(f"startswith(sanic_reason,'{RFC_MARK}')"))]
    lineas = fetch_by_values(ES_LINE, '_sanic_changerequestid_value', ids, PK[ES_LINE])
    bitacora = fetch_by_values(ES_AUDIT, '_sanic_relatedrfc_value', ids, PK[ES_AUDIT])
    print(f'  lineas de esos RFC                : {len(lineas)}')
    print(f'  filas de bitacora de esos RFC      : {len(bitacora)}')


# ===========================================================================
# WIPE — borra TODO lo sembrado por este seeder
# ===========================================================================
def wipe(a):
    print('\n== WIPE (STRESS-* / [STRESS]*) ==')

    ids = [r[PK[ES_RFC]] for r in get_all(
        f'/{ES_RFC}?$select={PK[ES_RFC]}&$filter=' + urllib.parse.quote(f"startswith(sanic_reason,'{RFC_MARK}')"))]
    print(f'  RFC [STRESS]* encontrados: {len(ids)}')

    if ids:
        bitacora = fetch_by_values(ES_AUDIT, '_sanic_relatedrfc_value', ids, PK[ES_AUDIT])
        print(f'  bitacora vinculada: {len(bitacora)}')
        reqs = [('DELETE', f'/{ES_AUDIT}({r[PK[ES_AUDIT]]})', None, True) for r in bitacora]
        run_batches(reqs, 'borrando bitacora (bypass)', a.chunk, a.workers)

        lineas = fetch_by_values(ES_LINE, '_sanic_changerequestid_value', ids, PK[ES_LINE])
        print(f'  lineas vinculadas: {len(lineas)}')
        # bypass: el padre puede NO estar en Draft (Submitted/Applied/Rejected/
        # Failed) y GuardChangeRequestLinePlugin bloquearia el Delete.
        reqs = [('DELETE', f'/{ES_LINE}({r[PK[ES_LINE]]})', None, True) for r in lineas]
        run_batches(reqs, 'borrando lineas (bypass)', a.chunk, a.workers)

        reqs = [('DELETE', f'/{ES_RFC}({rid})', None) for rid in ids]
        res, _ = run_batches(reqs, 'borrando cabeceras RFC', a.chunk, a.workers)
        ok = sum(1 for r in res.values() if not r[2])
        print(f'  cabeceras RFC borradas: {ok}/{len(ids)}')

    for label, es, filtro in (
        ('sucursales', ES_BRANCH, f"startswith(sanic_name,'{MARK_CLIENT}')"),
        ('correos', ES_EMAIL, f"contains(sanic_name,'@{EMAIL_DOMAIN}')"),
        ('cuentas', ES_ACCOUNT, f"startswith(sanic_name,'{ACCOUNT_PREFIX}')"),
    ):
        filas = get_all(f'/{es}?$select={PK[es]}&$filter=' + urllib.parse.quote(filtro))
        print(f'  {label} a borrar: {len(filas)}')
        reqs = [('DELETE', f'/{es}({r[PK[es]]})', None) for r in filas]
        res, _ = run_batches(reqs, f'borrando {label}', a.chunk, a.workers)
        ok = sum(1 for r in res.values() if not r[2])
        print(f'  {label} borrados: {ok}/{len(filas)}')

    clientes = get_all(f'/{ES_CLIENT}?$select={PK[ES_CLIENT]}&$filter='
                       + urllib.parse.quote(f"startswith(sanic_name,'{MARK_CLIENT}')"))
    print(f'  clientes a borrar: {len(clientes)}')
    reqs = [('DELETE', f'/{ES_CLIENT}({r[PK[ES_CLIENT]]})', None) for r in clientes]
    res, _ = run_batches(reqs, 'borrando clientes', a.chunk, a.workers)
    ok = sum(1 for r in res.values() if not r[2])
    print(f'  clientes borrados: {ok}/{len(clientes)}')


# ===========================================================================
# main
# ===========================================================================
def build_args():
    ap = argparse.ArgumentParser(
        description='Seeder de estres de CH-App-ClientAdministration v2.')
    ap.add_argument('--all', action='store_true', help='F1+F2 (clientes+hijos) y F3 (RFCs)')
    ap.add_argument('--only-clients', action='store_true', help='solo clientes + hijos (F1+F2)')
    ap.add_argument('--only-rfcs', action='store_true',
                    help='solo RFCs (F3) — requiere clientes STRESS-* ya sembrados')
    ap.add_argument('--no-bypass', action='store_true',
                    help='omite el historial resuelto (Applied/Rejected/Failed); '
                         'esos RFC se reparten como Submitted en su lugar')
    ap.add_argument('--verify', action='store_true')
    ap.add_argument('--wipe', action='store_true')
    ap.add_argument('--dry-run', action='store_true', help='plan, cero escrituras')
    ap.add_argument('--dry-writes', action='store_true',
                    help='lecturas reales, escrituras simuladas (valida deltas sin tocar DEV)')
    # volumenes
    ap.add_argument('--clients', type=int, default=1500)
    ap.add_argument('--rfcs', type=int, default=600)
    # red
    ap.add_argument('--chunk', type=int, default=100)
    ap.add_argument('--workers', type=int, default=6)
    return ap.parse_args()


def main():
    a = build_args()
    if a.dry_writes:
        NOWRITE['on'] = True
        print('== DRY-WRITES: lecturas reales, escrituras SIMULADAS ==')

    if a.dry_run:
        print('\n== DRY-RUN: plan, cero escrituras ==')
        buckets, resumen = plan_rfcs(a.rfcs)
        print(f'  clientes objetivo: {a.clients} (93% Activo / 5% Inactivo / 2% PendingApproval)')
        print('  RFC reparto: ' + ' · '.join(f'{k}={v}' for k, v in resumen.items()))
        mostrar_reintentos()
        return

    if a.wipe:
        wipe(a)
        mostrar_reintentos()
        return
    if a.verify:
        verify(a)
        return

    corrio = False
    if a.all or a.only_clients:
        clientes = seed_clients(a)
        seed_children(a, clientes)
        corrio = True
    if a.all or a.only_rfcs:
        seed_rfcs(a)
        corrio = True

    if not corrio:
        print('\nNada que hacer: pasa --all, --only-clients, --only-rfcs, --verify o --wipe.')
    else:
        print('\n== RESUMEN FINAL ==')
        verify(a)
    mostrar_reintentos()
    if NOWRITE['on']:
        print(f'\n  (dry-writes) escrituras simuladas: {NOWRITE["n"]}')


if __name__ == '__main__':
    main()
