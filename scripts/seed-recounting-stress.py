#!/usr/bin/env python3
"""Seeder de ESTRES del dominio de RECONTEO para CH-App-Recounting v2.

Hermano de `seed-reception-stress.py`: MISMA arquitectura ($batch con UN
changeset por request, marcadores rastreables, fases `--only-*`, throughput
reporting, `--clean` con apagado temporal y auditado de los pasos Delete del
guard, auth por `pp_secrets.env`). Lo que cambia es el dominio.

    F1 declaraciones : ~3.000 DeclaredBags · ~12.400 DeclaredDeposits
                       · ~17.000 DeclaredChecks (con 6 bolsas GORDAS de >500
                         cheques declarados cada una)
    F2 sesiones      : 40 mesas · ~600 RecountingSessions historicas
                       (Confirmed, retro-fechadas) · ~800 ApprovalRequests
    F3 reconteo      : ~1.500 tulas abiertas · ~1.500 RecountingSessionDetails
                       · ~4.500 DepositsRecountings · ~15.700 CountedMoneyDetails
                       · ~3.900 Checks (con UNA tula GORDA de 600 cheques)
                       · 1.000 cierres limpios · 200 con diferencia · 300 abiertas
    F4 contadora     : ~700 XMLReadings · ~31.500 XMLReadingDetails

-----------------------------------------------------------------------------
DE DONDE SALE LA MATERIA PRIMA: EL STRESS DE RECEPCION
-----------------------------------------------------------------------------
NO se crean tulas nuevas. Las tulas a declarar y a recontar salen del universo
que ya dejo `seed-reception-stress.py` (marcador `sanic_bagid` = 'ST'+correlativo)
y que esta en 'Almacenada en boveda' (159460001) — el UNICO estado desde el que
Recounting puede abrir una tula (01 §5.1). Si ese seed no corrio, este aborta con
el conteo real en la mano en vez de inventar tulas que Reception no reconoceria.

-----------------------------------------------------------------------------
EL MARCADOR: 'SRC-' EN sanic_name, EN TODAS LAS TABLAS
-----------------------------------------------------------------------------
Todas las tablas nuevas de este dominio tienen `sanic_name` AUTONUMBER
(`RCS-{SEQNUM:4}`, `DR-{SEQNUM:8}`, `CMD-{SEQNUM:12}`, `CK-{SEQNUM:8}`,
`DB-{SEQNUM:7}`, `DD-{SEQNUM:8}`, `AR-{SEQNUM:8}`, `Decla-{SEQNUM:4}`, …).
Un autonumber de Dataverse SE PUEDE PISAR mandando el valor explicito en el
Create — es exactamente lo que ya hace `seed-supervisor-aprobaciones.py` con
`sanic_name = 'AR-STRESS-####'`. Se reusa ese patron probado:

    sanic_name = 'SRC-<TAG><correlativo>'   en TODAS las tablas sembradas

Ventajas frente a cualquier otro marcador:
  - un unico filtro `startswith(sanic_name,'SRC-')`, SERVER-SIDE y delegable,
    sirve para el inventario y para el `--clean` de las 13 tablas;
  - no colisiona con los marcadores de Reception ('SR'+digitos en planillas,
    'ST'+digitos en tulas: sin guion) ni con 'AR-STRESS-' del seed de supervisor;
  - no ensucia ninguna columna de negocio (los `BagNumber`, `CheckNumber`,
    `Denomination`, `Currency` y montos son REALES, que es lo que la app lee).

Las tablas cuyo `sanic_name` NO es escribible con sentido propio se rastrean por
su padre en memoria (misma tecnica que las sucursales del seed de Reception):
ninguna. Las 13 llevan marcador propio.

-----------------------------------------------------------------------------
EL GUARD Y COMO SE LO RESPETA — EL ORDEN LO HACE LEGAL
-----------------------------------------------------------------------------
Este seeder corre con el guard 100% ACTIVO. Los gates que lo miran:

  GuardRecountingPlugin (Pre-validation 10)
      Create/Update/Delete de DepositsRecountings, Checks y CountedMoneyDetails
      exigen que la tula ANCESTRO este 'Asignada a cajero' (159460002).
      => F3 patchea PRIMERO la tula a 'Asignada a cajero' y RECIEN DESPUES crea
         depositos, arqueos y cheques. Cerrar la tula es lo ULTIMO.

  GuardReceptionPlugin (Pre-validation, matriz de BagStatus)
      'Almacenada en boveda' -> 'Asignada a cajero'      LEGAL
      'Asignada a cajero'    -> 'Recontada'              LEGAL
      'Asignada a cajero'    -> 'Pendiente … - recuento' LEGAL
      'Recontada'            -> *                        TERMINAL, nada sale
      => el orden de F3 es exactamente ese, y por eso el `--clean` SI necesita
         apagar el Update de tulas: de 'Recontada' no se vuelve.

  BagRecountClosurePlugin (Pre-op 20 / Post-op 40 Update de tulas)
      Al ENTRAR a Recontada o a Pendiente-recuento recalcula TotalNIO/USD/EUR
      desde los depositos PERSISTIDOS y crea los CH-Tbl-Transactions.
      => EL SEEDER NO ESCRIBE NI TOTALES NI TRANSACCIONES. Son la SALIDA que se
         verifica, no una entrada. Si el seeder los mandara, estaria probando su
         propio JSON en vez del plugin.

  CheckGuard (Create de cheques)
      Un cheque solo nace SIN estado o en 'Recontado'.
      => los cheques nacen sin `sanic_checkstatus`; el marcado a 'Recontado' va
         en el cierre, en un Update que toca UNICAMENTE `sanic_checkstatus`
         (exencion `touchesOnlyCheckStatus` de RecountingGuard.IsWriteExempt).

  ReceptionGuard.EvaluateApprovalCreate
      🧨 UNA SOLICITUD NO PUEDE NACER RESUELTA. La unica excepcion es el tipo
      EXACTO "Crear sesion de recuento" naciendo 'Aprobado' con la sesion del
      JSON perteneciendo al solicitante — y eso lo estampa el propio
      RecountingSessionAutoApprovePlugin, no el cliente.
      => ver la seccion de decisiones de abajo.

  Espejos (E-1 DepositSessionMirror, E-2 CheckMirrorStamp, E-3
  DeclaredCheckBagMirror, E-4 XmlReadingDetailMirror)
      NUNCA se mandan en el JSON: los estampan los plugins y `--verify` los
      muestrea. Se sembraron para probarlos, no para simularlos.

-----------------------------------------------------------------------------
DECISIONES TOMADAS (y por que)
-----------------------------------------------------------------------------
1) IDENTIDADES. `RecountingSessions.Cashier` y `CountingDesks.Cajero` son
   lookups REQUERIDOS a `systemuser`. Se usan usuarios REALES del entorno
   (`isdisabled eq false and applicationid eq null`, ordenados por `fullname`);
   si el entorno no tiene ninguno, se cae al usuario de aplicacion del SP
   (`WhoAmI`). NO hay excepcion por identidad en ningun guard del repo
   (05 §6.3, punto 4·b·2) y este seeder no pide una.

2) FECHAS HISTORICAS. `sanic_chtblrecountingsession` (y todas las demas del
   dominio) exponen `overriddencreatedon` con `ValidForCreateApi=1`. Se manda en
   el Create y Dataverse deja `createdon` con esa fecha: las ~600 sesiones
   quedan repartidas entre ayer y hace 90 dias. `--no-backdate` lo desactiva si
   algun entorno lo rechaza; el `--dry-run` VERIFICA la metadata de
   `overriddencreatedon` antes de prometer nada.

3) SOLICITUDES HISTORICAS RESUELTAS. Hay DOS caminos y solo uno es gratis:
   (a) tipo "Crear sesion de recuento" cuya sesion cuelga de una mesa cuyo
       `Cajero` es el propio actor: `RecountingSessionAutoApprovePlugin` la
       auto-aprueba EN EL CREATE. Nacen RESUELTAS sin tocar un solo step. Es el
       camino por defecto para ese tipo.
   (b) los otros TRES tipos NO pueden nacer resueltas (EvaluateApprovalCreate) y
       tampoco se pueden resolver despues por Update: `EvaluateApprovalUpdate`
       exige resolutor != creador Y pertenencia al grupo AAD aprobador, y el
       seeder es una sola identidad. Por defecto NACEN PENDIENTES.
       `--resolve-approvals` desactiva TEMPORALMENTE el unico step
       `CashHub.GuardReception: Update de sanic_chtblapprovalrequest`, resuelve
       en lote y lo restaura (try/finally + verificacion), con el mismo contrato
       auditable que el `--clean`. Es OPT-IN a proposito.

4) TOTALES Y ASIENTOS. No se escriben (los hace el guard). `HasDifference`
   tampoco. `Total`, `TotalAmount`, `Subtotal` e `IsRelatedToBag` son CAMPOS
   FORMULA/ROLLUP: no admiten escritura.

5) `AccountNumber` es INT (1..999999999), no texto con guiones (01 §2.4, L-4).
   `Quantity` de CountedMoneyDetails tiene MINIMO 1 y `CheckAmount` minimo 1:
   el generador nunca produce ceros.

6) `OtherLocaCheckAmount` (DepositsRecountings, SIN la `l`) vs
   `OtherLocalCheckAmount` (DeclaredDeposits, CON la `l`). Son dos nombres
   distintos en el modelo. Verificado en Entity.xml.

-----------------------------------------------------------------------------
EL --clean Y LOS STEPS QUE HAY QUE APAGAR
-----------------------------------------------------------------------------
El borrado no tiene orden legal posible (de 'Recontada' no se vuelve, y una
sesion Confirmed no se borra ni siendo propia), asi que se desactivan SOLO estos
SEIS pasos y se restauran SIEMPRE (try/finally + verificacion + `--restore-steps`):

    CashHub.GuardRecounting: Delete de sanic_chtbldepositsrecounting
    CashHub.GuardRecounting: Delete de sanic_chtblchecks
    CashHub.GuardRecounting: Delete de sanic_chtblcountedmoneydetails
    CashHub.GuardRecounting: Delete de sanic_chtblapprovalrequest
    CashHub.GuardRecounting: Delete de sanic_chtblrecountingsession
    CashHub.GuardReception:  Update de sanic_chtblbags

(los 5 primeros salen textuales del header de `register-recounting-guard.sh`;
el sexto es el que congela 'Recontada' y hay que apagarlo para devolver las
tulas a 'Almacenada en boveda').
Los ESPEJOS y `BagRecountClosure` NUNCA se tocan: al volver a boveda el cierre
es un no-op por contrato (solo actua al ENTRAR a Recontada/Pendiente).
`sanic_chtbltransactions` NO tiene step de Delete registrado: los asientos que
genero el guard se borran sin apagar nada.

-----------------------------------------------------------------------------
Uso:
    python3 scripts/seed-recounting-stress.py --dry-run          # plan, cero escrituras
    python3 scripts/seed-recounting-stress.py --all              # todas las fases
    python3 scripts/seed-recounting-stress.py --only-declarations
    python3 scripts/seed-recounting-stress.py --only-sessions
    python3 scripts/seed-recounting-stress.py --only-recount
    python3 scripts/seed-recounting-stress.py --only-xml
    python3 scripts/seed-recounting-stress.py --resume --all     # retoma sin duplicar
    python3 scripts/seed-recounting-stress.py --verify
    python3 scripts/seed-recounting-stress.py --clean
    python3 scripts/seed-recounting-stress.py --restore-steps    # rescate
Requiere pp_secrets.env en la raiz. NO correr en produccion.
"""
import argparse
import http.client
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
from datetime import datetime, timedelta, timezone

random.seed(20260817)

# ============================================================ marcador propio
MARK = 'SRC-'                       # 'Stress ReCounting' — no colisiona con SR*/ST*/AR-STRESS-

TAG_DECL = MARK + 'DECL'            # sanic_chtbldeclarations
TAG_DBAG = MARK + 'DB'              # sanic_chtbldeclaredbags
TAG_DDEP = MARK + 'DD'              # sanic_chtbldeclareddeposits
TAG_DCHK = MARK + 'DC'              # sanic_chtbldeclaredchecks
TAG_DESK = MARK + 'MESA'            # sanic_chtblcountingdesk
TAG_SESS = MARK + 'S'               # sanic_chtblrecountingsession
TAG_SDET = MARK + 'SD'              # sanic_chtblrecountingsessiondetail
TAG_DEP = MARK + 'DR'               # sanic_chtbldepositsrecounting
TAG_CMD = MARK + 'CMD'              # sanic_chtblcountedmoneydetails
TAG_CHK = MARK + 'CK'               # sanic_chtblchecks
TAG_APR = MARK + 'AR'               # sanic_chtblapprovalrequest
TAG_XML = MARK + 'XR'               # sanic_chtblxmlreading
TAG_XMD = MARK + 'XD'               # sanic_chtblxmlreadingdetail

# marcador de la MATERIA PRIMA (lo dejo seed-reception-stress.py)
RCP_BAG_PREFIX = 'ST'

# ================================================================ option sets
# sanic_chchcurrency (GLOBAL) — 01 §5.2
CUR_NIO, CUR_USD, CUR_EUR = 159460000, 159460001, 159460002
CUR_LABEL = {CUR_NIO: 'NIO', CUR_USD: 'USD', CUR_EUR: 'EUR'}

# BagStatus (local de sanic_chtblbags) — 01 §5.1
BAG_ESCLUSA = 159460000
BAG_BOVEDA = 159460001
BAG_CAJERO = 159460002
BAG_RECONTADA = 159460003
BAG_PEND_RECUENTO = 159460004
BAG_PEND_ESCLUSA = 159460005
BAG_LABEL = {BAG_ESCLUSA: 'Recibida en esclusa', BAG_BOVEDA: 'Almacenada en boveda',
             BAG_CAJERO: 'Asignada a cajero', BAG_RECONTADA: 'Recontada',
             BAG_PEND_RECUENTO: 'Pendiente supervisor - recuento',
             BAG_PEND_ESCLUSA: 'Pendiente supervisor - esclusa'}

# ch-recountingDetailsStatus (GLOBAL) — 01 §5.3
DET_EN_PROGRESO = 159460000
DET_CERRADO = 159460001
DET_CERRADO_DIF = 159460002

# CheckTypes (local de sanic_chtblchecks) / CheckType (local de declaredchecks) — 01 §5.4
CHK_BAC, CHK_OTROS = 159460000, 159460001
# CheckStatus (local de sanic_chtblchecks)
CHK_RECONTADO = 159460000

# ApprovalStatus (local) — 01 §5.5
APR_APROBADO, APR_RECHAZADO = 159460000, 159460001

# Type (local de sanic_chtblmoneydenominations)
DEN_BILLETE, DEN_MONEDA = 159460000, 159460001
DEN_TYPE_TEXT = {DEN_BILLETE: 'Billete', DEN_MONEDA: 'Moneda'}

# RequestType — TEXTO LIBRE, strings EXACTOS de 01 §5.6
RT_SESION = 'Crear sesion de recuento'
RT_SIN_DECL = 'Tula sin declaracion'
RT_CON_DIF = 'Tula con diferencia'
RT_RECONT_SIN_DECL = 'Tula recontada sin declaracion'

# ======================================================== steps de los guards
STEP_DEL_DEPOSITO = 'CashHub.GuardRecounting: Delete de sanic_chtbldepositsrecounting'
STEP_DEL_CHEQUE = 'CashHub.GuardRecounting: Delete de sanic_chtblchecks'
STEP_DEL_ARQUEO = 'CashHub.GuardRecounting: Delete de sanic_chtblcountedmoneydetails'
STEP_DEL_SOLICITUD = 'CashHub.GuardRecounting: Delete de sanic_chtblapprovalrequest'
STEP_DEL_SESION = 'CashHub.GuardRecounting: Delete de sanic_chtblrecountingsession'
STEP_UPD_TULA = 'CashHub.GuardReception: Update de sanic_chtblbags'
STEPS_CLEAN = (STEP_DEL_DEPOSITO, STEP_DEL_CHEQUE, STEP_DEL_ARQUEO,
               STEP_DEL_SOLICITUD, STEP_DEL_SESION, STEP_UPD_TULA)

