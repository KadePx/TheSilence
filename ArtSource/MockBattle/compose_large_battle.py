"""Mock-up 2: a large battle on a big field, seen at a low angle, with no visible grid.
Ships at 1x on a field several screens wide; long orders; a minimap shows the scale."""
import math, sys, random
from PIL import Image, ImageDraw, ImageFont, ImageEnhance, ImageFilter

P = 'C:/Users/Kade/TheSilence/'
D = sys.argv[1] + '/'          # this folder: bg.png, station.png, lp/<dir>.png, bs/<dir>.png
OUT = sys.argv[2]
FR = int(sys.argv[3]) if len(sys.argv) > 3 else 6
GRID = sys.argv[4] if len(sys.argv) > 4 else 'orbital'
random.seed(7)

W, H = 1920, 1080
TILT = 0.5
# Field coordinates are in "spans" on the floor plane; the camera looks at a window of it.
U = 46.0                       # pixels per span on screen (long axis)
CAMX, CAMY = 2.0, 1.0          # floor point at the centre of the view
CX, CY = 960, 420


def pt(x, y):
    return (CX + (x - CAMX) * U, CY + (y - CAMY) * U * TILT)


def font(size, name='Label'):
    return ImageFont.truetype(P + f'Assets/_Project/Fonts/Pixel/Silence{name}.ttf', size)


F12, F16B, H16 = font(12), font(16, 'Body'), font(16, 'Heading')
GREEN, RED, CYAN, AMBER, ORANGE, GREY, VIOLET, IVORY = (140, 196, 122), (217, 87, 63), (127, 220, 242), (255, 196, 106), (240, 160, 64), (138, 144, 160), (180, 140, 240), (235, 224, 198)

# ---- backdrop: the generated field, 3x, darkened a touch so ships carry the frame
bg = Image.open(D + 'bg.png').convert('RGBA')
bg = bg.resize((bg.width * 3, bg.height * 3), Image.NEAREST)
img = bg.crop((60, 20, 60 + W, 20 + H))
img = ImageEnhance.Brightness(img).enhance(0.82)


def layer():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))


def blend(l):
    global img
    img = Image.alpha_composite(img, l)


def dashed(d, pts, col, width=2, on=9, off=7):
    acc, draw = 0.0, True
    for a, b in zip(pts, pts[1:]):
        L = math.dist(a, b)
        t = 0.0
        while t < L:
            seg = min(L - t, (on if draw else off) - acc)
            if draw:
                p0 = (a[0] + (b[0] - a[0]) * t / L, a[1] + (b[1] - a[1]) * t / L)
                p1 = (a[0] + (b[0] - a[0]) * (t + seg) / L, a[1] + (b[1] - a[1]) * (t + seg) / L)
                d.line([p0, p1], fill=col, width=width)
            t += seg
            acc += seg
            if acc >= (on if draw else off) - 1e-6:
                acc, draw = 0.0, not draw


def bezier(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]


def ellipse(d, x, y, r, col, width=1, fill=None):
    cx, cy = pt(x, y)
    d.ellipse((cx - r * U, cy - r * U * TILT, cx + r * U, cy + r * U * TILT), outline=col, width=width, fill=fill)


def label(d, xy, text, col, f=F12, plate=True, anchor='mm'):
    box = d.textbbox(xy, text, font=f, anchor=anchor)
    if plate:
        d.rectangle((box[0] - 6, box[1] - 4, box[2] + 6, box[3] + 4), fill=(8, 10, 16, 220), outline=col + (200,))
    d.text(xy, text, font=f, fill=col, anchor=anchor)


def load(path):
    im = Image.open(path).convert('RGBA')
    return im.crop(im.getbbox())


def ship(name, facing):
    if name in ('lp', 'bs'):
        return load(D + f'{name}/{facing}.png')
    return load(P + f'Assets/_Project/Art/Ships/IsoDemo/iso_{name}_{facing}.png')


