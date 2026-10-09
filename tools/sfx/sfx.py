#!/usr/bin/env python3
"""Generates the game's NES-style 8-bit sound effects into art/audio/.

The sounds are built the way an NES game builds them: two pulse voices, a triangle and a noise
voice, each changed once per 60 Hz frame (pitch, volume 0-15, duty or noise mode), mixed with the
NES's own non-linear mixer and written as 8-bit mono WAV files. Pitches snap to the NES timer
steps, so notes carry the same slight detune the console has.

Run from anywhere:  python3 tools/sfx/sfx.py            (writes every sound)
                    python3 tools/sfx/sfx.py goal post  (writes only those)
Nothing is random at run time: the same script always writes the same files.
"""

import math
import pathlib
import random
import struct
import sys

RATE = 22050                 # output sample rate
FRAME = 60                   # parameter updates per second, like an NES sound engine
CPU = 1789773                # NTSC CPU clock; pulse, triangle and noise timers count from it
NOISE_PERIODS = [4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068]
TRIANGLE_STEPS = list(range(15, -1, -1)) + list(range(16))
OUT = pathlib.Path(__file__).resolve().parents[2] / "art" / "audio"


# ---- notes ---------------------------------------------------------------------------------

NAMES = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}


def note(name):
    """Frequency of a note such as 'A4' or 'C#5'."""
    letter, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((NAMES[letter] + 12 * (octave - 4)) / 12)


def pulse_hz(hz):
    """Snaps a pulse frequency to the nearest NES 11-bit timer value."""
    t = max(8, min(2047, round(CPU / (16 * hz) - 1)))
    return CPU / (16 * (t + 1))


def triangle_hz(hz):
    """Snaps a triangle frequency to the nearest NES timer value (it runs an octave lower)."""
    t = max(2, min(2047, round(CPU / (32 * hz) - 1)))
    return CPU / (32 * (t + 1))


# ---- voices: one entry per frame -----------------------------------------------------------

class Voice:
    """One channel's frames: per frame a pitch, a volume 0-15 and a duty (pulse) or mode (noise)."""

    def __init__(self, kind):
        self.kind = kind     # "pulse", "triangle" or "noise"
        self.frames = []     # (pitch or noise period index, volume, duty or short-mode flag)

    def add(self, pitch, volumes, extra=None):
        """Holds one pitch over a list of per-frame volumes. Pitch may be a function of the frame index."""
        for i, v in enumerate(volumes):
            p = pitch(i) if callable(pitch) else pitch
            self.frames.append((p, int(max(0, min(15, round(v)))), extra))
        return self

    def rest(self, frames):
        return self.add(0, [0] * frames)


def decay(start, frames, end=0):
    """Volumes falling in a straight line over a number of frames."""
    return [start + (end - start) * i / max(1, frames - 1) for i in range(frames)]


def hold(volume, frames, release=0):
    """A held volume with an optional linear release."""
    return [volume] * frames + decay(volume, release)[1:] if release else [volume] * frames


def slide(a, b, frames):
    """A pitch function sliding from a to b over the given number of frames."""
    return lambda i: a + (b - a) * min(1.0, i / max(1, frames - 1))


def vibrato(hz, depth, speed=6):
    """A pitch function wobbling around hz by depth (a ratio) at speed frames per cycle."""
    return lambda i: hz * (1 + depth * math.sin(2 * math.pi * i / speed))


# ---- rendering -----------------------------------------------------------------------------

def render(voices, tail=0.02):
    frames = max(len(v.frames) for v in voices)
    n = int((frames / FRAME + tail) * RATE)
    channel = {"pulse": [], "triangle": [], "noise": []}
    for v in voices:
        channel[v.kind].append(render_voice(v, n))
    zeros = [0.0] * n
    pulses = channel["pulse"] + [zeros, zeros]
    tri = channel["triangle"][0] if channel["triangle"] else zeros
    noi = channel["noise"][0] if channel["noise"] else zeros
    out = []
    for i in range(n):
        p = pulses[0][i] + pulses[1][i]
        pulse_out = 0.0 if p <= 0 else 95.88 / (8128 / p + 100)
        tnd = tri[i] / 8227 + noi[i] / 12241
        tnd_out = 0.0 if tnd <= 0 else 159.79 / (1 / tnd + 100)
        out.append(pulse_out + tnd_out)
    # remove the DC offset the way the console's output capacitor does, then fade the last 5 ms
    hp, prev_in, prev_out = [], 0.0, 0.0
    for s in out:
        prev_out = 0.996 * prev_out + s - prev_in
        prev_in = s
        hp.append(prev_out)
    fade = int(0.005 * RATE)
    for i in range(fade):
        hp[n - 1 - i] *= i / fade
    return hp


