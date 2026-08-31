#!/usr/bin/env python3
"""
Provisiona CH-App-Configuration: la app model-driven de CONFIGURACION de CashHub.

Las 12 tablas de configuracion del sistema (parametros, catalogo de apps, permisos
RFC, reglas de validacion, plantillas de correo, playbook, denominaciones, catalogo
de campos del RFC, y las 3 maestras operativas) no se administran desde ninguna
canvas app: hoy se tocan a mano en el editor de tablas de Dataverse o via script.
Esta app les da una sola puerta de entrada, con vistas y formularios de verdad.

Que hace (idempotente, se puede correr N veces):
  1. Web resource del icono (SVG, linea de marca CashHub).
  2. Reescribe la vista publica por defecto de cada tabla con columnas utiles.
  3. Crea vistas operativas extra (huecos de config, reglas inactivas, etc).
  4. Reescribe el formulario principal de cada tabla con secciones y todos los campos.
  5. SiteMap + AppModule (5 grupos, 12 subareas).
  6. Rol CH-SR-ConfigAdmin con CRUD Global sobre las tablas asegurables + acceso a la app.
  7. Registra TODO en la solucion CashHub_Patch_2 y publica.

TODO lo nuevo va a CashHub_Patch_2 — nunca a CashHubCore/Apps/Flows.
Las vistas y formularios que reescribe son los STUBS autogenerados al crear cada
tabla (solo "Name" + "Created On"): ninguna otra app los consume (las canvas apps
consultan Dataverse directo, no usan savedqueries) y no existe otra model-driven
app sobre estas tablas.

OJO — sanic_chtblparameter tiene OwnershipType=None: Dataverse NO genera privilegios
para ella, asi que NINGUN rol puede gobernar su acceso (ver el informe al final de
la corrida). El rol la deja fuera porque no hay nada que otorgar.

Requiere pp_secrets.env en la raiz.
Uso:  python3 scripts/provision-config-app.py [--dry-run]
"""
import base64
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOLUTION = "CashHub_Patch_2"
API = "/api/data/v9.2"
LCID = 1033

APP_UNIQUE = "sanic_CHAppConfiguration"
APP_NAME = "CH-App-Configuration"
APP_DESC = ("Configuracion de CashHub: parametros del sistema, catalogo de apps del Hub, "
            "permisos del modulo RFC, motor de validacion, plantillas de correo y "
            "catalogos maestros. Unico punto de administracion de lo que no se toca "
            "desde las canvas apps.")
ICON_WR = "sanic_/icons/cashhub-configuration.svg"
ICON_SRC = ROOT / "docs" / "marca" / "icono-configuration.svg"
ROLE_NAME = "CH-SR-ConfigAdmin"

DRY = "--dry-run" in sys.argv

# Namespace fijo: los GUID de celdas/secciones/subareas salen de uuid5, asi que
# re-correr el script produce EXACTAMENTE el mismo XML y no genera churn.
NS = uuid.UUID("6f1a2c33-4d5e-4f60-9a71-c0f1c0a5b3d7")


def det(*parts):
    return "{" + str(uuid.uuid5(NS, "|".join(parts))) + "}"


# ---------------------------------------------------------------------------
# credenciales / transporte
# ---------------------------------------------------------------------------
def load_env():
    env = {}
    for line in (ROOT / "pp_secrets.env").read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        env[k.strip()] = v.strip().strip('"').strip("'").rstrip("\r")
    return env


ENV = load_env()
BASE = ENV["DATAVERSE_URL"].rstrip("/")


def get_token():
    data = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": ENV["PP_CLIENT_ID"],
        "client_secret": ENV["PP_CLIENT_SECRET"],
        "scope": f"{BASE}/.default",
    }).encode()
    url = f"https://login.microsoftonline.com/{ENV['PP_TENANT_ID']}/oauth2/v2.0/token"
    with urllib.request.urlopen(urllib.request.Request(url, data=data)) as r:
        return json.load(r)["access_token"]


TOKEN = get_token()


def call(method, path, body=None, solution=False, prefer_rep=False):
    """Devuelve (status, dict). Nunca tira: el caller decide que hacer con el error."""
    headers = {
        "Authorization": f"Bearer {TOKEN}",
        "Accept": "application/json",
        "OData-MaxVersion": "4.0",
        "OData-Version": "4.0",
    }
    if solution:
        headers["MSCRM.SolutionUniqueName"] = SOLUTION
    if prefer_rep:
        headers["Prefer"] = "return=representation"
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE + API + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read().decode()
            out = json.loads(raw) if raw.strip() else {}
            eid = r.headers.get("OData-EntityId")
            if eid and "(" in eid:
                out.setdefault("_id", eid.rsplit("(", 1)[1].rstrip(")"))
            return r.status, out
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            msg = json.loads(raw)["error"]["message"]
        except Exception:
            msg = raw[:400]
        return e.code, {"error": msg}


def must(status, res, what):
    if status >= 300:
        print(f"  !! FALLO {what}: HTTP {status} — {res.get('error')}")
        sys.exit(1)
    return res


