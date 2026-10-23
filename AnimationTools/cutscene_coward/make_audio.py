# Writes the silent voice placeholders (one per line) + a wind loop + a low drone loop for the coward meeting cutscene.
import os, sys, json, wave, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from lines import LINES, est_duration
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
SR = 22050

def write(path, data, sr=SR):
    data = np.clip(data, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((data * 32767).astype(np.int16).tobytes())

durs = {}
for lid, spk, en, ar in LINES:
    d = est_duration(en, spk)
    durs[lid] = d
    write(os.path.join(OUT, f"{lid}_{'Khalid' if spk == 'K' else 'Coward'}.wav"), np.zeros(int(SR * d)))
json.dump(durs, open(os.path.join(OUT, "durations.json"), "w"), indent=1)

rng = np.random.default_rng(3)
def lowpass(x, a):            # one-pole lowpass, a in (0,1): smaller = darker
    y = np.empty_like(x); acc = 0.0
    for i in range(len(x)): acc += a * (x[i] - acc); y[i] = acc
    return y
def loopify(x, fade):         # crossfade the tail into the head so it loops cleanly
    n = int(fade * SR); head = x[:n].copy(); tail = x[-n:]
    w = np.linspace(0, 1, n); x = x[:-n].copy(); x[:n] = head * w + tail * (1 - w); return x

# wind: slow gusts of filtered noise, 20 s loop
T = 22.0; t = np.arange(int(SR * T)) / SR
noise = rng.standard_normal(len(t))
w1 = lowpass(noise, 0.02); w2 = lowpass(rng.standard_normal(len(t)), 0.06)
gust = 0.55 + 0.45 * np.sin(2*np.pi*t/7.3 + 1.0) * np.sin(2*np.pi*t/3.1 + 0.3)
wind = (w1 * 6.0 + w2 * 1.2) * gust
wind = loopify(wind / np.max(np.abs(wind)) * 0.45, 2.0)
write(os.path.join(OUT, "Wind_Loop.wav"), wind)

# drone: low detuned tones with slow beating + dark noise bed, 24 s loop
T = 26.0; t = np.arange(int(SR * T)) / SR
drone = (np.sin(2*np.pi*48.0*t) + 0.8*np.sin(2*np.pi*48.6*t + 0.7) + 0.35*np.sin(2*np.pi*96.3*t) + 0.18*np.sin(2*np.pi*143.7*t + 2.0))
drone *= 0.75 + 0.25 * np.sin(2*np.pi*t/9.0)
drone += lowpass(rng.standard_normal(len(t)), 0.01) * 3.0
drone = loopify(drone / np.max(np.abs(drone)) * 0.5, 2.0)
write(os.path.join(OUT, "Drone_Loop.wav"), drone)
print("ok", len(LINES), "lines, total voice", round(sum(durs.values()), 1), "s")
