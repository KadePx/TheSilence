"""Mock-up 3: commanding the battle from a holo table. The field is a hologram projected over a table in a dark
command room: isometric plane, cyan light, terrain as projection, our pixel ships standing on beams of light."""
import math, sys, random
from PIL import Image, ImageDraw, ImageFont, ImageEnhance, ImageFilter, ImageChops

P = 'C:/Users/Kade/TheSilence/'
D = sys.argv[1] + '/'
OUT = sys.argv[2]
U = float(sys.argv[3]) if len(sys.argv) > 3 else 34.0
random.seed(7)

W, H = 1920, 1080
CAMX, CAMY = 2.0, -0.5
CX, CY = 960, 404
TX0, TX1, TY0, TY1 = -18.0, 22.0, -13.0, 12.0     # the table's extent on the plane

HOLO = (90, 220, 255)        # the projector's light
HOLO_DIM = (40, 120, 170)
GREEN, RED, AMBER, ORANGE, IVORY, VIOLET = (120, 235, 170), (255, 96, 80), (255, 200, 110), (240, 160, 64), (235, 240, 245), (190, 150, 255)


def pt(x, y):
    dx, dy = x - CAMX, y - CAMY
    X, Y = (dx - dy) * 0.7071, (dx + dy) * 0.7071
    return (CX + X * U * 1.1, CY + Y * U * 0.55)


def font(size, name='Label'):
    return ImageFont.truetype(P + f'Assets/_Project/Fonts/Pixel/Silence{name}.ttf', size)


F12, F16B, H16 = font(12), font(16, 'Body'), font(16, 'Heading')


def layer():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))


def glow(l, radius=4, strength=1.6):
    """The layer plus a blurred, brightened copy of itself under it: light, not paint."""
    g = l.filter(ImageFilter.GaussianBlur(radius))
    a = g.getchannel('A').point(lambda v: min(255, int(v * strength)))
    g.putalpha(a)
    out = Image.alpha_composite(layer(), g)
    return Image.alpha_composite(out, l)


def add(base, l):
    """Additive blend for light."""
    rgb = ImageChops.add(base.convert('RGB'), Image.composite(l.convert('RGB'), Image.new('RGB', (W, H)), l.getchannel('A')))
    return rgb.convert('RGBA')


def dashed(d, pts, col, width=2, on=9, off=7):
    acc, draw = 0.0, True
    for a, b in zip(pts, pts[1:]):
        L = math.dist(a, b)
        t = 0.0
        while t < L:
            seg = min(L - t, (on if draw else off) - acc)
            if draw:
                d.line([(a[0] + (b[0] - a[0]) * t / L, a[1] + (b[1] - a[1]) * t / L), (a[0] + (b[0] - a[0]) * (t + seg) / L, a[1] + (b[1] - a[1]) * (t + seg) / L)], fill=col, width=width)
            t += seg
            acc += seg
            if acc >= (on if draw else off) - 1e-6:
                acc, draw = 0.0, not draw


def bezier(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]


def ring(d, x, y, r, col, width=1, n=48):
    d.line([pt(x + r * math.cos(2 * math.pi * i / n), y + r * math.sin(2 * math.pi * i / n)) for i in range(n + 1)], fill=col, width=width)


def label(d, xy, text, col, anchor='mm'):
    box = d.textbbox(xy, text, font=F12, anchor=anchor)
    d.rectangle((box[0] - 5, box[1] - 3, box[2] + 5, box[3] + 3), fill=(4, 18, 28, 170), outline=col + (150,))
    d.text(xy, text, font=F12, fill=col + (255,), anchor=anchor)


def load(path):
    im = Image.open(path).convert('RGBA')
    return im.crop(im.getbbox())


