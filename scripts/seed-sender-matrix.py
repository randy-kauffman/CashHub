#!/usr/bin/env python3
"""
Monta en el ambiente la MATRIZ DE REMITENTES de la bateria de pruebas (idempotente).

Cada caso de la matriz necesita un cliente distinto, en un estado distinto de
sanic_recordstatus, con un vinculo (CH-Tbl-ClientEmail) en un estado distinto contra
una de las tres direcciones de prueba. El archivo .xlsx que se manda es trivial y
siempre valido a proposito: lo que se prueba es el VINCULO, no el contenido.

REPARTO DE LAS TRES DIRECCIONES — no es arbitrario, cada una encarna un rol:

  randyekg@gmail.com        RICA. Tiene vinculos vigentes con varios clientes, asi que
                            la puerta de RESPUESTA siempre le abre. Sirve para probar
                            todo lo que depende del vinculo con ESTE cliente
                            (vigente / revocado / pendiente / inexistente) sin que el
                            resultado se confunda con "no le escribimos a desconocidos".

  randyekg@outlook.com      REVOCADA. Su UNICO vinculo esta Inactivo. Prueba el caso
                            "le cortamos en todos lados": no se le responde nada.

  edugonzalez95@hotmail.com DESCONOCIDA. Cero vinculos en toda la plataforma. Prueba
                            "cliente por registrar" (CIF real) y "sospechoso" (CIF que
                            no existe).

Uso: ./scripts/seed-sender-matrix.py [--dry-run]
Requiere pp_secrets.env.
"""
import json
import os
import subprocess
import sys
import urllib.parse
import tempfile

DRY = '--dry-run' in sys.argv

GMAIL = 'randyekg@gmail.com'
OUTLOOK = 'randyekg@outlook.com'
HOTMAIL = 'edugonzalez95@hotmail.com'

ACTIVE, INACTIVE, PENDING_APPROVAL, PENDING_DEACT = 159460000, 159460001, 159460002, 159460003
ESTADO = {ACTIVE: 'Activo', INACTIVE: 'Inactivo',
          PENDING_APPROVAL: 'PendingApproval', PENDING_DEACT: 'PendingDeactivation'}

# OJO: sanic_chtblclientaccount tiene la alternate key sanic_keynumerodecuenta sobre
# sanic_name — el numero de cuenta es unico EN TODO EL BANCO, no por cliente. Cada
# cliente de la matriz necesita el suyo. El .xlsx de cada caso declara justo este.
CUENTA_BASE = 910000000

# (cif, nombre, estado_cliente, taxid, [(direccion, estado_vinculo), ...], esperado)
CLIENTES = [
    ('9010000001', 'MTX Vigente', ACTIVE, None,
     [(GMAIL, ACTIVE)],
     'Registered — responde con DETALLE completo (aceptacion)'),

    ('9010000002', 'MTX Otro cliente', ACTIVE, None,
     [],
     'OtherClientContact — responde GENERICO (gmail es vigente con otros)'),

    ('9010000003', 'MTX Vinculo revocado', ACTIVE, None,
     [(GMAIL, INACTIVE)],
     'RevokedForClient — responde GENERICO + ReviewManual'),

    ('9010000004', 'MTX Vinculo pendiente', ACTIVE, None,
     [(GMAIL, PENDING_APPROVAL)],
     'RevokedForClient (motivo dice PENDIENTE DE APROBACION) — GENERICO + ReviewManual'),

    ('9010000005', 'MTX Vinculo pend. baja', ACTIVE, None,
     [(GMAIL, PENDING_DEACT)],
     'Registered — PendingDeactivation OPERA; detalle completo (aceptacion)'),

    ('9010000006', 'MTX Cliente inactivo', INACTIVE, None,
     [(GMAIL, ACTIVE)],
     'Registered + VR-25 falla — rechazo CON detalle (es su contacto autorizado)'),

    ('9010000007', 'MTX Cliente pendiente', PENDING_APPROVAL, None,
     [(GMAIL, ACTIVE)],
     'Registered + VR-25 falla — PendingApproval del CLIENTE no autoriza'),

    ('9010000008', 'MTX Cliente pend. baja', PENDING_DEACT, None,
     [(GMAIL, ACTIVE)],
     'Registered + VR-25 PASA — el cliente opera hasta que firmen la baja'),

    # Se resuelve por sanic_taxid, la columna nueva. El CIF de este cliente es otro.
    ('9010000009', 'MTX Por taxid', ACTIVE, 'J0310000000009',
     [(GMAIL, ACTIVE)],
     'Registered — resuelto por sanic_taxid y no por CIFBCO/CIFCOM'),

    ('9010000010', 'MTX Revocado total', ACTIVE, None,
     [(OUTLOOK, INACTIVE)],
     'Revoked — NO se responde (unico vinculo de outlook, revocado)'),
]