def q(path, flt=None, select=None, top=None):
    parts = []
    if select:
        parts.append("$select=" + select)
    if flt:
        parts.append("$filter=" + urllib.parse.quote(flt))
    if top:
        parts.append("$top=" + str(top))
    sep = "?" if parts else ""
    status, res = call("GET", path + sep + "&".join(parts))
    if status >= 300:
        print(f"  !! query {path}: {res.get('error')}")
        return []
    return res.get("value", [])


# ---------------------------------------------------------------------------
# Especificacion de las 12 tablas
#   view  : columnas de la vista por defecto  [(logicalname, ancho)]
#   order : [(attr, descending)]
#   form  : [(titulo de seccion, [logicalnames])]
#   extra : vistas adicionales [(nombre, [cols], [order], filter_xml, descripcion)]
# ---------------------------------------------------------------------------
ACTIVE = '<filter type="and"><condition attribute="statecode" operator="eq" value="0" /></filter>'

TABLES = [
    {
        "logical": "sanic_chtblparameter",
        "label": "Parametros",
        "group": "params",
        "view": [("sanic_name", 260), ("sanic_value", 300), ("sanic_category", 120),
                 ("sanic_datatype", 110), ("sanic_isactive", 90), ("sanic_description", 280)],
        "order": [("sanic_category", False), ("sanic_name", False)],
        "form": [
            ("Identificacion", ["sanic_name", "sanic_category", "sanic_datatype", "sanic_isactive"]),
            ("Valor", ["sanic_value"]),
            ("Documentacion", ["sanic_description"]),
        ],
        "extra": [(
            "CH-Config | Parametros sin valor o inactivos",
            [("sanic_name", 260), ("sanic_category", 120), ("sanic_value", 300),
             ("sanic_isactive", 90), ("sanic_description", 280)],
            [("sanic_category", False), ("sanic_name", False)],
            '<filter type="and"><condition attribute="statecode" operator="eq" value="0" />'
            '<filter type="or"><condition attribute="sanic_value" operator="null" />'
            '<condition attribute="sanic_isactive" operator="eq" value="0" /></filter></filter>',
            "Huecos de configuracion: parametros sin valor o desactivados. "
            "La politica fail-closed del backend frena la operacion si alguno de estos "
            "es un Security.ApproverGroup.*",
        )],
    },
    {
        "logical": "sanic_chtblappcatalog",
        "label": "Catalogo de apps",
        "group": "hub",
        "view": [("sanic_order", 80), ("sanic_displayname", 220), ("sanic_appkey", 160),
                 ("sanic_enabled", 90), ("sanic_tags", 160), ("sanic_appurl", 320)],
        "order": [("sanic_order", False)],
        "form": [
            ("Identificacion", ["sanic_appkey", "sanic_name", "sanic_displayname", "sanic_order", "sanic_enabled"]),
            ("Presentacion en el Hub", ["sanic_description", "sanic_tags"]),
            ("Destino", ["sanic_appurl"]),
        ],
        "extra": [],
    },
    {
        "logical": "sanic_chtblappcatalogaccess",
        "label": "Acceso a apps (grupos AAD)",
        "group": "hub",
        "view": [("sanic_appcatalog", 240), ("sanic_name", 220), ("sanic_aadgroupid", 320)],
        "order": [("sanic_appcatalog", False), ("sanic_name", False)],
        "form": [("Acceso", ["sanic_appcatalog", "sanic_name", "sanic_aadgroupid"])],
        "extra": [],
    },
    {
        "logical": "sanic_chtblclientdatachangepermission",
        "label": "Permisos RFC de cliente",
        "group": "hub",
        "view": [("sanic_name", 200), ("sanic_type", 140), ("sanic_aadgroupid", 320),
                 ("sanic_teamsgroupid", 320)],
        "order": [("sanic_name", False), ("sanic_type", False)],
        "form": [
            ("Alcance", ["sanic_name", "sanic_type"]),
            ("Grupos", ["sanic_aadgroupid", "sanic_teamsgroupid"]),
        ],
        "extra": [(
            "CH-Config | Permisos RFC sin grupo AAD",
            [("sanic_name", 200), ("sanic_type", 140), ("sanic_aadgroupid", 320),
             ("sanic_teamsgroupid", 320)],
            [("sanic_name", False)],
            '<filter type="and"><condition attribute="statecode" operator="eq" value="0" />'
            '<filter type="or"><condition attribute="sanic_aadgroupid" operator="null" />'
            '<condition attribute="sanic_aadgroupid" operator="like" value="PENDIENTE%" /></filter></filter>',
            "Filas del modulo RFC que todavia arrastran el placeholder PENDIENTE-AAD-* "
            "o quedaron sin grupo. Sin estas filas nadie puede operar Client Administration.",
        )],
    },
    {
        "logical": "sanic_validationrules",
        "label": "Reglas de validacion",
        "group": "email",
        "view": [("sanic_validationorder", 90), ("sanic_validationname", 180), ("sanic_name", 220),
                 ("sanic_validationoperation", 150), ("sanic_validationvalue", 170),
                 ("sanic_isactive", 90), ("sanic_validationfailmessage", 320)],
        "order": [("sanic_validationorder", False)],
        "form": [
            ("Identificacion", ["sanic_validationname", "sanic_name", "sanic_validationorder", "sanic_isactive"]),
            ("Definicion", ["sanic_sourcetype", "sanic_validationdatatype", "sanic_validationoperation",
                            "sanic_validationvalue", "sanic_validationdependency"]),
            ("Mensaje al cliente", ["sanic_validationfailmessage"]),
            ("Notas", ["sanic_notes"]),
        ],
        "extra": [(
            "CH-Config | Reglas inactivas",
            [("sanic_validationorder", 90), ("sanic_validationname", 180), ("sanic_name", 260),
             ("sanic_validationoperation", 150), ("sanic_validationvalue", 170)],
            [("sanic_validationorder", False)],
            '<filter type="and"><condition attribute="statecode" operator="eq" value="0" />'
            '<condition attribute="sanic_isactive" operator="eq" value="0" /></filter>',
            "Reglas apagadas: el motor las saltea. Revisar antes de culpar al parser.",
        )],
    },
    {
        "logical": "sanic_chtblemailtemplate",
        "label": "Plantillas de correo",
        "group": "email",
        "view": [("sanic_name", 240), ("sanic_subject", 360), ("sanic_isactive", 90),
                 ("modifiedon", 140), ("modifiedby", 180)],
        "order": [("sanic_name", False)],
        "form": [
            ("Identificacion", ["sanic_name", "sanic_isactive"]),
            ("Asunto", ["sanic_subject"]),
            ("Cuerpo HTML", ["sanic_bodyhtml"]),
        ],
        "extra": [],
    },
    {
        "logical": "sanic_chtblplaybook",
        "label": "Playbook de la bandeja",
        "group": "email",
        "view": [("sanic_orden", 80), ("sanic_name", 200), ("sanic_titulo", 280),
                 ("sanic_isactive", 90), ("sanic_nota", 280)],
        "order": [("sanic_orden", False)],
        "form": [
            ("Identificacion", ["sanic_name", "sanic_titulo", "sanic_orden", "sanic_isactive"]),
            ("Pasos", ["sanic_pasos"]),
            ("Nota", ["sanic_nota"]),
        ],
        "extra": [],
    },
    {
        "logical": "sanic_chtblclienttablelogicalfieldname",
        "label": "Campos editables por RFC",
        "group": "catalogos",
        "view": [("sanic_table", 160), ("sanic_tabledisplayname", 180), ("sanic_logicalname", 200),
                 ("sanic_name", 200), ("sanic_datatype", 130), ("sanic_isallowed", 90),
                 ("sanic_lookuptargettable", 170)],
        "order": [("sanic_table", False), ("sanic_name", False)],
        "form": [
            ("Tabla objetivo", ["sanic_table", "sanic_tabledisplayname", "sanic_tablepluralname"]),
            ("Campo", ["sanic_logicalname", "sanic_name", "sanic_datatype", "sanic_isallowed"]),
            ("Tipos compuestos", ["sanic_lookuptargettable", "sanic_optionsetmap"]),
        ],
        "extra": [],
    },
    {
        "logical": "sanic_chtblmoneydenomination",
        "label": "Denominaciones de moneda",
        "group": "catalogos",
        "view": [("sanic_currency", 120), ("sanic_type", 120), ("sanic_value", 120),
                 ("sanic_name", 240), ("sanic_enabled", 90)],
        "order": [("sanic_currency", False), ("sanic_value", True)],
        "form": [("Denominacion", ["sanic_name", "sanic_currency", "sanic_type", "sanic_value", "sanic_enabled"])],
        "extra": [(
            "CH-Config | Denominaciones deshabilitadas",
            [("sanic_currency", 120), ("sanic_type", 120), ("sanic_value", 120), ("sanic_name", 240)],
            [("sanic_currency", False), ("sanic_value", True)],
            '<filter type="and"><condition attribute="statecode" operator="eq" value="0" />'
            '<condition attribute="sanic_enabled" operator="eq" value="0" /></filter>',
            "Denominaciones que el banco no acepta. Se deshabilitan, NO se borran: "
            "el historico de conteo las referencia.",
        )],
    },
    {
        "logical": "sanic_chtbllocations",
        "label": "Ubicaciones",
        "group": "maestras",
        "view": [("sanic_name", 320), ("createdon", 140), ("modifiedon", 140)],
        "order": [("sanic_name", False)],
        "form": [("Ubicacion", ["sanic_name"])],
        "extra": [],
    },
    {
        "logical": "sanic_chtblcountingdesk",
        "label": "Mesas de conteo",
        "group": "maestras",
        "view": [("sanic_name", 200), ("sanic_cajero", 220), ("sanic_machineserialnumber", 200),
                 ("sanic_enable", 90)],
        "order": [("sanic_name", False)],
        "form": [("Mesa", ["sanic_name", "sanic_cajero", "sanic_machineserialnumber", "sanic_enable"])],
        "extra": [],
    },
    {
        "logical": "sanic_chtbltransportcompany",
        "label": "Transportadoras",
        "group": "maestras",
        "view": [("sanic_name", 320), ("createdon", 140), ("modifiedon", 140)],
        "order": [("sanic_name", False)],
        "form": [("Transportadora", ["sanic_name"])],
        "extra": [],
    },
]

