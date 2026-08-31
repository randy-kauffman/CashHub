#!/usr/bin/env bash
# =============================================================================
# Sube el assembly CashHub.Plugins recompilado a Dataverse.
#
# QUE HACE, EN ESTE ORDEN:
#   1. Borra los plugintype registrados que YA NO existen en el DLL, siempre que
#      no tengan steps. Si alguno tiene steps, ABORTA sin subir nada.
#   2. PATCH del content del pluginassembly con el DLL nuevo.
#   3. Verifica que los plugintype del ambiente coincidan con las clases del DLL.
#
# POR QUE ESE ORDEN: Dataverse valida el assembly contra los plugintype ya
# registrados. Si se sube un DLL al que le falta una clase que sigue registrada,
# la actualizacion puede fallar o dejar un plugintype apuntando a la nada. Se
# limpia primero y se sube despues.
#
# POR QUE ABORTA SI HAY STEPS: un plugintype con steps es codigo que Dataverse
# esta ejecutando hoy. Borrarlo sin desregistrar los steps rompe el pipeline en
# produccion. Eso no se automatiza: se revisa a mano.
#
# OJO — DRY-RUN POR DEFECTO.
#
# Requiere pp_secrets.env y el DLL compilado (dotnet build -c Release).
# Uso:  ./scripts/deploy-assembly.sh            # dry-run
#       ./scripts/deploy-assembly.sh --apply    # sube
# =============================================================================
set -euo pipefail

ASSEMBLY="${ASSEMBLY:-src/backend/CashHub.Plugins/bin/Release/net462/CashHub.Plugins.dll}"
[ -f "$ASSEMBLY" ] || { echo "Falta $ASSEMBLY — compilar primero (dotnet build -c Release)"; exit 1; }

source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

APPLY=0; [ "${1:-}" = "--apply" ] && APPLY=1
export TOKEN DATAVERSE_URL ASSEMBLY APPLY

python3 - <<'PYEOF'
import base64, json, os, re, subprocess, tempfile, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
TOKEN = os.environ['TOKEN']
APPLY = os.environ['APPLY'] == '1'
ASSEMBLY = os.environ['ASSEMBLY']


def traza(etapa, msg):
    print(f"[{etapa}] {msg}", flush=True)


def req(method, path, body=None):
    """OJO: el body va SIEMPRE por archivo (-d @archivo), nunca inline.

    El content del assembly en base64 pasa el medio megabyte y como argumento de
    linea de comandos revienta con 'Argument list too long' (Errno 7).
    """
    url = path if path.startswith('http') else f"{API}/{path}"
    cmd = ['curl', '-sS', '-X', method, url,
           '-H', f'Authorization: Bearer {TOKEN}',
           '-H', 'Accept: application/json',
           '-H', 'OData-MaxVersion: 4.0', '-H', 'OData-Version: 4.0',
           '-w', '\n%{http_code}']
    tmp = None
    if body is not None:
        tmp = tempfile.NamedTemporaryFile('w', suffix='.json', delete=False,
                                          encoding='utf-8')
        json.dump(body, tmp)
        tmp.close()
        cmd += ['-H', 'Content-Type: application/json', '-d', '@' + tmp.name]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True).stdout
    finally:
        if tmp:
            os.unlink(tmp.name)
    cuerpo, _, code = out.rpartition('\n')
    code = int(code.strip() or 0)
    if code >= 400:
        raise RuntimeError(f"{method} {path} -> HTTP {code}: {cuerpo[:400]}")
    return json.loads(cuerpo) if cuerpo.strip() else None


# --- clases IPlugin que trae el fuente -------------------------------------
# OJO: el directorio del proyecto es el prefijo ANTES de /bin/, sin dirname. Poner
# dirname sube un nivel de mas (a src/backend/), que no tiene .cs, y entonces la
# lista de clases sale VACIA y TODOS los plugintype del ambiente parecen huerfanos.
PROYECTO = ASSEMBLY.split('/bin/')[0]
clases = set()
for f in os.listdir(PROYECTO):
    if not f.endswith('.cs'):
        continue
    txt = open(os.path.join(PROYECTO, f), encoding='utf-8-sig').read()
    for m in re.finditer(r'class\s+(\w+)\s*:\s*IPlugin', txt):
        clases.add('CashHub.Plugins.' + m.group(1))
