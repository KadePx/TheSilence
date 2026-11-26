"""Synthesize the alpha's placeholder sound effects as 16-bit mono WAV files.

    python Tools/make_sfx.py [out_dir]

Everything is generated from sine, square and noise sources with simple envelopes and a one-pole
low-pass filter, from a fixed seed, so re-running gives byte-identical files.
Default output: Assets/_Project/Audio/
"""
import os
import sys
import wave

import numpy as np

RATE = 22050
rng = np.random.default_rng(1701)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(n, attack=0.005, release=0.1, curve=2.0):
    """Linear attack, then a power-curve decay to zero over the rest."""
    e = np.ones(n)
    a = max(1, int(RATE * attack))
    e[:a] = np.linspace(0, 1, a)
    tail = n - a
    if tail > 0:
        e[a:] = np.linspace(1, 0, tail) ** curve
    r = min(n, max(1, int(RATE * release)))
    e[-r:] *= np.linspace(1, 0, r)
    return e


def sweep(f0, f1, seconds, shape="sine"):
    x = t(seconds)
    f = np.linspace(f0, f1, len(x))
    phase = 2 * np.pi * np.cumsum(f) / RATE
    s = np.sin(phase)
    if shape == "square":
        s = np.sign(s)
    elif shape == "saw":
        s = 2 * ((phase / (2 * np.pi)) % 1.0) - 1
    return s


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def lowpass(x, cutoff):
    """One-pole low-pass; cutoff may be a number or an array the length of x."""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1.0 - np.exp(-2 * np.pi * c / RATE)
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def pad(x, before=0.0):
    return np.concatenate([np.zeros(int(RATE * before)), x])


def lance():
    # Rising charge whine, then a bright falling beam over a low thump.
    charge = sweep(300, 1400, 0.12) * env(int(RATE * 0.12), 0.08, 0.02, 0.5) * 0.35
    beam = (sweep(1900, 520, 0.42, "saw") * 0.5 + sweep(950, 260, 0.42) * 0.5) * env(int(RATE * 0.42), 0.004, 0.2, 1.6)
    hiss = lowpass(noise(0.42), np.linspace(6000, 900, int(RATE * 0.42))) * env(int(RATE * 0.42), 0.002, 0.2, 2.5) * 0.5
    thump = sweep(120, 40, 0.3) * env(int(RATE * 0.3), 0.002, 0.1, 2.0) * 0.8
    return mix(charge, pad(mix(beam * 0.6, hiss, thump), 0.11))


def battery():
    # Four quick dull cannon reports.
    out = np.zeros(int(RATE * 0.62))
    for i in range(4):
        n = int(RATE * 0.16)
        shot = lowpass(noise(0.16), np.linspace(2600, 300, n)) * env(n, 0.001, 0.05, 3.0)
        shot += sweep(170, 60, 0.16) * env(n, 0.001, 0.05, 2.5) * 0.7
        start = int(RATE * (0.02 + i * 0.105 + rng.uniform(-0.008, 0.008)))
        out[start:start + n] += shot * (0.9 - i * 0.08)
    return out


def shot():
    n = int(RATE * 0.12)
    return (sweep(1200, 300, 0.12, "square") * 0.25 + lowpass(noise(0.12), 3000) * 0.5) * env(n, 0.001, 0.04, 2.5)


def torpedo():
    # Whoosh into a heavy impact.
    n = int(RATE * 0.3)
    whoosh = lowpass(noise(0.3), np.linspace(400, 3200, n)) * np.linspace(0, 1, n) ** 2 * 0.5
    return mix(whoosh, pad(explosion(0.55, 0.9), 0.28))


def explosion(seconds=1.1, gain=1.0):
    n = int(RATE * seconds)
    rumble = lowpass(noise(seconds), np.linspace(2400, 120, n)) * env(n, 0.003, 0.3, 1.8)
    boom = sweep(95, 28, seconds) * env(n, 0.002, 0.3, 1.5) * 0.9
    crack = lowpass(noise(0.08), 7000) * env(int(RATE * 0.08), 0.001, 0.03, 3.0) * 0.7
    return mix(rumble * 1.3, boom, crack) * gain


def pop():
    return explosion(0.28, 0.7)


def hit():
    n = int(RATE * 0.2)
    return (lowpass(noise(0.2), np.linspace(3500, 250, n)) + sweep(140, 50, 0.2) * 0.8) * env(n, 0.001, 0.06, 2.6)


