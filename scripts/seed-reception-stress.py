#!/usr/bin/env python3
"""Seeder de ESTRES del dominio de RECEPCION para CH-App-Reception v2.

Siembra el universo que la app v2 necesita para probarse DE VERDAD contra los
topes de delegacion de Power Fx (500 / 2000 filas) y contra el volumen de lotes:

    ~2.400 lotes    (250 En progreso · 900 Recibido · 1.250 En boveda)
    ~6.000 planillas (1-4 por lote, montos NIO/USD realistas)
    ~13.000 tulas    (1-4 por planilla)

MARCADORES (todo lo sembrado es rastreable y borrable por prefijo):
    lote     sanic_transportadora = 'STRESS-RCP <TransportadoraFicticia>'
    planilla sanic_receiptid      = 'SR' + correlativo   (y sanic_name igual)
    tula     sanic_bagid          = 'ST' + correlativo   (y sanic_name igual)
    cliente  sanic_name           = 'STRESS-RCP ...'     (solo si hay que crearlos)

-----------------------------------------------------------------------------
EL GUARD Y COMO SE LO RESPETA
-----------------------------------------------------------------------------
GuardReceptionPlugin (Pre-validation, sincrono) impone ReceptionGuard:

  lote      Create  : solo nace 'En progreso' (o sin estado).
            Update  : En progreso <-> Recibido, Recibido -> En boveda.
                      En boveda = CONGELADO (nada, ni siquiera reabrir).
            Delete  : solo 'En progreso'.
  planilla  Create  : el lote destino NO puede estar en boveda/anulado.
            Delete  : idem.
  tula      Create  : nace en esclusa, en boveda o pendiente-supervisor-esclusa.
            Update  : matriz de transiciones; la identidad (bagid/name/planilla)
                      solo se edita con el lote abierto.
            Delete  : lote abierto, o tula pendiente-supervisor-esclusa.

=> EL SEED CORRE CON EL GUARD 100% ACTIVO. El orden lo hace legal:
   1. lote nace En progreso (se manda 159460000 explicito),
   2. planillas y tulas se crean con el lote todavia abierto,
   3. recien despues se transiciona el lote En progreso -> Recibido -> En boveda.
   Las tulas de un lote destinado a boveda NACEN 'Almacenada en boveda'
   (EvaluateBagCreate lo permite explicitamente: es como el supervisor
   materializa 'Agregar tula'), que deja el MISMO estado final que patchearlas
   una por una y ahorra ~7.000 escrituras con 3 plugins sincronos cada una.
   Para no perder la prueba de la transicion real, --transition-sample N
   crea N tulas en esclusa y las patchea a boveda DESPUES del cierre del lote
   (legal: EvaluateBagStatusTransition no mira el lote si no se toca identidad).

=> EL --clean SI NECESITA APAGAR EL GUARD: los lotes cerrados y en boveda no
   admiten borrado de nada (ni tulas, ni planillas, ni el lote), y desde boveda
   no hay marcha atras. Se desactivan SOLO los 3 pasos Delete del guard
   (lote / planilla / tula) y se restauran SIEMPRE (try/finally + verificacion
   + --restore-steps de rescate). Los ESPEJOS nunca se tocan.

-----------------------------------------------------------------------------
Uso:
    python3 scripts/seed-reception-stress.py                 # sembrar todo
    python3 scripts/seed-reception-stress.py --dry-run       # solo el plan
    python3 scripts/seed-reception-stress.py --verify        # solo verificar
    python3 scripts/seed-reception-stress.py --clean         # borrar lo marcado
    python3 scripts/seed-reception-stress.py --restore-steps # rescate de steps
Requiere pp_secrets.env en la raiz. NO correr en produccion.
"""
import argparse
import json
import os
import random
import statistics
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed

random.seed(20260816)

# ---------------------------------------------------------------- marcadores
TRANSPORTADORA_PREFIX = 'STRESS-RCP '
RECEIPT_PREFIX = 'SR'
BAG_PREFIX = 'ST'
CLIENT_PREFIX = 'STRESS-RCP '

TRANSPORTADORAS = [
    'Valores Sinteticos', 'Blindados Fantasma', 'Custodia Espectro',
    'Transvalor Simulado', 'Caudales Prueba', 'Escolta Ficticia',
]

# --------------------------------------------------------------- option sets
BATCH_EN_PROGRESO = 159460000
BATCH_RECIBIDO = 159460001
BATCH_ANULADO = 159460002
BATCH_EN_BOVEDA = 159460003
BATCH_LABEL = {BATCH_EN_PROGRESO: 'En progreso', BATCH_RECIBIDO: 'Recibido',
               BATCH_ANULADO: 'Anulado', BATCH_EN_BOVEDA: 'En boveda'}

BAG_ESCLUSA = 159460000
BAG_BOVEDA = 159460001

# ------------------------------------------------------------ steps del guard
STEP_DEL_BATCH = 'CashHub.GuardReception: Delete de sanic_chtblreceptionbatch'
STEP_DEL_RECEPTION = 'CashHub.GuardReception: Delete de sanic_chtblreceptions'
STEP_DEL_BAG = 'CashHub.GuardReception: Delete de sanic_chtblbags'
STEPS_CLEAN = (STEP_DEL_BATCH, STEP_DEL_RECEPTION, STEP_DEL_BAG)
# Espejos: NUNCA se desactivan (ni en seed ni en clean). Se listan para auditar.
STEPS_MIRROR = (
    'CashHub.ReceptionMirror: Create de sanic_chtblreceptions',
    'CashHub.ReceptionMirror: Update de sanic_chtblreceptionbatch',
    'CashHub.BagReceptionMirror: Create de sanic_chtblbags',
)

