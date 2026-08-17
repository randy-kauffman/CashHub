#!/usr/bin/env python3
"""Backfill de los espejos server-side de CH-App-Recounting v2 — idempotente.

Estampa en los registros EXISTENTES lo que los plugins estamparan de aca en
adelante. Sin esto los espejos solo existen para lo que nazca despues del
registro, y las consultas delegables de la v2 mienten por omision sobre todo el
historico (que es exactamente el bug que los espejos vienen a matar).

  E-1  sanic_chtbldepositsrecounting.sanic_recountingsessionmirror
       <- BagID.RecountingSession
  E-2  sanic_chtblchecks.sanic_bag  <- RelatedReCounterDepositID.BagID
       (el espejo B6 del tracker: la columna y el plugin YA EXISTEN — esta fase
        esta aca para poder verificarlo/completarlo en una sola corrida; el
        backfill original vive en scripts/backfill-tracker.py y solo estampaba
        cuando el espejo estaba VACIO, no cuando difiere)
  E-3  sanic_chtbldeclaredchecks.sanic_declaredbagmirror
       <- DeclaredDepositID.DeclaredBag
  E-4  sanic_chtblxmlreadingdetail.sanic_recountingsessiondetailmirror
       + .sanic_xmlreadingactiva
       <- XMLReading.RecountingSessionDetailId / .IsActive

Solo PATCHea lo que DIFIERE del valor esperado: correrlo dos veces no genera
auditoria basura ni escrituras al pedo. Va por $batch (un changeset POR REQUEST,
mismo patron que backfill-clientbranch-mirror.py) porque a escala de miles de
filas un PATCH unitario es media hora de red.

Los cuatro espejos son PURA Web API: NO necesitan el assembly desplegado. Con la
columna creada + este backfill las consultas ya delegan sobre lo que existe hoy;
lo que queda pendiente hasta el deploy del plugin es SOLO el mantenimiento en
vivo (filas nuevas y re-enlaces).

Al final corre ademas el GC de solicitudes legadas "Primera tula en planilla"
(00 §2.2 punto 7). Ese paso es DRY-RUN POR DEFECTO: lista y clasifica las
pendientes peligrosas y solo las cierra con --gc-apply.

Uso:
    python3 scripts/backfill-recounting-mirrors.py --dry-run        # solo el diff
    python3 scripts/backfill-recounting-mirrors.py                  # estampar todo
    python3 scripts/backfill-recounting-mirrors.py --only e1,e4     # una parte
    python3 scripts/backfill-recounting-mirrors.py --only-gc        # solo listar el GC
    python3 scripts/backfill-recounting-mirrors.py --only-gc --gc-apply   # y cerrarlas
Requiere pp_secrets.env en la raiz.
"""
import argparse
import json
import os
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed

PAGE_SIZE = 5000

# ---------- auth (mismo patron que los demas backfills del repo) ----------
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


def req(method, path, body=None, prefer=None, retry=4):
    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + path, method=method,
                                   data=json.dumps(body).encode() if body is not None else None)
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
            last = f'{method} {path[:140]}: {e.code} {e.read().decode()[:400]}'
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                continue
            if e.code in (429, 503) and attempt < retry - 1:
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            raise RuntimeError(last) from e
        except urllib.error.URLError as e:
            last = f'{method} {path[:140]}: {e}'
            if attempt < retry - 1:
                time.sleep(3)
                continue
            raise RuntimeError(last) from e
    raise RuntimeError(last or 'sin reintentos')


def get_all(path):
    rows, url = [], path
    while url:
        d = req('GET', url, prefer=f'odata.maxpagesize={PAGE_SIZE}')
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