def hologram(im, tint=HOLO, alpha=0.7):
    """Terrain as a projection: one hue from its brightness, see-through, with scanlines."""
    g = im.convert('L')
    g = ImageEnhance.Contrast(g).enhance(1.3)
    out = Image.new('RGBA', im.size)
    px, gp, ap = out.load(), g.load(), im.getchannel('A').load()
    for y in range(im.height):
        line = 0.55 if y % 3 == 2 else 1.0
        for x in range(im.width):
            v = gp[x, y] / 255
            a = ap[x, y]
            if a == 0:
                continue
            px[x, y] = (int(tint[0] * (0.35 + v)), int(tint[1] * (0.35 + v)), int(tint[2] * (0.35 + v)), int(a * alpha * line))
    return out


# ---------------------------------------------------------------- the room
img = Image.new('RGBA', (W, H), (5, 8, 13, 255))
# the space beyond the bridge windows, very dim, so the room has depth
bg = Image.open(D + 'bg.png').convert('RGBA').resize((688 * 3, 384 * 3), Image.NEAREST).crop((60, 20, 60 + W, 20 + H))
img = Image.blend(img, bg, 0.18)
# light pooling around the table
pool = layer(); d = ImageDraw.Draw(pool)
corners = [pt(TX0, TY0), pt(TX1, TY0), pt(TX1, TY1), pt(TX0, TY1)]
d.polygon(corners, fill=HOLO + (40,))
img = add(img, pool.filter(ImageFilter.GaussianBlur(90)))

# ---------------------------------------------------------------- the table surface: lattice, rings, edge
l = layer(); d = ImageDraw.Draw(l)
d.polygon(corners, fill=(20, 80, 120, 22))
for g in range(int(TX0), int(TX1) + 1, 2):
    major = g % 6 == 0
    d.line([pt(g, TY0), pt(g, TY1)], fill=HOLO + ((95 if major else 28),), width=1)
for g in range(int(TY0), int(TY1) + 1, 2):
    major = g % 6 == 0
    d.line([pt(TX0, g), pt(TX1, g)], fill=HOLO + ((95 if major else 28),), width=1)
station = (3.6, -2.8, 3.2)
ox, oy = station[0], station[1]
for r in range(3, 30, 3):
    major = r % 9 == 0
    pts = []
    for i in range(241):
        x, y = ox + r * math.cos(2 * math.pi * i / 240), oy + r * math.sin(2 * math.pi * i / 240)
        pts.append((x, y))
    # only the parts of each ring that lie over the table
    run = []
    for (x, y) in pts:
        if TX0 <= x <= TX1 and TY0 <= y <= TY1:
            run.append(pt(x, y))
        elif run:
            if len(run) > 1:
                d.line(run, fill=HOLO + ((150 if major else 60),), width=1)
            run = []
    if len(run) > 1:
        d.line(run, fill=HOLO + ((150 if major else 60),), width=1)
for a in range(0, 360, 5):
    r0, r1 = 9, (9.35 if a % 15 else 9.8)
    x0, y0 = ox + r0 * math.cos(math.radians(a)), oy + r0 * math.sin(math.radians(a))
    if TX0 <= x0 <= TX1 and TY0 <= y0 <= TY1:
        d.line([pt(x0, y0), pt(ox + r1 * math.cos(math.radians(a)), oy + r1 * math.sin(math.radians(a)))], fill=HOLO + (170,), width=1)
for i in range(4):
    d.line([corners[i], corners[(i + 1) % 4]], fill=HOLO + (230,), width=2)
for (cx0, cy0) in corners:
    d.ellipse((cx0 - 4, cy0 - 2, cx0 + 4, cy0 + 2), fill=(220, 250, 255, 255))
# distance marks along the front edges
for g in range(int(TX0), int(TX1) + 1, 6):
    sx, sy = pt(g, TY1)
    d.text((sx - 6, sy + 10), str(g - int(TX0)), font=F12, fill=HOLO + (150,))
img = add(img, glow(l, 3, 1.4))

