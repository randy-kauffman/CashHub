#!/usr/bin/env bash
# =============================================================================
# Cierra los huecos de privilegios del rol del cajero (CH-SR-Cashier).
# Idempotente: AddPrivilegesRole no duplica lo que ya esta.
#
# POR QUE EXISTE. El cajero abria una tula CON declaracion y saltaba
# "Network error when using Patch function: You don't have permission to edit
# this row". La tula quedaba abierta pero SIN enlazar: el primer Patch entraba y
# el segundo no.
#
# El enlace declarado <-> recontado se escribe en LAS DOS PUNTAS, y el rol solo
# tenia Read/Append/AppendTo sobre las declaradas:
#
#   scrTulas  Patch('CH-Tbl-DeclaredBags',     ..., { BagID: gvarTula })
#   scrConteo Patch('CH-Tbl-DeclaredDeposits', ..., { RelatedRecountedDeposit: ... })
#
# Append/AppendTo NO alcanzan: sirven para colgar un registro de otro, no para
# modificar el registro apuntado. El Patch necesita Write.
#
# LA PRIMERA VERSION DE ESTE SCRIPT SE QUEDO CORTA, y vale la pena decir por que:
# la auditoria se hizo con un grep de UNA LINEA (Patch\('CH-Tbl-...'\)), y en esta
# app la tabla casi siempre va en la linea SIGUIENTE al Patch(. Asi se escaparon
# DeclaredChecks y tres Delete. El barrido bueno contempla multilinea; de ahi sale
# la lista de abajo.
#
# 🚨 TIENE UNA IMPLICANCIA DE SEGURIDAD, y hay que decirla. Write en Dataverse es
#    por TABLA, no por columna: el cajero queda pudiendo modificar cualquier campo
#    de la bolsa declarada, incluidos los montos que declaro el cliente. Lo que la
#    app necesita es escribir UN lookup. Si eso molesta, el arreglo de fondo es
#    mover esas dos escrituras a una Custom API con contrato angosto —como se hizo
#    con sanic_CHResolveApprovalRequest— y quitarle el Write al rol.
#
# Uso: ./scripts/grant-cashier-declared-write.sh
# =============================================================================
set -euo pipefail
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL

python3 - <<'PYEOF'
import json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json',
     '-H', 'Accept: application/json']

def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise SystemExit(f"{method} {path}: {d['error'].get('message','')[:300]}")
    return d

def find(entityset, filtro, select):
    return req('GET', f"/{entityset}?$select={select}&$filter={urllib.parse.quote(filtro)}").get('value', [])

# El rol se busca por ID y no por nombre: en el entorno figura como
# "CH-SR-Cashier" y en solutions/unpacked/core/Roles/ como "CH-SR-Counter.xml".
# Mismo id, nombres distintos — buscar por nombre falla segun de que lado mires.
ROL = 'f9bee50d-6a75-f011-b4cb-7c1e5248985f'
rol = find('roles', f"roleid eq {ROL}", 'name,_businessunitid_value')
if not rol:
    raise SystemExit(f"No existe el rol {ROL}.")
bu = rol[0]['_businessunitid_value']
print(f"Rol: {rol[0]['name']}  ({ROL})")