# unico step que hay que apagar para RESOLVER solicitudes historicas (opt-in)
STEP_UPD_SOLICITUD = 'CashHub.GuardReception: Update de sanic_chtblapprovalrequest'

# Se auditan pero JAMAS se desactivan.
STEPS_AUDIT = (
    'CashHub.GuardRecounting: Create de sanic_chtbldepositsrecounting',
    'CashHub.GuardRecounting: Update de sanic_chtbldepositsrecounting',
    'CashHub.GuardRecounting: Create de sanic_chtblchecks',
    'CashHub.GuardRecounting: Update de sanic_chtblchecks',
    'CashHub.GuardRecounting: Create de sanic_chtblcountedmoneydetails',
    'CashHub.GuardRecounting: Update de sanic_chtblcountedmoneydetails',
    'CashHub.BagRecountClosure: Update de sanic_chtblbags (totales)',
    'CashHub.BagRecountClosure: Update de sanic_chtblbags (asientos)',
    'CashHub.RecountingAutoApprove: Create de sanic_chtblapprovalrequest (Pre-op)',
    'CashHub.RecountingAutoApprove: Create de sanic_chtblapprovalrequest (Post-op)',
    'CashHub.DepositSessionMirror: Create de sanic_chtbldepositsrecounting',
    'CashHub.DepositSessionMirror: Update de sanic_chtbldepositsrecounting',
    'CashHub.DepositSessionMirror: Update de sanic_chtblbags',
    'CashHub.CheckMirrorStamp: Create de sanic_chtblchecks',
    'CashHub.CheckMirrorStamp: Update de sanic_chtblchecks',
    'CashHub.DeclaredCheckBagMirror: Create de sanic_chtbldeclaredchecks',
    'CashHub.DeclaredCheckBagMirror: Update de sanic_chtbldeclaredchecks',
    'CashHub.XmlReadingDetailMirror: Create de sanic_chtblxmlreadingdetail',
    'CashHub.XmlReadingDetailMirror: Update de sanic_chtblxmlreadingdetail',
    'CashHub.XmlReadingDetailMirror: Update de sanic_chtblxmlreading',
    'CashHub.GuardReception: Update de sanic_chtblapprovalrequest',
    # 'CashHub.GuardReception: Update de sanic_chtblbags' NO va aca: ya esta en
    # STEPS_CLEAN (es el que hay que apagar para devolver una tula Recontada a
    # boveda). Listarlo dos veces inflaria el conteo de pasos auditados.
)

# ===================================================== entity sets (EntitySetName real)
ES_BAG = 'sanic_chtblbagses'
ES_DECL = 'sanic_chtbldeclarations'
ES_DBAG = 'sanic_chtbldeclaredbagses'
ES_DDEP = 'sanic_chtbldeclareddepositses'
ES_DCHK = 'sanic_chtbldeclaredcheckses'
ES_DESK = 'sanic_chtblcountingdesks'
ES_SESS = 'sanic_chtblrecountingsessions'
ES_SDET = 'sanic_chtblrecountingsessiondetails'
ES_DEP = 'sanic_chtbldepositsrecountings'
ES_CMD = 'sanic_chtblcountedmoneydetailses'
ES_CHK = 'sanic_chtblcheckses'
ES_APR = 'sanic_chtblapprovalrequests'
ES_XML = 'sanic_chtblxmlreadings'
ES_XMD = 'sanic_chtblxmlreadingdetails'
ES_DEN = 'sanic_chtblmoneydenominations'
ES_TRX = 'sanic_chtbltransactionses'
ES_USER = 'systemusers'

PK = {
    ES_BAG: 'sanic_chtblbagsid',
    ES_DECL: 'sanic_chtbldeclarationid',
    ES_DBAG: 'sanic_chtbldeclaredbagsid',
    ES_DDEP: 'sanic_chtbldeclareddepositsid',
    ES_DCHK: 'sanic_chtbldeclaredchecksid',
    ES_DESK: 'sanic_chtblcountingdeskid',
    ES_SESS: 'sanic_chtblrecountingsessionid',
    ES_SDET: 'sanic_chtblrecountingsessiondetailid',
    ES_DEP: 'sanic_chtbldepositsrecountingid',
    ES_CMD: 'sanic_chtblcountedmoneydetailsid',
    ES_CHK: 'sanic_chtblchecksid',
    ES_APR: 'sanic_chtblapprovalrequestid',
    ES_XML: 'sanic_chtblxmlreadingid',
    ES_XMD: 'sanic_chtblxmlreadingdetailid',
    ES_DEN: 'sanic_chtblmoneydenominationid',
    ES_TRX: 'sanic_chtbltransactionsid',
    ES_USER: 'systemuserid',
}

# Tablas sembradas, EN ORDEN INVERSO LEGAL DE BORRADO. El tag es el prefijo de
# sanic_name; el clean filtra server-side con startswith.
SEMBRADAS = [
    (ES_CMD, TAG_CMD, 'arqueos (CountedMoneyDetails)'),
    (ES_CHK, TAG_CHK, 'cheques (Checks)'),
    (ES_DEP, TAG_DEP, 'depositos (DepositsRecountings)'),
    (ES_XMD, TAG_XMD, 'detalles de lectura (XMLReadingDetails)'),
    (ES_XML, TAG_XML, 'lecturas (XMLReadings)'),
    (ES_SDET, TAG_SDET, 'detalles de sesion (RecountingSessionDetails)'),
    (ES_APR, TAG_APR, 'solicitudes (ApprovalRequests)'),
    (ES_SESS, TAG_SESS, 'sesiones (RecountingSessions)'),
    (ES_DCHK, TAG_DCHK, 'cheques declarados (DeclaredChecks)'),
    (ES_DDEP, TAG_DDEP, 'depositos declarados (DeclaredDeposits)'),
    (ES_DBAG, TAG_DBAG, 'bolsas declaradas (DeclaredBags)'),
    (ES_DECL, TAG_DECL, 'declaraciones (Declarations)'),
    (ES_DESK, TAG_DESK, 'mesas (CountingDesks)'),
]

# ===========================================================================
# auth + http — patron identico a seed-reception-stress.py
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

# contador de reintentos por causa (se imprime al final: dice cuanto costo la red)
REINTENTOS = {}
_ret_lock = threading.Lock()
# ENSAYO EN SECO de las escrituras (--dry-writes): las LECTURAS son reales, las
# escrituras se simulan. Sirve para validar los deltas del --resume sin tocar DEV.
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


def req(method, path, body=None, retry=6, prefer=None, timed=False):
    """Request unitario. Devuelve dict (o (dict, segundos) si timed)."""
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
        # 🧨 El timeout del SOCKET no viaja envuelto en URLError: salta CRUDO desde
        # http.client.HTTPConnection.getresponse() (TimeoutError, que es OSError) y
        # sin este except se lleva el proceso puesto en corridas largas.
        except (TimeoutError, OSError, http.client.HTTPException) as e:
            last = f'{method} {path[:140]}: {type(e).__name__} {e}'
            if attempt < retry - 1:
                _reintento(f'req/{type(e).__name__}')
                time.sleep(5)
                continue
            raise RuntimeError(last) from e
    raise RuntimeError(last or 'sin reintentos')


def get_all(path, page=5000):
    rows, url = [], path
    while url:
        d = req('GET', url, prefer=f'odata.maxpagesize={page}')
        rows.extend(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return rows


def count_of(entity_set, filtro):
    """Conteo REAL (no @odata.count, que Dataverse tope en 5000): pagina el id."""
    n = 0
    url = f'/{entity_set}?$select={PK[entity_set]}&$filter={urllib.parse.quote(filtro)}'
    while url:
        d = req('GET', url, prefer='odata.maxpagesize=5000')
        n += len(d.get('value', []))
        nxt = d.get('@odata.nextLink')
        url = nxt[len(API):] if nxt else None
    return n


def marcadas(entity_set, tag, select=None):
    """Filas sembradas por este seeder: startswith(sanic_name,'<tag>')."""
    sel = select or PK[entity_set]
    flt = urllib.parse.quote(f"startswith(sanic_name,'{tag}')")
    return get_all(f'/{entity_set}?$select={sel}&$filter={flt}')


def contar_marcadas(entity_set, tag):
    return count_of(entity_set, f"startswith(sanic_name,'{tag}')")


def next_correlativo(rows, prefix):
    top = 0
    for r in rows:
        v = (r.get('sanic_name') or '')[len(prefix):]
        if v.isdigit():
            top = max(top, int(v))
    return top + 1


# ===========================================================================
# RESUME — el delta se decide SIEMPRE contra lo que hay en DEV, nunca contra
# un contador ciego. Cada etapa pregunta "¿cuanto de MI objetivo ya existe?"
# leyendo el marcador SRC-<TAG>, y devuelve cuantas filas le faltan.
# ===========================================================================
def etapa(label, ya, objetivo, resume):
    """Devuelve cuantas filas hay que crear en esta etapa.

    Sin --resume el comportamiento es el historico (crea el objetivo entero).
    Con --resume: 0 si la etapa ya esta COMPLETA, el DELTA si quedo PARCIAL.
    """
    if not resume:
        return objetivo
    falta = max(0, objetivo - ya)
    estado = 'COMPLETA -> SALTO' if falta == 0 else f'PARCIAL -> delta {falta}'
    print(f'  [resume] {label:44} ya {ya:7} / objetivo {objetivo:7} · {estado}')
    return falta


def agrupar(rows, campo):
    """{valor_lookup_lower: [filas]} — para contar hijos por padre sin N+1."""
    out = {}
    for r in rows:
        out.setdefault((r.get(campo) or '').lower(), []).append(r)
    return out


# ===========================================================================
# $batch — un changeset POR REQUEST (cada operacion falla sola)
# ===========================================================================
def batch_exec(requests_, retry=6):
    """requests_ = [(method, path_relativo, body|None)].
    Devuelve [(status:int, entity_id:str|None, error:str|None)] en ORDEN."""
    if not requests_:
        return []
    if NOWRITE['on']:
        # ENSAYO: se ejercita TODA la logica de deltas (incluido --resume) sin
        # mandar una sola escritura. Los ids son falsos a proposito.
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
        try:
            with urllib.request.urlopen(r, timeout=600) as resp:
                return _parse_batch(resp.read().decode('utf-8'), len(requests_))
        except urllib.error.HTTPError as e:
            cuerpo = e.read().decode()
            # 🧨 Dataverse contesta el SOBRE del $batch con 400 cuando UNA de las
            # partes falla, pero el cuerpo es el multipart COMPLETO con el status
            # de cada parte. Tratarlo como fallo total marcaba como error las 100
            # operaciones del chunk cuando en realidad la mayoria SE ESCRIBIO
            # (el run del 2026-08-17 reporto 4.937 errores con 424 filas creadas).
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
        # 🧨 MISMO AGUJERO que en req(): el TimeoutError del socket salta crudo desde
        # h.getresponse() (no lo envuelve URLError) y antes mataba el proceso entero
        # DESDE UN THREAD del pool, perdiendo la fase a medias. Ahora reintenta.
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
    """Ejecuta requests_ en $batch paralelos. Devuelve (resultados_en_orden, segundos)."""
    if not requests_:
        print(f'  {label}: nada que hacer')
        return [], 0.0
    chunks = [requests_[i:i + chunk] for i in range(0, len(requests_), chunk)]
    results = [None] * len(chunks)
    pr = Progress(len(requests_), label)
    t0 = time.perf_counter()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(batch_exec, c): i for i, c in enumerate(chunks)}
        for fut in as_completed(futs):
            i = futs[fut]
            results[i] = fut.result()
            errs = [r[2] for r in results[i] if r[2]]
            pr.add(len(results[i]) - len(errs), len(errs), errs)
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
    flt = urllib.parse.quote("startswith(name,'CashHub.')")
    rows = get_all('/sdkmessageprocessingsteps?$select=name,statecode,statuscode,'
                   'sdkmessageprocessingstepid&$filter=' + flt)
    return {r['name']: r for r in rows}


def set_step(step_id, activo):
    body = {'statecode': 0, 'statuscode': 1} if activo else {'statecode': 1, 'statuscode': 2}
    req('PATCH', f'/sdkmessageprocessingsteps({step_id})', body)


def mostrar_steps(titulo, nombres, todos=None):
    todos = todos if todos is not None else leer_steps()
    print(f'  {titulo}')
    faltan = []
    for n in nombres:
        s = todos.get(n)
        if not s:
            print(f'    [NO EXISTE] {n}')
            faltan.append(n)
            continue
        estado = 'ACTIVO' if s['statecode'] == 0 and s['statuscode'] == 1 else 'INACTIVO'
        print(f'    [{estado:8}] {n}')
    return todos, faltan


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
# identidades y catalogos
# ===========================================================================
def whoami():
    return req('GET', '/WhoAmI()').get('UserId')


def usuarios(n=60):
    """Usuarios REALES habilitados (no aplicaciones). Fallback: el propio SP.

    No hay excepcion por identidad en ningun guard (05 §6.3, 4·b·2): el seeder
    usa identidades de verdad y respeta el guard por ORDEN, no por privilegio.
    """
    rows = get_all(f'/{ES_USER}?$select=systemuserid,fullname,domainname'
                   f'&$filter=isdisabled eq false and applicationid eq null'
                   f'&$orderby=fullname asc&$top={n}', page=n)
    if rows:
        return rows, False
    me = whoami()
    print('  !! el entorno no tiene usuarios reales habilitados: se usa el usuario '
          'de aplicacion del SP como Cashier/Cajero', file=sys.stderr)
    return [{'systemuserid': me, 'fullname': 'Service Principal'}], True


def denominaciones():
    """Denominaciones REALES de CH-Tbl-MoneyDenominations (solo habilitadas)."""
    rows = get_all(f'/{ES_DEN}?$select=sanic_value,sanic_currency,sanic_type,sanic_enabled,'
                   f'sanic_name&$filter=sanic_enabled eq true')
    por_moneda = {}
    for r in rows:
        c = r.get('sanic_currency')
        if c is None or r.get('sanic_value') in (None, 0):
            continue
        por_moneda.setdefault(c, []).append(r)
    for v in por_moneda.values():
        v.sort(key=lambda r: -float(r['sanic_value']))
    return por_moneda