# ------------------------------------------------------------------- entities
ES_BATCH = 'sanic_chtblreceptionbatchs'
ES_RECEPTION = 'sanic_chtblreceptionses'
ES_BAG = 'sanic_chtblbagses'
ES_CLIENT = 'sanic_chtblclients'

# ===========================================================================
# auth + http (mismo patron que seed-supervisor-aprobaciones.py)
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


def req(method, path, body=None, retry=4, prefer=None, timed=False):
    """Request unitario. Devuelve dict (o (dict, segundos) si timed)."""
    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + path.replace(' ', '%20'), method=method,
                                   data=json.dumps(body).encode() if body is not None else None)
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', 'application/json')
        r.add_header('Accept', 'application/json')
        if prefer:
            r.add_header('Prefer', prefer)
        t0 = time.perf_counter()
        try:
            with urllib.request.urlopen(r, timeout=300) as resp:
                raw = resp.read().decode()
                d = json.loads(raw) if raw.strip() else {}
                return (d, time.perf_counter() - t0) if timed else d
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:400]
            last = f'{method} {path[:140]}: {e.code} {msg}'
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


def get_all(path, page_cb=None):
    rows, url = [], path
    while url:
        d = req('GET', url)
        rows.extend(d.get('value', []))
        if page_cb:
            page_cb(len(rows))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def count_of(entity_set, filtro):
    """Conteo REAL (no el @odata.count, que Dataverse tope a 5000): pagina el id."""
    n = 0
    url = f'/{entity_set}?$select={PK[entity_set]}&$filter={urllib.parse.quote(filtro)}'
    while url:
        d = req('GET', url, prefer='odata.maxpagesize=5000')
        n += len(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return n


PK = {ES_BATCH: 'sanic_chtblreceptionbatchid', ES_RECEPTION: 'sanic_chtblreceptionsid',
      ES_BAG: 'sanic_chtblbagsid', ES_CLIENT: 'sanic_chtblclientid'}


# ===========================================================================
# $batch — un changeset POR REQUEST: una sola ida y vuelta HTTP, pero cada
# operacion falla sola. Un changeset gigante seria atomico y un solo rebote del
# guard tiraria abajo las otras 99 escrituras (y encima sin decir cual fue).
# ===========================================================================
def batch_exec(requests_, retry=3):
    """requests_ = [(method, path_relativo, body|None)].
    Devuelve [(status:int, entity_id:str|None, error:str|None)] en ORDEN."""
    if not requests_:
        return []
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
        try:
            with urllib.request.urlopen(r, timeout=600) as resp:
                return _parse_batch(resp.read().decode('utf-8'), len(requests_))
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:400]
            last = f'$batch: {e.code} {msg}'
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
    return [(0, None, last)] * len(requests_)


def _parse_batch(text, expected):
    """Parser del multipart de respuesta: status + OData-EntityId + error por parte."""
    out = []
    cur = None
    in_headers = False
    body_lines = []
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
        # Defensivo: si el parser se desalinea, se marca todo el bloque.
        return [(0, None, f'respuesta $batch ilegible ({len(res)} partes de {expected})')] * expected
    return res


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
    """Ejecuta requests_ en $batch paralelos. Devuelve (resultados_en_orden, segundos)."""
    chunks = [requests_[i:i + chunk] for i in range(0, len(requests_), chunk)]
    results = [None] * len(chunks)
    pr = Progress(len(requests_), label)
    t0 = time.perf_counter()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(batch_exec, c): i for i, c in enumerate(chunks)}
        for fut in as_completed(futs):
            i = futs[fut]
            res = fut.result()
            results[i] = res
            errs = [r[2] for r in res if r[2]]
            pr.add(len(res) - len(errs), len(errs), errs)
    flat = [r for c in results for r in c]
    dt = time.perf_counter() - t0
    n_err = sum(1 for r in flat if r[2])
    print(f'  {label}: {len(flat) - n_err} ok · {n_err} errores · {dt:.1f}s '
          f'({len(flat) / dt if dt else 0:.1f} op/s)')
    for s in pr.samples:
        print(f'      !! {s}', file=sys.stderr)
    return flat, dt


# ===========================================================================
# steps
# ===========================================================================
def leer_steps():
    flt = urllib.parse.quote(
        "contains(name,'sanic_chtblreceptionbatch') or contains(name,'sanic_chtblreceptions') "
        "or contains(name,'sanic_chtblbags')")
    rows = get_all('/sdkmessageprocessingsteps?$select=name,statecode,statuscode,'
                   'sdkmessageprocessingstepid&$filter=' + flt)
    return {r['name']: r for r in rows}


def set_step(step_id, activo):
    body = {'statecode': 0, 'statuscode': 1} if activo else {'statecode': 1, 'statuscode': 2}
    req('PATCH', f'/sdkmessageprocessingsteps({step_id})', body)


def mostrar_steps(titulo, nombres=None):
    todos = leer_steps()
    print(f'  {titulo}')
    for n in (nombres or (STEPS_CLEAN + STEPS_MIRROR)):
        s = todos.get(n)
        if not s:
            print(f'    ?? {n}: NO EXISTE')
            continue
        estado = 'ACTIVO' if s['statecode'] == 0 and s['statuscode'] == 1 else 'INACTIVO'
        print(f'    [{estado:8}] {n}')
    return todos