# Salido de auditar TODAS las escrituras de la app del cajero (Patch/Remove) con
# un barrido que contempla MULTILINEA: la tabla suele ir en la linea siguiente al
# Patch(, y un grep de una sola linea se los pierde. Asi se me habia escapado
# DeclaredChecks en la primera pasada.
#
# Depth BASIC en los Delete: el cajero borra LO SUYO (su sesion, su solicitud
# pendiente, sus filas de denominacion). Global ahi le dejaria borrar de otros.
# Depth GLOBAL en el Write de DeclaredChecks: esos registros los creo la ingesta,
# no el cajero, asi que Basic no alcanzaria.
NECESARIOS = [
    ('prvWritesanic_CHTblDeclaredBags',        'Global'),
    ('prvWritesanic_CHTblDeclaredDeposits',    'Global'),
    # MatchedCheck: enlaza el cheque declarado con el del recuento (scrConteo 1112, 6185, 6218)
    ('prvWritesanic_CHTblDeclaredChecks',      'Global'),
    # el cajero cancela SU solicitud pendiente (scrSesion 1029)
    ('prvDeletesanic_CHTblApprovalRequest',    'Basic'),
    # baja de filas de denominacion al descartar o recalcular (scrConteo 1138, 6250)
    ('prvDeletesanic_CHTblCountedMoneyDetails','Basic'),
    # libera la mesa / revierte la sesion si la solicitud no entro (scrSesion 864, 1053)
    ('prvDeletesanic_CHTblRecountingSession',  'Basic'),
    # 🧨 LO NECESITA PARA SER DUEÑO, no para leer una pantalla. CreateTransactionCut
    #    le asigna al cajero la PROPIEDAD de su lote —"los cajeros ven solo lo suyo",
    #    filtrado por owner— y Dataverse exige que el nuevo dueño tenga Read sobre la
    #    tabla. Sin esto el corte MUERE con un volcado de privilegios ilegible:
    #    "Principal user (Id=..., roleCount=2 ...) is missing prvReadsanic_...".
    #    Basic alcanza: el cajero ve SU lote, no los ajenos.
    ('prvReadsanic_TransactionBatches',        'Basic'),
]
# NO se pide prvWritesanic_CHTblApprovalRequest: los tres Patch contra esa tabla
# son sobre Defaults(), o sea CREATE, que el rol ya tiene. Write ahi le dejaria
# modificar solicitudes ajenas sin que ningun flujo de la app lo necesite.

actuales = {x['name'] for x in
            req('GET', f"/roles({ROL})/roleprivileges_association?$select=name").get('value', [])}

faltan = []
for nombre, depth in NECESARIOS:
    if nombre in actuales:
        print(f"  ya lo tiene: {nombre}")
        continue
    p = find('privileges', f"name eq '{nombre}'", 'name,privilegeid')
    if not p:
        raise SystemExit(f"No existe el privilegio {nombre}.")
    faltan.append({'Depth': depth, 'PrivilegeId': p[0]['privilegeid'],
                   'BusinessUnitId': bu, 'PrivilegeName': nombre})
    print(f"  agregando:   {nombre}  ({depth})")

if faltan:
    req('POST', f"/roles({ROL})/Microsoft.Dynamics.CRM.AddPrivilegesRole", {'Privileges': faltan})

# 🧨 AddPrivilegesRole actualiza el ROL pero NO recalcula los privilegios efectivos
#    de los usuarios que YA lo tenian asignado. Se comprobo: el rol quedaba con el
#    privilegio y RetrieveUserPrivileges seguia sin listarlo, seis consultas a lo
#    largo de dos minutos. Quitar y volver a poner el rol fuerza el recalculo.
#    Sin esto el script "termina bien" y el usuario sigue sin poder trabajar.
usuarios = req('GET', f"/roles({ROL})/systemuserroles_association"
                      "?$select=fullname,systemuserid").get('value', [])
print(f"\nrefrescando privilegios de {len(usuarios)} usuario(s) con el rol:")
for u in usuarios:
    uid = u['systemuserid']
    req('DELETE', f"/systemusers({uid})/systemuserroles_association/$ref?$id={API}/roles({ROL})")
    req('POST', f"/systemusers({uid})/systemuserroles_association/$ref",
        {'@odata.id': f"{API}/roles({ROL})"})
    efectivos = {x['PrivilegeName'] for x in
                 req('GET', f"/systemusers({uid})/Microsoft.Dynamics.CRM.RetrieveUserPrivileges()")
                 .get('RolePrivileges', [])}
    faltantes = [n for n, _ in NECESARIOS if n not in efectivos]
    estado = 'OK' if not faltantes else f"AUN FALTAN {faltantes}"
    print(f"  {u.get('fullname')}: {estado}")

final = {x['name'] for x in
         req('GET', f"/roles({ROL})/roleprivileges_association?$select=name").get('value', [])}
print("\nEstado final sobre las tablas declaradas:")
for tabla in ('DeclaredBags', 'DeclaredDeposits', 'DeclaredChecks',
              'ApprovalRequest', 'CountedMoneyDetails', 'RecountingSession'):
    acc = [a for a in ('Read', 'Write', 'Create', 'Append', 'AppendTo', 'Delete')
           if f"prv{a}sanic_CHTbl{tabla}" in final]
    print(f"  {tabla:<18} {', '.join(acc) if acc else '(ninguno)'}")
PYEOF
