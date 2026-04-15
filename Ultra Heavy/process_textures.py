"""
Texture optimization pass for the Horror Project.

Plan:
  - Skip TMP fonts, lightmaps, reflection probes, Unity-generated textures.
  - Delete all normal maps (filename pattern OR meta TextureType: 1).
  - Skyboxes (AllSkyFree, Skybox folders) -> resize to 1024 max dimension.
  - Everything else -> resize to 256 max dimension.
  - Convert TGA -> PNG (rename .tga.meta -> .png.meta to preserve GUID).
  - PSD -> PNG (merged image only).
  - Skip EXR/HDR (Pillow can't reliably handle).

Run from the project root.
"""

from __future__ import annotations

import os
import re
import sys
import json
import shutil
from pathlib import Path
from PIL import Image

# Pillow opens PSD as merged image
try:
    from PIL import PsdImagePlugin  # noqa: F401
except Exception:
    pass

PROJECT_ROOT = Path(r"G:\Game Projects\-horror-project-2026")
ASSETS_DIR = PROJECT_ROOT / "Assets"
OUT_DIR = PROJECT_ROOT / "Ultra Heavy"
MANIFEST = OUT_DIR / "manifest.jsonl"
LOG = OUT_DIR / "process.log"

DEFAULT_MAX = 256
SKYBOX_MAX = 1024

TEXTURE_EXTS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".psd"}
SKIP_EXTS = {".exr", ".hdr"}  # Pillow can't reliably handle these

# Skip these path fragments (case-insensitive substring match)
SKIP_PATH_FRAGMENTS = [
    "textmesh pro",
    "tmp_",
    "tmpro",
    "lightingdata",
    "reflectionprobe-",
    "_OversizedModels".lower(),  # backup folder, leave alone
]
SKIP_FILENAME_PATTERNS = [
    re.compile(r"^lightmap-", re.IGNORECASE),
    re.compile(r"^reflectionprobe-", re.IGNORECASE),
]

# Skybox classification
SKYBOX_PATH_FRAGMENTS = ["allskyfree", "/skybox", "\\skybox", "equirect"]

# Normal map filename patterns (matched against filename without extension)
NORMAL_PATTERNS = [
    re.compile(r"_N$", re.IGNORECASE),
    re.compile(r"_Nor$", re.IGNORECASE),
    re.compile(r"_NRM$", re.IGNORECASE),
    re.compile(r"_Normal$", re.IGNORECASE),
    re.compile(r"_Normals$", re.IGNORECASE),
    re.compile(r"_Norm$", re.IGNORECASE),
    re.compile(r"_NormalMap$", re.IGNORECASE),
    re.compile(r"_normal\b", re.IGNORECASE),
    re.compile(r"\bNormal$", re.IGNORECASE),
    re.compile(r"_nor_", re.IGNORECASE),  # _nor_gl_2k style
    re.compile(r"_normalmap\b", re.IGNORECASE),
]


def is_skipped_path(rel_path: str) -> bool:
    low = rel_path.lower()
    for frag in SKIP_PATH_FRAGMENTS:
        if frag in low:
            return True
    fname = os.path.basename(rel_path)
    for pat in SKIP_FILENAME_PATTERNS:
        if pat.search(fname):
            return True
    return False


def is_skybox(rel_path: str) -> bool:
    low = rel_path.lower()
    return any(frag in low for frag in SKYBOX_PATH_FRAGMENTS)


def is_normal_map(file_path: Path) -> bool:
    stem = file_path.stem
    for pat in NORMAL_PATTERNS:
        if pat.search(stem):
            return True
    # Check .meta file for TextureType: 1
    meta = file_path.with_suffix(file_path.suffix + ".meta")
    if meta.exists():
        try:
            text = meta.read_text(encoding="utf-8", errors="ignore")
            # m_TextureType: 1 = Normal Map; m_ConvertToNormalMap: 1 = also normal-ish
            if re.search(r"m_TextureType:\s*1\b", text):
                return True
        except Exception:
            pass
    return False