def token():
    env = {}
    for line in open('pp_secrets.env', encoding='utf-8'):
        line = line.strip()
        if line and not line.startswith('#') and '=' in line:
            k, v = line.split('=', 1)
            env[k.strip()] = v.strip().rstrip('\r')
    url = env['DATAVERSE_URL'].rstrip('/')
    out = subprocess.run(['curl', '-sf', '-X', 'POST',
        f"https://login.microsoftonline.com/{env['PP_TENANT_ID']}/oauth2/v2.0/token",
        '-d', f"grant_type=client_credentials&client_id={env['PP_CLIENT_ID']}"
              f"&client_secret={env['PP_CLIENT_SECRET']}&scope={url}/.default"],
        capture_output=True, text=True).stdout
    return url, json.loads(out)['access_token']


URL, TOK = token()
API = URL + '/api/data/v9.2'
H = ['-H', f'Authorization: Bearer {TOK}', '-H', 'Content-Type: application/json',
     '-H', 'OData-MaxVersion: 4.0', '-H', 'OData-Version: 4.0']


def req(method, path, body=None):
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *H, '-X', method, API + path]
    if method in ('POST', 'PATCH'):
        cmd += ['-H', 'Prefer: return=representation']
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False, encoding='utf-8')
        json.dump(body, tmp); tmp.close()
        cmd += ['--data-binary', '@' + tmp.name]
    try:
        raw = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp: os.unlink(tmp.name)
    payload, _, code = raw.rpartition('\n')
    code = int(code)
    d = json.loads(payload) if payload.strip() else {}
    if code >= 400:
        raise RuntimeError(f'{method} {path} -> {code}: {json.dumps(d)[:500]}')
    return d


def upsert(entity_set, pk, filtro, valores, etiqueta):
    """
    Busca por filtro; crea o parchea. Devuelve el id.

    El nombre de la clave primaria se pasa EXPLICITO. Adivinarlo con
    "el primer campo que termina en id" agarra cualquier cosa segun el orden del
    JSON de respuesta, y un GUID basura en un @odata.bind devuelve
    "Error in query syntax", que no se parece en nada a la causa real.
    """
    # El $filter va URL-ENCODEADO: lleva espacios y comillas, y sin encodear la
    # consulta no matchea nada. Eso hace que el upsert crea que la fila no existe,
    # intente crearla y choque contra la alternate key.
    query = urllib.parse.urlencode({'$select': pk, '$filter': filtro})
    found = req('GET', f"/{entity_set}?{query}").get('value', [])
    if found:
        rid = found[0][pk]
        if DRY:
            print(f'    [dry] PATCH {etiqueta}')
        else:
            req('PATCH', f'/{entity_set}({rid})', valores)
            print(f'    actualizado: {etiqueta}')
        return rid
    if DRY:
        print(f'    [dry] CREATE {etiqueta}')
        return None
    created = req('POST', f'/{entity_set}', valores)
    print(f'    creado: {etiqueta}')
    return created[pk]


# ---------------------------------------------------------------------------
# Cliente sintetico de la bateria de ARCHIVO (casos 01..40).
#
# Esos 40 .xlsx declaran el CIF y las cuentas de "Distribuidora El Porvenir". Sin
# esto NO llegan a ejecutar nada interesante: VR-25 o VR-23 los cortan antes y los
# casos -LIM- nunca alcanzan el materializador, que es justo donde se los espera.
#
# Se encontro que en el ambiente estaban:
#   - las cuentas 100200111 / 100200112 en PendingApproval (nunca aprobadas)
#   - el vinculo de gmail con el cliente en PendingApproval
# Con el motor nuevo, PendingApproval NO autoriza, asi que la bateria entera moria
# en VR-23 / VR-26. Se dejan Activos aca, que es el estado que la bateria asume.
# ---------------------------------------------------------------------------
BATERIA_CIF = '00120240014'
BATERIA_CUENTAS = ['100200111', '100200112']