# ---------- $batch: un changeset POR REQUEST (cada PATCH falla solo) ----------
def batch_exec(requests_, retry=3):
    if not requests_:
        return []
    bb = 'batch_' + uuid.uuid4().hex
    parts = []
    for i, (method, path, body) in enumerate(requests_, 1):
        cs = 'cs_' + uuid.uuid4().hex
        payload = json.dumps(body, ensure_ascii=False) if body is not None else ''
        parts.append(
            f'--{bb}\r\nContent-Type: multipart/mixed; boundary={cs}\r\n\r\n'
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
    payload = ''.join(parts) + f'--{bb}--\r\n'

    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + '/$batch', method='POST', data=payload.encode('utf-8'))
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', f'multipart/mixed; boundary={bb}')
        r.add_header('Accept', 'application/json')
        try:
            with urllib.request.urlopen(r, timeout=600) as resp:
                return _parse_batch(resp.read().decode('utf-8'), len(requests_))
        except urllib.error.HTTPError as e:
            last = f'$batch: {e.code} {e.read().decode()[:400]}'
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                continue
            if e.code in (429, 503) and attempt < retry - 1:
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            break
        except urllib.error.URLError as e:
            last = f'$batch: {e}'
            if attempt < retry - 1:
                time.sleep(3)
                continue
            break
    return [(0, last)] * len(requests_)


def _parse_batch(text, expected):
    """[(status, error|None)] en ORDEN. Mismo parser que backfill-clientbranch-mirror.py."""
    out, cur, in_headers, body_lines = [], None, False, []
    for raw in text.split('\n'):
        line = raw.rstrip('\r')
        if line.startswith('--') and ('batch' in line or 'cs_' in line
                                      or 'changesetresponse' in line.lower()):
            if cur is not None:
                cur['body'] = '\n'.join(body_lines).strip()
                out.append(cur)
                cur = None
            body_lines, in_headers = [], False
            continue
        if line.startswith('HTTP/1.1 '):
            if cur is not None:
                cur['body'] = '\n'.join(body_lines).strip()
                out.append(cur)
            try:
                code = int(line.split()[1])
            except (IndexError, ValueError):
                code = 0
            cur = {'status': code}
            body_lines, in_headers = [], True
            continue
        if cur is None:
            continue
        if in_headers:
            if line.strip() == '':
                in_headers = False
            continue
        body_lines.append(line)
    if cur is not None:
        cur['body'] = '\n'.join(body_lines).strip()
        out.append(cur)

    res = []
    for p in out:
        st, err = p['status'], None
        if st == 0 or st >= 400:
            b = p.get('body') or ''
            try:
                err = json.loads(b)['error']['message'][:300]
            except Exception:                                          # noqa: BLE001
                err = (b[:300] or f'HTTP {st}')
        res.append((st, err))
    if len(res) != expected:
        return [(0, f'respuesta $batch ilegible ({len(res)} partes de {expected})')] * expected
    return res


def run_batches(requests_, label, chunk=100, workers=6):
    chunks = [requests_[i:i + chunk] for i in range(0, len(requests_), chunk)]
    results = [None] * len(chunks)
    t0 = time.perf_counter()
    done = 0
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(batch_exec, c): i for i, c in enumerate(chunks)}
        for fut in as_completed(futs):
            res = fut.result()
            results[futs[fut]] = res
            done += len(res)
            el = time.perf_counter() - t0
            print(f'    {label}: {done}/{len(requests_)} ({done / el if el else 0:.0f} op/s)',
                  flush=True)
    flat = [r for c in results for r in c]
    dt = time.perf_counter() - t0
    errs = [e for (_, e) in flat if e]
    print(f'  {label}: {len(flat) - len(errs)} ok · {len(errs)} errores · {dt:.1f}s '
          f'({len(flat) / dt if dt else 0:.0f} op/s)')
    for e in errs[:5]:
        print(f'      !! {e}', file=sys.stderr)
    return flat, dt


_sets = {}


def entity_set(logical):
    """EntitySetName real desde metadata: no se adivina el plural."""
    if logical not in _sets:
        _sets[logical] = req(
            'GET', f"/EntityDefinitions(LogicalName='{logical}')?$select=EntitySetName"
        )['EntitySetName']
    return _sets[logical]


def norm(guid):
    """GUID comparable: Dataverse devuelve _value en minusculas, pero no siempre."""
    return guid.lower() if isinstance(guid, str) and guid else None


