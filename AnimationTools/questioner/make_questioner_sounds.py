# The Questioner's move cues (spec: every attack has a wind-up AND an audio cue) into Assets/Audio/Enemies/Questioner/Moves/
# (44.1 kHz mono 16-bit):
#   Q_Inhale          - the scream wind-up: a sharp, rasping inhale (band-passed noise rising in pitch + throat rasp)
#   Q_Hiss            - the swipe wind-up: short throat hiss
#   Q_Claw_1..2       - the claw swipes: fast light whoosh
#   Q_Bite            - teeth crunch + wet layer (BulletFlesh) + low thump
#   Q_GrabCue         - the grab wind-up: cloth rustle + a low grunt (the opening burst of his own Questioner_Attack2)
#   Q_Muffled_1,2,5   - stagger: his cry through the hands clamped over his mouth (Monster hurt sounds, slowed + low-passed)
#   Q_Whisper         - the hold: 2 s cut of Wall Whisper.mp3 (placeholder until the real "وين أبوك؟" line is recorded)
#   Q_ScreamRing      - the scream in Khalid's ears: a high ringing (tinnitus) that swells in, holds and dies away over ~6.5 s
#   python AnimationTools/questioner/make_questioner_sounds.py [name name ...]     (needs ffmpeg on PATH; no names = all)
import numpy as np, wave, os, subprocess, tempfile, sys

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
OUT = ROOT + "Assets/Audio/Enemies/Questioner/Moves/"
AUD = ROOT + "Assets/Audio/"
SR = 44100
os.makedirs(OUT, exist_ok=True)


ONLY = set(sys.argv[1:])


def save(name, x, peak):
    if ONLY and name not in ONLY: return
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(OUT + name + ".wav", "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def load(path, start=0.0, dur=None, rate=1.0):
    """any audio file -> mono float at SR via ffmpeg; start + dur are in source seconds; rate < 1 = pitched + slowed down
    (resampled to more samples, played back at SR)"""
    tmp = tempfile.mktemp(suffix=".wav")
    cmd = ["ffmpeg", "-v", "error", "-y", "-ss", str(start), "-i", path]
    if dur: cmd += ["-t", str(dur)]
    cmd += ["-ac", "1", "-ar", str(int(SR / rate)), "-sample_fmt", "s16", tmp]
    subprocess.run(cmd, check=True)
    with wave.open(tmp) as w:
        x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64) / 32768
    os.remove(tmp)
    return x          # (played back at SR: rate < 1 lowers the pitch)


def t_axis(dur): return np.arange(int(dur * SR)) / SR


def bandpass_tv(x, fc, q):
    y = np.zeros_like(x); z1 = z2 = 0.0
    for i in range(len(x)):
        w0 = 2 * np.pi * fc[i] / SR
        alpha = np.sin(w0) / (2 * q)
        b0, a0, a1, a2 = alpha, 1 + alpha, -2 * np.cos(w0), 1 - alpha
        out = (b0 / a0) * x[i] + z1
        z1 = -(a1 / a0) * out + z2
        z2 = (-b0 / a0) * x[i] - (a2 / a0) * out
        y[i] = out
    return y


def lowpass(x, fc, passes=1):
    a = np.exp(-2 * np.pi * fc / SR)
    for _ in range(passes):
        y = np.zeros_like(x); s = 0.0
        for i in range(len(x)):
            s = (1 - a) * x[i] + a * s; y[i] = s
        x = y
    return x


def fade(x, a=0.01, r=0.05):
    n = len(x); e = np.ones(n)
    na, nr = int(a * SR), int(r * SR)
    if na: e[:na] = np.linspace(0, 1, na)
    if nr: e[-nr:] = np.linspace(1, 0, nr)
    return x * e


def inhale(seed=1, dur=0.75):
    rng = np.random.default_rng(seed)
    t = t_axis(dur); p = t / dur
    n = rng.standard_normal(len(t))
    air = bandpass_tv(n, 900 + 1900 * p ** 1.3, 2.2)
    env = (p ** 1.6) * np.where(p < 0.93, 1.0, np.cos(0.5 * np.pi * (p - 0.93) / 0.07))
    rasp = lowpass(n, 500) * (0.55 + 0.45 * np.sign(np.sin(2 * np.pi * 63 * t)))   # vocal-fold flutter
    return fade((air * 1.0 + rasp * 0.9) * env, 0.02, 0.02)


def hiss(seed=2, dur=0.6):
    rng = np.random.default_rng(seed)
    t = t_axis(dur); p = t / dur
    n = rng.standard_normal(len(t))
    hi = n - lowpass(n, 2600)
    body = bandpass_tv(n, np.full(len(t), 5200.0), 1.4)
    env = np.clip(p / 0.08, 0, 1) * np.clip((1 - p) / 0.35, 0, 1) * (1 + 0.15 * np.sin(2 * np.pi * 9 * t))
    return fade((hi * 0.5 + body) * env, 0.005, 0.03)


