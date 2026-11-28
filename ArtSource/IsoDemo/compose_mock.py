"""Mock-up: the battle screen with an isometric, gridless board. Real HUD and backdrop, new 8-facing ships."""
import math, sys
from PIL import Image, ImageDraw, ImageFont, ImageEnhance

P = 'C:/Users/Kade/TheSilence/'
D = sys.argv[1] + '/'
shot = Image.open(P + 'ArtSource/Screenshots/m5_board_pass.png').convert('RGBA')
bg = Image.open(P + 'Assets/_Project/Art/Board/battle_bg_kessane.png').convert('RGBA')
bg = bg.resize((bg.width * 3, bg.height * 3), Image.NEAREST).crop((72, 36, 1992, 1116))
img = ImageEnhance.Brightness(bg).enhance(0.55)

TILT = 0.52
CX, CY = 960, 400
U = 76  # pixels per range unit on the plane's long axis


def pt(x, y):
    return (CX + x * U, CY + y * U * TILT)


def font(size, name='Label'):
    return ImageFont.truetype(P + f'Assets/_Project/Fonts/Pixel/Silence{name}.ttf', size)


F12, F24, H16 = font(12), font(24), font(16, 'Heading')


def layer():
    return Image.new('RGBA', img.size, (0, 0, 0, 0))


def blend(l):
    global img
    img = Image.alpha_composite(img, l)


