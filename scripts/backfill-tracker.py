#!/usr/bin/env python3
"""Backfill del paquete tracker (B1, B3, B4, B5, B6, B7) — idempotente.

Estampa en los registros EXISTENTES lo que los plugins estamparan de aca en
adelante. Correr DESPUES de create-tracker-columns.sh y register-tracker-mirrors.sh.

Uso: python3 scripts/backfill-tracker.py
Requiere pp_secrets.env en la raiz.
"""
import json
import os
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request

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
tok_body = urllib.parse.urlencode({
    'grant_type': 'client_credentials', 'client_id': env['PP_CLIENT_ID'],
    'client_secret': env['PP_CLIENT_SECRET'], 'scope': URL + '/.default',
}).encode()
tok = json.load(urllib.request.urlopen(urllib.request.Request(
    f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token", data=tok_body)))
TOKEN = tok['access_token']
API = URL + '/api/data/v9.2'


def req(method, path, body=None):
    r = urllib.request.Request(API + path, method=method,
        data=json.dumps(body).encode() if body is not None else None)
    r.add_header('Authorization', 'Bearer ' + TOKEN)
    r.add_header('Content-Type', 'application/json')
    try:
        with urllib.request.urlopen(r) as resp:
            raw = resp.read().decode()
            return json.loads(raw) if raw.strip() else {}
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"{method} {path}: {e.code} {e.read().decode()[:400]}") from e


def get_all(path):
    rows, url = [], path
    while url:
        d = req('GET', url)
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


# ---------- B1: Declaration.ReceivedEmail desde sourcerecordid ----------
print('== B1: Declaration -> ReceivedEmail')
decls = get_all("/sanic_chtbldeclarations?$select=sanic_chtbldeclarationid,sanic_sourceentity,"
                "sanic_sourcerecordid,_sanic_receivedemail_value")
n = 0
for d in decls:
    if d.get('_sanic_receivedemail_value'):
        continue
    if d.get('sanic_sourceentity') != 'sanic_chtblreceivedemails' or not d.get('sanic_sourcerecordid'):
        continue
    req('PATCH', f"/sanic_chtbldeclarations({d['sanic_chtbldeclarationid']})", {
        'sanic_ReceivedEmail@odata.bind': f"/sanic_chtblreceivedemailses({d['sanic_sourcerecordid']})"
    })
    n += 1
print(f'  estampadas: {n} de {len(decls)}')

# ---------- B1b: ValidationRuns.ReceivedEmail desde sourcerecordid ----------
# El mismo caso que B1 pero en la tabla de corridas, que se habia quedado afuera.
#
# OJO — NO LLEVA EL GUARD DE sourceentity que si tiene B1. Esa columna se elimino de
# ValidationRuns el 2026-08-31 (tenia el MISMO valor en las 292 filas, o sea que el
# guard era vacuo). El chequeo real lo hace el propio PATCH: si el correo no existe,
# Dataverse lo rechaza.
#
# OJO — LOS FALLOS SON ESPERABLES Y SE REPORTAN, NO ROMPEN. Al correrlo por primera
# vez, las 13 corridas sin lookup resultaron ser HUERFANAS: su correo de origen habia
# sido borrado. Eso es justamente lo que sanic_sourcerecordid permite y el lookup no —
# el texto no tiene integridad referencial, asi que conserva punteros colgados. Un
# huerfano no se puede estampar; se cuenta y se informa.
print('== B1b: ValidationRuns -> ReceivedEmail')
runs = get_all("/sanic_chtblvalidationrunses?$select=sanic_chtblvalidationrunsid,"
               "sanic_sourcerecordid,_sanic_receivedemail_value")
n = 0
huerfanas = 0
for r in runs:
    if r.get('_sanic_receivedemail_value') or not r.get('sanic_sourcerecordid'):
        continue
    try:
        req('PATCH', f"/sanic_chtblvalidationrunses({r['sanic_chtblvalidationrunsid']})", {
            'sanic_ReceivedEmail@odata.bind': f"/sanic_chtblreceivedemailses({r['sanic_sourcerecordid']})"
        })
        n += 1
    except Exception:
        huerfanas += 1
print(f'  estampadas: {n} de {len(runs)}; huerfanas (correo de origen borrado): {huerfanas}')

# ---------- B7: DeclaredDeposits/DeclaredChecks -> Declaration ----------
print('== B7: DeclaredDeposits -> Declaration')
deps = get_all("/sanic_chtbldeclareddepositses?$select=sanic_chtbldeclareddepositsid,"
               "_sanic_declaration_value&$expand=sanic_DeclaredBag($select=_sanic_declarationid_value)")
n = 0
for d in deps:
    if d.get('_sanic_declaration_value'):
        continue
    decl = (d.get('sanic_DeclaredBag') or {}).get('_sanic_declarationid_value')
    if not decl:
        continue
    req('PATCH', f"/sanic_chtbldeclareddepositses({d['sanic_chtbldeclareddepositsid']})", {
        'sanic_Declaration@odata.bind': f"/sanic_chtbldeclarations({decl})"
    })
    n += 1
print(f'  estampados: {n} de {len(deps)}')

print('== B7: DeclaredChecks -> Declaration')
dchecks = get_all("/sanic_chtbldeclaredcheckses?$select=sanic_chtbldeclaredchecksid,"
                  "_sanic_declaration_value"
                  "&$expand=sanic_DeclaredDepositID($select=_sanic_declaration_value)")
n = 0
for c in dchecks:
    if c.get('_sanic_declaration_value'):
        continue
    decl = (c.get('sanic_DeclaredDepositID') or {}).get('_sanic_declaration_value')
    if not decl:
        continue
    req('PATCH', f"/sanic_chtbldeclaredcheckses({c['sanic_chtbldeclaredchecksid']})", {
        'sanic_Declaration@odata.bind': f"/sanic_chtbldeclarations({decl})"
    })
    n += 1
print(f'  estampados: {n} de {len(dchecks)}')

# ---------- B3 + B6: espejos de cheques ----------
print('== B3/B6: cheques fisicos -> tula y MatchedCheck')
checks = get_all("/sanic_chtblcheckses?$select=sanic_chtblchecksid,_sanic_bag_value,"
                 "_sanic_relateddeclaredcheck_value"
                 "&$expand=sanic_RelatedReCounterDepositID($select=_sanic_bagid_value)")
n_bag = n_match = 0
for c in checks:
    bag = (c.get('sanic_RelatedReCounterDepositID') or {}).get('_sanic_bagid_value')
    if bag and not c.get('_sanic_bag_value'):
        req('PATCH', f"/sanic_chtblcheckses({c['sanic_chtblchecksid']})", {
            'sanic_Bag@odata.bind': f"/sanic_chtblbagses({bag})"
        })
        n_bag += 1
    declared = c.get('_sanic_relateddeclaredcheck_value')
    if declared:
        req('PATCH', f"/sanic_chtbldeclaredcheckses({declared})", {
            'sanic_MatchedCheck@odata.bind': f"/sanic_chtblcheckses({c['sanic_chtblchecksid']})"
        })
        n_match += 1
print(f'  tulas estampadas: {n_bag}; MatchedCheck estampados: {n_match} (de {len(checks)} cheques)')

# ---------- B4: totales declarados por lote ----------
print('== B4: totales declarados de lotes de recepcion')
batches = get_all('/sanic_chtblreceptionbatchs?$select=sanic_chtblreceptionbatchid')
recs = get_all("/sanic_chtblreceptionses?$select=sanic_niocash,sanic_niocheck,"
               "sanic_usdcash,sanic_usdcheck,_sanic_receptionbatch_value")
by_batch = {}
for r in recs:
    b = r.get('_sanic_receptionbatch_value')
    if not b:
        continue
    t = by_batch.setdefault(b, [0.0, 0.0, 0.0, 0.0])
    t[0] += r.get('sanic_niocash') or 0
    t[1] += r.get('sanic_niocheck') or 0
    t[2] += r.get('sanic_usdcash') or 0
    t[3] += r.get('sanic_usdcheck') or 0
for b in batches:
    bid = b['sanic_chtblreceptionbatchid']
    nc, nk, uc, uk = by_batch.get(bid, [0.0, 0.0, 0.0, 0.0])
    req('PATCH', f"/sanic_chtblreceptionbatchs({bid})", {
        'sanic_decniocash': nc, 'sanic_decniocheck': nk, 'sanic_decniototal': nc + nk,
        'sanic_decusdcash': uc, 'sanic_decusdcheck': uk, 'sanic_decusdtotal': uc + uk,
    })
print(f'  lotes recalculados: {len(batches)}')

# ---------- B5: conciliacion materializada por tula ----------
print('== B5: conciliacion de tulas')
BAG_RECONTADA = 159460003
bags = get_all("/sanic_chtblbagses?$select=sanic_chtblbagsid,sanic_bagstatus,"
               "sanic_totalnio,sanic_totalusd,sanic_totaleur"
               "&$expand=sanic_BagDeclarationID($select=sanic_totalnio,sanic_totalusd,sanic_totaleur)")
n = 0
for bag in bags:
    decl = bag.get('sanic_BagDeclarationID')
    has_decl = decl is not None
    d_nio = (decl.get('sanic_totalnio') if has_decl else 0) or 0
    d_usd = (decl.get('sanic_totalusd') if has_decl else 0) or 0
    d_eur = (decl.get('sanic_totaleur') if has_decl else 0) or 0
    recontada = bag.get('sanic_bagstatus') == BAG_RECONTADA
    diff = recontada and has_decl and (
        (bag.get('sanic_totalnio') or 0) != d_nio
        or (bag.get('sanic_totalusd') or 0) != d_usd
        or (bag.get('sanic_totaleur') or 0) != d_eur)
    req('PATCH', f"/sanic_chtblbagses({bag['sanic_chtblbagsid']})", {
        'sanic_decltotalnio': d_nio, 'sanic_decltotalusd': d_usd,
        'sanic_decltotaleur': d_eur, 'sanic_hasdifference': bool(diff),
    })
    n += 1
print(f'  tulas recalculadas: {n}')

print('Backfill del tracker completo.')