# ---- actors: (sprite set, facing, x, y, side, radius, label)
concord = [
    ('lp', 'north_east', -6.0, 4.6, 'C', 1.6, 'LONG PROMISE'),
    ('con_frigate', 'north_east', -9.2, 2.4, 'C', 1.2, ''),
    ('con_frigate', 'north_east', -4.2, 8.6, 'C', 1.2, ''),
    ('con_skiff', 'east', -1.0, 9.6, 'C', 0.8, ''),
    ('con_skiff', 'north_east', -10.8, -1.2, 'C', 0.8, ''),
    ('con_skiff', 'north_east', -12.6, 5.4, 'C', 0.8, ''),
]
asc = [
    ('bs', 'south_west', 13.6, -6.2, 'A', 1.7, 'NINTH SENTENCE'),
    ('asc_cruiser', 'south_west', 9.2, -9.4, 'A', 1.2, ''),
    ('asc_cruiser', 'west', 16.6, -0.8, 'A', 1.2, ''),
    ('asc_escort', 'south_west', 7.0, -3.6, 'A', 0.7, ''),
    ('asc_escort', 'south_west', 10.4, 1.4, 'A', 0.7, ''),
    ('asc_escort', 'west', 18.4, -9.6, 'A', 0.7, ''),
    ('asc_escort', 'south_west', 4.8, -8.8, 'A', 0.7, ''),
]
station = (3.6, -2.8, 3.2)   # x, y, radius of the landmark
# a few gameplay rocks along the belt the backdrop already shows (screen line y = 550 - 0.45 (x - 960))
def unproject(sx, sy):
    return ((sx - CX) / U + CAMX, (sy - CY) / (U * TILT) + CAMY)
belt = [unproject(sx, 550 - 0.45 * (sx - 960) + random.uniform(-18, 18)) for sx in (120, 260, 420, 1280, 1420, 1560, 1700)]

# ---- the floor: no grid, only a soft vignette of "depth" and the drift of dust
l = layer(); d = ImageDraw.Draw(l)
for i in range(240):
    x, y = random.uniform(-22, 26), random.uniform(-16, 16)
    sx, sy = pt(x, y)
    d.point((sx, sy), fill=(150, 170, 210, random.randint(20, 60)))
blend(l)