def process_one(src: Path, manifest_fp, log_fp) -> dict:
    """Process a single texture file. Returns a manifest entry dict."""
    rel = str(src.relative_to(PROJECT_ROOT))
    ext = src.suffix.lower()
    entry = {
        "path": rel,
        "ext": ext,
        "size_before": 0,
        "size_after": 0,
        "action": None,
        "dim_before": None,
        "dim_after": None,
        "error": None,
    }
    try:
        entry["size_before"] = src.stat().st_size
    except Exception as e:
        entry["error"] = f"stat: {e}"
        return entry

    if is_skipped_path(rel):
        entry["action"] = "skip:protected_path"
        entry["size_after"] = entry["size_before"]
        return entry

    # Normal-map deletion runs BEFORE format check so EXR/HDR normals get deleted too
    if is_normal_map(src):
        meta = src.with_suffix(src.suffix + ".meta")
        try:
            src.unlink()
            if meta.exists():
                meta.unlink()
            entry["action"] = "delete:normal_map"
            entry["size_after"] = 0
        except Exception as e:
            entry["error"] = f"delete: {e}"
            entry["size_after"] = entry["size_before"]
        return entry

    if ext in SKIP_EXTS:
        entry["action"] = "skip:unsupported_format"
        entry["size_after"] = entry["size_before"]
        return entry

    target_max = SKYBOX_MAX if is_skybox(rel) else DEFAULT_MAX

    # Open image
    try:
        with Image.open(src) as im:
            im.load()
            w, h = im.size
            entry["dim_before"] = [w, h]
            mode = im.mode
            # Determine target size (preserve aspect ratio, only downsize)
            largest = max(w, h)
            if largest > target_max:
                scale = target_max / largest
                new_w = max(1, int(round(w * scale)))
                new_h = max(1, int(round(h * scale)))
                im_resized = im.resize((new_w, new_h), Image.LANCZOS)
            else:
                new_w, new_h = w, h
                im_resized = im.copy()
            entry["dim_after"] = [new_w, new_h]

            # Decide output path
            output_format = "PNG"
            if ext in (".tga", ".psd", ".tif", ".tiff", ".bmp"):
                # Convert to PNG and rename .meta
                out_path = src.with_suffix(".png")
                # Avoid clobbering existing PNG
                if out_path.exists() and out_path != src:
                    # Add suffix to avoid collision (extremely unlikely)
                    out_path = src.with_name(src.stem + "_conv.png")
            elif ext == ".png":
                out_path = src
                output_format = "PNG"
            elif ext in (".jpg", ".jpeg"):
                out_path = src
                output_format = "JPEG"
            else:
                out_path = src

            # Mode handling
            if output_format == "JPEG":
                if im_resized.mode in ("RGBA", "LA", "P"):
                    im_resized = im_resized.convert("RGB")
                im_resized.save(out_path, format="JPEG", quality=85, optimize=True)
            else:
                if im_resized.mode == "P":
                    im_resized = im_resized.convert("RGBA")
                elif im_resized.mode not in ("RGB", "RGBA", "L", "LA"):
                    im_resized = im_resized.convert("RGBA")
                im_resized.save(out_path, format="PNG", optimize=True)

            # If output path differs from src, delete the original and rename .meta
            if out_path != src:
                old_meta = src.with_suffix(src.suffix + ".meta")
                new_meta = out_path.with_suffix(out_path.suffix + ".meta")
                src.unlink()
                if old_meta.exists():
                    if new_meta.exists() and new_meta != old_meta:
                        # Already a meta for the new file; remove old to avoid orphan
                        old_meta.unlink()
                    else:
                        old_meta.rename(new_meta)
                entry["action"] = f"convert+resize:{ext}->png"
            else:
                entry["action"] = "resize" if largest > target_max else "kept_smaller_than_target"

            try:
                entry["size_after"] = out_path.stat().st_size
            except Exception:
                entry["size_after"] = 0
    except Exception as e:
        entry["error"] = f"process: {e}"
        entry["size_after"] = entry["size_before"]
        log_fp.write(f"ERROR {rel}: {e}\n")
    return entry


def main():
    OUT_DIR.mkdir(exist_ok=True)
    files = []
    for root, dirs, filenames in os.walk(ASSETS_DIR):
        # Don't walk into .git
        dirs[:] = [d for d in dirs if d != ".git"]
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext in TEXTURE_EXTS or ext in SKIP_EXTS:
                files.append(Path(root) / fn)

    total = len(files)
    print(f"Found {total} texture-like files to consider.")

    counts = {
        "deleted_normal": 0,
        "skipped_protected": 0,
        "skipped_format": 0,
        "resized": 0,
        "converted": 0,
        "kept_small": 0,
        "errors": 0,
    }
    bytes_before = 0
    bytes_after = 0

    with open(MANIFEST, "w", encoding="utf-8") as mfp, open(LOG, "w", encoding="utf-8") as lfp:
        for i, f in enumerate(files, 1):
            entry = process_one(f, mfp, lfp)
            mfp.write(json.dumps(entry) + "\n")
            bytes_before += entry["size_before"]
            bytes_after += entry["size_after"]
            act = entry["action"] or ""
            if entry["error"]:
                counts["errors"] += 1
            elif act.startswith("delete"):
                counts["deleted_normal"] += 1
            elif act == "skip:protected_path":
                counts["skipped_protected"] += 1
            elif act == "skip:unsupported_format":
                counts["skipped_format"] += 1
            elif act.startswith("convert"):
                counts["converted"] += 1
            elif act == "resize":
                counts["resized"] += 1
            elif act == "kept_smaller_than_target":
                counts["kept_small"] += 1
            if i % 250 == 0:
                print(f"  {i}/{total}  ({(bytes_before-bytes_after)/1048576:.0f} MB saved so far)")

    print("\n=== SUMMARY ===")
    for k, v in counts.items():
        print(f"  {k}: {v}")
    print(f"  bytes_before: {bytes_before/1048576:.1f} MB")
    print(f"  bytes_after:  {bytes_after/1048576:.1f} MB")
    print(f"  saved:        {(bytes_before-bytes_after)/1048576:.1f} MB")
    print(f"\nManifest: {MANIFEST}")
    print(f"Log:      {LOG}")


if __name__ == "__main__":
    main()
