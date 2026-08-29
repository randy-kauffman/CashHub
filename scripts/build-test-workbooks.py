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
# CIF REAL del cliente sintetico en el ambiente: son 11 digitos, no 10. La bateria
# apuntaba a '0012024001' y ese cliente NO EXISTE, asi que los 40 casos de archivo
# morian en VR-25 sin llegar nunca al materializador.
CIF = '00120240014'
NIO_ACC, USD_ACC = 100200111, 100200112

# ---- Limites DUROS de las columnas de Dataverse (verificados contra el ambiente) ----
# No son opiniones: son el MaxLength/MinValue/MaxValue real de cada columna. Un archivo
# que pasa todas las reglas pero excede uno de estos NO se rechaza con un mensaje de
# negocio: revienta al persistir y el correo queda en Revision con error. Cada limite
# sin regla que lo cuide es un hueco, y esta bateria existe para exponerlos.
LIM_TULA = 50            # sanic_chtbldeclaredbags.sanic_bagnumber       String(50)
LIM_CHEQUE = 30          # sanic_chtbldeclaredchecks.sanic_checknumber   String(30)
LIM_CUENTA_MAX = 999999999   # sanic_chtbldeclareddeposits.sanic_accountnumber Integer 1..999999999
LIM_CUENTA_MIN = 1
LIM_MONTO = 100000000000     # sanic_totalnio/usd/eur                    Decimal 0..1e11, precision 2
LIM_CIF = 50             # sanic_chtblreceivedemails.sanic_declaredcif   String(50)

# Los identificadores de la matriz de remitentes viven en el fixture del ambiente
# (scripts/seed-sender-matrix.py). Cada uno resuelve a un cliente distinto montado en
# un estado distinto de recordstatus / vinculo.
MATRIZ = {
    'vigente':      '9010000001',   # cliente Activo, vinculo Activo
    'otro':         '9010000002',   # cliente Activo, SIN vinculo con el remitente
    'revocado':     '9010000003',   # cliente Activo, vinculo Inactivo
    'pendiente':    '9010000004',   # cliente Activo, vinculo PendingApproval
    'pdeact':       '9010000005',   # cliente Activo, vinculo PendingDeactivation
    'cli-inactivo': '9010000006',   # cliente Inactivo, vinculo Activo
    'cli-pending':  '9010000007',   # cliente PendingApproval, vinculo Activo
    'cli-pdeact':   '9010000008',   # cliente PendingDeactivation, vinculo Activo
    'taxid':        'J0310000000009',  # se resuelve por sanic_taxid, no por CIF
}
# El numero de cuenta es UNICO EN TODO EL BANCO (alternate key sanic_keynumerodecuenta),
# asi que cada cliente de la matriz tiene la suya. El orden es el de MATRIZ.
MATRIZ_ACC_BASE = 910000000

