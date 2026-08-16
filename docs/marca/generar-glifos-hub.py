#!/usr/bin/env python3
"""
Genera los 7 glifos de tarjeta del Hub (columna de imagen sanic_icon de
CH-Tbl-AppCatalogs) segun el contrato de docs/hub-rebuild/01-pantalla-hub.md
SS 2.8.1: PNG 88x88, fondo transparente, motivo centrado de ~46x46, trazo
efectivo 4 (se muestra a 44px => 2px visuales, igual al mockup), color #C8102E,
extremos y uniones redondeados.

La fuente de verdad de los motivos es docs/hub-rebuild/mockup-hub.html (los
SVG inline de cada tarjeta, viewBox 24x24 stroke 2). Aca viajan calcados: si
cambia el mockup, cambia este script.

Salida: data/seed/assets/icons/<appkey>.png (los consume seed-data.sh via
"@file:assets/icons/<appkey>.png" en data/seed/30-appcatalogs.json).

Uso (venv aparte, mismo patron que generar-icono-*.py):
    python3 -m venv /tmp/venv-icono
    /tmp/venv-icono/bin/pip install Pillow cairosvg
    /tmp/venv-icono/bin/python docs/marca/generar-glifos-hub.py
"""
import io
import os

import cairosvg
from PIL import Image

# Motivos del mockup aprobado (viewBox 0 0 24 24, stroke 2) — verbatim.
MOTIVOS = {
    "reception": (
        '<path d="M22 12h-6l-2 3h-4l-2-3H2"/>'
        '<path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6'
        'l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/>'
    ),
    "supervisor": (
        '<path d="m16 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1z"/>'
        '<path d="m2 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1z"/>'
        '<path d="M7 21h10"/><path d="M12 3v18"/>'
        '<path d="M3 7h2c2 0 5-1 7-2 2 1 5 2 7 2h2"/>'
    ),
    "recounting": (
        '<circle cx="8" cy="8" r="6"/>'
        '<path d="M18.09 10.37A6 6 0 1 1 10.34 18"/>'
        '<path d="M7 6h1v4"/>'
        '<path d="m16.71 13.88.7.71-2.82 2.82"/>'
    ),
    "digitization": (
        '<rect x="2" y="4" width="20" height="16" rx="2"/>'
        '<path d="M6 8h.01M10 8h.01M14 8h.01M18 8h.01'
        'M6 12h.01M10 12h.01M14 12h.01M18 12h.01M7 16h10"/>'
    ),
    "emailval": (
        '<rect x="2" y="4" width="20" height="16" rx="2"/>'
        '<path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>'
    ),
    "tracker": (
        '<circle cx="6" cy="19" r="3"/>'
        '<path d="M9 19h8.5a3.5 3.5 0 0 0 0-7h-11a3.5 3.5 0 0 1 0-7H15"/>'
        '<circle cx="18" cy="5" r="3"/>'
    ),
    "clientadmin": (
        '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>'
        '<circle cx="9" cy="7" r="4"/>'
        '<path d="M19 8v6M22 11h-6"/>'
    ),
}

LIENZO = 88          # px del asset (2x del render de 44 en imgHubGlifo)
MOTIVO = 46          # px que ocupa el motivo centrado dentro del lienzo
TRAZO_EFECTIVO = 4   # sobre el lienzo de 88 (=> 2px visuales a 44)
COLOR = "#C8102E"
SS = 4               # supersampling: render 4x y bajar con LANCZOS

here = os.path.dirname(os.path.abspath(__file__))
out_dir = os.path.normpath(os.path.join(here, "..", "..", "data", "seed", "assets", "icons"))
os.makedirs(out_dir, exist_ok=True)

escala = MOTIVO / 24
margen = (LIENZO - MOTIVO) / 2
stroke = TRAZO_EFECTIVO / escala  # en unidades del viewBox de 24

for appkey, cuerpo in MOTIVOS.items():
    svg = (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{LIENZO}" height="{LIENZO}" '
        f'viewBox="0 0 {LIENZO} {LIENZO}">'
        f'<g transform="translate({margen} {margen}) scale({escala})" '
        f'fill="none" stroke="{COLOR}" stroke-width="{stroke:.4f}" '
        f'stroke-linecap="round" stroke-linejoin="round">{cuerpo}</g></svg>'
    )
    png_ss = cairosvg.svg2png(bytestring=svg.encode(), output_width=LIENZO * SS, output_height=LIENZO * SS)
    img = Image.open(io.BytesIO(png_ss)).convert("RGBA").resize((LIENZO, LIENZO), Image.LANCZOS)
    ruta = os.path.join(out_dir, f"{appkey}.png")
    img.save(ruta, optimize=True)
    kb = os.path.getsize(ruta) / 1024
    assert kb < 10, f"{appkey}.png pesa {kb:.1f}KB (>10KB, viola SS 2.8.1)"
    print(f"  {appkey}.png ({kb:.1f} KB)")
print("Listo.")
