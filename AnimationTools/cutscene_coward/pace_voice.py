# Bakes the pauses in lines.tsv (last column, "where:seconds;...") into the coward's recordings, for every voice version:
#   Voice Lines/CowardN.mp3            -> Voice Lines/Paced/Original/CowardN.wav
#   Voice Lines/<Version>/CowardN.wav  -> Voice Lines/Paced/<Version>/CowardN.wav
# Lines without pauses are copied as they are. A pause is filled with the recording's own room tone (grains of the quiet
# around the cut), cross-faded in and out - pure digital silence made the background hiss drop out and come back, which
# sounded like the audio stuttering/lagging rather than him hesitating. Each cut is moved to the quietest 10 ms within
# SNAP of where lines.tsv puts it (printed if it moved), so it falls between words.
# (Splitting the line into several Timeline clips instead made it stutter: Unity seeks into compressed audio loosely.)
# Run: python AnimationTools/cutscene_coward/pace_voice.py   (needs ffmpeg and numpy)
import os, subprocess, wave
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
VOICE = os.path.join(ROOT, "Assets", "Cutscenes", "CowardMeeting1", "Voice Lines")
VERSIONS = ["Original", "Coward_Deep", "Coward_Deeper"]
SR = 44100
XFADE = 0.025          # cross-fade into and out of the room tone
GRAIN = 0.06           # room-tone grain length
SNAP = 0.06            # how far a cut may move to find the quietest spot
rng = np.random.default_rng(7)

def load(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "2", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()

def save(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())

def energy(x, hop):
    m = x.mean(1); n = len(m) // hop
    return np.sqrt((m[:n * hop].reshape(n, hop) ** 2).mean(1))

def snap(x, at):
    # the quietest 10 ms within SNAP of `at` (seconds)
    hop = SR // 100; e = energy(x, hop)
    i = int(round(at * 100)); a, b = max(0, i - int(SNAP * 100)), min(len(e), i + int(SNAP * 100) + 1)
    return (a + int(np.argmin(e[a:b]))) / 100 + 0.005

def room_tone(x, cut, length):
    # overlap-added grains (sine window: constant power for noise) from the quietest stretches of the recording,
    # scaled to the level right at the cut
    g = int(GRAIN * SR); hop = g // 4; win = np.sin(np.linspace(0, np.pi, g))[:, None]
    e = energy(x, hop)
    f = int(XFADE * SR)
    near = min(np.sqrt((x[max(0, cut - f):cut] ** 2).mean()), np.sqrt((x[cut:cut + f] ** 2).mean()))
    # the loudest quarter-grain in each grain: only grains quiet all the way through (no word edges in them), and not
    # from hard-edited digital silence when the cut itself has the room's hiss
    grain_e = np.array([e[i:i + 4].max() for i in range(len(e) - 4)])
    ok = np.nonzero(grain_e >= near * 0.25)[0]
    if len(ok) < 4: ok = np.arange(len(grain_e))
    quiet = ok[np.argsort(grain_e[ok])[:max(4, len(ok) // 12)]]
    out = np.zeros((length + g, 2), np.float32)
    for at in range(0, length, g // 2):
        s = quiet[rng.integers(len(quiet))] * hop
        out[at:at + g] += x[s:s + g] * win
    out = out[:length]
    # never louder than the line is right at the cut (a hard-edited silence stays silent)
    have = np.sqrt((out ** 2).mean()) + 1e-9
    return out * min(near / have, 1.0)

def pace(x, pauses):
    f = int(XFADE * SR)
    fade_in = np.sin(np.linspace(0, np.pi / 2, f))[:, None]; fade_out = fade_in[::-1]
    shift = 0
    for at, gap in pauses:
        cut, n = int(round(at * SR)) + shift, int(round(gap * SR))
        tone = room_tone(x, cut, n)
        tone[:f] *= fade_in; tone[-f:] *= fade_out
        a = x[:cut + f].copy(); a[cut:] *= fade_out                 # the line dies away into the room tone...
        b = x[cut - f:].copy(); b[:f] *= fade_in                     # ...and comes back out of it
        y = np.zeros((len(x) + n, 2), np.float32)
        y[:len(a)] += a
        y[cut:cut + n] += tone
        y[cut + n - f:] += b
        x, shift = y, shift + n
    return x

def recordings():
    for line in open(os.path.join(os.path.dirname(__file__), "lines.tsv"), encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if line.startswith("#") or len(f) < 6 or not f[5].startswith("Coward"): continue
        pauses = [(float(a), float(b)) for a, b in (p.split(":") for p in f[7].split(";") if p)] if len(f) > 7 else []
        yield f[5], pauses

for rec, pauses in recordings():
    name = os.path.splitext(rec)[0]
    # the cuts are placed on the original recording (every version keeps its timing)
    orig = load(os.path.join(VOICE, rec))
    snapped = [(snap(orig, at), gap) for at, gap in pauses]
    for (at, _), (s, _) in zip(pauses, snapped):
        if abs(s - at) > 0.006: print("  %s: cut %.2f -> %.3f (quieter) - put it in lines.tsv" % (rec, at, s))
    for version in VERSIONS:
        src = os.path.join(VOICE, rec) if version == "Original" else os.path.join(VOICE, version, name + ".wav")
        if not os.path.exists(src): continue
        save(os.path.join(VOICE, "Paced", version, name + ".wav"), pace(load(src), snapped))
    print(rec, snapped)
