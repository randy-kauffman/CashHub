#!/usr/bin/env python3
# =============================================================================
# Limpieza de variables de entorno sin consumidor — categoria B de
# docs/referencia/limpieza-env-vars.md
#
# QUE BORRA: 21 de las 23 definiciones sanic_CHEV*. Sobreviven exactamente dos,
# las unicas con consumidor vivo:
#   - sanic_CHEVSharedMailboxAddress  (4 cloud flows, por parametro)
#   - sanic_CHEVOpsTeamsGroupId       (CH-Flow-MailboxSweep, ACTIVO)
#
# EL DATO QUE SOSTIENE TODO: ningun plugin C# lee environmentvariablevalue — cero
# coincidencias en todo src/backend/. En esta solucion una env var solo puede
# consumirse desde un cloud flow, y los 18 flows se barrieron uno por uno.
#
# OJO — DRY-RUN POR DEFECTO. Sin --apply no toca nada.
#
# OJO — BORRAR LA DEFINICION BORRA EL VALOR. environmentvariablevalue cuelga de la
# definicion; al eliminarla se va tambien el valor cargado por ambiente. El script
# imprime el valor actual de cada una ANTES de borrarla, para que quede en el log.
#
# OJO — ESTO SE PROPAGA A TEST/PROD. La definicion vive en CashHubCore. Borrarla en
# DEV y despues importar la solucion la elimina tambien alla, junto con su valor.
# La ausencia de valor se verifico solo en DEV; la ausencia de CONSUMIDOR es a nivel
# de codigo y el codigo es el mismo en todos los ambientes — por eso el borrado se
# sostiene igual, pero conviene saberlo antes de importar.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/cleanup-env-vars.py            # dry-run, no toca nada
#       ./scripts/cleanup-env-vars.py --apply    # ejecuta los borrados
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

# Confirmadas por el usuario el 2026-08-31. La razon de cada una sale del analisis
# en docs/referencia/limpieza-env-vars.md, agrupadas por CAUSA de no-uso.
VARIABLES = [
    # --- Grupo 1: migradas, con reemplazo probado y funcionando -----------------
    ("sanic_CHEVBancaMailbox",
     "Reemplazada por CH-Tbl-Parameter key 'Email.Banca.Mailbox', con el MISMO valor. "
     "Leida en screen ColaDeProcesamiento.fx.yaml:710"),
    ("sanic_CHEVAppUrl_ClientAdmin",
     "Reemplazada por CH-Tbl-Parameter key 'Apps.ClientAdministration.Url'. Leida en "
     "la misma pantalla (711/712) y por 2 flows via OData"),
    ("sanic_CHEVFileSizeMaxMB",
     "Reemplazada por la regla VR-06 de CH-Tbl-ValidationRules. EnvelopeRules.cs:107-111 "
     "aplica el tope con fallback 5120 KB = los mismos 5 MB de esta variable"),

    # --- Grupo 2: andamiaje nunca cableado --------------------------------------
    # Sin defaultvalue, sin valor en ningun ambiente, sin consumidor, y el CONCEPTO
    # no existe en el codigo. No son restos de algo removido: nunca se conectaron.
    ("sanic_CHEVAADGroup_Admin", "Andamiaje nunca cableado"),
    ("sanic_CHEVAADGroup_ClientAdmin", "Andamiaje nunca cableado"),
    ("sanic_CHEVAADGroup_Counting", "Andamiaje nunca cableado"),
    ("sanic_CHEVAADGroup_EmailValidation", "Andamiaje nunca cableado"),
    ("sanic_CHEVAADGroup_Reception", "Andamiaje nunca cableado"),
    ("sanic_CHEVAADGroup_Supervisor", "Andamiaje nunca cableado"),
    ("sanic_CHEVAppUrl_Counting", "Andamiaje nunca cableado"),
    ("sanic_CHEVAppUrl_EmailValidation", "Andamiaje nunca cableado"),
    ("sanic_CHEVAppUrl_Reception", "Andamiaje nunca cableado"),
    ("sanic_CHEVAppUrl_Supervisor", "Andamiaje nunca cableado"),
    ("sanic_CHEVCsvExportLibraryUrl",
     "Andamiaje nunca cableado; 0 coincidencias del concepto en el repo"),
    ("sanic_CHEVEmailSubjectToAccept",
     "Andamiaje nunca cableado; 0 coincidencias del concepto en el repo"),
    ("sanic_CHEVIbsBranchCodeList",
     "Andamiaje nunca cableado; 0 coincidencias del concepto en el repo"),
    ("sanic_CHEVProcessingLibraryUrl",
     "Andamiaje nunca cableado; 0 coincidencias del concepto en el repo"),

    # --- Grupo 3: default plausible, funcionalidad inexistente ------------------
    ("sanic_CHEVTimezone",
     "Default 'America/Managua' pero NO hay manejo de zona horaria en el codigo: 0 "
     "coincidencias de Timezone/TimeZone en src/backend/, scripts/ y flows"),
    ("sanic_CHEVSharedMailboxArchiveFolderID",
     "Default con id de carpeta de Exchange valido pero NO hay archivado de correo "
     "implementado: 0 coincidencias de ArchiveFolder"),

    # --- Grupo 4: configurada a proposito y sin lector --------------------------
    ("sanic_CHEVEnvironmentURL",
     "UNICA de las 20 con valor cargado deliberadamente en DEV "
     "(https://org36e60d9d.crm.dynamics.com/) y sin un solo lector"),

    # --- Acoplada a un flow apagado, verificada como segura ---------------------
    ("sanic_CHEVRfcApplyTimeoutMinutes",
     "Su unico consumidor es CH-Flow-RFCWatchdog, hoy en Draft. NO es dependencia "
     "dura: el flow no la toma como parametro sino que consulta "
     "environmentvariablevalues con un $filter por schemaname y hace "
     "coalesce(..., '30') — mismo default que esta variable. Como no existe registro "
     "de valor, esa consulta YA devuelve vacio y el flow YA usa el 30. Ademas su "
     "runAfter acepta Succeeded y Failed. Borrarla no lo rompe ni si se reactiva: un "
     "$filter por un schemaname inexistente devuelve cero filas, no un error"),
]