def claw(seed, dur=0.22):
    rng = np.random.default_rng(seed)
    t = t_axis(dur); p = t / dur
    n = rng.standard_normal(len(t))
    fc = 700 + 2300 * np.sin(np.pi * np.clip(p, 0, 1)) ** 2
    w = bandpass_tv(n, fc, 1.6)
    env = np.sin(np.pi * np.clip(p / 0.85, 0, 1)) ** 2
    flap = lowpass(n, 300) * np.exp(-((p - 0.45) / 0.08) ** 2) * 2.0           # sleeve / rag flap
    return fade(w * env + flap, 0.003, 0.02)


def bite(seed=3):
    rng = np.random.default_rng(seed)
    dur = 0.55; t = t_axis(dur)
    x = np.zeros(len(t))
    # crunch: a cluster of sharp clicks
    for k in range(9):
        at = int((0.012 + k * 0.011 + rng.uniform(-0.003, 0.003)) * SR)
        ln = int(rng.uniform(0.002, 0.007) * SR)
        b = rng.standard_normal(ln) * np.exp(-np.arange(ln) / (ln * 0.3))
        x[at:at + ln] += b * rng.uniform(0.5, 1.0)
    x = bandpass_tv(x, np.full(len(t), 2400.0), 0.9) * 1.6
    thump = np.sin(2 * np.pi * (95 - 40 * t) * t) * np.exp(-t / 0.06)
    wet = load(AUD + "BloodSounds/BulletFlesh2.wav")[:len(t)]
    wet = np.pad(wet, (0, len(t) - len(wet)))
    return fade(x + thump * 0.8 + wet * 0.9, 0.002, 0.1)


def grab_cue(seed=4):
    rng = np.random.default_rng(seed)
    dur = 0.9; t = t_axis(dur)
    n = rng.standard_normal(len(t))
    gate = lowpass(np.abs(rng.standard_normal(len(t))), 25) * 3.0
    cloth = bandpass_tv(n, np.full(len(t), 3000.0), 0.8) * gate * np.clip(1 - t / dur, 0, 1)
    grunt = load(AUD + "Enemies/Questioner/Questioner_Attack2.mp3", start=0.03, dur=0.27, rate=0.9)
    grunt = fade(lowpass(grunt, 1800), 0.005, 0.06) * 0.65   # louder source than the cloth was balanced against
    grunt = np.pad(grunt, (int(0.08 * SR), 0))[:len(t)]
    grunt = np.pad(grunt, (0, len(t) - len(grunt)))
    return fade(cloth * 0.55 + grunt * 1.0, 0.01, 0.12)


# (hurt sound, rate): slowed well below the Monster hurt sound that plays at the same moment, so the cry sits under it
# and rings on after it instead of doubling it
MUFFLED = {1: (1, 0.80), 2: (2, 0.85), 5: (2, 0.72)}


def muffled(i):
    src, rate = MUFFLED[i]
    v = load(AUD + "MonsterHunt/Monster hurt sound %d.mp3" % src, rate=rate)
    v = lowpass(v, 520, passes=2) * 3.0                    # hands clamped over the mouth
    return fade(v, 0.01, 0.08)


def ring(seed=5, dur=6.6):
    """tinnitus: two close high tones beating against each other, a slow wobble, a breath of hiss under it"""
    rng = np.random.default_rng(seed)
    t = t_axis(dur)
    wob = 1 + 0.0025 * np.sin(2 * np.pi * 3.1 * t)
    ph1 = 2 * np.pi * np.cumsum(6650.0 * wob) / SR
    ph2 = 2 * np.pi * np.cumsum(6712.0 * wob) / SR
    tone = np.sin(ph1) + 0.7 * np.sin(ph2) + 0.12 * np.sin(2 * np.pi * 3325.0 * t)
    hiss = bandpass_tv(rng.standard_normal(len(t)), np.full(len(t), 6000.0), 1.5) * 0.15
    env = np.minimum(t / 0.12, 1.0) * np.exp(-np.maximum(t - 0.5, 0) / 3.2) * (0.75 + 0.25 * np.exp(-t / 0.6))
    return fade((tone + hiss) * env, 0.005, 0.4)


save("Q_ScreamRing", ring(), 0.55)
save("Q_Inhale", inhale(), 0.85)
save("Q_Hiss", hiss(), 0.7)
save("Q_Claw_1", claw(11), 0.7)
save("Q_Claw_2", claw(12), 0.7)
save("Q_Bite", bite(), 0.95)
save("Q_GrabCue", grab_cue(), 0.85)
for i in (1, 2, 5):
    save("Q_Muffled_%d" % i, muffled(i), 0.85)
save("Q_Whisper", fade(load(AUD + "Wall Whisper/Wall Whisper.mp3", start=0.3, dur=2.0), 0.15, 0.4), 0.8)
print("done", sorted(os.listdir(OUT)))