# ---- the orbital plane: a reference grid the battle sits on, centred on the derelict (or a plain lattice)
PLANE = (120, 170, 230)
l = layer(); d = ImageDraw.Draw(l)
if GRID == 'orbital':
    ox, oy = 3.6, -2.8
    for r in range(4, 61, 4):
        major = r % 12 == 0
        n = 180
        pts = [pt(ox + r * math.cos(2 * math.pi * i / n), oy + r * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
        d.line(pts, fill=PLANE + ((95 if major else 48),), width=1)
    for a in range(0, 360, 15):
        major = a % 45 == 0
        p0 = pt(ox + 4 * math.cos(math.radians(a)), oy + 4 * math.sin(math.radians(a)))
        p1 = pt(ox + 60 * math.cos(math.radians(a)), oy + 60 * math.sin(math.radians(a)))
        d.line([p0, p1], fill=PLANE + ((60 if major else 26),), width=1)
    for r in (12, 24):
        lx, ly = pt(ox + r * math.cos(math.radians(232)), oy + r * math.sin(math.radians(232)))
        d.text((lx, ly - 7), f'{r} SPANS', font=F12, fill=PLANE + (150,), anchor='mm')
    # bearing ticks around the innermost major ring
    for a in range(0, 360, 5):
        r0, r1 = 12, 12.4 if a % 15 else 12.9
        d.line([pt(ox + r0 * math.cos(math.radians(a)), oy + r0 * math.sin(math.radians(a))), pt(ox + r1 * math.cos(math.radians(a)), oy + r1 * math.sin(math.radians(a)))], fill=PLANE + (110,), width=1)
else:
    for gx in range(-30, 40, 3):
        d.line([pt(gx, -30), pt(gx, 30)], fill=PLANE + ((80 if gx % 12 == 0 else 34),), width=1)
    for gy in range(-30, 31, 3):
        d.line([pt(-30, gy), pt(40, gy)], fill=PLANE + ((80 if gy % 12 == 0 else 34),), width=1)
blend(l)

# ---- shadows under everything (sells the height of the ships above the floor)
l = layer(); d = ImageDraw.Draw(l)
for (_, _, x, y, _, r, _) in concord + asc:
    ellipse(d, x, y + 0.9, r * 0.9, None, fill=(0, 0, 0, 90))
blend(l.filter(ImageFilter.GaussianBlur(4)))

# ---- line of sight: the station's shadow, cast from the selected flagship
l = layer(); d = ImageDraw.Draw(l)
fx, fy = concord[0][2], concord[0][3]
ang = math.atan2(station[1] - fy, station[0] - fx)
dist = math.hypot(station[0] - fx, station[1] - fy)
half = math.asin(station[2] / dist)
poly = []
for i in range(21):
    a = ang - half + 2 * half * i / 20
    poly.append(pt(fx + dist * math.cos(a), fy + dist * math.sin(a)))
for i in range(21):
    a = ang + half - 2 * half * i / 20
    poly.append(pt(fx + 30 * math.cos(a), fy + 30 * math.sin(a)))
d.polygon(poly, fill=(0, 0, 8, 110))
blend(l.filter(ImageFilter.GaussianBlur(3)))

# ---- orders: long, sweeping, with a ghost at the end (movement covers real distance now)
l = layer(); d = ImageDraw.Draw(l)
orders = [
    (concord[0], (-1.0, 1.8), (3.4, 2.6), 'FLANK  SPEED 5'),
    (concord[1], (-6.0, -2.0), (-1.6, -5.4), 'SCREEN  SPEED 4'),
    (concord[2], (1.0, 9.0), (6.6, 6.2), 'TORPEDO RUN  SPEED 6'),
    (concord[3], (5.0, 10.0), (9.6, 5.8), ''),
    (concord[4], (-8.0, -5.0), (-3.4, -8.0), ''),
    (concord[5], (-9.0, 6.0), (-5.6, 3.6), ''),
]
for (s, ctrl, end, text) in orders:
    a, c, b = pt(s[2], s[3]), pt(*ctrl), pt(*end)
    path = bezier(a, c, b)
    dashed(d, path, GREEN + (210,), 2)
    ellipse(d, end[0], end[1], s[5], GREEN + (150,), 1)
    # arrowhead
    p, q = path[-2], path[-1]
    ang2 = math.atan2(q[1] - p[1], q[0] - p[0])
    for side in (-1, 1):
        d.line([q, (q[0] - 12 * math.cos(ang2 + side * 0.5), q[1] - 12 * math.sin(ang2 + side * 0.5))], fill=GREEN + (230,), width=2)
    if text:
        mid = path[len(path) // 2]
        label(d, (mid[0], mid[1] - 16), text, GREEN)
# the enemy's announced moves, in red and shorter
enemy_orders = [(asc[0], (10.0, -2.0), (7.6, 0.6)), (asc[1], (6.0, -9.0), (3.0, -7.6)), (asc[3], (4.0, -2.0), (2.0, 0.4)),
                (asc[4], (7.0, 3.6), (4.6, 4.6)), (asc[2], (13.0, 1.6), (11.4, 2.8))]
for (s, ctrl, end) in enemy_orders:
    path = bezier(pt(s[2], s[3]), pt(*ctrl), pt(*end))
    dashed(d, path, RED + (170,), 2, 6, 7)
    ellipse(d, end[0], end[1], s[5], RED + (110,), 1)
blend(l)

# ---- selection and range around the flagship: one soft ring, not a grid
l = layer(); d = ImageDraw.Draw(l)
for r, a in ((6.0, 70), (6.06, 40)):
    ellipse(d, fx, fy, r, AMBER + (a,), 1)
blend(l)

# ---- the field's objects and ships, back to front
things = []
st = load(D + 'station.png')
things.append((station[1], st, station[0], station[1], 0.62))
for i, (x, y) in enumerate(belt):
    rock = load(P + f'Assets/_Project/Art/Ships/IsoDemo/iso_rock_{"a" if i % 2 else "b"}.png')
    if i % 3 == 0:
        rock = rock.resize((rock.width // 2, rock.height // 2), Image.NEAREST)
    things.append((y, rock, x, y, 0.55))
for (name, facing, x, y, side, r, _) in concord + asc:
    things.append((y, ship(name, facing), x, y, 0.62))
ships_at = {(x, y): side for (_, _, x, y, side, _, _) in concord + asc}
HOVER = 22   # ships ride above the plane; a marker and a line tie each one to its point on it
for _, im, x, y, lift in sorted(things, key=lambda t: t[0]):
    sx, sy = pt(x, y)
    side = ships_at.get((x, y))
    if side:
        col = GREEN if side == 'C' else RED
        mk = ImageDraw.Draw(img)
        mk.ellipse((sx - 9, sy - 4, sx + 9, sy + 4), outline=col + (220,), width=1)
        mk.line([(sx, sy), (sx, sy - HOVER)], fill=col + (150,), width=1)
        img.alpha_composite(im, (int(sx - im.width / 2), int(sy - HOVER - im.height * 0.5)))
    else:
        img.alpha_composite(im, (int(sx - im.width / 2), int(sy - im.height * lift)))

# ---- engine wakes: short streaks behind moving ships
l = layer(); d = ImageDraw.Draw(l)
for (name, facing, x, y, side, r, _) in concord + asc:
    hx, hy = {'north_east': (0.7, -0.7), 'east': (1, 0), 'south_west': (-0.7, 0.7), 'west': (-1, 0)}[facing]
    tail = pt(x - hx * (r + 0.2), y - hy * (r + 0.2))
    for k in range(10):
        p0 = (tail[0] - hx * U * 0.25 * k, tail[1] - hy * U * TILT * 0.25 * k)
        col = (AMBER if side == 'C' else CYAN) + (max(0, 120 - k * 12),)
        d.point(p0, fill=col)
        d.point((p0[0] + 1, p0[1]), fill=col)
blend(l)

# ---- fire: a lance from the battleship, a torpedo salvo, a broadside, a kill
l = layer(); d = ImageDraw.Draw(l)
a, b = pt(asc[0][2] - 1.0, asc[0][3] + 0.4), pt(concord[2][2] + 0.3, concord[2][3] - 0.2)
d.line([a, b], fill=CYAN + (110,), width=7)
d.line([a, b], fill=CYAN + (255,), width=3)
d.line([a, b], fill=(255, 255, 255, 255), width=1)
for k in range(3):   # torpedoes from the skiff toward the cruiser
    s0 = pt(concord[3][2] + 0.6, concord[3][3] - 0.3)
    tgt = pt(asc[4][2], asc[4][3] + 0.2 * k)
    ctrl = ((s0[0] + tgt[0]) / 2 + 60 - 30 * k, (s0[1] + tgt[1]) / 2 + 40)
    path = bezier(s0, ctrl, tgt, 30)[:22 - 4 * k]
    for i, p in enumerate(path):
        if i % 2 == 0:
            d.ellipse((p[0] - 1, p[1] - 1, p[0] + 1, p[1] + 1), fill=AMBER + (60 + i * 8,))
    hp = path[-1]
    d.ellipse((hp[0] - 2, hp[1] - 2, hp[0] + 2, hp[1] + 2), fill=(255, 240, 200, 255))
for k in range(4):   # broadside from a cruiser
    s0 = pt(asc[1][2] - 0.6, asc[1][3] + 0.4 + 0.25 * k)
    t0 = pt(concord[4][2] + 0.4, concord[4][3] - 0.1 * k)
    m = ((s0[0] * 0.35 + t0[0] * 0.65), (s0[1] * 0.35 + t0[1] * 0.65))
    d.line([(m[0] + 6 * k, m[1]), (m[0] + 6 * k + 14, m[1] - 5)], fill=(255, 210, 140, 230), width=2)
blend(l)

# explosions: frames from the existing sheet
ex = Image.open(P + 'Assets/_Project/Art/Effects/fx_explosion.png').convert('RGBA')
frame = lambda i: ex.crop(((i % 4) * 96, (i // 4) * 96, (i % 4) * 96 + 96, (i // 4) * 96 + 96))
hit = Image.open(P + 'Assets/_Project/Art/Effects/fx_hit.png').convert('RGBA').crop((0, 64, 64, 128))
for (x, y, fr) in ((asc[3][2], asc[3][3], FR),):
    sx, sy = pt(x, y)
    img.alpha_composite(frame(fr), (int(sx - 48), int(sy - 60)))
sx, sy = b
img.alpha_composite(hit, (int(sx - 32), int(sy - 32)))

# ---- callouts: few, only where they matter
l = layer(); d = ImageDraw.Draw(l)
sx, sy = pt(fx, fy)
label(d, (sx - 10, sy - 66), 'LONG PROMISE', IVORY)
sx, sy = pt(asc[0][2], asc[0][3])
label(d, (sx, sy - 78), 'NINTH SENTENCE  LANCE CHARGED', RED)
sx, sy = pt(station[0], station[1])
label(d, (sx, sy - 110), 'DERELICT  BLOCKS FIRE', VIOLET)
sx, sy = pt(asc[3][2], asc[3][3])
label(d, (sx + 70, sy + 30), 'BLADE DESTROYED', ORANGE)
sx, sy = b
label(d, (sx + 10, sy + 34), '-6 HULL', RED)
blend(l)

# ---- vignette: the field runs on past the edges
vg = Image.new('L', (W, H), 0)
dv = ImageDraw.Draw(vg)
for i in range(60):
    dv.rectangle((i * 3, i * 2, W - i * 3, H - i * 2), outline=int(140 * (1 - i / 60)))
img = Image.composite(Image.new('RGBA', (W, H), (4, 5, 9, 255)), img, vg.filter(ImageFilter.GaussianBlur(20)))

# ---- HUD: the game's own top bar and hand, from the current battle screen
shot = Image.open(P + 'ArtSource/Screenshots/m5_board_pass.png').convert('RGBA')
img.paste(shot.crop((0, 0, 1920, 52)), (0, 0))
img.paste(shot.crop((0, 770, 1920, 1080)), (0, 770))
d = ImageDraw.Draw(img)
d.rectangle((140, 4, 760, 48), fill=(26, 28, 34, 255))
d.text((160, 16), 'YOUR ORDERS   6 SHIPS   2 SQUADRONS', font=H16, fill=(201, 162, 74))
d.rectangle((720, 4, 1200, 48), fill=(26, 28, 34, 255))
d.text((960, 26), 'THE PALE ANCHORAGE', font=H16, fill=GREY, anchor='mm')

# ---- minimap: the whole field, and the window the camera shows
mx, my, mw, mh = 1612, 64, 296, 178
d.rectangle((mx - 4, my - 4, mx + mw + 4, my + mh + 26), fill=(16, 17, 22, 240), outline=(110, 86, 34, 255), width=2)
d.text((mx + 6, my + 4), 'FIELD  60 x 36 SPANS', font=F12, fill=(110, 86, 34))
fw, fh = 60.0, 36.0
m = lambda x, y: (mx + (x + 24) / fw * mw, my + 14 + (y + 18) / fh * (mh - 14))
for (x, y) in belt:
    p = m(x, y); d.point(p, fill=GREY)
p = m(*station[:2]); d.ellipse((p[0] - 5, p[1] - 4, p[0] + 5, p[1] + 4), outline=VIOLET)
for (_, _, x, y, side, r, _) in concord + asc:
    p = m(x, y); d.rectangle((p[0] - 2, p[1] - 2, p[0] + 2, p[1] + 2), fill=GREEN if side == 'C' else RED)
for (x, y) in ((30, -12), (31, -10), (29, -9), (-18, 12), (-19, 14)):
    p = m(x - 2, y); d.rectangle((p[0] - 2, p[1] - 2, p[0] + 2, p[1] + 2), fill=RED if x > 0 else GREEN)
v0, v1 = m(CAMX - CX / U, CAMY - (CY - 52) / (U * TILT)), m(CAMX + (W - CX) / U, CAMY + (770 - CY) / (U * TILT))
d.rectangle((v0[0], max(my + 14, v0[1]), v1[0], min(my + mh, v1[1])), outline=(235, 224, 198), width=1)
d.text((mx + 6, my + mh + 10), 'ENEMY RESERVES ARRIVE TURN 4', font=F12, fill=RED)

# hint line
d.rectangle((400, 740, 1520, 766), fill=(12, 14, 20, 255), outline=(40, 42, 50, 255))
d.text((416, 745), 'Long Promise: drag the end of its path to give a new course. The whole fleet moves when you end the turn.', font=F16B, fill=(214, 218, 228))
img.convert('RGB').save(OUT)
print('saved', OUT)
