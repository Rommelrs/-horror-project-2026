# Minimal Arabic shaper for PIL (no raqm / arabic_reshaper installed): picks the joined letter forms
# (Arabic Presentation Forms-B), makes lam-alef ligatures, and returns the string in visual (left-to-right) order,
# keeping digit / Latin runs in their own left-to-right order.   from arabic_shape import shape
# Tashkeel (harakat, tanween) is dropped.

# letter: (isolated, final, initial, medial) - None for forms that don't exist (right-joining letters)
FORMS = {
    "ء": ("ﺀ", None, None, None),
    "آ": ("ﺁ", "ﺂ", None, None), "أ": ("ﺃ", "ﺄ", None, None),
    "ؤ": ("ﺅ", "ﺆ", None, None), "إ": ("ﺇ", "ﺈ", None, None),
    "ئ": ("ﺉ", "ﺊ", "ﺋ", "ﺌ"), "ا": ("ﺍ", "ﺎ", None, None),
    "ب": ("ﺏ", "ﺐ", "ﺑ", "ﺒ"), "ة": ("ﺓ", "ﺔ", None, None),
    "ت": ("ﺕ", "ﺖ", "ﺗ", "ﺘ"), "ث": ("ﺙ", "ﺚ", "ﺛ", "ﺜ"),
    "ج": ("ﺝ", "ﺞ", "ﺟ", "ﺠ"), "ح": ("ﺡ", "ﺢ", "ﺣ", "ﺤ"),
    "خ": ("ﺥ", "ﺦ", "ﺧ", "ﺨ"), "د": ("ﺩ", "ﺪ", None, None),
    "ذ": ("ﺫ", "ﺬ", None, None), "ر": ("ﺭ", "ﺮ", None, None),
    "ز": ("ﺯ", "ﺰ", None, None), "س": ("ﺱ", "ﺲ", "ﺳ", "ﺴ"),
    "ش": ("ﺵ", "ﺶ", "ﺷ", "ﺸ"), "ص": ("ﺹ", "ﺺ", "ﺻ", "ﺼ"),
    "ض": ("ﺽ", "ﺾ", "ﺿ", "ﻀ"), "ط": ("ﻁ", "ﻂ", "ﻃ", "ﻄ"),
    "ظ": ("ﻅ", "ﻆ", "ﻇ", "ﻈ"), "ع": ("ﻉ", "ﻊ", "ﻋ", "ﻌ"),
    "غ": ("ﻍ", "ﻎ", "ﻏ", "ﻐ"), "ف": ("ﻑ", "ﻒ", "ﻓ", "ﻔ"),
    "ق": ("ﻕ", "ﻖ", "ﻗ", "ﻘ"), "ك": ("ﻙ", "ﻚ", "ﻛ", "ﻜ"),
    "ل": ("ﻝ", "ﻞ", "ﻟ", "ﻠ"), "م": ("ﻡ", "ﻢ", "ﻣ", "ﻤ"),
    "ن": ("ﻥ", "ﻦ", "ﻧ", "ﻨ"), "ه": ("ﻩ", "ﻪ", "ﻫ", "ﻬ"),
    "و": ("ﻭ", "ﻮ", None, None), "ى": ("ﻯ", "ﻰ", None, None),
    "ي": ("ﻱ", "ﻲ", "ﻳ", "ﻴ"),
}
LAM_ALEF = {"آ": ("ﻵ", "ﻶ"), "أ": ("ﻷ", "ﻸ"), "إ": ("ﻹ", "ﻺ"), "ا": ("ﻻ", "ﻼ")}
TASHKEEL = set(chr(c) for c in range(0x064B, 0x0653)) | {"ٰ"}

def _dual(ch):
    f = FORMS.get(ch)
    return f is not None and f[2] is not None

def _joins(ch):
    return ch in FORMS and ch != "ء"

def _shape_logical(text):
    s = [c for c in text if c not in TASHKEEL]
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c not in FORMS:
            out.append(c); i += 1; continue
        prev_joins = i > 0 and _dual(s[i - 1]) and not (out and out[-1] in ("ﻻ", "ﻼ", "ﻵ", "ﻶ", "ﻷ", "ﻸ", "ﻹ", "ﻺ"))
        # lam + alef ligature
        if c == "ل" and i + 1 < len(s) and s[i + 1] in LAM_ALEF:
            iso, fin = LAM_ALEF[s[i + 1]]
            out.append(fin if prev_joins else iso); i += 2; continue
        next_joins = i + 1 < len(s) and _joins(s[i + 1])
        iso, fin, ini, med = FORMS[c]
        if ini is None:                      # right-joining only
            out.append(fin if prev_joins else iso)
        elif prev_joins and next_joins:
            out.append(med)
        elif prev_joins:
            out.append(fin)
        elif next_joins:
            out.append(ini)
        else:
            out.append(iso)
        i += 1
    return "".join(out)

def _is_ltr(c):
    return c.isascii() and (c.isalnum() or c in ":.%/-+")

def shape(text):
    """logical Arabic text -> shaped string in visual order for PIL"""
    shaped = _shape_logical(text)
    # split into runs: LTR runs (digits/latin, with inner punctuation) stay as they are, the run order is reversed
    runs, cur, cur_ltr = [], "", None
    for c in shaped:
        ltr = _is_ltr(c)
        if c == " " and cur_ltr:              # a space after an LTR run: decide by what follows -> treat as RTL
            ltr = False
        if cur_ltr is None or ltr == cur_ltr:
            cur += c
        else:
            runs.append((cur, cur_ltr)); cur = c
        cur_ltr = ltr
    if cur:
        runs.append((cur, cur_ltr))
    vis = []
    for r, ltr in reversed(runs):
        vis.append(r if ltr else r[::-1])
    return "".join(vis)