def render_voice(v, n):
    out = [0.0] * n
    phase, lfsr, noise_acc = 0.0, 1, 0.0
    for i in range(n):
        f = int(i * FRAME / RATE)
        if f >= len(v.frames):
            break
        pitch, vol, extra = v.frames[f]
        if v.kind == "pulse":
            if vol and pitch:
                phase = (phase + pulse_hz(pitch) / RATE) % 1.0
                out[i] = vol if phase < (extra or 0.5) else 0
        elif v.kind == "triangle":
            if vol and pitch:   # the NES triangle has no volume: it is on or off
                phase = (phase + triangle_hz(pitch) / RATE) % 1.0
            out[i] = TRIANGLE_STEPS[int(phase * 32) % 32] if vol and pitch else 0
        else:
            noise_acc += CPU / NOISE_PERIODS[pitch] / RATE
            while noise_acc >= 1:
                noise_acc -= 1
                tap = 6 if extra else 1
                bit = (lfsr ^ (lfsr >> tap)) & 1
                lfsr = (lfsr >> 1) | (bit << 14)
            out[i] = vol if vol and not lfsr & 1 else 0
    return out


def write(name, samples, gain=0.92):
    peak = max(1e-9, max(abs(s) for s in samples))
    data = bytes(max(0, min(255, 128 + round(127 * gain * s / peak))) for s in samples)
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.wav"
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVEfmt ")
        f.write(struct.pack("<IHHIIHH", 16, 1, 1, RATE, RATE, 1, 8))
        f.write(b"data" + struct.pack("<I", len(data)) + data)
    print(f"{path.relative_to(OUT.parents[1])}  {len(samples) / RATE:.2f} s")


# ---- the sounds ----------------------------------------------------------------------------

def shot():
    """Slap of the stick and the puck leaving: a noise crack over a falling square."""
    n = Voice("noise").add(2, [15, 14]).add(5, decay(12, 6))
    p = Voice("pulse").add(slide(900, 180, 6), decay(13, 6), 0.25)
    return [n, p]


def passing():
    """A lighter stick tap."""
    n = Voice("noise").add(1, [11]).add(3, decay(7, 3))
    p = Voice("pulse").add(slide(1300, 700, 3), decay(8, 3), 0.125)
    return [n, p]


def stick():
    """The puck landing on a stick."""
    n = Voice("noise").add(0, [8, 4, 1], True)
    p = Voice("pulse").add(1600, [6, 2], 0.5)
    return [n, p]


def boards():
    """Rubber on the boards: a low boom and a rattle of the glass."""
    t = Voice("triangle").add(slide(95, 50, 9), [1] * 9)
    n = Voice("noise").add(9, decay(15, 6)).add(6, decay(6, 8), True)
    p = Voice("pulse").add(slide(140, 70, 5), decay(9, 5), 0.5)
    return [t, n, p]


def post():
    """The ping of the post: two bright tones ringing out."""
    p1 = Voice("pulse").add(note("B6"), decay(14, 42), 0.5)
    p2 = Voice("pulse").rest(1).add(vibrato(note("E7"), 0.004, 4), decay(9, 30), 0.25)
    n = Voice("noise").add(3, [13, 6], True)
    return [p1, p2, n]


def whistle():
    """A pea whistle: a thin high tone trilling between two pitches."""
    pitch = lambda i: 2800 if i % 2 == 0 else 3050
    p = Voice("pulse").add(pitch, [7, 12] + hold(12, 30, 5), 0.125)
    n = Voice("noise").add(0, [3] * 30 + decay(2, 4))
    return [p, n]


def horn():
    """The goal horn: a low, slightly sour chord held long."""
    rise = lambda base: (lambda i: base * (0.94 if i < 2 else 1.0))
    p1 = Voice("pulse").add(rise(117), [10, 13] + hold(13, 80, 14), 0.5)
    p2 = Voice("pulse").add(rise(147), [9, 12] + hold(12, 80, 14), 0.25)
    t = Voice("triangle").add(rise(58.5), [1] * 94)
    return [p1, p2, t]


