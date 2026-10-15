#!/usr/bin/env python3
"""Generates the game's NES-style chiptune music into art/audio/music/.

The tracks use the voices of the NES APU: two pulse voices (duty 12.5, 25 or 50 %), a triangle
for the bass and a noise voice for the drums. Pitches snap to the NES timer steps like in
tools/sfx/sfx.py, but each note gets its own attack and release envelope and band-limited edges
so nothing clicks, and the voices are mixed linearly with headroom and written as 16-bit mono WAV.

Loops are rendered twice in a row and the second pass is kept: it already carries the tails of the
notes that ring over the loop point, so its last sample runs straight into its first.

Run from anywhere:  python3 tools/music/music.py              (writes every track)
                    python3 tools/music/music.py menu final   (writes only those)
                    python3 tools/music/music.py --check      (prints loop and level numbers)
Nothing is random at run time: the same script always writes the same files.
"""

import array
import math
import pathlib
import struct
import sys

RATE = 22050                 # output sample rate, the same as the sound effects
CPU = 1789773                # NTSC CPU clock; pulse, triangle and noise timers count from it
NOISE_PERIODS = [4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068]
TRIANGLE_STEPS = list(range(15, -1, -1)) + list(range(16))
PEAK = 0.79                  # every track is scaled to this peak, about -2 dBFS
OUT = pathlib.Path(__file__).resolve().parents[2] / "art" / "audio" / "music"


# ---- notes and chords ----------------------------------------------------------------------

NAMES = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
         "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}

CHORDS = {"": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "m7": [0, 3, 7, 10]}


def midi(name):
    """MIDI number of a note such as 'A4' or 'C#5'."""
    return NAMES[name[:-1]] + 12 * (int(name[-1]) + 1)


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def chord(name):
    """Root pitch class and intervals of a chord such as 'Bm' or 'C7'."""
    root = name[:2] if len(name) > 1 and name[1] in "#b" else name[:1]
    return NAMES[root], CHORDS[name[len(root):]]


def in_range(pc, low):
    """The MIDI note of pitch class pc at or just above low."""
    return low + (pc - low) % 12


def pulse_hz(f):
    """Snaps a pulse frequency to the nearest NES 11-bit timer value."""
    t = max(8, min(2047, round(CPU / (16 * f) - 1)))
    return CPU / (16 * (t + 1))


def triangle_hz(f):
    """Snaps a triangle frequency to the nearest NES timer value (it runs an octave lower)."""
    t = max(2, min(2047, round(CPU / (32 * f) - 1)))
    return CPU / (32 * (t + 1))


# ---- envelopes and voices ------------------------------------------------------------------

def envelope(gate, attack, decay, sustain, release, fall=0.0):
    """Per-sample levels: a linear attack, a decay to sustain, an optional exponential fall while
    held, and a linear release from wherever the note was at note-off down to exactly zero."""
    a, d, r = max(1, int(attack * RATE)), int(decay * RATE), max(1, int(release * RATE))
    out = []
    for i in range(gate):
        if i < a:
            e = i / a
        elif i < a + d:
            e = 1 - (1 - sustain) * (i - a) / d
        else:
            e = sustain
        if fall and i >= a:
            e *= math.exp(-fall * (i - a) / RATE)
        out.append(e)
    last = out[-1] if out else 0.0
    out.extend(last * (1 - i / r) for i in range(1, r + 1))
    return out


def polyblep(t, dt):
    """Smooths a step at phase 0 so the pulse edges do not alias."""
    if t < dt:
        t /= dt
        return t + t - t * t - 1
    if t > 1 - dt:
        t = (t - 1) / dt
        return t * t + t + t + 1
    return 0.0