GROUPS = [
    ("params", "Parametros del sistema"),
    ("hub", "Acceso: Hub y modulo RFC"),
    ("email", "Motor de validacion y correo"),
    ("catalogos", "Catalogos de negocio"),
    ("maestras", "Maestras operativas"),
]

# classid de control por tipo de atributo (los de siempre en formxml de Dataverse)
CLASSID = {
    "String": "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}",
    "Memo": "{E0DECE4B-6FC8-4a8f-A065-082708572369}",
    "Picklist": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "State": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "Status": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "Boolean": "{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}",
    "Integer": "{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}",
    "BigInt": "{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}",
    "Decimal": "{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}",
    "Double": "{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}",
    "Money": "{533B9E00-756B-4312-95A0-DC888637AC78}",
    "Lookup": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "Customer": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "Owner": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "DateTime": "{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}",
    "Uniqueidentifier": "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}",
}

MULTILINE = {"Memo"}


def esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
             .replace('"', "&quot;"))


# ---------------------------------------------------------------------------
# 0) metadatos de las tablas (una sola pasada)
# ---------------------------------------------------------------------------
print("== 0. Metadatos de las 12 tablas")
META = {}
for spec in TABLES:
    t = spec["logical"]
    status, ent = call("GET", f"/EntityDefinitions(LogicalName='{t}')"
                              "?$select=MetadataId,SchemaName,ObjectTypeCode,"
                              "PrimaryIdAttribute,PrimaryNameAttribute,OwnershipType")
    must(status, ent, f"metadata {t}")
    attrs = q(f"/EntityDefinitions(LogicalName='{t}')/Attributes",
              select="LogicalName,AttributeType,DisplayName")
    labels, types = {}, {}
    for a in attrs:
        types[a["LogicalName"]] = a["AttributeType"]
        dn = (a.get("DisplayName") or {}).get("UserLocalizedLabel") or {}
        labels[a["LogicalName"]] = dn.get("Label") or a["LogicalName"]
    META[t] = {"ent": ent, "types": types, "labels": labels}
    print(f"  {t:42} otc={ent['ObjectTypeCode']:6} ownership={ent['OwnershipType']}")

