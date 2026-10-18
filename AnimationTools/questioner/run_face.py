# one call: rebuild the v2 head (SDF), teeth, eyes, detail + paint, preview shots.   exec(open(this).read()) with TAG / FACE_VS set
HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
_tag = globals().get("TAG", "w")
FACE_VS = globals().get("FACE_VS", 0.0015)
exec(open(HERE + r"\q14_face.py", encoding="utf8").read())
exec(open(HERE + r"\q15_teeth_eyes.py", encoding="utf8").read())
if globals().get("DETAIL", True):
    exec(open(HERE + r"\q16_detail.py", encoding="utf8").read())
only_visible(["Q_HeadHR", "Q_Teeth2", "Q_Eyes2"] + list(globals().get("ALSO_SHOW", [])))
shots(_tag, globals().get("VIEWS", ("front", "side", "straight", "profile", "q34", "game", "gameside", "ufront", "uside")))
result = {"done": _tag}
