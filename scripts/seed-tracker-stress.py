#!/usr/bin/env python3
"""Seed de ESTRÉS para CH-App-DepositsTracker — datos sintéticos coherentes.

Genera la cadena completa (correo → validaciones → declaración → recepción →
recuento → transacciones) caminando la máquina de estados REAL (los guards
server-side aplican), con volúmenes que cruzan los umbrales de delegación:

  ~220 correos + runs + ~6.000 validation results (con Omitida/Fail reales)
  ~180 declaraciones, ~450 bolsas, ~2.400 depósitos declarados, ~1.400 cheques decl.
  37 lotes de recepción (1 MONSTRUO de 600 planillas), ~1.500 planillas
  ~3.300 tulas (65% recontadas, ~15% con diferencia real), ~25 sesiones
  ~5.500 depósitos recontados (1 tula monstruo con 80), ~3.500 cheques
  ~6.500 transacciones (+ faltantes/sobrantes) y cortes TB vía Custom API

TODO lleva el marcador STRESS (transportadora, receiptid, bagid, referencias,
correos stress+N@stress.test) para poder limpiarlo con filtros.

Uso: python3 scripts/seed-tracker-stress.py [--scale 1.0]
Requiere pp_secrets.env. Corre en ~15-30 min (los plugins hacen su trabajo real).
"""
import argparse
import json
import os
import random
import sys
import threading
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta

random.seed(20260814)

# ---------- auth ----------
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
env = {}
with open(os.path.join(ROOT, os.environ.get('ENV_FILE', 'pp_secrets.env'))) as f:
    for line in f:
        line = line.strip().rstrip('\r')
        if line and not line.startswith('#') and '=' in line:
            k, v = line.split('=', 1)
            env[k.strip()] = v.strip()
URL = env['DATAVERSE_URL'].rstrip('/')
API = URL + '/api/data/v9.2'
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


def req(method, path, body=None, retry=3):
    for attempt in range(retry):
        r = urllib.request.Request(API + path.replace(' ', '%20'), method=method,
            data=json.dumps(body).encode() if body is not None else None)
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', 'application/json')
        if method == 'POST':
            r.add_header('Prefer', 'return=representation')
        try:
            with urllib.request.urlopen(r, timeout=120) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw.strip() else {}
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:300]
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                continue
            if e.code == 429 and attempt < retry - 1:
                import time
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            raise RuntimeError(f"{method} {path}: {e.code} {msg}") from e
    raise RuntimeError('sin reintentos')


def get_all(path):
    rows, url = [], path
    while url:
        d = req('GET', url)
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def create(entityset, body):
    d = req('POST', f'/{entityset}', body)
    for k, v in d.items():
        if k.endswith('id') and not k.startswith('@') and isinstance(v, str) and len(v) == 36:
            return v
    raise RuntimeError(f'sin id en respuesta de {entityset}')


def para(fn, items, workers=10, label=''):
    out, errs = [], 0
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(fn, it): it for it in items}
        for i, fut in enumerate(as_completed(futs), 1):
            try:
                out.append(fut.result())
            except Exception as e:
                errs += 1
                if errs <= 5:
                    print(f'  !! {label}: {e}', file=sys.stderr)
            if i % 250 == 0:
                print(f'  {label}: {i}/{len(items)} (errores {errs})')
    if errs:
        print(f'  {label}: TERMINADO con {errs} errores de {len(items)}')
    return out


ap = argparse.ArgumentParser()
ap.add_argument('--scale', type=float, default=1.0)
S = ap.parse_args().scale
N = lambda n: max(1, int(n * S))

BASE = datetime(2026, 7, 10)
iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')
CUR = {'NIO': 159460000, 'USD': 159460001, 'EUR': 159460002}
CURW = ['NIO'] * 70 + ['USD'] * 25 + ['EUR'] * 5
monto = lambda lo, hi: round(random.uniform(lo, hi), 2)

SUCURSALES = ['Metrocentro', 'León', 'Masaya', 'Estelí', 'Granada', 'Chinandega',
              'Matagalpa', 'Rivas', 'Jinotega', 'Bluefields', 'Juigalpa', 'Ocotal']

