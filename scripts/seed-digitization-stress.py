#!/usr/bin/env python3
"""Seeder de ESTRES del dominio de DIGITALIZACION para CH-App-Digitization v2.

Hermano de `seed-recounting-stress.py`: MISMA arquitectura ($batch con UN
changeset por request, marcadores rastreables, fases `--only-*`, throughput
reporting, auth por `pp_secrets.env`, reintentos con timeout de socket cubierto).
Lo que cambia es el dominio y —clave— que este seeder NO necesita apagar ni un
solo step del guard: respeta TransactionGuard, TransactionRouting, CheckGuard,
AccountingGuard y RecountingGuard PURAMENTE POR CONSTRUCCION. El unico borrado que
hace el `--clean` tampoco pide apagar nada (ver mas abajo).

    OBJETIVO DE VOLUMEN (para reventar DELEGACION de la app):
      F1 transacciones : >5.000 CH-Tbl-Transactions PENDIENTES (sin lote, sin
                         status), repartidas por `createdby` entre ~40 cajeros
                         reales (impersonacion). UN cajero "pesado" con >500
                         PENDIENTES de tipo Batch y >500 de tipo Manual (para
                         cruzar el tope 500/2000 del cliente en scrCorte y en
                         scrDigitacion filtradas por cajero).
      F2 lotes         : varios lotes 'Generado' + UN lote GORDO con >500
                         transacciones asignadas (para el grid del lote — D-1).
      F3 cheques       : >2.000 CH-Tbl-Checks llevados a 'Enviado a digitacion'
                         (para scrCheques y su CountRows delegado).

-----------------------------------------------------------------------------
DE DONDE SALE LA MATERIA PRIMA
-----------------------------------------------------------------------------
Las TRANSACCIONES del bulk NO dependen de nada previo: son transacciones del
"circuito de digitacion" (ver seccion del guard), se crean de cero.

Los CHEQUES si salen del universo que dejo `seed-recounting-stress.py`
(marcador `sanic_name` = 'SRC-CK*'): NO se crean cheques nuevos porque nacer un
cheque exige la tula ancestro 'Asignada a cajero' (RecountingGuard, Create NO
exento). En cambio la TRANSICION de estado de un cheque que ya existe toca
UNICAMENTE `sanic_checkstatus` y ESA escritura esta EXENTA del gate de recuento
(RecountingGuard.IsWriteExempt, `touchesOnlyCheckStatus`, depth 1) y la gobierna
solo CheckGuard. Asi que F3 reusa cheques SRC-CK* y los mueve por el handshake.
Si el stress de recounting no corrio, F3 aborta con el conteo en la mano en vez
de inventar cheques que RecountingGuard no dejaria nacer.

-----------------------------------------------------------------------------
EL MARCADOR: 'SDG-' RASTREABLE Y DELEGABLE
-----------------------------------------------------------------------------
  - Transacciones : `sanic_transactionreference` = 'SDG-<correlativo>'. Esa
                    columna es TEXTO escribible (RequiredLevel=recommended) y en
                    una transaccion del circuito de digitacion (sin deposito
                    declarado) esta libre: no le pisa nada de negocio. El
                    `--clean` filtra server-side con startswith(...'SDG-').
  - Lotes         : `sanic_name` = 'SDG-BATCH-<correlativo>' (autonumber pisable,
                    mismo patron probado que 'SRC-' en recounting y 'AR-STRESS-'
                    en el seed de supervisor).
  - Cheques       : NO se les pone marcador propio (son SRC-CK*, de recounting).
                    Se rastrean por su estado 'Enviado a digitacion' dentro del
                    universo SRC-CK*. El `--clean` de cheques es de recounting,
                    no de este seeder (el handshake es de una sola via — ver
                    abajo). 'SDG-' no colisiona con 'SR*'/'ST*'/'SRC-'/'AR-STRESS-'.

-----------------------------------------------------------------------------
EL GUARD Y COMO SE LO RESPETA — LA CONSTRUCCION LO HACE LEGAL
-----------------------------------------------------------------------------
Corre con TODOS los steps ACTIVOS. Los gates que miran este dominio:

  GuardTransactionPlugin — Create de sanic_chtbltransactions (rank 1)
      "Una transaccion con lote preseteado solo entra a un lote Generado y sin
      nacer Aplicada." Las PENDIENTES del bulk nacen SIN lote y SIN status:
      EvaluateTransactionUpdate(previousStatus=null, batchInTarget=false,
      statusInTarget=false) => Allow trivial. Las del lote GORDO nacen con
      `sanic_transactionbatch` preseteado a un lote 'Generado' y SIN status:
      assignsBatch + hadBatch=false + targetBatchStatus=Generado + previous=null
      => Allow. En Create el guard NO evalua `touchesAmounts` (solo en Update),
      asi que sembrar montos en el Create nunca choca contra el congelamiento H-04.

  GuardSupervisorAccountingPlugin — Create de sanic_chtbltransactions (rank 2)
      🧨 EL NUDO DEL DISENO. AccountingGuard corre en CUALQUIER Create donde el
      tipo sea contable (Deposito/Faltante/Sobrante/'Sin declaracion') O donde
      venga `sanic_relateddepositrecounting`. Cuando corre, RECALCULA los montos
      server-side y EXIGE que el asiento diga EXACTAMENTE lo que el recuento dice,
      con moneda igual a la del recontado y UN solo asiento por (recuento, tipo)
      —blindado ademas por la alternate key sanic_KeyAsientoPorRecuento—.

      Su PROPIA salida temprana define el camino legitimo del bulk:
          if (!IsAccountingType(type) && recountingRef == null) -> Allow
      es decir, una transaccion SIN tipo contable y SIN recuento asociado es del
      "circuito de digitacion" y pasa sin tocar Dataverse. EL BULK VA POR AHI:
      no setea `sanic_transactiontype` ni `sanic_relateddepositrecounting`. Ambas
      columnas son RequiredLevel=recommended (verificado en Entity.xml), no
      requeridas: omitirlas es legal y ademas mantiene la alternate key sin
      colisiones (no hay par (recuento, tipo) que repetir).

      POR QUE NO ATO CADA TRANSACCION A UN SRC-DR (como sugeria el brief): hacerlo
      dispararia el recalculo por-componente y ademas
        (a) CAPARIA el volumen al numero de DepositsRecountings disponibles
            (~4.500 SRC-DR) — por debajo del objetivo de >5.000, y con Faltante/
            Sobrante casi todos en cero (bloqueados);
        (b) exigiria ECHAR los montos EXACTOS de cada SRC-DR (cash/bac/otros) y su
            moneda en cada transaccion — un read por fila y cero margen;
        (c) chocaria contra la alternate key en el SEGUNDO asiento del mismo
            (recuento, tipo).
      Para un seeder cuyo objetivo es VOLUMEN CRUDO de delegacion, el circuito de
      digitacion es la eleccion correcta. Para quien igual quiera la trazabilidad
      recontado->asiento, la fase OPT-IN `--asientos N` crea N asientos 'Deposito'
      REALISTAS reusando SRC-DR* (echa montos+moneda, uno por recuento, respetando
      el guard): acotada, honesta y desactivada por defecto.

  TransactionRoutingPlugin — Pre-op(20) Create/Update de sanic_chtbltransactions
      Re-resuelve `sanic_transactionprocessingtype` con la regla del dominio y lo
      estampa: Manual explicito se respeta; con cheques (montos propios o del
      deposito) o SIN cuenta -> Manual; solo efectivo CON cuenta -> Batch. El
      seeder NO pelea con el ruteo: CONSTRUYE los insumos para que el tipo salga
      el buscado y lo verifica leyendo de vuelta.
        Batch  = cash>0, `sanic_accountnumber` presente, cheques en CERO.
        Manual = con `sanic_baclocalcheckamount`/`otherlocalcheckamount` > 0
                 (cheques fuerzan Manual) O sin `sanic_accountnumber`.
      Se setea ademas `sanic_transactionprocessingtype` al valor esperado; si el
      ruteo coincide es no-op, y si no, MANDA el ruteo (es la unica fuente).

  CheckGuard — Update de sanic_chtblchecks (handshake de recepcion)
      Un cheque NO puede NACER en 'Enviado a digitacion' (EvaluateCheckCreate solo
      admite null o 'Recontado'), y tampoco se salta de null a 'Enviado'. El
      handshake es null -> Recontado -> 'Enviado a digitacion'. F3 hace, por Update
      que toca SOLO `sanic_checkstatus`:
          paso A: los cheques en null -> 'Recontado'
          paso B: todos (ya 'Recontado') -> 'Enviado a digitacion'
      'Recibido'/'No Recibido'/'Digitado'/'Rebotado' se saltean (terminal o
      reservado). CheckStatusStampPlugin estampa senttodigitizationon/by server-
      side: el seeder NO toca esas columnas.

  RecountingGuard — Create/Update/Delete de los hijos del recuento
      Create de cheque exige tula 'Asignada a cajero' (por eso NO creamos cheques).
      El Update status-only esta EXENTO (touchesOnlyCheckStatus, depth 1): F3 pasa
      por ahi sin apagar el gate.

  Espejos y totales (BatchTotalsCalculator, TransactionBatchStatusMirror, los
  sellos de CreateTransactionCut/BatchTransitionStamp)
      NUNCA se mandan en el JSON: `sanic_batchstatus` (espejo en la transaccion),
      `sanic_totalnio/usd/eur` (rollup del lote) y `sanic_generadopor/ingresadopor`
      los mantienen los plugins. Se siembran para probarlos, no para simularlos.

-----------------------------------------------------------------------------
IDENTIDADES Y createdby — POR QUE HAY IMPERSONACION
-----------------------------------------------------------------------------
La transaccion PENDIENTE no tiene lookup de cajero (solo el LOTE tiene
`sanic_cajero`). El rail Environment.sanic_CHWorkloadByCashier agrega la carga
por `createdby`. Para que el spread por cajero sea REAL, cada Create del bulk se
manda IMPERSONANDO al cajero con el header `CallerObjectId` =
`azureactivedirectoryobjectid` del systemuser (mismo mecanismo que
seed-tracker-stress.py). Se agrupan las requests por cajero: un $batch por cajero
con SU header. `--no-impersonate` cae a crear todo como el SP (createdby=SP) para
entornos donde el SP no tiene 'Act on Behalf of Another User'; se avisa que eso
ANULA el test del rail por cajero.

  🧨 GOTCHA DE ESQUEMA (verificado en Entity.xml): `sanic_accountnumber` tiene
  IsSecured=1 (field-level security). Si el cajero impersonado NO tiene el Field
  Security Profile de esa columna, la escritura de la cuenta se DESCARTA y el
  ruteo, al no ver cuenta, manda la transaccion a Manual — degradando el conteo
  de Batch. El seeder VERIFICA leyendo de vuelta el reparto Batch/Manual del
  cajero pesado y AVISA si Batch < 500 (senal de que falta el FSP). No lo puede
  resolver desde aca: es permiso de entorno.

-----------------------------------------------------------------------------
EL --clean NO APAGA NADA
-----------------------------------------------------------------------------
`sanic_chtbltransactions` NO tiene step de Delete registrado (verificado): las
transacciones SDG-* se borran directo. Los lotes SI tienen Delete guardado
(EvaluateBatchDelete) que solo permite 'Generado' y SIN transacciones — por eso
el orden del clean es: PRIMERO borrar las transacciones SDG-* (incluidas las
asignadas al lote gordo), DESPUES borrar los lotes SDG-BATCH-* (ya vacios y
Generado). Como el seeder NUNCA avanza un lote mas alla de 'Generado', el guard
lo deja. (Si un humano avanzo un lote a Ingresado/Aplicado, el clean lo reporta y
lo saltea: hay que resolverlo a mano — es correcto que un lote cargado al Core no
se borre solo.) Los CHEQUES NO los toca el clean de este seeder: el handshake es
de una via (de 'Enviado a digitacion' no se vuelve por guard) y los cheques son
SRC-CK*, propiedad del `--clean` de recounting.

-----------------------------------------------------------------------------
DUDAS DE ESQUEMA PENDIENTES (no inventadas: a confirmar contra el entorno)
-----------------------------------------------------------------------------
  - Field Security de `sanic_accountnumber`: ver gotcha de impersonacion. El
    seeder lo detecta por verificacion, no lo asume.
  - `azureactivedirectoryobjectid`: no todos los systemuser lo tienen (usuarios
    de aplicacion, cuentas legacy). Se filtran los que lo tengan; si quedan <2 se
    cae a `--no-impersonate` con aviso.
  - EntitySetName de los lotes = 'sanic_transactionbatcheses' (verificado en
    Entity.xml). El de transacciones = 'sanic_chtbltransactionses'.
  - Navigation properties de los @odata.bind: se usan en PascalCase del logical
    name (`sanic_TransactionBatch`, `sanic_Cajero`, `sanic_RelatedDepositRecounting`),
    MISMA convencion que ya usa y prueba seed-recounting-stress.py
    (`sanic_RecountingSession`, `sanic_Cashier`, `sanic_DeclaredBag`). Si el
    entorno expone otro casing, el bind falla con 400 legible por fila y se
    corrige aca — no se traga en silencio.

-----------------------------------------------------------------------------
Uso:
    python3 scripts/seed-digitization-stress.py --dry-run       # plan, cero escrituras
    python3 scripts/seed-digitization-stress.py --all
    python3 scripts/seed-digitization-stress.py --only-tx       # transacciones pendientes
    python3 scripts/seed-digitization-stress.py --only-lotes    # lotes + lote gordo
    python3 scripts/seed-digitization-stress.py --only-cheques  # SRC-CK* -> Enviado a digitacion
    python3 scripts/seed-digitization-stress.py --asientos 800  # OPT-IN: asientos Deposito reales
    python3 scripts/seed-digitization-stress.py --no-impersonate --only-tx
    python3 scripts/seed-digitization-stress.py --verify
    python3 scripts/seed-digitization-stress.py --clean
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
from datetime import datetime, timezone

random.seed(20260823)

# ============================================================ marcador propio
MARK = 'SDG-'                          # 'Stress DiGitization'
TAG_TX = MARK                          # sanic_transactionreference = 'SDG-<seq>'
TAG_BATCH = MARK + 'BATCH-'            # sanic_name = 'SDG-BATCH-<seq>'

# marcador de la MATERIA PRIMA de cheques (lo dejo seed-recounting-stress.py)
RCP_CHK_PREFIX = 'SRC-CK'
RCP_DEP_PREFIX = 'SRC-DR'              # depositos recontados (para --asientos)

# ================================================================ option sets
# sanic_transactionprocessingtype (local de la transaccion)
PROC_MANUAL, PROC_BATCH = 159460000, 159460001
PROC_LABEL = {PROC_MANUAL: 'Manual', PROC_BATCH: 'Batch'}

# sanic_transactionstatus — unico valor; pendiente = null (NO setear)
TX_APLICADA = 159460000

# sanic_transactiontype (local de la transaccion) — AccountingGuard
TT_DEPOSITO = 159460000
TT_FALTANTE = 159460001
TT_SOBRANTE = 159460002
TT_SIN_DECL = 159460003

# sanic_batchstatus (local del lote) — TransactionGuard
BATCH_GENERADO = 159460000
BATCH_INGRESADO = 159460001
BATCH_APROBADO = 159460002
BATCH_APLICADO = 159460003
BATCH_LABEL = {BATCH_GENERADO: 'Generado', BATCH_INGRESADO: 'Ingresado',
               BATCH_APROBADO: 'Aprobado', BATCH_APLICADO: 'Aplicado'}

# sanic_chchcurrency (GLOBAL)
CUR_NIO, CUR_USD, CUR_EUR = 159460000, 159460001, 159460002
CUR_LABEL = {CUR_NIO: 'NIO', CUR_USD: 'USD', CUR_EUR: 'EUR'}

# sanic_checkstatus (local del cheque) — CheckGuard
CHK_RECONTADO = 159460000
CHK_DIGITADO = 159460001          # reservado
CHK_REBOTADO = 159460002          # reservado
CHK_ENVIADO = 159460003          # 'Enviado a digitacion'
CHK_RECIBIDO = 159460004         # terminal
CHK_NO_RECIBIDO = 159460005
CHK_LABEL = {None: '(sin estado)', CHK_RECONTADO: 'Recontado', CHK_DIGITADO: 'Digitado',
             CHK_REBOTADO: 'Rebotado', CHK_ENVIADO: 'Enviado a digitacion',
             CHK_RECIBIDO: 'Recibido', CHK_NO_RECIBIDO: 'No Recibido'}
# estados desde los que SI se puede llegar a 'Enviado a digitacion'
CHK_TRANSICIONABLES = (None, CHK_RECONTADO)

# ===================================================== entity sets (EntitySetName real)
ES_TRX = 'sanic_chtbltransactionses'          # verificado en Entity.xml
ES_TB = 'sanic_transactionbatcheses'          # verificado en Entity.xml
ES_CHK = 'sanic_chtblcheckses'
ES_DEP = 'sanic_chtbldepositsrecountings'
ES_USER = 'systemusers'

PK = {
    ES_TRX: 'sanic_chtbltransactionsid',
    ES_TB: 'sanic_transactionbatchesid',
    ES_CHK: 'sanic_chtblchecksid',
    ES_DEP: 'sanic_chtbldepositsrecountingid',
    ES_USER: 'systemuserid',
}

# el step que el guard registra SOBRE el borrado del lote (auditado, NUNCA se apaga)
STEP_DEL_LOTE = 'CashHub.GuardTransaction: Delete de sanic_transactionbatches'
# NO existe step de Delete de sanic_chtbltransactions: se auditan estos para dejar
# claro que el borrado de transacciones no pasa por ningun guard.
STEPS_AUDIT = (
    'CashHub.GuardTransaction: Create de sanic_chtbltransactions',
    'CashHub.GuardSupervisorAccounting: Create de sanic_chtbltransactions',
    'CashHub.GuardTransaction: Update de sanic_chtbltransactions',
    'CashHub.GuardTransaction: Create de sanic_transactionbatches',
    'CashHub.GuardTransaction: Update de sanic_transactionbatches',
    STEP_DEL_LOTE,
    'CashHub.GuardTransaction: Create de sanic_chtblchecks',
    'CashHub.GuardTransaction: Update de sanic_chtblchecks',
)

# ===========================================================================
# auth + http — patron identico a seed-recounting-stress.py
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


def req(method, path, body=None, retry=6, prefer=None, caller=None):
    """Request unitario. `caller` (AAD object id) impersona via CallerObjectId."""
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
        if caller:
            r.add_header('CallerObjectId', caller)
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
        # 🧨 El timeout del SOCKET salta CRUDO (TimeoutError es OSError), no envuelto
        # en URLError: sin este except se lleva el proceso en corridas largas.
        except (TimeoutError, OSError, http.client.HTTPException) as e:
            last = f'{method} {path[:140]}: {type(e).__name__} {e}'
            if attempt < retry - 1:
                _reintento(f'req/{type(e).__name__}')
                time.sleep(5)
                continue
            raise RuntimeError(last) from e
    raise RuntimeError(last or 'sin reintentos')


def get_all(path, page=5000, caller=None):
    rows, url = [], path
    while url:
        d = req('GET', url, prefer=f'odata.maxpagesize={page}', caller=caller)
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def count_of(entity_set, filtro):
    """Conteo REAL paginando el id (no @odata.count, que Dataverse tope en 5000)."""
    n = 0
    url = f'/{entity_set}?$select={PK[entity_set]}&$filter={urllib.parse.quote(filtro)}'
    while url:
        d = req('GET', url, prefer='odata.maxpagesize=5000')
        n += len(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return n


# ===========================================================================
# $batch — un changeset POR REQUEST; header CallerObjectId por chunk (single-caller)
# ===========================================================================
def batch_exec(requests_, caller=None, retry=6):
    """requests_ = [(method, path_relativo, body|None)] TODAS del mismo caller.
    Devuelve [(status:int, entity_id:str|None, error:str|None)] en ORDEN."""
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
        if caller:
            # Impersonacion de TODO el changeset como el cajero (createdby).
            r.add_header('CallerObjectId', caller)
        try:
            with urllib.request.urlopen(r, timeout=600) as resp:
                return _parse_batch(resp.read().decode('utf-8'), len(requests_))
        except urllib.error.HTTPError as e:
            cuerpo = e.read().decode()
            # 🧨 Dataverse contesta el SOBRE del $batch con 400 cuando UNA parte falla,
            # pero el cuerpo trae el multipart COMPLETO con el status de cada parte.
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
    """requests_ = [(method, path, body, caller|None)]. Agrupa por caller (un
    $batch NO mezcla impersonaciones: el header es del sobre entero), chunkea
    dentro de cada grupo y ejecuta en paralelo. Devuelve (resultados_map, seg).

    resultados_map: dict {indice_original: (status, id, err)} — el orden global
    se preserva por indice para que el llamador re-ate meta<->resultado."""
    if not requests_:
        print(f'  {label}: nada que hacer')
        return {}, 0.0
    # normaliza a 4-tupla e indexa
    norm = []
    for idx, r in enumerate(requests_):
        if len(r) == 3:
            m, p, b = r
            c = None
        else:
            m, p, b, c = r
        norm.append((idx, m, p, b, c))
    # agrupa por caller preservando orden de aparicion
    grupos = {}
    for idx, m, p, b, c in norm:
        grupos.setdefault(c, []).append((idx, m, p, b))
    # arma chunks single-caller
    tareas = []      # [(caller, [(idx, m, p, b), ...])]
    for c, filas in grupos.items():
        for i in range(0, len(filas), chunk):
            tareas.append((c, filas[i:i + chunk]))

    pr = Progress(len(norm), label)
    resultados = {}
    t0 = time.perf_counter()

    def _run(tarea):
        c, filas = tarea
        reqs = [(m, p, b) for (_i, m, p, b) in filas]
        res = batch_exec(reqs, caller=c)
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
    print(f'  {label}: {len(resultados) - n_err} ok · {n_err} errores · {dt:.1f}s '
          f'({len(resultados) / dt if dt else 0:.1f} op/s)')
    for s in pr.samples:
        print(f'      !! {s}', file=sys.stderr)
    return resultados, dt


# ===========================================================================
# steps (solo AUDITORIA — este seeder no apaga ninguno)
# ===========================================================================
def leer_steps():
    flt = urllib.parse.quote("startswith(name,'CashHub.')")
    rows = get_all('/sdkmessageprocessingsteps?$select=name,statecode,statuscode,'
                   'sdkmessageprocessingstepid&$filter=' + flt)
    return {r['name']: r for r in rows}


def auditar_steps():
    print('\n== AUDITORIA DE STEPS (este seeder NO apaga ninguno) ==')
    todos = leer_steps()
    for n in STEPS_AUDIT:
        s = todos.get(n)
        if not s:
            print(f'    [NO EXISTE] {n}')
            continue
        estado = 'ACTIVO' if s['statecode'] == 0 and s['statuscode'] == 1 else 'INACTIVO'
        print(f'    [{estado:8}] {n}')
    return todos


# ===========================================================================
# identidades
# ===========================================================================
def whoami():
    return req('GET', '/WhoAmI()').get('UserId')


def cajeros(n=40, impersonate=True):
    """Cajeros REALES habilitados (no aplicaciones) CON azureactivedirectoryobjectid
    (imprescindible para impersonar por CallerObjectId). Devuelve (lista, es_sp).

    Sin impersonacion (o si quedan <2 con AAD id) se cae al SP: todo bajo un solo
    createdby, lo que ANULA el test del rail por cajero. Se avisa fuerte."""
    rows = get_all(f'/{ES_USER}?$select=systemuserid,fullname,domainname,'
                   f'azureactivedirectoryobjectid'
                   f'&$filter=isdisabled eq false and applicationid eq null'
                   f'&$orderby=fullname asc&$top={n}', page=n)
    conaad = [r for r in rows if r.get('azureactivedirectoryobjectid')]
    if impersonate and len(conaad) >= 2:
        return conaad, False
    me = whoami()
    if impersonate:
        print('  !! menos de 2 usuarios reales con azureactivedirectoryobjectid: '
              'se cae a createdby=SP (rail por cajero DEGRADADO)', file=sys.stderr)
    else:
        print('  !! --no-impersonate: todo se crea como el SP (rail por cajero ANULADO)',
              file=sys.stderr)
    return [{'systemuserid': me, 'fullname': 'Service Principal',
             'azureactivedirectoryobjectid': None}], True


def caller_de(u, impersonate):
    return u.get('azureactivedirectoryobjectid') if impersonate else None


# ===========================================================================
# generadores
# ===========================================================================
def cuenta():
    return round(random.uniform(100_000_000, 999_999_999), 0)   # decimal, cuenta destino


def monto(lo=500, hi=250_000):
    return round(random.uniform(lo, hi), 2)


def moneda_pesada():
    return random.choices((CUR_NIO, CUR_USD, CUR_EUR), weights=(72, 25, 3))[0]


def tx_body(ref, tipo_proc, cur):
    """Cuerpo de una transaccion PENDIENTE del circuito de digitacion.

    NO setea sanic_transactiontype ni sanic_relateddepositrecounting (circuito de
    digitacion: AccountingGuard sale temprano). NO setea sanic_transactionstatus
    (pendiente = null). NO setea sanic_batchstatus (espejo del plugin).
    Construye los insumos para que TransactionRouting resuelva `tipo_proc`."""
    b = {
        'sanic_transactionreference': ref,
        'sanic_cashamount': monto(),
        'sanic_currency': cur,
        # se declara el tipo ESPERADO; si el ruteo coincide es no-op, si no MANDA el ruteo
        'sanic_transactionprocessingtype': tipo_proc,
    }
    if tipo_proc == PROC_BATCH:
        # Batch := efectivo + cuenta destino + cheques en CERO
        b['sanic_accountnumber'] = cuenta()
        b['sanic_baclocalcheckamount'] = 0
        b['sanic_otherlocalcheckamount'] = 0
    else:
        # Manual := dos sabores, elegidos al azar
        if random.random() < 0.6:
            # (a) con cheques (fuerzan Manual aunque haya cuenta)
            b['sanic_accountnumber'] = cuenta()
            b['sanic_baclocalcheckamount'] = monto(0, 60_000)
            b['sanic_otherlocalcheckamount'] = monto(0, 40_000)
        else:
            # (b) sin cuenta (sin cuenta -> Manual)
            b['sanic_baclocalcheckamount'] = 0
            b['sanic_otherlocalcheckamount'] = 0
    return b


# ===========================================================================
# INVENTARIO / PLAN
# ===========================================================================
def inventario(a):
    print('\n== INVENTARIO PREVIO (leido de DEV) ==')
    inv = {}
    inv['tx_sdg'] = count_of(ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}')")
    inv['batch_sdg'] = count_of(ES_TB, f"startswith(sanic_name,'{TAG_BATCH}')")
    print(f'  transacciones SDG-* ya sembradas          : {inv["tx_sdg"]}')
    print(f'  lotes SDG-BATCH-* ya sembrados            : {inv["batch_sdg"]}')

    ck_total = count_of(ES_CHK, f"startswith(sanic_name,'{RCP_CHK_PREFIX}')")
    ck_enviados = count_of(
        ES_CHK, f"startswith(sanic_name,'{RCP_CHK_PREFIX}') and sanic_checkstatus eq {CHK_ENVIADO}")
    inv['ck_total'] = ck_total
    inv['ck_enviados'] = ck_enviados
    print(f'  cheques SRC-CK* (materia prima)           : {ck_total}')
    print(f'    ... ya en Enviado a digitacion           : {ck_enviados}')

    dep_total = count_of(ES_DEP, f"startswith(sanic_name,'{RCP_DEP_PREFIX}')")
    inv['dep_total'] = dep_total
    print(f'  depositos recontados SRC-DR* (--asientos) : {dep_total}')

    usrs, es_sp = cajeros(a.cajeros, impersonate=not a.no_impersonate)
    inv['cajeros'] = len(usrs)
    inv['solo_sp'] = es_sp
    print(f'  cajeros reales con AAD id                 : {len(usrs)}'
          f'{"  (SOLO SP)" if es_sp else ""}')
    return inv, usrs


def plan(a, inv):
    p = {}
    p['heavy'] = a.heavy_batch                       # por-tipo del cajero pesado
    p['total_tx'] = max(a.total_tx, 2 * a.heavy_batch + 1000)
    p['resto'] = p['total_tx'] - 2 * a.heavy_batch
    p['fat_batch'] = a.fat_batch_size
    p['small_batches'] = a.small_batches
    p['checks'] = a.checks
    p['asientos'] = a.asientos

    print('\n== PLAN (numeros objetivo) ==')
    print('  F1 TRANSACCIONES PENDIENTES (circuito de digitacion, sin lote/sin status)')
    print(f'     cajero PESADO   : {p["heavy"]} Batch + {p["heavy"]} Manual '
          f'({">500 OK" if p["heavy"] > 500 else "NO cruza 500"} por tipo)')
    print(f'     resto repartido : {p["resto"]}  entre los demas cajeros (~50/50 Batch/Manual)')
    print(f'     TOTAL bulk      : {p["total_tx"]}  '
          f'({">5000 OK" if p["total_tx"] > 5000 else "NO cruza 5000"})')
    print('  F2 LOTES (Generado)')
    print(f'     lote GORDO      : 1 con {p["fat_batch"]} transacciones asignadas '
          f'({">500 OK" if p["fat_batch"] > 500 else "NO cruza 500"})')
    print(f'     lotes chicos    : {p["small_batches"]}  (variedad, pocas tx c/u)')
    print('  F3 CHEQUES')
    print(f'     a Enviado a dig.: {p["checks"]}  '
          f'({">2000 OK" if p["checks"] > 2000 else "NO cruza 2000"}) '
          f'· disponibles SRC-CK*: {inv["ck_total"]}')
    if p['asientos']:
        print('  OPT-IN ASIENTOS')
        print(f'     Deposito reales : {p["asientos"]}  (echa montos de SRC-DR*, '
              f'uno por recuento, respeta AccountingGuard)')
    total = p['total_tx'] + p['fat_batch'] + p['small_batches'] * 3 + p['checks'] + p['asientos']
    print(f'  ---- ESCRITURAS TOTALES ESTIMADAS: ~{total} '
          f'(cheques cuentan doble si nacen en null)')
    return p


# ===========================================================================
# F1 — TRANSACCIONES PENDIENTES (bulk, spread por createdby)
# ===========================================================================
def _next_ref_seq():
    """Siguiente correlativo de SDG-<n> mirando lo que ya hay (idempotencia grosera)."""
    rows = get_all(f'/{ES_TRX}?$select=sanic_transactionreference&$filter='
                   + urllib.parse.quote(f"startswith(sanic_transactionreference,'{TAG_TX}')"))
    top = 0
    for r in rows:
        v = (r.get('sanic_transactionreference') or '')[len(TAG_TX):]
        if v.isdigit():
            top = max(top, int(v))
    return top + 1


def seed_transactions(a, inv, usrs):
    print('\n' + '=' * 78)
    print('F1 — TRANSACCIONES PENDIENTES (circuito de digitacion, spread por cajero)')
    print('=' * 78)
    t_ini = time.perf_counter()
    imp = not a.no_impersonate and not inv['solo_sp']
    seq = _next_ref_seq()
    print(f'  correlativo de referencia desde SDG-{seq:08d} · impersonacion: {imp}')

    total_tx = max(a.total_tx, 2 * a.heavy_batch + 1000)
    heavy = usrs[0]
    otros = usrs[1:] if len(usrs) > 1 else usrs

    reqs = []          # (method, path, body, caller)
    meta = []          # (cajero_id, tipo_proc)

    def add_tx(u, tipo_proc, cur):
        nonlocal seq
        ref = f'{TAG_TX}{seq:08d}'
        seq += 1
        reqs.append(('POST', f'/{ES_TRX}', tx_body(ref, tipo_proc, cur), caller_de(u, imp)))
        meta.append((u['systemuserid'], tipo_proc))

    # --- cajero PESADO: >500 Batch + >500 Manual ---
    for _ in range(a.heavy_batch):
        add_tx(heavy, PROC_BATCH, moneda_pesada())
    for _ in range(a.heavy_batch):
        add_tx(heavy, PROC_MANUAL, moneda_pesada())

    # --- resto repartido ~50/50 entre los demas ---
    resto = total_tx - 2 * a.heavy_batch
    for i in range(resto):
        u = otros[i % len(otros)]
        tipo = PROC_BATCH if i % 2 == 0 else PROC_MANUAL
        add_tx(u, tipo, moneda_pesada())

    print(f'  a crear: {len(reqs)} transacciones · cajero pesado={heavy["fullname"]!r}')
    res, dt = run_batches(reqs, 'creando transacciones', a.chunk, a.workers)

    ok = sum(1 for r in res.values() if not r[2])
    print(f'\n  transacciones creadas: {ok}/{len(reqs)} · {dt:.1f}s '
          f'({ok / dt if dt else 0:.1f} op/s)')

    # --- VERIFICACION del ruteo (el field-security de accountnumber puede degradar Batch) ---
    _verificar_ruteo_pesado(heavy)
    print(f'  TOTAL F1 {time.perf_counter() - t_ini:.1f}s')
    return {'creadas': ok, 'intentadas': len(reqs)}


def _verificar_ruteo_pesado(heavy):
    """Lee de vuelta el reparto Batch/Manual del cajero pesado. Si Batch < 500 avisa
    del gotcha de field-security de sanic_accountnumber."""
    hid = heavy['systemuserid']
    base = f"startswith(sanic_transactionreference,'{TAG_TX}') and _createdby_value eq {hid}"
    nb = count_of(ES_TRX, base + f" and sanic_transactionprocessingtype eq {PROC_BATCH}")
    nm = count_of(ES_TRX, base + f" and sanic_transactionprocessingtype eq {PROC_MANUAL}")
    print(f'  [verif] cajero pesado -> Batch={nb} · Manual={nm} '
          f'({"OK" if nb > 500 and nm > 500 else "REVISAR"})')
    if nb <= 500:
        print('  !! Batch <= 500: probable field-security de sanic_accountnumber sin FSP '
              'para el cajero impersonado (la cuenta no se escribio -> ruteo a Manual). '
              'Otorgar el Field Security Profile o usar --no-impersonate.', file=sys.stderr)


# ===========================================================================
# F2 — LOTES (Generado) + lote GORDO
# ===========================================================================
def seed_lotes(a, inv, usrs):
    print('\n' + '=' * 78)
    print('F2 — LOTES Generado (+ lote GORDO con >500 transacciones)')
    print('=' * 78)
    t_ini = time.perf_counter()
    imp = not a.no_impersonate and not inv['solo_sp']

    # correlativo de lotes
    rows = get_all(f'/{ES_TB}?$select=sanic_name&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{TAG_BATCH}')"))
    top = 0
    for r in rows:
        v = (r.get('sanic_name') or '')[len(TAG_BATCH):]
        if v.isdigit():
            top = max(top, int(v))
    bseq = top + 1

    # -------- 1) crear los lotes (Generado explicito + cajero) --------------
    print('\n== 1) LOTES ==')
    reqs, meta = [], []
    total_lotes = a.small_batches + 1                    # +1 el gordo
    for i in range(total_lotes):
        u = usrs[i % len(usrs)]
        nombre = f'{TAG_BATCH}{bseq:06d}'
        bseq += 1
        reqs.append(('POST', f'/{ES_TB}', {
            'sanic_name': nombre,
            'sanic_batchstatus': BATCH_GENERADO,       # EvaluateBatchCreate exige Generado explicito
            'sanic_Cajero@odata.bind': f'/{ES_USER}({u["systemuserid"]})',
            'sanic_comentarios': f'{MARK}lote de estres de digitacion',
            # sanic_totalnio/usd/eur -> rollup del plugin. sanic_generadopor -> stamp. NO setear.
        }, None))
        meta.append((nombre, u, i == 0))                # el PRIMERO es el gordo
    res, _ = run_batches(reqs, 'creando lotes', a.chunk, min(a.workers, 3))
    lotes = []
    for (nombre, u, gordo), idx in zip(meta, range(len(meta))):
        st, bid, err = res.get(idx, (0, None, 'sin resultado'))
        if err or not bid:
            print(f'      !! lote {nombre} rechazado: {err}', file=sys.stderr)
            continue
        lotes.append({'id': bid, 'name': nombre, 'user': u, 'gordo': gordo})
    print(f'  lotes creados: {len(lotes)}/{len(meta)}')
    if not lotes:
        raise RuntimeError('no se creo ningun lote: revisar EvaluateBatchCreate / permisos')

    gordo = next((l for l in lotes if l['gordo']), lotes[0])

    # -------- 2) llenar el lote GORDO con >500 transacciones ----------------
    # Se crean DIRECTO dentro del lote (sanic_transactionbatch preseteado): el guard
    # Create valida igual que la asignacion (lote destino Generado, no nace Aplicada).
    # Se impersona al cajero del lote (createdby coherente). Tipo Batch (efectivo+cuenta).
    print('\n== 2) LLENADO DEL LOTE GORDO ==')
    seq = _next_ref_seq()
    reqs = []
    caller = caller_de(gordo['user'], imp)
    for _ in range(a.fat_batch_size):
        ref = f'{TAG_TX}{seq:08d}'
        seq += 1
        body = tx_body(ref, PROC_BATCH, moneda_pesada())
        body['sanic_TransactionBatch@odata.bind'] = f'/{ES_TB}({gordo["id"]})'
        reqs.append(('POST', f'/{ES_TRX}', body, caller))
    res, dt = run_batches(reqs, f'llenando lote gordo {gordo["name"]}', a.chunk, a.workers)
    ok = sum(1 for r in res.values() if not r[2])
    asignadas = count_of(ES_TRX, f"_sanic_transactionbatch_value eq {gordo['id']}")
    print(f'  transacciones en el lote gordo: {asignadas} '
          f'({">500 OK" if asignadas > 500 else "NO cruza 500"})')
    print(f'  TOTAL F2 {time.perf_counter() - t_ini:.1f}s')
    return {'lotes': len(lotes), 'lote_gordo': gordo['name'], 'asignadas': asignadas}


# ===========================================================================
# F3 — CHEQUES a 'Enviado a digitacion' (status-only, exento de RecountingGuard)
# ===========================================================================
def seed_cheques(a, inv):
    print('\n' + '=' * 78)
    print("F3 — CHEQUES SRC-CK* -> 'Enviado a digitacion' (handshake CheckGuard)")
    print('=' * 78)
    t_ini = time.perf_counter()

    # cheques SRC-CK* transicionables (null o Recontado) y aun NO enviados.
    # Se lee el prefijo con estado y se filtra en cliente (robusto ante variaciones
    # de soporte de 'eq null' en optionsets segun version del entorno).
    candidatos = [r for r in get_all(
        f'/{ES_CHK}?$select={PK[ES_CHK]},sanic_name,sanic_checkstatus&$filter='
        + urllib.parse.quote(f"startswith(sanic_name,'{RCP_CHK_PREFIX}')"))
        if r.get('sanic_checkstatus') in CHK_TRANSICIONABLES]
    if not candidatos:
        raise RuntimeError(
            f"no hay cheques {RCP_CHK_PREFIX}* transicionables (null/Recontado): "
            'correr antes scripts/seed-recounting-stress.py')
    objetivo = candidatos[:a.checks]
    en_null = [c for c in objetivo if c.get('sanic_checkstatus') is None]
    en_recontado = [c for c in objetivo if c.get('sanic_checkstatus') == CHK_RECONTADO]
    print(f'  cheques transicionables: {len(candidatos)} · objetivo: {len(objetivo)} '
          f'(null={len(en_null)}, Recontado={len(en_recontado)})')

    # -------- paso A: null -> Recontado (status-only) -----------------------
    if en_null:
        print('\n== A) null -> Recontado ==')
        reqs = _patch_status(en_null, CHK_RECONTADO)
        resA, _ = run_batches(reqs, 'marcando Recontado', a.chunk, a.workers)
        okA = sum(1 for r in resA.values() if not r[2])
        print(f'  a Recontado: {okA}/{len(reqs)}')

    # -------- paso B: Recontado -> Enviado a digitacion ---------------------
    print("\n== B) Recontado -> 'Enviado a digitacion' ==")
    reqs = _patch_status(objetivo, CHK_ENVIADO)
    resB, dt = run_batches(reqs, 'enviando a digitacion', a.chunk, a.workers)
    okB = sum(1 for r in resB.values() if not r[2])

    enviados = count_of(
        ES_CHK, f"startswith(sanic_name,'{RCP_CHK_PREFIX}') and sanic_checkstatus eq {CHK_ENVIADO}")
    print(f'\n  cheques en Enviado a digitacion (SRC-CK*): {enviados} '
          f'({">2000 OK" if enviados > 2000 else "NO cruza 2000"})')
    print(f'  TOTAL F3 {time.perf_counter() - t_ini:.1f}s')
    return {'objetivo': len(objetivo), 'enviados_total': enviados}


def _patch_status(cheques, nuevo):
    """PATCH que toca UNICAMENTE sanic_checkstatus (exento de RecountingGuard).
    Las filas ya traen el PK (se selecciona al leer los candidatos)."""
    pk = PK[ES_CHK]
    return [('PATCH', f'/{ES_CHK}({r[pk]})', {'sanic_checkstatus': nuevo}, None)
            for r in cheques if r.get(pk)]


# ===========================================================================
# OPT-IN — ASIENTOS 'Deposito' REALES contra SRC-DR* (respeta AccountingGuard)
# ===========================================================================
def seed_asientos(a, inv, usrs):
    print('\n' + '=' * 78)
    print('OPT-IN — ASIENTOS Deposito reales (echa montos de SRC-DR*, uno por recuento)')
    print('=' * 78)
    t_ini = time.perf_counter()
    imp = not a.no_impersonate and not inv['solo_sp']

    # SRC-DR* con sus montos y moneda REALES; el asiento Deposito debe ECHARLOS
    # exactos y su moneda igual (AccountingGuard.EvaluateAccountingCreate/TipoDeposito).
    deps = get_all(f'/{ES_DEP}?$select={PK[ES_DEP]},sanic_name,sanic_currency,'
                   f'sanic_cashamount,sanic_baclocalcheckamount,sanic_otherlocacheckamount'
                   f'&$filter=' + urllib.parse.quote(f"startswith(sanic_name,'{RCP_DEP_PREFIX}')"))
    if not deps:
        raise RuntimeError(f"no hay depositos {RCP_DEP_PREFIX}*: correr antes el stress de recounting")

    # excluir los que YA tienen un asiento Deposito (idempotencia + alternate key)
    con_asiento = set()
    for r in get_all(f'/{ES_TRX}?$select=_sanic_relateddepositrecounting_value&$filter='
                     + urllib.parse.quote(f"sanic_transactiontype eq {TT_DEPOSITO}")):
        v = r.get('_sanic_relateddepositrecounting_value')
        if v:
            con_asiento.add(v.lower())
    libres = [d for d in deps if d[PK[ES_DEP]].lower() not in con_asiento]
    objetivo = libres[:a.asientos]
    print(f'  SRC-DR* totales: {len(deps)} · sin asiento Deposito: {len(libres)} · '
          f'a crear: {len(objetivo)}')

    seq = _next_ref_seq()
    reqs = []
    for i, d in enumerate(objetivo):
        u = usrs[i % len(usrs)]
        cur = d.get('sanic_currency')
        if cur is None:
            continue                                   # sin moneda el guard bloquea
        ref = f'{TAG_TX}{seq:08d}'
        seq += 1
        body = {
            'sanic_transactionreference': ref,
            'sanic_transactiontype': TT_DEPOSITO,      # tipo contable -> AccountingGuard recalcula
            'sanic_RelatedDepositRecounting@odata.bind': f'/{ES_DEP}({d[PK[ES_DEP]]})',
            'sanic_currency': cur,
            # montos ECHADOS EXACTOS del recontado (el guard exige igualdad por componente)
            'sanic_cashamount': d.get('sanic_cashamount') or 0,
            'sanic_baclocalcheckamount': d.get('sanic_baclocalcheckamount') or 0,
            'sanic_otherlocalcheckamount': d.get('sanic_otherlocacheckamount') or 0,
        }
        reqs.append(('POST', f'/{ES_TRX}', body, caller_de(u, imp)))
    res, dt = run_batches(reqs, 'creando asientos Deposito', a.chunk, a.workers)
    ok = sum(1 for r in res.values() if not r[2])
    errs = [r[2] for r in res.values() if r[2]]
    print(f'  asientos creados: {ok}/{len(reqs)}')
    for e in errs[:4]:
        print(f'      !! {e}', file=sys.stderr)
    print(f'  TOTAL asientos {time.perf_counter() - t_ini:.1f}s')
    return {'creados': ok, 'intentados': len(reqs)}


# ===========================================================================
# VERIFY
# ===========================================================================
def verify(a):
    print('\n== VERIFICACION ==')
    tx = count_of(ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}')")
    aplicadas = count_of(
        ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}') and "
                f"sanic_transactionstatus eq {TX_APLICADA}")
    pend = tx - aplicadas          # pendiente = sin status (el seeder nunca setea status)
    batch = count_of(
        ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}') and "
                f"sanic_transactionprocessingtype eq {PROC_BATCH}")
    manual = count_of(
        ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}') and "
                f"sanic_transactionprocessingtype eq {PROC_MANUAL}")
    lotes = count_of(ES_TB, f"startswith(sanic_name,'{TAG_BATCH}')")
    ck_env = count_of(
        ES_CHK, f"startswith(sanic_name,'{RCP_CHK_PREFIX}') and sanic_checkstatus eq {CHK_ENVIADO}")
    asi = count_of(
        ES_TRX, f"startswith(sanic_transactionreference,'{TAG_TX}') and "
                f"sanic_transactiontype eq {TT_DEPOSITO}")
    print(f'  transacciones SDG-*                 : {tx}')
    print(f'    ... pendientes (sin status)        : {pend}')
    print(f'    ... Batch / Manual                 : {batch} / {manual}')
    print(f'    ... asientos Deposito (--asientos)  : {asi}')
    print(f'  lotes SDG-BATCH-*                    : {lotes}')
    print(f'  cheques SRC-CK* en Enviado a dig.    : {ck_env}')
    # reparto por createdby (top 5)
    print('\n  reparto por createdby (muestra):')
    rows = get_all(f'/{ES_TRX}?$select=_createdby_value&$filter='
                   + urllib.parse.quote(f"startswith(sanic_transactionreference,'{TAG_TX}')"))
    por = {}
    for r in rows:
        por[r.get('_createdby_value')] = por.get(r.get('_createdby_value'), 0) + 1
    for uid, n in sorted(por.items(), key=lambda kv: -kv[1])[:5]:
        print(f'    {uid}: {n}')
    print(f'  cajeros distintos con transacciones  : {len(por)}')


# ===========================================================================
# CLEAN — sin apagar NADA (transacciones no tienen Delete step; lotes solo Generado+vacios)
# ===========================================================================
def clean(a):
    print('\n== CLEAN (SDG-*) — este seeder NO apaga ningun step ==')
    auditar_steps()

    # 1) transacciones SDG-* (incluidas las asignadas a lotes). Sin Delete step: pasan.
    tx = get_all(f'/{ES_TRX}?$select={PK[ES_TRX]}&$filter='
                 + urllib.parse.quote(f"startswith(sanic_transactionreference,'{TAG_TX}')"))
    print(f'\n  transacciones SDG-* a borrar: {len(tx)}')
    reqs = [('DELETE', f'/{ES_TRX}({r[PK[ES_TRX]]})', None, None) for r in tx]
    res, _ = run_batches(reqs, 'borrando transacciones', a.chunk, a.workers)
    okt = sum(1 for r in res.values() if not r[2])
    print(f'  transacciones borradas: {okt}/{len(reqs)}')

    # 2) lotes SDG-BATCH-*: el guard solo deja borrar Generado + sin transacciones.
    #    Ya vaciados por el paso 1. Los avanzados se saltean con aviso.
    lotes = get_all(f'/{ES_TB}?$select={PK[ES_TB]},sanic_name,sanic_batchstatus&$filter='
                    + urllib.parse.quote(f"startswith(sanic_name,'{TAG_BATCH}')"))
    borrables = [l for l in lotes if l.get('sanic_batchstatus') == BATCH_GENERADO]
    avanzados = [l for l in lotes if l.get('sanic_batchstatus') != BATCH_GENERADO]
    for l in avanzados:
        print(f"  !! lote {l['sanic_name']} en {BATCH_LABEL.get(l.get('sanic_batchstatus'))}: "
              f"no se borra (el guard solo permite Generado) — resolver a mano", file=sys.stderr)
    print(f'\n  lotes SDG-BATCH-* a borrar: {len(borrables)} (avanzados salteados: {len(avanzados)})')
    reqs = [('DELETE', f'/{ES_TB}({l[PK[ES_TB]]})', None, None) for l in borrables]
    res, _ = run_batches(reqs, 'borrando lotes', a.chunk, min(a.workers, 3))
    okl = sum(1 for r in res.values() if not r[2])
    print(f'  lotes borrados: {okl}/{len(reqs)}')

    print('\n  NOTA: los cheques SRC-CK* NO se tocan (handshake de una via; son de '
          'recounting). El --clean de scripts/seed-recounting-stress.py los borra.')


# ===========================================================================
# main
# ===========================================================================
def build_args():
    ap = argparse.ArgumentParser(description='Seeder de estres de CH-App-Digitization v2.')
    ap.add_argument('--all', action='store_true', help='F1 + F2 + F3')
    ap.add_argument('--only-tx', action='store_true', help='solo transacciones pendientes')
    ap.add_argument('--only-lotes', action='store_true', help='solo lotes + lote gordo')
    ap.add_argument('--only-cheques', action='store_true', help='solo SRC-CK* -> Enviado a dig.')
    ap.add_argument('--asientos', type=int, default=0,
                    help='OPT-IN: N asientos Deposito reales contra SRC-DR* (0 = off)')
    ap.add_argument('--verify', action='store_true')
    ap.add_argument('--clean', action='store_true')
    ap.add_argument('--dry-run', action='store_true', help='plan + inventario, cero escrituras')
    ap.add_argument('--dry-writes', action='store_true',
                    help='lecturas reales, escrituras simuladas (valida deltas sin tocar DEV)')
    ap.add_argument('--no-impersonate', action='store_true',
                    help='crea todo como el SP (ANULA el rail por cajero)')
    # volumenes
    ap.add_argument('--cajeros', type=int, default=40)
    ap.add_argument('--total-tx', type=int, default=5200)
    ap.add_argument('--heavy-batch', type=int, default=600,
                    help='transacciones POR TIPO del cajero pesado (>500 para cruzar el tope)')
    ap.add_argument('--fat-batch-size', type=int, default=600,
                    help='transacciones asignadas al lote gordo (>500)')
    ap.add_argument('--small-batches', type=int, default=5)
    ap.add_argument('--checks', type=int, default=2200)
    # red
    ap.add_argument('--chunk', type=int, default=100)
    ap.add_argument('--workers', type=int, default=6)
    return ap.parse_args()


def main():
    a = build_args()
    if a.dry_writes:
        NOWRITE['on'] = True
        print('== DRY-WRITES: lecturas reales, escrituras SIMULADAS ==')

    inv, usrs = inventario(a)
    plan(a, inv)

    if a.dry_run:
        print('\n== DRY-RUN: solo plan e inventario, cero escrituras ==')
        auditar_steps()
        mostrar_reintentos()
        return

    if a.clean:
        clean(a)
        mostrar_reintentos()
        return
    if a.verify:
        verify(a)
        return

    corrio = False
    auditar_steps()
    if a.all or a.only_tx:
        seed_transactions(a, inv, usrs); corrio = True
    if a.all or a.only_lotes:
        seed_lotes(a, inv, usrs); corrio = True
    if a.all or a.only_cheques:
        seed_cheques(a, inv); corrio = True
    if a.asientos > 0:
        seed_asientos(a, inv, usrs); corrio = True

    if not corrio:
        print('\nNada que hacer: pasa --all, --only-tx, --only-lotes, --only-cheques, '
              '--asientos N, --verify o --clean.')
    else:
        print('\n== RESUMEN FINAL ==')
        verify(a)
    mostrar_reintentos()
    if NOWRITE['on']:
        print(f'\n  (dry-writes) escrituras simuladas: {NOWRITE["n"]}')


if __name__ == '__main__':
    main()
