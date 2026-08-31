#!/usr/bin/env python3
# =============================================================================
# Limpieza de componentes muertos — categoria A de docs/referencia/limpieza-candidatos.md
#
# QUE BORRA: 1 tabla, su relacion con CH-Tbl-Client, y 10 columnas.
#
# Por defecto exige DOBLE condicion para borrar:
#   (a) cero referencias en codigo — los tres arboles de fuente de canvas apps,
#       los cloud flows, src/backend/ y scripts/
#   (b) cero datos y cero dependencias en el ambiente — verificado en vivo
#
# La lista COLUMNAS_APROBADAS levanta esas barreras de a una, con justificacion
# escrita por columna y aprobacion explicita del usuario. Ver ahi los tres casos.
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
import re
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
]

# Columnas aprobadas EXPLICITAMENTE por el usuario, con justificacion escrita. Ser
# parte de esta lista habilita dos cosas que no pasan por defecto:
#   1. despejar sus dependencias de vistas y formularios antes de borrar
#   2. borrarlas aunque tengan datos
#
# La barrera de "cero datos" se levanta SOLO aca, y solo despues de haber MIRADO el
# contenido real — nunca por conveniencia ni para que el script termine limpio.
COLUMNAS_APROBADAS = [
    ("sanic_chtblclient", "sanic_iseditable",
     "COLUMNA FORMULA, no autorada: "
     "=If(sanic_recordstatus = Active || sanic_recordstatus = Inactive, true, false). "
     "Su definicion vivia en Entities/sanic_CHTblClient/Formulas/. Por eso ningun "
     "script la escribe y sus 6519 valores no son dato de negocio sino el resultado "
     "de un calculo: los 32 en 'No' son exactamente los clientes en PendingApproval o "
     "PendingDeactivation. Cero perdida de informacion al borrarla — el valor se "
     "reconstruye entero desde sanic_recordstatus. Nadie la leia. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblappcatalog", "sanic_tags",
     "Etiquetas para filtrar apps en el Hub. SIN DATOS: las 7 filas de la tabla la "
     "tienen nula. El Hub filtra por acceso, no por tags — corroborado en "
     "docs/procesos/00-acceso-y-plataforma.md:125 ('No se leen en pantalla'). "
     "PENDIENTE — BLOQUEADA POR LA VISTA QUICK FIND: el script despejo las otras 4 "
     "vistas y el formulario 'Information', pero el fetchxml de una vista Quick Find "
     "NO es escribible por API (devuelve HTTP 400 incluso mandandolo IDENTICO, "
     "verificado). Hay que sacar la columna de 'Quick Find Active CH-Tbl-AppCatalogs' "
     "desde el maker portal y recien despues correr este script otra vez. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblappcatalog", "sanic_aadgroupid",
     "DEPRECADO POR DISENO PROPIO, no es un campo en uso: es una lapida. El modelo "
     "de gating por grupos AAD se movio a la tabla relacional CH-Tbl-AppCatalogAccess "
     "(una fila por par app-grupo). El UNICO codigo que lo toca es "
     "AppCatalogGuard.EvaluateDeprecatedCatalogGroups(), invocado desde "
     "GuardAppCatalogPlugin, y existe para BLOQUEAR cualquier escritura con el "
     "mensaje 'declare los grupos en CH-Tbl-AppCatalogAccess'. Nadie lo LEE. "
     "0 de 7 filas tienen valor (AppCatalogAccess tiene 4). Los hits de "
     "'aadgroupid' en el Hub, en ClientAdministration, en CH-Flow-Notifyreviewers y "
     "en ChangeRequestApproverResolver pertenecen a OTRAS dos tablas que tienen un "
     "campo homonimo: AppCatalogAccess y ClientDataChangePermission. Bloqueado por "
     "3 vistas (Active, Inactive, Advanced Find); ningun formulario. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblvalidationresults", "sanic_sourcetype",
     "CERO INFORMACION: 8388 de 8388 filas con el MISMO valor (Shared Mailbox), "
     "porque el C# lo escribe hardcodeado — ReceivedEmailValidationService.CreateResult() "
     "hace new OptionSetValue(Schema.Run.SourceSharedMailbox) sin condicion. Nadie lo lee: "
     "0 en Power Fx, 0 en flows, 0 GetAttributeValue y 0 ColumnSet en C#, 0 steps con el "
     "campo en filteringattributes. OJO — HOMONIMO: sanic_sourcetype existe TAMBIEN en "
     "ValidationRuns y ValidationRules; esos NO se tocan y son otro analisis. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblvalidationresults", "sanic_rownumber",
     "REDUNDANTE AL 100%: el numero de fila ya esta en sanic_cellref, que se guarda como "
     "'J33' — la fila es su parte numerica. Verificado en los datos: cellref='J33' con "
     "rownumber=33, cellref='K7' con rownumber=7. La consola muestra Hoja y Celda juntas y "
     "a Fila la ignora, justamente porque ya esta ahi. 666 filas con valor, ningun lector. "
     "Aprobado por el usuario 2026-08-31."),

    # --- CH-Tbl-ValidationRuns --------------------------------------------------
    # Los dos tienen UN SOLO valor en las 292 filas: no distinguen nada. Sobreviven
    # sanic_sourcerecordid (lo filtran la consola y CH-Flow-MailboxSweep) y
    # sanic_receivedemail (lo filtra el tracker) — las dos formas del vinculo estan
    # vivas, cada una en una app distinta.
    ("sanic_chtblvalidationruns", "sanic_sourcetype",
     "CERO INFORMACION: 292 de 292 filas con el MISMO valor (Shared Mailbox), escrito "
     "hardcodeado por ReceivedEmailValidationService. Tercero del mismo nombre que se "
     "elimina: ya cayeron los de ValidationResults y ValidationRules. 0 lectores en "
     "Power Fx, flows, C# y scripts; 0 formularios, 0 vistas, 0 steps. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblvalidationruns", "sanic_sourceentity",
     "CERO INFORMACION: 292 de 292 filas con el MISMO valor "
     "('sanic_chtblreceivedemails'). La genericidad de la 'tabla generica' nunca se "
     "uso: solo hubo una fuente. Era el guard del paso B1b del backfill, pero un guard "
     "sobre un valor constante es vacuo — se saco de ahi y el chequeo real lo hace el "
     "propio PATCH. OJO: el homonimo de CH-Tbl-Declaration SI se conserva, porque el "
     "paso B1 lo lee de verdad. Aprobado por el usuario 2026-08-31."),

    # --- sanic_ValidationRules (OJO: la carpeta NO lleva prefijo CHTbl) ---------
    # Las 27 reglas de validacion son CLASES C# (IValidationRule en ContentRules,
    # EnvelopeRules, StructureRules y BusinessRules). Esta tabla solo les da
    # configuracion: Value, FailMessage, Order, IsActive, Dependency — los 7 campos
    # que el motor lee via Schema.Rule. Los tres de abajo quedaron afuera de ese
    # ColumnSet y de todo el resto del sistema.
    ("sanic_validationrules", "sanic_sourcetype",
     "CERO INFORMACION: 29 de 29 filas con el MISMO valor (Shared Mailbox). Marcado "
     "required, lo que hace mas ruido todavia. No esta en Schema.Rule; 0 en Power Fx, "
     "0 en flows, 0 en C#. OJO — HOMONIMO: sanic_sourcetype sigue existiendo en "
     "ValidationRuns (Schema.Run.SourceType) y ahi NO se toca; los seeds "
     "seed-tracker-stress.py y seed-bandeja-demo.py lo escriben para ESA tabla. "
     "Aprobado por el usuario 2026-08-31."),

    ("sanic_validationrules", "sanic_validationdatatype",
     "MOTOR DECLARATIVO NUNCA CONSTRUIDO: 0 de 29 filas con valor, vacio al 100%. "
     "Marcado required — prueba de que ese required es de formulario y nunca se "
     "enforzo. Junto con validationoperation formaban el diseno 'regla como dato' "
     "(dataType + operation + value); solo value sobrevivio y se usa. Las reglas "
     "reales son 27 clases C#. Aprobado por el usuario 2026-08-31."),

    ("sanic_validationrules", "sanic_validationoperation",
     "MOTOR DECLARATIVO NUNCA CONSTRUIDO: 0 de 29 filas con valor. Ademas ensucia la "
     "UI: esta en el formulario de la app de Configuracion y en 2 VISTAS, o sea una "
     "columna siempre en blanco en dos grillas. Aprobado por el usuario 2026-08-31."),

    ("sanic_chtblclientaccount", "sanic_chchrecordstatus",
     "Estado de vigencia legacy, distinto de sanic_recordstatus (que SI se usa y "
     "convive en la misma tabla). Su nombre es el del option set global "
     "(CH-Ch-RecordStatus), no el del concepto: se creo tomando el nombre del choice "
     "en vez del nombre del campo. Las 2240 filas de la tabla tienen valor y las 2240 "
     "el MISMO: PendingApproval. Un picklist con un unico valor en el 100% de los "
     "registros no contiene informacion. El propio equipo lo documenta como no usado "
     "en scripts/seed-clientadmin-stress.py:184-187. "
     "Aprobado por el usuario 2026-08-31."),
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


def quitar_de_vistas(api, token, tabla, columna, aplicar):
    """Saca la columna de toda vista publica que la muestre.

    Dataverse rechaza el borrado de una columna que este en una vista. Hay que
    editar el fetchxml (que atributos trae) Y el layoutxml (que celdas pinta) de
    cada savedquery: si se toca solo uno de los dos, la vista queda inconsistente.

    Devuelve la lista de vistas modificadas.
    """
    r = llamar(api, token, "GET",
               "savedqueries?" + urllib.parse.urlencode({
                   "$select": "savedqueryid,name,fetchxml,layoutxml",
                   "$filter": f"returnedtypecode eq '{tabla}'"}))
    tocadas = []
    for vista in r.get("value", []):
        fetch = vista.get("fetchxml") or ""
        layout = vista.get("layoutxml") or ""
        if columna not in fetch and columna not in layout:
            continue
        nuevo_fetch = re.sub(
            rf'\s*<attribute\s+name="{re.escape(columna)}"\s*/>', "", fetch)
        nuevo_layout = re.sub(
            rf'\s*<cell\s+name="{re.escape(columna)}"[^>]*/>', "", layout)
        if nuevo_fetch == fetch and nuevo_layout == layout:
            traza("VISTAS", f"  '{vista['name']}' menciona {columna} pero no se pudo "
                            f"limpiar con el patron esperado — revisar a mano")
            continue
        if not aplicar:
            traza("VISTAS", f"  '{vista['name']}': se removeria {columna}")
            tocadas.append(vista["name"])
            continue

        # Se parchea layout y fetch por separado a proposito. Dataverse acepta el
        # layoutxml de una vista Quick Find pero RECHAZA su fetchxml (HTTP 400): esa
        # vista se autogenera con casi todos los atributos de la tabla y no se deja
        # editar por API. Mandarlos juntos hace que se pierdan los dos.
        # Un fallo en una vista no puede abortar la limpieza de las demas.
        ok_algo = False
        for campo, valor in (("layoutxml", nuevo_layout), ("fetchxml", nuevo_fetch)):
            if valor == (layout if campo == "layoutxml" else fetch):
                continue
            try:
                llamar(api, token, "PATCH",
                       f"savedqueries({vista['savedqueryid']})", {campo: valor})
                ok_algo = True
            except RuntimeError as e:
                traza("VISTAS", f"  '{vista['name']}': no acepto {campo} "
                                f"({str(e)[-60:].strip()})")
        if ok_algo:
            tocadas.append(vista["name"])
            traza("VISTAS", f"  '{vista['name']}': {columna} removida")
    return tocadas


def quitar_de_formularios(api, token, tabla, columna, aplicar):
    """Saca la columna de todo formulario que la muestre.

    Regla conservadora y deliberada: se elimina la <cell> que contiene el control,
    y SOLO si la <row> queda vacia se elimina tambien la fila. No se reacomoda nada
    mas. Un formulario es layout que alguien diseño; mover columnas o secciones a
    ciegas rompe trabajo ajeno.

    Se opera con regex y no con un parser XML a proposito: ElementTree reserializa
    el documento entero y produce un diff gigante donde el cambio real son 6 lineas.

    Devuelve la lista de formularios modificados.
    """
    r = llamar(api, token, "GET",
               "systemforms?" + urllib.parse.urlencode({
                   "$select": "formid,name,type,formxml",
                   "$filter": f"objecttypecode eq '{tabla}'"}))
    tocados = []
    for form in r.get("value", []):
        xml = form.get("formxml") or ""
        if f'datafieldname="{columna}"' not in xml:
            continue
        # 1. la celda completa que contiene el control
        nuevo = re.sub(
            rf'\s*<cell\b(?:(?!</cell>|<cell\b).)*?datafieldname="{re.escape(columna)}"'
            rf'(?:(?!</cell>|<cell\b).)*?</cell>', "", xml, flags=re.S)
        # 1b. variante con el control auto-cerrado sin hijos posteriores
        if nuevo == xml:
            nuevo = re.sub(
                rf'\s*<cell\b[^>]*>(?:(?!</cell>).)*?datafieldname="{re.escape(columna)}"'
                rf'(?:(?!</cell>).)*?</cell>', "", xml, flags=re.S)
        # 2. filas que quedaron sin celdas
        nuevo = re.sub(r'\s*<row>\s*</row>', "", nuevo)

        if nuevo == xml or f'datafieldname="{columna}"' in nuevo:
            traza("FORMS", f"  '{form['name']}' contiene {columna} pero el patron no "
                           f"limpio el XML — hay que sacarlo a mano desde el maker portal")
            continue
        tocados.append(form["name"])
        if aplicar:
            llamar(api, token, "PATCH", f"systemforms({form['formid']})",
                   {"formxml": nuevo})
            traza("FORMS", f"  '{form['name']}': {columna} removida")
        else:
            traza("FORMS", f"  '{form['name']}': se removeria {columna}")
    return tocados


def quitar_de_filtros_de_step(api, token, columna, deps, aplicar):
    """Saca la columna de los filteringattributes de los steps que la referencian.

    Dataverse no deja borrar una columna que un SdkMessageProcessingStep tenga en
    sus filtros.

    OJO — SE IDENTIFICA EL STEP POR EL ID QUE REPORTA LA DEPENDENCIA, no por nombre
    ni buscando 'contains(filteringattributes, columna)'. La razon: un mismo nombre
    logico puede existir en VARIAS tablas y sus guards respectivos lo tienen en los
    filtros. Buscar por texto agarraria tambien el step de la otra tabla y le
    romperia el filtro. La dependencia ya sabe cual es el step correcto.
    """
    ids = [d.get("dependentcomponentobjectid") for d in deps
           if d.get("dependentcomponenttype") == 92]
    tocados = []
    for sid in [i for i in ids if i]:
        s = llamar(api, token, "GET",
                   f"sdkmessageprocessingsteps({sid})"
                   f"?$select=name,filteringattributes")
        attrs = [a for a in (s.get("filteringattributes") or "").split(",") if a]
        if columna not in attrs:
            continue
        nuevos = [a for a in attrs if a != columna]
        if not nuevos:
            traza("STEPS", f"  '{s['name']}': {columna} es su UNICO filtro. No se toca: "
                           f"dejarlo sin filtros cambiaria cuando se dispara el plugin")
            continue
        traza("STEPS", f"  '{s['name']}': {','.join(attrs)} -> {','.join(nuevos)}")
        if aplicar:
            llamar(api, token, "PATCH", f"sdkmessageprocessingsteps({sid})",
                   {"filteringattributes": ",".join(nuevos)})
        tocados.append(s["name"])
    return tocados


def publicar(api, token, tabla):
    """Publica la tabla. Sin esto los cambios de vista no se aplican."""
    llamar(api, token, "POST", "PublishXml",
           {"ParameterXml": f"<importexportxml><entities><entity>{tabla}"
                            f"</entity></entities></importexportxml>"})


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
    total_col = len(COLUMNAS) + len(COLUMNAS_APROBADAS)
    traza("VERIFICACION", f"Revisando {total_col} columnas "
                          f"({len(COLUMNAS_APROBADAS)} aprobadas pese a tener datos)")
    cache_tabla = {}
    aprobadas = {(t, c) for t, c, _ in COLUMNAS_APROBADAS}
    for tabla, columna, razon in COLUMNAS + COLUMNAS_APROBADAS:
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

        es_aprobada = (tabla, columna) in aprobadas

        # Para las aprobadas, intentar despejar el camino: si lo unico que bloquea son
        # vistas (tipo 26), se saca la columna de esas vistas y se vuelve a preguntar.
        # Los formularios (tipo 60) NO se tocan automaticamente — editar un formulario
        # a ciegas puede romper el layout que alguien diseño.
        if es_aprobada and deps:
            tipos = {d.get("dependentcomponenttype") for d in deps}
            # 26 = SavedQuery (vista), 60 = SystemForm (formulario),
            # 92 = SdkMessageProcessingStep (la columna esta en sus filteringattributes).
            if tipos <= {26, 60, 92}:
                traza("DESPEJE", f"  {etiqueta}: bloqueada por "
                                 f"{describir_dependencias(deps)}; despejando")
                if 26 in tipos:
                    quitar_de_vistas(api, token, tabla, columna, APPLY)
                if 60 in tipos:
                    quitar_de_formularios(api, token, tabla, columna, APPLY)
                if 92 in tipos:
                    quitar_de_filtros_de_step(api, token, columna, deps, APPLY)
                if APPLY:
                    publicar(api, token, tabla)
                    deps = dependencias(api, token, mid, COMPONENT_TYPE_ATTRIBUTE)
                    traza("DESPEJE", f"  {etiqueta}: dependencias tras despejar: "
                                     f"{describir_dependencias(deps)}")
                else:
                    deps = []
            else:
                traza("DESPEJE", f"  {etiqueta}: bloqueada por algo que este script no "
                                 f"toca ({describir_dependencias(deps)}) — revisar a mano")

        # La barrera de dependencias NO se levanta nunca: Dataverse rechaza el borrado
        # de una columna que este en un formulario o vista, sin importar la aprobacion.
        if deps:
            motivo = f"tiene dependencias ({describir_dependencias(deps)})"
        elif no_nulos is None and not es_aprobada:
            motivo = "no se pudo verificar si tiene datos"
        elif no_nulos and no_nulos > 0 and not es_aprobada:
            motivo = f"TIENE DATOS: {no_nulos} filas con valor"
        else:
            motivo = None

        if motivo:
            traza("VERIFICACION", f"  {etiqueta}: BLOQUEADA — {motivo}")
            bloqueados.append((etiqueta, motivo))
        elif es_aprobada:
            traza("VERIFICACION",
                  f"  {etiqueta}: APROBADA PESE A {no_nulos} filas con valor — {razon}")
            borrables.append(("columna", tabla, columna))
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