SECURABLE = [s["logical"] for s in TABLES if META[s["logical"]]["ent"]["OwnershipType"] != "None"]
UNSECURABLE = [s["logical"] for s in TABLES if META[s["logical"]]["ent"]["OwnershipType"] == "None"]


# ---------------------------------------------------------------------------
# 1) Web resource del icono
# ---------------------------------------------------------------------------
print("\n== 1. Icono (web resource SVG)")
svg_b64 = base64.b64encode(ICON_SRC.read_bytes()).decode()
rows = q("/webresourceset", flt=f"name eq '{ICON_WR}'", select="webresourceid,content")
if rows:
    icon_id = rows[0]["webresourceid"]
    if rows[0].get("content") == svg_b64:
        print(f"  [=] icono sin cambios ({icon_id})")
    elif not DRY:
        must(*call("PATCH", f"/webresourceset({icon_id})", {"content": svg_b64}, solution=True),
             "patch icono")
        print(f"  [~] icono actualizado ({icon_id})")
else:
    if DRY:
        icon_id = "00000000-0000-0000-0000-000000000000"
        print("  [dry] crearia el web resource del icono")
    else:
        st, res = call("POST", "/webresourceset", {
            "name": ICON_WR,
            "displayname": "CashHub | Icono de Configuracion",
            "description": "Icono de CH-App-Configuration. Linea de marca CashHub: "
                           "cuadro rojo #C8102E, glifo blanco de trazo.",
            "webresourcetype": 11,  # 11 = SVG
            "content": svg_b64,
        }, solution=True)
        icon_id = must(st, res, "crear icono")["_id"]
        print(f"  [+] icono creado ({icon_id})")


# ---------------------------------------------------------------------------
# 2 y 3) Vistas
# ---------------------------------------------------------------------------
def build_fetch(logical, cols, order, filter_xml):
    pk = META[logical]["ent"]["PrimaryIdAttribute"]
    names = [pk] + [c for c, _ in cols if c != pk]
    body = "".join(f'<attribute name="{c}" />' for c in dict.fromkeys(names))
    body += "".join(f'<order attribute="{a}" descending="{"true" if d else "false"}" />'
                    for a, d in order)
    return (f'<fetch version="1.0" mapping="logical" output-format="xml-platform" distinct="false">'
            f'<entity name="{logical}">{body}{filter_xml}</entity></fetch>')


