#!/usr/bin/env python3
"""
Prueba de AMBIENTE: ¿el plugin de validacion revienta el worker del sandbox con
adjuntos hostiles, o falla controlado?

Los tests de AdversarialInputTests corren en net8.0 con RAM de sobra y no prueban
el worker real (net462, memoria y 2 min acotados). Esto si: crea un correo de
prueba, le cuelga un adjunto, invoca la Custom API sanic_CHValidateReceivedEmail
—que ejecuta el plugin EN EL SANDBOX— y mira si la llamada vuelve con una falla de
NEGOCIO (controlada) o con un error de INFRAESTRUCTURA (timeout / out of memory).

Escenarios:
  bomba    ZIP de pocos KB que descomprime a ~1 GB. Con el guardia de tamaño
           descomprimido debe volver RAPIDO y controlado. Si el job muere, el
           guardia no sirve en net462.
  volumen  ~60 MB descomprimidos de celdas reales, bajo el tope de 100 MB: pasa el
           guardia y estresa de verdad la RAM del parser. Mide si aguanta volumen.

Crea y BORRA todo lo que toca. Uso: ./scripts/probe-parser-limits.py [bomba|volumen|ambos]
"""
import io
import json
import os
import subprocess
import sys
import time
import urllib.parse
import zipfile

QUE = sys.argv[1] if len(sys.argv) > 1 else 'ambos'
M = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
GMAIL = 'randyekg@gmail.com'


def env():
    e = {}
    for line in open('pp_secrets.env', encoding='utf-8'):
        line = line.strip()
        if line and not line.startswith('#') and '=' in line:
            k, v = line.split('=', 1)
            e[k.strip()] = v.strip().rstrip('\r')
    return e


E = env()
URL = E['DATAVERSE_URL'].rstrip('/')
TOK = json.loads(subprocess.run(['curl', '-sf', '-X', 'POST',
    f"https://login.microsoftonline.com/{E['PP_TENANT_ID']}/oauth2/v2.0/token",
    '-d', f"grant_type=client_credentials&client_id={E['PP_CLIENT_ID']}"
          f"&client_secret={E['PP_CLIENT_SECRET']}&scope={URL}/.default"],
    capture_output=True, text=True).stdout)['access_token']
API = URL + '/api/data/v9.2'
AUTH = ['-H', f'Authorization: Bearer {TOK}']


def req(method, path, body=None, extra=None, raw=None):
    cmd = ['curl', '-s', '-w', '\n%{http_code}', *AUTH, '-X', method,
           API + path if path.startswith('/') else path]
    if body is not None:
        cmd += ['-H', 'Content-Type: application/json',
                '-H', 'Prefer: return=representation', '--data-binary', json.dumps(body)]
    if raw is not None:
        cmd += ['-H', 'Content-Type: application/octet-stream', '--data-binary', '@' + raw]
    if extra:
        for h in extra:
            cmd += ['-H', h]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    payload, _, code = out.rpartition('\n')
    return int(code), (json.loads(payload) if payload.strip() else {})


def sheet(inner):
    return f'<worksheet xmlns="{M}"><sheetData>{inner}</sheetData></worksheet>'


