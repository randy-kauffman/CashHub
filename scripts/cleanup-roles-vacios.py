#!/usr/bin/env python3
# =============================================================================
# Categoria C: elimina CH-SR-OpsAdmin y CH-SR-AnalyticsRead, transfiriendo antes
# la capacidad de diagnostico de OpsAdmin a CH-SR-ConfigAdmin.
#
# POR QUE SE BORRAN: ninguno tiene privilegios sobre tablas de negocio (solo
# prvReadsanic_CHTblAppCatalog y prvReadsanic_CHTblAppCatalogAccess, lo minimo para
# abrir el Hub) y ninguno tiene usuarios ni equipos asignados. Verificado en vivo:
# 0 y 0 en los dos.
#
# POR QUE PRIMERO SE TRANSFIERE: CH-SR-OpsAdmin era el UNICO rol de los 13 con
# prvReadPluginTraceLog, y eso fue una decision deliberada de seguridad —
# apply-supervisor-role-remediation.sh se lo quito a CH-Role, AnalyticsRead,
# ClientAdminContributor y ClientAdminApprover para que quedara solo en el rol de
# soporte explicito. Borrarlo sin transferir dejaria al sistema sin NADIE capaz de
# leer trace logs.
#
# QUE SE TRANSFIERE Y QUE NO: el delta OpsAdmin-menos-ConfigAdmin son 65
# privilegios. Se transfieren SOLO los 28 de LECTURA, que son los que sostienen el
# diagnostico. Los 37 de autoria (prvCreate*/prvWrite*/prvDelete*/prvAssign*/
# prvShare*/prvActivate* sobre Workflow, NewProcess, TranslationProcess,
# WorkflowSession, AsyncOperation) NO se transfieren: darle a un rol de
# configuracion el poder de crear, modificar y borrar flujos y procesos es una
# escalacion de privilegios, no una transferencia de capacidad de soporte.
# Si se quiere paridad total, agregarlos a mano a TRANSFERIR_IGUAL.
#
# OJO — YA EJECUTADO EL 2026-08-31 CONTRA DEV. No es re-ejecutable en este repo:
# lee CH-SR-OpsAdmin.xml para calcular el delta y ese archivo ya no existe (el rol
# se borro y el repo se resincronizo). Se conserva como registro de que se
# transfirio y con que criterio. Para correrlo contra OTRO ambiente hay que
# recuperar el XML del rol desde git antes del commit de esta limpieza.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/cleanup-roles-vacios.py            # dry-run
#       ./scripts/cleanup-roles-vacios.py --apply    # ejecuta
# =============================================================================
import json
import os
import re
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

ROL_ORIGEN = "CH-SR-OpsAdmin"
ROL_DESTINO = "CH-SR-ConfigAdmin"
ROLES_A_BORRAR = ["CH-SR-OpsAdmin", "CH-SR-AnalyticsRead"]

XML_ORIGEN = "solutions/unpacked/core/Roles/CH-SR-OpsAdmin.xml"
XML_DESTINO = "solutions/unpacked/core/Roles/CH-SR-ConfigAdmin.xml"

# Depth de Dataverse. OJO: en la Web API es un ENUM de OData
# (Microsoft.Dynamics.CRM.PrivilegeDepth), no un entero. Mandar 0/1/2/3 hace que
# el deserializador reviente con un error de enum. Va el nombre como string.
DEPTH = {"Basic": "Basic", "Local": "Local", "Deep": "Deep", "Global": "Global"}

# Roles que nunca se tocan.
NO_TOCAR = {"CH-SR-ConfigAdmin", "CH-SR-Cashier", "CH-SR-Digitizer",
            "CH-SR-Receptionist", "CH-SR-CountingSupervisor", "CH-SR-DepositsTracker",
            "CH-SR-ClientAdminApprover", "CH-SR-ClientAdminContributor",
            "CH-SR-EmailIngestionFlow", "CH-SR-EmailOperationsConsole", "CH-Role"}


def traza(etapa, mensaje):
    print(f"[{etapa}] {mensaje}", flush=True)


