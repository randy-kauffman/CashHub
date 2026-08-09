#!/usr/bin/env python3
"""
Provisiona CH-Tbl-Parameter (sanic_chtblparameter) en la solucion CashHubCore.

Tabla de configuracion key/value para consolidar parametros de CashHub
(grupos AAD, umbrales, listas, flags) en vez de multiplicar environment variables.

Idempotente: se puede correr N veces. No borra nada. No toca env vars.
Scope: exclusivamente CashHub (publisher sanic, solucion CashHubCore).

Uso:  python3 scripts/provision-parameters-table.py
"""
import json
import sys
import urllib.request
import urllib.parse
import urllib.error
from pathlib import Path

SOLUTION = "CashHubCore"
ENTITY_LOGICAL = "sanic_chtblparameter"
ENTITY_SET = "sanic_chtblparameters"
API = "/api/data/v9.2"

# ----------------------------------------------------------------------------
# credenciales desde pp_secrets.env
# ----------------------------------------------------------------------------
def load_env():
    env = {}
    p = Path(__file__).resolve().parent.parent / "pp_secrets.env"
    for line in p.read_text().splitlines():
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

def call(method, path, body=None, solution_aware=True):
    url = BASE + API + path
    headers = {
        "Authorization": f"Bearer {TOKEN}",
        "Accept": "application/json",
        "OData-MaxVersion": "4.0",
        "OData-Version": "4.0",
    }
    if solution_aware:
        headers["MSCRM.SolutionUniqueName"] = SOLUTION
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else {})
    except urllib.error.HTTPError as e:
        return e.code, {"error": e.read().decode()}

def label(text, lc=1033):
    return {
        "@odata.type": "Microsoft.Dynamics.CRM.Label",
        "LocalizedLabels": [{
            "@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel",
            "Label": text, "LanguageCode": lc,
        }],
    }

def option(value, text):
    return {
        "@odata.type": "Microsoft.Dynamics.CRM.OptionMetadata",
        "Value": value, "Label": label(text),
    }

def exists(path):
    code, _ = call("GET", path)
    return code == 200

# ----------------------------------------------------------------------------
# 1) Entidad + columna primaria (Key)
# ----------------------------------------------------------------------------
def create_entity():
    if exists(f"/EntityDefinitions(LogicalName='{ENTITY_LOGICAL}')?$select=LogicalName"):
        print("  [=] entidad ya existe, skip create")
        return
    body = {
        "@odata.type": "Microsoft.Dynamics.CRM.EntityMetadata",
        "SchemaName": "sanic_CHTblParameter",
        "DisplayName": label("CH-Tbl-Parameter"),
        "DisplayCollectionName": label("CH-Tbl-Parameters"),
        "Description": label("Tabla de configuracion key/value de CashHub."),
        "OwnershipType": "None",  # None = organization-owned
        "IsActivity": False,
        "HasActivities": False,
        "HasNotes": False,
        "Attributes": [{
            "@odata.type": "Microsoft.Dynamics.CRM.StringAttributeMetadata",
            "AttributeType": "String",
            "AttributeTypeName": {"Value": "StringType"},
            "SchemaName": "sanic_Name",
            "IsPrimaryName": True,
            "MaxLength": 100,
            "RequiredLevel": {"Value": "ApplicationRequired"},
            "DisplayName": label("Key"),
            "Description": label("Identificador unico. Convencion: Dominio.Sub.Nombre"),
        }],
    }
    code, res = call("POST", "/EntityDefinitions", body)
    print(f"  [+] create entidad -> HTTP {code}" + ("" if code < 300 else f"\n{res.get('error')}"))
    if code >= 300:
        sys.exit(1)

# ----------------------------------------------------------------------------
# 2) Columnas
# ----------------------------------------------------------------------------
def add_attribute(logical, body):
    if exists(f"/EntityDefinitions(LogicalName='{ENTITY_LOGICAL}')/Attributes(LogicalName='{logical}')?$select=LogicalName"):
        print(f"  [=] columna {logical} ya existe, skip")
        return
    code, res = call("POST", f"/EntityDefinitions(LogicalName='{ENTITY_LOGICAL}')/Attributes", body)
    print(f"  [+] columna {logical} -> HTTP {code}" + ("" if code < 300 else f"\n{res.get('error')}"))

