#!/usr/bin/env python3
"""Backfill de los espejos de CH-App-Supervisor v2 (B-2, B-3, B-6, B-9) — idempotente.

Estampa en los registros EXISTENTES lo que los plugins estamparan de aca en
adelante. Sin esto los espejos solo existen para lo que nazca despues del
registro, y las consultas delegables del supervisor mienten por omision sobre
todo el historico.

  B-2  sanic_chtblbags.sanic_receptionbatchid  <- Bag.ReceptionID.ReceptionBatch
  B-3  sanic_chtblbags.sanic_receiptidmirror   <- Bag.ReceptionID.ReceiptID
  B-6  sanic_chtblapprovalrequest.sanic_bagnumber <- JSON de RequestResources
  B-9  sanic_chtblchecks.sanic_recountingsession
       <- Check.RelatedReCounterDepositID.BagID -> Bag.RecountingSession

Solo PATCHea lo que difiere del valor esperado: correrlo dos veces no genera
auditoria basura ni escrituras al pedo.

ORDEN: correr DESPUES de create-supervisor-mirror-columns.sh y de
register-supervisor-mirrors.sh.

EXCEPCION IMPORTANTE (B-9): es PURA Web API y NO necesita el assembly desplegado.
Con la columna creada + este backfill, scrResumen ya puede contar cheques por
sesion con CountRows DELEGADO sobre todo el historico. Lo que queda pendiente
hasta el deploy del plugin es SOLO el mantenimiento en vivo (cheques nuevos y
tulas re-asignadas de sesion): para eso estan los pasos de CheckMirrorStampPlugin.
Mientras tanto, este script se puede re-correr cuantas veces haga falta.

OJO con el guard: el paso de Update de GuardTransactionPlugin sobre cheques NO
tiene filteringattributes (Recibido congela el registro entero). Si el assembly
desplegado todavia no incluye sanic_recountingsession en el bypass
touchesOnlyMirror, los cheques ya Recibidos rebotan. El script NO se cae por eso:
los cuenta aparte y avisa al final (se completan solos con el deploy).

Uso: python3 scripts/backfill-supervisor-mirrors.py
Requiere pp_secrets.env en la raiz.
"""
import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request

PAGE_SIZE = 500
MAX_LEN = 100

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


def req(method, path, body=None, prefer=None):
    r = urllib.request.Request(API + path, method=method,
        data=json.dumps(body).encode() if body is not None else None)
    r.add_header('Authorization', 'Bearer ' + TOKEN)
    r.add_header('Content-Type', 'application/json')
    if prefer:
        r.add_header('Prefer', prefer)
    try:
        with urllib.request.urlopen(r) as resp:
            raw = resp.read().decode()
            return json.loads(raw) if raw.strip() else {}
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"{method} {path}: {e.code} {e.read().decode()[:400]}") from e


def get_all(path):
    """Recorre TODAS las paginas siguiendo @odata.nextLink, de a PAGE_SIZE filas."""
    rows, url = [], path
    while url:
        d = req('GET', url, prefer=f'odata.maxpagesize={PAGE_SIZE}')
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


_sets = {}


def entity_set(logical):
    """EntitySetName real desde metadata: no se adivina el plural."""
    if logical not in _sets:
        d = req('GET', f"/EntityDefinitions(LogicalName='{logical}')?$select=EntitySetName")
        _sets[logical] = d['EntitySetName']
    return _sets[logical]


def cap(value):
    """Misma normalizacion que el dominio (BagReceptionMirrors.CapReceipt)."""
    if value is None:
        return None
    trimmed = str(value).strip()
    if not trimmed:
        return None
    return trimmed[:MAX_LEN]


BAGS = entity_set('sanic_chtblbags')
BATCHES = entity_set('sanic_chtblreceptionbatch')
APPROVALS = entity_set('sanic_chtblapprovalrequest')
CHECKS = entity_set('sanic_chtblchecks')
SESSIONS = entity_set('sanic_chtblrecountingsession')

# ---------- B-2 + B-3: tula -> lote y recibo de su planilla ----------
print('== B-2/B-3: tulas -> lote y recibo de la planilla')
# _sanic_recountingsession_value viaja en esta misma query: es el origen de B-9 y
# una segunda pasada por todas las tulas seria pura red al pedo.
bags = get_all(
    f"/{BAGS}?$select=sanic_chtblbagsid,sanic_bagid,_sanic_receptionbatchid_value,"
    "sanic_receiptidmirror,_sanic_recountingsession_value"
    "&$expand=sanic_ReceptionID($select=_sanic_receptionbatch_value,sanic_receiptid)")

n_batch = n_receipt = 0
for bag in bags:
    reception = bag.get('sanic_ReceptionID') or {}
    want_batch = reception.get('_sanic_receptionbatch_value')
    want_receipt = cap(reception.get('sanic_receiptid'))

    patch = {}
    if bag.get('_sanic_receptionbatchid_value') != want_batch:
        patch['sanic_ReceptionBatchId@odata.bind'] = (
            f"/{BATCHES}({want_batch})" if want_batch else None)
        n_batch += 1
    if cap(bag.get('sanic_receiptidmirror')) != want_receipt:
        patch['sanic_receiptidmirror'] = want_receipt
        n_receipt += 1

    if patch:
        req('PATCH', f"/{BAGS}({bag['sanic_chtblbagsid']})", patch)

print(f'  tulas revisadas: {len(bags)}; lote estampado: {n_batch}; recibo estampado: {n_receipt}')

# ---------- B-6: solicitud de aprobacion -> numero de tula ----------
print('== B-6: solicitudes -> numero de tula')