def bind(nav, entityset, value):
    """Par (clave, valor) de un PATCH de lookup. None = desvincular."""
    return (f'{nav}@odata.bind', f'/{entityset}({value})' if value else None)


# =====================================================================
# Fases. Cada una devuelve (patches, resumen) SIN escribir nada:
# el que escribe es main(), y solo si no es --dry-run.
# =====================================================================

def phase_e1():
    """E-1: deposito de recuento -> sesion de la tula."""
    deposits_set = entity_set('sanic_chtbldepositsrecounting')
    sessions_set = entity_set('sanic_chtblrecountingsession')

    # $expand de UN nivel: la sesion viaja con la tula del deposito en la MISMA
    # query. Un Retrieve de tula por deposito seria N+1 sobre el historico.
    rows = get_all(
        f'/{deposits_set}?$select=sanic_chtbldepositsrecountingid,'
        '_sanic_recountingsessionmirror_value'
        '&$expand=sanic_BagID($select=_sanic_recountingsession_value)')

    patches, ya_ok, sin_tula = [], 0, 0
    for row in rows:
        bag = row.get('sanic_BagID') or {}
        if not row.get('sanic_BagID'):
            sin_tula += 1
        want = norm(bag.get('_sanic_recountingsession_value'))
        have = norm(row.get('_sanic_recountingsessionmirror_value'))
        if have == want:
            ya_ok += 1
            continue
        k, v = bind('sanic_RecountingSessionMirror', sessions_set, want)
        patches.append(('PATCH',
                        f"/{deposits_set}({row['sanic_chtbldepositsrecountingid']})", {k: v}))

    return patches, (f'  depositos leidos: {len(rows)} · ya al dia: {ya_ok} · '
                     f'a estampar: {len(patches)} · sin tula: {sin_tula}')


def phase_e2():
    """E-2: cheque fisico -> tula de su deposito de recuento (espejo B6, ya existente)."""
    checks_set = entity_set('sanic_chtblchecks')
    bags_set = entity_set('sanic_chtblbags')

    rows = get_all(
        f'/{checks_set}?$select=sanic_chtblchecksid,_sanic_bag_value'
        '&$expand=sanic_RelatedReCounterDepositID($select=_sanic_bagid_value)')

    patches, ya_ok, sin_deposito = [], 0, 0
    for row in rows:
        deposit = row.get('sanic_RelatedReCounterDepositID') or {}
        if not row.get('sanic_RelatedReCounterDepositID'):
            sin_deposito += 1
        want = norm(deposit.get('_sanic_bagid_value'))
        have = norm(row.get('_sanic_bag_value'))
        if have == want:
            ya_ok += 1
            continue
        k, v = bind('sanic_Bag', bags_set, want)
        patches.append(('PATCH', f"/{checks_set}({row['sanic_chtblchecksid']})", {k: v}))

    return patches, (f'  cheques leidos: {len(rows)} · ya al dia: {ya_ok} · '
                     f'a estampar: {len(patches)} · sin deposito: {sin_deposito}')


def phase_e3():
    """E-3: cheque declarado -> bolsa declarada de su deposito declarado."""
    checks_set = entity_set('sanic_chtbldeclaredchecks')
    dbags_set = entity_set('sanic_chtbldeclaredbags')

    rows = get_all(
        f'/{checks_set}?$select=sanic_chtbldeclaredchecksid,_sanic_declaredbagmirror_value'
        '&$expand=sanic_DeclaredDepositID($select=_sanic_declaredbag_value)')

    patches, ya_ok, sin_deposito = [], 0, 0
    for row in rows:
        deposit = row.get('sanic_DeclaredDepositID') or {}
        if not row.get('sanic_DeclaredDepositID'):
            sin_deposito += 1
        want = norm(deposit.get('_sanic_declaredbag_value'))
        have = norm(row.get('_sanic_declaredbagmirror_value'))
        if have == want:
            ya_ok += 1
            continue
        k, v = bind('sanic_DeclaredBagMirror', dbags_set, want)
        patches.append(('PATCH',
                        f"/{checks_set}({row['sanic_chtbldeclaredchecksid']})", {k: v}))

    return patches, (f'  cheques declarados leidos: {len(rows)} · ya al dia: {ya_ok} · '
                     f'a estampar: {len(patches)} · sin deposito declarado: {sin_deposito}')


