# Coward meeting 1: every line (id, speaker K/C, English, Arabic). Used to build the silent voice placeholders
# (one WAV per line, length estimated from the English text + 0.3-0.5 s breath at the edges) and the subtitle entries.
LINES = [
 ("CW1_001","C","Don't shoot! Don't shoot! It wasn't me, I swear!","لا تطلق! لا تطلق! مو أنا، والله!"),
 ("CW1_002","K","...Do what?","...سويت إيش؟"),
 ("CW1_003","C","There's so many of them with my face... but I'm not one of them! Please!","في واجد منهم بوجهي... بس أنا مو منهم! تكفى!"),
 ("CW1_004","K","You locked me in that store.","انت قفلت علي البقالة."),
 ("CW1_005","C","No, no, no. That was one of them. Look, no knife.","لا لا لا. هذاك واحد منهم. شوف، ما معي سكين."),
 ("CW1_006","K","Who are you?","مين انت؟"),
 ("CW1_007","C","Um...","امم..."),
 ("CW1_008","K","I'm Khalid.","أنا خالد."),
 ("CW1_009","C","...Doesn't matter who I am.","...مو مهم مين أنا."),
 ("CW1_010","K","What happened here?","وش صار هني؟"),
 ("CW1_011","C","Nobody knows.","محد يدري."),
 ("CW1_012","C","Don't read those out loud. Something always answers.","لا تقرا هذي بصوت عالي. دايمًا في شي يرد."),
 ("CW1_013","K","I'm looking for a boy. He had a backpack.","أدور ولد. معاه شنطة."),
 ("CW1_014","C","...Is he yours?","...ولدك؟"),
 ("CW1_015","K","...He's my brother.","...أخوي."),
 ("CW1_A01","C","Then I hope you find him. He went toward the school.","الله يعينك تلقاه. راح صوب المدرسة."),
 ("CW1_A02","C","But don't go into the alley there. People go in. They don't come out.","بس لا تدخل السكة اللي هناك. الناس يدخلون. وما يطلعون."),
 ("CW1_A03","K","I have to.","لازم."),
 ("CW1_A04","C","No. You don't.","لا. مو لازم."),
 ("CW1_A05","K","Move.","وخر."),
 ("CW1_A06","C","Can't you hear me?! I'm telling you, you'll die in there!","ما تسمع؟! أقولك بتموت هناك!"),
 ("CW1_A07","K","Let go of me.","فكني."),
 ("CW1_A08","C","For what?! For a kid who's probably already\u2014","عشان إيش؟! عشان ولد يمكن أصلًا\u2014"),
 ("CW1_B01","C","Then I hope you find him. Where did you see him?","الله يعينك تلقاه. وين شفته؟"),
 ("CW1_B02","K","In the alley by the school. I just came from there.","في السكة اللي عند المدرسة. توني طالع منها."),
 ("CW1_B03","C","...You went in there? And you came back out?","...دخلت هناك؟ وطلعت؟"),
 ("CW1_B04","K","Yeah.","إيه."),
 ("CW1_B05","C","Nobody comes back out of there.","محد يطلع من هناك."),
 ("CW1_B06","K","I did.","أنا طلعت."),
 ("CW1_B07","C","...Then something let you.","...أجل في شي خلاك تطلع."),
 ("CW1_B08","K","I'm going back. The boy went that way.","برجع. الولد راح من هناك."),
 ("CW1_B09","C","No. Whatever let you out won't do it twice.","لا. اللي خلاك تطلع ما راح يسويها مرتين."),
 ("CW1_B10","K","Move.","وخر."),
 ("CW1_B11","C","Can't you hear me?! You got lucky! You'll die in there!","ما تسمع؟! انت محظوظ! بتموت هناك!"),
 ("CW1_B12","K","Let go of me.","فكني."),
 ("CW1_B13","C","For what?! For a kid who's probably already\u2014","عشان إيش؟! عشان ولد يمكن أصلًا\u2014"),
 ("CW1_E01","C","...Fine. Go. Don't say I didn't warn you.","...خلاص. روح. لا تقول ما حذرتك."),
 ("CW1_E02","C","I can't stay here.","ما أقدر أقعد هني."),
]

def est_duration(en, speaker="K"):
    rate = 0.25 if speaker == "C" else 0.30   # the coward talks fast and breathy
    words = len([w for w in en.replace("\u2014", " ").split() if any(ch.isalpha() for ch in w)])
    d = 0.35 + words * rate + en.count("...") * 0.35 + (en.count("!") + en.count("?")) * 0.08 + 0.4
    if en.endswith("\u2014"): d -= 0.25          # cut on the dash, keep the breath
    return round(max(1.0, d), 2)