def build_layout(logical, cols):
    ent = META[logical]["ent"]
    cells = "".join(f'<cell name="{c}" width="{w}" />' for c, w in cols)
    return (f'<grid name="resultset" object="{ent["ObjectTypeCode"]}" '
            f'jump="{ent["PrimaryNameAttribute"]}" select="1" icon="1" preview="1">'
            f'<row name="result" id="{ent["PrimaryIdAttribute"]}">{cells}</row></grid>')


print("\n== 2. Vistas por defecto (reescritas con columnas utiles)")
view_ids = []
for spec in TABLES:
    t = spec["logical"]
    views = q("/savedqueries", flt=f"returnedtypecode eq '{t}' and querytype eq 0 and isdefault eq true",
              select="savedqueryid,name,fetchxml,layoutxml")
    if not views:
        print(f"  !! {t}: sin vista publica por defecto — se saltea")
        continue
    v = views[0]
    fetch = build_fetch(t, spec["view"], spec["order"], ACTIVE)
    layout = build_layout(t, spec["view"])
    view_ids.append(v["savedqueryid"])
    if v.get("layoutxml") == layout and "savedqueryid" not in (v.get("fetchxml") or "") and v.get("fetchxml") == fetch:
        print(f"  [=] {v['name']}")
        continue
    if DRY:
        print(f"  [dry] reescribiria {v['name']} ({len(spec['view'])} columnas)")
        continue
    st, res = call("PATCH", f"/savedqueries({v['savedqueryid']})",
                   {"fetchxml": fetch, "layoutxml": layout}, solution=True)
    if st >= 300:
        print(f"  !! {v['name']}: {res.get('error')}")
    else:
        print(f"  [~] {v['name']} -> {len(spec['view'])} columnas")

print("\n== 3. Vistas operativas extra")
for spec in TABLES:
    t = spec["logical"]
    for name, cols, order, filter_xml, desc in spec["extra"]:
        fetch = build_fetch(t, cols, order, filter_xml)
        layout = build_layout(t, cols)
        rows = q("/savedqueries", flt=f"returnedtypecode eq '{t}' and name eq '{name}'",
                 select="savedqueryid")
        if rows:
            view_ids.append(rows[0]["savedqueryid"])
            if DRY:
                print(f"  [dry] actualizaria '{name}'")
                continue
            st, res = call("PATCH", f"/savedqueries({rows[0]['savedqueryid']})",
                           {"fetchxml": fetch, "layoutxml": layout, "description": desc},
                           solution=True)
            print(f"  [~] {name}" if st < 300 else f"  !! {name}: {res.get('error')}")
        else:
            if DRY:
                print(f"  [dry] crearia '{name}'")
                continue
            st, res = call("POST", "/savedqueries", {
                "name": name, "description": desc, "returnedtypecode": t,
                "querytype": 0, "fetchxml": fetch, "layoutxml": layout,
                "isdefault": False, "statecode": 0,
            }, solution=True)
            if st >= 300:
                print(f"  !! {name}: {res.get('error')}")
            else:
                view_ids.append(res["_id"])
                print(f"  [+] {name}")


# ---------------------------------------------------------------------------
# 4) Formularios principales
# ---------------------------------------------------------------------------
def build_form(logical, sections):
    types, labels = META[logical]["types"], META[logical]["labels"]
    out = []
    for si, (title, fields) in enumerate(sections):
        rows_xml = []
        for f in fields:
            at = types.get(f)
            if at is None:
                print(f"  !! {logical}: columna inexistente en el form spec: {f}")
                continue
            cid = CLASSID.get(at, CLASSID["String"])
            span = ' rowspan="4" colspan="1"' if at in MULTILINE else ""
            rows_xml.append(
                f'<row><cell id="{det(logical, "cell", f)}" showlabel="true"{span}>'
                f'<labels><label description="{esc(labels.get(f, f))}" languagecode="{LCID}" /></labels>'
                f'<control id="{f}" classid="{cid}" datafieldname="{f}" disabled="false" />'
                f'</cell></row>')
        out.append(
            f'<section id="{det(logical, "sec", str(si))}" name="sec_{si}" showlabel="true" '
            f'showbar="false" IsUserDefined="0" layout="varwidth" columns="1">'
            f'<labels><label description="{esc(title)}" languagecode="{LCID}" /></labels>'
            f'<rows>{"".join(rows_xml)}</rows></section>')
    return (f'<form><tabs><tab verticallayout="true" id="{det(logical, "tab")}" '
            f'name="tab_general" IsUserDefined="1" expanded="true">'
            f'<labels><label description="General" languagecode="{LCID}" /></labels>'
            f'<columns><column width="100%"><sections>{"".join(out)}</sections></column></columns>'
            f'</tab></tabs></form>')