traza('FUENTE', f"{len(clases)} clases IPlugin en {PROYECTO}")
if not clases:
    raise SystemExit("ABORTADO: no se detecto NINGUNA clase IPlugin en el fuente. "
                     "Eso casi siempre es un error de ruta, no un proyecto vacio. "
                     "Sin esta guarda, todos los plugintype del ambiente se "
                     "interpretarian como huerfanos.")

# --- plugintype registrados hoy --------------------------------------------
q = urllib.parse.urlencode({
    '$select': 'plugintypeid,typename',
    '$filter': "startswith(typename,'CashHub.Plugins.')"})
registrados = {t['typename']: t['plugintypeid']
               for t in req('GET', f'plugintypes?{q}')['value']}
traza('AMBIENTE', f"{len(registrados)} plugintype registrados")

huerfanos = sorted(set(registrados) - clases)
faltantes = sorted(clases - set(registrados))
traza('DIFF', f"registrados y ya NO en el codigo: {len(huerfanos)}")
for t in huerfanos:
    traza('DIFF', f"    {t}")
traza('DIFF', f"en el codigo y aun sin registrar: {len(faltantes)} "
              f"(normal: se registran por su propio script)")

# --- abortar si algun huerfano tiene steps ---------------------------------
bloqueo = False
for t in huerfanos:
    qs = urllib.parse.urlencode({
        '$select': 'name', '$filter': f"_plugintypeid_value eq {registrados[t]}"})
    steps = req('GET', f'sdkmessageprocessingsteps?{qs}')['value']
    if steps:
        traza('BLOQUEO', f"  {t} tiene {len(steps)} step(s) registrados: "
                         f"{[s['name'] for s in steps]}")
        bloqueo = True
if bloqueo:
    raise SystemExit("ABORTADO: hay plugintype huerfanos CON steps. Desregistrar "
                     "los steps a mano antes de subir el assembly.")

if not APPLY:
    traza('FIN', f"Dry-run. Se borrarian {len(huerfanos)} plugintype y se subiria "
                 f"el assembly ({os.path.getsize(ASSEMBLY)} bytes). Nada modificado.")
    raise SystemExit(0)

# --- 1. borrar huerfanos ----------------------------------------------------
for t in huerfanos:
    req('DELETE', f"plugintypes({registrados[t]})")
    traza('LIMPIEZA', f"  plugintype eliminado: {t}")

# --- 2. subir el assembly ---------------------------------------------------
contenido = base64.b64encode(open(ASSEMBLY, 'rb').read()).decode()
q = urllib.parse.urlencode({'$select': 'pluginassemblyid',
                            '$filter': "name eq 'CashHub.Plugins'"})
asm = req('GET', f'pluginassemblies?{q}')['value']
if not asm:
    raise SystemExit("No existe el pluginassembly CashHub.Plugins en el ambiente.")
req('PATCH', f"pluginassemblies({asm[0]['pluginassemblyid']})", {'content': contenido})
traza('SUBIDA', f"  assembly actualizado ({os.path.getsize(ASSEMBLY)} bytes)")

# --- 3. verificar -----------------------------------------------------------
registrados2 = {t['typename'] for t in req('GET', f'plugintypes?{urllib.parse.urlencode({chr(36)+"select":"typename",chr(36)+"filter":"startswith(typename,\'CashHub.Plugins.\')"})}')['value']}
sobran = sorted(registrados2 - clases)
traza('VERIFICACION', f"plugintype tras la subida: {len(registrados2)}")
traza('VERIFICACION', f"huerfanos restantes: {len(sobran)} {sobran if sobran else ''}")
traza('FIN', 'Listo.')
PYEOF