# NO se tocan. Consumidor vivo y verificado.
INTOCABLES = {
    "sanic_CHEVSharedMailboxAddress":
        "Consumida por parametro en 4 cloud flows; valor cargado en DEV",
    "sanic_CHEVOpsTeamsGroupId":
        "Consumida por CH-Flow-MailboxSweep, que esta ACTIVO (StateCode=1). Es el "
        "destinatario (body/recipient) de la Adaptive Card de alerta y ademas gatea "
        "la condicion 'varFaltantes > 0 AND variable != \"\"'. Valor cargado en DEV",
}

COMPONENT_TYPE_ENVVAR_DEFINITION = 380


def traza(etapa, mensaje):
    print(f"[{etapa}] {mensaje}", flush=True)


def cargar_secretos():
    ruta = os.environ.get("ENV_FILE", "pp_secrets.env")
    if not os.path.exists(ruta):
        raise SystemExit(
            f"No existe {ruta}. Este script necesita DATAVERSE_URL, PP_TENANT_ID, "
            f"PP_CLIENT_ID y PP_CLIENT_SECRET para autenticarse contra Dataverse."
        )
    valores = {}
    with open(ruta, encoding="utf-8") as fh:
        for linea in fh:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, _, valor = linea.partition("=")
            valores[clave.strip()] = valor.strip().strip('"').strip("'").rstrip("\r")
    faltantes = [k for k in
                 ("DATAVERSE_URL", "PP_TENANT_ID", "PP_CLIENT_ID", "PP_CLIENT_SECRET")
                 if not valores.get(k)]
    if faltantes:
        raise SystemExit(f"Faltan claves en {ruta}: {', '.join(faltantes)}")
    valores["DATAVERSE_URL"] = valores["DATAVERSE_URL"].rstrip("/")
    return valores


def obtener_token(sec):
    datos = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": sec["PP_CLIENT_ID"],
        "client_secret": sec["PP_CLIENT_SECRET"],
        "scope": sec["DATAVERSE_URL"] + "/.default",
    }).encode()
    url = f"https://login.microsoftonline.com/{sec['PP_TENANT_ID']}/oauth2/v2.0/token"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=datos)) as r:
            return json.load(r)["access_token"]
    except urllib.error.HTTPError as e:
        raise SystemExit(
            f"Fallo la autenticacion contra Azure AD ({e.code}). Revisar "
            f"PP_TENANT_ID / PP_CLIENT_ID / PP_CLIENT_SECRET. Respuesta: "
            f"{e.read().decode(errors='replace')[:400]}"
        )


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
        detalle = e.read().decode(errors="replace")
        try:
            detalle = json.loads(detalle)["error"]["message"]
        except Exception:
            detalle = detalle[:400]
        raise RuntimeError(f"{metodo} {ruta} -> HTTP {e.code}: {detalle}")