print('== Referencias del entorno')
clients = get_all("/sanic_chtblclients?$select=sanic_chtblclientid,sanic_name&$top=200")
if not clients:
    sys.exit('No hay clientes en el entorno — sembrar clientes primero.')
users = get_all("/systemusers?$select=systemuserid,azureactivedirectoryobjectid,fullname"
                "&$filter=isdisabled eq false and azureactivedirectoryobjectid ne null&$top=20")
sp_user = get_all(f"/systemusers?$select=systemuserid&$filter=azureactivedirectoryobjectid eq "
                  f"{urllib.parse.quote(env.get('PP_SP_OBJECT_ID', '00000000-0000-0000-0000-000000000000'))}")
whoami = req('GET', '/WhoAmI()')
sp_id = whoami['UserId']
desks = get_all("/sanic_chtblcountingdesks?$select=sanic_chtblcountingdeskid&$top=10")
if not desks:
    desks = [{'sanic_chtblcountingdeskid': create('sanic_chtblcountingdesks',
              {'sanic_name': f'STRESS Desk {i}'})} for i in range(1, 4)]
rules = get_all("/sanic_validationruleses?$select=sanic_validationrulesid,sanic_validationname,"
                "sanic_validationorder&$orderby=sanic_validationorder asc")
print(f'  clientes={len(clients)} usuarios={len(users)} desks={len(desks)} reglas={len(rules)}')
cli = lambda: random.choice(clients)
cliref = lambda c: f"/sanic_chtblclients({c['sanic_chtblclientid']})"

# =====================================================================
# 1) CORREOS + RUNS + RESULTS + DECLARACIONES (cadena documental)
# =====================================================================
print('== 1/6 Correos, validaciones y declaraciones')
n_mail = N(220)
mails = []


def mk_mail(i):
    c = cli()
    when = BASE + timedelta(minutes=random.randint(0, 60 * 24 * 30))
    destino = random.random()
    # 82% aceptado, 12% denegado, 6% error
    status = 159460001 if destino < 0.82 else (159460005 if destino < 0.94 else 159460004)
    mid = create('sanic_chtblreceivedemailses', {
        'sanic_sourceemailaddress': f'stress+{i}@stress.test',
        'sanic_subject': f"Declaracion {c['sanic_name']} {when:%d/%m}",
        'sanic_receptiondatetime': iso(when),
        'sanic_receivedemailinternetmessageid': f'<stress-{i}@stress.test>',
        'sanic_receivedemailoutlookuniqueid': f'STRESS-OUTLOOK-{i}',
        'sanic_Client@odata.bind': cliref(c),
        'sanic_emailreviewstatus': status,
        'sanic_validationstatus': 159460001 if status == 159460001 else 159460002,
        'sanic_emailreplystatus': 159460001,
        'sanic_replysubject': ('Declaracion recibida — pendiente' if status == 159460001
                               else 'Declaracion rechazada — pendiente'),
        'sanic_replybodyhtml': f"<p>Estimado cliente {c['sanic_name']}: su declaracion fue "
                               f"{'ACEPTADA' if status == 159460001 else 'RECHAZADA'} "
                               f"(gestion de estres #{i}).</p>",
        'sanic_replydatetime': iso(when + timedelta(minutes=2)),
        'sanic_repliedby': 'CashHub (automatico)',
    })
    return {'id': mid, 'i': i, 'client': c, 'when': when, 'ok': status == 159460001}


mails = para(mk_mail, range(n_mail), label='correos')


def mk_run(m):
    rid = create('sanic_chtblvalidationrunses', {
        'sanic_runstart': iso(m['when']), 'sanic_runend': iso(m['when'] + timedelta(seconds=40)),
        'sanic_runresult': 159460000 if m['ok'] else 159460001,
        'sanic_sourcerecordid': m['id'],
        'sanic_ReceivedEmail@odata.bind': f"/sanic_chtblreceivedemailses({m['id']})",
    })
    fail_at = random.randint(4, len(rules) - 6) if not m['ok'] else -1
    for idx, r in enumerate(rules):
        if m['ok'] or idx < fail_at:
            outcome, name, detail = 159460000, f"{r['sanic_validationname']} Pass", None
        elif idx == fail_at:
            outcome = 159460001
            name = f"{r['sanic_validationname']} FAIL"
            detail = f"Valor invalido en la fila {random.randint(3, 40)} (seed de estres)"
        else:
            outcome, name = 159460002, f"{r['sanic_validationname']} Omitida"
            detail = f"Omitida: dependencia {rules[fail_at]['sanic_validationname']} fallo"
        body = {
            'sanic_name': name,
            'sanic_ValidationRun@odata.bind': f'/sanic_chtblvalidationrunses({rid})',
            'sanic_ValidationRule@odata.bind': f"/sanic_validationruleses({r['sanic_validationrulesid']})",
            'sanic_validationresult': outcome,
            'sanic_durationms': random.randint(2, 300),
        }
        if detail:
            body['sanic_detail'] = detail
            if outcome == 159460001:
                body['sanic_sheet'] = 'Declaracion'
                body['sanic_cellref'] = f'C{random.randint(3, 40)}'
        create('sanic_chtblvalidationresultses', body)
    return rid