def phase_e4():
    """E-4: detalle de lectura -> sesion-detalle + vigencia de su lectura (DOS columnas)."""
    details_set = entity_set('sanic_chtblxmlreadingdetail')
    sdetails_set = entity_set('sanic_chtblrecountingsessiondetail')

    rows = get_all(
        f'/{details_set}?$select=sanic_chtblxmlreadingdetailid,'
        '_sanic_recountingsessiondetailmirror_value,sanic_xmlreadingactiva'
        '&$expand=sanic_XMLReading($select=_sanic_recountingsessiondetailid_value,sanic_isactive)')

    patches, ya_ok, sin_lectura = [], 0, 0
    for row in rows:
        reading = row.get('sanic_XMLReading') or {}
        if not row.get('sanic_XMLReading'):
            sin_lectura += 1
        want_detail = norm(reading.get('_sanic_recountingsessiondetailid_value'))
        want_active = reading.get('sanic_isactive')
        have_detail = norm(row.get('_sanic_recountingsessiondetailmirror_value'))
        have_active = row.get('sanic_xmlreadingactiva')

        if have_detail == want_detail and have_active == want_active:
            ya_ok += 1
            continue

        # Las DOS juntas SIEMPRE: son un par y escribir una sola dejaria al detalle
        # con mitad de la verdad de su lectura (mismo criterio que el plugin).
        k, v = bind('sanic_RecountingSessionDetailMirror', sdetails_set, want_detail)
        patches.append(('PATCH',
                        f"/{details_set}({row['sanic_chtblxmlreadingdetailid']})",
                        {k: v, 'sanic_xmlreadingactiva': want_active}))

    return patches, (f'  detalles leidos: {len(rows)} · ya al dia: {ya_ok} · '
                     f'a estampar: {len(patches)} · sin lectura: {sin_lectura}')


PHASES = [
    ('e1', 'E-1: depositos de recuento -> sesion de la tula', phase_e1),
    ('e2', 'E-2: cheques -> tula del deposito de recuento (B6, ya existente)', phase_e2),
    ('e3', 'E-3: cheques declarados -> bolsa declarada', phase_e3),
    ('e4', 'E-4: detalles de lectura -> sesion-detalle + vigencia', phase_e4),
]


# =====================================================================
# GC de solicitudes legadas "Primera tula en planilla"
# (00 §2.2 punto 7 — NO es un plugin: es una limpieza de convivencia)
#
# Con L-6 decidido, la v2 no crea NI ESPERA ese tipo de solicitud. Pero una
# solicitud v1 todavia PENDIENTE sigue siendo peligrosa: al aprobarla, el canvas
# de CH-App-Supervisor re-patchea la bolsa a 'Asignada a cajero' con la SESION y
# la DECLARACION viejas que viajan en su JSON — sobre una tula que la v2 quiza ya
# reconto. Es dinero volviendo a la mesa equivocada.
#
# Este paso es DRY-RUN POR DEFECTO: lista y clasifica, no escribe. Cerrar una
# solicitud es rechazarla (ApprovalStatus = Rechazado + StatusReason), nunca
# borrarla: la solicitud es evidencia de que alguien pidio algo.
# =====================================================================

REQUEST_TYPE_LEGACY = 'Primera tula en planilla'
APPROVAL_RECHAZADO = 159460001

# sanic_bagstatus de CH-Tbl-Bags. Una tula en estos dos estados todavia no entro
# al ciclo de recuento: aprobar la solicitud legada seria inocuo.
BAG_RECIBIDA_EN_ESCLUSA = 159460000
BAG_ALMACENADA_EN_BOVEDA = 159460001