def cargar_secretos():
    ruta = os.environ.get("ENV_FILE", "pp_secrets.env")
    if not os.path.exists(ruta):
        raise SystemExit(f"No existe {ruta}.")
    v = {}
    with open(ruta, encoding="utf-8") as fh:
        for l in fh:
            l = l.strip()
            if not l or l.startswith("#") or "=" not in l:
                continue
            k, _, val = l.partition("=")
            v[k.strip()] = val.strip().strip('"').strip("'").rstrip("\r")
    faltan = [k for k in ("DATAVERSE_URL", "PP_TENANT_ID", "PP_CLIENT_ID",
                          "PP_CLIENT_SECRET") if not v.get(k)]
    if faltan:
        raise SystemExit(f"Faltan claves en {ruta}: {', '.join(faltan)}")
    v["DATAVERSE_URL"] = v["DATAVERSE_URL"].rstrip("/")
    return v


def obtener_token(sec):
    datos = urllib.parse.urlencode({
        "grant_type": "client_credentials", "client_id": sec["PP_CLIENT_ID"],
        "client_secret": sec["PP_CLIENT_SECRET"],
        "scope": sec["DATAVERSE_URL"] + "/.default"}).encode()
    url = f"https://login.microsoftonline.com/{sec['PP_TENANT_ID']}/oauth2/v2.0/token"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=datos)) as r:
            return json.load(r)["access_token"]
    except urllib.error.HTTPError as e:
        raise SystemExit(f"Fallo la autenticacion contra Azure AD ({e.code}): "
                         f"{e.read().decode(errors='replace')[:300]}")


def llamar(api, token, metodo, ruta, cuerpo=None):
    url = ruta if ruta.startswith("http") else f"{api}/{ruta}"
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(url, data=datos, method=metodo)
    req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/json")
    req.add_header("OData-MaxVersion", "4.0")
    req.add_header("OData-Version", "4.0")
    if datos:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as r:
            crudo = r.read()
            return json.loads(crudo) if crudo else None
    except urllib.error.HTTPError as e:
        d = e.read().decode(errors="replace")
        try:
            d = json.loads(d)["error"]["message"]
        except Exception:
            d = d[:400]
        raise RuntimeError(f"{metodo} {ruta} -> HTTP {e.code}: {d}")


def privilegios_del_xml(ruta):
    x = open(ruta, encoding="utf-8-sig").read()
    return {m.group(1): m.group(2)
            for m in re.finditer(r'<RolePrivilege name="([^"]+)" level="([^"]+)"', x)}


def rol_por_nombre(api, token, nombre):
    q = urllib.parse.urlencode({"$select": "roleid,name", "$filter": f"name eq '{nombre}'"})
    r = llamar(api, token, "GET", f"roles?{q}")["value"]
    return r[0] if r else None


