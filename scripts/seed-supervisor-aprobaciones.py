#!/usr/bin/env python3
"""Seeder de ESTRES para scrAprobaciones de CH-App-Supervisor v2.

Siembra el universo que necesita la pantalla de Aprobaciones del rediseno
(docs/supervisor-rebuild/03-pantalla-aprobaciones.md) para probarse de verdad:

  A) ~400 solicitudes PENDIENTES repartidas entre los 10 RequestType del
     contrato §0, con RequestResources COHERENTE (claves exactas por tipo,
     BagGUID vs GUID vs BagID-numero, BagPreviousState por LABEL) apuntando a
     registros STRESS REALES, y RequestDetails calcado del productor
     (Reception 'AdministracionDeLotes' / Recounting 'Screen1').
  B) ~800 solicitudes RESUELTAS (50/50 Aprobado/Rechazado) con
     ResolutionDatetime repartido en los ultimos 60 dias y StatusReason
     variado, para desbordar el tope de delegacion del Historico.
  C) Variedad para las otras pantallas: tulas a 'Pendiente de revision por
     supervisor - recuento' (segmento vacio hoy) y cheques 'Enviado a
     digitacion' -> 'No Recibido' (KPI + reenvio de scrCheques).

MARCA: todo nace con sanic_name = 'AR-STRESS-####' y un comentario
'<!-- STRESS-APR -->' en el detalle. Con eso se limpia (--clean) y se hace
idempotente (una re-corrida solo crea los nombres que falten).

-----------------------------------------------------------------------------
EL GUARD Y POR QUE SE APAGA (y se prende SIEMPRE)
-----------------------------------------------------------------------------
GuardReceptionPlugin (Pre-validation) bloquea con
"Una solicitud de aprobacion no puede crearse ya resuelta" y, en el Update,
exige membresia del grupo AAD aprobador — el service principal NO es miembro.
Verificado contra el entorno antes de escribir este script.

Por eso el bloque de siembra desactiva TEMPORALMENTE **solo estos dos** steps:
    CashHub.GuardReception: Create de sanic_chtblapprovalrequest
    CashHub.GuardReception: Update de sanic_chtblapprovalrequest
y los reactiva en un `finally` + verificacion final. Los steps de
Bags / Receptions / ReceptionBatch NO se tocan: los cambios de estado de tulas
y cheques (parte C) se hacen con el guard ACTIVO y respetan la matriz de
transiciones real (ReceptionGuard/CheckGuard):
    tula  159460000 esclusa  -> 159460005 pendiente-supervisor-esclusa   LEGAL
    tula  159460002 cajero   -> 159460004 pendiente-supervisor-recuento  LEGAL
    tula  159460003 Recontada es TERMINAL -> NO se patchea ninguna
    cheque 159460003 'Enviado a digitacion' -> 159460005 'No Recibido'   LEGAL
El espejo sanic_bagnumber lo estampa OTRO step
(CashHub.ApprovalRequestBagNumber: Create), que queda ACTIVO y sigue corriendo.

CREADOR: las solicitudes nacen del application user (createdby = SP). Es
deliberado y aceptado: sirve para volumen, delegacion e Historico. El control
de CUATRO OJOS se prueba con las solicitudes que cree un humano desde las apps
(el guard bloquea que el creador se apruebe a si mismo).

-----------------------------------------------------------------------------
Uso:
    python3 scripts/seed-supervisor-aprobaciones.py            # sembrar
    python3 scripts/seed-supervisor-aprobaciones.py --clean    # borrar/revertir
    python3 scripts/seed-supervisor-aprobaciones.py --dry-run  # solo el plan
    python3 scripts/seed-supervisor-aprobaciones.py --restore-steps
                                       # reactivar los 2 steps y salir (rescate)
Requiere pp_secrets.env en la raiz del repo. NO correr en produccion.
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
from datetime import datetime, timedelta, timezone

random.seed(20260815)

TAG = 'STRESS-APR'
NAME_PREFIX = 'AR-STRESS-'
DETAIL_MARK = '<!-- ' + TAG + ' -->'

# ---------- auth (mismo patron que seed-tracker-stress.py) ----------
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


def req(method, path, body=None, retry=4, prefer=None):
    last = None
    for attempt in range(retry):
        r = urllib.request.Request(API + path.replace(' ', '%20'), method=method,
                                   data=json.dumps(body).encode() if body is not None else None)
        r.add_header('Authorization', 'Bearer ' + token())
        r.add_header('Content-Type', 'application/json')
        r.add_header('Accept', 'application/json')
        if prefer:
            r.add_header('Prefer', prefer)
        try:
            with urllib.request.urlopen(r, timeout=180) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw.strip() else {}
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:400]
            last = f'{method} {path[:120]}: {e.code} {msg}'
            if e.code == 401 and attempt < retry - 1:
                with _tok_lock:
                    _token['v'] = None
                continue
            if e.code in (429, 503) and attempt < retry - 1:
                import time
                time.sleep(int(e.headers.get('Retry-After', '5')))
                continue
            raise RuntimeError(last) from e
        except urllib.error.URLError as e:
            last = f'{method} {path[:120]}: {e}'
            if attempt < retry - 1:
                import time
                time.sleep(3)
                continue
            raise RuntimeError(last) from e
    raise RuntimeError(last or 'sin reintentos')


def get_all(path):
    rows, url = [], path
    while url:
        d = req('GET', url)
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def para(fn, items, workers=12, label=''):
    out, errs, samples = [], 0, []
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(fn, it): it for it in items}
        for i, fut in enumerate(as_completed(futs), 1):
            try:
                out.append(fut.result())
            except Exception as e:                                    # noqa: BLE001
                errs += 1
                if len(samples) < 5:
                    samples.append(str(e)[:300])
            if i % 200 == 0:
                print(f'    {label}: {i}/{len(items)} (errores {errs})', flush=True)
    if errs:
        print(f'    {label}: {errs} errores de {len(items)}')
        for s in samples:
            print(f'      !! {s}', file=sys.stderr)
    return out, errs


# ---------- constantes de dominio (verificadas contra el entorno) ----------
APR, RECH = 159460000, 159460001                     # sanic_approvalstatus
BAG_ESCLUSA = 159460000
BAG_BOVEDA = 159460001
BAG_CAJERO = 159460002
BAG_RECONTADA = 159460003
BAG_PEND_RECUENTO = 159460004
BAG_PEND_ESCLUSA = 159460005
BAG_LABEL = {
    BAG_ESCLUSA: 'Recibida en esclusa',
    BAG_BOVEDA: 'Almacenada en boveda',
    BAG_CAJERO: 'Asignada a cajero',
    BAG_RECONTADA: 'Recontada',
    BAG_PEND_RECUENTO: 'Pendiente de revision por supervisor - recuento',
    BAG_PEND_ESCLUSA: 'Pendiente de revision por supervisor - esclusa',
}
CHK_ENVIADO = 159460003
CHK_NO_RECIBIDO = 159460005
CUR = {159460000: 'NIO', 159460001: 'USD', 159460002: 'EUR'}

STEP_GUARD_CREATE = 'CashHub.GuardReception: Create de sanic_chtblapprovalrequest'
STEP_GUARD_UPDATE = 'CashHub.GuardReception: Update de sanic_chtblapprovalrequest'
STEP_MIRROR = 'CashHub.ApprovalRequestBagNumber: Create de sanic_chtblapprovalrequest'

# Reparto de los 400 pendientes (mas peso donde pide el encargo).
MIX = [
    ('Tula con diferencia', 105),
    ('Agregar tula', 70),
    ('Crear sesion de recuento', 60),
    ('Tula sin declaracion', 40),
    ('Tula recontada sin declaracion', 35),
    ('Primera tula en planilla', 25),
    ('Eliminar tula', 20),
    ('Remapear tula', 20),
    ('Editar tula', 15),
    ('Modificar numero de planilla', 10),
]

RAZONES_APR = [
    'Diferencia verificada contra el conteo fisico de la mesa.',
    'Autorizado: coincide con el acta firmada por el cajero.',
    'Revisado con el cajero, procede.',
    'Conforme con la evidencia del recuento.',
    'Aprobado en supervision de turno.',
    'Validado contra la declaracion del cliente.',
    None,  # el productor tambien aprueba sin motivo
]
RAZONES_RECH = [
    'El monto recontado no coincide con la declaracion del cliente.',
    'Falta evidencia del recuento en la mesa.',
    'El numero de tula ya existe en el mismo lote de recepcion.',
    'La planilla ya fue cerrada en boveda.',
    'Solicitud duplicada: ya se resolvio en otra gestion.',
    'Datos incompletos: rehacer el recuento.',
    'No corresponde: la tula pertenece a otra planilla.',
    'Fuera del horario de corte de la transportadora.',
    'El cajero debe reconciliar los cheques antes de reintentar.',
]

CSS_DIF = """
                                        body {
                                            font-family: 'Segoe UI', sans-serif;
                                            background-color: #f9f9f9;
                                            margin: 0;
                                            padding: 20px;
                                        }
                                        h1 { text-align: center; color: #333; margin-bottom: 40px; }
                                        h2 { color: #2c3e50; border-bottom: 2px solid #e0e0e0; padding-bottom: 10px; margin-bottom: 20px; }
                                        table { width: 100%; border-collapse: collapse; margin-top: 10px; }
                                        td { border: 1px solid #ddd; padding: 10px; text-align: center; }
                                        th { background-color: #f0f0f0; color: #555; border: 1px solid #ddd; padding: 10px; text-align: center; }
"""


def money(v):
    """Formato "#,###.00" del productor."""
    return f'{(v or 0):,.2f}'


def iso(dt):
    return dt.strftime('%Y-%m-%dT%H:%M:%SZ')


NOW = datetime.now(timezone.utc).replace(tzinfo=None)


# =============================================================================
# Steps del guard
# =============================================================================
def leer_steps():
    filtro = urllib.parse.quote("contains(name,'sanic_chtblapprovalrequest')")
    rows = get_all('/sdkmessageprocessingsteps?$select=name,statecode,statuscode,'
                   'sdkmessageprocessingstepid&$filter=' + filtro)
    return {r['name']: r for r in rows}


def steps_guard():
    todos = leer_steps()
    faltan = [n for n in (STEP_GUARD_CREATE, STEP_GUARD_UPDATE) if n not in todos]
    if faltan:
        raise RuntimeError('no se encontraron los steps del guard: ' + ', '.join(faltan))
    return [todos[STEP_GUARD_CREATE], todos[STEP_GUARD_UPDATE]], todos


def set_step(step_id, activo):
    body = {'statecode': 0, 'statuscode': 1} if activo else {'statecode': 1, 'statuscode': 2}
    req('PATCH', f'/sdkmessageprocessingsteps({step_id})', body)


def mostrar_steps(titulo):
    todos = leer_steps()
    print(f'  {titulo}')
    for n in (STEP_GUARD_CREATE, STEP_GUARD_UPDATE, STEP_MIRROR):
        s = todos.get(n)
        if not s:
            print(f'    ?? {n}: NO EXISTE')
            continue
        estado = 'ACTIVO' if s['statecode'] == 0 and s['statuscode'] == 1 else 'INACTIVO'
        print(f'    [{estado:8}] statecode={s["statecode"]} statuscode={s["statuscode"]}  {n}')
    return todos


def reactivar_guard(pasos):
    """Reactiva y VERIFICA. Reintenta; si no puede, grita fuerte."""
    ok = True
    for s in pasos:
        for intento in range(4):
            try:
                set_step(s['sdkmessageprocessingstepid'], True)
                break
            except Exception as e:                                    # noqa: BLE001
                print(f'  !! fallo reactivando {s["name"]} (intento {intento + 1}): {e}',
                      file=sys.stderr)
        else:
            ok = False
    todos = leer_steps()
    for n in (STEP_GUARD_CREATE, STEP_GUARD_UPDATE):
        s = todos.get(n, {})
        if not (s.get('statecode') == 0 and s.get('statuscode') == 1):
            ok = False
            print(f'  !!!! EL STEP {n} QUEDO INACTIVO — REACTIVALO A MANO !!!!', file=sys.stderr)
    return ok


# =============================================================================
# Inventario del universo STRESS
# =============================================================================
def inventario():
    print('  leyendo el universo STRESS ...', flush=True)
    inv = {}
    inv['bags'] = get_all(
        "/sanic_chtblbagses?$select=sanic_bagid,sanic_bagstatus,sanic_hasdifference,"
        "sanic_receiptidmirror,sanic_totalnio,sanic_totalusd,sanic_totaleur,"
        "sanic_decltotalnio,sanic_decltotalusd,sanic_decltotaleur,"
        "_sanic_receptionid_value,_sanic_receptionbatchid_value,"
        "_sanic_recountingsession_value,_sanic_bagdeclarationid_value"
        "&$filter=startswith(sanic_bagid,'T-STRESS')")
    inv['recs'] = get_all(
        "/sanic_chtblreceptionses?$select=sanic_receiptid,sanic_remitentname,"
        "_sanic_receptionbatch_value&$filter=startswith(sanic_receiptid,'PL-STRESS')")
    inv['batches'] = get_all('/sanic_chtblreceptionbatchs?$select=sanic_name')
    inv['sessions'] = get_all('/sanic_chtblrecountingsessions?$select=sanic_name,'
                              '_sanic_countingdesk_value')
    inv['desks'] = get_all('/sanic_chtblcountingdesks?$select=sanic_name')
    inv['recdep'] = get_all(
        '/sanic_chtbldepositsrecountings?$select=sanic_currency,sanic_cashamount,'
        'sanic_baclocalcheckamount,sanic_otherlocacheckamount,sanic_transactionnumber,'
        '_sanic_bagid_value,_sanic_relateddeclareddeposit_value')
    inv['decdep'] = get_all(
        '/sanic_chtbldeclareddepositses?$select=sanic_currency,sanic_cashamount,'
        'sanic_baclocalcheckamount,sanic_otherlocalcheckamount,'
        '_sanic_relatedrecounteddeposit_value')
    inv['checks'] = get_all(
        "/sanic_chtblcheckses?$select=sanic_checknumber,sanic_checkstatus"
        "&$filter=startswith(sanic_checknumber,'CHK-STRESS')")
    todos_u = get_all('/systemusers?$select=fullname,domainname,applicationid'
                      '&$filter=isdisabled eq false')
    # Solo personas: fuera los application users. Los "# User N" del tenant de DEV
    # se dejan a proposito: son los aprobadores sinteticos del Historico.
    inv['users'] = [u for u in todos_u
                    if not u.get('applicationid')
                    and '@microsoft.com' not in (u.get('domainname') or '')
                    and ((u.get('fullname') or '').startswith('# User ')
                         or not (u.get('fullname') or '').startswith('#'))]
    print(f"    tulas {len(inv['bags'])} · planillas {len(inv['recs'])} · lotes {len(inv['batches'])}"
          f" · sesiones {len(inv['sessions'])} · dep.recontados {len(inv['recdep'])}"
          f" · dep.declarados {len(inv['decdep'])} · cheques {len(inv['checks'])}"
          f" · usuarios {len(inv['users'])}")
    return inv


def indices(inv):
    ix = {}
    ix['rec'] = {r['sanic_chtblreceptionsid']: r for r in inv['recs']}
    ix['batch'] = {b['sanic_chtblreceptionbatchid']: b for b in inv['batches']}
    ix['desk'] = {d['sanic_chtblcountingdeskid']: d for d in inv['desks']}
    ix['ses'] = {s['sanic_chtblrecountingsessionid']: s for s in inv['sessions']}
    ix['dec_por_rec'] = {d['_sanic_relatedrecounteddeposit_value']: d
                         for d in inv['decdep'] if d.get('_sanic_relatedrecounteddeposit_value')}
    por_bag = {}
    for d in inv['recdep']:
        por_bag.setdefault(d.get('_sanic_bagid_value'), []).append(d)
    for v in por_bag.values():
        v.sort(key=lambda x: (x.get('sanic_transactionnumber') or 0,
                              x['sanic_chtbldepositsrecountingid']))
    ix['recdep_por_bag'] = por_bag
    return ix


def lote_de_bag(bag, ix):
    r = ix['rec'].get(bag.get('_sanic_receptionid_value'))
    b = ix['batch'].get(r['_sanic_receptionbatch_value']) if r else None
    return (b or {}).get('sanic_name', 'RB-?')


def planilla_de_bag(bag, ix):
    r = ix['rec'].get(bag.get('_sanic_receptionid_value'))
    return (r or {}).get('sanic_receiptid') or bag.get('sanic_receiptidmirror') or '?'


def cliente_de_bag(bag, ix):
    r = ix['rec'].get(bag.get('_sanic_receptionid_value'))
    return (r or {}).get('sanic_remitentname') or 'Sucursal'


# =============================================================================
# RequestDetails — calcados de los productores reales
# =============================================================================
def det_agregar(bag, rec, ix, nuevo_num):
    return ('<p>Lote de recepcion ' + lote_de_bag(bag, ix) + '<br>Agregar tula ' + nuevo_num
            + ' a la planilla ' + rec['sanic_receiptid'] + '</p>' + DETAIL_MARK)


def det_eliminar(bag, ix):
    return ('<p>Lote de recepcion ' + lote_de_bag(bag, ix) + '<br>Eliminar tula '
            + bag['sanic_bagid'] + ' de planilla ' + planilla_de_bag(bag, ix) + '</p>' + DETAIL_MARK)


def det_remapear(bag, destino, ix):
    return ('<p>Lote de recepcion ' + lote_de_bag(bag, ix) + '<br>Remapear tula '
            + bag['sanic_bagid'] + ' de planilla ' + planilla_de_bag(bag, ix) + ' a '
            + destino['sanic_receiptid'] + '</p>' + DETAIL_MARK)


def det_modificar(rec, nuevo, ix):
    lote = (ix['batch'].get(rec.get('_sanic_receptionbatch_value')) or {}).get('sanic_name', 'RB-?')
    return ('<p>Lote de recepcion ' + lote + '<br>Cambiar numero de planilla '
            + rec['sanic_receiptid'] + ' a ' + nuevo + '</p>' + DETAIL_MARK)


def det_editar(bag, nuevo, ix):
    return ('<p>Lote de recepcion ' + lote_de_bag(bag, ix) + '<br>En la planilla '
            + planilla_de_bag(bag, ix) + ' cambiar numero de tula de ' + bag['sanic_bagid']
            + ' a  ' + nuevo + '</p>' + DETAIL_MARK)


def det_recuento(bag, ix, quien):
    """Formato de 'Primera tula en planilla' / 'Tula sin declaracion'."""
    return ('<p>Solicitada por: ' + quien + '<br>Tula: ' + bag['sanic_bagid']
            + '<br>Planilla: ' + planilla_de_bag(bag, ix)
            + '<br>Cliente: ' + cliente_de_bag(bag, ix) + '</p>' + DETAIL_MARK)


def det_sesion(desk, quien, auto=False):
    cab = 'Auto-aprobada por: ' if auto else 'Solicitada por: '
    return '<p>' + cab + quien + '<br>Mesa seleccionada: ' + desk + '</p>' + DETAIL_MARK


def det_diferencia(bag, ix):
    """varRequestDetails del Recounting Screen1: HTML completo con las tablas."""
    h = ["<html lang='es'>", '<head>', "<meta charset='UTF-8'>",
         '<title>Diferencias Globales de la Tula</title>', '<style>', CSS_DIF, '</style>',
         '</head>', '<body>', '<h1>Tula: ' + bag['sanic_bagid'] + '</h1>']

    dec = (bag.get('sanic_decltotalnio') or 0, bag.get('sanic_decltotalusd') or 0,
           bag.get('sanic_decltotaleur') or 0)
    rec = (bag.get('sanic_totalnio') or 0, bag.get('sanic_totalusd') or 0,
           bag.get('sanic_totaleur') or 0)
    if any(abs(r - d) > 0.004 for r, d in zip(rec, dec)):
        h.append(
            "<div class='section'><h2>Recontado / Declarado</h2><table>"
            '<tr><th></th><th>NIO</th><th>USD</th><th>EUR</th></tr>'
            '<tr><td>Recontado</td><td>' + money(rec[0]) + '</td><td>' + money(rec[1])
            + '</td><td>' + money(rec[2]) + '</td></tr>'
            '<tr><td>Declarado</td><td>' + money(dec[0]) + '</td><td>' + money(dec[1])
            + '</td><td>' + money(dec[2]) + '</td></tr>'
            '<tr><td>Diferencia</td><td>' + money(rec[0] - dec[0]) + '</td><td>'
            + money(rec[1] - dec[1]) + '</td><td>' + money(rec[2] - dec[2])
            + '</td></tr></table></div>')

    filas, huerfanos = [], []
    for r in ix['recdep_por_bag'].get(bag['sanic_chtblbagsid'], []):
        rtot = ((r.get('sanic_cashamount') or 0) + (r.get('sanic_baclocalcheckamount') or 0)
                + (r.get('sanic_otherlocacheckamount') or 0))
        moneda = CUR.get(r.get('sanic_currency'), '?')
        d = ix['dec_por_rec'].get(r['sanic_chtbldepositsrecountingid'])
        if not d:
            huerfanos.append('<tr><td>' + moneda + '</td><td>' + money(rtot) + '</td></tr>')
            continue
        dtot = ((d.get('sanic_cashamount') or 0) + (d.get('sanic_baclocalcheckamount') or 0)
                + (d.get('sanic_otherlocalcheckamount') or 0))
        if abs(rtot - dtot) > 0.004:
            filas.append('<tr><td>' + moneda + '</td><td>' + money(rtot) + '</td><td>'
                         + money(dtot) + '</td><td>' + money(rtot - dtot) + '</td></tr>')
    if filas:
        h.append("<div class='section'><h2>Diferencias por Transacci&oacute;n</h2><table>"
                 '<tr><th>Moneda</th><th>Monto Recontado</th><th>Monto Declarado</th>'
                 '<th>Diferencia</th></tr>' + ''.join(filas) + '</table></div>')
    if huerfanos:
        h.append("<div class='section'><h2>Dep&oacute;sitos Recontados sin Enlazar</h2><table>"
                 '<tr><th>Moneda</th><th>Monto</th></tr>' + ''.join(huerfanos)
                 + '</table></div>')
    h.append(DETAIL_MARK)
    h.append('</body></html>')
    return ''.join(h)


# =============================================================================
# Plan determinista de solicitudes
# =============================================================================
def ciclo(pool):
    i = {'n': 0}

    def nxt():
        v = pool[i['n'] % len(pool)]
        i['n'] += 1
        return v
    return nxt


def construir_plan(inv, ix, n_pend, n_hist):
    bags = sorted(inv['bags'], key=lambda b: b['sanic_chtblbagsid'])
    recs = sorted(inv['recs'], key=lambda r: r['sanic_chtblreceptionsid'])
    sess = sorted(inv['sessions'], key=lambda s: s['sanic_chtblrecountingsessionid'])
    users = sorted(inv['users'], key=lambda u: u['systemuserid'])

    # IDEMPOTENCIA DE LA PARTE C: los pools de tulas patcheables incluyen el estado
    # ORIGEN **y** el DESTINO. Si solo miraran el origen, una segunda corrida veria
    # un pool encogido (las ya patcheadas salieron) y elegiria OTRAS 110 tulas:
    # el patcheo se multiplicaria por corrida y las solicitudes ya sembradas
    # apuntarian a tulas fuera de estado. Con la union + orden estable por GUID, la
    # eleccion es la MISMA en cada corrida y solo se patchea lo que falte.
    p_esclusa = [b for b in bags if b['sanic_bagstatus'] in (BAG_ESCLUSA, BAG_PEND_ESCLUSA)]
    p_cajero = [b for b in bags if b['sanic_bagstatus'] in (BAG_CAJERO, BAG_PEND_RECUENTO)]
    p_dif = [b for b in bags if b.get('sanic_hasdifference')]
    p_recont_sin_decl = [b for b in bags if b['sanic_bagstatus'] == BAG_RECONTADA
                         and not b.get('_sanic_bagdeclarationid_value')]
    p_con_decl = [b for b in bags if b.get('_sanic_bagdeclarationid_value')
                  and b.get('_sanic_recountingsession_value')]
    faltan = [(n, len(p)) for n, p in [('esclusa', p_esclusa), ('cajero', p_cajero),
                                       ('con diferencia', p_dif),
                                       ('recontada sin declaracion', p_recont_sin_decl),
                                       ('con declaracion', p_con_decl)] if not p]
    if faltan:
        raise RuntimeError('pools vacios en el universo STRESS: ' + str(faltan))

    quien = '# PowerPlatform-Dev'
    total = n_pend + n_hist
    escala = total / 400.0
    tipos = []
    for t, n in MIX:
        tipos += [t] * max(1, round(n * escala))
    tipos = tipos[:total] + [MIX[0][0]] * max(0, total - len(tipos))
    random.shuffle(tipos)
    # los pendientes primero, con el mix exacto pedido
    pend_tipos = []
    for t, n in MIX:
        pend_tipos += [t] * n
    pend_tipos = pend_tipos[:n_pend]
    while len(pend_tipos) < n_pend:
        pend_tipos.append(MIX[0][0])
    random.shuffle(pend_tipos)
    hist_tipos = tipos[:n_hist]

    nx = {
        'esclusa_pend': ciclo(p_esclusa),          # se patchean a pendiente-esclusa
        'esclusa_libre': ciclo(list(reversed(p_esclusa))),
        'cajero': ciclo(p_cajero),
        'dif': ciclo(p_dif),
        'rsd': ciclo(p_recont_sin_decl),
        'decl': ciclo(p_con_decl),
        'rec': ciclo(recs),
        'ses': ciclo(sess),
    }

    plan, patch_esclusa, patch_recuento = [], [], []
    idx = 0

    def armar(tipo, pendiente):
        nonlocal idx
        idx += 1
        nombre = f'{NAME_PREFIX}{idx:04d}'
        res, det, bag = {}, '', None

        if tipo == 'Agregar tula':
            bag = nx['esclusa_pend']() if pendiente else nx['esclusa_libre']()
            rec = ix['rec'].get(bag.get('_sanic_receptionid_value')) or nx['rec']()
            nuevo = bag['sanic_bagid'] + '-N' + str(idx % 97)
            res = {'ReceiptGUID': rec['sanic_chtblreceptionsid'], 'BagID': nuevo,
                   'BagGUID': bag['sanic_chtblbagsid'],
                   'ReceptionBatchGUID': rec['_sanic_receptionbatch_value']}
            det = det_agregar(bag, rec, ix, nuevo)
            if pendiente:
                patch_esclusa.append(bag)

        elif tipo == 'Eliminar tula':
            bag = nx['esclusa_pend']() if pendiente else nx['esclusa_libre']()
            res = {'GUID': bag['sanic_chtblbagsid'],
                   'BagPreviousState': BAG_LABEL[BAG_ESCLUSA]}
            det = det_eliminar(bag, ix)
            if pendiente:
                patch_esclusa.append(bag)

        elif tipo == 'Remapear tula':
            bag = nx['esclusa_pend']() if pendiente else nx['esclusa_libre']()
            destino = nx['rec']()
            res = {'GUID': bag['sanic_chtblbagsid'], 'ReceiptID': destino['sanic_receiptid'],
                   'ReceiptGUID': destino['sanic_chtblreceptionsid'],
                   'BagPreviousState': BAG_LABEL[BAG_ESCLUSA]}
            det = det_remapear(bag, destino, ix)
            if pendiente:
                patch_esclusa.append(bag)

        elif tipo == 'Modificar numero de planilla':
            rec = nx['rec']()
            nuevo = rec['sanic_receiptid'] + '-R' + str(idx % 89)
            res = {'ReceiptGUID': rec['sanic_chtblreceptionsid'], 'NewReceiptID': nuevo}
            det = det_modificar(rec, nuevo, ix)

        elif tipo == 'Editar tula':
            bag = nx['esclusa_libre']()
            rec = ix['rec'].get(bag.get('_sanic_receptionid_value')) or nx['rec']()
            nuevo = bag['sanic_bagid'] + '-E' + str(idx % 83)
            # OJO: BagID aca es el NUMERO nuevo, NO un GUID (bug de la app vieja).
            res = {'ReceiptGUID': rec['sanic_chtblreceptionsid'], 'BagID': nuevo,
                   'ReceptionBatchGUID': rec['_sanic_receptionbatch_value'],
                   'BagGUID': bag['sanic_chtblbagsid']}
            det = det_editar(bag, nuevo, ix)

        elif tipo == 'Crear sesion de recuento':
            s = nx['ses']()
            desk = (ix['desk'].get(s.get('_sanic_countingdesk_value')) or {}).get(
                'sanic_name', 'Mesa')
            res = {'RecountingSessionGUID': s['sanic_chtblrecountingsessionid']}
            # §0 regla 5: el productor de la mesa PROPIA nace auto-aprobado, sin
            # SolvedBy ni ResolutionDatetime. Se siembran para probar los tres
            # tramos separados de lblDetSubApr (§7).
            auto = (not pendiente) and idx % 5 == 0
            det = det_sesion(desk, quien, auto=auto)

        elif tipo == 'Primera tula en planilla':
            bag = nx['decl']()
            res = {'BagGUID': bag['sanic_chtblbagsid'],
                   'RecountingSessionGUID': bag['_sanic_recountingsession_value'],
                   'DeclarationBagGUID': bag['_sanic_bagdeclarationid_value']}
            det = det_recuento(bag, ix, quien)

        elif tipo == 'Tula sin declaracion':
            bag = nx['cajero']()
            res = {'BagGUID': bag['sanic_chtblbagsid'],
                   'RecountingSessionGUID': bag.get('_sanic_recountingsession_value')}
            det = det_recuento(bag, ix, quien)
            if pendiente:
                patch_recuento.append(bag)

        elif tipo == 'Tula con diferencia':
            bag = nx['dif']()
            res = {'BagGUID': bag['sanic_chtblbagsid']}
            det = det_diferencia(bag, ix)

        elif tipo == 'Tula recontada sin declaracion':
            bag = nx['rsd']()
            res = {'BagGUID': bag['sanic_chtblbagsid']}
            det = det_diferencia(bag, ix)

        else:
            raise RuntimeError('tipo desconocido: ' + tipo)

        res = {k: v for k, v in res.items() if v}
        auto_aprobada = tipo == 'Crear sesion de recuento' and not pendiente and idx % 5 == 0
        cuerpo = {
            'sanic_name': nombre,
            'sanic_requesttype': tipo,
            'sanic_requestresources': json.dumps(res, ensure_ascii=False, separators=(',', ':')),
            'sanic_requestdetails': det,
        }
        if pendiente:
            # entre hoy y hace 10 dias, para que rindan "hoy"/"ayer" en la bandeja
            cuerpo['overriddencreatedon'] = iso(
                NOW - timedelta(minutes=random.randint(5, 10 * 24 * 60)))
        else:
            resol = NOW - timedelta(minutes=random.randint(60, 60 * 24 * 60))
            aprobado = auto_aprobada or idx % 2 == 0
            cuerpo['sanic_approvalstatus'] = APR if aprobado else RECH
            cuerpo['overriddencreatedon'] = iso(resol - timedelta(
                minutes=random.randint(30, 72 * 60)))
            if not auto_aprobada:
                cuerpo['sanic_resolutiondatetime'] = iso(resol)
                razon = random.choice(RAZONES_APR if aprobado else RAZONES_RECH)
                if razon:
                    cuerpo['sanic_statusreason'] = razon[:200]
                u = users[idx % len(users)]
                cuerpo['sanic_SolvedBy@odata.bind'] = '/systemusers(%s)' % u['systemuserid']
        return cuerpo

    for t in pend_tipos:
        plan.append(armar(t, True))
    for t in hist_tipos:
        plan.append(armar(t, False))

    # de-dup de los patches (una tula, un patch)
    def unicos(lst):
        vistos, out = set(), []
        for b in lst:
            if b['sanic_chtblbagsid'] not in vistos:
                vistos.add(b['sanic_chtblbagsid'])
                out.append(b)
        return out

    return plan, unicos(patch_esclusa), unicos(patch_recuento)


# =============================================================================
# Acciones
# =============================================================================
def existentes():
    filtro = urllib.parse.quote(f"startswith(sanic_name,'{NAME_PREFIX}')")
    rows = get_all('/sanic_chtblapprovalrequests?$select=sanic_name,sanic_approvalstatus,'
                   'sanic_requesttype,sanic_bagnumber&$filter=' + filtro)
    return rows


def limpiar(inv):
    print('\n== LIMPIEZA ==')
    rows = existentes()
    print(f'  solicitudes {NAME_PREFIX}*: {len(rows)}')
    if rows:
        _, errs = para(lambda r: req(
            'DELETE', '/sanic_chtblapprovalrequests(%s)' % r['sanic_chtblapprovalrequestid']),
            rows, label='borrando solicitudes')
        print(f'  borradas {len(rows) - errs} (errores {errs})')

    revertir = [(b, BAG_ESCLUSA) for b in inv['bags'] if b['sanic_bagstatus'] == BAG_PEND_ESCLUSA]
    revertir += [(b, BAG_CAJERO) for b in inv['bags'] if b['sanic_bagstatus'] == BAG_PEND_RECUENTO]
    if revertir:
        _, errs = para(lambda t: req('PATCH', '/sanic_chtblbagses(%s)' % t[0]['sanic_chtblbagsid'],
                                     {'sanic_bagstatus': t[1]}), revertir, label='revirtiendo tulas')
        print(f'  tulas revertidas {len(revertir) - errs} (errores {errs})')
    else:
        print('  no hay tulas en estados pendientes-supervisor que revertir')

    chks = [c for c in inv['checks'] if c['sanic_checkstatus'] == CHK_NO_RECIBIDO]
    if chks:
        _, errs = para(lambda c: req('PATCH', '/sanic_chtblcheckses(%s)'
                                     % c['sanic_chtblchecksid'],
                                     {'sanic_checkstatus': CHK_ENVIADO}), chks,
                       label='revirtiendo cheques')
        print(f'  cheques revertidos a "Enviado a digitacion": {len(chks) - errs} (errores {errs})')
    else:
        print('  no hay cheques STRESS en "No Recibido" que revertir')


def main():
    ap = argparse.ArgumentParser(description='Seeder de estres para scrAprobaciones (STRESS-APR)')
    ap.add_argument('--pend', type=int, default=400, help='solicitudes pendientes (def 400)')
    ap.add_argument('--hist', type=int, default=800, help='solicitudes resueltas (def 800)')
    ap.add_argument('--cheques', type=int, default=30, help='cheques Enviado -> No Recibido')
    ap.add_argument('--clean', action='store_true', help='borra lo sembrado y revierte estados')
    ap.add_argument('--dry-run', action='store_true', help='arma el plan y no escribe nada')
    ap.add_argument('--restore-steps', action='store_true',
                    help='reactiva los 2 steps del guard y sale (rescate)')
    a = ap.parse_args()

    print('=' * 78)
    print('SEEDER scrAprobaciones — marca ' + TAG + ' — ' + URL)
    print('=' * 78)

    if a.restore_steps:
        pasos, _ = steps_guard()
        ok = reactivar_guard(pasos)
        mostrar_steps('estado final de los steps:')
        sys.exit(0 if ok else 1)

    inv = inventario()
    ix = indices(inv)

    if a.clean:
        limpiar(inv)
        mostrar_steps('\nestado de los steps (no se tocaron):')
        return

    plan, patch_esclusa, patch_recuento = construir_plan(inv, ix, a.pend, a.hist)
    ya = {r['sanic_name'] for r in existentes()}
    pendientes_de_crear = [c for c in plan if c['sanic_name'] not in ya]

    print('\n== PLAN ==')
    print(f'  solicitudes planificadas : {len(plan)} '
          f'({a.pend} pendientes + {a.hist} resueltas)')
    print(f'  ya existentes (idempot.) : {len(ya)}')
    print(f'  a crear ahora            : {len(pendientes_de_crear)}')
    # solo lo que todavia NO esta en el estado destino (re-corrida = 0 escrituras)
    falta_esclusa = [b for b in patch_esclusa if b['sanic_bagstatus'] != BAG_PEND_ESCLUSA]
    falta_recuento = [b for b in patch_recuento if b['sanic_bagstatus'] != BAG_PEND_RECUENTO]
    print(f'  tulas -> pendiente-esclusa  : {len(patch_esclusa)} '
          f'(por patchear ahora {len(falta_esclusa)})')
    print(f'  tulas -> pendiente-recuento : {len(patch_recuento)} '
          f'(por patchear ahora {len(falta_recuento)})')
    # mismo criterio para cheques: pool = origen + destino, orden estable
    pool_chk = sorted([c for c in inv['checks']
                       if c['sanic_checkstatus'] in (CHK_ENVIADO, CHK_NO_RECIBIDO)],
                      key=lambda c: c['sanic_chtblchecksid'])[:a.cheques]
    cheques = [c for c in pool_chk if c['sanic_checkstatus'] == CHK_ENVIADO]
    print(f'  cheques Enviado -> No Recibido: {len(pool_chk)} '
          f'(por patchear ahora {len(cheques)})')
    patch_esclusa, patch_recuento = falta_esclusa, falta_recuento
    if a.dry_run:
        print('\n(dry-run) ejemplo de solicitud:')
        print(json.dumps(pendientes_de_crear[0] if pendientes_de_crear else plan[0],
                         indent=1, ensure_ascii=False)[:1800])
        return

    # ---- C) tulas y cheques: CON EL GUARD ACTIVO ----
    print('\n== C) estados de tulas y cheques (guard ACTIVO) ==')
    res_esclusa = res_recuento = res_chk = (0, 0)
    if patch_esclusa:
        _, e = para(lambda b: req('PATCH', '/sanic_chtblbagses(%s)' % b['sanic_chtblbagsid'],
                                  {'sanic_bagstatus': BAG_PEND_ESCLUSA}),
                    patch_esclusa, label='tulas -> pendiente-esclusa')
        res_esclusa = (len(patch_esclusa) - e, e)
        print(f'  tulas a "Pendiente de revision por supervisor - esclusa": '
              f'{res_esclusa[0]} ok, {res_esclusa[1]} rebotadas')
    if patch_recuento:
        _, e = para(lambda b: req('PATCH', '/sanic_chtblbagses(%s)' % b['sanic_chtblbagsid'],
                                  {'sanic_bagstatus': BAG_PEND_RECUENTO}),
                    patch_recuento, label='tulas -> pendiente-recuento')
        res_recuento = (len(patch_recuento) - e, e)
        print(f'  tulas a "Pendiente de revision por supervisor - recuento": '
              f'{res_recuento[0]} ok, {res_recuento[1]} rebotadas')
    if cheques:
        _, e = para(lambda c: req('PATCH', '/sanic_chtblcheckses(%s)' % c['sanic_chtblchecksid'],
                                  {'sanic_checkstatus': CHK_NO_RECIBIDO}),
                    cheques, label='cheques -> No Recibido')
        res_chk = (len(cheques) - e, e)
        print(f'  cheques "Enviado a digitacion" -> "No Recibido": '
              f'{res_chk[0]} ok, {res_chk[1]} rebotados')

    # ---- A + B) solicitudes: guard APAGADO, try/finally ----
    pasos, _ = steps_guard()
    creadas = errs = 0
    print('\n== A+B) solicitudes de aprobacion ==')
    guard_ok = True
    if not pendientes_de_crear:
        # Nada que crear (re-corrida idempotente): el guard NI SE TOCA.
        print('  nada nuevo que crear — el guard no se toca')
        mostrar_steps('steps (intactos):')
        _verificar(a, plan)
        return
    mostrar_steps('steps ANTES:')
    guard_ok = False
    try:
        for s in pasos:
            set_step(s['sdkmessageprocessingstepid'], False)
        print('  guard de ApprovalRequest DESACTIVADO (temporal)')
        mostrar_steps('steps DURANTE:')
        if pendientes_de_crear:
            _, errs = para(lambda c: req('POST', '/sanic_chtblapprovalrequests', c),
                           pendientes_de_crear, workers=12, label='creando solicitudes')
            creadas = len(pendientes_de_crear) - errs
        print(f'  creadas {creadas} (errores {errs})')
    finally:
        guard_ok = reactivar_guard(pasos)
        print('  guard de ApprovalRequest REACTIVADO' if guard_ok
              else '  !! GUARD NO CONFIRMADO — revisar a mano')

    # ---- verificacion ----
    _verificar(a, plan)
    mostrar_steps('\nESTADO FINAL DE LOS STEPS (deben quedar ACTIVOS):')
    if not guard_ok:
        sys.exit(2)


def _verificar(a, plan):
    print('\n== VERIFICACION ==')
    todas = existentes()
    por_tipo, por_estado = {}, {'pendiente': 0, 'Aprobado': 0, 'Rechazado': 0}
    con_espejo = 0
    for r in todas:
        k = r['sanic_requesttype']
        st = r.get('sanic_approvalstatus')
        e = 'pendiente' if st is None else ('Aprobado' if st == APR else 'Rechazado')
        por_tipo.setdefault(k, {'pendiente': 0, 'Aprobado': 0, 'Rechazado': 0})[e] += 1
        por_estado[e] += 1
        if r.get('sanic_bagnumber'):
            con_espejo += 1
    print(f'  total {NAME_PREFIX}*: {len(todas)}')
    print(f'  {"TIPO":34} {"PEND":>6} {"APR":>6} {"RECH":>6}')
    for k in sorted(por_tipo):
        v = por_tipo[k]
        print(f'  {k:34} {v["pendiente"]:>6} {v["Aprobado"]:>6} {v["Rechazado"]:>6}')
    print(f'  {"TOTAL":34} {por_estado["pendiente"]:>6} {por_estado["Aprobado"]:>6} '
          f'{por_estado["Rechazado"]:>6}')
    esperado_espejo = sum(1 for r in todas if r['sanic_requesttype']
                          not in ('Crear sesion de recuento', 'Modificar numero de planilla'))
    print(f'  espejo sanic_bagnumber estampado: {con_espejo}/{esperado_espejo} '
          f'(los tipos sin tula no llevan espejo)')

    b2 = get_all("/sanic_chtblbagses?$select=sanic_bagid,sanic_bagstatus"
                 "&$filter=startswith(sanic_bagid,'T-STRESS')")
    print(f'  tulas STRESS en pendiente-esclusa  : '
          f'{sum(1 for b in b2 if b["sanic_bagstatus"] == BAG_PEND_ESCLUSA)}')
    print(f'  tulas STRESS en pendiente-recuento : '
          f'{sum(1 for b in b2 if b["sanic_bagstatus"] == BAG_PEND_RECUENTO)}')
    c2 = get_all("/sanic_chtblcheckses?$select=sanic_checkstatus"
                 "&$filter=startswith(sanic_checknumber,'CHK-STRESS')")
    print(f'  cheques STRESS en "No Recibido"    : '
          f'{sum(1 for c in c2 if c["sanic_checkstatus"] == CHK_NO_RECIBIDO)}')


if __name__ == '__main__':
    main()