def buscar_definicion(api, token, schemaname):
    q = urllib.parse.urlencode({
        "$select": "environmentvariabledefinitionid,schemaname,defaultvalue,displayname",
        "$filter": f"schemaname eq '{schemaname}'"})
    r = llamar(api, token, "GET", f"environmentvariabledefinitions?{q}")
    v = r.get("value", [])
    return v[0] if v else None


def valor_actual(api, token, definicion_id):
    q = urllib.parse.urlencode({
        "$select": "value",
        "$filter": f"_environmentvariabledefinitionid_value eq {definicion_id}"})
    r = llamar(api, token, "GET", f"environmentvariablevalues?{q}")
    v = r.get("value", [])
    return v[0]["value"] if v else None


def dependencias(api, token, metadata_id):
    r = llamar(api, token, "GET",
               f"RetrieveDependenciesForDelete(ObjectId={metadata_id},"
               f"ComponentType={COMPONENT_TYPE_ENVVAR_DEFINITION})")
    return r.get("value", []) if r else []


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR (se van a borrar definiciones)" if APPLY
          else "MODO: DRY-RUN (no se toca nada; usar --apply para ejecutar)")
    traza("INICIO", f"{len(VARIABLES)} variables confirmadas para borrar; "
                    f"{len(INTOCABLES)} intocables")
    for nombre, razon in INTOCABLES.items():
        traza("INTOCABLE", f"  {nombre}: {razon}")

    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    borrables, bloqueadas = [], []
    for schemaname, razon in VARIABLES:
        if schemaname in INTOCABLES:
            raise SystemExit(
                f"ERROR DE CONFIGURACION: {schemaname} esta en VARIABLES y tambien en "
                f"INTOCABLES. Revisar el script antes de correrlo."
            )
        d = buscar_definicion(api, token, schemaname)
        if not d:
            traza("VERIFICACION", f"  {schemaname}: NO EXISTE en este ambiente")
            continue
        did = d["environmentvariabledefinitionid"]
        val = valor_actual(api, token, did)
        deps = dependencias(api, token, did)
        detalle = (f"default={d.get('defaultvalue') or '(vacio)'} | "
                   f"valor cargado={val if val is not None else '(ninguno)'}")
        if deps:
            traza("VERIFICACION", f"  {schemaname}: BLOQUEADA — {len(deps)} dependencia(s). "
                                  f"{detalle}")
            bloqueadas.append((schemaname, f"{len(deps)} dependencia(s)"))
        else:
            traza("VERIFICACION", f"  {schemaname}: libre. {detalle}")
            traza("VERIFICACION", f"      razon: {razon}")
            borrables.append((schemaname, did, val))

    print()
    traza("REPORTE", f"Libres para borrar: {len(borrables)} | "
                     f"Bloqueadas: {len(bloqueadas)}")
    con_valor = [(n, v) for n, _, v in borrables if v]
    if con_valor:
        traza("REPORTE", "Estas tienen valor cargado que se va a perder — queda en el log:")
        for n, v in con_valor:
            print(f"           - {n} = {v}")
    for n, m in bloqueadas:
        print(f"           - BLOQUEADA {n}: {m}")

    if not APPLY:
        print()
        traza("FIN", "Dry-run terminado. Nada fue modificado. "
                     "Revisar el reporte y volver a correr con --apply.")
        return

    print()
    traza("BORRADO", "Aplicando.")
    hechas = 0
    for schemaname, did, _ in borrables:
        try:
            llamar(api, token, "DELETE", f"environmentvariabledefinitions({did})")
            traza("BORRADO", f"  eliminada: {schemaname}")
            hechas += 1
        except RuntimeError as e:
            traza("BORRADO", f"  FALLO en {schemaname}: {e}")

    print()
    traza("FIN", f"{hechas} de {len(borrables)} definiciones eliminadas.")
    if hechas:
        traza("FIN", "Reexportar CashHubCore y correr pac solution unpack para que el "
                     "repo refleje el ambiente.")


if __name__ == "__main__":
    main()