para(mk_run, mails, workers=8, label='runs+results')

declaraciones = []


def mk_decl(m):
    did = create('sanic_chtbldeclarations', {
        'sanic_name': f"Declaracion {m['when']:%Y-%m-%d %H:%M} correo {m['id']}",
        'sanic_client@odata.bind': cliref(m['client']),
        'sanic_sourceentity': 'sanic_chtblreceivedemails',
        'sanic_sourcerecordid': m['id'],
        'sanic_ReceivedEmail@odata.bind': f"/sanic_chtblreceivedemailses({m['id']})",
    })
    bolsas = []
    monster = m['i'] == 0  # el primer correo: declaracion MONSTRUO
    for b in range(60 if monster else random.randint(1, 4)):
        deps = []
        for _ in range(8 if monster else random.randint(3, 8)):
            mon = random.choice(CURW)
            cash = monto(500, 90000)
            bac = monto(0, 25000) if random.random() < 0.45 else 0.0
            otr = monto(0, 12000) if random.random() < 0.25 else 0.0
            deps.append({'mon': mon, 'cash': cash, 'bac': bac, 'otr': otr,
                         'total': round(cash + bac + otr, 2)})
        tot = lambda mo: round(sum(d['total'] for d in deps if d['mon'] == mo), 2)
        bid = create('sanic_chtbldeclaredbagses', {
            'sanic_name': f"STRESS-B{m['i']}-{b}", 'sanic_bagnumber': f"STRESS-B{m['i']}-{b}",
            'sanic_DeclarationID@odata.bind': f'/sanic_chtbldeclarations({did})',
            'sanic_totalnio': tot('NIO'), 'sanic_totalusd': tot('USD'), 'sanic_totaleur': tot('EUR'),
        })
        dep_ids = []
        for k, d in enumerate(deps):
            depid = create('sanic_chtbldeclareddepositses', {
                'sanic_name': f'Deposito {k + 1}', 'sanic_transactionno': k + 1,
                'sanic_accountnumber': random.randint(100000000, 999999999),
                'sanic_currency': CUR[d['mon']],
                'sanic_cashamount': d['cash'], 'sanic_baclocalcheckamount': d['bac'],
                'sanic_otherlocalcheckamount': d['otr'], 'sanic_total': d['total'],
                'sanic_depositreference': f"DEP-STRESS-{m['i']}-{b}-{k}",
                'sanic_DeclaredBag@odata.bind': f'/sanic_chtbldeclaredbagses({bid})',
                'sanic_Declaration@odata.bind': f'/sanic_chtbldeclarations({did})',
            })
            checks = []
            if d['bac'] > 0:
                cid = create('sanic_chtbldeclaredcheckses', {
                    'sanic_name': f"Cheque STRESS-{m['i']}-{b}-{k}",
                    'sanic_checknumber': f"CHK-STRESS-{m['i']}{b}{k}",
                    'sanic_checkamount': d['bac'], 'sanic_checkcurrency': CUR[d['mon']],
                    'sanic_checktype': 159460000,
                    'sanic_DeclaredDepositID@odata.bind': f'/sanic_chtbldeclareddepositses({depid})',
                    'sanic_Declaration@odata.bind': f'/sanic_chtbldeclarations({did})',
                })
                checks.append({'id': cid, 'amount': d['bac'], 'mon': d['mon'], 'tipo': 159460000})
            d.update({'id': depid, 'checks': checks})
            dep_ids.append(d)
        bolsas.append({'id': bid, 'deps': dep_ids,
                       'nio': tot('NIO'), 'usd': tot('USD'), 'eur': tot('EUR')})
    return {'id': did, 'mail': m, 'bolsas': bolsas}