def bags_boveda(select='sanic_bagid,sanic_bagstatus,_sanic_receptionid_value,'
                       'sanic_receiptidmirror,_sanic_bagdeclarationid_value'):
    """Tulas del stress de RECEPCION que estan en boveda: la materia prima."""
    flt = urllib.parse.quote(
        f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') and sanic_bagstatus eq {BAG_BOVEDA}")
    return get_all(f'/{ES_BAG}?$select={select}&$filter={flt}')


def bags_marcadas_recuento(select='sanic_bagid,sanic_bagstatus,_sanic_recountingsession_value,'
                                  '_sanic_bagdeclarationid_value'):
    """Tulas ST* que este seeder movio fuera de boveda (cualquier estado de recuento)."""
    flt = urllib.parse.quote(
        f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') and "
        f"(sanic_bagstatus eq {BAG_CAJERO} or sanic_bagstatus eq {BAG_RECONTADA} "
        f"or sanic_bagstatus eq {BAG_PEND_RECUENTO})")
    return get_all(f'/{ES_BAG}?$select={select}&$filter={flt}')


# ===========================================================================
# generadores de datos realistas
# ===========================================================================
def monto(lo=500, hi=250_000):
    return round(random.uniform(lo, hi), 2)


def cuenta():
    return random.randint(100_000_000, 999_999_999)      # int, 1..999999999 (L-4)


def moneda_pesada():
    """NIO domina, USD segundo, EUR marginal — como el negocio real."""
    return random.choices((CUR_NIO, CUR_USD, CUR_EUR), weights=(72, 25, 3))[0]


def fecha_pasada(dias_max, ahora=None):
    ahora = ahora or datetime.now(timezone.utc)
    d = random.randint(1, max(1, dias_max))
    h = random.randint(7, 19)
    m = random.choice((0, 7, 15, 23, 31, 44, 52))
    return (ahora - timedelta(days=d)).replace(
        hour=h, minute=m, second=random.randint(0, 59), microsecond=0
    ).strftime('%Y-%m-%dT%H:%M:%SZ')


def html_solicitud(titulo, tula, planilla, cajero, mesa, extra=''):
    """HTML corto (RequestDetails es ntext(10000): esto entra holgadisimo)."""
    return (f'<h3>{titulo}</h3>'
            f'<table><tr><td>Tula</td><td>{tula}</td></tr>'
            f'<tr><td>Planilla</td><td>{planilla or "-"}</td></tr>'
            f'<tr><td>Cajero</td><td>{cajero}</td></tr>'
            f'<tr><td>Mesa</td><td>{mesa}</td></tr>'
            f'{extra}</table>')


# ===========================================================================
# INVENTARIO / PLAN
# ===========================================================================
def inventario():
    print('\n== INVENTARIO PREVIO (leido de DEV) ==')
    inv = {}
    boveda = count_of(ES_BAG, f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') "
                              f"and sanic_bagstatus eq {BAG_BOVEDA}")
    st_total = count_of(ES_BAG, f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}')")
    print(f'  tulas del stress de RECEPCION (ST*)        : {st_total}')
    print(f'    ... en Almacenada en boveda (materia prima): {boveda}')
    for est in (BAG_CAJERO, BAG_RECONTADA, BAG_PEND_RECUENTO):
        n = count_of(ES_BAG, f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') "
                             f"and sanic_bagstatus eq {est}")
        print(f'    ... en {BAG_LABEL[est]:34}: {n}')
        inv[f'bags_{est}'] = n
    inv['bags_boveda'] = boveda
    inv['bags_st'] = st_total

    print('  ya sembrado por ESTE seeder (marcador SRC-*):')
    for es, tag, label in SEMBRADAS:
        n = contar_marcadas(es, tag)
        inv[tag] = n
        print(f'    {label:44} {n}')

    den = denominaciones()
    inv['denominaciones'] = {CUR_LABEL.get(k, k): len(v) for k, v in den.items()}
    print(f'  denominaciones habilitadas por moneda      : {inv["denominaciones"]}')
    usrs, es_sp = usuarios()
    inv['usuarios'] = len(usrs)
    inv['solo_sp'] = es_sp
    print(f'  usuarios reales habilitados                : {len(usrs)}'
          f'{"  (SOLO SP)" if es_sp else ""}')
    return inv, den, usrs


def metadata_backdate():
    """¿overriddencreatedon es escribible en el Create de la sesion?"""
    try:
        d = req('GET', "/EntityDefinitions(LogicalName='sanic_chtblrecountingsession')"
                       "/Attributes(LogicalName='overriddencreatedon')"
                       "?$select=LogicalName,IsValidForCreate,IsValidForUpdate")
        return bool(d.get('IsValidForCreate')), d
    except RuntimeError as e:
        return False, {'error': str(e)[:200]}


def plan(a, inv):
    """Devuelve el plan de volumenes y lo imprime. No escribe nada."""
    p = {}
    disp = inv['bags_boveda']
    p['declared_bags'] = min(a.declared_bags, disp)
    p['declared_deposits'] = int(p['declared_bags'] * (a.dep_min + a.dep_max) / 2)
    p['declared_checks'] = int(p['declared_deposits'] * 1.15)
    p['fat_declared'] = a.fat_declared
    p['fat_declared_checks'] = a.fat_declared * a.fat_declared_deps * a.fat_declared_chk
    p['desks'] = a.desks
    p['sessions'] = a.sessions
    p['approvals'] = a.approvals
    p['recount_bags'] = min(a.recount_bags, disp)
    p['recount_deposits'] = int(p['recount_bags'] * (a.rdep_min + a.rdep_max) / 2)
    p['counted_details'] = int(p['recount_deposits'] * (a.cmd_min + a.cmd_max) / 2)
    p['recount_checks'] = int(p['recount_deposits'] * 0.55) + a.fat_bag_checks
    p['close_clean'] = a.close_clean
    p['close_diff'] = a.close_diff
    p['left_open'] = max(0, p['recount_bags'] - a.close_clean - a.close_diff)
    p['xml_readings'] = a.xml_readings
    p['xml_details'] = int(a.xml_readings * (a.xmd_min + a.xmd_max) / 2)

    print('\n== PLAN (numeros objetivo) ==')
    print(f'  F1 DECLARACIONES')
    print(f'     DeclaredBags                : {p["declared_bags"]}   (tope 500/2000 de la grilla B1)')
    print(f'     DeclaredDeposits            : ~{p["declared_deposits"]}')
    print(f'     DeclaredChecks              : ~{p["declared_checks"]}')
    print(f'     bolsas GORDAS               : {p["fat_declared"]} x '
          f'{a.fat_declared_deps} depositos x {a.fat_declared_chk} cheques = '
          f'{a.fat_declared_deps * a.fat_declared_chk} cheques declarados POR BOLSA '
          f'({">500 OK" if a.fat_declared_deps * a.fat_declared_chk > 500 else "NO cruza 500"})')
    print(f'  F2 SESIONES E HISTORICO')
    print(f'     CountingDesks               : {p["desks"]}')
    print(f'     RecountingSessions          : {p["sessions"]}  (Confirmed, retro-fechadas '
          f'hasta {a.backdate_days} dias)')
    print(f'     ApprovalRequests            : {p["approvals"]}  '
          f'({">500 OK" if p["approvals"] > 500 else "NO cruza 500"})')
    print(f'  F3 RECONTEO')
    print(f'     tulas abiertas              : {p["recount_bags"]}  '
          f'(boveda -> Asignada a cajero)')
    print(f'     RecountingSessionDetails    : {p["recount_bags"]}')
    print(f'     DepositsRecountings         : ~{p["recount_deposits"]}')
    print(f'     CountedMoneyDetails         : ~{p["counted_details"]}')
    print(f'     Checks                      : ~{p["recount_checks"]}  '
          f'(incluye UNA tula gorda de {a.fat_bag_checks} cheques: '
          f'{">500 OK" if a.fat_bag_checks > 500 else "NO cruza 500"})')
    print(f'     cierres a Recontada         : {p["close_clean"]}  '
          f'(el guard recalcula totales y crea Transactions)')
    print(f'     cierres a Pendiente-recuento: {p["close_diff"]}  (+1 ApprovalRequest c/u)')
    print(f'     quedan ABIERTAS             : {p["left_open"]}')
    print(f'  F4 CONTADORA')
    print(f'     XMLReadings                 : {p["xml_readings"]}  (IsActive mixto)')
    print(f'     XMLReadingDetails           : ~{p["xml_details"]}  '
          f'({">20000 OK" if p["xml_details"] > 20000 else "NO cruza 20k"})')
    total = (p['declared_bags'] + p['declared_deposits'] + p['declared_checks']
             + p['desks'] + p['sessions'] + p['approvals'] + p['recount_bags'] * 2
             + p['recount_deposits'] + p['counted_details'] + p['recount_checks']
             + p['close_clean'] + p['close_diff'] * 2
             + p['xml_readings'] + p['xml_details'])
    print(f'  ---- ESCRITURAS TOTALES ESTIMADAS: ~{total} '
          f'(+ ~{p["close_clean"] * 3} CH-Tbl-Transactions que crea el GUARD)')
    return p


# ===========================================================================
# F1 — DECLARACIONES
# ===========================================================================
def _f1_retomar_huerfanas(a, dbags, t_ini):
    """--resume con las 3000 bolsas ya declaradas: solo cierra las HUERFANAS.

    Una DeclaredBag sin NINGUN DeclaredDeposit es, por dato, una etapa 3 que
    quedo a medias (el generador nunca produce 0 depositos: dep_min >= 1).
    En cambio 'un deposito sin cheques' es un caso LEGITIMO (38% de peso), asi
    que no se puede retomar por dato y no se toca: quedaria duplicando.
    """
    ddeps = get_all(f'/{ES_DDEP}?$select=sanic_name,_sanic_declaredbag_value&$filter='
                    + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DDEP}')"))
    por_dbag = agrupar(ddeps, '_sanic_declaredbag_value')
    huerfanas = [r for r in dbags if r[PK[ES_DBAG]].lower() not in por_dbag]
    print(f'  [resume] DeclaredBags SIN depositos (etapa 3 a medias): {len(huerfanas)}')
    if not huerfanas:
        print('  [resume] F1 COMPLETA: no se escribe nada')
        return {'salteado': True, 'dbags': len(dbags), 'ddeps': len(ddeps)}
    seq = {'ddep': next_correlativo(ddeps, TAG_DDEP),
           'dchk': next_correlativo(marcadas(ES_DCHK, TAG_DCHK, 'sanic_name'), TAG_DCHK)}
    reqs, meta = [], []
    for db in huerfanas:
        for _ in range(random.randint(a.dep_min, a.dep_max)):
            nombre = f'{TAG_DDEP}{seq["ddep"]:06d}'
            seq['ddep'] += 1
            cur = moneda_pesada()
            n_chk = random.choices((0, 1, 2, 3, 5, 8), weights=(38, 25, 16, 11, 7, 3))[0]
            reqs.append(('POST', f'/{ES_DDEP}', {
                'sanic_name': nombre,
                'sanic_DeclaredBag@odata.bind': f'/{ES_DBAG}({db[PK[ES_DBAG]]})',
                'sanic_accountnumber': cuenta(),
                'sanic_currency': cur,
                'sanic_cashamount': monto(),
                'sanic_baclocalcheckamount': monto(0, 60_000) if n_chk else 0,
                'sanic_otherlocalcheckamount': monto(0, 40_000) if n_chk else 0,
                'sanic_transactionno': random.randint(1, 500),
                'sanic_depositreference': f'{MARK}{random.randint(0, 99_999_999):08d}',
            }))
            meta.append((cur, n_chk))
    res, _ = run_batches(reqs, 'completando depositos declarados huerfanos', a.chunk, a.workers)
    reqs = []
    for (cur, n_chk), (st, did, err) in zip(meta, res):
        if err or not did:
            continue
        for j in range(n_chk):
            nombre = f'{TAG_DCHK}{seq["dchk"]:06d}'
            seq['dchk'] += 1
            reqs.append(('POST', f'/{ES_DCHK}', {
                'sanic_name': nombre,
                'sanic_DeclaredDepositID@odata.bind': f'/{ES_DDEP}({did})',
                'sanic_checknumber': f'{random.randint(0, 99_999_999):08d}',
                'sanic_checkamount': round(random.uniform(1, 90_000), 2),
                'sanic_checkcurrency': cur,
                'sanic_checktype': CHK_BAC if j % 2 == 0 else CHK_OTROS,
            }))
    res2, _ = run_batches(reqs, 'completando cheques declarados', a.chunk, a.workers)
    print(f'  TOTAL {time.perf_counter() - t_ini:.1f}s')
    return {'resume': True, 'ddeps_nuevos': sum(1 for r in res if r[1]),
            'dchecks_nuevos': sum(1 for r in res2 if r[1])}


def seed_declarations(a, inv, usrs):
    print('\n' + '=' * 78)
    print('F1 — DECLARACIONES (DeclaredBags / DeclaredDeposits / DeclaredChecks)')
    print('=' * 78)
    t_ini = time.perf_counter()
    tiempos = {}

    bags = bags_boveda('sanic_bagid,_sanic_receptionid_value,sanic_receiptidmirror')
    if not bags:
        raise RuntimeError(
            "no hay tulas ST* en 'Almacenada en boveda': correr antes "
            'scripts/seed-reception-stress.py')
    ya = marcadas(ES_DBAG, TAG_DBAG, 'sanic_name,sanic_bagnumber')
    usadas = {r.get('sanic_bagnumber') for r in ya}
    libres = [b for b in bags if b['sanic_bagid'] not in usadas]
    # con --resume el objetivo es el DELTA contra las bolsas SRC-DB* que ya existen
    falta_dbag = etapa('DeclaredBags', len(ya), a.declared_bags, a.resume)
    n = min(falta_dbag, len(libres))
    objetivo = random.sample(libres, n)
    if a.resume and n == 0:
        # nada nuevo que declarar; solo se retoma la etapa 3 de las bolsas que
        # quedaron SIN depositos (esas si son detectables por dato)
        return _f1_retomar_huerfanas(a, ya, t_ini)
    seq = {
        'decl': next_correlativo(marcadas(ES_DECL, TAG_DECL, 'sanic_name'), TAG_DECL),
        'dbag': next_correlativo(ya, TAG_DBAG),
        'ddep': next_correlativo(marcadas(ES_DDEP, TAG_DDEP, 'sanic_name'), TAG_DDEP),
        'dchk': next_correlativo(marcadas(ES_DCHK, TAG_DCHK, 'sanic_name'), TAG_DCHK),
    }
    print(f'  tulas en boveda: {len(bags)} · sin declarar: {len(libres)} · a declarar: {n}')
    print(f'  correlativos desde DB{seq["dbag"]:06d} / DD{seq["ddep"]:06d} / DC{seq["dchk"]:06d}')

    # -------- 1) declaraciones contenedoras (1 cada `bags_por_declaracion`) ----
    print('\n== 1) DECLARACIONES ==')
    n_decl = max(1, (n + a.bags_por_declaracion - 1) // a.bags_por_declaracion)
    reqs = []
    for _ in range(n_decl):
        nombre = f'{TAG_DECL}{seq["decl"]:06d}'
        seq['decl'] += 1
        reqs.append(('POST', f'/{ES_DECL}', {
            'sanic_name': nombre,
            # requeridos: identifican de donde vino la declaracion materializada
            'sanic_sourceentity': 'sanic_chtblreceivedemails',
            'sanic_sourcerecordid': str(uuid.uuid4()),
        }))
    res, tiempos['declaraciones'] = run_batches(reqs, 'creando declaraciones', a.chunk, a.workers)
    decls = [d for (_, d, e) in res if d]
    if not decls:
        raise RuntimeError(f'no se creo ninguna declaracion: {res[0][2] if res else "sin respuesta"}')

    # -------- 2) DeclaredBags -------------------------------------------------
    # BagNumber = el numero REAL de la tula (ST*): es lo que la app matchea al
    # escanear (03 §B1). El marcador va en sanic_name, que es autonumber pisable.
    print('\n== 2) DECLARED BAGS ==')
    reqs, meta = [], []
    gordas = set(range(min(a.fat_declared, n)))          # las primeras N son las GORDAS
    for i, b in enumerate(objetivo):
        nombre = f'{TAG_DBAG}{seq["dbag"]:06d}'
        seq['dbag'] += 1
        n_dep = (a.fat_declared_deps if i in gordas
                 else random.randint(a.dep_min, a.dep_max))
        reqs.append(('POST', f'/{ES_DBAG}', {
            'sanic_name': nombre,
            'sanic_bagnumber': b['sanic_bagid'],
            'sanic_DeclarationID@odata.bind': f'/{ES_DECL}({random.choice(decls)})',
            # TotalDeposits es ROLLUP y IsRelatedToBag es FORMULA: no se escriben.
            # Los totales declarados SI son datos de negocio y la app los compara.
            'sanic_totalnio': monto(5_000, 900_000),
            'sanic_totalusd': round(monto(200, 30_000), 2),
            'sanic_totaleur': 0,
        }))
        meta.append((b, n_dep, i in gordas))
    res, tiempos['declaredbags'] = run_batches(reqs, 'creando DeclaredBags', a.chunk, a.workers)
    dbags = []
    for (b, n_dep, gorda), (st, did, err) in zip(meta, res):
        if err or not did:
            continue
        dbags.append({'id': did, 'bag': b, 'n_dep': n_dep, 'gorda': gorda})
    print(f'  DeclaredBags creadas: {len(dbags)}/{len(meta)}')

    # -------- 3) DeclaredDeposits --------------------------------------------
    print('\n== 3) DECLARED DEPOSITS ==')
    reqs, meta = [], []
    for db in dbags:
        for k in range(db['n_dep']):
            nombre = f'{TAG_DDEP}{seq["ddep"]:06d}'
            seq['ddep'] += 1
            cur = moneda_pesada()
            n_chk = (a.fat_declared_chk if db['gorda']
                     else random.choices((0, 1, 2, 3, 5, 8), weights=(38, 25, 16, 11, 7, 3))[0])
            cash = monto()
            bac = monto(0, 60_000) if n_chk else 0
            otros = monto(0, 40_000) if n_chk else 0
            reqs.append(('POST', f'/{ES_DDEP}', {
                'sanic_name': nombre,
                'sanic_DeclaredBag@odata.bind': f'/{ES_DBAG}({db["id"]})',
                'sanic_accountnumber': cuenta(),
                'sanic_currency': cur,
                'sanic_cashamount': cash,
                'sanic_baclocalcheckamount': bac,
                # 🧨 CON la 'l': en DeclaredDeposits es OtherLocalCheckAmount
                'sanic_otherlocalcheckamount': otros,
                'sanic_transactionno': random.randint(1, 500),
                'sanic_depositreference': f'{MARK}{random.randint(0, 99_999_999):08d}',
                # sanic_Total es CAMPO FORMULA: no se escribe.
            }))
            meta.append((db, cur, n_chk, bac, otros))
    res, tiempos['declareddeposits'] = run_batches(reqs, 'creando DeclaredDeposits',
                                                   a.chunk, a.workers)
    ddeps = []
    for (db, cur, n_chk, bac, otros), (st, did, err) in zip(meta, res):
        if err or not did:
            continue
        ddeps.append({'id': did, 'dbag': db, 'cur': cur, 'n_chk': n_chk,
                      'bac': bac, 'otros': otros})
    print(f'  DeclaredDeposits creados: {len(ddeps)}/{len(meta)}')

    # -------- 4) DeclaredChecks ----------------------------------------------
    # El espejo E-3 (DeclaredBagMirror) NO se manda: lo estampa
    # DeclaredCheckBagMirrorPlugin en el Create. --verify lo muestrea.
    print('\n== 4) DECLARED CHECKS ==')
    reqs = []
    for dd in ddeps:
        for j in range(dd['n_chk']):
            nombre = f'{TAG_DCHK}{seq["dchk"]:06d}'
            seq['dchk'] += 1
            tipo = CHK_BAC if j % 2 == 0 else CHK_OTROS
            reqs.append(('POST', f'/{ES_DCHK}', {
                'sanic_name': nombre,
                'sanic_DeclaredDepositID@odata.bind': f'/{ES_DDEP}({dd["id"]})',
                'sanic_checknumber': f'{random.randint(0, 99_999_999):08d}',
                'sanic_checkamount': round(random.uniform(1, 90_000), 2),   # min 1
                'sanic_checkcurrency': dd['cur'],
                'sanic_checktype': tipo,
                # MatchedCheck se enlaza en F3, no aca.
            }))
    res, tiempos['declaredchecks'] = run_batches(reqs, 'creando DeclaredChecks',
                                                 a.chunk, a.workers)
    n_chk_ok = sum(1 for (st, cid, err) in res if cid)
    print(f'  DeclaredChecks creados: {n_chk_ok}/{len(reqs)}')

    dt = time.perf_counter() - t_ini
    print('\n== RESUMEN F1 ==')
    for k, v in tiempos.items():
        print(f'  {k:20} {v:7.1f}s')
    total = len(decls) + len(dbags) + len(ddeps) + n_chk_ok
    print(f'  TOTAL {dt:.1f}s · {total} escrituras · {total / dt if dt else 0:.1f} op/s')
    return {'declaraciones': len(decls), 'dbags': len(dbags),
            'ddeps': len(ddeps), 'dchecks': n_chk_ok}


# ===========================================================================
# F2 — MESAS, SESIONES HISTORICAS Y SOLICITUDES
# ===========================================================================
def seed_sessions(a, inv, usrs):
    print('\n' + '=' * 78)
    print('F2 — MESAS · SESIONES HISTORICAS · SOLICITUDES')
    print('=' * 78)
    t_ini = time.perf_counter()
    tiempos = {}
    me = whoami()

    puede_backdate, meta_bd = metadata_backdate()
    backdate = puede_backdate and not a.no_backdate
    print(f'  overriddencreatedon escribible en Create: {puede_backdate} '
          f'-> retro-fechado {"ACTIVO" if backdate else "DESACTIVADO"}')
    if not puede_backdate:
        print(f'    (metadata: {meta_bd})')

    seq = {
        'desk': next_correlativo(marcadas(ES_DESK, TAG_DESK, 'sanic_name'), TAG_DESK),
        'sess': next_correlativo(marcadas(ES_SESS, TAG_SESS, 'sanic_name'), TAG_SESS),
        'apr': next_correlativo(marcadas(ES_APR, TAG_APR, 'sanic_name'), TAG_APR),
    }

    # -------- 1) mesas --------------------------------------------------------
    # UNA de las mesas tiene Cajero = el propio actor (SP). Es la que habilita el
    # camino (a) de solicitudes historicas RESUELTAS: RecountingSessionAutoApprove
    # las aprueba EN EL CREATE cuando la mesa de la sesion es del solicitante.
    print('\n== 1) MESAS (CountingDesks) ==')
    # --resume: las mesas SRC-MESA* que ya existen se REUSAN como contenedor
    # (incluida la 'propia' del actor, que es la que habilita el auto-approve).
    previas = []
    if a.resume:
        for r in marcadas(ES_DESK, TAG_DESK, 'sanic_name,_sanic_cajero_value'):
            previas.append({'id': r[PK[ES_DESK]], 'name': r.get('sanic_name'),
                            'user': r.get('_sanic_cajero_value'),
                            'propia': (r.get('_sanic_cajero_value') or '').lower() == me.lower()})
        print(f'  [resume] mesas SRC-MESA* reusadas: {len(previas)} '
              f'(propias del actor: {sum(1 for d in previas if d["propia"])})')
    falta_desk = etapa('CountingDesks', len(previas), a.desks, a.resume)
    reqs, meta = [], []
    for i in range(falta_desk):
        nombre = f'{TAG_DESK}{seq["desk"]:04d}'
        seq['desk'] += 1
        propia = (i == 0 and not any(d['propia'] for d in previas))
        u = me if propia else random.choice(usrs)['systemuserid']
        reqs.append(('POST', f'/{ES_DESK}', {
            'sanic_name': nombre,
            'sanic_Cajero@odata.bind': f'/{ES_USER}({u})',
            'sanic_machineserialnumber': f'{MARK}MAQ-{random.randint(0, 999999):06d}',
            'sanic_enable': True,
        }))
        meta.append((nombre, u, propia))
    res, tiempos['mesas'] = run_batches(reqs, 'creando mesas', a.chunk, min(a.workers, 3))
    desks = list(previas)
    for (nombre, u, propia), (st, did, err) in zip(meta, res):
        if err or not did:
            continue
        desks.append({'id': did, 'name': nombre, 'user': u, 'propia': propia})
    print(f'  mesas creadas: {len(desks)}/{len(meta)}  '
          f'(propias del actor: {sum(1 for d in desks if d["propia"])})')
    if not desks:
        raise RuntimeError('no se creo ninguna mesa: CountingDesk es requerido por la sesion')
    mesa_propia = next((d for d in desks if d['propia']), None)

    # -------- 2) sesiones historicas -----------------------------------------
    # Confirmed = true se ESCRIBE aca a proposito: no hay guard sobre el
    # Create/Update de la sesion (register-recounting-guard.sh solo registra el
    # Delete), y la sesion historica sin Confirmed seria una sesion que nunca
    # existio. La app NO la escribe; el seeder materializa el estado final.
    print('\n== 2) SESIONES HISTORICAS ==')
    ya_sess = contar_marcadas(ES_SESS, TAG_SESS) if a.resume else 0
    falta_sess = etapa('RecountingSessions', ya_sess, a.sessions, a.resume)
    reqs, meta = [], []
    for _ in range(falta_sess):
        nombre = f'{TAG_SESS}{seq["sess"]:06d}'
        seq['sess'] += 1
        d = random.choice(desks)
        body = {
            'sanic_name': nombre,
            'sanic_Cashier@odata.bind': f'/{ES_USER}({d["user"]})',
            'sanic_CountingDesk@odata.bind': f'/{ES_DESK}({d["id"]})',
            'sanic_confirmed': True,
            'sanic_ConfirmedBy@odata.bind': f'/{ES_USER}({d["user"]})',
            'sanic_sessionforciblyclosed': False,
        }
        if backdate:
            body['overriddencreatedon'] = fecha_pasada(a.backdate_days)
        reqs.append(('POST', f'/{ES_SESS}', body))
        meta.append((nombre, d))
    res, tiempos['sesiones'] = run_batches(reqs, 'creando sesiones', a.chunk, a.workers)
    sesiones = []
    for (nombre, d), (st, sid, err) in zip(meta, res):
        if err or not sid:
            continue
        sesiones.append({'id': sid, 'name': nombre, 'desk': d})
    print(f'  sesiones creadas: {len(sesiones)}/{len(meta)}')

    # -------- 3) solicitudes historicas --------------------------------------
    # 🧨 ReceptionGuard.EvaluateApprovalCreate: NINGUNA solicitud nace resuelta,
    # salvo "Crear sesion de recuento" aprobada por el atajo de mesa propia — y
    # ese estampado lo hace el PLUGIN, no el cliente. Asi que:
    #   - las de tipo RT_SESION que apuntan a una sesion de la MESA PROPIA nacen
    #     RESUELTAS solitas (el plugin las auto-aprueba en el Create);
    #   - las otras tres nacen PENDIENTES;
    #   - --resolve-approvals apaga TEMPORALMENTE el unico step de Update de
    #     solicitudes y las resuelve en lote (opt-in, try/finally).
    print('\n== 3) SOLICITUDES HISTORICAS ==')
    ya_apr = contar_marcadas(ES_APR, TAG_APR) if a.resume else 0
    falta_apr = etapa('ApprovalRequests historicas', ya_apr, a.approvals, a.resume)
    if a.resume and falta_apr and not sesiones:
        # la etapa de solicitudes necesita sesiones como recurso: si en este run
        # no se creo ninguna (porque ya estaban), se reusan las SRC-S* existentes
        por_id = {d['id'].lower(): d for d in desks}
        for r in marcadas(ES_SESS, TAG_SESS, 'sanic_name,_sanic_countingdesk_value'):
            sesiones.append({'id': r[PK[ES_SESS]], 'name': r.get('sanic_name'),
                             'desk': por_id.get((r.get('_sanic_countingdesk_value') or '').lower())})
        print(f'  [resume] sesiones SRC-S* reusadas como recurso: {len(sesiones)}')
    sesiones_propias = [s for s in sesiones if s['desk'] and s['desk']['propia']]
    bags_ref = bags_boveda('sanic_bagid,sanic_receiptidmirror')
    reqs, meta = [], []
    tipos = [RT_SESION, RT_SIN_DECL, RT_CON_DIF, RT_RECONT_SIN_DECL]
    for i in range(falta_apr):
        nombre = f'{TAG_APR}{seq["apr"]:06d}'
        seq['apr'] += 1
        tipo = tipos[i % 4]
        b = random.choice(bags_ref) if bags_ref else None
        tula = (b or {}).get('sanic_bagid', 'ST000000')
        planilla = (b or {}).get('sanic_receiptidmirror')
        if tipo == RT_SESION:
            # Se prefiere una sesion de la MESA PROPIA: el plugin la auto-aprueba.
            s = random.choice(sesiones_propias or sesiones) if sesiones else None
            if not s:
                continue
            recursos = {'RecountingSessionGUID': s['id']}
            cajero = (s['desk'] or {}).get('name', 'Mesa')
            body = {
                'sanic_name': nombre,
                'sanic_requesttype': tipo,
                'sanic_bagnumber': tula,
                'sanic_requestdetails': html_solicitud(
                    'Solicitud de mesa de recuento', tula, planilla, 'Cajero', cajero),
                'sanic_requestresources': json.dumps(recursos),
            }
        else:
            recursos = {'BagGUID': str(uuid.uuid4())}
            if tipo == RT_SIN_DECL and sesiones:
                recursos = {'BagGUID': str(uuid.uuid4()),
                            'RecountingSessionGUID': random.choice(sesiones)['id']}
            body = {
                'sanic_name': nombre,
                'sanic_requesttype': tipo,
                'sanic_bagnumber': tula,
                'sanic_requestdetails': html_solicitud(tipo, tula, planilla, 'Cajero',
                                                       random.choice(desks)['name']),
                'sanic_requestresources': json.dumps(recursos),
            }
        # ApprovalStatus NUNCA se manda: nacer resuelta esta PROHIBIDO por el guard.
        reqs.append(('POST', f'/{ES_APR}', body))
        meta.append((nombre, tipo))
    res, tiempos['solicitudes'] = run_batches(reqs, 'creando solicitudes', a.chunk, a.workers)
    aprobaciones = []
    for (nombre, tipo), (st, aid, err) in zip(meta, res):
        if err or not aid:
            continue
        aprobaciones.append({'id': aid, 'name': nombre, 'tipo': tipo})
    print(f'  solicitudes creadas: {len(aprobaciones)}/{len(meta)}')

    # cuantas quedaron RESUELTAS solas (auto-approve del plugin)
    auto = count_of(ES_APR, f"startswith(sanic_name,'{TAG_APR}') and sanic_approvalstatus ne null")
    print(f'  RESUELTAS por el auto-approve del plugin (sin tocar un step): {auto}')

    if a.resolve_approvals:
        tiempos['resolver'] = resolver_solicitudes(a, aprobaciones, me)

    dt = time.perf_counter() - t_ini
    print('\n== RESUMEN F2 ==')
    for k, v in tiempos.items():
        print(f'  {k:20} {v:7.1f}s')
    total = len(desks) + len(sesiones) + len(aprobaciones)
    print(f'  TOTAL {dt:.1f}s · {total} escrituras · {total / dt if dt else 0:.1f} op/s')
    return {'mesas': len(desks), 'sesiones': len(sesiones),
            'solicitudes': len(aprobaciones), 'auto_aprobadas': auto,
            'backdate': backdate}


def resolver_solicitudes(a, aprobaciones, me):
    """OPT-IN: apaga el UNICO step de Update de solicitudes y las resuelve.

    ReceptionGuard.EvaluateApprovalUpdate exige resolutor != creador Y
    pertenencia al grupo AAD aprobador. El seeder es UNA sola identidad y la
    creadora de todo, asi que no puede satisfacer ninguna de las dos. En vez de
    pedir una excepcion permanente por identidad en el guard (que seria una
    llave maestra), se apaga UN paso concreto, se resuelve y se restaura —
    exactamente el contrato del --clean.
    """
    print('\n== 3.b) RESOLUCION HISTORICA (--resolve-approvals) ==')
    todos = leer_steps()
    s = todos.get(STEP_UPD_SOLICITUD)
    if not s:
        print(f'  !! no existe el step {STEP_UPD_SOLICITUD}: se omite', file=sys.stderr)
        return 0.0
    pendientes = [ap for ap in aprobaciones]
    mostrar_steps('step ANTES:', (STEP_UPD_SOLICITUD,), todos)
    dt = 0.0
    try:
        set_step(s['sdkmessageprocessingstepid'], False)
        mostrar_steps('step DURANTE:', (STEP_UPD_SOLICITUD,))
        reqs = []
        for ap in pendientes:
            rechazo = random.random() < a.reject_ratio
            body = {
                'sanic_approvalstatus': APR_RECHAZADO if rechazo else APR_APROBADO,
                'sanic_SolvedBy@odata.bind': f'/{ES_USER}({me})',
                'sanic_resolutiondatetime': fecha_pasada(a.backdate_days),
            }
            if rechazo:
                body['sanic_statusreason'] = 'Rechazada por el seeder de estres (SRC)'
            reqs.append(('PATCH', f'/{ES_APR}({ap["id"]})', body))
        _, dt = run_batches(reqs, 'resolviendo solicitudes', a.chunk, a.workers)
    finally:
        ok = reactivar([s])
        print('  step de Update de solicitudes REACTIVADO' if ok
              else '  !! STEP NO CONFIRMADO — revisar a mano')
    return dt


# ===========================================================================
# F3 — RECONTEO MASIVO
# ===========================================================================
def _f3_rehidratar(a):
    """--resume de F3: reconstruye el estado en memoria desde lo YA sembrado.

    Devuelve la MISMA forma que produce el camino normal (`abiertas`, con
    'deps' colgando de cada tula), mas lo que hace falta para decidir deltas:
    montos actuales del deposito, efectivo REAL del arqueo ya sembrado,
    cheques por deposito y estado de cada tula/detalle.
    Todo con queries paginadas por marcador: cero N+1, cero contadores ciegos.
    """
    print('\n== 0) RESUME — rehidratando F3 desde DEV ==')
    sdets = get_all(f'/{ES_SDET}?$select=sanic_name,_sanic_bagid_value,'
                    f'_sanic_recountingsessionid_value,sanic_recountingsessiondetailstatus'
                    f'&$filter=' + urllib.parse.quote(f"startswith(sanic_name,'{TAG_SDET}')"))
    bag_rows = get_all(
        f'/{ES_BAG}?$select={PK[ES_BAG]},sanic_bagid,sanic_bagstatus,sanic_receiptidmirror,'
        f'_sanic_receptionid_value,_sanic_recountingsession_value,'
        f'_sanic_bagdeclarationid_value&$filter='
        + urllib.parse.quote(f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') and "
                             f'sanic_bagstatus ne {BAG_BOVEDA}'))
    por_bag = {r[PK[ES_BAG]].lower(): r for r in bag_rows}
    deps = get_all(f'/{ES_DEP}?$select=sanic_name,_sanic_bagid_value,sanic_currency,'
                   f'sanic_cashamount,sanic_baclocalcheckamount,sanic_otherlocacheckamount,'
                   f'_sanic_relateddeclareddeposit_value&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DEP}')"))
    cmds = get_all(f'/{ES_CMD}?$select=_sanic_relateddepositrecountingid_value,'
                   f'sanic_denomination,sanic_quantity&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{TAG_CMD}')"))
    chks = get_all(f'/{ES_CHK}?$select=sanic_name,_sanic_relatedrecounterdepositid_value,'
                   f'sanic_checkamount,sanic_checktypes,sanic_checkstatus,'
                   f'_sanic_relateddeclaredcheck_value&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{TAG_CHK}')"))
    cmd_por_dep = agrupar(cmds, '_sanic_relateddepositrecountingid_value')
    chk_por_dep = agrupar(chks, '_sanic_relatedrecounterdepositid_value')
    dep_por_bag = agrupar(deps, '_sanic_bagid_value')

    abiertas = []
    sin_tula = 0
    for sd in sorted(sdets, key=lambda r: r.get('sanic_name') or ''):
        b = por_bag.get((sd.get('_sanic_bagid_value') or '').lower())
        if not b:
            sin_tula += 1
            continue
        t = {'bag': b,
             'sesion': {PK[ES_SESS]: b.get('_sanic_recountingsession_value')
                        or sd.get('_sanic_recountingsessionid_value')},
             'dbag': b.get('_sanic_bagdeclarationid_value'),
             'sdet': sd[PK[ES_SDET]],
             'sdet_status': sd.get('sanic_recountingsessiondetailstatus'),
             'deps': []}
        for d in sorted(dep_por_bag.get(b[PK[ES_BAG]].lower(), []),
                        key=lambda r: r.get('sanic_name') or ''):
            did = d[PK[ES_DEP]]
            filas = cmd_por_dep.get(did.lower(), [])
            cheques = chk_por_dep.get(did.lower(), [])
            t['deps'].append({
                'id': did, 'name': d.get('sanic_name'), 'cur': d.get('sanic_currency'),
                'dd': None, 'dd_id': d.get('_sanic_relateddeclareddeposit_value'),
                'fat': False,
                'cash_db': float(d.get('sanic_cashamount') or 0),
                'bac_db': float(d.get('sanic_baclocalcheckamount') or 0),
                'otros_db': float(d.get('sanic_otherlocacheckamount') or 0),
                'n_cmd': len(filas),
                'cash_arqueo': round(sum(float(r.get('sanic_denomination') or 0)
                                         * int(r.get('sanic_quantity') or 0)
                                         for r in filas), 2),
                'checks_db': cheques, 'tula': t,
            })
        abiertas.append(t)
    if sin_tula:
        print(f'  !! {sin_tula} detalles de sesion cuya tula volvio a boveda: se ignoran',
              file=sys.stderr)
    est = {}
    for t in abiertas:
        est[t['bag'].get('sanic_bagstatus')] = est.get(t['bag'].get('sanic_bagstatus'), 0) + 1
    print(f'  tulas rehidratadas: {len(abiertas)} · depositos {len(deps)} · '
          f'arqueo {len(cmds)} · cheques {len(chks)}')
    print('  estado de esas tulas: ' + ', '.join(
        f'{BAG_LABEL.get(k, k)}={v}' for k, v in sorted(est.items(), key=lambda kv: str(kv[0]))))
    return abiertas


def seed_recount(a, inv, usrs, den):
    print('\n' + '=' * 78)
    print('F3 — RECONTEO MASIVO (el orden ES la legalidad)')
    print('=' * 78)
    t_ini = time.perf_counter()
    tiempos = {}
    if not den:
        raise RuntimeError('CH-Tbl-MoneyDenominations no tiene denominaciones habilitadas: '
                           'sin ellas el arqueo seria inventado')

    # sesiones y mesas de este seeder (las historicas sirven de contenedor)
    sesiones = marcadas(ES_SESS, TAG_SESS, 'sanic_name')
    if not sesiones:
        raise RuntimeError('no hay sesiones SRC-S*: correr antes --only-sessions')
    dbags = marcadas(ES_DBAG, TAG_DBAG, 'sanic_name,sanic_bagnumber')
    dbag_por_numero = {r['sanic_bagnumber']: r[PK[ES_DBAG]] for r in dbags
                       if r.get('sanic_bagnumber')}
    ddeps_por_dbag = {}
    for r in get_all(f'/{ES_DDEP}?$select=sanic_name,_sanic_declaredbag_value,sanic_currency,'
                     f'sanic_cashamount&$filter='
                     + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DDEP}')")):
        ddeps_por_dbag.setdefault((r.get('_sanic_declaredbag_value') or '').lower(), []).append(r)
    dchecks_por_ddep = {}
    for r in get_all(f'/{ES_DCHK}?$select=sanic_name,_sanic_declareddepositid_value,'
                     f'sanic_checkamount,sanic_checkcurrency,sanic_checktype,sanic_checknumber'
                     f'&$filter=' + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DCHK}')")):
        dchecks_por_ddep.setdefault(
            (r.get('_sanic_declareddepositid_value') or '').lower(), []).append(r)

    # --resume: lo ya sembrado se rehidrata ANTES de decidir cualquier delta
    previas = _f3_rehidratar(a) if a.resume else []
    for t in previas:                       # el enlace al deposito declarado ya esta en DB
        for d in t['deps']:
            if d['dd_id']:
                d['dd'] = {PK[ES_DDEP]: d['dd_id']}

    bags = bags_boveda('sanic_bagid,_sanic_receptionid_value,sanic_receiptidmirror')
    falta_tulas = etapa('tulas abiertas (RecountingSessionDetail)', len(previas),
                        a.recount_bags, a.resume)
    n = min(falta_tulas, len(bags))
    objetivo = random.sample(bags, n)
    seq = {
        'sdet': next_correlativo(marcadas(ES_SDET, TAG_SDET, 'sanic_name'), TAG_SDET),
        'dep': next_correlativo(marcadas(ES_DEP, TAG_DEP, 'sanic_name'), TAG_DEP),
        'cmd': next_correlativo(marcadas(ES_CMD, TAG_CMD, 'sanic_name'), TAG_CMD),
        'chk': next_correlativo(marcadas(ES_CHK, TAG_CHK, 'sanic_name'), TAG_CHK),
        'apr': next_correlativo(marcadas(ES_APR, TAG_APR, 'sanic_name'), TAG_APR),
    }
    print(f'  tulas en boveda disponibles: {len(bags)} · a recontar: {n}')
    print(f'  sesiones SRC-S* disponibles: {len(sesiones)} · '
          f'DeclaredBags enlazables: {len(dbag_por_numero)}')

    # -------- 1) APERTURA: boveda -> Asignada a cajero -----------------------
    # PRIMERO la tula, porque GuardRecounting rechaza cualquier hijo cuya tula
    # ancestro no este 'Asignada a cajero'. El mismo PATCH cuelga la sesion y la
    # declaracion (01 §2.4: la app escribe BagStatus + RecountingSession +
    # BagDeclarationID al abrir).
    print('\n== 1) APERTURA  Almacenada en boveda -> Asignada a cajero ==')
    reqs, meta = [], []
    for b in objetivo:
        s = random.choice(sesiones)
        body = {
            'sanic_bagstatus': BAG_CAJERO,
            'sanic_RecountingSession@odata.bind': f'/{ES_SESS}({s[PK[ES_SESS]]})',
        }
        dbid = dbag_por_numero.get(b['sanic_bagid'])
        if dbid:
            body['sanic_BagDeclarationID@odata.bind'] = f'/{ES_DBAG}({dbid})'
        reqs.append(('PATCH', f'/{ES_BAG}({b[PK[ES_BAG]]})', body))
        meta.append((b, s, dbid))
    res, tiempos['apertura'] = run_batches(reqs, 'abriendo tulas', a.chunk, a.workers)
    nuevas, rechazos = [], []
    for (b, s, dbid), (st, _, err) in zip(meta, res):
        if err:
            rechazos.append((b['sanic_bagid'], err))
            continue
        b['sanic_bagstatus'] = BAG_CAJERO       # el estado en memoria refleja el PATCH que paso
        nuevas.append({'bag': b, 'sesion': s, 'dbag': dbid, 'deps': []})
    print(f'  tulas abiertas: {len(nuevas)}/{len(meta)} · rechazadas por guard: {len(rechazos)}')
    for r in rechazos[:3]:
        print(f'      !! {r}', file=sys.stderr)
    if not nuevas and not previas:
        raise RuntimeError('el guard rechazo TODAS las aperturas: revisar la matriz de BagStatus')
    abiertas = previas + nuevas

    # -------- 2) RecountingSessionDetail (uno por tula, SIEMPRE) -------------
    # Con --resume las tulas rehidratadas YA tienen su detalle: solo van las nuevas.
    print('\n== 2) DETALLES DE SESION (uno por tula, las 4 rutas) ==')
    pendientes_sdet = [t for t in abiertas if not t.get('sdet')]
    if a.resume:
        print(f'  [resume] detalles ya existentes: {len(abiertas) - len(pendientes_sdet)} · '
              f'a crear: {len(pendientes_sdet)}')
    reqs = []
    for t in pendientes_sdet:
        nombre = f'{TAG_SDET}{seq["sdet"]:06d}'
        seq['sdet'] += 1
        reqs.append(('POST', f'/{ES_SDET}', {
            'sanic_name': nombre,
            'sanic_RecountingSessionId@odata.bind': f'/{ES_SESS}({t["sesion"][PK[ES_SESS]]})',
            'sanic_BagId@odata.bind': f'/{ES_BAG}({t["bag"][PK[ES_BAG]]})',
            'sanic_recountingsessiondetailstatus': DET_EN_PROGRESO,
        }))
    res, tiempos['detalles'] = run_batches(reqs, 'creando detalles de sesion', a.chunk, a.workers)
    for t, (st, sdid, err) in zip(pendientes_sdet, res):
        t['sdet'] = sdid if not err else None
        t['sdet_status'] = DET_EN_PROGRESO if t['sdet'] else None
    print(f'  detalles creados: {sum(1 for t in abiertas if t["sdet"])}/{len(abiertas)}')

    # -------- 3) DepositsRecountings -----------------------------------------
    # El espejo E-1 (RecountingSessionMirror) NO se manda: lo estampa
    # DepositSessionMirrorPlugin desde BagID.RecountingSession.
    print('\n== 3) DEPOSITOS RECONTADOS ==')
    reqs, meta = [], []
    # La tula GORDA de cheques es la del PRIMER deposito sembrado (SRC-DR000001):
    # el correlativo se asigna en orden de recorrido, asi que ese deposito ES el
    # 'fat' del run original. Con --resume se lo identifica por dato, no por indice.
    sin_deps = [t for t in abiertas if not t.get('deps')]
    fat_t = None
    if a.resume and previas:
        cand = [(d['name'], t, d) for t in previas for d in t['deps'] if d.get('name')]
        if cand:
            _, fat_t, fat_d = min(cand, key=lambda x: x[0])
            fat_d['fat'] = True
            print(f'  [resume] tula GORDA de cheques identificada por SRC-DR mas bajo: '
                  f'{fat_t["bag"]["sanic_bagid"]} (deposito {fat_d["name"]}, '
                  f'{len(fat_t["deps"])} depositos, {len(fat_d.get("checks_db") or [])} cheques)')
    if a.resume:
        print(f'  [resume] tulas SIN depositos (etapa a medias): {len(sin_deps)}')
    fat_idx = 0 if (sin_deps and fat_t is None) else None   # la primera tula es la GORDA
    for i, t in enumerate(sin_deps):
        declarados = ddeps_por_dbag.get((t['dbag'] or '').lower(), [])
        n_dep = random.randint(a.rdep_min, a.rdep_max)
        if i == fat_idx:
            n_dep = max(n_dep, a.fat_bag_deps)
        for k in range(n_dep):
            nombre = f'{TAG_DEP}{seq["dep"]:06d}'
            seq['dep'] += 1
            dd = declarados[k] if k < len(declarados) else None
            cur = dd['sanic_currency'] if dd else moneda_pesada()
            body = {
                'sanic_name': nombre,
                'sanic_BagID@odata.bind': f'/{ES_BAG}({t["bag"][PK[ES_BAG]]})',
                'sanic_currency': cur,
                'sanic_accountnumber': cuenta(),
                'sanic_transactionnumber': random.randint(1, 500),
                # montos: se rellenan abajo con lo contado + cheques
                'sanic_cashamount': 0, 'sanic_baclocalcheckamount': 0,
                # 🧨 SIN la 'l': en DepositsRecountings es OtherLocaCheckAmount
                'sanic_otherlocacheckamount': 0,
            }
            if dd:
                body['sanic_RelatedDeclaredDeposit@odata.bind'] = \
                    f'/{ES_DDEP}({dd[PK[ES_DDEP]]})'
            reqs.append(('POST', f'/{ES_DEP}', body))
            meta.append((t, cur, dd, i == fat_idx and k == 0))
    res, tiempos['depositos'] = run_batches(reqs, 'creando depositos', a.chunk, a.workers)
    deps = []
    for (t, cur, dd, es_fat), (st, did, err) in zip(meta, res):
        if err or not did:
            continue
        deps.append({'id': did, 'name': None, 'tula': t, 'cur': cur, 'dd': dd, 'fat': es_fat,
                     'cash_db': 0.0, 'bac_db': 0.0, 'otros_db': 0.0,
                     'n_cmd': 0, 'cash_arqueo': 0.0, 'checks_db': []})
        t.setdefault('deps', []).append(deps[-1])
    print(f'  depositos creados: {len(deps)}/{len(meta)}')
    # de aca en adelante las etapas trabajan sobre TODOS los depositos (los de
    # este run + los rehidratados), y cada una decide su delta por DATO.
    deps = [d for t in abiertas for d in t['deps']]
    print(f'  depositos en juego (nuevos + rehidratados): {len(deps)}')

    # -------- 4) CountedMoneyDetails (denominaciones REALES) -----------------
    # Quantity tiene MINIMO 1: el generador jamas produce ceros (01 §2.4).
    # DenominationType es TEXTO ("Billete"/"Moneda"), no el option set.
    # RESUME: 'cero filas de arqueo' es INEQUIVOCO (cmd_min >= 1 y k >= 1
    # siempre), asi que el delta son exactamente los depositos con n_cmd == 0.
    print('\n== 4) ARQUEO — CountedMoneyDetails ==')
    reqs, cash_por_dep = [], {}
    for d in deps:
        if d['n_cmd']:
            cash_por_dep[d['id']] = d['cash_arqueo']
    faltan_arqueo = [d for d in deps if not d['n_cmd']]
    if a.resume:
        print(f'  [resume] depositos con arqueo ya sembrado: {len(deps) - len(faltan_arqueo)} · '
              f'a arquear: {len(faltan_arqueo)}')
    for d in faltan_arqueo:
        cat = den.get(d['cur']) or den.get(CUR_NIO) or []
        if not cat:
            continue
        k = min(len(cat), random.randint(a.cmd_min, a.cmd_max))
        efectivo = 0.0
        for row in random.sample(cat, k):
            nombre = f'{TAG_CMD}{seq["cmd"]:07d}'
            seq['cmd'] += 1
            valor = float(row['sanic_value'])
            qty = random.randint(1, 60 if valor <= 100 else 12)
            efectivo += valor * qty
            reqs.append(('POST', f'/{ES_CMD}', {
                'sanic_name': nombre,
                'sanic_RelatedDepositRecountingID@odata.bind': f'/{ES_DEP}({d["id"]})',
                'sanic_denomination': valor,
                'sanic_denominationtype': DEN_TYPE_TEXT.get(row.get('sanic_type'), 'Billete'),
                'sanic_quantity': qty,
                # sanic_totalamount es CAMPO FORMULA: no se escribe.
            }))
        cash_por_dep[d['id']] = round(efectivo, 2)
    res, tiempos['arqueo'] = run_batches(reqs, 'creando arqueo', a.chunk, a.workers)
    print(f'  filas de arqueo creadas: {sum(1 for (st, x, e) in res if x)}/{len(reqs)}')

    # -------- 5) Checks (declarados enlazados + manuales) --------------------
    # Nacen SIN sanic_checkstatus: CheckGuard solo admite null o 'Recontado' en
    # el Create, y el marcado a Recontado va en el cierre.
    # El espejo E-2 (Bag) lo estampa CheckMirrorStampPlugin: no se manda.
    # RESUME — 'deposito con cero cheques' es AMBIGUO (el 50% de peso es cero
    # legitimo), asi que el delta NO se decide por eso: se usa la MARCA DE AGUA
    # del correlativo. Los cheques se emiten recorriendo los depositos en orden
    # de sanic_name, asi que el deposito de nombre MAS ALTO que ya tiene cheques
    # es la frontera: todo lo <= a el ya paso por esta etapa (con o sin cheques),
    # y solo lo > a el falta. Sin marca de agua (cero cheques en total) corre entera.
    print('\n== 5) CHEQUES RECONTADOS ==')
    reqs, meta = [], []
    bac_por_dep, otros_por_dep = {}, {}
    for d in deps:                                  # cheques YA sembrados: cuentan al monto
        for c in d['checks_db']:
            importe = float(c.get('sanic_checkamount') or 0)
            if c.get('sanic_checktypes') == CHK_BAC:
                bac_por_dep[d['id']] = round(bac_por_dep.get(d['id'], 0) + importe, 2)
            else:
                otros_por_dep[d['id']] = round(otros_por_dep.get(d['id'], 0) + importe, 2)
    faltan_chk = deps
    if a.resume:
        # DELTA = depositos con CERO cheques. Es la unica regla que sobrevive a un
        # corte con AGUJEROS (chunks paralelos que fallaron salteados), y no duplica
        # NUNCA: cada deposito que ya tiene cheques se deja intacto.
        # 🧨 LIMITACION HONESTA: 'cero cheques' tambien es un resultado legitimo del
        # generador (peso 50%), asi que un --resume sobre una etapa YA COMPLETA le
        # da una segunda tirada a esos depositos e infla el total. Es seguro
        # (no duplica filas) pero no es idempotente en el volumen.
        faltan_chk = [d for d in deps if not d['checks_db']]
        print(f'  [resume] depositos con cheques ya sembrados: {len(deps) - len(faltan_chk)} · '
              f'sin cheques (se les tira de nuevo): {len(faltan_chk)}')
        print('  [resume] OJO: "cero cheques" tambien es legitimo — sobre una etapa '
              'completa esto INFLARIA el total (no duplica, pero no es idempotente).')
        # La tula gorda se decide POR TULA, no por deposito: si ALGUNA tula ya
        # cruza el tope de cheques, la etapa gorda esta hecha y no se repite.
        # (Mirarlo por deposito hacia que un segundo deposito de la MISMA tula
        # gorda se llevara otros 600 cheques.)
        max_tula = max((sum(len(x['checks_db']) for x in t['deps']) for t in abiertas),
                       default=0)
        if max_tula >= a.fat_bag_checks:
            for d in faltan_chk:
                d['fat'] = False
            print(f'  [resume] tula GORDA ya sembrada ({max_tula} cheques en una tula): '
                  f'no se vuelve a engordar ninguna')
        elif not any(d['fat'] for d in faltan_chk):
            gorda = next((d for d in faltan_chk
                          if len(d['tula']['deps']) >= a.fat_bag_deps), None) \
                or (faltan_chk[0] if faltan_chk else None)
            if gorda is not None:
                gorda['fat'] = True
                print(f'  [resume] tula GORDA de cheques asignada: '
                      f'{gorda["tula"]["bag"]["sanic_bagid"]} (deposito {gorda["name"]})')
    for d in faltan_chk:
        declarados = dchecks_por_ddep.get(
            (d['dd'][PK[ES_DDEP]] or '').lower(), []) if d['dd'] else []
        if d['fat']:
            n_chk = a.fat_bag_checks
        else:
            n_chk = random.choices((0, 1, 2, 3, 6), weights=(50, 22, 14, 9, 5))[0]
        for j in range(n_chk):
            nombre = f'{TAG_CHK}{seq["chk"]:07d}'
            seq['chk'] += 1
            dc = declarados[j] if j < len(declarados) else None
            if dc:
                importe = float(dc['sanic_checkamount'])
                cur = dc['sanic_checkcurrency']
                tipo = CHK_BAC if dc.get('sanic_checktype') == CHK_BAC else CHK_OTROS
                numero = dc.get('sanic_checknumber') or f'{random.randint(0, 99999999):08d}'
            else:
                importe = round(random.uniform(1, 60_000), 2)      # min 1
                cur = d['cur']
                tipo = CHK_BAC if j % 2 == 0 else CHK_OTROS
                numero = f'{random.randint(0, 99999999):08d}'
            if tipo == CHK_BAC:
                bac_por_dep[d['id']] = round(bac_por_dep.get(d['id'], 0) + importe, 2)
            else:
                otros_por_dep[d['id']] = round(otros_por_dep.get(d['id'], 0) + importe, 2)
            # 🧨 BUG DE PLUGIN (2026-08-17): el cheque NACE SIN
            # sanic_RelatedDeclaredCheck a proposito. CheckMirrorStampPlugin corre
            # en PRE-operation (stage 20) del Create y su estampa B3 hace
            # system.Update(declarado, MatchedCheck = <id del cheque>) — un lookup
            # a una fila que TODAVIA NO EXISTE. Dataverse lo rechaza siempre:
            #   0x80040265 "No se pudieron estampar los espejos del cheque:
            #   Entity 'sanic_CHTblChecks' With Id = <guid> Does Not Exist"
            # El enlace va en un PATCH posterior (etapa 5.b), que es donde el
            # plugin SI puede estampar. No se apaga ningun paso.
            reqs.append(('POST', f'/{ES_CHK}', {
                'sanic_name': nombre,
                'sanic_checknumber': numero,
                'sanic_checkamount': importe,
                'sanic_checkcurrency': cur,
                'sanic_checktypes': tipo,
                'sanic_RelatedReCounterDepositID@odata.bind': f'/{ES_DEP}({d["id"]})',
            }))
            meta.append((d, dc))
    res, tiempos['cheques'] = run_batches(reqs, 'creando cheques', a.chunk, a.workers)
    checks = []
    for (d, dc), (st, cid, err) in zip(meta, res):
        if err or not cid:
            continue
        checks.append({'id': cid, 'dep': d, 'dc': dc})
    print(f'  cheques creados: {len(checks)}/{len(meta)}')

    # -------- 5.b) enlace cheque -> cheque DECLARADO (Update, no Create) -----
    # Este PATCH es el que dispara la estampa B3 del espejo: el plugin escribe
    # DeclaredCheck.MatchedCheck solo. La etapa 6 verifica que lo haya hecho.
    print('\n== 5.b) ENLACE Check.RelatedDeclaredCheck (post-Create por el bug B3) ==')
    res_link, tiempos['enlace_declarado'] = run_batches(
        [('PATCH', f'/{ES_CHK}({c["id"]})',
          {'sanic_RelatedDeclaredCheck@odata.bind': f'/{ES_DCHK}({c["dc"][PK[ES_DCHK]]})'})
         for c in checks if c['dc']],
        'enlazando cheque -> declarado', a.chunk, a.workers)

    # -------- 6) enlace inverso del cheque declarado (MatchedCheck) ----------
    # 🧨 MatchedCheck, JAMAS DeclaredDepositID (bug D-6 de la v1).
    # RESUME: el enlace es INEQUIVOCO por dato (MatchedCheck null o no). Los
    # cheques rehidratados que apuntan a un declarado sin MatchedCheck se enlazan
    # igual que los nuevos: el PATCH es idempotente sobre el mismo par.
    # Se lee el estado FRESCO: si el plugin ya estampo (etapa 5.b), esta etapa
    # escribe CERO — y ese cero es la evidencia de que el espejo B3 funciona.
    print('\n== 6) ENLACE DeclaredChecks.MatchedCheck (relleno + evidencia B3) ==')
    sin_match = {r[PK[ES_DCHK]].lower() for r in get_all(
        f'/{ES_DCHK}?$select={PK[ES_DCHK]},_sanic_matchedcheck_value&$filter='
        + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DCHK}')"))
        if not r.get('_sanic_matchedcheck_value')}
    pares = [(c['dc'][PK[ES_DCHK]], c['id']) for c in checks
             if c['dc'] and (c['dc'][PK[ES_DCHK]] or '').lower() in sin_match]
    previos = [(c.get('_sanic_relateddeclaredcheck_value'), c[PK[ES_CHK]])
               for d in deps for c in d['checks_db']
               if c.get('_sanic_relateddeclaredcheck_value')
               and (c.get('_sanic_relateddeclaredcheck_value') or '').lower() in sin_match]
    print(f'  DeclaredChecks SIN MatchedCheck ahora: {len(sin_match)} · '
          f'que el plugin NO estampo y hay que rellenar: {len(pares)} de este run '
          f'+ {len(previos)} de cheques previos')
    pares += previos
    res, tiempos['matched'] = run_batches(
        [('PATCH', f'/{ES_DCHK}({dcid})', {'sanic_MatchedCheck@odata.bind': f'/{ES_CHK}({cid})'})
         for dcid, cid in pares], 'enlazando cheques declarados', a.chunk, a.workers)

    # -------- 7) montos del deposito (efectivo contado + cheques) ------------
    # RESUME: se compara el monto CALCULADO (arqueo real + cheques reales, todo
    # leido de DEV) contra el que tiene la fila. Solo se PATCHea lo que difiere:
    # cero escrituras redundantes, cero totales inventados.
    print('\n== 7) MONTOS DE LOS DEPOSITOS ==')
    reqs, iguales = [], 0
    for d in deps:
        cash = cash_por_dep.get(d['id'], 0)
        bac = bac_por_dep.get(d['id'], 0)
        otros = otros_por_dep.get(d['id'], 0)
        if a.resume and (abs(cash - d['cash_db']) < 0.005 and abs(bac - d['bac_db']) < 0.005
                         and abs(otros - d['otros_db']) < 0.005):
            iguales += 1
            continue
        reqs.append(('PATCH', f'/{ES_DEP}({d["id"]})', {
            'sanic_cashamount': cash,
            'sanic_baclocalcheckamount': bac,
            'sanic_otherlocacheckamount': otros,
        }))
    if a.resume:
        print(f'  [resume] depositos con el monto ya correcto: {iguales} · a corregir: {len(reqs)}')
    res, tiempos['montos'] = run_batches(reqs, 'actualizando montos', a.chunk, a.workers)
    # enlace inverso del deposito declarado
    pares_dd = [(d['dd'][PK[ES_DDEP]], d['id']) for d in deps if d['dd']]
    if a.resume:
        ya_enlazados = {(r.get('_sanic_relatedrecounteddeposit_value') or '').lower()
                        for r in get_all(
                            f'/{ES_DDEP}?$select=_sanic_relatedrecounteddeposit_value&$filter='
                            + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DDEP}')"))
                        if r.get('_sanic_relatedrecounteddeposit_value')}
        antes = len(pares_dd)
        pares_dd = [(dd, dep) for dd, dep in pares_dd if dep.lower() not in ya_enlazados]
        print(f'  [resume] DeclaredDeposits ya enlazados a su recontado: {len(ya_enlazados)} · '
              f'enlaces a escribir: {len(pares_dd)}/{antes}')
    run_batches([('PATCH', f'/{ES_DDEP}({dd})',
                  {'sanic_RelatedRecountedDeposit@odata.bind': f'/{ES_DEP}({dep})'})
                 for dd, dep in pares_dd], 'enlazando depositos declarados', a.chunk, a.workers)

    # -------- 8) CIERRE ------------------------------------------------------
    # 🧨 NO se escriben TotalNIO/USD/EUR ni CH-Tbl-Transactions: los produce
    # BagRecountClosurePlugin al ENTRAR a Recontada / Pendiente-recuento.
    print('\n== 8) CIERRE (el guard recalcula totales y crea Transactions) ==')
    con_deps = [t for t in abiertas if t.get('deps')]
    random.shuffle(con_deps)
    # RESUME: el estado de la TULA es el dato. Las que ya estan Recontada /
    # Pendiente-recuento cuentan contra el objetivo y NO se vuelven a tocar
    # (re-PATCHear una Recontada la rechaza el guard: es estado terminal).
    ya_rec = [t for t in con_deps if t['bag'].get('sanic_bagstatus') == BAG_RECONTADA]
    ya_pen = [t for t in con_deps if t['bag'].get('sanic_bagstatus') == BAG_PEND_RECUENTO]
    cerrables = [t for t in con_deps if t['bag'].get('sanic_bagstatus') == BAG_CAJERO]
    if a.resume:
        n_limpias = etapa('cierres a Recontada', len(ya_rec), a.close_clean, True)
        n_dif = etapa('cierres a Pendiente-recuento', len(ya_pen), a.close_diff, True)
    else:
        cerrables, n_limpias, n_dif = con_deps, a.close_clean, a.close_diff
    limpias = cerrables[:n_limpias]
    con_dif = cerrables[n_limpias:n_limpias + n_dif]
    abiertas_quedan = cerrables[n_limpias + n_dif:] + \
        [t for t in abiertas if not t.get('deps')]
    print(f'  a Recontada: {len(limpias)} · a Pendiente-recuento: {len(con_dif)} · '
          f'quedan abiertas: {len(abiertas_quedan)}')

    res, tiempos['cierre_limpio'] = run_batches(
        [('PATCH', f'/{ES_BAG}({t["bag"][PK[ES_BAG]]})', {'sanic_bagstatus': BAG_RECONTADA})
         for t in limpias], 'cerrando tulas (Recontada)', a.chunk, min(a.workers, 4))
    ok_limpias = [t for t, (st, _, e) in zip(limpias, res) if not e]
    rechazos += [(t['bag']['sanic_bagid'], e) for t, (st, _, e) in zip(limpias, res) if e]

    res, tiempos['cierre_dif'] = run_batches(
        [('PATCH', f'/{ES_BAG}({t["bag"][PK[ES_BAG]]})', {'sanic_bagstatus': BAG_PEND_RECUENTO})
         for t in con_dif], 'cerrando tulas (Pendiente-recuento)', a.chunk, min(a.workers, 4))
    ok_dif = [t for t, (st, _, e) in zip(con_dif, res) if not e]
    rechazos += [(t['bag']['sanic_bagid'], e) for t, (st, _, e) in zip(con_dif, res) if e]

    # detalle de sesion al estado final. Con --resume tambien se corrigen los
    # detalles de tulas que ya estaban cerradas pero cuyo detalle quedo en curso.
    cerradas_limpias = ok_limpias + ([t for t in ya_rec if t.get('sdet_status') != DET_CERRADO]
                                     if a.resume else [])
    cerradas_dif = ok_dif + ([t for t in ya_pen if t.get('sdet_status') != DET_CERRADO_DIF]
                             if a.resume else [])
    reqs = [('PATCH', f'/{ES_SDET}({t["sdet"]})',
             {'sanic_recountingsessiondetailstatus': DET_CERRADO})
            for t in cerradas_limpias if t.get('sdet')]
    reqs += [('PATCH', f'/{ES_SDET}({t["sdet"]})',
              {'sanic_recountingsessiondetailstatus': DET_CERRADO_DIF})
             for t in cerradas_dif if t.get('sdet')]
    run_batches(reqs, 'cerrando detalles de sesion', a.chunk, a.workers)

    # cheques -> Recontado: Update que toca UNICAMENTE sanic_checkstatus
    # (exencion touchesOnlyCheckStatus de RecountingGuard.IsWriteExempt).
    # RESUME: entran los cheques ya sembrados cuyo checkstatus sigue en null.
    ids_cerradas = {t['bag'][PK[ES_BAG]] for t in ok_limpias} | \
        ({t['bag'][PK[ES_BAG]] for t in ya_rec} if a.resume else set())
    a_marcar = [c['id'] for c in checks
                if c['dep']['tula']['bag'][PK[ES_BAG]] in ids_cerradas]
    if a.resume:
        a_marcar += [c[PK[ES_CHK]] for d in deps for c in d['checks_db']
                     if c.get('sanic_checkstatus') is None
                     and d['tula']['bag'][PK[ES_BAG]] in ids_cerradas]
    res, tiempos['cheques_recontado'] = run_batches(
        [('PATCH', f'/{ES_CHK}({cid})', {'sanic_checkstatus': CHK_RECONTADO})
         for cid in a_marcar], 'marcando cheques Recontado', a.chunk, a.workers)

    # -------- 9) solicitudes 'Tula con diferencia' ---------------------------
    # RESUME: la solicitud lleva el GUID REAL de la tula en RequestResources
    # (las historicas de F2 llevan un uuid inventado), asi que 'esta tula ya
    # tiene su solicitud' se decide por dato y no se duplica.
    print('\n== 9) SOLICITUDES DE DIFERENCIA ==')
    pendientes_apr = list(ok_dif)
    if a.resume:
        con_guid = set()
        for r in marcadas(ES_APR, TAG_APR, 'sanic_name,sanic_requesttype,sanic_requestresources'):
            if r.get('sanic_requesttype') != RT_CON_DIF:
                continue
            try:
                g = (json.loads(r.get('sanic_requestresources') or '{}').get('BagGUID') or '')
            except (ValueError, TypeError):
                g = ''
            if g:
                con_guid.add(g.lower())
        pendientes_apr = [t for t in (ok_dif + ya_pen)
                          if t['bag'][PK[ES_BAG]].lower() not in con_guid]
        print(f'  [resume] solicitudes SRC-AR* de diferencia con BagGUID en RequestResources: '
              f'{len(con_guid)} (las historicas de F2 llevan un GUID inventado, no matchean) · '
              f'tulas Pendiente-recuento sin solicitud: {len(pendientes_apr)}')
    reqs = []
    for t in pendientes_apr:
        nombre = f'{TAG_APR}{seq["apr"]:06d}'
        seq['apr'] += 1
        reqs.append(('POST', f'/{ES_APR}', {
            'sanic_name': nombre,
            'sanic_requesttype': RT_CON_DIF,
            'sanic_bagnumber': t['bag']['sanic_bagid'],
            'sanic_requestdetails': html_solicitud(
                'Tula con diferencia', t['bag']['sanic_bagid'],
                t['bag'].get('sanic_receiptidmirror'), 'Cajero', 'Mesa',
                extra='<tr><td>Origen</td><td>seed de estres SRC</td></tr>'),
            'sanic_requestresources': json.dumps({'BagGUID': t['bag'][PK[ES_BAG]]}),
            # ApprovalStatus NO se manda: nace PENDIENTE (01 §5.5).
        }))
    res, tiempos['solicitudes_dif'] = run_batches(reqs, 'creando solicitudes de diferencia',
                                                  a.chunk, a.workers)

    dt = time.perf_counter() - t_ini
    print('\n== RESUMEN F3 ==')
    for k, v in tiempos.items():
        print(f'  {k:20} {v:7.1f}s')
    print(f'  TOTAL {dt:.1f}s')
    print(f'  RECHAZOS DEL GUARD: {len(rechazos)}')
    for r in rechazos[:10]:
        print(f'    !! {r}')
    return {'abiertas': len(abiertas), 'depositos': len(deps), 'cheques': len(checks),
            'recontadas': len(ok_limpias), 'con_diferencia': len(ok_dif),
            'siguen_abiertas': len(abiertas_quedan), 'rechazos': len(rechazos)}


# ===========================================================================
# F4 — LECTURAS DE CONTADORA
# ===========================================================================
def seed_xml(a, den):
    print('\n' + '=' * 78)
    print('F4 — LECTURAS DE CONTADORA (XMLReadings / XMLReadingDetails)')
    print('=' * 78)
    t_ini = time.perf_counter()
    tiempos = {}

    detalles = marcadas(ES_SDET, TAG_SDET, 'sanic_name')
    if not detalles:
        raise RuntimeError('no hay RecountingSessionDetails SRC-SD*: correr antes --only-recount')
    seq = {
        'xml': next_correlativo(marcadas(ES_XML, TAG_XML, 'sanic_name'), TAG_XML),
        'xmd': next_correlativo(marcadas(ES_XMD, TAG_XMD, 'sanic_name'), TAG_XMD),
    }
    print(f'  detalles de sesion disponibles: {len(detalles)}')

    # RESUME: las lecturas ya sembradas se rehidratan para (a) descontar del
    # objetivo y (b) detectar las que quedaron SIN detalles (etapa 2 a medias).
    # 'lectura con cero detalles' es INEQUIVOCO: xmd_min >= 1 siempre.
    lecturas_previas = []
    if a.resume:
        xmls = marcadas(ES_XML, TAG_XML, 'sanic_name,sanic_isactive')
        xmds = get_all(f'/{ES_XMD}?$select=_sanic_xmlreading_value&$filter='
                       + urllib.parse.quote(f"startswith(sanic_name,'{TAG_XMD}')"))
        por_xml = agrupar(xmds, '_sanic_xmlreading_value')
        lecturas_previas = [(r[PK[ES_XML]], bool(r.get('sanic_isactive')),
                             len(por_xml.get(r[PK[ES_XML]].lower(), []))) for r in xmls]
        huerfanas = [x for x in lecturas_previas if x[2] == 0]
        print(f'  [resume] lecturas ya sembradas: {len(lecturas_previas)} · '
              f'detalles ya sembrados: {len(xmds)} · lecturas SIN detalles: {len(huerfanas)}')

    # -------- 1) lecturas ----------------------------------------------------
    # IsActive MIXTO y REALISTA: varias lecturas por detalle, solo la ULTIMA
    # activa. Es el caso que Q17 tiene que sobrevivir (E-4b).
    print('\n== 1) XMLReadings ==')
    reqs, meta = [], []
    restantes = etapa('XMLReadings', len(lecturas_previas), a.xml_readings, a.resume)
    random.shuffle(detalles)
    i = 0
    while restantes > 0 and detalles:
        d = detalles[i % len(detalles)]
        i += 1
        k = min(restantes, random.choices((1, 2, 3), weights=(55, 30, 15))[0])
        for j in range(k):
            nombre = f'{TAG_XML}{seq["xml"]:06d}'
            seq['xml'] += 1
            activa = (j == k - 1)
            reqs.append(('POST', f'/{ES_XML}', {
                'sanic_name': nombre,
                'sanic_filename': f'{MARK}lectura-{seq["xml"]:06d}.xml',
                'sanic_RecountingSessionDetailId@odata.bind':
                    f'/{ES_SDET}({d[PK[ES_SDET]]})',
                'sanic_isactive': activa,
            }))
            meta.append(activa)
        restantes -= k
    res, tiempos['lecturas'] = run_batches(reqs, 'creando lecturas', a.chunk, a.workers)
    lecturas = [(xid, act) for act, (st, xid, err) in zip(meta, res) if xid]
    print(f'  lecturas creadas: {len(lecturas)}/{len(meta)}  '
          f'(activas {sum(1 for _, act in lecturas if act)})')
    if a.resume:
        # las huerfanas entran a la etapa 2 junto con las nuevas
        lecturas += [(xid, act) for xid, act, n in lecturas_previas if n == 0]
        print(f'  [resume] lecturas que pasan a la etapa de detalles: {len(lecturas)}')

    # -------- 2) detalles ----------------------------------------------------
    # Los espejos E-4a/E-4b (RecountingSessionDetailMirror + XMLReadingActiva)
    # NO se mandan: los estampa XmlReadingDetailMirrorPlugin en el Create.
    print('\n== 2) XMLReadingDetails ==')
    catalogo = [(float(r['sanic_value']), CUR_LABEL.get(c, 'NIO'))
                for c, rows in den.items() for r in rows]
    if not catalogo:
        catalogo = [(v, 'NIO') for v in (1000, 500, 200, 100, 50, 20, 10, 5, 1)]
    reqs = []
    for xid, _ in lecturas:
        k = random.randint(a.xmd_min, a.xmd_max)
        for _ in range(k):
            nombre = f'{TAG_XMD}{seq["xmd"]:07d}'
            seq['xmd'] += 1
            valor, cur = random.choice(catalogo)
            reqs.append(('POST', f'/{ES_XMD}', {
                'sanic_name': nombre,
                'sanic_XMLReading@odata.bind': f'/{ES_XML}({xid})',
                'sanic_denomination': valor,
                'sanic_quantity': random.randint(1, 80),
                # 🧨 Currency de XMLReadingDetails es TEXTO LIBRE, no picklist.
                'sanic_currency': cur,
                # sanic_subtotal es CAMPO FORMULA: no se escribe.
            }))
    res, tiempos['detalles'] = run_batches(reqs, 'creando detalles de lectura',
                                           a.chunk, a.workers)
    n_ok = sum(1 for (st, x, e) in res if x)
    dt = time.perf_counter() - t_ini
    print('\n== RESUMEN F4 ==')
    for k, v in tiempos.items():
        print(f'  {k:20} {v:7.1f}s')
    total = len(lecturas) + n_ok
    print(f'  detalles creados: {n_ok}/{len(reqs)}')
    print(f'  TOTAL {dt:.1f}s · {total} escrituras · {total / dt if dt else 0:.1f} op/s')
    return {'lecturas': len(lecturas), 'detalles': n_ok}


# ===========================================================================
# CLEAN
# ===========================================================================
def clean(a):
    print('\n' + '=' * 78)
    print('LIMPIEZA — orden INVERSO legal, con 6 steps apagados y restaurados')
    print('=' * 78)

    inventario_clean = {}
    for es, tag, label in SEMBRADAS:
        inventario_clean[es] = marcadas(es, tag, 'sanic_name')
        print(f'  {label:44} {len(inventario_clean[es])}')
    tulas = bags_marcadas_recuento()
    print(f'  {"tulas ST* fuera de boveda (a devolver)":44} {len(tulas)}')

    hay = any(inventario_clean.values()) or tulas
    todos, faltan = mostrar_steps('steps ANTES:', STEPS_CLEAN)
    if not hay:
        print('  nada marcado SRC-* que borrar')
        return
    if faltan:
        raise RuntimeError(
            'faltan steps del guard: ' + ', '.join(faltan) +
            ' — desplegar register-recounting-guard.sh / register-reception-guard.sh antes '
            'de limpiar (o el borrado quedaria a medias sin aviso)')
    pasos = [todos[n] for n in STEPS_CLEAN]

    ok = True
    try:
        for s in pasos:
            set_step(s['sdkmessageprocessingstepid'], False)
        print(f'  {len(pasos)} steps DESACTIVADOS (los espejos y BagRecountClosure NO se tocan)')
        mostrar_steps('steps DURANTE:', STEPS_CLEAN)

        # 0) asientos que creo el guard sobre NUESTROS depositos.
        #    sanic_chtbltransactions NO tiene step de Delete: se borran sin apagar nada.
        dep_ids = {r[PK[ES_DEP]].lower() for r in inventario_clean[ES_DEP]}
        if dep_ids:
            trx = [r for r in get_all(
                f'/{ES_TRX}?$select={PK[ES_TRX]},_sanic_relateddepositrecounting_value')
                if (r.get('_sanic_relateddepositrecounting_value') or '').lower() in dep_ids]
            print(f'\n  asientos CH-Tbl-Transactions de esos depositos: {len(trx)}')
            run_batches([('DELETE', f'/{ES_TRX}({r[PK[ES_TRX]]})', None) for r in trx],
                        'borrando asientos', a.chunk, a.workers)

        # 1) desenlazar lo que apunta a registros que se van a borrar
        print('\n  desenlazando lookups cruzados...')
        # DELETE .../<NavProperty>/$ref es la forma DOCUMENTADA de desasociar un
        # lookup single-valued y la unica que se comporta igual dentro de $batch.
        dchk = inventario_clean[ES_DCHK]
        run_batches([('DELETE', f'/{ES_DCHK}({r[PK[ES_DCHK]]})/sanic_MatchedCheck/$ref', None)
                     for r in dchk], 'limpiando MatchedCheck', a.chunk, a.workers)
        ddep = inventario_clean[ES_DDEP]
        run_batches([('DELETE',
                      f'/{ES_DDEP}({r[PK[ES_DDEP]]})/sanic_RelatedRecountedDeposit/$ref', None)
                     for r in ddep], 'limpiando RelatedRecountedDeposit', a.chunk, a.workers)

        # 2) devolver las tulas a boveda ANTES de borrar sesiones y declaraciones
        #    (de 'Recontada' no se vuelve con el guard puesto: por eso el step
        #     CashHub.GuardReception: Update de sanic_chtblbags esta apagado).
        if tulas:
            print('\n  devolviendo tulas a Almacenada en boveda...')
            run_batches([('PATCH', f'/{ES_BAG}({t[PK[ES_BAG]]})',
                          {'sanic_bagstatus': BAG_BOVEDA})
                         for t in tulas], 'devolviendo tulas a boveda', a.chunk, min(a.workers, 4))
            run_batches([('DELETE', f'/{ES_BAG}({t[PK[ES_BAG]]})/sanic_RecountingSession/$ref',
                          None) for t in tulas if t.get('_sanic_recountingsession_value')],
                        'desenlazando sesion de las tulas', a.chunk, a.workers)
            run_batches([('DELETE', f'/{ES_BAG}({t[PK[ES_BAG]]})/sanic_BagDeclarationID/$ref',
                          None) for t in tulas if t.get('_sanic_bagdeclarationid_value')],
                        'desenlazando declaracion de las tulas', a.chunk, a.workers)

        # 3) borrado en orden inverso legal
        for es, tag, label in SEMBRADAS:
            rows = inventario_clean[es]
            if not rows:
                continue
            run_batches([('DELETE', f'/{es}({r[PK[es]]})', None) for r in rows],
                        f'borrando {label}', a.chunk, a.workers)
    finally:
        ok = reactivar(pasos)
        print('  steps REACTIVADOS' if ok else '  !! GUARD NO CONFIRMADO — revisar a mano')

    print('\n  QUEDAN:')
    for es, tag, label in SEMBRADAS:
        print(f'    {label:44} {contar_marcadas(es, tag)}')
    print(f'    {"tulas ST* fuera de boveda":44} {len(bags_marcadas_recuento())}')
    mostrar_steps('\nESTADO FINAL DE LOS STEPS (deben quedar ACTIVOS):', STEPS_CLEAN)
    if not ok:
        sys.exit(2)


# ===========================================================================
# VERIFICACION — las consultas de la app v2 a escala
# ===========================================================================
def percentiles(xs):
    xs = sorted(xs)
    if not xs:
        return 0, 0
    return statistics.median(xs) * 1000, xs[min(len(xs) - 1, int(round(0.95 * (len(xs) - 1))))] * 1000


def timed_query(path, n=8):
    ts, first = [], None
    for _ in range(n):
        d, dt = req('GET', path, prefer='odata.maxpagesize=100', timed=True)
        ts.append(dt)
        if first is None:
            first = d
    return first, ts


def verify(a):
    print('\n' + '=' * 78)
    print('VERIFICACION — consultas de CH-App-Recounting v2 a escala')
    print('=' * 78)
    res = {}

    print('\n[1] VOLUMENES SEMBRADOS (paginados de verdad)')
    for es, tag, label in SEMBRADAS:
        n = contar_marcadas(es, tag)
        marca = '>2000 OK' if n > 2000 else ('>500 OK' if n > 500 else '')
        print(f'  {label:44} {n:7}  {marca}')
        res[label] = n

    print('\n[2] TULAS por estado de recuento (Q de scrTulas)')
    for est in (BAG_CAJERO, BAG_RECONTADA, BAG_PEND_RECUENTO, BAG_BOVEDA):
        n = count_of(ES_BAG, f'sanic_bagstatus eq {est}')
        _, ts = timed_query(f'/{ES_BAG}?$select=sanic_bagid,sanic_bagstatus,sanic_totalnio'
                            f'&$filter=sanic_bagstatus eq {est}&$top=100')
        p50, p95 = percentiles(ts)
        print(f'  {BAG_LABEL[est]:34} {n:7}  p50={p50:.0f}/p95={p95:.0f}ms')
        res[f'bags_{est}'] = n

    print('\n[3] ESPEJO E-1 RecountingSessionMirror (deposito -> sesion)')
    muestra = req('GET', f'/{ES_DEP}?$select={PK[ES_DEP]},sanic_name,'
                         f'_sanic_recountingsessionmirror_value,_sanic_bagid_value'
                         f'&$filter=' + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DEP}')")
                  + '&$orderby=createdon desc', prefer='odata.maxpagesize=500').get('value', [])
    con = sum(1 for r in muestra if r.get('_sanic_recountingsessionmirror_value'))
    print(f'  muestra {len(muestra)} · espejo poblado {con} '
          f'({100.0 * con / (len(muestra) or 1):.1f}%)')
    if muestra and not con:
        print('  !! HALLAZGO: DepositSessionMirrorPlugin NO estampo nada (step inactivo?)',
              file=sys.stderr)
    res['e1'] = (con, len(muestra))

    print('\n[4] ESPEJO E-2 Bag (cheque -> tula)')
    muestra = req('GET', f'/{ES_CHK}?$select={PK[ES_CHK]},sanic_name,_sanic_bag_value'
                         f'&$filter=' + urllib.parse.quote(f"startswith(sanic_name,'{TAG_CHK}')")
                  + '&$orderby=createdon desc', prefer='odata.maxpagesize=500').get('value', [])
    con = sum(1 for r in muestra if r.get('_sanic_bag_value'))
    print(f'  muestra {len(muestra)} · espejo poblado {con} '
          f'({100.0 * con / (len(muestra) or 1):.1f}%)')
    res['e2'] = (con, len(muestra))

    print('\n[5] ESPEJO E-3 DeclaredBagMirror (cheque declarado -> bolsa declarada)')
    muestra = req('GET', f'/{ES_DCHK}?$select={PK[ES_DCHK]},sanic_name,'
                         f'_sanic_declaredbagmirror_value&$filter='
                  + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DCHK}')")
                  + '&$orderby=createdon desc', prefer='odata.maxpagesize=500').get('value', [])
    con = sum(1 for r in muestra if r.get('_sanic_declaredbagmirror_value'))
    print(f'  muestra {len(muestra)} · espejo poblado {con} '
          f'({100.0 * con / (len(muestra) or 1):.1f}%)')
    res['e3'] = (con, len(muestra))

    print('\n[6] ESPEJO E-4 + Q17 (detalle de lectura -> detalle de sesion, solo activas)')
    muestra = req('GET', f'/{ES_XMD}?$select={PK[ES_XMD]},sanic_name,'
                         f'_sanic_recountingsessiondetailmirror_value,sanic_xmlreadingactiva'
                         f'&$filter='
                  + urllib.parse.quote(f"startswith(sanic_name,'{TAG_XMD}')")
                  + '&$orderby=createdon desc', prefer='odata.maxpagesize=500').get('value', [])
    con = sum(1 for r in muestra if r.get('_sanic_recountingsessiondetailmirror_value'))
    act = sum(1 for r in muestra if r.get('sanic_xmlreadingactiva'))
    print(f'  muestra {len(muestra)} · espejo poblado {con} · XMLReadingActiva=true {act}')
    sdets = marcadas(ES_SDET, TAG_SDET, 'sanic_name')
    if sdets:
        sd = random.choice(sdets)[PK[ES_SDET]]
        flt = (f'_sanic_recountingsessiondetailmirror_value eq {sd} '
               f'and sanic_xmlreadingactiva eq true')
        n = count_of(ES_XMD, flt)
        _, ts = timed_query(f'/{ES_XMD}?$select=sanic_denomination,sanic_quantity,sanic_currency'
                            f'&$filter={urllib.parse.quote(flt)}&$top=100')
        p50, p95 = percentiles(ts)
        print(f'  Q17 (mirror + activa) sobre un detalle: {n} filas · '
              f'p50={p50:.0f}/p95={p95:.0f}ms')
        res['q17'] = n
    res['e4'] = (con, act, len(muestra))

    print('\n[7] TOTALES Y ASIENTOS QUE ESCRIBIO EL GUARD (no el seeder)')
    rec = get_all(f'/{ES_BAG}?$select=sanic_bagid,sanic_totalnio,sanic_totalusd,'
                  f'sanic_hasdifference&$filter='
                  + urllib.parse.quote(f"startswith(sanic_bagid,'{RCP_BAG_PREFIX}') "
                                       f"and sanic_bagstatus eq {BAG_RECONTADA}"))
    con_total = sum(1 for r in rec if (r.get('sanic_totalnio') or 0) > 0
                    or (r.get('sanic_totalusd') or 0) > 0)
    print(f'  tulas Recontadas: {len(rec)} · con TotalNIO/USD > 0: {con_total} '
          f'({100.0 * con_total / (len(rec) or 1):.1f}%)')
    if rec and not con_total:
        print('  !! HALLAZGO: BagRecountClosurePlugin NO recalculo totales', file=sys.stderr)
    dep_ids = {r[PK[ES_DEP]].lower() for r in marcadas(ES_DEP, TAG_DEP)}
    trx = [r for r in get_all(f'/{ES_TRX}?$select={PK[ES_TRX]},'
                              f'_sanic_relateddepositrecounting_value,sanic_transactiontype')
           if (r.get('_sanic_relateddepositrecounting_value') or '').lower() in dep_ids]
    print(f'  CH-Tbl-Transactions colgando de depositos SRC-DR*: {len(trx)}')
    res['totales'] = (con_total, len(rec))
    res['transacciones'] = len(trx)

    print('\n[8] SOLICITUDES por tipo (recuperacion de 02/03/04)')
    for tipo in (RT_SESION, RT_SIN_DECL, RT_CON_DIF, RT_RECONT_SIN_DECL):
        flt_tipo = f"sanic_requesttype eq '{tipo}'"
        n = count_of(ES_APR, flt_tipo)
        pend = count_of(ES_APR, flt_tipo + ' and sanic_approvalstatus eq null')
        _, ts = timed_query(f"/{ES_APR}?$select=sanic_name,sanic_approvalstatus,sanic_bagnumber"
                            f"&$filter={urllib.parse.quote(flt_tipo)}&$top=100")
        p50, p95 = percentiles(ts)
        print(f'  {tipo:34} total {n:6} · pendientes {pend:6} · '
              f'p50={p50:.0f}/p95={p95:.0f}ms  '
              f'{">500 OK" if n > 500 else ""}')
        res[f'apr_{tipo}'] = (n, pend)

    print('\n[9] CHEQUES Y DECLARADOS POR TULA/BOLSA (topes de 500 por grilla)')
    tulas = get_all(f'/{ES_CHK}?$select=_sanic_bag_value&$filter='
                    + urllib.parse.quote(f"startswith(sanic_name,'{TAG_CHK}')"))
    por_tula = {}
    for r in tulas:
        por_tula[r.get('_sanic_bag_value')] = por_tula.get(r.get('_sanic_bag_value'), 0) + 1
    por_tula.pop(None, None)
    if por_tula:
        top = max(por_tula.items(), key=lambda kv: kv[1])
        print(f'  tula con mas cheques: {top[0]} -> {top[1]} '
              f'({">500 OK" if top[1] > 500 else "NO cruza 500"})')
        res['cheques_tula_max'] = top[1]
    dchk = get_all(f'/{ES_DCHK}?$select=_sanic_declaredbagmirror_value&$filter='
                   + urllib.parse.quote(f"startswith(sanic_name,'{TAG_DCHK}')"))
    por_bolsa = {}
    for r in dchk:
        k = r.get('_sanic_declaredbagmirror_value')
        por_bolsa[k] = por_bolsa.get(k, 0) + 1
    por_bolsa.pop(None, None)
    if por_bolsa:
        top = max(por_bolsa.items(), key=lambda kv: kv[1])
        print(f'  bolsa declarada con mas cheques (por E-3): {top[0]} -> {top[1]} '
              f'({">500 OK" if top[1] > 500 else "NO cruza 500"})')
        res['dchecks_bolsa_max'] = top[1]
    return res


