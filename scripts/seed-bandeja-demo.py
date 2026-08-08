#!/usr/bin/env python3
# =============================================================================
# Datos de DEMO para ver la bandeja de trabajo de CH-App-IngestionConsole.
#
# Crea un correo por cada categoria, con su corrida de validacion y sus
# resultados, para que el panel de detalle se vea completo: el motivo del
# triage, las reglas que fallaron con su celda, y las que no se intentaron.
#
# Todos usan InternetMessageId <dm-XXX@demo.cashhub.test>, asi que se
# reconocen y se pueden borrar de una:
#   sanic_receivedemailinternetmessageid contiene '@demo.cashhub.test'
#
# NO correr en produccion.
# Uso:  TOKEN=... DATAVERSE_URL=... python3 scripts/seed-bandeja-demo.py
# =============================================================================
import json,os,subprocess,datetime
API=os.environ['DATAVERSE_URL']+'/api/data/v9.2'
H=['-H',f"Authorization: Bearer {os.environ['TOKEN']}",'-H','Content-Type: application/json',
   '-H','Prefer: return=representation']
def req(m,p,b=None):
    c=['curl','-s',*H,'-X',m,API+p]
    if b is not None: c+=['-d',json.dumps(b)]
    o=subprocess.run(c,capture_output=True,text=True).stdout
    return json.loads(o) if o.strip() else {}

CLI={'porvenir':'6d195e27-bb90-f111-b8da-002248097b5c','colonia':'6f195e27-bb90-f111-b8da-002248097b5c',
     'ferreteria':'d2ce582d-bb90-f111-b8da-002248097b5c','sanbenito':'d4ce582d-bb90-f111-b8da-002248097b5c',
     'lago':'d6ce582d-bb90-f111-b8da-002248097b5c'}
RULE={'VR-03':'efb6a7cc-ac90-f111-b8da-002248097b5c','VR-10':'be06b8d2-ac90-f111-b8da-002248097b5c',
      'VR-17':'c206b8d2-ac90-f111-b8da-002248097b5c','VR-25':'d606b8d2-ac90-f111-b8da-002248097b5c',
      'VR-26':'e175b0d8-ac90-f111-b8da-002248097b5c','VR-23':'a576b0d8-ac90-f111-b8da-002248097b5c'}
def hace(h): return (datetime.datetime.utcnow()-datetime.timedelta(hours=h)).strftime('%Y-%m-%dT%H:%M:%SZ')

CASOS=[
 # --- Esperando a Banca: cliente real, remitente sin registrar ---
 dict(k='dm-001', de='j.morales@lacolonia.com.ni', asunto='Pre-registro depositos lunes', h=2,
      cli='colonia', cif='0012024002', clase=159460001, rev=159460003, val=159460002, resp=159460002,
      triage="VR-03 dio Fail, pero VR-27 confirma la plantilla oficial y VR-25 resolvio el CIF a un cliente real: se trata como cliente por registrar y pasa a revision interna; no se le escribe al remitente.",
      err="El correo j.morales@lacolonia.com.ni no esta registrado como remitente autorizado en CashHub.",
      fallos=[('VR-03','El correo j.morales@lacolonia.com.ni no esta registrado como remitente autorizado en CashHub.',None,None),
              ('VR-26','El remitente j.morales@lacolonia.com.ni no esta autorizado para declarar depositos del cliente indicado.',None,None)]),
 dict(k='dm-002', de='tesoreria.nueva@sanbenito.com.ni', asunto='Pre-registro', h=7,
      cli='sanbenito', cif='0012024004', clase=159460001, rev=159460003, val=159460002, resp=159460002,
      triage="VR-03 dio Fail, pero VR-27 confirma la plantilla oficial y VR-25 resolvio el CIF a un cliente real: se trata como cliente por registrar y pasa a revision interna; no se le escribe al remitente.",
      err="El correo tesoreria.nueva@sanbenito.com.ni no esta registrado como remitente autorizado en CashHub.",
      fallos=[('VR-03','El correo tesoreria.nueva@sanbenito.com.ni no esta registrado como remitente autorizado en CashHub.',None,None),
              ('VR-10','Fila 9 sin numero de bolsa.','Depositos','J9'),
              ('VR-26','El remitente no esta autorizado para declarar depositos del cliente indicado.',None,None)]),
 # --- Sospechosos: CIF que no existe en CashHub ---
 dict(k='dm-003', de='pagos@distribuidora-norte.net', asunto='Pre-registro urgente', h=5,
      cli=None, cif='0012024099', clase=159460002, rev=159460002, val=159460002, resp=159460002,
      triage="VR-03 dio Fail y VR-27 confirma la plantilla oficial, pero VR-25 dio Fail: el CIF no corresponde a ningun cliente. Se cuarentena y se alerta.",
      err="El CIF 0012024099 no corresponde a ningun cliente registrado en CashHub.",
      fallos=[('VR-03','El correo pagos@distribuidora-norte.net no esta registrado como remitente autorizado en CashHub.',None,None),
              ('VR-25','El CIF 0012024099 no corresponde a ningun cliente registrado en CashHub.','Depositos','J2')]),
 # --- Errores del motor ---
 dict(k='dm-004', de='contabilidad@ferreteriacentral.com.ni', asunto='Pre-registro miercoles', h=1,
      cli='ferreteria', cif='0012024003', clase=None, rev=159460004, val=159460000, resp=159460002,
      triage=None,
      err="Error de sistema del motor de validacion: el adjunto no se pudo abrir como paquete ZIP.",
      fallos=[]),
 dict(k='dm-005', de='deposito@transporteslago.com.ni', asunto='Pre-registro', h=3,
      cli='lago', cif='0012024005', clase=159460000, rev=159460004, val=159460000, resp=159460002,
      triage=None,
      err="VR-17 no pudo evaluarse: FormatException al convertir el valor de la celda Depositos!O6. El motor la aislo y siguio con el resto.",
      fallos=[]),
 # --- Sin responder: agotaron los 3 reintentos ---
 dict(k='dm-006', de='tesoreria@elporvenir.com.ni', asunto='Pre-registro jueves', h=26,
      cli='porvenir', cif='0012024001', clase=159460000, rev=159460005, val=159460002, resp=159460000, intentos=3,
      triage="VR-03 paso: la direccion esta registrada en CashHub, se puede responder.",
      err="Fila 7 sin numero de bolsa.\nLa cuenta 999999999 no pertenece al cliente.",
      resp_asunto="Pre-registro con observaciones",
      resp_html="<div style='font-family:Segoe UI,sans-serif'><h3 style='color:rgb(110,22,34)'>Encontramos 2 puntos por corregir</h3><p>Corrija lo siguiente en el archivo y vuelva a enviarlo.</p><table cellpadding='6' style='font-size:13px'><tr><td>Depositos!J7</td><td>La fila 7 no tiene numero de bolsa.</td></tr><tr><td>Depositos!K7</td><td>La cuenta 999999999 no pertenece a su empresa.</td></tr></table></div>",
      fallos=[('VR-10','Fila 7 sin numero de bolsa.','Depositos','J7'),
              ('VR-23','La cuenta 999999999 no pertenece al cliente.','Depositos','K7')]),
 dict(k='dm-007', de='admin@lacolonia.com.ni', asunto='Pre-registro sabado', h=31,
      cli='colonia', cif='0012024002', clase=159460000, rev=159460005, val=159460002, resp=159460000, intentos=3,
      triage="VR-03 paso: la direccion esta registrada en CashHub, se puede responder.",
      err="La celda O6 debe ser numerica: 'mil'.",
      resp_asunto="Pre-registro con observaciones",
      resp_html="<div style='font-family:Segoe UI,sans-serif'><h3 style='color:rgb(110,22,34)'>Encontramos 1 punto por corregir</h3><table cellpadding='6' style='font-size:13px'><tr><td>Depositos!O6</td><td>El monto debe ser un numero.</td></tr></table></div>",
      fallos=[('VR-17',"La celda O6 debe ser numerica: 'mil'.",'Depositos','O6')]),
]

