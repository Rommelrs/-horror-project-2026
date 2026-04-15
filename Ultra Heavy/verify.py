"""Audit pass: verify final texture sizes, check normal maps gone, recompute folder sizes."""
import os, json
from pathlib import Path
from PIL import Image

PROJECT_ROOT = Path(r"G:\Game Projects\-horror-project-2026")
ASSETS_DIR = PROJECT_ROOT / "Assets"
OUT = PROJECT_ROOT / "Ultra Heavy" / "AUDIT.txt"

TEXTURE_EXTS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".psd"}

DEFAULT_MAX = 256
SKYBOX_MAX = 1024
SKYBOX_FRAGS = ["allskyfree", "/skybox", "\\skybox", "equirect"]

oversized_default = []
oversized_skybox = []
remaining_normals = []
remaining_tga = []
total_count = 0
total_bytes = 0
dim_buckets = {"<=256": 0, "257-512": 0, "513-1024": 0, "1025-2048": 0, ">2048": 0}

# Heaviest-by-folder
folder_bytes = {}

for root, dirs, files in os.walk(ASSETS_DIR):
    dirs[:] = [d for d in dirs if d != ".git"]
    for fn in files:
        ext = os.path.splitext(fn)[1].lower()
        if ext not in TEXTURE_EXTS and ext not in {".exr", ".hdr"}:
            continue
        p = Path(root) / fn
        rel = str(p.relative_to(PROJECT_ROOT))
        try:
            sz = p.stat().st_size
        except Exception:
            continue
        total_count += 1
        total_bytes += sz
        # Track folder size (top-level Assets subfolder)
        parts = p.relative_to(ASSETS_DIR).parts
        top = parts[0] if parts else "(root)"
        folder_bytes[top] = folder_bytes.get(top, 0) + sz

        # Check normal map name leaked through
        stem_lower = p.stem.lower()
        if any(t in stem_lower for t in ["_normal", "_nrm", "_normalmap", "_nor_"]) or stem_lower.endswith("_n") or stem_lower.endswith("_nor"):
            remaining_normals.append((rel, sz))
        if ext == ".tga":
            remaining_tga.append((rel, sz))
        # Sample dims
        if ext in TEXTURE_EXTS:
            try:
                with Image.open(p) as im:
                    w, h = im.size
                    largest = max(w, h)
                    if largest <= 256: dim_buckets["<=256"] += 1
                    elif largest <= 512: dim_buckets["257-512"] += 1
                    elif largest <= 1024: dim_buckets["513-1024"] += 1
                    elif largest <= 2048: dim_buckets["1025-2048"] += 1
                    else: dim_buckets[">2048"] += 1
                    rl = rel.lower()
                    is_sb = any(f in rl for f in SKYBOX_FRAGS)
                    if is_sb and largest > SKYBOX_MAX:
                        oversized_skybox.append((rel, w, h, sz))
                    elif not is_sb and largest > DEFAULT_MAX:
                        oversized_default.append((rel, w, h, sz))
            except Exception:
                pass

with open(OUT, "w", encoding="utf-8") as fp:
    fp.write("=================================================\n")
    fp.write(" TEXTURE OPTIMIZATION AUDIT\n")
    fp.write("=================================================\n\n")
    fp.write(f"Total texture files: {total_count}\n")
    fp.write(f"Total bytes:         {total_bytes/1048576:.1f} MB\n\n")

    fp.write("--- Dimension distribution ---\n")
    for k, v in dim_buckets.items():
        fp.write(f"  {k:>10}: {v}\n")
    fp.write("\n")

    fp.write(f"--- Skybox files exceeding {SKYBOX_MAX}px ---\n")
    if not oversized_skybox:
        fp.write("  (none)\n")
    for rel, w, h, sz in sorted(oversized_skybox, key=lambda x: -x[3]):
        fp.write(f"  {w}x{h}  {sz/1024:.0f} KB  {rel}\n")
    fp.write("\n")

    fp.write(f"--- Default textures exceeding {DEFAULT_MAX}px ---\n")
    if not oversized_default:
        fp.write("  (none)\n")
    for rel, w, h, sz in sorted(oversized_default, key=lambda x: -x[3])[:30]:
        fp.write(f"  {w}x{h}  {sz/1024:.0f} KB  {rel}\n")
    if len(oversized_default) > 30:
        fp.write(f"  ... and {len(oversized_default)-30} more\n")
    fp.write("\n")

    fp.write(f"--- Suspected normal map files still present ({len(remaining_normals)}) ---\n")
    if not remaining_normals:
        fp.write("  (none)\n")
    for rel, sz in remaining_normals[:30]:
        fp.write(f"  {sz/1024:.0f} KB  {rel}\n")
    fp.write("\n")

    fp.write(f"--- TGA files still present ({len(remaining_tga)}) ---\n")
    if not remaining_tga:
        fp.write("  (none)\n")
    for rel, sz in remaining_tga[:30]:
        fp.write(f"  {sz/1024:.0f} KB  {rel}\n")
    fp.write("\n")

    fp.write("--- Per top-level folder, current texture bytes ---\n")
    for folder, b in sorted(folder_bytes.items(), key=lambda x: -x[1]):
        if b > 1024 * 100:  # skip <100KB
            fp.write(f"  {b/1048576:7.1f} MB  Assets/{folder}\n")

print("Audit written to", OUT)
print(f"Files: {total_count}, Total: {total_bytes/1048576:.1f} MB")
print(f"Dim buckets: {dim_buckets}")
print(f"Oversized default: {len(oversized_default)}, oversized skybox: {len(oversized_skybox)}")
print(f"Remaining normals: {len(remaining_normals)}, remaining TGAs: {len(remaining_tga)}")