def ring(d, x, y, r, col, width=2, dash=0):
    n = 96
    pts = [pt(x + r * math.cos(2 * math.pi * i / n), y + r * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
    for i in range(n):
        if dash and (i // dash) % 2: continue
        d.line([pts[i], pts[i + 1]], fill=col, width=width)


def wedge(x, y, r, a0, a1, r0=0.0):
    n = 28
    out = [pt(x + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), y + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    inn = [pt(x + r0 * math.cos(math.radians(a1 - (a1 - a0) * i / n)), y + r0 * math.sin(math.radians(a1 - (a1 - a0) * i / n))) for i in range(n + 1)]
    return out + inn


def dashed(d, a, b, col, width=2, on=10, off=8):
    L = math.hypot(b[0] - a[0], b[1] - a[1]); t = 0
    while t < L:
        u = min(L, t + on)
        d.line([(a[0] + (b[0] - a[0]) * t / L, a[1] + (b[1] - a[1]) * t / L), (a[0] + (b[0] - a[0]) * u / L, a[1] + (b[1] - a[1]) * u / L)], fill=col, width=width)
        t += on + off


DIRS = ['east', 'south-east', 'south', 'south-west', 'west', 'north-west', 'north', 'north-east']


def sprite(name, heading, scale=2):
    ang = math.degrees(math.atan2(math.sin(math.radians(heading)) * TILT, math.cos(math.radians(heading))))
    d = DIRS[int(round(ang / 45)) % 8]
    im = Image.open(D + f'dl/{name}/rotations/{d}.png').convert('RGBA')
    im = im.crop(im.getbbox())
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


def label(d, xy, text, col, f=F12, anchor='mm', plate=True):
    box = d.textbbox(xy, text, font=f, anchor=anchor)
    if plate:
        d.rectangle((box[0] - 6, box[1] - 4, box[2] + 6, box[3] + 4), fill=(10, 12, 18, 225), outline=col)
    d.text(xy, text, font=f, fill=col, anchor=anchor)


GREEN, RED, CYAN, ORANGE, GREY, VIOLET = (120, 220, 130), (240, 90, 80), (127, 220, 242), (255, 170, 70), (138, 144, 160), (190, 140, 230)

# --- the plane: a faint ruled floor, so depth reads without a grid to stand on ---
l = layer(); d = ImageDraw.Draw(l)
for r in (2, 4, 6, 8, 10):
    ring(d, 0, 0, r, (120, 150, 200, 26), 1)
for a in range(0, 360, 30):
    d.line([pt(2 * math.cos(math.radians(a)), 2 * math.sin(math.radians(a))), pt(10 * math.cos(math.radians(a)), 10 * math.sin(math.radians(a)))], fill=(120, 150, 200, 18), width=1)
blend(l)

# --- actors (plane coordinates, heading in degrees: 0 east, 90 toward the camera) ---
flag = dict(n='con_frigate', x=-4.2, y=0.2, h=-15, r=1.0, side=GREEN)
skiff = dict(n='con_skiff', x=-6.3, y=3.3, h=-40, r=0.55, side=GREEN)
cruiser = dict(n='asc_cruiser', x=0.3, y=3.5, h=200, r=1.1, side=RED)
escort1 = dict(n='asc_escort', x=4.6, y=-3.9, h=170, r=0.55, side=RED)
escort2 = dict(n='asc_escort', x=6.4, y=1.5, h=215, r=0.55, side=RED)
wreck = dict(x=1.0, y=-2.2, r=1.0)
rocks = [(3.6, 5.0, 'rock_0', 2), (7.6, -2.2, 'rock_3', 2), (-7.4, -3.2, 'rock_0', 1)]

# --- sight shadow behind the wreck, seen from the flagship ---
l = layer(); d = ImageDraw.Draw(l)
ang = math.degrees(math.atan2(wreck['y'] - flag['y'], wreck['x'] - flag['x']))
dist = math.hypot(wreck['x'] - flag['x'], wreck['y'] - flag['y'])
half = math.degrees(math.asin(wreck['r'] / dist))
d.polygon(wedge(flag['x'], flag['y'], 11.5, ang - half, ang + half, dist), fill=(4, 5, 10, 150))
blend(l)

# --- selected card: Deck Guns, starboard arc, range 5 ---
RANGE = 6.0
l = layer(); d = ImageDraw.Draw(l)
sa0, sa1 = flag['h'] + 30, flag['h'] + 150   # starboard: ship's right
d.polygon(wedge(flag['x'], flag['y'], RANGE, sa0, sa1, flag['r']), fill=(255, 170, 70, 46))
pa0, pa1 = flag['h'] - 150, flag['h'] - 30
d.polygon(wedge(flag['x'], flag['y'], RANGE, pa0, pa1, flag['r']), fill=(255, 170, 70, 22))
blend(l)
l = layer(); d = ImageDraw.Draw(l)
ring(d, flag['x'], flag['y'], RANGE, (255, 170, 70, 150), 2, dash=2)
for a in (sa0, sa1, pa0, pa1):
    d.line([pt(flag['x'] + flag['r'] * math.cos(math.radians(a)), flag['y'] + flag['r'] * math.sin(math.radians(a))),
            pt(flag['x'] + RANGE * math.cos(math.radians(a)), flag['y'] + RANGE * math.sin(math.radians(a)))], fill=(255, 170, 70, 120), width=1)

# --- bases: every ship has a radius, not a hex ---
for s in (flag, skiff, cruiser, escort1, escort2):
    ring(d, s['x'], s['y'], s['r'], s['side'] + (220,), 2)
    hx, hy = math.cos(math.radians(s['h'])), math.sin(math.radians(s['h']))
    d.line([pt(s['x'] + s['r'] * hx, s['y'] + s['r'] * hy), pt(s['x'] + (s['r'] + 0.35) * hx, s['y'] + (s['r'] + 0.35) * hy)], fill=s['side'] + (255,), width=3)
ring(d, wreck['x'], wreck['y'], wreck['r'], VIOLET + (150,), 1, dash=3)

# --- momentum: where each ship drifts to if nothing is played ---
def drift(s, speed):
    hx, hy = math.cos(math.radians(s['h'])), math.sin(math.radians(s['h']))
    a = pt(s['x'] + (s['r'] + 0.4) * hx, s['y'] + (s['r'] + 0.4) * hy)
    e = (s['x'] + speed * hx, s['y'] + speed * hy)
    dashed(d, a, pt(*e), s['side'] + (200,), 2)
    ring(d, e[0], e[1], s['r'], s['side'] + (110,), 1, dash=2)
    return e
fd = drift(flag, 2.9)
drift(cruiser, 2.4); drift(escort1, 2.2); drift(escort2, 2.0)
# the turn a Hard About would allow: a fan ahead of the flagship
for da in (-45, 45):
    a = flag['h'] + da
    dashed(d, pt(flag['x'] + 1.4 * math.cos(math.radians(a)), flag['y'] + 1.4 * math.sin(math.radians(a))),
           pt(flag['x'] + 2.6 * math.cos(math.radians(a)), flag['y'] + 2.6 * math.sin(math.radians(a))), (120, 220, 130, 90), 1, 5, 6)

# --- firing lines ---
def edge(a, b, back):
    L = math.hypot(b['x'] - a['x'], b['y'] - a['y'])
    return (b['x'] - (b['x'] - a['x']) * back / L, b['y'] - (b['y'] - a['y']) * back / L)
c_hit = edge(flag, cruiser, cruiser['r'])
d.line([pt(flag['x'], flag['y']), pt(*c_hit)], fill=ORANGE + (255,), width=3)
w_hit = edge(flag, wreck, wreck['r'])
d.line([pt(flag['x'], flag['y']), pt(*w_hit)], fill=GREY + (230,), width=2)
dashed(d, pt(*w_hit), pt(escort1['x'], escort1['y']), GREY + (130,), 1, 6, 8)
blend(l)

# --- sprites, back to front ---
things = []
for s, ghost in ((flag, fd),):
    g = sprite(s['n'], s['h']); g.putalpha(g.getchannel('A').point(lambda v: v * 60 // 255))
    things.append((ghost[1] - 0.01, g, ghost))
for s in (flag, skiff, cruiser, escort1, escort2):
    things.append((s['y'], sprite(s['n'], s['h']), (s['x'], s['y'])))
w = Image.open(D + 'dl/wreck_2.png').convert('RGBA'); w = w.crop(w.getbbox()); w = w.resize((w.width * 2, w.height * 2), Image.NEAREST)
things.append((wreck['y'], w, (wreck['x'], wreck['y'])))
for x, y, n, sc in rocks:
    r = Image.open(D + f'dl/{n}.png').convert('RGBA'); r = r.crop(r.getbbox()); r = r.resize((r.width * sc, r.height * sc), Image.NEAREST)
    things.append((y, r, (x, y)))
for _, im, (x, y) in sorted(things, key=lambda t: t[0]):
    sx, sy = pt(x, y)
    img.alpha_composite(im, (int(sx - im.width / 2), int(sy - im.height * 0.62)))

# --- callouts ---
l = layer(); d = ImageDraw.Draw(l)
sx, sy = pt(cruiser['x'], cruiser['y']); label(d, (sx, sy + 86), 'IN ARC   RANGE 5.6 OF 6   5 DMG TO PORT', ORANGE)
sx, sy = pt(escort1['x'], escort1['y']); label(d, (sx, sy - 62), 'NO LINE OF SIGHT', GREY)
sx, sy = pt(wreck['x'], wreck['y']); label(d, (sx, sy - 96), 'WRECK   BLOCKS FIRE', VIOLET)
sx, sy = pt(escort2['x'], escort2['y']); label(d, (sx, sy + 58), 'OUT OF RANGE', GREY)
sx, sy = pt(*fd); label(d, (sx + 10, sy - 78), 'DRIFT   SPEED 2', GREEN)
sx, sy = pt(flag['x'] + 3.4 * math.cos(math.radians(flag['h'] + 118)), flag['y'] + 3.4 * math.sin(math.radians(flag['h'] + 118))); label(d, (sx, sy), 'STARBOARD ARC', ORANGE, plate=False)
sx, sy = pt(flag['x'] + 3.6 * math.cos(math.radians(flag['h'] - 118)), flag['y'] + 3.6 * math.sin(math.radians(flag['h'] - 118))); label(d, (sx, sy), 'PORT ARC', (200, 140, 70), plate=False)
# hull bars under ships
for s, frac in ((flag, 0.7), (skiff, 1.0), (cruiser, 0.85), (escort1, 1.0), (escort2, 0.5)):
    sx, sy = pt(s['x'], s['y'] + s['r']); wd = int(s['r'] * 60)
    d.rectangle((sx - wd, sy + 8, sx + wd, sy + 13), fill=(10, 12, 18, 255))
    d.rectangle((sx - wd + 1, sy + 9, sx - wd + 1 + int((2 * wd - 2) * frac), sy + 12), fill=s['side'] + (255,))
blend(l)

# --- the real HUD, lifted from the current battle screen ---
for box in ((0, 0, 1920, 52), (10, 62, 314, 354), (1614, 64, 1910, 210), (398, 736, 1522, 770), (0, 770, 1920, 1080)):
    img.paste(shot.crop(box), box[:2])
d = ImageDraw.Draw(img)
d.rectangle((402, 740, 1518, 766), fill=(12, 14, 20, 255))
d.text((416, 745), 'Deck Guns: pick a target in the lit arc. Wrecks and rocks block fire.', font=font(16, 'Body'), fill=(220, 224, 232))
# mark the selected card
d.rectangle((852, 776, 1068, 1074), outline=ORANGE, width=3)
# the old enemy order rows named the hex board's ships: relabel
d.rectangle((1660, 106, 1880, 124), fill=(14, 16, 22)); d.text((1664, 109), 'VERDICT CRUISER  ATTACK', font=F12, fill=RED)
d.rectangle((1660, 138, 1880, 156), fill=(14, 16, 22)); d.text((1664, 141), 'BLADE ESCORT  FLANK', font=F12, fill=GREY)
d.rectangle((1660, 170, 1880, 188), fill=(14, 16, 22)); d.text((1664, 173), 'BLADE ESCORT  CLOSE IN', font=F12, fill=GREY)
img.convert('RGB').save(sys.argv[2])
print('saved', sys.argv[2])
