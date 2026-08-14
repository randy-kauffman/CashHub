"""Icono CH-App-DepositsTracker: fondo rojo BAC, ruta de tracking + lupa con check."""
from PIL import Image, ImageDraw
import math

S = 2048  # lienzo 4x, se reduce a 512 para antialiasing


def bezier(pts, n=400):
    out = []
    for i in range(n + 1):
        t = i / n
        p = list(pts)
        while len(p) > 1:
            p = [((1 - t) * a[0] + t * b[0], (1 - t) * a[1] + t * b[1])
                 for a, b in zip(p, p[1:])]
        out.append(p[0])
    return out


img = Image.new("RGB", (S, S), "#C8102E")
d = ImageDraw.Draw(img)

# banda diagonal más oscura, sutil, esquina inferior derecha
d.polygon([(S, S * 0.45), (S, S), (S * 0.45, S)], fill="#B00E28")

W = "#FFFFFF"
px = lambda fx, fy: (fx * S, fy * S)

# ---- ruta de tracking: curva S punteada con 3 nodos ----
curve = bezier([px(.14, .84), px(.52, .78), px(.10, .34), px(.52, .22)])
# longitud acumulada para dashes uniformes
seglen, total = [0.0], 0.0
for a, b in zip(curve, curve[1:]):
    total += math.dist(a, b)
    seglen.append(total)
dash, gap, wline = S * .050, S * .034, int(S * .026)
i = 0
while i < len(curve) - 1:
    start = seglen[i]
    j = i
    while j < len(curve) - 1 and seglen[j] - start < dash:
        j += 1
    d.line(curve[i:j + 1], fill=W, width=wline, joint="curve")
    k = j
    while k < len(curve) - 1 and seglen[k] - seglen[j] < gap:
        k += 1
    i = k if k > i else i + 1

# nodos (inicio y medio de la ruta)
for fx, fy in [(.14, .84), (.305, .555)]:
    x, y = px(fx, fy)
    r = S * .043
    d.ellipse([x - r, y - r, x + r, y + r], fill=W)
    r2 = S * .020
    d.ellipse([x - r2, y - r2, x + r2, y + r2], fill="#C8102E")

# ---- lupa ----
cx, cy, R = S * .615, S * .335, S * .195
lw = int(S * .052)
d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=W, width=lw)
# mango
ang = math.radians(45)
hx0, hy0 = cx + (R + lw * .28) * math.cos(ang), cy + (R + lw * .28) * math.sin(ang)
hx1, hy1 = cx + R * 2.05 * math.cos(ang), cy + R * 2.05 * math.sin(ang)
d.line([(hx0, hy0), (hx1, hy1)], fill=W, width=int(S * .075))
r = S * .075 / 2
d.ellipse([hx1 - r, hy1 - r, hx1 + r, hy1 + r], fill=W)

# check dentro de la lupa
chk = [px(.535, .335), px(.590, .400), px(.700, .265)]
d.line(chk, fill=W, width=int(S * .042), joint="curve")
for x, y in (chk[0], chk[-1]):
    r = S * .042 / 2
    d.ellipse([x - r, y - r, x + r, y + r], fill=W)

out = img.resize((512, 512), Image.LANCZOS)
out.save("cashhub-tracker-icon-512.png")
out.resize((245, 245), Image.LANCZOS).save("cashhub-tracker-icon-245.png")
print("ok")
