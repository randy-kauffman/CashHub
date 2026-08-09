#!/usr/bin/env python3
"""Genera los archivos de datasource de una tabla Dataverse para una canvas app desempaquetada.

El pack de una canvas app necesita, por tabla: DataSources/<Nombre>.json (NativeCDSDataSourceInfo
+ ViewInfo + OptionSetInfos) y pkgs/TableDefinitions/<Nombre>.json (cache de respuestas del Web
API que el Studio refresca al abrir). Este script las construye desde la metadata viva del
entorno, espejando las queries que el Studio cachea (verificado contra los @odata.context de los
blobs existentes).

Uso: TOKEN=... DATAVERSE_URL=... python3 generate-canvas-datasource.py <logicalname> <app_src_dir>
"""
import json
import os
import subprocess
import sys
import urllib.parse

LOGICAL = sys.argv[1]
APP_DIR = sys.argv[2]
BASE = os.environ['DATAVERSE_URL']

def get(path, version='v9.0'):
    url = f"{BASE}/api/data/{version}{path}"
    out = subprocess.run(
        ['curl', '-sf', '-H', f"Authorization: Bearer {os.environ['TOKEN']}",
         '-H', 'Accept: application/json', url],
        capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError(f"GET {path} fallo: {out.stderr[:200]}")
    return out.stdout

def q(s):
    return urllib.parse.quote(s, safe="(),'$=&")

meta = json.loads(get(
    f"/EntityDefinitions(LogicalName='{LOGICAL}')"
    "?$select=MetadataId,LogicalName,DisplayCollectionName,EntitySetName,Description,HasNotes,"
    "IsActivity,IsIntersect,IsManaged,TableType,IsPrivate,IsLogicalEntity,PrimaryIdAttribute,"
    "PrimaryNameAttribute,ObjectTypeCode,OwnershipType,Privileges,IsAvailableOffline,"
    "IsOfflineInMobileClient&$expand=Attributes,ManyToOneRelationships,OneToManyRelationships,"
    "ManyToManyRelationships"))

display = meta['DisplayCollectionName']['UserLocalizedLabel']['Label']
entityset = meta['EntitySetName']

def label(attr):
    dn = attr.get('DisplayName') or {}
    ull = dn.get('UserLocalizedLabel')
    return ull['Label'] if ull else None

# --- NameMapping: atributos con display name + colecciones 1:N de sistema (mismo set que
# --- genera el Studio para las tablas ya presentes en la app) ---
name_mapping = {}
for attr in meta['Attributes']:
    lbl = label(attr)
    if not lbl:
        continue
    logical = attr['LogicalName']
    key = '_ownerid_value' if logical == 'ownerid' else logical
    name_mapping[key] = lbl

SYSTEM_1N = {
    'AsyncOperations': 'System Jobs',
    'BulkDeleteFailures': 'Bulk Delete Failures',
    'MailboxTrackingFolders': 'Mailbox Auto Tracking Folders',
    'PrincipalObjectAttributeAccesses': 'Field Sharing',
    'ProcessSession': 'Process Sessions',
    'SyncErrors': 'Sync Errors',
}
for rel in meta['OneToManyRelationships']:
    schema = rel.get('SchemaName', '')
    for suffix, lbl in SYSTEM_1N.items():
        if schema == f'{LOGICAL}_{suffix}':
            name_mapping[schema] = lbl

views_raw = get(f"/savedqueries?$select=savedqueryid,name,querytype&$filter={q(f'returnedtypecode eq %27{LOGICAL}%27'.replace('%27', chr(39)))}")
views = json.loads(views_raw)
view_mapping = {v['savedqueryid']: v['name'] for v in views['value']}

def optionset_block(column, entity_display, mapping, type_key, is_boolean):
    return {
        'DisplayName': f'{name_mapping.get(column, column)} ({entity_display})',
        'Name': f'{LOGICAL}_{column}',
        'OptionSetInfoNameMapping': mapping,
        'OptionSetIsBooleanValued': is_boolean,
        'OptionSetIsGlobal': False,
        'OptionSetReference': {
            'OptionSetReferenceItem0': {
                'OptionSetReferenceColumnName': column,
                'OptionSetReferenceEntityName': entity_display,
            }
        },
        'OptionSetTypeKey': type_key,
        'RelatedColumnInvariantName': column,
        'RelatedEntityName': entity_display,
        'Type': 'OptionSetInfo',
    }

state_attr = json.loads(get(
    f"/EntityDefinitions(LogicalName='{LOGICAL}')/Attributes/Microsoft.Dynamics.CRM.StateAttributeMetadata"
    "?$select=MetadataId,LogicalName,SchemaName,DisplayName&$expand=OptionSet"))
status_attr = json.loads(get(
    f"/EntityDefinitions(LogicalName='{LOGICAL}')/Attributes/Microsoft.Dynamics.CRM.StatusAttributeMetadata"
    "?$select=MetadataId,LogicalName,SchemaName,DisplayName&$expand=OptionSet"))

def optionset_mapping(attr_response):
    opts = attr_response['value'][0]['OptionSet']['Options']
    return {str(o['Value']): o['Label']['UserLocalizedLabel']['Label'] for o in opts}

datasource = [
    {
        'CdsFCBContext': '{"FileType":true,"ImageExternalStorage":true}',
        'EntitySetName': entityset,
        'IsHidden': False,
        'IsSampleData': False,
        'IsWritable': True,
        'LogicalName': LOGICAL,
        'MaxGetRowsCount': 500,
        'Name': display,
        'NativeCDSDataSourceInfoNameMapping': dict(sorted(name_mapping.items())),
        'PreferredName': display,
        'Type': 'NativeCDSDataSourceInfo',
    },
    {
        'DisplayName': f'{display} (Views)',
        'Name': f'_{LOGICAL}_views',
        'RelatedEntityName': display,
        'TrimmedViewName': True,
        'Type': 'ViewInfo',
        'ViewInfoNameMapping': view_mapping,
    },
    optionset_block('statecode', display, optionset_mapping(state_attr), 'StateType', False),
    optionset_block('statuscode', display, optionset_mapping(status_attr), 'StatusType', False),
]

# --- TableDefinitions: cache de las mismas queries que guarda el Studio ---
def attr_query(odata_type):
    return get(
        f"/EntityDefinitions(LogicalName='{LOGICAL}')/Attributes/Microsoft.Dynamics.CRM.{odata_type}"
        "?$select=MetadataId,LogicalName,SchemaName,DisplayName&$expand=OptionSet")

default_view = get(
    f"/savedqueries?$select=layoutxml&$filter=returnedtypecode eq '{LOGICAL}' and isdefault eq true and querytype eq 0"
    .replace(' ', '%20').replace("'", '%27'))
forms = get(
    f"/systemforms?$select=name,formid&$filter=objecttypecode eq '{LOGICAL}' and type eq 2"
    .replace(' ', '%20').replace("'", '%27'))

entity_meta_raw = get(
    f"/EntityDefinitions(LogicalName='{LOGICAL}')"
    "?$select=MetadataId,LogicalName,DisplayCollectionName,EntitySetName,Description,HasNotes,"
    "IsActivity,IsIntersect,IsManaged,TableType,IsPrivate,IsLogicalEntity,PrimaryIdAttribute,"
    "PrimaryNameAttribute,ObjectTypeCode,OwnershipType,Privileges,IsAvailableOffline,"
    "IsOfflineInMobileClient&$expand=Attributes,ManyToOneRelationships,OneToManyRelationships,"
    "ManyToManyRelationships")

table_definitions = {
    'DatasetName': 'default.cds',
    'EntityName': display,
    'environmentVariableName': '',
    'InstanceUrl': BASE.rstrip('/') + '/',
    'LocalReferenceDSJson': {'entitySetName': entityset, 'logicalName': LOGICAL},
    'state': 'Configured',
    'TableDefinition': {
        'BooleanOptionSetAttribute': attr_query('BooleanAttributeMetadata'),
        'DefaultPublicView': default_view,
        'EntityMetadata': entity_meta_raw,
        'EntityNameOptionSetAttribute': attr_query('EntityNameAttributeMetadata'),
        'Forms': forms,
        'IconUrl': '',
        'MultiSelectPicklistOptionSetAttribute': attr_query('MultiSelectPicklistAttributeMetadata'),
        'PicklistOptionSetAttribute': attr_query('PicklistAttributeMetadata'),
        'StateOptionSetAttribute': attr_query('StateAttributeMetadata'),
        'StatusOptionSetAttribute': attr_query('StatusAttributeMetadata'),
        'TableName': LOGICAL,
        'Views': views_raw,
    },
    'UnusedDataSources': {},
    'webApiVersion': 'v9.0',
}

ds_path = os.path.join(APP_DIR, 'DataSources', f'{display}.json')
td_path = os.path.join(APP_DIR, 'pkgs', 'TableDefinitions', f'{display}.json')
with open(ds_path, 'w', encoding='utf-8') as f:
    json.dump(datasource, f, indent=2, ensure_ascii=False)
with open(td_path, 'w', encoding='utf-8') as f:
    json.dump(table_definitions, f, indent=2, ensure_ascii=False)
print('escrito:', ds_path)
print('escrito:', td_path)
print('name mapping:', json.dumps(dict(sorted(name_mapping.items())), ensure_ascii=False)[:400])
