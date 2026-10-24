using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Builds "The coward's first meeting" (Alley 1 crossroads) in the open Game scene: the scene marks, the coward NPC,
// the virtual cameras, audio sources, the four timelines (Open / A / B / End) and the CowardMeetingCutscene controller.
// Re-runnable: it deletes its own "CowardMeeting1" object and rebuilds everything from the numbers below, so timing
// and camera tweaks are best made here (or afterwards directly in the Timeline window).
public static class CowardMeetingBuilder
{
    const string Dir = "Assets/Cutscenes/CowardMeeting1";
    const string AudioDir = "Assets/Audio/Cutscenes/CowardMeeting1";
    const float Floor = 1.45f;

    // ── scene marks: the crossroads of the main alley (papers on the ground), just outside the burned rope gate ──
    // Khalid steps out of the gate and walks into the crossroads (K1). The coward comes walking down the main alley
    // from the north, past the west end of the blue barrel, not looking; Khalid spots him, turns and aims, and the
    // coward freezes in front of him (C1). At the end Khalid walks off east into the side alley across the crossroads
    // (toward the school) and the coward bolts back north, the way he came.
    static readonly Vector3 K0 = new Vector3(-180.1f, Floor, -32.3f);    // the trigger, just out of the rope gate
    static readonly Vector3 K1 = new Vector3(-176.3f, Floor, -29.8f);
    const float KhalidWalkYaw = 56.7f;                                    // gate -> K1
    static readonly Vector3 C1 = new Vector3(-177.6f, Floor, -28.0f);    // 2.2 m from Khalid
    // He comes down the alley on Mixamo's "Male Careful Walk" (hunched, arms in, wary steps - a coward, not a stroll),
    // baked into a straight multi-cycle walk (BakeLoopedWalk -> Coward_CarefulWalk.anim, 0.825 m/s). He reaches C1 at
    // CowardArrive, coming from just south of the hand truck (the line clears the barrel's west end).
    const string CarefulWalk = Dir + "/Coward_CarefulWalk.anim";
    const float CowardWalkSpeed = 0.825f, CowardArrive = 5.5f, CarefulStep = 1.733f / 2f;
    static readonly Vector3 WalkFrom = new Vector3(-178.6f, Floor, -21.0f);
    static readonly float WalkYaw = YawTo(WalkFrom, C1);
    static readonly Vector3 WalkStart = C1 + (WalkFrom - C1).normalized * CowardWalkSpeed * CowardArrive;
    // the careful walk travels 3.4 deg left of where its body faces: turned so he ends on C1
    const float WalkHeadingFix = 3.4f;
    static readonly float K1Yaw = YawTo(K1, C1);              // facing the coward at C1
    static readonly float CowardYaw = YawTo(C1, K1);          // facing Khalid
    // his way out, down the middle of the east side alley (walls at z -30.45 and -33.7, from x -170 on). He turns away
    // off camera and we cut to him already walking in it. Walk_SH covers 0.95 m/s (a planted foot slides back at that
    // speed), so he is moved at exactly that and his feet stay put.
    static readonly Vector3 EastIn = new Vector3(-169.0f, Floor, -32.08f);
    static readonly Vector3 EastFar = new Vector3(-148.0f, Floor, -32.08f);
    const float WalkSpeed = 0.95f;

    static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

    // The gun-draw close-up was laid out in the first staging (knife alley, Khalid facing east). It is kept in those
    // "staging" numbers (S...) and moved as one rigid block that puts the staging K1 -> C1 pair onto the real K1 -> C1.
    static readonly Vector3 StagingK1 = new Vector3(-197.8f, Floor, -32.3f);
    static readonly float Turn = K1Yaw - 70.3f;
    static Vector3 S(float x, float y, float z) => K1 + Quaternion.Euler(0f, Turn, 0f) * (new Vector3(x, y, z) - StagingK1);

    // Every camera stays on Khalid's right-hand (east) side of the line between the two of them, so Khalid is always
    // frame-left and the coward frame-right and no cut crosses the line. The blue barrel stands ~3 m beyond the coward,
    // a little to that side: the coward's singles look across from the right so it stays out of their frame.
    // Dialogue axis: Khalid at K1 -> coward at C1. Ax(f, r, h) = f metres toward the coward, r to Khalid's right.
    static Vector3 DF => (C1 - K1).normalized;
    static Vector3 DR => Quaternion.Euler(0f, 90f, 0f) * DF;
    static Vector3 Ax(float f, float r, float h) => At(K1 + DF * f + DR * r, h);
    static Vector3 At(Vector3 p, float height) => new Vector3(p.x, Floor + height, p.z);

    class Line { public string id, spk, en, ar, audio; public float offset, speechEnd; public Vector2[] pauses = new Vector2[0]; }
    static Dictionary<string, Line> lines;
    static StringTableCollectionRef subtitles;
    struct StringTableCollectionRef { public UnityEditor.Localization.StringTableCollection col; }

