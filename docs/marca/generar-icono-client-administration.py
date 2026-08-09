#!/usr/bin/env python3
"""
Genera los PNG del icono de CH-App-ClientAdministration desde primitivas Pillow.

Mismas coordenadas que icono-client-administration.svg (base 512): fuente y PNG
dicen lo mismo. Se dibuja a 4x y se reduce con LANCZOS para que quede nitido en
chico. No hay conversor de SVG en la maquina; por eso se redibuja con Pillow.

Uso (venv aparte con Pillow):
    python3 -m venv /tmp/venv-icono && /tmp/venv-icono/bin/pip install Pillow
    /tmp/venv-icono/bin/python docs/marca/generar-icono-client-administration.py
"""
from PIL import Image, ImageDraw

RED = (200, 16, 46, 255)      # #C8102E
WHITE = (255, 255, 255, 255)
BASE = 512
SCALE = 4
B = BASE * SCALE
STROKE = 30 * SCALE
CAP = STROKE // 2             # radio del punto para simular cap redondo


def s(v):
    return v * SCALE


def circle(cx, cy, r, width):
    d.ellipse([s(cx) - s(r), s(cy) - s(r), s(cx) + s(r), s(cy) + s(r)],
              outline=WHITE, width=width)


def fill_circle(cx, cy, r, color):
    d.ellipse([s(cx) - s(r), s(cy) - s(r), s(cx) + s(r), s(cy) + s(r)], fill=color)


def dot(cx, cy, r):
    d.ellipse([s(cx) - r, s(cy) - r, s(cx) + r, s(cy) + r], fill=WHITE)


img = Image.new("RGBA", (B, B), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Fondo rojo con esquinas redondeadas (los angulos quedan transparentes)
d.rounded_rectangle([0, 0, B - 1, B - 1], radius=s(112), fill=RED)

# Cabeza
circle(176, 176, 62, STROKE)

# Hombros: mitad superior de la elipse con bbox [58,262,294,450] (domo ancho y bajo)
d.arc([s(58), s(262), s(294), s(450)], start=180, end=360, fill=WHITE, width=STROKE)
dot(58, 356, CAP)    # caps redondos de ambos extremos del busto (coincide con el SVG)
dot(294, 356, CAP)

# Recorte rojo: un respiro separa la insignia del busto
fill_circle(392, 396, 82, RED)

# Insignia de verificacion
circle(392, 396, 70, STROKE)
d.line([(s(362), s(400)), (s(384), s(420)), (s(420), s(364))],
       fill=WHITE, width=STROKE, joint="curve")
dot(362, 400, CAP)   # caps redondos del check
dot(420, 364, CAP)

# Exportar a los tamanos de marca
import os
here = os.path.dirname(os.path.abspath(__file__))
for size in [512, 192, 96, 48, 32]:
    out = img.resize((size, size), Image.LANCZOS)
    out.save(os.path.join(here, f"icono-client-administration-{size}.png"))
    print(f"  icono-client-administration-{size}.png")
print("Listo.")