def reactivar(pasos):
    ok = True
    for s in pasos:
        for intento in range(4):
            try:
                set_step(s['sdkmessageprocessingstepid'], True)
                break
            except Exception as e:                                     # noqa: BLE001
                print(f'  !! fallo reactivando {s["name"]} (intento {intento + 1}): {e}',
                      file=sys.stderr)
        else:
            ok = False
    todos = leer_steps()
    for s in pasos:
        c = todos.get(s['name'], {})
        if not (c.get('statecode') == 0 and c.get('statuscode') == 1):
            ok = False
            print(f'  !!!! EL STEP {s["name"]} QUEDO INACTIVO — REACTIVALO A MANO !!!!',
                  file=sys.stderr)
    return ok


# ===========================================================================
# inventario de lo sembrado
# ===========================================================================
def batches_stress():
    flt = urllib.parse.quote(f"startswith(sanic_transportadora,'{TRANSPORTADORA_PREFIX}')")
    return get_all(f'/{ES_BATCH}?$select=sanic_name,sanic_receptionstatus,sanic_transportadora'
                   f'&$filter={flt}')


def receptions_stress(select='sanic_receiptid,sanic_batchstatus,_sanic_receptionbatch_value'):
    flt = urllib.parse.quote(f"startswith(sanic_receiptid,'{RECEIPT_PREFIX}')")
    return get_all(f'/{ES_RECEPTION}?$select={select}&$filter={flt}')


def bags_stress(select='sanic_bagid,sanic_bagstatus,_sanic_receptionid_value,'
                       '_sanic_receptionbatchid_value,sanic_receiptidmirror'):
    flt = urllib.parse.quote(f"startswith(sanic_bagid,'{BAG_PREFIX}')")
    return get_all(f'/{ES_BAG}?$select={select}&$filter={flt}')


def next_correlativo(rows, key, prefix):
    top = 0
    for r in rows:
        v = (r.get(key) or '')[len(prefix):]
        if v.isdigit():
            top = max(top, int(v))
    return top + 1


# ===========================================================================
# clientes
# ===========================================================================
def clientes(crear_si_falta=True):
    rows = get_all(f'/{ES_CLIENT}?$select=sanic_name&$top=50')
    if rows:
        print(f'  clientes reutilizados del entorno: {len(rows)}')
        return rows
    if not crear_si_falta:
        return []
    print('  no hay clientes: se siembran 3 STRESS')
    nuevos = [{'sanic_name': f'{CLIENT_PREFIX}Cliente {i}'} for i in (1, 2, 3)]
    res, _ = run_batches([('POST', f'/{ES_CLIENT}', c) for c in nuevos],
                         'creando clientes', chunk=10, workers=1)
    out = []
    for c, (st, cid, err) in zip(nuevos, res):
        if err:
            print(f'    !! {err}', file=sys.stderr)
        else:
            out.append({'sanic_chtblclientid': cid, 'sanic_name': c['sanic_name']})
    return out


# ===========================================================================
# SEED
# ===========================================================================
def plan_volumes(a):
    """Reparte los lotes por estado destino y decide planillas/tulas por lote."""
    total = a.batches
    n_prog = round(total * a.mix_progreso / 100.0)
    n_boveda = round(total * a.mix_boveda / 100.0)
    n_recib = total - n_prog - n_boveda
    destinos = ([BATCH_EN_PROGRESO] * n_prog + [BATCH_RECIBIDO] * n_recib
                + [BATCH_EN_BOVEDA] * n_boveda)
    random.shuffle(destinos)
    # media 2.5 planillas/lote -> ~6.000 ; media ~2.17 tulas/planilla -> ~13.000
    plan = []
    for d in destinos:
        n_pl = random.choice((1, 2, 3, 4))
        pls = [random.choices((1, 2, 3, 4), weights=(35, 25, 28, 12))[0] for _ in range(n_pl)]
        plan.append((d, pls))
    return plan, (n_prog, n_recib, n_boveda)


def monto():
    return round(random.uniform(1_000, 900_000), 2)