print("\n== 4. Formularios principales (secciones + todos los campos de config)")
form_ids = []
for spec in TABLES:
    t = spec["logical"]
    forms = q("/systemforms", flt=f"objecttypecode eq '{t}' and type eq 2",
              select="formid,name,formxml")
    if not forms:
        print(f"  !! {t}: sin formulario principal — se saltea")
        continue
    f0 = forms[0]
    xml = build_form(t, spec["form"])
    form_ids.append(f0["formid"])
    if f0.get("formxml") == xml:
        print(f"  [=] {t}")
        continue
    if DRY:
        n = sum(len(s[1]) for s in spec["form"])
        print(f"  [dry] reescribiria form de {t} ({len(spec['form'])} secciones, {n} campos)")
        continue
    st, res = call("PATCH", f"/systemforms({f0['formid']})",
                   {"formxml": xml, "description": f"Formulario de configuracion de {spec['label']}."},
                   solution=True)
    if st >= 300:
        print(f"  !! {t}: {res.get('error')}")
    else:
        n = sum(len(s[1]) for s in spec["form"])
        print(f"  [~] {t} -> {len(spec['form'])} secciones / {n} campos")


# ---------------------------------------------------------------------------
# 5) SiteMap + AppModule
# ---------------------------------------------------------------------------
def build_sitemap():
    groups_xml = []
    for gid, gtitle in GROUPS:
        subs = []
        for spec in TABLES:
            if spec["group"] != gid:
                continue
            subs.append(
                f'<SubArea Id="sub_{spec["logical"][6:][:28]}" Entity="{spec["logical"]}" '
                f'Client="All" AvailableOffline="false" PassParams="false" Sku="All,OnPremise,Live,SPLA">'
                f'<Titles><Title LCID="{LCID}" Title="{esc(spec["label"])}" /></Titles></SubArea>')
        groups_xml.append(
            f'<Group Id="grp_{gid}" ResourceId="SitemapDesigner.NewGroup" '
            f'DescriptionResourceId="SitemapDesigner.NewGroup" IntroducedVersion="7.0.0.0" '
            f'IsProfile="false" ToolTipResourseId="SitemapDesigner.Unknown">'
            f'<Titles><Title LCID="{LCID}" Title="{esc(gtitle)}" /></Titles>'
            f'{"".join(subs)}</Group>')
    return ('<SiteMap IntroducedVersion="7.0.0.0">'
            '<Area Id="area_chconfig" ResourceId="SitemapDesigner.NewTitle" '
            'DescriptionResourceId="SitemapDesigner.NewTitle" ShowGroups="true" '
            'IntroducedVersion="7.0.0.0">'
            f'<Titles><Title LCID="{LCID}" Title="Configuracion" /></Titles>'
            f'{"".join(groups_xml)}</Area></SiteMap>')


print("\n== 5. SiteMap + AppModule")
sitemap_xml = build_sitemap()
rows = q("/sitemaps", flt=f"sitemapnameunique eq '{APP_UNIQUE}'", select="sitemapid,sitemapxml")
if rows:
    sitemap_id = rows[0]["sitemapid"]
    if rows[0].get("sitemapxml") == sitemap_xml:
        print(f"  [=] sitemap sin cambios ({sitemap_id})")
    elif not DRY:
        must(*call("PATCH", f"/sitemaps({sitemap_id})", {"sitemapxml": sitemap_xml}, solution=True),
             "patch sitemap")
        print(f"  [~] sitemap actualizado ({sitemap_id})")
elif DRY:
    sitemap_id = None
    print("  [dry] crearia el sitemap")
else:
    st, res = call("POST", "/sitemaps", {
        "sitemapname": APP_NAME,
        "sitemapnameunique": APP_UNIQUE,
        "sitemapxml": sitemap_xml,
        "isappaware": True,
    }, solution=True, prefer_rep=True)
    sitemap_id = must(st, res, "crear sitemap").get("sitemapid") or res["_id"]
    print(f"  [+] sitemap creado ({sitemap_id})")

# OJO — un appmodule SIN PUBLICAR no lo devuelve /appmodules ni el retrieve por id
# (404), pero SI existe: reserva el uniquename y aparece en solutioncomponents.
# Por eso la busqueda tiene dos patas: la publicada y la que todavia no lo esta.
def find_appmodule():
    rows = q("/appmodules", flt=f"uniquename eq '{APP_UNIQUE}'",
             select="appmoduleid,name,webresourceid")
    if rows:
        return rows[0]["appmoduleid"], True
    published = {a["appmoduleid"].lower() for a in q("/appmodules", select="appmoduleid")}
    sid = q("/solutions", flt=f"uniquename eq '{SOLUTION}'", select="solutionid")[0]["solutionid"]
    huerfanos = [c["objectid"] for c in q("/solutioncomponents",
                                          flt=f"componenttype eq 80 and _solutionid_value eq {sid}",
                                          select="objectid")
                 if c["objectid"].lower() not in published]
    if len(huerfanos) == 1:
        return huerfanos[0], False
    if len(huerfanos) > 1:
        print(f"  !! {len(huerfanos)} appmodules sin publicar en {SOLUTION}: "
              "no puedo decidir cual es. Resolver a mano.")
        sys.exit(1)
    return None, False


