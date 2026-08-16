#!/usr/bin/env python3
"""Backfill del espejo de nombre de cliente en la sucursal — idempotente.

Estampa en las sucursales EXISTENTES lo que ClientBranchNameMirrorPlugin
estampa de aca en adelante:

    sanic_chtblclientbranch.sanic_clientnamemirror  <-  sanic_Client.sanic_name

Sin esto el espejo solo existe para lo que nazca despues del registro, y el
combo de sucursales de CH-App-Reception v2 muestra en blanco (y no encuentra)
todo el historico.

DOS USOS, el mismo script:
  1. Despues de create-clientbranch-mirror-column.sh + register-clientbranch-mirror.sh,
     para poner al dia lo que ya existia.
  2. Despues de un RENAME de cliente. El plugin NO cascadea el rename a proposito
     (ver la limitacion conocida en el header de ClientBranchNameMirrorPlugin):
     un cliente puede tener miles de sucursales y una cascada sincrona le pega N
     escrituras a una operacion que el usuario ve como corregir una letra. Este
     script es la cobertura de esa limitacion, y por eso es RE-EJECUTABLE.

Solo PATCHea lo que difiere del valor esperado: correrlo dos veces no genera
auditoria basura ni escrituras al pedo. Va por $batch (un changeset POR
REQUEST, igual que seed-reception-stress.py) porque a escala de 50.000
sucursales un PATCH unitario por fila es media hora de red.

Uso:
    python3 scripts/backfill-clientbranch-mirror.py            # estampar
    python3 scripts/backfill-clientbranch-mirror.py --dry-run  # solo el diff
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
# Mismo tope que ClientBranchNameMirrors.MaxNameLength en el dominio.
MAX_LEN = 850

# ---------- auth (mismo patron que los seeders del repo) ----------
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
    """[(status, error|None)] en ORDEN. Mismo parser que seed-reception-stress.py."""
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


def cap(value):
    """Misma normalizacion que el dominio (ClientBranchNameMirrors.CapName)."""
    if value is None:
        return None
    trimmed = str(value).strip()
    if not trimmed:
        return None
    return trimmed[:MAX_LEN]


_sets = {}


def entity_set(logical):
    """EntitySetName real desde metadata: no se adivina el plural."""
    if logical not in _sets:
        _sets[logical] = req(
            'GET', f"/EntityDefinitions(LogicalName='{logical}')?$select=EntitySetName"
        )['EntitySetName']
    return _sets[logical]


def main():
    ap = argparse.ArgumentParser(
        description='Backfill de sanic_clientnamemirror en las sucursales')
    ap.add_argument('--dry-run', action='store_true', help='solo reporta el diff')
    ap.add_argument('--chunk', type=int, default=100, help='PATCHes por $batch')
    ap.add_argument('--workers', type=int, default=6, help='$batch en paralelo')
    a = ap.parse_args()

    branches_set = entity_set('sanic_chtblclientbranch')
    print('=' * 78)
    print(f'BACKFILL sanic_clientnamemirror — {URL}')
    print('=' * 78)

    t0 = time.perf_counter()
    # $expand del lookup: el nombre del cliente viaja con la sucursal en la MISMA
    # query. Un Retrieve de cliente por sucursal seria N+1 sobre 50.000 filas.
    rows = get_all(
        f'/{branches_set}?$select=sanic_chtblclientbranchid,sanic_name,sanic_clientnamemirror'
        '&$expand=sanic_Client($select=sanic_name)')
    print(f'  sucursales leidas: {len(rows)} en {time.perf_counter() - t0:.1f}s')

    patches, sin_cliente, ya_ok = [], 0, 0
    for row in rows:
        client = row.get('sanic_Client') or {}
        want = cap(client.get('sanic_name'))
        if want is None:
            # Sin cliente no hay de donde derivar. Solo se limpia si habia algo:
            # borrar por borrar es auditoria basura.
            sin_cliente += 1
            if cap(row.get('sanic_clientnamemirror')) is None:
                continue
        if cap(row.get('sanic_clientnamemirror')) == want:
            ya_ok += 1
            continue
        patches.append(('PATCH', f"/{branches_set}({row['sanic_chtblclientbranchid']})",
                        {'sanic_clientnamemirror': want}))

    print(f'  ya al dia: {ya_ok} · a estampar: {len(patches)} · sin cliente: {sin_cliente}')

    if not patches:
        print('Nada que estampar: el espejo ya esta al dia.')
        return
    if a.dry_run:
        print('(dry-run) no se escribe nada')
        return

    res, dt = run_batches(patches, 'estampando espejo', a.chunk, a.workers)
    n_err = sum(1 for (_, e) in res if e)
    print(f'\nBackfill completo: {len(res) - n_err} sucursales estampadas · '
          f'{n_err} errores · {dt:.1f}s')
    if n_err:
        sys.exit(1)


if __name__ == '__main__':
    main()