# light rising off the front edges of the table: the projection volume
curtain = layer(); d = ImageDraw.Draw(curtain)
for (a, b) in ((corners[3], corners[2]), (corners[2], corners[1])):
    n = int(math.dist(a, b) / 6)
    for i in range(n):
        x = a[0] + (b[0] - a[0]) * i / n
        y = a[1] + (b[1] - a[1]) * i / n
        h = 60 + 40 * math.sin(i * 0.7) ** 2
        for k in range(int(h)):
            d.point((x, y - k), fill=HOLO + (int(26 * (1 - k / h)),))
img = add(img, curtain.filter(ImageFilter.GaussianBlur(1)))

# ---------------------------------------------------------------- forces
concord = [
    ('lp', -6.0, 4.6, 'C', 'LONG PROMISE'),
    ('con_frigate', -10.0, 1.4, 'C', ''),
    ('con_frigate', -3.6, 8.8, 'C', ''),
    ('con_skiff', -0.4, 9.8, 'C', ''),
    ('con_skiff', -12.0, -3.0, 'C', ''),
    ('con_skiff', -14.4, 5.6, 'C', ''),
]
asc = [
    ('bs', 15.0, -6.6, 'A', 'NINTH SENTENCE'),
    ('asc_cruiser', 10.6, -10.4, 'A', ''),
    ('asc_cruiser', 18.4, 0.4, 'A', ''),
    ('asc_escort', 8.0, -3.6, 'A', ''),
    ('asc_escort', 11.4, 3.0, 'A', ''),
    ('asc_escort', 19.6, -10.2, 'A', ''),
    ('asc_escort', 5.4, -9.0, 'A', ''),
]
orders = [(concord[0], (-1.0, 1.8), (3.0, 3.2), 'FLANK  SPEED 5'), (concord[1], (-6.0, -2.6), (-2.6, -6.4), 'SCREEN  SPEED 4'),
          (concord[2], (1.6, 9.6), (6.8, 7.2), 'TORPEDO RUN  SPEED 6'), (concord[3], (5.4, 10.6), (10.0, 6.8), ''),
          (concord[4], (-9.0, -7.0), (-4.6, -9.6), ''), (concord[5], (-10.0, 6.6), (-6.8, 3.4), '')]
enemy = [(asc[0], (11.0, -2.6), (8.4, 0.2)), (asc[1], (7.0, -10.0), (3.6, -8.8)), (asc[3], (5.0, -2.0), (2.6, 0.2)),
         (asc[4], (8.2, 4.6), (5.6, 5.6)), (asc[2], (14.6, 2.6), (12.6, 3.6))]
NAMES = ['east', 'south_east', 'south', 'south_west', 'west', 'north_west', 'north', 'north_east']


def facing_of(v):
    return NAMES[int(round(math.degrees(math.atan2(v[1], v[0])) / 45.0)) % 8]


FACE = {}
for (s, ctrl, *_rest) in orders + enemy:
    a0, c0 = pt(s[1], s[2]), pt(*ctrl)
    FACE[(s[1], s[2])] = facing_of((c0[0] - a0[0], c0[1] - a0[1]))
for fleet, other in ((concord, asc), (asc, concord)):
    ps = [pt(x, y) for (_, x, y, _, _) in other]
    c = (sum(p[0] for p in ps) / len(ps), sum(p[1] for p in ps) / len(ps))
    for (_, x, y, _, _) in fleet:
        if (x, y) not in FACE:
            p = pt(x, y)
            FACE[(x, y)] = facing_of((c[0] - p[0], c[1] - p[1]))


def ship(name, x, y):
    f = FACE[(x, y)]
    return load(D + f'{name}/{f}.png') if name in ('lp', 'bs') else load(P + f'Assets/_Project/Art/Ships/IsoDemo/iso_{name}_{f}.png')