if DRY:
    app_id, app_pub = None, False
    print("  [dry] crearia/actualizaria el appmodule")
else:
    app_id, app_pub = find_appmodule()
    if app_id:
        st, res = call("PATCH", f"/appmodules({app_id})", {
            "name": APP_NAME, "description": APP_DESC, "webresourceid": icon_id,
        }, solution=True)
        estado = "publicada" if app_pub else "sin publicar todavia"
        print(f"  [=] app existente ({app_id}, {estado})"
              + ("" if st < 300 else f" — patch fallo: {res.get('error')[:120]}"))
    else:
        st, res = call("POST", "/appmodules", {
            "name": APP_NAME,
            "uniquename": APP_UNIQUE,
            "description": APP_DESC,
            "clienttype": 4,          # Unified Interface
            "navigationtype": 0,      # navegacion clasica por areas/grupos
            "formfactor": 1,
            "isdefault": False,
            "isfeatured": False,
            "webresourceid": icon_id,  # SystemRequired: sin icono no se crea
        }, solution=True)
        app_id = must(st, res, "crear appmodule")["_id"]
        print(f"  [+] app creada ({app_id})")

    # AddAppComponents es la unica via que acepta la API: appmodulecomponents no
    # expone appmoduleidunique como navigation property. Es idempotente del lado
    # del servidor (re-agregar un componente ya presente no duplica).
    comps = [{"@odata.type": "Microsoft.Dynamics.CRM.sitemap", "sitemapid": sitemap_id}]
    comps += [{"@odata.type": "Microsoft.Dynamics.CRM.entity",
               "entityid": META[s["logical"]]["ent"]["MetadataId"]} for s in TABLES]
    st, res = call("POST", "/AddAppComponents", {"AppId": app_id, "Components": comps})
    if st >= 300:
        print(f"  !! AddAppComponents: {res.get('error')[:220]}")
    else:
        print(f"  componentes de la app: 1 sitemap + {len(TABLES)} tablas")


# ---------------------------------------------------------------------------
# 6) Rol CH-SR-ConfigAdmin
# ---------------------------------------------------------------------------
print("\n== 6. Rol " + ROLE_NAME)
ACTIONS = ["Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share"]

if DRY:
    print(f"  [dry] crearia/actualizaria el rol con CRUD sobre {len(SECURABLE)} tablas")
    role_id = None
else:
    root_bu = q("/businessunits", flt="_parentbusinessunitid_value eq null",
                select="businessunitid")[0]["businessunitid"]
    roles = q("/roles", flt=f"name eq '{ROLE_NAME}' and _businessunitid_value eq {root_bu}",
              select="roleid")
    if roles:
        role_id = roles[0]["roleid"]
        print(f"  [=] rol existente ({role_id})")
    else:
        st, res = call("POST", "/roles", {
            "name": ROLE_NAME,
            "businessunitid@odata.bind": f"/businessunits({root_bu})",
            "description": "Administrador de la configuracion de CashHub. CRUD Global sobre "
                           "las tablas de parametros/catalogos y acceso a CH-App-Configuration. "
                           "NO otorga privilegios sobre datos operativos (depositos, recuentos, "
                           "transacciones): esto configura el sistema, no lo opera.",
        }, solution=True, prefer_rep=True)
        role_id = must(st, res, "crear rol").get("roleid") or res["_id"]
        print(f"  [+] rol creado ({role_id})")

    call("POST", "/AddSolutionComponent", {
        "ComponentType": 20, "ComponentId": role_id,
        "SolutionUniqueName": SOLUTION, "AddRequiredComponents": False,
    })

    granted = {p["privilegeid"] for p in q(f"/roles({role_id})/roleprivileges_association",
                                           select="privilegeid")}

    def depth_for(p):
        if p.get("canbeglobal"):
            return "Global"
        if p.get("canbedeep"):
            return "Deep"
        if p.get("canbelocal"):
            return "Local"
        return "Basic"

    to_grant, skipped = [], []
    for t in SECURABLE:
        schema = META[t]["ent"]["SchemaName"]
        found = q("/privileges", flt=f"contains(name,'{schema}')",
                  select="privilegeid,name,canbeglobal,canbedeep,canbelocal,canbebasic")
        by_action = {p["name"].lower(): p for p in found}
        for act in ACTIONS:
            p = by_action.get(f"prv{act}{schema}".lower())
            if not p:
                skipped.append(f"prv{act}{schema}")
                continue
            if p["privilegeid"] in granted:
                continue
            to_grant.append({"PrivilegeId": p["privilegeid"], "Depth": depth_for(p)})

    # Plataforma: abrir la app, leer vistas/formularios/webresources, resolver el
    # lookup de cajero (sanic_cajero -> systemuser) en Mesas de conteo.
    for pname in ["prvReadAppModule", "prvReadWebResource", "prvReadQuery",
                  "prvCreateUserQuery", "prvReadUserQuery", "prvWriteUserQuery",
                  "prvDeleteUserQuery", "prvReadUser", "prvReadBusinessUnit",
                  "prvReadTeam"]:
        found = q("/privileges", flt=f"name eq '{pname}'",
                  select="privilegeid,name,canbeglobal,canbedeep,canbelocal,canbebasic")
        if not found:
            skipped.append(pname)
            continue
        p = found[0]
        if p["privilegeid"] in granted:
            continue
        to_grant.append({"PrivilegeId": p["privilegeid"], "Depth": depth_for(p)})

    if to_grant:
        st, res = call("POST", f"/roles({role_id})/Microsoft.Dynamics.CRM.AddPrivilegesRole",
                       {"Privileges": to_grant})
        if st >= 300:
            print(f"  !! AddPrivilegesRole: {res.get('error')}")
        else:
            print(f"  [+] {len(to_grant)} privilegios otorgados")
    else:
        print("  [=] privilegios ya otorgados")
    if skipped:
        print(f"  !! privilegios inexistentes (no se pudo otorgar): {', '.join(skipped)}")