def seed(a):
    t_ini = time.perf_counter()
    print('\n== INVENTARIO PREVIO ==')
    cli = clientes()
    if not cli:
        raise RuntimeError('sin clientes: sanic_RemitentClient es requerido')
    prev_rec = receptions_stress('sanic_receiptid')
    prev_bag = bags_stress('sanic_bagid')
    prev_bat = batches_stress()
    print(f'  ya sembrado: lotes {len(prev_bat)} · planillas {len(prev_rec)} · tulas {len(prev_bag)}')
    seq_rec = next_correlativo(prev_rec, 'sanic_receiptid', RECEIPT_PREFIX)
    seq_bag = next_correlativo(prev_bag, 'sanic_bagid', BAG_PREFIX)
    print(f'  correlativos: planillas desde {RECEIPT_PREFIX}{seq_rec:06d} · '
          f'tulas desde {BAG_PREFIX}{seq_bag:06d}')

    plan, (n_prog, n_recib, n_boveda) = plan_volumes(a)
    n_pl = sum(len(p) for _, p in plan)
    n_bg = sum(sum(p) for _, p in plan)
    print('\n== PLAN ==')
    print(f'  lotes    : {len(plan)}  (En progreso {n_prog} · Recibido {n_recib} · '
          f'En boveda {n_boveda})')
    print(f'  planillas: {n_pl}')
    print(f'  tulas    : {n_bg}')
    print(f'  transiciones: {n_recib + n_boveda} a Recibido, {n_boveda} a En boveda')
    print(f'  muestra de transicion esclusa->boveda POST-cierre: {a.transition_sample} tulas')
    if a.dry_run:
        print('\n(dry-run) no se escribe nada')
        return

    tiempos = {}
    # ---------------- 1) lotes (nacen En progreso, guard ACTIVO) -------------
    print('\n== 1) LOTES ==')
    reqs = [('POST', f'/{ES_BATCH}', {
        'sanic_receptionstatus': BATCH_EN_PROGRESO,
        'sanic_transportadora': TRANSPORTADORA_PREFIX + random.choice(TRANSPORTADORAS),
    }) for _ in plan]
    res, tiempos['lotes'] = run_batches(reqs, 'creando lotes', a.chunk, a.workers)
    lotes = []
    for (destino, pls), (st, bid, err) in zip(plan, res):
        if err or not bid:
            continue
        lotes.append({'id': bid, 'destino': destino, 'pls': pls})
    print(f'  lotes creados: {len(lotes)}/{len(plan)}')
    if plan and not lotes:
        raise RuntimeError('no se creo ningun lote')

    # ---------------- 2) planillas (lote todavia abierto) --------------------
    print('\n== 2) PLANILLAS ==')
    reqs, meta = [], []
    for lote in lotes:
        for n_tulas in lote['pls']:
            rid = f'{RECEIPT_PREFIX}{seq_rec:06d}'
            seq_rec += 1
            c = random.choice(cli)
            reqs.append(('POST', f'/{ES_RECEPTION}', {
                'sanic_name': rid,
                'sanic_receiptid': rid,
                'sanic_ReceptionBatch@odata.bind': f'/{ES_BATCH}({lote["id"]})',
                'sanic_RemitentClient@odata.bind': f'/{ES_CLIENT}({c["sanic_chtblclientid"]})',
                'sanic_remitentname': c.get('sanic_name') or 'Remitente',
                'sanic_niocash': monto(), 'sanic_niocheck': monto(),
                'sanic_usdcash': round(monto() / 36.0, 2),
                'sanic_usdcheck': round(monto() / 36.0, 2),
            }))
            meta.append((lote, rid, n_tulas))
    res, tiempos['planillas'] = run_batches(reqs, 'creando planillas', a.chunk, a.workers)
    planillas = []
    for (lote, rid, n_tulas), (st, pid, err) in zip(meta, res):
        if err or not pid:
            continue
        planillas.append({'id': pid, 'rid': rid, 'lote': lote, 'n': n_tulas})
    print(f'  planillas creadas: {len(planillas)}/{len(meta)}')

    # ---------------- 3) tulas ----------------------------------------------
    # Las de lotes destinados a boveda NACEN en boveda (EvaluateBagCreate lo
    # permite). Una muestra nace en esclusa para probar la transicion real
    # DESPUES de que el lote se congele.
    print('\n== 3) TULAS ==')
    reqs, meta = [], []
    muestra_pendiente = a.transition_sample
    muestra_ids = []
    for pl in planillas:
        for _ in range(pl['n']):
            bid = f'{BAG_PREFIX}{seq_bag:06d}'
            seq_bag += 1
            en_boveda = pl['lote']['destino'] == BATCH_EN_BOVEDA
            es_muestra = False
            if en_boveda and muestra_pendiente > 0:
                es_muestra = True
                muestra_pendiente -= 1
            reqs.append(('POST', f'/{ES_BAG}', {
                'sanic_bagid': bid, 'sanic_name': bid,
                'sanic_ReceptionID@odata.bind': f'/{ES_RECEPTION}({pl["id"]})',
                'sanic_bagstatus': BAG_BOVEDA if (en_boveda and not es_muestra) else BAG_ESCLUSA,
            }))
            meta.append((bid, es_muestra))
    res, tiempos['tulas'] = run_batches(reqs, 'creando tulas', a.chunk, a.workers)
    n_bag_ok = 0
    for (bid, es_muestra), (st, gid, err) in zip(meta, res):
        if err or not gid:
            continue
        n_bag_ok += 1
        if es_muestra:
            muestra_ids.append(gid)
    print(f'  tulas creadas: {n_bag_ok}/{len(meta)}')

    # ---------------- 4) transicion En progreso -> Recibido -------------------
    print('\n== 4) TRANSICION  En progreso -> Recibido  (guard ACTIVO) ==')
    a_recibido = [l for l in lotes if l['destino'] in (BATCH_RECIBIDO, BATCH_EN_BOVEDA)]
    res, tiempos['t_recibido'] = run_batches(
        [('PATCH', f'/{ES_BATCH}({l["id"]})', {'sanic_receptionstatus': BATCH_RECIBIDO})
         for l in a_recibido], 'lotes -> Recibido', a.chunk, a.workers)
    rechazos = [(l['id'], err) for l, (st, _, err) in zip(a_recibido, res) if err]
    ok_recibido = [l for l, (st, _, err) in zip(a_recibido, res) if not err]

    # ---------------- 5) transicion Recibido -> En boveda --------------------
    print('\n== 5) TRANSICION  Recibido -> En boveda  (guard ACTIVO) ==')
    a_boveda = [l for l in ok_recibido if l['destino'] == BATCH_EN_BOVEDA]
    res, tiempos['t_boveda'] = run_batches(
        [('PATCH', f'/{ES_BATCH}({l["id"]})', {'sanic_receptionstatus': BATCH_EN_BOVEDA})
         for l in a_boveda], 'lotes -> En boveda', a.chunk, a.workers)
    rechazos += [(l['id'], err) for l, (st, _, err) in zip(a_boveda, res) if err]

    # ------------- 6) muestra: tula esclusa -> boveda con el lote CONGELADO --
    print('\n== 6) MUESTRA esclusa -> boveda con el lote YA en boveda ==')
    if muestra_ids:
        res, tiempos['muestra'] = run_batches(
            [('PATCH', f'/{ES_BAG}({g})', {'sanic_bagstatus': BAG_BOVEDA}) for g in muestra_ids],
            'tulas -> Almacenada en boveda', a.chunk, a.workers)
        m_err = [e for (_, _, e) in res if e]
        print(f'  transicion post-cierre: {len(res) - len(m_err)} ok · {len(m_err)} rechazadas')
        for e in m_err[:3]:
            print(f'      !! {e}', file=sys.stderr)
        rechazos += [('tula-muestra', e) for e in m_err]
    else:
        print('  (sin muestra)')

    # ------------- 7) LOTE GORDO: >500 tulas en UN SOLO lote ----------------
    # El mix normal da como mucho 16 tulas por lote, asi que las consultas
    # POR LOTE (Q-4 galTulasLot, Q-5 btnMoverBovedaLot, Q-9 edicion de lote)
    # nunca cruzarian el tope de delegacion de 500. Este lote las obliga:
    # queda en 'Recibido' con TODAS sus tulas en esclusa, que es exactamente el
    # estado en el que 'Mover a boveda' hace ClearCollect de >500 filas.
    if a.fat_batch > 0:
        print(f'\n== 7) LOTE GORDO ({a.fat_batch} tulas en UN lote) ==')
        res, _ = run_batches([('POST', f'/{ES_BATCH}', {
            'sanic_receptionstatus': BATCH_EN_PROGRESO,
            'sanic_transportadora': TRANSPORTADORA_PREFIX + 'Lote Gordo',
        })], 'creando lote gordo', 1, 1)
        fat_id = res[0][1]
        if not fat_id:
            print(f'  !! no se creo el lote gordo: {res[0][2]}', file=sys.stderr)
        else:
            por_pl = 50
            n_pl_fat = (a.fat_batch + por_pl - 1) // por_pl
            reqs, rids = [], []
            for _ in range(n_pl_fat):
                rid = f'{RECEIPT_PREFIX}{seq_rec:06d}'
                seq_rec += 1
                rids.append(rid)
                c = random.choice(cli)
                reqs.append(('POST', f'/{ES_RECEPTION}', {
                    'sanic_name': rid, 'sanic_receiptid': rid,
                    'sanic_ReceptionBatch@odata.bind': f'/{ES_BATCH}({fat_id})',
                    'sanic_RemitentClient@odata.bind':
                        f'/{ES_CLIENT}({c["sanic_chtblclientid"]})',
                    'sanic_remitentname': c.get('sanic_name') or 'Remitente',
                    'sanic_niocash': monto(), 'sanic_niocheck': monto(),
                    'sanic_usdcash': round(monto() / 36.0, 2),
                    'sanic_usdcheck': round(monto() / 36.0, 2),
                }))
            res, _ = run_batches(reqs, 'planillas del lote gordo', a.chunk, a.workers)
            fat_pl = [pid for (st, pid, err) in res if pid]
            reqs, restantes = [], a.fat_batch
            for pid in fat_pl:
                for _ in range(min(por_pl, restantes)):
                    bid = f'{BAG_PREFIX}{seq_bag:06d}'
                    seq_bag += 1
                    restantes -= 1
                    reqs.append(('POST', f'/{ES_BAG}', {
                        'sanic_bagid': bid, 'sanic_name': bid,
                        'sanic_ReceptionID@odata.bind': f'/{ES_RECEPTION}({pid})',
                        'sanic_bagstatus': BAG_ESCLUSA,
                    }))
            res, tiempos['lote_gordo'] = run_batches(reqs, 'tulas del lote gordo',
                                                     a.chunk, a.workers)
            n_ok = sum(1 for (st, gid, err) in res if gid)
            r2, _ = run_batches(
                [('PATCH', f'/{ES_BATCH}({fat_id})',
                  {'sanic_receptionstatus': BATCH_RECIBIDO})], 'lote gordo -> Recibido', 1, 1)
            if r2[0][2]:
                rechazos.append((fat_id, r2[0][2]))
            print(f'  lote gordo {fat_id}: {len(fat_pl)} planillas · {n_ok} tulas '
                  f'en esclusa · estado Recibido')

    # ---------------- resumen -----------------------------------------------
    total_ops = (len(plan) + len(meta) + len(planillas) + len(a_recibido) + len(a_boveda)
                 + len(muestra_ids))
    dt = time.perf_counter() - t_ini
    print('\n== RESUMEN DEL SEED ==')
    for k, v in tiempos.items():
        print(f'  {k:12} {v:7.1f}s')
    print(f'  TOTAL {dt:.1f}s · ~{total_ops} escrituras · '
          f'{total_ops / dt if dt else 0:.1f} op/s efectivas')
    print(f'  TRANSICIONES RECHAZADAS POR EL GUARD: {len(rechazos)}')
    for i, (rid, err) in enumerate(rechazos[:10]):
        print(f'    !! {rid}: {err}')
    return rechazos