# orders: light on the table
l = layer(); d = ImageDraw.Draw(l)
for (s, ctrl, end, text) in orders:
    path = bezier(pt(s[1], s[2]), pt(*ctrl), pt(*end))
    dashed(d, path, GREEN + (230,), 2)
    ring(d, end[0], end[1], 1.1, GREEN + (170,))
    p, q = path[-2], path[-1]
    ang = math.atan2(q[1] - p[1], q[0] - p[0])
    for side in (-1, 1):
        d.line([q, (q[0] - 11 * math.cos(ang + side * 0.5), q[1] - 11 * math.sin(ang + side * 0.5))], fill=GREEN + (240,), width=2)
for (s, ctrl, end) in enemy:
    dashed(d, bezier(pt(s[1], s[2]), pt(*ctrl), pt(*end)), RED + (190,), 2, 6, 7)
    ring(d, end[0], end[1], 1.1, RED + (130,))
# the flagship's reach
ring(d, concord[0][1], concord[0][2], 6.0, AMBER + (120,))
img = add(img, glow(l, 3, 1.2))

# ---------------------------------------------------------------- terrain as projection, ships as models, back to front
things = []
st = hologram(load(D + 'station.png'), HOLO, 0.8)
things.append((station[1], 'station', st, station[0], station[1]))
for i, (x, y) in enumerate([(-15.0, -9.0), (-8.0, -11.0), (13.0, 8.0), (16.0, 10.0), (-16.0, 10.0), (4.0, 10.6), (20.0, 6.0)]):
    rock = hologram(load(P + f'Assets/_Project/Art/Ships/IsoDemo/iso_rock_{"a" if i % 2 else "b"}.png'), HOLO, 0.65)
    things.append((y, 'rock', rock, x, y))
for (name, x, y, side, _) in concord + asc:
    things.append((y, side, ship(name, x, y), x, y))
HOVER = 26
for _, kind, im, x, y in sorted(things, key=lambda t: t[3] + t[4]):
    sx, sy = pt(x, y)
    if kind in ('C', 'A'):
        col = GREEN if kind == 'C' else RED
        beam = layer(); bd = ImageDraw.Draw(beam)
        for k in range(HOVER):
            bd.line([(sx - 3 + k * 0.08, sy - k), (sx + 3 - k * 0.08, sy - k)], fill=HOLO + (int(70 * (1 - k / HOVER)),))
        bd.ellipse((sx - 12, sy - 5, sx + 12, sy + 5), outline=col + (255,), width=1)
        bd.ellipse((sx - 7, sy - 3, sx + 7, sy + 3), outline=col + (160,), width=1)
        img = add(img, glow(beam, 2, 1.2))
        # a faint rim of projector light around the model
        rim = Image.new('RGBA', im.size, HOLO + (0,))
        rim.putalpha(im.getchannel('A').filter(ImageFilter.MaxFilter(3)).point(lambda v: 60 if v else 0))
        pos = (int(sx - im.width / 2), int(sy - HOVER - im.height * 0.5))
        img.alpha_composite(rim, (pos[0], pos[1]))
        img.alpha_composite(im, pos)
    else:
        img = add(img, Image.new('RGBA', (W, H)))
        layer_ = layer()
        layer_.alpha_composite(im, (int(sx - im.width / 2), int(sy - im.height * 0.6)))
        img = add(img, glow(layer_, 3, 0.9))

# ---------------------------------------------------------------- fire, rendered as the hologram would show it
l = layer(); d = ImageDraw.Draw(l)
bs, tgt = asc[2], concord[3]
a = pt(bs[1] - 1.0, bs[2] + 0.4); a = (a[0], a[1] - HOVER)
b = pt(tgt[1], tgt[2]); b = (b[0], b[1] - HOVER)
d.line([a, b], fill=(140, 240, 255, 255), width=3)
d.line([a, b], fill=(255, 255, 255, 255), width=1)
for k in range(3):
    s0 = pt(concord[3][1] + 0.6, concord[3][2] - 0.3); s0 = (s0[0], s0[1] - HOVER)
    t0 = pt(asc[4][1], asc[4][2]); t0 = (t0[0], t0[1] - HOVER + 4 * k)
    path = bezier(s0, ((s0[0] + t0[0]) / 2 + 50 - 25 * k, (s0[1] + t0[1]) / 2 + 40), t0, 30)[:22 - 4 * k]
    for i, p in enumerate(path):
        if i % 2 == 0:
            d.ellipse((p[0] - 1, p[1] - 1, p[0] + 1, p[1] + 1), fill=AMBER + (60 + i * 8,))