def goal():
    """The goal fanfare: a quick climb to a held high note over a bass."""
    lead = Voice("pulse")
    for name in ["G4", "C5", "E5", "G5"]:
        lead.add(note(name), [12, 11, 10, 9, 9, 8], 0.25)
    lead.add(vibrato(note("C6"), 0.01), hold(12, 30, 18), 0.25)
    harm = Voice("pulse")
    for name in ["E4", "G4", "C5", "E5"]:
        harm.add(note(name), [8, 7, 7, 6, 6, 5], 0.5)
    harm.add(note("G5"), hold(8, 30, 18), 0.5)
    bass = Voice("triangle")
    for name in ["C3", "G3", "C3", "G3"]:
        bass.add(note(name), [1] * 5).rest(1)
    bass.add(note("C3"), [1] * 40)
    return [lead, harm, bass]


def save():
    """A pad or glove taking the puck: a short soft thump."""
    n = Voice("noise").add(8, decay(12, 5))
    t = Voice("triangle").add(slide(150, 80, 5), [1] * 5)
    return [n, t]


def check():
    """A body check: a heavy crunch with a falling boom."""
    n = Voice("noise").add(10, [15, 15]).add(11, decay(13, 11))
    t = Voice("triangle").add(slide(110, 40, 10), [1] * 10)
    p = Voice("pulse").add(slide(80, 55, 6), decay(10, 6), 0.5)
    return [n, t, p]


def faceoff():
    """The puck dropped on the ice: a single clack."""
    n = Voice("noise").add(3, [12, 5, 1], True)
    p = Voice("pulse").add(1000, [7], 0.125)
    return [n, p]


def crowd():
    """The crowd rising and falling: filtered noise that swells, flickers and fades."""
    rng = random.Random(7)
    n = Voice("noise")
    vols = decay(1, 20, 11) + [11 + rng.randint(-2, 1) for _ in range(60)] + decay(11, 30)
    for v in vols:
        n.add(rng.choice([6, 7, 7, 8]), [v])
    return [n]


def penalty():
    """A falling sting for a penalty call."""
    lead = Voice("pulse")
    for name in ["E5", "C5"]:
        lead.add(note(name), hold(11, 6), 0.5)
    lead.add(slide(note("A4"), note("A4") * 0.94, 30), hold(11, 18, 14), 0.5)
    bass = Voice("triangle").rest(12).add(note("A2"), [1] * 28)
    return [lead, bass]


def fight():
    """The gloves come off: a fast rising alarm over drum hits."""
    lead = Voice("pulse")
    for _ in range(2):
        for name in ["A4", "C5", "E5", "A5"]:
            lead.add(note(name), [12, 11, 9], 0.25)
    lead.add(note("A5"), hold(12, 8, 10), 0.25)
    drum = Voice("noise")
    for _ in range(4):
        drum.add(9, decay(14, 6))
    return [lead, drum]


def linechange():
    """Two soft blips for a line change."""
    p = Voice("pulse").add(note("E6"), [8, 7, 6, 4]).add(note("A6"), [8, 7, 5, 4, 2, 1])
    return [p]


def final():
    """End of the match: a slower cadence that lands on a held chord."""
    lead = Voice("pulse")
    harm = Voice("pulse")
    bass = Voice("triangle")
    for top, mid, low in [("A5", "F5", "F3"), ("B5", "G5", "G3"), ("C6", "E5", "C3")]:
        held = 12 if top != "C6" else 40
        lead.add(note(top), hold(12, held - 2) + [6, 0], 0.25)
        harm.add(note(mid), hold(8, held - 2) + [4, 0], 0.5)
        bass.add(note(low), [1] * (held - 1)).rest(1)
    lead.add(note("C6"), decay(10, 20), 0.25)
    return [lead, harm, bass]


SOUNDS = {
    "shot": shot, "pass": passing, "stick": stick, "boards": boards, "post": post,
    "whistle": whistle, "horn": horn, "goal": goal, "save": save, "check": check,
    "faceoff": faceoff, "crowd": crowd, "penalty": penalty, "fight": fight,
    "linechange": linechange, "final": final,
}


def main(names):
    for name in names or SOUNDS:
        write(name, render(SOUNDS[name]()))


if __name__ == "__main__":
    main(sys.argv[1:])