# ===========================================================================
# CLEAN
# ===========================================================================
def clean(a):
    print('\n== LIMPIEZA ==')
    bats = batches_stress()
    recs = receptions_stress('sanic_receiptid,_sanic_receptionbatch_value')
    bags = bags_stress('sanic_bagid')
    print(f'  a borrar: tulas {len(bags)} · planillas {len(recs)} · lotes {len(bats)}')
    if not (bats or recs or bags):
        print('  nada marcado que borrar')
        mostrar_steps('steps (intactos):')
        return

    todos = mostrar_steps('steps ANTES:')
    pasos = [todos[n] for n in STEPS_CLEAN if n in todos]
    if len(pasos) != len(STEPS_CLEAN):
        raise RuntimeError('no se encontraron los 3 steps Delete del guard')

    ok = True
    try:
        for s in pasos:
            set_step(s['sdkmessageprocessingstepid'], False)
        print('  3 steps Delete del guard DESACTIVADOS (los espejos NO se tocan)')
        mostrar_steps('steps DURANTE:')

        for label, entity_set, rows in (
                ('borrando tulas', ES_BAG, bags),
                ('borrando planillas', ES_RECEPTION, recs),
                ('borrando lotes', ES_BATCH, bats)):
            if not rows:
                continue
            run_batches([('DELETE', f'/{entity_set}({r[PK[entity_set]]})', None) for r in rows],
                        label, a.chunk, a.workers)

        cli = get_all(f"/{ES_CLIENT}?$select=sanic_name&$filter="
                      + urllib.parse.quote(f"startswith(sanic_name,'{CLIENT_PREFIX}')"))
        if cli:
            run_batches([('DELETE', f'/{ES_CLIENT}({c["sanic_chtblclientid"]})', None)
                         for c in cli], 'borrando clientes STRESS', a.chunk, a.workers)
    finally:
        ok = reactivar(pasos)
        print('  steps Delete del guard REACTIVADOS' if ok
              else '  !! GUARD NO CONFIRMADO — revisar a mano')

    print(f'\n  quedan: lotes {len(batches_stress())} · '
          f'planillas {len(receptions_stress("sanic_receiptid"))} · '
          f'tulas {len(bags_stress("sanic_bagid"))}')
    mostrar_steps('\nESTADO FINAL DE LOS STEPS (deben quedar ACTIVOS):')
    if not ok:
        sys.exit(2)


