#!/usr/bin/env python3
"""
Caza Reset() / SetFocus() / Select() apuntando a controles que NO existen, en las
fuentes .fx.yaml de una canvas app.

POR QUE EXISTE. `pac canvas pack` NO valida formulas: empaqueta e importa igual, y el
error aparece recien en Studio o en runtime, contra el usuario. Renombrar un control y
olvidar una referencia deja un Reset() colgado que pasa el empaquetado sin una sola
advertencia. Paso exactamente eso al renombrar inpCampoMontoMan -> inpCampoTotalMan.

SOLO mira el argumento de Reset/SetFocus/Select. Ahi un identificador es SIEMPRE un
control, sin ambiguedad. Se probo tambien validar los accesos `X.Propiedad`, pero un
dot-walk de lookup (Cajero.User), un alias de With (r.TotalNIO) y un enum
(SortOrder.Ascending) son indistinguibles de un control: el chequeo escupia decenas de
falsos positivos y un chequeo que grita en falso se ignora. Precision antes que
cobertura.

Uso: ./scripts/check-canvas-refs.py <carpeta de fuentes de la app>
"""
import re, sys, glob, os


def main(carpeta):
    src = os.path.join(carpeta, "Src")
    archivos = glob.glob(os.path.join(src, "**", "*.fx.yaml"), recursive=True)
    if not archivos:
        print(f"  no hay fuentes en {src}")
        return 1

    definidos = set()
    for f in archivos:
        definidos |= set(re.findall(r'^\s*"?([A-Za-z_]\w*) As ', open(f, encoding="utf-8").read(), re.M))

    problemas = 0
    for f in sorted(archivos):
        # Sin comentarios: un nombre viejo mencionado en una nota no es una referencia.
        s = "\n".join(re.sub(r"//.*$", "", l) for l in open(f, encoding="utf-8").read().split("\n"))
        colgadas = sorted({
            m for m in re.findall(r'\b(?:Reset|SetFocus|Select)\(\s*([A-Za-z_]\w*)\s*\)', s)
            if m not in definidos and m not in ("Parent", "Self", "ThisItem")
        })
        if colgadas:
            problemas += 1
            print(f"  {os.path.basename(f)}: {colgadas}")

    if problemas:
        print(f"\nFALLA: {problemas} archivo(s) con Reset/SetFocus/Select a controles inexistentes.")
        return 1
    print("  sin referencias colgadas")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