def add_columns():
    add_attribute("sanic_value", {
        "@odata.type": "Microsoft.Dynamics.CRM.MemoAttributeMetadata",
        "AttributeTypeName": {"Value": "MemoType"},
        "SchemaName": "sanic_Value", "MaxLength": 4000, "Format": "TextArea",
        "RequiredLevel": {"Value": "ApplicationRequired"},
        "DisplayName": label("Value"),
        "Description": label("Valor crudo: GUID, lista, numero, JSON, email."),
    })
    add_attribute("sanic_category", {
        "@odata.type": "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
        "AttributeTypeName": {"Value": "PicklistType"},
        "SchemaName": "sanic_Category",
        "RequiredLevel": {"Value": "None"},
        "DisplayName": label("Category"),
        "OptionSet": {
            "@odata.type": "Microsoft.Dynamics.CRM.OptionSetMetadata",
            "IsGlobal": False, "OptionSetType": "Picklist",
            "Options": [
                option(159460000, "Security"), option(159460001, "Email"),
                option(159460002, "Validation"), option(159460003, "Workflow"),
                option(159460004, "Integration"), option(159460005, "General"),
            ],
        },
    })
    add_attribute("sanic_datatype", {
        "@odata.type": "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
        "AttributeTypeName": {"Value": "PicklistType"},
        "SchemaName": "sanic_DataType",
        "RequiredLevel": {"Value": "ApplicationRequired"},
        "DefaultFormValue": 159460000,
        "DisplayName": label("Data Type"),
        "OptionSet": {
            "@odata.type": "Microsoft.Dynamics.CRM.OptionSetMetadata",
            "IsGlobal": False, "OptionSetType": "Picklist",
            "Options": [
                option(159460000, "String"), option(159460001, "Number"),
                option(159460002, "Boolean"), option(159460003, "Guid"),
                option(159460004, "GuidList"), option(159460005, "Json"),
                option(159460006, "Email"), option(159460007, "Url"),
            ],
        },
    })
    add_attribute("sanic_description", {
        "@odata.type": "Microsoft.Dynamics.CRM.MemoAttributeMetadata",
        "AttributeTypeName": {"Value": "MemoType"},
        "SchemaName": "sanic_Description", "MaxLength": 2000, "Format": "TextArea",
        "RequiredLevel": {"Value": "None"},
        "DisplayName": label("Description"),
        "Description": label("Que es y quien lo consume."),
    })
    add_attribute("sanic_isactive", {
        "@odata.type": "Microsoft.Dynamics.CRM.BooleanAttributeMetadata",
        "AttributeTypeName": {"Value": "BooleanType"},
        "SchemaName": "sanic_IsActive",
        "RequiredLevel": {"Value": "None"},
        "DefaultValue": True,
        "DisplayName": label("Is Active"),
        "OptionSet": {
            "@odata.type": "Microsoft.Dynamics.CRM.BooleanOptionSetMetadata",
            "TrueOption": option(1, "Yes"),
            "FalseOption": option(0, "No"),
        },
    })

# ----------------------------------------------------------------------------
# 3) Alternate key en Key (unicidad + retrieve-by-key)
# ----------------------------------------------------------------------------
def add_alternate_key():
    code, res = call("GET", f"/EntityDefinitions(LogicalName='{ENTITY_LOGICAL}')/Keys?$select=SchemaName")
    keys = [k.get("SchemaName") for k in res.get("value", [])] if code == 200 else []
    if "sanic_ParameterKey" in keys:
        print("  [=] alternate key ya existe, skip")
        return
    body = {
        "@odata.type": "Microsoft.Dynamics.CRM.EntityKeyMetadata",
        "SchemaName": "sanic_ParameterKey",
        "DisplayName": label("Parameter Key"),
        "KeyAttributes": ["sanic_name"],
    }
    code, res = call("POST", f"/EntityDefinitions(LogicalName='{ENTITY_LOGICAL}')/Keys", body)
    print(f"  [+] alternate key -> HTTP {code}" + ("" if code < 300 else f"\n{res.get('error')}"))

# ----------------------------------------------------------------------------
# 4) Publish
# ----------------------------------------------------------------------------
def publish():
    code, res = call("POST", "/PublishAllXml", {}, solution_aware=False)
    print(f"  [+] PublishAllXml -> HTTP {code}")

# ----------------------------------------------------------------------------
# 5) Seed de ejemplo (idempotente por sanic_name). No pisa nada existente.
# ----------------------------------------------------------------------------
SEED = [
    {
        "sanic_name": "Security.ApproverGroup.Banca",
        "sanic_value": "17b5e5c7-e7a0-4b61-98cd-ceff67211797",
        "sanic_category": 159460000, "sanic_datatype": 159460003, "sanic_isactive": True,
        "sanic_description": "AAD group cuyos miembros aprueban RFCs de la mesa Banca.",
    },
    {
        "sanic_name": "Security.ApproverGroup.Boveda",
        "sanic_value": "4756326a-bf57-4ba2-8cc1-3c024957b55a",
        "sanic_category": 159460000, "sanic_datatype": 159460003, "sanic_isactive": True,
        "sanic_description": "AAD group cuyos miembros aprueban RFCs de la mesa Boveda.",
    },
]

def seed():
    for row in SEED:
        key = row["sanic_name"]
        q = urllib.parse.quote(f"sanic_name eq '{key}'")
        code, res = call("GET", f"/{ENTITY_SET}?$select=sanic_name&$filter={q}", solution_aware=False)
        if code == 200 and res.get("value"):
            print(f"  [=] seed '{key}' ya existe, skip")
            continue
        code, res = call("POST", f"/{ENTITY_SET}", row, solution_aware=False)
        print(f"  [+] seed '{key}' -> HTTP {code}" + ("" if code < 300 else f"\n{res.get('error')}"))

# ----------------------------------------------------------------------------
if __name__ == "__main__":
    print(f"Provisionando {ENTITY_LOGICAL} en solucion {SOLUTION} ...")
    create_entity()
    add_columns()
    add_alternate_key()
    publish()
    seed()
    print("Listo.")