CASOS = [
    # 01 — camino feliz completo (efectivo + cheques, dos monedas, dos bolsas)
    # Los numeros de cheque son SOLO DIGITOS desde VR-29: 'CHQ-70001' ya no es valido.
    ('01-OK-completo', CIF,
     [dep(1, 'B-1001', USD_ACC, 'USD', efectivo=500, bac=300, ref='Venta semana 31'),
      dep(2, 'B-1001', NIO_ACC, 'NIO', efectivo=15000, ref='Caja chica'),
      dep(3, 'B-1002', USD_ACC, 'USD', efectivo=1200, ref='Deposito viernes')],
     [chk(1, '70001', 200), chk(1, '70002', 100)]),

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
     [chk(1, '700100', 200), chk(1, '700100', 100), chk(99, '700199', 50)]),

    # 11 — cheque con campos incompletos (VR-15; VR-29 queda Omitida por dependencia)
    ('11-ERR-cheque-incompleto', CIF,
     [dep(1, 'B-1201', USD_ACC, 'USD', bac=300, total=300)],
     [{'deposito': 1, 'cheque': '700200', 'tipo': '', 'moneda': '', 'monto': 300}]),

    # 12 — sin ningun deposito declarado (VR-09)
    ('12-ERR-sin-depositos', CIF, [], []),

    # 13 — multiples errores mezclados (prueba el agrupado del correo)
    ('13-ERR-multiple', CIF,
     [dep(1, '', 999999999, 'USD', efectivo=-100, total=-100),
      dep(2, 'B-1301', USD_ACC, 'EUR', efectivo=0, total=0),
      dep(3, 'B-1301', USD_ACC, 'USD', bac=900, total=900)],
     [chk(3, '700301', 100), chk(77, '700302', 50)]),

    # 14 — volumen: 25 depositos correctos (prueba rendimiento y resumen)
    ('14-OK-volumen', CIF,
     [dep(i, f'B-14{i:02d}', USD_ACC if i % 2 else NIO_ACC, 'USD' if i % 2 else 'NIO',
          efectivo=100 * i, ref=f'Lote {i}') for i in range(1, 26)], []),

    # 15 — desglose cruzado: declara 300 en la columna BAC pero el cheque es de otro
    # banco (VR-28). Estaba fuera del generador; se reincorpora.
    ('15-ERR-desglose-cheques', CIF,
     [dep(1, 'B-1501', USD_ACC, 'USD', bac=300, total=300)],
     [chk(1, '700400', 300, tipo='Otros Local')]),

    # =================================================================
    # VR-29 — el numero de cheque son SOLO DIGITOS
    # =================================================================

    # 16 — ceros a la izquierda: VALIDO y significativo (0001234 != 1234).
    # Es la razon por la que la columna es texto y no un entero.
    ('16-OK-cheque-ceros-izquierda', CIF,
     [dep(1, 'B-1601', USD_ACC, 'USD', bac=250, total=250)],
     [chk(1, '0001234', 150), chk(1, '0000001', 100)]),

    # 17 — espacios ALREDEDOR: valido. El motor valida el valor normalizado, el mismo
    # que persiste; un espacio de mas en Excel no puede rechazar una declaracion.
    ('17-OK-cheque-espacios-alrededor', CIF,
     [dep(1, 'B-1701', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '   700501   ', 100)]),

    # 18 — letras (VR-29)
    ('18-ERR-cheque-alfanumerico', CIF,
     [dep(1, 'B-1801', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, 'ABC123', 100)]),

    # 19 — guion: no existe en el campo serial del MICR (VR-29)
    ('19-ERR-cheque-con-guion', CIF,
     [dep(1, 'B-1901', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '123-456', 100)]),

    # 20 — decimal, negativo y notacion cientifica: los tres los dejaria pasar
    # TryDecimal (VR-17). VR-29 es mas estricto a proposito (VR-29 x3).
    ('20-ERR-cheque-no-entero', CIF,
     [dep(1, 'B-2001', USD_ACC, 'USD', bac=300, total=300)],
     [chk(1, '1234.56', 100), chk(1, '-500', 100), chk(1, '1e5', 100)]),

    # 21 — espacio INTERNO: distinto de los espacios alrededor (VR-29)
    ('21-ERR-cheque-espacio-interno', CIF,
     [dep(1, 'B-2101', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '12 34', 100)]),

    # 22 — digitos arabigo-indicos: char.IsDigit los aceptaria, el MICR no (VR-29)
    ('22-ERR-cheque-digitos-no-ascii', CIF,
     [dep(1, 'B-2201', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '١٢٣٤٥٦', 100)]),

    # =================================================================
    # LIMITES DUROS DE COLUMNA — el archivo PASA todas las reglas y muere al persistir.
    # Cada caso -LIM- que rompa es un hueco de validacion, no un archivo mal armado.
    # =================================================================

    # 23 — tula de 50 caracteres: cabe JUSTO. Debe procesarse sin drama.
    ('23-LIM-tula-50-exacto', CIF,
     [dep(1, 'T' + '0' * (LIM_TULA - 1), USD_ACC, 'USD', efectivo=100)], []),

    # 24 — tula de 51: NINGUNA regla mide el largo de la tula. Esperado hoy: pasa
    # validacion y revienta en el materializador (String(50)).
    ('24-LIM-tula-51-excede', CIF,
     [dep(1, 'T' + '0' * LIM_TULA, USD_ACC, 'USD', efectivo=100)], []),

    # 25 — cheque de 30 digitos: cabe JUSTO y VR-29 lo acepta.
    ('25-LIM-cheque-30-exacto', CIF,
     [dep(1, 'B-2501', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '1' * LIM_CHEQUE, 100)]),

    # 26 — cheque de 31 digitos: VR-29 pasa (son digitos) pero no cabe en String(30).
    # VR-29 valida forma, NO largo. Hueco.
    ('26-LIM-cheque-31-excede', CIF,
     [dep(1, 'B-2601', USD_ACC, 'USD', bac=100, total=100)],
     [chk(1, '1' * (LIM_CHEQUE + 1), 100)]),

    # 27 — cuenta de 10 digitos: excede Integer(999999999). Deuda de esquema ya
    # documentada; el materializador lanza un mensaje explicito.
    ('27-LIM-cuenta-10-digitos', CIF,
     [dep(1, 'B-2701', 1000000000, 'USD', efectivo=100)], []),

    # 28 — cuenta en cero: parsea como entero pero viola MinValue=1 de la columna.
    ('28-LIM-cuenta-cero', CIF,
     [dep(1, 'B-2801', 0, 'USD', efectivo=100)], []),

    # 29 — monto en el tope exacto de la columna (1e11).
    ('29-LIM-monto-tope-exacto', CIF,
     [dep(1, 'B-2901', USD_ACC, 'USD', efectivo=LIM_MONTO, total=LIM_MONTO)], []),

    # 30 — monto que excede el tope de la columna por 1.
    ('30-LIM-monto-excede', CIF,
     [dep(1, 'B-3001', USD_ACC, 'USD', efectivo=LIM_MONTO + 1, total=LIM_MONTO + 1)], []),

    # 31 — tres decimales contra una columna de precision 2: ¿redondea o falla?
    ('31-LIM-decimales-de-mas', CIF,
     [dep(1, 'B-3101', USD_ACC, 'USD', efectivo=100.005, total=100.005)], []),

    # 32 — SUMA por tula que excede el tope. Cada deposito pasa VR-20/VR-21 por
    # separado; nadie valida el total de la bolsa, que es lo que se persiste.
    ('32-LIM-suma-bolsa-excede', CIF,
     [dep(i, 'B-3201', USD_ACC, 'USD', efectivo=LIM_MONTO // 2, total=LIM_MONTO // 2)
      for i in range(1, 4)], []),

    # 33 — CIF mas largo que la columna que lo guarda (String(50)).
    ('33-LIM-cif-excede', '9' * (LIM_CIF + 10),
     [dep(1, 'B-3301', USD_ACC, 'USD', efectivo=100)], []),

    # =================================================================
    # VALORES RAROS — unicode, control, inyeccion, normalizacion
    # =================================================================

    # 34 — la MISMA tula escrita distinto: minuscula, mayuscula y con espacios.
    # Debe colapsar en UNA sola bolsa, guardada en MAYUSCULAS.
    ('34-OK-tula-case-y-espacios', CIF,
     [dep(1, 'tula-34', USD_ACC, 'USD', efectivo=100),
      dep(2, 'TULA-34', USD_ACC, 'USD', efectivo=200),
      dep(3, '  tula-34  ', USD_ACC, 'USD', efectivo=300)], []),

    # 35 — unicode: acentos, emoji y CJK en tula y referencia.
    ('35-RARO-unicode', CIF,
     [dep(1, 'BOLSA-ÑÁÉÍÓÚ-35', USD_ACC, 'USD', efectivo=100, ref='Depósito 🏦 東京 café'),
      dep(2, 'B-35-💰', USD_ACC, 'USD', efectivo=200, ref='Ñandú')], []),

    # 36 — caracteres de control e invisibles: zero-width, RTL, tab, salto de linea.
    # Buscamos que no rompan el XML ni el HTML de la respuesta.
    ('36-RARO-invisibles', CIF,
     [dep(1, 'B-36​01', USD_ACC, 'USD', efectivo=100, ref='ref\tcon\ttabs'),
      dep(2, 'B-36‮02', USD_ACC, 'USD', efectivo=200, ref='linea1\nlinea2')], []),

    # 37 — INYECCION. La respuesta al cliente se arma con plantillas {{token}} y con
    # HTML: si un valor declarado por el cliente se cuela sin escapar, se refleja en
    # el correo que le mandamos. Se prueban HTML, SQL, formula de Excel y —el que de
    # verdad importa— un token de la propia plantilla de respuesta.
    ('37-RARO-inyeccion', CIF,
     [dep(1, 'B-3701', USD_ACC, 'USD', efectivo=100, ref='<script>alert(1)</script>'),
      dep(2, 'B-3702', USD_ACC, 'USD', efectivo=200, ref="'; DROP TABLE clientes;--"),
      dep(3, 'B-3703', USD_ACC, 'USD', efectivo=300, ref='=1+1'),
      dep(4, 'B-3704', USD_ACC, 'USD', efectivo=400, ref='{{cliente}} {{gestion}}')], []),

    # 38 — campos con SOLO espacios: ¿se tratan como vacios (VR-10/VR-11) o pasan?
    ('38-RARO-solo-espacios', CIF,
     [dep(1, '   ', USD_ACC, 'USD', efectivo=100),
      dep(2, 'B-3802', '   ', 'USD', efectivo=200)], []),

    # 39 — referencia larguisima (1000 caracteres).
    ('39-RARO-referencia-larga', CIF,
     [dep(1, 'B-3901', USD_ACC, 'USD', efectivo=100, ref='X' * 1000)], []),

    # 40 — volumen al TOPE del rango de la plantilla: 200 depositos y 50 cheques.
    # Sirve para medir tiempo de ejecucion y el corte del resumen del correo.
    # Solo los primeros 50 depositos llevan cheque (el rango de la hoja Cheques es
    # 2..51); el resto va sin cheques o VR-12 los marca descuadrados.
    ('40-OK-volumen-tope', CIF,
     [dep(i, f'B-40{i % 5:02d}', USD_ACC, 'USD',
          efectivo=10, bac=10 if i <= 50 else 0, total=20 if i <= 50 else 10,
          ref=f'L{i}') for i in range(1, 201)],
     [chk(i, f'{800000 + i}', 10) for i in range(1, 51)]),

    # =================================================================
    # MATRIZ DE REMITENTES — el archivo es trivial y siempre valido a proposito.
    # Lo que se prueba es el VINCULO entre quien envia y el cliente del CIF, asi que
    # cada uno se manda desde la direccion que indica el plan de pruebas.
    # Requiere scripts/seed-sender-matrix.py corrido en el ambiente.
    # =================================================================
] + [
    (f'5{i}-MTX-{nombre}', ident,
     [dep(1, f'B-MTX-{i}', MATRIZ_ACC_BASE + i + 1, 'USD', efectivo=500,
          ref=f'Matriz {nombre}')], [])
    for i, (nombre, ident) in enumerate(MATRIZ.items())
] + [
    # 60 — CUENTA COMPARTIDA: el cliente 9010000009 declara la cuenta 910000001, que
    # esta autorizada TAMBIEN al cliente 9010000001. Con la key vieja sobre
    # (sanic_name) sola este escenario no se podia ni montar en la base.
    ('60-MTX-cuenta-compartida', '9010000009',
     [dep(1, 'B-MTX-60', MATRIZ_ACC_BASE + 1, 'USD', efectivo=750,
          ref='Cuenta recolectora compartida')], []),

    # 59 — CIF que no existe en ninguna parte: con plantilla oficial y remitente
    # desconocido tiene que caer en Sospechoso, no en "cliente por registrar".
    ('59-MTX-cif-inexistente', '9019999999',
     [dep(1, 'B-MTX-9', MATRIZ_ACC_BASE + 1, 'USD', efectivo=500, ref='Matriz sospechoso')], []),
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
