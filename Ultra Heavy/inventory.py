"""Dry-run inventory: classify all textures, write classification report. No file changes."""
import os, re, json, sys
from pathlib import Path

PROJECT_ROOT = Path(r"G:\Game Projects\-horror-project-2026")
ASSETS_DIR = PROJECT_ROOT / "Assets"
OUT = PROJECT_ROOT / "Ultra Heavy" / "inventory.txt"

TEXTURE_EXTS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".psd"}
SKIP_EXTS = {".exr", ".hdr"}

SKIP_PATH_FRAGMENTS = ["textmesh pro", "tmp_", "tmpro", "lightingdata", "reflectionprobe-", "_oversizedmodels"]
SKIP_FN = [re.compile(r"^lightmap-", re.IGNORECASE), re.compile(r"^reflectionprobe-", re.IGNORECASE)]
SKYBOX = ["allskyfree", "/skybox", "\\skybox", "equirect"]
NORMAL = [
    re.compile(r"_N$", re.IGNORECASE),
    re.compile(r"_Nor$", re.IGNORECASE),
    re.compile(r"_NRM$", re.IGNORECASE),
    re.compile(r"_Normal$", re.IGNORECASE),
    re.compile(r"_Normals$", re.IGNORECASE),
    re.compile(r"_Norm$", re.IGNORECASE),
    re.compile(r"_NormalMap$", re.IGNORECASE),
    re.compile(r"_normal\b", re.IGNORECASE),
    re.compile(r"\bNormal$", re.IGNORECASE),
    re.compile(r"_nor_", re.IGNORECASE),
    re.compile(r"_normalmap\b", re.IGNORECASE),
]

def is_skipped(rel):
    low = rel.lower()
    if any(f in low for f in SKIP_PATH_FRAGMENTS): return True
    fn = os.path.basename(rel)
    return any(p.search(fn) for p in SKIP_FN)

def is_skybox(rel):
    low = rel.lower()
    return any(f in low for f in SKYBOX)

def is_normal(p: Path):
    if any(pat.search(p.stem) for pat in NORMAL):
        return True, "filename"
    meta = p.with_suffix(p.suffix + ".meta")
    if meta.exists():
        try:
            t = meta.read_text(encoding="utf-8", errors="ignore")
            if re.search(r"m_TextureType:\s*1\b", t):
                return True, "meta"
        except Exception: pass
    return False, ""

cats = {"delete_normal": [], "skip_protected": [], "skip_format": [], "skybox": [], "default": []}
total_bytes = {"delete_normal": 0, "skip_protected": 0, "skip_format": 0, "skybox": 0, "default": 0}

for root, dirs, files in os.walk(ASSETS_DIR):
    dirs[:] = [d for d in dirs if d != ".git"]
    for fn in files:
        ext = os.path.splitext(fn)[1].lower()
        if ext not in TEXTURE_EXTS and ext not in SKIP_EXTS: continue
        p = Path(root) / fn
        rel = str(p.relative_to(PROJECT_ROOT))
        try: sz = p.stat().st_size
        except: sz = 0
        if is_skipped(rel):
            cats["skip_protected"].append((rel, sz, "path"))
            total_bytes["skip_protected"] += sz
            continue
        nm, why = is_normal(p)
        if nm:
            cats["delete_normal"].append((rel, sz, why))
            total_bytes["delete_normal"] += sz
            continue
        if ext in SKIP_EXTS:
            cats["skip_format"].append((rel, sz, "ext"))
            total_bytes["skip_format"] += sz
            continue
        if is_skybox(rel):
            cats["skybox"].append((rel, sz, ""))
            total_bytes["skybox"] += sz
            continue
        cats["default"].append((rel, sz, ""))
        total_bytes["default"] += sz

with open(OUT, "w", encoding="utf-8") as fp:
    fp.write("=== TEXTURE INVENTORY (DRY RUN) ===\n\n")
    for cat in ["delete_normal", "skip_protected", "skip_format", "skybox", "default"]:
        n = len(cats[cat])
        mb = total_bytes[cat] / 1048576
        fp.write(f"[{cat}] {n} files, {mb:.1f} MB\n")
    fp.write("\n=== TOP 30 normal maps to be deleted (by size) ===\n")
    for rel, sz, why in sorted(cats["delete_normal"], key=lambda x: -x[1])[:30]:
        fp.write(f"  {sz/1048576:6.1f} MB [{why}]  {rel}\n")
    fp.write("\n=== ALL skybox files ===\n")
    for rel, sz, _ in sorted(cats["skybox"], key=lambda x: -x[1]):
        fp.write(f"  {sz/1048576:6.1f} MB  {rel}\n")
    fp.write("\n=== TOP 30 default textures (by size) ===\n")
    for rel, sz, _ in sorted(cats["default"], key=lambda x: -x[1])[:30]:
        fp.write(f"  {sz/1048576:6.1f} MB  {rel}\n")
    fp.write("\n=== TOP 30 skipped (protected) ===\n")
    for rel, sz, _ in sorted(cats["skip_protected"], key=lambda x: -x[1])[:30]:
        fp.write(f"  {sz/1048576:6.1f} MB  {rel}\n")

print("Inventory written to", OUT)
for cat in ["delete_normal", "skip_protected", "skip_format", "skybox", "default"]:
    print(f"  {cat}: {len(cats[cat])} files, {total_bytes[cat]/1048576:.1f} MB")