def pulse(buf, start, env, tones, duty, vol, vibrato=0.0):
    """A pulse note. Several tones cycle once per 60 Hz frame, the NES way of playing a chord."""
    tones = [pulse_hz(t) for t in tones]
    dc = 2 * duty - 1
    phase = 0.0
    for i, e in enumerate(env):
        j = start + i
        if j >= len(buf):
            break
        f = tones[(i * 60 // RATE) % len(tones)]
        if vibrato:
            t = i / RATE
            if t > 0.12:
                f *= 1 + vibrato * min(1.0, (t - 0.12) / 0.25) * math.sin(2 * math.pi * 5.5 * t)
        dt = f / RATE
        phase += dt
        if phase >= 1:
            phase -= 1
        y = (1.0 if phase < duty else -1.0) - dc + polyblep(phase, dt) - polyblep((phase - duty) % 1.0, dt)
        buf[j] += vol * e * y


def triangle(buf, start, env, pitch, vol):
    """A triangle note from the NES's 32-step table. Pitch may be a function of time in seconds."""
    phase = 0.0
    for i, e in enumerate(env):
        j = start + i
        if j >= len(buf):
            break
        f = pitch(i / RATE) if callable(pitch) else pitch
        phase = (phase + triangle_hz(f) / RATE) % 1.0
        buf[j] += vol * e * (TRIANGLE_STEPS[int(phase * 32)] - 7.5) / 7.5


def noise(buf, start, env, period, vol, short=False):
    """A noise hit from the NES's 15-bit shift register, restarted for every hit."""
    lfsr, acc = 1, 0.0
    step = CPU / NOISE_PERIODS[period] / RATE
    tap = 6 if short else 1
    for i, e in enumerate(env):
        j = start + i
        if j >= len(buf):
            break
        acc += step
        while acc >= 1:
            acc -= 1
            bit = (lfsr ^ (lfsr >> tap)) & 1
            lfsr = (lfsr >> 1) | (bit << 14)
        buf[j] += vol * e * (1.0 if lfsr & 1 else -1.0)


# ---- a song: notes placed on a grid of sixteenth-note steps ---------------------------------

class Song:
    def __init__(self, step, bar_steps, bars):
        self.step = step                   # samples per sixteenth note (a whole number)
        self.bar_steps = bar_steps         # sixteenths per bar
        self.bars = bars
        self.length = step * bar_steps * bars
        self.notes = []                    # (start sample, voice function, arguments)

    def at(self, bar, step=0):
        return (bar * self.bar_steps + step) * self.step

    def add(self, start, fn, *args):
        self.notes.append((start, fn, args))

    def melody(self, line, duty, vol, vibrato=0.0, attack=0.004, sustain=0.75, release=0.04):
        """Places a line such as 'A4:2 D5:2 -:4' (note:sixteenths, '-' rests) from bar 0."""
        pos = 0
        for token in line.split():
            name, steps = token.split(":")
            steps = int(steps)
            if name != "-":
                gap = min(steps * self.step // 4, int(0.03 * RATE))
                env = envelope(steps * self.step - gap, attack, 0.09, sustain, release)
                self.add(pos * self.step, pulse, env, [hz(midi(name))], duty, vol, vibrato)
            pos += steps

    def render(self, loop):
        """Mixes every note. A loop is played twice and the second pass kept, so the tails that
        ring past the loop point are already folded onto its start."""
        passes = 2 if loop else 1
        tail = 0 if loop else int(1.0 * RATE)
        buf = [0.0] * (self.length * passes + tail)
        for p in range(passes):
            for start, fn, args in self.notes:
                fn(buf, start + p * self.length, *args)
        # a gentle low-pass, like the console's output stage, then keep the last pass
        a = 1 - math.exp(-2 * math.pi * 6000 / RATE)
        y = 0.0
        for i, s in enumerate(buf):
            y += a * (s - y)
            buf[i] = y
        return buf[self.length:] if loop else buf


def chords_per_bar(song, progression):
    """Expands a list of bars such as ['D', 'Am F'] to (start step, length, chord) spans."""
    spans = []
    for bar, names in enumerate(progression):
        names = names.split()
        each = song.bar_steps // len(names)
        for k, name in enumerate(names):
            spans.append((bar * song.bar_steps + k * each, each, name))
    return spans


def bass_line(song, spans, pattern, vol, gate=0.8):
    """Triangle bass: pattern holds (step within span, length, semitones above the root)."""
    for start, length, name in spans:
        root = in_range(chord(name)[0], midi("E2"))
        for off, steps, interval in pattern:
            if off >= length:
                continue
            env = envelope(int(steps * gate * song.step), 0.003, 0, 1, 0.015)
            song.add((start + off) * song.step, triangle, env, hz(root + interval), vol)


def arpeggio(song, spans, order, low, duty, vol):
    """Sixteenth-note arpeggio over each chord, walking its tones in the given order."""
    for start, length, name in spans:
        pc, iv = chord(name)
        root = in_range(pc, low)
        tones = [root + i for i in iv[:3]] + [root + 12]
        for s in range(length):
            env = envelope(int(song.step * 0.7), 0.003, 0.05, 0.5, 0.03, fall=6)
            song.add((start + s) * song.step, pulse, env, [hz(tones[order[s % len(order)]])], duty, vol)


def kick(song, at, vol):
    sweep = lambda t: 45 + 120 * math.exp(-t * 35)
    song.add(at, triangle, envelope(int(0.17 * RATE), 0.001, 0, 1, 0.01, fall=14), sweep, vol)
    song.add(at, noise, envelope(int(0.006 * RATE), 0.0005, 0, 1, 0.004), 8, vol * 0.3)


def snare(song, at, vol):
    song.add(at, noise, envelope(int(0.16 * RATE), 0.001, 0, 1, 0.01, fall=20), 6, vol)
    song.add(at, triangle, envelope(int(0.05 * RATE), 0.001, 0, 1, 0.01, fall=40), lambda t: 190 - 400 * t, vol * 0.8)


def hat(song, at, vol, open_=False):
    length, fall = (0.14, 12) if open_ else (0.05, 60)
    song.add(at, noise, envelope(int(length * RATE), 0.0005, 0, 1, 0.008, fall=fall), 0, vol)


def crash(song, at, vol):
    song.add(at, noise, envelope(int(1.4 * RATE), 0.001, 0, 1, 0.05, fall=2.6), 2, vol)


def rock_drums(song, bar, style, vol=1.0):
    """One bar of 4/4 drums. Style: 'verse', 'drive' (sixteenth hats), 'stomp' (four on the floor)
    or 'fill' (a snare run into the next bar)."""
    at = lambda s: song.at(bar, s)
    kicks = {"verse": [0, 8, 10] if bar % 2 else [0, 8], "drive": [0, 6, 8, 10], "stomp": [0, 4, 8, 12]}
    for s in kicks.get(style, [0, 8]):
        kick(song, at(s), 0.42 * vol)
    for s in [4, 12] if style != "fill" else [4]:
        snare(song, at(s), 0.2 * vol)
    if style == "fill":
        for k, s in enumerate([8, 10, 12, 13, 14, 15]):
            snare(song, at(s), (0.12 + 0.02 * k) * vol)
        return
    hats = range(16) if style == "drive" else range(0, 16, 2)
    for s in hats:
        hat(song, at(s), (0.07 if s % 4 == 2 else 0.045) * vol, open_=(style == "stomp" and s % 4 == 2))


# ---- the tracks ----------------------------------------------------------------------------

def menu():
    """Title loop: a driving arena anthem in D major, 150 BPM, 24 bars."""
    song = Song(step=RATE * 60 // 150 // 4, bar_steps=16, bars=24)
    progression = ["D", "A", "Bm", "G", "D", "A", "Bm", "G",
                   "Bm", "G", "D", "A", "Bm", "G", "Em", "A",
                   "G", "A", "Bm", "Bm", "G", "A", "D", "A"]
    song.melody(
        # A: the hook
        "A4:2 D5:2 F#5:4 E5:2 D5:2 E5:4  C#5:2 E5:2 A5:6 G5:2 F#5:2 E5:2 "
        "F#5:4 D5:2 B4:2 D5:2 F#5:2 B5:4  A5:2 G5:2 F#5:2 G5:2 D5:8 "
        "A4:2 D5:2 F#5:4 E5:2 D5:2 A5:4  G5:2 F#5:2 E5:4 C#5:2 E5:2 A4:4 "
        "B4:2 D5:2 F#5:4 E5:2 F#5:2 G5:4  A5:4 G5:2 F#5:2 E5:8 "
        # B: climbing, off the beat
        "-:2 B5:2 B5:2 A5:2 B5:4 F#5:4  -:2 G5:2 G5:2 F#5:2 G5:4 D5:4 "
        "-:2 A5:2 A5:2 G5:2 F#5:4 A5:4  E5:4 C#5:2 E5:2 A5:8 "
        "-:2 B5:2 B5:2 A5:2 B5:4 D6:4  B5:4 G5:2 B5:2 D6:8 "
        "E6:4 D6:2 B5:2 G5:4 E5:4  C#6:6 B5:2 A5:4 E5:4 "
        # C: the crowd chant
        "D5:2 G5:2 B5:2 D6:6 B5:2 D6:2  C#6:4 C#6:2 B5:2 A5:4 E5:4 "
        "F#5:2 B5:2 D6:2 F#6:6 E6:2 D6:2  C#6:4 D6:4 B5:8 "
        "D6:2 D6:2 -:2 D6:2 -:2 D6:2 E6:4  E6:2 E6:2 -:2 E6:2 -:2 E6:2 C#6:4 "
        "F#6:4 E6:2 D6:2 A5:4 F#5:4  E5:4 G5:4 F#5:2 E5:2 C#5:4",
        duty=0.25, vol=0.3, vibrato=0.005)
    spans = chords_per_bar(song, progression)
    arpeggio(song, spans[:16], [0, 1, 2, 3, 2, 1, 2, 3], midi("A3"), 0.125, 0.11)
    arpeggio(song, spans[16:], [0, 2, 3, 2, 1, 2, 3, 2], midi("A3"), 0.25, 0.1)
    rock = [(0, 2, 0), (2, 2, 0), (4, 2, 12), (6, 2, 0), (8, 2, 0), (10, 2, 12), (12, 2, 7), (14, 2, 12)]
    bass_line(song, spans, rock, 0.38)
    crash(song, 0, 0.1)
    crash(song, song.at(16), 0.1)
    for bar in range(24):
        style = "fill" if bar % 8 == 7 else ["verse", "drive", "stomp"][bar // 8]
        rock_drums(song, bar, style)
    return song, True


def intermission():
    """Intermission loop: a public-skate organ waltz in F major, 3/4 at 126 BPM, 24 bars."""
    song = Song(step=RATE * 60 // 126 // 4, bar_steps=12, bars=24)
    progression = ["F", "Dm", "Gm", "C", "F", "Dm", "Gm", "C",
                   "Bb", "Bb", "F", "D7", "Gm", "C7", "F", "C7",
                   "F", "Dm", "Gm", "C", "Am", "Dm", "Gm", "C7"]
    song.melody(
        "A4:4 C5:4 F5:4  A5:8 F5:4  G5:4 A5:4 Bb5:4  G5:8 E5:4 "
        "F5:4 A5:4 C6:4  D6:8 A5:4  Bb5:4 A5:4 G5:4  E5:8 C5:4 "
        "D5:4 F5:4 Bb5:4  D6:8 Bb5:4  C6:4 A5:4 F5:4  F#5:8 A5:4 "
        "G5:4 Bb5:4 D6:4  E6:4 D6:4 Bb5:4  A5:12  G5:4 E5:4 C5:4 "
        "A4:4 C5:4 F5:4  A5:8 F5:4  G5:4 A5:4 Bb5:4  C6:8 G5:4 "
        "A5:4 C6:4 E6:4  D6:8 A5:4  Bb5:4 A5:4 G5:4  G5:4 E5:4 C5:4",
        duty=0.5, vol=0.26, vibrato=0.007, attack=0.015, sustain=0.85, release=0.08)
    spans = chords_per_bar(song, progression)
    for bar, (start, length, name) in enumerate(spans):
        pc, iv = chord(name)
        root = in_range(pc, midi("E2"))
        # oom: the bass on the downbeat, root and fifth on alternate bars
        low = root if bar % 2 == 0 else root - 5
        song.add(start * song.step, triangle, envelope(int(3.5 * song.step), 0.004, 0, 1, 0.03), hz(low), 0.4)
        # pah-pah: the chord as a 60 Hz arpeggio on the second pulse, like an organ stab
        top = in_range(pc, midi("F4"))
        tones = [hz(top + i) for i in iv[1:]] + [hz(top + 12)]
        for beat in (4, 8):
            env = envelope(int(2.8 * song.step), 0.01, 0.08, 0.6, 0.06)
            song.add((start + beat) * song.step, pulse, env, tones, 0.25, 0.09)
        kick(song, start * song.step, 0.2)
        for beat in (4, 8):
            hat(song, (start + beat) * song.step, 0.035)
    return song, True


def final():
    """End-of-game theme: a fanfare in C major that lands on a long held chord and fades out."""
    song = Song(step=RATE * 60 // 126 // 4, bar_steps=16, bars=7)
    progression = ["C", "F", "G", "Am F", "G"]
    song.melody(
        "G4:2 C5:2 E5:2 G5:6 E5:2 G5:2  A5:4 F5:2 A5:2 C6:8  B5:4 G5:2 B5:2 D6:6 C6:2 "
        "C6:4 A5:4 A5:2 C6:2 F6:4  E6:4 D6:4 B5:2 C6:2 D6:4",
        duty=0.25, vol=0.3, vibrato=0.005)
    spans = chords_per_bar(song, progression)
    arpeggio(song, spans, [0, 1, 2, 3, 2, 1, 2, 3], midi("G3"), 0.125, 0.11)
    rock = [(0, 2, 0), (2, 2, 0), (4, 2, 12), (6, 2, 0), (8, 2, 0), (10, 2, 12), (12, 2, 7), (14, 2, 12)]
    bass_line(song, spans, rock, 0.38)
    for bar in range(4):
        rock_drums(song, bar, "verse" if bar < 2 else "drive")
    for s in range(16):                                   # a snare roll into the last chord
        snare(song, song.at(4, s), 0.07 + 0.012 * s)
    kick(song, song.at(4, 0), 0.42)
    kick(song, song.at(4, 8), 0.42)
    # the last chord: held over two bars and fading to nothing
    end, held = song.at(5), 2 * 16 * song.step
    song.add(end, pulse, envelope(held, 0.004, 0.1, 0.9, 0.5, fall=1.1), [hz(midi("C6"))], 0.25, 0.3, 0.006)
    song.add(end, pulse, envelope(held, 0.004, 0.1, 0.8, 0.5, fall=1.3),
             [hz(midi(n)) for n in ("E5", "G5", "C6")], 0.125, 0.12)
    song.add(end, triangle, envelope(held, 0.003, 0, 1, 0.5, fall=1.0), hz(midi("C2")), 0.4)
    kick(song, end, 0.45)
    crash(song, end, 0.13)
    return song, False


TRACKS = {"menu": menu, "intermission": intermission, "final": final}


# ---- output --------------------------------------------------------------------------------

def write(name, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    data = array.array("h", (max(-32767, min(32767, round(32767 * PEAK * s / peak))) for s in samples))
    if sys.byteorder == "big":
        data.byteswap()
    raw = data.tobytes()
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.wav"
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 36 + len(raw)) + b"WAVEfmt ")
        f.write(struct.pack("<IHHIIHH", 16, 1, 1, RATE, RATE * 2, 2, 16))
        f.write(b"data" + struct.pack("<I", len(raw)) + raw)
    print(f"{path.relative_to(OUT.parents[2])}  {len(samples) / RATE:.2f} s")


def read(path):
    raw = path.read_bytes()
    data = array.array("h")
    data.frombytes(raw[44:])
    if sys.byteorder == "big":
        data.byteswap()
    return data


def check():
    """Prints, per track, the jump from the last sample back to the first, the largest jump
    between neighbouring samples anywhere in the file, and the peak level."""
    for name, fn in TRACKS.items():
        s = read(OUT / f"{name}.wav")
        wrap = abs(s[-1] - s[0])
        step = max(abs(s[i + 1] - s[i]) for i in range(len(s) - 1))
        peak = max(abs(v) for v in s)
        kind = "loop " if fn is not final else "ends "
        extra = f"wrap jump {wrap:5d}" if kind == "loop " else f"last sample {s[-1]:5d}"
        print(f"{name:13s} {kind} {len(s) / RATE:6.2f} s  {extra}  max neighbour jump {step:5d}  "
              f"peak {peak:5d} ({20 * math.log10(peak / 32767):.1f} dBFS)")


def main(args):
    if "--check" in args:
        check()
        return
    for name in args or TRACKS:
        song, loop = TRACKS[name]()
        write(name, song.render(loop))


if __name__ == "__main__":
    main(sys.argv[1:])