def preparar_cliente_bateria():
    print('== Cliente sintetico de la bateria de archivo (casos 01..40)')
    q = urllib.parse.urlencode({'$select': 'sanic_chtblclientid,sanic_name',
                                '$filter': f"sanic_cifbco eq '{BATERIA_CIF}'"})
    found = req('GET', f'/sanic_chtblclients?{q}').get('value', [])
    if not found:
        print(f'   *** no existe el cliente con CIFBCO {BATERIA_CIF}; la bateria de archivo NO va a funcionar')
        return
    cid, nombre = found[0]['sanic_chtblclientid'], found[0]['sanic_name']
    print(f'   {BATERIA_CIF}  {nombre}')

    if not DRY:
        req('PATCH', f'/sanic_chtblclients({cid})', {'sanic_recordstatus': ACTIVE})
    print(f'   {"[dry] " if DRY else ""}cliente -> Activo')

    for cuenta in BATERIA_CUENTAS:
        qa = urllib.parse.urlencode({'$select': 'sanic_chtblclientaccountid',
                                     '$filter': f"sanic_name eq '{cuenta}'"})
        rows = req('GET', f'/sanic_chtblclientaccounts?{qa}').get('value', [])
        if not rows:
            print(f'   *** falta la cuenta {cuenta}')
            continue
        if not DRY:
            req('PATCH', f"/sanic_chtblclientaccounts({rows[0]['sanic_chtblclientaccountid']})",
                {'sanic_recordstatus': ACTIVE})
        print(f'   {"[dry] " if DRY else ""}cuenta {cuenta} -> Activa')

    upsert('sanic_chtblclientemails', 'sanic_chtblclientemailid',
           f"sanic_name eq '{GMAIL}' and _sanic_client_value eq {cid}",
           {'sanic_name': GMAIL, 'sanic_recordstatus': ACTIVE, 'sanic_alwaysnotify': False,
            'sanic_Client@odata.bind': f'/sanic_chtblclients({cid})'},
           f'vinculo {GMAIL} [Activo]')
    print()


preparar_cliente_bateria()

print(f'== Matriz de remitentes contra {URL}')
print(f'   gmail={GMAIL} (rica) · outlook={OUTLOOK} (revocada) · hotmail={HOTMAIL} (desconocida)\n')

for indice, (cif, nombre, estado_cli, taxid, vinculos, esperado) in enumerate(CLIENTES, start=1):
    cuenta = str(CUENTA_BASE + indice)
    print(f'-- {cif}  {nombre}  [cliente {ESTADO[estado_cli]}]  cuenta {cuenta}')
    print(f'   esperado: {esperado}')

    valores = {'sanic_name': nombre, 'sanic_cifbco': cif, 'sanic_recordstatus': estado_cli}
    if taxid:
        valores['sanic_taxid'] = taxid
    cliente_id = upsert('sanic_chtblclients', 'sanic_chtblclientid',
                        f"sanic_cifbco eq '{cif}'", valores, f'cliente {nombre}')

    if cliente_id:
        upsert('sanic_chtblclientaccounts', 'sanic_chtblclientaccountid',
               f"sanic_name eq '{cuenta}'",
               {'sanic_name': cuenta, 'sanic_recordstatus': ACTIVE,
                'sanic_Client@odata.bind': f'/sanic_chtblclients({cliente_id})'},
               f'cuenta {cuenta}')

        for direccion, estado_vin in vinculos:
            upsert('sanic_chtblclientemails', 'sanic_chtblclientemailid',
                   f"sanic_name eq '{direccion}' and _sanic_client_value eq {cliente_id}",
                   {'sanic_name': direccion, 'sanic_recordstatus': estado_vin,
                    'sanic_alwaysnotify': False,
                    'sanic_Client@odata.bind': f'/sanic_chtblclients({cliente_id})'},
                   f'vinculo {direccion} [{ESTADO[estado_vin]}]')
    print()

print('== Verificacion de que hotmail NO tenga vinculos (caso "desconocido")')
v = req('GET', f"/sanic_chtblclientemails?$select=sanic_name&$filter=sanic_name eq '{HOTMAIL}'")
n = len(v.get('value', []))
print(f'   {HOTMAIL}: {n} vinculo(s) — {"OK" if n == 0 else "*** TIENE VINCULOS, el caso desconocido NO va a funcionar ***"}')

print('\nMatriz lista.' if not DRY else '\n[DRY-RUN] nada se escribio.')
