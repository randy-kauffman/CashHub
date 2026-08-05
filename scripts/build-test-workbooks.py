#!/usr/bin/env python3
"""
Genera la bateria de archivos de prueba a partir de la plantilla oficial.

Regla de oro aprendida a los golpes: NUNCA se borran celdas del XML (rompe la
hoja protegida). Para vaciar una celda se deja el elemento <c> con su estilo y
sin valor; para escribirla se reemplaza su contenido conservando el estilo.

Uso: python3 scripts/build-test-workbooks.py [directorio-salida]
"""
import copy
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

PLANTILLA = Path('docs/plantillas/CH-TPL-Depositos-v1.1.1.xlsx')
M = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
ET.register_namespace('', M)
NS = '{' + M + '}'

DEPOSITOS_SHEET = 'xl/worksheets/sheet1.xml'
CHEQUES_SHEET = 'xl/worksheets/sheet2.xml'
DEP_COLS = {'deposito': 'I', 'bolsa': 'J', 'cuenta': 'K', 'moneda': 'L',
            'bac': 'M', 'otros': 'N', 'efectivo': 'O', 'total': 'P', 'referencia': 'Q'}
CHK_COLS = {'deposito': 'A', 'cheque': 'B', 'tipo': 'C', 'moneda': 'D', 'monto': 'E'}
DEP_FIRST_ROW, DEP_LAST_ROW = 6, 206
CHK_FIRST_ROW, CHK_LAST_ROW = 2, 51


def _cell(sheet_root, ref):
    for c in sheet_root.iter(NS + 'c'):
        if c.get('r') == ref:
            return c
    return None


def _row(sheet_root, number):
    for r in sheet_root.iter(NS + 'row'):
        if r.get('r') == str(number):
            return r
    return None


def _clear(cell):
    """Vacia la celda conservando su estilo (jamas se elimina el elemento)."""
    for child in list(cell):
        cell.remove(child)
    cell.attrib.pop('t', None)


def _write(sheet_root, ref, value, row_hint=None):
    cell = _cell(sheet_root, ref)
    if cell is None:
        row = _row(sheet_root, row_hint or int(re.sub(r'\D', '', ref)))
        if row is None:
            return
        cell = ET.SubElement(row, NS + 'c')
        cell.set('r', ref)
    _clear(cell)
    if value is None or value == '':
        return
    if isinstance(value, (int, float)):
        v = ET.SubElement(cell, NS + 'v')
        v.text = str(value)
    else:
        cell.set('t', 'inlineStr')
        is_el = ET.SubElement(cell, NS + 'is')
        t = ET.SubElement(is_el, NS + 't')
        t.text = str(value)


def build(name, cif, deposits, checks, out_dir):
    """deposits/checks: listas de dicts con las claves de DEP_COLS/CHK_COLS."""
    zin = zipfile.ZipFile(PLANTILLA)
    dep_root = ET.fromstring(zin.read(DEPOSITOS_SHEET))
    chk_root = ET.fromstring(zin.read(CHEQUES_SHEET))

    # CIF del cliente
    _write(dep_root, 'J2', cif, row_hint=2)

    # Depositos: escribe los declarados y vacia el resto del rango
    for i, row_number in enumerate(range(DEP_FIRST_ROW, DEP_LAST_ROW + 1)):
        data = deposits[i] if i < len(deposits) else None
        for key, col in DEP_COLS.items():
            ref = f'{col}{row_number}'
            cell = _cell(dep_root, ref)
            if cell is None and data is None:
                continue
            _write(dep_root, ref, (data or {}).get(key), row_hint=row_number)

    # Cheques: idem
    for i, row_number in enumerate(range(CHK_FIRST_ROW, CHK_LAST_ROW + 1)):
        data = checks[i] if i < len(checks) else None
        for key, col in CHK_COLS.items():
            ref = f'{col}{row_number}'
            cell = _cell(chk_root, ref)
            if cell is None and data is None:
                continue
            _write(chk_root, ref, (data or {}).get(key), row_hint=row_number)

    out = Path(out_dir) / f'{name}.xlsx'
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as zout:
        for item in zin.namelist():
            if item == DEPOSITOS_SHEET:
                zout.writestr(item, ET.tostring(dep_root, encoding='unicode'))
            elif item == CHEQUES_SHEET:
                zout.writestr(item, ET.tostring(chk_root, encoding='unicode'))
            else:
                zout.writestr(item, zin.read(item))
    return out


def dep(no, bolsa, cuenta, moneda, efectivo=0, bac=0, otros=0, total=None, ref='Prueba'):
    return {'deposito': no, 'bolsa': bolsa, 'cuenta': cuenta, 'moneda': moneda,
            'bac': bac, 'otros': otros, 'efectivo': efectivo,
            'total': efectivo + bac + otros if total is None else total, 'referencia': ref}


def chk(no_dep, numero, monto, tipo='Cheque Local BAC', moneda='USD'):
    return {'deposito': no_dep, 'cheque': numero, 'tipo': tipo, 'moneda': moneda, 'monto': monto}


