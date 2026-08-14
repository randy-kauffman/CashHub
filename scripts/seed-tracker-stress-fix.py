#!/usr/bin/env python3
"""Reparación del seed de estrés: la cadena DECLARADA + lotes TB.

La corrida original falló en mk_decl por el casing de la nav property
(sanic_client vs sanic_Client) y el corte TB gigante superó el límite de 2 min
del Custom API síncrono. Este script:

  1. Crea ~180 declaraciones (una por correo stress aceptado, lookup B1).
  2. Para ~60% de las tulas STRESS recontadas: construye la bolsa declarada y
     sus depósitos declarados ESPEJANDO los montos recontados reales (15% con
     diferencia intencional), enlaza RelatedDeclaredDeposit, crea cheques
     declarados y enlaza los físicos (dispara B3), y cuelga la tula de su bolsa
     (dispara B5 → DeclTotal* + HasDifference).
  3. Crea transacciones Faltante/Sobrante para las tulas con diferencia.
  4. Arma 4 lotes TB DIRECTOS (~150 tx c/u) y los transiciona con segregación
     (Aplicado impersonando otro usuario via CallerObjectId).

Idempotencia básica: solo toca tulas STRESS recontadas SIN declaración.
Uso: python3 -u scripts/seed-tracker-stress-fix.py
"""
import json
import os
import random
import sys
import threading
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

random.seed(20260815)
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
_lock = threading.Lock()
_tok = {'v': None}


def token():
    with _lock:
        if _tok['v'] is None:
            body = urllib.parse.urlencode({
                'grant_type': 'client_credentials', 'client_id': env['PP_CLIENT_ID'],
                'client_secret': env['PP_CLIENT_SECRET'], 'scope': URL + '/.default'}).encode()
            _tok['v'] = json.load(urllib.request.urlopen(urllib.request.Request(
                f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
                data=body)))['access_token']
        return _tok['v']


def req(method, path, body=None, caller=None, retry=3):
    for attempt in range(retry):
        r = urllib.request.Request(API + path.replace(' ', '%20'), method=method,
            data=json.dumps(body).encode() if body is not None else None)
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', 'application/json')
        if method == 'POST':
            r.add_header('Prefer', 'return=representation')
        if caller:
            r.add_header('CallerObjectId', caller)
        try:
            with urllib.request.urlopen(r, timeout=180) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw.strip() else {}
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:250]
            if e.code == 401 and attempt < retry - 1:
                with _lock:
                    _tok['v'] = None
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


def create(es, body):
    d = req('POST', f'/{es}', body)
    for k, v in d.items():
        if k.endswith('id') and not k.startswith('@') and isinstance(v, str) and len(v) == 36:
            return v
    raise RuntimeError(f'sin id de {es}')


def para(fn, items, workers=10, label=''):
    out, errs = [], 0
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = [ex.submit(fn, it) for it in items]
        for i, fut in enumerate(as_completed(futs), 1):
            try:
                out.append(fut.result())
            except Exception as e:
                errs += 1
                if errs <= 8:
                    print(f'  !! {label}: {e}')
            if i % 200 == 0:
                print(f'  {label}: {i}/{len(items)} (errores {errs})')
    print(f'  {label}: fin {len(items)} (errores {errs})')
    return out


CUR_NAME = {159460000: 'NIO', 159460001: 'USD', 159460002: 'EUR'}

print('== Referencias')
mails = get_all("/sanic_chtblreceivedemailses?$select=sanic_chtblreceivedemailsid,"
                "_sanic_client_value&$filter=endswith(sanic_sourceemailaddress,'stress.test') "
                "and sanic_emailreviewstatus eq 159460001")
whoami = req('GET', '/WhoAmI()')
sp_id = whoami['UserId']
users = get_all("/systemusers?$select=systemuserid,azureactivedirectoryobjectid"
                "&$filter=isdisabled eq false and azureactivedirectoryobjectid ne null&$top=10")
otros = [u for u in users if u['systemuserid'] != sp_id]
print(f'  correos aceptados={len(mails)} otros_usuarios={len(otros)}')

# ---------------------------------------------------------------
print('== 1/4 Declaraciones (sanic_Client con C mayuscula)')


def mk_decl(m):
    body = {
        'sanic_name': f"Declaracion STRESS correo {m['sanic_chtblreceivedemailsid'][:8]}",
        'sanic_sourceentity': 'sanic_chtblreceivedemails',
        'sanic_sourcerecordid': m['sanic_chtblreceivedemailsid'],
        'sanic_ReceivedEmail@odata.bind':
            f"/sanic_chtblreceivedemailses({m['sanic_chtblreceivedemailsid']})",
    }
    if m.get('_sanic_client_value'):
        body['sanic_Client@odata.bind'] = f"/sanic_chtblclients({m['_sanic_client_value']})"
    return create('sanic_chtbldeclarations', body)