GC_STATUS_REASON = (
    'Cerrada por convivencia v1/v2: la tula ya avanzo en el ciclo de recuento y aprobar '
    'esta solicitud la devolveria a la mesa con la sesion y la declaracion viejas.')


def _json_value(blob, key):
    """Lector posicional del JSON plano de RequestResources (mismo criterio que FlatJson)."""
    if not blob:
        return None
    try:
        return (json.loads(blob) or {}).get(key)
    except (ValueError, TypeError):
        # Los productores v1 concatenaban comillas a mano: si no parsea, se busca
        # el par "clave":"valor" con una expresion, que es lo que hace el guard.
        import re
        m = re.search(r'"%s"\s*:\s*"([^"]*)"' % re.escape(key), blob)
        return m.group(1) if m else None


def _fetch_bags(bag_ids):
    """Estado y sesion de un conjunto de tulas, en tandas de 20 (filtro OData acotado)."""
    bags_set = entity_set('sanic_chtblbags')
    out = {}
    ids = [b for b in bag_ids if b]
    for i in range(0, len(ids), 20):
        chunk = ids[i:i + 20]
        flt = ' or '.join(f'sanic_chtblbagsid eq {b}' for b in chunk)
        rows = get_all(
            f'/{bags_set}?$select=sanic_chtblbagsid,sanic_bagid,sanic_bagstatus,'
            f'_sanic_recountingsession_value&$filter={urllib.parse.quote(flt)}')
        for row in rows:
            out[norm(row['sanic_chtblbagsid'])] = row
    return out


def gc_legacy_approvals(apply_changes):
    """Lista (y opcionalmente cierra) las 'Primera tula en planilla' pendientes peligrosas."""
    requests_set = entity_set('sanic_chtblapprovalrequest')

    flt = (f"sanic_requesttype eq '{REQUEST_TYPE_LEGACY}' and sanic_approvalstatus eq null")
    rows = get_all(
        f'/{requests_set}?$select=sanic_chtblapprovalrequestid,sanic_name,sanic_bagnumber,'
        f'sanic_requestresources,createdon&$filter={urllib.parse.quote(flt)}'
        '&$orderby=createdon%20asc')

    print(f'  solicitudes "{REQUEST_TYPE_LEGACY}" PENDIENTES: {len(rows)}')
    if not rows:
        print('  nada que limpiar.')
        return 0

    bags = _fetch_bags({norm(_json_value(r.get('sanic_requestresources'), 'BagGUID'))
                        for r in rows})

    peligrosas, inocuas, sin_tula = [], 0, 0
    for row in rows:
        blob = row.get('sanic_requestresources')
        bag_id = norm(_json_value(blob, 'BagGUID'))
        req_session = norm(_json_value(blob, 'RecountingSessionGUID'))
        bag = bags.get(bag_id) if bag_id else None

        if bag is None:
            # Sin tula resoluble no se puede afirmar que sea peligrosa: se reporta
            # aparte y NO se cierra (cerrar a ciegas es el error del otro lado).
            sin_tula += 1
            continue

        estado = bag.get('sanic_bagstatus')
        bag_session = norm(bag.get('_sanic_recountingsession_value'))

        avanzada = estado not in (BAG_RECIBIDA_EN_ESCLUSA, BAG_ALMACENADA_EN_BOVEDA)
        otra_sesion = bag_session is not None and bag_session != req_session

        if avanzada or otra_sesion:
            peligrosas.append((row, bag, estado, bag_session, req_session))
        else:
            inocuas += 1

    print(f'  PELIGROSAS (cerrar): {len(peligrosas)} · inocuas (dejar): {inocuas} · '
          f'sin tula resoluble (revisar a mano): {sin_tula}')

    for row, bag, estado, bag_session, req_session in peligrosas[:25]:
        print(f'    - {row.get("sanic_name") or row["sanic_chtblapprovalrequestid"]} · '
              f'tula {bag.get("sanic_bagid") or row.get("sanic_bagnumber")} · '
              f'BagStatus={estado} · sesion-tula={bag_session} · sesion-solicitud={req_session} · '
              f'creada {(row.get("createdon") or "")[:10]}')
    if len(peligrosas) > 25:
        print(f'    … y {len(peligrosas) - 25} mas')

    if not peligrosas:
        return 0

    if not apply_changes:
        print('  (dry-run) no se cerro ninguna. Para cerrarlas: --gc-apply')
        return 0

    patches = [('PATCH', f'/{requests_set}({row["sanic_chtblapprovalrequestid"]})',
                {'sanic_approvalstatus': APPROVAL_RECHAZADO,
                 'sanic_statusreason': GC_STATUS_REASON})
               for row, _, _, _, _ in peligrosas]

    print('  🧨 GuardReceptionPlugin impone que quien resuelve pertenezca al grupo AAD '
          'aprobador (Security.ApproverGroup.Supervisor) y NO sea el creador. Si el SP de '
          'este script no esta en ese grupo, los PATCH van a rebotar con el mensaje del '
          'guard: sumalo al grupo o corre este cierre con una identidad que si lo este.')

    res, _ = run_batches(patches, 'cerrando solicitudes legadas')
    return sum(1 for (_, e) in res if e)


