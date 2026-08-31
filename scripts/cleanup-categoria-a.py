#!/usr/bin/env python3
# =============================================================================
# Limpieza de componentes muertos — categoria A de docs/referencia/limpieza-candidatos.md
#
# QUE BORRA: 1 tabla, 1 relacion implicita y 11 columnas que cumplen DOBLE condicion:
#   (a) cero referencias en codigo — los tres arboles de fuente de canvas apps,
#       los cloud flows, src/backend/ y scripts/
#   (b) cero datos en el ambiente — verificado por consulta agregada
#
# POR QUE UN SCRIPT Y NO A MANO: borrar metadata en Dataverse es IRREVERSIBLE.
# Un script deja constancia de que se borro, en que orden y con que verificacion
# previa; y se puede volver a correr contra TEST/PROD, donde los datos pueden ser
# distintos a DEV y la verificacion debe repetirse desde cero.
#
# OJO — DRY-RUN POR DEFECTO. Sin --apply no toca nada: solo reporta dependencias
# y conteos. Ejecutar SIEMPRE en dry-run primero y leer el reporte.
#
# OJO — EL ORDEN IMPORTA. Dataverse rechaza el borrado de una columna que este en
# un formulario o vista, y el de una tabla que tenga relaciones vivas. Por eso se
# consulta RetrieveDependenciesForDelete ANTES de cada borrado: si el componente
# tiene dependencias, se reporta y se SALTA en vez de forzar.
#
# OJO — NO CONFIAR EN LA VERIFICACION HECHA CONTRA DEV. Los conteos de datos se
# vuelven a correr en el ambiente que apunte pp_secrets.env. Una columna vacia en
# DEV puede tener datos en produccion; el script aborta ese borrado si encuentra
# datos, sin importar lo que diga el documento de analisis.
#
# Requiere pp_secrets.env.
# Uso:  ./scripts/cleanup-categoria-a.py            # dry-run, no toca nada
#       ./scripts/cleanup-categoria-a.py --apply    # ejecuta los borrados
# =============================================================================
import json
import os
import sys
import urllib.parse
import urllib.request

APPLY = "--apply" in sys.argv

# --- componentes objetivo -----------------------------------------------------
# Cada columna lleva la razon por la que se considera muerta, para que el reporte
# sea auditable sin abrir el documento de analisis.
COLUMNAS = [
    ("sanic_chtblreceivedemails", "sanic_attachment",
     "Adjunto del correo; sustituido por la tabla CH-Tbl-EmailsAttachments"),
    ("sanic_chtblreceivedemails", "sanic_overrideby",
     "Override manual de validacion, nunca construido (grant-console-email-fields.sh:30)"),
    ("sanic_chtblreceivedemails", "sanic_overrideon",
     "Override manual de validacion, nunca construido"),
    ("sanic_chtblreceivedemails", "sanic_overridereason",
     "Override manual de validacion, nunca construido"),
    ("sanic_chtblreceivedemails", "sanic_replyemailinternetmessageid",
     "Encadenado de respuestas por Internet-Message-Id, nunca construido"),
    ("sanic_chtblreceivedemails", "sanic_replyemailoutlookuniqueid",
     "Encadenado de respuestas por id de Outlook, nunca construido"),
    ("sanic_chtblreceptions", "sanic_comments",
     "Comentario libre de recepcion; sin referencias"),
    ("sanic_chtblappcatalog", "sanic_tags",
     "Etiquetas del Hub; el Hub filtra por acceso, no por tags"),
    ("sanic_chtblclientaccount", "sanic_chchrecordstatus",
     "Estado de vigencia legacy; distinto de sanic_recordstatus, que SI se usa"),
    ("sanic_chtblclient", "sanic_iseditable",
     "Bandera de editabilidad; sin referencias en ningun arbol de busqueda"),
]