declaraciones = para(mk_decl, [m for m in mails if m['ok']], workers=8, label='declaraciones')

# =====================================================================
# 2) SESIONES DE RECUENTO
# =====================================================================
print('== 2/6 Sesiones de recuento')
sesiones = []
for i in range(N(25)):
    sid = create('sanic_chtblrecountingsessions', {
        'sanic_Cashier@odata.bind': f'/systemusers({sp_id})',
        'sanic_CountingDesk@odata.bind':
            f"/sanic_chtblcountingdesks({random.choice(desks)['sanic_chtblcountingdeskid']})",
        'sanic_confirmed': True,
    })
    sesiones.append(sid)

# =====================================================================
# 3) LOTES + PLANILLAS + TULAS (con maquina de estados real)
# =====================================================================
print('== 3/6 Lotes, planillas y tulas')
bolsas_pool = [b for d in declaraciones for b in d['bolsas']]
random.shuffle(bolsas_pool)
pool_lock = threading.Lock()


def next_bolsa():
    with pool_lock:
        return bolsas_pool.pop() if bolsas_pool else None


lotes_spec = [{'planillas': random.randint(15, 35)} for _ in range(N(36))]
lotes_spec.append({'planillas': 600, 'monster': True})
tulas_creadas = []


def mk_lote(spec):
    lid = create('sanic_chtblreceptionbatchs', {
        'sanic_transportadora': random.choice(['STRESS-PROVAL', 'STRESS-SERVIPRO', 'STRESS-RANITA']),
        'sanic_receptionstatus': 159460000,  # En progreso (nacimiento legal)
    })
    monster_planilla = spec.get('monster')
    for p in range(spec['planillas']):
        c = cli()
        pid = create('sanic_chtblreceptionses', {
            'sanic_name': f'Planilla STRESS {lid[:6]}-{p}',
            'sanic_receiptid': f'PL-STRESS-{lid[:6]}-{p}',
            'sanic_remitentname': f"Sucursal {random.choice(SUCURSALES)}",
            'sanic_RemitentClient@odata.bind': cliref(c),
            'sanic_ReceptionBatch@odata.bind': f'/sanic_chtblreceptionbatchs({lid})',
            'sanic_niocash': monto(5000, 300000), 'sanic_niocheck': monto(0, 80000),
            'sanic_usdcash': monto(0, 20000), 'sanic_usdcheck': monto(0, 8000),
        })
        # tulas: 300 en la planilla 0 del lote monstruo; si no 1-3
        n_tulas = 300 if (monster_planilla and p == 0) else random.randint(1, 3)
        for t in range(n_tulas):
            bolsa = next_bolsa() if random.random() < 0.6 else None
            body = {
                'sanic_name': f'T-STRESS-{lid[:6]}-{p}-{t}',
                'sanic_bagid': f'T-STRESS-{lid[:6]}-{p}-{t}',
                'sanic_ReceptionID@odata.bind': f'/sanic_chtblreceptionses({pid})',
                'sanic_bagstatus': 159460000,  # Recibida en esclusa (nacimiento legal)
            }
            if bolsa:
                body['sanic_BagDeclarationID@odata.bind'] = f"/sanic_chtbldeclaredbagses({bolsa['id']})"
            tid = create('sanic_chtblbagses', body)
            if bolsa:
                req('PATCH', f"/sanic_chtbldeclaredbagses({bolsa['id']})", {
                    'sanic_BagID@odata.bind': f'/sanic_chtblbagses({tid})',
                    'sanic_isrelatedtobag': True})
            tulas_creadas.append({'id': tid, 'bolsa': bolsa,
                                  'monster_tula': monster_planilla and p == 0 and t == 0})
    return lid


lotes = para(mk_lote, lotes_spec, workers=6, label='lotes')

# =====================================================================
# 4) RECUENTO: transiciones de tula + depositos + cheques
# =====================================================================
print('== 4/6 Recuento (transiciones, depositos, cheques)')
CHECK_TRANS = [  # (peso, secuencia de estados post-Recontado)
    (55, []), (20, [159460003]), (15, [159460003, 159460004]), (10, [159460003, 159460005]),
]
depositos_recontados = []