    [MenuItem("Tools/Cutscenes/Build Coward Meeting 1")]
    public static void Build()
    {
        LoadLines();
        BakeLoopedWalk("Careful_Walk_Male", CarefulWalk, CowardArrive + 1.5f);
        var alley1 = GameObject.Find("Alley 1 Gameplay");
        if (alley1 == null) { Debug.LogError("[CowardMeeting] Alley 1 Gameplay not found - open the Game scene."); return; }

        var old = alley1.transform.Find("CowardMeeting1");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("CowardMeeting1");
        root.transform.SetParent(alley1.transform, false);
        root.transform.SetPositionAndRotation(K0, Quaternion.identity);

        var marks = Child(root.transform, "Marks");
        var mK0 = Mark(marks, "K0", K0, 90f);
        var mK1 = Mark(marks, "K1", K1, K1Yaw);
        Mark(marks, "CowardWalkStart", WalkStart, WalkYaw);
        Mark(marks, "C1", C1, CowardYaw);
        Mark(marks, "EastIn", EastIn, 90f);
        PlanWalks();
        // "I'm not leaving.": he looks away from the coward, off to his right and a little down
        var mLookAway = Mark(marks, "KhalidLookAway", K1 + Quaternion.Euler(0f, K1Yaw + 70f, 0f) * Vector3.forward * 3f + Vector3.up * 0.8f, 0f);
        // the glass breaks in a window up in the alley's north wall, on his left a few metres ahead of where he stops
        Vector3 stopAt = WalkPos(WalkS(endWalk, EndStop));
        var mGlass = Mark(marks, "GlassWindow", new Vector3(stopAt.x + 3f, Floor + 4.3f, -30.5f), 0f);
        // a skip puts him where the walk leaves him
        var mSkip = Mark(marks, "KhalidAfterSkip", WalkPos(endWalk.s[endWalk.s.Count - 1]), 90f);
        Mark(marks, "EXIT_FogLine", ExitEnd, 0f);

        RestoreKnifeAlleyWall();

        // ── actors ──
        var actors = Child(root.transform, "Actors");
        var mover = Child(actors, "CowardMover");
        var cowardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/CowardNPC.prefab");
        var coward = (GameObject)PrefabUtility.InstantiatePrefab(cowardPrefab, mover);
        coward.name = "CowardNPC";
        coward.transform.SetPositionAndRotation(WalkStart, Quaternion.Euler(0, WalkYaw, 0));
        var cowardAnim = coward.GetComponent<Animator>();
        var cowardHead = FindDeep(coward.transform, "Head");
        var cowardHips = FindDeep(coward.transform, "Hips");
        var cowardChest = FindDeep(coward.transform, "Spine2") ?? cowardHips;
        coward.SetActive(false);

        var player = Object.FindFirstObjectByType<Player>();
        Animator khalidAnim = null;
        foreach (var a in player.GetComponentsInChildren<Animator>(true)) if (a.name == "MainCharacterAnimated") khalidAnim = a;
        var khalidHead = FindDeep(khalidAnim.transform, "head.x");

        // ── audio ──
        var audioRoot = Child(root.transform, "Audio");
        var mixer = AssetDatabase.FindAssets("t:AudioMixer").Select(g => AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(m => m != null && m.name.Contains("HorrorProject"));
        AudioMixerGroup Group(params string[] names)
        {
            if (mixer == null) return null;
            foreach (var n in names) { var g = mixer.FindMatchingGroups(n); if (g != null && g.Length > 0) return g[0]; }
            return null;
        }
        var voGroup = Group("Voice", "Dialogue", "SFX", "Master");
        var sfxGroup = Group("SFX", "Master");
        var ambGroup = Group("Ambience", "Ambient", "SFX", "Master");
        var voKhalid = Source(audioRoot, "VO_Khalid", Vector3.zero, false, 1f, voGroup);
        var voCoward = Source(audioRoot, "VO_Coward", Vector3.zero, false, 1f, voGroup);
        // the voices come from the speakers' heads, heard from wherever the shot is, with a little of the alley on them
        // (2D they sounded like someone talking into a mic). Khalid's is carried to his head by the controller.
        Voice(voKhalid); Voice(voCoward);
        voCoward.transform.SetParent(cowardHead, false);
        voCoward.transform.localPosition = Vector3.zero;
        var sfxAlcove = Source(audioRoot, "SFX_Coward", (WalkStart + C1) * 0.5f + Vector3.up * 0.3f, true, 1f, sfxGroup);
        var sfx2D = Source(audioRoot, "SFX_2D", Vector3.zero, false, 0.9f, sfxGroup);
        var wind = Source(audioRoot, "Wind", Vector3.zero, false, 0.32f, ambGroup);
        var windGust = Source(audioRoot, "WindGust", Vector3.zero, false, 0.55f, ambGroup);
        var drone = Source(audioRoot, "Drone", Vector3.zero, false, 0.5f, ambGroup);
        var music = Source(audioRoot, "Music", Vector3.zero, false, MusicVolume, Group("Music", "Ambience", "Ambient", "Master"));
        // in that window, close by: loud and placed (it was 20 m off up the alley)
        var glass = Source(audioRoot, "Glass", mGlass.position, true, 1f, sfxGroup);
        glass.minDistance = 3f; glass.maxDistance = 45f; glass.rolloffMode = AudioRolloffMode.Logarithmic;
        var reverb = glass.gameObject.AddComponent<AudioReverbFilter>(); reverb.reverbPreset = AudioReverbPreset.Cave;

        // ── cameras ──
        var cams = Child(root.transform, "Cameras");
        var noiseMild = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
        var noiseStrong = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_strong.asset");
        Vector3 kHead = K1 + Vector3.up * 1.62f, c1Head = C1 + Vector3.up * 1.55f;
        // establishing shots, placed for the crossroads itself
        // from the south end of the main alley: Khalid steps out of the gate (left) and walks in; the pallets are ahead-left
        var cAlleyWide = Vcam(cams, "CM_Alley_Wide", new Vector3(-174.2f, 3.3f, -33.3f), new Vector3(-178.4f, 2.3f, -29.0f), 50f);
        // Khalid spots him: from behind Khalid's right shoulder, the coward walks out of the dust toward him
        var cSpot = Vcam(cams, "CM_Spot", Ax(-1.6f, 1.2f, 1.9f), At(C1, 1.4f), 40f, cowardHead, 0.6f, noiseMild, 0.5f, 0.5f, 0.45f);

        var cKMCU = Vcam(cams, "CM_Khalid_MCU", S(-196.3f, 2.85f, -33.2f), K1 + Vector3.up * 1.45f, 38f);
        // over Khalid's right shoulder onto the coward (frame right): far enough back that his back doesn't fill the frame
        // and far enough round that his aimed gun reads beside the coward, not on his head
        var cOtsK = Vcam(cams, "CM_OTS_Khalid", Ax(-1.0f, 2.0f, 1.85f), c1Head, 34f, cowardHead, 0.66f, noiseMild, 0.6f, 0.35f, 0.42f);
        var cOtsKPush = Vcam(cams, "CM_OTS_Khalid_Push", Ax(-1.0f, 2.0f, 1.85f), c1Head, 34f, cowardHead, 0.66f, noiseMild, 0.6f, 0.35f, 0.42f);
        Drift(cOtsKPush, new Vector3(0, 0, 0.12f), -1.35f, 5.2f);
        // the reverse: from just past the coward's left shoulder onto Khalid (frame left); the hood stays out of frame (it
        // was a black blob bobbing along the bottom)
        var cOtsC = Vcam(cams, "CM_OTS_Coward", Ax(3.07f, 0.75f, 1.95f), kHead, 30f, khalidHead, 0.4f, noiseMild, 0.35f, 0.35f, 0.42f);
        // Khalid's single, off his eyeline to the coward (closer to it, he looked into the lens)
        var cKCU = Vcam(cams, "CM_Khalid_CU", Ax(1.3f, 0.7f, 1.68f), kHead, 34f, khalidHead, 0.42f, null, 0f, 0.35f, 0.5f);
        // the two of them in profile, Khalid left, the coward right, the west wall behind
        var cSide = Vcam(cams, "CM_Side_Two", Ax(1.11f, 3.6f, 1.55f), Ax(1.11f, 0f, 1.15f), 45f);
        // the coward's singles: from between them on Khalid's side, he looks off frame-left at Khalid, pallets behind.
        // Framed on his chest: on his head the camera swam round with every nervous glance
        var cCowardCU = Vcam(cams, "CM_Coward_CU", Ax(0.95f, 0.55f, 1.6f), c1Head, 35f, cowardChest, 0.6f, null, 0f, 0.6f, 0.68f);
        var cCowardCUPush = Vcam(cams, "CM_Coward_CU_Hold", Ax(0.95f, 0.55f, 1.6f), c1Head, 35f, cowardChest, 0.6f, null, 0f, 0.6f, 0.68f);
        Drift(cCowardCUPush, new Vector3(0, 0, 0.1f), -1.2f, 7f);
        var cCowardMCU = Vcam(cams, "CM_Coward_MCU", Ax(0.8f, 0.6f, 1.55f), c1Head, 36f, cowardChest, 0.55f, null, 0f, 0.6f, 0.62f);
        // following Khalid from behind as he walks off: the player sees his stride falter, never his face
        var cWalk = FollowCam(cams, "CM_Walk_Behind", player.transform, khalidHead);
        // from the crossroads' south-east corner (Khalid is past it, in the east alley by then): the coward running off
        // north past the car. Placed already looking at where the run starts, so the composer doesn't whip round.
        var cExit = Vcam(cams, "CM_Exit_Track", new Vector3(-172.9f, 3.1f, -31.5f), At(C1, 1.0f), 45f, cowardHips, 0.5f, null, 0f, 0.9f);

        var skipUI = BuildSkipUI(root.transform, out var skipFill);

        // ── timelines ──
        var timelines = Child(root.transform, "Timelines");
        var dOpen = Director(timelines, "TL_Coward_Open");
        var dA = Director(timelines, "TL_Coward_A");
        var dB = Director(timelines, "TL_Coward_B");
        var dEnd = Director(timelines, "TL_Coward_End");

        var brain = Object.FindFirstObjectByType<CinemachineBrain>();

        var ctx = new Ctx
        {
            brain = brain, khalid = khalidAnim, coward = coward, cowardAnim = cowardAnim,
            voK = voKhalid, voC = voCoward, sfxAlcove = sfxAlcove, sfx2D = sfx2D, wind = wind, windGust = windGust, drone = drone, glass = glass,
            music = music,
        };
        FindMusic();

        BuildOpen(dOpen, ctx, new[] { cAlleyWide, cSpot, cKMCU, cOtsK, cOtsC, cOtsKPush, cKCU, cSide, cCowardCU, cCowardCUPush, cCowardMCU });
        BuildA(dA, ctx, new[] { cCowardCU, cOtsK, cCowardMCU, cWalk });
        BuildB(dB, ctx);
        BuildEnd(dEnd, ctx, new[] { cCowardMCU, cWalk, cCowardCU, cExit });

        // ── controller ──
        // across the rope gate's opening (z -33.7..-30.9), a step out into the crossroads
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true; trigger.size = new Vector3(1.0f, 3f, 2.8f); trigger.center = new Vector3(0, 1.5f, 0);
        var cs = root.AddComponent<CowardMeetingCutscene>();
        cs.open = dOpen; cs.versionA = dA; cs.versionB = dB; cs.end = dEnd;
        cs.knifeHandle = AssetDatabase.LoadAssetAtPath<Item>("Assets/Resources/Items/Key items/Knife Handle.asset");
        cs.coward = coward; cs.cowardMover = mover; cs.cowardHead = cowardHead;
        cs.khalidVoice = voKhalid.transform;
        cs.lookPoints = new[] { mLookAway, mGlass };
        var enemies = alley1.transform.Find("EnemiesAllGroups");
        cs.alleyEnemiesRoot = enemies;
        cs.skipKhalidMark = mSkip;
        cs.skipPrompt = skipUI; cs.skipFill = skipFill;
        cs.subtitleReceivers = new[] { dOpen, dA, dB, dEnd }.Select(d => d.GetComponent<CutsceneSubtitleReceiver>()).ToArray();
        cs.camerasRoot = cams.gameObject;
        // debug "play now": the rope gate shown burned
        var rope = alley1.transform.Find("Burnable Rope");
        var ropeGate = rope != null ? rope.Find("Ropes Gate") : null;
        var ropeBurned = rope != null ? rope.Find("Burned Ropes Gate") : null;
        cs.debugHide = ropeGate != null ? new[] { ropeGate.gameObject } : new GameObject[0];
        cs.debugShow = ropeBurned != null ? new[] { ropeBurned.gameObject } : new GameObject[0];
        cs.phases = new[]
        {
            new CowardMeetingCutscene.Phase
            {
                name = "Open",
                // walks in, sees someone coming out of the dust (head first), stops and turns to him, and stays there
                khalid = new[] { Key(0f, K0, 90f, true), Key(KhalidWalkEnd, K1, KhalidWalkYaw), Key(4.1f, K1, KhalidWalkYaw), Key(5.1f, K1, K1Yaw) },
                headLook = new[] { new Vector2(0f, 0f), new Vector2(3.4f, 0.8f), new Vector2(5.3f, 0f), new Vector2(gunDown + 1.4f, 0.6f),
                                   new Vector2(lookAwayAt, 1f), new Vector2(lookBackAt + 0.8f, 0.6f) },
                // "I'm not leaving." looking off toward the alley, then back at the coward to ask about the boy
                headLookAt = new[] { new Vector2(0f, -1f), new Vector2(lookAwayAt, 0f), new Vector2(lookBackAt, -1f) },
                // he looks at Khalid while they talk, off into the fog for the boy
                cowardLook = new[] { new Vector2(0f, 0f), new Vector2(7.2f, 0.7f), new Vector2(fogFrom, 0f), new Vector2(fogTo + 0.2f, 0.7f) },
                // "...Other versions?": the gun dips a little; the lower takes over from gunDown
                gunDip = new[] { new Vector2(0f, 0f), new Vector2(dipStart, 0f), new Vector2(dipStart + 0.6f, 1f), new Vector2(gunDown, 1f), new Vector2(gunDown + 0.45f, 0f) },
                ambience = new[] { new Vector2(0f, 1f), new Vector2(7.0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "A",
                // stands until "Thanks.", then - on the cut - is walking down the east alley
                // on "Thanks." (the cut to the walk cam) he is at the alley mouth at once - no blended frame in between
                khalid = new[] { Key(0f, K1, K1Yaw) }.Concat(WalkKeys(aWalk).Select((k, i) => { k.cut = i == 0; return k; })).ToArray(),
                headLook = new[] { new Vector2(0f, 0.6f), new Vector2(AWalkStart, 0f) },
                cowardLook = new[] { new Vector2(0f, 0.7f), new Vector2(AWalkStart + 1f, 0.85f) },
                ambience = new[] { new Vector2(0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "B",
                khalid = new[] { Key(0f, K1, K1Yaw) },
                ambience = new[] { new Vector2(0f, 0.71f) },
            },
            new CowardMeetingCutscene.Phase
            {
                name = "End",
                khalid = WalkKeys(endWalk).ToArray(),
                // the glass: he looks up at the window on his left, then back ahead
                headLook = new[] { new Vector2(0f, 0f), new Vector2(LookUpTime, 1f), new Vector2(LookBackTime, 0f) },
                headLookAt = new[] { new Vector2(0f, 1f) },
                // follows Khalid with his eyes; the glass takes them; he looks after him once more, then bolts
                cowardLook = new[] { new Vector2(0f, 0.85f), new Vector2(GlassTime, 0f), new Vector2(InsaneTime - 0.4f, 0.6f), new Vector2(BoltTime - 0.1f, 0f) },
                ambience = new[] { new Vector2(0f, 0.71f), new Vector2(BoltTime + 2.3f, 1f) },
            },
        };
        // the hidden gameplay camera is put behind him under the last shot, before the blend back
        cs.gameplayCameraSnapTime = BoltTime + 2.5f;
        cs.scaredWalkEnd = CowardArrive;
        // hands up as the gun comes up (raise ends GunUp + 0.8), down once he says he believes him
        cs.surrenderUp = GunUp + 0.8f; cs.surrenderDown = handsDown;

        // Alley 2 visited = an Alley 2 entrance door was used
        var flagObj = new GameObject("Alley2VisitedFlag");
        flagObj.transform.SetParent(root.transform, false);
        var flag = flagObj.AddComponent<StoryFlagTrigger>();
        var so = new SerializedObject(flag); so.FindProperty("flag").stringValue = CowardMeetingCutscene.Alley2Flag; so.ApplyModifiedPropertiesWithoutUndo();
        var alley2 = GameObject.Find("Alley 2 Gameplay");
        if (alley2 != null)
            foreach (var door in alley2.GetComponentsInChildren<DoorInteractable>(true))
            {
                if (!door.name.Contains("Entrance")) continue;
                for (int i = door.OnInteractedSuccess.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (door.OnInteractedSuccess.GetPersistentMethodName(i) == nameof(StoryFlagTrigger.SetFlag)) UnityEventTools.RemovePersistentListener(door.OnInteractedSuccess, i);
                UnityEventTools.AddPersistentListener(door.OnInteractedSuccess, flag.SetFlag);
                EditorUtility.SetDirty(door);
            }

        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CowardMeeting] built: Open " + dOpen.playableAsset.duration.ToString("F1") + " s, A " + dA.playableAsset.duration.ToString("F1") + " s, B " + dB.playableAsset.duration.ToString("F1") + " s, End " + dEnd.playableAsset.duration.ToString("F1") + " s");
    }

    // ───────────────────────── timelines ─────────────────────────

    class Ctx
    {
        public CinemachineBrain brain; public Animator khalid, cowardAnim; public GameObject coward;
        public AudioSource voK, voC, sfxAlcove, sfx2D, wind, windGust, drone, glass, music;
    }

    // Khalid walks from the gate to K1 (~4.5 m) by this time
    const float KhalidWalkEnd = 3.9f;

    // Khalid's aim for the scene: a two-handed pistol stance retargeted from Mixamo mocap ("Pistol Idle - Idle With Aimed
    // Pistol"), with a raise (gun up into the chest, pressed out, right foot stepping back into the stance) and a lower
    // (down to a low ready, to his side, foot back) built on it in Blender - AnimationTools/khalid_gun_aim.py. The raise
    // starts and the lower ends on his idle stand pose (idle time 0), the aim loop starts on the raise's last pose.
    const string GunRaise = Dir + "/Khalid_GunRaise.anim", GunAim = Dir + "/Khalid_GunAim.anim", GunLower = Dir + "/Khalid_GunLower.anim";
    const float GunRaiseTime = 0.8f, GunLowerTime = 34f / 30f;
    // he aims at the coward from GunUp; the gun dips a little on "...Other versions?" (the controller tips his arms, the
    // lower clip would lift his foot halfway) and comes down to his side on "Alright... I believe you."
    const float GunUp = 6.3f;
    // after "I'm not leaving." (said looking off toward the alley) he looks back at the coward before asking about the boy
    const float KhalidTurnBack = 1.3f;

    // the Open's beats, from the lines as they fall (the lines follow one another, so a pause in one moves the rest);
    // kStep is just before "You locked me in that store." (the cut to the two of them)
    static float kStep, dipStart, gunDown, handsDown, lookAwayAt, lookBackAt, fogFrom, fogTo;

    // the coward's body clip phase where Open / A leave off, so the next timeline carries on from it
    static float openEndPhase, aEndPhase;

    static void BuildOpen(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_Open");
        var (voK, voC) = VoiceTracks(tl, d, c);
        // the gaps between lines (user: longer, let each line land): ~0.8-1.3 s, longest where the answer is weighed
        var c1 = Say(tl, voK, voC, "CW2_01", 6.6f);               // "Wait, wait! Don't shoot!" as the gun comes up
        var k1 = Say(tl, voK, voC, "CW2_02", c1.end + 0.9f);
        var c2 = Say(tl, voK, voC, "CW2_03", k1.end + 0.8f);
        var k2 = Say(tl, voK, voC, "CW2_04", c2.end + 1.1f);      // "...Other versions?" - taking it in
        var c3 = Say(tl, voK, voC, "CW2_05", k2.end + 0.8f);
        var k3 = Say(tl, voK, voC, "CW2_06", c3.end + 1.2f);      // a breath, the gun coming down
        var c4 = Say(tl, voK, voC, "CW2_07", k3.end + 0.9f);
        var k4 = Say(tl, voK, voC, "CW2_08", c4.end + 1.0f);
        var k5 = Say(tl, voK, voC, "CW2_09", k4.end + KhalidTurnBack);
        var c5 = Say(tl, voK, voC, "CW2_10", k5.end + 1.0f);
        var k6 = Say(tl, voK, voC, "CW2_11", c5.end + 1.0f);
        float len = k6.end + 0.6f;
        kStep = k1.start - 0.2f; dipStart = k2.start - 0.25f; gunDown = k3.start - 0.2f; handsDown = k3.end + 0.15f;
        lookAwayAt = k4.start - 0.25f; lookBackAt = k4.end + 0.1f; fogFrom = c5.start; fogTo = c5.end;

        Activation(tl, d, c.coward, 0, len, ActivationTrack.PostPlaybackState.LeaveAsIs);
        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KClip(k, "rig|khalid_walk", 0f, KhalidWalkEnd + 0.25f, true);   // replaced below by Walk_SH if present
        KClip(k, "rig|khalid_idle", KhalidWalkEnd - 0.15f, GunUp + 0.15f, true);
        KClip(k, GunRaise, GunUp, GunUp + GunRaiseTime, false);
        KClip(k, GunAim, GunUp + GunRaiseTime - 0.05f, gunDown + 0.15f, true);
        // "Alright... I believe you." -> the gun down to his side
        KClip(k, GunLower, gunDown, gunDown + GunLowerTime, false);
        KClip(k, "rig|khalid_idle", gunDown + GunLowerTime - 0.1f, len, true);

        // creeps down the alley glancing round (the head from Look_Around_Nervously on a head-only layer over the walk;
        // the controller bends the rest of him into a scared posture, see CowardMeetingCutscene.PoseLayers); startles
        // when he sees Khalid and throws his hands up (arm IK in the controller, from surrenderUp to surrenderDown) while
        // his body fidgets and talks underneath
        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        var walk = cw.CreateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(CarefulWalk));
        walk.start = 0f; walk.duration = CowardArrive + 0.1f;
        Offsets(walk, WalkStart, WalkYaw + WalkHeadingFix, false);
        var looks = tl.CreateTrack<AnimationTrack>(cw, "Coward head (looking round)");
        looks.avatarMask = HeadMask();
        var look = looks.CreateClip(Mixamo("Look_Around_Nervously"));
        look.start = 0f; look.duration = CowardArrive - 0.2f; look.easeOutDuration = 0.5f;
        ((AnimationPlayableAsset)look.asset).loop = AnimationPlayableAsset.LoopMode.On;
        Chain(cw, "Reacting_Surprised", CowardArrive - 0.25f, 7.4f, C1, CowardYaw, 0.2f);
        Chain(cw, "Nervously_Look_Around", 7.0f, c2.start + 0.1f, C1, CowardYaw);
        Chain(cw, "Talking_TwoHands", c2.start - 0.3f, c2.end + 0.3f, C1, CowardYaw);    // "It wasn't me!..."
        Chain(cw, "Nervously_Look_Around", c2.end, c4.start + 0.1f, C1, CowardYaw);
        Chain(cw, "Talking_TwoHands", c4.start - 0.3f, c4.end + 0.3f, C1, CowardYaw);    // "Look, I'm trying to help you..."
        Chain(cw, "Nervously_Look_Around", c4.end, c5.start + 0.2f, C1, CowardYaw);
        Chain(cw, "Look_Around_Nervously", c5.start - 0.1f, c5.end + 0.3f, C1, CowardYaw); // peering into the fog for the boy
        openEndPhase = Chain(cw, "Nervously_Look_Around", c5.end, len, C1, CowardYaw);

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, 4f);                                   // (opens under the fade from black)
        Shot(cm, d, v[1], 4f, GunUp);                                // he spots him
        Shot(cm, d, v[2], GunUp, 8f);                                // the gun comes up - "Wait, wait!"
        Shot(cm, d, v[3], 8f, kStep);                                // hands up, over the gun
        Shot(cm, d, v[7], kStep, c2.start - 0.15f);                  // "You locked me in that store." gun steady
        Shot(cm, d, v[8], c2.start - 0.15f, c2.phrases[2] - 0.1f);
        Shot(cm, d, v[5], c2.phrases[2] - 0.1f, k2.start - 0.15f);
        Shot(cm, d, v[6], k2.start - 0.15f, c3.start - 0.15f);       // "...Other versions?"
        Shot(cm, d, v[10], c3.start - 0.15f, gunDown - 0.1f);
        Shot(cm, d, v[7], gunDown - 0.1f, c4.start - 0.1f);          // the gun comes down, so do his hands
        Shot(cm, d, v[8], c4.start - 0.1f, c4.phrases[2] - 0.1f);
        Shot(cm, d, v[3], c4.phrases[2] - 0.1f, k4.start - 0.15f);
        Shot(cm, d, v[6], k4.start - 0.15f, c5.start - 0.15f);       // "I'm not leaving." - he looks back - "Have you seen a kid..."
        Shot(cm, d, v[9], c5.start - 0.15f, c5.phrases[2] - 0.1f);
        Shot(cm, d, v[7], c5.phrases[2] - 0.1f, k6.start - 0.15f);
        Shot(cm, d, v[6], k6.start - 0.15f, len);                    // "He's my brother. Where did he go?"

        // his footsteps in the sand (a step every half cycle of the careful walk)
        var sfx = AudioTrackFor(tl, d, "SFX (coward)", c.sfxAlcove);
        for (int i = 0; 0.3f + i * CarefulStep < CowardArrive; i++) Sound(sfx, "Assets/Audio/Footsteps/Sand/Sand" + (i % 4 + 1) + ".mp3", 0.3f + i * CarefulStep);
        var sfx2 = AudioTrackFor(tl, d, "SFX", c.sfx2D);
        Sound(sfx2, "Assets/Audio/NewSFX/pulls_out_gun.wav", GunUp);
        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        var drClip = Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", c2.start, len); drClip.easeInDuration = 3f;
        Music(tl, d, c, len, true, false);
        Finish(tl);
    }

    // Version A: "The alley by the school..." - "Thanks." cuts the coward off, on a cut to Khalid from behind already walking
    // down the east alley (no turn shown); "Wait! He went in there hours ago!" behind him and his stride falters
    static float AWalkStart, AThanks;
    // "The alley by the school." - his answer to "Where did he go?" (Open ends 0.6 s after it)
    const float AFirstLine = 0.7f;

    static void BuildA(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_A");
        float len = aWalk.End;
        var (voK, voC) = VoiceTracks(tl, d, c);
        var c6 = Say(tl, voK, voC, "CW2_A1", AFirstLine);
        Say(tl, voK, voC, "CW2_A2", AThanks);
        var c7 = Say(tl, voK, voC, "CW2_A3", AWalkStart + FalterAt - LineLength("CW2_A3") - 0.05f);   // ends as his stride falters

        Activation(tl, d, c.coward, 0, len, ActivationTrack.PostPlaybackState.LeaveAsIs);
        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        KClip(k, "rig|khalid_idle", 0f, AWalkStart + 0.02f, true);
        var sfx2 = AudioTrackFor(tl, d, "SFX", c.sfx2D);
        aWalkPhase = WalkClips(k, sfx2, aWalk, AWalkStart, len, 0f);

        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        Chain(cw, "Nervously_Look_Around", 0f, AFirstLine + 0.3f, C1, CowardYaw, openEndPhase);
        Chain(cw, "Talking_TwoHands", AFirstLine, c6.end + 0.2f, C1, CowardYaw);
        Chain(cw, "Nervously_Look_Around", c6.end - 0.1f, c7.start + 0.2f, C1, CowardYaw);
        aEndPhase = Chain(cw, "Hands_Forward_Gesture", c7.start - 0.1f, len, C1, CowardYaw);   // "Wait!"

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, c6.phrases[2] - 0.1f);
        Shot(cm, d, v[1], c6.phrases[2] - 0.1f, c6.phrases[3] - 0.1f);
        Shot(cm, d, v[2], c6.phrases[3] - 0.1f, AThanks);   // "Come with me instead, we can-"
        Shot(cm, d, v[3], AThanks, len);                    // "Thanks." - behind him, already walking down the alley

        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", 0f, len);
        Music(tl, d, c, len, false, false);
        FixedLength(tl, len);
        Finish(tl);
    }

    // Version B (Alley 2 already visited) isn't written yet: an empty stand-in, versionBEnabled stays off
    static void BuildB(PlayableDirector d, Ctx c)
    {
        var tl = NewTimeline(d, "TL_Coward_B");
        Activation(tl, d, c.coward, 0, 0.1f, ActivationTrack.PostPlaybackState.LeaveAsIs);
        Finish(tl);
    }

    // both versions end the same: "HEY! I'M TRYING TO SAVE YOUR LIFE!" at his back; glass crashes from a window up on his
    // left, the coward freezes, Khalid stops, looks up at it and back ahead, and walks on; "You're insane..." - a beat -
    // and the coward bolts north
    const float GlassTime = 2.3f, LookUpTime = 2.65f, LookBackTime = 4.2f, InsaneTime = 5.4f, BoltTime = 7.3f;

    static void BuildEnd(PlayableDirector d, Ctx c, CinemachineVirtualCameraBase[] v)
    {
        var tl = NewTimeline(d, "TL_Coward_End");
        float len = endWalk.End;
        Activation(tl, d, c.coward, 0, BoltTime + 5f, ActivationTrack.PostPlaybackState.Inactive);

        var k = Anim(tl, d, "Khalid", c.khalid, TrackOffset.ApplySceneOffsets);
        var sfx2 = AudioTrackFor(tl, d, "SFX", c.sfx2D);
        WalkClips(k, sfx2, endWalk, 0f, EndStop + 0.3f, aWalkPhase);
        KClip(k, "rig|khalid_idle", EndStop, EndGo + 0.3f, true);
        WalkClips(k, sfx2, endWalk, EndGo, len, 0f);

        var cw = Anim(tl, d, "Coward", c.cowardAnim, TrackOffset.ApplyTransformOffsets);
        Chain(cw, "Hands_Forward_Gesture", 0f, GlassTime + 0.25f, C1, CowardYaw, aEndPhase);
        // the glass: he startles and freezes there (the startle's end, slowed right down), says it quietly, waits, bolts
        CClip(cw, "Reacting_Surprised", GlassTime, GlassTime + 1.25f, C1, CowardYaw, false);
        var frozen = CClip(cw, "Reacting_Surprised", GlassTime + 1.2f, BoltTime + 0.15f, C1, CowardYaw, false, 0f, 1.2f);
        frozen.timeScale = 0.12f;
        RunLegs(cw, C1, BoltTime);

        var cm = Cameras(tl, d, c.brain);
        Shot(cm, d, v[0], 0f, GlassTime);                    // "HEY!"
        Shot(cm, d, v[1], GlassTime, InsaneTime - 0.35f);    // behind Khalid: he stops, looks up at the window, walks on
        Shot(cm, d, v[2], InsaneTime - 0.35f, BoltTime);     // "You're insane..." and a beat
        Shot(cm, d, v[3], BoltTime, BoltTime + 2.3f);        // he runs off into the dust
        var back = Shot(cm, d, v[1], BoltTime + 2.3f, len); back.easeOutDuration = 1.5f;   // behind Khalid, then back to play

        var (voK, voC) = VoiceTracks(tl, d, c);
        Say(tl, voK, voC, "CW2_E1", 0.3f); Say(tl, voK, voC, "CW2_E2", InsaneTime);

        Sound(AudioTrackFor(tl, d, "Glass", c.glass), "Assets/Audio/Glass Debris/GlassDestroy.wav", GlassTime);
        for (int i = 0; i < 5; i++) Sound(sfx2, "Assets/Audio/Footsteps/Sand/Sand" + (i % 4 + 1) + ".mp3", BoltTime + 0.3f + i * 0.25f / RunScale);
        Loop(AudioTrackFor(tl, d, "Ambience (wind)", c.wind), AudioDir + "/Wind_Loop.wav", 0f, len);
        var gust = Loop(AudioTrackFor(tl, d, "Wind returns", c.windGust), AudioDir + "/Wind_Loop.wav", BoltTime + 1.5f, len); gust.easeInDuration = 1f; gust.easeOutDuration = 1.2f;
        Loop(AudioTrackFor(tl, d, "Drone", c.drone), AudioDir + "/Drone_Loop.wav", 0f, GlassTime);
        Music(tl, d, c, len, false, true);
        FixedLength(tl, len);
        Finish(tl);
    }

    static readonly Vector3 ExitEnd = new Vector3(-177.5f, Floor, 9.0f);
    // west of the blue barrel (x -177.5..-175.8, z -26.7..-25.5): he runs there first, then straight up the alley between
    // the hand truck and the white car into the dust
    static readonly Vector3 ExitTurn = new Vector3(-177.95f, Floor, -26.1f);
    // Coward_Run.anim is baked at 4.7 m/s; played at RunScale he runs ~3.2 m/s (at full speed he was gone in a blink)
    const float RunSpeed = 4.7f, RunScale = 0.68f;

    // Coward_Run.anim (the in-place Run baked to root motion): one leg to the turn, one up the alley. The second leg
    // carries the run cycle on (clipIn) and is placed back by the distance that skips, since the clip's start offset is
    // removed at its first frame. (He turns to run on a cut.)
    static void RunLegs(AnimationTrack cw, Vector3 from, float start)
    {
        var runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/Coward_Run.anim");
        float leg1 = Vector3.Distance(from, ExitTurn) / (RunSpeed * RunScale);
        var a = cw.CreateClip(runClip);
        a.start = start; a.duration = leg1 + 0.2f; a.timeScale = RunScale;
        Offsets(a, from, YawTo(from, ExitTurn), false);
        var b = cw.CreateClip(runClip);
        float yaw2 = YawTo(ExitTurn, ExitEnd), skipped = (leg1 - 0.1f) * RunScale;
        b.start = start + leg1 - 0.1f; b.clipIn = skipped; b.timeScale = RunScale;
        b.duration = 6f;
        Offsets(b, ExitTurn - Quaternion.Euler(0f, yaw2, 0f) * Vector3.forward * RunSpeed * skipped, yaw2, false);
    }

    // ── Khalid's walk off, down the east alley. Each walk is his distance along that way over time, sampled
    // from a speed profile; the controller moves him through keys at constant speed and the walk clip is played at the
    // matching rate, so the feet don't slide. A's walk carries straight on into End's (same clip phase and distance). ──
    class Walk { public float t0; public readonly List<float> s = new List<float>(); public const float Dt = 1f / 30f; public float End => t0 + (s.Count - 1) * Dt; }
    static Walk aWalk, endWalk;
    static float aWalkPhase;
    // A: walking from the cut on; a beat after "He could already be-" his stride falters for a step
    const float FalterAt = 4.6f, FalterTime = 0.9f, AWalkLength = 6.5f;
    // End: he stops for the glass, looks up at it and back, then walks on
    const float EndStop = 2.9f, EndGo = 4.7f, EndLength = 13.0f;

    static void PlanWalks()
    {
        // "Thanks." cuts in on the coward's "we can-", on the cut to him walking
        AThanks = AFirstLine + LineLength("CW2_A1") - 0.1f;
        AWalkStart = AThanks;
        aWalk = PlanWalk(AWalkStart, 0f, AWalkLength, t =>
        {
            float v = WalkSpeed;
            if (t > FalterAt && t < FalterAt + FalterTime) v *= 1f - 0.65f * Mathf.Sin(Mathf.PI * (t - FalterAt) / FalterTime);
            return v;
        });
        endWalk = PlanWalk(0f, aWalk.s[aWalk.s.Count - 1], EndLength, t =>
        {
            if (t < GlassTime) return WalkSpeed;
            if (t < EndStop) return WalkSpeed * 0.5f * (1f + Mathf.Cos(Mathf.PI * (t - GlassTime) / (EndStop - GlassTime)));
            if (t < EndGo) return 0f;
            return WalkSpeed * Mathf.SmoothStep(0f, 1f, (t - EndGo) / 0.7f);
        });
    }

    static Walk PlanWalk(float t0, float s0, float duration, System.Func<float, float> speed)
    {
        var w = new Walk { t0 = t0 };
        float dist = s0; w.s.Add(dist);
        int n = Mathf.RoundToInt(duration / Walk.Dt);
        for (int i = 0; i < n; i++) { dist += speed((i + 0.5f) * Walk.Dt) * Walk.Dt; w.s.Add(dist); }
        return w;
    }

    static float WalkS(Walk w, float t)
    {
        float f = Mathf.Clamp((t - w.t0) / Walk.Dt, 0f, w.s.Count - 1.001f);
        int i = (int)f;
        return Mathf.Lerp(w.s[i], w.s[i + 1], f - i);
    }

    // down the east alley
    static Vector3 WalkPos(float dist)
    {
        Vector3[] pts = { EastIn, EastFar };
        for (int i = 0; i < pts.Length - 1; i++)
        {
            float seg = Vector3.Distance(pts[i], pts[i + 1]);
            if (dist <= seg || i == pts.Length - 2) return Vector3.LerpUnclamped(pts[i], pts[i + 1], dist / seg);
            dist -= seg;
        }
        return pts[pts.Length - 1];
    }

    // the way he faces: along the path, rounded over the corner
    static float WalkYawAt(float dist) => YawTo(WalkPos(Mathf.Max(0f, dist - 0.7f)), WalkPos(dist + 0.7f));

    // pose keys every 0.2 s, passed through at constant speed
    static IEnumerable<CowardMeetingCutscene.PoseKey> WalkKeys(Walk w)
    {
        for (float t = w.t0; t < w.End + 0.1f; t += 0.2f)
        {
            float time = Mathf.Min(t, w.End), dist = WalkS(w, time);
            var key = Key(time, WalkPos(dist), WalkYawAt(dist));
            key.linear = true;
            yield return key;
        }
    }

    // the walk clip from `from` to `to` in short pieces, each played at the speed he is moving then (never slower than
    // 0.35, a near-stop is left to an idle blended over it); a footstep each half cycle. Returns the clip phase reached.
    static float WalkClips(AnimationTrack k, AudioTrack steps, Walk w, float from, float to, float phase)
    {
        var clip = KhalidClip("rig|khalid_walk");
        float L = clip.length;
        // the first and last pieces are 0.5 s, so a blend to or from idle overlaps only one of them
        var cuts = new List<float> { from };
        for (float t = from + 0.5f; t < to - 0.5f; t += 0.25f) cuts.Add(t);
        cuts.Add(to);
        int stepIndex = 0;
        for (int i = 0; i < cuts.Count - 1; i++)
        {
            float a = cuts[i], b = cuts[i + 1];
            float rate = Mathf.Max(0.35f, (WalkS(w, b) - WalkS(w, a)) / (b - a) / WalkSpeed);
            var c = KClip(k, "rig|khalid_walk", a, b, true);
            c.clipIn = Mathf.Repeat(phase, L); c.timeScale = rate;
            float next = phase + (b - a) * rate;
            for (float half = Mathf.Floor(phase / (L * 0.5f)) + 1; half * L * 0.5f < next; half++)
                Sound(steps, "Assets/Audio/Footsteps/Sand/Sand" + (stepIndex++ % 4 + 1) + ".mp3", a + (half * L * 0.5f - phase) / rate);
            phase = next;
        }
        return phase;
    }

    // Terrified's first 0.7 s are an upright stand before he cowers (it looked like going to idle and then getting scared)
    const float TerrifiedIn = 0.7f;

    // A body clip held for a while: back-to-back copies cross-faded over `fade` (the Mixamo clips don't loop cleanly,
    // and looping them snapped the pose back to the first frame every cycle). The last copy is slowed to end exactly
    // at `end` instead of holding a frozen last frame. Returns the clip time reached at `end`.
    static float Chain(AnimationTrack cw, string name, float start, float end, Vector3 pos, float yaw, float clipIn = 0f, float fade = 0.6f)
    {
        float len = Mixamo(name).length;
        float t = start, ci = Mathf.Repeat(clipIn, len);
        // Terrified opens standing upright and only then cowers: every copy of it starts already cowering
        float repeatFrom = name == "Terrified" ? TerrifiedIn + 0.8f : 0f;
        while (true)
        {
            float left = len - ci;
            bool last = t + left >= end - fade - 0.4f;   // the rest would be too short for another copy
            var c = cw.CreateClip(Mixamo(name));
            c.start = t; c.clipIn = ci;
            if (last)
            {
                float dur = end - t;
                c.duration = dur;
                if (dur > left) c.timeScale = left / dur;   // stretch rather than hold the last frame
                Offsets(c, pos, yaw, false);
                return ci + (float)(dur * c.timeScale);
            }
            c.duration = left;
            Offsets(c, pos, yaw, false);
            t += left - fade; ci = repeatFrom;
        }
    }

    // overlapping clips on a track cross-fade over the whole overlap (both sides set, or the mixer adds them up)
    static void FixBlends(TimelineAsset tl)
    {
        foreach (var track in tl.GetOutputTracks())
        {
            if (!(track is AnimationTrack)) continue;
            var clips = track.GetClips().OrderBy(c => c.start).ToList();
            foreach (var c in clips) { c.blendInDuration = 0; c.blendOutDuration = 0; }
            for (int i = 1; i < clips.Count; i++)
            {
                double overlap = clips[i - 1].end - clips[i].start;
                if (overlap <= 0) continue;
                clips[i].blendInDuration = overlap;
                clips[i - 1].blendOutDuration = overlap;
            }
        }
    }

    // ───────────────────────── timeline helpers ─────────────────────────

    static TimelineAsset NewTimeline(PlayableDirector d, string name)
    {
        string path = Dir + "/" + name + ".playable";
        var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (tl == null) { tl = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(tl, path); }
        foreach (var t in tl.GetRootTracks().ToList()) tl.DeleteTrack(t);
        if (tl.markerTrack != null) foreach (var m in tl.markerTrack.GetMarkers().ToList()) tl.markerTrack.DeleteMarker(m);
        tl.editorSettings.frameRate = 30;
        tl.durationMode = TimelineAsset.DurationMode.BasedOnClips;
        d.playableAsset = tl;
        return tl;
    }

    static void Finish(TimelineAsset tl) { FixBlends(tl); EditorUtility.SetDirty(tl); }

    // ends with the walk (the last footstep sounds would run it on, with Khalid's walk clips already over)
    static void FixedLength(TimelineAsset tl, float len)
    {
        tl.durationMode = TimelineAsset.DurationMode.FixedLength;
        tl.fixedDuration = len;
    }

    static void Activation(TimelineAsset tl, PlayableDirector d, GameObject go, float start, float end, ActivationTrack.PostPlaybackState post)
    {
        var t = tl.CreateTrack<ActivationTrack>(null, go.name + " active");
        var clip = t.CreateDefaultClip(); clip.start = start; clip.duration = end - start;
        t.postPlaybackState = post;
        d.SetGenericBinding(t, go);
    }

    // A Mixamo walk cycle repeated into one straight walk of `seconds`: the root carries on forward cycle after cycle
    // (Timeline can't loop root motion), every other curve gets its small start/end mismatch spread over the cycle so
    // the seams don't pop. Humanoid curves; the foot goals are body-relative and repeat as they are.
    // Baked once: re-baking over the clip on every build left the in-memory clip stale, and the first Play after a build
    // walked him from the world origin (the clip offsets lost). Delete the .anim to bake it again.
    static void BakeLoopedWalk(string mixamoName, string outPath, float seconds)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null) return;
        var src = Mixamo(mixamoName);
        float L = src.length; int cycles = Mathf.CeilToInt(seconds / L);
        var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(outPath), frameRate = 30f };
        int n = Mathf.RoundToInt(cycles * L * 30f);
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            float v0 = c.Evaluate(0f), delta = c.Evaluate(L) - v0;
            bool advance = b.propertyName == "RootT.x" || b.propertyName == "RootT.z";
            var keys = new Keyframe[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / 30f;
                int k = Mathf.Min(Mathf.FloorToInt(t / L), cycles - 1);
                float u = t - k * L;
                keys[i] = new Keyframe(t, c.Evaluate(u) + (advance ? k * delta : -delta * (u / L)));
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetEditorCurve(clip, b, curve);
        }
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = false; st.keepOriginalPositionY = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
        if (existing == null) AssetDatabase.CreateAsset(clip, outPath);
        else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); }
        AssetDatabase.SaveAssets();
    }

    // humanoid mask with only the head + neck: a layer that turns his head without touching the walk under it
    static AvatarMask HeadMask()
    {
        string path = Dir + "/Coward_HeadOnly.mask";
        var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (m == null) { m = new AvatarMask(); AssetDatabase.CreateAsset(m, path); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i == (int)AvatarMaskBodyPart.Head);
        EditorUtility.SetDirty(m);
        return m;
    }

    static AnimationTrack Anim(TimelineAsset tl, PlayableDirector d, string name, Animator target, TrackOffset offsets)
    {
        var t = tl.CreateTrack<AnimationTrack>(null, name);
        t.trackOffset = offsets;
        t.position = Vector3.zero; t.rotation = Quaternion.identity;
        d.SetGenericBinding(t, target);
        return t;
    }

    static AnimationClip KhalidClip(string name)
    {
        if (name.StartsWith("Assets/")) return AssetDatabase.LoadAssetAtPath<AnimationClip>(name);
        if (name == "rig|khalid_walk")
        {
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MainCharacter/Walk_SH.anim");
            if (walk != null) return walk;
        }
        return ModelClip("Assets/MainCharacter/MainCharacterAnimated.fbx", name);
    }

    static TimelineClip KClip(AnimationTrack t, string clipName, float start, float end, bool loop, float blendIn = 0f)
    {
        var clip = t.CreateClip(KhalidClip(clipName));
        clip.start = start; clip.duration = end - start;
        var a = (AnimationPlayableAsset)clip.asset; a.loop = loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
        a.removeStartOffset = false;
        if (blendIn > 0f) clip.blendInDuration = blendIn;
        return clip;
    }

    static AnimationClip ModelClip(string path, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && c.name == name) return c;
        Debug.LogError("[CowardMeeting] clip not found: " + path + " : " + name);
        return null;
    }

    static AnimationClip Mixamo(string name) => ModelClip(Dir + "/Mixamo/" + name + ".fbx", name);

    static TimelineClip CClip(AnimationTrack t, string clipName, float start, float end, Vector3 pos, float yaw, bool loop, float blendIn = 0f, float clipIn = 0f)
    {
        var clip = t.CreateClip(Mixamo(clipName));
        clip.start = start; clip.duration = end - start; clip.clipIn = clipIn;
        Offsets(clip, pos, yaw, loop);
        if (blendIn > 0f) clip.blendInDuration = blendIn;
        return clip;
    }

    static void Offsets(TimelineClip clip, Vector3 pos, float yaw, bool loop)
    {
        var a = (AnimationPlayableAsset)clip.asset;
        a.removeStartOffset = true;
        a.position = pos; a.rotation = Quaternion.Euler(0f, yaw, 0f);
        a.loop = loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
    }

    static CinemachineTrack Cameras(TimelineAsset tl, PlayableDirector d, CinemachineBrain brain)
    {
        var t = tl.CreateTrack<CinemachineTrack>(null, "Cameras");
        d.SetGenericBinding(t, brain);
        return t;
    }

    static TimelineClip Shot(CinemachineTrack t, PlayableDirector d, CinemachineVirtualCameraBase vcam, float start, float end, float easeIn = 0f)
    {
        var clip = t.CreateDefaultClip();
        clip.start = start; clip.duration = end - start;
        clip.displayName = vcam.name;
        var shot = (CinemachineShot)clip.asset;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        d.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
        if (easeIn > 0f) clip.easeInDuration = easeIn;
        return clip;
    }

    static (AudioTrack k, AudioTrack c) VoiceTracks(TimelineAsset tl, PlayableDirector d, Ctx c)
        => (AudioTrackFor(tl, d, "VO Khalid", c.voK), AudioTrackFor(tl, d, "VO Coward", c.voC));

    static AudioTrack AudioTrackFor(TimelineAsset tl, PlayableDirector d, string name, AudioSource src)
    {
        var t = tl.CreateTrack<AudioTrack>(null, name);
        d.SetGenericBinding(t, src);
        return t;
    }

    static TimelineClip Sound(AudioTrack t, string path, float start, float maxLen = 0f)
    {
        var clipAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clipAsset == null) { Debug.LogWarning("[CowardMeeting] missing sound " + path); return null; }
        var clip = t.CreateClip(clipAsset);
        clip.start = start;
        if (maxLen > 0f && clip.duration > maxLen) { clip.duration = maxLen; clip.easeOutDuration = Mathf.Min(0.3f, maxLen * 0.4f); }
        return clip;
    }

    // Music: a PLACEHOLDER track the user picked to hear the mood (not licensed - swap it before anything ships). Any audio
    // file named Music_Placeholder in AudioDir; none = no music track. One piece running on across Open -> A -> End: each
    // timeline picks the track up where the last one left it (looping if it is shorter), fading in at the start and out
    // at the end, under the voices.
    const float MusicVolume = 0.35f;
    static AudioClip music;
    static float musicAt;

    static void FindMusic()
    {
        music = AssetDatabase.FindAssets("Music_Placeholder t:AudioClip", new[] { AudioDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(a => a != null);
        musicAt = 0f;
        Debug.Log(music != null ? "[CowardMeeting] music: " + AssetDatabase.GetAssetPath(music) : "[CowardMeeting] no Music_Placeholder in " + AudioDir + " - no music");
    }

    static void Music(TimelineAsset tl, PlayableDirector d, Ctx c, float len, bool first, bool last)
    {
        if (music == null || c.music == null) return;
        var clip = AudioTrackFor(tl, d, "Music (placeholder)", c.music).CreateClip(music);
        clip.start = 0f; clip.duration = len; clip.clipIn = musicAt % music.length;
        ((AudioPlayableAsset)clip.asset).loop = true;
        if (first) clip.easeInDuration = 2.5f;
        if (last) clip.easeOutDuration = 3f;
        musicAt += len;
    }

    static TimelineClip Loop(AudioTrack t, string path, float start, float end)
    {
        var clip = t.CreateClip(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        clip.start = start; clip.duration = end - start;
        ((AudioPlayableAsset)clip.asset).loop = true;
        return clip;
    }

    const string VoiceDir = Dir + "/Voice Lines";
    // One actor voices both men, so the coward's lines are lowered into a deeper man's voice (rubberband pitch shift with
    // the formants moved along, same length) in Voice Lines/<CowardVoice>/CowardN.wav: Coward_Deep ~-3.5 semitones,
    // Coward_Deeper ~-5.5. (Higher and formant-only versions were tried and sounded wrong.) Empty = the original recordings.
    const string CowardVoice = "Coward_Deep";

    // a time in the recording -> the time in the line once its pauses are in
    // Breaks put into the coward's lines at quiet points between words (he rushed through his "..."): the pauses column
    // of lines.tsv, baked into Voice Lines/Paced/<version>/ by AnimationTools/cutscene_coward/pace_voice.py. (Splitting
    // the line into Timeline clips instead made it stutter.) A pause pushes the rest of the line - and the lines after
    // it - later, and its subtitles with it.
    static float Later(Line line, float t)
    {
        float add = 0f;
        foreach (var p in line.pauses) if (t >= p.x - 0.001f) add += p.y;
        return t + add;
    }

    // from the line's start to the end of its speech, pauses included
    static float LineLength(string id) => Later(lines[id], lines[id].speechEnd);

    class Spoken { public float start, end; public readonly List<float> phrases = new List<float>(); }

    // a recorded line on the speaker's track + its subtitles: a long line is cut into
    // phrases (id + b, c, d...) that come up as they are spoken. Returns when it starts, its speech ends and each phrase.
    static Spoken Say(TimelineAsset tl, AudioTrack k, AudioTrack c, string id, float start)
    {
        var line = lines[id];
        string audio = line.spk == "C" ? "Paced/" + (CowardVoice != "" ? CowardVoice : "Original") + "/" + Path.ChangeExtension(line.audio, ".wav") : line.audio;
        var clipAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + "/" + audio);
        if (clipAsset == null) Debug.LogError("[CowardMeeting] missing voice line " + VoiceDir + "/" + audio + " (run pace_voice.py)");
        var clip = (line.spk == "K" ? k : c).CreateClip(clipAsset);
        clip.start = start; clip.displayName = id;

        var phrases = new List<Line> { line };
        for (char part = 'b'; lines.TryGetValue(id + part, out var phrase); part++) phrases.Add(phrase);
        var said = new Spoken { start = start, end = start + LineLength(id) };
        for (int i = 0; i < phrases.Count; i++)
        {
            float at = start + Later(line, phrases[i].offset);
            // a phrase stays up until the next one comes, the last a moment past the speech
            float until = i + 1 < phrases.Count ? start + Later(line, phrases[i + 1].offset) + 0.15f : Mathf.Max(at + 1.2f, said.end + 0.6f);
            Subtitle(tl, phrases[i], at, until - at);
            said.phrases.Add(at);
        }
        return said;
    }

    static void Subtitle(TimelineAsset tl, Line line, float at, float duration)
    {
        if (tl.markerTrack == null) tl.CreateMarkerTrack();
        var marker = tl.markerTrack.CreateMarker<SubtitleMarker>(at + 0.05);
        var entry = subtitles.col.SharedData.GetEntry(line.en);
        marker.line = new LocalizedString(subtitles.col.TableCollectionName, entry.Id);
        marker.duration = duration;
        marker.lineId = line.id;
        marker.name = line.id;
    }

    static PlayableDirector Director(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var d = go.AddComponent<PlayableDirector>();
        d.playOnAwake = false; d.extrapolationMode = DirectorWrapMode.Hold; d.timeUpdateMode = DirectorUpdateMode.GameTime;
        go.AddComponent<CutsceneSubtitleReceiver>();
        return d;
    }

    static CowardMeetingCutscene.PoseKey Key(float t, Vector3 p, float yaw, bool fromCurrent = false)
        => new CowardMeetingCutscene.PoseKey { time = t, position = p, yaw = yaw, fromCurrent = fromCurrent };

    // ───────────────────────── scene pieces ─────────────────────────

    // the first build cut an alcove into this knife-alley wall and switched the panel off; the scene now happens in
    // the crossroads, so put the original panel back (no-op once it is on)
    static void RestoreKnifeAlleyWall()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Wall Mesh_2 (44)" && t.parent != null && t.parent.name == "New Walls" && !t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                EditorUtility.SetDirty(t.gameObject);
            }
    }

    static CanvasGroup BuildSkipUI(Transform root, out Image fill)
    {
        var go = new GameObject("SkipPrompt", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;
        var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var group = go.AddComponent<CanvasGroup>(); group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        Font font = null;
        if (SubtitleManager.instance == null)
        {
            var sm = Object.FindFirstObjectByType<SubtitleManager>(FindObjectsInactive.Include);
            if (sm != null) { var t = new SerializedObject(sm).FindProperty("subtitleTxt").objectReferenceValue as Text; if (t != null) font = t.font; }
        }
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)textGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-70f, 40f); rt.sizeDelta = new Vector2(420f, 40f);
        var text = textGo.AddComponent<Text>();
        text.text = "Hold E to skip"; text.font = font; text.fontSize = 26; text.alignment = TextAnchor.MiddleRight; text.color = new Color(1f, 1f, 1f, 0.8f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(go.transform, false);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(1f, 0f);
        frt.anchoredPosition = new Vector2(-30f, 45f); frt.sizeDelta = new Vector2(30f, 30f);
        fill = fillGo.AddComponent<Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillAmount = 0f; fill.color = new Color(1f, 1f, 1f, 0.85f);
        return group;
    }

    static CinemachineVirtualCamera Vcam(Transform parent, string name, Vector3 pos, Vector3 lookPoint, float fov,
        Transform lookAt = null, float screenX = 0.5f, NoiseSettings noise = null, float noiseGain = 0f, float damping = 0.35f,
        float screenY = 0.45f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(lookPoint - pos);
        var vc = go.AddComponent<CinemachineVirtualCamera>();
        vc.Priority = 0;
        // keep tracking while not live, so a shot's first frame is already aimed (round-robin left the exit shot
        // pointing at an empty alley for a frame)
        vc.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        vc.m_Lens.FieldOfView = fov; vc.m_Lens.NearClipPlane = 0.05f; vc.m_Lens.FarClipPlane = 600f;
        if (lookAt != null)
        {
            vc.LookAt = lookAt;
            var comp = vc.AddCinemachineComponent<CinemachineComposer>();
            comp.m_ScreenX = screenX; comp.m_ScreenY = screenY;
            comp.m_HorizontalDamping = damping; comp.m_VerticalDamping = damping;
            comp.m_DeadZoneWidth = 0.06f; comp.m_DeadZoneHeight = 0.06f;
        }
        if (noise != null)
        {
            var n = vc.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            n.m_NoiseProfile = noise; n.m_AmplitudeGain = noiseGain; n.m_FrequencyGain = 0.8f;
        }
        return vc;
    }

    // a camera that follows a walker from behind (the offset turns with him, smoothed round corners), aimed at his head
    static CinemachineVirtualCamera FollowCam(Transform parent, string name, Transform follow, Transform lookAt)
    {
        var vc = Vcam(parent, name, follow.position + new Vector3(0f, 1.8f, -2.7f), follow.position + Vector3.up * 1.5f, 42f, lookAt, 0.5f, null, 0f, 0.6f, 0.4f);
        vc.Follow = follow;
        var body = vc.AddCinemachineComponent<CinemachineTransposer>();
        body.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        body.m_FollowOffset = new Vector3(0.35f, 1.75f, -2.7f);
        body.m_XDamping = body.m_YDamping = body.m_ZDamping = 0.6f;
        body.m_YawDamping = 0f;
        return vc;
    }

    static void Drift(CinemachineVirtualCamera vc, Vector3 move, float fov, float duration)
    {
        var d = vc.gameObject.AddComponent<CutsceneCameraDrift>();
        d.move = move; d.fovChange = fov; d.duration = duration;
    }

    static AudioSource Source(Transform parent, string name, Vector3 pos, bool spatial, float volume, AudioMixerGroup group)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (spatial) go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.spatialBlend = spatial ? 1f : 0f; s.volume = volume; s.outputAudioMixerGroup = group;
        s.minDistance = 2f; s.maxDistance = 40f;
        return s;
    }

    // a voice in the scene: mostly positional (some stays centred, so cuts don't swing it hard across), full up close,
    // about half at 15 m, never lost; muffled (the close-mic top end rolled off) and a short alley reverb on it
    static void Voice(AudioSource s)
    {
        s.spatialBlend = 0.85f; s.spread = 70f; s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Custom; s.minDistance = 1f; s.maxDistance = 40f;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(3f / 40f, 1f), new Keyframe(15f / 40f, 0.55f), new Keyframe(1f, 0.3f)));
        var lp = s.gameObject.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 4200f; lp.lowpassResonanceQ = 1f;   // muffled a little (user: 7 kHz still too clean)
        var rv = s.gameObject.AddComponent<AudioReverbFilter>();
        rv.reverbPreset = AudioReverbPreset.User;
        rv.dryLevel = 0f; rv.room = -1300f; rv.roomHF = -400f; rv.roomLF = 0f;
        rv.decayTime = 1.2f; rv.decayHFRatio = 0.7f;
        rv.reflectionsLevel = -900f; rv.reflectionsDelay = 0.01f;
        rv.reverbLevel = -300f; rv.reverbDelay = 0.015f;
        rv.diffusion = 100f; rv.density = 100f;
    }

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        // world-placed children: keep the root's offset out of their positions
        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
        return go.transform;
    }

    static Transform Mark(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        return t;
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // AnimationTools/cutscene_coward/lines.tsv: id, speaker, English, Arabic, phrase start in the recording, recording,
    // where its speech ends, pauses (where:seconds;...). Each line is put in the Subtitles table (keyed by its English)
    // if it isn't there yet.
    static void LoadLines()
    {
        lines = new Dictionary<string, Line>();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var raw in File.ReadAllLines("AnimationTools/cutscene_coward/lines.tsv", System.Text.Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
            var f = raw.Split('\t');
            lines[f[0]] = new Line { id = f[0], spk = f[1], en = f[2], ar = f[3], offset = float.Parse(f[4], inv), audio = f[5],
                                     speechEnd = f.Length > 6 && f[6] != "" ? float.Parse(f[6], inv) : 0f,
                                     pauses = f.Length > 7 && f[7] != "" ? f[7].Split(';').Select(pz => { var ab = pz.Split(':'); return new Vector2(float.Parse(ab[0], inv), float.Parse(ab[1], inv)); }).ToArray() : new Vector2[0] };
        }
        var col = LocalizationEditorSettings.GetStringTableCollection("Subtitles");
        subtitles = new StringTableCollectionRef { col = col };
        foreach (var line in lines.Values)
        {
            var entry = col.SharedData.GetEntry(line.en) ?? col.SharedData.AddKey(line.en);
            foreach (var table in col.StringTables)
            {
                string text = table.LocaleIdentifier.Code.StartsWith("ar") ? line.ar : line.en;
                var e = table.GetEntry(entry.Id);
                if (e == null) table.AddEntry(entry.Id, text); else e.Value = text;
                EditorUtility.SetDirty(table);
            }
        }
        EditorUtility.SetDirty(col.SharedData);
    }
}
