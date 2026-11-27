"""Synthesize the alpha's placeholder music as seamless 16-bit mono WAV loops.

    python Tools/make_music.py [out_dir]

Four loops in D minor, built from sine partials, filtered noise and a circular reverb. Every note is placed
on a ring buffer (anything that runs past the end wraps to the start) and the reverb is a circular
convolution, so each file loops without a seam. Fixed seed: re-running gives byte-identical files.

    music_menu    48 s   a drone and a breath of noise: the broadcast, four seconds of nothing, over and over
    music_map     64 s   slow pads and a sparse bell figure for the sector map
    music_battle  40 s   a pulse and low stabs for ordinary fights
    music_hunt    32 s   the same pulse, faster and sourer, for a nemesis or the gate

Default output: Assets/_Project/Audio/
"""
import os
import sys
import wave

import numpy as np

RATE = 22050
rng = np.random.default_rng(4242)


def hz(semitones_from_d2):
    """Frequency of a note counted in semitones up from D2 (73.42 Hz)."""
    return 73.416 * 2 ** (semitones_from_d2 / 12)


# D natural minor, as semitone offsets from D
D, E, F, G, A, Bb, C = 0, 2, 3, 5, 7, 8, 10


class Ring:
    def __init__(self, seconds):
        self.n = int(RATE * seconds)
        self.buf = np.zeros(self.n)

    def add(self, start_s, sig, gain=1.0):
        sig = sig[: self.n]
        idx = (int(start_s * RATE) + np.arange(len(sig))) % self.n
        self.buf[idx] += sig * gain

    def reverb(self, decay_s=1.6, mix=0.3):
        m = min(self.n, int(RATE * decay_s * 4))
        ir = rng.standard_normal(m) * np.exp(-np.arange(m) / (RATE * decay_s))
        ir /= np.sqrt(np.sum(ir ** 2))
        pad = np.zeros(self.n)
        pad[:m] = ir
        wet = np.fft.irfft(np.fft.rfft(self.buf) * np.fft.rfft(pad), self.n)
        self.buf = self.buf * (1 - mix) + wet * mix

    def lowpass(self, cutoff_hz):
        spec = np.fft.rfft(self.buf)
        f = np.fft.rfftfreq(self.n, 1 / RATE)
        self.buf = np.fft.irfft(spec / (1 + (f / cutoff_hz) ** 4), self.n)

    def save(self, path, peak=0.7):
        x = self.buf - np.mean(self.buf)
        x = x / np.max(np.abs(x)) * peak
        with wave.open(path, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(RATE)
            w.writeframes((x * 32767).astype("<i2").tobytes())


def tt(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def swell(n, attack_s, release_s):
    e = np.ones(n)
    a = min(n // 2, int(RATE * attack_s))
    r = min(n // 2, int(RATE * release_s))
    e[:a] = np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    e[n - r:] = np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    return e


def pad(note, seconds, attack=2.0, release=2.5, bright=0.35):
    """A soft, slightly detuned tone: three voices of a few partials each."""
    x = tt(seconds)
    f = hz(note)
    out = np.zeros(len(x))
    for detune in (-0.004, 0.0, 0.005):
        for k, amp in ((1, 1.0), (2, bright), (3, bright * 0.5), (4, bright * 0.2)):
            out += amp * np.sin(2 * np.pi * f * (1 + detune) * k * x + rng.uniform(0, 6.28))
    return out * swell(len(x), attack, release) / 3


def bell(note, seconds=3.0):
    x = tt(seconds)
    f = hz(note)
    s = np.sin(2 * np.pi * f * x) + 0.4 * np.sin(2 * np.pi * f * 2.01 * x) * np.exp(-x * 3) + 0.2 * np.sin(2 * np.pi * f * 3.98 * x) * np.exp(-x * 6)
    return s * np.exp(-x * 1.6) * np.minimum(1, x * 200)


def pluck(note, seconds=0.28, bright=0.6):
    """A short low square-ish note for the pulse."""
    x = tt(seconds)
    f = hz(note)
    s = sum(np.sin(2 * np.pi * f * k * x) / k * (bright ** (k // 2)) for k in (1, 3, 5, 7))
    return s * np.exp(-x * 9) * np.minimum(1, x * 400)


def thump(seconds=0.35):
    x = tt(seconds)
    f = 110 * np.exp(-x * 18) + 42
    return np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-x * 9)


def tick(seconds=0.05, tone=0.3):
    n = int(RATE * seconds)
    s = rng.standard_normal(n)
    s = s - np.concatenate(([0], s[:-1])) * (1 - tone)  # crude high-pass
    return s * np.exp(-np.arange(n) / (RATE * 0.012))


def breath(seconds, centre_hz=600, width_hz=500):
    """Band-limited noise with a slow swell: air, or a voice with nothing in it."""
    n = int(RATE * seconds)
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / RATE)
    spec *= np.exp(-((f - centre_hz) / width_hz) ** 2)
    s = np.fft.irfft(spec, n)
    return s / np.max(np.abs(s)) * swell(n, seconds * 0.45, seconds * 0.45)


def menu():
    r = Ring(48)
    for start in (0, 24):
        r.add(start, pad(D - 12, 28, 8, 8, 0.2), 0.9)
        r.add(start + 2, pad(A - 12, 24, 9, 9, 0.15), 0.5)
    r.add(6, pad(F, 14, 5, 6, 0.2), 0.25)
    r.add(30, pad(E, 12, 5, 6, 0.2), 0.2)
    # Four seconds of shaped nothing, twice a loop.
    r.add(10, breath(4.0, 700, 420), 0.5)
    r.add(34, breath(4.0, 520, 380), 0.5)
    for start, note in ((14.5, D + 24), (20, A + 12), (38.5, F + 24), (43, D + 12)):
        r.add(start, bell(note, 4.0), 0.22)
    r.reverb(2.4, 0.45)
    r.lowpass(3200)
    return r


def sector_map():
    bar = 4.0
    r = Ring(16 * bar)
    chords = [(D, F, A), (Bb - 12, D, F), (G - 12, Bb - 12, D), (A - 12, C, E)]
    for i, chord in enumerate(chords):
        start = i * 4 * bar
        r.add(start, pad(chord[0] - 12, 4 * bar + 3, 3, 4, 0.2), 0.8)
        for note in chord:
            r.add(start + 0.5, pad(note, 4 * bar + 2, 3.5, 4, 0.3), 0.32)
    # A sparse bell figure: the same five notes, never quite in the same order.
    scale = [D + 12, F + 12, A + 12, C + 12, D + 24, E + 24]
    for b in range(16):
        for beat in (0, 2.5):
            if rng.random() < 0.62:
                r.add(b * bar + beat + rng.uniform(0, 0.08), bell(scale[rng.integers(len(scale))], 3.5), 0.2)
    r.add(30, breath(6, 900, 500), 0.12)
    r.reverb(2.0, 0.4)
    r.lowpass(4200)
    return r


def fight(bpm, bars, roots, sour=False):
    beat = 60 / bpm
    bar = beat * 4
    r = Ring(bars * bar)
    for b in range(bars):
        root = roots[b % len(roots)]
        start = b * bar
        for eighth in range(8):
            note = root - 12 if eighth % 4 != 3 else root - 12 + (1 if sour else 7)
            r.add(start + eighth * beat / 2, pluck(note, beat * 0.55), 0.55 if eighth % 2 == 0 else 0.35)
        for q in range(4):
            r.add(start + q * beat, thump(), 0.9 if q in (0, 2) else 0.45)
            r.add(start + q * beat + beat / 2, tick(), 0.14)
        if sour:
            r.add(start + beat, tick(0.12, 0.8), 0.22)
            r.add(start + 3 * beat, tick(0.12, 0.8), 0.22)
        if b % 2 == 0:
            r.add(start, pad(root, bar * 2 + 1, 0.6, 1.5, 0.5), 0.3)
            r.add(start, pad(root + (6 if sour else 7), bar * 2 + 1, 0.8, 1.5, 0.4), 0.2)
        if b % 4 == 3:
            r.add(start + 2 * beat, bell(root + 24 + (1 if sour else 3), 2.0), 0.16)
    r.reverb(0.9, 0.2)
    r.lowpass(5200)
    return r


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "_Project", "Audio")
    os.makedirs(out, exist_ok=True)
    tracks = {
        "music_menu": menu(),
        "music_map": sector_map(),
        "music_battle": fight(96, 16, [D, D, Bb - 12, C]),
        "music_hunt": fight(120, 16, [D, D, E - 1, D, Bb - 12, Bb - 12, A - 12, A - 12], sour=True),
    }
    for name, ring in tracks.items():
        path = os.path.join(out, name + ".wav")
        ring.save(path)
        print(f"{path}: {ring.n / RATE:.1f} s")


if __name__ == "__main__":
    main()