def xlsx(sheet1_inner):
    """xlsx minimo con hojas Depositos/Cheques; sheet1 = lo que le metamos."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml',
                   '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
                   '<Default Extension="xml" ContentType="application/xml"/></Types>')
        z.writestr('xl/workbook.xml',
                   f'<workbook xmlns="{M}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
                   '<sheets><sheet name="Depositos" sheetId="1" r:id="rId1"/>'
                   '<sheet name="Cheques" sheetId="2" r:id="rId2"/></sheets></workbook>')
        z.writestr('xl/_rels/workbook.xml.rels',
                   '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                   '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
                   '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/></Relationships>')
        z.writestr('xl/worksheets/sheet1.xml', sheet1_inner)
        z.writestr('xl/worksheets/sheet2.xml', sheet(''))
    return buf.getvalue()


def build(kind):
    if kind == 'bomba':
        # ~1 GB de espacios en una celda: comprime a poquisimo. VR-06 (mide comprimido)
        # lo dejaria pasar; el guardia de tamaño descomprimido debe cortarlo.
        relleno = ' ' * (1024 * 1024 * 1024)
        return xlsx(sheet(f'<row r="1"><c r="A1" t="inlineStr"><is><t>{relleno}</t></is></c></row>'))
    # volumen: ~82 MB descomprimidos (BAJO el tope de 100 MB, para que el guardia NO lo
    # corte y el parser cargue el DOM entero). ~1,5M filas. Contenido corto y repetitivo
    # para que comprima chico y entre por la columna File (tope 32 MB). El parseo ocurre
    # en la etapa 3, ANTES de VR-06 (etapa 5), asi que aunque VR-06 despues lo rechace por
    # tamaño, el parser YA cargo el XDocument gigante: eso es justo lo que se quiere medir.
    filas = ''.join(f'<row r="{r}"><c r="A{r}" t="inlineStr"><is><t>x</t></is></c></row>'
                    for r in range(1, 1_200_001))
    return xlsx(sheet(filas))


def probar(kind):
    print(f'\n===== {kind.upper()} =====')
    data = build(kind)
    print(f'  archivo: {len(data)} bytes comprimidos', end='')
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        desc = sum(i.file_size for i in z.infolist())
    print(f' -> {desc // (1024*1024)} MB descomprimidos (ratio {desc/len(data):.0f}x)')

    # 1. correo de prueba
    code, mail = req('POST', '/sanic_chtblreceivedemailses', {
        'sanic_name': f'PROBE-{kind}',
        'sanic_sourceemailaddress': GMAIL,
        'sanic_subject': f'Pre-registro PROBE {kind}'})
    if code >= 300:
        print(f'  *** no se pudo crear el correo: {mail}'); return
    mail_id = mail['sanic_chtblreceivedemailsid']

    # 2. adjunto — sanic_emailattachmentid es required; la nav property es sanic_Email
    # (schema name, con mayuscula), no el logical name sanic_email.
    code, att = req('POST', '/sanic_chtblemailsattachmentses', {
        'sanic_filename': f'{kind}.xlsx', 'sanic_tamanoenbytes': len(data),
        'sanic_emailattachmentid': f'PROBE-{kind}',
        'sanic_Email@odata.bind': f'/sanic_chtblreceivedemailses({mail_id})'})
    att_id = att.get('sanic_chtblemailsattachmentsid')
    if not att_id:
        print(f'  *** no se pudo crear el adjunto: {att.get("error", att)}')
        req('DELETE', f'/sanic_chtblreceivedemailses({mail_id})')
        return

    # 3. subir bytes a la columna File
    tmp = f'/tmp/probe-{kind}.xlsx'
    open(tmp, 'wb').write(data)
    code, _ = req('PATCH', f'/sanic_chtblemailsattachmentses({att_id})/sanic_filecontent',
                  extra=[f'x-ms-file-name: {kind}.xlsx'], raw=tmp)
    os.unlink(tmp)
    print(f'  subida del adjunto: HTTP {code}')

    # 4. invocar la Custom API — corre el plugin EN EL SANDBOX
    print('  invocando sanic_CHValidateReceivedEmail...', flush=True)
    t0 = time.time()
    code, resp = req('POST', '/sanic_CHValidateReceivedEmail',
                     {'ReceivedEmailId': mail_id})
    dt = time.time() - t0

    print(f'  HTTP {code} en {dt:.1f}s')
    if code < 300:
        print(f'  CONTROLADO -> Outcome={resp.get("Result")} '
              f'Findings={resp.get("FailedFindings")}')
    else:
        msg = resp.get('error', {}).get('message', '')
        infra = any(w in msg.lower() for w in ('memory', 'timeout', 'time limit', 'sandbox', 'exceeded'))
        print(f'  {"*** INFRAESTRUCTURA (revento el worker)" if infra else "FALLA"}: {msg[:200]}')

    # 5. estado que quedo en el correo
    _, st = req('GET', f'/sanic_chtblreceivedemailses({mail_id})'
                       f'?$select=sanic_emailreviewstatus,sanic_errortext')
    print(f'  ReviewStatus={st.get("sanic_emailreviewstatus")} '
          f'error={(st.get("sanic_errortext") or "")[:400]}')

    # 6. limpieza
    req('DELETE', f'/sanic_chtblemailsattachmentses({att_id})')
    req('DELETE', f'/sanic_chtblreceivedemailses({mail_id})')
    print('  (limpieza: correo y adjunto borrados)')


print(f'Prueba de limites del parser contra {URL}')
for k in (['bomba', 'volumen'] if QUE == 'ambos' else [QUE]):
    probar(k)