# Mapa GUID de tula -> numero, en UNA query: los tipos que solo traen BagGUID/GUID
# se resuelven en memoria (un Retrieve por solicitud seria N+1 sobre el historico).
bag_numbers = {b['sanic_chtblbagsid']: cap(b.get('sanic_bagid')) for b in bags}

GUID_RE = re.compile(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-'
                     r'[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')


def json_value(raw, key):
    """Valor escalar de una clave. Espejo del FlatJson del dominio: nunca lanza."""
    if not raw:
        return None
    try:
        parsed = json.loads(raw)
        if isinstance(parsed, dict):
            for k, v in parsed.items():
                if k.lower() == key.lower():
                    return None if v is None or isinstance(v, (dict, list)) else str(v)
            return None
    except (ValueError, TypeError):
        pass
    # JSON roto: se intenta la clave textual, igual que el lector tolerante del plugin.
    m = re.search(r'"%s"\s*:\s*"([^"]*)"' % re.escape(key), raw, re.IGNORECASE)
    return m.group(1) if m else None


def resolve_number(raw):
    """(numero, motivo). Espejo de ApprovalBagNumber.Resolve."""
    number = json_value(raw, 'BagID')
    bag_guid = None
    if number and GUID_RE.match(number.strip()):
        bag_guid, number = number.strip(), None
    if number:
        return cap(number), 'numero en el JSON'
    if not bag_guid:
        for key in ('BagGUID', 'GUID'):
            candidate = json_value(raw, key)
            if candidate and GUID_RE.match(candidate.strip()):
                bag_guid = candidate.strip()
                break
    if bag_guid:
        return bag_numbers.get(bag_guid.lower()) or bag_numbers.get(bag_guid), 'GUID resuelto'
    return None, 'sin tula en el JSON'


requests_rows = get_all(
    f"/{APPROVALS}?$select=sanic_chtblapprovalrequestid,sanic_requesttype,"
    "sanic_requestresources,sanic_bagnumber")

n_stamped = 0
n_blank = 0
for row in requests_rows:
    number, reason = resolve_number(row.get('sanic_requestresources'))
    if number is None:
        n_blank += 1
        continue
    if cap(row.get('sanic_bagnumber')) == number:
        continue
    req('PATCH', f"/{APPROVALS}({row['sanic_chtblapprovalrequestid']})",
        {'sanic_bagnumber': number})
    n_stamped += 1

print(f'  solicitudes revisadas: {len(requests_rows)}; estampadas: {n_stamped}; '
      f'sin tula en el JSON: {n_blank}')

# ---------- B-9: cheque -> sesion de recuento de su tula ----------
print('== B-9: cheques -> sesion de recuento de la tula')

# Mapa tula -> sesion en memoria, reusando la query de arriba: el join
# cheque -> tula -> sesion NO se puede pedir con $expand anidado, y un Retrieve de
# tula por cheque seria N+1 sobre miles de cheques.
bag_sessions = {
    b['sanic_chtblbagsid'].lower(): (b.get('_sanic_recountingsession_value') or '').lower() or None
    for b in bags
}

# La tula del cheque sale del deposito de recuento (misma cadena que usa el
# plugin); si el cheque ya tiene el espejo B6 estampado, sirve de respaldo.
checks = get_all(
    f"/{CHECKS}?$select=sanic_chtblchecksid,_sanic_bag_value,_sanic_recountingsession_value"
    "&$expand=sanic_RelatedReCounterDepositID($select=_sanic_bagid_value)")

n_session = 0        # espejos escritos
n_unknown_bag = 0    # el cheque apunta a una tula que la query de tulas no trajo
n_frozen = 0         # rebotados por el guard (cheque Recibido, bypass sin desplegar)
for check in checks:
    deposit = check.get('sanic_RelatedReCounterDepositID') or {}
    bag = (deposit.get('_sanic_bagid_value') or check.get('_sanic_bag_value') or '').lower() or None

    if bag is None:
        want = None                      # sin tula no hay sesion: espejo en blanco
    elif bag in bag_sessions:
        want = bag_sessions[bag]
    else:
        # Tula fuera del universo leido: NO se limpia el espejo por las dudas —
        # borrar dato por un mapa incompleto es peor que dejarlo como esta.
        n_unknown_bag += 1
        continue

    have = (check.get('_sanic_recountingsession_value') or '').lower() or None
    if have == want:
        continue

    patch = {'sanic_RecountingSession@odata.bind': f"/{SESSIONS}({want})" if want else None}
    try:
        req('PATCH', f"/{CHECKS}({check['sanic_chtblchecksid']})", patch)
        n_session += 1
    except RuntimeError as err:
        # El guard de cheques congela los Recibidos y su paso NO esta filtrado: si el
        # assembly desplegado aun no tiene sanic_recountingsession en el bypass
        # touchesOnlyMirror, estos rebotan. Es esperable y se resuelve con el deploy.
        if 'congela' not in str(err):
            raise
        n_frozen += 1

print(f'  cheques revisados: {len(checks)}; sesion estampada: {n_session}; '
      f'tula desconocida: {n_unknown_bag}; congelados por el guard: {n_frozen}')
if n_frozen:
    print(f'  AVISO: {n_frozen} cheques Recibidos quedaron sin espejo de sesion — '
          'el guard los congela y el bypass touchesOnlyMirror con '
          'sanic_recountingsession todavia no esta desplegado. Re-correr este '
          'script despues de subir el assembly.')

print('Backfill de los espejos del supervisor completo.')
