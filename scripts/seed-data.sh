#!/usr/bin/env bash
# =============================================================================
# Siembra de datos de configuración de CashHub (idempotente)
#
# Lee data/seed/*.json y hace upsert por clave natural contra el ambiente:
#   - fila inexistente  -> CREATE
#   - fila con cambios  -> UPDATE
#   - fila idéntica     -> SKIP
#   - valor "PENDIENTE*" -> se OMITE la fila con advertencia (config faltante)
#   - "@canvasapp:<nombre>" -> se resuelve a la URL real de la app en el ambiente
#
# Requiere pp_secrets.env en la raíz (DATAVERSE_URL, PP_TENANT_ID,
# PP_CLIENT_ID, PP_CLIENT_SECRET).
#
# Uso:  ./scripts/seed-data.sh [--dry-run] [directorio]
#       directorio default: data/seed (config). Ej: data/synthetic (datos de prueba).
# =============================================================================
set -euo pipefail
DRY_RUN=""
DATA_DIR="data/seed"
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN="--dry-run" ;;
    *) DATA_DIR="$arg" ;;
  esac
done
source "${ENV_FILE:-pp_secrets.env}"
DATAVERSE_URL="${DATAVERSE_URL%$'\r'}"; DATAVERSE_URL="${DATAVERSE_URL%/}"
PP_TENANT_ID="${PP_TENANT_ID%$'\r'}"; PP_CLIENT_ID="${PP_CLIENT_ID%$'\r'}"; PP_CLIENT_SECRET="${PP_CLIENT_SECRET%$'\r'}"

TOKEN=$(curl -sf -X POST "https://login.microsoftonline.com/${PP_TENANT_ID}/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=${PP_CLIENT_ID}&client_secret=${PP_CLIENT_SECRET}&scope=${DATAVERSE_URL}/.default" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
export TOKEN DATAVERSE_URL DRY_RUN DATA_DIR

python3 - <<'PYEOF'
import glob, json, os, subprocess, urllib.parse

API = os.environ['DATAVERSE_URL'] + '/api/data/v9.2'
H = ['-H', f"Authorization: Bearer {os.environ['TOKEN']}", '-H', 'Content-Type: application/json']
DRY = os.environ.get('DRY_RUN') == '--dry-run'

def req(method, path, body=None):
    cmd = ['curl', '-s', *H, '-X', method, API + path]
    if body is not None:
        cmd += ['-d', json.dumps(body)]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    d = json.loads(out) if out.strip() else {}
    if 'error' in d:
        raise RuntimeError(f"{method} {path}: {d['error'].get('message','')[:200]}")
    return d

def odata_literal(v):
    if isinstance(v, str):
        return "'" + v.replace("'", "''") + "'"
    if isinstance(v, bool):
        return 'true' if v else 'false'
    return str(v)

app_url_cache = {}
lookup_cache = {}
def resolve(value):
    if isinstance(value, str) and value.startswith('@canvasapp:'):
        name = value.split(':', 1)[1]
        if name not in app_url_cache:
            f = urllib.parse.quote(f"name eq '{name}'")
            rows = req('GET', f"/canvasapps?$select=appopenuri&$filter={f}").get('value', [])
            app_url_cache[name] = rows[0].get('appopenuri') if rows else None
        if not app_url_cache[name]:
            raise RuntimeError(f"canvas app '{name}' sin appopenuri en el ambiente")
        return app_url_cache[name]
    if isinstance(value, str) and value.startswith('@lookup:'):
        # @lookup:<entitySet>:<campo>:<valor> -> "/<entitySet>(<id>)"
        _, eset, field, val = value.split(':', 3)
        ck = (eset, field, val)
        if ck not in lookup_cache:
            f = urllib.parse.quote(f"{field} eq '{val}'")
            rows = req('GET', f"/{eset}?$filter={f}").get('value', [])
            if not rows:
                raise RuntimeError(f"@lookup sin resultado: {eset}.{field}='{val}' (¿orden de filas correcto?)")
            pk = next(k for k in rows[0] if k.endswith('id') and not k.startswith('_') and 'odata' not in k)
            lookup_cache[ck] = f"/{eset}({rows[0][pk]})"
        return lookup_cache[ck]
    return value

total = {'create': 0, 'update': 0, 'skip': 0, 'omit': 0}
for path in sorted(glob.glob(os.environ.get('DATA_DIR', 'data/seed') + '/*.json')):
    spec = json.load(open(path, encoding='utf-8'))
    es, nk = spec['entitySet'], spec['naturalKey']
    print(f"== {os.path.basename(path)} -> {es} ({len(spec['rows'])} filas)")
    for row in spec['rows']:
        if any(isinstance(v, str) and v.startswith('PENDIENTE') for v in row.values()):
            key = ' / '.join(str(row[k]) for k in nk)
            print(f"   OMITIDA (config PENDIENTE): {key}")
            total['omit'] += 1
            continue
        row = {k: resolve(v) for k, v in row.items()}
        def key_expr(k):
            if k.endswith('@odata.bind'):
                logical = k[:-len('@odata.bind')].lower()
                guid = row[k].rsplit('(', 1)[1].rstrip(')')
                return f"_{logical}_value eq {guid}"
            return f"{k} eq {odata_literal(row[k])}"
        flt = ' and '.join(key_expr(k) for k in nk)
        existing = req('GET', f"/{es}?$filter={urllib.parse.quote(flt)}").get('value', [])
        key = ' / '.join(str(row[k]) for k in nk)
        if not existing:
            if not DRY:
                req('POST', f"/{es}", row)
            print(f"   CREADA: {key}")
            total['create'] += 1
        else:
            cur = existing[0]
            # Dataverse guarda "" como null: normalizar para no generar updates eternos
            norm = lambda v: None if v == '' else v
            def changed(k, v):
                if k.endswith('@odata.bind'):
                    # comparar el GUID resuelto contra _<logical>_value del registro
                    logical = k[:-len('@odata.bind')].lower()
                    cur_id = cur.get(f'_{logical}_value')
                    new_id = v.rsplit('(', 1)[1].rstrip(')') if v else None
                    return (cur_id or '').lower() != (new_id or '').lower()
                return norm(cur.get(k)) != norm(v)
            diff = {k: v for k, v in row.items() if changed(k, v)}
            if diff:
                if not DRY:
                    pk = next(k for k in cur if k.endswith('id') and not k.startswith('_') and 'odata' not in k)
                    req('PATCH', f"/{es}({cur[pk]})", diff)
                print(f"   ACTUALIZADA: {key} ({', '.join(diff)})")
                total['update'] += 1
            else:
                total['skip'] += 1
print(f"\nResumen: {total['create']} creadas, {total['update']} actualizadas, {total['skip']} sin cambios, {total['omit']} omitidas por config pendiente{' [DRY-RUN]' if DRY else ''}")
PYEOF