creados=0
for c in CASOS:
    e={"sanic_receivedemailinternetmessageid":f"<{c['k']}@demo.cashhub.test>",
       "sanic_receivedemailoutlookuniqueid":f"demo-{c['k']}",
       "sanic_sourceemailaddress":c['de'], "sanic_subject":c['asunto'],
       "sanic_receptiondatetime":hace(c['h']),
       "sanic_validationstatus":c['val'], "sanic_emailreviewstatus":c['rev'],
       "sanic_emailreplystatus":c['resp'], "sanic_emailreviewmethod":159460001,
       "sanic_errortext":c['err'], "sanic_declaredcif":c['cif']}
    if c.get('triage'): e["sanic_triagenotes"]=c['triage']
    if c.get('clase') is not None: e["sanic_senderclassification"]=c['clase']
    if c.get('cli'): e["sanic_Client@odata.bind"]=f"/sanic_chtblclients({CLI[c['cli']]})"
    if c.get('intentos'): e["sanic_replyattempts"]=c['intentos']
    if c.get('resp_html'):
        e["sanic_replybodyhtml"]=c['resp_html']; e["sanic_replysubject"]=c['resp_asunto']
    r=req('POST','/sanic_chtblreceivedemailses',e)
    if 'error' in r: print("  ERROR",c['k'],r['error']['message'][:110]); continue
    eid=r['sanic_chtblreceivedemailsid']; creados+=1

    run=req('POST','/sanic_chtblvalidationrunses',{
      "sanic_name":f"Validacion de correo {eid}","sanic_sourceentity":"sanic_chtblreceivedemails",
      "sanic_sourcerecordid":eid,"sanic_sourcetype":159460000,
      "sanic_runstart":hace(c['h']),"sanic_runend":hace(c['h']),
      "sanic_runresult":159460001 if c['fallos'] else 159460000,
      "sanic_ReceivedEmail@odata.bind":f"/sanic_chtblreceivedemailses({eid})"})
    if 'error' in run: print("   run:",run['error']['message'][:90]); continue
    rid=run['sanic_chtblvalidationrunsid']
    fallidas={f[0] for f in c['fallos']}
    for k,det,hoja,celda in c['fallos']:
        req('POST','/sanic_chtblvalidationresultses',{
          "sanic_name":f"{k} FAIL","sanic_validationresult":159460001,"sanic_detail":det,
          "sanic_sheet":hoja,"sanic_cellref":celda,"sanic_durationms":3,"sanic_sourcetype":159460000,
          "sanic_ValidationRun@odata.bind":f"/sanic_chtblvalidationrunses({rid})",
          "sanic_ValidationRule@odata.bind":f"/sanic_validationruleses({RULE[k]})"})
    for k,gid in RULE.items():
        if k in fallidas: continue
        req('POST','/sanic_chtblvalidationresultses',{
          "sanic_name":f"{k} Pass","sanic_validationresult":159460000,"sanic_durationms":1,
          "sanic_sourcetype":159460000,
          "sanic_ValidationRun@odata.bind":f"/sanic_chtblvalidationrunses({rid})",
          "sanic_ValidationRule@odata.bind":f"/sanic_validationruleses({gid})"})
    print(f"  {c['k']}  {r['sanic_name']:<12} {c['de'][:34]:<36} {len(c['fallos'])} fallidas")
print(f"\n  creados: {creados}")