def main():
    ap = argparse.ArgumentParser(
        description='Backfill de los espejos server-side de CH-App-Recounting v2')
    ap.add_argument('--dry-run', action='store_true', help='solo reporta el diff')
    ap.add_argument('--only', default='', help='fases a correr, coma-separadas (e1,e2,e3,e4)')
    ap.add_argument('--chunk', type=int, default=100, help='PATCHes por $batch')
    ap.add_argument('--workers', type=int, default=6, help='$batch en paralelo')
    ap.add_argument('--gc-apply', action='store_true',
                    help='cierra (Rechazado) las "Primera tula en planilla" pendientes '
                         'peligrosas. SIN esta bandera el GC solo LISTA (00 §2.2 punto 7)')
    ap.add_argument('--only-gc', action='store_true',
                    help='corre SOLO el GC de solicitudes legadas, sin tocar los espejos')
    a = ap.parse_args()

    wanted = [p.strip().lower() for p in a.only.split(',') if p.strip()]
    unknown = [w for w in wanted if w not in {k for k, _, _ in PHASES}]
    if unknown:
        raise SystemExit(f"Fase desconocida: {', '.join(unknown)} (validas: e1, e2, e3, e4)")

    print('=' * 78)
    print(f'BACKFILL espejos CH-App-Recounting v2 — {URL}')
    if a.dry_run:
        print('MODO DRY-RUN: no se escribe nada')
    print('=' * 78)

    total_err = 0
    for key, title, fn in PHASES:
        if a.only_gc or (wanted and key not in wanted):
            continue
        print(f'== {title}')
        t0 = time.perf_counter()
        patches, resumen = fn()
        print(f'{resumen} · lectura {time.perf_counter() - t0:.1f}s')

        if not patches:
            print('  nada que estampar: el espejo ya esta al dia.')
            continue
        if a.dry_run:
            print(f'  (dry-run) {len(patches)} PATCHes no ejecutados')
            continue

        res, _ = run_batches(patches, f'estampando {key}', a.chunk, a.workers)
        total_err += sum(1 for (_, e) in res if e)

    # GC de solicitudes legadas: SIEMPRE se reporta al final, NUNCA escribe sin
    # --gc-apply. Es el punto 7 de 00 §2.2 y no es un plugin a proposito — es una
    # limpieza de convivencia v1/v2, de una sola vez, con revision humana.
    print('== GC: solicitudes legadas "Primera tula en planilla" pendientes (00 §2.2 punto 7)')
    total_err += gc_legacy_approvals(apply_changes=a.gc_apply and not a.dry_run)

    print('Backfill de los espejos de recounting completo.'
          + (f' {total_err} errores.' if total_err else ''))
    if total_err:
        sys.exit(1)


if __name__ == '__main__':
    main()
