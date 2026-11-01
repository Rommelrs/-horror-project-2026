# Synthesises the pipe melee sounds into Assets/Audio/Melee/ (44.1 kHz mono 16-bit):
#   PipeSwing_1..3     - whoosh (band-passed noise sweeping up and down + a low "whum" for weight)
#   PipeHitFlesh_1..4  - heavy body hit: pitch-dropping thump + meaty slap + a wet layer cut from BulletFlesh*.wav + faint
#                        hollow-steel ring
#   PipeHitWall_1..2   - steel pipe striking a wall: click + loud inharmonic tube ring
#   PipeReady          - grip / cloth shuffle when he raises the pipe
#   python AnimationTools/melee/make_melee_sounds.py
import numpy as np, wave, os, glob

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
OUT = ROOT + "Assets/Audio/Melee/"
SR = 44100
os.makedirs(OUT, exist_ok=True)
# BulletFlesh*.wav converted to 16-bit 44.1 kHz mono first (ffmpeg -ac 1 -ar 44100 -sample_fmt s16) into this folder
WET_DIR = os.environ.get("MELEE_WET_DIR", ROOT + "AnimationTools/melee/wet/")


def save(name, x, peak):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(OUT + name + ".wav", "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def t_axis(dur): return np.arange(int(dur * SR)) / SR


def bandpass_tv(x, fc, q):
    """2-pole resonant band-pass whose centre frequency follows the array fc (per sample)"""
    y = np.zeros_like(x)
    z1 = z2 = 0.0
    for i in range(len(x)):
        w0 = 2 * np.pi * fc[i] / SR
        alpha = np.sin(w0) / (2 * q)
        b0, a0, a1, a2 = alpha, 1 + alpha, -2 * np.cos(w0), 1 - alpha
        # direct form II transposed
        out = (b0 / a0) * x[i] + z1
        z1 = -(a1 / a0) * out + z2
        z2 = (-b0 / a0) * x[i] - (a2 / a0) * out
        y[i] = out
    return y


def lowpass(x, fc):
    a = np.exp(-2 * np.pi * fc / SR)
    y = np.zeros_like(x); s = 0.0
    for i in range(len(x)):
        s = (1 - a) * x[i] + a * s; y[i] = s
    return y


def env_exp(t, tau): return np.exp(-t / tau)


def whoosh(seed, dur=0.34, peak_at=0.42, f_lo=320, f_hi=1250):
    rng = np.random.default_rng(seed)
    t = t_axis(dur)
    p = t / dur
    bell = np.sin(np.pi * np.clip(p / (2 * peak_at), 0, 0.5)) ** 2
    bell = np.where(p < peak_at, bell, np.cos(0.5 * np.pi * (p - peak_at) / (1 - peak_at)) ** 2)
    fc = f_lo + (f_hi - f_lo) * bell
    n = rng.standard_normal(len(t))
    air = bandpass_tv(n, fc, 1.3) * bell
    whum = lowpass(rng.standard_normal(len(t)), 180) * bell ** 1.5 * 6
    return air + whum * 0.6


def read_wav(path):
    with wave.open(path, "rb") as w:
        n, ch, sw, sr = w.getnframes(), w.getnchannels(), w.getsampwidth(), w.getframerate()
        raw = w.readframes(n)
    if sw != 2: return None
    a = np.frombuffer(raw, dtype=np.int16).astype(np.float64) / 32768
    if ch > 1: a = a.reshape(-1, ch).mean(axis=1)
    if sr != SR: a = np.interp(np.arange(0, len(a), sr / SR), np.arange(len(a)), a)
    return a


def resample(a, factor):            # factor < 1 = lower pitch, longer
    return np.interp(np.arange(0, len(a) - 1, factor), np.arange(len(a)), a)


WET = [w for w in (read_wav(p) for p in sorted(glob.glob(WET_DIR + "BulletFlesh*.wav"))) if w is not None]
print("wet layers", len(WET))


def flesh_hit(seed, pitch=1.0):
    rng = np.random.default_rng(seed)
    dur = 0.55
    t = t_axis(dur)
    # thump: 110 -> 48 Hz pitch drop
    f = 48 + 62 * np.exp(-t / 0.035)
    f *= pitch
    phase = 2 * np.pi * np.cumsum(f) / SR
    thump = (np.sin(phase) + 0.25 * np.sin(2 * phase)) * env_exp(t, 0.07)
    # slap / crunch: short bright burst + meaty mid band
    n = rng.standard_normal(len(t))
    slap = lowpass(n, 3200) * env_exp(t, 0.010) * 1.4
    meat = bandpass_tv(rng.standard_normal(len(t)), np.full(len(t), 700 * pitch), 1.6) * env_exp(t, 0.045) * 3.0
    # wet layer
    wet = np.zeros(len(t))
    if WET:
        w = resample(WET[seed % len(WET)], 0.82 * pitch)[: len(t)]
        wet[: len(w)] = w * 0.9
    # hollow steel ring (faint - it's a pipe, but it hit flesh)
    ring = sum(a * np.sin(2 * np.pi * fr * pitch * t + rng.uniform(0, 6)) * env_exp(t, d)
               for fr, d, a in ((520, 0.22, 0.05), (1370, 0.15, 0.035), (2650, 0.10, 0.02), (4100, 0.07, 0.012)))
    x = thump * 1.0 + slap + meat + wet + ring
    x = np.tanh(x * 1.6)                  # glue / weight
    return x


def wall_hit(seed, pitch=1.0):
    rng = np.random.default_rng(seed)
    dur = 1.1
    t = t_axis(dur)
    click = lowpass(rng.standard_normal(len(t)), 6000) * env_exp(t, 0.004) * 2.0
    ring = sum(a * np.sin(2 * np.pi * fr * pitch * rng.uniform(0.98, 1.02) * t + rng.uniform(0, 6)) * env_exp(t, d)
               for fr, d, a in ((612, 0.55, 0.5), (1655, 0.42, 0.42), (3060, 0.30, 0.3), (4790, 0.20, 0.2), (6950, 0.12, 0.12)))
    f = 70 * pitch
    thud = np.sin(2 * np.pi * f * t) * env_exp(t, 0.05) * 0.5
    grit = bandpass_tv(rng.standard_normal(len(t)), np.full(len(t), 2400), 0.9) * env_exp(t, 0.03) * 1.2
    return np.tanh((click + ring + thud + grit) * 1.3)


def ready_rustle(seed):
    rng = np.random.default_rng(seed)
    dur = 0.4
    t = t_axis(dur)
    p = t / dur
    env = np.sin(np.pi * np.clip(p, 0, 1)) ** 1.5 * (1 + 0.6 * np.sin(2 * np.pi * 11 * t))
    cloth = bandpass_tv(rng.standard_normal(len(t)), 1800 + 800 * p, 0.7) * env
    grip = sum(np.exp(-((t - c) / 0.004) ** 2) * rng.uniform(0.5, 1.0) for c in (0.12, 0.27)) * rng.standard_normal(len(t)) * 0.8
    return cloth + lowpass(grip, 2500)


for i, (s, lo, hi, d) in enumerate(((11, 320, 1250, 0.34), (12, 290, 1100, 0.36), (13, 360, 1400, 0.32)), 1):
    save("PipeSwing_%d" % i, whoosh(s, d, 0.42, lo, hi), 0.55)
for i, (s, p) in enumerate(((21, 1.0), (22, 0.92), (23, 1.08), (24, 0.97)), 1):
    save("PipeHitFlesh_%d" % i, flesh_hit(s, p), 0.95)
for i, (s, p) in enumerate(((31, 1.0), (32, 0.9)), 1):
    save("PipeHitWall_%d" % i, wall_hit(s, p), 0.8)
save("PipeReady", ready_rustle(41), 0.35)
print("SOUNDS_DONE", sorted(os.listdir(OUT)))