img = add(img, glow(l, 4, 1.5))
ex = Image.open(P + 'Assets/_Project/Art/Effects/fx_explosion.png').convert('RGBA').crop((2 * 96, 96, 3 * 96, 2 * 96))
sx, sy = pt(asc[3][1], asc[3][2])
img.alpha_composite(ex, (int(sx - 48), int(sy - HOVER - 48)))

# ---------------------------------------------------------------- labels in the projection
l = layer(); d = ImageDraw.Draw(l)
for (s, ctrl, end, text) in orders:
    if text:
        mid = bezier(pt(s[1], s[2]), pt(*ctrl), pt(*end))[20]
        label(d, (mid[0], mid[1] - 14), text, GREEN)
sx, sy = pt(concord[0][1], concord[0][2]); label(d, (sx, sy - HOVER - 62), 'LONG PROMISE', IVORY)
sx, sy = pt(asc[0][1], asc[0][2]); label(d, (sx, sy - HOVER - 56), 'NINTH SENTENCE  LANCE CHARGING', RED)
sx, sy = pt(asc[2][1], asc[2][2]); label(d, (sx + 10, sy - HOVER - 44), 'VERDICT CRUISER  FIRING', RED)
sx, sy = pt(*station[:2]); label(d, (sx, sy - 118), 'DERELICT  BLOCKS FIRE', VIOLET)
sx, sy = pt(asc[3][1], asc[3][2]); label(d, (sx + 76, sy - HOVER + 24), 'BLADE DESTROYED', ORANGE)
label(d, (b[0] - 74, b[1] + 6), '-6 HULL', RED)
img.alpha_composite(l)

# ---------------------------------------------------------------- the projection's own texture: scanlines and a slow band
sl = layer(); d = ImageDraw.Draw(sl)
for y in range(52, 770, 3):
    d.line([(0, y), (W, y)], fill=(0, 0, 0, 38))
for y in range(300, 360):
    d.line([(0, y), (W, y)], fill=HOLO + (int(10 * math.sin((y - 300) / 60 * math.pi)),))
img.alpha_composite(sl)

# ---------------------------------------------------------------- the console: the game's own top bar and hand
shot = Image.open(P + 'ArtSource/Screenshots/m5_board_pass.png').convert('RGBA')
img.paste(shot.crop((0, 0, 1920, 52)), (0, 0))
img.paste(shot.crop((0, 770, 1920, 1080)), (0, 770))
d = ImageDraw.Draw(img)
d.rectangle((140, 4, 760, 48), fill=(26, 28, 34, 255))
d.text((160, 16), 'YOUR ORDERS   6 SHIPS   2 SQUADRONS', font=H16, fill=(201, 162, 74))
d.rectangle((720, 4, 1200, 48), fill=(26, 28, 34, 255))
d.text((960, 26), 'THE PALE ANCHORAGE  -  TACTICAL TABLE', font=H16, fill=(138, 144, 160), anchor='mm')
d.rectangle((400, 740, 1520, 766), fill=(8, 14, 20, 255), outline=(30, 60, 80, 255))
d.text((416, 745), 'Long Promise: drag the end of its path to give a new course. The whole fleet moves when you end the turn.', font=F16B, fill=(190, 230, 245))
img.convert('RGB').save(OUT)
print('saved', OUT)