# ---------------------------------------------------------------------------
# 7) Solucion + publish   (el rol se asocia DESPUES de publicar: la N:N contra
#     un appmodule sin publicar no resuelve)
# ---------------------------------------------------------------------------
if not DRY:
    print(f"\n== 7. Registrar en {SOLUTION} y publicar")
    comps = ([(26, v) for v in dict.fromkeys(view_ids)]
             + [(60, f) for f in dict.fromkeys(form_ids)]
             + [(61, icon_id), (62, sitemap_id), (80, app_id)])
    ok = fail = 0
    for ctype, cid in comps:
        st, res = call("POST", "/AddSolutionComponent", {
            "ComponentType": ctype, "ComponentId": cid,
            "SolutionUniqueName": SOLUTION, "AddRequiredComponents": False,
        })
        if st >= 300:
            fail += 1
            print(f"  !! ctype={ctype} {cid}: {res.get('error')[:180]}")
        else:
            ok += 1
    print(f"  componentes registrados en {SOLUTION}: {ok} ok, {fail} con error")

    ents = "".join(f"<entity>{s['logical']}</entity>" for s in TABLES)
    pub = ("<importexportxml>"
           f"<entities>{ents}</entities>"
           f"<webresources><webresource>{icon_id}</webresource></webresources>"
           f"<sitemaps><sitemap>{sitemap_id}</sitemap></sitemaps>"
           f"<appmodules><appmodule>{app_id}</appmodule></appmodules>"
           "</importexportxml>")
    st, res = call("POST", "/PublishXml", {"ParameterXml": pub})
    print("  publicado" if st < 300 else f"  !! publish: {res.get('error')}")

    # acceso a la app: N:N appmoduleroles (recien ahora que la app esta publicada)
    assoc = q(f"/appmodules({app_id})/appmoduleroles_association",
              flt=f"roleid eq {role_id}", select="roleid")
    if assoc:
        print("  [=] rol ya asociado a la app")
    else:
        st2, res2 = call("POST", f"/appmodules({app_id})/appmoduleroles_association/$ref",
                         {"@odata.id": f"{BASE}{API}/roles({role_id})"})
        print("  [+] rol asociado a la app" if st2 < 300
              else f"  !! asociar rol: {res2.get('error')[:200]}")

    st, res = call("GET", f"/ValidateApp(AppModuleId={app_id})")
    if st < 300:
        errs = [r for r in res.get("AppValidationResponse", {}).get("ValidationIssueList", [])
                if r.get("ErrorType") == "Error"]
        warns = [r for r in res.get("AppValidationResponse", {}).get("ValidationIssueList", [])
                 if r.get("ErrorType") != "Error"]
        print(f"  ValidateApp: {len(errs)} errores, {len(warns)} advertencias")
        for r in (errs + warns)[:12]:
            print(f"    - [{r.get('ErrorType')}] {r.get('Message')}")
    else:
        print(f"  ValidateApp no disponible: {res.get('error')[:160]}")

print("\n" + "=" * 76)
if UNSECURABLE:
    print("AVISO DE SEGURIDAD — tablas sin privilegios (OwnershipType=None):")
    for t in UNSECURABLE:
        print(f"  * {t}")
    print("  Dataverse NO genera privilegios para tablas con ownership 'None', asi que")
    print("  NINGUN rol puede otorgar acceso: hoy solo System Administrator/Customizer")
    print("  las escribe con seguridad. Aparecen igual en la app, pero un usuario que")
    print("  solo tenga " + ROLE_NAME + " puede no poder editarlas.")
    print("  Remediacion real: recrear la tabla como OrganizationOwned y migrar las filas.")
print(f"App: {APP_NAME} ({APP_UNIQUE})   Rol: {ROLE_NAME}   Solucion: {SOLUTION}")
print("Falta a mano: asignar el rol a los usuarios/teams que administran configuracion.")