def recuento(t):
    # 65% de las tulas llegan a Recontada; el resto queda en esclusa/cajero
    destino = random.random()
    if destino < 0.20:
        return None  # queda en esclusa
    ses = random.choice(sesiones)
    req('PATCH', f"/sanic_chtblbagses({t['id']})", {
        'sanic_bagstatus': 159460002,  # Asignada a cajero
        'sanic_RecountingSession@odata.bind': f'/sanic_chtblrecountingsessions({ses})'})
    if destino < 0.35:
        return None  # queda asignada
    bolsa = t['bolsa']
    con_dif = random.random() < 0.15
    deps_decl = bolsa['deps'] if bolsa else []
    n_deps = 80 if t.get('monster_tula') else (len(deps_decl) or random.randint(1, 4))
    tot = {'NIO': 0.0, 'USD': 0.0, 'EUR': 0.0}
    for k in range(n_deps):
        dd = deps_decl[k] if k < len(deps_decl) else None
        mon = dd['mon'] if dd else random.choice(CURW)
        cash = dd['cash'] if dd else monto(500, 60000)
        bac = dd['bac'] if dd else (monto(0, 15000) if random.random() < 0.4 else 0.0)
        otr = dd['otr'] if dd else 0.0
        if con_dif and k == 0:
            cash = round(cash + random.choice([-1, 1]) * monto(50, 2000), 2)
        total = round(cash + bac + otr, 2)
        body = {
            'sanic_cashamount': cash, 'sanic_baclocalcheckamount': bac,
            'sanic_otherlocacheckamount': otr, 'sanic_total': total,
            'sanic_currency': CUR[mon],
            'sanic_accountnumber': random.randint(100000000, 999999999),
            'sanic_transactionnumber': k + 1,
            'sanic_BagID@odata.bind': f"/sanic_chtblbagses({t['id']})",
        }
        if dd:
            body['sanic_RelatedDeclaredDeposit@odata.bind'] = \
                f"/sanic_chtbldeclareddepositses({dd['id']})"
        depid = create('sanic_chtbldepositsrecountings', body)
        tot[mon] = round(tot[mon] + total, 2)
        # cheques fisicos (nacen Recontado; el handshake camina despues)
        n_cheq = 3 if t.get('monster_tula') else (1 if bac > 0 else 0)
        for q in range(n_cheq):
            cbody = {
                'sanic_name': f"CK STRESS {t['id'][:6]}-{k}-{q}",
                'sanic_checknumber': f"CHK-STRESS-{t['id'][:6]}{k}{q}",
                'sanic_checkamount': round(max(bac, 100) / max(n_cheq, 1), 2),
                'sanic_checkcurrency': CUR[mon], 'sanic_checktypes': 159460000,
                'sanic_checkstatus': 159460000,
                'sanic_RelatedReCounterDepositID@odata.bind':
                    f'/sanic_chtbldepositsrecountings({depid})',
            }
            if dd and dd['checks'] and q < len(dd['checks']):
                cbody['sanic_RelatedDeclaredCheck@odata.bind'] = \
                    f"/sanic_chtbldeclaredcheckses({dd['checks'][q]['id']})"
            cid = create('sanic_chtblcheckses', cbody)
            seq = random.choices([s for _, s in CHECK_TRANS], [w for w, _ in CHECK_TRANS])[0]
            for st in seq:
                req('PATCH', f'/sanic_chtblcheckses({cid})', {'sanic_checkstatus': st})
        depositos_recontados.append({'id': depid, 'mon': mon, 'total': total,
                                     'cash': cash, 'bac': bac, 'otr': otr,
                                     'dif': con_dif and k == 0,
                                     'decl_total': dd['total'] if dd else None})
    # Recontada + totales (dispara B5 con retrieve del DeclaredBag)
    req('PATCH', f"/sanic_chtblbagses({t['id']})", {
        'sanic_bagstatus': 159460003,
        'sanic_totalnio': tot['NIO'], 'sanic_totalusd': tot['USD'], 'sanic_totaleur': tot['EUR']})
    return t['id']


para(recuento, tulas_creadas, workers=8, label='recuento')