def shield():
    # Glassy shimmer: detuned high sines with a quick flutter.
    n = int(RATE * 0.38)
    x = t(0.38)
    body = (np.sin(2 * np.pi * 1320 * x) + np.sin(2 * np.pi * 1987 * x) * 0.6 + np.sin(2 * np.pi * 2640 * x) * 0.3)
    flutter = 0.6 + 0.4 * np.sin(2 * np.pi * 38 * x)
    zap = lowpass(noise(0.05), 5000) * env(int(RATE * 0.05), 0.001, 0.02, 2.0) * 0.5
    return mix(body * flutter * env(n, 0.003, 0.2, 1.7) * 0.4, zap)


def click():
    n = int(RATE * 0.05)
    return (sweep(900, 500, 0.05, "square") * 0.3 + lowpass(noise(0.05), 4000) * 0.4) * env(n, 0.001, 0.02, 3.0)


def card():
    # A plate sliding onto a console: short filtered swish and a soft tick.
    n = int(RATE * 0.14)
    swish = lowpass(noise(0.14), np.linspace(800, 4200, n)) * env(n, 0.03, 0.05, 1.2) * 0.5
    tick = sweep(660, 440, 0.04) * env(int(RATE * 0.04), 0.001, 0.02, 2.0) * 0.5
    return mix(swish, pad(tick, 0.1))


def deny():
    n = int(RATE * 0.16)
    return sweep(180, 140, 0.16, "square") * env(n, 0.002, 0.05, 1.2) * 0.35


def turn():
    n = int(RATE * 0.22)
    return lowpass(noise(0.22), np.linspace(500, 1800, n)) * env(n, 0.05, 0.1, 1.0) * 0.5


def launch():
    n = int(RATE * 0.25)
    return (lowpass(noise(0.25), np.linspace(600, 3600, n)) * 0.5 + sweep(220, 660, 0.25) * 0.25) * env(n, 0.02, 0.1, 1.4)


def victory():
    # Cold rising fifths, like a choir pad cut short.
    out = np.zeros(int(RATE * 1.3))
    for i, f in enumerate((196.0, 293.66, 392.0)):
        n = int(RATE * 0.9)
        x = t(0.9)
        tone = (np.sin(2 * np.pi * f * x) + 0.4 * np.sin(2 * np.pi * f * 2 * x) + 0.2 * np.sin(2 * np.pi * f * 3.01 * x)) * env(n, 0.06, 0.4, 1.3)
        start = int(RATE * i * 0.16)
        out[start:start + n] += tone * 0.3
    return out


def defeat():
    n = int(RATE * 1.4)
    x = t(1.4)
    drone = (np.sin(2 * np.pi * 110 * x) + np.sin(2 * np.pi * 116.5 * x) * 0.8 + sweep(220, 98, 1.4) * 0.5) * env(n, 0.05, 0.6, 1.2)
    return mix(drone * 0.4, lowpass(noise(1.4), 300) * env(n, 0.1, 0.6, 1.0) * 0.6)


def jump():
    # Travelling between map nodes: a low swell that cuts off.
    n = int(RATE * 0.6)
    return (lowpass(noise(0.6), np.linspace(200, 2400, n)) * 0.5 + sweep(60, 240, 0.6) * 0.5) * np.linspace(0, 1, n) ** 1.5 * env(n, 0.01, 0.03, 0.3)


def coin():
    n = int(RATE * 0.2)
    x = t(0.2)
    return (np.sin(2 * np.pi * 1568 * x) + np.sin(2 * np.pi * 2349 * x) * 0.5) * env(n, 0.001, 0.1, 2.2) * 0.35


SOUNDS = {
    "sfx_lance": lance, "sfx_battery": battery, "sfx_shot": shot, "sfx_torpedo": torpedo,
    "sfx_explosion": explosion, "sfx_pop": pop, "sfx_hit": hit, "sfx_shield": shield,
    "sfx_click": click, "sfx_card": card, "sfx_deny": deny, "sfx_turn": turn, "sfx_launch": launch,
    "sfx_victory": victory, "sfx_defeat": defeat, "sfx_jump": jump, "sfx_coin": coin,
}


def write(path, samples):
    peak = np.max(np.abs(samples))
    if peak > 0:
        samples = samples / peak * 0.85
    data = (np.clip(samples, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "_Project", "Audio")
    os.makedirs(out, exist_ok=True)
    for name, make in SOUNDS.items():
        samples = make()
        write(os.path.join(out, name + ".wav"), samples)
        print(f"{name}.wav  {len(samples) / RATE:.2f}s")


if __name__ == "__main__":
    main()