decl_ids = para(mk_decl, mails, label='declaraciones')
if not decl_ids:
    sys.exit('Sin declaraciones — abortar.')

# ---------------------------------------------------------------
print('== 2/4 Bolsas declaradas desde las tulas recontadas (60%, 15% con dif)')
bags = get_all("/sanic_chtblbagses?$select=sanic_chtblbagsid,sanic_bagid,sanic_totalnio,"
               "sanic_totalusd,sanic_totaleur&$filter=startswith(sanic_bagid,'T-STRESS') "
               "and sanic_bagstatus eq 159460003 and _sanic_bagdeclarationid_value eq null")
random.shuffle(bags)
bags = bags[:int(len(bags) * 0.6)]
print(f'  tulas a vincular: {len(bags)}')
decl_rr = threading.Lock()
decl_idx = {'i': 0}


def next_decl():
    with decl_rr:
        decl_idx['i'] = (decl_idx['i'] + 1) % len(decl_ids)
        return decl_ids[decl_idx['i']]


dif_bags = []


def vincular(bag):
    bid = bag['sanic_chtblbagsid']
    deps = get_all(f"/sanic_chtbldepositsrecountings?$select=sanic_chtbldepositsrecountingid,"
                   f"sanic_cashamount,sanic_baclocalcheckamount,sanic_otherlocacheckamount,"
                   f"sanic_total,sanic_currency&$filter=_sanic_bagid_value eq {bid}")
    if not deps:
        return None
    did = next_decl()
    con_dif = random.random() < 0.15
    delta = round(random.choice([-1, 1]) * random.uniform(100, 3000), 2) if con_dif else 0.0
    tot = {'NIO': 0.0, 'USD': 0.0, 'EUR': 0.0}
    for k, d in enumerate(deps):
        mon = CUR_NAME.get(d['sanic_currency'], 'NIO')
        dec_cash = round((d['sanic_cashamount'] or 0) - (delta if k == 0 else 0), 2)
        dec_total = round(dec_cash + (d['sanic_baclocalcheckamount'] or 0)
                          + (d['sanic_otherlocacheckamount'] or 0), 2)
        tot[mon] = round(tot[mon] + dec_total, 2)
    bolsa_id = create('sanic_chtbldeclaredbagses', {
        'sanic_name': f"STRESSFIX-{bag['sanic_bagid'][9:]}",
        'sanic_bagnumber': f"STRESSFIX-{bag['sanic_bagid'][9:]}",
        'sanic_DeclarationID@odata.bind': f'/sanic_chtbldeclarations({did})',
        'sanic_totalnio': tot['NIO'], 'sanic_totalusd': tot['USD'], 'sanic_totaleur': tot['EUR'],
        'sanic_BagID@odata.bind': f'/sanic_chtblbagses({bid})',
        'sanic_isrelatedtobag': True,
    })
    for k, d in enumerate(deps):
        dec_cash = round((d['sanic_cashamount'] or 0) - (delta if k == 0 else 0), 2)
        bac = d['sanic_baclocalcheckamount'] or 0
        otr = d['sanic_otherlocacheckamount'] or 0
        dd_id = create('sanic_chtbldeclareddepositses', {
            'sanic_name': f'Deposito {k + 1}', 'sanic_transactionno': k + 1,
            'sanic_accountnumber': random.randint(100000000, 999999999),
            'sanic_currency': d['sanic_currency'],
            'sanic_cashamount': dec_cash, 'sanic_baclocalcheckamount': bac,
            'sanic_otherlocalcheckamount': otr,
            'sanic_total': round(dec_cash + bac + otr, 2),
            'sanic_depositreference': f"DEP-STRESS-FIX-{bid[:8]}-{k}",
            'sanic_DeclaredBag@odata.bind': f'/sanic_chtbldeclaredbagses({bolsa_id})',
            'sanic_Declaration@odata.bind': f'/sanic_chtbldeclarations({did})',
            'sanic_RelatedRecountedDeposit@odata.bind':
                f"/sanic_chtbldepositsrecountings({d['sanic_chtbldepositsrecountingid']})",
        })
        req('PATCH', f"/sanic_chtbldepositsrecountings({d['sanic_chtbldepositsrecountingid']})",
            {'sanic_RelatedDeclaredDeposit@odata.bind':
                 f'/sanic_chtbldeclareddepositses({dd_id})'})
        if bac > 0:
            checks = get_all(f"/sanic_chtblcheckses?$select=sanic_chtblchecksid,sanic_checkstatus,"
                             f"sanic_checkamount,sanic_checkcurrency&$filter="
                             f"_sanic_relatedrecounterdepositid_value eq "
                             f"{d['sanic_chtbldepositsrecountingid']}")
            for c in checks:
                dc_id = create('sanic_chtbldeclaredcheckses', {
                    'sanic_name': f"Cheque STRESSFIX {c['sanic_chtblchecksid'][:8]}",
                    'sanic_checknumber': f"CHK-STRESSFIX-{c['sanic_chtblchecksid'][:8]}",
                    'sanic_checkamount': c['sanic_checkamount'],
                    'sanic_checkcurrency': c['sanic_checkcurrency'],
                    'sanic_checktype': 159460000,
                    'sanic_DeclaredDepositID@odata.bind':
                        f'/sanic_chtbldeclareddepositses({dd_id})',
                    'sanic_Declaration@odata.bind': f'/sanic_chtbldeclarations({did})',
                })
                if c['sanic_checkstatus'] != 159460004:  # Recibido congela el registro
                    req('PATCH', f"/sanic_chtblcheckses({c['sanic_chtblchecksid']})",
                        {'sanic_RelatedDeclaredCheck@odata.bind':
                             f'/sanic_chtbldeclaredcheckses({dc_id})'})
    # colgar la tula de la bolsa -> B5 recalcula DeclTotal* + HasDifference
    req('PATCH', f'/sanic_chtblbagses({bid})',
        {'sanic_BagDeclarationID@odata.bind': f'/sanic_chtbldeclaredbagses({bolsa_id})'})
    if con_dif:
        dif_bags.append({'dep': deps[0], 'delta': delta})
    return bid