# ---- Cliente sintetico de referencia: Distribuidora El Porvenir ----
CIF = '0012024001'
NIO_ACC, USD_ACC = 100200111, 100200112

CASOS = [
    # 01 — camino feliz completo (efectivo + cheques, dos monedas, dos bolsas)
    ('01-OK-completo', CIF,
     [dep(1, 'B-1001', USD_ACC, 'USD', efectivo=500, bac=300, ref='Venta semana 31'),
      dep(2, 'B-1001', NIO_ACC, 'NIO', efectivo=15000, ref='Caja chica'),
      dep(3, 'B-1002', USD_ACC, 'USD', efectivo=1200, ref='Deposito viernes')],
     [chk(1, 'CHQ-70001', 200), chk(1, 'CHQ-70002', 100)]),

    # 02 — minimo aceptable: un solo deposito, solo efectivo
    ('02-OK-minimo', CIF, [dep(1, 'B-2001', NIO_ACC, 'NIO', efectivo=5000, ref='Unico')], []),

    # 03 — cuenta que no pertenece al cliente (VR-23)
    ('03-ERR-cuenta-ajena', CIF,
     [dep(1, 'B-3001', USD_ACC, 'USD', efectivo=100),
      dep(2, 'B-3001', 999999999, 'USD', efectivo=200)], []),

    # 04 — CIF vacio (VR-24)
    ('04-ERR-sin-cif', '', [dep(1, 'B-4001', NIO_ACC, 'NIO', efectivo=800)], []),

    # 05 — CIF inexistente (VR-25)
    ('05-ERR-cif-desconocido', '0099999999', [dep(1, 'B-5001', NIO_ACC, 'NIO', efectivo=800)], []),

    # 06 — deposito sin bolsa y sin cuenta (VR-10, VR-11)
    ('06-ERR-sin-bolsa-ni-cuenta', CIF,
     [dep(1, '', USD_ACC, 'USD', efectivo=100),
      dep(2, 'B-6001', '', 'USD', efectivo=100)], []),

    # 07 — monto negativo y total en cero (VR-20, VR-21)
    ('07-ERR-montos-invalidos', CIF,
     [dep(1, 'B-7001', USD_ACC, 'USD', efectivo=-500, total=-500),
      dep(2, 'B-7001', USD_ACC, 'USD', efectivo=0, total=0)], []),

    # 08 — texto en columna numerica y moneda no soportada (VR-17, VR-18)
    ('08-ERR-tipos-y-moneda', CIF,
     [dep(1, 'B-8001', USD_ACC, 'CAD', efectivo='doscientos', total=200)], []),

    # 09 — misma cuenta con dos monedas (VR-13)
    ('09-ERR-cuenta-dos-monedas', CIF,
     [dep(1, 'B-9001', USD_ACC, 'USD', efectivo=100),
      dep(2, 'B-9001', USD_ACC, 'NIO', efectivo=100)], []),

    # 10 — cheques: repetido, huerfano y descuadre (VR-14, VR-22, VR-12)
    ('10-ERR-cheques', CIF,
     [dep(1, 'B-1101', USD_ACC, 'USD', bac=500, total=500)],
     [chk(1, 'CHQ-REP', 200), chk(1, 'CHQ-REP', 100), chk(99, 'CHQ-HUERFANO', 50)]),

    # 11 — cheque con campos incompletos (VR-15)
    ('11-ERR-cheque-incompleto', CIF,
     [dep(1, 'B-1201', USD_ACC, 'USD', bac=300, total=300)],
     [{'deposito': 1, 'cheque': 'CHQ-INCOMPLETO', 'tipo': '', 'moneda': '', 'monto': 300}]),

    # 12 — sin ningun deposito declarado (VR-09)
    ('12-ERR-sin-depositos', CIF, [], []),

    # 13 — multiples errores mezclados (prueba el agrupado del correo)
    ('13-ERR-multiple', CIF,
     [dep(1, '', 999999999, 'USD', efectivo=-100, total=-100),
      dep(2, 'B-1301', USD_ACC, 'EUR', efectivo=0, total=0),
      dep(3, 'B-1301', USD_ACC, 'USD', bac=900, total=900)],
     [chk(3, 'CHQ-X', 100), chk(77, 'CHQ-Y', 50)]),

    # 14 — volumen: 25 depositos correctos (prueba rendimiento y resumen)
    ('14-OK-volumen', CIF,
     [dep(i, f'B-14{i:02d}', USD_ACC if i % 2 else NIO_ACC, 'USD' if i % 2 else 'NIO',
          efectivo=100 * i, ref=f'Lote {i}') for i in range(1, 26)], []),
]


def main():
    out_dir = Path(sys.argv[1] if len(sys.argv) > 1 else 'docs/plantillas/pruebas')
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, cif, deposits, checks in CASOS:
        path = build(name, cif, deposits, checks, out_dir)
        print(f'  {path.name:34} {len(deposits):>3} depositos, {len(checks)} cheques')
    print(f'\n{len(CASOS)} archivos en {out_dir}')


if __name__ == '__main__':
    main()