def asignaciones(api, token, roleid):
    us = llamar(api, token, "GET",
                f"roles({roleid})/systemuserroles_association?$select=fullname")["value"]
    ts = llamar(api, token, "GET",
                f"roles({roleid})/teamroles_association?$select=name")["value"]
    return len(us), len(ts)


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR" if APPLY else
                    "MODO: DRY-RUN (no se toca nada; usar --apply)")

    solapado = set(ROLES_A_BORRAR) & NO_TOCAR
    if solapado:
        raise SystemExit(f"ERROR DE CONFIGURACION: {solapado} esta en las dos listas.")

    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    # --- 1. calcular el delta de LECTURA ---------------------------------------
    ops = privilegios_del_xml(XML_ORIGEN)
    cfg = privilegios_del_xml(XML_DESTINO)
    delta = {n: l for n, l in ops.items() if n not in cfg}
    lectura = {n: l for n, l in delta.items() if n.startswith("prvRead")}
    autoria = {n: l for n, l in delta.items() if not n.startswith("prvRead")}

    traza("DELTA", f"{ROL_ORIGEN} tiene {len(ops)} privilegios; "
                   f"{ROL_DESTINO} tiene {len(cfg)}")
    traza("DELTA", f"En origen y no en destino: {len(delta)}")
    traza("DELTA", f"  se transfieren (lectura/diagnostico): {len(lectura)}")
    traza("DELTA", f"  NO se transfieren (autoria, seria escalacion): {len(autoria)}")
    for n in sorted(autoria):
        traza("DELTA", f"      omitido: {n}")

    # --- 2. resolver los privilegeid ------------------------------------------
    a_agregar = []
    for nombre, nivel in sorted(lectura.items()):
        q = urllib.parse.urlencode({
            "$select": "privilegeid,name", "$filter": f"name eq '{nombre}'"})
        r = llamar(api, token, "GET", f"privileges?{q}")["value"]
        if not r:
            traza("DELTA", f"  AVISO: no se resolvio el privilegio {nombre}; se omite")
            continue
        a_agregar.append({"PrivilegeId": r[0]["privilegeid"],
                          "Depth": DEPTH.get(nivel, "Global"), "_n": nombre, "_l": nivel})
    traza("DELTA", f"Resueltos {len(a_agregar)} de {len(lectura)} privilegios de lectura")
    if any(p["_n"] == "prvReadPluginTraceLog" for p in a_agregar):
        traza("DELTA", "  prvReadPluginTraceLog incluido — es el que motivo la transferencia")
    else:
        traza("DELTA", "  AVISO: prvReadPluginTraceLog NO quedo en la lista. Revisar.")

    destino = rol_por_nombre(api, token, ROL_DESTINO)
    if not destino:
        raise SystemExit(f"No se encontro el rol destino {ROL_DESTINO}.")

    # --- 3. verificar que los roles a borrar no tengan asignaciones ------------
    objetivos = []
    for nombre in ROLES_A_BORRAR:
        r = rol_por_nombre(api, token, nombre)
        if not r:
            traza("VERIFICACION", f"  {nombre}: NO EXISTE (ya borrado)")
            continue
        nu, nt = asignaciones(api, token, r["roleid"])
        if nu or nt:
            traza("VERIFICACION", f"  {nombre}: BLOQUEADO — {nu} usuario(s) y "
                                  f"{nt} equipo(s) asignados. NO se borra.")
            continue
        traza("VERIFICACION", f"  {nombre}: libre (0 usuarios, 0 equipos)")
        objetivos.append((r["roleid"], nombre))

    if not APPLY:
        print()
        traza("FIN", f"Dry-run. Se transferirian {len(a_agregar)} privilegios a "
                     f"{ROL_DESTINO} y se borrarian {len(objetivos)} roles. "
                     f"Nada fue modificado.")
        return

    # --- 4. transferir ANTES de borrar ----------------------------------------
    print()
    traza("TRANSFERENCIA", f"Agregando {len(a_agregar)} privilegios a {ROL_DESTINO}")
    try:
        llamar(api, token, "POST",
               f"roles({destino['roleid']})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
               {"Privileges": [{"PrivilegeId": p["PrivilegeId"], "Depth": p["Depth"]}
                               for p in a_agregar]})
        traza("TRANSFERENCIA", "  OK")
    except RuntimeError as e:
        raise SystemExit(f"FALLO LA TRANSFERENCIA: {e}\n"
                         f"NO se borra ningun rol. El sistema queda como estaba.")

    # --- 5. recien ahora, borrar ----------------------------------------------
    print()
    traza("BORRADO", "Transferencia confirmada. Borrando roles.")
    hechos = 0
    for rid, nombre in objetivos:
        try:
            llamar(api, token, "DELETE", f"roles({rid})")
            traza("BORRADO", f"  eliminado: {nombre}")
            hechos += 1
        except RuntimeError as e:
            traza("BORRADO", f"  FALLO en {nombre}: {e}")

    print()
    traza("FIN", f"{hechos} de {len(objetivos)} roles eliminados.")
    traza("FIN", "PENDIENTE: editar los scripts que los nombran — "
                 "provision-appcatalog-access.sh, provision-tracker-role.sh y "
                 "apply-supervisor-role-remediation.sh.")


if __name__ == "__main__":
    main()