# ===========================================================================
# VERIFICACION — las consultas EXACTAS de la app v2
# ===========================================================================
def percentiles(xs):
    xs = sorted(xs)
    if not xs:
        return 0, 0
    p50 = statistics.median(xs)
    p95 = xs[min(len(xs) - 1, int(round(0.95 * (len(xs) - 1))))]
    return p50 * 1000, p95 * 1000


def timed_query(path, n=12):
    ts, first = [], None
    for i in range(n):
        d, dt = req('GET', path, prefer='odata.maxpagesize=100', timed=True)
        ts.append(dt)
        if first is None:
            first = d
    return first, ts


def verify(a):
    print('\n' + '=' * 78)
    print('VERIFICACION — consultas de CH-App-Reception v2 a escala')
    print('=' * 78)
    res = {}

    # ---- 1) galLotesLot: lotes por estado, createdon desc ----
    # Q-1 real (§6.2): la vista 'Reception app view' (sin filtro propio) + estado +
    # StartsWith(Name) || StartsWith(Transportadora) + rango de createdon.
    desde = '2000-01-01T00:00:00Z'
    hasta = '2999-01-01T00:00:00Z'
    print('\n[1] galLotesLot — lotes por sanic_receptionstatus, orden createdon desc')
    for etiqueta, val in (('Recibido', BATCH_RECIBIDO), ('En boveda', BATCH_EN_BOVEDA),
                          ('En progreso', BATCH_EN_PROGRESO)):
        flt = f'sanic_receptionstatus eq {val}'
        real = count_of(ES_BATCH, flt)
        d = req('GET', f'/{ES_BATCH}?$count=true&$top=1&$select=sanic_name&$filter='
                + urllib.parse.quote(flt))
        odata_count = d.get('@odata.count')
        first, ts = timed_query(
            f'/{ES_BATCH}?$select=sanic_name,sanic_receptionstatus,sanic_transportadora,createdon'
            f'&$filter={urllib.parse.quote(flt)}&$orderby=createdon desc&$top=100')
        p50, p95 = percentiles(ts)
        # el predicado COMPLETO de la formula, con el buscador vacio
        flt_full = (f"{flt} and (startswith(sanic_name,'') or "
                    f"startswith(sanic_transportadora,'')) "
                    f"and createdon ge {desde} and createdon lt {hasta}")
        _, ts2 = timed_query(
            f'/{ES_BATCH}?$select=sanic_name,sanic_receptionstatus,sanic_transportadora,createdon'
            f'&$filter={urllib.parse.quote(flt_full)}&$orderby=createdon desc&$top=100', n=8)
        f50, f95 = percentiles(ts2)
        print(f'  {etiqueta:12} count REAL={real:5}  $count(odata)={odata_count:5}  '
              f'{">500 OK" if real > 500 else "<=500 "} '
              f'{">2000 OK" if real > 2000 else "        "}  '
              f'simple p50={p50:.0f}/p95={p95:.0f}ms · Q-1 completo p50={f50:.0f}/p95={f95:.0f}ms')
        res[f'lotes_{etiqueta}'] = real
        res[f'lat_lotes_{etiqueta}'] = (p50, p95)
        res[f'lat_Q1full_{etiqueta}'] = (f50, f95)
    # buscador con texto: StartsWith sobre la transportadora marcada
    flt_b = (f"startswith(sanic_transportadora,'{TRANSPORTADORA_PREFIX}') "
             f"and createdon ge {desde} and createdon lt {hasta}")
    nb = count_of(ES_BATCH, flt_b)
    _, tsb = timed_query(f'/{ES_BATCH}?$select=sanic_name,sanic_transportadora'
                         f'&$filter={urllib.parse.quote(flt_b)}&$orderby=createdon desc&$top=100',
                         n=8)
    b50, b95 = percentiles(tsb)
    print(f'  {"buscador":12} StartsWith(Transportadora,"{TRANSPORTADORA_PREFIX.strip()}") '
          f'-> {nb} filas · p50={b50:.0f}/p95={b95:.0f}ms')
    res['lat_buscador_lotes'] = (b50, b95)

    # ---- 2) galPlanillasLot: espejo sanic_batchstatus ----
    print('\n[2] galPlanillasLot — planillas por espejo sanic_batchstatus (INT)')
    for etiqueta, val in (('Recibido', BATCH_RECIBIDO), ('En boveda', BATCH_EN_BOVEDA)):
        espejo = count_of(ES_RECEPTION, f'sanic_batchstatus eq {val}')
        # join REAL: planillas cuyo LOTE esta en ese estado (via navegacion)
        real = count_of(ES_RECEPTION,
                        f'sanic_ReceptionBatch/sanic_receptionstatus eq {val}')
        first, ts = timed_query(
            f'/{ES_RECEPTION}?$select=sanic_receiptid,sanic_batchstatus,sanic_remitentname'
            f'&$filter=sanic_batchstatus eq {val}&$orderby=createdon desc&$top=100')
        p50, p95 = percentiles(ts)
        # Q-2 completo (§10.2): espejo + StartsWith x2 + rango de createdon
        flt_full = (f"sanic_batchstatus eq {val} and (startswith(sanic_receiptid,'') or "
                    f"startswith(sanic_remitentname,'')) and createdon ge {desde} "
                    f"and createdon lt {hasta}")
        _, ts2 = timed_query(
            f'/{ES_RECEPTION}?$select=sanic_receiptid,sanic_batchstatus,sanic_remitentname'
            f'&$filter={urllib.parse.quote(flt_full)}&$orderby=createdon desc&$top=100', n=8)
        f50, f95 = percentiles(ts2)
        marca = 'OK' if espejo == real else f'DESINCRONIZADAS {abs(espejo - real)}'
        print(f'  {etiqueta:12} espejo={espejo:5}  join real={real:5}  -> {marca}  '
              f'simple p50={p50:.0f}/p95={p95:.0f}ms · Q-2 completo '
              f'p50={f50:.0f}/p95={f95:.0f}ms')
        res[f'lat_Q2full_{etiqueta}'] = (f50, f95)
        res[f'planillas_espejo_{etiqueta}'] = (espejo, real)
        res[f'lat_planillas_{etiqueta}'] = (p50, p95)

    # ---- 3) tulas de UN lote grande por el espejo B-2 ----
    print('\n[3] galTulas — tulas de un lote por _sanic_receptionbatchid_value (espejo B-2)')
    bags = bags_stress('sanic_bagid,_sanic_receptionbatchid_value,_sanic_receptionid_value,'
                       'sanic_receiptidmirror,sanic_bagstatus')
    por_lote = {}
    for b in bags:
        por_lote.setdefault(b.get('_sanic_receptionbatchid_value'), []).append(b)
    por_lote.pop(None, None)
    if por_lote:
        lote_id, esperadas = max(por_lote.items(), key=lambda kv: len(kv[1]))
        got = count_of(ES_BAG, f'_sanic_receptionbatchid_value eq {lote_id}')
        first, ts = timed_query(
            f'/{ES_BAG}?$select=sanic_bagid,sanic_bagstatus,sanic_receiptidmirror'
            f'&$filter=_sanic_receptionbatchid_value eq {lote_id}&$top=100')
        p50, p95 = percentiles(ts)
        print(f'  lote {lote_id}: creadas {len(esperadas)} · devueltas {got} -> '
              f'{"OK" if got == len(esperadas) else "DISCREPANCIA"}  '
              f'p50={p50:.0f}ms p95={p95:.0f}ms')
        res['tulas_lote'] = (len(esperadas), got)
        res['lat_tulas'] = (p50, p95)
    else:
        print('  !! ninguna tula tiene el espejo B-2 estampado')
        res['tulas_lote'] = (0, 0)

    # ---- 4) dup-check delegable (bug C1 de la v1) ----
    print('\n[4] dup-check delegable — LookUp(receptionbatchid + bagid)')
    total_bags = count_of(ES_BAG, f"startswith(sanic_bagid,'{BAG_PREFIX}')")
    ordenadas = sorted([b for b in bags if b.get('_sanic_receptionbatchid_value')],
                       key=lambda b: b['sanic_bagid'])
    if len(ordenadas) > 600:
        objetivo = ordenadas[-1]                     # la ULTIMA de todo el universo
        pos = len(ordenadas)
        flt = (f"_sanic_receptionbatchid_value eq {objetivo['_sanic_receptionbatchid_value']} "
               f"and sanic_bagid eq '{objetivo['sanic_bagid']}'")
        d, dt = req('GET', f'/{ES_BAG}?$select=sanic_bagid&$filter=' + urllib.parse.quote(flt),
                    timed=True)
        hit = d.get('value', [])
        print(f'  universo de tulas STRESS: {total_bags} (>500 y >2000: '
              f'{"SI" if total_bags > 2000 else "no"})')
        print(f'  buscada {objetivo["sanic_bagid"]} (posicion global ~{pos}, muy '
              f'mas alla de la fila 500) -> {len(hit)} fila(s) en {dt * 1000:.0f}ms '
              f'{"ENCONTRADA" if hit else "NO ENCONTRADA (bug C1 vivo)"}')
        res['dupcheck'] = bool(hit)
        # control negativo: mismo bagid en OTRO lote no debe aparecer
        otro = next((b for b in ordenadas
                     if b['_sanic_receptionbatchid_value']
                     != objetivo['_sanic_receptionbatchid_value']), None)
        if otro:
            flt2 = (f"_sanic_receptionbatchid_value eq {otro['_sanic_receptionbatchid_value']} "
                    f"and sanic_bagid eq '{objetivo['sanic_bagid']}'")
            d2 = req('GET', f'/{ES_BAG}?$select=sanic_bagid&$filter=' + urllib.parse.quote(flt2))
            print(f'  control negativo (mismo numero, otro lote): '
                  f'{len(d2.get("value", []))} filas '
                  f'{"OK" if not d2.get("value") else "FALSO POSITIVO"}')
    else:
        print(f'  universo insuficiente ({len(ordenadas)} tulas con espejo)')
        res['dupcheck'] = None

    # ---- 5) espejos estampados ----
    print('\n[5] ESPEJOS estampados por los plugins bajo carga')
    rid_por_id = {r['sanic_chtblreceptionsid']: r for r in
                  receptions_stress('sanic_receiptid,sanic_batchstatus,'
                                    '_sanic_receptionbatch_value')}
    con_lote = sum(1 for b in bags if b.get('_sanic_receptionbatchid_value'))
    ok_receipt = 0
    mal_receipt = []
    for b in bags:
        r = rid_por_id.get(b.get('_sanic_receptionid_value'))
        if r and b.get('sanic_receiptidmirror') == r['sanic_receiptid']:
            ok_receipt += 1
        elif r:
            mal_receipt.append(b['sanic_bagid'])
    n = len(bags) or 1
    print(f'  tulas STRESS: {len(bags)}')
    print(f'  B-2 _sanic_receptionbatchid_value no nulo : {con_lote}/{len(bags)} '
          f'({100.0 * con_lote / n:.2f}%)')
    print(f'  B-3 sanic_receiptidmirror correcto        : {ok_receipt}/{len(bags)} '
          f'({100.0 * ok_receipt / n:.2f}%)  desincronizadas {len(mal_receipt)}')
    if mal_receipt[:5]:
        print(f'    ejemplos: {mal_receipt[:5]}')

    bat_por_id = {b['sanic_chtblreceptionbatchid']: b for b in batches_stress()}
    ok_bs, mal_bs, sin_bs = 0, [], 0
    for r in rid_por_id.values():
        lote = bat_por_id.get(r.get('_sanic_receptionbatch_value'))
        if not lote:
            continue
        if r.get('sanic_batchstatus') is None:
            sin_bs += 1
        elif r['sanic_batchstatus'] == lote['sanic_receptionstatus']:
            ok_bs += 1
        else:
            mal_bs.append((r['sanic_receiptid'], r['sanic_batchstatus'],
                           lote['sanic_receptionstatus']))
    tot_r = len(rid_por_id) or 1
    print(f'  planillas STRESS: {len(rid_por_id)}')
    print(f'  espejo sanic_batchstatus == estado del lote: {ok_bs}/{len(rid_por_id)} '
          f'({100.0 * ok_bs / tot_r:.2f}%)  nulo {sin_bs}  distinto {len(mal_bs)}')
    if mal_bs[:5]:
        print(f'    ejemplos (receiptid, espejo, lote): {mal_bs[:5]}')
    res['espejos'] = (con_lote, ok_receipt, len(bags), ok_bs, len(rid_por_id))

    # ---- 6) latencias resumidas ----
    print('\n[6] LATENCIAS $top=100 (lo que pide una galeria por pagina)')
    for k, v in res.items():
        if k.startswith('lat_'):
            print(f'  {k[4:]:24} p50={v[0]:6.0f}ms  p95={v[1]:6.0f}ms')
    return res