# La tabla se borra al final: arrastra sus columnas, relaciones, formularios y vistas.
TABLA = ("sanic_chtblclientdeposithistory",
         "Historial de depositos por cliente; funcion cubierta por "
         "CH-Tbl-DeclaredDeposits y CH-Tbl-Transactions")

COMPONENT_TYPE_ENTITY = 1
COMPONENT_TYPE_ATTRIBUTE = 2


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
            f"Fallo la autenticacion contra Azure AD ({e.code}). "
            f"Revisar PP_TENANT_ID / PP_CLIENT_ID / PP_CLIENT_SECRET. Respuesta: "
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


def metadata_tabla(api, token, tabla):
    """Devuelve (MetadataId, EntitySetName, PrimaryIdAttribute).

    OJO: el EntitySetName NO se puede derivar agregando 's' al logical name.
    Dataverse pluraliza de forma irregular (sanic_transactionbatches ->
    sanic_transactionbatcheses). Hay que preguntarselo a la metadata.
    """
    r = llamar(api, token, "GET",
               f"EntityDefinitions(LogicalName='{tabla}')"
               f"?$select=MetadataId,EntitySetName,PrimaryIdAttribute")
    return r["MetadataId"], r["EntitySetName"], r["PrimaryIdAttribute"]


def metadata_id_columna(api, token, tabla, columna):
    r = llamar(api, token, "GET",
               f"EntityDefinitions(LogicalName='{tabla}')"
               f"/Attributes(LogicalName='{columna}')?$select=MetadataId")
    return r["MetadataId"]


def contar_filas(api, token, entity_set, pk):
    """Cuenta TODAS las filas de la tabla."""
    q = urllib.parse.urlencode({"$count": "true", "$top": "1", "$select": pk})
    r = llamar(api, token, "GET", f"{entity_set}?{q}")
    return r.get("@odata.count", 0)


def contar_no_nulos(api, token, entity_set, columna, etiqueta):
    """Cuenta filas con valor en la columna. None si Dataverse rechaza el filtro."""
    q = urllib.parse.urlencode({
        "$count": "true", "$top": "1", "$filter": f"{columna} ne null"})
    try:
        r = llamar(api, token, "GET", f"{entity_set}?{q}")
        return r.get("@odata.count", 0)
    except RuntimeError as e:
        traza("DATOS", f"  no se pudo contar {etiqueta}: {e}")
        return None


def dependencias(api, token, metadata_id, tipo):
    r = llamar(api, token, "GET",
               f"RetrieveDependenciesForDelete(ObjectId={metadata_id},"
               f"ComponentType={tipo})")
    return r.get("value", []) if r else []


def describir_dependencias(deps):
    if not deps:
        return "ninguna"
    resumen = {}
    for d in deps:
        tipo = d.get("dependentcomponenttype", "?")
        resumen[tipo] = resumen.get(tipo, 0) + 1
    return ", ".join(f"tipo {t}: {n}" for t, n in sorted(resumen.items()))