para(vincular, bags, workers=8, label='vinculacion')

# ---------------------------------------------------------------
print(f'== 3/4 Transacciones Faltante/Sobrante ({len(dif_bags)})')


def mk_dif_tx(item):
    d, delta = item['dep'], item['delta']
    # delta>0: declarado < recontado -> SOBRANTE; delta<0 -> FALTANTE
    return create('sanic_chtbltransactionses', {
        'sanic_transactiontype': 159460002 if delta > 0 else 159460001,
        'sanic_cashamount': abs(delta), 'sanic_baclocalcheckamount': 0.0,
        'sanic_otherlocalcheckamount': 0.0, 'sanic_total': abs(delta),
        'sanic_currency': d['sanic_currency'],
        'sanic_transactionprocessingtype': 159460000,
        'sanic_transactionreference': f"STRESS-DIF-{d['sanic_chtbldepositsrecountingid'][:8]}",
        'sanic_RelatedDepositRecounting@odata.bind':
            f"/sanic_chtbldepositsrecountings({d['sanic_chtbldepositsrecountingid']})",
    })


para(mk_dif_tx, dif_bags, label='dif-tx')

# ---------------------------------------------------------------
print('== 4/4 Lotes TB directos (el corte gigante supera los 2 min del Custom API)')
txs = get_all("/sanic_chtbltransactionses?$select=sanic_chtbltransactionsid"
              "&$filter=startswith(sanic_transactionreference,'STRESS-') and "
              "_sanic_transactionbatch_value eq null and sanic_transactionstatus eq null&$top=700")
print(f'  transacciones sin lote disponibles: {len(txs)}')
random.shuffle(txs)
waves = [txs[i * 150:(i + 1) * 150] for i in range(4)]
for w, wave in enumerate(waves):
    if not wave:
        break
    try:
        tb = create('sanic_transactionbatcheses', {
            'sanic_batchstatus': 159460000,
            'sanic_comentarios': f'Lote STRESS directo {w + 1}',
        })
    except Exception as e:
        print(f'  crear TB fallo (guard?): {e} — se omiten los lotes directos')
        break
    asignadas = 0
    for t in wave:
        try:
            req('PATCH', f"/sanic_chtbltransactionses({t['sanic_chtbltransactionsid']})",
                {'sanic_TransactionBatch@odata.bind': f'/sanic_transactionbatcheses({tb})'})
            asignadas += 1
        except Exception:
            pass
    print(f'  TB directo {w + 1}: {asignadas} tx asignadas')
    if w == 0:
        continue  # queda Generado
    try:
        req('PATCH', f'/sanic_transactionbatcheses({tb})', {'sanic_batchstatus': 159460001})
        if w >= 2 and otros:
            req('PATCH', f'/sanic_transactionbatcheses({tb})', {'sanic_batchstatus': 159460003},
                caller=otros[w % len(otros)]['azureactivedirectoryobjectid'])
            print(f'  TB directo {w + 1} -> Aplicado (impersonado)')
        else:
            print(f'  TB directo {w + 1} -> Ingresado')
    except Exception as e:
        print(f'  transicion TB {w + 1}: {e}')

print('REPARACION COMPLETA.')
