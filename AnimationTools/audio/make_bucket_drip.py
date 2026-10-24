# Cuts the reference recording "Water drip on another..." (several drops in 1 s) into single-drop clips for DrippingPipe.
# Run: python make_bucket_drip.py -> Assets/Audio/Water/WaterDrip_Bucket_1..3.wav (mono, 48k)
import subprocess, wave, os
import numpy as np

SR = 48000
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = r"D:/Unity Stuff/Shamkha Refs/Water_drip_on_anothe_#4-1792500665822.mp3"
OUT = os.path.join(ROOT, "Assets", "Audio", "Water")
raw = subprocess.run(["ffmpeg", "-v", "error", "-i", SRC, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True).stdout
x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)

# (start, end) seconds of each single drop found in the recording
cuts = [(0.012, 0.20), (0.255, 0.50), (0.600, 0.78)]
for i, (a, b) in enumerate(cuts, 1):
    s = x[int(a * SR):int(b * SR)].copy()
    n = int(0.003 * SR); s[:n] *= np.linspace(0, 1, n)             # no click at the cut
    n = int(0.04 * SR); s[-n:] *= np.linspace(1, 0, n) ** 2        # fade the tail out
    # the source is quiet (-29 LUFS): lift it and soft-limit (gentle compression) so each drop is as present as the old drip
    s /= np.abs(s).max()
    g = 3.0
    s = np.tanh(g * s) / np.tanh(g) * 0.85
    s *= min(0.10 / np.sqrt((s ** 2).mean()), 0.95 / np.abs(s).max())   # similar loudness for every drop
    w = wave.open(os.path.join(OUT, "WaterDrip_Bucket_%d.wav" % i), "wb")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(s, -1, 1) * 32767).astype("<i2").tobytes()); w.close()
    print(i, "%.0f ms" % ((b - a) * 1000), "rms %.3f" % np.sqrt((s ** 2).mean()))
