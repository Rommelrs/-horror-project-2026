# Builds 4 reshaped variations of the ORIGINAL drip (WaterDrip_Dry.wav) for DrippingPipe.
# Run: python make_soft_drip.py   -> writes Assets/Audio/Water/WaterDrip_Soft_1..4.wav
# v1 added a low "thump" and sounded like a drum, so this version keeps the original water "plip" and only:
#   - shifts its pitch per variation (so no two consecutive drips are identical),
#   - rolls off the harsh top end (the ping),
#   - shortens the ring so it is a quick plip instead of a tonal bloop,
#   - matches A-weighted loudness to the original.
import wave, os
import numpy as np

SR = 48000
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Audio", "Water")
REF = os.path.join(OUT_DIR, "WaterDrip_Dry.wav")


def read_wav(path):
    w = wave.open(path)
    return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768


def write_wav(path, x):
    x = np.clip(x, -1, 1)
    w = wave.open(path, "wb")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((x * 32767).astype("<i2").tobytes())
    w.close()


def lowpass(x, hi, order=4):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return np.fft.irfft(X / (1 + (f / hi) ** (2 * order)), len(x))


def a_weighted_rms(x):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    f2 = np.maximum(f, 1.0) ** 2
    ra = (12194 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2))
    return np.sqrt(np.sum((np.abs(X) * ra) ** 2)) / len(x)


def resample(x, factor):
    """factor > 1 = higher pitch and shorter."""
    n = int(len(x) / factor)
    return np.interp(np.arange(n) * factor, np.arange(len(x)), x)


ref = read_wav(REF)
ref = np.pad(ref, (0, 0))
ref_loud = a_weighted_rms(ref)
# (pitch factor, low-pass corner Hz, ring time constant s)
variants = [(0.84, 2300, 0.016), (0.93, 2500, 0.014), (1.06, 2700, 0.013), (1.17, 2900, 0.012)]
for i, (pf, lp_hz, ring) in enumerate(variants, 1):
    x = resample(ref, pf)
    t = np.arange(len(x)) / SR
    x = lowpass(x, lp_hz)
    x *= np.exp(-np.maximum(t - 0.022, 0) / ring)                       # shorter ring after the strike
    x[:int(0.0008 * SR)] *= np.linspace(0, 1, int(0.0008 * SR))         # no click at the start
    fade = int(0.012 * SR)
    x[-fade:] *= np.linspace(1, 0, fade)
    # match the old drip's loudness without exceeding full scale: raise the gain and soft-limit (gentle compression)
    x /= np.abs(x).max()
    lo, hi_g = 0.5, 8.0
    for _ in range(40):
        g = (lo + hi_g) / 2
        y = np.tanh(g * x) / np.tanh(g) * 0.92
        if a_weighted_rms(y) < ref_loud: lo = g
        else: hi_g = g
    x = y
    path = os.path.join(OUT_DIR, "WaterDrip_Soft_%d.wav" % i)
    write_wav(path, x)
    X = np.abs(np.fft.rfft(x * np.hanning(len(x)))); f = np.fft.rfftfreq(len(x), 1 / SR)
    print("%s  len %.0f ms  peak %.2f  A-loudness vs old %.2f  centroid %.0f Hz  peak freq %.0f Hz" % (
        os.path.basename(path), len(x) / SR * 1000, np.abs(x).max(), a_weighted_rms(x) / ref_loud, (X * f).sum() / X.sum(), f[X.argmax()]))
X = np.abs(np.fft.rfft(ref * np.hanning(len(ref)))); f = np.fft.rfftfreq(len(ref), 1 / SR)
print("old clip: len %.0f ms centroid %.0f Hz peak freq %.0f Hz" % (len(ref) / SR * 1000, (X * f).sum() / X.sum(), f[X.argmax()]))
