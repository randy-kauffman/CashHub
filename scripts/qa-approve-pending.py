#!/usr/bin/env python3
"""QA helper: aprueba o rechaza solicitudes PENDIENTES por Web API, saltando el
cuatro-ojos client-side de CH-App-Supervisor. SOLO PARA QA EN DEV.

Uso:
  python3 scripts/qa-approve-pending.py                 # lista las pendientes
  python3 scripts/qa-approve-pending.py --approve       # aprueba la MAS RECIENTE
  python3 scripts/qa-approve-pending.py --approve N     # aprueba la solicitud numero N de la lista
  python3 scripts/qa-approve-pending.py --reject "motivo"       # rechaza la mas reciente con motivo
  python3 scripts/qa-approve-pending.py --reject "motivo" N     # rechaza la N

Requiere que el paso 'CashHub.GuardReception: Update de sanic_chtblapprovalrequest'
este DESACTIVADO (estado QA). Ver docs/recounting-rebuild/06-escenarios-qa.md §7.
"""
import json
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APROBADO = 159460000
RECHAZADO = 159460001


def token(env):
    url = env['DATAVERSE_URL'].rstrip('/')
    req = urllib.request.Request(
        f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
        data=urllib.parse.urlencode({
            'client_id': env['PP_CLIENT_ID'],
            'client_secret': env['PP_CLIENT_SECRET'],
            'scope': url + '/.default',
            'grant_type': 'client_credentials',
        }).encode())
    return json.load(urllib.request.urlopen(req))['access_token'], url + '/api/data/v9.2'


def main():
    env = {}
    for line in (ROOT / 'pp_secrets.env').read_text().splitlines():
        line = line.strip()
        if line and '=' in line and not line.startswith('#'):
            k, v = line.split('=', 1)
            env[k] = v.strip()
    tok, api = token(env)

    def call(method, path, body=None):
        req = urllib.request.Request(api + path, method=method,
                                     data=json.dumps(body).encode() if body else None)
        req.add_header('Authorization', 'Bearer ' + tok)
        req.add_header('Content-Type', 'application/json')
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else None

    flt = urllib.parse.quote('sanic_approvalstatus eq null')
    rows = call('GET', '/sanic_chtblapprovalrequests?$select=sanic_name,sanic_requesttype,'
                       'sanic_bagnumber,createdon&$filter=' + flt +
                       '&$orderby=createdon%20desc&$top=15')['value']
    if not rows:
        print('No hay solicitudes pendientes.')
        return

    print('PENDIENTES (mas reciente primero):')
    for i, r in enumerate(rows, 1):
        print(f"  [{i}] {r['sanic_name']}  {r['sanic_requesttype']}  "
              f"tula={r.get('sanic_bagnumber') or '-'}  {r['createdon']}")

    args = sys.argv[1:]
    if not args:
        return
    accion = args[0]
    resto = args[1:]
    motivo = None
    if accion == '--reject':
        if not resto:
            sys.exit('Falta el motivo: --reject "motivo" [N]')
        motivo, resto = resto[0], resto[1:]
    idx = int(resto[0]) - 1 if resto else 0
    target = rows[idx]

    body = {
        'sanic_approvalstatus': APROBADO if accion == '--approve' else RECHAZADO,
        'sanic_resolutiondatetime': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
    }
    if motivo:
        body['sanic_statusreason'] = motivo
    call('PATCH', f"/sanic_chtblapprovalrequests({target['sanic_chtblapprovalrequestid']})", body)
    verbo = 'APROBADA' if accion == '--approve' else f'RECHAZADA ("{motivo}")'
    print(f"\n{verbo}: {target['sanic_name']} ({target['sanic_requesttype']}, "
          f"tula={target.get('sanic_bagnumber') or '-'})")


if __name__ == '__main__':
    main()