# ===========================================================================
def main():
    ap = argparse.ArgumentParser(
        description='Seeder de estres de RECONTEO para CH-App-Recounting v2 (marcador SRC-*)')
    # ---- F1
    ap.add_argument('--declared-bags', type=int, default=3000)
    ap.add_argument('--dep-min', type=int, default=2, help='depositos declarados por bolsa (min)')
    ap.add_argument('--dep-max', type=int, default=6, help='depositos declarados por bolsa (max)')
    ap.add_argument('--bags-por-declaracion', type=int, default=50)
    ap.add_argument('--fat-declared', type=int, default=6,
                    help='bolsas declaradas GORDAS (>500 cheques declarados c/u): prueban E-3')
    ap.add_argument('--fat-declared-deps', type=int, default=60)
    ap.add_argument('--fat-declared-chk', type=int, default=10)
    # ---- F2
    ap.add_argument('--desks', type=int, default=40)
    ap.add_argument('--sessions', type=int, default=600)
    ap.add_argument('--approvals', type=int, default=800)
    ap.add_argument('--backdate-days', type=int, default=90)
    ap.add_argument('--no-backdate', action='store_true',
                    help='no manda overriddencreatedon (todo queda con fecha de hoy)')
    ap.add_argument('--resolve-approvals', action='store_true',
                    help='OPT-IN: apaga TEMPORALMENTE el step de Update de solicitudes para '
                         'dejarlas RESUELTAS (ver docstring, decision 3)')
    ap.add_argument('--reject-ratio', type=float, default=0.25)
    # ---- F3
    ap.add_argument('--recount-bags', type=int, default=1500)
    ap.add_argument('--rdep-min', type=int, default=1)
    ap.add_argument('--rdep-max', type=int, default=5)
    ap.add_argument('--cmd-min', type=int, default=2, help='denominaciones por deposito (min)')
    ap.add_argument('--cmd-max', type=int, default=5, help='denominaciones por deposito (max)')
    ap.add_argument('--fat-bag-deps', type=int, default=6)
    ap.add_argument('--fat-bag-checks', type=int, default=600,
                    help='cheques en UNA sola tula: cruza el tope de 500 de la grilla de 04')
    ap.add_argument('--close-clean', type=int, default=1000)
    ap.add_argument('--close-diff', type=int, default=200)
    # ---- F4
    ap.add_argument('--xml-readings', type=int, default=700)
    ap.add_argument('--xmd-min', type=int, default=30)
    ap.add_argument('--xmd-max', type=int, default=60)
    # ---- infraestructura
    ap.add_argument('--chunk', type=int, default=100, help='requests por $batch')
    ap.add_argument('--workers', type=int, default=6, help='$batch en paralelo')
    # ---- fases
    ap.add_argument('--resume', action='store_true',
                    help='retoma una corrida cortada: cada ETAPA consulta lo que ya existe por '
                         'marcador SRC-<TAG> y salta si esta completa o completa el DELTA. '
                         'Nunca duplica y nunca decide por contadores ciegos.')
    ap.add_argument('--all', action='store_true', help='F1 + F2 + F3 + F4')
    ap.add_argument('--only-declarations', action='store_true')
    ap.add_argument('--only-sessions', action='store_true')
    ap.add_argument('--only-recount', action='store_true')
    ap.add_argument('--only-xml', action='store_true')
    ap.add_argument('--clean', action='store_true')
    ap.add_argument('--dry-run', action='store_true', help='plan + conteos reales, CERO escrituras')
    ap.add_argument('--dry-writes', action='store_true',
                    help='ENSAYO: corre las fases de verdad (lecturas reales, deltas reales) '
                         'pero SIMULA cada escritura. Valida el --resume sin tocar DEV.')
    ap.add_argument('--verify', action='store_true', help='solo verificar')
    ap.add_argument('--no-verify', action='store_true', help='sembrar y no verificar')
    ap.add_argument('--restore-steps', action='store_true',
                    help='reactiva los steps del clean y sale (rescate)')
    a = ap.parse_args()

    print('=' * 78)
    print(f'SEEDER RECONTEO — marcador {MARK}* · materia prima {RCP_BAG_PREFIX}* — {URL}')
    print('=' * 78)
    if a.dry_writes:
        NOWRITE['on'] = True
        print('  !! --dry-writes: ENSAYO. Las lecturas son REALES, las escrituras se SIMULAN.')

    if a.restore_steps:
        todos = leer_steps()
        pasos = [todos[n] for n in (STEPS_CLEAN + (STEP_UPD_SOLICITUD,)) if n in todos]
        ok = reactivar(pasos)
        mostrar_steps('estado final:', STEPS_CLEAN + (STEP_UPD_SOLICITUD,))
        sys.exit(0 if ok else 1)

    if a.clean:
        clean(a)
        return
    if a.verify:
        verify(a)
        return

    fases = {
        'declarations': a.only_declarations or a.all,
        'sessions': a.only_sessions or a.all,
        'recount': a.only_recount or a.all,
        'xml': a.only_xml or a.all,
    }
    if not any(fases.values()) and not a.dry_run:
        raise SystemExit('elegir una fase: --all | --only-declarations | --only-sessions | '
                         '--only-recount | --only-xml  (o --dry-run para ver el plan)')
    if a.dry_run and not any(fases.values()):
        fases = {k: True for k in fases}

    inv, den, usrs = inventario()

    print('\n== ESTADO DE LOS GUARDS (deben estar TODOS ACTIVOS para sembrar) ==')
    todos = leer_steps()
    _, faltan_clean = mostrar_steps('steps que el --clean apaga:', STEPS_CLEAN, todos)
    _, faltan_audit = mostrar_steps('steps auditados (NUNCA se apagan):', STEPS_AUDIT, todos)
    if faltan_clean or faltan_audit:
        print(f'  !! {len(faltan_clean) + len(faltan_audit)} steps NO EXISTEN todavia: '
              'el guard de recounting no esta desplegado '
              '(./scripts/register-recounting-guard.sh)', file=sys.stderr)

    puede_bd, meta_bd = metadata_backdate()
    print(f'\n  overriddencreatedon (retro-fechado de sesiones): '
          f'IsValidForCreate={puede_bd}')

    p = plan(a, inv)
    print('\n  FASES A CORRER: ' + ', '.join(k for k, v in fases.items() if v)
          + ('   [MODO RESUME: delta por etapa contra lo ya sembrado]' if a.resume else ''))

    if a.dry_run:
        print('\n' + '=' * 78)
        print('(dry-run) NO SE ESCRIBIO NADA')
        print('=' * 78)
        return

    if faltan_clean or faltan_audit:
        raise SystemExit(
            'ABORTADO: el guard de recounting no esta completo en el entorno. Este seeder '
            'esta disenado para correr con el guard 100% ACTIVO (es parte de lo que prueba). '
            'Desplegar register-recounting-guard.sh y volver.')

    resultados = {}
    if fases['declarations']:
        resultados['F1'] = seed_declarations(a, inv, usrs)
    if fases['sessions']:
        resultados['F2'] = seed_sessions(a, inv, usrs)
    if fases['recount']:
        resultados['F3'] = seed_recount(a, inv, usrs, den)
    if fases['xml']:
        resultados['F4'] = seed_xml(a, den)

    print('\n' + '=' * 78)
    print('RESUMEN GENERAL')
    print('=' * 78)
    for k, v in resultados.items():
        print(f'  {k}: {v}')
    mostrar_reintentos()
    if NOWRITE['on']:
        print(f'\n  !! ENSAYO --dry-writes: {NOWRITE["n"]} escrituras SIMULADAS, ninguna real')
        return

    if not a.no_verify:
        verify(a)


if __name__ == '__main__':
    main()