def main():
    sec = cargar_secretos()
    api = sec["DATAVERSE_URL"] + "/api/data/v9.2"
    traza("INICIO", f"Ambiente: {sec['DATAVERSE_URL']}")
    traza("INICIO", "MODO: APLICAR (se van a borrar componentes)" if APPLY
          else "MODO: DRY-RUN (no se toca nada; usar --apply para ejecutar)")

    token = obtener_token(sec)
    traza("AUTH", "token obtenido")

    borrables, bloqueados = [], []

    # --- columnas -------------------------------------------------------------
    traza("VERIFICACION", f"Revisando {len(COLUMNAS)} columnas")
    cache_tabla = {}
    for tabla, columna, razon in COLUMNAS:
        etiqueta = f"{tabla}.{columna}"
        try:
            if tabla not in cache_tabla:
                cache_tabla[tabla] = metadata_tabla(api, token, tabla)
            _, entity_set, _ = cache_tabla[tabla]
            mid = metadata_id_columna(api, token, tabla, columna)
        except RuntimeError as e:
            traza("VERIFICACION", f"  {etiqueta}: NO EXISTE o no se pudo leer -> {e}")
            bloqueados.append((etiqueta, "no se pudo resolver el MetadataId"))
            continue

        no_nulos = contar_no_nulos(api, token, entity_set, columna, etiqueta)
        deps = dependencias(api, token, mid, COMPONENT_TYPE_ATTRIBUTE)

        if no_nulos is None:
            motivo = "no se pudo verificar si tiene datos"
        elif no_nulos > 0:
            motivo = f"TIENE DATOS: {no_nulos} filas con valor"
        elif deps:
            motivo = f"tiene dependencias ({describir_dependencias(deps)})"
        else:
            motivo = None

        if motivo:
            traza("VERIFICACION", f"  {etiqueta}: BLOQUEADA — {motivo}")
            bloqueados.append((etiqueta, motivo))
        else:
            traza("VERIFICACION", f"  {etiqueta}: libre (0 datos, 0 dependencias) — {razon}")
            borrables.append(("columna", tabla, columna))

    # --- tabla ----------------------------------------------------------------
    tabla, razon = TABLA
    traza("VERIFICACION", f"Revisando tabla {tabla}")
    try:
        mid, entity_set, pk = metadata_tabla(api, token, tabla)
        filas = contar_filas(api, token, entity_set, pk)
        deps = dependencias(api, token, mid, COMPONENT_TYPE_ENTITY)
        if filas > 0:
            motivo = f"TIENE DATOS: {filas} filas"
        elif deps:
            motivo = f"tiene dependencias ({describir_dependencias(deps)})"
        else:
            motivo = None
        if motivo:
            traza("VERIFICACION", f"  {tabla}: BLOQUEADA — {motivo}")
            bloqueados.append((tabla, motivo))
        else:
            traza("VERIFICACION", f"  {tabla}: libre (0 filas, 0 dependencias) — {razon}")
            borrables.append(("tabla", tabla, None))
    except RuntimeError as e:
        traza("VERIFICACION", f"  {tabla}: no se pudo leer -> {e}")
        bloqueados.append((tabla, "no se pudo resolver el MetadataId"))

    # --- reporte --------------------------------------------------------------
    print()
    traza("REPORTE", f"Libres para borrar: {len(borrables)}")
    for tipo, t, c in borrables:
        print(f"           - {tipo}: {t}" + (f".{c}" if c else ""))
    traza("REPORTE", f"Bloqueados: {len(bloqueados)}")
    for etiqueta, motivo in bloqueados:
        print(f"           - {etiqueta}: {motivo}")

    if not APPLY:
        print()
        traza("FIN", "Dry-run terminado. Nada fue modificado. "
                     "Revisar el reporte y volver a correr con --apply.")
        return

    # --- borrado --------------------------------------------------------------
    print()
    traza("BORRADO", "Aplicando. Las columnas primero, la tabla al final.")
    borradas = 0
    for tipo, t, c in borrables:
        try:
            if tipo == "columna":
                llamar(api, token, "DELETE",
                       f"EntityDefinitions(LogicalName='{t}')/Attributes(LogicalName='{c}')")
                traza("BORRADO", f"  columna eliminada: {t}.{c}")
            else:
                llamar(api, token, "DELETE", f"EntityDefinitions(LogicalName='{t}')")
                traza("BORRADO", f"  tabla eliminada: {t}")
            borradas += 1
        except RuntimeError as e:
            traza("BORRADO", f"  FALLO en {t}" + (f".{c}" if c else "") + f": {e}")

    print()
    traza("FIN", f"{borradas} de {len(borrables)} componentes eliminados. "
                 f"{len(bloqueados)} quedaron bloqueados y NO se tocaron.")
    if borradas:
        traza("FIN", "Reexportar la solucion y correr pac solution unpack para que el "
                     "repo refleje el ambiente.")


if __name__ == "__main__":
    main()
