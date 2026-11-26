"""RotSprite-style pixel-art rotation: Scale2x three times (8x), nearest-neighbour rotate, nearest downsample.
Keeps the palette exact (no new colours) and avoids the ragged look of plain nearest rotation.
Usage: python3 rotsprite.py in.png out.png angle_deg_ccw canvas_size"""
import sys
from PIL import Image
import numpy as np


def scale2x(a):
    h, w, _ = a.shape
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode='edge')
    B = p[0:h, 1:w + 1]; D = p[1:h + 1, 0:w]; E = p[1:h + 1, 1:w + 1]
    F = p[1:h + 1, 2:w + 2]; H = p[2:h + 2, 1:w + 1]
    eq = lambda x, y: np.all(x == y, axis=2)
    cond = (~eq(B, H)) & (~eq(D, F))
    e0 = np.where((cond & eq(D, B))[..., None], D, E)
    e1 = np.where((cond & eq(B, F))[..., None], F, E)
    e2 = np.where((cond & eq(D, H))[..., None], D, E)
    e3 = np.where((cond & eq(H, F))[..., None], F, E)
    out = np.zeros((h * 2, w * 2, a.shape[2]), dtype=a.dtype)
    out[0::2, 0::2] = e0; out[0::2, 1::2] = e1; out[1::2, 0::2] = e2; out[1::2, 1::2] = e3
    return out


def rotsprite(img, angle, size):
    img = img.convert('RGBA')
    a = np.array(img)
    a[a[..., 3] < 128] = 0  # clean transparency so edges compare equal
    # pad to the target canvas first so nothing is cut off
    W, H = img.size
    canvas = np.zeros((size, size, 4), dtype=a.dtype)
    ox, oy = (size - W) // 2, (size - H) // 2
    canvas[oy:oy + H, ox:ox + W] = a
    big = canvas
    for _ in range(3):
        big = scale2x(big)
    rot = Image.fromarray(big).rotate(angle, resample=Image.NEAREST, expand=False)
    small = rot.resize((size, size), Image.NEAREST)
    return small


if __name__ == '__main__':
    src, dst, ang, size = sys.argv[1], sys.argv[2], float(sys.argv[3]), int(sys.argv[4])
    rotsprite(Image.open(src), ang, size).save(dst)