# =====================================================================
# 5) TRANSACCIONES (+ faltantes/sobrantes)
# =====================================================================
print('== 5/6 Transacciones')


def mk_tx(d):
    ids = []
    ids.append(create('sanic_chtbltransactionses', {
        'sanic_transactiontype': 159460000,  # Deposito
        'sanic_cashamount': d['cash'], 'sanic_baclocalcheckamount': d['bac'],
        'sanic_otherlocalcheckamount': d['otr'], 'sanic_total': d['total'],
        'sanic_currency': CUR[d['mon']],
        'sanic_accountnumber': random.randint(100000000, 999999999),
        'sanic_transactionreference': f"STRESS-{d['id'][:8]}",
        'sanic_RelatedDepositRecounting@odata.bind':
            f"/sanic_chtbldepositsrecountings({d['id']})",
    }))
    if d['dif'] and d['decl_total'] is not None:
        delta = round(d['total'] - d['decl_total'], 2)
        if abs(delta) >= 0.01:
            ids.append(create('sanic_chtbltransactionses', {
                'sanic_transactiontype': 159460001 if delta < 0 else 159460002,
                'sanic_cashamount': abs(delta), 'sanic_baclocalcheckamount': 0.0,
                'sanic_otherlocalcheckamount': 0.0, 'sanic_total': abs(delta),
                'sanic_currency': CUR[d['mon']],
                'sanic_transactionprocessingtype': 159460000,
                'sanic_transactionreference': f"STRESS-DIF-{d['id'][:8]}",
                'sanic_RelatedDepositRecounting@odata.bind':
                    f"/sanic_chtbldepositsrecountings({d['id']})",
            }))
    return ids


para(mk_tx, depositos_recontados, workers=10, label='transacciones')

# =====================================================================
# 6) CORTES TB (Custom API real) + transiciones con segregacion
# =====================================================================
print('== 6/6 Cortes TB')
otros = [u for u in users if u['systemuserid'] != sp_id]
for corte in range(4):
    try:
        out = req('POST', '/sanic_CHCreateTransactionCut', {'CajeroId': sp_id})
        bid, bname = out.get('BatchId'), out.get('BatchName')
        print(f"  corte {corte + 1}: {bname} con {out.get('AssignedCount')} tx")
        if not bid or corte == 3:
            break  # el ultimo lote queda Generado
        req('PATCH', f'/sanic_transactionbatcheses({bid})', {'sanic_batchstatus': 159460001})
        if corte < 2 and otros:
            # Aplicado impersonando a otro usuario (segregacion quien-ingresa != quien-aplica)
            imp = otros[corte % len(otros)]['azureactivedirectoryobjectid']
            r = urllib.request.Request(
                API + f'/sanic_transactionbatcheses({bid})', method='PATCH',
                data=json.dumps({'sanic_batchstatus': 159460003}).encode())
            r.add_header('Authorization', 'Bearer ' + token())
            r.add_header('Content-Type', 'application/json')
            r.add_header('CallerObjectId', imp)
            try:
                urllib.request.urlopen(r, timeout=120)
                print(f'  {bname} -> Aplicado (impersonando)')
            except urllib.error.HTTPError as e:
                print(f'  {bname} quedo Ingresado (aplicar fallo: {e.read().decode()[:150]})')
    except Exception as e:
        print(f'  corte {corte + 1} fallo: {e}')
        break

# Lotes de recepcion: algunos avanzan Recibido / En boveda (legal, congela hijos AL FINAL)
print('== Transiciones de lotes de recepcion')
for i, lid in enumerate(lotes):
    if i % 3 == 0:
        continue  # queda En progreso
    try:
        req('PATCH', f'/sanic_chtblreceptionbatchs({lid})', {'sanic_receptionstatus': 159460001})
        if i % 3 == 2:
            req('PATCH', f'/sanic_chtblreceptionbatchs({lid})', {'sanic_receptionstatus': 159460003})
    except Exception as e:
        print(f'  lote {lid[:8]}: {e}')

print('SEED DE ESTRÉS COMPLETO.')
print('Limpieza futura: filtrar por transportadora startswith STRESS, receiptid PL-STRESS-*, ')
print('bagid T-STRESS-*, sourceemailaddress *@stress.test, referencias STRESS-*.')