# ===========================================================================
def main():
    ap = argparse.ArgumentParser(description='Seeder de estres de RECEPCION (STRESS-RCP)')
    ap.add_argument('--batches', type=int, default=2400)
    ap.add_argument('--mix-progreso', type=float, default=250 / 24.0)   # % -> ~250
    ap.add_argument('--mix-boveda', type=float, default=1250 / 24.0)    # % -> ~1250
    ap.add_argument('--fat-batch', type=int, default=600,
                    help='tulas en UN solo lote, para cruzar el tope de 500 POR LOTE '
                         '(Q-4/Q-5/Q-9). Queda Recibido con las tulas en esclusa.')
    ap.add_argument('--transition-sample', type=int, default=200,
                    help='tulas que nacen en esclusa y se patchean a boveda POST-cierre')
    ap.add_argument('--chunk', type=int, default=100, help='requests por $batch')
    ap.add_argument('--workers', type=int, default=6, help='$batch en paralelo')
    ap.add_argument('--clean', action='store_true')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--verify', action='store_true', help='solo verificar, no sembrar')
    ap.add_argument('--no-verify', action='store_true', help='sembrar y no verificar')
    ap.add_argument('--restore-steps', action='store_true',
                    help='reactiva los 3 steps Delete del guard y sale (rescate)')
    a = ap.parse_args()

    print('=' * 78)
    print(f'SEEDER RECEPCION — marca {TRANSPORTADORA_PREFIX.strip()} / '
          f'{RECEIPT_PREFIX}* / {BAG_PREFIX}* — {URL}')
    print('=' * 78)

    if a.restore_steps:
        todos = leer_steps()
        pasos = [todos[n] for n in STEPS_CLEAN if n in todos]
        ok = reactivar(pasos)
        mostrar_steps('estado final de los steps:')
        sys.exit(0 if ok else 1)

    if a.clean:
        clean(a)
        return
    if a.verify:
        verify(a)
        return

    seed(a)
    if not a.no_verify:
        verify(a)


if __name__ == '__main__':
    main()
