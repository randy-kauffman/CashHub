#!/usr/bin/env python3
"""
Genera los PNG del icono de CH-App-Recounting desde icono-recounting.svg.

El SVG es la fuente de verdad. Se renderiza con cairosvg a 4x (2048) y se
reduce con LANCZOS a cada tamano de marca, para que el trazo quede nitido en
chico sin las puas que produce el joint="curve" de Pillow sobre beziers.

Uso (venv aparte):
    python3 -m venv /tmp/venv-icono
    /tmp/venv-icono/bin/pip install Pillow cairosvg
    /tmp/venv-icono/bin/python docs/marca/generar-icono-recounting.py
"""
import io
import os

import cairosvg
from PIL import Image

here = os.path.dirname(os.path.abspath(__file__))
svg = os.path.join(here, "icono-recounting.svg")

# Supersampling: render grande y bajo con LANCZOS
png4x = cairosvg.svg2png(url=svg, output_width=2048, output_height=2048)
master = Image.open(io.BytesIO(png4x)).convert("RGBA")

for size in [512, 192, 96, 48, 32]:
    out = master.resize((size, size), Image.LANCZOS)
    out.save(os.path.join(here, f"icono-recounting-{size}.png"))
    print(f"  icono-recounting-{size}.png")
print("Listo.")
